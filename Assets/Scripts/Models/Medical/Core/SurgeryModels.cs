using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

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
    RuneSuture = 1 << 12,
    AgeTreatment = 1 << 13
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
    Rehabilitation = 10,
    Maintenance = 11,
    SpeciesStabilization = 12,
    SpeciesAugmentation = 13
}

public enum MedicalProcedureFamily
{
    Biological = 0,
    Slime = 1,
    Myconid = 2,
    Avian = 3,
    Construct = 4,
    Vampiric = 5,
    Demonic = 6,
    Arcane = 7
}

public enum MedicalProcedureUrgency
{
    Maintenance = 0,
    Elective = 1,
    Required = 2,
    Emergency = 3
}

[Serializable]
public sealed class ProcedureOperatorStatRequirement
{
    public string statId = "stat:medical";
    [Min(0f)] public float weight = 1f;
    [Min(0)] public int minimumValue;
}

[Serializable]
public sealed class ProcedureOperatorRequirement
{
    [SerializeField] private List<ProcedureOperatorStatRequirement> stats = new();
    [SerializeField, Min(0f)] private float minimumWeightedScore = 3f;

    public IReadOnlyList<ProcedureOperatorStatRequirement> Stats => stats;
    public float MinimumWeightedScore => Mathf.Max(0f, minimumWeightedScore);

    public bool IsConfigured => stats != null && stats.Any(item =>
        item != null && !string.IsNullOrWhiteSpace(item.statId) && item.weight > 0f);
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

public enum SurgeryStatusCode
{
    None = 0,
    PatientAdmissionWaiting,
    PatientMissing,
    PatientAdmitted,
    PatientMovingToSurgery,
    PatientCurrentMovePending,
    PatientTransportByRescuer,
    PatientRestraintRequired,
    PatientAdmissionCellMissing,
    WildlifePatientMissing,
    WildlifePatientReady,
    WildlifePatientTransporting,
    WildlifePatientReturning,
    WildlifePatientReturnCompleted,
    WildlifeRestraintRequired,
    CorpseReady,
    CorpseTransportPending,
    MaterialsDeliveryPending,
    ProcessFluidUnavailable,
    AnesthesiaInProgress,
    PatientRestraintInProgress,
    OperationStarted,
    IncisionInProgress,
    ProcedureInProgress,
    SuturingInProgress,
    RecoveryObservation,
    RecoveryCompleted,
    Completed,
    CompletedWithMinorFailure,
    CompletedWithMajorFailure,
    FailedFatal,
    Cancelled,
    DoctorReplacementRequested,
    FacilityUnavailable,
    EnvironmentUnsafe,
    EnvironmentStabilizing,
    EnvironmentRestored,
    EnvironmentRecoveryIdle,
    EnvironmentRecoveryRequested,
    EmergencyProcedureContinuing,
    ProcedurePaused,
    ProcedureInterruptedOpenWound,
    PrisonReturnInProgress,
    PrisonReturnCompleted
}

public enum SurgeryRiskSummaryCode
{
    None = 0,
    SurgeryRiskProcedureMissing,
    SurgeryRiskEvaluated,
    SurgeryRiskEnvironmentAdjusted
}

[Serializable]
public sealed class SurgeryStatusData
{
    public SurgeryStatusCode code;
    public string primaryId = string.Empty;
    public string secondaryId = string.Empty;
    public float scalarValue;
    public float secondaryScalarValue;
    public float tertiaryScalarValue;
    public int countValue;
    public SurgeryOrderState stage = SurgeryOrderState.PatientWaiting;

    public void Set(
        SurgeryStatusCode nextCode,
        string nextPrimaryId = "",
        string nextSecondaryId = "",
        float nextScalarValue = 0f,
        float nextSecondaryScalarValue = 0f,
        float nextTertiaryScalarValue = 0f,
        int nextCountValue = 0,
        SurgeryOrderState nextStage = SurgeryOrderState.PatientWaiting)
    {
        code = nextCode;
        primaryId = nextPrimaryId ?? string.Empty;
        secondaryId = nextSecondaryId ?? string.Empty;
        scalarValue = nextScalarValue;
        secondaryScalarValue = nextSecondaryScalarValue;
        tertiaryScalarValue = nextTertiaryScalarValue;
        countValue = nextCountValue;
        stage = nextStage;
    }

    public SurgeryStatusData Clone()
    {
        return new SurgeryStatusData
        {
            code = code,
            primaryId = primaryId ?? string.Empty,
            secondaryId = secondaryId ?? string.Empty,
            scalarValue = scalarValue,
            secondaryScalarValue = secondaryScalarValue,
            tertiaryScalarValue = tertiaryScalarValue,
            countValue = countValue,
            stage = stage
        };
    }
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class HealSurgicalNodeEffect : SurgicalProcedureEffect
{
    [Min(0f)] public float health = 12f;
    [Min(0f)] public float infectionReduction = 10f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MaintainSurgicalPartEffect : SurgicalProcedureEffect
{
    [Min(0f)] public float durability = 18f;
    [Min(0f)] public float contaminationReduction = 8f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RemoveSurgicalNodeEffect : SurgicalProcedureEffect
{
    public bool createExtractedPart = true;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class InstallSurgicalPartEffect : SurgicalProcedureEffect
{
    public SurgicalPartKind partKind = SurgicalPartKind.NaturalOrgan;
    [Range(0.1f, 1.75f)] public float efficiency = 1f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ApplySurgicalBurdenEffect : SurgicalProcedureEffect
{
    [Min(0f)] public float rejection;
    [Min(0f)] public float mutation;
    [Min(0f)] public float infection;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ReduceSurgicalBurdenEffect : SurgicalProcedureEffect
{
    [Min(0f)] public float rejection;
    [Min(0f)] public float mutation;
    [Min(0f)] public float infection;
}

public enum AgeTreatmentEffectKind
{
    OrganRegeneration = 0,
    BloodRejuvenation = 1,
    RuneHibernation = 2,
    WholeBodyRegeneration = 3,
    TemporalStasis = 4
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ApplyAgeTreatmentEffect : SurgicalProcedureEffect
{
    public AgeTreatmentEffectKind treatment;
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
    public float complicationRiskMultiplier = 1f;
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
    public SurgeryRiskSummaryCode summaryCode;

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
    public SurgeryStatusData statusData = new();
    public float createdAt;
    public float recoveryUntil;
    public SurgeryOrderState environmentResumeStage =
        SurgeryOrderState.Anesthetizing;
    public SurgeryStatusData environmentWait = new();
    public float environmentStableSeconds;
    public SurgeryStatusData environmentRecovery = new();

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
    public bool preservationCanisterApplied;
    public string preservationOperationId = string.Empty;
    public string preservationCommitId = string.Empty;
    public string preservationSourceStackId = string.Empty;
    public long preservationInputMassGrams;
    public bool preservationOutcomePublished;
    public bool installed;
    public string installedSubjectId = string.Empty;
    public string sourceProductionCommitId = string.Empty;
    public string installationOrderId = string.Empty;
    public string installationOperationId = string.Empty;
    public string installationCommitId = string.Empty;
    public string installationSourceStackId = string.Empty;
    public string installationSubjectId = string.Empty;
}

public static class SurgicalPartInstallationIdentity
{
    public static string FormatOperationId(
        string orderId,
        string partInstanceId) =>
        $"surgical-part-install:{orderId}:{partInstanceId}";
}

[Serializable]
public sealed class DungeonSurgerySaveData
{
    public const int CurrentVersion = 10;

    public int version = CurrentVersion;
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
