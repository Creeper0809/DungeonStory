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
public static class InfrastructurePowerFuelOutcomePlayModeFacade
{
    public const string RequestPath =
        "Temp/e12-infrastructure-power-fuel-playmode.request";
    private const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";

    static InfrastructurePowerFuelOutcomePlayModeFacade()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static void RunBatch()
    {
        string evidencePath = IndustrialInfrastructurePlayModeVerifier
            .ResolvePowerFuelEvidencePath();
        if (File.Exists(evidencePath))
        {
            throw new InvalidOperationException(
                "The requested immutable power-fuel evidence already exists: "
                + evidencePath);
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
        Debug.Log("E12 infrastructure power-fuel PlayMode request queued.");
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
                IndustrialInfrastructurePlayModeVerifier.RunPowerFuelOnly();
            }
            return;
        }

        if (change != PlayModeStateChange.EnteredEditMode)
            return;

        string evidencePath = IndustrialInfrastructurePlayModeVerifier
            .ResolvePowerFuelEvidencePath();
        string[] lines = File.Exists(evidencePath)
            ? File.ReadAllLines(evidencePath)
            : Array.Empty<string>();
        bool passed = lines.Any(line => string.Equals(
                line,
                "result=PASS",
                StringComparison.Ordinal))
            && lines.Any(line => string.Equals(
                line,
                "powerFuelAutonomousOutcome=exact-once;physical-child=joined",
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
            .ResolvePowerFuelEvidencePath();
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllLines(
            path,
            new[]
            {
                "result=FAIL",
                "mode=power-fuel-only",
                "phase=edit-mode-setup",
                "exception=" + exception
            },
            new UTF8Encoding(false));
        File.Delete(RequestPath);
        Debug.LogError(
            "Infrastructure power-fuel PlayMode setup failed: " + exception);
        if (Application.isBatchMode)
        {
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }
}
#endif
