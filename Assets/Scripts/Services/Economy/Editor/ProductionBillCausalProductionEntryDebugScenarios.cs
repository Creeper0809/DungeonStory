#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class ProductionPreparedOutputFullPersistenceDebugScenarios
{
    internal const string CausalProductionOwnerRestoreKind =
        "owner-family:production-bill-cycle";
    internal const string CausalProductionCommandActionId =
        "action:production-command-add-bill";
    internal const string CausalProductionCompletionActionId =
        "action:production-cycle-complete";

    private const string CausalProductionOwnerKind =
        "owner-kind:production-bill-cycle";
    private const string CausalProductionStateSchema =
        "dungeonstory-causal-production-bill-owner-state@1";

    internal static IReadOnlyList<IProductionCausalActionFixture>
        CreateCausalProductionFixtures(
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
                "A production checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new CausalProductionContext(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            2,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                CausalProductionCommandActionId,
                "production",
                ProductionCommandOutcomeIds.Applied,
                context.CaptureState,
                context.ApplyCommand,
                context.RequireCommandOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalProductionOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null),
            new DelegateProductionCausalActionFixture(
                CausalProductionCompletionActionId,
                "production",
                ProductionCombatOutcomeIds.ProductionCompleted,
                () => new Dictionary<string, object>(),
                context.CompleteCycle,
                context.RequireCompletionOutcome,
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                context.PersistentOwnerId,
                context.PersistentOwnerKind,
                context.HasPersistentOwner
                    ? context.CaptureOwnerCheckpoint
                    : null,
                context.HasPersistentOwner
                    ? CausalProductionOwnerRestoreKind
                    : null,
                context.HasPersistentOwner ? context : null)
        };
    }

    public static bool RunCausalProductionOwnerFamily(bool log = false)
    {
        const string OwnerId = "owner:causal-production-bill:focused";
        JObject commandCheckpoint;
        using (var fixtures = new CausalProductionFixtureSet(
                   CreateCausalProductionFixtures(
                       "run:causal-production-bill:command",
                       OwnerId)))
        {
            IProductionCausalActionFixture command = fixtures.Get(
                CausalProductionCommandActionId);
            IReadOnlyDictionary<string, object> before = command.CaptureState();
            Require((int)before["production.billCount"] == 0
                    && (long)before["production.commandSequence"] == 1L
                    && !(bool)before["production.commandApplied"],
                "The production owner did not begin without a bill.");
            Require(command.Execute(),
                "The real production bill command did not add a bill.");
            Require(command.RequireExactOutcome().HasCanonicalPayloadHash,
                "The production command has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = command.CaptureState();
            Require((int)after["production.billCount"] == 1
                    && (long)after["production.commandSequence"] == 2L
                    && (bool)after["production.commandApplied"]
                    && !(bool)after["production.completed"],
                "The production command did not retain one exact pending cycle.");
            commandCheckpoint = ((IProductionCausalOwnerCheckpointFixture)command)
                .CaptureOwnerCheckpoint();
        }

        JObject completedCheckpoint;
        using (var fixtures = new CausalProductionFixtureSet(
                   CreateCausalProductionFixtures(
                       "run:causal-production-bill:completion",
                       OwnerId,
                       commandCheckpoint)))
        {
            IProductionCausalActionFixture state = fixtures.Get(
                CausalProductionCommandActionId);
            IProductionCausalActionFixture completion = fixtures.Get(
                CausalProductionCompletionActionId);
            IReadOnlyDictionary<string, object> restored = state.CaptureState();
            Require((int)restored["production.billCount"] == 1
                    && (bool)restored["production.commandApplied"]
                    && (int)restored["production.outputQuantity"] == 0,
                "Fresh production restore lost the commanded bill.");
            Require(completion.Execute(),
                "The real production work entry did not complete its cycle.");
            Require(completion.RequireExactOutcome().HasCanonicalPayloadHash,
                "The production completion has no canonical payload hash.");
            IReadOnlyDictionary<string, object> after = state.CaptureState();
            Require((bool)after["production.completed"]
                    && (int)after["production.outputQuantity"]
                        == SawmillScenario.ExpectedOutputQuantity
                    && (long)after["production.outputMassGrams"]
                        == SawmillScenario.ExpectedBatchMassGrams,
                "Production completion did not retain the exact physical output.");
            completedCheckpoint = ((IProductionCausalOwnerCheckpointFixture)completion)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new CausalProductionFixtureSet(
                   CreateCausalProductionFixtures(
                       "run:causal-production-bill:terminal-restore",
                       OwnerId,
                       completedCheckpoint)))
        {
            IProductionCausalActionFixture command = fixtures.Get(
                CausalProductionCommandActionId);
            IProductionCausalActionFixture completion = fixtures.Get(
                CausalProductionCompletionActionId);
            IReadOnlyDictionary<string, object> restored = command.CaptureState();
            Require((bool)restored["production.commandApplied"]
                    && (bool)restored["production.completed"]
                    && (int)restored["production.outputQuantity"]
                        == SawmillScenario.ExpectedOutputQuantity,
                "Completed production did not survive a second fresh restore.");
            Require(!command.Execute() && !completion.Execute(),
                "Completed production accepted a duplicate command or cycle.");
        }

        JObject tampered = (JObject)completedCheckpoint.DeepClone();
        ((JObject)tampered["domainState"])["billId"] =
            "production-bill:tampered";
        bool tamperRejected = false;
        try
        {
            using var ignored = new CausalProductionFixtureSet(
                CreateCausalProductionFixtures(
                    "run:causal-production-bill:tampered",
                    OwnerId,
                    tampered));
        }
        catch (InvalidOperationException)
        {
            tamperRejected = true;
        }
        Require(tamperRejected,
            "A production checkpoint with a different bill identity was accepted.");

        if (log)
            Debug.Log("Production command -> cycle completion causal owner family PASS");
        return true;
    }

    public static void RunCausalProductionOwnerFamilyBatch()
    {
        if (!RunCausalProductionOwnerFamily(log: true))
        {
            throw new InvalidOperationException(
                "Production causal owner-family batch returned false.");
        }
    }

    private sealed class CausalProductionContext : IDisposable
    {
        private readonly string ownerId;
        private readonly string facilityId;
        private readonly string workerId;
        private readonly RuntimeGraph graph;
        private string billId = string.Empty;
        private bool commandApplied;
        private bool completed;
        private bool disposed;

        internal CausalProductionContext(
            string causalRunId,
            string ownerId,
            JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string stableSeed = HasPersistentOwner ? this.ownerId : causalRunId;
            string scope = ProductionCausalOutcomeEvidence.Sha256Text(stableSeed)
                .Substring("sha256:".Length, 24);
            facilityId = "building:causal-production:" + scope;
            workerId = "character:causal-production:" + scope;
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    CausalProductionOwnerKind)
                : causalRunId + ":production-ledger";
            var outcomes = new ProductionOwnerFamilyOutcomeFixture(ledgerRunId);
            var config = new ScenarioConfig(
                "causal-production-" + scope,
                SawmillScenario.RecipeId,
                SawmillScenario.ItemId,
                facilityId,
                workerId,
                SawmillScenario.BuildingAssetPath,
                SawmillScenario.ExpectedOutputQuantity,
                SawmillScenario.ExpectedBatchMassGrams,
                SawmillScenario.ExpectedCapacityGrams,
                SawmillScenario.ExpectedInitialFreshnessSeconds);
            graph = new RuntimeGraph(
                "Causal Production " + scope,
                7301,
                config,
                ownerFamilyOutcomes: outcomes,
                persistentIds: new CausalProductionDeterministicIds(scope));
            try
            {
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                ValidateState();
            }
            catch
            {
                graph.Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner
            ? CausalProductionOwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            DungeonProductionBillSaveData production = graph.Production.Capture();
            ProductionBillSaveData bill = production.bills.SingleOrDefault(value =>
                value != null
                && string.Equals(value.billId, billId, StringComparison.Ordinal));
            WorldItemStackSnapshot output = FindOutput();
            long outputMass = output == null
                ? 0L
                : graph.Mass.GetQuantityMass(
                    (ItemDefinitionId)graph.Config.ItemId,
                    PhysicalItemMassSubjectAdapter.Create(
                        graph.Mass,
                        (ItemDefinitionId)graph.Config.ItemId,
                        output.ItemInstanceId,
                        output.Components),
                    output.Quantity).Value;
            return new Dictionary<string, object>
            {
                ["production.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-production-bill",
                ["production.facilityId"] = facilityId,
                ["production.workerId"] = workerId,
                ["production.recipeId"] = graph.Config.RecipeId,
                ["production.billId"] = billId.Length == 0 ? "none" : billId,
                ["production.billCount"] = production.bills.Count,
                ["production.commandSequence"] =
                    production.nextCommandSequence,
                ["production.cycleSequence"] = bill?.cycleSequence ?? 0,
                ["production.remainingCycles"] = bill?.remainingCycles ?? 0,
                ["production.completedWork"] = bill?.completedWork ?? 0f,
                ["production.preparedOutputPhase"] = bill?.preparedOutput == null
                    ? "none"
                    : bill.preparedOutput.phase.ToString(),
                ["production.outputQuantity"] = output?.Quantity ?? 0,
                ["production.outputMassGrams"] = outputMass,
                ["production.commandApplied"] = commandApplied,
                ["production.completed"] = completed
            };
        }

        internal bool ApplyCommand()
        {
            ThrowIfDisposed();
            if (commandApplied || completed || graph.Production.Capture().bills.Count != 0)
                return false;
            ProductionBillCommandResult result = graph.Production.AddBill(
                graph.FacilityHandle,
                graph.Recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                1);
            if (!result.Succeeded)
                return false;
            billId = result.BillId.Value;
            if (!HasCanonicalPayload(RequireCommandOutcome(false)))
            {
                throw new InvalidOperationException(
                    "Production command mutated its owner without an exact outcome.");
            }
            commandApplied = true;
            ValidateState();
            return true;
        }

        internal bool CompleteCycle()
        {
            ThrowIfDisposed();
            if (!commandApplied || completed || billId.Length == 0)
                return false;
            ProductionBillRecord record = graph.State.Bills.SingleOrDefault(value =>
                value != null && string.Equals(
                    value.billId.Value,
                    billId,
                    StringComparison.Ordinal));
            if (record == null || record.cycleSequence != 1)
                return false;
            graph.PrepareWip(record);
            ProductionWorkExecutionResult result = graph.Production.ExecuteWork(
                graph.WorkerHandle,
                graph.FacilityHandle,
                record.billId,
                float.MaxValue);
            if (!result.Succeeded
                || !result.CycleCompleted
                || result.Outcome != ProductionBillOutcomeCode.CycleCompleted)
            {
                return false;
            }
            if (!HasCanonicalPayload(RequireCompletionOutcome(false)))
            {
                throw new InvalidOperationException(
                    "Production cycle mutated its owner without an exact completion outcome.");
            }
            completed = true;
            ValidateState();
            return true;
        }

        internal GameplayOutcomeReplayIdentity RequireCommandOutcome() =>
            RequireCommandOutcome(true);

        internal GameplayOutcomeReplayIdentity RequireCompletionOutcome() =>
            RequireCompletionOutcome(true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(
                graph.CompletedOutcome.Ledger,
                identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "This production context has no persistent owner.");
            }
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                CausalProductionOwnerKind,
                CausalProductionOwnerRestoreKind,
                SerializeCausalProductionState(
                    CaptureDomainState(),
                    "production bill state"),
                graph.CompletedOutcome.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            graph.Dispose();
        }

        private GameplayOutcomeReplayIdentity RequireCommandOutcome(
            bool throwOnFailure)
        {
            GameplayResultKey key = ProductionCommandOutcomeIds.ResultKey(1L);
            return RequireExact(
                key,
                ProductionCommandOutcomeIds.Applied,
                throwOnFailure);
        }

        private GameplayOutcomeReplayIdentity RequireCompletionOutcome(
            bool throwOnFailure)
        {
            var key = new GameplayResultKey(
                ProductionCombatOutcomeIds.ProductionProducerId,
                new GameplayOperationId(
                    "production-cycle:" + billId + ":1"),
                1L,
                0);
            return RequireExact(
                key,
                ProductionCombatOutcomeIds.ProductionCompleted,
                throwOnFailure);
        }

        private GameplayOutcomeReplayIdentity RequireExact(
            GameplayResultKey key,
            GameplayOutcomeTypeId expectedType,
            bool throwOnFailure)
        {
            GameplayOutcomeLedger ledger = graph.CompletedOutcome.Ledger;
            bool valid = ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash
                && ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact != null
                && string.Equals(
                    exact.outcomeTypeId,
                    expectedType.Value,
                    StringComparison.Ordinal);
            if (!valid && throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Causal production entry did not publish exact outcome "
                    + expectedType.Value + ".");
            }
            return valid ? identity : default;
        }

        private CausalProductionStateSaveData CaptureDomainState() => new()
        {
            schemaVersion = 1,
            schema = CausalProductionStateSchema,
            facilityId = facilityId,
            workerId = workerId,
            recipeId = graph.Config.RecipeId,
            billId = billId,
            sections = graph.Registry.CaptureAll()
                .Select(CloneEnvelope)
                .ToList(),
            commandApplied = commandApplied,
            completed = completed
        };

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                CausalProductionOwnerKind,
                CausalProductionOwnerRestoreKind,
                graph.CompletedOutcome.Ledger);
            CausalProductionStateSaveData saved = DeserializeCausalProductionState<
                CausalProductionStateSaveData>(state, "production bill state");
            if (saved.schemaVersion != 1
                || !string.Equals(
                    saved.schema,
                    CausalProductionStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals(saved.facilityId, facilityId, StringComparison.Ordinal)
                || !string.Equals(saved.workerId, workerId, StringComparison.Ordinal)
                || !string.Equals(
                    saved.recipeId,
                    graph.Config.RecipeId,
                    StringComparison.Ordinal)
                || saved.sections == null
                || saved.sections.Count == 0
                || saved.commandApplied && string.IsNullOrWhiteSpace(saved.billId)
                || saved.completed && !saved.commandApplied)
            {
                throw new InvalidOperationException(
                    "Causal production owner identity or state header changed.");
            }

            var report = new DungeonGameRestoreReport();
            bool restored = graph.Registry.RestoreAll(
                saved.sections.Select(CloneEnvelope).ToList(),
                report);
            if (!restored || !report.Success)
            {
                throw new InvalidOperationException(
                    "Causal production aggregate restore was rejected: "
                    + string.Join(" | ", report.Errors));
            }
            billId = saved.billId ?? string.Empty;
            commandApplied = saved.commandApplied;
            completed = saved.completed;
        }

        private void ValidateState()
        {
            if (!string.Equals(
                    graph.CurrentFacility.RequirePersistentInstanceId().Value,
                    facilityId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    CharacterPersistentIdentity.Require(graph.Worker).Value,
                    workerId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Causal production facility or worker identity changed.");
            }

            DungeonProductionBillSaveData production = graph.Production.Capture();
            bool hasBill = production.bills.Any(value => value != null
                && string.Equals(value.billId, billId, StringComparison.Ordinal));
            WorldItemStackSnapshot output = FindOutput();
            bool commandTruth = production.nextCommandSequence == 2L
                && billId.Length > 0
                && hasBill
                && HasCanonicalPayload(RequireCommandOutcome(false));
            bool completionTruth = output != null
                && output.Quantity == graph.Config.ExpectedOutputQuantity
                && output.State == WorldItemStackState.FacilityOutputBuffer
                && HasCanonicalPayload(RequireCompletionOutcome(false));
            int expectedLedgerCount = (commandApplied ? 1 : 0)
                + (completed ? 1 : 0);
            int actualLedgerCount = graph.CompletedOutcome.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(8),
                OutcomeFilter.All).Items.Count;
            if (commandApplied != commandTruth
                || completed != completionTruth
                || actualLedgerCount != expectedLedgerCount
                || !commandApplied && (production.bills.Count != 0
                    || production.nextCommandSequence != 1L
                    || billId.Length != 0
                    || output != null)
                || commandApplied && !completed && production.bills.Count != 1
                || completed && !commandApplied)
            {
                throw new InvalidOperationException(
                    "Causal production flags disagree with production, physical, or ledger authority.");
            }
        }

        private WorldItemStackSnapshot FindOutput()
        {
            WorldItemStackSnapshot[] outputs = graph.WorldItems.GetAllStacks()
                .Where(value => value != null
                    && string.Equals(
                        value.ItemId,
                        graph.Config.ItemId,
                        StringComparison.Ordinal))
                .ToArray();
            if (outputs.Length > 1)
            {
                throw new InvalidOperationException(
                    "Causal production created more than one output stack.");
            }
            return outputs.SingleOrDefault();
        }

        private static bool HasCanonicalPayload(
            GameplayOutcomeReplayIdentity identity) =>
            identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
            && !string.IsNullOrEmpty(identity.CanonicalPayloadHash)
            && identity.CanonicalPayloadHash.Length == 64;

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    internal sealed class ProductionOwnerFamilyOutcomeFixture
    {
        internal ProductionOwnerFamilyOutcomeFixture(string runId)
        {
            var josa = new KoreanJosaFormatter();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new ProductionCommandOutcomeDescriptor(josa),
                    new ProductionCompletedOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new ProductionCommandOutcomeAdapter(),
                    new ProductionCompletedOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            GameplayDomainFactCommitGroup commits = new(
                Recorder,
                Ledger);
            CommandFacts = new DomainFactDispatcher();
            CommandFacts.Subscribe(
                "production.command-observer",
                new GameplayDomainFactObserver<
                    ProductionCommandAppliedFact,
                    ProductionCommandOutcomeReceipt>(
                    ProductionCommandFactObservation.Capture,
                    Registry,
                    Ledger,
                    Recorder,
                    commits));
            CommandFacts.Subscribe(
                "production.completion-observer",
                new GameplayDomainFactObserver<
                    ProductionCycleCompletedFact,
                    ProductionCompletedOutcomeReceipt>(
                    ProductionCompletionFactObservation.Capture,
                    Registry,
                    Ledger,
                    Recorder,
                    commits));
            CompletionFacts = CommandFacts;
            CompletedOutcome = new ProductionCompletedOutcomeFixture(
                Ledger,
                CompletionFacts);
        }

        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal DomainFactDispatcher CommandFacts { get; }
        internal DomainFactDispatcher CompletionFacts { get; }
        internal ProductionCompletedOutcomeFixture CompletedOutcome { get; }
    }

    [Serializable]
    private sealed class CausalProductionStateSaveData
    {
        public int schemaVersion;
        public string schema = string.Empty;
        public string facilityId = string.Empty;
        public string workerId = string.Empty;
        public string recipeId = string.Empty;
        public string billId = string.Empty;
        public List<DungeonSaveSectionEnvelope> sections = new();
        public bool commandApplied;
        public bool completed;
    }

    private sealed class CausalProductionFixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal CausalProductionFixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
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

    private sealed class CausalProductionDeterministicIds :
        IPersistentIdGenerator
    {
        private readonly string scope;
        private int itemInstanceSequence;
        private int itemStackSequence;
        private int characterSequence;
        private int buildingSequence;
        private int wildlifeSequence;

        internal CausalProductionDeterministicIds(string scope) =>
            this.scope = scope;

        public ItemInstanceId NewItemInstanceId() => new(
            "item-instance:causal-production:" + scope + ":"
            + (++itemInstanceSequence).ToString("D4"));

        public ItemStackId NewItemStackId() => new(
            "stack:causal-production:" + scope + ":"
            + (++itemStackSequence).ToString("D4"));

        public CharacterId NewCharacterId() => new(
            "character:causal-production:" + scope + ":"
            + (++characterSequence).ToString("D4"));

        public BuildingInstanceId NewBuildingInstanceId() => new(
            "building:causal-production:" + scope + ":"
            + (++buildingSequence).ToString("D4"));

        public WildlifeHabitatPatchId NewWildlifeHabitatPatchId() => new(
            "wildlife-habitat:causal-production:" + scope + ":"
            + (++wildlifeSequence).ToString("D4"));
    }

    private static JObject SerializeCausalProductionState(
        object value,
        string label)
    {
        try
        {
            return JObject.Parse(JsonUtility.ToJson(
                value ?? throw new ArgumentNullException(nameof(value))));
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException)
        {
            throw new InvalidOperationException(
                "Could not serialize causal " + label + ".",
                exception);
        }
    }

    private static T DeserializeCausalProductionState<T>(
        JToken token,
        string label)
        where T : class
    {
        T value = JsonUtility.FromJson<T>(
            (token ?? throw new InvalidOperationException(
                "Causal " + label + " is missing."))
            .ToString(Formatting.None));
        return value ?? throw new InvalidOperationException(
            "Causal " + label + " is invalid.");
    }
}
#endif
