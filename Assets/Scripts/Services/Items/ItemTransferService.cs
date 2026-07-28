using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IItemTransferService
{
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

public sealed class ItemTransferService : IItemTransferService
{
    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IItemHaulingSettingsProvider haulingSettingsProvider;
    private readonly ICharacterIdRegistry characterIdRegistry;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ICombatEquipmentRuntime combatEquipmentRuntime;
    private readonly ICombatEquipmentCatalog combatEquipmentCatalog;
    private readonly IResourceEconomyContentCatalog resourceEconomyCatalog;
    private readonly IGameEventBus gameEventBus;
    private readonly WorldItemRepository repository;
    private readonly IWorldItemSpawner itemSpawner;
    private readonly IItemMarkerPresenter markerPresenter;

    public ItemTransferService(
        IGridSystemProvider gridSystemProvider,
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider haulingSettingsProvider,
        ICharacterIdRegistry characterIdRegistry,
        ICharacterAiWorldRegistry worldRegistry,
        ICombatEquipmentRuntime combatEquipmentRuntime,
        ICombatEquipmentCatalog combatEquipmentCatalog,
        IGameEventBus gameEventBus,
        WorldItemRepository repository,
        IWorldItemSpawner itemSpawner,
        IItemMarkerPresenter markerPresenter,
        IResourceEconomyContentCatalog resourceEconomyCatalog = null)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.catalogProvider = catalogProvider
            ?? throw new ArgumentNullException(nameof(catalogProvider));
        this.haulingSettingsProvider = haulingSettingsProvider
            ?? throw new ArgumentNullException(nameof(haulingSettingsProvider));
        this.characterIdRegistry = characterIdRegistry
            ?? throw new ArgumentNullException(nameof(characterIdRegistry));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.combatEquipmentRuntime = combatEquipmentRuntime
            ?? throw new ArgumentNullException(nameof(combatEquipmentRuntime));
        this.combatEquipmentCatalog = combatEquipmentCatalog
            ?? throw new ArgumentNullException(nameof(combatEquipmentCatalog));
        this.resourceEconomyCatalog = resourceEconomyCatalog;
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.itemSpawner = itemSpawner
            ?? throw new ArgumentNullException(nameof(itemSpawner));
        this.markerPresenter = markerPresenter
            ?? throw new ArgumentNullException(nameof(markerPresenter));
    }

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
                    record.itemId,
                    pickedUp,
                    catalogProvider,
                    haulingSettingsProvider,
                    record.wasteOrigin,
                    record.contamination,
                    out int acceptedQuantity,
                    out string carryFailure))
            {
                sourceWarehouse.Inventory.Deposit(sourceCategory, pickedUp);
                pickedUp = 0;
                failureReason = string.IsNullOrWhiteSpace(carryFailure)
                    ? "carry limit"
                    : carryFailure;
                return false;
            }

            if (acceptedQuantity != pickedUp)
            {
                sourceWarehouse.Inventory.Deposit(
                    sourceCategory,
                    pickedUp - acceptedQuantity);
                pickedUp = acceptedQuantity;
            }
        }
        else if (!inventory.TryAddPartialStack(
                     record.stackId,
                     record.itemId,
                     requested,
                     catalogProvider,
                     haulingSettingsProvider,
                     record.wasteOrigin,
                     record.contamination,
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
        combatEquipmentRuntime.TrySetWorldStateBySourceStack(
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
                    out StockCategory category))
            {
                int deposited = warehouse.Inventory.Deposit(
                    category,
                    remaining);
                if (deposited > 0)
                {
                    AddStoredWarehouseItems(
                        warehouse,
                        item.itemId,
                        deposited,
                        item.wasteOrigin,
                        item.contamination);
                }

                remaining -= deposited;
                depositedAny |= deposited > 0;
            }
            else if (DungeonItemCatalogSO.TryGetEquipmentIdFromItemId(
                         item.itemId,
                         out string equipmentId))
            {
                bool isCombatEquipment =
                    combatEquipmentCatalog.TryGet(equipmentId, out _);
                if (isCombatEquipment
                    && remaining == 1
                    && itemSpawner.SpawnUnique(
                        item.itemId,
                        ResolveWarehouseStoragePosition(warehouse),
                        WorldItemStackState.Stored,
                        GetWarehouseStorageDestinationId(warehouse),
                        out string storedStackId))
                {
                    if (combatEquipmentRuntime.TryGetInstanceBySourceStack(
                            item.sourceStackId,
                            out CombatEquipmentInstance linked))
                    {
                        combatEquipmentRuntime.TryLinkToWorldStack(
                            linked.instanceId,
                            storedStackId,
                            CombatEquipmentWorldState.Stored);
                    }
                    else
                    {
                        CombatEquipmentInstance created =
                            combatEquipmentRuntime.CreateInstance(
                                equipmentId,
                                CombatEquipmentQuality.Normal,
                                CombatEquipmentWorldState.Stored);
                        combatEquipmentRuntime.TryLinkToWorldStack(
                            created.instanceId,
                            storedStackId,
                            CombatEquipmentWorldState.Stored);
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
                itemSpawner.Spawn(
                    item.itemId,
                    remaining,
                    dropPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    wasteOrigin: item.wasteOrigin,
                    contamination: item.contamination);
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
                && combatEquipmentRuntime.TryGetInstanceBySourceStack(
                    item.sourceStackId,
                    out CombatEquipmentInstance linked)
                && itemSpawner.SpawnUnique(
                    item.itemId,
                    destinationPosition,
                    WorldItemStackState.FacilityBuffer,
                    normalizedDestination,
                    destinationPosition,
                    out string bufferStackId))
            {
                combatEquipmentRuntime.TryLinkToWorldStack(
                    linked.instanceId,
                    bufferStackId,
                    CombatEquipmentWorldState.MaintenanceBuffer);
                spawned = 1;
            }
            else
            {
                spawned = itemSpawner.Spawn(
                    item.itemId,
                    item.quantity,
                    destinationPosition,
                    WorldItemStackState.FacilityBuffer,
                    normalizedDestination,
                    hasDestinationPosition: true,
                    destinationPosition: destinationPosition,
                    wasteOrigin: item.wasteOrigin,
                    contamination: item.contamination);
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
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDestination))
        {
            failureReason = "destination missing";
            return false;
        }

        Dictionary<StockCategory, int> required = costs?
            .Where(pair => pair.Value > 0)
            .GroupBy(pair => pair.Key)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => Mathf.Max(0, pair.Value)))
            ?? new Dictionary<StockCategory, int>();
        if (required.Count == 0 || DungeonDebugRuntimeRules.ShouldSkipCosts())
        {
            return true;
        }

        Dictionary<StockCategory, int> available =
            new Dictionary<StockCategory, int>();
        foreach (WorldItemStackRecord stack in repository.Records)
        {
            if (!MatchesFacilityBuffer(
                    stack,
                    normalizedDestination,
                    out StockCategory category))
            {
                continue;
            }

            available.TryGetValue(category, out int current);
            available[category] = current + stack.quantity;
        }

        foreach (KeyValuePair<StockCategory, int> pair in required)
        {
            if (!available.TryGetValue(pair.Key, out int stock)
                || stock < pair.Value)
            {
                failureReason = "facility materials missing";
                return false;
            }
        }

        foreach (KeyValuePair<StockCategory, int> pair in required)
        {
            int remaining = pair.Value;
            foreach (WorldItemStackRecord stack in repository.Records.ToArray())
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (!MatchesFacilityBuffer(
                        stack,
                        normalizedDestination,
                        out StockCategory category)
                    || category != pair.Key)
                {
                    continue;
                }

                int consumed = Mathf.Min(remaining, stack.quantity);
                Vector2Int position = stack.position;
                stack.quantity -= consumed;
                remaining -= consumed;
                repository.MarkChanged();
                if (stack.quantity <= 0)
                {
                    repository.Remove(stack);
                }

                markerPresenter.RefreshAt(position);
            }
        }

        return true;
    }

    public bool TryConsumeFacilityItemBuffer(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDestination))
        {
            failureReason = "destination missing";
            return false;
        }

        Dictionary<string, int> required = costs?
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            .GroupBy(pair => pair.Key.Trim(), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => Mathf.Max(0, pair.Value)),
                StringComparer.Ordinal)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        if (required.Count == 0 || DungeonDebugRuntimeRules.ShouldSkipCosts())
        {
            return true;
        }

        Dictionary<string, int> available =
            new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (WorldItemStackRecord stack in repository.Records)
        {
            if (!MatchesFacilityItemBuffer(stack, normalizedDestination))
            {
                continue;
            }

            available.TryGetValue(stack.itemId, out int current);
            available[stack.itemId] = current + stack.quantity;
        }

        foreach (KeyValuePair<string, int> pair in required)
        {
            if (!available.TryGetValue(pair.Key, out int stock)
                || stock < pair.Value)
            {
                failureReason = $"facility item missing: {pair.Key}";
                return false;
            }
        }

        foreach (KeyValuePair<string, int> pair in required)
        {
            int remaining = pair.Value;
            foreach (WorldItemStackRecord stack in repository.Records.ToArray())
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (!MatchesFacilityItemBuffer(stack, normalizedDestination)
                    || !string.Equals(stack.itemId, pair.Key, StringComparison.Ordinal))
                {
                    continue;
                }

                int consumed = Mathf.Min(remaining, stack.quantity);
                Vector2Int position = stack.position;
                stack.quantity -= consumed;
                remaining -= consumed;
                repository.MarkChanged();
                if (stack.quantity <= 0)
                {
                    repository.Remove(stack);
                }

                markerPresenter.RefreshAt(position);
            }
        }

        return true;
    }

    private bool MatchesFacilityBuffer(
        WorldItemStackRecord stack,
        string destinationId,
        out StockCategory category)
    {
        category = default;
        return stack != null
            && stack.quantity > 0
            && stack.state == WorldItemStackState.FacilityBuffer
            && string.Equals(
                stack.destinationId ?? string.Empty,
                destinationId,
                StringComparison.Ordinal)
            && TryGetWarehouseStockCategory(stack.itemId, out category);
    }

    private static bool MatchesFacilityItemBuffer(
        WorldItemStackRecord stack,
        string destinationId)
    {
        return stack != null
            && stack.quantity > 0
            && stack.state == WorldItemStackState.FacilityBuffer
            && string.Equals(
                stack.destinationId ?? string.Empty,
                destinationId,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(stack.itemId);
    }

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

        withdrawn = warehouse.Inventory.Withdraw(
            category,
            Mathf.Min(requested, stack.quantity));
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
        float contamination = 0f)
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
                contamination: contamination);
    }

    private Vector2Int ResolveActorGridPosition(CharacterActor actor)
    {
        return actor != null
            && gridSystemProvider.TryGetGrid(out Grid grid)
            ? grid.GetXY(actor.transform.position)
            : Vector2Int.zero;
    }

    private static string GetWarehouseStorageDestinationId(
        IWarehouseFacility warehouse)
    {
        if (warehouse is BuildableObject building)
        {
            return string.Concat(
                WorldItemStackRuntime.WarehouseStorageDestinationPrefix,
                building.GridId.ToString(CultureInfo.InvariantCulture),
                ":",
                building.centerPos.x.ToString(CultureInfo.InvariantCulture),
                ":",
                building.centerPos.y.ToString(CultureInfo.InvariantCulture));
        }

        return WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + warehouse.GetHashCode().ToString(CultureInfo.InvariantCulture);
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
        if (DungeonItemCatalogSO.TryGetStockCategoryFromItemId(
                itemId,
                out category))
        {
            return true;
        }

        if (CombatItemDefinitions.TryGetDefinition(
                itemId,
                out DungeonItemDefinition ammunition))
        {
            category = ammunition.StockCategory;
            return true;
        }

        if (resourceEconomyCatalog != null
            && resourceEconomyCatalog.TryGetItem(
                itemId,
                out ResourceItemDefinitionSO resource))
        {
            category = resource.StockCategory;
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
