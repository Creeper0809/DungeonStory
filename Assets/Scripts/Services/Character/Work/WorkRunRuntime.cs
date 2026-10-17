using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>
/// Owns the shared work-run lifetime, approved labor and safe suspension receipts.
/// Normal work and restock use this same authority without calling their executor.
/// </summary>
internal sealed class WorkRunRuntime
{
    private readonly AbilityWork work;
    private readonly IWorkAmountCalculator workAmountCalculator;
    private readonly IGameClock gameClock;
    private readonly ICharacterEnvironmentWorkContext characterEnvironment;
    private readonly IEnvironmentalWorkwearCommand environmentalWorkwearCommands;
    private readonly IEnvironmentWorkPolicy environmentWorkPolicy;
    private readonly IGameCalendar calendar;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private float nextEnvironmentRecheckAt;
    private bool environmentInterrupted;
    private bool workStartedPublished;
    private int currentRunId;
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
    private readonly RestockCargoOperation restockCargo;
    private readonly WorkRunLaborAccounting laborAccounting;
    private readonly WorkProficiencyProgress proficiencyProgress;
    private IDisposable activeExecutionCancellationResource;
    private IBuildingWorkerAllocation activeWorkerAllocation;
    private readonly WorkAccidentExecutionRuntime accidents;

    internal WorkRunRuntime(WorkTaskCoreDependencies core, WorkTaskExecutionDependencies execution,
        WorkTaskEnvironmentDependencies environment, IGameCalendar calendar,
        CharacterIdentityEventPublisher identityEvents, RestockCargoOperation restockCargo,
        WorkRunLaborAccounting laborAccounting, WorkProficiencyProgress proficiencyProgress,
        WorkAccidentExecutionRuntime accidents)
    {
        work = core.Work;
        gameClock = core.GameClock;
        workAmountCalculator = execution.WorkAmountCalculator;
        characterEnvironment = environment.CharacterEnvironment;
        environmentalWorkwearCommands = environment.EnvironmentalWorkwearCommands;
        environmentWorkPolicy = environment.EnvironmentWorkPolicy;
        this.calendar = calendar;
        this.identityEvents = identityEvents;
        this.restockCargo = restockCargo;
        this.laborAccounting = laborAccounting;
        this.proficiencyProgress = proficiencyProgress;
        this.accidents = accidents;
    }

    internal bool HasActiveInlineProgress => genericProgressActive;
    internal float InlineCompletedWork => genericProgressCompletedWork;
    internal float InlineRequiredWork => genericProgressRequiredWork;
    internal bool HasPendingResumedInlineProgress => resumedGenericProgressPending;
    internal string LastWorkAccidentFireFailure => accidents.LastFailure;

    internal void BeginRun(int runId)
    {
        ReleaseWorkerAllocation();
        if (!work.IsActiveWorkRun(runId))
            return;
        restockCargo.Release(ItemReservationReleaseReason.Replanned);
        ReleaseActiveExecutionCancellationResource();
        // A new run is an ownership boundary. In normal flow AbilityWork stops
        // and settles the previous coroutine first, but restore/replan can leave
        // the coroutine reference cleared after its ledger operation was already
        // published. Never erase that operation ID without settling it: doing so
        // leaves the worker permanently owned by the old emergency ledger row
        // and the first approved WU of this run fails as WorkerAlreadyActive.
        laborAccounting.BeginRun();
        currentRunId = runId;
        emergencySuspensionRequested = false;
        emergencySuspended = false;
        requestedEmergencyEpochId = 0L;
        proficiencyProgress.BeginRun();
    }

    internal void BeginDutyWorkRun()
    {
        accidents.BeginRun();
        workStartedPublished = false;
        work.BeginDutyWorkRun();
    }

    internal void BeginActiveWork()
    {
        work.isWorking = true;
        environmentInterrupted = false;
        nextEnvironmentRecheckAt = gameClock.Time + 1f;
    }

    internal void BeginInlineProgress(WorkTypeId workTypeId, BuildableObject target,
        float completedWork, float requiredWork)
    {
        genericProgressActive = true;
        genericProgressWorkTypeId = workTypeId;
        genericProgressTargetId = target?.PersistentInstanceId.Value ?? string.Empty;
        genericProgressCompletedWork = completedWork;
        genericProgressRequiredWork = requiredWork;
    }

    internal void AdvanceInlineProgress(float completedWork) => genericProgressCompletedWork = completedWork;

    internal void EndInlineProgress()
    {
        genericProgressActive = false;
        genericProgressWorkTypeId = default;
        genericProgressTargetId = string.Empty;
        genericProgressCompletedWork = 0f;
        genericProgressRequiredWork = 0f;
    }

    internal void CompleteExecutionCancellationResource(IDisposable constructionLease)
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

    internal bool ApplyTimedWorkNeedsAndInterrupt(
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
        WorkExecutionChangedEvent.Publish(identityEvents, actor,
            WorkExecutionTransition.RoutineNeedInterrupted,
            default(WorkTypeId), detail: interruptReason);
        return true;
    }

    internal bool ShouldInterruptForEnvironment(
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
        actor.Brain?.ReportRuntimeActionFailure(
            AIActionFailure.Create(
                AIActionFailureKind.CannotStart,
                evacuate
                    ? "environment-exposure-critical"
                    : "environment-reassignment-required",
                target),
            assessment.Failure,
            CharacterOperationBlockAxis.Environment,
            requestImmediateReplan: false);
        WorkExecutionChangedEvent.Publish(identityEvents, actor,
            evacuate ? WorkExecutionTransition.EnvironmentEvacuation : WorkExecutionTransition.EnvironmentReassignment,
            work.AssignedWorkType, target, detail: reason);
        environmentInterrupted = true;
        work.isWorking = false;
        if (actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
        }

        return true;
    }

    internal static void EndAiAction(
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

    internal void FinishWorkRun(CharacterActor actor, AIAction currentAction)
    {
        restockCargo.Release(ItemReservationReleaseReason.Completed);
        laborAccounting.Complete("completed");
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

    internal bool ShouldAbortWorkRun(int runId, CharacterActor actor)
    {
        return !work.IsActiveWorkRun(runId)
            || actor == null
            || actor.Brain == null
            || accidents.Occurred
            || actor.Brain.isBestActionEnd;
    }

    internal bool ShouldAbortWorkRun(
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

    internal bool CanContinueTimedWork(int runId)
    {
        return runId <= 0 || work.IsActiveWorkRun(runId);
    }

    internal bool CanContinueTimedWork(int runId, CharacterActor actor)
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

    internal bool CanContinueTimedWork(
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

    internal void AbortWorkRun(
        int runId,
        CharacterActor actor,
        AIAction currentAction,
        CharacterAiActionTerminalKind terminalKind =
            CharacterAiActionTerminalKind.Cancelled)
    {
        if (currentRunId != runId || !work.IsActiveWorkRun(runId))
            return;
        ReleaseWorkerAllocation();
        if (currentRunId != runId || !work.IsActiveWorkRun(runId))
            return;
        if (terminalKind == CharacterAiActionTerminalKind.Cancelled
            && (environmentInterrupted || accidents.Occurred))
        {
            terminalKind = CharacterAiActionTerminalKind.Failed;
        }
        restockCargo.Release(ItemReservationReleaseReason.Cancelled);
        if (emergencySuspended)
        {
            WorkTypeId suspendedWorkTypeId = work.AssignedWorkTypeId;
            BuildableObject suspendedTarget = work.assignedShop;
            laborAccounting.Complete("suspended-for-emergency");
            proficiencyProgress.AwardApprovedWork(
                actor,
                ProficiencyWorkOutcome.PartialSuccess,
                "work-run:" + runId);
            PublishWorkCompleted(
                actor,
                suspendedWorkTypeId,
                "checkpoint:emergency-suspended",
                runId,
                WorkCompletionIdentityReferenceDefaults.CheckpointTerminalOwner,
                string.Empty,
                suspendedTarget != null
                    ? suspendedTarget.RequirePersistentInstanceId().Value
                    : string.Empty,
                proficiencyProgress.ApprovedWork);
            proficiencyProgress.RecordSpeciesCompletedWork(
                actor,
                suspendedWorkTypeId,
                proficiencyProgress.ApprovedWork,
                "work-run:" + runId);
            CharacterSkillRuntimeEffects.EndWork(actor);
            characterEnvironment.ClearWorkContext(
                new CharacterId(actor?.Identity?.PersistentId));
            ReturnEnvironmentalWorkwear(actor);
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
        laborAccounting.Complete(
            accidents.Occurred
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
        proficiencyProgress.AwardApprovedWork(
            actor,
            routineNeedInterrupted
                ? ProficiencyWorkOutcome.PartialSuccess
                : environmentInterrupted || accidents.Occurred
                ? ProficiencyWorkOutcome.AccidentOrForcedStop
                : ProficiencyWorkOutcome.SafeFailure,
            "work-run:" + runId);
        if (!routineNeedInterrupted)
        {
            PublishWorkCompleted(
                actor,
                interruptedWorkTypeId,
                accidents.Occurred
                    ? "outcome:accident"
                    : environmentInterrupted
                        ? "outcome:environment-interrupted"
                        : "outcome:failure",
                runId,
                WorkCompletionIdentityReferenceDefaults.ExecutorTerminalOwner,
                string.Empty,
                interruptedTarget != null
                    ? interruptedTarget.RequirePersistentInstanceId().Value
                    : string.Empty,
                proficiencyProgress.ApprovedWork);
        }
        else
        {
            PublishWorkCompleted(
                actor,
                interruptedWorkTypeId,
                "checkpoint:routine-need-suspended",
                runId,
                WorkCompletionIdentityReferenceDefaults.CheckpointTerminalOwner,
                string.Empty,
                interruptedTarget != null
                    ? interruptedTarget.RequirePersistentInstanceId().Value
                    : string.Empty,
                proficiencyProgress.ApprovedWork);
            proficiencyProgress.RecordSpeciesCompletedWork(
                actor,
                interruptedWorkTypeId,
                proficiencyProgress.ApprovedWork,
                "work-run:" + runId + ":routine-checkpoint");
            WorkExecutionChangedEvent.Publish(identityEvents, actor,
                WorkExecutionTransition.RoutineNeedSuspended,
                interruptedWorkTypeId, interruptedTarget);
        }
        CharacterSkillRuntimeEffects.EndWork(actor);
        characterEnvironment.ClearWorkContext(
            new CharacterId(actor?.Identity?.PersistentId));
        // Keep the exact temporary protection through an already-started
        // emergency evacuation. Ordinary completion/cancellation returns it;
        // removing it here would make the safety move itself unprotected.
        if (!environmentInterrupted)
        {
            ReturnEnvironmentalWorkwear(actor);
        }
        currentAction?.ReleaseReservation(actor);
        if (accidents.Occurred)
        {
            actor?.Brain?.ReportRuntimeActionFailure(
                AIActionFailure.Create(
                    AIActionFailureKind.CannotStart,
                    "work-accident",
                    interruptedTarget),
                DomainFailure.None,
                CharacterOperationBlockAxis.Risk,
                requestImmediateReplan: false);
        }
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

    internal void RecordApprovedWork(
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
            laborAccounting.RecordApproved(
                CharacterPersistentIdentity.TryGet(actor, out CharacterId approvedWorkerId)
                    ? approvedWorkerId : default,
                amount, remainingWork, workTypeId, currentRunId);
            proficiencyProgress.RecordApproved(amount);
            work.RecordApprovedWorkProgressForDiagnostics(amount);
            PublishWorkStarted(actor, workTypeId);
            if (allowAccident)
                accidents.TryTriggerWorkAccident(actor, amount, workTypeId, currentRunId);
        }
    }

    internal string CaptureTimedWorkGateDetail(
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
            + ",accident=" + accidents.Occurred
            + ",emergencyRequested=" + emergencySuspensionRequested
            + ",emergencyEpoch=" + requestedEmergencyEpochId
            + ",environmentInterrupted=" + environmentInterrupted;
    }

    public void CancelActiveRun(CharacterActor actor, string reason)
    {
        int cancelledRunId = currentRunId;
        ReleaseWorkerAllocation();
        if (currentRunId != cancelledRunId)
            return;
        restockCargo.Release(ItemReservationReleaseReason.Cancelled);
        ReleaseActiveExecutionCancellationResource();
        laborAccounting.Complete(
            string.IsNullOrWhiteSpace(reason) ? "cancelled" : reason.Trim());
        CharacterSkillRuntimeEffects.EndWork(actor);
        characterEnvironment.ClearWorkContext(
            new CharacterId(actor?.Identity?.PersistentId));
        ReturnEnvironmentalWorkwear(actor);
    }

    internal bool TryRegisterWorkerAllocation(int runId, IBuildingWorkerAllocation allocation)
    {
        if (allocation == null)
            throw new ArgumentNullException(nameof(allocation));
        if (currentRunId != runId || !work.IsActiveWorkRun(runId)
            || activeWorkerAllocation != null)
        {
            allocation.Dispose();
            return false;
        }
        activeWorkerAllocation = allocation;
        return true;
    }

    private void ReleaseWorkerAllocation()
    {
        IBuildingWorkerAllocation allocation = activeWorkerAllocation;
        activeWorkerAllocation = null;
        allocation?.Dispose();
    }

    internal void RegisterActiveExecutionCancellationResource(
        IDisposable resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));
        ReleaseActiveExecutionCancellationResource();
        activeExecutionCancellationResource = resource;
    }

    internal void ReleaseActiveExecutionCancellationResource()
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

    internal float TryConsumeResumedGenericProgress(
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

    internal bool TrySuspendAtSafeCheckpoint(
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId)
    {
        if (!emergencySuspensionRequested
            || requestedEmergencyEpochId <= 0L
            || !work.isWorking
            || accidents.Occurred
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
        WorkExecutionChangedEvent.Publish(identityEvents, actor,
            WorkExecutionTransition.EmergencySuspended,
            workTypeId, target);
        actor.Brain.SuspendCurrentActionAtSafeCheckpoint(
            "suspended-for-emergency");
        return true;
    }

    internal void PublishWorkStarted(CharacterActor actor, WorkTypeId workTypeId)
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

    internal void PublishWorkCompleted(
        CharacterActor actor,
        WorkTypeId workTypeId,
        string productId,
        int runId = -1,
        string terminalOwnerId = "",
        string childResultKey = "",
        string targetEntityId = "",
        float contributionWorkUnits = 0f)
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
            calendar?.Day ?? 0,
            runId >= 0
                ? $"work-run:{id.Value}:{runId}"
                : WorkCompletionIdentityReferenceDefaults.None,
            string.IsNullOrEmpty(terminalOwnerId)
                ? runId >= 0
                    ? WorkCompletionIdentityReferenceDefaults
                        .ExecutorTerminalOwner
                    : WorkCompletionIdentityReferenceDefaults
                        .LegacyTerminalOwner
                : terminalOwnerId,
            string.IsNullOrEmpty(childResultKey)
                ? WorkCompletionIdentityReferenceDefaults.None
                : childResultKey,
            string.IsNullOrEmpty(targetEntityId)
                ? WorkCompletionIdentityReferenceDefaults.None
                : targetEntityId,
            Mathf.Max(0f, contributionWorkUnits),
            CharacterSkillRuntimeEffects.CaptureWorkModifierProvenance(
                actor,
                workTypeId)));
    }

    internal CharacterCommandOrigin ResolveCommandOrigin() =>
        work.PriorityWorkTarget != null
        && work.assignedShop == work.PriorityWorkTarget
            ? CharacterCommandOrigin.DirectPlayerOrder
            : CharacterCommandOrigin.Autonomous;

    public void RestoreEmergencySuspensionReceipt(
        in EmergencyWorkSuspensionReceipt receipt)
    {
        if (!receipt.IsValid)
        {
            throw new ArgumentException(
                "A valid emergency suspension receipt is required.",
                nameof(receipt));
        }
        if (pendingSuspensionReceipt.IsValid)
        {
            throw new InvalidOperationException(
                "An emergency suspension receipt is already pending.");
        }
        if (!emergencySuspended || work.isWorking)
        {
            throw new InvalidOperationException(
                "Only an already-suspended work action can restore its receipt.");
        }

        pendingSuspensionReceipt = receipt;
        requestedEmergencyEpochId = receipt.AlertEpochId;
    }

    internal void ReturnEnvironmentalWorkwear(CharacterActor actor)
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
