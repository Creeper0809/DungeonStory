using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ProductionFacilityDefinitionIdentity
{
    public static bool IsProductionWorkstation(BuildableObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        BuildingSO definition = candidate.BuildingData;
        if (definition == null)
        {
            return false;
        }

        BuildingProductionWorkstationAbility authored =
            definition.GetAbility<BuildingProductionWorkstationAbility>();
        if (authored == null)
        {
            return false;
        }

        string authoredTag = authored.workstationTag ?? string.Empty;
        if (string.IsNullOrWhiteSpace(authoredTag)
            || !string.Equals(
                authoredTag,
                authoredTag.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production workstation has a missing or noncanonical workstation tag.");
        }

        return true;
    }

    public static string Resolve(BuildingSO definition)
    {
        try
        {
            return BuildingDefinitionIdentity.Resolve(definition);
        }
        catch (ArgumentNullException exception)
        {
            throw new InvalidOperationException(
                "Production facility has no building definition authority.",
                exception);
        }
    }
}

/// <summary>
/// Projects a live building into the immutable production facility handle.
/// This query deliberately has no dependency on the production aggregate or
/// output-capability registry so output handlers can validate a destination
/// without recursively constructing their own registry.
/// </summary>
public sealed class ProductionFacilityHandleQueryAdapter :
    IProductionFacilityHandleQuery
{
    public ProductionFacilityHandle CaptureFacility(object runtimeObject) =>
        ProductionFacilityHandleProjection.Capture(runtimeObject);
}

internal static class ProductionFacilityHandleProjection
{
    public static ProductionFacilityHandle Capture(object runtimeObject)
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
        BuildingProductionWorkstationAbility workstation =
            facility.BuildingData?.GetProductionWorkstationAbility();
        if (workstation != null && bufferAbility == null)
        {
            throw new InvalidOperationException(
                "Production workstation is missing authored output-buffer capacity: "
                + ProductionFacilityDefinitionIdentity.Resolve(
                    facility.BuildingData));
        }
        return new ProductionFacilityHandle(
            facility,
            facility.PersistentInstanceId,
            facility.centerPos,
            facility.IsGridDestroyed,
            sensorItemId,
            bufferAbility?.allowOverflowDump == true,
            bufferAbility?.overflowOffset ?? default,
            ProductionFacilityDefinitionIdentity.Resolve(facility.BuildingData),
            workstation?.WorkstationTag ?? string.Empty,
            bufferAbility?.physicalOutputBufferCycleCapacity ?? 4,
            facility.BuildingData == null
                ? ProductionFacilityProcessFluidCapacityProfile.Empty
                : ProductionFacilityCapacitySubjectAdapter
                    .CaptureProcessFluidProfile(facility.BuildingData),
            workstation == null
                ? ProductionFacilityWorkstationLaneCapacityProfile.Empty
                : ProductionFacilityCapacitySubjectAdapter
                    .CaptureWorkstationLaneProfile(facility.BuildingData));
    }
}

/// <summary>
/// The only adapter allowed to unwrap production scene handles. It keeps the
/// named Economy aggregate independent from Assembly-CSharp actor types.
/// </summary>
public sealed class ProductionAssemblyBridgeAdapter : IProductionAssemblyBridge
{
    private readonly IProductionItemGateway items;
    private readonly IProductionOutputBufferGateway outputBuffer;
    private readonly IProductionStockSensorPhysicalGateway stockSensorPhysical;
    private readonly IProductionInputLogisticsService inputLogistics;
    private readonly IProductionCycleUtilityService cycleUtilities;
    private readonly IProductionWorkshopRuntime workshops;
    private readonly IBuildingWorldQuery buildings;
    private readonly IWarehouseWorldQuery warehouses;
    private readonly IWorkforceReplanService workforce;
    private readonly ProductionOutputHandlerRegistry outputHandlers;
    private readonly IWorkerNarrativeQualificationQuery narrativeQualification;
    private readonly Func<ICharacterPerformanceQuery> performance;

    [VContainer.Inject]
    public ProductionAssemblyBridgeAdapter(
        IProductionItemGateway items,
        IProductionOutputBufferGateway outputBuffer,
        IProductionStockSensorPhysicalGateway stockSensorPhysical,
        IProductionInputLogisticsService inputLogistics,
        IProductionCycleUtilityService cycleUtilities,
        IProductionWorkshopRuntime workshops,
        IBuildingWorldQuery buildings,
        IWarehouseWorldQuery warehouses,
        IWorkforceReplanService workforce,
        ProductionOutputHandlerRegistry outputHandlers,
        IWorkerNarrativeQualificationQuery narrativeQualification,
        Func<ICharacterPerformanceQuery> performance)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.outputBuffer = outputBuffer
            ?? throw new ArgumentNullException(nameof(outputBuffer));
        this.stockSensorPhysical = stockSensorPhysical
            ?? throw new ArgumentNullException(nameof(stockSensorPhysical));
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
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
    }

    public int BuildingVersion => buildings.BuildingVersion;

    public IReadOnlyList<ProductionOutputCapabilityContractSnapshot>
        OutputCapabilityContracts => outputHandlers.CapabilityContracts;

    public IReadOnlyList<ProductionFacilityHandle> Facilities =>
        (buildings.Buildings ?? Array.Empty<BuildableObject>())
        .Where(ProductionFacilityDefinitionIdentity.IsProductionWorkstation)
        .Select(CaptureFacility)
        .ToArray();

    public ProductionFacilityHandle CaptureFacility(object runtimeObject)
        => ProductionFacilityHandleProjection.Capture(runtimeObject);

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
        if (worker?.AuthorityKind is ProductionWorkerAuthorityKind
                .AutomaticExecutor or ProductionWorkerAuthorityKind.PassiveProcessor)
        {
            failureReason = string.Empty;
            return true;
        }
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
        ICharacterPerformanceQuery performanceQuery = performance();
        if (performanceQuery == null)
            throw new InvalidOperationException(
                "Production craft quality requires the authoritative character performance query.");
        ProficiencyWorkProfileAuthoring profile = recipe?.Proficiency;
        if (profile == null || !profile.IsValid)
            throw new InvalidOperationException(
                $"Production recipe '{recipe?.RecipeId}' has no authored proficiency profile.");
        CharacterPerformanceSnapshot result = performanceQuery.Evaluate(
            actor,
            "performance:work:craft:quality",
            new CharacterPerformanceEvaluationContext
            {
                PrimaryProficiencyOverride = profile.Primary.Value,
                SecondaryProficiencyOverride = profile.Secondary.Value
            });
        return Mathf.Max(0f, result.Value * 58f);
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
    public bool ConsumeDeliveredToWip(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        string operationId,
        out ProductionWipInputReceipt receipt,
        out string failureReason) => items.ConsumeDeliveredToWip(
            destinationId,
            costs,
            operationId,
        out receipt,
        out failureReason);
    public bool AcknowledgeWipInput(
        string commitId,
        out string failureReason) => items.AcknowledgeWipInput(
            commitId,
            out failureReason);
    public bool CommitStockSensorInstallPending(
        string destinationId,
        string itemId,
        string operationId,
        string reasonCode,
        out ProductionStockSensorPhysicalReceipt receipt,
        out string failureReason)
    {
        return stockSensorPhysical.CommitPending(
                destinationId,
                itemId,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);
    }

    public bool TryGetPendingStockSensorInstall(
        string operationId,
        out ProductionStockSensorPhysicalReceipt receipt)
    {
        return stockSensorPhysical.TryGetPending(operationId, out receipt);
    }

    public bool AcknowledgeStockSensorInstall(
        string commitId,
        out string failureReason) =>
        stockSensorPhysical.Acknowledge(commitId, out failureReason);
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
    public bool TryCommitBufferedOutput(
        string commitId,
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId,
        out DomainFailure failure) => outputBuffer.TryCommitBufferedOutput(
            commitId,
            itemId,
            amount,
            position,
            destinationId,
            out failure);
    public bool AcknowledgeBufferedOutput(
        string commitId,
        out DomainFailure failure) => outputBuffer.AcknowledgeBufferedOutput(
            commitId,
            out failure);
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
    public bool TryReleaseDestinationAtomically(
        string destinationId,
        Vector2Int releasePosition,
        out int released,
        out string failureReason) => items.TryReleaseDestinationAtomically(
            destinationId,
            releasePosition,
            out released,
            out failureReason);
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
    public long ResolveInputBufferMassCapacity(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility) =>
        inputLogistics.ResolveInputBufferMassCapacity(
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
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out ProductionProcessFluidReceipt receipt,
        out string failureReason) => cycleUtilities.TryConsumeCycleUtilities(
            record,
            recipe,
            Unwrap(facility),
            out receipt,
            out failureReason);
    public bool AcknowledgeCycleUtilities(
        ProductionProcessFluidReceipt receipt,
        out string failureReason) => cycleUtilities.AcknowledgeCycleUtilities(
            receipt,
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

    public int ResolveOutputBufferCycleCapacity(
        ProductionFacilityHandle facility)
    {
        BuildingProductionBufferAbility ability = Unwrap(facility)
            ?.BuildingData
            ?.GetProductionBufferAbility();
        if (ability == null)
        {
            throw new InvalidOperationException(
                "Production facility is missing authored output-buffer capacity: "
                + (facility?.DefinitionId ?? "<missing>"));
        }
        int capacity = ability.physicalOutputBufferCycleCapacity;
        if (capacity < 2 || capacity > 4)
        {
            throw new InvalidOperationException(
                $"Physical production output buffer cycle capacity '{capacity}' must be authored in [2,4].");
        }
        return capacity;
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

    public ProductionOutputCapabilityDescriptor CaptureOutputCapability(
        string outputLineId,
        string itemId) => outputHandlers.CaptureDescriptor(
        outputLineId,
        itemId);

    public bool TryValidateOutputCapability(
        ProductionOutputCapabilityDescriptor capability,
        out DomainFailure failure) => outputHandlers.TryValidateExact(
        capability,
        out _,
        out failure);

    public bool TryHandleOutput(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        ProductionOutputCapabilityDescriptor capability,
        int amount,
        string outputDestinationId,
        float qualityModifier,
        float workerQuality,
        string commitId,
        out bool handled,
        out DomainFailure failure)
    {
        handled = false;
        failure = DomainFailure.None;
        if (!outputHandlers.TryResolveExact(
                capability,
                out IProductionOutputHandler handler,
                out failure))
            return false;

        handled = true;
        ProductionOutputContext context = new(
            recipe,
            Unwrap(facility),
            Unwrap(worker),
            capability.OutputLineId,
            capability.ItemId,
            amount,
            outputDestinationId,
            qualityModifier,
            workerQuality,
            commitId);
        if (handler is not IIdempotentProductionOutputHandler idempotent)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                capability.ItemId,
                "handler-not-idempotent");
            return false;
        }
        if (idempotent.TryProduceIdempotent(context, out failure))
        {
            return true;
        }
        if (!failure.IsFailure)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                capability.ItemId);
        }
        return false;
    }

    public bool AcknowledgeHandledOutput(
        ProductionOutputCapabilityDescriptor capability,
        string commitId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!outputHandlers.TryResolveExact(
                capability,
                out IProductionOutputHandler handler,
                out failure))
            return false;
        if (handler is not IIdempotentProductionOutputHandler idempotent)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                capability.ItemId,
                "handler-not-idempotent");
            return false;
        }
        return idempotent.TryAcknowledge(commitId, out failure);
    }

    public bool TryCaptureCommittedOutput(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        ProductionOutputCapabilityDescriptor capability,
        int amount,
        string outputDestinationId,
        float qualityModifier,
        float workerQuality,
        string commitId,
        out ProductionCommittedOutputSnapshot snapshot,
        out DomainFailure failure)
    {
        snapshot = null;
        if (!outputHandlers.TryResolveExact(
                capability,
                out IProductionOutputHandler handler,
                out failure))
        {
            return false;
        }
        if (handler is IIdempotentProductionOutputHandler idempotent)
        {
            ProductionOutputContext context = new(
                recipe,
                Unwrap(facility),
                Unwrap(worker),
                capability.OutputLineId,
                capability.ItemId,
                amount,
                outputDestinationId,
                qualityModifier,
                workerQuality,
                commitId);
            return idempotent.TryCaptureCommittedOutput(
                context,
                out snapshot,
                out failure);
        }
        failure = new DomainFailure(
            FailureCode.ProductionOutputUnavailable,
            capability.ItemId,
            "handler-not-idempotent");
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

    public bool HasCompatibleWarehouse(string itemId) =>
        items.TryGetStockCategory(itemId, out StockCategory category)
        && (warehouses.Warehouses ?? Array.Empty<IWarehouseFacility>())
            .Any(warehouse => warehouse != null
                && warehouse.HasWarehouseInventory
                && warehouse.Inventory != null
                && warehouse.Inventory.Accepts(category)
                && warehouse.Inventory.CanStoreItem(itemId, 1));

    public void RequestWorkReplan(WorkTypeId workTypeId) =>
        workforce.RequestOneWorkerToReplanFor(workTypeId);
    public void RequestOneHaulerToReplan(bool forceInterrupt) =>
        workforce.RequestOneHaulerToReplan(forceInterrupt: forceInterrupt);

    private static BuildableObject Unwrap(ProductionFacilityHandle handle) =>
        handle?.RuntimeObject as BuildableObject;
    private static CharacterActor Unwrap(ProductionWorkerHandle handle) =>
        handle?.RuntimeObject as CharacterActor;
}
