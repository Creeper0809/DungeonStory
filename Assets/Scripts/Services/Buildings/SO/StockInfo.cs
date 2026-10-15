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

public sealed class StockSupplyDeliveryTransaction
{
    private readonly IGameMoneyAccount money;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly DungeonPhysicalItemSaveData physicalBefore;
    private readonly int balanceBefore;
    private readonly EconomyTransactionContext rollbackContext;
    private bool rolledBack;
    private bool durablyCommitted;

    internal StockSupplyDeliveryTransaction(
        IGameMoneyAccount money,
        IWorldItemStackRuntime itemStackRuntime,
        DungeonPhysicalItemSaveData physicalBefore,
        int balanceBefore,
        string transactionIdentity)
    {
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.physicalBefore = physicalBefore
            ?? throw new ArgumentNullException(nameof(physicalBefore));
        this.balanceBefore = Mathf.Max(0, balanceBefore);
        rollbackContext = new EconomyTransactionContext(
            EconomyTransactionKind.DebugAdjustment,
            (transactionIdentity?.Trim() ?? string.Empty) + ":rollback",
            "stock-delivery",
            "stock delivery outcome rollback");
    }

    public bool IsDurablyCommitted => durablyCommitted;

    internal void CaptureSpawnedStackEvidence(
        string itemId,
        ref StockSupplyResult result)
    {
        try
        {
            DungeonPhysicalItemSaveData after = itemStackRuntime.Capture()
                ?? throw new InvalidOperationException(
                    "Stock delivery physical capture returned no snapshot.");
            var beforeByStackId = (physicalBefore.stacks
                    ?? new List<WorldItemStackSaveData>())
                .Where(stack => stack != null)
                .ToDictionary(
                    stack => stack.stackId ?? string.Empty,
                    stack => Mathf.Max(0, stack.quantity),
                    StringComparer.Ordinal);
            var spawned = new List<string>();
            int quantity = 0;
            foreach (WorldItemStackSaveData stack in (after.stacks
                         ?? new List<WorldItemStackSaveData>())
                     .Where(stack => stack != null
                         && string.Equals(
                             stack.itemId,
                             itemId?.Trim() ?? string.Empty,
                             StringComparison.Ordinal))
                     .OrderBy(stack => stack.stackId, StringComparer.Ordinal))
            {
                int beforeQuantity = beforeByStackId.TryGetValue(
                    stack.stackId ?? string.Empty,
                    out int observed)
                        ? observed
                        : 0;
                int delta = Mathf.Max(0, stack.quantity - beforeQuantity);
                if (delta <= 0)
                    continue;
                quantity += delta;
                spawned.Add((stack.stackId ?? string.Empty) + ":" + delta);
            }
            result.AttachSpawnedStackEvidence(quantity, string.Join(",", spawned));
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            RollbackOrThrow();
            throw new InvalidOperationException(
                "Stock delivery could not capture its physical evidence.",
                exception);
        }
    }

    public void RollbackOrThrow()
    {
        if (durablyCommitted)
        {
            throw new InvalidOperationException(
                "A durably committed stock delivery cannot be rolled back.");
        }
        if (rolledBack)
        {
            return;
        }

        try
        {
            itemStackRuntime.Restore(physicalBefore);
            money.SetBalance(balanceBefore, rollbackContext);
            if (money.Balance != balanceBefore
                || !string.Equals(
                    JsonUtility.ToJson(physicalBefore),
                    JsonUtility.ToJson(itemStackRuntime.Capture()),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Stock delivery rollback did not restore the saved money or physical state.");
            }
            rolledBack = true;
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            throw new InvalidOperationException(
                "Stock delivery rollback failed after the delivery result was produced.",
                exception);
        }
    }

    public void MarkDurablyCommitted()
    {
        if (rolledBack)
        {
            throw new InvalidOperationException(
                "A rolled-back stock delivery cannot become durably committed.");
        }
        durablyCommitted = true;
    }
}

public sealed class StockSupplyDeliveryException : Exception
{
    public StockSupplyDeliveryException(
        StockSupplyResult result,
        Exception innerException)
        : base("Stock delivery execution failed after payment settlement.", innerException)
    {
        Result = result;
    }

    public StockSupplyResult Result { get; }
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
        return TryPurchaseDelivery(
            money,
            warehouses,
            itemStackRuntime,
            offer,
            debugRules,
            out result,
            out _,
            resultCallback);
    }

    public static bool TryPurchaseDelivery(
        IGameMoneyAccount money,
        IEnumerable<IWarehouseFacility> warehouses,
        IWorldItemStackRuntime itemStackRuntime,
        StockDeliveryOffer offer,
        IDungeonDebugRuleQuery debugRules,
        out StockSupplyResult result,
        out StockSupplyDeliveryTransaction deliveryTransaction,
        Action<StockSupplyResult> resultCallback = null,
        string transactionIdentity = null)
    {
        deliveryTransaction = null;
        if (!offer.IsValid)
        {
            result = Fail(
                offer.category,
                offer.amount,
                offer.sourceLabel,
                "납품 정보가 올바르지 않습니다");
            resultCallback?.Invoke(result);
            return false;
        }

        if (money == null)
        {
            result = Fail(
                offer.category,
                offer.amount,
                offer.sourceLabel,
                "자금 데이터가 없습니다");
            resultCallback?.Invoke(result);
            return false;
        }

        bool skipCosts = (debugRules
            ?? throw new ArgumentNullException(nameof(debugRules))).ShouldSkipCosts();
        if (!skipCosts
            && !money.CanSpend(offer.cost))
        {
            result = Fail(
                offer.category,
                offer.amount,
                offer.sourceLabel,
                "자금 부족",
                balanceBefore: money.Balance,
                balanceAfter: money.Balance);
            resultCallback?.Invoke(result);
            return false;
        }

        if (itemStackRuntime == null)
        {
            result = Fail(
                offer.category,
                offer.amount,
                offer.sourceLabel,
                "물리 아이템 런타임 없음",
                balanceBefore: money.Balance,
                balanceAfter: money.Balance);
            resultCallback?.Invoke(result);
            return false;
        }

        int beforeBalance = money.Balance;
        string identity = string.IsNullOrWhiteSpace(transactionIdentity)
            ? CreateTransactionIdentity(offer)
            : transactionIdentity.Trim();
        deliveryTransaction = new StockSupplyDeliveryTransaction(
            money,
            itemStackRuntime,
            itemStackRuntime.Capture(),
            beforeBalance,
            identity);
        EconomyTransactionContext paymentContext = CreatePaymentContext(offer, identity);
        EconomyTransactionContext refundContext = CreateRefundContext(offer, identity);
        if (!skipCosts && !TryPayDelivery(money, offer, paymentContext, out string reason))
        {
            result = Fail(
                offer.category,
                offer.amount,
                offer.sourceLabel,
                reason,
                balanceBefore: beforeBalance,
                balanceAfter: money.Balance,
                paymentTransactionSource: paymentContext.sourceId,
                refundTransactionSource: refundContext.sourceId);
            deliveryTransaction.CaptureSpawnedStackEvidence(offer.itemId, ref result);
            resultCallback?.Invoke(result);
            return false;
        }

        int paid = skipCosts
            ? 0
            : Mathf.Max(0, beforeBalance - money.Balance);

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
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            int refunded;
            try
            {
                refunded = skipCosts
                    ? 0
                    : RefundDelivery(money, offer, paid, 0, refundContext);
            }
            catch (Exception refundFailure) when (
                refundFailure is not OutOfMemoryException
                && refundFailure is not StackOverflowException
                && refundFailure is not AccessViolationException)
            {
                deliveryTransaction.RollbackOrThrow();
                throw new InvalidOperationException(
                    "Stock delivery exception refund failed.",
                    refundFailure);
            }
            result = new StockSupplyResult(
                false,
                offer.category,
                offer.amount,
                0,
                Mathf.Max(0, beforeBalance - money.Balance),
                offer.sourceLabel,
                "물리 납품 생성 예외",
                paid,
                refunded,
                Mathf.Max(0, beforeBalance - money.Balance),
                beforeBalance,
                money.Balance,
                paymentContext.sourceId,
                refundContext.sourceId,
                exception.GetType().Name);
            deliveryTransaction.CaptureSpawnedStackEvidence(offer.itemId, ref result);
            result.deliveredAmount = result.spawnedStackQuantity;
            deliveryTransaction.RollbackOrThrow();
            throw new StockSupplyDeliveryException(result, exception);
        }

        int delivered = Mathf.Clamp(spawned, 0, offer.amount);
        int settledCost = skipCosts
            ? 0
            : CalculateSettledDeliveryCost(
                offer.cost,
                offer.amount,
                delivered);
        int requestedRefund = skipCosts
            ? 0
            : Mathf.Max(0, paid - settledCost);
        int refundedAmount = 0;
        if (requestedRefund > 0)
        {
            try
            {
                refundedAmount = RefundDelivery(
                    money,
                    offer,
                    requestedRefund,
                    delivered,
                    refundContext);
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                && exception is not StackOverflowException
                && exception is not AccessViolationException)
            {
                deliveryTransaction.RollbackOrThrow();
                throw new InvalidOperationException(
                    "Stock delivery refund failed after physical spawn.",
                    exception);
            }
        }

        bool stackSuccess = spawnCompleted && delivered == offer.amount;
        int netCost = Mathf.Max(0, beforeBalance - money.Balance);
        bool paymentSettled = skipCosts
            ? netCost == 0
            : paid == offer.cost
                && refundedAmount == requestedRefund
                && netCost == settledCost;
        result = new StockSupplyResult(
            stackSuccess && paymentSettled,
            offer.category,
            offer.amount,
            delivered,
            netCost,
            offer.sourceLabel,
            stackSuccess && paymentSettled
                ? string.Empty
                : !paymentSettled
                    ? "납품 결제 정산 불일치"
                    : delivered > 0
                        ? "physical delivery interrupted"
                        : "물리 납품 생성 실패",
            paid,
            refundedAmount,
            netCost,
            beforeBalance,
            money.Balance,
            paymentContext.sourceId,
            refundContext.sourceId);
        deliveryTransaction.CaptureSpawnedStackEvidence(offer.itemId, ref result);
        resultCallback?.Invoke(result);
        return result.success;
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

    private static int RefundDelivery(
        IGameMoneyAccount money,
        StockDeliveryOffer offer,
        int refund,
        int deliveredAmount,
        EconomyTransactionContext context)
    {
        if (refund <= 0)
        {
            return 0;
        }

        int before = money.Balance;
        money.Add(
            refund,
            context);
        return Mathf.Max(0, money.Balance - before);
    }

    private static bool TryPayDelivery(
        IGameMoneyAccount money,
        StockDeliveryOffer offer,
        EconomyTransactionContext context,
        out string reason)
    {
        if (money.TrySpend(
                offer.cost,
                context,
                out reason))
        {
            return true;
        }

        return false;
    }

    private static EconomyTransactionContext CreatePaymentContext(
        StockDeliveryOffer offer,
        string transactionIdentity) => new(
            EconomyTransactionKind.ShopPurchase,
            transactionIdentity + ":payment",
            offer.category.ToString(),
            offer.sourceLabel);

    private static EconomyTransactionContext CreateRefundContext(
        StockDeliveryOffer offer,
        string transactionIdentity) => new(
            EconomyTransactionKind.ShopPurchaseRefund,
            transactionIdentity + ":refund",
            offer.category.ToString(),
            offer.sourceLabel);

    private static string CreateTransactionIdentity(StockDeliveryOffer offer) =>
        "stock-delivery:category=" + offer.category
        + ":item=" + (offer.itemId?.Trim() ?? string.Empty)
        + ":amount=" + Mathf.Max(0, offer.amount)
        + ":cost=" + Mathf.Max(0, offer.cost)
        + ":source=" + (offer.sourceLabel?.Trim() ?? string.Empty);

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
            result = Fail(category, safeAmount, sourceLabel, "보상 수량이 없습니다");
            resultCallback?.Invoke(result);
            return false;
        }

        if (itemStackRuntime == null)
        {
            result = Fail(
                category,
                safeAmount,
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
        string sourceLabel,
        string reason,
        int balanceBefore = 0,
        int balanceAfter = 0,
        string paymentTransactionSource = "",
        string refundTransactionSource = "")
    {
        return new StockSupplyResult(
            false,
            category,
            requestedAmount,
            0,
            0,
            sourceLabel,
            reason,
            balanceBefore: balanceBefore,
            balanceAfter: balanceAfter,
            paymentTransactionSource: paymentTransactionSource,
            refundTransactionSource: refundTransactionSource);
    }
}
