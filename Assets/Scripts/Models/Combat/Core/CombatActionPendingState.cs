using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class CombatActionPendingData
{
    public string publicationKey = string.Empty;
    public CombatActionFactData fact;
    public CombatActionPendingData Clone() => new() { publicationKey = publicationKey, fact = fact?.Clone() };
}

[Serializable]
public sealed class CombatActionPendingSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<CombatActionPendingData> pending = new();
}

/// <summary>Applied attacks awaiting observation, independent of command/engagement lifetime.</summary>
public sealed class CombatActionPendingState
{
    public const int Capacity = 256;
    private readonly List<CombatActionPendingData> pending = new();
    public int Count => pending.Count;
    public bool ContainsOperation(string identity) => pending.Any(item => new CombatActionFact(item.fact).Identity == identity);
    public void Add(CombatActionPendingData value)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.publicationKey) || value.publicationKey.Length > 1024)
            throw new ArgumentException("combat-pending-reference-invalid");
        var fact = new CombatActionFact(value.fact);
        if (Count >= Capacity || ContainsOperation(fact.Identity) || pending.Any(item => item.publicationKey == value.publicationKey))
            throw new InvalidOperationException("combat-pending-duplicate-or-capacity");
        pending.Add(value.Clone());
    }
    public void Remove(string key) => pending.RemoveAll(value => value.publicationKey == key);
    public CombatActionPendingSaveData Capture() => new() { pending = pending.Select(value => value.Clone()).ToList() };
    public static CombatActionPendingState Restore(CombatActionPendingSaveData save)
    {
        if (save == null || save.version != CombatActionPendingSaveData.CurrentVersion || save.pending == null)
            throw new ArgumentException("combat-pending-save-invalid");
        var state = new CombatActionPendingState();
        foreach (var value in save.pending) state.Add(value);
        return state;
    }
}
