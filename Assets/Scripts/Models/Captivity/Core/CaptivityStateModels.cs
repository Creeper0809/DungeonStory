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
public sealed class CaptivityInterrogationTerminalState
{
    [Min(0)] public int attemptId;
    public string subjectDisplayName = string.Empty;
    public bool hasInformation;
    public string codexEntryId = string.Empty;
    public string codexEntryTitle = string.Empty;
    public string originEnemyArchetypeId = string.Empty;
    public string originFactionId = string.Empty;
    public string enemyDisplayName = string.Empty;
    public string formationTag = string.Empty;
    public string informationText = string.Empty;
    public bool highFearCaution;
    public bool codexPublicationCompleted;
    public bool noticePublicationCompleted;

    public bool HasOutcome => attemptId > 0;
    public bool HasPendingPublication => HasOutcome
        && !noticePublicationCompleted;

    public CaptivityInterrogationTerminalState Clone() =>
        (CaptivityInterrogationTerminalState)MemberwiseClone();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityInteractionTerminalState
{
    [Min(0)] public int attemptId;
    [Min(0)] public long outcomeRevision;
    public string interactionId = string.Empty;
    public CaptiveInteractionKind interactionKind;
    public string interactionDisplayName = string.Empty;
    public string wardenId = string.Empty;
    public string wardenDisplayName = string.Empty;
    public string facilityId = string.Empty;
    public string facilityDisplayName = string.Empty;
    public int resultGridX;
    public int resultGridY;
    public bool success;
    public string message = string.Empty;
    public float willBefore;
    public float willAfter;
    public float fearBefore;
    public float fearAfter;
    public float trustBefore;
    public float trustAfter;
    public float grudgeBefore;
    public float grudgeAfter;
    public float corruptionBefore;
    public float corruptionAfter;
    public string outputItemId = string.Empty;
    [Min(0)] public int outputAmount;
    public string outputOperationId = string.Empty;
    public string outputCommitId = string.Empty;
    public bool outputPublished;
    [Min(0f)] public float bodyDamageAmount;
    [Min(0f)] public float bodyHealthBefore;
    [Min(0f)] public float bodyHealthAfter;
    [Min(0f)] public float bodyMaximumHealth;
    public bool bodyObserversPending;

    public bool HasOutcome => attemptId > 0;
    public bool HasCommittedOutcome => HasOutcome && outcomeRevision > 0L;
    public bool HasOutput => outputAmount > 0;
    public Vector2Int ResultPosition => new(resultGridX, resultGridY);

    public CaptivityInteractionTerminalState Clone() =>
        (CaptivityInteractionTerminalState)MemberwiseClone();
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
    [Min(0)] public int interactionAttemptSequence;
    [Min(0)] public int currentInteractionAttemptId;
    [Min(0)] public long interactionOutcomeRevision;
    public CaptivityInteractionTerminalState interactionTerminal = new();
    [Min(0)] public int interrogationAttemptSequence;
    [Min(0)] public int currentInterrogationAttemptId;
    public CaptivityInterrogationTerminalState interrogationTerminal = new();
    public string lastResult = string.Empty;
    public float performerSkill;
    public float performerFame;
    public int performerInjuries;
    public int privilegeTier;
    public bool carePriorityUnlocked;
    public float nextCareSupplyAt;
    public bool staffContractUnlocked;
    public bool finalContractPending;
    [Min(0)] public long performerMilestoneOutcomeRevision;
    [Min(0)] public long carePriorityOutcomeRevision;
    [Min(0)] public long staffContractOutcomeRevision;
    [Min(0)] public long finalContractOutcomeRevision;
    [Min(0)] public long escapeOutcomeRevision;
    [Min(0)] public long ransomOutcomeRevision;
    [Min(0)] public int ransomAcceptedAmount;
    public bool ransomIncomeCredited;
    public bool ransomObserversPending;
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
    public bool CanLaborWithBody(float healthPercent, bool bodyAvailable) =>
        bodyAvailable
        && compliance >= 50f
        && CaptivityBodyHealthRules.NormalizeHealthPercent(healthPercent) >= 40f
        && status is CaptivityStatus.Confined or CaptivityStatus.Labor;
    public bool CanRecruit => trust >= 70f && grudge <= 30f && corruption < 60f;
    public bool CanBecomeMinion => corruption >= 80f;
    public int CalculateRansomValue(float healthPercent) => Mathf.Max(
        50,
        Mathf.RoundToInt(
            60f
            + CaptivityBodyHealthRules.NormalizeHealthPercent(healthPercent) * 0.8f
            + performerFame * 1.5f
            + (100f - will) * 0.25f));
    public CaptiveState Clone()
    {
        CaptiveState clone = (CaptiveState)MemberwiseClone();
        clone.interactionTerminal = interactionTerminal?.Clone();
        clone.interrogationTerminal = interrogationTerminal?.Clone();
        return clone;
    }
}

public static class CaptivityBodyHealthRules
{
    public static float NormalizeHealthPercent(float healthPercent)
    {
        return float.IsNaN(healthPercent) || float.IsInfinity(healthPercent)
            ? 0f
            : Mathf.Clamp(healthPercent, 0f, 100f);
    }

    public static float GetHealthPercent(
        float currentHealth,
        float maximumHealth) =>
        NormalizeHealthPercent(
            currentHealth / Mathf.Max(1f, maximumHealth) * 100f);

    public static bool IsBodyAvailable(
        bool actorDead,
        bool vitalsDead,
        bool downed) =>
        !actorDead && !vitalsDead && !downed;
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
        state.currentInteractionAttemptId = 0;
        state.interactionTerminal = new CaptivityInteractionTerminalState();
        state.currentInterrogationAttemptId = 0;
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

public static class CaptivityInterrogationAttemptIdentity
{
    public const string InteractionId = "captivity:interrogation";
    public const float HighFearCautionThreshold = 75f;

    public static string FormatNoticeSourceId(
        string captiveId,
        int attemptId) =>
        $"captivity:interrogation:{captiveId ?? string.Empty}:{attemptId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static string FormatInformationText(
        string originEnemyArchetypeId,
        string originFactionId,
        string formationTag) =>
        "미확인 진술: 출신 부대 "
        + $"{originEnemyArchetypeId ?? string.Empty} / 세력 "
        + $"{originFactionId ?? string.Empty} / 전열·전술 표식 "
        + $"'{formationTag ?? string.Empty}'";
}

public static class CaptivityInteractionAttemptIdentity
{
    public static string FormatOutputOperationId(
        string captiveId,
        int attemptId) =>
        $"captivity-interaction-output:{captiveId ?? string.Empty}:{attemptId:D8}";
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivitySaveData
{
    public const int CurrentVersion = 5;
    public int version = CurrentVersion;
    public int captureSequence;
    public int policySequence;
    public List<CaptiveState> captives = new List<CaptiveState>();
    public List<CaptivePolicyData> policies = new List<CaptivePolicyData>();
}
