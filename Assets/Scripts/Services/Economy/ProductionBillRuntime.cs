using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class ProductionBillRecord
{
    public string billId = string.Empty;
    public string recipeId = string.Empty;
    public int buildingId;
    public Vector2Int position;
    public ProductionOrderMode mode;
    public int remainingCycles;
    public int targetStock;
    public int minimumReserve;
    public bool suspended;
    public bool materialsConsumed;
    public bool processFluidConsumed;
    public float completedWork;
    public string reservedWorkerId = string.Empty;
    public string materialDestinationId = string.Empty;
    public readonly HashSet<string> allowedMaterialIds =
        new HashSet<string>(StringComparer.Ordinal);
    public readonly HashSet<string> allowedWorkerIds =
        new HashSet<string>(StringComparer.Ordinal);
}

public sealed class ProductionBillRuntime : IProductionBillRuntime
{
    public const string DestinationPrefix = "production:";

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionItemGateway items;
    private readonly IBlueprintResearchRuntimeProvider researchProvider;
    private readonly IWorkforceReplanService workforceReplanService;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly IProcessFluidUseRuntime processFluids;
    private readonly IReadOnlyList<IProductionOutputHandler> outputHandlers;
    private readonly IRandomStream random;
    private readonly List<ProductionBillRecord> bills =
        new List<ProductionBillRecord>();
    private int nextBillSequence = 1;

    public ProductionBillRuntime(
        IResourceEconomyContentCatalog catalog,
        IProductionItemGateway items,
        IRandomStreamProvider randomStreamProvider,
        IBlueprintResearchRuntimeProvider researchProvider = null,
        IWorkforceReplanService workforceReplanService = null,
        IGrandProjectBenefitQuery grandProjectBenefits = null,
        IReadOnlyList<IProductionOutputHandler> outputHandlers = null,
        IProcessFluidUseRuntime processFluids = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        random = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("economy:production");
        this.researchProvider = researchProvider;
        this.workforceReplanService = workforceReplanService;
        this.grandProjectBenefits = grandProjectBenefits;
        this.outputHandlers = outputHandlers ?? Array.Empty<IProductionOutputHandler>();
        this.processFluids = processFluids;
    }

    public int Version { get; private set; }

    public IReadOnlyList<ProductionBillSnapshot> GetBills(BuildableObject facility)
    {
        if (facility == null)
        {
            return Array.Empty<ProductionBillSnapshot>();
        }

        return bills
            .Where(record => MatchesFacility(record, facility))
            .Select(record => ToSnapshot(record, facility))
            .ToArray();
    }

    public ProductionBillCommandResult AddBill(
        BuildableObject facility,
        string recipeId,
        ProductionOrderMode mode,
        int amount)
    {
        if (facility == null || facility.IsGridDestroyed)
        {
            return ProductionBillCommandResult.Failure("생산 시설이 없습니다.");
        }

        if (!catalog.TryGetRecipe(recipeId, out ProductionRecipeSO recipe))
        {
            return ProductionBillCommandResult.Failure("알 수 없는 생산 조합입니다.");
        }

        if (!facility.SupportsWork(recipe.WorkTypeId)
            || !facility.HasSemanticTag(recipe.FacilityTag))
        {
            return ProductionBillCommandResult.Failure(
                $"{facility.BuildingData?.objectName ?? "시설"}에서는 이 조합을 만들 수 없습니다.");
        }

        if (!IsResearchUnlocked(recipe, out string researchReason))
        {
            return ProductionBillCommandResult.Failure(researchReason);
        }

        string billId = $"production-bill:{nextBillSequence++}";
        ProductionBillRecord record = new ProductionBillRecord
        {
            billId = billId,
            recipeId = recipe.RecipeId,
            buildingId = facility.id,
            position = facility.centerPos,
            mode = mode,
            remainingCycles = mode == ProductionOrderMode.RepeatCount
                ? Mathf.Max(1, amount)
                : -1,
            targetStock = mode == ProductionOrderMode.MaintainStock
                ? Mathf.Max(1, amount)
                : 0,
            materialDestinationId = DestinationPrefix + billId
        };
        bills.Add(record);
        RequestMissingInputs(record, recipe);
        Touch(recipe.WorkTypeId, requestWorker: false);
        return ProductionBillCommandResult.Success(billId, "생산 주문을 등록했습니다.");
    }

    public ProductionBillCommandResult RemoveBill(
        string billId,
        bool returnMaterials)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failure("생산 주문을 찾을 수 없습니다.");
        }

        if (returnMaterials && !record.materialsConsumed)
        {
            items.ReleaseDestination(
                record.materialDestinationId,
                record.position);
        }
        else
        {
            items.RemoveDestination(record.materialDestinationId);
        }

        bills.Remove(record);
        Touch(default, requestWorker: false);
        return ProductionBillCommandResult.Success(billId, "생산 주문을 취소했습니다.");
    }

    public ProductionBillCommandResult MoveBill(string billId, int targetIndex)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failure("생산 주문을 찾을 수 없습니다.");
        }

        List<ProductionBillRecord> facilityBills = bills
            .Where(candidate => candidate.buildingId == record.buildingId
                && candidate.position == record.position)
            .ToList();
        int currentLocalIndex = facilityBills.IndexOf(record);
        int clampedTarget = Mathf.Clamp(targetIndex, 0, facilityBills.Count - 1);
        if (currentLocalIndex == clampedTarget)
        {
            return ProductionBillCommandResult.Success(billId);
        }

        ProductionBillRecord anchor = facilityBills[clampedTarget];
        bills.Remove(record);
        int anchorIndex = bills.IndexOf(anchor);
        bills.Insert(
            currentLocalIndex < clampedTarget ? anchorIndex + 1 : anchorIndex,
            record);
        Touch(default, requestWorker: false);
        return ProductionBillCommandResult.Success(billId);
    }

    public ProductionBillCommandResult SetSuspended(
        string billId,
        bool suspended)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failure("생산 주문을 찾을 수 없습니다.");
        }

        record.suspended = suspended;
        record.reservedWorkerId = string.Empty;
        Touch(ResolveRecipe(record)?.WorkTypeId ?? default, requestWorker: !suspended);
        return ProductionBillCommandResult.Success(billId);
    }

    public ProductionBillCommandResult SetStockPolicy(
        string billId,
        int minimumReserve,
        int targetStock)
    {
        ProductionBillRecord record = Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failure("생산 주문을 찾을 수 없습니다.");
        }

        record.minimumReserve = Mathf.Max(0, minimumReserve);
        record.targetStock = Mathf.Max(record.minimumReserve, targetStock);
        record.mode = ProductionOrderMode.MaintainStock;
        Touch(ResolveRecipe(record)?.WorkTypeId ?? default, requestWorker: true);
        return ProductionBillCommandResult.Success(billId);
    }

    public bool HasWorkAvailable(
        BuildableObject facility,
        WorkTypeId workTypeId,
        out string reason)
    {
        reason = string.Empty;
        ProductionBillRecord record = FindRunnableBill(
            facility,
            workTypeId,
            requireDeliveredInputs: true,
            out reason);
        return record != null;
    }

    public bool TryBeginWork(
        CharacterActor worker,
        BuildableObject facility,
        WorkTypeId workTypeId,
        out ProductionBillSnapshot bill,
        out string failureReason)
    {
        bill = null;
        ProductionBillRecord record = FindRunnableBill(
            facility,
            workTypeId,
            requireDeliveredInputs: true,
            out failureReason);
        if (record == null)
        {
            return false;
        }

        ProductionRecipeSO recipe = ResolveRecipe(record);
        if (!record.materialsConsumed
            && !items.ConsumeDelivered(
                record.materialDestinationId,
                ToInputMap(recipe),
                out failureReason))
        {
            RequestMissingInputs(record, recipe);
            return false;
        }

        record.materialsConsumed = true;
        if (!record.processFluidConsumed
            && processFluids != null
            && !processFluids.TryConsumeCycle(
                facility,
                workTypeId,
                out failureReason))
        {
            return false;
        }

        record.processFluidConsumed = true;
        record.reservedWorkerId = worker?.Identity?.PersistentId ?? string.Empty;
        Touch(default, requestWorker: false);
        bill = ToSnapshot(record, facility);
        return true;
    }

    public bool ApplyWork(
        CharacterActor worker,
        BuildableObject facility,
        string billId,
        float amount,
        out bool cycleCompleted,
        out string message)
    {
        cycleCompleted = false;
        message = string.Empty;
        ProductionBillRecord record = Find(billId);
        ProductionRecipeSO recipe = ResolveRecipe(record);
        if (record == null
            || recipe == null
            || !MatchesFacility(record, facility)
            || record.suspended
            || !record.materialsConsumed)
        {
            message = "생산 주문을 계속할 수 없습니다.";
            return false;
        }

        string workerId = worker?.Identity?.PersistentId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(record.reservedWorkerId)
            && !string.Equals(record.reservedWorkerId, workerId, StringComparison.Ordinal))
        {
            message = "다른 작업자가 사용 중입니다.";
            return false;
        }

        record.reservedWorkerId = workerId;
        record.completedWork = Mathf.Clamp(
            record.completedWork + Mathf.Max(0f, amount),
            0f,
            recipe.RequiredWork);
        if (record.completedWork + 0.001f < recipe.RequiredWork)
        {
            message =
                $"{recipe.DisplayName} {Mathf.RoundToInt(record.completedWork / recipe.RequiredWork * 100f)}%";
            return true;
        }

        foreach (ProductionOutputDefinition output in recipe.Outputs)
        {
            if (output == null || !random.Chance(output.Probability))
            {
                continue;
            }

            int outputAmount = ResolveOutputAmount(
                output.Amount,
                grandProjectBenefits?.GetProductionOutputMultiplier(
                    recipe.FacilityTag) ?? 1f);
            ProductionOutputContext outputContext = new ProductionOutputContext(
                recipe,
                facility,
                worker,
                output.ItemId,
                outputAmount);
            IProductionOutputHandler handler = outputHandlers.FirstOrDefault(
                candidate => candidate != null
                    && candidate.CanHandle(output.ItemId));
            if (handler != null)
            {
                if (!handler.TryProduce(outputContext, out string outputFailure))
                {
                    message = string.IsNullOrWhiteSpace(outputFailure)
                        ? $"{recipe.DisplayName} 결과물 생성 실패"
                        : outputFailure;
                    record.reservedWorkerId = string.Empty;
                    return false;
                }
            }
            else
            {
                items.SpawnOutput(
                    output.ItemId,
                    outputAmount,
                    facility.centerPos);
            }
        }

        cycleCompleted = true;
        record.completedWork = 0f;
        record.materialsConsumed = false;
        record.processFluidConsumed = false;
        record.reservedWorkerId = string.Empty;
        if (record.mode == ProductionOrderMode.RepeatCount)
        {
            record.remainingCycles = Mathf.Max(0, record.remainingCycles - 1);
        }

        bool finished = !ShouldRunAnotherCycle(record, recipe);
        if (finished && record.mode == ProductionOrderMode.RepeatCount)
        {
            bills.Remove(record);
        }
        else
        {
            RequestMissingInputs(record, recipe);
        }

        Touch(recipe.WorkTypeId, requestWorker: !finished);
        workforceReplanService?.RequestOneHaulerToReplan(forceInterrupt: false);
        message = $"{recipe.DisplayName} 생산 완료";
        return true;
    }

    private int ResolveOutputAmount(int baseAmount, float multiplier)
    {
        float scaled = Mathf.Max(0f, baseAmount) * Mathf.Max(0f, multiplier);
        int whole = Mathf.FloorToInt(scaled);
        float remainder = scaled - whole;
        return Mathf.Max(
            1,
            whole + (remainder > 0f && random.Chance(remainder) ? 1 : 0));
    }

    public DungeonProductionBillSaveData Capture()
    {
        return new DungeonProductionBillSaveData
        {
            nextBillSequence = nextBillSequence,
            bills = bills.Select(ToSaveData).ToList()
        };
    }

    public void Restore(DungeonProductionBillSaveData snapshot)
    {
        bills.Clear();
        nextBillSequence = Mathf.Max(1, snapshot?.nextBillSequence ?? 1);
        foreach (ProductionBillSaveData saved in snapshot?.bills
                 ?? new List<ProductionBillSaveData>())
        {
            if (saved == null
                || !catalog.TryGetRecipe(saved.recipeId, out _)
                || string.IsNullOrWhiteSpace(saved.billId))
            {
                continue;
            }

            ProductionBillRecord record = new ProductionBillRecord
            {
                billId = saved.billId,
                recipeId = saved.recipeId,
                buildingId = saved.buildingId,
                position = new Vector2Int(saved.gridX, saved.gridY),
                mode = saved.mode,
                remainingCycles = saved.remainingCycles,
                targetStock = Mathf.Max(0, saved.targetStock),
                minimumReserve = Mathf.Max(0, saved.minimumReserve),
                suspended = saved.suspended,
                materialsConsumed = saved.materialsConsumed,
                processFluidConsumed = saved.processFluidConsumed,
                completedWork = Mathf.Max(0f, saved.completedWork),
                reservedWorkerId = string.Empty,
                materialDestinationId = string.IsNullOrWhiteSpace(saved.materialDestinationId)
                    ? DestinationPrefix + saved.billId
                    : saved.materialDestinationId
            };
            record.allowedMaterialIds.UnionWith(
                saved.allowedMaterialIds ?? new List<string>());
            record.allowedWorkerIds.UnionWith(
                saved.allowedWorkerIds ?? new List<string>());
            bills.Add(record);
        }

        Touch(default, requestWorker: false);
    }

    private ProductionBillRecord FindRunnableBill(
        BuildableObject facility,
        WorkTypeId workTypeId,
        bool requireDeliveredInputs,
        out string reason)
    {
        reason = "등록된 생산 주문이 없습니다.";
        if (facility == null || !workTypeId.IsValid)
        {
            return null;
        }

        foreach (ProductionBillRecord record in bills)
        {
            if (!MatchesFacility(record, facility)
                || record.suspended
                || ResolveRecipe(record) is not ProductionRecipeSO recipe
                || recipe.WorkTypeId != workTypeId)
            {
                continue;
            }

            if (!IsResearchUnlocked(recipe, out reason))
            {
                continue;
            }

            if (!ShouldRunAnotherCycle(record, recipe))
            {
                reason = "목표 재고를 충족했습니다.";
                continue;
            }

            if (record.materialsConsumed)
            {
                return record;
            }

            if (!HasDeliveredInputs(record, recipe, out reason))
            {
                RequestMissingInputs(record, recipe);
                if (requireDeliveredInputs)
                {
                    continue;
                }
            }

            return record;
        }

        return null;
    }

    private bool HasDeliveredInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        out string reason)
    {
        reason = string.Empty;
        foreach (KeyValuePair<string, int> requirement in ToInputMap(recipe))
        {
            if (items.CountDelivered(
                    requirement.Key,
                    record.materialDestinationId)
                < requirement.Value)
            {
                reason = $"재료 운반 대기: {requirement.Key}";
                return false;
            }
        }

        return true;
    }

    private void RequestMissingInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe)
    {
        if (record == null || recipe == null || record.materialsConsumed)
        {
            return;
        }

        bool requestedAny = false;
        foreach (KeyValuePair<string, int> requirement in ToInputMap(recipe))
        {
            int pending = items.CountPending(
                requirement.Key,
                record.materialDestinationId);
            int missing = Mathf.Max(0, requirement.Value - pending);
            if (missing <= 0)
            {
                continue;
            }

            items.RequestDelivery(
                requirement.Key,
                missing,
                record.position,
                record.materialDestinationId,
                out int requested,
                out _);
            requestedAny |= requested > 0;
        }

        if (!requestedAny)
        {
            return;
        }

        items.PrioritizeDestination(record.materialDestinationId);
        workforceReplanService?.RequestOneHaulerToReplan(forceInterrupt: false);
    }

    private bool ShouldRunAnotherCycle(
        ProductionBillRecord record,
        ProductionRecipeSO recipe)
    {
        if (record == null || recipe == null)
        {
            return false;
        }

        if (record.mode == ProductionOrderMode.RepeatCount)
        {
            return record.remainingCycles > 0;
        }

        string primaryOutput = recipe.Outputs
            .FirstOrDefault(output => output != null)?.ItemId;
        if (string.IsNullOrWhiteSpace(primaryOutput))
        {
            return false;
        }

        int stock = items.CountAvailableStock(
            primaryOutput,
            record.materialDestinationId);
        return stock < Mathf.Max(record.minimumReserve, record.targetStock);
    }

    private bool IsResearchUnlocked(
        ProductionRecipeSO recipe,
        out string reason)
    {
        reason = string.Empty;
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.RequiredResearchId))
        {
            return true;
        }

        if (researchProvider == null
            || !researchProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            reason = $"연구 필요: {recipe.RequiredResearchId}";
            return false;
        }

        bool unlocked = runtime.State.Projects.IsCompleted(
            new ResearchProjectId(recipe.RequiredResearchId));
        if (!unlocked)
        {
            reason = $"연구 필요: {recipe.RequiredResearchId}";
        }
        return unlocked;
    }

    private static Dictionary<string, int> ToInputMap(
        ProductionRecipeSO recipe)
    {
        return (recipe?.Inputs ?? Array.Empty<ItemAmountDefinition>())
            .Where(input => input != null && !string.IsNullOrWhiteSpace(input.ItemId))
            .GroupBy(input => input.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(input => input.Amount),
                StringComparer.Ordinal);
    }

    private ProductionBillSnapshot ToSnapshot(
        ProductionBillRecord record,
        BuildableObject facility)
    {
        ProductionRecipeSO recipe = ResolveRecipe(record);
        ProductionBillStatus status;
        string blockedReason = string.Empty;
        if (record.suspended)
        {
            status = ProductionBillStatus.Suspended;
        }
        else if (record.materialsConsumed)
        {
            status = record.completedWork > 0f
                ? ProductionBillStatus.InProgress
                : ProductionBillStatus.Ready;
        }
        else if (recipe != null && HasDeliveredInputs(record, recipe, out blockedReason))
        {
            status = ProductionBillStatus.Ready;
        }
        else
        {
            status = ProductionBillStatus.WaitingForMaterials;
        }

        return new ProductionBillSnapshot
        {
            BillId = record.billId,
            RecipeId = record.recipeId,
            RecipeName = recipe?.DisplayName ?? record.recipeId,
            BuildingId = record.buildingId,
            Position = record.position,
            WorkTypeId = recipe?.WorkTypeId ?? default,
            Mode = record.mode,
            Status = status,
            RemainingCycles = record.remainingCycles,
            TargetStock = record.targetStock,
            MinimumReserve = record.minimumReserve,
            RequiredWork = recipe?.RequiredWork ?? 0f,
            CompletedWork = record.completedWork,
            MaterialsConsumed = record.materialsConsumed,
            ProcessFluidConsumed = record.processFluidConsumed,
            ReservedWorkerId = record.reservedWorkerId,
            MaterialDestinationId = record.materialDestinationId,
            BlockedReason = blockedReason,
            Inputs = recipe?.Inputs ?? Array.Empty<ItemAmountDefinition>(),
            Outputs = recipe?.Outputs ?? Array.Empty<ProductionOutputDefinition>()
        };
    }

    private static ProductionBillSaveData ToSaveData(ProductionBillRecord record)
    {
        return new ProductionBillSaveData
        {
            billId = record.billId,
            recipeId = record.recipeId,
            buildingId = record.buildingId,
            gridX = record.position.x,
            gridY = record.position.y,
            mode = record.mode,
            remainingCycles = record.remainingCycles,
            targetStock = record.targetStock,
            minimumReserve = record.minimumReserve,
            suspended = record.suspended,
            materialsConsumed = record.materialsConsumed,
            processFluidConsumed = record.processFluidConsumed,
            completedWork = record.completedWork,
            reservedWorkerId = record.reservedWorkerId,
            materialDestinationId = record.materialDestinationId,
            allowedMaterialIds = record.allowedMaterialIds.ToList(),
            allowedWorkerIds = record.allowedWorkerIds.ToList()
        };
    }

    private ProductionRecipeSO ResolveRecipe(ProductionBillRecord record)
    {
        return record != null
            && catalog.TryGetRecipe(record.recipeId, out ProductionRecipeSO recipe)
                ? recipe
                : null;
    }

    private ProductionBillRecord Find(string billId)
    {
        return string.IsNullOrWhiteSpace(billId)
            ? null
            : bills.FirstOrDefault(record =>
                string.Equals(record.billId, billId, StringComparison.Ordinal));
    }

    private static bool MatchesFacility(
        ProductionBillRecord record,
        BuildableObject facility)
    {
        return record != null
            && facility != null
            && !facility.IsGridDestroyed
            && record.buildingId == facility.id
            && record.position == facility.centerPos;
    }

    private void Touch(WorkTypeId workTypeId, bool requestWorker)
    {
        unchecked
        {
            Version++;
        }
        if (requestWorker && workTypeId.IsValid)
        {
            workforceReplanService?.RequestOneWorkerToReplanFor(workTypeId);
        }
    }
}
