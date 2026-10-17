using System;
using System.Linq;

internal static class RunVariableFactMapping
{
    internal static MigratedDomainFactCapture Capture(RunVariableFact fact)
    {
        RunVariableFactData data = fact.Snapshot();
        data.Validate();

        if (data.kind == RunVariableFactKind.Expired
            && data.entries.Count > 16)
        {
            throw new InvalidOperationException(
                "Run-variable expiration exceeded the fixed gameplay-outcome batch capacity.");
        }

        MigratedProducerOutcomeSubject[] subjects = data.entries
            .Select(entry => new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.OperationKind,
                VariableIdentity(data.runSeed, entry.instanceSequence),
                entry.title,
                MigratedProducerOutcomeIds.OperationRole))
            .ToArray();

        MigratedProducerOutcomeKind kind;
        string ownerIdentity;
        string summary;
        switch (data.kind)
        {
            case RunVariableFactKind.Activated:
                kind = MigratedProducerOutcomeKind.RunVariableActivatedEvent;
                ownerIdentity = VariableIdentity(
                    data.runSeed,
                    data.entries[0].instanceSequence);
                summary = data.entries[0].title + " 변수가 발현됐다.";
                break;
            case RunVariableFactKind.Expired:
                kind = MigratedProducerOutcomeKind.RunVariableExpiredEvent;
                ownerIdentity = "run:" + data.runSeed
                    + ":operation-expiry-day:" + data.absoluteDay;
                summary = data.entries.Count + "개의 런 변수가 종료됐다.";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(data.kind),
                    data.kind,
                    "Unknown run-variable fact kind.");
        }

        MigratedProducerOutcomeDetails details = new(
            subjects,
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            summary);
        return new MigratedDomainFactCapture(
            kind,
            ownerIdentity,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    private static string VariableIdentity(int runSeed, long sequence) =>
        "run-variable:" + runSeed + ":" + sequence;
}
