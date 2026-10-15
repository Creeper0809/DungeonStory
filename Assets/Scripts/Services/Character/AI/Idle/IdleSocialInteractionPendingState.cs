using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class IdleSocialInteractionPendingData
{
    public string publicationKey = string.Empty;
    public IdleSocialInteractionFactData fact;
    public IdleSocialInteractionPendingData Clone() => new() { publicationKey = publicationKey, fact = fact?.Clone() };
}

[Serializable]
public sealed class IdleSocialInteractionPendingSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<IdleSocialInteractionPendingData> pending = new();
}

/// <summary>Applied interactions awaiting observation; replay never owns or reapplies relationship/mood state.</summary>
public sealed class IdleSocialInteractionPendingState
{
    public const int Capacity = 64;
    private readonly List<IdleSocialInteractionPendingData> pending = new();
    public int Count => pending.Count;
    public bool ContainsParticipant(string id) => pending.Any(value => value.fact.actorId == id || value.fact.targetId == id);
    public void Add(IdleSocialInteractionPendingData value)
    {
        if (value == null || !DomainFactValueSyntax.IsText(value.publicationKey, 1024, false))
            throw new ArgumentException("idle-social-pending-reference-invalid");
        var fact = new IdleSocialInteractionResolvedFact(value.fact);
        if (Count >= Capacity || ContainsParticipant(fact.ActorId) || ContainsParticipant(fact.TargetId)
            || pending.Any(item => item.publicationKey == value.publicationKey || item.fact.operationId == fact.OperationId))
            throw new InvalidOperationException("idle-social-pending-duplicate-participant-or-capacity");
        pending.Add(value.Clone());
    }
    public void Remove(string key) => pending.RemoveAll(value => value.publicationKey == key);
    public IdleSocialInteractionPendingSaveData Capture() => new() { pending = pending.Select(value => value.Clone()).ToList() };
    public static IdleSocialInteractionPendingState Restore(IdleSocialInteractionPendingSaveData save)
    {
        if (save == null || save.version != IdleSocialInteractionPendingSaveData.CurrentVersion || save.pending == null)
            throw new ArgumentException("idle-social-pending-save-invalid");
        var result = new IdleSocialInteractionPendingState();
        foreach (var value in save.pending) result.Add(value);
        return result;
    }
}
