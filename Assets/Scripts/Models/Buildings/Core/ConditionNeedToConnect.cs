using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

[MovedFrom(true, null, "Assembly-CSharp", "ConditionNeedToConnect")]
public class ConditionNeedToConnect : IBuildingCondition
{
    [FormerlySerializedAs("connectWithGate")]
    public bool connectWithEntrance;
    [HideIf("connectWithEntrance")]
    public int associatedId;

    public void OnBuild(IBuildingConditionContextPort context)
    {
    }

    public bool IsSatisfy(
        IBuildingConnectivityQueryPort connectivity,
        List<Vector2Int> buildPos,
        IBuildingConditionContextPort context,
        out string errorMessage)
    {
        if (connectivity == null || buildPos == null || buildPos.Count == 0)
        {
            errorMessage = "건물 연결 조건을 확인할 수 없습니다";
            return false;
        }

        if (connectWithEntrance)
        {
            bool connected = connectivity.IsConnectedWithAny(buildPos);
            errorMessage = connected ? string.Empty : "건물이 입구와 연결되지 않았습니다.";
            return connected;
        }

        bool associated = connectivity.IsConnected(buildPos[0], associatedId);
        errorMessage = associated ? string.Empty : "건물 연결 조건을 만족하지 못했습니다";
        return associated;
    }
}
