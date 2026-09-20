using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionBillRuntime :
    IProductionBillCoreQuery,
    IProductionFacilityDestructiveDrainPreparedOutputQuery,
    IProductionBillCoreOrderCommand,
    IProductionBillCoreWorkExecution,
    IProductionBillDetachedFacilityPersistence,
    IProductionGenericBillWipTerminalCheckpointGcPort,
    ITickable
{
    public const string DestinationPrefix = "production:";
    public const string OutputDestinationPrefix = ProductionOutputDestinationId.Prefix;
    public const string StockSensorDestinationPrefix = "production-sensor:";
    public const string StockSensorItemId = "component:stock-sensor-panel";
    public const string GenericBillLifecycleSchema =
        "production-generic-bill-lifecycle@2";
    private const float SafeUtilityOutageHours = 6f;
    private const float DangerousTemperatureGraceHours = 3f;
    private const float DefaultDeliverySeconds = 12f;
    private const float DeliverySafetySeconds = 3f;
    private const int MaximumPrefetchBatches = 3;

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionAssemblyBridge items;
    private readonly IProductionAssemblyBridge workforceReplanService;
    private readonly IProductionAssemblyBridge workshops;
    private readonly IProductionOutputPlanningService outputPlanning;
    private readonly IProductionOutputExecutionService outputExecution;
    private readonly IProductionPreparedOutputExecutionPort preparedOutputExecution;
    private readonly IProductionRuinedBatchExecutionPort ruinedBatchExecution;
    private readonly IProductionPreparedOutputRoutingAuthority preparedOutputRouting;
    private readonly IProductionRecipeExecutionReceiptAuthority
        recipeExecutionReceipts;
    private readonly IProductionAssemblyBridge cycleUtilities;
    private readonly IProductionAssemblyBridge inputLogistics;
    private readonly IProductionStockSensorRuntime stockSensors;
    private readonly ProductionAggregateStateStore stateStore;
    private readonly IProductionBillSnapshotProjector snapshotProjector;
    private readonly IProductionAssemblyBridge buildingWorld;
    private readonly IProductionInputDestinationClaimRuntime inputDestinationClaims;
    private readonly IProductionFacilityMutationEpochQuery facilityMutationEpoch;
    private readonly IGameClock clock;
    private readonly IRecipeBalanceWorkCalculator balanceWorkCalculator;
    private readonly IProductionCommandOutcomeCommitter commandOutcomes;
    private WipTerminalCheckpointGcCandidate activeWipCheckpointGcCandidate;
    private int lastOutputAuthorityBuildingVersion = int.MinValue;
    private IReadOnlyList<ProductionBillRecord> bills => stateStore.Bills;
    private int nextBillSequence
    {
        get => stateStore.NextBillSequence;
        set => stateStore.NextBillSequence = value;
    }

    public ProductionBillRuntime(
        ProductionBillOrderDependencies order,
        ProductionBillExecutionDependencies execution)
    {
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }
        if (execution == null)
        {
            throw new ArgumentNullException(nameof(execution));
        }
        catalog = order.Catalog;
        items = order.Bridge;
        workforceReplanService = order.Bridge;
        inputLogistics = order.Bridge;
        stockSensors = order.StockSensors;
        stateStore = order.StateStore;
        workshops = order.Bridge;
        outputPlanning = execution.OutputPlanning;
        outputExecution = execution.OutputExecution;
        preparedOutputExecution = execution.PreparedOutputExecution;
        ruinedBatchExecution = execution.RuinedBatchExecution;
        preparedOutputRouting = execution.PreparedOutputRouting;
        recipeExecutionReceipts = execution.RecipeExecutionReceipts;
        cycleUtilities = execution.Bridge;
        snapshotProjector = execution.SnapshotProjector;
        buildingWorld = execution.Bridge;
        inputDestinationClaims = order.InputDestinationClaims;
        facilityMutationEpoch = order.FacilityMutationEpoch;
        clock = execution.Clock;
        balanceWorkCalculator = order.BalanceWorkCalculator;
        commandOutcomes = order.CommandOutcomes;
    }

    public int Version => stateStore.BillVersion;

    [GameplayInternalOnly("Shared production quality command", "ProductionBillSceneFacade")]
    public ProductionBillCommandResult SetMinimumCraftQuality(ProductionBillId billId, int minimumTier)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
            return ProductionBillCommandResult.Failed(new DomainFailure(FailureCode.ProductionBillMissing, billId.Value));
        if (TryGetFrozenMutationFailure(record, out var frozen)) return frozen;
        ProductionRecipeSO recipe = ResolveRecipe(record);
        if (!ProductionQualityTargetRules.IsValidTarget(minimumTier)
            || record.materialsConsumed || record.completedWork > 0f || record.outputOutcomeResolved
            || (minimumTier >= 0 && ProductionQualityTargetRules.CaptureTier(recipe,
                ResolveFacility(record), items, outputPlanning, 100f) < 0))
            return ProductionBillCommandResult.Failed(new DomainFailure(FailureCode.ProductionBillUnavailable,
                "quality-target-invalid-unsupported-or-cycle-in-progress"));
        if (record.minimumCraftQuality == minimumTier
            && !record.blockedFailure.IsFailure)
        {
            return ProductionBillCommandResult.Success(billId);
        }
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteRecordedBillCommand(
            record,
            recipe,
            ProductionCommandOutcomeKind.MinimumCraftQualityChanged,
            $"tier={record.minimumCraftQuality};blocked={(int)record.blockedFailure.Code}",
            $"tier={minimumTier};blocked=0",
            () =>
            {
                record.SetMinimumCraftQuality(minimumTier);
                record.SetBlockedFailure(DomainFailure.None);
            },
            () => record.RestoreConfigurationState(snapshot),
            recipe.WorkTypeId,
            requestWorker: true);
    }

    private DomainFailure CheckQualityTarget(ProductionBillRecord record, ProductionRecipeSO recipe,
        ProductionFacilityHandle facility, bool automatic)
    {
        if (record.minimumCraftQuality < 0) return DomainFailure.None;
        // Processing owns no worker tick; the stage transition validates again before finishing.
        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            && record.batchStage == ProductionBatchStage.Processing) return DomainFailure.None;
        if (record.outputOutcomeResolved)
        {
            if (items is not IProductionCraftQualityProjectionBridge quality)
                return ProductionQualityTargetRules.Check(record.minimumCraftQuality, -1);
            foreach (var output in record.resolvedOutputs)
            {
                var descriptor = items.CaptureOutputCapability(output.outputLineId, output.itemId);
                if (quality.TryResolveCraftQualityTier(descriptor, output.qualityModifier, 100f, out int tier)
                    && tier < record.minimumCraftQuality)
                    return ProductionQualityTargetRules.Check(record.minimumCraftQuality, tier);
            }
            return DomainFailure.None; // Frozen output remains authoritative, including a zero-output outcome.
        }
        float completed = record.completedWork;
        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            && record.batchStage == ProductionBatchStage.Finishing)
            completed += ResolveCycleRequiredWork(recipe) - ResolveCurrentRequiredWork(record, recipe);
        float ceiling = automatic ? facility.AutomaticQualityScoreCeiling
            : ProductionAutomaticQualityRules.ResolveScoreCeiling(facility.AutomaticQualityScoreCeiling,
                completed, record.workerContributions);
        return ProductionQualityTargetRules.Check(record.minimumCraftQuality,
            ProductionQualityTargetRules.CaptureTier(recipe, facility, items, outputPlanning, ceiling));
    }

    public bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionGenericBillTerminalDrainSaveData> producers,
        out IProductionGenericBillWipTerminalCheckpointGcCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (activeWipCheckpointGcCandidate != null)
        {
            failureReason = "production-wip-checkpoint-gc-already-active";
            return false;
        }
        ProductionGenericBillTerminalDrainSaveData[] ordered = (producers
                ?? Array.Empty<ProductionGenericBillTerminalDrainSaveData>())
            .OrderBy(value => value?.billId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.billId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            failureReason = "production-wip-checkpoint-gc-producer-invalid";
            return false;
        }

        List<ProductionWipTerminalReceiptSaveData> rows = new();
        foreach (ProductionGenericBillTerminalDrainSaveData producer in ordered)
        {
            if (producer.phase != ProductionGenericBillTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc
                || !ProductionGenericBillTerminalDrainCanonical.IsValidSave(
                    producer))
            {
                failureReason =
                    "production-wip-checkpoint-gc-producer-not-terminal";
                return false;
            }
            bool required = ProductionGenericBillTerminalDrainCanonical
                .RequiresWipTerminalReceipt(producer.sourceBill);
            if (!required)
            {
                if (!string.IsNullOrEmpty(producer.wipTerminalCommitId))
                {
                    failureReason =
                        "production-wip-checkpoint-gc-unexpected-owner";
                    return false;
                }
                continue;
            }
            if (!ProductionGenericBillTerminalDrainCanonical
                    .TryCreateWipTerminalReceipt(
                        producer.sourceBill,
                        out ProductionWipTerminalReceiptSaveData expected,
                        out failureReason)
                || !string.Equals(
                    producer.wipTerminalCommitId,
                    expected.commitId,
                    StringComparison.Ordinal))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "production-wip-checkpoint-gc-owner-mismatch"
                    : failureReason;
                return false;
            }
            ProductionWipTerminalReceiptSaveData[] matches = stateStore
                .WipTerminalReceipts
                .Where(value => value != null && string.Equals(
                    value.commitId,
                    expected.commitId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1
                || !ProductionGenericBillTerminalDrainCanonical.WipReceiptEquals(
                    matches[0], expected))
            {
                failureReason =
                    "production-wip-checkpoint-gc-lower-row-missing-or-conflicting";
                return false;
            }
            rows.Add(expected);
        }
        activeWipCheckpointGcCandidate = new WipTerminalCheckpointGcCandidate(
            stateStore.BillVersion,
            rows);
        candidate = activeWipCheckpointGcCandidate;
        return true;
    }

    public bool TryPublishCheckpointGarbageCollection(
        IProductionGenericBillWipTerminalCheckpointGcCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        WipTerminalCheckpointGcCandidate exact = RequireWipCheckpointGcCandidate(
            candidate);
        if (exact.Published)
            return true;
        if (stateStore.BillVersion != exact.ExpectedVersion
            || exact.Rows.Any(expected => stateStore.WipTerminalReceipts
                .Count(value => value != null && string.Equals(
                    value.commitId,
                    expected.commitId,
                    StringComparison.Ordinal)) != 1
                || !stateStore.WipTerminalReceipts.Any(value =>
                    ProductionGenericBillTerminalDrainCanonical.WipReceiptEquals(
                        value, expected))))
        {
            failureReason = "production-wip-checkpoint-gc-live-authority-changed";
            return false;
        }
        foreach (ProductionWipTerminalReceiptSaveData expected in exact.Rows)
        {
            if (!stateStore.TryRemoveWipTerminalReceiptExact(expected))
                throw new InvalidOperationException(
                    "WIP checkpoint-GC exact row vanished during publish.");
        }
        if (exact.Rows.Count > 0)
            stateStore.IncrementBillVersion();
        exact.Published = true;
        exact.PublishedVersion = stateStore.BillVersion;
        return true;
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionGenericBillWipTerminalCheckpointGcCandidate candidate)
    {
        WipTerminalCheckpointGcCandidate exact = RequireWipCheckpointGcCandidate(
            candidate);
        if (!exact.Published)
            return;
        if (stateStore.BillVersion != exact.PublishedVersion
            || exact.Rows.Any(expected => stateStore.WipTerminalReceipts.Any(
                value => string.Equals(value?.commitId, expected.commitId,
                    StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                "WIP checkpoint-GC rollback encountered live authority drift.");
        }
        foreach (ProductionWipTerminalReceiptSaveData expected in exact.Rows)
        {
            if (!stateStore.AddWipTerminalReceipt(expected))
                throw new InvalidOperationException(
                    "WIP checkpoint-GC rollback could not restore exact row.");
        }
        if (exact.Rows.Count > 0
            && !stateStore.TryRestoreBillVersionForCheckpointGc(
                exact.PublishedVersion,
                exact.ExpectedVersion))
        {
            throw new InvalidOperationException(
                "WIP checkpoint-GC rollback could not restore version.");
        }
        exact.Published = false;
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionGenericBillWipTerminalCheckpointGcCandidate candidate)
    {
        RequireWipCheckpointGcCandidate(candidate);
        activeWipCheckpointGcCandidate = null;
    }

    public IReadOnlyList<ProductionBillSnapshot> GetBills(ProductionFacilityHandle facility)
    {
        if (facility == null)
        {
            return Array.Empty<ProductionBillSnapshot>();
        }

        return bills
            .Where(record => MatchesFacility(record, facility))
            .Select(record => ToSnapshot(record, facility))
            .ToArray();
    }

    public ProductionFacilityBillLifecycleSnapshot CaptureFacilityLifecycle(
        BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
        {
            throw new ArgumentException(
                "A valid facility ID is required.",
                nameof(facilityId));
        }

        ProductionBillRecord[] owned = bills
            .Where(record => record != null
                && record.buildingInstanceId.Equals(facilityId))
            .OrderBy(record => record.billId.Value, StringComparer.Ordinal)
            .ToArray();
        int activeWipCount = 0;
        int waitingCount = 0;
        int publicationPreparedCount = 0;
        int physicalCommitPendingCount = 0;
        StringBuilder canonical = new StringBuilder(128 + owned.Length * 192)
            .Append(GenericBillLifecycleSchema).Append('|')
            .Append(facilityId.Value).Append('|')
            .Append(Version).Append('|');
        StringBuilder durableCanonical = new StringBuilder(
            128 + owned.Length * 192)
            .Append(GenericBillLifecycleSchema).Append('|')
            .Append(facilityId.Value).Append('|');

        foreach (ProductionBillRecord record in owned)
        {
            ProductionPreparedOutputPhase phase = record.preparedOutput?.phase
                ?? ProductionPreparedOutputPhase.Unresolved;
            bool exactPublicationPrepared = record.resolvedOutputs.Any(output =>
                output != null
                && !string.IsNullOrEmpty(output.pendingCommitId)
                && !output.pendingCommitApplied);
            bool exactPhysicalCommitPending = record.resolvedOutputs.Any(output =>
                output != null
                && !string.IsNullOrEmpty(output.pendingCommitId)
                && output.pendingCommitApplied
                && output.pendingOutputPublication?.phase
                    == ProductionExactOutputPublicationPhase.Published);
            bool hasActiveWip = record.materialsConsumed
                || record.wipInputQuantity > 0
                || record.wipInputMassGrams > 0L
                || record.batchStage != ProductionBatchStage.None
                || record.completedWork > 0f
                || phase != ProductionPreparedOutputPhase.Unresolved;
            if (hasActiveWip)
                activeWipCount++;
            if (phase == ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace)
                waitingCount++;
            else if (phase == ProductionPreparedOutputPhase.PublicationPrepared
                     || exactPublicationPrepared)
                publicationPreparedCount++;
            else if (phase == ProductionPreparedOutputPhase
                         .PhysicalBatchCommittedPublicationPending
                     || exactPhysicalCommitPending)
                physicalCommitPendingCount++;

            AppendLifecycleRecord(canonical, record);
            AppendLifecycleRecord(durableCanonical, record);
        }

        string fingerprint;
        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(
                Encoding.UTF8.GetBytes(canonical.ToString()));
            StringBuilder hex = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
                hex.Append(value.ToString("x2"));
            fingerprint = hex.ToString();
        }
        return new ProductionFacilityBillLifecycleSnapshot(
            facilityId,
            owned.Length,
            activeWipCount,
            waitingCount,
            publicationPreparedCount,
            physicalCommitPendingCount,
            Version,
            fingerprint,
            ComputeLifecycleFingerprint(durableCanonical));
    }

    public IReadOnlyList<ProductionFacilityDestructiveDrainPreparedOutputOwner>
        CapturePreparedOutputOwners(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
        {
            throw new ArgumentException(
                "A valid facility ID is required.",
                nameof(facilityId));
        }

        return bills
            .Where(record => record != null
                && record.buildingInstanceId.Equals(facilityId))
            .OrderBy(record => record.billId.Value, StringComparer.Ordinal)
            .Select(record =>
            {
                ProductionPreparedOutputBatchSaveData batch =
                    record.preparedOutput
                    ?? ProductionPreparedOutputBatchSaveData.Unresolved();
                if (batch.phase != ProductionPreparedOutputPhase.Unresolved)
                {
                    ProductionPreparedOutputContract.ValidateForBill(
                        batch,
                        record.billId,
                        record.recipeId,
                        record.cycleSequence,
                        record.outputDestinationId);
                }

                return new
                    ProductionFacilityDestructiveDrainPreparedOutputOwner(
                        record.billId,
                        record.buildingInstanceId,
                        record.recipeId,
                        record.cycleSequence,
                        record.outputDestinationId,
                        batch.phase,
                        batch.batchCommitId,
                        batch.outcomeFingerprint);
            })
            .ToArray();
    }

    private static void AppendLifecycleRecord(
        StringBuilder canonical,
        ProductionBillRecord record)
    {
        ProductionBillSaveData saved = ProductionBillStateCodec.ToSaveData(record);
        string payload = JsonUtility.ToJson(saved);
        canonical.Append(Encoding.UTF8.GetByteCount(payload)).Append(':')
            .Append(payload).Append(';');
    }

    private static string ComputeLifecycleFingerprint(StringBuilder canonical)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(canonical?.ToString() ?? string.Empty));
        StringBuilder hex = new StringBuilder(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("x2"));
        return hex.ToString();
    }

    public ProductionBillCommandResult AddBill(
        ProductionFacilityHandle facility,
        string recipeId,
        ProductionOrderMode mode,
        int amount)
    {
        if (facility == null || facility.IsDestroyed)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionFacilityMissing));
        }
        BuildingInstanceId facilityId = facility.InstanceId;
        if (!facilityId.IsValid)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionFacilityMissing));
        }
        if (facilityMutationEpoch.IsFrozen(facilityId))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    facilityId.Value,
                    "facility-mutation-frozen"));
        }

        if (!catalog.TryGetRecipe(recipeId, out ProductionRecipeSO recipe))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionRecipeMissing,
                    recipeId?.Trim() ?? string.Empty));
        }

        if (!MatchesRecipeWorkstation(facility, recipe))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionWorkstationMismatch,
                    recipe.RecipeId,
                    facilityId.Value));
        }

        if (!IsResearchUnlocked(recipe, out DomainFailure researchFailure))
        {
            return ProductionBillCommandResult.Failed(researchFailure);
        }

        if (!workshops.HasRequiredSupports(
                facility,
                recipe.RequiredSupportTags,
                out _))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionSupportUnavailable,
                    recipe.RecipeId));
        }

        if (mode == ProductionOrderMode.MaintainStock
            && !HasStockSensor(facility))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionStockSensorRequired,
                    facilityId.Value));
        }

        if (nextBillSequence == int.MaxValue)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    "id-sequence-exhausted"));
        }

        int sequence = nextBillSequence;
        ProductionBillId billId = (ProductionBillId)
            $"production-bill:{sequence}";
        ProductionBillRecord record = ProductionBillRecord.Create(
            billId,
            recipe.RecipeId,
            facilityId,
            mode,
            mode == ProductionOrderMode.RepeatCount
                ? Mathf.Max(1, amount)
                : -1,
            mode == ProductionOrderMode.MaintainStock
                ? Mathf.Max(1, amount)
                : 0,
            recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                ? ProductionBatchStage.Preparing
                : ProductionBatchStage.None,
            DestinationPrefix + billId.Value);
        record.SetOutputDestination(ResolveOutputDestinationId(facility));
        long ownerRevision = stateStore.NextCommandOutcomeSequence;
        if (ownerRevision <= 0L || ownerRevision == long.MaxValue)
        {
            return ProductionBillCommandResult.Failed(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                billId.Value,
                "production-command-owner-revision-exhausted"));
        }
        ProductionCommandOutcomeSource outcomeSource = new(
            ProductionCommandOutcomeKind.BillAdded,
            billId.Value,
            recipe.RecipeId,
            recipe.DisplayName,
            facilityId.Value,
            facility.Position.x,
            facility.Position.y,
            "absent",
            DescribeBillState(record));
        if (!commandOutcomes.TryPrepare(
                outcomeSource,
                ownerRevision,
                out IPreparedProductionCommandOutcome preparedOutcome,
                out string outcomePrepareFailure))
        {
            return CommandOutcomeFailure(billId, outcomePrepareFailure);
        }
        bool requiresInputDestination = RequiresPhysicalInputDestination(
            record,
            recipe,
            facility);
        if (requiresInputDestination)
        {
            long inputBufferMassGrams = inputLogistics
                .ResolveInputBufferMassCapacity(record, recipe, facility);
            if (!inputDestinationClaims.TryClaim(
                    record,
                    facility,
                    inputBufferMassGrams,
                    out string claimFailure))
            {
                commandOutcomes.Cancel(preparedOutcome);
                return ProductionBillCommandResult.Failed(
                    new DomainFailure(
                        FailureCode.ProductionBillUnavailable,
                        billId.Value,
                        claimFailure));
            }
        }

        try
        {
            nextBillSequence = sequence + 1;
            stateStore.AddBill(record);
            RequestMissingInputs(record, recipe, facility);
            QueueCommittedCommandOutcome(
                outcomeSource,
                ownerRevision,
                preparedOutcome);
        }
        catch
        {
            stateStore.RemovePendingCommandOutcome(ownerRevision);
            items.ReleaseDestination(record.materialDestinationId, facility.Position);
            stateStore.RemoveBill(record);
            nextBillSequence = sequence;
            stateStore.NextCommandOutcomeSequence = ownerRevision;
            commandOutcomes.Cancel(preparedOutcome);
            RevokeInputDestinationClaimOrThrow(
                record,
                requiresInputDestination,
                "rolled back after add failure");
            throw;
        }
        Touch(recipe.WorkTypeId, requestWorker: false);
        return ProductionBillCommandResult.Success(
            billId,
            ProductionBillOutcomeCode.BillAdded);
    }

    public ProductionBillCommandResult RemoveBill(
        ProductionBillId billId,
        bool returnMaterials)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillMissing,
                    billId.Value));
        }
        if (TryGetFrozenMutationFailure(record, out var frozen))
            return frozen;

        ProductionRecipeSO inputRecipe = ResolveRecipe(record)
            ?? throw new InvalidOperationException(
                $"Production bill '{billId.Value}' recipe is missing during removal.");
        ProductionFacilityHandle inputFacility = ResolveFacility(record);
        bool requiresInputDestination = RequiresPhysicalInputDestination(
            record,
            inputRecipe,
            inputFacility);
        if (requiresInputDestination
            && !inputDestinationClaims.TryValidateClaim(
                record,
                out string claimFailure))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    billId.Value,
                    claimFailure));
        }

        bool usesPreparedOutput = UsesPreparedOutput(record);
        if (usesPreparedOutput
            && !CanRetirePreparedOutputBill(record))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    record.billId.Value,
                    "prepared-output-routing-pending"));
        }

        long ownerRevision = stateStore.NextCommandOutcomeSequence;
        if (inputFacility == null
            || ownerRevision <= 0L
            || ownerRevision == long.MaxValue)
        {
            return ProductionBillCommandResult.Failed(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                billId.Value,
                inputFacility == null
                    ? "production-command-facility-missing"
                    : "production-command-owner-revision-exhausted"));
        }
        ProductionCommandOutcomeSource outcomeSource = new(
            ProductionCommandOutcomeKind.BillRemoved,
            record.billId.Value,
            inputRecipe.RecipeId,
            inputRecipe.DisplayName,
            inputFacility.InstanceId.Value,
            inputFacility.Position.x,
            inputFacility.Position.y,
            DescribeBillState(record),
            "absent");
        if (!commandOutcomes.TryPrepare(
                outcomeSource,
                ownerRevision,
                out IPreparedProductionCommandOutcome preparedOutcome,
                out string outcomePrepareFailure))
        {
            return CommandOutcomeFailure(billId, outcomePrepareFailure);
        }

        bool domainCommitted = false;
        try
        {
            if (usesPreparedOutput)
            {
                if (HasLegacyOutputAuthority(record))
                {
                    return ProductionBillCommandResult.Failed(
                        PreparedOutputAuthorityConflict(record));
                }
                ProductionPreparedOutputReleaseResult release =
                    preparedOutputExecution.Release(
                        record,
                        ProductionWipTerminalReason.Cancelled);
                if (!release.IsValid
                    || !release.Released
                    || record.preparedOutput?.phase !=
                        ProductionPreparedOutputPhase.Unresolved)
                {
                    DomainFailure preparedReleaseFailure = release.IsValid
                        && release.Failure.IsFailure
                            ? release.Failure
                            : new DomainFailure(
                                FailureCode.ProductionOutputUnavailable,
                                record.billId.Value,
                                release.IsValid && release.PhysicalBatchCommitted
                                    ? "prepared-output-physical-batch-retained"
                                    : release.IsValid && release.Released
                                        ? "prepared-output-release-state-mismatch"
                                        : "prepared-output-release-invalid-result");
                    return ProductionBillCommandResult.Failed(
                        preparedReleaseFailure);
                }
            }

            if (record.materialsConsumed
                && !TryCommitWipTerminalDisposition(
                    record,
                    ProductionWipTerminalReason.Cancelled))
            {
                return ProductionBillCommandResult.Failed(
                    new DomainFailure(
                        FailureCode.ProductionBillUnavailable,
                        billId.Value,
                        "wip-terminal-receipt-conflict"));
            }

            if (requiresInputDestination
                && !items.TryReleaseDestinationAtomically(
                    record.materialDestinationId,
                    ResolveFacility(record)?.Position ?? Vector2Int.zero,
                    out _,
                    out string releaseFailure))
            {
                return ProductionBillCommandResult.Failed(
                    new DomainFailure(
                        FailureCode.ProductionBillUnavailable,
                        billId.Value,
                        releaseFailure));
            }

            RevokeInputDestinationClaimOrThrow(
                record,
                requiresInputDestination,
                "revoked during removal");
            QueueCommittedCommandOutcome(
                outcomeSource,
                ownerRevision,
                preparedOutcome,
                deliverImmediately: false);
            if (!stateStore.RemoveBill(record))
            {
                stateStore.RemovePendingCommandOutcome(ownerRevision);
                stateStore.NextCommandOutcomeSequence = ownerRevision;
                throw new InvalidOperationException(
                    $"Production bill '{billId.Value}' vanished during removal commit.");
            }
            domainCommitted = true;
            DeliverPendingCommandOutcome(ownerRevision, preparedOutcome);
            Touch(default, requestWorker: false);
            return ProductionBillCommandResult.Success(
                billId,
                ProductionBillOutcomeCode.BillRemoved);
        }
        finally
        {
            if (!domainCommitted)
                commandOutcomes.Cancel(preparedOutcome);
        }
    }

    public ProductionBillCommandResult MoveBill(
        ProductionBillId billId,
        int targetIndex)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillMissing,
                    billId.Value));
        }
        if (TryGetFrozenMutationFailure(record, out var frozen))
            return frozen;

        List<ProductionBillRecord> facilityBills = bills
            .Where(candidate => candidate.buildingInstanceId.Equals(
                record.buildingInstanceId))
            .ToList();
        int currentLocalIndex = facilityBills.IndexOf(record);
        int clampedTarget = Mathf.Clamp(targetIndex, 0, facilityBills.Count - 1);
        if (currentLocalIndex == clampedTarget)
        {
            return ProductionBillCommandResult.Success(billId);
        }

        ProductionBillRecord anchor = facilityBills[clampedTarget];
        ProductionRecipeSO recipe = ResolveRecipe(record);
        return ExecuteRecordedBillCommand(
            record,
            recipe,
            ProductionCommandOutcomeKind.BillPriorityChanged,
            $"local-index={currentLocalIndex}",
            $"local-index={clampedTarget}",
            () => stateStore.MoveBill(
                record,
                anchor,
                insertAfter: currentLocalIndex < clampedTarget),
            () => RestoreFacilityBillIndex(record, currentLocalIndex),
            default,
            requestWorker: false);
    }

    public ProductionBillCommandResult SetSuspended(
        ProductionBillId billId,
        bool suspended)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillMissing,
                    billId.Value));
        }
        if (TryGetFrozenMutationFailure(record, out var frozen))
            return frozen;

        if (record.suspended == suspended
            && string.IsNullOrEmpty(record.reservedWorkerId))
        {
            return ProductionBillCommandResult.Success(billId);
        }
        ProductionRecipeSO recipe = ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteRecordedBillCommand(
            record,
            recipe,
            ProductionCommandOutcomeKind.SuspensionChanged,
            $"suspended={Bool(record.suspended)};reserved-worker={Optional(record.reservedWorkerId)}",
            $"suspended={Bool(suspended)};reserved-worker=none",
            () => record.SetSuspended(suspended),
            () => record.RestoreConfigurationState(snapshot),
            recipe?.WorkTypeId ?? default,
            requestWorker: !suspended);
    }

    public ProductionBillCommandResult SetStockPolicy(
        ProductionBillId billId,
        int minimumReserve,
        int targetStock)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillMissing,
                    billId.Value));
        }
        if (TryGetFrozenMutationFailure(record, out var frozen))
            return frozen;

        ProductionFacilityHandle facility = ResolveFacility(record);
        if (!HasStockSensor(facility))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionStockSensorRequired,
                    record.buildingInstanceId.Value));
        }

        int normalizedMinimum = Math.Max(0, minimumReserve);
        int normalizedTarget = Math.Max(normalizedMinimum, targetStock);
        bool transitionChanges = WouldModeTransitionChange(
            record,
            ProductionOrderMode.MaintainStock);
        if (record.minimumReserve == normalizedMinimum
            && record.targetStock == normalizedTarget
            && !transitionChanges)
        {
            return ProductionBillCommandResult.Success(billId);
        }
        ProductionRecipeSO recipe = ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteRecordedBillCommand(
            record,
            recipe,
            ProductionCommandOutcomeKind.StockPolicyChanged,
            DescribeOrderState(record),
            DescribeOrderStateAfter(
                record,
                ProductionOrderMode.MaintainStock,
                record.remainingCycles,
                normalizedMinimum,
                normalizedTarget),
            () =>
            {
                record.SetStockPolicy(minimumReserve, targetStock);
                QueueOrApplyModeTransition(
                    record,
                    ProductionOrderMode.MaintainStock);
            },
            () => record.RestoreConfigurationState(snapshot),
            recipe?.WorkTypeId ?? default,
            requestWorker: true);
    }

    public ProductionBillCommandResult SetOrderMode(
        ProductionBillId billId,
        ProductionOrderMode mode,
        int amount)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillMissing,
                    billId.Value));
        }
        if (TryGetFrozenMutationFailure(record, out var frozen))
            return frozen;

        if (mode == ProductionOrderMode.MaintainStock
            && !HasStockSensor(ResolveFacility(record)))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionStockSensorRequired,
                    record.buildingInstanceId.Value));
        }

        int normalizedRemaining = record.remainingCycles;
        int normalizedMinimum = record.minimumReserve;
        int normalizedTarget = record.targetStock;
        if (mode == ProductionOrderMode.RepeatCount)
            normalizedRemaining = Mathf.Max(1, amount);
        else if (mode == ProductionOrderMode.MaintainStock)
        {
            normalizedTarget = Mathf.Max(1, amount);
            normalizedMinimum = Mathf.Min(record.minimumReserve, normalizedTarget);
        }
        if (record.remainingCycles == normalizedRemaining
            && record.minimumReserve == normalizedMinimum
            && record.targetStock == normalizedTarget
            && !WouldModeTransitionChange(record, mode))
        {
            return ProductionBillCommandResult.Success(billId);
        }

        ProductionRecipeSO recipe = ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteRecordedBillCommand(
            record,
            recipe,
            ProductionCommandOutcomeKind.OrderModeChanged,
            DescribeOrderState(record),
            DescribeOrderStateAfter(
                record,
                mode,
                normalizedRemaining,
                normalizedMinimum,
                normalizedTarget),
            () =>
            {
                if (mode == ProductionOrderMode.RepeatCount)
                {
                    record.SetRepeatCount(Mathf.Max(1, amount));
                }
                else if (mode == ProductionOrderMode.MaintainStock)
                {
                    int target = Mathf.Max(1, amount);
                    record.SetStockPolicy(
                        Mathf.Min(record.minimumReserve, target),
                        target);
                }

                QueueOrApplyModeTransition(record, mode);
            },
            () => record.RestoreConfigurationState(snapshot),
            recipe?.WorkTypeId ?? default,
            requestWorker: true);
    }

    public ProductionBillCommandResult SetDistributionPolicy(
        ProductionBillId billId,
        ProductionDistributionMode mode,
        IReadOnlyList<ProductionConsumerRoutePolicy> routes)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillMissing,
                    billId.Value));
        }
        if (TryGetFrozenMutationFailure(record, out var frozen))
            return frozen;

        ProductionConsumerRoutePolicy[] normalizedRoutes = (routes
                ?? Array.Empty<ProductionConsumerRoutePolicy>())
            .Where(route => route != null
                && !string.IsNullOrWhiteSpace(route.consumerId))
            .GroupBy(route => route.consumerId.Trim(), StringComparer.Ordinal)
            .Select(group => group.First().Clone())
            .ToArray();
        string beforePolicy = DescribeDistributionPolicy(
            record.distributionMode,
            record.routePolicies);
        string afterPolicy = DescribeDistributionPolicy(mode, normalizedRoutes);
        if (string.Equals(beforePolicy, afterPolicy, StringComparison.Ordinal))
            return ProductionBillCommandResult.Success(billId);

        ProductionRecipeSO recipe = ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteRecordedBillCommand(
            record,
            recipe,
            ProductionCommandOutcomeKind.DistributionPolicyChanged,
            beforePolicy,
            afterPolicy,
            () => record.ReplaceDistributionPolicy(mode, normalizedRoutes),
            () => record.RestoreConfigurationState(snapshot),
            default,
            requestWorker: false);
    }

    public ProductionBillCommandResult SetWorkerPolicy(
        ProductionBillId billId,
        WorkerSelectionPolicySaveData policy)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionBillMissing, billId.Value));
        }
        if (TryGetFrozenMutationFailure(record, out var frozen))
            return frozen;
        WorkerSelectionPolicySaveData normalized = policy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        string beforePolicy = DescribeWorkerPolicy(
            record.workerPolicy,
            record.reservedWorkerId);
        string afterPolicy = DescribeWorkerPolicy(normalized, string.Empty);
        if (string.Equals(beforePolicy, afterPolicy, StringComparison.Ordinal))
            return ProductionBillCommandResult.Success(billId);

        ProductionRecipeSO recipe = ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteRecordedBillCommand(
            record,
            recipe,
            ProductionCommandOutcomeKind.WorkerPolicyChanged,
            beforePolicy,
            afterPolicy,
            () =>
            {
                record.SetWorkerPolicy(normalized);
                record.SetReservedWorker(string.Empty);
            },
            () => record.RestoreConfigurationState(snapshot),
            recipe?.WorkTypeId ?? default,
            requestWorker: true);
    }

    public ProductionBillCommandResult SetEmergencyWorker(
        ProductionBillId billId,
        string characterId)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionBillMissing, billId.Value));
        if (TryGetFrozenMutationFailure(record, out var frozen))
            return frozen;

        string normalized = characterId?.Trim() ?? string.Empty;
        if (normalized.Length > 0
            && (!string.Equals(normalized, characterId, StringComparison.Ordinal)
                || bills.Any(candidate => candidate != null
                    && candidate != record
                    && string.Equals(
                        candidate.emergencyWorkerId,
                        normalized,
                        StringComparison.Ordinal))))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.WorkOrderWorkerIneligible,
                    normalized));
        }

        WorkerSelectionPolicySaveData resultingPolicy = normalized.Length > 0
            ? new WorkerSelectionPolicySaveData
            {
                mode = WorkerSelectionMode.SpecificCharacters,
                sortMode = WorkerCandidateSortMode.SpecificThenBestExpectedQuality,
                specificCharacterIds = new List<string> { normalized }
            }.CloneNormalized()
            : record.workerPolicy?.CloneNormalized()
                ?? WorkerSelectionPolicySaveData.Anyone();
        string beforeState = "emergency=" + Optional(record.emergencyWorkerId)
            + ";" + DescribeWorkerPolicy(
                record.workerPolicy,
                record.reservedWorkerId);
        string afterState = "emergency=" + Optional(normalized)
            + ";" + DescribeWorkerPolicy(resultingPolicy, string.Empty);
        if (string.Equals(beforeState, afterState, StringComparison.Ordinal))
            return ProductionBillCommandResult.Success(billId);

        ProductionRecipeSO recipe = ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteRecordedBillCommand(
            record,
            recipe,
            ProductionCommandOutcomeKind.EmergencyWorkerChanged,
            beforeState,
            afterState,
            () =>
            {
                record.SetEmergencyWorker(normalized);
                if (normalized.Length > 0)
                    record.SetWorkerPolicy(resultingPolicy);
                record.SetReservedWorker(string.Empty);
            },
            () => record.RestoreConfigurationState(snapshot),
            recipe?.WorkTypeId ?? default,
            requestWorker: true);
    }

    public bool HasStockSensor(ProductionFacilityHandle facility)
    {
        return stockSensors.Has(facility);
    }

    public ProductionBillCommandResult RequestStockSensorInstallation(
        ProductionFacilityHandle facility)
    {
        if (TryGetFrozenMutationFailure(facility, out var frozen))
            return frozen;
        bool wasInstalled = stockSensors.Has(facility);
        if (wasInstalled)
            return stockSensors.RequestInstallation(facility);
        if (!TryPrepareFacilityCommandOutcome(
                facility,
                ProductionCommandOutcomeKind.StockSensorInstalled,
                "not-installed",
                "installed",
                out ProductionCommandOutcomeSource outcomeSource,
                out long ownerRevision,
                out IPreparedProductionCommandOutcome prepared,
                out ProductionBillCommandResult prepareFailure))
        {
            return prepareFailure;
        }
        int version = stockSensors.Version;
        ProductionBillCommandResult result =
            stockSensors.RequestInstallation(facility);
        if (!wasInstalled && stockSensors.Has(facility))
        {
            QueueCommittedCommandOutcome(
                outcomeSource,
                ownerRevision,
                prepared);
        }
        else
        {
            commandOutcomes.Cancel(prepared);
        }
        if (stockSensors.Version != version)
        {
            Touch(default, requestWorker: false);
        }

        return result;
    }

    public ProductionBillCommandResult RemoveStockSensor(
        ProductionFacilityHandle facility)
    {
        if (TryGetFrozenMutationFailure(facility, out var frozen))
            return frozen;
        bool wasInstalled = stockSensors.Has(facility);
        if (!wasInstalled)
            return stockSensors.Remove(facility);
        if (!TryPrepareFacilityCommandOutcome(
                facility,
                ProductionCommandOutcomeKind.StockSensorRemoved,
                "installed",
                "removed",
                out ProductionCommandOutcomeSource outcomeSource,
                out long ownerRevision,
                out IPreparedProductionCommandOutcome prepared,
                out ProductionBillCommandResult prepareFailure))
        {
            return prepareFailure;
        }
        int version = stockSensors.Version;
        ProductionBillCommandResult result = stockSensors.Remove(facility);
        if (wasInstalled && !stockSensors.Has(facility))
        {
            QueueCommittedCommandOutcome(
                outcomeSource,
                ownerRevision,
                prepared);
        }
        else
        {
            commandOutcomes.Cancel(prepared);
        }
        if (stockSensors.Version != version)
        {
            Touch(default, requestWorker: false);
        }

        return result;
    }

    public ProductionBillCommandResult AcknowledgeStockSensorUnlock(
        ProductionFacilityHandle facility)
    {
        if (TryGetFrozenMutationFailure(facility, out var frozen))
            return frozen;
        bool wasAcknowledged = stockSensors.IsAcknowledged(facility);
        if (wasAcknowledged)
            return stockSensors.Acknowledge(facility);
        if (!TryPrepareFacilityCommandOutcome(
                facility,
                ProductionCommandOutcomeKind.StockSensorUnlockAcknowledged,
                "unacknowledged",
                "acknowledged",
                out ProductionCommandOutcomeSource outcomeSource,
                out long ownerRevision,
                out IPreparedProductionCommandOutcome prepared,
                out ProductionBillCommandResult prepareFailure))
        {
            return prepareFailure;
        }
        int version = stockSensors.Version;
        ProductionBillCommandResult result = stockSensors.Acknowledge(facility);
        if (!wasAcknowledged && stockSensors.IsAcknowledged(facility))
        {
            QueueCommittedCommandOutcome(
                outcomeSource,
                ownerRevision,
                prepared);
        }
        else
        {
            commandOutcomes.Cancel(prepared);
        }
        if (stockSensors.Version != version)
        {
            Touch(default, requestWorker: false);
        }

        return result;
    }


    public ProductionWorkAvailabilityResult CheckWorkAvailability(
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId)
    {
        ProductionBillRecord record = FindRunnableBill(
            facility,
            workTypeId,
            requireDeliveredInputs: true,
            out DomainFailure failure);
        if (record != null)
        {
            ProductionRecipeSO recipe = ResolveRecipe(record);
            string utilityFailure = recipe == null
                ? "production-recipe-missing"
                : string.Empty;
            bool finishingPassiveBatch = recipe != null
                && recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                && record.batchStage == ProductionBatchStage.Finishing;
            bool utilitiesValid = recipe != null
                && (finishingPassiveBatch
                    ? cycleUtilities.ValidateProcessingUtilities(
                        record.occupiedSupportNodeId,
                        recipe,
                        facility,
                        out utilityFailure)
                    : cycleUtilities.ValidateCycleRequirements(
                        record,
                        recipe,
                        facility,
                        bills,
                        out utilityFailure));
            if (recipe == null
                || !utilitiesValid)
            {
                failure = new DomainFailure(
                    FailureCode.ProductionUtilitiesUnavailable,
                    utilityFailure ?? "production-cycle-utility-missing");
                record = null;
            }
        }
        return new ProductionWorkAvailabilityResult(
            record != null,
            failure,
            record != null ? ToSnapshot(record, facility) : null);
    }

    public ProductionWorkBeginResult BeginWork(
        ProductionWorkerHandle worker,
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId)
    {
        ProductionBillRecord record = FindRunnableBill(
            facility,
            workTypeId,
            requireDeliveredInputs: true,
            out DomainFailure failure,
            automatic: string.IsNullOrEmpty(worker?.PersistentId));
        if (record == null)
        {
            return new ProductionWorkBeginResult(null, failure);
        }
        if (facilityMutationEpoch.IsFrozen(record.buildingInstanceId))
        {
            DomainFailure frozen = new(
                FailureCode.ProductionBillUnavailable,
                record.buildingInstanceId.Value,
                "facility-mutation-frozen");
            return new ProductionWorkBeginResult(null, frozen);
        }

        if (!workshops.IsWorkerEligible(
                worker,
                record.workerPolicy,
                out string workerFailure))
        {
            DomainFailure ineligible = new(
                FailureCode.WorkOrderWorkerIneligible,
                workerFailure ?? string.Empty);
            record.SetBlockedFailure(ineligible);
            record.SetReservedWorker(string.Empty);
            return new ProductionWorkBeginResult(null, ineligible);
        }

        ProductionRecipeSO recipe = ResolveRecipe(record);
        if (record.materialsConsumed
            && !string.IsNullOrEmpty(record.wipInputCommitId)
            && !items.AcknowledgeWipInput(
                record.wipInputCommitId,
                out string restoredAcknowledgeFailure))
        {
            throw new InvalidOperationException(
                $"Restored production WIP input '{record.wipInputCommitId}' could not be acknowledged: {restoredAcknowledgeFailure}");
        }
        ProductionPreparedOutputPhase preparedPhase = record.preparedOutput?.phase
            ?? ProductionPreparedOutputPhase.Unresolved;
        bool usesPreparedOutput = UsesPreparedOutput(recipe);
        if (usesPreparedOutput
            && !record.materialsConsumed
            && preparedPhase != ProductionPreparedOutputPhase.Unresolved)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                record.billId.Value,
                "prepared-output-cycle-start-phase-conflict");
            record.SetBlockedFailure(failure);
            Touch(default, requestWorker: false);
            return new ProductionWorkBeginResult(null, failure);
        }
        bool requiresCycleStartCapacity = usesPreparedOutput
            ? ProductionPreparedOutputMigrationScope
                .RequiresCycleStartCapacity(record)
            : !record.materialsConsumed;
        if (requiresCycleStartCapacity
            && !TryValidateCycleStart(
                record,
                recipe,
                facility,
                out DomainFailure cycleFailure))
        {
            failure = cycleFailure.IsFailure
                ? cycleFailure
                : new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    record.billId.Value,
                    "cycle-start-invalid-result");
            record.SetBlockedFailure(failure);
            Touch(default, requestWorker: false);
            return new ProductionWorkBeginResult(null, failure);
        }

        IReadOnlyDictionary<string, int> cycleInputs =
            ToCycleInputMap(record, recipe, facility);
        ProductionWipInputReceipt wipReceipt = default;
        if (!record.materialsConsumed
            && cycleInputs.Count > 0
            && !items.ConsumeDeliveredToWip(
                record.materialDestinationId,
                cycleInputs,
                $"production-wip:{record.billId.Value}:{record.cycleSequence:D8}",
                out wipReceipt,
                out _))
        {
            RequestMissingInputs(record, recipe, facility);
            return new ProductionWorkBeginResult(
                null,
                new DomainFailure(FailureCode.ProductionMaterialsMissing));
        }

        if (!record.materialsConsumed && cycleInputs.Count > 0)
        {
            record.SetWipInput(wipReceipt);
            record.SetMaterialsConsumed(true);
            if (!items.AcknowledgeWipInput(
                    wipReceipt.CommitId,
                    out string acknowledgeFailure))
            {
                throw new InvalidOperationException(
                    $"Production WIP input '{wipReceipt.CommitId}' was recorded but could not be acknowledged: {acknowledgeFailure}");
            }
        }
        else
        {
            record.SetMaterialsConsumed(true);
        }
        if (record.processFluidConsumed
            && record.processManualWaterTransfers.Count > 0
            && !cycleUtilities.AcknowledgeCycleUtilities(
                record.CaptureProcessFluidReceipt(),
                out string replayAcknowledgeFailure))
        {
            failure = new DomainFailure(
                FailureCode.ProductionUtilitiesUnavailable,
                replayAcknowledgeFailure);
            record.SetBlockedFailure(failure);
            return new ProductionWorkBeginResult(null, failure);
        }
        if (!record.processFluidConsumed)
        {
            if (!TryConsumeCycleUtilities(
                    record,
                    recipe,
                    facility,
                    out ProductionProcessFluidReceipt fluidReceipt,
                    out string utilityFailure))
            {
                failure = new DomainFailure(
                    FailureCode.ProductionUtilitiesUnavailable,
                    utilityFailure);
                record.SetBlockedFailure(failure);
                return new ProductionWorkBeginResult(null, failure);
            }
            record.SetProcessFluid(fluidReceipt);
            record.SetProcessFluidConsumed(true);
            if (!cycleUtilities.AcknowledgeCycleUtilities(
                    fluidReceipt,
                    out string fluidAcknowledgeFailure))
            {
                throw new InvalidOperationException(
                    $"Production process-fluid receipt for '{record.billId}' could not be acknowledged: {fluidAcknowledgeFailure}");
            }
        }

        record.SetBlockedFailure(DomainFailure.None);
        record.SetReservedWorker(worker?.PersistentId);
        RecalculatePrefetch(record, recipe, worker);
        RequestMissingInputs(record, recipe, facility);
        Touch(default, requestWorker: false);
        return new ProductionWorkBeginResult(
            ToSnapshot(record, facility),
            DomainFailure.None);
    }

    public ProductionWorkExecutionResult ExecuteWork(
        ProductionWorkerHandle worker,
        ProductionFacilityHandle facility,
        ProductionBillId billId,
        float amount)
    {
        ProductionBillRecord record = Find(billId);
        ProductionRecipeSO recipe = ResolveRecipe(record);
        if (record == null
            || recipe == null
            || !MatchesFacility(record, facility)
            || record.suspended
            || !record.materialsConsumed)
        {
            return FailedExecution(FailureCode.ProductionBillUnavailable);
        }
        DomainFailure qualityFailure = CheckQualityTarget(record, recipe, facility,
            string.IsNullOrEmpty(worker?.PersistentId));
        if (qualityFailure.IsFailure)
        {
            record.SetBlockedFailure(qualityFailure);
            return new ProductionWorkExecutionResult(false, false, ProductionBillOutcomeCode.None, qualityFailure);
        }
        if (facilityMutationEpoch.IsFrozen(record.buildingInstanceId))
        {
            return FailedExecution(
                FailureCode.ProductionBillUnavailable,
                record.buildingInstanceId.Value,
                "facility-mutation-frozen");
        }

        string workerId = worker?.PersistentId ?? string.Empty;
        if (!workshops.IsWorkerEligible(
                worker,
                record.workerPolicy,
                out string workerFailure))
        {
            record.SetReservedWorker(string.Empty);
            record.SetBlockedFailure(new DomainFailure(
                FailureCode.WorkOrderWorkerIneligible,
                workerFailure ?? string.Empty));
            return FailedExecution(
                FailureCode.WorkOrderWorkerIneligible,
                workerFailure ?? string.Empty);
        }
        if (!string.IsNullOrWhiteSpace(record.reservedWorkerId)
            && !string.Equals(record.reservedWorkerId, workerId, StringComparison.Ordinal))
        {
            return FailedExecution(
                FailureCode.ProductionBillReservedByOtherWorker,
                record.reservedWorkerId);
        }

        record.SetReservedWorker(workerId);
        float requiredWork = ResolveCurrentRequiredWork(record, recipe);
        float supportWorkMultiplier =
            outputExecution.ResolveWorkSpeedMultiplier(facility, recipe);
        float acceptedWork = Mathf.Min(
            Mathf.Max(0f, amount) * supportWorkMultiplier,
            Mathf.Max(0f, requiredWork - record.completedWork));
        record.SetCompletedWork(Mathf.Clamp(
            record.completedWork
                + acceptedWork,
            0f,
            requiredWork));
        CraftContributionAccumulator contributions =
            new(record.workerContributions);
        contributions.Add(
            workerId,
            acceptedWork,
            workshops.GetRelevantCraftSkill(worker, recipe));
        record.ReplaceWorkerContributions(contributions.Capture());
        if (record.completedWork + 0.001f < requiredWork)
        {
            return SuccessfulExecution(
                cycleCompleted: false,
                outcome: ProductionBillOutcomeCode.WorkProgressed);
        }

        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            && record.batchStage == ProductionBatchStage.Preparing)
        {
            if (!TryOccupyBatchSupport(
                    record,
                    recipe,
                    facility,
                    out string supportFailure))
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionSupportUnavailable));
                record.SetReservedWorker(string.Empty);
                return new ProductionWorkExecutionResult(
                    false,
                    false,
                    ProductionBillOutcomeCode.None,
                    record.blockedFailure);
            }

            record.SetBatchStage(ProductionBatchStage.Processing);
            record.SetRemainingProcessingHours(recipe.ProcessingGameHours);
            record.SetCompletedWork(0f);
            record.SetReservedWorker(string.Empty);
            record.SetBlockedFailure(DomainFailure.None);
            Touch(recipe.WorkTypeId, requestWorker: false);
            return SuccessfulExecution(
                cycleCompleted: false,
                outcome: ProductionBillOutcomeCode.ProcessingStarted);
        }

        bool usesPreparedOutput = UsesPreparedOutput(recipe);
        if (usesPreparedOutput)
        {
            if (HasLegacyOutputAuthority(record))
            {
                return BlockPreparedOutput(record,
                    PreparedOutputAuthorityConflict(record));
            }
            ProductionPreparedOutputExecutionResult preparedResult =
                preparedOutputExecution.Execute(
                    record,
                    recipe,
                    facility,
                    worker);
            if (!preparedResult.IsValid
                || !preparedResult.CycleOutputCompleted)
            {
                bool phaseMatches = preparedResult.IsValid
                    && record.preparedOutput?.phase == preparedResult.Phase;
                DomainFailure preparedFailure = phaseMatches
                    && preparedResult.Failure.IsFailure
                    ? preparedResult.Failure
                    : new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        preparedResult.IsValid && !phaseMatches
                            ? "prepared-output-result-phase-mismatch"
                            : "prepared-output-incomplete-result");
                return BlockPreparedOutput(record, preparedFailure);
            }
            if (preparedResult.Phase != ProductionPreparedOutputPhase.Completed
                || record.preparedOutput?.phase !=
                    ProductionPreparedOutputPhase.Completed)
            {
                return BlockPreparedOutput(
                    record,
                    new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        "prepared-output-completion-state-mismatch"));
            }
            if (!recipeExecutionReceipts.TryPublishCompleted(
                    record.billId,
                    record.cycleSequence,
                    record.recipeId,
                    record.buildingInstanceId,
                    worker?.AuthorityKind == ProductionWorkerAuthorityKind.Actor
                        ? workerId
                        : string.Empty,
                    record.wipInputCommitId,
                    record.wipInputQuantity,
                    record.wipInputMassGrams,
                    record.preparedOutput,
                    out string receiptFailure))
            {
                return BlockPreparedOutput(
                    record,
                    new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        string.IsNullOrEmpty(receiptFailure)
                            ? "recipe-execution-receipt-publication-failed"
                            : receiptFailure));
            }
            record.ClearCompletedPreparedOutput();
        }
        else
        {
            if (!recipeExecutionReceipts.TryEnsureExactCapture(
                    record.billId,
                    record.cycleSequence,
                    record.recipeId,
                    record.buildingInstanceId,
                    out string ensureExactFailure))
            {
                record.SetReservedWorker(string.Empty);
                return new ProductionWorkExecutionResult(
                    false,
                    false,
                    ProductionBillOutcomeCode.None,
                    new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        string.IsNullOrEmpty(ensureExactFailure)
                            ? "recipe-exact-capture-reservation-failed"
                            : ensureExactFailure));
            }
            if (!record.outputOutcomeResolved)
            {
                record.SetResolvedOutputs(outputExecution.ResolveAll(
                    recipe,
                    facility,
                    worker,
                    record.batchIntegrity,
                    ProductionAutomaticQualityRules.ResolveScoreCeiling(
                        facility.AutomaticQualityScoreCeiling,
                        ResolveCycleRequiredWork(recipe),
                        record.workerContributions)));
            }
            foreach (ProductionResolvedOutputSaveData output in record.resolvedOutputs)
            {
                while (output.committedAmount < output.amount)
                {
                    if (string.IsNullOrEmpty(output.pendingCommitId))
                    {
                        record.BeginResolvedOutputUnit(
                            output.outputLineId,
                            ProductionOutputCommitIdentity.Format(
                                record.billId,
                                record.cycleSequence,
                                output.outputLineId,
                                output.itemId,
                                output.committedAmount));
                    }
                    string commitId = output.pendingCommitId;
                    bool captureExact = recipeExecutionReceipts
                        .RequiresExactCapture(
                            record.billId,
                            record.cycleSequence);
                    ProductionCommittedOutputSnapshot diagnosticUnit = null;
                    if (!output.pendingCommitApplied)
                    {
                        DomainFailure outputFailure = outputExecution.ProduceOne(
                            recipe,
                            facility,
                            worker,
                            output,
                            record.outputDestinationId,
                            commitId,
                            out diagnosticUnit,
                            out ProductionOutputPublicationExposure
                                publicationExposure);
                        if (outputFailure.IsFailure)
                        {
                            if (publicationExposure ==
                                ProductionOutputPublicationExposure.None)
                            {
                                record.AbortUnpublishedResolvedOutputUnit(
                                    output.outputLineId,
                                    commitId);
                            }
                            record.SetReservedWorker(string.Empty);
                            return new ProductionWorkExecutionResult(
                                false,
                                false,
                                ProductionBillOutcomeCode.None,
                                outputFailure);
                        }
                        record.MarkResolvedOutputUnitCommitted(
                            output.outputLineId,
                            commitId,
                            diagnosticUnit);
                    }
                    else if (captureExact)
                    {
                        diagnosticUnit = output.pendingOutputPublication
                            ?.ToRuntimeSnapshot();
                    }
                    if (captureExact
                        && !recipeExecutionReceipts.TryCaptureExactCommittedUnit(
                            record.billId,
                            record.cycleSequence,
                            record.recipeId,
                            record.buildingInstanceId,
                            output,
                            diagnosticUnit,
                            out string unitReceiptFailure))
                    {
                        record.SetReservedWorker(string.Empty);
                        return new ProductionWorkExecutionResult(
                            false,
                            false,
                            ProductionBillOutcomeCode.None,
                            new DomainFailure(
                                FailureCode.ProductionOutputUnavailable,
                                record.billId.Value,
                                string.IsNullOrEmpty(unitReceiptFailure)
                                    ? "recipe-exact-unit-receipt-failed"
                                    : unitReceiptFailure));
                    }
                    DomainFailure acknowledgeFailure = outputExecution.AcknowledgeOne(
                        output,
                        commitId);
                    if (acknowledgeFailure.IsFailure)
                    {
                        record.SetReservedWorker(string.Empty);
                        return new ProductionWorkExecutionResult(
                            false,
                            false,
                            ProductionBillOutcomeCode.None,
                            acknowledgeFailure);
                    }
                    record.ClearResolvedOutputPendingCommit(
                        output.outputLineId,
                        commitId);
                }
            }

            record.ClearOutputReservations();
            if (recipeExecutionReceipts.RequiresExactCapture(
                    record.billId,
                    record.cycleSequence)
                && !recipeExecutionReceipts.TryFinalizeExactCompleted(
                    record.billId,
                    record.cycleSequence,
                    record.recipeId,
                    record.buildingInstanceId,
                    record.wipInputCommitId,
                    record.wipInputQuantity,
                    record.wipInputMassGrams,
                    record.resolvedOutputs,
                    out string exactReceiptFailure))
            {
                record.SetReservedWorker(string.Empty);
                return new ProductionWorkExecutionResult(
                    false,
                    false,
                    ProductionBillOutcomeCode.None,
                    new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        string.IsNullOrEmpty(exactReceiptFailure)
                            ? "recipe-exact-cycle-receipt-failed"
                            : exactReceiptFailure));
            }
            if (!recipeExecutionReceipts.TryCommitExactCompletedOutcome(
                    record.billId,
                    record.cycleSequence,
                    record.recipeId,
                    record.buildingInstanceId,
                    worker?.AuthorityKind == ProductionWorkerAuthorityKind.Actor
                        ? workerId
                        : string.Empty,
                    record.resolvedOutputs,
                    out string gameplayOutcomeFailure))
            {
                record.SetReservedWorker(string.Empty);
                return new ProductionWorkExecutionResult(
                    false,
                    false,
                    ProductionBillOutcomeCode.None,
                    new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        record.billId.Value,
                        string.IsNullOrEmpty(gameplayOutcomeFailure)
                            ? "production-gameplay-outcome-commit-failed"
                            : gameplayOutcomeFailure));
            }
            record.ClearResolvedOutputs();
        }

        record.ClearSelectedSupplies();
        record.SetCompletedWork(0f);
        record.SetMaterialsConsumed(false);
        record.ClearWipInput();
        record.AdvanceCycleSequence();
        record.ClearProcessFluid();
        record.SetBatchStage(recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            ? ProductionBatchStage.Preparing
            : ProductionBatchStage.None);
        record.SetRemainingProcessingHours(0f);
        record.SetBatchIntegrity(100f);
        record.SetUtilityOutageHours(0f);
        record.SetTemperatureOutageHours(0f);
        record.SetOccupiedSupportNode(string.Empty);
        record.SetBlockedFailure(DomainFailure.None);
        record.SetReservedWorker(string.Empty);
        record.ReplaceWorkerContributions(
            Array.Empty<CraftContributionSaveData>());
        if (record.mode == ProductionOrderMode.RepeatCount)
        {
            record.SetRepeatCount(record.remainingCycles - 1);
        }
        ApplyPendingModeTransition(record, facility);

        bool finished = !ShouldRunAnotherCycle(record, recipe);
        if (finished && record.mode == ProductionOrderMode.RepeatCount)
        {
            TryRetireFinishedRepeatCountBill(record, facility);
        }
        else
        {
            RequestMissingInputs(record, recipe, facility);
        }

        Touch(recipe.WorkTypeId, requestWorker: !finished);
        workforceReplanService.RequestOneHaulerToReplan(forceInterrupt: false);
        return SuccessfulExecution(
            cycleCompleted: true,
            outcome: ProductionBillOutcomeCode.CycleCompleted);
    }

    private static bool HasLegacyOutputAuthority(
        ProductionBillRecord record) => record == null
        || record.outputOutcomeResolved
        || record.resolvedOutputs.Count != 0
        || record.outputReservations.Count != 0;

    private static DomainFailure PreparedOutputAuthorityConflict(
        ProductionBillRecord record) => new(
        FailureCode.ProductionOutputUnavailable,
        record == null ? string.Empty : record.billId.Value,
        "prepared-output-legacy-authority-conflict");

    private static ProductionWorkExecutionResult BlockPreparedOutput(
        ProductionBillRecord record,
        DomainFailure failure)
    {
        DomainFailure exact = failure.IsFailure
            ? failure
            : new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                record == null ? string.Empty : record.billId.Value,
                "prepared-output-unspecified-failure");
        if (record != null)
        {
            record.SetReservedWorker(string.Empty);
            record.SetBlockedFailure(exact);
        }
        return new ProductionWorkExecutionResult(
            false,
            false,
            ProductionBillOutcomeCode.None,
            exact);
    }

    private static ProductionWorkExecutionResult SuccessfulExecution(
        bool cycleCompleted,
        ProductionBillOutcomeCode outcome)
    {
        return new ProductionWorkExecutionResult(
            true,
            cycleCompleted,
            outcome,
            DomainFailure.None);
    }

    private static ProductionWorkExecutionResult FailedExecution(
        FailureCode code,
        params string[] parameters)
    {
        return new ProductionWorkExecutionResult(
            false,
            false,
            ProductionBillOutcomeCode.None,
            new DomainFailure(code, parameters));
    }

    public void Tick()
    {
        FlushPendingCommandOutcomes();
        ReconcileOutputDestinationAuthorities();
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        FinalizeDeliveredStockSensors();
        ReconcileFinishedOutputBills();
        ReconcileMissingFacilities();
        float elapsedHours = clock.DeltaTime
            / GameSimulationTimeRules.SecondsPerGameHour;
        foreach (ProductionBillRecord record in bills.ToArray())
        {
            ProductionRecipeSO recipe = ResolveRecipe(record);
            if (facilityMutationEpoch.IsFrozen(record.buildingInstanceId))
            {
                continue;
            }
            if (recipe == null
                || recipe.ProcessKind != ProductionProcessKind.PassiveBatch
                || record.batchStage != ProductionBatchStage.Processing)
            {
                continue;
            }

            ProductionFacilityHandle facility = ResolveFacility(record);
            if (facility == null)
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionWorkstationMissing,
                    record.buildingInstanceId.Value));
                continue;
            }

            if (!TryValidateProcessingUtilities(
                    record,
                    recipe,
                    facility,
                    out string utilityFailure))
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionUtilitiesUnavailable));
                record.SetUtilityOutageHours(ApplyOutageDecay(
                    record.utilityOutageHours,
                    elapsedHours,
                    SafeUtilityOutageHours,
                    5f,
                    record));

                TryConvertRuinedBatch(record, recipe, facility);
                continue;
            }

            record.SetUtilityOutageHours(0f);
            ProductionFacilityHandle temperatureTarget =
                ResolveOccupiedBatchSupport(record, facility) ?? facility;
            float temperatureSpeed = ResolveTemperatureSpeed(
                recipe,
                temperatureTarget,
                out bool dangerous);
            if (dangerous)
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionUtilitiesUnavailable,
                    "temperature-dangerous"));
                record.SetTemperatureOutageHours(ApplyOutageDecay(
                    record.temperatureOutageHours,
                    elapsedHours,
                    DangerousTemperatureGraceHours,
                    5f,
                    record));

                TryConvertRuinedBatch(record, recipe, facility);
                continue;
            }

            record.SetTemperatureOutageHours(0f);
            record.SetBlockedFailure(temperatureSpeed < 1f
                ? new DomainFailure(
                    FailureCode.ProductionUtilitiesUnavailable,
                    "temperature-slow")
                : DomainFailure.None);
            if (temperatureSpeed < 1f)
            {
                record.SetBatchIntegrity(Mathf.Max(
                    0f,
                    record.batchIntegrity - elapsedHours));
            }

            record.SetRemainingProcessingHours(Mathf.Max(
                0f,
                record.remainingProcessingHours
                    - elapsedHours * temperatureSpeed));
            if (TryConvertRuinedBatch(record, recipe, facility)
                || record.remainingProcessingHours > 0.001f)
            {
                continue;
            }

            record.SetBatchStage(ProductionBatchStage.Finishing);
            record.SetCompletedWork(0f);
            record.SetReservedWorker(string.Empty);
            record.SetBlockedFailure(DomainFailure.None);
            if (recipe.FinishingWork > 0f)
            {
                Touch(recipe.WorkTypeId, requestWorker: true);
            }
            else
            {
                ExecuteWork(
                    ProductionWorkerHandle.PassiveProcessor,
                    facility,
                    record.billId,
                    0f);
            }
        }
    }

    private void ReconcileOutputDestinationAuthorities()
    {
        int currentVersion = buildingWorld.BuildingVersion;
        if (currentVersion == lastOutputAuthorityBuildingVersion)
            return;
        preparedOutputExecution.RestoreDestinationAuthorities(
            bills,
            buildingWorld.Facilities);
        if (!stockSensors.TryReconcileDestinationAuthorities(
                out string sensorFailure))
        {
            throw new InvalidOperationException(
                "Production stock-sensor destination authorities could not be reconciled: "
                + sensorFailure);
        }
        lastOutputAuthorityBuildingVersion = currentVersion;
    }

    private void ReconcileMissingFacilities()
    {
        bool changed = false;
        foreach (ProductionBillRecord record in bills.ToArray())
        {
            if (facilityMutationEpoch.IsFrozen(record.buildingInstanceId))
                continue;
            bool usesPreparedOutput = UsesPreparedOutput(record);
            if (usesPreparedOutput
                && !CanRetirePreparedOutputBill(record))
            {
                // The durable routing authority still owns completed physical
                // output from at least one cycle. Facility loss must not turn
                // bill cleanup into remote output deletion or an owner orphan.
                continue;
            }
            if (ResolveFacility(record) != null)
            {
                continue;
            }
            if (usesPreparedOutput)
            {
                if (HasLegacyOutputAuthority(record))
                {
                    record.SetBlockedFailure(
                        PreparedOutputAuthorityConflict(record));
                    continue;
                }
                ProductionPreparedOutputReleaseResult release =
                    preparedOutputExecution.Release(
                        record,
                        ProductionWipTerminalReason.FacilityDestroyed);
                if (!release.IsValid
                    || !release.Released
                    || record.preparedOutput?.phase !=
                        ProductionPreparedOutputPhase.Unresolved)
                {
                    record.SetBlockedFailure(release.IsValid
                        && release.Failure.IsFailure
                            ? release.Failure
                            : new DomainFailure(
                                FailureCode.ProductionOutputUnavailable,
                                record.billId.Value,
                                release.IsValid
                                    && release.PhysicalBatchCommitted
                                        ? "prepared-output-physical-batch-retained"
                                        : release.IsValid && release.Released
                                            ? "prepared-output-release-state-mismatch"
                                            : "prepared-output-release-invalid-result"));
                    continue;
                }
            }
            if (record.materialsConsumed
                && !TryCommitWipTerminalDisposition(
                    record,
                    ProductionWipTerminalReason.FacilityDestroyed))
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    record.billId.Value,
                    "wip-terminal-receipt-conflict"));
                continue;
            }
            ProductionRecipeSO missingFacilityRecipe = ResolveRecipe(record)
                ?? throw new InvalidOperationException(
                    $"Production bill '{record.billId.Value}' recipe is missing while its facility is reconciled.");
            bool hadInputDestination = RequiresPhysicalInputDestination(
                    record,
                    missingFacilityRecipe,
                    facility: null)
                || inputDestinationClaims.TryValidateClaim(record, out _);
            if (hadInputDestination
                && !items.TryReleaseDestinationAtomically(
                    record.materialDestinationId,
                    Vector2Int.zero,
                    out _,
                    out string releaseFailure))
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    record.billId.Value,
                    releaseFailure));
                continue;
            }
            RevokeInputDestinationClaimOrThrow(
                record,
                hadInputDestination,
                "revoked after facility loss");
            stateStore.RemoveBill(record);
            changed = true;
        }
        if (changed)
        {
            Touch(default, requestWorker: false);
        }
    }

    private void ReconcileFinishedOutputBills()
    {
        bool changed = false;
        foreach (ProductionBillRecord record in bills.ToArray())
        {
            if (facilityMutationEpoch.IsFrozen(record.buildingInstanceId))
                continue;
            if (!IsTerminalOutputRepeatCount(record))
            {
                continue;
            }

            ProductionFacilityHandle facility = ResolveFacility(record);
            if (TryRetireFinishedRepeatCountBill(record, facility))
            {
                changed = true;
            }
        }

        if (changed)
        {
            Touch(default, requestWorker: false);
        }
    }

    private bool TryRetireFinishedRepeatCountBill(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        if (record == null
            || record.mode != ProductionOrderMode.RepeatCount
            || record.remainingCycles > 0)
        {
            return false;
        }
        if (UsesPreparedOutput(record)
            && !CanRetirePreparedOutputBill(record))
        {
            return false;
        }
        if (!UsesPreparedOutput(record) && HasBufferedExactOutput(record))
        {
            // Exact stateful capabilities publish a physical lot into the
            // facility output buffer before returning success. The bill is the
            // only routing policy owner until distribution releases that lot;
            // retiring it here would leave a permanent orphan in the buffer.
            // Its consumed input destination is a separate authority and must
            // not stay claimed while only output routing remains outstanding.
            TryReleaseTerminalExactInputAuthorities(record, facility);
            return false;
        }

        if (!UsesPreparedOutput(record))
        {
            if (!TryReleaseTerminalExactInputAuthorities(record, facility))
                return false;

            stateStore.RemoveBill(record);
            return true;
        }

        ProductionRecipeSO terminalRecipe = ResolveRecipe(record)
            ?? throw new InvalidOperationException(
                $"Production bill '{record.billId.Value}' recipe is missing during terminal retirement.");
        bool requiresInputDestination = RequiresPhysicalInputDestination(
            record,
            terminalRecipe,
            facility);
        if (requiresInputDestination
            && !items.TryReleaseDestinationAtomically(
                record.materialDestinationId,
                facility?.Position ?? Vector2Int.zero,
                out _,
                out string releaseFailure))
        {
            record.SetBlockedFailure(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                record.billId.Value,
                releaseFailure));
            return false;
        }

        RevokeInputDestinationClaimOrThrow(
            record,
            requiresInputDestination,
            "revoked during terminal retirement");
        stateStore.RemoveBill(record);
        return true;
    }

    private bool TryReleaseTerminalExactInputAuthorities(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        bool hasInputDestination = inputDestinationClaims.TryValidateClaim(
            record,
            out _);
        if (hasInputDestination
            && !items.TryReleaseDestinationAtomically(
                record.materialDestinationId,
                facility?.Position ?? Vector2Int.zero,
                out _,
                out string releaseFailure))
        {
            record.SetBlockedFailure(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                record.billId.Value,
                releaseFailure));
            return false;
        }

        if (!inputDestinationClaims.TryRevokeIfPresent(
                record,
                out string claimFailure))
        {
            record.SetBlockedFailure(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                record.billId.Value,
                claimFailure));
            return false;
        }

        return true;
    }

    private bool TryGetFrozenMutationFailure(
        ProductionBillRecord record,
        out ProductionBillCommandResult failure) =>
        TryGetFrozenMutationFailure(
            record?.buildingInstanceId ?? default,
            out failure);

    private bool TryGetFrozenMutationFailure(
        ProductionFacilityHandle facility,
        out ProductionBillCommandResult failure) =>
        TryGetFrozenMutationFailure(
            facility?.InstanceId ?? default,
            out failure);

    private bool TryGetFrozenMutationFailure(
        BuildingInstanceId facilityId,
        out ProductionBillCommandResult failure)
    {
        failure = default;
        if (!facilityId.IsValid || !facilityMutationEpoch.IsFrozen(facilityId))
            return false;

        failure = ProductionBillCommandResult.Failed(
            new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                facilityId.Value,
                "facility-mutation-frozen"));
        return true;
    }

    private bool CanRetirePreparedOutputBill(ProductionBillRecord record)
    {
        bool outstanding = preparedOutputRouting.HasOutstandingForBill(
            record.billId);
        bool canRetire = preparedOutputRouting.CanRetireBill(record.billId);
        if (outstanding && canRetire)
        {
            throw new InvalidOperationException(
                $"Prepared-output routing authority returned conflicting retirement state for '{record.billId.Value}'.");
        }
        return !outstanding && canRetire;
    }

    private bool IsTerminalOutputRepeatCount(
        ProductionBillRecord record) => record != null
        && record.mode == ProductionOrderMode.RepeatCount
        && record.remainingCycles <= 0;

    private bool HasBufferedExactOutput(ProductionBillRecord record)
    {
        ProductionRecipeSO recipe = ResolveRecipe(record);
        if (recipe == null)
            return false;
        return recipe.CaptureCanonicalOutputs()
            .Where(output => output != null
                && ProductionOutputRoleRules.IsPhysical(output.Role)
                && output.Amount > 0)
            .Select(output => output.ItemId)
            .Distinct(StringComparer.Ordinal)
            .Any(itemId => items.CountBufferedOutput(
                itemId,
                record.outputDestinationId) > 0);
    }

    private bool TryCommitWipTerminalDisposition(
        ProductionBillRecord record,
        ProductionWipTerminalReason reason)
    {
        if (record == null)
        {
            return true;
        }

        long committedOutputMassGrams;
        long availableMassGrams;
        long accountedMassGrams;
        long declaredLossMassGrams;
        try
        {
            committedOutputMassGrams = record.resolvedOutputs
                .Where(output => output != null)
                .Aggregate(
                    0L,
                    (total, output) => checked(
                        total + output.committedMassGrams));
            availableMassGrams = checked(
                record.wipInputMassGrams
                + record.processCleanWaterMassGrams);
            accountedMassGrams = checked(
                committedOutputMassGrams
                + record.processWastewaterMassGrams);
            declaredLossMassGrams = checked(
                availableMassGrams - accountedMassGrams);
        }
        catch (OverflowException)
        {
            return false;
        }
        if (availableMassGrams == 0L)
        {
            return true;
        }
        if (declaredLossMassGrams < 0L)
        {
            return false;
        }
        return stateStore.AddWipTerminalReceipt(
            new ProductionWipTerminalReceiptSaveData
            {
                commitId = ProductionBillStateCodec.BuildWipTerminalCommitId(
                    record.billId.Value,
                    record.cycleSequence,
                    reason),
                billId = record.billId.Value,
                recipeId = record.recipeId,
                buildingInstanceId = record.buildingInstanceId.Value,
                cycleSequence = record.cycleSequence,
                inputCommitId = record.wipInputCommitId,
                inputQuantity = record.wipInputQuantity,
                inputMassGrams = record.wipInputMassGrams,
                processCleanWaterMassGrams =
                    record.processCleanWaterMassGrams,
                processWastewaterMassGrams =
                    record.processWastewaterMassGrams,
                wastewaterComponents = record.processWastewaterComponents
                    .OrderBy(value => (int)value.composition)
                    .ThenBy(value => (int)value.sourceKind)
                    .ThenBy(value => value.sourceStableId, StringComparer.Ordinal)
                    .Select(value => value.Clone())
                    .ToList(),
                committedOutputMassGrams = committedOutputMassGrams,
                reason = reason,
                lossKind =
                    ProductionWipTerminalLossKind.ExplicitIrrecoverableProcessLoss,
                declaredLossMassGrams = declaredLossMassGrams
            });
    }

    private void FinalizeDeliveredStockSensors()
    {
        HashSet<string> installedBefore = stockSensors.InstalledFacilityIds
            .ToHashSet(StringComparer.Ordinal);
        int version = stockSensors.Version;
        stockSensors.FinalizeDeliveredSensors();
        if (stockSensors.Version != version)
        {
            HashSet<string> installedAfter = stockSensors.InstalledFacilityIds
                .ToHashSet(StringComparer.Ordinal);
            Dictionary<string, ProductionFacilityHandle> facilities =
                (items.Facilities ?? Array.Empty<ProductionFacilityHandle>())
                .Where(value => value != null && value.InstanceId.IsValid)
                .GroupBy(value => value.InstanceId.Value, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
            foreach (string facilityId in installedAfter
                         .Except(installedBefore, StringComparer.Ordinal)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                RecordCommittedSensorOutcomeAfterDomain(
                    facilities,
                    facilityId,
                    ProductionCommandOutcomeKind.StockSensorInstalled,
                    "not-installed",
                    "installed");
            }
            foreach (string facilityId in installedBefore
                         .Except(installedAfter, StringComparer.Ordinal)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                RecordCommittedSensorOutcomeAfterDomain(
                    facilities,
                    facilityId,
                    ProductionCommandOutcomeKind.StockSensorRemoved,
                    "installed",
                    "removed");
            }
            Touch(default, requestWorker: false);
        }
    }


    private bool TryValidateCycleStart(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (UsesPreparedOutput(recipe))
        {
            if (HasLegacyOutputAuthority(record))
            {
                failure = PreparedOutputAuthorityConflict(record);
                return false;
            }
            ProductionPreparedOutputCapacityResult capacity =
                preparedOutputExecution.AssessCycleStart(
                    record,
                    recipe,
                    facility);
            if (!capacity.IsValid)
            {
                failure = new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    record.billId.Value,
                    "prepared-output-capacity-invalid-result");
                return false;
            }
            if (!capacity.CanBeginCycle)
            {
                failure = capacity.Failure.IsFailure
                    ? capacity.Failure
                    : new DomainFailure(
                        FailureCode.ProductionOutputSpaceUnavailable,
                        record.outputDestinationId);
                return false;
            }
        }
        else if (!EnsureOutputReservation(
                     record,
                     recipe,
                     facility,
                     out string outputFailureReason))
        {
            failure = new DomainFailure(
                FailureCode.ProductionUtilitiesUnavailable,
                outputFailureReason);
            return false;
        }

        if (cycleUtilities.ValidateCycleRequirements(
                record,
                recipe,
                facility,
                bills,
                out string utilityFailureReason))
        {
            return true;
        }
        failure = new DomainFailure(
            FailureCode.ProductionUtilitiesUnavailable,
            utilityFailureReason);
        return false;
    }

    private bool TryValidateProcessingUtilities(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason)
    {
        return cycleUtilities.ValidateProcessingUtilities(
            record?.occupiedSupportNodeId ?? string.Empty,
            recipe,
            facility,
            out failureReason);
    }

    private bool TryConsumeCycleUtilities(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out ProductionProcessFluidReceipt receipt,
        out string failureReason)
    {
        return cycleUtilities.TryConsumeCycleUtilities(
            record,
            recipe,
            facility,
            out receipt,
            out failureReason);
    }

    private bool TryOccupyBatchSupport(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason)
    {
        if (!cycleUtilities.TryResolveBatchSupport(
            record,
            recipe,
            facility,
            bills,
            out string supportNodeId,
            out failureReason))
        {
            return false;
        }

        record.SetOccupiedSupportNode(supportNodeId);
        return true;
    }

    private float ResolveTemperatureSpeed(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out bool dangerous)
    {
        return cycleUtilities.ResolveTemperatureSpeed(
            recipe,
            facility,
            out dangerous);
    }

    private ProductionFacilityHandle ResolveOccupiedBatchSupport(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        return cycleUtilities.ResolveOccupiedBatchSupport(
            record?.occupiedSupportNodeId ?? string.Empty,
            facility);
    }


    private bool TryConvertRuinedBatch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility)
    {
        if (record.batchIntegrity > 0f)
        {
            return false;
        }

        // Ruined output follows the same capability decision as successful
        // output. A passive definition-only recipe added later therefore uses
        // the conservative prepared-output disposition without a recipe-ID
        // branch; stateful/custom output remains on its declared owner.
        bool usesPreparedRuinedBatch = UsesPreparedOutput(recipe);
        if (!usesPreparedRuinedBatch)
        {
            // No implicit Loose fallback is allowed: it has no gram admission,
            // no exact disposition receipt and can delete or create mass. A
            // stateful/custom passive recipe must declare its own ruined-batch
            // capability before this WIP can be terminally disposed.
            record.SetBlockedFailure(new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                record.billId.Value,
                "ruined-output-capability-unsupported"));
            record.SetReservedWorker(string.Empty);
            Touch(recipe.WorkTypeId, requestWorker: false);
            return true;
        }
        else
        {
            ProductionRuinedBatchExecutionResult ruinedResult =
                ruinedBatchExecution.ExecuteRuinedBatch(
                    record,
                    recipe,
                    facility);
            if (!ruinedResult.IsValid
                || !ruinedResult.BatchDispositionCompleted)
            {
                bool phaseMatches = ruinedResult.IsValid
                    && record.preparedOutput?.phase == ruinedResult.Phase;
                record.SetBlockedFailure(phaseMatches
                    && ruinedResult.Failure.IsFailure
                        ? ruinedResult.Failure
                        : new DomainFailure(
                            FailureCode.ProductionOutputUnavailable,
                            record.billId.Value,
                            ruinedResult.IsValid && !phaseMatches
                                ? "ruined-output-result-phase-mismatch"
                                : "ruined-output-incomplete-result"));
                Touch(recipe.WorkTypeId, requestWorker: false);
                return true;
            }
            if (!IsCompletedRuinedBatchDispositionValid(
                    record,
                    recipe,
                    ruinedResult))
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    record.billId.Value,
                    "ruined-output-completion-state-mismatch"));
                Touch(recipe.WorkTypeId, requestWorker: false);
                return true;
            }

            record.ClearCompletedPreparedOutput();
        }

        record.ClearSelectedSupplies();
        record.SetCompletedWork(0f);
        record.SetMaterialsConsumed(false);
        record.ClearWipInput();
        record.AdvanceCycleSequence();
        record.ClearProcessFluid();
        record.SetBatchStage(ProductionBatchStage.Preparing);
        record.SetRemainingProcessingHours(0f);
        record.SetBatchIntegrity(100f);
        record.SetUtilityOutageHours(0f);
        record.SetTemperatureOutageHours(0f);
        record.SetOccupiedSupportNode(string.Empty);
        record.SetReservedWorker(string.Empty);
        record.SetBlockedFailure(new DomainFailure(
            FailureCode.ProductionBatchRuined,
            recipe.SpoilageItemId));
        if (record.mode == ProductionOrderMode.RepeatCount)
        {
            record.SetRepeatCount(record.remainingCycles - 1);
        }

        if (!ShouldRunAnotherCycle(record, recipe)
            && record.mode == ProductionOrderMode.RepeatCount)
        {
            TryRetireFinishedRepeatCountBill(record, facility);
        }
        else
        {
            RequestMissingInputs(record, recipe, facility);
        }

        Touch(recipe.WorkTypeId, requestWorker: false);
        return true;
    }

    private static bool IsCompletedRuinedBatchDispositionValid(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionRuinedBatchExecutionResult result)
    {
        if (record == null
            || recipe == null
            || result.Phase != ProductionPreparedOutputPhase.Completed
            || record.preparedOutput?.phase != ProductionPreparedOutputPhase.Completed)
        {
            return false;
        }

        ProductionRuinedBatchDispositionPlan disposition = result.Disposition;
        long availableMassGrams;
        long preparedWasteMassGrams;
        long preparedLossMassGrams;
        int preparedWasteQuantity;
        int preparedWasteLineCount;
        int preparedLossLineCount;
        try
        {
            availableMassGrams = checked(
                record.wipInputMassGrams
                + record.processCleanWaterMassGrams);
            preparedWasteMassGrams = record.preparedOutput.lines
                .Where(line => line != null
                    && line.role == ProductionOutputRole.RecoverableWaste
                    && string.Equals(
                        line.outputLineId,
                        ProductionRuinedBatchDispositionPlan
                            .RecoverableWasteOutputLineId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        line.itemId,
                        recipe.SpoilageItemId,
                        StringComparison.Ordinal))
                .Aggregate(0L, (total, line) => checked(
                    total + line.exactMassGrams));
            preparedWasteQuantity = record.preparedOutput.lines
                .Where(line => line != null
                    && line.role == ProductionOutputRole.RecoverableWaste
                    && string.Equals(
                        line.outputLineId,
                        ProductionRuinedBatchDispositionPlan
                            .RecoverableWasteOutputLineId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        line.itemId,
                        recipe.SpoilageItemId,
                        StringComparison.Ordinal))
                .Aggregate(0, (total, line) => checked(total + line.quantity));
            preparedWasteLineCount = record.preparedOutput.lines.Count(line =>
                line != null
                && line.role == ProductionOutputRole.RecoverableWaste
                && string.Equals(
                    line.outputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    StringComparison.Ordinal));
            preparedLossMassGrams = record.preparedOutput.lines
                .Where(line => line != null
                    && line.role == ProductionOutputRole.DeclaredLoss
                    && string.Equals(
                        line.outputLineId,
                        ProductionRuinedBatchDispositionPlan
                            .DeclaredLossOutputLineId,
                        StringComparison.Ordinal))
                .Aggregate(0L, (total, line) => checked(
                    total + line.exactMassGrams));
            preparedLossLineCount = record.preparedOutput.lines.Count(line =>
                line != null
                && line.role == ProductionOutputRole.DeclaredLoss
                && string.Equals(
                    line.outputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .DeclaredLossOutputLineId,
                    StringComparison.Ordinal));
        }
        catch (OverflowException)
        {
            return false;
        }

        return disposition.AvailableMassGrams == availableMassGrams
            && disposition.ProcessWastewaterMassGrams ==
                record.processWastewaterMassGrams
            && string.Equals(
                disposition.SpoilageItemId,
                recipe.SpoilageItemId,
                StringComparison.Ordinal)
            && disposition.RecoverableWasteMassGrams == preparedWasteMassGrams
            && disposition.RecoverableWasteQuantity == preparedWasteQuantity
            && preparedWasteLineCount == 1
            && disposition.DeclaredLossMassGrams == preparedLossMassGrams
            && preparedLossLineCount ==
                (disposition.DeclaredLossMassGrams > 0L ? 1 : 0)
            && record.preparedOutput.totalPhysicalMassGrams ==
                disposition.RecoverableWasteMassGrams
            && record.preparedOutput.totalDeclaredLossMassGrams ==
                disposition.DeclaredLossMassGrams
            && checked(
                disposition.RecoverableWasteMassGrams
                + disposition.ProcessWastewaterMassGrams
                + disposition.DeclaredLossMassGrams)
                == availableMassGrams;
    }

    private float ResolveCurrentRequiredWork(
        ProductionBillRecord record,
        ProductionRecipeSO recipe)
    {
        float balancedWork = ResolveCycleRequiredWork(recipe);
        if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch)
        {
            return balancedWork;
        }

        return record.batchStage == ProductionBatchStage.Finishing
            ? (recipe.FinishingWork > 0f ? balancedWork * 0.20f : 0f)
            : (recipe.FinishingWork > 0f ? balancedWork * 0.80f : balancedWork);
    }

    private float ResolveCycleRequiredWork(ProductionRecipeSO recipe) =>
        balanceWorkCalculator?.CalculateRecipe(recipe) ?? recipe.RequiredWork;

    private static float ApplyOutageDecay(
        float accumulatedHours,
        float elapsedHours,
        float graceHours,
        float integrityLossPerHour,
        ProductionBillRecord record)
    {
        float previous = Mathf.Max(0f, accumulatedHours);
        accumulatedHours = previous + Mathf.Max(0f, elapsedHours);
        float damagingHours = Mathf.Max(0f, accumulatedHours - graceHours)
            - Mathf.Max(0f, previous - graceHours);
        if (damagingHours <= 0f)
        {
            return accumulatedHours;
        }

        record.SetBatchIntegrity(Mathf.Max(
            0f,
            record.batchIntegrity - damagingHours * integrityLossPerHour));
        return accumulatedHours;
    }

    private ProductionFacilityHandle ResolveFacility(ProductionBillRecord record)
    {
        return buildingWorld.Facilities.FirstOrDefault(building =>
            MatchesFacility(record, building));
    }

    private void QueueOrApplyModeTransition(
        ProductionBillRecord record,
        ProductionOrderMode mode)
    {
        if (record == null)
        {
            return;
        }

        bool cycleActive = record.materialsConsumed
            || record.completedWork > 0f
            || record.batchStage is ProductionBatchStage.Processing
                or ProductionBatchStage.Finishing;
        if (cycleActive)
        {
            record.RequestModeTransition(mode);
            return;
        }

        record.SetOrderMode(mode);
        record.ClearModeTransition();
    }

    private void ApplyPendingModeTransition(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        if (record == null || !record.hasPendingModeTransition)
        {
            return;
        }

        record.SetOrderMode(record.pendingMode);
        record.ClearModeTransition();
        items.ReleaseDestination(
            record.materialDestinationId,
            facility?.Position ?? Vector2Int.zero);
        record.SetPrefetchPlan(
            record.estimatedProductionCycleSeconds,
            1,
            new ProductionLogisticsStatus(
                ProductionBillOutcomeCode.OrderModeTransitionCompleted));
    }

    private bool EnsureOutputReservation(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason)
    {
        string destinationId = string.IsNullOrWhiteSpace(
            record?.outputDestinationId)
                ? ResolveOutputDestinationId(facility)
                : record.outputDestinationId;
        if (!outputPlanning.TryCreateReservation(
            recipe,
            facility,
            destinationId,
            GetOtherOutputReservations(record, destinationId),
            record?.outputReservations.Count > 0,
            out ProductionOutputReservationPlan plan,
            out failureReason))
        {
            return false;
        }

        if (record != null && record.outputReservations.Count == 0)
        {
            record.SetOutputDestination(plan.DestinationId);
            foreach (KeyValuePair<string, int> reservation in plan.Reservations)
            {
                record.SetOutputReservation(reservation.Key, reservation.Value);
            }
        }

        return true;
    }

    private Dictionary<string, int> GetOtherOutputReservations(
        ProductionBillRecord record,
        string destinationId)
    {
        return bills
            .Where(candidate => candidate != record
                && string.Equals(
                    candidate.outputDestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .SelectMany(candidate => candidate.outputReservations)
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => pair.Value),
                StringComparer.Ordinal);
    }

    private string ResolveOutputDestinationId(ProductionFacilityHandle facility)
    {
        return outputPlanning.ResolveDestinationId(facility);
    }

    public DungeonProductionBillSaveData Capture()
    {
        return ProductionBillStateCodec.Capture(
            nextBillSequence,
            stateStore.NextCommandOutcomeSequence,
            stateStore.PendingCommandOutcomes,
            bills,
            stockSensors.InstalledFacilityIds,
            stockSensors.AcknowledgedFacilityIds,
            stockSensors.PendingInstallations,
            stockSensors.InstalledSensors,
            stockSensors.PendingRemovals,
            stateStore.WipTerminalReceipts);
    }

    public ProductionBillRestoreCandidate BuildRestore(
        DungeonProductionBillSaveData snapshot)
    {
        ProductionBillRestoreCandidate candidate =
            ProductionBillStateCodec.CreateRestoreCandidate(
            snapshot,
            catalog,
            stateStore.BillVersion + 1,
            stateStore.StockSensorVersion + 1);
        foreach (ProductionBillRecord record in candidate.Bills)
        {
            ProductionRecipeSO recipe = ResolveRecipe(record)
                ?? throw new InvalidOperationException(
                    "Production restore references a missing recipe: "
                    + (record?.recipeId ?? string.Empty));
            bool usesPreparedOutput = UsesPreparedOutput(recipe);
            bool hasPreparedAuthority = record.preparedOutput != null
                && record.preparedOutput.phase !=
                    ProductionPreparedOutputPhase.Unresolved;
            if (usesPreparedOutput
                && ProductionPreparedOutputMigrationScope
                    .HasLegacyOutputAuthority(record))
            {
                throw new InvalidOperationException(
                    $"Production bill '{record.billId.Value}' restored legacy output authority for a standard prepared capability.");
            }
            if (!usesPreparedOutput && hasPreparedAuthority)
            {
                throw new InvalidOperationException(
                    $"Production bill '{record.billId.Value}' restored prepared authority for a non-standard output capability.");
            }
        }
        foreach (ProductionResolvedOutputSaveData output in candidate.Bills
                     .SelectMany(record => record.resolvedOutputs))
        {
            DomainFailure failure = outputExecution.ValidateOne(output);
            if (failure.IsFailure)
            {
                throw new InvalidOperationException(
                    "Production resolved-output capability restore validation failed: "
                    + failure.Code);
            }
        }
        foreach (ProductionPreparedOutputLineSaveData line in candidate.Bills
                     .Where(record => record.preparedOutput != null
                         && record.preparedOutput.phase !=
                            ProductionPreparedOutputPhase.Unresolved)
                     .SelectMany(record => record.preparedOutput.lines)
                     .Where(line => ProductionOutputRoleRules.IsPhysical(
                         line.role)))
        {
            ProductionOutputCapabilityDescriptor capability = new(
                line.outputLineId,
                line.itemId,
                line.outputCapabilityId,
                line.outputCapabilityVersion,
                line.outputComponentCodecId,
                line.outputComponentCodecVersion,
                line.outputCapabilityFingerprint);
            if (!items.TryValidateOutputCapability(
                    capability,
                    out DomainFailure failure))
            {
                throw new InvalidOperationException(
                    "Production prepared-output capability restore validation failed: "
                    + failure.Code);
            }
        }
        return candidate;
    }

    public void Restore(ProductionBillRestoreCandidate candidate)
    {
        Restore(candidate, buildingWorld.Facilities);
    }

    public void Restore(
        ProductionBillRestoreCandidate candidate,
        IReadOnlyList<ProductionFacilityHandle> detachedFacilities)
    {
        ProductionBillRestoreCandidate exact = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        IReadOnlyList<ProductionFacilityHandle> facilities = detachedFacilities
            ?? throw new ArgumentNullException(nameof(detachedFacilities));
        ProductionBillRecord[] inputDestinationBills = exact.Bills
            .Where(record => RequiresPhysicalInputDestination(
                record,
                facilities))
            .ToArray();
        Dictionary<string, long> inputBufferMassGramsByBillId =
            inputDestinationBills
            .OrderBy(record => record.billId.Value, StringComparer.Ordinal)
            .ToDictionary(
                record => record.billId.Value,
                record => ResolveInputBufferMassCapacity(
                    record,
                    facilities),
                StringComparer.Ordinal);
        if (!inputDestinationClaims.TryReplace(
                inputDestinationBills,
                facilities,
                inputBufferMassGramsByBillId,
                out string claimFailure))
        {
            throw new InvalidOperationException(
                "Production input destination claims could not be restored: "
                + claimFailure);
        }
        preparedOutputExecution.RestoreDestinationAuthorities(
            exact.Bills,
            facilities);
        if (!stockSensors.TryReconcileDestinationAuthorities(
                out string sensorFailure))
        {
            throw new InvalidOperationException(
                "Production stock-sensor destination authorities could not be restored: "
                + sensorFailure);
        }
        lastOutputAuthorityBuildingVersion = buildingWorld.BuildingVersion;
        stateStore.Replace(
            exact);
    }

    private void RevokeInputDestinationClaimOrThrow(
        ProductionBillRecord record,
        bool required,
        string operation)
    {
        string failureReason;
        bool revoked = required
            ? inputDestinationClaims.TryRevoke(record, out failureReason)
            : inputDestinationClaims.TryRevokeIfPresent(
                record,
                out failureReason);
        if (!revoked)
        {
            throw new InvalidOperationException(
                $"Production bill '{record?.billId.Value ?? "null"}' input destination claim could not be {operation}: {failureReason}");
        }
    }

    private ProductionBillRecord FindRunnableBill(
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId,
        bool requireDeliveredInputs,
        out DomainFailure failure,
        bool automatic = false)
    {
        DomainFailure qualityFailure = DomainFailure.None;
        var eligible = bills.Where(record =>
        {
            if (!MatchesFacility(record, facility) || record.suspended) return true;
            var recipe = ResolveRecipe(record);
            if (recipe == null || recipe.WorkTypeId != workTypeId) return true;
            DomainFailure candidate = CheckQualityTarget(record, recipe, facility, automatic);
            if (!candidate.IsFailure) return true;
            qualityFailure = candidate;
            return false;
        }).ToArray();
        var found = inputLogistics.FindRunnableBill(
            eligible,
            facility,
            workTypeId,
            requireDeliveredInputs,
            out failure);
        if (found == null && qualityFailure.IsFailure) failure = qualityFailure;
        return found;
    }

    private void RequestMissingInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility)
    {
        if (!RequiresPhysicalInputDestination(record, recipe, facility))
            return;

        long inputBufferMassGrams = inputLogistics
            .ResolveInputBufferMassCapacity(record, recipe, facility);
        if (!inputDestinationClaims.TryEnsureCapacity(
                record,
                inputBufferMassGrams,
                out string capacityFailure))
        {
            throw new InvalidOperationException(
                $"Production bill '{record?.billId.Value ?? "null"}' input-buffer capacity could not be published: {capacityFailure}");
        }
        inputLogistics.RequestMissingInputs(record, recipe, facility);
    }

    private long ResolveInputBufferMassCapacity(
        ProductionBillRecord record,
        IReadOnlyList<ProductionFacilityHandle> facilities)
    {
        ProductionRecipeSO recipe = ResolveRecipe(record)
            ?? throw new InvalidOperationException(
                $"Production bill '{record?.billId.Value ?? "null"}' recipe is missing during input-buffer restore.");
        ProductionFacilityHandle facility = (facilities
                ?? Array.Empty<ProductionFacilityHandle>())
            .SingleOrDefault(value => MatchesFacility(record, value))
            ?? throw new InvalidOperationException(
                $"Production bill '{record.billId.Value}' facility is missing during input-buffer restore.");
        return inputLogistics.ResolveInputBufferMassCapacity(
            record,
            recipe,
            facility);
    }

    private bool RequiresPhysicalInputDestination(
        ProductionBillRecord record,
        IReadOnlyList<ProductionFacilityHandle> facilities)
    {
        ProductionRecipeSO recipe = ResolveRecipe(record)
            ?? throw new InvalidOperationException(
                $"Production bill '{record?.billId.Value ?? "null"}' recipe is missing while resolving its input-destination requirement.");
        ProductionFacilityHandle facility = (facilities
                ?? Array.Empty<ProductionFacilityHandle>())
            .SingleOrDefault(value => MatchesFacility(record, value))
            ?? throw new InvalidOperationException(
                $"Production bill '{record.billId.Value}' facility is missing while resolving its input-destination requirement.");
        return RequiresPhysicalInputDestination(record, recipe, facility);
    }

    private bool RequiresPhysicalInputDestination(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility) =>
        inputLogistics.ToCycleInputMap(record, recipe, facility).Count > 0;

    private void RecalculatePrefetch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionWorkerHandle worker)
    {
        inputLogistics.RecalculatePrefetch(record, recipe, worker);
    }

    private bool ShouldRunAnotherCycle(
        ProductionBillRecord record,
        ProductionRecipeSO recipe)
    {
        return inputLogistics.ShouldRunAnotherCycle(record, recipe);
    }

    private bool IsResearchUnlocked(
        ProductionRecipeSO recipe,
        out DomainFailure failure)
    {
        return inputLogistics.IsResearchUnlocked(recipe, out failure);
    }

    private Dictionary<string, int> ToCycleInputMap(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility)
    {
        return inputLogistics.ToCycleInputMap(record, recipe, facility);
    }


    private ProductionBillSnapshot ToSnapshot(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        return snapshotProjector.Project(record, facility, bills);
    }


    private ProductionRecipeSO ResolveRecipe(ProductionBillRecord record)
    {
        return record != null
            && catalog.TryGetRecipe(record.recipeId, out ProductionRecipeSO recipe)
                ? recipe
                : null;
    }

    private bool UsesPreparedOutput(ProductionBillRecord record) =>
        UsesPreparedOutput(ResolveRecipe(record));

    private bool UsesPreparedOutput(ProductionRecipeSO recipe) =>
        ProductionPreparedOutputCapabilitySelection
            .UsesPreparedOutputMaterializer(recipe, items);

    private ProductionBillRecord Find(ProductionBillId billId)
    {
        return !billId.IsValid
            ? null
            : bills.FirstOrDefault(record =>
                record.billId.Equals(billId));
    }

    private static bool MatchesFacility(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        return record != null
            && facility != null
            && !facility.IsDestroyed
            && record.buildingInstanceId.Equals(
                facility.InstanceId);
    }

    private bool MatchesRecipeWorkstation(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe)
    {
        return workshops.MatchesWorkstation(facility, recipe);
    }

    private WipTerminalCheckpointGcCandidate RequireWipCheckpointGcCandidate(
        IProductionGenericBillWipTerminalCheckpointGcCandidate candidate)
    {
        if (candidate is not WipTerminalCheckpointGcCandidate exact
            || !ReferenceEquals(activeWipCheckpointGcCandidate, exact))
        {
            throw new InvalidOperationException(
                "WIP checkpoint-GC candidate is stale or foreign.");
        }
        return exact;
    }

    private sealed class WipTerminalCheckpointGcCandidate :
        IProductionGenericBillWipTerminalCheckpointGcCandidate
    {
        internal WipTerminalCheckpointGcCandidate(
            int expectedVersion,
            IReadOnlyList<ProductionWipTerminalReceiptSaveData> rows)
        {
            ExpectedVersion = expectedVersion;
            PublishedVersion = expectedVersion;
            Rows = (rows ?? Array.Empty<ProductionWipTerminalReceiptSaveData>())
                .Select(value => value.Clone())
                .OrderBy(value => value.commitId, StringComparer.Ordinal)
                .ToArray();
        }

        internal int ExpectedVersion { get; }
        internal int PublishedVersion { get; set; }
        internal IReadOnlyList<ProductionWipTerminalReceiptSaveData> Rows
            { get; }
        internal bool Published { get; set; }
    }

    private void RestoreFacilityBillIndex(
        ProductionBillRecord record,
        int targetLocalIndex)
    {
        List<ProductionBillRecord> facilityBills = bills
            .Where(candidate => candidate.buildingInstanceId.Equals(
                record.buildingInstanceId))
            .ToList();
        int currentLocalIndex = facilityBills.IndexOf(record);
        int clampedTarget = Mathf.Clamp(
            targetLocalIndex,
            0,
            facilityBills.Count - 1);
        if (currentLocalIndex == clampedTarget) return;
        ProductionBillRecord anchor = facilityBills[clampedTarget];
        stateStore.MoveBill(
            record,
            anchor,
            insertAfter: currentLocalIndex < clampedTarget);
    }

    private static bool WouldModeTransitionChange(
        ProductionBillRecord record,
        ProductionOrderMode target)
    {
        if (IsCycleActive(record))
        {
            return !record.hasPendingModeTransition
                || record.pendingMode != target;
        }
        return record.mode != target || record.hasPendingModeTransition;
    }

    private static bool IsCycleActive(ProductionBillRecord record) =>
        record.materialsConsumed
        || record.completedWork > 0f
        || record.batchStage is ProductionBatchStage.Processing
            or ProductionBatchStage.Finishing;

    private static string DescribeOrderState(ProductionBillRecord record) =>
        DescribeOrderState(
            record.mode,
            record.remainingCycles,
            record.minimumReserve,
            record.targetStock,
            record.hasPendingModeTransition,
            record.pendingMode);

    private static string DescribeOrderStateAfter(
        ProductionBillRecord record,
        ProductionOrderMode requestedMode,
        int remainingCycles,
        int minimumReserve,
        int targetStock)
    {
        bool active = IsCycleActive(record);
        return DescribeOrderState(
            active ? record.mode : requestedMode,
            remainingCycles,
            minimumReserve,
            targetStock,
            active,
            active ? requestedMode : record.pendingMode);
    }

    private static string DescribeOrderState(
        ProductionOrderMode mode,
        int remainingCycles,
        int minimumReserve,
        int targetStock,
        bool hasPendingMode,
        ProductionOrderMode pendingMode) =>
        $"mode={(int)mode};remaining={remainingCycles};minimum={minimumReserve};target={targetStock};pending={Bool(hasPendingMode)};pending-mode={(hasPendingMode ? (int)pendingMode : -1)}";

    private static string DescribeBillState(ProductionBillRecord record) =>
        string.Join(";",
            DescribeOrderState(record),
            "suspended=" + Bool(record.suspended),
            "minimum-quality=" + record.minimumCraftQuality,
            "distribution=" + (int)record.distributionMode,
            "worker-mode=" + (int)(record.workerPolicy?.mode
                ?? WorkerSelectionMode.Anyone),
            "emergency-worker=" + Optional(record.emergencyWorkerId));

    private static string DescribeDistributionPolicy(
        ProductionDistributionMode mode,
        IEnumerable<ProductionConsumerRoutePolicy> routes)
    {
        string values = string.Join(",", (routes
                ?? Array.Empty<ProductionConsumerRoutePolicy>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .Select(value => string.Join("|",
                value.consumerId,
                Bool(value.enabled),
                value.minimumReserve,
                value.targetStock,
                value.priority,
                value.weight,
                value.waitingSeconds.ToString("R", CultureInfo.InvariantCulture))));
        return $"mode={(int)mode};routes=[{values}]";
    }

    private static string DescribeWorkerPolicy(
        WorkerSelectionPolicySaveData policy,
        string reservedWorkerId)
    {
        WorkerSelectionPolicySaveData value = policy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        return string.Join(";",
            $"mode={(int)value.mode}",
            $"match={(int)value.matchMode}",
            $"sort={(int)value.sortMode}",
            "specific=" + string.Join(",", value.specificCharacterIds),
            "excluded=" + string.Join(",", value.excludedCharacterIds),
            "skill=" + Optional(value.minimumSkillId),
            $"skill-xp={value.minimumSkillExperience}",
            $"career={value.minimumCareerRank}",
            "required-traits=" + string.Join(",", value.requiredTraitIds),
            "excluded-traits=" + string.Join(",", value.excludedTraitIds),
            "reserved-worker=" + Optional(reservedWorkerId));
    }

    private static string Bool(bool value) => value ? "true" : "false";
    private static string Optional(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();

    private void QueueCommittedCommandOutcome(
        in ProductionCommandOutcomeSource source,
        long ownerRevision,
        IPreparedProductionCommandOutcome prepared,
        bool deliverImmediately = true)
    {
        if (prepared == null
            || prepared.OwnerRevision != ownerRevision
            || ownerRevision != stateStore.NextCommandOutcomeSequence
            || ownerRevision == long.MaxValue
            || stateStore.TryGetPendingCommandOutcome(ownerRevision, out _))
        {
            throw new InvalidOperationException(
                "Production command outcome outbox owner revision is stale or conflicting.");
        }
        ProductionCommandOutcomeOutboxSaveData pending =
            ProductionCommandOutcomeOutboxSaveData.Create(
                source,
                ownerRevision,
                prepared.FrozenContext);
        stateStore.SetPendingCommandOutcome(pending);
        stateStore.NextCommandOutcomeSequence = checked(ownerRevision + 1L);
        if (deliverImmediately)
            DeliverPendingCommandOutcome(ownerRevision, prepared);
    }

    private bool TryPrepareFacilityCommandOutcome(
        ProductionFacilityHandle facility,
        ProductionCommandOutcomeKind kind,
        string beforeValue,
        string afterValue,
        out ProductionCommandOutcomeSource source,
        out long ownerRevision,
        out IPreparedProductionCommandOutcome prepared,
        out ProductionBillCommandResult failure)
    {
        source = default;
        prepared = null;
        ownerRevision = stateStore.NextCommandOutcomeSequence;
        if (facility == null
            || facility.IsDestroyed
            || !facility.InstanceId.IsValid
            || ownerRevision <= 0L
            || ownerRevision == long.MaxValue)
        {
            failure = ProductionBillCommandResult.Failed(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                facility?.InstanceId.Value ?? string.Empty,
                facility == null || facility.IsDestroyed
                    ? "production-command-facility-missing"
                    : "production-command-owner-revision-exhausted"));
            return false;
        }
        try
        {
            source = new ProductionCommandOutcomeSource(
                kind,
                string.Empty,
                string.Empty,
                string.Empty,
                facility.InstanceId.Value,
                facility.Position.x,
                facility.Position.y,
                beforeValue,
                afterValue);
            if (!commandOutcomes.TryPrepare(
                    source,
                    ownerRevision,
                    out prepared,
                    out string prepareFailure))
            {
                failure = CommandOutcomeFailure(default, prepareFailure);
                return false;
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failure = CommandOutcomeFailure(
                default,
                "production-command-prepare-exception:"
                + exception.GetType().Name);
            return false;
        }
        failure = null;
        return true;
    }

    private void RecordCommittedSensorOutcomeAfterDomain(
        IReadOnlyDictionary<string, ProductionFacilityHandle> facilities,
        string facilityId,
        ProductionCommandOutcomeKind kind,
        string beforeValue,
        string afterValue)
    {
        ProductionBillCommandResult failure = null;
        if (facilities == null
            || !facilities.TryGetValue(
                facilityId,
                out ProductionFacilityHandle facility)
            || !TryPrepareFacilityCommandOutcome(
                facility,
                kind,
                beforeValue,
                afterValue,
                out ProductionCommandOutcomeSource source,
                out long ownerRevision,
                out IPreparedProductionCommandOutcome prepared,
                out failure))
        {
            Debug.LogError(
                "Committed stock-sensor state has no production command outcome: "
                + facilityId + ":"
                + (failure != null && failure.Failure.Parameters.Length > 0
                    ? failure.Failure.Parameters[0]
                    : "facility-or-outcome-preparation-missing"));
            return;
        }
        QueueCommittedCommandOutcome(source, ownerRevision, prepared);
    }

    private void DeliverPendingCommandOutcome(
        long ownerRevision,
        IPreparedProductionCommandOutcome prepared = null)
    {
        if (!stateStore.TryGetPendingCommandOutcome(
                ownerRevision,
                out ProductionCommandOutcomeOutboxSaveData pending))
        {
            return;
        }
        try
        {
            IPreparedProductionCommandOutcome token = prepared;
            if (token == null
                && !commandOutcomes.TryPreparePending(
                    pending,
                    out token,
                    out string prepareFailure))
            {
                Debug.LogError(
                    "Production command outcome remains pending: "
                    + ownerRevision + ":" + prepareFailure);
                return;
            }
            ProductionCommandOutcomeCommitResult result =
                commandOutcomes.Commit(token);
            if (result.DurablyCommitted)
            {
                stateStore.RemovePendingCommandOutcome(ownerRevision);
                return;
            }
            Debug.LogError(
                "Production command outcome remains pending after commit rejection: "
                + ownerRevision + ":" + result.DetailCode);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Production command outcome delivery threw and remains pending: "
                + ownerRevision + ":" + exception.GetType().Name);
        }
    }

    private void FlushPendingCommandOutcomes()
    {
        foreach (ProductionCommandOutcomeOutboxSaveData pending in
                 stateStore.PendingCommandOutcomes
                     .OrderBy(value => value.ownerRevision)
                     .Select(value => value.Clone())
                     .ToArray())
        {
            DeliverPendingCommandOutcome(pending.ownerRevision);
        }
    }

    private ProductionBillCommandResult ExecuteRecordedBillCommand(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionCommandOutcomeKind kind,
        string beforeValue,
        string afterValue,
        Action mutation,
        Action rollback,
        WorkTypeId workTypeId,
        bool requestWorker,
        ProductionBillOutcomeCode outcome = ProductionBillOutcomeCode.BillUpdated)
    {
        if (record == null
            || recipe == null
            || mutation == null
            || rollback == null)
        {
            return ProductionBillCommandResult.Failed(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                "production-command-transaction-input-invalid"));
        }
        ProductionFacilityHandle facility = ResolveFacility(record);
        long ownerRevision = stateStore.NextCommandOutcomeSequence;
        if (facility == null
            || ownerRevision <= 0L
            || ownerRevision == long.MaxValue)
        {
            return ProductionBillCommandResult.Failed(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                record.billId.Value,
                facility == null
                    ? "production-command-facility-missing"
                    : "production-command-owner-revision-exhausted"));
        }

        IPreparedProductionCommandOutcome prepared;
        string prepareFailure;
        try
        {
            ProductionCommandOutcomeSource source = new(
                kind,
                record.billId.Value,
                recipe.RecipeId,
                recipe.DisplayName,
                facility.InstanceId.Value,
                facility.Position.x,
                facility.Position.y,
                beforeValue,
                afterValue);
            if (!commandOutcomes.TryPrepare(
                    source,
                    ownerRevision,
                    out prepared,
                    out prepareFailure))
            {
                return CommandOutcomeFailure(record.billId, prepareFailure);
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return CommandOutcomeFailure(
                record.billId,
                "production-command-prepare-exception:"
                + exception.GetType().Name);
        }

        try
        {
            mutation();
            stateStore.NextCommandOutcomeSequence = checked(ownerRevision + 1L);
            ProductionCommandOutcomeCommitResult committed =
                commandOutcomes.Commit(prepared);
            if (!committed.DurablyCommitted)
            {
                rollback();
                stateStore.NextCommandOutcomeSequence = ownerRevision;
                return CommandOutcomeFailure(
                    record.billId,
                    committed.DetailCode.Length == 0
                        ? "production-command-outcome-commit-rejected"
                        : committed.DetailCode);
            }
        }
        catch (Exception exception)
        {
            rollback();
            stateStore.NextCommandOutcomeSequence = ownerRevision;
            commandOutcomes.Cancel(prepared);
            return CommandOutcomeFailure(
                record.billId,
                "production-command-transaction-exception:"
                + exception.GetType().Name);
        }

        Touch(workTypeId, requestWorker);
        return ProductionBillCommandResult.Success(record.billId, outcome);
    }

    private static ProductionBillCommandResult CommandOutcomeFailure(
        ProductionBillId billId,
        string detail) => ProductionBillCommandResult.Failed(new DomainFailure(
        FailureCode.ProductionBillUnavailable,
        billId.Value,
        string.IsNullOrWhiteSpace(detail)
            ? "production-command-outcome-failed"
            : detail));

    private void Touch(WorkTypeId workTypeId, bool requestWorker)
    {
        unchecked
        {
            stateStore.IncrementBillVersion();
        }
        if (requestWorker && workTypeId.IsValid)
        {
            workforceReplanService.RequestWorkReplan(workTypeId);
        }
    }
}
