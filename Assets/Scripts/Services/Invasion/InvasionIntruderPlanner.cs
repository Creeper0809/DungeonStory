using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public static class InvasionIntruderPlanner
{
    public static float CalculateFocus(float elapsedSeconds, float secondsToFullFocus)
    {
        return Mathf.Clamp01(elapsedSeconds / Mathf.Max(0.1f, secondsToFullFocus));
    }

    public static Queue<GridMoveStep> GetNextPath(
        Grid grid,
        Vector2Int start,
        Vector2Int ownerPosition,
        float focus,
        IGridPathSearchBroker pathSearchBroker,
        IRandomStream randomStream,
        out bool directPath)
    {
        return GetNextPath(
            grid,
            start,
            ownerPosition,
            focus,
            pathSearchBroker,
            randomStream,
            InvasionIntruderPatternCatalog.Default,
            out directPath,
            out _);
    }

    public static Queue<GridMoveStep> GetNextPath(
        Grid grid,
        Vector2Int start,
        Vector2Int ownerPosition,
        float focus,
        IGridPathSearchBroker pathSearchBroker,
        IRandomStream randomStream,
        InvasionIntruderPatternDefinition pattern,
        out bool directPath,
        out BuildableObject priorityTarget,
        ISet<int> excludedFacilityInstanceIds = null,
        int damagedFacilityCount = 0)
    {
        directPath = false;
        priorityTarget = null;
        if (grid == null
            || pathSearchBroker == null
            || !grid.IsValidGridPos(start)
            || !grid.IsValidGridPos(ownerPosition))
        {
            return new Queue<GridMoveStep>();
        }

        if (start == ownerPosition)
        {
            directPath = true;
            return new Queue<GridMoveStep>();
        }

        pattern ??= InvasionIntruderPatternCatalog.Default;
        if (!pathSearchBroker.TryGetSearch(grid, start, out GridPathSearchResult searchResult))
        {
            return new Queue<GridMoveStep>();
        }
        if (damagedFacilityCount < pattern.maxFacilityDamageCount
            && focus < pattern.facilityDiversionFocus
            && InvasionFacilityDamageResolver.TryFindPriorityTarget(
                searchResult,
                pattern.targetPreference,
                out priorityTarget,
                excludedFacilityInstanceIds))
        {
            return searchResult.GetMovePathTo(priorityTarget);
        }

        if (focus >= pattern.directOwnerFocus)
        {
            directPath = true;
            return searchResult.GetMovePath((pos) => pos == ownerPosition);
        }

        Vector2Int exploreTarget = SelectExploreTarget(
            grid,
            searchResult,
            ownerPosition,
            focus,
            randomStream);
        if (exploreTarget == start)
        {
            directPath = true;
            return searchResult.GetMovePath((pos) => pos == ownerPosition);
        }

        return searchResult.GetMovePath((pos) => pos == exploreTarget);
    }

    public static Vector2Int SelectExploreTarget(
        Grid grid,
        GridPathSearchResult searchResult,
        Vector2Int ownerPosition,
        float focus,
        IRandomStream randomStream)
    {
        if (grid == null || searchResult == null)
        {
            return Vector2Int.zero;
        }

        if (randomStream == null)
        {
            throw new System.ArgumentNullException(nameof(randomStream));
        }

        List<Vector2Int> candidates = searchResult.GetReachablePositions()
            .Where((pos) => pos != searchResult.start && grid.IsWalkable(pos))
            .ToList();

        if (candidates.Count == 0)
        {
            return searchResult.start;
        }

        if (candidates.Count > 1)
        {
            candidates.Remove(ownerPosition);
        }

        if (candidates.Count == 0)
        {
            return searchResult.start;
        }

        int maxDistance = Mathf.Max(1, candidates.Max((pos) => Manhattan(pos, ownerPosition)));
        float clampedFocus = Mathf.Clamp01(focus);

        return candidates
            .OrderByDescending((pos) =>
            {
                float closeness = 1f - ((float)Manhattan(pos, ownerPosition) / maxDistance);
                float explorationNoise = randomStream.NextFloat();
                return Mathf.Lerp(explorationNoise, closeness, clampedFocus);
            })
            .First();
    }

    public static bool IsAtOwner(Grid grid, CharacterActor intruder, CharacterActor owner)
    {
        if (grid == null || intruder == null || owner == null)
        {
            return false;
        }

        return grid.GetXY(intruder.transform.position) == grid.GetXY(owner.transform.position);
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
