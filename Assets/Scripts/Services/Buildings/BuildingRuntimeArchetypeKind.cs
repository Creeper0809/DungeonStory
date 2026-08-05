using System;
using UnityEngine;

public enum BuildingRuntimeArchetypeKind
{
    Generic = 0,
    Facility = 1,
    Shop = 2,
    Door = 3,
    InteriorDoor = 4,
    Hallway = 5,
    Stair = 6,
    DefenseFacility = 7
}

public static class BuildingRuntimeArchetypeKindExtensions
{
    public static bool IsDefined(this BuildingRuntimeArchetypeKind value)
    {
        return value >= BuildingRuntimeArchetypeKind.Generic
            && value <= BuildingRuntimeArchetypeKind.DefenseFacility;
    }

    public static BuildingRuntimeArchetypeKind FromComponentType(Type type)
    {
        if (type == typeof(BuildableObject)) return BuildingRuntimeArchetypeKind.Generic;
        if (type == typeof(Facility)) return BuildingRuntimeArchetypeKind.Facility;
        if (type == typeof(Shop)) return BuildingRuntimeArchetypeKind.Shop;
        if (type == typeof(Door)) return BuildingRuntimeArchetypeKind.Door;
        if (type == typeof(InteriorDoor)) return BuildingRuntimeArchetypeKind.InteriorDoor;
        if (type == typeof(Hallway)) return BuildingRuntimeArchetypeKind.Hallway;
        if (type == typeof(Stair)) return BuildingRuntimeArchetypeKind.Stair;
        if (type == typeof(DefenseFacility)) return BuildingRuntimeArchetypeKind.DefenseFacility;
        throw new ArgumentOutOfRangeException(
            nameof(type),
            type,
            "The component type has no authored building runtime archetype.");
    }

    public static Type ToComponentType(this BuildingRuntimeArchetypeKind value)
    {
        return value switch
        {
            BuildingRuntimeArchetypeKind.Generic => typeof(BuildableObject),
            BuildingRuntimeArchetypeKind.Facility => typeof(Facility),
            BuildingRuntimeArchetypeKind.Shop => typeof(Shop),
            BuildingRuntimeArchetypeKind.Door => typeof(Door),
            BuildingRuntimeArchetypeKind.InteriorDoor => typeof(InteriorDoor),
            BuildingRuntimeArchetypeKind.Hallway => typeof(Hallway),
            BuildingRuntimeArchetypeKind.Stair => typeof(Stair),
            BuildingRuntimeArchetypeKind.DefenseFacility => typeof(DefenseFacility),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public static BuildableObject AddComponent(
        this BuildingRuntimeArchetypeKind value,
        GameObject target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        return value switch
        {
            BuildingRuntimeArchetypeKind.Generic => target.AddComponent<BuildableObject>(),
            BuildingRuntimeArchetypeKind.Facility => target.AddComponent<Facility>(),
            BuildingRuntimeArchetypeKind.Shop => target.AddComponent<Shop>(),
            BuildingRuntimeArchetypeKind.Door => target.AddComponent<Door>(),
            BuildingRuntimeArchetypeKind.InteriorDoor => target.AddComponent<InteriorDoor>(),
            BuildingRuntimeArchetypeKind.Hallway => target.AddComponent<Hallway>(),
            BuildingRuntimeArchetypeKind.Stair => target.AddComponent<Stair>(),
            BuildingRuntimeArchetypeKind.DefenseFacility => target.AddComponent<DefenseFacility>(),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }
}
