using System;
using System.Linq;

public sealed class FactionGoodwillFactMapping
{
    private const string PhysicalBatchOutcomeProducer =
        "trade-inventory.physical-item-batch-disposition-receipt";
    private const string DigestSchema =
        "faction-goodwill-transferred-fact-v1";
    private const string DigestKind =
        "faction-goodwill-context-sha256";
    private const string CausationRelation =
        "goodwill-trust-from-physical-transfer";

    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;

    public FactionGoodwillFactMapping(
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public MigratedDomainFactCapture Capture(
        FactionGoodwillTransferredFact fact)
    {
        if (ReferenceEquals(fact, null))
            throw new ArgumentNullException(nameof(fact));

        GameplayResultKey parentKey = ParentKey(fact);
        if (!diagnostics.TryGetResultIdentity(
                parentKey,
                out GameplayOutcomeReplayIdentity parent)
            || parent.ResultKey != parentKey
            || !parent.OutcomeId.IsValid
            || !parent.HasCanonicalPayloadHash
            || !IsCommitted(parent.State))
        {
            throw new InvalidOperationException(
                "Faction goodwill physical transfer is not committed.");
        }

        MigratedProducerOutcomeSubject faction = new(
            MigratedProducerOutcomeIds.FactionKind,
            fact.FactionId,
            fact.FactionDisplayName,
            MigratedProducerOutcomeIds.FactionRole);
        MigratedProducerOutcomeSubject operation = new(
            MigratedProducerOutcomeIds.OperationKind,
            fact.OperationId,
            "호의 물자 전달",
            MigratedProducerOutcomeIds.OperationRole);
        string reason = "호의 물자 " + fact.PhysicalValue;
        var details = new MigratedProducerOutcomeDetails(
            new[] { faction, operation },
            new[]
            {
                Metric(
                    MigratedProducerOutcomeIds.TrustBeforeMetric,
                    fact.PreviousTrust,
                    MigratedProducerOutcomeIds.PointUnit,
                    faction.EntityId),
                Metric(
                    MigratedProducerOutcomeIds.TrustAfterMetric,
                    fact.CurrentTrust,
                    MigratedProducerOutcomeIds.PointUnit,
                    faction.EntityId),
                Metric(
                    MigratedProducerOutcomeIds.DeltaMetric,
                    fact.CurrentTrust - fact.PreviousTrust,
                    MigratedProducerOutcomeIds.PointUnit,
                    faction.EntityId),
                Metric(
                    MigratedProducerOutcomeIds.QuantityMetric,
                    fact.Quantity,
                    MigratedProducerOutcomeIds.CountUnit,
                    operation.EntityId),
                Metric(
                    MigratedProducerOutcomeIds.MassMetric,
                    fact.MassGrams,
                    MigratedProducerOutcomeIds.GramUnit,
                    operation.EntityId),
                Metric(
                    MigratedProducerOutcomeIds.ValueMetric,
                    fact.PhysicalValue,
                    MigratedProducerOutcomeIds.CurrencyUnit,
                    operation.EntityId)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.ReasonFact,
                    reason),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.BatchFact,
                    fact.TransferCommitId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.IdentityFact,
                    parentKey.ToString())
            },
            "호의 물자에 따른 세력 신뢰 변화: faction="
            + fact.FactionId
            + "; trust=" + fact.PreviousTrust
            + "->" + fact.CurrentTrust
            + "; quantity=" + fact.Quantity
            + "; value=" + fact.PhysicalValue,
            new GameplayOutcomeCausation(
                parent.OutcomeId,
                new GameplayOperationId(fact.OperationId),
                CausationRelation),
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    DigestKind,
                    ComputeContextSha256(fact))
            });
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.FactionTrustChangedEvent,
            fact.OperationId,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public static GameplayResultKey ParentKey(
        FactionGoodwillTransferredFact fact)
    {
        if (ReferenceEquals(fact, null))
            throw new ArgumentNullException(nameof(fact));
        return new GameplayResultKey(
            PhysicalBatchOutcomeProducer,
            new GameplayOperationId(fact.OperationId),
            fact.TransferRevision,
            0);
    }

    public static string ComputeContextSha256(
        FactionGoodwillTransferredFact fact)
    {
        if (ReferenceEquals(fact, null))
            throw new ArgumentNullException(nameof(fact));

        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append(DigestSchema);
        digest.Append(fact.FactionId);
        digest.Append(fact.FactionDisplayName);
        digest.Append(fact.OperationId);
        digest.Append(fact.TransferCommitId);
        digest.Append(fact.AbsoluteDay);
        digest.Append(fact.PreviousTrust);
        digest.Append(fact.CurrentTrust);
        digest.Append(fact.Quantity);
        digest.Append(fact.PhysicalValue);
        digest.Append(fact.MassGrams);
        digest.Append(fact.TransferRevision);
        string[] sources = (fact.SourceStackIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        digest.Append(sources.Length);
        foreach (string source in sources)
            digest.Append(source);
        return digest.ComputeSha256();
    }

    private static bool IsCommitted(GameplayOutcomeReplayState state) =>
        state is GameplayOutcomeReplayState.Committed
            or GameplayOutcomeReplayState.DeliveryFaultPending
            or GameplayOutcomeReplayState.PublishedAwaitingAcknowledgement
            or GameplayOutcomeReplayState.PublishedAcknowledged
            or GameplayOutcomeReplayState.Compacted
            or GameplayOutcomeReplayState.Forgotten;

    private static GameplayOutcomeMetric Metric(
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId reference) => new(id, value, unit, reference);
}
