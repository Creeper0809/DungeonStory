using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class InvasionFacilityDamageResolver
{
    public static bool TryFindDamageTarget(
        Grid grid,
        Vector2Int current,
        out BuildableObject target)
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
        ISet<BuildingInstanceId> excludedBuildingIds = null)
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
        List<BuildableObject> candidates = new();
        foreach (Vector2Int position in positions)
        {
            GridCell cell = grid.GetGridCell(position);
            if (cell == null)
            {
                continue;
            }

            foreach (BuildableObject building in cell.GetAllOccupants()
                         .OfType<BuildableObject>())
            {
                if (!candidates.Contains(building))
                {
                    candidates.Add(building);
                }
            }
        }

        InvasionFacilityTargetRuntimeProjection projection =
            InvasionFacilityTargetRuntimeProjection.Capture(candidates);
        BuildingInstanceId preferredTargetId = preferredTarget != null
            && candidates.Contains(preferredTarget)
            && IsDamageableFacility(preferredTarget)
            && !IsExcluded(preferredTarget, excludedBuildingIds)
                ? preferredTarget.RequirePersistentInstanceId()
                : default;
        InvasionFacilityTargetSelectionSnapshot selection = new(
            projection.Snapshots,
            preference,
            preferredTargetId,
            excludedBuildingIds);
        return InvasionFacilityDamageSelectionRules.TrySelectDamageTarget(
                selection,
                out InvasionIntruderFacilityTargetSnapshot selected)
            && projection.TryResolve(selected.TargetId, out target);
    }

    public static bool TryFindPriorityTarget(
        GridPathSearchResult searchResult,
        InvasionIntruderTargetPreference preference,
        out BuildableObject target,
        ISet<BuildingInstanceId> excludedBuildingIds = null)
    {
        target = null;
        if (searchResult == null
            || preference == InvasionIntruderTargetPreference.Owner)
        {
            return false;
        }

        InvasionFacilityTargetRuntimeProjection projection =
            InvasionFacilityTargetRuntimeProjection.Capture(
                searchResult.GetAllReachableOccupants()
                    .OfType<BuildableObject>(),
                searchResult.GetMoveCostTo);
        InvasionFacilityTargetSelectionSnapshot selection = new(
            projection.Snapshots,
            preference,
            default,
            excludedBuildingIds);
        return InvasionFacilityDamageSelectionRules.TrySelectPriorityTarget(
                selection,
                out InvasionIntruderFacilityTargetSnapshot selected)
            && projection.TryResolve(selected.TargetId, out target);
    }

    public static bool IsDamageableFacility(BuildableObject building)
    {
        return InvasionFacilityTargetRuntimeProjection
            .IsDamageableFacility(building);
    }

    private static bool IsExcluded(
        BuildableObject building,
        ISet<BuildingInstanceId> excludedBuildingIds)
    {
        return building != null
            && excludedBuildingIds != null
            && excludedBuildingIds.Contains(
                building.RequirePersistentInstanceId());
    }
}
