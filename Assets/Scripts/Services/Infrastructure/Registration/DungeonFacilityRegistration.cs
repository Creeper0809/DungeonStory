using System;
using VContainer;
using VContainer.Unity;

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
        builder.Register<FacilityEvolutionEngineFactory>(Lifetime.Singleton)
            .As<IFacilityEvolutionEngineFactory>();
        builder.Register<FacilityEvolutionRecordComponentFactory>(Lifetime.Singleton)
            .As<IFacilityEvolutionRecordComponentFactory>();
        builder.Register<FacilityEvolutionRecordComponentService>(Lifetime.Singleton)
            .As<IFacilityEvolutionRecordComponentService>()
            .As<IFacilityEvolutionRecordProvider>();
        builder.Register<RoomProfileBuilder>(Lifetime.Singleton)
            .As<IRoomProfileProvider>();
        builder.Register<RuleBasedFacilityEvolutionProposalProvider>(Lifetime.Singleton)
            .As<IFacilityEvolutionProposalProvider>();
        builder.Register<DefaultFacilityEvolutionValidator>(Lifetime.Singleton)
            .As<IFacilityEvolutionValidator>();
        builder.Register<DefaultFacilityEvolutionCandidateBuilder>(Lifetime.Singleton)
            .As<IFacilityEvolutionCandidateBuilder>();
        builder.Register<DefaultFacilityEvolutionMutationResolver>(Lifetime.Singleton)
            .As<IFacilityEvolutionMutationResolver>();
        builder.Register<FacilityEvolutionDefinitionContext>(Lifetime.Singleton);
        builder.Register<FacilityEvolutionExecutionContextFactory>(Lifetime.Singleton)
            .As<IFacilityEvolutionExecutionContextFactory>();
        builder.Register<FacilityEvolutionRecordEventRecorder>(Lifetime.Singleton)
            .As<IFacilityEvolutionRecordEventRecorder>();
        builder.Register<FacilityEvolutionStateComponentFactory>(Lifetime.Singleton)
            .As<IFacilityEvolutionStateComponentFactory>()
            .As<IBuildingEvolutionStatePort>();
        builder.Register<UsageLedgerCompactor>(Lifetime.Singleton)
            .As<IUsageLedgerCompactor>();
        builder.Register<EvolutionModuleRegistry>(Lifetime.Singleton)
            .As<IEvolutionModuleRegistry>();
        builder.Register<FacilityRelocationWorldService>(Lifetime.Singleton)
            .As<IFacilityRelocationWorldService>();
        builder.Register<FacilityInstanceEvolutionRuntime>(Lifetime.Singleton)
            .As<IFacilityEvolutionRuntime>();
        builder.Register<FacilityEvolutionPendingMaterialRestoreGuard>(Lifetime.Singleton)
            .As<IDungeonRestoreTransactionParticipant>();
        builder.RegisterEntryPoint<FacilityEvolutionPendingMaterialProjection>(
            Lifetime.Singleton);
        builder.RegisterEntryPoint<FacilityEvolutionActivationProjection>(
            Lifetime.Singleton);
        builder.Register<FacilityEvolutionModifierQuery>(Lifetime.Singleton)
            .As<IFacilityEvolutionModifierQuery>();
        builder.Register<DataCatalogCodexReferenceCatalog>(Lifetime.Singleton)
            .As<ICodexReferenceCatalog>();
        builder.Register<CodexRuntimeApplicationAdapter>(Lifetime.Singleton)
            .As<ICodexRuntimeApplicationPort>()
            .As<ICodexReferenceSnapshotQueryPort>();
        builder.Register<CodexReferenceImporter>(Lifetime.Singleton)
            .As<ICodexReferenceImporter>();
    }
}
