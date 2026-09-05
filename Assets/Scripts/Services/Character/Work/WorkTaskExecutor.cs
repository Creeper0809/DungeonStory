using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WorkTaskCoreDependencies
{
    public WorkTaskCoreDependencies(
        AbilityWork work,
        WorkTargetSelector targetSelector,
        IGameClock gameClock,
        IDungeonDebugRuleQuery debugRules)
    {
        Work = work ?? throw new ArgumentNullException(nameof(work));
        TargetSelector = targetSelector ?? throw new ArgumentNullException(nameof(targetSelector));
        GameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        DebugRules = debugRules ?? throw new ArgumentNullException(nameof(debugRules));
    }

    public AbilityWork Work { get; }
    public WorkTargetSelector TargetSelector { get; }
    public IGameClock GameClock { get; }
    public IDungeonDebugRuleQuery DebugRules { get; }
}

public sealed class WorkTaskExecutionDependencies
{
    public WorkTaskExecutionDependencies(
        IWorkExecutionHandlerRegistry executionHandlers,
        IWorkOrderRuntime workOrderRuntime,
        IWorkAmountCalculator workAmountCalculator,
        IPaidFacilityContractRuntime paidFacilityContracts)
    {
        ExecutionHandlers = executionHandlers
            ?? throw new ArgumentNullException(nameof(executionHandlers));
        WorkOrderRuntime = workOrderRuntime
            ?? throw new ArgumentNullException(nameof(workOrderRuntime));
        WorkAmountCalculator = workAmountCalculator
            ?? throw new ArgumentNullException(nameof(workAmountCalculator));
        PaidFacilityContracts = paidFacilityContracts
            ?? throw new ArgumentNullException(nameof(paidFacilityContracts));
    }

    public IWorkExecutionHandlerRegistry ExecutionHandlers { get; }
    public IWorkOrderRuntime WorkOrderRuntime { get; }
    public IWorkAmountCalculator WorkAmountCalculator { get; }
    public IPaidFacilityContractRuntime PaidFacilityContracts { get; }
}

public sealed class WorkTaskEnvironmentDependencies
{
    public WorkTaskEnvironmentDependencies(
        IRoomEnvironmentExperienceService roomEnvironmentExperienceService,
        ICharacterEnvironmentWorkContext characterEnvironment,
        IEnvironmentalWorkwearCommand environmentalWorkwearCommands,
        IEnvironmentWorkPolicy environmentWorkPolicy)
    {
        RoomEnvironmentExperienceService = roomEnvironmentExperienceService
            ?? throw new ArgumentNullException(nameof(roomEnvironmentExperienceService));
        CharacterEnvironment = characterEnvironment
            ?? throw new ArgumentNullException(nameof(characterEnvironment));
        EnvironmentalWorkwearCommands = environmentalWorkwearCommands
            ?? throw new ArgumentNullException(nameof(environmentalWorkwearCommands));
        EnvironmentWorkPolicy = environmentWorkPolicy
            ?? throw new ArgumentNullException(nameof(environmentWorkPolicy));
    }

    public IRoomEnvironmentExperienceService RoomEnvironmentExperienceService { get; }
    public ICharacterEnvironmentWorkContext CharacterEnvironment { get; }
    public IEnvironmentalWorkwearCommand EnvironmentalWorkwearCommands { get; }
    public IEnvironmentWorkPolicy EnvironmentWorkPolicy { get; }
}

public readonly struct EmergencyWorkSuspensionReceipt
{
    public EmergencyWorkSuspensionReceipt(
        WorkTypeId workTypeId,
        string targetBuildingId,
        long alertEpochId,
        bool progressExternallyPersisted,
        float inlineCompletedWork = 0f,
        float inlineRequiredWork = 0f)
    {
        WorkTypeId = workTypeId;
        TargetBuildingId = targetBuildingId?.Trim() ?? string.Empty;
        AlertEpochId = alertEpochId;
        ProgressExternallyPersisted = progressExternallyPersisted;
        InlineCompletedWork = inlineCompletedWork;
        InlineRequiredWork = inlineRequiredWork;
    }

    public WorkTypeId WorkTypeId { get; }
    public string TargetBuildingId { get; }
    public long AlertEpochId { get; }
    public bool ProgressExternallyPersisted { get; }
    public float InlineCompletedWork { get; }
    public float InlineRequiredWork { get; }
    public bool HasInlineProgress => InlineRequiredWork > 0f
        && InlineCompletedWork >= 0f
        && InlineCompletedWork < InlineRequiredWork;
    public bool IsValid => WorkTypeId.IsValid
        && !string.IsNullOrWhiteSpace(TargetBuildingId)
        && AlertEpochId > 0L
        && (ProgressExternallyPersisted || HasInlineProgress);
}

public enum WorkPreWuExitKind
{
    None = 0,
    RoutineNeedInterrupt = 1,
    EmergencySuspension = 2,
    EnvironmentInterrupt = 3,
    WorkOrderLoopConditionRejected = 4,
    WorkOrderApplyRejected = 5,
    TimedLoopConditionRejected = 6,
    PersistentApplyRejected = 7,
    ExecutionHandlerRejected = 8
}

public sealed class WorkTaskExecutor
{
    private const float RestockPickupWaitSeconds = 0.35f;
    private const float BaseAccidentHazardPerApprovedWorkUnit = 0.001f;
    private const float WorkAccidentDamage = 2f;

    private readonly AbilityWork work;
    private readonly WorkTargetSelector targetSelector;
    private readonly IWorkExecutionHandlerRegistry executionHandlers;
    private readonly IWorkOrderRuntime workOrderRuntime;
    private readonly IWorkAmountCalculator workAmountCalculator;
    private readonly IGameClock gameClock;
    private readonly IRoomEnvironmentExperienceService roomEnvironmentExperienceService;
    private readonly IPaidFacilityContractRuntime paidFacilityContracts;
    private readonly ICharacterEnvironmentWorkContext characterEnvironment;
    private readonly IEnvironmentalWorkwearCommand environmentalWorkwearCommands;
    private readonly IEnvironmentWorkPolicy environmentWorkPolicy;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly ICharacterProficiencyCommand proficiencyCommands;
    private readonly ICharacterProficiencyQuery proficiencyQuery;
    private readonly IGameCalendar calendar;
    private readonly ICombatEquipmentRuntime combatEquipmentRuntime;
    private readonly IRandomStream workAccidentRandom;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly ICharacterPerformanceQuery performance;
    private readonly CharacterWorkPerformanceContextResolver performanceContext;
    private readonly IAnatomyHealthRuntime anatomyHealth;
    private readonly ICharacterSpeciesCommand speciesCommands;
    private readonly IEmergencyWorkAccountingService emergencyWorkAccounting;
    private readonly ISettlementLaborAccountingService settlementLaborAccounting;
    private readonly IReservedItemTransferService reservedItemTransfers;
    private readonly ICharacterSettlementStandingQuery settlementStandings;
    private float nextEnvironmentRecheckAt;
    private bool environmentInterrupted;
    private float approvedProficiencyWork;
    private bool proficiencyAwarded;
    private float proficiencyRepetitionMultiplier = 1f;
    private bool workAccidentOccurred;
    private bool workStartedPublished;
    private int currentRunId;
    private string activeEmergencyOperationId = string.Empty;
    private long emergencyAccountingSequence;
    private double actualLaborMilliWuCarry;
    private double projectOutputMilliWuCarry;
    private long latestApprovedLaborMilliWu;
    private bool emergencySuspensionRequested;
    private bool emergencySuspended;
    private long requestedEmergencyEpochId;
    private EmergencyWorkSuspensionReceipt pendingSuspensionReceipt;
    private bool genericProgressActive;
    private WorkTypeId genericProgressWorkTypeId;
    private string genericProgressTargetId = string.Empty;
    private float genericProgressCompletedWork;
    private float genericProgressRequiredWork;
    private bool resumedGenericProgressPending;
    private WorkTypeId resumedGenericWorkTypeId;
    private string resumedGenericTargetId = string.Empty;
    private float resumedGenericCompletedWork;
    private float resumedGenericRequiredWork;
    private string activeRestockOperationId = string.Empty;
    private string activeRestockLeaseId = string.Empty;
    private IRetailRestockOperationOwner activeRestockOwner;
    private IDisposable activeExecutionCancellationResource;
    private WorkPreWuExitKind lastPreWuExitKind;
    private string lastPreWuExitDetail = string.Empty;
    private string lastWorkOrderExecutionDetail = string.Empty;

    internal bool HasActiveGenericProgressForDiagnostics =>
        genericProgressActive;
    internal float GenericCompletedWorkForDiagnostics =>
        genericProgressCompletedWork;
    internal float GenericRequiredWorkForDiagnostics =>
        genericProgressRequiredWork;
    internal bool HasPendingResumedGenericProgressForDiagnostics =>
        resumedGenericProgressPending;
    internal WorkPreWuExitKind LastPreWuExitKindForDiagnostics =>
        lastPreWuExitKind;
    internal string LastPreWuExitDetailForDiagnostics =>
        lastPreWuExitDetail;
    internal string LastWorkOrderExecutionDetailForDiagnostics =>
        lastWorkOrderExecutionDetail;

    public WorkTaskExecutor(
        WorkTaskCoreDependencies core,
        WorkTaskExecutionDependencies execution,
        WorkTaskEnvironmentDependencies environment,
        ICharacterProficiencyCommand proficiencyCommands = null,
        IGameCalendar calendar = null,
        ICharacterProficiencyQuery proficiencyQuery = null,
        ICombatEquipmentRuntime combatEquipmentRuntime = null,
        IRandomStream workAccidentRandom = null,
        CharacterIdentityEventPublisher identityEvents = null,
        ICharacterPerformanceQuery performance = null,
        CharacterWorkPerformanceContextResolver performanceContext = null,
        IAnatomyHealthRuntime anatomyHealth = null,
        ICharacterSpeciesCommand speciesCommands = null,
        IEmergencyWorkAccountingService emergencyWorkAccounting = null,
        ISettlementLaborAccountingService settlementLaborAccounting = null,
        IReservedItemTransferService reservedItemTransfers = null,
        ICharacterSettlementStandingQuery settlementStandings = null)
    {
        core = core ?? throw new ArgumentNullException(nameof(core));
        execution = execution ?? throw new ArgumentNullException(nameof(execution));
        environment = environment ?? throw new ArgumentNullException(nameof(environment));
        work = core.Work;
        targetSelector = core.TargetSelector;
        gameClock = core.GameClock;
        debugRules = core.DebugRules;
        executionHandlers = execution.ExecutionHandlers;
        workOrderRuntime = execution.WorkOrderRuntime;
        workAmountCalculator = execution.WorkAmountCalculator;
        paidFacilityContracts = execution.PaidFacilityContracts;
        roomEnvironmentExperienceService = environment.RoomEnvironmentExperienceService;
        characterEnvironment = environment.CharacterEnvironment;
        environmentalWorkwearCommands = environment.EnvironmentalWorkwearCommands;
        environmentWorkPolicy = environment.EnvironmentWorkPolicy;
        this.proficiencyCommands = proficiencyCommands;
        this.proficiencyQuery = proficiencyQuery;
        this.calendar = calendar;
        this.combatEquipmentRuntime = combatEquipmentRuntime;
        this.workAccidentRandom = workAccidentRandom;
        this.identityEvents = identityEvents;
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.performanceContext = performanceContext
            ?? throw new ArgumentNullException(nameof(performanceContext));
        this.anatomyHealth = anatomyHealth
            ?? throw new ArgumentNullException(nameof(anatomyHealth));
        this.speciesCommands = speciesCommands
            ?? throw new ArgumentNullException(nameof(speciesCommands));
        this.emergencyWorkAccounting = emergencyWorkAccounting;
        this.settlementLaborAccounting = settlementLaborAccounting;
        this.reservedItemTransfers = reservedItemTransfers;
        this.settlementStandings = settlementStandings;
    }

    public IEnumerator Work(int runId)
    {
        ReleaseActiveRestockLease(ItemReservationReleaseReason.Replanned);
        ReleaseActiveExecutionCancellationResource();
        // A new run is an ownership boundary. In normal flow AbilityWork stops
        // and settles the previous coroutine first, but restore/replan can leave
        // the coroutine reference cleared after its ledger operation was already
        // published. Never erase that operation ID without settling it: doing so
        // leaves the worker permanently owned by the old emergency ledger row
        // and the first approved WU of this run fails as WorkerAlreadyActive.
        CompleteEmergencyAccounting("superseded-before-work-run");
        currentRunId = runId;
        activeEmergencyOperationId = string.Empty;
        emergencyAccountingSequence = 0L;
        // Do not discard sub-milli WU remainders when an actor stops and later
        // resumes work. Routine needs create many short operations, and flooring
        // each operation independently makes the settlement ledger drift below
        // the authoritative physical project progress. The carries belong to
        // this executor's cumulative accounting stream, not to one operation.
        latestApprovedLaborMilliWu = 0L;
        emergencySuspensionRequested = false;
        emergencySuspended = false;
        requestedEmergencyEpochId = 0L;
        approvedProficiencyWork = 0f;
        lastPreWuExitKind = WorkPreWuExitKind.None;
        lastPreWuExitDetail = string.Empty;
        lastWorkOrderExecutionDetail = string.Empty;
        proficiencyAwarded = false;
        proficiencyRepetitionMultiplier = 1f;
        workAccidentOccurred = false;
        workStartedPublished = false;
        work.BeginDutyWorkRun();
        CharacterActor actor = work.WorkerActor;
        AIAction currentAction = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;

        work.EnsureWorkReferences();
        AbilityMove move = work.WorkerMove;
        Grid grid = work.WorkGridResolver.ResolveActiveGrid(work, null);
        if (move == null || grid == null)
        {
            WorkDebugLog.LogEnd(actor, "이동 정보 없음");
            actor?.AddActivity(CharacterActivityEvent.Work(
                work.AssignedWorkType,
                CharacterActivityOutcomes.Failed,
                "작업 실패: 이동 정보 없음",
                work.assignedShop,
                reasonCode: "missing-movement",
                bubbleEligible: true));
            work.isWorking = false;
            EndAiAction(actor, currentAction);
            work.ClearActiveWorkRoutine(runId);
            yield break;
        }

        work.isWorking = true;
        environmentInterrupted = false;
        nextEnvironmentRecheckAt = gameClock.Time + 1f;
        if (work.AssignedWorkType == FacilityWorkType.Restock)
        {
            bool restockCompleted = false;
            yield return ExecuteRestockHaulWork(
                runId,
                currentAction,
                move,
                grid,
                success => restockCompleted = success);
            if (work.IsActiveWorkRun(runId))
            {
                if (restockCompleted)
                {
                    FinishWorkRun(actor, currentAction);
                    work.ClearActiveWorkRoutine(runId);
                }
                else
                {
                    AbortWorkRun(
                        runId,
                        actor,
                        currentAction,
                        CharacterAiActionTerminalKind.Failed);
                }
            }
            yield break;
        }

        BuildableObject plannedTarget = work.assignedShop;
        WorkTypeId plannedWorkTypeId = FacilityWorkTypeMap.TryGet(
                work.AssignedWorkType,
                out WorkTypeDefinition plannedWorkDefinition)
            ? plannedWorkDefinition.WorkTypeId
            : default;
        string expectedWorkOrderId = string.Empty;
        bool requiresCommonWorkOrder = RequiresCommonWorkOrderRoute(
            plannedWorkTypeId);
        if (workOrderRuntime != null
            && plannedTarget != null
            && plannedWorkTypeId.IsValid
            && workOrderRuntime.TryGetOrderFor(
                plannedTarget,
                plannedWorkTypeId,
                out WorkOrderProgressState plannedOrder))
        {
            expectedWorkOrderId = plannedOrder.WorkOrderId;
            lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                "planned",
                plannedTarget,
                plannedWorkTypeId,
                expectedWorkOrderId,
                plannedOrder);
        }
        else if (requiresCommonWorkOrder)
        {
            lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                "planned-missing",
                plannedTarget,
                plannedWorkTypeId,
                expectedWorkOrderId,
                null);
            RecordPreWuExit(
                WorkPreWuExitKind.WorkOrderLoopConditionRejected,
                lastWorkOrderExecutionDetail);
            currentAction?.ReleaseReservation(actor);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    "required-work-order-unavailable-before-approach",
                    plannedTarget),
                requestImmediateReplan: false);
            work.isWorking = false;
            work.AssignWork(null, FacilityWorkType.None);
            EndAiAction(
                actor,
                currentAction,
                CharacterAiActionTerminalKind.Failed);
            work.ClearActiveWorkRoutine(runId);
            yield break;
        }

        yield return move.MoveByCurrentBestActionPath();
        if (ShouldAbortWorkRun(runId, actor) || !work.isWorking)
        {
            AbortWorkRun(runId, actor, currentAction);
            yield break;
        }

        BuildableObject assignedTarget = work.assignedShop;
        string assignedTargetName = assignedTarget != null
            ? assignedTarget.name
            : "<missing-work-target>";
        string assignedTargetPersistentId = assignedTarget != null
            ? assignedTarget.RequirePersistentInstanceId().Value
            : string.Empty;
        WorkOrderProgressState currentOrder = null;
        bool approachOrderAvailable = workOrderRuntime != null
            && assignedTarget != null
            && workOrderRuntime.TryGetOrderFor(
                assignedTarget,
                plannedWorkTypeId,
                out currentOrder);
        if (!string.IsNullOrWhiteSpace(expectedWorkOrderId)
            && (assignedTarget != plannedTarget
                || !approachOrderAvailable
                || !string.Equals(
                    currentOrder.WorkOrderId,
                    expectedWorkOrderId,
                    StringComparison.Ordinal)
                || currentOrder.Status == WorkOrderStatus.Completed
                || currentOrder.Status == WorkOrderStatus.Cancelled))
        {
            lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                "approach-revalidation-failed",
                assignedTarget,
                plannedWorkTypeId,
                expectedWorkOrderId,
                currentOrder);
            RecordPreWuExit(
                WorkPreWuExitKind.WorkOrderLoopConditionRejected,
                lastWorkOrderExecutionDetail);
            currentAction?.ReleaseReservation(actor);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    "work-order-invalidated-during-approach",
                    assignedTarget),
                requestImmediateReplan: false);
            actor?.AddActivity(CharacterActivityEvent.Work(
                plannedWorkTypeId,
                CharacterActivityOutcomes.Failed,
                "작업 실패: 이동 중 작업 주문이 취소되거나 교체되었습니다.",
                assignedTarget,
                reasonCode: "work-order-invalidated-during-approach",
                bubbleEligible: true));
            work.isWorking = false;
            work.AssignWork(null, FacilityWorkType.None);
            EndAiAction(
                actor,
                currentAction,
                CharacterAiActionTerminalKind.Failed);
            work.ClearActiveWorkRoutine(runId);
            yield break;
        }
        if (HasReachedAssignedWorkTarget(actor, grid)
            && assignedTarget is IWorkableFacility facility)
        {
            IBuildingVisitorPort visitor = actor?.BuildingVisitor;
            yield return facility.AllocateWorker(visitor);
            if (ShouldAbortWorkRun(runId, actor)
                || !work.isWorking
                || work.assignedShop != assignedTarget)
            {
                facility.DeallocateWorker(visitor);
                AbortWorkRun(runId, actor, currentAction);
                yield break;
            }

            currentAction?.ReleaseReservation(actor);
            FacilityWorkType workType = work.AssignedWorkType;
            WorkTypeDefinition workDefinition = FacilityWorkTypeMap.TryGet(
                    workType,
                    out WorkTypeDefinition resolvedWorkDefinition)
                ? resolvedWorkDefinition
                : null;
            WorkTypeId workTypeId = workDefinition?.WorkTypeId ?? default;
            string paidOrderKey =
                $"work:{workTypeId.Value}:{actor?.Identity?.PersistentId}:{runId}";
            if (workOrderRuntime != null
                && workTypeId.IsValid
                && workOrderRuntime.TryGetOrderFor(
                    assignedTarget,
                    workTypeId,
                    out WorkOrderProgressState paidOrder))
            {
                paidOrderKey = paidOrder.WorkOrderId;
                proficiencyRepetitionMultiplier =
                    ResolveRepeatPracticeMultiplier(
                        paidOrder.QualityAttemptIndex);
            }

            if (paidFacilityContracts != null
                && !paidFacilityContracts.TryChargeOrder(
                    assignedTarget,
                    paidOrderKey,
                    out string paidFailureReason))
            {
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Blocked,
                    $"{workDefinition?.DisplayName ?? "작업"} 중단: {paidFailureReason}",
                    assignedTarget,
                    reasonCode: "paid-facility-order",
                    bubbleEligible: true));
                facility.DeallocateWorker(visitor);
                work.isWorking = false;
                EndAiAction(
                    actor,
                    currentAction,
                    CharacterAiActionTerminalKind.Failed);
                work.ClearActiveWorkRoutine(runId);
                yield break;
            }

            CharacterSkillRuntimeEffects.BeginWork(
                actor,
                assignedTarget,
                workTypeId,
                $"work:{runId}:{assignedTargetPersistentId}:started");
            characterEnvironment.SetWorkContext(
                new CharacterId(actor?.Identity?.PersistentId),
                WorkExecutionRules.ResolveEnvironmentWorkKind(workTypeId));
            WorkDebugLog.LogStarted(actor);
            bool completedImmediately = false;
            bool completedSuccessfully = true;
            bool completionEffectsAlreadyApplied = false;
            string executionFailureCode = string.Empty;
            WorkOrderProgressState executionOrder = null;
            bool hasExecutionOrder = workOrderRuntime != null
                && workTypeId.IsValid
                && workOrderRuntime.TryGetOrderFor(
                    assignedTarget,
                    workTypeId,
                    out executionOrder);
            bool executionOrderMatchesPlan = hasExecutionOrder
                && (!requiresCommonWorkOrder
                    || string.Equals(
                        executionOrder.WorkOrderId,
                        expectedWorkOrderId,
                        StringComparison.Ordinal))
                && executionOrder.Status != WorkOrderStatus.Completed
                && executionOrder.Status != WorkOrderStatus.Cancelled;
            lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                executionOrderMatchesPlan
                    ? "execution-ready"
                    : requiresCommonWorkOrder
                        ? "execution-revalidation-failed"
                        : "execution-no-order",
                assignedTarget,
                workTypeId,
                expectedWorkOrderId,
                hasExecutionOrder ? executionOrder : null);
            if (requiresCommonWorkOrder && !executionOrderMatchesPlan)
            {
                // CommonWorkOrder is an authored execution contract, not a
                // hint. Falling through to CheckActionWork used to turn a
                // missing/replaced order into a 30-second generic duty shift,
                // then report work-execution-unavailable with zero accepted
                // WU. Preserve the planned identity and fail at the exact
                // ownership boundary instead.
                RecordPreWuExit(
                    WorkPreWuExitKind.WorkOrderLoopConditionRejected,
                    lastWorkOrderExecutionDetail);
                executionFailureCode =
                    "required-work-order-unavailable-after-allocation";
                completedSuccessfully = false;
                completedImmediately = true;
            }
            else if (executionOrderMatchesPlan)
            {
                yield return ExecuteWorkOrderRoutine(
                    runId,
                    actor,
                    assignedTarget,
                    workType,
                    workDefinition,
                    executionOrder.WorkOrderId,
                    (success, appliedEffects) =>
                    {
                        completedSuccessfully = success;
                        completionEffectsAlreadyApplied = appliedEffects;
                    });
                if (ShouldAbortWorkRun(runId, actor))
                {
                    facility.DeallocateWorker(visitor);
                    AbortWorkRun(runId, actor, currentAction);
                    yield break;
                }

                completedImmediately = true;
            }
            else if (WorkExecutionRules.TryGetExteriorWorkSeconds(
                         assignedTarget,
                         actor,
                         workTypeId,
                         out float exteriorWorkSeconds))
            {
                yield return ExecuteWorkAmountLoop(
                    runId,
                    actor,
                    assignedTarget,
                    workType,
                    exteriorWorkSeconds,
                    WorkTaskCatalog.GetLegacyDisplayName(workType));
                if (ShouldAbortWorkRun(runId, actor))
                {
                    facility.DeallocateWorker(visitor);
                    AbortWorkRun(runId, actor, currentAction);
                    yield break;
                }

                completedImmediately = true;
            }
            else if (executionHandlers != null
                && workDefinition != null
                && executionHandlers.TryGet(
                    workTypeId,
                    out IWorkExecutionHandler executionHandler))
            {
                WorkExecutionResult executionResult = new WorkExecutionResult();
                WorkExecutionContext executionContext = new WorkExecutionContext(
                    runId,
                    work,
                    actor,
                    assignedTarget,
                    workTypeId,
                    (requiredWork, label, extraMultiplier) => ExecuteWorkAmountLoop(
                        runId,
                        actor,
                        assignedTarget,
                        workType,
                        requiredWork,
                        label,
                        extraMultiplier),
                    () => CanContinueTimedWork(
                            runId,
                            actor,
                            assignedTarget)
                        && work.isWorking,
                    (
                        requiredWork,
                        completedWork,
                        label,
                        extraMultiplier,
                        applyDelta) => ExecutePersistentWorkAmountLoop(
                            runId,
                            actor,
                            assignedTarget,
                            workType,
                            requiredWork,
                            completedWork,
                            label,
                            applyDelta,
                            extraMultiplier),
                    (amount, remainingWork) => RecordApprovedWork(
                        amount,
                        actor,
                        remainingWork: remainingWork),
                    () => TrySuspendAtSafeCheckpoint(
                        actor,
                        assignedTarget,
                        workTypeId),
                    RegisterActiveExecutionCancellationResource);
                yield return executionHandler.Execute(executionContext, executionResult);
                ReleaseActiveExecutionCancellationResource();
                completedSuccessfully = executionResult.CompletedSuccessfully;
                completionEffectsAlreadyApplied =
                    executionResult.CompletionEffectsAlreadyApplied;
                if (ShouldAbortWorkRun(runId, actor))
                {
                    facility.DeallocateWorker(visitor);
                    AbortWorkRun(runId, actor, currentAction);
                    yield break;
                }

                completedImmediately = true;
            }

            if (!completedImmediately)
            {
                work.StartCheckActionWork(runId);
                yield return new WaitUntil(() => !work.IsActiveWorkRun(runId) || !work.isWorking);
                if (!work.IsActiveWorkRun(runId))
                {
                    AbortWorkRun(runId, actor, currentAction);
                    yield break;
                }

                work.ClearActiveWorkCheckRoutine(runId);
                completedSuccessfully = work.LastWorkRunCompleted;
            }
            else
            {
                work.isWorking = false;
                WorkDebugLog.LogEnd(actor, "작업량 완료");
            }

            // Dismantle completion intentionally removes its own target. All
            // other removals are external invalidations and remain failures.
            // Cache identity above and never dereference Unity fake-null during
            // successful dismantle finalization.
            bool expectedDismantleRemoval =
                workTypeId == BuiltInWorkTypeIds.Dismantle
                && completedSuccessfully
                && completionEffectsAlreadyApplied;
            if (!expectedDismantleRemoval
                && (assignedTarget == null
                    || !assignedTarget.gameObject.activeInHierarchy))
            {
                CompleteEmergencyAccounting("target-destroyed-after-execution");
                actor?.Brain?.ReportRuntimeActionFailure(
                    AIActionFailure.Create(
                        AIActionFailureKind.Destroyed,
                        "work-target-destroyed-or-inactive-after-execution",
                        assignedTarget),
                    requestImmediateReplan: false);
                CharacterSkillRuntimeEffects.EndWork(actor);
                characterEnvironment.ClearWorkContext(
                    new CharacterId(actor?.Identity?.PersistentId));
                ReturnEnvironmentalWorkwear(actor);
                facility.DeallocateWorker(visitor);
                work.isWorking = false;
                if (work.IsActiveWorkRun(runId))
                {
                    work.AssignWork(null, FacilityWorkType.None);
                }
                EndAiAction(
                    actor,
                    currentAction,
                    CharacterAiActionTerminalKind.Failed);
                work.ClearActiveWorkRoutine(runId);
                yield break;
            }

            bool routineNeedInterrupted =
                work.LastWorkRunInterruptedForRoutineNeed;
            if (!completedSuccessfully
                && !routineNeedInterrupted
                && approvedProficiencyWork <= 0f)
            {
                RecordPreWuExit(
                    WorkPreWuExitKind.ExecutionHandlerRejected,
                    "workType=" + workTypeId.Value + "; "
                    + CaptureTimedWorkGateDetail(
                        runId,
                        actor,
                        assignedTarget));
            }
            if (completedSuccessfully)
            {
                AwardApprovedWork(actor, ProficiencyWorkOutcome.Success);
                RecordSpeciesCompletedWork(
                    actor,
                    workTypeId,
                    approvedProficiencyWork);
                PublishWorkCompleted(actor, workTypeId, string.Empty);
                CharacterSkillRuntimeEffects.TriggerWorkCompleted(
                    actor,
                    assignedTarget,
                    workTypeId,
                    $"work:{runId}:{assignedTargetPersistentId}:completed");
                if (!completionEffectsAlreadyApplied)
                {
                    ModularFacilityRuntimeEffects.ApplyWorkCompleted(
                        visitor,
                        assignedTarget,
                        workTypeId);
                    roomEnvironmentExperienceService?.Apply(new RoomEnvironmentExperienceEvent(
                        actor,
                        assignedTarget,
                        RoomExperienceActivity.Work,
                        workTypeId));
                }
            }
            else if (routineNeedInterrupted)
            {
                AwardApprovedWork(actor, ProficiencyWorkOutcome.PartialSuccess);
                RecordSpeciesCompletedWork(
                    actor,
                    workTypeId,
                    approvedProficiencyWork);
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workTypeId,
                    CharacterActivityOutcomes.Changed,
                    "생리 욕구 해결 후 재개하도록 작업 진행 상태 보존",
                    assignedTarget,
                    reasonCode: "routine-need-suspended"));
            }
            else
            {
                AwardApprovedWork(actor, ProficiencyWorkOutcome.SafeFailure);
                PublishWorkCompleted(
                    actor,
                    workTypeId,
                    "outcome:failure");
                actor?.Brain?.ReportRuntimeActionFailure(
                    AIActionFailure.Create(
                        AIActionFailureKind.ResourceUnavailable,
                        string.IsNullOrWhiteSpace(executionFailureCode)
                            ? $"work-execution-unavailable:{workTypeId.Value}"
                            : executionFailureCode,
                        assignedTarget),
                    requestImmediateReplan: false);
            }

            actor?.AiMemory?.RecordWork(
                workTypeId,
                assignedTarget,
                completedSuccessfully,
                $"{WorkTaskCatalog.GetLegacyDisplayName(workType)} {(completedSuccessfully ? "완료" : "실패")}: {assignedTargetName}");
            CompleteEmergencyAccounting(
                completedSuccessfully
                    ? "completed"
                    : routineNeedInterrupted
                        ? "routine-need-suspended"
                        : "failed");
            CharacterSkillRuntimeEffects.EndWork(actor);
            bool wasPriorityTarget = work.assignedShop == work.PriorityWorkTarget;
            // Keep the AI action alive until every runtime owner is released.
            // Publishing isBestActionEnd before this cleanup lets a facility
            // invalidation wake a new Work run while this old run can still
            // clear assignedShop, clobbering the new run's target and leaving
            // a Work action without an executing coroutine.
            facility.DeallocateWorker(visitor);
            work.AssignWork(null, FacilityWorkType.None);
            if (wasPriorityTarget && !routineNeedInterrupted)
            {
                work.ClearPriorityWorkTarget();
            }
            EndAiAction(
                actor,
                currentAction,
                completedSuccessfully || routineNeedInterrupted
                    ? CharacterAiActionTerminalKind.Completed
                    : CharacterAiActionTerminalKind.Failed);
        }
        else
        {
            work.isWorking = false;
            WorkDebugLog.LogEnd(actor, "작업 도달 실패");
            actor?.AddActivity(CharacterActivityEvent.Work(
                work.AssignedWorkType,
                CharacterActivityOutcomes.Failed,
                "작업 실패: 작업 도달 실패",
                assignedTarget,
                reasonCode: "target-unreachable",
                bubbleEligible: true));
            actor?.AiMemory?.RecordWork(
                work.AssignedWorkTypeId,
                assignedTarget,
                false,
                $"작업 도달 실패: {(assignedTarget != null ? assignedTarget.name : "대상 없음")}");
            currentAction?.ReleaseReservation(actor);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.NoPath,
                    "work-target-unreachable",
                    assignedTarget),
                requestImmediateReplan: false);
            EndAiAction(
                actor,
                currentAction,
                CharacterAiActionTerminalKind.Failed);
            work.ClearActiveWorkRoutine(runId);
            yield break;
        }

        EndAiAction(actor, currentAction);
        work.ClearActiveWorkRoutine(runId);
    }

    private bool HasReachedAssignedWorkTarget(CharacterActor actor, Grid grid)
    {
        if (actor == null || grid == null || work.assignedShop == null)
        {
            return false;
        }

        return work.assignedShop.IsWorkAccessGridPosition(
            grid,
            grid.GetXY(work.transform.position));
    }

    private IEnumerator ExecuteRestockHaulWork(
        int runId,
        AIAction currentAction,
        AbilityMove move,
        Grid grid,
        Action<bool> onCompleted)
    {
        CharacterActor actor = work.WorkerActor;
        BuildableObject restockTarget = work.assignedShop;
        if (restockTarget == null || restockTarget.isDestroy)
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.Destroyed,
                    "restock-target-invalid-before-start"),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }
        if (!CharacterPersistentIdentity.TryGet(
                actor,
                out CharacterId restockActorId)
            || !restockTarget.PersistentInstanceId.IsValid)
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.CannotStart,
                    "restock-plan-identity-invalid",
                    restockTarget),
                requestImmediateReplan: false);
            ReleaseActiveRestockLease(
                ItemReservationReleaseReason.Cancelled);
            work.isWorking = false;
            yield break;
        }
        BuildingInstanceId restockTargetId =
            restockTarget.PersistentInstanceId;
        CharacterSkillRuntimeEffects.BeginWork(
            actor,
            restockTarget,
            BuiltInWorkTypeIds.Restock,
            $"work:{runId}:{restockTargetId.Value}:restock-started");
        actor?.Brain?.SetActionPhase(
            "\uBCF4\uCDA9 \uACC4\uD68D",
            restockTarget,
            "restock:planning");
        float durationMultiplier = work.GetWorkEnvironmentDurationMultiplier(BuiltInWorkTypeIds.Restock)
            / Mathf.Max(0.1f, CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(actor));
        if (restockTarget is not IRestockableFacility restockable)
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    "restock-target-not-restockable"),
                requestImmediateReplan: false);
            actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Restock,
                CharacterActivityOutcomes.Failed,
                "보충 실패: 재고를 받을 수 없는 시설",
                restockTarget,
                reasonCode: "target-not-restockable",
                bubbleEligible: true));
            work.isWorking = false;
            yield break;
        }

        if (!TryCreateRestockHaulPlan(
            actor,
            grid,
            restockTarget,
            restockable,
            out BuildableObject warehouseBuilding,
            out IWarehouseFacility warehouse,
            out WarehouseRestockItem saleItem,
            out int loadAmount,
            out string failureReason))
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    string.IsNullOrWhiteSpace(failureReason)
                        ? "restock-haul-plan-unavailable"
                        : failureReason),
                requestImmediateReplan: false);
            actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Restock,
                CharacterActivityOutcomes.Failed,
                $"보충 실패: {failureReason}",
                restockTarget,
                reasonCode: failureReason,
                bubbleEligible: true));
            work.isWorking = false;
            yield break;
        }

        if (restockTarget is not Shop shop
            || !shop.TryGetSaleItem(saleItem.Id, out SaleItem authoredSaleItem)
            || authoredSaleItem == null
            || !authoredSaleItem.ItemDefinitionId.IsValid
            || actor.WorldItemStackRuntime is not IWorldItemQuantityLeaseRuntime leaseRuntime
            || reservedItemTransfers == null)
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.Unsupported,
                    "restock-quantity-lease-runtime-unavailable"),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }

        activeRestockOperationId = CreateRestockOperationId(
            restockActorId.Value,
            restockTargetId.Value,
            saleItem.Id,
            runId);
        WorldItemReservedStackQuantity stockReservation = default;
        Vector2Int pickupStandPosition = default;
        bool reserved = false;
        for (int requested = Mathf.Max(1, loadAmount); requested >= 1; requested--)
        {
            if (!leaseRuntime.TryReserveAvailableItemForDirectPickup(
                    actor,
                    authoredSaleItem.ItemDefinitionId.Value,
                    requested,
                    ItemReservationPurpose.FacilityBuffer,
                    activeRestockOperationId,
                    out stockReservation,
                    out pickupStandPosition,
                    out failureReason))
            {
                continue;
            }
            loadAmount = requested;
            reserved = true;
            break;
        }
        if (!reserved || !stockReservation.IsValid)
        {
            activeRestockOperationId = string.Empty;
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    string.IsNullOrWhiteSpace(failureReason)
                        ? "restock-quantity-unavailable"
                        : failureReason),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }
        activeRestockLeaseId = stockReservation.LeaseId;
        activeRestockOwner = restockTarget as IRetailRestockOperationOwner;
        if (activeRestockOwner == null
            || !activeRestockOwner.TryBeginRestockOperation(
                activeRestockOperationId))
        {
            ReleaseActiveRestockLease(ItemReservationReleaseReason.Cancelled);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    "restock-operation-owner-rejected"),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }
        actor?.Brain?.SetActionPhase(
            "\uC774\uB3D9",
            warehouseBuilding,
            "restock:move-to-stock");
        Vector2Int pickupStart = work.WorkGridResolver.GetGridPosition(grid, actor);
        Queue<GridMoveStep> pathToWarehouse = actor.PathSearchBroker?.GetMovePathTo(
            grid,
            pickupStart,
            pickupStandPosition,
            GridPathSearchPriority.Normal,
            GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor)));
        if (pathToWarehouse == null
            || (pathToWarehouse.Count == 0
                && pickupStart != pickupStandPosition))
        {
            ReleaseActiveRestockLease(ItemReservationReleaseReason.Replanned);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.NoPath,
                    "restock-pickup-path-unavailable",
                    warehouseBuilding),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }

        actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Restock,
            CharacterActivityOutcomes.Progress,
            $"보충 이동: {warehouseBuilding.name} -> {restockTarget.name}",
            restockTarget,
            reasonCode: "moving-to-stock"));
        yield return move.MoveByPath(pathToWarehouse, currentAction);
        if (ShouldAbortWorkRun(runId, actor, restockTarget))
        {
            AbortWorkRun(runId, actor, currentAction);
            yield break;
        }
        if (TrySuspendAtSafeCheckpoint(
                actor,
                restockTarget,
                BuiltInWorkTypeIds.Restock))
        {
            AbortWorkRun(runId, actor, currentAction);
            yield break;
        }

        int carriedAmount = 0;
        for (int i = 0; i < loadAmount; i++)
        {
            actor?.Brain?.SetActionPhase(
                "\uBCF4\uCDA9 \uC801\uC7AC",
                warehouseBuilding,
                $"restock:loading:{i}/{loadAmount}");
            Vector2 pickupWorld = grid.GetWorldPos(pickupStandPosition);
            Vector3 pickupPosition = new Vector3(
                pickupWorld.x,
                pickupWorld.y,
                actor.transform.position.z);
            yield return move.Move2PosBySpeed(pickupPosition, 0.8f, currentAction);
            if (ShouldAbortWorkRun(runId, actor, restockTarget))
            {
                ReleaseActiveRestockLease(ItemReservationReleaseReason.Cancelled);
                AbortWorkRun(runId, actor, currentAction);
                yield break;
            }

            carriedAmount++;
            actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Restock,
                CharacterActivityOutcomes.Progress,
                $"보충 적재: {saleItem.Name} {carriedAmount}/{loadAmount}",
                warehouseBuilding,
                reasonCode: "loading-stock",
                quantity: carriedAmount));
            work.FloatingIconFeedbackService.Show(
                actor,
                saleItem.Sprite,
                FloatingIconFeedbackDefaults.DefaultMaxWorldSize);
            float loadingStartedAt = gameClock.Time;
            yield return new WaitForSeconds(RestockPickupWaitSeconds * durationMultiplier);
            float loadingElapsed = Mathf.Max(
                1f / 60f,
                gameClock.Time - loadingStartedAt);
            float approvedLoadingWork =
                WorkExecutionRules.CalculateWorkPerSecond(
                    workAmountCalculator,
                    actor,
                    restockTarget,
                    BuiltInWorkTypeIds.Restock,
                    durationMultiplier)
                * loadingElapsed;
            RecordApprovedWork(
                approvedLoadingWork,
                actor,
                remainingWork: Mathf.Max(
                    0f,
                    loadAmount - carriedAmount)
                    * RestockPickupWaitSeconds);
            if (ShouldAbortWorkRun(runId, actor, restockTarget))
            {
                ReleaseActiveRestockLease(ItemReservationReleaseReason.Cancelled);
                AbortWorkRun(runId, actor, currentAction);
                yield break;
            }
            if (TrySuspendAtSafeCheckpoint(
                    actor,
                    restockTarget,
                    BuiltInWorkTypeIds.Restock))
            {
                ReleaseActiveRestockLease(ItemReservationReleaseReason.Cancelled);
                AbortWorkRun(runId, actor, currentAction);
                yield break;
            }
        }

        if (carriedAmount <= 0)
        {
            actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Restock,
                CharacterActivityOutcomes.Failed,
                "보충 실패: 창고 재고 부족",
                warehouseBuilding,
                reasonCode: "warehouse-stock-shortage",
                bubbleEligible: true));
            work.isWorking = false;
            yield break;
        }

        CharacterCarryInventory restockCarry = CharacterCarryInventory.Ensure(actor);
        int physicallyPickedUp = 0;
        string pickupFailure = "restock-carry-runtime-unavailable";
        if (restockCarry == null
            || actor.WorldItemStackRuntime == null
            || !actor.WorldItemStackRuntime.TryPickupReservedStackQuantity(
                actor,
                restockCarry,
                stockReservation,
                out physicallyPickedUp,
                out pickupFailure)
            || physicallyPickedUp <= 0)
        {
            ReleaseActiveRestockLease(ItemReservationReleaseReason.Replanned);
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    string.IsNullOrWhiteSpace(pickupFailure)
                        ? "restock-physical-pickup-failed"
                        : pickupFailure),
                requestImmediateReplan: false);
            work.isWorking = false;
            yield break;
        }
        carriedAmount = physicallyPickedUp;

        if (!TryGetPathToBuilding(grid, actor, restockTarget, out Queue<GridMoveStep> pathToShop))
        {
            ReleaseActiveRestockLease(ItemReservationReleaseReason.Replanned);
            actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Restock,
                CharacterActivityOutcomes.Blocked,
                "보충 실패: 상점 경로 없음",
                restockTarget,
                reasonCode: "shop-path-missing",
                bubbleEligible: true));
            work.isWorking = false;
            yield break;
        }

        actor?.Brain?.SetActionPhase(
            "\uC774\uB3D9",
            restockTarget,
            "restock:return-to-target");
        yield return move.MoveByPath(pathToShop, currentAction);
        if (ShouldAbortWorkRun(runId, actor, restockTarget))
        {
            ReleaseActiveRestockLease(ItemReservationReleaseReason.Cancelled);
            AbortWorkRun(runId, actor, currentAction);
            yield break;
        }
        if (TrySuspendAtSafeCheckpoint(
                actor,
                restockTarget,
                BuiltInWorkTypeIds.Restock))
        {
            ReleaseActiveRestockLease(ItemReservationReleaseReason.Cancelled);
            AbortWorkRun(runId, actor, currentAction);
            yield break;
        }

        actor?.Brain?.SetActionPhase(
            "\uBCF4\uCDA9 \uBC18\uC601",
            restockTarget,
            "restock:commit");
        ReservedRetailStockTransferReceipt transferReceipt = null;
        DomainFailure consumeFailure = new DomainFailure(
            FailureCode.ItemTransferRequestFailed,
            "retail-transfer-service-unavailable");
        if (reservedItemTransfers is not IReservedRetailStockTransferService retailTransfers
            || !retailTransfers.TryTakeReservedRetailLots(
                activeRestockLeaseId,
                carriedAmount,
                saleItem.Id,
                authoredSaleItem.ItemDefinitionId.Value,
                activeRestockOperationId,
                restockCarry,
                out transferReceipt,
                out consumeFailure))
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.ResourceUnavailable,
                    consumeFailure.ToString()),
                requestImmediateReplan: false);
            ReleaseActiveRestockLease(ItemReservationReleaseReason.Replanned);
            work.isWorking = false;
            yield break;
        }
        if (!restockable.TryReceiveExactRetailLots(
                transferReceipt.Lots,
                carriedAmount,
                out int restocked,
                out string resultMessage))
        {
            if (!retailTransfers.TryRollbackRetailTransfer(
                    transferReceipt,
                    out DomainFailure rollbackFailure))
            {
                throw new InvalidOperationException(
                    $"Retail transfer '{transferReceipt.OperationId}' failed to rollback: {rollbackFailure}");
            }
            // The exact physical cargo is Carried again after rollback. End the
            // operation only after that ownership is restored so the normal
            // cancellation path drops it at the actor's current cell instead
            // of leaving an ownerless carried slice behind.
            ReleaseActiveRestockLease(ItemReservationReleaseReason.Cancelled);
            restocked = 0;
        }
        else
        {
            // Shop ownership is now authoritative. Only now may the source
            // quantity lease and the restock operation be completed.
            ReleaseActiveRestockLease(ItemReservationReleaseReason.Completed);
        }

        actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Restock,
            restocked > 0 ? CharacterActivityOutcomes.Completed : CharacterActivityOutcomes.Failed,
            restocked > 0
                ? $"보충 완료: {restockTarget.name} {resultMessage}"
                : $"보충 실패: {resultMessage}",
            restockTarget,
            reasonCode: resultMessage,
            quantity: restocked,
            bubbleEligible: restocked <= 0));
        if (restocked > 0)
        {
            RecordApprovedWork(restocked, actor, remainingWork: 0f);
            RecordSpeciesCompletedWork(
                actor,
                BuiltInWorkTypeIds.Restock,
                restocked);
            PublishWorkCompleted(
                actor,
                BuiltInWorkTypeIds.Restock,
                saleItem.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            CharacterSkillRuntimeEffects.TriggerWorkCompleted(
                actor,
                restockTarget,
                BuiltInWorkTypeIds.Restock,
                $"work:{runId}:{restockTarget.RequirePersistentInstanceId().Value}:restock-completed");
        }

        actor?.AiMemory?.RecordWork(
            BuiltInWorkTypeIds.Restock,
            restockTarget,
            restocked > 0,
            restocked > 0
                ? $"보충 완료: {restockTarget.name}"
                : $"보충 실패: {restockTarget.name}");

        onCompleted?.Invoke(restocked > 0);
        yield return new WaitForSeconds(0.5f * durationMultiplier);
        work.isWorking = false;
        WorkDebugLog.LogEnd(actor, "보충 완료");
    }

    private bool TryCreateRestockHaulPlan(
        CharacterActor actor,
        Grid grid,
        BuildableObject restockTarget,
        IRestockableFacility restockable,
        out BuildableObject warehouseBuilding,
        out IWarehouseFacility warehouse,
        out WarehouseRestockItem saleItem,
        out int loadAmount,
        out string failureReason)
    {
        warehouseBuilding = null;
        warehouse = null;
        saleItem = default;
        loadAmount = 0;
        failureReason = string.Empty;

        if (actor == null || grid == null || restockTarget == null || restockable == null)
        {
            failureReason = "보충 경로 정보 없음";
            return false;
        }

        List<IWarehouseFacility> reachableWarehouses = targetSelector
            .FindReachableWarehouses(null)
            .Where(candidate => candidate.HasWarehouseInventory
                && candidate.Inventory != null
                && !ReferenceEquals(candidate, restockTarget))
            .ToList();

        if (!restockable.TryFindRestockSource(
            reachableWarehouses,
            restockable.MissingStock,
            out warehouse,
            out saleItem,
            out loadAmount,
            out failureReason))
        {
            return false;
        }

        warehouseBuilding = warehouse as BuildableObject;
        if (warehouseBuilding == null)
        {
            failureReason = "창고 건물 정보 없음";
            return false;
        }

        return true;
    }

    private static Vector3 GetWarehousePickupWorldPosition(
        Grid grid,
        BuildableObject warehouseBuilding,
        int pickupIndex,
        int pickupCount)
    {
        if (grid == null
            || warehouseBuilding == null
            || warehouseBuilding.buildPoses == null
            || warehouseBuilding.buildPoses.Count == 0)
        {
            return warehouseBuilding != null ? warehouseBuilding.transform.position : Vector3.zero;
        }

        int minX = warehouseBuilding.buildPoses.Min((pos) => pos.x);
        int maxX = warehouseBuilding.buildPoses.Max((pos) => pos.x);
        int slotCount = Mathf.Clamp(pickupCount, 1, Mathf.Max(1, maxX - minX + 1));
        int slot = pickupIndex % slotCount;
        if ((pickupIndex / slotCount) % 2 == 1)
        {
            slot = slotCount - 1 - slot;
        }

        Vector2 minWorld = grid.GetWorldPos(new Vector2Int(minX, warehouseBuilding.centerPos.y));
        Vector2 maxWorld = grid.GetWorldPos(new Vector2Int(maxX, warehouseBuilding.centerPos.y));
        float minWorldX = Mathf.Min(minWorld.x, maxWorld.x) + 0.15f;
        float maxWorldX = Mathf.Max(minWorld.x, maxWorld.x) - 0.15f;
        float t = slotCount <= 1 ? 0.5f : (slot + 0.5f) / slotCount;
        float x = minWorldX <= maxWorldX
            ? Mathf.Lerp(minWorldX, maxWorldX, t)
            : (minWorld.x + maxWorld.x) * 0.5f;

        return new Vector3(x, minWorld.y, warehouseBuilding.transform.position.z);
    }

    private bool TryGetPathToBuilding(
        Grid grid,
        CharacterActor actor,
        BuildableObject target,
        out Queue<GridMoveStep> path)
    {
        path = null;
        if (grid == null || actor == null || target == null)
        {
            return false;
        }

        Vector2Int startPos = work.WorkGridResolver.GetGridPosition(grid, actor);
        if (actor.PathSearchBroker == null)
        {
            return false;
        }

        GridTraversalContext traversal = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(actor));
        if (!actor.PathSearchBroker.TryGetSearch(
                grid,
                startPos,
                out GridPathSearchResult search,
                GridPathSearchPriority.Normal,
                traversal)
            || !WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(
                target,
                search,
                out Vector2Int workAccess))
        {
            return false;
        }

        path = search.GetMovePathTo(workAccess);
        return path != null;
    }

    private static string CreateRestockOperationId(
        string actorId,
        string targetId,
        int saleItemId,
        int runId)
    {
        if (string.IsNullOrWhiteSpace(actorId)
            || string.IsNullOrWhiteSpace(targetId)
            || saleItemId < 0
            || runId <= 0)
        {
            throw new ArgumentException(
                "Restock operation identity requires actor, target and sale item IDs.");
        }
        return $"restock:{actorId.Trim()}:{targetId.Trim()}:{saleItemId}:{runId:D8}";
    }

    private void ReleaseActiveRestockLease(ItemReservationReleaseReason reason)
    {
        string leaseId = activeRestockLeaseId;
        string operationId = activeRestockOperationId;
        IRetailRestockOperationOwner operationOwner = activeRestockOwner;
        CharacterActor actor = work.WorkerActor;
        if (!string.IsNullOrWhiteSpace(operationId)
            && actor != null)
        {
            CharacterCarryInventory carry = actor.GetComponent<CharacterCarryInventory>();
            bool ownsCargo = carry?.Items.Any(item => item != null
                && item.quantity > 0
                && string.Equals(
                    item.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal)) == true;
            string dropFailure = "restock-drop-runtime-unavailable";
            if (ownsCargo
                && (actor.WorldItemStackRuntime
                        is not IWorldItemCarryRecoveryRuntime carryRecovery
                    || !carryRecovery.TryDropCarriedItems(
                        actor,
                        carry,
                        new[] { operationId },
                        out dropFailure)))
            {
                throw new InvalidOperationException(
                    $"Restock cargo '{operationId}' could not be physically dropped before lease release: {dropFailure}");
            }
        }
        activeRestockLeaseId = string.Empty;
        activeRestockOperationId = string.Empty;
        activeRestockOwner = null;
        operationOwner?.EndRestockOperation(operationId);
        if (string.IsNullOrWhiteSpace(leaseId))
            return;
        if (work.WorkerActor?.WorldItemStackRuntime
            is IWorldItemQuantityLeaseRuntime leaseRuntime)
        {
            leaseRuntime.ReleaseQuantityLease(leaseId, reason);
        }
    }

    private IEnumerator ExecuteWorkOrderRoutine(
        int runId,
        CharacterActor actor,
        BuildableObject target,
        FacilityWorkType workType,
        WorkTypeDefinition workDefinition,
        string expectedWorkOrderId,
        Action<bool, bool> onCompleted)
    {
        bool completed = false;
        bool appliedCompletionEffects = false;
        lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
            "routine-entry",
            target,
            workDefinition?.WorkTypeId ?? default,
            expectedWorkOrderId,
            null);
        if (target == null || workOrderRuntime == null)
        {
            lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                "routine-entry-rejected",
                target,
                workDefinition?.WorkTypeId ?? default,
                expectedWorkOrderId,
                null) + ",reason=target-or-runtime-missing";
            onCompleted?.Invoke(false, false);
            yield break;
        }

        string label = workDefinition?.DisplayName ?? WorkTaskCatalog.GetLegacyDisplayName(workType);
        WorkTypeId workTypeId = workDefinition?.WorkTypeId ?? default;
        float durationMultiplier = work.GetWorkEnvironmentDurationMultiplier(workTypeId);
        float lastReportTime = -10f;
        float lastNeedUpdateTime = gameClock.Time;
        bool enteredWorkOrderLoop = false;
        IConstructionProjectWorkforceRuntime constructionProject =
            workTypeId == BuiltInWorkTypeIds.Construct
                ? workOrderRuntime as IConstructionProjectWorkforceRuntime
                : null;
        ProjectWorkerLease constructionLease = null;
        string workforceFailure = string.Empty;
        WorkOrderProgressState beforeLoopOrder = null;
        workOrderRuntime.TryGetOrderFor(
            target,
            workTypeId,
            out beforeLoopOrder);
        lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
            "routine-before-loop",
            target,
            workTypeId,
            expectedWorkOrderId,
            beforeLoopOrder);
        if (workTypeId == BuiltInWorkTypeIds.Construct
            && (constructionProject == null
                || !constructionProject.TryJoinConstructionProject(
                    target,
                    actor,
                    out constructionLease,
                    out workforceFailure)))
        {
            lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                "construction-workforce-rejected",
                target,
                workTypeId,
                expectedWorkOrderId,
                beforeLoopOrder) + ",reason=" + (workforceFailure ?? string.Empty);
            actor?.AddActivity(CharacterActivityEvent.Work(
                workType,
                CharacterActivityOutcomes.Blocked,
                $"{label} 중단: {workforceFailure ?? "건설 인력 권위 없음"}",
                target,
                reasonCode: "construction-workforce-blocked",
                bubbleEligible: true));
            onCompleted?.Invoke(false, false);
            yield break;
        }

        if (constructionLease != null)
        {
            // Unity may stop the owning work coroutine without advancing this
            // iterator again. Register the project slot with the executor's
            // synchronous cancellation authority so lifecycle/replan cleanup
            // cannot leave a ghost worker in the construction project.
            RegisterActiveExecutionCancellationResource(constructionLease);
        }

        try
        {
        while (CanContinueTimedWork(runId, actor, target)
            && work.isWorking
            && workOrderRuntime.TryGetOrderFor(target, workTypeId, out WorkOrderProgressState order)
            && string.Equals(
                order.WorkOrderId,
                expectedWorkOrderId,
                StringComparison.Ordinal)
            && order.Status != WorkOrderStatus.Completed
            && order.Status != WorkOrderStatus.Cancelled)
        {
            enteredWorkOrderLoop = true;
            lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                "routine-loop-entered",
                target,
                workTypeId,
                expectedWorkOrderId,
                order);
            if (ApplyTimedWorkNeedsAndInterrupt(
                    actor,
                    ref lastNeedUpdateTime,
                    out string interruptReason))
            {
                lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                    "routine-need-exit",
                    target,
                    workTypeId,
                    expectedWorkOrderId,
                    order) + ",reason=" + interruptReason + "; "
                    + CaptureTimedWorkGateDetail(runId, actor, target);
                RecordPreWuExit(
                    WorkPreWuExitKind.RoutineNeedInterrupt,
                    "reason=" + interruptReason + "; "
                    + CaptureTimedWorkGateDetail(runId, actor, target));
                onCompleted?.Invoke(false, false);
                yield break;
            }

            if (TrySuspendAtSafeCheckpoint(actor, target, workTypeId))
            {
                lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                    "emergency-suspension-exit",
                    target,
                    workTypeId,
                    expectedWorkOrderId,
                    order) + "; "
                    + CaptureTimedWorkGateDetail(runId, actor, target);
                RecordPreWuExit(
                    WorkPreWuExitKind.EmergencySuspension,
                    CaptureTimedWorkGateDetail(runId, actor, target));
                onCompleted?.Invoke(false, false);
                yield break;
            }

            float workerRate = CalculateWorkPerSecond(
                workAmountCalculator,
                actor,
                target,
                workTypeId,
                durationMultiplier);
            float contributionMultiplier = 1f;
            if (constructionProject != null)
            {
                if (!constructionProject.UpdateConstructionWorkerRate(
                        target,
                        actor,
                        workerRate))
                {
                    lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                        "construction-rate-rejected",
                        target,
                        workTypeId,
                        expectedWorkOrderId,
                        order) + "; "
                        + CaptureTimedWorkGateDetail(runId, actor, target);
                    onCompleted?.Invoke(false, false);
                    yield break;
                }
                contributionMultiplier =
                    constructionProject.GetConstructionContributionMultiplier(
                        target,
                        actor);
                if (contributionMultiplier <= 0f)
                {
                    lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                        "construction-contribution-rejected",
                        target,
                        workTypeId,
                        expectedWorkOrderId,
                        order) + ",multiplier="
                        + contributionMultiplier.ToString("0.###") + "; "
                        + CaptureTimedWorkGateDetail(runId, actor, target);
                    onCompleted?.Invoke(false, false);
                    yield break;
                }
            }

            float remainingSeconds = Mathf.Max(
                0f,
                order.RequiredWork - order.CompletedWork)
                / Mathf.Max(
                    0.05f,
                    workerRate * contributionMultiplier);
            if (ShouldInterruptForEnvironment(
                    actor,
                    target,
                    workTypeId,
                    remainingSeconds))
            {
                lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                    "environment-exit",
                    target,
                    workTypeId,
                    expectedWorkOrderId,
                    order) + ",remainingSeconds="
                    + remainingSeconds.ToString("0.###") + "; "
                    + CaptureTimedWorkGateDetail(runId, actor, target);
                RecordPreWuExit(
                    WorkPreWuExitKind.EnvironmentInterrupt,
                    CaptureTimedWorkGateDetail(runId, actor, target));
                onCompleted?.Invoke(false, false);
                yield break;
            }

            if (order.Status == WorkOrderStatus.WaitingForMaterials)
            {
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Blocked,
                    $"{label} 대기: 재료가 아직 도착하지 않음",
                    target,
                    reasonCode: "waiting-for-materials",
                    value: order.ProgressRatio));
                yield return new WaitForSeconds(0.35f);
                if (!workOrderRuntime.RefreshMaterialsReady(target as ConstructionSite))
                {
                    continue;
                }
            }

            float requestedLaborWork = workerRate * gameClock.DeltaTime;
            float deltaWork = Mathf.Min(
                requestedLaborWork * contributionMultiplier,
                Mathf.Max(0f, order.RequiredWork - order.CompletedWork));
            float acceptedLaborWork = deltaWork / contributionMultiplier;
            if (deltaWork <= 0f || acceptedLaborWork <= 0f)
            {
                yield return null;
                continue;
            }
            bool expectsTargetDestruction =
                workTypeId == BuiltInWorkTypeIds.Dismantle
                && order.CompletedWork + deltaWork + 0.001f
                    >= order.RequiredWork;
            if (expectsTargetDestruction)
            {
                work.BeginExpectedWorkTargetDestruction(target);
            }

            bool applied;
            string message;
            lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                "apply-before",
                target,
                workTypeId,
                expectedWorkOrderId,
                order);
            try
            {
                applied = workOrderRuntime.ApplyWork(
                    actor,
                    target,
                    workTypeId,
                    deltaWork,
                    out completed,
                    out appliedCompletionEffects,
                    out message);
            }
            finally
            {
                if (expectsTargetDestruction)
                {
                    work.EndExpectedWorkTargetDestruction(target);
                }
            }
            lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                "apply-after",
                target,
                workTypeId,
                expectedWorkOrderId,
                order)
                + ",accepted=" + applied
                + ",completed=" + completed
                + ",effects=" + appliedCompletionEffects
                + ",message=" + (message ?? string.Empty);
            if (!applied)
            {
                lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                    "apply-rejected",
                    target,
                    workTypeId,
                    expectedWorkOrderId,
                    order) + ",message=" + (message ?? string.Empty) + "; "
                    + CaptureTimedWorkGateDetail(runId, actor, target);
                RecordPreWuExit(
                    WorkPreWuExitKind.WorkOrderApplyRejected,
                    (message ?? string.Empty) + "; "
                    + CaptureTimedWorkGateDetail(runId, actor, target));
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Blocked,
                    $"{label} 중단: {message}",
                    target,
                    reasonCode: "work-order-blocked",
                    bubbleEligible: true));
                onCompleted?.Invoke(false, false);
                yield break;
            }
            RecordApprovedWork(
                acceptedLaborWork,
                actor,
                remainingWork: Mathf.Max(
                    0f,
                    order.RequiredWork - order.CompletedWork - deltaWork)
                    / contributionMultiplier);
            RecordProjectOutputAdjustment(
                deltaWork,
                acceptedLaborWork,
                workTypeId);

            if (gameClock.Time - lastReportTime >= 0.75f
                && workOrderRuntime.TryGetOrderFor(target, workTypeId, out order))
            {
                lastReportTime = gameClock.Time;
                actor?.Brain?.SetActionPhase($"{label} {Mathf.RoundToInt(order.ProgressRatio * 100f)}%", target);
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Progress,
                    $"{label} 진행 {Mathf.RoundToInt(order.ProgressRatio * 100f)}%",
                    target,
                    reasonCode: "work-progress",
                    value: order.ProgressRatio));
            }

            if (completed)
            {
                lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                    "routine-completed",
                    target,
                    workTypeId,
                    expectedWorkOrderId,
                    order) + ",effects=" + appliedCompletionEffects;
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Completed,
                    $"{label} 완료",
                    target,
                    reasonCode: "work-order-completed",
                    value: 1f));
                onCompleted?.Invoke(true, appliedCompletionEffects);
                yield break;
            }

            yield return null;
        }
        }
        finally
        {
            if (ReferenceEquals(
                    activeExecutionCancellationResource,
                    constructionLease))
            {
                ReleaseActiveExecutionCancellationResource();
            }
            else
            {
                // CancelActiveRun may already have disposed and detached it.
                constructionLease?.Dispose();
            }
        }

        WorkOrderProgressState exitOrder = null;
        workOrderRuntime.TryGetOrderFor(target, workTypeId, out exitOrder);
        lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
            enteredWorkOrderLoop
                ? "routine-loop-exited"
                : "routine-loop-not-entered",
            target,
            workTypeId,
            expectedWorkOrderId,
            exitOrder) + "; "
            + CaptureTimedWorkGateDetail(runId, actor, target);
        if (!enteredWorkOrderLoop)
        {
            string orderDetail;
            if (!workOrderRuntime.TryGetOrderFor(
                    target,
                    workTypeId,
                    out WorkOrderProgressState rejectedOrder))
            {
                orderDetail = "order=missing";
            }
            else
            {
                orderDetail = "order=" + rejectedOrder.WorkOrderId
                    + "/expected=" + expectedWorkOrderId
                    + ",status=" + rejectedOrder.Status
                    + ",remaining="
                    + Mathf.Max(
                        0f,
                        rejectedOrder.RequiredWork
                        - rejectedOrder.CompletedWork).ToString("0.###");
            }
            RecordPreWuExit(
                WorkPreWuExitKind.WorkOrderLoopConditionRejected,
                orderDetail + "; "
                + CaptureTimedWorkGateDetail(runId, actor, target));
        }
        onCompleted?.Invoke(false, appliedCompletionEffects);
    }

    private IEnumerator ExecuteWorkAmountLoop(
        int runId,
        CharacterActor actor,
        BuildableObject target,
        FacilityWorkType workType,
        float requiredWork,
        string label,
        float extraMultiplier = 1f)
    {
        requiredWork = Mathf.Max(0.1f, requiredWork);
        label = string.IsNullOrWhiteSpace(label) ? WorkTaskCatalog.GetLegacyDisplayName(workType) : label;
        if (debugRules.IsEnabled(DungeonDebugCheat.InstantWork))
        {
            RecordApprovedWork(
                requiredWork,
                actor,
                allowAccident: false,
                remainingWork: 0f);
            actor?.Brain?.SetActionPhase($"{label} 100%", target);
            yield return null;
            yield break;
        }

        WorkTypeId workTypeId = FacilityWorkTypeMap.TryGet(
                workType,
                out WorkTypeDefinition definition)
            ? definition.WorkTypeId
            : default;
        float completedWork = TryConsumeResumedGenericProgress(
            workTypeId,
            target,
            requiredWork);
        float durationMultiplier = work.GetWorkEnvironmentDurationMultiplier(workTypeId);
        float lastReportTime = -10f;
        float lastNeedUpdateTime = gameClock.Time;
        genericProgressActive = true;
        genericProgressWorkTypeId = workTypeId;
        genericProgressTargetId = target?.PersistentInstanceId.Value ?? string.Empty;
        genericProgressCompletedWork = completedWork;
        genericProgressRequiredWork = requiredWork;
        bool enteredTimedLoop = false;
        try
        {
        while (completedWork + 0.001f < requiredWork
            && CanContinueTimedWork(runId, actor, target)
            && work.isWorking)
        {
            enteredTimedLoop = true;
            // Generic timed handlers (repair, operate and other authored
            // ExecuteWorkAmount users) are real emergency checkpoints too.
            // Previously only persistent/work-order loops consumed the
            // suspension request, so these jobs ignored a committed Red alert
            // until they happened to finish.
            if (TrySuspendAtSafeCheckpoint(actor, target, workTypeId))
            {
                RecordPreWuExit(
                    WorkPreWuExitKind.EmergencySuspension,
                    CaptureTimedWorkGateDetail(runId, actor, target));
                yield break;
            }

            if (ApplyTimedWorkNeedsAndInterrupt(
                    actor,
                    ref lastNeedUpdateTime,
                    out string interruptReason))
            {
                RecordPreWuExit(
                    WorkPreWuExitKind.RoutineNeedInterrupt,
                    "reason=" + interruptReason + "; "
                    + CaptureTimedWorkGateDetail(runId, actor, target));
                yield break;
            }

            float remainingSeconds =
                Mathf.Max(0f, requiredWork - completedWork)
                / Mathf.Max(
                    0.05f,
                    CalculateWorkPerSecond(
                        workAmountCalculator,
                        actor,
                        target,
                        workTypeId,
                        durationMultiplier));
            if (ShouldInterruptForEnvironment(
                    actor,
                    target,
                    workTypeId,
                    remainingSeconds))
            {
                RecordPreWuExit(
                    WorkPreWuExitKind.EnvironmentInterrupt,
                    CaptureTimedWorkGateDetail(runId, actor, target));
                yield break;
            }

            float tickDeltaTime = gameClock.DeltaTime > 0f
                ? gameClock.DeltaTime
                : 1f / 60f;
            float deltaWork = CalculateWorkPerSecond(
                    workAmountCalculator,
                    actor,
                    target,
                    workTypeId,
                    durationMultiplier)
                * Mathf.Max(0.05f, extraMultiplier)
                * tickDeltaTime;
            deltaWork = Mathf.Min(requiredWork - completedWork, deltaWork);
            completedWork = Mathf.Min(requiredWork, completedWork + deltaWork);
            genericProgressCompletedWork = completedWork;
            RecordApprovedWork(
                deltaWork,
                actor,
                remainingWork: Mathf.Max(0f, requiredWork - completedWork),
                approvedWorkTypeId: workTypeId);
            if (gameClock.Time - lastReportTime >= 0.75f)
            {
                lastReportTime = gameClock.Time;
                float ratio = Mathf.Clamp01(completedWork / requiredWork);
                actor?.Brain?.SetActionPhase($"{label} {Mathf.RoundToInt(ratio * 100f)}%", target);
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Progress,
                    $"{label} 진행 {Mathf.RoundToInt(ratio * 100f)}%",
                    target,
                    reasonCode: "work-progress",
                    value: ratio));
            }

            yield return null;
        }
        }
        finally
        {
            if (!enteredTimedLoop)
            {
                RecordPreWuExit(
                    WorkPreWuExitKind.TimedLoopConditionRejected,
                    "completed=" + completedWork.ToString("0.###")
                    + "/" + requiredWork.ToString("0.###") + "; "
                    + CaptureTimedWorkGateDetail(runId, actor, target));
            }
            genericProgressActive = false;
            genericProgressWorkTypeId = default;
            genericProgressTargetId = string.Empty;
            genericProgressCompletedWork = 0f;
            genericProgressRequiredWork = 0f;
        }
    }

    private IEnumerator ExecutePersistentWorkAmountLoop(
        int runId,
        CharacterActor actor,
        BuildableObject target,
        FacilityWorkType workType,
        float requiredWork,
        float completedWork,
        string label,
        Func<float, bool> applyDelta,
        float extraMultiplier = 1f)
    {
        requiredWork = Mathf.Max(0.1f, requiredWork);
        completedWork = Mathf.Clamp(completedWork, 0f, requiredWork);
        label = string.IsNullOrWhiteSpace(label)
            ? WorkTaskCatalog.GetLegacyDisplayName(workType)
            : label;
        WorkTypeId workTypeId = FacilityWorkTypeMap.TryGet(
                workType,
                out WorkTypeDefinition definition)
            ? definition.WorkTypeId
            : work.AssignedWorkTypeId;

        if (debugRules.IsEnabled(DungeonDebugCheat.InstantWork))
        {
            float remainingWork = Mathf.Max(0f, requiredWork - completedWork);
            if (remainingWork > 0f)
            {
                if (applyDelta(remainingWork))
                {
                    RecordApprovedWork(
                        remainingWork,
                        actor,
                        allowAccident: false,
                        remainingWork: 0f,
                        approvedWorkTypeId: workTypeId);
                }
            }

            actor?.Brain?.SetActionPhase($"{label} 100%", target);
            yield return null;
            yield break;
        }

        float durationMultiplier =
            work.GetWorkEnvironmentDurationMultiplier(workTypeId);
        float lastReportTime = -10f;
        float lastNeedUpdateTime = gameClock.Time;

        while (completedWork + 0.001f < requiredWork
            && CanContinueTimedWork(runId, actor, target)
            && work.isWorking)
        {
            if (ApplyTimedWorkNeedsAndInterrupt(
                    actor,
                    ref lastNeedUpdateTime,
                    out string interruptReason))
            {
                RecordPreWuExit(
                    WorkPreWuExitKind.RoutineNeedInterrupt,
                    "reason=" + interruptReason + "; "
                    + CaptureTimedWorkGateDetail(runId, actor, target));
                yield break;
            }

            if (TrySuspendAtSafeCheckpoint(actor, target, workTypeId))
            {
                yield break;
            }

            float remainingSeconds =
                Mathf.Max(0f, requiredWork - completedWork)
                / Mathf.Max(
                    0.05f,
                    CalculateWorkPerSecond(
                        workAmountCalculator,
                        actor,
                        target,
                        workTypeId,
                        durationMultiplier));
            if (ShouldInterruptForEnvironment(
                    actor,
                    target,
                    workTypeId,
                    remainingSeconds))
            {
                yield break;
            }

            float tickDeltaTime = gameClock.DeltaTime > 0f
                ? gameClock.DeltaTime
                : 1f / 60f;
            float deltaWork = Mathf.Min(
                requiredWork - completedWork,
                CalculateWorkPerSecond(
                        workAmountCalculator,
                        actor,
                        target,
                        workTypeId,
                        durationMultiplier)
                    * Mathf.Max(0.05f, extraMultiplier)
                    * tickDeltaTime);
            if (deltaWork <= 0f || !applyDelta(deltaWork))
            {
                yield break;
            }

            completedWork = Mathf.Min(requiredWork, completedWork + deltaWork);
            RecordApprovedWork(
                deltaWork,
                actor,
                remainingWork: Mathf.Max(0f, requiredWork - completedWork),
                approvedWorkTypeId: workTypeId);
            if (gameClock.Time - lastReportTime >= 0.75f)
            {
                lastReportTime = gameClock.Time;
                float ratio = Mathf.Clamp01(completedWork / requiredWork);
                actor?.Brain?.SetActionPhase(
                    $"{label} {Mathf.RoundToInt(ratio * 100f)}%",
                    target);
                actor?.AddActivity(CharacterActivityEvent.Work(
                    workType,
                    CharacterActivityOutcomes.Progress,
                    $"{label} 진행 {Mathf.RoundToInt(ratio * 100f)}%",
                    target,
                    reasonCode: "persistent-work-progress",
                    value: ratio));
            }

            yield return null;
        }
    }

    private bool ApplyTimedWorkNeedsAndInterrupt(
        CharacterActor actor,
        ref float lastNeedUpdateTime,
        out string interruptReason)
    {
        interruptReason = string.Empty;
        float currentTime = gameClock.Time;
        float elapsed = Mathf.Max(0f, currentTime - lastNeedUpdateTime);
        lastNeedUpdateTime = currentTime;
        if (elapsed > 0f)
        {
            work.ApplyWorkNeedDepletion(elapsed);
        }

        if (!work.ShouldInterruptCurrentWork(out interruptReason))
        {
            return false;
        }

        work.MarkCurrentWorkInterruptedFromExecutor(interruptReason);
        actor?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Duty,
            CharacterActivityOutcomes.Changed,
            $"생리 욕구로 작업 중단: {interruptReason}",
            reasonCode: "routine-need-interrupt"));
        return true;
    }

    private bool ShouldInterruptForEnvironment(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        float remainingSeconds)
    {
        if (environmentWorkPolicy == null
            || actor == null
            || gameClock.Time < nextEnvironmentRecheckAt)
        {
            return false;
        }

        nextEnvironmentRecheckAt = gameClock.Time + 1f;
        EnvironmentalWorkKind workKind =
            WorkExecutionRules.ResolveEnvironmentWorkKind(workTypeId);
        if (workKind is EnvironmentalWorkKind.EmergencySurgery
            or EnvironmentalWorkKind.Defense
            or EnvironmentalWorkKind.Safety)
        {
            return false;
        }

        WorkEnvironmentAssessment assessment =
            environmentWorkPolicy.RecheckActive(
                actor,
                actor.GetNowXY(),
                remainingSeconds,
                workKind,
                forced: false);
        EnvironmentalExposureBand actualBand =
            (EnvironmentalExposureBand)Mathf.Max(
                (int)characterEnvironment.GetPhysiologicalBand(
                    new CharacterId(actor.Identity?.PersistentId)),
                (int)characterEnvironment.GetVisualBand(
                    new CharacterId(actor.Identity?.PersistentId)));
        bool evacuate = assessment.Projection.HasLethalChannel
            || actualBand >= EnvironmentalExposureBand.Critical;
        bool reassign = actualBand >= EnvironmentalExposureBand.Impaired;
        if (!evacuate && !reassign)
        {
            return false;
        }

        string reason;
        if (evacuate)
        {
            Grid grid = work.WorkGridResolver.ResolveActiveGrid(work, null);
            if (environmentWorkPolicy.TryFindEvacuationCell(
                    actor,
                    grid,
                    out Vector2Int safeCell,
                    out bool fullySafe,
                    out string evacuationWarning)
                && work.WorkerMove != null)
            {
                work.WorkerMove.TryStartSystemMove(
                    safeCell,
                    DoorAccessOverrideKind.None,
                    out string moveMessage);
                reason = fullySafe
                    ? $"환경 위험으로 작업 중단, ({safeCell.x},{safeCell.y}) 대피"
                    : $"{evacuationWarning} {moveMessage}";
            }
            else
            {
                reason = string.IsNullOrWhiteSpace(evacuationWarning)
                    ? "안전한 대피 경로 없음"
                    : evacuationWarning;
            }
        }
        else
        {
            reason =
                $"환경 노출 {actualBand}: 진행률을 보존하고 안전한 인력 재배정을 요청합니다.";
        }

        actor.Brain?.SetActionPhase(reason, target);
        actor.AddActivity(CharacterActivityEvent.Work(
            work.AssignedWorkType,
            CharacterActivityOutcomes.Blocked,
            reason,
            target,
            reasonCode: evacuate
                ? "environment-evacuation"
                : "environment-reassignment",
            bubbleEligible: true));
        environmentInterrupted = true;
        work.isWorking = false;
        if (actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }

        return true;
    }

    private static void EndAiAction(
        CharacterActor actor,
        AIAction currentAction,
        CharacterAiActionTerminalKind terminalKind =
            CharacterAiActionTerminalKind.Completed)
    {
        currentAction?.ReleaseReservation(actor);
        AIBrain brain = actor?.Brain;
        if (brain != null
            && (currentAction != null
                ? ReferenceEquals(brain.bestAction, currentAction)
                : brain.bestAction == null))
        {
            if (currentAction != null)
            {
                brain.EndExpectedAction(
                    currentAction,
                    terminalKind,
                    clearFailures:
                        terminalKind == CharacterAiActionTerminalKind.Completed);
            }
            else
            {
                brain.isBestActionEnd = true;
            }
        }
    }

    private void FinishWorkRun(CharacterActor actor, AIAction currentAction)
    {
        ReleaseActiveRestockLease(ItemReservationReleaseReason.Completed);
        CompleteEmergencyAccounting("completed");
        work.RecordSuccessfulWorkAttempt(work.AssignedWorkTypeId);
        CharacterSkillRuntimeEffects.EndWork(actor);
        characterEnvironment.ClearWorkContext(
            new CharacterId(actor?.Identity?.PersistentId));
        ReturnEnvironmentalWorkwear(actor);
        bool wasPriorityTarget = work.assignedShop == work.PriorityWorkTarget;
        currentAction?.ReleaseReservation(actor);
        work.AssignWork(null, FacilityWorkType.None);
        if (wasPriorityTarget)
        {
            work.ClearPriorityWorkTarget();
        }

        EndAiAction(actor, currentAction);
    }

    private bool ShouldAbortWorkRun(int runId, CharacterActor actor)
    {
        return !work.IsActiveWorkRun(runId)
            || actor == null
            || actor.Brain == null
            || workAccidentOccurred
            || actor.Brain.isBestActionEnd;
    }

    private bool ShouldAbortWorkRun(
        int runId,
        CharacterActor actor,
        BuildableObject expectedTarget)
    {
        return ShouldAbortWorkRun(runId, actor)
            || expectedTarget == null
            || expectedTarget.isDestroy
            || !expectedTarget.gameObject.activeInHierarchy
            || work.assignedShop != expectedTarget;
    }

    private bool CanContinueTimedWork(int runId)
    {
        return runId <= 0 || work.IsActiveWorkRun(runId);
    }

    private bool CanContinueTimedWork(int runId, CharacterActor actor)
    {
        if (!CanContinueTimedWork(runId))
        {
            return false;
        }

        if (runId <= 0)
        {
            return true;
        }

        return actor != null
            && actor.Brain != null
            && !actor.Brain.isBestActionEnd;
    }

    private bool CanContinueTimedWork(
        int runId,
        CharacterActor actor,
        BuildableObject expectedTarget)
    {
        if (!CanContinueTimedWork(runId, actor))
            return false;
        return expectedTarget != null
            && !expectedTarget.isDestroy
            && expectedTarget.gameObject.activeInHierarchy
            && work.assignedShop == expectedTarget;
    }

    internal void RecordRoutineApprovedWorkTime(
        CharacterActor actor,
        float elapsedGameSeconds,
        float remainingShiftSeconds)
    {
        WorkTypeId workTypeId = work.AssignedWorkTypeId;
        BuildableObject target = work.assignedShop;
        if (elapsedGameSeconds <= 0f
            || !workTypeId.IsValid
            || workTypeId == BuiltInWorkTypeIds.Rest
            || target == null
            || target.isDestroy
            || !target.gameObject.activeInHierarchy)
        {
            return;
        }

        float approvedWork = WorkExecutionRules.CalculateWorkPerSecond(
                workAmountCalculator,
                actor,
                target,
                workTypeId,
                work.GetWorkEnvironmentDurationMultiplier(workTypeId))
            * elapsedGameSeconds;
        RecordApprovedWork(
            approvedWork,
            actor,
            remainingWork: Mathf.Max(0f, remainingShiftSeconds));
    }

    private void AbortWorkRun(
        int runId,
        CharacterActor actor,
        AIAction currentAction,
        CharacterAiActionTerminalKind terminalKind =
            CharacterAiActionTerminalKind.Cancelled)
    {
        ReleaseActiveRestockLease(ItemReservationReleaseReason.Cancelled);
        if (emergencySuspended)
        {
            CompleteEmergencyAccounting("suspended-for-emergency");
            AwardApprovedWork(actor, ProficiencyWorkOutcome.PartialSuccess);
            RecordSpeciesCompletedWork(
                actor,
                work.AssignedWorkTypeId,
                approvedProficiencyWork);
            CharacterSkillRuntimeEffects.EndWork(actor);
            characterEnvironment.ClearWorkContext(
                new CharacterId(actor?.Identity?.PersistentId));
            currentAction?.ReleaseReservation(actor);
            work.isWorking = false;
            if (work.IsActiveWorkRun(runId))
            {
                work.AssignWork(null, FacilityWorkType.None);
            }
            EndAiAction(actor, currentAction, terminalKind);
            work.ClearActiveWorkRoutine(runId);
            emergencySuspended = false;
            return;
        }

        bool routineNeedInterrupted =
            work.LastWorkRunInterruptedForRoutineNeed;
        CompleteEmergencyAccounting(
            workAccidentOccurred
                ? "accident"
                : environmentInterrupted
                    ? "environment-interrupted"
                    : routineNeedInterrupted
                        ? "routine-need-suspended"
                        : "aborted");
        WorkTypeId interruptedWorkTypeId = routineNeedInterrupted
            ? work.RoutineNeedResumeWorkTypeId
            : work.AssignedWorkTypeId;
        BuildableObject interruptedTarget = routineNeedInterrupted
            ? work.RoutineNeedResumeTarget
            : work.assignedShop;
        if (!routineNeedInterrupted)
        {
            work.RecordFailedWorkAttempt(work.AssignedWorkTypeId);
        }
        AwardApprovedWork(
            actor,
            routineNeedInterrupted
                ? ProficiencyWorkOutcome.PartialSuccess
                : environmentInterrupted || workAccidentOccurred
                ? ProficiencyWorkOutcome.AccidentOrForcedStop
                : ProficiencyWorkOutcome.SafeFailure);
        if (!routineNeedInterrupted)
        {
            PublishWorkCompleted(
                actor,
                interruptedWorkTypeId,
                workAccidentOccurred
                    ? "outcome:accident"
                    : environmentInterrupted
                        ? "outcome:environment-interrupted"
                        : "outcome:failure");
        }
        else
        {
            RecordSpeciesCompletedWork(
                actor,
                interruptedWorkTypeId,
                approvedProficiencyWork);
            actor?.AddActivity(CharacterActivityEvent.Work(
                interruptedWorkTypeId,
                CharacterActivityOutcomes.Changed,
                "생리 욕구 해결 후 재개하도록 작업 진행 상태 보존",
                interruptedTarget,
                reasonCode: "routine-need-suspended"));
        }
        CharacterSkillRuntimeEffects.EndWork(actor);
        characterEnvironment.ClearWorkContext(
            new CharacterId(actor?.Identity?.PersistentId));
        if (!environmentInterrupted)
        {
            ReturnEnvironmentalWorkwear(actor);
        }
        currentAction?.ReleaseReservation(actor);
        work.isWorking = false;
        if (work.IsActiveWorkRun(runId))
        {
            work.AssignWork(null, FacilityWorkType.None);
        }

        // Abort is a terminal executor path just like successful completion.
        // Routine-need interruption previously cleared the coroutine and work
        // ownership without ending the matching AI action, producing an
        // orphan Work action (brain executed, no work routine) indefinitely.
        EndAiAction(actor, currentAction, terminalKind);
        work.ClearActiveWorkRoutine(runId);
    }

    private void RecordApprovedWork(
        float amount,
        CharacterActor actor,
        bool allowAccident = true,
        float remainingWork = -1f,
        WorkTypeId approvedWorkTypeId = default)
    {
        if (amount > 0f && !float.IsNaN(amount) && !float.IsInfinity(amount))
        {
            WorkTypeId workTypeId = approvedWorkTypeId.IsValid
                ? approvedWorkTypeId
                : work.AssignedWorkTypeId;
            RecordEmergencyAccounting(
                actor,
                amount,
                remainingWork,
                workTypeId);
            approvedProficiencyWork += amount;
            work.RecordApprovedWorkProgressForDiagnostics(amount);
            PublishWorkStarted(actor, workTypeId);
            if (allowAccident)
                TryTriggerWorkAccident(actor, amount, workTypeId);
        }
    }

    private void RecordPreWuExit(
        WorkPreWuExitKind kind,
        string detail)
    {
        if (kind == WorkPreWuExitKind.None
            || approvedProficiencyWork > 0f
            || lastPreWuExitKind != WorkPreWuExitKind.None)
        {
            return;
        }

        lastPreWuExitKind = kind;
        lastPreWuExitDetail = detail?.Trim() ?? string.Empty;
    }

    private static bool RequiresCommonWorkOrderRoute(WorkTypeId workTypeId)
    {
        if (!workTypeId.IsValid)
        {
            return false;
        }

        IReadOnlyList<WorkExecutionFailureProfile> profiles =
            BuiltInWorkExecutionFailureProfiles.All;
        for (int index = 0; index < profiles.Count; index++)
        {
            WorkExecutionFailureProfile profile = profiles[index];
            if (profile.WorkTypeId == workTypeId)
            {
                return profile.Route == WorkExecutorRoute.CommonWorkOrder;
            }
        }

        return false;
    }

    private static string FormatWorkOrderExecutionDetail(
        string stage,
        BuildableObject target,
        WorkTypeId workTypeId,
        string expectedWorkOrderId,
        WorkOrderProgressState order)
    {
        string targetId = target != null && target.PersistentInstanceId.IsValid
            ? target.PersistentInstanceId.Value
            : target != null
                ? target.name + "@" + target.GetInstanceID()
                : "missing";
        return "stage=" + (stage ?? string.Empty)
            + ",workType=" + workTypeId.Value
            + ",target=" + targetId
            + ",expected=" + (expectedWorkOrderId ?? string.Empty)
            + ",actual=" + (order?.WorkOrderId ?? "missing")
            + ",status=" + (order?.Status.ToString() ?? "missing")
            + ",remaining=" + (order != null
                ? Mathf.Max(0f, order.RequiredWork - order.CompletedWork)
                    .ToString("0.###")
                : "missing");
    }

    private string CaptureTimedWorkGateDetail(
        int runId,
        CharacterActor actor,
        BuildableObject target)
    {
        return "run=" + runId + "/" + currentRunId
            + ",activeRun=" + work.IsActiveWorkRun(runId)
            + ",actor=" + (actor != null)
            + ",brain=" + (actor?.Brain != null)
            + ",actionEnd=" + (actor?.Brain?.isBestActionEnd ?? false)
            + ",target=" + (target != null)
            + ",destroyed=" + (target == null || target.isDestroy)
            + ",active="
            + (target != null && target.gameObject.activeInHierarchy)
            + ",assigned=" + ReferenceEquals(work.assignedShop, target)
            + ",working=" + work.isWorking
            + ",accident=" + workAccidentOccurred
            + ",emergencyRequested=" + emergencySuspensionRequested
            + ",emergencyEpoch=" + requestedEmergencyEpochId
            + ",environmentInterrupted=" + environmentInterrupted;
    }

    public void CancelActiveRun(CharacterActor actor, string reason)
    {
        ReleaseActiveRestockLease(ItemReservationReleaseReason.Cancelled);
        ReleaseActiveExecutionCancellationResource();
        CompleteEmergencyAccounting(
            string.IsNullOrWhiteSpace(reason) ? "cancelled" : reason.Trim());
        CharacterSkillRuntimeEffects.EndWork(actor);
        characterEnvironment.ClearWorkContext(
            new CharacterId(actor?.Identity?.PersistentId));
        ReturnEnvironmentalWorkwear(actor);
    }

    private void RegisterActiveExecutionCancellationResource(
        IDisposable resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));
        ReleaseActiveExecutionCancellationResource();
        activeExecutionCancellationResource = resource;
    }

    private void ReleaseActiveExecutionCancellationResource()
    {
        IDisposable resource = activeExecutionCancellationResource;
        activeExecutionCancellationResource = null;
        resource?.Dispose();
    }

    public bool RequestEmergencySuspension(long alertEpochId)
    {
        WorkTypeId workTypeId = work.AssignedWorkTypeId;
        if (alertEpochId <= 0L
            || !work.isWorking
            || work.assignedShop == null
            || !workTypeId.IsValid
            || !WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            || (definition.EmergencyFlags & EmergencyWorkFlags.ReserveEligible) == 0
            || (definition.EmergencyFlags
                & (EmergencyWorkFlags.InterruptImmediately
                    | EmergencyWorkFlags.InterruptAtCheckpoint)) == 0)
        {
            return false;
        }

        emergencySuspensionRequested = true;
        requestedEmergencyEpochId = alertEpochId;
        return true;
    }

    public bool CancelEmergencySuspensionRequest(long alertEpochId)
    {
        if (alertEpochId <= 0L
            || requestedEmergencyEpochId != alertEpochId
            || emergencySuspended
            || pendingSuspensionReceipt.IsValid)
        {
            return false;
        }

        emergencySuspensionRequested = false;
        requestedEmergencyEpochId = 0L;
        return true;
    }

    public bool TryConsumeEmergencySuspension(
        out EmergencyWorkSuspensionReceipt receipt)
    {
        receipt = pendingSuspensionReceipt;
        if (!receipt.IsValid)
        {
            return false;
        }

        pendingSuspensionReceipt = default;
        requestedEmergencyEpochId = 0L;
        return true;
    }

    public void RestoreInlineEmergencyProgress(
        WorkTypeId workTypeId,
        string targetBuildingId,
        float completedWork,
        float requiredWork)
    {
        if (!workTypeId.IsValid
            || string.IsNullOrWhiteSpace(targetBuildingId)
            || completedWork < 0f
            || requiredWork <= 0f
            || completedWork >= requiredWork
            || float.IsNaN(completedWork)
            || float.IsInfinity(completedWork)
            || float.IsNaN(requiredWork)
            || float.IsInfinity(requiredWork))
        {
            throw new ArgumentOutOfRangeException(
                nameof(completedWork),
                "Inline emergency progress must be finite and incomplete.");
        }
        resumedGenericProgressPending = true;
        resumedGenericWorkTypeId = workTypeId;
        resumedGenericTargetId = targetBuildingId.Trim();
        resumedGenericCompletedWork = completedWork;
        resumedGenericRequiredWork = requiredWork;
    }

    private float TryConsumeResumedGenericProgress(
        WorkTypeId workTypeId,
        BuildableObject target,
        float requiredWork)
    {
        if (!resumedGenericProgressPending
            || resumedGenericWorkTypeId != workTypeId
            || target == null
            || !string.Equals(
                resumedGenericTargetId,
                target.PersistentInstanceId.Value,
                StringComparison.Ordinal)
            || !Mathf.Approximately(
                resumedGenericRequiredWork,
                requiredWork))
        {
            return 0f;
        }
        float resumed = Mathf.Clamp(
            resumedGenericCompletedWork,
            0f,
            Mathf.Max(0f, requiredWork - 0.001f));
        resumedGenericProgressPending = false;
        resumedGenericWorkTypeId = default;
        resumedGenericTargetId = string.Empty;
        resumedGenericCompletedWork = 0f;
        resumedGenericRequiredWork = 0f;
        return resumed;
    }

    private bool TrySuspendAtSafeCheckpoint(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId)
    {
        if (!emergencySuspensionRequested
            || requestedEmergencyEpochId <= 0L
            || !work.isWorking
            || workAccidentOccurred
            || actor == null
            || actor.Brain == null
            || actor.Brain.isBestActionEnd
            || target == null
            || !workTypeId.IsValid
            || !target.PersistentInstanceId.IsValid)
        {
            return false;
        }

        bool hasInlineProgress = genericProgressActive
            && genericProgressWorkTypeId == workTypeId
            && string.Equals(
                genericProgressTargetId,
                target.PersistentInstanceId.Value,
                StringComparison.Ordinal)
            && genericProgressRequiredWork > 0f
            && genericProgressCompletedWork >= 0f
            && genericProgressCompletedWork < genericProgressRequiredWork;
        pendingSuspensionReceipt = new EmergencyWorkSuspensionReceipt(
            workTypeId,
            target.PersistentInstanceId.Value,
            requestedEmergencyEpochId,
            progressExternallyPersisted: !hasInlineProgress,
            inlineCompletedWork: hasInlineProgress
                ? genericProgressCompletedWork
                : 0f,
            inlineRequiredWork: hasInlineProgress
                ? genericProgressRequiredWork
                : 0f);
        emergencySuspensionRequested = false;
        emergencySuspended = true;
        // The suspension receipt is now the sole authority for restoring this
        // explicit command after Green. Leaving the live priority installed
        // lets the scheduler reacquire the same ordinary work between this
        // checkpoint and the alarm runtime consuming the receipt, defeating
        // both Red suspension and Amber hold semantics.
        work.ClearPriorityWorkTarget();
        work.isWorking = false;
        actor.Brain?.SetActionPhase(
            "비상 대응을 위해 안전 지점에서 작업 일시중단",
            target);
        actor.AddActivity(CharacterActivityEvent.Work(
            workTypeId,
            CharacterActivityOutcomes.Changed,
            "비상 대응을 위해 진행 상태를 보존하고 작업을 일시중단했습니다.",
            target,
            reasonCode: "suspended-for-emergency"));
        actor.Brain.SuspendCurrentActionAtSafeCheckpoint(
            "suspended-for-emergency");
        return true;
    }

    private void RecordEmergencyAccounting(
        CharacterActor actor,
        float approvedWork,
        float remainingWork,
        WorkTypeId workTypeId)
    {
        if (emergencyWorkAccounting == null)
        {
            return;
        }

        if (!workTypeId.IsValid
            || !WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
        {
            throw new InvalidOperationException(
                "Emergency work accounting requires a registered work type and persistent worker identity: "
                + $"approvedWorkType={workTypeId.Value}; "
                + $"assignedWorkType={work.AssignedWorkTypeId.Value}; "
                + $"actor={actor?.Identity?.PersistentId ?? "<null>"}; "
                + $"run={currentRunId}.");
        }

        float knownRemaining = remainingWork >= 0f
            ? remainingWork
            : EmergencyWuUnits.ToWu(EmergencyWuUnits.MaximumReserveWindowMilliWu);
        long remainingMilliWu = EmergencyWuUnits.FromWu(knownRemaining);
        long approvedMilliWu = ConvertApprovedWorkToMilliWu(approvedWork);
        latestApprovedLaborMilliWu = approvedMilliWu;
        if (string.IsNullOrWhiteSpace(activeEmergencyOperationId))
        {
            string operationId =
                $"work:{characterId.Value}:{currentRunId}:{workTypeId.Value}";
            long initialRemaining = checked(
                remainingMilliWu + approvedMilliWu);
            long reserve = (definition.EmergencyFlags & EmergencyWorkFlags.ReserveEligible) != 0
                ? Math.Min(initialRemaining, EmergencyWuUnits.MaximumReserveWindowMilliWu)
                : 0L;
            EmergencyAccountingResult registration = emergencyWorkAccounting.Register(
                new EmergencyWorkLedgerEntry(
                    operationId,
                    characterId.Value,
                    workTypeId,
                    definition.EmergencyFlags,
                    initialRemaining,
                    reserve,
                    classificationRevision: 0,
                    mutationSequence: emergencyAccountingSequence));
            RequireEmergencyAccountingSuccess(registration);
            // Registration is the ownership commit point. Do not retain a
            // candidate operation ID when Register fails, otherwise cleanup
            // later attempts to remove an operation the ledger never owned and
            // hides the original lifecycle fault behind OperationMissing noise.
            activeEmergencyOperationId = operationId;
        }

        emergencyAccountingSequence = checked(emergencyAccountingSequence + 1L);
        EmergencyAccountingResult progress = emergencyWorkAccounting.ApplyProgress(
            new EmergencyWorkProgress(
                activeEmergencyOperationId,
                approvedMilliWu,
                remainingMilliWu,
                emergencyAccountingSequence));
        RequireEmergencyAccountingSuccess(progress);

        if (settlementLaborAccounting != null)
        {
            EmergencyAccountingResult labor = settlementLaborAccounting.Record(
                new SettlementLaborContribution(
                    activeEmergencyOperationId,
                    emergencyAccountingSequence,
                    SettlementLaborContributionChannel.ActualLabor,
                    approvedMilliWu,
                    workTypeId.Value));
            RequireEmergencyAccountingSuccess(labor);
            if (SettlementLaborBalanceRules.TryGetMaintenanceChannel(
                    workTypeId,
                    out SettlementLaborContributionChannel maintenanceChannel))
            {
                EmergencyAccountingResult maintenance =
                    settlementLaborAccounting.Record(
                        new SettlementLaborContribution(
                            activeEmergencyOperationId,
                            emergencyAccountingSequence,
                            maintenanceChannel,
                            approvedMilliWu,
                            workTypeId.Value));
                RequireEmergencyAccountingSuccess(maintenance);
            }
        }
    }

    private long ConvertApprovedWorkToMilliWu(float approvedWork)
    {
        if (float.IsNaN(approvedWork)
            || float.IsInfinity(approvedWork)
            || approvedWork < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(approvedWork),
                approvedWork,
                "Approved work must be finite and non-negative.");
        }

        // Work progresses in small floating-point deltas. Rounding every tick
        // independently accumulated measurable drift against the authoritative
        // physical project progress. Carry the sub-milli remainder within the
        // active operation so the integer ledger tracks the rounded cumulative
        // total instead of the sum of rounded fragments.
        double exactMilliWu = approvedWork * EmergencyWuUnits.UnitsPerWu
            + actualLaborMilliWuCarry;
        long wholeMilliWu = checked((long)Math.Floor(exactMilliWu + 1e-9d));
        actualLaborMilliWuCarry = exactMilliWu - wholeMilliWu;
        return wholeMilliWu;
    }

    private void CompleteEmergencyAccounting(string reason)
    {
        if (emergencyWorkAccounting == null
            || string.IsNullOrWhiteSpace(activeEmergencyOperationId))
        {
            return;
        }

        emergencyAccountingSequence = checked(emergencyAccountingSequence + 1L);
        string operationId = activeEmergencyOperationId;
        activeEmergencyOperationId = string.Empty;
        // Preserve the cumulative sub-milli carries across operation boundaries.
        // They are consumed by the next approved work delta for this actor.
        latestApprovedLaborMilliWu = 0L;
        EmergencyAccountingResult result = emergencyWorkAccounting.Remove(
            new EmergencyWorkCompletion(
                operationId,
                $"{operationId}:{reason}",
                emergencyAccountingSequence));
        RequireEmergencyAccountingSuccess(result);
    }

    private void RecordProjectOutputAdjustment(
        float outputEquivalentWork,
        float actualLaborWork,
        WorkTypeId workTypeId)
    {
        if (settlementLaborAccounting == null
            || string.IsNullOrWhiteSpace(activeEmergencyOperationId)
            || !workTypeId.IsValid)
        {
            return;
        }

        double exactOutputMilliWu = outputEquivalentWork
            * EmergencyWuUnits.UnitsPerWu
            + projectOutputMilliWuCarry;
        long outputMilliWu = checked((long)Math.Floor(exactOutputMilliWu + 1e-9d));
        projectOutputMilliWuCarry = exactOutputMilliWu - outputMilliWu;
        long laborMilliWu = latestApprovedLaborMilliWu;
        long difference = checked(outputMilliWu - laborMilliWu);
        if (difference == 0L)
        {
            return;
        }

        SettlementLaborContributionChannel channel = difference > 0L
            ? SettlementLaborContributionChannel.ConvertedProcessOutput
            : SettlementLaborContributionChannel.FuelMaintenanceAccidentSpoilageLoss;
        EmergencyAccountingResult result = settlementLaborAccounting.Record(
            new SettlementLaborContribution(
                activeEmergencyOperationId,
                emergencyAccountingSequence,
                channel,
                Math.Abs(difference),
                workTypeId.Value));
        RequireEmergencyAccountingSuccess(result);
    }

    private static void RequireEmergencyAccountingSuccess(
        EmergencyAccountingResult result)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"{result.Code}: {result.Message}");
        }
    }

    private void PublishWorkStarted(CharacterActor actor, WorkTypeId workTypeId)
    {
        if (workStartedPublished
            || identityEvents == null
            || !workTypeId.IsValid
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id))
            return;
        workStartedPublished = true;
        identityEvents.Publish(new WorkStartedIdentityEvent(
            id,
            workTypeId.Value,
            ResolveCommandOrigin(),
            calendar?.Day ?? 0));
    }

    private void PublishWorkCompleted(
        CharacterActor actor,
        WorkTypeId workTypeId,
        string productId)
    {
        if (!workStartedPublished
            || identityEvents == null
            || !workTypeId.IsValid
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id))
            return;
        identityEvents.Publish(new WorkCompletedIdentityEvent(
            id,
            workTypeId.Value,
            productId,
            ResolveCommandOrigin(),
            calendar?.Day ?? 0));
    }

    private CharacterCommandOrigin ResolveCommandOrigin() =>
        work.PriorityWorkTarget != null
        && work.assignedShop == work.PriorityWorkTarget
            ? CharacterCommandOrigin.DirectPlayerOrder
            : CharacterCommandOrigin.Autonomous;

    private bool TryTriggerWorkAccident(
        CharacterActor actor,
        float approvedWork,
        WorkTypeId approvedWorkTypeId)
    {
        if (workAccidentOccurred
            || actor == null
            || workAccidentRandom == null
            || approvedWork <= 0f)
        {
            return false;
        }

        if (!approvedWorkTypeId.IsValid)
            throw new InvalidOperationException(
                "Work accident execution requires the approved work type captured before progress commit.");
        if (performance == null || performanceContext == null)
            throw new InvalidOperationException(
                "Work accident execution requires the character performance query.");
        if (!performanceContext.TryResolve(
                actor,
                work.assignedShop,
                approvedWorkTypeId,
                out ProficiencyWorkProfile profile,
                out string failureReason))
            throw new InvalidOperationException(failureReason);
        CharacterPerformanceSnapshot accident = performance.EvaluateWork(
            actor,
            approvedWorkTypeId,
            CharacterPerformanceResultChannel.AccidentRisk,
            performanceContext.BuildEvaluationContext(
                profile,
                new GameplayEffectContext(new[] { approvedWorkTypeId.Value })));
        if (!accident.IsApplicable)
            throw new InvalidOperationException(
                accident.Failure?.Message
                ?? $"Work accident performance '{approvedWorkTypeId.Value}' is unavailable.");
        float accidentMultiplier = Mathf.Max(0f, accident.Value);
        float chance = 1f - Mathf.Exp(
            -BaseAccidentHazardPerApprovedWorkUnit
            * approvedWork
            * accidentMultiplier);
        CharacterPerformanceExecutionTrace.Record(
            accident.FormulaId,
            "WorkTaskExecutor.TryTriggerWorkAccident",
            approvedWork,
            chance,
            approvedWorkTypeId.Value);
        if (!workAccidentRandom.Chance(Mathf.Clamp01(chance)))
            return false;

        workAccidentOccurred = true;
        work.isWorking = false;
        AnatomyNodeHealthState[] eligibleNodes = anatomyHealth
            .GetAnatomySnapshot(actor)
            .Nodes
            .Where(value => value != null
                && !value.missing
                && value.currentHealth > 0f)
            .OrderBy(value => value.nodeId, StringComparer.Ordinal)
            .ToArray();
        if (eligibleNodes.Length == 0)
            throw new InvalidOperationException(
                $"Work accident cannot resolve an anatomy node for '{actor.name}'.");
        AnatomyNodeHealthState injured = eligibleNodes[
            workAccidentRandom.NextInt(0, eligibleNodes.Length)];
        if (!anatomyHealth.TryDamageNode(
                actor,
                injured.nodeId,
                WorkAccidentDamage,
                bleeding: 0f,
                reason: "work-accident"))
            throw new InvalidOperationException(
                $"Work accident failed to damage anatomy node '{injured.nodeId}'.");
        actor.AddActivity(CharacterActivityEvent.Work(
            approvedWorkTypeId,
            CharacterActivityOutcomes.Failed,
            "작업 사고로 작업이 중단됨",
            work.assignedShop,
            reasonCode: "work-accident",
            bubbleEligible: true));
        return true;
    }

    private float CalculateWorkPerSecond(
        IWorkAmountCalculator calculator,
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        float environmentDurationMultiplier)
    {
        float baseRate = WorkExecutionRules.CalculateWorkPerSecond(
            calculator,
            actor,
            target,
            workTypeId,
            environmentDurationMultiplier);
        return WorkRateBoundsAuthority.Clamp(baseRate);
    }

    private void AwardApprovedWork(
        CharacterActor actor,
        ProficiencyWorkOutcome outcome)
    {
        if (proficiencyAwarded
            || approvedProficiencyWork <= 0f
            || proficiencyCommands == null
            || calendar == null
            || !TryResolveProficiencyProfile(
                actor,
                work.assignedShop,
                work.AssignedWorkTypeId,
                out ProficiencyWorkProfile profile)
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
        {
            return;
        }

        proficiencyAwarded = true;
        float standingExperienceMultiplier = settlementStandings
            ?.GetApprovedWorkExperienceMultiplier(actor) ?? 1f;
        proficiencyCommands.AddApprovedWork(
            characterId,
            profile,
            approvedProficiencyWork * Mathf.Clamp01(
                standingExperienceMultiplier),
            difficultyMultiplier: ResolveDifficultyMultiplier(
                actor,
                work.assignedShop,
                work.AssignedWorkTypeId,
                profile),
            outcome: outcome,
            learningMultiplier:
                CharacterProficiencyLearningRules.Resolve(
                    actor,
                    profile,
                    work.AssignedWorkTypeId),
            repetitionMultiplier: work.AssignedWorkTypeId
                == BuiltInWorkTypeIds.Dismantle
                    ? Mathf.Min(0.20f, proficiencyRepetitionMultiplier)
                    : proficiencyRepetitionMultiplier,
            absoluteHour: calendar.AbsoluteHour);
    }

    private void RecordSpeciesCompletedWork(
        CharacterActor actor,
        WorkTypeId workTypeId,
        float completedWork)
    {
        if (completedWork <= 0f)
            return;
        if (!CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
            throw new InvalidOperationException(
                "Species completed-work projection requires a persistent character id.");
        if (!speciesCommands.RecordCompletedWork(
                characterId,
                workTypeId.Value,
                completedWork,
                out DomainFailure failure))
            throw new InvalidOperationException(
                $"Species completed-work projection failed: {failure.Code}");
    }

    private bool TryResolveProficiencyProfile(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        out ProficiencyWorkProfile profile)
    {
        ProficiencyWorkProfileAuthoring authored = ResolveAuthoredProfile(
            target,
            workTypeId);
        if (authored?.IsValid == true)
        {
            if (authored.CombinationMode == ProficiencyCombinationMode.Higher)
            {
                profile = ResolveHigherCombatProfile(actor);
                return profile.IsValid;
            }
            profile = new ProficiencyWorkProfile(
                authored.Primary,
                authored.Secondary,
                authored.PrimaryWeight);
            return profile.IsValid;
        }
        if (workTypeId == BuiltInWorkTypeIds.Operate)
        {
            profile = default;
            return false;
        }
        if (!WorkTypeProficiencyRules.TryResolve(workTypeId, out profile))
        {
            return false;
        }

        if (workTypeId == BuiltInWorkTypeIds.Hunt)
        {
            CharacterProficiencyId combat =
                BuiltInCharacterProficiencyIds.MeleeCombat;
            if (actor != null
                && combatEquipmentRuntime != null
                && combatEquipmentRuntime.TryGetActiveWeapon(
                    actor.Identity?.PersistentId ?? string.Empty,
                    out CombatWeaponSnapshot weapon)
                && weapon?.IsRanged == true)
            {
                combat = BuiltInCharacterProficiencyIds.RangedCombat;
            }
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.FoodProduction,
                combat,
                0.80f);
        }
        else if (workTypeId == BuiltInWorkTypeIds.Warden
            && actor != null
            && proficiencyQuery != null
            && calendar != null
            && CharacterPersistentIdentity.TryGet(
                actor,
                out CharacterId characterId))
        {
            proficiencyQuery.TryGetProficiency(
                characterId,
                BuiltInCharacterProficiencyIds.MeleeCombat,
                calendar.AbsoluteHour,
                out CharacterProficiencySnapshot melee);
            proficiencyQuery.TryGetProficiency(
                characterId,
                BuiltInCharacterProficiencyIds.RangedCombat,
                calendar.AbsoluteHour,
                out CharacterProficiencySnapshot ranged);
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Social,
                ranged.CurrentExperience > melee.CurrentExperience
                    ? BuiltInCharacterProficiencyIds.RangedCombat
                    : BuiltInCharacterProficiencyIds.MeleeCombat,
                0.80f);
        }
        else if (workTypeId == BuiltInWorkTypeIds.Craft
            && IsRuneCraftTarget(target))
        {
            profile = new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.Crafting,
                BuiltInCharacterProficiencyIds.Scholarship,
                0.80f);
        }
        return true;
    }

    private float ResolveDifficultyMultiplier(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        ProficiencyWorkProfile profile)
    {
        ProficiencyWorkProfileAuthoring authored = ResolveAuthoredProfile(
            target,
            workTypeId);
        if (authored?.IsValid != true
            || proficiencyQuery == null
            || calendar == null
            || actor == null
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            || !proficiencyQuery.TryGetProficiency(
                id,
                profile.Primary,
                calendar.AbsoluteHour,
                out CharacterProficiencySnapshot current))
        {
            return 1f;
        }

        int rankDifference = (int)current.Rank - (int)authored.RecommendedRank;
        if (rankDifference < 0) return 1.25f;
        if (rankDifference == 0) return 1f;
        if (rankDifference == 1) return 0.55f;
        return 0.20f;
    }

    private static ProficiencyWorkProfileAuthoring ResolveAuthoredProfile(
        BuildableObject target,
        WorkTypeId workTypeId)
    {
        BuildingSO building = target?.BuildingData;
        if (building == null) return null;
        if (workTypeId == BuiltInWorkTypeIds.Operate)
            return building.OperationProficiency;
        if (workTypeId == BuiltInWorkTypeIds.Construct
            || workTypeId == BuiltInWorkTypeIds.Repair
            || workTypeId == BuiltInWorkTypeIds.Plumbing
            || workTypeId == BuiltInWorkTypeIds.Dismantle
            || workTypeId == BuiltInWorkTypeIds.GrandProject)
            return building.ConstructionProficiency;
        return null;
    }

    private ProficiencyWorkProfile ResolveHigherCombatProfile(
        CharacterActor actor)
    {
        if (actor == null || proficiencyQuery == null || calendar == null
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id))
        {
            return new ProficiencyWorkProfile(
                BuiltInCharacterProficiencyIds.MeleeCombat);
        }
        proficiencyQuery.TryGetProficiency(
            id,
            BuiltInCharacterProficiencyIds.MeleeCombat,
            calendar.AbsoluteHour,
            out CharacterProficiencySnapshot melee);
        proficiencyQuery.TryGetProficiency(
            id,
            BuiltInCharacterProficiencyIds.RangedCombat,
            calendar.AbsoluteHour,
            out CharacterProficiencySnapshot ranged);
        return new ProficiencyWorkProfile(
            ranged.CurrentMilliExperience > melee.CurrentMilliExperience
                ? BuiltInCharacterProficiencyIds.RangedCombat
                : BuiltInCharacterProficiencyIds.MeleeCombat);
    }

    private static float ResolveRepeatPracticeMultiplier(int attemptIndex)
    {
        int attemptNumber = Mathf.Max(0, attemptIndex) + 1;
        if (attemptNumber <= 3) return 1f;
        if (attemptNumber <= 10) return 0.50f;
        return 0.15f;
    }

    private static bool IsRuneCraftTarget(BuildableObject target)
    {
        string name = target?.BuildingData?.objectName ?? string.Empty;
        return name.IndexOf("rune", StringComparison.OrdinalIgnoreCase) >= 0
            || name.Contains("룬", StringComparison.Ordinal);
    }

    private void ReturnEnvironmentalWorkwear(CharacterActor actor)
    {
        CharacterId characterId = new(actor?.Identity?.PersistentId);
        if (!characterId.IsValid)
        {
            return;
        }

        if (!environmentalWorkwearCommands.TryUnequip(
            characterId,
            out DomainFailure failure)
            && failure.Code != FailureCode.EnvironmentWorkwearNotEquipped)
        {
            Debug.LogWarning(
                $"[환경 작업복] {characterId.Value} 자동 반납 실패: "
                + failure.Code);
        }
    }

}
