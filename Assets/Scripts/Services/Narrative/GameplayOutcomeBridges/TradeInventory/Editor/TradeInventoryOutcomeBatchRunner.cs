using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class TradeInventoryOutcomeBatchRunner
{
    private const string RelativeReportPath =
        "Artifacts/QA/GameplayOutcomeLedgerPhase80TradeInventory-20260918-r1/"
        + "physical-relocation-runtime-test.json";

    public static void RunBatch()
    {
        string absolute = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            RelativeReportPath));
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)
            ?? throw new InvalidOperationException(
                "Trade/inventory runtime-test report directory is missing."));
        try
        {
            string detail = TradeInventoryOutcomeDebugScenarios.RunAll();
            File.WriteAllText(
                absolute,
                BuildReport("PASS", detail, string.Empty),
                new UTF8Encoding(false));
            Debug.Log(detail);
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                absolute,
                BuildReport("FAIL", string.Empty, exception.ToString()),
                new UTF8Encoding(false));
            throw;
        }
    }

    private static string BuildReport(
        string status,
        string detail,
        string error) =>
        "{\n"
        + "  \"schema\": \"trade-inventory-physical-relocation-runtime-test@1\",\n"
        + "  \"status\": \"" + Escape(status) + "\",\n"
        + "  \"candidate\": \"PhysicalItemRelocationReceipt\",\n"
        + "  \"scenario\": \"TradeInventoryOutcomeDebugScenarios.RunAll\",\n"
        + "  \"assertions\": [\n"
        + "    \"prepare-before-mutation and exact reversible rollback\",\n"
        + "    \"post-canonical faults never roll back physical state\",\n"
        + "    \"retry-before-ack returns the original receipt without a second mutation\",\n"
        + "    \"save-between-commit-deliver restores the same result key and outcome identity\",\n"
        + "    \"retry-after-ack and compacted tombstone keep exact key/outcome/hash proof\",\n"
        + "    \"same operation with a different payload fails closed\"\n"
        + "  ],\n"
        + "  \"detail\": \"" + Escape(detail) + "\",\n"
        + "  \"error\": \"" + Escape(error) + "\"\n"
        + "}\n";

    private static string Escape(string value) => (value ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n");
}
