using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class ResourceStockPolicyLogisticsDependencies
{
    public ResourceStockPolicyLogisticsDependencies(
        IWorldItemStackRuntime itemRuntime,
        IWorldDropZoneQuery dropZones,
        IWorkforceReplanService workforce)
    {
        ItemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        DropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
        Workforce = workforce
            ?? throw new ArgumentNullException(nameof(workforce));
    }

    internal IWorldItemStackRuntime ItemRuntime { get; }
    internal IWorldDropZoneQuery DropZones { get; }
    internal IWorkforceReplanService Workforce { get; }
}

public sealed class ResourceStockPolicyProductionDependencies
{
    public ResourceStockPolicyProductionDependencies(
        IProductionBillQuery productionBillQuery,
        IProductionBillOrderCommand productionBillCommands,
        IBuildingWorldQuery buildingWorld)
    {
        ProductionBillQuery = productionBillQuery
            ?? throw new ArgumentNullException(nameof(productionBillQuery));
        ProductionBillCommands = productionBillCommands
            ?? throw new ArgumentNullException(nameof(productionBillCommands));
        BuildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
    }

    internal IProductionBillQuery ProductionBillQuery { get; }
    internal IProductionBillOrderCommand ProductionBillCommands { get; }
    internal IBuildingWorldQuery BuildingWorld { get; }
}

public sealed class ResourceStockPolicyRuntime :
    IResourceStockPolicyRuntime,
    IInitializable,
    ITickable
{
    private const float EvaluationInterval = 2f;
    private const string SellDestinationPrefix = "stock-policy:sell:";

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly ResourceStockPolicyLogisticsDependencies logistics;
    private readonly ResourceStockPolicyProductionDependencies production;
    private readonly IGameMoneyAccount money;
    private readonly IGameClock gameClock;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    private ResourceStockPolicyAggregateState state
    {
        get => aggregateRootStore.GetOrCreate(
            () => new ResourceStockPolicyAggregateState());
        set => aggregateRootStore.Replace(value);
    }

    private Dictionary<string, ResourceStockPolicyData> byItemId => state.ByItemId;
    private IReadOnlyList<ResourceStockPolicyData> policyView => state.PolicyView;

    public ResourceStockPolicyRuntime(
        IResourceEconomyContentCatalog catalog,
        ResourceStockPolicyLogisticsDependencies logistics,
        ResourceStockPolicyProductionDependencies production,
        IGameMoneyAccount money,
        IGameClock gameClock,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.logistics = logistics
            ?? throw new ArgumentNullException(nameof(logistics));
        this.production = production
            ?? throw new ArgumentNullException(nameof(production));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public int Version => state.Version;
    public IReadOnlyList<ResourceStockPolicyData> Policies => policyView;

    public void Initialize()
    {
        foreach (ResourceItemDefinitionSO item in catalog.Items)
        {
            GetOrCreate(item.ItemId);
        }

        RefreshView();
    }

    public void Tick()
    {
        if (gameClock.IsPaused || gameClock.Time < state.NextEvaluationTime)
        {
            return;
        }

        state.NextEvaluationTime = gameClock.Time + EvaluationInterval;
        foreach (ResourceStockPolicyData policy in policyView)
        {
            Evaluate(policy);
        }
    }

    public ResourceStockPolicyData GetOrCreate(string itemId)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Item id is required.", nameof(itemId));
        }

        if (!byItemId.TryGetValue(normalized, out ResourceStockPolicyData policy))
        {
            policy = new ResourceStockPolicyData
            {
                itemId = normalized,
                enabled = false,
                minimumStock = 10,
                targetStock = 20,
                maximumStock = 40,
                surplusDisposition = StockSurplusDisposition.Hold
            };
            byItemId.Add(normalized, policy);
            RefreshView();
        }

        return policy.Clone();
    }

    public bool SetPolicy(
        ResourceStockPolicyData policy,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (policy == null || string.IsNullOrWhiteSpace(policy.itemId))
        {
            failureReason = "재고 정책에 아이템이 지정되지 않았습니다.";
            return false;
        }

        ResourceStockPolicyData copy = policy.Clone();
        copy.Normalize();
        if (!IsKnownPolicyItem(copy.itemId))
        {
            failureReason = "알 수 없는 자원 아이템입니다.";
            return false;
        }

        byItemId[copy.itemId] = copy;
        state.Version++;
        RefreshView();
        return true;
    }

    public int CountOwned(string itemId)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        return logistics.ItemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && string.Equals(stack.ItemId, normalized, StringComparison.Ordinal)
                && !IsOutboundDestination(stack.DestinationId))
            .Sum(stack => stack.Quantity);
    }

    public DungeonResourceStockPolicySaveData Capture()
    {
        return new DungeonResourceStockPolicySaveData
        {
            policies = policyView
                .Select(policy => policy.Clone())
                .ToList()
        };
    }

    public ResourceStockPolicyRestoreCandidate PrepareRestoreCandidate(
        DungeonResourceStockPolicySaveData saveData)
    {
        if (saveData?.policies == null)
        {
            throw new InvalidOperationException(
                "Stock-policy restore payload or policy list is missing.");
        }
        ResourceStockPolicyAggregateState restored = new()
        {
            Version = state.Version + 1,
            NextEvaluationTime = gameClock.Time + EvaluationInterval
        };
        foreach (ResourceStockPolicyData saved in saveData.policies)
        {
            ResourceStockPolicyData copy = saved.Clone();
            restored.ByItemId.Add(copy.itemId, copy);
        }

        RefreshView(restored);
        return new ResourceStockPolicyRestoreCandidate(restored, saveData);
    }

    public void PublishRestoreCandidate(
        ResourceStockPolicyRestoreCandidate candidate)
    {
        state = candidate.State;
    }

    private bool IsKnownPolicyItem(string itemId)
    {
        return catalog.TryGetItem(itemId, out _);
    }

    private void Evaluate(ResourceStockPolicyData policy)
    {
        if (policy == null || !policy.enabled)
        {
            return;
        }

        int owned = CountOwned(policy.itemId);
        int surplus = Mathf.Max(0, owned - policy.maximumStock);
        if (surplus <= 0)
        {
            SetStatus(policy, owned < policy.minimumStock
                ? $"부족 {owned}/{policy.minimumStock}"
                : $"목표 범위 {owned}/{policy.targetStock}");
            return;
        }

        switch (policy.surplusDisposition)
        {
            case StockSurplusDisposition.Sell:
                EvaluateSale(policy, surplus);
                break;
            case StockSurplusDisposition.Process:
            case StockSurplusDisposition.Compost:
            case StockSurplusDisposition.Dismantle:
                EvaluateProduction(policy, surplus);
                break;
            default:
                SetStatus(policy, $"초과 재고 {surplus}개 보관 중");
                break;
        }
    }

    private void EvaluateSale(ResourceStockPolicyData policy, int surplus)
    {
        ResourceItemDefinitionSO resourceItem = null;
        if (catalog.TryGetItem(policy.itemId, out resourceItem)
            && !resourceItem.CanSellToMarket)
        {
            SetStatus(policy, "판매 전에 감정 또는 가공이 필요합니다.");
            return;
        }

        string destinationId = SellDestinationPrefix + policy.itemId;
        int delivered = CountAtDestination(
            policy.itemId,
            destinationId,
            WorldItemStackState.FacilityBuffer);
        if (delivered > 0)
        {
            if (logistics.ItemRuntime.TryConsumeFacilityItemBuffer(
                    destinationId,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [policy.itemId] = delivered
                    },
                    out string consumeReason))
            {
                int unitPrice = resourceItem != null
                    ? resourceItem.UnitPrice
                    : 1;
                float saleRate = resourceItem != null
                    ? resourceItem.MarketSaleRate
                    : 0.6f;
                int proceeds = Mathf.Max(1, Mathf.RoundToInt(
                    delivered * unitPrice * saleRate));
                AddMoney(proceeds);
                SetStatus(policy, $"초과 재고 {delivered}개 판매 · {proceeds} 골드");
                state.Version++;
                return;
            }

            SetStatus(policy, consumeReason);
            return;
        }

        int pending = CountAtDestination(policy.itemId, destinationId, null);
        int missing = Mathf.Max(0, surplus - pending);
        if (missing <= 0)
        {
            SetStatus(policy, $"판매 집결 중 {pending}/{surplus}");
            return;
        }

        if (!logistics.DropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            SetStatus(policy, "판매 집결점이 없습니다.");
            return;
        }

        logistics.ItemRuntime.TryRequestItemDelivery(
            policy.itemId,
            missing,
            dropoff,
            destinationId,
            out int requested,
            out string failureReason);
        if (requested > 0)
        {
            PrioritizeDestination(destinationId);
            logistics.Workforce.RequestOneHaulerToReplan(forceInterrupt: false);
            SetStatus(policy, $"판매 물품 운반 요청 {pending + requested}/{surplus}");
        }
        else
        {
            SetStatus(policy, string.IsNullOrWhiteSpace(failureReason)
                ? "판매 가능한 저장 재고가 없습니다."
                : failureReason);
        }
    }

    private void EvaluateProduction(ResourceStockPolicyData policy, int surplus)
    {
        ProductionRecipeSO recipe = FindSurplusRecipe(
            policy.itemId,
            policy.surplusDisposition);
        if (recipe == null)
        {
            SetStatus(
                policy,
                policy.surplusDisposition == StockSurplusDisposition.Dismantle
                    ? "이 아이템을 해체할 조합이 없습니다."
                    : "이 아이템을 처리할 조합이 없습니다.");
            return;
        }

        BuildableObject facility = production.BuildingWorld.Buildings
            .FirstOrDefault(building => building != null
                && !building.IsGridDestroyed
                && building.HasSemanticTag(recipe.FacilityTag)
                && building.SupportsWork(recipe.WorkTypeId));
        if (facility == null)
        {
            SetStatus(policy, $"{recipe.DisplayName} 시설이 필요합니다.");
            return;
        }

        bool alreadyQueued = production.ProductionBillQuery.GetBills(facility)
            .Any(bill => string.Equals(
                bill.RecipeId,
                recipe.RecipeId,
                StringComparison.Ordinal));
        if (alreadyQueued)
        {
            SetStatus(policy, $"{recipe.DisplayName} 처리 중");
            return;
        }

        int inputPerCycle = recipe.Inputs
            .Where(input => input != null
                && string.Equals(
                    input.ItemId,
                    policy.itemId,
                    StringComparison.Ordinal))
            .Sum(input => input.Amount);
        int cycles = Mathf.Max(
            1,
            Mathf.Min(10, surplus / Mathf.Max(1, inputPerCycle)));
        ProductionBillCommandResult result = production.ProductionBillCommands.AddBill(
            facility,
            recipe.RecipeId,
            ProductionOrderMode.RepeatCount,
            cycles);
        SetStatus(policy, result.Succeeded
            ? $"{recipe.DisplayName} {cycles}회 등록"
            : result.Failure.Code.ToString());
    }

    private ProductionRecipeSO FindSurplusRecipe(
        string itemId,
        StockSurplusDisposition disposition)
    {
        IEnumerable<ProductionRecipeSO> candidates = catalog.Recipes
            .Where(recipe => recipe != null
                && recipe.Inputs.Any(input => input != null
                    && string.Equals(
                        input.ItemId,
                        itemId,
                        StringComparison.Ordinal))
                && recipe.Outputs.Count > 0);
        candidates = disposition switch
        {
            StockSurplusDisposition.Compost =>
                candidates.Where(recipe => recipe.Outputs.Any(output =>
                    output != null
                    && string.Equals(
                        output.ItemId,
                        "material:compost",
                        StringComparison.Ordinal))),
            StockSurplusDisposition.Dismantle =>
                candidates.Where(recipe =>
                    recipe.RecipeId.IndexOf(
                        "salvage",
                        StringComparison.OrdinalIgnoreCase) >= 0
                    || recipe.RecipeId.IndexOf(
                        "dismantle",
                        StringComparison.OrdinalIgnoreCase) >= 0),
            _ => candidates
        };
        return candidates
            .OrderBy(recipe => recipe.RequiredWork)
            .ThenBy(recipe => recipe.RecipeId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private int CountAtDestination(
        string itemId,
        string destinationId,
        WorldItemStackState? requiredState)
    {
        return logistics.ItemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && (!requiredState.HasValue
                    || stack.State == requiredState.Value))
            .Sum(stack => stack.Quantity);
    }

    private void PrioritizeDestination(string destinationId)
    {
        foreach (WorldItemStackSnapshot stack in logistics.ItemRuntime.GetAllStacks())
        {
            if (stack != null
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            {
                logistics.ItemRuntime.PrioritizeHaul(stack.StackId);
            }
        }
    }

    private void AddMoney(int amount)
    {
        if (amount > 0)
        {
            money.Add(
                amount,
                new EconomyTransactionContext(
                    EconomyTransactionKind.SaleIncome,
                    "stock-policy",
                    description: "초과 재고 판매"));
        }
    }

    private void SetStatus(
        ResourceStockPolicyData policy,
        string status)
    {
        string normalized = status?.Trim() ?? string.Empty;
        if (string.Equals(policy.lastStatus, normalized, StringComparison.Ordinal))
        {
            return;
        }

        policy.lastStatus = normalized;
        state.Version++;
    }

    private void RefreshView()
    {
        RefreshView(state);
    }

    private static void RefreshView(ResourceStockPolicyAggregateState target)
    {
        target.PolicyView = target.ByItemId.Values
            .OrderBy(policy => policy.itemId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsOutboundDestination(string destinationId)
    {
        return !string.IsNullOrWhiteSpace(destinationId)
            && (destinationId.StartsWith(
                    SellDestinationPrefix,
                    StringComparison.Ordinal)
                || destinationId.StartsWith(
                    "regional-contract:",
                    StringComparison.Ordinal)
                || destinationId.StartsWith(
                    "grand-project:",
                    StringComparison.Ordinal));
    }
}
