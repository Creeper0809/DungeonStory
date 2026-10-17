using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class AbilityMoveTraversalGuard
{
    private readonly IDoorAccessQuery doorAccessQuery;
    private readonly IDefenseEngagementRuntime defenseEngagement;
    private readonly Func<DoorAccessOverrideKind> overrideProvider;
    private Door passageFrom;
    private Door passageTo;
    private int passageEpoch;
    private readonly List<Door> worldSegmentDoors = new();

    internal bool TryOpenWorldSegment(CharacterActor actor, Grid grid, Vector3 from, Vector3 to)
    {
        if (actor == null || grid == null) return false;
        worldSegmentDoors.Clear();
        Vector2Int start = grid.GetXY(from), end = grid.GetXY(to);
        var context = GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor), overrideProvider());
        // Raw motion may span several cells in one presentation update. Test the
        // swept segment, not just its endpoint. Leaving the source cell retains
        // the same permission semantics as ordinary GridMoveStep traversal.
        for (int y = Mathf.Max(0, Mathf.Min(start.y, end.y)); y <= Mathf.Min(grid.height - 1, Mathf.Max(start.y, end.y)); y++)
        for (int x = Mathf.Max(0, Mathf.Min(start.x, end.x)); x <= Mathf.Min(grid.width - 1, Mathf.Max(start.x, end.x)); x++)
        {
            var cell = new Vector2Int(x, y);
            if (cell == start || !WorldSegmentTouchesCell(grid, cell, from, to)) continue;
            if (grid.GetGridCell(cell)?.GetOccupant(GridLayer.Building) is not Door door
                || worldSegmentDoors.Contains(door)) continue;
            if (!doorAccessQuery.CanTraverse(grid, cell, context, out _)) return false;
            worldSegmentDoors.Add(door);
        }
        // No yield between preflight, opening and the caller's position commit.
        // Physical occupancy owns protection after that commit; no raw lease is saved.
        foreach (Door door in worldSegmentDoors)
            if (!door.TryOpenForTraversal(context, doorAccessQuery, out _)) return false;
        return true;
    }

    internal static bool WorldSegmentTouchesCell(Grid grid, Vector2Int cell, Vector3 from, Vector3 to)
    {
        Vector3 center = grid.GetWorldPos(cell);
        float enter = 0f, exit = 1f;
        // Grid.GetXY uses floor(y) / CellWorldHeight (integer division), so row
        // zero extends below the origin by height-1, unlike the positive rows.
        float bottom = cell.y == 0 ? grid.OriginPosition.y + 1 - grid.CellWorldHeight : center.y;
        return ClipAxis(from.x, to.x - from.x, center.x - 0.5f, center.x + 0.5f, ref enter, ref exit)
            && ClipAxis(from.y, to.y - from.y, bottom, center.y + grid.CellWorldHeight, ref enter, ref exit);
    }

    private static bool ClipAxis(float start, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (delta == 0f) return start >= min && start <= max;
        float a = (min - start) / delta, b = (max - start) / delta;
        enter = Mathf.Max(enter, Mathf.Min(a, b));
        exit = Mathf.Min(exit, Mathf.Max(a, b));
        return enter <= exit;
    }

    internal bool OccupiesDoorPassage(Door door) => door != null && (passageFrom == door || passageTo == door);
    internal void ClearDoorPassage()
    {
        passageFrom = null;
        passageTo = null;
        passageEpoch = unchecked(passageEpoch + 1);
    }

    internal PassageScope BeginDoorPassage(CharacterActor actor, Grid grid, Vector2Int target)
    {
        ClearDoorPassage();
        if (grid == null || actor == null) return new PassageScope(this, passageEpoch, false);
        var destination = grid.GetGridCell(target)?.GetOccupant(GridLayer.Building) as Door;
        if (destination != null && !destination.TryOpenForTraversal(
                GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor), overrideProvider()),
                doorAccessQuery, out _)) return new PassageScope(this, passageEpoch, false);
        passageTo = destination;
        passageFrom = grid.GetGridCell(grid.GetXY(actor.transform.position))?.GetOccupant(GridLayer.Building) as Door;
        return new PassageScope(this, passageEpoch, true);
    }

    internal readonly struct PassageScope : IDisposable
    {
        private readonly AbilityMoveTraversalGuard owner;
        private readonly int epoch;
        internal PassageScope(AbilityMoveTraversalGuard owner, int epoch, bool allowed)
        { this.owner = owner; this.epoch = epoch; Allowed = allowed; }
        internal bool Allowed { get; }
        public void Dispose() { if (owner.passageEpoch == epoch) owner.ClearDoorPassage(); }
    }

    public AbilityMoveTraversalGuard(
        IDoorAccessQuery doorAccessQuery,
        IDefenseEngagementRuntime defenseEngagement,
        Func<DoorAccessOverrideKind> overrideProvider)
    {
        this.doorAccessQuery = doorAccessQuery
            ?? throw new ArgumentNullException(nameof(doorAccessQuery));
        this.defenseEngagement = defenseEngagement;
        this.overrideProvider = overrideProvider
            ?? throw new ArgumentNullException(nameof(overrideProvider));
    }

    public bool TryGetWalkStepBlockReason(
        CharacterActor actor,
        Grid grid,
        GridMoveStep step,
        out GridMoveFailureReason reason)
    {
        reason = GridMoveFailureReason.None;
        if (!step.IsValid || step.MoveType != GridMoveType.Walk || grid == null)
        {
            return false;
        }

        reason = GetCellBlockReason(actor, grid, step.To);
        return reason != GridMoveFailureReason.None;
    }

    public GridMoveFailureReason GetCellBlockReason(
        CharacterActor actor,
        Grid grid,
        Vector2Int position)
    {
        if (grid != null && grid.IsMovementBlockedByWall(position))
        {
            return GridMoveFailureReason.WallBlocked;
        }
        if (!CanTraverseDoor(actor, grid, position, out _))
        {
            return GridMoveFailureReason.DoorDenied;
        }
        if (defenseEngagement?.IsCellReservedForOther(actor, position) ?? false)
        {
            return GridMoveFailureReason.DefenseReservation;
        }
        return GridMoveFailureReason.None;
    }

    public bool CanTraverseDoor(
        CharacterActor actor,
        Grid grid,
        Vector2Int position,
        out string denialReason)
    {
        denialReason = string.Empty;
        if (grid == null || actor == null)
        {
            return true;
        }

        return doorAccessQuery.CanTraverse(
            grid,
            position,
            GridTraversalContext.ForCharacter(
                CharacterPersistentIdentity.Require(actor),
                overrideProvider()),
            out denialReason);
    }

    public static bool IsAtStepStart(
        Grid grid,
        Vector3 worldPosition,
        GridMoveStep step)
    {
        return step.IsValid
            && grid != null
            && grid.GetXY(worldPosition) == step.From;
    }

    public bool TryRollbackForChangedBlock(
        CharacterActor actor,
        Grid grid,
        Transform transform,
        Vector2Int? blockedPosition,
        ref int observedGridVersion,
        Vector3 fallbackPosition,
        out GridMoveFailureReason reason)
    {
        reason = GridMoveFailureReason.None;
        if (!blockedPosition.HasValue || grid == null)
        {
            return false;
        }

        if (defenseEngagement?.IsCellReservedForOther(
                actor,
                blockedPosition.Value) ?? false)
        {
            reason = GridMoveFailureReason.DefenseReservation;
        }
        else if (!CanTraverseDoor(
                     actor,
                     grid,
                     blockedPosition.Value,
                     out _))
        {
            reason = GridMoveFailureReason.DoorDenied;
        }
        else if (grid.TraversalVersion != observedGridVersion)
        {
            observedGridVersion = grid.TraversalVersion;
            if (grid.IsMovementBlockedByWall(blockedPosition.Value))
            {
                reason = GridMoveFailureReason.TraversalChanged;
            }
        }

        if (reason == GridMoveFailureReason.None)
        {
            return false;
        }

        transform.position = fallbackPosition;
        return true;
    }
}
