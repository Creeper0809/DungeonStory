using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ProcessFluidUseRuntime : IProcessFluidUseRuntime
{
    private readonly IFluidInfrastructureTransaction water;
    private readonly IFluidWastewaterTransaction wastewater;
    private readonly IWorldItemStackRuntime items;

    public ProcessFluidUseRuntime(
        IFluidInfrastructureTransaction water,
        IFluidWastewaterTransaction wastewater,
        IWorldItemStackRuntime items)
    {
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.wastewater = wastewater
            ?? throw new ArgumentNullException(nameof(wastewater));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public bool EnsureCycleSupply(
        BuildableObject facility,
        WorkTypeId workTypeId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        BuildingProcessFluidAbility ability =
            facility?.BuildingData?.GetAbility<BuildingProcessFluidAbility>();
        if (facility == null
            || ability == null
            || !ability.Supports(workTypeId)
            || ability.cleanWaterPerCycle <= 0f)
        {
            return true;
        }

        if (water.CanConsume(
                facility,
                ability.minimumQuality,
                ability.cleanWaterPerCycle,
                out _))
        {
            return true;
        }

        if (!ability.allowsManualWaterFallback)
        {
            failure = new DomainFailure(FailureCode.FluidInsufficientWater);
            return false;
        }

        string facilityId = IndustrialInfrastructureIdentity.GetNodeId(facility);
        string destinationId =
            $"plumbing:process-water:{facilityId}:{workTypeId.Value}";
        int requiredContainers = Mathf.Max(
            1,
            Mathf.CeilToInt(ability.cleanWaterPerCycle));
        IReadOnlyList<WorldItemStackSnapshot> physicalStacks =
            items.GetAllStacks();
        bool buffered = physicalStacks.Any(stack =>
            stack != null
            && stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal)
            && string.Equals(
                stack.ItemId,
                "resource:clean-water",
                StringComparison.Ordinal)
            && stack.AvailableQuantity >= requiredContainers);
        if (buffered)
        {
            return true;
        }

        bool alreadyRequested = physicalStacks.Any(stack =>
            stack != null
            && string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal)
            && stack.Quantity > 0);
        if (!alreadyRequested)
        {
            items.TryRequestFacilityDelivery(
                StockCategory.Water,
                requiredContainers,
                facility.centerPos,
                destinationId,
                out _,
                out _);
        }

        failure = new DomainFailure(
            FailureCode.FluidManualWaterUnavailable,
            destinationId);
        return false;
    }

    public bool TryConsumeCycle(
        BuildableObject facility,
        WorkTypeId workTypeId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        BuildingProcessFluidAbility ability =
            facility?.BuildingData?.GetAbility<BuildingProcessFluidAbility>();
        if (facility == null
            || ability == null
            || !ability.Supports(workTypeId))
        {
            return true;
        }

        DomainFailure drainFailure = DomainFailure.None;
        bool canDrain = ability.wastewaterPerCycle <= 0f
            || wastewater.CanAcceptWastewater(
                facility,
                ability.wastewaterPerCycle,
                out drainFailure);
        if (!canDrain && !ability.allowsManualWaterFallback)
        {
            failure = drainFailure.IsFailure
                ? drainFailure
                : new DomainFailure(FailureCode.FluidWastewaterUnavailable);
            return false;
        }

        bool consumed = water.TryConsume(
            facility,
            ability.minimumQuality,
            ability.cleanWaterPerCycle,
            out _,
            out DomainFailure pipeFailure);
        if (!consumed && ability.allowsManualWaterFallback)
        {
            string facilityId =
                IndustrialInfrastructureIdentity.GetNodeId(facility);
            string destinationId =
                $"plumbing:process-water:{facilityId}:{workTypeId.Value}";
            consumed = water.TryConsumeManualContainer(
                facility,
                destinationId,
                ability.cleanWaterPerCycle,
                out _);
            if (!consumed)
            {
                items.TryRequestFacilityDelivery(
                    StockCategory.Water,
                    1,
                    facility.centerPos,
                    destinationId,
                    out _,
                    out _);
                failure = new DomainFailure(
                    FailureCode.FluidManualWaterUnavailable);
                return false;
            }
        }

        if (!consumed)
        {
            failure = pipeFailure.IsFailure
                ? pipeFailure
                : new DomainFailure(FailureCode.FluidInsufficientWater);
            return false;
        }

        if (ability.wastewaterPerCycle > 0f && canDrain)
        {
            wastewater.TryAddWastewater(
                facility,
                ability.wastewaterPerCycle,
                out _,
                out _);
        }
        else if (ability.wastewaterPerCycle > 0f)
        {
            items.SpawnItemAt(
                IndustrialItemDefinitions.SludgeId,
                Mathf.Max(1, Mathf.CeilToInt(
                    ability.wastewaterPerCycle)),
                facility.centerPos,
                WorldItemStackState.Loose,
                string.Empty,
                out _);
        }

        return true;
    }

    public bool TryConsumeCycle(
        BuildableObject facility,
        WorkTypeId workTypeId,
        float cleanWater,
        float wastewaterAmount,
        bool allowsManualWaterFallback,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (facility == null)
        {
            failure = new DomainFailure(
                FailureCode.IndustrialBuildingUnavailable);
            return false;
        }

        float requiredWater = Mathf.Max(0f, cleanWater);
        float requiredWastewater = Mathf.Max(0f, wastewaterAmount);
        if (requiredWastewater > 0f
            && !wastewater.CanAcceptWastewater(
                facility,
                requiredWastewater,
                out DomainFailure drainFailure))
        {
            failure = drainFailure.IsFailure
                ? drainFailure
                : new DomainFailure(FailureCode.FluidWastewaterUnavailable);
            return false;
        }

        bool consumed = water.TryConsume(
            facility,
            WorldWaterQuality.Clean,
            requiredWater,
            out _,
            out DomainFailure pipeFailure);
        if (!consumed && allowsManualWaterFallback)
        {
            string facilityId =
                IndustrialInfrastructureIdentity.GetNodeId(facility);
            string destinationId =
                $"plumbing:process-water:{facilityId}:{workTypeId.Value}";
            consumed = water.TryConsumeManualContainer(
                facility,
                destinationId,
                requiredWater,
                out _);
            if (!consumed)
            {
                items.TryRequestFacilityDelivery(
                    StockCategory.Water,
                    Mathf.Max(1, Mathf.CeilToInt(requiredWater)),
                    facility.centerPos,
                    destinationId,
                    out _,
                    out _);
                failure = new DomainFailure(
                    FailureCode.FluidManualWaterUnavailable);
                return false;
            }
        }

        if (!consumed)
        {
            failure = pipeFailure.IsFailure
                ? pipeFailure
                : new DomainFailure(FailureCode.FluidInsufficientWater);
            return false;
        }

        if (requiredWastewater > 0f)
        {
            wastewater.TryAddWastewater(
                facility,
                requiredWastewater,
                out _,
                out _);
        }

        return true;
    }
}
