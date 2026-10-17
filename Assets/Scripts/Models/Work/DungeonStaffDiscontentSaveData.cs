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
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;
    public long outcomeRevision;
    public long transitionRevision;
    public StaffDiscontentObservationData responseObservation = new();
    public StaffDiscontentObservationData transitionObservation = new();
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
    public int localRebellionDays;
    public int lastProcessedAbsoluteDay = -1;
    public bool permanentLoss;
    public bool departed;
    public bool localRebellion;
    public bool ownerThreat;
    public bool isolated;
    public bool suppressed;

    public DungeonStaffDiscontentRecordSaveData Clone() => (DungeonStaffDiscontentRecordSaveData)MemberwiseClone();
}

public enum StaffDiscontentFactKind { DailyTransition, Response }

[Serializable]
public sealed class StaffDiscontentFactData
{
    public StaffDiscontentFactKind kind;
    public long revision;
    public int absoluteDay;
    public StaffDiscontentOutcome transition;
    public StaffRebellionResponseType responseType;
    public string responderId = string.Empty;
    public string responderDisplayName = string.Empty;
    public bool moodApplied;
    public float resolvedMoodDelta;
    public DungeonStaffDiscontentRecordSaveData before;
    public DungeonStaffDiscontentRecordSaveData after;

    public StaffDiscontentFactData Clone()
    {
        var clone = (StaffDiscontentFactData)MemberwiseClone();
        clone.before = before?.Clone(); clone.after = after?.Clone();
        return clone;
    }
}

[Serializable]
public sealed class StaffDiscontentObservationData
{
    // JsonUtility materializes inline null classes. Persist an explicit empty value instead.
    public StaffDiscontentFactData fact = new()
    {
        before = new DungeonStaffDiscontentRecordSaveData(),
        after = new DungeonStaffDiscontentRecordSaveData()
    };
    public string publicationKey = string.Empty;
    public bool pending;
    public bool notificationPending;
    public StaffDiscontentObservationData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey, pending = pending, notificationPending = notificationPending };
}
