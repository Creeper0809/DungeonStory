using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct EnemyTacticalDecision
{
    public EnemyTacticalDecision(
        EnemyTacticalIntentKind intent,
        string targetId,
        string abilityId = "")
    {
        Intent = intent;
        TargetId = targetId?.Trim() ?? string.Empty;
        AbilityId = abilityId?.Trim() ?? string.Empty;
    }

    public EnemyTacticalIntentKind Intent { get; }
    public string TargetId { get; }
    public string AbilityId { get; }
}

public interface IEnemyTacticalDecisionService
{
    EnemyTacticalDecision Decide(
        OffenseBattleSession session,
        EnemyIndividualSaveData individual,
        bool allowAbility = true);
}

public sealed class EnemyTacticalDecisionService :
    IEnemyTacticalDecisionService
{
    private readonly IEnemyArchetypeCatalog archetypes;

    public EnemyTacticalDecisionService(IEnemyArchetypeCatalog archetypes)
    {
        this.archetypes = archetypes
            ?? throw new ArgumentNullException(nameof(archetypes));
    }

    public EnemyTacticalDecision Decide(
        OffenseBattleSession session,
        EnemyIndividualSaveData individual,
        bool allowAbility = true)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (individual == null) throw new ArgumentNullException(nameof(individual));
        OffenseBattleCombatant actor = session.CurrentActor
            ?? throw new InvalidOperationException("The battle has no current actor.");
        if (actor.Team != OffenseBattleTeam.Enemies
            || !string.Equals(
                actor.PersistentId,
                individual.characterId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Tactical input does not match the current enemy actor.");
        }

        EnemyArchetypeDefinitionSO archetype =
            archetypes.Require(individual.enemyArchetypeId);
        EnemyTacticalProfile profile = archetype.tacticalProfile;
        if (actor.HealthRatio <= profile.retreatHealthFraction
            && profile.retreatWeight > 0f)
        {
            return new EnemyTacticalDecision(
                EnemyTacticalIntentKind.Retreat,
                actor.PersistentId);
        }

        OffenseBattleCombatant[] opponents = session.Combatants
            .Where(value => value.Team == OffenseBattleTeam.Allies
                && !value.IsDead
                && !value.IsDowned)
            .OrderBy(value => TargetScore(value, profile))
            .ThenBy(value => StableTie(session, actor, value))
            .ToArray();
        if (opponents.Length == 0)
            return new EnemyTacticalDecision(
                EnemyTacticalIntentKind.Protect,
                actor.PersistentId);

        if (allowAbility)
        {
            CharacterCombatAbilityDefinition ability = actor.Abilities
                .Where(value => actor.GetCooldown(value.Id) <= 0)
                .OrderByDescending(value => AbilityScore(value, profile))
                .ThenBy(value => value.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (ability != null && profile.abilityWeight >= profile.attackWeight)
            {
                string targetId = SelectAbilityTarget(
                    session,
                    actor,
                    opponents,
                    ability);
                if (!string.IsNullOrWhiteSpace(targetId))
                {
                    return new EnemyTacticalDecision(
                        EnemyTacticalIntentKind.UseAbility,
                        targetId,
                        ability.Id);
                }
            }
        }

        OffenseBattleCombatant target = opponents
            .Where(value => session.PreviewBasicAttack(actor, value).Valid)
            .OrderBy(value => TargetScore(value, profile))
            .ThenBy(value => StableTie(session, actor, value))
            .FirstOrDefault();
        if (target != null)
        {
            return new EnemyTacticalDecision(
                EnemyTacticalIntentKind.Attack,
                target.PersistentId);
        }

        return new EnemyTacticalDecision(
            EnemyTacticalIntentKind.Protect,
            actor.PersistentId);
    }

    private static string SelectAbilityTarget(
        OffenseBattleSession session,
        OffenseBattleCombatant actor,
        IReadOnlyList<OffenseBattleCombatant> opponents,
        CharacterCombatAbilityDefinition ability)
    {
        if (ability.TargetRule == OffenseBattleTargetRule.Self)
            return actor.PersistentId;
        if (ability.TargetRule == OffenseBattleTargetRule.Ally)
        {
            return session.Combatants
                .Where(value => value.Team == actor.Team && !value.IsDead)
                .OrderBy(value => value.HealthRatio)
                .ThenBy(value => value.PersistentId, StringComparer.Ordinal)
                .Select(value => value.PersistentId)
                .FirstOrDefault() ?? string.Empty;
        }
        return opponents.FirstOrDefault()?.PersistentId ?? string.Empty;
    }

    private static float TargetScore(
        OffenseBattleCombatant target,
        EnemyTacticalProfile profile)
    {
        float score = target.HealthRatio * 10f - target.Stats.Attack * 0.05f;
        if ((profile.preferredTargetTags ?? new List<string>())
            .Contains("backline", StringComparer.Ordinal)
            && target.Formation == OffenseFormationSlot.Rear)
        {
            score -= 4f;
        }
        if ((profile.preferredTargetTags ?? new List<string>())
            .Contains("fast", StringComparer.Ordinal))
        {
            score -= target.Stats.MoveSpeed * 0.1f;
        }
        return score;
    }

    private static float AbilityScore(
        CharacterCombatAbilityDefinition ability,
        EnemyTacticalProfile profile) =>
        profile.abilityWeight
        + OffenseBattleSessionRules.EstimateAbilityDamageMultiplier(ability);

    private static uint StableTie(
        OffenseBattleSession session,
        OffenseBattleCombatant actor,
        OffenseBattleCombatant target) =>
        PersistentEntityId.GetStableHash32(
            $"{session.BattleId}:{session.RoundNumber}:{actor.PersistentId}:"
            + $"{actor.TurnsStarted}:{target.PersistentId}");
}
