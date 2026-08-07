#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class V19SaveAtomicityPlayModeFacade
{
    public const string RequestPath = "Temp/v19-save-atomicity.request";
    public const string ReportPath =
        "Artifacts/QA/v19-save-atomicity-report.txt";
    public const string EarlyErrorPath =
        "Temp/v19-save-atomicity-errors.txt";
    private const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";

    static V19SaveAtomicityPlayModeFacade()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        Application.logMessageReceived -= CaptureEarlyError;
        Application.logMessageReceived += CaptureEarlyError;
    }

    [MenuItem("DungeonStory/QA/Request V19 Save Atomicity")]
    public static void RequestRun()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.Delete(EarlyErrorPath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.Ticks.ToString());
        Debug.Log("V19 save atomicity PlayMode request queued.");
    }

    private static void CaptureEarlyError(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (!File.Exists(RequestPath)
            || type is not (LogType.Error
                or LogType.Exception
                or LogType.Assert))
        {
            return;
        }

        try
        {
            File.AppendAllText(
                EarlyErrorPath,
                (condition ?? string.Empty).Replace('\r', ' ')
                + "\n"
                + (stackTrace ?? string.Empty).Replace('\r', ' ')
                + "\n---\n");
        }
        catch
        {
            // A Console callback must never throw or recursively log.
        }
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath))
            return;

        if (EditorApplication.isPlaying)
        {
            if (UnityEngine.Object.FindFirstObjectByType<
                    V19SaveAtomicityPlayModeRunner>() == null)
            {
                new GameObject("V19 Save Atomicity Runner")
                    .AddComponent<V19SaveAtomicityPlayModeRunner>();
            }
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        try
        {
            string activeScene = SceneManager.GetActiveScene().path;
            if (!string.Equals(
                    activeScene,
                    GameplayScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "V19 save verification will not switch the user's active "
                    + $"scene. Expected '{GameplayScenePath}', found "
                    + $"'{activeScene}'.");
            }

            // Entering PlayMode keeps an already open dirty scene in memory.
            // This verifier never saves, discards, or switches that scene.
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception)
        {
            WriteFailure("EditMode setup", exception);
        }
    }

    internal static void WriteFailure(string phase, Exception exception)
    {
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllLines(
            ReportPath,
            new[]
            {
                "RESULT=FAIL",
                "target=V19_SAVE_ATOMICITY",
                "phase=" + phase,
                "exception=" + exception
            });
        File.Delete(RequestPath);
        Debug.LogError(
            "V19 save atomicity verification failed. " + ReportPath);
    }
}

public sealed class V19SaveAtomicityPlayModeRunner : MonoBehaviour
{
    private const float RuntimeReadyTimeoutSeconds = 45f;

    private IEnumerator Start()
    {
        float deadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;

        DungeonRuntimeLifetimeScope readyScope = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            readyScope = UnityEngine.Object
                .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate != null && candidate.Container != null);
            if (readyScope != null)
                break;
            yield return null;
        }

        yield return null;
        if (readyScope != null)
        {
            OwnerRunManager ownerManager = UnityEngine.Object
                .FindFirstObjectByType<OwnerRunManager>();
            if (ownerManager == null || ownerManager.CurrentOwnerActor == null)
            {
                StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
                for (int frame = 0; frame < 10; frame++)
                    yield return null;
            }
        }

        try
        {
            if (readyScope == null)
            {
                DungeonRuntimeLifetimeScope[] discovered = UnityEngine.Object
                    .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                throw new InvalidOperationException(
                    "Dungeon runtime scope did not become ready. "
                    + $"discovered={discovered.Length}; "
                    + $"activeScene={SceneManager.GetActiveScene().path}; "
                    + "containers="
                    + string.Join(
                        ",",
                        discovered.Select(value =>
                            value != null && value.Container != null
                                ? "ready"
                                : "null"))
                    + "; earlyErrors="
                    + (File.Exists(V19SaveAtomicityPlayModeFacade.EarlyErrorPath)
                        ? File.ReadAllText(
                            V19SaveAtomicityPlayModeFacade.EarlyErrorPath)
                        : "none"));
            }
            string result = V19SaveAtomicityDebugScenarios.RunLoaded();
            Directory.CreateDirectory("Artifacts/QA");
            File.WriteAllLines(
                V19SaveAtomicityPlayModeFacade.ReportPath,
                new[]
                {
                    "RESULT=PASS",
                    "target=V19_SAVE_ATOMICITY",
                    result
                });
            Debug.Log(result);
            File.Delete(V19SaveAtomicityPlayModeFacade.RequestPath);
        }
        catch (Exception exception)
        {
            V19SaveAtomicityPlayModeFacade.WriteFailure(
                "PlayMode execution",
                exception);
        }

        yield return null;
        EditorApplication.ExitPlaymode();
    }
}
#endif
