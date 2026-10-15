using System;
using System.Collections.Generic;

namespace DungeonStory.Foundation.Facts
{
    // Domain publication contracts deliberately reference no narrative, ledger,
    // Unity scene, receipt adapter, score or display implementation.
    public enum DomainFactDeliveryPhase
    {
        Rejected,
        AcceptedPendingDelivery,
        Delivered,
        InDoubt
    }

    public readonly struct DomainFactDelivery
    {
        public DomainFactDelivery(DomainFactDeliveryPhase phase, string operationKey,
            string publicationId, string fingerprint, string detail)
        {
            Phase = phase;
            OperationKey = operationKey ?? string.Empty;
            PublicationId = publicationId ?? string.Empty;
            Fingerprint = fingerprint ?? string.Empty;
            Detail = detail ?? string.Empty;
        }
        public DomainFactDeliveryPhase Phase { get; }
        public string OperationKey { get; }
        public string PublicationId { get; }
        public string Fingerprint { get; }
        public string Detail { get; }
        public bool MayBeCommitted => Phase != DomainFactDeliveryPhase.Rejected;
        public bool Completed => Phase == DomainFactDeliveryPhase.Delivered;
        public static DomainFactDelivery Reject(string detail) => new(
            DomainFactDeliveryPhase.Rejected, null, null, null, detail);
        public static DomainFactDelivery Uncertain(string detail) => new(
            DomainFactDeliveryPhase.InDoubt, null, null, null, detail);
    }

    /// <summary>Transient, dispatcher-owned reservation. Never serialize this handle.</summary>
    public readonly struct DomainFactLease
    {
        internal DomainFactLease(DomainFactDispatcher dispatcher, long id)
        { Dispatcher = dispatcher; Id = id; }
        internal DomainFactDispatcher Dispatcher { get; }
        internal long Id { get; }
        public bool IsValid => Dispatcher != null && Dispatcher.IsActive(this);
        // Opaque observer identity: save alongside the exact domain fact before
        // publication, never parse it or use it as domain state authority.
        public string PublicationKey => Dispatcher?.ReservedPublicationKey(this) ?? string.Empty;
    }

    public interface IDomainFactReservedIdentity
    {
        string PublicationKey { get; }
    }

    public interface IDomainFactRecoveryPublisher
    {
        bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results, out string failure);
    }

    /// <summary>Queries an opaque durable publication, never the producing domain's effects.</summary>
    public interface IDomainFactDeliveryQuery
    {
        DomainFactDelivery Reconcile(string publicationKey);
    }

    public interface IDurableDomainFactRecoveryObserver<TFact>
    {
        bool TryPrepareReplay(string publicationKey, in TFact fact,
            out IDomainFactPreparedPublication prepared, out string failure);
    }

    public abstract class DomainFactReplay
    {
        protected DomainFactReplay(string publicationKey) => PublicationKey = publicationKey;
        public string PublicationKey { get; }
        internal abstract bool Prepare(DomainFactDispatcher dispatcher,
            out IDomainFactPreparedPublication prepared, out string failure);
        public static DomainFactReplay Create<TFact>(string publicationKey, in TFact fact) =>
            new ReplayRequest<TFact>(publicationKey, fact);
        private sealed class ReplayRequest<TFact> : DomainFactReplay
        {
            private readonly TFact fact;
            public ReplayRequest(string key, TFact fact) : base(key) => this.fact = fact;
            internal override bool Prepare(DomainFactDispatcher dispatcher,
                out IDomainFactPreparedPublication prepared, out string failure) =>
                dispatcher.TryPrepareReplay(PublicationKey, fact, out prepared, out failure);
        }
    }

    public interface IDomainFactPublisher
    {
        bool TryReserve<TFact>(in TFact proposed, out DomainFactLease lease, out string failure);
        // Escaped nonfatal exceptions are pre-commit failures. Implementations
        // must return InDoubt (never throw/reject) once a commit may have begun.
        DomainFactDelivery Publish<TFact>(in DomainFactLease lease, in TFact actual);
        bool PublishBatch(IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results, out string failure);
        DomainFactDelivery Replay<TFact>(in TFact fact);
        void Cancel(in DomainFactLease lease);
    }

    /// <summary>Exact facts enlisted before an independently reversible domain mutation.</summary>
    public interface IDomainFactStagingPublisher
    {
        bool TryStage<TFact>(in TFact fact, out IDomainFactStage stage, out string failure);
    }

    public interface IDomainFactStage
    {
        string OperationKey { get; }
        long OwnerRevision { get; }
        bool IsReplay { get; }
        bool IsActive { get; }
        DomainFactDelivery Commit();
        void Cancel();
    }

    public interface IDurableDomainFactStagingObserver<TFact>
    {
        bool TryStage(in TFact fact, out object reservation,
            out IDomainFactPreparedPublication prepared, out string failure);
    }

    public interface IDomainFactPreparedIdentity
    {
        string OperationKey { get; }
        long OwnerRevision { get; }
        bool IsReplay { get; }
    }

    /// <summary>
    /// A required observer must reserve before domain mutation. Its prepare is
    /// cancellable; only the shared commit group may cross the durable boundary.
    /// Publication failures after that boundary must never claim Rejected.
    /// </summary>
    public interface IDurableDomainFactObserver<TFact>
    {
        bool TryReserve(in TFact proposed, out object reservation, out string failure);
        bool TryPrepare(in TFact actual, object reservation,
            out IDomainFactPreparedPublication prepared, out string failure);
        DomainFactDelivery Replay(in TFact fact);
        void Cancel(object reservation);
    }

    public interface IDomainFactPreparedPublication
    {
        IDomainFactCommitGroup CommitGroup { get; }
        void Cancel();
    }

    /// <summary>
    /// One atomic storage authority for a cohort of independently observed facts.
    /// False means none committed. An ambiguous commit returns InDoubt, not false.
    /// </summary>
    public interface IDomainFactCommitGroup
    {
        bool Commit(IReadOnlyList<IDomainFactPreparedPublication> publications,
            DomainFactDelivery[] results, out string failure);
    }

    public interface IDomainFactObserverBinding
    {
        IReadOnlyCollection<Type> FactTypes { get; }
        IDisposable Connect(DomainFactDispatcher dispatcher);
    }

    public abstract class DomainFactEmission
    {
        protected DomainFactEmission(DomainFactLease lease) => Lease = lease;
        public DomainFactLease Lease { get; }
        internal abstract bool Prepare(DomainFactDispatcher dispatcher,
            out IDomainFactPreparedPublication prepared, out string failure);
        public static DomainFactEmission Create<TFact>(in DomainFactLease lease, in TFact fact) =>
            new Emission<TFact>(lease, fact);

        private sealed class Emission<TFact> : DomainFactEmission
        {
            private readonly TFact fact;
            public Emission(DomainFactLease lease, TFact fact) : base(lease) => this.fact = fact;
            internal override bool Prepare(DomainFactDispatcher dispatcher,
                out IDomainFactPreparedPublication prepared, out string failure) =>
                dispatcher.TryPrepare(Lease, fact, out prepared, out failure);
        }
    }
}
