using System;

internal static class OffenseStockRewardFactMapping
{
    internal static MigratedDomainFactCapture Capture(
        OffenseStockRewardCompletedFact fact)
    {
        OffenseStockRewardReceiptData data = fact.Receipt.Snapshot();
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.StockSupplyResult,
            fact.OperationId,
            fact.AbsoluteDay,
            ResolveStatus(data),
            key => CreateDetails(fact, data, key));
    }

    private static GameplayOutcomeStatus ResolveStatus(
        OffenseStockRewardReceiptData data)
    {
        if (data.success)
            return GameplayOutcomeStatus.Succeeded;

        return data.deliveredAmount > 0
            ? GameplayOutcomeStatus.PartiallySucceeded
            : GameplayOutcomeStatus.Failed;
    }

    private static MigratedProducerOutcomeDetails CreateDetails(
        OffenseStockRewardCompletedFact fact,
        OffenseStockRewardReceiptData data,
        GameplayResultKey key) => new(
        new[]
        {
            new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.OperationKind,
                key.OperationId.Value,
                string.IsNullOrWhiteSpace(data.sourceLabel)
                    ? data.category.ToString()
                    : data.sourceLabel,
                MigratedProducerOutcomeIds.OperationRole)
        },
        Array.Empty<GameplayOutcomeMetric>(),
        Array.Empty<GameplayOutcomeFact>(),
        CreateSummary(fact, data));

    private static string CreateSummary(
        OffenseStockRewardCompletedFact fact,
        OffenseStockRewardReceiptData data) =>
        "원정 재고 결과: success=" + (data.success ? "true" : "false")
        + "; category=" + data.category
        + "; requested=" + data.requestedAmount
        + "; delivered=" + data.deliveredAmount
        + "; source=" + data.sourceLabel
        + "; reason=" + data.reason
        + "; item=" + data.itemId
        + "; target=" + fact.TargetId;
}
