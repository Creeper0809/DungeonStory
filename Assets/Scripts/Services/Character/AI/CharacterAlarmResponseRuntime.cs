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
                        receipt.ProgressExternallyPersisted));
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
            if (CharacterWorkRoleUtility.TryGetWork(next.Actor, out AbilityWork work)
                && work.isWorking)
            {
                EnqueueReturn(next.Actor, next.EpochId);
                continue;
            }
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
                bool suspended = false;
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
                                    receipt.ProgressExternallyPersisted));
                        if (!recorded.Success)
                        {
                            throw new InvalidOperationException(
                                $"{recorded.Code}: {recorded.Message}");
                        }
                        suspended = true;
                    }
                    else
                    {
                        activeWork.CancelEmergencySuspensionRequest(
                            change.EpochId);
                    }
                }
                if (!suspended)
                {
                    emergencyAssigned.Remove(characterId);
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
                if (TryFindCharacter(characterId, out CharacterActor actor)
                    && alerts.TryClaimContextTransition(
                        characterId,
                        change.EpochId,
                        toEmergency: false))
                {
                    EnqueueReturn(actor, change.EpochId);
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

        bool restored = actor != null
            && actor.TryGetAbility(out AbilityWork work)
            && target != null
            && work.TrySetPriorityWorkTarget(
                target,
                suspended.WorkTypeId,
                null,
                out _);
        EmergencyAccountingResult cleared = alerts.MarkSuspendedWorkResumed(
            characterId,
            epochId);
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
        if (emergencyAssignedEpochId != epochId)
        {
            emergencyAssigned.Clear();
            emergencyAssignedEpochId = epochId;
        }
        SettlementEmergencyReserveTargetSnapshot target =
            reserveTarget.CaptureTarget();
        EmergencyRiskForecastSnapshot risk = forecasts.Capture();
        long liveTarget = Math.Max(
            12_000L,
            Math.Max(target.MinimumMilliWu, risk.HighestP90MilliWu));
        if (alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Red)
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
            if (actor == null
                || actor.IsDead
                || actor.Brain == null
                || !actor.TryGetAbility(out AbilityWork _)
                || !TryGetCharacterId(actor, out string characterId))
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
