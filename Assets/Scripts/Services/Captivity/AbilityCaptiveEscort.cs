using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class AbilityCaptiveEscort : MonoBehaviour
{
    private CharacterActor actor;
    private AbilityMove move;
    private ICaptivityEscortRuntime runtime;
    private IGameClock gameClock;
    private Coroutine routine;
    private string activeCaptiveId = string.Empty;

    public bool IsEscorting => routine != null;

    private void Awake()
    {
        CacheReferences();
    }

    public static AbilityCaptiveEscort Ensure(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        AbilityCaptiveEscort ability = actor.GetComponent<AbilityCaptiveEscort>();
        if (ability == null && Application.isPlaying)
        {
            ability = actor.gameObject.AddComponent<AbilityCaptiveEscort>();
        }

        ability?.CacheReferences();
        return ability;
    }

    public void Configure(ICaptivityEscortRuntime runtime, IGameClock gameClock)
    {
        this.runtime = runtime
            ?? throw new System.ArgumentNullException(nameof(runtime));
        this.gameClock = gameClock
            ?? throw new System.ArgumentNullException(nameof(gameClock));
    }

    public void StartEscort(string captiveId)
    {
        StopEscort("새 호송 명령");
        if (runtime == null
            || gameClock == null
            || string.IsNullOrWhiteSpace(captiveId))
        {
            return;
        }

        activeCaptiveId = captiveId.Trim();
        routine = StartCoroutine(EscortRoutine(activeCaptiveId));
    }

    public void StopEscort(string reason)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (!string.IsNullOrWhiteSpace(activeCaptiveId))
        {
            runtime?.FailEscort(activeCaptiveId, actor, reason);
        }

        activeCaptiveId = string.Empty;
    }

    private IEnumerator EscortRoutine(string captiveId)
    {
        if (!TryGetState(captiveId, out CaptiveState state, out _, out string failure))
        {
            Fail(failure);
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(state.restraintStackId))
        {
            if (!TryBuildPath(
                    state.restraintPickupPosition,
                    DoorAccessOverrideKind.None,
                    out Queue<GridMoveStep> restraintPath))
            {
                Fail("구속구 보관 위치로 갈 수 없습니다.");
                yield break;
            }

            actor.Brain?.SetActionPhase(
                "구속구를 가지러 이동",
                null,
                state.restraintPickupPosition.ToString());
            yield return move.MoveByPath(restraintPath);
            if (!runtime.TryPickupReservedRestraint(state, actor, out failure))
            {
                Fail(failure);
                yield break;
            }
        }

        if (!TryGetState(captiveId, out state, out CharacterActor subject, out failure)
            || !TryBuildPath(
                subject.GetNowXY(),
                DoorAccessOverrideKind.None,
                out Queue<GridMoveStep> subjectPath))
        {
            Fail(string.IsNullOrWhiteSpace(failure)
                ? "쓰러진 침입자에게 갈 수 없습니다."
                : failure);
            yield break;
        }

        actor.Brain?.SetActionPhase(
            "포로에게 이동",
            null,
            subject.Identity?.DisplayName);
        yield return move.MoveByPath(subjectPath);

        while (TryGetState(captiveId, out state, out subject, out failure)
               && !state.stabilized)
        {
            float work = actor.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Warden)
                * gameClock.DeltaTime;
            float progress = runtime.AdvanceStabilization(captiveId, actor, work);
            actor.Brain?.SetActionPhase(
                $"포로 안정화 {Mathf.RoundToInt(progress * 100f)}%",
                null,
                subject.Identity?.DisplayName);
            yield return null;
        }

        if (!runtime.TryBeginEscort(captiveId, actor, out failure))
        {
            Fail(failure);
            yield break;
        }

        if (!TryGetState(captiveId, out state, out subject, out failure))
        {
            Fail(failure);
            yield break;
        }

        using System.IDisposable escortPass =
            runtime.BeginEscortPass(actor, captiveId);

        if (!TryBuildPath(
                state.housingPosition,
                DoorAccessOverrideKind.EscortPass,
                out Queue<GridMoveStep> housingPath))
        {
            Fail("감방까지 안전한 호송 경로가 없습니다.");
            yield break;
        }

        actor.Brain?.SetActionPhase(
            "포로 호송",
            null,
            state.housingPosition.ToString());
        yield return move.MoveByPath(housingPath);
        if (!runtime.TryCompleteEscort(captiveId, actor, out failure))
        {
            Fail(failure);
            yield break;
        }

        activeCaptiveId = string.Empty;
        routine = null;
        actor.Brain?.SetActionPhase(
            "포로 수용 완료",
            null,
            subject.Identity?.DisplayName);
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
    }

    private bool TryBuildPath(
        Vector2Int destination,
        DoorAccessOverrideKind overrideKind,
        out Queue<GridMoveStep> path)
    {
        path = new Queue<GridMoveStep>();
        if (actor?.WorldRegistry == null
            || !actor.WorldRegistry.TryGetGrid(out Grid grid))
        {
            return false;
        }

        if (actor.GetNowXY() == destination)
        {
            return true;
        }

        path = actor.PathSearchBroker?.GetMovePath(
            grid,
            actor.GetNowXY(),
            cell => cell == destination,
            GridPathSearchPriority.Urgent,
            GridTraversalContext.ForCharacter(actor, overrideKind));
        return path != null && path.Count > 0;
    }

    private bool TryGetState(
        string captiveId,
        out CaptiveState state,
        out CharacterActor subject,
        out string failure)
    {
        return runtime.TryGetEscortState(
            captiveId,
            actor,
            out state,
            out subject,
            out failure);
    }

    private void Fail(string reason)
    {
        runtime?.FailEscort(activeCaptiveId, actor, reason);
        activeCaptiveId = string.Empty;
        routine = null;
        actor?.Brain?.SetActionPhase("호송 중단", null, reason);
        actor?.Brain?.RequestImmediateReplan(clearFailures: false);
    }

    private void CacheReferences()
    {
        actor = actor != null ? actor : GetComponent<CharacterActor>();
        move = move != null ? move : GetComponent<AbilityMove>();
    }
}
