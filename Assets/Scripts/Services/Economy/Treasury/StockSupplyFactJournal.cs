using System;
using DungeonStory.Foundation.Facts;
using VContainer.Unity;

// Treasury owns the purchase sequence and one frozen observation. Recovery never buys goods.
public sealed class StockSupplyFactJournal : IStockSupplyFactPublisher, ITickable, IDungeonSaveRestoreCompletedHook
{
    private readonly TreasuryEconomyAggregateStateStore state;
    private readonly IDomainFactPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly System.Diagnostics.Stopwatch retry = System.Diagnostics.Stopwatch.StartNew();
    private long activeSequence;
    public string LastFailure { get; private set; } = string.Empty;
    public StockSupplyFactJournal(TreasuryEconomyAggregateStateStore state,
        IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }
    public bool TryBegin(out long sequence, out string failure)
    {
        sequence = 0;
        if (state.StockSupplyInFlight) { failure = "stock-supply-command-active"; return false; }
        if (!RecoverPending(out failure)) return false;
        var current = StockSupplyFactSaveData.ValidateAndClone(state.Current.WarehouseSupply);
        if (current.nextSequence == long.MaxValue) { failure = "stock-supply-sequence-exhausted"; return false; }
        sequence = activeSequence = current.nextSequence;
        state.BeginStockSupply();
        return true;
    }
    public DomainFactDelivery Publish(StockSupplyCompletedFact fact)
    {
        if (!state.StockSupplyInFlight || activeSequence == 0 || fact == null || fact.Sequence != activeSequence
            || state.Current.WarehouseSupply.nextSequence != activeSequence || state.Current.WarehouseSupply.pending.Count != 0)
            throw new InvalidOperationException("stock-supply-publication-command-conflict");
        DomainFactLease lease = default;
        try
        {
            if (!publisher.TryReserve(fact, out lease, out string failure))
                return DomainFactDelivery.Reject("stock-supply-reservation:" + failure);
            var row = new StockSupplyPendingFactData { publicationKey = lease.PublicationKey, fact = fact.Snapshot() };
            var candidate = new StockSupplyFactSaveData { nextSequence = checked(activeSequence + 1) };
            candidate.pending.Add(row);
            state.Current.WarehouseSupply = StockSupplyFactSaveData.ValidateAndClone(candidate);
            DomainFactDelivery result;
            try { result = publisher.Publish(lease, fact); }
            catch (Exception error) when (Recoverable(error))
            { result = DomainFactDelivery.Uncertain("stock-supply-response-unknown:" + error.Message); }
            if (!result.MayBeCommitted)
                state.Current.WarehouseSupply = new StockSupplyFactSaveData { nextSequence = activeSequence };
            else if (Acknowledged(row, result)) state.Current.WarehouseSupply.pending.Clear();
            LastFailure = state.Current.WarehouseSupply.pending.Count == 0 ? string.Empty : result.Detail;
            return result;
        }
        finally { if (lease.IsValid) publisher.Cancel(lease); }
    }
    public void End()
    {
        if (activeSequence == 0) return;
        activeSequence = 0;
        state.EndStockSupply();
    }
    public bool RecoverPending(out string failure)
    {
        failure = string.Empty;
        if (state.StockSupplyInFlight) { failure = "stock-supply-recovery-busy"; return false; }
        var snapshot = StockSupplyFactSaveData.ValidateAndClone(state.Current.WarehouseSupply);
        if (snapshot.pending.Count == 0) return true;
        state.BeginStockSupply();
        try
        {
            var row = snapshot.pending[0];
            var results = new DomainFactDelivery[1];
            if (recovery.ReplayBatch(new[] { DomainFactReplay.Create(row.publicationKey, new StockSupplyCompletedFact(row.fact)) }, results, out failure)
                && Acknowledged(row, results[0]))
            {
                state.Current.WarehouseSupply.pending.Clear(); LastFailure = string.Empty; return true;
            }
            LastFailure = failure = "stock-supply-recovery-pending:" + failure;
            return false;
        }
        catch (Exception error) when (Recoverable(error))
        { LastFailure = failure = "stock-supply-recovery:" + error.Message; return false; }
        finally { state.EndStockSupply(); }
    }
    private static bool Acknowledged(StockSupplyPendingFactData row, DomainFactDelivery result) =>
        result.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery
        && result.OperationKey == row.publicationKey && DomainFactValueSyntax.IsText(result.PublicationId, 4096, false)
        && !string.IsNullOrWhiteSpace(result.Fingerprint);
    private static bool Recoverable(Exception error) => error is not OutOfMemoryException
        && error is not StackOverflowException && error is not AccessViolationException;
    public void AssertIdle() => state.AssertStockSupplyIdle();
    public StockSupplyFactSaveData Capture() { AssertIdle(); return StockSupplyFactSaveData.ValidateAndClone(state.Current.WarehouseSupply); }
    public void OnRestoreCompleted() => RecoverPending(out _);
    public void Tick()
    {
        if (state.StockSupplyInFlight || state.Current.WarehouseSupply.pending.Count == 0 || retry.ElapsedMilliseconds < 1000) return;
        retry.Restart(); RecoverPending(out _);
    }
}
