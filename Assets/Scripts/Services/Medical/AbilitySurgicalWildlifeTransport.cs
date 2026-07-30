using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class AbilitySurgicalWildlifeTransport : MonoBehaviour
{
    private CharacterActor actor;
    private AbilityMove move;
    private ISurgicalPatientTransportRuntime runtime;
    private Coroutine routine;
    private string activeOrderId = string.Empty;

    public bool IsTransporting => routine != null;

    public static AbilitySurgicalWildlifeTransport Ensure(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        AbilitySurgicalWildlifeTransport ability =
            actor.GetComponent<AbilitySurgicalWildlifeTransport>();
        if (ability == null && Application.isPlaying)
        {
            ability = actor.gameObject
                .AddComponent<AbilitySurgicalWildlifeTransport>();
        }

        ability?.CacheReferences();
        return ability;
    }

    public void Configure(ISurgicalPatientTransportRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new System.ArgumentNullException(nameof(runtime));
    }

    public void StartTransport(string orderId)
    {
        CancelCurrent();
        if (runtime == null || string.IsNullOrWhiteSpace(orderId))
        {
            return;
        }

        activeOrderId = orderId.Trim();
        routine = StartCoroutine(TransportRoutine(activeOrderId));
    }

    private IEnumerator TransportRoutine(string orderId)
    {
        if (!runtime.TryGetTransport(
                orderId,
                actor,
                out WildlifeActor patient,
                out Vector2Int destination,
                out bool returning,
                out string failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        if (!TryBuildPath(
                patient.GridPosition,
                DoorAccessOverrideKind.None,
                out Queue<GridMoveStep> pickupPath))
        {
            Fail("동물 환자에게 갈 수 없습니다.");
            yield break;
        }

        actor.Brain?.SetActionPhase(
            returning ? "동물 환자 복귀 준비" : "동물 환자 이송 준비",
            null,
            patient.DisplayName);
        yield return move.MoveByPath(pickupPath);
        if (!runtime.TryBeginCarry(orderId, actor, out failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        using System.IDisposable pass =
            runtime.BeginTransportPass(actor, orderId);
        if (!TryBuildPath(
                destination,
                DoorAccessOverrideKind.EscortPass,
                out Queue<GridMoveStep> destinationPath))
        {
            Fail(returning
                ? "우리로 돌아가는 운반 경로가 없습니다."
                : "수술실까지 이어지는 운반 경로가 없습니다.");
            yield break;
        }

        actor.Brain?.SetActionPhase(
            returning ? "동물 환자를 우리로 운반" : "동물 환자를 수술실로 운반",
            null,
            patient.DisplayName);
        yield return move.MoveByPath(destinationPath);
        if (!runtime.TryCompleteCarry(orderId, actor, out failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        activeOrderId = string.Empty;
        routine = null;
        actor.Brain?.SetActionPhase(
            returning ? "동물 환자 복귀 완료" : "동물 환자 입실 완료",
            null,
            patient.DisplayName);
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

        path = actor.PathSearchBroker?.GetMovePathTo(
            grid,
            actor.GetNowXY(),
            destination,
            GridPathSearchPriority.Urgent,
            GridTraversalContext.ForCharacter(actor, overrideKind));
        return path != null && path.Count > 0;
    }

    private void Fail(string reason)
    {
        string orderId = activeOrderId;
        activeOrderId = string.Empty;
        routine = null;
        runtime?.FailCarry(orderId, actor, reason);
        actor?.Brain?.SetActionPhase("동물 환자 운반 중단", null, reason);
        actor?.Brain?.RequestImmediateReplan(clearFailures: false);
    }

    private void CancelCurrent()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        activeOrderId = string.Empty;
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
