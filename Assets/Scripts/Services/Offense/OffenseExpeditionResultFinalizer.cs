using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public interface IOffenseExpeditionResultFinalizer
{
    OffenseExpeditionResult Finalize(
        OffenseExpeditionRun expedition,
        OffenseExpeditionResult result,
        List<OffenseExpeditionResult> resultHistory);
}

/// <summary>
/// Commits the cross-aggregate effects of a finished expedition. The
/// expedition runtime owns its active-run state; this service owns reward,
/// campaign and meta-progression side effects.
/// </summary>
public sealed class OffenseExpeditionResultFinalizer :
    IOffenseExpeditionResultFinalizer
{
    private const int MaxResultHistory = 20;

    private readonly IOffenseCampaignCommands campaign;
    private readonly OffenseRewardRuntime rewards;
    private readonly MetaProgressionRuntime metaProgression;
    private readonly IGameEventBus gameEventBus;

    public OffenseExpeditionResultFinalizer(
        OffenseSceneRuntimeReferences offenseRuntimes,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IGameEventBus gameEventBus,
        IOffenseCampaignCommands campaign)
    {
        offenseRuntimes = offenseRuntimes
            ?? throw new ArgumentNullException(nameof(offenseRuntimes));
        this.campaign = campaign
            ?? throw new ArgumentNullException(nameof(campaign));
        rewards = offenseRuntimes.Rewards
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseExpeditionResultFinalizer)} requires a loaded {nameof(OffenseRewardRuntime)}.");
        metaProgression = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .MetaProgression
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseExpeditionResultFinalizer)} requires a loaded {nameof(MetaProgressionRuntime)}.");
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public OffenseExpeditionResult Finalize(
        OffenseExpeditionRun expedition,
        OffenseExpeditionResult result,
        List<OffenseExpeditionResult> resultHistory)
    {
        if (expedition == null || result == null)
        {
            return null;
        }

        if (resultHistory == null)
        {
            throw new ArgumentNullException(nameof(resultHistory));
        }

        if (result.success)
        {
            metaProgression.RecordOffenseSuccess();
        }

        if (result.success)
        {
            IReadOnlyList<OffenseRewardGrantResult> grantedRewards =
                rewards.ApplyExpeditionRewards(expedition, result);
            result = result.WithGrantedRewards(grantedRewards);
            gameEventBus.Publish(new OffenseRewardGrantedEvent(
                result,
                result.grantedRewards));
        }

        resultHistory.Insert(0, result);
        if (resultHistory.Count > MaxResultHistory)
        {
            resultHistory.RemoveRange(
                MaxResultHistory,
                resultHistory.Count - MaxResultHistory);
        }

        if (result.success
            && (!expedition.UsesWorldTravel
                || expedition.Target.revealsTruth))
        {
            AdvanceCampaign(expedition, result);
        }

        gameEventBus.RaiseAlert(
            "expedition-result",
            result.ToDetailText(),
            result.success
                ? EventAlertImportance.Medium
                : EventAlertImportance.High,
            "offense");
        return result;
    }

    private void AdvanceCampaign(
        OffenseExpeditionRun expedition,
        OffenseExpeditionResult result)
    {
        bool recorded;
        string campaignMessage;
        if (expedition.UsesWorldTravel)
        {
            recorded = campaign.TryRecordStrategicTruthReveal(
                result.targetId,
                out campaignMessage);
        }
        else
        {
            recorded = campaign.TryRecordSuccessfulExpedition(
                result.targetId,
                out _,
                out campaignMessage);
        }

        if (!recorded)
        {
            Debug.LogWarning(
                "Successful battle did not advance the offense campaign: "
                + campaignMessage);
        }
    }
}
