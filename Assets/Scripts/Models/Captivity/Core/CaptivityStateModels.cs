using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CaptivityStatus
{
    None, AwaitingCapture, Stabilizing, AwaitingEscort, Escorting, Confined,
    Labor, Interaction, Performer, EscapeAttempt, Ransom, Recruited, Minion,
    Released, Escaped, Dead
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CaptiveInteractionKind
{
    Persuasion, Isolation, Coercion, Interrogation, Indoctrination, Branding,
    BloodExtraction, MemoryExtraction, ForcedModification, CorruptionRitual
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CaptivePerformerMilestoneChoice
{
    None = 0,
    StaffContract = 1,
    ReleaseNegotiation = 2,
    ExclusiveFighterContract = 3
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CaptivityPolicyIds
{
    public const string Standard = "captivity:standard";
    public const string ForcedLabor = "captivity:forced-labor";
    public const string Performer = "captivity:performer";
    public const string Corruption = "captivity:corruption";

    public static bool IsBuiltIn(string policyId) => policyId is Standard
        or ForcedLabor
        or Performer
        or Corruption;
}

[Flags]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CaptiveLaborPermission
{
    None = 0,
    Clean = 1 << 0,
    Haul = 1 << 1,
    DrawWater = 1 << 2,
    Refuel = 1 << 3,
    Construct = 1 << 4,
    Repair = 1 << 5,
    Butcher = 1 << 6,
    CraftAssist = 1 << 7,
    All = Clean | Haul | DrawWater | Refuel | Construct | Repair | Butcher | CraftAssist
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivePolicyData
{
    public string policyId = CaptivityPolicyIds.Standard;
    public string displayName = "표준 수용";
    public CaptiveLaborPermission allowedLabor =
        CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul;
    public bool allowRansom = true;
    public bool allowRecruitment = true;
    public bool allowCorruption;
    public bool allowPerformance;

    public CaptivePolicyData Clone() => (CaptivePolicyData)MemberwiseClone();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptiveState
{
    public string captiveId = string.Empty;
    public string displayName = string.Empty;
    public string speciesTag = string.Empty;
    public CaptivityStatus status = CaptivityStatus.AwaitingCapture;
    public string policyId = CaptivityPolicyIds.Standard;
    public string reservedCarrierId = string.Empty;
    public string reservedWardenId = string.Empty;
    public string housingBuildingId = string.Empty;
    public string restraintStackId = string.Empty;
    public string restraintItemId = string.Empty;
    public int restraintQuantity;
    public Vector2Int restraintPickupPosition;
    public string assignedRestraintItemId = string.Empty;
    public string assignedRestraintInstanceId = string.Empty;
    [Min(0f)] public float assignedRestraintDurability;
    [Min(0f)] public float assignedRestraintMaximumDurability;
    public Vector2Int capturePosition;
    public Vector2Int housingPosition;
    public Vector2Int escapeDestination;
    [Range(0f, 100f)] public float will = 100f;
    [Range(0f, 100f)] public float fear;
    [Range(0f, 100f)] public float trust;
    [Range(0f, 100f)] public float grudge;
    [Range(0f, 100f)] public float corruption;
    [Range(0f, 100f)] public float compliance;
    [Range(0f, 100f)] public float escapeRisk = 30f;
    [Range(0f, 100f)] public float health = 40f;
    public bool falseCompliance;
    public bool equipmentConfiscated;
    public bool stabilized;
    public bool restrained;
    public CaptiveLaborPermission laborPermissions;
    public CaptiveLaborPermission pendingLaborPermissions;
    public string laborToolDestinationId = string.Empty;
    public string assignedLaborToolItemId = string.Empty;
    public string assignedLaborToolInstanceId = string.Empty;
    [Min(0f)] public float assignedLaborToolDurability;
    [Min(0f)] public float assignedLaborToolMaximumDurability;
    public string laborToolAssignmentOperationId = string.Empty;
    public string laborToolAssignmentCommitId = string.Empty;
    public string laborToolAssignmentSourceStackId = string.Empty;
    public bool laborToolAssignmentCompleted;
    [Min(0f)] public float nextLaborToolWearAt;
    public string currentInteractionId = string.Empty;
    public string interactionMaterialDestinationId = string.Empty;
    public bool interactionMaterialsConsumed;
    public float completedInteractionWork;
    public float requiredInteractionWork;
    public string lastResult = string.Empty;
    public float performerSkill;
    public float performerFame;
    public int performerInjuries;
    public int privilegeTier;
    public bool carePriorityUnlocked;
    public float nextCareSupplyAt;
    public bool staffContractUnlocked;
    public bool finalContractPending;
    public bool exclusiveFighter;
    public CaptivePerformerMilestoneChoice resolvedMilestoneChoice;
    public int failedEscapeAttempts;
    public float nextSecurityCheckAt;
    [Range(0f, 100f)] public float retaliationPressure;
    public string betrayalTrigger = string.Empty;

    public bool IsActive => status is not CaptivityStatus.Recruited
        and not CaptivityStatus.Released
        and not CaptivityStatus.Escaped
        and not CaptivityStatus.Dead;
    public bool CanLabor => compliance >= 50f && health >= 40f
        && status is CaptivityStatus.Confined or CaptivityStatus.Labor;
    public bool CanRecruit => trust >= 70f && grudge <= 30f && corruption < 60f;
    public bool CanBecomeMinion => corruption >= 80f;
    public int RansomValue => Mathf.Max(
        50,
        Mathf.RoundToInt(
            60f
            + health * 0.8f
            + performerFame * 1.5f
            + (100f - will) * 0.25f));
    public CaptiveState Clone() => (CaptiveState)MemberwiseClone();
}

public static class CaptivityLaborToolAssignmentIdentity
{
    public static string FormatOperationId(
        string captiveId,
        string itemInstanceId) =>
        $"captive-labor-tool-assign:{captiveId ?? string.Empty}:{itemInstanceId ?? string.Empty}";
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivitySaveData
{
    public const int CurrentVersion = 3;
    public int version = CurrentVersion;
    public int captureSequence;
    public int policySequence;
    public List<CaptiveState> captives = new List<CaptiveState>();
    public List<CaptivePolicyData> policies = new List<CaptivePolicyData>();
}
