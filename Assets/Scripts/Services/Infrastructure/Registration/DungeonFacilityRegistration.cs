using System;
using VContainer;

public static class DungeonFacilityRegistration
{
    public static void RegisterDungeonFacilitySystems(
        this IContainerBuilder builder,
        FacilityFeatureSceneRuntimeReferences runtimeReferences)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.RegisterInstance(runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences)));
        builder.Register<FacilityEvolutionRuntimeProvider>(Lifetime.Singleton)
            .As<IFacilityEvolutionRuntimeProvider>();
        builder.Register<FacilitySynthesisRuntimeProvider>(Lifetime.Singleton)
            .As<IFacilitySynthesisRuntimeProvider>();
        builder.Register<DataCatalogFacilitySynthesisRecipeCatalog>(Lifetime.Singleton)
            .As<IFacilitySynthesisRecipeCatalog>();
        builder.Register<FacilitySynthesisRecipeQuery>(Lifetime.Singleton)
            .As<IFacilitySynthesisRecipeQuery>();
        builder.Register<DataCatalogFacilityEvolutionRecipeProvider>(Lifetime.Singleton)
            .As<IFacilityEvolutionRecipeProvider>();
        builder.Register<FacilityEvolutionRecipeQuery>(Lifetime.Singleton)
            .As<IFacilityEvolutionRecipeQuery>();
        builder.Register<RegistryFacilityEvolutionWarehouseInventoryQuery>(Lifetime.Singleton)
            .As<IFacilityEvolutionWarehouseInventoryQuery>();
        builder.Register<WarehouseFacilityEvolutionResourceProvider>(Lifetime.Singleton)
            .As<IFacilityEvolutionResourceProvider>();
        builder.Register<DataCatalogFacilityEvolutionRecordTokenDefinitionProvider>(Lifetime.Singleton)
            .As<IFacilityEvolutionRecordTokenDefinitionProvider>();
        builder.Register<DefaultFacilityEvolutionRecordTokenConsumer>(Lifetime.Singleton)
            .As<IFacilityEvolutionRecordTokenConsumer>();
        builder.Register<GridFacilityEvolutionBuildingReplacerFactory>(Lifetime.Singleton)
            .As<IFacilityEvolutionBuildingReplacerFactory>();
        builder.Register<FacilityEvolutionRecordComponentFactory>(Lifetime.Singleton)
            .As<IFacilityEvolutionRecordComponentFactory>();
        builder.Register<FacilityEvolutionRecordComponentService>(Lifetime.Singleton)
            .As<IFacilityEvolutionRecordComponentService>()
            .As<IFacilityEvolutionRecordProvider>();
        builder.Register<FacilityEvolutionRecordEventRecorder>(Lifetime.Singleton)
            .As<IFacilityEvolutionRecordEventRecorder>();
        builder.Register<FacilityEvolutionStateComponentFactory>(Lifetime.Singleton)
            .As<IFacilityEvolutionStateComponentFactory>();
        builder.Register<DataCatalogCodexReferenceCatalog>(Lifetime.Singleton)
            .As<ICodexReferenceCatalog>();
        builder.Register<CodexReferenceImporter>(Lifetime.Singleton)
            .As<ICodexReferenceImporter>();
        builder.Register<CodexRuntimeProvider>(Lifetime.Singleton)
            .As<ICodexRuntimeProvider>();
    }
}
