using VContainer;

public static class SocialLifeOutcomeRegistration
{
    public static void RegisterSocialLifeGameplayOutcomeBridges(
        this IContainerBuilder builder)
    {
        builder.Register<SocialLifeGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<ISocialLifeOutcomeCommitter>();
        builder.Register<SocialConflictOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<ApologyOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<SocialConflictOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<ApologyOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}
