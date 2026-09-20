#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class InfrastructureCommandOutcomePlayModeFacade
{
    public const string RequestPath =
        "Temp/phase80-infrastructure-command-playmode.request";
    private const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";

    static InfrastructureCommandOutcomePlayModeFacade()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem(
        "DungeonStory/QA/Request Phase80 Infrastructure Command Outcomes")]
    public static void RequestRunFromMenu()
    {
        if (File.Exists(
                IndustrialInfrastructurePlayModeVerifier
                    .CommandOutcomeEvidencePath))
        {
            throw new InvalidOperationException(
                "The immutable infrastructure-command r4 evidence already exists.");
        }
        Directory.CreateDirectory("Temp");
        string reportDirectory = Path.GetDirectoryName(
            IndustrialInfrastructurePlayModeVerifier.ReportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory))
            Directory.CreateDirectory(reportDirectory);
        File.Delete(RequestPath);
        File.Delete(IndustrialInfrastructurePlayModeVerifier.ReportPath);
        File.WriteAllText(
            RequestPath,
            DateTime.UtcNow.Ticks.ToString(),
            new UTF8Encoding(false));
        Debug.Log("Phase80 infrastructure command PlayMode request queued.");
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath)
            || EditorApplication.isPlaying
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            bool alreadyOpen = string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase);
            if (!alreadyOpen)
            {
                if (!Application.isBatchMode)
                {
                    throw new InvalidOperationException(
                        "Open GameplayScene before running this verifier; the verifier will not replace an interactive scene.");
                }
                EditorSceneManager.OpenScene(
                    GameplayScenePath,
                    OpenSceneMode.Single);
                return;
            }
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception)
        {
            WriteSetupFailure(exception);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (!File.Exists(RequestPath))
            return;

        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            if (UnityEngine.Object.FindFirstObjectByType<
                    IndustrialInfrastructurePlayModeVerificationRunner>()
                == null)
            {
                IndustrialInfrastructurePlayModeVerifier
                    .RunInfrastructureCommandOutcomesOnly();
            }
            return;
        }

        if (change != PlayModeStateChange.EnteredEditMode)
            return;

        bool passed = File.Exists(
                IndustrialInfrastructurePlayModeVerifier
                    .CommandOutcomeEvidencePath)
            && File.ReadAllLines(
                    IndustrialInfrastructurePlayModeVerifier
                        .CommandOutcomeEvidencePath)
                .Any(line => string.Equals(
                    line,
                    "result=PASS",
                    StringComparison.Ordinal));
        File.Delete(RequestPath);
        if (Application.isBatchMode)
        {
            EditorApplication.delayCall += () =>
                EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    private static void WriteSetupFailure(Exception exception)
    {
        string path = IndustrialInfrastructurePlayModeVerifier
            .CommandOutcomeEvidencePath;
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllLines(
            path,
            new[]
            {
                "result=FAIL",
                "mode=infrastructure-command-outcomes-only",
                "phase=edit-mode-setup",
                "exception=" + exception
            },
            new UTF8Encoding(false));
        File.Delete(RequestPath);
        Debug.LogError(
            "Infrastructure command PlayMode setup failed: " + exception);
        if (Application.isBatchMode)
        {
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }
}
#endif
