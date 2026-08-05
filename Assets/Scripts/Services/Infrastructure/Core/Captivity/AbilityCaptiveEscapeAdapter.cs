using System.Collections;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AbilityCaptiveEscape : MonoBehaviour
{
    private ICaptiveEscapeAbilityPort port;
    private IGameClock clock;
    private Coroutine routine;
    private string activeCaptiveId = string.Empty;

    public bool IsEscaping => routine != null;

    public void Configure(ICaptiveEscapeAbilityPort port, IGameClock clock)
    {
        this.port = port ?? throw new System.ArgumentNullException(nameof(port));
        this.clock = clock ?? throw new System.ArgumentNullException(nameof(clock));
    }

    public void StartEscape(string captiveId)
    {
        CancelCurrent(reportFailure: false, string.Empty);
        if (port == null
            || clock == null
            || !CaptiveEscapeAbilityRules.TryNormalizeId(
                captiveId,
                out string normalizedId))
        {
            return;
        }

        activeCaptiveId = normalizedId;
        routine = StartCoroutine(EscapeRoutine(activeCaptiveId));
    }

    private IEnumerator EscapeRoutine(string captiveId)
    {
        if (!port.TryGetEscapeState(
                captiveId,
                out Vector2Int destination,
                out string failureReason))
        {
            Fail(failureReason);
            yield break;
        }

        using System.IDisposable pass = port.BeginEscapePass(captiveId);
        port.SetActionPhase("탈출 경로로 이동 중", destination);

        float elapsed = 0f;
        float repathElapsed = CaptiveEscapeAbilityRules.RepathIntervalSeconds;
        Vector2Int lastPosition = port.Position;
        while (port.IsAlive
               && port.Position != destination
               && elapsed < CaptiveEscapeAbilityRules.EscapeTimeoutSeconds)
        {
            float delta = Mathf.Max(0f, clock.DeltaTime);
            elapsed += delta;
            repathElapsed += delta;
            Vector2Int current = port.Position;
            if (current != lastPosition)
            {
                lastPosition = current;
                repathElapsed = 0f;
            }

            if (repathElapsed >= CaptiveEscapeAbilityRules.RepathIntervalSeconds)
            {
                repathElapsed = 0f;
                if (!port.TryStartSystemMove(destination, out failureReason))
                {
                    Fail(failureReason);
                    yield break;
                }
            }

            yield return null;
        }

        if (port.IsAlive && port.Position == destination)
        {
            activeCaptiveId = string.Empty;
            routine = null;
            port.CompleteEscape(captiveId);
            yield break;
        }

        Fail(port.IsAlive
            ? "탈출 경로를 확보하지 못했습니다."
            : "탈출 중 쓰러졌습니다.");
    }

    private void Fail(string reason)
    {
        string captiveId = activeCaptiveId;
        activeCaptiveId = string.Empty;
        routine = null;
        port?.FailEscape(captiveId, reason);
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
            port?.FailEscape(activeCaptiveId, reason);
        }

        activeCaptiveId = string.Empty;
    }
}
