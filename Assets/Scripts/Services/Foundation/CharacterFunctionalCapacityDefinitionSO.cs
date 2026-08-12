using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterFunctionalCapacityDefinition",
    menuName = "DungeonStory/Character/Functional Capacity",
    order = 30)]
public sealed class CharacterFunctionalCapacityDefinitionSO : ScriptableObject
{
    [SerializeField] private CharacterFunctionalCapacityId capacityId;
    [SerializeField] private string stableId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField, TextArea] private string description = string.Empty;

    public CharacterFunctionalCapacityId CapacityId => capacityId;
    public string StableId => stableId?.Trim() ?? string.Empty;
    public string DisplayName => displayName?.Trim() ?? string.Empty;
    public string Description => description?.Trim() ?? string.Empty;

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        string expected = CharacterFunctionalCapacityIds.GetStableId(capacityId);
        if (!string.Equals(StableId, expected, StringComparison.Ordinal))
            errors.Add($"Capacity {capacityId} requires stable id '{expected}'.");
        if (string.IsNullOrWhiteSpace(DisplayName))
            errors.Add($"Capacity '{StableId}' requires a display name.");
        return errors;
    }

#if UNITY_EDITOR
    public void Configure(
        CharacterFunctionalCapacityId id,
        string authoredDisplayName,
        string authoredDescription)
    {
        capacityId = id;
        stableId = CharacterFunctionalCapacityIds.GetStableId(id);
        displayName = authoredDisplayName?.Trim() ?? string.Empty;
        description = authoredDescription?.Trim() ?? string.Empty;
    }
#endif
}
