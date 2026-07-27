using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct FinalDefenseRallyPlan
{
    private readonly GridMoveStep[] ownerSteps;
    private readonly GridMoveStep[] intruderSteps;

    public FinalDefenseRallyPlan(
        Vector2Int target,
        IEnumerable<GridMoveStep> ownerSteps,
        IEnumerable<GridMoveStep> intruderSteps)
    {
        Target = target;
        this.ownerSteps = ownerSteps?.Where(step => step.IsValid).ToArray()
            ?? Array.Empty<GridMoveStep>();
        this.intruderSteps = intruderSteps?.Where(step => step.IsValid).ToArray()
            ?? Array.Empty<GridMoveStep>();
    }

    public Vector2Int Target { get; }
    public IReadOnlyList<GridMoveStep> OwnerSteps => ownerSteps ?? Array.Empty<GridMoveStep>();
    public IReadOnlyList<GridMoveStep> IntruderSteps => intruderSteps ?? Array.Empty<GridMoveStep>();
    public Queue<GridMoveStep> CreateOwnerPath() => new Queue<GridMoveStep>(OwnerSteps);
    public Queue<GridMoveStep> CreateIntruderPath() => new Queue<GridMoveStep>(IntruderSteps);
}

public static class FinalDefenseRallyPlanner
{
    public static bool TryCreate(
        Grid grid,
        Vector2Int entryPosition,
        Vector2Int ownerPosition,
        IGridPathSearchBroker pathSearchBroker,
        out FinalDefenseRallyPlan plan)
    {
        plan = default;
        if (grid == null
            || pathSearchBroker == null
            || !grid.IsValidGridPos(entryPosition)
            || !grid.IsValidGridPos(ownerPosition)
            || !grid.IsWalkable(entryPosition))
        {
            return false;
        }

        if (!pathSearchBroker.TryGetSearch(grid, entryPosition, out GridPathSearchResult entrySearch)
            || !pathSearchBroker.TryGetSearch(grid, ownerPosition, out GridPathSearchResult ownerSearch))
        {
            return false;
        }

        Vector2Int bestTarget = default;
        GridMoveStep[] bestOwnerPath = null;
        GridMoveStep[] bestIntruderPath = null;
        int bestEntryDistance = -1;
        bool bestIsPlainHallway = false;

        foreach (Vector2Int candidate in entrySearch.GetReachablePositions())
        {
            if (candidate == entryPosition
                || candidate.y != entryPosition.y
                || !ownerSearch.ContainsPosition(candidate)
                || !grid.IsWalkable(candidate))
            {
                continue;
            }

            GridMoveStep[] intruderPath = entrySearch
                .GetMovePathTo(candidate)
                .Where(step => step.IsValid)
                .ToArray();
            if (intruderPath.Length == 0)
            {
                continue;
            }

            GridMoveStep[] ownerPath = ownerSearch
                .GetMovePathTo(candidate)
                .Where(step => step.IsValid)
                .ToArray();
            if (candidate != ownerPosition && ownerPath.Length == 0)
            {
                continue;
            }

            bool isPlainHallway = grid.GetGridCell(candidate)?.GetOccupant(GridLayer.Building) == null;
            bool isBetter = intruderPath.Length > bestEntryDistance
                || (intruderPath.Length == bestEntryDistance
                    && isPlainHallway
                    && !bestIsPlainHallway)
                || (intruderPath.Length == bestEntryDistance
                    && isPlainHallway == bestIsPlainHallway
                    && ownerPath.Length < (bestOwnerPath?.Length ?? int.MaxValue));
            if (!isBetter)
            {
                continue;
            }

            bestTarget = candidate;
            bestOwnerPath = ownerPath;
            bestIntruderPath = intruderPath;
            bestEntryDistance = intruderPath.Length;
            bestIsPlainHallway = isPlainHallway;
        }

        if (bestIntruderPath == null || bestIntruderPath.Length == 0)
        {
            return false;
        }

        plan = new FinalDefenseRallyPlan(bestTarget, bestOwnerPath, bestIntruderPath);
        return true;
    }
}
