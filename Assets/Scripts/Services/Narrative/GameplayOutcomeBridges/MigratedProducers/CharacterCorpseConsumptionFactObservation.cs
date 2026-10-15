using System;

public static class CharacterCorpseConsumptionFactObservation
{
    public static MigratedDomainFactCapture Capture(
        CharacterCorpseConsumedFact fact,
        GameplayResultKey physicalKey,
        GameplayOutcomeId physicalId)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        PhysicalItemDisposedFact physical = fact.Physical;
        GameplayResultKey expectedPhysicalKey =
            PhysicalItemDispositionFactObservation.Capture(physical).ResultKey;
        if (!physicalKey.IsValid || physicalKey != expectedPhysicalKey)
            throw new ArgumentException(
                "corpse-consumption-physical-key-mismatch",
                nameof(physicalKey));
        if (!physicalId.IsValid)
            throw new ArgumentException(
                "corpse-consumption-physical-outcome-id-invalid",
                nameof(physicalId));

        PhysicalItemBatchDispositionReceipt receipt = physical.Receipt;
        PhysicalItemDispositionSourceFact source = receipt.SourceFacts[0];
        CharacterCorpseConsumptionExperienceData experience = fact.Experience;
        var actorEntity = new GameplayEntityId(
            MigratedProducerOutcomeIds.CharacterKind,
            experience.characterId);
        MigratedProducerOutcomeDetails details = new(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    experience.characterId,
                    experience.characterName,
                    MigratedProducerOutcomeIds.ActorRole),
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    experience.victimId,
                    experience.victimName,
                    MigratedProducerOutcomeIds.TargetRole),
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.ItemStackKind,
                    source.StackId,
                    source.StackId,
                    MigratedProducerOutcomeIds.SourceRole),
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.ItemDefinitionKind,
                    source.ItemDefinitionId,
                    source.ItemDefinitionId,
                    MigratedProducerOutcomeIds.ItemRole)
            },
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.QuantityMetric,
                    receipt.Quantity,
                    MigratedProducerOutcomeIds.CountUnit,
                    actorEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.InputMassMetric,
                    receipt.InputMassGrams,
                    MigratedProducerOutcomeIds.GramUnit,
                    actorEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.HungerBeforeMetric,
                    experience.hungerBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actorEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.HungerAfterMetric,
                    experience.hungerAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actorEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.HygieneBeforeMetric,
                    experience.hygieneBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actorEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.HygieneAfterMetric,
                    experience.hygieneAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actorEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.InfectionBeforeMetric,
                    experience.infectionBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actorEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.InfectionAfterMetric,
                    experience.infectionAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actorEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.MoodBeforeMetric,
                    experience.moodBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    actorEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.MoodAfterMetric,
                    experience.moodAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    actorEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.PreviousMetric,
                    experience.memoriesBefore,
                    MigratedProducerOutcomeIds.CountUnit,
                    actorEntity),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CurrentMetric,
                    experience.memoriesAfter,
                    MigratedProducerOutcomeIds.CountUnit,
                    actorEntity)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.ReasonFact,
                    "extreme-hunger"),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.CharacterFact,
                    experience.victimId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.SourceStackFact,
                    source.StackId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.ItemDefinitionFact,
                    source.ItemDefinitionId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.PhysicalCommitFact,
                    receipt.CommitId),
                new GameplayOutcomeFact(
                    new GameplayOutcomeFactId("physical-result-key"),
                    physicalKey.ToString()),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.OperationFact,
                    receipt.OperationId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.DetailFact,
                    "action=corpse-consumption;sameSpecies="
                    + experience.sameSpecies
                    + ";deathReason="
                    + experience.deathReason)
            },
            experience.memory,
            new GameplayOutcomeCausation(
                physicalId,
                new GameplayOperationId(receipt.OperationId),
                "corpse-eat-from-physical-sink"));

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.CharacterTabooIncidentEvent,
            "taboo-corpse-eat:" + receipt.OperationId,
            experience.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }
}
