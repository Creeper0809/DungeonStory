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
    public string itemId;
    public int amount;
    public int cost;
    public string sourceLabel;

    public StockDeliveryOffer(
        StockCategory category,
        string itemId,
        int amount,
        int cost,
        string sourceLabel)
    {
        this.category = category;
        this.itemId = itemId?.Trim() ?? string.Empty;
        this.amount = Mathf.Max(0, amount);
        this.cost = Mathf.Max(0, cost);
        this.sourceLabel = sourceLabel;
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(itemId) && amount > 0 && cost >= 0;
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
                definition.DeliveryItemId,
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

        bool skipCosts = (debugRules
            ?? throw new ArgumentNullException(nameof(debugRules))).ShouldSkipCosts();
        if (!skipCosts
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

        if (!skipCosts && !TryPayDelivery(money, offer, out result))
        {
            resultCallback?.Invoke(result);
            return false;
        }

        bool spawnCompleted;
        int spawned;
        try
        {
            spawnCompleted = itemStackRuntime.SpawnItemAtDropoff(
                offer.itemId,
                offer.amount,
                offer.sourceLabel,
                out spawned);
        }
        catch
        {
            if (!skipCosts)
            {
                RefundDelivery(money, offer, offer.cost, 0);
            }
            throw;
        }

        int delivered = Mathf.Clamp(spawned, 0, offer.amount);
        int settledCost = skipCosts
            ? 0
            : CalculateSettledDeliveryCost(
                offer.cost,
                offer.amount,
                delivered);
        int refund = skipCosts ? 0 : Mathf.Max(0, offer.cost - settledCost);
        if (refund > 0)
        {
            RefundDelivery(money, offer, refund, delivered);
        }

        bool stackSuccess = spawnCompleted && delivered == offer.amount;
        result = new StockSupplyResult(
            stackSuccess,
            offer.category,
            offer.amount,
            delivered,
            settledCost,
            offer.sourceLabel,
            stackSuccess
                ? string.Empty
                : delivered > 0
                    ? "physical delivery interrupted"
                    : "물리 납품 생성 실패");
        resultCallback?.Invoke(result);
        return stackSuccess;
    }

    public static int CalculateSettledDeliveryCost(
        int totalCost,
        int requestedAmount,
        int deliveredAmount)
    {
        int safeCost = Mathf.Max(0, totalCost);
        int safeRequested = Mathf.Max(0, requestedAmount);
        int safeDelivered = Mathf.Clamp(deliveredAmount, 0, safeRequested);
        if (safeCost == 0 || safeRequested == 0 || safeDelivered == 0)
        {
            return 0;
        }

        int proportional = (int)Math.Ceiling(
            (double)safeCost * safeDelivered / safeRequested);
        return Mathf.Clamp(proportional, 0, safeCost);
    }

    private static void RefundDelivery(
        IGameMoneyAccount money,
        StockDeliveryOffer offer,
        int refund,
        int deliveredAmount)
    {
        if (refund <= 0)
        {
            return;
        }

        money.Add(
            refund,
            new EconomyTransactionContext(
                EconomyTransactionKind.ShopPurchaseRefund,
                "stock-delivery",
                offer.category.ToString(),
                $"{offer.sourceLabel}: {deliveredAmount}/{offer.amount} delivered"));
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
        string itemId,
        int amount,
        float unitCost,
        string sourceLabel,
        Func<StockCategory, float> stockCostMultiplier)
    {
        int safeAmount = Mathf.Max(0, amount);
        float costMultiplier = stockCostMultiplier(category);
        int cost = Mathf.CeilToInt(
            safeAmount * Mathf.Max(0f, unitCost) * Mathf.Max(0.05f, costMultiplier));
        return new StockDeliveryOffer(category, itemId, safeAmount, cost, sourceLabel);
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
