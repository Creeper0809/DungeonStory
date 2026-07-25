using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

public static class DungeonWorldSimulationRegistration
{
    public static void RegisterDungeonWorldSimulation(
        this IContainerBuilder builder,
        Scene scopeScene,
        WorldSimulationSceneReferences sceneReferences)
    {
        builder.RegisterInstance(sceneReferences
            ?? throw new System.ArgumentNullException(nameof(sceneReferences)));
        builder.Register<WorldDropZoneQuery>(Lifetime.Singleton)
            .As<IWorldDropZoneQuery>();
        builder.RegisterEntryPoint<ExteriorActivityRuntime>(Lifetime.Singleton)
            .As<IExteriorActivityRuntime>()
            .As<IExteriorZoneQuery>()
            .As<IExteriorPatrolRuntime>()
            .As<IExteriorIncidentRuntime>()
            .As<IExpeditionDepartureService>()
            .As<IExpeditionReturnService>();
        builder.Register<ResourceDungeonItemCatalogProvider>(Lifetime.Singleton)
            .As<IDungeonItemCatalogProvider>();
        builder.Register<ResourceItemHaulingSettingsProvider>(
                Lifetime.Singleton)
            .As<IItemHaulingSettingsProvider>();
        builder.Register<ItemMarkerPresenter>(Lifetime.Singleton)
            .As<IItemMarkerPresenter>();
        builder.Register<WorldItemRepository>(Lifetime.Singleton);
        builder.Register<ItemReservationService>(Lifetime.Singleton)
            .As<IItemReservationService>();
        builder.Register<WorldItemSpawner>(Lifetime.Singleton)
            .As<IWorldItemSpawner>();
        builder.Register<WorldItemQueryService>(Lifetime.Singleton)
            .AsSelf()
            .As<IWorldItemQueryService>();
        builder.Register<WorldItemHaulPlanningService>(Lifetime.Singleton)
            .As<IWorldItemHaulPlanningService>();
        builder.Register<ItemTransferService>(Lifetime.Singleton)
            .As<IItemTransferService>();
        builder.RegisterEntryPoint<WorldItemStackRuntime>(Lifetime.Singleton)
            .As<IWorldItemStackRuntime>();
        builder.RegisterEntryPoint<WorldFilthRuntime>(Lifetime.Singleton)
            .As<IWorldFilthQuery>();
        builder.RegisterEntryPoint<WorldWaterRuntime>(Lifetime.Singleton)
            .As<IWorldWaterQuery>();
        if (SampleSceneRationRuntime.SupportsScene(scopeScene.name))
        {
            builder.RegisterEntryPoint<SampleSceneRationRuntime>(
                    Lifetime.Singleton)
                .AsSelf();
        }

        builder.RegisterEntryPoint<CharacterDeprivationRuntime>(
                Lifetime.Singleton)
            .As<ICharacterDeprivationRuntime>();
        builder.Register<WorkOrderRuntime>(Lifetime.Singleton)
            .As<IWorkOrderRuntime>();
        builder.Register<ResourceWildlifeSpeciesCatalogProvider>(
                Lifetime.Singleton)
            .As<IWildlifeSpeciesCatalogProvider>();
        builder.RegisterEntryPoint<WildlifeHabitatMarkerRegistry>(
                Lifetime.Singleton)
            .AsSelf()
            .As<IWildlifeHabitatMarkerQuery>()
            .As<IWildlifeHabitatMarkerRegistry>();
        builder.RegisterEntryPoint<WildlifeEcosystemRuntime>(
                Lifetime.Singleton)
            .AsSelf()
            .As<IWildlifeEcosystemRuntime>();
        builder.Register<WildlifeCarcassService>(Lifetime.Singleton)
            .As<IWildlifeCarcassService>();
        builder.RegisterEntryPoint<WildlifeRuntime>(Lifetime.Singleton)
            .AsSelf()
            .As<IWildlifeRuntime>()
            .As<IWildlifeQuery>()
            .As<IWildlifeHuntCommandService>();
        builder.RegisterEntryPoint<SurvivalFoodRuntime>(Lifetime.Singleton)
            .As<ISurvivalFoodRuntime>();
        builder.RegisterEntryPoint<ItemStackViewToggleRuntime>(
            Lifetime.Singleton);
        builder.RegisterEntryPoint<WildlifeEcosystemViewToggleRuntime>(
            Lifetime.Singleton);
    }
}
