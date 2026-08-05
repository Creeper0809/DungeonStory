using System;
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
