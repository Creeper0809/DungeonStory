using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class CharacterSpawnerSceneApplicationAdapter
{
    private const string CatalogMerged = "catalog-merged";
    private const string RuntimeInitialized = "runtime-initialized";
    private const string SpawnRoutineStarted = "spawn-routine-started";

    private readonly HashSet<string> flags = new(StringComparer.Ordinal);
    private readonly Dictionary<int, CharacterSO> charactersById = new();
    private readonly Dictionary<int, Door> entranceByBuildingVersion = new();

    public bool IsRuntimeInitialized => flags.Contains(RuntimeInitialized);

    public bool BeginCatalogMerge() => flags.Add(CatalogMerged);

    public bool BeginRuntimeInitialization() => flags.Add(RuntimeInitialized);

    public bool BeginSpawnRoutine() => flags.Add(SpawnRoutineStarted);

    public void ResetInjectedProjection()
    {
        flags.Remove(CatalogMerged);
        flags.Remove(RuntimeInitialized);
        charactersById.Clear();
        entranceByBuildingVersion.Clear();
    }

    public void RebuildCharacterIndex(IEnumerable<CharacterSO> characters)
    {
        charactersById.Clear();
        foreach (IGrouping<int, CharacterSO> group in
                 (characters ?? Array.Empty<CharacterSO>())
                 .Where(value => value != null)
                 .GroupBy(value => value.id))
        {
            charactersById[group.Key] = group.First();
        }
    }

    public bool TryGetCharacter(int id, out CharacterSO character) =>
        charactersById.TryGetValue(id, out character);

    public bool TryResolveEntrance(
        Grid grid,
        IBuildingWorldQuery buildingWorld,
        Vector2Int preferredInsidePosition,
        out Door entrance)
    {
        entrance = null;
        if (grid == null)
        {
            return false;
        }

        if (buildingWorld == null)
        {
            entrance = ResolveFromGrid(grid, preferredInsidePosition);
            return entrance != null;
        }

        int version = buildingWorld.BuildingVersion;
        if (entranceByBuildingVersion.TryGetValue(version, out entrance)
            && entrance != null
            && !entrance.isDestroy)
        {
            return true;
        }

        entrance = buildingWorld.Buildings
            .OfType<Door>()
            .Where(IsEntrance)
            .OrderBy(door => Distance(door.centerPos, preferredInsidePosition))
            .FirstOrDefault();
        entranceByBuildingVersion.Clear();
        if (entrance != null)
        {
            entranceByBuildingVersion[version] = entrance;
        }

        return entrance != null;
    }

    private static Door ResolveFromGrid(Grid grid, Vector2Int preferredInsidePosition)
    {
        return grid.GetCells()
            .Select(cell => cell?.GetBuildingInlayer(GridLayer.Building))
            .OfType<Door>()
            .Where(IsEntrance)
            .Distinct()
            .OrderBy(door => Distance(door.centerPos, preferredInsidePosition))
            .FirstOrDefault();
    }

    private static bool IsEntrance(Door door) =>
        door != null
        && door.IsDungeonEntrance
        && !door.isDestroy
        && door.BuildingData != null
        && !door.BuildingData.IsInteriorDoor;

    private static int Distance(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
}
