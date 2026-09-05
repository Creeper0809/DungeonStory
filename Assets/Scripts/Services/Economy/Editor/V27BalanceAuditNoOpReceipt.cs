#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Executes the real AuditOnly generator twice and publishes a source-bound
/// receipt only when the second invocation attempted all six writes and every
/// writer returned Unchanged without touching the files.
/// </summary>
public static class V27BalanceAuditNoOpReceipt
{
    public const string ArtifactPath =
        "Artifacts/QA/v27-balance-audit-second-generation-noop.json";
    private const string SchemaVersion = "v27.audit-second-generation-noop.1";
    private const string ExecutionCommand =
        "DungeonStory/V27/Generate Audit-Only Twice And Verify No-Op";
    private const string ExecutionBranch = "audit-only-second-generation";

    private static readonly string[] GeneratedArtifactPaths =
    {
        V27BalanceCsvSerializer.ArtifactPath,
        V27BalanceAudit.MarkdownPath,
        V27BalanceJsonSerializer.AnomalyArtifactPath,
        V27BalanceAudit.AuditPath,
        V27BalanceAudit.SourceInventoryPath,
        V27BalanceAudit.ManifestPath
    };

    [MenuItem(ExecutionCommand)]
    public static void GenerateTwiceAndVerifyNoOp()
    {
        RunAuthorityState initialAuthority = CaptureRunAuthorityState();

        V27BalanceAuditOutput first = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        RequireIntegrity(first, "first");
        RequireSameRunAuthority(
            initialAuthority,
            CaptureRunAuthorityState(),
            "after-first");
        ArtifactIdentity[] beforeSecond = CaptureArtifactIdentities(
            first.WriteResult,
            includeSecondWriteResult: false);

        V27BalanceAuditOutput second = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        RequireIntegrity(second, "second");
        RunAuthorityState finalAuthority = CaptureRunAuthorityState();
        RequireSameRunAuthority(
            initialAuthority,
            finalAuthority,
            "after-second");
        ArtifactIdentity[] afterSecond = CaptureArtifactIdentities(
            second.WriteResult,
            includeSecondWriteResult: true);

        if (second.WriteResult.ChangedCount != 0)
        {
            throw new InvalidOperationException(
                "AUDIT_SECOND_GENERATION_WROTE_ARTIFACTS: changed="
                + second.WriteResult.ChangedCount.ToString(
                    CultureInfo.InvariantCulture));
        }

        int byteDiffCount = 0;
        int lengthDiffCount = 0;
        int runtimeMtimeDiffCount = 0;
        for (int index = 0; index < beforeSecond.Length; index++)
        {
            ArtifactIdentity before = beforeSecond[index];
            ArtifactIdentity after = afterSecond[index];
            if (!string.Equals(before.ProjectRelativePath,
                    after.ProjectRelativePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "AUDIT_NOOP_ARTIFACT_ORDER_DRIFT.");
            }
            if (!string.Equals(before.Sha256, after.Sha256,
                    StringComparison.Ordinal))
                byteDiffCount++;
            if (before.Length != after.Length)
                lengthDiffCount++;
            if (before.LastWriteUtcTicks != after.LastWriteUtcTicks)
                runtimeMtimeDiffCount++;
        }
        if (byteDiffCount != 0 || lengthDiffCount != 0
            || runtimeMtimeDiffCount != 0)
        {
            throw new InvalidOperationException(
                "AUDIT_SECOND_GENERATION_NOT_NOOP: byteDiff="
                + byteDiffCount.ToString(CultureInfo.InvariantCulture)
                + ";lengthDiff="
                + lengthDiffCount.ToString(CultureInfo.InvariantCulture)
                + ";mtimeDiff="
                + runtimeMtimeDiffCount.ToString(CultureInfo.InvariantCulture));
        }

        string firstSemanticDigest = ComputeArtifactIdentitySetDigest(beforeSecond);
        string secondSemanticDigest = ComputeArtifactIdentitySetDigest(afterSecond);
        if (!string.Equals(firstSemanticDigest, secondSemanticDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "AUDIT_SECOND_GENERATION_SEMANTIC_DRIFT.");
        }
        AuditNoOpArtifactData[] files = afterSecond.Select(value =>
            AuditNoOpArtifactData.Capture(
                value.ProjectRelativePath,
                value.Sha256,
                value.Length,
                value.SecondWriteChanged)).ToArray();
        AuditNoOpReceiptDraft draft = new()
        {
            schemaVersion = SchemaVersion,
            executionCommand = ExecutionCommand,
            executionBranch = ExecutionBranch,
            currentSourceDigest = finalAuthority.Source.Digest,
            currentSourceInputCount = finalAuthority.Source.InputCount,
            currentSourcePathDigest = finalAuthority.Source.PathListDigest,
            gameplaySceneSha256 = finalAuthority.SceneDigest,
            generatorVersion = second.ArtifactManifest.GeneratorVersion,
            ledgerSourceDigest = second.AuthoritySnapshot.SourceDigest,
            ledgerSourceCount = second.AuthoritySnapshot.SourceCount,
            approvalDigest = finalAuthority.ApprovalDigest,
            assetPatchDigest = second.AssetPatchDigest,
            marketSecondApplyReceiptPath = finalAuthority.MarketReceiptPath,
            marketSecondApplyReceiptSha256 = finalAuthority.MarketReceiptDigest,
            marketSecondApplyReceiptLength = finalAuthority.MarketReceiptLength,
            marketSecondApplySemanticDigest =
                finalAuthority.MarketReceiptSemanticDigest,
            rowCount = second.Ledger.Count,
            criticalCount = second.ArtifactManifest.CriticalCount,
            collapsedCriticalCount =
                second.ArtifactManifest.CollapsedCriticalCount,
            approvedCount = second.ArtifactManifest.ApprovedCount,
            sccCount = second.ArtifactManifest.SccCount,
            integrityFailureCount = second.ArtifactManifest.IntegrityFailureCount,
            firstSemanticDigest = firstSemanticDigest,
            secondSemanticDigest = secondSemanticDigest,
            firstWriterInvocationCount = first.WriteResult.InvocationCount,
            secondWriterInvocationCount = second.WriteResult.InvocationCount,
            secondChangedCount = second.WriteResult.ChangedCount,
            byteDiffCount = byteDiffCount,
            lengthDiffCount = lengthDiffCount,
            runtimeMtimeDiffCount = runtimeMtimeDiffCount,
            files = files
        };
        draft.artifactSetDigest = ComputeArtifactSetDigest(files);
        draft.executionEpochDigest = ComputeExecutionEpochDigest(draft);
        draft.receiptDigest = ComputeReceiptDigest(draft);
        AuditNoOpReceiptData receipt = AuditNoOpReceiptData.Capture(draft);
        V27BalanceArtifactWriter.WriteIfDifferent(
            ArtifactPath,
            stream => AuditNoOpReceiptSerializer.Write(stream, receipt));
        AssetDatabase.Refresh();
        Debug.Log(
            "V27 AuditOnly second-generation no-op PASS: rows="
            + receipt.rowCount.ToString(CultureInfo.InvariantCulture)
            + ";writes=6;changed=0;receipt=" + receipt.receiptDigest + ".");
    }

    private static void RequireIntegrity(
        V27BalanceAuditOutput output,
        string invocation)
    {
        if (output == null)
            throw new InvalidOperationException(
                "AUDIT_NOOP_OUTPUT_MISSING: " + invocation);
        if (output.WriteResult == null
            || output.WriteResult.InvocationCount != 6)
        {
            throw new InvalidOperationException(
                "AUDIT_NOOP_WRITE_CONTRACT_INVALID: " + invocation);
        }
        if (output.IntegrityFailures.Count != 0)
        {
            throw new InvalidOperationException(
                "AUDIT_NOOP_INTEGRITY_FAILURE: " + invocation + ":\n"
                + string.Join("\n", output.IntegrityFailures));
        }
    }

    private static ArtifactIdentity[] CaptureArtifactIdentities(
        V27BalanceAuditWriteResult writeResult,
        bool includeSecondWriteResult)
    {
        bool[] writes =
        {
            writeResult.CsvChanged,
            writeResult.MarkdownChanged,
            writeResult.AnomalyChanged,
            writeResult.AuditChanged,
            writeResult.SourceInventoryChanged,
            writeResult.ManifestChanged
        };
        if (writes.Length != GeneratedArtifactPaths.Length)
            throw new InvalidOperationException("AUDIT_NOOP_WRITE_COUNT_DRIFT.");
        ArtifactIdentity[] identities = new ArtifactIdentity[writes.Length];
        for (int index = 0; index < identities.Length; index++)
        {
            string path = GeneratedArtifactPaths[index];
            string absolute = ProjectAbsolutePath(path);
            if (!File.Exists(absolute))
            {
                throw new InvalidOperationException(
                    "AUDIT_NOOP_ARTIFACT_MISSING: " + path);
            }
            FileInfo info = new(absolute);
            identities[index] = new ArtifactIdentity(
                path,
                V27BalanceArtifactWriter.ComputeSha256(path),
                info.Length,
                info.LastWriteTimeUtc.Ticks,
                includeSecondWriteResult && writes[index]);
        }
        return identities;
    }

    private static string ComputeArtifactIdentitySetDigest(
        ArtifactIdentity[] identities)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(identities.Length);
        foreach (ArtifactIdentity identity in identities)
        {
            digest.Append(identity.ProjectRelativePath);
            digest.Append(identity.Sha256);
            digest.Append(identity.Length);
        }
        return digest.ComputeSha256();
    }

    private static string ComputeArtifactSetDigest(
        AuditNoOpArtifactData[] files)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(files.Length);
        foreach (AuditNoOpArtifactData file in files)
        {
            digest.Append(file.path);
            digest.Append(file.sha256);
            digest.Append(file.length);
            digest.Append(file.secondWriteChanged);
        }
        return digest.ComputeSha256();
    }

    private static string ComputeExecutionEpochDigest(AuditNoOpReceiptDraft value)
    {
        CanonicalSemanticDigestBuilder digest = new();
        AppendReceiptFields(digest, value);
        return digest.ComputeSha256();
    }

    private static string ComputeReceiptDigest(AuditNoOpReceiptDraft value)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("v27-audit-second-generation-noop-receipt");
        AppendReceiptFields(digest, value);
        digest.Append(value.executionEpochDigest);
        return digest.ComputeSha256();
    }

    private static void AppendReceiptFields(
        CanonicalSemanticDigestBuilder digest,
        AuditNoOpReceiptDraft value)
    {
        digest.Append(value.schemaVersion);
        digest.Append(value.executionCommand);
        digest.Append(value.executionBranch);
        digest.Append(value.currentSourceDigest);
        digest.Append(value.currentSourceInputCount);
        digest.Append(value.currentSourcePathDigest);
        digest.Append(value.gameplaySceneSha256);
        digest.Append(value.generatorVersion);
        digest.Append(value.ledgerSourceDigest);
        digest.Append(value.ledgerSourceCount);
        digest.Append(value.approvalDigest);
        digest.Append(value.assetPatchDigest);
        digest.Append(value.marketSecondApplyReceiptPath);
        digest.Append(value.marketSecondApplyReceiptSha256);
        digest.Append(value.marketSecondApplyReceiptLength);
        digest.Append(value.marketSecondApplySemanticDigest);
        digest.Append(value.rowCount);
        digest.Append(value.criticalCount);
        digest.Append(value.collapsedCriticalCount);
        digest.Append(value.approvedCount);
        digest.Append(value.sccCount);
        digest.Append(value.integrityFailureCount);
        digest.Append(value.firstSemanticDigest);
        digest.Append(value.secondSemanticDigest);
        digest.Append(value.firstWriterInvocationCount);
        digest.Append(value.secondWriterInvocationCount);
        digest.Append(value.secondChangedCount);
        digest.Append(value.byteDiffCount);
        digest.Append(value.lengthDiffCount);
        digest.Append(value.runtimeMtimeDiffCount);
        digest.Append(value.artifactSetDigest);
        digest.Append(value.files.Length);
        foreach (AuditNoOpArtifactData file in value.files)
        {
            digest.Append(file.path);
            digest.Append(file.sha256);
            digest.Append(file.length);
            digest.Append(file.secondWriteChanged);
        }
    }

    private static RunAuthorityState CaptureRunAuthorityState()
    {
        V27CurrentSourceEvidenceSnapshot source =
            V27CurrentSourceEvidenceDigest.Capture();
        string scene = V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest();
        if (!string.Equals(
                scene,
                V27CurrentSourceEvidenceDigest.OfficialGameplaySceneSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "AUDIT_NOOP_GAMEPLAY_SCENE_AUTHORITY_DRIFT: " + scene);
        }
        string marketReceiptPath =
            V27BalanceAssetApplication.MarketSecondApplyNoOpReceiptPath;
        if (!File.Exists(ProjectAbsolutePath(marketReceiptPath)))
        {
            throw new InvalidOperationException(
                "AUDIT_NOOP_MARKET_SECOND_APPLY_RECEIPT_MISSING: "
                + marketReceiptPath);
        }
        string marketReceiptText = V27StrictJsonGuard.ReadProjectRelative(
            marketReceiptPath);
        MarketSecondApplyReceiptHeader marketHeader =
            JsonUtility.FromJson<MarketSecondApplyReceiptHeader>(marketReceiptText)
            ?? throw new InvalidOperationException(
                "AUDIT_NOOP_MARKET_SECOND_APPLY_RECEIPT_INVALID_JSON.");
        if (!IsLowercaseSha256(marketHeader.receiptDigest))
        {
            throw new InvalidOperationException(
                "AUDIT_NOOP_MARKET_SECOND_APPLY_RECEIPT_DIGEST_INVALID.");
        }
        FileInfo marketReceiptInfo = new(ProjectAbsolutePath(marketReceiptPath));
        return new RunAuthorityState(
            source,
            scene,
            V27BalanceArtifactWriter.ComputeSha256(V27BalanceAudit.ApprovalPath),
            marketReceiptPath,
            V27BalanceArtifactWriter.ComputeSha256(marketReceiptPath),
            marketReceiptInfo.Length,
            marketHeader.receiptDigest);
    }

    private static void RequireSameRunAuthority(
        RunAuthorityState expected,
        RunAuthorityState actual,
        string phase)
    {
        if (!string.Equals(expected.Source.Digest, actual.Source.Digest,
                StringComparison.Ordinal)
            || expected.Source.InputCount != actual.Source.InputCount
            || !string.Equals(expected.Source.PathListDigest,
                actual.Source.PathListDigest, StringComparison.Ordinal)
            || !string.Equals(expected.SceneDigest, actual.SceneDigest,
                StringComparison.Ordinal)
            || !string.Equals(expected.ApprovalDigest, actual.ApprovalDigest,
                StringComparison.Ordinal)
            || !string.Equals(expected.MarketReceiptPath, actual.MarketReceiptPath,
                StringComparison.Ordinal)
            || !string.Equals(expected.MarketReceiptDigest,
                actual.MarketReceiptDigest, StringComparison.Ordinal)
            || expected.MarketReceiptLength != actual.MarketReceiptLength
            || !string.Equals(expected.MarketReceiptSemanticDigest,
                actual.MarketReceiptSemanticDigest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "AUDIT_NOOP_RUN_AUTHORITY_DRIFT: " + phase);
        }
    }

    [BalanceSerializationLayer]
    private static class AuditNoOpReceiptSerializer
    {
        public static void Write(Stream stream, AuditNoOpReceiptData value)
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
            WriteString(writer, "currentSourceDigest", value.currentSourceDigest);
            WriteInt(writer, "currentSourceInputCount", value.currentSourceInputCount);
            WriteString(writer, "currentSourcePathDigest", value.currentSourcePathDigest);
            WriteString(writer, "gameplaySceneSha256", value.gameplaySceneSha256);
            WriteString(writer, "generatorVersion", value.generatorVersion);
            WriteString(writer, "ledgerSourceDigest", value.ledgerSourceDigest);
            WriteInt(writer, "ledgerSourceCount", value.ledgerSourceCount);
            WriteString(writer, "approvalDigest", value.approvalDigest);
            WriteString(writer, "assetPatchDigest", value.assetPatchDigest);
            WriteString(writer, "marketSecondApplyReceiptPath",
                value.marketSecondApplyReceiptPath);
            WriteString(writer, "marketSecondApplyReceiptSha256",
                value.marketSecondApplyReceiptSha256);
            WriteLong(writer, "marketSecondApplyReceiptLength",
                value.marketSecondApplyReceiptLength);
            WriteString(writer, "marketSecondApplySemanticDigest",
                value.marketSecondApplySemanticDigest);
            WriteInt(writer, "rowCount", value.rowCount);
            WriteInt(writer, "criticalCount", value.criticalCount);
            WriteInt(writer, "collapsedCriticalCount", value.collapsedCriticalCount);
            WriteInt(writer, "approvedCount", value.approvedCount);
            WriteInt(writer, "sccCount", value.sccCount);
            WriteInt(writer, "integrityFailureCount", value.integrityFailureCount);
            WriteString(writer, "firstSemanticDigest", value.firstSemanticDigest);
            WriteString(writer, "secondSemanticDigest", value.secondSemanticDigest);
            WriteInt(writer, "firstWriterInvocationCount",
                value.firstWriterInvocationCount);
            WriteInt(writer, "secondWriterInvocationCount",
                value.secondWriterInvocationCount);
            WriteInt(writer, "secondChangedCount", value.secondChangedCount);
            WriteInt(writer, "byteDiffCount", value.byteDiffCount);
            WriteInt(writer, "lengthDiffCount", value.lengthDiffCount);
            WriteInt(writer, "runtimeMtimeDiffCount", value.runtimeMtimeDiffCount);
            WriteString(writer, "artifactSetDigest", value.artifactSetDigest);
            writer.Write("  \"files\": [");
            writer.Write('\n');
            for (int index = 0; index < value.files.Count; index++)
            {
                AuditNoOpArtifactData file = value.files[index];
                writer.Write("    {\"path\":");
                V27BalanceJsonSerializer.WriteJsonString(writer, file.path);
                writer.Write(",\"sha256\":");
                V27BalanceJsonSerializer.WriteJsonString(writer, file.sha256);
                writer.Write(",\"length\":");
                writer.Write(file.length);
                writer.Write(",\"secondWriteChanged\":");
                writer.Write(file.secondWriteChanged ? "true" : "false");
                writer.Write('}');
                if (index + 1 < value.files.Count)
                    writer.Write(',');
                writer.Write('\n');
            }
            writer.Write("  ],");
            writer.Write('\n');
            WriteString(writer, "executionEpochDigest", value.executionEpochDigest);
            WriteString(writer, "receiptDigest", value.receiptDigest, false);
            writer.Write('}');
            writer.Write('\n');
            writer.Flush();
        }

        private static void WriteString(
            StreamWriter writer,
            string name,
            string value,
            bool comma = true)
        {
            writer.Write("  ");
            V27BalanceJsonSerializer.WriteJsonString(writer, name);
            writer.Write(": ");
            V27BalanceJsonSerializer.WriteJsonString(writer, value);
            if (comma)
                writer.Write(',');
            writer.Write('\n');
        }

        private static void WriteInt(StreamWriter writer, string name, int value)
        {
            writer.Write("  ");
            V27BalanceJsonSerializer.WriteJsonString(writer, name);
            writer.Write(": ");
            writer.Write(value);
            writer.Write(',');
            writer.Write('\n');
        }

        private static void WriteLong(StreamWriter writer, string name, long value)
        {
            writer.Write("  ");
            V27BalanceJsonSerializer.WriteJsonString(writer, name);
            writer.Write(": ");
            writer.Write(value);
            writer.Write(',');
            writer.Write('\n');
        }
    }

    private static string ProjectAbsolutePath(string projectRelativePath)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        return Path.Combine(
            root,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool IsLowercaseSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character =>
            character >= '0' && character <= '9'
            || character >= 'a' && character <= 'f');

    private readonly struct ArtifactIdentity
    {
        public ArtifactIdentity(
            string projectRelativePath,
            string sha256,
            long length,
            long lastWriteUtcTicks,
            bool secondWriteChanged)
        {
            ProjectRelativePath = projectRelativePath;
            Sha256 = sha256;
            Length = length;
            LastWriteUtcTicks = lastWriteUtcTicks;
            SecondWriteChanged = secondWriteChanged;
        }

        public string ProjectRelativePath { get; }
        public string Sha256 { get; }
        public long Length { get; }
        public long LastWriteUtcTicks { get; }
        public bool SecondWriteChanged { get; }
    }

    private readonly struct RunAuthorityState
    {
        public RunAuthorityState(
            V27CurrentSourceEvidenceSnapshot source,
            string sceneDigest,
            string approvalDigest,
            string marketReceiptPath,
            string marketReceiptDigest,
            long marketReceiptLength,
            string marketReceiptSemanticDigest)
        {
            Source = source;
            SceneDigest = sceneDigest;
            ApprovalDigest = approvalDigest;
            MarketReceiptPath = marketReceiptPath;
            MarketReceiptDigest = marketReceiptDigest;
            MarketReceiptLength = marketReceiptLength;
            MarketReceiptSemanticDigest = marketReceiptSemanticDigest;
        }

        public V27CurrentSourceEvidenceSnapshot Source { get; }
        public string SceneDigest { get; }
        public string ApprovalDigest { get; }
        public string MarketReceiptPath { get; }
        public string MarketReceiptDigest { get; }
        public long MarketReceiptLength { get; }
        public string MarketReceiptSemanticDigest { get; }
    }

    private sealed class AuditNoOpReceiptDraft
    {
        public string schemaVersion;
        public string executionCommand;
        public string executionBranch;
        public string currentSourceDigest;
        public int currentSourceInputCount;
        public string currentSourcePathDigest;
        public string gameplaySceneSha256;
        public string generatorVersion;
        public string ledgerSourceDigest;
        public int ledgerSourceCount;
        public string approvalDigest;
        public string assetPatchDigest;
        public string marketSecondApplyReceiptPath;
        public string marketSecondApplyReceiptSha256;
        public long marketSecondApplyReceiptLength;
        public string marketSecondApplySemanticDigest;
        public int rowCount;
        public int criticalCount;
        public int collapsedCriticalCount;
        public int approvedCount;
        public int sccCount;
        public int integrityFailureCount;
        public string firstSemanticDigest;
        public string secondSemanticDigest;
        public int firstWriterInvocationCount;
        public int secondWriterInvocationCount;
        public int secondChangedCount;
        public int byteDiffCount;
        public int lengthDiffCount;
        public int runtimeMtimeDiffCount;
        public string artifactSetDigest;
        public AuditNoOpArtifactData[] files;
        public string executionEpochDigest;
        public string receiptDigest;
    }

    [BalanceImmutableRecord]
    private sealed class AuditNoOpReceiptData
    {
        private AuditNoOpReceiptData(AuditNoOpReceiptDraft value)
        {
            schemaVersion = value.schemaVersion;
            executionCommand = value.executionCommand;
            executionBranch = value.executionBranch;
            currentSourceDigest = value.currentSourceDigest;
            currentSourceInputCount = value.currentSourceInputCount;
            currentSourcePathDigest = value.currentSourcePathDigest;
            gameplaySceneSha256 = value.gameplaySceneSha256;
            generatorVersion = value.generatorVersion;
            ledgerSourceDigest = value.ledgerSourceDigest;
            ledgerSourceCount = value.ledgerSourceCount;
            approvalDigest = value.approvalDigest;
            assetPatchDigest = value.assetPatchDigest;
            marketSecondApplyReceiptPath = value.marketSecondApplyReceiptPath;
            marketSecondApplyReceiptSha256 = value.marketSecondApplyReceiptSha256;
            marketSecondApplyReceiptLength = value.marketSecondApplyReceiptLength;
            marketSecondApplySemanticDigest = value.marketSecondApplySemanticDigest;
            rowCount = value.rowCount;
            criticalCount = value.criticalCount;
            collapsedCriticalCount = value.collapsedCriticalCount;
            approvedCount = value.approvedCount;
            sccCount = value.sccCount;
            integrityFailureCount = value.integrityFailureCount;
            firstSemanticDigest = value.firstSemanticDigest;
            secondSemanticDigest = value.secondSemanticDigest;
            firstWriterInvocationCount = value.firstWriterInvocationCount;
            secondWriterInvocationCount = value.secondWriterInvocationCount;
            secondChangedCount = value.secondChangedCount;
            byteDiffCount = value.byteDiffCount;
            lengthDiffCount = value.lengthDiffCount;
            runtimeMtimeDiffCount = value.runtimeMtimeDiffCount;
            artifactSetDigest = value.artifactSetDigest;
            files = Array.AsReadOnly((value.files ?? Array.Empty<AuditNoOpArtifactData>())
                .ToArray());
            executionEpochDigest = value.executionEpochDigest;
            receiptDigest = value.receiptDigest;
        }

        public string schemaVersion { get; }
        public string executionCommand { get; }
        public string executionBranch { get; }
        public string currentSourceDigest { get; }
        public int currentSourceInputCount { get; }
        public string currentSourcePathDigest { get; }
        public string gameplaySceneSha256 { get; }
        public string generatorVersion { get; }
        public string ledgerSourceDigest { get; }
        public int ledgerSourceCount { get; }
        public string approvalDigest { get; }
        public string assetPatchDigest { get; }
        public string marketSecondApplyReceiptPath { get; }
        public string marketSecondApplyReceiptSha256 { get; }
        public long marketSecondApplyReceiptLength { get; }
        public string marketSecondApplySemanticDigest { get; }
        public int rowCount { get; }
        public int criticalCount { get; }
        public int collapsedCriticalCount { get; }
        public int approvedCount { get; }
        public int sccCount { get; }
        public int integrityFailureCount { get; }
        public string firstSemanticDigest { get; }
        public string secondSemanticDigest { get; }
        public int firstWriterInvocationCount { get; }
        public int secondWriterInvocationCount { get; }
        public int secondChangedCount { get; }
        public int byteDiffCount { get; }
        public int lengthDiffCount { get; }
        public int runtimeMtimeDiffCount { get; }
        public string artifactSetDigest { get; }
        public IReadOnlyList<AuditNoOpArtifactData> files { get; }
        public string executionEpochDigest { get; }
        public string receiptDigest { get; }

        [BalanceCaptureFactory]
        public static AuditNoOpReceiptData Capture(AuditNoOpReceiptDraft value) =>
            new AuditNoOpReceiptData(value
                ?? throw new ArgumentNullException(nameof(value)));
    }

    [BalanceImmutableRecord]
    private sealed class AuditNoOpArtifactData
    {
        private AuditNoOpArtifactData(
            string capturedPath,
            string capturedSha256,
            long capturedLength,
            bool capturedSecondWriteChanged)
        {
            path = capturedPath;
            sha256 = capturedSha256;
            length = capturedLength;
            secondWriteChanged = capturedSecondWriteChanged;
        }

        public string path { get; }
        public string sha256 { get; }
        public long length { get; }
        public bool secondWriteChanged { get; }

        [BalanceCaptureFactory]
        public static AuditNoOpArtifactData Capture(
            string path,
            string sha256,
            long length,
            bool secondWriteChanged) => new AuditNoOpArtifactData(
                path,
                sha256,
                length,
                secondWriteChanged);
    }

    [Serializable]
    private sealed class MarketSecondApplyReceiptHeader
    {
        public string receiptDigest;
    }
}
#endif
