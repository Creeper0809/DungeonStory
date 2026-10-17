using System;

public static class CaptivityInteractionFactMapping
{
    public static CaptivityInteractionOutcomeReceipt Capture(CaptivityInteractionResolvedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        var value = fact.Snapshot();
        return new CaptivityInteractionOutcomeReceipt(fact.CaptiveId,
            CaptivityInteractionOutcomeNames.Snapshot("captive", fact.CaptiveId, value.captiveDisplayName),
            value.wardenId, CaptivityInteractionOutcomeNames.Snapshot("warden", value.wardenId, value.wardenDisplayName),
            value.facilityId, CaptivityInteractionOutcomeNames.Snapshot("facility", value.facilityId, value.facilityDisplayName),
            value.resultGridX, value.resultGridY, value.interactionId, value.interactionKind,
            value.interactionDisplayName, value.success, value.message,
            value.willAfter - value.willBefore, value.fearAfter - value.fearBefore,
            value.trustAfter - value.trustBefore, value.grudgeAfter - value.grudgeBefore,
            value.corruptionAfter - value.corruptionBefore, value.bodyDamageAmount,
            value.bodyHealthBefore, value.bodyHealthAfter, value.outputItemId, value.outputAmount,
            value.attemptId, fact.OwnerRevision, value.absoluteDay, fact.ComputeSemanticDigest());
    }
}
