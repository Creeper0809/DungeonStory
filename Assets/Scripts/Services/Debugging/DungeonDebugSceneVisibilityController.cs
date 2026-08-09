using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer.Unity;

/// <summary>
/// Keeps scene-authored and runtime debug presentation hidden unless the
/// persisted Debug Mode option is enabled. Simulation objects are never placed
/// below these roots.
/// </summary>
public sealed class DungeonDebugSceneVisibilityController :
    IStartable,
    IDisposable
{
    private readonly IDungeonDebugModeService modeService;

    public DungeonDebugSceneVisibilityController(IDungeonDebugModeService modeService)
    {
        this.modeService = modeService
            ?? throw new ArgumentNullException(nameof(modeService));
    }

    public void Start()
    {
        modeService.StateChanged += Refresh;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Refresh();
    }

    public void Dispose()
    {
        modeService.StateChanged -= Refresh;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene _, LoadSceneMode __) => Refresh();

    private void Refresh()
    {
        bool visible = modeService.IsDeveloperModeEnabled;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == null)
            {
                continue;
            }
            if (string.Equals(root.name, "__Debug", StringComparison.Ordinal))
            {
                root.SetActive(visible);
                continue;
            }
            if (!string.Equals(
                    root.name,
                    DungeonRuntimeHierarchy.RootName,
                    StringComparison.Ordinal))
            {
                continue;
            }
            Transform runtimeDebug = root.transform.Find(DungeonRuntimeHierarchy.Debug);
            if (runtimeDebug != null)
            {
                runtimeDebug.gameObject.SetActive(visible);
            }
        }
    }
}
