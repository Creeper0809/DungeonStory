using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class CharacterLifeTransitionPublicationSaveData
{
    public CharacterLifeTransitionSaveData fact = new();
    public string publicationKey = string.Empty;
}

/// <summary>The already applied daily cohort awaiting observation, not a second aging authority.</summary>
[Serializable]
public sealed class CharacterLifeDailyObservationSaveData
{
    public int absoluteDay;
    public List<CharacterLifeTransitionPublicationSaveData> entries = new();
}

public static class CharacterLifeObservationState
{
    public static CharacterLifeDailyObservationSaveData Clone(CharacterLifeDailyObservationSaveData value)
    {
        Validate(value);
        return new CharacterLifeDailyObservationSaveData { absoluteDay = value.absoluteDay,
            entries = value.entries.Select(row => new CharacterLifeTransitionPublicationSaveData
            { fact = new CharacterLifeTransitionFact(row.fact).Snapshot(), publicationKey = row.publicationKey }).ToList() };
    }

    public static void Validate(CharacterLifeDailyObservationSaveData value)
    {
        if (value == null || value.absoluteDay < 0 || value.entries == null
            || (value.absoluteDay == 0) != (value.entries.Count == 0))
            throw new InvalidOperationException("life-daily-observation-shape-invalid");
        var domains = new HashSet<string>(StringComparer.Ordinal);
        var publications = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in value.entries)
        {
            if (row == null || !DomainFactValueSyntax.IsText(row.publicationKey, 1024, false))
                throw new InvalidOperationException("life-daily-observation-key-invalid");
            var fact = new CharacterLifeTransitionFact(row.fact);
            if (fact.AbsoluteDay != value.absoluteDay || !domains.Add(fact.DomainId) || !publications.Add(row.publicationKey))
                throw new InvalidOperationException("life-daily-observation-cohort-invalid");
        }
    }

    public static void RequireDelivery(in DomainFactDelivery delivered, string expectedKey)
    {
        if (!delivered.Completed || delivered.OperationKey != expectedKey
            || !DomainFactValueSyntax.IsText(delivered.PublicationId, 1024, false)
            || delivered.Fingerprint?.Length != 64
            || !delivered.Fingerprint.All(value => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
            throw new InvalidOperationException("life-observation-delivery-pending:" + delivered.Detail);
    }

    public static AgeConditionAnatomyProjectionSaveData Projection(CharacterLifeTransitionFact fact,
        in DomainFactDelivery parent)
    {
        RequireDelivery(parent, parent.OperationKey);
        if (!DomainFactValueSyntax.IsText(parent.OperationKey, 1024, false)
            || fact.Kind != CharacterLifeTransitionKind.AgeCondition || fact.AgeChange.Resolved)
            throw new InvalidOperationException("life-anatomy-parent-invalid");
        var change = fact.AgeChange;
        return new AgeConditionAnatomyProjectionSaveData
        {
            phase = (int)AgeConditionAnatomyProjectionPhase.Pending, operationId = fact.AnatomyOperationId,
            parentPublicationId = parent.PublicationId, parentPublicationKey = parent.OperationKey,
            absoluteDay = fact.AbsoluteDay, characterId = fact.CharacterId, conditionId = change.ConditionId,
            previousSeverity = (int)change.Previous, currentSeverity = (int)change.Current,
            newlyDiagnosed = change.NewlyDiagnosed, resolved = change.Resolved
        };
    }
}
