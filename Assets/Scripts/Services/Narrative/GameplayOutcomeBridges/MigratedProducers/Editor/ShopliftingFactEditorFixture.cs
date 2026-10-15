#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

internal sealed class ShopliftingFactEditorFixture : IDisposable
{
    private readonly IDisposable subscription;

    internal ShopliftingFactEditorFixture(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe)
        : this(
            outcomes?.Transaction
                ?? throw new ArgumentNullException(nameof(outcomes)),
            new GameplayDomainFactCommitGroup(
                outcomes.Recorder,
                outcomes.Ledger),
            probe)
    {
    }

    internal ShopliftingFactEditorFixture(
        MigratedProducerOutcomeRuntime runtime,
        GameplayDomainFactCommitGroup commits,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        Dispatcher = new DomainFactDispatcher();
        try
        {
            subscription = Dispatcher.Subscribe(
                "fixture:shoplifting.fact",
                new MigratedProducerDomainFactProbeObserver<ShopliftingFact>(
                    ShopliftingFactMapping.Capture,
                    runtime ?? throw new ArgumentNullException(nameof(runtime)),
                    commits ?? throw new ArgumentNullException(nameof(commits)),
                    probe ?? throw new ArgumentNullException(nameof(probe))));
            var transport = new FaultingTransport(this);
            Journal = new ShopliftingFactJournal(transport, transport);
        }
        catch
        {
            Dispatcher.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher Dispatcher { get; }
    internal ShopliftingFactJournal Journal { get; }
    internal bool HideNextAcknowledgement { get; set; }
    internal bool BlockRecovery { get; set; }
    internal int HiddenAcknowledgements { get; private set; }

    private sealed class FaultingTransport :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly ShopliftingFactEditorFixture owner;

        internal FaultingTransport(ShopliftingFactEditorFixture owner) =>
            this.owner = owner;

        public bool TryReserve<TFact>(
            in TFact fact,
            out DomainFactLease lease,
            out string failure) =>
            owner.Dispatcher.TryReserve(fact, out lease, out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact fact)
        {
            DomainFactDelivery result = owner.Dispatcher.Publish(lease, fact);
            if (!owner.HideNextAcknowledgement || !result.MayBeCommitted)
                return result;
            owner.HideNextAcknowledgement = false;
            owner.HiddenAcknowledgements++;
            return DomainFactDelivery.Uncertain(
                "fixture-native-acknowledgement-lost");
        }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            if (owner.BlockRecovery)
            {
                failure = "fixture-recovery-unavailable";
                return false;
            }
            return owner.Dispatcher.ReplayBatch(facts, results, out failure);
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) =>
            owner.Dispatcher.PublishBatch(facts, results, out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            owner.Dispatcher.Replay(fact);

        public void Cancel(in DomainFactLease lease) =>
            owner.Dispatcher.Cancel(lease);
    }

    public void Dispose()
    {
        subscription?.Dispose();
        Dispatcher.Dispose();
    }
}
#endif
