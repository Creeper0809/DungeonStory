using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public static class InvasionIntruderPlanner
{
    public static float CalculateFocus(
        float elapsedSeconds,
        float secondsToFullFocus)
    {
        return InvasionIntruderPlanningRules.CalculateFocus(
            elapsedSeconds,
            secondsToFullFocus);
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
        ISet<BuildingInstanceId> excludedFacilityIds = null,
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

        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }
        if (!pathSearchBroker.TryGetSearch(
                grid,
                start,
                out GridPathSearchResult searchResult))
        {
            return new Queue<GridMoveStep>();
        }

        InvasionIntruderPathRuntimeSnapshot runtimeSnapshot =
            InvasionIntruderPathRuntimeSnapshot.Capture(grid, searchResult);
        InvasionIntruderRoutePlan plan = InvasionIntruderPlanningRules.Plan(
            runtimeSnapshot.Search,
            ownerPosition,
            focus,
            randomStream,
            pattern,
            excludedFacilityIds,
            damagedFacilityCount);
        directPath = plan.DirectPath;
        switch (plan.TargetKind)
        {
            case InvasionIntruderRouteTargetKind.Facility:
                if (runtimeSnapshot.TryResolve(
                        plan.PriorityTargetId,
                        out priorityTarget))
                {
                    return searchResult.GetMovePathTo(priorityTarget);
                }
                return new Queue<GridMoveStep>();

            case InvasionIntruderRouteTargetKind.Owner:
            case InvasionIntruderRouteTargetKind.Explore:
                return searchResult.GetMovePathTo(plan.Destination);

            default:
                return new Queue<GridMoveStep>();
        }
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

        return InvasionIntruderPlanningRules.SelectExploreTarget(
            searchResult.GetReachablePositions()
                .Where(position => grid.IsWalkable(position)),
            searchResult.start,
            ownerPosition,
            focus,
            randomStream);
    }

    public static bool IsAtOwner(
        Grid grid,
        CharacterActor intruder,
        CharacterActor owner)
    {
        if (grid == null || intruder == null || owner == null)
        {
            return false;
        }

        return InvasionIntruderPlanningRules.IsAtOwner(
            grid.GetXY(intruder.transform.position),
            grid.GetXY(owner.transform.position));
    }
}

internal sealed class InvasionIntruderPathRuntimeSnapshot
{
    private readonly InvasionFacilityTargetRuntimeProjection targets;

    private InvasionIntruderPathRuntimeSnapshot(
        InvasionIntruderPathSearchSnapshot search,
        InvasionFacilityTargetRuntimeProjection targets)
    {
        Search = search ?? throw new ArgumentNullException(nameof(search));
        this.targets = targets
            ?? throw new ArgumentNullException(nameof(targets));
    }

    public InvasionIntruderPathSearchSnapshot Search { get; }

    public static InvasionIntruderPathRuntimeSnapshot Capture(
        Grid grid,
        GridPathSearchResult searchResult)
    {
        if (grid == null)
        {
            throw new ArgumentNullException(nameof(grid));
        }
        if (searchResult == null)
        {
            throw new ArgumentNullException(nameof(searchResult));
        }

        InvasionFacilityTargetRuntimeProjection targets =
            InvasionFacilityTargetRuntimeProjection.Capture(
                searchResult.GetAllReachableOccupants()
                    .OfType<BuildableObject>(),
                searchResult.GetMoveCostTo);

        InvasionIntruderPathSearchSnapshot search = new(
            searchResult.start,
            searchResult.GetReachablePositions()
                .Where(position => grid.IsWalkable(position)),
            targets.Snapshots);
        return new InvasionIntruderPathRuntimeSnapshot(search, targets);
    }

    public bool TryResolve(
        BuildingInstanceId targetId,
        out BuildableObject target)
    {
        return targets.TryResolve(targetId, out target);
    }
}
