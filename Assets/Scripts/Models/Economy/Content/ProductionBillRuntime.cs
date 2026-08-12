using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionBillRuntime :
    IProductionBillCoreQuery,
    IProductionBillCoreOrderCommand,
    IProductionBillCoreWorkExecution,
    IProductionBillPersistence,
    ITickable
{
    public const string DestinationPrefix = "production:";
    public const string OutputDestinationPrefix = "production-output:";
    public const string StockSensorDestinationPrefix = "production-sensor:";
    public const string StockSensorItemId = "component:stock-sensor-panel";
    private const float SecondsPerGameHour = 7.5f;
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
    private readonly IProductionAssemblyBridge cycleUtilities;
    private readonly IProductionAssemblyBridge inputLogistics;
    private readonly IProductionStockSensorRuntime stockSensors;
    private readonly ProductionAggregateStateStore stateStore;
    private readonly IProductionBillSnapshotProjector snapshotProjector;
    private readonly IProductionAssemblyBridge buildingWorld;
    private readonly IGameClock clock;
    private readonly IRecipeBalanceWorkCalculator balanceWorkCalculator;
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
        cycleUtilities = execution.Bridge;
        snapshotProjector = execution.SnapshotProjector;
        buildingWorld = execution.Bridge;
        clock = execution.Clock;
        balanceWorkCalculator = order.BalanceWorkCalculator;
    }

    public int Version => stateStore.BillVersion;

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
        nextBillSequence = sequence + 1;
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
        stateStore.AddBill(record);
        RequestMissingInputs(record, recipe, facility);
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

        if (returnMaterials && !record.materialsConsumed)
        {
            items.ReleaseDestination(
                record.materialDestinationId,
                ResolveFacility(record)?.Position ?? Vector2Int.zero);
        }
        else
        {
            items.RemoveDestination(record.materialDestinationId);
        }

        stateStore.RemoveBill(record);
        Touch(default, requestWorker: false);
        return ProductionBillCommandResult.Success(
            billId,
            ProductionBillOutcomeCode.BillRemoved);
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
        stateStore.MoveBill(
            record,
            anchor,
            insertAfter: currentLocalIndex < clampedTarget);
        Touch(default, requestWorker: false);
        return ProductionBillCommandResult.Success(billId);
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

        record.SetSuspended(suspended);
        Touch(ResolveRecipe(record)?.WorkTypeId ?? default, requestWorker: !suspended);
        return ProductionBillCommandResult.Success(billId);
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

        ProductionFacilityHandle facility = ResolveFacility(record);
        if (!HasStockSensor(facility))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionStockSensorRequired,
                    record.buildingInstanceId.Value));
        }

        record.SetStockPolicy(minimumReserve, targetStock);
        QueueOrApplyModeTransition(record, ProductionOrderMode.MaintainStock);
        Touch(ResolveRecipe(record)?.WorkTypeId ?? default, requestWorker: true);
        return ProductionBillCommandResult.Success(billId);
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

        if (mode == ProductionOrderMode.MaintainStock
            && !HasStockSensor(ResolveFacility(record)))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionStockSensorRequired,
                    record.buildingInstanceId.Value));
        }

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
        Touch(ResolveRecipe(record)?.WorkTypeId ?? default, requestWorker: true);
        return ProductionBillCommandResult.Success(billId);
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

        record.ReplaceDistributionPolicy(mode, (routes
                ?? Array.Empty<ProductionConsumerRoutePolicy>())
            .Where(route => route != null
                && !string.IsNullOrWhiteSpace(route.consumerId))
            .GroupBy(route => route.consumerId.Trim(), StringComparer.Ordinal)
            .Select(group => group.First().Clone()));
        Touch(default, requestWorker: false);
        return ProductionBillCommandResult.Success(billId);
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
        record.SetWorkerPolicy(policy);
        record.SetReservedWorker(string.Empty);
        Touch(ResolveRecipe(record)?.WorkTypeId ?? default, requestWorker: true);
        return ProductionBillCommandResult.Success(billId);
    }

    public ProductionBillCommandResult SetEmergencyWorker(
        ProductionBillId billId,
        string characterId)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
            return ProductionBillCommandResult.Failed(
                new DomainFailure(FailureCode.ProductionBillMissing, billId.Value));

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

        record.SetEmergencyWorker(normalized);
        if (normalized.Length > 0)
        {
            record.SetWorkerPolicy(new WorkerSelectionPolicySaveData
            {
                mode = WorkerSelectionMode.SpecificCharacters,
                sortMode = WorkerCandidateSortMode.SpecificThenBestExpectedQuality,
                specificCharacterIds = new List<string> { normalized }
            });
        }
        record.SetReservedWorker(string.Empty);
        Touch(ResolveRecipe(record)?.WorkTypeId ?? default, requestWorker: true);
        return ProductionBillCommandResult.Success(billId);
    }

    public bool HasStockSensor(ProductionFacilityHandle facility)
    {
        return stockSensors.Has(facility);
    }

    public ProductionBillCommandResult RequestStockSensorInstallation(
        ProductionFacilityHandle facility)
    {
        int version = stockSensors.Version;
        ProductionBillCommandResult result =
            stockSensors.RequestInstallation(facility);
        if (stockSensors.Version != version)
        {
            Touch(default, requestWorker: false);
        }

        return result;
    }

    public ProductionBillCommandResult RemoveStockSensor(
        ProductionFacilityHandle facility)
    {
        int version = stockSensors.Version;
        ProductionBillCommandResult result = stockSensors.Remove(facility);
        if (stockSensors.Version != version)
        {
            Touch(default, requestWorker: false);
        }

        return result;
    }

    public ProductionBillCommandResult AcknowledgeStockSensorUnlock(
        ProductionFacilityHandle facility)
    {
        int version = stockSensors.Version;
        ProductionBillCommandResult result = stockSensors.Acknowledge(facility);
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
            out DomainFailure failure);
        if (record == null)
        {
            return new ProductionWorkBeginResult(null, failure);
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
        if (!TryValidateCycleStart(
                record,
                recipe,
                facility,
                out string cycleFailure))
        {
            failure = new DomainFailure(
                FailureCode.ProductionUtilitiesUnavailable);
            record.SetBlockedFailure(failure);
            Touch(default, requestWorker: false);
            return new ProductionWorkBeginResult(null, failure);
        }

        if (!record.materialsConsumed
            && !items.ConsumeDelivered(
                record.materialDestinationId,
                ToCycleInputMap(record, recipe, facility),
                out _))
        {
            RequestMissingInputs(record, recipe, facility);
            return new ProductionWorkBeginResult(
                null,
                new DomainFailure(FailureCode.ProductionMaterialsMissing));
        }

        record.SetMaterialsConsumed(true);
        if (!record.processFluidConsumed
            && !TryConsumeCycleUtilities(
                record,
                recipe,
                facility,
                out string utilityFailure))
        {
            failure = new DomainFailure(
                FailureCode.ProductionUtilitiesUnavailable);
            record.SetBlockedFailure(failure);
            return new ProductionWorkBeginResult(null, failure);
        }

        record.SetProcessFluidConsumed(true);
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

        DomainFailure outputFailure = outputExecution.ProduceAll(
            recipe,
            facility,
            worker,
            record.batchIntegrity,
            record.outputDestinationId);
        if (outputFailure.IsFailure)
        {
            record.SetReservedWorker(string.Empty);
            return new ProductionWorkExecutionResult(
                false,
                false,
                ProductionBillOutcomeCode.None,
                outputFailure);
        }

        record.ClearOutputReservations();
        record.ClearSelectedSupplies();
        record.SetCompletedWork(0f);
        record.SetMaterialsConsumed(false);
        record.SetProcessFluidConsumed(false);
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
            stateStore.RemoveBill(record);
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
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        FinalizeDeliveredStockSensors();
        float elapsedHours = clock.DeltaTime / SecondsPerGameHour;
        foreach (ProductionBillRecord record in bills.ToArray())
        {
            ProductionRecipeSO recipe = ResolveRecipe(record);
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
                    null,
                    facility,
                    record.billId,
                    0f);
            }
        }
    }

    private void FinalizeDeliveredStockSensors()
    {
        int version = stockSensors.Version;
        stockSensors.FinalizeDeliveredSensors();
        if (stockSensors.Version != version)
        {
            Touch(default, requestWorker: false);
        }
    }


    private bool TryValidateCycleStart(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason)
    {
        return EnsureOutputReservation(
                record,
                recipe,
                facility,
                out failureReason)
            && cycleUtilities.ValidateCycleRequirements(
                record,
                recipe,
                facility,
                bills,
                out failureReason);
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
        out string failureReason)
    {
        return cycleUtilities.TryConsumeCycleUtilities(
            recipe,
            facility,
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

        items.SpawnOutput(
            recipe.SpoilageItemId,
            Mathf.Max(1, recipe.Inputs.Sum(input => input?.Amount ?? 0)),
            facility.Position);
        record.SetCompletedWork(0f);
        record.SetMaterialsConsumed(false);
        record.SetProcessFluidConsumed(false);
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
            stateStore.RemoveBill(record);
        }
        else
        {
            RequestMissingInputs(record, recipe, facility);
        }

        Touch(recipe.WorkTypeId, requestWorker: false);
        return true;
    }

    private float ResolveCurrentRequiredWork(
        ProductionBillRecord record,
        ProductionRecipeSO recipe)
    {
        float balancedWork = balanceWorkCalculator?.CalculateRecipe(recipe)
            ?? recipe.RequiredWork;
        if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch)
        {
            return balancedWork;
        }

        return record.batchStage == ProductionBatchStage.Finishing
            ? (recipe.FinishingWork > 0f ? balancedWork * 0.20f : 0f)
            : (recipe.FinishingWork > 0f ? balancedWork * 0.80f : balancedWork);
    }

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
            bills,
            stockSensors.InstalledFacilityIds,
            stockSensors.AcknowledgedFacilityIds);
    }

    public ProductionBillRestoreCandidate BuildRestore(
        DungeonProductionBillSaveData snapshot)
    {
        return ProductionBillStateCodec.CreateRestoreCandidate(
            snapshot,
            catalog,
            stateStore.BillVersion + 1,
            stateStore.StockSensorVersion + 1);
    }

    public void Restore(ProductionBillRestoreCandidate candidate)
    {
        stateStore.Replace(
            candidate ?? throw new ArgumentNullException(nameof(candidate)));
    }

    private ProductionBillRecord FindRunnableBill(
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId,
        bool requireDeliveredInputs,
        out DomainFailure failure)
    {
        return inputLogistics.FindRunnableBill(
            bills,
            facility,
            workTypeId,
            requireDeliveredInputs,
            out failure);
    }

    private void RequestMissingInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility)
    {
        inputLogistics.RequestMissingInputs(record, recipe, facility);
    }

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
