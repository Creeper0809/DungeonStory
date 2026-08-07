using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterBackground", menuName = "DungeonStory/V20/Character Background")]
public sealed class CharacterBackgroundDefinitionSO : V20AuthoredContentSO
{
    public List<V20SkillBonus> startingSkills = new();
    public List<V20ContentEffect> startingEffects = new();
    public List<V20WeightedId> factionReactions = new();
    public string initialMemoryCode = string.Empty;

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (string.IsNullOrWhiteSpace(initialMemoryCode)) errors.Add($"'{StableId}' requires an initial memory code.");
        if ((startingSkills ?? new()).Any(value => value == null || string.IsNullOrWhiteSpace(value.skillId)))
            errors.Add($"'{StableId}' has an invalid starting skill.");
        return errors;
    }
}
