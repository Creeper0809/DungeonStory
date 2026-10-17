using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;

public sealed class FactionCargoFactMapping
{
    private const string DigestSchema = "faction-cargo-delivered-fact-v1";
    private const string DigestKind = "faction-cargo-context-sha256";
    private const string CausationRelation = "route-cargo-from-arrival";

    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;

    public FactionCargoFactMapping(
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public MigratedDomainFactCapture Capture(
        FactionCargoDeliveredFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        FactionRouteCargoDeliveryReceipt receipt = fact.Receipt()
            ?? throw new InvalidOperationException(
                "Faction cargo fact has no frozen delivery receipt.");
        GameplayOutcomeId parent = ResolveArrival(fact.ArrivalPublicationKey);
        MigratedProducerOutcomeDetails details = CreateDetails(
            fact,
            receipt,
            parent);
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.FactionRouteCargoDeliveryReceipt,
            receipt.batchCommitId,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public static string ComputeContextSha256(
        FactionCargoDeliveredFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        FactionRouteCargoDeliveryReceipt receipt = fact.Receipt()
            ?? throw new InvalidOperationException(
                "Faction cargo fact has no frozen delivery receipt.");
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append(DigestSchema);
        digest.Append(fact.RouteId);
        digest.Append(fact.FactionId);
        digest.Append(fact.FactionDisplayName);
        digest.Append(fact.AbsoluteDay);
        digest.Append(fact.SourceQ);
        digest.Append(fact.SourceR);
        digest.Append(fact.DestinationQ);
        digest.Append(fact.DestinationR);
        digest.Append(fact.ArrivalPublicationKey);
        digest.AppendEnum(receipt.state);
        digest.Append(receipt.batchCommitId);
        digest.Append(receipt.destinationId);
        digest.Append(receipt.outcomeFingerprint);
        digest.Append(receipt.deliveryX);
        digest.Append(receipt.deliveryY);
        digest.Append(receipt.totalMassGrams);

        IReadOnlyList<ProductionDomainPublishedStackSaveData> stacks =
            receipt.stacks == null
                ? Array.Empty<ProductionDomainPublishedStackSaveData>()
                : receipt.stacks;
        digest.Append(stacks.Count);
        foreach (ProductionDomainPublishedStackSaveData stack in stacks)
        {
            if (stack == null)
                throw new InvalidOperationException(
                    "Faction cargo fact contains a null published stack.");
            digest.Append(stack.outputLineId);
            digest.Append(stack.itemId);
            digest.Append(stack.itemInstanceId);
            digest.Append(stack.stackId);
            digest.Append(stack.quantity);
            digest.Append(stack.massGrams);
            digest.Append(stack.displayText);
        }

        IReadOnlyList<FactionRouteQuoteLineFactSnapshot> quoteLines =
            fact.QuoteLines
            ?? Array.Empty<FactionRouteQuoteLineFactSnapshot>();
        digest.Append(quoteLines.Count);
        foreach (FactionRouteQuoteLineFactSnapshot line in quoteLines)
        {
            digest.Append(line.ItemId);
            digest.Append(line.Amount);
            digest.Append(line.UnitPriceGold);
        }
        return digest.ComputeSha256();
    }

    private GameplayOutcomeId ResolveArrival(string publicationKey)
    {
        if (string.IsNullOrWhiteSpace(publicationKey))
            return default;
        if (!GameplayDomainFactDeliveryQuery.TryReadKey(
                publicationKey,
                out GameplayResultKey key)
            || !diagnostics.TryGetResultIdentity(
                key,
                out GameplayOutcomeReplayIdentity identity)
            || identity.ResultKey != key
            || !identity.OutcomeId.IsValid
            || !identity.HasCanonicalPayloadHash
            || !IsCommitted(identity.State))
        {
            throw new InvalidOperationException(
                "Faction cargo arrival publication is not committed.");
        }

        return identity.OutcomeId;
    }

    private static bool IsCommitted(GameplayOutcomeReplayState state) =>
        state is GameplayOutcomeReplayState.Committed
            or GameplayOutcomeReplayState.DeliveryFaultPending
            or GameplayOutcomeReplayState.PublishedAwaitingAcknowledgement
            or GameplayOutcomeReplayState.PublishedAcknowledged
            or GameplayOutcomeReplayState.Compacted
            or GameplayOutcomeReplayState.Forgotten;

    private static MigratedProducerOutcomeDetails CreateDetails(
        FactionCargoDeliveredFact fact,
        FactionRouteCargoDeliveryReceipt receipt,
        GameplayOutcomeId parent)
    {
        var subjects = new List<MigratedProducerOutcomeSubject>
        {
            new(
                MigratedProducerOutcomeIds.RouteKind,
                fact.RouteId,
                fact.RouteId,
                MigratedProducerOutcomeIds.RouteRole),
            new(
                MigratedProducerOutcomeIds.FactionKind,
                fact.FactionId,
                string.IsNullOrWhiteSpace(fact.FactionDisplayName)
                    ? fact.FactionId
                    : fact.FactionDisplayName,
                MigratedProducerOutcomeIds.FactionRole),
            Location(
                fact.SourceQ,
                fact.SourceR,
                MigratedProducerOutcomeIds.SourceRole),
            Location(
                fact.DestinationQ,
                fact.DestinationR,
                MigratedProducerOutcomeIds.DestinationRole)
        };
        var metrics = new List<GameplayOutcomeMetric>();
        ProductionDomainPublishedStackSaveData[] stacks =
            (receipt.stacks
                ?? new List<ProductionDomainPublishedStackSaveData>())
            .Where(value => value != null)
            .ToArray();
        ILookup<string, FactionRouteQuoteLineFactSnapshot> quoteLines =
            (fact.QuoteLines
                ?? Array.Empty<FactionRouteQuoteLineFactSnapshot>())
            .ToLookup(value => value.ItemId, StringComparer.Ordinal);
        foreach (IGrouping<string, ProductionDomainPublishedStackSaveData>
                 group in stacks
                     .GroupBy(value => value.itemId, StringComparer.Ordinal)
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            subjects.Add(new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.ItemDefinitionKind,
                group.Key,
                group.Key,
                MigratedProducerOutcomeIds.ItemRole));
            GameplayEntityId item = new(
                MigratedProducerOutcomeIds.ItemDefinitionKind,
                group.Key);
            int quantity = group.Sum(value => value.quantity);
            long mass = group.Sum(value => value.massGrams);
            int unitPrice = quoteLines[group.Key]
                .Select(value => value.UnitPriceGold)
                .FirstOrDefault();
            metrics.Add(Metric(
                MigratedProducerOutcomeIds.QuantityMetric,
                quantity,
                MigratedProducerOutcomeIds.CountUnit,
                item));
            metrics.Add(Metric(
                MigratedProducerOutcomeIds.MassMetric,
                mass,
                MigratedProducerOutcomeIds.GramUnit,
                item));
            metrics.Add(Metric(
                MigratedProducerOutcomeIds.ValueMetric,
                (long)quantity * unitPrice,
                MigratedProducerOutcomeIds.CurrencyUnit,
                item));
        }

        GameplayEntityId route = subjects[0].EntityId;
        metrics.Add(Metric(
            MigratedProducerOutcomeIds.CountMetric,
            receipt.stacks?.Count ?? 0,
            MigratedProducerOutcomeIds.CountUnit,
            route));
        metrics.Add(Metric(
            MigratedProducerOutcomeIds.MassMetric,
            receipt.totalMassGrams,
            MigratedProducerOutcomeIds.GramUnit,
            route));
        return new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            metrics.ToArray(),
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.BatchFact,
                    receipt.batchCommitId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.IdentityFact,
                    receipt.outcomeFingerprint),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.AfterFact,
                    receipt.destinationId)
            },
            "세력 경로 화물 인도: route=" + fact.RouteId
            + "; destination=" + receipt.destinationId
            + "; stacks=" + (receipt.stacks?.Count ?? 0)
            + "; mass-grams=" + receipt.totalMassGrams,
            new GameplayOutcomeCausation(
                parent,
                new GameplayOperationId(fact.RouteId),
                CausationRelation),
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    DigestKind,
                    ComputeContextSha256(fact))
            });
    }

    private static MigratedProducerOutcomeSubject Location(
        int q,
        int r,
        GameplayRoleId role) => new(
        MigratedProducerOutcomeIds.WorldLocationKind,
        $"hex:{q}:{r}",
        $"육각 좌표 ({q}, {r})",
        role);

    private static GameplayOutcomeMetric Metric(
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId reference) => new(id, value, unit, reference);
}
