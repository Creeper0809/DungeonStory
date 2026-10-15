using System;

internal static class ObservedServiceIncidentFactMapping
{
    internal static MigratedDomainFactCapture Capture(
        ObservedServiceIncidentFact fact)
    {
        ObservedMealIncidentEvidenceSaveData data = fact.Snapshot();
        MigratedProducerOutcomeDetails details = new(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    data.targetCharacterId,
                    data.targetCharacterId,
                    MigratedProducerOutcomeIds.ActorRole)
            },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            BuildSummary(data));
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.ObservedMealIncidentCaptureResult,
            data.operationId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    private static string BuildSummary(
        ObservedMealIncidentEvidenceSaveData data) =>
        "서비스 사건 목격: source=" + data.sourceKind
        + "; incident=" + data.incidentKind
        + "; operation=" + data.operationId
        + "; occurrence=" + data.occurrenceInstanceId
        + "; target=" + data.targetCharacterId
        + "; facility=" + data.facilityInstanceId
        + "; location=(" + data.locationX + "," + data.locationY + ")"
        + "; day=" + data.absoluteDay;
}
