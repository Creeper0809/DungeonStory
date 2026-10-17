using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;
using static ProductionBillIdentity;

internal sealed class ProductionBillCommandRuntime
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionAssemblyBridge items;
    private readonly IProductionAssemblyBridge workforceReplanService;
    private readonly IProductionAssemblyBridge workshops;
    private readonly IProductionOutputPlanningService outputPlanning;
    private readonly IProductionAssemblyBridge inputLogistics;
    private readonly IProductionStockSensorRuntime stockSensors;
    private readonly ProductionAggregateStateStore stateStore;
    private readonly IProductionInputDestinationClaimRuntime inputDestinationClaims;
    private readonly IProductionFacilityMutationEpochQuery facilityMutationEpoch;
    private readonly IDomainFactStagingPublisher commandObservations;
    private readonly IDomainFactRecoveryPublisher commandRecovery;
    private readonly IGameCalendar calendar;
    private readonly ProductionBillRuntimeQuery billQueries;
    private readonly ProductionBillInputRuntime inputRuntime;

    private bool publishingCommand;
    private IReadOnlyList<ProductionBillRecord> bills => stateStore.Bills;
    private int nextBillSequence
    {
        get => stateStore.NextBillSequence;
        set => stateStore.NextBillSequence = value;
    }

    private int Version => stateStore.BillVersion;

    internal ProductionBillCommandRuntime(
        ProductionBillOrderDependencies order,
        ProductionBillExecutionDependencies execution,
        ProductionBillRuntimeQuery billQueries,
        ProductionBillInputRuntime inputRuntime)
    {
        this.catalog = order.Catalog;
        this.items = order.Bridge;
        this.workforceReplanService = order.Bridge;
        this.workshops = order.Bridge;
        this.outputPlanning = execution.OutputPlanning;
        this.inputLogistics = order.Bridge;
        this.stockSensors = order.StockSensors;
        this.stateStore = order.StateStore;
        this.inputDestinationClaims = order.InputDestinationClaims;
        this.facilityMutationEpoch = order.FacilityMutationEpoch;
        this.commandObservations = order.CommandObservations;
        this.commandRecovery = order.CommandRecovery;
        this.calendar = order.Calendar;
        this.billQueries = billQueries;
        this.inputRuntime = inputRuntime;
    }

    [GameplayInternalOnly("Shared production quality command", "ProductionBillSceneFacade")]
    public ProductionBillCommandResult SetMinimumCraftQuality(ProductionBillId billId, int minimumTier)
    {
        ProductionBillRecord record = billQueries.Find(billId);
        if (record == null)
            return ProductionBillCommandResult.Failed(new DomainFailure(FailureCode.ProductionBillMissing, billId.Value));
        if (TryGetFrozenMutationFailure(record, out var frozen)) return frozen;
        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
        if (!ProductionQualityTargetRules.IsValidTarget(minimumTier)
            || record.materialsConsumed || record.completedWork > 0f || record.outputOutcomeResolved
            || (minimumTier >= 0 && ProductionQualityTargetRules.CaptureTier(recipe,
                billQueries.ResolveFacility(record), items, outputPlanning, 100f) < 0))
            return ProductionBillCommandResult.Failed(new DomainFailure(FailureCode.ProductionBillUnavailable,
                "quality-target-invalid-unsupported-or-cycle-in-progress"));
        if (record.minimumCraftQuality == minimumTier
            && !record.blockedFailure.IsFailure)
        {
            return ProductionBillCommandResult.Success(billId);
        }
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteObservedBillCommand(
            record,
            recipe,
            ProductionCommandKind.MinimumCraftQualityChanged,
            CaptureCommandState(record),
            ProjectCommandState(record, value => { value.minimumQuality = minimumTier; value.blockedCode = 0; }),
            () =>
            {
                record.SetMinimumCraftQuality(minimumTier);
                record.SetBlockedFailure(DomainFailure.None);
            },
            () => record.RestoreConfigurationState(snapshot),
            recipe.WorkTypeId,
            requestWorker: true);
    }

    public ProductionBillCommandResult AddBill(
        ProductionFacilityHandle facility,
        string recipeId,
        ProductionOrderMode mode,
        int amount)
    {
        AssertCommandObservationIdle();
        if (TryGetPendingCommandFailure(out var pendingCommand)) return pendingCommand;
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

        if (!billQueries.MatchesRecipeWorkstation(facility, recipe))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionWorkstationMismatch,
                    recipe.RecipeId,
                    facilityId.Value));
        }

        if (!inputRuntime.IsResearchUnlocked(recipe, out DomainFailure researchFailure))
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
        record.SetOutputDestination(inputRuntime.ResolveOutputDestinationId(facility));
        long ownerRevision = stateStore.NextCommandSequence;
        if (ownerRevision <= 0L || ownerRevision == long.MaxValue)
        {
            return ProductionBillCommandResult.Failed(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                billId.Value,
                "production-command-owner-revision-exhausted"));
        }
        ProductionCommandAppliedFact outcomeSource = CreateCommandFact(
            ProductionCommandKind.BillAdded,
            record, recipe, facility, new ProductionCommandStateData(), CaptureCommandState(record), ownerRevision);
        if (!TryStageCommand(
                outcomeSource,
                out IDomainFactStage preparedOutcome,
                out string outcomePrepareFailure))
        {
            return CommandObservationFailure(billId, outcomePrepareFailure);
        }
        try
        {
            bool requiresInputDestination = inputRuntime.RequiresPhysicalInputDestination(
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
                    CancelCommandStage(preparedOutcome);
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
                inputRuntime.RequestMissingInputs(record, recipe, facility);
                QueueCommittedCommandObservation(
                    outcomeSource,
                    ownerRevision,
                    preparedOutcome);
            }
            catch
            {
                stateStore.RemovePendingCommandObservation(ownerRevision);
                items.ReleaseDestination(record.materialDestinationId, facility.Position);
                stateStore.RemoveBill(record);
                nextBillSequence = sequence;
                stateStore.NextCommandSequence = ownerRevision;
                CancelCommandStage(preparedOutcome);
                inputRuntime.RevokeInputDestinationClaimOrThrow(
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
        finally { CancelCommandStage(preparedOutcome); }
    }

    public ProductionBillCommandResult MoveBill(
        ProductionBillId billId,
        int targetIndex)
    {
        ProductionBillRecord record = billQueries.Find(billId);
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
        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
        return ExecuteObservedBillCommand(
            record,
            recipe,
            ProductionCommandKind.BillPriorityChanged,
            new ProductionCommandStateData { localIndex = currentLocalIndex },
            new ProductionCommandStateData { localIndex = clampedTarget },
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
        ProductionBillRecord record = billQueries.Find(billId);
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
        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteObservedBillCommand(
            record,
            recipe,
            ProductionCommandKind.SuspensionChanged,
            CaptureCommandState(record),
            ProjectCommandState(record, value => { value.suspended = suspended; value.reservedWorkerId = string.Empty; }),
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
        ProductionBillRecord record = billQueries.Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillMissing,
                    billId.Value));
        }
        if (TryGetFrozenMutationFailure(record, out var frozen))
            return frozen;

        ProductionFacilityHandle facility = billQueries.ResolveFacility(record);
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
        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteObservedBillCommand(
            record,
            recipe,
            ProductionCommandKind.StockPolicyChanged,
            CaptureCommandState(record),
            ProjectOrderStateAfter(
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
        ProductionBillRecord record = billQueries.Find(billId);
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
            && !HasStockSensor(billQueries.ResolveFacility(record)))
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

        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteObservedBillCommand(
            record,
            recipe,
            ProductionCommandKind.OrderModeChanged,
            CaptureCommandState(record),
            ProjectOrderStateAfter(
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
        ProductionBillRecord record = billQueries.Find(billId);
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
        var beforePolicy = CaptureCommandState(record);
        var afterPolicy = ProjectCommandState(record, value =>
        { value.distributionMode = mode; value.routes = normalizedRoutes.Select(route => route.Clone()).ToList(); });
        if (beforePolicy.SameAs(afterPolicy))
            return ProductionBillCommandResult.Success(billId);

        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteObservedBillCommand(
            record,
            recipe,
            ProductionCommandKind.DistributionPolicyChanged,
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
        ProductionBillRecord record = billQueries.Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionBillMissing, billId.Value));
        }
        if (TryGetFrozenMutationFailure(record, out var frozen))
            return frozen;
        WorkerSelectionPolicySaveData normalized = policy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        var beforePolicy = CaptureCommandState(record);
        var afterPolicy = ProjectCommandState(record, value =>
        { value.workerPolicy = normalized.CloneNormalized(); value.reservedWorkerId = string.Empty; });
        if (beforePolicy.SameAs(afterPolicy))
            return ProductionBillCommandResult.Success(billId);

        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteObservedBillCommand(
            record,
            recipe,
            ProductionCommandKind.WorkerPolicyChanged,
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
        ProductionBillRecord record = billQueries.Find(billId);
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
        var beforeState = CaptureCommandState(record);
        var afterState = ProjectCommandState(record, value =>
        { value.emergencyWorkerId = normalized; value.workerPolicy = resultingPolicy.CloneNormalized(); value.reservedWorkerId = string.Empty; });
        if (beforeState.SameAs(afterState))
            return ProductionBillCommandResult.Success(billId);

        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
        IProductionBillConfigurationState snapshot =
            record.CaptureConfigurationState();
        return ExecuteObservedBillCommand(
            record,
            recipe,
            ProductionCommandKind.EmergencyWorkerChanged,
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
        if (!TryPrepareFacilityCommandFact(
                facility,
                ProductionCommandKind.StockSensorInstalled,
                new ProductionCommandStateData(),
                new ProductionCommandStateData { sensorInstalled = true },
                out ProductionCommandAppliedFact outcomeSource,
                out long ownerRevision,
                out IDomainFactStage prepared,
                out ProductionBillCommandResult prepareFailure))
        {
            return prepareFailure;
        }
        try
        {
            int version = stockSensors.Version;
            ProductionBillCommandResult result =
                stockSensors.RequestInstallation(facility);
            if (!wasInstalled && stockSensors.Has(facility))
            {
                QueueCommittedCommandObservation(
                    outcomeSource,
                    ownerRevision,
                    prepared);
            }
            else
            {
                CancelCommandStage(prepared);
            }
            if (stockSensors.Version != version)
            {
                Touch(default, requestWorker: false);
            }

            return result;
        }
        finally { CancelCommandStage(prepared); }
    }

    public ProductionBillCommandResult RemoveStockSensor(
        ProductionFacilityHandle facility)
    {
        if (TryGetFrozenMutationFailure(facility, out var frozen))
            return frozen;
        bool wasInstalled = stockSensors.Has(facility);
        if (!wasInstalled)
            return stockSensors.Remove(facility);
        if (!TryPrepareFacilityCommandFact(
                facility,
                ProductionCommandKind.StockSensorRemoved,
                new ProductionCommandStateData { sensorInstalled = true },
                new ProductionCommandStateData(),
                out ProductionCommandAppliedFact outcomeSource,
                out long ownerRevision,
                out IDomainFactStage prepared,
                out ProductionBillCommandResult prepareFailure))
        {
            return prepareFailure;
        }
        try
        {
            int version = stockSensors.Version;
            ProductionBillCommandResult result = stockSensors.Remove(facility);
            if (wasInstalled && !stockSensors.Has(facility))
            {
                QueueCommittedCommandObservation(
                    outcomeSource,
                    ownerRevision,
                    prepared);
            }
            else
            {
                CancelCommandStage(prepared);
            }
            if (stockSensors.Version != version)
            {
                Touch(default, requestWorker: false);
            }

            return result;
        }
        finally { CancelCommandStage(prepared); }
    }

    public ProductionBillCommandResult AcknowledgeStockSensorUnlock(
        ProductionFacilityHandle facility)
    {
        if (TryGetFrozenMutationFailure(facility, out var frozen))
            return frozen;
        bool wasAcknowledged = stockSensors.IsAcknowledged(facility);
        if (wasAcknowledged)
            return stockSensors.Acknowledge(facility);
        if (!TryPrepareFacilityCommandFact(
                facility,
                ProductionCommandKind.StockSensorUnlockAcknowledged,
                new ProductionCommandStateData { sensorInstalled = true },
                new ProductionCommandStateData { sensorInstalled = true, sensorAcknowledged = true },
                out ProductionCommandAppliedFact outcomeSource,
                out long ownerRevision,
                out IDomainFactStage prepared,
                out ProductionBillCommandResult prepareFailure))
        {
            return prepareFailure;
        }
        try
        {
            int version = stockSensors.Version;
            ProductionBillCommandResult result = stockSensors.Acknowledge(facility);
            if (!wasAcknowledged && stockSensors.IsAcknowledged(facility))
            {
                QueueCommittedCommandObservation(
                    outcomeSource,
                    ownerRevision,
                    prepared);
            }
            else
            {
                CancelCommandStage(prepared);
            }
            if (stockSensors.Version != version)
            {
                Touch(default, requestWorker: false);
            }

            return result;
        }
        finally { CancelCommandStage(prepared); }
    }

    internal bool TryGetFrozenMutationFailure(
        ProductionBillRecord record,
        out ProductionBillCommandResult failure) =>
        TryGetFrozenMutationFailure(
            record?.buildingInstanceId ?? default,
            out failure);

    internal bool TryGetFrozenMutationFailure(
        ProductionFacilityHandle facility,
        out ProductionBillCommandResult failure) =>
        TryGetFrozenMutationFailure(
            facility?.InstanceId ?? default,
            out failure);

    internal bool TryGetFrozenMutationFailure(
        BuildingInstanceId facilityId,
        out ProductionBillCommandResult failure)
    {
        AssertCommandObservationIdle();
        if (TryGetPendingCommandFailure(out failure)) return true;
        if (stateStore.Bills.Any(record => record.buildingInstanceId.Equals(facilityId)
                && record.CompletionObservation != null))
        {
            failure = CommandObservationFailure(default, "production-completion-observation-pending");
            return true;
        }
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

    internal void FinalizeDeliveredStockSensors()
    {
        HashSet<string> installedBefore = stockSensors.InstalledFacilityIds
            .ToHashSet(StringComparer.Ordinal);
        int version = stockSensors.Version;
        publishingCommand = true;
        try { stockSensors.FinalizeDeliveredSensors(); }
        finally { publishingCommand = false; }
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
                ObserveCommittedSensorChange(
                    facilities,
                    facilityId,
                    ProductionCommandKind.StockSensorInstalled,
                    new ProductionCommandStateData(),
                    new ProductionCommandStateData { sensorInstalled = true });
            }
            foreach (string facilityId in installedBefore
                         .Except(installedAfter, StringComparer.Ordinal)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                ObserveCommittedSensorChange(
                    facilities,
                    facilityId,
                    ProductionCommandKind.StockSensorRemoved,
                    new ProductionCommandStateData { sensorInstalled = true },
                    new ProductionCommandStateData());
            }
            Touch(default, requestWorker: false);
        }
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

    internal static ProductionCommandStateData CaptureCommandState(ProductionBillRecord record) => new()
    {
        exists = true, mode = record.mode, remainingCycles = record.remainingCycles,
        minimumReserve = record.minimumReserve, targetStock = record.targetStock,
        hasPendingMode = record.hasPendingModeTransition, pendingMode = record.pendingMode,
        suspended = record.suspended, minimumQuality = record.minimumCraftQuality,
        blockedCode = (int)record.blockedFailure.Code, distributionMode = record.distributionMode,
        routes = record.routePolicies.Select(row => row.Clone()).ToList(),
        workerPolicy = record.workerPolicy?.CloneNormalized() ?? WorkerSelectionPolicySaveData.Anyone(),
        reservedWorkerId = record.reservedWorkerId ?? string.Empty, emergencyWorkerId = record.emergencyWorkerId ?? string.Empty
    };

    private static ProductionCommandStateData ProjectCommandState(ProductionBillRecord record, Action<ProductionCommandStateData> project)
    { var value = CaptureCommandState(record); project(value); return value; }

    private static ProductionCommandStateData ProjectOrderStateAfter(ProductionBillRecord record,
        ProductionOrderMode requestedMode, int remainingCycles, int minimumReserve, int targetStock) =>
        ProjectCommandState(record, value =>
        {
            bool active = IsCycleActive(record);
            value.mode = active ? record.mode : requestedMode;
            value.remainingCycles = remainingCycles; value.minimumReserve = minimumReserve; value.targetStock = targetStock;
            value.hasPendingMode = active; value.pendingMode = active ? requestedMode : record.pendingMode;
        });

    internal ProductionCommandAppliedFact CreateCommandFact(ProductionCommandKind kind, ProductionBillRecord record,
        ProductionRecipeSO recipe, ProductionFacilityHandle facility, ProductionCommandStateData before,
        ProductionCommandStateData after, long revision) => new(new ProductionCommandFactData
    {
        ownerRevision = revision, kind = kind, billId = record?.billId.Value ?? string.Empty,
        recipeId = recipe?.RecipeId ?? string.Empty, recipeDisplayName = recipe?.DisplayName ?? string.Empty,
        facilityId = facility?.InstanceId.Value ?? string.Empty, facilityDisplayName = facility?.DisplayName ?? string.Empty,
        facilityX = facility?.Position.x ?? 0, facilityY = facility?.Position.y ?? 0,
        absoluteDay = calendar?.Day ?? 0, before = before, after = after
    });

    internal bool TryStageCommand(ProductionCommandAppliedFact fact, out IDomainFactStage stage, out string failure)
    {
        AssertCommandObservationIdle();
        stage = null;
        if (commandObservations == null || commandRecovery == null || calendar == null)
        { failure = "production-command-observer-not-connected"; return false; }
        publishingCommand = true;
        try
        {
            if (!commandObservations.TryStage(fact, out stage, out failure) || stage == null)
            { CancelCommandStage(stage); stage = null; return false; }
            if (stage.IsReplay || stage.OwnerRevision != fact.OwnerRevision
                || !DomainFactValueSyntax.IsText(stage.OperationKey, 4096, false)
                || stage.OperationKey != stage.OperationKey.Trim())
            {
                failure = "production-command-observation-owner-conflict";
                CancelCommandStage(stage); stage = null; return false;
            }
            return true;
        }
        catch (Exception error) when (IsObservationError(error))
        { CancelCommandStage(stage); stage = null; failure = "production-command-prepare:" + error.Message; return false; }
    }

    internal void QueueCommittedCommandObservation(ProductionCommandAppliedFact fact, long revision,
        IDomainFactStage prepared, bool deliverImmediately = true)
    {
        if (fact == null || fact.OwnerRevision != revision || revision != stateStore.NextCommandSequence
            || stateStore.TryGetPendingCommandObservation(revision, out _)
            || prepared != null && prepared.OwnerRevision != revision)
            throw new InvalidOperationException("production-command-observation-sequence-conflict");
        stateStore.SetPendingCommandObservation(new ProductionCommandObservationSaveData
        { fact = fact.Snapshot(), publicationKey = prepared?.OperationKey ?? string.Empty });
        stateStore.NextCommandSequence = checked(revision + 1);
        if (deliverImmediately) DeliverPendingCommandObservation(revision, prepared);
    }

    private bool TryPrepareFacilityCommandFact(ProductionFacilityHandle facility, ProductionCommandKind kind,
        ProductionCommandStateData before, ProductionCommandStateData after, out ProductionCommandAppliedFact fact,
        out long revision, out IDomainFactStage prepared, out ProductionBillCommandResult failure)
    {
        fact = null; prepared = null; revision = stateStore.NextCommandSequence; failure = null;
        try
        {
            if (facility == null || facility.IsDestroyed)
            { failure = CommandObservationFailure(default, "production-command-facility-missing"); return false; }
            fact = CreateCommandFact(kind, null, null, facility, before, after, revision);
            if (TryStageCommand(fact, out prepared, out string reason)) return true;
            failure = CommandObservationFailure(default, reason); return false;
        }
        catch (Exception error) when (IsObservationError(error))
        { CancelCommandStage(prepared); failure = CommandObservationFailure(default, error.Message); return false; }
    }

    private void ObserveCommittedSensorChange(IReadOnlyDictionary<string, ProductionFacilityHandle> facilities,
        string facilityId, ProductionCommandKind kind, ProductionCommandStateData before, ProductionCommandStateData after)
    {
        if (facilities == null || !facilities.TryGetValue(facilityId, out var facility))
            throw new InvalidOperationException("committed-stock-sensor-facility-missing:" + facilityId);
        // The physical sensor already changed. Retain the fact even when no observer can reserve now.
        long revision = stateStore.NextCommandSequence;
        var fact = CreateCommandFact(kind, null, null, facility, before, after, revision);
        QueueCommittedCommandObservation(fact, revision, null);
    }

    internal void DeliverPendingCommandObservation(long revision, IDomainFactStage prepared = null)
    {
        if (!stateStore.TryGetPendingCommandObservation(revision, out var pending))
        { CancelCommandStage(prepared); return; }
        if (prepared == null) AssertCommandObservationIdle();
        publishingCommand = true;
        IDomainFactStage token = prepared;
        try
        {
            DomainFactDelivery delivery;
            if (token != null) delivery = CommitCommandStage(token);
            else if (pending.publicationKey.Length == 0)
            {
                if (commandObservations == null || !commandObservations.TryStage(new ProductionCommandAppliedFact(pending.fact),
                        out token, out string failure) || token == null)
                { Debug.LogError("Production command fact remains pending: " + revision); return; }
                if (token.OwnerRevision != revision || !DomainFactValueSyntax.IsText(token.OperationKey, 4096, false))
                    throw new InvalidOperationException("production-command-recovery-stage-conflict");
                pending.publicationKey = token.OperationKey;
                delivery = CommitCommandStage(token);
            }
            else
            {
                var results = new DomainFactDelivery[1];
                if (commandRecovery == null || !commandRecovery.ReplayBatch(new[]
                    { DomainFactReplay.Create(pending.publicationKey, new ProductionCommandAppliedFact(pending.fact)) }, results, out _))
                { Debug.LogError("Production command fact recovery remains pending: " + revision); return; }
                delivery = results[0];
            }
            if (AcceptCommandProof(pending, delivery) && delivery.Completed)
                stateStore.RemovePendingCommandObservation(revision);
            else Debug.LogError("Production command fact remains pending: " + revision + ":" + delivery.Detail);
        }
        catch (Exception error) when (IsObservationError(error))
        { Debug.LogError("Production command fact remains pending: " + revision + ":" + error.Message); }
        finally { CancelCommandStage(token); }
    }

    internal void FlushPendingCommandObservations()
    {
        AssertCommandObservationIdle();
        foreach (long revision in stateStore.PendingCommandObservations.Select(value => value.fact.ownerRevision).ToArray())
            DeliverPendingCommandObservation(revision);
    }

    private bool TryGetPendingCommandFailure(out ProductionBillCommandResult failure)
    {
        AssertCommandObservationIdle();
        if (commandObservations == null || commandRecovery == null || calendar == null)
        { failure = CommandObservationFailure(default, "production-command-observer-not-connected"); return true; }
        FlushPendingCommandObservations();
        if (stateStore.PendingCommandObservations.Count > 0)
        { failure = CommandObservationFailure(default, "production-command-observation-pending"); return true; }
        failure = null; return false;
    }

    private ProductionBillCommandResult ExecuteObservedBillCommand(ProductionBillRecord record, ProductionRecipeSO recipe,
        ProductionCommandKind kind, ProductionCommandStateData before, ProductionCommandStateData after,
        Action mutation, Action rollback, WorkTypeId workTypeId, bool requestWorker,
        ProductionBillOutcomeCode outcome = ProductionBillOutcomeCode.BillUpdated)
    {
        if (record == null || recipe == null || mutation == null || rollback == null)
            return CommandObservationFailure(default, "production-command-transaction-input-invalid");
        long revision = stateStore.NextCommandSequence;
        IDomainFactStage prepared = null;
        ProductionCommandAppliedFact fact;
        try
        {
            fact = CreateCommandFact(kind, record, recipe, billQueries.ResolveFacility(record), before, after, revision);
            if (!TryStageCommand(fact, out prepared, out string reason)) return CommandObservationFailure(record.billId, reason);
        }
        catch (Exception error) when (IsObservationError(error))
        { CancelCommandStage(prepared); return CommandObservationFailure(record.billId, error.Message); }
        try
        {
            try { mutation(); }
            catch (Exception error) when (IsObservationError(error))
            { rollback(); return CommandObservationFailure(record.billId, "production-command-mutation:" + error.Message); }
            QueueCommittedCommandObservation(fact, revision, prepared, deliverImmediately: false);
            var delivery = CommitCommandStage(prepared);
            if (delivery.Phase == DomainFactDeliveryPhase.Rejected)
            {
                rollback(); stateStore.RemovePendingCommandObservation(revision); stateStore.NextCommandSequence = revision;
                return CommandObservationFailure(record.billId, delivery.Detail);
            }
            stateStore.TryGetPendingCommandObservation(revision, out var pending);
            bool confirmed = AcceptCommandProof(pending, delivery);
            if (confirmed && delivery.Completed) stateStore.RemovePendingCommandObservation(revision);
            Touch(workTypeId, requestWorker);
            return confirmed ? ProductionBillCommandResult.Success(record.billId, outcome)
                : CommandObservationFailure(record.billId, "production-command-observation-unconfirmed:" + delivery.Detail);
        }
        finally { CancelCommandStage(prepared); }
    }

    private static DomainFactDelivery CommitCommandStage(IDomainFactStage prepared)
    {
        try { return prepared.Commit(); }
        catch (Exception error) when (IsObservationError(error))
        { return DomainFactDelivery.Uncertain("production-command-commit-exception:" + error.Message); }
    }
    private static bool AcceptCommandProof(ProductionCommandObservationSaveData pending, DomainFactDelivery delivery)
    {
        if (pending == null || delivery.Phase is not (DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery)
            || pending.publicationKey != delivery.OperationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 4096, false) || delivery.PublicationId != delivery.PublicationId.Trim()
            || !ProductionCommandObservationSaveData.IsFingerprint(delivery.Fingerprint)
            || pending.publicationId.Length > 0 && (pending.publicationId != delivery.PublicationId || pending.publicationFingerprint != delivery.Fingerprint))
            return false;
        pending.publicationId = delivery.PublicationId; pending.publicationFingerprint = delivery.Fingerprint; return true;
    }
    internal void CancelCommandStage(IDomainFactStage stage)
    { try { stage?.Cancel(); } finally { publishingCommand = false; } }
    internal void AssertCommandObservationIdle()
    { if (publishingCommand || stateStore.Bills.Any(record => record.IsCompletionPublishing))
            throw new InvalidOperationException("production-observation-in-progress"); }
    private static bool IsObservationError(Exception error) =>
        error is not OutOfMemoryException && error is not StackOverflowException && error is not AccessViolationException;
    internal static ProductionBillCommandResult CommandObservationFailure(ProductionBillId billId, string detail) =>
        ProductionBillCommandResult.Failed(new DomainFailure(FailureCode.ProductionBillUnavailable, billId.Value,
            string.IsNullOrWhiteSpace(detail) ? "production-command-observation-failed" : detail));

    internal void Touch(WorkTypeId workTypeId, bool requestWorker)
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
