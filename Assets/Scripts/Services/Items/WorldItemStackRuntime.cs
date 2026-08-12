using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

public sealed class WorldItemStackRuntime :
    IWorldItemStackRuntime,
    IWorldItemQuantityLeaseRuntime,
    IPhysicalItemRestoreStaging,
    IWorldItemMarkerDataSource,
    IHaulPlanBuilder,
    IStartable,
    ITickable,
    IDisposable
{
    public const string FacilityInputDestinationPrefix = "facility-input:";
    public const string WarehouseStorageDestinationPrefix =
        WarehouseStorageIdentity.DestinationPrefix;
    public const string CombatLoadoutDestinationPrefix = "combat-loadout-pickup:";

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IItemHaulingSettingsProvider haulingSettingsProvider;
    private readonly ICharacterIdRegistry characterIdRegistry;
    private readonly IWorldDropZoneQuery worldDropZoneQuery;
    private readonly ICharacterSpawnerProvider characterSpawnerProvider;
    private readonly IItemMarkerPresenter itemMarkerPresenter;
    private readonly WorldItemRepository itemRepository;
    private readonly IItemReservationService reservationService;
    private readonly IWorldItemSpawner itemSpawner;
    private readonly WorldItemQueryService itemQueryService;
    private readonly IWorldItemHaulPlanningService haulPlanningService;
    private readonly IItemTransferService itemTransferService;
    private readonly ICharacterAiPerformanceRecorder performanceRecorder;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly WorldItemTheftService theftService;
    private readonly WorldItemPersistenceService persistence;
    private readonly WorldItemWarehouseService warehouseService;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private int projectedRestoreRevision;

    private List<WorldItemStackRecord> stacks => itemRepository.Records;
    private Dictionary<string, WorldItemStackRecord> stacksById => itemRepository.RecordsById;
    private Dictionary<Vector2Int, List<WorldItemStackRecord>> stacksByPosition =>
        itemRepository.RecordsByPosition;
    public WorldItemStackRuntime(
        IGridSystemProvider gridSystemProvider,
        ICharacterIdRegistry characterIdRegistry,
        IWorldDropZoneQuery worldDropZoneQuery,
        ICharacterSpawnerProvider characterSpawnerProvider,
        WorldItemReadServices readServices,
        WorldItemMutationServices mutationServices,
        WorldItemPersistenceService persistence,
        WorldItemWarehouseService warehouseService)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.characterIdRegistry = characterIdRegistry ?? throw new ArgumentNullException(nameof(characterIdRegistry));
        this.worldDropZoneQuery = worldDropZoneQuery
            ?? throw new ArgumentNullException(nameof(worldDropZoneQuery));
        this.characterSpawnerProvider = characterSpawnerProvider
            ?? throw new ArgumentNullException(nameof(characterSpawnerProvider));
        WorldItemReadServices requiredRead = readServices
            ?? throw new ArgumentNullException(nameof(readServices));
        WorldItemMutationServices requiredMutations = mutationServices
            ?? throw new ArgumentNullException(nameof(mutationServices));
        catalogProvider = requiredRead.Catalog;
        haulingSettingsProvider = requiredRead.HaulingSettings;
        itemQueryService = requiredRead.Queries;
        itemMarkerPresenter = requiredRead.Markers;
        performanceRecorder = requiredRead.Performance;
        debugRules = requiredRead.DebugRules;
        itemRepository = requiredMutations.Repository;
        aggregateRootStore = itemRepository.AggregateRootStore;
        reservationService = requiredMutations.Reservations;
        itemSpawner = requiredMutations.Spawner;
        haulPlanningService = requiredMutations.HaulPlanning;
        itemTransferService = requiredMutations.Transfers;
        theftService = requiredMutations.Theft;
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.warehouseService = warehouseService
            ?? throw new ArgumentNullException(nameof(warehouseService));
    }

    public IDungeonItemCatalogProvider CatalogProvider => catalogProvider;
    public IItemHaulingSettingsProvider HaulingSettingsProvider => haulingSettingsProvider;
    public bool StoredItemMarkersVisible =>
        itemQueryService.StoredItemMarkersVisible;
    public int ItemStackVersion => itemRepository.ItemStackVersion;
    public int HaulJobVersion => itemRepository.HaulJobVersion;
    public void Start()
    {
        itemMarkerPresenter.Initialize(this);
        projectedRestoreRevision = aggregateRootStore.PublishedRestoreRevision;
        RefreshAllMarkers();
    }

    public void Tick()
    {
        if (projectedRestoreRevision == aggregateRootStore.PublishedRestoreRevision)
        {
            return;
        }

        projectedRestoreRevision = aggregateRootStore.PublishedRestoreRevision;
        ProjectRestoredWorldState();
    }

    public void Dispose()
    {
    }

    public DungeonPhysicalItemSaveData Capture()
    {
        return persistence.Capture();
    }

    public void SetStoredItemMarkersVisible(bool visible)
    {
        itemQueryService.SetStoredItemMarkersVisible(visible);
    }

    public void Restore(DungeonPhysicalItemSaveData snapshot)
    {
        IDungeonSaveRestoreStage stage = StageRestore(snapshot);
        stage.Commit(new DungeonGameRestoreReport());
    }

    public IDungeonSaveRestoreStage StageRestore(
        DungeonPhysicalItemSaveData snapshot)
    {
        WorldItemRestoreState staged = persistence.StageRestore(snapshot);
        return new DungeonDelegateSaveRestoreStage(
            PhysicalItemsSaveSection.Id,
            _ => CommitRestore(staged));
    }

    private void CommitRestore(WorldItemRestoreState staged)
    {
        persistence.Commit(staged);
        if (!aggregateRootStore.IsRestoreStaging)
        {
            ProjectRestoredWorldState();
        }
    }

    private void ProjectRestoredWorldState()
    {
        warehouseService.NormalizeStorageIds();
        RefreshAllMarkers();
    }

    public bool SpawnItemAtDropoff(
        string itemId,
        int amount,
        string sourceLabel,
        out int spawned)
    {
        spawned = 0;
        string normalizedItemId = itemId?.Trim() ?? string.Empty;
        if (normalizedItemId.Length == 0
            || amount <= 0
            || !TryGetDropoffPosition(out Vector2Int dropoff)
            || !catalogProvider.TryGetDefinition(normalizedItemId, out DungeonItemDefinition definition)
            || definition.MaxStack <= 1)
        {
            return false;
        }

        spawned = Spawn(
            normalizedItemId,
            amount,
            dropoff,
            WorldItemStackState.Loose,
            string.Empty);
        return spawned == amount;
    }

    public bool SpawnStockAtDropoff(StockCategory category, int amount, string sourceLabel, out int spawned)
    {
        return SpawnStockAtDropoff(
            category,
            amount,
            sourceLabel,
            WorldItemStackState.Loose,
            string.Empty,
            out spawned);
    }

    public bool SpawnStockAtDropoff(
        StockCategory category,
        int amount,
        string sourceLabel,
        WorldItemStackState state,
        string destinationId,
        out int spawned)
    {
        spawned = 0;
        if (amount <= 0 || !TryGetDropoffPosition(out Vector2Int dropoff))
        {
            return false;
        }

        DungeonItemDefinition definition = catalogProvider.All
            .Where(candidate => candidate != null
                && candidate.StockCategory == category
                && candidate.MaxStack > 1)
            .OrderBy(candidate => candidate.ItemId, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No authored stackable item belongs to stock category '{category}'. "
                + "Unique equipment must be created through its authoritative equipment runtime.");
        spawned = Spawn(definition.ItemId, amount, dropoff, state, destinationId ?? string.Empty);
        return spawned == amount;
    }

    public bool SpawnStockInWarehouse(
        IWarehouseFacility warehouse,
        StockCategory category,
        int amount,
        out int spawned)
    {
        return warehouseService.SpawnStock(
            warehouse,
            category,
            amount,
            out spawned);
    }

    public bool SpawnItemAt(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned)
    {
        spawned = 0;
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return false;
        }

        spawned = Spawn(itemId.Trim(), amount, position, state, destinationId ?? string.Empty);
        return spawned == amount;
    }

    public bool SpawnWasteAt(
        string itemId,
        int amount,
        Vector2Int position,
        WasteOriginKind origin,
        float contamination,
        out int spawned)
    {
        spawned = 0;
        if (string.IsNullOrWhiteSpace(itemId)
            || amount <= 0
            || origin == WasteOriginKind.Unknown)
        {
            return false;
        }

        spawned = Spawn(
            itemId.Trim(),
            amount,
            position,
            WorldItemStackState.Loose,
            string.Empty,
            wasteOrigin: origin,
            contamination: contamination);
        return spawned == amount;
    }

    public bool SpawnUniqueItemAt(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId)
    {
        return itemSpawner.SpawnUnique(
            itemId,
            position,
            state,
            destinationId,
            out stackId);
    }

    public bool SpawnExistingUniqueItemAt(
        string itemId,
        ItemInstanceId itemInstanceId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out string stackId)
    {
        IReadOnlyList<ItemInstanceComponentSaveData> components =
            itemRepository.EquipmentInstances.TryGetValue(
                itemInstanceId.Value,
                out CombatEquipmentInstance equipment)
                ? new[]
                {
                    EquipmentItemStateCodec.Encode(
                        equipment,
                        (equipment.moduleSlots ?? new List<EquipmentModuleSlotState>())
                            .Where(slot => slot != null
                                && !string.IsNullOrWhiteSpace(slot.moduleInstanceId)
                                && itemRepository.EquipmentModules.ContainsKey(
                                    slot.moduleInstanceId))
                            .Select(slot =>
                                itemRepository.EquipmentModules[slot.moduleInstanceId]))
                }
                : Array.Empty<ItemInstanceComponentSaveData>();
        return itemSpawner.SpawnExistingUnique(
            itemId,
            itemInstanceId,
            position,
            state,
            destinationId,
            false,
            default,
            components,
            out stackId);
    }

    public bool SpawnUniqueItemAt(
        string itemId,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        Vector2Int destinationPosition,
        out string stackId)
    {
        return itemSpawner.SpawnUnique(
            itemId,
            position,
            state,
            destinationId,
            destinationPosition,
            out stackId);
    }

    public bool SpawnHumanoidCorpse(
        CharacterActor source,
        Vector2Int position,
        string deathReason,
        out string stackId)
    {
        stackId = string.Empty;
        if (source == null)
        {
            return false;
        }

        if (!CharacterPersistentIdentity.TryGet(source, out CharacterId characterId))
        {
            return false;
        }

        string persistentId = characterId.Value;

        int spawned = Spawn(
            DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
            1,
            position,
            WorldItemStackState.Loose,
            string.Empty,
            sourceCharacterId: persistentId,
            sourceDisplayName: source.Identity?.DisplayName ?? source.name,
            sourceSpeciesTag: source.Identity?.SpeciesTag ?? string.Empty,
            sourceDeathReason: deathReason ?? string.Empty,
            emergencyButcheryAllowed: false);
        if (spawned <= 0)
        {
            return false;
        }

        stackId = stacks.LastOrDefault(record => record != null
            && record.itemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId
            && record.sourceCharacterId == persistentId
            && record.position == position)?.stackId ?? string.Empty;
        return !string.IsNullOrWhiteSpace(stackId);
    }

    public bool TrySetInstanceComponent(
        string stackId,
        ItemInstanceComponentSaveData component)
    {
        if (component == null
            || string.IsNullOrWhiteSpace(component.componentTypeId)
            || !stacksById.TryGetValue(
                stackId?.Trim() ?? string.Empty,
                out WorldItemStackRecord stack)
            || stack == null)
        {
            return false;
        }

        stack.components ??= new List<ItemInstanceComponentSaveData>();
        stack.components.RemoveAll(existing => existing != null
            && string.Equals(
                existing.componentTypeId?.Trim(),
                component.componentTypeId.Trim(),
                StringComparison.Ordinal));
        stack.components.Add(component.Clone());
        MarkStacksChanged();
        return true;
    }

    public bool TryRequestFacilityDelivery(
        StockCategory category,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        return warehouseService.TryRequestCategoryDelivery(
            category,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out failureReason);
    }

    public bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        return warehouseService.TryRequestDelivery(
            itemId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out failureReason);
    }

    public bool TryRequestStackDelivery(
        string stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        return warehouseService.TryRequestStackDelivery(
            stackId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out failureReason);
    }

    public bool TryGetPileAt(Vector2Int position, out WorldItemPileSnapshot pile)
    {
        return itemQueryService.TryGetPileAt(position, out pile);
    }

    public bool TryGetPileTargetAt(
        Vector2Int position,
        out ItemPileInfoTarget target,
        out UnityEngine.Object markerObject)
    {
        return itemQueryService.TryGetPileTargetAt(
            position,
            out target,
            out markerObject);
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetStacksAt(Vector2Int position, bool includeStored = false)
    {
        return itemQueryService.GetStacksAt(position, includeStored);
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks()
    {
        return itemQueryService.GetAllStacks();
    }

    public bool TryFindNearestAvailableStock(
        Vector2Int origin,
        StockCategory category,
        bool preferStored,
        out WorldItemStackSnapshot stack)
    {
        return itemQueryService.TryFindNearestAvailableStock(
            origin,
            category,
            preferStored,
            out stack);
    }

    public void CopyAvailableStockCandidates(
        StockCategory category,
        List<WorldItemStockCandidate> destination)
    {
        itemQueryService.CopyAvailableStockCandidates(category, destination);
    }

    public bool TryFindBestAvailableStack(
        Vector2Int origin,
        Func<string, int> rankSelector,
        out WorldItemStackSnapshot stack)
    {
        return itemQueryService.TryFindBestAvailableStack(
            origin,
            rankSelector,
            out stack);
    }

    public bool HasAvailableHaulJob(CharacterActor actor)
    {
        return haulPlanningService.HasAvailablePlan(actor);
    }

    public bool TryReserveBestHaulPlan(
        CharacterActor actor,
        out WorldItemHaulPlan plan,
        out string failureReason)
    {
        long started = performanceRecorder?.DetailedCollectionEnabled == true
            ? Stopwatch.GetTimestamp()
            : 0L;
        try
        {
            return haulPlanningService.TryReserveBestPlan(actor, out plan, out failureReason);
        }
        finally
        {
            if (started != 0L)
            {
                performanceRecorder.Record(
                    AiPerformanceCategory.HaulPlanning,
                    (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency);
            }
        }
    }

    public bool TryReserveStoredItemForDirectPickup(
        CharacterActor actor,
        string itemId,
        int quantity,
        out WorldItemReservedStackQuantity reservation,
        out Vector2Int pickupStandPosition,
        out string failureReason)
    {
        return warehouseService.TryReserveStoredForDirectPickup(
            actor,
            itemId,
            quantity,
            out reservation,
            out pickupStandPosition,
            out failureReason);
    }

    public bool TryReserveBestHaulJob(
        CharacterActor actor,
        out WorldItemHaulJob job,
        out string failureReason)
    {
        return haulPlanningService.TryReserveBestJob(actor, out job, out failureReason);
    }

    public bool TryPickupReservedStackQuantity(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemReservedStackQuantity reservation,
        out int pickedUp,
        out string failureReason)
    {
        return itemTransferService.TryPickupReservedStackQuantity(
            actor,
            inventory,
            reservation,
            out pickedUp,
            out failureReason);
    }

    public bool TryPickupReservedStack(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemHaulJob job,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || inventory == null || !job.IsValid)
        {
            failureReason = "invalid haul job";
            return false;
        }

        if (!stacksById.TryGetValue(job.StackId, out WorldItemStackRecord record)
            || record.quantity <= 0)
        {
            failureReason = "stack disappeared";
            return false;
        }

        if (string.IsNullOrWhiteSpace(job.LeaseId)
            || string.IsNullOrWhiteSpace(job.OwnerOperationId))
        {
            failureReason = "haul job has no quantity lease";
            return false;
        }

        WorldItemReservedStackQuantity reservation = new WorldItemReservedStackQuantity(
            record.stackId,
            record.itemId,
            Mathf.Max(1, job.Quantity),
            record.position,
            job.DestinationKind,
            job.DestinationId,
            job.LeaseId,
            job.OwnerOperationId);
        return TryPickupReservedStackQuantity(
            actor,
            inventory,
            reservation,
            out _,
            out failureReason);
    }

    public bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        out string failureReason)
    {
        return itemTransferService.TryDepositCarriedItems(
            actor,
            inventory,
            warehouse,
            out failureReason);
    }

    public bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        out string failureReason)
    {
        return itemTransferService.TryDepositCarriedItemsToFacility(
            actor,
            inventory,
            destinationPosition,
            destinationId,
            out failureReason);
    }

    public bool TryConsumeFacilityBuffer(
        string destinationId,
        IReadOnlyDictionary<StockCategory, int> costs,
        out string failureReason)
    {
        return itemTransferService.TryConsumeFacilityBuffer(
            destinationId,
            costs,
            out failureReason);
    }

    public bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason)
    {
        return itemTransferService.TryConsumeFacilityItemBuffer(
            destinationId,
            costs,
            out failureReason);
    }

    public bool TryStealLooseItem(
        CharacterActor actor,
        int searchRadius,
        out WorldItemStackSnapshot stolenItem,
        out string failureReason)
    {
        return theftService.TryStealLooseItem(
            actor,
            searchRadius,
            out stolenItem,
            out failureReason);
    }

    public void ReleaseReservation(string stackId, string persistentId)
    {
        itemTransferService.ReleaseQuantityReservationsByOwner(
            $"haul:{persistentId?.Trim() ?? string.Empty}",
            ItemReservationReleaseReason.Cancelled);
        reservationService.Release(stackId, persistentId);
    }

    public bool TryRenewQuantityLease(
        string leaseId,
        double requestedUntilGameSeconds,
        out string failureReason)
    {
        bool renewed = itemTransferService.RenewQuantityReservation(
            leaseId,
            requestedUntilGameSeconds,
            out DomainFailure failure);
        failureReason = renewed ? string.Empty : failure.ToString();
        return renewed;
    }

    public bool TryClearReservation(string stackId)
    {
        return reservationService.TryClear(stackId);
    }

    public bool SetForbidden(string stackId, bool forbidden)
    {
        return reservationService.SetForbidden(stackId, forbidden);
    }

    public bool PrioritizeHaul(string stackId)
    {
        return reservationService.PrioritizeHaul(stackId);
    }

    public bool TryRouteStackToDestination(
        string stackId,
        WorldItemStackState state,
        string destinationId,
        Vector2Int destinationPosition,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(stackId)
            || string.IsNullOrWhiteSpace(destinationId)
            || !stacksById.TryGetValue(stackId, out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.reservedQuantity > 0)
        {
            failureReason = "이동시킬 물품 스택을 찾을 수 없습니다.";
            return false;
        }

        record.state = state;
        record.destinationId = destinationId.Trim();
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = true;
        record.destinationPosition = destinationPosition;
        record.reservedByPersistentId = string.Empty;
        MarkStacksChanged();
        RefreshMarkerAt(record.position);
        return true;
    }

    public bool DeleteStack(string stackId)
    {
        if (string.IsNullOrWhiteSpace(stackId)
            || !stacksById.TryGetValue(stackId, out WorldItemStackRecord record))
        {
            return false;
        }

        Vector2Int position = record.position;
        if (!string.IsNullOrWhiteSpace(record.itemInstanceId))
        {
            itemRepository.TryMarkEquipmentLostBySourceStack(record.stackId);
            itemRepository.TryMarkModuleLostBySourceStack(record.stackId);
        }
        RemoveRecord(record);
        RefreshMarkerAt(position);
        return true;
    }

    public bool TryAbsorbUniqueItemStack(
        string stackId,
        ItemInstanceId expectedInstanceId)
    {
        string normalizedStackId = stackId?.Trim() ?? string.Empty;
        if (!expectedInstanceId.IsValid
            || !stacksById.TryGetValue(
                normalizedStackId,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity != 1
            || !string.Equals(
                record.itemInstanceId,
                expectedInstanceId.Value,
                StringComparison.Ordinal))
        {
            return false;
        }

        Vector2Int position = record.position;
        RemoveRecord(record);
        RefreshMarkerAt(position);
        return true;
    }

    public bool TryConsumeStackQuantity(string stackId, int quantity, out WorldItemStackSnapshot consumed)
    {
        ItemStackId typedStackId = new(stackId);
        if (!typedStackId.IsValid)
        {
            consumed = null;
            return false;
        }

        return itemTransferService.TryConsumeStackQuantity(
            typedStackId,
            quantity,
            out consumed,
            out _);
    }

    public bool SetEmergencyButcheryAllowed(string stackId, bool allowed)
    {
        if (string.IsNullOrWhiteSpace(stackId)
            || !stacksById.TryGetValue(stackId, out WorldItemStackRecord record)
            || record == null
            || record.itemId != DarkSurvivalItemDefinitions.HumanoidCorpseItemId)
        {
            return false;
        }

        record.emergencyButcheryAllowed = allowed;
        MarkStacksChanged();
        RefreshMarkerAt(record.position);
        return true;
    }

    public int RemoveStacksByStateAndDestination(WorldItemStackState state, string destinationId)
    {
        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDestination))
        {
            return 0;
        }

        WorldItemStackRecord[] targets = stacks
            .Where(stack => stack != null
                && stack.state == state
                && string.Equals(
                    stack.destinationId ?? string.Empty,
                    normalizedDestination,
                    StringComparison.Ordinal))
            .ToArray();
        int removed = 0;
        foreach (WorldItemStackRecord target in targets)
        {
            Vector2Int position = target.position;
            removed += Mathf.Max(0, target.quantity);
            if (state == WorldItemStackState.Stored && IsOutboundStoredStack(target))
            {
                int quantity = target.quantity;
                string itemId = target.itemId;
                string sourceStorageDestinationId = target.sourceStorageDestinationId;
                RemoveRecord(target);
                Spawn(
                    itemId,
                    quantity,
                    position,
                    WorldItemStackState.Stored,
                    sourceStorageDestinationId);
            }
            else
            {
                RemoveRecord(target);
            }

            RefreshMarkerAt(position);
        }

        return removed;
    }

    public int ReleaseStacksByDestination(
        string destinationId,
        Vector2Int releasePosition)
    {
        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDestination))
        {
            return 0;
        }

        WorldItemStackRecord[] targets = stacks
            .Where(stack => stack != null
                && string.Equals(
                    stack.destinationId ?? string.Empty,
                    normalizedDestination,
                    StringComparison.Ordinal))
            .ToArray();
        int released = 0;
        foreach (WorldItemStackRecord target in targets)
        {
            int quantity = Mathf.Max(0, target.quantity);
            if (quantity <= 0)
            {
                continue;
            }

            released += quantity;
            Vector2Int oldPosition = target.position;
            string itemId = target.itemId;
            if (target.state == WorldItemStackState.Stored
                && IsOutboundStoredStack(target))
            {
                string sourceStorageDestinationId =
                    target.sourceStorageDestinationId ?? string.Empty;
                RemoveRecord(target);
                Spawn(
                    itemId,
                    quantity,
                    oldPosition,
                    WorldItemStackState.Stored,
                    sourceStorageDestinationId);
            }
            else
            {
                Vector2Int loosePosition =
                    target.state == WorldItemStackState.FacilityBuffer
                    || target.state == WorldItemStackState.FacilityOutputBuffer
                        ? releasePosition
                        : oldPosition;
                RemoveRecord(target);
                Spawn(
                    itemId,
                    quantity,
                    loosePosition,
                    WorldItemStackState.Loose,
                    string.Empty);
                RefreshMarkerAt(loosePosition);
            }

            RefreshMarkerAt(oldPosition);
        }

        return released;
    }

    private int Spawn(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        bool hasDestinationPosition = false,
        Vector2Int destinationPosition = default,
        string sourceCharacterId = "",
        string sourceDisplayName = "",
        string sourceSpeciesTag = "",
        string sourceDeathReason = "",
        bool emergencyButcheryAllowed = false,
        string sourceStorageDestinationId = "",
        WasteOriginKind wasteOrigin = WasteOriginKind.Unknown,
        float contamination = 0f)
    {
        return itemSpawner.Spawn(
            itemId,
            amount,
            position,
            state,
            destinationId,
            hasDestinationPosition,
            destinationPosition,
            sourceCharacterId,
            sourceDisplayName,
            sourceSpeciesTag,
            sourceDeathReason,
            emergencyButcheryAllowed,
            sourceStorageDestinationId,
            wasteOrigin,
            contamination);
    }

    private static WasteOriginKind ResolveLegacyWasteOrigin(string itemId)
    {
        string id = itemId?.Trim() ?? string.Empty;
        if (string.Equals(id, "waste:plant-rot", StringComparison.Ordinal))
        {
            return WasteOriginKind.Plant;
        }

        if (string.Equals(id, "waste:animal-rot", StringComparison.Ordinal))
        {
            return WasteOriginKind.Animal;
        }

        if (string.Equals(id, "waste:forbidden-rot", StringComparison.Ordinal))
        {
            return WasteOriginKind.Forbidden;
        }

        return IsLegacyWasteItem(id)
            ? WasteOriginKind.Mixed
            : WasteOriginKind.Unknown;
    }

    private static bool IsLegacyWasteItem(string itemId)
    {
        string id = itemId?.Trim() ?? string.Empty;
        return id.StartsWith("waste:", StringComparison.Ordinal)
            || string.Equals(id, WildlifeItemDefinitions.RotItemId, StringComparison.Ordinal);
    }

    private static string GetStoredSourceDestinationId(WorldItemStackRecord stack)
    {
        if (stack == null || stack.state != WorldItemStackState.Stored)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId))
        {
            return stack.sourceStorageDestinationId.Trim();
        }

        string destinationId = stack.destinationId?.Trim() ?? string.Empty;
        return destinationId.StartsWith(WarehouseStorageDestinationPrefix, StringComparison.Ordinal)
            ? destinationId
            : string.Empty;
    }

    private static Vector2Int ResolveWarehouseStoragePosition(IWarehouseFacility warehouse)
    {
        return warehouse is BuildableObject building ? building.centerPos : Vector2Int.zero;
    }

    private static bool IsOutboundStoredStack(WorldItemStackRecord stack)
    {
        return stack != null
            && stack.state == WorldItemStackState.Stored
            && stack.hasDestinationPosition
            && !string.IsNullOrWhiteSpace(stack.destinationId)
            && !string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
            && (IsFacilityInputDestination(stack.destinationId)
                || IsCombatLoadoutDestination(stack.destinationId));
    }

    private static bool IsFacilityInputDestination(string destinationId)
    {
        return !string.IsNullOrWhiteSpace(destinationId)
            && (destinationId.StartsWith(FacilityInputDestinationPrefix, StringComparison.Ordinal)
                || destinationId.StartsWith(WorkOrderRuntime.ConstructionDestinationPrefix, StringComparison.Ordinal));
    }

    private static bool IsCombatLoadoutDestination(string destinationId)
    {
        return !string.IsNullOrWhiteSpace(destinationId)
            && destinationId.StartsWith(CombatLoadoutDestinationPrefix, StringComparison.Ordinal);
    }

    private bool TryGetDropoffPosition(out Vector2Int dropoff)
    {
        if (worldDropZoneQuery.TryGetDeliveryDropoff(out dropoff))
        {
            return true;
        }

        if (characterSpawnerProvider.TryGetSpawner(out CharacterSpawner spawner)
            && spawner.TryGetEntryGridPosition(out dropoff))
        {
            return true;
        }

        if (TryGetGrid(out Grid grid))
        {
            GridCell cell = grid.GetCells()
                .Where(candidate => candidate != null && grid.IsWalkable(candidate.Position))
                .OrderBy(candidate => candidate.Position.y)
                .ThenBy(candidate => candidate.Position.x)
                .FirstOrDefault();
            if (cell != null)
            {
                dropoff = cell.Position;
                return true;
            }
        }

        dropoff = default;
        return false;
    }

    private void RemoveRecord(WorldItemStackRecord record)
    {
        itemRepository.Remove(record);
    }

    private void MarkStacksChanged()
    {
        itemRepository.MarkChanged();
    }

    private WorldItemStackSnapshot ToSnapshot(WorldItemStackRecord stack)
    {
        return itemQueryService.CreateSnapshot(stack);
    }

    private void RefreshAllMarkers()
    {
        itemMarkerPresenter.RefreshAll(stacks
            .Where(stack => stack != null)
            .Select(stack => stack.position)
            .Distinct()
            .ToArray());
    }

    private void RefreshMarkerAt(Vector2Int position)
    {
        itemMarkerPresenter.RefreshAt(position);
    }

    private bool TryGetGrid(out Grid grid)
    {
        return gridSystemProvider.TryGetGrid(out grid);
    }

}
