using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Flags]
public enum DoorAccessGroup
{
    None = 0,
    Owner = 1 << 0,
    Staff = 1 << 1,
    Customer = 1 << 2,
    Captive = 1 << 3,
    Intruder = 1 << 4,
    Wildlife = 1 << 5,
    CaptiveWildlife = 1 << 6,
    All = Owner
        | Staff
        | Customer
        | Captive
        | Intruder
        | Wildlife
        | CaptiveWildlife
}

public enum DoorAccessIndividualRule
{
    GroupDefault = 0,
    Allow = 1,
    Deny = 2
}

public enum DoorAccessPreset
{
    AllowAll = 0,
    StaffOnly = 1,
    CustomerArea = 2,
    Cell = 3,
    AnimalPen = 4
}

[Serializable]
public sealed class DoorAccessPolicyState
{
    [SerializeField] private int allowedGroups = (int)DoorAccessGroup.All;
    [SerializeField] private List<string> individuallyAllowedIds = new List<string>();
    [SerializeField] private List<string> individuallyDeniedIds = new List<string>();

    public DoorAccessGroup AllowedGroups
    {
        get => (DoorAccessGroup)allowedGroups & DoorAccessGroup.All;
        set => allowedGroups = (int)(value & DoorAccessGroup.All);
    }

    public IReadOnlyList<string> IndividuallyAllowedIds =>
        individuallyAllowedIds ??= new List<string>();
    public IReadOnlyList<string> IndividuallyDeniedIds =>
        individuallyDeniedIds ??= new List<string>();
    public bool IsRestricted => AllowedGroups != DoorAccessGroup.All
        || IndividuallyDeniedIds.Count > 0;

    public bool IsGroupAllowed(DoorAccessGroup group)
    {
        return group != DoorAccessGroup.None && (AllowedGroups & group) == group;
    }

    public DoorAccessIndividualRule GetIndividualRule(string persistentId)
    {
        string normalized = NormalizeId(persistentId);
        if (normalized.Length == 0)
        {
            return DoorAccessIndividualRule.GroupDefault;
        }

        if (Contains(individuallyDeniedIds, normalized))
        {
            return DoorAccessIndividualRule.Deny;
        }

        return Contains(individuallyAllowedIds, normalized)
            ? DoorAccessIndividualRule.Allow
            : DoorAccessIndividualRule.GroupDefault;
    }

    public bool SetGroupAllowed(DoorAccessGroup group, bool allowed)
    {
        DoorAccessGroup validGroup = group & DoorAccessGroup.All;
        if (validGroup == DoorAccessGroup.None)
        {
            return false;
        }

        DoorAccessGroup previous = AllowedGroups;
        AllowedGroups = allowed
            ? previous | validGroup
            : previous & ~validGroup;
        return previous != AllowedGroups;
    }

    public bool SetIndividualRule(
        string persistentId,
        DoorAccessIndividualRule rule)
    {
        string normalized = NormalizeId(persistentId);
        if (normalized.Length == 0)
        {
            return false;
        }

        individuallyAllowedIds ??= new List<string>();
        individuallyDeniedIds ??= new List<string>();
        DoorAccessIndividualRule previous = GetIndividualRule(normalized);
        Remove(individuallyAllowedIds, normalized);
        Remove(individuallyDeniedIds, normalized);
        if (rule == DoorAccessIndividualRule.Allow)
        {
            individuallyAllowedIds.Add(normalized);
        }
        else if (rule == DoorAccessIndividualRule.Deny)
        {
            individuallyDeniedIds.Add(normalized);
        }

        Normalize();
        return previous != rule;
    }

    public void ApplyPreset(DoorAccessPreset preset)
    {
        AllowedGroups = preset switch
        {
            DoorAccessPreset.StaffOnly =>
                DoorAccessGroup.Owner | DoorAccessGroup.Staff,
            DoorAccessPreset.CustomerArea =>
                DoorAccessGroup.Owner
                | DoorAccessGroup.Staff
                | DoorAccessGroup.Customer,
            DoorAccessPreset.Cell =>
                DoorAccessGroup.Owner | DoorAccessGroup.Staff,
            DoorAccessPreset.AnimalPen =>
                DoorAccessGroup.Owner
                | DoorAccessGroup.Staff
                | DoorAccessGroup.CaptiveWildlife,
            _ => DoorAccessGroup.All
        };
        individuallyAllowedIds?.Clear();
        individuallyDeniedIds?.Clear();
    }

    public void CopyFrom(DoorAccessPolicyState source)
    {
        AllowedGroups = source?.AllowedGroups ?? DoorAccessGroup.All;
        individuallyAllowedIds = source == null
            ? new List<string>()
            : source.IndividuallyAllowedIds
                .Select(NormalizeId)
                .Where(id => id.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        individuallyDeniedIds = source == null
            ? new List<string>()
            : source.IndividuallyDeniedIds
                .Select(NormalizeId)
                .Where(id => id.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        Normalize();
    }

    public DoorAccessPolicyState Clone()
    {
        DoorAccessPolicyState clone = new DoorAccessPolicyState();
        clone.CopyFrom(this);
        return clone;
    }

    public void Normalize()
    {
        AllowedGroups &= DoorAccessGroup.All;
        individuallyAllowedIds = NormalizeIds(individuallyAllowedIds);
        individuallyDeniedIds = NormalizeIds(individuallyDeniedIds);
        if (individuallyDeniedIds.Count > 0 && individuallyAllowedIds.Count > 0)
        {
            HashSet<string> denied = new HashSet<string>(
                individuallyDeniedIds,
                StringComparer.Ordinal);
            individuallyAllowedIds.RemoveAll(denied.Contains);
        }
    }

    private static List<string> NormalizeIds(IEnumerable<string> source)
    {
        return (source ?? Array.Empty<string>())
            .Select(NormalizeId)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    private static bool Contains(IEnumerable<string> values, string id)
    {
        return values != null
            && values.Any(value => string.Equals(
                NormalizeId(value),
                id,
                StringComparison.Ordinal));
    }

    private static void Remove(List<string> values, string id)
    {
        values?.RemoveAll(value => string.Equals(
            NormalizeId(value),
            id,
            StringComparison.Ordinal));
    }

    private static string NormalizeId(string value)
    {
        return value?.Trim() ?? string.Empty;
    }
}

public readonly struct DoorAccessSubjectRef : IEquatable<DoorAccessSubjectRef>
{
    public DoorAccessSubjectRef(
        string persistentId,
        DoorAccessGroup group,
        UnityEngine.Object character = null,
        UnityEngine.Object wildlife = null)
    {
        PersistentId = persistentId?.Trim() ?? string.Empty;
        Group = group;
        Character = character;
        Wildlife = wildlife;
    }

    public string PersistentId { get; }
    public DoorAccessGroup Group { get; }
    public UnityEngine.Object Character { get; }
    public UnityEngine.Object Wildlife { get; }
    public bool IsValid => PersistentId.Length > 0 && Group != DoorAccessGroup.None;

    public bool Equals(DoorAccessSubjectRef other)
    {
        return string.Equals(PersistentId, other.PersistentId, StringComparison.Ordinal)
            && Group == other.Group;
    }

    public override bool Equals(object obj)
    {
        return obj is DoorAccessSubjectRef other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((PersistentId != null
                ? StringComparer.Ordinal.GetHashCode(PersistentId)
                : 0) * 397) ^ (int)Group;
        }
    }
}

public interface IDoorAccessQuery : IGridTraversalAccessQuery
{
    DoorAccessSubjectRef ResolveSubject(GridTraversalContext context);
    bool CanUse(
        Door door,
        GridTraversalContext context,
        out string denialReason);
}

public interface IDoorAccessCommandService
{
    bool SetGroupAllowed(Door door, DoorAccessGroup group, bool allowed);
    bool SetIndividualRule(
        Door door,
        string persistentId,
        DoorAccessIndividualRule rule);
    bool ApplyPreset(Door door, DoorAccessPreset preset);
    bool CopyPolicy(Door source);
    bool PastePolicy(Door destination);
    int ApplyPolicyToRoomDoors(Door source);
    IDisposable BeginTemporaryOverride(
        DoorAccessSubjectRef subject,
        DoorAccessOverrideKind kind,
        string scopeId);
}

public interface IDoorAccessSubjectRegistry
{
    void SetCaptive(string persistentId, bool captive);
    void SetCapturedWildlife(string wildlifeId, bool captured);
    void ReplaceCaptiveSubjects(IEnumerable<string> persistentIds);
    void ReplaceCapturedWildlifeSubjects(IEnumerable<string> wildlifeIds);
}

public interface IDoorAccessStateChangeSink
{
    void NotifyDoorPolicyChanged();
}
