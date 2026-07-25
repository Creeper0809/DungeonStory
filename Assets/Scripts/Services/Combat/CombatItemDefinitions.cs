using System;
using System.Collections.Generic;
using UnityEngine;

public static class CombatItemDefinitions
{
    public const string ArrowItemId = "ammo:arrow";
    public const string BoltItemId = "ammo:bolt";
    public const string ArrowBundleRecipeId = "craft:ammo:arrow";
    public const string BoltBundleRecipeId = "craft:ammo:bolt";

    private static readonly Dictionary<string, DungeonItemDefinition> Definitions =
        new Dictionary<string, DungeonItemDefinition>(StringComparer.Ordinal)
        {
            {
                ArrowItemId,
                new DungeonItemDefinition(
                    ArrowItemId,
                    "화살",
                    "활에 사용하는 회수 불가능한 탄약.",
                    StockCategory.Ammunition,
                    1,
                    null,
                    0.06f,
                    50)
            },
            {
                BoltItemId,
                new DungeonItemDefinition(
                    BoltItemId,
                    "볼트",
                    "석궁에 사용하는 무거운 탄약.",
                    StockCategory.Ammunition,
                    2,
                    null,
                    0.09f,
                    40)
            }
        };

    public static bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
    {
        return Definitions.TryGetValue(itemId?.Trim() ?? string.Empty, out definition);
    }
}
