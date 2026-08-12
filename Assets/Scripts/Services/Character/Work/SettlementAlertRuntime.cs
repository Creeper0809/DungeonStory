using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using VContainer.Unity;

public sealed class SettlementAlertRuntime :
    ISettlementAlertService,
    ISettlementAlertPersistence,
    ITickable
{
    public const long DowngradeStabilityHours = 2L;
    public const long CoverageStabilityHours = 2L;

    private readonly IGameCalendar calendar;
    private readonly IGameEventBus events;
    private readonly IEmergencyWorkAccountingReconciler accountingReconciler;
    private readonly IEmergencyWorkAccountingService accounting;
    private readonly Dictionary<string, IncidentState> incidents =
        new Dictionary<string, IncidentState>(StringComparer.Ordinal);
    private readonly Dictionary<string, ContextTransitionState> contextTransitions =
        new Dictionary<string, ContextTransitionState>(StringComparer.Ordinal);
    private readonly Dictionary<string, SettlementSuspendedWorkSnapshot> suspendedWork =
        new Dictionary<string, SettlementSuspendedWorkSnapshot>(StringComparer.Ordinal);

    private SettlementThreatAlertLevel desiredLevel;
    private SettlementThreatAlertLevel committedLevel;
    private long alertEpochId;
    private long levelEnteredAbsoluteHour;
    private long downgradeStableSinceAbsoluteHour = -1L;
    private int amberIncidentCount;
    private int redIncidentCount;
    private int lastQualifiedRedAuditDay = -1;

    private EmergencyReserveCoverageBand coverageBand =
        EmergencyReserveCoverageBand.Adequate;
    private EmergencyReserveCoverageBand desiredCoverageBand =
        EmergencyReserveCoverageBand.Adequate;
    private float reserveCoverage = 1f;
    private long coverageStableSinceAbsoluteHour = -1L;
    private bool snapshotDirty = true;
    private SettlementAlertSnapshot cachedSnapshot;

    public SettlementAlertRuntime(
        IGameCalendar calendar,
        IGameEventBus events,
        IEmergencyWorkAccountingReconciler accountingReconciler,
        IEmergencyWorkAccountingService accounting)
    {
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.accountingReconciler = accountingReconciler
            ?? throw new ArgumentNullException(nameof(accountingReconciler));
        this.accounting = accounting ?? throw new ArgumentNullException(nameof(accounting));
        levelEnteredAbsoluteHour = 0L;
    }

    public void Tick()
    {
        long now = calendar.AbsoluteHour;
        AdvanceThreatDowngrade(now);
        AdvanceCoverageUpgrade(now);
    }

    public SettlementAlertSnapshot Capture()
    {
        if (!snapshotDirty)
        {
            return cachedSnapshot;
        }

        List<string> activeIncidentIds = new List<string>(amberIncidentCount + redIncidentCount);
        foreach (KeyValuePair<string, IncidentState> pair in incidents)
        {
            if (pair.Value.Active)
            {
                activeIncidentIds.Add(pair.Key);
            }
        }
        activeIncidentIds.Sort(StringComparer.Ordinal);
        List<SettlementSuspendedWorkSnapshot> suspended =
            new List<SettlementSuspendedWorkSnapshot>(suspendedWork.Values);
        suspended.Sort((left, right) =>
            string.CompareOrdinal(left.CharacterId, right.CharacterId));

        cachedSnapshot = new SettlementAlertSnapshot(
            desiredLevel,
            committedLevel,
            alertEpochId,
            levelEnteredAbsoluteHour,
            downgradeStableSinceAbsoluteHour,
            amberIncidentCount,
            redIncidentCount,
            activeIncidentIds,
            suspended,
            coverageBand,
            reserveCoverage,
            coverageStableSinceAbsoluteHour);
        snapshotDirty = false;
        return cachedSnapshot;
    }

    public DungeonStory.Infrastructure.SettlementThreatAlertSaveData CaptureAlertSaveData()
    {
        DungeonStory.Infrastructure.SettlementThreatAlertSaveData result =
            new DungeonStory.Infrastructure.SettlementThreatAlertSaveData
            {
                committedLevel = (int)committedLevel,
                desiredLevel = (int)desiredLevel,
                alertEpochId = alertEpochId,
                levelEnteredAbsoluteHour = levelEnteredAbsoluteHour,
                downgradeStableSinceAbsoluteHour = downgradeStableSinceAbsoluteHour,
                reserveCoverageBand = (int)coverageBand,
                reserveCoverage = reserveCoverage,
                coverageStableSinceAbsoluteHour = coverageStableSinceAbsoluteHour
            };
        foreach (KeyValuePair<string, IncidentState> pair in incidents)
        {
            result.incidents.Add(new DungeonStory.Infrastructure.SettlementIncidentSaveData
            {
                incidentId = pair.Key,
                active = pair.Value.Active,
                requiredLevel = (int)pair.Value.Level,
                revision = pair.Value.Revision,
                sourceId = pair.Value.SourceId,
                diagnostic = pair.Value.Diagnostic
            });
        }
        result.incidents.Sort((left, right) =>
            string.CompareOrdinal(left.incidentId, right.incidentId));
        foreach (SettlementSuspendedWorkSnapshot value in suspendedWork.Values)
        {
            result.suspendedWork.Add(
                new DungeonStory.Infrastructure.SettlementSuspendedWorkSaveData
                {
                    characterId = value.CharacterId,
                    workTypeId = value.WorkTypeId.Value,
                    targetBuildingId = value.TargetBuildingId,
                    alertEpochId = value.AlertEpochId,
                    suspendedAtAbsoluteHour = value.SuspendedAtAbsoluteHour,
                    progressExternallyPersisted = value.ProgressExternallyPersisted
                });
        }
        result.suspendedWork.Sort((left, right) =>
            string.CompareOrdinal(left.characterId, right.characterId));
        return result;
    }

    public void RestoreAlertSaveData(
        DungeonStory.Infrastructure.SettlementThreatAlertSaveData saveData)
    {
        if (saveData == null
            || saveData.incidents == null
            || saveData.suspendedWork == null)
        {
            throw new ArgumentNullException(nameof(saveData));
        }

        incidents.Clear();
        contextTransitions.Clear();
        suspendedWork.Clear();
        amberIncidentCount = 0;
        redIncidentCount = 0;
        for (int index = 0; index < saveData.incidents.Count; index++)
        {
            DungeonStory.Infrastructure.SettlementIncidentSaveData source =
                saveData.incidents[index];
            IncidentState restored = new IncidentState(
                source.active,
                (SettlementThreatAlertLevel)source.requiredLevel,
                source.revision,
                source.sourceId,
                source.diagnostic);
            incidents.Add(source.incidentId, restored);
            if (restored.Active)
            {
                IncrementIncidentCount(restored.Level);
            }
        }

        committedLevel = (SettlementThreatAlertLevel)saveData.committedLevel;
        desiredLevel = (SettlementThreatAlertLevel)saveData.desiredLevel;
        alertEpochId = saveData.alertEpochId;
        levelEnteredAbsoluteHour = saveData.levelEnteredAbsoluteHour;
        downgradeStableSinceAbsoluteHour = saveData.downgradeStableSinceAbsoluteHour;
        coverageBand = (EmergencyReserveCoverageBand)saveData.reserveCoverageBand;
        desiredCoverageBand = ResolveCoverageBand(saveData.reserveCoverage);
        reserveCoverage = saveData.reserveCoverage;
        coverageStableSinceAbsoluteHour = saveData.coverageStableSinceAbsoluteHour;
        for (int index = 0; index < saveData.suspendedWork.Count; index++)
        {
            DungeonStory.Infrastructure.SettlementSuspendedWorkSaveData source =
                saveData.suspendedWork[index];
            if (source == null)
            {
                throw new InvalidOperationException(
                    "Restored settlement alert contains null suspended work.");
            }
            WorkTypeId workTypeId = new WorkTypeId(source.workTypeId);
            if (!WorkTypeCatalog.TryGet(workTypeId, out _))
            {
                throw new InvalidOperationException(
                    $"Restored suspended work references unknown work type '{source.workTypeId}'.");
            }
            suspendedWork.Add(
                source.characterId,
                new SettlementSuspendedWorkSnapshot(
                    source.characterId,
                    workTypeId,
                    source.targetBuildingId,
                    source.alertEpochId,
                    source.suspendedAtAbsoluteHour,
                    source.progressExternallyPersisted));
        }
        TouchSnapshot();

        SettlementThreatAlertLevel calculatedDesired = redIncidentCount > 0
            ? SettlementThreatAlertLevel.Red
            : amberIncidentCount > 0
                ? SettlementThreatAlertLevel.Amber
                : SettlementThreatAlertLevel.Green;
        if (calculatedDesired != desiredLevel || desiredLevel > committedLevel)
        {
            throw new InvalidOperationException(
                "Restored settlement alert levels do not match active incident ground truth.");
        }
    }

    public EmergencyAccountingResult PublishIncidentSignal(
        SettlementIncidentSignal signal)
    {
        if (string.IsNullOrWhiteSpace(signal.IncidentId)
            || signal.Revision < 0L
            || signal.RequiredLevel == SettlementThreatAlertLevel.Green)
        {
            return EmergencyAccountingResult.Fail(
                "SettlementIncidentSignalInvalid",
                "An active incident requires an ID, non-negative revision and Amber or Red level.");
        }

        if (incidents.TryGetValue(signal.IncidentId, out IncidentState previous))
        {
            if (signal.Revision <= previous.Revision)
            {
                return EmergencyAccountingResult.Ok("duplicate-incident-signal-ignored");
            }

            if (previous.Active)
            {
                DecrementIncidentCount(previous.Level);
            }
        }

        incidents[signal.IncidentId] = new IncidentState(
            true,
            signal.RequiredLevel,
            signal.Revision,
            signal.SourceId,
            signal.Diagnostic);
        IncrementIncidentCount(signal.RequiredLevel);
        TouchSnapshot();
        ReevaluateDesiredLevel();
        PublishActiveIncidentsChanged();
        return EmergencyAccountingResult.Ok("incident-signal-published");
    }

    public EmergencyAccountingResult ResolveIncident(string incidentId, long revision)
    {
        string normalized = incidentId?.Trim() ?? string.Empty;
        if (!incidents.TryGetValue(normalized, out IncidentState previous))
        {
            return EmergencyAccountingResult.Fail(
                "SettlementIncidentMissing",
                $"Incident '{normalized}' is not registered.");
        }

        if (revision <= previous.Revision)
        {
            return EmergencyAccountingResult.Ok("duplicate-incident-resolution-ignored");
        }

        if (previous.Active)
        {
            DecrementIncidentCount(previous.Level);
        }
        incidents[normalized] = new IncidentState(
            false,
            previous.Level,
            revision,
            previous.SourceId,
            previous.Diagnostic);
        TouchSnapshot();
        ReevaluateDesiredLevel();
        PublishActiveIncidentsChanged();
        return EmergencyAccountingResult.Ok("incident-resolved");
    }

    public long GetNextIncidentRevision(string incidentId)
    {
        string normalized = incidentId?.Trim() ?? string.Empty;
        return incidents.TryGetValue(normalized, out IncidentState state)
            ? checked(state.Revision + 1L)
            : 0L;
    }

    public EmergencyAccountingResult UpdateReserveCoverage(
        long currentMilliWu,
        long targetMilliWu)
    {
        if (currentMilliWu < 0L || targetMilliWu <= 0L)
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyReserveCoverageInvalid",
                "Reserve current WU must be non-negative and target WU must be positive.");
        }

        reserveCoverage = currentMilliWu / (float)targetMilliWu;
        desiredCoverageBand = ResolveCoverageBand(reserveCoverage);
        if (desiredCoverageBand > coverageBand)
        {
            coverageBand = desiredCoverageBand;
            coverageStableSinceAbsoluteHour = -1L;
        }
        else if (desiredCoverageBand < coverageBand
            && MeetsCoverageRecoveryThreshold(coverageBand, reserveCoverage))
        {
            if (coverageStableSinceAbsoluteHour < 0L)
            {
                coverageStableSinceAbsoluteHour = calendar.AbsoluteHour;
            }
        }
        else
        {
            coverageStableSinceAbsoluteHour = -1L;
        }

        TouchSnapshot();

        return EmergencyAccountingResult.Ok("reserve-coverage-updated");
    }

    public bool TryClaimContextTransition(
        string characterId,
        long epochId,
        bool toEmergency)
    {
        string normalized = characterId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized) || epochId != alertEpochId)
        {
            return false;
        }

        contextTransitions.TryGetValue(normalized, out ContextTransitionState previous);
        if (previous.EpochId == epochId
            && (toEmergency ? previous.ToEmergency : previous.ToPeace))
        {
            return false;
        }

        contextTransitions[normalized] = new ContextTransitionState(
            epochId,
            previous.EpochId == epochId && previous.ToEmergency || toEmergency,
            previous.EpochId == epochId && previous.ToPeace || !toEmergency);
        return true;
    }

    public EmergencyAccountingResult RecordSuspendedWork(
        SettlementSuspendedWorkSnapshot value)
    {
        if (string.IsNullOrWhiteSpace(value.CharacterId)
            || !value.WorkTypeId.IsValid
            || string.IsNullOrWhiteSpace(value.TargetBuildingId)
            || value.AlertEpochId != alertEpochId
            || value.SuspendedAtAbsoluteHour < 0L
            || !value.ProgressExternallyPersisted)
        {
            return EmergencyAccountingResult.Fail(
                "SettlementSuspendedWorkInvalid",
                "Suspended work requires a character, work type, target, current epoch and externally persisted progress.");
        }

        suspendedWork[value.CharacterId] = value;
        TouchSnapshot();
        return EmergencyAccountingResult.Ok("suspended-work-recorded");
    }

    public bool TryGetSuspendedWork(
        string characterId,
        out SettlementSuspendedWorkSnapshot value)
    {
        return suspendedWork.TryGetValue(
            characterId?.Trim() ?? string.Empty,
            out value);
    }

    public EmergencyAccountingResult MarkSuspendedWorkResumed(
        string characterId,
        long epochId)
    {
        string normalized = characterId?.Trim() ?? string.Empty;
        if (!suspendedWork.TryGetValue(
                normalized,
                out SettlementSuspendedWorkSnapshot value))
        {
            return EmergencyAccountingResult.Ok("suspended-work-already-absent");
        }
        if (value.AlertEpochId != epochId)
        {
            return EmergencyAccountingResult.Fail(
                "SettlementSuspendedWorkEpochMismatch",
                $"Suspended work for '{normalized}' belongs to epoch {value.AlertEpochId}, not {epochId}.");
        }

        suspendedWork.Remove(normalized);
        TouchSnapshot();
        return EmergencyAccountingResult.Ok("suspended-work-resumed");
    }

    private void ReevaluateDesiredLevel()
    {
        desiredLevel = redIncidentCount > 0
            ? SettlementThreatAlertLevel.Red
            : amberIncidentCount > 0
                ? SettlementThreatAlertLevel.Amber
                : SettlementThreatAlertLevel.Green;
        TouchSnapshot();

        if (desiredLevel > committedLevel)
        {
            CommitEscalation(desiredLevel);
        }
        else if (desiredLevel < committedLevel)
        {
            downgradeStableSinceAbsoluteHour = calendar.AbsoluteHour;
        }
        else
        {
            downgradeStableSinceAbsoluteHour = -1L;
        }
    }

    private void CommitEscalation(SettlementThreatAlertLevel next)
    {
        if (next == SettlementThreatAlertLevel.Red)
        {
            EmergencyReserveSnapshot accountingSnapshot = accounting.CaptureSnapshot();
            if (!accountingSnapshot.Healthy || lastQualifiedRedAuditDay != calendar.Day)
            {
                EmergencyAccountingReconciliationResult reconciliation =
                    accountingReconciler.Reconcile(
                        EmergencyAccountingReconciliationTrigger.BeforeQualifiedRedEscalation);
                if (!reconciliation.Success)
                {
                    throw new InvalidOperationException(reconciliation.Diagnostic);
                }
                lastQualifiedRedAuditDay = calendar.Day;
            }
        }

        SettlementThreatAlertLevel previous = committedLevel;
        committedLevel = next;
        alertEpochId = checked(alertEpochId + 1L);
        if (suspendedWork.Count > 0)
        {
            List<string> suspendedCharacters =
                new List<string>(suspendedWork.Keys);
            for (int index = 0; index < suspendedCharacters.Count; index++)
            {
                string characterId = suspendedCharacters[index];
                SettlementSuspendedWorkSnapshot previousWork =
                    suspendedWork[characterId];
                suspendedWork[characterId] =
                    new SettlementSuspendedWorkSnapshot(
                        previousWork.CharacterId,
                        previousWork.WorkTypeId,
                        previousWork.TargetBuildingId,
                        alertEpochId,
                        previousWork.SuspendedAtAbsoluteHour,
                        previousWork.ProgressExternallyPersisted);
            }
        }
        levelEnteredAbsoluteHour = calendar.AbsoluteHour;
        downgradeStableSinceAbsoluteHour = -1L;
        contextTransitions.Clear();
        TouchSnapshot();
        events.Publish(new SettlementCommittedAlertChangedEvent(
            previous,
            committedLevel,
            alertEpochId,
            levelEnteredAbsoluteHour));
    }

    private void AdvanceThreatDowngrade(long now)
    {
        if (desiredLevel >= committedLevel)
        {
            if (downgradeStableSinceAbsoluteHour >= 0L)
            {
                downgradeStableSinceAbsoluteHour = -1L;
                TouchSnapshot();
            }
            return;
        }

        if (downgradeStableSinceAbsoluteHour < 0L)
        {
            downgradeStableSinceAbsoluteHour = now;
            TouchSnapshot();
            return;
        }

        if (now - downgradeStableSinceAbsoluteHour < DowngradeStabilityHours)
        {
            return;
        }

        SettlementThreatAlertLevel previous = committedLevel;
        committedLevel = previous == SettlementThreatAlertLevel.Red
            ? SettlementThreatAlertLevel.Amber
            : SettlementThreatAlertLevel.Green;
        levelEnteredAbsoluteHour = now;
        downgradeStableSinceAbsoluteHour = committedLevel > desiredLevel ? now : -1L;
        TouchSnapshot();
        events.Publish(new SettlementCommittedAlertChangedEvent(
            previous,
            committedLevel,
            alertEpochId,
            now));
    }

    private void AdvanceCoverageUpgrade(long now)
    {
        if (desiredCoverageBand >= coverageBand
            || !MeetsCoverageRecoveryThreshold(coverageBand, reserveCoverage))
        {
            if (coverageStableSinceAbsoluteHour >= 0L)
            {
                coverageStableSinceAbsoluteHour = -1L;
                TouchSnapshot();
            }
            return;
        }

        if (coverageStableSinceAbsoluteHour < 0L)
        {
            coverageStableSinceAbsoluteHour = now;
            TouchSnapshot();
            return;
        }

        if (now - coverageStableSinceAbsoluteHour < CoverageStabilityHours)
        {
            return;
        }

        coverageBand = (EmergencyReserveCoverageBand)((int)coverageBand - 1);
        coverageStableSinceAbsoluteHour = coverageBand > desiredCoverageBand ? now : -1L;
        TouchSnapshot();
    }

    private static EmergencyReserveCoverageBand ResolveCoverageBand(float coverage)
    {
        if (coverage < 0.75f) return EmergencyReserveCoverageBand.CollapseRisk;
        if (coverage < 1f) return EmergencyReserveCoverageBand.Vulnerable;
        if (coverage < 1.25f) return EmergencyReserveCoverageBand.Adequate;
        return EmergencyReserveCoverageBand.Surplus;
    }

    private static bool MeetsCoverageRecoveryThreshold(
        EmergencyReserveCoverageBand current,
        float coverage)
    {
        return current switch
        {
            EmergencyReserveCoverageBand.CollapseRisk => coverage >= 0.85f,
            EmergencyReserveCoverageBand.Vulnerable => coverage >= 1.10f,
            EmergencyReserveCoverageBand.Adequate => coverage >= 1.35f,
            _ => false
        };
    }

    private void IncrementIncidentCount(SettlementThreatAlertLevel level)
    {
        if (level == SettlementThreatAlertLevel.Red)
            redIncidentCount = checked(redIncidentCount + 1);
        else if (level == SettlementThreatAlertLevel.Amber)
            amberIncidentCount = checked(amberIncidentCount + 1);
    }

    private void DecrementIncidentCount(SettlementThreatAlertLevel level)
    {
        if (level == SettlementThreatAlertLevel.Red)
            redIncidentCount = Math.Max(0, redIncidentCount - 1);
        else if (level == SettlementThreatAlertLevel.Amber)
            amberIncidentCount = Math.Max(0, amberIncidentCount - 1);
    }

    private void PublishActiveIncidentsChanged()
    {
        events.Publish(new SettlementActiveIncidentsChangedEvent(
            alertEpochId,
            desiredLevel,
            committedLevel,
            checked(amberIncidentCount + redIncidentCount)));
    }

    private void TouchSnapshot() => snapshotDirty = true;

    private readonly struct IncidentState
    {
        public IncidentState(
            bool active,
            SettlementThreatAlertLevel level,
            long revision,
            string sourceId,
            string diagnostic)
        {
            Active = active;
            Level = level;
            Revision = revision;
            SourceId = sourceId;
            Diagnostic = diagnostic;
        }

        public bool Active { get; }
        public SettlementThreatAlertLevel Level { get; }
        public long Revision { get; }
        public string SourceId { get; }
        public string Diagnostic { get; }
    }

    private readonly struct ContextTransitionState
    {
        public ContextTransitionState(long epochId, bool toEmergency, bool toPeace)
        {
            EpochId = epochId;
            ToEmergency = toEmergency;
            ToPeace = toPeace;
        }

        public long EpochId { get; }
        public bool ToEmergency { get; }
        public bool ToPeace { get; }
    }
}
