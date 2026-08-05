using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CombatAttackPositionPlanner
{
    private readonly ICombatLineOfSightService lineOfSight;
    private readonly IDefenseTacticalCoordinator tacticalCoordinator;
    private readonly IGridPathSearchBroker pathSearchBroker;

    public CombatAttackPositionPlanner(
        ICombatLineOfSightService lineOfSight,
        IDefenseTacticalCoordinator tacticalCoordinator,
        IGridPathSearchBroker pathSearchBroker)
    {
        this.lineOfSight = lineOfSight
            ?? throw new ArgumentNullException(nameof(lineOfSight));
        this.tacticalCoordinator = tacticalCoordinator
            ?? throw new ArgumentNullException(nameof(tacticalCoordinator));
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
    }

    public bool TryFindAndReserve(
        Grid grid,
        CharacterActor actor,
        CombatWeaponSnapshot weapon,
        Vector2Int targetCell,
        string actorId,
        string targetId,
        out Vector2Int destination)
    {
        destination = default;
        int preferredRange = weapon.IsRanged
            ? Mathf.Clamp(weapon.MaximumRange * 2 / 3, 2, weapon.MaximumRange)
            : 1;
        List<Vector2Int> candidates = new List<Vector2Int>();
        int radius = weapon.IsRanged ? weapon.MaximumRange : 1;
        for (int dy = -radius; dy <= radius; dy++)
        {
            int remaining = radius - Mathf.Abs(dy);
            for (int dx = -remaining; dx <= remaining; dx++)
            {
                Vector2Int cell = targetCell + new Vector2Int(dx, dy);
                int distance = Manhattan(cell, targetCell);
                if (distance <= 0
                    || (!weapon.IsRanged && distance != 1)
                    || (weapon.IsRanged && distance > weapon.MaximumRange)
                    || !grid.IsValidGridPos(cell)
                    || !grid.IsWalkable(cell))
                {
                    continue;
                }

                if (weapon.IsRanged)
                {
                    CombatLineOfSightResult sight = lineOfSight.Evaluate(
                        grid,
                        cell,
                        targetCell,
                        actorId,
                        string.Empty);
                    if (!sight.HasLineOfSight || sight.FriendlyFireRisk)
                    {
                        continue;
                    }
                }
                candidates.Add(cell);
            }
        }

        Vector2Int actorCell = actor.GetNowXY();
        candidates.RemoveAll(cell =>
            tacticalCoordinator.IsReservedForOther(actorId, cell));
        candidates.Sort((left, right) =>
        {
            int rangeComparison = Mathf.Abs(
                    Manhattan(left, targetCell) - preferredRange)
                .CompareTo(Mathf.Abs(
                    Manhattan(right, targetCell) - preferredRange));
            return rangeComparison != 0
                ? rangeComparison
                : Manhattan(actorCell, left).CompareTo(
                    Manhattan(actorCell, right));
        });

        Vector2Int? selected = null;
        foreach (Vector2Int candidate in candidates)
        {
            if (candidate == actorCell)
            {
                selected = candidate;
                break;
            }

            Queue<GridMoveStep> path = pathSearchBroker.GetMovePathTo(
                grid,
                actorCell,
                candidate);
            if (path == null)
            {
                return false;
            }
            if (path.Count > 0)
            {
                selected = candidate;
                break;
            }
        }

        if (!selected.HasValue)
        {
            return false;
        }

        destination = selected.Value;
        return tacticalCoordinator.TryReserve(
            actorId,
            targetId,
            destination,
            weapon.IsRanged
                ? CombatPositionReservationKind.Ranged
                : CombatPositionReservationKind.Melee,
            weapon.IsRanged ? weapon.MaximumRange : 1f,
            out _);
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }
}
