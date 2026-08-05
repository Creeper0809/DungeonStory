using System;
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
