using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

public static class RunResultPanelFactoryDebugScenarios
{
    public static bool EnsureDungeonRuntimeScopeInActiveScene(out string report)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        DungeonRuntimeLifetimeScope existing = Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault((scope) => scope.gameObject.scene == activeScene);
        if (existing != null)
        {
            if (!existing.gameObject.activeSelf)
            {
                existing.gameObject.SetActive(true);
                EditorSceneManager.MarkSceneDirty(activeScene);
                bool savedExisting = EditorSceneManager.SaveScene(activeScene);
                report = $"Activated existing scope={existing.name}, scene={activeScene.path}, saved={savedExisting}";
                return savedExisting;
            }

            report = $"Existing active scope={existing.name}, scene={activeScene.path}";
            return true;
        }

        GameObject scopeObject = new GameObject("DungeonRuntimeLifetimeScope");
        scopeObject.AddComponent<DungeonRuntimeLifetimeScope>();
        EditorSceneManager.MarkSceneDirty(activeScene);
        bool saved = EditorSceneManager.SaveScene(activeScene);
        report = $"Added scope to scene={activeScene.path}, saved={saved}";
        return saved;
    }

    public static bool RunPlayModeSmoke(out string report)
    {
        report = string.Empty;
        if (!EditorApplication.isPlaying)
        {
            report = "PlayMode is required.";
            return false;
        }

        LifetimeScope scope = Object.FindFirstObjectByType<LifetimeScope>(FindObjectsInactive.Include);
        if (scope == null || scope.Container == null)
        {
            report = "No active LifetimeScope/container.";
            return false;
        }

        IRunResultPanelFactory factory = scope.Container.Resolve<IRunResultPanelFactory>();
        IRunResultPanelService service = scope.Container.Resolve<IRunResultPanelService>();
        if (factory == null || service == null)
        {
            report = $"Resolve failed. factory={factory != null}, service={service != null}";
            return false;
        }

        RunResultPanel panel = factory.CreateDefaultPanel();
        if (panel == null)
        {
            report = "RunResultPanelFactory returned null.";
            return false;
        }

        RunResultSnapshot snapshot = new RunResultSnapshot(
            ownerName: "Smoke Owner",
            endReason: "Factory Smoke",
            survivalSeconds: 73f,
            survivedOperatingDays: 2,
            settlementCount: 1,
            defendedInvasionCount: 1,
            maxThreatStage: InvasionThreatStage.Warning,
            finalInvasionThreat: 3f,
            firstDiscoveredFacilityCount: 4,
            firstUnlockedRecipeCount: 5,
            offenseSuccessCount: 1,
            difficultyMultiplier: 1.25f,
            legacyCurrency: 9);

        panel.Render(snapshot);
        TMP_Text text = panel.GetComponentInChildren<TMP_Text>(true);
        Canvas canvas = panel.GetComponentInParent<Canvas>();
        bool active = panel.gameObject.activeSelf;
        bool hasText = text != null && !string.IsNullOrWhiteSpace(text.text);
        bool textContainsOwner = text != null && text.text.Contains("Smoke Owner");
        bool canvasConfigured = canvas != null
            && canvas.renderMode == RenderMode.ScreenSpaceOverlay
            && canvas.sortingOrder == 1000;

        report = $"scope={scope.name}, factoryResolved=True, serviceResolved=True, panel={panel.name}, active={active}, text={hasText}, ownerText={textContainsOwner}, canvasConfigured={canvasConfigured}, textLength={(text != null ? text.text.Length : -1)}";

        if (canvas != null)
        {
            Object.Destroy(canvas.gameObject);
        }

        return active && hasText && textContainsOwner && canvasConfigured;
    }
}
