using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public readonly struct WorkDutyStartDiagnostics
{
    public WorkDutyStartDiagnostics(
        AbilityWork.DutyState dutyState,
        bool actorMissing,
        bool expeditionBlocked,
        bool prioritySuppress,
        bool priorityWork,
        bool urgentPriorityWork,
        bool routineNeedBlocked,
        bool discontentBlocked,
        string discontentReason,
        bool conditionWouldTakeOffDuty,
        bool restProtectionBlocked,
        bool canStartByDutyGate)
    {
        DutyState = dutyState;
        ActorMissing = actorMissing;
        ExpeditionBlocked = expeditionBlocked;
        PrioritySuppress = prioritySuppress;
        PriorityWork = priorityWork;
        UrgentPriorityWork = urgentPriorityWork;
        RoutineNeedBlocked = routineNeedBlocked;
        DiscontentBlocked = discontentBlocked;
        DiscontentReason = discontentReason ?? string.Empty;
        ConditionWouldTakeOffDuty = conditionWouldTakeOffDuty;
        RestProtectionBlocked = restProtectionBlocked;
        CanStartByDutyGate = canStartByDutyGate;
    }

    public AbilityWork.DutyState DutyState { get; }
    public bool ActorMissing { get; }
    public bool ExpeditionBlocked { get; }
    public bool PrioritySuppress { get; }
    public bool PriorityWork { get; }
    public bool UrgentPriorityWork { get; }
    public bool RoutineNeedBlocked { get; }
    public bool DiscontentBlocked { get; }
    public string DiscontentReason { get; }
    public bool ConditionWouldTakeOffDuty { get; }
    public bool RestProtectionBlocked { get; }
    public bool CanStartByDutyGate { get; }
}

public sealed class WorkDutyController
{
    private static readonly WaitForSeconds WorkCheckDelay = new WaitForSeconds(1f);

    private readonly AbilityWork work;
    private readonly ICharacterNeedDefinitionCatalog needDefinitionCatalog;
    private AbilityWork.DutyState dutyState = AbilityWork.DutyState.OnDuty;
    private float offDutyStartedAt = float.NegativeInfinity;
    private bool restProtectionActive;
    private float restProtectionStartedAt = float.NegativeInfinity;
    private CharacterCondition? routineNeedBlockingWorkStart;
    private BuildableObject routineNeedResumeTarget;
    private WorkTypeId routineNeedResumeWorkTypeId;

    public WorkDutyController(
        AbilityWork work,
        ICharacterNeedDefinitionCatalog needDefinitionCatalog)
    {
        this.work = work ?? throw new System.ArgumentNullException(nameof(work));
        this.needDefinitionCatalog = needDefinitionCatalog
            ?? throw new System.ArgumentNullException(nameof(needDefinitionCatalog));
    }

    public AbilityWork.DutyState CurrentState => dutyState;
    public bool IsOffDuty => dutyState == AbilityWork.DutyState.OffDuty;
    public bool LastWorkRunCompleted { get; private set; }
    public bool LastWorkRunInterruptedForRoutineNeed { get; private set; }
    internal bool HasRoutineNeedWorkBlock => routineNeedBlockingWorkStart.HasValue;
    internal BuildableObject RoutineNeedResumeTarget => routineNeedResumeTarget;
    internal WorkTypeId RoutineNeedResumeWorkTypeId => routineNeedResumeWorkTypeId;
    private float Now => RequireGameClock().Time;

    public void InitializeWorkerCondition(CharacterSO data)
    {
        if (GetWorkerStats() == null || data == null)
        {
            return;
        }

        foreach (CharacterNeedDefinition definition in needDefinitionCatalog.All)
        {
            EnsureStatAtLeast(definition.Condition, definition.WorkerInitialValue);
        }

        EnsureStatAtLeast(CharacterCondition.MOOD, 75f);
    }

    public bool ShouldUseRestProtection()
    {
        WorkPriorityProfile priorities = work.WorkPriorities;
        if (priorities == null || !priorities.IsEnabled(BuiltInWorkTypeIds.Rest))
        {
            restProtectionActive = false;
            return false;
        }

        CharacterStats stats = GetWorkerStats();
        if (stats == null)
        {
            restProtectionActive = false;
            return false;
        }

        if (!stats.Stats.TryGetValue(CharacterCondition.SLEEP, out float sleep))
        {
            restProtectionActive = false;
            return false;
        }

        if (sleep <= work.RestProtectionSleepThreshold)
        {
            if (!restProtectionActive)
            {
                restProtectionStartedAt = Now;
            }

            restProtectionActive = true;
        }

        if (!restProtectionActive)
        {
            return false;
        }

        bool sleptEnough = sleep >= work.RestProtectionResumeSleepThreshold;
        bool waitedEnough = Now - restProtectionStartedAt >= work.MinimumRestProtectionSeconds;
        if (sleptEnough && waitedEnough)
        {
            restProtectionActive = false;
            restProtectionStartedAt = float.NegativeInfinity;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Captures the work-start gates without changing duty, rest protection,
    /// routine-need memory, assignments, or AI scheduling state.
    /// </summary>
    public WorkDutyStartDiagnostics CaptureStartDiagnostics()
    {
        CharacterActor actor = work.WorkerActor;
        bool actorMissing = actor == null;
        bool expeditionBlocked = actor != null && actor.IsOnExpedition;
        bool prioritySuppress = work.HasPrioritySuppressTarget;
        bool priorityWork = work.PriorityWorkTarget != null
            && work.PriorityWorkTypeId.IsValid;
        bool urgentPriorityWork = work.HasUrgentPriorityTarget();
        bool routineNeedBlocked = routineNeedBlockingWorkStart.HasValue;
        string discontentReason = string.Empty;
        bool discontentBlocked = actor != null
            && work.StaffDiscontentRuntimeService.ShouldBlockWork(
                actor,
                out discontentReason);
        bool conditionWouldTakeOffDuty = ShouldTakeOffDuty();
        bool restProtectionBlocked = PeekRestProtectionBlocked();

        bool canStartByDutyGate = !actorMissing
            && !expeditionBlocked
            && (prioritySuppress
                || (!routineNeedBlocked
                    && (urgentPriorityWork
                        || (!discontentBlocked
                            && (!IsOffDuty || ShouldReturnToWork())
                            && !conditionWouldTakeOffDuty
                            && !restProtectionBlocked))));
        return new WorkDutyStartDiagnostics(
            dutyState,
            actorMissing,
            expeditionBlocked,
            prioritySuppress,
            priorityWork,
            urgentPriorityWork,
            routineNeedBlocked,
            discontentBlocked,
            discontentReason,
            conditionWouldTakeOffDuty,
            restProtectionBlocked,
            canStartByDutyGate);
    }

    private bool PeekRestProtectionBlocked()
    {
        WorkPriorityProfile priorities = work.WorkPriorities;
        CharacterStats stats = GetWorkerStats();
        if (priorities == null
            || !priorities.IsEnabled(BuiltInWorkTypeIds.Rest)
            || stats == null
            || !stats.Stats.TryGetValue(
                CharacterCondition.SLEEP,
                out float sleep))
        {
            return false;
        }

        if (!restProtectionActive)
        {
            return sleep <= work.RestProtectionSleepThreshold;
        }

        bool sleptEnough = sleep >= work.RestProtectionResumeSleepThreshold;
        bool waitedEnough = Now - restProtectionStartedAt
            >= work.MinimumRestProtectionSeconds;
        return !sleptEnough || !waitedEnough;
    }

    public bool CanStartWorkAction()
    {
        CharacterActor actor = work.WorkerActor;
        if (actor != null && actor.IsOnExpedition)
        {
            return false;
        }

        if (work.HasPrioritySuppressTarget)
        {
            if (IsOffDuty)
            {
                SetDutyState(AbilityWork.DutyState.OnDuty);
                actor?.AddActivity(CharacterActivityEvent.Create(
                    CharacterActivityKinds.Duty,
                    CharacterActivityOutcomes.Responded,
                    "비번 중 제압 명령 대응",
                    actionId: "command:suppress"));
            }

            return true;
        }

        if (HasRoutinePhysiologicalNeed())
        {
            return false;
        }

        if (work.HasUrgentPriorityTarget())
        {
            if (IsOffDuty)
            {
                SetDutyState(AbilityWork.DutyState.OnDuty);
                actor?.AddActivity(CharacterActivityEvent.Create(
                    CharacterActivityKinds.Duty,
                    CharacterActivityOutcomes.Responded,
                    "비번 중 우선 작업 대응",
                    actionId: "command:priority-work"));
            }

            return true;
        }

        if (work.StaffDiscontentRuntimeService.ShouldBlockWork(actor, out string discontentReason))
        {
            BeginOffDuty(string.IsNullOrWhiteSpace(discontentReason)
                ? "직원 불만"
                : discontentReason);
            return false;
        }

        if (IsOffDuty)
        {
            if (ShouldReturnToWork())
            {
                SetDutyState(AbilityWork.DutyState.OnDuty);
                actor?.AddActivity(CharacterActivityEvent.Create(
                    CharacterActivityKinds.Duty,
                    CharacterActivityOutcomes.Returned,
                    "근무 복귀",
                    reasonCode: "condition-recovered",
                    sentiment: 0.3f));
                return true;
            }

            return false;
        }

        if (ShouldTakeOffDuty())
        {
            BeginOffDuty("컨디션 저하");
            return false;
        }

        if (ShouldUseRestProtection())
        {
            return false;
        }

        return true;
    }

    public bool ShouldTakeOffDuty()
    {
        CharacterActor actor = work.WorkerActor;
        if (actor == null || actor.IsOwner || GetWorkerStats() == null)
        {
            return false;
        }

        float sleep = GetStat(CharacterCondition.SLEEP, 100f);
        float mood = GetStat(CharacterCondition.MOOD, 100f);
        float excretion = GetStat(CharacterCondition.EXCRETION, 100f);
        float hygiene = GetStat(CharacterCondition.HYGIENE, 100f);
        return sleep <= work.OffDutySleepThreshold
            || mood <= work.OffDutyMoodThreshold
            || excretion <= 25f
            || hygiene <= 20f;
    }

    public bool ShouldReturnToWork()
    {
        if (!IsOffDuty)
        {
            return false;
        }

        if (Now - offDutyStartedAt < work.MinimumOffDutySeconds)
        {
            return false;
        }

        float sleep = GetStat(CharacterCondition.SLEEP, 0f);
        float mood = GetStat(CharacterCondition.MOOD, 0f);
        float excretion = GetStat(CharacterCondition.EXCRETION, 0f);
        float hygiene = GetStat(CharacterCondition.HYGIENE, 0f);
        return sleep >= work.ReturnToWorkSleepThreshold
            && mood >= work.ReturnToWorkMoodThreshold
            && excretion >= 55f
            && hygiene >= 45f;
    }

    public void BeginOffDuty(string reason)
    {
        CharacterActor actor = work.WorkerActor;
        if (actor == null || actor.IsOwner)
        {
            return;
        }

        bool wasOffDuty = IsOffDuty;
        work.ReleaseAssignedWorkTarget();
        SetDutyState(AbilityWork.DutyState.OffDuty);
        if (!wasOffDuty)
        {
            actor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Duty,
                CharacterActivityOutcomes.Departed,
                string.IsNullOrWhiteSpace(reason)
                    ? "비번 시작"
                    : $"비번 시작: {reason}",
                reasonCode: reason,
                sentiment: -0.2f));
        }

        if (!wasOffDuty && actor.TryGetAbility(out AbilityShopping shopping))
        {
            shopping.BeginOffDutyVisitCycle();
        }
    }

    public void PrepareForExpedition()
    {
        work.ReleaseAssignedWorkTarget();
        work.ClearPriorityWorkTarget();
        SetDutyState(AbilityWork.DutyState.OnDuty);
        work.WorkerActor?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Duty,
            CharacterActivityOutcomes.Changed,
            "원정 준비: 작업 해제",
            reasonCode: "expedition-preparation"));
    }

    public void SetDutyState(AbilityWork.DutyState nextState)
    {
        if (dutyState == nextState)
        {
            return;
        }

        if (dutyState != AbilityWork.DutyState.OffDuty
            && nextState == AbilityWork.DutyState.OffDuty)
        {
            offDutyStartedAt = Now;
        }

        dutyState = nextState;
        work.MarkFacilityDynamicStateDirty();
        if (!work.isWorking)
        {
            work.WorkerActor?.Brain?.RequestImmediateReplan(
                clearFailures: nextState == AbilityWork.DutyState.OnDuty);
        }
    }

    public void RecoverOffDuty(
        float sleep,
        float mood,
        float fun = 0f,
        float hunger = 0f,
        float excretion = 0f,
        float hygiene = 0f,
        IReadOnlyList<string> activeConditionIds = null)
    {
        CharacterStats stats = GetWorkerStats();
        if (stats == null) return;

        if (sleep != 0f)
        {
            stats.RecoverNeed(
                CharacterCondition.SLEEP,
                sleep,
                CharacterNeedRecoverySource.Rest,
                activeConditionIds);
        }
        if (mood != 0f)
        {
            stats.ApplyMoodFactor(
                "rest:off-duty",
                mood > 0f ? "잠깐 숨을 돌림" : "제대로 쉬지 못함",
                mood,
                90f,
                3);
        }
        if (fun != 0f) stats.ChangesStat(CharacterCondition.FUN, fun);
        if (hunger != 0f)
        {
            stats.RecoverNeed(
                CharacterCondition.HUNGER,
                hunger,
                CharacterNeedRecoverySource.Meal);
        }
        if (excretion != 0f)
        {
            stats.RecoverNeed(
                CharacterCondition.EXCRETION,
                excretion,
                CharacterNeedRecoverySource.Toilet);
        }
        if (hygiene != 0f)
        {
            stats.RecoverNeed(
                CharacterCondition.HYGIENE,
                hygiene,
                CharacterNeedRecoverySource.Hygiene);
        }
    }

    public void ApplyWorkFatigueTick(float elapsedGameSeconds = 1f)
    {
        if (!ApplyWorkNeedDepletion(elapsedGameSeconds)) return;

        CharacterStats stats = GetWorkerStats();
        stats.ApplyMoodFactor(
            "work:fatigue",
            "계속된 작업",
            -work.MoodDrainPerWorkTick,
            90f,
            8);
    }

    internal bool ApplyWorkNeedDepletion(float elapsedGameSeconds)
    {
        CharacterStats stats = GetWorkerStats();
        if (stats == null)
        {
            return false;
        }

        stats.ApplyWorkNeedDepletion(elapsedGameSeconds);
        return true;
    }

    public IEnumerator CheckActionWork(int runId)
    {
        CharacterActor actor = work.WorkerActor;
        string endReason = string.Empty;
        float startedAt = Now;
        float lastFatigueAppliedAt = startedAt;
        float routineShiftSeconds = work.RoutineOperateShiftSeconds
            * work.GetWorkEnvironmentDurationMultiplier(work.AssignedWorkTypeId);
        LastWorkRunCompleted = false;
        while (work.CanContinueWorkRun(runId) && actor != null && actor.Brain != null)
        {
            float currentTime = Now;
            float elapsedWork = Mathf.Max(0f, currentTime - lastFatigueAppliedAt);
            ApplyWorkFatigueTick(elapsedWork);
            lastFatigueAppliedAt = currentTime;
            if (!CanContinueAssignedWork(out string stopReason))
            {
                endReason = stopReason;
                WorkDebugLog.LogEnd(actor, stopReason);
                break;
            }

            if (ShouldInterruptCurrentWork(out string interruptReason))
            {
                endReason = interruptReason;
                WorkDebugLog.LogEnd(actor, interruptReason);
                break;
            }

            if (elapsedWork > 0f)
            {
                work.RecordRoutineApprovedWorkTime(
                    elapsedWork,
                    Mathf.Max(
                        0f,
                        routineShiftSeconds - (currentTime - startedAt)));
            }

            if (ShouldEndRoutineWorkShift(startedAt, routineShiftSeconds, out string routineShiftReason))
            {
                LastWorkRunCompleted = true;
                endReason = routineShiftReason;
                WorkDebugLog.LogEnd(actor, $"근무 교대 · {routineShiftReason}");
                actor.AddActivity(CharacterActivityEvent.Create(
                    CharacterActivityKinds.Duty,
                    CharacterActivityOutcomes.Changed,
                    $"근무 교대: {routineShiftReason}",
                    reasonCode: routineShiftReason));
                work.BeginRoutineWorkCooldown(work.AssignedWorkTypeId);
                break;
            }

            yield return WorkCheckDelay;
        }

        if (!work.IsActiveWorkRun(runId))
        {
            work.ClearActiveWorkCheckRoutine(runId);
            yield break;
        }

        work.isWorking = false;
        if (string.IsNullOrWhiteSpace(endReason))
        {
            const string externalStopReason = "외부 작업 상태 해제";
            WorkDebugLog.LogEnd(actor, externalStopReason);
        }

        work.ClearActiveWorkCheckRoutine(runId);
    }

    public bool ShouldInterruptCurrentWork(out string interruptReason)
    {
        interruptReason = string.Empty;
        if (work.HasPrioritySuppressTarget)
        {
            interruptReason = "우선 제압 명령";
            return true;
        }

        BuildableObject priorityTarget = work.PriorityWorkTarget;
        if (priorityTarget != null && priorityTarget != work.assignedShop)
        {
            interruptReason = "우선 작업 명령";
            return true;
        }

        float hunger = GetStat(CharacterCondition.HUNGER, 100f);
        if (hunger <= work.HungerWorkInterruptThreshold)
        {
            RememberRoutineNeedInterruption(CharacterCondition.HUNGER);
            interruptReason = "식사 필요";
            return true;
        }

        if (ShouldUseRestProtection())
        {
            RememberRoutineNeedInterruption(CharacterCondition.SLEEP);
            interruptReason = "휴식 필요";
            return true;
        }

        if (ShouldInterruptForRoutineNeed(
                CharacterCondition.SLEEP,
                "휴식 필요",
                out interruptReason)
            || ShouldInterruptForRoutineNeed(
                CharacterCondition.THIRST,
                "음수 필요",
                out interruptReason)
            || ShouldInterruptForRoutineNeed(
                CharacterCondition.EXCRETION,
                "배변 필요",
                out interruptReason)
            || ShouldInterruptForRoutineNeed(
                CharacterCondition.HYGIENE,
                "위생 필요",
                out interruptReason))
        {
            return true;
        }

        if (work.HasUrgentPriorityTarget())
        {
            return false;
        }

        if (ShouldInterruptForRoutineNeed(
                CharacterCondition.FUN,
                "여가 필요",
                out interruptReason))
        {
            return true;
        }

        if (CharacterMoodImpulseUtility.GetMood01(work.WorkerActor) <= 0.18f)
        {
            interruptReason = "기분이 바닥나 일을 내팽개침";
            return true;
        }

        if (ShouldTakeOffDuty())
        {
            BeginOffDuty("근무 피로 누적");
            interruptReason = "근무 피로 누적";
            return true;
        }

        return false;
    }

    internal bool HasRoutinePhysiologicalNeed()
    {
        if (!routineNeedBlockingWorkStart.HasValue)
        {
            return false;
        }

        CharacterCondition condition = routineNeedBlockingWorkStart.Value;
        if (!CharacterNeedAiThresholds.IsSatisfied(work.WorkerActor, condition))
        {
            return true;
        }

        routineNeedBlockingWorkStart = null;
        return false;
    }

    private bool ShouldInterruptForRoutineNeed(
        CharacterCondition condition,
        string reason,
        out string interruptReason)
    {
        interruptReason = string.Empty;
        if (CharacterNeedAiThresholds.GetRoutineUtility(
                work.WorkerActor,
                condition) <= 0f)
        {
            return false;
        }

        RememberRoutineNeedInterruption(condition);
        interruptReason = reason;
        return true;
    }

    internal void BeginWorkRun()
    {
        LastWorkRunCompleted = false;
        LastWorkRunInterruptedForRoutineNeed = false;
    }

    internal bool NotifyRoutineNeedServiceCompleted()
    {
        if (routineNeedResumeTarget == null
            || !routineNeedResumeWorkTypeId.IsValid
            || HasRoutinePhysiologicalNeed())
        {
            return false;
        }

        BuildableObject resumeTarget = routineNeedResumeTarget;
        WorkTypeId resumeWorkTypeId = routineNeedResumeWorkTypeId;
        ClearRoutineNeedResumeIntent();
        if (resumeTarget == null
            || resumeTarget.isDestroy
            || !work.WorkPriorities.IsEnabled(resumeWorkTypeId))
        {
            return false;
        }

        work.AssignWork(resumeTarget, resumeWorkTypeId);
        AIBrain brain = work.WorkerActor?.Brain;
        brain?.PreferWorkActionOnNextDecision(
            resumeWorkTypeId,
            persistenceSeconds: 30f);
        work.WorkerActor?.AddActivity(CharacterActivityEvent.Work(
            resumeWorkTypeId,
            CharacterActivityOutcomes.Returned,
            "생리 욕구를 해결해 중단했던 작업으로 복귀 준비",
            resumeTarget,
            reasonCode: "routine-need-resume"));
        return true;
    }

    private void RememberRoutineNeedInterruption(CharacterCondition condition)
    {
        routineNeedBlockingWorkStart = condition;
        LastWorkRunInterruptedForRoutineNeed = true;
        BuildableObject target = work.assignedShop;
        WorkTypeId workTypeId = work.AssignedWorkTypeId;
        if (target == null || target.isDestroy || !workTypeId.IsValid)
        {
            return;
        }

        routineNeedResumeTarget = target;
        routineNeedResumeWorkTypeId = workTypeId;
    }

    private void ClearRoutineNeedResumeIntent()
    {
        routineNeedResumeTarget = null;
        routineNeedResumeWorkTypeId = default;
    }

    private bool ShouldEndRoutineWorkShift(float startedAt, float routineShiftSeconds, out string reason)
    {
        reason = string.Empty;
        bool assignedOperate = work.IsAssignedWork(BuiltInWorkTypeIds.Operate);
        bool assignedGuard = work.IsAssignedWork(BuiltInWorkTypeIds.Guard);
        if (!assignedOperate && !assignedGuard)
        {
            return false;
        }

        if (work.HasPrioritySuppressTarget || work.HasUrgentPriorityTarget())
        {
            return false;
        }

        if (Now - startedAt < Mathf.Max(0.5f, routineShiftSeconds))
        {
            return false;
        }

        reason = assignedGuard
            ? "경비 교대"
            : "운영 교대";
        return true;
    }

    private IGameClock RequireGameClock()
    {
        return work.GameClock
            ?? throw new System.InvalidOperationException(
                $"{nameof(WorkDutyController)} requires an injected {nameof(IGameClock)}.");
    }

    public bool CanContinueAssignedWork(out string stopReason)
    {
        stopReason = string.Empty;

        if (work.HasPrioritySuppressTarget)
        {
            return true;
        }

        BuildableObject priorityTarget = work.PriorityWorkTarget;
        if (priorityTarget != null && priorityTarget != work.assignedShop)
        {
            return true;
        }

        BuildableObject target = work.assignedShop;
        if (target == null)
        {
            stopReason = "작업장 없음";
            return false;
        }

        if (target.isDestroy)
        {
            stopReason = "작업장 파괴됨";
            return false;
        }

        if (!target.gameObject.activeInHierarchy)
        {
            stopReason = "work-target-inactive";
            return false;
        }

        FacilityWorkType workType = work.AssignedWorkType;
        if (workType == FacilityWorkType.None)
        {
            stopReason = "작업 종류 없음";
            return false;
        }

        if (!work.TryGetAssignedWorkDefinition(out WorkTypeDefinition definition))
        {
            stopReason = "알 수 없는 작업 종류";
            return false;
        }

        if (!work.WorkPriorities.IsEnabled(definition.WorkTypeId))
        {
            stopReason = $"{definition.DisplayName} 우선순위 꺼짐";
            return false;
        }

        if (work.WorkPolicyRegistry != null
            && !work.WorkPolicyRegistry.IsAvailable(
                definition.WorkTypeId,
                work.WorkerActor,
                target,
                out stopReason))
        {
            return false;
        }

        // Construction sites own a parallel-worker contract. Asking the base
        // BuildableObject assignment contract reports "unsupported work" and
        // makes the decision pipeline interrupt a valid construction action on
        // every scheduler pass.
        if (target is ConstructionSite constructionSite)
        {
            return constructionSite.CanAssignWorker(
                work.WorkerActor?.BuildingVisitor,
                out stopReason);
        }

        if (!target.CanAssignWork(definition.WorkTypeId, out stopReason))
        {
            return false;
        }

        return true;
    }

    private void EnsureStatAtLeast(CharacterCondition condition, float value)
    {
        CharacterStats stats = GetWorkerStats();
        if (stats == null) return;

        if (!stats.Stats.TryGetValue(condition, out float current) || current < value)
        {
            stats.Stats[condition] = value;
        }
    }

    private float GetStat(CharacterCondition condition, float defaultValue)
    {
        CharacterStats stats = GetWorkerStats();
        if (stats == null)
        {
            return defaultValue;
        }

        return stats.Stats.TryGetValue(condition, out float value)
            ? value
            : defaultValue;
    }

    private CharacterStats GetWorkerStats()
    {
        return work.WorkerActor != null ? work.WorkerActor.Stats : null;
    }
}
