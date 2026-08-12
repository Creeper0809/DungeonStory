using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public interface IProductionBillSnapshotProjector
{
    ProductionBillSnapshot Project(
        ProductionBillRecord record,
        ProductionFacilityHandle facility,
        IReadOnlyList<ProductionBillRecord> allBills);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionBillSnapshotProjector :
    IProductionBillSnapshotProjector
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionAssemblyBridge items;
    private readonly IProductionOutputPlanningService outputPlanning;
    private readonly IProductionAssemblyBridge inputLogistics;
    private readonly IProductionStockSensorRuntime stockSensors;
    private readonly IProductionDistributionQuery distribution;
    private readonly IRecipeBalanceWorkCalculator balanceWorkCalculator;

    public ProductionBillSnapshotProjector(
        IResourceEconomyContentCatalog catalog,
        IProductionAssemblyBridge bridge,
        IProductionOutputPlanningService outputPlanning,
        IProductionStockSensorRuntime stockSensors,
        IProductionDistributionQuery distribution,
        IRecipeBalanceWorkCalculator balanceWorkCalculator = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        items = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.outputPlanning = outputPlanning
            ?? throw new ArgumentNullException(nameof(outputPlanning));
        inputLogistics = bridge;
        this.stockSensors = stockSensors
            ?? throw new ArgumentNullException(nameof(stockSensors));
        this.distribution = distribution
            ?? throw new ArgumentNullException(nameof(distribution));
        this.balanceWorkCalculator = balanceWorkCalculator;
    }

    public ProductionBillSnapshot Project(
        ProductionBillRecord record,
        ProductionFacilityHandle facility,
        IReadOnlyList<ProductionBillRecord> allBills)
    {
        ProductionRecipeSO recipe = ResolveRecipe(record);
        ProductionBillStatus status;
        DomainFailure blockedFailure = record.blockedFailure;
        if (record.suspended)
        {
            status = ProductionBillStatus.Suspended;
        }
        else if (record.mode == ProductionOrderMode.MaintainStock
            && !stockSensors.Has(facility))
        {
            status = ProductionBillStatus.WaitingForStockSensor;
            blockedFailure = new DomainFailure(
                FailureCode.ProductionStockSensorRequired);
        }
        else if (record.routePolicies.Count > 0
            && record.routePolicies.All(route => !route.enabled))
        {
            status = ProductionBillStatus.WaitingForDistributionRoute;
            blockedFailure = new DomainFailure(
                FailureCode.ProductionDistributionRouteUnavailable);
        }
        else if (record.blockedFailure.Code
            == FailureCode.WorkOrderWorkerIneligible)
        {
            status = ProductionBillStatus.WaitingForEligibleWorker;
        }
        else if (recipe != null
            && !HasOutputCapacity(
                record,
                recipe,
                facility,
                allBills,
                out _))
        {
            status = ProductionBillStatus.WaitingForOutputSpace;
            blockedFailure = new DomainFailure(
                FailureCode.ProductionOutputSpaceUnavailable);
        }
        else if (record.batchStage == ProductionBatchStage.Processing)
        {
            status = record.utilityOutageHours > 0f
                || record.temperatureOutageHours > 0f
                    ? ProductionBillStatus.WaitingForUtilities
                    : ProductionBillStatus.Processing;
        }
        else if (record.batchStage == ProductionBatchStage.Finishing)
        {
            status = ProductionBillStatus.WaitingForFinishing;
        }
        else if (record.materialsConsumed)
        {
            status = record.completedWork > 0f
                ? ProductionBillStatus.InProgress
                : ProductionBillStatus.Ready;
        }
        else if (recipe != null
            && inputLogistics.HasDeliveredInputs(
                record,
                recipe,
                facility,
                out blockedFailure))
        {
            status = ProductionBillStatus.Ready;
        }
        else
        {
            status = ProductionBillStatus.WaitingForMaterials;
        }

        float requiredWork = recipe == null
            ? 0f
            : ResolveCurrentRequiredWork(record, recipe);
        string primaryOutput = recipe?.Outputs
            .FirstOrDefault(output => output != null)?.ItemId
            ?? string.Empty;
        int bufferedOutput = !string.IsNullOrWhiteSpace(primaryOutput)
                ? items.CountBufferedOutput(
                    primaryOutput,
                    record.outputDestinationId)
                : 0;
        int reservedOutput = record.outputReservations.Values.Sum();
        int outputCapacity = string.IsNullOrWhiteSpace(primaryOutput)
            ? 0
            : outputPlanning.ResolveCapacity(
                facility,
                primaryOutput,
                recipe.Outputs.First(output => output != null).Amount);
        return new ProductionBillSnapshot
        {
            BillId = record.billId,
            RecipeId = record.recipeId,
            RecipeName = recipe?.DisplayName ?? record.recipeId,
            BuildingInstanceId = record.buildingInstanceId,
            Position = facility?.Position ?? default,
            WorkTypeId = recipe?.WorkTypeId ?? default,
            Mode = record.mode,
            Status = status,
            RemainingCycles = record.remainingCycles,
            TargetStock = record.targetStock,
            MinimumReserve = record.minimumReserve,
            RequiredWork = requiredWork,
            CompletedWork = record.completedWork,
            MaterialsConsumed = record.materialsConsumed,
            ProcessFluidConsumed = record.processFluidConsumed,
            BatchStage = record.batchStage,
            RemainingProcessingHours = record.remainingProcessingHours,
            BatchIntegrity = record.batchIntegrity,
            UtilityOutageHours = record.utilityOutageHours,
            TemperatureOutageHours = record.temperatureOutageHours,
            OccupiedSupportNodeId = record.occupiedSupportNodeId,
            ReservedWorkerId = record.reservedWorkerId,
            WorkerPolicy = record.workerPolicy?.CloneNormalized()
                ?? WorkerSelectionPolicySaveData.Anyone(
                    WorkerCandidateSortMode.Fastest),
            EmergencyWorkerId = record.emergencyWorkerId,
            MaterialDestinationId = record.materialDestinationId,
            BlockedFailure = blockedFailure,
            PrefetchBatchCount = record.prefetchBatchCount,
            EstimatedDeliverySeconds = record.estimatedDeliverySeconds,
            EstimatedProductionCycleSeconds =
                record.estimatedProductionCycleSeconds,
            Logistics = record.logisticsStatus,
            Inputs = recipe?.Inputs ?? Array.Empty<ItemAmountDefinition>(),
            Outputs = recipe?.Outputs ?? Array.Empty<ProductionOutputDefinition>(),
            ProcessingProgressRatio = recipe == null
                || recipe.ProcessingGameHours <= 0f
                    ? 0f
                    : Mathf.Clamp01(
                        1f - record.remainingProcessingHours
                            / recipe.ProcessingGameHours),
            HasPendingModeTransition = record.hasPendingModeTransition,
            PendingMode = record.pendingMode,
            OutputDestinationId = record.outputDestinationId,
            OutputBufferedQuantity = bufferedOutput,
            ReservedOutputQuantity = reservedOutput,
            OutputCapacity = outputCapacity,
            HasStockSensor = stockSensors.Has(facility),
            HasUnacknowledgedStockSensorUnlock = stockSensors.Has(facility)
                && !stockSensors.IsAcknowledged(facility),
            DistributionMode = record.distributionMode,
            RoutePolicies = record.routePolicies
                .Select(route => route.Clone())
                .ToArray(),
            RouteStates = distribution.GetRouteStates(record.billId)
        };
    }

    private bool HasOutputCapacity(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out string failureReason)
    {
        string destinationId = string.IsNullOrWhiteSpace(
            record.outputDestinationId)
                ? outputPlanning.ResolveDestinationId(facility)
                : record.outputDestinationId;
        Dictionary<string, int> otherReservations =
            (allBills ?? Array.Empty<ProductionBillRecord>())
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
        return outputPlanning.HasCapacity(
            recipe,
            facility,
            destinationId,
            otherReservations,
            record.outputReservations.Count > 0,
            out failureReason);
    }

    private ProductionRecipeSO ResolveRecipe(ProductionBillRecord record)
    {
        return record != null
            && catalog.TryGetRecipe(
                record.recipeId,
                out ProductionRecipeSO recipe)
                    ? recipe
                    : null;
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
}
