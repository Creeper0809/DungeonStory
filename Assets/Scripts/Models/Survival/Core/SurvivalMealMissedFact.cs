using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class SurvivalMealMissedData
{
    public string characterId = string.Empty;
    public int absoluteDay;
    public int completedDay;
    public int consecutiveMisses;
    public int cohortIndex;
    public int cohortSize;

    public SurvivalMealMissedData Clone() => (SurvivalMealMissedData)MemberwiseClone();
    public void Validate()
    {
        var character = new CharacterId(characterId);
        if (!character.IsValid || character.Value != characterId
            || absoluteDay < 2 || completedDay != absoluteDay - 1 || consecutiveMisses < 1
            || cohortSize is < 1 or > SurvivalMealMissedCohortSaveData.MaximumMembers
            || cohortIndex < 0 || cohortIndex >= cohortSize)
            throw new InvalidOperationException("survival-meal-missed-fact-invalid");
    }
}

public sealed class SurvivalMealMissedFact
{
    private readonly SurvivalMealMissedData frozen;
    public SurvivalMealMissedFact(SurvivalMealMissedData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate();
        frozen = source.Clone();
    }
    public SurvivalMealMissedData Snapshot() => frozen.Clone();
}

[Serializable]
public sealed class SurvivalMealMissedPublicationSaveData
{
    public SurvivalMealMissedData fact = new();
    public string publicationKey = string.Empty;
    public SurvivalMealMissedPublicationSaveData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey };
    public void Validate()
    {
        if (fact == null || !DomainFactValueSyntax.IsText(publicationKey, 1024, false))
            throw new InvalidOperationException("survival-meal-missed-publication-invalid");
        fact.Validate();
    }
}

[Serializable]
public sealed class SurvivalMealMissedCohortSaveData
{
    public const int MaximumMembers = 256;
    public const int MaximumCohorts = 16384;
    public int absoluteDay;
    public bool publicationConfirmed;
    public List<SurvivalMealMissedPublicationSaveData> members = new();
    public SurvivalMealMissedCohortSaveData Clone() => new()
    {
        absoluteDay = absoluteDay, publicationConfirmed = publicationConfirmed,
        members = members?.Select(value => value?.Clone()).ToList()
    };
    public void Validate()
    {
        if (absoluteDay < 2 || members == null || members.Count is < 1 or > MaximumMembers)
            throw new InvalidOperationException("survival-meal-missed-cohort-invalid");
        string previous = null;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < members.Count; index++)
        {
            var member = members[index] ?? throw new InvalidOperationException("survival-meal-missed-member-missing");
            member.Validate();
            if (member.fact.absoluteDay != absoluteDay || member.fact.cohortIndex != index
                || member.fact.cohortSize != members.Count || !keys.Add(member.publicationKey)
                || previous != null && string.CompareOrdinal(previous, member.fact.characterId) >= 0)
                throw new InvalidOperationException("survival-meal-missed-cohort-members-conflict");
            previous = member.fact.characterId;
        }
    }
}
