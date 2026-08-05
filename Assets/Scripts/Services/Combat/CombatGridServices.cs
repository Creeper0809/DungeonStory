using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cohesive cover capability for combat executors that both inspect and
/// damage cover. Character health query and command boundaries remain split.
/// </summary>
public sealed class CombatCoverServices
{
    public CombatCoverServices(
        ICombatCoverQuery query,
        ICombatCoverDurabilityRegistry durability)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
        Durability = durability
            ?? throw new ArgumentNullException(nameof(durability));
    }

    public ICombatCoverQuery Query { get; }
    public ICombatCoverDurabilityRegistry Durability { get; }
}

public readonly struct CombatLineOfSightResult
{
    public CombatLineOfSightResult(
        bool hasLineOfSight,
        bool friendlyFireRisk,
        Vector2Int blockingCell,
        IReadOnlyList<Vector2Int> traversedCells,
        string failureReason,
        IReadOnlyList<CombatShotTraceOccupant> intermediateOccupants = null)
    {
        HasLineOfSight = hasLineOfSight;
        FriendlyFireRisk = friendlyFireRisk;
        BlockingCell = blockingCell;
        TraversedCells = traversedCells ?? Array.Empty<Vector2Int>();
        FailureReason = failureReason ?? string.Empty;
        Trace = new CombatShotTrace(
            TraversedCells,
            intermediateOccupants ?? Array.Empty<CombatShotTraceOccupant>(),
            blockingCell,
            FailureReason);
    }

    public bool HasLineOfSight { get; }
    public bool FriendlyFireRisk { get; }
    public Vector2Int BlockingCell { get; }
    public IReadOnlyList<Vector2Int> TraversedCells { get; }
    public string FailureReason { get; }
    public CombatShotTrace Trace { get; }
}

public interface ICombatLineOfSightService
{
    CombatLineOfSightResult Evaluate(
        Grid grid,
        Vector2Int attackerCell,
        Vector2Int targetCell,
        string attackerId = "",
        string targetId = "");
}

public interface ICombatCoverQuery
{
    CombatCoverSnapshot GetCover(Grid grid, Vector2Int attackerCell, Vector2Int targetCell);
}

public sealed class GridCombatLineOfSightService : ICombatLineOfSightService
{
    private readonly ICombatAffiliationService affiliation;
    private readonly ICharacterAiWorldRegistry worldRegistry;

    public GridCombatLineOfSightService(
        ICombatAffiliationService affiliation,
        ICharacterAiWorldRegistry worldRegistry)
    {
        this.affiliation = affiliation;
        this.worldRegistry = worldRegistry;
    }

    public CombatLineOfSightResult Evaluate(
        Grid grid,
        Vector2Int attackerCell,
        Vector2Int targetCell,
        string attackerId = "",
        string targetId = "")
    {
        if (grid == null
            || !grid.IsValidGridPos(attackerCell)
            || !grid.IsValidGridPos(targetCell))
        {
            return new CombatLineOfSightResult(
                false, false, attackerCell, Array.Empty<Vector2Int>(), "유효하지 않은 사선");
        }

        List<Vector2Int> cells = TraceSupercover(attackerCell, targetCell);
        List<CombatShotTraceOccupant> intermediateOccupants =
            new List<CombatShotTraceOccupant>();
        CombatParticipantRef attacker = FindParticipant(attackerId);
        bool friendlyRisk = false;
        for (int i = 1; i < cells.Count; i++)
        {
            Vector2Int cellPosition = cells[i];
            GridCell cell = grid.GetGridCell(cellPosition);
            if (cell == null)
            {
                return new CombatLineOfSightResult(
                    false, friendlyRisk, cellPosition, cells, "Grid 밖", intermediateOccupants);
            }

            if (i < cells.Count - 1
                && IsHardBlocker(cell, attackerCell, targetCell, cellPosition))
            {
                return new CombatLineOfSightResult(
                    false,
                    friendlyRisk,
                    cellPosition,
                    cells,
                    "벽 또는 높은 엄폐에 가로막힘",
                    intermediateOccupants);
            }

            if (i > 0
                && cells[i - 1].y != cellPosition.y
                && !HasVerticalOpening(grid.GetGridCell(cells[i - 1]))
                && !HasVerticalOpening(cell))
            {
                return new CombatLineOfSightResult(
                    false,
                    friendlyRisk,
                    cellPosition,
                    cells,
                    "층간 사선이 닫힘",
                    intermediateOccupants);
            }

            if (i >= cells.Count - 1)
            {
                continue;
            }

            foreach (CombatParticipantRef participant in GetParticipantsAt(cellPosition))
            {
                if (!participant.IsValid
                    || string.Equals(participant.Id, attackerId, StringComparison.Ordinal)
                    || string.Equals(participant.Id, targetId, StringComparison.Ordinal))
                {
                    continue;
                }

                CombatRelationship relationship = affiliation != null && attacker.IsValid
                    ? affiliation.GetRelationship(attacker, participant)
                    : CombatRelationship.Neutral;
                intermediateOccupants.Add(new CombatShotTraceOccupant(
                    participant,
                    cellPosition,
                    Mathf.Abs(cellPosition.x - attackerCell.x)
                        + Mathf.Abs(cellPosition.y - attackerCell.y),
                    relationship));
                friendlyRisk |= relationship != CombatRelationship.Hostile;
            }
        }

        return new CombatLineOfSightResult(
            true, friendlyRisk, default, cells, string.Empty, intermediateOccupants);
    }

    public static List<Vector2Int> TraceSupercover(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> result = new List<Vector2Int> { start };
        int dx = end.x - start.x;
        int dy = end.y - start.y;
        int nx = Mathf.Abs(dx);
        int ny = Mathf.Abs(dy);
        int signX = Math.Sign(dx);
        int signY = Math.Sign(dy);
        int x = start.x;
        int y = start.y;
        int ix = 0;
        int iy = 0;

        while (ix < nx || iy < ny)
        {
            int decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx;
            if (decision == 0)
            {
                x += signX;
                y += signY;
                ix++;
                iy++;
            }
            else if (decision < 0)
            {
                x += signX;
                ix++;
            }
            else
            {
                y += signY;
                iy++;
            }

            Vector2Int next = new Vector2Int(x, y);
            if (result[result.Count - 1] != next)
            {
                result.Add(next);
            }
        }

        return result;
    }

    private static bool IsHardBlocker(
        GridCell cell,
        Vector2Int attackerCell,
        Vector2Int targetCell,
        Vector2Int blockerCell)
    {
        BuildableObject building = cell.GetOccupant(GridLayer.Building) as BuildableObject;
        if (building?.BuildingData == null)
        {
            return false;
        }

        if (building.BuildingData.IsStructuralWall && !building.BuildingData.IsDoor)
        {
            return true;
        }

        BuildingCoverAbility cover = building.BuildingData.GetCoverAbility();
        return cover != null
            && cover.height == CombatCoverHeight.High
            && !CanCornerPeek(cover, attackerCell, targetCell, blockerCell);
    }

    private static bool CanCornerPeek(
        BuildingCoverAbility cover,
        Vector2Int attackerCell,
        Vector2Int targetCell,
        Vector2Int coverCell)
    {
        if (cover?.allowsCornerPeek != true
            || Mathf.Abs(attackerCell.x - coverCell.x)
                + Mathf.Abs(attackerCell.y - coverCell.y) != 1)
        {
            return false;
        }

        Vector2 fromShooter = coverCell - attackerCell;
        Vector2 toTarget = targetCell - coverCell;
        return Vector2.Dot(fromShooter.normalized, toTarget.normalized) > 0.25f;
    }

    private CombatParticipantRef FindParticipant(string participantId)
    {
        if (string.IsNullOrWhiteSpace(participantId))
        {
            return default;
        }

        foreach (CharacterActor actor in worldRegistry?.Characters ?? Array.Empty<CharacterActor>())
        {
            if (actor != null
                && string.Equals(
                    actor.Identity?.PersistentId,
                    participantId,
                    StringComparison.Ordinal))
            {
                return new CombatParticipantRef(actor);
            }
        }

        foreach (WildlifeActor wildlife in worldRegistry?.Wildlife ?? Array.Empty<WildlifeActor>())
        {
            if (wildlife != null
                && string.Equals(wildlife.WildlifeId, participantId, StringComparison.Ordinal))
            {
                return new CombatParticipantRef(wildlife);
            }
        }

        return default;
    }

    private IEnumerable<CombatParticipantRef> GetParticipantsAt(Vector2Int cell)
    {
        foreach (CharacterActor actor in worldRegistry?.Characters ?? Array.Empty<CharacterActor>())
        {
            if (actor != null && !actor.IsDead && actor.GetNowXY() == cell)
            {
                yield return new CombatParticipantRef(actor);
            }
        }

        foreach (WildlifeActor wildlife in worldRegistry?.Wildlife ?? Array.Empty<WildlifeActor>())
        {
            if (wildlife != null && wildlife.IsAlive && wildlife.GridPosition == cell)
            {
                yield return new CombatParticipantRef(wildlife);
            }
        }
    }

    private static bool HasVerticalOpening(GridCell cell)
    {
        if (cell == null)
        {
            return false;
        }

        if (cell.AreaType is GridCellAreaType.Entrance
            or GridCellAreaType.ExteriorPath
            or GridCellAreaType.DropZone)
        {
            return true;
        }

        BuildableObject building = cell.GetOccupant(GridLayer.Building) as BuildableObject;
        return building != null
            && (building.BuildingData?.IsDoor == true
                || building.category == BuildingCategory.Movement);
    }
}

public sealed class GridCombatCoverQuery : ICombatCoverQuery
{
    public CombatCoverSnapshot GetCover(
        Grid grid,
        Vector2Int attackerCell,
        Vector2Int targetCell)
    {
        if (grid == null || attackerCell == targetCell)
        {
            return default;
        }

        List<Vector2Int> line = GridCombatLineOfSightService.TraceSupercover(attackerCell, targetCell);
        if (line.Count < 2)
        {
            return default;
        }

        int i = line.Count - 2;
        if (i >= 1)
        {
            GridCell cell = grid.GetGridCell(line[i]);
            BuildableObject building = cell?.GetOccupant(GridLayer.Building) as BuildableObject;
            BuildingCoverAbility cover = building?.BuildingData?.GetCoverAbility();
            if (cover == null || cover.height == CombatCoverHeight.None)
            {
                return default;
            }

            CombatCoverDurability durability = building.GetComponent<CombatCoverDurability>();
            float durabilityMultiplier = durability != null
                ? Mathf.Lerp(0.45f, 1f, durability.DurabilityRatio)
                : 1f;

            Vector2 facing = cover.facingDirection == Vector2Int.zero
                ? Vector2.left
                : ((Vector2)cover.facingDirection).normalized;
            Vector2 toAttacker = ((Vector2)(attackerCell - line[i])).normalized;
            float angle = Vector2.Angle(facing, toAttacker);
            return new CombatCoverSnapshot(
                cover.height,
                cover.blockChance * durabilityMultiplier,
                angle,
                durability != null
                    ? durability.SourceId
                    : $"cover:{building.RequirePersistentInstanceId().Value}",
                cover.allowsCornerPeek);
        }

        return default;
    }
}
