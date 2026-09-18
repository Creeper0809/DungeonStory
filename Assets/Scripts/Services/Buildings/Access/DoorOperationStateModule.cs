using System;
using UnityEngine;

// Access permissions remain in door.access; this module owns only the leaf state.
public sealed class DoorOperationStateModule : IBuildingStateModule
{
    public const string StateModuleId = "door.operation";
    private readonly Action changed;
    private int state = 1; // 1 closed, 2 transient open, 3 held open; zero is invalid.
    public DoorOperationStateModule(Action changed) => this.changed = changed
        ?? throw new ArgumentNullException(nameof(changed));
    public string ModuleId => StateModuleId;
    public int CurrentVersion => 1;
    public bool IsOpen => state != 1;
    public bool IsHeldOpen => state == 3;
    public int Revision { get; private set; }

    [GameplayInternalOnly("Physical door operation authority", "Door;DoorOperationStateModule.TryRestoreState")]
    internal void Set(bool open, bool held)
    {
        int next = held ? 3 : open ? 2 : 1;
        if (state == next) return;
        state = next;
        Revision = unchecked(Revision + 1);
        changed();
    }

    public string CaptureState() => JsonUtility.ToJson(new Payload { state = state });
    [GameplayInternalOnly("Existing building module candidate restore", "BuildingStateModulePersistence")]
    public bool TryRestoreState(int version, string payload, out string error)
    {
        error = string.Empty;
        if (version != CurrentVersion) { error = "door-operation-version"; return false; }
        Payload restored;
        try { restored = JsonUtility.FromJson<Payload>(payload); }
        catch (Exception ex) { error = ex.Message; return false; }
        if (restored == null || restored.state < 1 || restored.state > 3)
        { error = "door-operation-state-required"; return false; }
        Set(restored.state != 1, restored.state == 3);
        return true;
    }
    [Serializable] private sealed class Payload { public int state; }
}
