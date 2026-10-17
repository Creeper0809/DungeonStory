using System;
using UnityEngine;

[Serializable]
public sealed class DoorHeldOpenPendingSaveData
{
    public const string PolicyStatusKey = "door.operation.held-open";
    public const string PlayerPanelOrigin = "player-ui:door-access-panel";

    public string doorId;
    public string operationId;
    public string commandOrigin;
    public long revision;
    public int absoluteDay;
    public int positionX;
    public int positionY;
    public bool beforeOpen;
    public bool beforeHeld;
    public bool afterOpen;
    public bool afterHeld;

    public DoorHeldOpenPendingSaveData Clone() =>
        (DoorHeldOpenPendingSaveData)MemberwiseClone();

    public static string BuildOperationId(string doorId, long revision) =>
        "door-held-open:" + doorId + ":" + revision;

    public static bool IsValid(DoorHeldOpenPendingSaveData row) =>
        row != null
        && GameplayOutcomeStableIdSyntax.IsValid(row.doorId)
        && GameplayOutcomeStableIdSyntax.IsValid(row.commandOrigin)
        && GameplayOutcomeStableIdSyntax.IsValid(row.operationId)
        && row.revision > 0L
        && row.absoluteDay >= 0
        && string.Equals(
            row.operationId,
            BuildOperationId(row.doorId, row.revision),
            StringComparison.Ordinal)
        && row.beforeHeld != row.afterHeld
        && (!row.beforeHeld || row.beforeOpen)
        && (!row.afterHeld || row.afterOpen);
}

// Access permissions remain in door.access; this module owns only the leaf state.
public sealed class DoorOperationStateModule : IBuildingStateModule
{
    public const string StateModuleId = "door.operation";
    private readonly Action changed;
    private int state = 1; // 1 closed, 2 transient open, 3 held open; zero is invalid.
    public DoorOperationStateModule(Action changed) => this.changed = changed
        ?? throw new ArgumentNullException(nameof(changed));
    public string ModuleId => StateModuleId;
    public int CurrentVersion => 3;
    public bool IsOpen => state != 1;
    public bool IsHeldOpen => state == 3;
    public int Revision { get; private set; }
    public long ManualRevision { get; private set; }
    public DoorHeldOpenPendingSaveData PendingManualOutcome => pending?.Clone();
    private DoorHeldOpenPendingSaveData pending;

    [GameplayInternalOnly("Physical door operation authority", "Door;DoorOperationStateModule.TryRestoreState")]
    internal void Set(bool open, bool held)
    {
        int next = held ? 3 : open ? 2 : 1;
        if (state == next) return;
        state = next;
        Revision = unchecked(Revision + 1);
        changed();
    }

    internal bool TryApplyManualChange(
        bool open,
        bool held,
        DoorHeldOpenPendingSaveData next)
    {
        if (!DoorHeldOpenPendingSaveData.IsValid(next)
            || pending != null
            || ManualRevision == long.MaxValue
            || next.revision != ManualRevision + 1L
            || next.beforeOpen != IsOpen
            || next.beforeHeld != IsHeldOpen
            || next.afterOpen != open
            || next.afterHeld != held
            || next.beforeHeld == held)
        {
            return false;
        }

        state = held ? 3 : open ? 2 : 1;
        ManualRevision = next.revision;
        pending = next.Clone();
        Revision = unchecked(Revision + 1);
        changed();
        return true;
    }

    internal bool AcknowledgeManualOutcome(string operationId)
    {
        if (pending == null || !string.Equals(
                pending.operationId,
                operationId,
                StringComparison.Ordinal))
        {
            return false;
        }
        pending = null;
        return true;
    }

    public string CaptureState() => JsonUtility.ToJson(new Payload
    {
        state = state,
        manualRevision = ManualRevision,
        hasPending = pending != null,
        pending = pending?.Clone()
    });
    [GameplayInternalOnly("Existing building module candidate restore", "BuildingStateModulePersistence")]
    public bool TryRestoreState(int version, string payload, out string error)
    {
        error = string.Empty;
        if (version < 1 || version > CurrentVersion)
        { error = "door-operation-version"; return false; }
        Payload restored;
        try { restored = JsonUtility.FromJson<Payload>(payload); }
        catch (Exception ex) { error = ex.Message; return false; }
        if (restored == null || restored.state < 1 || restored.state > 3)
        { error = "door-operation-state-required"; return false; }
        if (version == 1)
        {
            Set(restored.state != 1, restored.state == 3);
            ManualRevision = 0L;
            pending = null;
            return true;
        }
        if (version == 2
            && restored.manualRevision == 0L
            && IsJsonUtilityDefaultPending(restored.pending))
        {
            restored.pending = null;
        }
        if (version >= 3)
        {
            if (restored.hasPending && restored.pending == null)
            {
                error = "door-operation-manual-outcome-presence-invalid";
                return false;
            }
            if (!restored.hasPending)
            {
                restored.pending = null;
            }
        }
        if (restored.manualRevision < 0L
            || restored.pending != null
            && (!DoorHeldOpenPendingSaveData.IsValid(restored.pending)
                || restored.pending.revision != restored.manualRevision
                || restored.pending.afterHeld != (restored.state == 3)))
        {
            error = "door-operation-manual-outcome-invalid";
            return false;
        }
        Set(restored.state != 1, restored.state == 3);
        ManualRevision = restored.manualRevision;
        pending = restored.pending?.Clone();
        return true;
    }

    private static bool IsJsonUtilityDefaultPending(
        DoorHeldOpenPendingSaveData row) =>
        row != null
        && string.IsNullOrEmpty(row.doorId)
        && string.IsNullOrEmpty(row.operationId)
        && string.IsNullOrEmpty(row.commandOrigin)
        && row.revision == 0L
        && row.absoluteDay == 0
        && row.positionX == 0
        && row.positionY == 0
        && !row.beforeOpen
        && !row.beforeHeld
        && !row.afterOpen
        && !row.afterHeld;

    [Serializable] private sealed class Payload
    {
        public int state;
        public long manualRevision;
        public bool hasPending;
        public DoorHeldOpenPendingSaveData pending;
    }
}
