#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Balance;
using DungeonStory.Factions;
using UnityEditor;
using UnityEngine;

public static class V27BalanceMarketDebugScenarios
{
    public const string ReportPath = "Artifacts/QA/v27-balance-market-authority.txt";
    public const string ReviewBundleCsvPath =
        "Artifacts/QA/v27-balance-market-review-bundles.csv";
    public const string ReviewBundleReportPath =
        "Artifacts/QA/v27-balance-market-review-bundles.txt";

    [MenuItem("DungeonStory/V27/Verify Applied Item Market Authority")]
    public static void RunAll()
    {
        List<string> rows = new List<string>();
        List<string> failures = new List<string>();
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        Check(
            audit.IntegrityFailures.Count == 0 && audit.CriticalCount == 0,
            "MARKET_AUDIT_INTEGRITY_ZERO",
            $"integrity={audit.IntegrityFailures.Count}; critical={audit.CriticalCount}",
            rows,
            failures);

        IReadOnlyList<CanonicalBalanceMetricRecord> records = audit.Ledger.Records;
        VerifyAppliedMetric(records, "authored-unit-price-gold", rows, failures);
        VerifyAppliedMetric(records, "authored-market-sale-rate", rows, failures);
        VerifyAppliedMetric(records, "authored-retail-cost-gold", rows, failures);
        VerifyAppliedMetric(records, "authored-daily-unit-cost-gold", rows, failures);
        VerifyAppliedMetric(records, "authored-money-reward-gold", rows, failures);

        Dictionary<string, long> acquisitionById = records
            .Where(value => value.Metric == "acquisition-cost")
            .ToDictionary(
                value => value.StableId,
                value => long.Parse(value.After, CultureInfo.InvariantCulture),
                StringComparer.Ordinal);
        CanonicalBalanceMetricRecord[] saleCredits = records
            .Where(value => value.Metric == "market-sale-credit")
            .ToArray();
        bool saleFloorExact = saleCredits.Length > 0;
        foreach (CanonicalBalanceMetricRecord credit in saleCredits)
        {
            if (!acquisitionById.TryGetValue(credit.StableId, out long acquisition))
            {
                saleFloorExact = false;
                break;
            }
            long target = V27EwuQuantizer.MultiplyOutputCredit(
                EwuAmount.FromMilliEwu(acquisition),
                0.60m).MilliEwu;
            long actual = long.Parse(credit.After, CultureInfo.InvariantCulture);
            if (actual > target)
            {
                saleFloorExact = false;
                break;
            }
        }
        Check(
            saleFloorExact,
            "MARKET_SALE_OUTPUT_FLOOR_EXACT",
            $"rows={saleCredits.Length}; targetRecovery=0.60",
            rows,
            failures);

        FakeStockCatalog stockCatalog = new FakeStockCatalog(
            new StockCategoryDefinition(
                "v27-ceil-probe",
                StockCategory.Food,
                "V27 Ceil Probe",
                "Ceil",
                0,
                1f,
                "food:grain-porridge",
                1,
                1.01f,
                1));
        StockDeliveryOffer offer = StockSupplyService.CreateDailyDeliveryOffers(
                1,
                _ => 1f,
                stockCatalog)
            .Single();
        Check(
            offer.cost == 2,
            "MARKET_PROCUREMENT_RUNTIME_INPUT_CEIL",
            $"amount={offer.amount}; unitCost=1.01; cost={offer.cost}",
            rows,
            failures);

        string first = failures.Count == 0
            ? "RESULT=PASS; failures=0"
            : $"RESULT=FAIL; failures={failures.Count}";
        string text = first + "\n" + string.Join("\n", rows) + "\n";
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(bytes, 0, bytes.Length));
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        if (failures.Count > 0)
            throw new InvalidOperationException("V27 market authority failed:\n" + string.Join("\n", failures));
        Debug.Log($"V27 market authority PASS; rows={records.Count}.");
    }

    [MenuItem("DungeonStory/V27/Verify Market Applied Candidate Separation")]
    public static void VerifyAppliedCandidateSeparationFromMenu()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        if (audit.IntegrityFailures.Count != 0)
        {
            throw new InvalidOperationException(
                "V27 market candidate separation has ledger integrity failures:\n"
                + string.Join("\n", audit.IntegrityFailures));
        }

        int candidateCount = RequireAppliedCandidateSeparation(audit.Ledger.Records);
        RequireCausalDependencyRootPromotion(audit.Anomalies);
        Debug.Log(
            "V27 market applied/candidate separation PASS; "
            + $"candidates={candidateCount}; unresolvedCritical={audit.CriticalCount}; "
            + "no asset mutation performed.");
    }

    [MenuItem("DungeonStory/V27/Generate Market Review Bundles")]
    public static void GenerateMarketReviewBundlesFromMenu()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        if (audit.IntegrityFailures.Count != 0)
        {
            throw new InvalidOperationException(
                "Cannot generate market review bundles from an invalid ledger:\n"
                + string.Join("\n", audit.IntegrityFailures));
        }

        MarketReviewBundleRow[] rows = BuildMarketReviewBundleRows(
            audit.Ledger.Records);
        RequireMarketReviewBundlePartition(rows);
        WriteMarketReviewBundleCsv(rows);
        WriteMarketReviewBundleReport(rows, audit);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log(
            "V27 market review bundles generated without asset mutation: "
            + $"rows={rows.Length}; bundles="
            + $"{rows.Select(value => value.BundleId).Distinct(StringComparer.Ordinal).Count()}; "
            + $"critical={audit.CriticalCount}.");
    }

    internal static MarketReviewBundleRow[] BuildMarketReviewBundleRows(
        IReadOnlyList<CanonicalBalanceMetricRecord> records)
    {
        CanonicalBalanceMetricRecord[] candidates = records
            .Where(value => value.Metric.StartsWith(
                V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix,
                StringComparison.Ordinal))
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ThenBy(value => value.Metric, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, CanonicalBalanceMetricRecord> acquisitionById = records
            .Where(value => string.Equals(
                value.Metric,
                "acquisition-cost",
                StringComparison.Ordinal))
            .ToDictionary(value => value.StableId, StringComparer.Ordinal);
        Dictionary<string, CanonicalBalanceMetricRecord> liveCreditById = records
            .Where(value => string.Equals(
                value.Metric,
                "market-sale-credit",
                StringComparison.Ordinal))
            .ToDictionary(value => value.StableId, StringComparer.Ordinal);
        Dictionary<string, CanonicalBalanceMetricRecord> candidateCreditById = records
            .Where(value => string.Equals(
                value.Metric,
                V27BalanceAudit.MarketDerivedRecalibrationCandidateMetricPrefix
                    + "market-sale-credit",
                StringComparison.Ordinal))
            .ToDictionary(value => value.StableId, StringComparer.Ordinal);
        HashSet<string> priceCandidateIds = candidates
            .Where(value => value.Metric.EndsWith(
                "authored-unit-price-gold",
                StringComparison.Ordinal))
            .Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string[]> dependencyGraph = BuildEconomicDependencyGraph(records);
        HashSet<string> allWuAuthorityIds = records
            .Where(value => string.Equals(
                value.Metric,
                "authored-required-wu",
                StringComparison.Ordinal))
            .Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> reviewedWuAuthorityIds = records
            .Where(value => string.Equals(
                    value.Metric,
                    "authored-required-wu",
                    StringComparison.Ordinal)
                && string.Equals(value.AssetApplied, "true", StringComparison.Ordinal)
                && (string.Equals(
                        value.ReviewStatus,
                        "approved",
                        StringComparison.Ordinal)
                    || string.Equals(
                        value.ReviewStatus,
                        "implemented",
                        StringComparison.Ordinal)))
            .Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        ExternalInflowClosureSnapshot externalInflows =
            CaptureExternalInflowEconomicClosure();

        List<MarketReviewBundleRow> output = new List<MarketReviewBundleRow>(
            candidates.Length);
        foreach (CanonicalBalanceMetricRecord candidate in candidates)
        {
            string authorityMetric = candidate.Metric.Substring(
                V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix.Length);
            string anchorItemId = ResolveMarketReviewAnchor(
                candidate,
                authorityMetric);
            acquisitionById.TryGetValue(
                anchorItemId,
                out CanonicalBalanceMetricRecord acquisition);
            bool quarryCascade = DependsOn(
                anchorItemId,
                "source:quarry",
                dependencyGraph);
            string[] externalInflowRoots = externalInflows.AllItemIds
                .Where(value => DependsOn(anchorItemId, value, dependencyGraph))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            bool liveOverTarget = false;
            if (acquisition != null
                && liveCreditById.TryGetValue(
                    anchorItemId,
                    out CanonicalBalanceMetricRecord liveCredit))
            {
                long acquisitionMilli = long.Parse(
                    acquisition.After,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture);
                long liveCreditMilli = long.Parse(
                    liveCredit.After,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture);
                long targetCredit = V27EwuQuantizer.MultiplyOutputCredit(
                    EwuAmount.FromMilliEwu(acquisitionMilli),
                    0.60m).MilliEwu;
                liveOverTarget = liveCreditMilli > targetCredit;
            }

            string cohort;
            if (string.Equals(
                    authorityMetric,
                    "authored-unit-price-gold",
                    StringComparison.Ordinal))
            {
                bool formulaCleanLeaf = acquisition != null
                    && string.Equals(
                        acquisition.AnomalyDisposition,
                        "none",
                        StringComparison.Ordinal)
                    && string.Equals(
                        acquisition.DownstreamConsumerCount,
                        "0",
                        StringComparison.Ordinal);
                cohort = formulaCleanLeaf
                    ? "price-formula-clean-leaf"
                    : "price-dependency-review";
            }
            else if (string.Equals(
                         authorityMetric,
                         "authored-market-sale-rate",
                         StringComparison.Ordinal))
            {
                cohort = priceCandidateIds.Contains(candidate.StableId)
                    ? "sale-rate-with-price"
                    : "sale-rate-only";
            }
            else
            {
                cohort = "market-consumer";
            }

            string bundleId = "market-atomic:" + anchorItemId;
            decimal absoluteDelta = Math.Abs(decimal.Parse(
                candidate.PercentDelta,
                NumberStyles.Float,
                CultureInfo.InvariantCulture));
            List<string> risks = new List<string>();
            if (quarryCascade)
                risks.Add("quarry-cascade");
            if (absoluteDelta > 300m)
                risks.Add("delta-over-300pct");
            if (acquisition != null
                && !string.Equals(
                    acquisition.AnomalyDisposition,
                    "none",
                    StringComparison.Ordinal))
            {
                risks.Add("acquisition-" + acquisition.AnomalyDisposition);
            }
            if (liveOverTarget)
                risks.Add("live-recovery-over-60pct");
            if (risks.Count == 0)
                risks.Add("formula-review-only");

            output.Add(new MarketReviewBundleRow(
                bundleId,
                anchorItemId,
                string.Empty,
                cohort,
                candidate.ReasonCode == "market-authority-provenance-missing"
                    ? "provenance-missing"
                    : "previous-applied",
                candidate.StableId,
                authorityMetric,
                candidate.Before,
                candidate.After,
                candidate.PercentDelta,
                candidate.SourceAuthority,
                candidate.SourcePropertyPath,
                candidate.ExactFormula,
                candidate.DependencyFingerprint,
                candidate.SourceDigest,
                candidate.SemanticHash,
                string.Join("|", candidate.DependencyIds),
                string.Join("|", (quarryCascade
                        ? new[] { "source:quarry" }.Concat(externalInflowRoots)
                        : externalInflowRoots)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)),
                acquisition?.AnomalyDisposition ?? "not-applicable",
                acquisition?.DownstreamConsumerCount ?? "not-applicable",
                string.Join("|", risks.OrderBy(value => value, StringComparer.Ordinal)),
                "pending-explicit-review"));
        }
        Dictionary<string, string> shapeByBundle = output
            .GroupBy(value => value.BundleId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => BuildMarketMemberShape(group),
                StringComparer.Ordinal);
        Dictionary<string, string> digestByBundle = output
            .GroupBy(value => value.BundleId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => BuildMarketBundleDigest(group),
                StringComparer.Ordinal);
        Dictionary<string, (string Decision, string Reason)> recommendationByBundle =
            output.GroupBy(value => value.BundleId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => BuildMarketRecommendation(
                        group,
                        liveCreditById,
                        candidateCreditById,
                        acquisitionById,
                        dependencyGraph,
                        allWuAuthorityIds,
                        reviewedWuAuthorityIds,
                        externalInflows),
                    StringComparer.Ordinal);
        return output
            .Select(value => value.WithBundleMetadata(
                shapeByBundle[value.BundleId],
                digestByBundle[value.BundleId],
                recommendationByBundle[value.BundleId].Decision,
                recommendationByBundle[value.BundleId].Reason))
            .OrderBy(value => value.Cohort, StringComparer.Ordinal)
            .ThenBy(value => value.BundleId, StringComparer.Ordinal)
            .ThenBy(value => value.StableId, StringComparer.Ordinal)
            .ThenBy(value => value.AuthorityMetric, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveMarketReviewAnchor(
        CanonicalBalanceMetricRecord candidate,
        string authorityMetric)
    {
        if (string.Equals(authorityMetric, "authored-unit-price-gold", StringComparison.Ordinal)
            || string.Equals(authorityMetric, "authored-market-sale-rate", StringComparison.Ordinal))
        {
            return candidate.StableId;
        }

        string[] anchors = candidate.DependencyIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (anchors.Length != 1)
        {
            throw new InvalidOperationException(
                "Market consumer candidate must identify exactly one item anchor: "
                + $"{candidate.StableId}/{authorityMetric}; anchors={string.Join("|", anchors)}.");
        }
        return anchors[0];
    }

    private static string BuildMarketMemberShape(
        IEnumerable<MarketReviewBundleRow> rows)
    {
        return string.Concat(rows
            .Select(value => MarketMemberCode(value.AuthorityMetric))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => "GPRST".IndexOf(value, StringComparison.Ordinal)));
    }

    private static string BuildMarketBundleDigest(
        IEnumerable<MarketReviewBundleRow> rows)
    {
        string canonical = string.Join("\n", rows
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ThenBy(value => value.AuthorityMetric, StringComparer.Ordinal)
            .Select(value => string.Join("|", new[]
            {
                value.BundleId,
                value.AnchorItemId,
                value.StableId,
                value.AuthorityMetric,
                value.Before,
                value.Candidate,
                value.DependencyFingerprint,
                value.SourceDigest,
                value.SemanticHash
            })));
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(canonical));
        StringBuilder hex = new StringBuilder(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        return hex.ToString();
    }

    private static (string Decision, string Reason) BuildMarketRecommendation(
        IEnumerable<MarketReviewBundleRow> rows,
        IReadOnlyDictionary<string, CanonicalBalanceMetricRecord> liveCreditById,
        IReadOnlyDictionary<string, CanonicalBalanceMetricRecord> candidateCreditById,
        IReadOnlyDictionary<string, CanonicalBalanceMetricRecord> acquisitionById,
        IReadOnlyDictionary<string, string[]> dependencyGraph,
        HashSet<string> allWuAuthorityIds,
        HashSet<string> reviewedWuAuthorityIds,
        ExternalInflowClosureSnapshot externalInflows)
    {
        MarketReviewBundleRow[] bundle = rows.ToArray();
        string anchorItemId = bundle[0].AnchorItemId;
        string[] externalRoots = bundle
            .SelectMany(value => value.RootFamilyIds.Split('|'))
            .Where(externalInflows.AllItemIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] openExternalRoots = externalRoots
            .Where(value => !externalInflows.ClosedItemIds.Contains(value))
            .ToArray();
        if (openExternalRoots.Length != 0)
        {
            return (
                "rework-unpriced-external-inflow",
                "candidate value is blocked by external physical inflow without an exact paid-or-budgeted settlement: "
                + string.Join("|", openExternalRoots));
        }

        if (liveCreditById.TryGetValue(anchorItemId, out CanonicalBalanceMetricRecord live)
            && candidateCreditById.TryGetValue(
                anchorItemId,
                out CanonicalBalanceMetricRecord candidate))
        {
            decimal liveCredit = decimal.Parse(
                live.After,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
            decimal candidateCredit = decimal.Parse(
                candidate.After,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
            if (candidateCredit >= liveCredit * 2m)
            {
                if (!acquisitionById.TryGetValue(
                        anchorItemId,
                        out CanonicalBalanceMetricRecord acquisition))
                {
                    return (
                        "rework-sale-credit-double",
                        "large sale-credit rebase has no acquisition authority");
                }
                long acquisitionMilliEwu = long.Parse(
                    acquisition.After,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture);
                long candidateMilliEwu = long.Parse(
                    candidate.After,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture);
                long recoveryCap = V27EwuQuantizer.MultiplyOutputCredit(
                    EwuAmount.FromMilliEwu(acquisitionMilliEwu),
                    0.60m).MilliEwu;
                string[] unreviewedCausalWork = CollectDependencies(
                        anchorItemId,
                        dependencyGraph)
                    .Where(allWuAuthorityIds.Contains)
                    .Where(value => !reviewedWuAuthorityIds.Contains(value))
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                if (candidateMilliEwu > recoveryCap
                    || unreviewedCausalWork.Length != 0)
                {
                    return (
                        "rework-sale-credit-double",
                        candidateMilliEwu > recoveryCap
                            ? "large sale-credit rebase exceeds the 60% acquisition recovery cap"
                            : "large sale-credit rebase depends on unreviewed authored work: "
                              + string.Join("|", unreviewedCausalWork));
                }
                return (
                    "promote-candidate",
                    "large sale-credit rebase reviewed: exact candidate remains within the 60% acquisition cap, causal authored work is applied, and external inflows are closed");
            }
        }

        return (
            "promote-candidate",
            externalRoots.Length == 0
                ? "formula-consistent candidate remains outside the identified open-inflow and 2x-sale-shock gates"
                : "formula-consistent candidate; every dependent external inflow root has an exact paid-or-budgeted settlement: "
                  + string.Join("|", externalRoots));
    }

    private static ExternalInflowClosureSnapshot
        CaptureExternalInflowEconomicClosure()
    {
        IDungeonItemCatalogProvider items = EditorItemCatalogFactory.Create();
        FactionRouteEconomicPolicyRegistry policies = new(new IFactionRouteEconomicPolicy[]
        {
            new PaidMarketPurchaseFactionRouteEconomicPolicy(items),
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
        ResourceFactionAllianceBenefitBudgetApplicationAdapter budget = new();
        if (definitions.Length == 0 || definitions.Length != budget.Routes.Count)
        {
            throw new InvalidOperationException(
                "EXTERNAL_INFLOW_ECONOMIC_CLOSURE_INVALID: faction definitions and budget routes diverged.");
        }

        HashSet<string> all = new(StringComparer.Ordinal);
        HashSet<string> closed = new(StringComparer.Ordinal);
        foreach (FactionDefinitionSnapshot definition in definitions)
        {
            FactionCargoLine[] tradeCargo = CanonicalCargo(definition.TradeCargo);
            FactionCargoLine[] supplyCargo = CanonicalCargo(definition.SupplyCargo);
            foreach (FactionCargoLine line in tradeCargo.Concat(supplyCargo))
                all.Add(line.itemId);

            if (!policies.TryCreateQuote(
                    definition,
                    FactionRouteKind.TradeCaravan,
                    out FactionRouteQuoteSnapshot trade,
                    out string tradeFailure)
                || trade.RouteKind != FactionRouteKind.TradeCaravan
                || trade.PaymentGold <= 0
                || trade.PaymentGold != trade.CargoAuthoredGold
                || !QuoteMatchesCargo(trade, tradeCargo))
            {
                throw new InvalidOperationException(
                    "EXTERNAL_INFLOW_ECONOMIC_CLOSURE_INVALID: paid Trade route is open for "
                    + definition.StableId + "; " + tradeFailure);
            }

            if (!policies.TryCreateQuote(
                    definition,
                    FactionRouteKind.SupplyCaravan,
                    out FactionRouteQuoteSnapshot supply,
                    out string supplyFailure)
                || supply.RouteKind != FactionRouteKind.SupplyCaravan
                || supply.PaymentGold != 0
                || supply.CargoAuthoredGold <= 0
                || !QuoteMatchesCargo(supply, supplyCargo)
                || !budget.TryGetRoute(
                    definition.StableId,
                    out FactionAllianceBenefitRouteBudgetSnapshot route)
                || route.DebitMilliEwu <= 0
                || route.CooldownDays != definition.SupplyCooldownDays
                || !string.Equals(
                    route.SupplyQuoteSourceDigest,
                    supply.SourceDigest,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "EXTERNAL_INFLOW_ECONOMIC_CLOSURE_INVALID: budgeted Supply route is open for "
                    + definition.StableId + "; " + supplyFailure);
            }

            foreach (FactionCargoLine line in tradeCargo.Concat(supplyCargo))
                closed.Add(line.itemId);
        }

        if (!all.SetEquals(closed))
        {
            throw new InvalidOperationException(
                "EXTERNAL_INFLOW_ECONOMIC_CLOSURE_INVALID: not every faction cargo item is closed.");
        }
        return new ExternalInflowClosureSnapshot(all, closed);
    }

    private static FactionCargoLine[] CanonicalCargo(
        IReadOnlyList<FactionCargoLine> cargo) =>
        (cargo ?? Array.Empty<FactionCargoLine>())
        .Where(value => value != null)
        .OrderBy(value => value.itemId, StringComparer.Ordinal)
        .ThenBy(value => value.amount)
        .ToArray();

    private static bool QuoteMatchesCargo(
        FactionRouteQuoteSnapshot quote,
        IReadOnlyList<FactionCargoLine> cargo)
    {
        FactionRouteQuoteLineReceipt[] lines = quote.QuoteLines
            .OrderBy(value => value.itemId, StringComparer.Ordinal)
            .ThenBy(value => value.amount)
            .ToArray();
        return lines.Length == cargo.Count
            && lines.Select(value => value.itemId)
                .SequenceEqual(cargo.Select(value => value.itemId), StringComparer.Ordinal)
            && lines.Select(value => value.amount)
                .SequenceEqual(cargo.Select(value => value.amount))
            && lines.All(value => value.unitPriceGold > 0);
    }

    private sealed class ExternalInflowClosureSnapshot
    {
        public ExternalInflowClosureSnapshot(
            IEnumerable<string> allItemIds,
            IEnumerable<string> closedItemIds)
        {
            AllItemIds = new HashSet<string>(
                allItemIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            ClosedItemIds = new HashSet<string>(
                closedItemIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
        }

        public HashSet<string> AllItemIds { get; }
        public HashSet<string> ClosedItemIds { get; }
    }

    private static string MarketMemberCode(string authorityMetric)
    {
        switch (authorityMetric)
        {
            case "authored-unit-price-gold": return "P";
            case "authored-market-sale-rate": return "R";
            case "authored-daily-unit-cost-gold": return "S";
            case "authored-retail-cost-gold": return "T";
            case "authored-money-reward-gold": return "G";
            default:
                throw new InvalidOperationException(
                    "Unsupported market review authority metric: " + authorityMetric);
        }
    }

    private static Dictionary<string, string[]> BuildEconomicDependencyGraph(
        IReadOnlyList<CanonicalBalanceMetricRecord> records)
    {
        Dictionary<string, string[]> result = new Dictionary<string, string[]>(
            StringComparer.Ordinal);
        foreach (IGrouping<string, CanonicalBalanceMetricRecord> group in records
                     .GroupBy(value => value.StableId, StringComparer.Ordinal))
        {
            CanonicalBalanceMetricRecord[] authorities = group
                .Where(value => string.Equals(value.Metric, "acquisition-cost", StringComparison.Ordinal)
                    || string.Equals(value.Metric, "cultivated-acquisition-cost", StringComparison.Ordinal)
                    || string.Equals(value.Metric, "direct-wu", StringComparison.Ordinal))
                .ToArray();
            if (authorities.Length == 0)
                continue;
            result.Add(
                group.Key,
                authorities.SelectMany(value => value.DependencyIds)
                    .Where(value => !string.Equals(
                        value,
                        group.Key,
                        StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray());
        }
        return result;
    }

    private static bool DependsOn(
        string subjectId,
        string targetId,
        IReadOnlyDictionary<string, string[]> graph)
    {
        Stack<string> pending = new Stack<string>();
        HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(subjectId);
        while (pending.Count > 0)
        {
            string current = pending.Pop();
            if (!visited.Add(current))
                continue;
            if (string.Equals(current, targetId, StringComparison.Ordinal))
                return true;
            if (!graph.TryGetValue(current, out string[] dependencies))
                continue;
            for (int index = dependencies.Length - 1; index >= 0; index--)
                pending.Push(dependencies[index]);
        }
        return false;
    }

    private static HashSet<string> CollectDependencies(
        string subjectId,
        IReadOnlyDictionary<string, string[]> graph)
    {
        Stack<string> pending = new();
        HashSet<string> visited = new(StringComparer.Ordinal);
        pending.Push(subjectId);
        while (pending.Count > 0)
        {
            string current = pending.Pop();
            if (!visited.Add(current)
                || !graph.TryGetValue(current, out string[] dependencies))
            {
                continue;
            }
            for (int index = dependencies.Length - 1; index >= 0; index--)
                pending.Push(dependencies[index]);
        }
        return visited;
    }

    internal static void RequireMarketReviewBundlePartition(
        IReadOnlyList<MarketReviewBundleRow> rows)
    {
        if (rows == null)
            throw new ArgumentNullException(nameof(rows));

        string[] supportedCohorts =
        {
            "price-formula-clean-leaf",
            "price-dependency-review",
            "sale-rate-with-price",
            "sale-rate-only",
            "market-consumer"
        };
        string[] supportedRecommendations =
        {
            "promote-candidate",
            "rework-unpriced-external-inflow",
            "rework-sale-credit-double"
        };
        if (rows.Any(value => value.Decision != "pending-explicit-review")
            || rows.Any(value => !supportedCohorts.Contains(
                value.Cohort,
                StringComparer.Ordinal))
            || rows.Any(value => !supportedRecommendations.Contains(
                value.RecommendedDecision,
                StringComparer.Ordinal))
            || rows.Any(value => string.IsNullOrWhiteSpace(
                value.RecommendationReason))
            || rows.Any(value => value.AuthorityState != "previous-applied"
                && value.AuthorityState != "provenance-missing")
            || rows.GroupBy(
                    value => value.StableId + "\u001f" + value.AuthorityMetric,
                    StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(
                "V27 market review rows contain an unsupported value or duplicate member.");
        }

        foreach (IGrouping<string, MarketReviewBundleRow> group in rows
                     .GroupBy(value => value.BundleId, StringComparer.Ordinal))
        {
            MarketReviewBundleRow[] bundle = group.ToArray();
            string anchor = bundle[0].AnchorItemId;
            string expectedBundleId = "market-atomic:" + anchor;
            string expectedShape = BuildMarketMemberShape(bundle);
            string expectedDigest = BuildMarketBundleDigest(bundle);
            if (anchor.Length == 0
                || !string.Equals(group.Key, expectedBundleId, StringComparison.Ordinal)
                || bundle.Any(value => !string.Equals(
                    value.AnchorItemId,
                    anchor,
                    StringComparison.Ordinal))
                || bundle.Any(value => !string.Equals(
                    value.MemberShape,
                    expectedShape,
                    StringComparison.Ordinal))
                || bundle.Any(value => !string.Equals(
                    value.BundleDigest,
                    expectedDigest,
                    StringComparison.Ordinal))
                || bundle.Select(value => value.RecommendedDecision)
                    .Distinct(StringComparer.Ordinal).Count() != 1
                || bundle.Select(value => value.RecommendationReason)
                    .Distinct(StringComparer.Ordinal).Count() != 1)
            {
                throw new InvalidOperationException(
                    "V27 market review bundle is not an exact atomic anchor closure: "
                    + group.Key + ".");
            }
        }
    }

    private static void WriteMarketReviewBundleCsv(
        IReadOnlyList<MarketReviewBundleRow> rows)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(ReviewBundleCsvPath, stream =>
        {
            using StreamWriter writer = new StreamWriter(
                stream,
                new UTF8Encoding(false, true),
                4096,
                leaveOpen: true);
            writer.NewLine = "\r\n";
            WriteReviewCsvRow(writer, new[]
            {
                "schemaVersion", "bundleId", "bundleDigest", "anchorItemId", "memberShape", "cohort", "authorityState", "stableId",
                "authorityMetric", "before", "candidate", "percentDelta",
                "sourceAuthority", "sourcePropertyPath", "exactFormula",
                "dependencyFingerprint", "sourceDigest", "semanticHash", "dependencyIds",
                "rootFamilyIds", "acquisitionDisposition", "downstreamConsumerCount",
                "riskFlags", "recommendedDecision", "recommendationReason", "decision"
            });
            foreach (MarketReviewBundleRow row in rows)
            {
                WriteReviewCsvRow(writer, new[]
                {
                    "v27.market-review.4", row.BundleId, row.BundleDigest,
                    row.AnchorItemId, row.MemberShape,
                    row.Cohort, row.AuthorityState,
                    row.StableId, row.AuthorityMetric, row.Before, row.Candidate,
                    row.PercentDelta, row.SourceAuthority, row.SourcePropertyPath,
                    row.ExactFormula, row.DependencyFingerprint, row.SourceDigest,
                    row.SemanticHash,
                    row.DependencyIds, row.RootFamilyIds, row.AcquisitionDisposition,
                    row.DownstreamConsumerCount, row.RiskFlags,
                    row.RecommendedDecision, row.RecommendationReason, row.Decision
                });
            }
            writer.Flush();
        });
    }

    private static void WriteReviewCsvRow(StreamWriter writer, IReadOnlyList<string> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (index > 0)
                writer.Write(',');
            V27BalanceCsvSerializer.WriteEscapedField(
                writer,
                (fields[index] ?? string.Empty).AsSpan());
        }
        writer.Write('\r');
        writer.Write('\n');
    }

    private static void WriteMarketReviewBundleReport(
        IReadOnlyList<MarketReviewBundleRow> rows,
        V27BalanceAuditOutput audit)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(ReviewBundleReportPath, stream =>
        {
            using StreamWriter writer = new StreamWriter(
                stream,
                new UTF8Encoding(false, true),
                4096,
                leaveOpen: true);
            writer.NewLine = "\n";
            writer.WriteLine("RESULT=REVIEW_REQUIRED");
            writer.WriteLine("schemaVersion=v27.market-review.4");
            writer.WriteLine("rows=" + rows.Count.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("bundles="
                + rows.Select(value => value.BundleId).Distinct(StringComparer.Ordinal).Count()
                    .ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("critical=" + audit.CriticalCount.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("integrityFailures="
                + audit.IntegrityFailures.Count.ToString(CultureInfo.InvariantCulture));
            foreach (IGrouping<string, MarketReviewBundleRow> cohort in rows
                         .GroupBy(value => value.Cohort, StringComparer.Ordinal)
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                writer.WriteLine("cohort:" + cohort.Key + "="
                    + cohort.Count().ToString(CultureInfo.InvariantCulture));
            }
            foreach (IGrouping<string, MarketReviewBundleRow> shape in rows
                         .GroupBy(value => value.MemberShape, StringComparer.Ordinal)
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                writer.WriteLine("shape:" + shape.Key + "="
                    + shape.Select(value => value.BundleId).Distinct(StringComparer.Ordinal).Count()
                        .ToString(CultureInfo.InvariantCulture));
            }
            writer.WriteLine("previousApplied="
                + rows.Count(value => value.AuthorityState == "previous-applied")
                    .ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("provenanceMissing="
                + rows.Count(value => value.AuthorityState == "provenance-missing")
                    .ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("quarryCascadeCandidates="
                + rows.Count(value => value.RootFamilyIds.StartsWith(
                    "source:quarry",
                    StringComparison.Ordinal))
                    .ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("manaCascadeCandidates="
                + rows.Count(value => value.RootFamilyIds.Contains("resource:mana-crystal"))
                    .ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("liveRecoveryOver60Items="
                + rows.Where(value => value.RiskFlags.Split('|').Contains(
                        "live-recovery-over-60pct",
                        StringComparer.Ordinal))
                    .Select(value => value.AnchorItemId)
                    .Distinct(StringComparer.Ordinal)
                    .Count().ToString(CultureInfo.InvariantCulture));
            foreach (IGrouping<string, MarketReviewBundleRow> recommendation in rows
                         .GroupBy(value => value.RecommendedDecision, StringComparer.Ordinal)
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                writer.WriteLine("recommendation:" + recommendation.Key + "="
                    + recommendation.Select(value => value.BundleId)
                        .Distinct(StringComparer.Ordinal).Count()
                        .ToString(CultureInfo.InvariantCulture));
            }
            writer.WriteLine("decision=pending-explicit-review");
            writer.Flush();
        });
    }

    internal readonly struct MarketReviewBundleRow
    {
        public MarketReviewBundleRow(
            string bundleId,
            string anchorItemId,
            string memberShape,
            string cohort,
            string authorityState,
            string stableId,
            string authorityMetric,
            string before,
            string candidate,
            string percentDelta,
            string sourceAuthority,
            string sourcePropertyPath,
            string exactFormula,
            string dependencyFingerprint,
            string sourceDigest,
            string semanticHash,
            string dependencyIds,
            string rootFamilyIds,
            string acquisitionDisposition,
            string downstreamConsumerCount,
            string riskFlags,
            string decision)
        {
            BundleId = bundleId;
            BundleDigest = string.Empty;
            AnchorItemId = anchorItemId;
            MemberShape = memberShape;
            Cohort = cohort;
            AuthorityState = authorityState;
            StableId = stableId;
            AuthorityMetric = authorityMetric;
            Before = before;
            Candidate = candidate;
            PercentDelta = percentDelta;
            SourceAuthority = sourceAuthority;
            SourcePropertyPath = sourcePropertyPath;
            ExactFormula = exactFormula;
            DependencyFingerprint = dependencyFingerprint;
            SourceDigest = sourceDigest;
            SemanticHash = semanticHash;
            DependencyIds = dependencyIds;
            RootFamilyIds = rootFamilyIds;
            AcquisitionDisposition = acquisitionDisposition;
            DownstreamConsumerCount = downstreamConsumerCount;
            RiskFlags = riskFlags;
            RecommendedDecision = string.Empty;
            RecommendationReason = string.Empty;
            Decision = decision;
        }

        private MarketReviewBundleRow(
            MarketReviewBundleRow source,
            string memberShape,
            string bundleDigest,
            string recommendedDecision,
            string recommendationReason)
            : this(
                source.BundleId, source.AnchorItemId, memberShape, source.Cohort,
                source.AuthorityState, source.StableId, source.AuthorityMetric,
                source.Before, source.Candidate, source.PercentDelta,
                source.SourceAuthority, source.SourcePropertyPath,
                source.ExactFormula, source.DependencyFingerprint,
                source.SourceDigest, source.SemanticHash, source.DependencyIds,
                source.RootFamilyIds, source.AcquisitionDisposition,
                source.DownstreamConsumerCount, source.RiskFlags, source.Decision)
        {
            BundleDigest = bundleDigest;
            RecommendedDecision = recommendedDecision;
            RecommendationReason = recommendationReason;
        }

        public MarketReviewBundleRow WithBundleMetadata(
            string memberShape,
            string bundleDigest,
            string recommendedDecision,
            string recommendationReason)
        {
            return new MarketReviewBundleRow(
                this,
                memberShape,
                bundleDigest,
                recommendedDecision,
                recommendationReason);
        }

        public string BundleId { get; }
        public string BundleDigest { get; }
        public string AnchorItemId { get; }
        public string MemberShape { get; }
        public string Cohort { get; }
        public string AuthorityState { get; }
        public string StableId { get; }
        public string AuthorityMetric { get; }
        public string Before { get; }
        public string Candidate { get; }
        public string PercentDelta { get; }
        public string SourceAuthority { get; }
        public string SourcePropertyPath { get; }
        public string ExactFormula { get; }
        public string DependencyFingerprint { get; }
        public string SourceDigest { get; }
        public string SemanticHash { get; }
        public string DependencyIds { get; }
        public string RootFamilyIds { get; }
        public string AcquisitionDisposition { get; }
        public string DownstreamConsumerCount { get; }
        public string RiskFlags { get; }
        public string RecommendedDecision { get; }
        public string RecommendationReason { get; }
        public string Decision { get; }
    }

    private static void RequireCausalDependencyRootPromotion(
        IReadOnlyList<BalanceAnomalyNode> anomalies)
    {
        if (anomalies == null)
            throw new ArgumentNullException(nameof(anomalies));

        BalanceAnomalyNode[] wronglyPromotedSaleRates = anomalies
            .Where(value => value.EmitsCiAnnotation
                && string.Equals(
                    value.Metric,
                    "authored-market-sale-rate",
                    StringComparison.Ordinal)
                && string.Equals(
                    value.ReasonCode,
                    "v27-market-sale-rate-output-floor",
                    StringComparison.Ordinal))
            .ToArray();
        if (wronglyPromotedSaleRates.Length != 0)
        {
            throw new InvalidOperationException(
                "Dependency roots must not be promoted through an unrelated sale-rate metric: "
                + string.Join(
                    ",",
                    wronglyPromotedSaleRates.Select(value => value.StableId)));
        }

        string[] expectedApprovedItemRoots =
        {
            "fiber:cave-silk",
            "fiber:deep-goat-wool",
            "fiber:frost-wool",
            "medicine:mycelial-culture-pack",
            "resource:rune-dust",
            "sample:antigen:blood-wasting",
            "sample:antigen:cave-flu",
            "sample:antigen:gut-rot",
            "sample:antigen:mana-pox",
            "sample:antigen:red-fever",
            "sample:antigen:slime-blight",
            "sample:antigen:spore-lung"
        };
        string[] actualApprovedItemRoots = anomalies
            .Where(value => value.Disposition == BalanceAnomalyDisposition.Approved
                && string.Equals(
                    value.Metric,
                    "authored-unit-price-gold",
                    StringComparison.Ordinal)
                && expectedApprovedItemRoots.Contains(
                    value.StableId,
                    StringComparer.Ordinal))
            .Select(value => value.StableId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expected = expectedApprovedItemRoots
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!actualApprovedItemRoots.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Derived acquisition changes must reuse the exact approved item-price root; "
                + "expected=" + string.Join(",", expected)
                + "; actual=" + string.Join(",", actualApprovedItemRoots) + ".");
        }
    }

    internal static int RequireAppliedCandidateSeparation(
        IReadOnlyList<CanonicalBalanceMetricRecord> records)
    {
        if (records == null)
            throw new ArgumentNullException(nameof(records));

        CanonicalBalanceMetricRecord[] candidates = records
            .Where(value => value.Metric.StartsWith(
                V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix,
                StringComparison.Ordinal))
            .ToArray();
        string[] supportedAuthorityMetrics =
        {
            "authored-unit-price-gold",
            "authored-market-sale-rate",
            "authored-daily-unit-cost-gold",
            "authored-retail-cost-gold",
            "authored-money-reward-gold"
        };
        if (candidates.Any(value => !supportedAuthorityMetrics.Contains(
                value.Metric.Substring(
                    V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix.Length),
                StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "V27 market candidate uses an unsupported authored metric.");
        }

        foreach (CanonicalBalanceMetricRecord candidate in candidates)
        {
            string authorityMetric = candidate.Metric.Substring(
                V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix.Length);
            CanonicalBalanceMetricRecord[] authorityMatches = records
                .Where(value => string.Equals(
                        value.StableId,
                        candidate.StableId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.Metric,
                        authorityMetric,
                        StringComparison.Ordinal))
                .ToArray();
            if (authorityMatches.Length != 1)
            {
                throw new InvalidOperationException(
                    "Market candidate requires one observed authority row: "
                    + $"{candidate.StableId}:{candidate.Metric}; "
                    + $"matches={authorityMatches.Length}.");
            }

            CanonicalBalanceMetricRecord authority = authorityMatches[0];
            RequireExact(
                authority.After,
                candidate.Before,
                candidate,
                "candidate Before must equal observed authority After");
            RequireExact(
                authority.SourceAuthority,
                candidate.SourceAuthority,
                candidate,
                "candidate source asset must equal observed authority source");
            RequireExact(
                authority.SourcePropertyPath,
                candidate.SourcePropertyPath,
                candidate,
                "candidate property must equal observed authority property");
            RequireExact(
                candidate.AssetApplied,
                "false",
                candidate,
                "review-only candidate must not be marked applied");
            RequireExact(
                candidate.ApprovalKey,
                string.Empty,
                candidate,
                "review-only candidate must not have an approval key");
            RequireExact(
                candidate.ReviewStatus,
                "pending-explicit-review",
                candidate,
                "review-only candidate must remain pending explicit review");
            RequireExact(
                candidate.AnomalyDisposition,
                "local-critical",
                candidate,
                "review-only candidate must remain locally critical");
            RequireExact(
                candidate.SaveAuthority,
                "derived market recalibration proposal + explicit review authority",
                candidate,
                "review-only candidate must not claim authored save authority");

            bool previouslyApproved = string.Equals(
                    authority.ReviewStatus,
                    "applied",
                    StringComparison.Ordinal)
                || (string.Equals(
                        authority.ReviewStatus,
                        "approved",
                        StringComparison.Ordinal)
                    && string.Equals(
                        authority.AssetApplied,
                        "true",
                        StringComparison.Ordinal)
                    && authority.ApprovalKey.Length > 0);
            if (previouslyApproved)
            {
                if (authority.ApprovalKey.Length == 0)
                {
                    throw new InvalidOperationException(
                        "Previously applied market authority lost its approval key: "
                        + $"{candidate.StableId}:{authorityMetric}.");
                }
                RequireExact(
                    candidate.ReasonCode,
                    "previous-applied-market-recalibration-review-required",
                    candidate,
                    "previously applied authority requires a promotion review reason");
            }
            else
            {
                RequireExact(
                    authority.ReviewStatus,
                    "provenance-missing",
                    candidate,
                    "unproved current authority must be visibly provenance-missing");
                RequireExact(
                    authority.ApprovalKey,
                    string.Empty,
                    candidate,
                    "provenance-missing authority must not have an approval key");
                RequireExact(
                    candidate.ReasonCode,
                    "market-authority-provenance-missing",
                    candidate,
                    "unproved current authority requires a typed provenance failure");
            }
        }

        CanonicalBalanceMetricRecord[] derivedCandidates = records
            .Where(value => value.Metric.StartsWith(
                V27BalanceAudit.MarketDerivedRecalibrationCandidateMetricPrefix,
                StringComparison.Ordinal))
            .ToArray();
        HashSet<string> liveSaleCreditIds = records
            .Where(value => string.Equals(
                value.Metric,
                "market-sale-credit",
                StringComparison.Ordinal))
            .Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        string[] expectedDerivedIds = candidates
            .Where(value => value.Metric.EndsWith(
                    "authored-unit-price-gold",
                    StringComparison.Ordinal)
                || value.Metric.EndsWith(
                    "authored-market-sale-rate",
                    StringComparison.Ordinal))
            .Select(value => value.StableId)
            .Where(liveSaleCreditIds.Contains)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actualDerivedIds = derivedCandidates
            .Select(value => value.StableId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!actualDerivedIds.SequenceEqual(
                expectedDerivedIds,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Derived market-sale-credit candidates must exactly close every "
                + "unresolved price/rate item; expected="
                + string.Join("|", expectedDerivedIds)
                + "; actual=" + string.Join("|", actualDerivedIds) + ".");
        }
        foreach (CanonicalBalanceMetricRecord derived in derivedCandidates)
        {
            CanonicalBalanceMetricRecord authority = records.Single(value =>
                string.Equals(value.StableId, derived.StableId, StringComparison.Ordinal)
                && string.Equals(value.Metric, "market-sale-credit", StringComparison.Ordinal));
            RequireExact(
                authority.After,
                derived.Before,
                derived,
                "derived candidate Before must equal live sale credit");
            RequireExact(
                authority.ReviewStatus,
                "observed-live-derived",
                derived,
                "live sale credit must be labeled as an observed derivation");
            RequireExact(
                derived.AssetApplied,
                "false",
                derived,
                "derived review candidate must not be marked applied");
            RequireExact(
                derived.ApprovalKey,
                string.Empty,
                derived,
                "derived review candidate must not be independently approvable");
            RequireExact(
                derived.AnomalyDisposition,
                "collapsed-inherited",
                derived,
                "derived review candidate must collapse under its upstream authority");
            RequireExact(
                derived.ReasonCode,
                "derived-from-unresolved-market-authority",
                derived,
                "derived review candidate requires explicit upstream attribution");
            bool hasUpstreamCandidate = candidates.Any(value =>
                string.Equals(value.StableId, derived.StableId, StringComparison.Ordinal)
                && (value.Metric.EndsWith(
                        "authored-unit-price-gold",
                        StringComparison.Ordinal)
                    || value.Metric.EndsWith(
                        "authored-market-sale-rate",
                        StringComparison.Ordinal)));
            if (!hasUpstreamCandidate)
            {
                throw new InvalidOperationException(
                    "Derived sale credit has no unresolved price/rate candidate: "
                    + derived.StableId + ".");
            }
        }

        CanonicalBalanceMetricRecord noOpCandidate = candidates.FirstOrDefault(value =>
            string.Equals(value.Before, value.After, StringComparison.Ordinal));
        if (noOpCandidate != null)
        {
            throw new InvalidOperationException(
                "Market candidate must preserve a distinct current-to-candidate review boundary: "
                + noOpCandidate.StableId + ":" + noOpCandidate.Metric + ".");
        }
        CanonicalBalanceMetricRecord[] legacyPending = records
            .Where(value => string.Equals(value.ReviewStatus, "pending", StringComparison.Ordinal)
                && string.Equals(value.AssetApplied, "false", StringComparison.Ordinal)
                && value.ApprovalKey.Length > 0
                && (string.Equals(
                        value.Metric,
                        "authored-unit-price-gold",
                        StringComparison.Ordinal)
                    || string.Equals(
                        value.Metric,
                        "authored-money-reward-gold",
                        StringComparison.Ordinal)))
            .ToArray();
        if (legacyPending.Any(value =>
                value.ApprovalKey.Length == 0
                || value.DependencyFingerprint.Length == 0
                || value.SourceDigest.Length == 0))
        {
            throw new InvalidOperationException(
                "V27 pending legacy market custody lost an exact approval identity.");
        }
        return candidates.Length + derivedCandidates.Length;
    }

    private static void RequireExact(
        string actual,
        string expected,
        CanonicalBalanceMetricRecord candidate,
        string reason)
    {
        if (string.Equals(actual, expected, StringComparison.Ordinal))
            return;
        throw new InvalidOperationException(
            $"Market candidate separation failed ({reason}): "
            + $"{candidate.StableId}:{candidate.Metric}; "
            + $"expected={expected}; actual={actual}.");
    }

    private static void VerifyAppliedMetric(
        IReadOnlyList<CanonicalBalanceMetricRecord> records,
        string metric,
        ICollection<string> rows,
        ICollection<string> failures)
    {
        CanonicalBalanceMetricRecord[] matching = records
            .Where(value => value.Metric == metric)
            .ToArray();
        bool passed = matching.Length > 0
            && matching.All(value => value.AssetApplied == "true")
            && matching.All(value => value.ReviewStatus == "implemented"
                || value.ReviewStatus == "approved");
        Check(
            passed,
            "MARKET_" + metric.Replace('-', '_').ToUpperInvariant() + "_APPLIED_EXACT",
            $"actual={matching.Length}; applied={matching.Count(value => value.AssetApplied == "true")}",
            rows,
            failures);
    }

    private static void Check(
        bool passed,
        string marker,
        string detail,
        ICollection<string> rows,
        ICollection<string> failures)
    {
        string row = (passed ? "PASS " : "FAIL ") + marker + " " + detail;
        rows.Add(row);
        if (!passed)
            failures.Add(row);
    }

    private sealed class FakeStockCatalog : IStockCategoryDefinitionCatalog
    {
        private readonly StockCategoryDefinition definition;

        public FakeStockCatalog(StockCategoryDefinition definition)
        {
            this.definition = definition;
            All = new[] { definition };
        }

        public IReadOnlyList<StockCategoryDefinition> All { get; }

        public bool TryGet(StockCategory category, out StockCategoryDefinition value)
        {
            value = category == definition.Category ? definition : null;
            return value != null;
        }

        public bool TryGet(string id, out StockCategoryDefinition value)
        {
            value = string.Equals(id, definition.Id, StringComparison.Ordinal)
                ? definition
                : null;
            return value != null;
        }

        public StockCategoryDefinition Require(StockCategory category) =>
            category == definition.Category
                ? definition
                : throw new KeyNotFoundException(category.ToString());

        public string GetDisplayName(StockCategory category) => Require(category).DisplayName;
        public string GetShortName(StockCategory category) => Require(category).ShortName;
    }
}
#endif
