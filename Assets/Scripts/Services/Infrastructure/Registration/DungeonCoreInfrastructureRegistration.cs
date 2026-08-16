using System;
using DungeonStory.Content.CoreSession;
using DungeonStory.Infrastructure;
using VContainer;
using VContainer.Unity;

public static class DungeonCoreInfrastructureRegistration
{
    public static void RegisterDungeonGameContentCatalog(
        this IContainerBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Register<ResourceGameContentCatalog>(Lifetime.Singleton)
            .As<IGameContentCatalog>()
            .As<IGameContentDefinitionSource>()
            .As<IServiceProcessAuthoredContentPort>()
            .As<IRoomEnvironmentAuthoredContentPort>()
            .As<IOffenseAuthoredContentPort>()
            .As<ICoreSessionRulesProvider>();
    }

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
        builder.RegisterInstance(sceneRuntimeReferences.RunVariables
                ?? throw new InvalidOperationException(
                    "Gameplay composition requires RunVariableRuntime."))
            .As<IRunVariableRuntime>();
        builder.RegisterInstance(userSettingsTargets
            ?? throw new ArgumentNullException(nameof(userSettingsTargets)));
        builder.RegisterInstance(validationReferences
            ?? throw new ArgumentNullException(nameof(validationReferences)));
        builder.RegisterEntryPoint<DungeonLegacyPersistenceMigration>(Lifetime.Singleton)
            .AsSelf();
        builder.RegisterEntryPoint<SceneBuildableLeakValidator>(Lifetime.Singleton)
            .AsSelf();
        builder.Register<UnityGameContentRootLoader>(Lifetime.Singleton)
            .As<IGameContentRootLoader>();
        builder.Register<GameContentDataCatalog>(Lifetime.Singleton)
            .As<IDataCatalog>();
        builder.Register<BuildingDefinitionLookup>(Lifetime.Singleton)
            .As<IBuildingDefinitionLookup>();
        builder.Register<BuildingWorkOrderSummaryAdapter>(Lifetime.Singleton)
            .As<IBuildingWorkOrderSummaryQuery>();
        builder.Register<BuildingSummaryFormatter>(Lifetime.Singleton)
            .As<IBuildingSummaryFormatter>();
        builder.Register<ScopedGameSessionStateStore>(Lifetime.Singleton)
            .As<IGameSessionStateProvider>()
            .As<IGameSessionStateStore>()
            .As<IGameSessionPauseAuthority>()
            .As<IGameSessionPersistence>()
            .As<IDungeonRestoreTransactionParticipant>();
        builder.RegisterEntryPoint<GameCalendarRuntime>(Lifetime.Singleton)
            .As<IGameCalendar>();
        builder.Register<ResourceClimateDefinitionCatalog>(Lifetime.Singleton)
            .As<IClimateDefinitionCatalog>();
        builder.RegisterEntryPoint<ClimateRuntime>(Lifetime.Singleton)
            .As<IClimateQuery>()
            .As<IClimatePersistence>();
        builder.Register<GameSpeedController>(Lifetime.Singleton)
            .As<IGameSpeedController>();
        builder.Register<TreasuryEconomyAggregateStateStore>(Lifetime.Singleton);
        builder.Register<TreasuryEconomyPersistence>(Lifetime.Singleton)
            .As<ITreasuryEconomyPersistence>();
        builder.RegisterEntryPoint<EconomyTransactionLedgerRuntime>(
                Lifetime.Singleton)
            .AsSelf()
            .As<IEconomyTransactionLedger>();
        builder.Register<GameMoneyAccount>(Lifetime.Singleton)
            .As<IGameMoneyAccount>();
        builder.Register<EmploymentContractRuntime>(Lifetime.Singleton)
            .AsSelf()
            .As<IEmploymentContractRuntime>();
        builder.Register<PaidFacilityContractRuntime>(Lifetime.Singleton)
            .AsSelf()
            .As<IPaidFacilityContractRuntime>()
            .As<IBuildingPaidFacilityContractPort>();
        builder.Register<AutoProcurementFinancialDependencies>(Lifetime.Singleton);
        builder.Register<AutoProcurementStockDependencies>(Lifetime.Singleton);
        builder.Register<AutoProcurementRuntime>(Lifetime.Singleton)
            .AsSelf()
            .As<IAutoProcurementRuntime>();
        builder.RegisterEntryPoint<EquipmentOverclockRuntime>(
                Lifetime.Singleton)
            .AsSelf()
            .As<IEquipmentOverclockRuntime>()
            .As<IFacilityOverclockRuntime>();
        builder.Register<ReforgePrecisionService>(Lifetime.Singleton)
            .As<IReforgePrecisionService>();
        builder.Register<TreasuryDefenseRuntime>(Lifetime.Singleton)
            .AsSelf()
            .As<ITreasuryDefenseRuntime>();
        builder.Register<GameManagerFloatingNumberFeedbackService>(Lifetime.Singleton)
            .As<IFloatingNumberFeedbackService>();
        builder.Register<DungeonAutomationInputState>(Lifetime.Singleton)
            .As<IDungeonAutomationInputReader>()
            .As<IDungeonAutomationInputControl>();
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
            .As<IDungeonUserSettingsService>()
            .As<IBuildingPresentationSettingsPort>();

        builder.RegisterEntryPoint<DungeonDebugModeService>(Lifetime.Singleton)
            .As<IDungeonDebugModeService>();
        builder.Register<DungeonDebugRuleRuntime>(Lifetime.Singleton)
            .As<IDungeonDebugRuleRuntime>()
            .As<IDungeonDebugRuleQuery>()
            .As<IBuildingDamageRulePort>();
        builder.Register<DungeonDebugCheatCommandProvider>(Lifetime.Singleton)
            .AsSelf().As<IDungeonDebugCommandProvider>();
        builder.Register<DungeonDebugEconomyCommandProvider>(Lifetime.Singleton)
            .AsSelf().As<IDungeonDebugCommandProvider>();
        builder.Register<DungeonDebugItemCommandProvider>(Lifetime.Singleton)
            .AsSelf().As<IDungeonDebugCommandProvider>();
        builder.Register<DungeonDebugCharacterCommandProvider>(Lifetime.Singleton)
            .AsSelf().As<IDungeonDebugCommandProvider>();
        builder.Register<DungeonDebugWorkCommandProvider>(Lifetime.Singleton)
            .AsSelf().As<IDungeonDebugCommandProvider>();
        builder.Register<DungeonDebugSurvivalWildlifeCommandProvider>(Lifetime.Singleton)
            .AsSelf().As<IDungeonDebugCommandProvider>();
        builder.Register<DungeonDebugDefenseCommandProvider>(Lifetime.Singleton)
            .AsSelf().As<IDungeonDebugCommandProvider>();
        builder.Register<DungeonDebugOverlayCommandProvider>(Lifetime.Singleton)
            .AsSelf().As<IDungeonDebugCommandProvider>();
        builder.Register<DungeonDebugPerformanceCommandProvider>(Lifetime.Singleton)
            .AsSelf().As<IDungeonDebugCommandProvider>();
        builder.Register<DungeonDebugCommandRegistry>(Lifetime.Singleton)
            .As<IDungeonDebugCommandRegistry>();
        builder.Register<DungeonDebugTargetResolver>(Lifetime.Singleton);
        builder.RegisterEntryPoint<DungeonDebugPaletteUiController>(Lifetime.Singleton)
            .AsSelf();
        builder.Register<DungeonDebugOverlayPresentationDependencies>(Lifetime.Singleton);
        builder.Register<DungeonDebugOverlayWorldDependencies>(Lifetime.Singleton);
        builder.Register<DungeonDebugOverlayHazardDependencies>(Lifetime.Singleton);
        builder.RegisterEntryPoint<DungeonDebugWorldOverlayController>(Lifetime.Singleton)
            .AsSelf();
        builder.RegisterEntryPoint<DungeonDebugSceneVisibilityController>(Lifetime.Singleton)
            .AsSelf();
        builder.RegisterEntryPoint<DungeonAudioController>(Lifetime.Singleton)
            .As<IDungeonAudioService>();
        builder.RegisterEntryPoint<DungeonSettingsUiController>(Lifetime.Singleton)
            .As<IDungeonSettingsUi>();

        builder.Register<ShopStockCatalog>(Lifetime.Singleton)
            .As<IShopStockCatalog>();
        builder.Register<GridSystemProvider>(Lifetime.Singleton)
            .As<IGridSystemProvider>()
            .As<IGridSystemPublisher>();
        builder.Register<DungeonBackdropSpriteTilingFactory>(Lifetime.Singleton)
            .As<IDungeonBackdropSpriteTilingFactory>();
        builder.Register<WorldInfoClickSelectionService>(Lifetime.Singleton)
            .As<IWorldInfoClickSelector>();
        builder.Register<BuildingInfoPresentationAdapter>(Lifetime.Singleton)
            .As<IBuildingInfoPresentationPort>();
        builder.RegisterEntryPoint<WorldInfoClickInputController>(Lifetime.Singleton)
            .AsSelf();
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
            .As<IModularFacilityWorldSaveService>()
            .As<IDungeonRestoreTransactionParticipant>();
        builder.Register<DungeonStory.Characters.CharacterIdRegistry>(Lifetime.Singleton);
        builder.Register<CharacterIdRegistryAdapter>(Lifetime.Singleton)
            .As<ICharacterIdRegistry>();
        builder.Register<CharacterWorldSaveService>(Lifetime.Singleton)
            .As<ICharacterWorldSaveService>()
            .As<ICharacterWorldPersistenceIdentityQuery>()
            .As<ICharacterHaulDeliveryRestoreQuery>()
            .As<IDungeonRestoreTransactionParticipant>();
        builder.Register<HaulDeliveryIntentRestoreCoordinator>(Lifetime.Singleton)
            .As<IDungeonRestoreTransactionParticipant>();
        builder.Register<OperatingDaySettlementSaveService>(Lifetime.Singleton)
            .As<IOperatingDaySettlementSaveService>();
        builder.Register<EventAlertSaveService>(Lifetime.Singleton)
            .As<IEventAlertSaveService>();
        builder.Register<OffenseSaveService>(Lifetime.Singleton)
            .As<IOffenseSaveService>();
        builder.Register<InvasionSaveRuntimeAdapter>(Lifetime.Singleton)
            .As<IInvasionSaveRuntimePort>();
        builder.Register<InvasionSaveService>(Lifetime.Singleton)
            .As<IInvasionSaveService>()
            .As<IDungeonRestoreTransactionParticipant>();
        builder.Register<GridGhostObjectResolver>(Lifetime.Singleton)
            .As<IGridGhostObjectResolver>();
        builder.Register<GridConstructButtonFactory>(Lifetime.Singleton)
            .As<IGridConstructButtonFactory>();
    }
}
