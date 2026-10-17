using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

// Frozen domain result. No log key, receipt, score, or display sentence is owned here.
[Serializable]
public sealed class FacilityEvolutionCompletedFactData
{
    public string operationId = string.Empty;
    public int historySequence;
    public string facilityPersistentId = string.Empty;
    public string facilityDisplayName = string.Empty;
    public string recipeId = string.Empty;
    public string resultFacilityDefinitionId = string.Empty;
    public int resultStarGrade;
    public string materialCommitId = string.Empty;
    public string[] sourceStackIds = Array.Empty<string>();
    public int materialQuantity;
    public long materialInputMassGrams;
    public int absoluteDay;

    public bool HasMaterialCommit => !string.IsNullOrEmpty(materialCommitId);
    public bool IsEmpty => string.IsNullOrEmpty(operationId) && historySequence == 0
        && string.IsNullOrEmpty(facilityPersistentId) && string.IsNullOrEmpty(facilityDisplayName)
        && string.IsNullOrEmpty(recipeId) && string.IsNullOrEmpty(resultFacilityDefinitionId)
        && resultStarGrade == 0 && string.IsNullOrEmpty(materialCommitId)
        && (sourceStackIds == null || sourceStackIds.Length == 0)
        && materialQuantity == 0 && materialInputMassGrams == 0 && absoluteDay == 0;

    public FacilityEvolutionCompletedFactData Clone() => new()
    {
        operationId = operationId, historySequence = historySequence,
        facilityPersistentId = facilityPersistentId, facilityDisplayName = facilityDisplayName,
        recipeId = recipeId, resultFacilityDefinitionId = resultFacilityDefinitionId,
        resultStarGrade = resultStarGrade, materialCommitId = materialCommitId,
        sourceStackIds = sourceStackIds?.ToArray(), materialQuantity = materialQuantity,
        materialInputMassGrams = materialInputMassGrams, absoluteDay = absoluteDay
    };

    public void Validate()
    {
        if (!DomainFactValueSyntax.IsIdentity(operationId)
            || !DomainFactValueSyntax.IsIdentity(facilityPersistentId)
            || !DomainFactValueSyntax.IsIdentity(recipeId)
            || !DomainFactValueSyntax.IsIdentity(resultFacilityDefinitionId)
            || !Text(facilityDisplayName, false) || !Text(materialCommitId, true)
            || facilityDisplayName.Length > DomainFactValueSyntax.MaximumNameCharacters
            || historySequence <= 0 || resultStarGrade <= 0 || absoluteDay < 0
            || sourceStackIds == null
            || sourceStackIds.Any(value => !DomainFactValueSyntax.IsIdentity(value))
            || !sourceStackIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(sourceStackIds, StringComparer.Ordinal)
            || (HasMaterialCommit
                ? materialQuantity <= 0 || materialInputMassGrams <= 0 || sourceStackIds.Length == 0
                : materialQuantity != 0 || materialInputMassGrams != 0 || sourceStackIds.Length != 0))
            throw new ArgumentException("facility-evolution-completed-fact-invalid");
    }

    internal static bool Text(string value, bool allowEmpty) =>
        DomainFactValueSyntax.IsText(value, 4096, allowEmpty) && value == value.Trim();
}

public readonly struct FacilityEvolutionCompletedFact
{
    private readonly FacilityEvolutionCompletedFactData data;
    public FacilityEvolutionCompletedFact(FacilityEvolutionCompletedFactData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate();
        data = source.Clone();
    }
    public FacilityEvolutionCompletedFactData Snapshot() => data?.Clone()
        ?? throw new InvalidOperationException("facility-evolution-completed-fact-empty");
}
