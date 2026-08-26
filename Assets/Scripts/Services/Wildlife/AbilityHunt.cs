using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AbilityHunt : MonoBehaviour
{
    private CharacterActor actor;
    private AbilityMove move;
    private IWildlifeRuntime wildlifeRuntime;
    private Coroutine huntingRoutine;
    private WildlifeHuntJob activeJob;
    private bool huntExecutionActive;
#if UNITY_EDITOR
    private IGridPathSearchBroker pathSearchOverrideForDiagnostics;
#endif

    public bool IsHunting => huntExecutionActive;

#if UNITY_EDITOR
    public IGridPathSearchBroker DebugReplacePathSearchBroker(
        IGridPathSearchBroker replacement)
    {
        IGridPathSearchBroker previous = pathSearchOverrideForDiagnostics;
        pathSearchOverrideForDiagnostics = replacement;
        return previous;
    }
#endif

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            StopHunting("hunter-disabled");
        }
    }

    public static AbilityHunt Ensure(
        CharacterActor targetActor,
        IWildlifeRuntime wildlifeRuntime)
    {
        if (targetActor == null)
        {
            return null;
        }

        AbilityHunt ability = targetActor.GetComponent<AbilityHunt>();
        if (ability == null && Application.isPlaying)
        {
            ability = targetActor.gameObject.AddComponent<AbilityHunt>();
        }

        ability?.CacheReferences();
        if (wildlifeRuntime != null)
        {
            ability?.Configure(wildlifeRuntime);
        }

        return ability;
    }

    public static AbilityHunt Ensure(CharacterActor targetActor)
    {
        if (targetActor == null)
        {
            return null;
        }

        AbilityHunt ability = targetActor.GetComponent<AbilityHunt>();
        if (ability == null && Application.isPlaying)
        {
            ability = targetActor.gameObject.AddComponent<AbilityHunt>();
        }

        ability?.CacheReferences();
        return ability;
    }

    public void Configure(IWildlifeRuntime runtime)
    {
        wildlifeRuntime = runtime
            ?? throw new System.ArgumentNullException(nameof(runtime));
    }

    public bool CanStartHunting(out string failureReason)
    {
        failureReason = string.Empty;
        CacheReferences();
        return actor != null
            && move != null
            && wildlifeRuntime != null
            && wildlifeRuntime.HasAvailableHuntJob(actor);
    }

    public void StartHunting()
    {
        CacheReferences();
        if (actor == null || move == null || wildlifeRuntime == null)
        {
            FailAiAction(
                AIActionFailureKind.Unsupported,
                "Hunt runtime collaborators are unavailable.");
            return;
        }

        StopHunting("재시작");
        if (!wildlifeRuntime.TryReserveBestHuntJob(
                actor,
                out WildlifeHuntJob job,
                out string reason))
        {
            actor.Brain?.SetActionPhase("사냥 대기", null, reason);
            FailAiAction(AIActionFailureKind.NoWork, reason);
            return;
        }

        activeJob = job;
        huntExecutionActive = true;
        Coroutine started = StartCoroutine(HuntRoutine(job));
        // A coroutine can terminate before its first yield.  In that case the
        // terminal path has already cleared huntExecutionActive and the
        // returned completed handle must not resurrect IsHunting.
        huntingRoutine = huntExecutionActive ? started : null;
    }

    public void StopHunting(string reason)
    {
        if (huntingRoutine != null)
        {
            StopCoroutine(huntingRoutine);
            huntingRoutine = null;
        }

        huntExecutionActive = false;
        ReleaseReservation(activeJob);
        activeJob = default;
    }

    private IEnumerator HuntRoutine(WildlifeHuntJob job)
    {
        if (job.Target == null || wildlifeRuntime == null || !TryGetGrid(out Grid grid))
        {
            FailAiAction(
                job.Target == null
                    ? AIActionFailureKind.Destroyed
                    : AIActionFailureKind.NoGrid,
                job.Target == null
                    ? "Hunt target is unavailable."
                    : "Hunt grid or runtime is unavailable.");
            yield break;
        }

        AIAction expectedAction = actor.Brain != null ? actor.Brain.bestAction : null;
        move.CancelActiveMovement();
        actor.Brain?.SetActionPhase("사냥감 추적", null, job.Target.DisplayName);
        int safety = 0;
        while (job.Target != null && job.Target.IsAlive && safety++ < 96)
        {
            if (IsActionCancelled(expectedAction))
            {
                ReleaseReservation(job);
                EndAiAction(
                    CharacterAiActionTerminalKind.Cancelled,
                    clearFailures: false);
                yield break;
            }

            if (wildlifeRuntime.NeedsHuntReload(actor))
            {
                float reloadDuration = wildlifeRuntime.GetHuntReloadDuration(actor);
                actor.Brain?.SetActionPhase("무기 재장전", null, job.Target.DisplayName);
                if (reloadDuration > 0f)
                {
                    yield return new WaitForSeconds(reloadDuration);
                }

                string reloadMessage = string.Empty;
                if (IsActionCancelled(expectedAction))
                {
                    ReleaseReservation(job);
                    EndAiAction(
                        CharacterAiActionTerminalKind.Cancelled,
                        clearFailures: false);
                    yield break;
                }

                if (!wildlifeRuntime.TryReloadHuntWeapon(actor, out reloadMessage))
                {
                    actor.Brain?.SetActionPhase(
                        "사냥 중단",
                        null,
                        string.IsNullOrWhiteSpace(reloadMessage) ? "재장전 실패" : reloadMessage);
                    ReleaseReservation(job);
                    FailAiAction(
                        AIActionFailureKind.ResourceUnavailable,
                        string.IsNullOrWhiteSpace(reloadMessage)
                            ? "Hunt weapon reload failed."
                            : reloadMessage);
                    yield break;
                }
            }

            if (!wildlifeRuntime.CanAttackHuntTargetFrom(
                    actor,
                    job.Target,
                    grid,
                    actor.GetNowXY()))
            {
                Queue<GridMoveStep> path = GetPathSearchBroker()?.GetMovePath(
                    grid,
                    actor.GetNowXY(),
                    position => wildlifeRuntime != null
                        && wildlifeRuntime.CanAttackHuntTargetFrom(
                            actor,
                            job.Target,
                            grid,
                            position));
                if (path == null || path.Count == 0)
                {
                    actor.Brain?.SetActionPhase(
                        "사냥 실패",
                        null,
                        "공격 가능한 위치가 없습니다.");
                    ReleaseReservation(job);
                    FailAiAction(
                        AIActionFailureKind.NoPath,
                        "No attack position is reachable for the hunt target.");
                    yield break;
                }

                actor.Brain?.SetActionPhase("사냥 위치로 이동", null, job.Target.DisplayName);
                yield return MoveAlongHuntPath(
                    path,
                    job,
                    expectedAction);
                if (IsActionCancelled(expectedAction))
                {
                    ReleaseReservation(job);
                    EndAiAction(
                        CharacterAiActionTerminalKind.Cancelled,
                        clearFailures: false);
                    yield break;
                }

                if (job.Target == null || !job.Target.IsAlive)
                {
                    break;
                }

                bool canAttackFromCurrentCell =
                    wildlifeRuntime.CanAttackHuntTargetFrom(
                        actor,
                        job.Target,
                        grid,
                        actor.GetNowXY());
                if (move.LastGridMoveWasBlocked && !canAttackFromCurrentCell)
                {
                    ReleaseReservation(job);
                    FailAiAction(
                        AIActionFailureKind.NoPath,
                        "The hunt path became blocked.");
                    yield break;
                }

                // Wildlife can move while the hunter follows the resolved path.
                // Re-enter pursuit if the target left the approved attack cell.
                if (!canAttackFromCurrentCell)
                {
                    continue;
                }
            }

            actor.Brain?.SetActionPhase("사냥 공격", null, job.Target.DisplayName);
            if (!wildlifeRuntime.ApplyHuntHit(
                    actor,
                    job.WildlifeId,
                    out string attackMessage))
            {
                actor.Brain?.SetActionPhase("사냥 중단", null, attackMessage);
                ReleaseReservation(job);
                FailAiAction(AIActionFailureKind.CannotStart, attackMessage);
                yield break;
            }

            if (job.Target == null || !job.Target.IsAlive)
            {
                break;
            }

            yield return new WaitForSeconds(
                Mathf.Max(0.15f, wildlifeRuntime.GetHuntAttackInterval(actor)));
        }

        if (job.Target != null && job.Target.IsAlive)
        {
            ReleaseReservation(job);
            activeJob = default;
            FailAiAction(
                AIActionFailureKind.CannotStart,
                "Hunt attack safety limit was exhausted before the target was defeated.");
            yield break;
        }

        if (job.Target == null)
        {
            ReleaseReservation(job);
            activeJob = default;
            FailAiAction(
                AIActionFailureKind.Destroyed,
                "The reserved hunt target despawned before hunt completion.");
            yield break;
        }

        ReleaseReservation(job);
        huntingRoutine = null;
        activeJob = default;
        EndAiAction(CharacterAiActionTerminalKind.Completed, clearFailures: true);
    }

    private IEnumerator MoveAlongHuntPath(
        Queue<GridMoveStep> path,
        WildlifeHuntJob job,
        AIAction expectedAction)
    {
        while (path != null
            && path.Count > 0
            && job.Target != null
            && job.Target.IsAlive)
        {
            Queue<GridMoveStep> oneStep = new();
            oneStep.Enqueue(path.Dequeue());
            yield return move.MoveByPath(oneStep, expectedAction);
            if (move.LastGridMoveWasBlocked
                || IsActionCancelled(expectedAction))
            {
                yield break;
            }
        }
    }

    private bool TryGetGrid(out Grid grid)
    {
        if (actor?.WorldRegistry != null && actor.WorldRegistry.TryGetGrid(out grid))
        {
            return true;
        }

        grid = null;
        return false;
    }

    private IGridPathSearchBroker GetPathSearchBroker()
    {
#if UNITY_EDITOR
        if (pathSearchOverrideForDiagnostics != null)
        {
            return pathSearchOverrideForDiagnostics;
        }
#endif
        return actor?.PathSearchBroker;
    }

    private bool IsActionCancelled(AIAction expectedAction)
    {
        if (actor == null
            || actor.IsDead
            || !actor.isActiveAndEnabled
            || actor.CurrentLifecycleState != CharacterLifecycleState.Active)
        {
            return true;
        }

        return expectedAction != null
            && (actor.Brain == null || actor.Brain.bestAction != expectedAction);
    }

    private void ReleaseReservation(WildlifeHuntJob job)
    {
        if (job.IsValid && actor != null && wildlifeRuntime != null)
        {
            wildlifeRuntime.ReleaseHuntReservation(job.WildlifeId, actor);
        }
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
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.EndExpectedAction(
                actor.Brain.bestAction,
                terminalKind,
                clearFailures);
        }

        huntingRoutine = null;
        huntExecutionActive = false;
    }

    private void CacheReferences()
    {
        actor = actor != null ? actor : GetComponent<CharacterActor>();
        move = move != null ? move : GetComponent<AbilityMove>();
    }
}
