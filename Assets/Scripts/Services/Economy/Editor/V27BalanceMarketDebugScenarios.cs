#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

public static class V27BalanceMarketDebugScenarios
{
    public const string ReportPath = "Artifacts/QA/v27-balance-market-authority.txt";

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
        VerifyAppliedMetric(records, "authored-unit-price-gold", 413, rows, failures);
        VerifyAppliedMetric(records, "authored-market-sale-rate", 349, rows, failures);
        VerifyAppliedMetric(records, "authored-retail-cost-gold", 4, rows, failures);
        VerifyAppliedMetric(records, "authored-daily-unit-cost-gold", 7, rows, failures);
        VerifyAppliedMetric(records, "authored-money-reward-gold", 13, rows, failures);

        Dictionary<string, long> acquisitionById = records
            .Where(value => value.Metric == "acquisition-cost")
            .ToDictionary(
                value => value.StableId,
                value => long.Parse(value.After, CultureInfo.InvariantCulture),
                StringComparer.Ordinal);
        CanonicalBalanceMetricRecord[] saleCredits = records
            .Where(value => value.Metric == "market-sale-credit")
            .ToArray();
        bool saleFloorExact = saleCredits.Length == 349;
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

    private static void VerifyAppliedMetric(
        IReadOnlyList<CanonicalBalanceMetricRecord> records,
        string metric,
        int expected,
        ICollection<string> rows,
        ICollection<string> failures)
    {
        CanonicalBalanceMetricRecord[] matching = records
            .Where(value => value.Metric == metric)
            .ToArray();
        bool passed = matching.Length == expected
            && matching.All(value => value.AssetApplied == "true")
            && matching.All(value => value.ReviewStatus == "implemented"
                || value.ReviewStatus == "approved");
        Check(
            passed,
            "MARKET_" + metric.Replace('-', '_').ToUpperInvariant() + "_APPLIED_EXACT",
            $"expected={expected}; actual={matching.Length}; applied={matching.Count(value => value.AssetApplied == "true")}",
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
