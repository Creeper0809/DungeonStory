using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StartingHistory",
    menuName = "DungeonStory/Character/Starting History")]
public sealed class CharacterStartingHistorySO : ScriptableObject
{
    public string historyId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    public string primaryProficiencyId = string.Empty;
    public string secondaryProficiencyId = string.Empty;
    [Min(0)] public int primaryBaseExperience = 45;
    [Min(0)] public int secondaryBaseExperience = 25;
    public List<CharacterStartingProficiencyBonus> proficiencyBonuses = new();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        CharacterProficiencyId primary = new(primaryProficiencyId);
        CharacterProficiencyId secondary = new(secondaryProficiencyId);
        if (string.IsNullOrWhiteSpace(historyId)
            || string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add("History id and display name are required.");
        }
        if (!primary.IsValid || !secondary.IsValid || primary == secondary
            || !BuiltInCharacterProficiencyIds.All.Contains(primary)
            || !BuiltInCharacterProficiencyIds.All.Contains(secondary))
        {
            errors.Add("History requires distinct built-in primary and secondary proficiencies.");
        }
        if (primaryBaseExperience < secondaryBaseExperience
            || secondaryBaseExperience < 0)
        {
            errors.Add("History primary/secondary experience is not ordered.");
        }
        CharacterStartingOriginSO.ValidateBonuses(proficiencyBonuses, errors);
        return errors;
    }
}
