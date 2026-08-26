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
            string itemDefinitionId,
            long unitMassGrams,
            bool requiresUniqueInstance,
            StockCategory category,
            int cost,
            OnBuyItemSO[] onBuy)
        {
            Id = id;
            Name = name ?? string.Empty;
            ItemDefinitionId = itemDefinitionId ?? string.Empty;
            UnitMassGrams = unitMassGrams;
            RequiresUniqueInstance = requiresUniqueInstance;
            Category = category;
            Cost = cost;
            OnBuy = onBuy ?? Array.Empty<OnBuyItemSO>();
        }

        public int Id { get; }
        public string Name { get; }
        public string ItemDefinitionId { get; }
        public long UnitMassGrams { get; }
        public bool RequiresUniqueInstance { get; }
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
        int GetStock(string itemDefinitionId);
    }

    public interface IBuildingShopInventoryOwnerPort
    {
        bool IsConfigured { get; }
        int ShopId { get; }
        string PersistentOwnerId { get; }
        string DisplayName { get; }
        int ConfiguredInternalStockCapacity { get; }
        bool HasRetailSpecialization { get; }
        StockCategory RetailCategory { get; }
        int GetRoomStorageCapacity(StockCategory category);
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
        private readonly List<RetailStockLotSnapshot> lots = new();
        private readonly HashSet<int> activatedAuthoredSaleItemIds = new();
        private readonly List<Stock> purchasableStockBuffer = new();
        private IBuildingShopStockCatalogPort catalog;
        private ShopStockDefinition baseStock;
        private bool initialized;

        public ShopInventoryRuntime(IBuildingShopInventoryOwnerPort owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public IReadOnlyList<RemainStock> Stocks => stocks;
        public IReadOnlyList<RetailStockLotSnapshot> Lots => lots;
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
            lots.Clear();
            activatedAuthoredSaleItemIds.Clear();
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

        public bool IsSaleItemAccepted(int saleItemId)
        {
            EnsureInitialized();
            return TryGetBaseStockSeed(saleItemId, out ShopStockSeed seed)
                && IsSaleItemAllowed(seed.Item);
        }

        public IReadOnlyList<ShopStockSeed> CapturePendingAcceptedAuthoredStock()
        {
            EnsureInitialized();
            return (baseStock?.Stocks ?? Array.Empty<ShopStockSeed>())
                .Where(seed => seed.Amount > 0
                    && IsSaleItemAllowed(seed.Item)
                    && !activatedAuthoredSaleItemIds.Contains(seed.Item.Id))
                .OrderBy(seed => seed.Item.Id)
                .ToArray();
        }

        public bool TryActivateAuthoredGenericStock(
            ShopStockSeed seed,
            out string failureReason)
        {
            failureReason = string.Empty;
            EnsureInitialized();
            if (seed.Amount <= 0
                || seed.Item.RequiresUniqueInstance
                || !IsSaleItemAllowed(seed.Item)
                || activatedAuthoredSaleItemIds.Contains(seed.Item.Id))
            {
                failureReason = "retail-authored-generic-source-not-pending";
                return false;
            }

            int accepted = Math.Min(seed.Amount, MissingStock);
            if (accepted <= 0)
            {
                failureReason = "retail-authored-generic-capacity-unavailable";
                return false;
            }
            AddAuthoredGenericLot(
                seed.Item,
                accepted,
                $"retail-source:authored-seed:{owner.PersistentOwnerId}:{seed.Item.Id}");
            activatedAuthoredSaleItemIds.Add(seed.Item.Id);
            return true;
        }

        public void MarkAuthoredSaleItemActivated(int saleItemId)
        {
            EnsureInitialized();
            if (!TryGetBaseStockSeed(saleItemId, out ShopStockSeed seed)
                || !IsSaleItemAllowed(seed.Item))
            {
                throw new InvalidOperationException(
                    $"Cannot activate rejected authored sale item '{saleItemId}'.");
            }
            activatedAuthoredSaleItemIds.Add(saleItemId);
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
                    candidate.HasInventory
                    && candidate.GetStock(seed.Item.ItemDefinitionId) > 0);
                if (warehouse == null) continue;
                int amount = Math.Min(
                    targetAmount,
                    warehouse.GetStock(seed.Item.ItemDefinitionId));
                if (amount <= 0) continue;
                source = new ShopRestockSource(warehouse, seed.Item, amount);
                return true;
            }

            failureReason = "창고 재고 부족";
            return false;
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
                        && warehouse.GetStock(seed.Item.ItemDefinitionId) > 0))
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
                schemaVersion = ShopStockStateSnapshot.CurrentSchemaVersion,
                activatedAuthoredSaleItemIds = activatedAuthoredSaleItemIds
                    .OrderBy(id => id)
                    .ToList(),
                lots = lots.Select(lot => lot.Clone()).ToList()
            };
        }

        public void ApplySnapshot(ShopStockStateSnapshot snapshot)
        {
            EnsureInitialized();
            if (snapshot == null
                || snapshot.schemaVersion != ShopStockStateSnapshot.CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Shop stock snapshot schema must be {ShopStockStateSnapshot.CurrentSchemaVersion}.");
            }

            RetailStockLotSnapshot[] restored = (snapshot.lots
                    ?? new List<RetailStockLotSnapshot>())
                .Where(lot => lot != null)
                .Select(lot => lot.Clone())
                .ToArray();
            int[] restoredActivated = (snapshot.activatedAuthoredSaleItemIds
                    ?? new List<int>())
                .ToArray();
            if (restoredActivated.Distinct().Count() != restoredActivated.Length
                || restoredActivated.Any(id =>
                    !TryGetBaseStockSeed(id, out _)))
            {
                throw new InvalidOperationException(
                    "Shop stock snapshot contains invalid authored-source activation authority.");
            }
            foreach (RetailStockLotSnapshot lot in restored)
            {
                ValidateLotOrThrow(lot);
                if (!TryGetBaseStockSeed(lot.saleItemId, out ShopStockSeed seed)
                    || !MatchesAuthoredPhysicalIdentity(seed.Item, lot))
                {
                    throw new InvalidOperationException(
                        $"Retail lot '{lot.sourceOperationId}' does not match authored sale item '{lot.saleItemId}'.");
                }
            }
            if (restored.Select(lot => lot.sourceOperationId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() != restored.Length
                || restored.Where(lot => !string.IsNullOrEmpty(lot.itemInstanceId))
                    .Select(lot => lot.itemInstanceId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() != restored.Count(lot =>
                        !string.IsNullOrEmpty(lot.itemInstanceId)))
            {
                throw new InvalidOperationException(
                    "Shop stock snapshot contains duplicated operation or unique-instance authority.");
            }

            lots.Clear();
            lots.AddRange(restored);
            activatedAuthoredSaleItemIds.Clear();
            foreach (int saleItemId in restoredActivated)
            {
                activatedAuthoredSaleItemIds.Add(saleItemId);
            }
            RebuildStockProjection();
            owner.NotifyStockChanged();
        }

        public bool TryReceiveExactLot(
            RetailStockLotSnapshot incoming,
            int requestedAmount,
            out int received,
            out string resultMessage)
        {
            return TryReceiveExactLots(
                new[] { incoming },
                requestedAmount,
                out received,
                out resultMessage);
        }

        public bool TryReceiveExactLots(
            IReadOnlyList<RetailStockLotSnapshot> incoming,
            int requestedAmount,
            out int received,
            out string resultMessage)
        {
            received = 0;
            resultMessage = string.Empty;
            EnsureInitialized();
            RetailStockLotSnapshot[] candidates = (incoming
                    ?? Array.Empty<RetailStockLotSnapshot>())
                .Where(lot => lot != null)
                .Select(lot => lot.Clone())
                .ToArray();
            try
            {
                foreach (RetailStockLotSnapshot candidate in candidates)
                {
                    ValidateLotOrThrow(candidate);
                }
            }
            catch (Exception exception)
            {
                resultMessage = $"invalid-retail-lot:{exception.Message}";
                return false;
            }
            if (candidates.Length == 0)
            {
                resultMessage = "retail-lot-empty";
                return false;
            }

            foreach (RetailStockLotSnapshot candidate in candidates)
            {
                if (!TryGetBaseStockSeed(candidate.saleItemId, out ShopStockSeed seed)
                    || !IsSaleItemAllowed(seed.Item)
                    || !MatchesAuthoredPhysicalIdentity(seed.Item, candidate))
                {
                    resultMessage = "retail-lot-definition-mismatch";
                    return false;
                }
            }
            int totalQuantity = checked(candidates.Sum(candidate => candidate.quantity));
            if (totalQuantity > MissingStock)
            {
                resultMessage = "retail-lot-capacity-changed";
                return false;
            }
            HashSet<string> operationIds = new HashSet<string>(
                lots.Select(lot => lot.sourceOperationId),
                StringComparer.Ordinal);
            if (candidates.Any(candidate => !operationIds.Add(candidate.sourceOperationId)))
            {
                resultMessage = "retail-lot-operation-duplicate";
                return false;
            }

            lots.AddRange(candidates);
            RebuildStockProjection();
            received = totalQuantity;
            resultMessage = $"{received}개 보충";
            owner.PublishRestock(Math.Max(0, requestedAmount), received, resultMessage);
            owner.NotifyStockChanged();
            return true;
        }

        public bool TryTakeExactLot(
            int saleItemId,
            out RetailStockLotSnapshot taken,
            out string failureReason)
        {
            EnsureInitialized();
            taken = null;
            failureReason = string.Empty;
            RetailStockLotSnapshot source = lots
                .Where(lot => lot != null
                    && lot.saleItemId == saleItemId
                    && lot.quantity > 0)
                .OrderBy(lot => lot.sourceOperationId, StringComparer.Ordinal)
                .ThenBy(lot => lot.itemInstanceId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (source == null)
            {
                failureReason = "retail-lot-unavailable";
                return false;
            }

            taken = source.Clone();
            taken.quantity = 1;
            source.quantity--;
            if (source.quantity <= 0)
            {
                lots.Remove(source);
            }
            RebuildStockProjection();
            owner.NotifyStockChanged();
            return true;
        }

        public bool TryRestoreTakenExactLot(
            RetailStockLotSnapshot taken,
            out string failureReason)
        {
            EnsureInitialized();
            failureReason = string.Empty;
            RetailStockLotSnapshot restored = taken?.Clone();
            try
            {
                ValidateLotOrThrow(restored);
            }
            catch (Exception exception)
            {
                failureReason = $"retail-lot-restore-invalid:{exception.Message}";
                return false;
            }
            if (restored.quantity != 1
                || !TryGetBaseStockSeed(
                    restored.saleItemId,
                    out ShopStockSeed seed)
                || !string.Equals(
                    seed.Item.ItemDefinitionId,
                    restored.itemDefinitionId,
                    StringComparison.Ordinal))
            {
                failureReason = "retail-lot-restore-definition-mismatch";
                return false;
            }
            if (MissingStock <= 0)
            {
                failureReason = "retail-lot-restore-capacity-changed";
                return false;
            }

            RetailStockLotSnapshot existing = lots.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.sourceOperationId,
                    restored.sourceOperationId,
                    StringComparison.Ordinal));
            if (existing != null)
            {
                if (!CanMergeExactLot(existing, restored))
                {
                    failureReason = "retail-lot-restore-operation-conflict";
                    return false;
                }
                existing.quantity = checked(existing.quantity + 1);
            }
            else
            {
                lots.Add(restored);
            }

            RebuildStockProjection();
            owner.NotifyStockChanged();
            return true;
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
            lots.Clear();
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
                AddAuthoredGenericLot(
                    seed.Item,
                    initial,
                    $"retail-source:authored-seed:{owner.PersistentOwnerId}:{seed.Item.Id}");
                if (!seed.Item.RequiresUniqueInstance)
                {
                    activatedAuthoredSaleItemIds.Add(seed.Item.Id);
                }
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

        private void AddAuthoredGenericLot(
            ShopSaleItemDefinition item,
            int amount,
            string sourceOperationId)
        {
            if (amount <= 0)
            {
                return;
            }
            if (item.RequiresUniqueInstance)
            {
                // A count-only authored seed cannot invent the instance and
                // component authority required by a unique physical item.
                // Unique retail goods enter only through an exact physical
                // restock transfer.
                return;
            }

            RetailStockLotSnapshot lot = new RetailStockLotSnapshot
            {
                saleItemId = item.Id,
                itemDefinitionId = item.ItemDefinitionId,
                quantity = amount,
                unitMassGrams = item.UnitMassGrams,
                sourceOperationId = sourceOperationId
            };
            ValidateLotOrThrow(lot);
            RetailStockLotSnapshot existing = lots.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.sourceOperationId,
                    sourceOperationId,
                    StringComparison.Ordinal));
            if (existing == null)
            {
                lots.Add(lot);
            }
            else
            {
                existing.quantity = checked(existing.quantity + amount);
            }
            RebuildStockProjection();
            owner.NotifyStockChanged();
        }

        private void RebuildStockProjection()
        {
            stocks.Clear();
            foreach (IGrouping<int, RetailStockLotSnapshot> group in lots
                .Where(lot => lot != null && lot.quantity > 0)
                .GroupBy(lot => lot.saleItemId)
                .OrderBy(group => group.Key))
            {
                if (!TryGetBaseStockSeed(group.Key, out ShopStockSeed seed))
                {
                    throw new InvalidOperationException(
                        $"Retail lot references unknown sale item '{group.Key}'.");
                }
                stocks.Add(CreateRemainStock(
                    seed.Item,
                    checked(group.Sum(lot => lot.quantity))));
            }
        }

        private static void ValidateLotOrThrow(RetailStockLotSnapshot lot)
        {
            if (lot == null
                || lot.saleItemId < 0
                || string.IsNullOrWhiteSpace(lot.itemDefinitionId)
                || !string.Equals(
                    lot.itemDefinitionId,
                    lot.itemDefinitionId.Trim(),
                    StringComparison.Ordinal)
                || lot.quantity <= 0
                || lot.unitMassGrams <= 0L
                || string.IsNullOrWhiteSpace(lot.sourceOperationId)
                || !string.Equals(
                    lot.sourceOperationId,
                    lot.sourceOperationId.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Retail lot requires canonical identity, positive quantity/mass, and source operation.");
            }
            if (!string.IsNullOrEmpty(lot.itemInstanceId) && lot.quantity != 1)
            {
                throw new InvalidOperationException(
                    "A unique retail lot must contain exactly one item instance.");
            }
            if (!string.IsNullOrEmpty(lot.sourceStackId)
                && !string.Equals(
                    lot.sourceStackId,
                    lot.sourceStackId.Trim(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Retail source stack ID must already be canonical.");
            }
            if ((lot.components?.Count ?? 0) > 0
                && string.IsNullOrWhiteSpace(lot.componentFingerprint))
            {
                throw new InvalidOperationException(
                    "Stateful retail lots require a component fingerprint.");
            }
        }

        private static bool CanMergeExactLot(
            RetailStockLotSnapshot existing,
            RetailStockLotSnapshot incoming)
        {
            if (existing == null || incoming == null)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(existing.itemInstanceId)
                || !string.IsNullOrEmpty(incoming.itemInstanceId))
            {
                return false;
            }
            return existing.saleItemId == incoming.saleItemId
                && existing.unitMassGrams == incoming.unitMassGrams
                && string.Equals(
                    existing.itemDefinitionId,
                    incoming.itemDefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    existing.sourceStackId,
                    incoming.sourceStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    existing.componentFingerprint,
                    incoming.componentFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    existing.sourceOperationId,
                    incoming.sourceOperationId,
                    StringComparison.Ordinal);
        }

        private static bool MatchesAuthoredPhysicalIdentity(
            ShopSaleItemDefinition authored,
            RetailStockLotSnapshot lot)
        {
            if (lot == null
                || authored.UnitMassGrams != lot.unitMassGrams
                || !string.Equals(
                    authored.ItemDefinitionId,
                    lot.itemDefinitionId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            bool hasUniqueInstance = !string.IsNullOrEmpty(lot.itemInstanceId);
            return authored.RequiresUniqueInstance == hasUniqueInstance;
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
