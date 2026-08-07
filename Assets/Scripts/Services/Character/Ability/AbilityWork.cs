using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public class AbilityWork : CharacterAbility
{
    private static readonly IFloatingIconFeedbackService FallbackFloatingIconFeedbackService =
        new AbilityWorkNoopFloatingIconFeedbackService();

    public enum DutyState
    {
        OnDuty,
        OffDuty
    }

    public BuildableObject assignedShop;
    public bool isWorking;

    [SerializeField] private WorkPriorityProfile workPriorities = WorkPriorityProfile.CreateDefault();
    [SerializeField] private float restProtectionSleepThreshold = 1f;
    [SerializeField] private float restProtectionResumeSleepThreshold = 35f;
    [SerializeField, Min(0f)] private float minimumRestProtectionSeconds = 6f;
    [SerializeField] private float restRecoveryOnWait = 15f;
    [SerializeField] private float offDutySleepThreshold = 25f;
    [SerializeField] private float returnToWorkSleepThreshold = 55f;
    [SerializeField] private float hungerWorkInterruptThreshold = 35f;
    [SerializeField] private float offDutyMoodThreshold = 25f;
    [SerializeField] private float returnToWorkMoodThreshold = 45f;
    [SerializeField] private float minimumOffDutySeconds = 8f;
    [SerializeField] private float sleepDrainPerWorkTick = 3f;
    [SerializeField] private float moodDrainPerWorkTick = 1f;
    [SerializeField] private float suppressBaseDamage = 18f;
    [SerializeField] private float suppressAttackInterval = 0.55f;
    [SerializeField, Min(0.5f)] private float routineOperateShiftSeconds = 45f;
    [SerializeField, Min(0f)] private float routineOperateCooldownSeconds = 4f;

    private AbilitySchedule schedule;
    private FacilityWorkType assignedWorkType = FacilityWorkType.None;
    private WorkTargetSelector targetSelector;
    private WorkTaskExecutor taskExecutor;
    private WorkDutyController dutyController;
    private WorkCommandHandler commandHandler;
    private IBlueprintResearchWorkService blueprintResearchWorkService;
    private IStaffDiscontentRuntimeService staffDiscontentRuntimeService;
    private IFloatingIconFeedbackService floatingIconFeedbackService;
    private IWorkGridResolver workGridResolver;
    private IFacilityCandidateCache facilityCandidateCache;
    private IRoomEnvironmentQuery roomEnvironmentQuery;
    private IExteriorZoneQuery exteriorZoneQuery;
    private IWorkExecutionHandlerRegistry workExecutionHandlerRegistry;
    private IWorkPolicyRegistry workPolicyRegistry;
    private ICharacterNeedDefinitionCatalog needDefinitionCatalog;
    private IWorkOrderRuntime workOrderRuntime;
    private IPaidFacilityContractRuntime paidFacilityContracts;
    private IEnvironmentWorkPolicy environmentWorkPolicy;
    private IDungeonDebugRuleQuery debugRules;
    private ICharacterEnvironmentWorkContext characterEnvironment;
    private IEnvironmentalWorkwearCommand environmentalWorkwearCommands;
    private IWorkAmountCalculator workAmountCalculator;
    private ICaptiveLaborQuery captiveLaborQuery;
    private IGameClock gameClock;
    private IDefenseEngagementRuntime defenseEngagementRuntime;
    private IRoomEnvironmentExperienceService roomEnvironmentExperienceService;
    private bool isScheduleBound;
    private float routineOperateCooldownUntil;
    private Coroutine activeWorkRoutine;
    private Coroutine activeWorkCheckRoutine;
    private int activeWorkRunId;

    public BuildableObject PriorityWorkTarget => CommandHandler.PriorityWorkTarget;
    public CharacterActor PrioritySuppressActor => CommandHandler.PrioritySuppressActor;
    public bool HasPrioritySuppressTarget => CommandHandler.HasPrioritySuppressTarget;
    internal FacilityWorkType PriorityWorkType => CommandHandler.PriorityWorkType;
    public WorkTypeId PriorityWorkTypeId => CommandHandler.PriorityWorkTypeId;
    public WorkPriorityProfile WorkPriorities => workPriorities ??= WorkPriorityProfile.CreateDefault();
    internal FacilityWorkType AssignedWorkType => assignedWorkType;
    public WorkTypeId AssignedWorkTypeId => TryGetAssignedWorkDefinition(out WorkTypeDefinition definition)
        ? definition.WorkTypeId
        : default;
    internal string AssignedWorkDisplayName => TryGetAssignedWorkDefinition(out WorkTypeDefinition definition)
        ? definition.DisplayName
        : WorkTaskCatalog.GetLegacyDisplayName(assignedWorkType);
    public float RestRecoveryOnWait => restRecoveryOnWait;
    public DutyState CurrentDutyState => DutyController.CurrentState;
    public bool IsOffDuty => DutyController.IsOffDuty;
    public WorkTargetCandidate LastRejectedWorkCandidate => TargetSelector.LastRejectedCandidate;
    public bool HasExteriorZoneQuery => exteriorZoneQuery != null;

    public CharacterActor WorkerActor => actor;
    public AbilityMove WorkerMove => move;
    public Grid CachedGrid => grid;
    internal IBlueprintResearchWorkService BlueprintResearchWorkService =>
        blueprintResearchWorkService
        ?? throw MissingDependency(nameof(IBlueprintResearchWorkService));
    internal IStaffDiscontentRuntimeService StaffDiscontentRuntimeService =>
        staffDiscontentRuntimeService
        ?? throw MissingDependency(nameof(IStaffDiscontentRuntimeService));
    internal IFloatingIconFeedbackService FloatingIconFeedbackService => floatingIconFeedbackService
        ?? FallbackFloatingIconFeedbackService;
    internal IWorkGridResolver WorkGridResolver => workGridResolver
        ?? throw MissingDependency(nameof(IWorkGridResolver));
    internal IFacilityCandidateCache FacilityCandidateCacheService =>
        facilityCandidateCache
        ?? throw MissingDependency(nameof(IFacilityCandidateCache));
    internal IRoomEnvironmentQuery RoomEnvironmentQuery => roomEnvironmentQuery;
    internal IExteriorZoneQuery ExteriorZoneQuery => exteriorZoneQuery;
    internal IWorkExecutionHandlerRegistry WorkExecutionHandlerRegistry =>
        workExecutionHandlerRegistry;
    internal IWorkPolicyRegistry WorkPolicyRegistry => workPolicyRegistry;
    internal IWorkOrderRuntime WorkOrderRuntime => workOrderRuntime;
    internal IGameClock GameClock => gameClock;

    private InvalidOperationException MissingDependency(string dependencyName)
    {
        return new InvalidOperationException(
            $"{nameof(AbilityWork)} requires {dependencyName} injection before use.");
    }

    public void SeedDecisionContext(
        in CharacterAiDecisionContext context)
    {
        TargetSelector.SeedDecisionContext(actor, in context);
    }

    internal float GetWorkEnvironmentDurationMultiplier(WorkTypeId workTypeId)
    {
        return workTypeId.IsValid
            ? roomEnvironmentQuery?.GetWorkDurationMultiplier(assignedShop, workTypeId) ?? 1f
            : 1f;
    }

    internal bool TryGetAssignedWorkDefinition(out WorkTypeDefinition definition)
    {
        definition = null;
        return assignedWorkType != FacilityWorkType.None
            && FacilityWorkTypeMap.TryGet(assignedWorkType, out definition);
    }

    public bool IsAssignedWork(WorkTypeId workTypeId)
    {
        return workTypeId.IsValid && AssignedWorkTypeId == workTypeId;
    }

    public bool IsPriorityWork(WorkTypeId workTypeId)
    {
        return workTypeId.IsValid && PriorityWorkTypeId == workTypeId;
    }

    internal float RestProtectionSleepThreshold =>
        ResolveNeedResponse(
            CharacterCondition.SLEEP,
            restProtectionSleepThreshold).emergencyStart;
    internal float RestProtectionResumeSleepThreshold =>
        ResolveNeedResponse(
            CharacterCondition.SLEEP,
            restProtectionResumeSleepThreshold).resumeTarget;
    internal float MinimumRestProtectionSeconds => minimumRestProtectionSeconds;
    internal float OffDutySleepThreshold =>
        ResolveNeedResponse(
            CharacterCondition.SLEEP,
            offDutySleepThreshold).emergencyStart;
    internal float ReturnToWorkSleepThreshold =>
        ResolveNeedResponse(
            CharacterCondition.SLEEP,
            returnToWorkSleepThreshold).resumeTarget;
    internal float HungerWorkInterruptThreshold =>
        ResolveNeedResponse(
            CharacterCondition.HUNGER,
            hungerWorkInterruptThreshold).emergencyStart;
    internal float OffDutyMoodThreshold => offDutyMoodThreshold;
    internal float ReturnToWorkMoodThreshold => returnToWorkMoodThreshold;
    internal float MinimumOffDutySeconds => minimumOffDutySeconds;
    internal float SleepDrainPerWorkTick => sleepDrainPerWorkTick;
    internal float MoodDrainPerWorkTick => moodDrainPerWorkTick;
    internal float SuppressBaseDamage => suppressBaseDamage;
    internal float SuppressAttackInterval => suppressAttackInterval;
    internal float RoutineOperateShiftSeconds => routineOperateShiftSeconds;
    internal bool LastWorkRunCompleted => DutyController.LastWorkRunCompleted;

    private CharacterNeedResponseProfile ResolveNeedResponse(
        CharacterCondition condition,
        float fallback)
    {
        if (WorkerActor?.Stats != null)
        {
            return WorkerActor.Stats.GetNeedResponse(condition);
        }

        return new CharacterNeedResponseProfile(
            fallback,
            fallback,
            fallback);
    }

    private WorkTargetSelector TargetSelector
    {
        get
        {
            EnsureWorkModules();
            return targetSelector;
        }
    }

    private WorkTaskExecutor TaskExecutor
    {
        get
        {
            EnsureWorkModules();
            return taskExecutor;
        }
    }

    private WorkDutyController DutyController
    {
        get
        {
            EnsureWorkModules();
            return dutyController;
        }
    }

    private WorkCommandHandler CommandHandler
    {
        get
        {
            EnsureWorkModules();
            return commandHandler;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        EnsureWorkModules();
        TryBindScheduleEvents();
    }

    [Inject]
    public void ConstructAbilityWork(
        IBlueprintResearchWorkService blueprintResearchWorkService,
        IStaffDiscontentRuntimeService staffDiscontentRuntimeService,
        IFloatingIconFeedbackService floatingIconFeedbackService,
        IWorkGridResolver workGridResolver,
        IFacilityCandidateCache facilityCandidateCache,
        IRoomEnvironmentQuery roomEnvironmentQuery,
        IExteriorZoneQuery exteriorZoneQuery,
        IWorkExecutionHandlerRegistry workExecutionHandlerRegistry,
        IWorkPolicyRegistry workPolicyRegistry,
        IWorkOrderRuntime workOrderRuntime,
        IWorkAmountCalculator workAmountCalculator,
        ICaptiveLaborQuery captiveLaborQuery,
        IGameClock gameClock,
        IDefenseEngagementRuntime defenseEngagementRuntime,
        IRoomEnvironmentExperienceService roomEnvironmentExperienceService,
        IPaidFacilityContractRuntime paidFacilityContracts,
        IEnvironmentWorkPolicy environmentWorkPolicy,
        ICharacterEnvironmentWorkContext characterEnvironment,
        IEnvironmentalWorkwearCommand environmentalWorkwearCommands,
        ICharacterNeedDefinitionCatalog needDefinitionCatalog,
        IDungeonDebugRuleQuery debugRules)
    {
        this.blueprintResearchWorkService = blueprintResearchWorkService
            ?? throw new ArgumentNullException(nameof(blueprintResearchWorkService));
        this.staffDiscontentRuntimeService = staffDiscontentRuntimeService
            ?? throw new ArgumentNullException(nameof(staffDiscontentRuntimeService));
        this.floatingIconFeedbackService = floatingIconFeedbackService
            ?? throw new ArgumentNullException(nameof(floatingIconFeedbackService));
        this.workGridResolver = workGridResolver
            ?? throw new ArgumentNullException(nameof(workGridResolver));
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.roomEnvironmentQuery = roomEnvironmentQuery;
        this.exteriorZoneQuery = exteriorZoneQuery;
        this.workExecutionHandlerRegistry = workExecutionHandlerRegistry;
        this.workPolicyRegistry = workPolicyRegistry;
        this.workOrderRuntime = workOrderRuntime;
        this.workAmountCalculator = workAmountCalculator;
        this.captiveLaborQuery = captiveLaborQuery;
        this.gameClock = gameClock;
        this.defenseEngagementRuntime = defenseEngagementRuntime;
        this.roomEnvironmentExperienceService = roomEnvironmentExperienceService;
        this.paidFacilityContracts = paidFacilityContracts;
        this.environmentWorkPolicy = environmentWorkPolicy;
        this.characterEnvironment = characterEnvironment
            ?? throw new ArgumentNullException(nameof(characterEnvironment));
        this.environmentalWorkwearCommands = environmentalWorkwearCommands
            ?? throw new ArgumentNullException(nameof(environmentalWorkwearCommands));
        this.needDefinitionCatalog = needDefinitionCatalog
            ?? throw new ArgumentNullException(nameof(needDefinitionCatalog));
        this.debugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
        targetSelector = null;
        taskExecutor = null;
        dutyController = null;
        commandHandler = null;
    }

    public override void Initializtion(CharacterSO data)
    {
        base.Initializtion(data);
        EnsureWorkModules();
        TryBindScheduleEvents();
        workPriorities = data != null && data.defaultWorkPriorities != null
            ? data.defaultWorkPriorities.Clone()
            : WorkPriorityProfile.CreateDefault();
        if (data != null && data.role == CharacterRole.Owner)
        {
            ApplyOwnerPreferredWorkTypes(data.OwnerPreferredWorkTypeIds);
        }

        DutyController.InitializeWorkerCondition(data);
        TryAssignShop();
    }

    public void EnsureWorkReferences()
    {
        CacheCommonReferences();
    }

    private void ApplyOwnerPreferredWorkTypes(IEnumerable<WorkTypeId> preferredTypes)
    {
        foreach (WorkTypeId workTypeId in preferredTypes ?? Array.Empty<WorkTypeId>())
        {
            workPriorities.ApplyPreferredTypes(workTypeId);
        }
    }

    public bool TryAssignShop(GridPathSearchResult searchResult = null)
    {
        return TryAssignAnyWork(searchResult);
    }

    public bool TryAssignAnyWork(GridPathSearchResult searchResult = null)
    {
        return TargetSelector.TryAssignAnyWork(searchResult);
    }

    public bool TryAssignWork(WorkTypeId requestedWorkTypeId, GridPathSearchResult searchResult = null)
    {
        return TargetSelector.TryAssignWork(searchResult, requestedWorkTypeId);
    }

    public bool TryGetBestAnyWorkCandidate(
        GridPathSearchResult searchResult,
        out WorkTargetCandidate candidate)
    {
        bool found = TargetSelector.TryGetBestAnyCandidate(searchResult, out candidate);
        if (!found && !isWorking)
        {
            AssignWork(null, FacilityWorkType.None);
        }

        return found;
    }

    public bool TryGetBestWorkCandidate(
        WorkTypeId requestedWorkTypeId,
        GridPathSearchResult searchResult,
        out WorkTargetCandidate candidate)
    {
        bool found = TargetSelector.TryGetBestCandidate(requestedWorkTypeId, searchResult, out candidate);
        if (!found && !isWorking)
        {
            AssignWork(null, FacilityWorkType.None);
        }

        return found;
    }

    public float GetAnyWorkUtilityScore(GridPathSearchResult searchResult)
    {
        return TargetSelector.GetAnyUtilityScore(searchResult);
    }

    public float GetWorkUtilityScore(WorkTypeId requestedWorkTypeId, GridPathSearchResult searchResult)
    {
        return TargetSelector.GetUtilityScore(requestedWorkTypeId, searchResult);
    }

    public bool TryGetLastRejectedWorkCandidate(out WorkTargetCandidate candidate)
    {
        candidate = TargetSelector.LastRejectedCandidate;
        return candidate.Building != null
            && !candidate.IsValid
            && candidate.FailureKind != AIActionFailureKind.None;
    }

    public void StartAnyWork(BuildableObject preferredTarget = null)
    {
        StartWorkingWithLegacyType(FacilityWorkType.None, preferredTarget);
    }

    private void StartWorkingWithLegacyType(
        FacilityWorkType requestedWorkType,
        BuildableObject preferredTarget)
    {
        WorkerMove?.CancelActiveMovement();

        if (CanExecuteSuppressCommand(requestedWorkType))
        {
            StartCoroutine(CommandHandler.SuppressPriorityTarget());
            return;
        }

        if (isWorking || activeWorkRoutine != null || activeWorkCheckRoutine != null)
        {
            StopAssignedWork(null);
        }

        bool assigned = preferredTarget != null
            ? TryAssignWorkTargetWithLegacyType(preferredTarget, requestedWorkType)
            : TryAssignConfiguredWork(requestedWorkType);
        if (!assigned || actor == null || actor.Brain == null)
        {
            if (actor != null && actor.Brain != null)
            {
                actor.AddActivity(CharacterActivityEvent.Work(
                    requestedWorkType,
                    CharacterActivityOutcomes.Failed,
                    "작업 실패: 작업장 없음",
                    preferredTarget,
                    reasonCode: "no-workplace",
                    bubbleEligible: true));
                actor.Brain.isBestActionEnd = true;
            }
            return;
        }

        int runId = BeginWorkRun();
        activeWorkRoutine = StartCoroutine(TaskExecutor.Work(runId));
    }

    public void StartWorking(
        WorkTypeId requestedWorkTypeId,
        BuildableObject preferredTarget = null)
    {
        FacilityWorkType requestedWorkType = WorkTypeCatalog.TryGet(
                requestedWorkTypeId,
                out WorkTypeDefinition definition)
            ? FacilityWorkTypeMap.GetRequired(definition)
            : FacilityWorkType.None;
        StartWorkingWithLegacyType(requestedWorkType, preferredTarget);
    }

    private bool TryAssignWorkTargetWithLegacyType(
        BuildableObject target,
        FacilityWorkType requestedWorkType,
        GridPathSearchResult searchResult = null)
    {
        bool forced = requestedWorkType != FacilityWorkType.None;
        if (TargetSelector.TryEvaluateWorkTarget(target, searchResult, requestedWorkType, forced, out WorkTargetCandidate candidate))
        {
            AssignWork(target, candidate.WorkTypeId);
            return true;
        }

        return false;
    }

    public bool TryAssignWorkTarget(
        BuildableObject target,
        WorkTypeId requestedWorkTypeId,
        GridPathSearchResult searchResult = null)
    {
        FacilityWorkType requestedWorkType = WorkTypeCatalog.TryGet(
                requestedWorkTypeId,
                out WorkTypeDefinition definition)
            ? FacilityWorkTypeMap.GetRequired(definition)
            : FacilityWorkType.None;
        return TryAssignWorkTargetWithLegacyType(target, requestedWorkType, searchResult);
    }

    public bool TrySetPriorityWorkTarget(BuildableObject building, out string errorMessage)
    {
        return CommandHandler.TrySetPriorityWorkTarget(building, out errorMessage);
    }

    public bool TrySetPriorityWorkTarget(
        BuildableObject building,
        WorkTypeId preferredWorkTypeId,
        GridPathSearchResult searchResult,
        out string errorMessage)
    {
        return CommandHandler.TrySetPriorityWorkTarget(
            building,
            preferredWorkTypeId,
            searchResult,
            out errorMessage);
    }

    public bool TrySetPrioritySuppressTarget(
        CharacterActor target,
        GridPathSearchResult searchResult,
        out string errorMessage)
    {
        return CommandHandler.TrySetPrioritySuppressTarget(target, searchResult, out errorMessage);
    }

    public bool TryGetPrioritySuppressDestination(GridPathSearchResult searchResult, out BuildableObject destination)
    {
        return CommandHandler.TryGetPrioritySuppressDestination(searchResult, out destination);
    }

    public void ClearPriorityWorkTarget()
    {
        CommandHandler.ClearPriorityWorkTarget();
    }

    public void SetWorkPriority(WorkTypeId workTypeId, WorkPriorityLevel priority)
    {
        SetWorkPriority(workTypeId, priority, null);
    }

    public void SetWorkPriority(
        WorkTypeId workTypeId,
        WorkPriorityLevel priority,
        GridPathSearchResult searchResult)
    {
        if (!WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition))
        {
            throw new ArgumentException(
                $"No work type definition is registered for '{workTypeId}'.",
                nameof(workTypeId));
        }

        SetWorkPriority(definition, priority, searchResult);
    }

    private void SetWorkPriority(
        WorkTypeDefinition definition,
        WorkPriorityLevel priority,
        GridPathSearchResult searchResult)
    {
        workPriorities ??= WorkPriorityProfile.CreateDefault();
        WorkTypeId workTypeId = definition.WorkTypeId;
        FacilityWorkType workType = FacilityWorkTypeMap.GetRequired(definition);
        WorkPriorityLevel previousPriority = workPriorities.GetPriority(workTypeId);
        workPriorities.SetPriority(workTypeId, priority);

        WorkTypeId assignedWorkTypeId = AssignedWorkTypeId;
        bool currentWorkDisabled = assignedWorkTypeId.IsValid
            && !workPriorities.IsEnabled(assignedWorkTypeId);
        if (currentWorkDisabled)
        {
            StopAssignedWork($"{WorkTaskCatalog.GetLegacyDisplayName(assignedWorkType)} 우선순위 꺼짐");
        }
        else if (!isWorking)
        {
            AssignWork(null, FacilityWorkType.None);
            MarkFacilityDynamicStateDirty();
            actor?.Brain?.RequestImmediateReplan(clearFailures: true);
        }
        else
        {
            MarkFacilityDynamicStateDirty();
            AIBrain brain = actor?.Brain;
            brain?.InvalidateQueuedActionForNextDecision();
            if (IsPriorityRaised(previousPriority, priority)
                && ShouldReplanForRaisedPriority(definition, brain, searchResult))
            {
                string reason = $"{WorkTaskCatalog.GetLegacyDisplayName(workType)} 우선순위 상향";
                if (!brain.StopCurrentActionForReplan(reason))
                {
                    StopAssignedWork(reason, false);
                }

                brain.RequestImmediateReplan(clearFailures: true);
            }
        }

        actor?.AddActivity(CharacterActivityEvent.Work(
            workType,
            CharacterActivityOutcomes.Changed,
            $"{WorkTaskCatalog.GetLegacyDisplayName(workType)} 우선순위: {priority.ToDisplayText()}",
            reasonCode: $"priority:{(int)priority}",
            value: (int)priority));
    }

    private bool ShouldReplanForRaisedPriority(
        WorkTypeDefinition requestedDefinition,
        AIBrain brain,
        GridPathSearchResult searchResult)
    {
        FacilityWorkType workType = FacilityWorkTypeMap.GetRequired(requestedDefinition);
        WorkTypeId workTypeId = requestedDefinition.WorkTypeId;
        if (brain == null
            || actor == null
            || workType == FacilityWorkType.None
            || workTypeId == AssignedWorkTypeId)
        {
            return false;
        }

        WorkPriorityLevel requestedPriority = workPriorities.GetPriority(workTypeId);
        WorkTypeId assignedWorkTypeId = AssignedWorkTypeId;
        WorkPriorityLevel currentPriority = assignedWorkTypeId.IsValid
            ? workPriorities.GetPriority(assignedWorkTypeId)
            : WorkPriorityLevel.Off;
        if (requestedPriority == WorkPriorityLevel.Off
            || (currentPriority != WorkPriorityLevel.Off && requestedPriority > currentPriority))
        {
            return false;
        }

        if (!CanStartWorkAction(workTypeId, searchResult)
            || !TryGetBestWorkCandidate(workTypeId, searchResult, out WorkTargetCandidate requestedCandidate)
            || !TryGetBestAnyWorkCandidate(searchResult, out WorkTargetCandidate bestCandidate))
        {
            return false;
        }

        return bestCandidate.WorkTypeId == workTypeId
            && bestCandidate.Building == requestedCandidate.Building;
    }

    private static bool IsPriorityRaised(
        WorkPriorityLevel previousPriority,
        WorkPriorityLevel currentPriority)
    {
        return currentPriority != WorkPriorityLevel.Off
            && (previousPriority == WorkPriorityLevel.Off || currentPriority < previousPriority);
    }

    public bool ShouldUseRestProtection()
    {
        return DutyController.ShouldUseRestProtection();
    }

    public bool CanStartWorkAction()
    {
        return DutyController.CanStartWorkAction();
    }

    public bool CanStartAnyWorkAction(GridPathSearchResult searchResult)
    {
        return CanStartWorkAction()
            || TargetSelector.HasUrgentAnyAvailableWork(searchResult);
    }

    public bool CanStartWorkAction(WorkTypeId requestedWorkTypeId, GridPathSearchResult searchResult)
    {
        return CanStartWorkAction()
            || TargetSelector.HasUrgentAvailableWork(searchResult, requestedWorkTypeId);
    }

    private bool TryAssignConfiguredWork(
        FacilityWorkType requestedWorkType,
        GridPathSearchResult searchResult = null)
    {
        if (requestedWorkType == FacilityWorkType.None)
        {
            return TryAssignAnyWork(searchResult);
        }

        return FacilityWorkTypeMap.TryGet(
                requestedWorkType,
                out WorkTypeDefinition definition)
            && TryAssignWork(definition.WorkTypeId, searchResult);
    }

    public bool CanContinueCurrentWork(out string stopReason)
    {
        return DutyController.CanContinueAssignedWork(out stopReason);
    }

    public bool ShouldInterruptCurrentWork(out string interruptReason)
    {
        return DutyController.ShouldInterruptCurrentWork(out interruptReason);
    }

    public bool ShouldThrottleRoutineWork(WorkTypeId workTypeId)
    {
        return workTypeId == BuiltInWorkTypeIds.Operate
            && GameClock.Time < routineOperateCooldownUntil
            && !HasUrgentPriorityTarget()
            && PriorityWorkTarget == null
            && !HasPrioritySuppressTarget;
    }

    public void BeginRoutineWorkCooldown(WorkTypeId workTypeId)
    {
        if (workTypeId != BuiltInWorkTypeIds.Operate || routineOperateCooldownSeconds <= 0f)
        {
            return;
        }

        routineOperateCooldownUntil = GameClock.Time + routineOperateCooldownSeconds;
        if (!isWorking)
        {
            actor?.Brain?.RequestImmediateReplan(clearFailures: true);
        }
    }

    public bool ShouldTakeOffDuty()
    {
        return DutyController.ShouldTakeOffDuty();
    }

    public bool ShouldReturnToWork()
    {
        return DutyController.ShouldReturnToWork();
    }

    public void BeginOffDuty(string reason)
    {
        DutyController.BeginOffDuty(reason);
    }

    public void PrepareForExpedition()
    {
        DutyController.PrepareForExpedition();
    }

    public void SetDutyState(DutyState nextState)
    {
        DutyController.SetDutyState(nextState);
    }

    public void RecoverOffDuty(
        float sleep,
        float mood,
        float fun = 0f,
        float hunger = 0f,
        float excretion = 0f,
        float hygiene = 0f)
    {
        DutyController.RecoverOffDuty(sleep, mood, fun, hunger, excretion, hygiene);
    }

    public void ApplyWorkFatigueTick()
    {
        DutyController.ApplyWorkFatigueTick();
    }

    public IEnumerator CheckActionWork()
    {
        return DutyController.CheckActionWork(activeWorkRunId);
    }

    public void CheckSchedule(Schedule schedule)
    {
        if (isWorking && schedule != Schedule.WORK)
        {
            StopAssignedWork("스케줄 변경");
        }
    }

    internal void AssignWork(BuildableObject building, FacilityWorkType workType)
    {
        assignedShop = building;
        assignedWorkType = workType;
    }

    internal void AssignWork(BuildableObject building, WorkTypeId workTypeId)
    {
        AssignWork(
            building,
            workTypeId.IsValid
                ? FacilityWorkTypeMap.GetRequired(workTypeId)
                : FacilityWorkType.None);
    }

    internal void ReleaseAssignedWorkTarget()
    {
        StopAssignedWork(null);
    }

    internal void StopAssignedWork(string reason)
    {
        StopAssignedWork(reason, true);
    }

    internal void StopAssignedWorkFromAi(string reason)
    {
        StopAssignedWork(reason, false);
    }

    private void StopAssignedWork(string reason, bool requestImmediateReplan)
    {
        FacilityWorkType stoppedWorkType = assignedWorkType;
        BuildableObject stoppedTarget = assignedShop;
        InvalidateActiveWorkRun();
        WorkerMove?.CancelActiveMovement();

        if (assignedShop is IWorkableFacility facility)
        {
            facility.DeallocateWorker(actor?.BuildingVisitor);
        }

        AIAction currentAction = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;
        currentAction?.ReleaseReservation(actor);

        AssignWork(null, FacilityWorkType.None);
        isWorking = false;
        MarkFacilityDynamicStateDirty();
        if (!string.IsNullOrWhiteSpace(reason))
        {
            actor?.AddActivity(CharacterActivityEvent.Work(
                stoppedWorkType,
                CharacterActivityOutcomes.Cancelled,
                $"작업 종료: {reason}",
                stoppedTarget,
                reasonCode: reason));
        }

        if (actor != null && actor.Brain != null)
        {
            actor.Brain.bestAction = null;
            actor.Brain.isBestActionEnd = true;
            if (requestImmediateReplan)
            {
                actor.Brain.RequestImmediateReplan(clearFailures: true);
            }
        }
    }

    internal bool IsActiveWorkRun(int runId)
    {
        return runId == activeWorkRunId;
    }

    internal bool CanContinueWorkRun(int runId)
    {
        return isWorking && IsActiveWorkRun(runId);
    }

    internal Coroutine StartCheckActionWork(int runId)
    {
        if (!IsActiveWorkRun(runId))
        {
            return null;
        }

        StopActiveWorkCheckRoutine();
        activeWorkCheckRoutine = StartCoroutine(DutyController.CheckActionWork(runId));
        return activeWorkCheckRoutine;
    }

    internal void ClearActiveWorkRoutine(int runId)
    {
        if (IsActiveWorkRun(runId))
        {
            activeWorkRoutine = null;
        }
    }

    internal void ClearActiveWorkCheckRoutine(int runId)
    {
        if (IsActiveWorkRun(runId))
        {
            activeWorkCheckRoutine = null;
        }
    }

    internal bool HasUrgentPriorityTarget()
    {
        return CommandHandler.HasUrgentPriorityTarget();
    }

    internal void MarkFacilityDynamicStateDirty()
    {
        FacilityCandidateCacheService.MarkDynamicStateDirty();
    }

    private bool CanExecuteSuppressCommand(FacilityWorkType requestedWorkType)
    {
        return HasPrioritySuppressTarget
            && (requestedWorkType == FacilityWorkType.None || requestedWorkType == FacilityWorkType.Guard);
    }

    private void OnDisable()
    {
        UnbindScheduleEvents();
    }

    private void OnEnable()
    {
        TryBindScheduleEvents();
    }

    private void TryBindScheduleEvents()
    {
        CacheLocalReferences();

        if (abilityCache == null
            || !abilityCache.TryGetAbility(out AbilitySchedule nextSchedule)
            || nextSchedule == null
            || nextSchedule.nowSheduleData == null)
        {
            return;
        }

        if (schedule == nextSchedule && isScheduleBound)
        {
            return;
        }

        UnbindScheduleEvents();
        schedule = nextSchedule;
        schedule.nowSheduleData.OnValueChange += CheckSchedule;
        isScheduleBound = true;
    }

    private void UnbindScheduleEvents()
    {
        if (schedule != null && schedule.nowSheduleData != null && isScheduleBound)
        {
            schedule.nowSheduleData.OnValueChange -= CheckSchedule;
        }

        isScheduleBound = false;
    }

    private void EnsureWorkModules()
    {
        if (needDefinitionCatalog == null)
        {
            return;
        }

        if (targetSelector != null
            && taskExecutor != null
            && dutyController != null
            && commandHandler != null)
        {
            return;
        }

        targetSelector ??= new WorkTargetSelector(
            this,
            workPolicyRegistry,
            captiveLaborQuery,
            environmentWorkPolicy);
        taskExecutor ??= new WorkTaskExecutor(
            new WorkTaskCoreDependencies(
                this,
                targetSelector,
                gameClock,
                debugRules),
            new WorkTaskExecutionDependencies(
                workExecutionHandlerRegistry,
                workOrderRuntime,
                workAmountCalculator,
                paidFacilityContracts),
            new WorkTaskEnvironmentDependencies(
                roomEnvironmentExperienceService,
                characterEnvironment,
                environmentalWorkwearCommands,
                environmentWorkPolicy));
        dutyController ??= new WorkDutyController(
            this,
            needDefinitionCatalog);
        commandHandler ??= new WorkCommandHandler(this, targetSelector, defenseEngagementRuntime);
    }

    private int BeginWorkRun()
    {
        StopActiveWorkRoutines();
        activeWorkRunId++;
        return activeWorkRunId;
    }

    private void InvalidateActiveWorkRun()
    {
        activeWorkRunId++;
        StopActiveWorkRoutines();
    }

    private void StopActiveWorkRoutines()
    {
        StopActiveWorkRoutine();
        StopActiveWorkCheckRoutine();
    }

    private void StopActiveWorkRoutine()
    {
        if (activeWorkRoutine == null)
        {
            return;
        }

        StopCoroutine(activeWorkRoutine);
        activeWorkRoutine = null;
    }

    private void StopActiveWorkCheckRoutine()
    {
        if (activeWorkCheckRoutine == null)
        {
            return;
        }

        StopCoroutine(activeWorkCheckRoutine);
        activeWorkCheckRoutine = null;
    }

    private IEnumerator SuppressPriorityTarget()
    {
        return CommandHandler.SuppressPriorityTarget();
    }

    private bool TryEvaluateWorkTarget(
        BuildableObject building,
        GridPathSearchResult searchResult,
        FacilityWorkType forcedWorkType,
        bool ignorePriority,
        out WorkTargetCandidate bestCandidate)
    {
        return TargetSelector.TryEvaluateWorkTarget(
            building,
            searchResult,
            forcedWorkType,
            ignorePriority,
            out bestCandidate);
    }

    private sealed class AbilityWorkNoopFloatingIconFeedbackService : IFloatingIconFeedbackService
    {
        public bool Show(Component target, Sprite sprite, float maxWorldSize) => false;
    }

}
