using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Flags]
public enum SurgeryFacilityTag
{
    None = 0,
    Emergency = 1 << 0,
    Anatomy = 1 << 1,
    GeneralSurgery = 1 << 2,
    Sterilization = 1 << 3,
    Anesthesia = 1 << 4,
    ProstheticAssembly = 1 << 5,
    Rehabilitation = 1 << 6,
    OrganStorage = 1 << 7,
    Transplant = 1 << 8,
    ImmuneControl = 1 << 9,
    IsolationRecovery = 1 << 10,
    ArcaneSurgery = 1 << 11,
    RuneSuture = 1 << 12
}

public enum SurgicalSubjectKind
{
    Character = 0,
    Wildlife = 1,
    HumanoidCorpse = 2,
    WildlifeCorpse = 3
}

public enum SurgicalProcedureKind
{
    Suture = 0,
    Transfusion = 1,
    RemoveForeignBody = 2,
    HealOrgan = 3,
    Amputate = 4,
    ExtractOrgan = 5,
    TransplantOrgan = 6,
    InstallProsthetic = 7,
    InstallImplant = 8,
    ArcaneModification = 9,
    Rehabilitation = 10
}

public enum SurgeryOrderState
{
    PatientWaiting = 0,
    MaterialsWaiting = 1,
    Anesthetizing = 2,
    Incision = 3,
    Procedure = 4,
    Suturing = 5,
    Recovering = 6,
    Completed = 7,
    Failed = 8,
    Cancelled = 9,
    EnvironmentWaiting = 10
}

public enum SurgeryFailureSeverity
{
    None = 0,
    Minor = 1,
    Major = 2,
    Fatal = 3
}

[Serializable]
public sealed class SurgicalMaterialRequirement
{
    public string itemId = string.Empty;
    public int quantity = 1;
    public bool optional;

    public SurgicalMaterialRequirement Clone()
    {
        return new SurgicalMaterialRequirement
        {
            itemId = itemId ?? string.Empty,
            quantity = Mathf.Max(1, quantity),
            optional = optional
        };
    }
}

[Serializable]
public abstract class SurgicalProcedureEffect
{
    [SerializeField] private string effectId = string.Empty;

    protected SurgicalProcedureEffect()
    {
        effectId = GetType().Name;
    }

    public string EffectId => string.IsNullOrWhiteSpace(effectId)
        ? GetType().Name
        : effectId.Trim();
}

[Serializable]
public sealed class HealSurgicalNodeEffect : SurgicalProcedureEffect
{
    [Min(0f)] public float health = 12f;
    [Min(0f)] public float infectionReduction = 10f;
}

[Serializable]
public sealed class RemoveSurgicalNodeEffect : SurgicalProcedureEffect
{
    public bool createExtractedPart = true;
}

[Serializable]
public sealed class InstallSurgicalPartEffect : SurgicalProcedureEffect
{
    public SurgicalPartKind partKind = SurgicalPartKind.NaturalOrgan;
    [Range(0.1f, 1.5f)] public float efficiency = 1f;
}

[Serializable]
public sealed class ApplySurgicalBurdenEffect : SurgicalProcedureEffect
{
    [Min(0f)] public float rejection;
    [Min(0f)] public float mutation;
    [Min(0f)] public float infection;
}

[Serializable]
public sealed class ReduceSurgicalBurdenEffect : SurgicalProcedureEffect
{
    [Min(0f)] public float rejection;
    [Min(0f)] public float mutation;
    [Min(0f)] public float infection;
}

[Serializable]
public sealed class SurgicalSubjectRef
{
    public SurgicalSubjectKind kind;
    public string subjectId = string.Empty;
    public string displayName = string.Empty;
    public string speciesId = string.Empty;
    public string anatomyProfileId = string.Empty;
    public bool willing = true;
    public bool automaticEmergencyDefault;

    public bool IsValid => !string.IsNullOrWhiteSpace(subjectId);

    public SurgicalSubjectRef Clone()
    {
        return new SurgicalSubjectRef
        {
            kind = kind,
            subjectId = subjectId ?? string.Empty,
            displayName = displayName ?? string.Empty,
            speciesId = speciesId ?? string.Empty,
            anatomyProfileId = anatomyProfileId ?? string.Empty,
            willing = willing,
            automaticEmergencyDefault = automaticEmergencyDefault
        };
    }
}

[Serializable]
public sealed class SurgeryRiskBreakdown
{
    public float successChance;
    public float infectionChance;
    public float bleedingChance;
    public float organDamageChance;
    public float deathChance;
    public float medicalContribution;
    public float dexterityContribution;
    public float researchContribution;
    public float facilityContribution;
    public float cleanlinessContribution;
    public float medicineContribution;
    public float anesthesiaContribution;
    public float difficultyPenalty;
    public float instabilityPenalty;
    public float compatibilityPenalty;
    public float environmentSuccessPenalty;
    public float environmentInfectionPenalty;
    public float environmentBleedingPenalty;
    public float environmentOrganDamagePenalty;
    public float environmentInstabilityAdded;
    public int environmentStagesEvaluated;
    public string summary = string.Empty;

    public SurgeryRiskBreakdown Clone()
    {
        return (SurgeryRiskBreakdown)MemberwiseClone();
    }
}

[Serializable]
public sealed class SurgeryOrder
{
    public string orderId = string.Empty;
    public string procedureId = string.Empty;
    public SurgicalSubjectRef subject = new();
    public string targetNodeId = string.Empty;
    public string selectedPartInstanceId = string.Empty;
    public string preferredDoctorId = string.Empty;
    public string doctorId = string.Empty;
    public string facilityId = string.Empty;
    public string materialDestinationId = string.Empty;
    public SurgeryOrderState state;
    public float requiredWork;
    public float completedWork;
    public float anesthesiaWork;
    public float incisionWork;
    public float procedureWork;
    public float sutureWork;
    public bool materialsRequested;
    public bool materialsConsumed;
    public bool processFluidConsumed;
    public bool anesthesiaConsumed;
    public bool incisionOpen;
    public bool resultRolled;
    public bool patientAdmitted;
    public bool admissionMoveRequested;
    public bool subjectAiWasPaused;
    public string patientTransporterId = string.Empty;
    public bool patientTransportInProgress;
    public bool patientReturnRequested;
    public int patientOriginX;
    public int patientOriginY;
    public int admissionX;
    public int admissionY;
    public float nextAdmissionRetryAt;
    public SurgeryFailureSeverity failureSeverity;
    public SurgeryRiskBreakdown risk = new();
    public List<SurgeryOrderState> reachedClinicalStages = new();
    public List<SurgicalMaterialRequirement> materials = new();
    public string status = string.Empty;
    public float createdAt;
    public float recoveryUntil;
    public SurgeryOrderState environmentResumeStage =
        SurgeryOrderState.Anesthetizing;
    public string environmentWaitReason = string.Empty;
    public float environmentStableSeconds;
    public string environmentRecoveryWorkStatus = string.Empty;

    public bool IsActive => state is not SurgeryOrderState.Completed
        and not SurgeryOrderState.Failed
        and not SurgeryOrderState.Cancelled;

    public float Progress01 => requiredWork <= 0f
        ? 0f
        : Mathf.Clamp01(completedWork / requiredWork);
}

[Serializable]
public sealed class SurgicalPartInstance
{
    public string partInstanceId = string.Empty;
    public SurgicalPartKind kind;
    public string nodeId = string.Empty;
    public string displayName = string.Empty;
    public string donorId = string.Empty;
    public string donorName = string.Empty;
    public string donorSpeciesId = string.Empty;
    public string anatomyFamily = string.Empty;
    public float quality = 1f;
    public float freshnessSeconds;
    public float contamination;
    public float specialEffectStrength;
    public string specialEffectId = string.Empty;
    public string worldStackId = string.Empty;
    public string storedFacilityId = string.Empty;
    public string reservedOrderId = string.Empty;
    public bool installed;
    public string installedSubjectId = string.Empty;
}

[Serializable]
public sealed class DungeonSurgerySaveData
{
    public List<SurgeryOrder> orders = new();
    public List<SurgicalPartInstance> parts = new();
    public List<SurgicalOrganStorageState> organStorageStates = new();
    public List<SurgicalCorpseFreshnessState> corpseFreshness = new();
    public List<SurgerySubjectPolicyState> policies = new();
    public List<CorpseSurgicalRecord> corpseRecords = new();
    public List<WildlifeAnatomyState> wildlifeAnatomy = new();
    public int orderSequence;
    public int partSequence;
}

[Serializable]
public sealed class SurgicalCorpseFreshnessState
{
    public string stackId = string.Empty;
    public float remainingFreshnessSeconds;

    public SurgicalCorpseFreshnessState Clone()
    {
        return new SurgicalCorpseFreshnessState
        {
            stackId = stackId ?? string.Empty,
            remainingFreshnessSeconds = Mathf.Max(0f, remainingFreshnessSeconds)
        };
    }
}

[Serializable]
public sealed class SurgicalOrganStorageState
{
    public string facilityId = string.Empty;
    public float fuelSecondsRemaining;
    public bool fuelDeliveryRequested;

    public SurgicalOrganStorageState Clone()
    {
        return new SurgicalOrganStorageState
        {
            facilityId = facilityId ?? string.Empty,
            fuelSecondsRemaining = Mathf.Max(0f, fuelSecondsRemaining),
            fuelDeliveryRequested = fuelDeliveryRequested
        };
    }
}

public readonly struct SurgicalOrganStorageSnapshot
{
    public SurgicalOrganStorageSnapshot(
        string facilityId,
        int storedParts,
        int capacity,
        bool powered,
        float fuelSecondsRemaining)
    {
        FacilityId = facilityId ?? string.Empty;
        StoredParts = Mathf.Max(0, storedParts);
        Capacity = Mathf.Max(0, capacity);
        Powered = powered;
        FuelSecondsRemaining = Mathf.Max(0f, fuelSecondsRemaining);
    }

    public string FacilityId { get; }
    public int StoredParts { get; }
    public int Capacity { get; }
    public bool Powered { get; }
    public float FuelSecondsRemaining { get; }
}

[Serializable]
public sealed class CorpseSurgicalRecord
{
    public string stackId = string.Empty;
    public List<string> extractedNodeIds = new();
}

[Serializable]
public sealed class SurgerySubjectPolicyState
{
    public string subjectId = string.Empty;
    public bool automaticEmergencySurgery;
}

public readonly struct SurgicalFacilitySnapshot
{
    public SurgicalFacilitySnapshot(
        BuildableObject primaryFacility,
        SurgeryFacilityTag availableTags,
        float sterility,
        float speedMultiplier,
        float successBonus,
        float anesthesiaBonus,
        IReadOnlyList<BuildableObject> supportFacilities,
        string blockReason)
    {
        PrimaryFacility = primaryFacility;
        AvailableTags = availableTags;
        Sterility = Mathf.Clamp01(sterility);
        SpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.25f, 3f);
        SuccessBonus = Mathf.Clamp(successBonus, -0.25f, 0.35f);
        AnesthesiaBonus = Mathf.Clamp01(anesthesiaBonus);
        SupportFacilities = supportFacilities ?? Array.Empty<BuildableObject>();
        BlockReason = blockReason ?? string.Empty;
    }

    public BuildableObject PrimaryFacility { get; }
    public SurgeryFacilityTag AvailableTags { get; }
    public float Sterility { get; }
    public float SpeedMultiplier { get; }
    public float SuccessBonus { get; }
    public float AnesthesiaBonus { get; }
    public IReadOnlyList<BuildableObject> SupportFacilities { get; }
    public string BlockReason { get; }
    public bool IsAvailable => PrimaryFacility != null
        && string.IsNullOrWhiteSpace(BlockReason);
}

public interface ISurgicalProcedureCatalog
{
    IReadOnlyList<SurgicalProcedureSO> Procedures { get; }
    bool TryGet(string procedureId, out SurgicalProcedureSO procedure);
    IReadOnlyList<string> Validate();
}

public interface ISurgicalFacilityQuery
{
    SurgicalFacilitySnapshot Evaluate(
        BuildableObject primaryFacility,
        SurgeryFacilityTag requiredTags);
    bool TryFindBestFacility(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        out SurgicalFacilitySnapshot facility,
        out string failureReason);
    IReadOnlyList<SurgicalFacilitySnapshot> GetCandidateFacilities(
        SurgicalProcedureSO procedure,
        bool includeBlocked = false);
    string GetFacilityId(BuildableObject facility);
}

public interface ISurgicalPatientTransportRuntime
{
    bool EnsureWildlifeAdmission(
        SurgeryOrder order,
        WildlifeActor patient,
        Vector2Int destination,
        out string status);
    void RequestWildlifeReturn(SurgeryOrder order);
    void CancelTransport(SurgeryOrder order, string reason);
    bool TryGetTransport(
        string orderId,
        CharacterActor carrier,
        out WildlifeActor patient,
        out Vector2Int destination,
        out bool returning,
        out string failureReason);
    IDisposable BeginTransportPass(
        CharacterActor carrier,
        string orderId);
    bool TryBeginCarry(
        string orderId,
        CharacterActor carrier,
        out string failureReason);
    bool TryCompleteCarry(
        string orderId,
        CharacterActor carrier,
        out string failureReason);
    void FailCarry(
        string orderId,
        CharacterActor carrier,
        string reason);
}

public interface ISurgeryRiskEvaluator
{
    SurgeryRiskBreakdown Evaluate(
        CharacterActor doctor,
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        SurgicalFacilitySnapshot facility,
        float patientInstability,
        float compatibilityPenalty);
}

public readonly struct SurgeryEnvironmentRiskSnapshot
{
    public SurgeryEnvironmentRiskSnapshot(
        EnvironmentalCellSnapshot environment,
        EnvironmentalExposureBand doctorBand,
        EnvironmentalExposureBand patientBand,
        float successPenalty,
        float infectionAdded,
        float bleedingAdded,
        float organDamageAdded,
        float instabilityAdded,
        bool extreme,
        bool normal,
        string summary)
    {
        Environment = environment;
        DoctorBand = doctorBand;
        PatientBand = patientBand;
        SuccessPenalty = Mathf.Max(0f, successPenalty);
        InfectionAdded = Mathf.Max(0f, infectionAdded);
        BleedingAdded = Mathf.Max(0f, bleedingAdded);
        OrganDamageAdded = Mathf.Max(0f, organDamageAdded);
        InstabilityAdded = Mathf.Max(0f, instabilityAdded);
        Extreme = extreme;
        Normal = normal;
        Summary = summary ?? string.Empty;
    }

    public EnvironmentalCellSnapshot Environment { get; }
    public EnvironmentalExposureBand DoctorBand { get; }
    public EnvironmentalExposureBand PatientBand { get; }
    public float SuccessPenalty { get; }
    public float InfectionAdded { get; }
    public float BleedingAdded { get; }
    public float OrganDamageAdded { get; }
    public float InstabilityAdded { get; }
    public bool Extreme { get; }
    public bool Normal { get; }
    public string Summary { get; }
}

public interface ISurgeryEnvironmentRiskEvaluator
{
    SurgeryEnvironmentRiskSnapshot Evaluate(
        Vector2Int facilityPosition,
        CharacterActor doctor,
        SurgicalSubjectRef subject);
    SurgeryRiskBreakdown Apply(
        SurgeryRiskBreakdown baseline,
        SurgeryEnvironmentRiskSnapshot snapshot,
        float stageWeight);
}

public interface ISurgicalPartRuntime
{
    IReadOnlyList<SurgicalPartInstance> Parts { get; }
    bool TryGet(string partInstanceId, out SurgicalPartInstance part);
    bool TryCreateExtractedPart(
        SurgicalSubjectRef donor,
        string nodeId,
        SurgicalPartKind kind,
        float quality,
        Vector2Int position,
        out SurgicalPartInstance part,
        out string failureReason);
    bool TryCreateCraftedPart(
        string nodeId,
        string displayName,
        SurgicalPartKind kind,
        float quality,
        Vector2Int position,
        out SurgicalPartInstance part,
        out string failureReason);
    bool TryReserveForOrder(
        string partInstanceId,
        string orderId,
        out string failureReason);
    void ReleaseReservation(string partInstanceId, string orderId);
    bool TryConsumeForInstallation(
        string partInstanceId,
        string orderId,
        string subjectId,
        out SurgicalPartInstance part,
        out string failureReason);
    void TickFreshness(float deltaTime);
    IReadOnlyList<SurgicalPartInstance> CaptureParts();
    IReadOnlyList<SurgicalOrganStorageState> CaptureStorageStates();
    void RestoreParts(
        IEnumerable<SurgicalPartInstance> parts,
        IList<string> warnings);
    void RestoreStorageStates(
        IEnumerable<SurgicalOrganStorageState> states,
        IList<string> warnings);
    bool TryGetOrganStorageStatus(
        BuildableObject storage,
        out SurgicalOrganStorageSnapshot snapshot);
}

public interface ISurgicalCorpseFreshnessRuntime
{
    bool TryGetFreshness(
        string stackId,
        out float remainingFreshnessSeconds,
        out bool isFresh);
    IReadOnlyList<SurgicalCorpseFreshnessState> Capture();
    void Restore(
        IEnumerable<SurgicalCorpseFreshnessState> states,
        IList<string> warnings);
}

public interface ISurgicalAugmentationQuery
{
    int GetStatBonus(string subjectId, CharacterStatType statType);
    string GetSpecialEffectLabel(SurgicalPartInstance part);
}

public interface ISurgeryPolicyRuntime
{
    bool IsAutomaticEmergencySurgeryEnabled(SurgicalSubjectRef subject);
    void SetAutomaticEmergencySurgery(
        SurgicalSubjectRef subject,
        bool enabled);
}

public interface ISurgeryExtractionLedger
{
    bool IsExtracted(string corpseStackId, string nodeId);
    bool TryMarkExtracted(
        string corpseStackId,
        string nodeId,
        out string failureReason);
    IReadOnlyList<CorpseSurgicalRecord> Capture();
    void Restore(
        IEnumerable<CorpseSurgicalRecord> records,
        IList<string> warnings);
}

public interface ISurgeryRuntime
{
    IReadOnlyList<SurgeryOrder> ActiveOrders { get; }
    bool TryGetOrder(string orderId, out SurgeryOrder order);
    bool HasWorkFor(BuildableObject facility);
    bool TryGetWorkFor(
        BuildableObject facility,
        out SurgeryOrder order);
    bool TryReserveWork(
        BuildableObject facility,
        CharacterActor doctor,
        out SurgeryOrder order,
        out string failureReason);
    bool ApplyWork(
        string orderId,
        CharacterActor doctor,
        float work,
        out bool completed,
        out string failureReason);
    void ReleaseDoctor(string orderId, CharacterActor doctor, string reason);
    DungeonSurgerySaveData Capture();
    void Restore(DungeonSurgerySaveData saveData, IList<string> warnings);
}

public interface ISurgeryCommandService
{
    bool TrySchedule(
        SurgicalSubjectRef subject,
        string procedureId,
        string targetNodeId,
        string selectedPartInstanceId,
        string preferredDoctorId,
        string preferredFacilityId,
        out SurgeryOrder order,
        out string failureReason);
    bool TryCancel(string orderId, out string failureReason);
}

public interface ISurgicalProcedureEffectHandler
{
    Type EffectType { get; }
    bool Apply(
        SurgeryOrder order,
        SurgicalProcedureEffect effect,
        BuildableObject facility,
        out string failureReason);
}
