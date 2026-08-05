using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct DefenseRangedPositionGeometrySnapshot
{
    public DefenseRangedPositionGeometrySnapshot(
        Vector2Int cell,
        bool hasGridCell,
        GridCellAreaType areaType,
        int targetDistance,
        int maximumRange,
        bool reservedForOther)
    {
        Cell = cell;
        HasGridCell = hasGridCell;
        AreaType = areaType;
        TargetDistance = targetDistance;
        MaximumRange = maximumRange;
        ReservedForOther = reservedForOther;
    }

    public Vector2Int Cell { get; }
    public bool HasGridCell { get; }
    public GridCellAreaType AreaType { get; }
    public int TargetDistance { get; }
    public int MaximumRange { get; }
    public bool ReservedForOther { get; }
}

public readonly struct DefenseRangedPositionCandidateSnapshot
{
    public DefenseRangedPositionCandidateSnapshot(
        DefenseRangedPositionGeometrySnapshot geometry,
        bool hasLineOfSight,
        bool friendlyFireRisk,
        float accuracyMultiplier,
        float damageMultiplier,
        bool hasCover,
        float coverBaseBlockChance,
        float coverDirectionalMultiplier,
        int guardTravelDistance,
        bool crowdedByMeleeLine)
    {
        Geometry = geometry;
        HasLineOfSight = hasLineOfSight;
        FriendlyFireRisk = friendlyFireRisk;
        AccuracyMultiplier = accuracyMultiplier;
        DamageMultiplier = damageMultiplier;
        HasCover = hasCover;
        CoverBaseBlockChance = coverBaseBlockChance;
        CoverDirectionalMultiplier = coverDirectionalMultiplier;
        GuardTravelDistance = guardTravelDistance;
        CrowdedByMeleeLine = crowdedByMeleeLine;
    }

    public DefenseRangedPositionGeometrySnapshot Geometry { get; }
    public bool HasLineOfSight { get; }
    public bool FriendlyFireRisk { get; }
    public float AccuracyMultiplier { get; }
    public float DamageMultiplier { get; }
    public bool HasCover { get; }
    public float CoverBaseBlockChance { get; }
    public float CoverDirectionalMultiplier { get; }
    public int GuardTravelDistance { get; }
    public bool CrowdedByMeleeLine { get; }
}

public readonly struct DefenseRangedPositionScoreSnapshot
{
    public DefenseRangedPositionScoreSnapshot(Vector2Int cell, float score)
    {
        Cell = cell;
        Score = score;
    }

    public Vector2Int Cell { get; }
    public float Score { get; }
}

public enum DefenseRangedPathDecision
{
    Skip = 0,
    Select = 1,
    Abort = 2
}

public static class DefenseRangedPositionPlanningRules
{
    public static int ResolveSearchRadius(int maximumRange)
    {
        return Mathf.Max(2, maximumRange);
    }

    public static bool IsEligibleGeometry(
        DefenseRangedPositionGeometrySnapshot geometry)
    {
        return geometry.HasGridCell
            && geometry.AreaType == GridCellAreaType.DungeonInterior
            && geometry.TargetDistance >= 2
            && geometry.TargetDistance <= geometry.MaximumRange
            && !geometry.ReservedForOther;
    }

    public static bool TryScore(
        DefenseRangedPositionCandidateSnapshot candidate,
        out DefenseRangedPositionScoreSnapshot scored)
    {
        if (!IsEligibleGeometry(candidate.Geometry)
            || !candidate.HasLineOfSight
            || candidate.FriendlyFireRisk)
        {
            scored = default;
            return false;
        }

        float rangeScore = candidate.AccuracyMultiplier * 2.2f
            + candidate.DamageMultiplier;
        float coverScore = candidate.HasCover
            ? candidate.CoverBaseBlockChance
                * candidate.CoverDirectionalMultiplier
                * 4f
            : 0f;
        float travelPenalty = candidate.GuardTravelDistance * 0.04f;
        float crowdPenalty = candidate.CrowdedByMeleeLine ? 4f : 0f;
        scored = new DefenseRangedPositionScoreSnapshot(
            candidate.Geometry.Cell,
            rangeScore + coverScore - travelPenalty - crowdPenalty);
        return true;
    }

    public static void SortByDescendingScore(
        List<DefenseRangedPositionScoreSnapshot> candidates)
    {
        if (candidates == null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
    }

    public static DefenseRangedPathDecision EvaluatePathCandidate(
        Vector2Int guardCell,
        Vector2Int candidateCell,
        bool pathIsNull,
        int pathStepCount)
    {
        if (candidateCell == guardCell)
        {
            return DefenseRangedPathDecision.Select;
        }
        if (pathIsNull)
        {
            return DefenseRangedPathDecision.Abort;
        }

        return pathStepCount > 0
            ? DefenseRangedPathDecision.Select
            : DefenseRangedPathDecision.Skip;
    }

    public static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
