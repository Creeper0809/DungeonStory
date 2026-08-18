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

    private static readonly string[] RecurringMetrics =
    {
        "authored-required-wu",
        "authored-sow-wu",
        "authored-harvest-wu"
    };
    private const string ConstructionMetric =
        "construction-authored-wu:redistributed";
    private const string ConstructionMaterialMetricPrefix =
        "construction-material-amount:";
    private const string ResearchMetric = "authored-research-required-wu";

    [MenuItem("DungeonStory/V27/Verify Labor and Facility Candidates")]
    public static void VerifyCandidatesFromMenu() => RunAndWrite(requireApplied: false);

    [MenuItem("DungeonStory/V27/Verify Applied Labor and Facility Authority")]
    public static void VerifyAppliedFromMenu() => RunAndWrite(requireApplied: true);

    public static void RequireIntegrity(
        V27BalanceAuditOutput audit,
        bool requireApplied,
        bool allowUnapprovedCritical = false)
    {
        if (audit == null)
            throw new ArgumentNullException(nameof(audit));
        if (audit.IntegrityFailures.Count != 0
            || (!allowUnapprovedCritical && audit.CriticalCount != 0))
        {
            throw new InvalidOperationException(
                $"V27 labor/facility audit is not clean: integrity="
                + $"{audit.IntegrityFailures.Count}; critical={audit.CriticalCount}.");
        }

        IReadOnlyList<CanonicalBalanceMetricRecord> records = audit.Ledger.Records;
        RequireMetric(records, "authored-required-wu", 350, requireApplied);
        RequireMetric(records, "authored-sow-wu", 12, requireApplied);
        RequireMetric(records, "authored-harvest-wu", 12, requireApplied);
        RequireMetric(records, ConstructionMetric, 356, requireApplied, false);
        RequireMetric(records, ResearchMetric, 180, requireApplied);

        CanonicalBalanceMetricRecord[] recurring = records
            .Where(value => RecurringMetrics.Contains(value.Metric, StringComparer.Ordinal))
            .ToArray();
        foreach (CanonicalBalanceMetricRecord record in recurring)
        {
            decimal before = Parse(record.Before);
            decimal after = Parse(record.After);
            decimal expectedLegacy = decimal.Ceiling(after * 2.25m);
            Require(before == expectedLegacy,
                $"Recurring-throughput correction mismatch: {record.StableId}:{record.Metric}; "
                + $"legacy={record.Before}; after={record.After}; expectedLegacy={expectedLegacy}.");
            Require(string.Equals(record.BeforeBom, record.AfterBom, StringComparison.Ordinal),
                $"BOM changed in labor-only patch: {record.StableId}:{record.Metric}.");
            Require(record.ApprovalKey.Length != 0,
                $"Exact approval key is missing: {record.StableId}:{record.Metric}.");
        }
        CanonicalBalanceMetricRecord[] facilities = records
            .Where(value => value.Metric == "construction-wu:approved")
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        Require(facilities.Length == 356,
            $"Expected 356 approved construction rows, found {facilities.Length}.");
        Dictionary<string, CanonicalBalanceMetricRecord> periodCandidates = records
            .Where(value => value.Metric == "construction-wu:period-preserving")
            .ToDictionary(value => value.StableId, StringComparer.Ordinal);
        foreach (CanonicalBalanceMetricRecord facility in facilities)
        {
            decimal beforeWu = Parse(facility.Before);
            decimal afterWu = Parse(facility.After);
            Require(afterWu >= decimal.Ceiling(beforeWu * 1.5m)
                    && afterWu <= decimal.Ceiling(beforeWu * 2.25m),
                $"Facility construction WU escaped the 1.5-2.25 band: "
                + $"{facility.StableId}; before={beforeWu}; after={afterWu}.");
            CanonicalBalanceMetricRecord period = periodCandidates[facility.StableId];
            decimal targetInvestment = Parse(period.AfterDirectWu)
                + Parse(period.AfterBomEwu);
            decimal selectedInvestment = afterWu + Parse(facility.AfterBomEwu);
            decimal errorRatio = Math.Abs(selectedInvestment - targetInvestment)
                / targetInvestment;
            Require(errorRatio <= 0.02m,
                $"Facility total investment escaped ±2%: {facility.StableId}; "
                + $"target={targetInvestment}; selected={selectedInvestment}; "
                + $"error={errorRatio}.");
            decimal densityRatio = Parse(facility.AfterLaborDensity)
                / Parse(facility.BeforeLaborDensity);
            bool regularDensity = densityRatio >= 0.67m && densityRatio <= 1.50m;
            bool documentedException = facility.ReasonDetail.Contains(
                    "material share is already >=60%",
                    StringComparison.Ordinal)
                || facility.ReasonDetail.Contains(
                    "one-cell primitive infrastructure",
                    StringComparison.Ordinal);
            Require(regularDensity || documentedException,
                $"Facility labor-density drift has no bounded exception: "
                + $"{facility.StableId}={densityRatio}; {facility.ReasonDetail}.");
            Require(!string.Equals(
                    facility.AnomalyDisposition,
                    "local-critical",
                    StringComparison.Ordinal),
                $"Facility remains local-critical after redistribution: {facility.StableId}.");
        }

        CanonicalBalanceMetricRecord[] materialRows = records
            .Where(value => value.Metric.StartsWith(
                ConstructionMaterialMetricPrefix,
                StringComparison.Ordinal))
            .ToArray();
        Require(materialRows.Length >= facilities.Length,
            $"Construction BOM patch rows are incomplete: {materialRows.Length}.");
        foreach (CanonicalBalanceMetricRecord material in materialRows)
        {
            int before = int.Parse(material.Before, CultureInfo.InvariantCulture);
            int after = int.Parse(material.After, CultureInfo.InvariantCulture);
            Require(after >= before && after <= (before * 3 + 1) / 2,
                $"Construction BOM amount escaped the 50% cap: "
                + $"{material.StableId}:{material.Metric}={before}->{after}.");
            Require(before == after || material.ApprovalKey.Length != 0,
                $"Changed construction BOM row lacks exact approval: "
                + $"{material.StableId}:{material.Metric}.");
            if (requireApplied)
            {
                Require(material.AssetApplied == "true",
                    $"Construction BOM row is not applied: "
                    + $"{material.StableId}:{material.Metric}.");
            }
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
            .Where(value => value.Metric == "construction-wu:approved")
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
        int recurringCount = records.Count(value =>
            RecurringMetrics.Contains(value.Metric, StringComparer.Ordinal));
        int recurringAppliedCount = records.Count(value =>
            RecurringMetrics.Contains(value.Metric, StringComparer.Ordinal)
            && value.AssetApplied == "true");
        int researchCount = records.Count(value => value.Metric == ResearchMetric);
        int researchAppliedCount = records.Count(value =>
            value.Metric == ResearchMetric && value.AssetApplied == "true");

        StringBuilder report = new StringBuilder();
        report.Append("RESULT=PASS; stage=")
            .Append(requireApplied ? "applied" : "candidate")
            .Append("; failures=0\n");
        report.Append("PASS V27_RECURRING_WU_PROJECT_SCALE_REMOVED rows=")
            .Append(recurringCount).Append("; factor=1\n");
        report.Append("PASS V27_LABOR_BOM_UNCHANGED rows=")
            .Append(recurringCount).Append('\n');
        int warningFacilities = facilities.Count(value =>
            !string.Equals(value.AnomalyDisposition, "none", StringComparison.Ordinal));
        report.Append("PASS V27_FACILITY_BOUNDED_WU_BOM_NO_CRITICAL rows=")
            .Append(facilities.Length)
            .Append("; minRatio=").Append(minDensity.ToString(CultureInfo.InvariantCulture))
            .Append("; maxRatio=").Append(maxDensity.ToString(CultureInfo.InvariantCulture))
            .Append("; documentedWarnings=").Append(warningFacilities)
            .Append('\n');
        report.Append("PASS V27_FACILITY_DISMANTLE_REBUILD_STRICT_LOSS rows=")
            .Append(cycles.Length)
            .Append("; maximumMargin=").Append(maximumCycleMargin)
            .Append("mEWU\n");
        report.Append("PASS V27_LABOR_EXACT_APPROVAL_KEYS rows=")
            .Append(recurringCount).Append('\n');
        report.Append("PASS V27_LABOR_ASSET_APPLIED_EXACT applied=")
            .Append(recurringAppliedCount).Append("; total=").Append(recurringCount)
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
            + $"{(requireApplied ? "applied" : "candidate")}; rows={recurringCount}.");
    }

    private static void RequireMetric(
        IReadOnlyList<CanonicalBalanceMetricRecord> records,
        string metric,
        int expectedCount,
        bool requireApplied,
        bool requireChange = true)
    {
        CanonicalBalanceMetricRecord[] matching = records
            .Where(value => value.Metric == metric)
            .ToArray();
        Require(matching.Length == expectedCount,
            $"Expected {expectedCount} {metric} rows, found {matching.Length}.");
        if (requireChange)
        {
            Require(matching.All(value => value.Before != value.After),
                $"{metric} contains a no-op row.");
        }
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
