using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class WorldItemRestoreState
{
    public ItemHaulingSettingsSnapshot HaulingSettings { get; set; }
    public WorldItemRepositoryState RepositoryState { get; set; }
}

/// <summary>
/// Serializes physical item state and builds a fully validated restore stage.
/// It never mutates the live repository while parsing or validating input.
/// </summary>
public sealed class WorldItemPersistenceService
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IItemHaulingSettingsProvider haulingSettings;
    private readonly WorldItemRepository repository;

    public WorldItemPersistenceService(
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider haulingSettings,
        WorldItemRepository repository)
    {
        this.catalogProvider = catalogProvider
            ?? throw new ArgumentNullException(nameof(catalogProvider));
        this.haulingSettings = haulingSettings
            ?? throw new ArgumentNullException(nameof(haulingSettings));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public DungeonPhysicalItemSaveData Capture()
    {
        DungeonPhysicalItemSaveData snapshot = new DungeonPhysicalItemSaveData
        {
            version = DungeonPhysicalItemSaveData.CurrentVersion,
            haulingSettings = haulingSettings.Capture(),
            stacks = repository.Records
                .Where(stack => stack != null && stack.quantity > 0)
                .OrderBy(stack => stack.position.y)
                .ThenBy(stack => stack.position.x)
                .ThenBy(stack => stack.itemId, StringComparer.Ordinal)
                .ThenBy(stack => stack.stackId, StringComparer.Ordinal)
                .Select(ToSaveData)
                .ToList(),
            uniqueItems = repository.EquipmentInstances.Values
                .Where(instance => instance != null)
                .Select(instance => new UniqueItemInstanceSaveData
                {
                    itemInstanceId = instance.instanceId,
                    definitionId = PhysicalItemIds.ForEquipment(instance.definitionId),
                    components = new List<ItemInstanceComponentSaveData>
                    {
                        EquipmentItemStateCodec.Encode(
                            instance,
                            (instance.moduleSlots
                                ?? new List<EquipmentModuleSlotState>())
                            .Where(slot => slot != null
                                && !string.IsNullOrWhiteSpace(slot.moduleInstanceId)
                                && repository.EquipmentModules.ContainsKey(
                                    slot.moduleInstanceId))
                            .Select(slot => repository.EquipmentModules[
                                slot.moduleInstanceId]))
                    }
                })
                .Concat(repository.EquipmentModules.Values
                    .Where(module => module != null
                        && !string.IsNullOrWhiteSpace(module.sourceStackId)
                        && string.IsNullOrWhiteSpace(
                            module.attachedEquipmentInstanceId))
                    .Select(module => new UniqueItemInstanceSaveData
                    {
                        itemInstanceId = module.instanceId,
                        definitionId = PhysicalItemIds.ForEquipmentModule(),
                        components = new List<ItemInstanceComponentSaveData>
                        {
                            EquipmentModuleItemStateCodec.Encode(module)
                        }
                    }))
                .OrderBy(item => item.itemInstanceId, StringComparer.Ordinal)
                .ToList()
        };

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        PhysicalItemSaveValidation.Validate(snapshot, report, catalogProvider);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                $"Physical item capture produced a non-canonical V{DungeonPhysicalItemSaveData.CurrentVersion} payload: "
                + string.Join(" | ", report.Errors));
        }
        return snapshot;
    }

    internal WorldItemRestoreState StageRestore(DungeonPhysicalItemSaveData snapshot)
    {
        DungeonGameRestoreReport validation = new DungeonGameRestoreReport();
        PhysicalItemSaveValidation.Validate(
            snapshot,
            validation,
            catalogProvider);
        if (!validation.Success)
        {
            throw new InvalidOperationException(
                "Physical item restore validation failed: "
                + string.Join(" | ", validation.Errors));
        }

        Dictionary<string, CombatEquipmentInstance> equipment =
            new(StringComparer.Ordinal);
        Dictionary<string, EquipmentModuleInstance> modules =
            new(StringComparer.Ordinal);
        DecodeUniqueItems(snapshot.uniqueItems, equipment, modules);

        List<WorldItemStackRecord> records = new();
        HashSet<string> stackIds = new(StringComparer.Ordinal);
        HashSet<string> itemInstanceIds = new(StringComparer.Ordinal);
        foreach (WorldItemStackSaveData entry in snapshot.stacks)
        {
            ItemStackId stackId = (ItemStackId)entry.stackId;
            if (!stackId.IsValid || !stackIds.Add(stackId.Value))
            {
                throw new InvalidOperationException(
                    $"Physical item stack has invalid or duplicate persistent ID '{entry.stackId}'.");
            }

            DungeonItemDefinition definition =
                catalogProvider.GetDefinition(entry.itemId);
            ItemInstanceId itemInstanceId = (ItemInstanceId)entry.itemInstanceId;
            if (definition.MaxStack == 1 && !itemInstanceId.IsValid)
            {
                throw new InvalidOperationException(
                    $"Unique item stack '{stackId.Value}' has no valid item-instance ID.");
            }
            if (itemInstanceId.IsValid
                && !itemInstanceIds.Add(itemInstanceId.Value))
            {
                throw new InvalidOperationException(
                    $"Duplicate physical item-instance ID '{itemInstanceId.Value}'.");
            }
            if (PhysicalItemIds.TryGetEquipmentDefinitionId(
                    entry.itemId,
                    out string equipmentDefinitionId)
                && (!equipment.TryGetValue(
                        itemInstanceId.Value,
                        out CombatEquipmentInstance equipmentInstance)
                    || !string.Equals(
                        equipmentInstance.definitionId,
                        equipmentDefinitionId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        equipmentInstance.sourceStackId,
                        stackId.Value,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Equipment stack '{stackId.Value}' does not reference its authoritative item instance.");
            }
            if (PhysicalItemIds.IsEquipmentModule(entry.itemId)
                && (!modules.TryGetValue(
                        itemInstanceId.Value,
                        out EquipmentModuleInstance moduleInstance)
                    || !string.Equals(
                        moduleInstance.sourceStackId,
                        stackId.Value,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Equipment-module stack '{stackId.Value}' does not reference its authoritative item instance.");
            }

            WorldItemStackRecord record = new WorldItemStackRecord
            {
                stackId = stackId.Value,
                itemInstanceId = itemInstanceId.IsValid
                    ? itemInstanceId.Value
                    : string.Empty,
                itemId = entry.itemId,
                quantity = entry.quantity,
                state = entry.state,
                position = new Vector2Int(entry.gridX, entry.gridY),
                reservedByPersistentId = string.Empty,
                destinationId = entry.destinationId,
                sourceStorageDestinationId = entry.sourceStorageDestinationId,
                hasDestinationPosition = entry.hasDestinationPosition,
                destinationPosition = new Vector2Int(
                    entry.destinationGridX,
                    entry.destinationGridY),
                forbidden = entry.forbidden,
                sourceCharacterId = entry.sourceCharacterId,
                sourceDisplayName = entry.sourceDisplayName,
                sourceSpeciesTag = entry.sourceSpeciesTag,
                sourceDeathReason = entry.sourceDeathReason,
                emergencyButcheryAllowed = entry.emergencyButcheryAllowed,
                wasteOrigin = entry.wasteOrigin,
                contamination = entry.contamination,
                components = CloneComponents(entry.components)
            };
            if (record.state == WorldItemStackState.Stored)
            {
                ValidateWarehouseStorageKey(record.destinationId);
                ValidateWarehouseStorageKey(record.sourceStorageDestinationId);
            }
            records.Add(record);
        }

        HashSet<string> stackedUniqueIds = records
            .Where(record => !string.IsNullOrWhiteSpace(record.itemInstanceId))
            .Select(record => record.itemInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (CombatEquipmentInstance instance in equipment.Values)
        {
            bool expectsStack = instance.worldState is CombatEquipmentWorldState.Stored
                or CombatEquipmentWorldState.Loose
                or CombatEquipmentWorldState.Carried
                or CombatEquipmentWorldState.MaintenanceBuffer;
            if (expectsStack
                && (!stackedUniqueIds.Contains(instance.instanceId)
                    || string.IsNullOrWhiteSpace(instance.sourceStackId)))
            {
                throw new InvalidOperationException(
                    $"Physical equipment '{instance.instanceId}' is missing its stack reference.");
            }
        }
        foreach (EquipmentModuleInstance module in modules.Values)
        {
            if (module == null
                || !string.IsNullOrWhiteSpace(
                    module.attachedEquipmentInstanceId))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(module.sourceStackId)
                || !stackedUniqueIds.Contains(module.instanceId)
                || !records.Any(record => record != null
                    && string.Equals(
                        record.stackId,
                        module.sourceStackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        record.itemInstanceId,
                        module.instanceId,
                        StringComparison.Ordinal)
                    && PhysicalItemIds.IsEquipmentModule(record.itemId)))
            {
                throw new InvalidOperationException(
                    $"Physical equipment module '{module.instanceId}' is missing its authoritative stack reference.");
            }
        }

        return new WorldItemRestoreState
        {
            HaulingSettings = new ItemHaulingSettingsSnapshot
            {
                maxCarryMultiplier = snapshot.haulingSettings.maxCarryMultiplier
            },
            RepositoryState = repository.CreateDetachedState(
                records,
                equipment,
                modules)
        };
    }

    internal void Commit(WorldItemRestoreState staged)
    {
        WorldItemRestoreState required = staged
            ?? throw new ArgumentNullException(nameof(staged));
        haulingSettings.Restore(required.HaulingSettings);
        repository.ReplaceState(required.RepositoryState);
    }

    private static void DecodeUniqueItems(
        IEnumerable<UniqueItemInstanceSaveData> savedItems,
        IDictionary<string, CombatEquipmentInstance> equipment,
        IDictionary<string, EquipmentModuleInstance> modules)
    {
        foreach (UniqueItemInstanceSaveData unique in savedItems
                     ?? Array.Empty<UniqueItemInstanceSaveData>())
        {
            if (unique != null
                && PhysicalItemIds.IsEquipmentModule(unique.definitionId))
            {
                string moduleDecodeError = "missing equipment-module state";
                ItemInstanceComponentSaveData moduleComponent =
                    unique.components?.FirstOrDefault(candidate =>
                        candidate != null
                        && candidate.componentTypeId
                            == ItemInstanceComponentIds.EquipmentModule);
                if (!((ItemInstanceId)unique.itemInstanceId).IsValid
                    || moduleComponent == null
                    || !EquipmentModuleItemStateCodec.TryDecode(
                        moduleComponent,
                        out EquipmentModuleInstance module,
                        out moduleDecodeError)
                    || !string.Equals(
                        module.instanceId,
                        unique.itemInstanceId,
                        StringComparison.Ordinal)
                    || modules.ContainsKey(unique.itemInstanceId))
                {
                    throw new InvalidOperationException(
                        $"Invalid physical equipment module '{unique?.itemInstanceId}': {moduleDecodeError}");
                }

                modules.Add(unique.itemInstanceId, module.Clone());
                continue;
            }

            string decodeError = "missing equipment state";
            EquipmentPhysicalStatePayload payload = null;
            ItemInstanceComponentSaveData component = unique?.components?
                .FirstOrDefault(candidate => candidate != null
                    && candidate.componentTypeId
                        == ItemInstanceComponentIds.Equipment);
            if (unique == null
                || !((ItemInstanceId)unique.itemInstanceId).IsValid
                || component == null
                || !EquipmentItemStateCodec.TryDecodeFull(
                    component,
                    out payload,
                    out decodeError)
                || !string.Equals(
                    payload.equipment.instanceId,
                    unique.itemInstanceId,
                    StringComparison.Ordinal)
                || equipment.ContainsKey(unique.itemInstanceId))
            {
                throw new InvalidOperationException(
                    $"Invalid physical unique item '{unique?.itemInstanceId}': {decodeError}");
            }
            equipment.Add(unique.itemInstanceId, payload.equipment.Clone());
            foreach (EquipmentModuleInstance module in payload.attachedModules)
            {
                if (module == null
                    || string.IsNullOrWhiteSpace(module.instanceId)
                    || modules.ContainsKey(module.instanceId))
                {
                    throw new InvalidOperationException(
                        $"Invalid or duplicate physical equipment module '{module?.instanceId}'.");
                }
                modules.Add(module.instanceId, module.Clone());
            }
        }
    }

    private static WorldItemStackSaveData ToSaveData(WorldItemStackRecord stack)
    {
        bool directPickup = !string.IsNullOrWhiteSpace(
                stack.reservedByPersistentId)
            && IsCombatLoadoutDestination(stack.destinationId);
        string sourceStorage = stack.sourceStorageDestinationId?.Trim()
            ?? string.Empty;
        WorldItemStackState durableState = directPickup
            ? sourceStorage.Length > 0
                ? WorldItemStackState.Stored
                : WorldItemStackState.Loose
            : stack.state;
        string durableDestination = directPickup
            ? sourceStorage
            : stack.destinationId?.Trim() ?? string.Empty;
        bool hasDestinationPosition = !directPickup
            && stack.hasDestinationPosition;
        Vector2Int destinationPosition = hasDestinationPosition
            ? stack.destinationPosition
            : default;
        return new WorldItemStackSaveData
        {
            stackId = stack.stackId,
            itemInstanceId = stack.itemInstanceId,
            itemId = stack.itemId,
            quantity = stack.quantity,
            state = durableState,
            gridX = stack.position.x,
            gridY = stack.position.y,
            reservedByPersistentId = string.Empty,
            destinationId = durableDestination,
            sourceStorageDestinationId = directPickup
                ? string.Empty
                : stack.sourceStorageDestinationId?.Trim() ?? string.Empty,
            hasDestinationPosition = hasDestinationPosition,
            destinationGridX = destinationPosition.x,
            destinationGridY = destinationPosition.y,
            forbidden = stack.forbidden,
            sourceCharacterId = stack.sourceCharacterId?.Trim() ?? string.Empty,
            sourceDisplayName = stack.sourceDisplayName?.Trim() ?? string.Empty,
            sourceSpeciesTag = stack.sourceSpeciesTag?.Trim() ?? string.Empty,
            sourceDeathReason = stack.sourceDeathReason?.Trim() ?? string.Empty,
            emergencyButcheryAllowed = stack.emergencyButcheryAllowed,
            wasteOrigin = stack.wasteOrigin,
            contamination = stack.contamination,
            components = CloneComponents(stack.components)
        };
    }

    private static List<ItemInstanceComponentSaveData> CloneComponents(
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        return components
            .Select(component => new ItemInstanceComponentSaveData
            {
                componentTypeId = component.componentTypeId,
                schemaVersion = component.schemaVersion,
                affectsStacking = component.affectsStacking,
                values = component.values
                    .Select(value => new ItemStateValueSaveData
                    {
                        key = value.key,
                        kind = value.kind,
                        stringValue = value.stringValue,
                        integerValue = value.integerValue,
                        decimalValue = value.decimalValue,
                        booleanValue = value.booleanValue
                    })
                    .ToList()
            })
            .ToList();
    }

    private static bool IsCombatLoadoutDestination(string destinationId)
    {
        return !string.IsNullOrWhiteSpace(destinationId)
            && destinationId.StartsWith(
                WorldItemStackRuntime.CombatLoadoutDestinationPrefix,
                StringComparison.Ordinal);
    }

    private static void ValidateWarehouseStorageKey(string destinationId)
    {
        string normalized = destinationId?.Trim() ?? string.Empty;
        if (!normalized.StartsWith(
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                StringComparison.Ordinal))
        {
            return;
        }
        string suffix = normalized.Substring(
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix.Length);
        if (!suffix.StartsWith("building:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Legacy warehouse storage key '{normalized}' cannot be restored in V18.");
        }
    }

}
