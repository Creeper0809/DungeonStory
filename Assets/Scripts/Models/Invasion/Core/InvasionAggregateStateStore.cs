using System;

public sealed class InvasionAggregateStateStore
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;

    public InvasionAggregateStateStore(
        DungeonRuntimeAggregateRootStore rootStore)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
    }

    internal InvasionAggregateState State => rootStore.GetOrCreateWritable(
        () => new InvasionAggregateState(),
        state => state.Clone());

    public InvasionThreatAggregateState Threat => State.Threat;
    public InvasionCampaignAggregateState Campaign => State.Campaign;
    public bool IsRestoreStaging => rootStore.IsRestoreStaging;

    internal void Replace(InvasionAggregateState state)
    {
        rootStore.Replace(state);
    }
}
