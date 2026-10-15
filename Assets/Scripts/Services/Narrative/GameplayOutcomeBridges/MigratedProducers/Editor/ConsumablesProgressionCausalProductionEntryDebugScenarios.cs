#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class ConsumablesProgressionCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:character-consumables-progression";
    internal const string MealActionId = "action:consume-field-meal";
    internal const string SubstanceActionId = "action:consume-medical-substance";

    private const string OwnerKind =
        "owner-kind:character-consumables-progression";
    private const string DomainStateSchema =
        "dungeonstory-causal-character-consumables-owner-state@1";
    private const string CharacterIdValue =
        "character:causal:consumables-progression";
    private const int AbsoluteDay = 42;
    private const float MealNutrition = 30f;
    private const float MealMood = 3f;
    private const float SubstanceMood = 1f;
    private const float SubstanceTolerance = 1f;

    private static readonly BuildingInstanceId FacilityId = new(
        "building:migrated-e2e:medical");
    private static readonly ConsumableItemDefinitionId MealId = new(
        "food:migrated-e2e:meal");
    private static readonly ConsumableItemDefinitionId SubstanceId = new(
        "drug:migrated-e2e:medicine");
    private static readonly ConsumableItemDefinitionId DetoxId = new(
        "medicine:migrated-e2e:antidote");
    private static readonly ItemStackId MealStack = new(
        "stack:migrated-e2e:meal");
    private static readonly ItemStackId SubstanceStack = new(
        "stack:migrated-e2e:substance");
    private static readonly ItemStackId DetoxStack = new(
        "stack:migrated-e2e:detox");

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
        {
            throw new ArgumentException(
                "A causal run ID is required.",
                nameof(causalRunId));
        }
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A consumables checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            Fixture(
                MealActionId,
                MigratedProducerOutcomeKind.CharacterConsumablesMealResult,
                context,
                context.ConsumeMeal,
                context.RequireMealOutcome,
                lifetime),
            Fixture(
                SubstanceActionId,
                MigratedProducerOutcomeKind.CharacterConsumablesSubstanceResult,
                context,
                context.ConsumeSubstance,
                context.RequireSubstanceOutcome,
                lifetime)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerIdValue =
            "character-consumables:causal-progression:focused";
        JObject mealCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-consumables:meal",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture meal = fixtures.Get(MealActionId);
            IReadOnlyDictionary<string, object> before = meal.CaptureState();
            Require((int)before["consumables.stage"] == 0
                    && (int)before["consumables.completedOperationCount"] == 0,
                "Consumables owner did not start before its first meal.");
            Require(meal.Execute(), "The real field-meal command failed.");
            Require(meal.RequireExactOutcome().HasCanonicalPayloadHash,
                "The meal outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = meal.CaptureState();
            Require((int)after["consumables.stage"] == 1
                    && Approximately(
                        Convert.ToSingle(
                            after["consumables.hungerRecovered"],
                            CultureInfo.InvariantCulture),
                        MealNutrition)
                    && (int)after["consumables.acknowledgementCount"] == 1,
                "The field meal did not persist its real effects and receipt.");
            mealCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)meal)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-consumables:substance",
                   OwnerIdValue,
                   mealCheckpoint)))
        {
            IProductionCausalActionFixture meal = fixtures.Get(MealActionId);
            IProductionCausalActionFixture substance = fixtures.Get(
                SubstanceActionId);
            Require(!meal.Execute(),
                "Fresh composition restore replayed the consumed meal.");
            Require(substance.Execute(),
                "The restored owner did not consume the medical substance.");
            Require(substance.RequireExactOutcome().HasCanonicalPayloadHash,
                "The substance outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after =
                substance.CaptureState();
            Require((int)after["consumables.stage"] == 2
                    && (int)after["consumables.completedOperationCount"] == 2
                    && Approximately(
                        Convert.ToSingle(
                            after["consumables.substanceTolerance"],
                            CultureInfo.InvariantCulture),
                        SubstanceTolerance)
                    && Approximately(
                        Convert.ToSingle(
                            after["consumables.moodApplied"],
                            CultureInfo.InvariantCulture),
                        MealMood + SubstanceMood)
                    && (int)after["consumables.acknowledgementCount"] == 2,
                "The medical substance did not preserve the prior meal state.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)substance)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-consumables:terminal",
                   OwnerIdValue,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture meal = fixtures.Get(MealActionId);
            IProductionCausalActionFixture substance = fixtures.Get(
                SubstanceActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)substance)
                .CaptureOwnerCheckpoint();
            Require(!meal.Execute() && !substance.Execute(),
                "Terminal consumables owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)substance)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected consumables replay mutated the owner checkpoint.");
        }

        JObject effectTamper = (JObject)mealCheckpoint.DeepClone();
        ((JObject)effectTamper["domainState"])["port"]![
            "hungerRecovered"] = 0f;
        Require(RejectCheckpoint(OwnerIdValue, effectTamper, "effect-tamper"),
            "A meal checkpoint with missing hunger recovery was accepted.");

        JObject operationTamper = (JObject)mealCheckpoint.DeepClone();
        ((JObject)operationTamper["domainState"])["runtime"]![
            "completedOperations"] = new JArray();
        Require(RejectCheckpoint(
                OwnerIdValue,
                operationTamper,
                "operation-tamper"),
            "A meal checkpoint without its completed operation was accepted.");

        if (log)
        {
            Debug.Log(
                "Field meal -> restored medical substance causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Consumables causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static IProductionCausalActionFixture Fixture(
        string actionId,
        MigratedProducerOutcomeKind kind,
        Context context,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime) =>
        new DelegateProductionCausalActionFixture(
            actionId,
            "survival",
            MigratedProducerOutcomeCatalog.Get(kind).OutcomeTypeId,
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

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-consumables:" + suffix,
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
        private readonly CharacterConsumablesPolicyFactFixture policyFacts;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly CharacterId actorId = new(CharacterIdValue);
        private readonly MigratedProducerCharacterSurvivalEntryE2EDebugScenarios
            .ConsumablesPort port;
        private readonly CharacterConsumablesRuntime runtime;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":character-consumables-ledger";
            var aggregateRootStore = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ledgerRunId,
                aggregateRootStore);
            policyFacts = new CharacterConsumablesPolicyFactFixture(
                causalRunId + ":policy-facts");
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            AttachConsumptionFactObservers(
                policyFacts.Dispatcher,
                outcomes,
                probe);
            port = new MigratedProducerCharacterSurvivalEntryE2EDebugScenarios
                .ConsumablesPort(
                    actorId,
                    FacilityId,
                    MealId,
                    SubstanceId,
                    DetoxId,
                    MealStack,
                    SubstanceStack,
                    DetoxStack);
            runtime = new CharacterConsumablesRuntime(
                port,
                port,
                port,
                FixedClock.Instance,
                new RandomStreamProvider(104729),
                aggregateRootStore,
                DefaultCharacterNeedBalanceRuntime.Instance,
                domainFacts: policyFacts.Dispatcher,
                calendar: new EditorFixedGameCalendar(AbsoluteDay, 9));
            CharacterConsumablesPolicyChangeResult policy =
                runtime.SetSubstancePolicy(
                    actorId,
                    "substance:migrated-e2e:medicine",
                    SubstancePolicyMode.MedicalOnly,
                    0f,
                    0);
            if (policy.Code != CharacterConsumablesPolicyChangeCode.Changed)
            {
                throw new InvalidOperationException(
                    "Consumables causal fixture could not publish its initial policy fact: "
                    + policy.DetailCode);
            }
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
        internal string PersistentOwnerKind => HasPersistentOwner
            ? OwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            DungeonCharacterConsumablesSaveData save = runtime.Capture();
            CharacterSubstanceState substance = save.substanceStates
                .SingleOrDefault();
            return new Dictionary<string, object>
            {
                ["consumables.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "character-consumables:ephemeral-progression",
                ["consumables.stage"] = Stage,
                ["consumables.absoluteDay"] = AbsoluteDay,
                ["consumables.characterId"] = CharacterIdValue,
                ["consumables.completedOperationCount"] =
                    save.completedOperations.Count,
                ["consumables.substanceStateCount"] =
                    save.substanceStates.Count,
                ["consumables.substanceTolerance"] =
                    substance?.tolerance ?? 0f,
                ["consumables.hungerRecovered"] = port.HungerRecovered,
                ["consumables.moodApplied"] = port.MoodApplied,
                ["consumables.damageApplied"] = port.DamageApplied,
                ["consumables.acknowledgementCount"] =
                    port.AcknowledgementCount,
                ["consumables.pendingCommitCount"] = port.PendingCommitCount,
                ["consumables.mealOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterConsumablesMealResult),
                ["consumables.substanceOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .CharacterConsumablesSubstanceResult),
                ["consumables.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool ConsumeMeal()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            bool consumed = runtime.TryConsumeFieldMeal(
                actorId,
                MealStack,
                out CharacterConsumablesMealResult result);
            bool valid = consumed && result.Success && Stage == 1;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool ConsumeSubstance()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            bool consumed = runtime.TryConsumeSubstance(
                new ConsumeSubstanceByIdCommand(
                    new ConsumableOperationId(
                        "consumable-operation:causal:substance"),
                    actorId,
                    SubstanceId,
                    SubstanceStack,
                    medicalContext: true,
                    combatContext: false),
                out CharacterConsumablesSubstanceResult result);
            bool valid = consumed && result.Success && Stage == 2;
            if (valid)
                ValidateState();
            return valid;
        }

        internal GameplayOutcomeReplayIdentity RequireMealOutcome() =>
            RequireOutcome(
                MigratedProducerOutcomeKind.CharacterConsumablesMealResult,
                expectedSequence: 1);

        internal GameplayOutcomeReplayIdentity RequireSubstanceOutcome() =>
            RequireOutcome(
                MigratedProducerOutcomeKind.CharacterConsumablesSubstanceResult,
                expectedSequence: 2);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral consumables fixtures have no checkpoint.");
            }
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
                    ["runtime"] = JObject.Parse(
                        JsonUtility.ToJson(runtime.Capture())),
                    ["port"] = JObject.Parse(JsonUtility.ToJson(
                        port.CaptureCausalState())),
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
            policyFacts.Dispose();
        }

        private int Stage
        {
            get
            {
                DungeonCharacterConsumablesSaveData save = runtime.Capture();
                int meals = OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterConsumablesMealResult);
                int substances = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .CharacterConsumablesSubstanceResult);
                CharacterSubstanceState substance = save.substanceStates
                    .SingleOrDefault();
                if (meals == 0 && substances == 0
                    && save.completedOperations.Count == 0
                    && save.substanceStates.Count == 0
                    && Approximately(port.HungerRecovered, 0f)
                    && Approximately(port.MoodApplied, 0f)
                    && port.AcknowledgementCount == 0
                    && port.PendingCommitCount == 0)
                {
                    return 0;
                }
                if (meals == 1 && substances == 0
                    && save.completedOperations.Count == 1
                    && save.substanceStates.Count == 0
                    && Approximately(port.HungerRecovered, MealNutrition)
                    && Approximately(port.MoodApplied, MealMood)
                    && port.AcknowledgementCount == 1
                    && port.PendingCommitCount == 0)
                {
                    return 1;
                }
                if (meals == 1 && substances == 1
                    && save.completedOperations.Count == 2
                    && save.substanceStates.Count == 1
                    && substance != null
                    && string.Equals(substance.characterId, CharacterIdValue,
                        StringComparison.Ordinal)
                    && string.Equals(substance.itemDefinitionId,
                        SubstanceId.Value,
                        StringComparison.Ordinal)
                    && Approximately(
                        substance.tolerance,
                        SubstanceTolerance)
                    && Approximately(port.HungerRecovered, MealNutrition)
                    && Approximately(
                        port.MoodApplied,
                        MealMood + SubstanceMood)
                    && port.AcknowledgementCount == 2
                    && port.PendingCommitCount == 0)
                {
                    return 2;
                }
                return -1;
            }
        }

        private static void AttachConsumptionFactObservers(
            DomainFactDispatcher dispatcher,
            MigratedProducerOutcomeEditorFixture outcomes,
            MigratedProducerOutcomeProbeTransaction probe)
        {
            var commits = new GameplayDomainFactCommitGroup(
                outcomes.Recorder,
                outcomes.Ledger);
            dispatcher.Subscribe(
                "test:causal:character-consumables-meal",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterMealConsumedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "test:causal:character-consumables-substance",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterSubstanceConsumedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "test:causal:character-consumables-detox",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterDetoxAppliedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "test:causal:character-consumables-primitive-meal",
                new MigratedProducerDomainFactProbeObserver<
                    CharacterPrimitiveMealCompletedFact>(
                    CharacterConsumptionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind,
            int expectedSequence)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    kind,
                    probe);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact.sequence != expectedSequence
                || exact.absoluteDay != AbsoluteDay
                || exact.status != GameplayOutcomeStatus.Succeeded
                || !string.Equals(
                    exact.producerId,
                    definition.ProducerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                || !HasExpectedSubjectShape(exact, kind))
            {
                throw new InvalidOperationException(
                    "Consumables production entry did not publish its exact outcome: "
                    + kind);
            }
            return identity;
        }

        private static bool HasExpectedSubjectShape(
            GameplayOutcomeSnapshot exact,
            MigratedProducerOutcomeKind kind)
        {
            if (exact?.subjects == null || exact.participants == null)
                return false;
            if (kind != MigratedProducerOutcomeKind.CharacterConsumablesMealResult)
                return exact.subjects.Count == 1;
            return exact.subjects.Count >= 3
                && exact.participants.Any(value =>
                    string.Equals(
                        value.entityKindId,
                        MigratedProducerOutcomeIds.CharacterKind.Value,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.roleId,
                        MigratedProducerOutcomeIds.ActorRole.Value,
                        StringComparison.Ordinal))
                && exact.participants.Any(value =>
                    string.Equals(
                        value.entityKindId,
                        MigratedProducerOutcomeIds.ItemDefinitionKind.Value,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.roleId,
                        MigratedProducerOutcomeIds.ItemRole.Value,
                        StringComparison.Ordinal))
                && exact.participants.Any(value =>
                    string.Equals(
                        value.entityKindId,
                        MigratedProducerOutcomeIds.ItemStackKind.Value,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.roleId,
                        MigratedProducerOutcomeIds.SourceRole.Value,
                        StringComparison.Ordinal))
                && exact.metrics.Any(value => string.Equals(
                    value.metricId,
                    MigratedProducerOutcomeIds.HungerBeforeMetric.Value,
                    StringComparison.Ordinal))
                && exact.metrics.Any(value => string.Equals(
                    value.metricId,
                    MigratedProducerOutcomeIds.HungerAfterMetric.Value,
                    StringComparison.Ordinal));
        }

        private int OutcomeCount(MigratedProducerOutcomeKind kind)
        {
            string type = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        type,
                        StringComparison.Ordinal));
        }

        private int ExactOutcomeCount() => outcomes.Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes.Count;

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
                "character-consumables owner state",
                "schemaVersion",
                "schema",
                "stage",
                "characterId",
                "runtime",
                "port",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["characterId"],
                    CharacterIdValue,
                    StringComparison.Ordinal)
                || state["runtime"] is not JObject runtimeJson
                || state["port"] is not JObject portJson
                || state["outcomeOwners"] is not JObject ownerJson)
            {
                throw new InvalidOperationException(
                    "Consumables checkpoint identity changed.");
            }

            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    ownerJson.ToString(Formatting.None))));
            runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(
                JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                    runtimeJson.ToString(Formatting.None))));
            port.RestoreCausalState(JsonUtility.FromJson<
                MigratedProducerCharacterSurvivalEntryE2EDebugScenarios
                    .ConsumablesPortCausalState>(
                portJson.ToString(Formatting.None)));
            int expectedStage = (int)state["stage"];
            if (expectedStage != Stage)
            {
                throw new InvalidOperationException(
                    "Consumables checkpoint stage disagrees with its saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            bool valid = stage is >= 0 and <= 2
                && port.PendingCommitCount == 0
                && ExactOutcomeCount() == stage
                && OutcomeCount(
                    MigratedProducerOutcomeKind.CharacterConsumablesMealResult)
                    == (stage == 0 ? 0 : 1)
                && OutcomeCount(
                    MigratedProducerOutcomeKind
                        .CharacterConsumablesSubstanceResult)
                    == (stage == 2 ? 1 : 0);
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Consumables runtime, effects, outcome owner and exact ledger diverged.");
            }
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
            this.fixtures = fixtures
                ?? throw new ArgumentNullException(nameof(fixtures));

        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.Single(value => string.Equals(
                value.ActionId,
                actionId,
                StringComparison.Ordinal));

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private sealed class FixedClock : IGameClock
    {
        internal static readonly FixedClock Instance = new();
        public float DeltaTime => 0f;
        public float Time => 1f;
        public int FrameCount => 1;
        public bool IsPaused => false;
    }

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.0001f;

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
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
                label + " fields changed; expected="
                + string.Join(",", orderedExpected)
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
