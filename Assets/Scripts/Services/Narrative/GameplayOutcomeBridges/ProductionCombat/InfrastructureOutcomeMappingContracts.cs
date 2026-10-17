using System;
using System.Linq;

public enum InfrastructureCommandOutcomeKind
{
    PowerConnectionChanged = 1,
    PowerPriorityChanged = 2,
    PowerBreakerReset = 3,
    FluidBlockageCleared = 4,
    WaterTransferModeChanged = 5,
    FluidLeakRepaired = 6,
    ConveyorNodeEnabledChanged = 7,
    ConveyorDestinationChanged = 8,
    ConveyorOverflowPolicyChanged = 9,
    ConveyorFilterChanged = 10,
    ConveyorOverflowApproved = 11,
    AutomationModeChanged = 12,
    AutomationMaintained = 13,
    AutomationMaintenanceRequiredEntered = 14,
    PowerFuelConsumed = 15,
    PowerBreakerTripped = 16,
    WastewaterBatchCompleted = 17,
    FluidLeakEpisode = 18,
    WastewaterBackflow = 19,
    PipeFreezeLatched = 20,
    PipeFreezeRecovered = 21
}

public static class InfrastructureOutcomeKindRules
{
    public static bool IsAutonomous(
        InfrastructureCommandOutcomeKind kind) => kind is
        InfrastructureCommandOutcomeKind.PowerFuelConsumed
        or InfrastructureCommandOutcomeKind.PowerBreakerTripped
        or InfrastructureCommandOutcomeKind.WastewaterBatchCompleted
        or InfrastructureCommandOutcomeKind.FluidLeakEpisode
        or InfrastructureCommandOutcomeKind.WastewaterBackflow
        or InfrastructureCommandOutcomeKind.PipeFreezeLatched
        or InfrastructureCommandOutcomeKind.PipeFreezeRecovered;
}

public readonly struct InfrastructureCommandOutcomeSource
{
    private const int MaximumSnapshotLength = 4096;

    public InfrastructureCommandOutcomeSource(
        InfrastructureCommandOutcomeKind kind,
        string targetId,
        string facilityId,
        int facilityX,
        int facilityY,
        string beforeValue,
        string afterValue)
    {
        if (!Enum.IsDefined(typeof(InfrastructureCommandOutcomeKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        TargetId = RequireStableId(targetId, nameof(targetId));
        FacilityId = RequireStableId(facilityId, nameof(facilityId));
        FacilityX = facilityX;
        FacilityY = facilityY;
        BeforeValue = RequireText(beforeValue, nameof(beforeValue));
        AfterValue = RequireText(afterValue, nameof(afterValue));
    }

    public InfrastructureCommandOutcomeKind Kind { get; }
    public string TargetId { get; }
    public string FacilityId { get; }
    public int FacilityX { get; }
    public int FacilityY { get; }
    public string BeforeValue { get; }
    public string AfterValue { get; }

    private static string RequireStableId(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0
            || normalized.Length > 256
            || !string.Equals(value, normalized, StringComparison.Ordinal)
            || normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "A canonical infrastructure command identity is required.",
                parameterName);
        }
        return normalized;
    }

    private static string RequireText(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException(
                "A non-empty infrastructure command snapshot is required.",
                parameterName);
        if (normalized.Length > MaximumSnapshotLength)
            throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
