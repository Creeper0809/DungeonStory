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
    internal const string MarketSecondApplyNoOpReceiptPath =
        "Artifacts/QA/v27-balance-market-second-apply-noop.json";
    private const string MarketSecondApplyNoOpReceiptSchema =
        "v27.market-second-apply-noop.1";

    private static void WriteMarketSecondApplyNoOpReceipt(
        MarketReviewDecisionFileData decisionFile,
        FrozenBalanceLedger appliedLedger,
        MarketApplicationReceiptValidation receiptValidation)
    {
        if (UnityEditor.EditorApplication.isCompiling
            || UnityEditor.EditorApplication.isUpdating)
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_EDITOR_NOT_STABLE.");
        }
        V27CurrentSourceEvidenceSnapshot sourceBefore =
            V27CurrentSourceEvidenceDigest.Capture();
        if (decisionFile == null)
            throw new ArgumentNullException(nameof(decisionFile));
        if (appliedLedger == null)
            throw new ArgumentNullException(nameof(appliedLedger));
        if (receiptValidation == null)
            throw new ArgumentNullException(nameof(receiptValidation));

        string applicationReceiptPath = ResolveMarketApplicationReceiptPath(
            decisionFile.decisionEpochDigest);
        MarketApplicationReceiptV2FileData applicationReceipt =
            JsonUtility.FromJson<MarketApplicationReceiptV2FileData>(
                V27StrictJsonGuard.ReadProjectRelative(
                    applicationReceiptPath))
            ?? throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_V2_RECEIPT_INVALID_JSON.");
        RequireMarketReceiptV2Header(applicationReceipt, decisionFile);
        string currentDecisionSha256 = ComputeFileSha256(
            MarketReviewDecisionPath);
        if (!string.Equals(
                applicationReceipt.decisionAuthoritySha256Diagnostic,
                currentDecisionSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_DECISION_AUTHORITY_STALE.");
        }

        MarketApplicationPatchScopeRowData[] propertyScope =
            (applicationReceipt.patchScopeRows
             ?? Array.Empty<MarketApplicationPatchScopeRowData>())
            .OrderBy(value => value.role, StringComparer.Ordinal)
            .ThenBy(value => value.stableId, StringComparer.Ordinal)
            .ThenBy(value => value.metric, StringComparer.Ordinal)
            .ThenBy(value => value.sourceAuthority, StringComparer.Ordinal)
            .ThenBy(value => value.sourcePropertyPath, StringComparer.Ordinal)
            .ToArray();
        if (propertyScope.Length == 0)
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_PROPERTY_SCOPE_EMPTY.");
        }
        string propertySetDigest = ComputeMarketApplicationPatchScopeDigest(
            propertyScope);
        if (!string.Equals(
                propertySetDigest,
                decisionFile.patchScopeDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_PATCH_SCOPE_STALE.");
        }
        string[] duplicateProperties = propertyScope
            .GroupBy(
                value => ReceiptIdentity(
                    value.sourceAuthority,
                    value.sourcePropertyPath),
                StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (duplicateProperties.Length != 0)
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_DUPLICATE_PROPERTY: "
                + string.Join(",", duplicateProperties));
        }

        MarketApplicationReceiptAssetData[] receiptAssets =
            (applicationReceipt.assets
             ?? Array.Empty<MarketApplicationReceiptAssetData>())
            .OrderBy(value => value.sourceAuthority, StringComparer.Ordinal)
            .ToArray();
        if (receiptAssets.Length == 0
            || receiptAssets.Select(value => value.sourceAuthority)
                .Distinct(StringComparer.Ordinal).Count() != receiptAssets.Length)
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_ASSET_SCOPE_INVALID.");
        }
        HashSet<string> receiptAssetPaths = receiptAssets
            .Select(value => value.sourceAuthority)
            .ToHashSet(StringComparer.Ordinal);
        string[] missingPropertyAssets = propertyScope
            .Select(value => value.sourceAuthority)
            .Where(value => !receiptAssetPaths.Contains(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (missingPropertyAssets.Length != 0)
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_PROPERTY_ASSET_MISSING: "
                + string.Join(",", missingPropertyAssets));
        }

        NoOpFileState[] before = receiptAssets
            .SelectMany(value => new[]
            {
                CaptureNoOpFileState(value.sourceAuthority),
                CaptureNoOpFileState(value.sourceAuthority + ".meta")
            })
            .Append(CaptureNoOpFileState(applicationReceiptPath))
            .Append(CaptureNoOpFileState(MarketReviewDecisionPath))
            .Append(CaptureNoOpFileState(V27BalanceAudit.ApprovalPath))
            .OrderBy(value => value.Path, StringComparer.Ordinal)
            .ToArray();
        BalanceAssetPatch[] patches = propertyScope
            .Select(value => BalanceAssetPatch.CaptureForMarketSecondApplyNoOp(
                value.sourceAuthority,
                value.sourcePropertyPath,
                value.before,
                value.after))
            .ToArray();

        BalanceAssetApplicationResult application = ApplyPatches(
            patches,
            dryRun: false,
            requireCleanGit: false,
            BalanceAssetApplicationFailurePoint.None);
        int expectedApplicationAssetCount = propertyScope
            .Select(value => value.sourceAuthority)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (!application.Passed
            || application.ApprovedPatchCount != propertyScope.Length
            || application.AssetCount != expectedApplicationAssetCount
            || application.DifferingPropertyCount != 0)
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_NOT_NO_OP: "
                + application.Format("second-apply"));
        }
        MarketApplicationReceiptValidation afterReceiptValidation =
            ValidateMarketApplicationReceipts(appliedLedger);
        if (!string.Equals(
                receiptValidation.ReceiptScopeDigest,
                afterReceiptValidation.ReceiptScopeDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                receiptValidation.ReceiptDigest,
                afterReceiptValidation.ReceiptDigest,
                StringComparison.Ordinal)
            || receiptValidation.Rows.Count != afterReceiptValidation.Rows.Count)
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_V2_RECEIPT_CHANGED.");
        }

        Dictionary<string, NoOpFileState> after = receiptAssets
            .SelectMany(value => new[]
            {
                CaptureNoOpFileState(value.sourceAuthority),
                CaptureNoOpFileState(value.sourceAuthority + ".meta")
            })
            .Append(CaptureNoOpFileState(applicationReceiptPath))
            .Append(CaptureNoOpFileState(MarketReviewDecisionPath))
            .Append(CaptureNoOpFileState(V27BalanceAudit.ApprovalPath))
            .ToDictionary(value => value.Path, StringComparer.Ordinal);
        int byteDifferences = 0;
        int lengthDifferences = 0;
        int mtimeDifferences = 0;
        foreach (NoOpFileState state in before)
        {
            NoOpFileState current = after[state.Path];
            if (!state.Bytes.SequenceEqual(current.Bytes))
                byteDifferences++;
            if (state.Length != current.Length)
                lengthDifferences++;
            if (state.LastWriteUtcTicks != current.LastWriteUtcTicks)
                mtimeDifferences++;
        }
        if (byteDifferences != 0
            || lengthDifferences != 0
            || mtimeDifferences != 0)
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_FILE_MUTATION: bytes="
                + byteDifferences.ToString(CultureInfo.InvariantCulture)
                + "; length="
                + lengthDifferences.ToString(CultureInfo.InvariantCulture)
                + "; mtime="
                + mtimeDifferences.ToString(CultureInfo.InvariantCulture)
                + ".");
        }

        MarketSecondApplyNoOpAssetData[] assets = receiptAssets
            .Select(value =>
            {
                NoOpFileState state = after[value.sourceAuthority];
                if (!string.Equals(
                        value.assetAfterSha256,
                        state.Sha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "MARKET_SECOND_APPLY_ASSET_HASH_STALE: "
                        + value.sourceAuthority);
                }
                return MarketSecondApplyNoOpAssetData.Capture(
                    value.sourceAuthority,
                    value.assetAfterSha256,
                    state.Sha256,
                    state.Length);
            })
            .ToArray();
        MarketSecondApplyNoOpPropertyData[] properties = propertyScope
            .Select(value => MarketSecondApplyNoOpPropertyData.Capture(
                value.role,
                value.stableId,
                value.metric,
                value.sourceAuthority,
                value.sourcePropertyPath,
                value.before,
                value.after,
                value.dependencyFingerprint,
                value.sourceDigest,
                value.semanticHash))
            .ToArray();
        MarketSecondApplyNoOpFileData[] files = after.Values
            .OrderBy(value => value.Path, StringComparer.Ordinal)
            .Select(value => MarketSecondApplyNoOpFileData.Capture(
                value.Path,
                value.Sha256,
                value.Length))
            .ToArray();
        string assetSetDigest = ComputeMarketSecondApplyAssetSetDigest(assets);
        V27CurrentSourceEvidenceSnapshot sourceAfter =
            V27CurrentSourceEvidenceDigest.Capture();
        if (!string.Equals(sourceBefore.Digest, sourceAfter.Digest,
                StringComparison.Ordinal)
            || sourceBefore.InputCount != sourceAfter.InputCount
            || !string.Equals(sourceBefore.PathListDigest,
                sourceAfter.PathListDigest, StringComparison.Ordinal)
            || UnityEditor.EditorApplication.isCompiling
            || UnityEditor.EditorApplication.isUpdating)
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_CURRENT_SOURCE_DRIFT.");
        }
        MarketSecondApplyNoOpReceiptDraft draft = new()
        {
            schemaVersion = MarketSecondApplyNoOpReceiptSchema,
            executionCommand =
                "DungeonStory/V27/Apply Reviewed Market Promotions",
            executionBranch = "already-applied-no-op",
            sourceDigest = sourceBefore.Digest,
            sourceInputCount = sourceBefore.InputCount,
            sourcePathListDigest = sourceBefore.PathListDigest,
            epochId = decisionFile.epochId,
            decisionAuthoritySha256 = currentDecisionSha256,
            decisionPayloadDigest = decisionFile.decisionPayloadDigest,
            decisionEpochDigest = decisionFile.decisionEpochDigest,
            sourceLedgerDigest = decisionFile.sourceLedgerDigest,
            patchScopeDigest = decisionFile.patchScopeDigest,
            v2ApplicationReceiptPath = applicationReceiptPath,
            v2ApplicationReceiptSha256 = ComputeFileSha256(
                applicationReceiptPath),
            v2ApplicationReceiptByteLength =
                after[applicationReceiptPath].Length,
            v2ReceiptScopeDigest = receiptValidation.ReceiptScopeDigest,
            v2ReceiptDigest = receiptValidation.ReceiptDigest,
            applicationInvocationOrdinal = 2,
            applicationInvocationCount = 1,
            approvedPatchCount = application.ApprovedPatchCount,
            applicationAssetCount = application.AssetCount,
            v2AssetCount = assets.Length,
            propertyCount = properties.Length,
            targetFileCount = files.Length,
            differingPropertyCount = application.DifferingPropertyCount,
            runtimeByteDifferenceCount = byteDifferences,
            runtimeLengthDifferenceCount = lengthDifferences,
            runtimeMtimeDifferenceCount = mtimeDifferences,
            assetSetDigest = assetSetDigest,
            propertySetDigest = propertySetDigest,
            assets = assets,
            properties = properties,
            files = files
        };
        draft.receiptDigest = ComputeMarketSecondApplyNoOpReceiptDigest(draft);
        MarketSecondApplyNoOpReceiptData output =
            MarketSecondApplyNoOpReceiptData.Capture(draft);
        V27BalanceArtifactWriter.WriteIfDifferent(
            MarketSecondApplyNoOpReceiptPath,
            stream => MarketSecondApplyNoOpReceiptSerializer.Write(stream, output));
        bool secondWriteChanged = V27BalanceArtifactWriter.WriteIfDifferent(
            MarketSecondApplyNoOpReceiptPath,
            stream => MarketSecondApplyNoOpReceiptSerializer.Write(stream, output));
        if (secondWriteChanged)
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_RECEIPT_SECOND_WRITE_CHANGED.");
        }
    }

    private static NoOpFileState CaptureNoOpFileState(string path)
    {
        string absolute = ProjectAbsolutePath(path);
        if (!File.Exists(absolute))
        {
            throw new InvalidOperationException(
                "MARKET_SECOND_APPLY_FILE_MISSING: " + path);
        }
        FileInfo info = new(absolute);
        byte[] bytes = File.ReadAllBytes(absolute);
        return new NoOpFileState(
            path,
            Sha256Lower(bytes),
            bytes,
            info.Length,
            info.LastWriteTimeUtc.Ticks);
    }

    private static string ComputeMarketSecondApplyAssetSetDigest(
        IEnumerable<MarketSecondApplyNoOpAssetData> assets)
    {
        StringBuilder canonical = new();
        foreach (MarketSecondApplyNoOpAssetData asset in assets
                     .OrderBy(value => value.sourceAuthority, StringComparer.Ordinal))
        {
            AppendCanonicalField(canonical, asset.sourceAuthority);
            AppendCanonicalField(canonical, asset.expectedAfterSha256);
            AppendCanonicalField(canonical, asset.observedSha256);
            AppendCanonicalField(
                canonical,
                asset.byteLength.ToString(CultureInfo.InvariantCulture));
        }
        return HashBytes(new UTF8Encoding(false, true).GetBytes(
            canonical.ToString())).ToLowerInvariant();
    }

    private static string ComputeMarketSecondApplyNoOpReceiptDigest(
        MarketSecondApplyNoOpReceiptDraft receipt)
    {
        StringBuilder canonical = new();
        AppendCanonicalField(canonical, receipt.schemaVersion);
        AppendCanonicalField(canonical, receipt.executionCommand);
        AppendCanonicalField(canonical, receipt.executionBranch);
        AppendCanonicalField(canonical, receipt.sourceDigest);
        AppendCanonicalField(
            canonical,
            receipt.sourceInputCount.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalField(canonical, receipt.sourcePathListDigest);
        AppendCanonicalField(canonical, receipt.epochId);
        AppendCanonicalField(canonical, receipt.decisionAuthoritySha256);
        AppendCanonicalField(canonical, receipt.decisionPayloadDigest);
        AppendCanonicalField(canonical, receipt.decisionEpochDigest);
        AppendCanonicalField(canonical, receipt.sourceLedgerDigest);
        AppendCanonicalField(canonical, receipt.patchScopeDigest);
        AppendCanonicalField(canonical, receipt.v2ApplicationReceiptPath);
        AppendCanonicalField(canonical, receipt.v2ApplicationReceiptSha256);
        AppendCanonicalField(
            canonical,
            receipt.v2ApplicationReceiptByteLength.ToString(
                CultureInfo.InvariantCulture));
        AppendCanonicalField(canonical, receipt.v2ReceiptScopeDigest);
        AppendCanonicalField(canonical, receipt.v2ReceiptDigest);
        AppendCanonicalField(
            canonical,
            receipt.applicationInvocationOrdinal.ToString(
                CultureInfo.InvariantCulture));
        AppendCanonicalField(
            canonical,
            receipt.applicationInvocationCount.ToString(
                CultureInfo.InvariantCulture));
        AppendCanonicalField(
            canonical,
            receipt.approvedPatchCount.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalField(
            canonical,
            receipt.applicationAssetCount.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalField(
            canonical,
            receipt.v2AssetCount.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalField(
            canonical,
            receipt.propertyCount.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalField(
            canonical,
            receipt.targetFileCount.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalField(
            canonical,
            receipt.differingPropertyCount.ToString(CultureInfo.InvariantCulture));
        AppendCanonicalField(
            canonical,
            receipt.runtimeByteDifferenceCount.ToString(
                CultureInfo.InvariantCulture));
        AppendCanonicalField(
            canonical,
            receipt.runtimeLengthDifferenceCount.ToString(
                CultureInfo.InvariantCulture));
        AppendCanonicalField(
            canonical,
            receipt.runtimeMtimeDifferenceCount.ToString(
                CultureInfo.InvariantCulture));
        AppendCanonicalField(canonical, receipt.assetSetDigest);
        AppendCanonicalField(canonical, receipt.propertySetDigest);
        foreach (MarketSecondApplyNoOpAssetData asset in receipt.assets)
        {
            AppendCanonicalField(canonical, asset.sourceAuthority);
            AppendCanonicalField(canonical, asset.expectedAfterSha256);
            AppendCanonicalField(canonical, asset.observedSha256);
            AppendCanonicalField(
                canonical,
                asset.byteLength.ToString(CultureInfo.InvariantCulture));
        }
        foreach (MarketSecondApplyNoOpPropertyData property in receipt.properties)
        {
            AppendCanonicalField(canonical, property.role);
            AppendCanonicalField(canonical, property.stableId);
            AppendCanonicalField(canonical, property.metric);
            AppendCanonicalField(canonical, property.sourceAuthority);
            AppendCanonicalField(canonical, property.sourcePropertyPath);
            AppendCanonicalField(canonical, property.before);
            AppendCanonicalField(canonical, property.after);
            AppendCanonicalField(canonical, property.dependencyFingerprint);
            AppendCanonicalField(canonical, property.sourceDigest);
            AppendCanonicalField(canonical, property.semanticHash);
        }
        foreach (MarketSecondApplyNoOpFileData file in receipt.files)
        {
            AppendCanonicalField(canonical, file.path);
            AppendCanonicalField(canonical, file.sha256);
            AppendCanonicalField(
                canonical,
                file.byteLength.ToString(CultureInfo.InvariantCulture));
        }
        return HashBytes(new UTF8Encoding(false, true).GetBytes(
            canonical.ToString())).ToLowerInvariant();
    }

    [BalanceSerializationLayer]
    private static class MarketSecondApplyNoOpReceiptSerializer
    {
        public static void Write(
            Stream stream,
            MarketSecondApplyNoOpReceiptData value)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            using StreamWriter writer = new(
                stream,
                new UTF8Encoding(false, true),
                8192,
                leaveOpen: true);
            writer.Write('{');
            writer.Write('\n');
            WriteString(writer, "schemaVersion", value.schemaVersion);
            WriteString(writer, "executionCommand", value.executionCommand);
            WriteString(writer, "executionBranch", value.executionBranch);
            WriteString(writer, "sourceDigest", value.sourceDigest);
            WriteInt(writer, "sourceInputCount", value.sourceInputCount);
            WriteString(writer, "sourcePathListDigest", value.sourcePathListDigest);
            WriteString(writer, "epochId", value.epochId);
            WriteString(writer, "decisionAuthoritySha256",
                value.decisionAuthoritySha256);
            WriteString(writer, "decisionPayloadDigest", value.decisionPayloadDigest);
            WriteString(writer, "decisionEpochDigest", value.decisionEpochDigest);
            WriteString(writer, "sourceLedgerDigest", value.sourceLedgerDigest);
            WriteString(writer, "patchScopeDigest", value.patchScopeDigest);
            WriteString(writer, "v2ApplicationReceiptPath",
                value.v2ApplicationReceiptPath);
            WriteString(writer, "v2ApplicationReceiptSha256",
                value.v2ApplicationReceiptSha256);
            WriteLong(writer, "v2ApplicationReceiptByteLength",
                value.v2ApplicationReceiptByteLength);
            WriteString(writer, "v2ReceiptScopeDigest", value.v2ReceiptScopeDigest);
            WriteString(writer, "v2ReceiptDigest", value.v2ReceiptDigest);
            WriteInt(writer, "applicationInvocationOrdinal",
                value.applicationInvocationOrdinal);
            WriteInt(writer, "applicationInvocationCount",
                value.applicationInvocationCount);
            WriteInt(writer, "approvedPatchCount", value.approvedPatchCount);
            WriteInt(writer, "applicationAssetCount", value.applicationAssetCount);
            WriteInt(writer, "v2AssetCount", value.v2AssetCount);
            WriteInt(writer, "propertyCount", value.propertyCount);
            WriteInt(writer, "targetFileCount", value.targetFileCount);
            WriteInt(writer, "differingPropertyCount", value.differingPropertyCount);
            WriteInt(writer, "runtimeByteDifferenceCount",
                value.runtimeByteDifferenceCount);
            WriteInt(writer, "runtimeLengthDifferenceCount",
                value.runtimeLengthDifferenceCount);
            WriteInt(writer, "runtimeMtimeDifferenceCount",
                value.runtimeMtimeDifferenceCount);
            WriteString(writer, "assetSetDigest", value.assetSetDigest);
            WriteString(writer, "propertySetDigest", value.propertySetDigest);
            WriteString(writer, "receiptDigest", value.receiptDigest);
            WriteAssets(writer, value.assets);
            WriteProperties(writer, value.properties);
            WriteFiles(writer, value.files);
            writer.Write('}');
            writer.Write('\n');
            writer.Flush();
        }

        private static void WriteAssets(
            StreamWriter writer,
            IReadOnlyList<MarketSecondApplyNoOpAssetData> values)
        {
            writer.Write("  \"assets\": [");
            writer.Write('\n');
            for (int index = 0; index < values.Count; index++)
            {
                MarketSecondApplyNoOpAssetData value = values[index];
                writer.Write("    {\"sourceAuthority\":");
                WriteJson(writer, value.sourceAuthority);
                writer.Write(",\"expectedAfterSha256\":");
                WriteJson(writer, value.expectedAfterSha256);
                writer.Write(",\"observedSha256\":");
                WriteJson(writer, value.observedSha256);
                writer.Write(",\"byteLength\":");
                writer.Write(value.byteLength);
                writer.Write('}');
                if (index + 1 < values.Count)
                    writer.Write(',');
                writer.Write('\n');
            }
            writer.Write("  ],");
            writer.Write('\n');
        }

        private static void WriteProperties(
            StreamWriter writer,
            IReadOnlyList<MarketSecondApplyNoOpPropertyData> values)
        {
            writer.Write("  \"properties\": [");
            writer.Write('\n');
            for (int index = 0; index < values.Count; index++)
            {
                MarketSecondApplyNoOpPropertyData value = values[index];
                writer.Write("    {\"role\":");
                WriteJson(writer, value.role);
                writer.Write(",\"stableId\":");
                WriteJson(writer, value.stableId);
                writer.Write(",\"metric\":");
                WriteJson(writer, value.metric);
                writer.Write(",\"sourceAuthority\":");
                WriteJson(writer, value.sourceAuthority);
                writer.Write(",\"sourcePropertyPath\":");
                WriteJson(writer, value.sourcePropertyPath);
                writer.Write(",\"before\":");
                WriteJson(writer, value.before);
                writer.Write(",\"after\":");
                WriteJson(writer, value.after);
                writer.Write(",\"dependencyFingerprint\":");
                WriteJson(writer, value.dependencyFingerprint);
                writer.Write(",\"sourceDigest\":");
                WriteJson(writer, value.sourceDigest);
                writer.Write(",\"semanticHash\":");
                WriteJson(writer, value.semanticHash);
                writer.Write('}');
                if (index + 1 < values.Count)
                    writer.Write(',');
                writer.Write('\n');
            }
            writer.Write("  ],");
            writer.Write('\n');
        }

        private static void WriteFiles(
            StreamWriter writer,
            IReadOnlyList<MarketSecondApplyNoOpFileData> values)
        {
            writer.Write("  \"files\": [");
            writer.Write('\n');
            for (int index = 0; index < values.Count; index++)
            {
                MarketSecondApplyNoOpFileData value = values[index];
                writer.Write("    {\"path\":");
                WriteJson(writer, value.path);
                writer.Write(",\"sha256\":");
                WriteJson(writer, value.sha256);
                writer.Write(",\"byteLength\":");
                writer.Write(value.byteLength);
                writer.Write('}');
                if (index + 1 < values.Count)
                    writer.Write(',');
                writer.Write('\n');
            }
            writer.Write("  ]");
            writer.Write('\n');
        }

        private static void WriteString(
            StreamWriter writer,
            string name,
            string value)
        {
            writer.Write("  ");
            WriteJson(writer, name);
            writer.Write(": ");
            WriteJson(writer, value);
            writer.Write(',');
            writer.Write('\n');
        }

        private static void WriteInt(StreamWriter writer, string name, int value)
        {
            writer.Write("  ");
            WriteJson(writer, name);
            writer.Write(": ");
            writer.Write(value);
            writer.Write(',');
            writer.Write('\n');
        }

        private static void WriteLong(StreamWriter writer, string name, long value)
        {
            writer.Write("  ");
            WriteJson(writer, name);
            writer.Write(": ");
            writer.Write(value);
            writer.Write(',');
            writer.Write('\n');
        }

        private static void WriteJson(StreamWriter writer, string value) =>
            V27BalanceJsonSerializer.WriteJsonString(writer, value);
    }

    private readonly struct NoOpFileState
    {
        public NoOpFileState(
            string path,
            string sha256,
            byte[] bytes,
            long length,
            long lastWriteUtcTicks)
        {
            Path = path;
            Sha256 = sha256;
            Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
            Length = length;
            LastWriteUtcTicks = lastWriteUtcTicks;
        }

        public string Path { get; }
        public string Sha256 { get; }
        public byte[] Bytes { get; }
        public long Length { get; }
        public long LastWriteUtcTicks { get; }
    }

    private sealed class MarketSecondApplyNoOpReceiptDraft
    {
        public string schemaVersion;
        public string executionCommand;
        public string executionBranch;
        public string sourceDigest;
        public int sourceInputCount;
        public string sourcePathListDigest;
        public string epochId;
        public string decisionAuthoritySha256;
        public string decisionPayloadDigest;
        public string decisionEpochDigest;
        public string sourceLedgerDigest;
        public string patchScopeDigest;
        public string v2ApplicationReceiptPath;
        public string v2ApplicationReceiptSha256;
        public long v2ApplicationReceiptByteLength;
        public string v2ReceiptScopeDigest;
        public string v2ReceiptDigest;
        public int applicationInvocationOrdinal;
        public int applicationInvocationCount;
        public int approvedPatchCount;
        public int applicationAssetCount;
        public int v2AssetCount;
        public int propertyCount;
        public int targetFileCount;
        public int differingPropertyCount;
        public int runtimeByteDifferenceCount;
        public int runtimeLengthDifferenceCount;
        public int runtimeMtimeDifferenceCount;
        public string assetSetDigest;
        public string propertySetDigest;
        public string receiptDigest;
        public MarketSecondApplyNoOpAssetData[] assets;
        public MarketSecondApplyNoOpPropertyData[] properties;
        public MarketSecondApplyNoOpFileData[] files;
    }

    [BalanceImmutableRecord]
    private sealed class MarketSecondApplyNoOpReceiptData
    {
        private MarketSecondApplyNoOpReceiptData(
            MarketSecondApplyNoOpReceiptDraft value)
        {
            schemaVersion = value.schemaVersion;
            executionCommand = value.executionCommand;
            executionBranch = value.executionBranch;
            sourceDigest = value.sourceDigest;
            sourceInputCount = value.sourceInputCount;
            sourcePathListDigest = value.sourcePathListDigest;
            epochId = value.epochId;
            decisionAuthoritySha256 = value.decisionAuthoritySha256;
            decisionPayloadDigest = value.decisionPayloadDigest;
            decisionEpochDigest = value.decisionEpochDigest;
            sourceLedgerDigest = value.sourceLedgerDigest;
            patchScopeDigest = value.patchScopeDigest;
            v2ApplicationReceiptPath = value.v2ApplicationReceiptPath;
            v2ApplicationReceiptSha256 = value.v2ApplicationReceiptSha256;
            v2ApplicationReceiptByteLength = value.v2ApplicationReceiptByteLength;
            v2ReceiptScopeDigest = value.v2ReceiptScopeDigest;
            v2ReceiptDigest = value.v2ReceiptDigest;
            applicationInvocationOrdinal = value.applicationInvocationOrdinal;
            applicationInvocationCount = value.applicationInvocationCount;
            approvedPatchCount = value.approvedPatchCount;
            applicationAssetCount = value.applicationAssetCount;
            v2AssetCount = value.v2AssetCount;
            propertyCount = value.propertyCount;
            targetFileCount = value.targetFileCount;
            differingPropertyCount = value.differingPropertyCount;
            runtimeByteDifferenceCount = value.runtimeByteDifferenceCount;
            runtimeLengthDifferenceCount = value.runtimeLengthDifferenceCount;
            runtimeMtimeDifferenceCount = value.runtimeMtimeDifferenceCount;
            assetSetDigest = value.assetSetDigest;
            propertySetDigest = value.propertySetDigest;
            receiptDigest = value.receiptDigest;
            assets = Array.AsReadOnly((value.assets
                ?? Array.Empty<MarketSecondApplyNoOpAssetData>()).ToArray());
            properties = Array.AsReadOnly((value.properties
                ?? Array.Empty<MarketSecondApplyNoOpPropertyData>()).ToArray());
            files = Array.AsReadOnly((value.files
                ?? Array.Empty<MarketSecondApplyNoOpFileData>()).ToArray());
        }

        public string schemaVersion { get; }
        public string executionCommand { get; }
        public string executionBranch { get; }
        public string sourceDigest { get; }
        public int sourceInputCount { get; }
        public string sourcePathListDigest { get; }
        public string epochId { get; }
        public string decisionAuthoritySha256 { get; }
        public string decisionPayloadDigest { get; }
        public string decisionEpochDigest { get; }
        public string sourceLedgerDigest { get; }
        public string patchScopeDigest { get; }
        public string v2ApplicationReceiptPath { get; }
        public string v2ApplicationReceiptSha256 { get; }
        public long v2ApplicationReceiptByteLength { get; }
        public string v2ReceiptScopeDigest { get; }
        public string v2ReceiptDigest { get; }
        public int applicationInvocationOrdinal { get; }
        public int applicationInvocationCount { get; }
        public int approvedPatchCount { get; }
        public int applicationAssetCount { get; }
        public int v2AssetCount { get; }
        public int propertyCount { get; }
        public int targetFileCount { get; }
        public int differingPropertyCount { get; }
        public int runtimeByteDifferenceCount { get; }
        public int runtimeLengthDifferenceCount { get; }
        public int runtimeMtimeDifferenceCount { get; }
        public string assetSetDigest { get; }
        public string propertySetDigest { get; }
        public string receiptDigest { get; }
        public IReadOnlyList<MarketSecondApplyNoOpAssetData> assets { get; }
        public IReadOnlyList<MarketSecondApplyNoOpPropertyData> properties { get; }
        public IReadOnlyList<MarketSecondApplyNoOpFileData> files { get; }

        [BalanceCaptureFactory]
        public static MarketSecondApplyNoOpReceiptData Capture(
            MarketSecondApplyNoOpReceiptDraft value) =>
            new MarketSecondApplyNoOpReceiptData(value
                ?? throw new ArgumentNullException(nameof(value)));
    }

    [BalanceImmutableRecord]
    private sealed class MarketSecondApplyNoOpAssetData
    {
        private MarketSecondApplyNoOpAssetData(
            string capturedSourceAuthority,
            string capturedExpectedAfterSha256,
            string capturedObservedSha256,
            long capturedByteLength)
        {
            sourceAuthority = capturedSourceAuthority;
            expectedAfterSha256 = capturedExpectedAfterSha256;
            observedSha256 = capturedObservedSha256;
            byteLength = capturedByteLength;
        }

        public string sourceAuthority { get; }
        public string expectedAfterSha256 { get; }
        public string observedSha256 { get; }
        public long byteLength { get; }

        [BalanceCaptureFactory]
        public static MarketSecondApplyNoOpAssetData Capture(
            string sourceAuthority,
            string expectedAfterSha256,
            string observedSha256,
            long byteLength) => new MarketSecondApplyNoOpAssetData(
                sourceAuthority,
                expectedAfterSha256,
                observedSha256,
                byteLength);
    }

    [BalanceImmutableRecord]
    private sealed class MarketSecondApplyNoOpPropertyData
    {
        private MarketSecondApplyNoOpPropertyData(
            string capturedRole,
            string capturedStableId,
            string capturedMetric,
            string capturedSourceAuthority,
            string capturedSourcePropertyPath,
            string capturedBefore,
            string capturedAfter,
            string capturedDependencyFingerprint,
            string capturedSourceDigest,
            string capturedSemanticHash)
        {
            role = capturedRole;
            stableId = capturedStableId;
            metric = capturedMetric;
            sourceAuthority = capturedSourceAuthority;
            sourcePropertyPath = capturedSourcePropertyPath;
            before = capturedBefore;
            after = capturedAfter;
            dependencyFingerprint = capturedDependencyFingerprint;
            sourceDigest = capturedSourceDigest;
            semanticHash = capturedSemanticHash;
        }

        public string role { get; }
        public string stableId { get; }
        public string metric { get; }
        public string sourceAuthority { get; }
        public string sourcePropertyPath { get; }
        public string before { get; }
        public string after { get; }
        public string dependencyFingerprint { get; }
        public string sourceDigest { get; }
        public string semanticHash { get; }

        [BalanceCaptureFactory]
        public static MarketSecondApplyNoOpPropertyData Capture(
            string role,
            string stableId,
            string metric,
            string sourceAuthority,
            string sourcePropertyPath,
            string before,
            string after,
            string dependencyFingerprint,
            string sourceDigest,
            string semanticHash) => new MarketSecondApplyNoOpPropertyData(
                role,
                stableId,
                metric,
                sourceAuthority,
                sourcePropertyPath,
                before,
                after,
                dependencyFingerprint,
                sourceDigest,
                semanticHash);
    }

    [BalanceImmutableRecord]
    private sealed class MarketSecondApplyNoOpFileData
    {
        private MarketSecondApplyNoOpFileData(
            string capturedPath,
            string capturedSha256,
            long capturedByteLength)
        {
            path = capturedPath;
            sha256 = capturedSha256;
            byteLength = capturedByteLength;
        }

        public string path { get; }
        public string sha256 { get; }
        public long byteLength { get; }

        [BalanceCaptureFactory]
        public static MarketSecondApplyNoOpFileData Capture(
            string path,
            string sha256,
            long byteLength) => new MarketSecondApplyNoOpFileData(
                path,
                sha256,
                byteLength);
    }
}
#endif
