#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class ObservedServiceIncidentFactDebugScenarios
{
    public static string RunSoIncidentVariantsForBatch(IGameContentCatalog content)
    {
        using var fixture = new Fixture(content: content);
        Require(content.GetAll<ServiceIncidentDefinitionSO>().Count(value =>
            value.kind == ServiceIncidentKind.Contamination) == 2, "Incident variant fixture requires two same-kind definitions.");
        ServiceIncidentDefinitionSO added = content.GetAll<ServiceIncidentDefinitionSO>()
            .Single(value => value.kind == ServiceIncidentKind.Contamination
                && value.StableId.EndsWith("-qa-so-extension", StringComparison.Ordinal));
        ServiceIncidentDefinitionSO original = content.GetAll<ServiceIncidentDefinitionSO>()
            .Single(value => value.kind == ServiceIncidentKind.Contamination && value != added);
        // Select fixture input before invoking any owner: exercise the added variant itself.
        V20DailyEventContext context = Enumerable.Range(1, 64).Select(seed =>
            new V20DailyEventContext { AbsoluteDay = 42, RunSeed = seed, Season = Season.Spring })
            .First(value => V20ContentDefinitionRules.EventRoll(value, added.StableId)
                < V20ContentDefinitionRules.EventRoll(value, original.StableId));
        fixture.Transport.Reject = true;
        string before = JsonUtility.ToJson(fixture.Campaign.CaptureSociety());
        Require(!fixture.Campaign.CaptureObservedMealIncident(Evidence(), context).Created
            && JsonUtility.ToJson(fixture.Campaign.CaptureSociety()) == before,
            "Rejected variant incident changed campaign authority.");
        fixture.Transport.Reject = false;
        var result = fixture.Campaign.CaptureObservedMealIncident(Evidence(), context);
        Require(result.Created && fixture.NativeCount == 1
            && fixture.Campaign.CaptureSociety().successfulIncidentOperations.Single().definitionId == added.StableId,
            "Two same-kind definitions blocked the real incident producer.");
        Bundle saved = RoundTrip(fixture.Capture());
        using var restored = new Fixture(saved, content);
        string once = JsonUtility.ToJson(restored.Capture());
        Require(restored.Campaign.CaptureObservedMealIncident(Evidence(), context).Disposition
            == ObservedMealIncidentCaptureDisposition.ExactReplay
            && JsonUtility.ToJson(restored.Capture()) == once && restored.NativeCount == 1,
            "Variant incident JSON/fresh-owner replay duplicated the event or outcome.");
        return "SO_INCIDENT_VARIANTS=PASS; same-kind composition; real rejection/retry; JSON/fresh owner; exact replay";
    }

    public static void RunBatch()
    {
        Require(Application.isBatchMode, "B114 requires isolated batch execution.");
        VerifyRejection();
        VerifyPublicationFenceReadTiming(reject: false);
        VerifyPublicationFenceReadTiming(reject: true);
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

    private static void VerifyPublicationFenceReadTiming(bool reject)
    {
        using var fixture = new Fixture();
        SocietyEventAggregateState before = fixture.Campaign.PrepareSociety(fixture.Campaign.CaptureSociety());
        int boundaries = 0;
        void Check(int activeCount)
        {
            boundaries++;
            Require(fixture.Campaign.ActiveSocietyEvents.Count == activeCount
                    && fixture.Campaign.GetOwnedCustomerIds().Count == activeCount
                    && fixture.Campaign.GetRemainingDays() == 0,
                "Publication queries did not see the currently published Society root.");
            RejectPublication(() => fixture.Campaign.CaptureSociety());
            RejectPublication(() => fixture.Campaign.PublishSociety(before));
            RejectPublication(() => fixture.Capture());
            RejectPublication(() => fixture.Campaign.CaptureObservedMealIncident(Evidence(), Context()));
            RejectPublication(() => fixture.Campaign.ApplyWorkDelay("publication-probe", 1, 42));
            fixture.Campaign.ApplyWorkDelay("publication-probe", 0, 42);
        }
        fixture.Publisher.BeforeReserve = () => Check(0);
        fixture.Publisher.BeforePublish = () => Check(1);
        // The journal cancels its dispatcher lease before returning delivery.
        // The campaign candidate is still published at that boundary, even on rejection.
        fixture.Transport.BeforeCancel = () => Check(1);
        fixture.Transport.Reject = reject;
        var result = fixture.Campaign.CaptureObservedMealIncident(Evidence(), Context());
        Require(result.Created == !reject && boundaries == 3
                && fixture.Campaign.CaptureSociety().workDelays.Count == 0,
            "Publication fence did not span reservation, candidate publication and lease cancellation.");
        fixture.Publisher.BeforeReserve = fixture.Publisher.BeforePublish = fixture.Transport.BeforeCancel = null;
        fixture.Campaign.ApplyWorkDelay("publication-probe", 1, 42);
        Require(fixture.Campaign.CaptureSociety().workDelays.Count == 1,
            "Publication fence remained held after the transaction completed.");
        Debug.Log("ARCHITECTURE_V20_INCIDENT_FENCE=PASS; rejected=" + reject
            + "; reservation; candidate; cancellation; current-root queries; idle release");
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
        internal readonly PublisherProbe Publisher;
        internal readonly V20CampaignRuntime Campaign;
        internal int NativeCount => Outcomes.Ledger.CaptureGameplayOutcomes().outbox.Count
            + Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count;
        internal Fixture(Bundle saved = null, IGameContentDefinitionSource content = null)
        {
            Facts = new SocialRitualFactEditorFixture(Outcomes);
            Transport = new Transport(Facts.Dispatcher);
            Journal = new SocialRitualFactJournal(Transport, Transport);
            Publisher = new PublisherProbe(Journal);
            Campaign = new V20CampaignRuntime(Outcomes.AggregateRootStore,
                new V20StoryContentCatalog(content ?? new ResourceGameContentCatalog(new UnityGameContentRootLoader())), null, null, Publisher);
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

    private sealed class PublisherProbe : ISocialRitualFactPublisher
    {
        private readonly ISocialRitualFactPublisher publisher;
        internal Action BeforeReserve, BeforePublish;
        internal PublisherProbe(ISocialRitualFactPublisher publisher) => this.publisher = publisher;
        public void AssertIdle() => publisher.AssertIdle();
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure)
        {
            BeforeReserve?.Invoke();
            return publisher.TryReserve(fact, out lease, out failure);
        }
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
        {
            BeforePublish?.Invoke();
            return publisher.Publish(lease, fact);
        }
        public void Cancel(in DomainFactLease lease)
        {
            publisher.Cancel(lease);
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            publisher.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => publisher.Replay(fact);
    }

    private sealed class Transport : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher dispatcher;
        internal bool Reject, ThrowBeforeCommit, HideAcknowledgement;
        internal Action BeforePublish, BeforeCancel;
        internal Transport(DomainFactDispatcher dispatcher) => this.dispatcher = dispatcher;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) => dispatcher.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact) => dispatcher.Publish(lease, fact);
        public void Cancel(in DomainFactLease lease)
        {
            BeforeCancel?.Invoke();
            dispatcher.Cancel(lease);
        }
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
    private static void RejectPublication(Action action)
    {
        try { action(); }
        catch (InvalidOperationException error) when (
            error.Message == "observed-service-incident-publication-active") { return; }
        throw new InvalidOperationException("Expected the Society publication fence to reject reentry.");
    }
    private static void Require(bool value, string failure)
    { if (!value) throw new InvalidOperationException(failure); }
}
#endif
