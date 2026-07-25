using System.Collections.Generic;
using UnityEngine;

public class ConditionNeedMoney : IBuildingCondition
{
    [SerializeField]private int cost;

    public void OnBuild(BuildingConditionContext context)
    {
        if (DungeonDebugRuntimeRules.ShouldSkipCosts()
            || DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.FreeConstruction))
        {
            return;
        }

        GameData gameData = context.GameData;
        if (gameData == null) return;

        gameData.holdingMoney.Value -= cost;
    }

    public bool IsSatisfy(
        Grid grid,
        List<Vector2Int> buildPos,
        BuildingConditionContext context,
        out string errorMessage)
    {
        if (DungeonDebugRuntimeRules.ShouldSkipCosts()
            || DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.FreeConstruction))
        {
            errorMessage = string.Empty;
            return true;
        }

        GameData gameData = context.GameData;
        if (gameData == null)
        {
            errorMessage = "게임 데이터가 초기화되지 않았습니다";
            return false;
        }

        if (cost > gameData.holdingMoney.Value)
        {
            errorMessage = "소지중인 돈이 부족합니다";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

}
