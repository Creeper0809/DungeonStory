using System.Globalization;

public static class RetailPurchaseFactMapping
{
    private static readonly GameplayOutcomeFactId SaleItemFact =
        new("retail-sale-item-id");
    private static readonly GameplayOutcomeFactId DefinitionFact =
        new("retail-item-definition-id");
    private static readonly GameplayOutcomeFactId InstanceFact =
        new("retail-item-instance-id");
    private static readonly GameplayOutcomeFactId SourceStackFact =
        new("retail-source-stack-id");
    private static readonly GameplayOutcomeFactId SourceOperationFact =
        new("retail-source-operation-id");
    private static readonly GameplayOutcomeFactId FingerprintFact =
        new("retail-component-fingerprint");
    private static readonly GameplayOutcomeFactId BuyerDebitFact =
        new("retail-buyer-debit-operation-id");
    private static readonly GameplayOutcomeFactId ResultKeyFact =
        new("retail-purchase-result-key");
    private static readonly GameplayOutcomeFactId DispositionFact =
        new("retail-physical-disposition");
    private static readonly GameplayOutcomeFactId RevenueWorkerFact =
        new("retail-revenue-worker-id");
    private static readonly GameplayOutcomeFactId RevenueMultiplierFact =
        new("retail-revenue-price-multiplier");
    private static readonly GameplayOutcomeFactId RevenueSkillProvenanceFact =
        new("retail-revenue-skill-modifier-provenance");

    public static MigratedDomainFactCapture Capture(RetailPurchaseFact fact)
    {
        RetailPurchaseFactData data = fact.Snapshot();
        data.Validate();
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult,
            "retail-purchase:" + data.operationId + ":buyer=" + data.buyerId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            key => CreateDetails(data, key));
    }

    private static MigratedProducerOutcomeDetails CreateDetails(
        RetailPurchaseFactData data,
        GameplayResultKey resultKey)
    {
        RetailStockLotSnapshot lot = data.lot;
        GameplayEntityId buyer = new(
            MigratedProducerOutcomeIds.CharacterKind,
            data.buyerId);
        GameplayEntityId facility = new(
            MigratedProducerOutcomeIds.FacilityKind,
            data.facilityId);
        GameplayEntityId purchasedLot = new(
            MigratedProducerOutcomeIds.OperationKind,
            data.operationId);
        string summary =
            "시설 소매 구매 확정: " + lot.itemDefinitionId
            + "; quantity=" + lot.quantity + "; cost=" + data.purchaseCost
            + "; buyerMoney=" + data.buyerMoneyBefore
            + "->" + data.buyerMoneyAfter;

        return new MigratedProducerOutcomeDetails(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    buyer.Kind,
                    buyer.Value,
                    "손님",
                    MigratedProducerOutcomeIds.CustomerRole),
                new MigratedProducerOutcomeSubject(
                    facility.Kind,
                    facility.Value,
                    "상점",
                    MigratedProducerOutcomeIds.FacilityRole),
                new MigratedProducerOutcomeSubject(
                    purchasedLot.Kind,
                    purchasedLot.Value,
                    lot.itemDefinitionId,
                    MigratedProducerOutcomeIds.ItemRole)
            },
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.QuantityMetric,
                    lot.quantity,
                    MigratedProducerOutcomeIds.CountUnit,
                    purchasedLot),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.MassMetric,
                    lot.unitMassGrams * lot.quantity,
                    MigratedProducerOutcomeIds.GramUnit,
                    purchasedLot),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.AmountMetric,
                    data.purchaseCost,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    buyer),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.PreviousMetric,
                    data.buyerMoneyBefore,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    buyer),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CurrentMetric,
                    data.buyerMoneyAfter,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    buyer),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.DeltaMetric,
                    -data.purchaseCost,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    buyer)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.OperationFact,
                    data.operationId),
                new GameplayOutcomeFact(
                    SaleItemFact,
                    lot.saleItemId.ToString()),
                new GameplayOutcomeFact(
                    DefinitionFact,
                    lot.itemDefinitionId),
                new GameplayOutcomeFact(
                    InstanceFact,
                    Optional(lot.itemInstanceId)),
                new GameplayOutcomeFact(
                    SourceStackFact,
                    Optional(lot.sourceStackId)),
                new GameplayOutcomeFact(
                    SourceOperationFact,
                    lot.sourceOperationId),
                new GameplayOutcomeFact(
                    FingerprintFact,
                    Optional(lot.componentFingerprint)),
                new GameplayOutcomeFact(
                    BuyerDebitFact,
                    data.operationId),
                new GameplayOutcomeFact(
                    ResultKeyFact,
                    resultKey.ToString()),
                new GameplayOutcomeFact(
                    DispositionFact,
                    "retail-terminal-sink-committed"),
                new GameplayOutcomeFact(
                    RevenueWorkerFact,
                    Optional(data.revenueWorkerId)),
                new GameplayOutcomeFact(
                    RevenueMultiplierFact,
                    data.revenuePriceMultiplier.ToString(
                        "R",
                        CultureInfo.InvariantCulture)),
                new GameplayOutcomeFact(
                    RevenueSkillProvenanceFact,
                    data.revenueSkillModifierProvenance)
            },
            summary,
            summaryFirst: true);
    }

    private static string Optional(string value) =>
        string.IsNullOrEmpty(value) ? "none" : value;
}
