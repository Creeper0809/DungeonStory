using System;

internal static class CareerMentorshipAwardFactMapping
{
    internal static MigratedDomainFactCapture Capture(
        CareerMentorshipAwardFact fact)
    {
        CareerMentorshipAwardFactData data = fact.Snapshot();
        MigratedProducerOutcomeDetails details = new(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    data.studentId,
                    data.studentId,
                    MigratedProducerOutcomeIds.ActorRole)
            },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            BuildSummary(data));
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.CareerMentorshipAwardCommitReceipt,
            data.operationId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    private static string BuildSummary(
        CareerMentorshipAwardFactData data) =>
        "멘토링 보상 확정: mentor=" + data.mentorId
        + "; student=" + data.studentId
        + "; academy=" + data.academyId
        + "; proficiency=" + data.proficiencyId
        + "; ledger=" + data.ledgerStackId
        + "; revision=" + data.beforeContentRevision
        + "->" + data.afterContentRevision
        + "; day=" + data.absoluteDay;
}
