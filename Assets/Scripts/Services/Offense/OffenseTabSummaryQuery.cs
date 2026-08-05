using System;
using System.Linq;

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
    private readonly IOffenseQuery offense;
    private readonly RegularCustomerRuntime regularCustomers;
    private readonly ICaptivityRuntime captivityRuntime;

    public OffenseTabSummaryService(
        IOffenseQuery offense,
        RegularCustomerRuntime regularCustomers,
        ICaptivityRuntime captivityRuntime)
    {
        this.offense = offense ?? throw new ArgumentNullException(nameof(offense));
        this.regularCustomers = regularCustomers
            ?? throw new ArgumentNullException(nameof(regularCustomers));
        this.captivityRuntime = captivityRuntime
            ?? throw new ArgumentNullException(nameof(captivityRuntime));
    }

    public OffenseTabSummary Capture()
    {
        OffenseCampaignSnapshot campaign = offense.Capture();
        int prisonerCount = captivityRuntime.Captives.Count(captive =>
            captive != null
            && captive.status is not CaptivityStatus.Released
            and not CaptivityStatus.Escaped
            and not CaptivityStatus.Dead
            and not CaptivityStatus.Recruited);
        int recruitCandidateCount = regularCustomers.State.Records.Count(record =>
            record != null && record.IsRecruitCandidate && !record.IsRecruited);

        return new OffenseTabSummary(
            campaign.IsAvailable,
            campaign.ReconLevel,
            campaign.ScanRange,
            campaign.VisibleTargets.Count,
            campaign.SelectedTargetId,
            campaign.ActiveExpeditions.Count,
            campaign.CompletedTargetCount,
            campaign.CampaignTargetCount,
            campaign.TruthRevealed,
            campaign.MoneyEarned,
            prisonerCount,
            recruitCandidateCount);
    }
}
