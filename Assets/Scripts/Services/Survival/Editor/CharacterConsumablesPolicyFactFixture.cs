#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

internal sealed class CharacterConsumablesPolicyFactFixture : IDisposable
{
    private readonly IDisposable subscription;

    internal CharacterConsumablesPolicyFactFixture(string runId)
    {
        Registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new PolicyValueChangeOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new PolicyValueChangeOutcomeAdapter()
            });
        Ledger = new GameplayOutcomeLedger(
            Registry,
            new GameplayOutcomeBufferLimits(16, 0, 64),
            new GameplayOutcomeRunId(runId),
            1L);
        Recorder = new GameplayOutcomeRecorder(
            Ledger,
            Registry,
            new GameEventBus());
        Dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
        subscription = Dispatcher.Subscribe(
            "test:survival:character-consumables-policy-change",
            new GameplayDomainFactObserver<
                CharacterConsumablesPolicyChangedFact,
                PolicyValueChangeReceipt>(
                CharacterConsumablesPolicyFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                commits));
        Publisher = new FaultingPolicyFactPublisher(Dispatcher);
    }

    internal GameplayOutcomeRegistry Registry { get; }
    internal GameplayOutcomeLedger Ledger { get; }
    internal GameplayOutcomeRecorder Recorder { get; }
    internal DomainFactDispatcher Dispatcher { get; }
    internal FaultingPolicyFactPublisher Publisher { get; }

    public void Dispose()
    {
        subscription.Dispose();
        Dispatcher.Dispose();
    }
}

internal sealed class FaultingPolicyFactPublisher : IDomainFactPublisher
{
    private readonly IDomainFactPublisher inner;
    private bool rejectNextPublish;
    private bool hideNextAcknowledgement;
    private bool rejectNextReplay;

    internal FaultingPolicyFactPublisher(IDomainFactPublisher inner) =>
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

    internal void RejectNextPublish() => rejectNextPublish = true;
    internal void HideNextAcknowledgement() => hideNextAcknowledgement = true;
    internal void RejectNextReplay() => rejectNextReplay = true;

    public bool TryReserve<TFact>(in TFact proposed, out DomainFactLease lease,
        out string failure) => inner.TryReserve(proposed, out lease, out failure);

    public DomainFactDelivery Publish<TFact>(
        in DomainFactLease lease,
        in TFact actual)
    {
        if (rejectNextPublish)
        {
            rejectNextPublish = false;
            inner.Cancel(lease);
            return DomainFactDelivery.Reject(
                "editor-injected-policy-outcome-commit-failure");
        }

        DomainFactDelivery delivered = inner.Publish(lease, actual);
        if (!hideNextAcknowledgement || !delivered.MayBeCommitted)
            return delivered;

        hideNextAcknowledgement = false;
        return new DomainFactDelivery(
            DomainFactDeliveryPhase.InDoubt,
            delivered.OperationKey,
            delivered.PublicationId,
            delivered.Fingerprint,
            "editor-injected-policy-outcome-commit-in-doubt");
    }

    public bool PublishBatch(
        IReadOnlyList<DomainFactEmission> facts,
        DomainFactDelivery[] results,
        out string failure) => inner.PublishBatch(facts, results, out failure);

    public DomainFactDelivery Replay<TFact>(in TFact fact)
    {
        if (rejectNextReplay)
        {
            rejectNextReplay = false;
            return DomainFactDelivery.Reject("editor-injected-policy-replay-rejected");
        }
        return inner.Replay(fact);
    }

    public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
}
#endif
