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
    IWorldItemMarkerDataSource,
    IHaulPlanBuilder,
    IStartable,
    IDisposable
{
    public const string FacilityInputDestinationPrefix = "facility-input:";
    public const string WarehouseStorageDestinationPrefix = "warehouse:";
    public const string CombatLoadoutDestinationPrefix = "combat-loadout-pickup:";

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IDungeonItemCatalogProvider catalogProvider;
    private readonly IItemHaulingSettingsProvider haulingSettingsProvider;
    private readonly ICharacterIdRegistry characterIdRegistry;
    private readonly IWorldDropZoneQuery worldDropZoneQuery;
    private readonly ICharacterSpawnerProvider characterSpawnerProvider;
    private readonly IGridPathSearchBroker pathSearchBroker;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ICombatEquipmentRuntime combatEquipmentRuntime;
    private readonly ICombatEquipmentCatalog combatEquipmentCatalog;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private readonly IItemMarkerPresenter itemMarkerPresenter;
    private readonly WorldItemRepository itemRepository;
    private readonly IItemReservationService reservationService;
    private readonly IWorldItemSpawner itemSpawner;
    private readonly WorldItemQueryService itemQueryService;
    private readonly IWorldItemHaulPlanningService haulPlanningService;
    private readonly IItemTransferService itemTransferService;
    private readonly ICharacterAiPerformanceRecorder performanceRecorder;

    private List<WorldItemStackRecord> stacks => itemRepository.Records;
    private Dictionary<string, WorldItemStackRecord> stacksById => itemRepository.RecordsById;
    private Dictionary<Vector2Int, List<WorldItemStackRecord>> stacksByPosition =>
        itemRepository.RecordsByPosition;
    private int nextStackSequence
    {
        get => itemRepository.NextStackSequence;
        set => itemRepository.NextStackSequence = value;
    }
    public WorldItemStackRuntime(
        IGridSystemProvider gridSystemProvider,
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider haulingSettingsProvider,
        ICharacterIdRegistry characterIdRegistry,
        IWorldDropZoneQuery worldDropZoneQuery,
        ICharacterSpawnerProvider characterSpawnerProvider,
        IGridPathSearchBroker pathSearchBroker,
        ICharacterAiWorldRegistry worldRegistry,
        ICombatEquipmentRuntime combatEquipmentRuntime,
        ICombatEquipmentCatalog combatEquipmentCatalog,
        IGameEventBus gameEventBus,
        IGameClock gameClock,
        WorldItemRepository itemRepository,
        IItemReservationService reservationService,
        IWorldItemSpawner itemSpawner,
        WorldItemQueryService itemQueryService,
        IWorldItemHaulPlanningService haulPlanningService,
        IItemMarkerPresenter itemMarkerPresenter = null,
        IItemTransferService itemTransferService = null,
        ICharacterAiPerformanceRecorder performanceRecorder = null)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        this.haulingSettingsProvider = haulingSettingsProvider
            ?? throw new ArgumentNullException(nameof(haulingSettingsProvider));
        this.characterIdRegistry = characterIdRegistry ?? throw new ArgumentNullException(nameof(characterIdRegistry));
        this.worldDropZoneQuery = worldDropZoneQuery
            ?? throw new ArgumentNullException(nameof(worldDropZoneQuery));
        this.characterSpawnerProvider = characterSpawnerProvider
            ?? throw new ArgumentNullException(nameof(characterSpawnerProvider));
        this.pathSearchBroker = pathSearchBroker
            ?? throw new ArgumentNullException(nameof(pathSearchBroker));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.combatEquipmentRuntime = combatEquipmentRuntime
            ?? throw new ArgumentNullException(nameof(combatEquipmentRuntime));
        this.combatEquipmentCatalog = combatEquipmentCatalog
            ?? throw new ArgumentNullException(nameof(combatEquipmentCatalog));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.itemRepository = itemRepository
            ?? throw new ArgumentNullException(nameof(itemRepository));
        this.reservationService = reservationService
            ?? throw new ArgumentNullException(nameof(reservationService));
        this.itemSpawner = itemSpawner
            ?? throw new ArgumentNullException(nameof(itemSpawner));
        this.itemQueryService = itemQueryService
            ?? throw new ArgumentNullException(nameof(itemQueryService));
        this.haulPlanningService = haulPlanningService
            ?? throw new ArgumentNullException(nameof(haulPlanningService));
        this.itemMarkerPresenter = itemMarkerPresenter ?? NullItemMarkerPresenter.Instance;
        this.performanceRecorder = performanceRecorder;
        this.itemTransferService = itemTransferService
            ?? new ItemTransferService(
                this.gridSystemProvider,
                this.catalogProvider,
                this.haulingSettingsProvider,
                this.characterIdRegistry,
                this.worldRegistry,
                this.combatEquipmentRuntime,
                this.combatEquipmentCatalog,
                this.gameEventBus,
                this.itemRepository,
                this.itemSpawner,
                this.itemMarkerPresenter);
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
        RefreshAllMarkers();
    }

    public void Dispose()
    {
    }

    public DungeonPhysicalItemSaveData Capture()
    {
        return new DungeonPhysicalItemSaveData
        {
            version = DungeonPhysicalItemSaveData.CurrentVersion,
            nextStackSequence = nextStackSequence,
            haulingSettings = haulingSettingsProvider.Capture(),
            stacks = stacks
                .Where(stack => stack != null && stack.quantity > 0)
                .OrderBy(stack => stack.position.y)
                .ThenBy(stack => stack.position.x)
                .ThenBy(stack => stack.itemId, StringComparer.Ordinal)
                .Select(ToSaveData)
                .ToList()
        };
    }

    public void SetStoredItemMarkersVisible(bool visible)
    {
        itemQueryService.SetStoredItemMarkersVisible(visible);
    }

    public void Restore(DungeonPhysicalItemSaveData snapshot)
    {
        ClearRuntimeStacks();
        if (snapshot == null)
        {
            return;
        }

        if (snapshot.version != DungeonPhysicalItemSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported physical item save version {snapshot.version}.");
        }

        haulingSettingsProvider.Restore(snapshot.haulingSettings);
        nextStackSequence = Mathf.Max(1, snapshot.nextStackSequence);
        foreach (WorldItemStackSaveData entry in snapshot.stacks ?? new List<WorldItemStackSaveData>())
        {
            if (entry == null || entry.quantity <= 0 || string.IsNullOrWhiteSpace(entry.itemId))
            {
                continue;
            }

            WorldItemStackRecord record = new WorldItemStackRecord
            {
                stackId = string.IsNullOrWhiteSpace(entry.stackId) ? AllocateStackId() : entry.stackId.Trim(),
                itemId = entry.itemId.Trim(),
                quantity = Mathf.Max(0, entry.quantity),
                state = Enum.IsDefined(typeof(WorldItemStackState), entry.state)
                    ? entry.state
                    : WorldItemStackState.Loose,
                position = new Vector2Int(entry.gridX, entry.gridY),
                // Haul plans are runtime-only. Persisting their owner without
                // restoring the plan would leave an unreachable reserved stack.
                reservedByPersistentId = string.Empty,
                destinationId = entry.destinationId?.Trim() ?? string.Empty,
                sourceStorageDestinationId = entry.sourceStorageDestinationId?.Trim() ?? string.Empty,
                hasDestinationPosition = entry.hasDestinationPosition,
                destinationPosition = new Vector2Int(entry.destinationGridX, entry.destinationGridY),
                forbidden = entry.forbidden
                ,sourceCharacterId = entry.sourceCharacterId?.Trim() ?? string.Empty
                ,sourceDisplayName = entry.sourceDisplayName?.Trim() ?? string.Empty
                ,sourceSpeciesTag = entry.sourceSpeciesTag?.Trim() ?? string.Empty
                ,sourceDeathReason = entry.sourceDeathReason?.Trim() ?? string.Empty
                ,emergencyButcheryAllowed = entry.emergencyButcheryAllowed
            };
            if (!string.IsNullOrWhiteSpace(entry.reservedByPersistentId)
                && IsCombatLoadoutDestination(record.destinationId))
            {
                RestoreDirectPickupStack(record);
            }
            AddRecord(record);
        }

        NormalizeLegacyWarehouseStorageIds();
        SyncWarehouseInventoriesFromStoredStacks();
        RefreshAllMarkers();
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

        DungeonItemDefinition definition = catalogProvider.GetDefinition(category);
        spawned = Spawn(definition.ItemId, amount, dropoff, state, destinationId ?? string.Empty);
        return spawned == amount;
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

        string persistentId = source.Identity?.PersistentId;
        if (string.IsNullOrWhiteSpace(persistentId))
        {
            persistentId = $"character:{source.GetInstanceID()}";
        }

        int before = nextStackSequence;
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
            && record.position == position)?.stackId ?? $"stack:{before:D8}";
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
        DungeonItemDefinition definition = catalogProvider.GetDefinition(category);
        return TryRequestItemDelivery(
            definition.ItemId,
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
        requested = 0;
        failureReason = string.Empty;
        int remaining = Mathf.Max(0, amount);
        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (remaining <= 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(normalizedDestination))
        {
            failureReason = "destination missing";
            return false;
        }

        DungeonItemDefinition definition = catalogProvider.GetDefinition(itemId);
        IWarehouseFacility[] warehouses = GetWarehouses().ToArray();
        foreach (IWarehouseFacility warehouse in warehouses)
        {
            EnsureStoredWarehouseMirror(
                warehouse,
                definition.ItemId,
                definition.StockCategory);
        }

        int looseAvailable = CountLooseStockAvailable(definition.ItemId);
        int warehouseAvailable = warehouses.Sum(candidate =>
            CountUnassignedStoredStock(candidate, definition.ItemId));
        if (looseAvailable + warehouseAvailable < remaining)
        {
            failureReason = "stock unavailable";
            return false;
        }

        int looseRequested = RequestLooseStockDelivery(
            definition.ItemId,
            remaining,
            destinationPosition,
            normalizedDestination);
        requested += looseRequested;
        remaining -= looseRequested;
        if (remaining <= 0)
        {
            return true;
        }

        int storedRequested = RequestStoredStockDelivery(
            warehouses,
            definition.ItemId,
            remaining,
            destinationPosition,
            normalizedDestination);
        requested += storedRequested;
        remaining -= storedRequested;

        if (requested <= 0)
        {
            failureReason = "stock unavailable";
            return false;
        }

        if (requested < amount)
        {
            failureReason = "partial stock delivery requested";
            return false;
        }

        return true;
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
        reservation = default;
        pickupStandPosition = default;
        failureReason = string.Empty;
        if (actor == null
            || string.IsNullOrWhiteSpace(itemId)
            || quantity <= 0
            || !TryGetGrid(out Grid grid))
        {
            failureReason = "직접 수령 대상이 올바르지 않습니다.";
            return false;
        }

        string actorId = characterIdRegistry.GetOrAssignPersistentId(actor);
        WorldItemStackRecord selected = stacks
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
            .OrderBy(candidate => GetManhattanDistance(
                actor.GetNowXY(),
                candidate.Stand))
            .Select(candidate => candidate.Record)
            .FirstOrDefault();
        if (selected == null
            || !TryResolvePickupStandCell(
                grid,
                selected.position,
                out pickupStandPosition))
        {
            failureReason = "창고에 준비된 장비가 없습니다.";
            return false;
        }

        if (!reservationService.TryReserve(new[] { selected.stackId }, actorId))
        {
            failureReason = "장비 예약 상태가 변경되었습니다.";
            return false;
        }

        if (TryGetWarehouseStockCategory(selected.itemId, out _)
            && string.IsNullOrWhiteSpace(selected.sourceStorageDestinationId))
        {
            selected.sourceStorageDestinationId = selected.destinationId;
            selected.destinationId = CombatLoadoutDestinationPrefix + actorId;
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
        MarkStacksChanged();
        RefreshMarkerAt(selected.position);
        return true;
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

        string actorId = characterIdRegistry.GetOrAssignPersistentId(actor);
        if (!stacksById.TryGetValue(job.StackId, out WorldItemStackRecord record)
            || record.quantity <= 0)
        {
            failureReason = "stack disappeared";
            return false;
        }

        if (!string.Equals(record.reservedByPersistentId, actorId, StringComparison.Ordinal))
        {
            failureReason = "stack reserved by someone else";
            return false;
        }

        WorldItemReservedStackQuantity reservation = new WorldItemReservedStackQuantity(
            record.stackId,
            record.itemId,
            record.quantity,
            record.position,
            job.DestinationKind,
            job.DestinationId);
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

    public bool TryStealLooseItem(
        CharacterActor actor,
        int searchRadius,
        out WorldItemStackSnapshot stolenItem,
        out string failureReason)
    {
        stolenItem = null;
        failureReason = string.Empty;
        if (actor == null || actor.characterType != CharacterType.Customer)
        {
            failureReason = "not a customer";
            return false;
        }

        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        if (inventory == null)
        {
            failureReason = "no carry inventory";
            return false;
        }

        Vector2Int origin = ResolveActorGridPosition(actor);
        int radius = Mathf.Max(0, searchRadius);
        WorldItemStackRecord bestStack = null;
        DungeonItemDefinition bestDefinition = null;
        float bestScore = float.MinValue;
        foreach (WorldItemStackRecord stack in stacks)
        {
            if (stack == null
                || stack.quantity <= 0
                || stack.state != WorldItemStackState.Loose
                || stack.forbidden
                || !string.IsNullOrWhiteSpace(stack.reservedByPersistentId))
            {
                continue;
            }

            int distance = Mathf.Abs(stack.position.x - origin.x) + Mathf.Abs(stack.position.y - origin.y);
            if (distance > radius)
            {
                continue;
            }

            DungeonItemDefinition definition = catalogProvider.GetDefinition(stack.itemId);
            if (inventory.GetMaxAcceptableQuantity(
                    stack.itemId,
                    1,
                    catalogProvider,
                    haulingSettingsProvider) <= 0)
            {
                continue;
            }

            float score = definition.UnitPrice * 10f
                + Mathf.Min(50, stack.quantity)
                - distance * 5f;
            if (score <= bestScore)
            {
                continue;
            }

            bestStack = stack;
            bestDefinition = definition;
            bestScore = score;
        }

        if (bestStack == null)
        {
            failureReason = "no loose item nearby";
            return false;
        }

        if (!inventory.TryAdd(
                $"floor-theft:{bestStack.stackId}:{gameClock.FrameCount}",
                bestStack.itemId,
                1,
                catalogProvider,
                haulingSettingsProvider,
                out failureReason))
        {
            return false;
        }

        stolenItem = ToSnapshot(bestStack);
        stolenItem.Quantity = 1;
        if (bestDefinition != null)
        {
            stolenItem.DisplayName = bestDefinition.DisplayName;
            stolenItem.Description = bestDefinition.Description;
            stolenItem.StockCategory = bestDefinition.StockCategory;
            stolenItem.UnitPrice = bestDefinition.UnitPrice;
            stolenItem.UnitWeight = bestDefinition.UnitWeight;
            stolenItem.Sprite = bestDefinition.Sprite;
        }

        Vector2Int position = bestStack.position;
        bestStack.quantity--;
        MarkStacksChanged();
        if (bestStack.quantity <= 0)
        {
            RemoveRecord(bestStack);
        }

        RefreshMarkerAt(position);
        return true;
    }

    public void ReleaseReservation(string stackId, string persistentId)
    {
        reservationService.Release(stackId, persistentId);
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

    public bool DeleteStack(string stackId)
    {
        if (string.IsNullOrWhiteSpace(stackId)
            || !stacksById.TryGetValue(stackId, out WorldItemStackRecord record))
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
        consumed = null;
        if (string.IsNullOrWhiteSpace(stackId)
            || quantity <= 0
            || !stacksById.TryGetValue(stackId, out WorldItemStackRecord record)
            || record == null
            || record.quantity <= 0)
        {
            return false;
        }

        if (DungeonDebugRuntimeRules.ShouldSkipCosts())
        {
            consumed = ToSnapshot(record);
            consumed.Quantity = Mathf.Min(quantity, record.quantity);
            return consumed.Quantity > 0;
        }

        int amount = Mathf.Min(quantity, record.quantity);
        consumed = ToSnapshot(record);
        consumed.Quantity = amount;
        Vector2Int position = record.position;
        record.quantity -= amount;
        MarkStacksChanged();
        if (record.quantity <= 0)
        {
            RemoveRecord(record);
        }

        RefreshMarkerAt(position);
        return amount > 0;
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

    private int CountLooseStockAvailable(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        return stacks
            .Where(stack => stack != null
                && stack.quantity > 0
                && stack.state == WorldItemStackState.Loose
                && !stack.forbidden
                && string.IsNullOrWhiteSpace(stack.reservedByPersistentId)
                && string.IsNullOrWhiteSpace(stack.destinationId)
                && string.Equals(stack.itemId, itemId, StringComparison.Ordinal))
            .Sum(stack => stack.quantity);
    }

    private int RequestLooseStockDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId)
    {
        if (string.IsNullOrWhiteSpace(itemId)
            || string.IsNullOrWhiteSpace(destinationId)
            || amount <= 0)
        {
            return 0;
        }

        int remaining = amount;
        int requested = 0;
        foreach (WorldItemStackRecord stack in stacks
            .Where(stack => stack != null
                && stack.quantity > 0
                && stack.state == WorldItemStackState.Loose
                && !stack.forbidden
                && string.IsNullOrWhiteSpace(stack.reservedByPersistentId)
                && string.IsNullOrWhiteSpace(stack.destinationId)
                && string.Equals(stack.itemId, itemId, StringComparison.Ordinal))
            .OrderBy(stack => Mathf.Abs(stack.position.x - destinationPosition.x)
                + Mathf.Abs(stack.position.y - destinationPosition.y))
            .ToArray())
        {
            if (remaining <= 0)
            {
                break;
            }

            int moved = Mathf.Min(remaining, stack.quantity);
            Vector2Int sourcePosition = stack.position;
            stack.quantity -= moved;
            MarkStacksChanged();
            if (stack.quantity <= 0)
            {
                RemoveRecord(stack);
            }

            requested += Spawn(
                itemId,
                moved,
                sourcePosition,
                WorldItemStackState.Loose,
                destinationId,
                hasDestinationPosition: true,
                destinationPosition: destinationPosition);
            remaining -= moved;
            RefreshMarkerAt(sourcePosition);
        }

        return requested;
    }

    private int RequestStoredStockDelivery(
        IEnumerable<IWarehouseFacility> warehouses,
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId)
    {
        if (string.IsNullOrWhiteSpace(itemId)
            || string.IsNullOrWhiteSpace(destinationId)
            || amount <= 0)
        {
            return 0;
        }

        int remaining = amount;
        int requested = 0;
        foreach (IWarehouseFacility warehouse in (warehouses ?? Enumerable.Empty<IWarehouseFacility>())
            .Where(candidate => candidate != null && candidate.Inventory != null)
            .OrderBy(candidate => candidate is BuildableObject building
                ? GetManhattanDistance(building.centerPos, destinationPosition)
                : int.MaxValue))
        {
            if (remaining <= 0)
            {
                break;
            }

            string storageDestinationId = GetWarehouseStorageDestinationId(warehouse);
            foreach (WorldItemStackRecord stack in stacks
                .Where(stack => stack != null
                    && stack.quantity > 0
                    && stack.state == WorldItemStackState.Stored
                    && !stack.forbidden
                    && string.IsNullOrWhiteSpace(stack.reservedByPersistentId)
                    && string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                    && string.Equals(stack.itemId, itemId, StringComparison.Ordinal)
                    && string.Equals(
                        stack.destinationId ?? string.Empty,
                        storageDestinationId,
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
                MarkStacksChanged();
                if (stack.quantity <= 0)
                {
                    RemoveRecord(stack);
                }

                int created = Spawn(
                    itemId,
                    assigned,
                    sourcePosition,
                    WorldItemStackState.Stored,
                    destinationId,
                    hasDestinationPosition: true,
                    destinationPosition: destinationPosition,
                    sourceStorageDestinationId: storageDestinationId);
                if (created < assigned)
                {
                    AddStoredWarehouseItems(warehouse, itemId, assigned - created);
                }

                requested += created;
                remaining -= created;
                RefreshMarkerAt(sourcePosition);
            }
        }

        return requested;
    }

    private void EnsureStoredWarehouseMirror(
        IWarehouseFacility warehouse,
        string itemId,
        StockCategory category)
    {
        if (warehouse == null
            || warehouse.Inventory == null
            || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        string storageDestinationId = GetWarehouseStorageDestinationId(warehouse);
        int physicalAmount = stacks
            .Where(stack => stack != null
                && stack.quantity > 0
                && stack.state == WorldItemStackState.Stored
                && string.Equals(stack.itemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    GetStoredSourceDestinationId(stack),
                    storageDestinationId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.quantity);
        int missingMirror = Mathf.Max(0, warehouse.Inventory.GetStock(category) - physicalAmount);
        if (missingMirror > 0)
        {
            AddStoredWarehouseItems(warehouse, itemId, missingMirror);
        }
    }

    private int CountUnassignedStoredStock(IWarehouseFacility warehouse, string itemId)
    {
        if (warehouse == null || string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        string storageDestinationId = GetWarehouseStorageDestinationId(warehouse);
        return stacks
            .Where(stack => stack != null
                && stack.quantity > 0
                && stack.state == WorldItemStackState.Stored
                && !stack.forbidden
                && string.IsNullOrWhiteSpace(stack.reservedByPersistentId)
                && string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                && string.Equals(stack.itemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    stack.destinationId ?? string.Empty,
                    storageDestinationId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.quantity);
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
        string sourceStorageDestinationId = "")
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
            sourceStorageDestinationId);
    }

    private static int GetManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
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

    private void SyncWarehouseInventoriesFromStoredStacks()
    {
        Dictionary<string, Dictionary<StockCategory, int>> stockByWarehouse =
            new Dictionary<string, Dictionary<StockCategory, int>>(StringComparer.Ordinal);
        foreach (WorldItemStackRecord stack in stacks)
        {
            if (stack == null
                || stack.quantity <= 0
                || stack.state != WorldItemStackState.Stored
                || !TryGetWarehouseStockCategory(stack.itemId, out StockCategory category))
            {
                continue;
            }

            string destinationId = GetStoredSourceDestinationId(stack);
            if (!destinationId.StartsWith(WarehouseStorageDestinationPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!stockByWarehouse.TryGetValue(destinationId, out Dictionary<StockCategory, int> stockByCategory))
            {
                stockByCategory = new Dictionary<StockCategory, int>();
                stockByWarehouse[destinationId] = stockByCategory;
            }

            long combined = (long)(stockByCategory.TryGetValue(category, out int current) ? current : 0)
                + stack.quantity;
            stockByCategory[category] = combined >= int.MaxValue ? int.MaxValue : (int)combined;
        }

        if (stockByWarehouse.Count == 0)
        {
            return;
        }

        foreach (IWarehouseFacility warehouse in GetWarehouses())
        {
            WarehouseInventorySnapshot snapshot = warehouse.Inventory.CreateSnapshot();
            string destinationId = GetWarehouseStorageDestinationId(warehouse);
            stockByWarehouse.TryGetValue(destinationId, out Dictionary<StockCategory, int> stockByCategory);
            snapshot.stocks = (stockByCategory ?? new Dictionary<StockCategory, int>())
                .Where(pair => pair.Value > 0 && warehouse.Inventory.Accepts(pair.Key))
                .OrderBy(pair => StockCategoryCatalog.TryGet(pair.Key, out StockCategoryDefinition definition)
                    ? definition.SortOrder
                    : int.MaxValue)
                .ThenBy(pair => Convert.ToInt32(pair.Key, CultureInfo.InvariantCulture))
                .Select(pair => StockAmountSnapshot.From(pair.Key, pair.Value))
                .ToList();
            warehouse.Inventory.ApplySnapshot(snapshot);
        }
    }

    private int AddStoredWarehouseItems(IWarehouseFacility warehouse, string itemId, int amount)
    {
        if (warehouse == null || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return 0;
        }

        return Spawn(
            itemId,
            amount,
            ResolveWarehouseStoragePosition(warehouse),
            WorldItemStackState.Stored,
            GetWarehouseStorageDestinationId(warehouse));
    }

    private int RemoveStoredWarehouseItems(IWarehouseFacility warehouse, string itemId, int amount)
    {
        if (warehouse == null || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
        {
            return 0;
        }

        if (DungeonDebugRuntimeRules.ShouldSkipCosts())
        {
            return amount;
        }

        string destinationId = GetWarehouseStorageDestinationId(warehouse);
        int remaining = amount;
        int removed = 0;
        foreach (WorldItemStackRecord stack in stacks.ToArray())
        {
            if (remaining <= 0)
            {
                break;
            }

            if (stack == null
                || stack.quantity <= 0
                || stack.state != WorldItemStackState.Stored
                || !string.Equals(stack.itemId, itemId, StringComparison.Ordinal)
                || !string.Equals(stack.destinationId ?? string.Empty, destinationId, StringComparison.Ordinal))
            {
                continue;
            }

            int consumed = Mathf.Min(remaining, stack.quantity);
            Vector2Int position = stack.position;
            stack.quantity -= consumed;
            remaining -= consumed;
            removed += consumed;
            MarkStacksChanged();
            if (stack.quantity <= 0)
            {
                RemoveRecord(stack);
            }

            RefreshMarkerAt(position);
        }

        return removed;
    }

    private static string GetWarehouseStorageDestinationId(IWarehouseFacility warehouse)
    {
        if (warehouse is BuildableObject building)
        {
            return string.Concat(
                WarehouseStorageDestinationPrefix,
                building.GridId.ToString(CultureInfo.InvariantCulture),
                ":",
                building.centerPos.x.ToString(CultureInfo.InvariantCulture),
                ":",
                building.centerPos.y.ToString(CultureInfo.InvariantCulture));
        }

        return WarehouseStorageDestinationPrefix + warehouse.GetHashCode().ToString(CultureInfo.InvariantCulture);
    }

    private void NormalizeLegacyWarehouseStorageIds()
    {
        IWarehouseFacility[] warehouses = GetWarehouses().ToArray();
        foreach (WorldItemStackRecord stack in stacks)
        {
            if (stack == null || stack.state != WorldItemStackState.Stored)
            {
                continue;
            }

            stack.destinationId = NormalizeLegacyWarehouseStorageId(
                stack.destinationId,
                stack.position,
                warehouses);
            stack.sourceStorageDestinationId = NormalizeLegacyWarehouseStorageId(
                stack.sourceStorageDestinationId,
                stack.position,
                warehouses);
        }
    }

    private static string NormalizeLegacyWarehouseStorageId(
        string storageDestinationId,
        Vector2Int storagePosition,
        IReadOnlyList<IWarehouseFacility> warehouses)
    {
        string normalized = storageDestinationId?.Trim() ?? string.Empty;
        if (!normalized.StartsWith(WarehouseStorageDestinationPrefix, StringComparison.Ordinal))
        {
            return normalized;
        }

        string suffix = normalized.Substring(WarehouseStorageDestinationPrefix.Length);
        if (suffix.Contains(":")
            || !int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int legacyGridId))
        {
            return normalized;
        }

        IWarehouseFacility matchingWarehouse = (warehouses ?? Array.Empty<IWarehouseFacility>())
            .FirstOrDefault(candidate =>
                candidate is BuildableObject building
                && building.GridId == legacyGridId
                && building.centerPos == storagePosition);
        return matchingWarehouse != null
            ? GetWarehouseStorageDestinationId(matchingWarehouse)
            : normalized;
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

    private static bool TryGetWarehouseStockCategory(string itemId, out StockCategory category)
    {
        if (DungeonItemCatalogSO.TryGetStockCategoryFromItemId(itemId, out category))
        {
            return true;
        }

        if (CombatItemDefinitions.TryGetDefinition(itemId, out DungeonItemDefinition ammunition))
        {
            category = ammunition.StockCategory;
            return true;
        }

        category = default;
        return false;
    }

    private static void RestoreDirectPickupStack(WorldItemStackRecord stack)
    {
        if (stack == null || !IsCombatLoadoutDestination(stack.destinationId))
        {
            return;
        }

        stack.destinationId = stack.sourceStorageDestinationId ?? string.Empty;
        stack.sourceStorageDestinationId = string.Empty;
        stack.hasDestinationPosition = false;
        stack.destinationPosition = default;
    }

    private static bool TryResolvePickupStandCell(
        Grid grid,
        Vector2Int itemPosition,
        out Vector2Int standCell)
    {
        if (grid.IsValidGridPos(itemPosition) && grid.IsWalkable(itemPosition))
        {
            standCell = itemPosition;
            return true;
        }

        return grid.TryFindNearbyWalkablePositionOnSameFloor(itemPosition, out standCell, maxDistance: 1);
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

    private Vector2Int ResolveActorGridPosition(CharacterActor actor)
    {
        if (actor != null && TryGetGrid(out Grid grid))
        {
            return grid.GetXY(actor.transform.position);
        }

        return Vector2Int.zero;
    }

    private void AddRecord(WorldItemStackRecord record)
    {
        itemRepository.Add(record);
    }

    private void RemoveRecord(WorldItemStackRecord record)
    {
        itemRepository.Remove(record);
    }

    private void ClearRuntimeStacks()
    {
        itemRepository.Clear();
        itemMarkerPresenter.Clear();
    }

    private void MarkStacksChanged()
    {
        itemRepository.MarkChanged();
    }

    private string AllocateStackId()
    {
        return itemRepository.AllocateStackId();
    }

    private WorldItemStackSaveData ToSaveData(WorldItemStackRecord stack)
    {
        return new WorldItemStackSaveData
        {
            stackId = stack.stackId,
            itemId = stack.itemId,
            quantity = Mathf.Max(0, stack.quantity),
            state = stack.state,
            gridX = stack.position.x,
            gridY = stack.position.y,
            reservedByPersistentId = stack.reservedByPersistentId ?? string.Empty,
            destinationId = stack.destinationId ?? string.Empty,
            sourceStorageDestinationId = stack.sourceStorageDestinationId ?? string.Empty,
            hasDestinationPosition = stack.hasDestinationPosition,
            destinationGridX = stack.destinationPosition.x,
            destinationGridY = stack.destinationPosition.y,
            forbidden = stack.forbidden
            ,sourceCharacterId = stack.sourceCharacterId ?? string.Empty
            ,sourceDisplayName = stack.sourceDisplayName ?? string.Empty
            ,sourceSpeciesTag = stack.sourceSpeciesTag ?? string.Empty
            ,sourceDeathReason = stack.sourceDeathReason ?? string.Empty
            ,emergencyButcheryAllowed = stack.emergencyButcheryAllowed
        };
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
