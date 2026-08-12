using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class V20AuthoredContentSO : ScriptableObject
{
    [SerializeField] private string stableId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField, TextArea] private string description = string.Empty;
    [SerializeField, Min(1)] private int authoringRevision = 1;
    [SerializeField, TextArea] private string sourceNote = string.Empty;

    public string StableId => stableId?.Trim() ?? string.Empty;
    public string DisplayName => displayName?.Trim() ?? string.Empty;
    public string Description => description?.Trim() ?? string.Empty;
    public int AuthoringRevision => authoringRevision;
    public string SourceNote => sourceNote?.Trim() ?? string.Empty;

    public virtual IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (StableId.Length == 0) errors.Add("Stable id is required.");
        if (DisplayName.Length == 0) errors.Add($"'{StableId}' display name is required.");
        if (authoringRevision < 1) errors.Add($"'{StableId}' authoring revision must be positive.");
        return errors;
    }

#if UNITY_EDITOR
    public void ConfigureMetadata(
        string id,
        string title,
        string detail,
        int revision,
        string note)
    {
        stableId = id?.Trim() ?? string.Empty;
        displayName = title?.Trim() ?? string.Empty;
        description = detail?.Trim() ?? string.Empty;
        authoringRevision = Mathf.Max(1, revision);
        sourceNote = note?.Trim() ?? string.Empty;
    }
#endif
}

[Serializable]
public sealed class V20ItemAmountRequirement
{
    public string itemDefinitionId = string.Empty;
    [Min(1)] public int amount = 1;
    public bool consume;
}

[Serializable]
public sealed class V20FacilityRequirement
{
    public string buildingDefinitionId = string.Empty;
    public string capabilityId = string.Empty;
    [Min(1)] public int minimumCount = 1;
    public bool mustBeOperational = true;
}

[Serializable]
public sealed class V20ResearchRequirement
{
    [Min(1)] public int researchNumericId;
}

[Serializable]
public sealed class V20CharacterRequirement
{
    public CharacterLifeStage minimumLifeStage = CharacterLifeStage.Infant;
    public CharacterLifeStage maximumLifeStage = CharacterLifeStage.Elder;
    public string requiredTraitId = string.Empty;
    public string excludedTraitId = string.Empty;
    [Range(0, 100)] public int minimumHealth;
}

[Serializable]
public sealed class V20FactionRequirement
{
    public string factionId = string.Empty;
    [Range(-100, 100)] public int minimumRapport = -100;
    [Range(0, 100)] public int maximumGrievance = 100;
    [Range(0, 5)] public int minimumObligationTokens;
}

public enum V20WorldMetricKind
{
    None,
    Population,
    Money,
    FoodDays,
    DefenseReadiness,
    ProductionAutomation,
    RunePower,
    SelfSufficiencyDays,
    CompletedGenerations,
    DefeatedHumanBranches,
    PerCapitaNetWuIndex,
    EmergencyReserveCoverage,
    ProductivityCoverageDays,
    CultureAcceptance,
    PerCapitaServiceIndex
}

[Serializable]
public sealed class V20WorldMetricRequirement
{
    public V20WorldMetricKind kind;
    public float minimumValue;
}

[Serializable]
public sealed class V20ContentRequirementSet
{
    public List<V20ItemAmountRequirement> items = new();
    public List<V20FacilityRequirement> facilities = new();
    public List<V20ResearchRequirement> research = new();
    public List<V20CharacterRequirement> characters = new();
    public List<V20FactionRequirement> factions = new();
    public List<V20WorldMetricRequirement> worldMetrics = new();
    public List<string> requiredFlags = new();
    public List<string> excludedFlags = new();

    public IEnumerable<string> Validate(string ownerId)
    {
        foreach (V20ItemAmountRequirement value in items ?? new())
        {
            if (value == null || string.IsNullOrWhiteSpace(value.itemDefinitionId) || value.amount < 1)
                yield return $"'{ownerId}' contains an invalid item requirement.";
        }
        foreach (V20FacilityRequirement value in facilities ?? new())
        {
            if (value == null
                || (string.IsNullOrWhiteSpace(value.buildingDefinitionId)
                    && string.IsNullOrWhiteSpace(value.capabilityId))
                || value.minimumCount < 1)
                yield return $"'{ownerId}' contains an invalid facility requirement.";
        }
        foreach (V20ResearchRequirement value in research ?? new())
        {
            if (value == null || value.researchNumericId < 1)
                yield return $"'{ownerId}' contains an invalid research requirement.";
        }
        foreach (V20WorldMetricRequirement value in worldMetrics ?? new())
        {
            if (value == null || value.kind == V20WorldMetricKind.None)
                yield return $"'{ownerId}' contains an invalid world metric requirement.";
        }
        if ((requiredFlags ?? new()).Any(string.IsNullOrWhiteSpace)
            || (excludedFlags ?? new()).Any(string.IsNullOrWhiteSpace))
            yield return $"'{ownerId}' contains an empty state flag requirement.";
    }
}

public enum V20ContentEffectKind
{
    None,
    Mood,
    Trauma,
    SkillExperience,
    Health,
    Relationship,
    FactionRapport,
    FactionGrievance,
    FactionObligation,
    Money,
    ItemGrant,
    ItemConsume,
    WorldFlag,
    WorkDelayDays,
    Threat,
    DiseaseExposure,
    AmbitionProgress,
    MilestonePressure
}

[Serializable]
public sealed class V20ContentEffect
{
    public V20ContentEffectKind kind;
    public string targetId = string.Empty;
    public float amount;
    [Min(0)] public int durationDays;

    public bool IsValid => kind != V20ContentEffectKind.None
        && (kind != V20ContentEffectKind.WorldFlag || !string.IsNullOrWhiteSpace(targetId));
}

[Serializable]
public sealed class V20ChoiceDefinition
{
    public string choiceId = string.Empty;
    public string title = string.Empty;
    [TextArea] public string outcomeText = string.Empty;
    public V20ContentRequirementSet requirements = new();
    public List<V20ContentEffect> effects = new();

    public IEnumerable<string> Validate(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(choiceId) || string.IsNullOrWhiteSpace(title))
            yield return $"'{ownerId}' contains a choice without id or title.";
        foreach (string error in (requirements ?? new()).Validate(ownerId)) yield return error;
        if (effects == null || effects.Count == 0 || effects.Any(value => value == null || !value.IsValid))
            yield return $"'{ownerId}' choice '{choiceId}' requires valid mechanical effects.";
    }
}

[Serializable]
public sealed class V20WeightedId
{
    public string id = string.Empty;
    [Range(0.1f, 10f)] public float weight = 1f;
}

[Serializable]
public sealed class V20SkillBonus
{
    public string skillId = string.Empty;
    public int experience;
}

public static class V20AuthoredDefinitionValidation
{
    public static void RequireUniqueNonEmptyIds<T>(
        IEnumerable<T> values,
        Func<T, string> idSelector,
        string label,
        ICollection<string> errors)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (T value in values ?? Array.Empty<T>())
        {
            string id = value == null ? string.Empty : idSelector(value)?.Trim() ?? string.Empty;
            if (id.Length == 0) errors.Add($"{label} contains an empty id.");
            else if (!ids.Add(id)) errors.Add($"{label} id '{id}' is duplicated.");
        }
    }
}
