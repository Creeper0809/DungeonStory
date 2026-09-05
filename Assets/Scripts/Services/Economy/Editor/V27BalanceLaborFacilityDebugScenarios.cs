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

    [MenuItem("DungeonStory/V27/Verify Construction Applied Candidate Separation")]
    public static void VerifyConstructionAppliedCandidateSeparationFromMenu()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        int candidateCount = RequireConstructionAppliedCandidateSeparation(
            audit.Ledger.Records);
        Debug.Log(
            "V27 construction previous-applied/candidate separation PASS; "
            + $"candidates={candidateCount}; integrityFailures="
            + $"{audit.IntegrityFailures.Count}; no asset mutation performed.");
    }

    public static void RequireIntegrity(
        V27BalanceAuditOutput audit,
        bool requireApplied,
        bool allowUnapprovedCritical = false)
    {
        if (audit == null)
            throw new ArgumentNullException(nameof(audit));
        RequireConstructionAppliedCandidateSeparation(audit.Ledger.Records);
        if (audit.IntegrityFailures.Count != 0
            || (!allowUnapprovedCritical && audit.CriticalCount != 0))
        {
            throw new InvalidOperationException(
                $"V27 labor/facility audit is not clean: integrity="
                + $"{audit.IntegrityFailures.Count}; critical={audit.CriticalCount}.");
        }

        IReadOnlyList<CanonicalBalanceMetricRecord> records = audit.Ledger.Records;
        RequireMetric(records, "authored-required-wu", 351, requireApplied);
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

    internal static int RequireOnlyTypedPostRebaseCriticals(
        V27BalanceAuditOutput audit)
    {
        if (audit == null)
            throw new ArgumentNullException(nameof(audit));
        if (audit.IntegrityFailures.Count != 0)
        {
            throw new InvalidOperationException(
                "Post-rebase staged review cannot retain ledger integrity failures: "
                + string.Join(" | ", audit.IntegrityFailures));
        }

        RequireConstructionAppliedCandidateSeparation(audit.Ledger.Records);
        V27BalanceMarketDebugScenarios.RequireAppliedCandidateSeparation(
            audit.Ledger.Records);

        BalanceAnomalyNode[] emitted = audit.Anomalies
            .Where(value => value.EmitsCiAnnotation)
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ThenBy(value => value.Metric, StringComparer.Ordinal)
            .ToArray();
        if (emitted.Length != audit.CriticalCount)
        {
            throw new InvalidOperationException(
                "Post-rebase staged Critical count diverged from the manifest: "
                + $"nodes={emitted.Length}; manifest={audit.CriticalCount}.");
        }

        Dictionary<string, CanonicalBalanceMetricRecord[]> recordsByIdentity =
            audit.Ledger.Records
                .GroupBy(
                    value => value.StableId + "\u001f" + value.Metric,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToArray(),
                    StringComparer.Ordinal);
        foreach (BalanceAnomalyNode anomaly in emitted)
        {
            string identity = anomaly.StableId + "\u001f" + anomaly.Metric;
            if (!recordsByIdentity.TryGetValue(
                    identity,
                    out CanonicalBalanceMetricRecord[] matches)
                || matches.Length != 1)
            {
                throw new InvalidOperationException(
                    "Every staged Critical must map to exactly one immutable ledger row: "
                    + $"{anomaly.StableId}:{anomaly.Metric}; "
                    + $"matches={(matches == null ? 0 : matches.Length)}.");
            }

            CanonicalBalanceMetricRecord record = matches[0];
            bool marketCandidate = anomaly.Metric.StartsWith(
                    V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix,
                    StringComparison.Ordinal)
                && (string.Equals(
                        anomaly.ReasonCode,
                        "previous-applied-market-recalibration-review-required",
                        StringComparison.Ordinal)
                    || string.Equals(
                        anomaly.ReasonCode,
                        "market-authority-provenance-missing",
                        StringComparison.Ordinal))
                && IsExactReviewOnlyCandidate(record);
            bool constructionCandidate = (string.Equals(
                        anomaly.Metric,
                        V27BalanceAudit.ConstructionRecalibrationCandidateWuMetric,
                        StringComparison.Ordinal)
                    || anomaly.Metric.StartsWith(
                        V27BalanceAudit.ConstructionRecalibrationCandidateMaterialMetricPrefix,
                        StringComparison.Ordinal))
                && string.Equals(
                    anomaly.ReasonCode,
                    "previous-applied-recalibration-review-required",
                    StringComparison.Ordinal)
                && IsExactReviewOnlyCandidate(record);
            bool derivedAcquisitionRoot = string.Equals(
                    anomaly.Metric,
                    "acquisition-cost",
                    StringComparison.Ordinal)
                && string.Equals(
                    anomaly.ReasonCode,
                    "v27-duration-preserving-first-candidate",
                    StringComparison.Ordinal)
                && anomaly.Disposition == BalanceAnomalyDisposition.RootCritical
                && anomaly.RootCauseIds.Count == 0
                && string.Equals(record.Domain, "items", StringComparison.Ordinal)
                && string.Equals(record.DefinitionKind, "item", StringComparison.Ordinal)
                && string.Equals(record.SourcePropertyPath, "recipe graph", StringComparison.Ordinal)
                && string.Equals(record.AnomalyDisposition, "root-critical", StringComparison.Ordinal)
                && string.Equals(record.ReviewStatus, "pending", StringComparison.Ordinal)
                && string.Equals(record.AssetApplied, "false", StringComparison.Ordinal)
                && record.ApprovalKey.Length != 0;

            if (!marketCandidate && !constructionCandidate && !derivedAcquisitionRoot)
            {
                throw new InvalidOperationException(
                    "Post-rebase validation encountered an untyped unresolved Critical: "
                    + $"{anomaly.StableId}:{anomaly.Metric}; "
                    + $"reason={anomaly.ReasonCode}; disposition={anomaly.Disposition}.");
            }
        }

        return emitted.Length;
    }

    private static bool IsExactReviewOnlyCandidate(
        CanonicalBalanceMetricRecord record) =>
        string.Equals(record.AnomalyDisposition, "local-critical", StringComparison.Ordinal)
        && string.Equals(record.ReviewStatus, "pending-explicit-review", StringComparison.Ordinal)
        && string.Equals(record.AssetApplied, "false", StringComparison.Ordinal)
        && record.ApprovalKey.Length == 0;

    internal static int RequireConstructionAppliedCandidateSeparation(
        IReadOnlyList<CanonicalBalanceMetricRecord> records)
    {
        if (records == null)
            throw new ArgumentNullException(nameof(records));

        CanonicalBalanceMetricRecord[] candidates = records
            .Where(value => string.Equals(
                    value.Metric,
                    V27BalanceAudit.ConstructionRecalibrationCandidateWuMetric,
                    StringComparison.Ordinal)
                || value.Metric.StartsWith(
                    V27BalanceAudit.ConstructionRecalibrationCandidateMaterialMetricPrefix,
                    StringComparison.Ordinal))
            .ToArray();
        foreach (CanonicalBalanceMetricRecord candidate in candidates)
        {
            string appliedMetric = string.Equals(
                    candidate.Metric,
                    V27BalanceAudit.ConstructionRecalibrationCandidateWuMetric,
                    StringComparison.Ordinal)
                ? ConstructionMetric
                : ConstructionMaterialMetricPrefix
                    + candidate.Metric.Substring(
                        V27BalanceAudit.ConstructionRecalibrationCandidateMaterialMetricPrefix.Length);
            CanonicalBalanceMetricRecord[] appliedMatches = records
                .Where(value => string.Equals(
                        value.StableId,
                        candidate.StableId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.Metric,
                        appliedMetric,
                        StringComparison.Ordinal))
                .ToArray();
            Require(appliedMatches.Length == 1,
                $"Construction candidate requires one applied custody row: "
                + $"{candidate.StableId}:{candidate.Metric}; matches={appliedMatches.Length}.");
            CanonicalBalanceMetricRecord applied = appliedMatches[0];
            Require(string.Equals(applied.After, candidate.Before, StringComparison.Ordinal),
                $"Construction candidate must start at exact applied authority: "
                + $"{candidate.StableId}:{candidate.Metric}; "
                + $"appliedAfter={applied.After}; candidateBefore={candidate.Before}.");
            Require(string.Equals(applied.AssetApplied, "true", StringComparison.Ordinal)
                    && applied.ApprovalKey.Length > 0,
                $"Construction applied custody is not exactly approved: "
                + $"{candidate.StableId}:{appliedMetric}.");
            Require(string.Equals(candidate.AssetApplied, "false", StringComparison.Ordinal)
                    && candidate.ApprovalKey.Length == 0,
                $"Construction review-only candidate became mutation-eligible: "
                + $"{candidate.StableId}:{candidate.Metric}.");
            Require(string.Equals(
                    candidate.ReviewStatus,
                    "pending-explicit-review",
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.AnomalyDisposition,
                    "local-critical",
                    StringComparison.Ordinal),
                $"Construction candidate lost its explicit review gate: "
                + $"{candidate.StableId}:{candidate.Metric}.");
            Require(string.Equals(
                    applied.SourceAuthority,
                    candidate.SourceAuthority,
                    StringComparison.Ordinal)
                && string.Equals(
                    applied.SourcePropertyPath,
                    candidate.SourcePropertyPath,
                    StringComparison.Ordinal),
                $"Construction candidate does not target the applied property: "
                + $"{candidate.StableId}:{candidate.Metric}.");
            Require(string.Equals(
                    candidate.SaveAuthority,
                    "derived optimizer proposal + explicit review authority",
                    StringComparison.Ordinal),
                $"Construction candidate was mislabeled as authored save authority: "
                + $"{candidate.StableId}:{candidate.Metric}.");
            if (candidate.Metric.StartsWith(
                    V27BalanceAudit.ConstructionRecalibrationCandidateMaterialMetricPrefix,
                    StringComparison.Ordinal))
            {
                int historical = int.Parse(applied.Before, CultureInfo.InvariantCulture);
                int proposed = int.Parse(candidate.After, CultureInfo.InvariantCulture);
                int upper = (historical * 3 + 1) / 2;
                Require(proposed >= historical && proposed <= upper,
                    $"Construction material candidate escaped its historical 50% bound: "
                    + $"{candidate.StableId}:{candidate.Metric}; "
                    + $"historical={historical}; proposed={proposed}; upper={upper}.");
                Require(candidate.ExactFormula.Contains(
                        "historicalBaseline=" + historical.ToString(CultureInfo.InvariantCulture),
                        StringComparison.Ordinal),
                    $"Construction material candidate formula lost historical authority: "
                    + $"{candidate.StableId}:{candidate.Metric}.");
            }
        }

        CanonicalBalanceMetricRecord d12Applied = records.Single(value =>
            string.Equals(value.StableId, "building:1011", StringComparison.Ordinal)
            && string.Equals(
                value.Metric,
                ConstructionMaterialMetricPrefix + "material:treated-lumber",
                StringComparison.Ordinal));
        Require(d12Applied.Before == "4" && d12Applied.After == "6",
            "building:1011 treated-lumber must remain exact 4->6 applied evidence.");
        return candidates.Length;
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
