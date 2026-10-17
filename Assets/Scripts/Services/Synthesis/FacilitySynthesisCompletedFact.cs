using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class FacilitySynthesisCompletedFactData
{
    public string survivorFacilityId = string.Empty;
    public long revision;
    public string recipeId = string.Empty, recipeDisplayName = string.Empty;
    public string resultDefinitionId = string.Empty, resultDisplayName = string.Empty;
    public string[] materialIds = Array.Empty<string>();
    public string[] materialDefinitionIds = Array.Empty<string>();
    public string[] materialDisplayNames = Array.Empty<string>();
    public int inheritedLevel, absoluteDay, x, y;

    public FacilitySynthesisCompletedFactData Clone() => new()
    {
        survivorFacilityId = survivorFacilityId, revision = revision,
        recipeId = recipeId, recipeDisplayName = recipeDisplayName,
        resultDefinitionId = resultDefinitionId, resultDisplayName = resultDisplayName,
        materialIds = materialIds?.ToArray(), materialDefinitionIds = materialDefinitionIds?.ToArray(),
        materialDisplayNames = materialDisplayNames?.ToArray(),
        inheritedLevel = inheritedLevel, absoluteDay = absoluteDay, x = x, y = y
    };

    public void Validate()
    {
        if (!DomainFactValueSyntax.IsIdentity(survivorFacilityId) || revision <= 0
            || !DomainFactValueSyntax.IsIdentity(recipeId) || !Name(recipeDisplayName)
            || !DomainFactValueSyntax.IsIdentity(resultDefinitionId) || !Name(resultDisplayName)
            || materialIds == null || materialDefinitionIds == null || materialDisplayNames == null
            || materialIds.Length < 2 || materialIds.Length != materialDefinitionIds.Length
            || materialIds.Length != materialDisplayNames.Length
            || materialIds.Any(value => !DomainFactValueSyntax.IsIdentity(value))
            || materialDefinitionIds.Any(value => !DomainFactValueSyntax.IsIdentity(value))
            || materialDisplayNames.Any(value => !Name(value))
            || !materialIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(materialIds, StringComparer.Ordinal)
            || !materialIds.Contains(survivorFacilityId, StringComparer.Ordinal)
            || inheritedLevel <= 0 || absoluteDay < 0)
            throw new ArgumentException("facility-synthesis-fact-invalid");
    }

    private static bool Name(string value) => DomainFactValueSyntax.IsText(
        value, DomainFactValueSyntax.MaximumNameCharacters, false) && value == value.Trim();
}

// Frozen domain result only; native identities, scoring and Korean rendering belong to observers.
public readonly struct FacilitySynthesisCompletedFact
{
    private readonly FacilitySynthesisCompletedFactData data;
    public FacilitySynthesisCompletedFact(FacilitySynthesisCompletedFactData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate(); data = source.Clone();
    }
    public FacilitySynthesisCompletedFactData Snapshot() => data?.Clone()
        ?? throw new InvalidOperationException("facility-synthesis-fact-empty");
}
