using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class CharacterRestAppliedFactData
{
    public string characterId = string.Empty;
    public string displayName = string.Empty;
    public float before;
    public float after;
    public int absoluteDay;
    public List<string> activeConditions = new();
    public CharacterEventContext context;
    public CharacterRestAppliedFactData Clone() => new()
    { characterId = characterId, displayName = displayName, before = before, after = after,
        absoluteDay = absoluteDay, activeConditions = activeConditions?.ToList(), context = context?.Clone() };
}

public sealed class CharacterRestAppliedFact
{
    private readonly CharacterRestAppliedFactData data;
    public CharacterRestAppliedFact(CharacterRestAppliedFactData source)
    {
        if (source == null || !DomainFactValueSyntax.IsIdentity(source.characterId)
            || !DomainFactValueSyntax.IsText(source.displayName, 16384, true)
            || !float.IsFinite(source.before) || !float.IsFinite(source.after)
            || source.before < 0f || source.after < 0f || source.absoluteDay < 0
            || source.activeConditions == null || source.activeConditions.Count > 256
            || source.activeConditions.Any(value => !DomainFactValueSyntax.IsText(value, 1024, false)))
            throw new ArgumentException("rest-applied-fact-invalid");
        source.context?.Validate();
        data = source.Clone();
    }
    public CharacterRestAppliedFactData Snapshot() => data.Clone();
}

[Serializable]
public sealed class CharacterRestObservationSaveData
{
    public CharacterRestAppliedFactData fact = new();
    public string publicationKey = string.Empty;
    public bool observationCommitted;
    public bool deliveryCompleted;
    public bool notificationsAttempted;
    public bool rollbackBlocked;
    public string rollbackFailure = string.Empty;
    public CharacterRestObservationSaveData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey, observationCommitted = observationCommitted,
        deliveryCompleted = deliveryCompleted,
        notificationsAttempted = notificationsAttempted, rollbackBlocked = rollbackBlocked,
        rollbackFailure = rollbackFailure };
    public void Validate(string owner)
    {
        _ = new CharacterRestAppliedFact(fact);
        if (fact.characterId != owner || !DomainFactValueSyntax.IsText(publicationKey, 4096, false)
            || !DomainFactValueSyntax.IsText(rollbackFailure, 16384, true)
            || rollbackBlocked != (rollbackFailure.Length > 0)
            || rollbackBlocked && (observationCommitted || deliveryCompleted || notificationsAttempted)
            || (notificationsAttempted || deliveryCompleted) && !observationCommitted)
            throw new ArgumentException("rest-observation-save-invalid");
    }
}
