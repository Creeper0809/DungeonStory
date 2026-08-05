using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct DefenseEngagementInterceptSnapshot
{
    public DefenseEngagementInterceptSnapshot(
        bool active,
        Vector2Int intruderStopCell,
        Vector2Int guardCell,
        bool hasReserveCell,
        Vector2Int reserveCell,
        bool hasRangedGuard,
        Vector2Int rangedCell,
        bool hasSecondaryRangedGuard,
        Vector2Int secondaryRangedCell)
    {
        Active = active;
        IntruderStopCell = intruderStopCell;
        GuardCell = guardCell;
        HasReserveCell = hasReserveCell;
        ReserveCell = reserveCell;
        HasRangedGuard = hasRangedGuard;
        RangedCell = rangedCell;
        HasSecondaryRangedGuard = hasSecondaryRangedGuard;
        SecondaryRangedCell = secondaryRangedCell;
    }

    public bool Active { get; }
    public Vector2Int IntruderStopCell { get; }
    public Vector2Int GuardCell { get; }
    public bool HasReserveCell { get; }
    public Vector2Int ReserveCell { get; }
    public bool HasRangedGuard { get; }
    public Vector2Int RangedCell { get; }
    public bool HasSecondaryRangedGuard { get; }
    public Vector2Int SecondaryRangedCell { get; }
}

public readonly struct DefenseInterceptCellSnapshot
{
    public DefenseInterceptCellSnapshot(
        Vector2Int position,
        bool valid,
        bool walkable,
        GridCellAreaType areaType)
    {
        Position = position;
        Valid = valid;
        Walkable = walkable;
        AreaType = areaType;
    }

    public Vector2Int Position { get; }
    public bool Valid { get; }
    public bool Walkable { get; }
    public GridCellAreaType AreaType { get; }
    public bool IsDungeonInterior =>
        Valid && AreaType == GridCellAreaType.DungeonInterior;
}

public readonly struct DefenseInterceptRoutePairSnapshot
{
    public DefenseInterceptRoutePairSnapshot(
        GridMoveStep stopStep,
        GridMoveStep guardStep,
        DefenseInterceptCellSnapshot stopCell,
        DefenseInterceptCellSnapshot guardCell,
        bool stopAvailable,
        bool guardAvailable,
        int intruderSteps)
    {
        StopStep = stopStep;
        GuardStep = guardStep;
        StopCell = stopCell;
        GuardCell = guardCell;
        StopAvailable = stopAvailable;
        GuardAvailable = guardAvailable;
        IntruderSteps = Mathf.Max(0, intruderSteps);
    }

    public GridMoveStep StopStep { get; }
    public GridMoveStep GuardStep { get; }
    public DefenseInterceptCellSnapshot StopCell { get; }
    public DefenseInterceptCellSnapshot GuardCell { get; }
    public bool StopAvailable { get; }
    public bool GuardAvailable { get; }
    public int IntruderSteps { get; }
}

public sealed class DefenseInterceptCandidateSnapshot
{
    public DefenseInterceptCandidateSnapshot(
        DefenseInterceptRoutePairSnapshot routePair,
        Vector2Int guardStart,
        IEnumerable<GridMoveStep> guardPath,
        DefenseInterceptCellSnapshot spacedReserveCell,
        bool spacedReserveAvailable,
        DefenseInterceptCellSnapshot adjacentReserveCell,
        bool adjacentReserveAvailable)
    {
        RoutePair = routePair;
        GuardStart = guardStart;
        GuardPath = Array.AsReadOnly(
            (guardPath ?? Array.Empty<GridMoveStep>()).ToArray());
        SpacedReserveCell = spacedReserveCell;
        SpacedReserveAvailable = spacedReserveAvailable;
        AdjacentReserveCell = adjacentReserveCell;
        AdjacentReserveAvailable = adjacentReserveAvailable;
    }

    public DefenseInterceptRoutePairSnapshot RoutePair { get; }
    public Vector2Int GuardStart { get; }
    public IReadOnlyList<GridMoveStep> GuardPath { get; }
    public DefenseInterceptCellSnapshot SpacedReserveCell { get; }
    public bool SpacedReserveAvailable { get; }
    public DefenseInterceptCellSnapshot AdjacentReserveCell { get; }
    public bool AdjacentReserveAvailable { get; }
}

public sealed class DefenseOwnerFinalInterceptSnapshot
{
    public DefenseOwnerFinalInterceptSnapshot(
        Vector2Int ownerCell,
        IReadOnlyList<GridMoveStep> routeSteps,
        bool stopAvailable,
        bool ownerAvailable,
        DefenseInterceptCellSnapshot spacedReserveCell,
        bool spacedReserveAvailable,
        DefenseInterceptCellSnapshot adjacentReserveCell,
        bool adjacentReserveAvailable)
    {
        OwnerCell = ownerCell;
        RouteSteps = Array.AsReadOnly(
            (routeSteps ?? Array.Empty<GridMoveStep>()).ToArray());
        StopAvailable = stopAvailable;
        OwnerAvailable = ownerAvailable;
        SpacedReserveCell = spacedReserveCell;
        SpacedReserveAvailable = spacedReserveAvailable;
        AdjacentReserveCell = adjacentReserveCell;
        AdjacentReserveAvailable = adjacentReserveAvailable;
    }

    public Vector2Int OwnerCell { get; }
    public IReadOnlyList<GridMoveStep> RouteSteps { get; }
    public bool StopAvailable { get; }
    public bool OwnerAvailable { get; }
    public DefenseInterceptCellSnapshot SpacedReserveCell { get; }
    public bool SpacedReserveAvailable { get; }
    public DefenseInterceptCellSnapshot AdjacentReserveCell { get; }
    public bool AdjacentReserveAvailable { get; }
}

public static class DefenseInterceptPlanningRules
{
    public static HashSet<Vector2Int> BuildUnavailableCells(
        IEnumerable<DefenseEngagementInterceptSnapshot> engagements)
    {
        HashSet<Vector2Int> cells = new();
        if (engagements == null)
        {
            return cells;
        }

        foreach (DefenseEngagementInterceptSnapshot engagement in engagements)
        {
            if (!engagement.Active)
            {
                continue;
            }

            cells.Add(engagement.IntruderStopCell);
            cells.Add(engagement.GuardCell);
            if (engagement.HasReserveCell)
            {
                cells.Add(engagement.ReserveCell);
            }
            if (engagement.HasRangedGuard)
            {
                cells.Add(engagement.RangedCell);
            }
            if (engagement.HasSecondaryRangedGuard)
            {
                cells.Add(engagement.SecondaryRangedCell);
            }
        }

        return cells;
    }

    public static bool IsPotentialRoutePair(
        DefenseInterceptRoutePairSnapshot candidate)
    {
        return candidate.StopStep.IsValid
            && candidate.GuardStep.IsValid
            && candidate.StopStep.MoveType == GridMoveType.Walk
            && candidate.GuardStep.MoveType == GridMoveType.Walk
            && candidate.StopCell.IsDungeonInterior
            && candidate.GuardCell.IsDungeonInterior
            && candidate.StopCell.Position.y == candidate.GuardCell.Position.y
            && Mathf.Abs(
                candidate.StopCell.Position.x
                - candidate.GuardCell.Position.x) == 1
            && candidate.StopAvailable
            && candidate.GuardAvailable;
    }

    public static bool TryCreatePlan(
        DefenseInterceptCandidateSnapshot candidate,
        out DefenseInterceptPlan plan)
    {
        plan = default;
        if (candidate == null
            || !IsPotentialRoutePair(candidate.RoutePair))
        {
            return false;
        }

        bool guardAlreadyThere = candidate.GuardStart
            == candidate.RoutePair.GuardCell.Position;
        if (!guardAlreadyThere && candidate.GuardPath.Count == 0)
        {
            return false;
        }
        if (candidate.GuardPath.Any(step =>
                step.IsValid
                && step.To == candidate.RoutePair.StopCell.Position))
        {
            return false;
        }

        int guardSteps = guardAlreadyThere ? 0 : candidate.GuardPath.Count;
        if (guardSteps > candidate.RoutePair.IntruderSteps)
        {
            return false;
        }

        Vector2Int reserveCell = SelectReserveCell(
            candidate.RoutePair.GuardCell.Position,
            candidate.SpacedReserveCell,
            candidate.SpacedReserveAvailable,
            candidate.AdjacentReserveCell,
            candidate.AdjacentReserveAvailable);
        plan = new DefenseInterceptPlan(
            candidate.RoutePair.StopCell.Position,
            candidate.RoutePair.GuardCell.Position,
            reserveCell,
            new Queue<GridMoveStep>(candidate.GuardPath),
            candidate.RoutePair.IntruderSteps);
        return true;
    }

    public static bool TryCreateOwnerFinalPlan(
        DefenseOwnerFinalInterceptSnapshot snapshot,
        out DefenseInterceptPlan plan)
    {
        plan = default;
        if (snapshot == null || snapshot.RouteSteps.Count == 0)
        {
            return false;
        }

        GridMoveStep finalStep = snapshot.RouteSteps[snapshot.RouteSteps.Count - 1];
        if (!finalStep.IsValid
            || finalStep.MoveType != GridMoveType.Walk
            || finalStep.From.y != snapshot.OwnerCell.y
            || Mathf.Abs(finalStep.From.x - snapshot.OwnerCell.x) != 1
            || !snapshot.StopAvailable
            || !snapshot.OwnerAvailable)
        {
            return false;
        }

        Vector2Int reserveCell = SelectReserveCell(
            snapshot.OwnerCell,
            snapshot.SpacedReserveCell,
            snapshot.SpacedReserveAvailable,
            snapshot.AdjacentReserveCell,
            snapshot.AdjacentReserveAvailable);
        plan = new DefenseInterceptPlan(
            finalStep.From,
            snapshot.OwnerCell,
            reserveCell,
            new Queue<GridMoveStep>(),
            snapshot.RouteSteps.Count - 1);
        return true;
    }

    private static Vector2Int SelectReserveCell(
        Vector2Int guardCell,
        DefenseInterceptCellSnapshot spacedCandidate,
        bool spacedAvailable,
        DefenseInterceptCellSnapshot adjacentCandidate,
        bool adjacentAvailable)
    {
        if (spacedCandidate.Valid
            && spacedCandidate.Walkable
            && spacedCandidate.IsDungeonInterior
            && spacedAvailable)
        {
            return spacedCandidate.Position;
        }

        return adjacentCandidate.Valid
            && adjacentCandidate.Walkable
            && adjacentCandidate.IsDungeonInterior
            && adjacentAvailable
                ? adjacentCandidate.Position
                : guardCell;
    }
}
