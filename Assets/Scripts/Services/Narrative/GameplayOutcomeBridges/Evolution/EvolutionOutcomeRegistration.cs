using VContainer;

public static class EvolutionOutcomeRegistration
{
    public static void RegisterEvolutionGameplayOutcomeBridges(
        this IContainerBuilder builder)
    {
        builder.Register<EvolutionGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<IApparelChangeOutcomeCommitter>()
            .As<IAcquiredTraitReactionOutcomeCommitter>()
            .As<IFacilityEvolutionOutcomeCommitter>()
            .As<IMemoryErasureOutcomeCommitter>();

        builder.Register<ApparelChangeOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<AcquiredTraitReactionOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<FacilityEvolutionOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<MemoryErasureOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();

        builder.Register<ApparelChangeOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<AcquiredTraitReactionOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<FacilityEvolutionOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<MemoryErasureOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}
