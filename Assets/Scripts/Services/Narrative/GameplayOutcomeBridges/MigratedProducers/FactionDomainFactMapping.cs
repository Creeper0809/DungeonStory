using System;
using System.Collections.Generic;
using System.Linq;

public sealed class FactionDomainFactMapping
{
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    public FactionDomainFactMapping(IGameplayOutcomeDiagnosticsQuery diagnostics) =>
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    public MigratedDomainFactCapture Capture(
        FactionTrustChangedDomainFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        MigratedProducerOutcomeSubject faction = CreateFactionSubject(
            fact.FactionId,
            fact.FactionDisplayName,
            MigratedProducerOutcomeIds.FactionRole);
        string reason = fact.Reason.Length > 0 ? fact.Reason : "unspecified";
        var details = new MigratedProducerOutcomeDetails(
            new[] { faction },
            new[]
            {
                Metric(
                    MigratedProducerOutcomeIds.TrustBeforeMetric,
                    fact.Previous,
                    MigratedProducerOutcomeIds.PointUnit,
                    faction.EntityId),
                Metric(
                    MigratedProducerOutcomeIds.TrustAfterMetric,
                    fact.Current,
                    MigratedProducerOutcomeIds.PointUnit,
                    faction.EntityId),
                Metric(
                    MigratedProducerOutcomeIds.DeltaMetric,
                    fact.Current - fact.Previous,
                    MigratedProducerOutcomeIds.PointUnit,
                    faction.EntityId)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.ReasonFact,
                    reason),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.IdentityFact,
                    fact.OwnerIdentity)
            },
            "세력 신뢰 변화: faction=" + fact.FactionId
            + "; previous=" + fact.Previous
            + "; current=" + fact.Current
            + "; reason=" + fact.Reason
            + "; owner=" + fact.OwnerIdentity);
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.FactionTrustChangedEvent,
            fact.OwnerIdentity,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public MigratedDomainFactCapture Capture(
        FactionBetrayalDomainFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        var subjects = new List<MigratedProducerOutcomeSubject>
        {
            new(
                MigratedProducerOutcomeIds.OperationKind,
                "dungeon-authority",
                "던전 운영 주체",
                MigratedProducerOutcomeIds.BetrayerRole)
        };
        var metrics = new List<GameplayOutcomeMetric>
        {
            Metric(
                MigratedProducerOutcomeIds.ValueMetric,
                fact.ActualLootValue,
                MigratedProducerOutcomeIds.CurrencyUnit,
                subjects[0].EntityId)
        };
        foreach (FactionTrustTransitionFactSnapshot transition in
                 fact.Transitions)
        {
            MigratedProducerOutcomeSubject subject = CreateFactionSubject(
                transition.FactionId,
                transition.FactionDisplayName,
                string.Equals(
                    transition.FactionId,
                    fact.BetrayedFactionId,
                    StringComparison.Ordinal)
                    ? MigratedProducerOutcomeIds.TargetRole
                    : MigratedProducerOutcomeIds.WitnessRole);
            subjects.Add(subject);
            metrics.Add(Metric(
                MigratedProducerOutcomeIds.TrustBeforeMetric,
                transition.Previous,
                MigratedProducerOutcomeIds.PointUnit,
                subject.EntityId));
            metrics.Add(Metric(
                MigratedProducerOutcomeIds.TrustAfterMetric,
                transition.Current,
                MigratedProducerOutcomeIds.PointUnit,
                subject.EntityId));
        }

        string transitionSummary = string.Join(
            ",",
            fact.Transitions.Select(transition =>
                transition.FactionId
                + ":" + transition.Previous
                + "->" + transition.Current
                + ":" + (string.Equals(
                    transition.FactionId,
                    fact.BetrayedFactionId,
                    StringComparison.Ordinal)
                    ? "betrayed"
                    : "witness")));
        var details = new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            metrics.ToArray(),
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.OperationFact,
                    fact.OperationId)
            },
            "세력 배신 신뢰 변화: target=" + fact.BetrayedFactionId
            + "; operation=" + fact.OperationId
            + "; affected=" + fact.Transitions.Count
            + "; loot-value=" + fact.ActualLootValue
            + "; transitions=" + transitionSummary);
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.FactionTrustChangedEvent,
            fact.OperationId,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public MigratedDomainFactCapture Capture(
        FactionRouteSettlementDomainFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        List<MigratedProducerOutcomeSubject> subjects =
            CreateRouteCommonSubjects(fact.Route);
        foreach (string itemId in fact.Settlement.QuoteLines
                     .Select(value => value.ItemId)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            subjects.Add(CreateItemSubject(itemId));
        }

        var metrics = new List<GameplayOutcomeMetric>();
        foreach (FactionRouteQuoteLineFactSnapshot line in
                 fact.Settlement.QuoteLines.OrderBy(
                     value => value.ItemId,
                     StringComparer.Ordinal))
        {
            GameplayEntityId item = new(
                MigratedProducerOutcomeIds.ItemDefinitionKind,
                line.ItemId);
            metrics.Add(Metric(
                MigratedProducerOutcomeIds.QuantityMetric,
                line.Amount,
                MigratedProducerOutcomeIds.CountUnit,
                item));
            metrics.Add(Metric(
                MigratedProducerOutcomeIds.ValueMetric,
                line.UnitPriceGold,
                MigratedProducerOutcomeIds.CurrencyUnit,
                item));
        }

        GameplayEntityId routeId = subjects[0].EntityId;
        bool paid = fact.Settlement.State
            == DungeonStory.Factions.FactionRouteSettlementState.Paid;
        metrics.Add(Metric(
            MigratedProducerOutcomeIds.ValueMetric,
            fact.Settlement.CargoAuthoredGold,
            MigratedProducerOutcomeIds.CurrencyUnit,
            routeId));
        metrics.Add(Metric(
            MigratedProducerOutcomeIds.AmountMetric,
            paid
                ? fact.Settlement.PaymentGold
                : fact.Settlement.AllianceBenefitDebitMilliEwu,
            paid
                ? MigratedProducerOutcomeIds.CurrencyUnit
                : MigratedProducerOutcomeIds.PointUnit,
            routeId));
        metrics.Add(Metric(
            MigratedProducerOutcomeIds.PreviousMetric,
            paid
                ? fact.Settlement.BalanceBefore
                : fact.Settlement.AllianceBenefitBalanceBeforeMilliEwu,
            paid
                ? MigratedProducerOutcomeIds.CurrencyUnit
                : MigratedProducerOutcomeIds.PointUnit,
            routeId));
        metrics.Add(Metric(
            MigratedProducerOutcomeIds.CurrentMetric,
            paid
                ? fact.Settlement.BalanceAfter
                : fact.Settlement.AllianceBenefitBalanceAfterMilliEwu,
            paid
                ? MigratedProducerOutcomeIds.CurrencyUnit
                : MigratedProducerOutcomeIds.PointUnit,
            routeId));
        string transactionIdentity = paid
            ? fact.Settlement.TransactionId
            : fact.Settlement.AllianceBenefitReservationId;
        var details = new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            metrics.ToArray(),
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.OperationFact,
                    fact.SettlementIdentity),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.IdentityFact,
                    fact.Settlement.CapabilityId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.BatchFact,
                    transactionIdentity),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.BeforeFact,
                    fact.Settlement.SourceDigest),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.AfterFact,
                    fact.Settlement.QuoteDigest)
            },
            "세력 경로 정산: route=" + fact.Route.RouteId
            + "; faction=" + fact.Route.FactionId
            + "; state=" + fact.Settlement.State
            + "; payment=" + fact.Settlement.PaymentGold,
            new GameplayOutcomeCausation(
                default,
                new GameplayOperationId(fact.Route.RouteId),
                "route-settlement-started"),
            provenance: ObservationProvenance(fact.CaptureData()));
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.FactionRouteSettlementReceipt,
            fact.SettlementIdentity,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public MigratedDomainFactCapture Capture(
        FactionRouteArrivedDomainFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        List<MigratedProducerOutcomeSubject> subjects =
            CreateRouteCommonSubjects(fact.After);
        foreach (string itemId in fact.After.Cargo
                     .Select(value => value.ItemId)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            subjects.Add(CreateItemSubject(itemId));
        }
        GameplayEntityId routeId = subjects[0].EntityId;
        var details = new MigratedProducerOutcomeDetails(
            subjects.ToArray(),
            new[]
            {
                Metric(
                    MigratedProducerOutcomeIds.StrengthBeforeMetric,
                    fact.Before.Strength,
                    MigratedProducerOutcomeIds.PointUnit,
                    routeId),
                Metric(
                    MigratedProducerOutcomeIds.StrengthAfterMetric,
                    fact.After.Strength,
                    MigratedProducerOutcomeIds.PointUnit,
                    routeId),
                Metric(
                    MigratedProducerOutcomeIds.DelayBeforeMetric,
                    fact.Before.DelaySeconds,
                    MigratedProducerOutcomeIds.SecondUnit,
                    routeId),
                Metric(
                    MigratedProducerOutcomeIds.DelayAfterMetric,
                    fact.After.DelaySeconds,
                    MigratedProducerOutcomeIds.SecondUnit,
                    routeId)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.BeforeFact,
                    fact.Before.Status.ToString()),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.AfterFact,
                    fact.After.Status.ToString())
            },
            "세력 경로 도착: route=" + fact.After.RouteId
            + "; faction=" + fact.After.FactionId
            + "; kind=" + fact.After.Kind
            + "; strength=" + fact.After.Strength,
            CreateArrivalCausation(fact.After),
            provenance: ObservationProvenance(fact.CaptureData()));
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.FactionRouteArrivedEvent,
            fact.After.RouteId,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    private static List<MigratedProducerOutcomeSubject>
        CreateRouteCommonSubjects(FactionRouteFactSnapshot route) => new()
        {
            new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.RouteKind,
                route.RouteId,
                route.RouteId,
                MigratedProducerOutcomeIds.RouteRole),
            CreateFactionSubject(
                route.FactionId,
                route.FactionDisplayName,
                MigratedProducerOutcomeIds.FactionRole),
            CreateLocationSubject(
                route.SourceQ,
                route.SourceR,
                MigratedProducerOutcomeIds.SourceRole),
            CreateLocationSubject(
                route.DestinationQ,
                route.DestinationR,
                MigratedProducerOutcomeIds.DestinationRole)
        };

    private static MigratedProducerOutcomeSubject CreateFactionSubject(
        string factionId,
        string displayName,
        GameplayRoleId role) => new(
        MigratedProducerOutcomeIds.FactionKind,
        factionId,
        displayName,
        role);

    private static MigratedProducerOutcomeSubject CreateLocationSubject(
        int q,
        int r,
        GameplayRoleId role) => new(
        MigratedProducerOutcomeIds.WorldLocationKind,
        $"hex:{q}:{r}",
        $"육각 좌표 ({q}, {r})",
        role);

    private static MigratedProducerOutcomeSubject CreateItemSubject(
        string itemId) => new(
        MigratedProducerOutcomeIds.ItemDefinitionKind,
        itemId,
        itemId,
        MigratedProducerOutcomeIds.ItemRole);

    private GameplayOutcomeCausation CreateArrivalCausation(FactionRouteFactSnapshot route)
    {
        GameplayOutcomeId parent = default;
        if (!string.IsNullOrEmpty(route.SettlementPublicationKey))
        {
            if (!GameplayDomainFactDeliveryQuery.TryReadKey(route.SettlementPublicationKey, out var key)
                || !diagnostics.TryGetResultIdentity(key, out var identity) || identity.ResultKey != key
                || !identity.OutcomeId.IsValid || !identity.HasCanonicalPayloadHash
                || identity.State is GameplayOutcomeReplayState.Reserved or GameplayOutcomeReplayState.Prepared)
                throw new InvalidOperationException("faction-arrival-settlement-parent-not-committed");
            parent = identity.OutcomeId;
        }
        return new GameplayOutcomeCausation(parent, new GameplayOperationId(route.RouteId), "route-arrival-from-settlement");
    }

    private static GameplayOutcomeProvenanceReference[] ObservationProvenance(DungeonStory.Factions.FactionRouteObservationData data) => new[]
    {
        new GameplayOutcomeProvenanceReference("faction-route-observation-sha256",
            NarrativeInferenceHash.ComputeSha256Utf8(UnityEngine.JsonUtility.ToJson(data)))
    };

    private static GameplayOutcomeMetric Metric(
        GameplayMetricId metricId,
        double value,
        GameplayMetricUnitId unitId,
        GameplayEntityId referenceId) => new(
        metricId,
        value,
        unitId,
        referenceId);
}
