using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class CharacterNarrativeEvidenceMetadata
{
    public const float OrdinaryImportance = 1f;

    public CharacterNarrativeEvidenceMetadata(
        string eventGroupKey,
        string actionKey,
        string relationshipKey,
        float importancePoints)
    {
        this.eventGroupKey = RequireCanonical(eventGroupKey, nameof(eventGroupKey));
        this.actionKey = RequireCanonical(actionKey, nameof(actionKey));
        this.relationshipKey = NormalizeOptional(relationshipKey, nameof(relationshipKey));
        if (float.IsNaN(importancePoints) || float.IsInfinity(importancePoints) || importancePoints < 0f)
            throw new ArgumentOutOfRangeException(nameof(importancePoints));
        this.importancePoints = importancePoints;
    }

    public string eventGroupKey { get; }
    public string actionKey { get; }
    public string relationshipKey { get; }
    public float importancePoints { get; }

    public static CharacterNarrativeEvidenceMetadata Ordinary(
        CharacterNarrativeDomain domain,
        string factId)
    {
        string canonicalFact = factId?.Trim() ?? string.Empty;
        return new CharacterNarrativeEvidenceMetadata(
            "ordinary:" + domain,
            "ordinary:" + canonicalFact,
            string.Empty,
            OrdinaryImportance);
    }

    private static string RequireCanonical(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new ArgumentException("A canonical non-empty narrative evidence key is required.", name);
        return canonical;
    }

    private static string NormalizeOptional(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (!string.Equals(canonical, value ?? string.Empty, StringComparison.Ordinal))
            throw new ArgumentException("Narrative evidence keys must already be canonical.", name);
        return canonical;
    }
}

[Serializable]
public sealed class CharacterNarrativeFact
{
    public CharacterNarrativeDomain domain;
    public string factId = string.Empty;
    public string subjectId = string.Empty;
    public string outcome = string.Empty;
    public int count;
    public float totalValue;
    public int lastDay;
    public int milestoneCount;
    [Min(0)] public int influenceUseCount;
    public string eventGroupKey = string.Empty;
    public string actionKey = string.Empty;
    public string relationshipKey = string.Empty;
    [Min(0f)] public float importancePoints = 1f;
    public GameplayNarrativeEventContext lastEventContext = new GameplayNarrativeEventContext();
    // One entry per occurrence. Empty means the producer supplied no event ID;
    // it is not reconstructed from the last known event or a causal chain.
    public List<string> sourceEventIds = new List<string>();

    public bool HasCompleteSourceEventIds => sourceEventIds != null
        && sourceEventIds.Count == count
        && sourceEventIds.All(value => value != null
            && string.Equals(value, value.Trim(), StringComparison.Ordinal));

    public CharacterNarrativeFact Clone()
    {
        CharacterNarrativeFact clone = (CharacterNarrativeFact)MemberwiseClone();
        clone.lastEventContext = lastEventContext?.Clone() ?? new GameplayNarrativeEventContext();
        clone.sourceEventIds = sourceEventIds == null ? null : new List<string>(sourceEventIds);
        return clone;
    }
}

[Serializable]
public sealed class CharacterNarrativeLedger
{
    private static readonly int[] Milestones = { 1, 3, 8, 20, 50 };

    public List<CharacterNarrativeFact> facts = new List<CharacterNarrativeFact>();
    public long nextEventSequence = 1L;

    public IReadOnlyList<CharacterNarrativeFact> Facts => facts ??= new List<CharacterNarrativeFact>();
    public int MeaningfulRecordCount => Facts.Sum(item => item?.milestoneCount ?? 0);
    public int MeaningfulDomainCount => Facts
        .Where(item => item != null && item.milestoneCount > 0)
        .Select(item => item.domain)
        .Distinct()
        .Count();

    public void Record(
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value = 0f,
        int day = 0)
    {
        if (string.IsNullOrWhiteSpace(factId)) return;
        Record(domain, factId, subjectId, outcome, value, day,
            CharacterNarrativeEvidenceMetadata.Ordinary(domain, factId));
    }

    public void Record(
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value,
        int day,
        CharacterNarrativeEvidenceMetadata metadata)
    {
        Record(domain, factId, subjectId, outcome, value, day, metadata, null);
    }

    public void Record(
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value,
        int day,
        CharacterNarrativeEvidenceMetadata metadata,
        GameplayNarrativeEventContext eventContext)
    {
        if (string.IsNullOrWhiteSpace(factId))
        {
            return;
        }

        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        facts ??= new List<CharacterNarrativeFact>();
        string normalizedFactId = factId.Trim();
        string normalizedSubject = subjectId?.Trim() ?? string.Empty;
        CharacterNarrativeFact fact = facts.Find(item => item != null
            && item.domain == domain
            && string.Equals(item.factId, normalizedFactId, StringComparison.Ordinal)
            && string.Equals(item.subjectId, normalizedSubject, StringComparison.Ordinal));
        if (fact == null)
        {
            fact = new CharacterNarrativeFact
            {
                domain = domain,
                factId = normalizedFactId,
                subjectId = normalizedSubject,
                eventGroupKey = metadata.eventGroupKey,
                actionKey = metadata.actionKey,
                relationshipKey = metadata.relationshipKey,
                importancePoints = metadata.importancePoints
            };
            facts.Add(fact);
        }
        else if (!string.Equals(fact.eventGroupKey, metadata.eventGroupKey, StringComparison.Ordinal)
            || !string.Equals(fact.actionKey, metadata.actionKey, StringComparison.Ordinal)
            || !string.Equals(fact.relationshipKey, metadata.relationshipKey, StringComparison.Ordinal)
            || fact.importancePoints != metadata.importancePoints)
        {
            throw new InvalidOperationException(
                $"Narrative fact '{normalizedFactId}' cannot change its formula evidence metadata.");
        }

        if (fact.sourceEventIds == null || fact.sourceEventIds.Count != fact.count)
            throw new InvalidOperationException(
                $"Narrative fact '{normalizedFactId}' has incomplete source occurrence identities.");
        int nextCount = checked(fact.count + 1);
        fact.sourceEventIds.Add(eventContext?.eventInstanceId?.Trim() ?? string.Empty);
        fact.count = nextCount;
        fact.totalValue += value;
        fact.lastDay = Mathf.Max(fact.lastDay, day);
        fact.outcome = outcome?.Trim() ?? string.Empty;
        fact.milestoneCount = Milestones.Count(threshold => fact.count >= threshold);
        if (eventContext != null)
        {
            fact.lastEventContext = eventContext.Clone();
            if (fact.lastEventContext.sequence <= 0L)
                fact.lastEventContext.sequence = Math.Max(1L, nextEventSequence);
            nextEventSequence = Math.Max(nextEventSequence, fact.lastEventContext.sequence + 1L);
        }
    }

    public void Record(
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value,
        int day,
        string eventGroupKey,
        string actionKey,
        string relationshipKey,
        float importancePoints)
    {
        Record(domain, factId, subjectId, outcome, value, day,
            new CharacterNarrativeEvidenceMetadata(
                eventGroupKey,
                actionKey,
                relationshipKey,
                importancePoints));
    }

    public CharacterNarrativeLedger Clone()
    {
        return new CharacterNarrativeLedger
        {
            facts = Facts.Where(item => item != null).Select(item => item.Clone()).ToList(),
            nextEventSequence = Math.Max(1L, nextEventSequence)
        };
    }
}
