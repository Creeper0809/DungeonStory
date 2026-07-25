using System;
using System.Collections.Generic;

[Serializable]
public sealed class DungeonStaffDiscontentSaveData
{
    public List<DungeonStaffDiscontentRecordSaveData> records =
        new List<DungeonStaffDiscontentRecordSaveData>();
}

[Serializable]
public sealed class DungeonStaffDiscontentRecordSaveData
{
    public string staffId = string.Empty;
    public string displayName = string.Empty;
    public StaffDiscontentStage stage = StaffDiscontentStage.Stable;
    public StaffDiscontentOutcome outcome = StaffDiscontentOutcome.None;
    public float mood = 100f;
    public int lowMoodDays;
    public bool permanentLoss;
    public bool departed;
    public bool localRebellion;
    public bool ownerThreat;
    public bool isolated;
    public bool suppressed;
}
