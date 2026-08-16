using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class AbilityRescue : MonoBehaviour
{
    private sealed class PathResolution
    {
        public Queue<GridMoveStep> Path = new Queue<GridMoveStep>();
        public AIActionFailure Failure = AIActionFailure.None;
    }

    private CharacterActor actor;
    private AbilityMove move;
    private ICharacterMedicalQuery medicalQuery;
    private ICharacterMedicalCommand medicalCommands;
    private IGameClock gameClock;
    private IWorkAmountCalculator workAmount;
    private Coroutine rescueRoutine;
    private bool rescueExecutionActive;
    private CharacterMedicalOrder activeOrder;
    private string rescueStageForDiagnostics = "none";
    private string lastRescueTerminalForDiagnostics = "none";
    private int rescueExecutionRevisionForDiagnostics;
#if UNITY_EDITOR
    public System.Action<CharacterMedicalOrder> DebugBeforeRescueRoutineStart;
#endif

    public bool IsRescuing => rescueExecutionActive;
    public string RescueStageForDiagnostics => rescueStageForDiagnostics;
    public string LastRescueTerminalForDiagnostics =>
        lastRescueTerminalForDiagnostics;
    public int RescueExecutionRevisionForDiagnostics =>
        rescueExecutionRevisionForDiagnostics;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            StopRescue(
                CharacterMedicalStatusCode.RescueInterrupted,
                "ability-disabled");
        }
    }

    public static AbilityRescue Ensure(CharacterActor targetActor)
    {
        if (targetActor == null)
        {
            return null;
        }

        AbilityRescue ability = targetActor.GetComponent<AbilityRescue>();
        if (ability == null && Application.isPlaying)
        {
            ability = targetActor.gameObject.AddComponent<AbilityRescue>();
        }

        ability?.CacheReferences();
        if (ability != null
            && targetActor.MedicalQuery != null
            && targetActor.MedicalCommands != null
            && targetActor.GameClock != null)
        {
            ability.Configure(
                targetActor.MedicalQuery,
                targetActor.MedicalCommands,
                targetActor.GameClock,
                targetActor.WorkAmountCalculator);
        }

        return ability;
    }

    public void Configure(
        ICharacterMedicalQuery medicalQuery,
        ICharacterMedicalCommand medicalCommands,
        IGameClock gameClock,
        IWorkAmountCalculator workAmount)
    {
        this.medicalQuery = medicalQuery
            ?? throw new System.ArgumentNullException(nameof(medicalQuery));
        this.medicalCommands = medicalCommands
            ?? throw new System.ArgumentNullException(nameof(medicalCommands));
        this.gameClock = gameClock
            ?? throw new System.ArgumentNullException(nameof(gameClock));
        this.workAmount = workAmount
            ?? throw new System.ArgumentNullException(nameof(workAmount));
    }

    public bool CanStartRescue(out DomainFailure failure)
    {
        CacheReferences();
        failure = DomainFailure.None;
        if (actor == null
            || move == null
            || medicalQuery == null
            || medicalCommands == null
            || gameClock == null)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalRuntimeUnavailable);
            return false;
        }

        if (!medicalQuery.HasAvailableRescueOrder(actor))
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalPatientUnavailable);
            return false;
        }

        return true;
    }

    public void StartRescue()
    {
        CacheReferences();
        StopRescue(CharacterMedicalStatusCode.Restarted, "start-rescue-restart");
        DomainFailure failure = DomainFailure.None;
        CharacterMedicalOrder order = null;
        if (actor == null
            || move == null
            || medicalCommands == null
            || !medicalCommands.TryReserveBestOrder(
                actor,
                out order,
                out failure))
        {
            if (!failure.IsFailure)
            {
                failure = new DomainFailure(
                    FailureCode.CharacterMedicalRuntimeUnavailable);
            }
            actor?.Brain?.SetActionPhase(
                CharacterMedicalStatusCode.AwaitingRescue.ToString(),
                null,
                failure.Code.ToString());
            FailAiAction(
                ToAiFailureKind(failure),
                failure.Code.ToString());
            return;
        }

        activeOrder = order;
        rescueExecutionActive = true;
        BeginRescueDiagnostics(order, "ai-rescue");
#if UNITY_EDITOR
        DebugBeforeRescueRoutineStart?.Invoke(order);
#endif
        Coroutine started = StartCoroutine(RescueRoutine(order, enforceAiAction: true));
        rescueRoutine = rescueExecutionActive ? started : null;
    }

    public void StartRescue(CharacterActor patient)
    {
        CacheReferences();
        StopRescue(
            CharacterMedicalStatusCode.ManualRescueAssigned,
            "manual-rescue-restart");
        DomainFailure failure = DomainFailure.None;
        CharacterMedicalOrder order = null;
        if (actor == null
            || move == null
            || patient == null
            || medicalCommands == null
            || !medicalCommands.TryReserveOrderForPatient(
                actor,
                patient,
                out order,
                out failure))
        {
            if (!failure.IsFailure)
            {
                failure = new DomainFailure(
                    FailureCode.CharacterMedicalParticipantsInvalid);
            }
            actor?.Brain?.SetActionPhase(
                CharacterMedicalStatusCode.AwaitingRescue.ToString(),
                null,
                failure.Code.ToString());
            FailAiAction(
                ToAiFailureKind(failure),
                failure.Code.ToString());
            return;
        }

        activeOrder = order;
        rescueExecutionActive = true;
        BeginRescueDiagnostics(order, "manual-rescue");
#if UNITY_EDITOR
        DebugBeforeRescueRoutineStart?.Invoke(order);
#endif
        Coroutine started = StartCoroutine(RescueRoutine(order, enforceAiAction: false));
        rescueRoutine = rescueExecutionActive ? started : null;
    }

    public void StopRescue(
        CharacterMedicalStatusCode releaseStatus,
        string source = "unspecified")
    {
        if (rescueExecutionActive || rescueRoutine != null || activeOrder != null)
        {
            RecordRescueTerminal(
                "external-stop",
                releaseStatus,
                $"source={source}; {DescribeActionOwnership()}");
        }
        rescueExecutionActive = false;
        if (rescueRoutine != null)
        {
            StopCoroutine(rescueRoutine);
            rescueRoutine = null;
        }

        if (activeOrder != null && medicalCommands != null)
        {
            medicalCommands.TryReleaseReservation(
                activeOrder.orderId,
                actor,
                releaseStatus,
                out _);
        }

        activeOrder = null;
    }

    private IEnumerator RescueRoutine(
        CharacterMedicalOrder order,
        bool enforceAiAction)
    {
        rescueStageForDiagnostics = "routine-entry";
        if (!TryGetGrid(out Grid grid)
            || medicalQuery == null
            || medicalCommands == null
            || !medicalQuery.TryGetPatient(order, out CharacterActor patient))
        {
            // The order is already reserved before the coroutine starts. Route
            // every immediate failure through the reservation-aware terminal.
            Fail(
                order,
                CharacterMedicalStatusCode.RescueInterrupted,
                "runtime-or-patient-missing");
            yield break;
        }

        AIAction expectedAction = enforceAiAction ? actor.Brain?.bestAction : null;
        rescueStageForDiagnostics = "patient-path";
        PathResolution patientRoute = new PathResolution();
        yield return ResolvePathToCell(
            grid,
            patient.GetNowXY(),
            expectedAction,
            CharacterMedicalStatusCode.PreparingStabilization.ToString(),
            patientRoute);
        if (patientRoute.Failure.HasFailure)
        {
            Fail(
                order,
                patientRoute.Failure.Kind == AIActionFailureKind.CannotStart
                    ? CharacterMedicalStatusCode.RescueInterrupted
                    : CharacterMedicalStatusCode.PatientPathUnavailable,
                $"patient-path={patientRoute.Failure.Kind}; {DescribeActionOwnership()}");
            yield break;
        }

        if (patientRoute.Path.Count > 0)
        {
            // The medical order owns this locked action. AIBrain may replace the
            // equivalent AIAction instance while preserving the rescue intent.
            yield return move.MoveByPath(patientRoute.Path);
        }

        if (IsActionCancelled(expectedAction))
        {
            Fail(
                order,
                CharacterMedicalStatusCode.RescueInterrupted,
                $"after-patient-move; {DescribeActionOwnership()}");
            yield break;
        }

        while (!order.stabilized)
        {
            rescueStageForDiagnostics = "stabilization";
            if (!medicalQuery.TryGetPatient(order, out patient)
                || patient == null
                || patient.IsDead
                || patient.CurrentLifecycleState != CharacterLifecycleState.Downed)
            {
                Fail(
                    order,
                    CharacterMedicalStatusCode.RescueInterrupted,
                    $"patient-invalid-during-stabilization; {DescribeActionOwnership()}");
                yield break;
            }

            actor.Brain?.SetActionPhase(
                $"{CharacterMedicalStatusCode.Stabilizing}:{ProgressPercent(order.completedStabilizationWork, order.requiredStabilizationWork)}",
                null,
                patient.Identity?.DisplayName);
            float work = CalculateMedicalWorkPerSecond() * gameClock.DeltaTime;
            float beforeWork = order.completedStabilizationWork;
            medicalCommands.AdvanceStabilization(order.orderId, actor, work);
            actor.Brain?.NotifyGameplayWorkProgress(
                Mathf.Max(0f, order.completedStabilizationWork - beforeWork));
            if (IsActionCancelled(expectedAction))
            {
                Fail(
                    order,
                    CharacterMedicalStatusCode.StabilizationInterrupted,
                    $"action-changed-during-stabilization; {DescribeActionOwnership()}");
                yield break;
            }

            yield return null;
        }

        rescueStageForDiagnostics = "begin-carry";
        if (!medicalCommands.TryBeginCarrying(
                order.orderId,
                actor,
                out DomainFailure carryFailure))
        {
            actor.Brain?.SetActionPhase(
                CharacterMedicalStatusCode.AwaitingBed.ToString(),
                null,
                carryFailure.Code.ToString());
            Fail(
                order,
                carryFailure.Code == FailureCode.CharacterMedicalBedUnavailable
                    ? CharacterMedicalStatusCode.AwaitingBed
                    : CharacterMedicalStatusCode.RescueInterrupted,
                $"begin-carry={carryFailure.Code}; {DescribeActionOwnership()}");
            yield break;
        }

        if (!medicalQuery.TryGetTreatmentFacility(
                order,
                out BuildableObject facility))
        {
            Fail(
                order,
                CharacterMedicalStatusCode.TreatmentPathUnavailable,
                "treatment-facility-missing");
            yield break;
        }

        PathResolution bedRoute = new PathResolution();
        rescueStageForDiagnostics = "bed-path";
        yield return ResolvePathToCell(
            grid,
            facility.centerPos,
            expectedAction,
            CharacterMedicalStatusCode.Carrying.ToString(),
            bedRoute);
        if (bedRoute.Failure.HasFailure)
        {
            Fail(
                order,
                bedRoute.Failure.Kind == AIActionFailureKind.CannotStart
                    ? CharacterMedicalStatusCode.RescueInterrupted
                    : CharacterMedicalStatusCode.TreatmentPathUnavailable,
                $"bed-path={bedRoute.Failure.Kind}; {DescribeActionOwnership()}");
            yield break;
        }

        actor.Brain?.SetActionPhase(
            CharacterMedicalStatusCode.Carrying.ToString(),
            null,
            patient.Identity?.DisplayName);
        if (bedRoute.Path.Count > 0)
        {
            yield return move.MoveByPath(bedRoute.Path);
        }

        DomainFailure placementFailure = DomainFailure.None;
        rescueStageForDiagnostics = "place-at-treatment";
        if (IsActionCancelled(expectedAction)
            || !medicalCommands.TryPlaceAtTreatmentDestination(
                order.orderId,
                actor,
                out placementFailure))
        {
            Fail(
                order,
                placementFailure.Code == FailureCode.CharacterMedicalDestinationUnavailable
                    ? CharacterMedicalStatusCode.AwaitingBed
                    : CharacterMedicalStatusCode.RescueInterrupted,
                $"placement={placementFailure.Code}; {DescribeActionOwnership()}");
            yield break;
        }

        while (medicalQuery.TryGetOrder(order.orderId, out order)
            && order.IsActive
            && medicalQuery.TryGetPatient(order, out patient)
            && patient.CurrentLifecycleState == CharacterLifecycleState.Downed)
        {
            rescueStageForDiagnostics = "treatment";
            actor.Brain?.SetActionPhase(
                $"{CharacterMedicalStatusCode.Treating}:{ProgressPercent(order.completedTreatmentWork, order.requiredTreatmentWork)}",
                null,
                patient.Identity?.DisplayName);
            float work = CalculateMedicalWorkPerSecond() * gameClock.DeltaTime;
            float beforeWork = order.completedTreatmentWork;
            medicalCommands.AdvanceTreatment(order.orderId, actor, work);
            actor.Brain?.NotifyGameplayWorkProgress(
                Mathf.Max(0f, order.completedTreatmentWork - beforeWork));
            if (IsActionCancelled(expectedAction))
            {
                Fail(
                    order,
                    CharacterMedicalStatusCode.TreatmentInterrupted,
                    $"action-changed-during-treatment; {DescribeActionOwnership()}");
                yield break;
            }

            yield return null;
        }

        if (order != null
            && order.IsActive
            && (!medicalQuery.TryGetPatient(order, out patient)
                || patient == null
                || patient.IsDead
                || patient.CurrentLifecycleState != CharacterLifecycleState.Active))
        {
            Fail(
                order,
                CharacterMedicalStatusCode.TreatmentInterrupted,
                $"patient-not-active-after-treatment; {DescribeActionOwnership()}");
            yield break;
        }

        activeOrder = null;
        rescueExecutionActive = false;
        rescueRoutine = null;
        RecordRescueTerminal(
            "completed",
            CharacterMedicalStatusCode.TreatmentCompleted,
            DescribeActionOwnership());
        EndAiAction(CharacterAiActionTerminalKind.Completed, clearFailures: true);
    }

    private IEnumerator ResolvePathToCell(
        Grid grid,
        Vector2Int destination,
        AIAction expectedAction,
        string phase,
        PathResolution resolution)
    {
        resolution.Path.Clear();
        resolution.Failure = AIActionFailure.None;
        if (actor.GetNowXY() == destination)
        {
            yield break;
        }

        IGridPathSearchBroker broker = actor.PathSearchBroker;
        if (broker == null)
        {
            resolution.Failure = AIActionFailure.Create(
                AIActionFailureKind.NoGrid,
                "Path search broker is unavailable.");
            yield break;
        }

        AIBrain brain = actor.Brain;
        int requestId = brain != null
            ? brain.NotifyPathRequested(repath: false)
            : 0;
        int pendingFrames = 0;
        while (true)
        {
            if (IsActionCancelled(expectedAction))
            {
                if (requestId != 0)
                {
                    brain.NotifyPathResult(
                        requestId,
                        CharacterAiPathTraceState.Cancelled,
                        0);
                }
                resolution.Failure = AIActionFailure.Create(
                    AIActionFailureKind.CannotStart,
                    "Rescue path request was cancelled.");
                yield break;
            }

            GridPathRequestStatus status = broker.RequestMovePathTo(
                grid,
                actor.GetNowXY(),
                destination,
                out Queue<GridMoveStep> path,
                GridPathSearchPriority.Urgent,
                GridTraversalContext.ForCharacter(
                    CharacterPersistentIdentity.Require(actor)));
            if (status == GridPathRequestStatus.Pending)
            {
                pendingFrames++;
                brain?.SetActionPhase(
                    phase,
                    null,
                    $"path pending ({pendingFrames}) to {destination}");
                // The exact search is incremental. Pending means that its time
                // slice or shared budget ended, not that the target is
                // unreachable. Keep the committed medical action and resume on
                // the next frame instead of emitting a false NoPath terminal.
                if (pendingFrames == 1
                    || (pendingFrames & (pendingFrames - 1)) == 0)
                {
                    brain?.NotifyRetryScheduled(0f);
                }
                yield return null;
                continue;
            }

            bool reachable = status == GridPathRequestStatus.Reachable
                && path != null
                && (path.Count > 0 || actor.GetNowXY() == destination);
            if (requestId != 0)
            {
                brain.NotifyPathResult(
                    requestId,
                    reachable
                        ? CharacterAiPathTraceState.Found
                        : CharacterAiPathTraceState.NoPath,
                    path?.Count ?? 0);
            }
            brain?.SetActionPhase(
                phase,
                null,
                reachable
                    ? destination.ToString()
                    : $"unreachable: {destination}");
            if (!reachable)
            {
                resolution.Failure = AIActionFailure.Create(
                    AIActionFailureKind.NoPath,
                    $"No rescue path to {destination}.");
                yield break;
            }

            resolution.Path = path;
            yield break;
        }
    }

    private void Fail(
        CharacterMedicalOrder order,
        CharacterMedicalStatusCode statusCode,
        string detail = "")
    {
        RecordRescueTerminal(rescueStageForDiagnostics, statusCode, detail);
        if (medicalCommands != null && order != null)
        {
            medicalCommands.TryReleaseReservation(
                order.orderId,
                actor,
                statusCode,
                out _);
        }
        activeOrder = null;
        rescueExecutionActive = false;
        rescueRoutine = null;
        FailAiAction(ToAiFailureKind(statusCode), statusCode.ToString());
    }

    private void BeginRescueDiagnostics(CharacterMedicalOrder order, string source)
    {
        rescueExecutionRevisionForDiagnostics = checked(
            rescueExecutionRevisionForDiagnostics + 1);
        rescueStageForDiagnostics = "starting";
        lastRescueTerminalForDiagnostics =
            $"revision={rescueExecutionRevisionForDiagnostics}; source={source}; "
            + $"order={order?.orderId ?? "<none>"}; terminal=pending";
    }

    private void RecordRescueTerminal(
        string stage,
        CharacterMedicalStatusCode statusCode,
        string detail)
    {
        rescueStageForDiagnostics = stage ?? "unknown";
        lastRescueTerminalForDiagnostics =
            $"revision={rescueExecutionRevisionForDiagnostics}; "
            + $"order={activeOrder?.orderId ?? "<none>"}; "
            + $"stage={rescueStageForDiagnostics}; status={statusCode}; "
            + (detail ?? string.Empty);
    }

    private string DescribeActionOwnership()
    {
        return $"lifecycle={actor?.CurrentLifecycleState}; "
            + $"action={actor?.Brain?.CurrentActionDebugLabel ?? "<none>"}; "
            + $"phase={actor?.Brain?.CurrentActionPhase ?? "<none>"}; "
            + $"executed={actor?.Brain?.isExecuted}; "
            + $"actionEnd={actor?.Brain?.isBestActionEnd}";
    }

    private void FailAiAction(AIActionFailureKind kind, string reason)
    {
        if (actor?.Brain != null)
        {
            actor.Brain.ReportRuntimeActionFailure(
                AIActionFailure.Create(kind, reason),
                requestImmediateReplan: false);
        }

        EndAiAction(CharacterAiActionTerminalKind.Failed, clearFailures: false);
    }

    private void EndAiAction(
        CharacterAiActionTerminalKind terminalKind,
        bool clearFailures)
    {
        if (actor?.Brain != null)
        {
            actor.Brain.EndExpectedAction(
                actor.Brain.bestAction,
                terminalKind,
                clearFailures);
        }

        rescueExecutionActive = false;
        rescueRoutine = null;
    }

    private static AIActionFailureKind ToAiFailureKind(DomainFailure failure)
    {
        return failure.Code switch
        {
            FailureCode.CharacterMedicalRuntimeUnavailable =>
                AIActionFailureKind.Unsupported,
            FailureCode.CharacterMedicalPatientUnavailable =>
                AIActionFailureKind.NoWork,
            FailureCode.CharacterMedicalBedUnavailable =>
                AIActionFailureKind.ResourceUnavailable,
            FailureCode.CharacterMedicalDestinationUnavailable =>
                AIActionFailureKind.Destroyed,
            _ => AIActionFailureKind.CannotStart
        };
    }

    private static AIActionFailureKind ToAiFailureKind(
        CharacterMedicalStatusCode statusCode)
    {
        return statusCode switch
        {
            CharacterMedicalStatusCode.PatientPathUnavailable or
            CharacterMedicalStatusCode.TreatmentPathUnavailable =>
                AIActionFailureKind.NoPath,
            CharacterMedicalStatusCode.AwaitingBed =>
                AIActionFailureKind.ResourceUnavailable,
            CharacterMedicalStatusCode.RescueInterrupted or
            CharacterMedicalStatusCode.StabilizationInterrupted or
            CharacterMedicalStatusCode.TreatmentInterrupted =>
                AIActionFailureKind.CannotStart,
            _ => AIActionFailureKind.Unknown
        };
    }

    private bool IsActionCancelled(AIAction expectedAction)
    {
        if (expectedAction == null)
        {
            return actor == null
                || actor.CurrentLifecycleState != CharacterLifecycleState.Active;
        }

        if (actor == null
            || actor.Brain == null
            || actor.CurrentLifecycleState != CharacterLifecycleState.Active)
        {
            return true;
        }

        AIAction currentAction = actor.Brain.bestAction;
        return currentAction != null
            && currentAction.actionset is not AIRescue;
    }

    private bool TryGetGrid(out Grid grid)
    {
        grid = null;
        return actor?.WorldRegistry != null
            && actor.WorldRegistry.TryGetGrid(out grid);
    }

    private void CacheReferences()
    {
        actor = actor != null ? actor : GetComponent<CharacterActor>();
        move = move != null ? move : GetComponent<AbilityMove>();
    }

    private static int ProgressPercent(float completed, float required)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(completed / Mathf.Max(0.01f, required)) * 100f);
    }

    private float CalculateMedicalWorkPerSecond() =>
        workAmount.CalculateWorkPerSecond(
            actor,
            null,
            BuiltInWorkTypeIds.Treat,
            1f);
}
