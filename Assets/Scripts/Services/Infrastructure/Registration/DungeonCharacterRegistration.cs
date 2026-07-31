using System;
using VContainer;
using VContainer.Unity;

public static class DungeonCharacterRegistration
{
    public static void RegisterDungeonCharacterSystems(
        this IContainerBuilder builder,
        CharacterSceneRuntimeReferences runtimeReferences)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.RegisterInstance(runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences)));
        builder.RegisterEntryPoint<DungeonRunFlowRuntime>(Lifetime.Singleton)
            .As<IDungeonRunFlowRuntime>();
        builder.Register<RunVariableRuntimeProvider>(Lifetime.Singleton)
            .As<IRunVariableRuntimeProvider>();
        builder.Register<RunVariableRuntimeReader>(Lifetime.Singleton)
            .As<IRunVariableRuntimeReader>();
        builder.Register<ResourceRunCharacterCatalog>(Lifetime.Singleton)
            .As<IRunCharacterCatalog>();
        builder.Register<ResourceCharacterSpeciesCatalog>(Lifetime.Singleton)
            .As<ICharacterSpeciesCatalog>();
        builder.Register<SpeciesIncidentHandlerRegistry>(Lifetime.Singleton)
            .As<ISpeciesIncidentHandlerRegistry>();
        builder.RegisterEntryPoint<CharacterSpeciesRuntime>(Lifetime.Singleton)
            .As<ICharacterSpeciesRuntime>();
        builder.Register<ResourceOwnerCandidateCatalog>(Lifetime.Singleton)
            .As<IOwnerCandidateCatalog>();
        builder.Register<RunStartVariableCatalog>(Lifetime.Singleton)
            .As<IRunStartVariableCatalog>();
        builder.Register<RunStartVariableSelector>(Lifetime.Singleton)
            .As<IRunStartVariableSelector>();
        builder.Register<CharacterVisualRootFactory>(Lifetime.Singleton)
            .As<ICharacterVisualRootFactory>();
        builder.Register<OwnerRunDataProvider>(Lifetime.Singleton)
            .As<IOwnerRunDataProvider>()
            .As<IOwnerRunManagerProvider>();
        builder.Register<OwnerCharacterFactory>(Lifetime.Singleton)
            .As<IOwnerCharacterFactory>();
        builder.Register<OwnerSelectionOptionButtonFactory>(Lifetime.Singleton)
            .As<IOwnerSelectionOptionButtonFactory>();
        builder.Register<OwnerRunLifecycleService>(Lifetime.Singleton)
            .As<IOwnerRunLifecycleService>();
        builder.Register<CharacterSpawnerProvider>(Lifetime.Singleton)
            .As<ICharacterSpawnerProvider>();
        builder.Register<CharacterSpawnObjectFactory>(Lifetime.Singleton)
            .As<ICharacterSpawnObjectFactory>();
        builder.RegisterEntryPoint<CharacterStatMaintenanceRuntime>(
            Lifetime.Singleton);
        builder.Register<StaffDiscontentRuntimeProvider>(Lifetime.Singleton)
            .As<IStaffDiscontentRuntimeProvider>();
        builder.Register<StaffDiscontentRuntimeService>(Lifetime.Singleton)
            .As<IStaffDiscontentRuntimeService>();
        builder.Register<DungeonWorkforceReplanService>(Lifetime.Singleton)
            .As<IWorkforceReplanService>();
        builder.Register<LocalLlmRuntimeProvider>(Lifetime.Singleton)
            .As<ILocalLlmRuntimeProvider>();
        builder.Register<ResourceCharacterSkillSystemSettingsProvider>(Lifetime.Singleton)
            .As<ICharacterSkillSystemSettingsProvider>();
        builder.Register<CharacterPopulationService>(Lifetime.Singleton)
            .As<ICharacterPopulationService>();
        builder.Register<PreparedStartPartyGameplayApplier>(Lifetime.Singleton)
            .As<IPreparedStartPartyGameplayApplier>();
        builder.Register<PreparedStartPartyCommitService>(Lifetime.Singleton)
            .As<IPreparedStartPartyCommitService>();
        builder.Register<StartPartyPreparationService>(Lifetime.Singleton)
            .As<IStartPartyPreparationService>();
        builder.RegisterEntryPoint<CharacterSkillGenerationService>(Lifetime.Singleton)
            .As<ICharacterSkillGenerationService>();
        builder.RegisterEntryPoint<CharacterSkillAutomaticTriggerRuntime>(Lifetime.Singleton);
        builder.Register<CharacterLogNarrativeService>(Lifetime.Singleton)
            .AsSelf()
            .As<ICharacterLogNarrativeService>();
    }
}
