using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum DefenseEngagementState
{
    Dispatching,
    InterceptPlanned,
    Engaged,
    ReserveWaiting,
    Switching,
    Retreating,
    FrontCollapsed,
    Completed
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum DefenseResponsePolicyKind
{
    Standard,
    SurvivalFirst,
    HoldTheLine,
    Custom
}

public static class DefenseResponsePolicyIds
{
    public const string Standard = "defense-policy:standard";
    public const string SurvivalFirst = "defense-policy:survival-first";
    public const string HoldTheLine = "defense-policy:hold-the-line";
    public const string CustomPrefix = "defense-policy:custom:";
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class HumanInvasionBranchIds
{
    public const string RoyalArmy = "human-branch:royal-army";
    public const string PioneerSupply = "human-branch:pioneer-supply";
    public const string RoyalOrdnance = "human-branch:royal-ordnance";
    public const string IntelligenceHunters = "human-branch:intelligence-hunters";
    public const string RadiantOrder = "human-branch:radiant-order";
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum InvasionOperationKind
{
    FrontalAssault = 0,
    Siege = 1,
    FacilitySabotage = 2,
    Loot = 3,
    CaptiveRescue = 4,
    OwnerAssassination = 5
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum InvasionIntruderState
{
    None = 0,
    Entering = 1,
    Searching = 2,
    MovingToOwner = 3,
    MovingToFacility = 4,
    DamagingFacility = 5,
    InterceptPlanned = 6,
    Engaged = 7,
    FrontBroken = 8,
    FinalCombat = 9,
    Finished = 10,
    Rallying = 11,
    Breaching = 12
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DefenseKnownRiskSaveData
{
    public int x;
    public int y;
    public float severity;
    public string facilityBuildingInstanceId = string.Empty;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DefenseExpectedPathCellSaveData
{
    public int x;
    public int y;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DefenseRaidAwarenessSaveData
{
    public string raidId = string.Empty;
    public int identificationStage;
    public string routeChangeReason = string.Empty;
    public string breachTargetBuildingInstanceId = string.Empty;
    public List<DefenseKnownRiskSaveData> knownRisks =
        new List<DefenseKnownRiskSaveData>();
    public List<DefenseExpectedPathCellSaveData> expectedPath =
        new List<DefenseExpectedPathCellSaveData>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DefenseResponsePolicyData
{
    public string id = string.Empty;
    public string displayName = string.Empty;
    public DefenseResponsePolicyKind kind = DefenseResponsePolicyKind.Custom;
    public bool autoRespond = true;
    [Range(0f, 1f)] public float minimumDispatchHealthRatio = 0.4f;
    [Range(0f, 1f)] public float retreatHealthRatio = 0.2f;
    public bool holdWithoutReplacement = true;
    [Range(0f, 1f)] public float rejoinHealthRatio = 0.6f;

    public DefenseResponsePolicyData Clone()
    {
        return new DefenseResponsePolicyData
        {
            id = id,
            displayName = displayName,
            kind = kind,
            autoRespond = autoRespond,
            minimumDispatchHealthRatio =
                Mathf.Clamp01(minimumDispatchHealthRatio),
            retreatHealthRatio = Mathf.Clamp01(retreatHealthRatio),
            holdWithoutReplacement = holdWithoutReplacement,
            rejoinHealthRatio = Mathf.Clamp01(rejoinHealthRatio)
        };
    }

    public void Normalize()
    {
        id = id?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
        minimumDispatchHealthRatio =
            Mathf.Clamp01(minimumDispatchHealthRatio);
        retreatHealthRatio = Mathf.Clamp01(retreatHealthRatio);
        rejoinHealthRatio = Mathf.Clamp(
            rejoinHealthRatio,
            minimumDispatchHealthRatio,
            1f);
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DefenseResponsePolicySaveSnapshot
{
    public List<DefenseResponsePolicyData> policies =
        new List<DefenseResponsePolicyData>();
    public List<DefensePolicyAssignmentSaveData> assignments =
        new List<DefensePolicyAssignmentSaveData>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DefensePolicyAssignmentSaveData
{
    public string characterId = string.Empty;
    public string policyId = string.Empty;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DefenseEngagementSaveSnapshot
{
    public List<DefenseEngagementSaveData> engagements =
        new List<DefenseEngagementSaveData>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DefenseEngagementSaveData
{
    public string id = string.Empty;
    public string intruderId = string.Empty;
    public string leadGuardId = string.Empty;
    public string reserveGuardId = string.Empty;
    public string rangedGuardId = string.Empty;
    public string secondaryRangedGuardId = string.Empty;
    public DefenseEngagementState state;
    public int intruderStopX;
    public int intruderStopY;
    public int guardX;
    public int guardY;
    public int reserveX;
    public int reserveY;
    public int rangedX;
    public int rangedY;
    public int secondaryRangedX;
    public int secondaryRangedY;
    public bool hasReserveCell;
    public bool ownerFinalDefense;
    public bool forced;
    public float guardAttackRemaining;
    public float intruderAttackRemaining;
    public float rangedAttackRemaining;
    public float secondaryRangedAttackRemaining;
    public int exchangeCount;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OwnerEvacuationSaveSnapshot
{
    public bool active;
    public int targetX;
    public int targetY;
    public bool usedAdministrationRoom;
    public string statusText = string.Empty;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum InvasionThreatDifficulty
{
    Easy,
    Normal,
    Hard
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum InvasionThreatStage
{
    Peaceful,
    Warning,
    Candidate,
    Safety
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class InvasionThreatSettings
{
    public const float DefaultInitialSafetyDurationSeconds = 180f;

    [Header("Thresholds")]
    [Range(1f, 100f)] public float warningThreshold = 70f;
    [Range(1f, 200f)] public float candidateThreshold = 100f;
    [Min(0f)] public float warningCooldownSeconds = 45f;
    [Min(0f)] public float initialSafetyDurationSeconds = 180f;
    [Min(0f)] public float safetyDurationSeconds = 30f;
    [Min(0f)] public float minCandidateDelaySeconds = 5f;
    [Min(0f)] public float maxCandidateDelaySeconds = 12f;

    [Header("Base Rise")]
    [Min(0f)] public float baseRisePerSecond = 0.025f;
    [Min(0f)] public float dungeonValueRiseWeight = 0.012f;
    [Min(0f)] public float reputationRiseWeight = 0.018f;
    [Min(0f)] public float timeRiseWeight = 0.01f;
    [Min(0f)] public float riskRiseWeight = 0.04f;

    [Header("Difficulty")]
    public InvasionThreatDifficulty difficulty =
        InvasionThreatDifficulty.Normal;
    [Min(0f)] public float easyMultiplier = 0.65f;
    [Min(0f)] public float normalMultiplier = 1f;
    [Min(0f)] public float hardMultiplier = 1.45f;

    public float GetDifficultyMultiplier()
    {
        return difficulty switch
        {
            InvasionThreatDifficulty.Easy => easyMultiplier,
            InvasionThreatDifficulty.Hard => hardMultiplier,
            _ => normalMultiplier
        };
    }

    public float GetCandidateDelay(IRandomStream randomStream)
    {
        if (randomStream == null)
        {
            throw new ArgumentNullException(nameof(randomStream));
        }

        float min = Mathf.Max(0f, minCandidateDelaySeconds);
        float max = Mathf.Max(min, maxCandidateDelaySeconds);
        return Mathf.Lerp(min, max, randomStream.NextFloat());
    }

    public float GetInitialSafetyDuration()
    {
        return initialSafetyDurationSeconds > 0f
            ? initialSafetyDurationSeconds
            : DefaultInitialSafetyDurationSeconds;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct InvasionThreatFactors
{
    public readonly float dungeonValue;
    public readonly float reputation;
    public readonly float time;
    public readonly float risk;

    public InvasionThreatFactors(
        float dungeonValue,
        float reputation,
        float time,
        float risk)
    {
        this.dungeonValue = Mathf.Max(0f, dungeonValue);
        this.reputation = Mathf.Max(0f, reputation);
        this.time = Mathf.Max(0f, time);
        this.risk = Mathf.Max(0f, risk);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct InvasionThreatSnapshot
{
    public readonly float threat;
    public readonly InvasionThreatStage stage;
    public readonly InvasionThreatFactors factors;
    public readonly float pendingDelayRemaining;
    public readonly float safetyRemaining;

    public InvasionThreatSnapshot(
        float threat,
        InvasionThreatStage stage,
        InvasionThreatFactors factors,
        float pendingDelayRemaining,
        float safetyRemaining)
    {
        this.threat = threat;
        this.stage = stage;
        this.factors = factors;
        this.pendingDelayRemaining = Mathf.Max(0f, pendingDelayRemaining);
        this.safetyRemaining = Mathf.Max(0f, safetyRemaining);
    }
}
