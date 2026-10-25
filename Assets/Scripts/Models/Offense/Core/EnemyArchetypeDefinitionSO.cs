using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EnemyCombatRole { Vanguard, Defender, Marksman, Support, Controller, Boss }
public enum EnemyTacticalIntentKind { Attack, Move, Protect, UseAbility, Retreat }

public static class EnemyTacticalTargetProtocols
{
    public const string Nearest = "nearest";
    public const string Backline = "backline";
    public const string Fast = "fast";
    public const string Shielded = "shielded";
    public const string Armored = "armored";
    public const string Guarded = "guarded";
    public const string LowHealth = "low-health";
    public const string Construct = "construct";
    public const string None = "none";
    public const string Desperate = "desperate";
    public static readonly IReadOnlyList<string> TargetTags = System.Array.AsReadOnly(new[]
        { Nearest, Backline, Fast, Shielded, Armored, Guarded, LowHealth, Construct, None });
    public static bool IsTargetTag(string value) => TargetTags.Contains(value, System.StringComparer.OrdinalIgnoreCase);
    public static bool IsOverride(string value) => string.IsNullOrEmpty(value)
        || string.Equals(value, Desperate, System.StringComparison.OrdinalIgnoreCase);
}

[System.Serializable]
public sealed class EnemyTacticalProfile
{
    [Range(0f, 10f)] public float attackWeight = 1f;
    [Range(0f, 10f)] public float protectWeight;
    [Range(0f, 10f)] public float abilityWeight = 1f;
    [Range(0f, 10f)] public float retreatWeight;
    [Range(0f, 1f)] public float retreatHealthFraction = 0.15f;
    [ContentReference(ContentReferenceKind.EnemyTacticalTarget, IgnoreCase = true)]
    public List<string> preferredTargetTags = new();
    [ContentReference(ContentReferenceKind.EnemyTacticalTarget, IgnoreCase = true)]
    public List<string> avoidedTargetTags = new();
    public string formationTag = string.Empty;

    public bool IsValid => attackWeight + protectWeight + abilityWeight + retreatWeight > 0f
        && !string.IsNullOrWhiteSpace(formationTag);
}

[System.Serializable]
public sealed class EnemyEquipmentLoadoutRecord
{
    [ContentReference(ContentReferenceKind.Weapon)]
    public string weaponDefinitionId = string.Empty;
    [ContentReference(ContentReferenceKind.Armor)]
    public string armorDefinitionId = string.Empty;
    [ContentReference(ContentReferenceKind.Shield)]
    public string shieldDefinitionId = string.Empty;
    [ContentReference(ContentReferenceKind.Ammunition)]
    public string ammunitionItemId = string.Empty;
}

[System.Serializable]
public sealed class EnemyBossPhaseRecord
{
    [Range(0.05f, 1f)] public float healthThreshold = 0.5f;
    [ContentReference(ContentReferenceKind.EnemyAbility)]
    public List<string> abilityIds = new();
    [ContentReference(ContentReferenceKind.EnemyTacticalOverride, IgnoreCase = true)]
    public string tacticalProfileOverrideTag = string.Empty;
}

[System.Serializable]
public sealed class EnemyIndividualGenerationProfile
{
    [Range(0, 4)] public int minimumGeneralTraits = 2;
    [Range(0, 4)] public int maximumGeneralTraits = 3;
    [Range(0, 4)] public int minimumExpressedHeritableTraits;
    [Range(0, 4)] public int maximumExpressedHeritableTraits = 2;
    [Range(0, 2)] public int maximumLatentHeritableTraits = 1;
    [Range(0, 30)] public int aptitudeVariance = 15;
    [Range(0f, 0.25f)] public float combatStatVariance = 0.12f;
    [Range(0f, 100f)] public float minimumLoyalty = 25f;
    [Range(0f, 100f)] public float maximumLoyalty = 75f;
    public bool recruitable = true;
    public string militaryTrainingId = string.Empty;
    [ContentReference("CharacterBackgroundDefinitionSO", "StableId")]
    public List<string> allowedBackgroundIds = new();

    public bool IsValid => minimumGeneralTraits >= 0
        && maximumGeneralTraits >= minimumGeneralTraits
        && maximumGeneralTraits <= 4
        && minimumExpressedHeritableTraits >= 0
        && maximumExpressedHeritableTraits >= minimumExpressedHeritableTraits
        && maximumExpressedHeritableTraits <= 4
        && maximumLatentHeritableTraits is >= 0 and <= 2
        && aptitudeVariance is >= 0 and <= 30
        && combatStatVariance is >= 0f and <= 0.25f
        && maximumLoyalty >= minimumLoyalty
        && !string.IsNullOrWhiteSpace(militaryTrainingId)
        && (allowedBackgroundIds ?? new List<string>()).All(value =>
            !string.IsNullOrWhiteSpace(value));
}

[CreateAssetMenu(fileName = "EnemyArchetype", menuName = "DungeonStory/V20/Enemy Archetype")]
public sealed class EnemyArchetypeDefinitionSO : ScriptableObject
{
    [ContentIdentity]
    public string stableId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    [Min(1)] public int authoringRevision = 1;
    [TextArea] public string sourceNote = string.Empty;
    [ContentIdentity]
    public string factionId = string.Empty;
    [ContentReference(ContentReferenceKind.Species, Aliases = "Truth|Construct")]
    public string speciesTag = string.Empty;
    public EnemyCombatRole role;
    [Min(1f)] public float maxHealth = 80f;
    [Min(0f)] public float attack = 5f;
    [Min(0f)] public float strength = 5f;
    [Min(0f)] public float toughness = 5f;
    [Min(0f)] public float dexterity = 5f;
    [Min(0.1f)] public float moveSpeed = 4f;
    public EnemyEquipmentLoadoutRecord equipment = new();
    [ContentReference(ContentReferenceKind.EnemyAbility)]
    public List<string> abilityIds = new();
    public EnemyTacticalProfile tacticalProfile = new();
    [ContentReference(ContentReferenceKind.BattleCounter)]
    public List<string> counterTags = new();
    [ContentReference(ContentReferenceKind.Item)]
    public List<string> rewardItemIds = new();
    public List<EnemyBossPhaseRecord> bossPhases = new();
    public EnemyIndividualGenerationProfile individualGeneration = new();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(stableId)) errors.Add("Enemy archetype id is required.");
        if (string.IsNullOrWhiteSpace(displayName)) errors.Add($"'{stableId}' display name is required.");
        if (string.IsNullOrWhiteSpace(factionId) || string.IsNullOrWhiteSpace(speciesTag))
            errors.Add($"'{stableId}' requires faction and species.");
        if (abilityIds == null || abilityIds.Count < 1 || abilityIds.Count > 3 || abilityIds.Any(string.IsNullOrWhiteSpace))
            errors.Add($"'{stableId}' requires one to three ability ids.");
        else if (abilityIds.Count != abilityIds.Distinct(System.StringComparer.Ordinal).Count())
            errors.Add($"'{stableId}' requires unique ability ids.");
        if (tacticalProfile == null || !tacticalProfile.IsValid)
            errors.Add($"'{stableId}' requires a valid tactical profile.");
        else if ((tacticalProfile.preferredTargetTags ?? new())
                     .Concat(tacticalProfile.avoidedTargetTags ?? new())
                     .Any(value => !EnemyTacticalTargetProtocols.IsTargetTag(value)))
            errors.Add($"'{stableId}' contains an unknown tactical target tag.");
        if (counterTags == null || counterTags.Count == 0 || counterTags.Any(string.IsNullOrWhiteSpace))
            errors.Add($"'{stableId}' requires counter tags.");
        else
        {
            foreach (string counterTag in counterTags.Where(value =>
                         !OffenseBattleCounterRules.IsDeclared(value)))
            {
                errors.Add($"'{stableId}' has an unknown battle counter tag '{counterTag}'.");
            }
        }
        if (individualGeneration == null || !individualGeneration.IsValid)
            errors.Add($"'{stableId}' requires a valid individual-generation profile.");
        if (bossPhases == null)
        {
            errors.Add($"'{stableId}' requires a boss phase list.");
        }
        else
        {
            HashSet<string> ownedAbilityIds = new(
                abilityIds?.Where(value => !string.IsNullOrWhiteSpace(value))
                    ?? Enumerable.Empty<string>(),
                System.StringComparer.Ordinal);
            for (int index = 0; index < bossPhases.Count; index++)
            {
                EnemyBossPhaseRecord phase = bossPhases[index];
                if (phase == null)
                {
                    errors.Add($"'{stableId}' boss phase {index} is missing.");
                    continue;
                }
                if (!EnemyTacticalTargetProtocols.IsOverride(phase.tacticalProfileOverrideTag))
                    errors.Add($"'{stableId}' boss phase {index} has an unknown tactical override.");

                List<string> phaseAbilityIds = phase.abilityIds;
                if (phaseAbilityIds == null
                    || phaseAbilityIds.Count == 0
                    || phaseAbilityIds.Any(string.IsNullOrWhiteSpace))
                {
                    errors.Add($"'{stableId}' boss phase {index} requires ability ids.");
                    continue;
                }
                if (phaseAbilityIds.Count
                    != phaseAbilityIds.Distinct(System.StringComparer.Ordinal).Count())
                {
                    errors.Add($"'{stableId}' boss phase {index} requires unique ability ids.");
                }
                foreach (string abilityId in phaseAbilityIds
                             .Distinct(System.StringComparer.Ordinal))
                {
                    if (!ownedAbilityIds.Contains(abilityId))
                    {
                        errors.Add(
                            $"'{stableId}' boss phase {index} ability '{abilityId}' "
                            + "is not owned by the archetype.");
                    }
                }
            }
        }
        return errors;
    }
}
