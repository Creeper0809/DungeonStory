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
        builder.Register<BlueprintResearchRuntimeProvider>(Lifetime.Singleton)
            .As<IBlueprintResearchRuntimeProvider>();
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
        builder.Register<DungeonOffensePreparationService>(Lifetime.Singleton)
            .As<IOffensePreparationService>();
        builder.Register<ResourceExpeditionEquipmentCatalogProvider>(Lifetime.Singleton)
            .As<IExpeditionEquipmentCatalogProvider>();
        builder.Register<ExpeditionEquipmentRuntime>(Lifetime.Singleton)
            .As<IExpeditionEquipmentRuntime>();
        builder.RegisterEntryPoint<OffenseBattleRuntime>(Lifetime.Singleton)
            .As<IOffenseBattleRuntime>();
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
        builder.Register<OffenseHumanFactionRewardGrantHandler>(Lifetime.Singleton)
            .As<IOffenseRewardGrantHandler>();
        builder.Register<OffenseRivalFactionRewardGrantHandler>(Lifetime.Singleton)
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
