using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class WildlifeWorldRuntime
{
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameClock gameClock;
    private readonly IRandomStreamProvider randomStreamProvider;
    private readonly IDoorAccessQuery doorAccessQuery;

    public WildlifeWorldRuntime(
        WildlifeWorldServices world,
        WildlifeExecutionServices execution)
    {
        WildlifeWorldServices requiredWorld = world
            ?? throw new ArgumentNullException(nameof(world));
        WildlifeExecutionServices requiredExecution = execution
            ?? throw new ArgumentNullException(nameof(execution));
        pathSearchBroker = requiredWorld.PathSearch;
        worldRegistry = requiredWorld.WorldRegistry;
        gameClock = requiredExecution.Clock;
        randomStreamProvider = requiredExecution.RandomStreams;
        doorAccessQuery = requiredExecution.Doors;
    }

    public WildlifeActor CreateActor(
        Grid grid,
        WildlifeSpeciesDefinition species,
        Vector2Int position,
        string wildlifeId,
        WildlifeSaveData saveData,
        bool detachedRestore)
    {
        GameObject gameObject = new GameObject("Wildlife");
        WildlifeActor actor = null;
        try
        {
            if (detachedRestore)
            {
                gameObject.SetActive(false);
            }

            DungeonRuntimeHierarchy.Parent(
                gameObject,
                DungeonRuntimeHierarchy.Wildlife);
            actor = gameObject.AddComponent<WildlifeActor>();
            if (detachedRestore)
            {
                actor.PrepareForDetachedRestore();
            }

            actor.ConfigureRuntimeServices(
                pathSearchBroker,
                worldRegistry,
                gameClock,
                randomStreamProvider,
                doorAccessQuery);
            actor.Initialize(grid, species, wildlifeId, position, saveData);
            return actor;
        }
        catch
        {
            if (actor != null && actor.IsDetachedRestoreCandidate)
            {
                actor.DiscardDetachedRestore();
            }
            else if (actor != null)
            {
                DestroyActor(actor);
            }
            else if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            throw;
        }
    }

    public void DestroyActor(WildlifeActor actor)
    {
        if (actor == null)
        {
            return;
        }

        actor.PrepareForDespawn();
        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(actor.gameObject);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(actor.gameObject);
        }
    }

    public void DiscardCandidateActors(WildlifePopulationState state)
    {
        if (state == null)
        {
            return;
        }

        foreach (WildlifeActor actor in state.Actors)
        {
            if (actor == null)
            {
                continue;
            }

            if (actor.IsDetachedRestoreCandidate)
            {
                actor.DiscardDetachedRestore();
            }
            else
            {
                DestroyActor(actor);
            }
        }

        ClearPopulationCollections(state);
    }

    public void DestroyPopulationActors(WildlifePopulationState state)
    {
        if (state == null)
        {
            return;
        }

        foreach (WildlifeActor actor in state.Actors)
        {
            DestroyActor(actor);
        }

        ClearPopulationCollections(state);
    }

    public IEnumerable<Vector2Int> GetInitialSpawnCandidates(Grid grid)
    {
        return grid.GetCells()
            .Where(cell => IsInitialSpawnCell(grid, cell))
            .Select(cell => cell.Position);
    }

    public Vector2Int FindNearbySpawnPosition(Grid grid, Vector2Int anchor)
    {
        for (int radius = 1; radius <= 4; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > radius)
                    {
                        continue;
                    }

                    Vector2Int candidate = anchor + new Vector2Int(dx, dy);
                    if (CanInitialSpawnAt(grid, candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return CanInitialSpawnAt(grid, anchor)
            ? anchor
            : GetInitialSpawnCandidates(grid).FirstOrDefault();
    }

    public bool TryFindNearestInitialSpawnCell(
        Grid grid,
        Vector2Int origin,
        out Vector2Int position)
    {
        position = default;
        if (grid == null)
        {
            return false;
        }

        int maxRadius = Mathf.Max(grid.width, grid.height);
        for (int radius = 0; radius <= maxRadius; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) != radius)
                    {
                        continue;
                    }

                    Vector2Int candidate = origin + new Vector2Int(dx, dy);
                    if (!grid.IsValidGridPos(candidate)
                        || !CanInitialSpawnAt(grid, candidate))
                    {
                        continue;
                    }

                    position = candidate;
                    return true;
                }
            }
        }

        GridCell fallback = grid.GetCells()
            .FirstOrDefault(cell => IsInitialSpawnCell(grid, cell));
        if (fallback == null)
        {
            return false;
        }

        position = fallback.Position;
        return true;
    }

    public static bool IsInitialSpawnCell(Grid grid, GridCell cell)
    {
        return cell != null
            && grid != null
            && cell.AreaType == GridCellAreaType.ExteriorPath
            && grid.IsWalkable(cell.Position)
            && IsOutdoorSurfaceCell(grid, cell)
            && !cell.HasOccupantInLayer(GridLayer.Wildlife);
    }

    public static bool CanInitialSpawnAt(Grid grid, Vector2Int position)
    {
        return IsInitialSpawnCell(grid, grid?.GetGridCell(position));
    }

    public static bool IsValidCurrentPosition(Grid grid, WildlifeActor actor)
    {
        if (grid == null || actor == null || !grid.IsWalkable(actor.GridPosition))
        {
            return false;
        }

        GridCell cell = grid.GetGridCell(actor.GridPosition);
        if (cell == null || cell.AreaType == GridCellAreaType.BlockedExterior)
        {
            return false;
        }

        if (cell.AreaType == GridCellAreaType.ExteriorPath
            && !IsOutdoorSurfaceCell(grid, cell))
        {
            return false;
        }

        return actor.CanEnterDungeon
            || cell.AreaType != GridCellAreaType.DungeonInterior;
    }

    public static bool CanSpawnAt(
        Grid grid,
        Vector2Int position,
        bool canEnterDungeon)
    {
        GridCell cell = grid?.GetGridCell(position);
        if (cell == null
            || !grid.IsWalkable(position)
            || cell.HasOccupantInLayer(GridLayer.Wildlife)
            || cell.AreaType == GridCellAreaType.BlockedExterior)
        {
            return false;
        }

        if (cell.AreaType == GridCellAreaType.ExteriorPath
            && !IsOutdoorSurfaceCell(grid, cell))
        {
            return false;
        }

        return canEnterDungeon
            || cell.AreaType != GridCellAreaType.DungeonInterior;
    }

    public static bool IsOutdoorSurfaceCell(Grid grid, GridCell cell) =>
        WildlifeBehaviorRuntime.IsOutdoorSurfaceCell(grid, cell);

    private static void ClearPopulationCollections(WildlifePopulationState state)
    {
        state.Actors.Clear();
        state.NextBehaviorTickByWildlifeId.Clear();
        state.FoodRaidOrders.Clear();
    }
}
