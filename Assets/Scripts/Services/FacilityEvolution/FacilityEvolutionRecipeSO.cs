using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Facility Evolution/Recipe", order = 0)]
public class FacilityEvolutionRecipeSO : DataScriptableObject
{
    public string evolutionId;
    public string displayName;
    [TextArea] public string description;

    [Header("Lineage")]
    public BuildingSO resultBuilding;
    public BuildingSO[] fromFacilities = Array.Empty<BuildingSO>();
    public string[] fromLineageTags = Array.Empty<string>();
    [Min(1)] public int requiredStarGrade = 1;
    [Min(1)] public int resultStarGrade = 2;

    [Header("Visibility")]
    public bool publicByDefault = true;
    public string requiredResearchRecipeId;

    [Header("Room")]
    public bool requireUsableRoom = true;
    public FacilityEvolutionMetricRequirement[] requiredRoomScores = Array.Empty<FacilityEvolutionMetricRequirement>();
    public FacilityEvolutionMetricRequirement[] requiredRoomMetrics = Array.Empty<FacilityEvolutionMetricRequirement>();
    public string[] requiredRoomTags = Array.Empty<string>();
    public BuildingSO[] requiredUniqueFixtures = Array.Empty<BuildingSO>();

    [Header("Records and Resources")]
    public FacilityEvolutionTokenRequirement[] requiredRecordTokens = Array.Empty<FacilityEvolutionTokenRequirement>();
    public FacilityEvolutionMaterialRequirement[] requiredMaterials = Array.Empty<FacilityEvolutionMaterialRequirement>();
    public string[] allowedMutationTags = Array.Empty<string>();
    public bool consumeRecordTokens;

    [Header("Identity Pressure")]
    public FacilityEvolutionValue[] identityPressureWeights = Array.Empty<FacilityEvolutionValue>();
    [Range(0f, 1f)] public float minimumIdentityScore;

    [Header("Narrative Formula")]
    [Tooltip("Formula metadata is the only authority for new dynamic lineage modifiers. Empty metadata is fail-closed.")]
    public FacilityEvolutionFormulaPolicyDefinition formulaPolicy;
    [Tooltip("Each capability names a registered evolution module and its immutable potency formula.")]
    public List<FacilityEvolutionFormulaCapabilityDefinition> formulaCapabilities = new();

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public bool IsSpecial => !publicByDefault || !string.IsNullOrWhiteSpace(requiredResearchRecipeId);

    public bool HasValidData => resultBuilding != null
        && (!string.IsNullOrWhiteSpace(evolutionId) || !string.IsNullOrWhiteSpace(name))
        && (fromFacilities == null || fromFacilities.All((building) => building != null))
        && (fromFacilities != null && fromFacilities.Length > 0
            || fromLineageTags != null && fromLineageTags.Length > 0);

    public string EffectiveId => !string.IsNullOrWhiteSpace(evolutionId) ? evolutionId : name;

    public NarrativeFormulaStrengthPolicy RequireFormulaPolicy()
    {
        if (formulaPolicy == null)
            throw new InvalidOperationException($"Facility recipe '{EffectiveId}' has no v1 formula policy.");
        formulaPolicy.RequireCatalogSha256();
        return formulaPolicy.ToRuntime();
    }

    public FacilityEvolutionFormulaCapabilityDefinition RequireFormulaCapability()
    {
        FacilityEvolutionFormulaCapabilityDefinition[] values = (formulaCapabilities
                ?? new List<FacilityEvolutionFormulaCapabilityDefinition>())
            .Where(value => value != null).ToArray();
        if (values.Length != 1)
            throw new InvalidOperationException($"Facility recipe '{EffectiveId}' requires exactly one v1 formula capability.");
        values[0].ToRuntime();
        return values[0];
    }

    public IReadOnlyList<FacilityEvolutionFormulaCapabilityDefinition> RequireFormulaCapabilities()
    {
        FacilityEvolutionFormulaCapabilityDefinition[] values = (formulaCapabilities
                ?? new List<FacilityEvolutionFormulaCapabilityDefinition>())
            .Where(value => value != null)
            .OrderBy(value => value.evolutionModuleId, StringComparer.Ordinal)
            .ToArray();
        if (values.Length == 0
            || values.Select(value => value.evolutionModuleId)
                .Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidOperationException(
                $"Facility recipe '{EffectiveId}' requires distinct formula module definitions.");
        foreach (FacilityEvolutionFormulaCapabilityDefinition value in values)
            value.ToRuntime();
        return Array.AsReadOnly(values);
    }

    public FacilityEvolutionFormulaCapabilityDefinition RequireFormulaCapabilityForModule(
        string moduleId)
    {
        string canonical = moduleId?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, moduleId, StringComparison.Ordinal))
            throw new InvalidOperationException("Facility formula module ID must be canonical.");
        FacilityEvolutionFormulaCapabilityDefinition[] matches = RequireFormulaCapabilities()
            .Where(value => string.Equals(value.evolutionModuleId, canonical,
                StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                $"Facility recipe '{EffectiveId}' does not author module '{canonical}'.");
        return matches[0];
    }

    public bool MatchesSource(BuildableObject facility, FacilityEvolutionStateComponent state)
    {
        if (facility == null)
        {
            return false;
        }

        if (fromFacilities != null
            && fromFacilities.Any((building) => building != null && facility.id == building.id))
        {
            return true;
        }

        if (fromLineageTags == null || fromLineageTags.Length == 0)
        {
            return false;
        }

        return state != null
            && state.LineageTags.Any((tag) => fromLineageTags.Contains(tag));
    }
}

[Serializable]
public sealed class FacilityEvolutionFormulaRangeDefinition
{
    public string parameterId = string.Empty;
    [Min(0)] public long minimumUnits;
    [Min(0)] public long maximumUnits;
    [Min(1)] public long quantumUnits = 1;
    [Range(0, 6)] public int decimalPlaces;
    [Min(0)] public int costPerQuantum;

    public NarrativeFormulaQuantizedRange ToRuntime() => new(
        parameterId, minimumUnits, maximumUnits, quantumUnits,
        decimalPlaces, costPerQuantum);
}

[Serializable]
public sealed class FacilityEvolutionFormulaCapabilityDefinition
{
    public string capabilityId = string.Empty;
    [Tooltip("Must resolve through EvolutionModuleRegistry; this is never LLM-selected.")]
    public string evolutionModuleId = string.Empty;
    [Min(0)] public int baseCost;
    [Min(0)] public int triggerFrequencyCostPerUnit;
    [Min(0)] public int guaranteedProcCost;
    [Min(0)] public int areaCostPerExtraTarget;
    [Min(0)] public int multiEffectCostPerExtraEffect;
    [Min(0f)] public float narrativeAffinity = 1f;
    public List<string> affinityKeys = new();
    public List<string> conflictGroups = new();
    public List<string> forbiddenSynergies = new();
    public string formatterId = string.Empty;
    public string applicatorId = string.Empty;
    public List<FacilityEvolutionFormulaRangeDefinition> parameterRanges = new();

    public NarrativeFormulaCapabilityDescriptor ToRuntime()
    {
        RequireCanonical(capabilityId, nameof(capabilityId));
        RequireCanonical(evolutionModuleId, nameof(evolutionModuleId));
        if (!string.Equals(formatterId, capabilityId, StringComparison.Ordinal)
            || !string.Equals(applicatorId, capabilityId, StringComparison.Ordinal))
            throw new InvalidOperationException($"Facility formula capability '{capabilityId}' formatter/applicator must equal its capability ID.");
        return new NarrativeFormulaCapabilityDescriptor(
            capabilityId,
            (parameterRanges ?? throw new InvalidOperationException("Facility formula ranges are missing."))
                .Select(value => value?.ToRuntime() ?? throw new InvalidOperationException("Facility formula range is null.")),
            new[] { NarrativeFormulaParameterIds.Magnitude },
            baseCost,
            triggerFrequencyCostPerUnit,
            guaranteedProcCost,
            areaCostPerExtraTarget,
            multiEffectCostPerExtraEffect,
            Array.Empty<NarrativeFormulaPairSynergyCost>(),
            narrativeAffinity,
            affinityKeys,
            conflictGroups,
            forbiddenSynergies,
            formatterId,
            applicatorId);
    }

    private static void RequireCanonical(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException($"Facility formula {label} must be canonical.");
    }
}

[Serializable]
public sealed class FacilityEvolutionFormulaPolicyDefinition
{
    [Min(1)] public int formulaVersion =
        FacilityFormulaEvolutionAuthority.DrawbackModuleSelectionFormulaVersion;
    public string catalogSha256 = string.Empty;
    [Min(0)] public int baseBudget;
    [Min(0)] public int powerScale;
    [Min(0.0001f)] public float softCapK = 1f;
    [Min(0f)] public float minimumImportance;
    [Min(0f)] public float maximumImportance = 4f;
    public List<float> milestoneWeights = new();
    public NarrativeFormulaDrawbackCreditPolicyDefinition drawbackCredit = new();
    [Min(0)] public int triggerFrequencyUnits;
    public bool guaranteedProc = true;
    [Min(1)] public int targetCount = 1;

    public NarrativeFormulaStrengthPolicy ToRuntime() => new(
        formulaVersion, baseBudget, powerScale, softCapK,
        minimumImportance, maximumImportance,
        (milestoneWeights ?? new List<float>()).Select(value => (double)value));

    public NarrativeFormulaDrawbackCreditPolicy RequireDrawbackPolicy() =>
        (drawbackCredit ?? throw new InvalidOperationException(
            "Facility drawback-credit policy is missing.")).ToRuntime();

    public NarrativeFormulaGenerationCostContext RequireGenerationContext()
    {
        if (triggerFrequencyUnits < 0 || targetCount < 1)
            throw new InvalidOperationException("Facility formula generation context is invalid.");
        return new NarrativeFormulaGenerationCostContext(
            triggerFrequencyUnits, guaranteedProc, targetCount);
    }

    public string RequireCatalogSha256()
    {
        if (catalogSha256 == null || catalogSha256.Length != 64
            || !string.Equals(catalogSha256, catalogSha256.ToLowerInvariant(), StringComparison.Ordinal)
            || catalogSha256.Any(value => !Uri.IsHexDigit(value)))
            throw new InvalidOperationException("Facility formula catalog SHA-256 is invalid.");
        return catalogSha256;
    }
}
