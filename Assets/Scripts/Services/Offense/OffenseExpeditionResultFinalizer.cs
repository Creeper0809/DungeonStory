using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly IGameClock gameClock;
    private readonly IOffenseReturnArrivalRuntime arrivals;
    private readonly IV27EmbeddedWorkValueProjectionQuery workValues;
    private readonly IOffenseWorldSimulation world;

    public OffenseExpeditionResultFinalizer(
        OffenseSceneRuntimeReferences offenseRuntimes,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IGameEventBus gameEventBus,
        IOffenseCampaignCommands campaign,
        CharacterIdentityEventPublisher identityEvents = null,
        IGameClock gameClock = null,
        IOffenseReturnArrivalRuntime arrivals = null,
        IV27EmbeddedWorkValueProjectionQuery workValues = null,
        IOffenseWorldSimulation world = null)
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
        this.identityEvents = identityEvents;
        this.gameClock = gameClock;
        this.arrivals = arrivals;
        this.workValues = workValues;
        this.world = world;
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
            OffenseExpeditionItemReceipt[] physicalRewards = result.grantedRewards
                .Where(value => value?.success == true)
                .SelectMany(value => value.physicalItems)
                .GroupBy(value => value.itemId, StringComparer.Ordinal)
                .Select(group => new OffenseExpeditionItemReceipt(
                    OffenseExpeditionItemReceiptKind.RewardGranted,
                    group.Key,
                    group.Sum(value => value.quantity),
                    string.Empty,
                    0f,
                    OffenseExpeditionRun.CreateValuation(
                        workValues,
                        group.Key,
                        group.Sum(value => value.quantity))))
                .ToArray();
            result = result.WithAdditionalItemReceipts(physicalRewards);
            if (arrivals != null)
            {
                result = result.WithArrivalReceipts(
                    arrivals.GetSettlementReceipts(expedition.ExpeditionId));
            }
            gameEventBus.Publish(new OffenseRewardGrantedEvent(
                result,
                result.grantedRewards));
        }

        if (expedition.UsesWorldTravel
            && !string.IsNullOrWhiteSpace(
                expedition.Target.seasonalOccurrenceInstanceId))
            world?.TryResolveSite(expedition.WorldSiteId);

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
        PublishIdentityOutcome(expedition, result);
        return result;
    }

    private void PublishIdentityOutcome(
        OffenseExpeditionRun expedition,
        OffenseExpeditionResult result)
    {
        if (identityEvents == null)
            return;
        CharacterId[] participants = expedition.MemberActors
            .Where(value => value != null)
            .Select(value => new CharacterId(value.Identity?.PersistentId))
            .Where(value => value.IsValid)
            .Distinct()
            .OrderBy(value => value.Value, StringComparer.Ordinal)
            .ToArray();
        if (participants.Length == 0)
            return;
        int day = gameClock == null
            ? 0
            : Mathf.Max(0, Mathf.FloorToInt(
                gameClock.Time / GameCalendarRules.SecondsPerDay));
        identityEvents.Publish(new ExpeditionOutcomeEvent(
            expedition.ExpeditionId,
            participants,
            result.success ? "success" : "failure",
            day));
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
