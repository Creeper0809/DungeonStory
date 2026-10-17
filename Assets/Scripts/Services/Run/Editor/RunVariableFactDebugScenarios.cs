#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class RunVariableFactDebugScenarios
{
    public static void RunBatch()
    {
        Require(Application.isBatchMode, "B113 requires isolated batch execution.");
        VerifyRejectionAndGuard();
        VerifyRecovery(expiration: false, afterCommit: false);
        VerifyRecovery(expiration: true, afterCommit: true);
        Require(RunVariableDebugScenarios.RunAll(true), "Existing run-variable checks failed.");
        Require(RunVariableCausalProductionEntryDebugScenarios.RunCausalOwnerFamily(true), "Causal save round trip failed.");
        RunVariableObservationObserverDebugScenarios.RunForBatch();
        Debug.Log("B113_RUN_VARIABLE_OBSERVERS=PASS");
    }

    private static void VerifyRejectionAndGuard()
    {
        using var fixture = new Fixture();
        string initial = fixture.Section.Capture();
        fixture.Transport.Reject = true;
        Require(fixture.Runtime.ActivateOperationVariable(Fixture.VariableId, 1, false) == null,
            "Explicit observation rejection was accepted.");
        Require(fixture.Section.Capture() == initial && fixture.ExactCount == 0 && fixture.Dispatcher.ActiveLeaseCount == 0,
            "Rejected activation changed its owner or leaked a reservation.");
        fixture.Transport.Reject = false;
        int guarded = 0;
        fixture.Transport.DuringPublish = () =>
        {
            Reject(() => fixture.Runtime.CaptureForSave()); guarded++;
            Reject(() => fixture.Runtime.ActivateOperationVariable(Fixture.VariableId)); guarded++;
            Reject(() => fixture.Runtime.OnTriggerEvent(new OperatingDayEndedEvent(1))); guarded++;
        };
        Require(fixture.Runtime.ActivateOperationVariable(Fixture.VariableId, 1, false) != null && guarded == 3,
            "Actual activation or reentrant guard failed.");
        fixture.Transport.DuringPublish = null;
        string activated = fixture.Section.Capture();
        fixture.Transport.Reject = true;
        fixture.Runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));
        Require(fixture.Section.Capture() == activated && fixture.ExactCount == 1,
            "Rejected expiration lost its active variable or advanced the day.");
        fixture.Transport.Reject = false;
        fixture.Runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));
        fixture.Runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));
        Require(fixture.Runtime.State.ActiveOperationVariables.Count == 0 && fixture.ExactCount == 2,
            "Expiration retry failed or duplicate day created a second result.");
        Debug.Log("B113_RUN_VARIABLE_REJECTION_GUARD=PASS");
    }

    private static void VerifyRecovery(bool expiration, bool afterCommit)
    {
        Bundle saved;
        using (var fixture = new Fixture())
        {
            if (expiration)
                Require(fixture.Runtime.ActivateOperationVariable(Fixture.VariableId, 1, false) != null, "Setup activation failed.");
            fixture.Transport.UnknownBefore = !afterCommit;
            fixture.Transport.UnknownAfter = afterCommit;
            if (expiration) fixture.Runtime.OnTriggerEvent(new OperatingDayEndedEvent(1));
            else Require(fixture.Runtime.ActivateOperationVariable(Fixture.VariableId, 1, false) != null, "Unknown ACK lost activation.");
            saved = fixture.Capture();
            Require(saved.owner.pendingObservations.Count == 1 && fixture.Dispatcher.ActiveLeaseCount == 0,
                "Frozen fact/key missing or transient lease retained.");
            RunVariableFactPreflight.ValidatePending(saved.owner, saved.ledger, saved.counters);
            var invalid = RoundTrip(saved.owner);
            invalid.pendingObservations[0].publicationKey += "-changed";
            Reject(() => RunVariableFactPreflight.ValidatePending(invalid, saved.ledger, saved.counters));
            if (afterCommit)
            {
                invalid = RoundTrip(saved.owner);
                invalid.pendingObservations[0].fact.entries[0].title += " changed";
                Reject(() => RunVariableFactPreflight.ValidatePending(invalid, saved.ledger, saved.counters));
            }
            fixture.Transport.BlockRecovery = true;
            Reject(() => fixture.Runtime.OnTriggerEvent(new OperatingDayStartedEvent(2)));
            Require(JsonUtility.ToJson(fixture.Capture()) == JsonUtility.ToJson(saved),
                "A blocked subsequent command changed the frozen effect.");
        }
        using (var restored = new Fixture(saved))
        {
            int notifications = 0;
            using var activated = restored.Events.Subscribe<RunVariableActivatedEvent>(_ => notifications++);
            using var expired = restored.Events.Subscribe<RunVariableExpiredEvent>(_ => notifications++);
            var before = restored.Runtime.CaptureForSave();
            restored.Runtime.OnRestoreCompleted();
            var after = restored.Runtime.CaptureForSave();
            Require(after.pendingObservations.Count == 0 && restored.ExactCount == (expiration ? 2 : 1)
                && before.nextOperationSequence == after.nextOperationSequence
                && before.lastOperationAdvanceDay == after.lastOperationAdvanceDay
                && before.activeOperationVariables.Select(value => (value.definitionId, value.instanceSequence, value.startDay, value.remainingDays))
                    .SequenceEqual(after.activeOperationVariables.Select(value => (value.definitionId, value.instanceSequence, value.startDay, value.remainingDays)))
                && notifications == 0,
                "Saved observation replay changed effects, repeated notifications, or duplicated native output.");
            string completed = JsonUtility.ToJson(restored.Capture());
            Require(restored.Runtime.ResumePendingObservations() && JsonUtility.ToJson(restored.Capture()) == completed,
                "A completed frozen publication replay changed state.");
            var raw = Newtonsoft.Json.Linq.JObject.Parse(restored.Section.Capture());
            raw.Remove("pendingObservations");
            Reject(() => restored.Section.StageRestore(raw.ToString(), restored.Section.SectionVersion, new DungeonGameRestoreReport()));
            Reject(() => restored.Section.StageRestore(restored.Section.Capture(), 4, new DungeonGameRestoreReport()));
        }
        Debug.Log(afterCommit ? "B113_RUN_VARIABLE_ACK_AFTER=PASS" : "B113_RUN_VARIABLE_ACK_BEFORE=PASS");
    }

    [Serializable]
    private sealed class Bundle
    {
        public DungeonRunVariableSaveData owner;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonMigratedProducerOutcomeSaveData counters;
    }

    private sealed class Fixture : IDisposable
    {
        internal const string VariableId = "run:b113:operation";
        private readonly GameObject host = new("B113 Run Variable Fact Fixture");
        internal readonly MigratedProducerOutcomeEditorFixture Outcomes;
        internal readonly DomainFactDispatcher Dispatcher;
        internal readonly Transport Transport;
        internal readonly GameEventBus Events = new();
        internal readonly RunVariableRuntime Runtime;
        internal readonly RunVariableSaveSection Section;
        internal int ExactCount => Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count
            + Outcomes.Ledger.CaptureGameplayOutcomes().outbox.Count;

        internal Fixture(Bundle saved = null)
        {
            var catalog = new Catalog();
            Outcomes = new MigratedProducerOutcomeEditorFixture("run:b113:variables");
            Dispatcher = Outcomes.CreateRunVariableFacts();
            Transport = new Transport(Dispatcher);
            Runtime = host.AddComponent<RunVariableRuntime>();
            Runtime.Construct(catalog, EditorRuntimeReferenceFixtures.Invasion, catalog,
                new RandomStreamProvider(113), Events, catalog, catalog, Outcomes.AggregateRootStore, Transport, Transport);
            Section = new RunVariableSaveSection(Runtime, catalog, catalog);
            if (saved == null) Runtime.StartRun(113);
            else
            {
                RunVariableFactPreflight.ValidatePending(saved.owner, saved.ledger, saved.counters);
                var report = new DungeonGameRestoreReport();
                var staged = Section.StageRestore(JsonUtility.ToJson(saved.owner), Section.SectionVersion, report);
                var candidate = Outcomes.Ledger.PrepareGameplayOutcomeRestore(saved.ledger);
                Outcomes.Ledger.BeginRestoreCandidate(); Outcomes.Ledger.PublishGameplayOutcomeRestore(candidate);
                Outcomes.Ledger.PublishRestoreCandidate(); Outcomes.Ledger.CompleteRestoreCandidate();
                Outcomes.Transaction.Restore(Outcomes.Transaction.PrepareRestore(saved.counters));
                staged.Commit(report);
                Require(report.Success, "Current save candidate failed: " + string.Join(" | ", report.Errors));
            }
        }
        internal Bundle Capture() => RoundTrip(new Bundle
        { owner = Runtime.CaptureForSave(), ledger = Outcomes.Ledger.CaptureGameplayOutcomes(), counters = Outcomes.Transaction.Capture() });
        public void Dispose() { UnityEngine.Object.DestroyImmediate(host); Dispatcher.Dispose(); }
    }

    private sealed class Transport : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        internal bool Reject, UnknownBefore, UnknownAfter, BlockRecovery;
        internal Action DuringPublish;
        internal Transport(DomainFactDispatcher inner) => this.inner = inner;
        public bool TryReserve<T>(in T value, out DomainFactLease lease, out string failure) => inner.TryReserve(value, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T value)
        {
            DuringPublish?.Invoke();
            if (Reject) return DomainFactDelivery.Reject("B113-controlled-rejection");
            if (UnknownBefore) return DomainFactDelivery.Uncertain("B113-controlled-unknown-before");
            var delivered = inner.Publish(lease, value);
            return UnknownAfter && delivered.MayBeCommitted ? DomainFactDelivery.Uncertain("B113-controlled-unknown-after") : delivered;
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> values, DomainFactDelivery[] results, out string failure)
        {
            if (!BlockRecovery) return inner.ReplayBatch(values, results, out failure);
            failure = "B113-controlled-recovery-blocked";
            for (int index = 0; index < results.Length; index++) results[index] = DomainFactDelivery.Reject(failure);
            return false;
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> values, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(values, results, out failure);
        public DomainFactDelivery Replay<T>(in T value) => inner.Replay(value);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class Catalog : IRunVariableDefinitionCatalog, IOwnerDoctrineDefinitionCatalog,
        IOwnerRunDataProvider, IRunStartVariableSelector
    {
        private readonly RunVariableDefinition variable = new(Fixture.VariableId, RunVariableCategory.Operation,
            "테스트 운영 변수", "하루 동안 적용", EventAlertImportance.Low, 1, Array.Empty<IRunVariableEffect>());
        private readonly OwnerDoctrineDefinition doctrine = new("doctrine:b113", "Slime", "교리", "", "", Array.Empty<IRunVariableEffect>());
        public CharacterSO SelectedOwnerData => null;
        public IReadOnlyCollection<RunVariableDefinition> All => new[] { variable };
        public RunVariableDefinition Get(string id) => id == variable.id ? variable : null;
        public RunVariableDefinition Require(string id) => Get(id) ?? throw new KeyNotFoundException(id);
        public IReadOnlyList<RunVariableDefinition> GetByCategory(RunVariableCategory category) =>
            category == variable.category ? new[] { variable } : Array.Empty<RunVariableDefinition>();
        IReadOnlyCollection<OwnerDoctrineDefinition> IOwnerDoctrineDefinitionCatalog.All => new[] { doctrine };
        OwnerDoctrineDefinition IOwnerDoctrineDefinitionCatalog.Get(string id) => id == doctrine.id ? doctrine : null;
        OwnerDoctrineDefinition IOwnerDoctrineDefinitionCatalog.Require(string id) => id == doctrine.id ? doctrine : throw new KeyNotFoundException(id);
        public OwnerDoctrineDefinition ResolveFor(CharacterSO owner) => doctrine;
        public OwnerDoctrineDefinition ResolveForSpecies(string species) => doctrine;
        public RunStartVariableSnapshot Create(int seed, CharacterSO owner, DungeonDifficulty difficulty,
            DungeonSurvivalPressure survivalPressure = DungeonSurvivalPressure.Standard) =>
            new(seed, "Slime", difficulty, Array.Empty<int>(), Array.Empty<string>(), Array.Empty<int>(), seed, "", 1f, doctrine.id, survivalPressure);
    }

    private static T RoundTrip<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("B113 expected an explicit boundary rejection.");
    }
    private static void Require(bool value, string failure)
    { if (!value) throw new InvalidOperationException(failure); }
}
#endif
