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
        "direct-wu",
        "construction-authored-wu:redistributed",
        // Approval-refresh compatibility only. The live ledger no longer emits
        // this pre-redistribution metric, but an exact refresh must be allowed
        // to retire its old approval keys atomically.
        "construction-authored-wu:period-preserving"
    };
    private static readonly HashSet<string> RecurringThroughputApprovalMetrics = new(
        StringComparer.Ordinal)
    {
        "authored-required-wu",
        "authored-sow-wu",
        "authored-harvest-wu"
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

    [MenuItem("DungeonStory/V27/Apply Approved Patches In Current V27 Worktree")]
    public static void ApplyApprovedInCurrentV27WorktreeFromMenu()
    {
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        BalanceApprovalFileData approvalFile = LoadApprovals();
        Dictionary<string, BalanceApprovalEntryData> approvals =
            ValidateApprovals(approvalFile);
        List<BalanceAssetPatch> patches = CreatePatches(audit.Ledger, approvals);

        // This explicit command is reserved for the active V27 recalibration
        // worktree. ApplyPatches still requires every approved property to equal
        // the ledger Before, mutates only approved property paths, snapshots all
        // target bytes, and rolls the complete target set back on any failure.
        BalanceAssetApplicationResult result = ApplyPatches(
            patches,
            dryRun: false,
            requireCleanGit: false,
            BalanceAssetApplicationFailurePoint.None);
        Debug.Log(result.Format("ApplyApprovedCurrentV27Worktree"));
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
            requireApplied: false,
            allowUnapprovedCritical: true);
        string approvalPath = ProjectAbsolutePath(V27BalanceAudit.ApprovalPath);
        byte[] rollback = File.ReadAllBytes(approvalPath);
        try
        {
            int count = WriteApprovals(
                audit.Ledger,
                record => ItemMarketApprovalMetrics.Contains(record.Metric)
                    || IsLaborFacilityApprovalMetric(record.Metric),
                replaceIncludedApprovals: true);
            V27BalanceAuditOutput verified = V27BalanceAudit.Generate(
                BalanceLedgerExecutionMode.AuditOnly);
            V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
                verified,
                requireApplied: false);
            Debug.Log($"V27 exact labor/facility approvals generated: approvals={count}.");
        }
        catch
        {
            File.WriteAllBytes(approvalPath, rollback);
            AssetDatabase.ImportAsset(
                V27BalanceAudit.ApprovalPath,
                ImportAssetOptions.ForceUpdate);
            throw;
        }
    }

    [MenuItem("DungeonStory/V27/Rebase And Apply Previously Approved Labor Facility Drift")]
    public static void RebaseAndApplyPreviouslyApprovedLaborFacilityDriftFromMenu()
    {
        V27BalanceAuditOutput current = V27BalanceAudit.GenerateForApprovalRefresh();
        V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
            current,
            requireApplied: false,
            allowUnapprovedCritical: true);

        string approvalPath = ProjectAbsolutePath(V27BalanceAudit.ApprovalPath);
        byte[] approvalRollback = File.ReadAllBytes(approvalPath);
        SortedDictionary<string, byte[]> assetRollback = new(
            StringComparer.Ordinal);
        const int MaxRebaseIterations = 8;
        int iterationCount = 0;
        int totalRebasePatchCount = 0;
        int totalDirectRefreshPatchCount = 0;
        int totalChangedAssetCount = 0;
        string rebasePhase = "prepare-iteration-0";

        try
        {
            while (true)
            {
                rebasePhase = $"prepare-iteration-{iterationCount}";
                Dictionary<string, BalanceApprovalEntryData> approvals =
                    ValidateApprovals(LoadApprovals());
                List<BalanceAssetPatch> rebasePatches =
                    CreatePreviouslyApprovedRebasePatches(
                        current.Ledger,
                        approvals);
                List<BalanceAssetPatch> directRefreshCandidates =
                    CreateLaborFacilityAndMarketRefreshPatches(current.Ledger);
                HashSet<string> rebasePropertyKeys = rebasePatches
                    .Select(value => value.AssetPath + "\u001f" + value.PropertyPath)
                    .ToHashSet(StringComparer.Ordinal);
                List<BalanceAssetPatch> directRefreshPatches = directRefreshCandidates
                    .Where(value => !rebasePropertyKeys.Contains(
                        value.AssetPath + "\u001f" + value.PropertyPath))
                    .ToList();

                if (rebasePatches.Count == 0 && directRefreshPatches.Count == 0)
                    break;
                if (iterationCount >= MaxRebaseIterations)
                {
                    throw new InvalidOperationException(
                        "V27 labor/facility rebase did not converge within "
                        + $"{MaxRebaseIterations} exact iterations; "
                        + $"remainingRebase={rebasePatches.Count}; "
                        + $"remainingDirectRefresh={directRefreshPatches.Count}.");
                }

                string[] iterationPaths = rebasePatches
                    .Concat(directRefreshPatches)
                    .Select(value => value.AssetPath)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                foreach (string path in iterationPaths)
                {
                    if (!assetRollback.ContainsKey(path))
                    {
                        assetRollback.Add(
                            path,
                            File.ReadAllBytes(ProjectAbsolutePath(path)));
                    }
                }

                // Publish exact temporary custody for the target calculated
                // from this immutable ledger before mutating any asset. The
                // next refresh may calculate a new target after BOM/dependency
                // changes, but it may only do so from this exact approved
                // authority. The approval file and every touched asset remain
                // under the same outer rollback transaction.
                rebasePhase = $"write-iteration-{iterationCount}-custody";
                WriteApprovals(
                    current.Ledger,
                    record => ItemMarketApprovalMetrics.Contains(record.Metric)
                        || IsLaborFacilityApprovalMetric(record.Metric),
                    replaceIncludedApprovals: true);

                rebasePhase = $"apply-iteration-{iterationCount}-rebases";
                BalanceAssetApplicationResult rebased = ApplyPatches(
                    rebasePatches,
                    dryRun: false,
                    requireCleanGit: false,
                    BalanceAssetApplicationFailurePoint.None);
                rebasePhase = $"apply-iteration-{iterationCount}-direct-refresh";
                BalanceAssetApplicationResult directRefresh = ApplyPatches(
                    directRefreshPatches,
                    dryRun: false,
                    requireCleanGit: false,
                    BalanceAssetApplicationFailurePoint.None);

                totalRebasePatchCount += rebasePatches.Count;
                totalDirectRefreshPatchCount += directRefreshPatches.Count;
                totalChangedAssetCount += rebased.AssetCount + directRefresh.AssetCount;
                iterationCount++;

                rebasePhase = $"refresh-after-iteration-{iterationCount}";
                current = V27BalanceAudit.GenerateForApprovalRefresh();
                V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
                    current,
                    requireApplied: false,
                    allowUnapprovedCritical: true);
            }

            // The converged ledger owns the final post-application source
            // digests. Replace temporary custody with final exact approvals
            // before running the normal, strict audit and no-op application.
            rebasePhase = "write-converged-approvals";
            int approvalCount = WriteApprovals(
                current.Ledger,
                record => ItemMarketApprovalMetrics.Contains(record.Metric)
                    || IsLaborFacilityApprovalMetric(record.Metric),
                replaceIncludedApprovals: true);

            rebasePhase = "standard-verify";
            V27BalanceAuditOutput verified = V27BalanceAudit.Generate(
                BalanceLedgerExecutionMode.AuditOnly);
            V27BalanceLaborFacilityDebugScenarios.RequireIntegrity(
                verified,
                requireApplied: true);
            rebasePhase = "no-op-verify";
            List<BalanceAssetPatch> verifiedPatches = CreatePatches(
                verified.Ledger,
                ValidateApprovals(LoadApprovals()));
            BalanceAssetApplicationResult noOp = ApplyPatches(
                verifiedPatches,
                dryRun: true,
                requireCleanGit: false,
                BalanceAssetApplicationFailurePoint.None);
            if (noOp.DifferingPropertyCount != 0)
            {
                throw new InvalidOperationException(
                    "Rebased V27 labor/facility authority was not a no-op after verification.");
            }

            Debug.Log(
                $"V27 labor/facility approved drift rebased: iterations={iterationCount}; "
                + $"rebasePatches={totalRebasePatchCount}; "
                + $"directRefreshPatches={totalDirectRefreshPatchCount}; "
                + $"changedAssets={totalChangedAssetCount}; "
                + $"rollbackAssets={assetRollback.Count}; "
                + $"approvals={approvalCount}; "
                + $"noOpDiff={noOp.DifferingPropertyCount}.");
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"V27 labor/facility rebase failed in phase '{rebasePhase}': "
                + exception);
            File.WriteAllBytes(approvalPath, approvalRollback);
            foreach (KeyValuePair<string, byte[]> pair in assetRollback)
                File.WriteAllBytes(ProjectAbsolutePath(pair.Key), pair.Value);
            AssetDatabase.ImportAsset(
                V27BalanceAudit.ApprovalPath,
                ImportAssetOptions.ForceUpdate);
            foreach (string path in assetRollback.Keys)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            throw;
        }
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
                || IsLaborFacilityApprovalMetric(record.Metric)
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
            if (TokenMatchesProperty(property, patch.After))
                continue;
            if (!TokenMatchesProperty(property, patch.Before))
            {
                throw new InvalidOperationException(
                    $"Stale approved patch {patch.AssetPath}:{patch.PropertyPath}; "
                    + $"ledger Before={patch.Before}, authority={current}.");
            }
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
            // Market approvals must keep the exact authored Before until the
            // approval is applied. Treating a pending market After as the new
            // Before makes the immediately-following standard audit collapse
            // Before==After and reject the freshly generated approval as stale.
            //
            // Recurring throughput is different: its approval maps an
            // accidentally project-scaled authored value back to the frozen
            // per-batch authority. The approved After is therefore the base
            // used to reconstruct both the legacy scaled Before and the
            // corrected authored After on every audit, before and after asset
            // application.
            string inheritedBefore = RecurringThroughputApprovalMetrics.Contains(
                    entry.metric)
                ? entry.exactAfterValue
                : entry.exactBeforeValue;
            if (string.IsNullOrEmpty(inheritedBefore))
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
            if (!result.TryAdd(key, inheritedBefore))
            {
                throw new InvalidOperationException(
                    $"Duplicate historical Before authority: {entry.rootStableId}:{entry.metric}.");
            }
        }
        return result;
    }

    internal static bool IsPreviouslyApprovedCurrentAuthority(
        string stableId,
        string metric,
        string exactBeforeValue,
        string currentValue,
        string sourceDigest)
    {
        // Approval refresh is the one boundary where a changed source digest is
        // expected: a new BOM/dependency revision is precisely what invalidated
        // the old approval. Never treat that old digest as approval for the new
        // target. It only proves that the current authored scalar is the exact
        // After value of a canonical previous approval; the refreshed ledger
        // captures and approves the new source digest before normal audit runs.
        RequireCanonicalSha256(sourceDigest, "current source digest");
        BalanceApprovalEntryData[] matches = ValidateApprovals(LoadApprovals())
            .Values
            .Where(value => string.Equals(
                    value.rootStableId,
                    stableId,
                    StringComparison.Ordinal)
                && string.Equals(value.metric, metric, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"Duplicate V27 approval identity: {stableId}:{metric}.");
        }
        if (matches.Length == 0)
            return false;

        BalanceApprovalEntryData approval = matches[0];
        RequireStoredApprovalKeyValid(approval);
        return string.Equals(
                approval.exactBeforeValue,
                exactBeforeValue,
                StringComparison.Ordinal)
            && string.Equals(
                approval.exactAfterValue,
                currentValue,
                StringComparison.Ordinal);
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
                    || IsLaborFacilityApprovalMetric(value.metric)
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

    private static bool IsLaborFacilityApprovalMetric(string metric) =>
        LaborFacilityApprovalMetrics.Contains(metric)
        || (!string.IsNullOrEmpty(metric)
            && metric.StartsWith(
                "construction-material-amount:",
                StringComparison.Ordinal));

    private static List<BalanceAssetPatch> CreatePreviouslyApprovedRebasePatches(
        FrozenBalanceLedger ledger,
        IReadOnlyDictionary<string, BalanceApprovalEntryData> approvals)
    {
        Dictionary<string, BalanceApprovalEntryData> previousByIdentity = new(
            StringComparer.Ordinal);
        foreach (BalanceApprovalEntryData approval in approvals.Values)
        {
            if (!IsLaborFacilityApprovalMetric(approval.metric)
                && !ItemMarketApprovalMetrics.Contains(approval.metric))
            {
                continue;
            }
            RequireStoredApprovalKeyValid(approval);
            string identity = BuildHistoricalBeforeKey(
                approval.rootStableId,
                approval.metric);
            if (!previousByIdentity.TryAdd(identity, approval))
            {
                throw new InvalidOperationException(
                    $"Duplicate previous V27 approval identity: {identity}.");
            }
        }

        List<BalanceAssetPatch> patches = new();
        HashSet<string> propertyKeys = new(StringComparer.Ordinal);
        foreach (CanonicalBalanceMetricRecord record in ledger.Records)
        {
            if ((!IsLaborFacilityApprovalMetric(record.Metric)
                    && !ItemMarketApprovalMetrics.Contains(record.Metric))
                || string.Equals(record.Before, record.After, StringComparison.Ordinal)
                || string.Equals(record.AssetApplied, "true", StringComparison.Ordinal)
                || !record.SourceAuthority.EndsWith(
                    ".asset",
                    StringComparison.OrdinalIgnoreCase)
                || record.SourcePropertyPath.Length == 0)
            {
                continue;
            }

            string identity = BuildHistoricalBeforeKey(record.StableId, record.Metric);
            if (!previousByIdentity.TryGetValue(
                    identity,
                    out BalanceApprovalEntryData previous))
            {
                continue;
            }
            if (!string.Equals(
                    previous.exactBeforeValue,
                    record.Before,
                    StringComparison.Ordinal)
                || !string.Equals(
                    previous.reasonCode,
                    record.ReasonCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    previous.balanceBaselineRecordId,
                    record.BalanceBaselineRecordId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Previously approved V27 authority cannot be rebased: {identity}.");
            }
            if (string.Equals(
                    previous.exactAfterValue,
                    record.After,
                    StringComparison.Ordinal))
            {
                continue;
            }

            // The current record intentionally has a new source digest. The
            // exact old After/current-authority equality below is the rebase
            // custody gate; the new record digest becomes the replacement
            // approval and is verified again by the standard audit after apply.
            RequireCanonicalSha256(record.SourceDigest, "rebase source digest");

            string propertyKey = record.SourceAuthority + "\u001f" + record.SourcePropertyPath;
            if (!propertyKeys.Add(propertyKey))
            {
                throw new InvalidOperationException(
                    $"Multiple V27 rebase records target {propertyKey}.");
            }
            string assetPath = BalanceCanonicalText.ProjectRelativePath(
                record.SourceAuthority);
            string propertyPath = BalanceCanonicalText.Detail(
                record.SourcePropertyPath);
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath)
                ?? throw new InvalidOperationException(
                    $"Previously approved V27 asset is missing: {assetPath}.");
            SerializedObject serialized = new(asset);
            SerializedProperty property = serialized.FindProperty(propertyPath)
                ?? throw new InvalidOperationException(
                    $"Previously approved V27 property is missing: "
                    + $"{assetPath}:{propertyPath}.");

            // A later authored-content builder may legitimately restore the
            // historical Before while adding a new dependency/BOM row. That
            // state is not permission to reuse the old After: it is a second
            // exact custody state from which the current ledger's newly
            // calculated After can be applied. Accept only the canonical old
            // After or the exact historical Before; every third value remains
            // an unapproved authority edit and fails before mutation.
            string current = CaptureToken(property);
            string patchBefore;
            if (TokenMatchesProperty(property, previous.exactAfterValue))
            {
                patchBefore = previous.exactAfterValue;
            }
            else if (TokenMatchesProperty(property, record.Before))
            {
                patchBefore = record.Before;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Previously approved V27 authority changed before rebase: "
                    + $"{assetPath}:{propertyPath}; "
                    + $"approvedAfter={previous.exactAfterValue}; "
                    + $"historicalBefore={record.Before}; authority={current}.");
            }

            BalanceAssetPatch patch = BalanceAssetPatch.CaptureForApprovedRebase(
                record,
                patchBefore,
                previous.approvalKey);
            patches.Add(patch);
        }
        return patches
            .OrderBy(value => value.AssetPath, StringComparer.Ordinal)
            .ThenBy(value => value.PropertyPath, StringComparer.Ordinal)
            .ToList();
    }

    private static List<BalanceAssetPatch> CreateLaborFacilityAndMarketRefreshPatches(
        FrozenBalanceLedger ledger)
    {
        if (ledger == null)
            throw new ArgumentNullException(nameof(ledger));

        List<BalanceAssetPatch> patches = new();
        HashSet<string> propertyKeys = new(StringComparer.Ordinal);
        foreach (CanonicalBalanceMetricRecord record in ledger.Records)
        {
            if ((!IsLaborFacilityApprovalMetric(record.Metric)
                    && !ItemMarketApprovalMetrics.Contains(record.Metric))
                || string.Equals(record.Before, record.After, StringComparison.Ordinal)
                || string.Equals(record.AssetApplied, "true", StringComparison.Ordinal)
                || !record.SourceAuthority.EndsWith(
                    ".asset",
                    StringComparison.OrdinalIgnoreCase)
                || record.SourcePropertyPath.Length == 0)
            {
                continue;
            }

            string propertyKey = record.SourceAuthority + "\u001f"
                + record.SourcePropertyPath;
            if (!propertyKeys.Add(propertyKey))
            {
                throw new InvalidOperationException(
                    $"Multiple V27 labor/facility/market refresh records target {propertyKey}.");
            }
            patches.Add(BalanceAssetPatch.CaptureForApprovedRefresh(record));
        }

        return patches
            .OrderBy(value => value.AssetPath, StringComparer.Ordinal)
            .ThenBy(value => value.PropertyPath, StringComparer.Ordinal)
            .ToList();
    }

    private static void RequireStoredApprovalKeyValid(
        BalanceApprovalEntryData approval)
    {
        using SHA256 sha = SHA256.Create();
        string canonical = approval.rootStableId + "\u001f"
            + approval.metric + "\u001f"
            + approval.exactAfterValue + "\u001f"
            + approval.dependencyFingerprint + "\u001f"
            + approval.sourceDigest + "\u001f"
            + approval.reasonCode + "\u001f"
            + approval.balanceBaselineRecordId;
        byte[] digest = sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(canonical));
        StringBuilder expected = new(digest.Length * 2);
        foreach (byte value in digest)
            expected.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        if (!string.Equals(
                approval.approvalKey,
                expected.ToString(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Stored V27 approval key is not canonical: {approval.approvalKey}.");
        }
    }

    private static void RequireCanonicalSha256(string value, string label)
    {
        if (value == null || value.Length != 64)
        {
            throw new InvalidOperationException(
                $"V27 {label} must be a 64-character lowercase SHA-256 token.");
        }
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if ((character < '0' || character > '9')
                && (character < 'a' || character > 'f'))
            {
                throw new InvalidOperationException(
                    $"V27 {label} must be a 64-character lowercase SHA-256 token.");
            }
        }
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
                $"Stale or mismatched V27 approval: {approval.approvalKey}; "
                + $"record={record.StableId}|{record.Metric}|{record.Before}|"
                + $"{record.After}|{record.DependencyFingerprint}|{record.SourceDigest}|"
                + $"{record.ReasonCode}|{record.BalanceBaselineRecordId}; "
                + $"approval={approval.rootStableId}|{approval.metric}|"
                + $"{approval.exactBeforeValue}|{approval.exactAfterValue}|"
                + $"{approval.dependencyFingerprint}|{approval.sourceDigest}|"
                + $"{approval.reasonCode}|{approval.balanceBaselineRecordId}");
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

    [BalanceCaptureFactory]
    internal static BalanceAssetPatch CaptureForApprovedRebase(
        CanonicalBalanceMetricRecord record,
        string previouslyApprovedValue,
        string previousApprovalKey) => new(
        BalanceCanonicalText.ProjectRelativePath(record.SourceAuthority),
        BalanceCanonicalText.Detail(record.SourcePropertyPath),
        BalanceCanonicalText.Display(previouslyApprovedValue),
        BalanceCanonicalText.Display(record.After),
        BalanceCanonicalText.StableId(previousApprovalKey, "previousApprovalKey"));

    [BalanceCaptureFactory]
    internal static BalanceAssetPatch CaptureForApprovedRefresh(
        CanonicalBalanceMetricRecord record) => new(
        BalanceCanonicalText.ProjectRelativePath(record.SourceAuthority),
        BalanceCanonicalText.Detail(record.SourcePropertyPath),
        BalanceCanonicalText.Display(record.Before),
        BalanceCanonicalText.Display(record.After),
        "v27:bounded-labor-facility-refresh");
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
