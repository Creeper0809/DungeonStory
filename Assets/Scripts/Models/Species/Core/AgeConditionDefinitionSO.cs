using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AgeCondition",
    menuName = "DungeonStory/Population/Age Condition")]
public sealed class AgeConditionDefinitionSO : ScriptableObject
{
    [ContentIdentity] public string conditionId = string.Empty;
    public string displayName = string.Empty;
    public bool constructCondition;
    [ContentReference(ContentReferenceKind.AnatomyNode)]
    public List<string> affectedAnatomyNodeIds = new();
    public List<AgeConditionAnatomyTarget> anatomyTargets = new();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(conditionId) || string.IsNullOrWhiteSpace(displayName))
            errors.Add("Condition id and display name are required.");
        if (affectedAnatomyNodeIds == null
            || affectedAnatomyNodeIds.Count == 0
            || affectedAnatomyNodeIds.Any(string.IsNullOrWhiteSpace))
            errors.Add("At least one affected anatomy node is required.");
        var profiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in anatomyTargets ?? new())
        {
            if (target == null || string.IsNullOrWhiteSpace(target.anatomyProfileId)
                || !profiles.Add(target.anatomyProfileId)
                || target.nodeIds == null || target.nodeIds.Count == 0
                || target.nodeIds.Any(string.IsNullOrWhiteSpace)
                || target.nodeIds.Distinct(StringComparer.Ordinal).Count() != target.nodeIds.Count)
                errors.Add("Age-condition anatomy targets require distinct profiles and nonempty, distinct nodes.");
        }
        return errors;
    }

    public AgeConditionDefinition ToDefinition()
    {
        var errors = ValidateDefinition();
        if (errors.Count > 0)
            throw new InvalidOperationException($"Invalid age condition '{conditionId}': {string.Join("; ", errors)}");
        return new AgeConditionDefinition(conditionId, constructCondition,
            affectedAnatomyNodeIds.Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToArray(),
            (anatomyTargets ?? new()).ToDictionary(value => value.anatomyProfileId,
                value => (IReadOnlyList<string>)value.nodeIds.ToArray(), StringComparer.Ordinal));
    }
}

[Serializable]
public sealed class AgeConditionAnatomyTarget
{
    [ContentReference(ContentReferenceKind.AnatomyProfile)]
    public string anatomyProfileId = string.Empty;
    [ContentReference(ContentReferenceKind.AnatomyNode)]
    public List<string> nodeIds = new();
}
