#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RunVariableCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:run-operation-variable";
    internal const string ActivateActionId =
        "action:run-operation-variable-activated";
    internal const string ExpireActionId =
        "action:run-operation-variable-expired";

    private const string OwnerKind = "owner-kind:run-operation-variable";
    private const string DomainStateSchema =
        "dungeonstory-causal-run-operation-variable-owner-state@1";
    private const string VariableId = "run:causal-operation-variable";
    private const int RunSeed = 54123;
    private const int ActivationDay = 1;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException("A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A run-variable checkpoint requires an owner ID.",
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
            new DelegateProductionCausalActionFixture(
                ActivateActionId,
                "run",
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.RunVariableActivatedEvent)
                    .OutcomeTypeId,
                context.CaptureState,
                context.Activate,
                context.RequireActivatedOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                ExpireActionId,
                "run",
                MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.RunVariableExpiredEvent)
                    .OutcomeTypeId,
                context.CaptureState,
                context.Expire,
                context.RequireExpiredOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerId = "run-owner:causal-operation-variable:focused";
        JObject activeCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-operation-variable:activate",
                   OwnerId)))
        {
            IProductionCausalActionFixture activate = fixtures.Get(ActivateActionId);
            IReadOnlyDictionary<string, object> before = activate.CaptureState();
            Require((int)before["runVariable.stage"] == 0
                    && (int)before["runVariable.activeCount"] == 0
                    && (int)before["runVariable.exactOutcomeCount"] == 0,
                "Run-variable owner did not start empty.");
            Require(activate.Execute(), "The operation variable did not activate.");
            Require(activate.RequireExactOutcome().HasCanonicalPayloadHash,
                "The activation outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = activate.CaptureState();
            Require((int)after["runVariable.stage"] == 1
                    && (int)after["runVariable.activeCount"] == 1
                    && (int)after["runVariable.remainingDays"] == 1
                    && (long)after["runVariable.instanceSequence"] == 1L
                    && (long)after["runVariable.nextOperationSequence"] == 2L
                    && (int)after["runVariable.exactOutcomeCount"] == 1,
                "Activation did not retain the real run-variable owner state.");
            activeCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)activate)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-operation-variable:expire",
                   OwnerId,
                   activeCheckpoint)))
        {
            IProductionCausalActionFixture activate = fixtures.Get(ActivateActionId);
            IProductionCausalActionFixture expire = fixtures.Get(ExpireActionId);
            IReadOnlyDictionary<string, object> restored = expire.CaptureState();
            Require((int)restored["runVariable.stage"] == 1
                    && (int)restored["runVariable.activeCount"] == 1
                    && !activate.Execute(),
                "Fresh composition restore lost or duplicated activation.");
            Require(expire.Execute(), "The restored operation variable did not expire.");
            Require(expire.RequireExactOutcome().HasCanonicalPayloadHash,
                "The expiration outcome has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = expire.CaptureState();
            Require((int)after["runVariable.stage"] == 2
                    && (int)after["runVariable.activeCount"] == 0
                    && (int)after["runVariable.lastOperationAdvanceDay"] == 1
                    && (int)after["runVariable.exactOutcomeCount"] == 2,
                "Expiration did not preserve activation and advance the saved owner.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)expire)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-operation-variable:terminal",
                   OwnerId,
                   terminalCheckpoint)))
        {
            IProductionCausalActionFixture activate = fixtures.Get(ActivateActionId);
            IProductionCausalActionFixture expire = fixtures.Get(ExpireActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)activate)
                .CaptureOwnerCheckpoint();
            Require(!activate.Execute() && !expire.Execute(),
                "Terminal run-variable owner accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)expire)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected run-variable replay mutated the owner checkpoint.");
        }

        JObject durationTamper = (JObject)activeCheckpoint.DeepClone();
        JObject domain = (JObject)durationTamper["domainState"];
        JObject runVariables = (JObject)domain["runVariables"];
        ((JObject)((JArray)runVariables["activeOperationVariables"])[0])[
            "remainingDays"] = 2;
        Require(RejectCheckpoint(OwnerId, durationTamper, "duration-tamper"),
            "A run-variable checkpoint with changed duration was accepted.");

        JObject identityTamper = (JObject)activeCheckpoint.DeepClone();
        identityTamper["ownerId"] = "run-owner:causal-operation-variable:tampered";
        Require(RejectCheckpoint(OwnerId, identityTamper, "identity-tamper"),
            "A run-variable checkpoint with changed owner identity was accepted.");

        if (log)
        {
            Debug.Log(
                "Run operation variable activation -> restored expiration causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Run-variable causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-operation-variable:" + suffix,
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
        private readonly string ledgerRunId;
        private readonly GameObject root;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly DomainFactDispatcher runVariableFacts;
        private readonly RunVariableRuntime runtime;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":run-variable-ledger";
            outcomes = new MigratedProducerOutcomeEditorFixture(ledgerRunId);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            runVariableFacts = outcomes.CreateRunVariableFacts(probe);
            root = new GameObject("Causal run operation variable owner");
            runtime = root.AddComponent<RunVariableRuntime>();
            runtime.Construct(
                EmptyOwnerRunDataProvider.Instance,
                EditorRuntimeReferenceFixtures.Invasion,
                EmptyRunStartVariableSelector.Instance,
                new RandomStreamProvider(RunSeed),
                new GameEventBus(),
                new SingleRunVariableCatalog(new RunVariableDefinition(
                    VariableId,
                    RunVariableCategory.Operation,
                    "집중 경계 태세",
                    "하루 동안 경계 태세를 유지한다.",
                    EventAlertImportance.Low,
                    1,
                    Array.Empty<IRunVariableEffect>())),
                EmptyOwnerDoctrineCatalog.Instance,
                outcomes.AggregateRootStore,
                runVariableFacts,
                runVariableFacts);
            try
            {
                if (checkpoint == null)
                {
                    runtime.StartRun(
                        RunSeed,
                        null,
                        InvasionThreatDifficulty.Normal);
                }
                else
                {
                    RestoreOwnerCheckpoint(checkpoint);
                }
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
            DungeonRunVariableSaveData save = runtime.CaptureForSave();
            DungeonActiveRunVariableSaveData active =
                save.activeOperationVariables.SingleOrDefault();
            return new Dictionary<string, object>
            {
                ["runVariable.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "run-owner:ephemeral-operation-variable",
                ["runVariable.stage"] = Stage,
                ["runVariable.runSeed"] = save.runSeed,
                ["runVariable.currentDay"] = save.currentDay,
                ["runVariable.activeCount"] = save.activeOperationVariables.Count,
                ["runVariable.activeVariableId"] =
                    active?.definitionId ?? "none",
                ["runVariable.remainingDays"] = active?.remainingDays ?? 0,
                ["runVariable.instanceSequence"] =
                    active?.instanceSequence ?? 0L,
                ["runVariable.nextOperationSequence"] =
                    save.nextOperationSequence,
                ["runVariable.lastOperationAdvanceDay"] =
                    save.lastOperationAdvanceDay,
                ["runVariable.activationOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.RunVariableActivatedEvent),
                ["runVariable.expirationOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind.RunVariableExpiredEvent),
                ["runVariable.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool Activate()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            ActiveRunVariable active = runtime.ActivateOperationVariable(
                VariableId,
                ActivationDay,
                alert: false);
            bool valid = active != null
                && string.Equals(active.Definition.id, VariableId,
                    StringComparison.Ordinal)
                && Stage == 1;
            if (valid)
                ValidateState();
            return valid;
        }

        internal bool Expire()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            runtime.OnTriggerEvent(new OperatingDayEndedEvent(ActivationDay));
            bool valid = Stage == 2;
            if (valid)
                ValidateState();
            return valid;
        }

        internal GameplayOutcomeReplayIdentity RequireActivatedOutcome() =>
            RequireOutcome(MigratedProducerOutcomeKind.RunVariableActivatedEvent);

        internal GameplayOutcomeReplayIdentity RequireExpiredOutcome() =>
            RequireOutcome(MigratedProducerOutcomeKind.RunVariableExpiredEvent);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
                throw new InvalidOperationException(
                    "Ephemeral run-variable fixtures have no checkpoint.");
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
                    ["runSeed"] = RunSeed,
                    ["variableId"] = VariableId,
                    ["runVariables"] = JObject.FromObject(
                        runtime.CaptureForSave()),
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
            runVariableFacts?.Dispose();
            if (root != null)
                Object.DestroyImmediate(root);
        }

        private int Stage
        {
            get
            {
                int activated = OutcomeCount(
                    MigratedProducerOutcomeKind.RunVariableActivatedEvent);
                int expired = OutcomeCount(
                    MigratedProducerOutcomeKind.RunVariableExpiredEvent);
                int active = runtime.CaptureForSave().activeOperationVariables.Count;
                if (activated == 0 && expired == 0 && active == 0)
                    return 0;
                if (activated == 1 && expired == 0 && active == 1)
                    return 1;
                if (activated == 1 && expired == 1 && active == 0)
                    return 2;
                return -1;
            }
        }

        private GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    kind,
                    probe);
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact.absoluteDay != ActivationDay
                || exact.status != GameplayOutcomeStatus.Succeeded
                || exact.participants == null
                || exact.participants.Count != 1
                || exact.subjects == null
                || exact.subjects.Count != 1
                || !string.Equals(
                    exact.participants[0].entityId,
                    "run-variable:" + RunSeed + ":1",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Run-variable production entry did not publish its exact outcome: "
                    + kind);
            }
            return identity;
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
                "run-variable owner state",
                "schemaVersion",
                "schema",
                "stage",
                "runSeed",
                "variableId",
                "runVariables",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals((string)state["schema"], DomainStateSchema,
                    StringComparison.Ordinal)
                || (int)state["runSeed"] != RunSeed
                || !string.Equals((string)state["variableId"], VariableId,
                    StringComparison.Ordinal)
                || state["runVariables"] is not JObject runVariableJson
                || state["outcomeOwners"] is not JObject ownerJson)
            {
                throw new InvalidOperationException(
                    "Run-variable checkpoint identity changed.");
            }

            DungeonMigratedProducerOutcomeSaveData ownerData =
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    ownerJson.ToString(Formatting.None));
            outcomes.Transaction.Restore(
                outcomes.Transaction.PrepareRestore(ownerData));
            DungeonRunVariableSaveData runVariables =
                JsonUtility.FromJson<DungeonRunVariableSaveData>(
                    runVariableJson.ToString(Formatting.None));
            if (runVariables == null
                || runVariables.version != DungeonRunVariableSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "Run-variable checkpoint save version is incompatible.");
            }
            runtime.RestoreFromSave(runVariables);
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Run-variable checkpoint stage disagrees with its saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            DungeonRunVariableSaveData save = runtime.CaptureForSave();
            DungeonActiveRunVariableSaveData active =
                save.activeOperationVariables.SingleOrDefault();
            bool valid = stage is >= 0 and <= 2
                && save.version == DungeonRunVariableSaveData.CurrentVersion
                && save.runSeed == RunSeed
                && save.currentDay == 1
                && save.hasStartVariables
                && save.nextOperationSequence == (stage == 0 ? 1L : 2L)
                && save.lastOperationAdvanceDay == (stage == 2 ? 1 : 0)
                && ExactOutcomeCount() == stage;
            if (stage == 1)
            {
                valid &= active != null
                    && string.Equals(active.definitionId, VariableId,
                        StringComparison.Ordinal)
                    && active.startDay == ActivationDay
                    && active.remainingDays == 1
                    && active.instanceSequence == 1L;
            }
            else
            {
                valid &= active == null;
            }
            if (!valid)
            {
                throw new InvalidOperationException(
                    "Run-variable state, outcome owner and exact ledger diverged.");
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
            this.fixtures = fixtures ?? throw new ArgumentNullException(nameof(fixtures));

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

    private sealed class SingleRunVariableCatalog : IRunVariableDefinitionCatalog
    {
        private readonly RunVariableDefinition definition;

        internal SingleRunVariableCatalog(RunVariableDefinition definition) =>
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));

        public IReadOnlyCollection<RunVariableDefinition> All =>
            new[] { definition };
        public RunVariableDefinition Get(string id) =>
            string.Equals(id, definition.id, StringComparison.Ordinal)
                ? definition
                : null;
        public RunVariableDefinition Require(string id) => Get(id)
            ?? throw new KeyNotFoundException(id);
        public IReadOnlyList<RunVariableDefinition> GetByCategory(
            RunVariableCategory category) => definition.category == category
                ? new[] { definition }
                : Array.Empty<RunVariableDefinition>();
    }

    private sealed class EmptyOwnerRunDataProvider : IOwnerRunDataProvider
    {
        internal static readonly EmptyOwnerRunDataProvider Instance = new();
        public CharacterSO SelectedOwnerData => null;
    }

    private sealed class EmptyRunStartVariableSelector : IRunStartVariableSelector
    {
        internal static readonly EmptyRunStartVariableSelector Instance = new();

        public RunStartVariableSnapshot Create(
            int seed,
            CharacterSO ownerData,
            DungeonDifficulty difficulty,
            DungeonSurvivalPressure survivalPressure =
                DungeonSurvivalPressure.Standard) => new(
            seed,
            "Unknown",
            difficulty,
            Array.Empty<int>(),
            Array.Empty<string>(),
            Array.Empty<int>(),
            seed,
            "causal-run-variable",
            1f,
            survivalPressure: survivalPressure);
    }

    private sealed class EmptyOwnerDoctrineCatalog : IOwnerDoctrineDefinitionCatalog
    {
        internal static readonly EmptyOwnerDoctrineCatalog Instance = new();
        public IReadOnlyCollection<OwnerDoctrineDefinition> All =>
            Array.Empty<OwnerDoctrineDefinition>();
        public OwnerDoctrineDefinition Get(string id) => null;
        public OwnerDoctrineDefinition Require(string id) =>
            throw new KeyNotFoundException(id);
        public OwnerDoctrineDefinition ResolveFor(CharacterSO owner) => null;
        public OwnerDoctrineDefinition ResolveForSpecies(string speciesTag) => null;
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
