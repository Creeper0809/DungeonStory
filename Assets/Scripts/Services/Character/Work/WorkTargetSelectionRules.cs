using System.Collections.Generic;
using UnityEngine;

public static class WorkTargetSelectionRules
{
    public static CharacterAiIntentionType GetIntention(WorkTypeId workTypeId)
    {
        return workTypeId == BuiltInWorkTypeIds.Rescue
            || workTypeId == BuiltInWorkTypeIds.Haul
            || workTypeId == BuiltInWorkTypeIds.Restock
                ? CharacterAiIntentionType.Survive
                : CharacterAiIntentionType.Work;
    }

    public static bool IsExteriorWorkType(FacilityWorkType workType)
    {
        return workType == FacilityWorkType.Haul
            || workType == FacilityWorkType.Hunt
            || workType == FacilityWorkType.Guard
            || workType == FacilityWorkType.Reception
            || workType == FacilityWorkType.Clean
            || workType == FacilityWorkType.Repair;
    }

    public static string GetBuildingLabel(BuildableObject building)
    {
        if (building == null)
        {
            return "시설 없음";
        }

        return building.BuildingData != null
            && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
                ? building.BuildingData.objectName
                : building.name;
    }

    public static bool CanUseSuppressFor(FacilityWorkType requestedWorkType)
    {
        return requestedWorkType == FacilityWorkType.None
            || requestedWorkType == FacilityWorkType.Guard;
    }

    public static bool IsReachable(
        BuildableObject building,
        GridPathSearchResult searchResult)
    {
        if (building == null || searchResult == null)
        {
            return false;
        }

        if (searchResult.ContainsVisitableOccupant(building))
        {
            return true;
        }

        if (TryGetReachableWorkAccessPosition(
                building,
                searchResult,
                out _))
        {
            return true;
        }

        return building is ExteriorZoneMarker marker
            && searchResult.ContainsPosition(marker.GridPosition);
    }

    /// <summary>
    /// Resolves an actor-reachable work stand without granting visitor access.
    /// The supplied search already owns the actor's door/traversal context, so
    /// a roles=None workstation can be worked from an adjacent walkable cell
    /// while remaining unavailable to customer/guest admission.
    /// </summary>
    public static bool TryGetReachableWorkAccessPosition(
        BuildableObject building,
        GridPathSearchResult searchResult,
        out Vector2Int result)
    {
        result = default;
        if (building == null
            || building.isDestroy
            || searchResult == null
            || building.Grid == null
            || building.Grid != searchResult.sourceGrid)
        {
            return false;
        }

        Grid grid = searchResult.sourceGrid;
        IReadOnlyList<Vector2Int> footprint = building.buildPoses;
        if (footprint == null || footprint.Count == 0)
        {
            return false;
        }

        bool walkThroughTarget = building.BuildingData?.IsGridMovement == true;
        bool found = false;
        int bestCost = int.MaxValue;
        Vector2Int best = default;
        for (int index = 0; index < footprint.Count; index++)
        {
            Vector2Int occupied = footprint[index];
            if (walkThroughTarget)
            {
                Consider(occupied);
            }
            else
            {
                // Dungeon Y is a floor, not a north/south tile. Workstations
                // are therefore operated from either horizontal edge.
                Consider(occupied + Vector2Int.left);
                Consider(occupied + Vector2Int.right);
            }
        }

        result = best;
        return found;

        void Consider(Vector2Int candidate)
        {
            if (!grid.IsValidGridPos(candidate)
                || !grid.IsWalkable(candidate)
                || !searchResult.ContainsPosition(candidate)
                || !building.IsWorkAccessGridPosition(grid, candidate))
            {
                return;
            }

            int cost = searchResult.GetMoveCostTo(candidate);
            if (cost == int.MaxValue
                || found && (cost > bestCost
                    || cost == bestCost && Compare(candidate, best) >= 0))
            {
                return;
            }

            found = true;
            bestCost = cost;
            best = candidate;
        }
    }

    private static int Compare(Vector2Int left, Vector2Int right)
    {
        int y = left.y.CompareTo(right.y);
        return y != 0 ? y : left.x.CompareTo(right.x);
    }
}
