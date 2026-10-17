using System;
using DungeonStory.Foundation;

public static class CharacterWaterFactObservation
{
    public static MigratedDomainFactCapture CaptureWater(
        CharacterWaterConsumedFact fact)
    {
        CharacterWaterFactData data = RequireActual(fact);
        CharacterWaterOutcomeEvidence evidence = CreateEvidence(data);
        string summary =
            $"source={data.sourceId}; quality={data.quality}; consumed={data.consumed:0.###}; pathogen={data.pathogenDiseaseId}";

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.CharacterWaterConsumedEvent,
            "water-consumed:" + data.characterId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            CharacterSurvivalOutcomeDetails.Water(evidence, summary));
    }

    public static MigratedDomainFactCapture CaptureConsequence(
        CharacterWaterConsumedFact fact,
        string rootOperationId)
    {
        CharacterWaterFactData data = RequireActual(fact);
        CharacterWaterOutcomeEvidence evidence = CreateEvidence(data);

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.CharacterFoulWaterConsequenceResult,
            "foul-water-consequence:" + data.characterId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            CharacterSurvivalOutcomeDetails.FoulWaterConsequence(
                evidence,
                rootOperationId));
    }

    private static CharacterWaterFactData RequireActual(
        CharacterWaterConsumedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        if (fact.IsProposal)
            throw new InvalidOperationException(
                "water-consumption-proposal-cannot-be-observed");
        return fact.Data;
    }

    private static CharacterWaterOutcomeEvidence CreateEvidence(
        CharacterWaterFactData data) => new(
        new CharacterId(data.characterId),
        data.characterName,
        data.sourceId,
        data.sourceKind,
        data.quality,
        data.consumed,
        data.pathogenDiseaseId,
        data.thirstBefore,
        data.thirstAfter,
        data.healthBefore,
        data.healthAfter,
        data.hygieneBefore,
        data.hygieneAfter,
        data.infectionBefore,
        data.infectionAfter,
        data.moodBefore,
        data.moodAfter);
}
