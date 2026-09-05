using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ProcessFluidUseRuntime : IProcessFluidUseRuntime
{
    private readonly IFluidInfrastructureTransaction water;
    private readonly IFluidInfrastructureBatchTransaction batchWater;
    private readonly IManualWaterTransferTransaction manualWater;
    private readonly IFluidWastewaterTransaction wastewater;
    private readonly IWorldItemStackRuntime items;
    private readonly IFluidFacilityInputOwnerAuthority inputOwners;

    public ProcessFluidUseRuntime(
        IFluidInfrastructureTransaction water,
        IFluidWastewaterTransaction wastewater,
        IWorldItemStackRuntime items)
    {
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        inputOwners = water as IFluidFacilityInputOwnerAuthority
            ?? throw new ArgumentException(
                "Process fluid requires exact input-owner authority.",
                nameof(water));
        batchWater = water as IFluidInfrastructureBatchTransaction;
        manualWater = water as IManualWaterTransferTransaction;
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
        if (!inputOwners.TryEnsureManualDestination(
                facility,
                destinationId,
                ability.cleanWaterPerCycle,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Process-fluid destination authority is unavailable: "
                + ownerFailure);
        }
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
            items.TryRequestItemDelivery(
                FluidFacilityInputOwnerProjectionAuthority.CleanWaterItemId,
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
        if (ability.wastewaterPerCycle > 0f
            && ability.wastewaterComposition
                == ProcessWastewaterComposition.None)
        {
            failure = new DomainFailure(FailureCode.IndustrialCommandInvalid);
            return false;
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
            if (!inputOwners.TryEnsureManualDestination(
                    facility,
                    destinationId,
                    ability.cleanWaterPerCycle,
                    out string ownerFailure))
            {
                throw new InvalidOperationException(
                    "Process-fluid destination authority is unavailable: "
                    + ownerFailure);
            }
            consumed = water.TryConsumeManualContainer(
                facility,
                destinationId,
                ability.cleanWaterPerCycle,
                out _);
            if (!consumed)
            {
                items.TryRequestItemDelivery(
                    FluidFacilityInputOwnerProjectionAuthority.CleanWaterItemId,
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
            if (!inputOwners.TryEnsureManualDestination(
                    facility,
                    destinationId,
                    requiredWater,
                    out string ownerFailure))
            {
                throw new InvalidOperationException(
                    "Process-fluid destination authority is unavailable: "
                    + ownerFailure);
            }
            consumed = water.TryConsumeManualContainer(
                facility,
                destinationId,
                requiredWater,
                out _);
            if (!consumed)
            {
                items.TryRequestItemDelivery(
                    FluidFacilityInputOwnerProjectionAuthority.CleanWaterItemId,
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

    public bool TryConsumeBatch(
        IReadOnlyList<ProcessFluidCycleDemand> demands,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (demands == null)
        {
            failure = new DomainFailure(FailureCode.IndustrialCommandInvalid);
            return false;
        }

        var pipedDemands = new List<FluidNetworkBatchDemand>(demands.Count);
        bool requiresManualContainerFallback = false;
        for (int i = 0; i < demands.Count; i++)
        {
            ProcessFluidCycleDemand demand = demands[i];
            if (demand.Facility == null
                || float.IsNaN(demand.CleanWater)
                || float.IsInfinity(demand.CleanWater)
                || demand.CleanWater < 0f
                || float.IsNaN(demand.Wastewater)
                || float.IsInfinity(demand.Wastewater)
                || demand.Wastewater < 0f
                || !HasExactWastewaterComposition(demand))
            {
                failure = new DomainFailure(FailureCode.IndustrialCommandInvalid);
                return false;
            }

            bool canUsePipedWater = demand.CleanWater <= 0f
                || water.CanConsume(
                    demand.Facility,
                    WorldWaterQuality.Clean,
                    demand.CleanWater,
                    out _);
            requiresManualContainerFallback |=
                !canUsePipedWater && demand.AllowsManualWaterFallback;
            pipedDemands.Add(new FluidNetworkBatchDemand(
                demand.Facility,
                WorldWaterQuality.Clean,
                demand.CleanWater,
                demand.Wastewater));
        }

        if (batchWater != null && !requiresManualContainerFallback)
        {
            return batchWater.TryCommitBatch(pipedDemands, out failure);
        }

        // Manual-container fallback still uses its physical-item delivery path.
        // It is intentionally not presented as part of the piped-network atomic
        // transaction until exact container-lot and wastewater byproduct
        // ownership are implemented.
        for (int i = 0; i < demands.Count; i++)
        {
            ProcessFluidCycleDemand demand = demands[i];
            if (!TryConsumeCycle(
                    demand.Facility,
                    demand.WorkTypeId,
                    demand.CleanWater,
                    demand.Wastewater,
                    demand.AllowsManualWaterFallback,
                    out failure))
            {
                return false;
            }
        }

        return true;
    }

    public bool TryConsumeBatch(
        IReadOnlyList<ProcessFluidCycleDemand> demands,
        string operationId,
        out IReadOnlyList<ManualWaterTransferReceipt> manualTransfers,
        out IReadOnlyList<ProcessWastewaterComponent> wastewaterComponents,
        out DomainFailure failure)
    {
        manualTransfers = Array.Empty<ManualWaterTransferReceipt>();
        wastewaterComponents = Array.Empty<ProcessWastewaterComponent>();
        failure = DomainFailure.None;
        string operation = operationId ?? string.Empty;
        if (demands == null
            || operation.Length == 0
            || !string.Equals(operation, operation.Trim(), StringComparison.Ordinal)
            || batchWater == null
            || manualWater == null)
        {
            failure = new DomainFailure(FailureCode.IndustrialCommandInvalid);
            return false;
        }

        var adjustedDemands = new List<FluidNetworkBatchDemand>(demands.Count);
        var staged = new List<(ProcessFluidCycleDemand Demand, ManualWaterTransferReceipt Receipt)>();
        for (int i = 0; i < demands.Count; i++)
        {
            ProcessFluidCycleDemand demand = demands[i];
            if (demand.Facility == null
                || float.IsNaN(demand.CleanWater)
                || float.IsInfinity(demand.CleanWater)
                || demand.CleanWater < 0f
                || float.IsNaN(demand.Wastewater)
                || float.IsInfinity(demand.Wastewater)
                || demand.Wastewater < 0f
                || !HasExactWastewaterComposition(demand))
            {
                failure = new DomainFailure(FailureCode.IndustrialCommandInvalid);
                return false;
            }

            bool canUsePiped = demand.CleanWater <= 0f
                || water.CanConsume(
                    demand.Facility,
                    WorldWaterQuality.Clean,
                    demand.CleanWater,
                    out _);
            float pipedWater = demand.CleanWater;
            if (!canUsePiped)
            {
                if (!demand.AllowsManualWaterFallback)
                {
                    failure = new DomainFailure(FailureCode.FluidInsufficientWater);
                    return false;
                }
                string nodeId = IndustrialInfrastructureIdentity.GetNodeId(
                    demand.Facility);
                string destinationId =
                    $"plumbing:process-water:{nodeId}:{demand.WorkTypeId.Value}";
                if (!inputOwners.TryEnsureManualDestination(
                        demand.Facility,
                        destinationId,
                        demand.CleanWater,
                        out string ownerFailure))
                {
                    throw new InvalidOperationException(
                        "Process-fluid batch destination authority is unavailable: "
                        + ownerFailure);
                }
                string manualOperation =
                    $"{operation}:manual-water:{i:D4}:{nodeId}";
                if (!manualWater.TryStageManualWaterTransfer(
                        demand.Facility,
                        destinationId,
                        demand.CleanWater,
                        manualOperation,
                        out ManualWaterTransferReceipt transfer,
                        out failure))
                {
                    return false;
                }
                staged.Add((demand, transfer));
                pipedWater = 0f;
            }
            adjustedDemands.Add(new FluidNetworkBatchDemand(
                demand.Facility,
                WorldWaterQuality.Clean,
                pipedWater,
                demand.Wastewater));
        }

        if (!batchWater.TryCommitBatch(adjustedDemands, out failure))
        {
            return false;
        }

        var applied = new List<ManualWaterTransferReceipt>(staged.Count);
        foreach ((ProcessFluidCycleDemand demand, ManualWaterTransferReceipt transfer)
                 in staged)
        {
            if (!manualWater.TryApplyStagedManualWaterTransfer(
                    demand.Facility,
                    transfer.OperationId,
                    out ManualWaterTransferReceipt appliedTransfer,
                    out failure))
            {
                return false;
            }
            applied.Add(appliedTransfer);
        }
        manualTransfers = applied;
        wastewaterComponents = demands
            .SelectMany(value => value.WastewaterComponents)
            .OrderBy(value => (int)value.Composition)
            .ThenBy(value => (int)value.SourceKind)
            .ThenBy(value => value.SourceStableId, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    private static bool HasExactWastewaterComposition(
        ProcessFluidCycleDemand demand)
    {
        IReadOnlyList<ProcessWastewaterComponent> components =
            demand.WastewaterComponents
            ?? Array.Empty<ProcessWastewaterComponent>();
        if (demand.Wastewater <= 0f)
        {
            return components.Count == 0;
        }
        if (components.Count == 0)
        {
            return false;
        }

        float total = 0f;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < components.Count; i++)
        {
            ProcessWastewaterComponent component = components[i];
            string key = $"{(int)component.Composition:D3}:"
                + $"{(int)component.SourceKind:D3}:{component.SourceStableId}";
            if (!keys.Add(key))
            {
                return false;
            }
            total += component.AuthoredUnits;
        }
        return Mathf.Abs(total - demand.Wastewater) <= 0.0001f;
    }

    public bool AcknowledgeManualTransfers(
        IReadOnlyList<string> operationIds,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        foreach (string operationId in (operationIds ?? Array.Empty<string>())
                     .Where(value => !string.IsNullOrEmpty(value))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            if (manualWater == null
                || !manualWater.AcknowledgeManualWaterTransfer(
                    operationId,
                    out failure))
            {
                return false;
            }
        }
        return true;
    }
}
