using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Routes repository-owned physical stacks into and out of warehouses. Warehouse
/// inventories provide capacity/filter policy only and never author quantities.
/// </summary>
public sealed class WorldItemWarehouseService
{
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly WorldItemRepository repository;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemSpawner spawner;
    private readonly IItemMarkerPresenter markers;
    private readonly IGridSystemProvider gridProvider;
    private readonly ICharacterIdRegistry characterIds;
    private readonly IItemReservationService reservations;

    public WorldItemWarehouseService(
        IDungeonItemCatalogProvider catalog,
        WorldItemRepository repository,
        ICharacterAiWorldRegistry worldRegistry,
        IWorldItemSpawner spawner,
        IItemMarkerPresenter markers,
        IGridSystemProvider gridProvider,
        ICharacterIdRegistry characterIds,
        IItemReservationService reservations)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        this.characterIds = characterIds
            ?? throw new ArgumentNullException(nameof(characterIds));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
    }

    public bool SpawnStock(
        IWarehouseFacility warehouse,
        StockCategory category,
        int amount,
        out int spawned)
    {
        spawned = 0;
        if (warehouse?.Inventory == null
            || !warehouse.HasWarehouseInventory
            || amount <= 0
            || !warehouse.Inventory.Accepts(category))
        {
            return false;
        }
        DungeonItemDefinition definition = catalog.All
            .Where(candidate => candidate != null
                && candidate.StockCategory == category)
            .OrderBy(candidate => candidate.ItemId, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No authored concrete item belongs to stock category '{category}'.");
        int accepted = Mathf.Min(amount, warehouse.Inventory.RemainingCapacity);
        if (accepted <= 0)
        {
            return false;
        }
        spawned = AddStoredItems(warehouse, definition.ItemId, accepted);
        return spawned == amount;
    }

    public bool TryRequestDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        requested = 0;
        failureReason = string.Empty;
        int remaining = Mathf.Max(0, amount);
        string destination = destinationId?.Trim() ?? string.Empty;
        if (remaining <= 0)
        {
            return true;
        }
        if (destination.Length == 0)
        {
            failureReason = "items.delivery.destination_missing";
            return false;
        }

        DungeonItemDefinition definition = catalog.GetDefinition(itemId);
        IWarehouseFacility[] warehouses = GetWarehouses().ToArray();
        int available = CountLooseAvailable(definition.ItemId)
            + warehouses.Sum(warehouse =>
                CountUnassignedStored(warehouse, definition.ItemId));
        if (available < remaining)
        {
            failureReason = "items.delivery.stock_unavailable";
            return false;
        }

        int loose = RequestLoose(
            definition.ItemId,
            remaining,
            destinationPosition,
            destination);
        requested += loose;
        remaining -= loose;
        if (remaining > 0)
        {
            int stored = RequestStored(
                warehouses,
                definition.ItemId,
                remaining,
                destinationPosition,
                destination);
            requested += stored;
            remaining -= stored;
        }
        if (requested <= 0)
        {
            failureReason = "items.delivery.stock_unavailable";
            return false;
        }
        if (requested < amount)
        {
            failureReason = "items.delivery.partial_request";
            return false;
        }
        return true;
    }

    public bool TryRequestCategoryDelivery(
        StockCategory category,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        requested = 0;
        failureReason = string.Empty;
        int required = Mathf.Max(0, amount);
        string destination = destinationId?.Trim() ?? string.Empty;
        if (required == 0)
        {
            return true;
        }
        if (destination.Length == 0)
        {
            failureReason = "items.delivery.destination_missing";
            return false;
        }

        IWarehouseFacility[] warehouses = GetWarehouses().ToArray();
        var candidates = repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && !record.forbidden
                && string.IsNullOrWhiteSpace(record.reservedByPersistentId)
                && string.IsNullOrWhiteSpace(record.sourceStorageDestinationId)
                && (record.state == WorldItemStackState.Loose
                    && string.IsNullOrWhiteSpace(record.destinationId)
                    || record.state == WorldItemStackState.Stored
                    && string.IsNullOrWhiteSpace(record.destinationId)
                    || record.state == WorldItemStackState.Stored
                    && record.destinationId.StartsWith(
                        WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                        StringComparison.Ordinal)))
            .GroupBy(record => record.itemId, StringComparer.Ordinal)
            .Select(group => new
            {
                ItemId = group.Key,
                Category = catalog.GetDefinition(group.Key).StockCategory,
                Available = CountLooseAvailable(group.Key)
                    + warehouses.Sum(warehouse =>
                        CountUnassignedStored(warehouse, group.Key))
            })
            .Where(candidate => candidate.Category == category
                && candidate.Available > 0)
            .OrderByDescending(candidate => candidate.Available)
            .ThenBy(candidate => candidate.ItemId, StringComparer.Ordinal)
            .ToArray();

        if (candidates.Sum(candidate => candidate.Available) < required)
        {
            failureReason = "items.delivery.stock_unavailable";
            return false;
        }

        int remaining = required;
        foreach (var candidate in candidates)
        {
            int take = Mathf.Min(remaining, candidate.Available);
            if (!TryRequestDelivery(
                    candidate.ItemId,
                    take,
                    destinationPosition,
                    destination,
                    out int concreteRequested,
                    out failureReason))
            {
                return false;
            }

            requested += concreteRequested;
            remaining -= concreteRequested;
            if (remaining <= 0)
            {
                return true;
            }
        }

        failureReason = "items.delivery.stock_unavailable";
        return false;
    }

    public bool TryReserveStoredForDirectPickup(
        CharacterActor actor,
        string itemId,
        int quantity,
        out WorldItemReservedStackQuantity reservation,
        out Vector2Int pickupStandPosition,
        out string failureReason)
    {
        reservation = default;
        pickupStandPosition = default;
        failureReason = string.Empty;
        if (actor == null
            || string.IsNullOrWhiteSpace(itemId)
            || quantity <= 0
            || !gridProvider.TryGetGrid(out Grid grid))
        {
            failureReason = "items.pickup.invalid_request";
            return false;
        }

        string actorId = characterIds.GetOrAssignPersistentId(actor);
        WorldItemStackRecord selected = repository.Records
            .Where(record => record != null
                && record.quantity > 0
                && record.state == WorldItemStackState.Stored
                && !record.forbidden
                && string.IsNullOrWhiteSpace(record.reservedByPersistentId)
                && string.Equals(record.itemId, itemId, StringComparison.Ordinal))
            .Select(record => new
            {
                Record = record,
                HasStand = TryResolvePickupStandCell(
                    grid,
                    record.position,
                    out Vector2Int stand),
                Stand = stand
            })
            .Where(candidate => candidate.HasStand)
            .OrderBy(candidate => Manhattan(actor.GetNowXY(), candidate.Stand))
            .Select(candidate => candidate.Record)
            .FirstOrDefault();
        if (selected == null
            || !TryResolvePickupStandCell(
                grid,
                selected.position,
                out pickupStandPosition))
        {
            failureReason = "items.pickup.stored_item_unavailable";
            return false;
        }
        if (!reservations.TryReserve(new[] { selected.stackId }, actorId))
        {
            failureReason = "items.pickup.reservation_changed";
            return false;
        }

        if (string.IsNullOrWhiteSpace(selected.sourceStorageDestinationId))
        {
            selected.sourceStorageDestinationId = selected.destinationId;
            selected.destinationId =
                WorldItemStackRuntime.CombatLoadoutDestinationPrefix + actorId;
            selected.hasDestinationPosition = true;
            selected.destinationPosition = actor.GetNowXY();
        }
        reservation = new WorldItemReservedStackQuantity(
            selected.stackId,
            selected.itemId,
            Mathf.Min(selected.quantity, Mathf.Max(1, quantity)),
            selected.position,
            WorldItemHaulDestinationKind.Warehouse,
            selected.destinationId);
        repository.MarkChanged();
        markers.RefreshAt(selected.position);
        return true;
    }

    public bool TryRequestStackDelivery(
        string stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason)
    {
        requested = 0;
        failureReason = string.Empty;
        string id = stackId?.Trim() ?? string.Empty;
        string destination = destinationId?.Trim() ?? string.Empty;
        if (id.Length == 0
            || destination.Length == 0
            || amount <= 0
            || !repository.RecordsById.TryGetValue(
                id,
                out WorldItemStackRecord source)
            || source == null
            || source.quantity <= 0
            || source.forbidden
            || !string.IsNullOrWhiteSpace(source.reservedByPersistentId)
            || !string.IsNullOrWhiteSpace(source.sourceStorageDestinationId)
            || !string.IsNullOrWhiteSpace(source.destinationId)
                && source.state != WorldItemStackState.Stored)
        {
            failureReason = "items.delivery.stack_unavailable";
            return false;
        }
        if (source.state is not (WorldItemStackState.Loose
                or WorldItemStackState.Stored))
        {
            failureReason = "items.delivery.stack_state_invalid";
            return false;
        }

        int moved = Mathf.Min(amount, source.quantity);
        Vector2Int sourcePosition = source.position;
        string storageDestination = source.state == WorldItemStackState.Stored
            ? source.destinationId
            : string.Empty;
        source.quantity -= moved;
        repository.MarkChanged();
        if (source.quantity <= 0)
        {
            repository.Remove(source);
        }
        requested = spawner.Spawn(
            source.itemId,
            moved,
            sourcePosition,
            source.state,
            destination,
            true,
            destinationPosition,
            sourceStorageDestinationId: storageDestination,
            wasteOrigin: source.wasteOrigin,
            contamination: source.contamination);
        if (requested < moved)
        {
            spawner.Spawn(
                source.itemId,
                moved - requested,
                sourcePosition,
                source.state,
                storageDestination,
                wasteOrigin: source.wasteOrigin,
                contamination: source.contamination);
        }
        markers.RefreshAt(sourcePosition);
        if (requested <= 0)
        {
            failureReason = "items.delivery.stack_request_failed";
            return false;
        }
        return requested == amount;
    }

    public void NormalizeStorageIds()
    {
        IWarehouseFacility[] warehouses = GetWarehouses().ToArray();
        foreach (WorldItemStackRecord stack in repository.Records)
        {
            if (stack == null || stack.state != WorldItemStackState.Stored)
            {
                continue;
            }
            stack.destinationId = NormalizeStorageId(
                stack.destinationId,
                stack.position,
                warehouses);
            stack.sourceStorageDestinationId = NormalizeStorageId(
                stack.sourceStorageDestinationId,
                stack.position,
                warehouses);
        }
    }

    public void PrioritizeDestination(string destinationId)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            return;
        }
        foreach (WorldItemStackRecord record in repository.Records
                     .Where(record => record != null
                         && string.Equals(
                             record.destinationId,
                             destination,
                             StringComparison.Ordinal)))
        {
            reservations.PrioritizeHaul(record.stackId);
        }
    }

    private int CountLooseAvailable(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId)
            ? 0
            : repository.Records
                .Where(stack => stack != null
                    && stack.quantity > 0
                    && stack.state == WorldItemStackState.Loose
                    && !stack.forbidden
                    && string.IsNullOrWhiteSpace(stack.reservedByPersistentId)
                    && string.IsNullOrWhiteSpace(stack.destinationId)
                    && string.Equals(
                        stack.itemId,
                        itemId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.quantity);
    }

    private int RequestLoose(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId)
    {
        int remaining = amount;
        int requested = 0;
        foreach (WorldItemStackRecord stack in repository.Records
                     .Where(stack => stack != null
                         && stack.quantity > 0
                         && stack.state == WorldItemStackState.Loose
                         && !stack.forbidden
                         && string.IsNullOrWhiteSpace(
                             stack.reservedByPersistentId)
                         && string.IsNullOrWhiteSpace(stack.destinationId)
                         && string.Equals(
                             stack.itemId,
                             itemId,
                             StringComparison.Ordinal))
                     .OrderBy(stack => Manhattan(
                         stack.position,
                         destinationPosition))
                     .ToArray())
        {
            if (remaining <= 0)
            {
                break;
            }
            int moved = Mathf.Min(remaining, stack.quantity);
            Vector2Int sourcePosition = stack.position;
            stack.quantity -= moved;
            repository.MarkChanged();
            if (stack.quantity <= 0)
            {
                repository.Remove(stack);
            }
            requested += spawner.Spawn(
                itemId,
                moved,
                sourcePosition,
                WorldItemStackState.Loose,
                destinationId,
                true,
                destinationPosition,
                wasteOrigin: stack.wasteOrigin,
                contamination: stack.contamination);
            remaining -= moved;
            markers.RefreshAt(sourcePosition);
        }
        return requested;
    }

    private int RequestStored(
        IEnumerable<IWarehouseFacility> warehouses,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId)
    {
        int remaining = amount;
        int requested = 0;
        foreach (IWarehouseFacility warehouse in (warehouses
                     ?? Enumerable.Empty<IWarehouseFacility>())
                     .Where(candidate => candidate?.Inventory != null)
                     .OrderBy(candidate => candidate is BuildableObject building
                         ? Manhattan(building.centerPos, destinationPosition)
                         : int.MaxValue))
        {
            if (remaining <= 0)
            {
                break;
            }
            string storageId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
            foreach (WorldItemStackRecord stack in repository.Records
                         .Where(stack => stack != null
                             && stack.quantity > 0
                             && stack.state == WorldItemStackState.Stored
                             && !stack.forbidden
                             && string.IsNullOrWhiteSpace(
                                 stack.reservedByPersistentId)
                             && string.IsNullOrWhiteSpace(
                                 stack.sourceStorageDestinationId)
                             && string.Equals(
                                 stack.itemId,
                                 itemId,
                                 StringComparison.Ordinal)
                             && string.Equals(
                                 stack.destinationId ?? string.Empty,
                                 storageId,
                                 StringComparison.Ordinal))
                         .ToArray())
            {
                if (remaining <= 0)
                {
                    break;
                }
                int assigned = Mathf.Min(remaining, stack.quantity);
                Vector2Int sourcePosition = stack.position;
                stack.quantity -= assigned;
                repository.MarkChanged();
                if (stack.quantity <= 0)
                {
                    repository.Remove(stack);
                }
                int created = spawner.Spawn(
                    itemId,
                    assigned,
                    sourcePosition,
                    WorldItemStackState.Stored,
                    destinationId,
                    true,
                    destinationPosition,
                    sourceStorageDestinationId: storageId,
                    wasteOrigin: stack.wasteOrigin,
                    contamination: stack.contamination);
                if (created < assigned)
                {
                    AddStoredItems(
                        warehouse,
                        itemId,
                        assigned - created,
                        stack.wasteOrigin,
                        stack.contamination);
                }
                requested += created;
                remaining -= created;
                markers.RefreshAt(sourcePosition);
            }
        }
        return requested;
    }

    private int CountUnassignedStored(
        IWarehouseFacility warehouse,
        string itemId)
    {
        if (warehouse == null || string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }
        string storageId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        return repository.Records
            .Where(stack => stack != null
                && stack.quantity > 0
                && stack.state == WorldItemStackState.Stored
                && !stack.forbidden
                && string.IsNullOrWhiteSpace(stack.reservedByPersistentId)
                && string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                && string.Equals(stack.itemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    stack.destinationId ?? string.Empty,
                    storageId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.quantity);
    }

    private int AddStoredItems(
        IWarehouseFacility warehouse,
        string itemId,
        int amount,
        WasteOriginKind wasteOrigin = WasteOriginKind.Unknown,
        float contamination = 0f)
    {
        if (warehouse == null
            || string.IsNullOrWhiteSpace(itemId)
            || amount <= 0)
        {
            return 0;
        }
        Vector2Int position = warehouse is BuildableObject building
            ? building.centerPos
            : Vector2Int.zero;
        return spawner.Spawn(
            itemId,
            amount,
            position,
            WorldItemStackState.Stored,
            WarehouseStorageIdentity.RequireDestinationId(warehouse),
            wasteOrigin: wasteOrigin,
            contamination: contamination);
    }

    private IEnumerable<IWarehouseFacility> GetWarehouses()
    {
        return worldRegistry.Warehouses.Where(warehouse => warehouse != null
            && warehouse.HasWarehouseInventory
            && warehouse.Inventory != null);
    }

    private static string NormalizeStorageId(
        string storageDestinationId,
        Vector2Int storagePosition,
        IReadOnlyList<IWarehouseFacility> warehouses)
    {
        string normalized = storageDestinationId?.Trim() ?? string.Empty;
        if (!normalized.StartsWith(
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                StringComparison.Ordinal))
        {
            return normalized;
        }
        string suffix = normalized.Substring(
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix.Length);
        if (suffix.StartsWith("building:", StringComparison.Ordinal))
        {
            return normalized;
        }
        throw new InvalidOperationException(
            $"Legacy warehouse storage key '{normalized}' cannot be restored in V18.");
    }

    private static bool TryResolvePickupStandCell(
        Grid grid,
        Vector2Int storagePosition,
        out Vector2Int stand)
    {
        if (grid.IsValidGridPos(storagePosition)
            && grid.IsWalkable(storagePosition))
        {
            stand = storagePosition;
            return true;
        }
        return grid.TryFindNearbyWalkablePositionOnSameFloor(
            storagePosition,
            out stand,
            maxDistance: 1);
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
