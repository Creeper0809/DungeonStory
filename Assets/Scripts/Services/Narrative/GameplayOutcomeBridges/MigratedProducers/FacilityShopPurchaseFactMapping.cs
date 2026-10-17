using System;

public static class FacilityShopPurchaseFactMapping
{
    private static readonly GameplayOutcomeFactId ShopSourceFact =
        new("facility-shop-source");
    private static readonly GameplayOutcomeFactId OfferTypeFact =
        new("facility-shop-offer-type");
    private static readonly GameplayOutcomeFactId OfferDataFact =
        new("facility-shop-offer-data-id");
    private static readonly GameplayOutcomeFactId ItemDefinitionFact =
        new("facility-shop-item-definition-id");
    private static readonly GameplayOutcomeFactId PaymentTransactionFact =
        new("facility-shop-payment-transaction-id");
    private static readonly GameplayOutcomeFactId PaymentSourceFact =
        new("facility-shop-payment-source-id");

    public static MigratedDomainFactCapture Capture(
        FacilityShopPurchaseFact fact)
    {
        FacilityShopPurchaseFactData data = fact.Snapshot();
        data.Validate(fact.IsProposal);

        GameplayEntityId buyer = new(
            MigratedProducerOutcomeIds.RunKind,
            "facility-shop-treasury:current-run");
        GameplayEntityId offer = new(
            MigratedProducerOutcomeIds.OperationKind,
            "facility-shop-offer:" + data.offerTypeId + ":" + data.offerDataId);
        string paymentReference = fact.IsProposal
            ? "pending-payment"
            : data.paidCost > 0
                ? data.paymentTransactionId
                : "no-debit";
        string summary = fact.IsProposal
            ? "시설 상점 구매 예약: " + data.offerName
            : "시설 상점 구매 확정: " + data.offerName
              + "; paid=" + data.paidCost
              + "; balance=" + data.balanceBefore + "->" + data.balanceAfter;

        MigratedProducerOutcomeDetails details = new(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    buyer.Kind,
                    buyer.Value,
                    "던전 금고",
                    MigratedProducerOutcomeIds.OwnerRole),
                new MigratedProducerOutcomeSubject(
                    offer.Kind,
                    offer.Value,
                    data.offerName,
                    MigratedProducerOutcomeIds.TargetRole)
            },
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.QuantityMetric,
                    1,
                    MigratedProducerOutcomeIds.CountUnit,
                    offer),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.AmountMetric,
                    data.quotedCost,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    offer),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.ValueMetric,
                    data.quotedCost,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    offer),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.DeltaMetric,
                    -data.paidCost,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    buyer),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.PreviousMetric,
                    data.balanceBefore,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    buyer),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CurrentMetric,
                    data.balanceAfter,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    buyer)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.OperationFact,
                    data.operationId),
                new GameplayOutcomeFact(
                    ShopSourceFact,
                    string.IsNullOrWhiteSpace(data.shopSource)
                        ? "facility-shop"
                        : data.shopSource),
                new GameplayOutcomeFact(
                    OfferTypeFact,
                    data.offerTypeId),
                new GameplayOutcomeFact(
                    OfferDataFact,
                    data.offerDataId.ToString()),
                new GameplayOutcomeFact(
                    ItemDefinitionFact,
                    data.itemDefinitionId),
                new GameplayOutcomeFact(
                    PaymentTransactionFact,
                    paymentReference),
                new GameplayOutcomeFact(
                    PaymentSourceFact,
                    data.paymentSourceId)
            },
            summary,
            summaryFirst: true);

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.FacilityShopPurchaseResult,
            data.operationId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details,
            reservationOnly: fact.IsProposal);
    }
}
