using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IProductionInputLogisticsService
{
    ProductionBillRecord FindRunnableBill(
        IReadOnlyList<ProductionBillRecord> bills,
        BuildableObject facility,
        WorkTypeId workTypeId,
        bool requireDeliveredInputs,
        out DomainFailure failure);
    bool HasDeliveredInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out DomainFailure failure);
    void RequestMissingInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility);
    long ResolveInputBufferMassCapacity(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility);
    void RecalculatePrefetch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        CharacterActor worker);
    bool ShouldRunAnotherCycle(
        ProductionBillRecord record,
        ProductionRecipeSO recipe);
    bool IsResearchUnlocked(
        ProductionRecipeSO recipe,
        out DomainFailure failure);
    Dictionary<string, int> ToCycleInputMap(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility);
}

public sealed class ProductionInputLogisticsService :
    IProductionInputLogisticsService
{
    private const float DefaultDeliverySeconds = 12f;
    private const float DeliverySafetySeconds = 3f;
    private const int MaximumPrefetchBatches = 3;

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionItemGateway items;
    private readonly BlueprintResearchRuntime research;
    private readonly IWorkforceReplanService workforceReplanService;
    private readonly IProductionWorkshopRuntime workshops;

    public ProductionInputLogisticsService(
        IResourceEconomyContentCatalog catalog,
        IProductionItemGateway items,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IWorkforceReplanService workforceReplanService,
        IProductionWorkshopRuntime workshops)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(ProductionInputLogisticsService)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.workforceReplanService = workforceReplanService
            ?? throw new ArgumentNullException(nameof(workforceReplanService));
        this.workshops = workshops
            ?? throw new ArgumentNullException(nameof(workshops));
    }

    public ProductionBillRecord FindRunnableBill(
        IReadOnlyList<ProductionBillRecord> bills,
        BuildableObject facility,
        WorkTypeId workTypeId,
        bool requireDeliveredInputs,
        out DomainFailure failure)
    {
        failure = new DomainFailure(FailureCode.ProductionBillMissing);
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

            if (!IsResearchUnlocked(recipe, out failure))
            {
                continue;
            }

            if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                && record.batchStage == ProductionBatchStage.Processing)
            {
                failure = record.blockedFailure.IsFailure
                    ? record.blockedFailure
                    : new DomainFailure(FailureCode.ProductionProcessingActive);
                continue;
            }

            if (!ShouldRunAnotherCycle(record, recipe))
            {
                failure = new DomainFailure(
                    FailureCode.ProductionTargetStockSatisfied);
                continue;
            }

            if (record.materialsConsumed)
            {
                return record;
            }

            if (!HasDeliveredInputs(record, recipe, facility, out failure))
            {
                RequestMissingInputs(record, recipe, facility);
                if (requireDeliveredInputs)
                {
                    continue;
                }
            }

            return record;
        }

        return null;
    }

    public bool HasDeliveredInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        foreach (KeyValuePair<string, int> requirement in ToCycleInputMap(
                     record,
                     recipe,
                     facility))
        {
            if (items.CountDelivered(
                    requirement.Key,
                    record.materialDestinationId)
                < requirement.Value)
            {
                failure = new DomainFailure(
                    FailureCode.ProductionMaterialsMissing);
                return false;
            }
        }

        return true;
    }

    public void RequestMissingInputs(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility)
    {
        if (record == null || recipe == null)
        {
            return;
        }

        bool requestedAny = false;
        int requestedBatches = ResolveRequestedBatchCount(record);
        if (record.mode == ProductionOrderMode.RepeatCount)
        {
            int remainingInputCycles = Mathf.Max(
                0,
                record.remainingCycles - (record.materialsConsumed ? 1 : 0));
            requestedBatches = Mathf.Min(
                requestedBatches,
                remainingInputCycles);
            if (requestedBatches <= 0)
            {
                return;
            }
        }
        Dictionary<string, int> cycleInputs = ToCycleInputMap(
            record,
            recipe,
            facility);

        foreach (KeyValuePair<string, int> requirement in cycleInputs
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            int pending = items.CountPending(
                requirement.Key,
                record.materialDestinationId);
            int target = requirement.Value * requestedBatches;
            int missing = Mathf.Max(0, target - pending);
            if (missing <= 0)
            {
                continue;
            }

            items.RequestDelivery(
                requirement.Key,
                missing,
                facility.centerPos,
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
        workforceReplanService.RequestOneHaulerToReplan(forceInterrupt: false);
    }

    public long ResolveInputBufferMassCapacity(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility)
    {
        if (record == null || recipe == null || facility == null)
        {
            throw new ArgumentException(
                "A production bill, recipe, and live facility are required.");
        }

        int capacityBatchCount = Mathf.Clamp(
            Mathf.Max(2, ResolveRequestedBatchCount(record)),
            2,
            MaximumPrefetchBatches);
        long maxMassGrams = 0L;
        foreach (KeyValuePair<string, int> requirement in ToCycleInputMap(
                     record,
                     recipe,
                     facility)
                 .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            maxMassGrams = checked(maxMassGrams
                + items.GetDefinitionQuantityMassGrams(
                    requirement.Key,
                    checked(requirement.Value * capacityBatchCount)));
        }
        if (maxMassGrams <= 0L)
        {
            throw new InvalidOperationException(
                $"Production bill '{record.billId.Value}' has no positive input-buffer mass capacity.");
        }
        return maxMassGrams;
    }

    public void RecalculatePrefetch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        CharacterActor worker)
    {
        if (record == null || recipe == null)
        {
            return;
        }

        float workerSpeed = worker == null
            ? 1f
            : Mathf.Max(0.1f, worker.GetWorkSpeedMultiplier(recipe.WorkTypeId));
        float requiredWork = Mathf.Max(0.1f, ResolveCurrentRequiredWork(record, recipe));
        float effectiveCycleSeconds = requiredWork / workerSpeed;
        float deliverySeconds = record.estimatedDeliverySeconds > 0f
            ? record.estimatedDeliverySeconds
            : DefaultDeliverySeconds;
        int batches = ProductionMaterialPrefetchPolicy.CalculateBatchCount(
            deliverySeconds,
            DeliverySafetySeconds,
            effectiveCycleSeconds,
            MaximumPrefetchBatches);

        record.SetPrefetchPlan(
            effectiveCycleSeconds,
            batches,
            batches > 1
                ? new ProductionLogisticsStatus(
                    ProductionBillOutcomeCode.MaterialPrefetchAdjusted,
                    batches.ToString(
                        System.Globalization.CultureInfo.InvariantCulture))
                : ProductionLogisticsStatus.None);
    }

    public bool ShouldRunAnotherCycle(
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

        if (record.mode == ProductionOrderMode.RepeatForever)
        {
            return true;
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

    public bool IsResearchUnlocked(
        ProductionRecipeSO recipe,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (recipe == null || string.IsNullOrWhiteSpace(recipe.RequiredResearchId))
        {
            return true;
        }

        BlueprintResearchRuntime runtime = research;
        if (runtime == null)
        {
            failure = new DomainFailure(
                FailureCode.ProductionResearchLocked,
                recipe.RequiredResearchId);
            return false;
        }

        bool unlocked = runtime.State.Projects.IsCompleted(
            new ResearchProjectId(recipe.RequiredResearchId));
        if (!unlocked)
        {
            failure = new DomainFailure(
                FailureCode.ProductionResearchLocked,
                recipe.RequiredResearchId);
        }
        return unlocked;
    }

    public Dictionary<string, int> ToCycleInputMap(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        BuildableObject facility)
    {
        Dictionary<string, int> costs =
            (recipe?.Inputs ?? Array.Empty<ItemAmountDefinition>())
            .Where(input => input != null && !string.IsNullOrWhiteSpace(input.ItemId))
            .GroupBy(input => input.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(input => input.Amount),
                StringComparer.Ordinal);
        if (facility == null || recipe == null)
        {
            return costs;
        }

        HashSet<string> checkedSupports =
            new HashSet<string>(StringComparer.Ordinal);
        foreach (string tag in recipe.RequiredSupportTags
                     .Where(tag => !string.IsNullOrWhiteSpace(tag))
                     .Distinct(StringComparer.Ordinal))
        {
            if (!workshops.TryResolveSupport(
                    facility,
                    tag,
                    null,
                    out BuildableObject support,
                    out BuildingProductionSupportAbility ability)
                || ability == null
                || !ability.requiresFuel)
            {
                continue;
            }

            string nodeId =
                IndustrialInfrastructureIdentity.GetNodeId(support);
            string supplyKey = $"fuel:{nodeId}";
            string fuelItemId = ResolveFacilitySupplyItem(
                record,
                supplyKey,
                support,
                FacilitySupplyKind.Fuel,
                ability.fuelItemId);
            if (!checkedSupports.Add(nodeId)
                || string.IsNullOrWhiteSpace(fuelItemId))
            {
                continue;
            }

            costs[fuelItemId] =
                (costs.TryGetValue(fuelItemId, out int current)
                    ? current
                    : 0)
                + Mathf.Max(1, ability.fuelPerCycle);
        }
        return costs;
    }

    private string ResolveFacilitySupplyItem(
        ProductionBillRecord record,
        string supplyKey,
        BuildableObject support,
        FacilitySupplyKind kind,
        string fallbackItemId)
    {
        if (record != null
            && record.selectedSupplies.TryGetValue(supplyKey, out string selected)
            && catalog.TryGetItem(selected, out ResourceItemDefinitionSO selectedItem)
            && IsAllowedSupply(support, kind, selectedItem))
        {
            return selected;
        }

        FacilitySupplyProfile profile = support?.BuildingData
            .GetFacilitySupplyAbility()?.GetProfile(kind);
        if (profile == null)
        {
            return fallbackItemId?.Trim() ?? string.Empty;
        }

        ResourceItemDefinitionSO[] candidates = catalog.Items
            .Where(profile.Allows)
            .ToArray();
        ResourceItemDefinitionSO[] available = candidates
            .Where(item => items.CountAvailableStock(
                item.ItemId,
                record?.materialDestinationId ?? string.Empty) > 0)
            .ToArray();
        ResourceItemDefinitionSO chosen = (available.Length > 0
                ? available
                : candidates)
            .OrderBy(item => SupplyPriority(profile, item.ItemId))
            .ThenBy(item => SupplyPricePerValue(item, kind))
            .ThenBy(item => items is IProductionSupplyInventoryGateway inventory
                ? inventory.GetOldestAvailableStackId(
                    item.ItemId,
                    record?.materialDestinationId ?? string.Empty)
                : string.Empty,
                StringComparer.Ordinal)
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
        string itemId = chosen?.ItemId ?? fallbackItemId?.Trim() ?? string.Empty;
        if (record != null && !string.IsNullOrWhiteSpace(itemId))
        {
            record.SelectSupply(supplyKey, itemId);
        }
        return itemId;
    }

    private static bool IsAllowedSupply(
        BuildableObject support,
        FacilitySupplyKind kind,
        ResourceItemDefinitionSO item)
    {
        FacilitySupplyProfile profile = support?.BuildingData
            .GetFacilitySupplyAbility()?.GetProfile(kind);
        return profile == null || profile.Allows(item);
    }

    private static int SupplyPriority(
        FacilitySupplyProfile profile,
        string itemId)
    {
        int index = profile?.priorityItemIds?.FindIndex(id =>
            string.Equals(id, itemId, StringComparison.Ordinal)) ?? -1;
        return index < 0 ? int.MaxValue : index;
    }

    private static float SupplyPricePerValue(
        ResourceItemDefinitionSO item,
        FacilitySupplyKind kind)
    {
        float value = kind == FacilitySupplyKind.Fuel
            ? item.FuelValue
            : item.FacilityNutritionValue;
        return item.UnitPrice / Mathf.Max(0.01f, value);
    }


    private ProductionRecipeSO ResolveRecipe(ProductionBillRecord record)
    {
        return record != null
            && catalog.TryGetRecipe(
                record.recipeId,
                out ProductionRecipeSO recipe)
                    ? recipe
                    : null;
    }

    private static bool MatchesFacility(
        ProductionBillRecord record,
        BuildableObject facility)
    {
        return record != null
            && facility != null
            && !facility.IsGridDestroyed
            && record.buildingInstanceId.Equals(
                facility.PersistentInstanceId);
    }

    private static float ResolveCurrentRequiredWork(
        ProductionBillRecord record,
        ProductionRecipeSO recipe)
    {
        if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch)
        {
            return recipe.RequiredWork;
        }

        return record.batchStage == ProductionBatchStage.Finishing
            ? recipe.FinishingWork
            : recipe.PreparationWork;
    }

    private static int ResolveRequestedBatchCount(
        ProductionBillRecord record)
    {
        int requestedBatches = Mathf.Clamp(
            record?.prefetchBatchCount ?? 1,
            1,
            MaximumPrefetchBatches);
        if (record?.mode != ProductionOrderMode.RepeatCount)
            return requestedBatches;

        int remainingInputCycles = Mathf.Max(
            0,
            record.remainingCycles - (record.materialsConsumed ? 1 : 0));
        return Mathf.Min(requestedBatches, remainingInputCycles);
    }
}
