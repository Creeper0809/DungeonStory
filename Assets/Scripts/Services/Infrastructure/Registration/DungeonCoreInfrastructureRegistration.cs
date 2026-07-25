using System;
using VContainer;
using VContainer.Unity;

public static class DungeonCoreInfrastructureRegistration
{
    public static void RegisterDungeonCoreInfrastructure(
        this IContainerBuilder builder,
        DungeonSceneRuntimeReferences sceneRuntimeReferences,
        DungeonUserSettingsRuntimeTargets userSettingsTargets,
        SceneValidationReferences validationReferences)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.RegisterInstance(sceneRuntimeReferences
            ?? throw new ArgumentNullException(nameof(sceneRuntimeReferences)));
        builder.RegisterInstance(userSettingsTargets
            ?? throw new ArgumentNullException(nameof(userSettingsTargets)));
        builder.RegisterInstance(validationReferences
            ?? throw new ArgumentNullException(nameof(validationReferences)));
        builder.RegisterEntryPoint<DungeonLegacyPersistenceMigration>(Lifetime.Singleton);
        builder.RegisterEntryPoint<SceneBuildableLeakValidator>(Lifetime.Singleton);
        builder.Register<UnityResourcesAssetLoader>(Lifetime.Singleton)
            .As<IResourcesAssetLoader>();
        builder.Register<ResourceDataScriptableObjectSource>(Lifetime.Singleton)
            .As<IDataScriptableObjectSource>();
        builder.Register<DataManager>(Lifetime.Singleton);
        builder.Register<DataManagerCatalog>(Lifetime.Singleton)
            .As<IDataCatalog>();
        builder.Register<BuildingDefinitionLookup>(Lifetime.Singleton)
            .As<IBuildingDefinitionLookup>();
        builder.Register<BuildingSummaryFormatter>(Lifetime.Singleton)
            .As<IBuildingSummaryFormatter>();
        builder.Register<GameManagerGameDataProvider>(Lifetime.Singleton)
            .As<IGameDataProvider>();
        builder.Register<GameManagerFloatingNumberFeedbackService>(Lifetime.Singleton)
            .As<IFloatingNumberFeedbackService>();
        builder.Register<UnityPlayerInputReader>(Lifetime.Singleton)
            .As<IPlayerInputReader>();
        builder.Register<EventSystemUiPointerBlocker>(Lifetime.Singleton)
            .As<IUiPointerBlocker>();
        builder.Register<PhysicsWorldPointerRaycaster>(Lifetime.Singleton)
            .As<IWorldPointerRaycaster>();
        builder.Register<ResourceTmpKoreanFontProvider>(Lifetime.Singleton)
            .As<ITmpKoreanFontProvider>();
        builder.Register<TmpKoreanFontService>(Lifetime.Singleton)
            .As<ITmpKoreanFontService>();
        builder.RegisterEntryPoint<DungeonUserSettingsService>(Lifetime.Singleton)
            .As<IDungeonUserSettingsService>();

        builder.RegisterEntryPoint<DungeonDebugModeService>(Lifetime.Singleton)
            .As<IDungeonDebugModeService>();
        builder.Register<DungeonDebugCheatCommandProvider>(Lifetime.Singleton);
        builder.Register<DungeonDebugEconomyCommandProvider>(Lifetime.Singleton);
        builder.Register<DungeonDebugItemCommandProvider>(Lifetime.Singleton);
        builder.Register<DungeonDebugCharacterCommandProvider>(Lifetime.Singleton);
        builder.Register<DungeonDebugWorkCommandProvider>(Lifetime.Singleton);
        builder.Register<DungeonDebugSurvivalWildlifeCommandProvider>(Lifetime.Singleton);
        builder.Register<DungeonDebugDefenseCommandProvider>(Lifetime.Singleton);
        builder.Register<DungeonDebugOverlayCommandProvider>(Lifetime.Singleton);
        builder.Register<DungeonDebugCommandRegistry>(Lifetime.Singleton)
            .As<IDungeonDebugCommandRegistry>();
        builder.Register<DungeonDebugTargetResolver>(Lifetime.Singleton);
        builder.RegisterEntryPoint<DungeonDebugPaletteUiController>(Lifetime.Singleton)
            .AsSelf();
        builder.RegisterEntryPoint<DungeonDebugWorldOverlayController>(Lifetime.Singleton);
        builder.RegisterEntryPoint<DungeonAudioController>(Lifetime.Singleton)
            .As<IDungeonAudioService>();
        builder.RegisterEntryPoint<DungeonSettingsUiController>(Lifetime.Singleton)
            .As<IDungeonSettingsUi>();

        builder.Register<ShopStockCatalog>(Lifetime.Singleton)
            .As<IShopStockCatalog>();
        builder.Register<GridSystemProvider>(Lifetime.Singleton)
            .As<IGridSystemProvider>();
        builder.Register<DungeonBackdropSpriteTilingFactory>(Lifetime.Singleton)
            .As<IDungeonBackdropSpriteTilingFactory>();
        builder.Register<WorldInfoClickSelectionService>(Lifetime.Singleton)
            .As<IWorldInfoClickSelector>();
        builder.RegisterEntryPoint<WorldInfoClickInputController>(Lifetime.Singleton);
        builder.Register<DungeonGridBuildingControllerProvider>(Lifetime.Singleton)
            .As<IDungeonGridBuildingControllerProvider>();
        builder.Register<SceneMainCameraProvider>(Lifetime.Singleton)
            .As<IMainCameraProvider>();
        builder.Register<SceneCameraWorldPointerPositionProvider>(Lifetime.Singleton)
            .As<IWorldPointerPositionProvider>();
        builder.Register<GridTextureProvider>(Lifetime.Singleton)
            .As<IGridTextureProvider>();
        builder.Register<GridBuildingObjectFactory>(Lifetime.Singleton)
            .As<IGridBuildingObjectFactory>();
        builder.Register<ModularFacilityWorldSaveService>(Lifetime.Singleton)
            .As<IModularFacilityWorldSaveService>();
        builder.Register<CharacterWorldSaveService>(Lifetime.Singleton)
            .As<ICharacterWorldSaveService>()
            .As<ICharacterIdRegistry>();
        builder.Register<OperatingDaySettlementRuntimeProvider>(Lifetime.Singleton)
            .As<IOperatingDaySettlementRuntimeProvider>();
        builder.Register<OperatingDaySettlementSaveService>(Lifetime.Singleton)
            .As<IOperatingDaySettlementSaveService>();
        builder.Register<EventAlertRuntimeProvider>(Lifetime.Singleton)
            .As<IEventAlertRuntimeProvider>();
        builder.Register<EventAlertSaveService>(Lifetime.Singleton)
            .As<IEventAlertSaveService>();
        builder.Register<OffenseSaveService>(Lifetime.Singleton)
            .As<IOffenseSaveService>();
        builder.Register<InvasionSaveService>(Lifetime.Singleton)
            .As<IInvasionSaveService>();
        builder.Register<GridGhostObjectResolver>(Lifetime.Singleton)
            .As<IGridGhostObjectResolver>();
        builder.Register<GridConstructButtonFactory>(Lifetime.Singleton)
            .As<IGridConstructButtonFactory>();
    }
}
