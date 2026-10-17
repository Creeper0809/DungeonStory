#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Wim029CompanionRoleOutcomePlayModeVerifier
{
    private const string CompanionMode = "companion";
    private const string HaulMode = "haul";
    private const string PendingPath =
        "Temp/wim029-companion-role-outcome.flag";
    private const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";
    private static bool batchExitArmed;

    public static void RunBatch()
    {
        RunBatchCore(CompanionMode);
    }

    public static void RunHaulBatch()
    {
        RunBatchCore(HaulMode);
    }

    private static void RunBatchCore(string mode)
    {
        Directory.CreateDirectory("Temp");
        string reportPath = string.Equals(mode, HaulMode, StringComparison.Ordinal)
            ? Wim029CompanionLiveRunner.HaulReportPath
            : Wim029CompanionLiveRunner.ReportPath;
        bool createOnly = Wim029CompanionLiveRunner.IsE11CreateOnly;
        if (createOnly && (File.Exists(reportPath)
            || File.Exists(Path.ChangeExtension(reportPath, ".raw.json"))))
        {
            throw new InvalidOperationException(
                "E11 role evidence is immutable and already exists: " + reportPath);
        }
        if (File.Exists(reportPath))
        {
            if (createOnly)
            {
                throw new InvalidOperationException(
                    "E11 role evidence is immutable and already exists: "
                    + reportPath);
            }
            File.Delete(reportPath);
        }
        if (createOnly)
        {
            if (!Application.isBatchMode || !DungeonPersistencePaths.IsIsolatedVerification
                || EditorApplication.isPlaying
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
                    CaptivityWildlifeLifecyclePlayModeVerifier.LiveNarrativeCaptureVariable)))
                throw new InvalidOperationException(
                    "E11 role background deferral requires a fresh explicit isolated batch before Play.");
            Environment.SetEnvironmentVariable(
                CaptivityWildlifeLifecyclePlayModeVerifier.LiveNarrativeCaptureVariable, "1");
        }
        File.WriteAllText(PendingPath, mode + "\n" + DateTime.UtcNow.ToString("O"));
        if (!batchExitArmed)
        {
            batchExitArmed = true;
            EditorApplication.playModeStateChanged += ExitBatchAfterRun;
        }
        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(
                GameplayScenePath,
                OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!File.Exists(PendingPath)) return;
        bool haul = string.Equals(
            File.ReadLines(PendingPath).FirstOrDefault(),
            HaulMode,
            StringComparison.Ordinal);
        if (UnityEngine.Object.FindFirstObjectByType<
                Wim029CompanionRoleOutcomePlayModeRunner>() != null)
        {
            return;
        }
        new GameObject(nameof(Wim029CompanionRoleOutcomePlayModeRunner))
            .AddComponent<Wim029CompanionRoleOutcomePlayModeRunner>()
            .Configure(haul);
    }

    private static void ExitBatchAfterRun(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode || !batchExitArmed)
            return;
        EditorApplication.playModeStateChanged -= ExitBatchAfterRun;
        batchExitArmed = false;
        EditorApplication.delayCall += () =>
        {
            bool haul = File.Exists(PendingPath)
                && string.Equals(
                    File.ReadLines(PendingPath).FirstOrDefault(),
                    HaulMode,
                    StringComparison.Ordinal);
            string reportPath = haul
                ? Wim029CompanionLiveRunner.HaulReportPath
                : Wim029CompanionLiveRunner.ReportPath;
            bool passed = File.Exists(reportPath)
                && (!Wim029CompanionLiveRunner.IsE11CreateOnly
                    || (File.Exists(Path.ChangeExtension(reportPath, ".raw.json"))
                        && !File.Exists(reportPath + ".failure.txt")))
                && File.ReadLines(reportPath)
                    .Any(line => string.Equals(
                        line,
                        "result=PASS",
                        StringComparison.Ordinal));
            if (File.Exists(PendingPath))
                File.Delete(PendingPath);
            if (Wim029CompanionLiveRunner.IsE11CreateOnly)
                Environment.SetEnvironmentVariable(
                    CaptivityWildlifeLifecyclePlayModeVerifier.LiveNarrativeCaptureVariable, null);
            EditorApplication.Exit(passed ? 0 : 1);
        };
    }
}

public sealed class Wim029CompanionRoleOutcomePlayModeRunner : MonoBehaviour
{
    private bool haul;

    public void Configure(bool haulMode)
    {
        haul = haulMode;
    }

    private IEnumerator Start()
    {
        float readyDeadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < readyDeadline)
        {
            DungeonRuntimeLifetimeScope scope =
                FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include);
            if (scope?.Container != null
                && FindFirstObjectByType<GameManager>(
                    FindObjectsInactive.Include) != null)
            {
                break;
            }
            yield return null;
        }

        bool preparationFailed = false;
        try
        {
            if (haul)
                Wim029CompanionLiveRunner.StartHaul();
            else
                Wim029CompanionLiveRunner.StartFocused();
        }
        catch (Exception exception)
        {
            preparationFailed = true;
            string reportPath = haul
                ? Wim029CompanionLiveRunner.HaulReportPath
                : Wim029CompanionLiveRunner.ReportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(
                reportPath));
            WriteFailure(reportPath, "result=FAIL\n" + exception);
        }

        string outputPath = haul
            ? Wim029CompanionLiveRunner.HaulReportPath
            : Wim029CompanionLiveRunner.ReportPath;
        float completionDeadline = Time.realtimeSinceStartup + 240f;
        while (!preparationFailed && Time.realtimeSinceStartup < completionDeadline)
        {
            if (File.Exists(outputPath)
                && !File.ReadLines(outputPath)
                    .Any(line => string.Equals(
                        line,
                        "result=RUNNING",
                        StringComparison.Ordinal)))
            {
                break;
            }
            yield return null;
        }

        if (!preparationFailed && (!File.Exists(outputPath)
            || File.ReadLines(outputPath)
                .Any(line => string.Equals(
                    line,
                    "result=RUNNING",
                    StringComparison.Ordinal))))
        {
            WriteFailure(
                outputPath,
                "result=FAIL\nTimed out waiting for WIM029 "
                + (haul ? "haul" : "companion")
                + " role outcome witness.");
        }

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        };
        Destroy(gameObject);
    }

    private static void WriteFailure(string path, string failure)
    {
        if (Wim029CompanionLiveRunner.IsE11CreateOnly)
        {
            // Preserve any partial witness; a separate create-only failure is
            // not allowed to erase its RUNNING observations or older evidence.
            string output = File.Exists(path) ? path + ".failure.txt" : path;
            using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            writer.Write(failure);
        }
        else File.WriteAllText(path, failure);
    }
}
#endif
