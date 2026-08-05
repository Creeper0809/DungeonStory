using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum StaffDiscontentStage
{
    Stable,
    LowSatisfaction,
    EfficiencyDrop,
    WorkDisruption,
    Departure,
    LocalRebellion
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum StaffDiscontentOutcome
{
    None,
    Warning,
    EfficiencyPenalty,
    WorkDisruption,
    PermanentDeparture,
    LocalRebellion,
    OwnerThreat
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum StaffRebellionResponseType
{
    AutoSuppress,
    SuppressCommand,
    Isolate,
    Calm
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public class StaffDiscontentRules
{
    public float lowMoodThreshold = 50f;
    public float efficiencyDropMoodThreshold = 35f;
    public float workDisruptionMoodThreshold = 25f;
    public float departureMoodThreshold = 15f;
    public float rebellionMoodThreshold = 8f;
    public int sustainedLowMoodForEfficiencyDrop = 2;
    public int sustainedLowMoodForWorkDisruption = 3;
    public int sustainedLowMoodForDeparture = 4;
    public int ownerThreatEscalationDays = 2;
    public float calmMoodRecovery = 25f;
    public float lowSatisfactionMultiplier = 0.95f;
    public float efficiencyDropMultiplier = 0.8f;
    public float workDisruptionMultiplier = 0.6f;

    public static StaffDiscontentRules CreateDefault() =>
        new StaffDiscontentRules();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonStaffDiscontentSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public List<DungeonStaffDiscontentRecordSaveData> records =
        new List<DungeonStaffDiscontentRecordSaveData>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
