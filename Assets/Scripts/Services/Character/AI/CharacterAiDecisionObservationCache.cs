using System.Collections.Generic;
using UnityEngine;

internal sealed class CharacterAiDecisionObservationCache
{
    private readonly Dictionary<(BuildableObject Building, FacilityRole Role), float> facilityScoreCache =
        new Dictionary<(BuildableObject Building, FacilityRole Role), float>();
    private readonly Dictionary<CharacterAiBranch, CharacterAiWorldSignalSnapshot> worldSignalCache =
        new Dictionary<CharacterAiBranch, CharacterAiWorldSignalSnapshot>();
    private Vector2Int worldSignalCachePosition;
    private bool hasWorldSignalCachePosition;
    private bool hasCachedFacilityCrowdSensitivity;
    private float cachedFacilityCrowdSensitivity = 1f;

    internal bool TryGetCachedFacilityScore(
        BuildableObject building,
        FacilityRole role,
        out float score)
    {
        return facilityScoreCache.TryGetValue((building, role), out score);
    }

    internal void CacheFacilityScore(
        BuildableObject building,
        FacilityRole role,
        float score)
    {
        if (building != null)
        {
            facilityScoreCache[(building, role)] = Mathf.Clamp01(score);
        }
    }

    internal float GetFacilityCrowdSensitivity(CharacterActor sourceActor)
    {
        if (hasCachedFacilityCrowdSensitivity)
        {
            return cachedFacilityCrowdSensitivity;
        }

        cachedFacilityCrowdSensitivity = sourceActor?.Stats != null
            ? sourceActor.Stats.GetCrowdSensitivityMultiplier()
            : 1f;
        hasCachedFacilityCrowdSensitivity = true;
        return cachedFacilityCrowdSensitivity;
    }

    internal bool TryGetCachedWorldSignal(
        CharacterAiBranch branch,
        Vector2Int actorPosition,
        out CharacterAiWorldSignalSnapshot snapshot)
    {
        if (!hasWorldSignalCachePosition
            || worldSignalCachePosition != actorPosition)
        {
            snapshot = default;
            return false;
        }

        return worldSignalCache.TryGetValue(branch, out snapshot);
    }

    internal void CacheWorldSignal(
        CharacterAiBranch branch,
        Vector2Int actorPosition,
        CharacterAiWorldSignalSnapshot snapshot)
    {
        if (!hasWorldSignalCachePosition
            || worldSignalCachePosition != actorPosition)
        {
            worldSignalCache.Clear();
            worldSignalCachePosition = actorPosition;
            hasWorldSignalCachePosition = true;
        }

        worldSignalCache[branch] = snapshot;
    }

    internal void ClearWorldSignalCache()
    {
        worldSignalCache.Clear();
        hasWorldSignalCachePosition = false;
    }

    internal void ClearFacilityScores() => facilityScoreCache.Clear();
    internal void InvalidateCrowdSensitivity() => hasCachedFacilityCrowdSensitivity = false;
}
