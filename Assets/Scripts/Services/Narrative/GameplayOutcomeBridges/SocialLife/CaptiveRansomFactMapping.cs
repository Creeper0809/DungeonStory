using System;
using DungeonStory.Narrative.Korean;

public static class CaptiveRansomFactMapping
{
    public static CaptiveRansomOutcomeReceipt Capture(CaptiveRansomAcceptedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        var value = fact.Snapshot();
        var name = new KoreanNameSnapshot(value.displayName,
            "captivity-ransom-display-v1:" + NarrativeInferenceHash.ComputeSha256Utf8(value.captiveId + "|" + value.displayName),
            KoreanPronunciationHint.AutoHangulDisplay("captivity-ransom-pronunciation-v1:" + value.captiveId), "ko-KR");
        return new CaptiveRansomOutcomeReceipt(value.captiveId, name, value.decisionMakerId, value.previousStatus,
            value.amount, value.retaliationPressure, value.revision, value.absoluteDay);
    }
}
