using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[CreateAssetMenu(menuName = "DungeonStory/Character/Trait", order = 0)]
public class CharacterTraitSO : DataScriptableObject
{
    public string traitName;
    [TextArea] public string description;
    public CharacterStatBlock statBonus = new CharacterStatBlock();
    public CharacterModelModifiers modifiers = new CharacterModelModifiers();
    public CharacterCombatAbilityCollection combatAbilities = new CharacterCombatAbilityCollection();
    public ThermalProtectionProfile environmentalProtection =
        new ThermalProtectionProfile();
    public List<string> incompatibilityGroups = new();
    public List<CharacterTraitBehaviorPreference> behaviorPreferences = new();
    public List<CharacterTraitMoodReaction> moodReactions = new();
    public List<CharacterTraitEventWeight> eventWeights = new();

    public CharacterTraitId DefinitionId =>
        new($"character-trait:{id}");

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (id <= 0) errors.Add("Character trait numeric id must be positive.");
        if (string.IsNullOrWhiteSpace(traitName)) errors.Add($"Character trait {id} requires a name.");
        bool hasBehavior = (behaviorPreferences ?? new()).Any(value => value != null && value.IsValid);
        bool hasMood = (moodReactions ?? new()).Any(value => value != null && value.IsValid);
        bool hasEventWeight = (eventWeights ?? new()).Any(value => value != null && value.IsValid);
        if (!hasBehavior && !hasMood && !hasEventWeight)
            errors.Add($"Character trait {id} requires a behavior, mood, or event-weight consequence.");
        if ((incompatibilityGroups ?? new()).Any(string.IsNullOrWhiteSpace))
            errors.Add($"Character trait {id} contains an empty incompatibility group.");
        return errors;
    }
}

[System.Serializable]
public sealed class CharacterTraitBehaviorPreference
{
    public string behaviorTag = string.Empty;
    [Range(-1f, 1f)] public float utilityDelta;
    public bool IsValid => !string.IsNullOrWhiteSpace(behaviorTag) && Mathf.Abs(utilityDelta) > 0.0001f;
}

[System.Serializable]
public sealed class CharacterTraitMoodReaction
{
    public string triggerTag = string.Empty;
    [Range(-20f, 20f)] public float moodDelta;
    [Min(1)] public int durationDays = 1;
    public bool IsValid => !string.IsNullOrWhiteSpace(triggerTag) && Mathf.Abs(moodDelta) > 0.0001f;
}

[System.Serializable]
public sealed class CharacterTraitEventWeight
{
    public string eventCategoryId = string.Empty;
    [Range(0.1f, 10f)] public float multiplier = 1f;
    public bool IsValid => !string.IsNullOrWhiteSpace(eventCategoryId)
        && !Mathf.Approximately(multiplier, 1f);
}
