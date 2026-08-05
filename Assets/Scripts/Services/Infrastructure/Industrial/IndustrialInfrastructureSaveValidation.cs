using System;
using System.Collections.Generic;

internal static class IndustrialInfrastructureSaveValidation
{
    public static void RequireValid(DungeonPowerInfrastructureSaveData data) =>
        RequireValid(report => Validate(data, report), "power infrastructure");

    public static void RequireValid(DungeonFluidInfrastructureSaveData data) =>
        RequireValid(report => Validate(data, report), "fluid infrastructure");

    public static void RequireValid(
        DungeonConveyorInfrastructureSaveData data) =>
        RequireValid(report => Validate(data, report), "conveyor infrastructure");

    public static void RequireValid(DungeonAutomationSaveData data) =>
        RequireValid(report => Validate(data, report), "automation infrastructure");

    public static void Validate(
        DungeonPowerInfrastructureSaveData data,
        DungeonGameRestoreReport report)
    {
        if (data == null || data.version != DungeonPowerInfrastructureSaveData.CurrentVersion)
        {
            report.AddError("Power infrastructure payload version is invalid.");
            return;
        }

        HashSet<string> ids = NewIds();
        foreach (PowerNodeSaveData node in data.nodes ?? new List<PowerNodeSaveData>())
        {
            if (!ValidateBuildingId(
                    node?.buildingInstanceId,
                    ids,
                    "power node",
                    report))
            {
                continue;
            }

            if (!Enum.IsDefined(typeof(PowerPriority), node.priority)
                || !IsNonNegativeFinite(node.storedPower)
                || !IsNonNegativeFinite(node.fuelSeconds)
                || !IsNonNegativeFinite(node.heat)
                || !IsRangeFinite(node.fault, 0f, 100f))
            {
                report.AddError(
                    $"Power node {node.buildingInstanceId} has invalid state.");
            }
        }
    }

    public static void Validate(
        DungeonFluidInfrastructureSaveData data,
        DungeonGameRestoreReport report)
    {
        if (data == null || data.version != DungeonFluidInfrastructureSaveData.CurrentVersion)
        {
            report.AddError("Fluid infrastructure payload version is invalid.");
            return;
        }

        HashSet<string> ids = NewIds();
        foreach (FluidNodeSaveData node in data.nodes ?? new List<FluidNodeSaveData>())
        {
            if (!ValidateBuildingId(
                    node?.buildingInstanceId,
                    ids,
                    "fluid node",
                    report))
            {
                continue;
            }

            if (!IsNonNegativeFinite(node.cleanWater)
                || !IsNonNegativeFinite(node.unsafeWater)
                || !IsNonNegativeFinite(node.foulWater)
                || !IsNonNegativeFinite(node.wastewater)
                || !IsRangeFinite(node.blockage, 0f, 100f)
                || !IsRangeFinite(node.leak, 0f, 100f)
                || !IsNonNegativeFinite(node.processorWork)
                || !IsNonNegativeFinite(node.manualWaterReserve)
                || !Enum.IsDefined(typeof(WaterContainerTransferMode), node.transferMode)
                || !IsNonNegativeFinite(node.transferWork))
            {
                report.AddError(
                    $"Fluid node {node.buildingInstanceId} has invalid state.");
            }
        }
    }

    public static void Validate(
        DungeonConveyorInfrastructureSaveData data,
        DungeonGameRestoreReport report)
    {
        if (data == null
            || data.version != DungeonConveyorInfrastructureSaveData.CurrentVersion
            || data.nextPayloadSequence < 1)
        {
            report.AddError("Conveyor infrastructure payload header is invalid.");
            return;
        }

        HashSet<string> nodeIds = NewIds();
        foreach (ConveyorNodeSaveData node in data.nodes ?? new List<ConveyorNodeSaveData>())
        {
            if (!ValidateBuildingId(
                    node?.buildingInstanceId,
                    nodeIds,
                    "conveyor node",
                    report))
            {
                continue;
            }

            ConveyorFilterSaveData filter = node.filter;
            if (!Enum.IsDefined(typeof(ConveyorOverflowPolicy), node.overflowPolicy)
                || filter == null
                || !Enum.IsDefined(typeof(CombatEquipmentQuality), filter.minimumQuality)
                || !Enum.IsDefined(typeof(CombatEquipmentQuality), filter.maximumQuality)
                || filter.minimumQuality > filter.maximumQuality
                || !IsRangeFinite(filter.minimumFreshness01, 0f, 1f)
                || !IsRangeFinite(filter.maximumFreshness01, 0f, 1f)
                || filter.minimumFreshness01 > filter.maximumFreshness01)
            {
                report.AddError(
                    $"Conveyor node {node.buildingInstanceId} has invalid routing state.");
            }
        }

        HashSet<string> payloadIds = NewIds();
        foreach (ConveyorPayloadSaveData payload in data.payloads
                     ?? new List<ConveyorPayloadSaveData>())
        {
            if (!ValidateId(payload?.payloadId, payloadIds, "conveyor payload", report))
            {
                continue;
            }

            if (!new BuildingInstanceId(
                    payload.segmentBuildingInstanceId).IsValid
                || !new ItemStackId(payload.itemStackId).IsValid
                || !IsRangeFinite(payload.progress, 0f, 1f)
                || !IsNonNegativeFinite(payload.lastMovedAt)
                || !IsNonNegativeFinite(payload.stalledSince)
                || !Enum.IsDefined(
                    typeof(ConveyorStallReason),
                    payload.stallReason))
            {
                report.AddError($"Conveyor payload {payload.payloadId} has invalid state.");
            }
        }
    }

    public static void Validate(
        DungeonAutomationSaveData data,
        DungeonGameRestoreReport report)
    {
        if (data == null || data.version != DungeonAutomationSaveData.CurrentVersion)
        {
            report.AddError("Automation payload version is invalid.");
            return;
        }

        HashSet<string> ids = NewIds();
        foreach (AutomationFacilitySaveData facility in data.facilities
                     ?? new List<AutomationFacilitySaveData>())
        {
            if (!ValidateBuildingId(
                    facility?.buildingInstanceId,
                    ids,
                    "automation facility",
                    report))
            {
                continue;
            }

            if (!Enum.IsDefined(typeof(AutomationMode), facility.mode)
                || !IsRangeFinite(facility.maintenance, 0f, 100f)
                || !IsRangeFinite(facility.fault, 0f, 100f))
            {
                report.AddError(
                    $"Automation facility {facility.buildingInstanceId} has invalid state.");
            }
        }
    }

    private static HashSet<string> NewIds() =>
        new HashSet<string>(StringComparer.Ordinal);

    private static void RequireValid(
        Action<DungeonGameRestoreReport> validate,
        string label)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        validate(report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                $"Invalid {label} restore candidate: "
                + string.Join(" | ", report.Errors));
        }
    }

    private static bool ValidateId(
        string value,
        ISet<string> ids,
        string label,
        DungeonGameRestoreReport report)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            report.AddError($"Industrial payload contains a blank {label} ID.");
            return false;
        }

        if (!ids.Add(normalized))
        {
            report.AddError($"Industrial payload contains duplicate {label} {normalized}.");
            return false;
        }

        return true;
    }

    private static bool ValidateBuildingId(
        string value,
        ISet<string> ids,
        string label,
        DungeonGameRestoreReport report)
    {
        if (!new BuildingInstanceId(value).IsValid)
        {
            report.AddError(
                $"Industrial payload contains an invalid {label} building ID.");
            return false;
        }

        return ValidateId(value, ids, label, report);
    }

    private static bool IsNonNegativeFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool IsRangeFinite(float value, float minimum, float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;
}
