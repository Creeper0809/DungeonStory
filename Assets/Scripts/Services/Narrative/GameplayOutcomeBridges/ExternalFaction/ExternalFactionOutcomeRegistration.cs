using VContainer;

public static class ExternalFactionOutcomeRegistration
{
    public static void RegisterExternalFactionGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register<ExternalFactionGameplayOutcomeBridge>(
                Lifetime.Singleton)
            .As<IOffenseTruthRevealOutcomeCommitter>();
        builder.Register<OffenseTruthRevealOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<OffenseTruthRevealOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}
