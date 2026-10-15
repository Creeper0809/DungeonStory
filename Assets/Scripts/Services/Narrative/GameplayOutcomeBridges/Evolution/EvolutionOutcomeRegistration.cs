using VContainer;
using VContainer.Unity;

public static class EvolutionOutcomeRegistration
{
    public static void RegisterEvolutionGameplayOutcomeBridges(
        this IContainerBuilder builder)
    {
        builder.Register<TraitAnalysisFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<CharacterAmbitionTransitionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<CharacterSkillAcquiredFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<CharacterTraitInferredFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<CharacterTraitReactionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<MemoryErasureTerminalFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<MemoryErasureBossAwardFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<EquipmentModuleSelectedFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<ApparelLoadoutChangedFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<ApparelPhysicalCompletedFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<FacilityEvolutionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterMigratedDomainFact<CharacterLifeTransitionFact>(
            "log:character.life-transition", CharacterLifeTransitionFactMapping.Capture);
        builder.RegisterGameplayDomainFact<AgeConditionAnatomyAppliedFact, AgeConditionAnatomyOutcomeReceipt>(
            "log:character.age-condition-anatomy", AgeConditionAnatomyFactMapping.Capture);
        builder.Register<CharacterLifeFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayDomainFact<DiseaseAnatomyBurdenAppliedFact, DiseaseAnatomyBurdenOutcomeReceipt>(
            "log:character.disease-body-burden", DiseaseAnatomyBurdenFactMapping.Capture);
        builder.Register<DiseaseAnatomyBurdenFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayDomainFact<CombatEquipmentQualityResolvedFact, ProductQualityOutcomeReceipt>(
            "log:combat.craft-quality", CombatEquipmentQualityFactMapping.Capture);
        builder.Register<CombatEquipmentQualityFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<ReproductionTransitionFactMapping>(Lifetime.Singleton);
        builder.Register(resolver => new GameplayDomainFactObserverBinding<ReproductionTransitionFact, ReproductionProcessTransitionOutcomeReceipt>(
            "log:character.reproduction-transition", resolver.Resolve<ReproductionTransitionFactMapping>().Capture,
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()), Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register<ReproductionTransitionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register(resolver => FacilityEvolutionCompletedFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => HeritableTraitsAnalyzedFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => CharacterAmbitionTransitionFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => CharacterSkillAcquiredFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => CharacterTraitInferredFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => CharacterTraitReactionFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => MemoryErasureTerminalFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => MemoryErasureBossAwardFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => EquipmentModuleSelectedFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => ApparelLoadoutChangedFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => ApparelPhysicalCompletedFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.RegisterEntryPoint<MemoryErasureActivityObserver>(Lifetime.Singleton);
        builder.Register<EvolutionGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<IProductQualityOutcomeCommitter>();

        builder.RegisterGameplayActionLogs(nameof(ApparelChangeOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(ApparelPhysicalOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(ProductQualityOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CharacterSkillAcquiredOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(CharacterAmbitionTransitionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(AgeConditionAnatomyOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(ReproductionProcessTransitionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(EquipmentModuleSelectionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(AcquiredTraitInferenceOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(HeritableTraitAnalysisOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(AcquiredTraitReactionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(FacilityEvolutionOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(MemoryErasureOutcomeAdapter));
        builder.RegisterGameplayActionLogs(nameof(MemoryErasureBossAwardOutcomeAdapter));

    }
}
