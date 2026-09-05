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
    internal const string MarketReviewDecisionPath =
        "docs/game-design/v27-balance-market-review-decisions.json";
    internal const string MarketReviewDecisionSchema =
        "v27.market-review-decisions.3";
    internal const string MarketReviewDecisionBaseline =
        "balance:v27:market-review-recommendation-unpriced-inflow-v1";
    private const string MarketReviewPromotionReportPath =
        "Artifacts/QA/v27-balance-market-review-application.txt";
    private const string FactionBenefitBudgetAssetPath =
        "Assets/Resources/SO/Factions/FactionAllianceBenefitBudget.asset";

    [MenuItem("DungeonStory/V27/Validate Market Review Decisions")]
    public static void ValidateMarketReviewDecisionsFromMenu()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        MarketReviewDecisionValidation validation =
            ValidateMarketReviewDecisions(audit.Ledger);
        WriteMarketReviewPromotionReport(validation, audit.CriticalCount, 0, 0);
        Debug.Log(validation.Format("validated"));
    }

    [MenuItem("DungeonStory/V27/Refresh Market Decisions From Current Recommendations")]
    public static void RefreshMarketDecisionsFromCurrentRecommendationsFromMenu()
    {
        // Refreshing member decisions without rebuilding the epoch fields leaves a
        // syntactically valid but stale authority. Keep one transactional writer for
        // both refresh and first adoption so source, member, and epoch digests cannot
        // drift independently.
        AdoptCurrentMarketRecommendationsAsExactDecisionEpochFromMenu();
    }

    [MenuItem("DungeonStory/V27/Apply Reviewed Market Promotions")]
    public static void ApplyReviewedMarketPromotionsFromMenu()
    {
        V27BalanceAuditOutput beforeAudit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        if (beforeAudit.IntegrityFailures.Count != 0)
        {
            throw new InvalidOperationException(
                "Cannot apply market review decisions from an invalid ledger:\n"
                + string.Join("\n", beforeAudit.IntegrityFailures));
        }
        V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
            beforeAudit,
            requireApplied: true,
            allowUnapprovedCritical: true);
        V27BalanceLaborFacilityDebugScenarios.RequireOnlyTypedPostRebaseCriticals(
            beforeAudit);

        MarketReviewDecisionValidation before =
            ValidateMarketReviewDecisions(beforeAudit.Ledger);
        MarketReviewDecisionFileData decisionFile = LoadMarketReviewDecisions();
        CanonicalBalanceMetricRecord[] candidates = before.PendingPromotions
            .OrderBy(value => value.SourceAuthority, StringComparer.Ordinal)
            .ThenBy(value => value.SourcePropertyPath, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
        {
            MarketApplicationReceiptValidation receipt =
                ValidateMarketApplicationReceipts(beforeAudit.Ledger);
            WriteMarketSecondApplyNoOpReceipt(
                decisionFile,
                beforeAudit.Ledger,
                receipt);
            WriteMarketReviewPromotionReport(
                before,
                beforeAudit.CriticalCount,
                0,
                0);
            Debug.Log(before.Format("no-op") + "; " + receipt.Format("verified"));
            return;
        }

        CanonicalBalanceMetricRecord[] coupledAuthorities =
            CaptureCoupledUnappliedMarketAuthorities(
                beforeAudit.Ledger,
                candidates);
        string actualPatchScopeDigest = ComputeMarketPatchScopeDigest(
            candidates,
            coupledAuthorities);
        if (!string.Equals(
                decisionFile.patchScopeDigest,
                actualPatchScopeDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_REVIEW_PATCH_SCOPE_STALE: decision epoch does not own the "
                + "exact candidate and coupled-authority patch set.");
        }
        List<BalanceAssetPatch> patches = candidates
            .Select(BalanceAssetPatch.CaptureForMarketReviewPromotion)
            .Concat(coupledAuthorities.Select(BalanceAssetPatch.Capture))
            .GroupBy(
                value => value.AssetPath + "\u001f" + value.PropertyPath,
                StringComparer.Ordinal)
            .Select(group => group.Single())
            .ToList();
        string[] paths = patches
            .Select(value => value.AssetPath)
            .Append(FactionBenefitBudgetAssetPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        byte[] approvalRollback = File.ReadAllBytes(
            ProjectAbsolutePath(V27BalanceAudit.ApprovalPath));
        Dictionary<string, byte[]> assetRollback = paths.ToDictionary(
            value => value,
            value => File.ReadAllBytes(ProjectAbsolutePath(value)),
            StringComparer.Ordinal);
        string receiptPath = ResolveMarketApplicationReceiptPath(
            decisionFile.decisionEpochDigest);
        string receiptAbsolute = ProjectAbsolutePath(receiptPath);
        bool receiptExisted = File.Exists(receiptAbsolute);
        byte[] receiptRollback = receiptExisted
            ? File.ReadAllBytes(receiptAbsolute)
            : Array.Empty<byte>();
        string phase = "prepare-temporary-custody";

        try
        {
            WriteTemporaryMarketPromotionCustody(
                beforeAudit.Ledger,
                candidates);

            phase = "apply-atomic-property-bundle";
            BalanceAssetApplicationResult applied = ApplyPatches(
                patches,
                dryRun: false,
                requireCleanGit: false,
                BalanceAssetApplicationFailurePoint.None);

            phase = "refresh-faction-supply-quote-digests";
            RefreshFactionBenefitBudgetQuoteDigests();

            phase = "recapture-post-application-authority";
            V27BalanceAuditOutput afterAudit =
                V27BalanceAudit.GenerateForApprovalRefresh();
            if (afterAudit.IntegrityFailures.Count != 0)
            {
                throw new InvalidOperationException(
                    "Market promotion produced ledger integrity failures:\n"
                    + string.Join("\n", afterAudit.IntegrityFailures));
            }

            phase = "replace-temporary-custody-with-canonical-approvals";
            WriteApprovals(
                afterAudit.Ledger,
                record => ItemMarketApprovalMetrics.Contains(record.Metric),
                replaceIncludedApprovals: true);

            phase = "revalidate-all-applied-approval-custody";
            RevalidateSemanticallyUnchangedAppliedApprovalsFromMenu();

            phase = "recapture-post-approval-authority";
            afterAudit = V27BalanceAudit.GenerateForApprovalRefresh();
            if (afterAudit.IntegrityFailures.Count != 0)
            {
                throw new InvalidOperationException(
                    "Canonical approval refresh produced ledger integrity failures:\n"
                    + string.Join("\n", afterAudit.IntegrityFailures));
            }

            phase = "refresh-faction-benefit-balance-authority";
            FactionAllianceBenefitBudgetReviewSnapshot budgetAuthority =
                FactionAllianceBenefitBudgetReviewAuthority.Capture(afterAudit.Ledger);
            ApplyFactionBenefitBudgetReviewAuthority(budgetAuthority);

            phase = "strict-post-application-validation";
            V27BalanceAuditOutput verifiedAudit = V27BalanceAudit.Generate(
                BalanceLedgerExecutionMode.AuditOnly);
            if (verifiedAudit.IntegrityFailures.Count != 0)
            {
                throw new InvalidOperationException(
                    "Canonical market approvals did not survive strict validation:\n"
                    + string.Join("\n", verifiedAudit.IntegrityFailures));
            }
            V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
                verifiedAudit,
                requireApplied: true,
                allowUnapprovedCritical: true);
            V27BalanceLaborFacilityDebugScenarios.RequireOnlyTypedPostRebaseCriticals(
                verifiedAudit);
            RequireFactionBenefitBudgetReviewAuthorityMatches(
                FactionAllianceBenefitBudgetReviewAuthority.Capture(
                    verifiedAudit.Ledger));
            MarketReviewDecisionValidation verified =
                ValidateMarketReviewDecisions(verifiedAudit.Ledger);
            if (verified.PendingPromotions.Count != 0)
            {
                throw new InvalidOperationException(
                    "Reviewed market promotion was not exact-once; remaining="
                    + verified.PendingPromotions.Count.ToString(
                        CultureInfo.InvariantCulture));
            }

            phase = "write-immutable-market-application-receipt";
            MarketApplicationReceiptValidation receipt =
                WriteMarketApplicationReceiptV2(
                    decisionFile,
                    verifiedAudit.Ledger,
                    candidates,
                    coupledAuthorities,
                    assetRollback,
                    approvalRollback);

            WriteMarketReviewPromotionReport(
                verified,
                verifiedAudit.CriticalCount,
                applied.AssetCount + 1,
                applied.DifferingPropertyCount);
            Debug.Log(
                verified.Format("applied")
                + $"; assets={applied.AssetCount + 1}; properties={applied.DifferingPropertyCount}; "
                + $"critical={verifiedAudit.CriticalCount}; "
                + receipt.Format("written"));
        }
        catch (Exception exception)
        {
            File.WriteAllBytes(
                ProjectAbsolutePath(V27BalanceAudit.ApprovalPath),
                approvalRollback);
            foreach (KeyValuePair<string, byte[]> pair in assetRollback)
                File.WriteAllBytes(ProjectAbsolutePath(pair.Key), pair.Value);
            if (receiptExisted)
                File.WriteAllBytes(receiptAbsolute, receiptRollback);
            else if (File.Exists(receiptAbsolute))
                File.Delete(receiptAbsolute);
            AssetDatabase.ImportAsset(
                V27BalanceAudit.ApprovalPath,
                ImportAssetOptions.ForceUpdate);
            foreach (string path in paths)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            WriteMarketReviewPromotionFailureReport(phase, exception);
            throw new InvalidOperationException(
                $"Reviewed market promotion failed in phase '{phase}'.",
                exception);
        }
    }

    internal static MarketReviewDecisionValidation ValidateMarketReviewDecisions(
        FrozenBalanceLedger ledger)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        MarketReviewDecisionFileData file = LoadMarketReviewDecisions();
        if (!string.Equals(
                file.schemaVersion,
                MarketReviewDecisionSchema,
                StringComparison.Ordinal)
            || file.decisions == null
            || string.IsNullOrWhiteSpace(file.epochId)
            || string.IsNullOrWhiteSpace(file.decisionPayloadDigest)
            || string.IsNullOrWhiteSpace(file.decisionEpochDigest)
            || string.IsNullOrWhiteSpace(file.sourceLedgerDigest)
            || string.IsNullOrWhiteSpace(file.patchScopeDigest)
            || string.IsNullOrWhiteSpace(file.previousDecisionEpochDigest)
            || string.IsNullOrWhiteSpace(file.previousDecisionAuthorityDigest))
        {
            throw new InvalidOperationException(
                "MARKET_REVIEW_DECISION_STALE: invalid decision schema.");
        }

        MarketReviewDecisionData[] decisions = file.decisions
            .OrderBy(value => value?.bundleId, StringComparer.Ordinal)
            .ToArray();
        if (decisions.Any(value => value == null)
            || decisions.Select(value => value.bundleId)
                .Distinct(StringComparer.Ordinal).Count() != decisions.Length)
        {
            throw new InvalidOperationException(
                "MARKET_REVIEW_DECISION_STALE: decision bundles must be non-null and unique.");
        }
        string computedPayloadDigest = ComputeMarketDecisionPayloadDigest(
            file.sourceLedgerDigest,
            file.patchScopeDigest,
            decisions);
        string computedEpochDigest = ComputeMarketDecisionEpochDigest(
            computedPayloadDigest,
            file.previousDecisionEpochDigest,
            file.previousDecisionAuthorityDigest);
        if (!string.Equals(
                file.decisionPayloadDigest,
                computedPayloadDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                file.decisionEpochDigest,
                computedEpochDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                file.epochId,
                "market-review-epoch:" + computedEpochDigest.ToLowerInvariant(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_REVIEW_DECISION_STALE: decision epoch digest changed.");
        }

        V27BalanceMarketDebugScenarios.MarketReviewBundleRow[] currentRows =
            V27BalanceMarketDebugScenarios.BuildMarketReviewBundleRows(
                ledger.Records);
        Dictionary<string, V27BalanceMarketDebugScenarios.MarketReviewBundleRow[]> currentByBundle =
            currentRows.GroupBy(value => value.BundleId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(value => value.StableId, StringComparer.Ordinal)
                        .ThenBy(value => value.AuthorityMetric, StringComparer.Ordinal)
                        .ToArray(),
                    StringComparer.Ordinal);
        Dictionary<string, CanonicalBalanceMetricRecord> authorityByIdentity = ledger.Records
            .Where(value => !value.Metric.StartsWith(
                V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix,
                StringComparison.Ordinal))
            .GroupBy(
                value => BuildApprovalIdentity(value.StableId, value.Metric),
                StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        Dictionary<string, CanonicalBalanceMetricRecord> candidateByIdentity = ledger.Records
            .Where(value => value.Metric.StartsWith(
                V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix,
                StringComparison.Ordinal))
            .ToDictionary(
                value => BuildApprovalIdentity(
                    value.StableId,
                    value.Metric.Substring(
                        V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix.Length)),
                StringComparer.Ordinal);

        HashSet<string> decidedCurrentBundles = new(StringComparer.Ordinal);
        List<CanonicalBalanceMetricRecord> pendingPromotions = new();
        int promotedBundles = 0;
        int retainedBundles = 0;
        int reworkBundles = 0;
        int appliedBundles = 0;

        foreach (MarketReviewDecisionData decision in decisions)
        {
            RequireDecisionHeader(decision);
            MarketReviewDecisionMemberData[] members = decision.members
                .OrderBy(value => value?.stableId, StringComparer.Ordinal)
                .ThenBy(value => value?.authorityMetric, StringComparer.Ordinal)
                .ToArray();
            if (members.Any(value => value == null)
                || members.Select(value => BuildApprovalIdentity(
                        value.stableId,
                        value.authorityMetric))
                    .Distinct(StringComparer.Ordinal).Count() != members.Length)
            {
                throw Stale(decision.bundleId, "member list is null or duplicated");
            }

            bool hasCurrentBundle = currentByBundle.TryGetValue(
                decision.bundleId,
                out V27BalanceMarketDebugScenarios.MarketReviewBundleRow[] currentBundle);
            if (hasCurrentBundle)
            {
                decidedCurrentBundles.Add(decision.bundleId);
                if (!string.Equals(
                        decision.bundleDigest,
                        currentBundle[0].BundleDigest,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        decision.anchorItemId,
                        currentBundle[0].AnchorItemId,
                        StringComparison.Ordinal)
                    || currentBundle.Length != members.Length)
                {
                    throw Stale(decision.bundleId, "bundle identity or member count changed");
                }
            }

            bool allApplied = true;
            HashSet<string> memberDecisions = new(StringComparer.Ordinal);
            foreach (MarketReviewDecisionMemberData member in members)
            {
                RequireDecisionMember(decision, member);
                memberDecisions.Add(member.decision);
                string identity = BuildApprovalIdentity(
                    member.stableId,
                    member.authorityMetric);
                if (candidateByIdentity.TryGetValue(
                        identity,
                        out CanonicalBalanceMetricRecord candidate))
                {
                    allApplied = false;
                    if (!hasCurrentBundle)
                        throw Stale(decision.bundleId, "candidate bundle is missing");
                    V27BalanceMarketDebugScenarios.MarketReviewBundleRow row =
                        currentBundle.Single(value => string.Equals(
                                value.StableId,
                                member.stableId,
                                StringComparison.Ordinal)
                            && string.Equals(
                                value.AuthorityMetric,
                                member.authorityMetric,
                                StringComparison.Ordinal));
                    RequireExactDecisionMember(decision, member, row);
                    if (string.Equals(
                        member.decision,
                        "promote-candidate",
                        StringComparison.Ordinal))
                    {
                        pendingPromotions.Add(candidate);
                    }
                    continue;
                }

                if (!string.Equals(
                        member.decision,
                        "promote-candidate",
                        StringComparison.Ordinal)
                    || !authorityByIdentity.TryGetValue(
                        identity,
                        out CanonicalBalanceMetricRecord authority)
                    || !string.Equals(
                        authority.After,
                        member.candidateExactToken,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        authority.Before,
                        member.beforeExactToken,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        authority.SourceAuthority,
                        member.sourceAuthority,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        authority.SourcePropertyPath,
                        member.sourcePropertyPath,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        authority.DependencyFingerprint,
                        member.promotedAuthorityDependencyFingerprint,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        authority.SemanticHash,
                        member.promotedAuthoritySemanticHash,
                        StringComparison.Ordinal)
                    || !string.Equals(authority.AssetApplied, "true", StringComparison.Ordinal)
                    || authority.ApprovalKey.Length == 0)
                {
                    throw Stale(
                        decision.bundleId,
                        "member is neither an exact pending candidate nor an exact applied promotion: "
                        + identity);
                }
            }

            if (memberDecisions.SetEquals(new[] { "promote-candidate" }))
            {
                promotedBundles++;
                if (allApplied)
                    appliedBundles++;
            }
            else if (memberDecisions.SetEquals(new[] { "retain-current" }))
            {
                retainedBundles++;
            }
            else if (memberDecisions.SetEquals(new[] { "rework" }))
            {
                reworkBundles++;
            }
            else
            {
                throw Stale(
                    decision.bundleId,
                    "mixed member decisions are unsupported until a replacement proposal is exact");
            }
        }

        string[] missing = currentByBundle.Keys
            .Where(value => !decidedCurrentBundles.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length != 0)
        {
            throw new InvalidOperationException(
                "MARKET_REVIEW_DECISION_STALE: undecided current bundles="
                + string.Join(",", missing));
        }

        return new MarketReviewDecisionValidation(
            decisions.Length,
            promotedBundles,
            retainedBundles,
            reworkBundles,
            appliedBundles,
            pendingPromotions
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .ThenBy(value => value.Metric, StringComparer.Ordinal)
                .ToArray());
    }

    private static CanonicalBalanceMetricRecord[]
        CaptureCoupledUnappliedMarketAuthorities(
            FrozenBalanceLedger ledger,
            IReadOnlyList<CanonicalBalanceMetricRecord> candidates)
    {
        HashSet<string> pendingIdentities = candidates
            .Select(value => BuildApprovalIdentity(
                value.StableId,
                value.Metric.Substring(
                    V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix.Length)))
            .ToHashSet(StringComparer.Ordinal);
        string[] anchorIds = V27BalanceMarketDebugScenarios
            .BuildMarketReviewBundleRows(ledger.Records)
            .Where(value => pendingIdentities.Contains(BuildApprovalIdentity(
                value.StableId,
                value.AuthorityMetric)))
            .Select(value => value.AnchorItemId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return ledger.Records
            .Where(value => anchorIds.Contains(
                    value.StableId,
                    StringComparer.Ordinal)
                && ItemMarketApprovalMetrics.Contains(value.Metric)
                && value.ApprovalKey.Length != 0
                && string.Equals(value.AssetApplied, "false", StringComparison.Ordinal)
                && !string.Equals(value.Before, value.After, StringComparison.Ordinal)
                && value.SourceAuthority.EndsWith(
                    ".asset",
                    StringComparison.OrdinalIgnoreCase)
                && value.SourcePropertyPath.Length != 0)
            .OrderBy(value => value.SourceAuthority, StringComparer.Ordinal)
            .ThenBy(value => value.SourcePropertyPath, StringComparer.Ordinal)
            .ToArray();
    }

    private static void RefreshFactionBenefitBudgetQuoteDigests()
    {
        FactionAllianceBenefitBudgetSO source = AssetDatabase.LoadAssetAtPath<
            FactionAllianceBenefitBudgetSO>(FactionBenefitBudgetAssetPath)
            ?? throw new InvalidOperationException(
                "Faction alliance-benefit budget asset is missing.");
        IDungeonItemCatalogProvider items = EditorItemCatalogFactory.Create();
        FactionRouteEconomicPolicyRegistry policies = new(new IFactionRouteEconomicPolicy[]
        {
            new AllianceBenefitFactionRouteEconomicPolicy(items)
        });
        Dictionary<string, FactionDefinitionSnapshot> definitions = AssetDatabase
            .FindAssets(
                "t:DungeonFactionDefinitionSO",
                new[] { "Assets/Resources/SO/Factions/Dungeons" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<DungeonFactionDefinitionSO>)
            .Where(value => value != null)
            .Select(value => value.ToSnapshot())
            .ToDictionary(value => value.StableId, StringComparer.Ordinal);
        bool changed = false;
        foreach (FactionAllianceBenefitRouteCostRecord route in source.routeCosts)
        {
            string failure = string.Empty;
            if (!definitions.TryGetValue(
                    route.factionId,
                    out FactionDefinitionSnapshot definition)
                || !policies.TryCreateQuote(
                    definition,
                    FactionRouteKind.SupplyCaravan,
                    out FactionRouteQuoteSnapshot quote,
                    out failure))
            {
                throw new InvalidOperationException(
                    "Cannot refresh faction Supply quote digest: "
                    + route.factionId + "; " + failure);
            }
            if (string.Equals(
                    route.supplyQuoteSourceDigest,
                    quote.SourceDigest,
                    StringComparison.Ordinal))
            {
                continue;
            }
            route.supplyQuoteSourceDigest = quote.SourceDigest;
            changed = true;
        }
        PersistFactionBenefitBudget(source, changed);
    }

    private static void ApplyFactionBenefitBudgetReviewAuthority(
        FactionAllianceBenefitBudgetReviewSnapshot snapshot)
    {
        FactionAllianceBenefitBudgetSO source = AssetDatabase.LoadAssetAtPath<
            FactionAllianceBenefitBudgetSO>(FactionBenefitBudgetAssetPath)
            ?? throw new InvalidOperationException(
                "Faction alliance-benefit budget asset is missing.");
        bool changed = !string.Equals(
                source.approvedBalanceSourceDigest,
                snapshot.SourceDigest,
                StringComparison.Ordinal)
            || source.capacityMilliEwu != snapshot.CapacityMilliEwu
            || source.refillNumeratorMilliEwu != snapshot.RefillNumeratorMilliEwu
            || source.refillDenominatorDays != snapshot.RefillDenominatorDays
            || source.routeCosts.Count != snapshot.Routes.Count;
        source.approvedBalanceSourceDigest = snapshot.SourceDigest;
        source.capacityMilliEwu = snapshot.CapacityMilliEwu;
        source.refillNumeratorMilliEwu = snapshot.RefillNumeratorMilliEwu;
        source.refillDenominatorDays = snapshot.RefillDenominatorDays;
        if (source.routeCosts.Count != snapshot.Routes.Count)
        {
            source.routeCosts = snapshot.Routes
                .Select(value => new FactionAllianceBenefitRouteCostRecord
                {
                    factionId = value.FactionId,
                    cooldownDays = value.CooldownDays,
                    supplyQuoteSourceDigest = value.SupplyQuoteSourceDigest,
                    debitMilliEwu = value.DebitMilliEwu
                })
                .ToList();
        }
        else
        {
            for (int index = 0; index < snapshot.Routes.Count; index++)
            {
                FactionAllianceBenefitBudgetReviewRoute expected =
                    snapshot.Routes[index];
                FactionAllianceBenefitRouteCostRecord actual =
                    source.routeCosts[index];
                bool rowChanged = !string.Equals(
                        actual.factionId,
                        expected.FactionId,
                        StringComparison.Ordinal)
                    || actual.cooldownDays != expected.CooldownDays
                    || !string.Equals(
                        actual.supplyQuoteSourceDigest,
                        expected.SupplyQuoteSourceDigest,
                        StringComparison.Ordinal)
                    || actual.debitMilliEwu != expected.DebitMilliEwu;
                changed |= rowChanged;
                actual.factionId = expected.FactionId;
                actual.cooldownDays = expected.CooldownDays;
                actual.supplyQuoteSourceDigest = expected.SupplyQuoteSourceDigest;
                actual.debitMilliEwu = expected.DebitMilliEwu;
            }
        }
        PersistFactionBenefitBudget(source, changed);
    }

    private static void RequireFactionBenefitBudgetReviewAuthorityMatches(
        FactionAllianceBenefitBudgetReviewSnapshot expected)
    {
        if (expected == null)
            throw new ArgumentNullException(nameof(expected));
        FactionAllianceBenefitBudgetSO actual = AssetDatabase.LoadAssetAtPath<
            FactionAllianceBenefitBudgetSO>(FactionBenefitBudgetAssetPath)
            ?? throw new InvalidOperationException(
                "Faction alliance-benefit budget asset is missing.");
        if (!string.Equals(
                actual.approvedBalanceSourceDigest,
                expected.SourceDigest,
                StringComparison.Ordinal)
            || actual.capacityMilliEwu != expected.CapacityMilliEwu
            || actual.refillNumeratorMilliEwu != expected.RefillNumeratorMilliEwu
            || actual.refillDenominatorDays != expected.RefillDenominatorDays
            || actual.routeCosts.Count != expected.Routes.Count)
        {
            throw new InvalidOperationException(
                "FACTION_BENEFIT_BUDGET_REVIEW_AUTHORITY_STALE.");
        }
        for (int index = 0; index < expected.Routes.Count; index++)
        {
            FactionAllianceBenefitRouteCostRecord row = actual.routeCosts[index];
            FactionAllianceBenefitBudgetReviewRoute target = expected.Routes[index];
            if (!string.Equals(row.factionId, target.FactionId, StringComparison.Ordinal)
                || row.cooldownDays != target.CooldownDays
                || !string.Equals(
                    row.supplyQuoteSourceDigest,
                    target.SupplyQuoteSourceDigest,
                    StringComparison.Ordinal)
                || row.debitMilliEwu != target.DebitMilliEwu)
            {
                throw new InvalidOperationException(
                    "FACTION_BENEFIT_BUDGET_REVIEW_ROUTE_STALE: "
                    + target.FactionId);
            }
        }
    }

    private static void PersistFactionBenefitBudget(
        FactionAllianceBenefitBudgetSO source,
        bool changed)
    {
        if (!changed)
            return;
        IReadOnlyList<string> errors = source.ValidateDefinition();
        if (errors.Count != 0)
        {
            throw new InvalidOperationException(
                "Refreshed faction alliance-benefit budget is invalid: "
                + string.Join(" ", errors));
        }
        EditorUtility.SetDirty(source);
        AssetDatabase.SaveAssets();
        string[] path = { FactionBenefitBudgetAssetPath };
        AssetDatabase.ForceReserializeAssets(
            path,
            ForceReserializeAssetsOptions.ReserializeAssets);
        byte[] first = File.ReadAllBytes(ProjectAbsolutePath(
            FactionBenefitBudgetAssetPath));
        AssetDatabase.ForceReserializeAssets(
            path,
            ForceReserializeAssetsOptions.ReserializeAssets);
        byte[] second = File.ReadAllBytes(ProjectAbsolutePath(
            FactionBenefitBudgetAssetPath));
        if (!first.SequenceEqual(second))
        {
            throw new InvalidOperationException(
                "UNITY_YAML_UNEXPECTED_CHURN: faction benefit budget second serialization changed bytes.");
        }
    }

    private static void WriteTemporaryMarketPromotionCustody(
        FrozenBalanceLedger ledger,
        IReadOnlyList<CanonicalBalanceMetricRecord> candidates)
    {
        Dictionary<string, CanonicalBalanceMetricRecord> authorities = ledger.Records
            .Where(value => ItemMarketApprovalMetrics.Contains(value.Metric))
            .ToDictionary(
                value => BuildApprovalIdentity(value.StableId, value.Metric),
                StringComparer.Ordinal);
        HashSet<string> replacementIdentities = candidates
            .Select(value => BuildApprovalIdentity(
                value.StableId,
                value.Metric.Substring(
                    V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix.Length)))
            .ToHashSet(StringComparer.Ordinal);
        List<BalanceApprovalEntryData> entries = ValidateApprovals(LoadApprovals())
            .Values
            .Where(value => !replacementIdentities.Contains(
                BuildApprovalIdentity(value.rootStableId, value.metric)))
            .ToList();

        foreach (CanonicalBalanceMetricRecord candidate in candidates)
        {
            string authorityMetric = candidate.Metric.Substring(
                V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix.Length);
            CanonicalBalanceMetricRecord authority = authorities[
                BuildApprovalIdentity(candidate.StableId, authorityMetric)];
            string approvalKey = BuildMarketPromotionApprovalKey(
                candidate.StableId,
                authorityMetric,
                candidate.After,
                candidate.DependencyFingerprint,
                candidate.SourceDigest,
                authority.ReasonCode,
                authority.BalanceBaselineRecordId);
            entries.Add(new BalanceApprovalEntryData
            {
                approvalKey = approvalKey,
                rootStableId = candidate.StableId,
                metric = authorityMetric,
                exactBeforeValue = candidate.Before,
                exactAfterValue = candidate.After,
                dependencyFingerprint = candidate.DependencyFingerprint,
                sourceDigest = candidate.SourceDigest,
                reasonCode = authority.ReasonCode,
                balanceBaselineRecordId = authority.BalanceBaselineRecordId
            });
        }
        WriteApprovalEntries(entries);
    }

    private static string BuildMarketPromotionApprovalKey(
        string stableId,
        string metric,
        string after,
        string dependencyFingerprint,
        string sourceDigest,
        string reasonCode,
        string baselineRecordId)
    {
        string canonical = stableId + "\u001f"
            + metric + "\u001f"
            + after + "\u001f"
            + dependencyFingerprint + "\u001f"
            + sourceDigest + "\u001f"
            + reasonCode + "\u001f"
            + baselineRecordId;
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(canonical));
        StringBuilder hex = new(digest.Length * 2);
        foreach (byte value in digest)
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        return hex.ToString();
    }

    private static MarketReviewDecisionFileData LoadMarketReviewDecisions()
    {
        string path = ProjectAbsolutePath(MarketReviewDecisionPath);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "V27 market review decision authority is missing: "
                + MarketReviewDecisionPath);
        }
        return JsonUtility.FromJson<MarketReviewDecisionFileData>(
                   V27StrictJsonGuard.ReadProjectRelative(
                       MarketReviewDecisionPath))
               ?? throw new InvalidOperationException(
                   "V27 market review decision authority is invalid JSON.");
    }

    private static void RequireDecisionHeader(MarketReviewDecisionData decision)
    {
        if (string.IsNullOrWhiteSpace(decision.bundleId)
            || string.IsNullOrWhiteSpace(decision.bundleDigest)
            || string.IsNullOrWhiteSpace(decision.anchorItemId)
            || string.IsNullOrWhiteSpace(decision.decisionReason)
            || !string.Equals(
                decision.reviewedBaselineRecordId,
                MarketReviewDecisionBaseline,
                StringComparison.Ordinal)
            || decision.members == null
            || decision.members.Length == 0)
        {
            throw Stale(decision.bundleId, "decision header is incomplete");
        }
    }

    private static void RequireDecisionMember(
        MarketReviewDecisionData bundle,
        MarketReviewDecisionMemberData member)
    {
        bool decisionValid = string.Equals(
                member.decision,
                "promote-candidate",
                StringComparison.Ordinal)
            || string.Equals(member.decision, "retain-current", StringComparison.Ordinal)
            || string.Equals(member.decision, "rework", StringComparison.Ordinal);
        if (!decisionValid
            || string.IsNullOrWhiteSpace(member.stableId)
            || string.IsNullOrWhiteSpace(member.authorityMetric)
            || string.IsNullOrWhiteSpace(member.sourceAuthority)
            || string.IsNullOrWhiteSpace(member.sourcePropertyPath)
            || string.IsNullOrWhiteSpace(member.beforeExactToken)
            || string.IsNullOrWhiteSpace(member.candidateExactToken)
            || string.IsNullOrWhiteSpace(member.dependencyFingerprint)
            || string.IsNullOrWhiteSpace(member.sourceDigest)
            || string.IsNullOrWhiteSpace(member.semanticHash)
            || string.IsNullOrWhiteSpace(
                member.promotedAuthorityDependencyFingerprint)
            || string.IsNullOrWhiteSpace(member.promotedAuthoritySourceDigest)
            || string.IsNullOrWhiteSpace(member.promotedAuthoritySemanticHash))
        {
            throw Stale(bundle.bundleId, "decision member is incomplete");
        }
    }

    private static void RequireExactDecisionMember(
        MarketReviewDecisionData bundle,
        MarketReviewDecisionMemberData member,
        V27BalanceMarketDebugScenarios.MarketReviewBundleRow row)
    {
        if (!string.Equals(member.sourceAuthority, row.SourceAuthority, StringComparison.Ordinal)
            || !string.Equals(member.sourcePropertyPath, row.SourcePropertyPath, StringComparison.Ordinal)
            || !string.Equals(member.beforeExactToken, row.Before, StringComparison.Ordinal)
            || !string.Equals(member.candidateExactToken, row.Candidate, StringComparison.Ordinal)
            || !string.Equals(member.dependencyFingerprint, row.DependencyFingerprint, StringComparison.Ordinal)
            || !string.Equals(member.sourceDigest, row.SourceDigest, StringComparison.Ordinal)
            || !string.Equals(member.semanticHash, row.SemanticHash, StringComparison.Ordinal))
        {
            throw Stale(
                bundle.bundleId,
                "member identity changed: " + member.stableId + "|" + member.authorityMetric);
        }
    }

    private static InvalidOperationException Stale(string bundleId, string reason) =>
        new InvalidOperationException(
            "MARKET_REVIEW_DECISION_STALE: "
            + (bundleId ?? "<null>")
            + "; "
            + reason);

    private static void WriteMarketReviewPromotionReport(
        MarketReviewDecisionValidation validation,
        int criticalCount,
        int changedAssets,
        int changedProperties)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(
            MarketReviewPromotionReportPath,
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
                writer.WriteLine("schemaVersion=v27.market-review-application.1");
                writer.WriteLine("result=PASS");
                writer.WriteLine("decisionBundles=" + validation.DecisionBundleCount);
                writer.WriteLine("promoteBundles=" + validation.PromoteBundleCount);
                writer.WriteLine("retainBundles=" + validation.RetainBundleCount);
                writer.WriteLine("reworkBundles=" + validation.ReworkBundleCount);
                writer.WriteLine("appliedPromoteBundles=" + validation.AppliedPromoteBundleCount);
                writer.WriteLine("pendingPromotionMembers=" + validation.PendingPromotions.Count);
                writer.WriteLine("changedAssets=" + changedAssets);
                writer.WriteLine("changedProperties=" + changedProperties);
                writer.WriteLine("remainingCritical=" + criticalCount);
                writer.Flush();
            });
    }

    private static void WriteMarketReviewPromotionFailureReport(
        string phase,
        Exception exception)
    {
        Exception root = exception?.GetBaseException();
        string failureType = root?.GetType().FullName ?? "<null>";
        string failureMessage = root?.Message ?? "<null>";
        V27BalanceArtifactWriter.WriteIfDifferent(
            MarketReviewPromotionReportPath,
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
                writer.WriteLine("schemaVersion=v27.market-review-application.1");
                writer.WriteLine("result=FAIL");
                writer.WriteLine("rollback=PASS");
                writer.Write("phase=");
                writer.WriteLine(phase);
                writer.Write("failureType=");
                writer.WriteLine(failureType);
                writer.WriteLine("failureMessageBegin");
                writer.WriteLine(failureMessage);
                writer.WriteLine("failureMessageEnd");
                writer.Flush();
            });
    }

    [Serializable]
    private sealed class MarketReviewDecisionFileData
    {
        public string schemaVersion;
        public string epochId;
        public string decisionPayloadDigest;
        public string decisionEpochDigest;
        public string sourceLedgerDigest;
        public string patchScopeDigest;
        public string previousDecisionEpochDigest;
        public string previousDecisionAuthorityDigest;
        public MarketReviewDecisionData[] decisions;
    }

    [Serializable]
    private sealed class MarketReviewDecisionData
    {
        public string bundleId;
        public string bundleDigest;
        public string anchorItemId;
        public string decisionReason;
        public string reviewedBaselineRecordId;
        public MarketReviewDecisionMemberData[] members;
    }

    [Serializable]
    private sealed class MarketReviewDecisionMemberData
    {
        public string stableId;
        public string authorityMetric;
        public string sourceAuthority;
        public string sourcePropertyPath;
        public string beforeExactToken;
        public string candidateExactToken;
        public string dependencyFingerprint;
        public string sourceDigest;
        public string semanticHash;
        public string promotedAuthorityDependencyFingerprint;
        public string promotedAuthoritySourceDigest;
        public string promotedAuthoritySemanticHash;
        public string decision;
        public string replacementExactToken;
    }
}

public sealed class MarketReviewDecisionValidation
{
    internal MarketReviewDecisionValidation(
        int decisionBundleCount,
        int promoteBundleCount,
        int retainBundleCount,
        int reworkBundleCount,
        int appliedPromoteBundleCount,
        IReadOnlyList<CanonicalBalanceMetricRecord> pendingPromotions)
    {
        DecisionBundleCount = decisionBundleCount;
        PromoteBundleCount = promoteBundleCount;
        RetainBundleCount = retainBundleCount;
        ReworkBundleCount = reworkBundleCount;
        AppliedPromoteBundleCount = appliedPromoteBundleCount;
        PendingPromotions = pendingPromotions ?? Array.Empty<CanonicalBalanceMetricRecord>();
    }

    public int DecisionBundleCount { get; }
    public int PromoteBundleCount { get; }
    public int RetainBundleCount { get; }
    public int ReworkBundleCount { get; }
    public int AppliedPromoteBundleCount { get; }
    public IReadOnlyList<CanonicalBalanceMetricRecord> PendingPromotions { get; }

    public string Format(string action) =>
        "V27 market review decisions " + action
        + $": bundles={DecisionBundleCount}; promote={PromoteBundleCount}; "
        + $"retain={RetainBundleCount}; rework={ReworkBundleCount}; "
        + $"applied={AppliedPromoteBundleCount}; "
        + $"pendingMembers={PendingPromotions.Count}.";
}
#endif
