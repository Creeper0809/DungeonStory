using System;
using DungeonStory.Factions;
using VContainer;
using VContainer.Unity;

public static class DungeonFactionWarRegistration
{
    public static void RegisterDungeonFactionWar(this IContainerBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Register<ResourceDungeonFactionCatalogApplicationAdapter>(Lifetime.Singleton);
        builder.Register<ResourceFactionAllianceBenefitBudgetApplicationAdapter>(
            Lifetime.Singleton);
        builder.Register<FactionItemLogisticsDependencies>(Lifetime.Singleton);
        builder.Register<FactionCharacterSpawnDependencies>(Lifetime.Singleton);
        builder.Register<PaidMarketPurchaseFactionRouteEconomicPolicy>(
                Lifetime.Singleton)
            .As<IFactionRouteEconomicPolicy>();
        builder.Register<AllianceBenefitFactionRouteEconomicPolicy>(
                Lifetime.Singleton)
            .As<IFactionRouteEconomicPolicy>();
        builder.Register<FactionRouteEconomicPolicyRegistry>(Lifetime.Singleton)
            .As<IFactionRouteEconomicPolicyRegistry>();
        builder.RegisterEntryPoint<FactionRuntimeApplicationAdapter>(Lifetime.Singleton)
            .As<IFactionRuntime>()
            .As<IFactionContractQuery>()
            .As<IDungeonSaveCaptureGuard>();
        builder.RegisterEntryPoint<InvasionCampaignRuntime>(Lifetime.Singleton)
            .As<IInvasionCampaignRuntime>();
        builder.RegisterEntryPoint<MilestonePressureApplicationAdapter>(
            Lifetime.Singleton);
    }
}
