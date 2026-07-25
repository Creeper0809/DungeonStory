using DungeonStory.Foundation;
using UnityEngine;

public readonly struct MetaRunResultBuildContext
{
    public MetaRunResultBuildContext(
        CharacterActor owner,
        string reason,
        float runStartTime,
        int currentDay,
        int settlementCount,
        int defendedInvasionCount,
        InvasionThreatStage maxThreatStage,
        float finalInvasionThreat,
        int discoveredFacilityCount,
        int unlockedRecipeCount,
        int offenseSuccessCount,
        DungeonRunOutcome outcome = DungeonRunOutcome.Defeat)
    {
        Owner = owner;
        Reason = reason;
        RunStartTime = runStartTime;
        CurrentDay = currentDay;
        SettlementCount = settlementCount;
        DefendedInvasionCount = defendedInvasionCount;
        MaxThreatStage = maxThreatStage;
        FinalInvasionThreat = finalInvasionThreat;
        DiscoveredFacilityCount = discoveredFacilityCount;
        UnlockedRecipeCount = unlockedRecipeCount;
        OffenseSuccessCount = offenseSuccessCount;
        Outcome = outcome == DungeonRunOutcome.None ? DungeonRunOutcome.Defeat : outcome;
    }

    public CharacterActor Owner { get; }
    public string Reason { get; }
    public float RunStartTime { get; }
    public int CurrentDay { get; }
    public int SettlementCount { get; }
    public int DefendedInvasionCount { get; }
    public InvasionThreatStage MaxThreatStage { get; }
    public float FinalInvasionThreat { get; }
    public int DiscoveredFacilityCount { get; }
    public int UnlockedRecipeCount { get; }
    public int OffenseSuccessCount { get; }
    public DungeonRunOutcome Outcome { get; }
}

public interface IMetaRunResultBuilder
{
    RunResultSnapshot Build(MetaRunResultBuildContext context);
}

public sealed class MetaRunResultBuilder : IMetaRunResultBuilder
{
    private readonly IInvasionThreatRuntimeProvider threatRuntimeProvider;
    private readonly IGameClock gameClock;

    public MetaRunResultBuilder(
        IInvasionThreatRuntimeProvider threatRuntimeProvider,
        IGameClock gameClock)
    {
        this.threatRuntimeProvider = threatRuntimeProvider
            ?? throw new System.ArgumentNullException(nameof(threatRuntimeProvider));
        this.gameClock = gameClock
            ?? throw new System.ArgumentNullException(nameof(gameClock));
    }

    public RunResultSnapshot Build(MetaRunResultBuildContext context)
    {
        float difficultyMultiplier = 1f;
        DungeonDifficulty difficulty = DungeonDifficulty.Normal;
        if (threatRuntimeProvider.TryGetRuntime(out InvasionThreatRuntime threatRuntime)
            && threatRuntime.Settings != null)
        {
            difficultyMultiplier = threatRuntime.Settings.GetDifficultyMultiplier();
            difficulty = DungeonDifficultyRules.FromLegacy(threatRuntime.Settings.difficulty);
        }

        CharacterIdentity identity = context.Owner != null ? context.Owner.Identity : null;
        return new RunResultSnapshot(
            ownerName: identity != null
                ? identity.DisplayName
                : context.Owner != null ? context.Owner.name : "사장",
            endReason: string.IsNullOrWhiteSpace(context.Reason)
                ? "사장 쓰러짐"
                : context.Reason,
            survivalSeconds: gameClock.Time - context.RunStartTime,
            survivedOperatingDays: Mathf.Max(1, context.CurrentDay),
            settlementCount: context.SettlementCount,
            defendedInvasionCount: context.DefendedInvasionCount,
            maxThreatStage: context.MaxThreatStage,
            finalInvasionThreat: context.FinalInvasionThreat,
            firstDiscoveredFacilityCount: context.DiscoveredFacilityCount,
            firstUnlockedRecipeCount: context.UnlockedRecipeCount,
            offenseSuccessCount: context.OffenseSuccessCount,
            difficultyMultiplier: difficultyMultiplier,
            outcome: context.Outcome,
            difficulty: difficulty);
    }
}

public interface IRunResultPanelService
{
    RunResultPanel Show(RunResultSnapshot result);
}

public interface IRunResultPanelRegistry
{
    RunResultPanel Current { get; }
    void Register(RunResultPanel panel);
}

public sealed class RunResultPanelRegistry : IRunResultPanelRegistry
{
    public RunResultPanelRegistry(RunResultPanel initialPanel = null)
    {
        Current = initialPanel;
    }

    public RunResultPanel Current { get; private set; }

    public void Register(RunResultPanel panel)
    {
        Current = panel
            ?? throw new System.ArgumentNullException(nameof(panel));
    }
}

public sealed class RunResultPanelService : IRunResultPanelService
{
    private readonly IRunResultPanelRegistry panelRegistry;
    private readonly IRunResultPanelFactory panelFactory;

    public RunResultPanelService(
        IRunResultPanelRegistry panelRegistry,
        IRunResultPanelFactory panelFactory)
    {
        this.panelRegistry = panelRegistry
            ?? throw new System.ArgumentNullException(nameof(panelRegistry));
        this.panelFactory = panelFactory
            ?? throw new System.ArgumentNullException(nameof(panelFactory));
    }

    public RunResultPanel Show(RunResultSnapshot result)
    {
        RunResultPanel panel = panelRegistry.Current
            ?? panelFactory.CreateDefaultPanel();
        panel.Render(result);
        return panel;
    }
}
