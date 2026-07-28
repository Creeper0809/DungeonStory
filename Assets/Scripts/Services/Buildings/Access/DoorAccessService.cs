using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DoorAccessService :
    IDoorAccessQuery,
    IDoorAccessCommandService,
    IDoorAccessSubjectRegistry,
    IDoorAccessStateChangeSink
{
    private sealed class OverrideToken : IDisposable
    {
        private DoorAccessService owner;
        private readonly string key;
        private readonly OverrideSubjectKey subjectKey;

        public OverrideToken(
            DoorAccessService owner,
            string key,
            OverrideSubjectKey subjectKey)
        {
            this.owner = owner;
            this.key = key;
            this.subjectKey = subjectKey;
        }

        public void Dispose()
        {
            DoorAccessService current = owner;
            owner = null;
            current?.ReleaseOverride(key, subjectKey);
        }
    }

    private readonly struct OverrideSubjectKey :
        IEquatable<OverrideSubjectKey>
    {
        public OverrideSubjectKey(
            string persistentId,
            DoorAccessOverrideKind kind)
        {
            PersistentId = persistentId ?? string.Empty;
            Kind = kind;
        }

        private string PersistentId { get; }
        private DoorAccessOverrideKind Kind { get; }

        public bool Equals(OverrideSubjectKey other)
        {
            return Kind == other.Kind
                && string.Equals(
                    PersistentId,
                    other.PersistentId,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is OverrideSubjectKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((PersistentId != null
                    ? StringComparer.Ordinal.GetHashCode(PersistentId)
                    : 0) * 397) ^ (int)Kind;
            }
        }
    }

    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IRoomLayoutCache roomLayoutCache;
    private readonly HashSet<string> captiveIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> capturedWildlifeIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> activeOverrides =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<OverrideSubjectKey, int> activeOverrideCounts =
        new Dictionary<OverrideSubjectKey, int>();
    private DoorAccessPolicyState clipboard;

    public DoorAccessService(
        ICharacterAiWorldRegistry worldRegistry,
        IRoomLayoutCache roomLayoutCache = null)
    {
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.roomLayoutCache = roomLayoutCache;
    }

    public int DoorAccessVersion { get; private set; }

    public DoorAccessSubjectRef ResolveSubject(GridTraversalContext context)
    {
        if (context.Character != null)
        {
            CharacterActor actor = context.Character;
            string id = GetCharacterId(actor);
            DoorAccessGroup group;
            if (actor.IsOwner)
            {
                group = DoorAccessGroup.Owner;
            }
            else if (captiveIds.Contains(id))
            {
                group = DoorAccessGroup.Captive;
            }
            else
            {
                group = actor.characterType switch
                {
                    CharacterType.Intruder => DoorAccessGroup.Intruder,
                    CharacterType.Customer => DoorAccessGroup.Customer,
                    _ => DoorAccessGroup.Staff
                };
            }

            return new DoorAccessSubjectRef(id, group, character: actor);
        }

        if (context.Wildlife != null)
        {
            WildlifeActor wildlife = context.Wildlife;
            string id = wildlife.WildlifeId?.Trim() ?? string.Empty;
            if (id.Length == 0)
            {
                id = $"wildlife-instance:{wildlife.GetInstanceID()}";
            }

            DoorAccessGroup group = capturedWildlifeIds.Contains(id)
                ? DoorAccessGroup.CaptiveWildlife
                : DoorAccessGroup.Wildlife;
            return new DoorAccessSubjectRef(id, group, wildlife: wildlife);
        }

        return default;
    }

    public bool CanUse(
        Door door,
        GridTraversalContext context,
        out string denialReason)
    {
        denialReason = string.Empty;
        if (door == null || door.isDestroy)
        {
            return true;
        }

        DoorAccessSubjectRef subject = ResolveSubject(context);
        if (!subject.IsValid)
        {
            return true;
        }

        if (IsBypass(context.OverrideKind)
            || HasActiveOverride(subject, DoorAccessOverrideKind.DirectCommand)
            || HasActiveOverride(subject, DoorAccessOverrideKind.EscortPass)
            || HasActiveOverride(subject, DoorAccessOverrideKind.CaptiveEscape)
            || HasActiveOverride(subject, DoorAccessOverrideKind.IntruderBreach))
        {
            return true;
        }

        DoorAccessPolicyState policy = door.AccessPolicy;
        if (policy == null)
        {
            return true;
        }

        DoorAccessIndividualRule individual =
            policy.GetIndividualRule(subject.PersistentId);
        if (individual == DoorAccessIndividualRule.Deny)
        {
            denialReason = "문 권한에서 개별 차단됨";
            return false;
        }

        if (individual == DoorAccessIndividualRule.Allow)
        {
            return true;
        }

        if (policy.IsGroupAllowed(subject.Group))
        {
            return true;
        }

        denialReason = $"{GetGroupLabel(subject.Group)} 출입이 허용되지 않음";
        return false;
    }

    public bool CanTraverse(
        Grid grid,
        Vector2Int position,
        GridTraversalContext context,
        out string denialReason)
    {
        denialReason = string.Empty;
        Door door = grid?.GetGridCell(position)
            ?.GetOccupant(GridLayer.Building) as Door;
        return door == null || CanUse(door, context, out denialReason);
    }

    public bool SetGroupAllowed(
        Door door,
        DoorAccessGroup group,
        bool allowed)
    {
        return door != null
            && door.AccessStateModule != null
            && door.AccessStateModule.SetGroupAllowed(group, allowed);
    }

    public bool SetIndividualRule(
        Door door,
        string persistentId,
        DoorAccessIndividualRule rule)
    {
        return door != null
            && door.AccessStateModule != null
            && door.AccessStateModule.SetIndividualRule(persistentId, rule);
    }

    public bool ApplyPreset(Door door, DoorAccessPreset preset)
    {
        if (door?.AccessStateModule == null)
        {
            return false;
        }

        door.AccessStateModule.ApplyPreset(preset);
        return true;
    }

    public bool CopyPolicy(Door source)
    {
        if (source?.AccessPolicy == null)
        {
            return false;
        }

        clipboard = source.AccessPolicy.Clone();
        return true;
    }

    public bool PastePolicy(Door destination)
    {
        if (destination?.AccessStateModule == null || clipboard == null)
        {
            return false;
        }

        destination.AccessStateModule.CopyFrom(clipboard);
        return true;
    }

    public int ApplyPolicyToRoomDoors(Door source)
    {
        if (source?.Grid == null
            || source.AccessPolicy == null
            || roomLayoutCache == null
            || !TryFindAdjacentRoom(source, out RoomInstance room))
        {
            return 0;
        }

        int changed = 0;
        foreach (BuildableObject building in worldRegistry.Buildings)
        {
            if (building is not Door door
                || door == source
                || door.Grid != source.Grid
                || door.AccessStateModule == null
                || !TouchesRoom(door, room))
            {
                continue;
            }

            door.AccessStateModule.CopyFrom(source.AccessPolicy);
            changed++;
        }

        return changed;
    }

    public IDisposable BeginTemporaryOverride(
        DoorAccessSubjectRef subject,
        DoorAccessOverrideKind kind,
        string scopeId)
    {
        if (!subject.IsValid || !IsBypass(kind))
        {
            return new OverrideToken(
                null,
                string.Empty,
                default);
        }

        string key = BuildOverrideKey(subject.PersistentId, kind, scopeId);
        OverrideSubjectKey subjectKey = new OverrideSubjectKey(
            subject.PersistentId,
            kind);
        activeOverrides.TryGetValue(key, out int count);
        activeOverrides[key] = count + 1;
        activeOverrideCounts.TryGetValue(subjectKey, out int subjectCount);
        activeOverrideCounts[subjectKey] = subjectCount + 1;
        NotifyDoorPolicyChanged();
        return new OverrideToken(this, key, subjectKey);
    }

    public void SetCaptive(string persistentId, bool captive)
    {
        SetMembership(captiveIds, persistentId, captive);
    }

    public void SetCapturedWildlife(string wildlifeId, bool captured)
    {
        SetMembership(capturedWildlifeIds, wildlifeId, captured);
    }

    public void NotifyDoorPolicyChanged()
    {
        DoorAccessVersion++;
        foreach (CharacterActor actor in worldRegistry.Characters)
        {
            if (actor?.Brain == null)
            {
                continue;
            }

            actor.Brain.ClearPathSearchCache();
            if (actor.CanRunAi && !actor.Brain.IsManualCommandActive)
            {
                actor.Brain.RequestImmediateReplan();
            }
        }
    }

    private bool HasActiveOverride(
        DoorAccessSubjectRef subject,
        DoorAccessOverrideKind kind)
    {
        return activeOverrideCounts.TryGetValue(
                new OverrideSubjectKey(subject.PersistentId, kind),
                out int count)
            && count > 0;
    }

    private void ReleaseOverride(
        string key,
        OverrideSubjectKey subjectKey)
    {
        if (string.IsNullOrWhiteSpace(key)
            || !activeOverrides.TryGetValue(key, out int count))
        {
            return;
        }

        if (count <= 1)
        {
            activeOverrides.Remove(key);
        }
        else
        {
            activeOverrides[key] = count - 1;
        }

        if (activeOverrideCounts.TryGetValue(
                subjectKey,
                out int subjectCount))
        {
            if (subjectCount <= 1)
            {
                activeOverrideCounts.Remove(subjectKey);
            }
            else
            {
                activeOverrideCounts[subjectKey] = subjectCount - 1;
            }
        }

        NotifyDoorPolicyChanged();
    }

    private void SetMembership(
        HashSet<string> set,
        string persistentId,
        bool included)
    {
        string normalized = persistentId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return;
        }

        bool changed = included
            ? set.Add(normalized)
            : set.Remove(normalized);
        if (changed)
        {
            NotifyDoorPolicyChanged();
        }
    }

    private static bool IsBypass(DoorAccessOverrideKind kind)
    {
        return kind != DoorAccessOverrideKind.None;
    }

    private static string BuildOverrideKey(
        string persistentId,
        DoorAccessOverrideKind kind,
        string scopeId)
    {
        return $"{persistentId}|{kind}|{scopeId?.Trim() ?? string.Empty}";
    }

    private static string GetCharacterId(CharacterActor actor)
    {
        string id = actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
        if (id.Length == 0 && actor != null)
        {
            id = $"character-instance:{actor.GetInstanceID()}";
        }

        return id;
    }

    private static string GetGroupLabel(DoorAccessGroup group)
    {
        return group switch
        {
            DoorAccessGroup.Owner => "사장",
            DoorAccessGroup.Staff => "직원",
            DoorAccessGroup.Customer => "손님",
            DoorAccessGroup.Captive => "포로",
            DoorAccessGroup.Intruder => "침입자",
            DoorAccessGroup.Wildlife => "야생동물",
            DoorAccessGroup.CaptiveWildlife => "포획 동물",
            _ => "대상"
        };
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
            Vector2Int[] adjacent =
            {
                position + Vector2Int.left,
                position + Vector2Int.right
            };
            foreach (Vector2Int candidate in adjacent)
            {
                if (roomLayoutCache.TryGetRoom(grid, candidate, out room)
                    && room != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TouchesRoom(Door door, RoomInstance room)
    {
        if (door == null || room == null)
        {
            return false;
        }

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
