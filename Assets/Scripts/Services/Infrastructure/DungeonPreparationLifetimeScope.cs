using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

public sealed class DungeonPreparationCanvasProvider : IDungeonUiCanvasProvider
{
    private readonly SceneUiBootstrapReferences runtimeReferences;
    private Canvas canvas;

    public DungeonPreparationCanvasProvider(
        SceneUiBootstrapReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new System.ArgumentNullException(nameof(runtimeReferences));
    }

    public Canvas GetOrCreateCanvas()
    {
        if (canvas != null)
        {
            return canvas;
        }

        EnsureEventSystem();
        GameObject canvasObject = new GameObject(
            "PreparationCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private void EnsureEventSystem()
    {
        if (runtimeReferences.EventSystem != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        runtimeReferences.RegisterEventSystem(
            eventSystemObject.GetComponent<EventSystem>());
    }
}

public sealed class DungeonPreparationLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        Scene preparationScene = gameObject.scene;
        DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery(preparationScene);
        builder.RegisterInstance(new SceneUiBootstrapReferences(
            sceneQuery.First<EventSystem>(includeInactive: true)));
        builder.RegisterDungeonFoundation();
        builder.Register<UnityGameContentRootLoader>(Lifetime.Singleton)
            .As<IGameContentRootLoader>();
        builder.RegisterDungeonGameContentCatalog();
        builder.Register<ResourceTmpKoreanFontProvider>(Lifetime.Singleton)
            .As<ITmpKoreanFontProvider>();
        builder.Register<TmpKoreanFontService>(Lifetime.Singleton)
            .As<ITmpKoreanFontService>();
        builder.Register<DungeonPreparationCanvasProvider>(Lifetime.Singleton)
            .As<IDungeonUiCanvasProvider>();
        builder.Register<DungeonSceneNavigator>(Lifetime.Singleton)
            .AsSelf()
            .As<IDungeonSceneNavigator>();
        builder.Register<ResourceRunCharacterCatalog>(Lifetime.Singleton)
            .As<IRunCharacterCatalog>();
        builder.Register<ResourceOwnerCandidateCatalog>(Lifetime.Singleton)
            .As<IOwnerCandidateCatalog>();
        builder.Register<PreparationLocalLlmRuntimeProvider>(Lifetime.Singleton)
            .As<ILocalLlmRuntimeProvider>();
        builder.Register<ResourceCharacterSkillSystemSettingsProvider>(Lifetime.Singleton)
            .As<ICharacterSkillSystemSettingsProvider>();
        builder.RegisterComponentOnNewGameObject<LocalLlmRequestQueue>(
                Lifetime.Singleton,
                nameof(LocalLlmRequestQueue))
            .UnderTransform(transform);
        builder.RegisterEntryPoint<CharacterSkillGenerationService>(Lifetime.Singleton)
            .As<ICharacterSkillGenerationService>()
            .As<ICharacterSkillGenerationDiagnostics>();
        builder.Register<StartPartyPreparationService>(Lifetime.Singleton)
            .As<IStartPartyPreparationService>();
        builder.RegisterEntryPoint<StartPartyPreparationUiController>(Lifetime.Singleton);

        builder.RegisterBuildCallback(resolver =>
        {
            InjectSceneHierarchy(resolver, preparationScene);
            resolver.Resolve<LocalLlmRequestQueue>();
        });
    }

    private static void InjectSceneHierarchy(IObjectResolver resolver, Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            resolver.InjectGameObject(root);
        }
    }
}
