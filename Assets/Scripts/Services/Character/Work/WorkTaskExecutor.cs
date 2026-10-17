using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public sealed class WorkTaskExecutor
{

    private readonly AbilityWork work;
    private readonly IWorkExecutionHandlerRegistry executionHandlers;
    private readonly IWorkOrderRuntime workOrderRuntime;
    private readonly IWorkAmountCalculator workAmountCalculator;
    private readonly IGameClock gameClock;
    private readonly IRoomEnvironmentExperienceService roomEnvironmentExperienceService;
    private readonly IPaidFacilityContractRuntime paidFacilityContracts;
    private readonly ICharacterEnvironmentWorkContext characterEnvironment;
    private readonly IEnvironmentWorkPolicy environmentWorkPolicy;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly WorkRunLaborAccounting laborAccounting;
    private readonly WorkProficiencyProgress proficiencyProgress;
    private readonly WorkRunRuntime run;
    private readonly RestockWorkExecution restock;
    private readonly RestockCargoOperation restockCargo;
    private WorkPreWuExitKind lastPreWuExitKind;
    private string lastPreWuExitDetail = string.Empty;
    private string lastWorkOrderExecutionDetail = string.Empty;

    internal double ActualLaborMilliWuCarryForDiagnostics => laborAccounting.ActualLaborMilliWuCarry;
    internal bool HasActiveGenericProgressForDiagnostics =>
        run.HasActiveInlineProgress;
    internal float GenericCompletedWorkForDiagnostics =>
        run.InlineCompletedWork;
    internal float GenericRequiredWorkForDiagnostics =>
        run.InlineRequiredWork;
    internal bool HasPendingResumedGenericProgressForDiagnostics =>
        run.HasPendingResumedInlineProgress;
    internal WorkPreWuExitKind LastPreWuExitKindForDiagnostics =>
        lastPreWuExitKind;
    internal string LastPreWuExitDetailForDiagnostics =>
        lastPreWuExitDetail;
    internal string LastWorkOrderExecutionDetailForDiagnostics =>
        lastWorkOrderExecutionDetail;
    internal string LastWorkAccidentFireFailureDetailForDiagnostics =>
        run.LastWorkAccidentFireFailure;

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
        ICharacterSettlementStandingQuery settlementStandings = null,
        ICharacterBodyHealthMutationTransaction bodyHealthMutation = null,
        IDomainFactPublisher accidentFacts = null,
        WorkAccidentObservationRuntime workAccidentObservations = null)
    {
        core = core ?? throw new ArgumentNullException(nameof(core));
        execution = execution ?? throw new ArgumentNullException(nameof(execution));
        environment = environment ?? throw new ArgumentNullException(nameof(environment));
        work = core.Work;
        gameClock = core.GameClock;
        restockCargo = new RestockCargoOperation(gameClock);
        debugRules = core.DebugRules;
        executionHandlers = execution.ExecutionHandlers;
        workOrderRuntime = execution.WorkOrderRuntime;
        workAmountCalculator = execution.WorkAmountCalculator;
        paidFacilityContracts = execution.PaidFacilityContracts;
        roomEnvironmentExperienceService = environment.RoomEnvironmentExperienceService;
        characterEnvironment = environment.CharacterEnvironment;
        environmentWorkPolicy = environment.EnvironmentWorkPolicy;
        this.identityEvents = identityEvents;
        performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        performanceContext = performanceContext
            ?? throw new ArgumentNullException(nameof(performanceContext));
        anatomyHealth = anatomyHealth
            ?? throw new ArgumentNullException(nameof(anatomyHealth));
        proficiencyProgress = new WorkProficiencyProgress(work, proficiencyCommands, proficiencyQuery,
            calendar, combatEquipmentRuntime, speciesCommands, settlementStandings);
        laborAccounting = new WorkRunLaborAccounting(emergencyWorkAccounting, settlementLaborAccounting);
        bodyHealthMutation = bodyHealthMutation
            ?? throw new ArgumentNullException(nameof(bodyHealthMutation));
        accidentFacts = accidentFacts ?? throw new ArgumentNullException(nameof(accidentFacts));
        workAccidentObservations = workAccidentObservations
            ?? throw new ArgumentNullException(nameof(workAccidentObservations));
        var accidents = new WorkAccidentExecutionRuntime(work, execution, calendar,
            workAccidentRandom, identityEvents, performance, performanceContext,
            anatomyHealth, bodyHealthMutation, accidentFacts, workAccidentObservations);
        run = new WorkRunRuntime(core, execution, environment, calendar, identityEvents,
            restockCargo, laborAccounting, proficiencyProgress, accidents);
        restock = new RestockWorkExecution(core, execution, environment.EnvironmentWorkPolicy,
            reservedItemTransfers, identityEvents, restockCargo, proficiencyProgress, run);
    }

    internal void RebindTargetSelector(WorkTargetSelector selector) =>
        restock.RebindTargetSelector(selector);

    internal bool HasRestockOperation => restockCargo.OperationId.Length != 0;

    internal void RestoreRestockOwnership(CharacterActor actor, ItemQuantityLease lease,
        IRetailRestockOperationOwner owner) => restockCargo.Restore(actor, lease, owner);

    internal void RelinquishRestockForWorldRestore(string expectedOperationId = null) =>
        restockCargo.RelinquishForWorldRestore(expectedOperationId);

    public IEnumerator Work(int runId)
    {
        run.BeginRun(runId);
        if (!work.IsActiveWorkRun(runId))
            yield break;
        lastPreWuExitKind = WorkPreWuExitKind.None;
        lastPreWuExitDetail = string.Empty;
        lastWorkOrderExecutionDetail = string.Empty;
        run.BeginDutyWorkRun();
        CharacterActor actor = work.WorkerActor;
        AIAction currentAction = actor != null && actor.Brain != null
            ? actor.Brain.bestAction
            : null;

        work.EnsureWorkReferences();
        AbilityMove move = work.WorkerMove;
        Grid grid = work.WorkGridResolver.ResolveActiveGrid(work, null);
        if (move == null || grid == null)
        {
            WorkExecutionChangedEvent.Publish(identityEvents, actor,
                WorkExecutionTransition.MovementUnavailable,
                work.AssignedWorkType, work.assignedShop);
            work.isWorking = false;
            WorkRunRuntime.EndAiAction(actor, currentAction);
            work.ClearActiveWorkRoutine(runId);
            yield break;
        }

        run.BeginActiveWork();
        if (work.AssignedWorkType == FacilityWorkType.Restock)
        {
            bool restockCompleted = false;
            yield return restock.ExecuteRestockHaulWork(
                runId,
                currentAction,
                move,
                grid,
                success => restockCompleted = success);
            if (work.IsActiveWorkRun(runId))
            {
                if (restockCompleted)
                {
                    run.FinishWorkRun(actor, currentAction);
                    work.ClearActiveWorkRoutine(runId);
                }
                else
                {
                    run.AbortWorkRun(
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
            WorkRunRuntime.EndAiAction(
                actor,
                currentAction,
                CharacterAiActionTerminalKind.Failed);
            work.ClearActiveWorkRoutine(runId);
            yield break;
        }

        if (environmentWorkPolicy != null
            && actor != null
            && plannedTarget != null
            && plannedWorkTypeId.IsValid)
        {
            GridMoveStep[] confirmedApproach = grid
                .SearchPath(actor.GetNowXY())
                .GetMovePathTo(plannedTarget.centerPos)
                .ToArray();
            WorkEnvironmentAssessment preparation =
                environmentWorkPolicy.PrepareActiveWork(
                    actor,
                    plannedTarget.centerPos,
                    confirmedApproach,
                    expectedSeconds: 0f,
                    WorkExecutionRules.ResolveEnvironmentWorkKind(
                        plannedWorkTypeId),
                    forced: ReferenceEquals(
                        work.PriorityWorkTarget,
                        plannedTarget));
            if (!preparation.CanStart)
            {
                actor.Brain?.ReportRuntimeActionFailure(
                    AIActionFailure.Create(
                        AIActionFailureKind.CannotStart,
                        "environment-apparel-approach-blocked",
                        plannedTarget),
                    preparation.Failure,
                    CharacterOperationBlockAxis.Environment,
                    requestImmediateReplan: false);
                WorkExecutionChangedEvent.Publish(identityEvents, actor,
                    WorkExecutionTransition.ApproachProtectionUnavailable,
                    work.AssignedWorkType, plannedTarget);
                run.ReturnEnvironmentalWorkwear(actor);
                work.isWorking = false;
                work.AssignWork(null, FacilityWorkType.None);
                WorkRunRuntime.EndAiAction(
                    actor,
                    currentAction,
                    CharacterAiActionTerminalKind.Failed);
                work.ClearActiveWorkRoutine(runId);
                yield break;
            }
        }

        yield return move.MoveByCurrentBestActionPath();
        if (run.ShouldAbortWorkRun(runId, actor) || !work.isWorking)
        {
            run.AbortWorkRun(runId, actor, currentAction);
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
            WorkExecutionChangedEvent.Publish(identityEvents, actor,
                WorkExecutionTransition.ApproachedOrderReplaced,
                plannedWorkTypeId, assignedTarget);
            run.ReturnEnvironmentalWorkwear(actor);
            work.isWorking = false;
            work.AssignWork(null, FacilityWorkType.None);
            WorkRunRuntime.EndAiAction(
                actor,
                currentAction,
                CharacterAiActionTerminalKind.Failed);
            work.ClearActiveWorkRoutine(runId);
            yield break;
        }
        IWorkExecutionHandler plannedExecutionHandler = null;
        bool requiresWorkAccessStand = executionHandlers != null
            && plannedWorkTypeId.IsValid
            && executionHandlers.TryGet(
                plannedWorkTypeId,
                out plannedExecutionHandler)
            && plannedExecutionHandler is IWorkAccessStandExecutionHandler
                accessStandHandler
            && accessStandHandler.RequiresWorkAccessStand(
                plannedWorkTypeId,
                actor,
                assignedTarget);
        if (HasReachedAssignedWorkTarget(actor, grid)
            && assignedTarget is IWorkableFacility facility)
        {
            IBuildingVisitorPort visitor = actor?.BuildingVisitor;
            IBuildingWorkerAllocation workerAllocation = null;
            if (!requiresWorkAccessStand)
            {
                if (!facility.TryAllocateWorker(visitor, currentAction,
                        () => !run.ShouldAbortWorkRun(runId, actor, assignedTarget)
                            && work.isWorking
                            && ReferenceEquals(actor.Brain.bestAction, currentAction),
                        out workerAllocation, out string allocationFailure))
                {
                    if (work.IsActiveWorkRun(runId))
                    {
                        actor.Brain?.ReportRuntimeActionFailure(AIActionFailure.Create(
                            AIActionFailureKind.CannotStart,
                            "worker-allocation-rejected:" + allocationFailure, assignedTarget),
                            requestImmediateReplan: false);
                        run.AbortWorkRun(runId, actor, currentAction);
                    }
                    yield break;
                }
                if (!run.TryRegisterWorkerAllocation(runId, workerAllocation))
                    yield break;
                yield return workerAllocation.Approach();
            }
            if (run.ShouldAbortWorkRun(runId, actor)
                || !work.isWorking
                || work.assignedShop != assignedTarget
                || !requiresWorkAccessStand && !workerAllocation.IsReady)
            {
                workerAllocation?.Dispose();
                run.AbortWorkRun(runId, actor, currentAction);
                yield break;
            }

            if (!requiresWorkAccessStand)
            {
                currentAction?.ReleaseReservation(actor);
            }
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
                proficiencyProgress.SetQualityAttemptIndex(paidOrder.QualityAttemptIndex);
            }

            bool completedImmediately = false;
            bool completedSuccessfully = true;
            bool completionEffectsAlreadyApplied = false;
            bool completionIdentityAlreadyPublished = false;
            string executionTerminalOwnerId = string.Empty;
            string executionChildResultKey = string.Empty;
            string executionTargetEntityId = string.Empty;
            float? executionContributionWorkUnits = null;
            string executionFailureCode = string.Empty;
            DomainFailure executionDomainFailure = DomainFailure.None;
            CharacterOperationBlockAxis executionFailureAxis =
                CharacterOperationBlockAxis.Unknown;
            Func<int> executionFailureSourceRevisionReader = null;
            int executionFailureSourceRevision = 0;
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
            float preparationSeconds = 0f;
            if (executionOrderMatchesPlan)
            {
                float workerRate = WorkExecutionRules.CalculateWorkPerSecond(
                    workAmountCalculator,
                    actor,
                    assignedTarget,
                    workTypeId,
                    environmentDurationMultiplier: 1f);
                preparationSeconds = Mathf.Max(
                    0f,
                    executionOrder.RequiredWork - executionOrder.CompletedWork)
                    / Mathf.Max(0.05f, workerRate);
            }
            else if (WorkExecutionRules.TryGetExteriorWorkSeconds(
                         assignedTarget,
                         actor,
                         workTypeId,
                         out float knownExteriorSeconds))
            {
                preparationSeconds = knownExteriorSeconds;
            }
            characterEnvironment.SetWorkContext(
                new CharacterId(actor?.Identity?.PersistentId),
                WorkExecutionRules.ResolveEnvironmentWorkKind(workTypeId));
            WorkEnvironmentAssessment activePreparation =
                environmentWorkPolicy.PrepareActiveWork(
                    actor,
                    assignedTarget.centerPos,
                    Array.Empty<GridMoveStep>(),
                    preparationSeconds,
                    WorkExecutionRules.ResolveEnvironmentWorkKind(workTypeId),
                    forced: ReferenceEquals(
                        work.PriorityWorkTarget,
                        assignedTarget));
            if (!activePreparation.CanStart)
            {
                actor.Brain?.ReportRuntimeActionFailure(
                    AIActionFailure.Create(
                        AIActionFailureKind.CannotStart,
                        "environment-apparel-work-start-blocked",
                        assignedTarget),
                    activePreparation.Failure,
                    CharacterOperationBlockAxis.Environment,
                    requestImmediateReplan: false);
                WorkExecutionChangedEvent.Publish(identityEvents, actor,
                    WorkExecutionTransition.WorkProtectionUnavailable,
                    workType, assignedTarget);
                characterEnvironment.ClearWorkContext(
                    new CharacterId(actor?.Identity?.PersistentId));
                run.ReturnEnvironmentalWorkwear(actor);
                workerAllocation?.Dispose();
                work.isWorking = false;
                WorkRunRuntime.EndAiAction(
                    actor,
                    currentAction,
                    CharacterAiActionTerminalKind.Failed);
                work.ClearActiveWorkRoutine(runId);
                yield break;
            }
            if (paidFacilityContracts != null
                && !paidFacilityContracts.TryChargeOrder(
                    assignedTarget,
                    paidOrderKey,
                    out string paidFailureReason))
            {
                WorkExecutionChangedEvent.Publish(identityEvents, actor,
                    WorkExecutionTransition.PaidFacilityRejected,
                    workType, assignedTarget,
                    workLabel: workDefinition?.DisplayName ?? "작업", detail: paidFailureReason);
                characterEnvironment.ClearWorkContext(
                    new CharacterId(actor?.Identity?.PersistentId));
                run.ReturnEnvironmentalWorkwear(actor);
                workerAllocation?.Dispose();
                work.isWorking = false;
                WorkRunRuntime.EndAiAction(
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
            WorkControlChangedEvent.PublishAssigned(actor,
                WorkControlTransition.AssignedWorkStarted);
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
                if (run.ShouldAbortWorkRun(runId, actor))
                {
                    workerAllocation?.Dispose();
                    run.AbortWorkRun(runId, actor, currentAction);
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
                if (run.ShouldAbortWorkRun(runId, actor))
                {
                    workerAllocation?.Dispose();
                    run.AbortWorkRun(runId, actor, currentAction);
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
                    () => run.CanContinueTimedWork(
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
                    (amount, remainingWork) => run.RecordApprovedWork(
                        amount,
                        actor,
                        remainingWork: remainingWork),
                    () => run.TrySuspendAtSafeCheckpoint(
                        actor,
                        assignedTarget,
                        workTypeId),
                    run.RegisterActiveExecutionCancellationResource,
                    run.ResolveCommandOrigin(),
                    () => proficiencyProgress.ApprovedWork);
                yield return executionHandler.Execute(executionContext, executionResult);
                run.ReleaseActiveExecutionCancellationResource();
                completedSuccessfully = executionResult.CompletedSuccessfully;
                completionEffectsAlreadyApplied =
                    executionResult.CompletionEffectsAlreadyApplied;
                completionIdentityAlreadyPublished =
                    executionResult.CompletionIdentityAlreadyPublished
                    || executionResult.CompletionIdentityOwnedByDomain;
                executionTerminalOwnerId = executionResult.TerminalOwnerId;
                executionChildResultKey = executionResult.ChildResultKey;
                executionTargetEntityId = executionResult.TargetEntityId;
                executionContributionWorkUnits =
                    executionResult.ContributionWorkUnits;
                executionDomainFailure = executionResult.Failure;
                executionFailureAxis = executionResult.FailureAxis;
                executionFailureSourceRevisionReader =
                    executionResult.FailureSourceRevisionReader;
                executionFailureSourceRevision =
                    executionResult.FailureSourceRevision;
                if (run.ShouldAbortWorkRun(runId, actor))
                {
                    workerAllocation?.Dispose();
                    run.AbortWorkRun(runId, actor, currentAction);
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
                    run.AbortWorkRun(runId, actor, currentAction);
                    yield break;
                }

                work.ClearActiveWorkCheckRoutine(runId);
                completedSuccessfully = work.LastWorkRunCompleted;
            }
            else
            {
                work.isWorking = false;
                if (completedSuccessfully)
                    WorkControlChangedEvent.PublishAssigned(actor,
                        WorkControlTransition.WorkAmountFinished);
            }

            // Dismantle completion intentionally removes its own target. All
            // other removals are external invalidations and remain failures.
            // Cache identity above and never dereference Unity fake-null during
            // successful dismantle finalization.
            bool expectedDismantleRemoval =
                (workTypeId == BuiltInWorkTypeIds.Dismantle
                    || workTypeId == BuiltInWorkTypeIds.Construct)
                && completedSuccessfully
                && completionEffectsAlreadyApplied;
            if (!expectedDismantleRemoval
                && (assignedTarget == null
                    || !assignedTarget.gameObject.activeInHierarchy))
            {
                laborAccounting.Complete("target-destroyed-after-execution");
                actor?.Brain?.ReportRuntimeActionFailure(
                    AIActionFailure.Create(
                        AIActionFailureKind.Destroyed,
                        "work-target-destroyed-or-inactive-after-execution",
                        assignedTarget),
                    requestImmediateReplan: false);
                CharacterSkillRuntimeEffects.EndWork(actor);
                characterEnvironment.ClearWorkContext(
                    new CharacterId(actor?.Identity?.PersistentId));
                run.ReturnEnvironmentalWorkwear(actor);
                workerAllocation?.Dispose();
                work.isWorking = false;
                if (work.IsActiveWorkRun(runId))
                {
                    work.AssignWork(null, FacilityWorkType.None);
                }
                WorkRunRuntime.EndAiAction(
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
                && proficiencyProgress.ApprovedWork <= 0f)
            {
                RecordPreWuExit(
                    WorkPreWuExitKind.ExecutionHandlerRejected,
                    "workType=" + workTypeId.Value + "; "
                    + run.CaptureTimedWorkGateDetail(
                        runId,
                        actor,
                        assignedTarget));
            }
            if (completedSuccessfully)
            {
                proficiencyProgress.AwardApprovedWork(
                    actor,
                    ProficiencyWorkOutcome.Success,
                    string.IsNullOrWhiteSpace(expectedWorkOrderId)
                        ? "work-run:" + runId
                        : expectedWorkOrderId);
                proficiencyProgress.RecordSpeciesCompletedWork(
                    actor,
                    workTypeId,
                    proficiencyProgress.ApprovedWork,
                    string.IsNullOrWhiteSpace(expectedWorkOrderId)
                        ? "work-run:" + runId
                        : expectedWorkOrderId);
                if (WorkCompletionIdentityEmissionPolicy.ShouldPublishGeneric(
                        completionIdentityAlreadyPublished))
                {
                    run.PublishWorkCompleted(
                        actor,
                        workTypeId,
                        string.Empty,
                        runId,
                        executionTerminalOwnerId,
                        executionChildResultKey,
                        string.IsNullOrEmpty(executionTargetEntityId)
                            ? assignedTargetPersistentId
                            : executionTargetEntityId,
                        executionContributionWorkUnits
                            ?? proficiencyProgress.ApprovedWork);
                }
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
                        workTypeId,
                        proficiencyProgress.ApprovedWork);
                    roomEnvironmentExperienceService?.Apply(new RoomEnvironmentExperienceEvent(
                        actor,
                        assignedTarget,
                        RoomExperienceActivity.Work,
                        workTypeId));
                }
            }
            else if (routineNeedInterrupted)
            {
                proficiencyProgress.AwardApprovedWork(
                    actor,
                    ProficiencyWorkOutcome.PartialSuccess,
                    string.IsNullOrWhiteSpace(expectedWorkOrderId)
                        ? "work-run:" + runId
                        : expectedWorkOrderId);
                proficiencyProgress.RecordSpeciesCompletedWork(
                    actor,
                    workTypeId,
                    proficiencyProgress.ApprovedWork,
                    "work-run:" + runId + ":routine-checkpoint");
                WorkExecutionChangedEvent.Publish(identityEvents, actor,
                    WorkExecutionTransition.RoutineNeedSuspended,
                    workTypeId, assignedTarget);
            }
            else
            {
                proficiencyProgress.AwardApprovedWork(
                    actor,
                    ProficiencyWorkOutcome.SafeFailure,
                    string.IsNullOrWhiteSpace(expectedWorkOrderId)
                        ? "work-run:" + runId
                        : expectedWorkOrderId);
                if (WorkCompletionIdentityEmissionPolicy.ShouldPublishGeneric(
                        completionIdentityAlreadyPublished))
                {
                    run.PublishWorkCompleted(
                        actor,
                        workTypeId,
                        "outcome:failure",
                        runId,
                        executionTerminalOwnerId,
                        executionChildResultKey,
                        string.IsNullOrEmpty(executionTargetEntityId)
                            ? assignedTargetPersistentId
                            : executionTargetEntityId,
                        executionContributionWorkUnits
                            ?? proficiencyProgress.ApprovedWork);
                }
                actor?.Brain?.ReportRuntimeActionFailure(
                    AIActionFailure.Create(
                        AIActionFailureKind.ResourceUnavailable,
                        string.IsNullOrWhiteSpace(executionFailureCode)
                            ? $"work-execution-unavailable:{workTypeId.Value}"
                            : executionFailureCode,
                        assignedTarget),
                    executionDomainFailure,
                    executionFailureAxis,
                    executionFailureSourceRevisionReader,
                    executionFailureSourceRevision,
                    requestImmediateReplan: false);
            }

            actor?.AiMemory?.RecordWork(
                workTypeId,
                assignedTarget,
                completedSuccessfully,
                $"{WorkTaskCatalog.GetLegacyDisplayName(workType)} {(completedSuccessfully ? "완료" : "실패")}: {assignedTargetName}");
            laborAccounting.Complete(
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
            workerAllocation?.Dispose();
            work.AssignWork(null, FacilityWorkType.None);
            if (wasPriorityTarget && !routineNeedInterrupted)
            {
                work.ClearPriorityWorkTarget();
            }
            WorkRunRuntime.EndAiAction(
                actor,
                currentAction,
                completedSuccessfully || routineNeedInterrupted
                    ? CharacterAiActionTerminalKind.Completed
                    : CharacterAiActionTerminalKind.Failed);
        }
        else
        {
            work.isWorking = false;
            WorkExecutionChangedEvent.Publish(identityEvents, actor,
                WorkExecutionTransition.TargetUnreachable,
                work.AssignedWorkType, assignedTarget);
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
                DomainFailure.None,
                CharacterOperationBlockAxis.Access,
                requestImmediateReplan: false);
            WorkRunRuntime.EndAiAction(
                actor,
                currentAction,
                CharacterAiActionTerminalKind.Failed);
            work.ClearActiveWorkRoutine(runId);
            yield break;
        }

        WorkRunRuntime.EndAiAction(actor, currentAction);
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
            WorkExecutionChangedEvent.Publish(identityEvents, actor,
                WorkExecutionTransition.ConstructionWorkforceRejected,
                workType, target,
                workLabel: label, detail: workforceFailure ?? "건설 인력 권위 없음");
            onCompleted?.Invoke(false, false);
            yield break;
        }

        if (constructionLease != null)
        {
            // Unity may stop the owning work coroutine without advancing this
            // iterator again. Register the project slot with the executor's
            // synchronous cancellation authority so lifecycle/replan cleanup
            // cannot leave a ghost worker in the construction project.
            run.RegisterActiveExecutionCancellationResource(constructionLease);
        }

        try
        {
        while (run.CanContinueTimedWork(runId, actor, target)
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
            if (run.ApplyTimedWorkNeedsAndInterrupt(
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
                    + run.CaptureTimedWorkGateDetail(runId, actor, target);
                RecordPreWuExit(
                    WorkPreWuExitKind.RoutineNeedInterrupt,
                    "reason=" + interruptReason + "; "
                    + run.CaptureTimedWorkGateDetail(runId, actor, target));
                onCompleted?.Invoke(false, false);
                yield break;
            }

            if (run.TrySuspendAtSafeCheckpoint(actor, target, workTypeId))
            {
                lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                    "emergency-suspension-exit",
                    target,
                    workTypeId,
                    expectedWorkOrderId,
                    order) + "; "
                    + run.CaptureTimedWorkGateDetail(runId, actor, target);
                RecordPreWuExit(
                    WorkPreWuExitKind.EmergencySuspension,
                    run.CaptureTimedWorkGateDetail(runId, actor, target));
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
                        + run.CaptureTimedWorkGateDetail(runId, actor, target);
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
                        + run.CaptureTimedWorkGateDetail(runId, actor, target);
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
            if (run.ShouldInterruptForEnvironment(
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
                    + run.CaptureTimedWorkGateDetail(runId, actor, target);
                RecordPreWuExit(
                    WorkPreWuExitKind.EnvironmentInterrupt,
                    run.CaptureTimedWorkGateDetail(runId, actor, target));
                onCompleted?.Invoke(false, false);
                yield break;
            }

            if (order.Status == WorkOrderStatus.WaitingForMaterials)
            {
                WorkExecutionChangedEvent.Publish(identityEvents, actor,
                    WorkExecutionTransition.MaterialsPending,
                    workType, target, workLabel: label, progress: order.ProgressRatio);
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
                    + run.CaptureTimedWorkGateDetail(runId, actor, target);
                RecordPreWuExit(
                    WorkPreWuExitKind.WorkOrderApplyRejected,
                    (message ?? string.Empty) + "; "
                    + run.CaptureTimedWorkGateDetail(runId, actor, target));
                WorkExecutionChangedEvent.Publish(identityEvents, actor,
                    WorkExecutionTransition.WorkOrderRejected,
                    workType, target, workLabel: label, detail: message);
                onCompleted?.Invoke(false, false);
                yield break;
            }
            run.RecordApprovedWork(
                acceptedLaborWork,
                actor,
                remainingWork: Mathf.Max(
                    0f,
                    order.RequiredWork - order.CompletedWork - deltaWork)
                    / contributionMultiplier);
            laborAccounting.RecordProjectOutputAdjustment(
                deltaWork,
                workTypeId);

            if (gameClock.Time - lastReportTime >= 0.75f
                && workOrderRuntime.TryGetOrderFor(target, workTypeId, out order))
            {
                lastReportTime = gameClock.Time;
                actor?.Brain?.SetActionPhase($"{label} {Mathf.RoundToInt(order.ProgressRatio * 100f)}%", target);
                WorkExecutionChangedEvent.Publish(identityEvents, actor,
                    WorkExecutionTransition.ProgressAdvanced,
                    workType, target, workLabel: label, progress: order.ProgressRatio);
            }

            if (completed)
            {
                lastWorkOrderExecutionDetail = FormatWorkOrderExecutionDetail(
                    "routine-completed",
                    target,
                    workTypeId,
                    expectedWorkOrderId,
                    order) + ",effects=" + appliedCompletionEffects;
                WorkExecutionChangedEvent.Publish(identityEvents, actor,
                    WorkExecutionTransition.WorkOrderCompleted,
                    workType, target, workLabel: label, progress: 1f);
                onCompleted?.Invoke(true, appliedCompletionEffects);
                yield break;
            }

            yield return null;
        }
        }
        finally
        {
            run.CompleteExecutionCancellationResource(constructionLease);
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
            + run.CaptureTimedWorkGateDetail(runId, actor, target);
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
                + run.CaptureTimedWorkGateDetail(runId, actor, target));
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
            run.RecordApprovedWork(
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
        float completedWork = run.TryConsumeResumedGenericProgress(
            workTypeId,
            target,
            requiredWork);
        float durationMultiplier = work.GetWorkEnvironmentDurationMultiplier(workTypeId);
        float lastReportTime = -10f;
        float lastNeedUpdateTime = gameClock.Time;
        run.BeginInlineProgress(workTypeId, target, completedWork, requiredWork);
        bool enteredTimedLoop = false;
        try
        {
        while (completedWork + 0.001f < requiredWork
            && run.CanContinueTimedWork(runId, actor, target)
            && work.isWorking)
        {
            enteredTimedLoop = true;
            // Generic timed handlers (repair, operate and other authored
            // ExecuteWorkAmount users) are real emergency checkpoints too.
            // Previously only persistent/work-order loops consumed the
            // suspension request, so these jobs ignored a committed Red alert
            // until they happened to finish.
            if (run.TrySuspendAtSafeCheckpoint(actor, target, workTypeId))
            {
                RecordPreWuExit(
                    WorkPreWuExitKind.EmergencySuspension,
                    run.CaptureTimedWorkGateDetail(runId, actor, target));
                yield break;
            }

            if (run.ApplyTimedWorkNeedsAndInterrupt(
                    actor,
                    ref lastNeedUpdateTime,
                    out string interruptReason))
            {
                RecordPreWuExit(
                    WorkPreWuExitKind.RoutineNeedInterrupt,
                    "reason=" + interruptReason + "; "
                    + run.CaptureTimedWorkGateDetail(runId, actor, target));
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
            if (run.ShouldInterruptForEnvironment(
                    actor,
                    target,
                    workTypeId,
                    remainingSeconds))
            {
                RecordPreWuExit(
                    WorkPreWuExitKind.EnvironmentInterrupt,
                    run.CaptureTimedWorkGateDetail(runId, actor, target));
                yield break;
            }

            float tickDeltaTime = gameClock.DeltaTime;
            if (gameClock.IsPaused || tickDeltaTime <= 0f)
            {
                yield return null;
                continue;
            }
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
            run.AdvanceInlineProgress(completedWork);
            run.RecordApprovedWork(
                deltaWork,
                actor,
                remainingWork: Mathf.Max(0f, requiredWork - completedWork),
                approvedWorkTypeId: workTypeId);
            if (gameClock.Time - lastReportTime >= 0.75f)
            {
                lastReportTime = gameClock.Time;
                float ratio = Mathf.Clamp01(completedWork / requiredWork);
                actor?.Brain?.SetActionPhase($"{label} {Mathf.RoundToInt(ratio * 100f)}%", target);
                WorkExecutionChangedEvent.Publish(identityEvents, actor,
                    WorkExecutionTransition.ProgressAdvanced,
                    workType, target, workLabel: label, progress: ratio);
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
                    + run.CaptureTimedWorkGateDetail(runId, actor, target));
            }
            run.EndInlineProgress();
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
                    run.RecordApprovedWork(
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
            && run.CanContinueTimedWork(runId, actor, target)
            && work.isWorking)
        {
            if (run.ApplyTimedWorkNeedsAndInterrupt(
                    actor,
                    ref lastNeedUpdateTime,
                    out string interruptReason))
            {
                RecordPreWuExit(
                    WorkPreWuExitKind.RoutineNeedInterrupt,
                    "reason=" + interruptReason + "; "
                    + run.CaptureTimedWorkGateDetail(runId, actor, target));
                yield break;
            }

            if (run.TrySuspendAtSafeCheckpoint(actor, target, workTypeId))
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
            if (run.ShouldInterruptForEnvironment(
                    actor,
                    target,
                    workTypeId,
                    remainingSeconds))
            {
                yield break;
            }

            float tickDeltaTime = gameClock.DeltaTime;
            if (gameClock.IsPaused || tickDeltaTime <= 0f)
            {
                yield return null;
                continue;
            }
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
            run.RecordApprovedWork(
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
                WorkExecutionChangedEvent.Publish(identityEvents, actor,
                    WorkExecutionTransition.PersistentProgressAdvanced,
                    workType, target, workLabel: label, progress: ratio);
            }

            yield return null;
        }
    }

    internal void RecordRoutineApprovedWorkTime(
        CharacterActor actor,
        float elapsedGameSeconds,
        float remainingShiftSeconds) =>
        run.RecordRoutineApprovedWorkTime(actor, elapsedGameSeconds, remainingShiftSeconds);

    private void RecordPreWuExit(
        WorkPreWuExitKind kind,
        string detail)
    {
        if (kind == WorkPreWuExitKind.None
            || proficiencyProgress.ApprovedWork > 0f
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

    public void CancelActiveRun(CharacterActor actor, string reason) =>
        run.CancelActiveRun(actor, reason);

    public bool RequestEmergencySuspension(long alertEpochId) =>
        run.RequestEmergencySuspension(alertEpochId);

    public bool CancelEmergencySuspensionRequest(long alertEpochId) =>
        run.CancelEmergencySuspensionRequest(alertEpochId);

    public bool TryConsumeEmergencySuspension(
        out EmergencyWorkSuspensionReceipt receipt) =>
        run.TryConsumeEmergencySuspension(out receipt);

    public void RestoreInlineEmergencyProgress(
        WorkTypeId workTypeId,
        string targetBuildingId,
        float completedWork,
        float requiredWork) =>
        run.RestoreInlineEmergencyProgress(workTypeId, targetBuildingId, completedWork, requiredWork);

    public void RestoreEmergencySuspensionReceipt(
        in EmergencyWorkSuspensionReceipt receipt) =>
        run.RestoreEmergencySuspensionReceipt(in receipt);

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

}
