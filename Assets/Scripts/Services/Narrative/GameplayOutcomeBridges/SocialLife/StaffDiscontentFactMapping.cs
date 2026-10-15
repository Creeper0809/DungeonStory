using DungeonStory.Narrative.Korean;

public static class StaffDiscontentFactMapping
{
    public static StaffDiscontentTransitionOutcomeReceipt Capture(StaffDiscontentTransitionedFact fact)
    {
        var value = fact.Snapshot();
        return new StaffDiscontentTransitionOutcomeReceipt(value.revision, value.absoluteDay, value.transition,
            StaffDiscontentFactValues.Restore(value.before), StaffDiscontentFactValues.Restore(value.after));
    }
    public static StaffDiscontentOutcomeReceipt Capture(StaffRebellionResponseResolvedFact fact)
    {
        var value = fact.Snapshot();
        var responder = value.responderId.Length == 0 ? default : new CharacterId(value.responderId);
        KoreanNameSnapshot name = responder.IsValid
            ? StaffDiscontentOutcomeReceipt.CaptureName(value.responderId, value.responderDisplayName) : default;
        return new StaffDiscontentOutcomeReceipt(value.revision, value.absoluteDay, value.responseType,
            StaffDiscontentFactValues.Restore(value.before), StaffDiscontentFactValues.Restore(value.after), responder, name,
            value.moodApplied, value.resolvedMoodDelta);
    }
}
