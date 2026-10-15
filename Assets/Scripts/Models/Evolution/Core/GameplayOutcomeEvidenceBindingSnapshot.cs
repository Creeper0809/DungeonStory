using System;
using System.Collections.Generic;

/// <summary>
/// Primitive-only persisted identity for an exact gameplay-outcome evidence row.
/// This type lives in DungeonStory.Evolution so evolution save models never depend
/// on the default Assembly-CSharp presentation/runtime assembly.
/// </summary>
[Serializable]
public sealed class GameplayOutcomeEvidenceBindingSnapshot
{
    // Version 1 contained only flattened ID sets. Version 2 preserves the
    // typed, model-visible evidence that was actually selected so save/load,
    // export and replay can reject role/value/causal tampering.
    // Version 4 freezes policy-v2 judgments, Korean reasons and the exact typed
    // fact/metric/role IDs used for interpretation. No old-save migration is added.
    public int schemaVersion = 1;
    public string sourceKind = "exact";
    public string publicFactId = string.Empty;
    public string outcomeRunId = string.Empty;
    public long outcomeSequence;
    public string compactedMemoryId = string.Empty;
    public string outcomeTypeId = string.Empty;
    public string subjectKindId = string.Empty;
    public string subjectId = string.Empty;
    public int perspectiveKind;
    public int firstDay;
    public int lastDay;
    public int occurrenceCount = 1;
    public int anchorRevision;
    public int status;
    public float subjectSalience;
    // Derived interpretation, not the ledger's mutable salience or storage tier.
    // Frozen with a pending request; raw facts remain unchanged and auditable.
    public GameplayExperienceAssessmentSnapshot experience;
    public int influenceUseCount;
    public int influenceRevision;
    public string canonicalFactText = string.Empty;
    public List<string> roleIds = new();
    public List<string> metricIds = new();
    public List<string> metricReferenceIds = new();
    public List<string> factIds = new();
    public List<string> semanticTags = new();
    public List<GameplayOutcomeEvidenceParticipantBindingSnapshot> participants = new();
    public List<GameplayOutcomeEvidenceMetricBindingSnapshot> metrics = new();
    public List<GameplayOutcomeEvidenceFactBindingSnapshot> facts = new();
    public List<GameplayOutcomeEvidenceProvenanceBindingSnapshot> provenance = new();
    public string locationId = string.Empty;
    public string roomId = string.Empty;
    public int locationX;
    public int locationY;
    public string operationId = string.Empty;
    public string parentOutcomeId = string.Empty;
    public string rootOperationId = string.Empty;
    public string causationRelationId = string.Empty;

    public GameplayOutcomeEvidenceBindingSnapshot Clone() => new()
    {
        schemaVersion = schemaVersion,
        sourceKind = sourceKind ?? string.Empty,
        publicFactId = publicFactId ?? string.Empty,
        outcomeRunId = outcomeRunId ?? string.Empty,
        outcomeSequence = outcomeSequence,
        compactedMemoryId = compactedMemoryId ?? string.Empty,
        outcomeTypeId = outcomeTypeId ?? string.Empty,
        subjectKindId = subjectKindId ?? string.Empty,
        subjectId = subjectId ?? string.Empty,
        perspectiveKind = perspectiveKind,
        firstDay = firstDay,
        lastDay = lastDay,
        occurrenceCount = occurrenceCount,
        anchorRevision = anchorRevision,
        status = status,
        subjectSalience = subjectSalience,
        experience = experience?.Clone(),
        influenceUseCount = influenceUseCount,
        influenceRevision = influenceRevision,
        canonicalFactText = canonicalFactText ?? string.Empty,
        roleIds = roleIds == null ? new List<string>() : new List<string>(roleIds),
        metricIds = metricIds == null ? new List<string>() : new List<string>(metricIds),
        metricReferenceIds = metricReferenceIds == null
            ? new List<string>() : new List<string>(metricReferenceIds),
        factIds = factIds == null ? new List<string>() : new List<string>(factIds),
        semanticTags = semanticTags == null
            ? new List<string>() : new List<string>(semanticTags),
        participants = participants == null
            ? new List<GameplayOutcomeEvidenceParticipantBindingSnapshot>()
            : participants.ConvertAll(value => value?.Clone()),
        metrics = metrics == null
            ? new List<GameplayOutcomeEvidenceMetricBindingSnapshot>()
            : metrics.ConvertAll(value => value?.Clone()),
        facts = facts == null
            ? new List<GameplayOutcomeEvidenceFactBindingSnapshot>()
            : facts.ConvertAll(value => value?.Clone()),
        provenance = provenance == null
            ? new List<GameplayOutcomeEvidenceProvenanceBindingSnapshot>()
            : provenance.ConvertAll(value => value?.Clone()),
        locationId = locationId ?? string.Empty,
        roomId = roomId ?? string.Empty,
        locationX = locationX,
        locationY = locationY,
        operationId = operationId ?? string.Empty,
        parentOutcomeId = parentOutcomeId ?? string.Empty,
        rootOperationId = rootOperationId ?? string.Empty,
        causationRelationId = causationRelationId ?? string.Empty
    };
}

[Serializable]
public sealed class GameplayExperienceAssessmentSnapshot
{
    public int policyVersion;
    public string observerId = string.Empty;
    // 0 = invalid/insufficient evidence; 1..5 = experience levels; 6 = technical/non-experience.
    public int category;
    public float importance;
    public string reasonCode = string.Empty;
    public string explanationKo = string.Empty;
    public List<string> evidenceMetricIds = new();
    public List<string> evidenceFactIds = new();
    public List<string> evidenceRoleIds = new();

    public GameplayExperienceAssessmentSnapshot Clone() => new()
    {
        policyVersion = policyVersion,
        observerId = observerId,
        category = category,
        importance = importance,
        reasonCode = reasonCode,
        explanationKo = explanationKo,
        evidenceMetricIds = evidenceMetricIds == null ? null : new(evidenceMetricIds),
        evidenceFactIds = evidenceFactIds == null ? null : new(evidenceFactIds),
        evidenceRoleIds = evidenceRoleIds == null ? null : new(evidenceRoleIds)
    };
}

[Serializable]
public sealed class GameplayOutcomeEvidenceParticipantBindingSnapshot
{
    public string entityKindId = string.Empty;
    public string entityId = string.Empty;
    public string roleId = string.Empty;
    public int participationKind;
    public bool hasPerceptionEvidence;
    public string displayText = string.Empty;
    public string displayRevision = string.Empty;
    public string locale = string.Empty;
    public int pronunciationMode;
    public string pronunciationValue = string.Empty;
    public int finalConsonant;
    public string pronunciationRevision = string.Empty;

    public GameplayOutcomeEvidenceParticipantBindingSnapshot Clone() => new()
    {
        entityKindId = entityKindId ?? string.Empty,
        entityId = entityId ?? string.Empty,
        roleId = roleId ?? string.Empty,
        participationKind = participationKind,
        hasPerceptionEvidence = hasPerceptionEvidence,
        displayText = displayText ?? string.Empty,
        displayRevision = displayRevision ?? string.Empty,
        locale = locale ?? string.Empty,
        pronunciationMode = pronunciationMode,
        pronunciationValue = pronunciationValue ?? string.Empty,
        finalConsonant = finalConsonant,
        pronunciationRevision = pronunciationRevision ?? string.Empty
    };
}

[Serializable]
public sealed class GameplayOutcomeEvidenceMetricBindingSnapshot
{
    public string metricId = string.Empty;
    public string unitId = string.Empty;
    public string referenceKindId = string.Empty;
    public string referenceId = string.Empty;
    public double value;
    public double minimum;
    public double maximum;
    public int sampleCount = 1;

    public GameplayOutcomeEvidenceMetricBindingSnapshot Clone() => new()
    {
        metricId = metricId ?? string.Empty,
        unitId = unitId ?? string.Empty,
        referenceKindId = referenceKindId ?? string.Empty,
        referenceId = referenceId ?? string.Empty,
        value = value,
        minimum = minimum,
        maximum = maximum,
        sampleCount = sampleCount
    };
}

[Serializable]
public sealed class GameplayOutcomeEvidenceFactBindingSnapshot
{
    public string factId = string.Empty;
    public string value = string.Empty;

    public GameplayOutcomeEvidenceFactBindingSnapshot Clone() => new()
    {
        factId = factId ?? string.Empty,
        value = value ?? string.Empty
    };
}

[Serializable]
public sealed class GameplayOutcomeEvidenceProvenanceBindingSnapshot
{
    public string kindId = string.Empty;
    public string value = string.Empty;

    public GameplayOutcomeEvidenceProvenanceBindingSnapshot Clone() => new()
    {
        kindId = kindId ?? string.Empty,
        value = value ?? string.Empty
    };
}
