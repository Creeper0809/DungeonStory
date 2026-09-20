using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CaptivityEscapeCompletionRuntime
{
    private readonly CaptivityActorAccess actors;
    private readonly CaptivityActorRuntimeLookup actorRuntime;
    private readonly IDoorAccessSubjectRegistry doorSubjectRegistry;
    private readonly IGameClock gameClock;
    private readonly CaptiveEscapeOutcomeRuntime outcomes;

    public CaptivityEscapeCompletionRuntime(
        CaptivityActorAccess actors,
        CaptivityActorRuntimeLookup actorRuntime,
        IDoorAccessSubjectRegistry doorSubjectRegistry,
        IGameClock gameClock,
        CaptiveEscapeOutcomeRuntime outcomes)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.actorRuntime = actorRuntime
            ?? throw new ArgumentNullException(nameof(actorRuntime));
        this.doorSubjectRegistry = doorSubjectRegistry
            ?? throw new ArgumentNullException(nameof(doorSubjectRegistry));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
    }

    public bool TryCommitNextPendingPhysicalEscape(
        IReadOnlyList<CaptiveState> states,
        out bool attempted,
        out string failureReason)
    {
        attempted = false;
        failureReason = string.Empty;
        if (states == null)
            return true;
        foreach (CaptiveState candidate in states
                     .Where(value => value != null)
                     .OrderBy(value => value.captiveId, StringComparer.Ordinal))
        {
            if (candidate.status != CaptivityStatus.EscapeAttempt
                || candidate.escapeOutcomeRevision > 0L)
            {
                continue;
            }
            CharacterActor candidateActor = actorRuntime.Find(candidate.captiveId);
            if (candidateActor == null
                || candidateActor.CurrentLifecycleState
                    != CharacterLifecycleState.Active
                || candidateActor.GetNowXY() != candidate.escapeDestination)
            {
                continue;
            }
            attempted = true;
            return CompleteEscape(
                candidate.captiveId,
                candidateActor,
                string.Empty,
                out failureReason);
        }
        return true;
    }

    public bool CompleteEscape(
        string captiveId,
        CharacterActor actor,
        string trigger,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptiveState state = actors.FindState(captiveId);
        if (state == null || actor == null)
        {
            failureReason = "탈출 결과를 확정할 포로 또는 실제 인물을 찾을 수 없습니다.";
            return false;
        }
        if (!string.Equals(
                actor.Identity?.PersistentId?.Trim(),
                state.captiveId,
                StringComparison.Ordinal))
        {
            failureReason = "탈출 결과의 포로와 실제 인물이 일치하지 않습니다.";
            return false;
        }
        if (state.status == CaptivityStatus.Escaped)
            return true;
        CaptiveEscapeOutcomeKind kind = state.status switch
        {
            CaptivityStatus.EscapeAttempt =>
                CaptiveEscapeOutcomeKind.PhysicalEscape,
            CaptivityStatus.AwaitingCapture =>
                CaptiveEscapeOutcomeKind.ArrivalCustodyLoss,
            _ => 0
        };
        if (kind == 0
            || actor.CurrentLifecycleState != CharacterLifecycleState.Active)
        {
            failureReason = "현재 상태에서는 탈출 완료를 확정할 수 없습니다.";
            return false;
        }

        string normalizedTrigger = trigger?.Trim() ?? string.Empty;
        if (normalizedTrigger.Length == 0)
        {
            normalizedTrigger = !string.IsNullOrWhiteSpace(state.betrayalTrigger)
                ? state.betrayalTrigger.Trim()
                : kind == CaptiveEscapeOutcomeKind.ArrivalCustodyLoss
                    && !string.IsNullOrWhiteSpace(state.lastResult)
                        ? state.lastResult.Trim()
                        : "감방에서 탈출";
        }
        CaptivityStatus previousStatus = state.status;
        float resultingPressure = ClampStat(
            state.retaliationPressure + 15f + state.grudge * 0.2f);
        if (!outcomes.TryPrepare(
                state,
                kind,
                normalizedTrigger,
                previousStatus,
                resultingPressure,
                CurrentAbsoluteDay,
                out PreparedCaptiveEscapeOutcome prepared,
                out long ownerRevision,
                out normalizedTrigger,
                out failureReason))
        {
            return false;
        }

        string stateSnapshot =
            CaptivityStateTransitionRules.CaptureStateSnapshot(state);
        CharacterType previousActorType = actor.characterType;
        CharacterLifecycleState previousLifecycle = actor.CurrentLifecycleState;
        bool previousAiPaused = actor.IsAiPaused();
        bool previousDoorCaptive = state.IsInCustody;
        try
        {
            state.status = CaptivityStatus.Escaped;
            state.restrained = false;
            state.lastResult = normalizedTrigger;
            state.retaliationPressure = resultingPressure;
            state.escapeOutcomeRevision = ownerRevision;
            actor.characterType = CharacterType.Intruder;
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.SetAiPaused(false);
            doorSubjectRegistry.SetCaptive(state.captiveId, false);
        }
        catch (Exception exception)
        {
            outcomes.Cancel(prepared);
            RestoreCompletionState(
                state,
                stateSnapshot,
                actor,
                previousActorType,
                previousLifecycle,
                previousAiPaused,
                previousDoorCaptive);
            failureReason = "탈출 완료 상태를 적용하지 못했습니다: "
                + exception.Message;
            return false;
        }

        OwnerOutcomeCommitResult commit = outcomes.Commit(prepared);
        if (!commit.DurablyCommitted)
        {
            RestoreCompletionState(
                state,
                stateSnapshot,
                actor,
                previousActorType,
                previousLifecycle,
                previousAiPaused,
                previousDoorCaptive);
            failureReason = commit.DetailCode.Length == 0
                ? "포로 탈출 원장 커밋이 거절되었습니다."
                : commit.DetailCode;
            return false;
        }

        if (!prepared.IsReplay)
            outcomes.NotifyCommitted(state, normalizedTrigger, betrayal: false);
        try
        {
            actor.GetAbility<AbilityMove>()?.StartSystemExitDungeon();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "확정된 포로 탈출의 퇴장 명령 관찰자가 실패했습니다: "
                + exception);
        }
        return true;
    }

    private void RestoreCompletionState(
        CaptiveState state,
        string stateSnapshot,
        CharacterActor actor,
        CharacterType previousActorType,
        CharacterLifecycleState previousLifecycle,
        bool previousAiPaused,
        bool previousDoorCaptive)
    {
        CaptivityStateTransitionRules.RestoreStateSnapshot(stateSnapshot, state);
        actor.characterType = previousActorType;
        actor.SetLifecycleState(previousLifecycle);
        actor.SetAiPaused(previousAiPaused);
        doorSubjectRegistry.SetCaptive(state.captiveId, previousDoorCaptive);
    }

    private int CurrentAbsoluteDay => Mathf.Max(
        0,
        Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay));

    private static float ClampStat(float value) => Mathf.Clamp(value, 0f, 100f);
}
