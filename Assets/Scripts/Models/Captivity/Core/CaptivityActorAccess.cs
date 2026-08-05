using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(
    true,
    sourceAssembly: "Assembly-CSharp",
    sourceClassName: "CaptivityActorAccess")]
internal sealed class CaptivityStateAccess
{
    private readonly CaptivityAggregateStateStore stateStore;
    private readonly Action<CaptiveState> recalculate;

    internal CaptivityStateAccess(
        CaptivityAggregateStateStore stateStore,
        Action<CaptiveState> recalculate)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.recalculate = recalculate ?? throw new ArgumentNullException(nameof(recalculate));
    }

    private List<CaptiveState> StatesList => stateStore.State.Captives;

    public IReadOnlyList<CaptiveState> States => StatesList;

    public void ClearStates() => StatesList.Clear();

    public void AddState(CaptiveState state)
    {
        StatesList.Add(state ?? throw new ArgumentNullException(nameof(state)));
    }

    public CaptiveState FindState(string id)
    {
        string normalized = id?.Trim() ?? string.Empty;
        return StatesList.FirstOrDefault(state =>
            state != null
            && string.Equals(
                state.captiveId,
                normalized,
                StringComparison.Ordinal));
    }

    public void Recalculate(CaptiveState state) => recalculate(state);
}
