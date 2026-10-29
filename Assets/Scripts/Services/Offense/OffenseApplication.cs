using System;
using System.Collections.Generic;
using System.Linq;

public sealed class OffenseCampaignSnapshot
{
    public static OffenseCampaignSnapshot Unavailable => new OffenseCampaignSnapshot();

    public bool IsAvailable { get; set; }
    public int ReconLevel { get; set; }
    public float ScanRange { get; set; }
    public string SelectedTargetId { get; set; } = string.Empty;
    public int CompletedTargetCount { get; set; }
    public int CampaignTargetCount { get; set; }
    public bool TruthRevealed { get; set; }
    public bool CanLaunchExpedition { get; set; }
    public string ExpeditionBlocker { get; set; } = string.Empty;
    public int AvailableMemberCount { get; set; }
    public int MoneyEarned { get; set; }
    public int RecoveredLootValue { get; set; }
    public IReadOnlyList<OffenseTargetSnapshot> VisibleTargets { get; set; }
        = Array.Empty<OffenseTargetSnapshot>();
    public IReadOnlyList<OffenseExpeditionSnapshot> ActiveExpeditions { get; set; }
        = Array.Empty<OffenseExpeditionSnapshot>();
    public IReadOnlyList<OffenseExpeditionResult> ResultHistory { get; set; }
        = Array.Empty<OffenseExpeditionResult>();
}

public sealed class OffenseExpeditionSnapshot
{
    internal OffenseExpeditionSnapshot(OffenseExpeditionRun expedition)
    {
        if (expedition == null)
        {
            throw new ArgumentNullException(nameof(expedition));
        }

        ExpeditionId = expedition.ExpeditionId;
        TargetId = expedition.Target?.id ?? string.Empty;
        TargetTitle = expedition.Target?.title ?? string.Empty;
        Phase = expedition.Phase;
        CurrentNodeId = expedition.CurrentNodeId;
        RemainingSeconds = expedition.RemainingSeconds;
        Light = expedition.Light;
        MemberCount = expedition.MemberActors.Count;
        FieldFunds = expedition.FieldFunds;
        UsesWorldTravel = expedition.UsesWorldTravel;
    }

    public string ExpeditionId { get; }
    public string TargetId { get; }
    public string TargetTitle { get; }
    public OffenseExpeditionPhase Phase { get; }
    public string CurrentNodeId { get; }
    public float RemainingSeconds { get; }
    public float Light { get; }
    public int MemberCount { get; }
    public int FieldFunds { get; }
    public bool UsesWorldTravel { get; }
}

public interface IOffenseQuery
{
    OffenseCampaignSnapshot Capture();
    bool TryGetTargetDefinition(string targetId, out OffenseTargetDefinition definition);
}

public interface IOffenseApplication
{
    bool TryOpenWorldMap(out string message);
    bool TryOpenExpedition(out string message);
    bool TryUpgradeRecon(out string message);
    bool TrySelectTarget(string targetId, out string message);
    bool TryStartSelectedTarget(out string message);
}

/// <summary>
/// The sole cross-domain query and command boundary for the expedition aggregate.
/// Scene MonoBehaviours remain internal adapters and are never returned to callers.
/// </summary>
public sealed class OffenseApplication : IOffenseQuery, IOffenseApplication
{
    private readonly IOffenseCampaignQuery campaign;
    private readonly IOffenseCampaignCommands campaignCommands;
    private readonly IOffensePanelService panelService;
    private readonly OffenseExpeditionRuntime expedition;
    private readonly OffenseRewardRuntime reward;
    private readonly IBlueprintResearchStateService researchStateService;

    public OffenseApplication(
        OffenseSceneRuntimeReferences runtimeReferences,
        IBlueprintResearchStateService researchStateService,
        IOffenseCampaignQuery campaign,
        IOffenseCampaignCommands campaignCommands,
        IOffensePanelService panelService)
    {
        runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
        this.campaign = campaign
            ?? throw new ArgumentNullException(nameof(campaign));
        this.campaignCommands = campaignCommands
            ?? throw new ArgumentNullException(nameof(campaignCommands));
        this.panelService = panelService
            ?? throw new ArgumentNullException(nameof(panelService));
        expedition = runtimeReferences.Expedition
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseApplication)} requires a loaded {nameof(OffenseExpeditionRuntime)}.");
        reward = runtimeReferences.Rewards
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseApplication)} requires a loaded {nameof(OffenseRewardRuntime)}.");
        this.researchStateService = researchStateService
            ?? throw new ArgumentNullException(nameof(researchStateService));
    }

    public OffenseCampaignSnapshot Capture()
    {
        if (campaign == null || expedition == null)
        {
            return OffenseCampaignSnapshot.Unavailable;
        }

        BlueprintResearchState researchState = researchStateService.GetState()
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseApplication)} requires a current {nameof(BlueprintResearchState)}.");
        bool canLaunchExpedition = OffenseExpeditionAccessRules.IsUnlocked(researchState);
        return new OffenseCampaignSnapshot
        {
            IsAvailable = true,
            ReconLevel = campaign.State.ReconLevel,
            ScanRange = campaign.CurrentScanRange,
            SelectedTargetId = campaign.State.SelectedTargetId,
            CompletedTargetCount = campaign.State.CompletedTargetCount,
            CampaignTargetCount = campaign.CampaignTargetCount,
            TruthRevealed = campaign.State.TruthRevealed,
            CanLaunchExpedition = canLaunchExpedition,
            ExpeditionBlocker = canLaunchExpedition
                ? string.Empty
                : OffenseExpeditionAccessRules.BlockerMessage,
            AvailableMemberCount = expedition.GetAvailableMemberActors().Count,
            MoneyEarned = reward?.State.MoneyEarned ?? 0,
            RecoveredLootValue = reward?.State.RecoveredLootValue ?? 0,
            VisibleTargets = campaign.VisibleTargets.ToArray(),
            ActiveExpeditions = ProjectActiveExpeditions(expedition.ActiveExpeditions),
            ResultHistory = expedition.ResultHistory.ToArray()
        };
    }

    internal static IReadOnlyList<OffenseExpeditionSnapshot> ProjectActiveExpeditions(
        IReadOnlyList<OffenseExpeditionRun> expeditions)
    {
        if (expeditions == null)
        {
            throw new ArgumentNullException(nameof(expeditions));
        }

        return Array.AsReadOnly(expeditions
            .Select(value => new OffenseExpeditionSnapshot(value))
            .ToArray());
    }

    public bool TryGetTargetDefinition(
        string targetId,
        out OffenseTargetDefinition definition)
    {
        definition = null;
        return campaign.TryGetTargetDefinition(targetId, out definition);
    }

    public bool TryOpenWorldMap(out string message)
    {
        if (panelService == null)
        {
            message = "OffenseUnavailable";
            return false;
        }

        bool opened = panelService.ShowWorldMap() != null;
        message = opened ? "OffenseWorldMapOpened" : "OffenseWorldMapOpenFailed";
        return opened;
    }

    public bool TryOpenExpedition(out string message)
    {
        if (expedition == null)
        {
            message = "OffenseUnavailable";
            return false;
        }

        BlueprintResearchState researchState = researchStateService.GetState()
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseApplication)} requires a current {nameof(BlueprintResearchState)}.");
        if (!OffenseExpeditionAccessRules.IsUnlocked(researchState))
        {
            message = OffenseExpeditionAccessRules.BlockerMessage;
            return false;
        }

        bool opened = expedition.ShowExpeditionPanel() != null;
        message = opened ? "OffensePreparationOpened" : "OffensePreparationOpenFailed";
        return opened;
    }

    public bool TryUpgradeRecon(out string message)
    {
        if (campaignCommands == null)
        {
            message = "OffenseUnavailable";
            return false;
        }

        return campaignCommands.TryUpgradeRecon(out message);
    }

    public bool TrySelectTarget(string targetId, out string message)
    {
        if (campaignCommands == null)
        {
            message = "OffenseUnavailable";
            return false;
        }

        return campaignCommands.TrySelectTarget(targetId, out _, out message);
    }

    public bool TryStartSelectedTarget(out string message)
    {
        if (campaign == null || expedition == null)
        {
            message = "OffenseUnavailable";
            return false;
        }

        BlueprintResearchState researchState = researchStateService.GetState()
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseApplication)} requires a current {nameof(BlueprintResearchState)}.");
        if (!OffenseExpeditionAccessRules.IsUnlocked(researchState))
        {
            message = OffenseExpeditionAccessRules.BlockerMessage;
            return false;
        }

        OffenseTargetSnapshot selected = campaign.VisibleTargets.FirstOrDefault(target =>
            string.Equals(target.id, campaign.State.SelectedTargetId, StringComparison.Ordinal));
        if (selected == null || !selected.isAvailable)
        {
            message = "OffenseTargetUnavailable";
            return false;
        }

        CharacterActor[] party = expedition.GetAvailableMemberActors()
            .Take(selected.requiredMembers)
            .ToArray();
        return expedition.TryStartExpedition(
            selected.id,
            party,
            out _,
            out message);
    }
}
