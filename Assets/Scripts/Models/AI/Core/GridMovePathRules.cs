using System.Collections.Generic;
using UnityEngine;

public enum GridMoveFailureReason
{
    None,
    Cancelled,
    MissingPath,
    StaleStepStart,
    WallBlocked,
    DoorDenied,
    DefenseReservation,
    TraversalChanged,
    MissingMovementHandler,
    GridUnavailable,
    InvalidSpeed,
    DestinationMismatch
}

public static class GridMovePathRules
{
    public static bool TryGetPathDestination(
        Queue<GridMoveStep> path,
        out Vector2Int destination)
    {
        destination = default;
        if (path == null || path.Count == 0)
        {
            return false;
        }

        bool found = false;
        foreach (GridMoveStep step in path)
        {
            if (!step.IsValid)
            {
                continue;
            }

            destination = step.To;
            found = true;
        }
        return found;
    }

    public static bool IsSupportedIdleWanderPath(Queue<GridMoveStep> path)
    {
        if (path == null || path.Count == 0)
        {
            return false;
        }

        foreach (GridMoveStep step in path)
        {
            if (!step.IsValid)
            {
                return false;
            }
            if (step.MoveType != GridMoveType.Walk
                && !IsSupportedVerticalMovementStep(step))
            {
                return false;
            }
        }
        return true;
    }

    public static bool IsSupportedVerticalMovementStep(GridMoveStep step)
    {
        return step.IsValid
            && (step.MoveType == GridMoveType.Stair
                || step.MoveType == GridMoveType.Elevator)
            && step.MovementOccupant is IGridMovementHandler;
    }

    public static bool RequiresMovementHandler(GridMoveStep step)
    {
        return step.IsValid
            && (step.MoveType == GridMoveType.Stair
                || step.MoveType == GridMoveType.Elevator);
    }
}
