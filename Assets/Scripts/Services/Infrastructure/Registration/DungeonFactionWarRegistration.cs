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
        builder.Register<FactionItemLogisticsDependencies>(Lifetime.Singleton);
        builder.Register<FactionCharacterSpawnDependencies>(Lifetime.Singleton);
        builder.RegisterEntryPoint<FactionRuntimeApplicationAdapter>(Lifetime.Singleton)
            .As<IFactionRuntime>()
            .As<IFactionContractQuery>();
        builder.RegisterEntryPoint<InvasionCampaignRuntime>(Lifetime.Singleton)
            .As<IInvasionCampaignRuntime>();
        builder.RegisterEntryPoint<MilestonePressureApplicationAdapter>(
            Lifetime.Singleton);
    }
}
