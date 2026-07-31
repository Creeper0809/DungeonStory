using System;
using VContainer;
using VContainer.Unity;

public static class DungeonProgressionOffenseRegistration
{
    public static void RegisterDungeonProgressionAndOffense(
        this IContainerBuilder builder,
        OffenseSceneRuntimeReferences offenseRuntimeReferences,
        ProgressionSceneRuntimeReferences progressionRuntimeReferences)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.RegisterInstance(offenseRuntimeReferences
            ?? throw new ArgumentNullException(nameof(offenseRuntimeReferences)));
        builder.RegisterInstance(progressionRuntimeReferences
            ?? throw new ArgumentNullException(nameof(progressionRuntimeReferences)));
        builder.Register<RecruitedCharacterActivationService>(Lifetime.Singleton)
            .As<IRecruitedCharacterActivationService>();
        builder.Register<DailyFacilityShopRuntimeProvider>(Lifetime.Singleton)
            .As<IDailyFacilityShopRuntimeProvider>();
        builder.Register<DataCatalogFacilityShopCatalog>(Lifetime.Singleton)
            .As<IFacilityShopCatalog>();
        builder.Register<FacilityShopUnlockStateService>(Lifetime.Singleton)
            .As<IFacilityShopUnlockStateService>();
        builder.Register<ResourceResearchProjectCatalog>(Lifetime.Singleton)
            .As<IResearchProjectCatalog>();
        builder.Register<ResearchGraphLayoutService>(Lifetime.Singleton)
            .As<IResearchGraphLayoutService>();
        builder.Register<ResearchBlueprintArchiveQuery>(Lifetime.Singleton)
            .As<IResearchBlueprintArchiveQuery>();
        builder.Register<BlueprintResearchRuntimeProvider>(Lifetime.Singleton)
            .As<IBlueprintResearchRuntimeProvider>();
        builder.Register<ResearchQueueCommandService>(Lifetime.Singleton)
            .As<IResearchQueueCommandService>();
        builder.Register<BlueprintResearchWorkService>(Lifetime.Singleton)
            .As<IBlueprintResearchWorkService>();
        builder.Register<BlueprintResearchStateService>(Lifetime.Singleton)
            .As<IBlueprintResearchStateService>();

        builder.Register<MetaProgressionRuntimeProvider>(Lifetime.Singleton)
            .As<IMetaProgressionRuntimeProvider>();
        builder.Register<MetaProgressionRuntimeReader>(Lifetime.Singleton)
            .As<IMetaProgressionRuntimeReader>();
        builder.Register<MetaProfileStore>(Lifetime.Singleton)
            .As<IMetaProfileStore>();
        builder.RegisterEntryPoint<MetaProfilePersistenceService>(Lifetime.Singleton);
        builder.Register<DungeonRunTransitionService>(Lifetime.Singleton)
            .As<IDungeonRunTransitionService>();

        builder.Register<OffenseWorldMapRuntimeProvider>(Lifetime.Singleton)
            .As<IOffenseWorldMapRuntimeProvider>();
        builder.Register<OffenseRewardRuntimeProvider>(Lifetime.Singleton)
            .As<IOffenseRewardRuntimeProvider>();
        builder.Register<OffenseExpeditionRuntimeProvider>(Lifetime.Singleton)
            .As<IOffenseExpeditionRuntimeProvider>();
        builder.Register<OffenseExpeditionMemberQuery>(Lifetime.Singleton)
            .As<IOffenseExpeditionMemberQuery>();
        builder.Register<OffenseRegionRuntime>(Lifetime.Singleton)
            .As<IOffenseRegionRuntime>();
        builder.Register<DataCatalogOffenseV17ContentCatalog>(Lifetime.Singleton)
            .As<IOffenseV17ContentCatalog>();
        builder.RegisterEntryPoint<OffenseHexWorldSimulation>(Lifetime.Singleton)
            .AsSelf()
            .As<IOffenseWorldSimulation>()
            .As<IWorldThreatModifierQuery>();
        builder.RegisterDungeonFactionWar();
        builder.Register<OffenseReturnSafetyRuntime>(Lifetime.Singleton)
            .As<IOffenseReturnSafetyRuntime>();
        builder.RegisterEntryPoint<OffenseUrgentMitigationRuntime>(
                Lifetime.Singleton)
            .AsSelf()
            .As<IOffenseUrgentMitigationRuntime>();
        builder.Register<OffenseTravelRuntime>(Lifetime.Singleton)
            .As<IOffenseTravelRuntime>();
        builder.RegisterEntryPoint<OffenseTravelTicker>(Lifetime.Singleton);
        builder.RegisterEntryPoint<OffenseThreatGameplayBridge>(
            Lifetime.Singleton);
        builder.Register<OffenseDecisionRuntime>(Lifetime.Singleton)
            .As<IOffenseDecisionRuntime>();
        builder.Register<OffenseSupplyDecisionEffectHandler>(Lifetime.Singleton)
            .As<IOffenseDecisionEffectHandler>();
        builder.Register<OffenseGoldDecisionEffectHandler>(Lifetime.Singleton)
            .As<IOffenseDecisionEffectHandler>();
        builder.Register<OffenseStressDecisionEffectHandler>(Lifetime.Singleton)
            .As<IOffenseDecisionEffectHandler>();
        builder.Register<OffenseExposureDecisionEffectHandler>(Lifetime.Singleton)
            .As<IOffenseDecisionEffectHandler>();
        builder.Register<OffenseInjuryDecisionEffectHandler>(Lifetime.Singleton)
            .As<IOffenseDecisionEffectHandler>();
        builder.Register<OffenseLootDecisionEffectHandler>(Lifetime.Singleton)
            .As<IOffenseDecisionEffectHandler>();
        builder.Register<OffenseReconDecisionEffectHandler>(Lifetime.Singleton)
            .As<IOffenseDecisionEffectHandler>();
        builder.Register<OffenseTimeDecisionEffectHandler>(Lifetime.Singleton)
            .As<IOffenseDecisionEffectHandler>();
        builder.Register<OffenseEquipmentWearDecisionEffectHandler>(
                Lifetime.Singleton)
            .As<IOffenseDecisionEffectHandler>();
        builder.Register<OffenseForcedMoveDecisionEffectHandler>(Lifetime.Singleton)
            .As<IOffenseDecisionEffectHandler>();
        builder.Register<OffenseCombatDecisionEffectHandler>(Lifetime.Singleton)
            .As<IOffenseDecisionEffectHandler>();
        builder.Register<OffenseDecisionEffectExecutor>(Lifetime.Singleton)
            .As<IOffenseDecisionEffectExecutor>();
        builder.RegisterEntryPoint<KnowledgeResidueProcessingRuntime>(
                Lifetime.Singleton)
            .As<IKnowledgeResidueProcessingRuntime>();
        builder.RegisterEntryPoint<OffenseReturnArrivalRuntime>(Lifetime.Singleton)
            .As<IOffenseReturnArrivalRuntime>();
        builder.Register<DungeonOffensePreparationService>(Lifetime.Singleton)
            .As<IOffensePreparationService>();
        builder.RegisterEntryPoint<OffenseBattleRuntime>(Lifetime.Singleton)
            .As<IOffenseBattleRuntime>();
        builder.Register<OffenseCommandResolutionAdapter>(Lifetime.Singleton)
            .As<IOffenseCommandResolutionAdapter>();
        builder.Register<OffenseBattleDirector>(Lifetime.Singleton)
            .As<IOffenseBattleDirector>();
        builder.Register<CombatCardPresentationService>(Lifetime.Singleton)
            .As<ICombatCardPresentationService>();
        builder.Register<DataCatalogOffenseRewardCatalog>(Lifetime.Singleton)
            .As<IOffenseRewardCatalog>();
        builder.Register<OffenseRewardSelector>(Lifetime.Singleton)
            .As<IOffenseRewardSelector>();
        builder.Register<OffenseMoneyRewardGrantHandler>(Lifetime.Singleton)
            .As<IOffenseRewardGrantHandler>();
        builder.Register<OffenseStockRewardGrantHandler>(Lifetime.Singleton)
            .As<IOffenseRewardGrantHandler>();
        builder.Register<OffenseRareFacilityRewardGrantHandler>(Lifetime.Singleton)
            .As<IOffenseRewardGrantHandler>();
        builder.Register<OffenseBlueprintRewardGrantHandler>(Lifetime.Singleton)
            .As<IOffenseRewardGrantHandler>();
        builder.Register<OffenseRegionalPressureRewardGrantHandler>(Lifetime.Singleton)
            .As<IOffenseRewardGrantHandler>();
        builder.Register<OffenseRecruitCandidateRewardGrantHandler>(Lifetime.Singleton)
            .As<IOffenseRewardGrantHandler>();
        builder.Register<OffensePrisonerRewardGrantHandler>(Lifetime.Singleton)
            .As<IOffenseRewardGrantHandler>();
        builder.Register<OffenseSpecialMonsterRewardGrantHandler>(Lifetime.Singleton)
            .As<IOffenseRewardGrantHandler>();
        builder.Register<OffenseRewardGrantService>(Lifetime.Singleton)
            .As<IOffenseRewardGrantService>();
    }
}
