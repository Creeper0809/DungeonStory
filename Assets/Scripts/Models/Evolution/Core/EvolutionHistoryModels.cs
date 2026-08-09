using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class UsageLedgerEvent
{
    public string evidenceId = string.Empty;
    public string eventId = string.Empty;
    public string actorId = string.Empty;
    public string targetId = string.Empty;
    public float amount = 1f;
    public HistoricalEvidenceKind historicalEvidenceKind;
    public string outcomeId = string.Empty;
    public int generation;
    public int repeatCount = 1;
    public long sequence;
    public List<string> sourceTags = new List<string>();

    public UsageLedgerEvent Clone()
    {
        return new UsageLedgerEvent
        {
            evidenceId = evidenceId ?? string.Empty,
            eventId = eventId ?? string.Empty,
            actorId = actorId ?? string.Empty,
            targetId = targetId ?? string.Empty,
            amount = amount,
            historicalEvidenceKind = historicalEvidenceKind,
            outcomeId = outcomeId ?? string.Empty,
            generation = Mathf.Max(0, generation),
            repeatCount = Mathf.Max(1, repeatCount),
            sequence = sequence,
            sourceTags = sourceTags?
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToList() ?? new List<string>()
        };
    }
}

[Serializable]
public enum HistoricalEvidenceKind
{
    None,
    BossExecution,
    ProtectedOwner,
    InterceptedFatalHit,
    SurvivedNearDeath,
    RepeatedLongRangeHit,
    ArmorBroken,
    HeirInherited,
    OwnerDeathWitnessed,
    CapturedEnemy,
    ExpeditionReturn,
    UsedDuringPlague,
    UsedByMultipleGenerations
}

[Serializable]
public sealed class HistoricalEvidenceMetric
{
    public HistoricalEvidenceKind kind;
    public float strength;
    public int occurrences;

    public HistoricalEvidenceMetric Clone()
    {
        return new HistoricalEvidenceMetric
        {
            kind = kind,
            strength = strength,
            occurrences = Mathf.Max(0, occurrences)
        };
    }
}

[Serializable]
public sealed class UsageLedgerMetric
{
    public string metricId = string.Empty;
    public float value;

    public UsageLedgerMetric Clone()
    {
        return new UsageLedgerMetric
        {
            metricId = metricId ?? string.Empty,
            value = value
        };
    }
}

[Serializable]
public sealed class CompactedHistorySegment
{
    public int level;
    public int firstGeneration;
    public int lastGeneration;
    public int eventCount;
    public float totalMagnitude;
    public string historyHash = string.Empty;
    public List<UsageLedgerMetric> metrics = new List<UsageLedgerMetric>();
    public List<UsageLedgerEvent> keyEvents = new List<UsageLedgerEvent>();
    public List<HistoricalEvidenceMetric> historicalEvidence =
        new List<HistoricalEvidenceMetric>();
    public List<string> participantIds = new List<string>();
    public List<string> sourceTags = new List<string>();

    public CompactedHistorySegment Clone()
    {
        return new CompactedHistorySegment
        {
            level = Mathf.Max(0, level),
            firstGeneration = Mathf.Max(0, firstGeneration),
            lastGeneration = Mathf.Max(firstGeneration, lastGeneration),
            eventCount = Mathf.Max(0, eventCount),
            totalMagnitude = Mathf.Max(0f, totalMagnitude),
            historyHash = historyHash ?? string.Empty,
            metrics = metrics?
                .Where(metric => metric != null)
                .Select(metric => metric.Clone())
                .ToList() ?? new List<UsageLedgerMetric>(),
            keyEvents = keyEvents?
                .Where(entry => entry != null)
                .Select(entry => entry.Clone())
                .ToList() ?? new List<UsageLedgerEvent>(),
            historicalEvidence = historicalEvidence?
                .Where(entry => entry != null && entry.kind != HistoricalEvidenceKind.None)
                .Select(entry => entry.Clone())
                .OrderBy(entry => entry.kind)
                .ToList() ?? new List<HistoricalEvidenceMetric>(),
            participantIds = participantIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList() ?? new List<string>(),
            sourceTags = sourceTags?
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToList() ?? new List<string>()
        };
    }
}

[Serializable]
public sealed class UsageLedger
{
    public const int RawEventCapacity = 128;

    public long nextSequence = 1;
    public List<UsageLedgerEvent> currentGenerationEvents = new List<UsageLedgerEvent>();
    public List<CompactedHistorySegment> compactedSegments =
        new List<CompactedHistorySegment>();

    public UsageLedger Clone()
    {
        return new UsageLedger
        {
            nextSequence = Math.Max(1L, nextSequence),
            currentGenerationEvents = currentGenerationEvents?
                .Where(entry => entry != null)
                .Select(entry => entry.Clone())
                .TakeLast(RawEventCapacity)
                .ToList() ?? new List<UsageLedgerEvent>(),
            compactedSegments = compactedSegments?
                .Where(segment => segment != null)
                .Select(segment => segment.Clone())
                .ToList() ?? new List<CompactedHistorySegment>()
        };
    }
}

[Serializable]
public sealed class EvolutionNode
{
    public string nodeId = string.Empty;
    public string parentNodeId = string.Empty;
    public string effectId = string.Empty;
    public string burdenEffectId = string.Empty;
    public int generation;
    public bool active = true;
    public bool historical;
    public bool mechanicallyUnlocked = true;
    public bool narrativeReady = true;
    public bool uiVisible = true;
    public bool playerVisible = true;
    public string displayName = string.Empty;
    public string description = string.Empty;
    public string narrativeSchemaId = string.Empty;
    public int narrativeSchemaVersion;
    public string narrativeSchemaHash = string.Empty;
    public string narrativeCultureStyleId = string.Empty;
    public List<string> narrativeMotifIds = new List<string>();
    public List<string> narrativeCharacterFactIds = new List<string>();
    public string narrativePassVerdict = string.Empty;
    public int narrativeRetryCount;
    public bool narrativeUsedFallback;
    public float potencyMultiplier = 1f;
    public List<string> evidenceIds = new List<string>();
    public List<string> legalCandidateEffectIds = new List<string>();
    public int selectedCandidateIndex = -1;
    public EvolutionModuleActivationRule activationRule =
        new EvolutionModuleActivationRule();

    public EvolutionNode Clone()
    {
        return new EvolutionNode
        {
            nodeId = nodeId ?? string.Empty,
            parentNodeId = parentNodeId ?? string.Empty,
            effectId = effectId ?? string.Empty,
            burdenEffectId = burdenEffectId ?? string.Empty,
            generation = Mathf.Max(0, generation),
            active = active,
            historical = historical,
            mechanicallyUnlocked = mechanicallyUnlocked,
            narrativeReady = narrativeReady,
            uiVisible = uiVisible,
            playerVisible = playerVisible,
            displayName = displayName ?? string.Empty,
            description = description ?? string.Empty,
            narrativeSchemaId = narrativeSchemaId ?? string.Empty,
            narrativeSchemaVersion = Mathf.Max(0, narrativeSchemaVersion),
            narrativeSchemaHash = narrativeSchemaHash ?? string.Empty,
            narrativeCultureStyleId = narrativeCultureStyleId ?? string.Empty,
            narrativeMotifIds = narrativeMotifIds?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToList()
                ?? new List<string>(),
            narrativeCharacterFactIds = narrativeCharacterFactIds?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToList()
                ?? new List<string>(),
            narrativePassVerdict = narrativePassVerdict ?? string.Empty,
            narrativeRetryCount = Mathf.Max(0, narrativeRetryCount),
            narrativeUsedFallback = narrativeUsedFallback,
            potencyMultiplier = Mathf.Max(0.01f, potencyMultiplier),
            evidenceIds = evidenceIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>(),
            legalCandidateEffectIds = legalCandidateEffectIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(3)
                .ToList() ?? new List<string>(),
            selectedCandidateIndex = selectedCandidateIndex,
            activationRule = activationRule?.Clone() ??
                new EvolutionModuleActivationRule()
        };
    }
}

[Serializable]
public enum EvolutionNarrativeTargetKind
{
    Facility,
    Equipment
}

[Serializable]
public sealed class EvolutionNarrativeRequestSnapshot
{
    public string requestKey = string.Empty;
    public EvolutionNarrativeTargetKind targetKind;
    public string targetPersistentId = string.Empty;
    public string nodeId = string.Empty;
    public string parentNodeId = string.Empty;
    public string effectId = string.Empty;
    public string historyHash = string.Empty;
    public int generation;
    public int effectBudget = 1;
    public int attemptCount;
    public bool completed;
    public bool cancelled;
    public List<string> legalCandidateEffectIds = new List<string>();
    public int selectedCandidateIndex = -1;
    public List<string> evidenceIds = new List<string>();
    public List<string> participantIds = new List<string>();
    public List<string> sourceTags = new List<string>();

    public EvolutionNarrativeRequestSnapshot Clone()
    {
        return new EvolutionNarrativeRequestSnapshot
        {
            requestKey = requestKey ?? string.Empty,
            targetKind = targetKind,
            targetPersistentId = targetPersistentId ?? string.Empty,
            nodeId = nodeId ?? string.Empty,
            parentNodeId = parentNodeId ?? string.Empty,
            effectId = effectId ?? string.Empty,
            historyHash = historyHash ?? string.Empty,
            generation = Mathf.Max(0, generation),
            effectBudget = Mathf.Max(0, effectBudget),
            attemptCount = Mathf.Max(0, attemptCount),
            completed = completed,
            cancelled = cancelled,
            legalCandidateEffectIds = legalCandidateEffectIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(3)
                .ToList() ?? new List<string>(),
            selectedCandidateIndex = selectedCandidateIndex,
            evidenceIds = Normalize(evidenceIds),
            participantIds = Normalize(participantIds),
            sourceTags = Normalize(sourceTags)
        };
    }

    private static List<string> Normalize(IEnumerable<string> values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList() ?? new List<string>();
    }
}

[Serializable]
public sealed class AttunementRecord
{
    public string ownerPersistentId = string.Empty;
    public int affinityScore;
    public int attainedTier;
    public int startedGeneration;
    public string rootNodeId = string.Empty;
    public List<string> historyNodeIds = new List<string>();

    public AttunementRecord Clone()
    {
        return new AttunementRecord
        {
            ownerPersistentId = ownerPersistentId ?? string.Empty,
            affinityScore = Mathf.Max(0, affinityScore),
            attainedTier = Mathf.Max(0, attainedTier),
            startedGeneration = Mathf.Max(0, startedGeneration),
            rootNodeId = rootNodeId ?? string.Empty,
            historyNodeIds = historyNodeIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>()
        };
    }
}
