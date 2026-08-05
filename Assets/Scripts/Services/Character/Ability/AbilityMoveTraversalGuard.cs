using System;
using UnityEngine;

internal sealed class AbilityMoveTraversalGuard
{
    private readonly IDoorAccessQuery doorAccessQuery;
    private readonly IDefenseEngagementRuntime defenseEngagement;
    private readonly Func<DoorAccessOverrideKind> overrideProvider;

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
            GridTraversalContext.ForCharacter(actor, overrideProvider()),
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
