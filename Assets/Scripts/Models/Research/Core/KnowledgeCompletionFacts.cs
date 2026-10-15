using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class KnowledgeCompletionExperienceData
{
    public string taskId = string.Empty;
    public string facilityId = string.Empty;
    public string facilityName = string.Empty;
    public string regionId = string.Empty;
    public string regionName = string.Empty;
    public string codexClue = string.Empty;
    public KnowledgeResidueUse use;
    public int absoluteDay;
    public float rewardBefore;
    public float rewardAfter;

    public KnowledgeCompletionExperienceData Clone() =>
        (KnowledgeCompletionExperienceData)MemberwiseClone();
}

[Serializable]
public sealed class KnowledgeCompletionFactData
{
    public KnowledgeCompletionExperienceData experience = new();
    public PhysicalItemBatchDispositionSaveData physical = new();

    // JsonUtility serializes nested classes inline, including otherwise null
    // fields. Use an explicit empty value for an unapplied task, not null semantics.
    public bool IsEmpty => experience != null && physical != null
        && experience.taskId == string.Empty && experience.facilityId == string.Empty
        && experience.facilityName == string.Empty && experience.regionId == string.Empty
        && experience.regionName == string.Empty && experience.codexClue == string.Empty
        && experience.use == KnowledgeResidueUse.CodexAnalysis && experience.absoluteDay == 0
        && experience.rewardBefore == 0 && experience.rewardAfter == 0
        && physical.kind == 0 && physical.operationId == string.Empty && physical.reasonCode == string.Empty
        && physical.requestFingerprint == string.Empty && physical.quantity == 0 && physical.inputMassGrams == 0
        && physical.commitId == string.Empty && physical.ownerRevision == 0
        && !physical.publicationExpected && physical.publicationKey == string.Empty
        && physical.publicationId == string.Empty && physical.publicationFingerprint == string.Empty
        && !physical.publicationDelivered && physical.defaultObservationDay == 0
        && physical.sourceStackIds?.Count == 0 && physical.sourceFacts?.Count == 0;

    public KnowledgeCompletionFactData Clone() => new()
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
