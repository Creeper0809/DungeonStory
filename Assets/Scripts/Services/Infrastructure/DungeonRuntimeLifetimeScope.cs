using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

public sealed class DungeonRuntimeLifetimeScope : LifetimeScope
{
    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    protected override void Configure(IContainerBuilder builder)
    {
        Scene scopeScene = gameObject.scene;
        DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery(scopeScene);
        EnsureEventSystem(sceneQuery);
        DungeonSceneRuntimeReferences sceneRuntimeReferences =
            CaptureSceneRuntimeReferences(sceneQuery);
        DungeonUserSettingsRuntimeTargets userSettingsTargets =
            CaptureUserSettingsTargets(sceneQuery);
        SceneValidationReferences sceneValidationReferences =
            CaptureSceneValidationReferences(sceneQuery, scopeScene);
        OffenseSceneRuntimeReferences offenseRuntimeReferences =
            CaptureOffenseRuntimeReferences(sceneQuery);
        InvasionSceneRuntimeReferences invasionRuntimeReferences =
            CaptureInvasionRuntimeReferences(sceneQuery);
        FacilityFeatureSceneRuntimeReferences facilityRuntimeReferences =
            CaptureFacilityRuntimeReferences(sceneQuery);
        CharacterSceneRuntimeReferences characterRuntimeReferences =
            CaptureCharacterRuntimeReferences(sceneQuery);
        ProgressionSceneRuntimeReferences progressionRuntimeReferences =
            CaptureProgressionRuntimeReferences(sceneQuery);
        WorldSimulationSceneReferences worldSimulationReferences =
            CaptureWorldSimulationReferences(sceneQuery);
        OwnerCommandController ownerCommandController =
            sceneQuery.SingleRequired<OwnerCommandController>(includeInactive: true);
        IPlayerCombatCommandSource playerCombatCommands =
            ownerCommandController as IPlayerCombatCommandSource
            ?? new UnavailablePlayerCombatCommandSource();
        IPlayerStaffCommandSource playerStaffCommands =
            ownerCommandController as IPlayerStaffCommandSource
            ?? new UnavailablePlayerStaffCommandSource();
        builder.RegisterDungeonFoundation();
        builder.RegisterDungeonWork();

        builder.RegisterDungeonCombatAndInvasion(
            invasionRuntimeReferences,
            playerCombatCommands);
        builder.RegisterDungeonWorldSimulation(
            scopeScene,
            worldSimulationReferences);
        builder.RegisterDungeonSaveInfrastructure();
        builder.RegisterDungeonCoreInfrastructure(
            sceneRuntimeReferences,
            userSettingsTargets,
            sceneValidationReferences);
        builder.RegisterDungeonFacilitySystems(facilityRuntimeReferences);
        builder.RegisterDungeonCharacterSystems(characterRuntimeReferences);
        builder.RegisterDungeonAiAndRooms(transform, characterRuntimeReferences);
        builder.RegisterDungeonProgressionAndOffense(
            offenseRuntimeReferences,
            progressionRuntimeReferences);
        builder.RegisterDungeonPresentation(
            transform,
            sceneRuntimeReferences,
            playerStaffCommands,
            sceneQuery.First<RunResultPanel>(includeInactive: true));

        builder.RegisterBuildCallback((resolver) =>
        {
            InjectSceneHierarchy(resolver, scopeScene);
            resolver.Resolve<ItemPileInfoPanel>();
            resolver.Resolve<WildlifeInfoPanel>();
            resolver.Resolve<RegularCustomerRuntime>();
            resolver.Resolve<IExteriorActivityRuntime>();
            resolver.Resolve<IWildlifeRuntime>();
            resolver.Resolve<ISurvivalFoodRuntime>();
        });
    }

    private static void InjectSceneHierarchy(IObjectResolver resolver, Scene scopeScene)
    {
        if (!scopeScene.IsValid())
        {
            throw new System.InvalidOperationException(
                "Cannot inject DungeonStory scene components because the owning scene is invalid.");
        }

        GameObject[] roots = scopeScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            resolver.InjectGameObject(roots[i]);
        }
    }

    private static DungeonSceneRuntimeReferences CaptureSceneRuntimeReferences(
        DungeonSceneComponentQuery sceneQuery)
    {
        return new DungeonSceneRuntimeReferences(
            sceneQuery.First<UIManager>(includeInactive: true),
            sceneQuery.First<OperatingDaySettlementRuntime>(includeInactive: true),
            sceneQuery.First<EventAlertRuntime>(includeInactive: true),
            sceneQuery.First<RunVariableRuntime>(includeInactive: true),
            sceneQuery.First<Canvas>(includeInactive: true),
            sceneQuery.First<GameManager>(includeInactive: true),
            sceneQuery.First<GridSystemManager>(includeInactive: true),
            sceneQuery.First<DungeonStoryGridBuildingController>(includeInactive: true),
            sceneQuery.First<GridTexture>(includeInactive: true),
            sceneQuery.First<Camera>(includeInactive: true),
            sceneQuery.First<OwnerSelectionPanel>(includeInactive: true),
            sceneQuery.First<UIBuildingInfo>(includeInactive: true));
    }

    private static DungeonUserSettingsRuntimeTargets CaptureUserSettingsTargets(
        DungeonSceneComponentQuery sceneQuery)
    {
        return new DungeonUserSettingsRuntimeTargets(
            sceneQuery.First<CameraManager>(includeInactive: true),
            sceneQuery.All<DungeonUiThemeRuntime>(includeInactive: true),
            sceneQuery.First<GameManager>(includeInactive: true));
    }

    private static SceneValidationReferences CaptureSceneValidationReferences(
        DungeonSceneComponentQuery sceneQuery,
        Scene scopeScene)
    {
        return new SceneValidationReferences(
            scopeScene.GetRootGameObjects(),
            sceneQuery.All<BuildableObject>(includeInactive: true),
            sceneQuery.All<LocalLlmRequestQueue>(includeInactive: true));
    }

    private static OffenseSceneRuntimeReferences CaptureOffenseRuntimeReferences(
        DungeonSceneComponentQuery sceneQuery)
    {
        return new OffenseSceneRuntimeReferences(
            sceneQuery.First<OffenseWorldMapRuntime>(includeInactive: true),
            sceneQuery.First<OffenseRewardRuntime>(includeInactive: true),
            sceneQuery.First<OffenseExpeditionRuntime>(includeInactive: true),
            sceneQuery.First<OffenseWorldMapPanel>(includeInactive: true),
            sceneQuery.First<OffenseExpeditionPanel>(includeInactive: true));
    }

    private static InvasionSceneRuntimeReferences CaptureInvasionRuntimeReferences(
        DungeonSceneComponentQuery sceneQuery)
    {
        return new InvasionSceneRuntimeReferences(
            sceneQuery.First<InvasionThreatRuntime>(includeInactive: true),
            sceneQuery.First<InvasionDirectorRuntime>(includeInactive: true),
            sceneQuery.First<InvasionCombatReportRuntime>(includeInactive: true));
    }

    private static FacilityFeatureSceneRuntimeReferences CaptureFacilityRuntimeReferences(
        DungeonSceneComponentQuery sceneQuery)
    {
        return new FacilityFeatureSceneRuntimeReferences(
            sceneQuery.First<FacilityEvolutionRuntime>(includeInactive: true),
            sceneQuery.First<FacilitySynthesisRuntime>(includeInactive: true),
            sceneQuery.First<CodexRuntime>(includeInactive: true));
    }

    private static CharacterSceneRuntimeReferences CaptureCharacterRuntimeReferences(
        DungeonSceneComponentQuery sceneQuery)
    {
        return new CharacterSceneRuntimeReferences(
            sceneQuery.First<LocalLlmRequestQueue>(includeInactive: true),
            sceneQuery.First<SocialReputationRuntime>(includeInactive: true),
            sceneQuery.First<StaffDiscontentRuntime>(includeInactive: true),
            sceneQuery.SingleRequired<RegularCustomerRuntime>(includeInactive: true),
            sceneQuery.First<CharacterSpawner>(includeInactive: true),
            sceneQuery.First<CharacterAiScheduler>(includeInactive: true),
            sceneQuery.First<OwnerRunManager>(includeInactive: true),
            sceneQuery.First<AiDirectorRuntime>(includeInactive: true));
    }

    private static ProgressionSceneRuntimeReferences CaptureProgressionRuntimeReferences(
        DungeonSceneComponentQuery sceneQuery)
    {
        return new ProgressionSceneRuntimeReferences(
            sceneQuery.First<DailyFacilityShopRuntime>(includeInactive: true),
            sceneQuery.First<BlueprintResearchRuntime>(includeInactive: true),
            sceneQuery.First<MetaProgressionRuntime>(includeInactive: true));
    }

    private static WorldSimulationSceneReferences CaptureWorldSimulationReferences(
        DungeonSceneComponentQuery sceneQuery)
    {
        return new WorldSimulationSceneReferences(
            sceneQuery.All<WildlifeHabitatMarker>(includeInactive: true),
            sceneQuery.All<ExteriorZoneMarker>(includeInactive: true));
    }

    private static void EnsureEventSystem(DungeonSceneComponentQuery sceneQuery)
    {
        EventSystem eventSystem = sceneQuery.First<EventSystem>(includeInactive: true);
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }
        else if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        StandaloneInputModule legacyModule =
            eventSystem.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
        {
            legacyModule.enabled = false;
        }

        eventSystem.gameObject.SetActive(true);
    }
}
