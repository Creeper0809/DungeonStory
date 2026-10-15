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
        builder.Register<PhysicalItemLegacyOutcomeServices>(Lifetime.Singleton)
            .As<IOutcomeAwarePhysicalItemBatchDispositionService>()
            .As<IOutcomeAwareReservedPhysicalItemBatchDispositionService>();
        builder.Register(resolver => new GameplayDomainFactObserverBinding<
                PhysicalItemDisposedFact, TradeInventoryOutcomeReceipt>(
                "items.physical-disposition-observer", PhysicalItemDispositionFactObservation.Capture,
                resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => new GameplayDomainFactObserverBinding<
                PhysicalItemRelocatedFact, TradeInventoryOutcomeReceipt>(
                "items.physical-relocation-observer", PhysicalItemRelocationFactObservation.Capture,
                resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.RegisterGameplayDomainFact<WastePolicyChangedFact, TradeInventoryOutcomeReceipt>(
            "waste.policy-change-observer", WastePolicyFactObservation.Capture);
        builder.Register<WastePolicyFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayActionLogs(nameof(TradeInventoryOutcomeAdapter));
        builder.Register<PhysicalItemRelocationFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<PhysicalItemDispositionFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
    }
}
