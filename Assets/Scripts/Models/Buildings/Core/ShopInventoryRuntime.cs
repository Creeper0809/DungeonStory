using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.Buildings
{
    public readonly struct ShopSaleItemDefinition
    {
        public ShopSaleItemDefinition(
            int id,
            string name,
            StockCategory category,
            int cost,
            OnBuyItemSO[] onBuy)
        {
            Id = id;
            Name = name ?? string.Empty;
            Category = category;
            Cost = cost;
            OnBuy = onBuy ?? Array.Empty<OnBuyItemSO>();
        }

        public int Id { get; }
        public string Name { get; }
        public StockCategory Category { get; }
        public int Cost { get; }
        public OnBuyItemSO[] OnBuy { get; }
    }

    public readonly struct ShopStockSeed
    {
        public ShopStockSeed(ShopSaleItemDefinition item, int amount)
        {
            Item = item;
            Amount = amount;
        }

        public ShopSaleItemDefinition Item { get; }
        public int Amount { get; }
    }

    public sealed class ShopStockDefinition
    {
        public ShopStockDefinition(float priceMultiplier, IReadOnlyList<ShopStockSeed> stocks)
        {
            PriceMultiplier = priceMultiplier;
            Stocks = stocks ?? Array.Empty<ShopStockSeed>();
        }

        public float PriceMultiplier { get; }
        public IReadOnlyList<ShopStockSeed> Stocks { get; }
    }

    public interface IBuildingShopStockCatalogPort
    {
        bool TryGetStockDefinition(int shopId, out ShopStockDefinition definition);
        StockCategory GetStockCategory(int saleItemId);
    }

    public interface IBuildingShopWarehousePort
    {
        object RuntimeObject { get; }
        bool HasInventory { get; }
        int GetStock(StockCategory category);
    }

    public interface IBuildingShopInventoryOwnerPort
    {
        bool IsConfigured { get; }
        int ShopId { get; }
        string DisplayName { get; }
        int ConfiguredInternalStockCapacity { get; }
        bool HasRetailSpecialization { get; }
        StockCategory RetailCategory { get; }
        int GetRoomStorageCapacity(StockCategory category);
        int ConsumeWarehouseStock(
            IBuildingShopWarehousePort warehouse,
            StockCategory category,
            int amount);
        void PublishRestock(int requested, int received, string message);
        void NotifyStockChanged();
        void ReportMissingStockDefinition(int shopId);
    }

    public readonly struct ShopRestockSource
    {
        public ShopRestockSource(
            IBuildingShopWarehousePort warehouse,
            ShopSaleItemDefinition item,
            int amount)
        {
            Warehouse = warehouse;
            Item = item;
            Amount = amount;
        }

        public IBuildingShopWarehousePort Warehouse { get; }
        public ShopSaleItemDefinition Item { get; }
        public int Amount { get; }
    }

    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class ShopInventoryRuntime
    {
        private readonly IBuildingShopInventoryOwnerPort owner;
        private readonly List<RemainStock> stocks = new();
        private readonly List<Stock> purchasableStockBuffer = new();
        private IBuildingShopStockCatalogPort catalog;
        private ShopStockDefinition baseStock;
        private bool initialized;

        public ShopInventoryRuntime(IBuildingShopInventoryOwnerPort owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public IReadOnlyList<RemainStock> Stocks => stocks;
        public StockCategory ActiveCategory => ResolveActiveStockCategory();
        public int CurrentCount => stocks.Where(stock => stock != null && IsSaleItemAllowed(stock.id)).Sum(stock => stock.stock);
        public int MaxInternalStock => GetMaxInternalStock(ActiveCategory);
        public int MissingStock => Math.Max(0, MaxInternalStock - CurrentCount);

        public void Configure(IBuildingShopStockCatalogPort stockCatalog) =>
            catalog = stockCatalog ?? throw new ArgumentNullException(nameof(stockCatalog));

        public void Reset()
        {
            baseStock = null;
            initialized = false;
            stocks.Clear();
            purchasableStockBuffer.Clear();
        }

        public IReadOnlyList<RetailProductSnapshot> CreateProductSnapshots(float priceMultiplier)
        {
            EnsureInitialized();
            return stocks.Where(stock => stock != null && IsSaleItemAllowed(stock.id))
                .Select(stock => new RetailProductSnapshot(
                    stock.id,
                    stock.itemName,
                    CreatePricedStock(stock, priceMultiplier).cost,
                    stock.stock))
                .ToArray();
        }

        public List<Stock> GetStock(float priceMultiplier)
        {
            FillPurchasableStock(null, priceMultiplier);
            return new List<Stock>(purchasableStockBuffer);
        }

        public IReadOnlyList<Stock> GetPurchasableStock(
            IReadOnlyDictionary<int, int> selectedCounts,
            float priceMultiplier)
        {
            FillPurchasableStock(selectedCounts, priceMultiplier);
            return purchasableStockBuffer;
        }

        public Stock CreatePricedStock(RemainStock stock, float priceMultiplier) =>
            stock == null
                ? new Stock(-1, 0)
                : new Stock(stock.id, (int)Math.Floor(stock.cost * priceMultiplier));

        public StockCategory GetStockCategory(int saleItemId)
        {
            EnsureCatalog();
            return catalog.GetStockCategory(saleItemId);
        }

        public int RestockFrom(
            IEnumerable<IBuildingShopWarehousePort> warehouses,
            int maxAmount,
            out string resultMessage)
        {
            EnsureInitialized();
            resultMessage = string.Empty;
            if (!HasBaseStock())
            {
                resultMessage = "보충할 상품 데이터가 없습니다";
                return 0;
            }

            int targetAmount = Math.Min(Math.Max(0, maxAmount), MissingStock);
            if (targetAmount <= 0)
            {
                resultMessage = "재고가 이미 가득 찼습니다";
                return 0;
            }

            IBuildingShopWarehousePort[] sources = warehouses?.Where(value => value != null).ToArray()
                ?? Array.Empty<IBuildingShopWarehousePort>();
            int restocked = 0;
            foreach (ShopStockSeed seed in baseStock.Stocks)
            {
                if (!IsSaleItemAllowed(seed.Item)) continue;
                while (restocked < targetAmount)
                {
                    IBuildingShopWarehousePort warehouse = sources.FirstOrDefault(candidate =>
                        candidate.HasInventory && candidate.GetStock(seed.Item.Category) > 0);
                    if (warehouse == null) break;
                    int withdrawn = owner.ConsumeWarehouseStock(warehouse, seed.Item.Category, 1);
                    if (withdrawn <= 0) break;
                    AddRemainStock(seed.Item, withdrawn);
                    restocked += withdrawn;
                }
                if (restocked >= targetAmount) break;
            }

            resultMessage = restocked > 0 ? $"{restocked}개 보충" : "창고 재고 부족";
            owner.PublishRestock(targetAmount, restocked, resultMessage);
            if (restocked > 0) owner.NotifyStockChanged();
            return restocked;
        }

        public bool TryFindRestockSource(
            IEnumerable<IBuildingShopWarehousePort> warehouses,
            int maxAmount,
            out ShopRestockSource source,
            out string failureReason)
        {
            source = default;
            failureReason = string.Empty;
            EnsureInitialized();
            if (!HasBaseStock())
            {
                failureReason = "보충할 상품 데이터가 없습니다";
                return false;
            }

            int targetAmount = Math.Min(Math.Max(0, maxAmount), MissingStock);
            if (targetAmount <= 0)
            {
                failureReason = "재고가 이미 충분함";
                return false;
            }

            IBuildingShopWarehousePort[] candidates = warehouses?.Where(value => value != null).ToArray()
                ?? Array.Empty<IBuildingShopWarehousePort>();
            foreach (ShopStockSeed seed in baseStock.Stocks)
            {
                if (!IsSaleItemAllowed(seed.Item)) continue;
                IBuildingShopWarehousePort warehouse = candidates.FirstOrDefault(candidate =>
                    candidate.HasInventory && candidate.GetStock(seed.Item.Category) > 0);
                if (warehouse == null) continue;
                int amount = Math.Min(targetAmount, warehouse.GetStock(seed.Item.Category));
                if (amount <= 0) continue;
                source = new ShopRestockSource(warehouse, seed.Item, amount);
                return true;
            }

            failureReason = "창고 재고 부족";
            return false;
        }

        public int ReceiveRestock(
            ShopSaleItemDefinition saleItem,
            int amount,
            int requestedAmount,
            out string resultMessage)
        {
            if (!IsSaleItemAllowed(saleItem))
            {
                resultMessage = "현재 방 구성과 맞지 않는 상품입니다";
                return 0;
            }

            int restocked = Math.Min(Math.Max(0, amount), MissingStock);
            if (restocked <= 0)
            {
                resultMessage = "재고가 이미 충분함";
                return 0;
            }

            AddRemainStock(saleItem, restocked);
            resultMessage = $"{restocked}개 보충";
            owner.PublishRestock(Math.Max(0, requestedAmount), restocked, resultMessage);
            owner.NotifyStockChanged();
            return restocked;
        }

        public bool HasRestockSupply(
            IReadOnlyList<IBuildingShopWarehousePort> warehouses,
            out string failureReason)
        {
            EnsureInitialized();
            if (MissingStock <= 0)
            {
                failureReason = "재고가 이미 충분합니다";
                return false;
            }
            if (!HasBaseStock())
            {
                failureReason = "보충할 상품 데이터가 없습니다";
                return false;
            }

            foreach (ShopStockSeed seed in baseStock.Stocks)
            {
                if (!IsSaleItemAllowed(seed.Item)) continue;
                if ((warehouses ?? Array.Empty<IBuildingShopWarehousePort>()).Any(warehouse =>
                        warehouse != null && warehouse.HasInventory
                        && warehouse.GetStock(seed.Item.Category) > 0))
                {
                    failureReason = string.Empty;
                    return true;
                }
            }
            failureReason = "창고 재고가 부족합니다";
            return false;
        }

        public ShopStockStateSnapshot CreateSnapshot()
        {
            EnsureInitialized();
            return new ShopStockStateSnapshot
            {
                items = stocks.Where(stock => stock != null)
                    .Select(stock => new ShopStockItemSnapshot
                    {
                        saleItemId = stock.id,
                        amount = Math.Max(0, stock.stock)
                    }).ToList()
            };
        }

        public void ApplySnapshot(ShopStockStateSnapshot snapshot)
        {
            EnsureInitialized();
            stocks.Clear();
            foreach (ShopStockItemSnapshot item in snapshot?.items ?? new List<ShopStockItemSnapshot>())
            {
                if (item.amount > 0 && TryGetBaseStockSeed(item.saleItemId, out ShopStockSeed seed))
                    stocks.Add(CreateRemainStock(seed.Item, item.amount));
            }
            owner.NotifyStockChanged();
        }

        private bool TryGetBaseStockSeed(int saleItemId, out ShopStockSeed seed)
        {
            if (baseStock != null)
            {
                foreach (ShopStockSeed candidate in baseStock.Stocks)
                {
                    if (candidate.Item.Id != saleItemId) continue;
                    seed = candidate;
                    return true;
                }
            }

            seed = default;
            return false;
        }

        public void Clear()
        {
            stocks.Clear();
            owner.NotifyStockChanged();
        }

        public bool TryInitialize(bool requireCatalog)
        {
            if (initialized) return baseStock != null;
            if (!owner.IsConfigured) return false;
            if (catalog == null)
            {
                if (!requireCatalog) return false;
                throw new InvalidOperationException($"{nameof(ShopInventoryRuntime)} for '{owner.DisplayName}' requires catalog injection.");
            }
            initialized = true;
            if (!catalog.TryGetStockDefinition(owner.ShopId, out baseStock))
            {
                owner.ReportMissingStockDefinition(owner.ShopId);
                return false;
            }
            FillStock();
            return true;
        }

        public void EnsureInitialized() => TryInitialize(true);

        private void FillPurchasableStock(IReadOnlyDictionary<int, int> selectedCounts, float priceMultiplier)
        {
            EnsureInitialized();
            purchasableStockBuffer.Clear();
            foreach (RemainStock stock in stocks)
                if (stock != null && IsSaleItemAllowed(stock.id) && GetRemainingStockAfterSelection(stock, selectedCounts) > 0)
                    purchasableStockBuffer.Add(CreatePricedStock(stock, priceMultiplier));
        }

        private void FillStock()
        {
            stocks.Clear();
            if (!HasBaseStock()) return;
            Dictionary<StockCategory, int> remaining = new();
            foreach (ShopStockSeed seed in baseStock.Stocks)
            {
                if (!remaining.TryGetValue(seed.Item.Category, out int capacity))
                    capacity = GetConfiguredInternalStockCapacity() + owner.GetRoomStorageCapacity(seed.Item.Category);
                int initial = Math.Min(Math.Max(0, seed.Amount), capacity);
                if (initial <= 0) continue;
                stocks.Add(CreateRemainStock(seed.Item, initial));
                remaining[seed.Item.Category] = capacity - initial;
            }
            owner.NotifyStockChanged();
        }

        private void AddRemainStock(ShopSaleItemDefinition item, int amount)
        {
            if (amount <= 0) return;
            RemainStock remaining = stocks.FirstOrDefault(stock => stock.id == item.Id);
            if (remaining == null) stocks.Add(CreateRemainStock(item, amount));
            else remaining.stock += amount;
            owner.NotifyStockChanged();
        }

        private RemainStock CreateRemainStock(ShopSaleItemDefinition item, int count) =>
            new(item.Id, item.Name, (int)Math.Floor(item.Cost * (baseStock?.PriceMultiplier ?? 1f)), count, item.OnBuy);

        private bool IsSaleItemAllowed(int saleItemId) =>
            ShouldUseBaseStockPassThrough() || GetStockCategory(saleItemId) == ActiveCategory;

        private bool IsSaleItemAllowed(ShopSaleItemDefinition item) =>
            item.Id >= 0 && (ShouldUseBaseStockPassThrough() || item.Category == ActiveCategory);

        private StockCategory ResolveActiveStockCategory()
        {
            if (owner.HasRetailSpecialization) return owner.RetailCategory;
            return TryGetSingleBaseStockCategory(out StockCategory category) ? category : owner.RetailCategory;
        }

        private bool TryGetSingleBaseStockCategory(out StockCategory category)
        {
            category = StockCategory.General;
            if (!HasBaseStock()) return false;
            bool found = false;
            foreach (ShopStockSeed seed in baseStock.Stocks)
            {
                if (!found) { category = seed.Item.Category; found = true; }
                else if (seed.Item.Category != category) return false;
            }
            return found;
        }

        private bool ShouldUseBaseStockPassThrough() =>
            !owner.HasRetailSpecialization && !TryGetSingleBaseStockCategory(out _);

        private int GetMaxInternalStock(StockCategory category) =>
            GetConfiguredInternalStockCapacity() + owner.GetRoomStorageCapacity(category);

        private int GetConfiguredInternalStockCapacity() =>
            owner.ConfiguredInternalStockCapacity > 0
                ? owner.ConfiguredInternalStockCapacity
                : GetConfiguredStockCapacity();

        private int GetConfiguredStockCapacity() =>
            HasBaseStock() ? baseStock.Stocks.Sum(seed => Math.Max(0, seed.Amount)) : 0;

        private bool HasBaseStock() => baseStock?.Stocks != null && baseStock.Stocks.Count > 0;

        private void EnsureCatalog()
        {
            if (catalog == null) throw new InvalidOperationException(
                $"{nameof(ShopInventoryRuntime)} for '{owner.DisplayName}' requires catalog injection.");
        }

        public static int GetRemainingStockAfterSelection(
            RemainStock stock,
            IReadOnlyDictionary<int, int> selectedCounts)
        {
            if (stock == null) return 0;
            int selected = 0;
            selectedCounts?.TryGetValue(stock.id, out selected);
            return stock.stock - selected;
        }
    }
}
