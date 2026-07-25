using System;
using System.Collections.Generic;

[Serializable]
public sealed class DungeonRegularCustomerSaveData
{
    public List<DungeonRegularCustomerRecordSaveData> records =
        new List<DungeonRegularCustomerRecordSaveData>();
}

[Serializable]
public sealed class DungeonRegularCustomerRecordSaveData
{
    public string customerId = string.Empty;
    public string displayName = string.Empty;
    public string speciesTag = string.Empty;
    public int sourceDataId = -1;
    public int visitCount;
    public float averageSatisfaction;
    public bool isRegular;
    public bool isRecruitCandidate;
    public bool isRecruited;
    public RecruitCapability recruitCapabilities;
}
