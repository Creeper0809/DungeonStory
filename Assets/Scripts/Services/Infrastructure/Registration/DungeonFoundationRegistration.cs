using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using VContainer;
using VContainer.Unity;

public static class DungeonFoundationRegistration
{
    public static void RegisterDungeonFoundation(this IContainerBuilder builder)
    {
        builder.Register<DungeonRuntimeAggregateRootStore>(Lifetime.Singleton);
        builder.RegisterMigratedDomainFact<ServiceModeChangedFact>(
            "log:service.mode-changed", ServiceModeChangedFactMapping.Capture);
        builder.Register<ServiceModeFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
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
            .As<IRandomStreamDiagnosticsQuery>()
            .As<IDungeonRestoreTransactionParticipant>();
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
        builder.RegisterMigratedDomainFact<CombatCellShotFact>(
            "log:combat.cell-shot", CombatCellShotFactMapping.Capture);
        builder.Register<CombatCellShotFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<CombatActionFactObserver>(Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.Register<CombatEquipmentCraftCompletionFactObserver>(Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.Register<CombatEquipmentCraftCompletionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<CombatActionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayDomainFact<KnowledgeResidueCompletedFact, KnowledgeCompletionOutcomeReceipt>(
            "log:research.knowledge-completed", KnowledgeCompletionFactObservation.Capture);
        builder.Register<CharacterCorpseConsumptionFactObserver>(Lifetime.Singleton);
        builder.Register<CharacterCorpseConsumptionFactBinding>(Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.Register<CharacterDeprivationExperienceFactBinding>(Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.Register<CharacterWaterFactObserver>(Lifetime.Singleton);
        builder.RegisterMigratedDomainFact<CharacterRestAppliedFact>(
            "log:character-rest-applied", CharacterRestFactMapping.Capture);
        builder.Register<CharacterRestFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<CharacterWaterFactBinding>(Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.RegisterMigratedDomainFact<CharacterCriticalHealthTransitionFact>(
            "log:character.critical-health-transition", CharacterHealingFactMapping.CaptureCritical);
        builder.RegisterMigratedDomainFact<CharacterMobilityRecoveredFact>(
            "log:character.mobility-recovered", CharacterHealingFactMapping.CaptureRecovered);
        builder.Register<CharacterBodyFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterMigratedDomainFact<CharacterFatalityFact>(
            "log:character.fatality", CharacterFatalityFactMapping.CaptureDeath);
        builder.RegisterMigratedDomainFact<CharacterFatalCriticalTransitionFact>(
            "log:character.fatal-critical-transition", CharacterFatalityFactMapping.CaptureCritical);
        builder.RegisterMigratedDomainFact<CharacterFatalDownedFact>(
            "log:character.fatal-downed", CharacterFatalityFactMapping.CaptureDowned);
        builder.Register<ReproductionDeathParentPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<CharacterWaterFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<CharacterCorpseConsumptionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterMigratedDomainFact<CharacterProficiencyAwardCommitReceipt>(
            "log:character.proficiency-award", ProficiencyAwardFactMapping.Capture);
        builder.Register<FactionDomainFactMapping>(Lifetime.Singleton);
        builder.Register(resolver => new MigratedDomainFactObserverBinding<FactionTrustChangedDomainFact>(
            "log:faction.trust-changed", resolver.Resolve<FactionDomainFactMapping>().Capture,
            resolver.Resolve<MigratedProducerOutcomeRuntime>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.Register(resolver => new MigratedDomainFactObserverBinding<FactionBetrayalDomainFact>(
            "log:faction.betrayal", resolver.Resolve<FactionDomainFactMapping>().Capture,
            resolver.Resolve<MigratedProducerOutcomeRuntime>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.Register(resolver => new MigratedDomainFactObserverBinding<FactionRouteSettlementDomainFact>(
            "log:faction.route-settlement", resolver.Resolve<FactionDomainFactMapping>().Capture,
            resolver.Resolve<MigratedProducerOutcomeRuntime>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.Register(resolver => new MigratedDomainFactObserverBinding<FactionRouteArrivedDomainFact>(
            "log:faction.route-arrived", resolver.Resolve<FactionDomainFactMapping>().Capture,
            resolver.Resolve<MigratedProducerOutcomeRuntime>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.Register(resolver => new MigratedDomainFactObserverBinding<FactionGoodwillTransferredFact>(
            "log:faction.goodwill-transferred", new FactionGoodwillFactMapping(resolver.Resolve<IGameplayOutcomeDiagnosticsQuery>()).Capture,
            resolver.Resolve<MigratedProducerOutcomeRuntime>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.Register<FactionObservationPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register(resolver => new MigratedDomainFactObserverBinding<FactionCargoDeliveredFact>(
            "log:faction.cargo-delivered",
            new FactionCargoFactMapping(resolver.Resolve<IGameplayOutcomeDiagnosticsQuery>()).Capture,
            resolver.Resolve<MigratedProducerOutcomeRuntime>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.Register<FactionCargoFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterMigratedDomainFact<ExteriorVisitorReceptionFact>(
            "log:exterior.visitor-reception", ExteriorVisitorReceptionFactMapping.Capture);
        builder.RegisterMigratedDomainFact<CharacterMealConsumedFact>(
            "log:survival.meal-consumed", CharacterConsumptionFactMapping.Capture);
        builder.RegisterMigratedDomainFact<CharacterSubstanceConsumedFact>(
            "log:survival.substance-consumed", CharacterConsumptionFactMapping.Capture);
        builder.RegisterMigratedDomainFact<CharacterDetoxAppliedFact>(
            "log:survival.detox-applied", CharacterConsumptionFactMapping.Capture);
        builder.RegisterMigratedDomainFact<CharacterPrimitiveMealCompletedFact>(
            "log:survival.primitive-meal", CharacterConsumptionFactMapping.Capture);
        builder.RegisterMigratedDomainFact<AutoProcurementCompletedFact>(
            "log:economy.auto-procurement", AutoProcurementFactMapping.Capture);
        builder.RegisterMigratedDomainFact<WorldResourceCycleCompletedFact>(
            "log:economy.world-resource-cycle", WorldResourceCycleFactMapping.Capture);
        builder.RegisterMigratedDomainFact<AnimalHusbandryCommandFact>(
            "log:economy.husbandry-command", AnimalHusbandryCommandFactMapping.Capture);
        builder.RegisterMigratedDomainFact<AnimalHusbandryWorkFact>(
            "log:economy.husbandry-work", AnimalHusbandryWorkFactMapping.Capture);
        builder.Register<ResourceStockSaleFactMapping>(Lifetime.Singleton);
        builder.Register(resolver => new MigratedDomainFactObserverBinding<ResourceStockSaleFact>(
            "log:economy.surplus-stock-sale", resolver.Resolve<ResourceStockSaleFactMapping>().Capture,
            resolver.Resolve<MigratedProducerOutcomeRuntime>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.Register(resolver => new MigratedDomainFactObserverBinding<QualityRejectedSaleFact>(
            "log:economy.quality-rejected-sale", resolver.Resolve<ResourceStockSaleFactMapping>().Capture,
            resolver.Resolve<MigratedProducerOutcomeRuntime>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
            Lifetime.Singleton).As<IDomainFactObserverBinding>();
        builder.RegisterInstance(GameplayOutcomeBufferLimits.Default)
            .AsSelf();
        builder.Register(resolver => GameplayOutcomeRegistry.FromModules(
                resolver.Resolve<System.Collections.Generic.IEnumerable<IGameplayActionLogModule>>()),
                Lifetime.Singleton)
            .As<IGameplayOutcomeRegistry>()
            .As<IGameplayActionLogRegistry>();
        builder.Register(
                resolver => new GameplayOutcomeLedger(
                    resolver.Resolve<IGameplayOutcomeRegistry>(),
                    resolver.Resolve<GameplayOutcomeBufferLimits>(),
                    resolver.Resolve<IGameCalendar>()),
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
        builder.Register<GameplayDomainFactCommitGroup>(Lifetime.Singleton);
        builder.Register<GameplayDomainFactDeliveryQuery>(Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactDeliveryQuery>();
        builder.Register<DungeonStory.Foundation.Facts.DomainFactDispatcher>(Lifetime.Singleton)
            .AsSelf()
            .As<DungeonStory.Foundation.Facts.IDomainFactPublisher>()
            .As<DungeonStory.Foundation.Facts.IDomainFactRecoveryPublisher>()
            .As<DungeonStory.Foundation.Facts.IDomainFactStagingPublisher>();
        builder.Register<MigratedProducerOutcomeRuntime>(Lifetime.Singleton)
            .AsSelf()
            .As<IMigratedProducerOutcomeTransaction>()
            .As<IMigratedProducerOutcomeExternalBatchTransaction>()
            .As<IMigratedProducerOutcomePersistence>();
        builder.RegisterTradeInventoryGameplayOutcomes();
        builder.RegisterBuildingDemolitionGameplayOutcomes();
        builder.RegisterPolicyStatusChangeGameplayOutcomes();
        builder.RegisterGameplayDomainFact<DoorHeldOpenFact, PolicyStatusChangeReceipt>(
            "log:door.held-open", DoorHeldOpenFactMapping.Capture);
        builder.RegisterPolicyValueChangeGameplayOutcomes();
        builder.RegisterGameplayDomainFact<CharacterConsumablesPolicyChangedFact, PolicyValueChangeReceipt>(
            "log:consumables.policy", CharacterConsumablesPolicyFactMapping.Capture);
        builder.RegisterGameplayDomainFact<FamilyPlanningPolicyChangedFact, PolicyValueChangeReceipt>(
            "log:family-planning.policy", FamilyPlanningPolicyFactMapping.Capture);
        builder.Register<BuildingVisitEventPublisher>(Lifetime.Singleton)
            .As<IBuildingVisitEventPort>();
        builder.RegisterMigratedDomainFact<FacilityUseCompletedFact>(
            "log:facility.use-completed", FacilityUseFactMapping.Capture);
        builder.Register<FacilityUseFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
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
