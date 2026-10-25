using System.Collections.Generic;
using System;
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
    [ContentReference(ContentReferenceKind.EnemyAbilityTarget)]
    public string targetTag = string.Empty;
}

public static class EnemyAbilityTargetContract
{
    private static readonly IReadOnlyDictionary<string, CombatAbilityTargeting> Targets =
        new Dictionary<string, CombatAbilityTargeting>(StringComparer.Ordinal)
        {
            ["all-allies"] = new(CombatAbilityTargetFilter.Any, scope: CombatAbilityTargetScope.Team),
            ["ally-front"] = new(CombatAbilityTargetFilter.Front),
            ["arcane"] = new(CombatAbilityTargetFilter.Arcane),
            ["armored"] = new(CombatAbilityTargetFilter.Armored),
            ["backline"] = new(CombatAbilityTargetFilter.Rear),
            ["battlefield"] = new(CombatAbilityTargetFilter.Any, scope: CombatAbilityTargetScope.IntrinsicTeam),
            ["cluster"] = new(CombatAbilityTargetFilter.Any, scope: CombatAbilityTargetScope.Formation),
            ["construct"] = new(CombatAbilityTargetFilter.Construct),
            ["fast"] = new(CombatAbilityTargetFilter.Fast),
            ["front"] = new(CombatAbilityTargetFilter.Front),
            ["highest-power"] = new(CombatAbilityTargetFilter.Any, CombatAbilityTargetPreference.HighestPower),
            ["injured-ally"] = new(CombatAbilityTargetFilter.Wounded, CombatAbilityTargetPreference.LowestHealth),
            ["priority-ally"] = new(CombatAbilityTargetFilter.Any, CombatAbilityTargetPreference.HighestPower),
            ["reserve"] = new(CombatAbilityTargetFilter.Any),
            ["retreating-ally"] = new(CombatAbilityTargetFilter.RetreatEligible, CombatAbilityTargetPreference.LowestHealth),
            ["wounded"] = new(CombatAbilityTargetFilter.Wounded, CombatAbilityTargetPreference.LowestHealth)
        };

    public static IEnumerable<string> Keys => Targets.Keys;
    public static CombatAbilityTargeting Resolve(IReadOnlyList<EnemyAbilityEffectRecord> effects, OffenseBattleTargetRule rule)
    {
        if (effects == null || effects.Count == 0 || effects.Any(value => value == null))
            throw new InvalidOperationException("Enemy targeting requires effect records.");
        CombatAbilityTargeting? result = null;
        foreach (var effect in effects)
        {
            string tag = effect.targetTag ?? string.Empty;
            if (tag.Length > 0 && !Targets.ContainsKey(tag))
                throw new InvalidOperationException($"Unknown enemy ability target '{tag}'.");
            if (tag is "all-allies" or "ally-front" or "injured-ally" or "priority-ally" or "retreating-ally"
                && rule != OffenseBattleTargetRule.Ally)
                throw new InvalidOperationException($"Enemy target '{tag}' requires the ally target rule.");
            if (tag == "reserve" && (rule != OffenseBattleTargetRule.Self || effect.kind != EnemyAbilityEffectKind.Summon)
                || tag == "battlefield" && (rule != OffenseBattleTargetRule.Self || effect.kind != EnemyAbilityEffectKind.Smoke))
                throw new InvalidOperationException($"Enemy target '{tag}' requires its intrinsic self effect.");
            var targeting = tag.Length == 0 ? default : Targets[tag];
            if (tag == "backline" && effect.kind == EnemyAbilityEffectKind.Suppression)
                targeting = new(CombatAbilityTargetFilter.Rear, scope: CombatAbilityTargetScope.Formation);
            if (result.HasValue && !result.Value.Equals(targeting))
                throw new InvalidOperationException("One enemy ability cannot silently combine different target contracts.");
            result = targeting;
        }
        return result.Value;
    }
}

[CreateAssetMenu(fileName = "EnemyAbility", menuName = "DungeonStory/V20/Enemy Ability")]
public sealed class EnemyAbilityDefinitionSO : ScriptableObject
{
    [ContentIdentity]
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
        if (!Enum.IsDefined(typeof(OffenseBattleTargetRule), targetRule))
            errors.Add($"'{stableId}' has an unknown target rule.");
        if (effects == null || effects.Count == 0 || effects.Any(value => value == null
                || !Enum.IsDefined(typeof(EnemyAbilityEffectKind), value.kind)
                || !float.IsFinite(value.magnitude) || value.magnitude <= 0f || value.durationRounds < 0))
            errors.Add($"'{stableId}' requires valid effects.");
        else
        {
            try { _ = EnemyAbilityTargetContract.Resolve(effects, targetRule); }
            catch (InvalidOperationException error) { errors.Add($"'{stableId}': {error.Message}"); }
        }
        return errors;
    }
}
