#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class EditorVerificationSceneFixtureScope : IDisposable
{
    private const string FixtureFolder =
        "Assets/__EditorVerificationSceneFixtures";

    private readonly string ownerId;
    private readonly Scene originalScene;
    private readonly bool originalDirty;
    private readonly string originalTopology;
    private readonly string fixtureAssetPath;
    private Scene fixtureScene;
    private bool disposed;

    public EditorVerificationSceneFixtureScope(string ownerId)
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException(
                "Editor verification fixture scenes cannot be opened during PlayMode.");
        }
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new ArgumentException(
                "Editor verification fixture ownership requires a stable owner ID.",
                nameof(ownerId));
        }

        this.ownerId = ownerId.Trim();
        originalScene = SceneManager.GetActiveScene();
        if (!originalScene.IsValid() || !originalScene.isLoaded)
        {
            throw new InvalidOperationException(
                $"Editor verification fixture '{this.ownerId}' requires a loaded active scene.");
        }

        originalDirty = originalScene.isDirty;
        originalTopology = CaptureTopology(originalScene);

        fixtureScene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Additive);
        bool fixtureActive = fixtureScene.IsValid()
            && fixtureScene.isLoaded
            && (SceneManager.GetActiveScene().handle == fixtureScene.handle
                || SceneManager.SetActiveScene(fixtureScene));
        if (!fixtureScene.IsValid()
            || !fixtureScene.isLoaded
            || !fixtureActive)
        {
            TryCloseFixtureScene();
            throw new InvalidOperationException(
                $"Editor verification fixture '{this.ownerId}' could not acquire its scratch scene.");
        }

        EnsureFixtureFolder();
        fixtureAssetPath = FixtureFolder + "/"
            + Guid.NewGuid().ToString("N") + ".unity";
        if (!EditorSceneManager.SaveScene(
                fixtureScene,
                fixtureAssetPath,
                saveAsCopy: false))
        {
            TryCloseFixtureScene();
            TryDeleteFixtureAsset();
            throw new InvalidOperationException(
                $"Editor verification fixture '{this.ownerId}' could not persist its temporary scratch scene.");
        }
    }

    public static void Run(string ownerId, Action action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        using EditorVerificationSceneFixtureScope scope = new(ownerId);
        action();
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        string cleanupFailure = string.Empty;
        try
        {
            bool originalActive = originalScene.IsValid()
                && originalScene.isLoaded
                && (SceneManager.GetActiveScene().handle == originalScene.handle
                    || SceneManager.SetActiveScene(originalScene));
            if (!originalActive)
            {
                cleanupFailure = "original active scene could not be restored";
            }
        }
        finally
        {
            if (!TryCloseFixtureScene()
                && cleanupFailure.Length == 0)
                cleanupFailure = "scratch scene could not be closed";
            if (!TryDeleteFixtureAsset() && cleanupFailure.Length == 0)
                cleanupFailure = "scratch scene asset could not be deleted";
        }

        bool originalExact = originalScene.IsValid()
            && originalScene.isLoaded
            && SceneManager.GetActiveScene().handle == originalScene.handle
            && originalScene.isDirty == originalDirty
            && string.Equals(
                CaptureTopology(originalScene),
                originalTopology,
                StringComparison.Ordinal);
        if (!originalExact || cleanupFailure.Length != 0)
        {
            throw new InvalidOperationException(
                $"Editor verification fixture '{ownerId}' did not restore the original scene exactly: "
                + (cleanupFailure.Length == 0
                    ? "active scene, dirty state, or root topology drifted"
                    : cleanupFailure));
        }
    }

    private static void EnsureFixtureFolder()
    {
        if (AssetDatabase.IsValidFolder(FixtureFolder))
            return;
        string guid = AssetDatabase.CreateFolder(
            "Assets",
            "__EditorVerificationSceneFixtures");
        if (string.IsNullOrWhiteSpace(guid)
            || !AssetDatabase.IsValidFolder(FixtureFolder))
        {
            throw new InvalidOperationException(
                "Editor verification fixture folder could not be created.");
        }
    }

    private bool TryDeleteFixtureAsset()
    {
        bool deleted = string.IsNullOrWhiteSpace(fixtureAssetPath)
            || !File.Exists(fixtureAssetPath)
            || AssetDatabase.DeleteAsset(fixtureAssetPath);
        if (AssetDatabase.IsValidFolder(FixtureFolder)
            && !Directory.EnumerateFileSystemEntries(FixtureFolder).Any())
        {
            AssetDatabase.DeleteAsset(FixtureFolder);
        }
        return deleted;
    }

    private bool TryCloseFixtureScene()
    {
        if (!fixtureScene.IsValid() || !fixtureScene.isLoaded)
            return true;

        bool closed = EditorSceneManager.CloseScene(
            fixtureScene,
            removeScene: true);
        fixtureScene = default;
        return closed;
    }

    private static string CaptureTopology(Scene scene)
    {
        return string.Join(
            "\n",
            scene.GetRootGameObjects()
                .Where(root => root != null)
                .SelectMany(root => root
                    .GetComponentsInChildren<Transform>(includeInactive: true)
                    .Where(transform => transform != null)
                    .Select(transform =>
                        $"{transform.GetInstanceID()}|{BuildPath(transform)}|"
                        + $"{transform.gameObject.activeSelf}|{transform.childCount}"))
                .OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string BuildPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
#endif
