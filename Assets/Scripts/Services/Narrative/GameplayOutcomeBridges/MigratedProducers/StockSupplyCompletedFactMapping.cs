using System;

internal static class StockSupplyCompletedFactMapping
{
    internal static MigratedDomainFactCapture Capture(
        StockSupplyCompletedFact fact)
    {
        StockSupplyResult result = fact.Result;
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.StockSupplyResult,
            fact.OperationId,
            fact.AbsoluteDay,
            ResolveStatus(result),
            key => CreateDetails(fact.ItemId, result, key));
    }

    private static GameplayOutcomeStatus ResolveStatus(
        StockSupplyResult result)
    {
        if (result.success)
            return GameplayOutcomeStatus.Succeeded;

        return result.deliveredAmount > 0
            ? GameplayOutcomeStatus.PartiallySucceeded
            : GameplayOutcomeStatus.Failed;
    }

    private static MigratedProducerOutcomeDetails CreateDetails(
        string itemId,
        StockSupplyResult result,
        GameplayResultKey key) => new(
        new[]
        {
            new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.OperationKind,
                key.OperationId.Value,
                string.IsNullOrWhiteSpace(result.sourceLabel)
                    ? result.category.ToString()
                    : result.sourceLabel,
                MigratedProducerOutcomeIds.OperationRole)
        },
        Array.Empty<GameplayOutcomeMetric>(),
        Array.Empty<GameplayOutcomeFact>(),
        CreateSummary(itemId, result));

    private static string CreateSummary(string itemId, StockSupplyResult result) =>
        "재고 납품 결과: success=" + (result.success ? "true" : "false")
        + "; item=" + itemId
        + "; category=" + result.category
        + "; requested=" + result.requestedAmount
        + "; cost=" + result.cost
        + "; delivered=" + result.deliveredAmount
        + "; spawned=" + result.spawnedStackQuantity
        + "; spawnedStacks=" + result.spawnedStackIds
        + "; paid=" + result.paidAmount
        + "; refunded=" + result.refundedAmount
        + "; netCost=" + result.netCost
        + "; balance=" + result.balanceBefore + "->" + result.balanceAfter
        + "; paymentTransaction=" + result.paymentTransactionSource
        + "; refundTransaction=" + result.refundTransactionSource
        + "; exception=" + result.exceptionType
        + "; source=" + result.sourceLabel
        + "; reason=" + result.reason;
}
