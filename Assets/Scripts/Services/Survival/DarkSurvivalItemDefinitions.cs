using System;
using UnityEngine;

public static class DarkSurvivalItemDefinitions
{
    public const string HumanoidCorpseItemId = "dark:humanoid_corpse";
    public const string HumanoidMeatItemId = "dark:humanoid_meat";
    public const string BoneItemId = "dark:bone";

    public static bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        definition = normalized switch
        {
            HumanoidCorpseItemId => new DungeonItemDefinition(
                HumanoidCorpseItemId,
                "인간형 사체",
                "이름과 죽음의 흔적이 남아 있다. 비상 도축은 명시적으로 허용해야 한다.",
                StockCategory.General,
                0,
                null,
                28f,
                1),
            HumanoidMeatItemId => new DungeonItemDefinition(
                HumanoidMeatItemId,
                "금기의 고기",
                "먹을 수는 있지만 누구도 이 선택을 쉽게 잊지 못한다.",
                StockCategory.Food,
                1,
                null,
                0.65f,
                30),
            BoneItemId => new DungeonItemDefinition(
                BoneItemId,
                "인간형 뼈",
                "의식이나 비상 제작에 쓰이는 꺼림칙한 재료다.",
                StockCategory.General,
                1,
                null,
                0.4f,
                50),
            _ => null
        };
        return definition != null;
    }
}
