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
        ProductionRecipeSO recipe,
        BuildableObject facility,
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
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!processFluids.TryConsumeCycle(
                facility,
                recipe.WorkTypeId,
                out DomainFailure processFailure))
        {
            failureReason = processFailure.Code.ToString();
            return false;
        }

        if ((recipe.CleanWaterPerCycle > 0f
                || recipe.WastewaterPerCycle > 0f)
            && !processFluids.TryConsumeCycle(
                facility,
                recipe.WorkTypeId,
                recipe.CleanWaterPerCycle,
                recipe.WastewaterPerCycle,
                recipe.AllowsManualWaterFallback,
                out processFailure))
        {
            failureReason = processFailure.Code.ToString();
            return false;
        }

        HashSet<string> consumedSupports = new(StringComparer.Ordinal);
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
            if (!consumedSupports.Add(supportId)
                || ability.cleanWaterPerCycle <= 0f
                    && ability.wastewaterPerCycle <= 0f)
            {
                continue;
            }

            if (!processFluids.TryConsumeCycle(
                    support,
                    recipe.WorkTypeId,
                    ability.cleanWaterPerCycle,
                    ability.wastewaterPerCycle,
                    ability.allowsManualWaterFallback,
                    out processFailure))
            {
                failureReason = processFailure.Code.ToString();
                return false;
            }
        }

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
        if (recipe.WastewaterPerCycle > 0f
            && !wastewater.CanAcceptWastewater(
                facility,
                recipe.WastewaterPerCycle,
                out DomainFailure wastewaterFailure))
        {
            failureReason = wastewaterFailure.Code.ToString();
            return false;
        }

        if (recipe.CleanWaterPerCycle <= 0f
            || water.CanConsume(
                facility,
                WorldWaterQuality.Clean,
                recipe.CleanWaterPerCycle,
                out DomainFailure waterFailure))
        {
            return true;
        }

        if (recipe.AllowsManualWaterFallback)
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
