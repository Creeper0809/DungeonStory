using System;
using System.Collections.Generic;
using System.Linq;

public interface IRetailStockPhysicalRuntime
{
    bool TryCreateAuthoredUniqueLot(
        int saleItemId,
        string itemDefinitionId,
        long unitMassGrams,
        string sourceOperationId,
        out RetailStockLotSnapshot lot,
        out string failureReason);

    bool TryCommitExternalSink(
        RetailStockLotSnapshot lot,
        out string failureReason);

    bool TryPrepareExistingUniqueLot(
        RetailStockLotSnapshot lot,
        out string failureReason);

    bool TryBindExistingUniqueLot(
        RetailStockLotSnapshot lot,
        out string failureReason);

    bool TryRestoreBoundUniqueLot(
        RetailStockLotSnapshot lot,
        CombatEquipmentWorldState restoredWorldState,
        out string failureReason);
}

public interface IRetailEquipmentAuthority
{
    IReadOnlyCollection<EquipmentModuleInstance> ModuleInstances { get; }

    CombatEquipmentInstance CreateExternalInstance(
        string definitionId,
        CombatEquipmentQuality quality);

    bool TryGetInstance(string instanceId, out CombatEquipmentInstance instance);
    bool TryMarkLost(string instanceId);
    bool TryBindRetailStock(string instanceId, string operationId, out string failureReason);
    bool TryBindPhysicalToRetailStock(
        string instanceId,
        string sourceStackId,
        string operationId,
        out string failureReason);
    bool TryConsumeRetailStock(
        string instanceId,
        string operationId,
        out CombatEquipmentInstance consumed,
        out string failureReason);
    bool TryRestoreRetailStockToPhysical(
        string instanceId,
        string operationId,
        string sourceStackId,
        CombatEquipmentWorldState worldState,
        out string failureReason);
}

/// <summary>
/// Retail-only equipment state authority. It intentionally depends on the
/// aggregate repository rather than <see cref="ICombatEquipmentRuntime"/> so
/// item transfer can consume it without closing the combat -> physical item ->
/// item transfer dependency cycle.
/// </summary>
public sealed class RetailEquipmentAuthority : IRetailEquipmentAuthority
{
    private readonly ICombatEquipmentCatalog catalog;
    private readonly IItemInstanceRepository items;
    private readonly CombatEquipmentLoadoutStore loadouts;

    public RetailEquipmentAuthority(
        ICombatEquipmentCatalog catalog,
        IItemInstanceRepository items,
        CombatEquipmentLoadoutStore loadouts)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.loadouts = loadouts ?? throw new ArgumentNullException(nameof(loadouts));
    }

    public IReadOnlyCollection<EquipmentModuleInstance> ModuleInstances =>
        items.EquipmentModules.Values
            .Where(value => value != null)
            .Select(value => value.Clone())
            .ToArray();

    public CombatEquipmentInstance CreateExternalInstance(
        string definitionId,
        CombatEquipmentQuality quality)
    {
        string canonical = definitionId?.Trim() ?? string.Empty;
        if (!catalog.TryGet(canonical, out CombatEquipmentDefinitionSO definition))
        {
            throw new KeyNotFoundException(
                $"Unknown external combat equipment definition '{definitionId}'.");
        }
        CombatEquipmentInstance instance = new()
        {
            instanceId = items.AllocateItemInstanceId().Value,
            definitionId = definition.EquipmentId,
            materialId = definition.DefaultMaterialId,
            quality = quality,
            durabilityRatio = 1f,
            powerCharge = 100f,
            loadedAmmunition = new LoadedAmmunitionBatch(),
            worldState = CombatEquipmentWorldState.Equipped,
            moduleSlots = Enumerable.Range(0, definition.ModuleSlotCount)
                .Select(index => new EquipmentModuleSlotState { slotIndex = index })
                .ToList()
        };
        items.EquipmentInstances.Add(instance.instanceId, instance);
        return instance.Clone();
    }

    public bool TryGetInstance(string instanceId, out CombatEquipmentInstance instance)
    {
        if (items.EquipmentInstances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance stored))
        {
            instance = stored.Clone();
            return true;
        }
        instance = null;
        return false;
    }

    public bool TryMarkLost(string instanceId)
    {
        if (!items.EquipmentInstances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance))
        {
            return false;
        }
        loadouts.RemoveEquipment(instance.instanceId);
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = string.Empty;
        instance.worldState = CombatEquipmentWorldState.Lost;
        return true;
    }

    public bool TryBindRetailStock(
        string instanceId,
        string operationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string id = instanceId?.Trim() ?? string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        if (id.Length == 0 || operation.Length == 0
            || !string.Equals(id, instanceId, StringComparison.Ordinal)
            || !string.Equals(operation, operationId, StringComparison.Ordinal)
            || !items.EquipmentInstances.TryGetValue(id, out CombatEquipmentInstance instance))
        {
            failureReason = "retail-stock-identity-not-canonical";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(instance.ownerCharacterId)
            || instance.worldState is CombatEquipmentWorldState.ExpeditionPacked
                or CombatEquipmentWorldState.MaintenanceBuffer
                or CombatEquipmentWorldState.Carried)
        {
            failureReason = "retail-stock-equipment-owned-by-active-domain";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(instance.sourceStackId)
            && !string.Equals(instance.sourceStackId, operation, StringComparison.Ordinal))
        {
            failureReason = "retail-stock-equipment-source-conflict";
            return false;
        }
        loadouts.RemoveEquipment(instance.instanceId);
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = operation;
        instance.worldState = CombatEquipmentWorldState.RetailStock;
        return true;
    }

    public bool TryBindPhysicalToRetailStock(
        string instanceId,
        string sourceStackId,
        string operationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string id = instanceId?.Trim() ?? string.Empty;
        string stack = sourceStackId?.Trim() ?? string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        if (id.Length == 0 || stack.Length == 0 || operation.Length == 0
            || !string.Equals(id, instanceId, StringComparison.Ordinal)
            || !string.Equals(stack, sourceStackId, StringComparison.Ordinal)
            || !string.Equals(operation, operationId, StringComparison.Ordinal)
            || !items.EquipmentInstances.TryGetValue(id, out CombatEquipmentInstance instance)
            || instance.worldState != CombatEquipmentWorldState.Carried
            || !string.IsNullOrWhiteSpace(instance.ownerCharacterId)
            || !string.Equals(instance.sourceStackId, stack, StringComparison.Ordinal))
        {
            failureReason = "retail-stock-physical-bind-authority-mismatch";
            return false;
        }
        loadouts.RemoveEquipment(instance.instanceId);
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = operation;
        instance.worldState = CombatEquipmentWorldState.RetailStock;
        return true;
    }

    public bool TryConsumeRetailStock(
        string instanceId,
        string operationId,
        out CombatEquipmentInstance consumed,
        out string failureReason)
    {
        consumed = null;
        failureReason = string.Empty;
        string id = instanceId?.Trim() ?? string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        if (!items.EquipmentInstances.TryGetValue(id, out CombatEquipmentInstance instance)
            || instance.worldState != CombatEquipmentWorldState.RetailStock
            || !string.Equals(instance.sourceStackId, operation, StringComparison.Ordinal)
            || !string.IsNullOrWhiteSpace(instance.ownerCharacterId))
        {
            failureReason = "retail-stock-equipment-authority-mismatch";
            return false;
        }
        string[] moduleIds = (instance.moduleSlots ?? new List<EquipmentModuleSlotState>())
            .Where(slot => slot != null && !string.IsNullOrWhiteSpace(slot.moduleInstanceId))
            .Select(slot => slot.moduleInstanceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (moduleIds.Any(moduleId => !items.EquipmentModules.ContainsKey(moduleId)))
        {
            failureReason = "retail-stock-attached-module-missing";
            return false;
        }
        consumed = instance.Clone();
        loadouts.RemoveEquipment(instance.instanceId);
        items.EquipmentInstances.Remove(instance.instanceId);
        foreach (string moduleId in moduleIds)
            items.EquipmentModules.Remove(moduleId);
        return true;
    }

    public bool TryRestoreRetailStockToPhysical(
        string instanceId,
        string operationId,
        string sourceStackId,
        CombatEquipmentWorldState worldState,
        out string failureReason)
    {
        failureReason = string.Empty;
        string id = instanceId?.Trim() ?? string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        string stack = sourceStackId?.Trim() ?? string.Empty;
        if (worldState is not (CombatEquipmentWorldState.Stored
                or CombatEquipmentWorldState.Loose
                or CombatEquipmentWorldState.Carried
                or CombatEquipmentWorldState.MaintenanceBuffer)
            || stack.Length == 0
            || !items.EquipmentInstances.TryGetValue(id, out CombatEquipmentInstance instance)
            || instance.worldState != CombatEquipmentWorldState.RetailStock
            || !string.Equals(instance.sourceStackId, operation, StringComparison.Ordinal))
        {
            failureReason = "retail-stock-physical-restore-authority-mismatch";
            return false;
        }
        instance.sourceStackId = stack;
        instance.ownerCharacterId = string.Empty;
        instance.worldState = worldState;
        return true;
    }
}

public sealed class CombatEquipmentRuntimeRetailAuthorityAdapter :
    IRetailEquipmentAuthority
{
    private readonly ICombatEquipmentRuntime runtime;

    public CombatEquipmentRuntimeRetailAuthorityAdapter(ICombatEquipmentRuntime runtime) =>
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public IReadOnlyCollection<EquipmentModuleInstance> ModuleInstances =>
        runtime.ModuleInstances;
    public CombatEquipmentInstance CreateExternalInstance(
        string definitionId,
        CombatEquipmentQuality quality) =>
        runtime.CreateExternalInstance(definitionId, quality);
    public bool TryGetInstance(string id, out CombatEquipmentInstance instance) =>
        runtime.TryGetInstance(id, out instance);
    public bool TryMarkLost(string id) => runtime.TryMarkLost(id);
    public bool TryBindRetailStock(string id, string operation, out string failure) =>
        runtime.TryBindRetailStock(id, operation, out failure);
    public bool TryBindPhysicalToRetailStock(
        string id, string stack, string operation, out string failure) =>
        runtime.TryBindPhysicalToRetailStock(id, stack, operation, out failure);
    public bool TryConsumeRetailStock(
        string id, string operation, out CombatEquipmentInstance consumed, out string failure) =>
        runtime.TryConsumeRetailStock(id, operation, out consumed, out failure);
    public bool TryRestoreRetailStockToPhysical(
        string id,
        string operation,
        string stack,
        CombatEquipmentWorldState state,
        out string failure) =>
        runtime.TryRestoreRetailStockToPhysical(id, operation, stack, state, out failure);
}

/// <summary>
/// Bridges the shop-owned exact retail lot aggregate to stateful physical-item
/// authorities. Generic lots require no secondary owner. Combat equipment is
/// joined to the equipment instance repository while it is retail stock and is
/// removed from that repository only at the terminal external sink boundary.
/// </summary>
public sealed class RetailStockPhysicalRuntime : IRetailStockPhysicalRuntime
{
    private readonly IRetailEquipmentAuthority equipment;

    public RetailStockPhysicalRuntime(IRetailEquipmentAuthority equipment)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
    }

    public bool TryCreateAuthoredUniqueLot(
        int saleItemId,
        string itemDefinitionId,
        long unitMassGrams,
        string sourceOperationId,
        out RetailStockLotSnapshot lot,
        out string failureReason)
    {
        lot = null;
        failureReason = string.Empty;
        string normalizedItemId = itemDefinitionId?.Trim() ?? string.Empty;
        string normalizedOperationId = sourceOperationId?.Trim() ?? string.Empty;
        if (saleItemId < 0
            || unitMassGrams <= 0L
            || normalizedItemId.Length == 0
            || normalizedOperationId.Length == 0
            || !string.Equals(
                normalizedItemId,
                itemDefinitionId,
                StringComparison.Ordinal)
            || !string.Equals(
                normalizedOperationId,
                sourceOperationId,
                StringComparison.Ordinal)
            || !PhysicalItemIds.TryGetEquipmentDefinitionId(
                normalizedItemId,
                out string equipmentDefinitionId))
        {
            failureReason = "retail-authored-unique-source-not-supported";
            return false;
        }

        CombatEquipmentInstance created;
        try
        {
            created = equipment.CreateExternalInstance(
                equipmentDefinitionId,
                CombatEquipmentQuality.Normal);
        }
        catch (Exception exception)
        {
            failureReason = "retail-authored-equipment-source-failed:"
                + exception.Message;
            return false;
        }
        if (created == null
            || !equipment.TryBindRetailStock(
                created.instanceId,
                normalizedOperationId,
                out failureReason)
            || !equipment.TryGetInstance(
                created.instanceId,
                out CombatEquipmentInstance bound))
        {
            equipment.TryMarkLost(created?.instanceId);
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "retail-authored-equipment-bind-failed"
                : failureReason;
            return false;
        }

        ItemInstanceComponentSaveData component = CaptureEquipmentComponent(bound);
        List<ItemInstanceComponentSaveData> components = new() { component };
        lot = new RetailStockLotSnapshot
        {
            saleItemId = saleItemId,
            itemDefinitionId = normalizedItemId,
            itemInstanceId = bound.instanceId,
            sourceStackId = string.Empty,
            quantity = 1,
            unitMassGrams = unitMassGrams,
            sourceOperationId = normalizedOperationId,
            componentFingerprint = ItemStackSignature.Create(
                normalizedItemId,
                components),
            components = CaptureRetailComponents(components)
        };
        return true;
    }

    public bool TryCommitExternalSink(
        RetailStockLotSnapshot lot,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (lot == null
            || lot.quantity != 1
            || string.IsNullOrWhiteSpace(lot.itemDefinitionId)
            || string.IsNullOrWhiteSpace(lot.sourceOperationId))
        {
            failureReason = "retail-terminal-lot-invalid";
            return false;
        }
        if (string.IsNullOrEmpty(lot.itemInstanceId))
        {
            return true;
        }
        if (!PhysicalItemIds.TryGetEquipmentDefinitionId(
                lot.itemDefinitionId,
                out string equipmentDefinitionId)
            || !equipment.TryGetInstance(
                lot.itemInstanceId,
                out CombatEquipmentInstance instance)
            || !string.Equals(
                instance.definitionId,
                equipmentDefinitionId,
                StringComparison.Ordinal))
        {
            failureReason = "retail-terminal-instance-definition-mismatch";
            return false;
        }

        ItemInstanceComponentSaveData liveComponent =
            CaptureEquipmentComponent(instance);
        string liveFingerprint = ItemStackSignature.Create(
            lot.itemDefinitionId,
            new[] { liveComponent });
        if (!string.Equals(
                liveFingerprint,
                lot.componentFingerprint,
                StringComparison.Ordinal))
        {
            failureReason = "retail-terminal-component-fingerprint-mismatch";
            return false;
        }

        return equipment.TryConsumeRetailStock(
            lot.itemInstanceId,
            lot.sourceOperationId,
            out _,
            out failureReason);
    }

    public bool TryPrepareExistingUniqueLot(
        RetailStockLotSnapshot lot,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (lot == null
            || lot.quantity != 1
            || string.IsNullOrWhiteSpace(lot.itemInstanceId)
            || string.IsNullOrWhiteSpace(lot.sourceStackId)
            || string.IsNullOrWhiteSpace(lot.sourceOperationId)
            || !PhysicalItemIds.TryGetEquipmentDefinitionId(
                lot.itemDefinitionId,
                out string equipmentDefinitionId)
            || !equipment.TryGetInstance(
                lot.itemInstanceId,
                out CombatEquipmentInstance instance)
            || instance.worldState != CombatEquipmentWorldState.Carried
            || !string.Equals(
                instance.definitionId,
                equipmentDefinitionId,
                StringComparison.Ordinal)
            || !string.Equals(
                instance.sourceStackId,
                lot.sourceStackId,
                StringComparison.Ordinal))
        {
            failureReason = "retail-transfer-unique-authority-mismatch";
            return false;
        }
        ItemInstanceComponentSaveData liveComponent =
            CaptureEquipmentComponent(instance);
        string liveFingerprint = ItemStackSignature.Create(
            lot.itemDefinitionId,
            new[] { liveComponent });
        if (!string.Equals(
                liveFingerprint,
                lot.componentFingerprint,
                StringComparison.Ordinal))
        {
            failureReason = "retail-transfer-unique-component-mismatch";
            return false;
        }
        return true;
    }

    public bool TryBindExistingUniqueLot(
        RetailStockLotSnapshot lot,
        out string failureReason)
    {
        if (!TryPrepareExistingUniqueLot(lot, out failureReason)
            || !equipment.TryBindPhysicalToRetailStock(
                lot.itemInstanceId,
                lot.sourceStackId,
                lot.sourceOperationId,
                out failureReason)
            || !equipment.TryGetInstance(
                lot.itemInstanceId,
                out CombatEquipmentInstance bound))
        {
            return false;
        }
        ItemInstanceComponentSaveData component = CaptureEquipmentComponent(bound);
        List<ItemInstanceComponentSaveData> components = new() { component };
        lot.componentFingerprint = ItemStackSignature.Create(
            lot.itemDefinitionId,
            components);
        lot.components = CaptureRetailComponents(components);
        return true;
    }

    public bool TryRestoreBoundUniqueLot(
        RetailStockLotSnapshot lot,
        CombatEquipmentWorldState restoredWorldState,
        out string failureReason)
    {
        if (lot == null
            || string.IsNullOrWhiteSpace(lot.itemInstanceId)
            || string.IsNullOrWhiteSpace(lot.sourceStackId))
        {
            failureReason = "retail-transfer-unique-rollback-invalid";
            return false;
        }
        return equipment.TryRestoreRetailStockToPhysical(
            lot.itemInstanceId,
            lot.sourceOperationId,
            lot.sourceStackId,
            restoredWorldState,
            out failureReason);
    }

    private ItemInstanceComponentSaveData CaptureEquipmentComponent(
        CombatEquipmentInstance instance)
    {
        string[] attachedIds = (instance?.moduleSlots
                ?? new List<EquipmentModuleSlotState>())
            .Where(slot => slot != null
                && !string.IsNullOrWhiteSpace(slot.moduleInstanceId))
            .Select(slot => slot.moduleInstanceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, EquipmentModuleInstance> modules = equipment
            .ModuleInstances
            .Where(module => module != null
                && attachedIds.Contains(
                    module.instanceId,
                    StringComparer.Ordinal))
            .ToDictionary(module => module.instanceId, StringComparer.Ordinal);
        if (modules.Count != attachedIds.Length)
        {
            throw new InvalidOperationException(
                $"Retail equipment '{instance?.instanceId}' has a missing attached module authority.");
        }
        return EquipmentItemStateCodec.Encode(
            instance,
            attachedIds.Select(id => modules[id]));
    }

    private static List<RetailStockComponentSnapshot> CaptureRetailComponents(
        IEnumerable<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null)
            .OrderBy(component => component.componentTypeId, StringComparer.Ordinal)
            .Select(component => new RetailStockComponentSnapshot
            {
                componentTypeId = component.componentTypeId,
                schemaVersion = component.schemaVersion,
                affectsStacking = component.affectsStacking,
                values = (component.values ?? new List<ItemStateValueSaveData>())
                    .Where(value => value != null)
                    .OrderBy(value => value.key, StringComparer.Ordinal)
                    .Select(value => new RetailStockComponentValueSnapshot
                    {
                        key = value.key,
                        kind = (int)value.kind,
                        stringValue = value.stringValue,
                        integerValue = value.integerValue,
                        decimalValue = value.decimalValue,
                        booleanValue = value.booleanValue
                    })
                    .ToList()
            })
            .ToList();
}
