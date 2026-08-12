using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class SurvivalFoodSpoilageRuntime
{
    internal const int FreshnessSchemaVersion = 2;
    private const string RemainingSecondsKey = "remaining-seconds";
    private const string PreservedKey = "preserved";
    private const float FreshnessWarningThresholdSeconds = 90f;

    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IItemDefinitionCatalog itemCatalog;
    private readonly SurvivalFoodStockRuntime stockRuntime;
    private readonly ICharacterCarryInventoryRegistry carryInventories;
    private IEnvironmentalFieldQuery environmentalField =
        NoEnvironmentalFieldQuery.Instance;

    public SurvivalFoodSpoilageRuntime(
        IWorldItemStackRuntime itemStackRuntime,
        IItemDefinitionCatalog itemCatalog,
        SurvivalFoodStockRuntime stockRuntime,
        ICharacterCarryInventoryRegistry carryInventories = null)
    {
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.itemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
        this.stockRuntime = stockRuntime
            ?? throw new ArgumentNullException(nameof(stockRuntime));
        this.carryInventories = carryInventories;
    }

    public void ConfigureStorageEnvironment(IEnvironmentalFieldQuery fieldQuery)
    {
        environmentalField = fieldQuery
            ?? throw new ArgumentNullException(nameof(fieldQuery));
    }

    public void DebugAdvance(DungeonSurvivalSaveData state, float seconds)
    {
        _ = state ?? throw new ArgumentNullException(nameof(state));
        float advance = Mathf.Max(0f, seconds);
        foreach (WorldItemStackSnapshot stack in GetTrackableStacks())
        {
            FreshnessState freshness = ReadFreshness(stack);
            if (!freshness.Preserved)
            {
                WriteFreshness(
                    stack.StackId,
                    Mathf.Max(0f, freshness.RemainingSeconds - advance),
                    freshness.Preserved);
            }
        }
        ProcessCarriedFood(advance);
    }

    private void ProcessCarriedFood(float elapsedSeconds)
    {
        if (carryInventories == null)
            return;
        IReadOnlyList<CharacterCarryInventory> inventories = carryInventories.All;
        for (int inventoryIndex = 0; inventoryIndex < inventories.Count; inventoryIndex++)
        {
            CharacterCarryInventory inventory = inventories[inventoryIndex];
            if (inventory == null) continue;
            List<CharacterCarriedItemSaveData> spoiled =
                inventory.AdvanceCarriedFoodFreshness(
                    itemCatalog,
                    Mathf.Max(0f, elapsedSeconds));
            for (int itemIndex = 0; itemIndex < spoiled.Count; itemIndex++)
            {
                CharacterCarriedItemSaveData item = spoiled[itemIndex];
                if (item == null
                    || item.quantity <= 0
                    || !TryGetTrackableDefinition(
                        item.itemId,
                        out ItemDefinitionSO definition))
                {
                    continue;
                }
                ResolveWaste(
                    definition,
                    out string wasteItemId,
                    out WasteOriginKind wasteOrigin);
                itemStackRuntime.SpawnWasteAt(
                    wasteItemId,
                    item.quantity,
                    inventory.OwnerGridPosition,
                    wasteOrigin,
                    item.contamination > 0.01f ? 90f : 50f,
                    out _);
            }
        }
    }

    public void DebugReset(DungeonSurvivalSaveData state)
    {
        _ = state ?? throw new ArgumentNullException(nameof(state));
        foreach (WorldItemStackSnapshot stack in GetTrackableStacks())
        {
            ItemDefinitionSO definition = RequireTrackableDefinition(stack.ItemId);
            FoodItemFeature food = definition.GetFeatureOrDefault<FoodItemFeature>();
            WriteFreshness(
                stack.StackId,
                food.freshnessSeconds,
                food.preserved);
        }
    }

    public bool TryGetItemStatus(
        DungeonSurvivalSaveData state,
        string stackId,
        string itemId,
        out SurvivalItemStatus status)
    {
        _ = state ?? throw new ArgumentNullException(nameof(state));
        string normalizedStackId = stackId?.Trim() ?? string.Empty;
        string normalizedItemId = itemId?.Trim() ?? string.Empty;
        WorldItemStackSnapshot stack = stockRuntime.GetCachedItemStacks()
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.StackId,
                    normalizedStackId,
                    StringComparison.Ordinal));
        if (stack == null
            || !string.Equals(stack.ItemId, normalizedItemId, StringComparison.Ordinal)
            || !TryGetTrackableDefinition(stack.ItemId, out ItemDefinitionSO definition))
        {
            status = default;
            return false;
        }

        FreshnessState freshness = ReadFreshness(stack, definition);
        FoodItemFeature food = definition.GetFeatureOrDefault<FoodItemFeature>();
        bool contaminated = stack.Contamination > 0.01f;
        string label = contaminated
            ? "freshness-contaminated"
            : freshness.RemainingSeconds <= FreshnessWarningThresholdSeconds
                ? "freshness-warning"
                : freshness.Preserved
                    ? "freshness-preserved"
                    : "freshness-fresh";
        status = new SurvivalItemStatus(
            tracked: true,
            preserved: freshness.Preserved,
            contaminated: contaminated,
            freshness01: freshness.RemainingSeconds
                / Mathf.Max(1f, food.freshnessSeconds),
            remainingFreshnessSeconds: freshness.RemainingSeconds,
            label: label);
        return true;
    }

    public void Process(
        DungeonSurvivalSaveData state,
        SurvivalWeatherType weather,
        bool advanceTime = false)
    {
        _ = state ?? throw new ArgumentNullException(nameof(state));
        foreach (WorldItemStackSnapshot stack in GetTrackableStacks())
        {
            ItemDefinitionSO definition = RequireTrackableDefinition(stack.ItemId);
            FreshnessState freshness = ReadFreshness(stack, definition);
            float remaining = freshness.RemainingSeconds;
            if (advanceTime)
            {
                float environmentMultiplier = environmentalField.IsInitialized
                    ? environmentalField.GetFoodSpoilageMultiplier(stack.Position)
                    : weather == SurvivalWeatherType.HeatWave
                        ? 1.35f
                        : weather == SurvivalWeatherType.ColdSnap
                            ? 0.45f
                            : 1f;
                float dailyDelta = 180f * environmentMultiplier;
                remaining -= freshness.Preserved ? dailyDelta * 0.25f : dailyDelta;
            }

            bool contaminated = stack.Contamination > 0.01f;
            if (advanceTime && (remaining <= 0f || contaminated))
            {
                ResolveWaste(
                    definition,
                    out string wasteItemId,
                    out WasteOriginKind wasteOrigin);
                float contamination = contaminated ? 90f : 50f;
                itemStackRuntime.DeleteStack(stack.StackId);
                itemStackRuntime.SpawnWasteAt(
                    wasteItemId,
                    Mathf.Max(1, stack.Quantity),
                    stack.Position,
                    wasteOrigin,
                    contamination,
                    out _);
                continue;
            }

            if (!freshness.HasComponent || advanceTime)
            {
                WriteFreshness(
                    stack.StackId,
                    Mathf.Max(0f, remaining),
                    freshness.Preserved);
            }
        }

        if (advanceTime)
            ProcessCarriedFood(180f);
    }

    public int CountWarnings(DungeonSurvivalSaveData state)
    {
        _ = state ?? throw new ArgumentNullException(nameof(state));
        int count = 0;
        foreach (WorldItemStackSnapshot stack in GetTrackableStacks())
        {
            FreshnessState freshness = ReadFreshness(stack);
            if (stack.Contamination > 0.01f
                || freshness.RemainingSeconds <= FreshnessWarningThresholdSeconds)
            {
                count++;
            }
        }

        return count;
    }

    public int CountLooseRotStacks()
    {
        int count = 0;
        IReadOnlyList<WorldItemStackSnapshot> stacks = stockRuntime.GetCachedItemStacks();
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSnapshot stack = stacks[index];
            if (stack != null
                && !stack.Forbidden
                && stack.State != WorldItemStackState.Carried
                && (stack.IsWaste
                    || stack.ItemId.StartsWith("waste:", StringComparison.Ordinal)
                    || string.Equals(
                        stack.ItemId,
                        WildlifeItemDefinitions.RotItemId,
                        StringComparison.Ordinal)))
            {
                count++;
            }
        }

        return count;
    }

    private IEnumerable<WorldItemStackSnapshot> GetTrackableStacks()
    {
        return stockRuntime.GetCachedItemStacks()
            .Where(stack => stack != null
                && stack.State != WorldItemStackState.Carried
                && TryGetTrackableDefinition(stack.ItemId, out _));
    }

    private FreshnessState ReadFreshness(WorldItemStackSnapshot stack)
    {
        return ReadFreshness(stack, RequireTrackableDefinition(stack.ItemId));
    }

    private static FreshnessState ReadFreshness(
        WorldItemStackSnapshot stack,
        ItemDefinitionSO definition)
    {
        FoodItemFeature food = definition.GetFeatureOrDefault<FoodItemFeature>();
        ItemInstanceComponentSaveData component = stack.Components?
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.componentTypeId,
                    ItemInstanceComponentIds.Freshness,
                    StringComparison.Ordinal));
        float remaining = Mathf.Max(0f, food.freshnessSeconds);
        bool preserved = food.preserved;
        if (component != null)
        {
            ItemStateValueSaveData remainingValue = component.values?
                .FirstOrDefault(value => value != null
                    && string.Equals(
                        value.key,
                        RemainingSecondsKey,
                        StringComparison.Ordinal));
            ItemStateValueSaveData preservedValue = component.values?
                .FirstOrDefault(value => value != null
                    && string.Equals(value.key, PreservedKey, StringComparison.Ordinal));
            if (remainingValue?.kind == ItemStateValueKind.Decimal)
            {
                remaining = Mathf.Max(0f, (float)remainingValue.decimalValue);
            }
            if (preservedValue?.kind == ItemStateValueKind.Boolean)
            {
                preserved = preservedValue.booleanValue;
            }
        }

        return new FreshnessState(
            remaining,
            preserved,
            component != null && component.schemaVersion == FreshnessSchemaVersion);
    }

    private void WriteFreshness(
        string stackId,
        float remainingSeconds,
        bool preserved)
    {
        if (!itemStackRuntime.TrySetInstanceComponent(
                stackId,
                new ItemInstanceComponentSaveData
                {
                    componentTypeId = ItemInstanceComponentIds.Freshness,
                    schemaVersion = FreshnessSchemaVersion,
                    affectsStacking = true,
                    values = new List<ItemStateValueSaveData>
                    {
                        new ItemStateValueSaveData
                        {
                            key = RemainingSecondsKey,
                            kind = ItemStateValueKind.Decimal,
                            decimalValue = Math.Max(0d, remainingSeconds)
                        },
                        new ItemStateValueSaveData
                        {
                            key = PreservedKey,
                            kind = ItemStateValueKind.Boolean,
                            booleanValue = preserved
                        }
                    }
                }))
        {
            throw new InvalidOperationException(
                $"Could not persist freshness for physical stack '{stackId}'.");
        }
    }

    private bool TryGetTrackableDefinition(
        string itemId,
        out ItemDefinitionSO definition)
    {
        return itemCatalog.TryGet((ItemDefinitionId)itemId, out definition)
            && definition != null
            && definition.StockCategory == StockCategory.Food
            && definition.TryGetFeature(out FoodItemFeature food)
            && food.freshnessSeconds > 0f;
    }

    private ItemDefinitionSO RequireTrackableDefinition(string itemId)
    {
        if (TryGetTrackableDefinition(itemId, out ItemDefinitionSO definition))
        {
            return definition;
        }

        throw new InvalidOperationException(
            $"Physical food stack '{itemId}' has no authored perishable-food definition.");
    }

    private static void ResolveWaste(
        ItemDefinitionSO definition,
        out string wasteItemId,
        out WasteOriginKind origin)
    {
        ResourceIngredientTag tags =
            definition.GetFeatureOrDefault<ProductionItemFeature>()?.ingredientTags
            ?? ResourceIngredientTag.None;
        bool forbidden = (tags & ResourceIngredientTag.Forbidden) != 0;
        bool plant = (tags & (ResourceIngredientTag.Plant | ResourceIngredientTag.Fungus)) != 0;
        bool animal = (tags & (ResourceIngredientTag.Meat
            | ResourceIngredientTag.Blood
            | ResourceIngredientTag.Fat
            | ResourceIngredientTag.Milk
            | ResourceIngredientTag.Egg)) != 0;

        if (forbidden)
        {
            origin = WasteOriginKind.Forbidden;
            wasteItemId = "waste:forbidden-rot";
        }
        else if (plant && !animal)
        {
            origin = WasteOriginKind.Plant;
            wasteItemId = "waste:plant-rot";
        }
        else if (animal && !plant)
        {
            origin = WasteOriginKind.Animal;
            wasteItemId = "waste:animal-rot";
        }
        else
        {
            origin = WasteOriginKind.Mixed;
            wasteItemId = "waste:mixed-rot";
        }
    }

    private readonly struct FreshnessState
    {
        public FreshnessState(float remainingSeconds, bool preserved, bool hasComponent)
        {
            RemainingSeconds = Mathf.Max(0f, remainingSeconds);
            Preserved = preserved;
            HasComponent = hasComponent;
        }

        public float RemainingSeconds { get; }
        public bool Preserved { get; }
        public bool HasComponent { get; }
    }
}
