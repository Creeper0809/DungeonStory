using System;
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

        builder.Register<ResourceDungeonFactionCatalog>(Lifetime.Singleton);
        builder.Register<FactionRuntimeProvider>(Lifetime.Singleton)
            .AsSelf()
            .As<IFactionRuntimeProvider>();
        builder.RegisterEntryPoint<FactionRuntime>(Lifetime.Singleton)
            .As<IFactionRuntime>();
        builder.RegisterEntryPoint<InvasionCampaignRuntime>(Lifetime.Singleton)
            .As<IInvasionCampaignRuntime>();
    }
}
