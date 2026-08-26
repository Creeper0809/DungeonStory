using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Buildings;
using UnityEngine;

internal sealed class ShopInventoryRuntime
{
    private readonly Shop owner;
    private readonly ShopInventoryOwnerAdapter ownerAdapter;
    private readonly DungeonStory.Buildings.ShopInventoryRuntime core;
    private IShopStockCatalog catalog;
    private ShopStockCatalogAdapter catalogAdapter;
    private IStockQuery physicalStockQuery;

    public ShopInventoryRuntime(Shop owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        ownerAdapter = new ShopInventoryOwnerAdapter(owner);
        core = new DungeonStory.Buildings.ShopInventoryRuntime(ownerAdapter);
    }

    public IReadOnlyList<RemainStock> Stocks => core.Stocks;
    public StockCategory ActiveCategory => core.ActiveCategory;
    public int CurrentCount => core.CurrentCount;
    public int MaxInternalStock => core.MaxInternalStock;
    public int MissingStock => core.MissingStock;

    public void Configure(
        IShopStockCatalog stockCatalog,
        IStockQuery stockQuery)
    {
        catalog = stockCatalog ?? throw new ArgumentNullException(nameof(stockCatalog));
        physicalStockQuery = stockQuery ?? throw new ArgumentNullException(nameof(stockQuery));
        catalogAdapter = new ShopStockCatalogAdapter(stockCatalog);
        core.Configure(catalogAdapter);
    }

    public void SynchronizeAuthoredStock(
        IRetailStockPhysicalRuntime physicalRuntime)
    {
        EnsureInitialized();
        string ownerId = owner.RequirePersistentInstanceId().Value;
        HashSet<string> existingOperations = new HashSet<string>(
            core.CreateSnapshot().lots
                .Where(lot => lot != null)
                .Select(lot => lot.sourceOperationId),
            StringComparer.Ordinal);
        foreach (ShopStockSeed pending in core.CapturePendingAcceptedAuthoredStock())
        {
            ShopSaleItemDefinition saleItem = pending.Item;
            int authoredAmount = Math.Max(0, pending.Amount);
            if (!saleItem.RequiresUniqueInstance)
            {
                if (!core.TryActivateAuthoredGenericStock(
                        pending,
                        out string genericFailure)
                    && genericFailure != "retail-authored-generic-capacity-unavailable")
                {
                    throw new InvalidOperationException(
                        $"Shop '{ownerId}' could not activate generic stock '{saleItem.Id}': {genericFailure}");
                }
                continue;
            }
            if (physicalRuntime == null)
            {
                throw new InvalidOperationException(
                    $"Shop '{ownerId}' requires a physical source runtime for unique sale item '{saleItem.Id}'.");
            }

            int createdCount = 0;
            for (int ordinal = 0;
                 ordinal < authoredAmount && core.MissingStock > 0;
                 ordinal++)
            {
                string sourceOperationId =
                    $"retail-source:authored:{ownerId}:{saleItem.Id}:{ordinal:D4}";
                if (existingOperations.Contains(sourceOperationId))
                {
                    createdCount++;
                    continue;
                }
                if (!physicalRuntime.TryCreateAuthoredUniqueLot(
                        saleItem.Id,
                        saleItem.ItemDefinitionId,
                        saleItem.UnitMassGrams,
                        sourceOperationId,
                        out RetailStockLotSnapshot lot,
                        out string sourceFailure))
                {
                    throw new InvalidOperationException(
                        $"Shop '{ownerId}' could not create exact unique stock '{sourceOperationId}': {sourceFailure}");
                }
                if (!core.TryReceiveExactLot(
                        lot,
                        1,
                        out int received,
                        out string receiveFailure)
                    || received != 1)
                {
                    if (!physicalRuntime.TryCommitExternalSink(
                            lot,
                            out string rollbackFailure))
                    {
                        throw new InvalidOperationException(
                            $"Shop '{ownerId}' failed to rollback unique source '{sourceOperationId}': {rollbackFailure}");
                    }
                    throw new InvalidOperationException(
                        $"Shop '{ownerId}' rejected unique source '{sourceOperationId}': {receiveFailure}");
                }
                existingOperations.Add(sourceOperationId);
                createdCount++;
            }
            if (createdCount > 0)
            {
                core.MarkAuthoredSaleItemActivated(saleItem.Id);
            }
        }
    }

    public void Reset() => core.Reset();
    public IReadOnlyList<RetailProductSnapshot> CreateProductSnapshots(float priceMultiplier) =>
        core.CreateProductSnapshots(priceMultiplier);
    public List<Stock> GetStock(float priceMultiplier) => core.GetStock(priceMultiplier);
    public IReadOnlyList<Stock> GetPurchasableStock(
        IReadOnlyDictionary<int, int> selectedCounts,
        float priceMultiplier) => core.GetPurchasableStock(selectedCounts, priceMultiplier);
    public Stock CreatePricedStock(RemainStock stock, float priceMultiplier) =>
        core.CreatePricedStock(stock, priceMultiplier);
    public StockCategory GetStockCategory(int saleItemId) => core.GetStockCategory(saleItemId);
    public bool TryGetSaleItem(int saleItemId, out SaleItem saleItem)
    {
        saleItem = null;
        if (catalog == null)
        {
            return false;
        }

        return catalog.TryGetSaleItem(saleItemId, out saleItem);
    }

    public bool TryFindRestockSource(
        IEnumerable<IWarehouseFacility> warehouses,
        int maxAmount,
        out IWarehouseFacility warehouse,
        out WarehouseRestockItem saleItem,
        out int amount,
        out string failureReason)
    {
        warehouse = null;
        saleItem = default;
        amount = 0;
        if (!core.TryFindRestockSource(
                AdaptWarehouses(warehouses),
                maxAmount,
                out ShopRestockSource source,
                out failureReason))
        {
            return false;
        }

        warehouse = source.Warehouse?.RuntimeObject as IWarehouseFacility;
        if (warehouse == null
            || catalog == null
            || !catalog.TryGetSaleItem(source.Item.Id, out SaleItem authoredItem)
            || authoredItem == null)
        {
            warehouse = null;
            failureReason = "Restock item or warehouse reference is invalid.";
            return false;
        }

        saleItem = new WarehouseRestockItem(source.Item, authoredItem.itemSprite);
        amount = source.Amount;
        return true;
    }

    public bool TryFindRestockSource(
        IEnumerable<IWarehouseFacility> warehouses,
        int maxAmount,
        out IWarehouseFacility warehouse,
        out SaleItem saleItem,
        out int amount,
        out string failureReason)
    {
        warehouse = null;
        saleItem = null;
        amount = 0;
        if (!core.TryFindRestockSource(
                AdaptWarehouses(warehouses),
                maxAmount,
                out ShopRestockSource source,
                out failureReason))
        {
            return false;
        }

        warehouse = source.Warehouse?.RuntimeObject as IWarehouseFacility;
        if (warehouse == null
            || catalog == null
            || !catalog.TryGetSaleItem(source.Item.Id, out saleItem)
            || saleItem == null)
        {
            warehouse = null;
            saleItem = null;
            failureReason = "보충 상품 또는 창고 참조가 유효하지 않습니다";
            return false;
        }

        amount = source.Amount;
        return true;
    }

    public bool TryReceiveExactRetailLots(
        IReadOnlyList<RetailStockLotSnapshot> incoming,
        int requestedAmount,
        out int received,
        out string resultMessage) =>
        core.TryReceiveExactLots(
            incoming,
            requestedAmount,
            out received,
            out resultMessage);

    public bool HasRestockSupply(
        IReadOnlyList<IWarehouseFacility> warehouses,
        out string failureReason) =>
        core.HasRestockSupply(AdaptWarehouses(warehouses), out failureReason);

    public ShopStockStateSnapshot CreateSnapshot() => core.CreateSnapshot();
    public void ApplySnapshot(ShopStockStateSnapshot snapshot) => core.ApplySnapshot(snapshot);
    public bool TryTakeExactLot(
        int saleItemId,
        out RetailStockLotSnapshot taken,
        out string failureReason) =>
        core.TryTakeExactLot(saleItemId, out taken, out failureReason);
    public bool TryRestoreTakenExactLot(
        RetailStockLotSnapshot taken,
        out string failureReason) =>
        core.TryRestoreTakenExactLot(taken, out failureReason);
    public void Clear() => core.Clear();
    public bool TryInitialize(bool requireCatalog) => core.TryInitialize(requireCatalog);
    public void EnsureInitialized() => core.EnsureInitialized();

    public static int GetRemainingStockAfterSelection(
        RemainStock stock,
        IReadOnlyDictionary<int, int> selectedCounts) =>
        DungeonStory.Buildings.ShopInventoryRuntime.GetRemainingStockAfterSelection(
            stock,
            selectedCounts);

    private IReadOnlyList<IBuildingShopWarehousePort> AdaptWarehouses(
        IEnumerable<IWarehouseFacility> warehouses)
    {
        return warehouses?.Where(warehouse => warehouse != null)
                   .Select(warehouse => (IBuildingShopWarehousePort)
                       new ShopWarehouseAdapter(warehouse, physicalStockQuery))
                   .ToArray()
               ?? Array.Empty<IBuildingShopWarehousePort>();
    }
}

internal sealed class ShopStockCatalogAdapter : IBuildingShopStockCatalogPort
{
    private readonly IShopStockCatalog catalog;

    public ShopStockCatalogAdapter(IShopStockCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public bool TryGetStockDefinition(int shopId, out ShopStockDefinition definition)
    {
        if (!catalog.TryGetStockInfoForShop(shopId, out StockInfo stockInfo)
            || stockInfo == null)
        {
            definition = null;
            return false;
        }

        ShopStockSeed[] stocks = stockInfo.stocks?
            .Where(tuple => tuple?.Item1 != null)
            .Select(tuple => new ShopStockSeed(
                Capture(tuple.Item1),
                tuple.Item2))
            .ToArray() ?? Array.Empty<ShopStockSeed>();
        definition = new ShopStockDefinition(stockInfo.multifly, stocks);
        return true;
    }

    public StockCategory GetStockCategory(int saleItemId) =>
        catalog.GetStockCategory(saleItemId);

    internal ShopSaleItemDefinition Capture(SaleItem item)
    {
        if (item == null
            || !catalog.TryGetPhysicalDescriptor(
                item.id,
                out ItemDefinitionId itemDefinitionId,
                out long unitMassGrams,
                out bool requiresUniqueInstance))
        {
            throw new InvalidOperationException(
                $"Sale item '{item?.id}' has no canonical physical descriptor.");
        }

        return new ShopSaleItemDefinition(
            item.id,
            item.itemName,
            itemDefinitionId.Value,
            unitMassGrams,
            requiresUniqueInstance,
            catalog.GetStockCategory(item.id),
            item.cost,
            item.buyevent);
    }
}

internal sealed class ShopWarehouseAdapter : IBuildingShopWarehousePort
{
    private readonly IWarehouseFacility warehouse;
    private readonly IStockQuery physicalStockQuery;

    public ShopWarehouseAdapter(
        IWarehouseFacility warehouse,
        IStockQuery physicalStockQuery)
    {
        this.warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
        this.physicalStockQuery = physicalStockQuery
            ?? throw new ArgumentNullException(nameof(physicalStockQuery));
    }

    public object RuntimeObject => warehouse;
    private IWarehouseInventoryPort Inventory => warehouse.Inventory;
    public bool HasInventory => warehouse.HasWarehouseInventory && Inventory != null;
    public int GetStock(string itemDefinitionId) =>
        HasInventory && !string.IsNullOrWhiteSpace(itemDefinitionId)
            ? physicalStockQuery.GetWarehouseQuantity(
                warehouse.PersistentInstanceId,
                itemDefinitionId)
            : 0;
}

internal sealed class ShopInventoryOwnerAdapter : IBuildingShopInventoryOwnerPort
{
    private readonly Shop owner;

    public ShopInventoryOwnerAdapter(Shop owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public bool IsConfigured => owner.BuildingData != null;
    public int ShopId => owner.id;
    public string PersistentOwnerId =>
        owner.RequirePersistentInstanceId().Value;
    public string DisplayName => owner.name;
    public int ConfiguredInternalStockCapacity =>
        owner.BuildingData?.GetInternalStockCapacity() ?? 0;

    public bool HasRetailSpecialization
    {
        get
        {
            BuildingRoomOperationalSnapshot profile = owner.GetRoomOperationalProfile();
            return profile?.Parts != null
                && profile.Parts.OfType<BuildableObject>().Any(part =>
                    part?.BuildingData != null
                    && part.BuildingData.GetStockCategorySignals().Any());
        }
    }

    public StockCategory RetailCategory =>
        owner.GetRoomOperationalProfile().RetailCategory;

    public int GetRoomStorageCapacity(StockCategory category) =>
        owner.GetRoomOperationalProfile().GetStorageCapacity(category);

    public void PublishRestock(int requested, int received, string message) =>
        owner.PublishRestockEvent(requested, received, message);

    public void NotifyStockChanged() => owner.NotifyStockChanged();

    public void ReportMissingStockDefinition(int shopId)
    {
        Debug.LogWarning(
            $"{owner.name} 상점 재고 데이터를 찾지 못했습니다. shopId: {shopId}");
    }
}
