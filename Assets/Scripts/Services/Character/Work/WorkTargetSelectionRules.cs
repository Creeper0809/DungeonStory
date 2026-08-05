using System.Collections.Generic;
using UnityEngine;

internal static class WorkTargetSelectionRules
{
    public static CharacterAiIntentionType GetIntention(FacilityWorkType workType)
    {
        return workType == FacilityWorkType.Rescue
            || workType == FacilityWorkType.Haul
            || workType == FacilityWorkType.Restock
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

        if (building.Grid == searchResult.sourceGrid)
        {
            IReadOnlyList<Vector2Int> positions = building.buildPoses;
            for (int index = 0; index < positions.Count; index++)
            {
                if (searchResult.ContainsPosition(positions[index]))
                {
                    return true;
                }
            }
        }

        return building is ExteriorZoneMarker marker
            && searchResult.ContainsPosition(marker.GridPosition);
    }
}
