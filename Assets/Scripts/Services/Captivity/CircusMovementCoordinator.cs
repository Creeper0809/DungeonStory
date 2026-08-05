using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CircusMovementCoordinator : ICircusMovementCommands
{
    private readonly ICaptivityRuntime captivity;
    private readonly ICaptivityCommandService captivityCommands;
    private readonly IWildlifeCaptureRuntime wildlifeCapture;
    private readonly ICharacterAiWorldRegistry world;
    private readonly IGridSystemProvider gridProvider;
    private readonly IDoorAccessCommandService doorAccess;
    private readonly IGameClock clock;
    private readonly Dictionary<string, IDisposable> accessPasses =
        new Dictionary<string, IDisposable>(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2Int> wildlifeReturnTargets =
        new Dictionary<string, Vector2Int>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> wildlifeReturnOrders =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly List<string> wildlifeReturnTickIds = new List<string>();

    public CircusMovementCoordinator(
        CircusProgramContext program,
        CircusWorldContext worldContext,
        CircusSessionContext session)
    {
        program = program ?? throw new ArgumentNullException(nameof(program));
        worldContext = worldContext
            ?? throw new ArgumentNullException(nameof(worldContext));
        session = session ?? throw new ArgumentNullException(nameof(session));
        captivity = program.Captivity;
        captivityCommands = program.CaptivityCommands;
        wildlifeCapture = program.WildlifeCapture;
        world = worldContext.World;
        gridProvider = worldContext.GridProvider;
        doorAccess = worldContext.DoorAccess;
        clock = session.Clock;
    }

    public void Clear()
    {
        foreach (IDisposable pass in accessPasses.Values)
        {
            pass?.Dispose();
        }

        accessPasses.Clear();
        wildlifeReturnTargets.Clear();
        wildlifeReturnOrders.Clear();
        wildlifeReturnTickIds.Clear();
    }

    public void ClearOrderActorProjection(CircusShowOrder order)
    {
        foreach (string captiveId in order?.performerIds ?? new List<string>())
        {
            ReleaseAccessPass(captiveId);
            FindActor(captiveId)?.SetAiPaused(false);
        }

        foreach (string wildlifeId in order?.wildlifeIds ?? new List<string>())
        {
            ReleaseWildlifePass(wildlifeId);
        }

        foreach (string audienceId in order?.audienceIds ?? new List<string>())
        {
            FindActor(audienceId)?.SetAiPaused(false);
        }
    }

    public List<Vector2Int> ChooseAudiencePositions(RoomInstance room, int count)
    {
        List<Vector2Int> seats = room.Furniture
            .Where(item => item?.BuildingData.GetAudienceSeatingAbility()?.IsValid == true)
            .Select(item => item.centerPos)
            .Distinct()
            .ToList();
        if (seats.Count < count)
        {
            Vector2Int roomCenter = new Vector2Int(
                Mathf.RoundToInt(room.Bounds.center.x),
                Mathf.RoundToInt(room.Bounds.center.y));
            seats.AddRange(ChoosePositions(
                room,
                roomCenter,
                count - seats.Count,
                false));
        }

        return seats.Take(count).ToList();
    }

    public List<Vector2Int> ChoosePositions(
        RoomInstance room,
        Vector2Int origin,
        int count,
        bool nearFirst)
    {
        if (!gridProvider.TryGetGrid(out Grid grid))
        {
            return new List<Vector2Int>();
        }

        IEnumerable<Vector2Int> candidates = room.Cells
            .Where(cell => grid.IsWalkable(cell))
            .OrderBy(cell => nearFirst
                ? Manhattan(cell, origin)
                : -Manhattan(cell, origin));
        return candidates.Distinct().Take(Mathf.Max(0, count)).ToList();
    }

    public void StartParticipantMovement(CircusShowOrder order)
    {
        for (int index = 0;
             index < order.performerIds.Count && index < order.performerPositions.Count;
             index++)
        {
            string captiveId = order.performerIds[index];
            if (!captivity.TryGetActor(captiveId, out CharacterActor actor))
            {
                continue;
            }

            DoorAccessSubjectRef subject = new DoorAccessSubjectRef(
                captiveId,
                DoorAccessGroup.Captive,
                character: actor);
            ReplaceAccessPass(
                captiveId,
                doorAccess.BeginTemporaryOverride(
                    subject,
                    DoorAccessOverrideKind.EscortPass,
                    order.orderId));
            actor.SetAiPaused(true);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.GetAbility<AbilityMove>()?.TryStartSystemMove(
                order.performerPositions[index],
                DoorAccessOverrideKind.EscortPass,
                out _);
        }

        for (int index = 0;
             index < order.wildlifeIds.Count && index < order.wildlifePositions.Count;
             index++)
        {
            string wildlifeId = order.wildlifeIds[index];
            WildlifeActor wildlife = FindWildlife(wildlifeId);
            if (wildlife == null)
            {
                continue;
            }

            string passKey = CircusRuntimeQueries.WildlifePassKey(wildlifeId);
            ReplaceAccessPass(
                passKey,
                doorAccess.BeginTemporaryOverride(
                    new DoorAccessSubjectRef(
                        wildlifeId,
                        DoorAccessGroup.CaptiveWildlife,
                        wildlife: wildlife),
                    DoorAccessOverrideKind.EscortPass,
                    order.orderId));
            wildlife.TrySetManagedCaptivePath(
                order.wildlifePositions[index],
                clock.Time);
        }
    }

    public void StartAudienceMovement(CircusShowOrder order)
    {
        for (int index = 0;
             index < order.audienceIds.Count && index < order.audiencePositions.Count;
             index++)
        {
            CharacterActor actor = FindActor(order.audienceIds[index]);
            if (actor == null)
            {
                continue;
            }

            actor.SetAiPaused(true);
            actor.GetAbility<AbilityMove>()?.TryStartSystemMove(
                order.audiencePositions[index],
                DoorAccessOverrideKind.None,
                out _);
        }
    }

    public bool AreActorsAt(
        IReadOnlyList<string> actorIds,
        IReadOnlyList<Vector2Int> targets)
    {
        int checkedCount = Mathf.Min(actorIds?.Count ?? 0, targets?.Count ?? 0);
        if (checkedCount == 0)
        {
            return true;
        }

        for (int index = 0; index < checkedCount; index++)
        {
            CharacterActor actor = FindActor(actorIds[index]);
            if (actor != null && actor.GetNowXY() != targets[index])
            {
                return false;
            }
        }

        return true;
    }

    public bool AreParticipantsAt(CircusShowOrder order)
    {
        if (!AreActorsAt(order.performerIds, order.performerPositions))
        {
            return false;
        }

        int checkedCount = Mathf.Min(
            order.wildlifeIds?.Count ?? 0,
            order.wildlifePositions?.Count ?? 0);
        for (int index = 0; index < checkedCount; index++)
        {
            WildlifeActor actor = FindWildlife(order.wildlifeIds[index]);
            if (actor != null && actor.GridPosition != order.wildlifePositions[index])
            {
                return false;
            }
        }

        return true;
    }

    public void ReleaseOrderActors(CircusShowOrder order)
    {
        foreach (string captiveId in order.performerIds ?? new List<string>())
        {
            ReleaseAccessPass(captiveId);
            captivityCommands.TryAssignPerformer(captiveId, false, out _);
        }

        foreach (string wildlifeId in order.wildlifeIds ?? new List<string>())
        {
            WildlifeActor wildlife = FindWildlife(wildlifeId);
            if (!wildlifeCapture.TryGetCaptured(
                    wildlifeId,
                    out CapturedWildlifeState state))
            {
                ReleaseWildlifePass(wildlifeId);
                continue;
            }

            wildlifeReturnTargets[wildlifeId] = state.penPosition;
            wildlifeReturnOrders[wildlifeId] = order.orderId;
            if (wildlife != null)
            {
                wildlife.TrySetManagedCaptivePath(state.penPosition, clock.Time);
            }
        }

        foreach (string audienceId in order.audienceIds ?? new List<string>())
        {
            FindActor(audienceId)?.SetAiPaused(false);
        }
    }

    public void TickWildlifeReturns()
    {
        if (wildlifeReturnTargets.Count == 0)
        {
            return;
        }

        wildlifeReturnTickIds.Clear();
        wildlifeReturnTickIds.AddRange(wildlifeReturnTargets.Keys);
        for (int index = 0; index < wildlifeReturnTickIds.Count; index++)
        {
            string wildlifeId = wildlifeReturnTickIds[index];
            if (!wildlifeReturnTargets.TryGetValue(
                    wildlifeId,
                    out Vector2Int returnTarget))
            {
                continue;
            }

            WildlifeActor wildlife = FindWildlife(wildlifeId);
            if (wildlife == null || wildlife.GridPosition == returnTarget)
            {
                FinishWildlifeReturn(wildlifeId);
                continue;
            }

            if (!wildlife.IsMoving)
            {
                wildlife.TrySetManagedCaptivePath(returnTarget, clock.Time);
            }
        }
    }

    private void FinishWildlifeReturn(string wildlifeId)
    {
        wildlifeReturnOrders.Remove(wildlifeId, out string orderId);
        wildlifeReturnTargets.Remove(wildlifeId);
        ReleaseWildlifePass(wildlifeId);
        wildlifeCapture.CompleteShowAssignment(wildlifeId, orderId);
    }

    private void ReleaseWildlifePass(string wildlifeId)
    {
        ReleaseAccessPass(CircusRuntimeQueries.WildlifePassKey(wildlifeId));
    }

    private void ReplaceAccessPass(string key, IDisposable replacement)
    {
        ReleaseAccessPass(key);
        accessPasses[key] = replacement;
    }

    private void ReleaseAccessPass(string key)
    {
        if (accessPasses.Remove(key, out IDisposable pass))
        {
            pass?.Dispose();
        }
    }

    private CharacterActor FindActor(string persistentId) =>
        world.AllCharacters.FirstOrDefault(actor => string.Equals(
            actor?.Identity?.PersistentId?.Trim() ?? string.Empty,
            persistentId?.Trim(),
            StringComparison.Ordinal));

    private WildlifeActor FindWildlife(string wildlifeId) =>
        world.Wildlife.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                actor.WildlifeId,
                wildlifeId?.Trim(),
                StringComparison.Ordinal));

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
}
