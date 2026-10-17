#if UNITY_EDITOR
using System;
using DungeonStory.Foundation.Facts;

internal sealed class StockSupplyFactEditorFixture : IDisposable
{
    private readonly IDisposable subscription;

    internal StockSupplyFactEditorFixture(
        MigratedProducerOutcomeEditorFixture outcomes,
        TreasuryEconomyAggregateStateStore treasuryStore,
        MigratedProducerOutcomeProbeTransaction probe = null)
    {
        outcomes = outcomes
            ?? throw new ArgumentNullException(nameof(outcomes));
        treasuryStore = treasuryStore
            ?? throw new ArgumentNullException(nameof(treasuryStore));
        probe ??= new MigratedProducerOutcomeProbeTransaction(
            outcomes.Transaction);

        Dispatcher = new DomainFactDispatcher();
        try
        {
            subscription = Dispatcher.Subscribe(
                "fixture:stock-supply-completed",
                new MigratedProducerDomainFactProbeObserver<
                    StockSupplyCompletedFact>(
                    StockSupplyCompletedFactMapping.Capture,
                    outcomes.Transaction,
                    new GameplayDomainFactCommitGroup(
                        outcomes.Recorder,
                        outcomes.Ledger),
                    probe));
            Journal = new StockSupplyFactJournal(
                treasuryStore,
                Dispatcher,
                Dispatcher);
        }
        catch
        {
            subscription?.Dispose();
            Dispatcher.Dispose();
            throw;
        }
    }

    internal DomainFactDispatcher Dispatcher { get; }
    internal StockSupplyFactJournal Journal { get; }

    public void Dispose()
    {
        try
        {
            Journal?.AssertIdle();
        }
        finally
        {
            subscription?.Dispose();
            Dispatcher.Dispose();
        }
    }
}
#endif
