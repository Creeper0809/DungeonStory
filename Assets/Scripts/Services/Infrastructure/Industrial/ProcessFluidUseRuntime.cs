using System;
using UnityEngine;

public sealed class ProcessFluidUseRuntime : IProcessFluidUseRuntime
{
    private readonly IWaterNetworkRuntime water;
    private readonly IWastewaterNetworkRuntime wastewater;
    private readonly IWorldItemStackRuntime items;

    public ProcessFluidUseRuntime(
        IWaterNetworkRuntime water,
        IWastewaterNetworkRuntime wastewater,
        IWorldItemStackRuntime items)
    {
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.wastewater = wastewater
            ?? throw new ArgumentNullException(nameof(wastewater));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public bool TryConsumeCycle(
        BuildableObject facility,
        WorkTypeId workTypeId,
        out string failureReason)
    {
        failureReason = string.Empty;
        BuildingProcessFluidAbility ability =
            facility?.BuildingData?.GetAbility<BuildingProcessFluidAbility>();
        if (facility == null
            || ability == null
            || !ability.Supports(workTypeId))
        {
            return true;
        }

        string drainFailure = string.Empty;
        bool canDrain = ability.wastewaterPerCycle <= 0f
            || wastewater.CanAcceptWastewater(
                facility,
                ability.wastewaterPerCycle,
                out drainFailure);
        if (!canDrain && !ability.allowsManualWaterFallback)
        {
            failureReason = string.IsNullOrWhiteSpace(drainFailure)
                ? "폐수를 배출할 공간이 부족합니다."
                : drainFailure;
            return false;
        }

        bool consumed = water.TryConsume(
            facility,
            ability.minimumQuality,
            ability.cleanWaterPerCycle,
            out _,
            out string pipeFailure);
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
                failureReason = "공정용 물통을 운반하는 중입니다.";
                return false;
            }
        }

        if (!consumed)
        {
            failureReason = string.IsNullOrWhiteSpace(pipeFailure)
                ? "공정에 사용할 깨끗한 물이 부족합니다."
                : pipeFailure;
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
}
