using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public enum CharacterTraitSelectionRarity
{
    Common,
    Uncommon,
    Rare,
    Exceptional
}

public enum CharacterTraitPolarity
{
    Advantage,
    Tradeoff,
    Negative,
    Quirk,
    Extreme
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[CreateAssetMenu(menuName = "DungeonStory/Character/Trait", order = 0)]
public class CharacterTraitSO : DataScriptableObject, IGameplayEffectSource
{
    public string traitName;
    [TextArea] public string description;
    public CharacterModelModifiers modifiers = new CharacterModelModifiers();
    public CharacterCombatAbilityCollection combatAbilities = new CharacterCombatAbilityCollection();
    public ThermalProtectionProfile environmentalProtection =
        new ThermalProtectionProfile();
    public CharacterTraitSelectionRarity selectionRarity;
    public CharacterTraitPolarity polarity = CharacterTraitPolarity.Tradeoff;
    public string selectionFamilyId = string.Empty;
    public List<string> eligibleSpeciesTags = new();
    [Min(0.1f)] public float earnedWorkExperienceMultiplier = 1f;
    public List<string> incompatibilityGroups = new();
    public List<CharacterTraitBehaviorPreference> behaviorPreferences = new();
    public List<CharacterTraitMoodReaction> moodReactions = new();
    public List<CharacterTraitEventWeight> eventWeights = new();
    public List<GameplayEffectBinding> effects = new();
    [SerializeReference] public List<CharacterIdentityRule> identityRules = new();

    public CharacterTraitId DefinitionId =>
        new($"character-trait:{id}");

    public GameplayEffectSourceRef SourceRef =>
        new(GameplayEffectSourceKind.Trait, DefinitionId.Value);

    public IReadOnlyList<GameplayEffectBinding> Effects =>
        effects ??= new List<GameplayEffectBinding>();

    public bool IsExtreme => polarity == CharacterTraitPolarity.Extreme;

    public int SelectionWeight => selectionRarity switch
    {
        CharacterTraitSelectionRarity.Common => 100,
        CharacterTraitSelectionRarity.Uncommon => 55,
        CharacterTraitSelectionRarity.Rare => 25,
        CharacterTraitSelectionRarity.Exceptional => 10,
        _ => 0
    };

    public bool IsEligibleForSpecies(string speciesTag)
    {
        string[] authored = (eligibleSpeciesTags ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        return authored.Length == 0
            || authored.Contains(
                speciesTag?.Trim() ?? string.Empty,
                System.StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (id <= 0) errors.Add("Character trait numeric id must be positive.");
        if (string.IsNullOrWhiteSpace(traitName)) errors.Add($"Character trait {id} requires a name.");
        if (!System.Enum.IsDefined(typeof(CharacterTraitSelectionRarity), selectionRarity))
            errors.Add($"Character trait {id} has an invalid selection rarity.");
        if (!System.Enum.IsDefined(typeof(CharacterTraitPolarity), polarity))
            errors.Add($"Character trait {id} has an invalid polarity.");
        if (string.IsNullOrWhiteSpace(selectionFamilyId))
            errors.Add($"Character trait {id} requires a selection family.");
        if (earnedWorkExperienceMultiplier < 0.1f
            || float.IsNaN(earnedWorkExperienceMultiplier)
            || float.IsInfinity(earnedWorkExperienceMultiplier))
            errors.Add($"Character trait {id} has an invalid earned-work XP multiplier.");
        bool hasBehavior = (behaviorPreferences ?? new()).Any(value => value != null && value.IsValid);
        bool hasMood = (moodReactions ?? new()).Any(value => value != null && value.IsValid);
        bool hasEventWeight = (eventWeights ?? new()).Any(value => value != null && value.IsValid);
        bool hasEffect = Effects.Any(value => value != null);
        bool hasIdentity = (identityRules ?? new List<CharacterIdentityRule>())
            .Any(value => value != null);
        if (!hasBehavior && !hasMood && !hasEventWeight && !hasEffect && !hasIdentity)
            errors.Add($"Character trait {id} requires an effect or identity consequence.");
        foreach (GameplayEffectBinding binding in Effects.Where(value => value != null))
        {
            if (!binding.IsValidFor(SourceRef, out string reason))
                errors.Add($"Character trait {id} {reason}.");
        }
        if (hasEffect && HasLegacyNumericPayload())
            errors.Add(
                $"Character trait {id} uses shared effects and legacy numeric fields together; "
                + "legacy preferences/earned-work values must remain neutral.");
        foreach (CharacterIdentityRule rule in (identityRules
                     ?? new List<CharacterIdentityRule>()).Where(value => value != null))
            errors.AddRange(rule.Validate().Select(error =>
                $"Character trait {id}: {error}"));
        if ((identityRules ?? new List<CharacterIdentityRule>())
            .Where(value => value != null)
            .GroupBy(value => value.ruleId?.Trim() ?? string.Empty, System.StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
            errors.Add($"Character trait {id} contains duplicate identity rule ids.");
        if ((incompatibilityGroups ?? new()).Any(string.IsNullOrWhiteSpace))
            errors.Add($"Character trait {id} contains an empty incompatibility group.");
        if ((eligibleSpeciesTags ?? new()).Any(string.IsNullOrWhiteSpace))
            errors.Add($"Character trait {id} contains an empty eligible species tag.");
        if ((eligibleSpeciesTags ?? new())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value.Trim(), System.StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
            errors.Add($"Character trait {id} contains duplicate eligible species tags.");
        return errors;
    }

    private bool HasLegacyNumericPayload()
    {
        CharacterModelModifiers value = modifiers;
        return !Mathf.Approximately(earnedWorkExperienceMultiplier, 1f)
            || value != null && (
                value.preferredFacilityRoles != FacilityRole.None
                || value.dislikedFacilityRoles != FacilityRole.None
                || value.PreferredLegacyWorkTypes != FacilityWorkType.None
                || value.DislikedLegacyWorkTypes != FacilityWorkType.None);
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
[GameplayMigrationOnly(
    "Legacy V20 mood payload is read only by the founder-content migration builder; live mood policy uses CharacterIdentityRule.",
    "Remove after every retained non-founder V20 trait asset is migrated to identityRules and the V20 builder no longer authors this payload.")]
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
