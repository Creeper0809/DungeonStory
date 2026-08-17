#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

public static class V27BalanceAssetRollbackDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-asset-rollback.txt";
    private const string FixtureAssetPath =
        "Assets/__V27BalanceRollbackFixture.asset";

    [MenuItem("DungeonStory/V27/Verify Asset Application Atomic Rollback")]
    public static void RunFromMenu()
    {
        string report;
        Exception failure = null;
        try
        {
            report = RunAll();
        }
        catch (Exception exception)
        {
            failure = exception;
            report = "RESULT=FAIL; reason=" + exception.Message + "\n";
        }

        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
        {
            using StreamWriter writer = new(
                stream,
                new UTF8Encoding(false, true),
                4096,
                leaveOpen: true);
            writer.Write(report);
            writer.Flush();
        });
        AssetDatabase.Refresh();
        if (failure == null)
            Debug.Log(report);
        else
            Debug.LogError(report + failure);
    }

    public static string RunAll()
    {
        Require(!File.Exists(ProjectAbsolutePath(FixtureAssetPath))
                && !File.Exists(ProjectAbsolutePath(FixtureAssetPath) + ".meta"),
            "Rollback fixture path is already occupied.");

        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        CanonicalBalanceMetricRecord authority = audit.Ledger.Records.FirstOrDefault(record =>
            string.Equals(record.AssetApplied, "true", StringComparison.Ordinal)
            && !string.Equals(record.Before, record.After, StringComparison.Ordinal)
            && record.SourceAuthority.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
            && record.SourcePropertyPath.Length > 0)
            ?? throw new InvalidOperationException(
                "No applied V27 asset authority is available for rollback diagnostics.");

        string sourcePath = authority.SourceAuthority;
        byte[] sourceBefore = File.ReadAllBytes(ProjectAbsolutePath(sourcePath));
        bool fixtureCreated = false;
        try
        {
            Require(AssetDatabase.CopyAsset(sourcePath, FixtureAssetPath),
                "Failed to create isolated rollback fixture asset.");
            fixtureCreated = true;
            AssetDatabase.ImportAsset(FixtureAssetPath, ImportAssetOptions.ForceSynchronousImport);

            string fixtureAbsolute = ProjectAbsolutePath(FixtureAssetPath);
            byte[] fixtureBefore = File.ReadAllBytes(fixtureAbsolute);
            byte[] fixtureMetaBefore = File.ReadAllBytes(fixtureAbsolute + ".meta");
            CaptureIdentity(FixtureAssetPath, out string guidBefore, out long fileIdBefore);

            BalanceAssetPatch patch = BalanceAssetPatch.CaptureForDiagnostics(
                FixtureAssetPath,
                authority.SourcePropertyPath,
                authority.After,
                authority.Before);
            bool injectedFailureObserved = false;
            try
            {
                V27BalanceAssetApplication.ApplyPatchesForDiagnostics(
                    new[] { patch },
                    BalanceAssetApplicationFailurePoint.AfterFirstReserialize);
            }
            catch (BalanceAssetApplicationInjectedFailureException exception)
                when (exception.FailurePoint
                      == BalanceAssetApplicationFailurePoint.AfterFirstReserialize)
            {
                injectedFailureObserved = true;
            }

            Require(injectedFailureObserved,
                "The forced post-reserialize failure was not observed.");
            AssetDatabase.ImportAsset(FixtureAssetPath, ImportAssetOptions.ForceSynchronousImport);
            Require(fixtureBefore.SequenceEqual(File.ReadAllBytes(fixtureAbsolute)),
                "Fixture YAML bytes were not restored exactly.");
            Require(fixtureMetaBefore.SequenceEqual(File.ReadAllBytes(fixtureAbsolute + ".meta")),
                "Fixture meta bytes changed during rollback.");
            CaptureIdentity(FixtureAssetPath, out string guidAfter, out long fileIdAfter);
            Require(string.Equals(guidBefore, guidAfter, StringComparison.Ordinal)
                    && fileIdBefore == fileIdAfter,
                "Fixture GUID or main FileID changed during rollback.");
            Require(sourceBefore.SequenceEqual(File.ReadAllBytes(ProjectAbsolutePath(sourcePath))),
                "Source authority changed while testing the isolated fixture.");

            return "RESULT=PASS; failures=0\n"
                   + "PASS V27_ASSET_ROLLBACK_INJECTED_AFTER_FIRST_RESERIALIZE\n"
                   + "PASS V27_ASSET_ROLLBACK_YAML_BYTE_EXACT\n"
                   + "PASS V27_ASSET_ROLLBACK_META_GUID_FILEID_EXACT\n"
                   + "PASS V27_ASSET_ROLLBACK_SOURCE_AUTHORITY_UNCHANGED\n"
                   + "PASS V27_ASSET_ROLLBACK_TEMPORARY_FIXTURE_ISOLATED\n";
        }
        finally
        {
            if (fixtureCreated)
            {
                if (!AssetDatabase.DeleteAsset(FixtureAssetPath))
                    throw new InvalidOperationException(
                        "Failed to delete the isolated rollback fixture asset.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }

    private static void CaptureIdentity(
        string assetPath,
        out string guid,
        out long mainFileId)
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath)
            ?? throw new InvalidOperationException("Rollback fixture asset is missing.");
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                asset,
                out guid,
                out mainFileId))
            throw new InvalidOperationException("Cannot capture rollback fixture identity.");
    }

    private static string ProjectAbsolutePath(string relativePath)
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
