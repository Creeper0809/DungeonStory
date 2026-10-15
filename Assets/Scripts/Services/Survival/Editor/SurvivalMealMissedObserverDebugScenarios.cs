#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Controlled actual-owner checks; not a natural-play or whole-world-save claim.
public static class SurvivalMealMissedObserverDebugScenarios
{
    public static void RunFocused()
    {
        VerifyRecovery(false);
        VerifyRecovery(true);
        VerifyRejection(false);
        VerifyRejection(true);
        using (var normal = new Fixture())
        {
            normal.Runtime.Initialize();
            int events = 0;
            using var subscription = normal.Events.Subscribe<MealMissedEvent>(_ => events++);
            normal.Events.Publish(new OperatingDayStartedEvent(2));
            Require(normal.Runtime.Capture().missedMealCohorts.Single().publicationConfirmed
                && Count(normal.Outcomes.Ledger.CaptureGameplayOutcomes()) == 2 && events == 2,
                "Normal cohort or repeated Initialize produced the wrong count.");
            normal.Validate();
            string before = JsonUtility.ToJson(normal.Runtime.Capture());
            normal.Runtime.Dispose(); normal.Runtime.Dispose();
            normal.Events.Publish(new OperatingDayStartedEvent(3));
            Require(JsonUtility.ToJson(normal.Runtime.Capture()) == before, "Disposed daily subscription remained live.");
        }
        Require(MealShortageCausalProductionEntryDebugScenarios.RunCausalOwnerFamily(log: true), "Causal regression failed.");
        Debug.Log("MEAL_MISSED_OBSERVERS_FOCUSED_PASS; actualDailyOwner=true; cohortMembers=2; jsonFreshOwner=true; unknownBeforeAndAfterCommit=true; knownRejectionRollback=true; duplicateDailyEffects=0");
    }

    private static void VerifyRecovery(bool commitFirst)
    {
        DungeonSurvivalSaveData survival;
        GameplayOutcomeLedgerSaveData ledger;
        DungeonMigratedProducerOutcomeSaveData counters;
        string applied;
        using (var original = new Fixture(commitFirst))
        {
            int notifications = 0;
            using var subscription = original.Events.Subscribe<MealMissedEvent>(_ => notifications++);
            original.Runtime.OnTriggerEvent(new OperatingDayStartedEvent(2));
            survival = Copy(original.Runtime.Capture());
            ledger = Copy(original.Outcomes.Ledger.CaptureGameplayOutcomes());
            counters = Copy(original.Outcomes.Transaction.Capture());
            applied = DomainState(survival);
            Require(survival.lastProcessedDay == 2 && survival.lastNeededFood == 2
                && survival.lastMissingFood == 2 && survival.missedMealCohorts.Count == 1
                && survival.missedMealCohorts[0].members.Count == 2
                && !survival.missedMealCohorts[0].publicationConfirmed && notifications == 2
                && Count(ledger) == (commitFirst ? 2 : 0), "Unknown response lost applied state or cohort identity.");
            original.Validate();
        }
        var raw = JObject.Parse(JsonUtility.ToJson(survival));
        SurvivalResourcesSaveSection.ValidateMissedMealShape(raw.ToString());
        ((JObject)raw["missedMealCohorts"][0]).Remove("publicationConfirmed");
        ExpectRejected(() => SurvivalResourcesSaveSection.ValidateMissedMealShape(raw.ToString()));
        using (var restored = new Fixture(null, survival, ledger, counters))
        {
            int notifications = 0;
            using var subscription = restored.Events.Subscribe<MealMissedEvent>(_ => notifications++);
            restored.Runtime.OnRestoreCompleted();
            restored.Runtime.RecoverPendingMealMissedFacts();
            restored.Runtime.RecoverPendingMealMissedFacts();
            restored.Runtime.OnTriggerEvent(new OperatingDayStartedEvent(2));
            restored.Runtime.OnTriggerEvent(new OperatingDayStartedEvent(1));
            var confirmed = restored.Runtime.Capture();
            var confirmedLedger = restored.Outcomes.Ledger.CaptureGameplayOutcomes();
            Require(DomainState(confirmed) == applied && notifications == 0
                && confirmed.missedMealCohorts.Single().publicationConfirmed
                && confirmed.missedMealCohorts.Single().members.All(row => row.fact.absoluteDay == 2)
                && Count(confirmedLedger) == 2,
                "Recovery repeated daily effects/events or lost the original day (fixture calendar=137).");
            restored.Validate();
            var partial = Copy(confirmedLedger);
            Require(partial.exactOutcomes.Count == 2, "Recovered results were not fully delivered.");
            partial.exactOutcomes.RemoveAt(1);
            ExpectRejected(() => SurvivalMealMissedFactPreflight.Validate(confirmed, partial, counters));
            var altered = Copy(confirmedLedger);
            altered.exactOutcomes[0].immutablePayloadHash = new string('a', 64);
            ExpectRejected(() => SurvivalMealMissedFactPreflight.Validate(confirmed, altered, counters));
            var lost = Copy(confirmed);
            lost.missedMealCohorts.Clear();
            ExpectRejected(() => SurvivalMealMissedFactPreflight.Validate(lost, confirmedLedger, counters));
        }
        Debug.Log($"MEAL_MISSED_RECOVERY=PASS; committedBeforeAckLoss={commitFirst}; members=2; frozenDay=2; duplicateDailyEffects=0");
    }

    private static void VerifyRejection(bool duringPublication)
    {
        using var fixture = new Fixture(reject: duringPublication, subscribe: duringPublication);
        var before = fixture.Runtime.Capture();
        ExpectRejected(() => fixture.Runtime.OnTriggerEvent(new OperatingDayStartedEvent(2)));
        var after = fixture.Runtime.Capture();
        Require(after.lastProcessedDay == before.lastProcessedDay && after.lastNeededFood == before.lastNeededFood
            && after.lastConsumedFood == before.lastConsumedFood && after.lastMissingFood == before.lastMissingFood
            && after.consecutiveFoodShortageDays == before.consecutiveFoodShortageDays
            && after.missedMealCohorts.Count == 0 && Count(fixture.Outcomes.Ledger.CaptureGameplayOutcomes()) == 0,
            "Known rejection did not roll back its forecast and unpublished cohort.");
    }

    private static string DomainState(DungeonSurvivalSaveData value)
    { var copy = Copy(value); copy.missedMealCohorts.Clear(); return JsonUtility.ToJson(copy); }
    private static T Copy<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    private static int Count(GameplayOutcomeLedgerSaveData value) => value.outbox.Count + value.exactOutcomes.Count + value.tombstones.Count;
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void ExpectRejected(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected rejection was accepted.");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DomainFactDispatcher dispatcher;
        private readonly List<CharacterActor> actors = new();
        private readonly List<CharacterSO> definitions = new();
        public Fixture(bool? loseResponse = null, DungeonSurvivalSaveData survival = null,
            GameplayOutcomeLedgerSaveData ledger = null, DungeonMigratedProducerOutcomeSaveData counters = null,
            bool reject = false, bool subscribe = true)
        {
            var store = new DungeonRuntimeAggregateRootStore();
            Outcomes = new MigratedProducerOutcomeEditorFixture("run:meal-missed-observer", store);
            if (ledger != null)
            {
                var candidate = Outcomes.Ledger.PrepareGameplayOutcomeRestore(ledger);
                Outcomes.Ledger.BeginRestoreCandidate(); Outcomes.Ledger.PublishGameplayOutcomeRestore(candidate);
                Outcomes.Ledger.PublishRestoreCandidate(); Outcomes.Ledger.CompleteRestoreCandidate();
                Outcomes.Transaction.Restore(Outcomes.Transaction.PrepareRestore(counters));
            }
            dispatcher = subscribe ? Outcomes.CreateMealMissedFacts() : new DomainFactDispatcher();
            IDomainFactPublisher publisher = loseResponse.HasValue || reject
                ? new ResponseBoundary(dispatcher, loseResponse == true, reject) : dispatcher;
            Runtime = SurvivalDebugScenarios.CreateMealMissedOutcomeEntryFixture(Events, store, publisher);
            for (int index = 0; index < 2; index++)
            {
                var gameObject = CharacterAiPlanDebugFixtures.CreateActorObject("Meal Missed Observer Worker " + index);
                gameObject.SetActive(false);
                if (gameObject.GetComponent<AbilityWork>() == null) gameObject.AddComponent<AbilityWork>();
                CharacterAiEditorTestDependencies.Inject(gameObject);
                gameObject.SetActive(true);
                var actor = gameObject.GetComponent<CharacterActor>();
                var data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                    CharacterType.NPC, "결식 검사 주민 " + index, "Orc", CharacterRole.Regular);
                actors.Add(actor); definitions.Add(data);
                actor.EnsureRuntimeState(); actor.Initialization(data);
                actor.Identity.SetPersistentId("character:meal-observer:" + index);
                actor.RefreshAbilityCache(); actor.SetLifecycleState(CharacterLifecycleState.Active);
                Require(CharacterWorkRoleUtility.TryGetWork(actor, out _), "Fixture worker capability missing.");
                CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(actor);
                CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacterLifetime(actor);
            }
            Runtime.Initialize();
            if (survival != null) Runtime.PublishRestoreCandidate(Runtime.BuildRestoreCandidate(survival));
        }
        public GameEventBus Events { get; } = new();
        public MigratedProducerOutcomeEditorFixture Outcomes { get; }
        public SurvivalFoodRuntime Runtime { get; }
        public void Validate() => SurvivalMealMissedFactPreflight.Validate(Runtime.Capture(),
            Outcomes.Ledger.CaptureGameplayOutcomes(), Outcomes.Transaction.Capture());
        public void Dispose()
        {
            Runtime.Dispose(); dispatcher.Dispose();
            foreach (var actor in actors)
            {
                CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(actor);
                CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacterLifetime(actor);
                UnityEngine.Object.DestroyImmediate(actor.gameObject);
            }
            foreach (var data in definitions) UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private sealed class ResponseBoundary : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        private readonly bool commitFirst, reject;
        public ResponseBoundary(DomainFactDispatcher inner, bool commitFirst, bool reject)
        { this.inner = inner; this.commitFirst = commitFirst; this.reject = reject; }
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) => inner.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact) => throw new InvalidOperationException("Meal owner must publish its whole cohort.");
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> values, DomainFactDelivery[] results, out string failure)
        {
            if (commitFirst) Require(inner.PublishBatch(values, results, out _), "Fault fixture did not commit the real cohort.");
            else foreach (var value in values) inner.Cancel(value.Lease);
            failure = reject ? "qa:known-precommit-rejection" : "qa:lost-acknowledgement";
            for (int i = 0; i < results.Length; i++) results[i] = reject ? DomainFactDelivery.Reject(failure) : DomainFactDelivery.Uncertain(failure);
            return !reject;
        }
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure) => inner.ReplayBatch(facts, results, out failure);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }
}
#endif
