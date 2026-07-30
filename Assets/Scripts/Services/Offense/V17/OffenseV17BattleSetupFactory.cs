using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OffenseV17BattleSetupFactory
{
    private static readonly OffenseTacticalTag[] TacticalPattern =
    {
        OffenseTacticalTag.Intercept,
        OffenseTacticalTag.Maneuver,
        OffenseTacticalTag.Break,
        OffenseTacticalTag.Support,
        OffenseTacticalTag.Execute,
        OffenseTacticalTag.Intercept,
        OffenseTacticalTag.Maneuver,
        OffenseTacticalTag.Break
    };

    public static IReadOnlyList<OffenseBattleMemberDeckSeed> CreateMemberDecks(
        OffenseBattleSession session)
    {
        if (session == null)
        {
            return Array.Empty<OffenseBattleMemberDeckSeed>();
        }

        return session.Combatants
            .Where(combatant => combatant != null
                && combatant.Team == OffenseBattleTeam.Allies)
            .Take(5)
            .Select((combatant, memberIndex) => new OffenseBattleMemberDeckSeed
            {
                characterId = combatant.PersistentId,
                formation = (OffenseFormationPosition)Mathf.Clamp(
                    memberIndex,
                    0,
                    5),
                cards = CreateCards(combatant)
            })
            .ToArray();
    }

    public static IReadOnlyList<OffenseEnemyIntentStateData> CreateEnemyIntents(
        OffenseBattleSession session,
        int turnSequence = 0)
    {
        if (session == null)
        {
            return Array.Empty<OffenseEnemyIntentStateData>();
        }

        OffenseBattleCombatant[] allies = session.Combatants
            .Where(combatant => combatant != null
                && combatant.Team == OffenseBattleTeam.Allies
                && combatant.CanTakeTurn)
            .ToArray();
        if (allies.Length == 0)
        {
            return Array.Empty<OffenseEnemyIntentStateData>();
        }

        return session.Combatants
            .Where(combatant => combatant != null
                && combatant.Team == OffenseBattleTeam.Enemies
                && combatant.CanTakeTurn)
            .Select((enemy, index) => new OffenseEnemyIntentStateData
            {
                intentId = $"{session.BattleId}:intent:"
                    + $"{Mathf.Max(session.RoundNumber, turnSequence)}:{index}",
                enemyId = enemy.PersistentId,
                targetCharacterId = allies[index % allies.Length].PersistentId,
                actionId = string.Empty,
                displayName = "적의 공격",
                tacticalTag = TacticalPattern[index % TacticalPattern.Length],
                executionStages = Mathf.Clamp(1 + index % 3, 1, 3),
                speed = Mathf.Max(1, Mathf.RoundToInt(enemy.Initiative / 5f)),
                threat = Mathf.Max(1, Mathf.RoundToInt(
                    enemy.Stats.Attack + enemy.Stats.Strength * 0.5f))
            })
            .ToArray();
    }

    private static List<OffenseCommandCardStateData> CreateCards(
        OffenseBattleCombatant combatant)
    {
        IReadOnlyList<CharacterCombatAbilityDefinition> abilities =
            combatant.Abilities;
        List<OffenseCommandCardStateData> cards =
            new List<OffenseCommandCardStateData>(8);
        for (int index = 0; index < 8; index++)
        {
            bool isWeaponBasic = index < 2;
            CharacterCombatAbilityDefinition ability =
                !isWeaponBasic && abilities.Count > 0
                    ? abilities[(index - 2) % abilities.Count]
                    : null;
            int tier = index < 2
                ? 0
                : index < 5
                    ? 1
                    : index < 7
                        ? 2
                        : 3;
            cards.Add(new OffenseCommandCardStateData
            {
                instanceId = $"{combatant.PersistentId}:card:{index}",
                sourceSkillId = ability?.Id ?? string.Empty,
                displayName = ability?.DisplayName
                    ?? ResolveFallbackCardName(index, tier),
                tacticalTag = TacticalPattern[index],
                damageType = ResolveDamageType(index),
                executionStages = Mathf.Clamp(1 + tier / 2, 1, 3),
                speed = Mathf.Max(
                    1,
                    Mathf.RoundToInt(combatant.Initiative / 5f) + 2 - tier),
                power = Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        combatant.Stats.Attack
                        + combatant.Stats.Strength * 0.45f)
                    + tier * 2)
            });
        }

        return cards;
    }

    private static string ResolveFallbackCardName(int index, int tier)
    {
        if (index == 0)
        {
            return "기본 공격";
        }

        if (index == 1)
        {
            return "견제 공격";
        }

        return tier switch
        {
            1 => $"약한 기술 {index - 1}",
            2 => $"중간 기술 {index - 4}",
            _ => "강한 기술"
        };
    }

    private static CombatDamageType ResolveDamageType(int index)
    {
        return (index % 3) switch
        {
            0 => CombatDamageType.Slash,
            1 => CombatDamageType.Pierce,
            _ => CombatDamageType.Blunt
        };
    }
}
