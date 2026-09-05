#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies only a previously captured and reviewed V27 canonical-output proposal.
/// Every proposal and target is preflighted before the first SerializedProperty write.
/// </summary>
public static class V27CanonicalOutputLineApplyApproved
{
    public const string ManifestPath =
        "Artifacts/QA/v27-canonical-output-line-apply-manifest.txt";
    public const string CurrentAuthorityManifestPath =
        "Artifacts/QA/v27-canonical-output-line-current-authority.txt";

    private const int ExpectedRows = 357;
    private const int ExpectedOutputLineIdChanges = 353;
    private const int ExpectedOutputRoleChanges = 6;

    private static readonly HashSet<string> ExpectedRoleChangeKeys = new(
        StringComparer.Ordinal)
    {
        "source:logging|1|resource:dark-resin|0.18",
        "source:quarry|1|resource:coal|0.2",
        "source:quarry|2|resource:iron-ore|0.16",
        "source:quarry|3|resource:gold-ore|0.03",
        "source:quarry|4|resource:mana-crystal|0.01",
        "source:saltstone|1|resource:saltstone|0.25"
    };

    private static readonly IReadOnlyDictionary<string, string>
        ExpectedPostApprovalOutputItemByKey =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["recipe:dog-food|0"] = "feed:dog-food",
                ["recipe:dog-food-fresh|0"] = "feed:dog-food-fresh",
                ["recipe:hay-feed|0"] = "feed:hay",
                ["recipe:silage|0"] = "feed:silage"
            };

    private static readonly string[] ExpectedHeader =
    {
        "schemaVersion", "recipeId", "authoredOutputOrdinal", "itemId",
        "amount", "probability", "authoredOutputLineId", "authoredRole",
        "proposedOutputLineId", "proposedRole", "proposalReason",
        "proposalDisposition", "sourceAuthority", "sourceDigest"
    };

    [MenuItem("DungeonStory/V27/Production/Apply Approved Output-Line Backfill")]
    public static void ApplyApprovedFromMenu()
    {
        ApplyResult result = ApplyApproved();
        Debug.Log(
            "V27 canonical output-line ApplyApproved passed: "
            + $"changedAssets={result.ChangedAssetCount}; "
            + $"outputLineIdChanges={result.OutputLineIdChanges}; "
            + $"outputRoleChanges={result.OutputRoleChanges}; "
            + $"saveAssetsCalls={result.SaveAssetsCalls}; "
            + $"alreadyApplied={result.AlreadyApplied}.");
    }

    internal static ApplyResult ApplyApproved()
    {
        ApprovedArtifact approved = LoadApprovedArtifact();
        V27CanonicalOutputLineBackfillProposalSnapshot current =
            V27CanonicalOutputLineBackfillProposalDebugScenarios
                .CaptureProposalSnapshotForAudit();
        string currentSemanticHash = ComputeSemanticHash(
            current.Rows,
            useProposed: false);
        string approvedBeforeHash = ComputeSemanticHash(
            approved.Rows,
            useProposed: false);
        string approvedAfterHash = ComputeSemanticHash(
            approved.Rows,
            useProposed: true);

        if (IsFullyCanonicalCurrentAuthority(current.Rows))
        {
            VerifyAndWriteCurrentAuthority(
                approved,
                current,
                currentSemanticHash);
            return new ApplyResult(0, 0, 0, 0, true);
        }

        if (string.Equals(
                currentSemanticHash,
                approvedAfterHash,
                StringComparison.Ordinal))
        {
            VerifyAlreadyApplied(
                approved,
                current,
                approvedBeforeHash,
                approvedAfterHash);
            return new ApplyResult(0, 0, 0, 0, true);
        }

        Require(string.Equals(
                currentSemanticHash,
                approvedBeforeHash,
                StringComparison.Ordinal),
            "OUTPUT_LINE_PARTIAL_OR_STALE_STATE: current semantic authority "
            + "matches neither the approved Before nor the approved After hash.");
        Require(string.Equals(
                approved.ReportSourceDigest,
                current.SourceDigest,
                StringComparison.Ordinal),
            "OUTPUT_LINE_SOURCE_DIGEST_STALE: proposal source digest does not "
            + "match current authority.");
        Require(string.Equals(
                approved.ReportInspectedAssetDigest,
                current.InspectedAssetDigest,
                StringComparison.Ordinal),
            "OUTPUT_LINE_INSPECTED_DIGEST_STALE: proposal asset digest does not "
            + "match current recipe bytes.");

        PreflightResult preflight = Preflight(
            approved,
            current,
            approvedBeforeHash,
            approvedAfterHash);
        return Commit(preflight, approved);
    }

    private static PreflightResult Preflight(
        ApprovedArtifact approved,
        V27CanonicalOutputLineBackfillProposalSnapshot current,
        string beforeSemanticHash,
        string afterSemanticHash)
    {
        Require(approved.Rows.Count == ExpectedRows,
            $"Expected {ExpectedRows} approved rows, found {approved.Rows.Count}.");
        Require(current.Rows.Count == ExpectedRows,
            $"Expected {ExpectedRows} current rows, found {current.Rows.Count}.");

        Dictionary<string, V27CanonicalOutputLineBackfillProposalRow> currentByKey =
            UniqueRows(current.Rows, "current capture");
        Dictionary<string, ApprovedRow> approvedByKey =
            UniqueRows(approved.Rows, "approved artifact");
        Require(currentByKey.Count == approvedByKey.Count
                && currentByKey.Keys.All(approvedByKey.ContainsKey),
            "Approved/current output-line row keys do not match exactly.");

        int outputLineIdChanges = approved.Rows.Count(value =>
            !string.Equals(
                value.AuthoredOutputLineId,
                value.ProposedOutputLineId,
                StringComparison.Ordinal));
        ApprovedRow[] roleChanges = approved.Rows
            .Where(value => value.AuthoredRole != value.ProposedRole)
            .ToArray();
        Require(outputLineIdChanges == ExpectedOutputLineIdChanges,
            $"Expected {ExpectedOutputLineIdChanges} outputLineId changes, "
            + $"found {outputLineIdChanges}.");
        Require(roleChanges.Length == ExpectedOutputRoleChanges,
            $"Expected {ExpectedOutputRoleChanges} outputRole changes, "
            + $"found {roleChanges.Length}.");
        HashSet<string> actualRoleChangeKeys = roleChanges
            .Select(RoleChangeKey)
            .ToHashSet(StringComparer.Ordinal);
        Require(actualRoleChangeKeys.SetEquals(ExpectedRoleChangeKeys),
            "Approved role-change set differs from the six audited source "
            + "probabilistic secondary outputs.");
        Require(roleChanges.All(value =>
                value.AuthoredOutputOrdinal > 0
                && value.Probability > 0f
                && value.Probability < 1f
                && value.AuthoredRole == ProductionOutputRole.Main
                && value.ProposedRole == ProductionOutputRole.Byproduct),
            "An approved role change is not the audited "
            + "Main->Byproduct probabilistic-secondary shape.");

        foreach (IGrouping<string, ApprovedRow> recipeRows in approved.Rows
                     .GroupBy(value => value.RecipeId, StringComparer.Ordinal))
        {
            Require(recipeRows.Select(value => value.ProposedOutputLineId)
                    .Distinct(StringComparer.Ordinal).Count() == recipeRows.Count(),
                "Approved proposal contains duplicate line IDs in recipe: "
                + recipeRows.Key + ".");
        }

        List<OutputPatch> patches = new List<OutputPatch>(
            ExpectedOutputLineIdChanges);
        foreach (ApprovedRow row in approved.Rows)
        {
            Require(ProductionOutputDefinition.IsCanonicalOutputLineId(
                    row.ProposedOutputLineId),
                "Approved proposal contains a non-canonical line ID: "
                + row.ProposedOutputLineId + ".");
            Require(ProductionOutputRoleRules.IsPhysical(row.ProposedRole),
                "Approved proposal routes DeclaredLoss as a physical output.");
            V27CanonicalOutputLineBackfillProposalRow currentRow =
                currentByKey[RowKey(row.RecipeId, row.AuthoredOutputOrdinal)];
            RequireMatchesApprovedBefore(currentRow, row);

            bool changeLineId = !string.Equals(
                row.AuthoredOutputLineId,
                row.ProposedOutputLineId,
                StringComparison.Ordinal);
            bool changeRole = row.AuthoredRole != row.ProposedRole;
            if (!changeLineId && !changeRole)
                continue;

            ProductionRecipeSO recipe = AssetDatabase
                .LoadAssetAtPath<ProductionRecipeSO>(row.SourceAuthority)
                ?? throw new InvalidOperationException(
                    "Approved recipe asset is missing: "
                    + row.SourceAuthority + ".");
            Require(string.Equals(recipe.RecipeId, row.RecipeId,
                    StringComparison.Ordinal),
                "Approved source path resolves to a different recipe: "
                + row.SourceAuthority + ".");
            Require(!EditorUtility.IsDirty(recipe),
                "OUTPUT_LINE_DIRTY_TARGET: save or revert the target recipe "
                + "before ApplyApproved: " + row.SourceAuthority + ".");
            Require(recipe.FlowRole == ProductionFlowRole.Source || !changeRole,
                "Only Source recipes may receive the six approved role changes: "
                + row.RecipeId + ".");
            VerifySerializedBefore(recipe, row);
            string guid = AssetDatabase.AssetPathToGUID(row.SourceAuthority);
            Require(!string.IsNullOrWhiteSpace(guid),
                "Target recipe has no stable asset GUID: "
                + row.SourceAuthority + ".");
            patches.Add(new OutputPatch(
                recipe,
                row,
                changeLineId,
                changeRole,
                guid));
        }

        Require(patches.Count(value => value.ChangeLineId)
                == ExpectedOutputLineIdChanges,
            "Preflight outputLineId patch count drifted.");
        Require(patches.Count(value => value.ChangeRole)
                == ExpectedOutputRoleChanges,
            "Preflight outputRole patch count drifted.");
        RecipePatchSet[] patchSets = patches
            .GroupBy(value => value.Row.SourceAuthority, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new RecipePatchSet(
                group.Key,
                group.First().Recipe,
                group.First().AssetGuid,
                group.OrderBy(value => value.Row.AuthoredOutputOrdinal).ToArray()))
            .ToArray();
        foreach (RecipePatchSet patchSet in patchSets)
        {
            Require(patchSet.Patches.All(value =>
                    ReferenceEquals(value.Recipe, patchSet.Recipe)
                    && string.Equals(value.AssetGuid, patchSet.AssetGuid,
                        StringComparison.Ordinal)),
                "A changed path resolves to inconsistent recipe/GUID authority: "
                + patchSet.AssetPath + ".");
        }

        return new PreflightResult(
            patchSets,
            beforeSemanticHash,
            afterSemanticHash,
            current.SourceDigest,
            current.InspectedAssetDigest);
    }

    private static ApplyResult Commit(
        PreflightResult preflight,
        ApprovedArtifact approved)
    {
        SortedDictionary<string, RollbackEntry> rollback = new(
            StringComparer.Ordinal);
        foreach (RecipePatchSet patchSet in preflight.PatchSets)
        {
            string absolute = ProjectAbsolute(patchSet.AssetPath);
            string metaAbsolute = absolute + ".meta";
            Require(File.Exists(absolute) && File.Exists(metaAbsolute),
                "Target asset or meta is missing: " + patchSet.AssetPath + ".");
            rollback.Add(
                patchSet.AssetPath,
                new RollbackEntry(
                    File.ReadAllBytes(absolute),
                    File.ReadAllBytes(metaAbsolute),
                    patchSet.AssetGuid));
        }

        int outputLineIdChanges = 0;
        int outputRoleChanges = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (RecipePatchSet patchSet in preflight.PatchSets)
                {
                    SerializedObject serialized = new SerializedObject(
                        patchSet.Recipe);
                    SerializedProperty outputs = serialized.FindProperty("outputs")
                        ?? throw new InvalidOperationException(
                            "Target recipe lost serialized outputs: "
                            + patchSet.Recipe.RecipeId + ".");
                    bool changed = false;
                    foreach (OutputPatch patch in patchSet.Patches)
                    {
                        SerializedProperty element = outputs.GetArrayElementAtIndex(
                            patch.Row.AuthoredOutputOrdinal);
                        SerializedProperty lineId = element.FindPropertyRelative(
                            "outputLineId")
                            ?? throw new InvalidOperationException(
                                "Target output lost outputLineId authority.");
                        SerializedProperty role = element.FindPropertyRelative("role")
                            ?? throw new InvalidOperationException(
                                "Target output lost role authority.");
                        if (patch.ChangeLineId)
                        {
                            Require(string.Equals(
                                    lineId.stringValue ?? string.Empty,
                                    patch.Row.AuthoredOutputLineId,
                                    StringComparison.Ordinal),
                                "Target outputLineId changed after preflight.");
                            lineId.stringValue = patch.Row.ProposedOutputLineId;
                            outputLineIdChanges++;
                            changed = true;
                        }
                        if (patch.ChangeRole)
                        {
                            Require(role.intValue == (int)patch.Row.AuthoredRole,
                                "Target output role changed after preflight.");
                            role.intValue = (int)patch.Row.ProposedRole;
                            outputRoleChanges++;
                            changed = true;
                        }
                    }
                    Require(changed,
                        "A patch set contained no actual approved changes.");
                    Require(serialized.ApplyModifiedPropertiesWithoutUndo(),
                        "Unity rejected approved output-line changes for: "
                        + patchSet.AssetPath + ".");
                    EditorUtility.SetDirty(patchSet.Recipe);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Require(outputLineIdChanges == ExpectedOutputLineIdChanges,
                "Committed outputLineId count drifted.");
            Require(outputRoleChanges == ExpectedOutputRoleChanges,
                "Committed outputRole count drifted.");
            AssetDatabase.SaveAssets();

            foreach (RecipePatchSet patchSet in preflight.PatchSets)
            {
                RollbackEntry before = rollback[patchSet.AssetPath];
                Require(string.Equals(
                        AssetDatabase.AssetPathToGUID(patchSet.AssetPath),
                        before.AssetGuid,
                        StringComparison.Ordinal),
                    "Asset GUID changed during output-line application: "
                    + patchSet.AssetPath + ".");
                Require(File.ReadAllBytes(ProjectAbsolute(patchSet.AssetPath) + ".meta")
                        .SequenceEqual(before.MetaBytes),
                    "Asset meta changed during output-line application: "
                    + patchSet.AssetPath + ".");
            }

            V27CanonicalOutputLineBackfillProposalSnapshot after =
                V27CanonicalOutputLineBackfillProposalDebugScenarios
                    .CaptureProposalSnapshotForAudit();
            string actualAfterSemanticHash = ComputeSemanticHash(
                after.Rows,
                useProposed: false);
            Require(string.Equals(
                    actualAfterSemanticHash,
                    preflight.AfterSemanticHash,
                    StringComparison.Ordinal),
                "Applied output-line semantic hash differs from the approved After.");
            WriteManifest(
                approved,
                preflight,
                after,
                outputLineIdChanges,
                outputRoleChanges);
            return new ApplyResult(
                preflight.PatchSets.Count,
                outputLineIdChanges,
                outputRoleChanges,
                1,
                false);
        }
        catch
        {
            foreach (KeyValuePair<string, RollbackEntry> pair in rollback)
            {
                string absolute = ProjectAbsolute(pair.Key);
                File.WriteAllBytes(absolute, pair.Value.AssetBytes);
                File.WriteAllBytes(absolute + ".meta", pair.Value.MetaBytes);
            }
            foreach (string path in rollback.Keys)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Require(string.Equals(
                        AssetDatabase.AssetPathToGUID(path),
                        rollback[path].AssetGuid,
                        StringComparison.Ordinal),
                    "Rollback failed to restore asset GUID: " + path + ".");
            }
            throw;
        }
    }

    private static void VerifyAlreadyApplied(
        ApprovedArtifact approved,
        V27CanonicalOutputLineBackfillProposalSnapshot current,
        string beforeSemanticHash,
        string afterSemanticHash)
    {
        Require(File.Exists(ProjectAbsolute(ManifestPath)),
            "Already-applied semantic state has no exact apply manifest.");
        IReadOnlyDictionary<string, string> manifest = ParseKeyValueReport(
            File.ReadAllText(ProjectAbsolute(ManifestPath),
                new UTF8Encoding(false, true)));
        RequireValue(manifest, "approvalCsvDigest", approved.CsvByteDigest);
        RequireValue(manifest, "approvalReportDigest", approved.ReportByteDigest);
        RequireValue(manifest, "beforeSemanticHash", beforeSemanticHash);
        RequireValue(manifest, "afterSemanticHash", afterSemanticHash);
        RequireValue(manifest, "afterSourceDigest", current.SourceDigest);
        RequireValue(
            manifest,
            "afterInspectedAssetDigest",
            current.InspectedAssetDigest);
        RequireValue(
            manifest,
            "outputLineIdChanges",
            ExpectedOutputLineIdChanges.ToString(CultureInfo.InvariantCulture));
        RequireValue(
            manifest,
            "outputRoleChanges",
            ExpectedOutputRoleChanges.ToString(CultureInfo.InvariantCulture));
    }

    private static bool IsFullyCanonicalCurrentAuthority(
        IReadOnlyList<V27CanonicalOutputLineBackfillProposalRow> rows)
    {
        if (rows == null || rows.Count != ExpectedRows)
            return false;

        HashSet<string> lineIds = new(StringComparer.Ordinal);
        foreach (V27CanonicalOutputLineBackfillProposalRow row in rows)
        {
            if (!row.HasCanonicalAuthoredLine
                || !ProductionOutputRoleRules.IsPhysical(row.AuthoredRole))
            {
                return false;
            }

            string expected = ProductionOutputLineAuthoring.BuildStableId(
                row.RecipeId,
                row.AuthoredOutputOrdinal,
                row.ItemId,
                row.AuthoredRole);
            if (!string.Equals(
                    row.AuthoredOutputLineId,
                    expected,
                    StringComparison.Ordinal)
                || !lineIds.Add(row.AuthoredOutputLineId))
            {
                return false;
            }
        }

        return true;
    }

    private static void VerifyAndWriteCurrentAuthority(
        ApprovedArtifact approved,
        V27CanonicalOutputLineBackfillProposalSnapshot current,
        string currentSemanticHash)
    {
        Require(approved.Rows.Count == ExpectedRows,
            "Historical output-line approval row count drifted.");
        Require(approved.Rows.Count(value => !string.Equals(
                    value.AuthoredOutputLineId,
                    value.ProposedOutputLineId,
                    StringComparison.Ordinal)) == ExpectedOutputLineIdChanges,
            "Historical output-line approval no longer proves 353 ID changes.");
        Require(approved.Rows.Count(value =>
                    value.AuthoredRole != value.ProposedRole)
                == ExpectedOutputRoleChanges,
            "Historical output-line approval no longer proves six role changes.");

        Dictionary<string, ApprovedRow> approvedByKey = UniqueRows(
            approved.Rows,
            "historical approved output-line");
        Require(approvedByKey.Count == current.Rows.Count,
            "Current output-line row count differs from the reviewed approval.");
        foreach (V27CanonicalOutputLineBackfillProposalRow row in current.Rows)
        {
            string key = RowKey(row.RecipeId, row.AuthoredOutputOrdinal);
            Require(approvedByKey.TryGetValue(key, out ApprovedRow reviewed),
                "Current output-line row was not present in the reviewed approval: "
                + key + ".");
            string expectedItemId = ExpectedPostApprovalOutputItemByKey
                .TryGetValue(key, out string migratedItemId)
                    ? migratedItemId
                    : reviewed.ItemId;
            string expectedLineId = ExpectedPostApprovalOutputItemByKey
                .ContainsKey(key)
                    ? ProductionOutputLineAuthoring.BuildStableId(
                        row.RecipeId,
                        row.AuthoredOutputOrdinal,
                        expectedItemId,
                        reviewed.ProposedRole)
                    : reviewed.ProposedOutputLineId;
            Require(string.Equals(row.ItemId, expectedItemId,
                        StringComparison.Ordinal)
                    && row.Amount == reviewed.Amount
                    && BitConverter.SingleToInt32Bits(row.Probability)
                        == BitConverter.SingleToInt32Bits(reviewed.Probability)
                    && row.AuthoredRole == reviewed.ProposedRole
                    && string.Equals(row.AuthoredOutputLineId, expectedLineId,
                        StringComparison.Ordinal),
                "Current output-line authority differs from the reviewed approval "
                + "and its four explicit post-approval migrations: " + key + ".");
        }
        Require(current.Rows.Count(value =>
                    value.AuthoredRole == ProductionOutputRole.Main) == 351
                && current.Rows.Count(value =>
                    value.AuthoredRole == ProductionOutputRole.Byproduct) == 6
                && current.Rows.All(value => value.AuthoredRole is
                    ProductionOutputRole.Main or ProductionOutputRole.Byproduct),
            "Current output role distribution differs from the reviewed 351/6 authority.");

        HashSet<string> currentAuditedSecondaryKeys = current.Rows
            .Where(value => value.AuthoredRole == ProductionOutputRole.Byproduct)
            .Select(RoleChangeKey)
            .Where(ExpectedRoleChangeKeys.Contains)
            .ToHashSet(StringComparer.Ordinal);
        Require(currentAuditedSecondaryKeys.SetEquals(ExpectedRoleChangeKeys),
            "The six audited probabilistic Source outputs are not authored as Byproduct.");

        StringBuilder report = new StringBuilder(1024);
        report.Append("RESULT=PASS; phase=canonical-output-line-current-authority; ")
            .Append("assetMutations=0\n")
            .Append("recipes=355\n")
            .Append("physicalOutputLines=").Append(current.Rows.Count).Append('\n')
            .Append("canonicalOutputLines=").Append(current.Rows.Count).Append('\n')
            .Append("missingOutputLines=0\n")
            .Append("duplicateOutputLines=0\n")
            .Append("auditedSecondaryRoleCorrections=")
            .Append(ExpectedOutputRoleChanges).Append('\n')
            .Append("reviewedPostApprovalMigrations=")
            .Append(ExpectedPostApprovalOutputItemByKey.Count).Append('\n')
            .Append("historicalOutputLineIdChanges=")
            .Append(ExpectedOutputLineIdChanges).Append('\n')
            .Append("currentSemanticHash=").Append(currentSemanticHash).Append('\n')
            .Append("currentSourceDigest=").Append(current.SourceDigest).Append('\n')
            .Append("currentInspectedAssetDigest=")
            .Append(current.InspectedAssetDigest).Append('\n')
            .Append("approvalCsvDigest=").Append(approved.CsvByteDigest).Append('\n')
            .Append("approvalReportDigest=").Append(approved.ReportByteDigest).Append('\n')
            .Append("secondApplyChanges=0\n");
        byte[] bytes = new UTF8Encoding(false).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(
            CurrentAuthorityManifestPath,
            stream => stream.Write(bytes, 0, bytes.Length));
    }

    private static ApprovedArtifact LoadApprovedArtifact()
    {
        string csvAbsolute = ProjectAbsolute(
            V27CanonicalOutputLineBackfillProposalDebugScenarios.CsvPath);
        string reportAbsolute = ProjectAbsolute(
            V27CanonicalOutputLineBackfillProposalDebugScenarios.ReportPath);
        Require(File.Exists(csvAbsolute) && File.Exists(reportAbsolute),
            "Approved output-line proposal CSV/report is missing. Run and review "
            + "the AuditOnly proposal command first.");
        byte[] csvBytes = File.ReadAllBytes(csvAbsolute);
        byte[] reportBytes = File.ReadAllBytes(reportAbsolute);
        List<string[]> records = ParseRfc4180(csvBytes);
        Require(records.Count > 0,
            "Approved output-line proposal CSV is empty.");
        Require(records[0].SequenceEqual(ExpectedHeader, StringComparer.Ordinal),
            "Approved output-line proposal CSV schema/header drifted.");
        ApprovedRow[] rows = records.Skip(1)
            .Select(ParseApprovedRow)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ThenBy(value => value.AuthoredOutputOrdinal)
            .ToArray();
        Require(rows.Length == ExpectedRows,
            $"Expected {ExpectedRows} proposal rows, found {rows.Length}.");
        Require(records.Skip(1).Select(ParseApprovedRow)
                .SequenceEqual(rows, ApprovedRowComparer.Instance),
            "Approved proposal rows are not recipe-ID/ordinal sorted.");
        IReadOnlyDictionary<string, string> report = ParseKeyValueReport(
            new UTF8Encoding(false, true).GetString(reportBytes));
        return new ApprovedArtifact(
            rows,
            RequireValue(report, "sourceDigest"),
            RequireValue(report, "inspectedAssetDigest"),
            HashBytes(csvBytes),
            HashBytes(reportBytes));
    }

    private static ApprovedRow ParseApprovedRow(string[] fields)
    {
        Require(fields.Length == ExpectedHeader.Length,
            "Approved proposal row has an unexpected field count.");
        Require(fields[0] == "v27.production.output-line-backfill.1",
            "Approved proposal row has an unsupported schema version.");
        Require(int.TryParse(fields[2], NumberStyles.None,
                CultureInfo.InvariantCulture, out int ordinal)
                && ordinal >= 0,
            "Approved proposal has invalid authored output ordinal.");
        Require(int.TryParse(fields[4], NumberStyles.None,
                CultureInfo.InvariantCulture, out int amount)
                && amount > 0,
            "Approved proposal has invalid amount.");
        Require(float.TryParse(fields[5], NumberStyles.Float,
                CultureInfo.InvariantCulture, out float probability)
                && float.IsFinite(probability)
                && probability >= 0f
                && probability <= 1f,
            "Approved proposal has invalid probability.");
        Require(Enum.TryParse(fields[7], false,
                out ProductionOutputRole authoredRole)
                && Enum.IsDefined(typeof(ProductionOutputRole), authoredRole),
            "Approved proposal has invalid authored role.");
        Require(Enum.TryParse(fields[9], false,
                out ProductionOutputRole proposedRole)
                && Enum.IsDefined(typeof(ProductionOutputRole), proposedRole),
            "Approved proposal has invalid proposed role.");
        Require(!string.IsNullOrWhiteSpace(fields[1])
                && string.Equals(fields[1], fields[1].Trim(),
                    StringComparison.Ordinal),
            "Approved proposal has non-canonical recipe ID.");
        Require(!string.IsNullOrWhiteSpace(fields[3])
                && string.Equals(fields[3], fields[3].Trim(),
                    StringComparison.Ordinal),
            "Approved proposal has non-canonical item ID.");
        Require(ProductionOutputDefinition.IsCanonicalOutputLineId(fields[8]),
            "Approved proposal has non-canonical proposed output-line ID.");
        Require(!string.IsNullOrWhiteSpace(fields[12])
                && string.Equals(fields[12], CanonicalPath(fields[12]),
                    StringComparison.Ordinal),
            "Approved proposal has non-canonical source authority path.");
        Require(IsLowerHexDigest(fields[13]),
            "Approved proposal has invalid source digest.");
        return new ApprovedRow(
            fields[1],
            ordinal,
            fields[3],
            amount,
            probability,
            fields[6],
            authoredRole,
            fields[8],
            proposedRole,
            fields[10],
            fields[11],
            CanonicalPath(fields[12]),
            fields[13]);
    }

    private static void RequireMatchesApprovedBefore(
        V27CanonicalOutputLineBackfillProposalRow current,
        ApprovedRow approved)
    {
        Require(string.Equals(current.RecipeId, approved.RecipeId,
                    StringComparison.Ordinal)
                && current.AuthoredOutputOrdinal == approved.AuthoredOutputOrdinal
                && string.Equals(current.ItemId, approved.ItemId,
                    StringComparison.Ordinal)
                && current.Amount == approved.Amount
                && BitConverter.SingleToInt32Bits(current.Probability)
                    == BitConverter.SingleToInt32Bits(approved.Probability)
                && string.Equals(current.AuthoredOutputLineId,
                    approved.AuthoredOutputLineId, StringComparison.Ordinal)
                && current.AuthoredRole == approved.AuthoredRole
                && string.Equals(current.ProposedOutputLineId,
                    approved.ProposedOutputLineId, StringComparison.Ordinal)
                && current.ProposedRole == approved.ProposedRole
                && string.Equals(current.ProposalReason,
                    approved.ProposalReason, StringComparison.Ordinal)
                && string.Equals(current.ProposalDisposition,
                    approved.ProposalDisposition, StringComparison.Ordinal)
                && string.Equals(current.SourceAuthority,
                    approved.SourceAuthority, StringComparison.Ordinal)
                && string.Equals(current.SourceDigest,
                    approved.SourceDigest, StringComparison.Ordinal),
            "Approved proposal row is stale or differs from current capture: "
            + RowKey(approved.RecipeId, approved.AuthoredOutputOrdinal) + ".");
    }

    private static void VerifySerializedBefore(
        ProductionRecipeSO recipe,
        ApprovedRow row)
    {
        SerializedObject serialized = new SerializedObject(recipe);
        SerializedProperty outputs = serialized.FindProperty("outputs")
            ?? throw new InvalidOperationException(
                "Recipe lost serialized outputs: " + row.RecipeId + ".");
        Require(row.AuthoredOutputOrdinal < outputs.arraySize,
            "Approved ordinal is outside current outputs: "
            + RowKey(row.RecipeId, row.AuthoredOutputOrdinal) + ".");
        SerializedProperty element = outputs.GetArrayElementAtIndex(
            row.AuthoredOutputOrdinal);
        Require((element.FindPropertyRelative("outputLineId")?.stringValue
                    ?? string.Empty) == row.AuthoredOutputLineId
                && (element.FindPropertyRelative("role")?.intValue ?? -1)
                    == (int)row.AuthoredRole
                && (element.FindPropertyRelative("itemId")?.stringValue
                    ?? string.Empty) == row.ItemId
                && (element.FindPropertyRelative("amount")?.intValue ?? -1)
                    == row.Amount
                && BitConverter.SingleToInt32Bits(
                    element.FindPropertyRelative("probability")?.floatValue
                    ?? float.NaN)
                    == BitConverter.SingleToInt32Bits(row.Probability),
            "Serialized output authority differs from approved Before: "
            + RowKey(row.RecipeId, row.AuthoredOutputOrdinal) + ".");
    }

    private static string ComputeSemanticHash(
        IEnumerable<V27CanonicalOutputLineBackfillProposalRow> rows,
        bool useProposed)
    {
        return HashSemanticTokens(rows
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ThenBy(value => value.AuthoredOutputOrdinal)
            .Select(value => SemanticToken(
                value.RecipeId,
                value.AuthoredOutputOrdinal,
                value.ItemId,
                value.Amount,
                value.Probability,
                useProposed
                    ? value.ProposedOutputLineId
                    : value.AuthoredOutputLineId,
                useProposed ? value.ProposedRole : value.AuthoredRole)));
    }

    private static string ComputeSemanticHash(
        IEnumerable<ApprovedRow> rows,
        bool useProposed)
    {
        return HashSemanticTokens(rows
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ThenBy(value => value.AuthoredOutputOrdinal)
            .Select(value => SemanticToken(
                value.RecipeId,
                value.AuthoredOutputOrdinal,
                value.ItemId,
                value.Amount,
                value.Probability,
                useProposed
                    ? value.ProposedOutputLineId
                    : value.AuthoredOutputLineId,
                useProposed ? value.ProposedRole : value.AuthoredRole)));
    }

    private static string SemanticToken(
        string recipeId,
        int ordinal,
        string itemId,
        int amount,
        float probability,
        string outputLineId,
        ProductionOutputRole role) =>
        recipeId + "\u001f" + ordinal.ToString(CultureInfo.InvariantCulture)
        + "\u001f" + itemId
        + "\u001f" + amount.ToString(CultureInfo.InvariantCulture)
        + "\u001f" + probability.ToString("R", CultureInfo.InvariantCulture)
        + "\u001f" + outputLineId
        + "\u001f" + ((int)role).ToString(CultureInfo.InvariantCulture);

    private static string HashSemanticTokens(IEnumerable<string> tokens)
    {
        using SHA256 sha = SHA256.Create();
        foreach (string token in tokens)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(token + "\n");
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Hex(sha.Hash);
    }

    private static void WriteManifest(
        ApprovedArtifact approved,
        PreflightResult preflight,
        V27CanonicalOutputLineBackfillProposalSnapshot after,
        int outputLineIdChanges,
        int outputRoleChanges)
    {
        StringBuilder manifest = new StringBuilder(4096);
        manifest.Append("RESULT=PASS; phase=canonical-output-line-apply; ")
            .Append("assetMutation=approved-only\n")
            .Append("approvalCsvDigest=").Append(approved.CsvByteDigest).Append('\n')
            .Append("approvalReportDigest=").Append(approved.ReportByteDigest).Append('\n')
            .Append("proposalSourceDigest=").Append(preflight.BeforeSourceDigest).Append('\n')
            .Append("beforeInspectedAssetDigest=")
            .Append(preflight.BeforeInspectedAssetDigest).Append('\n')
            .Append("afterSourceDigest=").Append(after.SourceDigest).Append('\n')
            .Append("afterInspectedAssetDigest=")
            .Append(after.InspectedAssetDigest).Append('\n')
            .Append("beforeSemanticHash=").Append(preflight.BeforeSemanticHash).Append('\n')
            .Append("afterSemanticHash=").Append(preflight.AfterSemanticHash).Append('\n')
            .Append("outputLineIdChanges=").Append(outputLineIdChanges).Append('\n')
            .Append("outputRoleChanges=").Append(outputRoleChanges).Append('\n')
            .Append("changedAssets=").Append(preflight.PatchSets.Count).Append('\n')
            .Append("saveAssetsCalls=1\n")
            .Append("secondApplyChanges=0\n");
        for (int index = 0; index < preflight.PatchSets.Count; index++)
        {
            RecipePatchSet patchSet = preflight.PatchSets[index];
            manifest.Append("changedPath[")
                .Append(index.ToString("D3", CultureInfo.InvariantCulture))
                .Append("]=path:").Append(patchSet.AssetPath)
                .Append("|assetGuid:").Append(patchSet.AssetGuid)
                .Append("|outputLineIdChanges:")
                .Append(patchSet.Patches.Count(value => value.ChangeLineId))
                .Append("|outputRoleChanges:")
                .Append(patchSet.Patches.Count(value => value.ChangeRole))
                .Append('\n');
        }
        byte[] bytes = new UTF8Encoding(false).GetBytes(manifest.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(ManifestPath, stream =>
            stream.Write(bytes, 0, bytes.Length));
    }

    private static List<string[]> ParseRfc4180(byte[] bytes)
    {
        Require(bytes.Length < 3
                || bytes[0] != 0xef || bytes[1] != 0xbb || bytes[2] != 0xbf,
            "Approved proposal CSV must be UTF-8 without BOM.");
        string text = new UTF8Encoding(false, true).GetString(bytes);
        List<string[]> records = new List<string[]>();
        List<string> fields = new List<string>();
        StringBuilder field = new StringBuilder();
        bool quoted = false;
        bool quoteClosed = false;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                        quoteClosed = true;
                    }
                }
                else
                {
                    field.Append(character);
                }
                continue;
            }
            if (quoteClosed && character != ',' && character != '\r')
                throw new InvalidOperationException(
                    "Unexpected character after a quoted CSV field.");
            if (character == '"')
            {
                Require(field.Length == 0 && !quoteClosed,
                    "CSV quote appeared inside an unquoted field.");
                quoted = true;
            }
            else if (character == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
                quoteClosed = false;
            }
            else if (character == '\r')
            {
                Require(index + 1 < text.Length && text[index + 1] == '\n',
                    "CSV record delimiter must be CRLF.");
                index++;
                fields.Add(field.ToString());
                field.Clear();
                records.Add(fields.ToArray());
                fields.Clear();
                quoteClosed = false;
            }
            else if (character == '\n')
            {
                throw new InvalidOperationException(
                    "CSV contains a bare LF record delimiter.");
            }
            else
            {
                field.Append(character);
            }
        }
        Require(!quoted && !quoteClosed && field.Length == 0 && fields.Count == 0,
            "CSV must end with a complete CRLF-delimited record.");
        return records;
    }

    private static IReadOnlyDictionary<string, string> ParseKeyValueReport(
        string report)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string line in report.Replace("\r\n", "\n").Split('\n'))
        {
            foreach (string token in line.Split(';'))
            {
                string trimmed = token.Trim();
                int separator = trimmed.IndexOf('=');
                if (separator <= 0)
                    continue;
                string key = trimmed.Substring(0, separator);
                string value = trimmed.Substring(separator + 1);
                if (!values.TryAdd(key, value))
                    throw new InvalidOperationException(
                        "Duplicate report/manifest key: " + key + ".");
            }
        }
        return values;
    }

    private static string RequireValue(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        Require(values.TryGetValue(key, out string value)
                && !string.IsNullOrWhiteSpace(value),
            "Required report/manifest key is missing: " + key + ".");
        return value;
    }

    private static void RequireValue(
        IReadOnlyDictionary<string, string> values,
        string key,
        string expected)
    {
        Require(string.Equals(RequireValue(values, key), expected,
                StringComparison.Ordinal),
            "Report/manifest value mismatch for key: " + key + ".");
    }

    private static Dictionary<string, V27CanonicalOutputLineBackfillProposalRow>
        UniqueRows(
            IEnumerable<V27CanonicalOutputLineBackfillProposalRow> rows,
            string label)
    {
        Dictionary<string, V27CanonicalOutputLineBackfillProposalRow> result =
            new(StringComparer.Ordinal);
        foreach (V27CanonicalOutputLineBackfillProposalRow row in rows)
        {
            string key = RowKey(row.RecipeId, row.AuthoredOutputOrdinal);
            if (!result.TryAdd(key, row))
                throw new InvalidOperationException(
                    "Duplicate " + label + " row: " + key + ".");
        }
        return result;
    }

    private static Dictionary<string, ApprovedRow> UniqueRows(
        IEnumerable<ApprovedRow> rows,
        string label)
    {
        Dictionary<string, ApprovedRow> result = new(StringComparer.Ordinal);
        foreach (ApprovedRow row in rows)
        {
            string key = RowKey(row.RecipeId, row.AuthoredOutputOrdinal);
            if (!result.TryAdd(key, row))
                throw new InvalidOperationException(
                    "Duplicate " + label + " row: " + key + ".");
        }
        return result;
    }

    private static string RowKey(string recipeId, int ordinal) =>
        recipeId + "|" + ordinal.ToString(CultureInfo.InvariantCulture);

    private static string RoleChangeKey(ApprovedRow row) =>
        row.RecipeId + "|"
        + row.AuthoredOutputOrdinal.ToString(CultureInfo.InvariantCulture)
        + "|" + row.ItemId + "|"
        + row.Probability.ToString("R", CultureInfo.InvariantCulture);

    private static string RoleChangeKey(
        V27CanonicalOutputLineBackfillProposalRow row) =>
        row.RecipeId + "|"
        + row.AuthoredOutputOrdinal.ToString(CultureInfo.InvariantCulture)
        + "|" + row.ItemId + "|"
        + row.Probability.ToString("R", CultureInfo.InvariantCulture);

    private static bool IsLowerHexDigest(string value) =>
        value?.Length == 64 && value.All(character =>
            character >= '0' && character <= '9'
            || character >= 'a' && character <= 'f');

    private static string HashBytes(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(bytes));
    }

    private static string Hex(IEnumerable<byte> bytes) => string.Concat(
        bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));

    private static string ProjectAbsolute(string projectRelativePath)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        return Path.Combine(
            root,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string CanonicalPath(string path) =>
        (path ?? string.Empty).Replace('\\', '/');

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    internal readonly struct ApplyResult
    {
        public ApplyResult(
            int changedAssetCount,
            int outputLineIdChanges,
            int outputRoleChanges,
            int saveAssetsCalls,
            bool alreadyApplied)
        {
            ChangedAssetCount = changedAssetCount;
            OutputLineIdChanges = outputLineIdChanges;
            OutputRoleChanges = outputRoleChanges;
            SaveAssetsCalls = saveAssetsCalls;
            AlreadyApplied = alreadyApplied;
        }

        public int ChangedAssetCount { get; }
        public int OutputLineIdChanges { get; }
        public int OutputRoleChanges { get; }
        public int SaveAssetsCalls { get; }
        public bool AlreadyApplied { get; }
    }

    private sealed class ApprovedArtifact
    {
        public ApprovedArtifact(
            IReadOnlyList<ApprovedRow> rows,
            string reportSourceDigest,
            string reportInspectedAssetDigest,
            string csvByteDigest,
            string reportByteDigest)
        {
            Rows = rows;
            ReportSourceDigest = reportSourceDigest;
            ReportInspectedAssetDigest = reportInspectedAssetDigest;
            CsvByteDigest = csvByteDigest;
            ReportByteDigest = reportByteDigest;
        }

        public IReadOnlyList<ApprovedRow> Rows { get; }
        public string ReportSourceDigest { get; }
        public string ReportInspectedAssetDigest { get; }
        public string CsvByteDigest { get; }
        public string ReportByteDigest { get; }
    }

    private sealed class ApprovedRow
    {
        public ApprovedRow(
            string recipeId,
            int authoredOutputOrdinal,
            string itemId,
            int amount,
            float probability,
            string authoredOutputLineId,
            ProductionOutputRole authoredRole,
            string proposedOutputLineId,
            ProductionOutputRole proposedRole,
            string proposalReason,
            string proposalDisposition,
            string sourceAuthority,
            string sourceDigest)
        {
            RecipeId = recipeId;
            AuthoredOutputOrdinal = authoredOutputOrdinal;
            ItemId = itemId;
            Amount = amount;
            Probability = probability;
            AuthoredOutputLineId = authoredOutputLineId;
            AuthoredRole = authoredRole;
            ProposedOutputLineId = proposedOutputLineId;
            ProposedRole = proposedRole;
            ProposalReason = proposalReason;
            ProposalDisposition = proposalDisposition;
            SourceAuthority = sourceAuthority;
            SourceDigest = sourceDigest;
        }

        public string RecipeId { get; }
        public int AuthoredOutputOrdinal { get; }
        public string ItemId { get; }
        public int Amount { get; }
        public float Probability { get; }
        public string AuthoredOutputLineId { get; }
        public ProductionOutputRole AuthoredRole { get; }
        public string ProposedOutputLineId { get; }
        public ProductionOutputRole ProposedRole { get; }
        public string ProposalReason { get; }
        public string ProposalDisposition { get; }
        public string SourceAuthority { get; }
        public string SourceDigest { get; }
    }

    private sealed class ApprovedRowComparer : IEqualityComparer<ApprovedRow>
    {
        public static readonly ApprovedRowComparer Instance = new();

        public bool Equals(ApprovedRow left, ApprovedRow right) =>
            left != null && right != null
            && string.Equals(left.RecipeId, right.RecipeId, StringComparison.Ordinal)
            && left.AuthoredOutputOrdinal == right.AuthoredOutputOrdinal;

        public int GetHashCode(ApprovedRow value) => HashCode.Combine(
            value.RecipeId,
            value.AuthoredOutputOrdinal);
    }

    private sealed class OutputPatch
    {
        public OutputPatch(
            ProductionRecipeSO recipe,
            ApprovedRow row,
            bool changeLineId,
            bool changeRole,
            string assetGuid)
        {
            Recipe = recipe;
            Row = row;
            ChangeLineId = changeLineId;
            ChangeRole = changeRole;
            AssetGuid = assetGuid;
        }

        public ProductionRecipeSO Recipe { get; }
        public ApprovedRow Row { get; }
        public bool ChangeLineId { get; }
        public bool ChangeRole { get; }
        public string AssetGuid { get; }
    }

    private sealed class RecipePatchSet
    {
        public RecipePatchSet(
            string assetPath,
            ProductionRecipeSO recipe,
            string assetGuid,
            IReadOnlyList<OutputPatch> patches)
        {
            AssetPath = assetPath;
            Recipe = recipe;
            AssetGuid = assetGuid;
            Patches = patches;
        }

        public string AssetPath { get; }
        public ProductionRecipeSO Recipe { get; }
        public string AssetGuid { get; }
        public IReadOnlyList<OutputPatch> Patches { get; }
    }

    private sealed class PreflightResult
    {
        public PreflightResult(
            IReadOnlyList<RecipePatchSet> patchSets,
            string beforeSemanticHash,
            string afterSemanticHash,
            string beforeSourceDigest,
            string beforeInspectedAssetDigest)
        {
            PatchSets = patchSets;
            BeforeSemanticHash = beforeSemanticHash;
            AfterSemanticHash = afterSemanticHash;
            BeforeSourceDigest = beforeSourceDigest;
            BeforeInspectedAssetDigest = beforeInspectedAssetDigest;
        }

        public IReadOnlyList<RecipePatchSet> PatchSets { get; }
        public string BeforeSemanticHash { get; }
        public string AfterSemanticHash { get; }
        public string BeforeSourceDigest { get; }
        public string BeforeInspectedAssetDigest { get; }
    }

    private sealed class RollbackEntry
    {
        public RollbackEntry(
            byte[] assetBytes,
            byte[] metaBytes,
            string assetGuid)
        {
            AssetBytes = assetBytes;
            MetaBytes = metaBytes;
            AssetGuid = assetGuid;
        }

        public byte[] AssetBytes { get; }
        public byte[] MetaBytes { get; }
        public string AssetGuid { get; }
    }
}
#endif
