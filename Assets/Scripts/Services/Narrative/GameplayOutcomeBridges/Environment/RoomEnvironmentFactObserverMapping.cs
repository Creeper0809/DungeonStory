using System;

/// <summary>
/// Narrative-side projection of room domain facts. The producer owns only the
/// captured transition; this mapping owns ledger identity, subjects, metrics,
/// importance inputs, status and presentation facts through the receipt factory.
/// </summary>
public static class RoomEnvironmentFactObserverMapping
{
    public static RoomEnvironmentExperienceOutcomeReceipt CaptureExperience(
        RoomEnvironmentExperienceFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        return EnvironmentOutcomeReceiptFactory.CreateRoomExperience(
            fact.Sequence,
            fact.ActorId,
            fact.ActorDisplayName,
            fact.FacilityId,
            fact.FacilityDisplayName,
            fact.RoomId,
            fact.RoomDisplayName,
            fact.Activity,
            fact.WorkTypeId,
            fact.ImpressionMood,
            fact.CleanlinessMood,
            fact.DurationSeconds,
            fact.Location,
            fact.AbsoluteDay);
    }

    public static RoomConditionOutcomeReceipt CaptureCondition(
        RoomEnvironmentConditionFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        return EnvironmentOutcomeReceiptFactory.CreateRoomCondition(
            fact.Sequence,
            fact.ObserverId,
            fact.ObserverDisplayName,
            fact.FacilityId,
            fact.FacilityDisplayName,
            fact.RoomId,
            fact.RoomDisplayName,
            fact.PreviousCleanliness / 100f,
            fact.CurrentCleanliness / 100f,
            fact.Location,
            fact.AbsoluteDay);
    }
}
