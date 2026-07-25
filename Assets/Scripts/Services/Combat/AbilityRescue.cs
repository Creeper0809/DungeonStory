using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class AbilityRescue : MonoBehaviour
{
    private CharacterActor actor;
    private AbilityMove move;
    private ICharacterMedicalRuntime medicalRuntime;
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
            && targetActor.MedicalRuntime != null
            && targetActor.GameClock != null)
        {
            ability.Configure(targetActor.MedicalRuntime, targetActor.GameClock);
        }

        return ability;
    }

    public void Configure(
        ICharacterMedicalRuntime medicalRuntime,
        IGameClock gameClock)
    {
        this.medicalRuntime = medicalRuntime
            ?? throw new System.ArgumentNullException(nameof(medicalRuntime));
        this.gameClock = gameClock
            ?? throw new System.ArgumentNullException(nameof(gameClock));
    }

    public bool CanStartRescue(out string failureReason)
    {
        CacheReferences();
        failureReason = string.Empty;
        if (actor == null || move == null || medicalRuntime == null || gameClock == null)
        {
            failureReason = "의료 시스템이 준비되지 않았습니다.";
            return false;
        }

        if (!medicalRuntime.HasAvailableRescueOrder(actor))
        {
            failureReason = "구조할 환자가 없습니다.";
            return false;
        }

        return true;
    }

    public void StartRescue()
    {
        CacheReferences();
        StopRescue("재시작");
        string failureReason = string.Empty;
        if (actor == null
            || move == null
            || medicalRuntime == null
            || !medicalRuntime.TryReserveBestOrder(
                actor,
                out CharacterMedicalOrder order,
                out failureReason))
        {
            actor?.Brain?.SetActionPhase("구조 대기", null, failureReason);
            EndAiAction();
            return;
        }

        activeOrder = order;
        rescueRoutine = StartCoroutine(RescueRoutine(order, enforceAiAction: true));
    }

    public void StartRescue(CharacterActor patient)
    {
        CacheReferences();
        StopRescue("직접 구조 명령");
        string failureReason = string.Empty;
        if (actor == null
            || move == null
            || patient == null
            || medicalRuntime == null
            || !medicalRuntime.TryReserveOrderForPatient(
                actor,
                patient,
                out CharacterMedicalOrder order,
                out failureReason))
        {
            actor?.Brain?.SetActionPhase("구조 대기", null, failureReason);
            EndAiAction();
            return;
        }

        activeOrder = order;
        rescueRoutine = StartCoroutine(RescueRoutine(order, enforceAiAction: false));
    }

    public void StopRescue(string reason)
    {
        if (rescueRoutine != null)
        {
            StopCoroutine(rescueRoutine);
            rescueRoutine = null;
        }

        if (activeOrder != null && medicalRuntime != null)
        {
            medicalRuntime.ReleaseReservation(
                activeOrder.orderId,
                actor,
                reason);
        }

        activeOrder = null;
    }

    private IEnumerator RescueRoutine(
        CharacterMedicalOrder order,
        bool enforceAiAction)
    {
        if (!TryGetGrid(out Grid grid)
            || medicalRuntime == null
            || !medicalRuntime.TryGetPatient(order, out CharacterActor patient))
        {
            EndAiAction();
            yield break;
        }

        AIAction expectedAction = enforceAiAction ? actor.Brain?.bestAction : null;
        if (!MoveToCell(grid, patient.GetNowXY(), expectedAction, "환자에게 이동", out Queue<GridMoveStep> patientPath))
        {
            Fail(order, "환자에게 갈 수 없습니다.");
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
            Fail(order, "구조 중단");
            yield break;
        }

        while (!order.stabilized)
        {
            actor.Brain?.SetActionPhase(
                $"현장 안정화 {ProgressPercent(order.completedStabilizationWork, order.requiredStabilizationWork)}%",
                null,
                patient.Identity?.DisplayName);
            float work = actor.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Treat) * gameClock.DeltaTime;
            medicalRuntime.AdvanceStabilization(order.orderId, actor, work);
            if (IsActionCancelled(expectedAction))
            {
                Fail(order, "안정화 중단");
                yield break;
            }

            yield return null;
        }

        if (!medicalRuntime.TryBeginCarrying(
                order.orderId,
                actor,
                out string carryFailure))
        {
            actor.Brain?.SetActionPhase("치료 침상 대기", null, carryFailure);
            Fail(order, carryFailure);
            yield break;
        }

        if (!medicalRuntime.TryGetTreatmentFacility(
                order,
                out BuildableObject facility)
            || !MoveToCell(
                grid,
                facility.centerPos,
                expectedAction,
                "환자 이송",
                out Queue<GridMoveStep> bedPath))
        {
            Fail(order, "치료 침상으로 갈 수 없습니다.");
            yield break;
        }

        actor.Brain?.SetActionPhase("환자 이송", null, patient.Identity?.DisplayName);
        if (bedPath.Count > 0)
        {
            yield return move.MoveByPath(bedPath);
        }

        string placementFailure = string.Empty;
        if (IsActionCancelled(expectedAction)
            || !medicalRuntime.TryPlaceAtTreatmentDestination(
                order.orderId,
                actor,
                out placementFailure))
        {
            Fail(order, placementFailure);
            yield break;
        }

        while (medicalRuntime.TryGetOrder(order.orderId, out order)
            && order.IsActive
            && medicalRuntime.TryGetPatient(order, out patient)
            && patient.CurrentLifecycleState == CharacterLifecycleState.Downed)
        {
            actor.Brain?.SetActionPhase(
                $"병상 치료 {ProgressPercent(order.completedTreatmentWork, order.requiredTreatmentWork)}%",
                null,
                patient.Identity?.DisplayName);
            float work = actor.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Treat) * gameClock.DeltaTime;
            medicalRuntime.AdvanceTreatment(order.orderId, actor, work);
            if (IsActionCancelled(expectedAction))
            {
                Fail(order, "치료 중단");
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

        path = actor.PathSearchBroker?.GetMovePath(
            grid,
            actor.GetNowXY(),
            cell => cell == destination);
        actor.Brain?.SetActionPhase(phase, null, destination.ToString());
        return path != null && path.Count > 0 && !IsActionCancelled(expectedAction);
    }

    private void Fail(CharacterMedicalOrder order, string reason)
    {
        medicalRuntime?.ReleaseReservation(order?.orderId, actor, reason);
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
