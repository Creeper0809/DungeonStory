using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public class ConditionNeedMoney : IBuildingCondition
{
    [SerializeField]private int cost;

    public void OnBuild(IBuildingConditionContextPort context)
    {
        if (context.ShouldSkipConstructionCosts)
        {
            return;
        }

        context.TrySpendConstruction(cost);
    }

    public bool IsSatisfy(
        IBuildingConnectivityQueryPort connectivity,
        List<Vector2Int> buildPos,
        IBuildingConditionContextPort context,
        out string errorMessage)
    {
        if (context.ShouldSkipConstructionCosts)
        {
            errorMessage = string.Empty;
            return true;
        }

        if (!context.CanSpendConstruction(cost))
        {
            errorMessage = "소지중인 돈이 부족합니다";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

}
