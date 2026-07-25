using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AbilityWildlifeCaptureTransport : MonoBehaviour
{
    private CharacterActor actor;
    private AbilityMove move;
    private IWildlifeCaptureTransportRuntime runtime;
    private Coroutine routine;
    private string activeWildlifeId = string.Empty;

    public bool IsTransporting => routine != null;

    public static AbilityWildlifeCaptureTransport Ensure(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        AbilityWildlifeCaptureTransport ability =
            actor.GetComponent<AbilityWildlifeCaptureTransport>();
        if (ability == null && Application.isPlaying)
        {
            ability = actor.gameObject
                .AddComponent<AbilityWildlifeCaptureTransport>();
        }

        ability?.CacheReferences();
        return ability;
    }

    public void Configure(IWildlifeCaptureTransportRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new System.ArgumentNullException(nameof(runtime));
    }

    public void StartTransport(string wildlifeId)
    {
        CancelCurrent(reportFailure: false, string.Empty);
        if (runtime == null || string.IsNullOrWhiteSpace(wildlifeId))
        {
            return;
        }

        activeWildlifeId = wildlifeId.Trim();
        routine = StartCoroutine(TransportRoutine(activeWildlifeId));
    }

    private IEnumerator TransportRoutine(string wildlifeId)
    {
        if (!runtime.TryGetTransportState(
                wildlifeId,
                actor,
                out CapturedWildlifeState state,
                out WildlifeActor wildlife,
                out string failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        if (!TryBuildPath(
                wildlife.GridPosition,
                DoorAccessOverrideKind.None,
                out Queue<GridMoveStep> pickupPath))
        {
            Fail("생포할 동물에게 갈 수 없습니다.");
            yield break;
        }

        actor.Brain?.SetActionPhase(
            "운반 상자를 들고 동물에게 이동",
            null,
            wildlife.DisplayName);
        yield return move.MoveByPath(pickupPath);
        if (!runtime.TryBeginCarry(wildlifeId, actor, out failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        using System.IDisposable pass =
            runtime.BeginTransportPass(actor, wildlifeId);
        if (!TryBuildPath(
                state.penPosition,
                DoorAccessOverrideKind.EscortPass,
                out Queue<GridMoveStep> penPath))
        {
            Fail("야수 우리까지 이어지는 운반 경로가 없습니다.");
            yield break;
        }

        actor.Brain?.SetActionPhase(
            "포획 동물을 우리로 운반",
            null,
            state.penPosition.ToString());
        yield return move.MoveByPath(penPath);
        if (!runtime.TryCompleteCarry(wildlifeId, actor, out failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        activeWildlifeId = string.Empty;
        routine = null;
        actor.Brain?.SetActionPhase(
            "포획 동물 수용 완료",
            null,
            wildlife.DisplayName);
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

    private void Fail(string reason)
    {
        string wildlifeId = activeWildlifeId;
        activeWildlifeId = string.Empty;
        routine = null;
        runtime?.FailCarry(wildlifeId, actor, reason);
        actor?.Brain?.SetActionPhase("동물 운반 중단", null, reason);
        actor?.Brain?.RequestImmediateReplan(clearFailures: false);
    }

    private void CancelCurrent(bool reportFailure, string reason)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (reportFailure && !string.IsNullOrWhiteSpace(activeWildlifeId))
        {
            runtime?.FailCarry(activeWildlifeId, actor, reason);
        }

        activeWildlifeId = string.Empty;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        actor = actor != null ? actor : GetComponent<CharacterActor>();
        move = move != null ? move : GetComponent<AbilityMove>();
    }
}
