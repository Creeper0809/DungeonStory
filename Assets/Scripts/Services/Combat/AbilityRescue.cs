using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class AbilityRescue : MonoBehaviour
{
    private CharacterActor actor;
    private AbilityMove move;
    private ICharacterMedicalQuery medicalQuery;
    private ICharacterMedicalCommand medicalCommands;
    private IGameClock gameClock;
    private Coroutine rescueRoutine;
    private CharacterMedicalOrder activeOrder;

    public bool IsRescuing => rescueRoutine != null;

    private void Awake()
    {
        CacheReferences();
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
                targetActor.GameClock);
        }

        return ability;
    }

    public void Configure(
        ICharacterMedicalQuery medicalQuery,
        ICharacterMedicalCommand medicalCommands,
        IGameClock gameClock)
    {
        this.medicalQuery = medicalQuery
            ?? throw new System.ArgumentNullException(nameof(medicalQuery));
        this.medicalCommands = medicalCommands
            ?? throw new System.ArgumentNullException(nameof(medicalCommands));
        this.gameClock = gameClock
            ?? throw new System.ArgumentNullException(nameof(gameClock));
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
        StopRescue(CharacterMedicalStatusCode.Restarted);
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
            EndAiAction();
            return;
        }

        activeOrder = order;
        rescueRoutine = StartCoroutine(RescueRoutine(order, enforceAiAction: true));
    }

    public void StartRescue(CharacterActor patient)
    {
        CacheReferences();
        StopRescue(CharacterMedicalStatusCode.ManualRescueAssigned);
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
            EndAiAction();
            return;
        }

        activeOrder = order;
        rescueRoutine = StartCoroutine(RescueRoutine(order, enforceAiAction: false));
    }

    public void StopRescue(CharacterMedicalStatusCode releaseStatus)
    {
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
        if (!TryGetGrid(out Grid grid)
            || medicalQuery == null
            || medicalCommands == null
            || !medicalQuery.TryGetPatient(order, out CharacterActor patient))
        {
            EndAiAction();
            yield break;
        }

        AIAction expectedAction = enforceAiAction ? actor.Brain?.bestAction : null;
        if (!MoveToCell(
                grid,
                patient.GetNowXY(),
                expectedAction,
                CharacterMedicalStatusCode.PreparingStabilization.ToString(),
                out Queue<GridMoveStep> patientPath))
        {
            Fail(order, CharacterMedicalStatusCode.PatientPathUnavailable);
            yield break;
        }

        if (patientPath.Count > 0)
        {
            // The medical order owns this locked action. AIBrain may replace the
            // equivalent AIAction instance while preserving the rescue intent.
            yield return move.MoveByPath(patientPath);
        }

        if (IsActionCancelled(expectedAction))
        {
            Fail(order, CharacterMedicalStatusCode.RescueInterrupted);
            yield break;
        }

        while (!order.stabilized)
        {
            actor.Brain?.SetActionPhase(
                $"{CharacterMedicalStatusCode.Stabilizing}:{ProgressPercent(order.completedStabilizationWork, order.requiredStabilizationWork)}",
                null,
                patient.Identity?.DisplayName);
            float work = actor.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Treat) * gameClock.DeltaTime;
            medicalCommands.AdvanceStabilization(order.orderId, actor, work);
            if (IsActionCancelled(expectedAction))
            {
                Fail(order, CharacterMedicalStatusCode.StabilizationInterrupted);
                yield break;
            }

            yield return null;
        }

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
                    : CharacterMedicalStatusCode.RescueInterrupted);
            yield break;
        }

        if (!medicalQuery.TryGetTreatmentFacility(
                order,
                out BuildableObject facility)
            || !MoveToCell(
                grid,
                facility.centerPos,
                expectedAction,
                CharacterMedicalStatusCode.Carrying.ToString(),
                out Queue<GridMoveStep> bedPath))
        {
            Fail(order, CharacterMedicalStatusCode.TreatmentPathUnavailable);
            yield break;
        }

        actor.Brain?.SetActionPhase(
            CharacterMedicalStatusCode.Carrying.ToString(),
            null,
            patient.Identity?.DisplayName);
        if (bedPath.Count > 0)
        {
            yield return move.MoveByPath(bedPath);
        }

        DomainFailure placementFailure = DomainFailure.None;
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
                    : CharacterMedicalStatusCode.RescueInterrupted);
            yield break;
        }

        while (medicalQuery.TryGetOrder(order.orderId, out order)
            && order.IsActive
            && medicalQuery.TryGetPatient(order, out patient)
            && patient.CurrentLifecycleState == CharacterLifecycleState.Downed)
        {
            actor.Brain?.SetActionPhase(
                $"{CharacterMedicalStatusCode.Treating}:{ProgressPercent(order.completedTreatmentWork, order.requiredTreatmentWork)}",
                null,
                patient.Identity?.DisplayName);
            float work = actor.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Treat) * gameClock.DeltaTime;
            medicalCommands.AdvanceTreatment(order.orderId, actor, work);
            if (IsActionCancelled(expectedAction))
            {
                Fail(order, CharacterMedicalStatusCode.TreatmentInterrupted);
                yield break;
            }

            yield return null;
        }

        activeOrder = null;
        rescueRoutine = null;
        EndAiAction();
    }

    private bool MoveToCell(
        Grid grid,
        Vector2Int destination,
        AIAction expectedAction,
        string phase,
        out Queue<GridMoveStep> path)
    {
        path = new Queue<GridMoveStep>();
        if (actor.GetNowXY() == destination)
        {
            return true;
        }

        path = actor.PathSearchBroker?.GetMovePathTo(
            grid,
            actor.GetNowXY(),
            destination);
        actor.Brain?.SetActionPhase(phase, null, destination.ToString());
        return path != null && path.Count > 0 && !IsActionCancelled(expectedAction);
    }

    private void Fail(
        CharacterMedicalOrder order,
        CharacterMedicalStatusCode statusCode)
    {
        if (medicalCommands != null && order != null)
        {
            medicalCommands.TryReleaseReservation(
                order.orderId,
                actor,
                statusCode,
                out _);
        }
        activeOrder = null;
        rescueRoutine = null;
        EndAiAction();
    }

    private void EndAiAction()
    {
        if (actor?.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
            actor.Brain.RequestImmediateReplan(clearFailures: true);
        }

        rescueRoutine = null;
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
}
