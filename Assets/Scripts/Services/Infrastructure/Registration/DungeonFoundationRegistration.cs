using DungeonStory.Foundation;
using VContainer;

public static class DungeonFoundationRegistration
{
    public static void RegisterDungeonFoundation(this IContainerBuilder builder)
    {
        builder.Register<UnityGameClock>(Lifetime.Singleton)
            .As<IGameClock>();
        builder.Register<UnityUiClock>(Lifetime.Singleton)
            .As<IUiClock>();
        builder.Register<UnityGameTimeScaleController>(Lifetime.Singleton)
            .As<IGameTimeScaleController>();
        builder.Register(
                _ => new RandomStreamProvider(rootSeed: 1),
                Lifetime.Singleton)
            .As<IRandomStreamProvider>();
        builder.Register<GameEventBus>(Lifetime.Singleton)
            .As<IGameEventBus>();
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
        builder.Register<CharacterAiWorldRegistry>(Lifetime.Singleton)
            .AsImplementedInterfaces();
    }
}
