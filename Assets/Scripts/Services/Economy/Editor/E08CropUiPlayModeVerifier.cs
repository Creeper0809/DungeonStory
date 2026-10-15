#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public static class E08CropUiPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/LedgerB1E08CropUiPlayMode-20260925-r2/report.txt";
    private const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";
    private const string PendingKey =
        "DungeonStory.LedgerB1.E08.CropUi.Pending";
    private const string SuccessKey =
        "DungeonStory.LedgerB1.E08.CropUi.Success";
    private static double deadline;

    public static void RequestRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "E08 crop UI verification must start in Edit Mode.");
        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(
                GameplayScenePath,
                OpenSceneMode.Single);
        }
        Write("RUNNING\n");
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PendingKey, false))
            return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 90d;
            EditorApplication.update -= Verify;
            EditorApplication.update += Verify;
            return;
        }
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.update -= Verify;
        bool succeeded = SessionState.GetBool(SuccessKey, false);
        SessionState.SetBool(PendingKey, false);
        SessionState.SetBool(SuccessKey, false);
        if (Application.isBatchMode)
            EditorApplication.Exit(succeeded ? 0 : 1);
    }

    private static void Verify()
    {
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value?.Container != null);
        if (scope == null && EditorApplication.timeSinceStartup < deadline)
            return;

        EditorApplication.update -= Verify;
        bool succeeded = false;
        try
        {
            Require(scope != null, "Main gameplay container was not initialized.");
            (scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable)?
                .Dispose();
            scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
            GameManager game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Require(game != null, "Main game manager is missing.");
            game.isPause = true;
            scope.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
            Time.timeScale = 0f;
            Require(
                CropPlotDebugScenarios.RunWim016LightObservationFocused(
                    out string detail),
                detail);
            Write(
                "status=PASS\n"
                + "unityMcpUsed=false\n"
                + "dungeonPlayerMcpUsed=false\n"
                + detail
                + "\n");
            succeeded = true;
        }
        catch (Exception exception)
        {
            Write(
                "status=FAIL\n"
                + "unityMcpUsed=false\n"
                + "dungeonPlayerMcpUsed=false\n"
                + exception
                + "\n");
            Debug.LogException(exception);
        }
        finally
        {
            SessionState.SetBool(SuccessKey, succeeded);
            EditorApplication.ExitPlaymode();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Write(string value)
    {
        string directory = Path.GetDirectoryName(ReportPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(
            ReportPath,
            value,
            new System.Text.UTF8Encoding(false));
    }
}
#endif
