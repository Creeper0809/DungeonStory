using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class CharacterPerformanceCapacityInput
{
    [SerializeField] private CharacterFunctionalCapacityId capacityId;
    [SerializeField, Min(0f)] private float weight = 1f;
    [SerializeField] private CharacterPerformanceInputRole role;
    [SerializeField, Min(0f)] private float requiredThreshold = 0.10f;

    public CharacterPerformanceCapacityInput()
    {
    }

    public CharacterPerformanceCapacityInput(
        CharacterFunctionalCapacityId capacityId,
        float weight,
        CharacterPerformanceInputRole role,
        float requiredThreshold = 0.10f)
    {
        this.capacityId = capacityId;
        this.weight = weight;
        this.role = role;
        this.requiredThreshold = requiredThreshold;
    }

    public CharacterFunctionalCapacityId CapacityId => capacityId;
    public float Weight => weight;
    public CharacterPerformanceInputRole Role => role;
    public float RequiredThreshold => requiredThreshold;
}

[CreateAssetMenu(
    fileName = "CharacterPerformanceFormulaDefinition",
    menuName = "DungeonStory/Character/Performance Formula",
    order = 31)]
public sealed class CharacterPerformanceFormulaDefinitionSO : ScriptableObject
{
    [SerializeField] private string formulaId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private CharacterPerformanceFormulaDomain domain;
    [SerializeField] private CharacterPerformanceResultChannel resultChannel;
    [SerializeField] private float baseValue = 1f;
    [SerializeField] private List<CharacterPerformanceCapacityInput> capacityInputs = new();
    [SerializeField] private string primaryProficiencyId = string.Empty;
    [SerializeField] private string secondaryProficiencyId = string.Empty;
    [SerializeField, Range(0f, 0.2f)] private float secondaryProficiencyWeight;
    [SerializeField] private string gameplayEffectTargetId = string.Empty;
    [SerializeField] private string executionWorkTypeId = string.Empty;

    public string FormulaId => formulaId?.Trim() ?? string.Empty;
    public string DisplayName => displayName?.Trim() ?? string.Empty;
    public CharacterPerformanceFormulaDomain Domain => domain;
    public CharacterPerformanceResultChannel ResultChannel => resultChannel;
    public float BaseValue => baseValue;
    public IReadOnlyList<CharacterPerformanceCapacityInput> CapacityInputs => capacityInputs;
    public string PrimaryProficiencyId => primaryProficiencyId?.Trim() ?? string.Empty;
    public string SecondaryProficiencyId => secondaryProficiencyId?.Trim() ?? string.Empty;
    public float SecondaryProficiencyWeight => secondaryProficiencyWeight;
    public string GameplayEffectTargetId => gameplayEffectTargetId?.Trim() ?? string.Empty;
    public string ExecutionWorkTypeId => executionWorkTypeId?.Trim() ?? string.Empty;

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(FormulaId)) errors.Add("Formula id is empty.");
        if (string.IsNullOrWhiteSpace(DisplayName)) errors.Add($"Formula '{FormulaId}' has no display name.");
        if (float.IsNaN(BaseValue) || float.IsInfinity(BaseValue) || BaseValue < 0f)
            errors.Add($"Formula '{FormulaId}' has an invalid base value.");
        if (capacityInputs == null || capacityInputs.Count == 0)
            errors.Add($"Formula '{FormulaId}' has no capacity inputs.");
        else
        {
            if (capacityInputs.Any(input => input == null || float.IsNaN(input.Weight)
                    || float.IsInfinity(input.Weight) || input.Weight < 0f))
                errors.Add($"Formula '{FormulaId}' has an invalid capacity input.");
            if (capacityInputs.GroupBy(input => input.CapacityId).Any(group => group.Count() > 1))
                errors.Add($"Formula '{FormulaId}' repeats a capacity input.");
        }
        if (SecondaryProficiencyWeight < 0f || SecondaryProficiencyWeight > 0.2f)
            errors.Add($"Formula '{FormulaId}' secondary proficiency weight must be 0..0.2.");
        if (SecondaryProficiencyWeight > 0f && string.IsNullOrWhiteSpace(SecondaryProficiencyId))
            errors.Add($"Formula '{FormulaId}' has secondary weight without a secondary proficiency.");
        if (ExecutionWorkTypeId.Length > 0
            && !ExecutionWorkTypeId.StartsWith("work:", StringComparison.Ordinal))
            errors.Add($"Formula '{FormulaId}' has invalid execution work type '{ExecutionWorkTypeId}'.");
        return errors;
    }

#if UNITY_EDITOR
    public void Configure(
        string stableFormulaId,
        string authoredDisplayName,
        CharacterPerformanceFormulaDomain authoredDomain,
        CharacterPerformanceResultChannel channel,
        float authoredBaseValue,
        IEnumerable<CharacterPerformanceCapacityInput> inputs,
        string primaryProficiency,
        string secondaryProficiency,
        float secondaryWeight,
        string effectTargetId,
        string authoredExecutionWorkTypeId = "")
    {
        formulaId = stableFormulaId?.Trim() ?? string.Empty;
        displayName = authoredDisplayName?.Trim() ?? string.Empty;
        domain = authoredDomain;
        resultChannel = channel;
        baseValue = authoredBaseValue;
        capacityInputs = (inputs ?? Array.Empty<CharacterPerformanceCapacityInput>()).ToList();
        primaryProficiencyId = primaryProficiency?.Trim() ?? string.Empty;
        secondaryProficiencyId = secondaryProficiency?.Trim() ?? string.Empty;
        secondaryProficiencyWeight = secondaryWeight;
        gameplayEffectTargetId = effectTargetId?.Trim() ?? string.Empty;
        executionWorkTypeId = authoredExecutionWorkTypeId?.Trim() ?? string.Empty;
    }
#endif
}
