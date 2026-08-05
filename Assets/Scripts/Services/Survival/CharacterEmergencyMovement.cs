using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal sealed class CharacterEmergencyMovement
{
    private const int PathRetryFrames = 180;

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly CharacterDeprivationStateStore stateStore;

    public CharacterEmergencyMovement(
        IGridSystemProvider gridSystemProvider,
        CharacterDeprivationStateStore stateStore)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public IEnumerator MoveNear(
        CharacterActor actor,
        Vector2Int target,
        int distance,
        Queue<GridMoveStep> preparedPath = null)
    {
        if (actor == null
            || actor.IsDead
            || !gridSystemProvider.TryGetGrid(out Grid grid)
            || !actor.TryGetAbility(out AbilityMove move))
        {
            yield break;
        }

        Vector2Int start = actor.GetNowXY();
        if (Manhattan(start, target) <= distance)
        {
            yield break;
        }

        IGridPathSearchBroker broker = actor.PathSearchBroker;
        if (broker == null)
        {
            move.MarkGridMoveFailure(GridMoveFailureReason.MissingPath);
            yield break;
        }

        Vector2Int preferredAdjacent = start.x <= target.x
            ? target + Vector2Int.left
            : target + Vector2Int.right;
        Vector2Int alternateAdjacent = start.x <= target.x
            ? target + Vector2Int.right
            : target + Vector2Int.left;
        int destinationCount = distance <= 0 ? 1 : 2;
        Queue<GridMoveStep> path = preparedPath;
        GridTraversalContext traversalContext = GridTraversalContext.ForCharacter(actor);

        for (int destinationIndex = 0;
             destinationIndex < destinationCount && path == null;
             destinationIndex++)
        {
            Vector2Int destination = distance <= 0
                ? target
                : destinationIndex == 0
                    ? preferredAdjacent
                    : alternateAdjacent;
            if (!grid.IsValidGridPos(destination) || !grid.IsWalkable(destination))
            {
                continue;
            }

            for (int attempt = 0; attempt < PathRetryFrames; attempt++)
            {
                if (actor == null || actor.IsDead)
                {
                    yield break;
                }

                GridPathRequestStatus status = broker.RequestMovePathTo(
                    grid,
                    actor.GetNowXY(),
                    destination,
                    out path,
                    GridPathSearchPriority.Urgent,
                    traversalContext);
                if (status == GridPathRequestStatus.Reachable)
                {
                    break;
                }

                if (status == GridPathRequestStatus.Unreachable)
                {
                    path = null;
                    break;
                }

                yield return null;
            }

            if (path != null && path.Count == 0)
            {
                path = null;
            }
        }

        if (path == null || path.Count == 0)
        {
            move.MarkGridMoveFailure(GridMoveFailureReason.MissingPath);
            if (stateStore.TryGetWritable(actor, out CharacterDeprivationState state)
                && state.breakdown != null)
            {
                state.breakdown.targetId = string.Empty;
                state.breakdown.lastReplanReason = "경로가 막혀 다른 대상을 찾음";
            }
            yield break;
        }

        yield return move.MoveByPath(path);
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
