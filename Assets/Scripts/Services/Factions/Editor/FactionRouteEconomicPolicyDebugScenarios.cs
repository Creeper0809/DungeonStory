#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Factions;
using UnityEditor;
using UnityEngine;

public static class FactionRouteEconomicPolicyDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Factions/Verify Route Economic Policies")]
    public static void RunAll() => Debug.Log(Verify());

    public static string Verify()
    {
        IDungeonItemCatalogProvider items = EditorItemCatalogFactory.Create();
        IFactionRouteEconomicPolicy paid =
            new PaidMarketPurchaseFactionRouteEconomicPolicy(items);
        IFactionRouteEconomicPolicy benefit =
            new AllianceBenefitFactionRouteEconomicPolicy(items);
        FactionRouteEconomicPolicyRegistry forward = new(new[] { paid, benefit });
        FactionRouteEconomicPolicyRegistry reverse = new(new[] { benefit, paid });

        DungeonFactionDefinitionSO[] assets = AssetDatabase
            .FindAssets(
                "t:DungeonFactionDefinitionSO",
                new[] { "Assets/Resources/SO/Factions/Dungeons" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<DungeonFactionDefinitionSO>)
            .ToArray();
        Require(assets.Length == 6 && assets.All(value => value != null),
            "Expected six authored faction definitions.");

        foreach (DungeonFactionDefinitionSO asset in assets)
        {
            FactionDefinitionSnapshot definition = asset.ToSnapshot();
            Require(forward.TryCreateQuote(
                    definition,
                    FactionRouteKind.TradeCaravan,
                    out FactionRouteQuoteSnapshot trade,
                    out string tradeFailure),
                tradeFailure);
            Require(forward.TryCreateQuote(
                    definition,
                    FactionRouteKind.SupplyCaravan,
                    out FactionRouteQuoteSnapshot supply,
                    out string supplyFailure),
                supplyFailure);
            Require(reverse.TryCreateQuote(
                    definition,
                    FactionRouteKind.TradeCaravan,
                    out FactionRouteQuoteSnapshot reverseTrade,
                    out string reverseFailure),
                reverseFailure);
            Require(trade.PaymentGold > 0
                    && trade.PaymentGold == trade.CargoAuthoredGold
                    && supply.PaymentGold == 0
                    && supply.CargoAuthoredGold > 0
                    && trade.SourceDigest.Length == 64
                    && trade.QuoteDigest.Length == 64
                    && supply.SourceDigest.Length == 64
                    && supply.QuoteDigest.Length == 64
                    && string.Equals(
                        trade.QuoteDigest,
                        reverseTrade.QuoteDigest,
                        StringComparison.Ordinal),
                "Authored route quote or registry-order determinism failed for "
                + definition.StableId + ".");
        }

        Require(Throws(() => new FactionRouteEconomicPolicyRegistry(
                new[] { paid, paid })),
            "Duplicate economic policy registration was accepted.");
        FactionDefinitionSnapshot source = assets[0].ToSnapshot();
        FactionDefinitionSnapshot missing = new(
            source.StableId,
            source.DisplayName,
            source.SpeciesTag,
            source.Description,
            source.RelationTags,
            source.TradeTags,
            source.ReinforcementRole,
            source.TradeCargo,
            source.SupplyCargo,
            FactionRouteEconomicPolicyDescriptor.Create(
                "faction-economy:missing",
                1),
            source.SupplyEconomicPolicy,
            source.TradeCooldownDays,
            source.SupplyCooldownDays,
            source.ReinforcementCooldownDays);
        Require(!forward.TryCreateQuote(
                missing,
                FactionRouteKind.TradeCaravan,
                out _,
                out string missingReason)
                && !string.IsNullOrWhiteSpace(missingReason),
            "Missing economic policy did not fail loudly.");
        ValidateSettlementRecovery();
        return "FACTION_ROUTE_ECONOMIC_POLICY_PASS";
    }

    private static void ValidateSettlementRecovery()
    {
        RecoveryMoneyAccount money = new() { FailedCreditsRemaining = 2 };
        FactionTradeSettlementRecovery recovery = new(money);
        EconomyTransactionContext refund = new(
            EconomyTransactionKind.FactionTradePurchaseRefund,
            "faction-route-settlement:00000001:refund",
            "faction:dungeon:test");
        recovery.ValidateCanBegin(17, refund);
        recovery.BeginCommittedDebit(17, refund);
        Require(!recovery.TryResolve() && recovery.IsPending,
            "First injected faction refund failure was not retained.");
        Require(Throws(() => recovery.EnsureResolved("save capture"))
                && recovery.IsPending,
            "Pending faction refund did not block save capture.");
        Require(recovery.TryResolve()
                && !recovery.IsPending
                && money.CreditCalls == 3
                && money.CreditedGold == 17,
            "Faction refund did not forward-retry to one exact credit.");
        Require(recovery.TryResolve() && money.CreditCalls == 3,
            "Completed faction refund replay issued another credit.");

        recovery.ValidateCanBegin(9, refund);
        recovery.BeginCommittedDebit(9, refund);
        recovery.CompletePublication();
        Require(recovery.TryResolve()
                && money.CreditCalls == 3
                && money.CreditedGold == 17,
            "Successful faction route publication issued an unwanted refund.");
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
            throw new InvalidOperationException(failure);
    }

    private sealed class RecoveryMoneyAccount : IIdempotentGameMoneyAccount
    {
        public int FailedCreditsRemaining { get; set; }
        public int CreditCalls { get; private set; }
        public int CreditedGold { get; private set; }
        public int Balance => CreditedGold;
        public bool CanSpend(int amount) => false;
        public bool TrySpend(int amount, out string reason)
        {
            reason = "not used";
            return false;
        }
        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason) => TrySpend(amount, out reason);
        public bool TrySpendOnce(
            int amount,
            EconomyTransactionContext context,
            out EconomyTransactionRecord receipt,
            out string reason)
        {
            receipt = null;
            reason = "not used";
            return false;
        }
        public bool TryCreditOnce(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            CreditCalls++;
            if (FailedCreditsRemaining-- > 0)
            {
                reason = "injected credit failure";
                return false;
            }
            if (CreditedGold == 0)
                CreditedGold = amount;
            else if (CreditedGold != amount)
            {
                reason = "conflicting replay";
                return false;
            }
            reason = string.Empty;
            return true;
        }
        public void Add(int amount) => throw new NotSupportedException();
        public void Add(int amount, EconomyTransactionContext context) =>
            throw new NotSupportedException();
        public void SetBalance(
            int amount,
            EconomyTransactionContext context) =>
            throw new NotSupportedException();
    }
}
#endif
