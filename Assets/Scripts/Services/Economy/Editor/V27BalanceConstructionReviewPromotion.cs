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
using Debug = UnityEngine.Debug;

public static partial class V27BalanceAssetApplication
{
    internal const string ConstructionReviewDecisionPath =
        "docs/game-design/v27-balance-construction-review-decisions.json";
    internal const string ConstructionReviewDecisionSchema =
        "v27.construction-review-decisions.1";
    private const string ConstructionReviewPromotionReportPath =
        "Artifacts/QA/v27-balance-construction-review-application.txt";

    [MenuItem("DungeonStory/V27/Adopt Current Construction Recommendations As Exact Decisions")]
    public static void AdoptCurrentConstructionRecommendationsAsExactDecisionsFromMenu()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.GenerateForApprovalRefresh();
        V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
            audit,
            requireApplied: true,
            allowUnapprovedCritical: true);
        V27BalanceLaborFacilityDebugScenarios.RequireOnlyTypedPostRebaseCriticals(audit);

        CanonicalBalanceMetricRecord[] candidates = CaptureConstructionReviewCandidates(
            audit.Ledger);
        string absolutePath = ProjectAbsolutePath(ConstructionReviewDecisionPath);
        if (candidates.Length == 0)
        {
            if (!File.Exists(absolutePath))
            {
                throw new InvalidOperationException(
                    "CONSTRUCTION_REVIEW_DECISION_SCOPE_EMPTY: no candidate or "
                    + "existing exact decision authority exists.");
            }
            ConstructionReviewValidation existing =
                ValidateConstructionReviewDecisions(audit.Ledger);
            Debug.Log(existing.Format("adoption-no-op"));
            return;
        }

        ConstructionReviewDecisionRowData[] rows = candidates
            .Select(candidate => CaptureConstructionDecisionRow(audit.Ledger, candidate))
            .OrderBy(value => value.stableId, StringComparer.Ordinal)
            .ThenBy(value => value.candidateMetric, StringComparer.Ordinal)
            .ToArray();
        string sourceLedgerDigest = audit.AuthoritySnapshot.SourceDigest;
        string payloadDigest = ComputeConstructionDecisionPayloadDigest(
            sourceLedgerDigest,
            rows);
        string epochDigest = ComputeConstructionDecisionEpochDigest(payloadDigest);
        ConstructionReviewDecisionFileData output = new()
        {
            schemaVersion = ConstructionReviewDecisionSchema,
            decisionEpochId = "construction-review-epoch:" + epochDigest,
            sourceLedgerDigest = sourceLedgerDigest,
            decisionPayloadDigest = payloadDigest,
            decisionEpochDigest = epochDigest,
            decisions = rows
        };

        bool existed = File.Exists(absolutePath);
        byte[] rollback = existed ? File.ReadAllBytes(absolutePath) : Array.Empty<byte>();
        try
        {
            byte[] bytes = SerializeConstructionDecisionAuthority(output);
            WriteConstructionDecisionAuthorityTwice(bytes);
            ConstructionReviewValidation validation =
                ValidateConstructionReviewDecisions(audit.Ledger);
            if (validation.Pending.Count != rows.Length
                || validation.AppliedCount != 0)
            {
                throw new InvalidOperationException(
                    "CONSTRUCTION_REVIEW_DECISION_ADOPTION_SCOPE_MISMATCH: "
                    + $"expectedPending={rows.Length}; "
                    + $"actualPending={validation.Pending.Count}; "
                    + $"applied={validation.AppliedCount}.");
            }
            Debug.Log(validation.Format("exact-decisions-adopted")
                + "; secondWriteDiff=0");
        }
        catch
        {
            if (existed)
                File.WriteAllBytes(absolutePath, rollback);
            else if (File.Exists(absolutePath))
                File.Delete(absolutePath);
            throw;
        }
    }

    [MenuItem("DungeonStory/V27/Adopt And Apply Reviewed Residual Criticals")]
    public static void AdoptAndApplyReviewedResidualCriticalsFromMenu()
    {
        AdoptCurrentConstructionRecommendationsAsExactDecisionsFromMenu();
        ApplyReviewedResidualCriticalsFromMenu();
    }

    [MenuItem("DungeonStory/V27/Apply Reviewed Construction And Derived Criticals")]
    public static void ApplyReviewedResidualCriticalsFromMenu()
    {
        RevalidateSemanticallyUnchangedAppliedApprovalsFromMenu();
        V27BalanceAuditOutput beforeAudit = V27BalanceAudit.GenerateForApprovalRefresh();
        V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
            beforeAudit,
            requireApplied: true,
            allowUnapprovedCritical: true);
        V27BalanceLaborFacilityDebugScenarios.RequireOnlyTypedPostRebaseCriticals(
            beforeAudit);

        ConstructionReviewValidation before =
            ValidateConstructionReviewDecisions(beforeAudit.Ledger);
        HashSet<string> activeBefore =
            CaptureMatchingApprovalKeysForRefresh(beforeAudit.Ledger)
            .ToHashSet(StringComparer.Ordinal);
        CanonicalBalanceMetricRecord[] derivedApprovalOnly = beforeAudit.Ledger.Records
            .Where(IsApprovalOnlyLedgerRecord)
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ThenBy(value => value.Metric, StringComparer.Ordinal)
            .ToArray();
        CanonicalBalanceMetricRecord[] unresolvedDerived = derivedApprovalOnly
            .Where(value => !activeBefore.Contains(value.ApprovalKey))
            .ToArray();
        ValidateMarketApplicationReceipts(beforeAudit.Ledger);
        if (beforeAudit.CriticalCount
            != before.Pending.Count + unresolvedDerived.Length)
        {
            throw new InvalidOperationException(
                "Residual Critical scope contains rows outside the exact reviewed "
                + "construction and derived approval-only sets: "
                + $"critical={beforeAudit.CriticalCount}; "
                + $"construction={before.Pending.Count}; "
                + $"derived={unresolvedDerived.Length}.");
        }

        List<BalanceAssetPatch> patches = before.Pending
            .Select(BalanceAssetPatch.CaptureForConstructionReviewPromotion)
            .ToList();
        string[] paths = patches
            .Select(value => value.AssetPath)
            .Append(FactionBenefitBudgetAssetPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, byte[]> assetRollback = paths.ToDictionary(
            value => value,
            value => File.ReadAllBytes(ProjectAbsolutePath(value)),
            StringComparer.Ordinal);
        string approvalAbsolute = ProjectAbsolutePath(V27BalanceAudit.ApprovalPath);
        byte[] approvalRollback = File.ReadAllBytes(approvalAbsolute);
        string reportAbsolute = ProjectAbsolutePath(
            ConstructionReviewPromotionReportPath);
        bool reportExisted = File.Exists(reportAbsolute);
        byte[] reportRollback = reportExisted
            ? File.ReadAllBytes(reportAbsolute)
            : Array.Empty<byte>();
        string[] generatedArtifactPaths =
        {
            V27BalanceAudit.MarkdownPath,
            V27BalanceAudit.AuditPath,
            V27BalanceAudit.ManifestPath,
            V27BalanceAudit.SourceInventoryPath,
            V27BalanceCsvSerializer.ArtifactPath,
            V27BalanceJsonSerializer.AnomalyArtifactPath
        };
        Dictionary<string, byte[]> artifactRollback = generatedArtifactPaths
            .Where(value => File.Exists(ProjectAbsolutePath(value)))
            .ToDictionary(
                value => value,
                value => File.ReadAllBytes(ProjectAbsolutePath(value)),
                StringComparer.Ordinal);
        string phase = "apply-construction-patches";

        try
        {
            BalanceAssetApplicationResult applied = ApplyPatches(
                patches,
                dryRun: false,
                requireCleanGit: false,
                BalanceAssetApplicationFailurePoint.None);

            phase = "capture-promoted-authority";
            V27BalanceAuditOutput afterAudit = V27BalanceAudit.GenerateForApprovalRefresh();
            if (afterAudit.IntegrityFailures.Count != 0)
            {
                throw new InvalidOperationException(
                    "Construction promotion produced ledger integrity failures:\n"
                    + string.Join("\n", afterAudit.IntegrityFailures));
            }

            phase = "replace-custody-and-approve-derived-roots";
            WriteApprovals(
                afterAudit.Ledger,
                record => IsLaborFacilityApprovalMetric(record.Metric)
                    || IsApprovalOnlyLedgerRecord(record),
                replaceIncludedApprovals: true);

            phase = "revalidate-unchanged-approval-custody";
            RevalidateSemanticallyUnchangedAppliedApprovalsFromMenu();

            phase = "refresh-alliance-benefit-budget";
            afterAudit = V27BalanceAudit.GenerateForApprovalRefresh();
            FactionAllianceBenefitBudgetReviewSnapshot budgetAuthority =
                FactionAllianceBenefitBudgetReviewAuthority.Capture(afterAudit.Ledger);
            ApplyFactionBenefitBudgetReviewAuthority(budgetAuthority);

            phase = "strict-current-source-validation";
            V27BalanceAuditOutput verified = V27BalanceAudit.Generate(
                BalanceLedgerExecutionMode.AuditOnly);
            if (verified.IntegrityFailures.Count != 0)
            {
                throw new InvalidOperationException(
                    "Residual Critical promotion failed strict integrity:\n"
                    + string.Join("\n", verified.IntegrityFailures));
            }
            V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
                verified,
                requireApplied: true);
            if (verified.CriticalCount != 0)
            {
                throw new InvalidOperationException(
                    "Residual Critical promotion did not close the strict ledger: "
                    + verified.CriticalCount.ToString(CultureInfo.InvariantCulture));
            }
            string[] activeAfter = CaptureValidApprovalKeys(verified.Ledger);
            ConstructionReviewValidation closed =
                ValidateConstructionReviewDecisions(verified.Ledger);
            if (closed.Pending.Count != 0
                || closed.AppliedCount != closed.DecisionCount)
            {
                throw new InvalidOperationException(
                    "Construction decisions were not applied exact-once: "
                    + closed.Format("invalid"));
            }
            int approvedDerived = verified.Ledger.Records
                .Where(IsApprovalOnlyLedgerRecord)
                .Count(value => activeAfter.Contains(
                    value.ApprovalKey,
                    StringComparer.Ordinal));
            if (approvedDerived != derivedApprovalOnly.Length)
            {
                throw new InvalidOperationException(
                    "Derived approval-only root coverage is incomplete: "
                    + $"approved={approvedDerived}; expected={derivedApprovalOnly.Length}.");
            }

            phase = "verify-market-receipt-and-no-op";
            ValidateMarketApplicationReceipts(verified.Ledger);
            BalanceAssetApplicationResult noOp = ApplyPatches(
                CreatePatches(
                    verified.Ledger,
                    ValidateApprovals(LoadApprovals())),
                dryRun: true,
                requireCleanGit: false,
                BalanceAssetApplicationFailurePoint.None);
            if (noOp.DifferingPropertyCount != 0)
            {
                throw new InvalidOperationException(
                    "Residual Critical second-build gate was not a no-op: "
                    + noOp.DifferingPropertyCount.ToString(
                        CultureInfo.InvariantCulture));
            }

            phase = "write-deterministic-current-state-report";
            WriteConstructionReviewPromotionReport(
                closed,
                derivedApprovalOnly.Length,
                approvedDerived,
                verified.CriticalCount,
                noOp.DifferingPropertyCount);
            byte[] firstReport = File.ReadAllBytes(reportAbsolute);
            WriteConstructionReviewPromotionReport(
                closed,
                derivedApprovalOnly.Length,
                approvedDerived,
                verified.CriticalCount,
                noOp.DifferingPropertyCount);
            if (!firstReport.SequenceEqual(File.ReadAllBytes(reportAbsolute)))
            {
                throw new InvalidOperationException(
                    "Construction review current-state report was not byte deterministic.");
            }

            Debug.Log(closed.Format("applied")
                + $"; initiallyPending={before.Pending.Count}; "
                + $"initiallyUnresolvedDerived={unresolvedDerived.Length}; "
                + $"changedAssets={applied.AssetCount}; "
                + $"changedProperties={applied.DifferingPropertyCount}; "
                + $"derivedApproved={approvedDerived}; critical=0; noOpDiff=0");
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(approvalAbsolute, approvalRollback);
            foreach (KeyValuePair<string, byte[]> pair in assetRollback)
                File.WriteAllBytes(ProjectAbsolutePath(pair.Key), pair.Value);
            if (reportExisted)
                File.WriteAllBytes(reportAbsolute, reportRollback);
            else if (File.Exists(reportAbsolute))
                File.Delete(reportAbsolute);
            foreach (string path in generatedArtifactPaths)
            {
                string absolute = ProjectAbsolutePath(path);
                if (artifactRollback.TryGetValue(path, out byte[] bytes))
                    File.WriteAllBytes(absolute, bytes);
                else if (File.Exists(absolute))
                    File.Delete(absolute);
            }
            AssetDatabase.ImportAsset(
                V27BalanceAudit.ApprovalPath,
                ImportAssetOptions.ForceUpdate);
            foreach (string path in paths)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            throw new InvalidOperationException(
                "Reviewed residual Critical promotion failed in phase '"
                + phase + "'.",
                exception);
        }
    }

    internal static ConstructionReviewValidation ValidateConstructionReviewDecisions(
        FrozenBalanceLedger ledger)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        ConstructionReviewDecisionFileData file = LoadConstructionReviewDecisions();
        if (!string.Equals(
                file.schemaVersion,
                ConstructionReviewDecisionSchema,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(file.decisionEpochId)
            || string.IsNullOrWhiteSpace(file.sourceLedgerDigest)
            || string.IsNullOrWhiteSpace(file.decisionPayloadDigest)
            || string.IsNullOrWhiteSpace(file.decisionEpochDigest)
            || file.decisions == null
            || file.decisions.Length == 0)
        {
            throw new InvalidOperationException(
                "CONSTRUCTION_REVIEW_DECISION_STALE: header is incomplete.");
        }
        string payloadDigest = ComputeConstructionDecisionPayloadDigest(
            file.sourceLedgerDigest,
            file.decisions);
        string epochDigest = ComputeConstructionDecisionEpochDigest(payloadDigest);
        if (!string.Equals(
                payloadDigest,
                file.decisionPayloadDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                epochDigest,
                file.decisionEpochDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                file.decisionEpochId,
                "construction-review-epoch:" + epochDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "CONSTRUCTION_REVIEW_DECISION_STALE: digest mismatch.");
        }

        Dictionary<string, CanonicalBalanceMetricRecord[]> byIdentity =
            ledger.Records
                .GroupBy(
                    value => ConstructionDecisionIdentity(
                        value.StableId,
                        value.Metric),
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToArray(),
                    StringComparer.Ordinal);
        HashSet<string> active = CaptureMatchingApprovalKeysForRefresh(ledger)
            .ToHashSet(StringComparer.Ordinal);
        List<CanonicalBalanceMetricRecord> pending = new();
        HashSet<string> decisionKeys = new(StringComparer.Ordinal);
        int applied = 0;

        foreach (ConstructionReviewDecisionRowData decision in file.decisions)
        {
            RequireConstructionDecisionRow(decision);
            string decisionKey = ConstructionDecisionIdentity(
                decision.stableId,
                decision.candidateMetric);
            if (!decisionKeys.Add(decisionKey))
            {
                throw new InvalidOperationException(
                    "CONSTRUCTION_REVIEW_DECISION_STALE: duplicate decision "
                    + decisionKey + ".");
            }

            CanonicalBalanceMetricRecord candidate = TryRequireSingle(
                byIdentity,
                decisionKey);
            if (candidate != null)
            {
                RequireExactConstructionCandidate(decision, candidate);
                CanonicalBalanceMetricRecord current = RequireSingle(
                    byIdentity,
                    ConstructionDecisionIdentity(
                        decision.stableId,
                        decision.appliedMetric));
                if (!string.Equals(
                        current.After,
                        decision.beforeExactToken,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        current.SourceAuthority,
                        decision.sourceAuthority,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        current.SourcePropertyPath,
                        decision.sourcePropertyPath,
                        StringComparison.Ordinal)
                    || !string.Equals(current.AssetApplied, "true", StringComparison.Ordinal)
                    || !active.Contains(current.ApprovalKey))
                {
                    throw new InvalidOperationException(
                        "CONSTRUCTION_REVIEW_DECISION_STALE: pending decision lost "
                        + "its exact applied custody row: " + decisionKey + ".");
                }
                pending.Add(candidate);
                continue;
            }

            CanonicalBalanceMetricRecord promoted = RequireSingle(
                byIdentity,
                ConstructionDecisionIdentity(
                    decision.stableId,
                    decision.appliedMetric));
            if (!string.Equals(
                    promoted.After,
                    decision.afterExactToken,
                    StringComparison.Ordinal)
                || !string.Equals(
                    promoted.SourceAuthority,
                    decision.sourceAuthority,
                    StringComparison.Ordinal)
                || !string.Equals(
                    promoted.SourcePropertyPath,
                    decision.sourcePropertyPath,
                    StringComparison.Ordinal)
                || !string.Equals(promoted.AssetApplied, "true", StringComparison.Ordinal)
                || !active.Contains(promoted.ApprovalKey))
            {
                throw new InvalidOperationException(
                    "CONSTRUCTION_REVIEW_DECISION_STALE: promoted authority is not "
                    + "the exact approved target: " + decisionKey + ".");
            }
            applied++;
        }

        string[] unexpected = CaptureConstructionReviewCandidates(ledger)
            .Select(value => ConstructionDecisionIdentity(
                value.StableId,
                value.Metric))
            .Where(value => !decisionKeys.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (unexpected.Length != 0)
        {
            throw new InvalidOperationException(
                "CONSTRUCTION_REVIEW_DECISION_STALE: unreviewed candidates: "
                + string.Join(",", unexpected));
        }
        return new ConstructionReviewValidation(
            file.decisions.Length,
            applied,
            pending);
    }

    private static CanonicalBalanceMetricRecord[] CaptureConstructionReviewCandidates(
        FrozenBalanceLedger ledger) => ledger.Records
        .Where(value => string.Equals(
                value.Metric,
                V27BalanceAudit.ConstructionRecalibrationCandidateWuMetric,
                StringComparison.Ordinal)
            || value.Metric.StartsWith(
                V27BalanceAudit.ConstructionRecalibrationCandidateMaterialMetricPrefix,
                StringComparison.Ordinal))
        .OrderBy(value => value.StableId, StringComparer.Ordinal)
        .ThenBy(value => value.Metric, StringComparer.Ordinal)
        .ToArray();

    private static ConstructionReviewDecisionRowData CaptureConstructionDecisionRow(
        FrozenBalanceLedger ledger,
        CanonicalBalanceMetricRecord candidate)
    {
        string appliedMetric = ResolveConstructionAppliedMetric(candidate.Metric);
        CanonicalBalanceMetricRecord current = ledger.Records.Single(value =>
            string.Equals(value.StableId, candidate.StableId, StringComparison.Ordinal)
            && string.Equals(value.Metric, appliedMetric, StringComparison.Ordinal));
        if (!string.Equals(current.After, candidate.Before, StringComparison.Ordinal)
            || !string.Equals(current.SourceAuthority, candidate.SourceAuthority, StringComparison.Ordinal)
            || !string.Equals(current.SourcePropertyPath, candidate.SourcePropertyPath, StringComparison.Ordinal)
            || !string.Equals(current.AssetApplied, "true", StringComparison.Ordinal)
            || current.ApprovalKey.Length == 0)
        {
            throw new InvalidOperationException(
                "Construction candidate is not backed by exact applied custody: "
                + candidate.StableId + ":" + candidate.Metric + ".");
        }
        return new ConstructionReviewDecisionRowData
        {
            stableId = candidate.StableId,
            candidateMetric = candidate.Metric,
            appliedMetric = appliedMetric,
            sourceAuthority = candidate.SourceAuthority,
            sourcePropertyPath = candidate.SourcePropertyPath,
            beforeExactToken = candidate.Before,
            afterExactToken = candidate.After,
            dependencyFingerprint = candidate.DependencyFingerprint,
            sourceDigest = candidate.SourceDigest,
            semanticHash = candidate.SemanticHash,
            previousAppliedApprovalKey = current.ApprovalKey,
            decision = "promote-candidate"
        };
    }

    private static void RequireExactConstructionCandidate(
        ConstructionReviewDecisionRowData decision,
        CanonicalBalanceMetricRecord candidate)
    {
        if (!string.Equals(decision.decision, "promote-candidate", StringComparison.Ordinal)
            || !string.Equals(candidate.StableId, decision.stableId, StringComparison.Ordinal)
            || !string.Equals(candidate.Metric, decision.candidateMetric, StringComparison.Ordinal)
            || !string.Equals(candidate.SourceAuthority, decision.sourceAuthority, StringComparison.Ordinal)
            || !string.Equals(candidate.SourcePropertyPath, decision.sourcePropertyPath, StringComparison.Ordinal)
            || !string.Equals(candidate.Before, decision.beforeExactToken, StringComparison.Ordinal)
            || !string.Equals(candidate.After, decision.afterExactToken, StringComparison.Ordinal)
            || !string.Equals(candidate.DependencyFingerprint, decision.dependencyFingerprint, StringComparison.Ordinal)
            || !string.Equals(candidate.SourceDigest, decision.sourceDigest, StringComparison.Ordinal)
            || !string.Equals(candidate.SemanticHash, decision.semanticHash, StringComparison.Ordinal)
            || !string.Equals(candidate.AnomalyDisposition, "local-critical", StringComparison.Ordinal)
            || !string.Equals(candidate.ReviewStatus, "pending-explicit-review", StringComparison.Ordinal)
            || !string.Equals(candidate.AssetApplied, "false", StringComparison.Ordinal)
            || candidate.ApprovalKey.Length != 0)
        {
            throw new InvalidOperationException(
                "CONSTRUCTION_REVIEW_DECISION_STALE: candidate changed: "
                + decision.stableId + ":" + decision.candidateMetric + ".");
        }
    }

    private static string ResolveConstructionAppliedMetric(string candidateMetric)
    {
        if (string.Equals(
                candidateMetric,
                V27BalanceAudit.ConstructionRecalibrationCandidateWuMetric,
                StringComparison.Ordinal))
        {
            return "construction-authored-wu:redistributed";
        }
        if (candidateMetric.StartsWith(
                V27BalanceAudit.ConstructionRecalibrationCandidateMaterialMetricPrefix,
                StringComparison.Ordinal))
        {
            return "construction-material-amount:"
                + candidateMetric.Substring(
                    V27BalanceAudit.ConstructionRecalibrationCandidateMaterialMetricPrefix.Length);
        }
        throw new InvalidOperationException(
            "Unknown construction review candidate metric: " + candidateMetric + ".");
    }

    private static ConstructionReviewDecisionFileData LoadConstructionReviewDecisions()
    {
        string path = ProjectAbsolutePath(ConstructionReviewDecisionPath);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "V27 construction review decision authority is missing: "
                + ConstructionReviewDecisionPath);
        }
        return JsonUtility.FromJson<ConstructionReviewDecisionFileData>(
                   File.ReadAllText(path, new UTF8Encoding(false, true)))
               ?? throw new InvalidOperationException(
                   "V27 construction review decision authority is invalid JSON.");
    }

    private static void RequireConstructionDecisionRow(
        ConstructionReviewDecisionRowData row)
    {
        if (row == null
            || string.IsNullOrWhiteSpace(row.stableId)
            || string.IsNullOrWhiteSpace(row.candidateMetric)
            || string.IsNullOrWhiteSpace(row.appliedMetric)
            || string.IsNullOrWhiteSpace(row.sourceAuthority)
            || string.IsNullOrWhiteSpace(row.sourcePropertyPath)
            || string.IsNullOrWhiteSpace(row.beforeExactToken)
            || string.IsNullOrWhiteSpace(row.afterExactToken)
            || string.IsNullOrWhiteSpace(row.dependencyFingerprint)
            || string.IsNullOrWhiteSpace(row.sourceDigest)
            || string.IsNullOrWhiteSpace(row.semanticHash)
            || string.IsNullOrWhiteSpace(row.previousAppliedApprovalKey)
            || !string.Equals(row.decision, "promote-candidate", StringComparison.Ordinal)
            || !string.Equals(
                row.appliedMetric,
                ResolveConstructionAppliedMetric(row.candidateMetric),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "CONSTRUCTION_REVIEW_DECISION_STALE: decision row is incomplete.");
        }
    }

    private static CanonicalBalanceMetricRecord TryRequireSingle(
        IReadOnlyDictionary<string, CanonicalBalanceMetricRecord[]> records,
        string identity)
    {
        if (!records.TryGetValue(identity, out CanonicalBalanceMetricRecord[] matches))
            return null;
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "Construction ledger identity is not unique: " + identity + ".");
        }
        return matches[0];
    }

    private static CanonicalBalanceMetricRecord RequireSingle(
        IReadOnlyDictionary<string, CanonicalBalanceMetricRecord[]> records,
        string identity) => TryRequireSingle(records, identity)
        ?? throw new InvalidOperationException(
            "Construction ledger authority is missing: " + identity + ".");

    private static string ConstructionDecisionIdentity(
        string stableId,
        string metric) => stableId + "\u001f" + metric;

    private static string ComputeConstructionDecisionPayloadDigest(
        string sourceLedgerDigest,
        IEnumerable<ConstructionReviewDecisionRowData> rows)
    {
        StringBuilder canonical = new();
        AppendCanonicalField(canonical, ConstructionReviewDecisionSchema);
        AppendCanonicalField(canonical, sourceLedgerDigest);
        foreach (ConstructionReviewDecisionRowData row in rows
                     .OrderBy(value => value.stableId, StringComparer.Ordinal)
                     .ThenBy(value => value.candidateMetric, StringComparer.Ordinal))
        {
            RequireConstructionDecisionRow(row);
            AppendCanonicalField(canonical, row.stableId);
            AppendCanonicalField(canonical, row.candidateMetric);
            AppendCanonicalField(canonical, row.appliedMetric);
            AppendCanonicalField(canonical, row.sourceAuthority);
            AppendCanonicalField(canonical, row.sourcePropertyPath);
            AppendCanonicalField(canonical, row.beforeExactToken);
            AppendCanonicalField(canonical, row.afterExactToken);
            AppendCanonicalField(canonical, row.dependencyFingerprint);
            AppendCanonicalField(canonical, row.sourceDigest);
            AppendCanonicalField(canonical, row.semanticHash);
            AppendCanonicalField(canonical, row.previousAppliedApprovalKey);
            AppendCanonicalField(canonical, row.decision);
        }
        using SHA256 sha = SHA256.Create();
        return HashBytes(sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(canonical.ToString())));
    }

    private static string ComputeConstructionDecisionEpochDigest(string payloadDigest)
    {
        StringBuilder canonical = new();
        AppendCanonicalField(canonical, ConstructionReviewDecisionSchema);
        AppendCanonicalField(canonical, payloadDigest);
        using SHA256 sha = SHA256.Create();
        return HashBytes(sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(canonical.ToString())));
    }

    private static byte[] SerializeConstructionDecisionAuthority(
        ConstructionReviewDecisionFileData file) => new UTF8Encoding(false, true)
        .GetBytes(JsonUtility.ToJson(file, prettyPrint: true) + "\n");

    private static void WriteConstructionDecisionAuthorityTwice(byte[] bytes)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(
            ConstructionReviewDecisionPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        byte[] first = File.ReadAllBytes(ProjectAbsolutePath(
            ConstructionReviewDecisionPath));
        V27BalanceArtifactWriter.WriteIfDifferent(
            ConstructionReviewDecisionPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        byte[] second = File.ReadAllBytes(ProjectAbsolutePath(
            ConstructionReviewDecisionPath));
        if (!first.SequenceEqual(second))
        {
            throw new InvalidOperationException(
                "Construction decision authority second write was not a no-op.");
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
    }

    private static void WriteConstructionReviewPromotionReport(
        ConstructionReviewValidation validation,
        int derivedRootCount,
        int approvedDerivedRootCount,
        int remainingCritical,
        int noOpDifferingProperties)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(
            ConstructionReviewPromotionReportPath,
            stream =>
            {
                using StreamWriter writer = new(
                    stream,
                    new UTF8Encoding(false, true),
                    4096,
                    leaveOpen: true)
                {
                    NewLine = "\n"
                };
                writer.WriteLine("schemaVersion=v27.construction-review-application.1");
                writer.WriteLine("result=PASS");
                writer.WriteLine("decisionRows=" + validation.DecisionCount);
                writer.WriteLine("appliedDecisionRows=" + validation.AppliedCount);
                writer.WriteLine("pendingDecisionRows=" + validation.Pending.Count);
                writer.WriteLine("derivedApprovalOnlyRoots=" + derivedRootCount);
                writer.WriteLine("approvedDerivedRoots=" + approvedDerivedRootCount);
                writer.WriteLine("remainingCritical=" + remainingCritical);
                writer.WriteLine("secondBuildDifferingProperties="
                    + noOpDifferingProperties);
                writer.Flush();
            });
    }

    [Serializable]
    private sealed class ConstructionReviewDecisionFileData
    {
        public string schemaVersion;
        public string decisionEpochId;
        public string sourceLedgerDigest;
        public string decisionPayloadDigest;
        public string decisionEpochDigest;
        public ConstructionReviewDecisionRowData[] decisions;
    }

    [Serializable]
    private sealed class ConstructionReviewDecisionRowData
    {
        public string stableId;
        public string candidateMetric;
        public string appliedMetric;
        public string sourceAuthority;
        public string sourcePropertyPath;
        public string beforeExactToken;
        public string afterExactToken;
        public string dependencyFingerprint;
        public string sourceDigest;
        public string semanticHash;
        public string previousAppliedApprovalKey;
        public string decision;
    }
}

public sealed class ConstructionReviewValidation
{
    internal ConstructionReviewValidation(
        int decisionCount,
        int appliedCount,
        IReadOnlyList<CanonicalBalanceMetricRecord> pending)
    {
        DecisionCount = decisionCount;
        AppliedCount = appliedCount;
        Pending = pending ?? Array.Empty<CanonicalBalanceMetricRecord>();
    }

    public int DecisionCount { get; }
    public int AppliedCount { get; }
    public IReadOnlyList<CanonicalBalanceMetricRecord> Pending { get; }

    public string Format(string action) =>
        "V27 construction review decisions " + action
        + $": decisions={DecisionCount}; applied={AppliedCount}; "
        + $"pending={Pending.Count}.";
}
#endif
