public static class OffenseTruthRevealFactMapping
{
    public static OffenseTruthRevealOutcomeReceipt Capture(OffenseTruthRevealedFact fact)
    {
        var value = fact.Snapshot();
        return new OffenseTruthRevealOutcomeReceipt(value.targetId, value.ownerRevision,
            value.targetDisplayName, value.truthTitle, value.truthText, value.absoluteDay);
    }
}
