using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EnemyCombatRole { Vanguard, Defender, Marksman, Support, Controller, Boss }
public enum EnemyTacticalIntentKind { Attack, Move, Protect, UseAbility, Retreat }

[System.Serializable]
public sealed class EnemyTacticalProfile
{
    [Range(0f, 10f)] public float attackWeight = 1f;
    [Range(0f, 10f)] public float protectWeight;
    [Range(0f, 10f)] public float abilityWeight = 1f;
    [Range(0f, 10f)] public float retreatWeight;
    [Range(0f, 1f)] public float retreatHealthFraction = 0.15f;
    public List<string> preferredTargetTags = new();
    public List<string> avoidedTargetTags = new();
    public string formationTag = string.Empty;

    public bool IsValid => attackWeight + protectWeight + abilityWeight + retreatWeight > 0f
        && !string.IsNullOrWhiteSpace(formationTag);
}

[System.Serializable]
public sealed class EnemyEquipmentLoadoutRecord
{
    public string weaponDefinitionId = string.Empty;
    public string armorDefinitionId = string.Empty;
    public string shieldDefinitionId = string.Empty;
    public string ammunitionItemId = string.Empty;
}

[System.Serializable]
public sealed class EnemyBossPhaseRecord
{
    [Range(0.05f, 1f)] public float healthThreshold = 0.5f;
    public List<string> abilityIds = new();
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
    public string stableId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    [Min(1)] public int authoringRevision = 1;
    [TextArea] public string sourceNote = string.Empty;
    public string factionId = string.Empty;
    public string speciesTag = string.Empty;
    public EnemyCombatRole role;
    [Min(1f)] public float maxHealth = 80f;
    [Min(0f)] public float attack = 5f;
    [Min(0f)] public float strength = 5f;
    [Min(0f)] public float toughness = 5f;
    [Min(0f)] public float dexterity = 5f;
    [Min(0.1f)] public float moveSpeed = 4f;
    public EnemyEquipmentLoadoutRecord equipment = new();
    public List<string> abilityIds = new();
    public EnemyTacticalProfile tacticalProfile = new();
    public List<string> counterTags = new();
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
        if (tacticalProfile == null || !tacticalProfile.IsValid)
            errors.Add($"'{stableId}' requires a valid tactical profile.");
        if (counterTags == null || counterTags.Count == 0 || counterTags.Any(string.IsNullOrWhiteSpace))
            errors.Add($"'{stableId}' requires counter tags.");
        if (individualGeneration == null || !individualGeneration.IsValid)
            errors.Add($"'{stableId}' requires a valid individual-generation profile.");
        return errors;
    }
}
