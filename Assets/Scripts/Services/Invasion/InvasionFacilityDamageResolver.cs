using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class InvasionFacilityDamageResolver
{
    public static bool TryFindDamageTarget(Grid grid, Vector2Int current, out BuildableObject target)
    {
        return TryFindDamageTarget(
            grid,
            current,
            InvasionIntruderTargetPreference.Owner,
            null,
            out target,
            null);
    }

    public static bool TryFindDamageTarget(
        Grid grid,
        Vector2Int current,
        InvasionIntruderTargetPreference preference,
        BuildableObject preferredTarget,
        out BuildableObject target,
        ISet<int> excludedInstanceIds = null)
    {
        target = null;
        if (grid == null)
        {
            return false;
        }

        Vector2Int[] positions =
        {
            current,
            current + Vector2Int.left,
            current + Vector2Int.right
        };

        List<BuildableObject> candidates = new List<BuildableObject>();
        foreach (Vector2Int position in positions)
        {
            GridCell cell = grid.GetGridCell(position);
            if (cell == null)
            {
                continue;
            }

            foreach (BuildableObject building in cell.GetAllOccupants()
                         .OfType<BuildableObject>()
                         .Where(IsDamageableFacility)
                         .Where(building => !IsExcluded(building, excludedInstanceIds)))
            {
                if (!candidates.Contains(building))
                {
                    candidates.Add(building);
                }
            }
        }

        if (preferredTarget != null && candidates.Contains(preferredTarget))
        {
            target = preferredTarget;
            return true;
        }

        target = candidates.FirstOrDefault(candidate => MatchesPreference(candidate, preference));
        if (target == null && preference == InvasionIntruderTargetPreference.Owner)
        {
            target = candidates.FirstOrDefault();
        }

        return target != null;
    }

    public static bool TryFindPriorityTarget(
        GridPathSearchResult searchResult,
        InvasionIntruderTargetPreference preference,
        out BuildableObject target,
        ISet<int> excludedInstanceIds = null)
    {
        target = null;
        if (searchResult == null || preference == InvasionIntruderTargetPreference.Owner)
        {
            return false;
        }

        IEnumerable<BuildableObject> candidates = searchResult.GetAllReachableOccupants()
            .OfType<BuildableObject>()
            .Where(IsDamageableFacility)
            .Where(candidate => !IsExcluded(candidate, excludedInstanceIds))
            .Where(candidate => MatchesPreference(candidate, preference))
            .Distinct();

        target = preference == InvasionIntruderTargetPreference.ValuableFacility
            ? candidates
                .OrderByDescending(candidate => candidate.GetConstructionCost())
                .ThenBy(candidate => searchResult.GetMoveDistanceTo(candidate))
                .FirstOrDefault()
            : candidates
                .OrderBy(candidate => searchResult.GetMoveDistanceTo(candidate))
                .ThenByDescending(candidate => candidate.GetConstructionCost())
                .FirstOrDefault();
        return target != null;
    }

    public static bool IsDamageableFacility(BuildableObject building)
    {
        return building != null
            && !building.isDestroy
            && !building.IsDamaged
            && !building.IsGridMovement
            && building.Facility != null;
    }

    private static bool MatchesPreference(
        BuildableObject building,
        InvasionIntruderTargetPreference preference)
    {
        bool isDefense = building?.BuildingData?.Defense?.IsDefenseFacility == true;
        return preference switch
        {
            InvasionIntruderTargetPreference.DefenseFacility => isDefense,
            InvasionIntruderTargetPreference.ValuableFacility => !isDefense,
            _ => true
        };
    }

    private static bool IsExcluded(BuildableObject building, ISet<int> excludedInstanceIds)
    {
        return building != null
            && excludedInstanceIds != null
            && excludedInstanceIds.Contains(building.GetInstanceID());
    }
}
