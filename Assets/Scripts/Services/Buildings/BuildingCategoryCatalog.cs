using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class BuildingCategoryDefinition
{
    public BuildingCategoryDefinition(
        string id,
        BuildingCategory category,
        string displayName,
        int sortOrder,
        int shopCostWeight = 100)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Building category ID is required.", nameof(id));
        }

        Id = id.Trim();
        Category = category;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? Category.ToString()
            : displayName.Trim();
        SortOrder = sortOrder;
        ShopCostWeight = Mathf.Max(1, shopCostWeight);
    }

    public string Id { get; }
    public BuildingCategory Category { get; }
    public string DisplayName { get; }
    public int SortOrder { get; }
    public int ShopCostWeight { get; }
}

public static class BuildingCategoryCatalog
{
    private static readonly Dictionary<string, BuildingCategoryDefinition> ById =
        new Dictionary<string, BuildingCategoryDefinition>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<BuildingCategory, BuildingCategoryDefinition> ByCategory =
        new Dictionary<BuildingCategory, BuildingCategoryDefinition>();
    private static bool initialized;

    public static IReadOnlyList<BuildingCategoryDefinition> All
    {
        get
        {
            EnsureInitialized();
            return ById.Values
                .OrderBy((definition) => definition.SortOrder)
                .ThenBy((definition) => definition.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public static void Register(BuildingCategoryDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        EnsureInitialized();
        if (ById.TryGetValue(definition.Id, out BuildingCategoryDefinition existingById)
            && existingById.Category != definition.Category)
        {
            throw new InvalidOperationException(
                $"Building category ID '{definition.Id}' is already assigned to {existingById.Category}.");
        }

        if (ByCategory.TryGetValue(definition.Category, out BuildingCategoryDefinition existingByCategory)
            && !string.Equals(existingByCategory.Id, definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Building category '{definition.Category}' is already assigned to '{existingByCategory.Id}'.");
        }

        ById[definition.Id] = definition;
        ByCategory[definition.Category] = definition;
    }

    public static bool TryGet(
        BuildingCategory category,
        out BuildingCategoryDefinition definition)
    {
        EnsureInitialized();
        return ByCategory.TryGetValue(category, out definition);
    }

    public static bool TryResolve(
        string value,
        out BuildingCategoryDefinition definition)
    {
        EnsureInitialized();
        string normalized = value?.Trim() ?? string.Empty;
        if (ById.TryGetValue(normalized, out definition))
        {
            return true;
        }

        definition = ById.Values.FirstOrDefault((candidate) =>
            string.Equals(candidate.DisplayName, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Category.ToString(), normalized, StringComparison.OrdinalIgnoreCase));
        return definition != null;
    }

    public static string GetDisplayName(BuildingCategory category, string fallback = "시설")
    {
        return TryGet(category, out BuildingCategoryDefinition definition)
            ? definition.DisplayName
            : fallback;
    }

    public static int GetShopCostWeight(BuildingCategory category)
    {
        return TryGet(category, out BuildingCategoryDefinition definition)
            ? definition.ShopCostWeight
            : 100;
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
        RegisterBuiltIn("category:none", BuildingCategory.None, "기타", 0, 100);
        RegisterBuiltIn("category:wall", BuildingCategory.Wall, "벽/문", 10, 100);
        RegisterBuiltIn("category:shop", BuildingCategory.Shop, "상점", 20, 90);
        RegisterBuiltIn("category:special", BuildingCategory.Special, "특수", 30, 140);
        RegisterBuiltIn("category:movement", BuildingCategory.Movement, "이동", 40, 100);
        RegisterBuiltIn("category:production", BuildingCategory.Production, "생산", 50, 110);
        RegisterBuiltIn("category:crafting", BuildingCategory.Crafting, "제작", 60, 120);
        RegisterBuiltIn("category:resource", BuildingCategory.Resource, "자원", 70, 130);
    }

    private static void RegisterBuiltIn(
        string id,
        BuildingCategory category,
        string displayName,
        int sortOrder,
        int shopCostWeight)
    {
        BuildingCategoryDefinition definition = new BuildingCategoryDefinition(
            id,
            category,
            displayName,
            sortOrder,
            shopCostWeight);
        ById.Add(definition.Id, definition);
        ByCategory.Add(definition.Category, definition);
    }
}
