using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

/// <summary>Frozen body changes and opaque parent observation; contains no logging schema.</summary>
public sealed class AgeConditionAnatomyAppliedFact
{
    private readonly AgeConditionAnatomyProjectionSaveData value;
    public AgeConditionAnatomyAppliedFact(AgeConditionAnatomyProjectionSaveData source)
    {
        Validate(source);
        value = CharacterLifeAggregateState.CloneAgeConditionAnatomyProjection(source);
    }
    public AgeConditionAnatomyProjectionSaveData Snapshot() => CharacterLifeAggregateState.CloneAgeConditionAnatomyProjection(value);
    public static CharacterLifeTransitionFact Parent(AgeConditionAnatomyProjectionSaveData source) =>
        CharacterLifeTransitionFact.FromAgeCondition(new AgeConditionChange(new CharacterId(source.characterId),
            source.conditionId, (AgeConditionSeverity)source.previousSeverity, (AgeConditionSeverity)source.currentSeverity,
            source.newlyDiagnosed, source.resolved), source.absoluteDay);
    public static void Validate(AgeConditionAnatomyProjectionSaveData source)
    {
        if (source == null || source.phase != (int)AgeConditionAnatomyProjectionPhase.AnatomyApplied
            || !DomainFactValueSyntax.IsText(source.characterDisplayName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || !DomainFactValueSyntax.IsText(source.parentPublicationId, 1024, false)
            || !DomainFactValueSyntax.IsText(source.parentPublicationKey, 1024, false)
            || source.resolved || source.operationId != Parent(source).AnatomyOperationId
            || source.nodeDeltas == null || source.nodeDeltas.Count == 0
            || source.nodeDeltas.Select(row => row?.nodeId).Distinct(StringComparer.Ordinal).Count() != source.nodeDeltas.Count
            || !Nonnegative(source.aggregateMaximumHealth) || source.aggregateMaximumHealth <= 0
            || !Nonnegative(source.aggregateHealthBefore) || !Nonnegative(source.aggregateHealthAfter)
            || source.aggregateHealthAfter > source.aggregateHealthBefore || source.aggregateHealthBefore > source.aggregateMaximumHealth
            || source.nodeDeltas.Any(row => row == null || !DomainFactValueSyntax.IsIdentity(row.nodeId)
                || !Nonnegative(row.maximumHealth) || row.maximumHealth <= 0 || !Nonnegative(row.healthBefore)
                || !Nonnegative(row.healthAfter) || row.healthAfter > row.healthBefore || row.healthBefore > row.maximumHealth
                || !Nonnegative(row.requestedDamage) || row.requestedDamage <= 0
                || !Nonnegative(row.bleedingBefore) || !Nonnegative(row.bleedingAfter)))
            throw new InvalidOperationException("age-anatomy-applied-fact-invalid");
    }
    private static bool Nonnegative(float number) => !float.IsNaN(number) && !float.IsInfinity(number) && number >= 0;
}
