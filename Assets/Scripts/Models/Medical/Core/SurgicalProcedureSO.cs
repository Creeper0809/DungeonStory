using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(
    menuName = "DungeonStory/Medical/Surgical Procedure",
    fileName = "SurgicalProcedure")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class SurgicalProcedureSO : ScriptableObject
{
    public const string ResourcePath = "SO/Medical/Procedures";

    [SerializeField] private string procedureId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField, TextArea(2, 5)] private string description = string.Empty;
    [SerializeField] private SurgicalProcedureKind kind;
    [SerializeField] private MedicalProcedureFamily family =
        MedicalProcedureFamily.Biological;
    [SerializeField] private MedicalProcedureUrgency urgency =
        MedicalProcedureUrgency.Required;
    [SerializeField] private List<string> allowedAnatomyFamilies = new();
    [SerializeField] private List<string> allowedSpeciesIds = new();
    [SerializeField] private ProcedureOperatorRequirement operatorRequirement = new();
    [SerializeField] private string targetNodeId = string.Empty;
    [SerializeField] private string requiredResearchId = string.Empty;
    [SerializeField] private SurgeryFacilityTag requiredFacilityTags;
    [SerializeField, Min(1f)] private float requiredWork = 20f;
    [SerializeField, Range(0f, 0.9f)] private float difficultyPenalty = 0.15f;
    [SerializeField, Range(0f, 1f)] private float baseInfectionRisk = 0.1f;
    [SerializeField, Range(0f, 1f)] private float baseBleedingRisk = 0.1f;
    [SerializeField] private bool requiresAnesthesia = true;
    [SerializeField] private bool requiresRestraintForUnwilling = true;
    [SerializeField] private bool allowsLivingSubject = true;
    [SerializeField] private bool allowsCorpseSubject;
    [SerializeField] private bool allowsWildlife;
    [SerializeField] private List<SurgicalMaterialRequirement> materials = new();
    [SerializeReference] private List<SurgicalProcedureEffect> effects = new();

    public string ProcedureId => procedureId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? ProcedureId
        : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public SurgicalProcedureKind Kind => kind;
    public MedicalProcedureFamily Family => family;
    public MedicalProcedureUrgency Urgency => urgency;
    public IReadOnlyList<string> AllowedAnatomyFamilies => allowedAnatomyFamilies;
    public IReadOnlyList<string> AllowedSpeciesIds => allowedSpeciesIds;
    public ProcedureOperatorRequirement OperatorRequirement =>
        operatorRequirement ??= new ProcedureOperatorRequirement();
    public string TargetNodeId => targetNodeId?.Trim() ?? string.Empty;
    public string RequiredResearchId => requiredResearchId?.Trim() ?? string.Empty;
    public SurgeryFacilityTag RequiredFacilityTags => requiredFacilityTags;
    public float RequiredWork => Mathf.Max(1f, requiredWork);
    public float DifficultyPenalty => Mathf.Clamp(difficultyPenalty, 0f, 0.9f);
    public float BaseInfectionRisk => Mathf.Clamp01(baseInfectionRisk);
    public float BaseBleedingRisk => Mathf.Clamp01(baseBleedingRisk);
    public bool RequiresAnesthesia => requiresAnesthesia;
    public bool RequiresRestraintForUnwilling => requiresRestraintForUnwilling;
    public bool AllowsLivingSubject => allowsLivingSubject;
    public bool AllowsCorpseSubject => allowsCorpseSubject;
    public bool AllowsWildlife => allowsWildlife;
    public IReadOnlyList<SurgicalMaterialRequirement> Materials => materials;
    public IReadOnlyList<SurgicalProcedureEffect> Effects => effects;

#if UNITY_EDITOR
    public void Configure(
        string id,
        string label,
        string details,
        SurgicalProcedureKind procedureKind,
        string nodeId,
        string researchId,
        SurgeryFacilityTag facilityTags,
        float work,
        float difficulty,
        float infectionRisk,
        float bleedingRisk,
        bool anesthesia,
        bool restraint,
        bool living,
        bool corpse,
        bool wildlife,
        IEnumerable<SurgicalMaterialRequirement> requiredMaterials,
        IEnumerable<SurgicalProcedureEffect> procedureEffects,
        MedicalProcedureFamily procedureFamily = MedicalProcedureFamily.Biological,
        MedicalProcedureUrgency procedureUrgency = MedicalProcedureUrgency.Required,
        IEnumerable<string> anatomyFamilies = null,
        ProcedureOperatorRequirement requirement = null,
        IEnumerable<string> speciesIds = null)
    {
        procedureId = id?.Trim() ?? string.Empty;
        displayName = label?.Trim() ?? string.Empty;
        description = details?.Trim() ?? string.Empty;
        kind = procedureKind;
        targetNodeId = nodeId?.Trim() ?? string.Empty;
        requiredResearchId = researchId?.Trim() ?? string.Empty;
        requiredFacilityTags = facilityTags;
        requiredWork = Mathf.Max(1f, work);
        difficultyPenalty = Mathf.Clamp(difficulty, 0f, 0.9f);
        baseInfectionRisk = Mathf.Clamp01(infectionRisk);
        baseBleedingRisk = Mathf.Clamp01(bleedingRisk);
        requiresAnesthesia = anesthesia;
        requiresRestraintForUnwilling = restraint;
        allowsLivingSubject = living;
        allowsCorpseSubject = corpse;
        allowsWildlife = wildlife;
        materials = (requiredMaterials ?? Array.Empty<SurgicalMaterialRequirement>())
            .Where(item => item != null)
            .Select(item => item.Clone())
            .ToList();
        effects = (procedureEffects ?? Array.Empty<SurgicalProcedureEffect>())
            .Where(item => item != null)
            .ToList();
        family = procedureFamily;
        urgency = procedureUrgency;
        allowedAnatomyFamilies = (anatomyFamilies ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        operatorRequirement = requirement ?? new ProcedureOperatorRequirement();
        allowedSpeciesIds = (speciesIds ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
#endif
}
