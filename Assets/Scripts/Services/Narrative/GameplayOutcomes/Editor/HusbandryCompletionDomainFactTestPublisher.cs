#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

// A test-only routing boundary: all business facts use the production dispatcher;
// only completion delivery is observed through the explicitly held real command.
public class WorkCompletionDomainFactTestPublisher<TCompletion> :
    IDomainFactPublisher, IDomainFactRecoveryPublisher, IDisposable
{
    private readonly IDomainFactPublisher production;
    private readonly DomainFactDispatcher completion = new();

    public WorkCompletionDomainFactTestPublisher(IDomainFactPublisher production,
        IWorkCompletionIdentityDeliveryCommand held, CharacterIdentityEventPublisher events,
        WorkCompletionIdentityDeliveryLedger deliveries, Func<TCompletion, WorkCompletionIdentityDeliveryRequest> capture,
        WorkCompletionNotificationMode notificationMode,
        WorkIdentityAppliedFactMapping childValidation)
    {
        this.production = production ?? throw new ArgumentNullException(nameof(production));
        completion.Subscribe("fixture:work-completion", new WorkCompletionDomainFactObserver<TCompletion>(
            capture, held, deliveries, events, notificationMode, childValidation));
    }

    private IDomainFactPublisher For<TFact>() =>
        typeof(TFact) == typeof(TCompletion) ? completion : production;
    private static bool IsCompletion(string key) => key?.StartsWith(
        WorkCompletionIdentityOutcomeIds.ProducerId + "|", StringComparison.Ordinal) == true;

    public bool TryReserve<TFact>(in TFact proposed, out DomainFactLease lease, out string failure) =>
        For<TFact>().TryReserve(proposed, out lease, out failure);
    public DomainFactDelivery Publish<TFact>(in DomainFactLease lease, in TFact actual) =>
        For<TFact>().Publish(lease, actual);
    public DomainFactDelivery Replay<TFact>(in TFact fact) => For<TFact>().Replay(fact);
    public void Cancel(in DomainFactLease lease)
    {
        if (IsCompletion(lease.PublicationKey)) completion.Cancel(lease);
        else production.Cancel(lease);
    }

    public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure)
    {
        int count = facts?.Count(value => IsCompletion(value.Lease.PublicationKey)) ?? 0;
        if (count > 0 && count != facts.Count)
        { failure = "test-routing-mixed-completion-cohort-unsupported"; return false; }
        return (count > 0 ? completion : production).PublishBatch(facts, results, out failure);
    }

    public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure)
    {
        int count = facts?.Count(value => IsCompletion(value.PublicationKey)) ?? 0;
        if (count > 0 && count != facts.Count)
        { failure = "test-routing-mixed-completion-replay-unsupported"; return false; }
        if (count > 0) return completion.ReplayBatch(facts, results, out failure);
        if (production is not IDomainFactRecoveryPublisher recovery)
        { failure = "test-routing-production-recovery-unavailable"; return false; }
        return recovery.ReplayBatch(facts, results, out failure);
    }

    public void Dispose() => completion.Dispose();
}

public sealed class HusbandryCompletionDomainFactTestPublisher : WorkCompletionDomainFactTestPublisher<AnimalHusbandryCompletionFact>
{
    public HusbandryCompletionDomainFactTestPublisher(IDomainFactPublisher production,
        IWorkCompletionIdentityDeliveryCommand held, CharacterIdentityEventPublisher events,
        WorkCompletionIdentityDeliveryLedger deliveries,
        WorkIdentityAppliedFactMapping childValidation)
        : base(production, held, events, deliveries, AnimalHusbandryCompletionObservation.CreateRequest,
            WorkCompletionNotificationMode.ReferencedEvent, childValidation) { }
}
#endif
