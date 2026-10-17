using System;
using DungeonStory.Narrative.Korean;

/// <summary>Only observation interprets native identity and localization metadata.</summary>
public static class CaptivePerformerMilestoneFactMapping
{
    public static CaptivePerformerMilestoneOutcomeReceipt Capture(CaptivePerformerMilestoneUnlockedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        var value = fact.Snapshot();
        var name = new KoreanNameSnapshot(value.displayName,
            "captivity-performer-display-v1:" + NarrativeInferenceHash.ComputeSha256Utf8(value.captiveId + "|" + value.displayName),
            KoreanPronunciationHint.AutoHangulDisplay("captivity-performer-pronunciation-v1:" + value.captiveId), "ko-KR");
        return new CaptivePerformerMilestoneOutcomeReceipt(value.captiveId, name,
            value.threshold, value.fame, value.revision, value.absoluteDay);
    }
}
