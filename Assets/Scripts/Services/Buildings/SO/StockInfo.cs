using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Building/StockInfo", order = 0)]
public class StockInfo : DataScriptableObject
{
    public int shopId;
    public List<Tuple<SaleItem,int>> stocks;
    public float multifly;
}

[Serializable]
public class WarehouseInventory
{
    private readonly Dictionary<StockCategory, int> stockByCategory = new Dictionary<StockCategory, int>();
    [SerializeField] private int maxCapacity;
    [SerializeField] private bool restrictCategory;
    [SerializeField] private StockCategory acceptedCategory;

    public int TotalStock => stockByCategory.Values.Sum();
    public int MaxCapacity => maxCapacity > 0 ? maxCapacity : int.MaxValue;
    public int RemainingCapacity => Mathf.Max(0, MaxCapacity - TotalStock);
    public bool HasCapacityLimit => maxCapacity > 0;
    public bool RestrictsCategory => restrictCategory;
    public StockCategory AcceptedCategory => acceptedCategory;

    public WarehouseInventory()
    {
    }

    public WarehouseInventory(int maxCapacity)
    {
        this.maxCapacity = Mathf.Max(0, maxCapacity);
    }

    public WarehouseInventory(int maxCapacity, StockCategory acceptedCategory, bool restrictCategory)
    {
        this.maxCapacity = Mathf.Max(0, maxCapacity);
        this.acceptedCategory = acceptedCategory;
        this.restrictCategory = restrictCategory;
    }

    public static WarehouseInventory CreateSeeded(int totalStock)
    {
        int capacity = Mathf.Max(0, totalStock);
        WarehouseInventory inventory = new WarehouseInventory(capacity);
        StockCategoryDefinition[] definitions = StockCategoryCatalog.All
            .Where((definition) => definition.SeedWeight > 0f)
            .ToArray();
        float totalWeight = definitions.Sum((definition) => definition.SeedWeight);
        int remaining = capacity;
        for (int i = 0; i < definitions.Length; i++)
        {
            StockCategoryDefinition definition = definitions[i];
            int amount = i == definitions.Length - 1
                ? remaining
                : Mathf.Clamp(
                    Mathf.RoundToInt(capacity * definition.SeedWeight / Mathf.Max(0.0001f, totalWeight)),
                    0,
                    remaining);
            inventory.AddStock(definition.Category, amount);
            remaining -= amount;
        }

        return inventory;
    }

    public IReadOnlyList<KeyValuePair<StockCategory, int>> EnumerateStock()
    {
        return stockByCategory
            .Where((pair) => pair.Value > 0)
            .OrderBy((pair) => StockCategoryCatalog.TryGet(pair.Key, out StockCategoryDefinition definition)
                ? definition.SortOrder
                : int.MaxValue)
            .ThenBy((pair) => Convert.ToInt32(pair.Key, CultureInfo.InvariantCulture))
            .ToArray();
    }

    public int GetStock(StockCategory category)
    {
        return stockByCategory.TryGetValue(category, out int amount)
            ? amount
            : 0;
    }

    public bool HasStock(StockCategory category)
    {
        return GetStock(category) > 0;
    }

    public bool CanStore(int amount)
    {
        return RemainingCapacity >= Mathf.Max(0, amount);
    }

    public bool CanStore(StockCategory category, int amount)
    {
        return Accepts(category) && CanStore(amount);
    }

    public bool Accepts(StockCategory category)
    {
        return !restrictCategory || category == acceptedCategory;
    }

    public int AddStock(StockCategory category, int amount)
    {
        if (!Accepts(category)) return 0;
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount == 0) return 0;

        stockByCategory[category] = GetStock(category) + safeAmount;
        return safeAmount;
    }

    public int Deposit(StockCategory category, int amount)
    {
        if (!Accepts(category)) return 0;
        int safeAmount = Mathf.Max(0, amount);
        int deposited = Mathf.Min(safeAmount, RemainingCapacity);
        if (deposited <= 0) return 0;

        AddStock(category, deposited);
        return deposited;
    }

    public int Withdraw(StockCategory category, int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount == 0) return 0;

        int current = GetStock(category);
        int withdrawn = Mathf.Min(current, safeAmount);
        stockByCategory[category] = current - withdrawn;
        return withdrawn;
    }

    public WarehouseInventorySnapshot CreateSnapshot()
    {
        return new WarehouseInventorySnapshot
        {
            version = WarehouseInventorySnapshot.CurrentVersion,
            maxCapacity = maxCapacity,
            restrictCategory = restrictCategory,
            acceptedCategoryId = StockCategoryPersistenceId.ToId(acceptedCategory),
            stocks = EnumerateStock()
                .Select(pair => new StockAmountSnapshot
                {
                    categoryId = StockCategoryPersistenceId.ToId(pair.Key),
                    amount = pair.Value
                })
                .ToList()
        };
    }

    public void ApplySnapshot(WarehouseInventorySnapshot snapshot)
    {
        if (!TryApplySnapshot(snapshot, out string error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public bool TryApplySnapshot(WarehouseInventorySnapshot snapshot, out string error)
    {
        if (snapshot == null)
        {
            error = "Warehouse inventory snapshot is null.";
            return false;
        }

        if (snapshot.version != WarehouseInventorySnapshot.CurrentVersion)
        {
            error = $"Unsupported warehouse inventory snapshot version {snapshot.version}.";
            return false;
        }

        if (!StockCategoryPersistenceId.TryParse(snapshot.acceptedCategoryId, out StockCategory restoredAcceptedCategory))
        {
            error = $"Unknown accepted stock category id '{snapshot.acceptedCategoryId}'.";
            return false;
        }

        Dictionary<StockCategory, int> restoredStock = new Dictionary<StockCategory, int>();
        foreach (StockAmountSnapshot entry in snapshot.stocks ?? new List<StockAmountSnapshot>())
        {
            if (entry == null)
            {
                error = "Warehouse inventory snapshot contains a null stock entry.";
                return false;
            }

            if (!StockCategoryPersistenceId.TryParse(entry.categoryId, out StockCategory category))
            {
                error = $"Unknown stock category id '{entry.categoryId}'.";
                return false;
            }

            int amount = Mathf.Max(0, entry.amount);
            long combined = (long)(restoredStock.TryGetValue(category, out int current) ? current : 0) + amount;
            restoredStock[category] = combined >= int.MaxValue ? int.MaxValue : (int)combined;
        }

        stockByCategory.Clear();
        maxCapacity = Mathf.Max(0, snapshot.maxCapacity);
        restrictCategory = snapshot.restrictCategory;
        acceptedCategory = restoredAcceptedCategory;
        foreach (KeyValuePair<StockCategory, int> pair in restoredStock)
        {
            stockByCategory[pair.Key] = pair.Value;
        }

        error = string.Empty;
        return true;
    }
}

[Serializable]
public sealed class WarehouseInventorySnapshot
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public int maxCapacity;
    public bool restrictCategory;
    public string acceptedCategoryId = StockCategoryPersistenceId.ToId(StockCategory.General);
    public List<StockAmountSnapshot> stocks = new List<StockAmountSnapshot>();

    public static WarehouseInventorySnapshot FromLegacy(WarehouseInventorySnapshotV1 legacy)
    {
        legacy ??= new WarehouseInventorySnapshotV1();
        return new WarehouseInventorySnapshot
        {
            version = CurrentVersion,
            maxCapacity = legacy.maxCapacity,
            restrictCategory = legacy.restrictCategory,
            acceptedCategoryId = StockCategoryPersistenceId.ToId(legacy.acceptedCategory),
            stocks = new List<StockAmountSnapshot>
            {
                StockAmountSnapshot.From(StockCategory.Food, legacy.food),
                StockAmountSnapshot.From(StockCategory.General, legacy.general),
                StockAmountSnapshot.From(StockCategory.Weapon, legacy.weapon),
                StockAmountSnapshot.From(StockCategory.Mana, legacy.mana)
            }.Where(entry => entry.amount > 0).ToList()
        };
    }
}

[Serializable]
public sealed class WarehouseInventorySnapshotV1
{
    public int maxCapacity;
    public bool restrictCategory;
    public StockCategory acceptedCategory;
    public int food;
    public int general;
    public int weapon;
    public int mana;
}

[Serializable]
public sealed class StockAmountSnapshot
{
    public string categoryId;
    public int amount;

    public static StockAmountSnapshot From(StockCategory category, int amount)
    {
        return new StockAmountSnapshot
        {
            categoryId = StockCategoryPersistenceId.ToId(category),
            amount = Mathf.Max(0, amount)
        };
    }
}

public static class StockCategoryPersistenceId
{
    private const string Prefix = "stock:";

    public static string ToId(StockCategory category)
    {
        if (StockCategoryCatalog.TryGet(category, out StockCategoryDefinition definition))
        {
            return definition.Id;
        }

        return Prefix + Convert.ToInt32(category, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string categoryId, out StockCategory category)
    {
        string value = categoryId?.Trim();
        if (StockCategoryCatalog.TryGet(value, out StockCategoryDefinition definition))
        {
            category = definition.Category;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(value)
            && value.StartsWith(Prefix, StringComparison.Ordinal)
            && int.TryParse(value.Substring(Prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericId))
        {
            category = (StockCategory)numericId;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse(value, true, out StockCategory namedCategory))
        {
            category = namedCategory;
            return true;
        }

        category = default;
        return false;
    }
}

[Serializable]
public struct StockDeliveryOffer
{
    public StockCategory category;
    public int amount;
    public int cost;
    public string sourceLabel;

    public StockDeliveryOffer(StockCategory category, int amount, int cost, string sourceLabel)
    {
        this.category = category;
        this.amount = Mathf.Max(0, amount);
        this.cost = Mathf.Max(0, cost);
        this.sourceLabel = sourceLabel;
    }

    public bool IsValid => amount > 0 && cost >= 0;
}

[Serializable]
public struct StockProductionRule
{
    public StockCategory category;
    public int amount;
    public string sourceLabel;

    public StockProductionRule(StockCategory category, int amount, string sourceLabel)
    {
        this.category = category;
        this.amount = Mathf.Max(0, amount);
        this.sourceLabel = sourceLabel;
    }
}

[Serializable]
public struct StockSupplyResult
{
    public bool success;
    public StockCategory category;
    public int requestedAmount;
    public int deliveredAmount;
    public int cost;
    public string sourceLabel;
    public string reason;

    public StockSupplyResult(
        bool success,
        StockCategory category,
        int requestedAmount,
        int deliveredAmount,
        int cost,
        string sourceLabel,
        string reason)
    {
        this.success = success;
        this.category = category;
        this.requestedAmount = Mathf.Max(0, requestedAmount);
        this.deliveredAmount = Mathf.Max(0, deliveredAmount);
        this.cost = Mathf.Max(0, cost);
        this.sourceLabel = sourceLabel;
        this.reason = reason;
    }

    public string ToSummaryText()
    {
        string label = string.IsNullOrWhiteSpace(sourceLabel) ? "재고 수급" : sourceLabel;
        if (success)
        {
            string costText = cost > 0 ? $" / 비용 {cost}" : string.Empty;
            return $"{label}: {category} {deliveredAmount}개 입고{costText}";
        }

        return $"{label}: {category} {requestedAmount}개 입고 실패 - {reason}";
    }
}

public struct StockSupplyEvent
{
    public StockSupplyResult result;

    public StockSupplyEvent(StockSupplyResult result)
    {
        this.result = result;
    }
}

public static class StockSupplyService
{
    public static IReadOnlyList<StockDeliveryOffer> CreateDailyDeliveryOffers(
        int day,
        IRunVariableRuntimeReader runVariableReader)
    {
        if (runVariableReader == null)
        {
            throw new ArgumentNullException(nameof(runVariableReader));
        }

        return CreateDailyDeliveryOffers(day, runVariableReader.GetStockCostMultiplier);
    }

    public static IReadOnlyList<StockDeliveryOffer> CreateDailyDeliveryOffers(
        int day,
        Func<StockCategory, float> stockCostMultiplier)
    {
        if (stockCostMultiplier == null)
        {
            throw new ArgumentNullException(nameof(stockCostMultiplier));
        }

        int safeDay = Mathf.Max(1, day);
        int smallGrowth = Mathf.Min(12, safeDay / 3);

        return StockCategoryCatalog.All
            .Where((definition) => definition.DailyBaseAmount > 0)
            .Select((definition) => StockSupplyService.CreateOffer(
                definition.Category,
                definition.GetDailyAmount(smallGrowth),
                definition.DailyUnitCost,
                "운영일 납품",
                stockCostMultiplier))
            .ToList();
    }

    public static bool TryPurchaseDelivery(
        GameData gameData,
        IEnumerable<IWarehouseFacility> warehouses,
        StockDeliveryOffer offer,
        out StockSupplyResult result)
    {
        return TryPurchaseDelivery(
            gameData,
            warehouses,
            null,
            offer,
            out result);
    }

    public static bool TryPurchaseDelivery(
        GameData gameData,
        IEnumerable<IWarehouseFacility> warehouses,
        IWorldItemStackRuntime itemStackRuntime,
        StockDeliveryOffer offer,
        out StockSupplyResult result,
        Action<StockSupplyResult> resultCallback = null)
    {
        if (!offer.IsValid)
        {
            result = Fail(offer.category, offer.amount, offer.cost, offer.sourceLabel, "납품 정보가 올바르지 않습니다");
            resultCallback?.Invoke(result);
            return false;
        }

        if (gameData == null || gameData.holdingMoney == null)
        {
            result = Fail(offer.category, offer.amount, offer.cost, offer.sourceLabel, "자금 데이터가 없습니다");
            resultCallback?.Invoke(result);
            return false;
        }

        if (!DungeonDebugRuntimeRules.ShouldSkipCosts()
            && gameData.holdingMoney.Value < offer.cost)
        {
            result = Fail(offer.category, offer.amount, offer.cost, offer.sourceLabel, "자금 부족");
            resultCallback?.Invoke(result);
            return false;
        }

        if (itemStackRuntime != null
            && itemStackRuntime.SpawnStockAtDropoff(
                offer.category,
                offer.amount,
                offer.sourceLabel,
                out int spawned))
        {
            if (!DungeonDebugRuntimeRules.ShouldSkipCosts())
            {
                gameData.holdingMoney.Value -= offer.cost;
            }
            bool stackSuccess = spawned == offer.amount;
            result = new StockSupplyResult(
                stackSuccess,
                offer.category,
                offer.amount,
                spawned,
                offer.cost,
                offer.sourceLabel,
                stackSuccess ? string.Empty : "physical delivery interrupted");
            resultCallback?.Invoke(result);
            return stackSuccess;
        }

        if (!CanDepositAll(warehouses, offer.amount))
        {
            result = Fail(offer.category, offer.amount, offer.cost, offer.sourceLabel, "창고 공간 부족");
            resultCallback?.Invoke(result);
            return false;
        }

        if (!DungeonDebugRuntimeRules.ShouldSkipCosts())
        {
            gameData.holdingMoney.Value -= offer.cost;
        }
        int delivered = DepositToWarehouses(warehouses, offer.category, offer.amount);
        bool success = delivered == offer.amount;
        result = new StockSupplyResult(
            success,
            offer.category,
            offer.amount,
            delivered,
            offer.cost,
            offer.sourceLabel,
            success ? string.Empty : "입고 중단");
        resultCallback?.Invoke(result);
        return success;
    }

    public static bool GrantReward(
        IEnumerable<IWarehouseFacility> warehouses,
        StockCategory category,
        int amount,
        string sourceLabel,
        out StockSupplyResult result)
    {
        return GrantReward(
            warehouses,
            null,
            category,
            amount,
            sourceLabel,
            out result);
    }

    public static bool GrantReward(
        IEnumerable<IWarehouseFacility> warehouses,
        IWorldItemStackRuntime itemStackRuntime,
        StockCategory category,
        int amount,
        string sourceLabel,
        out StockSupplyResult result,
        Action<StockSupplyResult> resultCallback = null)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
        {
            result = Fail(category, safeAmount, 0, sourceLabel, "보상 수량이 없습니다");
            resultCallback?.Invoke(result);
            return false;
        }

        if (itemStackRuntime != null
            && itemStackRuntime.SpawnStockAtDropoff(
                category,
                safeAmount,
                sourceLabel,
                out int spawned))
        {
            bool stackSuccess = spawned == safeAmount;
            result = new StockSupplyResult(
                stackSuccess,
                category,
                safeAmount,
                spawned,
                0,
                sourceLabel,
                stackSuccess ? string.Empty : "physical reward interrupted");
            resultCallback?.Invoke(result);
            return stackSuccess;
        }

        if (!CanDepositAll(warehouses, safeAmount))
        {
            result = Fail(category, safeAmount, 0, sourceLabel, "창고 공간 부족");
            resultCallback?.Invoke(result);
            return false;
        }

        int delivered = DepositToWarehouses(warehouses, category, safeAmount);
        bool success = delivered == safeAmount;
        result = new StockSupplyResult(
            success,
            category,
            safeAmount,
            delivered,
            0,
            sourceLabel,
            success ? string.Empty : "입고 중단");
        resultCallback?.Invoke(result);
        return success;
    }

    public static List<StockSupplyResult> RunInternalProduction(
        IEnumerable<IWarehouseFacility> warehouses,
        IEnumerable<StockProductionRule> productionRules,
        Action<StockSupplyResult> resultCallback = null)
    {
        List<StockSupplyResult> results = new List<StockSupplyResult>();
        if (productionRules == null) return results;

        foreach (StockProductionRule rule in productionRules)
        {
            GrantReward(
                warehouses,
                null,
                rule.category,
                rule.amount,
                rule.sourceLabel,
                out StockSupplyResult result,
                resultCallback);
            results.Add(result);
        }

        return results;
    }

    public static int GetRemainingCapacity(IEnumerable<IWarehouseFacility> warehouses)
    {
        long capacity = 0;
        foreach (IWarehouseFacility warehouse in GetValidWarehouses(warehouses))
        {
            capacity += warehouse.Inventory.RemainingCapacity;
            if (capacity >= int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return Mathf.Max(0, (int)capacity);
    }

    private static StockDeliveryOffer CreateOffer(
        StockCategory category,
        int amount,
        int unitCost,
        string sourceLabel,
        Func<StockCategory, float> stockCostMultiplier)
    {
        int safeAmount = Mathf.Max(0, amount);
        float costMultiplier = stockCostMultiplier(category);
        int cost = Mathf.RoundToInt(safeAmount * Mathf.Max(0, unitCost) * Mathf.Max(0.05f, costMultiplier));
        return new StockDeliveryOffer(category, safeAmount, cost, sourceLabel);
    }

    private static bool CanDepositAll(IEnumerable<IWarehouseFacility> warehouses, int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        return safeAmount == 0 || GetRemainingCapacity(warehouses) >= safeAmount;
    }

    private static int DepositToWarehouses(IEnumerable<IWarehouseFacility> warehouses, StockCategory category, int amount)
    {
        int remaining = Mathf.Max(0, amount);
        int delivered = 0;

        foreach (IWarehouseFacility warehouse in GetValidWarehouses(warehouses))
        {
            if (remaining <= 0) break;

            int deposited = warehouse.Inventory.Deposit(category, remaining);
            delivered += deposited;
            remaining -= deposited;
        }

        return delivered;
    }

    private static IEnumerable<IWarehouseFacility> GetValidWarehouses(IEnumerable<IWarehouseFacility> warehouses)
    {
        return warehouses?
            .Where((warehouse) => warehouse != null && warehouse.HasWarehouseInventory && warehouse.Inventory != null)
            ?? Enumerable.Empty<IWarehouseFacility>();
    }

    private static StockSupplyResult Fail(
        StockCategory category,
        int requestedAmount,
        int cost,
        string sourceLabel,
        string reason)
    {
        return new StockSupplyResult(false, category, requestedAmount, 0, cost, sourceLabel, reason);
    }
}
