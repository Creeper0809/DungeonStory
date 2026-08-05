using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

public readonly struct BuildingDoorAccessSubject
{
    public BuildingDoorAccessSubject(string id, int group, Object runtime)
    { PersistentId = id ?? string.Empty; Group = group; Runtime = runtime; }
    public string PersistentId { get; }
    public int Group { get; }
    public Object Runtime { get; }
    public bool IsValid => PersistentId.Length > 0 && Group != 0;
}

public interface IBuildingDoorAccessPolicyPort
{
    bool IsDestroyed { get; }
    int GetIndividualRule(string id);
    bool IsGroupAllowed(int group);
    bool SetGroupAllowed(int group, bool allowed);
    bool SetIndividualRule(string id, int rule);
    void ApplyPreset(int preset);
    object CapturePolicy();
    bool RestorePolicy(object policy);
}

public interface IBuildingDoorRoomPolicyPort
{
    int ApplyToRoomDoors(IBuildingDoorAccessPolicyPort source, object policy);
}

public sealed class DoorAccessService
{
    private readonly IBuildingDoorAccessSubjectPort subjects;
    private readonly IBuildingDoorPolicyInvalidationPort invalidation;
    private readonly IBuildingDoorRoomPolicyPort rooms;
    private readonly DoorAccessSubjectAggregateStateStore state;
    private readonly Dictionary<string, int> overrides = new(StringComparer.Ordinal);
    private object clipboard;
    private int version;

    public DoorAccessService(IBuildingDoorAccessSubjectPort subjects, IBuildingDoorPolicyInvalidationPort invalidation, IBuildingDoorRoomPolicyPort rooms, DungeonRuntimeAggregateRootStore roots)
    {
        this.subjects = subjects ?? throw new ArgumentNullException(nameof(subjects));
        this.invalidation = invalidation ?? throw new ArgumentNullException(nameof(invalidation));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        state = new DoorAccessSubjectAggregateStateStore(roots);
    }

    public int DoorAccessVersion => unchecked(version + state.PublishedRestoreRevision);

    public BuildingDoorAccessSubject ResolveSubject(Object runtime)
    {
        if (!subjects.TryResolveDoorAccessSubject(runtime, out BuildingDoorAccessSubjectSnapshot value) || !value.IsValid) return default;
        string id = value.PersistentId;
        int group = value.Kind switch
        {
            BuildingDoorAccessSubjectKind.Owner => 1,
            BuildingDoorAccessSubjectKind.Customer => 4,
            BuildingDoorAccessSubjectKind.Intruder => 16,
            BuildingDoorAccessSubjectKind.Wildlife => state.State.CapturedWildlifeIds.Contains(id) ? 64 : 32,
            _ => state.State.CaptiveIds.Contains(id) ? 8 : 2
        };
        return new BuildingDoorAccessSubject(id, group, value.RuntimeSubject);
    }

    public bool CanUse(IBuildingDoorAccessPolicyPort door, Object runtime, int overrideKind, out string denial)
    {
        denial = string.Empty;
        if (door == null || door.IsDestroyed) return true;
        BuildingDoorAccessSubject subject = ResolveSubject(runtime);
        if (!subject.IsValid || overrideKind != 0 || HasOverride(subject.PersistentId)) return true;
        int individual = door.GetIndividualRule(subject.PersistentId);
        if (individual == 2) { denial = "문 권한에서 개별 차단됨"; return false; }
        if (individual == 1 || door.IsGroupAllowed(subject.Group)) return true;
        denial = "출입이 허용되지 않음";
        return false;
    }

    public bool SetGroupAllowed(IBuildingDoorAccessPolicyPort door, int group, bool allowed) => door != null && door.SetGroupAllowed(group, allowed);
    public bool SetIndividualRule(IBuildingDoorAccessPolicyPort door, string id, int rule) => door != null && door.SetIndividualRule(id, rule);
    public bool ApplyPreset(IBuildingDoorAccessPolicyPort door, int preset) { if (door == null) return false; door.ApplyPreset(preset); return true; }
    public bool CopyPolicy(IBuildingDoorAccessPolicyPort door) { clipboard = door?.CapturePolicy(); return clipboard != null; }
    public bool PastePolicy(IBuildingDoorAccessPolicyPort door) => door != null && clipboard != null && door.RestorePolicy(clipboard);
    public int ApplyPolicyToRoomDoors(IBuildingDoorAccessPolicyPort door) => door == null ? 0 : rooms.ApplyToRoomDoors(door, door.CapturePolicy());

    public IDisposable BeginTemporaryOverride(BuildingDoorAccessSubject subject, int kind, string scope)
    {
        if (!subject.IsValid || kind == 0) return new Token(null, string.Empty);
        string key = $"{subject.PersistentId}|{kind}|{scope?.Trim() ?? string.Empty}";
        overrides.TryGetValue(key, out int count); overrides[key] = count + 1; Changed();
        return new Token(this, key);
    }

    public void SetCaptive(string id, bool included) => SetMembership(state.State.CaptiveIds, id, included);
    public void SetCapturedWildlife(string id, bool included) => SetMembership(state.State.CapturedWildlifeIds, id, included);
    public void ReplaceCaptiveSubjects(IEnumerable<string> ids) { if (state.ReplaceCaptives(ids) && !state.IsRestoreStaging) Changed(); }
    public void ReplaceCapturedWildlifeSubjects(IEnumerable<string> ids) { if (state.ReplaceCapturedWildlife(ids) && !state.IsRestoreStaging) Changed(); }
    public void NotifyDoorPolicyChanged() => Changed();

    private bool HasOverride(string id) { foreach (string key in overrides.Keys) if (key.StartsWith(id + "|", StringComparison.Ordinal)) return true; return false; }
    private void SetMembership(HashSet<string> set, string id, bool value) { string key = id?.Trim() ?? string.Empty; if (key.Length > 0 && (value ? set.Add(key) : set.Remove(key))) Changed(); }
    private void Changed() { version++; invalidation.InvalidateDoorPolicyPaths(); }
    private void Release(string key) { if (!overrides.TryGetValue(key, out int count)) return; if (count <= 1) overrides.Remove(key); else overrides[key] = count - 1; Changed(); }

    private sealed class Token : IDisposable
    {
        private DoorAccessService owner; private readonly string key;
        public Token(DoorAccessService owner, string key) { this.owner = owner; this.key = key; }
        public void Dispose() { DoorAccessService current = owner; owner = null; current?.Release(key); }
    }
}
