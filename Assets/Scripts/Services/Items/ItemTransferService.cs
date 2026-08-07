using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IItemTransferService
{
    bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure);

    bool TryRequestStackDelivery(
        ItemStackId stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure);

    bool TryConsumeStackQuantity(
        ItemStackId stackId,
        int quantity,
        out WorldItemStackSnapshot consumed,
        out DomainFailure failure);

    bool TrySpawnItem(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned);

    bool TrySpawnItemWithComponents(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out int spawned);

    bool TryRouteFacilityOutput(
        string sourceDestinationId,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int routed,
        out DomainFailure failure);

    void PrioritizeDestination(string destinationId);
    int ReleaseDestination(string destinationId, Vector2Int releasePosition);
    int RemoveDestination(
        string destinationId,
        params WorldItemStackState[] states);

    bool TryInspectStackForTransit(
        ItemStackId stackId,
        out ItemTransitStackSnapshot stack);

    void CopyLoadableTransitStackIds(
        Vector2Int position,
        List<ItemStackId> destination);

    bool TryBeginTransit(
        ItemStackId stackId,
        Vector2Int expectedPosition,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack,
        out DomainFailure failure);

    bool TryGetTransitStack(
        ItemStackId stackId,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack);

    bool TryCompleteTransit(
        ItemStackId stackId,
        string transitOwnerId,
        WorldItemStackState destinationState,
        Vector2Int destinationPosition,
        string destinationId,
        out DomainFailure failure);

    bool TryPickupReservedStackQuantity(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemReservedStackQuantity reservation,
        out int pickedUp,
        out string failureReason);

    bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        out string failureReason);

    bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        out string failureReason);

    bool TryConsumeFacilityBuffer(
        string destinationId,
        IReadOnlyDictionary<StockCategory, int> costs,
        out string failureReason);

    bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason);
}

public readonly struct ItemTransitStackSnapshot
{
    public ItemTransitStackSnapshot(
        ItemStackId stackId,
        string itemId,
        int quantity,
        bool forbidden,
        float contamination)
    {
        StackId = stackId;
        ItemId = itemId ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
        Forbidden = forbidden;
        Contamination = Mathf.Clamp(contamination, 0f, 100f);
    }

    public ItemStackId StackId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
    public bool Forbidden { get; }
    public float Contamination { get; }
    public bool IsValid => StackId.IsValid && Quantity > 0;
}

public sealed class ItemTransferService : IItemTransferService
{
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IItemHaulingSettingsProvider haulingSettingsProvider;
    private readonly ICharacterIdRegistry characterIdRegistry;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ICombatEquipmentCatalog combatEquipmentCatalog;
    private readonly IGameEventBus gameEventBus;
    private readonly WorldItemRepository repository;
    private readonly IWorldItemSpawner itemSpawner;
    private readonly IItemMarkerPresenter markerPresenter;
    private readonly IDungeonDebugRuleQuery debugRules;
    private readonly WorldItemQueryService itemQueries;
    private readonly WorldItemWarehouseService warehouseService;

    public ItemTransferService(
        WorldItemReadServices readServices,
        ICharacterIdRegistry characterIdRegistry,
        ICharacterAiWorldRegistry worldRegistry,
        ICombatEquipmentCatalog combatEquipmentCatalog,
        IGameEventBus gameEventBus,
        WorldItemRepository repository,
        IWorldItemSpawner itemSpawner,
        WorldItemWarehouseService warehouseService)
    {
        WorldItemReadServices reads = readServices
            ?? throw new ArgumentNullException(nameof(readServices));
        catalogProvider = reads.Catalog;
        haulingSettingsProvider = reads.HaulingSettings;
        this.characterIdRegistry = characterIdRegistry
            ?? throw new ArgumentNullException(nameof(characterIdRegistry));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.combatEquipmentCatalog = combatEquipmentCatalog
            ?? throw new ArgumentNullException(nameof(combatEquipmentCatalog));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.itemSpawner = itemSpawner
            ?? throw new ArgumentNullException(nameof(itemSpawner));
        markerPresenter = reads.Markers;
        debugRules = reads.DebugRules;
        itemQueries = reads.Queries;
        this.warehouseService = warehouseService
            ?? throw new ArgumentNullException(nameof(warehouseService));
    }

    public bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure)
    {
        bool succeeded = warehouseService.TryRequestDelivery(
            itemId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out string rawFailure);
        failure = succeeded
            ? DomainFailure.None
            : new DomainFailure(
                string.IsNullOrWhiteSpace(destinationId)
                    ? FailureCode.ItemTransferDestinationMissing
                    : FailureCode.ItemTransferRequestFailed,
                itemId?.Trim() ?? string.Empty);
        return succeeded;
    }

    public bool TryRequestStackDelivery(
        ItemStackId stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure)
    {
        if (!stackId.IsValid)
        {
            requested = 0;
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable);
            return false;
        }
        bool succeeded = warehouseService.TryRequestStackDelivery(
            stackId.Value,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out string rawFailure);
        failure = succeeded
            ? DomainFailure.None
            : new DomainFailure(
                string.IsNullOrWhiteSpace(destinationId)
                    ? FailureCode.ItemTransferDestinationMissing
                    : FailureCode.ItemTransferRequestFailed,
                stackId.Value);
        return succeeded;
    }

    public bool TryConsumeStackQuantity(
        ItemStackId stackId,
        int quantity,
        out WorldItemStackSnapshot consumed,
        out DomainFailure failure)
    {
        consumed = null;
        failure = DomainFailure.None;
        if (!stackId.IsValid
            || quantity <= 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0)
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferStackUnavailable,
                stackId.Value);
            return false;
        }

        consumed = itemQueries.CreateSnapshot(record);
        consumed.Quantity = Mathf.Min(quantity, record.quantity);
        if (debugRules.ShouldSkipCosts())
        {
            return consumed.Quantity > 0;
        }

        Vector2Int position = record.position;
        record.quantity -= consumed.Quantity;
        repository.MarkChanged();
        if (record.quantity <= 0)
        {
            if (!string.IsNullOrWhiteSpace(record.itemInstanceId))
            {
                repository.TryMarkEquipmentLostBySourceStack(record.stackId);
            }
            repository.Remove(record);
        }
        markerPresenter.RefreshAt(position);
        return consumed.Quantity > 0;
    }

    public bool TrySpawnItem(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        out int spawned)
    {
        spawned = itemSpawner.Spawn(
            itemId,
            amount,
            position,
            state,
            destinationId?.Trim() ?? string.Empty);
        return spawned == Mathf.Max(0, amount);
    }

    public bool TrySpawnItemWithComponents(
        string itemId,
        int amount,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out int spawned)
    {
        spawned = itemSpawner.Spawn(
            itemId,
            amount,
            position,
            state,
            destinationId?.Trim() ?? string.Empty,
            components: components);
        return spawned == Mathf.Max(0, amount);
    }

    public bool TryRouteFacilityOutput(
        string sourceDestinationId,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int routed,
        out DomainFailure failure)
    {
        routed = 0;
        failure = DomainFailure.None;
        string sourceId = sourceDestinationId?.Trim() ?? string.Empty;
        string normalizedItemId = itemId?.Trim() ?? string.Empty;
        string targetId = destinationId?.Trim() ?? string.Empty;
        if (sourceId.Length == 0
            || normalizedItemId.Length == 0
            || amount <= 0)
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferRequestFailed,
                normalizedItemId);
            return false;
        }

        WorldItemStackRecord[] candidates = repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && record.state == WorldItemStackState.FacilityOutputBuffer
                && !record.forbidden
                && string.IsNullOrWhiteSpace(record.reservedByPersistentId)
                && string.Equals(
                    record.destinationId,
                    sourceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    record.itemId,
                    normalizedItemId,
                    StringComparison.Ordinal))
            .OrderBy(record => record.stackId, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Sum(record => record.quantity) < amount)
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferStackUnavailable,
                normalizedItemId);
            return false;
        }

        int remaining = amount;
        foreach (WorldItemStackRecord source in candidates)
        {
            if (remaining <= 0)
            {
                break;
            }

            int moved = Mathf.Min(remaining, source.quantity);
            Vector2Int sourcePosition = source.position;
            source.quantity -= moved;
            repository.MarkChanged();
            if (source.quantity <= 0)
            {
                repository.Remove(source);
            }

            int spawned = itemSpawner.Spawn(
                normalizedItemId,
                moved,
                sourcePosition,
                WorldItemStackState.Loose,
                targetId,
                hasDestinationPosition: targetId.Length > 0,
                destinationPosition: destinationPosition,
                wasteOrigin: source.wasteOrigin,
                contamination: source.contamination);
            if (spawned != moved)
            {
                itemSpawner.Spawn(
                    normalizedItemId,
                    moved - Mathf.Max(0, spawned),
                    sourcePosition,
                    WorldItemStackState.FacilityOutputBuffer,
                    sourceId,
                    wasteOrigin: source.wasteOrigin,
                    contamination: source.contamination);
                routed += Mathf.Max(0, spawned);
                failure = new DomainFailure(
                    FailureCode.ItemTransferRequestFailed,
                    normalizedItemId);
                return false;
            }

            routed += moved;
            remaining -= moved;
            markerPresenter.RefreshAt(sourcePosition);
        }

        if (targetId.Length > 0)
        {
            warehouseService.PrioritizeDestination(targetId);
        }
        return routed == amount;
    }

    public void PrioritizeDestination(string destinationId)
    {
        warehouseService.PrioritizeDestination(destinationId);
    }

    public int ReleaseDestination(
        string destinationId,
        Vector2Int releasePosition)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            return 0;
        }
        WorldItemStackRecord[] targets = repository.Records
            .Where(record => record != null
                && string.Equals(
                    record.destinationId,
                    destination,
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
            string sourceDestination = IsOutboundStoredStack(target)
                ? target.sourceStorageDestinationId
                : string.Empty;
            WorldItemStackState state = sourceDestination.Length > 0
                ? WorldItemStackState.Stored
                : WorldItemStackState.Loose;
            Vector2Int position = state == WorldItemStackState.Stored
                ? oldPosition
                : target.state is WorldItemStackState.FacilityBuffer
                    or WorldItemStackState.FacilityOutputBuffer
                        ? releasePosition
                        : oldPosition;
            repository.Remove(target);
            itemSpawner.Spawn(
                target.itemId,
                quantity,
                position,
                state,
                sourceDestination);
            markerPresenter.RefreshAt(oldPosition);
            markerPresenter.RefreshAt(position);
        }
        return released;
    }

    public int RemoveDestination(
        string destinationId,
        params WorldItemStackState[] states)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        HashSet<WorldItemStackState> allowed = new(
            states ?? Array.Empty<WorldItemStackState>());
        if (destination.Length == 0 || allowed.Count == 0)
        {
            return 0;
        }
        WorldItemStackRecord[] targets = repository.Records
            .Where(record => record != null
                && allowed.Contains(record.state)
                && string.Equals(
                    record.destinationId,
                    destination,
                    StringComparison.Ordinal))
            .ToArray();
        int removed = 0;
        foreach (WorldItemStackRecord target in targets)
        {
            removed += Mathf.Max(0, target.quantity);
            Vector2Int position = target.position;
            repository.Remove(target);
            markerPresenter.RefreshAt(position);
        }
        return removed;
    }

    public bool TryBeginTransit(
        ItemStackId stackId,
        Vector2Int expectedPosition,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack,
        out DomainFailure failure)
    {
        stack = default;
        failure = DomainFailure.None;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        if (!stackId.IsValid
            || ownerId.Length == 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackUnavailable,
                stackId.Value);
            return false;
        }

        if (record.state is not (WorldItemStackState.Loose
                or WorldItemStackState.FacilityOutputBuffer))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackUnavailable,
                stackId.Value);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(record.reservedByPersistentId))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackReserved,
                stackId.Value);
            return false;
        }

        if (Mathf.Abs(record.position.x - expectedPosition.x)
                + Mathf.Abs(record.position.y - expectedPosition.y)
            > 1)
        {
            failure = new DomainFailure(
                FailureCode.ConveyorStackOutOfRange,
                stackId.Value);
            return false;
        }

        Vector2Int previousPosition = record.position;
        record.state = WorldItemStackState.InTransit;
        record.destinationId = ownerId;
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
        record.reservedByPersistentId = string.Empty;
        repository.MarkChanged();
        markerPresenter.RefreshAt(previousPosition);
        stack = CreateTransitSnapshot(record);
        return true;
    }

    public bool TryInspectStackForTransit(
        ItemStackId stackId,
        out ItemTransitStackSnapshot stack)
    {
        stack = default;
        if (!stackId.IsValid
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0)
        {
            return false;
        }

        stack = CreateTransitSnapshot(record);
        return true;
    }

    public void CopyLoadableTransitStackIds(
        Vector2Int position,
        List<ItemStackId> destination)
    {
        if (destination == null)
        {
            return;
        }

        destination.Clear();
        if (!repository.RecordsByPosition.TryGetValue(
                position,
                out List<WorldItemStackRecord> records))
        {
            return;
        }

        foreach (WorldItemStackRecord record in records
                     .Where(record => record != null
                         && record.quantity > 0
                         && record.state is WorldItemStackState.Loose
                             or WorldItemStackState.FacilityOutputBuffer
                         && string.IsNullOrWhiteSpace(
                             record.reservedByPersistentId))
                     .OrderBy(record => record.stackId, StringComparer.Ordinal))
        {
            destination.Add(new ItemStackId(record.stackId));
        }
    }

    public bool TryGetTransitStack(
        ItemStackId stackId,
        string transitOwnerId,
        out ItemTransitStackSnapshot stack)
    {
        stack = default;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        if (!stackId.IsValid
            || ownerId.Length == 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0
            || record.state != WorldItemStackState.InTransit
            || !string.Equals(
                record.destinationId,
                ownerId,
                StringComparison.Ordinal))
        {
            return false;
        }

        stack = CreateTransitSnapshot(record);
        return true;
    }

    public bool TryCompleteTransit(
        ItemStackId stackId,
        string transitOwnerId,
        WorldItemStackState destinationState,
        Vector2Int destinationPosition,
        string destinationId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string ownerId = transitOwnerId?.Trim() ?? string.Empty;
        if (!stackId.IsValid
            || ownerId.Length == 0
            || !repository.RecordsById.TryGetValue(
                stackId.Value,
                out WorldItemStackRecord record)
            || record == null
            || record.state != WorldItemStackState.InTransit
            || !string.Equals(
                record.destinationId,
                ownerId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorTransitOwnershipMismatch,
                stackId.Value,
                ownerId);
            return false;
        }

        if (destinationState is not (WorldItemStackState.Loose
                or WorldItemStackState.Stored
                or WorldItemStackState.FacilityBuffer
                or WorldItemStackState.FacilityOutputBuffer))
        {
            failure = new DomainFailure(
                FailureCode.ConveyorDestinationUnavailable,
                destinationState.ToString());
            return false;
        }

        Vector2Int previousPosition = record.position;
        repository.Relocate(record, destinationPosition);
        record.state = destinationState;
        record.destinationId = destinationState == WorldItemStackState.Loose
            ? string.Empty
            : destinationId?.Trim() ?? string.Empty;
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = false;
        record.destinationPosition = default;
        record.reservedByPersistentId = string.Empty;
        repository.MarkChanged();
        markerPresenter.RefreshAt(previousPosition);
        markerPresenter.RefreshAt(destinationPosition);
        return true;
    }

    private static ItemTransitStackSnapshot CreateTransitSnapshot(
        WorldItemStackRecord record) =>
        new ItemTransitStackSnapshot(
            new ItemStackId(record.stackId),
            record.itemId,
            record.quantity,
            record.forbidden,
            record.contamination);

    public bool TryPickupReservedStackQuantity(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        WorldItemReservedStackQuantity reservation,
        out int pickedUp,
        out string failureReason)
    {
        pickedUp = 0;
        failureReason = string.Empty;
        if (actor == null || inventory == null || !reservation.IsValid)
        {
            failureReason = "invalid haul reservation";
            return false;
        }

        string actorId = characterIdRegistry.GetOrAssignPersistentId(actor);
        if (!repository.RecordsById.TryGetValue(
                reservation.StackId,
                out WorldItemStackRecord record)
            || record.quantity <= 0)
        {
            failureReason = "stack disappeared";
            return false;
        }

        if (!string.Equals(
                record.reservedByPersistentId,
                actorId,
                StringComparison.Ordinal))
        {
            failureReason = "stack reserved by someone else";
            return false;
        }

        int requested = Mathf.Min(
            record.quantity,
            Mathf.Max(1, reservation.Quantity));
        if (IsOutboundStoredStack(record))
        {
            int accepted = inventory.GetMaxAcceptableQuantity(
                record.itemId,
                requested,
                catalogProvider,
                haulingSettingsProvider);
            if (accepted <= 0)
            {
                failureReason = "carry limit";
                return false;
            }

            if (!TryWithdrawOutboundStoredStock(
                    record,
                    accepted,
                    out IWarehouseFacility sourceWarehouse,
                    out StockCategory sourceCategory,
                    out pickedUp,
                    out failureReason))
            {
                return false;
            }

            if (!inventory.TryAddPartialStack(
                    record.stackId,
                    record.itemInstanceId,
                    record.itemId,
                    pickedUp,
                    catalogProvider,
                    haulingSettingsProvider,
                    record.wasteOrigin,
                    record.contamination,
                    record.components,
                    out int acceptedQuantity,
                    out string carryFailure))
            {
                pickedUp = 0;
                failureReason = string.IsNullOrWhiteSpace(carryFailure)
                    ? "carry limit"
                    : carryFailure;
                return false;
            }

            if (acceptedQuantity != pickedUp)
            {
                pickedUp = acceptedQuantity;
            }
        }
        else if (!inventory.TryAddPartialStack(
                     record.stackId,
                     record.itemInstanceId,
                     record.itemId,
                     requested,
                     catalogProvider,
                     haulingSettingsProvider,
                     record.wasteOrigin,
                     record.contamination,
                     record.components,
                     out pickedUp,
                     out failureReason)
                 || pickedUp <= 0)
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "carry limit"
                : failureReason;
            return false;
        }

        Vector2Int position = record.position;
        repository.TrySetEquipmentWorldStateBySourceStack(
            record.stackId,
            CombatEquipmentWorldState.Carried);
        record.quantity -= pickedUp;
        record.reservedByPersistentId = string.Empty;
        if (record.quantity > 0
            && IsCombatLoadoutDestination(record.destinationId))
        {
            RestoreDirectPickupStack(record);
        }

        repository.MarkChanged();
        if (record.quantity <= 0)
        {
            repository.Remove(record);
        }

        markerPresenter.RefreshAt(position);
        return true;
    }

    public bool TryDepositCarriedItems(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        IWarehouseFacility warehouse,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || inventory == null || warehouse == null
            || !warehouse.HasWarehouseInventory
            || warehouse.Inventory == null)
        {
            failureReason = "warehouse unavailable";
            return false;
        }

        List<CharacterCarriedItemSaveData> carried = inventory.RemoveAllItems();
        if (carried.Count == 0)
        {
            failureReason = "nothing carried";
            return false;
        }

        Vector2Int dropPosition = ResolveActorGridPosition(actor);
        bool depositedAny = false;
        foreach (CharacterCarriedItemSaveData item in carried)
        {
            if (item == null || item.quantity <= 0)
            {
                continue;
            }

            int remaining = item.quantity;
            if (TryGetWarehouseStockCategory(
                    item.itemId,
                    out StockCategory category)
                && string.IsNullOrWhiteSpace(item.itemInstanceId))
            {
                int deposited = Mathf.Min(
                    remaining,
                    warehouse.Inventory.RemainingCapacity);
                if (deposited > 0)
                {
                    AddStoredWarehouseItems(
                        warehouse,
                        item.itemId,
                        deposited,
                        item.wasteOrigin,
                        item.contamination,
                        item.components);
                }

                remaining -= deposited;
                depositedAny |= deposited > 0;
            }
            else if (PhysicalItemIds.TryGetEquipmentDefinitionId(
                         item.itemId,
                         out string equipmentId))
            {
                bool isCombatEquipment =
                    combatEquipmentCatalog.TryGet(equipmentId, out _);
                if (isCombatEquipment
                    && remaining == 1
                    && TrySpawnCarriedItem(
                        item,
                        remaining,
                        ResolveWarehouseStoragePosition(warehouse),
                        WorldItemStackState.Stored,
                        GetWarehouseStorageDestinationId(warehouse),
                        false,
                        default,
                        out string storedStackId) == 1)
                {
                    if (repository.EquipmentInstances.TryGetValue(
                            item.itemInstanceId,
                            out CombatEquipmentInstance linked))
                    {
                        repository.TryLinkEquipmentToStack(
                            linked.instanceId,
                            storedStackId,
                            CombatEquipmentWorldState.Stored);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Carried equipment '{item.itemInstanceId}' has no item repository state.");
                    }

                    gameEventBus.Publish(
                        new EquipmentStoredEvent(equipmentId, 1));
                    depositedAny = true;
                    remaining = 0;
                }
                else if (!isCombatEquipment)
                {
                    gameEventBus.Publish(
                        new EquipmentStoredEvent(equipmentId, remaining));
                    depositedAny |= remaining > 0;
                    remaining = 0;
                }
            }

            if (remaining > 0)
            {
                TrySpawnCarriedItem(
                    item,
                    remaining,
                    dropPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    false,
                    default,
                    out _);
            }
        }

        if (!depositedAny)
        {
            failureReason = "warehouse rejected carried items";
        }

        return depositedAny;
    }

    public bool TryDepositCarriedItemsToFacility(
        CharacterActor actor,
        CharacterCarryInventory inventory,
        Vector2Int destinationPosition,
        string destinationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || inventory == null)
        {
            failureReason = "carrier unavailable";
            return false;
        }

        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDestination))
        {
            failureReason = "destination missing";
            return false;
        }

        List<CharacterCarriedItemSaveData> carried = inventory.RemoveAllItems();
        if (carried.Count == 0)
        {
            failureReason = "nothing carried";
            return false;
        }

        bool depositedAny = false;
        foreach (CharacterCarriedItemSaveData item in carried)
        {
            if (item == null
                || item.quantity <= 0
                || string.IsNullOrWhiteSpace(item.itemId))
            {
                continue;
            }

            int spawned;
            if (item.quantity == 1
                && repository.EquipmentInstances.TryGetValue(
                    item.itemInstanceId,
                    out CombatEquipmentInstance linked)
                && TrySpawnCarriedItem(
                    item,
                    item.quantity,
                    destinationPosition,
                    WorldItemStackState.FacilityBuffer,
                    normalizedDestination,
                    true,
                    destinationPosition,
                    out string bufferStackId) == 1)
            {
                repository.TryLinkEquipmentToStack(
                    linked.instanceId,
                    bufferStackId,
                    CombatEquipmentWorldState.MaintenanceBuffer);
                spawned = 1;
            }
            else
            {
                spawned = TrySpawnCarriedItem(
                    item,
                    item.quantity,
                    destinationPosition,
                    WorldItemStackState.FacilityBuffer,
                    normalizedDestination,
                    true,
                    destinationPosition,
                    out _);
            }

            depositedAny |= spawned > 0;
        }

        if (!depositedAny)
        {
            failureReason = "facility rejected carried items";
        }

        return depositedAny;
    }

    public bool TryConsumeFacilityBuffer(
        string destinationId,
        IReadOnlyDictionary<StockCategory, int> costs,
        out string failureReason) =>
        ItemFacilityBufferTransaction.TryConsumeByCategory(
            destinationId,
            costs,
            debugRules,
            repository,
            catalogProvider,
            markerPresenter,
            out failureReason);

    public bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason) =>
        ItemFacilityBufferTransaction.TryConsumeByItem(
            destinationId,
            costs,
            debugRules,
            repository,
            markerPresenter,
            out failureReason);

    private bool TryWithdrawOutboundStoredStock(
        WorldItemStackRecord stack,
        int requested,
        out IWarehouseFacility warehouse,
        out StockCategory category,
        out int withdrawn,
        out string failureReason)
    {
        warehouse = null;
        category = default;
        withdrawn = 0;
        failureReason = string.Empty;
        if (!IsOutboundStoredStack(stack)
            || requested <= 0
            || !TryGetWarehouseStockCategory(stack.itemId, out category))
        {
            failureReason = "stored source unavailable";
            return false;
        }

        string sourceStorageDestinationId =
            stack.sourceStorageDestinationId.Trim();
        warehouse = GetWarehouses().FirstOrDefault(candidate =>
            candidate != null
            && candidate.Inventory != null
            && string.Equals(
                GetWarehouseStorageDestinationId(candidate),
                sourceStorageDestinationId,
                StringComparison.Ordinal));
        if (warehouse == null)
        {
            failureReason = "source warehouse unavailable";
            return false;
        }

        withdrawn = Mathf.Min(requested, stack.quantity);
        if (withdrawn <= 0)
        {
            failureReason = "warehouse stock unavailable";
            return false;
        }

        return true;
    }

    private IEnumerable<IWarehouseFacility> GetWarehouses()
    {
        foreach (IWarehouseFacility warehouse in worldRegistry.Warehouses)
        {
            if (warehouse != null
                && warehouse.HasWarehouseInventory
                && warehouse.Inventory != null)
            {
                yield return warehouse;
            }
        }
    }

    private int AddStoredWarehouseItems(
        IWarehouseFacility warehouse,
        string itemId,
        int amount,
        WasteOriginKind wasteOrigin = WasteOriginKind.Unknown,
        float contamination = 0f,
        IReadOnlyList<ItemInstanceComponentSaveData> components = null)
    {
        return warehouse == null
            || string.IsNullOrWhiteSpace(itemId)
            || amount <= 0
            ? 0
            : itemSpawner.Spawn(
                itemId,
                amount,
                ResolveWarehouseStoragePosition(warehouse),
                WorldItemStackState.Stored,
                GetWarehouseStorageDestinationId(warehouse),
                wasteOrigin: wasteOrigin,
                contamination: contamination,
                components: components);
    }

    private int TrySpawnCarriedItem(
        CharacterCarriedItemSaveData item,
        int quantity,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        bool hasDestinationPosition,
        Vector2Int destinationPosition,
        out string stackId)
    {
        stackId = string.Empty;
        if (item == null || quantity <= 0 || string.IsNullOrWhiteSpace(item.itemId))
        {
            return 0;
        }

        ItemInstanceId instanceId = (ItemInstanceId)item.itemInstanceId;
        if (instanceId.IsValid)
        {
            return quantity == 1
                && itemSpawner.SpawnExistingUnique(
                    item.itemId,
                    instanceId,
                    position,
                    state,
                    destinationId,
                    hasDestinationPosition,
                    destinationPosition,
                    item.components,
                    out stackId)
                    ? 1
                    : 0;
        }

        return itemSpawner.Spawn(
            item.itemId,
            quantity,
            position,
            state,
            destinationId,
            hasDestinationPosition,
            destinationPosition,
            wasteOrigin: item.wasteOrigin,
            contamination: item.contamination,
            components: item.components);
    }

    private static Vector2Int ResolveActorGridPosition(CharacterActor actor)
    {
        return actor != null ? actor.GetNowXY() : Vector2Int.zero;
    }

    private static string GetWarehouseStorageDestinationId(
        IWarehouseFacility warehouse)
    {
        return WarehouseStorageIdentity.RequireDestinationId(warehouse);
    }

    private static Vector2Int ResolveWarehouseStoragePosition(
        IWarehouseFacility warehouse)
    {
        return warehouse is BuildableObject building
            ? building.centerPos
            : Vector2Int.zero;
    }

    private static bool IsOutboundStoredStack(WorldItemStackRecord stack)
    {
        return stack != null
            && stack.state == WorldItemStackState.Stored
            && stack.hasDestinationPosition
            && !string.IsNullOrWhiteSpace(stack.destinationId)
            && !string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId);
    }

    private static bool IsCombatLoadoutDestination(string destinationId)
    {
        return !string.IsNullOrWhiteSpace(destinationId)
            && destinationId.StartsWith(
                WorldItemStackRuntime.CombatLoadoutDestinationPrefix,
                StringComparison.Ordinal);
    }

    private bool TryGetWarehouseStockCategory(
        string itemId,
        out StockCategory category)
    {
        if (catalogProvider.TryGetDefinition(
                itemId,
                out DungeonItemDefinition definition))
        {
            category = definition.StockCategory;
            return true;
        }

        category = default;
        return false;
    }

    private static void RestoreDirectPickupStack(
        WorldItemStackRecord stack)
    {
        if (stack == null
            || !IsCombatLoadoutDestination(stack.destinationId))
        {
            return;
        }

        stack.destinationId =
            stack.sourceStorageDestinationId ?? string.Empty;
        stack.sourceStorageDestinationId = string.Empty;
        stack.hasDestinationPosition = false;
        stack.destinationPosition = default;
    }
}
