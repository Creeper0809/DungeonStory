#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class TradeInventoryCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:trade-inventory-three-producer";
    internal const string RelocateActionId =
        "action:trade-inventory-relocate-buffer";
    internal const string DisposeActionId =
        "action:trade-inventory-dispose-relocated-buffer";
    internal const string WastePolicyActionId =
        "action:trade-inventory-set-plant-compost-policy";

    private const string OwnerKind =
        "owner-kind:trade-inventory-three-producer";
    private const string DomainStateSchema =
        "dungeonstory-causal-trade-inventory-owner-state@1";
    private const string ItemId = "item:buffer";
    private const string RelocationProducerId =
        "trade-inventory.physical-item-relocation-receipt";
    private const string DispositionProducerId =
        "trade-inventory.physical-item-batch-disposition-receipt";
    private const string WastePolicyProducerId =
        "trade-inventory.waste-policy-command-result";
    private const string RelocationOperationId =
        "causal:trade-inventory:relocate-buffer";
    private const string DispositionOperationId =
        "causal:trade-inventory:dispose-relocated-buffer";
    private const string WastePolicyOperationId = "waste-policy:1:command:1";
    private const int AbsoluteDay = 36;
    private static readonly Vector2Int SourcePosition = new(2, 3);
    private static readonly Vector2Int DestinationPosition = new(8, 9);

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
        {
            throw new ArgumentException(
                "A causal run ID is required.", nameof(causalRunId));
        }
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A trade/inventory checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            3,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            Fixture(
                RelocateActionId,
                context,
                context.CaptureState,
                context.RelocateBuffer,
                context.RequireRelocationOutcome,
                lifetime),
            Fixture(
                DisposeActionId,
                context,
                () => new Dictionary<string, object>(),
                context.DisposeRelocatedBuffer,
                context.RequireDispositionOutcome,
                lifetime),
            Fixture(
                WastePolicyActionId,
                context,
                () => new Dictionary<string, object>(),
                context.SetPlantCompostPolicy,
                context.RequireWastePolicyOutcome,
                lifetime)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string ownerId = "owner:causal-trade-inventory:focused";
        JObject relocationCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-trade-inventory:relocation",
                   ownerId)))
        {
            IProductionCausalActionFixture relocation = fixtures.Get(
                RelocateActionId);
            Require((int)relocation.CaptureState()["tradeInventory.stage"] == 0,
                "Trade/inventory owner did not start at stage zero.");
            Require(relocation.Execute(),
                "The real physical relocation entry was rejected.");
            Require(relocation.RequireExactOutcome().HasCanonicalPayloadHash,
                "The relocation result has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = relocation.CaptureState();
            Require(
                (int)after["tradeInventory.stage"] == 1
                && (int)after["tradeInventory.sourceQuantity"] == 2
                && (int)after["tradeInventory.relocatedQuantity"] == 1
                && (int)after["tradeInventory.exactOutcomeCount"] == 1,
                "Relocation did not retain its physical split and exact result.");
            relocationCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)relocation)
                .CaptureOwnerCheckpoint();
        }

        JObject dispositionCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-trade-inventory:disposition",
                   ownerId,
                   relocationCheckpoint)))
        {
            IProductionCausalActionFixture relocation = fixtures.Get(
                RelocateActionId);
            IProductionCausalActionFixture disposition = fixtures.Get(
                DisposeActionId);
            Require(
                (int)relocation.CaptureState()["tradeInventory.stage"] == 1
                && !relocation.Execute(),
                "Fresh composition restore lost or replayed relocation.");
            Require(disposition.Execute(),
                "The real physical batch-disposition entry was rejected.");
            Require(disposition.RequireExactOutcome().HasCanonicalPayloadHash,
                "The disposition result has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = relocation.CaptureState();
            Require(
                (int)after["tradeInventory.stage"] == 2
                && (int)after["tradeInventory.sourceQuantity"] == 2
                && (int)after["tradeInventory.relocatedQuantity"] == 0
                && (int)after["tradeInventory.exactOutcomeCount"] == 2,
                "Disposition did not preserve the source stack and prior result.");
            dispositionCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)disposition)
                .CaptureOwnerCheckpoint();
        }

        JObject terminalCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-trade-inventory:waste-policy",
                   ownerId,
                   dispositionCheckpoint)))
        {
            IProductionCausalActionFixture relocation = fixtures.Get(
                RelocateActionId);
            IProductionCausalActionFixture disposition = fixtures.Get(
                DisposeActionId);
            IProductionCausalActionFixture policy = fixtures.Get(
                WastePolicyActionId);
            Require(
                (int)relocation.CaptureState()["tradeInventory.stage"] == 2
                && !relocation.Execute()
                && !disposition.Execute(),
                "Fresh composition restore lost or replayed physical results.");
            Require(policy.Execute(),
                "The real waste-policy production entry was rejected.");
            Require(policy.RequireExactOutcome().HasCanonicalPayloadHash,
                "The waste-policy result has no canonical outcome hash.");
            IReadOnlyDictionary<string, object> after = relocation.CaptureState();
            Require(
                (int)after["tradeInventory.stage"] == 3
                && (string)after["tradeInventory.wasteDisposition"] == "Compost"
                && !(bool)after["tradeInventory.wasteEnabled"]
                && Mathf.Approximately(
                    Convert.ToSingle(after["tradeInventory.wasteMaximumPercent"]),
                    40f)
                && (int)after["tradeInventory.exactOutcomeCount"] == 3,
                "Waste policy did not preserve both physical results.");
            terminalCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)policy)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-trade-inventory:terminal",
                   ownerId,
                   terminalCheckpoint)))
        {
            JObject before = ((IProductionCausalOwnerCheckpointFixture)
                fixtures.Get(RelocateActionId)).CaptureOwnerCheckpoint();
            Require(
                !fixtures.Get(RelocateActionId).Execute()
                && !fixtures.Get(DisposeActionId).Execute()
                && !fixtures.Get(WastePolicyActionId).Execute(),
                "Terminal trade/inventory owner was not idempotent.");
            JObject after = ((IProductionCausalOwnerCheckpointFixture)
                fixtures.Get(RelocateActionId)).CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected trade/inventory replay mutated the checkpoint.");
        }

        JObject physicalTamper = (JObject)relocationCheckpoint.DeepClone();
        ((JObject)physicalTamper["domainState"])["relocatedStackId"] =
            "stack:tampered-relocation";
        Require(RejectCheckpoint(ownerId, physicalTamper, "physical-tamper"),
            "A physical-stack identity mismatch was accepted.");

        JObject wasteTamper = (JObject)relocationCheckpoint.DeepClone();
        ((JObject)((JObject)wasteTamper["domainState"])["wasteState"])
            ["nextPolicySequence"] = 2;
        Require(RejectCheckpoint(ownerId, wasteTamper, "waste-tamper"),
            "A waste-policy sequence mismatch was accepted.");

        JObject ledgerTamper = (JObject)relocationCheckpoint.DeepClone();
        ((JArray)((JObject)ledgerTamper["outcomeLedger"])["exactOutcomes"])
            .Clear();
        Require(RejectCheckpoint(ownerId, ledgerTamper, "ledger-tamper"),
            "A physical-state/exact-ledger mismatch was accepted.");

        if (log)
        {
            Debug.Log(
                "Trade/inventory relocation -> disposition -> waste policy causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Trade/inventory causal owner family returned false.");
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
        Func<IReadOnlyDictionary<string, object>> capture,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime) =>
        new DelegateProductionCausalActionFixture(
            actionId,
            "trade-inventory",
            TradeInventoryOutcomeIds.Result,
            capture,
            execute,
            requireOutcome,
            context.CaptureOutcomeEvidence,
            lifetime.Release,
            context.PersistentOwnerId,
            context.PersistentOwnerKind,
            context.HasPersistentOwner
                ? context.CaptureOwnerCheckpoint
                : null,
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
                "run:causal-trade-inventory:" + suffix,
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
        private readonly GameplayOutcomeLedger ledger;
        private readonly GameplayOutcomeRecorder recorder;
        private readonly WorldItemStackRuntime items;
        private readonly WorldItemRepository repository;
        private readonly ItemQuantityReservationService quantityReservations;
        private readonly DomainFactDispatcher facts;
        private readonly FixedCalendar calendar;
        private readonly WasteProcessingRuntime waste;
        private string sourceStackId;
        private string relocatedStackId = string.Empty;
        private bool disposed;

        internal Context(
            string causalRunId,
            string ownerId,
            JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":trade-inventory-ledger";
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new TradeInventoryOutcomeDescriptor(
                        new DungeonStory.Narrative.Korean.KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new TradeInventoryOutcomeAdapter()
                });
            ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId(ledgerRunId),
                1L);
            recorder = new GameplayOutcomeRecorder(
                ledger,
                registry,
                new GameEventBus());
            calendar = new FixedCalendar(AbsoluteDay, 9);
            facts = new DomainFactDispatcher();
            facts.Subscribe(
                "physical-relocation",
                new GameplayDomainFactObserver<
                    PhysicalItemRelocatedFact,
                    TradeInventoryOutcomeReceipt>(
                    PhysicalItemRelocationFactObservation.Capture,
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger)));
            facts.Subscribe(
                "physical-disposition",
                new GameplayDomainFactObserver<
                    PhysicalItemDisposedFact,
                    TradeInventoryOutcomeReceipt>(
                    PhysicalItemDispositionFactObservation.Capture,
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger)));
            facts.Subscribe(
                "waste-policy",
                new GameplayDomainFactObserver<
                    WastePolicyChangedFact,
                    TradeInventoryOutcomeReceipt>(
                    WastePolicyFactObservation.Capture,
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger)));
            items = PhysicalItemDebugScenarios
                .CreateRuntimeForCrossDomainFixture(
                    out repository,
                    out _,
                    out quantityReservations,
                    out _);
            waste = CreateWasteRuntime(facts, facts, calendar);

            if (checkpoint == null)
            {
                sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
                    repository,
                    ItemId,
                    3,
                    WorldItemStackState.Loose,
                    position: SourcePosition);
            }
            else
            {
                RestoreOwnerCheckpoint(checkpoint);
            }
            ValidateState();
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
            WastePolicyData policy = waste.GetPolicy(WasteOriginKind.Plant);
            int relocationCount = ExactOutcomeCount(RelocationProducerId);
            int dispositionCount = ExactOutcomeCount(DispositionProducerId);
            int policyCount = ExactOutcomeCount(WastePolicyProducerId);
            return new Dictionary<string, object>
            {
                ["tradeInventory.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-trade-inventory",
                ["tradeInventory.stage"] = Stage,
                ["tradeInventory.absoluteDay"] = AbsoluteDay,
                ["tradeInventory.itemId"] = ItemId,
                ["tradeInventory.sourceStackId"] = sourceStackId,
                ["tradeInventory.relocatedStackId"] = relocatedStackId,
                ["tradeInventory.sourceQuantity"] = StackQuantity(sourceStackId),
                ["tradeInventory.relocatedQuantity"] =
                    StackQuantity(relocatedStackId),
                ["tradeInventory.stackCount"] = items.GetAllStacks().Count,
                ["tradeInventory.relocatedPresent"] =
                    StackQuantity(relocatedStackId) > 0,
                ["tradeInventory.sourceX"] = SourcePosition.x,
                ["tradeInventory.sourceY"] = SourcePosition.y,
                ["tradeInventory.destinationX"] = DestinationPosition.x,
                ["tradeInventory.destinationY"] = DestinationPosition.y,
                ["tradeInventory.wasteOrigin"] = policy.origin.ToString(),
                ["tradeInventory.wasteDisposition"] =
                    policy.disposition.ToString(),
                ["tradeInventory.wasteEnabled"] = policy.enabled,
                ["tradeInventory.wasteMaximumPercent"] =
                    policy.maximumFeedContamination,
                ["tradeInventory.wasteNextOutcomeSequence"] =
                    checked((int)waste.Capture().nextPolicySequence),
                ["tradeInventory.relocationOutcomeCount"] = relocationCount,
                ["tradeInventory.dispositionOutcomeCount"] = dispositionCount,
                ["tradeInventory.wastePolicyOutcomeCount"] = policyCount,
                ["tradeInventory.exactOutcomeCount"] =
                    relocationCount + dispositionCount + policyCount
            };
        }

        internal bool RelocateBuffer()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;
            var prepared = new PreparedPhysicalItemRelocationService(
                repository,
                items.MassQuery,
                items.CatalogProvider,
                EditorNullItemMarkerPresenter.Instance);
            var service = new PhysicalItemRelocationService(
                repository,
                prepared,
                facts,
                facts,
                calendar);
            bool succeeded = service.TryRelocateQuantity(
                sourceStackId,
                1,
                DestinationPosition,
                WorldItemStackState.Loose,
                string.Empty,
                RelocationOperationId,
                "causal-trade-inventory-relocation",
                out PhysicalItemRelocationReceipt receipt,
                out _);
            if (!succeeded || !receipt.IsCommitted)
                return false;
            relocatedStackId = receipt.DestinationStackId;
            ValidateState();
            return Stage == 1;
        }

        internal bool DisposeRelocatedBuffer()
        {
            ThrowIfDisposed();
            if (Stage != 1 || string.IsNullOrWhiteSpace(relocatedStackId))
                return false;
            var service = new PhysicalItemBatchDispositionService(
                repository,
                items.MassQuery,
                EditorNullItemMarkerPresenter.Instance,
                quantityReservations,
                items.CatalogProvider,
                facts,
                facts,
                new GameplayDomainFactDeliveryQuery(recorder, ledger),
                calendar);
            bool succeeded = service.TryCommit(
                new[] { new PhysicalItemTransformInput(relocatedStackId, 1) },
                PhysicalItemDispositionKind.Sink,
                DispositionOperationId,
                "causal-trade-inventory-disposition",
                out PhysicalItemBatchDispositionReceipt receipt,
                out _);
            if (!succeeded || !receipt.IsCommitted)
                return false;
            ValidateState();
            return Stage == 2;
        }

        internal bool SetPlantCompostPolicy()
        {
            ThrowIfDisposed();
            if (Stage != 2)
                return false;
            WastePolicyCommandResult result = waste.SetPolicy(
                new WastePolicyData
                {
                    origin = WasteOriginKind.Plant,
                    disposition = WasteDispositionKind.Compost,
                    enabled = false,
                    maximumFeedContamination = 40f
                });
            if (!result.Succeeded
                || !string.Equals(
                    result.OperationId,
                    WastePolicyOperationId,
                    StringComparison.Ordinal))
            {
                return false;
            }
            ValidateState();
            return Stage == 3;
        }

        internal GameplayOutcomeReplayIdentity RequireRelocationOutcome() =>
            RequireOutcome(
                RelocationProducerId,
                RelocationOperationId,
                throwOnFailure: true);

        internal GameplayOutcomeReplayIdentity RequireDispositionOutcome() =>
            RequireOutcome(
                DispositionProducerId,
                DispositionOperationId,
                throwOnFailure: true);

        internal GameplayOutcomeReplayIdentity RequireWastePolicyOutcome() =>
            RequireOutcome(
                WastePolicyProducerId,
                WastePolicyOperationId,
                throwOnFailure: true);

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral trade/inventory fixtures have no checkpoint.");
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
                    ["sourceStackId"] = sourceStackId,
                    ["relocatedStackId"] = relocatedStackId,
                    ["physicalItems"] = JObject.FromObject(items.Capture()),
                    ["wasteState"] = JObject.FromObject(waste.Capture())
                },
                ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            facts.Dispose();
            items.Dispose();
        }

        private int Stage =>
            ExactOutcomeCount(WastePolicyProducerId) == 1 ? 3
            : ExactOutcomeCount(DispositionProducerId) == 1 ? 2
            : ExactOutcomeCount(RelocationProducerId) == 1 ? 1
            : 0;

        private int StackQuantity(string stackId)
        {
            if (string.IsNullOrWhiteSpace(stackId))
                return 0;
            WorldItemStackSnapshot stack = items.GetAllStacks()
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.StackId,
                        stackId,
                        StringComparison.Ordinal));
            return stack?.Quantity ?? 0;
        }

        private int ExactOutcomeCount(string producerId) => ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes
            .Count(value => value != null
                && string.Equals(
                    value.producerId,
                    producerId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.outcomeTypeId,
                    TradeInventoryOutcomeIds.Result.Value,
                    StringComparison.Ordinal));

        private GameplayOutcomeReplayIdentity RequireOutcome(
            string producerId,
            string operationId,
            bool throwOnFailure)
        {
            GameplayOutcomeSnapshot exact = ledger
                .CaptureGameplayOutcomes()
                .exactOutcomes
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.producerId,
                        producerId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.operationId,
                        operationId,
                        StringComparison.Ordinal));
            GameplayOutcomeReplayIdentity identity = default;
            bool valid = exact != null
                && string.Equals(
                    exact.outcomeTypeId,
                    TradeInventoryOutcomeIds.Result.Value,
                    StringComparison.Ordinal)
                && ledger.TryGetResultIdentity(
                    new GameplayResultKey(
                        producerId,
                        new GameplayOperationId(operationId),
                        exact.commitRevision,
                        exact.localResultIndex),
                    out identity)
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash;
            if (!valid && throwOnFailure)
            {
                throw new InvalidOperationException(
                    "Trade/inventory production entry did not publish its exact outcome: "
                    + producerId + "/" + operationId);
            }
            return valid ? identity : default;
        }

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                ledger);
            RequireFields(
                state,
                "trade/inventory owner state",
                "schemaVersion",
                "schema",
                "stage",
                "sourceStackId",
                "relocatedStackId",
                "physicalItems",
                "wasteState");
            if ((int)state["schemaVersion"] != 1
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Trade/inventory checkpoint schema changed.");
            }
            sourceStackId = CausalGameplaySyntax.RequireId(
                (string)state["sourceStackId"],
                "trade/inventory source stack ID");
            relocatedStackId = (string)state["relocatedStackId"] ?? string.Empty;
            items.Restore(
                state["physicalItems"].ToObject<DungeonPhysicalItemSaveData>()
                ?? throw new InvalidOperationException(
                    "Trade/inventory physical item state is missing."));
            waste.Restore(waste.BuildRestore(
                state["wasteState"].ToObject<DungeonWasteProcessingSaveData>()
                ?? throw new InvalidOperationException(
                    "Trade/inventory waste state is missing.")));
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Trade/inventory checkpoint stage disagrees with its ledger.");
            }
        }

        private void ValidateState()
        {
            int relocationCount = ExactOutcomeCount(RelocationProducerId);
            int dispositionCount = ExactOutcomeCount(DispositionProducerId);
            int policyCount = ExactOutcomeCount(WastePolicyProducerId);
            WastePolicyData policy = waste.GetPolicy(WasteOriginKind.Plant);
            DungeonWasteProcessingSaveData wasteState = waste.Capture();
            int sourceQuantity = StackQuantity(sourceStackId);
            int destinationQuantity = StackQuantity(relocatedStackId);
            bool physicalValid = Stage switch
            {
                0 => sourceQuantity == 3
                    && string.IsNullOrEmpty(relocatedStackId)
                    && destinationQuantity == 0
                    && items.GetAllStacks().Count == 1,
                1 => sourceQuantity == 2
                    && !string.IsNullOrWhiteSpace(relocatedStackId)
                    && destinationQuantity == 1
                    && items.GetAllStacks().Count == 2,
                2 or 3 => sourceQuantity == 2
                    && !string.IsNullOrWhiteSpace(relocatedStackId)
                    && destinationQuantity == 0
                    && items.GetAllStacks().Count == 1,
                _ => false
            };
            bool policyChanged = policy.disposition
                    == WasteDispositionKind.Compost
                && !policy.enabled
                && Mathf.Approximately(
                    policy.maximumFeedContamination,
                    40f)
                && wasteState.nextPolicySequence == 2L;
            bool policyDefault = policy.disposition
                    == WasteDispositionKind.Store
                && policy.enabled
                && Mathf.Approximately(
                    policy.maximumFeedContamination,
                    79f)
                && wasteState.nextPolicySequence == 1L;
            bool ledgerValid = relocationCount is >= 0 and <= 1
                && dispositionCount is >= 0 and <= 1
                && policyCount is >= 0 and <= 1
                && relocationCount >= dispositionCount
                && dispositionCount >= policyCount
                && relocationCount + dispositionCount + policyCount == Stage;
            if (!physicalValid
                || !ledgerValid
                || (Stage < 3 ? !policyDefault : !policyChanged)
                || wasteState.pendingPolicyChanges == null
                || wasteState.pendingPolicyChanges.Count != 0)
            {
                throw new InvalidOperationException(
                    "Trade/inventory physical, waste-policy and exact ledger state diverged.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    "TradeInventoryCausalProductionEntryContext");
            }
        }

        private static WasteProcessingRuntime CreateWasteRuntime(
            IDomainFactStagingPublisher observations,
            IDomainFactRecoveryPublisher recovery,
            IGameCalendar calendar)
        {
            WasteProcessingRulesDefinition rules = new(
                5f,
                80f,
                new[]
                {
                    new WasteOriginRuleRecord
                    {
                        origin = WasteOriginKind.Plant,
                        defaultDisposition = WasteDispositionKind.Store,
                        defaultMaximumFeedContamination = 79f,
                        supportedDispositions = new List<WasteDispositionKind>
                        {
                            WasteDispositionKind.Store,
                            WasteDispositionKind.Compost
                        }
                    }
                },
                Array.Empty<WasteRecipeBindingRecord>(),
                Array.Empty<WasteFeedRuleRecord>(),
                Array.Empty<LegacyWasteItemRuleRecord>());
            return new WasteProcessingRuntime(
                new WasteProcessingMaterialDependencies(
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                        .Create<IWasteProcessingInventoryPort>(),
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                        .Create<IResourceEconomyContentCatalog>()),
                new WasteProcessingOperationDependencies(
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                        .Create<IWasteProcessingProductionPort>(),
                    new FixedClock(),
                    rules),
                new DungeonRuntimeAggregateRootStore(),
                observations,
                recovery,
                calendar);
        }
    }

    private sealed class FixedClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        private int day;
        private int hour;

        internal FixedCalendar(int day, int hour)
        {
            SetDateTime(day, hour);
        }

        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds =>
            ((Day - 1) * GameCalendarRules.SecondsPerDay)
            + (Hour * GameCalendarRules.SecondsPerGameHour);
        public TimeOfDay TimeOfDay => default;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int valueDay, int valueHour)
        {
            day = Math.Max(1, valueDay);
            hour = Math.Clamp(valueHour, 0, GameCalendarRules.HoursPerDay - 1);
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IProductionCausalActionFixture[] fixtures;

        internal FixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures)
        {
            this.fixtures = fixtures?.ToArray()
                ?? throw new ArgumentNullException(nameof(fixtures));
            Require(this.fixtures.Length == 3,
                "Trade/inventory causal fixture set is incomplete.");
        }

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

    private static void RequireFields(
        JObject value,
        string label,
        params string[] fields)
    {
        if (value == null)
            throw new InvalidOperationException(label + " is missing.");
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();
        string[] expected = fields
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields are not closed. expected="
                + string.Join(",", expected)
                + "; actual=" + string.Join(",", actual));
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
