using System;
using DungeonStory.Narrative.Korean;

public static class CaptivityTransitionFactMapping
{
    public static CaptivityRehabilitationOutcomeReceipt Capture(CaptivityRehabilitationProgressedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        var value = fact.Snapshot();
        return new CaptivityRehabilitationOutcomeReceipt(fact.CaptiveId,
            CaptivityTransitionOutcomeNames.Create(fact.CaptiveId, value.captiveDisplayName, "captivity-rehabilitation-captive"),
            value.wardenId, CaptivityTransitionOutcomeNames.Create(value.wardenId, value.wardenDisplayName, "captivity-rehabilitation-warden"),
            value.facilityId, CaptivityTransitionOutcomeNames.Create(value.facilityId, value.facilityDisplayName, "captivity-rehabilitation-facility"),
            value.absoluteDay, value.completedDay, value.approvedWork, value.workBefore, value.workAfter,
            value.rehabilitationDaysBefore, value.rehabilitationDaysAfter, value.trustBefore, value.trustAfter,
            value.grudgeBefore, value.grudgeAfter, value.corruptionBefore, value.corruptionAfter, value.foodConsumed, fact.OwnerRevision);
    }

    public static CaptivityReleaseOutcomeReceipt Capture(CaptivityReleasedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        var value = fact.Snapshot();
        KoreanNameSnapshot facilityName = value.facilityId.Length == 0 ? default
            : CaptivityTransitionOutcomeNames.Create(value.facilityId, value.facilityDisplayName, "captivity-release-facility");
        return new CaptivityReleaseOutcomeReceipt(fact.CaptiveId,
            CaptivityTransitionOutcomeNames.Create(fact.CaptiveId, value.captiveDisplayName, "captivity-release-captive"),
            value.deciderId, CaptivityTransitionOutcomeNames.Create(value.deciderId, value.deciderDisplayName, "captivity-release-decider"),
            value.facilityId, facilityName, value.absoluteDay, value.statusBefore, value.inCustodyBefore, value.restrainedBefore,
            value.actorTypeBefore, value.lifecycleBefore, value.aiPausedBefore, value.doorCaptiveBefore);
    }
}
