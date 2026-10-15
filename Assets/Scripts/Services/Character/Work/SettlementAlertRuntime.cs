using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Infrastructure;
using VContainer.Unity;

public sealed class SettlementAlertRuntime :
    ISettlementAlertService,
    ISettlementAlertPersistence,
    IDungeonRestoreTransactionParticipant,
    IDungeonSaveRestoreCompletedHook,
    ITickable
{
    public const long DowngradeStabilityHours = 2L;
    public const long CoverageStabilityHours = 2L;

    private readonly IGameCalendar calendar;
    private readonly IGameEventBus events;
    private readonly IEmergencyWorkAccountingReconciler accountingReconciler;
    private readonly IEmergencyWorkAccountingService accounting;
    private readonly IDomainFactStagingPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly Dictionary<string, EmergencyWorkSuspensionObservationSaveData> suspensionObservations = new(StringComparer.Ordinal);
    private bool publishingSuspension;
    private float? lastSuspensionRetryAt;
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
    private Action restorePreviousState;
    private DungeonStory.Infrastructure.SettlementThreatAlertSaveData restoreCandidate;

    public string ParticipantId => "210.operation.settlement-alert";

    public void BeginRestoreCandidate()
    {
        // Stage the save DTO without changing live authority. Keep transient
        // claims/cache state too: it cannot be reconstructed by replaying a DTO.
        if (restorePreviousState != null)
            throw new InvalidOperationException("Settlement alert restore is already staged.");
        var previousIncidents = new Dictionary<string, IncidentState>(incidents, StringComparer.Ordinal);
        var previousTransitions = new Dictionary<string, ContextTransitionState>(contextTransitions, StringComparer.Ordinal);
        var previousSuspended = new Dictionary<string, SettlementSuspendedWorkSnapshot>(suspendedWork, StringComparer.Ordinal);
        var previousObservations = suspensionObservations.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
        var previousRetryAt = lastSuspensionRetryAt;
        var previousDesired = desiredLevel; var previousCommitted = committedLevel;
        long previousEpoch = alertEpochId, previousEntered = levelEnteredAbsoluteHour;
        long previousDowngrade = downgradeStableSinceAbsoluteHour, previousCoverageSince = coverageStableSinceAbsoluteHour;
        int previousAmber = amberIncidentCount, previousRed = redIncidentCount, previousAuditDay = lastQualifiedRedAuditDay;
        var previousCoverage = coverageBand; var previousDesiredCoverage = desiredCoverageBand;
        float previousReserve = reserveCoverage;
        bool previousDirty = snapshotDirty;
        var previousSnapshot = cachedSnapshot;
        restorePreviousState = () =>
        {
            incidents.Clear();
            foreach (var pair in previousIncidents) incidents.Add(pair.Key, pair.Value);
            contextTransitions.Clear();
            foreach (var pair in previousTransitions) contextTransitions.Add(pair.Key, pair.Value);
            suspendedWork.Clear();
            foreach (var pair in previousSuspended) suspendedWork.Add(pair.Key, pair.Value);
            suspensionObservations.Clear();
            foreach (var pair in previousObservations) suspensionObservations.Add(pair.Key, pair.Value);
            lastSuspensionRetryAt = previousRetryAt;
            desiredLevel = previousDesired; committedLevel = previousCommitted;
            alertEpochId = previousEpoch; levelEnteredAbsoluteHour = previousEntered;
            downgradeStableSinceAbsoluteHour = previousDowngrade; coverageStableSinceAbsoluteHour = previousCoverageSince;
            amberIncidentCount = previousAmber; redIncidentCount = previousRed; lastQualifiedRedAuditDay = previousAuditDay;
            coverageBand = previousCoverage; desiredCoverageBand = previousDesiredCoverage; reserveCoverage = previousReserve;
            snapshotDirty = previousDirty; cachedSnapshot = previousSnapshot;
        };
    }

    public void PublishRestoreCandidate()
    {
        if (restoreCandidate != null) ApplyAlertSaveData(restoreCandidate);
    }

    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();

    public void DiscardRestoreCandidate()
    {
        restorePreviousState?.Invoke();
        CompleteRestoreCandidate();
    }

    public void CompleteRestoreCandidate()
    {
        restorePreviousState = null;
        restoreCandidate = null;
    }

    public SettlementAlertRuntime(
        IGameCalendar calendar,
        IGameEventBus events,
        IEmergencyWorkAccountingReconciler accountingReconciler,
        IEmergencyWorkAccountingService accounting,
        IDomainFactStagingPublisher facts,
        IDomainFactRecoveryPublisher recovery)
    {
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.accountingReconciler = accountingReconciler
            ?? throw new ArgumentNullException(nameof(accountingReconciler));
        this.accounting = accounting ?? throw new ArgumentNullException(nameof(accounting));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        levelEnteredAbsoluteHour = 0L;
    }

    public void Tick()
    {
        float retryTime = calendar.ElapsedSeconds;
        if (!lastSuspensionRetryAt.HasValue || retryTime < lastSuspensionRetryAt.Value
            || retryTime - lastSuspensionRetryAt.Value >= 5f)
        {
            lastSuspensionRetryAt = retryTime;
            try { ResumeSuspensionObservations(); }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
            { UnityEngine.Debug.LogException(exception); }
        }
        long now = calendar.AbsoluteHour;
        AdvanceThreatDowngrade(now);
        AdvanceCoverageUpgrade(now);
    }

    public void OnRestoreCompleted()
    {
        lastSuspensionRetryAt = calendar.ElapsedSeconds;
        try { ResumeSuspensionObservations(); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {
            // The world is already published. Keep the pending observation and
            // retry later; failure here must not report a rolled-back world.
            UnityEngine.Debug.LogException(exception);
        }
    }

    private void ResumeSuspensionObservations()
    {
        if (publishingSuspension) throw new InvalidOperationException("suspension-observation-reentrant");
        bool pending = false;
        foreach (var row in suspensionObservations.Values)
            if (row.pending) { pending = true; break; }
        if (!pending) return;
        publishingSuspension = true;
        try
        {
            foreach (var pair in suspensionObservations.Where(pair => pair.Value.pending).ToArray())
            {
                var row = pair.Value;
                var results = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey,
                        new EmergencyWorkSuspensionFact(row.fact)) }, results, out string failure))
                    throw new InvalidOperationException("suspension-observation-replay-pending:" + failure);
                RequireSuspensionDelivery(results[0], row.publicationKey);
                if (suspendedWork.ContainsKey(pair.Key)) row.pending = false;
                else suspensionObservations.Remove(pair.Key);
            }
        }
        finally { publishingSuspension = false; }
    }

    private static void RequireSuspensionDelivery(in DomainFactDelivery delivery, string key)
    {
        if (!delivery.Completed || delivery.OperationKey != key
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 1024, false)
            || delivery.Fingerprint?.Length != 64
            || !delivery.Fingerprint.All(value => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
            throw new InvalidOperationException("suspension-observation-delivery-pending:" + delivery.Detail);
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
        result.suspensionObservations = suspensionObservations.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value.Clone()).ToList();
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
                    progressExternallyPersisted = value.ProgressExternallyPersisted,
                    inlineCompletedWork = value.InlineCompletedWork,
                    inlineRequiredWork = value.InlineRequiredWork,
                    outcomeOwnerRevision = value.OutcomeOwnerRevision
                });
        }
        result.suspendedWork.Sort((left, right) =>
            string.CompareOrdinal(left.characterId, right.characterId));
        return result;
    }

    public void RestoreAlertSaveData(
        DungeonStory.Infrastructure.SettlementThreatAlertSaveData saveData)
    {
        if (saveData == null) throw new ArgumentNullException(nameof(saveData));
        if (restorePreviousState != null)
        {
            restoreCandidate = saveData;
            return;
        }
        ApplyAlertSaveData(saveData);
    }

    private void ApplyAlertSaveData(
        DungeonStory.Infrastructure.SettlementThreatAlertSaveData saveData)
    {
        EmergencyWorkSuspensionObservationState.Validate(saveData);
        if (saveData == null
            || saveData.incidents == null
            || saveData.suspendedWork == null)
        {
            throw new ArgumentNullException(nameof(saveData));
        }

        incidents.Clear();
        contextTransitions.Clear();
        suspendedWork.Clear();
        suspensionObservations.Clear();
        lastSuspensionRetryAt = null;
        foreach (var row in saveData.suspensionObservations)
            suspensionObservations.Add(row.fact.characterId, row.Clone());
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
                    source.progressExternallyPersisted,
                    source.inlineCompletedWork,
                    source.inlineRequiredWork,
                    source.outcomeOwnerRevision,
                    suspensionObservations[source.characterId].fact.workerDisplayName,
                    suspensionObservations[source.characterId].fact.targetDisplayName));
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
        if (publishingSuspension)
            return EmergencyAccountingResult.Fail("SettlementSuspensionReentrant", "A suspension observation is already being published.");
        if (string.IsNullOrWhiteSpace(value.CharacterId)
            || !value.WorkTypeId.IsValid
            || string.IsNullOrWhiteSpace(value.TargetBuildingId)
            || value.AlertEpochId != alertEpochId
            || value.SuspendedAtAbsoluteHour < 0L
            || value.OutcomeOwnerRevision <= 0L
            || (!value.ProgressExternallyPersisted && !value.HasInlineProgress))
        {
            return EmergencyAccountingResult.Fail(
                "SettlementSuspendedWorkInvalid",
                "Suspended work requires a character, work type, target, current epoch and externally persisted progress.");
        }

        bool hadPrevious = suspendedWork.TryGetValue(
            value.CharacterId,
            out SettlementSuspendedWorkSnapshot previous);
        suspensionObservations.TryGetValue(value.CharacterId, out var previousObservation);
        if (hadPrevious && previous.OutcomeOwnerRevision == value.OutcomeOwnerRevision)
        {
            // Escalation may rebind the active epoch; the original fact and names
            // remain frozen. A repeated physical receipt is not a second event.
            return Matches(previous, value) && previousObservation != null
                ? EmergencyAccountingResult.Ok(previousObservation.pending
                    ? "suspended-work-recorded-observation-pending" : "suspended-work-already-recorded")
                : EmergencyAccountingResult.Fail(
                    "SettlementSuspendedWorkRevisionConflict", "The suspension revision has different applied state.");
        }
        if (previousObservation?.pending == true)
            return EmergencyAccountingResult.Fail("SettlementSuspensionObservationPending", "The previous applied suspension awaits observation.");
        if (hadPrevious && value.OutcomeOwnerRevision <= previous.OutcomeOwnerRevision)
            return EmergencyAccountingResult.Fail("SettlementSuspendedWorkRevisionConflict", "Suspension revisions cannot move backwards.");
        if (!WorkTypeCatalog.TryGet(value.WorkTypeId, out var workType))
            return EmergencyAccountingResult.Fail("SettlementSuspendedWorkInvalid", "Unknown suspended work type.");
        EmergencyWorkSuspensionFact fact;
        try { fact = new EmergencyWorkSuspensionFact(value, Math.Max(1, calendar.Day), workType.DisplayName?.Trim() ?? string.Empty); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return EmergencyAccountingResult.Fail("SettlementSuspendedWorkInvalid", exception.Message); }
        IDomainFactStage stage;
        try
        {
            if (!facts.TryStage(fact, out stage, out string failureReason))
                return EmergencyAccountingResult.Fail("SettlementSuspendedWorkObservationPrepareFailed", failureReason);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        { return EmergencyAccountingResult.Fail("SettlementSuspendedWorkObservationPrepareFailed", exception.Message); }
        bool applied = false, publicationCrossed = false;
        publishingSuspension = true;
        try
        {
            if (stage.IsReplay)
                return EmergencyAccountingResult.Fail("SettlementSuspendedWorkReplayStateMissing",
                    "The suspension is already observed but its exact active owner state is absent.");
            var observation = new EmergencyWorkSuspensionObservationSaveData
            { fact = fact.Snapshot(), publicationKey = stage.OperationKey, pending = true };
            observation.Validate();
            suspendedWork[value.CharacterId] = value;
            suspensionObservations[value.CharacterId] = observation;
            TouchSnapshot();
            applied = true;
            publicationCrossed = true;
            var delivered = stage.Commit();
            if (!delivered.MayBeCommitted)
            {
                publicationCrossed = false;
                RestorePrevious();
                return EmergencyAccountingResult.Fail("SettlementSuspendedWorkObservationRejected", delivered.Detail);
            }
            if (!delivered.Completed)
                return EmergencyAccountingResult.Ok("suspended-work-recorded-observation-pending:" + delivered.Detail);
            RequireSuspensionDelivery(delivered, observation.publicationKey);
            if (suspendedWork.ContainsKey(value.CharacterId)) observation.pending = false;
            else suspensionObservations.Remove(value.CharacterId);
            return EmergencyAccountingResult.Ok("suspended-work-recorded");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {
            if (publicationCrossed)
                return EmergencyAccountingResult.Ok("suspended-work-recorded-observation-pending:" + exception.Message);
            if (applied) RestorePrevious();
            return EmergencyAccountingResult.Fail("SettlementSuspendedWorkObservationFailed", exception.Message);
        }
        finally
        {
            try { stage?.Cancel(); }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
            { UnityEngine.Debug.LogException(exception); }
            finally { publishingSuspension = false; }
        }

        void RestorePrevious()
        {
            if (hadPrevious) suspendedWork[value.CharacterId] = previous;
            else suspendedWork.Remove(value.CharacterId);
            if (previousObservation != null) suspensionObservations[value.CharacterId] = previousObservation;
            else suspensionObservations.Remove(value.CharacterId);
            TouchSnapshot();
        }
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
        RemoveCompletedSuspensionObservation(normalized);
        TouchSnapshot();
        return EmergencyAccountingResult.Ok("suspended-work-resumed");
    }

    public EmergencyAccountingResult MarkSuspendedWorkAbandoned(
        string characterId,
        long epochId,
        string reasonCode)
    {
        string normalized = characterId?.Trim() ?? string.Empty;
        string normalizedReason = reasonCode?.Trim() ?? string.Empty;
        if (normalizedReason.Length == 0)
        {
            return EmergencyAccountingResult.Fail(
                "SettlementSuspendedWorkAbandonReasonMissing",
                "Abandoning suspended work requires a stable reason code.");
        }
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
        RemoveCompletedSuspensionObservation(normalized);
        TouchSnapshot();
        return EmergencyAccountingResult.Ok(
            $"suspended-work-abandoned:{normalizedReason}");
    }

    private void RemoveCompletedSuspensionObservation(string characterId)
    {
        if (suspensionObservations.TryGetValue(characterId, out var row) && !row.pending)
            suspensionObservations.Remove(characterId);
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
                        previousWork.ProgressExternallyPersisted,
                        previousWork.InlineCompletedWork,
                        previousWork.InlineRequiredWork,
                        previousWork.OutcomeOwnerRevision,
                        previousWork.WorkerDisplayName,
                        previousWork.TargetDisplayName);
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

    private static bool Matches(
        in SettlementSuspendedWorkSnapshot left,
        in SettlementSuspendedWorkSnapshot right) =>
        string.Equals(left.CharacterId, right.CharacterId, StringComparison.Ordinal)
        && left.WorkTypeId == right.WorkTypeId
        && string.Equals(
            left.TargetBuildingId,
            right.TargetBuildingId,
            StringComparison.Ordinal)
        && left.AlertEpochId == right.AlertEpochId
        && left.SuspendedAtAbsoluteHour == right.SuspendedAtAbsoluteHour
        && left.ProgressExternallyPersisted == right.ProgressExternallyPersisted
        && left.InlineCompletedWork.Equals(right.InlineCompletedWork)
        && left.InlineRequiredWork.Equals(right.InlineRequiredWork)
        && left.OutcomeOwnerRevision == right.OutcomeOwnerRevision;

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
