using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CircusStateSession
{
    private readonly CircusAggregateStateStore store;
    private readonly CircusRestoreProjection projection;

    public CircusStateSession(
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        Action<CircusShowOrder> releaseOrder,
        Action clearTransientState)
    {
        store = new CircusAggregateStateStore(aggregateRootStore);
        projection = new CircusRestoreProjection(
            aggregateRootStore,
            store,
            releaseOrder,
            clearTransientState);
    }

    public IReadOnlyList<CircusShowOrder> Orders => store.State.Orders;

    public int NextOrderSequence
    {
        get => store.State.NextOrderSequence;
        set => store.State.NextOrderSequence = Math.Max(0, value);
    }

    public void Add(CircusShowOrder order) =>
        store.State.Orders.Add(order ?? throw new ArgumentNullException(nameof(order)));

    public CircusSaveData Capture(IEnumerable<CapturedWildlifeState> capturedWildlife) =>
        CircusStateCodec.Capture(store.State, capturedWildlife);

    public void Stage(CircusRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        store.Replace(candidate.Circus);
    }

    public void EnsureProjection() => projection.EnsureCurrent();
    public void PublishProjection() => projection.PublishCurrent();
    public CircusProjectionPublication BeginProjectionPublication() =>
        projection.BeginPublication();
    public void RollbackProjection(CircusProjectionPublication publication) =>
        projection.RollbackPublication(publication);
    public void CompleteProjection(CircusProjectionPublication publication) =>
        projection.CompletePublication(publication);
}

public sealed class CapturedWildlifeStateSession
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private CapturedWildlifeAggregateState projectedState;

    public CapturedWildlifeStateSession(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    private CapturedWildlifeAggregateState State =>
        aggregateRootStore.GetOrCreate(() => new CapturedWildlifeAggregateState());

    public int Count => State.Captured.Count;
    public IReadOnlyCollection<CapturedWildlifeState> Values => State.Captured.Values;

    public bool TryGet(string id, out CapturedWildlifeState state) =>
        State.Captured.TryGetValue(id?.Trim() ?? string.Empty, out state);

    public void Set(CapturedWildlifeState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }
        State.Captured[state.wildlifeId] = state;
    }

    public bool Remove(string id) =>
        State.Captured.Remove(id?.Trim() ?? string.Empty);

    public bool Remove(string id, out CapturedWildlifeState state) =>
        State.Captured.Remove(id?.Trim() ?? string.Empty, out state);

    public IReadOnlyList<CapturedWildlifeState> Capture() =>
        State.Captured.Values.Select(item => item.Clone()).ToArray();

    public void Stage(CircusRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        aggregateRootStore.Replace(candidate.CapturedWildlife);
    }

    public bool PublishProjection(Action clearTransientState)
    {
        CapturedWildlifeProjectionPublication publication =
            BeginProjectionPublication();
        bool changed = publication.Changed;
        if (changed)
        {
            clearTransientState?.Invoke();
        }
        CompleteProjectionPublication(publication);
        return changed;
    }

    public CapturedWildlifeProjectionPublication BeginProjectionPublication()
    {
        CapturedWildlifeAggregateState current = State;
        CapturedWildlifeProjectionPublication publication =
            new CapturedWildlifeProjectionPublication(
                this,
                projectedState,
                current);
        projectedState = current;
        return publication;
    }

    public void RollbackProjectionPublication(
        CapturedWildlifeProjectionPublication publication)
    {
        projectedState = (publication
                ?? throw new ArgumentNullException(nameof(publication)))
            .Rollback(this, projectedState);
    }

    public void CompleteProjectionPublication(
        CapturedWildlifeProjectionPublication publication)
    {
        (publication ?? throw new ArgumentNullException(nameof(publication)))
            .Complete(this, projectedState);
    }
}

public sealed class CapturedWildlifeProjectionPublication
{
    private readonly CapturedWildlifeStateSession owner;
    private CapturedWildlifeAggregateState previous;
    private readonly CapturedWildlifeAggregateState applied;
    private bool active = true;

    internal CapturedWildlifeProjectionPublication(
        CapturedWildlifeStateSession owner,
        CapturedWildlifeAggregateState previous,
        CapturedWildlifeAggregateState applied)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.previous = previous;
        this.applied = applied ?? throw new ArgumentNullException(nameof(applied));
        Changed = !ReferenceEquals(previous, applied);
    }

    public bool Changed { get; }

    internal CapturedWildlifeAggregateState Rollback(
        CapturedWildlifeStateSession expectedOwner,
        CapturedWildlifeAggregateState current)
    {
        RequireActive(expectedOwner, current);
        CapturedWildlifeAggregateState result = previous;
        previous = null;
        active = false;
        return result;
    }

    internal void Complete(
        CapturedWildlifeStateSession expectedOwner,
        CapturedWildlifeAggregateState current)
    {
        RequireActive(expectedOwner, current);
        previous = null;
        active = false;
    }

    private void RequireActive(
        CapturedWildlifeStateSession expectedOwner,
        CapturedWildlifeAggregateState current)
    {
        if (!active
            || !ReferenceEquals(owner, expectedOwner)
            || !ReferenceEquals(applied, current))
        {
            throw new InvalidOperationException(
                "Captured-wildlife projection publication has the wrong owner, is no longer current, or is already finished.");
        }
    }
}
