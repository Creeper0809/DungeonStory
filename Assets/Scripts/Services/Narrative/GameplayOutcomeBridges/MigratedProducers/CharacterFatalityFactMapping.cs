using System;
using System.Globalization;

public static class CharacterFatalityFactMapping
{
    public static MigratedDomainFactCapture CaptureDeath(
        CharacterFatalityFact fact)
    {
        CharacterFatalityFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        CharacterFatalityFactData data = value.Data;
        return Capture(
            value,
            data,
            MigratedProducerOutcomeKind.CharacterDeathEvent,
            $"{data.characterName}이(가) 사망했다.",
            $"cause={data.cause};reason={data.reasonCode}");
    }

    public static MigratedDomainFactCapture CaptureCritical(
        CharacterFatalCriticalTransitionFact transition)
    {
        CharacterFatalityFact fact = transition.Value
            ?? throw new ArgumentNullException(nameof(transition));
        CharacterFatalityFactData data = fact.Data;
        string detail = "beforeRatio=" + fact.RatioBefore.ToString(
                "0.###",
                CultureInfo.InvariantCulture)
            + ";afterRatio=0";
        return Capture(
            fact,
            data,
            MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
            $"{data.characterName}의 건강이 치명 임계치를 넘었다.",
            detail);
    }

    public static MigratedDomainFactCapture CaptureDowned(
        CharacterFatalDownedFact transition)
    {
        CharacterFatalityFact fact = transition.Value
            ?? throw new ArgumentNullException(nameof(transition));
        CharacterFatalityFactData data = fact.Data;
        return Capture(
            fact,
            data,
            MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
            $"{data.characterName}이(가) 전투 불능이 됐다.",
            $"beforeDowned={data.downedBefore};afterDowned={data.downedAfter}");
    }

    private static MigratedDomainFactCapture Capture(
        CharacterFatalityFact fact,
        CharacterFatalityFactData data,
        MigratedProducerOutcomeKind kind,
        string summary,
        string detail)
    {
        var subject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            data.characterId,
            data.characterName,
            MigratedProducerOutcomeIds.TargetRole);
        var details = new MigratedProducerOutcomeDetails(
            new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(),
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.DetailFact,
                    detail)
            },
            summary,
            summaryFirst: true);
        return new MigratedDomainFactCapture(
            kind,
            data.characterId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details,
            reservationOnly: fact.IsProposal);
    }
}
