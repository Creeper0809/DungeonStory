#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

public static class V27BalanceLaborFacilityDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-labor-facility-authority.txt";

    private static readonly string[] AuthoredMetrics =
    {
        "authored-required-wu",
        "authored-sow-wu",
        "authored-harvest-wu",
        "construction-authored-wu:period-preserving"
    };
    private const string ResearchMetric = "authored-research-required-wu";

    [MenuItem("DungeonStory/V27/Verify Labor and Facility Candidates")]
    public static void VerifyCandidatesFromMenu() => RunAndWrite(requireApplied: false);

    [MenuItem("DungeonStory/V27/Verify Applied Labor and Facility Authority")]
    public static void VerifyAppliedFromMenu() => RunAndWrite(requireApplied: true);

    public static void RequireIntegrity(
        V27BalanceAuditOutput audit,
        bool requireApplied)
    {
        if (audit == null)
            throw new ArgumentNullException(nameof(audit));
        if (audit.IntegrityFailures.Count != 0 || audit.CriticalCount != 0)
        {
            throw new InvalidOperationException(
                $"V27 labor/facility audit is not clean: integrity="
                + $"{audit.IntegrityFailures.Count}; critical={audit.CriticalCount}.");
        }

        IReadOnlyList<CanonicalBalanceMetricRecord> records = audit.Ledger.Records;
        RequireMetric(records, "authored-required-wu", 350, requireApplied);
        RequireMetric(records, "authored-sow-wu", 12, requireApplied);
        RequireMetric(records, "authored-harvest-wu", 12, requireApplied);
        RequireMetric(
            records,
            "construction-authored-wu:period-preserving",
            356,
            requireApplied);
        RequireMetric(records, ResearchMetric, 180, requireApplied);

        CanonicalBalanceMetricRecord[] authored = records
            .Where(value => AuthoredMetrics.Contains(value.Metric, StringComparer.Ordinal))
            .ToArray();
        foreach (CanonicalBalanceMetricRecord record in authored)
        {
            decimal before = Parse(record.Before);
            decimal after = Parse(record.After);
            decimal expected = decimal.Ceiling(before * 2.25m);
            Require(after == expected,
                $"Duration-preserving WU mismatch: {record.StableId}:{record.Metric}; "
                + $"before={record.Before}; after={record.After}; expected={expected}.");
            Require(string.Equals(record.BeforeBom, record.AfterBom, StringComparison.Ordinal),
                $"BOM changed in labor-only patch: {record.StableId}:{record.Metric}.");
            Require(record.ApprovalKey.Length != 0,
                $"Exact approval key is missing: {record.StableId}:{record.Metric}.");
        }
        foreach (CanonicalBalanceMetricRecord record in records
                     .Where(value => value.Metric == ResearchMetric))
        {
            decimal before = Parse(record.Before);
            decimal after = Parse(record.After);
            decimal expected = decimal.Ceiling(before * 45m / 99m);
            Require(after == expected,
                $"Research duration-preserving WU mismatch: {record.StableId}; "
                + $"before={record.Before}; after={record.After}; expected={expected}.");
            Require(record.ApprovalKey.Length != 0,
                $"Research exact approval key is missing: {record.StableId}.");
        }

        CanonicalBalanceMetricRecord[] facilities = records
            .Where(value => value.Metric ==
                "construction-authored-wu:period-preserving")
            .ToArray();
        foreach (CanonicalBalanceMetricRecord facility in facilities)
        {
            decimal beforeDensity = Parse(facility.BeforeLaborDensity);
            decimal afterDensity = Parse(facility.AfterLaborDensity);
            decimal ratio = afterDensity / beforeDensity;
            Require(ratio >= 0.80m && ratio <= 1.25m,
                $"Facility labor-density ratio escaped normal range: "
                + $"{facility.StableId}={ratio}.");
        }

        CanonicalBalanceMetricRecord[] cycles = records
            .Where(value => value.Metric == "dismantle-rebuild-cycle-margin")
            .ToArray();
        Require(cycles.Length == 356,
            $"Expected 356 dismantle/rebuild cycle rows, found {cycles.Length}.");
        foreach (CanonicalBalanceMetricRecord cycle in cycles)
        {
            Require(long.Parse(cycle.After, CultureInfo.InvariantCulture) <= -1L,
                $"Non-lossy dismantle/rebuild cycle: {cycle.StableId}={cycle.After}mEWU.");
        }
    }

    private static void RunAndWrite(bool requireApplied)
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        RequireIntegrity(audit, requireApplied);
        IReadOnlyList<CanonicalBalanceMetricRecord> records = audit.Ledger.Records;
        CanonicalBalanceMetricRecord[] facilities = records
            .Where(value => value.Metric ==
                "construction-authored-wu:period-preserving")
            .ToArray();
        CanonicalBalanceMetricRecord[] cycles = records
            .Where(value => value.Metric == "dismantle-rebuild-cycle-margin")
            .ToArray();
        decimal minDensity = facilities.Min(value =>
            Parse(value.AfterLaborDensity) / Parse(value.BeforeLaborDensity));
        decimal maxDensity = facilities.Max(value =>
            Parse(value.AfterLaborDensity) / Parse(value.BeforeLaborDensity));
        long maximumCycleMargin = cycles.Max(value =>
            long.Parse(value.After, CultureInfo.InvariantCulture));
        int authoredCount = records.Count(value =>
            AuthoredMetrics.Contains(value.Metric, StringComparer.Ordinal));
        int appliedCount = records.Count(value =>
            AuthoredMetrics.Contains(value.Metric, StringComparer.Ordinal)
            && value.AssetApplied == "true");
        int researchCount = records.Count(value => value.Metric == ResearchMetric);
        int researchAppliedCount = records.Count(value =>
            value.Metric == ResearchMetric && value.AssetApplied == "true");

        StringBuilder report = new StringBuilder();
        report.Append("RESULT=PASS; stage=")
            .Append(requireApplied ? "applied" : "candidate")
            .Append("; failures=0\n");
        report.Append("PASS V27_LABOR_AUTHORED_WU_SCALE_EXACT rows=")
            .Append(authoredCount).Append("; factor=2.25\n");
        report.Append("PASS V27_LABOR_BOM_UNCHANGED rows=")
            .Append(authoredCount).Append('\n');
        report.Append("PASS V27_FACILITY_LABOR_DENSITY_NORMAL rows=")
            .Append(facilities.Length)
            .Append("; minRatio=").Append(minDensity.ToString(CultureInfo.InvariantCulture))
            .Append("; maxRatio=").Append(maxDensity.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        report.Append("PASS V27_FACILITY_DISMANTLE_REBUILD_STRICT_LOSS rows=")
            .Append(cycles.Length)
            .Append("; maximumMargin=").Append(maximumCycleMargin)
            .Append("mEWU\n");
        report.Append("PASS V27_LABOR_EXACT_APPROVAL_KEYS rows=")
            .Append(authoredCount).Append('\n');
        report.Append("PASS V27_LABOR_ASSET_APPLIED_EXACT applied=")
            .Append(appliedCount).Append("; total=").Append(authoredCount)
            .Append('\n');
        report.Append("PASS V27_RESEARCH_WU_EFFECTIVE_AUTHORITY_EXACT rows=")
            .Append(researchCount).Append("; factor=45/99\n");
        report.Append("PASS V27_RESEARCH_WU_ASSET_APPLIED_EXACT applied=")
            .Append(researchAppliedCount).Append("; total=").Append(researchCount)
            .Append('\n');
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(bytes, 0, bytes.Length));
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log($"V27 labor/facility authority PASS; stage="
            + $"{(requireApplied ? "applied" : "candidate")}; rows={authoredCount}.");
    }

    private static void RequireMetric(
        IReadOnlyList<CanonicalBalanceMetricRecord> records,
        string metric,
        int expectedCount,
        bool requireApplied)
    {
        CanonicalBalanceMetricRecord[] matching = records
            .Where(value => value.Metric == metric)
            .ToArray();
        Require(matching.Length == expectedCount,
            $"Expected {expectedCount} {metric} rows, found {matching.Length}.");
        Require(matching.All(value => value.Before != value.After),
            $"{metric} contains a no-op row.");
        if (requireApplied)
        {
            Require(matching.All(value => value.AssetApplied == "true"),
                $"{metric} is not fully applied.");
        }
    }

    private static decimal Parse(string value) =>
        decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
