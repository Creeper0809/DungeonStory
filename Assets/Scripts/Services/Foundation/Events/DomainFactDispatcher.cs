using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Foundation.Facts
{
    /// <summary>
    /// Explicit synchronous observation, not a service locator or a logging facade.
    /// A producer publishes typed domain facts without knowing its subscriber.
    /// Exactly one required durable observer owns each fact type; it can be
    /// replaced only after its subscription is disposed. Cohorts commit atomically.
    /// </summary>
    public sealed class DomainFactDispatcher : IDomainFactPublisher, IDomainFactStagingPublisher,
        IDomainFactRecoveryPublisher, IDisposable
    {
        private readonly Dictionary<Type, ISubscription> subscriptions = new();
        private readonly Dictionary<long, Reservation> reservations = new();
        private readonly List<IDisposable> bindingConnections = new();
        private readonly List<DeferredDomainFactObserverBinding> deferredBindings = new();
        private long nextLease;
        private bool disposed;

        public DomainFactDispatcher(IEnumerable<IDomainFactObserverBinding> bindings = null)
        {
            try
            {
                var allDeclared = new HashSet<Type>();
                foreach (var binding in bindings ?? Array.Empty<IDomainFactObserverBinding>())
                {
                    if (binding == null) throw new ArgumentException("A domain observer binding is null.");
                    Type[] declared = binding.FactTypes?.ToArray();
                    if (declared == null || declared.Length == 0 || declared.Any(value => value == null)
                        || declared.Distinct().Count() != declared.Length)
                        throw new InvalidOperationException("Domain observer binding declares invalid fact types.");
                    if (declared.Any(value => !allDeclared.Add(value)))
                        throw new InvalidOperationException("Duplicate domain observer fact declaration.");
                    if (binding is DeferredDomainFactObserverBinding deferred)
                        deferredBindings.Add(deferred);
                    else
                        ConnectBinding(binding, declared);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void ConnectDeferredBindings()
        {
            ThrowIfDisposed();
            try
            {
                foreach (var binding in deferredBindings.ToArray())
                {
                    ConnectBinding(binding, binding.FactTypes.ToArray());
                    deferredBindings.Remove(binding);
                }
            }
            catch { Dispose(); throw; }
        }

        private void ConnectBinding(IDomainFactObserverBinding binding, Type[] declared)
        {
            var before = new HashSet<Type>(subscriptions.Keys);
            IDisposable connected = binding.Connect(this)
                ?? throw new InvalidOperationException("Domain observer binding returned no subscription.");
            Type[] added = subscriptions.Keys.Where(value => !before.Contains(value)).ToArray();
            if (!before.IsSubsetOf(subscriptions.Keys)
                || added.Length != declared.Length || !new HashSet<Type>(declared).SetEquals(added))
            {
                connected.Dispose();
                throw new InvalidOperationException("Domain observer binding did not connect its declared fact types.");
            }
            bindingConnections.Add(connected);
        }

        public IReadOnlyList<Type> SubscribedFactTypes => subscriptions.Keys
            .OrderBy(value => value.FullName, StringComparer.Ordinal).ToArray();
        public int ActiveLeaseCount => reservations.Count;

        public IDisposable Subscribe<TFact>(string observerId, IDurableDomainFactObserver<TFact> observer)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(observerId)) throw new ArgumentException(nameof(observerId));
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (subscriptions.ContainsKey(typeof(TFact))
                || subscriptions.Values.Any(value => value.ObserverId == observerId))
                throw new InvalidOperationException("Duplicate durable domain observer: " + observerId);
            var subscription = new Subscription<TFact>(this, observerId, observer);
            subscriptions.Add(typeof(TFact), subscription);
            return subscription;
        }

        public bool TryReserve<TFact>(in TFact proposed, out DomainFactLease lease, out string failure)
        {
            ThrowIfDisposed();
            lease = default;
            if (!subscriptions.TryGetValue(typeof(TFact), out var value))
            { failure = "domain-fact-observer-missing:" + typeof(TFact).FullName; return false; }
            var subscriber = (Subscription<TFact>)value;
            if (nextLease == long.MaxValue)
            { failure = "domain-fact-lease-sequence-exhausted"; return false; }
            if (!subscriber.Observer.TryReserve(proposed, out object state, out failure)) return false;
            if (state == null) throw new InvalidOperationException("Observer returned an empty reservation.");
            long id = checked(++nextLease);
            reservations.Add(id, new Reservation(subscriber, state));
            lease = new DomainFactLease(this, id);
            return true;
        }

        public DomainFactDelivery Publish<TFact>(in DomainFactLease lease, in TFact actual)
        {
            var results = new DomainFactDelivery[1];
            PublishBatch(new[] { DomainFactEmission.Create(lease, actual) }, results, out _);
            return results[0];
        }

        public bool TryStage<TFact>(in TFact fact, out IDomainFactStage stage, out string failure)
        {
            ThrowIfDisposed();
            stage = null;
            if (!subscriptions.TryGetValue(typeof(TFact), out var value)
                || value is not Subscription<TFact> subscription
                || subscription.Observer is not IDurableDomainFactStagingObserver<TFact> observer)
            { failure = "domain-fact-staging-observer-missing:" + typeof(TFact).FullName; return false; }
            if (nextLease == long.MaxValue)
            { failure = "domain-fact-lease-sequence-exhausted"; return false; }
            if (!observer.TryStage(fact, out object state, out var prepared, out failure)) return false;
            if (state == null || prepared?.CommitGroup == null
                || prepared is not IDomainFactPreparedIdentity identity
                || string.IsNullOrEmpty(identity.OperationKey) || identity.OwnerRevision < 0L)
            {
                prepared?.Cancel();
                if (state != null) subscription.Cancel(state);
                throw new InvalidOperationException("A staging observer returned no exact publication identity.");
            }
            long id = checked(++nextLease);
            reservations.Add(id, new Reservation(subscription, state));
            stage = new Stage(this, new DomainFactLease(this, id), prepared, identity);
            return true;
        }

        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results, out string failure)
        {
            ThrowIfDisposed();
            if (facts == null || results == null || facts.Count == 0 || facts.Count != results.Length)
                throw new ArgumentException("Invalid domain fact cohort shape.");
            var leaseIds = new HashSet<long>();
            for (int i = 0; i < facts.Count; i++)
            {
                if (facts[i] == null || !IsActive(facts[i].Lease) || !leaseIds.Add(facts[i].Lease.Id))
                    return RejectCohort(facts, results, Array.Empty<IDomainFactPreparedPublication>(),
                        "domain-fact-cohort-invalid-or-duplicate-lease", out failure);
            }
            var prepared = new List<IDomainFactPreparedPublication>(facts.Count);
            try
            {
                foreach (var fact in facts)
                {
                    if (!fact.Prepare(this, out var publication, out failure))
                        return RejectCohort(facts, results, prepared, failure, out failure);
                    if (publication == null || publication.CommitGroup == null)
                        throw new InvalidOperationException("Observer returned no commit authority.");
                    prepared.Add(publication);
                }
                if (prepared.Any(value => !ReferenceEquals(value.CommitGroup, prepared[0].CommitGroup)))
                    return RejectCohort(facts, results, prepared,
                        "domain-fact-cohort-different-commit-authorities", out failure);
            }
            catch
            {
                RejectCohort(facts, results, prepared, "domain-fact-prepare-exception", out _);
                throw;
            }

            // A used lease cannot be published again. The saved domain fact is
            // the retry authority, not a process-local subscription or token.
            foreach (var fact in facts) reservations.Remove(fact.Lease.Id);
            return CommitPrepared(prepared, results, out failure);
        }

        private static bool CommitPrepared(IReadOnlyList<IDomainFactPreparedPublication> prepared,
            DomainFactDelivery[] results, out string failure)
        {
            try
            {
                bool accepted = prepared[0].CommitGroup.Commit(prepared, results, out failure);
                if (accepted ? results.Any(value => !value.MayBeCommitted)
                    : results.Any(value => value.MayBeCommitted))
                    throw new InvalidOperationException("Commit group violated atomic delivery contract.");
                return accepted;
            }
            catch (Exception exception)
            {
                // Once Commit was invoked, cancelling or reporting rejection
                // could roll back a committed domain action. Expose ambiguity.
                failure = "domain-fact-commit-in-doubt:" + exception.GetType().Name;
                for (int i = 0; i < results.Length; i++) results[i] = new DomainFactDelivery(
                    DomainFactDeliveryPhase.InDoubt,
                    (prepared[i] as IDomainFactPreparedIdentity)?.OperationKey,
                    null, null, failure);
                return true;
            }
        }

        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results, out string failure)
        {
            ThrowIfDisposed();
            if (facts == null || results == null || facts.Count == 0 || facts.Count != results.Length)
                throw new ArgumentException("Invalid domain fact recovery cohort shape.");
            var publications = new List<IDomainFactPreparedPublication>(facts.Count);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fact in facts)
                if (fact == null || string.IsNullOrWhiteSpace(fact.PublicationKey) || !keys.Add(fact.PublicationKey))
                    return RejectReplay("domain-fact-replay-key-empty-or-duplicate", publications, results, out failure);
            try
            {
                foreach (var fact in facts)
                {
                    if (!fact.Prepare(this, out var publication, out failure))
                        return RejectReplay(failure, publications, results, out failure);
                    if (publication == null || publication.CommitGroup == null
                        || publication is not IDomainFactPreparedIdentity identity
                        || identity.OperationKey != fact.PublicationKey)
                    {
                        publication?.Cancel();
                        return RejectReplay("domain-fact-replay-identity-mismatch", publications, results, out failure);
                    }
                    publications.Add(publication);
                }
                if (publications.Any(value => !ReferenceEquals(value.CommitGroup, publications[0].CommitGroup)))
                    return RejectReplay("domain-fact-replay-different-commit-authorities", publications, results, out failure);
            }
            catch
            {
                RejectReplay("domain-fact-replay-prepare-exception", publications, results, out _);
                throw;
            }
            return CommitPrepared(publications, results, out failure);
        }

        private static bool RejectReplay(string reason, IReadOnlyList<IDomainFactPreparedPublication> publications,
            DomainFactDelivery[] results, out string failure)
        {
            for (int i = publications.Count - 1; i >= 0; i--) publications[i].Cancel();
            for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Reject(reason);
            failure = reason;
            return false;
        }

        internal bool TryPrepareReplay<TFact>(string key, in TFact fact,
            out IDomainFactPreparedPublication publication, out string failure)
        {
            publication = null;
            if (!subscriptions.TryGetValue(typeof(TFact), out var value)
                || value is not Subscription<TFact> subscription
                || subscription.Observer is not IDurableDomainFactRecoveryObserver<TFact> recovery)
            { failure = "domain-fact-recovery-observer-missing:" + typeof(TFact).FullName; return false; }
            return recovery.TryPrepareReplay(key, fact, out publication, out failure);
        }

        public DomainFactDelivery Replay<TFact>(in TFact fact)
        {
            ThrowIfDisposed();
            if (!subscriptions.TryGetValue(typeof(TFact), out var subscriber))
                return DomainFactDelivery.Reject("domain-fact-observer-missing:" + typeof(TFact).FullName);
            return ((Subscription<TFact>)subscriber).Observer.Replay(fact);
        }

        public void Cancel(in DomainFactLease lease)
        {
            if (!ReferenceEquals(lease.Dispatcher, this) || !reservations.TryGetValue(lease.Id, out var value)) return;
            reservations.Remove(lease.Id);
            value.Subscriber.Cancel(value.State);
        }

        internal bool IsActive(in DomainFactLease lease) => !disposed
            && ReferenceEquals(lease.Dispatcher, this) && reservations.ContainsKey(lease.Id);

        internal string ReservedPublicationKey(in DomainFactLease lease) => IsActive(lease)
            ? (reservations[lease.Id].State as IDomainFactReservedIdentity)?.PublicationKey ?? string.Empty
            : string.Empty;

        internal bool TryPrepare<TFact>(in DomainFactLease lease, in TFact actual,
            out IDomainFactPreparedPublication prepared, out string failure)
        {
            prepared = null;
            if (!IsActive(lease) || reservations[lease.Id].Subscriber is not Subscription<TFact> subscriber)
            { failure = "domain-fact-lease-type-mismatch"; return false; }
            return subscriber.Observer.TryPrepare(actual, reservations[lease.Id].State, out prepared, out failure);
        }

        private bool RejectCohort(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results,
            IReadOnlyList<IDomainFactPreparedPublication> prepared, string reason, out string failure)
        {
            for (int i = prepared.Count - 1; i >= 0; i--) prepared[i].Cancel();
            foreach (var fact in facts) if (fact != null) Cancel(fact.Lease);
            for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Reject(reason);
            failure = reason;
            return false;
        }

        private void Disconnect(ISubscription subscription)
        {
            foreach (long id in reservations.Where(value => ReferenceEquals(value.Value.Subscriber, subscription))
                .Select(value => value.Key).ToArray()) Cancel(new DomainFactLease(this, id));
            subscriptions.Remove(subscription.FactType);
        }

        public void Dispose()
        {
            if (disposed) return;
            foreach (var connection in bindingConnections) connection.Dispose();
            bindingConnections.Clear();
            deferredBindings.Clear();
            foreach (var subscriber in subscriptions.Values.ToArray()) subscriber.Dispose();
            disposed = true;
        }

        private void ThrowIfDisposed()
        { if (disposed) throw new ObjectDisposedException(nameof(DomainFactDispatcher)); }

        private interface ISubscription : IDisposable
        {
            string ObserverId { get; }
            Type FactType { get; }
            void Cancel(object state);
        }
        private sealed class Stage : IDomainFactStage
        {
            private readonly DomainFactDispatcher owner;
            private readonly DomainFactLease lease;
            private readonly IDomainFactPreparedPublication prepared;
            public Stage(DomainFactDispatcher owner, DomainFactLease lease,
                IDomainFactPreparedPublication prepared, IDomainFactPreparedIdentity identity)
            {
                this.owner = owner; this.lease = lease; this.prepared = prepared;
                OperationKey = identity.OperationKey; OwnerRevision = identity.OwnerRevision; IsReplay = identity.IsReplay;
            }
            public string OperationKey { get; }
            public long OwnerRevision { get; }
            public bool IsReplay { get; }
            public bool IsActive => owner.IsActive(lease);
            public DomainFactDelivery Commit()
            {
                if (!IsActive) return DomainFactDelivery.Reject("domain-fact-stage-no-longer-active");
                owner.reservations.Remove(lease.Id);
                var result = new DomainFactDelivery[1];
                CommitPrepared(new[] { prepared }, result, out _);
                return result[0];
            }
            public void Cancel() => owner.Cancel(lease);
        }
        private sealed class Subscription<TFact> : ISubscription
        {
            private DomainFactDispatcher owner;
            public Subscription(DomainFactDispatcher owner, string id, IDurableDomainFactObserver<TFact> observer)
            { this.owner = owner; ObserverId = id; Observer = observer; }
            public string ObserverId { get; }
            public Type FactType => typeof(TFact);
            public IDurableDomainFactObserver<TFact> Observer { get; }
            public void Cancel(object state) => Observer.Cancel(state);
            public void Dispose()
            {
                if (owner == null) return;
                owner.Disconnect(this);
                owner = null;
            }
        }
        private sealed class Reservation
        {
            public Reservation(ISubscription subscriber, object state) { Subscriber = subscriber; State = state; }
            public ISubscription Subscriber { get; }
            public object State { get; }
        }
    }
}
