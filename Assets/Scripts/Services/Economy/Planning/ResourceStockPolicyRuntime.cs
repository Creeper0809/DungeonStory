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
        IPhysicalFacilityItemBatchTransferGateway transferGateway,
        IWorldDropZoneQuery dropZones,
        IWorkforceReplanService workforce)
    {
        ItemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        TransferGateway = transferGateway
            ?? throw new ArgumentNullException(nameof(transferGateway));
        DropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
        Workforce = workforce
            ?? throw new ArgumentNullException(nameof(workforce));
    }

    internal IWorldItemStackRuntime ItemRuntime { get; }
    internal IPhysicalFacilityItemBatchTransferGateway TransferGateway { get; }
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
    IResourceStockPolicySaleCommandPort,
    IInitializable,
    ITickable
{
    private const float EvaluationInterval = 2f;
    private const string SellDestinationPrefix =
        ResourceStockPolicySaleOutbox.DestinationPrefix;

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly ResourceStockPolicyLogisticsDependencies logistics;
    private readonly ResourceStockPolicyProductionDependencies production;
    private readonly ICombatEquipmentRuntime combatEquipment;
    private readonly IGameMoneyAccount money;
    private readonly IGameClock gameClock;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly HashSet<string> reportedRejectedSaleFailures =
        new(StringComparer.Ordinal);

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
        ICombatEquipmentRuntime combatEquipment,
        IGameMoneyAccount money,
        IGameClock gameClock,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.logistics = logistics
            ?? throw new ArgumentNullException(nameof(logistics));
        this.production = production
            ?? throw new ArgumentNullException(nameof(production));
        this.combatEquipment = combatEquipment
            ?? throw new ArgumentNullException(nameof(combatEquipment));
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
        RecoverPendingSales();
        if (gameClock.IsPaused || gameClock.Time < state.NextEvaluationTime)
        {
            return;
        }

        state.NextEvaluationTime = gameClock.Time + EvaluationInterval;
        EvaluateRejectedQualitySales();
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
            nextSaleSequence = state.NextSaleSequence,
            policies = policyView
                .Select(policy => policy.Clone())
                .ToList(),
            pendingSales = state.PendingSalesByItemId.Values
                .OrderBy(pending => pending.itemId, StringComparer.Ordinal)
                .Select(pending => pending.Clone())
                .ToList()
        };
    }

    public ResourceStockPolicyRestoreCandidate PrepareRestoreCandidate(
        DungeonResourceStockPolicySaveData saveData)
    {
        if (saveData?.policies == null || saveData.pendingSales == null)
        {
            throw new InvalidOperationException(
                "Stock-policy restore payload or policy list is missing.");
        }
        ResourceStockPolicyAggregateState restored = new()
        {
            NextSaleSequence = saveData.nextSaleSequence,
            Version = state.Version + 1,
            NextEvaluationTime = gameClock.Time + EvaluationInterval
        };
        foreach (ResourceStockPolicyData saved in saveData.policies)
        {
            ResourceStockPolicyData copy = saved.Clone();
            restored.ByItemId.Add(copy.itemId, copy);
        }
        foreach (ResourceStockPolicyPendingSale saved in saveData.pendingSales)
        {
            ResourceStockPolicyPendingSale copy = saved.Clone();
            restored.PendingSalesByItemId.Add(copy.itemId, copy);
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
        if (state.PendingSalesByItemId.ContainsKey(policy.itemId))
        {
            SetStatus(policy, "판매 정산 복구 대기");
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

        int unitPrice = resourceItem != null
            ? resourceItem.UnitPrice
            : 1;
        float saleRate = resourceItem != null
            ? resourceItem.MarketSaleRate
            : 0.6f;
        float fractionalUnitProceeds = Mathf.Max(0f, unitPrice * saleRate);
        int minimumSaleBatch = fractionalUnitProceeds > 0f
            ? Mathf.Max(1, Mathf.CeilToInt(1f / fractionalUnitProceeds))
            : int.MaxValue;

        string destinationId = SellDestinationPrefix + policy.itemId;
        int delivered = CountAtDestination(
            policy.itemId,
            destinationId,
            WorldItemStackState.FacilityBuffer);
        if (delivered >= minimumSaleBatch)
        {
            int proceeds = Mathf.FloorToInt(delivered * fractionalUnitProceeds);
            if (proceeds <= 0)
            {
                SetStatus(policy, $"최소 판매 묶음 부족 · {delivered}/{minimumSaleBatch}");
                return;
            }

            int sequence = state.NextSaleSequence;
            if (sequence <= 0 || sequence == int.MaxValue)
            {
                SetStatus(policy, "판매 작업 순번이 소진되었습니다.");
                return;
            }
            string operationId = ResourceStockPolicySaleOutbox.FormatOperationId(
                policy.itemId,
                sequence);
            if (logistics.TransferGateway.TryCommitTransferPending(
                    destinationId,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [policy.itemId] = delivered
                    },
                    operationId,
                    ResourceStockPolicySaleOutbox.TransferReason,
                    out PhysicalItemBatchDispositionReceipt physicalReceipt,
                    out string transferReason))
            {
                ResourceStockPolicyPendingSale pendingSale =
                    ResourceStockPolicySaleOutbox.CreatePending(
                        sequence,
                        policy.itemId,
                        proceeds,
                        ToSaleReceipt(physicalReceipt));
                state.PendingSalesByItemId.Add(policy.itemId, pendingSale);
                state.NextSaleSequence = checked(sequence + 1);
                state.Version++;
                if (!TryFinalizePendingSale(
                        pendingSale,
                        out string finalizeFailure))
                {
                    SetStatus(policy, finalizeFailure);
                }
                return;
            }

            SetStatus(policy, transferReason);
            return;
        }

        if (surplus < minimumSaleBatch)
        {
            SetStatus(policy, $"최소 판매 묶음 대기 · {surplus}/{minimumSaleBatch}");
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

    private void EvaluateRejectedQualitySales()
    {
        WorldItemStackSnapshot[] candidates = logistics.ItemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity == 1
                && string.Equals(
                    stack.DestinationId,
                    QualityRejectedOutputRules.MarketDestinationId,
                    StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
        {
            return;
        }

        int settled = 0;
        foreach (WorldItemStackSnapshot stack in candidates)
        {
            if (stack.State == WorldItemStackState.FacilityOutputBuffer)
            {
                RequestRejectedQualitySaleDelivery(stack);
                continue;
            }
            if (stack.State != WorldItemStackState.FacilityBuffer
                || settled >= QualityRejectedOutputRules.MaximumSettlementsPerEvaluation
                || !TryResolveRejectedQualityTier(
                    stack,
                    out CraftsmanshipQualityTier quality))
            {
                continue;
            }
            if (quality == CraftsmanshipQualityTier.Mythic)
            {
                ReportRejectedSaleFailureOnce(
                    stack,
                    "신화품은 품질 미달 자동 판매 대상에서 제외됩니다.");
                continue;
            }

            float saleRate = ResolveRejectedQualitySaleRate(stack.ItemId);
            int proceeds = CalculateQualityRejectedSaleProceeds(
                stack.UnitPrice,
                saleRate,
                quality);
            if (proceeds <= 0)
            {
                ReportRejectedSaleFailureOnce(
                    stack,
                    "이 완제품은 시장 판매가 금지되어 있습니다.");
                continue;
            }
            if (!TryConsumeRejectedQualityOutput(
                    stack,
                    out string consumeReason))
            {
                ReportRejectedSaleFailureOnce(stack, consumeReason);
                continue;
            }

            reportedRejectedSaleFailures.Remove(stack.StackId);
            money.Add(
                proceeds,
                new EconomyTransactionContext(
                    EconomyTransactionKind.SaleIncome,
                    stack.StackId,
                    targetId: stack.ItemId,
                    description: "품질 미달 완제품 판매"));
            settled++;
            state.Version++;
        }
    }

    private void RequestRejectedQualitySaleDelivery(
        WorldItemStackSnapshot stack)
    {
        if (!logistics.DropZones.TryGetDeliveryDropoff(out Vector2Int dropoff))
        {
            return;
        }
        if (logistics.ItemRuntime.TryRequestStackDelivery(
                stack.StackId,
                1,
                dropoff,
                QualityRejectedOutputRules.MarketDestinationId,
                out int requested,
                out string failureReason)
            && requested == 1)
        {
            reportedRejectedSaleFailures.Remove(stack.StackId);
            logistics.ItemRuntime.PrioritizeHaul(stack.StackId);
            logistics.Workforce.RequestOneHaulerToReplan(forceInterrupt: false);
            return;
        }
        ReportRejectedSaleFailureOnce(stack, failureReason);
    }

    private float ResolveRejectedQualitySaleRate(string itemId)
    {
        if (catalog.TryGetItem(itemId, out ResourceItemDefinitionSO resource))
        {
            return resource.CanSellToMarket
                ? resource.MarketSaleRate
                : 0f;
        }
        return GoldEconomyBalanceRules.TargetExternalSaleRecovery;
    }

    private bool TryConsumeRejectedQualityOutput(
        WorldItemStackSnapshot stack,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (PhysicalItemIds.TryGetEquipmentDefinitionId(stack.ItemId, out _))
        {
            return combatEquipment.TryConsumeForMarketSale(
                stack.StackId,
                out _,
                out failureReason);
        }
        if (!ApparelItemStateCodec.TryRead(stack.Components, out _))
        {
            failureReason = "판매 대상으로 표시된 완제품 상태를 읽을 수 없습니다.";
            return false;
        }
        ItemInstanceId instanceId = (ItemInstanceId)stack.ItemInstanceId;
        if (!instanceId.IsValid
            || !logistics.ItemRuntime.TryAbsorbUniqueItemStack(
                stack.StackId,
                instanceId))
        {
            failureReason = "판매할 의복의 물리 스택을 소비하지 못했습니다.";
            return false;
        }
        return true;
    }

    private bool TryResolveRejectedQualityTier(
        WorldItemStackSnapshot stack,
        out CraftsmanshipQualityTier quality)
    {
        if (combatEquipment.TryGetInstanceBySourceStack(
                stack.StackId,
                out CombatEquipmentInstance equipment)
            && Enum.IsDefined(
                typeof(CraftsmanshipQualityTier),
                (int)equipment.quality))
        {
            quality = (CraftsmanshipQualityTier)(int)equipment.quality;
            return true;
        }
        if (ApparelItemStateCodec.TryRead(
                stack.Components,
                out ApparelInstanceState apparel))
        {
            quality = apparel.craftsmanshipQuality;
            return true;
        }
        quality = CraftsmanshipQualityTier.Normal;
        return false;
    }

    private void ReportRejectedSaleFailureOnce(
        WorldItemStackSnapshot stack,
        string failureReason)
    {
        if (stack == null
            || string.IsNullOrWhiteSpace(stack.StackId)
            || !reportedRejectedSaleFailures.Add(stack.StackId))
        {
            return;
        }
        Debug.LogWarning(
            $"품질 미달 완제품 판매 대기 · {stack.DisplayName} · "
            + (string.IsNullOrWhiteSpace(failureReason)
                ? "원인을 확인할 수 없습니다."
                : failureReason));
    }

    public static int CalculateQualityRejectedSaleProceeds(
        int unitPrice,
        float saleRate,
        CraftsmanshipQualityTier quality)
    {
        if (unitPrice <= 0 || saleRate <= 0f)
        {
            return 0;
        }
        return Mathf.Max(
            1,
            Mathf.FloorToInt(
                unitPrice
                * Mathf.Clamp01(saleRate)
                * CraftsmanshipQualityRules.ProjectionMultiplier(quality)));
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

    private void RecoverPendingSales()
    {
        ResourceStockPolicyPendingSale[] pendingSales =
            state.PendingSalesByItemId.Values
                .OrderBy(pending => pending.itemId, StringComparer.Ordinal)
                .ToArray();
        foreach (ResourceStockPolicyPendingSale pending in pendingSales)
        {
            if (!TryFinalizePendingSale(pending, out string failureReason)
                && byItemId.TryGetValue(
                    pending.itemId,
                    out ResourceStockPolicyData policy))
            {
                SetStatus(policy, failureReason);
            }
        }
    }

    private bool TryFinalizePendingSale(
        ResourceStockPolicyPendingSale pending,
        out string failureReason)
    {
        if (!ResourceStockPolicySaleOutbox.TryFinalizePending(
                pending,
                this,
                out failureReason))
        {
            return false;
        }

        if (!state.PendingSalesByItemId.Remove(pending.itemId))
        {
            failureReason = "stock-policy-sale-owner-missing-after-ack";
            throw new InvalidOperationException(failureReason);
        }
        if (byItemId.TryGetValue(
                pending.itemId,
                out ResourceStockPolicyData policy))
        {
            SetStatus(
                policy,
                $"초과 재고 {pending.quantity}개 판매 · {pending.proceeds} 골드");
        }
        state.Version++;
        failureReason = string.Empty;
        return true;
    }

    public bool TryGetPendingSaleTransfer(
        string operationId,
        out ResourceStockPolicySaleTransferReceipt receipt)
    {
        receipt = null;
        if (!logistics.TransferGateway.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt physicalReceipt))
        {
            return false;
        }
        receipt = ToSaleReceipt(physicalReceipt);
        return true;
    }

    public bool TryPublishSaleIncome(
        int amount,
        string operationId,
        string itemId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (amount <= 0
            || string.IsNullOrWhiteSpace(operationId)
            || !string.Equals(
                operationId,
                operationId.Trim(),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
        {
            failureReason = "stock-policy-sale-income-invalid";
            return false;
        }

        int balanceBefore = money.Balance;
        int expectedBalance;
        try
        {
            expectedBalance = checked(balanceBefore + amount);
        }
        catch (OverflowException)
        {
            failureReason = "stock-policy-sale-income-overflow";
            return false;
        }
        money.Add(
            amount,
            new EconomyTransactionContext(
                EconomyTransactionKind.SaleIncome,
                operationId,
                itemId,
                "초과 재고 판매"));
        if (money.Balance != expectedBalance)
        {
            failureReason = "stock-policy-sale-income-not-published";
            return false;
        }
        return true;
    }

    public bool AcknowledgeSaleTransfer(
        string commitId,
        out string failureReason) =>
        logistics.TransferGateway.Acknowledge(commitId, out failureReason);

    private static ResourceStockPolicySaleTransferReceipt ToSaleReceipt(
        PhysicalItemBatchDispositionReceipt receipt) => new()
    {
        operationId = receipt.OperationId,
        reasonCode = receipt.ReasonCode,
        commitId = receipt.CommitId,
        sourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList(),
        quantity = receipt.Quantity,
        inputMassGrams = receipt.InputMassGrams
    };

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
                    StringComparison.Ordinal)
                || string.Equals(
                    destinationId,
                    QualityRejectedOutputRules.MarketDestinationId,
                    StringComparison.Ordinal));
    }
}
