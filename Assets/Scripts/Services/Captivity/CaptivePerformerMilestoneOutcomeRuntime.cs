using System;
using System.Collections.Generic;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public sealed class CaptivePerformerMilestoneOutcomeRuntime
{
    private readonly ICaptivePerformerMilestoneOutcomeCommitter outcomes;
    private readonly ICaptivityPerformerPort port;

    public CaptivePerformerMilestoneOutcomeRuntime(
        ICaptivePerformerMilestoneOutcomeCommitter outcomes,
        ICaptivityPerformerPort port)
    {
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
        this.port = port ?? throw new ArgumentNullException(nameof(port));
    }

    public bool TryCommitEligibleMilestones(
        CaptiveState state,
        int absoluteDay,
        out string failureReason)
    {
        failureReason = string.Empty;
        while (true)
        {
            int threshold = NextEligibleThreshold(state);
            if (threshold == 0)
                return true;
            if (!TryCommitMilestone(
                    state,
                    threshold,
                    absoluteDay,
                    out failureReason))
            {
                return false;
            }
        }
    }

    public bool TryCommitNextPendingMilestone(
        IReadOnlyList<CaptiveState> states,
        int absoluteDay,
        out bool attempted,
        out string failureReason)
    {
        attempted = false;
        failureReason = string.Empty;
        if (states == null)
            return true;

        for (int index = 0; index < states.Count; index++)
        {
            CaptiveState state = states[index];
            int threshold = NextEligibleThreshold(state);
            if (threshold == 0)
                continue;
            attempted = true;
            return TryCommitMilestone(
                state,
                threshold,
                absoluteDay,
                out failureReason);
        }
        return true;
    }

    private bool TryCommitMilestone(
        CaptiveState state,
        int threshold,
        int absoluteDay,
        out string failureReason)
    {
        failureReason = string.Empty;
        long ownerRevision;
        try
        {
            ownerRevision = checked(state.performerMilestoneOutcomeRevision + 1L);
        }
        catch (OverflowException)
        {
            failureReason = "공연자 이정표 원장 리비전이 범위를 초과했습니다.";
            return false;
        }

        CaptivePerformerMilestoneOutcomeReceipt receipt;
        try
        {
            receipt = new CaptivePerformerMilestoneOutcomeReceipt(
                state.captiveId,
                CaptureName(state),
                threshold,
                state.performerFame,
                ownerRevision,
                absoluteDay);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            failureReason = "공연자 이정표 영수증을 만들 수 없습니다: "
                + exception.Message;
            return false;
        }

        if (!outcomes.TryPrepare(receipt, out var prepared, out failureReason))
            return false;

        string previousResult = state.lastResult;
        long previousHighWater = state.performerMilestoneOutcomeRevision;
        long previousThresholdRevision = ThresholdRevision(state, threshold);
        bool previousFlag = ThresholdFlag(state, threshold);
        string message = MilestoneMessage(threshold);
        ApplyMilestoneState(state, threshold, ownerRevision, message);

        OwnerOutcomeCommitResult commit = outcomes.Commit(prepared);
        if (!commit.DurablyCommitted)
        {
            RestoreMilestoneState(
                state,
                threshold,
                previousHighWater,
                previousThresholdRevision,
                previousFlag,
                previousResult);
            failureReason = commit.DetailCode.Length == 0
                ? "공연자 이정표 원장 커밋이 거절되었습니다."
                : commit.DetailCode;
            return false;
        }

        if (!prepared.IsReplay)
            NotifyObservers(state, threshold, message);
        failureReason = string.Empty;
        return true;
    }

    private static int NextEligibleThreshold(CaptiveState state)
    {
        if (state == null)
            return 0;
        if (state.performerFame >= 50f && !state.carePriorityUnlocked)
            return 50;
        if (state.performerFame >= 75f && !state.staffContractUnlocked)
            return 75;
        if (state.performerFame >= 100f
            && state.resolvedMilestoneChoice == CaptivePerformerMilestoneChoice.None
            && !state.finalContractPending)
        {
            return 100;
        }
        return 0;
    }

    private static void ApplyMilestoneState(
        CaptiveState state,
        int threshold,
        long ownerRevision,
        string message)
    {
        state.performerMilestoneOutcomeRevision = ownerRevision;
        state.lastResult = message;
        switch (threshold)
        {
            case 50:
                state.carePriorityUnlocked = true;
                state.carePriorityOutcomeRevision = ownerRevision;
                break;
            case 75:
                state.staffContractUnlocked = true;
                state.staffContractOutcomeRevision = ownerRevision;
                break;
            case 100:
                state.finalContractPending = true;
                state.finalContractOutcomeRevision = ownerRevision;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(threshold));
        }
    }

    private static void RestoreMilestoneState(
        CaptiveState state,
        int threshold,
        long previousHighWater,
        long previousThresholdRevision,
        bool previousFlag,
        string previousResult)
    {
        state.performerMilestoneOutcomeRevision = previousHighWater;
        state.lastResult = previousResult;
        switch (threshold)
        {
            case 50:
                state.carePriorityUnlocked = previousFlag;
                state.carePriorityOutcomeRevision = previousThresholdRevision;
                break;
            case 75:
                state.staffContractUnlocked = previousFlag;
                state.staffContractOutcomeRevision = previousThresholdRevision;
                break;
            case 100:
                state.finalContractPending = previousFlag;
                state.finalContractOutcomeRevision = previousThresholdRevision;
                break;
        }
    }

    private static long ThresholdRevision(CaptiveState state, int threshold) =>
        threshold switch
        {
            50 => state.carePriorityOutcomeRevision,
            75 => state.staffContractOutcomeRevision,
            100 => state.finalContractOutcomeRevision,
            _ => 0L
        };

    private static bool ThresholdFlag(CaptiveState state, int threshold) =>
        threshold switch
        {
            50 => state.carePriorityUnlocked,
            75 => state.staffContractUnlocked,
            100 => state.finalContractPending,
            _ => false
        };

    private static string MilestoneMessage(int threshold) => threshold switch
    {
        50 => "공연 명성으로 우선 식량·치료 특혜를 얻었습니다.",
        75 => "조건을 충족하면 직원 계약을 제안할 수 있습니다.",
        100 => "석방 협상과 전속 투사 계약 중 하나를 선택할 수 있습니다.",
        _ => throw new ArgumentOutOfRangeException(nameof(threshold))
    };

    private static KoreanNameSnapshot CaptureName(CaptiveState state)
    {
        string display = state.displayName?.Trim() ?? string.Empty;
        if (display.Length == 0)
            display = state.captiveId;
        string revision = "captivity-performer-display-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(
                state.captiveId + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(
                "captivity-performer-pronunciation-v1:" + state.captiveId),
            "ko-KR");
    }

    private void NotifyObservers(
        CaptiveState state,
        int threshold,
        string message)
    {
        try
        {
            port.Publish(new CaptivePerformerMilestoneEvent(
                state.captiveId,
                threshold,
                message));
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "공연자 이정표 이벤트 관찰자가 실패했습니다: "
                + exception);
        }
        try
        {
            port.RaiseAlert(
                $"공연자 명성 {threshold}",
                message,
                threshold >= 100
                    ? CaptivityMilestoneImportance.High
                    : CaptivityMilestoneImportance.Medium,
                "포로·노역");
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "공연자 이정표 알림 관찰자가 실패했습니다: "
                + exception);
        }
    }
}
