using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Converts committed settlement alert transitions into bounded staff replans.
/// Raw sensor and presentation alerts never reach this class, so the alert
/// service's hysteresis remains the sole context-switch boundary.
/// </summary>
public sealed class CharacterAlarmResponseRuntime :
    IStartable,
    ITickable,
    IDisposable
{
    private const float BaseResponseDelaySeconds = 2f;
    private const int MaximumReturnsPerTick = 4;

    private readonly IGameEventBus events;
    private readonly ICharacterWorldQuery world;
    private readonly IBuildingWorldQuery buildings;
    private readonly IGameClock clock;
    private readonly IGameCalendar calendar;
    private readonly ISettlementAlertService alerts;
    private readonly ISettlementEmergencyReserveTargetQuery reserveTarget;
    private readonly IEmergencyRiskForecastRegistry forecasts;
    private readonly Dictionary<string, PendingAlarmResponse> pending =
        new(StringComparer.Ordinal);
    private readonly Queue<ReturningWorker> returning = new();
    private readonly HashSet<string> queuedReturning = new(StringComparer.Ordinal);
    private readonly HashSet<string> emergencyAssigned = new(StringComparer.Ordinal);
    private readonly List<string> completedPending = new();
    private IDisposable subscription;
    private IDisposable incidentSubscription;
    private long emergencyAssignedEpochId = -1L;

    public int PendingResponderCountForDiagnostics => pending.Count;
    public int ReturningResponderCountForDiagnostics => returning.Count;
    public int AssignedResponderCountForDiagnostics => emergencyAssigned.Count;
    public long AssignedResponderEpochForDiagnostics => emergencyAssignedEpochId;

    public CharacterAlarmResponseRuntime(
        IGameEventBus events,
        ICharacterWorldQuery world,
        IBuildingWorldQuery buildings,
        IGameClock clock,
        IGameCalendar calendar,
        ISettlementAlertService alerts,
        ISettlementEmergencyReserveTargetQuery reserveTarget,
        IEmergencyRiskForecastRegistry forecasts)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        this.reserveTarget = reserveTarget
            ?? throw new ArgumentNullException(nameof(reserveTarget));
        this.forecasts = forecasts
            ?? throw new ArgumentNullException(nameof(forecasts));
    }

    public void Start()
    {
        subscription ??=
            events.Subscribe<SettlementCommittedAlertChangedEvent>(OnAlertChanged);
        incidentSubscription ??=
            events.Subscribe<SettlementActiveIncidentsChangedEvent>(
                OnActiveIncidentsChanged);
        RestoreQueuesFromAuthoritativeAlert();
    }

    public void Dispose()
    {
        subscription?.Dispose();
        incidentSubscription?.Dispose();
        subscription = null;
        incidentSubscription = null;
        pending.Clear();
        returning.Clear();
        queuedReturning.Clear();
        emergencyAssigned.Clear();
        completedPending.Clear();
    }

    public void Tick()
    {
        SettlementAlertSnapshot alert = alerts.Capture();
        if (alert.CommittedLevel == SettlementThreatAlertLevel.Red
            && RetireIneligibleEmergencyResponders(alert.AlertEpochId))
        {
            // Lifecycle cleanup owns transient actions, while this runtime owns
            // the emergency work gate and responder accounting. Replace a
            // casualty in the same committed epoch instead of leaving an
            // unusable gate counted as live coverage.
            ScheduleEmergencyResponders(
                alert.AlertEpochId,
                applyResponseDelay: true);
            alert = alerts.Capture();
        }
        ProcessEmergencyResponses(alert);
        ProcessReturns(alert);
    }

    private void ProcessEmergencyResponses(SettlementAlertSnapshot alert)
    {
        completedPending.Clear();
        foreach ((string characterId, PendingAlarmResponse response) in pending)
        {
            if (alert.CommittedLevel != SettlementThreatAlertLevel.Red
                || alert.AlertEpochId != response.EpochId)
            {
                completedPending.Add(characterId);
                continue;
            }
            if (clock.Time < response.DueAt)
            {
                continue;
            }

            if (CharacterWorkRoleUtility.TryGetWork(
                    response.Actor,
                    out AbilityWork activeWork)
                && activeWork.isWorking)
            {
                if (activeWork.HasEmergencyResponseWorkGateForDiagnostics
                    && activeWork.AssignedWorkTypeId
                        == activeWork.EmergencyResponseOnlyWorkTypeForDiagnostics)
                {
                    response.Actor?.Brain?.PreferWorkActionOnNextDecision(
                        activeWork.EmergencyResponseOnlyWorkTypeForDiagnostics,
                        persistenceSeconds: 90f);
                    completedPending.Add(characterId);
                    continue;
                }

                activeWork.RequestEmergencySuspension(response.EpochId);
                continue;
            }

            if (activeWork != null
                && activeWork.TryConsumeEmergencySuspension(
                    out EmergencyWorkSuspensionReceipt receipt))
            {
                EmergencyAccountingResult recorded = alerts.RecordSuspendedWork(
                    new SettlementSuspendedWorkSnapshot(
                        characterId,
                        receipt.WorkTypeId,
                        receipt.TargetBuildingId,
                        receipt.AlertEpochId,
                        calendar.AbsoluteHour,
                        receipt.ProgressExternallyPersisted,
                        receipt.InlineCompletedWork,
                        receipt.InlineRequiredWork));
                if (!recorded.Success)
                {
                    throw new InvalidOperationException(
                        $"{recorded.Code}: {recorded.Message}");
                }
            }

            response.Actor?.Brain?.PreferWorkActionOnNextDecision(
                ResolveEmergencyWorkType(alert),
                persistenceSeconds: 90f);
            response.Actor?.Brain?.RequestImmediateReplan(clearFailures: true);
            completedPending.Add(characterId);
        }
        for (int index = 0; index < completedPending.Count; index++)
        {
            pending.Remove(completedPending[index]);
        }
    }

    private void ProcessReturns(SettlementAlertSnapshot alert)
    {
        int resumed = 0;
        int inspected = returning.Count;
        while (resumed < MaximumReturnsPerTick
            && inspected > 0
            && returning.Count > 0)
        {
            inspected--;
            ReturningWorker next = returning.Dequeue();
            queuedReturning.Remove(next.CharacterId);
            if (alert.CommittedLevel != SettlementThreatAlertLevel.Green
                || alert.AlertEpochId != next.EpochId)
            {
                continue;
            }
            CharacterWorkRoleUtility.TryGetWork(
                next.Actor,
                out AbilityWork work);
            if (work != null
                && (work.isWorking
                    || work.HasActiveWorkRoutineForDiagnostics))
            {
                EnqueueReturn(next.Actor, next.EpochId);
                continue;
            }

            // AIWork owns its route before AbilityWork enters the working
            // phase.  Releasing the emergency gate while that route is still
            // running lets the old response arrive after the original
            // priority has been restored and overwrite that command.  Retire
            // the in-flight emergency route first, then release the gate and
            // restore the journal in this same tick so no scheduler decision
            // can enter between the ownership transitions.
            AIBrain brain = next.Actor?.Brain;
            if (brain?.IsExternallyDrivenActionActive == true)
            {
                EnqueueReturn(next.Actor, next.EpochId);
                continue;
            }
            if (work?.HasEmergencyResponseWorkGateForDiagnostics == true
                && work.EmergencyResponseWorkEpochForDiagnostics
                    != next.EpochId)
            {
                throw new InvalidOperationException(
                    $"Green return gate epoch mismatch for {next.CharacterId}: "
                    + $"queue={next.EpochId}; gate="
                    + work.EmergencyResponseWorkEpochForDiagnostics);
            }
            if (brain?.HasRunningWorkAction == true)
            {
                bool stopped = brain.StopCurrentActionForReplan(
                    "alert-green-return");
                if (!stopped
                    || brain.HasRunningWorkAction
                    || (work?.isWorking ?? false)
                    || (work?.HasActiveWorkRoutineForDiagnostics ?? false))
                {
                    EnqueueReturn(next.Actor, next.EpochId);
                    continue;
                }
            }

            work?.EndEmergencyResponseWorkGate(next.EpochId);
            bool restoredPriority = TryRestoreSuspendedPriority(
                next.Actor,
                next.CharacterId,
                next.EpochId);
            if (!restoredPriority)
            {
                next.Actor?.Brain?.RequestImmediateReplan(clearFailures: false);
            }
            resumed++;
        }
    }

    private void OnAlertChanged(SettlementCommittedAlertChangedEvent change)
    {
        if (change.Current == SettlementThreatAlertLevel.Amber)
        {
            // Amber changes reserve planning and response readiness without
            // interrupting the whole workforce.
            completedPending.Clear();
            foreach ((string characterId, PendingAlarmResponse response) in pending)
            {
                if (response.EpochId != change.EpochId)
                {
                    continue;
                }
                if (CharacterWorkRoleUtility.TryGetWork(
                        response.Actor,
                        out AbilityWork activeWork))
                {
                    if (activeWork.TryConsumeEmergencySuspension(
                            out EmergencyWorkSuspensionReceipt receipt))
                    {
                        EmergencyAccountingResult recorded =
                            alerts.RecordSuspendedWork(
                                new SettlementSuspendedWorkSnapshot(
                                    characterId,
                                    receipt.WorkTypeId,
                                    receipt.TargetBuildingId,
                                    receipt.AlertEpochId,
                                    calendar.AbsoluteHour,
                                    receipt.ProgressExternallyPersisted,
                                    receipt.InlineCompletedWork,
                                    receipt.InlineRequiredWork));
                        if (!recorded.Success)
                        {
                            throw new InvalidOperationException(
                                $"{recorded.Code}: {recorded.Message}");
                        }
                    }
                    else
                    {
                        activeWork.CancelEmergencySuspensionRequest(
                            change.EpochId);
                    }
                }
                completedPending.Add(characterId);
            }
            for (int index = 0; index < completedPending.Count; index++)
            {
                pending.Remove(completedPending[index]);
            }
            return;
        }

        if (change.Current == SettlementThreatAlertLevel.Green)
        {
            foreach (PendingAlarmResponse response in pending.Values)
            {
                if (CharacterWorkRoleUtility.TryGetWork(
                        response.Actor,
                        out AbilityWork pendingWork))
                {
                    pendingWork.CancelEmergencySuspensionRequest(
                        response.EpochId);
                }
            }
            pending.Clear();
            List<string> returnIds = new List<string>(emergencyAssigned);
            SettlementAlertSnapshot snapshot = alerts.Capture();
            for (int index = 0; index < snapshot.SuspendedWork.Count; index++)
            {
                string characterId = snapshot.SuspendedWork[index].CharacterId;
                if (!returnIds.Contains(characterId))
                {
                    returnIds.Add(characterId);
                }
            }
            returnIds.Sort(StringComparer.Ordinal);
            for (int index = 0; index < returnIds.Count; index++)
            {
                string characterId = returnIds[index];
                bool foundActor = TryFindCharacter(
                    characterId,
                    out CharacterActor actor);
                if (foundActor
                    && alerts.TryClaimContextTransition(
                        characterId,
                        change.EpochId,
                        toEmergency: false))
                {
                    EnqueueReturn(actor, change.EpochId);
                }
                else if (!foundActor
                         && alerts.TryGetSuspendedWork(characterId, out _))
                {
                    EmergencyAccountingResult abandoned =
                        alerts.MarkSuspendedWorkAbandoned(
                            characterId,
                            change.EpochId,
                            "responder-missing-at-return");
                    if (!abandoned.Success)
                    {
                        throw new InvalidOperationException(
                            $"{abandoned.Code}: {abandoned.Message}");
                    }
                }
            }
            emergencyAssigned.Clear();
            emergencyAssignedEpochId = -1L;
            return;
        }

        ScheduleEmergencyResponders(change.EpochId, applyResponseDelay: true);
    }

    private void OnActiveIncidentsChanged(
        SettlementActiveIncidentsChangedEvent change)
    {
        if (change.CommittedLevel == SettlementThreatAlertLevel.Red
            && change.ActiveIncidentCount > 0)
        {
            ScheduleEmergencyResponders(
                change.EpochId,
                applyResponseDelay: true);
        }
    }

    private void EnqueueReturn(CharacterActor actor, long epochId)
    {
        if (!TryGetCharacterId(actor, out string characterId)
            || !queuedReturning.Add(characterId))
        {
            return;
        }
        returning.Enqueue(new ReturningWorker(actor, characterId, epochId));
    }

    private bool TryRestoreSuspendedPriority(
        CharacterActor actor,
        string characterId,
        long epochId)
    {
        if (!alerts.TryGetSuspendedWork(
                characterId,
                out SettlementSuspendedWorkSnapshot suspended)
            || suspended.AlertEpochId != epochId)
        {
            return false;
        }

        BuildableObject target = null;
        IReadOnlyList<BuildableObject> all = buildings.Buildings;
        for (int index = 0; index < all.Count; index++)
        {
            BuildableObject candidate = all[index];
            if (candidate != null
                && !candidate.isDestroy
                && string.Equals(
                    candidate.PersistentInstanceId.Value,
                    suspended.TargetBuildingId,
                    StringComparison.Ordinal))
            {
                target = candidate;
                break;
            }
        }

        string restoreFailure = string.Empty;
        AbilityWork work = null;
        bool restored = actor != null
            && actor.TryGetAbility(out work)
            && target != null
            && work.TrySetPriorityWorkTarget(
                target,
                suspended.WorkTypeId,
                null,
                out restoreFailure);
        EmergencyAccountingResult cleared;
        if (restored)
        {
            if (suspended.HasInlineProgress)
            {
                work.RestoreInlineEmergencyProgress(
                    suspended.WorkTypeId,
                    suspended.TargetBuildingId,
                    suspended.InlineCompletedWork,
                    suspended.InlineRequiredWork);
            }
            // Restoring the domain priority alone is not enough: the alarm
            // response may still own the brain's preferred AIWork route even
            // after its work gate is released. Publish the restored work type
            // through the same production scheduler boundary used by direct
            // priority commands so a completed emergency action cannot select
            // another response before the original order resumes.
            actor.Brain?.PreferWorkActionOnNextDecision(
                suspended.WorkTypeId,
                persistenceSeconds: 90f);
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
            cleared = alerts.MarkSuspendedWorkResumed(
                characterId,
                epochId);
        }
        else
        {
            string reasonCode = target == null
                ? "target-destroyed-or-missing"
                : "priority-restore-rejected";
            cleared = alerts.MarkSuspendedWorkAbandoned(
                characterId,
                epochId,
                reasonCode);
            actor?.AddActivity(CharacterActivityEvent.Work(
                suspended.WorkTypeId,
                CharacterActivityOutcomes.Cancelled,
                target == null
                    ? "비상 중단 중 원래 작업 대상이 사라져 작업을 폐기하고 다시 계획합니다."
                    : $"비상 중단 작업을 복귀하지 못해 다시 계획합니다: {restoreFailure}",
                target,
                reasonCode: reasonCode));
        }
        if (!cleared.Success)
        {
            throw new InvalidOperationException(
                $"{cleared.Code}: {cleared.Message}");
        }
        return restored;
    }

    private void RestoreQueuesFromAuthoritativeAlert()
    {
        SettlementAlertSnapshot alert = alerts.Capture();
        if (alert.CommittedLevel == SettlementThreatAlertLevel.Red)
        {
            ScheduleEmergencyResponders(
                alert.AlertEpochId,
                applyResponseDelay: false);
            return;
        }

        if (alert.CommittedLevel == SettlementThreatAlertLevel.Amber)
        {
            WorkTypeId allowedWorkType = ResolveEmergencyWorkType(alert);
            emergencyAssigned.Clear();
            emergencyAssignedEpochId = alert.AlertEpochId;
            for (int index = 0; index < alert.SuspendedWork.Count; index++)
            {
                SettlementSuspendedWorkSnapshot suspended =
                    alert.SuspendedWork[index];
                if (TryFindCharacter(suspended.CharacterId, out CharacterActor actor)
                    && actor.TryGetAbility(out AbilityWork work))
                {
                    BindEmergencyResponseWorkGate(
                        work,
                        suspended.AlertEpochId,
                        allowedWorkType);
                    emergencyAssigned.Add(suspended.CharacterId);
                }
            }
            return;
        }

        if (alert.CommittedLevel != SettlementThreatAlertLevel.Green)
        {
            return;
        }
        for (int index = 0; index < alert.SuspendedWork.Count; index++)
        {
            SettlementSuspendedWorkSnapshot suspended = alert.SuspendedWork[index];
            if (TryFindCharacter(suspended.CharacterId, out CharacterActor actor))
            {
                EnqueueReturn(actor, suspended.AlertEpochId);
            }
        }
    }

    private bool TryFindCharacter(string characterId, out CharacterActor actor)
    {
        IReadOnlyList<CharacterActor> characters = world.Characters;
        for (int index = 0; index < characters.Count; index++)
        {
            CharacterActor candidate = characters[index];
            if (TryGetCharacterId(candidate, out string candidateId)
                && string.Equals(candidateId, characterId, StringComparison.Ordinal))
            {
                actor = candidate;
                return true;
            }
        }
        actor = null;
        return false;
    }

    private void ScheduleEmergencyResponders(
        long epochId,
        bool applyResponseDelay)
    {
        SettlementAlertSnapshot currentAlert = alerts.Capture();
        if (epochId <= 0L
            || currentAlert.AlertEpochId != epochId
            || currentAlert.CommittedLevel != SettlementThreatAlertLevel.Red)
        {
            throw new InvalidOperationException(
                $"Emergency responder scheduling requires the current Red epoch. requested={epochId}; current={currentAlert.AlertEpochId}/{currentAlert.CommittedLevel}.");
        }
        WorkTypeId allowedEmergencyWorkType =
            ResolveEmergencyWorkType(currentAlert);
        ReconcileEmergencyResponderOwnership(
            currentAlert,
            epochId,
            allowedEmergencyWorkType);
        SettlementEmergencyReserveTargetSnapshot target =
            reserveTarget.CaptureTarget();
        EmergencyRiskForecastSnapshot risk = forecasts.Capture();
        long liveTarget = Math.Max(
            12_000L,
            Math.Max(target.MinimumMilliWu, risk.HighestP90MilliWu));
        if (currentAlert.CommittedLevel == SettlementThreatAlertLevel.Red)
        {
            liveTarget = checked(liveTarget * 2L);
        }
        int desiredResponderCount = Math.Max(
            1,
            (int)Math.Ceiling(
                liveTarget
                / (double)EmergencyWuUnits.MaximumReserveWindowMilliWu));
        int required = Math.Max(
            0,
            desiredResponderCount - emergencyAssigned.Count);
        List<CharacterActor> candidates = new List<CharacterActor>(world.Characters);
        candidates.Sort((left, right) => string.CompareOrdinal(
            GetSortableCharacterId(left),
            GetSortableCharacterId(right)));

        for (int index = 0; index < candidates.Count && required > 0; index++)
        {
            CharacterActor actor = candidates[index];
            if (!IsEmergencyResponderEligible(actor)
                || !actor.TryGetAbility(out AbilityWork responseWork)
                || !TryGetCharacterId(actor, out string characterId)
                || emergencyAssigned.Contains(characterId))
            {
                continue;
            }

            if (CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work)
                && work.AssignedWorkTypeId.IsValid
                && WorkTypeCatalog.TryGet(
                    work.AssignedWorkTypeId,
                    out WorkTypeDefinition definition)
                && (definition.EmergencyFlags
                    & (EmergencyWorkFlags.CriticalNonInterruptible
                        | EmergencyWorkFlags.ProtectedRecovery
                        | EmergencyWorkFlags.EmergencyResponse)) != 0)
            {
                continue;
            }

            CharacterPerformanceSnapshot response = actor.Stats?.EvaluatePerformance(
                CharacterPerformanceFormulaIds.AlarmResponse);
            if (response?.IsApplicable != true)
            {
                continue;
            }
            if (!alerts.TryClaimContextTransition(
                    characterId,
                    epochId,
                    toEmergency: true))
            {
                continue;
            }
            BindEmergencyResponseWorkGate(
                responseWork,
                epochId,
                allowedEmergencyWorkType);
            float dueAt = applyResponseDelay
                ? clock.Time
                    + BaseResponseDelaySeconds
                        / Math.Max(0.01f, response.Value)
                : clock.Time;
            CharacterPerformanceExecutionTrace.Record(
                CharacterPerformanceFormulaIds.AlarmResponse,
                "CharacterAlarmResponseRuntime.ScheduleEmergencyResponders",
                applyResponseDelay ? BaseResponseDelaySeconds : 0f,
                dueAt - clock.Time,
                characterId);
            pending[characterId] = new PendingAlarmResponse(
                actor,
                dueAt,
                epochId);
            emergencyAssigned.Add(characterId);
            required--;
        }
    }

    private bool RetireIneligibleEmergencyResponders(long epochId)
    {
        if (epochId <= 0L || emergencyAssigned.Count == 0)
        {
            return false;
        }

        List<string> retired = null;
        foreach (string characterId in emergencyAssigned)
        {
            if (TryFindCharacter(characterId, out CharacterActor actor)
                && IsEmergencyResponderEligible(actor))
            {
                continue;
            }

            retired ??= new List<string>();
            retired.Add(characterId);
        }
        if (retired == null)
        {
            return false;
        }

        retired.Sort(StringComparer.Ordinal);
        for (int index = 0; index < retired.Count; index++)
        {
            string characterId = retired[index];
            if (TryFindCharacter(characterId, out CharacterActor actor)
                && actor.TryGetAbility(out AbilityWork work))
            {
                work.CancelEmergencySuspensionRequest(epochId);
                if (work.HasEmergencyResponseWorkGateForDiagnostics)
                {
                    work.EndEmergencyResponseWorkGate(
                        work.EmergencyResponseWorkEpochForDiagnostics);
                }
            }
            pending.Remove(characterId);
            emergencyAssigned.Remove(characterId);
            RemoveQueuedReturn(characterId);
        }
        return true;
    }

    private void RemoveQueuedReturn(string characterId)
    {
        queuedReturning.Remove(characterId);
        int count = returning.Count;
        for (int index = 0; index < count; index++)
        {
            ReturningWorker item = returning.Dequeue();
            if (!string.Equals(
                    item.CharacterId,
                    characterId,
                    StringComparison.Ordinal))
            {
                returning.Enqueue(item);
            }
        }
    }

    private static bool IsEmergencyResponderEligible(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && actor.CurrentLifecycleState == CharacterLifecycleState.Active
            && actor.CanRunAi
            && actor.Brain != null
            && actor.TryGetAbility(out AbilityWork _)
            && actor.Stats?.EvaluatePerformance(
                CharacterPerformanceFormulaIds.AlarmResponse)?.IsApplicable == true;
    }

    private void ReconcileEmergencyResponderOwnership(
        SettlementAlertSnapshot currentAlert,
        long epochId,
        WorkTypeId allowedEmergencyWorkType)
    {
        if (emergencyAssignedEpochId > epochId)
        {
            throw new InvalidOperationException(
                $"Emergency responder ownership cannot move backwards from epoch {emergencyAssignedEpochId} to {epochId}.");
        }

        bool epochChanged = emergencyAssignedEpochId != epochId;
        HashSet<string> carryForward = new HashSet<string>(
            emergencyAssigned,
            StringComparer.Ordinal);
        Dictionary<string, PendingAlarmResponse> carriedPending =
            new Dictionary<string, PendingAlarmResponse>(StringComparer.Ordinal);

        foreach ((string characterId, PendingAlarmResponse response) in pending)
        {
            carryForward.Add(characterId);
            carriedPending[characterId] = response;
        }
        foreach (ReturningWorker response in returning)
        {
            carryForward.Add(response.CharacterId);
        }
        for (int index = 0; index < currentAlert.SuspendedWork.Count; index++)
        {
            carryForward.Add(currentAlert.SuspendedWork[index].CharacterId);
        }

        IReadOnlyList<CharacterActor> characters = world.Characters;
        for (int index = 0; index < characters.Count; index++)
        {
            CharacterActor actor = characters[index];
            if (!TryGetCharacterId(actor, out string characterId)
                || !actor.TryGetAbility(out AbilityWork work))
            {
                continue;
            }
            if (work.HasEmergencyResponseWorkGateForDiagnostics)
            {
                carryForward.Add(characterId);
                continue;
            }
            if (work.isWorking
                && work.AssignedWorkTypeId.IsValid
                && WorkTypeCatalog.TryGet(
                    work.AssignedWorkTypeId,
                    out WorkTypeDefinition definition)
                && (definition.EmergencyFlags
                    & EmergencyWorkFlags.EmergencyResponse) != 0)
            {
                carryForward.Add(characterId);
            }
        }

        if (epochChanged)
        {
            pending.Clear();
            returning.Clear();
            queuedReturning.Clear();
            emergencyAssigned.Clear();
            emergencyAssignedEpochId = epochId;
        }

        List<string> ordered = new List<string>(carryForward);
        ordered.Sort(StringComparer.Ordinal);
        for (int index = 0; index < ordered.Count; index++)
        {
            string characterId = ordered[index];
            if (!TryFindCharacter(characterId, out CharacterActor actor)
                || !actor.TryGetAbility(out AbilityWork work))
            {
                emergencyAssigned.Remove(characterId);
                pending.Remove(characterId);
                continue;
            }

            bool gateChanged = BindEmergencyResponseWorkGate(
                work,
                epochId,
                allowedEmergencyWorkType);
            emergencyAssigned.Add(characterId);

            if (epochChanged
                && carriedPending.TryGetValue(
                    characterId,
                    out PendingAlarmResponse previousPending))
            {
                bool receiptRecorded = false;
                if (work.TryConsumeEmergencySuspension(
                        out EmergencyWorkSuspensionReceipt receipt))
                {
                    EmergencyAccountingResult recorded =
                        alerts.RecordSuspendedWork(
                            new SettlementSuspendedWorkSnapshot(
                                characterId,
                                receipt.WorkTypeId,
                                receipt.TargetBuildingId,
                                epochId,
                                calendar.AbsoluteHour,
                                receipt.ProgressExternallyPersisted,
                                receipt.InlineCompletedWork,
                                receipt.InlineRequiredWork));
                    if (!recorded.Success)
                    {
                        throw new InvalidOperationException(
                            $"{recorded.Code}: {recorded.Message}");
                    }
                    receiptRecorded = true;
                }
                else
                {
                    work.CancelEmergencySuspensionRequest(
                        previousPending.EpochId);
                }

                if (!receiptRecorded)
                {
                    pending[characterId] = new PendingAlarmResponse(
                        actor,
                        previousPending.DueAt,
                        epochId);
                }
            }

            bool alreadyRunningAllowedResponse = work.isWorking
                && work.AssignedWorkTypeId == allowedEmergencyWorkType;
            if (gateChanged && !alreadyRunningAllowedResponse)
            {
                actor.Brain?.PreferWorkActionOnNextDecision(
                    allowedEmergencyWorkType,
                    persistenceSeconds: 90f);
                actor.Brain?.RequestImmediateReplan(clearFailures: true);
            }
        }
    }

    private static WorkTypeId ResolveEmergencyWorkType(
        SettlementAlertSnapshot alert)
    {
        for (int index = 0; index < alert.ActiveIncidentIds.Count; index++)
        {
            if (string.Equals(
                    alert.ActiveIncidentIds[index],
                    "incident:invasion:active",
                    StringComparison.Ordinal))
            {
                return BuiltInWorkTypeIds.Guard;
            }
        }
        for (int index = 0; index < alert.ActiveIncidentIds.Count; index++)
        {
            if (string.Equals(
                    alert.ActiveIncidentIds[index],
                    "incident:medical-capacity",
                    StringComparison.Ordinal))
            {
                return BuiltInWorkTypeIds.Rescue;
            }
        }
        return BuiltInWorkTypeIds.ThreatMitigation;
    }

    private static bool BindEmergencyResponseWorkGate(
        AbilityWork work,
        long epochId,
        WorkTypeId allowedWorkTypeId)
    {
        if (work == null)
        {
            throw new ArgumentNullException(nameof(work));
        }
        if (!work.HasEmergencyResponseWorkGateForDiagnostics
            )
        {
            work.BeginEmergencyResponseWorkGate(epochId, allowedWorkTypeId);
            return true;
        }

        long currentEpoch = work.EmergencyResponseWorkEpochForDiagnostics;
        WorkTypeId currentWorkType =
            work.EmergencyResponseOnlyWorkTypeForDiagnostics;
        if (currentEpoch > epochId)
        {
            throw new InvalidOperationException(
                $"Emergency work ownership cannot move backwards from epoch {currentEpoch} to {epochId}.");
        }
        if (currentEpoch == epochId)
        {
            if (currentWorkType == allowedWorkTypeId)
            {
                return false;
            }
            work.UpdateEmergencyResponseWorkGate(
                epochId,
                currentWorkType,
                allowedWorkTypeId);
            return true;
        }

        work.AdvanceEmergencyResponseWorkGate(
            currentEpoch,
            epochId,
            allowedWorkTypeId);
        return true;
    }

    private static string GetSortableCharacterId(CharacterActor actor)
    {
        return TryGetCharacterId(actor, out string characterId)
            ? characterId
            : "~";
    }

    private static bool TryGetCharacterId(
        CharacterActor actor,
        out string characterId)
    {
        characterId = actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
        return actor != null
            && !actor.IsDead
            && actor.Brain != null
            && characterId.Length > 0;
    }

    private readonly struct PendingAlarmResponse
    {
        public PendingAlarmResponse(CharacterActor actor, float dueAt, long epochId)
        {
            Actor = actor;
            DueAt = dueAt;
            EpochId = epochId;
        }

        public CharacterActor Actor { get; }
        public float DueAt { get; }
        public long EpochId { get; }
    }

    private readonly struct ReturningWorker
    {
        public ReturningWorker(
            CharacterActor actor,
            string characterId,
            long epochId)
        {
            Actor = actor;
            CharacterId = characterId;
            EpochId = epochId;
        }

        public CharacterActor Actor { get; }
        public string CharacterId { get; }
        public long EpochId { get; }
    }
}
