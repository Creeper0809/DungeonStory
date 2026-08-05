using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class HumanInvasionBranchState
{
    public string branchId = string.Empty;
    public string displayName = string.Empty;
    [Range(0f, 100f)] public float strength = 70f;
    public bool operational = true;
    public float lastRecoveryAmount;
    public string recoveryReason = string.Empty;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class HumanSupportSiteState
{
    public string siteId = string.Empty;
    public string branchId = string.Empty;
    public string displayName = string.Empty;
    public int q;
    public int r;
    public bool alive = true;
    public bool connected = true;
    public int destroyedDay;
    public OffenseHexCoord Coord => new(q, r);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ScheduledInvasionOperationState
{
    public string operationId = string.Empty;
    public InvasionOperationKind kind;
    public string primaryBranchId = string.Empty;
    public List<string> participatingBranchIds = new();
    public string objectiveId = string.Empty;
    public int scheduledDay;
    public float intelligenceConfidence;
}
