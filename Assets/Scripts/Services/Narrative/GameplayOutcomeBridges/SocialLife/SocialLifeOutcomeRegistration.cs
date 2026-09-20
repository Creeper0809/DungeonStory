using VContainer;

public static class SocialLifeOutcomeRegistration
{
    public static void RegisterSocialLifeGameplayOutcomeBridges(
        this IContainerBuilder builder)
    {
        builder.Register<SocialLifeGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<ISocialLifeOutcomeCommitter>();
        builder.Register<StaffDiscontentGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<IStaffDiscontentOutcomeCommitter>();
        builder.Register<CaptivePerformerMilestoneGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<ICaptivePerformerMilestoneOutcomeCommitter>();
        builder.Register<CaptiveEscapeGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<ICaptiveEscapeOutcomeCommitter>();
        builder.Register<CaptiveRansomGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<ICaptiveRansomOutcomeCommitter>();
        builder.Register<CaptivityInteractionGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<ICaptivityInteractionOutcomeCommitter>();
        builder.Register<SocialConflictOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<ApologyOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<StaffDiscontentOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<CaptivePerformerMilestoneOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<CaptiveEscapeOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<CaptiveRansomOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<CaptivityInteractionOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<SocialConflictOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<ApologyOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<StaffDiscontentOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<CaptivePerformerMilestoneOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<CaptiveEscapeOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<CaptiveRansomOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<CaptivityInteractionOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}
