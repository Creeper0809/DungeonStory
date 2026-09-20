using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

internal sealed class CaptiveRansomOutcomeRuntime
{
    private readonly ICaptiveRansomOutcomeCommitter outcomes;
    private readonly IGameEventBus eventBus;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameClock gameClock;

    public CaptiveRansomOutcomeRuntime(
        ICaptiveRansomOutcomeCommitter outcomes,
        IGameEventBus eventBus,
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock)
    {
        this.outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public bool TryPrepare(
        CaptiveState state,
        CaptivityStatus previousStatus,
        int amount,
        float resultingRetaliationPressure,
        int absoluteDay,
        out PreparedCaptiveRansomOutcome prepared,
        out long ownerRevision,
        out string failureReason)
    {
        prepared = default;
        ownerRevision = 0L;
        failureReason = string.Empty;
        if (state == null)
        {
            failureReason = "몸값 결과를 기록할 포로 상태가 없습니다.";
            return false;
        }
        if (state.ransomOutcomeRevision > 0L)
        {
            failureReason = "포로 몸값 결과가 이미 확정되었습니다.";
            return false;
        }
        try
        {
            ownerRevision = checked(state.ransomOutcomeRevision + 1L);
        }
        catch (OverflowException)
        {
            failureReason = "포로 몸값 원장 리비전이 범위를 초과했습니다.";
            return false;
        }

        CaptiveRansomOutcomeReceipt receipt;
        try
        {
            receipt = new CaptiveRansomOutcomeReceipt(
                state.captiveId,
                CaptureName(state),
                previousStatus,
                amount,
                resultingRetaliationPressure,
                ownerRevision,
                absoluteDay);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            failureReason = "포로 몸값 영수증을 만들 수 없습니다: "
                + exception.Message;
            return false;
        }

        return outcomes.TryPrepare(receipt, out prepared, out failureReason);
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedCaptiveRansomOutcome prepared) => outcomes.Commit(prepared);

    public void Cancel(in PreparedCaptiveRansomOutcome prepared) =>
        outcomes.Cancel(prepared);

    public void NotifyCommitted(
        CaptiveState state,
        CharacterActor actor,
        int amount)
    {
        try
        {
            CharacterActor decider = worldRegistry.Characters
                .FirstOrDefault(value => value?.Identity?.IsOwner == true);
            if (CharacterPersistentIdentity.TryGet(decider, out CharacterId deciderId)
                && CharacterPersistentIdentity.TryGet(actor, out CharacterId prisonerId))
            {
                eventBus.Publish(new PrisonerDecisionEvent(
                    deciderId,
                    prisonerId,
                    "ransom",
                    CharacterCommandOrigin.DirectPlayerOrder,
                    CurrentAbsoluteDay));
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "확정된 몸값의 포로 결정 관찰자가 실패했습니다: " + exception);
        }

        try
        {
            eventBus.Publish(new CaptiveRansomedEvent(
                state?.captiveId ?? string.Empty,
                amount,
                state?.retaliationPressure ?? 0f));
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "확정된 몸값의 보복 관찰자가 실패했습니다: " + exception);
        }
    }

    private int CurrentAbsoluteDay => Mathf.Max(
        0,
        Mathf.FloorToInt(gameClock.Time / GameCalendarRules.SecondsPerDay));

    private static KoreanNameSnapshot CaptureName(CaptiveState state)
    {
        string display = state.displayName?.Trim() ?? string.Empty;
        if (display.Length == 0)
            display = state.captiveId;
        return new KoreanNameSnapshot(
            display,
            "captivity-ransom-display-v1:"
                + NarrativeInferenceHash.ComputeSha256Utf8(
                    state.captiveId + "|" + display),
            KoreanPronunciationHint.AutoHangulDisplay(
                "captivity-ransom-pronunciation-v1:" + state.captiveId),
            "ko-KR");
    }
}
