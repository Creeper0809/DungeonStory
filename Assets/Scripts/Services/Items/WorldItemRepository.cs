using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IItemInstanceRepository
{
    IDictionary<string, CombatEquipmentInstance> EquipmentInstances { get; }
    IDictionary<string, EquipmentModuleInstance> EquipmentModules { get; }
    ItemInstanceId AllocateItemInstanceId();
    bool TrySetEquipmentWorldStateBySourceStack(
        string sourceStackId,
        CombatEquipmentWorldState worldState);
    bool TryLinkEquipmentToStack(
        string instanceId,
        string sourceStackId,
        CombatEquipmentWorldState worldState);
    bool TryMarkEquipmentLostBySourceStack(string sourceStackId);
    bool TryMarkModuleLostBySourceStack(string sourceStackId);
}

internal sealed class WorldItemRepositoryState
{
    internal List<WorldItemStackRecord> Records { get; } = new();
    internal Dictionary<string, WorldItemStackRecord> RecordsById { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<Vector2Int, List<WorldItemStackRecord>> RecordsByPosition { get; } =
        new();
    internal List<WorldItemStackRecord> HaulableCache { get; } = new();
    internal HashSet<string> PrioritizedHaulStackIds { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<string, CombatEquipmentInstance> EquipmentInstances { get; } =
        new(StringComparer.Ordinal);
    internal Dictionary<string, EquipmentModuleInstance> EquipmentModules { get; } =
        new(StringComparer.Ordinal);
    internal bool HaulableCacheDirty { get; set; } = true;
    internal int ItemStackVersion { get; set; }
    internal int HaulJobVersion { get; set; }
    internal long NextHaulOperationSequence { get; set; } = 1;
}

public sealed class WorldItemRepository : IItemInstanceRepository
{
    private readonly IPersistentIdGenerator persistentIds;
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly HaulDeliveryIntentRuntime haulDeliveryIntents;

    private WorldItemRepositoryState state =>
        rootStore.GetOrCreate(() => new WorldItemRepositoryState());

    public WorldItemRepository(
        IPersistentIdGenerator persistentIds,
        DungeonRuntimeAggregateRootStore rootStore)
    {
        this.persistentIds = persistentIds
            ?? throw new ArgumentNullException(nameof(persistentIds));
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
        haulDeliveryIntents = new HaulDeliveryIntentRuntime(this);
    }

    internal DungeonRuntimeAggregateRootStore AggregateRootStore => rootStore;
    internal HaulDeliveryIntentRuntime HaulDeliveryIntents => haulDeliveryIntents;

    internal List<WorldItemStackRecord> Records => state.Records;

    internal Dictionary<string, WorldItemStackRecord> RecordsById => state.RecordsById;

    internal Dictionary<Vector2Int, List<WorldItemStackRecord>> RecordsByPosition =>
        state.RecordsByPosition;

    internal List<WorldItemStackRecord> HaulableCache => state.HaulableCache;

    internal HashSet<string> PrioritizedHaulStackIds => state.PrioritizedHaulStackIds;

    public IDictionary<string, CombatEquipmentInstance> EquipmentInstances =>
        state.EquipmentInstances;
    public IDictionary<string, EquipmentModuleInstance> EquipmentModules =>
        state.EquipmentModules;

    public int ItemStackVersion => state.ItemStackVersion;
    public int HaulJobVersion => state.HaulJobVersion;
    internal long NextHaulOperationSequence => state.NextHaulOperationSequence;

    internal event Action<string> StackRemoving;

#if UNITY_EDITOR
    public string AddEditorTestStack(
        string itemId,
        int quantity,
        WorldItemStackState stackState,
        string destinationId = "",
        string sourceStorageDestinationId = "",
        IReadOnlyList<ItemInstanceComponentSaveData> components = null,
        Vector2Int position = default)
    {
        WorldItemStackRecord record = new()
        {
            stackId = AllocateStackId(),
            itemId = itemId?.Trim() ?? string.Empty,
            quantity = quantity,
            state = stackState,
            position = position,
            destinationId = destinationId?.Trim() ?? string.Empty,
            sourceStorageDestinationId =
                sourceStorageDestinationId?.Trim() ?? string.Empty,
            components = (components ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Where(component => component != null)
                .Select(component => component.Clone())
                .ToList()
        };
        Add(record);
        return record.stackId;
    }

    public bool SetEditorTestComponent(
        string stackId,
        ItemInstanceComponentSaveData component)
    {
        if (component == null
            || string.IsNullOrWhiteSpace(component.componentTypeId)
            || !RecordsById.TryGetValue(
                stackId?.Trim() ?? string.Empty,
                out WorldItemStackRecord record)
            || record == null)
        {
            return false;
        }

        record.components ??= new List<ItemInstanceComponentSaveData>();
        record.components.RemoveAll(existing => existing != null
            && string.Equals(
                existing.componentTypeId?.Trim(),
                component.componentTypeId.Trim(),
                StringComparison.Ordinal));
        record.components.Add(component.Clone());
        MarkChanged();
        return true;
    }

    public void RemoveEditorTestStack(string stackId)
    {
        string normalizedStackId = stackId?.Trim() ?? string.Empty;
        if (!RecordsById.TryGetValue(
                normalizedStackId,
                out WorldItemStackRecord record))
        {
            throw new InvalidOperationException(
                $"Unknown test stack '{stackId}'.");
        }

        Remove(record);
    }

    public bool TryRemoveEditorTestStack(string stackId)
    {
        string normalizedStackId = stackId?.Trim() ?? string.Empty;
        if (!RecordsById.TryGetValue(
                normalizedStackId,
                out WorldItemStackRecord record))
        {
            return false;
        }

        Remove(record);
        return true;
    }

    public void SetEditorTestQuantity(string stackId, int quantity)
    {
        string normalizedStackId = stackId?.Trim() ?? string.Empty;
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }
        if (!RecordsById.TryGetValue(
                normalizedStackId,
                out WorldItemStackRecord record))
        {
            throw new InvalidOperationException(
                $"Unknown test stack '{stackId}'.");
        }

        if (quantity == 0)
        {
            Remove(record);
            return;
        }

        record.quantity = quantity;
        MarkChanged();
    }
#endif

    internal bool HaulableCacheDirty
    {
        get => state.HaulableCacheDirty;
        set => state.HaulableCacheDirty = value;
    }

    internal WorldItemRepositoryState CreateDetachedState(
        IEnumerable<WorldItemStackRecord> records,
        IReadOnlyDictionary<string, CombatEquipmentInstance> equipment,
        IReadOnlyDictionary<string, EquipmentModuleInstance> modules,
        long nextHaulOperationSequence)
    {
        WorldItemRepositoryState detached = new()
        {
            ItemStackVersion = state.ItemStackVersion,
            HaulJobVersion = state.HaulJobVersion,
            NextHaulOperationSequence = nextHaulOperationSequence > 0
                ? nextHaulOperationSequence
                : throw new ArgumentOutOfRangeException(
                    nameof(nextHaulOperationSequence))
        };
        foreach (KeyValuePair<string, CombatEquipmentInstance> pair in
                 equipment ?? new Dictionary<string, CombatEquipmentInstance>())
        {
            detached.EquipmentInstances.Add(pair.Key, pair.Value.Clone());
        }
        foreach (KeyValuePair<string, EquipmentModuleInstance> pair in
                 modules ?? new Dictionary<string, EquipmentModuleInstance>())
        {
            detached.EquipmentModules.Add(pair.Key, pair.Value.Clone());
        }
        foreach (WorldItemStackRecord record in records
                     ?? Array.Empty<WorldItemStackRecord>())
        {
            AddToState(detached, record);
        }

        return detached;
    }

    internal void ReplaceState(WorldItemRepositoryState staged)
    {
        rootStore.Replace(
            staged ?? throw new ArgumentNullException(nameof(staged)));
        MarkChanged();
    }

    internal string AllocateStackId()
    {
        string id;
        do
        {
            id = persistentIds.NewItemStackId().Value;
        }
        while (RecordsById.ContainsKey(id));

        return id;
    }

    internal string AllocateItemInstanceId() =>
        persistentIds.NewItemInstanceId().Value;

    internal string AllocateHaulDeliveryOperationId(string ownerCharacterId)
    {
        string owner = ownerCharacterId?.Trim() ?? string.Empty;
        if (owner.Length == 0)
            throw new ArgumentException("Haul operation requires an owner.", nameof(ownerCharacterId));
        string operationId;
        do
        {
            long sequence = state.NextHaulOperationSequence;
            state.NextHaulOperationSequence = checked(sequence + 1L);
            operationId = HaulDeliveryOperationIdentity.Format(owner, sequence);
        }
        while (haulDeliveryIntents.TryCapture(operationId, out _));
        return operationId;
    }

    ItemInstanceId IItemInstanceRepository.AllocateItemInstanceId() =>
        persistentIds.NewItemInstanceId();

    public bool TrySetEquipmentWorldStateBySourceStack(
        string sourceStackId,
        CombatEquipmentWorldState worldState)
    {
        CombatEquipmentInstance instance = FindEquipmentBySourceStack(sourceStackId);
        if (instance == null)
        {
            return false;
        }

        instance.worldState = worldState;
        PersistEquipmentComponent(instance);
        return true;
    }

    public bool TryLinkEquipmentToStack(
        string instanceId,
        string sourceStackId,
        CombatEquipmentWorldState worldState)
    {
        string normalizedStackId = sourceStackId?.Trim() ?? string.Empty;
        if (!EquipmentInstances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || string.IsNullOrWhiteSpace(normalizedStackId)
            || !RecordsById.TryGetValue(normalizedStackId, out WorldItemStackRecord record)
            || !string.Equals(
                record.itemInstanceId,
                instance.instanceId,
                StringComparison.Ordinal))
        {
            return false;
        }

        instance.sourceStackId = normalizedStackId;
        instance.worldState = worldState;
        if (worldState is CombatEquipmentWorldState.Stored
            or CombatEquipmentWorldState.Loose
            or CombatEquipmentWorldState.Carried
            or CombatEquipmentWorldState.MaintenanceBuffer)
        {
            instance.ownerCharacterId = string.Empty;
        }
        PersistEquipmentComponent(instance);
        return true;
    }

    public bool TryMarkEquipmentLostBySourceStack(string sourceStackId)
    {
        CombatEquipmentInstance instance = FindEquipmentBySourceStack(sourceStackId);
        if (instance == null
            || instance.worldState is CombatEquipmentWorldState.Carried
                or CombatEquipmentWorldState.Equipped)
        {
            return false;
        }

        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = string.Empty;
        instance.worldState = CombatEquipmentWorldState.Lost;
        foreach (EquipmentModuleSlotState slot in instance.moduleSlots
                     ?? new List<EquipmentModuleSlotState>())
        {
            if (slot != null
                && EquipmentModules.TryGetValue(
                    slot.moduleInstanceId,
                    out EquipmentModuleInstance module))
            {
                module.attachedEquipmentInstanceId = string.Empty;
                module.state = EquipmentModuleProcessState.Lost;
                module.condition = 0f;
            }
        }
        return true;
    }

    public bool TryMarkModuleLostBySourceStack(string sourceStackId)
    {
        EquipmentModuleInstance module = EquipmentModules.Values.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.sourceStackId,
                sourceStackId?.Trim() ?? string.Empty,
                StringComparison.Ordinal));
        if (module == null || !string.IsNullOrWhiteSpace(module.attachedEquipmentInstanceId))
        {
            return false;
        }

        module.sourceStackId = string.Empty;
        module.attachedEquipmentInstanceId = string.Empty;
        module.state = EquipmentModuleProcessState.Lost;
        module.condition = 0f;
        MarkChanged();
        return true;
    }

    private CombatEquipmentInstance FindEquipmentBySourceStack(string sourceStackId) =>
        EquipmentInstances.Values.FirstOrDefault(candidate => candidate != null
            && string.Equals(
                candidate.sourceStackId,
                sourceStackId?.Trim() ?? string.Empty,
                StringComparison.Ordinal));

    private void PersistEquipmentComponent(CombatEquipmentInstance instance)
    {
        if (instance == null
            || string.IsNullOrWhiteSpace(instance.sourceStackId)
            || !RecordsById.TryGetValue(
                instance.sourceStackId,
                out WorldItemStackRecord record))
        {
            return;
        }

        record.components.RemoveAll(component => component != null
            && string.Equals(
                component.componentTypeId,
                ItemInstanceComponentIds.Equipment,
                StringComparison.Ordinal));
        record.components.Add(EquipmentItemStateCodec.Encode(
            instance,
            (instance.moduleSlots ?? new List<EquipmentModuleSlotState>())
                .Where(slot => slot != null
                    && !string.IsNullOrWhiteSpace(slot.moduleInstanceId)
                    && EquipmentModules.ContainsKey(slot.moduleInstanceId))
                .Select(slot => EquipmentModules[slot.moduleInstanceId])));
        MarkChanged();
    }

    internal void Add(WorldItemStackRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.stackId))
        {
            return;
        }

        AddToState(state, record);
        MarkChanged();
    }

    private static void AddToState(
        WorldItemRepositoryState target,
        WorldItemStackRecord record)
    {
        if (target.RecordsById.ContainsKey(record.stackId))
        {
            throw new InvalidOperationException(
                $"Duplicate item-stack persistent ID '{record.stackId}'.");
        }

        target.Records.Add(record);
        target.RecordsById[record.stackId] = record;
        if (!target.RecordsByPosition.TryGetValue(
                record.position,
                out List<WorldItemStackRecord> positionRecords))
        {
            positionRecords = new List<WorldItemStackRecord>();
            target.RecordsByPosition[record.position] = positionRecords;
        }

        positionRecords.Add(record);
    }

    internal void Remove(WorldItemStackRecord record)
    {
        if (record == null)
        {
            return;
        }

        StackRemoving?.Invoke(record.stackId);

        PrioritizedHaulStackIds.Remove(record.stackId);
        Records.Remove(record);
        RecordsById.Remove(record.stackId);
        if (RecordsByPosition.TryGetValue(
                record.position,
                out List<WorldItemStackRecord> positionRecords))
        {
            positionRecords.Remove(record);
            if (positionRecords.Count == 0)
            {
                RecordsByPosition.Remove(record.position);
            }
        }

        MarkChanged();
    }

    internal void Relocate(
        WorldItemStackRecord record,
        Vector2Int destination)
    {
        if (record == null || record.position == destination)
        {
            return;
        }

        if (RecordsByPosition.TryGetValue(
                record.position,
                out List<WorldItemStackRecord> previousRecords))
        {
            previousRecords.Remove(record);
            if (previousRecords.Count == 0)
            {
                RecordsByPosition.Remove(record.position);
            }
        }

        record.position = destination;
        if (!RecordsByPosition.TryGetValue(
                destination,
                out List<WorldItemStackRecord> destinationRecords))
        {
            destinationRecords = new List<WorldItemStackRecord>();
            RecordsByPosition[destination] = destinationRecords;
        }

        destinationRecords.Add(record);
    }

    internal void Clear()
    {
        rootStore.Replace(new WorldItemRepositoryState
        {
            ItemStackVersion = state.ItemStackVersion,
            HaulJobVersion = state.HaulJobVersion
        });
        MarkChanged();
    }

    internal void MarkChanged()
    {
        unchecked
        {
            state.ItemStackVersion++;
            state.HaulJobVersion++;
        }

        HaulableCacheDirty = true;
    }
}
