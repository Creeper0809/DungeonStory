using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OffenseEncounterCatalog
{
    public static string GetEnemySummary(int campaignOrder)
    {
        return Mathf.Clamp(campaignOrder, 1, 6) switch
        {
            1 => "농장 집행관",
            2 => "상단 경비 + 석궁병",
            3 => "무기고 경비 2명 + 대장",
            4 => "마력 파수기 2명 + 마나 이상체",
            5 => "경쟁 던전 3종족 파티",
            _ => "봉인 수호자 2명 + 진실의 감시자"
        };
    }

    public static OffenseBattleCombatant CreateAlly(
        CharacterActor actor,
        string persistentId,
        OffenseFormationSlot formation = OffenseFormationSlot.Front,
        float stress = 0f)
    {
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }

        actor.EnsureRuntimeState();
        CharacterIdentity identity = actor.Identity;
        float stressMultiplier = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(stress / 100f));
        float maxHealth = Mathf.Max(1f, actor.MaxHealth);
        return new OffenseBattleCombatant(
            persistentId,
            identity != null ? identity.DisplayName : actor.name,
            identity != null ? identity.SpeciesTag : string.Empty,
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(
                maxHealth,
                actor.GetCharacterStat(CharacterStatType.Attack) * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.Strength) * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.Toughness) * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.Dexterity) * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.MoveSpeed) * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.Shooting) * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.Evasion) * stressMultiplier),
            Mathf.Clamp(actor.CurrentHealth, 0f, maxHealth),
            CharacterCombatAbilityCatalog.GetAbilities(actor),
            identity?.Data != null ? identity.Data.id : -1,
            formation);
    }

    public static IReadOnlyList<OffenseBattleCombatant> CreateEnemies(
        OffenseTargetDefinition target,
        DungeonDifficulty difficulty,
        OffenseRouteNode routeNode = null,
        OffenseStrategicPressureSnapshot pressure = default)
    {
        int stage = Mathf.Clamp(target?.campaignOrder ?? 1, 1, 6);
        DungeonDifficultyMultipliers multipliers = DungeonDifficultyRules.GetOffenseMultipliers(difficulty);
        List<EnemyTemplate> templates = stage switch
        {
            1 => new List<EnemyTemplate>
            {
                Enemy("farm-enforcer", "농장 집행관", "Human", 82f, 7f, 6f, 5f, 5f, 4f)
            },
            2 => new List<EnemyTemplate>
            {
                Enemy("caravan-guard", "상단 경비", "Human", 88f, 7f, 7f, 7f, 5f, 4f),
                Enemy("caravan-crossbow", "석궁병", "Human", 68f, 9f, 5f, 4f, 8f, 5f, AimedShot())
            },
            3 => new List<EnemyTemplate>
            {
                Enemy("armory-guard-a", "무기고 경비 A", "Human", 92f, 8f, 7f, 8f, 5f, 4f),
                Enemy("armory-guard-b", "무기고 경비 B", "Human", 92f, 8f, 7f, 8f, 5f, 4f),
                Enemy("armory-captain", "무기고 대장", "Human", 118f, 10f, 9f, 9f, 7f, 5f, CaptainBreak())
            },
            4 => new List<EnemyTemplate>
            {
                Enemy("arcane-sentry-a", "마력 파수기 A", "Construct", 96f, 9f, 6f, 9f, 7f, 4f, ArcaneBurn()),
                Enemy("arcane-sentry-b", "마력 파수기 B", "Construct", 96f, 9f, 6f, 9f, 7f, 4f, ArcaneBurn()),
                Enemy("mana-anomaly", "마나 이상체", "Arcane", 125f, 11f, 8f, 7f, 9f, 6f, ManaRend())
            },
            5 => new List<EnemyTemplate>
            {
                Enemy("rival-slime", "경쟁 던전 슬라임", "Slime", 120f, 10f, 7f, 10f, 6f, 4f, CharacterCombatAbilityCatalog.CreateSlimeBarrier()),
                Enemy("rival-orc", "경쟁 던전 오크", "Orc", 145f, 12f, 11f, 11f, 7f, 5f, CharacterCombatAbilityCatalog.CreateOrcCrush()),
                Enemy("rival-vampire", "경쟁 던전 뱀파이어", "Vampire", 112f, 11f, 8f, 7f, 12f, 7f, CharacterCombatAbilityCatalog.CreateVampireDrain())
            },
            _ => new List<EnemyTemplate>
            {
                Enemy("sealkeeper-a", "봉인 수호자 A", "Truth", 132f, 12f, 9f, 11f, 9f, 5f, SealBrand()),
                Enemy("sealkeeper-b", "봉인 수호자 B", "Truth", 132f, 12f, 9f, 11f, 9f, 5f, SealBrand()),
                Enemy("truth-warden", "진실의 감시자", "Truth", 210f, 15f, 13f, 13f, 11f, 6f, TruthRend())
            }
        };

        int enemyCount = routeNode == null || routeNode.IsBoss
            ? templates.Count
            : Mathf.Min(templates.Count, routeNode.Depth <= 1 ? 1 : 2);
        if (target != null && !target.revealsTruth && pressure.Manpower >= 40f)
        {
            float countMultiplier = Mathf.Clamp(1f - pressure.Manpower * 0.005f, 0.5f, 1f);
            enemyCount = Mathf.Clamp(
                Mathf.CeilToInt(enemyCount * countMultiplier),
                1,
                templates.Count);
        }

        float encounterScale = routeNode == null || routeNode.IsBoss
            ? 1f
            : Mathf.Clamp(routeNode.DangerMultiplier, 0.65f, 1.1f);
        float healthPressureMultiplier = target != null && !target.revealsTruth
            ? Mathf.Clamp(1f - pressure.Manpower * 0.002f, 0.8f, 1f)
            : 1f;
        float armamentPressureMultiplier = target != null && !target.revealsTruth
            ? Mathf.Clamp(1f - pressure.Armament * 0.002f, 0.8f, 1f)
            : 1f;
        float readinessPressureMultiplier = target != null && !target.revealsTruth
            ? Mathf.Clamp(1f - (pressure.Logistics + pressure.Intelligence) * 0.00075f, 0.85f, 1f)
            : 1f;
        return templates.Take(enemyCount).Select((template, index) => new OffenseBattleCombatant(
            $"enemy:{target?.id ?? "unknown"}:{template.Id}:{index}",
            template.Name,
            template.Species,
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(
                template.Health * multipliers.EnemyHealth * encounterScale * healthPressureMultiplier,
                template.Attack * multipliers.EnemyAttack * encounterScale * armamentPressureMultiplier,
                template.Strength * multipliers.EnemyAttack * encounterScale * armamentPressureMultiplier,
                template.Toughness * encounterScale * armamentPressureMultiplier,
                template.Dexterity * multipliers.EnemyInitiative * encounterScale * readinessPressureMultiplier,
                template.MoveSpeed * multipliers.EnemyInitiative * encounterScale * readinessPressureMultiplier),
            template.Health * multipliers.EnemyHealth * encounterScale * healthPressureMultiplier,
            template.Abilities,
            formation: (OffenseFormationSlot)Mathf.Clamp(index, 0, 2))).ToArray();
    }

    private static EnemyTemplate Enemy(
        string id,
        string name,
        string species,
        float health,
        float attack,
        float strength,
        float toughness,
        float dexterity,
        float moveSpeed,
        params CharacterCombatAbilityDefinition[] abilities)
    {
        return new EnemyTemplate(
            id,
            name,
            species,
            health,
            attack,
            strength,
            toughness,
            dexterity,
            moveSpeed,
            abilities);
    }

    private static CharacterCombatAbilityDefinition AimedShot()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.aimed-shot",
            "조준 사격",
            "강한 피해를 주고 행동을 늦춥니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(1.25f),
            new OffenseDelayEffect(2f));
    }

    private static CharacterCombatAbilityDefinition CaptainBreak()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.captain-break",
            "갑옷 파괴",
            "피해와 취약을 적용합니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(1.3f),
            new OffenseVulnerabilityEffect(0.2f));
    }

    private static CharacterCombatAbilityDefinition ArcaneBurn()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.arcane-burn",
            "비전 화상",
            "지속 피해를 남깁니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(0.8f),
            new OffenseDamageOverTimeEffect(5f, 2));
    }

    private static CharacterCombatAbilityDefinition ManaRend()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.mana-rend",
            "마력 절단",
            "큰 피해와 행동 지연을 줍니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(1.45f),
            new OffenseDelayEffect(3f));
    }

    private static CharacterCombatAbilityDefinition SealBrand()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.seal-brand",
            "봉인의 낙인",
            "취약과 지속 피해를 남깁니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(0.9f),
            new OffenseVulnerabilityEffect(0.2f),
            new OffenseDamageOverTimeEffect(6f, 2));
    }

    private static CharacterCombatAbilityDefinition TruthRend()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.truth-rend",
            "진실 절단",
            "강한 피해를 주고 방어를 무너뜨립니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(1.55f),
            new OffenseVulnerabilityEffect(0.3f));
    }

    private sealed class EnemyTemplate
    {
        public EnemyTemplate(
            string id,
            string name,
            string species,
            float health,
            float attack,
            float strength,
            float toughness,
            float dexterity,
            float moveSpeed,
            IEnumerable<CharacterCombatAbilityDefinition> abilities)
        {
            Id = id;
            Name = name;
            Species = species;
            Health = health;
            Attack = attack;
            Strength = strength;
            Toughness = toughness;
            Dexterity = dexterity;
            MoveSpeed = moveSpeed;
            Abilities = abilities?.Where(ability => ability != null).ToArray()
                ?? Array.Empty<CharacterCombatAbilityDefinition>();
        }

        public string Id { get; }
        public string Name { get; }
        public string Species { get; }
        public float Health { get; }
        public float Attack { get; }
        public float Strength { get; }
        public float Toughness { get; }
        public float Dexterity { get; }
        public float MoveSpeed { get; }
        public IReadOnlyList<CharacterCombatAbilityDefinition> Abilities { get; }
    }
}
