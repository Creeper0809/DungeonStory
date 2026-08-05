using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
                .Select(skill => CharacterSkillRuntimeEffects.ToCombatAbility(
                    skill,
                    progression.SkillSettings))
                .Where(ability => ability != null));
            if (progression.Ultimate != null
                && progression.Ultimate.ultimateDomain == CharacterUltimateDomain.Offense)
            {
                CharacterCombatAbilityDefinition ultimate =
                    CharacterSkillRuntimeEffects.ToCombatAbility(
                        progression.Ultimate,
                        progression.SkillSettings);
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
