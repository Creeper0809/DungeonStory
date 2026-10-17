#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;

internal sealed class RegularCustomerFactEditorFixture : IDisposable
{
    private readonly IDisposable subscription;

    internal RegularCustomerFactEditorFixture(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe = null)
        : this(
            outcomes?.Transaction
                ?? throw new ArgumentNullException(nameof(outcomes)),
            new GameplayDomainFactCommitGroup(
                outcomes.Recorder,
                outcomes.Ledger),
            probe)
    {
    }

    internal RegularCustomerFactEditorFixture(
        MigratedProducerOutcomeRuntime runtime,
        GameplayDomainFactCommitGroup commits,
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        commits = commits ?? throw new ArgumentNullException(nameof(commits));
        probe ??= new MigratedProducerOutcomeProbeTransaction(runtime);
        Dispatcher = new DomainFactDispatcher();
        try
        {
            subscription = Dispatcher.Subscribe(
                RegularCustomerFactMapping.ObserverId,
                new MigratedProducerDomainFactProbeObserver<RegularCustomerFact>(
                    RegularCustomerFactMapping.Capture,
                    runtime,
                    commits,
                    probe));
            var transport = new FaultingTransport(this);
            Publisher = transport;
            Recovery = transport;
        }
        catch
        {
            Dispatcher.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher Dispatcher { get; }
    internal IDomainFactPublisher Publisher { get; }
    internal IDomainFactRecoveryPublisher Recovery { get; }
    internal bool HideNextAcknowledgement { get; set; }
    internal RegularCustomerFactStage? HideAcknowledgementStage { get; set; }
    internal bool BlockRecovery { get; set; }
    internal int HiddenAcknowledgements { get; private set; }
    internal Action BeforePublish { get; set; }
    internal Action BeforeReplay { get; set; }

    private sealed class FaultingTransport :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly RegularCustomerFactEditorFixture owner;

        internal FaultingTransport(RegularCustomerFactEditorFixture owner) =>
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
            owner.BeforePublish?.Invoke();
            DomainFactDelivery result = owner.Dispatcher.Publish(lease, fact);
            if (!owner.HideNextAcknowledgement
                || !result.MayBeCommitted
                || !MatchesHiddenStage(fact))
            {
                return result;
            }

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

            owner.BeforeReplay?.Invoke();
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

        private bool MatchesHiddenStage<TFact>(in TFact fact)
        {
            if (!owner.HideAcknowledgementStage.HasValue)
            {
                return true;
            }
            return fact is RegularCustomerFact customer
                && customer.Snapshot().stage
                    == owner.HideAcknowledgementStage.Value;
        }
    }

    public void Dispose()
    {
        subscription?.Dispose();
        Dispatcher.Dispose();
    }
}
#endif
