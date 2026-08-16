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
    private System.IDisposable activeEscapePass;
    private bool escapeExecutionActive;

    public bool IsEscaping => escapeExecutionActive;
    public bool HasEscapePassForDiagnostics => activeEscapePass != null;

    public void Configure(ICaptiveEscapeAbilityPort port, IGameClock clock)
    {
        this.port = port ?? throw new System.ArgumentNullException(nameof(port));
        this.clock = clock ?? throw new System.ArgumentNullException(nameof(clock));
    }

    public void StartEscape(string captiveId)
    {
        CancelCurrent(
            reportFailure: escapeExecutionActive,
            reason: "escape-restarted");
        if (port == null
            || clock == null
            || !CaptiveEscapeAbilityRules.TryNormalizeId(
                captiveId,
                out string normalizedId))
        {
            return;
        }

        activeCaptiveId = normalizedId;
        escapeExecutionActive = true;
        Coroutine started = StartCoroutine(EscapeRoutine(activeCaptiveId));
        routine = escapeExecutionActive ? started : null;
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

        ReplaceEscapePass(port.BeginEscapePass(captiveId));
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
            escapeExecutionActive = false;
            activeCaptiveId = string.Empty;
            routine = null;
            ReleaseEscapePass();
            port.CompleteEscape(captiveId);
            yield break;
        }

        Fail(port.IsAlive
            ? "탈출 경로를 확보하지 못했습니다."
            : "탈출 중 쓰러졌습니다.");
    }

    private void Fail(string reason)
    {
        if (!escapeExecutionActive)
        {
            return;
        }

        string captiveId = activeCaptiveId;
        escapeExecutionActive = false;
        activeCaptiveId = string.Empty;
        routine = null;
        ReleaseEscapePass();
        if (!string.IsNullOrWhiteSpace(captiveId))
        {
            port?.FailEscape(captiveId, reason);
        }
    }

    private void CancelCurrent(bool reportFailure, string reason)
    {
        bool wasActive = escapeExecutionActive;
        string captiveId = activeCaptiveId;
        escapeExecutionActive = false;
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        ReleaseEscapePass();
        if (reportFailure
            && wasActive
            && !string.IsNullOrWhiteSpace(captiveId))
        {
            port?.FailEscape(captiveId, reason);
        }

        activeCaptiveId = string.Empty;
    }

    [GameplayInternalOnly(
        "Character lifecycle termination must synchronously close the escape action and its temporary door pass before Unity can stop its coroutine.",
        "CharacterActor.ReleaseTransientAiOwnership")]
    public void StopForLifecycleTransition(string reason)
    {
        CancelCurrent(
            reportFailure: escapeExecutionActive,
            reason: string.IsNullOrWhiteSpace(reason)
                ? "escape-actor-lifecycle-ended"
                : reason.Trim());
    }

    private void ReplaceEscapePass(System.IDisposable pass)
    {
        ReleaseEscapePass();
        activeEscapePass = pass;
    }

    private void ReleaseEscapePass()
    {
        System.IDisposable pass = activeEscapePass;
        activeEscapePass = null;
        pass?.Dispose();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            StopForLifecycleTransition("escape-actor-disabled");
        }
    }
}
