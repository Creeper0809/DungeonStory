using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using VContainer;
using VContainer.Unity;

public static class DungeonFoundationRegistration
{
    public static void RegisterDungeonFoundation(this IContainerBuilder builder)
    {
        builder.Register<DungeonRuntimeAggregateRootStore>(Lifetime.Singleton);
        builder.Register<UnityGameClock>(Lifetime.Singleton)
            .As<IGameClock>();
        builder.Register<UnityUiClock>(Lifetime.Singleton)
            .As<IUiClock>();
        builder.Register<UnityGameTimeScaleController>(Lifetime.Singleton)
            .As<IGameTimeScaleController>();
        builder.Register(
                resolver => new RandomStreamProvider(
                    resolver.Resolve<DungeonRuntimeAggregateRootStore>()),
                Lifetime.Singleton)
            .As<IRandomStreamProvider>()
            .As<IRandomStreamDiagnosticsQuery>();
        builder.Register<GameEventBus>(Lifetime.Singleton)
            .As<IGameEventBus>();
        builder.Register(
                _ => new KoreanJosaResolutionCache(
                    KoreanJosaResolutionCache.DefaultCapacity),
                Lifetime.Singleton)
            .AsSelf();
        builder.Register<KoreanJosaFormatter>(Lifetime.Singleton)
            .As<IKoreanJosaFormatter>();
        builder.RegisterMigratedProducerGameplayOutcomes();
        builder.RegisterInstance(GameplayOutcomeBufferLimits.Default)
            .AsSelf();
        builder.Register<GameplayOutcomeRegistry>(Lifetime.Singleton)
            .As<IGameplayOutcomeRegistry>();
        builder.Register(
                resolver => new GameplayOutcomeLedger(
                    resolver.Resolve<IGameplayOutcomeRegistry>(),
                    resolver.Resolve<GameplayOutcomeBufferLimits>()),
                Lifetime.Singleton)
            .AsSelf()
            .As<IGameplayOutcomeQuery>()
            .As<IGameplayOutcomeMemoryCommands>()
            .As<IGameplayOutcomeConsolidationService>()
            .As<IGameplayOutcomeDiagnosticsQuery>()
            .As<IGameplayOutcomePersistence>()
            .As<IDungeonRestoreTransactionParticipant>();
        builder.Register<GameplayOutcomeRecorder>(Lifetime.Singleton)
            .As<IGameplayOutcomeRecorder>();
        builder.Register<MigratedProducerOutcomeRuntime>(Lifetime.Singleton)
            .As<IMigratedProducerOutcomeTransaction>()
            .As<IMigratedProducerOutcomeExternalBatchTransaction>()
            .As<IMigratedProducerOutcomePersistence>();
        builder.RegisterEntryPoint<GameplayOutcomeConsolidationRuntime>(
                Lifetime.Singleton)
            .As<IInitializable>()
            .As<ITickable>()
            .As<IGameplayOutcomeConsolidationPumpDiagnostics>();
        builder.RegisterTradeInventoryGameplayOutcomes();
        builder.RegisterBuildingDemolitionGameplayOutcomes();
        builder.Register<BuildingVisitEventPublisher>(Lifetime.Singleton)
            .As<IBuildingVisitEventPort>();
        builder.Register<GuidPersistentIdGenerator>(Lifetime.Singleton)
            .As<IPersistentIdGenerator>();
        builder.Register<DynamicFrameWorkBudget>(Lifetime.Singleton)
            .As<IDynamicFrameWorkBudget>();
        builder.RegisterInstance(DefaultGridTraversalCostPolicy.Instance)
            .AsSelf()
            .As<IGridTraversalCostPolicy>();
        builder.Register<GridPathSearchBroker>(Lifetime.Singleton)
            .As<IGridPathSearchBroker>();

        builder.Register<SceneRuntimeRegistry<CharacterActor>>(Lifetime.Singleton)
            .As<ISceneRuntimeRegistry<CharacterActor>>();
        builder.Register<SceneRuntimeRegistry<WildlifeActor>>(Lifetime.Singleton)
            .As<ISceneRuntimeRegistry<WildlifeActor>>();
        builder.Register<SceneRuntimeRegistry<BuildableObject>>(Lifetime.Singleton)
            .As<ISceneRuntimeRegistry<BuildableObject>>();
        builder.Register<SceneRuntimeRegistry<IWarehouseFacility>>(Lifetime.Singleton)
            .As<ISceneRuntimeRegistry<IWarehouseFacility>>();
        builder.Register<SceneRuntimeRegistry<IRetailFacility>>(Lifetime.Singleton)
            .As<ISceneRuntimeRegistry<IRetailFacility>>();
        builder.Register<RestoreWorldCandidateIndex>(Lifetime.Singleton)
            .As<IRestoreWorldCandidateQuery>()
            .As<IRestoreWorldCandidatePublisher>()
            .As<IRestoreHaulDeliveryIntentCandidateQuery>();
        builder.Register<CharacterAiWorldRegistry>(Lifetime.Singleton)
            .AsImplementedInterfaces();
    }
}
