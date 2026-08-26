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
    internal List<string> PendingWarehouseEvacuationIds { get; } = new();
    internal int WarehouseEvacuationRevision { get; set; }
    internal Dictionary<string, PhysicalItemBatchDispositionSaveData>
        PendingBatchDispositions { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, ProductionPhysicalCustodyDrainSaveData>
        PendingProductionCustodyDrains { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, ProductionInputDestinationCustodyDrainSaveData>
        PendingProductionInputDestinationDrains { get; } =
            new(StringComparer.Ordinal);
    internal Dictionary<string, ProductionCapacityRoutingDrainSaveData>
        PendingCapacityRoutingDrains { get; } = new(StringComparer.Ordinal);
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
    internal int WarehouseEvacuationRevision => state.WarehouseEvacuationRevision;

    internal IReadOnlyList<PhysicalItemBatchDispositionSaveData>
        CapturePendingBatchDispositions() => state.PendingBatchDispositions.Values
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .Select(CloneBatchDisposition)
            .ToArray();

    internal IReadOnlyList<ProductionPhysicalCustodyDrainSaveData>
        CapturePendingProductionCustodyDrains() =>
            state.PendingProductionCustodyDrains.Values
                .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToArray();

    internal IReadOnlyList<ProductionCapacityRoutingDrainSaveData>
        CapturePendingCapacityRoutingDrains() =>
            state.PendingCapacityRoutingDrains.Values
                .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToArray();

    internal IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
        CapturePendingProductionInputDestinationDrains() =>
            state.PendingProductionInputDestinationDrains.Values
                .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
                .Select(value => value.Clone())
                .ToArray();

    internal bool TryGetPendingCapacityRoutingDrain(
        string stepOperationId,
        out ProductionCapacityRoutingDrainSaveData pending) =>
        state.PendingCapacityRoutingDrains.TryGetValue(
            stepOperationId ?? string.Empty,
            out pending);

    internal bool TryGetPendingCapacityRoutingDrainForBatch(
        string batchCommitId,
        out ProductionCapacityRoutingDrainSaveData pending)
    {
        pending = state.PendingCapacityRoutingDrains.Values
            .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
            .FirstOrDefault(value => string.Equals(
                value.batchCommitId,
                batchCommitId ?? string.Empty,
                StringComparison.Ordinal));
        return pending != null;
    }

    internal bool TryGetActiveCapacityRoutingAuthorityRelease(
        string operationId,
        out ProductionCapacityRoutingActorAuthorityReleaseSaveData release)
    {
        string operation = operationId ?? string.Empty;
        release = state.PendingCapacityRoutingDrains.Values
            .Where(drain => drain != null
                && drain.phase == ProductionCapacityRoutingDrainPhase
                    .ReleasingOperationAuthority)
            .SelectMany(drain => drain.actorAuthorityReleases
                ?? new List<ProductionCapacityRoutingActorAuthorityReleaseSaveData>())
            .Where(candidate => candidate != null
                && !candidate.effectsCommitted
                && candidate.operationIds != null
                && candidate.operationIds.Contains(
                    operation,
                    StringComparer.Ordinal))
            .OrderBy(candidate => candidate.actorPersistentId,
                StringComparer.Ordinal)
            .FirstOrDefault();
        if (release == null)
            return false;
        release = release.Clone();
        return true;
    }

    internal bool TryGetActiveCapacityRoutingAuthorityReleaseForAdmission(
        string tokenId,
        out ProductionCapacityRoutingActorAuthorityReleaseSaveData release)
    {
        string token = tokenId ?? string.Empty;
        release = state.PendingCapacityRoutingDrains.Values
            .Where(drain => drain != null
                && drain.phase == ProductionCapacityRoutingDrainPhase
                    .ReleasingOperationAuthority)
            .SelectMany(drain => drain.actorAuthorityReleases
                ?? new List<ProductionCapacityRoutingActorAuthorityReleaseSaveData>())
            .Where(candidate => candidate != null
                && !candidate.effectsCommitted
                && candidate.operations != null
                && candidate.operations.Any(row => row != null
                    && row.warehouseAdmissionTokenIds != null
                    && row.warehouseAdmissionTokenIds.Contains(
                        token,
                        StringComparer.Ordinal)))
            .OrderBy(candidate => candidate.actorPersistentId,
                StringComparer.Ordinal)
            .FirstOrDefault();
        if (release == null)
            return false;
        release = release.Clone();
        return true;
    }

    internal void SetPendingCapacityRoutingDrain(
        ProductionCapacityRoutingDrainSaveData pending)
    {
        if (pending == null || string.IsNullOrEmpty(pending.stepOperationId))
        {
            throw new ArgumentException(
                "A capacity-routing drain requires a step operation ID.",
                nameof(pending));
        }
        state.PendingCapacityRoutingDrains[pending.stepOperationId] =
            pending.Clone();
        MarkChanged();
    }

    internal bool RemovePendingCapacityRoutingDrain(string stepOperationId)
    {
        if (!state.PendingCapacityRoutingDrains.Remove(
                stepOperationId ?? string.Empty))
        {
            return false;
        }
        MarkChanged();
        return true;
    }

    internal bool TryGetPendingProductionCustodyDrain(
        string stepOperationId,
        out ProductionPhysicalCustodyDrainSaveData pending) =>
        state.PendingProductionCustodyDrains.TryGetValue(
            stepOperationId ?? string.Empty,
            out pending);

    internal bool TryGetActiveProductionCustodyDrainForDestination(
        string destinationId,
        out ProductionPhysicalCustodyDrainSaveData pending)
    {
        pending = state.PendingProductionCustodyDrains.Values
            .Where(IsMutationFenceActive)
            .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
            .FirstOrDefault(value => string.Equals(
                value.sourceDestinationId,
                destinationId,
                StringComparison.Ordinal));
        return pending != null;
    }

    internal bool TryGetActiveProductionCustodyDrainForStack(
        string stackId,
        out ProductionPhysicalCustodyDrainSaveData pending)
    {
        pending = state.PendingProductionCustodyDrains.Values
            .Where(IsMutationFenceActive)
            .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
            .FirstOrDefault(value => value.sourceStackIds?.Contains(
                stackId ?? string.Empty,
                StringComparer.Ordinal) == true);
        return pending != null;
    }

    private static bool IsMutationFenceActive(
        ProductionPhysicalCustodyDrainSaveData value) => value != null
        && value.phase is ProductionPhysicalCustodyDrainPhase.Prepared
            or ProductionPhysicalCustodyDrainPhase.ReleasingActors
            or ProductionPhysicalCustodyDrainPhase.ReleasingIntents
            or ProductionPhysicalCustodyDrainPhase.ReleasingDestination;

    internal void SetPendingProductionCustodyDrain(
        ProductionPhysicalCustodyDrainSaveData pending)
    {
        if (pending == null || string.IsNullOrEmpty(pending.stepOperationId))
            throw new ArgumentException(
                "A physical custody drain requires a step operation ID.",
                nameof(pending));
        state.PendingProductionCustodyDrains[pending.stepOperationId] =
            pending.Clone();
        MarkChanged();
    }

    internal bool RemovePendingProductionCustodyDrain(
        string stepOperationId)
    {
        if (!state.PendingProductionCustodyDrains.Remove(
                stepOperationId ?? string.Empty))
        {
            return false;
        }
        MarkChanged();
        return true;
    }

    internal bool TryGetPendingProductionInputDestinationDrain(
        string stepOperationId,
        out ProductionInputDestinationCustodyDrainSaveData pending) =>
        state.PendingProductionInputDestinationDrains.TryGetValue(
            stepOperationId ?? string.Empty,
            out pending);

    internal bool IsProductionInputDestinationDrainOpen(string destinationId)
    {
        string destination = destinationId ?? string.Empty;
        return destination.Length > 0
            && state.PendingProductionInputDestinationDrains.Values.Any(value =>
                value != null
                && string.Equals(
                    value.sourceDestinationId,
                    destination,
                    StringComparison.Ordinal));
    }

    internal void SetPendingProductionInputDestinationDrain(
        ProductionInputDestinationCustodyDrainSaveData pending)
    {
        if (pending == null || string.IsNullOrEmpty(pending.stepOperationId))
        {
            throw new ArgumentException(
                "A production input-destination drain requires a step operation ID.",
                nameof(pending));
        }
        state.PendingProductionInputDestinationDrains[pending.stepOperationId] =
            pending.Clone();
        MarkChanged();
    }

    internal bool RemovePendingProductionInputDestinationDrain(
        string stepOperationId)
    {
        if (!state.PendingProductionInputDestinationDrains.Remove(
                stepOperationId ?? string.Empty))
            return false;
        MarkChanged();
        return true;
    }

    internal bool TryGetPendingBatchDisposition(
        string operationId,
        out PhysicalItemBatchDispositionSaveData pending) =>
        state.PendingBatchDispositions.TryGetValue(
            operationId ?? string.Empty,
            out pending);

    internal void AddPendingBatchDisposition(
        PhysicalItemBatchDispositionSaveData pending)
    {
        if (pending == null
            || string.IsNullOrEmpty(pending.operationId)
            || !state.PendingBatchDispositions.TryAdd(
                pending.operationId,
                CloneBatchDisposition(pending)))
        {
            throw new InvalidOperationException(
                $"Duplicate or invalid pending physical disposition '{pending?.operationId}'.");
        }
        MarkChanged();
    }

    internal bool AcknowledgePendingBatchDisposition(string commitId)
    {
        string canonical = commitId ?? string.Empty;
        string operationId = state.PendingBatchDispositions
            .Where(pair => string.Equals(
                pair.Value.commitId,
                canonical,
                StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .SingleOrDefault();
        if (string.IsNullOrEmpty(operationId)
            || !state.PendingBatchDispositions.Remove(operationId))
        {
            return false;
        }
        MarkChanged();
        return true;
    }

    internal IReadOnlyList<string> CapturePendingWarehouseEvacuationIds() =>
        state.PendingWarehouseEvacuationIds.ToArray();

    internal bool ClearPendingWarehouseEvacuation(string destinationId)
    {
        string canonical = destinationId?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || !state.PendingWarehouseEvacuationIds.Remove(canonical))
        {
            return false;
        }
        state.WarehouseEvacuationRevision = checked(
            state.WarehouseEvacuationRevision + 1);
        return true;
    }

    internal event Action<string> StackRemoving;

#if UNITY_EDITOR
    public int GetEditorPendingBatchDispositionCount() =>
        state.PendingBatchDispositions.Count;

    public int GetEditorTestQuantity(string stackId) =>
        RecordsById.TryGetValue(stackId ?? string.Empty, out WorldItemStackRecord record)
            ? record.quantity
            : 0;

    [GameplayInternalOnly(
        "Allocates a canonical haul operation ID for isolated Editor fixtures.",
        "Physical item focused Editor fixtures only")]
    public string AllocateEditorTestHaulDeliveryOperationId(
        string ownerCharacterId) =>
        AllocateHaulDeliveryOperationId(ownerCharacterId);

    public string AddEditorTestStack(
        string itemId,
        int quantity,
        WorldItemStackState stackState,
        string destinationId = "",
        string sourceStorageDestinationId = "",
        IReadOnlyList<ItemInstanceComponentSaveData> components = null,
        Vector2Int position = default,
        string itemInstanceId = "")
    {
        WorldItemStackRecord record = new()
        {
            stackId = AllocateStackId(),
            itemInstanceId = itemInstanceId?.Trim() ?? string.Empty,
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
        long nextHaulOperationSequence,
        IReadOnlyList<string> pendingWarehouseEvacuationIds = null,
        IReadOnlyList<PhysicalItemBatchDispositionSaveData>
            pendingBatchDispositions = null,
        IReadOnlyList<ProductionPhysicalCustodyDrainSaveData>
            pendingProductionCustodyDrains = null,
        IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
            pendingProductionInputDestinationDrains = null,
        IReadOnlyList<ProductionCapacityRoutingDrainSaveData>
            pendingCapacityRoutingDrains = null)
    {
        WorldItemRepositoryState detached = new()
        {
            ItemStackVersion = state.ItemStackVersion,
            HaulJobVersion = state.HaulJobVersion,
            NextHaulOperationSequence = nextHaulOperationSequence > 0
                ? nextHaulOperationSequence
                : throw new ArgumentOutOfRangeException(
                    nameof(nextHaulOperationSequence)),
            WarehouseEvacuationRevision = checked(
                state.WarehouseEvacuationRevision + 1)
        };
        foreach (string destinationId in (pendingWarehouseEvacuationIds
                     ?? Array.Empty<string>())
                 .Where(value => !string.IsNullOrWhiteSpace(value))
                 .Select(value => value.Trim())
                 .Distinct(StringComparer.Ordinal)
                 .OrderBy(value => value, StringComparer.Ordinal))
        {
            detached.PendingWarehouseEvacuationIds.Add(destinationId);
        }
        foreach (PhysicalItemBatchDispositionSaveData pending in
                 pendingBatchDispositions
                 ?? Array.Empty<PhysicalItemBatchDispositionSaveData>())
        {
            if (pending == null
                || !detached.PendingBatchDispositions.TryAdd(
                    pending.operationId,
                    CloneBatchDisposition(pending)))
            {
                throw new InvalidOperationException(
                    $"Duplicate pending physical disposition '{pending?.operationId}'.");
            }
        }
        foreach (ProductionPhysicalCustodyDrainSaveData pending in
                 pendingProductionCustodyDrains
                 ?? Array.Empty<ProductionPhysicalCustodyDrainSaveData>())
        {
            if (pending == null
                || !detached.PendingProductionCustodyDrains.TryAdd(
                    pending.stepOperationId,
                    pending.Clone()))
            {
                throw new InvalidOperationException(
                    "Duplicate pending production physical custody drain '"
                    + pending?.stepOperationId + "'.");
            }
        }
        foreach (ProductionCapacityRoutingDrainSaveData pending in
                 pendingCapacityRoutingDrains
                 ?? Array.Empty<ProductionCapacityRoutingDrainSaveData>())
        {
            if (pending == null
                || !detached.PendingCapacityRoutingDrains.TryAdd(
                    pending.stepOperationId,
                    pending.Clone()))
            {
                throw new InvalidOperationException(
                    "Duplicate pending production capacity-routing drain '"
                    + pending?.stepOperationId + "'.");
            }
        }
        foreach (ProductionInputDestinationCustodyDrainSaveData pending in
                 pendingProductionInputDestinationDrains
                 ?? Array.Empty<
                     ProductionInputDestinationCustodyDrainSaveData>())
        {
            if (pending == null
                || !detached.PendingProductionInputDestinationDrains.TryAdd(
                    pending.stepOperationId,
                    pending.Clone()))
            {
                throw new InvalidOperationException(
                    "Duplicate pending production input-destination drain '"
                    + pending?.stepOperationId + "'.");
            }
        }
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

    private static PhysicalItemBatchDispositionSaveData CloneBatchDisposition(
        PhysicalItemBatchDispositionSaveData source) => new()
    {
        kind = source.kind,
        operationId = source.operationId,
        reasonCode = source.reasonCode,
        requestFingerprint = source.requestFingerprint,
        sourceStackIds = (source.sourceStackIds ?? new List<string>()).ToList(),
        quantity = source.quantity,
        inputMassGrams = source.inputMassGrams,
        commitId = source.commitId
    };

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

    /// <summary>
    /// Publishes a prevalidated batch as one repository mutation. No observer can
    /// observe a prefix: failures remove every inserted identity before returning.
    /// Persistent-ID allocation intentionally remains outside this transaction;
    /// failed attempts may consume IDs but can never leave physical records.
    /// </summary>
    internal bool TryAddBatchAtomically(
        IReadOnlyList<WorldItemStackRecord> records,
        Func<int, bool> failBeforeAdd,
        out string failureReason)
    {
        failureReason = string.Empty;
        WorldItemStackRecord[] batch = (records
                ?? Array.Empty<WorldItemStackRecord>())
            .ToArray();
        if (batch.Length == 0
            || batch.Any(record => record == null
                || string.IsNullOrWhiteSpace(record.stackId))
            || batch.Select(record => record.stackId)
                .Distinct(StringComparer.Ordinal).Count() != batch.Length
            || batch.Any(record => RecordsById.ContainsKey(record.stackId))
            || batch.Where(record => !string.IsNullOrEmpty(record.itemInstanceId))
                .Select(record => record.itemInstanceId)
                .Distinct(StringComparer.Ordinal).Count()
                != batch.Count(record => !string.IsNullOrEmpty(record.itemInstanceId))
            || batch.Any(record => !string.IsNullOrEmpty(record.itemInstanceId)
                && Records.Any(existing => string.Equals(
                    existing.itemInstanceId,
                    record.itemInstanceId,
                    StringComparison.Ordinal))))
        {
            failureReason = "world-item-atomic-batch-invalid";
            return false;
        }

        List<WorldItemStackRecord> inserted = new(batch.Length);
        try
        {
            for (int index = 0; index < batch.Length; index++)
            {
                if (failBeforeAdd?.Invoke(index) == true)
                    throw new InvalidOperationException("injected-atomic-publication-failure");
                AddToState(state, batch[index]);
                inserted.Add(batch[index]);
            }
        }
        catch (Exception exception)
        {
            for (int index = inserted.Count - 1; index >= 0; index--)
            {
                WorldItemStackRecord record = inserted[index];
                Records.Remove(record);
                RecordsById.Remove(record.stackId);
                if (RecordsByPosition.TryGetValue(
                        record.position,
                        out List<WorldItemStackRecord> positionRecords))
                {
                    positionRecords.Remove(record);
                    if (positionRecords.Count == 0)
                        RecordsByPosition.Remove(record.position);
                }
            }
            failureReason = exception.Message;
            return false;
        }

        MarkChanged();
        return true;
    }

    [GameplayInternalOnly(
        "Stamps an already-picked Editor fixture record with exact capacity-routing custody without exposing a production custody bypass.",
        "Capacity-routing actor transition focused Editor fixture only")]
    public bool ConfigureEditorCapacityRoutingCarriedStack(
        string stackId,
        string actorPersistentId,
        Vector2Int physicalCell,
        string batchCommitId,
        string outputLineId,
        string lineCommitId,
        int originalStackOrdinal,
        int originalBatchStackCount,
        int originalBatchQuantity,
        long originalBatchMassGrams,
        string routeOperationId,
        string sourceStackId,
        int sourceOffsetQuantity,
        string targetDestinationId,
        Vector2Int targetPosition,
        int quantity,
        long massGrams)
    {
        if (!RecordsById.TryGetValue(
                stackId?.Trim() ?? string.Empty,
                out WorldItemStackRecord record)
            || record == null
            || string.IsNullOrWhiteSpace(actorPersistentId)
            || string.IsNullOrWhiteSpace(batchCommitId)
            || string.IsNullOrWhiteSpace(outputLineId)
            || string.IsNullOrWhiteSpace(lineCommitId)
            || originalStackOrdinal < 0
            || originalStackOrdinal >= originalBatchStackCount
            || originalBatchStackCount <= 0
            || originalBatchQuantity <= 0
            || originalBatchMassGrams <= 0L
            || string.IsNullOrWhiteSpace(routeOperationId)
            || string.IsNullOrWhiteSpace(sourceStackId)
            || sourceOffsetQuantity < 0
            || string.IsNullOrWhiteSpace(targetDestinationId)
            || quantity <= 0
            || massGrams <= 0L
            || record.quantity != quantity)
        {
            return false;
        }
        record.components ??= new List<ItemInstanceComponentSaveData>();
        record.components.RemoveAll(FacilityOutputExactRouteCustodyCodec.IsCustody);
        string businessSignature = ItemStackSignature.Create(
            record.itemId,
            record.components);
        string componentFingerprint =
            ProductionCapacityRoutingDrainFingerprint
                .CreateActorCarryStackSignature(
                    record.itemId,
                    record.itemInstanceId,
                    record.components);
        FacilityOutputExactRouteCustodyMetadata metadata = new(
            FacilityOutputExactRouteCustodyPhase.Routable,
            batchCommitId,
            new string('a', 64),
            new string('b', 64),
            outputLineId,
            lineCommitId,
            originalStackOrdinal,
            originalBatchStackCount,
            originalBatchQuantity,
            originalBatchMassGrams,
            originalLineStackCount: originalBatchStackCount,
            originalLineQuantity: originalBatchQuantity,
            originalLineMassGrams: originalBatchMassGrams,
            record.itemId,
            businessSignature,
            componentFingerprint,
            "facility-output:capacity-routing-editor-origin",
            targetDestinationId,
            sourceStackId,
            sourceStackId,
            physicalCell,
            sourceOffsetQuantity,
            quantity,
            massGrams,
            routeOperationId,
            new string('c', 64),
            new string('d', 64));
        record.components.Add(FacilityOutputExactRouteCustodyCodec.Create(metadata));
        Relocate(record, physicalCell);
        record.state = WorldItemStackState.Carried;
        record.destinationId = actorPersistentId;
        record.hasDestinationPosition = true;
        record.destinationPosition = physicalCell;
        record.sourceStorageDestinationId = string.Empty;
        record.aggregationCohortId = string.Empty;
        record.dropDisposition = WorldItemDropDisposition.None;
        record.recoveryOwnerOperationId = string.Empty;
        record.recoverySourceStackId = string.Empty;
        record.recoveryCarrierPersistentId = string.Empty;
        record.recoveryInterruptionKind =
            WorldItemCarryInterruptionKind.None;
        record.droppedAtGameTime = 0d;
        record.recoveryDeadlineGameTime = 0d;
        MarkChanged();
        return true;
    }

    internal bool TryRemoveBatchAtomically(
        IReadOnlyList<WorldItemStackRecord> records,
        out string failureReason)
    {
        failureReason = string.Empty;
        WorldItemStackRecord[] batch = (records
                ?? Array.Empty<WorldItemStackRecord>())
            .ToArray();
        if (batch.Length == 0
            || batch.Distinct().Count() != batch.Length
            || batch.Any(record => record == null
                || !RecordsById.TryGetValue(record.stackId, out WorldItemStackRecord live)
                || !ReferenceEquals(record, live)
                || !Records.Contains(record)
                || !RecordsByPosition.TryGetValue(
                    record.position,
                    out List<WorldItemStackRecord> positioned)
                || !positioned.Contains(record)
                || record.reservedQuantity != 0
                || !string.IsNullOrEmpty(record.reservedByPersistentId)
                || PrioritizedHaulStackIds.Contains(record.stackId)))
        {
            failureReason = "world-item-atomic-remove-batch-invalid";
            return false;
        }

        foreach (WorldItemStackRecord record in batch)
        {
            Records.Remove(record);
            RecordsById.Remove(record.stackId);
            if (RecordsByPosition.TryGetValue(
                    record.position,
                    out List<WorldItemStackRecord> positionRecords))
            {
                positionRecords.Remove(record);
                if (positionRecords.Count == 0)
                    RecordsByPosition.Remove(record.position);
            }
        }
        MarkChanged();
        return true;
    }

    internal bool TryReplaceBatchComponentsAtomically(
        IReadOnlyDictionary<string, IReadOnlyList<ItemInstanceComponentSaveData>>
            replacements,
        out string failureReason)
    {
        failureReason = string.Empty;
        KeyValuePair<string, IReadOnlyList<ItemInstanceComponentSaveData>>[] batch =
            (replacements ?? new Dictionary<string,
                IReadOnlyList<ItemInstanceComponentSaveData>>())
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        if (batch.Length == 0
            || batch.Any(pair => string.IsNullOrWhiteSpace(pair.Key)
                || pair.Value == null
                || !RecordsById.ContainsKey(pair.Key)
                || pair.Value.Any(component => component == null)))
        {
            failureReason = "world-item-atomic-component-batch-invalid";
            return false;
        }

        Dictionary<string, List<ItemInstanceComponentSaveData>> previous =
            new(StringComparer.Ordinal);
        try
        {
            foreach (KeyValuePair<string, IReadOnlyList<ItemInstanceComponentSaveData>>
                     pair in batch)
            {
                WorldItemStackRecord record = RecordsById[pair.Key];
                previous.Add(
                    pair.Key,
                    (record.components ?? new List<ItemInstanceComponentSaveData>())
                    .Select(component => component?.Clone())
                    .ToList());
                record.components = pair.Value
                    .Select(component => component.Clone())
                    .ToList();
            }
        }
        catch (Exception exception)
        {
            foreach (KeyValuePair<string, List<ItemInstanceComponentSaveData>> pair in
                     previous)
            {
                if (RecordsById.TryGetValue(pair.Key, out WorldItemStackRecord record))
                    record.components = pair.Value;
            }
            failureReason = exception.Message;
            return false;
        }
        MarkChanged();
        return true;
    }

    internal bool TryQuiesceCarriedBatchAtomically(
        IReadOnlyList<WorldItemStackRecord> records,
        Vector2Int physicalCell,
        string targetDestinationId,
        Vector2Int targetPosition,
        ProductionCapacityRoutingDrainSaveData pendingDrain,
        Func<ProductionCapacityRoutingActorQuiesceReceiptSaveData>
            createReceiptAfterMutation,
        Func<int, bool> failBeforeMutation,
        out ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
        out string failureReason)
    {
        receipt = null;
        failureReason = string.Empty;
        WorldItemStackRecord[] batch = (records
                ?? Array.Empty<WorldItemStackRecord>())
            .OrderBy(value => value?.stackId, StringComparer.Ordinal)
            .ToArray();
        string destination = targetDestinationId ?? string.Empty;
        if (batch.Length == 0
            || string.IsNullOrWhiteSpace(destination)
            || !string.Equals(destination, destination.Trim(),
                StringComparison.Ordinal)
            || batch.Distinct().Count() != batch.Length
            || pendingDrain == null
            || !state.PendingCapacityRoutingDrains.TryGetValue(
                pendingDrain.stepOperationId,
                out ProductionCapacityRoutingDrainSaveData liveDrain)
            || !ReferenceEquals(pendingDrain, liveDrain)
            || pendingDrain.phase !=
                ProductionCapacityRoutingDrainPhase.QuiescingActors
            || createReceiptAfterMutation == null
            || batch.Any(record => record == null
                || !RecordsById.TryGetValue(
                    record.stackId,
                    out WorldItemStackRecord live)
                || !ReferenceEquals(record, live)
                || record.state is not (WorldItemStackState.Carried
                    or WorldItemStackState.InTransit)
                || !RecordsByPosition.TryGetValue(
                    record.position,
                    out List<WorldItemStackRecord> positioned)
                || !positioned.Contains(record)
                || record.dropDisposition != WorldItemDropDisposition.None
                || !string.IsNullOrEmpty(record.recoveryOwnerOperationId)
                || !string.IsNullOrEmpty(record.recoverySourceStackId)
                || !string.IsNullOrEmpty(record.recoveryCarrierPersistentId)
                || record.recoveryInterruptionKind !=
                    WorldItemCarryInterruptionKind.None
                || record.droppedAtGameTime != 0d
                || record.recoveryDeadlineGameTime != 0d))
        {
            failureReason = "world-item-atomic-carried-quiesce-invalid";
            return false;
        }

        List<QuiescenceSnapshot> previous = new(batch.Length);
        ProductionCapacityRoutingDrainSaveData previousDrain =
            pendingDrain.Clone();
        try
        {
            for (int index = 0; index < batch.Length; index++)
            {
                if (failBeforeMutation?.Invoke(index) == true)
                {
                    throw new InvalidOperationException(
                        "injected-atomic-carried-quiesce-failure");
                }
                WorldItemStackRecord record = batch[index];
                previous.Add(new QuiescenceSnapshot(record));
                Relocate(record, physicalCell);
                record.state = WorldItemStackState.Loose;
                record.destinationId = destination;
                record.hasDestinationPosition = true;
                record.destinationPosition = targetPosition;
                record.aggregationCohortId = string.Empty;
                record.sourceStorageDestinationId = string.Empty;
                record.dropDisposition = WorldItemDropDisposition.None;
                record.recoveryOwnerOperationId = string.Empty;
                record.recoverySourceStackId = string.Empty;
                record.recoveryCarrierPersistentId = string.Empty;
                record.recoveryInterruptionKind =
                    WorldItemCarryInterruptionKind.None;
                record.droppedAtGameTime = 0d;
                record.recoveryDeadlineGameTime = 0d;
            }

            ProductionCapacityRoutingActorQuiesceReceiptSaveData
                createdReceipt = createReceiptAfterMutation();
            if (createdReceipt == null
                || string.IsNullOrWhiteSpace(createdReceipt.actorPersistentId)
                || pendingDrain.actorQuiesceReceipts.Any(existing =>
                    existing != null
                    && string.Equals(
                        existing.actorPersistentId,
                        createdReceipt.actorPersistentId,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "world-item-atomic-carried-quiesce-receipt-invalid");
            }
            receipt = createdReceipt;
            pendingDrain.actorQuiesceReceipts.Add(createdReceipt.Clone());
        }
        catch (Exception exception)
        {
            for (int index = previous.Count - 1; index >= 0; index--)
                previous[index].Restore(this);
            state.PendingCapacityRoutingDrains[pendingDrain.stepOperationId] =
                previousDrain;
            receipt = null;
            failureReason = exception.Message;
            return false;
        }

        MarkChanged();
        return true;
    }

    private sealed class QuiescenceSnapshot
    {
        private readonly WorldItemStackRecord record;
        private readonly WorldItemStackState state;
        private readonly Vector2Int position;
        private readonly string destinationId;
        private readonly string aggregationCohortId;
        private readonly string sourceStorageDestinationId;
        private readonly bool hasDestinationPosition;
        private readonly Vector2Int destinationPosition;
        private readonly WorldItemDropDisposition dropDisposition;
        private readonly string recoveryOwnerOperationId;
        private readonly string recoverySourceStackId;
        private readonly string recoveryCarrierPersistentId;
        private readonly WorldItemCarryInterruptionKind recoveryInterruptionKind;
        private readonly double droppedAtGameTime;
        private readonly double recoveryDeadlineGameTime;

        internal QuiescenceSnapshot(WorldItemStackRecord record)
        {
            this.record = record;
            state = record.state;
            position = record.position;
            destinationId = record.destinationId;
            aggregationCohortId = record.aggregationCohortId;
            sourceStorageDestinationId = record.sourceStorageDestinationId;
            hasDestinationPosition = record.hasDestinationPosition;
            destinationPosition = record.destinationPosition;
            dropDisposition = record.dropDisposition;
            recoveryOwnerOperationId = record.recoveryOwnerOperationId;
            recoverySourceStackId = record.recoverySourceStackId;
            recoveryCarrierPersistentId = record.recoveryCarrierPersistentId;
            recoveryInterruptionKind = record.recoveryInterruptionKind;
            droppedAtGameTime = record.droppedAtGameTime;
            recoveryDeadlineGameTime = record.recoveryDeadlineGameTime;
        }

        internal void Restore(WorldItemRepository owner)
        {
            owner.Relocate(record, position);
            record.state = state;
            record.destinationId = destinationId;
            record.aggregationCohortId = aggregationCohortId;
            record.sourceStorageDestinationId = sourceStorageDestinationId;
            record.hasDestinationPosition = hasDestinationPosition;
            record.destinationPosition = destinationPosition;
            record.dropDisposition = dropDisposition;
            record.recoveryOwnerOperationId = recoveryOwnerOperationId;
            record.recoverySourceStackId = recoverySourceStackId;
            record.recoveryCarrierPersistentId = recoveryCarrierPersistentId;
            record.recoveryInterruptionKind = recoveryInterruptionKind;
            record.droppedAtGameTime = droppedAtGameTime;
            record.recoveryDeadlineGameTime = recoveryDeadlineGameTime;
        }
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
