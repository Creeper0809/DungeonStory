using System;

public static class ShopliftingFactMapping
{
    public static MigratedDomainFactCapture Capture(ShopliftingFact fact)
    {
        ShopliftingFactData data = fact.Snapshot();
        data.Validate();

        string ownerIdentity = "facility-crime:shoplifting:"
            + data.unitOperationId
            + ":facility=" + data.facilityId
            + ":actor=" + data.actorId;
        string summary = "시설 절도 확정: operation=shoplifting-commit:"
            + data.unitOperationId
            + "; actor=" + data.actorId
            + "; item=" + data.lot.itemDefinitionId
            + "; loss=" + data.lossValue
            + "; day=" + data.absoluteDay;
        MigratedProducerOutcomeDetails details = new(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.FacilityKind,
                    data.facilityId,
                    data.facilityName,
                    MigratedProducerOutcomeIds.FacilityRole)
            },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            summary);

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.FacilityCrimeEvent,
            ownerIdentity,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }
}
