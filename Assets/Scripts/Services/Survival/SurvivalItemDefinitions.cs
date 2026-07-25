using System;
using UnityEngine;

public static class SurvivalItemDefinitions
{
    public const string RawFoodItemId = "survival:raw_food";
    public const string CookedMealItemId = "survival:cooked_meal";
    public const string PreservedFoodItemId = "survival:preserved_food";
    public const string TaintedFoodItemId = "survival:tainted_food";

    public static bool TryGetStockCategory(string itemId, out StockCategory category)
    {
        switch (itemId?.Trim() ?? string.Empty)
        {
            case RawFoodItemId:
            case CookedMealItemId:
            case PreservedFoodItemId:
            case TaintedFoodItemId:
                category = StockCategory.Food;
                return true;
            default:
                category = default;
                return false;
        }
    }

    public static bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
    {
        switch (itemId?.Trim() ?? string.Empty)
        {
            case RawFoodItemId:
                definition = Food(
                    RawFoodItemId,
                    "날 식재료",
                    "조리하거나 보존하지 않으면 금방 상하는 식재료입니다.",
                    3,
                    0.6f,
                    50);
                return true;
            case CookedMealItemId:
                definition = Food(
                    CookedMealItemId,
                    "조리 식량",
                    "바로 먹을 수 있는 식량입니다. 포만감과 기분 회복에 좋습니다.",
                    6,
                    0.55f,
                    50);
                return true;
            case PreservedFoodItemId:
                definition = Food(
                    PreservedFoodItemId,
                    "보존 식량",
                    "맛은 덜하지만 오래 보관할 수 있는 식량입니다.",
                    5,
                    0.5f,
                    75);
                return true;
            case TaintedFoodItemId:
                definition = Food(
                    TaintedFoodItemId,
                    "오염된 음식",
                    "먹으면 병을 부를 수 있는 음식입니다. 치우거나 버려야 합니다.",
                    0,
                    0.5f,
                    75);
                return true;
            default:
                definition = null;
                return false;
        }
    }

    public static bool IsFoodLike(string itemId)
    {
        return TryGetStockCategory(itemId, out StockCategory category)
            && category == StockCategory.Food;
    }

    public static bool IsPreserved(string itemId)
    {
        return string.Equals(itemId?.Trim(), PreservedFoodItemId, StringComparison.Ordinal);
    }

    public static bool IsContaminated(string itemId)
    {
        return string.Equals(itemId?.Trim(), TaintedFoodItemId, StringComparison.Ordinal);
    }

    private static DungeonItemDefinition Food(
        string itemId,
        string displayName,
        string description,
        int unitPrice,
        float unitWeight,
        int maxStack)
    {
        return new DungeonItemDefinition(
            itemId,
            displayName,
            description,
            StockCategory.Food,
            unitPrice,
            null,
            Mathf.Max(0.01f, unitWeight),
            Mathf.Max(1, maxStack));
    }
}
