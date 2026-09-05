using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Factions;

public sealed class FactionRouteQuoteSnapshot
{
    public FactionRouteQuoteSnapshot(
        string factionId,
        FactionRouteKind routeKind,
        string capabilityId,
        int capabilityVersion,
        int cargoAuthoredGold,
        int paymentGold,
        IReadOnlyList<FactionRouteQuoteLineReceipt> quoteLines,
        string sourceDigest,
        string quoteDigest)
    {
        FactionId = factionId ?? string.Empty;
        RouteKind = routeKind;
        CapabilityId = capabilityId ?? string.Empty;
        CapabilityVersion = capabilityVersion;
        CargoAuthoredGold = cargoAuthoredGold;
        PaymentGold = paymentGold;
        QuoteLines = (quoteLines ?? Array.Empty<FactionRouteQuoteLineReceipt>())
            .Select(value => value?.Clone())
            .Where(value => value != null)
            .ToArray();
        SourceDigest = sourceDigest ?? string.Empty;
        QuoteDigest = quoteDigest ?? string.Empty;
    }

    public string FactionId { get; }
    public FactionRouteKind RouteKind { get; }
    public string CapabilityId { get; }
    public int CapabilityVersion { get; }
    public int CargoAuthoredGold { get; }
    public int PaymentGold { get; }
    public IReadOnlyList<FactionRouteQuoteLineReceipt> QuoteLines { get; }
    public string SourceDigest { get; }
    public string QuoteDigest { get; }
}

public interface IFactionRouteEconomicPolicy
{
    string CapabilityId { get; }
    int CapabilityVersion { get; }
    bool TryCreateQuote(
        string factionId,
        FactionRouteKind routeKind,
        IReadOnlyList<FactionCargoLine> cargo,
        out FactionRouteQuoteSnapshot quote,
        out string failureReason);
}

public interface IFactionRouteEconomicPolicyRegistry
{
    bool TryCreateQuote(
        FactionDefinitionSnapshot definition,
        FactionRouteKind routeKind,
        out FactionRouteQuoteSnapshot quote,
        out string failureReason);
}

public sealed class FactionRouteEconomicPolicyRegistry :
    IFactionRouteEconomicPolicyRegistry
{
    private readonly IReadOnlyDictionary<string, IFactionRouteEconomicPolicy> policies;

    public FactionRouteEconomicPolicyRegistry(
        IEnumerable<IFactionRouteEconomicPolicy> policies)
    {
        IFactionRouteEconomicPolicy[] captured = (policies
                ?? throw new ArgumentNullException(nameof(policies)))
            .Where(value => value != null)
            .OrderBy(value => value.CapabilityId, StringComparer.Ordinal)
            .ThenBy(value => value.CapabilityVersion)
            .ToArray();
        if (captured.Length == 0)
            throw new InvalidOperationException(
                "Faction route economic policy registry is empty.");

        Dictionary<string, IFactionRouteEconomicPolicy> indexed =
            new(StringComparer.Ordinal);
        foreach (IFactionRouteEconomicPolicy policy in captured)
        {
            string key = Key(policy.CapabilityId, policy.CapabilityVersion);
            if (!indexed.TryAdd(key, policy))
                throw new InvalidOperationException(
                    "Duplicate faction route economic policy: " + key + ".");
        }
        this.policies = indexed;
    }

    public bool TryCreateQuote(
        FactionDefinitionSnapshot definition,
        FactionRouteKind routeKind,
        out FactionRouteQuoteSnapshot quote,
        out string failureReason)
    {
        quote = null;
        if (definition == null)
        {
            failureReason = "세력 정의가 없습니다.";
            return false;
        }

        FactionRouteEconomicPolicyDescriptor descriptor;
        IReadOnlyList<FactionCargoLine> cargo;
        switch (routeKind)
        {
            case FactionRouteKind.TradeCaravan:
                descriptor = definition.TradeEconomicPolicy;
                cargo = definition.TradeCargo;
                break;
            case FactionRouteKind.SupplyCaravan:
                descriptor = definition.SupplyEconomicPolicy;
                cargo = definition.SupplyCargo;
                break;
            default:
                failureReason = "해당 경로는 경제 견적 대상이 아닙니다.";
                return false;
        }

        string key = Key(
            descriptor?.capabilityId,
            descriptor?.capabilityVersion ?? 0);
        if (!policies.TryGetValue(key, out IFactionRouteEconomicPolicy policy))
        {
            failureReason = "등록되지 않은 세력 경로 경제 정책입니다: " + key;
            return false;
        }
        return policy.TryCreateQuote(
            definition.StableId,
            routeKind,
            cargo,
            out quote,
            out failureReason);
    }

    private static string Key(string capabilityId, int version)
    {
        string id = capabilityId ?? string.Empty;
        if (id.Length == 0
            || !string.Equals(id, id.Trim(), StringComparison.Ordinal)
            || version <= 0)
            return "<invalid>";
        return id + "@" + version.ToString(CultureInfo.InvariantCulture);
    }
}

public sealed class PaidMarketPurchaseFactionRouteEconomicPolicy :
    IFactionRouteEconomicPolicy
{
    private readonly IDungeonItemCatalogProvider items;

    public PaidMarketPurchaseFactionRouteEconomicPolicy(
        IDungeonItemCatalogProvider items) =>
        this.items = items ?? throw new ArgumentNullException(nameof(items));

    public string CapabilityId =>
        FactionRouteEconomicPolicyIds.PaidMarketPurchase;
    public int CapabilityVersion => 1;

    public bool TryCreateQuote(
        string factionId,
        FactionRouteKind routeKind,
        IReadOnlyList<FactionCargoLine> cargo,
        out FactionRouteQuoteSnapshot quote,
        out string failureReason) =>
        FactionRouteQuoteBuilder.TryBuild(
            items,
            factionId,
            routeKind,
            CapabilityId,
            CapabilityVersion,
            cargo,
            paymentRequired: true,
            out quote,
            out failureReason);
}

public sealed class AllianceBenefitFactionRouteEconomicPolicy :
    IFactionRouteEconomicPolicy
{
    private readonly IDungeonItemCatalogProvider items;

    public AllianceBenefitFactionRouteEconomicPolicy(
        IDungeonItemCatalogProvider items) =>
        this.items = items ?? throw new ArgumentNullException(nameof(items));

    public string CapabilityId => FactionRouteEconomicPolicyIds.AllianceBenefit;
    public int CapabilityVersion => 1;

    public bool TryCreateQuote(
        string factionId,
        FactionRouteKind routeKind,
        IReadOnlyList<FactionCargoLine> cargo,
        out FactionRouteQuoteSnapshot quote,
        out string failureReason) =>
        FactionRouteQuoteBuilder.TryBuild(
            items,
            factionId,
            routeKind,
            CapabilityId,
            CapabilityVersion,
            cargo,
            paymentRequired: false,
            out quote,
        out failureReason);
}

public sealed class FactionTradeSettlementRecovery
{
    private readonly IIdempotentGameMoneyAccount money;
    private bool pending;
    private int refundGold;
    private EconomyTransactionContext refundContext;
    private string lastFailure = string.Empty;

    public FactionTradeSettlementRecovery(IIdempotentGameMoneyAccount money) =>
        this.money = money ?? throw new ArgumentNullException(nameof(money));

    public bool IsPending => pending;
    public string LastFailure => lastFailure;
    public string RefundSourceId => refundContext.sourceId ?? string.Empty;

    public void ValidateCanBegin(
        int exactRefundGold,
        EconomyTransactionContext exactRefundContext)
    {
        if (pending
            || exactRefundGold <= 0
            || exactRefundContext.kind
                != EconomyTransactionKind.FactionTradePurchaseRefund
            || string.IsNullOrWhiteSpace(exactRefundContext.sourceId)
            || string.IsNullOrWhiteSpace(exactRefundContext.targetId))
        {
            throw new InvalidOperationException(
                "Faction trade settlement recovery boundary is not ready.");
        }
    }

    public void BeginCommittedDebit(
        int exactRefundGold,
        EconomyTransactionContext exactRefundContext)
    {
        refundGold = exactRefundGold;
        refundContext = exactRefundContext;
        lastFailure = string.Empty;
        pending = true;
    }

    public void CompletePublication()
    {
        pending = false;
        refundGold = 0;
        refundContext = default;
        lastFailure = string.Empty;
    }

    public bool TryResolve()
    {
        if (!pending)
            return true;
        try
        {
            if (!money.TryCreditOnce(
                    refundGold,
                    refundContext,
                    out string failure))
            {
                lastFailure = failure ?? string.Empty;
                return false;
            }
        }
        catch (Exception exception)
        {
            lastFailure = exception.Message;
            return false;
        }

        CompletePublication();
        return true;
    }

    public void EnsureResolved(string operation)
    {
        if (TryResolve())
            return;
        throw new InvalidOperationException(
            $"Faction trade refund '{RefundSourceId}' is still pending "
            + $"and blocks {operation}: {lastFailure}");
    }
}

internal static class FactionRouteQuoteBuilder
{
    internal static bool TryBuild(
        IDungeonItemCatalogProvider items,
        string factionId,
        FactionRouteKind routeKind,
        string capabilityId,
        int capabilityVersion,
        IReadOnlyList<FactionCargoLine> cargo,
        bool paymentRequired,
        out FactionRouteQuoteSnapshot quote,
        out string failureReason)
    {
        quote = null;
        string canonicalFaction = factionId ?? string.Empty;
        if (canonicalFaction.Length == 0
            || !string.Equals(
                canonicalFaction,
                canonicalFaction.Trim(),
                StringComparison.Ordinal)
            || cargo == null
            || cargo.Count == 0)
        {
            failureReason = "세력 또는 화물 견적 입력이 비어 있습니다.";
            return false;
        }

        List<FactionRouteQuoteLineReceipt> lines = new(cargo.Count);
        HashSet<string> itemIds = new(StringComparer.Ordinal);
        int total = 0;
        foreach (FactionCargoLine line in cargo)
        {
            string itemId = line?.itemId ?? string.Empty;
            if (line == null
                || line.amount <= 0
                || itemId.Length == 0
                || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
                || !itemIds.Add(itemId)
                || !items.TryGetDefinition(itemId, out DungeonItemDefinition item)
                || item == null
                || item.UnitPrice <= 0)
            {
                failureReason = "화물에 중복·미등록·무가격 품목이 있습니다: " + itemId;
                return false;
            }
            try
            {
                total = checked(total + checked(line.amount * item.UnitPrice));
            }
            catch (OverflowException)
            {
                failureReason = "화물 견적이 정수 범위를 초과합니다.";
                return false;
            }
            lines.Add(new FactionRouteQuoteLineReceipt
            {
                itemId = itemId,
                amount = line.amount,
                unitPriceGold = item.UnitPrice
            });
        }

        lines.Sort((left, right) =>
            string.Compare(left.itemId, right.itemId, StringComparison.Ordinal));
        int payment = paymentRequired ? total : 0;
        if (!FactionPayloadValidation.TryCalculateSettlementDigests(
                canonicalFaction,
                routeKind,
                capabilityId,
                capabilityVersion,
                lines,
                payment,
                out int verifiedTotal,
                out string sourceDigest,
                out string quoteDigest)
            || verifiedTotal != total)
        {
            failureReason = "화물 견적의 동결 영수증을 생성하지 못했습니다.";
            return false;
        }
        quote = new FactionRouteQuoteSnapshot(
            canonicalFaction,
            routeKind,
            capabilityId,
            capabilityVersion,
            total,
            payment,
            lines,
            sourceDigest,
            quoteDigest);
        failureReason = string.Empty;
        return true;
    }

}
