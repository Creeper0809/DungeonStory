using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public static class OffenseRewardGrantHandlers
{
    public static IReadOnlyList<IOffenseRewardGrantHandler> CreateDefaults(
        IWorldItemStackRuntime itemStackRuntime = null,
        IWorldDropZoneQuery dropZoneQuery = null)
    {
        return Array.AsReadOnly<IOffenseRewardGrantHandler>(new IOffenseRewardGrantHandler[]
        {
            new OffenseMoneyRewardGrantHandler(itemStackRuntime, dropZoneQuery),
            new OffenseStockRewardGrantHandler(),
            new OffenseRareFacilityRewardGrantHandler(),
            new OffenseBlueprintRewardGrantHandler(),
            new OffenseRegionalPressureRewardGrantHandler(),
            new OffenseRecruitCandidateRewardGrantHandler(),
            new OffensePrisonerRewardGrantHandler(),
            new OffenseSpecialMonsterRewardGrantHandler()
        });
    }
}

public sealed class OffenseRewardGrantService : IOffenseRewardGrantService
{
    private readonly IOffenseRewardSelector selector;
    private readonly IReadOnlyDictionary<string, IOffenseRewardGrantHandler> handlersByType;

    public OffenseRewardGrantService(
        IOffenseRewardSelector selector,
        IEnumerable<IOffenseRewardGrantHandler> handlers)
    {
        this.selector = selector
            ?? throw new ArgumentNullException(nameof(selector));

        Dictionary<string, IOffenseRewardGrantHandler> mapped = new Dictionary<string, IOffenseRewardGrantHandler>(
            StringComparer.Ordinal);
        foreach (IOffenseRewardGrantHandler handler in handlers ?? Enumerable.Empty<IOffenseRewardGrantHandler>())
        {
            if (handler == null || string.IsNullOrWhiteSpace(handler.RewardTypeId))
            {
                throw new InvalidOperationException("Offense reward handlers require a stable reward type id.");
            }

            if (!mapped.TryAdd(handler.RewardTypeId, handler))
            {
                throw new InvalidOperationException(
                    $"Duplicate offense reward handler for '{handler.RewardTypeId}'.");
            }
        }

        if (mapped.Count == 0)
        {
            throw new InvalidOperationException("At least one offense reward handler is required.");
        }

        handlersByType = mapped;
    }

    public IReadOnlyList<OffenseRewardGrantResult> GrantRewards(
        IEnumerable<OffenseRewardPreview> rewards,
        OffenseRewardContext context)
    {
        if (rewards == null)
        {
            return Array.Empty<OffenseRewardGrantResult>();
        }

        OffenseRewardContext safeContext = context ?? new OffenseRewardContext();
        List<OffenseRewardGrantResult> results = new List<OffenseRewardGrantResult>();
        foreach (OffenseRewardPreview reward in rewards.Where((reward) => reward != null))
        {
            results.Add(GrantReward(reward, safeContext));
        }

        return results.AsReadOnly();
    }

    private OffenseRewardGrantResult GrantReward(
        OffenseRewardPreview reward,
        OffenseRewardContext context)
    {
        string rewardTypeId = reward.GrantSpec?.RewardTypeId;
        if (string.IsNullOrWhiteSpace(rewardTypeId))
        {
            return OffenseRewardGrantResultFactory.Fail(reward, "보상 지급 방식이 설정되지 않았습니다");
        }

        return handlersByType.TryGetValue(rewardTypeId, out IOffenseRewardGrantHandler handler)
            ? handler.Grant(reward, context, selector)
            : OffenseRewardGrantResultFactory.Fail(
                reward,
                $"등록되지 않은 보상 지급 방식: {rewardTypeId}");
    }
}

public abstract class OffenseRewardGrantHandler<TSpec> : IOffenseRewardGrantHandler
    where TSpec : OffenseRewardGrantSpec
{
    public abstract string RewardTypeId { get; }

    public OffenseRewardGrantResult Grant(
        OffenseRewardPreview reward,
        OffenseRewardContext context,
        IOffenseRewardSelector selector)
    {
        if (reward?.GrantSpec is not TSpec spec)
        {
            return OffenseRewardGrantResultFactory.Fail(reward, "보상 지급 설정 타입이 일치하지 않습니다");
        }

        return GrantTyped(reward, spec, context ?? new OffenseRewardContext(), selector);
    }

    protected abstract OffenseRewardGrantResult GrantTyped(
        OffenseRewardPreview reward,
        TSpec spec,
        OffenseRewardContext context,
        IOffenseRewardSelector selector);
}

public static class OffenseLootItemIds
{
    public const string UnappraisedLoot = "offense:unappraised-loot";
    public const string AppraisedValuables = "offense:appraised-valuables";
}

public sealed class OffenseMoneyRewardGrantHandler : OffenseRewardGrantHandler<OffenseMoneyRewardSpec>
{
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorldDropZoneQuery dropZoneQuery;

    public OffenseMoneyRewardGrantHandler(
        IWorldItemStackRuntime itemStackRuntime = null,
        IWorldDropZoneQuery dropZoneQuery = null)
    {
        this.itemStackRuntime = itemStackRuntime;
        this.dropZoneQuery = dropZoneQuery;
    }

    public override string RewardTypeId => OffenseRewardTypeIds.Money;

    protected override OffenseRewardGrantResult GrantTyped(
        OffenseRewardPreview reward,
        OffenseMoneyRewardSpec spec,
        OffenseRewardContext context,
        IOffenseRewardSelector selector)
    {
        int amount = Mathf.Max(0, reward.amount);
        if (amount <= 0)
        {
            return OffenseRewardGrantResultFactory.Fail(reward, "보상 금액이 없습니다");
        }

        if (itemStackRuntime == null || dropZoneQuery == null)
        {
            return OffenseRewardGrantResultFactory.Fail(
                reward,
                "전리품 하차 시스템을 사용할 수 없습니다");
        }

        if (!dropZoneQuery.TryGetExpeditionLootDropoff(out Vector2Int dropoff))
        {
            return OffenseRewardGrantResultFactory.Fail(
                reward,
                "전리품을 내릴 하차장이 없습니다");
        }

        if (!itemStackRuntime.SpawnItemAt(
                OffenseLootItemIds.UnappraisedLoot,
                amount,
                dropoff,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            || spawned != amount)
        {
            return OffenseRewardGrantResultFactory.Fail(
                reward,
                "미감정 전리품 하차에 실패했습니다");
        }

        context.rewardState?.RecordMoney(amount);
        return OffenseRewardGrantResultFactory.Success(
            reward,
            amount,
            "미감정 전리품 하차");
    }
}

public sealed class OffenseStockRewardGrantHandler : OffenseRewardGrantHandler<OffenseStockRewardSpec>
{
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IGameEventBus gameEventBus;

    public OffenseStockRewardGrantHandler(
        IWorldItemStackRuntime itemStackRuntime = null,
        IGameEventBus gameEventBus = null)
    {
        this.itemStackRuntime = itemStackRuntime;
        this.gameEventBus = gameEventBus;
    }

    public override string RewardTypeId => OffenseRewardTypeIds.Stock;

    protected override OffenseRewardGrantResult GrantTyped(
        OffenseRewardPreview reward,
        OffenseStockRewardSpec spec,
        OffenseRewardContext context,
        IOffenseRewardSelector selector)
    {
        int amount = Mathf.Max(0, reward.amount);
        if (amount <= 0)
        {
            return OffenseRewardGrantResultFactory.Fail(reward, "보상 수량이 없습니다");
        }

        bool success = StockSupplyService.GrantReward(
            context.warehouses,
            itemStackRuntime,
            spec.StockCategory,
            amount,
            string.IsNullOrWhiteSpace(reward.label) ? "오펜스 보상" : reward.label,
            out StockSupplyResult result,
            PublishSupplyResult);
        if (!success || !result.success)
        {
            return OffenseRewardGrantResultFactory.Fail(
                reward,
                string.IsNullOrWhiteSpace(result.reason) ? "재고 입고 실패" : result.reason);
        }

        context.rewardState?.RecordStock(spec.StockCategory, result.deliveredAmount);
        return OffenseRewardGrantResultFactory.Success(
            reward,
            result.deliveredAmount,
            $"{spec.StockCategory} 입고");
    }

    private void PublishSupplyResult(StockSupplyResult result)
    {
        gameEventBus?.Publish(new StockSupplyEvent(result));
    }
}

public sealed class OffenseRareFacilityRewardGrantHandler :
    OffenseRewardGrantHandler<OffenseRareFacilityRewardSpec>
{
    public override string RewardTypeId => OffenseRewardTypeIds.RareFacility;

    protected override OffenseRewardGrantResult GrantTyped(
        OffenseRewardPreview reward,
        OffenseRareFacilityRewardSpec spec,
        OffenseRewardContext context,
        IOffenseRewardSelector selector)
    {
        int count = Mathf.Max(1, reward.amount);
        List<string> grantedNames = new List<string>();
        HashSet<int> grantedBuildingIds = new HashSet<int>();
        for (int index = 0; index < count; index++)
        {
            BuildingSO building = selector.SelectRareFacility(context, grantedBuildingIds);
            if (building == null)
            {
                break;
            }

            (context.researchState ?? context.researchRuntime?.State)
                ?.UnlockBuilding(building.id);
            if (FacilityShopService.CanEnterBasicPurchase(building))
            {
                context.shopUnlockState?.UnlockBasicPurchase(building);
            }

            context.rewardState?.RecordRareFacility(building);
            grantedBuildingIds.Add(building.id);
            grantedNames.Add(FacilityShopService.GetBuildingName(building));
        }

        return grantedNames.Count > 0
            ? OffenseRewardGrantResultFactory.Success(
                reward,
                grantedNames.Count,
                string.Join(", ", grantedNames))
            : OffenseRewardGrantResultFactory.Fail(reward, "해금 가능한 희귀 시설이 없습니다");
    }
}

public sealed class OffenseBlueprintRewardGrantHandler :
    OffenseRewardGrantHandler<OffenseBlueprintRewardSpec>
{
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorldDropZoneQuery dropZoneQuery;
    private readonly IGameEventBus gameEventBus;

    public OffenseBlueprintRewardGrantHandler(
        IWorldItemStackRuntime itemStackRuntime = null,
        IWorldDropZoneQuery dropZoneQuery = null,
        IGameEventBus gameEventBus = null)
    {
        this.itemStackRuntime = itemStackRuntime;
        this.dropZoneQuery = dropZoneQuery;
        this.gameEventBus = gameEventBus;
    }

    public override string RewardTypeId => OffenseRewardTypeIds.Blueprint;

    protected override OffenseRewardGrantResult GrantTyped(
        OffenseRewardPreview reward,
        OffenseBlueprintRewardSpec spec,
        OffenseRewardContext context,
        IOffenseRewardSelector selector)
    {
        int count = Mathf.Max(1, reward.amount);
        List<string> grantedNames = new List<string>();
        for (int index = 0; index < count; index++)
        {
            FacilityBlueprintSO blueprint = selector.SelectBlueprint(spec, context);
            if (blueprint == null)
            {
                break;
            }

            if (itemStackRuntime != null)
            {
                if (dropZoneQuery == null
                    || !dropZoneQuery.TryGetExpeditionLootDropoff(out Vector2Int dropoff)
                    || !itemStackRuntime.SpawnUniqueItemAt(
                        blueprint.PhysicalItemId,
                        dropoff,
                        WorldItemStackState.Loose,
                        string.Empty,
                        out _))
                {
                    break;
                }
            }

            context.shopUnlockState?.MarkBlueprintAcquired(blueprint);
            context.rewardState?.RecordBlueprint(blueprint);
            grantedNames.Add(
                itemStackRuntime != null
                    ? $"{blueprint.DisplayName} 설계도 하차장 도착"
                    : $"{blueprint.DisplayName} 설계도 획득");
        }

        if (grantedNames.Count > 0)
        {
            gameEventBus?.RaiseAlert(
                "원정 설계도 도착",
                string.Join(", ", grantedNames),
                EventAlertImportance.Low,
                "연구");
            return OffenseRewardGrantResultFactory.Success(
                reward,
                grantedNames.Count,
                string.Join(", ", grantedNames));
        }

        return OffenseRewardGrantResultFactory.Fail(
            reward,
            itemStackRuntime != null
                ? "설계도를 하차장에 배치할 수 없습니다"
                : "획득 가능한 설계도가 없습니다");
    }
}

public sealed class OffenseRegionalPressureRewardGrantHandler :
    OffenseRewardGrantHandler<OffenseRegionalPressureRewardSpec>
{
    public override string RewardTypeId => OffenseRewardTypeIds.RegionalPressure;

    protected override OffenseRewardGrantResult GrantTyped(
        OffenseRewardPreview reward,
        OffenseRegionalPressureRewardSpec spec,
        OffenseRewardContext context,
        IOffenseRewardSelector selector)
    {
        int amount = OffenseRegionalPressureGrantUtility.Apply(reward, context);
        if (amount <= 0)
        {
            return OffenseRewardGrantResultFactory.Fail(
                reward,
                "이 목표에는 적용할 지역 압력이 없습니다.");
        }
        return OffenseRewardGrantResultFactory.Success(
            reward,
            amount,
            "지역 전략 압력");
    }
}

internal static class OffenseRegionalPressureGrantUtility
{
    public static int Apply(
        OffenseRewardPreview reward,
        OffenseRewardContext context)
    {
        if (context?.regionRuntime == null
            || !context.regionRuntime.TryApplyTargetPressure(
                context.target,
                Mathf.Max(1, reward?.amount ?? 1),
                out _,
                out float applied))
        {
            return 0;
        }

        return Mathf.RoundToInt(applied);
    }
}

public sealed class OffenseRecruitCandidateRewardGrantHandler :
    OffenseRewardGrantHandler<OffenseRecruitCandidateRewardSpec>
{
    public override string RewardTypeId => OffenseRewardTypeIds.RecruitCandidate;

    protected override OffenseRewardGrantResult GrantTyped(
        OffenseRewardPreview reward,
        OffenseRecruitCandidateRewardSpec spec,
        OffenseRewardContext context,
        IOffenseRewardSelector selector)
    {
        int amount = Mathf.Max(2, reward.amount);
        return OffenseRewardGrantResultFactory.Success(
            reward,
            amount,
            "영구 모집 후보 프로필 생성");
    }
}

public sealed class OffensePrisonerRewardGrantHandler :
    OffenseRewardGrantHandler<OffensePrisonerRewardSpec>
{
    public override string RewardTypeId => OffenseRewardTypeIds.Prisoner;

    protected override OffenseRewardGrantResult GrantTyped(
        OffenseRewardPreview reward,
        OffensePrisonerRewardSpec spec,
        OffenseRewardContext context,
        IOffenseRewardSelector selector)
    {
        int amount = Mathf.Max(1, reward.amount);
        int queued = context.returnArrivalRuntime?.QueueArrival(
            context.expeditionId,
            context.target?.id,
            OffenseReturnArrivalKind.Prisoner,
            amount) ?? 0;
        return queued > 0
            ? OffenseRewardGrantResultFactory.Success(
                reward,
                queued,
                "생존 포로가 원정대와 함께 귀환 중")
            : OffenseRewardGrantResultFactory.Fail(
                reward,
                "포로 귀환 대기열을 만들지 못했습니다.");
    }
}

public sealed class OffenseSpecialMonsterRewardGrantHandler :
    OffenseRewardGrantHandler<OffenseSpecialMonsterRewardSpec>
{
    public override string RewardTypeId => OffenseRewardTypeIds.SpecialMonster;

    protected override OffenseRewardGrantResult GrantTyped(
        OffenseRewardPreview reward,
        OffenseSpecialMonsterRewardSpec spec,
        OffenseRewardContext context,
        IOffenseRewardSelector selector)
    {
        int amount = Mathf.Max(1, reward.amount);
        int queued = context.returnArrivalRuntime?.QueueArrival(
            context.expeditionId,
            context.target?.id,
            OffenseReturnArrivalKind.SpecialWildlife,
            amount) ?? 0;
        return queued > 0
            ? OffenseRewardGrantResultFactory.Success(
                reward,
                queued,
                "특수 동물이 운반 상자에 실려 귀환 중")
            : OffenseRewardGrantResultFactory.Fail(
                reward,
                "특수 동물 귀환 대기열을 만들지 못했습니다.");
    }
}

public static class OffenseRewardGrantResultFactory
{
    public static OffenseRewardGrantResult Success(
        OffenseRewardPreview reward,
        int grantedAmount,
        string detail)
    {
        return Create(reward, grantedAmount, true, detail);
    }

    public static OffenseRewardGrantResult Fail(OffenseRewardPreview reward, string detail)
    {
        return Create(reward, 0, false, detail);
    }

    private static OffenseRewardGrantResult Create(
        OffenseRewardPreview reward,
        int grantedAmount,
        bool success,
        string detail)
    {
        return new OffenseRewardGrantResult(
            reward?.category ?? OffenseRewardCategory.Money,
            reward?.label,
            reward?.amount ?? 0,
            grantedAmount,
            success,
            detail);
    }
}

public class OffenseRewardRuntime : MonoBehaviour
{
    private readonly OffenseRewardState state = new OffenseRewardState();
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
        GameData gameData,
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

    public void RestorePersistentState(
        int moneyEarned,
        IReadOnlyDictionary<StockCategory, int> restoredStock,
        IEnumerable<int> restoredRareFacilityIds,
        IEnumerable<int> restoredBlueprintIds)
    {
        state.Restore(
            moneyEarned,
            restoredStock,
            restoredRareFacilityIds,
            restoredBlueprintIds);
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
