#if UNITY_EDITOR
using System;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

internal sealed class MigratedProducerOutcomeEditorFixture
{
    internal MigratedProducerOutcomeEditorFixture(
        string runId,
        DungeonRuntimeAggregateRootStore aggregateRootStore = null,
        MigratedProducerOutcomeDeliveryFault deliveryFault = null)
    {
        string canonicalRunId = runId?.Trim() ?? string.Empty;
        if (canonicalRunId.Length == 0)
            throw new ArgumentException("A fixture run ID is required.", nameof(runId));

        var adapter = new MigratedProducerOutcomeAdapter();
        IGameplayOutcomeDescriptorCatalog descriptorCatalog = deliveryFault == null
            ? new MigratedProducerOutcomeDescriptorCatalog(
                new KoreanJosaFormatter())
            : new FaultingMigratedProducerOutcomeDescriptorCatalog(
                new KoreanJosaFormatter(),
                deliveryFault);
        Registry = new GameplayOutcomeRegistry(
            Array.Empty<IGameplayOutcomeDescriptor>(),
            new IGameplayOutcomeAdapterRegistration[] { adapter },
            new IGameplayOutcomeDescriptorCatalog[] { descriptorCatalog });
        var limits = new GameplayOutcomeBufferLimits(
            smallPageCount: 128,
            largePageCount: 8,
            knownResultKeyCapacity: 512,
            retainedSmallPageCount: 128,
            retainedLargePageCount: 8);
        Ledger = new GameplayOutcomeLedger(
            Registry,
            limits,
            new GameplayOutcomeRunId(canonicalRunId),
            1L);
        Recorder = new GameplayOutcomeRecorder(
            Ledger,
            Registry,
            new GameEventBus());
        AggregateRootStore = aggregateRootStore
            ?? new DungeonRuntimeAggregateRootStore();
        Transaction = new MigratedProducerOutcomeRuntime(
            AggregateRootStore,
            Ledger,
            Recorder,
            Ledger);
    }

    internal DungeonRuntimeAggregateRootStore AggregateRootStore { get; }
    internal GameplayOutcomeRegistry Registry { get; }
    internal GameplayOutcomeLedger Ledger { get; }
    internal GameplayOutcomeRecorder Recorder { get; }
    internal MigratedProducerOutcomeRuntime Transaction { get; }
}

internal sealed class FixedGameSessionStateProvider : IGameSessionStateProvider
{
    private readonly GameSessionState state;

    internal FixedGameSessionStateProvider(int absoluteDay = 1)
    {
        state = new GameSessionState(
            startingMoney: 0,
            startingDay: Math.Max(0, absoluteDay));
    }

    public bool TryGetSessionState(out GameSessionState gameData)
    {
        gameData = state;
        return true;
    }
}
#endif
