using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StartingOrigin",
    menuName = "DungeonStory/Character/Starting Origin")]
public sealed class CharacterStartingOriginSO : ScriptableObject
{
    public string originId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    public List<CharacterStartingProficiencyBonus> proficiencyBonuses = new();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(originId)
            || string.IsNullOrWhiteSpace(displayName))
        {
            errors.Add("Origin id and display name are required.");
        }
        ValidateBonuses(proficiencyBonuses, errors);
        return errors;
    }

    internal static void ValidateBonuses(
        IReadOnlyList<CharacterStartingProficiencyBonus> bonuses,
        ICollection<string> errors)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (CharacterStartingProficiencyBonus bonus in bonuses
                     ?? Array.Empty<CharacterStartingProficiencyBonus>())
        {
            CharacterProficiencyId id = new(bonus?.proficiencyId);
            if (bonus == null || !id.IsValid
                || !BuiltInCharacterProficiencyIds.All.Contains(id)
                || bonus.experience < 0
                || !ids.Add(id.Value))
            {
                errors.Add("Origin/history proficiency bonuses are invalid.");
                return;
            }
        }
    }
}
