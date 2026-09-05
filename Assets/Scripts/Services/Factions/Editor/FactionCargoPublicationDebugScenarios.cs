using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Factions;
using UnityEditor;
using UnityEngine;

public static class FactionCargoPublicationDebugScenarios
{
    [MenuItem("Dungeon Story/QA/V27/Faction Exact Cargo Publication")]
    public static void Verify()
    {
        MethodInfo quantityMethod = typeof(FactionRuntimeApplicationAdapter)
            .GetMethod(
                "ResolveCargoDeliveryQuantity",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Faction cargo quantity projector is missing.");
        int Q(int amount, int strength) => (int)quantityMethod.Invoke(
            null,
            new object[] { amount, strength });
        Require(Q(3, 50) == 2
            && Q(5, 50) == 2
            && Q(7, 50) == 4,
            "Faction cargo strength scaling no longer matches midpoint-to-even semantics.");

        FactionRouteState route = new()
        {
            routeId = "faction-route:17",
            strength = 100,
            cargo = new List<FactionCargoLine>
            {
                new() { itemId = "material:iron-ingot", amount = 3 },
                new() { itemId = "material:lumber", amount = 5 },
                new() { itemId = "food:preserved-ration", amount = 7 }
            }
        };
        MethodInfo planMethod = typeof(FactionRuntimeApplicationAdapter)
            .GetMethod(
                "CreateCargoPublicationPlan",
                BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Faction cargo exact publication planner is missing.");
        PhysicalItemExactSourcePublicationPlan plan =
            (PhysicalItemExactSourcePublicationPlan)planMethod.Invoke(
                null,
                new object[] { route, new Vector2Int(9, 4) });
        Require(plan.OwnerDomain == "faction.route-cargo"
            && plan.OwnerOperationId == route.routeId
            && plan.DropPosition == new Vector2Int(9, 4)
            && plan.Outputs.Count == 3
            && plan.Outputs.Select(value => value.OutputLineId)
                .SequenceEqual(new[]
                {
                    "cargo:0000:material:iron-ingot",
                    "cargo:0001:material:lumber",
                    "cargo:0002:food:preserved-ration"
                }),
            "Faction cargo was not planned as one deterministic whole vector.");

        string runtimePath = Path.Combine(
            Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Project root is missing."),
            "Assets/Scripts/Services/Factions/FactionRuntime.cs");
        string source = File.ReadAllText(runtimePath);
        int start = source.IndexOf(
            "private bool TryDeliverCargo",
            StringComparison.Ordinal);
        int end = source.IndexOf(
            "private void MaterializeReinforcements",
            start,
            StringComparison.Ordinal);
        Require(start >= 0 && end > start, "Faction cargo method boundary drifted.");
        string cargoSection = source.Substring(start, end - start);
        Require(!cargoSection.Contains("itemSpawner.Spawn", StringComparison.Ordinal)
            && cargoSection.Contains("exactSources.TryPrepare", StringComparison.Ordinal)
            && cargoSection.Contains("exactSources.TryCommitReleased", StringComparison.Ordinal)
            && cargoSection.Contains(
                "FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned",
                StringComparison.Ordinal),
            "Faction cargo publication bypassed the one-vector exact source boundary.");

        ValidateDeliveredSaveReceipt();

        Debug.Log("FACTION_EXACT_CARGO_PUBLICATION_PASS");
    }

    private static void ValidateDeliveredSaveReceipt()
    {
        IDungeonItemCatalogProvider items = EditorItemCatalogFactory.Create();
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
        FactionDefinitionSnapshot definition = definitions[0];
        FactionRouteEconomicPolicyRegistry policies = new(new[]
        {
            new PaidMarketPurchaseFactionRouteEconomicPolicy(items)
        });
        Require(policies.TryCreateQuote(
                definition,
                FactionRouteKind.TradeCaravan,
                out FactionRouteQuoteSnapshot quote,
                out string quoteFailure),
            quoteFailure);
        FactionRouteState route = new()
        {
            routeId = "faction-route:1",
            factionId = definition.StableId,
            kind = FactionRouteKind.TradeCaravan,
            status = FactionRouteStatus.Arrived,
            path = new List<FactionHexCoordSaveData>
            {
                new() { q = 4, r = -2 },
                new() { q = 3, r = -1 }
            },
            pathIndex = 1,
            strength = 100,
            createdDay = 1,
            estimatedArrivalDay = 2,
            cargo = definition.TradeCargo
                .Select(value => value.Clone())
                .OrderBy(value => value.itemId, StringComparer.Ordinal)
                .ToList(),
            settlement = new FactionRouteSettlementReceipt
            {
                state = FactionRouteSettlementState.Paid,
                capabilityId = quote.CapabilityId,
                capabilityVersion = quote.CapabilityVersion,
                operationSequence = 1,
                cargoAuthoredGold = quote.CargoAuthoredGold,
                paymentGold = quote.PaymentGold,
                quoteLines = quote.QuoteLines
                    .Select(value => value.Clone())
                    .ToList(),
                sourceDigest = quote.SourceDigest,
                quoteDigest = quote.QuoteDigest,
                transactionId = "economy-transaction:faction-cargo-fixture",
                transactionSourceId =
                    "faction-route-settlement:00000001",
                transactionTargetId = definition.StableId,
                balanceBefore = 10000,
                balanceAfter = 10000 - quote.PaymentGold
            }
        };
        MethodInfo planMethod = typeof(FactionRuntimeApplicationAdapter)
            .GetMethod(
                "CreateCargoPublicationPlan",
                BindingFlags.Static | BindingFlags.NonPublic);
        PhysicalItemExactSourcePublicationPlan plan =
            (PhysicalItemExactSourcePublicationPlan)planMethod.Invoke(
                null,
                new object[] { route, new Vector2Int(6, 8) });
        List<ProductionDomainPublishedStackSaveData> stacks = plan.Outputs
            .Select((output, index) =>
                new ProductionDomainPublishedStackSaveData
                {
                    outputLineId = output.OutputLineId,
                    itemId = output.ItemDefinitionId.Value,
                    itemInstanceId = string.Empty,
                    stackId = $"stack:faction-cargo:{index:D4}",
                    quantity = output.Quantity,
                    massGrams = checked(output.Quantity * 100L)
                })
            .ToList();
        route.cargoDelivery = new FactionRouteCargoDeliveryReceipt
        {
            state = FactionRouteCargoDeliveryState.Delivered,
            batchCommitId = plan.BatchCommitId,
            destinationId = plan.DestinationId,
            outcomeFingerprint = plan.OutcomeFingerprint,
            deliveryX = plan.DropPosition.x,
            deliveryY = plan.DropPosition.y,
            totalMassGrams = stacks.Sum(value => value.massGrams),
            stacks = stacks
        };
        DungeonFactionSaveData payload = new()
        {
            currentDay = 3,
            routeSequence = 1,
            routeSettlementOperationSequence = 1,
            allianceBenefitBalanceMilliEwu = 39142546L,
            allianceBenefitRefillRemainder = 0L,
            allianceBenefitLastRefillDay = 3,
            allianceBenefitAuthorityDigest =
                "c539c892bb0b8355801c923c3a86da8f2a331ed459414684aa2dc60d0767fe15",
            factions = definitions.Select(value => new DungeonFactionState
            {
                factionId = value.StableId,
                homeQ = 4,
                homeR = -2
            }).ToList(),
            routes = new List<FactionRouteState> { route }
        };
        Require(FactionPayloadValidation.Validate(
                payload,
                definitions,
                _ => true).Count == 0,
            "terminal whole-vector cargo receipt was rejected");
        route.cargoDelivery.outcomeFingerprint = new string('0', 64);
        Require(FactionPayloadValidation.Validate(
                payload,
                definitions,
                _ => true).Count > 0,
            "tampered whole-vector outcome fingerprint was accepted");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
