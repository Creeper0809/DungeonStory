using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using UnityEditor;
using UnityEngine;

public static class FactionAllianceBenefitBudgetDebugScenarios
{
    [MenuItem("Dungeon Story/QA/V27/Faction Alliance Benefit Budget")]
    public static void Verify()
    {
        ResourceFactionAllianceBenefitBudgetApplicationAdapter authority =
            new ResourceFactionAllianceBenefitBudgetApplicationAdapter();
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            DungeonStory.Balance.BalanceLedgerExecutionMode.AuditOnly);
        FactionAllianceBenefitBudgetReviewSnapshot review =
            FactionAllianceBenefitBudgetReviewAuthority.Capture(audit.Ledger);
        Require(authority.SchemaVersion == 1, "schema version drifted");
        Require(string.Equals(
            authority.AuthorityDigest,
            review.SourceDigest,
            StringComparison.Ordinal), "source digest drifted");
        Require(authority.CapacityMilliEwu == review.CapacityMilliEwu,
            "capacity drifted");
        Require(authority.RefillNumeratorMilliEwu
                == review.RefillNumeratorMilliEwu
            && authority.RefillDenominatorDays
                == review.RefillDenominatorDays,
            "refill rational drifted");
        Require(authority.Routes.Count == 6
            && authority.Routes.Sum(value => value.DebitMilliEwu)
                == authority.CapacityMilliEwu,
            "one-bundle route sum drifted");
        ValidateCurrentFactionQuotes(authority);

        DungeonRuntimeAggregateRootStore refillStore = new();
        FactionDomainRuntime refillDomain = new(refillStore);
        refillDomain.ReplaceState(CreateState(authority, 0L, 1));
        refillDomain.ApplyAllianceBenefitRefill(
            2,
            authority.AuthorityDigest,
            authority.CapacityMilliEwu,
            authority.RefillNumeratorMilliEwu,
            authority.RefillDenominatorDays);
        Require(refillDomain.AllianceBenefitBalanceMilliEwu == 585437L
            && refillDomain.AllianceBenefitRefillRemainder == 1269253L,
            "first exact refill drifted");
        refillDomain.ApplyAllianceBenefitRefill(
            3,
            authority.AuthorityDigest,
            authority.CapacityMilliEwu,
            authority.RefillNumeratorMilliEwu,
            authority.RefillDenominatorDays);
        Require(refillDomain.AllianceBenefitBalanceMilliEwu == 1170875L
            && refillDomain.AllianceBenefitRefillRemainder == 695126L,
            "second exact refill drifted");
        refillDomain.ApplyAllianceBenefitRefill(
            3,
            authority.AuthorityDigest,
            authority.CapacityMilliEwu,
            authority.RefillNumeratorMilliEwu,
            authority.RefillDenominatorDays);
        Require(refillDomain.AllianceBenefitBalanceMilliEwu == 1170875L
            && refillDomain.AllianceBenefitRefillRemainder == 695126L,
            "same-day refill was not idempotent");

        DungeonRuntimeAggregateRootStore reserveStore = new();
        FactionDomainRuntime reserveDomain = new(reserveStore);
        reserveDomain.ReplaceState(CreateState(
            authority,
            authority.CapacityMilliEwu,
            1));
        long lastDebit = 0L;
        long lastAfter = 0L;
        foreach (FactionAllianceBenefitRouteBudgetSnapshot route in
                 authority.Routes)
        {
            Require(reserveDomain.TryReserveAllianceBenefit(
                authority.AuthorityDigest,
                authority.CapacityMilliEwu,
                route.DebitMilliEwu,
                out long before,
                out long after,
                out string failure),
                "route reservation failed: " + failure);
            Require(after == before - route.DebitMilliEwu,
                "route reservation arithmetic drifted");
            lastDebit = route.DebitMilliEwu;
            lastAfter = after;
        }
        Require(reserveDomain.AllianceBenefitBalanceMilliEwu == 0L,
            "one full route bundle did not exhaust the fixed capacity");
        FactionAllianceBenefitRouteBudgetSnapshot golem = authority.Routes
            .Single(value => string.Equals(
                value.FactionId,
                DungeonFactionIds.Golem,
                StringComparison.Ordinal));
        Require(!reserveDomain.TryReserveAllianceBenefit(
                authority.AuthorityDigest,
                authority.CapacityMilliEwu,
                golem.DebitMilliEwu,
                out _,
                out _,
                out _)
            && reserveDomain.AllianceBenefitBalanceMilliEwu == 0L,
            "insufficient reservation mutated the aggregate");
        reserveDomain.RefundAllianceBenefit(
            authority.AuthorityDigest,
            authority.CapacityMilliEwu,
            lastDebit,
            lastAfter);
        Require(reserveDomain.AllianceBenefitBalanceMilliEwu == lastDebit,
            "same-aggregate publication refund was not exact");

        Debug.Log("FACTION_ALLIANCE_BENEFIT_BUDGET_PASS");
    }

    private static void ValidateCurrentFactionQuotes(
        ResourceFactionAllianceBenefitBudgetApplicationAdapter authority)
    {
        IDungeonItemCatalogProvider items = EditorItemCatalogFactory.Create();
        FactionRouteEconomicPolicyRegistry policies = new(new[]
        {
            new AllianceBenefitFactionRouteEconomicPolicy(items)
        });
        IReadOnlyList<FactionDefinitionSnapshot> definitions = AssetDatabase
            .FindAssets(
                "t:DungeonFactionDefinitionSO",
                new[] { "Assets/Resources/SO/Factions/Dungeons" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<DungeonFactionDefinitionSO>)
            .Where(value => value != null)
            .Select(value => value.ToSnapshot())
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        Require(definitions.Count == authority.Routes.Count,
            "faction budget and authored definition counts diverged");
        foreach (FactionAllianceBenefitRouteBudgetSnapshot route in
                 authority.Routes)
        {
            FactionDefinitionSnapshot definition = definitions.Single(
                value => string.Equals(
                    value.StableId,
                    route.FactionId,
                    StringComparison.Ordinal));
            Require(definition.SupplyCooldownDays == route.CooldownDays,
                "supply cooldown drifted for " + route.FactionId);
            Require(policies.TryCreateQuote(
                    definition,
                    FactionRouteKind.SupplyCaravan,
                    out FactionRouteQuoteSnapshot quote,
                    out string quoteFailure),
                quoteFailure);
            Require(string.Equals(
                    quote.SourceDigest,
                    route.SupplyQuoteSourceDigest,
                    StringComparison.Ordinal),
                "supply quote source drifted for " + route.FactionId);
        }

        FactionAllianceBenefitRouteBudgetSnapshot firstBudget =
            authority.Routes[0];
        FactionDefinitionSnapshot first = definitions.Single(value =>
            string.Equals(
                value.StableId,
                firstBudget.FactionId,
                StringComparison.Ordinal));
        Require(policies.TryCreateQuote(
                first,
                FactionRouteKind.SupplyCaravan,
                out FactionRouteQuoteSnapshot supply,
                out string failure),
            failure);
        DungeonFactionSaveData payload = new()
        {
            currentDay = 3,
            routeSequence = 1,
            routeSettlementOperationSequence = 1,
            allianceBenefitBalanceMilliEwu =
                authority.CapacityMilliEwu - firstBudget.DebitMilliEwu,
            allianceBenefitRefillRemainder = 0L,
            allianceBenefitLastRefillDay = 3,
            allianceBenefitAuthorityDigest = authority.AuthorityDigest,
            factions = definitions.Select(value => new DungeonFactionState
            {
                factionId = value.StableId,
                homeQ = 4,
                homeR = -2
            }).ToList(),
            routes = new List<FactionRouteState>
            {
                new()
                {
                    routeId = "faction-route:1",
                    factionId = first.StableId,
                    kind = FactionRouteKind.SupplyCaravan,
                    status = FactionRouteStatus.Traveling,
                    path = new List<FactionHexCoordSaveData>
                    {
                        new() { q = 4, r = -2 },
                        new() { q = 3, r = -1 }
                    },
                    strength = 100,
                    createdDay = 1,
                    estimatedArrivalDay = 2,
                    cargo = first.SupplyCargo
                        .Select(value => value.Clone())
                        .OrderBy(value => value.itemId, StringComparer.Ordinal)
                        .ToList(),
                    cargoDelivery = new FactionRouteCargoDeliveryReceipt
                    {
                        state = FactionRouteCargoDeliveryState.Ready
                    },
                    settlement = new FactionRouteSettlementReceipt
                    {
                        state = FactionRouteSettlementState.AllianceBenefitDebited,
                        capabilityId = supply.CapabilityId,
                        capabilityVersion = supply.CapabilityVersion,
                        operationSequence = 1,
                        cargoAuthoredGold = supply.CargoAuthoredGold,
                        quoteLines = supply.QuoteLines
                            .Select(value => value.Clone())
                            .ToList(),
                        sourceDigest = supply.SourceDigest,
                        quoteDigest = supply.QuoteDigest,
                        allianceBenefitAuthorityDigest =
                            authority.AuthorityDigest,
                        allianceBenefitReservationId =
                            "faction-alliance-benefit:00000001",
                        allianceBenefitDebitMilliEwu =
                            firstBudget.DebitMilliEwu,
                        allianceBenefitBalanceBeforeMilliEwu =
                            authority.CapacityMilliEwu,
                        allianceBenefitBalanceAfterMilliEwu =
                            authority.CapacityMilliEwu
                                - firstBudget.DebitMilliEwu
                    }
                }
            }
        };
        Require(FactionPayloadValidation.Validate(
                payload,
                definitions,
                _ => true).Count == 0,
            "canonical Supply V5 payload was rejected");
        DungeonFactionSaveData tampered = JsonUtility.FromJson<
            DungeonFactionSaveData>(JsonUtility.ToJson(payload));
        tampered.routes[0].settlement.allianceBenefitAuthorityDigest =
            new string('0', 64);
        Require(FactionPayloadValidation.Validate(
                tampered,
                definitions,
                _ => true).Count > 0,
            "Supply receipt accepted a different valid-looking budget digest");
    }

    private static FactionAggregateState CreateState(
        ResourceFactionAllianceBenefitBudgetApplicationAdapter authority,
        long balance,
        int day) => new()
    {
        CurrentDay = day,
        AllianceBenefitBalanceMilliEwu = balance,
        AllianceBenefitRefillRemainder = 0L,
        AllianceBenefitLastRefillDay = day,
        AllianceBenefitAuthorityDigest = authority.AuthorityDigest
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
