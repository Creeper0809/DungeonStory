using System;
using DungeonStory.Narrative.Korean;

public static class CaptiveEscapeFactMapping
{
    public static CaptiveEscapeOutcomeReceipt Capture(CaptiveEscapedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        var value = fact.Snapshot();
        var name = new KoreanNameSnapshot(value.displayName,
            "captivity-escape-display-v1:" + NarrativeInferenceHash.ComputeSha256Utf8(value.captiveId + "|" + value.displayName),
            KoreanPronunciationHint.AutoHangulDisplay("captivity-escape-pronunciation-v1:" + value.captiveId), "ko-KR");
        return new CaptiveEscapeOutcomeReceipt(value.captiveId, name, value.kind, value.trigger,
            CaptiveEscapedFact.IsBetrayal(value.kind), value.previousStatus, value.retaliationPressure, value.revision, value.absoluteDay);
    }
}
