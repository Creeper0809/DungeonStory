#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PhysicalItemLogisticsBootFailureDebugScenarios
{
    [MenuItem("DungeonStory/Debug/QA/Verify Natural Boot Failure Report Ordering")]
    public static void RunFromMenu()
    {
        const string directory = "Temp/v27-natural-boot-failure-ordering";
        string reportPath = directory + "/runner.txt";
        string staleArtifactPath = directory + "/stale.csv";
        const string expected =
            "Physical Item Logistics PlayMode Verification\n"
            + "[FAIL] EDITOR_BOOT_GUARD: focused\n"
            + "RESULT=FAIL; failures=1\n";

        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(reportPath, "stale runner");
            File.WriteAllText(staleArtifactPath, "stale portfolio");
            PhysicalItemLogisticsPlayModeVerifier
                .PublishBootFailureReportForDiagnostics(
                    reportPath,
                    expected,
                    () =>
                    {
                        File.Delete(reportPath);
                        File.Delete(staleArtifactPath);
                    });

            Require(File.Exists(reportPath),
                "The boot failure publisher deleted its terminal report.");
            Require(string.Equals(
                    File.ReadAllText(reportPath),
                    expected,
                    StringComparison.Ordinal),
                "The boot failure publisher did not preserve exact report bytes.");
            Require(!File.Exists(staleArtifactPath),
                "The boot failure publisher retained a stale natural artifact.");
            Debug.Log(
                "V27_NATURAL_BOOT_FAILURE_REPORT_ORDERING=PASS; stale=0; terminal=1");
        }
        finally
        {
            File.Delete(reportPath);
            File.Delete(staleArtifactPath);
            if (Directory.Exists(directory))
                Directory.Delete(directory, false);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
