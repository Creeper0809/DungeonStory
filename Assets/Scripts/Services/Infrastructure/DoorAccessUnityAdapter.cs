using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Unity-facing adapter for the building-domain door access service.
/// The domain service owns policy decisions and persistent subject state;
/// this adapter translates scene objects and grid traversal contracts.
/// </summary>
public sealed class DoorAccessUnityAdapter :
    IDoorAccessQuery,
    IDoorAccessCommandService,
    IDoorAccessSubjectRegistry,
    IDoorAccessStateChangeSink
{
    private readonly DoorAccessService domain;
    private readonly ICharacterAiWorldRegistry world;

    public DoorAccessUnityAdapter(
        DoorAccessService domain,
        ICharacterAiWorldRegistry world)
    {
        this.domain = domain ?? throw new ArgumentNullException(nameof(domain));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public int DoorAccessVersion => domain.DoorAccessVersion;

    public DoorAccessSubjectRef ResolveSubject(GridTraversalContext context)
    {
        BuildingDoorAccessSubject subject = domain.ResolveSubject(
            ResolveRuntimeSubject(context));
        if (!subject.IsValid)
        {
            return default;
        }

        return new DoorAccessSubjectRef(
            subject.PersistentId,
            (DoorAccessGroup)subject.Group,
            character: subject.Runtime as CharacterActor,
            wildlife: subject.Runtime as WildlifeActor);
    }

    public bool CanUse(
        Door door,
        GridTraversalContext context,
        out string denialReason)
    {
        return domain.CanUse(
            DoorAccessPolicyPortAdapter.Wrap(door),
            ResolveRuntimeSubject(context),
            (int)context.OverrideKind,
            out denialReason);
    }

    public bool CanTraverse(
        Grid grid,
        Vector2Int position,
        GridTraversalContext context,
        out string denialReason)
    {
        Door door = grid?.GetGridCell(position)
            ?.GetOccupant(GridLayer.Building) as Door;
        if (door == null)
        {
            denialReason = string.Empty;
            return true;
        }

        return CanUse(door, context, out denialReason);
    }

    public bool SetGroupAllowed(
        Door door,
        DoorAccessGroup group,
        bool allowed)
    {
        return domain.SetGroupAllowed(
            DoorAccessPolicyPortAdapter.Wrap(door),
            (int)group,
            allowed);
    }

    public bool SetIndividualRule(
        Door door,
        string persistentId,
        DoorAccessIndividualRule rule)
    {
        return domain.SetIndividualRule(
            DoorAccessPolicyPortAdapter.Wrap(door),
            persistentId,
            (int)rule);
    }

    public bool ApplyPreset(Door door, DoorAccessPreset preset)
    {
        return domain.ApplyPreset(
            DoorAccessPolicyPortAdapter.Wrap(door),
            (int)preset);
    }

    public bool CopyPolicy(Door source) =>
        domain.CopyPolicy(DoorAccessPolicyPortAdapter.Wrap(source));

    public bool PastePolicy(Door destination) =>
        domain.PastePolicy(DoorAccessPolicyPortAdapter.Wrap(destination));

    public int ApplyPolicyToRoomDoors(Door source) =>
        domain.ApplyPolicyToRoomDoors(DoorAccessPolicyPortAdapter.Wrap(source));

    public IDisposable BeginTemporaryOverride(
        DoorAccessSubjectRef subject,
        DoorAccessOverrideKind kind,
        string scopeId)
    {
        UnityEngine.Object runtime = subject.Character != null
            ? subject.Character
            : subject.Wildlife;
        return domain.BeginTemporaryOverride(
            new BuildingDoorAccessSubject(
                subject.PersistentId,
                (int)subject.Group,
                runtime),
            (int)kind,
            scopeId);
    }

    public void SetCaptive(string persistentId, bool captive) =>
        domain.SetCaptive(persistentId, captive);

    public void SetCapturedWildlife(string wildlifeId, bool captured) =>
        domain.SetCapturedWildlife(wildlifeId, captured);

    public void ReplaceCaptiveSubjects(IEnumerable<string> persistentIds) =>
        domain.ReplaceCaptiveSubjects(persistentIds);

    public void ReplaceCapturedWildlifeSubjects(IEnumerable<string> wildlifeIds) =>
        domain.ReplaceCapturedWildlifeSubjects(wildlifeIds);

    public void NotifyDoorPolicyChanged() => domain.NotifyDoorPolicyChanged();

    private UnityEngine.Object ResolveRuntimeSubject(GridTraversalContext context)
    {
        if (context.SubjectKind == GridTraversalSubjectKind.Character)
        {
            return world.AllCharacters.FirstOrDefault(candidate =>
                candidate != null
                && candidate.BuildingCharacterId.Equals(context.CharacterId));
        }

        if (context.SubjectKind == GridTraversalSubjectKind.Wildlife)
        {
            return world.Wildlife.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.WildlifeId,
                    context.WildlifeId,
                    StringComparison.Ordinal));
        }

        return null;
    }
}

internal sealed class DoorAccessPolicyPortAdapter : IBuildingDoorAccessPolicyPort
{
    private readonly Door door;

    private DoorAccessPolicyPortAdapter(Door door)
    {
        this.door = door;
    }

    internal Door Door => door;

    public bool IsDestroyed => door == null || door.isDestroy;

    public static DoorAccessPolicyPortAdapter Wrap(Door door) =>
        door != null ? new DoorAccessPolicyPortAdapter(door) : null;

    public int GetIndividualRule(string id) =>
        (int)(door?.AccessPolicy?.GetIndividualRule(id)
            ?? DoorAccessIndividualRule.GroupDefault);

    public bool IsGroupAllowed(int group) =>
        door?.AccessPolicy?.IsGroupAllowed((DoorAccessGroup)group) == true;

    public bool SetGroupAllowed(int group, bool allowed) =>
        door?.AccessStateModule?.SetGroupAllowed((DoorAccessGroup)group, allowed) == true;

    public bool SetIndividualRule(string id, int rule) =>
        door?.AccessStateModule?.SetIndividualRule(
            id,
            (DoorAccessIndividualRule)rule) == true;

    public void ApplyPreset(int preset)
    {
        door?.AccessStateModule?.ApplyPreset((DoorAccessPreset)preset);
    }

    public object CapturePolicy() => door?.AccessPolicy?.Clone();

    public bool RestorePolicy(object policy)
    {
        if (door?.AccessStateModule == null
            || policy is not DoorAccessPolicyState typedPolicy)
        {
            return false;
        }

        door.AccessStateModule.CopyFrom(typedPolicy);
        return true;
    }
}

public sealed class BuildingDoorRoomPolicyAdapter : IBuildingDoorRoomPolicyPort
{
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IRoomLayoutCache roomLayoutCache;

    public BuildingDoorRoomPolicyAdapter(
        ICharacterAiWorldRegistry worldRegistry,
        IRoomLayoutCache roomLayoutCache)
    {
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.roomLayoutCache = roomLayoutCache
            ?? throw new ArgumentNullException(nameof(roomLayoutCache));
    }

    public int ApplyToRoomDoors(
        IBuildingDoorAccessPolicyPort source,
        object policy)
    {
        if (source is not DoorAccessPolicyPortAdapter sourceAdapter
            || sourceAdapter.Door == null
            || policy is not DoorAccessPolicyState typedPolicy
            || !TryFindAdjacentRoom(sourceAdapter.Door, out RoomInstance room))
        {
            return 0;
        }

        int changed = 0;
        foreach (BuildableObject building in worldRegistry.Buildings)
        {
            if (building is not Door door
                || door == sourceAdapter.Door
                || door.Grid != sourceAdapter.Door.Grid
                || door.AccessStateModule == null
                || !TouchesRoom(door, room))
            {
                continue;
            }

            door.AccessStateModule.CopyFrom(typedPolicy);
            changed++;
        }

        return changed;
    }

    private bool TryFindAdjacentRoom(Door door, out RoomInstance room)
    {
        room = null;
        Grid grid = door.Grid;
        if (grid == null)
        {
            return false;
        }

        foreach (Vector2Int position in door.buildPoses)
        {
            if (roomLayoutCache.TryGetRoom(grid, position + Vector2Int.left, out room)
                && room != null)
            {
                return true;
            }

            if (roomLayoutCache.TryGetRoom(grid, position + Vector2Int.right, out room)
                && room != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TouchesRoom(Door door, RoomInstance room)
    {
        foreach (Vector2Int position in door.buildPoses)
        {
            if (room.ContainsCell(position + Vector2Int.left)
                || room.ContainsCell(position + Vector2Int.right))
            {
                return true;
            }
        }

        return false;
    }
}
