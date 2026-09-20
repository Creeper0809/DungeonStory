#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class MigratedProducerProductionEntry57BatchRunner
{
    private const string ReportRelativePath =
        "Artifacts/QA/MigratedProducerProductionEntry57-20260920-r25/"
        + "runtime-report.json";

    public static void RunBatch()
    {
        string absolute = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            ReportRelativePath));
        if (File.Exists(absolute))
        {
            throw new InvalidOperationException(
                "Migrated production-entry evidence is immutable and already exists: "
                + ReportRelativePath);
        }

        bool passed = false;
        string detail = string.Empty;
        try
        {
            passed = MigratedProducerProductionEntry57E2EDebugScenarios.RunAll();
        }
        catch (Exception exception)
        {
            detail = exception.ToString();
        }

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)
            ?? throw new InvalidOperationException(
                "Migrated production-entry report directory is missing."));
        File.WriteAllText(
            absolute,
            BuildReport(passed, detail),
            new UTF8Encoding(false));
        if (!passed)
        {
            throw new InvalidOperationException(
                "Migrated production-entry 57/57 E2E failed: " + detail);
        }

        Debug.Log("MIGRATED_PRODUCER_PRODUCTION_ENTRY_57=PASS");
    }

    private static string BuildReport(bool passed, string detail) =>
        "{\n"
        + "  \"schema\": \"migrated-producer-production-entry-e2e@1\",\n"
        + "  \"status\": \"" + (passed ? "PASS" : "FAIL") + "\",\n"
        + "  \"expectedKinds\": 57,\n"
        + "  \"coverageContract\": \"success+reject-rollback+delivery-retry\",\n"
        + "  \"unityMcpUsed\": false,\n"
        + "  \"dungeonPlayerMcpUsed\": false,\n"
        + "  \"detail\": \"" + Escape(detail) + "\"\n"
        + "}\n";

    private static string Escape(string value) => (value ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n");
}
#endif
