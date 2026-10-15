#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class MealShortageCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:survival-meal-shortage";
    internal const string FirstMissActionId =
        "action:survival-meal-missed-first-day";
    internal const string RepeatMissActionId =
        "action:survival-meal-missed-next-day";

    private const string OwnerKind = "owner-kind:survival-meal-shortage";
    private const string DomainStateSchema =
        "dungeonstory-causal-survival-meal-shortage-owner-state@1";
    private const string CharacterIdValue = "character:causal:meal-shortage";

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException("A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
            throw new ArgumentException("A meal-shortage checkpoint requires an owner ID.", nameof(ownerCheckpoint));

        var context = new Context(causalRunId, persistent ? ownerId : null, ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(2, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            Fixture(
                FirstMissActionId,
                context,
                context.RecordFirstMiss,
                () => context.RequireOutcome(1),
                lifetime),
            Fixture(
                RepeatMissActionId,
                context,
                context.RecordRepeatMiss,
                () => context.RequireOutcome(2),
                lifetime)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerIdValue = "survival-meal-shortage:focused";
        JObject firstCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-meal-shortage:first",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture first = fixtures.Get(FirstMissActionId);
            Require((int)first.CaptureState()["mealShortage.stage"] == 0,
                "Meal-shortage owner did not start before the first missed meal.");
            Require(first.Execute(), "The first real meal-missed path failed.");
            Require(first.RequireExactOutcome().HasCanonicalPayloadHash,
                "The first meal-missed outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = first.CaptureState();
            Require((int)after["mealShortage.stage"] == 1
                    && (int)after["mealShortage.lastProcessedDay"] == 2
                    && (int)after["mealShortage.missingFood"] == 1
                    && (int)after["mealShortage.exactOutcomeCount"] == 1,
                "The first missed meal did not persist its runtime and ledger state.");
            firstCheckpoint = ((IProductionCausalOwnerCheckpointFixture)first)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-meal-shortage:repeat",
                   OwnerIdValue,
                   firstCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(FirstMissActionId);
            IProductionCausalActionFixture repeat = fixtures.Get(RepeatMissActionId);
            Require(!first.Execute(), "Fresh composition replayed the first missed meal.");
            Require(repeat.Execute(), "The restored owner did not record the next missed meal.");
            Require(repeat.RequireExactOutcome().HasCanonicalPayloadHash,
                "The repeated meal-missed outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = repeat.CaptureState();
            Require((int)after["mealShortage.stage"] == 2
                    && (int)after["mealShortage.lastProcessedDay"] == 3
                    && (int)after["mealShortage.missingFood"] == 1
                    && (int)after["mealShortage.exactOutcomeCount"] == 2,
                "The next missed meal did not preserve the prior shortage state.");
            terminalCheckpoint = ((IProductionCausalOwnerCheckpointFixture)repeat)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-meal-shortage:terminal",
                   OwnerIdValue,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture first = fixtures.Get(FirstMissActionId);
            IProductionCausalActionFixture repeat = fixtures.Get(RepeatMissActionId);
            JObject before = ((IProductionCausalOwnerCheckpointFixture)repeat)
                .CaptureOwnerCheckpoint();
            Require(!first.Execute() && !repeat.Execute(),
                "Terminal meal-shortage owner accepted a replay.");
            JObject after = ((IProductionCausalOwnerCheckpointFixture)repeat)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected meal-shortage replay mutated the owner checkpoint.");
        }

        JObject dayTamper = (JObject)firstCheckpoint.DeepClone();
        ((JObject)dayTamper["domainState"])["runtime"]!["lastProcessedDay"] = 1;
        Require(RejectCheckpoint(OwnerIdValue, dayTamper, "day-tamper"),
            "A meal-shortage checkpoint with a false day was accepted.");

        JObject outcomeTamper = (JObject)firstCheckpoint.DeepClone();
        ((JObject)outcomeTamper["domainState"])["outcomeOwners"]![
            "owners"] = new JArray();
        Require(RejectCheckpoint(OwnerIdValue, outcomeTamper, "outcome-tamper"),
            "A meal-shortage checkpoint without its outcome owner was accepted.");

        if (log)
            Debug.Log("Missed meal -> restored next-day missed meal causal owner family PASS");
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Meal-shortage causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static IProductionCausalActionFixture Fixture(
        string actionId,
        Context context,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime) =>
        new DelegateProductionCausalActionFixture(
            actionId,
            "survival",
            MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.MealMissedEvent).OutcomeTypeId,
            context.CaptureState,
            execute,
            requireOutcome,
            context.CaptureOutcomeEvidence,
            lifetime.Release,
            context.PersistentOwnerId,
            context.PersistentOwnerKind,
            context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
            context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
            context.HasPersistentOwner ? context : null);

    private static bool RejectCheckpoint(string ownerId, JObject checkpoint, string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-meal-shortage:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly DomainFactDispatcher mealMissedFacts;
        private readonly SurvivalFoodRuntime runtime;
        private readonly CharacterActor actor;
        private readonly CharacterSO actorData;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(this.ownerId, OwnerKind)
                : causalRunId + ":meal-shortage-ledger";
            var aggregateRootStore = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(ledgerRunId, aggregateRootStore);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            mealMissedFacts = outcomes.CreateMealMissedFacts(probe);
            runtime = SurvivalDebugScenarios.CreateMealMissedOutcomeEntryFixture(
                new GameEventBus(),
                aggregateRootStore,
                mealMissedFacts);

            GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "Causal Meal Shortage Worker");
            actorObject.SetActive(false);
            if (actorObject.GetComponent<AbilityWork>() == null)
                actorObject.AddComponent<AbilityWork>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorObject.SetActive(true);
            actor = actorObject.GetComponent<CharacterActor>();
            actorData = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                "결식 생존자",
                "Orc",
                CharacterRole.Regular);
            actor.EnsureRuntimeState();
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId(CharacterIdValue);
            actor.RefreshAbilityCache();
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            if (!CharacterWorkRoleUtility.TryGetWork(actor, out _))
            {
                throw new InvalidOperationException(
                    "Meal-shortage character is not a real survival worker.");
            }
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(actor);
            CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacterLifetime(actor);
            runtime.Initialize();

            try
            {
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                probe.ClearObservations();
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner ? OwnerKind : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            DungeonSurvivalSaveData save = runtime.Capture();
            return new Dictionary<string, object>
            {
                ["mealShortage.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "survival-meal-shortage:ephemeral",
                ["mealShortage.stage"] = Stage,
                ["mealShortage.characterId"] = CharacterIdValue,
                ["mealShortage.lastProcessedDay"] = save.lastProcessedDay,
                ["mealShortage.neededFood"] = save.lastNeededFood,
                ["mealShortage.consumedFood"] = save.lastConsumedFood,
                ["mealShortage.missingFood"] = save.lastMissingFood,
                ["mealShortage.neededWater"] = save.lastNeededWater,
                ["mealShortage.consumedWater"] = save.lastConsumedWater,
                ["mealShortage.missingWater"] = save.lastMissingWater,
                ["mealShortage.foodShortageDays"] = save.consecutiveFoodShortageDays,
                ["mealShortage.waterShortageDays"] = save.consecutiveWaterShortageDays,
                ["mealShortage.sanitationRisk"] = save.sanitationRisk,
                ["mealShortage.diseaseRisk"] = save.diseaseRisk,
                ["mealShortage.exteriorNightDanger"] = save.exteriorNightDanger,
                ["mealShortage.mealLedgerCount"] = save.mealLedger.Count,
                ["mealShortage.healthStateCount"] = save.health.Count,
                ["mealShortage.outcomeCount"] = OutcomeCount(),
                ["mealShortage.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool RecordFirstMiss() => RecordMiss(expectedStage: 0, day: 2);
        internal bool RecordRepeatMiss() => RecordMiss(expectedStage: 1, day: 3);

        internal GameplayOutcomeReplayIdentity RequireOutcome(int expectedSequence)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    MigratedProducerOutcomeKind.MealMissedEvent,
                    probe);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.MealMissedEvent);
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot exact)
                || exact.sequence != expectedSequence
                || exact.absoluteDay != expectedSequence + 1
                || exact.status != GameplayOutcomeStatus.Succeeded
                || !string.Equals(exact.producerId, definition.ProducerId, StringComparison.Ordinal)
                || !string.Equals(exact.outcomeTypeId, definition.OutcomeTypeId.Value, StringComparison.Ordinal)
                || exact.subjects == null
                || exact.subjects.Count != 1
                || !string.Equals(
                    exact.subjects[0].entityId,
                    CharacterIdValue,
                    StringComparison.Ordinal)
                || exact.participants == null
                || exact.participants.Count != 1
                || !string.Equals(
                    exact.participants[0].entityId,
                    CharacterIdValue,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Meal-shortage production entry did not publish its exact outcome.");
            }
            return identity;
        }

        internal JObject CaptureOutcomeEvidence(GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
                throw new InvalidOperationException("Ephemeral meal-shortage fixtures have no checkpoint.");
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 1,
                    ["schema"] = DomainStateSchema,
                    ["stage"] = Stage,
                    ["characterId"] = CharacterIdValue,
                    ["runtime"] = JObject.Parse(JsonUtility.ToJson(runtime.Capture())),
                    ["outcomeOwners"] = JObject.Parse(JsonUtility.ToJson(
                        outcomes.Transaction.Capture()))
                },
                outcomes.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            runtime.Dispose();
            mealMissedFacts.Dispose();
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacter(actor);
            CharacterAiEditorTestDependencies.WorldRegistry.UnregisterCharacterLifetime(actor);
            if (actorData != null)
                UnityEngine.Object.DestroyImmediate(actorData);
            if (actor != null)
                UnityEngine.Object.DestroyImmediate(actor.gameObject);
        }

        private int Stage
        {
            get
            {
                DungeonSurvivalSaveData save = runtime.Capture();
                int outcomesCount = OutcomeCount();
                if (save.lastProcessedDay == 0 && outcomesCount == 0 && ExactOutcomeCount() == 0)
                    return 0;
                if (save.lastProcessedDay == 2 && outcomesCount == 1 && ExactOutcomeCount() == 1)
                    return 1;
                if (save.lastProcessedDay == 3 && outcomesCount == 2 && ExactOutcomeCount() == 2)
                    return 2;
                return -1;
            }
        }

        private bool RecordMiss(int expectedStage, int day)
        {
            ThrowIfDisposed();
            if (Stage != expectedStage)
                return false;
            runtime.OnTriggerEvent(new OperatingDayStartedEvent(day));
            bool valid = Stage == expectedStage + 1;
            if (valid)
                ValidateState();
            return valid;
        }

        private int OutcomeCount()
        {
            string type = MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.MealMissedEvent).OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(value.outcomeTypeId, type, StringComparison.Ordinal));
        }

        private int ExactOutcomeCount() =>
            outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count;

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                outcomes.Ledger);
            RequireFields(
                state,
                "meal-shortage owner state",
                "schemaVersion",
                "schema",
                "stage",
                "characterId",
                "runtime",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals((string)state["schema"], DomainStateSchema, StringComparison.Ordinal)
                || !string.Equals((string)state["characterId"], CharacterIdValue, StringComparison.Ordinal)
                || state["runtime"] is not JObject runtimeJson
                || state["outcomeOwners"] is not JObject ownerJson)
            {
                throw new InvalidOperationException("Meal-shortage checkpoint identity changed.");
            }

            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    ownerJson.ToString(Formatting.None))));
            runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(
                JsonUtility.FromJson<DungeonSurvivalSaveData>(
                    runtimeJson.ToString(Formatting.None))));
            if ((int)state["stage"] != Stage)
                throw new InvalidOperationException(
                    "Meal-shortage checkpoint stage disagrees with its saved state.");
        }

        private void ValidateState()
        {
            int stage = Stage;
            DungeonSurvivalSaveData save = runtime.Capture();
            bool valid = stage is >= 0 and <= 2
                && OutcomeCount() == stage
                && ExactOutcomeCount() == stage
                && outcomes.Transaction.Capture().owners.Count == stage
                && (stage == 0
                    || save.lastNeededFood == 1
                    && save.lastConsumedFood == 0
                    && save.lastMissingFood == 1
                    && save.mealLedger.Count == 0);
            if (!valid)
                throw new InvalidOperationException(
                    "Meal-shortage runtime, outcome owner and exact ledger diverged.");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;
        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures ?? throw new ArgumentNullException(nameof(fixtures));
        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.Single(value => string.Equals(value.ActionId, actionId, StringComparison.Ordinal));
        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private static void RequireFields(JObject value, string label, params string[] expected)
    {
        string[] actual = value?.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        string[] orderedExpected = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(orderedExpected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields changed; expected=" + string.Join(",", orderedExpected)
                + ";actual=" + string.Join(",", actual));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
