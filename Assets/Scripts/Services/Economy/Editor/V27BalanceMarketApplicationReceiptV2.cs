#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Balance;
using UnityEngine;

public static partial class V27BalanceAssetApplication
{
    private const string MarketApplicationReceiptDirectory =
        "Artifacts/QA/v27-balance-market-application-receipts";
    private const string MarketApplicationReceiptSchemaV2 =
        "v27.market-application-receipt.2";

    public static string MarketApplicationReceiptPath =>
        ResolveMarketApplicationReceiptPath(
            LoadMarketReviewDecisions().decisionEpochDigest);

    private static string ResolveMarketApplicationReceiptPath(
        string decisionEpochDigest)
    {
        if (string.IsNullOrWhiteSpace(decisionEpochDigest)
            || decisionEpochDigest.Length != 64
            || decisionEpochDigest.Any(character =>
                !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_EPOCH_DIGEST_INVALID.");
        }
        return MarketApplicationReceiptDirectory + "/"
            + decisionEpochDigest.ToLowerInvariant() + ".json";
    }

    private static MarketApplicationReceiptValidation
        WriteMarketApplicationReceiptV2(
            MarketReviewDecisionFileData decisionFile,
            FrozenBalanceLedger appliedLedger,
            IReadOnlyList<CanonicalBalanceMetricRecord> candidates,
            IReadOnlyList<CanonicalBalanceMetricRecord> coupledAuthorities,
            IReadOnlyDictionary<string, byte[]> assetRollback,
            byte[] approvalRollback)
    {
        if (decisionFile == null)
            throw new ArgumentNullException(nameof(decisionFile));
        if (appliedLedger == null)
            throw new ArgumentNullException(nameof(appliedLedger));
        if (assetRollback == null)
            throw new ArgumentNullException(nameof(assetRollback));
        if (approvalRollback == null)
            throw new ArgumentNullException(nameof(approvalRollback));

        MarketApplicationPatchScopeRowData[] patchScopeRows =
            CaptureMarketApplicationPatchScopeRows(candidates, coupledAuthorities);
        string patchScopeDigest = ComputeMarketApplicationPatchScopeDigest(
            patchScopeRows);
        if (!string.Equals(
                decisionFile.patchScopeDigest,
                patchScopeDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_PATCH_SCOPE_STALE.");
        }

        ExpectedMarketReceiptV2Row[] expected =
            CaptureExpectedMarketReceiptV2Rows(decisionFile, appliedLedger);
        if (expected.Length == 0)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_SCOPE_EMPTY.");
        }

        MarketApplicationReceiptAssetData[] assets = assetRollback
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new MarketApplicationReceiptAssetData
            {
                sourceAuthority = value.Key,
                assetBeforeSha256 = Sha256Lower(value.Value),
                assetAfterSha256 = ComputeFileSha256(value.Key)
            })
            .ToArray();
        Dictionary<string, MarketApplicationReceiptAssetData> assetByPath = assets
            .ToDictionary(value => value.sourceAuthority, StringComparer.Ordinal);
        MarketApplicationReceiptV2RowData[] rows = expected
            .Select(value => CreateMarketApplicationReceiptV2Row(
                value,
                assetByPath[value.SourceAuthority]))
            .OrderBy(value => value.sourceAuthority, StringComparer.Ordinal)
            .ThenBy(value => value.sourcePropertyPath, StringComparer.Ordinal)
            .ToArray();

        MarketApplicationReceiptV2FileData output = new()
        {
            schemaVersion = MarketApplicationReceiptSchemaV2,
            epochId = decisionFile.epochId,
            decisionPayloadDigest = decisionFile.decisionPayloadDigest,
            decisionEpochDigest = decisionFile.decisionEpochDigest,
            sourceLedgerDigest = decisionFile.sourceLedgerDigest,
            patchScopeDigest = decisionFile.patchScopeDigest,
            previousDecisionEpochDigest = decisionFile.previousDecisionEpochDigest,
            previousDecisionAuthorityDigest =
                decisionFile.previousDecisionAuthorityDigest,
            decisionAuthoritySha256Diagnostic =
                ComputeFileSha256(MarketReviewDecisionPath),
            approvalBeforeSha256 = Sha256Lower(approvalRollback),
            approvalAfterSha256 = ComputeFileSha256(V27BalanceAudit.ApprovalPath),
            assetSetBeforeDigest = ComputeReceiptAssetSetDigest(
                assets,
                before: true),
            assetSetAfterDigest = ComputeReceiptAssetSetDigest(
                assets,
                before: false),
            receiptScopeDigest = ComputeMarketReceiptV2ScopeDigest(expected),
            patchScopeRows = patchScopeRows,
            assets = assets,
            receipts = rows
        };
        output.receiptDigest = ComputeMarketReceiptV2Digest(output);
        byte[] bytes = SerializeMarketApplicationReceiptV2(output);
        string path = ResolveMarketApplicationReceiptPath(
            decisionFile.decisionEpochDigest);
        string absolute = ProjectAbsolutePath(path);
        if (File.Exists(absolute)
            && !File.ReadAllBytes(absolute).SequenceEqual(bytes))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_IMMUTABLE: " + path);
        }
        WriteBytesTwiceAndRequireSecondNoOp(path, bytes);
        return ValidateMarketApplicationReceipts(appliedLedger);
    }

    internal static MarketApplicationReceiptValidation
        ValidateMarketApplicationReceipts(FrozenBalanceLedger ledger)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        MarketReviewDecisionValidation decisionValidation =
            ValidateMarketReviewDecisions(ledger);
        if (decisionValidation.PendingPromotions.Count != 0
            || decisionValidation.AppliedPromoteBundleCount
                != decisionValidation.PromoteBundleCount)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_DECISION_NOT_APPLIED: "
                + decisionValidation.Format("receipt"));
        }
        HashSet<string> activeApprovalKeys =
            CaptureMatchingApprovalKeysForRefresh(ledger)
            .ToHashSet(StringComparer.Ordinal);

        MarketReviewDecisionFileData decisionFile = LoadMarketReviewDecisions();
        string path = ResolveMarketApplicationReceiptPath(
            decisionFile.decisionEpochDigest);
        string absolute = ProjectAbsolutePath(path);
        if (!File.Exists(absolute))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_MISSING: " + path);
        }
        MarketApplicationReceiptV2FileData file = JsonUtility.FromJson<
            MarketApplicationReceiptV2FileData>(
                V27StrictJsonGuard.ReadProjectRelative(path))
            ?? throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_INVALID_JSON.");
        RequireMarketReceiptV2Header(file, decisionFile);

        string computedPatchScopeDigest = ComputeMarketApplicationPatchScopeDigest(
            file.patchScopeRows);
        if (!string.Equals(
                computedPatchScopeDigest,
                decisionFile.patchScopeDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_PATCH_SCOPE_STALE.");
        }

        ExpectedMarketReceiptV2Row[] expected =
            CaptureExpectedMarketReceiptV2Rows(decisionFile, ledger);
        string expectedScopeDigest = ComputeMarketReceiptV2StoredScopeDigest(
            file.receipts);
        if (!string.Equals(
                file.receiptScopeDigest,
                expectedScopeDigest,
                StringComparison.Ordinal)
            || file.receipts.Length != expected.Length)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_SCOPE_STALE: expected="
                + expected.Length.ToString(CultureInfo.InvariantCulture)
                + "; actual="
                + file.receipts.Length.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        Dictionary<string, MarketApplicationReceiptAssetData> assetByPath =
            file.assets.ToDictionary(
                value => value.sourceAuthority,
                StringComparer.Ordinal);
        if (assetByPath.Count != file.assets.Length)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_DUPLICATE_ASSET.");
        }
        foreach (MarketApplicationReceiptAssetData asset in file.assets)
        {
            RequireCanonicalSha256(asset.assetBeforeSha256, "receipt asset before hash");
            RequireCanonicalSha256(asset.assetAfterSha256, "receipt asset after hash");
            if (!string.Equals(
                    asset.assetAfterSha256,
                    ComputeFileSha256(asset.sourceAuthority),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_ASSET_AFTER_STALE: "
                    + asset.sourceAuthority);
            }
        }
        if (!string.Equals(
                file.assetSetBeforeDigest,
                ComputeReceiptAssetSetDigest(file.assets, before: true),
                StringComparison.Ordinal)
            || !string.Equals(
                file.assetSetAfterDigest,
                ComputeReceiptAssetSetDigest(file.assets, before: false),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_ASSET_SET_STALE.");
        }
        RequireCanonicalSha256(
            file.approvalBeforeSha256,
            "receipt approval before hash");
        RequireCanonicalSha256(
            file.approvalAfterSha256,
            "receipt approval after hash");

        Dictionary<string, MarketApplicationReceiptV2RowData> receiptByIdentity =
            file.receipts.ToDictionary(
                value => BuildApprovalIdentity(value.stableId, value.authorityMetric),
                StringComparer.Ordinal);
        if (receiptByIdentity.Count != file.receipts.Length)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_DUPLICATE_MEMBER.");
        }
        List<MarketApplicationReceiptValidatedRow> validated = new();
        foreach (ExpectedMarketReceiptV2Row row in expected)
        {
            string identity = BuildApprovalIdentity(row.StableId, row.AuthorityMetric);
            if (!receiptByIdentity.TryGetValue(
                    identity,
                    out MarketApplicationReceiptV2RowData receipt))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_MEMBER_MISSING: " + identity);
            }
            RequireMarketReceiptV2RowMatches(receipt, row);
            if (!activeApprovalKeys.Contains(row.AppliedApprovalKey))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_APPROVAL_MEMBER_STALE: "
                    + identity);
            }
            if (!assetByPath.TryGetValue(
                    receipt.sourceAuthority,
                    out MarketApplicationReceiptAssetData asset)
                || !string.Equals(
                    receipt.assetBeforeSha256,
                    asset.assetBeforeSha256,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.assetAfterSha256,
                    asset.assetAfterSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_MEMBER_ASSET_STALE: " + identity);
            }
            if (!string.Equals(
                    receipt.receiptRowDigest,
                    ComputeMarketReceiptV2RowDigest(receipt),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_ROW_DIGEST_STALE: " + identity);
            }
            validated.Add(new MarketApplicationReceiptValidatedRow(
                receipt.sourceAuthority,
                receipt.sourcePropertyPath,
                receipt.exactBeforeValue,
                receipt.exactAfterValue,
                receipt.assetBeforeSha256,
                receipt.assetAfterSha256));
        }

        string receiptDigest = ComputeMarketReceiptV2Digest(file);
        if (!string.Equals(
                file.receiptDigest,
                receiptDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_FILE_DIGEST_STALE.");
        }
        return new MarketApplicationReceiptValidation(
            expectedScopeDigest,
            receiptDigest,
            validated.OrderBy(value => value.SourceAuthority, StringComparer.Ordinal)
                .ThenBy(value => value.SourcePropertyPath, StringComparer.Ordinal)
                .ToArray());
    }

    private static ExpectedMarketReceiptV2Row[] CaptureExpectedMarketReceiptV2Rows(
        MarketReviewDecisionFileData decisionFile,
        FrozenBalanceLedger ledger)
    {
        Dictionary<string, CanonicalBalanceMetricRecord> authorityByIdentity = ledger
            .Records
            .Where(value => !value.Metric.StartsWith(
                V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix,
                StringComparison.Ordinal))
            .GroupBy(
                value => BuildApprovalIdentity(value.StableId, value.Metric),
                StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        HashSet<string> propertyIdentities = new(StringComparer.Ordinal);
        List<ExpectedMarketReceiptV2Row> result = new();
        foreach (MarketReviewDecisionData decision in decisionFile.decisions
                     .OrderBy(value => value.bundleId, StringComparer.Ordinal))
        {
            foreach (MarketReviewDecisionMemberData member in decision.members
                         .Where(value => string.Equals(
                             value.decision,
                             "promote-candidate",
                             StringComparison.Ordinal))
                         .OrderBy(value => value.stableId, StringComparer.Ordinal)
                         .ThenBy(value => value.authorityMetric, StringComparer.Ordinal))
            {
                string identity = BuildApprovalIdentity(
                    member.stableId,
                    member.authorityMetric);
                if (!authorityByIdentity.TryGetValue(
                        identity,
                        out CanonicalBalanceMetricRecord authority)
                    || !string.Equals(authority.Before, member.beforeExactToken, StringComparison.Ordinal)
                    || !string.Equals(authority.After, member.candidateExactToken, StringComparison.Ordinal)
                    || !string.Equals(authority.SourceAuthority, member.sourceAuthority, StringComparison.Ordinal)
                    || !string.Equals(authority.SourcePropertyPath, member.sourcePropertyPath, StringComparison.Ordinal)
                    || !string.Equals(authority.DependencyFingerprint, member.promotedAuthorityDependencyFingerprint, StringComparison.Ordinal)
                    || !string.Equals(authority.SemanticHash, member.promotedAuthoritySemanticHash, StringComparison.Ordinal)
                    || !string.Equals(authority.AssetApplied, "true", StringComparison.Ordinal)
                    || authority.ApprovalKey.Length == 0)
                {
                    throw new InvalidOperationException(
                        "MARKET_APPLICATION_RECEIPT_AUTHORITY_STALE: " + identity);
                }
                string propertyIdentity = ReceiptIdentity(
                    member.sourceAuthority,
                    member.sourcePropertyPath);
                if (!propertyIdentities.Add(propertyIdentity))
                {
                    throw new InvalidOperationException(
                        "MARKET_APPLICATION_RECEIPT_DUPLICATE_PROPERTY: "
                        + propertyIdentity);
                }
                result.Add(new ExpectedMarketReceiptV2Row(
                    decision,
                    member,
                    authority.ApprovalKey,
                    ComputeMarketReceiptDecisionMemberDigest(decision, member)));
            }
        }
        return result.ToArray();
    }

    private static MarketApplicationPatchScopeRowData[]
        CaptureMarketApplicationPatchScopeRows(
            IEnumerable<CanonicalBalanceMetricRecord> candidates,
            IEnumerable<CanonicalBalanceMetricRecord> coupledAuthorities) =>
        (candidates ?? Array.Empty<CanonicalBalanceMetricRecord>())
            .Select(value => CreateMarketApplicationPatchScopeRow(
                "review-candidate",
                value))
            .Concat((coupledAuthorities ?? Array.Empty<CanonicalBalanceMetricRecord>())
                .Select(value => CreateMarketApplicationPatchScopeRow(
                    "coupled-authority",
                    value)))
            .OrderBy(value => value.role, StringComparer.Ordinal)
            .ThenBy(value => value.stableId, StringComparer.Ordinal)
            .ThenBy(value => value.metric, StringComparer.Ordinal)
            .ToArray();

    private static MarketApplicationPatchScopeRowData
        CreateMarketApplicationPatchScopeRow(
            string role,
            CanonicalBalanceMetricRecord record) => new()
        {
            role = role,
            stableId = record.StableId,
            metric = record.Metric,
            sourceAuthority = record.SourceAuthority,
            sourcePropertyPath = record.SourcePropertyPath,
            before = record.Before,
            after = record.After,
            dependencyFingerprint = record.DependencyFingerprint,
            sourceDigest = record.SourceDigest,
            semanticHash = record.SemanticHash
        };

    private static string ComputeMarketApplicationPatchScopeDigest(
        IEnumerable<MarketApplicationPatchScopeRowData> rows)
    {
        StringBuilder canonical = new();
        foreach (MarketApplicationPatchScopeRowData row in (rows
                     ?? Array.Empty<MarketApplicationPatchScopeRowData>())
                 .OrderBy(value => value.role, StringComparer.Ordinal)
                 .ThenBy(value => value.stableId, StringComparer.Ordinal)
                 .ThenBy(value => value.metric, StringComparer.Ordinal))
        {
            AppendCanonicalField(canonical, row.role);
            AppendCanonicalField(canonical, row.stableId);
            AppendCanonicalField(canonical, row.metric);
            AppendCanonicalField(canonical, row.sourceAuthority);
            AppendCanonicalField(canonical, row.sourcePropertyPath);
            AppendCanonicalField(canonical, row.before);
            AppendCanonicalField(canonical, row.after);
            AppendCanonicalField(canonical, row.dependencyFingerprint);
            AppendCanonicalField(canonical, row.sourceDigest);
            AppendCanonicalField(canonical, row.semanticHash);
        }
        return HashBytes(new UTF8Encoding(false, true).GetBytes(canonical.ToString()));
    }

    private static MarketApplicationReceiptV2RowData
        CreateMarketApplicationReceiptV2Row(
            ExpectedMarketReceiptV2Row source,
            MarketApplicationReceiptAssetData asset)
    {
        MarketApplicationReceiptV2RowData row = new()
        {
            decisionBundleId = source.DecisionBundleId,
            decisionBundleDigest = source.DecisionBundleDigest,
            stableId = source.StableId,
            authorityMetric = source.AuthorityMetric,
            sourceAuthority = source.SourceAuthority,
            sourcePropertyPath = source.SourcePropertyPath,
            exactBeforeValue = source.ExactBeforeValue,
            exactAfterValue = source.ExactAfterValue,
            dependencyFingerprint = source.DependencyFingerprint,
            sourceDigest = source.SourceDigest,
            semanticHash = source.SemanticHash,
            promotedAuthorityDependencyFingerprint =
                source.PromotedAuthorityDependencyFingerprint,
            promotedAuthoritySourceDigest = source.PromotedAuthoritySourceDigest,
            promotedAuthoritySemanticHash = source.PromotedAuthoritySemanticHash,
            decision = source.Decision,
            replacementExactToken = source.ReplacementExactToken,
            decisionMemberDigest = source.DecisionMemberDigest,
            appliedApprovalKey = source.AppliedApprovalKey,
            assetBeforeSha256 = asset.assetBeforeSha256,
            assetAfterSha256 = asset.assetAfterSha256
        };
        row.receiptRowDigest = ComputeMarketReceiptV2RowDigest(row);
        return row;
    }

    private static void RequireMarketReceiptV2Header(
        MarketApplicationReceiptV2FileData actual,
        MarketReviewDecisionFileData decision)
    {
        if (!string.Equals(actual.schemaVersion, MarketApplicationReceiptSchemaV2, StringComparison.Ordinal)
            || actual.patchScopeRows == null
            || actual.assets == null
            || actual.receipts == null
            || !string.Equals(actual.epochId, decision.epochId, StringComparison.Ordinal)
            || !string.Equals(actual.decisionPayloadDigest, decision.decisionPayloadDigest, StringComparison.Ordinal)
            || !string.Equals(actual.decisionEpochDigest, decision.decisionEpochDigest, StringComparison.Ordinal)
            || !string.Equals(actual.sourceLedgerDigest, decision.sourceLedgerDigest, StringComparison.Ordinal)
            || !string.Equals(actual.patchScopeDigest, decision.patchScopeDigest, StringComparison.Ordinal)
            || !string.Equals(actual.previousDecisionEpochDigest, decision.previousDecisionEpochDigest, StringComparison.Ordinal)
            || !string.Equals(actual.previousDecisionAuthorityDigest, decision.previousDecisionAuthorityDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_DECISION_STALE.");
        }
        RequireCanonicalSha256(
            actual.decisionAuthoritySha256Diagnostic,
            "receipt decision diagnostic hash");
    }

    private static void RequireMarketReceiptV2RowMatches(
        MarketApplicationReceiptV2RowData actual,
        ExpectedMarketReceiptV2Row expected)
    {
        if (!string.Equals(actual.decisionBundleId, expected.DecisionBundleId, StringComparison.Ordinal)
            || !string.Equals(actual.decisionBundleDigest, expected.DecisionBundleDigest, StringComparison.Ordinal)
            || !string.Equals(actual.stableId, expected.StableId, StringComparison.Ordinal)
            || !string.Equals(actual.authorityMetric, expected.AuthorityMetric, StringComparison.Ordinal)
            || !string.Equals(actual.sourceAuthority, expected.SourceAuthority, StringComparison.Ordinal)
            || !string.Equals(actual.sourcePropertyPath, expected.SourcePropertyPath, StringComparison.Ordinal)
            || !string.Equals(actual.exactBeforeValue, expected.ExactBeforeValue, StringComparison.Ordinal)
            || !string.Equals(actual.exactAfterValue, expected.ExactAfterValue, StringComparison.Ordinal)
            || !string.Equals(actual.dependencyFingerprint, expected.DependencyFingerprint, StringComparison.Ordinal)
            || !string.Equals(actual.sourceDigest, expected.SourceDigest, StringComparison.Ordinal)
            || !string.Equals(actual.semanticHash, expected.SemanticHash, StringComparison.Ordinal)
            || !string.Equals(actual.promotedAuthorityDependencyFingerprint, expected.PromotedAuthorityDependencyFingerprint, StringComparison.Ordinal)
            || !string.Equals(actual.promotedAuthoritySourceDigest, expected.PromotedAuthoritySourceDigest, StringComparison.Ordinal)
            || !string.Equals(actual.promotedAuthoritySemanticHash, expected.PromotedAuthoritySemanticHash, StringComparison.Ordinal)
            || !string.Equals(actual.decision, expected.Decision, StringComparison.Ordinal)
            || !string.Equals(actual.replacementExactToken, expected.ReplacementExactToken, StringComparison.Ordinal)
            || !string.Equals(actual.decisionMemberDigest, expected.DecisionMemberDigest, StringComparison.Ordinal)
            )
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_PROPERTY_STALE: "
                + BuildApprovalIdentity(expected.StableId, expected.AuthorityMetric));
        }
    }

    private static string ComputeMarketReceiptDecisionMemberDigest(
        MarketReviewDecisionData decision,
        MarketReviewDecisionMemberData member)
    {
        StringBuilder canonical = new();
        AppendCanonicalField(canonical, decision.bundleId);
        AppendCanonicalField(canonical, decision.bundleDigest);
        AppendCanonicalField(canonical, decision.anchorItemId);
        AppendCanonicalField(canonical, decision.decisionReason);
        AppendCanonicalField(canonical, decision.reviewedBaselineRecordId);
        AppendCanonicalField(canonical, member.stableId);
        AppendCanonicalField(canonical, member.authorityMetric);
        AppendCanonicalField(canonical, member.sourceAuthority);
        AppendCanonicalField(canonical, member.sourcePropertyPath);
        AppendCanonicalField(canonical, member.beforeExactToken);
        AppendCanonicalField(canonical, member.candidateExactToken);
        AppendCanonicalField(canonical, member.dependencyFingerprint);
        AppendCanonicalField(canonical, member.sourceDigest);
        AppendCanonicalField(canonical, member.semanticHash);
        AppendCanonicalField(canonical, member.promotedAuthorityDependencyFingerprint);
        AppendCanonicalField(canonical, member.promotedAuthoritySourceDigest);
        AppendCanonicalField(canonical, member.promotedAuthoritySemanticHash);
        AppendCanonicalField(canonical, member.decision);
        AppendCanonicalField(canonical, member.replacementExactToken);
        return HashBytes(new UTF8Encoding(false, true).GetBytes(canonical.ToString()));
    }

    private static string ComputeMarketReceiptV2ScopeDigest(
        IEnumerable<ExpectedMarketReceiptV2Row> rows)
    {
        StringBuilder canonical = new();
        foreach (ExpectedMarketReceiptV2Row row in rows
                     .OrderBy(value => value.SourceAuthority, StringComparer.Ordinal)
                     .ThenBy(value => value.SourcePropertyPath, StringComparer.Ordinal))
        {
            AppendCanonicalField(canonical, row.Canonical);
        }
        return HashBytes(new UTF8Encoding(false, true).GetBytes(canonical.ToString()));
    }

    private static string ComputeMarketReceiptV2StoredScopeDigest(
        IEnumerable<MarketApplicationReceiptV2RowData> rows)
    {
        StringBuilder canonical = new();
        foreach (MarketApplicationReceiptV2RowData row in (rows
                     ?? Array.Empty<MarketApplicationReceiptV2RowData>())
                 .OrderBy(value => value.sourceAuthority, StringComparer.Ordinal)
                 .ThenBy(value => value.sourcePropertyPath, StringComparer.Ordinal))
        {
            string stored = row.decisionBundleId + "\u001f"
                + row.decisionBundleDigest + "\u001f" + row.stableId + "\u001f"
                + row.authorityMetric + "\u001f" + row.sourceAuthority + "\u001f"
                + row.sourcePropertyPath + "\u001f" + row.exactBeforeValue + "\u001f"
                + row.exactAfterValue + "\u001f" + row.dependencyFingerprint + "\u001f"
                + row.sourceDigest + "\u001f" + row.semanticHash + "\u001f"
                + row.promotedAuthorityDependencyFingerprint + "\u001f"
                + row.promotedAuthoritySourceDigest + "\u001f"
                + row.promotedAuthoritySemanticHash + "\u001f" + row.decision + "\u001f"
                + row.replacementExactToken + "\u001f" + row.appliedApprovalKey + "\u001f"
                + row.decisionMemberDigest;
            AppendCanonicalField(canonical, stored);
        }
        return HashBytes(new UTF8Encoding(false, true).GetBytes(canonical.ToString()));
    }

    private static string ComputeReceiptAssetSetDigest(
        IEnumerable<MarketApplicationReceiptAssetData> assets,
        bool before)
    {
        StringBuilder canonical = new();
        foreach (MarketApplicationReceiptAssetData asset in assets
                     .OrderBy(value => value.sourceAuthority, StringComparer.Ordinal))
        {
            AppendCanonicalField(canonical, asset.sourceAuthority);
            AppendCanonicalField(
                canonical,
                before ? asset.assetBeforeSha256 : asset.assetAfterSha256);
        }
        return HashBytes(new UTF8Encoding(false, true).GetBytes(canonical.ToString()));
    }

    private static string ComputeMarketReceiptV2RowDigest(
        MarketApplicationReceiptV2RowData row)
    {
        StringBuilder canonical = new();
        AppendCanonicalField(canonical, row.decisionBundleId);
        AppendCanonicalField(canonical, row.decisionBundleDigest);
        AppendCanonicalField(canonical, row.stableId);
        AppendCanonicalField(canonical, row.authorityMetric);
        AppendCanonicalField(canonical, row.sourceAuthority);
        AppendCanonicalField(canonical, row.sourcePropertyPath);
        AppendCanonicalField(canonical, row.exactBeforeValue);
        AppendCanonicalField(canonical, row.exactAfterValue);
        AppendCanonicalField(canonical, row.dependencyFingerprint);
        AppendCanonicalField(canonical, row.sourceDigest);
        AppendCanonicalField(canonical, row.semanticHash);
        AppendCanonicalField(canonical, row.promotedAuthorityDependencyFingerprint);
        AppendCanonicalField(canonical, row.promotedAuthoritySourceDigest);
        AppendCanonicalField(canonical, row.promotedAuthoritySemanticHash);
        AppendCanonicalField(canonical, row.decision);
        AppendCanonicalField(canonical, row.replacementExactToken);
        AppendCanonicalField(canonical, row.decisionMemberDigest);
        AppendCanonicalField(canonical, row.appliedApprovalKey);
        AppendCanonicalField(canonical, row.assetBeforeSha256);
        AppendCanonicalField(canonical, row.assetAfterSha256);
        return HashBytes(new UTF8Encoding(false, true).GetBytes(canonical.ToString()));
    }

    private static string ComputeMarketReceiptV2Digest(
        MarketApplicationReceiptV2FileData file)
    {
        StringBuilder canonical = new();
        AppendCanonicalField(canonical, file.schemaVersion);
        AppendCanonicalField(canonical, file.epochId);
        AppendCanonicalField(canonical, file.decisionPayloadDigest);
        AppendCanonicalField(canonical, file.decisionEpochDigest);
        AppendCanonicalField(canonical, file.sourceLedgerDigest);
        AppendCanonicalField(canonical, file.patchScopeDigest);
        AppendCanonicalField(canonical, file.previousDecisionEpochDigest);
        AppendCanonicalField(canonical, file.previousDecisionAuthorityDigest);
        AppendCanonicalField(canonical, file.decisionAuthoritySha256Diagnostic);
        AppendCanonicalField(canonical, file.approvalBeforeSha256);
        AppendCanonicalField(canonical, file.approvalAfterSha256);
        AppendCanonicalField(canonical, file.assetSetBeforeDigest);
        AppendCanonicalField(canonical, file.assetSetAfterDigest);
        AppendCanonicalField(canonical, file.receiptScopeDigest);
        AppendCanonicalField(
            canonical,
            ComputeMarketApplicationPatchScopeDigest(file.patchScopeRows));
        foreach (MarketApplicationReceiptAssetData asset in file.assets
                     .OrderBy(value => value.sourceAuthority, StringComparer.Ordinal))
        {
            AppendCanonicalField(canonical, asset.sourceAuthority);
            AppendCanonicalField(canonical, asset.assetBeforeSha256);
            AppendCanonicalField(canonical, asset.assetAfterSha256);
        }
        foreach (MarketApplicationReceiptV2RowData row in file.receipts
                     .OrderBy(value => value.sourceAuthority, StringComparer.Ordinal)
                     .ThenBy(value => value.sourcePropertyPath, StringComparer.Ordinal))
        {
            AppendCanonicalField(canonical, row.receiptRowDigest);
        }
        return HashBytes(new UTF8Encoding(false, true).GetBytes(canonical.ToString()));
    }

    private static byte[] SerializeMarketApplicationReceiptV2(
        MarketApplicationReceiptV2FileData file) =>
        new UTF8Encoding(false, true).GetBytes(
            JsonUtility.ToJson(file, prettyPrint: true) + "\n");

    private static string Sha256Lower(byte[] bytes) =>
        HashBytes(bytes).ToLowerInvariant();

    [Serializable]
    private sealed class MarketApplicationReceiptV2FileData
    {
        public string schemaVersion;
        public string epochId;
        public string decisionPayloadDigest;
        public string decisionEpochDigest;
        public string sourceLedgerDigest;
        public string patchScopeDigest;
        public string previousDecisionEpochDigest;
        public string previousDecisionAuthorityDigest;
        public string decisionAuthoritySha256Diagnostic;
        public string approvalBeforeSha256;
        public string approvalAfterSha256;
        public string assetSetBeforeDigest;
        public string assetSetAfterDigest;
        public string receiptScopeDigest;
        public string receiptDigest;
        public MarketApplicationPatchScopeRowData[] patchScopeRows;
        public MarketApplicationReceiptAssetData[] assets;
        public MarketApplicationReceiptV2RowData[] receipts;
    }

    [Serializable]
    private sealed class MarketApplicationPatchScopeRowData
    {
        public string role;
        public string stableId;
        public string metric;
        public string sourceAuthority;
        public string sourcePropertyPath;
        public string before;
        public string after;
        public string dependencyFingerprint;
        public string sourceDigest;
        public string semanticHash;
    }

    [Serializable]
    private sealed class MarketApplicationReceiptAssetData
    {
        public string sourceAuthority;
        public string assetBeforeSha256;
        public string assetAfterSha256;
    }

    [Serializable]
    private sealed class MarketApplicationReceiptV2RowData
    {
        public string decisionBundleId;
        public string decisionBundleDigest;
        public string stableId;
        public string authorityMetric;
        public string sourceAuthority;
        public string sourcePropertyPath;
        public string exactBeforeValue;
        public string exactAfterValue;
        public string dependencyFingerprint;
        public string sourceDigest;
        public string semanticHash;
        public string promotedAuthorityDependencyFingerprint;
        public string promotedAuthoritySourceDigest;
        public string promotedAuthoritySemanticHash;
        public string decision;
        public string replacementExactToken;
        public string decisionMemberDigest;
        public string appliedApprovalKey;
        public string assetBeforeSha256;
        public string assetAfterSha256;
        public string receiptRowDigest;
    }

    private sealed class ExpectedMarketReceiptV2Row
    {
        public ExpectedMarketReceiptV2Row(
            MarketReviewDecisionData decision,
            MarketReviewDecisionMemberData member,
            string appliedApprovalKey,
            string decisionMemberDigest)
        {
            DecisionBundleId = decision.bundleId;
            DecisionBundleDigest = decision.bundleDigest;
            StableId = member.stableId;
            AuthorityMetric = member.authorityMetric;
            SourceAuthority = member.sourceAuthority;
            SourcePropertyPath = member.sourcePropertyPath;
            ExactBeforeValue = member.beforeExactToken;
            ExactAfterValue = member.candidateExactToken;
            DependencyFingerprint = member.dependencyFingerprint;
            SourceDigest = member.sourceDigest;
            SemanticHash = member.semanticHash;
            PromotedAuthorityDependencyFingerprint =
                member.promotedAuthorityDependencyFingerprint;
            PromotedAuthoritySourceDigest = member.promotedAuthoritySourceDigest;
            PromotedAuthoritySemanticHash = member.promotedAuthoritySemanticHash;
            Decision = member.decision;
            ReplacementExactToken = member.replacementExactToken;
            AppliedApprovalKey = appliedApprovalKey;
            DecisionMemberDigest = decisionMemberDigest;
        }

        public string DecisionBundleId { get; }
        public string DecisionBundleDigest { get; }
        public string StableId { get; }
        public string AuthorityMetric { get; }
        public string SourceAuthority { get; }
        public string SourcePropertyPath { get; }
        public string ExactBeforeValue { get; }
        public string ExactAfterValue { get; }
        public string DependencyFingerprint { get; }
        public string SourceDigest { get; }
        public string SemanticHash { get; }
        public string PromotedAuthorityDependencyFingerprint { get; }
        public string PromotedAuthoritySourceDigest { get; }
        public string PromotedAuthoritySemanticHash { get; }
        public string Decision { get; }
        public string ReplacementExactToken { get; }
        public string AppliedApprovalKey { get; }
        public string DecisionMemberDigest { get; }
        public string Canonical => DecisionBundleId + "\u001f"
            + DecisionBundleDigest + "\u001f" + StableId + "\u001f"
            + AuthorityMetric + "\u001f" + SourceAuthority + "\u001f"
            + SourcePropertyPath + "\u001f" + ExactBeforeValue + "\u001f"
            + ExactAfterValue + "\u001f" + DependencyFingerprint + "\u001f"
            + SourceDigest + "\u001f" + SemanticHash + "\u001f"
            + PromotedAuthorityDependencyFingerprint + "\u001f"
            + PromotedAuthoritySourceDigest + "\u001f"
            + PromotedAuthoritySemanticHash + "\u001f" + Decision + "\u001f"
            + ReplacementExactToken + "\u001f" + AppliedApprovalKey + "\u001f"
            + DecisionMemberDigest;
    }
}
#endif
