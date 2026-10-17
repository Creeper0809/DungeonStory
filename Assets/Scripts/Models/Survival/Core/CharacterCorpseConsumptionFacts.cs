using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class CharacterCorpseConsumptionExperienceData
{
    public string characterId = string.Empty;
    public string characterName = string.Empty;
    public string victimId = string.Empty;
    public string victimName = string.Empty;
    public string victimSpecies = string.Empty;
    public string deathReason = string.Empty;
    public string memory = string.Empty;
    public bool sameSpecies;
    public int absoluteDay;
    public int memoriesBefore;
    public int memoriesAfter;
    public float hungerBefore;
    public float hungerAfter;
    public float hygieneBefore;
    public float hygieneAfter;
    public float infectionBefore;
    public float infectionAfter;
    public float moodBefore;
    public float moodAfter;

    public CharacterCorpseConsumptionExperienceData Clone() =>
        (CharacterCorpseConsumptionExperienceData)MemberwiseClone();
}

[Serializable]
public sealed class CharacterCorpseConsumptionFactData
{
    public CharacterCorpseConsumptionExperienceData experience = new();
    public PhysicalItemBatchDispositionSaveData physical = new();

    public CharacterCorpseConsumptionFactData Clone() => new()
    {
        experience = experience?.Clone(),
        physical = ClonePhysical(physical)
    };

    private static PhysicalItemBatchDispositionSaveData ClonePhysical(
        PhysicalItemBatchDispositionSaveData source)
    {
        if (source == null) return null;
        return new PhysicalItemBatchDispositionSaveData
        {
            kind = source.kind,
            operationId = source.operationId,
            reasonCode = source.reasonCode,
            requestFingerprint = source.requestFingerprint,
            sourceStackIds = source.sourceStackIds == null
                ? null
                : new List<string>(source.sourceStackIds),
            quantity = source.quantity,
            inputMassGrams = source.inputMassGrams,
            commitId = source.commitId,
            ownerRevision = source.ownerRevision,
            publicationExpected = source.publicationExpected,
            publicationKey = source.publicationKey,
            publicationId = source.publicationId,
            publicationFingerprint = source.publicationFingerprint,
            publicationDelivered = source.publicationDelivered,
            defaultObservationDay = source.defaultObservationDay,
            sourceFacts = source.sourceFacts?.Select(CloneSourceFact).ToList()
        };
    }

    private static PhysicalItemDispositionSourceFactSaveData CloneSourceFact(
        PhysicalItemDispositionSourceFactSaveData source) => source == null
        ? null
        : new PhysicalItemDispositionSourceFactSaveData
        {
            stackId = source.stackId,
            itemDefinitionId = source.itemDefinitionId,
            itemInstanceId = source.itemInstanceId,
            quantity = source.quantity,
            massGrams = source.massGrams,
            sourceX = source.sourceX,
            sourceY = source.sourceY,
            displayText = source.displayText
        };
}

[Serializable]
public sealed class CharacterCorpseConsumptionPublicationData
{
    public string publicationKey = string.Empty;
    public CharacterCorpseConsumptionFactData fact = new();

    public CharacterCorpseConsumptionPublicationData Clone() => new()
    {
        publicationKey = publicationKey,
        fact = fact?.Clone()
    };
}
