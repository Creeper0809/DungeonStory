#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class ObservedServiceIncidentFactDebugScenarios
{
    public static void RunBatch()
    {
        Require(Application.isBatchMode, "B114 requires isolated batch execution.");
        VerifyRejection();
        VerifyRecovery(afterCommit: false);
        VerifyRecovery(afterCommit: true);
        Wim040ObservedIncidentDebugScenarios.RunObserverChecks();
        Require(ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios.RunCausalOwnerFamily(true),
            "Observed incident -> resolution causal checkpoint failed.");
        Debug.Log("B114_SERVICE_INCIDENT_OBSERVERS=PASS");
    }

    private static void VerifyRejection()
    {
        using var fixture = new Fixture();
        string before = JsonUtility.ToJson(fixture.Campaign.CaptureSociety());
        var previous = fixture.Campaign.PrepareSociety(fixture.Campaign.CaptureSociety());
        int blocked = 0;
        fixture.Transport.BeforePublish = () =>
        {
            Reject(() => fixture.Campaign.CaptureSociety()); blocked++;
            Reject(() => fixture.Campaign.PublishSociety(previous)); blocked++;
            Reject(() => fixture.Capture()); blocked++;
            Reject(() => fixture.Campaign.CaptureObservedMealIncident(Evidence(), Context())); blocked++;
        };
        fixture.Transport.Reject = true;
        var result = fixture.Campaign.CaptureObservedMealIncident(Evidence(), Context());
        Require(!result.Created && result.Failure.IsFailure && blocked == 4
            && JsonUtility.ToJson(fixture.Campaign.CaptureSociety()) == before
            && fixture.NativeCount == 0 && fixture.Journal.Capture().pending.Count == 0,
            "Definite incident rejection mutated Society or leaked a publication.");
        fixture.Transport.BeforePublish = null;
        fixture.Transport.Reject = false;
        Require(fixture.Campaign.CaptureObservedMealIncident(Evidence(), Context()).Created,
            "Rejected incident could not retry.");
        string completed = JsonUtility.ToJson(fixture.Capture());
        Require(fixture.Campaign.CaptureObservedMealIncident(Evidence(), Context()).Disposition
                == ObservedMealIncidentCaptureDisposition.ExactReplay
            && JsonUtility.ToJson(fixture.Capture()) == completed && fixture.NativeCount == 1,
            "Exact incident replay changed state or duplicated its result.");
        Debug.Log("B114_INCIDENT_REJECTION_GUARD=PASS");
    }

    private static void VerifyRecovery(bool afterCommit)
    {
        Bundle saved;
        using (var source = new Fixture())
        {
            source.Transport.ThrowBeforeCommit = !afterCommit;
            source.Transport.HideAcknowledgement = afterCommit;
            Require(source.Campaign.CaptureObservedMealIncident(Evidence(), Context()).Created,
                "Unknown incident acknowledgement lost its actual Society occurrence.");
            Require(source.Journal.Capture().pending.Count == 1 && source.Facts.Dispatcher.ActiveLeaseCount == 0,
                "Unknown incident acknowledgement did not retain one durable frozen fact.");
            Require(source.Campaign.CaptureObservedMealIncident(Evidence(), Context()).Disposition
                == ObservedMealIncidentCaptureDisposition.ExactReplay,
                "Owner replay repeated an incident with unknown delivery.");
            if (afterCommit)
            {
                Require(source.Campaign.TryResolveObservedMealIncidentTargetDeath(new CharacterId(Character), 42, out _, out _),
                    "Actual terminal owner transition failed.");
                var terminal = source.Campaign.CaptureSociety();
                terminal.recentResolvedEvents.Clear();
                source.Campaign.PublishSociety(source.Campaign.PrepareSociety(terminal));
            }
            saved = source.Capture();
            Validate(saved);
            var invalid = RoundTrip(saved);
            invalid.society.successfulIncidentOperations.Clear();
            Reject(() => Validate(invalid));
            invalid = RoundTrip(saved);
            invalid.journal.pending[0].publicationKey += "-tampered";
            Reject(() => Validate(invalid));
        }
        using (var restored = new Fixture(saved))
        {
            string societyBefore = JsonUtility.ToJson(restored.Campaign.CaptureSociety());
            restored.Journal.OnRestoreCompleted();
            Require(restored.Journal.Capture().pending.Count == 0 && restored.NativeCount == 1
                && JsonUtility.ToJson(restored.Campaign.CaptureSociety()) == societyBefore,
                "Frozen replay changed campaign effects or duplicated the native result.");
            string complete = JsonUtility.ToJson(restored.Capture());
            Require(restored.Journal.RecoverPending(out _)
                && restored.Campaign.CaptureObservedMealIncident(Evidence(), Context()).Disposition
                    == ObservedMealIncidentCaptureDisposition.ExactReplay
                && JsonUtility.ToJson(restored.Capture()) == complete,
                "Completed incident replay changed its saved owner or ledger.");
        }
        Debug.Log(afterCommit ? "B114_INCIDENT_ACK_AFTER=PASS" : "B114_INCIDENT_ACK_BEFORE=PASS");
    }

    private const string Character = "character:b114:customer";
    private static ObservedMealIncidentSnapshot Evidence() => new(
        new ConsumableOperationId("consumable-operation:b114:incident"), new CharacterId(Character),
        new ItemDefinitionId("food:lavish-meat"), new ItemStackId("stack:b114:meal"), default,
        fieldMeal: true, new CoreGridCell(3, 4), 42, observedPolicyViolation: false,
        observedDowned: false, observedDead: false, observedContaminated: true);
    private static V20DailyEventContext Context() => new() { AbsoluteDay = 42, RunSeed = 157181, Season = Season.Spring };

    private static void Validate(Bundle value)
    {
        SocialRitualFactPreflight.ValidateOwners(value.journal, null, value.society, null);
        SocialRitualFactPreflight.ValidateJournal(value.journal, value.ledger, value.counters);
    }

    [Serializable]
    private sealed class Bundle
    {
        public SocietyEventWorldSaveData society;
        public SocialRitualFactSaveData journal;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonMigratedProducerOutcomeSaveData counters;
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly MigratedProducerOutcomeEditorFixture Outcomes = new("run:b114:service-incidents");
        internal readonly SocialRitualFactEditorFixture Facts;
        internal readonly Transport Transport;
        internal readonly SocialRitualFactJournal Journal;
        internal readonly V20CampaignRuntime Campaign;
        internal int NativeCount => Outcomes.Ledger.CaptureGameplayOutcomes().outbox.Count
            + Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count;
        internal Fixture(Bundle saved = null)
        {
            Facts = new SocialRitualFactEditorFixture(Outcomes);
            Transport = new Transport(Facts.Dispatcher);
            Journal = new SocialRitualFactJournal(Transport, Transport);
            Campaign = new V20CampaignRuntime(Outcomes.AggregateRootStore,
                new V20StoryContentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader())), null, null, Journal);
            if (saved == null) return;
            Validate(saved);
            var candidate = Campaign.PrepareSociety(saved.society);
            var native = Outcomes.Ledger.PrepareGameplayOutcomeRestore(saved.ledger);
            Outcomes.Ledger.BeginRestoreCandidate(); Outcomes.Ledger.PublishGameplayOutcomeRestore(native);
            Outcomes.Ledger.PublishRestoreCandidate(); Outcomes.Ledger.CompleteRestoreCandidate();
            Outcomes.Transaction.Restore(Outcomes.Transaction.PrepareRestore(saved.counters));
            Campaign.PublishSociety(candidate);
            Journal.Restore(saved.journal);
        }
        internal Bundle Capture() => RoundTrip(new Bundle
        {
            society = Campaign.CaptureSociety(), journal = Journal.Capture(),
            ledger = Outcomes.Ledger.CaptureGameplayOutcomes(), counters = Outcomes.Transaction.Capture()
        });
        public void Dispose() { Journal.AssertIdle(); Facts.Dispose(); }
    }

    private sealed class Transport : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher dispatcher;
        internal bool Reject, ThrowBeforeCommit, HideAcknowledgement;
        internal Action BeforePublish;
        internal Transport(DomainFactDispatcher dispatcher) => this.dispatcher = dispatcher;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) => dispatcher.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact) => dispatcher.Publish(lease, fact);
        public void Cancel(in DomainFactLease lease) => dispatcher.Cancel(lease);
        public DomainFactDelivery Replay<T>(in T fact) => dispatcher.Replay(fact);
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure)
        {
            BeforePublish?.Invoke();
            if (Reject)
            {
                failure = "B114-controlled-rejection";
                for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Reject(failure);
                return false;
            }
            if (ThrowBeforeCommit) throw new InvalidOperationException("B114-controlled-unknown-before");
            bool accepted = dispatcher.PublishBatch(facts, results, out failure);
            if (HideAcknowledgement && accepted)
                for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Uncertain("B114-controlled-unknown-after");
            return accepted;
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure) =>
            dispatcher.ReplayBatch(facts, results, out failure);
    }

    private static T RoundTrip<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("B114 expected an explicit boundary rejection.");
    }
    private static void Require(bool value, string failure)
    { if (!value) throw new InvalidOperationException(failure); }
}
#endif
