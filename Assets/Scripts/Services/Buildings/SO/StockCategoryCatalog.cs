using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class StockCategoryDefinition
{
    public StockCategoryDefinition(
        string id,
        StockCategory category,
        string displayName,
        string shortName,
        int sortOrder,
        float seedWeight,
        int dailyBaseAmount,
        int dailyUnitCost,
        int dailyGrowthDivisor)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Stock category id is required.", nameof(id));
        }

        Id = id.Trim();
        Category = category;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        ShortName = string.IsNullOrWhiteSpace(shortName) ? DisplayName : shortName.Trim();
        SortOrder = sortOrder;
        SeedWeight = Mathf.Max(0f, seedWeight);
        DailyBaseAmount = Mathf.Max(0, dailyBaseAmount);
        DailyUnitCost = Mathf.Max(0, dailyUnitCost);
        DailyGrowthDivisor = Mathf.Max(1, dailyGrowthDivisor);
    }

    public string Id { get; }
    public StockCategory Category { get; }
    public string DisplayName { get; }
    public string ShortName { get; }
    public int SortOrder { get; }
    public float SeedWeight { get; }
    public int DailyBaseAmount { get; }
    public int DailyUnitCost { get; }
    public int DailyGrowthDivisor { get; }

    public int GetDailyAmount(int smallGrowth)
    {
        return DailyBaseAmount + Mathf.Max(0, smallGrowth / DailyGrowthDivisor);
    }
}

public static class StockCategoryCatalog
{
    private static readonly Dictionary<string, StockCategoryDefinition> ById =
        new Dictionary<string, StockCategoryDefinition>(StringComparer.Ordinal);
    private static readonly Dictionary<StockCategory, StockCategoryDefinition> ByCategory =
        new Dictionary<StockCategory, StockCategoryDefinition>();
    private static bool initialized;

    public static IReadOnlyList<StockCategoryDefinition> All
    {
        get
        {
            EnsureInitialized();
            return ById.Values
                .OrderBy(definition => definition.SortOrder)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public static void Register(StockCategoryDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        EnsureInitialized();
        if (ById.TryGetValue(definition.Id, out StockCategoryDefinition existingById)
            && existingById.Category != definition.Category)
        {
            throw new InvalidOperationException(
                $"Stock category id '{definition.Id}' is already assigned to {existingById.Category}.");
        }

        if (ByCategory.TryGetValue(definition.Category, out StockCategoryDefinition existingByCategory)
            && !string.Equals(existingByCategory.Id, definition.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Stock category value '{(int)definition.Category}' is already assigned to '{existingByCategory.Id}'.");
        }

        ById[definition.Id] = definition;
        ByCategory[definition.Category] = definition;
    }

    public static bool TryGet(StockCategory category, out StockCategoryDefinition definition)
    {
        EnsureInitialized();
        return ByCategory.TryGetValue(category, out definition);
    }

    public static bool TryGet(string id, out StockCategoryDefinition definition)
    {
        EnsureInitialized();
        return ById.TryGetValue(id?.Trim() ?? string.Empty, out definition);
    }

    public static string GetDisplayName(StockCategory category)
    {
        return TryGet(category, out StockCategoryDefinition definition)
            ? definition.DisplayName
            : category.ToString();
    }

    public static string GetShortName(StockCategory category)
    {
        return TryGet(category, out StockCategoryDefinition definition)
            ? definition.ShortName
            : category.ToString();
    }

    public static void ResetToBuiltIns()
    {
        ById.Clear();
        ByCategory.Clear();
        initialized = true;
        RegisterBuiltIns();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ById.Clear();
        ByCategory.Clear();
        initialized = false;
    }

    private static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        RegisterBuiltIns();
    }

    private static void RegisterBuiltIns()
    {
        RegisterBuiltIn("stock:food", StockCategory.Food, "식량", "식량", 10, 0.40f, 20, 4, 1);
        RegisterBuiltIn("stock:water", StockCategory.Water, "물", "물", 15, 0.24f, 18, 3, 1);
        RegisterBuiltIn("stock:general", StockCategory.General, "잡화", "잡화", 20, 0.25f, 14, 6, 1);
        RegisterBuiltIn("stock:medicine", StockCategory.Medicine, "약품", "약", 25, 0.08f, 6, 12, 2);
        RegisterBuiltIn("stock:weapon", StockCategory.Weapon, "무기", "무기", 30, 0.25f, 8, 10, 2);
        RegisterBuiltIn("stock:ammunition", StockCategory.Ammunition, "탄약", "탄약", 32, 0.18f, 12, 4, 2);
        RegisterBuiltIn("stock:fuel", StockCategory.Fuel, "연료", "연료", 35, 0.16f, 12, 5, 1);
        RegisterBuiltIn("stock:mana", StockCategory.Mana, "마나", "마나", 40, 0.10f, 10, 9, 2);
    }

    private static void RegisterBuiltIn(
        string id,
        StockCategory category,
        string displayName,
        string shortName,
        int sortOrder,
        float seedWeight,
        int dailyBaseAmount,
        int dailyUnitCost,
        int dailyGrowthDivisor)
    {
        Register(new StockCategoryDefinition(
            id,
            category,
            displayName,
            shortName,
            sortOrder,
            seedWeight,
            dailyBaseAmount,
            dailyUnitCost,
            dailyGrowthDivisor));
    }
}
