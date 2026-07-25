using System;

public readonly struct OffenseTabSummary
{
    public OffenseTabSummary(
        bool hasWorldMap,
        int reconLevel,
        float scanRange,
        int visibleTargets,
        string selectedTargetId,
        int activeExpeditions,
        int completedTargets,
        int totalTargets,
        bool truthRevealed,
        int moneyEarned,
        int prisonerCount,
        int recruitCandidateCount)
    {
        HasWorldMap = hasWorldMap;
        ReconLevel = reconLevel;
        ScanRange = scanRange;
        VisibleTargets = visibleTargets;
        SelectedTargetId = selectedTargetId ?? string.Empty;
        ActiveExpeditions = activeExpeditions;
        CompletedTargets = completedTargets;
        TotalTargets = totalTargets;
        TruthRevealed = truthRevealed;
        MoneyEarned = moneyEarned;
        PrisonerCount = prisonerCount;
        RecruitCandidateCount = recruitCandidateCount;
    }

    public bool HasWorldMap { get; }
    public int ReconLevel { get; }
    public float ScanRange { get; }
    public int VisibleTargets { get; }
    public string SelectedTargetId { get; }
    public int ActiveExpeditions { get; }
    public int CompletedTargets { get; }
    public int TotalTargets { get; }
    public bool TruthRevealed { get; }
    public int MoneyEarned { get; }
    public int PrisonerCount { get; }
    public int RecruitCandidateCount { get; }
    public bool HasSelectedTarget => !string.IsNullOrWhiteSpace(SelectedTargetId);
}

public interface IOffenseTabSummaryService
{
    OffenseTabSummary Capture();
}

public sealed class OffenseTabSummaryService : IOffenseTabSummaryService
{
    private readonly IOffenseWorldMapRuntimeProvider worldMapProvider;
    private readonly IOffenseExpeditionRuntimeProvider expeditionProvider;
    private readonly IOffenseRewardRuntimeProvider rewardProvider;

    public OffenseTabSummaryService(
        IOffenseWorldMapRuntimeProvider worldMapProvider,
        IOffenseExpeditionRuntimeProvider expeditionProvider,
        IOffenseRewardRuntimeProvider rewardProvider)
    {
        this.worldMapProvider = worldMapProvider
            ?? throw new ArgumentNullException(nameof(worldMapProvider));
        this.expeditionProvider = expeditionProvider
            ?? throw new ArgumentNullException(nameof(expeditionProvider));
        this.rewardProvider = rewardProvider
            ?? throw new ArgumentNullException(nameof(rewardProvider));
    }

    public OffenseTabSummary Capture()
    {
        worldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime worldMap);
        expeditionProvider.TryGetRuntime(out OffenseExpeditionRuntime expedition);
        rewardProvider.TryGetRuntime(out OffenseRewardRuntime rewards);

        return new OffenseTabSummary(
            worldMap != null,
            worldMap != null ? worldMap.State.ReconLevel : 0,
            worldMap != null ? worldMap.CurrentScanRange : 0f,
            worldMap != null ? worldMap.VisibleTargets.Count : 0,
            worldMap != null ? worldMap.State.SelectedTargetId : string.Empty,
            expedition != null ? expedition.ActiveExpeditions.Count : 0,
            worldMap != null ? worldMap.State.CompletedTargetCount : 0,
            worldMap != null ? worldMap.CampaignTargetCount : 0,
            worldMap != null && worldMap.State.TruthRevealed,
            rewards != null ? rewards.State.MoneyEarned : 0,
            rewards != null ? rewards.State.PrisonerCount : 0,
            rewards != null ? rewards.State.RecruitCandidateCount : 0);
    }
}
