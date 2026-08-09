using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Flags]
[MovedFrom(true, sourceAssembly: "DungeonStory.Offense")]
public enum OffenseFormationMask
{
    None = 0,
    Front = 1 << 0,
    Middle = 1 << 1,
    Rear = 1 << 2,
    Any = Front | Middle | Rear
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseBattleTargetRule
{
    Self,
    Ally,
    Enemy
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCombatAbilityCollection
{
    [SerializeReference]
    private List<CharacterCombatAbilityDefinition> abilities = new();

    public IReadOnlyList<CharacterCombatAbilityDefinition> Abilities => abilities;

    public void SetAbilities(IEnumerable<CharacterCombatAbilityDefinition> values)
    {
        abilities = values?
            .Where(value => value != null && value.IsValid)
            .ToList() ?? new List<CharacterCombatAbilityDefinition>();
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCombatAbilityDefinition
{
    [SerializeField] private string id = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField, TextArea] private string description = string.Empty;
    [SerializeField, Min(0)] private int cooldownTurns;
    [SerializeField] private OffenseBattleTargetRule targetRule =
        OffenseBattleTargetRule.Enemy;
    [SerializeField] private OffenseFormationMask usableFrom =
        OffenseFormationMask.Any;
    [SerializeField] private OffenseFormationMask targetPositions =
        OffenseFormationMask.Any;
    [SerializeReference]
    private List<OffenseCombatEffectModule> effects = new();

    public CharacterCombatAbilityDefinition()
    {
    }

    public CharacterCombatAbilityDefinition(
        string id,
        string displayName,
        string description,
        int cooldownTurns,
        OffenseBattleTargetRule targetRule,
        params OffenseCombatEffectModule[] effects)
    {
        this.id = id ?? string.Empty;
        this.displayName = displayName ?? string.Empty;
        this.description = description ?? string.Empty;
        this.cooldownTurns = Math.Max(0, cooldownTurns);
        this.targetRule = targetRule;
        this.effects = effects?.Where(effect => effect != null).ToList()
            ?? new List<OffenseCombatEffectModule>();
    }

    public CharacterCombatAbilityDefinition(
        string id,
        string displayName,
        string description,
        int cooldownTurns,
        OffenseBattleTargetRule targetRule,
        OffenseFormationMask usableFrom,
        OffenseFormationMask targetPositions,
        params OffenseCombatEffectModule[] effects)
        : this(id, displayName, description, cooldownTurns, targetRule, effects)
    {
        this.usableFrom = usableFrom == OffenseFormationMask.None
            ? OffenseFormationMask.Any
            : usableFrom;
        this.targetPositions = targetPositions == OffenseFormationMask.None
            ? OffenseFormationMask.Any
            : targetPositions;
    }

    public string Id => id;
    public string DisplayName => displayName;
    public string Description => description;
    public int CooldownTurns => Math.Max(0, cooldownTurns);
    public OffenseBattleTargetRule TargetRule => targetRule;
    public OffenseFormationMask UsableFrom => usableFrom == OffenseFormationMask.None
        ? OffenseFormationMask.Any
        : usableFrom;
    public OffenseFormationMask TargetPositions =>
        targetPositions == OffenseFormationMask.None
            ? OffenseFormationMask.Any
            : targetPositions;
    public IReadOnlyList<OffenseCombatEffectModule> Effects => effects;
    public bool IsValid => !string.IsNullOrWhiteSpace(id)
        && !string.IsNullOrWhiteSpace(displayName)
        && effects != null
        && effects.Count > 0;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public abstract class OffenseCombatEffectModule
{
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseDamageEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(0f)] private float basicDamageMultiplier = 1f;
    [SerializeField] private float flatDamage;
    [SerializeField, Min(1)] private int hitCount = 1;

    public OffenseDamageEffect()
    {
    }

    public OffenseDamageEffect(
        float basicDamageMultiplier,
        float flatDamage = 0f,
        int hitCount = 1)
    {
        this.basicDamageMultiplier = Math.Max(0f, basicDamageMultiplier);
        this.flatDamage = flatDamage;
        this.hitCount = Math.Max(1, hitCount);
    }

    public float BasicDamageMultiplier => Math.Max(0f, basicDamageMultiplier);
    public float FlatDamage => flatDamage;
    public int HitCount => Math.Max(1, hitCount);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseHealEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(0f)] private float flatAmount;
    [SerializeField, Min(0f)] private float damageDealtRatio;

    public OffenseHealEffect()
    {
    }

    public OffenseHealEffect(float flatAmount, float damageDealtRatio = 0f)
    {
        this.flatAmount = Math.Max(0f, flatAmount);
        this.damageDealtRatio = Math.Max(0f, damageDealtRatio);
    }

    public float FlatAmount => Math.Max(0f, flatAmount);
    public float DamageDealtRatio => Math.Max(0f, damageDealtRatio);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseGuardEffect : OffenseCombatEffectModule
{
    [SerializeField, Range(0f, 0.95f)] private float damageReduction = 0.35f;
    [SerializeField, Min(1)] private int turns = 1;

    public OffenseGuardEffect()
    {
    }

    public OffenseGuardEffect(float damageReduction, int turns = 1)
    {
        this.damageReduction = Clamp(damageReduction, 0f, 0.95f);
        this.turns = Math.Max(1, turns);
    }

    public float DamageReduction => Clamp(damageReduction, 0f, 0.95f);
    public int Turns => Math.Max(1, turns);

    private static float Clamp(float value, float minimum, float maximum) =>
        Math.Min(maximum, Math.Max(minimum, value));
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseDamageOverTimeEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(0f)] private float damagePerTurn = 4f;
    [SerializeField, Min(1)] private int turns = 2;

    public OffenseDamageOverTimeEffect()
    {
    }

    public OffenseDamageOverTimeEffect(float damagePerTurn, int turns)
    {
        this.damagePerTurn = Math.Max(0f, damagePerTurn);
        this.turns = Math.Max(1, turns);
    }

    public float DamagePerTurn => Math.Max(0f, damagePerTurn);
    public int Turns => Math.Max(1, turns);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseVulnerabilityEffect : OffenseCombatEffectModule
{
    [SerializeField, Range(0f, 2f)] private float increasedDamage = 0.25f;
    [SerializeField, Min(1)] private int turns = 1;

    public OffenseVulnerabilityEffect()
    {
    }

    public OffenseVulnerabilityEffect(float increasedDamage, int turns = 1)
    {
        this.increasedDamage = Clamp(increasedDamage, 0f, 2f);
        this.turns = Math.Max(1, turns);
    }

    public float IncreasedDamage => Clamp(increasedDamage, 0f, 2f);
    public int Turns => Math.Max(1, turns);

    private static float Clamp(float value, float minimum, float maximum) =>
        Math.Min(maximum, Math.Max(minimum, value));
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseDelayEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(0f)] private float initiativePenalty = 3f;

    public OffenseDelayEffect()
    {
    }

    public OffenseDelayEffect(float initiativePenalty)
    {
        this.initiativePenalty = Math.Max(0f, initiativePenalty);
    }

    public float InitiativePenalty => Math.Max(0f, initiativePenalty);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseAttackModifierEffect : OffenseCombatEffectModule
{
    [SerializeField] private float multiplierDelta = 0.2f;
    [SerializeField, Min(1)] private int turns = 2;

    public OffenseAttackModifierEffect()
    {
    }

    public OffenseAttackModifierEffect(float multiplierDelta, int turns)
    {
        this.multiplierDelta = Clamp(multiplierDelta, -0.9f, 2f);
        this.turns = Math.Max(1, turns);
    }

    public float MultiplierDelta => Clamp(multiplierDelta, -0.9f, 2f);
    public int Turns => Math.Max(1, turns);

    private static float Clamp(float value, float minimum, float maximum) =>
        Math.Min(maximum, Math.Max(minimum, value));
}

[Serializable]
[MovedFrom(true, sourceAssembly: "DungeonStory.Offense")]
public sealed class OffenseSmokeEffect : OffenseCombatEffectModule
{
    [SerializeField, Range(0.1f, 0.8f)] private float obscuration = 0.5f;
    [SerializeField, Min(1)] private int turns = 2;

    public OffenseSmokeEffect()
    {
    }

    public OffenseSmokeEffect(float obscuration, int turns)
    {
        this.obscuration = Clamp(obscuration, 0.1f, 0.8f);
        this.turns = Math.Max(1, turns);
    }

    public float Obscuration => Clamp(obscuration, 0.1f, 0.8f);
    public int Turns => Math.Max(1, turns);

    private static float Clamp(float value, float minimum, float maximum) =>
        Math.Min(maximum, Math.Max(minimum, value));
}

[Serializable]
[MovedFrom(true, sourceAssembly: "DungeonStory.Offense")]
public sealed class OffenseSummonEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(1f)] private float interceptHealth = 20f;
    [SerializeField, Min(1)] private int turns = 3;

    public OffenseSummonEffect()
    {
    }

    public OffenseSummonEffect(float interceptHealth, int turns = 3)
    {
        this.interceptHealth = Math.Max(1f, interceptHealth);
        this.turns = Math.Max(1, turns);
    }

    public float InterceptHealth => Math.Max(1f, interceptHealth);
    public int Turns => Math.Max(1, turns);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseCleanseEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(1)] private int maximumStatuses = 1;

    public OffenseCleanseEffect()
    {
    }

    public OffenseCleanseEffect(int maximumStatuses)
    {
        this.maximumStatuses = Math.Max(1, maximumStatuses);
    }

    public int MaximumStatuses => Math.Max(1, maximumStatuses);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseRepositionEffect : OffenseCombatEffectModule
{
    [SerializeField] private int offset = 1;

    public OffenseRepositionEffect()
    {
    }

    public OffenseRepositionEffect(int offset)
    {
        this.offset = Math.Min(2, Math.Max(-2, offset));
    }

    public int Offset => offset;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseConditionalAmplifyEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(0f)] private float extraDamageMultiplier = 0.35f;
    [SerializeField, Range(0.05f, 1f)] private float healthThreshold = 0.5f;

    public OffenseConditionalAmplifyEffect()
    {
    }

    public OffenseConditionalAmplifyEffect(
        float extraDamageMultiplier,
        float healthThreshold)
    {
        this.extraDamageMultiplier = Math.Max(0f, extraDamageMultiplier);
        this.healthThreshold = Clamp(healthThreshold, 0.05f, 1f);
    }

    public float ExtraDamageMultiplier => Math.Max(0f, extraDamageMultiplier);
    public float HealthThreshold => healthThreshold;

    private static float Clamp(float value, float minimum, float maximum) =>
        Math.Min(maximum, Math.Max(minimum, value));
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseCooldownAdjustEffect : OffenseCombatEffectModule
{
    [SerializeField] private int turnDelta = -1;

    public OffenseCooldownAdjustEffect()
    {
    }

    public OffenseCooldownAdjustEffect(int turnDelta)
    {
        this.turnDelta = Math.Min(9, Math.Max(-99, turnDelta));
    }

    public int TurnDelta => turnDelta;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseMultiTargetEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(2)] private int targetCount = 2;
    [SerializeField, Range(0.1f, 1f)] private float splashMultiplier = 0.55f;

    public OffenseMultiTargetEffect()
    {
    }

    public OffenseMultiTargetEffect(
        int targetCount,
        float splashMultiplier = 0.55f)
    {
        this.targetCount = Math.Max(2, targetCount);
        this.splashMultiplier = Clamp(splashMultiplier, 0.1f, 1f);
    }

    public int TargetCount => targetCount;
    public float SplashMultiplier => splashMultiplier;

    private static float Clamp(float value, float minimum, float maximum) =>
        Math.Min(maximum, Math.Max(minimum, value));
}
