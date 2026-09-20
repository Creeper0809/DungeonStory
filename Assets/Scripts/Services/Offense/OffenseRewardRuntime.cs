using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public sealed class OffenseRewardRestoreCandidate
{
    internal OffenseRewardRestoreCandidate(OffenseRewardState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal OffenseRewardState State { get; }
}

internal sealed class OffenseRewardTransactionSnapshot
{
    internal OffenseRewardState RewardState;
    internal DungeonPhysicalItemSaveData PhysicalItems;
    internal FacilityShopStateSnapshot ShopUnlocks;
    internal BlueprintResearchAggregateState Research;
    internal DungeonOffenseRegionSaveData Regions;
    internal DungeonOffenseReturnArrivalSaveData Arrivals;
    internal OffenseRewardContext Context;
}

public class OffenseRewardRuntime : MonoBehaviour
{
    private OffenseRewardState state = new OffenseRewardState();
    private readonly OffenseRewardDebugContext debugContext = new OffenseRewardDebugContext();
    private IOffenseRewardContextBuilder contextBuilder;
    private IOffenseRewardGrantService grantService;
    private IWorldItemStackRuntime itemStackRuntime;

    public IOffenseRewardStateView State => state;

    [Inject]
    public void Construct(
        IOffenseRewardContextBuilder contextBuilder,
        IOffenseRewardGrantService grantService)
    {
        this.contextBuilder = contextBuilder
            ?? throw new ArgumentNullException(nameof(contextBuilder));
        this.grantService = grantService
            ?? throw new ArgumentNullException(nameof(grantService));
    }

    [Inject]
    public void ConstructTransactionItems(IWorldItemStackRuntime items)
    {
        itemStackRuntime = items
            ?? throw new ArgumentNullException(nameof(items));
    }

    public IReadOnlyList<OffenseRewardGrantResult> ApplyExpeditionRewards(
        OffenseExpeditionRun expedition,
        OffenseExpeditionResult result)
    {
        if (expedition == null || expedition.Target == null || result == null || !result.success)
        {
            return Array.Empty<OffenseRewardGrantResult>();
        }

        OffenseRewardContext context = CreateContext(
            expedition.Target,
            expedition.ExpeditionId);
        try
        {
            return ResolveGrantService().GrantRewards(
                expedition.Target.rewards,
                context);
        }
        finally
        {
            context.returnArrivalRuntime?
                .DiscardBattlePrisonerCandidates(expedition.ExpeditionId);
        }
    }

    public void SetDebugContext(
        GameSessionState gameData,
        IEnumerable<IWarehouseFacility> warehouses,
        FacilityShopUnlockState shopUnlockState,
        BlueprintResearchState researchState)
    {
        debugContext.gameData = gameData;
        debugContext.warehouses = warehouses?.Where((warehouse) => warehouse != null).ToList();
        debugContext.shopUnlockState = shopUnlockState;
        debugContext.researchState = researchState;
    }

    public void ClearDebugContext()
    {
        debugContext.Clear();
    }

    public void ResetState()
    {
        state.Reset();
    }

    internal static OffenseRewardState PreparePersistentState(
        int moneyEarned,
        IReadOnlyDictionary<StockCategory, int> restoredStock,
        IEnumerable<int> restoredRareFacilityIds,
        IEnumerable<int> restoredBlueprintIds)
    {
        if (moneyEarned < 0
            || restoredStock == null
            || restoredRareFacilityIds == null
            || restoredBlueprintIds == null
            || restoredStock.Any(pair => !Enum.IsDefined(
                    typeof(StockCategory),
                    pair.Key)
                || pair.Value <= 0)
            || restoredRareFacilityIds.Any(id => id <= 0)
            || restoredBlueprintIds.Any(id => id <= 0))
        {
            throw new InvalidOperationException(
                "Offense reward history is invalid or non-canonical.");
        }

        OffenseRewardState candidate = new OffenseRewardState();
        candidate.Restore(
            moneyEarned,
            restoredStock,
            restoredRareFacilityIds,
            restoredBlueprintIds);
        return candidate;
    }

    public static OffenseRewardRestoreCandidate BuildRestoreCandidate(
        int moneyEarned,
        IReadOnlyDictionary<StockCategory, int> restoredStock,
        IEnumerable<int> restoredRareFacilityIds,
        IEnumerable<int> restoredBlueprintIds) =>
        new OffenseRewardRestoreCandidate(PreparePersistentState(
            moneyEarned,
            restoredStock,
            restoredRareFacilityIds,
            restoredBlueprintIds));

    internal void PublishPersistentState(OffenseRewardState candidate)
    {
        state = candidate ?? throw new ArgumentNullException(nameof(candidate));
    }

    public void PublishRestoreCandidate(
        OffenseRewardRestoreCandidate candidate) =>
        PublishPersistentState(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);

    internal OffenseRewardTransactionSnapshot CaptureTransaction(
        OffenseExpeditionRun expedition)
    {
        if (expedition?.Target == null)
            throw new ArgumentNullException(nameof(expedition));
        OffenseRewardContext context = CreateContext(
            expedition.Target,
            expedition.ExpeditionId);
        return new OffenseRewardTransactionSnapshot
        {
            RewardState = PreparePersistentState(
                state.MoneyEarned,
                state.StockGrantedByCategory,
                state.RareFacilityBuildingIds,
                state.AcquiredBlueprintIds),
            PhysicalItems = itemStackRuntime?.Capture(),
            ShopUnlocks = context.shopUnlockState?.Capture(),
            Research = context.researchState?.CaptureAggregateClone(),
            Regions = context.regionRuntime?.Capture(),
            Arrivals = context.returnArrivalRuntime?.Capture(),
            Context = context
        };
    }

    internal void RestoreTransaction(
        OffenseRewardTransactionSnapshot snapshot)
    {
        snapshot = snapshot
            ?? throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.PhysicalItems != null)
        {
            if (itemStackRuntime == null)
                throw new InvalidOperationException(
                    "Offense reward physical-item rollback is unavailable.");
            itemStackRuntime.Restore(snapshot.PhysicalItems);
        }
        if (snapshot.ShopUnlocks != null)
            snapshot.Context.shopUnlockState?.Restore(snapshot.ShopUnlocks);
        if (snapshot.Research != null)
            snapshot.Context.researchState?.ReplaceAggregate(
                snapshot.Research.DeepClone());
        if (snapshot.Regions != null
            && snapshot.Context.regionRuntime != null)
        {
            snapshot.Context.regionRuntime.PublishRestoreCandidate(
                snapshot.Context.regionRuntime.BuildRestoreCandidate(
                    snapshot.Regions));
        }
        if (snapshot.Arrivals != null
            && snapshot.Context.returnArrivalRuntime != null)
        {
            DungeonGameRestoreReport report = new();
            OffenseReturnArrivalRestoreCandidate candidate =
                snapshot.Context.returnArrivalRuntime.BuildRestoreCandidate(
                    snapshot.Arrivals,
                    report);
            if (!report.Success || candidate == null)
                throw new InvalidOperationException(
                    "Offense reward arrival rollback snapshot is invalid.");
            snapshot.Context.returnArrivalRuntime.PublishRestoreCandidate(
                candidate);
        }
        PublishPersistentState(snapshot.RewardState);
    }

    private OffenseRewardContext CreateContext(
        OffenseTargetDefinition target,
        string expeditionId)
    {
        return ResolveContextBuilder().Create(
            target,
            state,
            debugContext,
            expeditionId);
    }

    private IOffenseRewardContextBuilder ResolveContextBuilder()
    {
        return contextBuilder
            ?? throw new InvalidOperationException($"{nameof(OffenseRewardRuntime)} requires {nameof(IOffenseRewardContextBuilder)} injection.");
    }

    private IOffenseRewardGrantService ResolveGrantService()
    {
        return grantService
            ?? throw new InvalidOperationException($"{nameof(OffenseRewardRuntime)} requires {nameof(IOffenseRewardGrantService)} injection.");
    }
}
