using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;
using VContainer;

public enum WorkCompletionNotificationMode
{
    ApplyOnly,
    ReferencedEvent
}

// Observes completed labor after its executor has recorded the accepted delta.
// The existing identity owner remains the only authority for effects and its
// delivery cursor; gameplay owners neither build receipts nor send identity events.
public sealed class WorkCompletionDomainFactObserver<TFact> :
    IDurableDomainFactObserver<TFact>, IDurableDomainFactRecoveryObserver<TFact>,
    IDomainFactCommitGroup
{
    private readonly Func<TFact, WorkCompletionIdentityDeliveryRequest> capture;
    private readonly IWorkCompletionIdentityDeliveryCommand completion;
    private readonly WorkCompletionIdentityDeliveryLedger deliveries;
    private readonly CharacterIdentityEventPublisher events;
    private readonly WorkCompletionNotificationMode notificationMode;
    private readonly WorkIdentityAppliedFactMapping childValidation;

    public WorkCompletionDomainFactObserver(
        Func<TFact, WorkCompletionIdentityDeliveryRequest> capture,
        IWorkCompletionIdentityDeliveryCommand completion,
        WorkCompletionIdentityDeliveryLedger deliveries,
        CharacterIdentityEventPublisher events, WorkCompletionNotificationMode notificationMode,
        WorkIdentityAppliedFactMapping childValidation)
    {
        this.capture = capture ?? throw new ArgumentNullException(nameof(capture));
        this.completion = completion ?? throw new ArgumentNullException(nameof(completion));
        this.deliveries = deliveries ?? throw new ArgumentNullException(nameof(deliveries));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        if (!Enum.IsDefined(typeof(WorkCompletionNotificationMode), notificationMode))
            throw new ArgumentOutOfRangeException(nameof(notificationMode));
        this.notificationMode = notificationMode;
        this.childValidation = childValidation ?? throw new ArgumentNullException(nameof(childValidation));
    }

    public bool TryReserve(in TFact proposed, out object reservation, out string failure)
    {
        var request = Capture(proposed);
        reservation = new Reservation(request);
        failure = string.Empty;
        return true;
    }

    public bool TryPrepare(in TFact actual, object reservation,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        if (reservation is not Reservation state || state.Cancelled || state.Prepared != null)
        { failure = "work-completion-fact-reservation-invalid"; return false; }
        var request = Capture(actual);
        if (state.PublicationKey != Key(request)
            || state.Request.PayloadFingerprint != request.PayloadFingerprint)
        { failure = "work-completion-fact-reserved-payload-changed"; return false; }
        state.Prepared = new Publication(this, request);
        prepared = state.Prepared;
        failure = string.Empty;
        return true;
    }

    public bool TryPrepareReplay(string publicationKey, in TFact fact,
        out IDomainFactPreparedPublication prepared, out string failure)
    {
        prepared = null;
        var request = Capture(fact);
        if (publicationKey != Key(request))
        { failure = "work-completion-fact-recovery-key-mismatch"; return false; }
        prepared = new Publication(this, request);
        failure = string.Empty;
        return true;
    }

    public DomainFactDelivery Replay(in TFact fact) =>
        DomainFactDelivery.Reject("work-completion-fact-replay-requires-saved-key");

    public void Cancel(object reservation)
    {
        if (reservation is not Reservation state) return;
        state.Cancelled = true;
        state.Prepared?.Cancel();
    }

    public bool Commit(IReadOnlyList<IDomainFactPreparedPublication> publications,
        DomainFactDelivery[] results, out string failure)
    {
        // Identity effects cannot be atomically applied to an arbitrary multi-
        // character cohort by this owner. Reject it before applying any member.
        if (publications == null || publications.Count != 1 || results?.Length != 1
            || publications[0] is not Publication value || value.CommitGroup != this || value.Terminal)
        { failure = "work-completion-fact-requires-one-active-publication"; return false; }
        value.Terminal = true;
        string key = Key(value.Request);
        try
        {
            if (!childValidation.TryValidateCommittedChild(value.Request, out string childFailure))
            {
                results[0] = Unconfirmed(key, childFailure);
                failure = string.Empty;
                return true;
            }
            WorkCompletionIdentityDeliveryResult result = completion.EnsureApplied(value.Request);
            if (!result.IsApplied)
            {
                // Deferred may mean no effects yet or an unknown observation ACK.
                // Without proof neither is an acknowledged durable publication.
                results[0] = Unconfirmed(key, "work-completion-fact:" + result.Status + ":" + result.FailureReason);
                failure = string.Empty;
                return true;
            }
            if (notificationMode == WorkCompletionNotificationMode.ReferencedEvent)
            {
                if (!deliveries.TryMarkNotificationAttempted(value.Request, out bool alreadyAttempted))
                {
                    results[0] = Unconfirmed(key, "work-completion-fact-delivery-cursor-not-finalized");
                    failure = string.Empty;
                    return true;
                }
                // This notification is also consumed by the identity adapter.
                // Capture verifies that it resolves to this very same delivery.
                // Mark before callbacks to preserve at-most-once attempts on replay.
                if (!alreadyAttempted) events.Publish(value.Request.ToEvent());
            }
            results[0] = new DomainFactDelivery(DomainFactDeliveryPhase.Delivered,
                key, value.Request.DeliveryId, value.Request.PayloadFingerprint, string.Empty);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            results[0] = Unconfirmed(key, "work-completion-fact-unconfirmed:" + exception.Message);
        }
        failure = string.Empty;
        return true;
    }

    private WorkCompletionIdentityDeliveryRequest Capture(TFact fact)
    {
        var request = capture(fact);
        if (notificationMode == WorkCompletionNotificationMode.ReferencedEvent)
        {
            var notification = WorkCompletionIdentityDeliveryLedger.CreateReferencedRequest(request.ToEvent());
            if (notification.DeliveryId != request.DeliveryId
                || notification.PayloadFingerprint != request.PayloadFingerprint)
                throw new InvalidOperationException("work-completion-notification-would-change-delivery-identity");
        }
        return request;
    }

    private static string Key(WorkCompletionIdentityDeliveryRequest request) =>
        WorkCompletionIdentityOutcomeIds.ResultKey(request).ToString();
    private static DomainFactDelivery Unconfirmed(string key, string detail) =>
        new(DomainFactDeliveryPhase.InDoubt, key, string.Empty, string.Empty, detail);

    private sealed class Reservation : IDomainFactReservedIdentity
    {
        internal Reservation(WorkCompletionIdentityDeliveryRequest request) => Request = request;
        internal WorkCompletionIdentityDeliveryRequest Request { get; }
        internal bool Cancelled;
        internal Publication Prepared;
        public string PublicationKey => Key(Request);
    }

    private sealed class Publication : IDomainFactPreparedPublication, IDomainFactPreparedIdentity
    {
        internal Publication(WorkCompletionDomainFactObserver<TFact> owner,
            WorkCompletionIdentityDeliveryRequest request)
        {
            CommitGroup = owner; Request = request;
            IsReplay = owner.deliveries.Inspect(request, out _) == WorkCompletionIdentityDeliveryStatus.AlreadyApplied;
        }
        public IDomainFactCommitGroup CommitGroup { get; }
        public string OperationKey => Key(Request);
        public long OwnerRevision => WorkCompletionIdentityOutcomeIds.ResultKey(Request).CommitRevision;
        public bool IsReplay { get; }
        internal WorkCompletionIdentityDeliveryRequest Request { get; }
        internal bool Terminal;
        public void Cancel() => Terminal = true;
    }
}

public sealed class WorkCompletionDomainFactBinding<TFact> : IDomainFactObserverBinding
{
    private readonly string id;
    private readonly WorkCompletionDomainFactObserver<TFact> observer;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(TFact) });
    public WorkCompletionDomainFactBinding(string id, Func<TFact, WorkCompletionIdentityDeliveryRequest> capture,
        IWorkCompletionIdentityDeliveryCommand completion, WorkCompletionIdentityDeliveryLedger deliveries,
        CharacterIdentityEventPublisher events, WorkCompletionNotificationMode notificationMode,
        WorkIdentityAppliedFactMapping childValidation)
    { this.id = id; observer = new(capture, completion, deliveries, events, notificationMode, childValidation); }
    public IDisposable Connect(DomainFactDispatcher dispatcher) => dispatcher.Subscribe(id, observer);
}

public static class WorkCompletionDomainFactRegistration
{
    public static void RegisterWorkCompletionDomainFact<TFact>(this IContainerBuilder builder,
        string id, Func<TFact, WorkCompletionIdentityDeliveryRequest> capture,
        WorkCompletionNotificationMode notificationMode)
    {
        // Reaction owners consume other domain services (which themselves publish
        // facts). Build the dispatcher first, then explicitly connect this observer
        // before the container is handed to gameplay. Never resolve a reaction
        // owner while constructing the dispatcher's binding collection.
        builder.Register(resolver => new WorkCompletionDomainFactBinding<TFact>(id, capture,
            resolver.Resolve<IWorkCompletionIdentityDeliveryCommand>(),
            resolver.Resolve<WorkCompletionIdentityDeliveryLedger>(),
            resolver.Resolve<CharacterIdentityEventPublisher>(), notificationMode,
            resolver.Resolve<WorkIdentityAppliedFactMapping>()), Lifetime.Singleton);
        builder.RegisterDeferredDomainFactBinding<TFact>((resolver, dispatcher) =>
            resolver.Resolve<WorkCompletionDomainFactBinding<TFact>>().Connect(dispatcher));
    }
}
