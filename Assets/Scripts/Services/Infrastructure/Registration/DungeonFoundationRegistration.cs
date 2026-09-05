using DungeonStory.Foundation;
using VContainer;

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
