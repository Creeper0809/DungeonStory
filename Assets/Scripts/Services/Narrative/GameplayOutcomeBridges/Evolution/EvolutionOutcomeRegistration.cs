using VContainer;

public static class EvolutionOutcomeRegistration
{
    public static void RegisterEvolutionGameplayOutcomeBridges(
        this IContainerBuilder builder)
    {
        builder.Register<EvolutionGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<IApparelChangeOutcomeCommitter>()
            .As<IApparelPhysicalOutcomeCommitter>()
            .As<IProductQualityOutcomeCommitter>()
            .As<IAcquiredTraitInferenceOutcomeCommitter>()
            .As<IAcquiredTraitReactionOutcomeCommitter>()
            .As<IFacilityEvolutionOutcomeCommitter>()
            .As<IMemoryErasureOutcomeCommitter>()
            .As<IMemoryErasureBossAwardOutcomeCommitter>();

        builder.Register<ApparelChangeOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<ApparelPhysicalOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<ProductQualityOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<AcquiredTraitInferenceOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<AcquiredTraitReactionOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<FacilityEvolutionOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<MemoryErasureOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<MemoryErasureBossAwardOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();

        builder.Register<ApparelChangeOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<ApparelPhysicalOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<ProductQualityOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<AcquiredTraitInferenceOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<AcquiredTraitReactionOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<FacilityEvolutionOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<MemoryErasureOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
        builder.Register<MemoryErasureBossAwardOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}
