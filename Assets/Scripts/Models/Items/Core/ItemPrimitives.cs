using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorldItemStackState
{
    Loose = 0,
    Stored = 1,
    FacilityBuffer = 2,
    Carried = 3,
    ExpeditionPacked = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WasteOriginKind
{
    Unknown = 0,
    Plant = 1,
    Animal = 2,
    Mixed = 3,
    Forbidden = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct EquipmentStoredEvent
{
    public EquipmentStoredEvent(string equipmentId, int quantity)
    {
        EquipmentId = equipmentId?.Trim() ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
    }

    public string EquipmentId { get; }
    public int Quantity { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ItemHaulingSettingsSnapshot
{
    public float maxCarryMultiplier = 1.5f;

    public void Normalize()
    {
        maxCarryMultiplier = Mathf.Clamp(maxCarryMultiplier, 1f, 2.5f);
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonPhysicalItemSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public int nextStackSequence = 1;
    public ItemHaulingSettingsSnapshot haulingSettings =
        new ItemHaulingSettingsSnapshot();
    public List<WorldItemStackSaveData> stacks =
        new List<WorldItemStackSaveData>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WorldItemStackSaveData
{
    public string stackId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public WorldItemStackState state = WorldItemStackState.Loose;
    public int gridX;
    public int gridY;
    public string reservedByPersistentId = string.Empty;
    public string destinationId = string.Empty;
    public string sourceStorageDestinationId = string.Empty;
    public bool hasDestinationPosition;
    public int destinationGridX;
    public int destinationGridY;
    public bool forbidden;
    public string sourceCharacterId = string.Empty;
    public string sourceDisplayName = string.Empty;
    public string sourceSpeciesTag = string.Empty;
    public string sourceDeathReason = string.Empty;
    public bool emergencyButcheryAllowed;
    public WasteOriginKind wasteOrigin;
    [Range(0f, 100f)] public float contamination;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorldItemHaulDestinationKind
{
    Warehouse = 0,
    FacilityBuffer = 1
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorldItemHaulPlanUnloadReason
{
    None = 0,
    LoadLimitReached = 1,
    NoPickupCandidate = 2,
    JobChanged = 3,
    Idle = 4,
    Interrupted = 5,
    Completed = 6
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCarriedItemSaveData
{
    public string sourceStackId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public WasteOriginKind wasteOrigin;
    [Range(0f, 100f)] public float contamination;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCarryInventorySaveData
{
    public List<CharacterCarriedItemSaveData> items =
        new List<CharacterCarriedItemSaveData>();
}
