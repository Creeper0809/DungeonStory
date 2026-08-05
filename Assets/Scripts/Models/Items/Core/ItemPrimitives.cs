using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorldItemStackState
{
    Loose = 0,
    Stored = 1,
    FacilityBuffer = 2,
    Carried = 3,
    ExpeditionPacked = 4,
    FacilityOutputBuffer = 5,
    InTransit = 6
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
    public const int CurrentVersion = 6;

    public int version = CurrentVersion;
    public ItemHaulingSettingsSnapshot haulingSettings =
        new ItemHaulingSettingsSnapshot();
    public List<WorldItemStackSaveData> stacks =
        new List<WorldItemStackSaveData>();
    public List<UniqueItemInstanceSaveData> uniqueItems = new();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class UniqueItemInstanceSaveData
{
    public string itemInstanceId = string.Empty;
    public string definitionId = string.Empty;
    public List<ItemInstanceComponentSaveData> components = new();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WorldItemStackSaveData
{
    public string stackId = string.Empty;
    public string itemInstanceId = string.Empty;
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
    public List<ItemInstanceComponentSaveData> components =
        new List<ItemInstanceComponentSaveData>();

    public string GetStackSignature() =>
        ItemStackSignature.Create(itemId, components);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ItemStateValueKind
{
    String = 0,
    Integer = 1,
    Decimal = 2,
    Boolean = 3
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ItemStateValueSaveData
{
    public string key = string.Empty;
    public ItemStateValueKind kind;
    public string stringValue = string.Empty;
    public long integerValue;
    public double decimalValue;
    public bool booleanValue;

    public string ToCanonicalString()
    {
        string value = kind switch
        {
            ItemStateValueKind.Integer => integerValue.ToString(CultureInfo.InvariantCulture),
            ItemStateValueKind.Decimal => decimalValue.ToString("R", CultureInfo.InvariantCulture),
            ItemStateValueKind.Boolean => booleanValue ? "1" : "0",
            _ => stringValue?.Trim() ?? string.Empty
        };
        return $"{key?.Trim()}={Convert.ToInt32(kind, CultureInfo.InvariantCulture)}:{value}";
    }
}

/// <summary>
/// Versioned mutable state attached to one physical item instance or stack. Definition SOs
/// remain immutable; new systems add a component instead of widening the generic stack DTO.
/// </summary>
[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ItemInstanceComponentSaveData
{
    public string componentTypeId = string.Empty;
    [Min(1)] public int schemaVersion = 1;
    public bool affectsStacking = true;
    public List<ItemStateValueSaveData> values = new List<ItemStateValueSaveData>();

    public string ToCanonicalString()
    {
        string fields = string.Join(",", (values ?? new List<ItemStateValueSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.key, StringComparer.Ordinal)
            .Select(value => value.ToCanonicalString()));
        return $"{componentTypeId?.Trim()}@{Math.Max(1, schemaVersion)}[{fields}]";
    }

    public ItemInstanceComponentSaveData Clone() => new ItemInstanceComponentSaveData
    {
        componentTypeId = componentTypeId?.Trim() ?? string.Empty,
        schemaVersion = Math.Max(1, schemaVersion),
        affectsStacking = affectsStacking,
        values = (values ?? new List<ItemStateValueSaveData>())
            .Where(value => value != null)
            .Select(value => new ItemStateValueSaveData
            {
                key = value.key?.Trim() ?? string.Empty,
                kind = value.kind,
                stringValue = value.stringValue ?? string.Empty,
                integerValue = value.integerValue,
                decimalValue = value.decimalValue,
                booleanValue = value.booleanValue
            })
            .ToList()
    };
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class ItemStackSignature
{
    public static string Create(
        string definitionId,
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        string state = string.Join("|", (components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null && component.affectsStacking)
            .OrderBy(component => component.componentTypeId, StringComparer.Ordinal)
            .Select(component => component.ToCanonicalString()));
        return $"{definitionId?.Trim() ?? string.Empty}::{state}";
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class ItemInstanceComponentIds
{
    public const string Freshness = "item-state:freshness";
    public const string Durability = "item-state:durability";
    public const string Quality = "item-state:quality";
    public const string Contamination = "item-state:contamination";
    public const string Equipment = "item-state:equipment";
    public const string EquipmentModule = "item-state:equipment-module";
    public const string Provenance = "item-state:provenance";
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
    public string itemInstanceId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public WasteOriginKind wasteOrigin;
    [Range(0f, 100f)] public float contamination;
    public List<ItemInstanceComponentSaveData> components =
        new List<ItemInstanceComponentSaveData>();

    public string GetStackSignature() =>
        string.IsNullOrWhiteSpace(itemInstanceId)
            ? ItemStackSignature.Create(itemId, components)
            : $"{ItemStackSignature.Create(itemId, components)}#instance={itemInstanceId.Trim()}";
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCarryInventorySaveData
{
    public List<CharacterCarriedItemSaveData> items =
        new List<CharacterCarriedItemSaveData>();
}
