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

    public void Configure(IShopStockCatalog stockCatalog)
    {
        catalog = stockCatalog ?? throw new ArgumentNullException(nameof(stockCatalog));
        core.Configure(new ShopStockCatalogAdapter(stockCatalog));
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

    public int RestockFrom(
        IEnumerable<IWarehouseFacility> warehouses,
        int maxAmount,
        out string resultMessage) =>
        core.RestockFrom(AdaptWarehouses(warehouses), maxAmount, out resultMessage);

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

    public int ReceiveRestock(
        WarehouseRestockItem saleItem,
        int amount,
        int requestedAmount,
        out string resultMessage)
    {
        return core.ReceiveRestock(
            saleItem.Definition,
            amount,
            requestedAmount,
            out resultMessage);
    }

    public int ReceiveRestock(
        SaleItem saleItem,
        int amount,
        int requestedAmount,
        out string resultMessage)
    {
        if (saleItem == null || catalog == null)
        {
            resultMessage = "보충할 상품 데이터가 없습니다";
            return 0;
        }

        return core.ReceiveRestock(
            ShopStockCatalogAdapter.Capture(
                saleItem,
                catalog.GetStockCategory(saleItem.id)),
            amount,
            requestedAmount,
            out resultMessage);
    }

    public bool HasRestockSupply(
        IReadOnlyList<IWarehouseFacility> warehouses,
        out string failureReason) =>
        core.HasRestockSupply(AdaptWarehouses(warehouses), out failureReason);

    public ShopStockStateSnapshot CreateSnapshot() => core.CreateSnapshot();
    public void ApplySnapshot(ShopStockStateSnapshot snapshot) => core.ApplySnapshot(snapshot);
    public void Clear() => core.Clear();
    public bool TryInitialize(bool requireCatalog) => core.TryInitialize(requireCatalog);
    public void EnsureInitialized() => core.EnsureInitialized();

    public static int GetRemainingStockAfterSelection(
        RemainStock stock,
        IReadOnlyDictionary<int, int> selectedCounts) =>
        DungeonStory.Buildings.ShopInventoryRuntime.GetRemainingStockAfterSelection(
            stock,
            selectedCounts);

    private static IReadOnlyList<IBuildingShopWarehousePort> AdaptWarehouses(
        IEnumerable<IWarehouseFacility> warehouses)
    {
        return warehouses?.Where(warehouse => warehouse != null)
                   .Select(warehouse => (IBuildingShopWarehousePort)
                       new ShopWarehouseAdapter(warehouse))
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
                Capture(tuple.Item1, catalog.GetStockCategory(tuple.Item1.id)),
                tuple.Item2))
            .ToArray() ?? Array.Empty<ShopStockSeed>();
        definition = new ShopStockDefinition(stockInfo.multifly, stocks);
        return true;
    }

    public StockCategory GetStockCategory(int saleItemId) =>
        catalog.GetStockCategory(saleItemId);

    internal static ShopSaleItemDefinition Capture(
        SaleItem item,
        StockCategory stockCategory)
    {
        if (item == null) return default;
        return new ShopSaleItemDefinition(
            item.id,
            item.itemName,
            stockCategory,
            item.cost,
            item.buyevent);
    }
}

internal sealed class ShopWarehouseAdapter : IBuildingShopWarehousePort
{
    private readonly IWarehouseFacility warehouse;

    public ShopWarehouseAdapter(IWarehouseFacility warehouse)
    {
        this.warehouse = warehouse ?? throw new ArgumentNullException(nameof(warehouse));
    }

    public object RuntimeObject => warehouse;
    private IWarehouseInventoryPort Inventory => warehouse.Inventory;
    public bool HasInventory => warehouse.HasWarehouseInventory && Inventory != null;
    public int GetStock(StockCategory category) =>
        HasInventory ? Inventory.GetStock(category) : 0;
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

    public int ConsumeWarehouseStock(
        IBuildingShopWarehousePort warehouse,
        StockCategory category,
        int amount)
    {
        IWarehouseFacility source = warehouse?.RuntimeObject as IWarehouseFacility
            ?? throw new InvalidOperationException(
                "Shop restocking requires a warehouse adapter.");
        IBuildingItemStackPort items = owner.WorldItemStackRuntime
            ?? throw new InvalidOperationException(
                "Shop restocking requires physical item runtime.");
        return items.ConsumeWarehouseStock(
            source as IBuildingWorldEntryPort,
            category,
            amount);
    }

    public void PublishRestock(int requested, int received, string message) =>
        owner.PublishRestockEvent(requested, received, message);

    public void NotifyStockChanged() => owner.NotifyStockChanged();

    public void ReportMissingStockDefinition(int shopId)
    {
        Debug.LogWarning(
            $"{owner.name} 상점 재고 데이터를 찾지 못했습니다. shopId: {shopId}");
    }
}
