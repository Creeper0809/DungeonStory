using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal delegate bool CaptivityRansomTerminalPhysicalFinalizer(
    CaptiveState state,
    out string failureReason);

internal interface ICaptivityRansomTerminalPhysicalPort
{
    bool TryFinalize(CaptiveState state, out string failureReason);
}

internal sealed class CaptivityRansomTerminalPhysicalPort :
    ICaptivityRansomTerminalPhysicalPort
{
    private readonly CaptivityRansomTerminalPhysicalFinalizer finalizer;

    public CaptivityRansomTerminalPhysicalPort(
        CaptivityRansomTerminalPhysicalFinalizer finalizer) =>
        this.finalizer = finalizer
            ?? throw new ArgumentNullException(nameof(finalizer));

    public bool TryFinalize(CaptiveState state, out string failureReason) =>
        finalizer(state, out failureReason);
}

internal sealed class CaptivityRansomRuntime
{
    private readonly CaptivityActorAccess actors;
    private readonly CaptivityActorRuntimeLookup actorRuntime;
    private readonly CaptivityPolicyRuntime policies;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly IGameMoneyAccount money;
    private readonly ICaptivityRansomTerminalPhysicalPort terminalPhysical;
    private readonly IDoorAccessSubjectRegistry doorSubjectRegistry;
    private readonly IGameClock gameClock;
    private readonly CaptiveRansomOutcomeRuntime outcomes;

    public CaptivityRansomRuntime(
        CaptivityActorAccess actors,
        CaptivityActorRuntimeLookup actorRuntime,
        CaptivityPolicyRuntime policies,
        ICharacterBodyHealthQuery bodyHealth,
        IGameMoneyAccount money,
        ICaptivityRansomTerminalPhysicalPort terminalPhysical,
        IDoorAccessSubjectRegistry doorSubjectRegistry,
        IGameClock gameClock,
        CaptiveRansomOutcomeRuntime outcomes)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.actorRuntime = actorRuntime
            ?? throw new ArgumentNullException(nameof(actorRuntime));
        this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.terminalPhysical = terminalPhysical
            ?? throw new ArgumentNullException(nameof(terminalPhysical));
        this.doorSubjectRegistry = doorSubjectRegistry
            ?? throw new ArgumentNullException(nameof(doorSubjectRegistry));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
    }

    public bool TryRansom(
        string captiveId,
        out int paidAmount,
        out string failureReason)
    {
        paidAmount = 0;
        failureReason = string.Empty;
        CaptiveState state = actors.FindState(captiveId);
        CharacterActor actor = actorRuntime.Find(captiveId);
        if (state == null || actor == null)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }
        if (!ActorMatches(state, actor))
        {
            failureReason = "몸값 대상 포로와 실제 인물이 일치하지 않습니다.";
            return false;
        }
        if (state.status == CaptivityStatus.Ransom
            && state.ransomOutcomeRevision > 0L)
        {
            return TryFinalizePending(
                state,
                actor,
                out paidAmount,
                out failureReason);
        }
        if (!state.IsInCustody)
        {
            failureReason = "포로를 찾을 수 없습니다.";
            return false;
        }

        CaptivePolicyData policy = policies.Find(state.policyId);
        if (policy?.allowRansom != true)
        {
            failureReason = "현재 수용 정책은 몸값 협상을 허용하지 않습니다.";
            return false;
        }
        if (!CaptiveRansomOutcomeIds.IsPreviousStatusValid(state.status))
        {
            failureReason = "현재 진행 중인 포로 작업을 먼저 끝내야 합니다.";
            return false;
        }

        CaptivityStatus previousStatus = state.status;
        int amount = state.CalculateRansomValue(GetHealthPercent(actor));
        float resultingPressure = ClampStat(
            state.retaliationPressure + state.grudge * 0.35f);
        if (!outcomes.TryPrepare(
                state,
                previousStatus,
                amount,
                resultingPressure,
                CurrentAbsoluteDay,
                out PreparedCaptiveRansomOutcome prepared,
                out long ownerRevision,
                out failureReason))
        {
            return false;
        }

        string stateSnapshot =
            CaptivityStateTransitionRules.CaptureStateSnapshot(state);
        try
        {
            state.status = CaptivityStatus.Ransom;
            state.retaliationPressure = resultingPressure;
            state.ransomOutcomeRevision = ownerRevision;
            state.ransomAcceptedAmount = amount;
            state.ransomIncomeCredited = false;
            state.ransomObserversPending = !prepared.IsReplay;
        }
        catch (Exception exception)
        {
            outcomes.Cancel(prepared);
            CaptivityStateTransitionRules.RestoreStateSnapshot(
                stateSnapshot,
                state);
            failureReason = "몸값 대기 상태를 적용하지 못했습니다: "
                + exception.Message;
            return false;
        }

        OwnerOutcomeCommitResult commit = outcomes.Commit(prepared);
        if (!commit.DurablyCommitted)
        {
            CaptivityStateTransitionRules.RestoreStateSnapshot(
                stateSnapshot,
                state);
            failureReason = commit.DetailCode.Length == 0
                ? "포로 몸값 원장 커밋이 거절되었습니다."
                : commit.DetailCode;
            return false;
        }

        if (TryFinalizePending(
                state,
                actor,
                out paidAmount,
                out failureReason))
        {
            return true;
        }
        failureReason = "몸값 결과는 확정되었지만 마무리를 재시도합니다: "
            + failureReason;
        return false;
    }

    public bool TryFinalizeNextPending(
        IReadOnlyList<CaptiveState> states,
        out bool attempted,
        out string failureReason)
    {
        attempted = false;
        failureReason = string.Empty;
        if (states == null)
            return true;
        foreach (CaptiveState state in states
                     .Where(value => value != null)
                     .OrderBy(value => value.captiveId, StringComparer.Ordinal))
        {
            if (state.status != CaptivityStatus.Ransom
                || state.ransomOutcomeRevision <= 0L)
            {
                continue;
            }
            attempted = true;
            CharacterActor actor = actorRuntime.Find(state.captiveId);
            return TryFinalizePending(
                state,
                actor,
                out _,
                out failureReason);
        }
        return true;
    }

    private bool TryFinalizePending(
        CaptiveState state,
        CharacterActor actor,
        out int paidAmount,
        out string failureReason)
    {
        paidAmount = 0;
        failureReason = string.Empty;
        if (state == null
            || actor == null
            || state.status != CaptivityStatus.Ransom
            || state.ransomOutcomeRevision <= 0L
            || state.ransomAcceptedAmount <= 0)
        {
            failureReason = "완료할 몸값 대기 상태가 올바르지 않습니다.";
            return false;
        }
        if (!ActorMatches(state, actor))
        {
            failureReason = "몸값 대상 포로와 실제 인물이 일치하지 않습니다.";
            return false;
        }

        int amount = state.ransomAcceptedAmount;
        if (!state.ransomIncomeCredited)
        {
            if (money is not IIdempotentGameMoneyAccount idempotentMoney)
            {
                failureReason = "재화 계정이 중복 방지 입금을 지원하지 않습니다.";
                return false;
            }
            EconomyTransactionContext context = new(
                EconomyTransactionKind.RansomIncome,
                state.captiveId,
                state.captiveId,
                "포로 몸값");
            if (!idempotentMoney.TryCreditOnce(
                    amount,
                    context,
                    out failureReason))
            {
                failureReason = "포로 몸값을 중복 없이 입금할 수 없습니다: "
                    + failureReason;
                return false;
            }
            state.ransomIncomeCredited = true;
        }

        if (!terminalPhysical.TryFinalize(state, out failureReason))
        {
            failureReason = "포로 몸값의 물리 소유권을 종결할 수 없습니다: "
                + failureReason;
            return false;
        }

        bool notify = state.ransomObserversPending;
        try
        {
            actor.characterType = CharacterType.Intruder;
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.SetAiPaused(false);
            doorSubjectRegistry.SetCaptive(state.captiveId, false);
            state.restrained = false;
            state.lastResult = $"몸값 {amount:N0}을 받고 석방";
            state.ransomObserversPending = false;
            state.status = CaptivityStatus.Released;
        }
        catch (Exception exception)
        {
            failureReason = "포로 몸값 석방 상태를 마무리하지 못했습니다: "
                + exception.Message;
            return false;
        }

        paidAmount = amount;
        if (notify)
            outcomes.NotifyCommitted(state, actor, amount);
        try
        {
            actor.GetAbility<AbilityMove>()?.StartSystemExitDungeon();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "확정된 몸값 석방의 퇴장 명령 관찰자가 실패했습니다: "
                + exception);
        }
        return true;
    }

    private float GetHealthPercent(CharacterActor actor)
    {
        CharacterVitalsSnapshot vitals = bodyHealth.GetVitals(actor);
        return CaptivityBodyHealthRules.GetHealthPercent(
            vitals.CurrentHealth,
            vitals.MaximumHealth);
    }

    private static bool ActorMatches(CaptiveState state, CharacterActor actor) =>
        string.Equals(
            actor?.Identity?.PersistentId?.Trim(),
            state?.captiveId,
            StringComparison.Ordinal);

    private int CurrentAbsoluteDay => Mathf.Max(
        0,
        Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay));

    private static float ClampStat(float value) => Mathf.Clamp(value, 0f, 100f);
}
