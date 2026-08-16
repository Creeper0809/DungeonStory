using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterAiBranch
{
    None,
    Critical,
    DeprivationBreakdown,
    LockedAction,
    SoftLock,
    InterruptCheck,
    MacroGoal,
    Emergency,
    RoutineUtility,
    ContinueCurrent,
    StopCurrent,
    SurvivalNeeds,
    DutyWork,
    LeisureVisit,
    ExitDungeon,
    Eat,
    Rest,
    Work,
    Shopping,
    LookAround,
    Wait,
    Idle,
    Toilet,
    Hygiene,
    Drink
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterAiInterruptReason
{
    None,
    Critical,
    DestinationInvalid,
    NoPath,
    FacilityUnavailable,
    PatienceExceeded,
    MacroGoalChanged,
    MoodImpulseChanged,
    SurvivalEmergency,
    ManualReplan,
    CurrentActionStopped
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterMoodImpulseType
{
    None,
    FollowRoutine,
    SeekFood,
    SeekRest,
    SeekToilet,
    SeekHygiene,
    SeekFun,
    ImpulseShopping,
    Wander,
    Wait,
    IgnoreDuty,
    AvoidFacility,
    Complain,
    ExitDungeon,
    Vandalize
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterMacroGoalType
{
    None,
    Continue,
    SeekFood,
    SeekToilet,
    SeekHygiene,
    SeekFun,
    AvoidFacility,
    Complain,
    ExitDungeon,
    Vandalize
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterMacroGoal
{
    public CharacterMacroGoalType type = CharacterMacroGoalType.None;
    public string reason;
    public int targetFacilityId = -1;
    public string targetFacilityTag;
    public float validUntil;
    public string source;

    public bool IsActive(float now)
    {
        return type != CharacterMacroGoalType.None
            && (validUntil <= 0f || now <= validUntil);
    }

    public bool IsEquivalentTo(CharacterMacroGoal other)
    {
        if (other == null)
        {
            return type == CharacterMacroGoalType.None;
        }

        return type == other.type
            && targetFacilityId == other.targetFacilityId
            && string.Equals(
                targetFacilityTag,
                other.targetFacilityTag,
                StringComparison.Ordinal);
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterMoodImpulse
{
    public CharacterMoodImpulseType type = CharacterMoodImpulseType.None;
    [Range(0f, 1f)] public float strength;
    public int targetFacilityId = -1;
    public string targetFacilityTag;
    public string reason;
    public float validUntil;
    public string source;

    public bool IsActive(float now)
    {
        return type != CharacterMoodImpulseType.None
            && strength > 0f
            && (validUntil <= 0f || now <= validUntil);
    }

    public bool IsEquivalentTo(CharacterMoodImpulse other)
    {
        if (other == null)
        {
            return type == CharacterMoodImpulseType.None;
        }

        return type == other.type
            && targetFacilityId == other.targetFacilityId
            && string.Equals(
                targetFacilityTag,
                other.targetFacilityTag,
                StringComparison.Ordinal)
            && Mathf.Approximately(strength, other.strength);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterAiIntentionType
{
    None,
    Survive,
    Recover,
    Work,
    Logistics,
    Guard,
    Hunt,
    Leisure,
    Social,
    Shop,
    Exit,
    Idle
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterAiUtilityFactorKind
{
    Need,
    Priority,
    Personality,
    Memory,
    Distance,
    Risk,
    Room,
    Stock,
    Crowd,
    Reservation,
    Momentum,
    Queue,
    Social,
    Weather,
    PathConfidence,
    Fatigue,
    Novelty,
    Schedule
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum AIActionFailureKind
{
    None,
    NoAction,
    Cooldown,
    PathSearchDeferred,
    CannotStart,
    NoScore,
    NoDestination,
    DestinationSelectionFailed,
    DestinationOccupied,
    NoPath,
    NoGrid,
    NoWork,
    OffDuty,
    Unsupported,
    Destroyed,
    Unknown,

    // Appended to preserve the serialized numeric values of the legacy kinds.
    CandidateEvaluationDeferred,
    FacilityCandidateDeferred,
    FacilityAdmissionRejected,
    FacilityServiceUnavailable,
    ResourceUnavailable,
    ConsumptionFailed,
    PathSearchStarved
}

public static class CharacterAiActionTags
{
    public const string Work = "ai:work";
    public const string Curiosity = "ai:curiosity";
    public const string SelfCare = "ai:self-care";
    public const string Shopping = "ai:shopping";
    public const string Patience = "ai:patience";
    public const string Exit = "ai:exit";
}
