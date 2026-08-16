using System;
using System.Collections.Generic;

public enum SettlementThreatAlertLevel
{
    Green = 0,
    Amber = 1,
    Red = 2
}

public enum EmergencyReserveCoverageBand
{
    Surplus = 0,
    Adequate = 1,
    Vulnerable = 2,
    CollapseRisk = 3
}

public readonly struct SettlementIncidentSignal
{
    public SettlementIncidentSignal(
        string incidentId,
        SettlementThreatAlertLevel requiredLevel,
        long revision,
        string sourceId,
        string diagnostic)
    {
        IncidentId = incidentId?.Trim() ?? string.Empty;
        RequiredLevel = requiredLevel;
        Revision = revision;
        SourceId = sourceId?.Trim() ?? string.Empty;
        Diagnostic = diagnostic?.Trim() ?? string.Empty;
    }

    public string IncidentId { get; }
    public SettlementThreatAlertLevel RequiredLevel { get; }
    public long Revision { get; }
    public string SourceId { get; }
    public string Diagnostic { get; }
}

public readonly struct SettlementSuspendedWorkSnapshot
{
    public SettlementSuspendedWorkSnapshot(
        string characterId,
        WorkTypeId workTypeId,
        string targetBuildingId,
        long alertEpochId,
        long suspendedAtAbsoluteHour,
        bool progressExternallyPersisted,
        float inlineCompletedWork = 0f,
        float inlineRequiredWork = 0f)
    {
        CharacterId = characterId?.Trim() ?? string.Empty;
        WorkTypeId = workTypeId;
        TargetBuildingId = targetBuildingId?.Trim() ?? string.Empty;
        AlertEpochId = alertEpochId;
        SuspendedAtAbsoluteHour = suspendedAtAbsoluteHour;
        ProgressExternallyPersisted = progressExternallyPersisted;
        InlineCompletedWork = inlineCompletedWork;
        InlineRequiredWork = inlineRequiredWork;
    }

    public string CharacterId { get; }
    public WorkTypeId WorkTypeId { get; }
    public string TargetBuildingId { get; }
    public long AlertEpochId { get; }
    public long SuspendedAtAbsoluteHour { get; }
    public bool ProgressExternallyPersisted { get; }
    public float InlineCompletedWork { get; }
    public float InlineRequiredWork { get; }
    public bool HasInlineProgress => InlineRequiredWork > 0f
        && InlineCompletedWork >= 0f
        && InlineCompletedWork < InlineRequiredWork
        && !float.IsNaN(InlineCompletedWork)
        && !float.IsInfinity(InlineCompletedWork)
        && !float.IsNaN(InlineRequiredWork)
        && !float.IsInfinity(InlineRequiredWork);
}

public readonly struct SettlementAlertSnapshot
{
    public SettlementAlertSnapshot(
        SettlementThreatAlertLevel desiredLevel,
        SettlementThreatAlertLevel committedLevel,
        long alertEpochId,
        long levelEnteredAbsoluteHour,
        long downgradeStableSinceAbsoluteHour,
        int amberIncidentCount,
        int redIncidentCount,
        IReadOnlyList<string> activeIncidentIds,
        IReadOnlyList<SettlementSuspendedWorkSnapshot> suspendedWork,
        EmergencyReserveCoverageBand reserveCoverageBand,
        float reserveCoverage,
        long coverageStableSinceAbsoluteHour)
    {
        DesiredLevel = desiredLevel;
        CommittedLevel = committedLevel;
        AlertEpochId = alertEpochId;
        LevelEnteredAbsoluteHour = levelEnteredAbsoluteHour;
        DowngradeStableSinceAbsoluteHour = downgradeStableSinceAbsoluteHour;
        AmberIncidentCount = amberIncidentCount;
        RedIncidentCount = redIncidentCount;
        ActiveIncidentIds = activeIncidentIds ?? Array.Empty<string>();
        SuspendedWork = suspendedWork
            ?? Array.Empty<SettlementSuspendedWorkSnapshot>();
        ReserveCoverageBand = reserveCoverageBand;
        ReserveCoverage = reserveCoverage;
        CoverageStableSinceAbsoluteHour = coverageStableSinceAbsoluteHour;
    }

    public SettlementThreatAlertLevel DesiredLevel { get; }
    public SettlementThreatAlertLevel CommittedLevel { get; }
    public long AlertEpochId { get; }
    public long LevelEnteredAbsoluteHour { get; }
    public long DowngradeStableSinceAbsoluteHour { get; }
    public int AmberIncidentCount { get; }
    public int RedIncidentCount { get; }
    public IReadOnlyList<string> ActiveIncidentIds { get; }
    public IReadOnlyList<SettlementSuspendedWorkSnapshot> SuspendedWork { get; }
    public EmergencyReserveCoverageBand ReserveCoverageBand { get; }
    public float ReserveCoverage { get; }
    public long CoverageStableSinceAbsoluteHour { get; }
}

public readonly struct SettlementCommittedAlertChangedEvent
{
    public SettlementCommittedAlertChangedEvent(
        SettlementThreatAlertLevel previous,
        SettlementThreatAlertLevel current,
        long epochId,
        long absoluteHour)
    {
        Previous = previous;
        Current = current;
        EpochId = epochId;
        AbsoluteHour = absoluteHour;
    }

    public SettlementThreatAlertLevel Previous { get; }
    public SettlementThreatAlertLevel Current { get; }
    public long EpochId { get; }
    public long AbsoluteHour { get; }
}

public readonly struct SettlementActiveIncidentsChangedEvent
{
    public SettlementActiveIncidentsChangedEvent(
        long epochId,
        SettlementThreatAlertLevel desiredLevel,
        SettlementThreatAlertLevel committedLevel,
        int activeIncidentCount)
    {
        EpochId = epochId;
        DesiredLevel = desiredLevel;
        CommittedLevel = committedLevel;
        ActiveIncidentCount = activeIncidentCount;
    }

    public long EpochId { get; }
    public SettlementThreatAlertLevel DesiredLevel { get; }
    public SettlementThreatAlertLevel CommittedLevel { get; }
    public int ActiveIncidentCount { get; }
}

public interface ISettlementAlertService
{
    SettlementAlertSnapshot Capture();
    EmergencyAccountingResult PublishIncidentSignal(SettlementIncidentSignal signal);
    EmergencyAccountingResult ResolveIncident(string incidentId, long revision);
    long GetNextIncidentRevision(string incidentId);
    EmergencyAccountingResult UpdateReserveCoverage(long currentMilliWu, long targetMilliWu);
    bool TryClaimContextTransition(string characterId, long epochId, bool toEmergency);
    EmergencyAccountingResult RecordSuspendedWork(
        SettlementSuspendedWorkSnapshot suspendedWork);
    bool TryGetSuspendedWork(
        string characterId,
        out SettlementSuspendedWorkSnapshot suspendedWork);
    EmergencyAccountingResult MarkSuspendedWorkResumed(
        string characterId,
        long epochId);
    EmergencyAccountingResult MarkSuspendedWorkAbandoned(
        string characterId,
        long epochId,
        string reasonCode);
}

public interface ISettlementAlertPersistence
{
    DungeonStory.Infrastructure.SettlementThreatAlertSaveData CaptureAlertSaveData();
    void RestoreAlertSaveData(
        DungeonStory.Infrastructure.SettlementThreatAlertSaveData saveData);
}
