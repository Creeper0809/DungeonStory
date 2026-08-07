using System.Collections.Generic;
using UnityEngine;

public enum BattlefieldModifierKind { Terrain, Objective, Hazard }

[CreateAssetMenu(fileName = "BattlefieldModifier", menuName = "DungeonStory/V20/Battlefield Modifier")]
public sealed class BattlefieldModifierDefinitionSO : ScriptableObject
{
    public string stableId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    [Min(1)] public int authoringRevision = 1;
    [TextArea] public string sourceNote = string.Empty;
    public BattlefieldModifierKind kind;
    [Range(0.25f, 2f)] public float movementMultiplier = 1f;
    [Range(0.25f, 2f)] public float accuracyMultiplier = 1f;
    [Range(0.25f, 2f)] public float damageMultiplier = 1f;
    public string requiredCounterTag = string.Empty;

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(stableId)) errors.Add("Battlefield modifier id is required.");
        if (string.IsNullOrWhiteSpace(displayName)) errors.Add($"'{stableId}' display name is required.");
        if (Mathf.Approximately(movementMultiplier, 1f)
            && Mathf.Approximately(accuracyMultiplier, 1f)
            && Mathf.Approximately(damageMultiplier, 1f)
            && string.IsNullOrWhiteSpace(requiredCounterTag))
            errors.Add($"'{stableId}' requires a mechanical consequence.");
        return errors;
    }
}
