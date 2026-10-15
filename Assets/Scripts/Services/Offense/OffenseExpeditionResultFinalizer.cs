using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
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

public interface IOffenseExpeditionFinalizationQuery
{
    bool TryGetAppliedResult(string expeditionId, out OffenseExpeditionResult result);
}

/// <summary>
/// Commits the cross-aggregate effects of a finished expedition. The
/// expedition runtime owns its active-run state; this service owns reward,
/// campaign and meta-progression side effects.
/// </summary>
public sealed class OffenseExpeditionResultFinalizer :
    IOffenseExpeditionResultFinalizer, IOffenseExpeditionFinalizationQuery
{
    private const int MaxResultHistory = 20;

    private readonly IOffenseCampaignCommands campaign;
    private readonly OffenseRewardRuntime rewards;
    private readonly MetaProgressionRuntime metaProgression;
    private readonly IOffenseReturnArrivalRuntime arrivals;
    private readonly IV27EmbeddedWorkValueProjectionQuery workValues;
    private readonly IOffenseWorldSimulation world;
    private IGameCalendar calendar;
    private OffenseExpeditionResultPublicationRuntime publications;

    public OffenseExpeditionResultFinalizer(
        OffenseSceneRuntimeReferences offenseRuntimes,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IOffenseCampaignCommands campaign,
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
        this.arrivals = arrivals;
        this.workValues = workValues;
        this.world = world;
    }

    [Inject]
    public void ConstructFactPublication(
        IGameCalendar calendar,
        OffenseExpeditionResultPublicationRuntime publications)
    {
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.publications = publications ?? throw new ArgumentNullException(nameof(publications));
    }

    public bool TryGetAppliedResult(string expeditionId, out OffenseExpeditionResult result)
    {
        if (publications == null) throw new InvalidOperationException("Offense result publication owner is unavailable.");
        bool found = publications.TryGetApplied(expeditionId, out var fact);
        result = found ? fact.Result : null;
        return found;
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
        if (result.expeditionId != expedition.ExpeditionId || result.targetId != expedition.Target.id)
            throw new InvalidOperationException("Offense result request does not match its expedition.");

        if (calendar == null || publications == null)
        {
            throw new InvalidOperationException(
                "Offense expedition-result outcome transaction is unavailable.");
        }
        if (publications.TryGetApplied(expedition.ExpeditionId, out var applied))
        {
            if (applied.Result.targetId != result.targetId || applied.Result.success != result.success)
                throw new InvalidOperationException("Offense finalized result identity conflicts with this request.");
            return applied.Result;
        }
        publications.Begin(expedition.ExpeditionId);
        try { return FinalizeNew(expedition, result, resultHistory, finalizeReturn); }
        finally { publications.End(); }
    }

    private OffenseExpeditionResult FinalizeNew(OffenseExpeditionRun expedition, OffenseExpeditionResult result,
        List<OffenseExpeditionResult> resultHistory, Action finalizeReturn)
    {
        OffenseExpeditionResult[] resultHistoryBefore = resultHistory.ToArray();
        bool advancesCampaign = result.success
            && (!expedition.UsesWorldTravel
                || expedition.Target.revealsTruth);
        bool campaignAdvanced = false;
        bool resolvesWorldSite = expedition.UsesWorldTravel
            && !string.IsNullOrWhiteSpace(
                expedition.Target.seasonalOccurrenceInstanceId)
            && world != null;
        MetaRunProgressTransactionSnapshot metaBefore = null;
        OffenseRewardTransactionSnapshot rewardsBefore = null;
        IOffenseCampaignRuntime campaignPersistence = null;
        DungeonOffenseCampaignSaveData campaignBefore = null;
        IOffenseTruthPublicationTransaction truthTransaction = null;
        bool truthDeferred = false;
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
                if (expedition.Target.revealsTruth)
                    truthTransaction = campaignPersistence as IOffenseTruthPublicationTransaction
                        ?? throw new InvalidOperationException("Offense truth publication transaction is unavailable.");
            }
            if (resolvesWorldSite)
                worldBefore = world.Capture();
        }
        catch
        {
            throw;
        }
        IReadOnlyList<OffenseRewardGrantResult> grantedRewards =
            Array.Empty<OffenseRewardGrantResult>();

        try
        {
            if (truthTransaction != null)
            {
                truthTransaction.BeginTruthDeferral();
                truthDeferred = true;
            }
            if (result.success)
            {
                metaProgression.RecordOffenseSuccess();
            }

            if (result.success)
            {
            grantedRewards = rewards.ApplyExpeditionRewards(expedition, result);
            result = result.WithGrantedRewards(grantedRewards);
            OffenseExpeditionItemReceipt[] physicalRewards = result.grantedRewards
                .Where(value => value != null)
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
                campaignAdvanced = AdvanceCampaign(expedition, result);
            }

            finalizeReturn?.Invoke();

            var fact = OffenseExpeditionResolvedFact.Capture(expedition, result, calendar.Day,
                campaignAdvanced, resolvesWorldSite);
            DomainFactDelivery delivery = publications.PublishApplied(fact);
            if (!delivery.MayBeCommitted)
                throw new InvalidOperationException("Offense expedition result publication rejected: " + delivery.Detail);
        }
        catch (Exception failure)
        {
            List<Exception> rollbackFailures = new();
            if (truthDeferred)
                AttemptRollback(truthTransaction.CancelTruthDeferral, rollbackFailures);
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

        // The parent may already be durable. Never let child/notification failures
        // escape into the caller's participant experience/recovery rollback.
        if (truthDeferred)
        {
            try
            {
                truthTransaction.CompleteTruthDeferral();
                if (campaign is OffenseWorldMapRuntime worldMap
                    && !worldMap.TryRecoverPendingTruth(out string truthFailure))
                    publications.RecordFollowupFailure(truthFailure);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                && exception is not StackOverflowException && exception is not AccessViolationException)
            { publications.RecordFollowupFailure("offense-result-truth-followup:" + exception.GetType().Name + ":" + exception.Message); }
        }
        // The publication owner delivers presentation on its tick, after ReturnFinalized
        // is installed by the caller. Observer failures cannot revoke this result.
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

    private bool AdvanceCampaign(
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
            if (expedition.Target.revealsTruth)
                throw new InvalidOperationException("Offense truth could not be applied: " + campaignMessage);
            Debug.LogWarning(
                "Successful battle did not advance the offense campaign: "
                + campaignMessage);
        }
        return recorded;
    }
}
