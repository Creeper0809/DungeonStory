using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CharacterMoodRecalculation
{
    public CharacterMoodRecalculation(
        bool expired,
        float previous,
        float current,
        CharacterMoodSnapshot snapshot)
    {
        Expired = expired;
        Previous = previous;
        Current = current;
        Snapshot = snapshot;
    }

    public bool Expired { get; }
    public float Previous { get; }
    public float Current { get; }
    public CharacterMoodSnapshot Snapshot { get; }
}

/// <summary>
/// Applies mood projection rules to caller-owned condition dictionaries and memories
/// without owning a second copy of that state.
/// </summary>
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterMoodStateService
{
    private readonly IGameClock gameClock;
    private readonly ICharacterNeedDefinitionQuery needDefinitions;

    public CharacterMoodStateService(
        IGameClock gameClock,
        ICharacterNeedDefinitionQuery needDefinitions)
    {
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.needDefinitions = needDefinitions
            ?? throw new ArgumentNullException(nameof(needDefinitions));
    }

    public float Time => gameClock.Time;

    public bool TryApplyFactor(
        List<CharacterMoodMemory> factors,
        string id,
        string label,
        float value,
        float durationSeconds,
        int maxStacks,
        out float now)
    {
        now = Time;
        if (string.IsNullOrWhiteSpace(id)
            || string.IsNullOrWhiteSpace(label)
            || Mathf.Approximately(value, 0f))
        {
            return false;
        }

        CharacterMoodRuntimeRules.PruneExpired(factors, now);
        CharacterMoodMemory factor = factors.Find(item =>
            item != null && item.Id == id);
        if (factor == null)
        {
            factors.Add(new CharacterMoodMemory(
                id,
                label,
                value,
                durationSeconds,
                maxStacks,
                now));
        }
        else
        {
            factor.Apply(label, value, durationSeconds, maxStacks, now);
        }

        return true;
    }

    public bool RemoveFactor(
        List<CharacterMoodMemory> factors,
        string id)
    {
        return !string.IsNullOrWhiteSpace(id)
            && factors != null
            && factors.RemoveAll(item => item != null && item.Id == id) > 0;
    }

    public List<CharacterMoodMemory> RestoreFactors(
        IReadOnlyList<CharacterMoodFactorSnapshot> snapshots)
    {
        List<CharacterMoodMemory> restored = new List<CharacterMoodMemory>();
        if (snapshots == null)
        {
            return restored;
        }

        float now = Time;
        foreach (CharacterMoodFactorSnapshot factor in snapshots)
        {
            if (factor == null
                || factor.Kind != CharacterMoodFactorKind.Interaction
                || string.IsNullOrWhiteSpace(factor.Id)
                || string.IsNullOrWhiteSpace(factor.Label)
                || Mathf.Approximately(factor.Value, 0f)
                || factor.RemainingSeconds <= 0f)
            {
                continue;
            }

            restored.Add(new CharacterMoodMemory(
                factor.Id,
                factor.Label,
                factor.Value,
                factor.RemainingSeconds,
                1,
                now));
        }

        return restored;
    }

    public void AdoptAssignedMoodAsBase(
        Dictionary<CharacterCondition, float> stats,
        IReadOnlyList<CharacterMoodMemory> factors,
        ref float baseMood,
        ref float lastCalculatedMood)
    {
        float requestedMood = stats.TryGetValue(
                CharacterCondition.MOOD,
                out float assigned)
            ? Mathf.Clamp(assigned, 0f, 100f)
            : CharacterMoodRules.DefaultBaseMood;
        float factorTotal = CharacterMoodRuntimeRules.CalculateFactorTotal(
            BuildSnapshot(stats, factors, baseMood, Time).Factors);
        baseMood = Mathf.Clamp(requestedMood - factorTotal, 0f, 100f);
        lastCalculatedMood = requestedMood;
    }

    public void SynchronizeExternalOverride(
        Dictionary<CharacterCondition, float> stats,
        IReadOnlyList<CharacterMoodMemory> factors,
        ref float baseMood,
        ref float lastCalculatedMood)
    {
        if (float.IsNaN(lastCalculatedMood)
            || !stats.TryGetValue(
                CharacterCondition.MOOD,
                out float currentMood)
            || Mathf.Approximately(currentMood, lastCalculatedMood))
        {
            return;
        }

        float factorTotal = CharacterMoodRuntimeRules.CalculateFactorTotal(
            BuildSnapshot(stats, factors, baseMood, Time).Factors);
        baseMood = Mathf.Clamp(currentMood - factorTotal, 0f, 100f);
        lastCalculatedMood = Mathf.Clamp(currentMood, 0f, 100f);
    }

    public CharacterMoodRecalculation Recalculate(
        Dictionary<CharacterCondition, float> stats,
        List<CharacterMoodMemory> factors,
        ref float baseMood,
        ref float lastCalculatedMood,
        bool adoptExternalOverride)
    {
        if (adoptExternalOverride)
        {
            SynchronizeExternalOverride(
                stats,
                factors,
                ref baseMood,
                ref lastCalculatedMood);
        }

        float now = Time;
        bool expired = CharacterMoodRuntimeRules.PruneExpired(factors, now);
        float nextMood = CharacterMoodRuntimeRules.CalculateValue(
            stats,
            factors,
            baseMood,
            now,
            needDefinitions);
        float previous = stats.TryGetValue(
                CharacterCondition.MOOD,
                out float current)
            ? current
            : nextMood;
        stats[CharacterCondition.MOOD] = nextMood;
        lastCalculatedMood = nextMood;
        return new CharacterMoodRecalculation(
            expired,
            previous,
            nextMood,
            BuildSnapshot(stats, factors, baseMood, now));
    }

    public bool PruneExpired(
        List<CharacterMoodMemory> factors,
        float now) => CharacterMoodRuntimeRules.PruneExpired(factors, now);

    public CharacterMoodSnapshot BuildSnapshot(
        IReadOnlyDictionary<CharacterCondition, float> stats,
        IReadOnlyList<CharacterMoodMemory> factors,
        float baseMood,
        float now) => CharacterMoodRuntimeRules.BuildSnapshot(
            stats,
            factors,
            baseMood,
            now,
            needDefinitions);
}
