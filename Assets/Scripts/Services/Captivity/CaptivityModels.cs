using System;
using System.Collections.Generic;
using UnityEngine;

public enum CaptivityStatus
{
    None,
    AwaitingCapture,
    Stabilizing,
    AwaitingEscort,
    Escorting,
    Confined,
    Labor,
    Interaction,
    Performer,
    EscapeAttempt,
    Ransom,
    Recruited,
    Minion,
    Released,
    Escaped,
    Dead
}

public enum CaptiveInteractionKind
{
    Persuasion,
    Isolation,
    Coercion,
    Interrogation,
    Indoctrination,
    Branding,
    BloodExtraction,
    MemoryExtraction,
    ForcedModification,
    CorruptionRitual
}

public enum CaptivePerformerMilestoneChoice
{
    None = 0,
    StaffContract = 1,
    ReleaseNegotiation = 2,
    ExclusiveFighterContract = 3
}

[Flags]
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
public sealed class CaptivePolicyData
{
    public string policyId = "captivity:standard";
    public string displayName = "표준 수용";
    public CaptiveLaborPermission allowedLabor =
        CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul;
    public bool allowRansom = true;
    public bool allowRecruitment = true;
    public bool allowCorruption;
    public bool allowPerformance;

    public CaptivePolicyData Clone()
    {
        return (CaptivePolicyData)MemberwiseClone();
    }
}

[Serializable]
public sealed class CaptiveState
{
    public string captiveId = string.Empty;
    public string displayName = string.Empty;
    public string speciesTag = string.Empty;
    public CaptivityStatus status = CaptivityStatus.AwaitingCapture;
    public string policyId = "captivity:standard";
    public string reservedCarrierId = string.Empty;
    public string reservedWardenId = string.Empty;
    public string housingBuildingId = string.Empty;
    public string restraintStackId = string.Empty;
    public string restraintItemId = string.Empty;
    public int restraintQuantity;
    public Vector2Int restraintPickupPosition;
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

    public bool IsActive =>
        status is not CaptivityStatus.Recruited
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

    public CaptiveState Clone()
    {
        return (CaptiveState)MemberwiseClone();
    }
}

[Serializable]
public sealed class CaptivitySaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public int captureSequence;
    public int policySequence;
    public List<CaptiveState> captives = new List<CaptiveState>();
    public List<CaptivePolicyData> policies = new List<CaptivePolicyData>();
}

public readonly struct CaptivityInteractionContext
{
    public CaptivityInteractionContext(
        CaptiveState captive,
        CharacterActor subject,
        CharacterActor warden,
        BuildableObject facility,
        Vector2Int resultPosition)
    {
        Captive = captive;
        Subject = subject;
        Warden = warden;
        Facility = facility;
        ResultPosition = resultPosition;
    }

    public CaptiveState Captive { get; }
    public CharacterActor Subject { get; }
    public CharacterActor Warden { get; }
    public BuildableObject Facility { get; }
    public Vector2Int ResultPosition { get; }
}

public readonly struct CaptivityInteractionResult
{
    public CaptivityInteractionResult(
        bool success,
        string message,
        float willDelta = 0f,
        float fearDelta = 0f,
        float trustDelta = 0f,
        float grudgeDelta = 0f,
        float corruptionDelta = 0f,
        float healthDelta = 0f,
        string outputItemId = "",
        int outputAmount = 0)
    {
        Success = success;
        Message = message ?? string.Empty;
        WillDelta = willDelta;
        FearDelta = fearDelta;
        TrustDelta = trustDelta;
        GrudgeDelta = grudgeDelta;
        CorruptionDelta = corruptionDelta;
        HealthDelta = healthDelta;
        OutputItemId = outputItemId ?? string.Empty;
        OutputAmount = Mathf.Max(0, outputAmount);
    }

    public bool Success { get; }
    public string Message { get; }
    public float WillDelta { get; }
    public float FearDelta { get; }
    public float TrustDelta { get; }
    public float GrudgeDelta { get; }
    public float CorruptionDelta { get; }
    public float HealthDelta { get; }
    public string OutputItemId { get; }
    public int OutputAmount { get; }
}

public interface ICaptivityInteractionHandler
{
    string InteractionId { get; }
    string DisplayName { get; }
    CaptiveInteractionKind Kind { get; }
    float RequiredWork { get; }
    IReadOnlyDictionary<StockCategory, int> MaterialRequirements { get; }
    bool CanExecute(CaptivityInteractionContext context, out string failureReason);
    CaptivityInteractionResult Execute(CaptivityInteractionContext context);
}

public interface ICaptivityRuntime
{
    IReadOnlyList<CaptiveState> Captives { get; }
    IReadOnlyList<CaptivePolicyData> Policies { get; }
    bool TryGetCaptive(string captiveId, out CaptiveState captive);
    bool TryGetActor(string captiveId, out CharacterActor actor);
    bool TryGetHousing(string captiveId, out BuildableObject housing);
    bool IsCaptive(string persistentId);
    bool HasSecureHousing(CharacterActor captive, out BuildableObject housing, out string reason);
    CaptivitySaveData Capture();
    void Restore(CaptivitySaveData saveData, IList<string> warnings);
}

public interface ICaptiveLaborQuery
{
    bool IsWorkAllowed(
        CharacterActor actor,
        WorkTypeId workTypeId,
        out string reason);
}

public interface ICaptivityCommandService
{
    bool TryOrderCapture(
        CharacterActor subject,
        CharacterActor carrier,
        out string failureReason);
    bool CancelCapture(string captiveId, string reason);
    bool TrySetPolicy(string captiveId, string policyId, out string failureReason);
    bool TryCreatePolicy(
        string displayName,
        out string policyId,
        out string failureReason);
    bool TryDuplicatePolicy(
        string sourcePolicyId,
        out string policyId,
        out string failureReason);
    bool TryUpdatePolicy(
        CaptivePolicyData policy,
        out string failureReason);
    bool TryDeletePolicy(string policyId, out string failureReason);
    bool TrySetLaborPermissions(
        string captiveId,
        CaptiveLaborPermission permissions,
        out string failureReason);
    bool TryStartInteraction(
        string captiveId,
        string interactionId,
        CharacterActor warden,
        BuildableObject facility,
        out string failureReason);
    bool AdvanceInteraction(
        string captiveId,
        CharacterActor warden,
        float workAmount,
        out string status);
    bool TryRecruit(string captiveId, out string failureReason);
    bool TryConvertToMinion(string captiveId, out string failureReason);
    bool TryRansom(
        string captiveId,
        out int paidAmount,
        out string failureReason);
    bool TryRelease(string captiveId, out string failureReason);
    bool TryTriggerBetrayal(
        string captiveId,
        string trigger,
        out string failureReason);
    bool TryAssignPerformer(string captiveId, bool assigned, out string failureReason);
    bool TryResolvePerformerMilestone(
        string captiveId,
        CaptivePerformerMilestoneChoice choice,
        out string failureReason);
    void RecordPerformance(
        string captiveId,
        float fameGain,
        float skillGain,
        bool injured);
}

public readonly struct CaptivePerformerMilestoneEvent
{
    public CaptivePerformerMilestoneEvent(
        string captiveId,
        int fameThreshold,
        string message)
    {
        CaptiveId = captiveId ?? string.Empty;
        FameThreshold = fameThreshold;
        Message = message ?? string.Empty;
    }

    public string CaptiveId { get; }
    public int FameThreshold { get; }
    public string Message { get; }
}

public interface ICaptivityEscapeRuntime
{
    bool TryGetEscapeState(
        string captiveId,
        CharacterActor actor,
        out Vector2Int destination,
        out string failureReason);
    IDisposable BeginEscapePass(CharacterActor actor, string captiveId);
    void CompleteEscape(string captiveId, CharacterActor actor);
    void FailEscape(string captiveId, CharacterActor actor, string reason);
}

public readonly struct CaptiveRansomedEvent
{
    public CaptiveRansomedEvent(
        string captiveId,
        int amount,
        float retaliationPressure)
    {
        CaptiveId = captiveId ?? string.Empty;
        Amount = Mathf.Max(0, amount);
        RetaliationPressure = Mathf.Clamp(retaliationPressure, 0f, 100f);
    }

    public string CaptiveId { get; }
    public int Amount { get; }
    public float RetaliationPressure { get; }
}

public readonly struct CaptiveEscapedEvent
{
    public CaptiveEscapedEvent(string captiveId, string trigger, bool betrayal)
    {
        CaptiveId = captiveId ?? string.Empty;
        Trigger = trigger ?? string.Empty;
        Betrayal = betrayal;
    }

    public string CaptiveId { get; }
    public string Trigger { get; }
    public bool Betrayal { get; }
}

public interface ICaptivityEscortRuntime
{
    IDisposable BeginEscortPass(CharacterActor carrier, string captiveId);
    bool TryGetEscortState(
        string captiveId,
        CharacterActor carrier,
        out CaptiveState captive,
        out CharacterActor subject,
        out string failureReason);
    bool TryPickupReservedRestraint(
        CaptiveState captive,
        CharacterActor carrier,
        out string failureReason);
    float AdvanceStabilization(
        string captiveId,
        CharacterActor carrier,
        float workAmount);
    bool TryBeginEscort(
        string captiveId,
        CharacterActor carrier,
        out string failureReason);
    bool TryCompleteEscort(
        string captiveId,
        CharacterActor carrier,
        out string failureReason);
    void FailEscort(string captiveId, CharacterActor carrier, string reason);
}
