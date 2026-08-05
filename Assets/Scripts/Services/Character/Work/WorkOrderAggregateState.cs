using System;
using System.Collections.Generic;

internal sealed class WorkOrderAggregateState
{
    public Dictionary<string, WorkOrderRecord> OrdersById { get; } =
        new Dictionary<string, WorkOrderRecord>(StringComparer.Ordinal);

    public int NextOrderSequence { get; set; } = 1;
    public int CandidateVersion { get; set; }

    public WorkOrderAggregateState DeepClone()
    {
        WorkOrderAggregateState clone = new WorkOrderAggregateState
        {
            NextOrderSequence = NextOrderSequence,
            CandidateVersion = CandidateVersion
        };
        foreach (KeyValuePair<string, WorkOrderRecord> pair in OrdersById)
        {
            clone.OrdersById.Add(pair.Key, pair.Value.DeepClone());
        }

        return clone;
    }
}

public sealed class WorkOrderAggregateStateStore
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;

    public WorkOrderAggregateStateStore(
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IRestoreWorldCandidateQuery restoreWorldCandidates)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
    }

    internal WorkOrderAggregateState Current =>
        aggregateRootStore.GetOrCreate(() => new WorkOrderAggregateState());

    internal WorkOrderAggregateState Writable =>
        aggregateRootStore.GetOrCreateWritable(
            () => new WorkOrderAggregateState(),
            state => state.DeepClone());

    internal void Replace(WorkOrderAggregateState state)
    {
        aggregateRootStore.Replace(
            state ?? throw new ArgumentNullException(nameof(state)));
    }

    internal bool TryGetRestoreGrid(out Grid grid)
    {
        return restoreWorldCandidates.TryGetGrid(out grid);
    }
}
