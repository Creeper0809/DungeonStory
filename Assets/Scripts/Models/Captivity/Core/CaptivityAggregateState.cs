using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class CaptivityAggregateState
{
    internal readonly List<CaptiveState> Captives = new();
    internal readonly List<CaptivePolicyData> Policies = new();
    internal int CaptureSequence;
    internal int PolicySequence;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class CaptivityAggregateStateStore
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    internal CaptivityAggregateStateStore(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    internal CaptivityAggregateState State =>
        aggregateRootStore.GetOrCreate(() => new CaptivityAggregateState());

    internal void Replace(CaptivityAggregateState state) =>
        aggregateRootStore.Replace(state);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityQueryView
{
    private readonly CaptivityAggregateStateStore stateStore;
    private readonly ICaptivityPolicyRuntime policies;

    public CaptivityQueryView(
        CaptivityActorAccess actors,
        CaptivityPolicyRuntime policies)
    {
        if (actors == null)
        {
            throw new ArgumentNullException(nameof(actors));
        }
        stateStore = actors.StateStore;
        this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
    }

    public IReadOnlyList<CaptiveState> Captives =>
        stateStore.State.Captives.Select(state => state.Clone()).ToArray();

    public IReadOnlyList<CaptivePolicyData> Policies => policies.Policies;

    public int GetCarePriority(string characterId) =>
        Find(characterId)?.carePriorityUnlocked == true ? 100 : 0;

    public bool IsCareSubject(string characterId) =>
        Find(characterId)?.IsInCustody == true;

    private CaptiveState Find(string characterId) =>
        stateStore.State.Captives.FirstOrDefault(candidate =>
            string.Equals(candidate?.captiveId, characterId, StringComparison.Ordinal));
}
