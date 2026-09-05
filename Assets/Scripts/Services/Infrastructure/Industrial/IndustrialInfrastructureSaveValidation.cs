using System;
using System.Collections.Generic;
using System.Linq;

public static class IndustrialInfrastructureSaveValidation
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
                || !IsRangeFinite(node.fault, 0f, 100f)
                || node.nextFuelOperationSequence <= 0
                || !ValidatePowerFuelCommit(node))
            {
                report.AddError(
                    $"Power node {node.buildingInstanceId} has invalid state.");
            }
        }
    }

    private static bool ValidatePowerFuelCommit(PowerNodeSaveData node)
    {
        PowerFuelCommitSaveData pending = node.pendingFuel;
        if (pending == null)
        {
            return false;
        }
        PowerFuelCommitPhase phase = (PowerFuelCommitPhase)pending.phase;
        if (phase == PowerFuelCommitPhase.None)
        {
            return pending.operationSequence == 0
                && string.IsNullOrEmpty(pending.operationId)
                && string.IsNullOrEmpty(pending.reasonCode)
                && string.IsNullOrEmpty(pending.nodeId)
                && string.IsNullOrEmpty(pending.destinationId)
                && string.IsNullOrEmpty(pending.itemId)
                && pending.quantity == 0
                && MathfApproximately(pending.fuelSecondsBefore, 0f)
                && MathfApproximately(pending.fuelSecondsAfter, 0f)
                && string.IsNullOrEmpty(pending.commitId)
                && (pending.sourceStackIds?.Count ?? 0) == 0
                && pending.inputMassGrams == 0L;
        }

        bool common = phase is PowerFuelCommitPhase.IntentRecorded
                or PowerFuelCommitPhase.OutcomePublished
            && pending.operationSequence == node.nextFuelOperationSequence
            && pending.operationSequence > 0
            && IsCanonicalRequired(pending.operationId)
            && IsCanonicalRequired(pending.reasonCode)
            && IsCanonicalRequired(pending.nodeId)
            && IsCanonicalRequired(pending.destinationId)
            && IsCanonicalRequired(pending.itemId)
            && pending.quantity == 1
            && IsNonNegativeFinite(pending.fuelSecondsBefore)
            && IsNonNegativeFinite(pending.fuelSecondsAfter)
            && pending.fuelSecondsAfter > pending.fuelSecondsBefore
            && pending.sourceStackIds != null
            && pending.sourceStackIds.All(IsCanonicalRequired)
            && pending.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                == pending.sourceStackIds.Count
            && IsOrdinallySorted(pending.sourceStackIds);
        if (!common)
        {
            return false;
        }

        if (phase == PowerFuelCommitPhase.IntentRecorded)
        {
            return MathfApproximately(node.fuelSeconds, pending.fuelSecondsBefore)
                && pending.sourceStackIds.Count == 0
                && pending.inputMassGrams == 0L
                && string.IsNullOrEmpty(pending.commitId);
        }

        return node.fuelSeconds >= 0f
            && node.fuelSeconds <= pending.fuelSecondsAfter + 0.0001f
            && pending.sourceStackIds.Count > 0
            && pending.inputMassGrams > 0L
            && IsCanonicalRequired(pending.commitId);
    }

    private static bool MathfApproximately(float left, float right) =>
        Math.Abs(left - right) <= 0.0001f;

    private static bool IsOrdinallySorted(IReadOnlyList<string> values)
    {
        for (int index = 1; index < values.Count; index++)
        {
            if (string.CompareOrdinal(values[index - 1], values[index]) > 0)
            {
                return false;
            }
        }

        return true;
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
        HashSet<string> manualOperations = NewIds();
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
                || node.nextImmediateManualWaterOperationSequence < 1
                || node.nextContainerFeedOperationSequence < 1
                || !Enum.IsDefined(typeof(WaterContainerTransferMode), node.transferMode)
                || !IsNonNegativeFinite(node.transferWork)
                || !ValidateContainerFeed(node))
            {
                report.AddError(
                    $"Fluid node {node.buildingInstanceId} has invalid state.");
            }

            if (node.pendingContainerFeed != null
                && (ContainerWaterFeedCommitPhase)
                    node.pendingContainerFeed.phase
                    != ContainerWaterFeedCommitPhase.None
                && !manualOperations.Add(
                    node.pendingContainerFeed.operationId))
            {
                report.AddError(
                    $"Fluid node {node.buildingInstanceId} reuses a physical operation ID.");
            }

            int immediateCount = 0;
            foreach (ManualWaterTransferSaveData pending in
                     node.pendingManualWaterTransfers
                     ?? new List<ManualWaterTransferSaveData>())
            {
                bool valid = pending != null
                    && IsCanonicalRequired(pending.operationId)
                    && IsCanonicalRequired(pending.destinationId)
                    && (pending.immediateConsumption
                        ? pending.operationSequence
                                == node.nextImmediateManualWaterOperationSequence
                            && string.Equals(
                                pending.operationId,
                                FluidPhysicalOperationIdentity
                                    .FormatImmediateManualWaterOperationId(
                                        node.buildingInstanceId,
                                        pending.operationSequence),
                                StringComparison.Ordinal)
                        : pending.operationSequence == 0)
                    && IsNonNegativeFinite(pending.requestedWaterUnits)
                    && pending.transferredWaterUnits >= 0
                    && pending.inputMassGrams >= 0L
                    && manualOperations.Add(pending.operationId)
                    && (pending.transferredWaterUnits == 0
                        ? pending.physicalCommitId.Length == 0
                            && pending.requestFingerprint.Length == 0
                            && pending.inputMassGrams == 0L
                            && (pending.sourceStackIds?.Count ?? 0) == 0
                        : IsCanonicalRequired(pending.physicalCommitId)
                            && IsCanonicalRequired(pending.requestFingerprint)
                            && pending.inputMassGrams > 0L
                            && pending.sourceStackIds != null
                            && pending.sourceStackIds.Count > 0
                            && pending.sourceStackIds.All(IsCanonicalRequired)
                            && pending.sourceStackIds.Distinct(
                                 StringComparer.Ordinal).Count()
                                == pending.sourceStackIds.Count
                            && IsOrdinallySorted(pending.sourceStackIds));
                if (!valid)
                {
                    report.AddError(
                        $"Fluid node {node.buildingInstanceId} has invalid pending manual-water transfer.");
                }
                if (pending?.immediateConsumption == true)
                {
                    immediateCount++;
                }
            }
            if (immediateCount > 1)
            {
                report.AddError(
                    $"Fluid node {node.buildingInstanceId} has multiple immediate manual-water owners.");
            }
        }
    }

    private static bool ValidateContainerFeed(FluidNodeSaveData node)
    {
        ContainerWaterFeedCommitSaveData pending = node.pendingContainerFeed;
        if (pending == null
            || !Enum.IsDefined(
                typeof(ContainerWaterFeedCommitPhase),
                pending.phase))
        {
            return false;
        }
        ContainerWaterFeedCommitPhase phase =
            (ContainerWaterFeedCommitPhase)pending.phase;
        if (phase == ContainerWaterFeedCommitPhase.None)
        {
            return pending.operationSequence == 0
                && pending.quantity == 0
                && pending.waterAmount == 0f
                && pending.inputMassGrams == 0L
                && string.IsNullOrEmpty(pending.operationId)
                && string.IsNullOrEmpty(pending.reasonCode)
                && string.IsNullOrEmpty(pending.requestFingerprint)
                && string.IsNullOrEmpty(pending.physicalCommitId)
                && string.IsNullOrEmpty(pending.nodeId)
                && string.IsNullOrEmpty(pending.networkId)
                && string.IsNullOrEmpty(pending.destinationId)
                && string.IsNullOrEmpty(pending.itemId)
                && (pending.sourceStackIds?.Count ?? 0) == 0;
        }

        return pending.operationSequence
                == node.nextContainerFeedOperationSequence
            && string.Equals(
                pending.operationId,
                FluidPhysicalOperationIdentity.FormatContainerFeedOperationId(
                    node.buildingInstanceId,
                    pending.operationSequence),
                StringComparison.Ordinal)
            && string.Equals(
                pending.reasonCode,
                FluidPhysicalOperationIdentity.ContainerFeedReasonCode,
                StringComparison.Ordinal)
            && string.Equals(
                pending.nodeId,
                node.buildingInstanceId,
                StringComparison.Ordinal)
            && IsCanonicalRequired(pending.networkId)
            && string.Equals(
                pending.destinationId,
                $"plumbing:water-transfer:{node.buildingInstanceId}",
                StringComparison.Ordinal)
            && string.Equals(
                pending.itemId,
                "resource:clean-water",
                StringComparison.Ordinal)
            && pending.quantity > 0
            && IsNonNegativeFinite(pending.waterAmount)
            && pending.waterAmount > 0f
            && IsCanonicalRequired(pending.requestFingerprint)
            && IsCanonicalRequired(pending.physicalCommitId)
            && pending.inputMassGrams > 0L
            && pending.sourceStackIds != null
            && pending.sourceStackIds.Count > 0
            && pending.sourceStackIds.All(IsCanonicalRequired)
            && pending.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                == pending.sourceStackIds.Count
            && IsOrdinallySorted(pending.sourceStackIds);
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

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
        HashSet<string> payloadStackIds = NewIds();
        foreach (ConveyorPayloadSaveData payload in data.payloads
                     ?? new List<ConveyorPayloadSaveData>())
        {
            if (!ValidateId(payload?.payloadId, payloadIds, "conveyor payload", report))
            {
                continue;
            }

            if (!IsCanonicalPersistentId(
                    payload.itemStackId,
                    new ItemStackId(payload.itemStackId).IsValid)
                || !payloadStackIds.Add(payload.itemStackId)
                || !IsCanonicalPersistentId(
                    payload.segmentBuildingInstanceId,
                    new BuildingInstanceId(
                        payload.segmentBuildingInstanceId).IsValid)
                || !IsCanonicalOptionalPersistentId(
                    payload.previousBuildingInstanceId,
                    value => new BuildingInstanceId(value).IsValid)
                || !IsCanonicalOptionalText(payload.destinationId)
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
        if (!IsCanonicalRequiredText(value))
        {
            report.AddError(
                $"Industrial payload contains a blank or non-canonical {label} ID.");
            return false;
        }

        if (!ids.Add(value))
        {
            report.AddError($"Industrial payload contains duplicate {label} {value}.");
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
        if (!IsCanonicalPersistentId(
                value,
                new BuildingInstanceId(value).IsValid))
        {
            report.AddError(
                $"Industrial payload contains an invalid {label} building ID.");
            return false;
        }

        return ValidateId(value, ids, label, report);
    }

    private static bool IsCanonicalPersistentId(
        string value,
        bool isValid) =>
        isValid && IsCanonicalRequiredText(value);

    private static bool IsCanonicalOptionalPersistentId(
        string value,
        Func<string, bool> isValid) =>
        IsCanonicalOptionalText(value)
        && (value.Length == 0 || isValid(value));

    private static bool IsCanonicalRequiredText(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalOptionalText(string value) =>
        value != null
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsNonNegativeFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool IsRangeFinite(float value, float minimum, float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;
}
