#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static partial class V27BalanceAssetApplication
{
    [MenuItem("DungeonStory/V27/Adopt Current Market Recommendations As Exact Decision Epoch")]
    public static void AdoptCurrentMarketRecommendationsAsExactDecisionEpochFromMenu()
    {
        string decisionPath = ProjectAbsolutePath(MarketReviewDecisionPath);
        string budgetPath = ProjectAbsolutePath(FactionBenefitBudgetAssetPath);
        string approvalPath = ProjectAbsolutePath(V27BalanceAudit.ApprovalPath);
        byte[] decisionRollback = File.ReadAllBytes(decisionPath);
        byte[] budgetRollback = File.ReadAllBytes(budgetPath);
        byte[] approvalRollback = File.ReadAllBytes(approvalPath);
        string phase = "capture-current-authority";

        try
        {
            V27BalanceAuditOutput audit = V27BalanceAudit.GenerateForApprovalRefresh();
            V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
                audit,
                requireApplied: true,
                allowUnapprovedCritical: true);
            V27BalanceLaborFacilityDebugScenarios.RequireOnlyTypedPostRebaseCriticals(
                audit);

            phase = "refresh-supply-quote-digests";
            RefreshFactionBenefitBudgetQuoteDigests();

            phase = "revalidate-applied-approval-custody";
            RevalidateSemanticallyUnchangedAppliedApprovalsFromMenu();

            phase = "capture-post-revalidation-authority";
            audit = V27BalanceAudit.GenerateForApprovalRefresh();
            if (audit.IntegrityFailures.Count != 0)
            {
                throw new InvalidOperationException(
                    "Market decision epoch prerequisite audit failed:\n"
                    + string.Join("\n", audit.IntegrityFailures));
            }
            phase = "refresh-alliance-benefit-budget";
            FactionAllianceBenefitBudgetReviewSnapshot budgetAuthority =
                FactionAllianceBenefitBudgetReviewAuthority.Capture(audit.Ledger);
            ApplyFactionBenefitBudgetReviewAuthority(budgetAuthority);

            phase = "capture-reviewed-ledger";
            audit = V27BalanceAudit.Generate(BalanceLedgerExecutionMode.AuditOnly);
            V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
                audit,
                requireApplied: true,
                allowUnapprovedCritical: true);
            V27BalanceLaborFacilityDebugScenarios.RequireOnlyTypedPostRebaseCriticals(
                audit);
            RequireFactionBenefitBudgetReviewAuthorityMatches(
                FactionAllianceBenefitBudgetReviewAuthority.Capture(audit.Ledger));
            V27BalanceMarketDebugScenarios.MarketReviewBundleRow[] rows =
                V27BalanceMarketDebugScenarios.BuildMarketReviewBundleRows(
                    audit.Ledger.Records);
            V27BalanceMarketDebugScenarios.RequireMarketReviewBundlePartition(rows);
            if (rows.Length == 0)
            {
                throw new InvalidOperationException(
                    "MARKET_REVIEW_DECISION_SCOPE_EMPTY: no current review candidate exists.");
            }

            phase = "build-exact-decision-epoch";
            MarketReviewDecisionData[] decisions = rows
                .GroupBy(value => value.BundleId, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(CreateExactMarketDecision)
                .ToArray();
            CanonicalBalanceMetricRecord[] candidates = audit.Ledger.Records
                .Where(value => value.Metric.StartsWith(
                    V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix,
                    StringComparison.Ordinal))
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .ThenBy(value => value.Metric, StringComparer.Ordinal)
                .ToArray();
            CanonicalBalanceMetricRecord[] coupledAuthorities =
                CaptureCoupledUnappliedMarketAuthorities(audit.Ledger, candidates);
            string patchScopeDigest = ComputeMarketPatchScopeDigest(
                candidates,
                coupledAuthorities);
            string sourceLedgerDigest = audit.AuthoritySnapshot.SourceDigest;
            string previousAuthorityDigest = HashBytes(decisionRollback);
            MarketReviewDecisionFileData previous = JsonUtility.FromJson<
                MarketReviewDecisionFileData>(
                new UTF8Encoding(false, true).GetString(decisionRollback));
            string payloadDigest = ComputeMarketDecisionPayloadDigest(
                sourceLedgerDigest,
                patchScopeDigest,
                decisions);
            bool repeatsCurrentPayload = previous != null
                && string.Equals(
                    previous.schemaVersion,
                    MarketReviewDecisionSchema,
                    StringComparison.Ordinal)
                && string.Equals(
                    previous.decisionPayloadDigest,
                    payloadDigest,
                    StringComparison.Ordinal);
            string previousEpochDigest = repeatsCurrentPayload
                ? previous.previousDecisionEpochDigest
                : !string.IsNullOrWhiteSpace(previous?.decisionEpochDigest)
                    ? previous.decisionEpochDigest
                    : "legacy-file-sha256:" + previousAuthorityDigest;
            if (repeatsCurrentPayload)
                previousAuthorityDigest = previous.previousDecisionAuthorityDigest;
            string epochDigest = ComputeMarketDecisionEpochDigest(
                payloadDigest,
                previousEpochDigest,
                previousAuthorityDigest);
            MarketReviewDecisionFileData file = new()
            {
                schemaVersion = MarketReviewDecisionSchema,
                epochId = "market-review-epoch:" + epochDigest.ToLowerInvariant(),
                decisionPayloadDigest = payloadDigest,
                decisionEpochDigest = epochDigest,
                sourceLedgerDigest = sourceLedgerDigest,
                patchScopeDigest = patchScopeDigest,
                previousDecisionEpochDigest = previousEpochDigest,
                previousDecisionAuthorityDigest = previousAuthorityDigest,
                decisions = decisions
            };

            phase = "write-exact-decision-epoch";
            byte[] bytes = SerializeMarketDecisionAuthority(file);
            WriteMarketDecisionAuthorityTwice(bytes);

            phase = "validate-exact-decision-epoch";
            MarketReviewDecisionValidation validation =
                ValidateMarketReviewDecisions(audit.Ledger);
            int expectedPromotions = decisions
                .SelectMany(value => value.members)
                .Count(value => string.Equals(
                    value.decision,
                    "promote-candidate",
                    StringComparison.Ordinal));
            if (validation.PendingPromotions.Count != expectedPromotions)
            {
                throw new InvalidOperationException(
                    "MARKET_REVIEW_DECISION_PENDING_SCOPE_MISMATCH: expected="
                    + expectedPromotions.ToString(CultureInfo.InvariantCulture)
                    + "; actual="
                    + validation.PendingPromotions.Count.ToString(
                        CultureInfo.InvariantCulture));
            }

            Debug.Log(
                validation.Format("exact-epoch-adopted")
                + "; epoch=" + file.epochId
                + "; members=" + rows.Length.ToString(CultureInfo.InvariantCulture)
                + "; secondWriteDiff=0");
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(decisionPath, decisionRollback);
            File.WriteAllBytes(budgetPath, budgetRollback);
            File.WriteAllBytes(approvalPath, approvalRollback);
            AssetDatabase.ImportAsset(
                FactionBenefitBudgetAssetPath,
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(
                V27BalanceAudit.ApprovalPath,
                ImportAssetOptions.ForceUpdate);
            throw new InvalidOperationException(
                "Market decision epoch adoption failed in phase '"
                + phase + "'.",
                exception);
        }
    }

    private static MarketReviewDecisionData CreateExactMarketDecision(
        IGrouping<string, V27BalanceMarketDebugScenarios.MarketReviewBundleRow> group)
    {
        V27BalanceMarketDebugScenarios.MarketReviewBundleRow[] rows = group
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .ThenBy(value => value.AuthorityMetric, StringComparer.Ordinal)
            .ToArray();
        string[] recommendations = rows
            .Select(value => value.RecommendedDecision)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (recommendations.Length != 1)
        {
            throw new InvalidOperationException(
                "MARKET_REVIEW_DECISION_MIXED_RECOMMENDATION: " + group.Key);
        }
        string decision = string.Equals(
                recommendations[0],
                "promote-candidate",
                StringComparison.Ordinal)
            ? "promote-candidate"
            : "rework";
        return new MarketReviewDecisionData
        {
            bundleId = group.Key,
            bundleDigest = rows[0].BundleDigest,
            anchorItemId = rows[0].AnchorItemId,
            decisionReason = "Adopted current deterministic recommendation: "
                + rows[0].RecommendationReason,
            reviewedBaselineRecordId = MarketReviewDecisionBaseline,
            members = rows.Select(value => new MarketReviewDecisionMemberData
                {
                    stableId = value.StableId,
                    authorityMetric = value.AuthorityMetric,
                    sourceAuthority = value.SourceAuthority,
                    sourcePropertyPath = value.SourcePropertyPath,
                    beforeExactToken = value.Before,
                    candidateExactToken = value.Candidate,
                    dependencyFingerprint = value.DependencyFingerprint,
                    sourceDigest = value.SourceDigest,
                    semanticHash = value.SemanticHash,
                    promotedAuthorityDependencyFingerprint =
                        value.DependencyFingerprint,
                    promotedAuthoritySourceDigest = value.SourceDigest,
                    promotedAuthoritySemanticHash =
                        V27BalanceAudit.BuildMarketAuthoritySemanticHash(
                            value.StableId,
                            value.AuthorityMetric,
                            value.Candidate),
                    decision = decision,
                    replacementExactToken = string.Empty
                })
                .ToArray()
        };
    }

    private static string ComputeMarketDecisionPayloadDigest(
        string sourceLedgerDigest,
        string patchScopeDigest,
        IEnumerable<MarketReviewDecisionData> decisions)
    {
        StringBuilder canonical = new();
        AppendCanonicalField(canonical, "schema");
        AppendCanonicalField(canonical, MarketReviewDecisionSchema);
        AppendCanonicalField(canonical, "source-ledger");
        AppendCanonicalField(canonical, sourceLedgerDigest);
        AppendCanonicalField(canonical, "patch-scope");
        AppendCanonicalField(canonical, patchScopeDigest);
        foreach (MarketReviewDecisionData decision in decisions
                     .OrderBy(value => value.bundleId, StringComparer.Ordinal))
        {
            AppendCanonicalField(canonical, "bundle");
            AppendCanonicalField(canonical, decision.bundleId);
            AppendCanonicalField(canonical, decision.bundleDigest);
            AppendCanonicalField(canonical, decision.anchorItemId);
            AppendCanonicalField(canonical, decision.decisionReason);
            AppendCanonicalField(canonical, decision.reviewedBaselineRecordId);
            foreach (MarketReviewDecisionMemberData member in decision.members
                         .OrderBy(value => value.stableId, StringComparer.Ordinal)
                         .ThenBy(value => value.authorityMetric, StringComparer.Ordinal))
            {
                AppendCanonicalField(canonical, "member");
                AppendCanonicalField(canonical, member.stableId);
                AppendCanonicalField(canonical, member.authorityMetric);
                AppendCanonicalField(canonical, member.sourceAuthority);
                AppendCanonicalField(canonical, member.sourcePropertyPath);
                AppendCanonicalField(canonical, member.beforeExactToken);
                AppendCanonicalField(canonical, member.candidateExactToken);
                AppendCanonicalField(canonical, member.dependencyFingerprint);
                AppendCanonicalField(canonical, member.sourceDigest);
                AppendCanonicalField(canonical, member.semanticHash);
                AppendCanonicalField(
                    canonical,
                    member.promotedAuthorityDependencyFingerprint);
                AppendCanonicalField(canonical, member.promotedAuthoritySourceDigest);
                AppendCanonicalField(canonical, member.promotedAuthoritySemanticHash);
                AppendCanonicalField(canonical, member.decision);
                AppendCanonicalField(canonical, member.replacementExactToken);
            }
        }
        return HashBytes(new UTF8Encoding(false, true).GetBytes(canonical.ToString()));
    }

    private static string ComputeMarketPatchScopeDigest(
        IEnumerable<CanonicalBalanceMetricRecord> candidates,
        IEnumerable<CanonicalBalanceMetricRecord> coupledAuthorities)
    {
        var rows = (candidates ?? Array.Empty<CanonicalBalanceMetricRecord>())
            .Select(value => (Role: "review-candidate", Record: value))
            .Concat((coupledAuthorities ?? Array.Empty<CanonicalBalanceMetricRecord>())
                .Select(value => (Role: "coupled-authority", Record: value)))
            .OrderBy(value => value.Role, StringComparer.Ordinal)
            .ThenBy(value => value.Record.StableId, StringComparer.Ordinal)
            .ThenBy(value => value.Record.Metric, StringComparer.Ordinal)
            .ToArray();
        StringBuilder canonical = new();
        foreach (var row in rows)
        {
            AppendCanonicalField(canonical, row.Role);
            AppendCanonicalField(canonical, row.Record.StableId);
            AppendCanonicalField(canonical, row.Record.Metric);
            AppendCanonicalField(canonical, row.Record.SourceAuthority);
            AppendCanonicalField(canonical, row.Record.SourcePropertyPath);
            AppendCanonicalField(canonical, row.Record.Before);
            AppendCanonicalField(canonical, row.Record.After);
            AppendCanonicalField(canonical, row.Record.DependencyFingerprint);
            AppendCanonicalField(canonical, row.Record.SourceDigest);
            AppendCanonicalField(canonical, row.Record.SemanticHash);
        }
        return HashBytes(new UTF8Encoding(false, true).GetBytes(canonical.ToString()));
    }

    private static string ComputeMarketDecisionEpochDigest(
        string payloadDigest,
        string previousEpochDigest,
        string previousAuthorityDigest)
    {
        StringBuilder canonical = new();
        AppendCanonicalField(canonical, "schema");
        AppendCanonicalField(canonical, MarketReviewDecisionSchema);
        AppendCanonicalField(canonical, "payload");
        AppendCanonicalField(canonical, payloadDigest);
        AppendCanonicalField(canonical, "previous-epoch");
        AppendCanonicalField(canonical, previousEpochDigest);
        AppendCanonicalField(canonical, "previous-authority");
        AppendCanonicalField(canonical, previousAuthorityDigest);
        return HashBytes(new UTF8Encoding(false, true).GetBytes(canonical.ToString()));
    }

    private static void AppendCanonicalField(StringBuilder writer, string value)
    {
        if (writer == null)
            throw new ArgumentNullException(nameof(writer));
        if (value == null)
            throw new InvalidOperationException("MARKET_REVIEW_CANONICAL_FIELD_NULL");
        writer.Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
    }

    private static byte[] SerializeMarketDecisionAuthority(
        MarketReviewDecisionFileData file) => new UTF8Encoding(false, true).GetBytes(
        JsonUtility.ToJson(file, prettyPrint: true) + "\n");

    private static void WriteMarketDecisionAuthorityTwice(byte[] bytes)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(
            MarketReviewDecisionPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        string absolute = ProjectAbsolutePath(MarketReviewDecisionPath);
        string firstHash = HashBytes(File.ReadAllBytes(absolute));
        long firstLength = new FileInfo(absolute).Length;
        long firstTicks = File.GetLastWriteTimeUtc(absolute).Ticks;
        V27BalanceArtifactWriter.WriteIfDifferent(
            MarketReviewDecisionPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        if (!string.Equals(
                firstHash,
                HashBytes(File.ReadAllBytes(absolute)),
                StringComparison.Ordinal)
            || firstLength != new FileInfo(absolute).Length
            || firstTicks != File.GetLastWriteTimeUtc(absolute).Ticks)
        {
            throw new InvalidOperationException(
                "MARKET_REVIEW_DECISION_SECOND_WRITE_NOT_NO_OP");
        }
    }

    private static string HashBytes(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(bytes ?? Array.Empty<byte>());
        StringBuilder result = new(digest.Length * 2);
        foreach (byte value in digest)
            result.Append(value.ToString("X2", CultureInfo.InvariantCulture));
        return result.ToString();
    }
}
#endif
