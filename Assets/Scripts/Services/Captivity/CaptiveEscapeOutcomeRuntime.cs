using System;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

internal sealed class CaptiveEscapeOutcomeRuntime
{
    private readonly ICaptiveEscapeOutcomeCommitter outcomes;
    private readonly IGameEventBus eventBus;

    public CaptiveEscapeOutcomeRuntime(
        ICaptiveEscapeOutcomeCommitter outcomes,
        IGameEventBus eventBus)
    {
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public bool TryPrepare(
        CaptiveState state,
        CaptiveEscapeOutcomeKind kind,
        string trigger,
        CaptivityStatus previousStatus,
        float resultingRetaliationPressure,
        int absoluteDay,
        out PreparedCaptiveEscapeOutcome prepared,
        out long ownerRevision,
        out string normalizedTrigger,
        out string failureReason)
    {
        prepared = default;
        ownerRevision = 0L;
        normalizedTrigger = trigger?.Trim() ?? string.Empty;
        failureReason = string.Empty;
        if (state == null)
        {
            failureReason = "탈출 결과를 기록할 포로 상태가 없습니다.";
            return false;
        }
        if (state.escapeOutcomeRevision > 0L)
        {
            failureReason = "포로 탈출 결과가 이미 확정되었습니다.";
            return false;
        }
        try
        {
            ownerRevision = checked(state.escapeOutcomeRevision + 1L);
        }
        catch (OverflowException)
        {
            failureReason = "포로 탈출 원장 리비전이 범위를 초과했습니다.";
            return false;
        }

        CaptiveEscapeOutcomeReceipt receipt;
        try
        {
            receipt = new CaptiveEscapeOutcomeReceipt(
                state.captiveId,
                CaptureName(state),
                kind,
                normalizedTrigger,
                CaptiveEscapeOutcomeIds.IsBetrayal(kind),
                previousStatus,
                resultingRetaliationPressure,
                ownerRevision,
                absoluteDay);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            failureReason = "포로 탈출 영수증을 만들 수 없습니다: "
                + exception.Message;
            return false;
        }

        return outcomes.TryPrepare(receipt, out prepared, out failureReason);
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedCaptiveEscapeOutcome prepared) => outcomes.Commit(prepared);

    public void Cancel(in PreparedCaptiveEscapeOutcome prepared) =>
        outcomes.Cancel(prepared);

    public void NotifyCommitted(
        CaptiveState state,
        string trigger,
        bool betrayal)
    {
        try
        {
            eventBus.Publish(new CaptiveEscapedEvent(
                state?.captiveId ?? string.Empty,
                trigger,
                betrayal));
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "포로 탈출 이벤트 관찰자가 실패했습니다: " + exception);
        }
    }

    private static KoreanNameSnapshot CaptureName(CaptiveState state)
    {
        string display = state.displayName?.Trim() ?? string.Empty;
        if (display.Length == 0)
            display = state.captiveId;
        return new KoreanNameSnapshot(
            display,
            "captivity-escape-display-v1:"
                + NarrativeInferenceHash.ComputeSha256Utf8(
                    state.captiveId + "|" + display),
            KoreanPronunciationHint.AutoHangulDisplay(
                "captivity-escape-pronunciation-v1:" + state.captiveId),
            "ko-KR");
    }
}
