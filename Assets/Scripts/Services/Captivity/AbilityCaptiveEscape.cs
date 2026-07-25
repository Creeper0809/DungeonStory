using System.Collections;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class AbilityCaptiveEscape : MonoBehaviour
{
    private const float RepathIntervalSeconds = 3f;
    private const float EscapeTimeoutSeconds = 35f;

    private CharacterActor actor;
    private AbilityMove move;
    private ICaptivityEscapeRuntime runtime;
    private IGameClock clock;
    private Coroutine routine;
    private string activeCaptiveId = string.Empty;

    public bool IsEscaping => routine != null;

    public static AbilityCaptiveEscape Ensure(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        AbilityCaptiveEscape ability = actor.GetComponent<AbilityCaptiveEscape>();
        if (ability == null && Application.isPlaying)
        {
            ability = actor.gameObject.AddComponent<AbilityCaptiveEscape>();
        }

        ability?.CacheReferences();
        return ability;
    }

    public void Configure(
        ICaptivityEscapeRuntime runtime,
        IGameClock clock)
    {
        this.runtime = runtime
            ?? throw new System.ArgumentNullException(nameof(runtime));
        this.clock = clock
            ?? throw new System.ArgumentNullException(nameof(clock));
    }

    public void StartEscape(string captiveId)
    {
        CancelCurrent(reportFailure: false, string.Empty);
        if (runtime == null
            || clock == null
            || string.IsNullOrWhiteSpace(captiveId))
        {
            return;
        }

        activeCaptiveId = captiveId.Trim();
        routine = StartCoroutine(EscapeRoutine(activeCaptiveId));
    }

    private IEnumerator EscapeRoutine(string captiveId)
    {
        if (!runtime.TryGetEscapeState(
                captiveId,
                actor,
                out Vector2Int destination,
                out string failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        using System.IDisposable pass =
            runtime.BeginEscapePass(actor, captiveId);
        actor.Brain?.SetActionPhase(
            "훔친 열쇠로 탈출 중",
            null,
            destination.ToString());

        float elapsed = 0f;
        float repathElapsed = RepathIntervalSeconds;
        Vector2Int lastPosition = actor.GetNowXY();
        while (actor != null
               && !actor.IsDead
               && actor.GetNowXY() != destination
               && elapsed < EscapeTimeoutSeconds)
        {
            float delta = Mathf.Max(0f, clock.DeltaTime);
            elapsed += delta;
            repathElapsed += delta;
            Vector2Int current = actor.GetNowXY();
            if (current != lastPosition)
            {
                lastPosition = current;
                repathElapsed = 0f;
            }

            if (repathElapsed >= RepathIntervalSeconds)
            {
                repathElapsed = 0f;
                if (!move.TryStartSystemMove(
                        destination,
                        DoorAccessOverrideKind.CaptiveEscape,
                        out failureReason))
                {
                    Fail(failureReason);
                    yield break;
                }
            }

            yield return null;
        }

        if (actor != null && !actor.IsDead && actor.GetNowXY() == destination)
        {
            activeCaptiveId = string.Empty;
            routine = null;
            runtime.CompleteEscape(captiveId, actor);
            yield break;
        }

        Fail(actor == null || actor.IsDead
            ? "탈출 중 쓰러졌습니다."
            : "탈출 경로를 확보하지 못했습니다.");
    }

    private void Fail(string reason)
    {
        string captiveId = activeCaptiveId;
        activeCaptiveId = string.Empty;
        routine = null;
        runtime?.FailEscape(captiveId, actor, reason);
    }

    private void CancelCurrent(bool reportFailure, string reason)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (reportFailure && !string.IsNullOrWhiteSpace(activeCaptiveId))
        {
            runtime?.FailEscape(activeCaptiveId, actor, reason);
        }

        activeCaptiveId = string.Empty;
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
