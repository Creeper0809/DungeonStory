using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum OffenseBattleTargetRule
{
    Self,
    Ally,
    Enemy
}

[Serializable]
public sealed class CharacterCombatAbilityCollection
{
    [SerializeReference]
    private List<CharacterCombatAbilityDefinition> abilities = new List<CharacterCombatAbilityDefinition>();

    public IReadOnlyList<CharacterCombatAbilityDefinition> Abilities => abilities;

    public void SetAbilities(IEnumerable<CharacterCombatAbilityDefinition> values)
    {
        abilities = values?
            .Where(value => value != null && value.IsValid)
            .ToList() ?? new List<CharacterCombatAbilityDefinition>();
    }
}

[Serializable]
public sealed class CharacterCombatAbilityDefinition
{
    [SerializeField] private string id = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField, TextArea] private string description = string.Empty;
    [SerializeField, Min(0)] private int cooldownTurns;
    [SerializeField] private OffenseBattleTargetRule targetRule = OffenseBattleTargetRule.Enemy;
    [SerializeField] private OffenseFormationMask usableFrom = OffenseFormationMask.Any;
    [SerializeField] private OffenseFormationMask targetPositions = OffenseFormationMask.Any;
    [SerializeReference] private List<OffenseCombatEffectModule> effects = new List<OffenseCombatEffectModule>();

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
        this.cooldownTurns = Mathf.Max(0, cooldownTurns);
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
    public int CooldownTurns => Mathf.Max(0, cooldownTurns);
    public OffenseBattleTargetRule TargetRule => targetRule;
    public OffenseFormationMask UsableFrom => usableFrom == OffenseFormationMask.None
        ? OffenseFormationMask.Any
        : usableFrom;
    public OffenseFormationMask TargetPositions => targetPositions == OffenseFormationMask.None
        ? OffenseFormationMask.Any
        : targetPositions;
    public IReadOnlyList<OffenseCombatEffectModule> Effects => effects;
    public bool IsValid => !string.IsNullOrWhiteSpace(id)
        && !string.IsNullOrWhiteSpace(displayName)
        && effects != null
        && effects.Count > 0;
}

public sealed class CharacterCombatSkillTrackEntry
{
    public CharacterCombatSkillTrackEntry(
        CharacterCombatAbilityDefinition definition,
        int requiredLevel,
        string sourceLabel)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        RequiredLevel = Mathf.Max(1, requiredLevel);
        SourceLabel = sourceLabel ?? string.Empty;
    }

    public CharacterCombatAbilityDefinition Definition { get; }
    public int RequiredLevel { get; }
    public string SourceLabel { get; }
}

[Serializable]
public abstract class OffenseCombatEffectModule
{
    internal abstract void Apply(OffenseBattleEffectContext context);
}

[Serializable]
public sealed class OffenseDamageEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(0f)] private float basicDamageMultiplier = 1f;
    [SerializeField] private float flatDamage;
    [SerializeField, Min(1)] private int hitCount = 1;

    public OffenseDamageEffect()
    {
    }

    public OffenseDamageEffect(float basicDamageMultiplier, float flatDamage = 0f, int hitCount = 1)
    {
        this.basicDamageMultiplier = Mathf.Max(0f, basicDamageMultiplier);
        this.flatDamage = flatDamage;
        this.hitCount = Mathf.Max(1, hitCount);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        for (int i = 0; i < Mathf.Max(1, hitCount) && !context.Target.IsDead; i++)
        {
            float damage = context.Session.CalculateBasicDamage(context.Source, context.Target)
                * Mathf.Max(0f, basicDamageMultiplier)
                + flatDamage;
            context.DamageDealt += context.Session.ApplyDamage(context.Source, context.Target, damage);
        }
    }
}

[Serializable]
public sealed class OffenseHealEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(0f)] private float flatAmount;
    [SerializeField, Min(0f)] private float damageDealtRatio;

    public OffenseHealEffect()
    {
    }

    public OffenseHealEffect(float flatAmount, float damageDealtRatio = 0f)
    {
        this.flatAmount = Mathf.Max(0f, flatAmount);
        this.damageDealtRatio = Mathf.Max(0f, damageDealtRatio);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        float flatHeal = Mathf.Max(0f, flatAmount);
        if (flatHeal > 0f)
        {
            context.Session.Heal(context.Target, flatHeal);
        }

        float drainHeal = context.DamageDealt * Mathf.Max(0f, damageDealtRatio);
        if (drainHeal > 0f)
        {
            context.Session.Heal(context.Source, drainHeal);
        }
    }
}

[Serializable]
public sealed class OffenseGuardEffect : OffenseCombatEffectModule
{
    [SerializeField, Range(0f, 0.95f)] private float damageReduction = 0.35f;
    [SerializeField, Min(1)] private int turns = 1;

    public OffenseGuardEffect()
    {
    }

    public OffenseGuardEffect(float damageReduction, int turns = 1)
    {
        this.damageReduction = Mathf.Clamp(damageReduction, 0f, 0.95f);
        this.turns = Mathf.Max(1, turns);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        context.Session.AddStatus(
            context.Target,
            OffenseBattleStatusType.Guard,
            Mathf.Clamp(damageReduction, 0f, 0.95f),
            Mathf.Max(1, turns),
            context.Source.PersistentId,
            $"guard:{context.Source.PersistentId}");
    }
}

[Serializable]
public sealed class OffenseDamageOverTimeEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(0f)] private float damagePerTurn = 4f;
    [SerializeField, Min(1)] private int turns = 2;

    public OffenseDamageOverTimeEffect()
    {
    }

    public OffenseDamageOverTimeEffect(float damagePerTurn, int turns)
    {
        this.damagePerTurn = Mathf.Max(0f, damagePerTurn);
        this.turns = Mathf.Max(1, turns);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        context.Session.AddStatus(
            context.Target,
            OffenseBattleStatusType.DamageOverTime,
            Mathf.Max(0f, damagePerTurn),
            Mathf.Max(1, turns),
            context.Source.PersistentId,
            $"dot:{context.Source.PersistentId}");
    }
}

[Serializable]
public sealed class OffenseVulnerabilityEffect : OffenseCombatEffectModule
{
    [SerializeField, Range(0f, 2f)] private float increasedDamage = 0.25f;
    [SerializeField, Min(1)] private int turns = 1;

    public OffenseVulnerabilityEffect()
    {
    }

    public OffenseVulnerabilityEffect(float increasedDamage, int turns = 1)
    {
        this.increasedDamage = Mathf.Clamp(increasedDamage, 0f, 2f);
        this.turns = Mathf.Max(1, turns);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        context.Session.AddStatus(
            context.Target,
            OffenseBattleStatusType.Vulnerability,
            Mathf.Clamp(increasedDamage, 0f, 2f),
            Mathf.Max(1, turns),
            context.Source.PersistentId,
            $"vulnerable:{context.Source.PersistentId}");
    }
}

[Serializable]
public sealed class OffenseDelayEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(0f)] private float initiativePenalty = 3f;

    public OffenseDelayEffect()
    {
    }

    public OffenseDelayEffect(float initiativePenalty)
    {
        this.initiativePenalty = Mathf.Max(0f, initiativePenalty);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        context.Session.Delay(context.Target, Mathf.Max(0f, initiativePenalty));
    }
}

[Serializable]
public sealed class OffenseAttackModifierEffect : OffenseCombatEffectModule
{
    [SerializeField] private float multiplierDelta = 0.2f;
    [SerializeField, Min(1)] private int turns = 2;

    public OffenseAttackModifierEffect()
    {
    }

    public OffenseAttackModifierEffect(float multiplierDelta, int turns)
    {
        this.multiplierDelta = Mathf.Clamp(multiplierDelta, -0.9f, 2f);
        this.turns = Mathf.Max(1, turns);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        context.Session.AddStatus(
            context.Target,
            OffenseBattleStatusType.AttackModifier,
            Mathf.Clamp(multiplierDelta, -0.9f, 2f),
            Mathf.Max(1, turns),
            context.Source.PersistentId,
            $"attack-modifier:{context.Source.PersistentId}:{context.Target.PersistentId}");
    }
}

[Serializable]
public sealed class OffenseCleanseEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(1)] private int maximumStatuses = 1;

    public OffenseCleanseEffect()
    {
    }

    public OffenseCleanseEffect(int maximumStatuses)
    {
        this.maximumStatuses = Mathf.Max(1, maximumStatuses);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        context.Session.Cleanse(context.Target, Mathf.Max(1, maximumStatuses));
    }
}

[Serializable]
public sealed class OffenseRepositionEffect : OffenseCombatEffectModule
{
    [SerializeField] private int offset = 1;

    public OffenseRepositionEffect()
    {
    }

    public OffenseRepositionEffect(int offset)
    {
        this.offset = Mathf.Clamp(offset, -2, 2);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        context.Session.Reposition(context.Target, offset);
    }
}

[Serializable]
public sealed class OffenseConditionalAmplifyEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(0f)] private float extraDamageMultiplier = 0.35f;
    [SerializeField, Range(0.05f, 1f)] private float healthThreshold = 0.5f;

    public OffenseConditionalAmplifyEffect()
    {
    }

    public OffenseConditionalAmplifyEffect(float extraDamageMultiplier, float healthThreshold)
    {
        this.extraDamageMultiplier = Mathf.Max(0f, extraDamageMultiplier);
        this.healthThreshold = Mathf.Clamp(healthThreshold, 0.05f, 1f);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        if (context.Target.HealthRatio > healthThreshold)
        {
            return;
        }

        float damage = context.Session.CalculateBasicDamage(context.Source, context.Target)
            * Mathf.Max(0f, extraDamageMultiplier);
        context.DamageDealt += context.Session.ApplyDamage(context.Source, context.Target, damage);
    }
}

[Serializable]
public sealed class OffenseCooldownAdjustEffect : OffenseCombatEffectModule
{
    [SerializeField] private int turnDelta = -1;

    public OffenseCooldownAdjustEffect()
    {
    }

    public OffenseCooldownAdjustEffect(int turnDelta)
    {
        this.turnDelta = Mathf.Clamp(turnDelta, -99, 9);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        context.Session.AdjustCooldowns(context.Target, turnDelta);
    }
}

[Serializable]
public sealed class OffenseMultiTargetEffect : OffenseCombatEffectModule
{
    [SerializeField, Min(2)] private int targetCount = 2;
    [SerializeField, Range(0.1f, 1f)] private float splashMultiplier = 0.55f;

    public OffenseMultiTargetEffect()
    {
    }

    public OffenseMultiTargetEffect(int targetCount, float splashMultiplier = 0.55f)
    {
        this.targetCount = Mathf.Max(2, targetCount);
        this.splashMultiplier = Mathf.Clamp(splashMultiplier, 0.1f, 1f);
    }

    internal override void Apply(OffenseBattleEffectContext context)
    {
        IEnumerable<OffenseBattleCombatant> additional = context.Session
            .GetLivingTeam(context.Target.Team)
            .Where(target => target != context.Target)
            .Take(Mathf.Max(1, targetCount - 1));
        foreach (OffenseBattleCombatant target in additional)
        {
            float damage = context.Session.CalculateBasicDamage(context.Source, target) * splashMultiplier;
            context.DamageDealt += context.Session.ApplyDamage(context.Source, target, damage);
        }
    }
}

public sealed class OffenseBattleEffectContext
{
    internal OffenseBattleEffectContext(
        OffenseBattleSession session,
        OffenseBattleCombatant source,
        OffenseBattleCombatant target)
    {
        Session = session;
        Source = source;
        Target = target;
    }

    public OffenseBattleSession Session { get; }
    public OffenseBattleCombatant Source { get; }
    public OffenseBattleCombatant Target { get; }
    public float DamageDealt { get; internal set; }
}

public static class CharacterCombatAbilityCatalog
{
    public const string SlimeBarrierId = "species.slime.mucus-barrier";
    public const string OrcCrushId = "species.orc.crush";
    public const string VampireDrainId = "species.vampire.drain";
    public const string FighterFlurryId = "trait.fighter.flurry";
    public const string FieldDressingId = "common.field-dressing";
    public const string ExposeWeaknessId = "common.expose-weakness";
    public const string HamstringId = "common.hamstring";

    public static IReadOnlyList<CharacterCombatAbilityDefinition> GetAbilities(CharacterActor actor)
    {
        List<CharacterCombatAbilityDefinition> abilities = GetSpeciesAbilities(actor).ToList();
        CharacterProgression progression = actor?.Progression;
        if (progression != null)
        {
            abilities.AddRange(progression.ActiveSkills
                .Select(CharacterSkillRuntimeEffects.ToCombatAbility)
                .Where(ability => ability != null));
            if (progression.Ultimate != null
                && progression.Ultimate.ultimateDomain == CharacterUltimateDomain.Offense)
            {
                CharacterCombatAbilityDefinition ultimate =
                    CharacterSkillRuntimeEffects.ToCombatAbility(progression.Ultimate);
                if (ultimate != null)
                {
                    abilities.Add(ultimate);
                }
            }
        }

        return abilities
            .Where(ability => ability != null && ability.IsValid)
            .GroupBy(ability => ability.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
    }

    public static IReadOnlyList<CharacterCombatAbilityDefinition> GetSpeciesAbilities(CharacterActor actor)
    {
        CharacterSO data = actor != null && actor.Identity != null ? actor.Identity.Data : null;
        List<CharacterCombatAbilityDefinition> result = new List<CharacterCombatAbilityDefinition>();
        if (data?.species?.combatAbilities?.Abilities != null)
        {
            result.AddRange(data.species.combatAbilities.Abilities
                .Where(ability => ability != null && ability.IsValid));
        }

        string species = actor != null ? actor.SpeciesTag : data?.SpeciesTag;
        if (Contains(species, "slime", "슬라임")) result.Add(CreateSlimeBarrier());
        if (Contains(species, "orc", "오크")) result.Add(CreateOrcCrush());
        if (Contains(species, "vampire", "뱀파이어")) result.Add(CreateVampireDrain());
        return result
            .GroupBy(ability => ability.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(1)
            .ToList();
    }

    public static IReadOnlyList<CharacterCombatSkillTrackEntry> GetSkillTrack(CharacterActor actor)
    {
        CharacterSO data = actor != null && actor.Identity != null ? actor.Identity.Data : null;
        List<CharacterCombatSkillTrackEntry> result = new List<CharacterCombatSkillTrackEntry>();
        AddConfigured(result, data?.species?.combatAbilities, 1, "종족");

        string species = actor != null ? actor.SpeciesTag : data?.SpeciesTag;
        if (Contains(species, "slime", "슬라임")) AddUnique(result, CreateSlimeBarrier(), 1, "종족");
        if (Contains(species, "orc", "오크")) AddUnique(result, CreateOrcCrush(), 1, "종족");
        if (Contains(species, "vampire", "뱀파이어")) AddUnique(result, CreateVampireDrain(), 1, "종족");

        foreach (CharacterTraitSO trait in data?.traits ?? Array.Empty<CharacterTraitSO>())
        {
            AddConfigured(result, trait?.combatAbilities, 3, "특성");
        }

        if ((data?.traits ?? Array.Empty<CharacterTraitSO>())
            .Any(trait => Contains(trait?.traitName, "fighter", "전사")))
        {
            AddUnique(result, CreateFighterFlurry(), 3, "특성");
        }

        AddUnique(result, CreateFieldDressing(), 2, "공용");
        AddUnique(result, CreateExposeWeakness(), 4, "공용");
        AddUnique(result, CreateHamstring(), 6, "공용");
        return result;
    }

    public static CharacterCombatAbilityDefinition CreateSlimeBarrier()
    {
        return new CharacterCombatAbilityDefinition(
            SlimeBarrierId,
            "점액 방벽",
            "아군 하나가 다음 자기 차례까지 받는 피해를 35% 줄입니다.",
            2,
            OffenseBattleTargetRule.Ally,
            OffenseFormationMask.Any,
            OffenseFormationMask.Any,
            new OffenseGuardEffect(0.35f));
    }

    public static CharacterCombatAbilityDefinition CreateOrcCrush()
    {
        return new CharacterCombatAbilityDefinition(
            OrcCrushId,
            "분쇄",
            "강한 일격을 가하고 다음 차례까지 받는 피해를 25% 늘립니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            OffenseFormationMask.Front | OffenseFormationMask.Middle,
            OffenseFormationMask.Front | OffenseFormationMask.Middle,
            new OffenseDamageEffect(1.6f),
            new OffenseVulnerabilityEffect(0.25f));
    }

    public static CharacterCombatAbilityDefinition CreateVampireDrain()
    {
        return new CharacterCombatAbilityDefinition(
            VampireDrainId,
            "흡혈",
            "피해의 절반만큼 체력을 회복합니다.",
            3,
            OffenseBattleTargetRule.Enemy,
            OffenseFormationMask.Middle | OffenseFormationMask.Rear,
            OffenseFormationMask.Any,
            new OffenseDamageEffect(1.25f),
            new OffenseHealEffect(0f, 0.5f));
    }

    public static CharacterCombatAbilityDefinition CreateFighterFlurry()
    {
        return new CharacterCombatAbilityDefinition(
            FighterFlurryId,
            "연속 공격",
            "기본 공격의 75% 피해를 두 번 줍니다.",
            3,
            OffenseBattleTargetRule.Enemy,
            OffenseFormationMask.Front | OffenseFormationMask.Middle,
            OffenseFormationMask.Front | OffenseFormationMask.Middle,
            new OffenseDamageEffect(0.75f, hitCount: 2));
    }

    public static CharacterCombatAbilityDefinition CreateFieldDressing()
    {
        return new CharacterCombatAbilityDefinition(
            FieldDressingId,
            "응급 처치",
            "아군 한 명의 체력을 18 회복합니다.",
            3,
            OffenseBattleTargetRule.Ally,
            OffenseFormationMask.Any,
            OffenseFormationMask.Any,
            new OffenseHealEffect(18f));
    }

    public static CharacterCombatAbilityDefinition CreateExposeWeakness()
    {
        return new CharacterCombatAbilityDefinition(
            ExposeWeaknessId,
            "약점 노출",
            "가벼운 피해를 주고 2턴 동안 받는 피해를 20% 늘립니다.",
            3,
            OffenseBattleTargetRule.Enemy,
            OffenseFormationMask.Middle | OffenseFormationMask.Rear,
            OffenseFormationMask.Any,
            new OffenseDamageEffect(0.65f),
            new OffenseVulnerabilityEffect(0.2f, 2));
    }

    public static CharacterCombatAbilityDefinition CreateHamstring()
    {
        return new CharacterCombatAbilityDefinition(
            HamstringId,
            "발목 끊기",
            "피해를 주고 적의 다음 행동을 늦춥니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            OffenseFormationMask.Any,
            OffenseFormationMask.Front | OffenseFormationMask.Middle,
            new OffenseDamageEffect(0.85f),
            new OffenseDelayEffect(4f));
    }

    private static void AddConfigured(
        ICollection<CharacterCombatSkillTrackEntry> destination,
        CharacterCombatAbilityCollection collection,
        int requiredLevel,
        string sourceLabel)
    {
        foreach (CharacterCombatAbilityDefinition ability in collection?.Abilities
            ?? Array.Empty<CharacterCombatAbilityDefinition>())
        {
            AddUnique(destination, ability, requiredLevel, sourceLabel);
        }
    }

    private static void AddUnique(
        ICollection<CharacterCombatSkillTrackEntry> destination,
        CharacterCombatAbilityDefinition ability,
        int requiredLevel,
        string sourceLabel)
    {
        if (ability == null || !ability.IsValid
            || destination.Any(existing => string.Equals(
                existing.Definition.Id,
                ability.Id,
                StringComparison.Ordinal)))
        {
            return;
        }

        destination.Add(new CharacterCombatSkillTrackEntry(ability, requiredLevel, sourceLabel));
    }

    private static bool Contains(string value, params string[] candidates)
    {
        return !string.IsNullOrWhiteSpace(value)
            && candidates.Any(candidate => value.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
