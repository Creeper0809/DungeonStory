using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public interface IOffenseExpeditionResultFinalizer
{
    OffenseExpeditionResult Finalize(
        OffenseExpeditionRun expedition,
        OffenseExpeditionResult result,
        List<OffenseExpeditionResult> resultHistory,
        Action finalizeReturn = null);
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
    private IGameCalendar calendar;
    private IMigratedProducerOutcomeTransaction outcomeTransactions;

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

    [Inject]
    public void ConstructOutcomeTransaction(
        IGameCalendar calendar,
        IMigratedProducerOutcomeTransaction outcomeTransactions)
    {
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.outcomeTransactions = outcomeTransactions
            ?? throw new ArgumentNullException(nameof(outcomeTransactions));
    }

    public OffenseExpeditionResult Finalize(
        OffenseExpeditionRun expedition,
        OffenseExpeditionResult result,
        List<OffenseExpeditionResult> resultHistory,
        Action finalizeReturn = null)
    {
        if (expedition == null || result == null)
        {
            return null;
        }

        if (resultHistory == null)
        {
            throw new ArgumentNullException(nameof(resultHistory));
        }

        if (calendar == null || outcomeTransactions == null)
        {
            throw new InvalidOperationException(
                "Offense expedition-result outcome transaction is unavailable.");
        }
        if (!outcomeTransactions.TryReserveSingleSubject(
                MigratedProducerOutcomeKind.OffenseExpeditionResult,
                "offense-expedition-result:" + expedition.ExpeditionId,
                Math.Max(1, calendar.Day),
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome prepared,
                out string reservationFailure))
        {
            throw new InvalidOperationException(
                "Offense expedition-result outcome reservation failed: "
                + reservationFailure);
        }

        OffenseExpeditionResult[] resultHistoryBefore = resultHistory.ToArray();
        bool advancesCampaign = result.success
            && (!expedition.UsesWorldTravel
                || expedition.Target.revealsTruth);
        bool resolvesWorldSite = expedition.UsesWorldTravel
            && !string.IsNullOrWhiteSpace(
                expedition.Target.seasonalOccurrenceInstanceId)
            && world != null;
        MetaRunProgressTransactionSnapshot metaBefore = null;
        OffenseRewardTransactionSnapshot rewardsBefore = null;
        IOffenseCampaignRuntime campaignPersistence = null;
        DungeonOffenseCampaignSaveData campaignBefore = null;
        OffenseWorldSaveData worldBefore = null;
        try
        {
            rewardsBefore = rewards.CaptureTransaction(expedition);
            if (result.success)
            {
                metaBefore = metaProgression.RunProgress
                    .CaptureTransactionState();
            }
            if (advancesCampaign)
            {
                campaignPersistence = ResolveCampaignPersistence();
                campaignBefore = campaignPersistence.Capture();
            }
            if (resolvesWorldSite)
                worldBefore = world.Capture();
        }
        catch
        {
            outcomeTransactions.Cancel(prepared);
            throw;
        }
        IReadOnlyList<OffenseRewardGrantResult> grantedRewards =
            Array.Empty<OffenseRewardGrantResult>();

        try
        {
            if (result.success)
            {
                metaProgression.RecordOffenseSuccess();
            }

            if (result.success)
            {
            grantedRewards = rewards.ApplyExpeditionRewards(expedition, result);
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
            }

            if (resolvesWorldSite
                && !world.TryResolveSite(expedition.WorldSiteId))
            {
                throw new InvalidOperationException(
                    "Offense expedition world site could not be resolved: "
                    + expedition.WorldSiteId);
            }

            resultHistory.Insert(0, result);
            if (resultHistory.Count > MaxResultHistory)
            {
            resultHistory.RemoveRange(
                MaxResultHistory,
                resultHistory.Count - MaxResultHistory);
            }

            if (advancesCampaign)
            {
                AdvanceCampaign(expedition, result);
            }

            finalizeReturn?.Invoke();

            MigratedProducerOutcomeCommitResult committed =
            outcomeTransactions.CommitSingleSubject(
                prepared,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.ExpeditionKind,
                    result.expeditionId,
                    result.targetTitle,
                    MigratedProducerOutcomeIds.ExpeditionRole),
                $"target={result.targetId}; success={result.success}; power={result.totalPower:0.###}/{result.requiredPower:0.###}; danger={result.danger:0.###}; elapsed={result.elapsedSeconds:0.###}; members={result.members.Count}; rewards={result.grantedRewards.Count}; items={result.itemReceipts.Count}; arrivals={result.arrivalReceipts.Count}; currency={result.currencyReceipts.Count}");
            if (!committed.DurablyCommitted)
            {
            throw new InvalidOperationException(
                "Offense expedition-result outcome commit failed: "
                + committed.DetailCode);
            }
        }
        catch (Exception failure)
        {
            outcomeTransactions.Cancel(prepared);
            List<Exception> rollbackFailures = new();
            AttemptRollback(
                () =>
                {
                    resultHistory.Clear();
                    resultHistory.AddRange(resultHistoryBefore);
                },
                rollbackFailures);
            if (worldBefore != null)
                AttemptRollback(() => world.Restore(worldBefore), rollbackFailures);
            if (campaignBefore != null)
            {
                AttemptRollback(
                    () => campaignPersistence.PublishRestoreCandidate(
                        campaignPersistence.BuildRestoreCandidate(
                            campaignBefore)),
                    rollbackFailures);
            }
            if (rewardsBefore != null)
                AttemptRollback(
                    () => rewards.RestoreTransaction(rewardsBefore),
                    rollbackFailures);
            if (metaBefore != null)
                AttemptRollback(
                    () => metaProgression.RunProgress
                        .RestoreTransactionState(metaBefore),
                    rollbackFailures);
            if (rollbackFailures.Count > 0)
            {
                rollbackFailures.Insert(0, failure);
                throw new AggregateException(
                    "Offense expedition result failed and rollback was incomplete.",
                    rollbackFailures);
            }
            throw;
        }

        if (result.success)
        {
            gameEventBus.Publish(new OffenseRewardGrantedEvent(
                result,
                grantedRewards));
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

    private IOffenseCampaignRuntime ResolveCampaignPersistence()
    {
        if (campaign is OffenseWorldMapRuntime worldMap)
            return worldMap.Campaign;
        if (campaign is IOffenseCampaignRuntime runtime)
            return runtime;
        throw new InvalidOperationException(
            "Offense expedition campaign rollback authority is unavailable.");
    }

    private static void AttemptRollback(
        Action rollback,
        ICollection<Exception> failures)
    {
        try
        {
            rollback?.Invoke();
        }
        catch (Exception exception)
        {
            failures?.Add(exception);
        }
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
