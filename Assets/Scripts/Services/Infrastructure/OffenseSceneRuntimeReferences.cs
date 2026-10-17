using System;

public sealed class OffenseSceneRuntimeReferences
{
    public OffenseSceneRuntimeReferences(
        OffenseWorldMapRuntime worldMap,
        OffenseRewardRuntime rewards,
        OffenseExpeditionRuntime expedition,
        OffenseWorldMapPanel worldMapPanel,
        OffenseExpeditionPanel expeditionPanel)
    {
        WorldMap = worldMap;
        Rewards = rewards;
        Expedition = expedition;
        WorldMapPanel = worldMapPanel;
        ExpeditionPanel = expeditionPanel;
    }

    public OffenseWorldMapRuntime WorldMap { get; }
    public OffenseRewardRuntime Rewards { get; }
    public OffenseExpeditionRuntime Expedition { get; }
    public OffenseWorldMapPanel WorldMapPanel { get; private set; }
    public OffenseExpeditionPanel ExpeditionPanel { get; private set; }

    public void RegisterWorldMapPanel(OffenseWorldMapPanel panel)
    {
        WorldMapPanel = panel
            ?? throw new ArgumentNullException(nameof(panel));
    }

    public void RegisterExpeditionPanel(OffenseExpeditionPanel panel)
    {
        ExpeditionPanel = panel
            ?? throw new ArgumentNullException(nameof(panel));
    }
}
