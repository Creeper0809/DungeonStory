using System;
using System.Linq;

public static class AutoProcurementFactMapping
{
    public static MigratedDomainFactCapture Capture(
        AutoProcurementCompletedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        string ownerIdentity = string.IsNullOrEmpty(fact.OfferKey)
            ? CreateResultIdentity(fact)
            : fact.OfferKey;
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.AutoProcurementResult,
            ownerIdentity,
            Math.Max(0, fact.Day),
            fact.Purchased
                ? GameplayOutcomeStatus.Succeeded
                : GameplayOutcomeStatus.Failed,
            key => BuildDetails(fact, key));
    }

    private static MigratedProducerOutcomeDetails BuildDetails(
        AutoProcurementCompletedFact fact,
        GameplayResultKey key)
    {
        GameplayEntityId operation = new(
            MigratedProducerOutcomeIds.OperationKind,
            key.OperationId.Value);
        string label = string.IsNullOrWhiteSpace(fact.ItemLabel)
            ? fact.RuleId
            : fact.ItemLabel;
        string stackRefs = string.Join(",",
            fact.PhysicalStacks.Select(value => value.StackId + ":"
                + (string.IsNullOrWhiteSpace(value.ItemInstanceId)
                    ? "none"
                    : value.ItemInstanceId)
                + ":" + value.Quantity));

        return new MigratedProducerOutcomeDetails(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.OperationKind,
                    operation.Value,
                    label,
                    MigratedProducerOutcomeIds.OperationRole)
            },
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.QuantityMetric,
                    fact.AttemptedQuantity,
                    MigratedProducerOutcomeIds.CountUnit,
                    operation),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.AmountMetric,
                    fact.AttemptedCost,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    operation),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CountMetric,
                    fact.CommittedQuantity,
                    MigratedProducerOutcomeIds.CountUnit,
                    operation),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.ValueMetric,
                    fact.CommittedCost,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    operation)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.OperationFact,
                    key.OperationId.Value),
                new GameplayOutcomeFact(
                    new GameplayOutcomeFactId(
                        "procurement-attempted-item-id"),
                    OptionalFact(fact.AttemptedItemId)),
                new GameplayOutcomeFact(
                    new GameplayOutcomeFactId(
                        "procurement-committed-item-id"),
                    OptionalFact(fact.CommittedItemId)),
                new GameplayOutcomeFact(
                    new GameplayOutcomeFactId(
                        "procurement-child-result-key"),
                    OptionalFact(fact.ChildPurchaseResultKey)),
                new GameplayOutcomeFact(
                    new GameplayOutcomeFactId(
                        "procurement-physical-stacks"),
                    OptionalFact(stackRefs)),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.ReasonFact,
                    OptionalFact(fact.Reason)),
                new GameplayOutcomeFact(
                    new GameplayOutcomeFactId("procurement-purchased"),
                    fact.Purchased ? "true" : "false")
            },
            CreateResultSummary(fact), summaryFirst: true);
    }

    private static string CreateResultIdentity(
        AutoProcurementCompletedFact fact) =>
        "auto-procurement:day=" + Math.Max(0, fact.Day)
        + ":rule=" + (fact.RuleId?.Trim() ?? string.Empty)
        + ":item=" + (fact.ItemLabel?.Trim() ?? string.Empty)
        + ":purchased=" + (fact.Purchased ? "1" : "0");

    private static string OptionalFact(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value;

    private static string CreateResultSummary(
        AutoProcurementCompletedFact fact) =>
        "자동 조달 결과: rule=" + fact.RuleId
        + "; item=" + fact.ItemLabel
        + "; quantity=" + fact.Quantity
        + "; cost=" + fact.Cost
        + "; attempted=" + fact.AttemptedItemId
        + "/" + fact.AttemptedQuantity
        + "/" + fact.AttemptedCost
        + "; committed=" + fact.CommittedItemId
        + "/" + fact.CommittedQuantity
        + "/" + fact.CommittedCost
        + "; child=" + fact.ChildPurchaseResultKey
        + "; stacks=" + string.Join(",",
            fact.PhysicalStacks.Select(value =>
                value.StackId + ":" + value.Quantity))
        + "; purchased=" + (fact.Purchased ? "true" : "false")
        + "; reason=" + fact.Reason;
}
