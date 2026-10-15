using System;
using System.Collections.Generic;
using System.Linq;

public static class ResourceStockPolicySaleOutcomeDetails
{
    public static readonly GameplayOutcomeFactId ItemDefinitionFact =
        new("item-definition-id");
    public static readonly GameplayOutcomeFactId DestinationFact =
        new("destination-id");
    public static readonly GameplayOutcomeFactId PhysicalCommitFact =
        new("physical-commit-id");
    public static readonly GameplayOutcomeFactId PhysicalResultKeyFact =
        new("physical-result-key");
    public static readonly GameplayOutcomeFactId SourceStackIdsFact =
        new("source-stack-ids");
    public static readonly GameplayOutcomeFactId EconomyTransactionFact =
        new("economy-transaction-id");
    public static readonly GameplayOutcomeFactId SaleRateFact =
        new("sale-rate-basis-points");
    public static readonly GameplayOutcomeFactId ComponentFingerprintFact =
        new("component-fingerprint");
    public static readonly GameplayOutcomeFactId QualityTierFact =
        new("quality-tier");
    public static readonly GameplayOutcomeFactId AuthorityReleaseFact =
        new("unique-authority-release-required");

    private static readonly GameplayEntityKindId ItemDefinitionKind =
        new("item-definition");
    private static readonly GameplayEntityKindId ItemStackKind =
        new("item-stack");
    private static readonly GameplayEntityKindId StockPolicyKind =
        new("stock-policy");
    private static readonly GameplayEntityKindId MarketDestinationKind =
        new("market-destination");

    public static GameplayResultKey PhysicalResultKey(
        string operationId,
        long ownerRevision) => new(
        TradeInventoryOutcomeDefinitions.Get(
            TradeInventoryOutcomeKind.PhysicalItemBatchDisposition).ProducerId,
        new GameplayOperationId(operationId),
        ownerRevision,
        0);

    public static MigratedProducerOutcomeDetails Create(
        ResourceStockPolicyPendingSale pending,
        GameplayOutcomeId physicalOutcomeId)
    {
        RequireNormal(pending, physicalOutcomeId);
        GameplayEntityId operation = new(
            MigratedProducerOutcomeIds.OperationKind,
            pending.operationId);
        GameplayEntityId item = new(ItemDefinitionKind, pending.itemId);
        GameplayEntityId policy = new(StockPolicyKind, pending.itemId);
        GameplayEntityId destination = new(
            MarketDestinationKind,
            StableDestinationId(pending.destinationId));
        List<MigratedProducerOutcomeSubject> subjects = new()
        {
            Subject(operation, "초과 재고 판매", MigratedProducerOutcomeIds.OperationRole),
            Subject(item, pending.itemId, MigratedProducerOutcomeIds.ItemRole),
            Subject(policy, "초과 재고 정책", MigratedProducerOutcomeIds.SourceRole),
            Subject(destination, "외부 시장", MigratedProducerOutcomeIds.DestinationRole)
        };
        foreach (string sourceStackId in pending.sourceStackIds
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            subjects.Add(Subject(
                new GameplayEntityId(ItemStackKind, sourceStackId),
                "판매 원본 물품",
                MigratedProducerOutcomeIds.SourceRole));
        }
        return new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            new[]
            {
                Metric(MigratedProducerOutcomeIds.QuantityMetric, pending.quantity, MigratedProducerOutcomeIds.CountUnit, item),
                Metric(MigratedProducerOutcomeIds.InputMassMetric, pending.inputMassGrams, MigratedProducerOutcomeIds.GramUnit, item),
                Metric(MigratedProducerOutcomeIds.AmountMetric, pending.proceeds, MigratedProducerOutcomeIds.CurrencyUnit, operation),
                Metric(MigratedProducerOutcomeIds.PreviousMetric, pending.balanceBefore, MigratedProducerOutcomeIds.CurrencyUnit, operation),
                Metric(MigratedProducerOutcomeIds.CurrentMetric, pending.balanceAfter, MigratedProducerOutcomeIds.CurrencyUnit, operation),
                Metric(MigratedProducerOutcomeIds.ValueMetric, pending.unitPrice, MigratedProducerOutcomeIds.CurrencyUnit, item),
                Metric(MigratedProducerOutcomeIds.RatioMetric, pending.saleRateBasisPoints, MigratedProducerOutcomeIds.PointUnit, policy),
                Metric(MigratedProducerOutcomeIds.RevisionMetric, pending.physicalOutcomeOwnerRevision, MigratedProducerOutcomeIds.RevisionUnit, operation)
            },
            new[]
            {
                Fact(MigratedProducerOutcomeIds.OperationFact, pending.operationId),
                Fact(MigratedProducerOutcomeIds.ReasonFact, pending.reasonCode),
                Fact(ItemDefinitionFact, pending.itemId),
                Fact(DestinationFact, pending.destinationId),
                Fact(PhysicalCommitFact, pending.commitId),
                Fact(PhysicalResultKeyFact, PhysicalResultKey(pending.operationId, pending.physicalOutcomeOwnerRevision).ToString()),
                Fact(SourceStackIdsFact, CanonicalStackIds(pending.sourceStackIds)),
                Fact(EconomyTransactionFact, pending.economyTransactionId),
                Fact(SaleRateFact, pending.saleRateBasisPoints.ToString())
            },
            "초과 재고 판매: item=" + pending.itemId
                + "; quantity=" + pending.quantity
                + "; proceeds=" + pending.proceeds
                + "; balance=" + pending.balanceBefore + "->" + pending.balanceAfter,
            new GameplayOutcomeCausation(
                physicalOutcomeId,
                new GameplayOperationId(pending.operationId),
                "stock-policy-sale-from-physical-transfer"));
    }

    public static MigratedProducerOutcomeDetails Create(
        QualityRejectedSalePending pending,
        GameplayOutcomeId physicalOutcomeId)
    {
        RequireRejected(pending, physicalOutcomeId);
        GameplayEntityId operation = new(
            MigratedProducerOutcomeIds.OperationKind,
            pending.operationId);
        GameplayEntityId item = new(
            MigratedProducerOutcomeIds.ItemKind,
            pending.itemInstanceId);
        GameplayEntityId stack = new(ItemStackKind, pending.sourceStackId);
        GameplayEntityId destination = new(
            MarketDestinationKind,
            StableDestinationId(pending.destinationId));
        return new MigratedProducerOutcomeDetails(
            new[]
            {
                Subject(operation, "품질 미달 판매", MigratedProducerOutcomeIds.OperationRole),
                Subject(item, pending.itemId, MigratedProducerOutcomeIds.ItemRole),
                Subject(stack, "판매 원본 물품", MigratedProducerOutcomeIds.SourceRole),
                Subject(destination, "외부 시장", MigratedProducerOutcomeIds.DestinationRole)
            },
            new[]
            {
                Metric(MigratedProducerOutcomeIds.QuantityMetric, pending.quantity, MigratedProducerOutcomeIds.CountUnit, item),
                Metric(MigratedProducerOutcomeIds.InputMassMetric, pending.inputMassGrams, MigratedProducerOutcomeIds.GramUnit, item),
                Metric(MigratedProducerOutcomeIds.AmountMetric, pending.proceeds, MigratedProducerOutcomeIds.CurrencyUnit, operation),
                Metric(MigratedProducerOutcomeIds.PreviousMetric, pending.balanceBefore, MigratedProducerOutcomeIds.CurrencyUnit, operation),
                Metric(MigratedProducerOutcomeIds.CurrentMetric, pending.balanceAfter, MigratedProducerOutcomeIds.CurrencyUnit, operation),
                Metric(MigratedProducerOutcomeIds.ValueMetric, pending.unitPrice, MigratedProducerOutcomeIds.CurrencyUnit, item),
                Metric(MigratedProducerOutcomeIds.RatioMetric, pending.saleRateBasisPoints, MigratedProducerOutcomeIds.PointUnit, operation),
                Metric(MigratedProducerOutcomeIds.StatusMetric, pending.qualityTier, MigratedProducerOutcomeIds.EnumUnit, item),
                Metric(MigratedProducerOutcomeIds.RevisionMetric, pending.physicalOutcomeOwnerRevision, MigratedProducerOutcomeIds.RevisionUnit, operation)
            },
            new[]
            {
                Fact(MigratedProducerOutcomeIds.OperationFact, pending.operationId),
                Fact(MigratedProducerOutcomeIds.ReasonFact, pending.reasonCode),
                Fact(ItemDefinitionFact, pending.itemId),
                Fact(MigratedProducerOutcomeIds.IdentityFact, pending.itemInstanceId),
                Fact(DestinationFact, pending.destinationId),
                Fact(PhysicalCommitFact, pending.commitId),
                Fact(PhysicalResultKeyFact, PhysicalResultKey(pending.operationId, pending.physicalOutcomeOwnerRevision).ToString()),
                Fact(SourceStackIdsFact, pending.sourceStackId),
                Fact(EconomyTransactionFact, pending.economyTransactionId),
                Fact(ComponentFingerprintFact, pending.componentFingerprint),
                Fact(QualityTierFact, pending.qualityTier.ToString()),
                Fact(SaleRateFact, pending.saleRateBasisPoints.ToString()),
                Fact(AuthorityReleaseFact, pending.requiresCombatAuthority ? "true" : "false")
            },
            "품질 미달 완제품 판매: item=" + pending.itemId
                + "; instance=" + pending.itemInstanceId
                + "; quality=" + pending.qualityTier
                + "; proceeds=" + pending.proceeds
                + "; balance=" + pending.balanceBefore + "->" + pending.balanceAfter,
            new GameplayOutcomeCausation(
                physicalOutcomeId,
                new GameplayOperationId(pending.operationId),
                "quality-rejected-sale-from-physical-transfer"));
    }

    public static MigratedProducerOutcomeDetails Preview(
        string operationId,
        string itemId,
        string destinationId,
        int quantity,
        long inputMassGrams,
        int proceeds,
        int unitPrice,
        int saleRateBasisPoints,
        bool qualityRejected,
        string itemInstanceId = "preview:item-instance",
        string sourceStackId = "preview:item-stack",
        int qualityTier = 0,
        IReadOnlyList<string> plannedSourceStackIds = null)
    {
        if (qualityRejected)
        {
            return Create(new QualityRejectedSalePending
            {
                sequence = 1,
                operationId = operationId,
                reasonCode = QualityRejectedSaleContract.TransferReason,
                sourceStackId = sourceStackId,
                itemId = itemId,
                itemInstanceId = itemInstanceId,
                componentFingerprint = new string('0', 64),
                destinationId = destinationId,
                quantity = quantity,
                proceeds = proceeds,
                unitPrice = unitPrice,
                saleRateBasisPoints = saleRateBasisPoints,
                qualityTier = qualityTier,
                phase = QualityRejectedSaleCommitPhase.IncomePublished,
                commitId = "preview:physical-commit",
                inputMassGrams = Math.Max(1L, inputMassGrams),
                physicalOutcomeOwnerRevision = 1L,
                economyTransactionId = "preview:economy-transaction",
                balanceBefore = 1,
                balanceAfter = 1 + proceeds
            }, new GameplayOutcomeId(new GameplayOutcomeRunId("preview-run"), 1L));
        }
        return Create(new ResourceStockPolicyPendingSale
        {
            sequence = 1,
            itemId = itemId,
            destinationId = destinationId,
            quantity = quantity,
            proceeds = proceeds,
            unitPrice = unitPrice,
            saleRateBasisPoints = saleRateBasisPoints,
            phase = ResourceStockPolicySaleCommitPhase.IncomePublished,
            operationId = operationId,
            reasonCode = ResourceStockPolicySaleOutbox.TransferReason,
            commitId = "preview:physical-commit",
            sourceStackIds = plannedSourceStackIds == null
                ? new List<string> { sourceStackId }
                : new List<string>(plannedSourceStackIds),
            inputMassGrams = Math.Max(1L, inputMassGrams),
            physicalOutcomeOwnerRevision = 1L,
            economyTransactionId = "preview:economy-transaction",
            balanceBefore = 1,
            balanceAfter = 1 + proceeds
        }, new GameplayOutcomeId(new GameplayOutcomeRunId("preview-run"), 1L));
    }

    private static MigratedProducerOutcomeSubject Subject(
        GameplayEntityId id,
        string display,
        GameplayRoleId role) => new(id.Kind, id.Value, display, role);

    private static string StableDestinationId(string destinationId) =>
        "market-destination:"
        + NarrativeInferenceHash.ComputeSha256Utf8(destinationId ?? string.Empty)
            .Substring(0, 32);

    private static string CanonicalStackIds(IEnumerable<string> stackIds) =>
        string.Join(
            ";",
            (stackIds ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal));

    private static GameplayOutcomeMetric Metric(
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId reference) => new(id, value, unit, reference);

    private static GameplayOutcomeFact Fact(
        GameplayOutcomeFactId id,
        string value) => new(id, value);

    private static void RequireNormal(
        ResourceStockPolicyPendingSale pending,
        GameplayOutcomeId physicalOutcomeId)
    {
        if (pending == null
            || pending.phase < ResourceStockPolicySaleCommitPhase.IncomePublished
            || !physicalOutcomeId.IsValid
            || string.IsNullOrWhiteSpace(pending.economyTransactionId)
            || pending.physicalOutcomeOwnerRevision <= 0L)
        {
            throw new ArgumentException("A paid stock-policy sale and physical parent are required.");
        }
    }

    private static void RequireRejected(
        QualityRejectedSalePending pending,
        GameplayOutcomeId physicalOutcomeId)
    {
        if (pending == null
            || pending.phase < QualityRejectedSaleCommitPhase.IncomePublished
            || !physicalOutcomeId.IsValid
            || string.IsNullOrWhiteSpace(pending.economyTransactionId)
            || pending.physicalOutcomeOwnerRevision <= 0L)
        {
            throw new ArgumentException("A paid rejected-quality sale and physical parent are required.");
        }
    }
}
