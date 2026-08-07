using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EnemyAbilityEffectKind
{
    Damage,
    DamageOverTime,
    Heal,
    Delay,
    Vulnerability,
    Suppression,
    Smoke,
    Summon,
    Dispel,
    Guard
}

[System.Serializable]
public sealed class EnemyAbilityEffectRecord
{
    public EnemyAbilityEffectKind kind;
    public float magnitude = 1f;
    [Min(0)] public int durationRounds;
    public string targetTag = string.Empty;
}

[CreateAssetMenu(fileName = "EnemyAbility", menuName = "DungeonStory/V20/Enemy Ability")]
public sealed class EnemyAbilityDefinitionSO : ScriptableObject
{
    public string stableId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    [Min(1)] public int authoringRevision = 1;
    [TextArea] public string sourceNote = string.Empty;
    [Min(0)] public int cooldownRounds;
    public OffenseBattleTargetRule targetRule = OffenseBattleTargetRule.Enemy;
    public List<EnemyAbilityEffectRecord> effects = new();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (string.IsNullOrWhiteSpace(stableId)) errors.Add("Enemy ability id is required.");
        if (string.IsNullOrWhiteSpace(displayName)) errors.Add($"'{stableId}' display name is required.");
        if (effects == null || effects.Count == 0 || effects.Any(value => value == null || value.magnitude <= 0f))
            errors.Add($"'{stableId}' requires valid effects.");
        return errors;
    }
}
