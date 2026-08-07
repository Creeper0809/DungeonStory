using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AgeCondition",
    menuName = "DungeonStory/Population/Age Condition")]
public sealed class AgeConditionDefinitionSO : ScriptableObject
{
    public string conditionId = string.Empty;
    public string displayName = string.Empty;
    public bool constructCondition;
    public List<string> affectedAnatomyNodeIds = new();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(conditionId) || string.IsNullOrWhiteSpace(displayName))
            errors.Add("Condition id and display name are required.");
        if (affectedAnatomyNodeIds == null
            || affectedAnatomyNodeIds.Count == 0
            || affectedAnatomyNodeIds.Any(string.IsNullOrWhiteSpace))
            errors.Add("At least one affected anatomy node is required.");
        return errors;
    }
}
