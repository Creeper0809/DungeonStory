using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
internal sealed class CharacterAiFailureMemory
{
    [SerializeField, ReadOnly] private Dictionary<BuildableObject, float> facilityCooldowns =
        new Dictionary<BuildableObject, float>();
    [SerializeField, ReadOnly] private Dictionary<string, int> recentFailureCounts =
        new Dictionary<string, int>();
    [NonSerialized] private IReadOnlyDictionary<string, int> recentFailureCountsView;

    public IReadOnlyDictionary<string, int> RecentFailureCounts
    {
        get
        {
            EnsureInitialized();
            return recentFailureCountsView;
        }
    }

    public void EnsureInitialized()
    {
        facilityCooldowns ??= new Dictionary<BuildableObject, float>();
        recentFailureCounts ??= new Dictionary<string, int>();
        recentFailureCountsView ??= ReadOnlyView.Dictionary(recentFailureCounts);
    }

    public void Record(
        AIActionFailure failure,
        BuildableObject target,
        float now,
        float cooldownSeconds)
    {
        EnsureInitialized();
        string key = failure.Kind != AIActionFailureKind.Unknown
            ? failure.Kind.ToString()
            : failure.ToString();
        recentFailureCounts[key] = recentFailureCounts.TryGetValue(key, out int count)
            ? count + 1
            : 1;

        if (target != null && ShouldCooldownFacility(failure.Kind))
        {
            PutFacilityOnCooldown(target, now, cooldownSeconds);
        }
    }

    public int GetRecentFailureCount(AIActionFailureKind kind)
    {
        EnsureInitialized();
        return recentFailureCounts.TryGetValue(kind.ToString(), out int count)
            ? count
            : 0;
    }

    public bool IsFacilityCoolingDown(
        BuildableObject building,
        float now,
        out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (building == null)
        {
            return false;
        }

        PruneFacilityCooldowns(now);
        if (!facilityCooldowns.TryGetValue(building, out float until))
        {
            return false;
        }

        remainingSeconds = Mathf.Max(0f, until - now);
        return remainingSeconds > 0f;
    }

    public void PutFacilityOnCooldown(
        BuildableObject building,
        float now,
        float cooldownSeconds)
    {
        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        EnsureInitialized();
        facilityCooldowns[building] = now + Mathf.Max(0.1f, cooldownSeconds);
    }

    public string BuildCooldownSummary(float now)
    {
        EnsureInitialized();
        PruneFacilityCooldowns(now);
        List<string> summaries = null;
        foreach (KeyValuePair<BuildableObject, float> pair in facilityCooldowns)
        {
            if (pair.Key == null || now >= pair.Value)
            {
                continue;
            }

            summaries ??= new List<string>();
            summaries.Add($"{GetBuildingLabel(pair.Key)} {pair.Value - now:0.0}s");
        }

        return summaries == null ? string.Empty : string.Join(", ", summaries);
    }

    public void PruneFacilityCooldowns(float now)
    {
        EnsureInitialized();
        if (facilityCooldowns.Count == 0)
        {
            return;
        }

        List<BuildableObject> expired = null;
        foreach (KeyValuePair<BuildableObject, float> pair in facilityCooldowns)
        {
            if (pair.Key != null && !pair.Key.isDestroy && now < pair.Value)
            {
                continue;
            }

            expired ??= new List<BuildableObject>();
            expired.Add(pair.Key);
        }

        if (expired == null)
        {
            return;
        }

        foreach (BuildableObject building in expired)
        {
            facilityCooldowns.Remove(building);
        }
    }

    private static bool ShouldCooldownFacility(AIActionFailureKind kind)
    {
        return kind == AIActionFailureKind.DestinationOccupied
            || kind == AIActionFailureKind.NoDestination
            || kind == AIActionFailureKind.DestinationSelectionFailed
            || kind == AIActionFailureKind.NoPath
            || kind == AIActionFailureKind.Destroyed;
    }

    private static string GetBuildingLabel(BuildableObject building)
    {
        if (building == null)
        {
            return "None";
        }

        return building.BuildingData != null
            && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
                ? building.BuildingData.objectName
                : building.name;
    }
}
