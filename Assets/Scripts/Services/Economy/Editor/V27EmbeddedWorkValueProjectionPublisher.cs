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

/// <summary>
/// Publishes the runtime EWU projection only from one completed, current V27
/// audit. This does not calculate economic values or refresh approvals.
/// </summary>
public static class V27EmbeddedWorkValueProjectionPublisher
{
    public const string ResourceAssetPath =
        "Assets/Resources/Balance/v27-embedded-work-values.json";
    public const string SettlementBasisId =
        "balance:wim:053:committed-expedition-settlement";
    private const string WholeGameBalanceBaselinePath =
        "docs/game-design/whole-game-balance-baseline.md";

    private static readonly UTF8Encoding Utf8NoBom = new(false, true);

    [MenuItem("DungeonStory/WIM/053/Publish Approved Expedition EWU Projection")]
    public static void PublishFromMenu()
    {
        V27BalanceAuditOutput completed = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        PublishFromCompletedAudit(completed);
        Debug.Log(
            "WIM053 approved expedition EWU projection published from the current "
            + "V27 audit.");
    }

    /// <summary>
    /// Publishes only an audit result that still matches the current V27
    /// manifest, source inventory, ledger bytes, and exact approvals.
    /// </summary>
    public static void PublishFromCompletedAudit(V27BalanceAuditOutput completed)
    {
        ValidateCompletedAudit(completed);
        V27EmbeddedWorkValueProjectionPayload payload = BuildPayload(completed);
        string json = SerializeCanonicalPayload(payload);
        VerifyReaderRoundTrip(json, payload);
        byte[] bytes = Utf8NoBom.GetBytes(json);

        V27BalanceArtifactWriter.WriteIfDifferent(
            ResourceAssetPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        AssetDatabase.ImportAsset(
            ResourceAssetPath,
            ImportAssetOptions.ForceSynchronousImport
                | ImportAssetOptions.ForceUpdate);
        if (V27BalanceArtifactWriter.WriteIfDifferent(
                ResourceAssetPath,
                stream => stream.Write(bytes, 0, bytes.Length)))
        {
            throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_SECOND_WRITE_DIFF");
        }
    }

    internal static string SerializeCanonicalPayload(
        V27EmbeddedWorkValueProjectionPayload payload)
    {
        if (payload == null)
            throw new ArgumentNullException(nameof(payload));
        using MemoryStream stream = new();
        using (StreamWriter writer = new(
                   stream,
                   Utf8NoBom,
                   4096,
                   leaveOpen: true) { NewLine = "\n" })
        {
            writer.Write("{\n  \"schema\":");
            V27BalanceJsonSerializer.WriteJsonString(writer, payload.schema);
            writer.Write(",\n  \"generatorVersion\":");
            V27BalanceJsonSerializer.WriteJsonString(writer, payload.generatorVersion);
            writer.Write(",\n  \"ledgerCsvSha256\":");
            V27BalanceJsonSerializer.WriteJsonString(writer, payload.ledgerCsvSha256);
            writer.Write(",\n  \"sourceDigest\":");
            V27BalanceJsonSerializer.WriteJsonString(writer, payload.sourceDigest);
            writer.Write(",\n  \"basisId\":");
            V27BalanceJsonSerializer.WriteJsonString(writer, payload.basisId);
            writer.Write(",\n  \"itemCount\":");
            writer.Write(payload.itemCount.ToString(CultureInfo.InvariantCulture));
            writer.Write(",\n  \"items\":[\n");
            for (int index = 0; index < payload.items.Count; index++)
            {
                V27EmbeddedWorkValueProjectionRow row = payload.items[index]
                    ?? throw new InvalidOperationException(
                        "WIM053_EWU_PROJECTION_NULL_ROW");
                writer.Write("    {\"itemId\":");
                V27BalanceJsonSerializer.WriteJsonString(writer, row.itemId);
                writer.Write(",\"acquisitionMilliEwu\":");
                writer.Write(row.acquisitionMilliEwu.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"recoverableMilliEwu\":");
                writer.Write(row.recoverableMilliEwu.ToString(CultureInfo.InvariantCulture));
                writer.Write(",\"selectedSourceId\":");
                V27BalanceJsonSerializer.WriteJsonString(writer, row.selectedSourceId);
                writer.Write(index + 1 < payload.items.Count ? "},\n" : "}\n");
            }
            writer.Write("  ]\n}\n");
            writer.Flush();
        }
        return Utf8NoBom.GetString(stream.ToArray());
    }

    private static void ValidateCompletedAudit(V27BalanceAuditOutput completed)
    {
        if (completed == null)
            throw new ArgumentNullException(nameof(completed));
        if (completed.Ledger.Count == 0
            || completed.AuthoritySnapshot.SourceCount <= 0
            || completed.WriteResult.InvocationCount != 6)
        {
            throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_AUDIT_INCOMPLETE");
        }
        if (completed.IntegrityFailures.Count != 0
            || completed.ArtifactManifest.IntegrityFailureCount != 0
            || completed.CriticalCount != 0)
        {
            throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_AUDIT_NOT_APPROVED");
        }

        string[] approvalKeys = V27BalanceAssetApplication.CaptureValidApprovalKeys(
            completed.Ledger);
        string currentAssetPatchDigest =
            V27BalanceAssetApplication.CaptureApprovedPatchDigest(completed.Ledger);
        if (approvalKeys.Length != completed.ArtifactManifest.ApprovedCount
            || !string.Equals(
                currentAssetPatchDigest,
                completed.AssetPatchDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_APPROVAL_DRIFT");
        }

        V27AuditManifestData manifest = ReadCurrentManifest();
        string currentSourceDigest = ValidateCurrentSourceInventory(
            completed.AuthoritySnapshot.SourceCount);
        string currentCsvHash = V27BalanceArtifactWriter.ComputeSha256(
            V27BalanceCsvSerializer.ArtifactPath);
        string currentSourceInventoryHash = V27BalanceArtifactWriter.ComputeSha256(
            V27BalanceAudit.SourceInventoryPath);
        string currentApprovalHash = V27BalanceArtifactWriter.ComputeSha256(
            V27BalanceAudit.ApprovalPath);
        if (!string.Equals(manifest.schemaVersion, completed.ArtifactManifest.SchemaVersion,
                StringComparison.Ordinal)
            || !string.Equals(manifest.generatorVersion,
                V27EmbeddedWorkValueProjectionCodec.ApprovedGeneratorVersion,
                StringComparison.Ordinal)
            || !string.Equals(manifest.generatorVersion,
                completed.ArtifactManifest.GeneratorVersion,
                StringComparison.Ordinal)
            || !string.Equals(manifest.sourceDigest,
                completed.AuthoritySnapshot.SourceDigest,
                StringComparison.Ordinal)
            || !string.Equals(manifest.sourceDigest, currentSourceDigest,
                StringComparison.Ordinal)
            || !string.Equals(manifest.sourceInventoryByteHash,
                currentSourceInventoryHash,
                StringComparison.Ordinal)
            || manifest.sourceCount != completed.AuthoritySnapshot.SourceCount
            || manifest.rowCount != completed.Ledger.Count
            || !string.Equals(manifest.csvByteHash, currentCsvHash,
                StringComparison.Ordinal)
            || !string.Equals(manifest.approvalDigest, currentApprovalHash,
                StringComparison.Ordinal)
            || !string.Equals(manifest.assetPatchDigest, currentAssetPatchDigest,
                StringComparison.Ordinal)
            || manifest.criticalCount != completed.ArtifactManifest.CriticalCount
            || manifest.collapsedCriticalCount
                != completed.ArtifactManifest.CollapsedCriticalCount
            || manifest.approvedCount != completed.ArtifactManifest.ApprovedCount
            || manifest.sccCount != completed.ArtifactManifest.SccCount
            || manifest.integrityFailureCount
                != completed.ArtifactManifest.IntegrityFailureCount
            || manifest.balanceBaselineRecordIds == null
            || !manifest.balanceBaselineRecordIds.Contains(
                V27BalanceAudit.BaselineRecordId,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_MANIFEST_OR_SOURCE_DRIFT");
        }
    }

    private static V27EmbeddedWorkValueProjectionPayload BuildPayload(
        V27BalanceAuditOutput completed)
    {
        Dictionary<string, ItemMetricPair> pairs = new(StringComparer.Ordinal);
        foreach (CanonicalBalanceMetricRecord record in completed.Ledger.Records)
        {
            if (!string.Equals(record.Domain, "items", StringComparison.Ordinal)
                || !string.Equals(record.DefinitionKind, "item", StringComparison.Ordinal))
            {
                continue;
            }

            string itemId = BalanceCanonicalText.StableId(
                record.StableId,
                "WIM053 EWU projection item id");
            if (!pairs.TryGetValue(itemId, out ItemMetricPair pair))
            {
                pair = new ItemMetricPair(itemId);
                pairs.Add(itemId, pair);
            }
            if (string.Equals(record.Metric, "acquisition-cost", StringComparison.Ordinal))
                pair.SetAcquisition(record);
            else if (string.Equals(record.Metric, "recoverable-value", StringComparison.Ordinal))
                pair.SetRecoverable(record);
        }

        if (pairs.Count == 0)
        {
            throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_ITEM_SCOPE_EMPTY");
        }

        V27EmbeddedWorkValueProjectionRow[] rows = pairs.Values
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .Select(value => value.ToRow())
            .ToArray();
        return new V27EmbeddedWorkValueProjectionPayload
        {
            schema = V27EmbeddedWorkValueProjectionCodec.Schema,
            generatorVersion = V27EmbeddedWorkValueProjectionCodec.ApprovedGeneratorVersion,
            ledgerCsvSha256 = V27BalanceArtifactWriter.ComputeSha256(
                V27BalanceCsvSerializer.ArtifactPath),
            sourceDigest = completed.AuthoritySnapshot.SourceDigest,
            basisId = SettlementBasisId,
            itemCount = rows.Length,
            items = rows.ToList()
        };
    }

    private static void VerifyReaderRoundTrip(
        string json,
        V27EmbeddedWorkValueProjectionPayload expected)
    {
        IReadOnlyDictionary<string, V27EmbeddedWorkValueProjection> actual =
            V27EmbeddedWorkValueProjectionCodec.Parse(json, out string basisId);
        if (!string.Equals(basisId, expected.basisId, StringComparison.Ordinal)
            || actual.Count != expected.itemCount)
        {
            throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_READER_HEADER_DRIFT");
        }
        foreach (V27EmbeddedWorkValueProjectionRow row in expected.items)
        {
            if (!actual.TryGetValue(row.itemId, out V27EmbeddedWorkValueProjection value)
                || value.AcquisitionMilliEwu != row.acquisitionMilliEwu
                || value.RecoverableMilliEwu != row.recoverableMilliEwu
                || !string.Equals(value.BasisId, expected.basisId, StringComparison.Ordinal)
                || !string.Equals(value.SelectedSourceId, row.selectedSourceId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "WIM053_EWU_PROJECTION_READER_ROW_DRIFT: " + row.itemId);
            }
        }
    }

    private static V27AuditManifestData ReadCurrentManifest()
    {
        V27AuditManifestData manifest = JsonUtility.FromJson<V27AuditManifestData>(
                V27StrictJsonGuard.ReadProjectRelative(V27BalanceAudit.ManifestPath))
            ?? throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_MANIFEST_INVALID");
        return manifest;
    }

    private static string ValidateCurrentSourceInventory(int expectedSourceCount)
    {
        V27SourceInventoryData inventory = JsonUtility.FromJson<V27SourceInventoryData>(
                V27StrictJsonGuard.ReadProjectRelative(
                    V27BalanceAudit.SourceInventoryPath))
            ?? throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_SOURCE_INVENTORY_INVALID");
        if (!string.Equals(inventory.schemaVersion, "v27.source.v2",
                StringComparison.Ordinal)
            || inventory.entries == null
            || inventory.entries.Length != expectedSourceCount)
        {
            throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_SOURCE_INVENTORY_SCOPE");
        }

        V27SourceInventoryEntry[] ordered = inventory.entries
            .OrderBy(value => value?.path, StringComparer.Ordinal)
            .ToArray();
        string previousPath = string.Empty;
        bool includesWholeGameBalanceBaseline = false;
        foreach (V27SourceInventoryEntry entry in ordered)
        {
            string path = entry?.path ?? string.Empty;
            if (entry == null
                || string.CompareOrdinal(previousPath, path) >= 0
                || !string.Equals(
                    path,
                    BalanceCanonicalText.ProjectRelativePath(path),
                    StringComparison.Ordinal)
                || !string.Equals(
                    entry.sha256,
                    ComputeCanonicalSourceFileSha256(path),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "WIM053_EWU_PROJECTION_SOURCE_INVENTORY_DRIFT: " + path);
            }
            includesWholeGameBalanceBaseline |= string.Equals(
                path,
                WholeGameBalanceBaselinePath,
                StringComparison.Ordinal);
            previousPath = path;
        }
        if (!includesWholeGameBalanceBaseline)
        {
            throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_SETTLEMENT_BASELINE_NOT_IN_SOURCE_INVENTORY");
        }
        string baseline = File.ReadAllText(
            ProjectAbsolutePath(WholeGameBalanceBaselinePath),
            Utf8NoBom);
        if (baseline.IndexOf(
                "기록 ID: `" + SettlementBasisId + "`",
                StringComparison.Ordinal) < 0)
        {
            throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_SETTLEMENT_BASELINE_RECORD_MISSING");
        }
        return ComputeAggregateSourceDigest(ordered);
    }

    private static string ComputeCanonicalSourceFileSha256(string projectRelativePath)
    {
        string canonical = BalanceCanonicalText.ProjectRelativePath(projectRelativePath);
        byte[] bytes = File.ReadAllBytes(ProjectAbsolutePath(canonical));
        using MemoryStream normalized = new(bytes.Length);
        for (int index = 0; index < bytes.Length; index++)
        {
            byte value = bytes[index];
            if (value != (byte)'\r')
            {
                normalized.WriteByte(value);
                continue;
            }
            if (index + 1 < bytes.Length && bytes[index + 1] == (byte)'\n')
                index++;
            normalized.WriteByte((byte)'\n');
        }
        normalized.Position = 0L;
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(normalized));
    }

    private static string ProjectAbsolutePath(string projectRelativePath)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string canonical = BalanceCanonicalText.ProjectRelativePath(projectRelativePath);
        string absolute = Path.GetFullPath(Path.Combine(
            root,
            canonical.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "WIM053_EWU_PROJECTION_SOURCE_PATH_ESCAPED: " + canonical);
        }
        return absolute;
    }

    private static string ComputeAggregateSourceDigest(
        IReadOnlyList<V27SourceInventoryEntry> entries)
    {
        using SHA256 sha = SHA256.Create();
        using MemoryStream stream = new();
        using (StreamWriter writer = new(
                   stream,
                   Utf8NoBom,
                   4096,
                   leaveOpen: true) { NewLine = "\n" })
        {
            foreach (V27SourceInventoryEntry entry in entries)
            {
                writer.Write(entry.path);
                writer.Write('=');
                writer.Write(entry.sha256);
                writer.Write('\n');
            }
            writer.Flush();
        }
        stream.Position = 0L;
        return Hex(sha.ComputeHash(stream));
    }

    private static string Hex(byte[] bytes)
    {
        const string Digits = "0123456789abcdef";
        char[] output = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            output[index * 2] = Digits[bytes[index] >> 4];
            output[index * 2 + 1] = Digits[bytes[index] & 0xf];
        }
        return new string(output);
    }

    private static bool TryParseCanonicalMilliEwu(string value, out long milliEwu)
    {
        milliEwu = 0L;
        return value != null
            && long.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out milliEwu)
            && milliEwu >= 0L
            && string.Equals(
                milliEwu.ToString(CultureInfo.InvariantCulture),
                value,
                StringComparison.Ordinal);
    }

    private sealed class ItemMetricPair
    {
        private long acquisitionMilliEwu;
        private long recoverableMilliEwu;
        private string acquisitionSourceId;
        private string recoverableSourceId;
        private bool hasAcquisition;
        private bool hasRecoverable;

        public ItemMetricPair(string itemId)
        {
            ItemId = itemId;
        }

        public string ItemId { get; }

        public void SetAcquisition(CanonicalBalanceMetricRecord record)
        {
            if (hasAcquisition
                || !string.Equals(record.Unit, "mEWU", StringComparison.Ordinal)
                || !TryParseCanonicalMilliEwu(record.After, out acquisitionMilliEwu))
            {
                throw new InvalidOperationException(
                    "WIM053_EWU_PROJECTION_ACQUISITION_INVALID: " + ItemId);
            }
            acquisitionSourceId = BalanceCanonicalText.StableId(
                record.ExecutionRoute,
                "WIM053 EWU projection acquisition source");
            hasAcquisition = true;
        }

        public void SetRecoverable(CanonicalBalanceMetricRecord record)
        {
            if (hasRecoverable
                || !string.Equals(record.Unit, "mEWU", StringComparison.Ordinal)
                || !TryParseCanonicalMilliEwu(record.After, out recoverableMilliEwu))
            {
                throw new InvalidOperationException(
                    "WIM053_EWU_PROJECTION_RECOVERABLE_INVALID: " + ItemId);
            }
            recoverableSourceId = BalanceCanonicalText.StableId(
                record.ExecutionRoute,
                "WIM053 EWU projection recoverable source");
            hasRecoverable = true;
        }

        public V27EmbeddedWorkValueProjectionRow ToRow()
        {
            if (!hasAcquisition
                || !hasRecoverable
                || !string.Equals(acquisitionSourceId, recoverableSourceId,
                    StringComparison.Ordinal)
                || recoverableMilliEwu > acquisitionMilliEwu)
            {
                throw new InvalidOperationException(
                    "WIM053_EWU_PROJECTION_ITEM_PAIR_INVALID: " + ItemId);
            }
            return new V27EmbeddedWorkValueProjectionRow
            {
                itemId = ItemId,
                acquisitionMilliEwu = acquisitionMilliEwu,
                recoverableMilliEwu = recoverableMilliEwu,
                selectedSourceId = acquisitionSourceId
            };
        }
    }

    [Serializable]
    private sealed class V27AuditManifestData
    {
        public string schemaVersion = string.Empty;
        public string generatorVersion = string.Empty;
        public string sourceDigest = string.Empty;
        public string sourceInventoryByteHash = string.Empty;
        public int sourceCount;
        public int rowCount;
        public string csvByteHash = string.Empty;
        public string approvalDigest = string.Empty;
        public string assetPatchDigest = string.Empty;
        public int criticalCount;
        public int collapsedCriticalCount;
        public int approvedCount;
        public int sccCount;
        public int integrityFailureCount;
        public string[] balanceBaselineRecordIds = Array.Empty<string>();
    }

    [Serializable]
    private sealed class V27SourceInventoryData
    {
        public string schemaVersion = string.Empty;
        public V27SourceInventoryEntry[] entries = Array.Empty<V27SourceInventoryEntry>();
    }

    [Serializable]
    private sealed class V27SourceInventoryEntry
    {
        public string path = string.Empty;
        public string sha256 = string.Empty;
    }
}
#endif
