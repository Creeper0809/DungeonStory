using System;
using UnityEngine;

public interface ISurgicalFacilityAbility
{
    SurgeryFacilityTag FacilityTags { get; }
    bool IsPrimaryOperatingFacility { get; }
    float SterilityBonus { get; }
    float WorkSpeedMultiplier { get; }
    float SuccessBonus { get; }
    float AnesthesiaBonus { get; }
}

[Serializable]
[BuildingAbilityDisplayName("수술대")]
public sealed class BuildingSurgeryTableAbility :
    BuildingAbility,
    ISurgicalFacilityAbility
{
    [InspectorName("허용 수술 태그")]
    public SurgeryFacilityTag allowedProcedureTags =
        SurgeryFacilityTag.Emergency | SurgeryFacilityTag.GeneralSurgery;
    [Range(-0.25f, 0.25f), InspectorName("성공 보정")]
    public float successBonus;
    [Min(0.25f), InspectorName("작업 속도")]
    public float workSpeedMultiplier = 1f;
    [Range(0f, 1f), InspectorName("기본 무균도")]
    public float baseSterility = 0.25f;
    [Min(1), InspectorName("환자 슬롯")]
    public int patientSlots = 1;

    public SurgeryFacilityTag FacilityTags => allowedProcedureTags;
    public bool IsPrimaryOperatingFacility => true;
    public float SterilityBonus => baseSterility;
    public float WorkSpeedMultiplier => workSpeedMultiplier;
    public float SuccessBonus => successBonus;
    public float AnesthesiaBonus => 0f;
}

[Serializable]
[BuildingAbilityDisplayName("해부대")]
public sealed class BuildingAnatomyTableAbility :
    BuildingAbility,
    ISurgicalFacilityAbility
{
    [Range(-0.25f, 0.25f)] public float successBonus = 0.04f;
    [Min(0.25f)] public float workSpeedMultiplier = 1f;
    public SurgeryFacilityTag FacilityTags => SurgeryFacilityTag.Anatomy;
    public bool IsPrimaryOperatingFacility => true;
    public float SterilityBonus => 0.1f;
    public float WorkSpeedMultiplier => workSpeedMultiplier;
    public float SuccessBonus => successBonus;
    public float AnesthesiaBonus => 0f;
}

[Serializable]
[BuildingAbilityDisplayName("세정대")]
public sealed class BuildingSterilizationAbility :
    BuildingAbility,
    ISurgicalFacilityAbility
{
    [Range(0f, 1f)] public float sterilityBonus = 0.25f;
    [Min(0)] public int waterCost = 1;
    [Min(0)] public int disinfectantCost = 1;
    public SurgeryFacilityTag FacilityTags => SurgeryFacilityTag.Sterilization;
    public bool IsPrimaryOperatingFacility => false;
    public float SterilityBonus => sterilityBonus;
    public float WorkSpeedMultiplier => 1f;
    public float SuccessBonus => sterilityBonus * 0.08f;
    public float AnesthesiaBonus => 0f;
}

[Serializable]
[BuildingAbilityDisplayName("마취 장치")]
public sealed class BuildingAnesthesiaAbility :
    BuildingAbility,
    ISurgicalFacilityAbility
{
    [Range(0f, 1f)] public float stabilityBonus = 0.3f;
    public string anesthesiaItemId = "medicine:anesthetic";
    [Min(1)] public int anesthesiaCost = 1;
    public SurgeryFacilityTag FacilityTags => SurgeryFacilityTag.Anesthesia;
    public bool IsPrimaryOperatingFacility => false;
    public float SterilityBonus => 0f;
    public float WorkSpeedMultiplier => 1f;
    public float SuccessBonus => stabilityBonus * 0.03f;
    public float AnesthesiaBonus => stabilityBonus;
}

[Serializable]
[BuildingAbilityDisplayName("장기 보관함")]
public sealed class BuildingOrganStorageAbility :
    BuildingAbility,
    ISurgicalFacilityAbility
{
    [Min(1f)] public float preservationDays = 15f;
    [Min(0)] public int fuelPerDay = 1;
    [Min(1)] public int capacity = 8;
    public SurgeryFacilityTag FacilityTags => SurgeryFacilityTag.OrganStorage;
    public bool IsPrimaryOperatingFacility => false;
    public float SterilityBonus => 0.05f;
    public float WorkSpeedMultiplier => 1f;
    public float SuccessBonus => 0f;
    public float AnesthesiaBonus => 0f;
}

[Serializable]
[BuildingAbilityDisplayName("이식 지원")]
public sealed class BuildingTransplantSupportAbility :
    BuildingAbility,
    ISurgicalFacilityAbility
{
    public bool circulationSupport = true;
    public bool immuneControl;
    public bool isolationRecovery;
    [Range(0f, 0.35f)] public float successBonus = 0.12f;
    [Range(0f, 1f)] public float rejectionReduction = 0.25f;
    [Min(0)] public int bloodCost = 1;
    [Min(0)] public int immunosuppressantCost = 1;

    public SurgeryFacilityTag FacilityTags =>
        (circulationSupport ? SurgeryFacilityTag.Transplant : SurgeryFacilityTag.None)
        | (immuneControl ? SurgeryFacilityTag.ImmuneControl : SurgeryFacilityTag.None)
        | (isolationRecovery ? SurgeryFacilityTag.IsolationRecovery : SurgeryFacilityTag.None);
    public bool IsPrimaryOperatingFacility => circulationSupport;
    public float SterilityBonus => isolationRecovery ? 0.2f : 0.05f;
    public float WorkSpeedMultiplier => circulationSupport ? 1.15f : 1f;
    public float SuccessBonus => successBonus;
    public float AnesthesiaBonus => circulationSupport ? 0.1f : 0f;
}

[Serializable]
[BuildingAbilityDisplayName("비전 수술")]
public sealed class BuildingArcaneSurgeryAbility :
    BuildingAbility,
    ISurgicalFacilityAbility
{
    [Range(0f, 0.35f)] public float successBonus = 0.1f;
    [Range(0f, 1f)] public float minimumMutationRisk = 0.08f;
    [Min(0)] public int manaCrystalCost = 1;
    public SurgeryFacilityTag FacilityTags => SurgeryFacilityTag.ArcaneSurgery;
    public bool IsPrimaryOperatingFacility => true;
    public float SterilityBonus => 0.05f;
    public float WorkSpeedMultiplier => 1f;
    public float SuccessBonus => successBonus;
    public float AnesthesiaBonus => 0.1f;
}

[Serializable]
[BuildingAbilityDisplayName("재활 보조")]
public sealed class BuildingRehabilitationAbility :
    BuildingAbility,
    ISurgicalFacilityAbility
{
    [Min(0.25f)] public float adaptationSpeedMultiplier = 1.5f;
    [Min(0f)] public float rejectionReductionPerWork = 0.1f;
    public bool primaryOperatingFacility = true;
    public bool runeSuture;
    [Min(0)] public int manaCrystalCost;
    public SurgeryFacilityTag FacilityTags => SurgeryFacilityTag.Rehabilitation
        | (runeSuture ? SurgeryFacilityTag.RuneSuture : SurgeryFacilityTag.None);
    public bool IsPrimaryOperatingFacility => primaryOperatingFacility;
    public float SterilityBonus => runeSuture ? 0.12f : 0f;
    public float WorkSpeedMultiplier => adaptationSpeedMultiplier;
    public float SuccessBonus => runeSuture ? 0.08f : 0f;
    public float AnesthesiaBonus => 0f;
}

[Serializable]
[BuildingAbilityDisplayName("보철 조립")]
public sealed class BuildingProstheticAssemblyAbility :
    BuildingAbility,
    ISurgicalFacilityAbility
{
    [Min(0.25f)] public float assemblySpeedMultiplier = 1f;
    [Range(0f, 0.25f)] public float qualityBonus = 0.05f;
    [Min(1)] public int outputCapacity = 2;

    public SurgeryFacilityTag FacilityTags => SurgeryFacilityTag.ProstheticAssembly;
    public bool IsPrimaryOperatingFacility => false;
    public float SterilityBonus => 0f;
    public float WorkSpeedMultiplier => assemblySpeedMultiplier;
    public float SuccessBonus => qualityBonus;
    public float AnesthesiaBonus => 0f;
}
