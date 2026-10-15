using System;

public enum MilestonePressureEffectKind
{
    DefenseObligation = 1,
    FarmingWorkDelay = 2,
    InheritanceWorkDelay = 3,
    RepairWorkDelay = 4,
    TemporalMaintenanceAcceleration = 5,
    InvasionScheduled = 6
}

/// <summary>
/// Frozen gameplay effect emitted only after the corresponding milestone
/// pressure command has completed. Presentation is an optional consumer.
/// </summary>
public sealed class MilestonePressureAppliedFact
{
    public MilestonePressureAppliedFact(
        MilestonePressureEffectKind kind,
        int absoluteDay,
        string pressureId,
        string sourceId,
        int affectedCount = 0,
        int obligationDelta = 0,
        string workScopeId = "",
        int workDelayDays = 0,
        int maintenanceLeadDays = 0,
        string invasionOperationId = "",
        float invasionThreat = 0f)
    {
        if (!Enum.IsDefined(typeof(MilestonePressureEffectKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (absoluteDay <= 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        if (affectedCount < 0)
            throw new ArgumentOutOfRangeException(nameof(affectedCount));
        if (obligationDelta < 0)
            throw new ArgumentOutOfRangeException(nameof(obligationDelta));
        if (workDelayDays < 0)
            throw new ArgumentOutOfRangeException(nameof(workDelayDays));
        if (maintenanceLeadDays < 0)
            throw new ArgumentOutOfRangeException(nameof(maintenanceLeadDays));
        if (float.IsNaN(invasionThreat) || float.IsInfinity(invasionThreat)
            || invasionThreat < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(invasionThreat));
        }

        Kind = kind;
        AbsoluteDay = absoluteDay;
        PressureId = RequireCanonical(pressureId, nameof(pressureId));
        SourceId = RequireCanonical(sourceId, nameof(sourceId));
        AffectedCount = affectedCount;
        ObligationDelta = obligationDelta;
        WorkScopeId = OptionalCanonical(workScopeId, nameof(workScopeId));
        WorkDelayDays = workDelayDays;
        MaintenanceLeadDays = maintenanceLeadDays;
        InvasionOperationId = OptionalCanonical(
            invasionOperationId,
            nameof(invasionOperationId));
        InvasionThreat = invasionThreat;
    }

    public MilestonePressureEffectKind Kind { get; }
    public int AbsoluteDay { get; }
    public string PressureId { get; }
    public string SourceId { get; }
    public int AffectedCount { get; }
    public int ObligationDelta { get; }
    public string WorkScopeId { get; }
    public int WorkDelayDays { get; }
    public int MaintenanceLeadDays { get; }
    public string InvasionOperationId { get; }
    public float InvasionThreat { get; }

    private static string RequireCanonical(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || !string.Equals(canonical, value, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Milestone pressure fact identifiers must be canonical.",
                name);
        }

        return canonical;
    }

    private static string OptionalCanonical(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (!string.Equals(canonical, value ?? string.Empty, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Milestone pressure fact values must be canonical.",
                name);
        }

        return canonical;
    }
}
