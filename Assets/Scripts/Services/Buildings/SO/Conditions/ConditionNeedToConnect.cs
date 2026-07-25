using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ConditionNeedToConnect : IBuildingCondition
{
    [FormerlySerializedAs("connectWithGate")]
    public bool connectWithEntrance;
    [HideIf("connectWithEntrance")]
    public int associatedId;

    public void OnBuild(BuildingConditionContext context) {}

    public bool IsSatisfy(
        Grid grid,
        List<Vector2Int> buildPos,
        BuildingConditionContext context,
        out string errorMessage)
    {
        if (grid == null || buildPos == null || buildPos.Count == 0)
        {
            errorMessage = "건물 연결 조건을 확인할 수 없습니다";
            return false;
        }

        bool isSatisfied = false;
        errorMessage = string.Empty;
        if (connectWithEntrance)
        {
            if (grid.IsConnectedWithAny(buildPos)) isSatisfied = true;
            else errorMessage = "건물이 입구와 연결되지 않았습니다.";
        }
        else
        {
            if (grid.IsConnected(buildPos[0], associatedId)) isSatisfied = true;
            else errorMessage = "건물 연결 조건을 만족하지 못했습니다";
        }

        return isSatisfied;
    }
}
