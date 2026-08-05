using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class DefenseRangedPositionPlanner
{
    private readonly ICombatLineOfSightService lineOfSight;
    private readonly ICombatCoverQuery coverQuery;
    private readonly IDefenseCombatExecutor combatExecutor;
    private readonly IDefenseTacticalCoordinator tacticalCoordinator;
    private readonly IGridPathSearchBroker pathSearchBroker;

    public DefenseRangedPositionPlanner(DefenseEngagementCombatServices services)
    {
        DefenseEngagementCombatServices required = services
            ?? throw new ArgumentNullException(nameof(services));
        lineOfSight = required.LineOfSight;
        coverQuery = required.Cover;
        combatExecutor = required.Executor;
        tacticalCoordinator = required.Tactics;
        pathSearchBroker = required.PathSearch;
    }

    public bool TryFind(
        Grid grid,
        DefenseEngagement engagement,
        CharacterActor guard,
        Func<CharacterActor, Vector2Int, bool> isReservedForOther,
        out Vector2Int bestCell,
        out Queue<GridMoveStep> bestPath)
    {
        bestCell = default;
        bestPath = null;
        if (grid == null
            || engagement?.IntruderActor == null
            || guard == null
            || !combatExecutor.TryGetActiveRangedWeapon(
                guard,
                out CombatWeaponSnapshot weapon))
        {
            return false;
        }

        string guardId = GetPersistentId(guard);
        string intruderId = GetPersistentId(engagement.IntruderActor);
        Vector2Int intruderCell = engagement.IntruderActor.GetNowXY();
        Vector2Int guardCell = guard.GetNowXY();
        List<DefenseRangedPositionScoreSnapshot> candidates = new();
        int maxRange = DefenseRangedPositionPlanningRules.ResolveSearchRadius(
            weapon.MaximumRange);
        for (int y = intruderCell.y - maxRange;
             y <= intruderCell.y + maxRange;
             y++)
        {
            for (int x = intruderCell.x - maxRange;
                 x <= intruderCell.x + maxRange;
                 x++)
            {
                Vector2Int cell = new(x, y);
                GridCell gridCell = grid.GetGridCell(cell);
                int distance = DefenseRangedPositionPlanningRules.Manhattan(
                    cell,
                    intruderCell);
                DefenseRangedPositionGeometrySnapshot geometry = new(
                    cell,
                    gridCell != null,
                    gridCell?.AreaType ?? default,
                    distance,
                    weapon.MaximumRange,
                    false);
                if (!DefenseRangedPositionPlanningRules.IsEligibleGeometry(
                    geometry))
                {
                    continue;
                }
                if (isReservedForOther(guard, cell))
                {
                    continue;
                }

                CombatLineOfSightResult sight = lineOfSight.Evaluate(
                    grid,
                    cell,
                    intruderCell,
                    guardId,
                    intruderId);
                if (!sight.HasLineOfSight || sight.FriendlyFireRisk)
                {
                    continue;
                }

                CombatRangeBand band = CombatRangeRules.GetBand(distance);
                CombatCoverSnapshot cover = coverQuery.GetCover(
                    grid,
                    intruderCell,
                    cell);
                DefenseRangedPositionCandidateSnapshot candidate = new(
                    geometry,
                    sight.HasLineOfSight,
                    sight.FriendlyFireRisk,
                    weapon.GetAccuracyMultiplier(band),
                    weapon.GetDamageMultiplier(band),
                    cover.Height != CombatCoverHeight.None,
                    cover.BaseBlockChance,
                    cover.GetDirectionalMultiplier(),
                    DefenseRangedPositionPlanningRules.Manhattan(
                        guardCell,
                        cell),
                    cell == engagement.GuardCell
                        || cell == engagement.ReserveCell);
                if (DefenseRangedPositionPlanningRules.TryScore(
                    candidate,
                    out DefenseRangedPositionScoreSnapshot scored))
                {
                    candidates.Add(scored);
                }
            }
        }

        DefenseRangedPositionPlanningRules.SortByDescendingScore(candidates);
        float bestScore = float.NegativeInfinity;
        foreach (DefenseRangedPositionScoreSnapshot candidate in candidates)
        {
            if (candidate.Cell == guardCell)
            {
                bestCell = candidate.Cell;
                bestScore = candidate.Score;
                bestPath = new Queue<GridMoveStep>();
                break;
            }

            Queue<GridMoveStep> candidatePath = pathSearchBroker.GetMovePathTo(
                grid,
                guardCell,
                candidate.Cell);
            DefenseRangedPathDecision pathDecision =
                DefenseRangedPositionPlanningRules.EvaluatePathCandidate(
                    guardCell,
                    candidate.Cell,
                    candidatePath == null,
                    candidatePath?.Count ?? 0);
            if (pathDecision == DefenseRangedPathDecision.Abort)
            {
                return false;
            }
            if (pathDecision == DefenseRangedPathDecision.Skip)
            {
                continue;
            }

            bestCell = candidate.Cell;
            bestScore = candidate.Score;
            bestPath = candidatePath;
            break;
        }

        if (float.IsNegativeInfinity(bestScore) || bestPath == null)
        {
            return false;
        }

        return (guardCell == bestCell || bestPath.Count > 0)
            && tacticalCoordinator.TryReserve(
                guardId,
                intruderId,
                bestCell,
                CombatPositionReservationKind.Ranged,
                bestScore,
                out _);
    }

    private static string GetPersistentId(CharacterActor actor)
    {
        return actor?.Identity?.PersistentId ?? string.Empty;
    }
}
