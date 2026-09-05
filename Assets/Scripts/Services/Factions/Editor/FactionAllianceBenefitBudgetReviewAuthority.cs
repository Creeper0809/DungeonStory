#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Balance;
using DungeonStory.Factions;
using UnityEditor;

public static class FactionAllianceBenefitBudgetReviewAuthority
{
    public static FactionAllianceBenefitBudgetReviewSnapshot Capture(
        FrozenBalanceLedger ledger)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));

        Dictionary<string, CanonicalBalanceMetricRecord> acquisition = ledger.Records
            .Where(value => string.Equals(
                value.Metric,
                "acquisition-cost",
                StringComparison.Ordinal))
            .ToDictionary(value => value.StableId, StringComparer.Ordinal);
        IDungeonItemCatalogProvider items = EditorItemCatalogFactory.Create();
        FactionRouteEconomicPolicyRegistry policies = new(new IFactionRouteEconomicPolicy[]
        {
            new AllianceBenefitFactionRouteEconomicPolicy(items)
        });
        FactionDefinitionSnapshot[] definitions = AssetDatabase
            .FindAssets(
                "t:DungeonFactionDefinitionSO",
                new[] { "Assets/Resources/SO/Factions/Dungeons" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<DungeonFactionDefinitionSO>)
            .Where(value => value != null)
            .Select(value => value.ToSnapshot())
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        if (definitions.Length == 0)
        {
            throw new InvalidOperationException(
                "Faction alliance-benefit review authority has no faction definitions.");
        }

        List<FactionAllianceBenefitBudgetReviewRoute> routes = new();
        foreach (FactionDefinitionSnapshot definition in definitions)
        {
            if (!policies.TryCreateQuote(
                    definition,
                    FactionRouteKind.SupplyCaravan,
                    out FactionRouteQuoteSnapshot quote,
                    out string failure)
                || quote.PaymentGold != 0
                || quote.CargoAuthoredGold <= 0)
            {
                throw new InvalidOperationException(
                    "Faction alliance-benefit Supply quote is invalid for "
                    + definition.StableId + ": " + failure);
            }

            long debit = 0L;
            FactionCargoLine[] cargo = definition.SupplyCargo
                .Where(value => value != null)
                .OrderBy(value => value.itemId, StringComparer.Ordinal)
                .ThenBy(value => value.amount)
                .ToArray();
            foreach (FactionCargoLine line in cargo)
            {
                if (line.amount <= 0
                    || !acquisition.TryGetValue(
                        line.itemId,
                        out CanonicalBalanceMetricRecord item))
                {
                    throw new InvalidOperationException(
                        "Faction alliance-benefit cargo has no acquisition authority: "
                        + definition.StableId + "/" + line.itemId + ".");
                }
                long unit = long.Parse(
                    item.After,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture);
                debit = checked(debit + checked(unit * line.amount));
            }
            routes.Add(new FactionAllianceBenefitBudgetReviewRoute(
                definition.StableId,
                definition.SupplyCooldownDays,
                quote.SourceDigest,
                debit,
                cargo));
        }

        long capacity = routes.Aggregate(
            0L,
            (sum, route) => checked(sum + route.DebitMilliEwu));
        long denominator = routes.Aggregate(
            1L,
            (value, route) => CheckedLcm(value, route.CooldownDays));
        long numerator = routes.Aggregate(
            0L,
            (sum, route) => checked(sum + checked(
                route.DebitMilliEwu
                * checked(denominator / route.CooldownDays))));
        long divisor = GreatestCommonDivisor(numerator, denominator);
        numerator /= divisor;
        denominator /= divisor;

        StringBuilder canonical = new();
        canonical.Append("schema|faction-alliance-benefit-budget-source|1");
        foreach (FactionAllianceBenefitBudgetReviewRoute route in routes)
        {
            canonical.Append('\n').Append("route|")
                .Append(route.FactionId).Append('|')
                .Append(route.CooldownDays.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(route.DebitMilliEwu.ToString(CultureInfo.InvariantCulture));
            foreach (FactionCargoLine line in route.Cargo)
            {
                CanonicalBalanceMetricRecord item = acquisition[line.itemId];
                canonical.Append('\n').Append("item|")
                    .Append(route.FactionId).Append('|')
                    .Append(line.itemId).Append('|')
                    .Append(line.amount.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(item.After).Append('|')
                    .Append(item.ReviewStatus).Append('|')
                    .Append(item.AnomalyDisposition).Append('|')
                    .Append(item.SourceDigest).Append('|')
                    .Append(item.SemanticHash);
            }
        }
        canonical.Append('\n').Append("capacity-mewu|")
            .Append(capacity.ToString(CultureInfo.InvariantCulture));
        canonical.Append('\n').Append("daily-refill-mewu-rational|")
            .Append(numerator.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(denominator.ToString(CultureInfo.InvariantCulture));

        return new FactionAllianceBenefitBudgetReviewSnapshot(
            Hash(canonical.ToString()),
            capacity,
            numerator,
            denominator,
            routes);
    }

    private static long CheckedLcm(long left, long right)
    {
        if (left <= 0 || right <= 0)
            throw new InvalidOperationException("Budget cooldown must be positive.");
        return checked(left / GreatestCommonDivisor(left, right) * right);
    }

    private static long GreatestCommonDivisor(long left, long right)
    {
        left = Math.Abs(left);
        right = Math.Abs(right);
        while (right != 0)
        {
            long remainder = left % right;
            left = right;
            right = remainder;
        }
        return left == 0 ? 1 : left;
    }

    private static string Hash(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(new UTF8Encoding(false, true).GetBytes(value));
        StringBuilder result = new(digest.Length * 2);
        foreach (byte item in digest)
            result.Append(item.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }
}

public sealed class FactionAllianceBenefitBudgetReviewSnapshot
{
    public FactionAllianceBenefitBudgetReviewSnapshot(
        string sourceDigest,
        long capacityMilliEwu,
        long refillNumeratorMilliEwu,
        long refillDenominatorDays,
        IReadOnlyList<FactionAllianceBenefitBudgetReviewRoute> routes)
    {
        SourceDigest = sourceDigest ?? string.Empty;
        CapacityMilliEwu = capacityMilliEwu;
        RefillNumeratorMilliEwu = refillNumeratorMilliEwu;
        RefillDenominatorDays = refillDenominatorDays;
        Routes = routes ?? Array.Empty<FactionAllianceBenefitBudgetReviewRoute>();
    }

    public string SourceDigest { get; }
    public long CapacityMilliEwu { get; }
    public long RefillNumeratorMilliEwu { get; }
    public long RefillDenominatorDays { get; }
    public IReadOnlyList<FactionAllianceBenefitBudgetReviewRoute> Routes { get; }
}

public sealed class FactionAllianceBenefitBudgetReviewRoute
{
    public FactionAllianceBenefitBudgetReviewRoute(
        string factionId,
        int cooldownDays,
        string supplyQuoteSourceDigest,
        long debitMilliEwu,
        IReadOnlyList<FactionCargoLine> cargo)
    {
        FactionId = factionId ?? string.Empty;
        CooldownDays = cooldownDays;
        SupplyQuoteSourceDigest = supplyQuoteSourceDigest ?? string.Empty;
        DebitMilliEwu = debitMilliEwu;
        Cargo = cargo ?? Array.Empty<FactionCargoLine>();
    }

    public string FactionId { get; }
    public int CooldownDays { get; }
    public string SupplyQuoteSourceDigest { get; }
    public long DebitMilliEwu { get; }
    public IReadOnlyList<FactionCargoLine> Cargo { get; }
}
#endif
