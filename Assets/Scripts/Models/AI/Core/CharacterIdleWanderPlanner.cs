using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class CharacterIdleWanderPlanner
{
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly IRandomStream random;

    public CharacterIdleWanderPlanner(
        IGridPathSearchBroker pathSearchBroker,
        IRandomStream random)
    {
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public bool TryFind(
        Grid grid,
        Vector3 worldPosition,
        GridTraversalContext traversalContext,
        int minDistance,
        int maxDistance,
        out Queue<GridMoveStep> path)
    {
        path = null;
        if (grid == null)
        {
            return false;
        }

        Vector2Int origin = grid.GetXY(worldPosition);
        if (!grid.IsValidGridPos(origin) || !grid.IsWalkable(origin))
        {
            return false;
        }

        int min = Mathf.Max(1, minDistance);
        int max = maxDistance > 0
            ? Mathf.Max(min, maxDistance)
            : Mathf.Max(min, 12);
        const int maximumPathAttempts = 2;
        for (int attempt = 0; attempt < maximumPathAttempts; attempt++)
        {
            int distance = random.NextInt(min, max + 1);
            int direction = random.Chance(0.5f) ? -1 : 1;
            Vector2Int destination =
                origin + new Vector2Int(distance * direction, 0);
            if (!IsPlainWalkable(grid, destination))
            {
                continue;
            }

            Queue<GridMoveStep> candidate = pathSearchBroker.GetMovePathTo(
                grid,
                origin,
                destination,
                GridPathSearchPriority.Normal,
                traversalContext);
            if (candidate == null)
            {
                return false;
            }
            if (GridMovePathRules.IsSupportedIdleWanderPath(candidate))
            {
                path = candidate;
                return true;
            }
        }

        Queue<GridMoveStep> fallback = pathSearchBroker.GetMovePath(
            grid,
            origin,
            position =>
            {
                int distance = Mathf.Abs(position.x - origin.x)
                    + Mathf.Abs(position.y - origin.y);
                return distance >= min
                    && distance <= max
                    && IsPlainWalkable(grid, position);
            },
            GridPathSearchPriority.Normal,
            traversalContext);
        if (!GridMovePathRules.IsSupportedIdleWanderPath(fallback))
        {
            return false;
        }

        path = fallback;
        return true;
    }

    private static bool IsPlainWalkable(Grid grid, Vector2Int position)
    {
        if (!grid.IsWalkable(position))
        {
            return false;
        }

        IGridOccupant building = grid.GetGridCell(position)
            ?.GetOccupant(GridLayer.Building);
        return building == null || !building.IsGridMovement;
    }
}
