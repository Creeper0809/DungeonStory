using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The only adapter allowed to unwrap production scene handles. It keeps the
/// named Economy aggregate independent from Assembly-CSharp actor types.
/// </summary>
public sealed class ProductionAssemblyBridgeAdapter : IProductionAssemblyBridge
{
    private readonly IProductionItemGateway items;
    private readonly IProductionOutputBufferGateway outputBuffer;
    private readonly IProductionInputLogisticsService inputLogistics;
    private readonly IProductionCycleUtilityService cycleUtilities;
    private readonly IProductionWorkshopRuntime workshops;
    private readonly IBuildingWorldQuery buildings;
    private readonly IWarehouseWorldQuery warehouses;
    private readonly IWorkforceReplanService workforce;
    private readonly IReadOnlyList<IProductionOutputHandler> outputHandlers;
    private readonly IWorkerNarrativeQualificationQuery narrativeQualification;

    public ProductionAssemblyBridgeAdapter(
        IProductionItemGateway items,
        IProductionOutputBufferGateway outputBuffer,
        IProductionInputLogisticsService inputLogistics,
        IProductionCycleUtilityService cycleUtilities,
        IProductionWorkshopRuntime workshops,
        IBuildingWorldQuery buildings,
        IWarehouseWorldQuery warehouses,
        IWorkforceReplanService workforce,
        IReadOnlyList<IProductionOutputHandler> outputHandlers,
        IWorkerNarrativeQualificationQuery narrativeQualification = null)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.outputBuffer = outputBuffer
            ?? throw new ArgumentNullException(nameof(outputBuffer));
        this.inputLogistics = inputLogistics
            ?? throw new ArgumentNullException(nameof(inputLogistics));
        this.cycleUtilities = cycleUtilities
            ?? throw new ArgumentNullException(nameof(cycleUtilities));
        this.workshops = workshops
            ?? throw new ArgumentNullException(nameof(workshops));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.warehouses = warehouses
            ?? throw new ArgumentNullException(nameof(warehouses));
        this.workforce = workforce
            ?? throw new ArgumentNullException(nameof(workforce));
        this.outputHandlers = outputHandlers
            ?? throw new ArgumentNullException(nameof(outputHandlers));
        this.narrativeQualification = narrativeQualification;
    }

    public IReadOnlyList<ProductionFacilityHandle> Facilities =>
        (buildings.Buildings ?? Array.Empty<BuildableObject>())
        .Where(candidate => candidate != null)
        .Select(CaptureFacility)
        .ToArray();

    public ProductionFacilityHandle CaptureFacility(object runtimeObject)
    {
        if (runtimeObject == null)
        {
            return null;
        }
        BuildableObject facility = runtimeObject as BuildableObject
            ?? throw new ArgumentException(
                "Production facility handle must wrap BuildableObject.",
                nameof(runtimeObject));
        string sensorItemId = facility.BuildingData
            ?.GetProductionWorkstationAbility()
            ?.StockSensorInstallationItemId
            ?? string.Empty;
        BuildingProductionBufferAbility bufferAbility = facility.BuildingData
            ?.GetProductionBufferAbility();
        return new ProductionFacilityHandle(
            facility,
            facility.PersistentInstanceId,
            facility.centerPos,
            facility.IsGridDestroyed,
            sensorItemId,
            bufferAbility?.allowOverflowDump == true,
            bufferAbility?.overflowOffset ?? default);
    }

    public ProductionWorkerHandle CaptureWorker(object runtimeObject)
    {
        if (runtimeObject == null)
        {
            return null;
        }
        CharacterActor worker = runtimeObject as CharacterActor
            ?? throw new ArgumentException(
                "Production worker handle must wrap CharacterActor.",
                nameof(runtimeObject));
        return new ProductionWorkerHandle(
            worker,
            worker.Identity?.PersistentId ?? string.Empty);
    }

    public bool IsWorkerEligible(
        ProductionWorkerHandle worker,
        WorkerSelectionPolicySaveData policy,
        out string failureReason)
    {
        return WorkerSelectionPolicyRules.IsEligible(
            policy,
            worker?.RuntimeObject as CharacterActor,
            narrativeQualification,
            out failureReason);
    }

    public float GetRelevantCraftSkill(
        ProductionWorkerHandle worker,
        ProductionRecipeSO recipe)
    {
        CharacterActor actor = worker?.RuntimeObject as CharacterActor;
        if (actor == null)
        {
            return 0f;
        }
        ProductionProcessClass process =
            V23BalanceWorkCalculator.ResolveProductionProcessClass(recipe);
        CharacterStatType primary = process switch
        {
            ProductionProcessClass.Medical => CharacterStatType.Medical,
            ProductionProcessClass.Precision or ProductionProcessClass.Rune =>
                CharacterStatType.Research,
            ProductionProcessClass.ForgingHeavyAssembly
                or ProductionProcessClass.HeavyIndustrial => CharacterStatType.Strength,
            _ => CharacterStatType.Dexterity
        };
        return actor.GetCharacterStat(primary);
    }

    public int CountDelivered(string itemId, string destinationId) =>
        items.CountDelivered(itemId, destinationId);
    public int CountPending(string itemId, string destinationId) =>
        items.CountPending(itemId, destinationId);
    public int CountAvailableStock(string itemId, string excludedDestinationId) =>
        items.CountAvailableStock(itemId, excludedDestinationId);
    public int CountBufferedOutput(string itemId) =>
        outputBuffer.CountBufferedOutput(itemId);
    public int CountBufferedOutput(string itemId, string destinationId) =>
        outputBuffer.CountBufferedOutput(itemId, destinationId);
    public bool RequestDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason) => items.RequestDelivery(
            itemId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out failureReason);
    public bool ConsumeDelivered(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason) => items.ConsumeDelivered(
            destinationId,
            costs,
            out failureReason);
    public bool SpawnOutput(string itemId, int amount, Vector2Int position) =>
        items.SpawnOutput(itemId, amount, position);
    public bool SpawnBufferedOutput(
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId) => outputBuffer.SpawnBufferedOutput(
            itemId,
            amount,
            position,
            destinationId);
    public bool TryRouteBufferedOutput(
        string sourceDestinationId,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int routed,
        out DomainFailure failure) => outputBuffer.TryRouteBufferedOutput(
            sourceDestinationId,
            itemId,
            amount,
            destinationPosition,
            destinationId,
            out routed,
            out failure);
    public void PrioritizeDestination(string destinationId) =>
        items.PrioritizeDestination(destinationId);
    public int ReleaseDestination(
        string destinationId,
        Vector2Int releasePosition) => items.ReleaseDestination(
            destinationId,
            releasePosition);
    public int RemoveDestination(string destinationId) =>
        items.RemoveDestination(destinationId);
    public string GetOldestAvailableStackId(
        string itemId,
        string excludedDestinationId) =>
        (items as IProductionSupplyInventoryGateway)
        ?.GetOldestAvailableStackId(itemId, excludedDestinationId)
        ?? string.Empty;

    public ProductionBillRecord FindRunnableBill(
        IReadOnlyList<ProductionBillRecord> bills,
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId,
        bool requireDeliveredInputs,
        out DomainFailure failure) => inputLogistics.FindRunnableBill(
            bills,
            Unwrap(facility),
            workTypeId,
            requireDeliveredInputs,
            out failure);
    public bool HasDeliveredInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out DomainFailure failure) => inputLogistics.HasDeliveredInputs(
            record,
            recipe,
            Unwrap(facility),
            out failure);
    public void RequestMissingInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility) => inputLogistics.RequestMissingInputs(
            record,
            recipe,
            Unwrap(facility));
    public void RecalculatePrefetch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionWorkerHandle worker) => inputLogistics.RecalculatePrefetch(
            record,
            recipe,
            Unwrap(worker));
    public bool ShouldRunAnotherCycle(
        ProductionBillRecord record,
        ProductionRecipeSO recipe) =>
        inputLogistics.ShouldRunAnotherCycle(record, recipe);
    public bool IsResearchUnlocked(
        ProductionRecipeSO recipe,
        out DomainFailure failure) =>
        inputLogistics.IsResearchUnlocked(recipe, out failure);
    public Dictionary<string, int> ToCycleInputMap(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility) => inputLogistics.ToCycleInputMap(
            record,
            recipe,
            Unwrap(facility));

    public bool ValidateCycleRequirements(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out string failureReason) => cycleUtilities.ValidateCycleRequirements(
            record,
            recipe,
            Unwrap(facility),
            allBills,
            out failureReason);
    public bool ValidateProcessingUtilities(
        string occupiedSupportNodeId,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason) => cycleUtilities.ValidateProcessingUtilities(
            occupiedSupportNodeId,
            recipe,
            Unwrap(facility),
            out failureReason);
    public bool TryConsumeCycleUtilities(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out string failureReason) => cycleUtilities.TryConsumeCycleUtilities(
            recipe,
            Unwrap(facility),
            out failureReason);
    public bool TryResolveBatchSupport(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out string supportNodeId,
        out string failureReason) => cycleUtilities.TryResolveBatchSupport(
            record,
            recipe,
            Unwrap(facility),
            allBills,
            out supportNodeId,
            out failureReason);
    public float ResolveTemperatureSpeed(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out bool dangerous) => cycleUtilities.ResolveTemperatureSpeed(
            recipe,
            Unwrap(facility),
            out dangerous);
    public ProductionFacilityHandle ResolveOccupiedBatchSupport(
        string occupiedSupportNodeId,
        ProductionFacilityHandle facility)
    {
        BuildableObject support = cycleUtilities.ResolveOccupiedBatchSupport(
            occupiedSupportNodeId,
            Unwrap(facility));
        return CaptureFacility(support);
    }

    public int ResolveOutputCapacity(
        ProductionFacilityHandle facility,
        string itemId,
        int outputPerBatch,
        int stackLimit)
    {
        return Unwrap(facility)?.BuildingData
            .GetProductionBufferAbility()
            ?.ResolveOutputCapacity(itemId, outputPerBatch, stackLimit)
            ?? Mathf.Max(stackLimit, Mathf.Max(1, outputPerBatch) * 4);
    }

    public float ResolveSupportModifier(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe,
        ProductionSupportModifierKind kind,
        float defaultValue,
        bool multiply)
    {
        BuildableObject workstation = Unwrap(facility);
        if (workstation == null || recipe == null)
        {
            return defaultValue;
        }

        float result = defaultValue;
        HashSet<string> appliedSupports = new(StringComparer.Ordinal);
        foreach (string tag in recipe.RequiredSupportTags
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal))
        {
            if (!workshops.TryResolveSupport(
                    workstation,
                    tag,
                    null,
                    out BuildableObject support,
                    out BuildingProductionSupportAbility ability))
            {
                continue;
            }
            string nodeId = IndustrialInfrastructureIdentity.GetNodeId(support);
            if (!appliedSupports.Add(nodeId))
            {
                continue;
            }
            float value = kind switch
            {
                ProductionSupportModifierKind.WorkSpeed =>
                    ability.workSpeedMultiplier,
                ProductionSupportModifierKind.Output => ability.outputMultiplier,
                ProductionSupportModifierKind.Quality => ability.qualityModifier,
                _ => defaultValue
            };
            result = multiply
                ? result * Mathf.Max(0.01f, value)
                : result + value;
        }
        return result;
    }

    public bool TryHandleOutput(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        string itemId,
        int amount,
        float qualityModifier,
        out bool handled,
        out DomainFailure failure)
    {
        handled = false;
        failure = DomainFailure.None;
        IProductionOutputHandler handler = outputHandlers.FirstOrDefault(
            candidate => candidate != null && candidate.CanHandle(itemId));
        if (handler == null)
        {
            return true;
        }

        handled = true;
        ProductionOutputContext context = new(
            recipe,
            Unwrap(facility),
            Unwrap(worker),
            itemId,
            amount,
            qualityModifier);
        if (handler is IDomainFailureProductionOutputHandler domainHandler)
        {
            if (domainHandler.TryProduce(context, out failure))
            {
                return true;
            }
            if (!failure.IsFailure)
            {
                failure = new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    itemId);
            }
            return false;
        }

        if (handler.TryProduce(context, out _))
        {
            return true;
        }
        failure = new DomainFailure(
            FailureCode.ProductionOutputUnavailable,
            itemId);
        return false;
    }

    public bool MatchesWorkstation(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe) =>
        Unwrap(facility)?.MatchesProductionWorkstation(recipe) == true;

    public bool HasRequiredSupports(
        ProductionFacilityHandle facility,
        IReadOnlyList<string> requiredFeatureTags,
        out string failureReason) => workshops.HasRequiredSupports(
            Unwrap(facility),
            requiredFeatureTags,
            out failureReason);

    public bool HasCompatibleWarehouse(StockCategory category) =>
        (warehouses.Warehouses ?? Array.Empty<IWarehouseFacility>())
        .Any(warehouse => warehouse != null
            && warehouse.HasWarehouseInventory
            && warehouse.Inventory != null
            && warehouse.Inventory.CanStore(category, 1));

    public void RequestWorkReplan(WorkTypeId workTypeId) =>
        workforce.RequestOneWorkerToReplanFor(workTypeId);
    public void RequestOneHaulerToReplan(bool forceInterrupt) =>
        workforce.RequestOneHaulerToReplan(forceInterrupt: forceInterrupt);

    private static BuildableObject Unwrap(ProductionFacilityHandle handle) =>
        handle?.RuntimeObject as BuildableObject;
    private static CharacterActor Unwrap(ProductionWorkerHandle handle) =>
        handle?.RuntimeObject as CharacterActor;
}
