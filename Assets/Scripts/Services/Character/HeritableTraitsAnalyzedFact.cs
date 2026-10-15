using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

// Facts describe the completed analysis, not its log representation or importance.
[Serializable]
public sealed class HeritableTraitsAnalyzedFactData
{
    public string operationId;
    public int operationSequence;
    public string characterId;
    public string characterDisplayName;
    public string facilityInstanceId;
    public string facilityDefinitionId;
    public string kitItemId;
    public string physicalCommitId;
    public string[] sourceStackIds;
    public long inputMassGrams;
    public string[] latentTraitIds;
    public int absoluteDay;

    public HeritableTraitsAnalyzedFactData Clone() => new()
    {
        operationId = operationId, operationSequence = operationSequence,
        characterId = characterId, characterDisplayName = characterDisplayName,
        facilityInstanceId = facilityInstanceId, facilityDefinitionId = facilityDefinitionId,
        kitItemId = kitItemId, physicalCommitId = physicalCommitId,
        sourceStackIds = sourceStackIds?.ToArray(), inputMassGrams = inputMassGrams,
        latentTraitIds = latentTraitIds?.ToArray(), absoluteDay = absoluteDay
    };

    public void Validate()
    {
        if (!DomainFactValueSyntax.IsIdentity(operationId) || operationSequence < 0
            || !DomainFactValueSyntax.IsIdentity(characterId)
            || !DomainFactValueSyntax.IsIdentity(facilityInstanceId)
            || !DomainFactValueSyntax.IsIdentity(facilityDefinitionId)
            || !DomainFactValueSyntax.IsIdentity(kitItemId)
            || !DomainFactValueSyntax.IsIdentity(physicalCommitId)
            || !DomainFactValueSyntax.IsText(characterDisplayName,
                DomainFactValueSyntax.MaximumNameCharacters, false)
            || characterDisplayName != characterDisplayName.Trim()
            || inputMassGrams <= 0 || absoluteDay < 1
            || !CanonicalIds(sourceStackIds) || !CanonicalIds(latentTraitIds))
            throw new ArgumentException("heritable-traits-analyzed-fact-invalid");
    }

    private static bool CanonicalIds(string[] values) => values != null && values.Length > 0
        && values.All(DomainFactValueSyntax.IsIdentity)
        && values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(values, StringComparer.Ordinal);
}

public readonly struct HeritableTraitsAnalyzedFact
{
    private readonly HeritableTraitsAnalyzedFactData data;
    public HeritableTraitsAnalyzedFact(HeritableTraitsAnalyzedFactData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate();
        data = source.Clone();
    }
    public HeritableTraitsAnalyzedFactData Snapshot() => data?.Clone()
        ?? throw new InvalidOperationException("heritable-traits-analyzed-fact-empty");
}
