using System;
using System.Globalization;

public static class CharacterHealingFactMapping
{
    public static MigratedDomainFactCapture CaptureCritical(
        CharacterCriticalHealthTransitionFact transition)
    {
        CharacterHealingFact fact = transition.Value
            ?? throw new ArgumentNullException(nameof(transition));
        CharacterHealingFactData data = fact.Data;
        string detail = "beforeRatio=" + fact.RatioBefore.ToString(
                "0.###",
                CultureInfo.InvariantCulture)
            + ";afterRatio=" + fact.RatioAfter.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
        return Capture(
            fact,
            data,
            MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
            $"{data.characterName}의 건강이 임계치를 회복했다.",
            detail);
    }

    public static MigratedDomainFactCapture CaptureRecovered(
        CharacterMobilityRecoveredFact transition)
    {
        CharacterHealingFact fact = transition.Value
            ?? throw new ArgumentNullException(nameof(transition));
        CharacterHealingFactData data = fact.Data;
        string detail = $"beforeDowned={data.downedBefore};afterDowned={data.downedAfter}";
        return Capture(
            fact,
            data,
            MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent,
            $"{data.characterName}이(가) 전투 불능에서 회복했다.",
            detail);
    }

    private static MigratedDomainFactCapture Capture(
        CharacterHealingFact fact,
        CharacterHealingFactData data,
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
