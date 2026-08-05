using System;
using System.Collections;
using System.Collections.Generic;
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
        IRunVariableRuntimeReader runVariableReader,
        IStockCategoryDefinitionCatalog categoryCatalog)
    {
        if (runVariableReader == null)
        {
            throw new ArgumentNullException(nameof(runVariableReader));
        }

        return CreateDailyDeliveryOffers(
            day,
            runVariableReader.GetStockCostMultiplier,
            categoryCatalog);
    }

    public static IReadOnlyList<StockDeliveryOffer> CreateDailyDeliveryOffers(
        int day,
        Func<StockCategory, float> stockCostMultiplier,
        IStockCategoryDefinitionCatalog categoryCatalog)
    {
        if (stockCostMultiplier == null)
        {
            throw new ArgumentNullException(nameof(stockCostMultiplier));
        }

        if (categoryCatalog == null)
        {
            throw new ArgumentNullException(nameof(categoryCatalog));
        }

        int safeDay = Mathf.Max(1, day);
        int smallGrowth = Mathf.Min(12, safeDay / 3);

        return categoryCatalog.All
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
        IGameMoneyAccount money,
        IEnumerable<IWarehouseFacility> warehouses,
        IWorldItemStackRuntime itemStackRuntime,
        StockDeliveryOffer offer,
        IDungeonDebugRuleQuery debugRules,
        out StockSupplyResult result,
        Action<StockSupplyResult> resultCallback = null)
    {
        if (!offer.IsValid)
        {
            result = Fail(offer.category, offer.amount, offer.cost, offer.sourceLabel, "납품 정보가 올바르지 않습니다");
            resultCallback?.Invoke(result);
            return false;
        }

        if (money == null)
        {
            result = Fail(offer.category, offer.amount, offer.cost, offer.sourceLabel, "자금 데이터가 없습니다");
            resultCallback?.Invoke(result);
            return false;
        }

        if (!(debugRules ?? throw new ArgumentNullException(nameof(debugRules))).ShouldSkipCosts()
            && !money.CanSpend(offer.cost))
        {
            result = Fail(offer.category, offer.amount, offer.cost, offer.sourceLabel, "자금 부족");
            resultCallback?.Invoke(result);
            return false;
        }

        if (itemStackRuntime == null)
        {
            result = Fail(
                offer.category,
                offer.amount,
                offer.cost,
                offer.sourceLabel,
                "물리 아이템 런타임 없음");
            resultCallback?.Invoke(result);
            return false;
        }

        if (itemStackRuntime.SpawnStockAtDropoff(
                offer.category,
                offer.amount,
                offer.sourceLabel,
                out int spawned))
        {
            if (!debugRules.ShouldSkipCosts())
            {
                if (!TryPayDelivery(money, offer, out result))
                {
                    resultCallback?.Invoke(result);
                    return false;
                }
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

        result = Fail(
            offer.category,
            offer.amount,
            offer.cost,
            offer.sourceLabel,
            "물리 납품 생성 실패");
        resultCallback?.Invoke(result);
        return false;
    }

    private static bool TryPayDelivery(
        IGameMoneyAccount money,
        StockDeliveryOffer offer,
        out StockSupplyResult failure)
    {
        if (money.TrySpend(
                offer.cost,
                new EconomyTransactionContext(
                    EconomyTransactionKind.ShopPurchase,
                    "stock-delivery",
                    offer.category.ToString(),
                    offer.sourceLabel),
                out string reason))
        {
            failure = default;
            return true;
        }

        failure = Fail(
            offer.category,
            offer.amount,
            offer.cost,
            offer.sourceLabel,
            reason);
        return false;
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

        if (itemStackRuntime == null)
        {
            result = Fail(
                category,
                safeAmount,
                0,
                sourceLabel,
                "물리 아이템 런타임 없음");
            resultCallback?.Invoke(result);
            return false;
        }

        if (itemStackRuntime.SpawnStockAtDropoff(
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

        result = Fail(
            category,
            safeAmount,
            0,
            sourceLabel,
            "물리 보상 생성 실패");
        resultCallback?.Invoke(result);
        return false;
    }

    public static List<StockSupplyResult> RunInternalProduction(
        IEnumerable<IWarehouseFacility> warehouses,
        IWorldItemStackRuntime itemStackRuntime,
        IEnumerable<StockProductionRule> productionRules,
        Action<StockSupplyResult> resultCallback = null)
    {
        List<StockSupplyResult> results = new List<StockSupplyResult>();
        if (productionRules == null) return results;

        foreach (StockProductionRule rule in productionRules)
        {
            GrantReward(
                warehouses,
                itemStackRuntime,
                rule.category,
                rule.amount,
                rule.sourceLabel,
                out StockSupplyResult result,
                resultCallback);
            results.Add(result);
        }

        return results;
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
