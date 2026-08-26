using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IProductionCycleUtilityService
{
    bool ValidateCycleRequirements(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out string failureReason);
    bool ValidateProcessingUtilities(
        string occupiedSupportNodeId,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string failureReason);
    bool TryConsumeCycleUtilities(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out ProductionProcessFluidReceipt receipt,
        out string failureReason);
    bool AcknowledgeCycleUtilities(
        ProductionProcessFluidReceipt receipt,
        out string failureReason);
    bool TryResolveBatchSupport(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out string supportNodeId,
        out string failureReason);
    float ResolveTemperatureSpeed(
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out bool dangerous);
    BuildableObject ResolveOccupiedBatchSupport(
        string occupiedSupportNodeId,
        BuildableObject facility);
}

public sealed class ProductionCycleUtilityService :
    IProductionCycleUtilityService
{
    private readonly IProcessFluidUseRuntime processFluids;
    private readonly IProductionWorkshopRuntime workshops;
    private readonly IPowerInfrastructureQuery power;
    private readonly IFluidInfrastructureTransaction water;
    private readonly IFluidWastewaterTransaction wastewater;
    private readonly IEnvironmentalFieldQuery environment;

    public ProductionCycleUtilityService(
        IProcessFluidUseRuntime processFluids,
        IProductionWorkshopRuntime workshops,
        IPowerInfrastructureQuery power,
        IFluidInfrastructureTransaction water,
        IFluidWastewaterTransaction wastewater,
        IEnvironmentalFieldQuery environment)
    {
        this.processFluids = processFluids
            ?? throw new ArgumentNullException(nameof(processFluids));
        this.workshops = workshops
            ?? throw new ArgumentNullException(nameof(workshops));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.wastewater = wastewater
            ?? throw new ArgumentNullException(nameof(wastewater));
        this.environment = environment
            ?? throw new ArgumentNullException(nameof(environment));
    }

    public bool ValidateCycleRequirements(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (record == null || recipe == null || facility == null)
        {
            failureReason = "production-cycle-target-missing";
            return false;
        }

        if (!workshops.HasRequiredSupports(
                facility,
                recipe.RequiredSupportTags,
                out failureReason))
        {
            return false;
        }

        if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            && !TryResolveAvailableBatchSupport(
                record,
                recipe,
                facility,
                allBills,
                out _,
                out _,
                out failureReason))
        {
            return false;
        }

        return ValidateFacilityUtilities(recipe, facility, out failureReason)
            && ValidateLinkedSupportUtilities(
                recipe,
                facility,
                out failureReason);
    }

    public bool ValidateProcessingUtilities(
        string occupiedSupportNodeId,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string failureReason)
    {
        ProductionSupportLinkSnapshot occupiedLink = workshops
            .GetLinks(facility)
            .FirstOrDefault(link =>
            {
                BuildingProductionSupportAbility candidate =
                    link.Support?.BuildingData.GetProductionSupportAbility();
                return candidate != null
                    && candidate.kind == ProductionSupportKind.BatchProcessor
                    && candidate.Provides(recipe.BatchSupportTag)
                    && string.Equals(
                        IndustrialInfrastructureIdentity.GetNodeId(link.Support),
                        occupiedSupportNodeId,
                        StringComparison.Ordinal);
            });
        if (occupiedLink == null)
        {
            failureReason = "occupied-batch-support-disconnected";
            return false;
        }

        return ValidateSupportUtilities(
            occupiedLink.Support,
            occupiedLink.Support.BuildingData.GetProductionSupportAbility(),
            out failureReason);
    }

    public bool TryConsumeCycleUtilities(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out ProductionProcessFluidReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        List<(BuildableObject Support, BuildingProductionSupportAbility Ability)>
            fluidSupports = new();
        HashSet<string> resolvedSupports = new(StringComparer.Ordinal);
        foreach (string tag in recipe.RequiredSupportTags)
        {
            if (!workshops.TryResolveSupport(
                    facility,
                    tag,
                    null,
                    out BuildableObject support,
                    out BuildingProductionSupportAbility ability))
            {
                failureReason = $"linked-support-missing:{tag}";
                return false;
            }

            string supportId = IndustrialInfrastructureIdentity.GetNodeId(support);
            if (resolvedSupports.Add(supportId)
                && (ability.cleanWaterPerCycle > 0f
                    || ability.wastewaterPerCycle > 0f))
            {
                fluidSupports.Add((support, ability));
            }
        }

        BuildingProcessFluidAbility facilityFluid =
            facility?.BuildingData?.GetAbility<BuildingProcessFluidAbility>();
        bool facilityFluidApplies = facilityFluid != null
            && facilityFluid.Supports(recipe.WorkTypeId);
        float facilityCleanWater = facilityFluidApplies
            ? Mathf.Max(0f, facilityFluid.cleanWaterPerCycle)
            : 0f;
        float facilityWastewater = facilityFluidApplies
            ? Mathf.Max(0f, facilityFluid.wastewaterPerCycle)
            : 0f;
        float recipeCleanWater = recipe.CleanWaterPerCycle;
        float recipeWastewater = recipe.WastewaterPerCycle;
        bool hasRecipeFluid = recipeCleanWater > 0f || recipeWastewater > 0f;
        bool combinedManualFallback =
            (facilityCleanWater <= 0f || facilityFluid.allowsManualWaterFallback)
            && (recipeCleanWater <= 0f || recipe.AllowsManualWaterFallback);
        float totalCleanWater = facilityCleanWater + recipeCleanWater;
        float totalWastewater = facilityWastewater + recipeWastewater;
        var facilityWastewaterComponents = new List<ProcessWastewaterComponent>(2);
        if (facilityWastewater > 0f)
        {
            if (facilityFluid.wastewaterComposition
                == ProcessWastewaterComposition.None)
            {
                failureReason = "facility-wastewater-composition-unassigned";
                return false;
            }
            facilityWastewaterComponents.Add(new ProcessWastewaterComponent(
                facilityFluid.wastewaterComposition,
                ProcessWastewaterSourceKind.Facility,
                IndustrialInfrastructureIdentity.GetNodeId(facility),
                facilityWastewater));
        }
        if (recipeWastewater > 0f)
        {
            if (recipe.WastewaterComposition
                == ProcessWastewaterComposition.None)
            {
                failureReason = "recipe-wastewater-composition-unassigned";
                return false;
            }
            facilityWastewaterComponents.Add(new ProcessWastewaterComponent(
                recipe.WastewaterComposition,
                ProcessWastewaterSourceKind.Recipe,
                recipe.RecipeId,
                recipeWastewater));
        }
        var fluidDemands = new List<ProcessFluidCycleDemand>(
            fluidSupports.Count + 1)
        {
            new ProcessFluidCycleDemand(
                facility,
                recipe.WorkTypeId,
                totalCleanWater,
                totalWastewater,
                combinedManualFallback,
                facilityWastewaterComponents)
        };
        foreach ((BuildableObject support, BuildingProductionSupportAbility ability)
                 in fluidSupports)
        {
            totalCleanWater += Mathf.Max(0f, ability.cleanWaterPerCycle);
            totalWastewater += Mathf.Max(0f, ability.wastewaterPerCycle);
            var supportWastewaterComponents =
                new List<ProcessWastewaterComponent>(1);
            if (ability.wastewaterPerCycle > 0f)
            {
                if (ability.wastewaterComposition
                    == ProcessWastewaterComposition.None)
                {
                    failureReason = "support-wastewater-composition-unassigned";
                    return false;
                }
                supportWastewaterComponents.Add(new ProcessWastewaterComponent(
                    ability.wastewaterComposition,
                    ProcessWastewaterSourceKind.Support,
                    IndustrialInfrastructureIdentity.GetNodeId(support),
                    Mathf.Max(0f, ability.wastewaterPerCycle)));
            }
            fluidDemands.Add(new ProcessFluidCycleDemand(
                support,
                recipe.WorkTypeId,
                Mathf.Max(0f, ability.cleanWaterPerCycle),
                Mathf.Max(0f, ability.wastewaterPerCycle),
                ability.allowsManualWaterFallback,
                supportWastewaterComponents));
        }

        if (record == null)
        {
            failureReason = "production-process-fluid-record-missing";
            return false;
        }
        string operationId =
            $"production-process-fluid:{record.billId.Value}:{record.cycleSequence:D8}";
        if (!processFluids.TryConsumeBatch(
                fluidDemands,
                operationId,
                out IReadOnlyList<ManualWaterTransferReceipt> manualTransfers,
                out IReadOnlyList<ProcessWastewaterComponent>
                    wastewaterComponents,
                out DomainFailure processFailure))
        {
            failureReason = processFailure.Code.ToString();
            return false;
        }

        receipt = new ProductionProcessFluidReceipt(
            ProductionFluidMassRules.ToMassGrams(totalCleanWater),
            wastewaterComponents.Aggregate(
                0L,
                (sum, value) => checked(sum + value.MassGrams)),
            manualTransfers.Select(value =>
                new ProductionManualWaterTransferSaveData
                {
                    operationId = value.OperationId,
                    physicalCommitId = value.PhysicalCommitId,
                    destinationId = value.DestinationId,
                    requestedWaterUnits = value.RequestedWaterUnits,
                    transferredWaterUnits = value.TransferredWaterUnits,
                    inputMassGrams = value.InputMassGrams,
                    sourceStackIds = value.SourceStackIds.ToList()
                }).ToArray(),
            wastewaterComponents);
        return true;
    }

    public bool TryConsumeCycleUtilities(
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out ProductionProcessFluidReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = "production-process-fluid-requires-bill-authority";
        return false;
    }

    public bool AcknowledgeCycleUtilities(
        ProductionProcessFluidReceipt receipt,
        out string failureReason)
    {
        if (!processFluids.AcknowledgeManualTransfers(
                receipt.ManualWaterTransfers
                    .Select(value => value.operationId)
                    .ToArray(),
                out DomainFailure failure))
        {
            failureReason = failure.Code.ToString();
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    public bool TryResolveBatchSupport(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out string supportNodeId,
        out string failureReason)
    {
        supportNodeId = string.Empty;
        if (!TryResolveAvailableBatchSupport(
                record,
                recipe,
                facility,
                allBills,
                out BuildableObject support,
                out _,
                out failureReason))
        {
            return false;
        }

        supportNodeId = IndustrialInfrastructureIdentity.GetNodeId(support);
        return true;
    }

    public float ResolveTemperatureSpeed(
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out bool dangerous)
    {
        dangerous = false;
        if (!environment.TryGetCell(
                facility.centerPos,
                out EnvironmentalCellSnapshot cell))
        {
            return 1f;
        }

        float temperature = cell.TemperatureC;
        if (temperature >= recipe.OptimalTemperatureMinimum
            && temperature <= recipe.OptimalTemperatureMaximum)
        {
            return 1f;
        }

        if (temperature >= recipe.WarningTemperatureMinimum
            && temperature <= recipe.WarningTemperatureMaximum)
        {
            return 0.5f;
        }

        dangerous = true;
        return 0f;
    }

    public BuildableObject ResolveOccupiedBatchSupport(
        string occupiedSupportNodeId,
        BuildableObject facility)
    {
        if (facility == null
            || string.IsNullOrWhiteSpace(occupiedSupportNodeId))
        {
            return null;
        }

        return workshops.GetLinks(facility)
            .Select(link => link?.Support)
            .FirstOrDefault(support => support != null
                && string.Equals(
                    IndustrialInfrastructureIdentity.GetNodeId(support),
                    occupiedSupportNodeId,
                    StringComparison.Ordinal));
    }

    private bool ValidateFacilityUtilities(
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        BuildingProcessFluidAbility facilityFluid =
            facility?.BuildingData?.GetAbility<BuildingProcessFluidAbility>();
        bool facilityFluidApplies = facilityFluid != null
            && facilityFluid.Supports(recipe.WorkTypeId);
        float facilityCleanWater = facilityFluidApplies
            ? Mathf.Max(0f, facilityFluid.cleanWaterPerCycle)
            : 0f;
        float facilityWastewater = facilityFluidApplies
            ? Mathf.Max(0f, facilityFluid.wastewaterPerCycle)
            : 0f;
        float requiredCleanWater = checked(
            facilityCleanWater + recipe.CleanWaterPerCycle);
        float requiredWastewater = checked(
            facilityWastewater + recipe.WastewaterPerCycle);
        bool combinedManualFallback =
            (facilityCleanWater <= 0f || facilityFluid.allowsManualWaterFallback)
            && (recipe.CleanWaterPerCycle <= 0f
                || recipe.AllowsManualWaterFallback);
        if (requiredWastewater > 0f
            && !wastewater.CanAcceptWastewater(
                facility,
                requiredWastewater,
                out DomainFailure wastewaterFailure))
        {
            failureReason = wastewaterFailure.Code.ToString();
            return false;
        }

        WorldWaterQuality minimumQuality = recipe.CleanWaterPerCycle > 0f
            ? WorldWaterQuality.Clean
            : facilityFluid?.minimumQuality ?? WorldWaterQuality.Clean;
        if (requiredCleanWater <= 0f
            || water.CanConsume(
                facility,
                minimumQuality,
                requiredCleanWater,
                out DomainFailure waterFailure))
        {
            return true;
        }

        if (combinedManualFallback)
        {
            failureReason = string.Empty;
            return true;
        }

        failureReason = waterFailure.Code.ToString();
        return false;
    }

    private bool ValidateLinkedSupportUtilities(
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        HashSet<string> checkedSupports = new(StringComparer.Ordinal);
        foreach (string tag in recipe.RequiredSupportTags)
        {
            if (!workshops.TryResolveSupport(
                    facility,
                    tag,
                    null,
                    out BuildableObject support,
                    out BuildingProductionSupportAbility ability))
            {
                failureReason = $"linked-support-missing:{tag}";
                return false;
            }

            string supportId = IndustrialInfrastructureIdentity.GetNodeId(support);
            if (checkedSupports.Add(supportId)
                && !ValidateSupportUtilities(
                    support,
                    ability,
                    out failureReason))
            {
                return false;
            }
        }

        return true;
    }

    private bool ValidateSupportUtilities(
        BuildableObject support,
        BuildingProductionSupportAbility ability,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (support == null || ability == null || support.IsGridDestroyed)
        {
            failureReason = "linked-support-unavailable";
            return false;
        }

        if (ability.requiresPower && !power.IsPowered(support))
        {
            failureReason = "linked-support-power-insufficient";
            return false;
        }

        if (ability.wastewaterPerCycle > 0f
            && !wastewater.CanAcceptWastewater(
                support,
                ability.wastewaterPerCycle,
                out DomainFailure wastewaterFailure))
        {
            failureReason = wastewaterFailure.Code.ToString();
            return false;
        }

        if (ability.cleanWaterPerCycle <= 0f
            || water.CanConsume(
                support,
                WorldWaterQuality.Clean,
                ability.cleanWaterPerCycle,
                out DomainFailure waterFailure))
        {
            return true;
        }

        if (ability.allowsManualWaterFallback)
        {
            failureReason = string.Empty;
            return true;
        }

        failureReason = waterFailure.Code.ToString();
        return false;
    }

    private bool TryResolveAvailableBatchSupport(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        IReadOnlyList<ProductionBillRecord> allBills,
        out BuildableObject support,
        out BuildingProductionSupportAbility ability,
        out string failureReason)
    {
        support = null;
        ability = null;
        foreach (ProductionSupportLinkSnapshot link in workshops.GetLinks(facility))
        {
            BuildingProductionSupportAbility candidate =
                link.Support?.BuildingData.GetProductionSupportAbility();
            if (candidate == null
                || candidate.kind != ProductionSupportKind.BatchProcessor
                || !candidate.Provides(recipe.BatchSupportTag))
            {
                continue;
            }

            string nodeId = IndustrialInfrastructureIdentity.GetNodeId(link.Support);
            int occupied = (allBills ?? Array.Empty<ProductionBillRecord>())
                .Count(other => other != record
                    && other.batchStage == ProductionBatchStage.Processing
                    && string.Equals(
                        other.occupiedSupportNodeId,
                        nodeId,
                        StringComparison.Ordinal));
            if (occupied >= candidate.BatchCapacity)
            {
                continue;
            }

            support = link.Support;
            ability = candidate;
            failureReason = string.Empty;
            return true;
        }

        bool hasMatchingSupport = workshops.GetLinks(facility).Any(link =>
            link.Support?.BuildingData.GetProductionSupportAbility()
                is BuildingProductionSupportAbility candidate
            && candidate.kind == ProductionSupportKind.BatchProcessor
            && candidate.Provides(recipe.BatchSupportTag));
        failureReason = hasMatchingSupport
            ? "batch-support-capacity-full"
            : $"batch-support-missing:{recipe.BatchSupportTag}";
        return false;
    }
}
