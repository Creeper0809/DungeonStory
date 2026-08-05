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

    public bool IsHunting => huntingRoutine != null;

    private void Awake()
    {
        CacheReferences();
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
            EndAiAction();
            return;
        }

        StopHunting("재시작");
        if (!wildlifeRuntime.TryReserveBestHuntJob(
                actor,
                out WildlifeHuntJob job,
                out string reason))
        {
            actor.Brain?.SetActionPhase("사냥 대기", null, reason);
            EndAiAction();
            return;
        }

        activeJob = job;
        huntingRoutine = StartCoroutine(HuntRoutine(job));
    }

    public void StopHunting(string reason)
    {
        if (huntingRoutine != null)
        {
            StopCoroutine(huntingRoutine);
            huntingRoutine = null;
        }

        ReleaseReservation(activeJob);
        activeJob = default;
    }

    private IEnumerator HuntRoutine(WildlifeHuntJob job)
    {
        if (job.Target == null || wildlifeRuntime == null || !TryGetGrid(out Grid grid))
        {
            EndAiAction();
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
                EndAiAction();
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
                if (IsActionCancelled(expectedAction)
                    || !wildlifeRuntime.TryReloadHuntWeapon(actor, out reloadMessage))
                {
                    actor.Brain?.SetActionPhase(
                        "사냥 중단",
                        null,
                        string.IsNullOrWhiteSpace(reloadMessage) ? "재장전 실패" : reloadMessage);
                    ReleaseReservation(job);
                    EndAiAction();
                    yield break;
                }
            }

            if (!wildlifeRuntime.CanAttackHuntTargetFrom(
                    actor,
                    job.Target,
                    grid,
                    actor.GetNowXY()))
            {
                Queue<GridMoveStep> path = actor.PathSearchBroker?.GetMovePath(
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
                    EndAiAction();
                    yield break;
                }

                actor.Brain?.SetActionPhase("사냥 위치로 이동", null, job.Target.DisplayName);
                yield return move.MoveByPath(path, expectedAction);
                if (IsActionCancelled(expectedAction)
                    || (move.LastGridMoveWasBlocked
                        && !wildlifeRuntime.CanAttackHuntTargetFrom(
                            actor,
                            job.Target,
                            grid,
                            actor.GetNowXY())))
                {
                    ReleaseReservation(job);
                    EndAiAction();
                    yield break;
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
                EndAiAction();
                yield break;
            }

            if (job.Target == null || !job.Target.IsAlive)
            {
                break;
            }

            yield return new WaitForSeconds(
                Mathf.Max(0.15f, wildlifeRuntime.GetHuntAttackInterval(actor)));
        }

        ReleaseReservation(job);
        huntingRoutine = null;
        activeJob = default;
        EndAiAction();
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

    private bool IsActionCancelled(AIAction expectedAction)
    {
        return expectedAction != null
            && (actor == null || actor.Brain == null || actor.Brain.bestAction != expectedAction);
    }

    private void ReleaseReservation(WildlifeHuntJob job)
    {
        if (job.IsValid && actor != null && wildlifeRuntime != null)
        {
            wildlifeRuntime.ReleaseHuntReservation(job.WildlifeId, actor);
        }
    }

    private void EndAiAction()
    {
        if (actor != null && actor.Brain != null)
        {
            actor.Brain.isBestActionEnd = true;
            actor.Brain.RequestImmediateReplan(clearFailures: true);
        }

        huntingRoutine = null;
    }

    private void CacheReferences()
    {
        actor = actor != null ? actor : GetComponent<CharacterActor>();
        move = move != null ? move : GetComponent<AbilityMove>();
    }
}
