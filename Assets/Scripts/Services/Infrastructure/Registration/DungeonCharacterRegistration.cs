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
        builder.Register<ExperiencePacingApplicationAdapter>(
            Lifetime.Singleton);
        builder.RegisterEntryPoint<ExperiencePacingRuntime>(Lifetime.Singleton)
            .AsSelf()
            .As<IExperiencePacingRuntime>();
        builder.RegisterEntryPoint<DungeonRunFlowRuntime>(Lifetime.Singleton)
            .As<IDungeonRunFlowRuntime>();
        builder.Register<RunVariableRuntimeReader>(Lifetime.Singleton)
            .As<IRunVariableRuntimeReader>()
            .As<IRunSeedProvider>()
            .As<ISurvivalPressureProvider>();
        builder.Register<ResourceRunCharacterCatalog>(Lifetime.Singleton)
            .As<IRunCharacterCatalog>();
        builder.Register<ResourceCharacterSpeciesCatalog>(Lifetime.Singleton)
            .As<ICharacterSpeciesCatalog>()
            .As<ICharacterSpeciesDefinitionCatalog>()
            .As<ICharacterSpeciesEnvironmentCatalog>();
        builder.Register<SpeciesIncidentHandlerRegistry>(Lifetime.Singleton)
            .As<ISpeciesIncidentHandlerRegistry>();
        builder.RegisterEntryPoint<CharacterSpeciesRuntime>(Lifetime.Singleton)
            .As<ICharacterSpeciesQuery>()
            .As<ICharacterSpeciesCommand>()
            .As<ICharacterSpeciesPersistence>();
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
        builder.Register<CharacterStatMaintenanceSceneAdapter>(Lifetime.Singleton)
            .As<DungeonStory.Characters.ICharacterStatMaintenancePort>();
        builder.Register<DungeonStory.Characters.CharacterStatMaintenanceRuntime>(
            Lifetime.Singleton);
        builder.RegisterEntryPoint<CharacterStatMaintenanceRuntimeAdapter>(
                Lifetime.Singleton)
            .AsSelf();
        builder.Register<StaffDiscontentRuntimeService>(Lifetime.Singleton)
            .As<IStaffDiscontentRuntimeService>();
        builder.Register<DungeonWorkforceReplanService>(Lifetime.Singleton)
            .As<IWorkforceReplanService>()
            .As<IBuildingWorkforceReplanPort>();
        builder.Register<LocalLlmRuntimeProvider>(Lifetime.Singleton)
            .As<ILocalLlmRuntimeProvider>();
        builder.Register<ResourceCharacterSkillSystemSettingsProvider>(Lifetime.Singleton)
            .As<ICharacterSkillSystemSettingsProvider>();
        builder.Register<CharacterProgressionProfileProjector>(
            Lifetime.Transient);
        builder.Register<CharacterProgressionNotificationApplicationAdapter>(
            Lifetime.Singleton);
        builder.Register<CharacterStatsProjectionService>(Lifetime.Singleton);
        builder.Register<CharacterNeedStateService>(Lifetime.Singleton);
        builder.Register<CharacterMoodStateService>(Lifetime.Singleton);
        builder.Register<CharacterStatsVitalsService>(Lifetime.Singleton);
        builder.Register<CharacterStatsMaintenanceSchedule>(Lifetime.Transient);
        builder.Register<CharacterPopulationApplicationAdapter>(Lifetime.Singleton);
        builder.Register<CharacterPopulationService>(Lifetime.Singleton)
            .As<ICharacterPopulationService>();
        builder.Register<PreparedStartPartyCharacterContext>(Lifetime.Singleton);
        builder.Register<PreparedStartPartyWorldContext>(Lifetime.Singleton);
        builder.Register<PreparedStartPartyGameplayApplier>(Lifetime.Singleton)
            .As<IPreparedStartPartyGameplayApplier>()
            .As<IPreparedStartPartyDiagnosticsQuery>();
        builder.Register<PreparedStartPartyCommitService>(Lifetime.Singleton)
            .As<IPreparedStartPartyCommitService>();
        builder.Register<StartPartyPreparationService>(Lifetime.Singleton)
            .As<IStartPartyPreparationService>();
        builder.RegisterEntryPoint<CharacterSkillGenerationService>(Lifetime.Singleton)
            .As<ICharacterSkillGenerationService>();
        builder.RegisterEntryPoint<CharacterSkillAutomaticTriggerRuntime>(
                Lifetime.Singleton)
            .AsSelf();
        builder.Register<CharacterRecordTemplateBank>(Lifetime.Singleton);
        builder.Register<CharacterLogNarrativeService>(Lifetime.Singleton)
            .AsSelf()
            .As<ICharacterLogNarrativeService>();
    }
}
