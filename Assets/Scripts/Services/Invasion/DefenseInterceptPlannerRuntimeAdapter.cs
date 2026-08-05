using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DefenseInterceptPlanner
{
    public HashSet<Vector2Int> BuildUnavailableCells(
        IReadOnlyList<DefenseEngagement> engagements)
    {
        if (engagements == null)
        {
            return DefenseInterceptPlanningRules.BuildUnavailableCells(null);
        }

        return DefenseInterceptPlanningRules.BuildUnavailableCells(
            engagements
                .Where(engagement => engagement != null)
                .Select(engagement => new DefenseEngagementInterceptSnapshot(
                    engagement.IsActive,
                    engagement.IntruderStopCell,
                    engagement.GuardCell,
                    engagement.HasReserveCell,
                    engagement.ReserveCell,
                    engagement.RangedGuard != null,
                    engagement.RangedCell,
                    engagement.SecondaryRangedGuard != null,
                    engagement.SecondaryRangedCell)));
    }

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
        Queue<GridMoveStep> route = intruder.CreateNextPath(
            grid,
            targetCell,
            out _);
        if (route == null || route.Count < 2)
        {
            route = grid.GetMovePathTo(intruderStart, targetCell);
        }

        GridMoveStep[] routeSteps = route?.ToArray()
            ?? Array.Empty<GridMoveStep>();
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
            DefenseInterceptRoutePairSnapshot routePair = new(
                stopStep,
                guardStep,
                CaptureCell(grid, stopStep.To),
                CaptureCell(grid, guardStep.To),
                IsAvailable(stopStep.To, unavailableCells),
                IsAvailable(guardStep.To, unavailableCells),
                index + 1);
            if (!DefenseInterceptPlanningRules.IsPotentialRoutePair(routePair))
            {
                continue;
            }

            Queue<GridMoveStep> guardPath = grid.GetMovePathTo(
                guardStart,
                guardStep.To);
            DefenseInterceptCandidateSnapshot candidate = CaptureCandidate(
                grid,
                routePair,
                guardStart,
                guardPath,
                unavailableCells);
            if (DefenseInterceptPlanningRules.TryCreatePlan(
                candidate,
                out plan))
            {
                return true;
            }
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
        Queue<GridMoveStep> route = grid.GetMovePathTo(
            intruder.IntruderActor.GetNowXY(),
            ownerCell);
        GridMoveStep[] steps = route?.ToArray()
            ?? Array.Empty<GridMoveStep>();
        if (steps.Length == 0)
        {
            return false;
        }

        GridMoveStep finalStep = steps[steps.Length - 1];
        CaptureReserveCells(
            grid,
            ownerCell,
            finalStep.From,
            unavailableCells,
            out DefenseInterceptCellSnapshot spaced,
            out bool spacedAvailable,
            out DefenseInterceptCellSnapshot adjacent,
            out bool adjacentAvailable);
        DefenseOwnerFinalInterceptSnapshot snapshot = new(
            ownerCell,
            steps,
            IsAvailable(finalStep.From, unavailableCells),
            IsAvailable(ownerCell, unavailableCells),
            spaced,
            spacedAvailable,
            adjacent,
            adjacentAvailable);
        return DefenseInterceptPlanningRules.TryCreateOwnerFinalPlan(
            snapshot,
            out plan);
    }

    private static DefenseInterceptCandidateSnapshot CaptureCandidate(
        Grid grid,
        DefenseInterceptRoutePairSnapshot routePair,
        Vector2Int guardStart,
        Queue<GridMoveStep> guardPath,
        ISet<Vector2Int> unavailableCells)
    {
        CaptureReserveCells(
            grid,
            routePair.GuardCell.Position,
            routePair.StopCell.Position,
            unavailableCells,
            out DefenseInterceptCellSnapshot spaced,
            out bool spacedAvailable,
            out DefenseInterceptCellSnapshot adjacent,
            out bool adjacentAvailable);
        return new DefenseInterceptCandidateSnapshot(
            routePair,
            guardStart,
            guardPath,
            spaced,
            spacedAvailable,
            adjacent,
            adjacentAvailable);
    }

    private static void CaptureReserveCells(
        Grid grid,
        Vector2Int guardCell,
        Vector2Int intruderCell,
        ISet<Vector2Int> unavailableCells,
        out DefenseInterceptCellSnapshot spaced,
        out bool spacedAvailable,
        out DefenseInterceptCellSnapshot adjacent,
        out bool adjacentAvailable)
    {
        int awayDirection = Math.Sign(guardCell.x - intruderCell.x);
        if (awayDirection == 0)
        {
            awayDirection = 1;
        }

        Vector2Int direction = new(awayDirection, 0);
        Vector2Int spacedPosition = guardCell + direction * 2;
        Vector2Int adjacentPosition = guardCell + direction;
        spaced = CaptureCell(grid, spacedPosition);
        spacedAvailable = IsAvailable(spacedPosition, unavailableCells);
        adjacent = CaptureCell(grid, adjacentPosition);
        adjacentAvailable = IsAvailable(adjacentPosition, unavailableCells);
    }

    private static DefenseInterceptCellSnapshot CaptureCell(
        Grid grid,
        Vector2Int position)
    {
        bool valid = grid != null && grid.IsValidGridPos(position);
        GridCell cell = valid ? grid.GetGridCell(position) : null;
        return new DefenseInterceptCellSnapshot(
            position,
            valid,
            valid && grid.IsWalkable(position),
            cell?.AreaType ?? default);
    }

    private static bool IsAvailable(
        Vector2Int cell,
        ISet<Vector2Int> unavailableCells)
    {
        return unavailableCells == null || !unavailableCells.Contains(cell);
    }
}
