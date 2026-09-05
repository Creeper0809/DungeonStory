using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CaptivityStatus
{
    None = 0,
    AwaitingCapture = 1,
    Stabilizing = 2,
    AwaitingEscort = 3,
    Escorting = 4,
    Confined = 5,
    Labor = 6,
    Interaction = 7,
    Performer = 8,
    EscapeAttempt = 9,
    Ransom = 10,
    Recruited = 11,
    Minion = 12,
    Released = 13,
    Escaped = 14,
    Dead = 15
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
    [Min(0)] public int capturedAbsoluteDay;
    [Min(0)] public int rehabilitationDays;
    public int lastRehabilitationAbsoluteDay = -1;
    [Min(0f)] public float completedRehabilitationWork;
    public bool rehabilitationInProgress;
    public string rehabilitationFacilityBuildingId = string.Empty;
    public Vector2Int rehabilitationPosition;
    public int lastMinionSocialAbsoluteDay = -1;

    public bool IsInCustody => status is CaptivityStatus.AwaitingCapture
        or CaptivityStatus.Stabilizing
        or CaptivityStatus.AwaitingEscort
        or CaptivityStatus.Escorting
        or CaptivityStatus.Confined
        or CaptivityStatus.Labor
        or CaptivityStatus.Interaction
        or CaptivityStatus.Performer
        or CaptivityStatus.EscapeAttempt;
    public bool IsMinion => status == CaptivityStatus.Minion;
    public bool IsTerminal => status is CaptivityStatus.Ransom
        or CaptivityStatus.Recruited
        or CaptivityStatus.Released
        or CaptivityStatus.Escaped
        or CaptivityStatus.Dead;
    [Obsolete("Use IsInCustody, IsMinion, or IsTerminal explicitly.")]
    public bool IsActive => IsInCustody;
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

public static class CaptivityStateTransitionRules
{
    public const float RehabilitationRequiredWork = 18f;

    public static string CaptureStateSnapshot(CaptiveState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }
        return JsonUtility.ToJson(state);
    }

    public static void RestoreStateSnapshot(
        string snapshotJson,
        CaptiveState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            throw new ArgumentException(
                "A captivity-state snapshot is required.",
                nameof(snapshotJson));
        }
        JsonUtility.FromJsonOverwrite(snapshotJson, state);
    }

    public static void ClearCaptiveOnlyState(CaptiveState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.reservedCarrierId = string.Empty;
        if (!state.IsMinion || !state.rehabilitationInProgress)
        {
            state.reservedWardenId = string.Empty;
        }
        state.housingBuildingId = string.Empty;
        state.restraintStackId = string.Empty;
        state.restraintItemId = string.Empty;
        state.restraintQuantity = 0;
        state.assignedRestraintItemId = string.Empty;
        state.assignedRestraintInstanceId = string.Empty;
        state.assignedRestraintDurability = 0f;
        state.assignedRestraintMaximumDurability = 0f;
        state.restrained = false;
        state.laborPermissions = CaptiveLaborPermission.None;
        state.pendingLaborPermissions = CaptiveLaborPermission.None;
        state.laborToolDestinationId = string.Empty;
        state.assignedLaborToolItemId = string.Empty;
        state.assignedLaborToolInstanceId = string.Empty;
        state.assignedLaborToolDurability = 0f;
        state.assignedLaborToolMaximumDurability = 0f;
        state.laborToolAssignmentOperationId = string.Empty;
        state.laborToolAssignmentCommitId = string.Empty;
        state.laborToolAssignmentSourceStackId = string.Empty;
        state.laborToolAssignmentCompleted = false;
        state.nextLaborToolWearAt = 0f;
        state.currentInteractionId = string.Empty;
        state.interactionMaterialDestinationId = string.Empty;
        state.interactionMaterialsConsumed = false;
        state.completedInteractionWork = 0f;
        state.requiredInteractionWork = 0f;
        state.carePriorityUnlocked = false;
        state.nextCareSupplyAt = 0f;
    }

    public static void ClearRehabilitationState(CaptiveState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.rehabilitationInProgress = false;
        state.rehabilitationFacilityBuildingId = string.Empty;
        state.rehabilitationPosition = default;
        state.completedRehabilitationWork = 0f;
        state.reservedWardenId = string.Empty;
    }
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
