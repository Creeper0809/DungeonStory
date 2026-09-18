using VContainer;

public static class TradeInventoryOutcomeRegistration
{
    public static void RegisterTradeInventoryGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register<TradeInventoryGameplayOutcomeBridge>(Lifetime.Singleton)
            .As<ITradeInventoryOutcomeCommitter>();
        builder.Register<DefaultPhysicalBatchDispositionOutcomeParticipant>(
                Lifetime.Singleton)
            .As<IDefaultPhysicalItemBatchDispositionOutcomeParticipant>();
        builder.Register<DefaultPhysicalItemRelocationOutcomeParticipant>(
                Lifetime.Singleton)
            .As<IPhysicalItemRelocationOutcomeParticipant>();
        builder.Register<WastePolicyGameplayOutcomeParticipant>(
                Lifetime.Singleton)
            .As<IWastePolicyGameplayOutcomeParticipant>();
        builder.Register<TradeInventoryOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<TradeInventoryOutcomeDescriptor>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}
