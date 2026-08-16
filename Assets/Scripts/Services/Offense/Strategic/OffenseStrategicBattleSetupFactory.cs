using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OffenseStrategicBattleSetupFactory
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
                && combatant.Team == OffenseBattleTeam.Allies
                && combatant.ParticipatesInInitiative)
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
            .Select((enemy, index) => new
            {
                Enemy = enemy,
                Index = index,
                Command = session.CreateStrategicEnemyCommand(
                    enemy.PersistentId,
                    session.LastProcessedCommandId + index + 1)
            })
            .Where(value => value.Command != null)
            .Select(value => new OffenseEnemyIntentStateData
            {
                intentId = $"{session.BattleId}:intent:"
                    + $"{Mathf.Max(session.RoundNumber, turnSequence)}:{value.Index}",
                enemyId = value.Enemy.PersistentId,
                targetCharacterId = value.Command.TargetId,
                actionType = value.Command.ActionType,
                actionId = value.Command.AbilityId,
                displayName = ResolveEnemyActionName(value.Command),
                tacticalTag = ResolveEnemyTacticalTag(
                    value.Command,
                    value.Index),
                executionStages = Mathf.Clamp(1 + value.Index % 3, 1, 3),
                speed = Mathf.Max(
                    1,
                    Mathf.RoundToInt(value.Enemy.Initiative / 5f)),
                threat = Mathf.Max(1, Mathf.RoundToInt(
                    value.Enemy.Stats.Attack
                    + value.Enemy.Stats.Strength * 0.5f))
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
            bool isWeaponBasic = index == 0;
            bool isAdvance = index == 1;
            CharacterCombatAbilityDefinition ability =
                !isWeaponBasic && !isAdvance && abilities.Count > 0
                    ? abilities[(index - 2) % abilities.Count]
                    : null;
            OffenseBattleActionType actionType = isWeaponBasic
                ? OffenseBattleActionType.BasicAttack
                : isAdvance
                    ? OffenseBattleActionType.Advance
                    : ability != null
                        ? OffenseBattleActionType.Ability
                        : index % 2 == 0
                            ? OffenseBattleActionType.Guard
                            : OffenseBattleActionType.Advance;
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
                actionType = actionType,
                sourceSkillId = ability?.Id ?? string.Empty,
                displayName = ability?.DisplayName
                    ?? ResolveFallbackCardName(actionType),
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

    private static string ResolveFallbackCardName(
        OffenseBattleActionType actionType)
    {
        return actionType switch
        {
            OffenseBattleActionType.Advance => "전진",
            OffenseBattleActionType.Guard => "방어 태세",
            _ => "기본 공격"
        };
    }

    private static string ResolveEnemyActionName(
        OffenseBattleCommand command)
    {
        return command.ActionType switch
        {
            OffenseBattleActionType.Advance => "적의 전진",
            OffenseBattleActionType.Guard => "적의 방어",
            OffenseBattleActionType.Ability => "적의 기술",
            OffenseBattleActionType.Reload => "적의 재장전",
            OffenseBattleActionType.SwitchWeapon => "적의 무기 교체",
            _ => "적의 공격"
        };
    }

    private static OffenseTacticalTag ResolveEnemyTacticalTag(
        OffenseBattleCommand command,
        int index)
    {
        return command.ActionType switch
        {
            OffenseBattleActionType.Advance => OffenseTacticalTag.Maneuver,
            OffenseBattleActionType.Guard => OffenseTacticalTag.Support,
            _ => TacticalPattern[index % TacticalPattern.Length]
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
