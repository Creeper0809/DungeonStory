using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class CircusAggregateState
{
    internal readonly List<CircusShowOrder> Orders = new();
    internal int NextOrderSequence;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class CircusAggregateStateStore
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    internal CircusAggregateStateStore(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    internal CircusAggregateState State =>
        aggregateRootStore.GetOrCreate(() => new CircusAggregateState());

    internal void Replace(CircusAggregateState state) =>
        aggregateRootStore.Replace(state);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal static class CircusStateCodec
{
    internal static CircusSaveData Capture(
        CircusAggregateState state,
        IEnumerable<CapturedWildlifeState> capturedWildlife)
    {
        return new CircusSaveData
        {
            version = CircusSaveData.CurrentVersion,
            nextOrderSequence = state.NextOrderSequence,
            orders = state.Orders.Select(order => order.Clone()).ToList(),
            capturedWildlife = (capturedWildlife ?? Array.Empty<CapturedWildlifeState>())
                .Select(item => item.Clone())
                .ToList()
        };
    }

}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class CircusRestoreProjection
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly CircusAggregateStateStore stateStore;
    private readonly Action<CircusShowOrder> releaseOrder;
    private readonly Action clearTransientState;
    private CircusAggregateState projectedState;

    internal CircusRestoreProjection(
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        CircusAggregateStateStore stateStore,
        Action<CircusShowOrder> releaseOrder,
        Action clearTransientState)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.releaseOrder = releaseOrder
            ?? throw new ArgumentNullException(nameof(releaseOrder));
        this.clearTransientState = clearTransientState
            ?? throw new ArgumentNullException(nameof(clearTransientState));
    }

    internal void PublishCurrent()
    {
        if (aggregateRootStore.IsRestoreStaging)
        {
            throw new InvalidOperationException(
                "Circus projection cannot publish while restore staging is active.");
        }
        CircusProjectionPublication publication = BeginPublication();
        CompletePublication(publication);
    }

    internal void EnsureCurrent()
    {
        CircusAggregateState current = stateStore.State;
        if (ReferenceEquals(projectedState, current)) return;
        CircusProjectionPublication publication = BeginPublication();
        CompletePublication(publication);
    }

    internal CircusProjectionPublication BeginPublication()
    {
        CircusAggregateState current = stateStore.State;
        CircusProjectionPublication publication =
            new CircusProjectionPublication(
                this,
                projectedState,
                current);
        projectedState = current;
        return publication;
    }

    internal void RollbackPublication(CircusProjectionPublication publication)
    {
        projectedState = (publication
                ?? throw new ArgumentNullException(nameof(publication)))
            .Rollback(this, projectedState);
    }

    internal void CompletePublication(CircusProjectionPublication publication)
    {
        CircusAggregateState previous = (publication
                ?? throw new ArgumentNullException(nameof(publication)))
            .Complete(this, projectedState);
        foreach (CircusShowOrder order in previous?.Orders
                     ?? Enumerable.Empty<CircusShowOrder>())
        {
            try
            {
                releaseOrder(order);
            }
            catch
            {
                // Projection retirement cannot invalidate an aggregate commit.
            }
        }

        try
        {
            clearTransientState();
        }
        catch
        {
            // Transient presentation cleanup is best effort after commit.
        }
    }
}

public sealed class CircusProjectionPublication
{
    private readonly CircusRestoreProjection owner;
    private CircusAggregateState previous;
    private readonly CircusAggregateState applied;
    private bool active = true;

    internal CircusProjectionPublication(
        CircusRestoreProjection owner,
        CircusAggregateState previous,
        CircusAggregateState applied)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.previous = previous;
        this.applied = applied ?? throw new ArgumentNullException(nameof(applied));
    }

    internal CircusAggregateState Rollback(
        CircusRestoreProjection expectedOwner,
        CircusAggregateState current)
    {
        RequireActive(expectedOwner, current);
        CircusAggregateState result = previous;
        previous = null;
        active = false;
        return result;
    }

    internal CircusAggregateState Complete(
        CircusRestoreProjection expectedOwner,
        CircusAggregateState current)
    {
        RequireActive(expectedOwner, current);
        CircusAggregateState result = previous;
        previous = null;
        active = false;
        return result;
    }

    private void RequireActive(
        CircusRestoreProjection expectedOwner,
        CircusAggregateState current)
    {
        if (!active
            || !ReferenceEquals(owner, expectedOwner)
            || !ReferenceEquals(applied, current))
        {
            throw new InvalidOperationException(
                "Circus projection publication has the wrong owner, is no longer current, or is already finished.");
        }
    }
}
