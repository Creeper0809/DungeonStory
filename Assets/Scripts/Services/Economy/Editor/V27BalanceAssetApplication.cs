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

[BalanceCaptureFactory]
public static class V27BalanceAssetApplication
{
    private static readonly HashSet<string> ItemMarketApprovalMetrics = new(
        StringComparer.Ordinal)
    {
        "authored-unit-price-gold",
        "authored-market-sale-rate",
        "authored-retail-cost-gold",
        "authored-daily-unit-cost-gold",
        "authored-money-reward-gold"
    };
    private static readonly HashSet<string> LaborFacilityApprovalMetrics = new(
        StringComparer.Ordinal)
    {
        "authored-required-wu",
        "authored-sow-wu",
        "authored-harvest-wu",
        "authored-research-required-wu",
        "construction-authored-wu:period-preserving"
    };
    private static readonly HashSet<string> CombatEncounterApprovalMetrics = new(
        StringComparer.Ordinal)
    {
        "enemy-health-multiplier",
        "enemy-damage-multiplier",
        "enemy-accuracy-multiplier",
        "objective-health-multiplier",
        "objective-control-resistance-multiplier",
        "additional-enemy-count",
        "objective-round-limit"
    };

    [MenuItem("DungeonStory/V27/Apply Approved Balance Patches")]
    public static void ApplyApprovedFromMenu()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(BalanceLedgerExecutionMode.AuditOnly);
        BalanceAssetApplicationResult result = ApplyApproved(audit.Ledger, dryRun: false);
        Debug.Log(result.Format("ApplyApproved"));
    }

    [MenuItem("DungeonStory/V27/Verify Applied Balance Patches")]
    public static void VerifyAppliedFromMenu()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(BalanceLedgerExecutionMode.AuditOnly);
        BalanceAssetApplicationResult result = ApplyApproved(audit.Ledger, dryRun: true);
        Debug.Log(result.Format("VerifyApplied"));
    }

    [MenuItem("DungeonStory/V27/Generate Exact Item Market Approvals")]
    public static void GenerateItemMarketApprovalsFromMenu()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        if (audit.IntegrityFailures.Count > 0 || audit.CriticalCount > 0)
        {
            throw new InvalidOperationException(
                "Cannot generate market approvals from a failing V27 audit.");
        }
        int count = WriteApprovals(
            audit.Ledger,
            record => ItemMarketApprovalMetrics.Contains(record.Metric));
        V27BalanceAuditOutput verified = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        if (verified.IntegrityFailures.Count > 0 || verified.CriticalCount > 0)
        {
            throw new InvalidOperationException(
                "Generated market approvals did not survive exact V27 revalidation.");
        }
        Debug.Log($"V27 exact item-market approvals generated: approvals={count}.");
    }

    [MenuItem("DungeonStory/V27/Generate Exact Labor and Facility Approvals")]
    public static void GenerateLaborFacilityApprovalsFromMenu()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.GenerateForApprovalRefresh();
        V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
            audit,
            requireApplied: false);
        int count = WriteApprovals(
            audit.Ledger,
            record => ItemMarketApprovalMetrics.Contains(record.Metric)
                || LaborFacilityApprovalMetrics.Contains(record.Metric),
            replaceIncludedApprovals: true);
        V27BalanceAuditOutput verified = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
            verified,
            requireApplied: false);
        Debug.Log($"V27 exact labor/facility approvals generated: approvals={count}.");
    }

    [MenuItem("DungeonStory/V27/Generate Exact Combat Encounter Approvals")]
    public static void GenerateCombatEncounterApprovalsFromMenu()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.GenerateForApprovalRefresh();
        if (audit.IntegrityFailures.Count > 0)
        {
            throw new InvalidOperationException(
                "Cannot generate combat approvals from a failing V27 audit:\n"
                + string.Join("\n", audit.IntegrityFailures));
        }
        RequireFinalCombatCheckpointEvidence();
        int count = WriteApprovals(
            audit.Ledger,
            record => ItemMarketApprovalMetrics.Contains(record.Metric)
                || LaborFacilityApprovalMetrics.Contains(record.Metric)
                || CombatEncounterApprovalMetrics.Contains(record.Metric),
            replaceIncludedApprovals: true);
        V27BalanceAuditOutput verified = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        if (verified.IntegrityFailures.Count > 0 || verified.CriticalCount > 0)
        {
            throw new InvalidOperationException(
                "Generated combat approvals did not survive exact V27 revalidation.");
        }
        Debug.Log($"V27 exact combat encounter approvals generated: approvals={count}.");
    }

    private static void RequireFinalCombatCheckpointEvidence()
    {
        foreach (CombatEncounterCalibration value in
                 CombatBalanceCheckpointAuthority.AllEncounters)
        {
            string path = Path.Combine(
                CombatOutcomeBalanceCalibrationScenario.FinalCheckpointDirectory,
                $"encounter-{value.EncounterNumber:00}.txt");
            if (!File.Exists(path)
                || File.ReadLines(path).FirstOrDefault()?.StartsWith(
                    "RESULT=PASS; samples=1000; failures=0; stalled=0",
                    StringComparison.Ordinal) != true)
            {
                throw new InvalidOperationException(
                    $"Missing accepted 1,000-seed combat checkpoint: {path}.");
            }
        }
    }

    public static BalanceAssetApplicationResult ApplyApproved(
        FrozenBalanceLedger ledger,
        bool dryRun)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        BalanceApprovalFileData approvalFile = LoadApprovals();
        Dictionary<string, BalanceApprovalEntryData> approvals = ValidateApprovals(approvalFile);
        List<BalanceAssetPatch> patches = CreatePatches(ledger, approvals);
        return ApplyPatches(
            patches,
            dryRun,
            requireCleanGit: true,
            BalanceAssetApplicationFailurePoint.None);
    }

    internal static BalanceAssetApplicationResult ApplyPatchesForDiagnostics(
        IReadOnlyList<BalanceAssetPatch> patches,
        BalanceAssetApplicationFailurePoint failurePoint)
    {
        return ApplyPatches(
            patches,
            dryRun: false,
            requireCleanGit: false,
            failurePoint);
    }

    private static BalanceAssetApplicationResult ApplyPatches(
        IReadOnlyList<BalanceAssetPatch> patches,
        bool dryRun,
        bool requireCleanGit,
        BalanceAssetApplicationFailurePoint failurePoint)
    {
        if (patches == null)
            throw new ArgumentNullException(nameof(patches));
        if (patches.Count == 0)
            return new BalanceAssetApplicationResult(0, 0, 0, true);

        string[] paths = patches.Select(value => value.AssetPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (requireCleanGit)
        {
            foreach (string path in paths)
            {
                string dirty = CaptureGitDirty(path);
                if (dirty.Length != 0)
                    throw new InvalidOperationException(
                        $"Refusing V27 asset application over an existing dirty file: {path} ({dirty})");
            }
        }

        Dictionary<string, AssetIdentitySnapshot> before = paths.ToDictionary(
            value => value,
            CaptureIdentity,
            StringComparer.Ordinal);
        int differing = 0;
        foreach (BalanceAssetPatch patch in patches)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(patch.AssetPath)
                ?? throw new InvalidOperationException($"Approved asset is missing: {patch.AssetPath}");
            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(patch.PropertyPath)
                ?? throw new InvalidOperationException(
                    $"Approved property is missing: {patch.AssetPath}:{patch.PropertyPath}");
            string current = CaptureToken(property);
            if (!TokenMatchesProperty(property, patch.Before))
            {
                throw new InvalidOperationException(
                    $"Stale approved patch {patch.AssetPath}:{patch.PropertyPath}; "
                    + $"ledger Before={patch.Before}, authority={current}.");
            }
            if (!TokenMatchesProperty(property, patch.After))
                differing++;
        }
        if (dryRun)
            return new BalanceAssetApplicationResult(patches.Count, paths.Length, differing, true);
        if (differing == 0)
            return new BalanceAssetApplicationResult(patches.Count, paths.Length, 0, true);

        Dictionary<string, byte[]> rollbackBytes = paths.ToDictionary(
            value => value,
            value => File.ReadAllBytes(ProjectAbsolutePath(value)),
            StringComparer.Ordinal);
        List<string> changedPaths = new List<string>();
        try
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (IGrouping<string, BalanceAssetPatch> group in patches
                             .GroupBy(value => value.AssetPath, StringComparer.Ordinal)
                             .OrderBy(value => value.Key, StringComparer.Ordinal))
                {
                    UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(group.Key)
                        ?? throw new InvalidOperationException($"Approved asset disappeared: {group.Key}");
                    SerializedObject serialized = new SerializedObject(asset);
                    bool changed = false;
                    foreach (BalanceAssetPatch patch in group
                                 .OrderBy(value => value.PropertyPath, StringComparer.Ordinal))
                    {
                        SerializedProperty property = serialized.FindProperty(patch.PropertyPath)
                            ?? throw new InvalidOperationException(
                                $"Approved property disappeared: {group.Key}:{patch.PropertyPath}");
                        string current = CaptureToken(property);
                        if (TokenMatchesProperty(property, patch.After))
                            continue;
                        if (!TokenMatchesProperty(property, patch.Before))
                            throw new InvalidOperationException(
                                $"Patch authority changed during application: {group.Key}:{patch.PropertyPath}");
                        ApplyToken(property, patch.After);
                        changed = true;
                    }
                    if (changed && serialized.ApplyModifiedPropertiesWithoutUndo())
                    {
                        EditorUtility.SetDirty(asset);
                        changedPaths.Add(group.Key);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            changedPaths.Sort(StringComparer.Ordinal);
            if (changedPaths.Count == 0)
                return new BalanceAssetApplicationResult(patches.Count, paths.Length, 0, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(
                changedPaths,
                ForceReserializeAssetsOptions.ReserializeAssets);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Dictionary<string, byte[]> firstPass = changedPaths.ToDictionary(
                value => value,
                value => File.ReadAllBytes(ProjectAbsolutePath(value)),
                StringComparer.Ordinal);
            if (failurePoint == BalanceAssetApplicationFailurePoint.AfterFirstReserialize)
                throw new BalanceAssetApplicationInjectedFailureException(failurePoint);
            AssetDatabase.ForceReserializeAssets(
                changedPaths,
                ForceReserializeAssetsOptions.ReserializeAssets);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            foreach (string path in changedPaths)
            {
                byte[] second = File.ReadAllBytes(ProjectAbsolutePath(path));
                if (!firstPass[path].SequenceEqual(second))
                    throw new InvalidOperationException(
                        $"UNITY_YAML_UNEXPECTED_CHURN second ForceReserialize changed {path}.");
                before[path].RequireStableIdentity(CaptureIdentity(path));
            }
            return new BalanceAssetApplicationResult(
                patches.Count, changedPaths.Count, differing, true);
        }
        catch
        {
            foreach (KeyValuePair<string, byte[]> pair in rollbackBytes)
                File.WriteAllBytes(ProjectAbsolutePath(pair.Key), pair.Value);
            foreach (string path in paths)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            throw;
        }
    }

    internal static string[] CaptureValidApprovalKeys(FrozenBalanceLedger ledger)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        Dictionary<string, BalanceApprovalEntryData> approvals =
            ValidateApprovals(LoadApprovals());
        HashSet<string> consumed = new HashSet<string>(StringComparer.Ordinal);
        foreach (CanonicalBalanceMetricRecord record in ledger.Records)
        {
            if (record.ApprovalKey.Length == 0
                || !approvals.TryGetValue(record.ApprovalKey, out BalanceApprovalEntryData approval))
                continue;
            RequireApprovalMatches(record, approval);
            consumed.Add(record.ApprovalKey);
        }
        string[] stale = approvals.Keys.Where(value => !consumed.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (stale.Length > 0)
            throw new InvalidOperationException("Stale V27 approval keys: " + string.Join(",", stale));
        return consumed.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    internal static string[] CaptureMatchingApprovalKeysForRefresh(FrozenBalanceLedger ledger)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        Dictionary<string, BalanceApprovalEntryData> approvals =
            ValidateApprovals(LoadApprovals());
        List<string> matching = new List<string>();
        foreach (CanonicalBalanceMetricRecord record in ledger.Records)
        {
            if (record.ApprovalKey.Length == 0
                || !approvals.TryGetValue(record.ApprovalKey, out BalanceApprovalEntryData approval))
            {
                continue;
            }
            RequireApprovalMatches(record, approval);
            matching.Add(record.ApprovalKey);
        }
        return matching.Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    internal static string CaptureApprovedPatchDigest(FrozenBalanceLedger ledger)
    {
        Dictionary<string, BalanceApprovalEntryData> approvals =
            ValidateApprovals(LoadApprovals());
        List<BalanceAssetPatch> patches = CreatePatches(ledger, approvals);
        using SHA256 sha = SHA256.Create();
        using MemoryStream stream = new MemoryStream();
        using (StreamWriter writer = new StreamWriter(
                   stream,
                   new UTF8Encoding(false, true),
                   4096,
                   leaveOpen: true))
        {
            foreach (BalanceAssetPatch patch in patches)
            {
                writer.Write(patch.AssetPath);
                writer.Write('\u001f');
                writer.Write(patch.PropertyPath);
                writer.Write('\u001f');
                writer.Write(patch.Before);
                writer.Write('\u001f');
                writer.Write(patch.After);
                writer.Write('\u001f');
                writer.Write(patch.ApprovalKey);
                writer.Write('\n');
            }
            writer.Flush();
        }
        stream.Position = 0L;
        byte[] digest = sha.ComputeHash(stream);
        char[] output = new char[digest.Length * 2];
        const string Digits = "0123456789abcdef";
        for (int index = 0; index < digest.Length; index++)
        {
            output[index * 2] = Digits[digest[index] >> 4];
            output[index * 2 + 1] = Digits[digest[index] & 0xf];
        }
        return new string(output);
    }

    internal static IReadOnlyDictionary<string, string> CaptureHistoricalBeforeValues()
    {
        BalanceApprovalFileData file = LoadApprovals();
        Dictionary<string, BalanceApprovalEntryData> approvals = ValidateApprovals(file);
        Dictionary<string, string> result = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (BalanceApprovalEntryData entry in approvals.Values)
        {
            if (string.IsNullOrEmpty(entry.exactBeforeValue))
                continue;
            if (CombatEncounterApprovalMetrics.Contains(entry.metric)
                && !string.Equals(
                    entry.balanceBaselineRecordId,
                    V27BalanceAudit.CombatOutcomeBaselineRecordId,
                    StringComparison.Ordinal))
            {
                // A completed encounter calibration becomes the authored Before
                // of the next explicit checkpoint revision. Carrying an older
                // baseline's original Before forward would make a minimal
                // follow-up appear to drift outside both its current and target
                // values, and would retain approvals for already-applied scalars.
                continue;
            }
            string key = BuildHistoricalBeforeKey(entry.rootStableId, entry.metric);
            if (!result.TryAdd(key, entry.exactBeforeValue))
            {
                throw new InvalidOperationException(
                    $"Duplicate historical Before authority: {entry.rootStableId}:{entry.metric}.");
            }
        }
        return result;
    }

    internal static string BuildHistoricalBeforeKey(string stableId, string metric) =>
        stableId + "\u001f" + metric;

    private static int WriteApprovals(
        FrozenBalanceLedger ledger,
        Func<CanonicalBalanceMetricRecord, bool> include,
        bool replaceIncludedApprovals = false)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));
        if (include == null)
            throw new ArgumentNullException(nameof(include));

        Dictionary<string, BalanceApprovalEntryData> existing =
            ValidateApprovals(LoadApprovals());
        List<BalanceApprovalEntryData> entries = new List<BalanceApprovalEntryData>();
        foreach (CanonicalBalanceMetricRecord record in ledger.Records)
        {
            if (record.ApprovalKey.Length == 0
                || (!existing.ContainsKey(record.ApprovalKey) && !include(record)))
            {
                continue;
            }
            entries.Add(new BalanceApprovalEntryData
            {
                approvalKey = record.ApprovalKey,
                rootStableId = record.StableId,
                metric = record.Metric,
                exactBeforeValue = record.Before,
                exactAfterValue = record.After,
                dependencyFingerprint = record.DependencyFingerprint,
                sourceDigest = record.SourceDigest,
                reasonCode = record.ReasonCode,
                balanceBaselineRecordId = record.BalanceBaselineRecordId
            });
        }
        HashSet<string> replaceableKeys = replaceIncludedApprovals
            ? existing.Values
                .Where(value => ItemMarketApprovalMetrics.Contains(value.metric)
                    || LaborFacilityApprovalMetrics.Contains(value.metric)
                    || CombatEncounterApprovalMetrics.Contains(value.metric))
                .Select(value => value.approvalKey)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        string[] missing = existing.Keys.Except(
                entries.Select(value => value.approvalKey),
                StringComparer.Ordinal)
            .Where(value => !replaceableKeys.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException("Existing approvals became stale: " + string.Join(",", missing));

        BalanceApprovalFileData output = new BalanceApprovalFileData
        {
            schemaVersion = "v27.2",
            approvals = entries
                .OrderBy(value => value.rootStableId, StringComparer.Ordinal)
                .ThenBy(value => value.metric, StringComparer.Ordinal)
                .ThenBy(value => value.approvalKey, StringComparer.Ordinal)
                .ToArray()
        };
        V27BalanceArtifactWriter.WriteIfDifferent(V27BalanceAudit.ApprovalPath, stream =>
        {
            using StreamWriter writer = new StreamWriter(
                stream,
                new UTF8Encoding(false, true),
                4096,
                leaveOpen: true)
            {
                NewLine = "\n"
            };
            writer.Write(JsonUtility.ToJson(output, prettyPrint: true));
            writer.Write('\n');
            writer.Flush();
        });
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        return output.approvals.Length;
    }

    private static List<BalanceAssetPatch> CreatePatches(
        FrozenBalanceLedger ledger,
        IReadOnlyDictionary<string, BalanceApprovalEntryData> approvals)
    {
        List<BalanceAssetPatch> patches = new List<BalanceAssetPatch>();
        HashSet<string> consumedApprovals = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> propertyKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (CanonicalBalanceMetricRecord record in ledger.Records)
        {
            if (record.ApprovalKey.Length == 0
                || !approvals.TryGetValue(record.ApprovalKey, out BalanceApprovalEntryData approval))
                continue;
            RequireApprovalMatches(record, approval);
            consumedApprovals.Add(record.ApprovalKey);
            if (string.Equals(record.AssetApplied, "true", StringComparison.Ordinal))
                continue;
            if (!record.SourceAuthority.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                || record.SourcePropertyPath.Length == 0
                || string.Equals(record.Before, record.After, StringComparison.Ordinal))
                continue;
            string propertyKey = record.SourceAuthority + "\u001f" + record.SourcePropertyPath;
            if (!propertyKeys.Add(propertyKey))
                throw new InvalidOperationException(
                    $"Multiple approvals target the same SerializedProperty: {propertyKey}");
            patches.Add(BalanceAssetPatch.Capture(record));
        }
        string[] stale = approvals.Keys.Where(value => !consumedApprovals.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (stale.Length > 0)
            throw new InvalidOperationException("Stale V27 approval keys: " + string.Join(",", stale));
        return patches.OrderBy(value => value.AssetPath, StringComparer.Ordinal)
            .ThenBy(value => value.PropertyPath, StringComparer.Ordinal)
            .ToList();
    }

    private static void RequireApprovalMatches(
        CanonicalBalanceMetricRecord record,
        BalanceApprovalEntryData approval)
    {
        BalanceReviewApproval canonical = BalanceReviewApproval.Capture(
            approval.rootStableId,
            approval.metric,
            approval.exactAfterValue,
            approval.dependencyFingerprint,
            approval.sourceDigest,
            approval.reasonCode,
            approval.balanceBaselineRecordId);
        if (!canonical.Matches(
                record.StableId,
                record.Metric,
                record.After,
                record.DependencyFingerprint,
                record.SourceDigest)
            || !string.Equals(approval.reasonCode, record.ReasonCode, StringComparison.Ordinal)
            || (!string.IsNullOrEmpty(approval.exactBeforeValue)
                && !string.Equals(
                    approval.exactBeforeValue,
                    record.Before,
                    StringComparison.Ordinal))
            || !string.Equals(
                approval.balanceBaselineRecordId,
                record.BalanceBaselineRecordId,
                StringComparison.Ordinal)
            || !string.Equals(approval.approvalKey, record.ApprovalKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Stale or mismatched V27 approval: {approval.approvalKey}");
        }
    }

    private static Dictionary<string, BalanceApprovalEntryData> ValidateApprovals(
        BalanceApprovalFileData file)
    {
        if (file == null
            || (!string.Equals(file.schemaVersion, "v27.1", StringComparison.Ordinal)
                && !string.Equals(file.schemaVersion, "v27.2", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("V27 approval schemaVersion must be v27.1 or v27.2.");
        }
        Dictionary<string, BalanceApprovalEntryData> result =
            new Dictionary<string, BalanceApprovalEntryData>(StringComparer.Ordinal);
        foreach (BalanceApprovalEntryData entry in file.approvals ?? Array.Empty<BalanceApprovalEntryData>())
        {
            if (entry == null || string.IsNullOrEmpty(entry.approvalKey)
                || !result.TryAdd(entry.approvalKey, entry))
                throw new InvalidOperationException("V27 approvals require unique non-empty approvalKey values.");
        }
        return result;
    }

    private static BalanceApprovalFileData LoadApprovals()
    {
        string path = ProjectAbsolutePath(V27BalanceAudit.ApprovalPath);
        if (!File.Exists(path))
            throw new InvalidOperationException("V27 approval authority is missing.");
        return JsonUtility.FromJson<BalanceApprovalFileData>(File.ReadAllText(path, Encoding.UTF8))
            ?? throw new InvalidOperationException("V27 approval authority is invalid JSON.");
    }

    private static string CaptureToken(SerializedProperty property) => property.propertyType switch
    {
        SerializedPropertyType.Integer => property.longValue.ToString(CultureInfo.InvariantCulture),
        SerializedPropertyType.Boolean => property.boolValue ? "true" : "false",
        SerializedPropertyType.Float => property.doubleValue.ToString("R", CultureInfo.InvariantCulture),
        SerializedPropertyType.Enum => property.enumValueIndex.ToString(CultureInfo.InvariantCulture),
        _ => throw new InvalidOperationException(
            $"Unsupported approved SerializedProperty type {property.propertyType}: {property.propertyPath}")
    };

    private static bool TokenMatchesProperty(SerializedProperty property, string token)
    {
        if (property.propertyType != SerializedPropertyType.Float)
        {
            return string.Equals(
                CaptureToken(property),
                token,
                StringComparison.Ordinal);
        }
        if (string.Equals(property.type, "float", StringComparison.Ordinal))
        {
            float expected = float.Parse(
                token,
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
            float actual = (float)property.doubleValue;
            return BitConverter.SingleToInt32Bits(actual)
                == BitConverter.SingleToInt32Bits(expected);
        }
        double expectedDouble = double.Parse(
            token,
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
        return BitConverter.DoubleToInt64Bits(property.doubleValue)
            == BitConverter.DoubleToInt64Bits(expectedDouble);
    }

    private static void ApplyToken(SerializedProperty property, string token)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                property.longValue = long.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
                break;
            case SerializedPropertyType.Boolean:
                property.boolValue = token switch
                {
                    "true" => true,
                    "false" => false,
                    _ => throw new InvalidOperationException($"Invalid boolean patch token: {token}")
                };
                break;
            case SerializedPropertyType.Float:
                property.doubleValue = double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
                break;
            case SerializedPropertyType.Enum:
                property.enumValueIndex = int.Parse(token, NumberStyles.Integer, CultureInfo.InvariantCulture);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported approved SerializedProperty type {property.propertyType}: {property.propertyPath}");
        }
    }

    private static AssetIdentitySnapshot CaptureIdentity(string assetPath)
    {
        string absolute = ProjectAbsolutePath(assetPath);
        string metaPath = absolute + ".meta";
        UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(assetPath)
            ?? throw new InvalidOperationException($"Asset identity target is missing: {assetPath}");
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(main, out string guid, out long mainFileId))
            throw new InvalidOperationException($"Cannot capture GUID/FileID: {assetPath}");
        string[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .Where(value => value != null)
            .Select(value =>
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string _, out long localId);
                return value.GetType().FullName + "|" + value.name + "|" + localId;
            })
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        SerializedObject serialized = new SerializedObject(main);
        SerializedProperty script = serialized.FindProperty("m_Script");
        string scriptIdentity = string.Empty;
        if (script?.objectReferenceValue != null
            && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                script.objectReferenceValue, out string scriptGuid, out long scriptFileId))
            scriptIdentity = scriptGuid + "|" + scriptFileId.ToString(CultureInfo.InvariantCulture);
        byte[] bytes = File.ReadAllBytes(absolute);
        return new AssetIdentitySnapshot(
            assetPath,
            HashFile(metaPath),
            guid,
            mainFileId,
            subAssets,
            scriptIdentity,
            CountYamlDocuments(bytes));
    }

    private static int CountYamlDocuments(byte[] bytes)
    {
        ReadOnlySpan<byte> marker = Encoding.ASCII.GetBytes("--- !u!");
        int count = 0;
        for (int index = 0; index <= bytes.Length - marker.Length; index++)
        {
            bool match = true;
            for (int offset = 0; offset < marker.Length; offset++)
                if (bytes[index + offset] != marker[offset]) { match = false; break; }
            if (match) count++;
        }
        return count;
    }

    private static string CaptureGitDirty(string assetPath)
    {
        ProcessStartInfo start = new ProcessStartInfo("git")
        {
            WorkingDirectory = ProjectRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = "status --porcelain=v1 -- " + QuoteArgument(assetPath)
        };
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Failed to start git dirty check.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("git dirty check failed: " + error);
        return output.Trim();
    }

    private static string QuoteArgument(string value) =>
        "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string HashFile(string path)
    {
        if (!File.Exists(path)) return string.Empty;
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
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

    private static string ProjectRoot() => Directory.GetParent(Application.dataPath)?.FullName
        ?? throw new InvalidOperationException("Project root is unavailable.");

    private static string ProjectAbsolutePath(string relative) => Path.Combine(
        ProjectRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    [Serializable]
    private sealed class BalanceApprovalFileData
    {
        public string schemaVersion;
        public BalanceApprovalEntryData[] approvals;
    }

    [Serializable]
    private sealed class BalanceApprovalEntryData
    {
        public string approvalKey;
        public string rootStableId;
        public string metric;
        public string exactBeforeValue;
        public string exactAfterValue;
        public string dependencyFingerprint;
        public string sourceDigest;
        public string reasonCode;
        public string balanceBaselineRecordId;
    }
}

[BalanceImmutableRecord]
public sealed class BalanceAssetPatch
{
    private BalanceAssetPatch(
        string assetPath,
        string propertyPath,
        string before,
        string after,
        string approvalKey)
    {
        AssetPath = assetPath;
        PropertyPath = propertyPath;
        Before = before;
        After = after;
        ApprovalKey = approvalKey;
    }

    public string AssetPath { get; }
    public string PropertyPath { get; }
    public string Before { get; }
    public string After { get; }
    public string ApprovalKey { get; }

    [BalanceCaptureFactory]
    public static BalanceAssetPatch Capture(CanonicalBalanceMetricRecord record) => new(
        BalanceCanonicalText.ProjectRelativePath(record.SourceAuthority),
        BalanceCanonicalText.Detail(record.SourcePropertyPath),
        BalanceCanonicalText.Display(record.Before),
        BalanceCanonicalText.Display(record.After),
        BalanceCanonicalText.StableId(record.ApprovalKey, "approvalKey"));

    [BalanceCaptureFactory]
    internal static BalanceAssetPatch CaptureForDiagnostics(
        string assetPath,
        string propertyPath,
        string before,
        string after) => new(
        BalanceCanonicalText.ProjectRelativePath(assetPath),
        BalanceCanonicalText.Detail(propertyPath),
        BalanceCanonicalText.Display(before),
        BalanceCanonicalText.Display(after),
        "diagnostic:forced-rollback");
}

internal enum BalanceAssetApplicationFailurePoint
{
    None = 0,
    AfterFirstReserialize = 1
}

internal sealed class BalanceAssetApplicationInjectedFailureException : Exception
{
    public BalanceAssetApplicationInjectedFailureException(
        BalanceAssetApplicationFailurePoint failurePoint)
        : base("Injected V27 asset application failure at " + failurePoint)
    {
        FailurePoint = failurePoint;
    }

    public BalanceAssetApplicationFailurePoint FailurePoint { get; }
}

[BalanceImmutableRecord]
public sealed class BalanceAssetApplicationResult
{
    public BalanceAssetApplicationResult(
        int approvedPatchCount,
        int assetCount,
        int differingPropertyCount,
        bool passed)
    {
        ApprovedPatchCount = approvedPatchCount;
        AssetCount = assetCount;
        DifferingPropertyCount = differingPropertyCount;
        Passed = passed;
    }

    public int ApprovedPatchCount { get; }
    public int AssetCount { get; }
    public int DifferingPropertyCount { get; }
    public bool Passed { get; }

    public string Format(string mode) =>
        $"RESULT={(Passed ? "PASS" : "FAIL")}; mode={mode}; patches={ApprovedPatchCount}; "
        + $"assets={AssetCount}; differing={DifferingPropertyCount}";
}

internal sealed class AssetIdentitySnapshot
{
    public AssetIdentitySnapshot(
        string path,
        string metaHash,
        string guid,
        long mainFileId,
        string[] subAssets,
        string scriptIdentity,
        int yamlDocumentCount)
    {
        Path = path;
        MetaHash = metaHash;
        Guid = guid;
        MainFileId = mainFileId;
        SubAssets = subAssets;
        ScriptIdentity = scriptIdentity;
        YamlDocumentCount = yamlDocumentCount;
    }

    public string Path { get; }
    public string MetaHash { get; }
    public string Guid { get; }
    public long MainFileId { get; }
    public IReadOnlyList<string> SubAssets { get; }
    public string ScriptIdentity { get; }
    public int YamlDocumentCount { get; }

    public void RequireStableIdentity(AssetIdentitySnapshot after)
    {
        if (!string.Equals(MetaHash, after.MetaHash, StringComparison.Ordinal)
            || !string.Equals(Guid, after.Guid, StringComparison.Ordinal)
            || MainFileId != after.MainFileId
            || !SubAssets.SequenceEqual(after.SubAssets, StringComparer.Ordinal)
            || !string.Equals(ScriptIdentity, after.ScriptIdentity, StringComparison.Ordinal)
            || YamlDocumentCount != after.YamlDocumentCount)
        {
            throw new InvalidOperationException(
                $"UNITY_YAML_UNEXPECTED_CHURN identity changed for {Path}.");
        }
    }
}
#endif
