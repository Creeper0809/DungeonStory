using System;
using DungeonStory.Foundation.Facts;

/// <summary>Immutable water-consumption evidence with no logging or presentation authority.</summary>
public sealed class CharacterWaterConsumedFact
{
    private readonly CharacterWaterFactData data;

    public CharacterWaterConsumedFact(CharacterWaterFactData data, bool proposalOnly = false)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (!DomainFactValueSyntax.IsIdentity(data.characterId)
            || !DomainFactValueSyntax.IsText(data.characterName,
                DomainFactValueSyntax.MaximumNameCharacters, false)
            || !DomainFactValueSyntax.IsIdentity(data.sourceId)
            || (!string.IsNullOrEmpty(data.pathogenDiseaseId)
                && !DomainFactValueSyntax.IsIdentity(data.pathogenDiseaseId))
            || !Enum.IsDefined(typeof(CharacterWaterSourceKind), data.sourceKind)
            || !Enum.IsDefined(typeof(WorldWaterQuality), data.quality)
            || data.absoluteDay <= 0
            || !Finite(data.consumed)
            || !Finite(data.thirstBefore) || !Finite(data.thirstAfter)
            || !Finite(data.healthBefore) || !Finite(data.healthAfter)
            || !Finite(data.hygieneBefore) || !Finite(data.hygieneAfter)
            || !Finite(data.infectionBefore) || !Finite(data.infectionAfter)
            || !Finite(data.moodBefore) || !Finite(data.moodAfter)
            || (proposalOnly ? data.consumed != 0f : data.consumed <= 0f))
            throw new ArgumentException("water-consumption-fact-invalid", nameof(data));

        this.data = data.Clone();
        this.data.pathogenDiseaseId ??= string.Empty;
        IsProposal = proposalOnly;
    }

    public bool IsProposal { get; }
    public CharacterWaterFactData Data => data.Clone();

    public CharacterWaterFactData Snapshot()
    {
        if (IsProposal)
            throw new InvalidOperationException("water-consumption-proposal-has-no-snapshot");
        return data.Clone();
    }

    public bool MatchesHeader(CharacterWaterConsumedFact other) => other != null
        && string.Equals(data.characterId, other.data.characterId, StringComparison.Ordinal)
        && string.Equals(data.characterName, other.data.characterName, StringComparison.Ordinal)
        && string.Equals(data.sourceId, other.data.sourceId, StringComparison.Ordinal)
        && data.sourceKind == other.data.sourceKind
        && data.quality == other.data.quality
        && string.Equals(data.pathogenDiseaseId, other.data.pathogenDiseaseId, StringComparison.Ordinal)
        && data.absoluteDay == other.data.absoluteDay;

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
