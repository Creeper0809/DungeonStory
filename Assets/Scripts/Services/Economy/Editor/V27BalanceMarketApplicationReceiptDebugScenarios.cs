#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public const string LegacyMarketApplicationReceiptPath =
        "Artifacts/QA/v27-balance-market-application-receipts.json";
    public const string MarketApplicationFocusedReportPath =
        "Artifacts/QA/v27-balance-market-application-focused.txt";
    public const string MarketApplicationParentReportPath =
        "Artifacts/QA/v27-balance-market-parent-current-source.txt";
    public const string MarketWriterProvenancePath =
        "Artifacts/QA/v27-balance-market-writer-provenance.txt";

    private const string LegacyMarketApplicationReceiptSchema =
        "v27.market-application-receipts.1";
    private const string MarketApplicationParentSchema =
        "v27.market-parent-current-source.1";

    [MenuItem("DungeonStory/V27/Bootstrap Immutable Market Application Receipts From Git Parent")]
    public static void BootstrapMarketApplicationReceiptsFromMenu()
    {
        V27BalanceAuditOutput audit = RequireCleanMarketAudit();
        ExpectedMarketReceiptRow[] expected = CaptureExpectedMarketReceiptRows(
            audit.Ledger);
        if (expected.Length == 0)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_SCOPE_EMPTY: no applied review property exists.");
        }

        string decisionDigest = ComputeFileSha256(MarketReviewDecisionPath);
        string baselineRevision = CaptureGitHeadRevision();
        Dictionary<string, string> beforeByAsset = expected
            .Select(value => value.SourceAuthority)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                value => value,
                value => ComputeGitBlobSha256(baselineRevision, value),
                StringComparer.Ordinal);
        Dictionary<string, string> afterByAsset = expected
            .Select(value => value.SourceAuthority)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                value => value,
                ComputeFileSha256,
                StringComparer.Ordinal);

        foreach (string assetPath in beforeByAsset.Keys)
        {
            if (string.Equals(
                    beforeByAsset[assetPath],
                    afterByAsset[assetPath],
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_BASELINE_NOT_DISTINCT: " + assetPath);
            }
        }

        MarketApplicationReceiptFileData output = new()
        {
            schemaVersion = LegacyMarketApplicationReceiptSchema,
            baselineRevision = baselineRevision,
            decisionAuthorityDigest = decisionDigest,
            receiptScopeDigest = ComputeExpectedReceiptScopeDigest(expected),
            receipts = expected.Select(value => CreateReceiptRow(
                    value,
                    beforeByAsset[value.SourceAuthority],
                    afterByAsset[value.SourceAuthority]))
                .OrderBy(value => value.sourceAuthority, StringComparer.Ordinal)
                .ThenBy(value => value.sourcePropertyPath, StringComparer.Ordinal)
                .ToArray()
        };
        output.receiptDigest = ComputeReceiptDigest(output.receipts);
        byte[] bytes = SerializeReceipt(output);
        string absolute = ProjectAbsolutePath(LegacyMarketApplicationReceiptPath);
        if (File.Exists(absolute))
        {
            byte[] current = File.ReadAllBytes(absolute);
            if (!current.SequenceEqual(bytes))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_IMMUTABLE: existing receipt differs; "
                    + "review and remove it explicitly before creating a new baseline.");
            }
        }
        WriteBytesTwiceAndRequireSecondNoOp(LegacyMarketApplicationReceiptPath, bytes);
        MarketApplicationReceiptValidation validation = ValidateLegacyMarketApplicationReceipts(
            audit.Ledger);
        Debug.Log(validation.Format("bootstrapped"));
    }

    [MenuItem("DungeonStory/V27/Verify Market Receipt And Writer Provenance (No Asset Writes)")]
    public static void VerifyMarketReceiptAndWriterProvenanceFromMenu()
    {
        string report = BuildMarketApplicationFocusedReport();
        WriteTextTwiceAndRequireSecondNoOp(
            MarketApplicationFocusedReportPath,
            report);
        Debug.Log(report);
    }

    [MenuItem("DungeonStory/V27/Verify Batch E Current-Source Parent")]
    public static void VerifyBatchECurrentSourceParentFromMenu()
    {
        string focused = BuildMarketApplicationFocusedReport();
        WriteTextTwiceAndRequireSecondNoOp(
            MarketApplicationFocusedReportPath,
            focused);

        V27BalanceBuilderNoClobberDebugScenarios.RequireFreshEvidence();
        string noClobber = File.ReadAllText(
            ProjectAbsolutePath(V27BalanceBuilderNoClobberDebugScenarios.ReportPath),
            new UTF8Encoding(false, true));
        string wholeCoverage = V27BalanceWholeGameCoverageDebugScenarios.RunAll();
        if (!wholeCoverage.StartsWith("RESULT=PASS;", StringComparison.Ordinal))
            throw new InvalidOperationException("Batch E whole-game coverage did not pass.");

        string currentSourceDigest = V27CurrentSourceEvidenceDigest
            .ComputeAllScriptsDigest();
        string sceneDigest = V27CurrentSourceEvidenceDigest
            .ComputeGameplaySceneDigest();
        string parent = "RESULT=PASS\n"
            + "schemaVersion=" + MarketApplicationParentSchema + "\n"
            + "currentSourceDigest=" + currentSourceDigest + "\n"
            + "gameplaySceneSha256=" + sceneDigest + "\n"
            + "focusedEvidenceSha256=" + HashText(focused) + "\n"
            + "decisionAuthoritySha256="
            + ComputeFileSha256(MarketReviewDecisionPath) + "\n"
            + "receiptAuthoritySha256="
            + ComputeFileSha256(MarketApplicationReceiptPath) + "\n"
            + "writerProvenanceSha256="
            + ComputeFileSha256(MarketWriterProvenancePath) + "\n"
            + "noClobberEvidenceSha256=" + HashText(noClobber) + "\n"
            + "wholeGameCoverageSemanticSha256=" + HashText(wholeCoverage) + "\n"
            + "decisionApplicationWriterNoClobberCoverageJoin=PASS\n"
            + "changedAssets=0\n"
            + "changedProperties=0\n"
            + "assetMutation=0\n"
            + "secondWriteDiff=0\n";
        WriteTextTwiceAndRequireSecondNoOp(
            MarketApplicationParentReportPath,
            parent);
        Debug.Log(parent);
    }

    internal static string BuildMarketApplicationFocusedReport()
    {
        V27BalanceAuditOutput audit = RequireCleanMarketAudit();
        MarketReviewDecisionValidation decisions = ValidateMarketReviewDecisions(
            audit.Ledger);
        if (decisions.PendingPromotions.Count != 0
            || decisions.AppliedPromoteBundleCount != decisions.PromoteBundleCount)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_NOT_EXACTLY_APPLIED: "
                + decisions.Format("focused"));
        }

        MarketApplicationReceiptValidation receipts =
            ValidateMarketApplicationReceipts(audit.Ledger);
        string root = ProjectRoot();
        V27PhysicalMassWriterProvenanceSnapshot writers =
            V27PhysicalMassWriterProvenanceRegistry.Capture(
                root,
                "Assets/Scripts/Services/Economy/Editor/"
                + "V27PhysicalMassAuthorityInventoryDebugScenarios.cs");
        RequireWriterSnapshot(writers);
        string writerReport = BuildWriterProvenanceReport(writers);
        WriteTextTwiceAndRequireSecondNoOp(
            MarketWriterProvenancePath,
            writerReport);

        string[] targetAssets = receipts.Rows
            .Select(value => value.SourceAuthority)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string beforeAssetSetDigest = ComputeAssetSetDigest(targetAssets);
        foreach (MarketApplicationReceiptValidatedRow row in receipts.Rows)
            RequireCurrentSerializedPropertyEqualsAfter(row);
        string afterAssetSetDigest = ComputeAssetSetDigest(targetAssets);
        if (!string.Equals(
                beforeAssetSetDigest,
                afterAssetSetDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_FOCUSED_MUTATED_ASSETS.");
        }

        MarketReviewDecisionFileData decisionFile = LoadMarketReviewDecisions();
        int decisionMembers = decisionFile.decisions.Sum(value => value.members.Length);
        string currentSourceDigest = V27CurrentSourceEvidenceDigest
            .ComputeAllScriptsDigest();
        string sceneDigest = V27CurrentSourceEvidenceDigest
            .ComputeGameplaySceneDigest();
        return "RESULT=PASS\n"
            + "schemaVersion=" + MarketApplicationParentSchema + "\n"
            + "currentSourceDigest=" + currentSourceDigest + "\n"
            + "gameplaySceneSha256=" + sceneDigest + "\n"
            + "decisionBundles=" + decisions.DecisionBundleCount + "\n"
            + "decisionMembers=" + decisionMembers + "\n"
            + "appliedPromoteBundles=" + decisions.AppliedPromoteBundleCount + "\n"
            + "pendingPromotionMembers=" + decisions.PendingPromotions.Count + "\n"
            + "receiptProperties=" + receipts.Rows.Count + "\n"
            + "receiptAssets=" + targetAssets.Length + "\n"
            + "receiptScopeDigest=" + receipts.ReceiptScopeDigest + "\n"
            + "receiptDigest=" + receipts.ReceiptDigest + "\n"
            + "decisionAuthoritySha256="
            + ComputeFileSha256(MarketReviewDecisionPath) + "\n"
            + "assetSetDigest=" + afterAssetSetDigest + "\n"
            + "writerDeclared=" + writers.DeclaredCount + "\n"
            + "writerDiscovered=" + writers.DiscoveredCount + "\n"
            + "writerUnknown=" + writers.Unknown.Count + "\n"
            + "writerDeclaredNotDiscovered="
            + writers.DeclaredNotDiscoveredCount + "\n"
            + "writerDuplicatePaths=" + writers.DuplicatePaths.Count + "\n"
            + "changedAssets=0\n"
            + "changedProperties=0\n"
            + "assetMutation=0\n"
            + "secondWriteDiff=0\n";
    }

    private static MarketApplicationReceiptValidation
        ValidateLegacyMarketApplicationReceipts(FrozenBalanceLedger ledger)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        string absolute = ProjectAbsolutePath(LegacyMarketApplicationReceiptPath);
        if (!File.Exists(absolute))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_MISSING: "
                + LegacyMarketApplicationReceiptPath);
        }

        MarketApplicationReceiptFileData file = JsonUtility.FromJson<
            MarketApplicationReceiptFileData>(File.ReadAllText(
                absolute,
                new UTF8Encoding(false, true)))
            ?? throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_INVALID_JSON.");
        if (!string.Equals(
                file.schemaVersion,
                LegacyMarketApplicationReceiptSchema,
                StringComparison.Ordinal)
            || !IsCanonicalGitRevision(file.baselineRevision)
            || file.receipts == null)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_INVALID_SCHEMA.");
        }

        string currentDecisionDigest = ComputeFileSha256(MarketReviewDecisionPath);
        if (!string.Equals(
                file.decisionAuthorityDigest,
                currentDecisionDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_DECISION_STALE.");
        }

        ExpectedMarketReceiptRow[] expected = CaptureExpectedMarketReceiptRows(ledger);
        if (expected.Length == 0)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_SCOPE_EMPTY.");
        }
        string expectedScopeDigest = ComputeExpectedReceiptScopeDigest(expected);
        if (!string.Equals(
                file.receiptScopeDigest,
                expectedScopeDigest,
                StringComparison.Ordinal)
            || expected.Length != file.receipts.Length)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_SCOPE_STALE: expected="
                + expected.Length + "; actual=" + file.receipts.Length + ".");
        }

        Dictionary<string, MarketApplicationReceiptRowData> receiptByIdentity =
            file.receipts.ToDictionary(
                value => ReceiptIdentity(
                    value.sourceAuthority,
                    value.sourcePropertyPath),
                StringComparer.Ordinal);
        Dictionary<string, string> baselineHashByAsset = expected
            .Select(value => value.SourceAuthority)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                value => value,
                value => ComputeGitBlobSha256(file.baselineRevision, value),
                StringComparer.Ordinal);
        List<MarketApplicationReceiptValidatedRow> validated = new();
        foreach (ExpectedMarketReceiptRow row in expected)
        {
            string identity = ReceiptIdentity(
                row.SourceAuthority,
                row.SourcePropertyPath);
            if (!receiptByIdentity.TryGetValue(
                    identity,
                    out MarketApplicationReceiptRowData receipt))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_PROPERTY_MISSING: " + identity);
            }
            RequireReceiptMatchesExpected(receipt, row);
            RequireCanonicalSha256(receipt.assetBeforeSha256, "asset before hash");
            RequireCanonicalSha256(receipt.assetAfterSha256, "asset after hash");
            if (!string.Equals(
                    receipt.assetBeforeSha256,
                    baselineHashByAsset[row.SourceAuthority],
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_ASSET_BEFORE_STALE: "
                    + row.SourceAuthority);
            }
            if (string.Equals(
                    receipt.assetBeforeSha256,
                    receipt.assetAfterSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_ASSET_HASHES_NOT_DISTINCT: "
                    + row.SourceAuthority);
            }
            string currentAfterHash = ComputeFileSha256(row.SourceAuthority);
            if (!string.Equals(
                    receipt.assetAfterSha256,
                    currentAfterHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_ASSET_AFTER_STALE: "
                    + row.SourceAuthority);
            }
            string expectedRowDigest = ComputeReceiptRowDigest(receipt);
            if (!string.Equals(
                    receipt.receiptRowDigest,
                    expectedRowDigest,
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

        string expectedReceiptDigest = ComputeReceiptDigest(file.receipts);
        if (!string.Equals(
                file.receiptDigest,
                expectedReceiptDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_FILE_DIGEST_STALE.");
        }
        return new MarketApplicationReceiptValidation(
            expectedScopeDigest,
            expectedReceiptDigest,
            validated.OrderBy(value => value.SourceAuthority, StringComparer.Ordinal)
                .ThenBy(value => value.SourcePropertyPath, StringComparer.Ordinal)
                .ToArray());
    }

    private static V27BalanceAuditOutput RequireCleanMarketAudit()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        if (audit.IntegrityFailures.Count != 0 || audit.CriticalCount != 0)
        {
            throw new InvalidOperationException(
                "Batch E requires a clean current-source audit: integrity="
                + audit.IntegrityFailures.Count + "; critical="
                + audit.CriticalCount + ".");
        }
        return audit;
    }

    private static ExpectedMarketReceiptRow[] CaptureExpectedMarketReceiptRows(
        FrozenBalanceLedger ledger)
    {
        MarketReviewDecisionFileData decisions = LoadMarketReviewDecisions();
        Dictionary<string, MarketReviewDecisionData> decisionByBundle = decisions.decisions
            .ToDictionary(value => value.bundleId, StringComparer.Ordinal);
        Dictionary<string, CanonicalBalanceMetricRecord> authorityByIdentity = ledger.Records
            .Where(value => !value.Metric.StartsWith(
                V27BalanceAudit.MarketRecalibrationCandidateMetricPrefix,
                StringComparison.Ordinal))
            .GroupBy(
                value => BuildApprovalIdentity(value.StableId, value.Metric),
                StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        V27BalanceMarketDebugScenarios.MarketReviewBundleRow[] rows =
            V27BalanceMarketDebugScenarios.BuildMarketReviewBundleRows(
                ledger.Records)
                .Where(value => string.Equals(
                    value.AuthorityState,
                    "previous-applied",
                    StringComparison.Ordinal))
                .OrderBy(value => value.SourceAuthority, StringComparer.Ordinal)
                .ThenBy(value => value.SourcePropertyPath, StringComparer.Ordinal)
                .ToArray();

        HashSet<string> propertyIdentities = new(StringComparer.Ordinal);
        List<ExpectedMarketReceiptRow> result = new();
        foreach (V27BalanceMarketDebugScenarios.MarketReviewBundleRow row in rows)
        {
            if (!decisionByBundle.TryGetValue(
                    row.BundleId,
                    out MarketReviewDecisionData decision))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_DECISION_MISSING: " + row.BundleId);
            }
            MarketReviewDecisionMemberData member = decision.members.Single(value =>
                string.Equals(value.stableId, row.StableId, StringComparison.Ordinal)
                && string.Equals(
                    value.authorityMetric,
                    row.AuthorityMetric,
                    StringComparison.Ordinal));
            RequireExactDecisionMember(decision, member, row);
            string approvalIdentity = BuildApprovalIdentity(
                row.StableId,
                row.AuthorityMetric);
            if (!authorityByIdentity.TryGetValue(
                    approvalIdentity,
                    out CanonicalBalanceMetricRecord authority)
                || !string.Equals(authority.After, row.Candidate, StringComparison.Ordinal)
                || !string.Equals(authority.Before, row.Before, StringComparison.Ordinal)
                || !string.Equals(
                    authority.DependencyFingerprint,
                    row.DependencyFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    authority.SourceDigest,
                    row.SourceDigest,
                    StringComparison.Ordinal)
                || !string.Equals(
                    authority.SemanticHash,
                    row.SemanticHash,
                    StringComparison.Ordinal)
                || !string.Equals(authority.AssetApplied, "true", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_AUTHORITY_STALE: "
                    + approvalIdentity);
            }
            string propertyIdentity = ReceiptIdentity(
                row.SourceAuthority,
                row.SourcePropertyPath);
            if (!propertyIdentities.Add(propertyIdentity))
            {
                throw new InvalidOperationException(
                    "MARKET_APPLICATION_RECEIPT_DUPLICATE_PROPERTY: "
                    + propertyIdentity);
            }
            result.Add(new ExpectedMarketReceiptRow(
                decision.bundleId,
                decision.bundleDigest,
                row.StableId,
                row.AuthorityMetric,
                row.SourceAuthority,
                row.SourcePropertyPath,
                row.Before,
                row.Candidate,
                row.DependencyFingerprint,
                row.SourceDigest,
                row.SemanticHash,
                ComputeDecisionMemberDigest(decision, member)));
        }
        return result.ToArray();
    }

    private static MarketApplicationReceiptRowData CreateReceiptRow(
        ExpectedMarketReceiptRow source,
        string assetBeforeSha256,
        string assetAfterSha256)
    {
        MarketApplicationReceiptRowData row = new()
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
            decisionMemberDigest = source.DecisionMemberDigest,
            assetBeforeSha256 = assetBeforeSha256,
            assetAfterSha256 = assetAfterSha256
        };
        row.receiptRowDigest = ComputeReceiptRowDigest(row);
        return row;
    }

    private static void RequireReceiptMatchesExpected(
        MarketApplicationReceiptRowData actual,
        ExpectedMarketReceiptRow expected)
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
            || !string.Equals(actual.decisionMemberDigest, expected.DecisionMemberDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_PROPERTY_STALE: "
                + ReceiptIdentity(expected.SourceAuthority, expected.SourcePropertyPath));
        }
    }

    private static void RequireCurrentSerializedPropertyEqualsAfter(
        MarketApplicationReceiptValidatedRow row)
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(row.SourceAuthority)
            ?? throw new InvalidOperationException(
                "Market receipt asset is missing: " + row.SourceAuthority);
        SerializedObject serialized = new(asset);
        SerializedProperty property = serialized.FindProperty(row.SourcePropertyPath)
            ?? throw new InvalidOperationException(
                "Market receipt property is missing: "
                + ReceiptIdentity(row.SourceAuthority, row.SourcePropertyPath));
        if (!TokenMatchesProperty(property, row.ExactAfterValue))
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_PROPERTY_AFTER_STALE: "
                + ReceiptIdentity(row.SourceAuthority, row.SourcePropertyPath)
                + "; current=" + CaptureToken(property)
                + "; expected=" + row.ExactAfterValue + ".");
        }
    }

    private static void RequireWriterSnapshot(
        V27PhysicalMassWriterProvenanceSnapshot snapshot)
    {
        if (snapshot.Rows.Count == 0
            || snapshot.Unknown.Count != 0
            || snapshot.DuplicatePaths.Count != 0
            || snapshot.DeclaredCount != snapshot.DiscoveredCount
            || snapshot.DeclaredNotDiscoveredCount != 0)
        {
            throw new InvalidOperationException(
                "PHYSICAL_MASS_WRITER_PROVENANCE_FAILED: declared="
                + snapshot.DeclaredCount + "; discovered="
                + snapshot.DiscoveredCount + "; unknown="
                + snapshot.Unknown.Count + "; stale="
                + snapshot.DeclaredNotDiscoveredCount + "; duplicates="
                + snapshot.DuplicatePaths.Count + ".");
        }
    }

    private static string BuildWriterProvenanceReport(
        V27PhysicalMassWriterProvenanceSnapshot snapshot)
    {
        StringBuilder result = new();
        result.Append("RESULT=PASS; declared=")
            .Append(snapshot.DeclaredCount)
            .Append("; discovered=").Append(snapshot.DiscoveredCount)
            .Append("; unknown=0; declaredNotDiscovered=0; duplicatePaths=0")
            .Append("; registryMode=source-derived-no-static-declarations\n")
            .Append("currentSourceDigest=")
            .Append(V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest())
            .Append('\n');
        foreach (V27PhysicalMassWriterProvenanceRow row in snapshot.Rows)
        {
            result.Append(row.Role).Append('\t')
                .Append(row.Path).Append('\t')
                .Append(row.EvidenceShape).Append('\t')
                .Append(row.WriteSiteCount.ToString(CultureInfo.InvariantCulture))
                .Append('\t').Append(row.Digest).Append('\n');
        }
        return result.ToString();
    }

    private static string ComputeExpectedReceiptScopeDigest(
        IEnumerable<ExpectedMarketReceiptRow> rows) => HashText(string.Join(
        "",
        rows.OrderBy(value => value.SourceAuthority, StringComparer.Ordinal)
            .ThenBy(value => value.SourcePropertyPath, StringComparer.Ordinal)
            .Select(value => value.Canonical + "\n")));

    private static string ComputeDecisionMemberDigest(
        MarketReviewDecisionData decision,
        MarketReviewDecisionMemberData member) => HashText(
        decision.bundleId + "\u001f"
        + decision.bundleDigest + "\u001f"
        + decision.reviewedBaselineRecordId + "\u001f"
        + member.stableId + "\u001f"
        + member.authorityMetric + "\u001f"
        + member.sourceAuthority + "\u001f"
        + member.sourcePropertyPath + "\u001f"
        + member.beforeExactToken + "\u001f"
        + member.candidateExactToken + "\u001f"
        + member.dependencyFingerprint + "\u001f"
        + member.sourceDigest + "\u001f"
        + member.semanticHash + "\u001f"
        + member.decision);

    private static string ComputeReceiptRowDigest(
        MarketApplicationReceiptRowData row) => HashText(
        row.decisionBundleId + "\u001f"
        + row.decisionBundleDigest + "\u001f"
        + row.stableId + "\u001f"
        + row.authorityMetric + "\u001f"
        + row.sourceAuthority + "\u001f"
        + row.sourcePropertyPath + "\u001f"
        + row.exactBeforeValue + "\u001f"
        + row.exactAfterValue + "\u001f"
        + row.dependencyFingerprint + "\u001f"
        + row.sourceDigest + "\u001f"
        + row.semanticHash + "\u001f"
        + row.decisionMemberDigest + "\u001f"
        + row.assetBeforeSha256 + "\u001f"
        + row.assetAfterSha256);

    private static string ComputeReceiptDigest(
        IEnumerable<MarketApplicationReceiptRowData> rows) => HashText(string.Join(
        "",
        rows.OrderBy(value => value.sourceAuthority, StringComparer.Ordinal)
            .ThenBy(value => value.sourcePropertyPath, StringComparer.Ordinal)
            .Select(value => value.receiptRowDigest + "\n")));

    private static byte[] SerializeReceipt(MarketApplicationReceiptFileData file)
    {
        string json = JsonUtility.ToJson(file, prettyPrint: true) + "\n";
        return new UTF8Encoding(false, true).GetBytes(json.Replace("\r\n", "\n"));
    }

    private static string ComputeAssetSetDigest(IEnumerable<string> paths) =>
        HashText(string.Join(
            "",
            paths.OrderBy(value => value, StringComparer.Ordinal)
                .Select(value => value + "\t" + ComputeFileSha256(value) + "\n")));

    private static string ComputeFileSha256(string relativePath)
    {
        string absolute = ProjectAbsolutePath(relativePath);
        if (!File.Exists(absolute))
            throw new InvalidOperationException("Evidence file is missing: " + relativePath);
        using FileStream stream = File.OpenRead(absolute);
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(stream));
    }

    private static string ComputeGitBlobSha256(
        string revision,
        string relativePath)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = ProjectRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = "show " + QuoteArgument(revision + ":" + relativePath)
        };
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start git blob capture.");
        using MemoryStream bytes = new();
        process.StandardOutput.BaseStream.CopyTo(bytes);
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_RECEIPT_BASELINE_MISSING: "
                + relativePath + "; " + error);
        }
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(bytes.ToArray()));
    }

    private static string CaptureGitHeadRevision()
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = ProjectRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = "rev-parse HEAD"
        };
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start git revision capture.");
        string output = process.StandardOutput.ReadToEnd().Trim();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || output.Length != 40)
            throw new InvalidOperationException("git revision capture failed: " + error);
        return output.ToLowerInvariant();
    }

    private static void WriteTextTwiceAndRequireSecondNoOp(
        string path,
        string value) => WriteBytesTwiceAndRequireSecondNoOp(
        path,
        new UTF8Encoding(false, true).GetBytes(
            (value ?? string.Empty).Replace("\r\n", "\n")));

    private static void WriteBytesTwiceAndRequireSecondNoOp(
        string path,
        byte[] bytes)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(
            path,
            stream => stream.Write(bytes, 0, bytes.Length));
        string absolute = ProjectAbsolutePath(path);
        string hash = ComputeFileSha256(path);
        long length = new FileInfo(absolute).Length;
        long ticks = File.GetLastWriteTimeUtc(absolute).Ticks;
        V27BalanceArtifactWriter.WriteIfDifferent(
            path,
            stream => stream.Write(bytes, 0, bytes.Length));
        if (!string.Equals(hash, ComputeFileSha256(path), StringComparison.Ordinal)
            || length != new FileInfo(absolute).Length
            || ticks != File.GetLastWriteTimeUtc(absolute).Ticks)
        {
            throw new InvalidOperationException(
                "MARKET_APPLICATION_SECOND_WRITE_NOT_NO_OP: " + path);
        }
    }

    private static string HashText(string value)
    {
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(value ?? string.Empty)));
    }

    private static string ReceiptIdentity(string assetPath, string propertyPath) =>
        assetPath + "\u001f" + propertyPath;

    private static bool IsCanonicalGitRevision(string value) =>
        value != null
        && value.Length == 40
        && value.All(character => (character >= '0' && character <= '9')
            || (character >= 'a' && character <= 'f'));

    [Serializable]
    private sealed class MarketApplicationReceiptFileData
    {
        public string schemaVersion;
        public string baselineRevision;
        public string decisionAuthorityDigest;
        public string receiptScopeDigest;
        public string receiptDigest;
        public MarketApplicationReceiptRowData[] receipts;
    }

    [Serializable]
    private sealed class MarketApplicationReceiptRowData
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
        public string decisionMemberDigest;
        public string assetBeforeSha256;
        public string assetAfterSha256;
        public string receiptRowDigest;
    }

    private sealed class ExpectedMarketReceiptRow
    {
        public ExpectedMarketReceiptRow(
            string decisionBundleId,
            string decisionBundleDigest,
            string stableId,
            string authorityMetric,
            string sourceAuthority,
            string sourcePropertyPath,
            string exactBeforeValue,
            string exactAfterValue,
            string dependencyFingerprint,
            string sourceDigest,
            string semanticHash,
            string decisionMemberDigest)
        {
            DecisionBundleId = decisionBundleId;
            DecisionBundleDigest = decisionBundleDigest;
            StableId = stableId;
            AuthorityMetric = authorityMetric;
            SourceAuthority = sourceAuthority;
            SourcePropertyPath = sourcePropertyPath;
            ExactBeforeValue = exactBeforeValue;
            ExactAfterValue = exactAfterValue;
            DependencyFingerprint = dependencyFingerprint;
            SourceDigest = sourceDigest;
            SemanticHash = semanticHash;
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
        public string DecisionMemberDigest { get; }
        public string Canonical => DecisionBundleId + "\u001f"
            + DecisionBundleDigest + "\u001f" + StableId + "\u001f"
            + AuthorityMetric + "\u001f" + SourceAuthority + "\u001f"
            + SourcePropertyPath + "\u001f" + ExactBeforeValue + "\u001f"
            + ExactAfterValue + "\u001f" + DependencyFingerprint + "\u001f"
            + SourceDigest + "\u001f" + SemanticHash + "\u001f"
            + DecisionMemberDigest;
    }
}

public sealed class MarketApplicationReceiptValidation
{
    public MarketApplicationReceiptValidation(
        string receiptScopeDigest,
        string receiptDigest,
        IReadOnlyList<MarketApplicationReceiptValidatedRow> rows)
    {
        ReceiptScopeDigest = receiptScopeDigest ?? string.Empty;
        ReceiptDigest = receiptDigest ?? string.Empty;
        Rows = rows ?? Array.Empty<MarketApplicationReceiptValidatedRow>();
    }

    public string ReceiptScopeDigest { get; }
    public string ReceiptDigest { get; }
    public IReadOnlyList<MarketApplicationReceiptValidatedRow> Rows { get; }

    public string Format(string action) =>
        "V27 market application receipts " + action + ": properties="
        + Rows.Count + "; scopeDigest=" + ReceiptScopeDigest
        + "; receiptDigest=" + ReceiptDigest + ".";
}

public readonly struct MarketApplicationReceiptValidatedRow
{
    public MarketApplicationReceiptValidatedRow(
        string sourceAuthority,
        string sourcePropertyPath,
        string exactBeforeValue,
        string exactAfterValue,
        string assetBeforeSha256,
        string assetAfterSha256)
    {
        SourceAuthority = sourceAuthority ?? string.Empty;
        SourcePropertyPath = sourcePropertyPath ?? string.Empty;
        ExactBeforeValue = exactBeforeValue ?? string.Empty;
        ExactAfterValue = exactAfterValue ?? string.Empty;
        AssetBeforeSha256 = assetBeforeSha256 ?? string.Empty;
        AssetAfterSha256 = assetAfterSha256 ?? string.Empty;
    }

    public string SourceAuthority { get; }
    public string SourcePropertyPath { get; }
    public string ExactBeforeValue { get; }
    public string ExactAfterValue { get; }
    public string AssetBeforeSha256 { get; }
    public string AssetAfterSha256 { get; }
}
#endif
