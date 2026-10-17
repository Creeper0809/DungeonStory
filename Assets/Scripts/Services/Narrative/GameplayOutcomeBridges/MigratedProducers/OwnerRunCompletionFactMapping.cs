using System;
using System.Globalization;

public static class OwnerRunCompletionFactMapping
{
    public const string ObserverId = "log:owner.run-completion";

    public static MigratedDomainFactCapture Capture(
        OwnerRunCompletionFact fact)
    {
        OwnerRunCompletionFactData data = fact.Snapshot();
        data.Validate();

        string seed = data.runSeed.ToString(CultureInfo.InvariantCulture);
        string ownerIdentity = "owner-run-ended:"
            + seed + ":" + data.ownerId;
        string summary = string.Join("|", new[]
        {
            "owner-run-ended@1",
            seed,
            data.ownerId,
            ((int)data.outcome).ToString(CultureInfo.InvariantCulture),
            data.absoluteDay.ToString(CultureInfo.InvariantCulture),
            data.reason
        });
        MigratedProducerOutcomeDetails details = new(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    data.ownerId,
                    data.displayName,
                    MigratedProducerOutcomeIds.ActorRole)
            },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            summary);

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.OwnerRunEndedEvent,
            ownerIdentity,
            data.absoluteDay,
            data.outcome == DungeonRunOutcome.Victory
                ? GameplayOutcomeStatus.Succeeded
                : GameplayOutcomeStatus.Failed,
            details);
    }
}
