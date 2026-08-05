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

        for (int index = 0; index < abilities.Count; index++)
        {
            if (abilities[index] is IBuildingExteriorWorkRuntimeAbility ability
                && ability.SupportsExteriorWork(workTypeId))
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
        for (int index = 0; index < abilities.Count; index++)
        {
            if (abilities[index] is IBuildingExteriorWorkRuntimeAbility ability
                && ability.SupportsExteriorWork(workTypeId)
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
        for (int index = 0; index < abilities.Count; index++)
        {
            if (abilities[index] is IBuildingExteriorWorkRuntimeAbility ability
                && ability.SupportsExteriorWork(workTypeId)
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
