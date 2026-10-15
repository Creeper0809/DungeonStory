using System;
using System.Globalization;

public static class CharacterLifeTransitionFactMapping
{
    public static MigratedDomainFactCapture Capture(
        CharacterLifeTransitionFact fact)
    {
        CharacterLifeTransitionFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        return value.Kind switch
        {
            CharacterLifeTransitionKind.AgeCondition =>
                CaptureAgeCondition(value),
            CharacterLifeTransitionKind.LifeStage =>
                CaptureLifeStage(value),
            _ => throw new ArgumentOutOfRangeException(nameof(fact))
        };
    }

    public static MigratedDomainFactObserverBinding<
        CharacterLifeTransitionFact> CreateBinding(
        MigratedProducerOutcomeRuntime runtime,
        GameplayDomainFactCommitGroup commits) => new(
            "log:character.life-transition",
            Capture,
            runtime,
            commits);

    private static MigratedDomainFactCapture CaptureAgeCondition(
        CharacterLifeTransitionFact fact)
    {
        AgeConditionChange change = fact.AgeChange;
        return SingleSubject(
            MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent,
            string.Join("|", new[]
            {
                "character-age-condition-change@1",
                Invariant(fact.AbsoluteDay),
                change.CharacterId.Value,
                change.ConditionId,
                Invariant((int)change.Previous),
                Invariant((int)change.Current),
                change.NewlyDiagnosed ? "1" : "0",
                change.Resolved ? "1" : "0"
            }),
            fact.CharacterId,
            fact.AbsoluteDay,
            string.Join("|", new[]
            {
                "age-condition-change@1",
                change.CharacterId.Value,
                change.ConditionId,
                Invariant((int)change.Previous),
                Invariant((int)change.Current),
                change.NewlyDiagnosed ? "new" : "progressed",
                change.Resolved ? "resolved" : "active"
            }));
    }

    private static MigratedDomainFactCapture CaptureLifeStage(
        CharacterLifeTransitionFact fact)
    {
        CharacterLifeStageTransition transition = fact.StageChange;
        return SingleSubject(
            MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent,
            string.Join("|", new[]
            {
                "character-life-stage-change@1",
                Invariant(fact.AbsoluteDay),
                transition.CharacterId.Value,
                Invariant((int)transition.Previous),
                Invariant((int)transition.Current)
            }),
            fact.CharacterId,
            fact.AbsoluteDay,
            string.Join("|", new[]
            {
                "life-stage-change@1",
                transition.CharacterId.Value,
                Invariant((int)transition.Previous),
                Invariant((int)transition.Current)
            }));
    }

    private static MigratedDomainFactCapture SingleSubject(
        MigratedProducerOutcomeKind kind,
        string ownerIdentity,
        string characterId,
        int absoluteDay,
        string summary)
    {
        var subject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            characterId,
            characterId,
            MigratedProducerOutcomeIds.ActorRole);
        var details = new MigratedProducerOutcomeDetails(
            new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            summary);
        return new MigratedDomainFactCapture(
            kind,
            ownerIdentity,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    private static string Invariant(int value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
