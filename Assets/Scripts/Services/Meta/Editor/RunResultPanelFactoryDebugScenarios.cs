using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
        IObjectResolver resolver = scope?.Container;
        bool isolated = resolver == null;
        resolver ??= BuildIsolatedResolver();

        IRunResultPanelFactory factory = resolver.Resolve<IRunResultPanelFactory>();
        IRunResultPanelService service = resolver.Resolve<IRunResultPanelService>();
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

        report = $"scope={(isolated ? "isolated" : scope.name)}, factoryResolved=True, serviceResolved=True, panel={panel.name}, active={active}, text={hasText}, ownerText={textContainsOwner}, canvasConfigured={canvasConfigured}, textLength={(text != null ? text.text.Length : -1)}";

        if (canvas != null)
        {
            Object.Destroy(canvas.gameObject);
        }
        if (isolated && resolver is System.IDisposable disposable)
        {
            disposable.Dispose();
        }

        return active && hasText && textContainsOwner && canvasConfigured;
    }

    private static IObjectResolver BuildIsolatedResolver()
    {
        ContainerBuilder builder = new ContainerBuilder();
        builder.RegisterInstance<ITmpKoreanFontService>(new NoopFontService());
        builder.RegisterInstance<IDungeonRunTransitionService>(new NoopTransitionService());
        builder.RegisterInstance<IGameTimeScaleController>(new MemoryTimeScaleController());
        builder.RegisterInstance<IRunResultTextQuery>(new FixedRunResultTextQuery());
        builder.RegisterInstance<IRunResultThemeQuery>(new FixedRunResultThemeQuery());
        builder.RegisterInstance<IRunResultPanelRegistry>(new RunResultPanelRegistry());
        builder.Register<RunResultPanelFactory>(Lifetime.Singleton)
            .As<IRunResultPanelFactory>();
        builder.Register<RunResultPanelService>(Lifetime.Singleton)
            .As<IRunResultPanelService>();
        return builder.Build();
    }

    private sealed class NoopFontService : ITmpKoreanFontService
    {
        public TMP_FontAsset Resolve() => null;
        public void Apply(TMP_Text text) { }
        public void ApplyToChildren(Transform root, bool includeInactive = true) { }
    }

    private sealed class NoopTransitionService : IDungeonRunTransitionService
    {
        public bool IsTransitioning => false;
        public void StartNextRun() { }
    }

    private sealed class MemoryTimeScaleController : IGameTimeScaleController
    {
        public float Scale { get; set; } = 1f;
    }

    private sealed class FixedRunResultTextQuery : IRunResultTextQuery
    {
        public string Get(RunResultTextId textId) => textId == RunResultTextId.NextRun
            ? "Next run"
            : "No run result";
    }

    private sealed class FixedRunResultThemeQuery : IRunResultThemeQuery
    {
        public Color ResultScrim => new Color(0f, 0f, 0f, 0.4f);
        public Color Panel => new Color(0.1f, 0.12f, 0.14f, 1f);
        public Color TextPrimary => Color.white;
        public void StylePrimaryButton(Button button) { }
        public void Apply(Canvas canvas) { }
    }
}
