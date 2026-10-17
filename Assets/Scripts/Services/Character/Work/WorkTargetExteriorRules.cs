using System.Collections.Generic;
using UnityEngine;

internal static class WorkTargetExteriorRules
{
    public static bool HasRuntime(BuildableObject building, WorkTypeId workTypeId)
    {
        IReadOnlyList<BuildingAbility> abilities = building?.BuildingData?.Abilities;
        if (abilities == null)
        {
            return false;
        }

        foreach (IBuildingExteriorWorkRuntimeAbility ability in building.GetRuntimeAbilities<IBuildingExteriorWorkRuntimeAbility>())
        {
            if (ability.SupportsExteriorWork(workTypeId))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsAvailable(
        BuildableObject building,
        CharacterActor actor,
        WorkTypeId workTypeId)
    {
        IReadOnlyList<BuildingAbility> abilities = building?.BuildingData?.Abilities;
        if (abilities == null)
        {
            return false;
        }

        IBuildingVisitorPort visitor = actor?.BuildingVisitor;
        foreach (IBuildingExteriorWorkRuntimeAbility ability in building.GetRuntimeAbilities<IBuildingExteriorWorkRuntimeAbility>())
        {
            if (ability.SupportsExteriorWork(workTypeId)
                && ability.IsExteriorWorkAvailable(visitor, building, workTypeId))
            {
                return true;
            }
        }

        return false;
    }

    public static float GetUrgency(
        BuildableObject building,
        CharacterActor actor,
        WorkTypeId workTypeId)
    {
        IReadOnlyList<BuildingAbility> abilities = building?.BuildingData?.Abilities;
        if (abilities == null)
        {
            return 0f;
        }

        IBuildingVisitorPort visitor = actor?.BuildingVisitor;
        float urgency = 0f;
        foreach (IBuildingExteriorWorkRuntimeAbility ability in building.GetRuntimeAbilities<IBuildingExteriorWorkRuntimeAbility>())
        {
            if (ability.SupportsExteriorWork(workTypeId)
                && ability.IsExteriorWorkAvailable(visitor, building, workTypeId))
            {
                urgency += Mathf.Max(
                    0f,
                    ability.GetExteriorWorkUrgency(visitor, building, workTypeId));
            }
        }

        return urgency;
    }
}
