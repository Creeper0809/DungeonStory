using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DefenseInterceptPlanner
{
    public bool TryCreatePlan(
        Grid grid,
        InvasionIntruderRuntime intruder,
        CharacterActor guard,
        Vector2Int targetCell,
        ISet<Vector2Int> unavailableCells,
        out DefenseInterceptPlan plan)
    {
        plan = default;
        if (grid == null || intruder == null || guard == null || guard.IsDead)
        {
            return false;
        }

        Vector2Int intruderStart = intruder.IntruderActor.GetNowXY();
        Queue<GridMoveStep> route = intruder.CreateNextPath(grid, targetCell, out _);
        if (route == null || route.Count < 2)
        {
            route = grid.GetMovePath(intruderStart, cell => cell == targetCell);
        }

        GridMoveStep[] routeSteps = route?.ToArray() ?? Array.Empty<GridMoveStep>();
        return TryCreatePlan(
            grid,
            routeSteps,
            guard.GetNowXY(),
            unavailableCells,
            out plan);
    }

    public bool TryCreatePlan(
        Grid grid,
        IReadOnlyList<GridMoveStep> routeSteps,
        Vector2Int guardStart,
        ISet<Vector2Int> unavailableCells,
        out DefenseInterceptPlan plan)
    {
        plan = default;
        if (grid == null || routeSteps == null || routeSteps.Count < 2)
        {
            return false;
        }

        for (int index = 0; index < routeSteps.Count - 1; index++)
        {
            GridMoveStep stopStep = routeSteps[index];
            GridMoveStep guardStep = routeSteps[index + 1];
            if (stopStep == null
                || guardStep == null
                || stopStep.MoveType != GridMoveType.Walk
                || guardStep.MoveType != GridMoveType.Walk
                || !IsDungeonInterior(grid, stopStep.To)
                || !IsDungeonInterior(grid, guardStep.To)
                || stopStep.To.y != guardStep.To.y
                || Mathf.Abs(stopStep.To.x - guardStep.To.x) != 1
                || !IsAvailable(stopStep.To, unavailableCells)
                || !IsAvailable(guardStep.To, unavailableCells))
            {
                continue;
            }

            Queue<GridMoveStep> guardPath = grid.GetMovePath(guardStart, cell => cell == guardStep.To);
            bool guardAlreadyThere = guardStart == guardStep.To;
            if (!guardAlreadyThere && (guardPath == null || guardPath.Count == 0))
            {
                continue;
            }

            if (guardPath != null && guardPath.Any(step => step != null && step.To == stopStep.To))
            {
                continue;
            }

            int intruderSteps = index + 1;
            int guardSteps = guardAlreadyThere ? 0 : guardPath.Count;
            if (guardSteps > intruderSteps)
            {
                continue;
            }

            Vector2Int reserveCell = FindReserveCell(
                grid,
                guardStep.To,
                stopStep.To,
                unavailableCells);
            plan = new DefenseInterceptPlan(
                stopStep.To,
                guardStep.To,
                reserveCell,
                guardPath,
                intruderSteps);
            return true;
        }

        return false;
    }

    public bool TryCreateOwnerFinalPlan(
        Grid grid,
        InvasionIntruderRuntime intruder,
        CharacterActor owner,
        ISet<Vector2Int> unavailableCells,
        out DefenseInterceptPlan plan)
    {
        plan = default;
        if (grid == null || intruder == null || owner == null || owner.IsDead)
        {
            return false;
        }

        Vector2Int ownerCell = owner.GetNowXY();
        Queue<GridMoveStep> route = grid.GetMovePath(
            intruder.IntruderActor.GetNowXY(),
            cell => cell == ownerCell);
        GridMoveStep[] steps = route?.ToArray() ?? Array.Empty<GridMoveStep>();
        if (steps.Length == 0)
        {
            return false;
        }

        GridMoveStep finalStep = steps[steps.Length - 1];
        if (finalStep == null
            || finalStep.MoveType != GridMoveType.Walk
            || finalStep.From.y != ownerCell.y
            || Mathf.Abs(finalStep.From.x - ownerCell.x) != 1
            || !IsAvailable(finalStep.From, unavailableCells)
            || !IsAvailable(ownerCell, unavailableCells))
        {
            return false;
        }

        Vector2Int reserveCell = FindReserveCell(grid, ownerCell, finalStep.From, unavailableCells);
        plan = new DefenseInterceptPlan(
            finalStep.From,
            ownerCell,
            reserveCell,
            new Queue<GridMoveStep>(),
            steps.Length - 1);
        return true;
    }

    private static Vector2Int FindReserveCell(
        Grid grid,
        Vector2Int guardCell,
        Vector2Int intruderCell,
        ISet<Vector2Int> unavailableCells)
    {
        int awayDirection = Math.Sign(guardCell.x - intruderCell.x);
        if (awayDirection == 0)
        {
            awayDirection = 1;
        }

        Vector2Int direction = new Vector2Int(awayDirection, 0);
        Vector2Int spacedCandidate = guardCell + direction * 2;
        if (grid.IsValidGridPos(spacedCandidate)
            && grid.IsWalkable(spacedCandidate)
            && IsDungeonInterior(grid, spacedCandidate)
            && IsAvailable(spacedCandidate, unavailableCells))
        {
            return spacedCandidate;
        }

        Vector2Int adjacentCandidate = guardCell + direction;
        return grid.IsValidGridPos(adjacentCandidate)
            && grid.IsWalkable(adjacentCandidate)
            && IsDungeonInterior(grid, adjacentCandidate)
            && IsAvailable(adjacentCandidate, unavailableCells)
                ? adjacentCandidate
                : guardCell;
    }

    private static bool IsDungeonInterior(Grid grid, Vector2Int cell)
    {
        return grid != null
            && grid.IsValidGridPos(cell)
            && grid.GetGridCell(cell)?.AreaType == GridCellAreaType.DungeonInterior;
    }

    private static bool IsAvailable(Vector2Int cell, ISet<Vector2Int> unavailableCells)
    {
        return unavailableCells == null || !unavailableCells.Contains(cell);
    }
}
