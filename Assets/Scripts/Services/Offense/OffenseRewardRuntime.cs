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

public class OffenseRewardRuntime : MonoBehaviour
{
    private OffenseRewardState state = new OffenseRewardState();
    private readonly OffenseRewardDebugContext debugContext = new OffenseRewardDebugContext();
    private IOffenseRewardContextBuilder contextBuilder;
    private IOffenseRewardGrantService grantService;

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
        return ResolveGrantService().GrantRewards(expedition.Target.rewards, context);
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
