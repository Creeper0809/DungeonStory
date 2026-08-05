using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityActorAccess
{
    private readonly CaptivityAggregateStateStore stateStore;
    private readonly CaptivityStateAccess stateAccess;

    public CaptivityActorAccess(
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        Action<CaptiveState> recalculate)
    {
        stateStore = new CaptivityAggregateStateStore(
            aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore)));
        stateAccess = new CaptivityStateAccess(stateStore, recalculate);
    }

    internal CaptivityAggregateStateStore StateStore => stateStore;

    public IReadOnlyList<CaptiveState> States => stateAccess.States;
    public void ClearStates() => stateAccess.ClearStates();
    public void AddState(CaptiveState state) => stateAccess.AddState(state);
    public CaptiveState FindState(string id) => stateAccess.FindState(id);
    public void Recalculate(CaptiveState state) => stateAccess.Recalculate(state);

    public int CaptureSequence
    {
        get => stateStore.State.CaptureSequence;
        set => stateStore.State.CaptureSequence = Math.Max(0, value);
    }

    public void Replace(CaptivityRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        stateStore.Replace(candidate.State);
    }

    public static string RequireCharacterId(string persistentId) =>
        persistentId?.Trim() ?? string.Empty;
}
