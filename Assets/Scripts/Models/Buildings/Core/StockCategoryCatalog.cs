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
        string deliveryItemId,
        int dailyBaseAmount,
        float dailyUnitCost,
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
        DeliveryItemId = deliveryItemId?.Trim() ?? string.Empty;
        DailyBaseAmount = Mathf.Max(0, dailyBaseAmount);
        DailyUnitCost = Mathf.Max(0f, dailyUnitCost);
        DailyGrowthDivisor = Mathf.Max(1, dailyGrowthDivisor);

        if (DailyBaseAmount > 0 && DeliveryItemId.Length == 0)
        {
            throw new ArgumentException(
                $"Stock category '{Id}' has a daily delivery amount but no concrete delivery item.",
                nameof(deliveryItemId));
        }
    }

    public string Id { get; }
    public StockCategory Category { get; }
    public string DisplayName { get; }
    public string ShortName { get; }
    public int SortOrder { get; }
    public float SeedWeight { get; }
    public string DeliveryItemId { get; }
    public int DailyBaseAmount { get; }
    public float DailyUnitCost { get; }
    public int DailyGrowthDivisor { get; }

    public int GetDailyAmount(int smallGrowth)
    {
        return DailyBaseAmount + Mathf.Max(0, smallGrowth / DailyGrowthDivisor);
    }
}

public static class GoldEconomyBalanceRules
{
    public const float GoldPerEmbeddedWorkUnit = 1f / 3f;
    public const float MinimumExternalPurchaseMarkup = 1.25f;
    public const float TargetExternalPurchaseMarkup = 1.35f;
    public const float MaximumExternalPurchaseMarkup = 1.50f;
    public const float MinimumExternalSaleRecovery = 0.50f;
    public const float TargetExternalSaleRecovery = 0.60f;
    public const float MaximumExternalSaleRecovery = 0.70f;
    public const float MinimumOrdinaryRetailMarkup = 1.15f;
    public const float TargetOrdinaryRetailMarkup = 1.20f;
    public const float MaximumOrdinaryRetailMarkup = 1.25f;
    public const float MaximumRetailFacilityPremium = 1.10f;
    public const float MaximumWorkerRevenuePremium = 1.15f;
    public const float TargetRegionalContractMarkup = 1.20f;
    public const float MaximumContractProjectMultiplier = 1.25f;
    public const float MinimumPremiumServiceNetMargin = 0.20f;
    public const float TargetPremiumServiceNetMargin = 0.25f;
    public const float MaximumPremiumServiceNetMargin = 0.35f;

    public static float TargetPurchaseGoldPerEmbeddedWorkUnit =>
        GoldPerEmbeddedWorkUnit * TargetExternalPurchaseMarkup;

    public static int CalculateRetailBasePrice(int internalUnitPrice) =>
        Mathf.Max(1, Mathf.CeilToInt(
            Mathf.Max(1, internalUnitPrice) * TargetOrdinaryRetailMarkup));

    public static int CalculateRegionalContractReward(
        int internalValue,
        float projectMultiplier) =>
        Mathf.Max(1, Mathf.RoundToInt(
            Mathf.Max(1, internalValue)
            * TargetRegionalContractMarkup
            * Mathf.Clamp(
                projectMultiplier,
                1f,
                MaximumContractProjectMultiplier)));

    public static int CalculatePremiumServiceReward(int internalValue) =>
        Mathf.Max(1, Mathf.CeilToInt(
            Mathf.Max(1, internalValue)
            / (1f - TargetPremiumServiceNetMargin)));
}
