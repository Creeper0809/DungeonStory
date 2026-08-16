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

public sealed class EnemyTacticalDecisionService : IEnemyTacticalDecisionService
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
            || !string.Equals(actor.PersistentId, individual.characterId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Tactical input does not match the current enemy actor.");
        }

        EnemyArchetypeDefinitionSO archetype =
            archetypes.Require(individual.enemyArchetypeId);
        EnemyTacticalProfile profile = archetype.tacticalProfile;
        EnemyBossPhaseRecord phase = ResolveActivePhase(archetype, actor.HealthRatio);
        bool desperate = string.Equals(
            phase?.tacticalProfileOverrideTag,
            "desperate",
            StringComparison.OrdinalIgnoreCase);
        float attackWeight = profile.attackWeight + (desperate ? 2f : 0f);
        float protectWeight = profile.protectWeight + (desperate ? 1f : 0f);
        float abilityWeight = profile.abilityWeight + (desperate ? 3f : 0f);

        OffenseBattleCombatant[] opponents = session.Combatants
            .Where(value => value.Team == OffenseBattleTeam.Allies
                && !value.IsDead
                && !value.IsDowned)
            .OrderBy(value => TargetScore(value, profile))
            .ThenBy(value => StableTie(session, actor, value))
            .ToArray();
        if (opponents.Length == 0)
        {
            return new EnemyTacticalDecision(
                EnemyTacticalIntentKind.Protect,
                actor.PersistentId);
        }

        OffenseBattleCombatant attackTarget = null;
        float bestAttackUtility = float.NegativeInfinity;
        foreach (OffenseBattleCombatant candidate in opponents)
        {
            CombatAttackPreview preview = session.PreviewBasicAttack(actor, candidate);
            if (!preview.Valid)
            {
                continue;
            }

            float utility = attackWeight
                + preview.ExpectedDamage / Math.Max(1f, candidate.CurrentHealth) * 5f
                - TargetScore(candidate, profile) * 0.05f;
            if (utility > bestAttackUtility)
            {
                bestAttackUtility = utility;
                attackTarget = candidate;
            }
        }

        CharacterCombatAbilityDefinition bestAbility = null;
        string bestAbilityTargetId = string.Empty;
        float bestAbilityUtility = float.NegativeInfinity;
        if (allowAbility && IsPositionAllowed(actor.Formation, OffenseFormationMask.Any))
        {
            foreach (CharacterCombatAbilityDefinition ability in actor.Abilities
                .Where(value => actor.GetCooldown(value.Id) <= 0)
                .Where(value => IsPositionAllowed(actor.Formation, value.UsableFrom))
                .OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                OffenseBattleCombatant abilityTarget = SelectAbilityTarget(
                    session,
                    actor,
                    opponents,
                    ability,
                    profile);
                if (abilityTarget == null)
                {
                    continue;
                }

                float utility = AbilityUtility(
                    session,
                    actor,
                    abilityTarget,
                    ability,
                    profile,
                    abilityWeight,
                    protectWeight);
                if (phase != null
                    && (phase.abilityIds ?? new List<string>())
                        .Contains(ability.Id, StringComparer.Ordinal))
                {
                    utility += 5f;
                }
                if (utility > bestAbilityUtility)
                {
                    bestAbilityUtility = utility;
                    bestAbility = ability;
                    bestAbilityTargetId = abilityTarget.PersistentId;
                }
            }
        }

        float lowestFriendlyHealth = session.Combatants
            // Actor health already contributes through the self-preservation
            // term below. Including the actor here applies low health twice,
            // causing Protect to dominate Retreat for every authored
            // non-boss profile (protect=1, retreat=2) even below its retreat
            // threshold. This term represents allies the actor could protect.
            .Where(value => value.Team == actor.Team
                && !value.IsDead
                && !string.Equals(
                    value.PersistentId,
                    actor.PersistentId,
                    StringComparison.Ordinal))
            .Select(value => value.HealthRatio)
            .DefaultIfEmpty(1f)
            .Min();
        float protectUtility = protectWeight
            + (1f - actor.HealthRatio) * 4f
            + (1f - lowestFriendlyHealth) * 3f;
        float retreatUtility = profile.retreatWeight * (1f - actor.HealthRatio) * 3f;
        if (actor.HealthRatio <= profile.retreatHealthFraction
            && profile.retreatWeight > 0f
            && retreatUtility >= Math.Max(
                protectUtility,
                Math.Max(bestAttackUtility, bestAbilityUtility)))
        {
            return new EnemyTacticalDecision(
                EnemyTacticalIntentKind.Retreat,
                actor.PersistentId);
        }

        if (bestAbility != null
            && bestAbilityUtility >= Math.Max(bestAttackUtility, protectUtility))
        {
            return new EnemyTacticalDecision(
                EnemyTacticalIntentKind.UseAbility,
                bestAbilityTargetId,
                bestAbility.Id);
        }
        if (attackTarget == null
            && actor.Formation != OffenseFormationSlot.Front)
        {
            return new EnemyTacticalDecision(
                EnemyTacticalIntentKind.Move,
                actor.PersistentId);
        }
        if (protectUtility > bestAttackUtility || attackTarget == null)
        {
            return new EnemyTacticalDecision(
                EnemyTacticalIntentKind.Protect,
                actor.PersistentId);
        }

        return new EnemyTacticalDecision(
            EnemyTacticalIntentKind.Attack,
            attackTarget.PersistentId);
    }

    private static EnemyBossPhaseRecord ResolveActivePhase(
        EnemyArchetypeDefinitionSO archetype,
        float healthRatio) =>
        (archetype?.bossPhases ?? new List<EnemyBossPhaseRecord>())
            .Where(value => value != null && healthRatio <= value.healthThreshold)
            .OrderBy(value => value.healthThreshold)
            .FirstOrDefault();

    private static OffenseBattleCombatant SelectAbilityTarget(
        OffenseBattleSession session,
        OffenseBattleCombatant actor,
        IReadOnlyList<OffenseBattleCombatant> opponents,
        CharacterCombatAbilityDefinition ability,
        EnemyTacticalProfile profile)
    {
        IEnumerable<OffenseBattleCombatant> candidates = ability.TargetRule switch
        {
            OffenseBattleTargetRule.Self => new[] { actor },
            OffenseBattleTargetRule.Ally => session.Combatants
                .Where(value => value.Team == actor.Team && !value.IsDead),
            _ => opponents
        };
        candidates = candidates.Where(value =>
            IsPositionAllowed(value.Formation, ability.TargetPositions));
        return ability.TargetRule == OffenseBattleTargetRule.Enemy
            ? candidates
                .OrderBy(value => TargetScore(value, profile))
                .ThenBy(value => StableTie(session, actor, value))
                .FirstOrDefault()
            : candidates
                .OrderBy(value => value.HealthRatio)
                .ThenBy(value => value.PersistentId, StringComparer.Ordinal)
                .FirstOrDefault();
    }

    private static float AbilityUtility(
        OffenseBattleSession session,
        OffenseBattleCombatant actor,
        OffenseBattleCombatant target,
        CharacterCombatAbilityDefinition ability,
        EnemyTacticalProfile profile,
        float abilityWeight,
        float protectWeight)
    {
        float utility = abilityWeight
            + OffenseBattleSessionRules.EstimateAbilityDamageMultiplier(ability) * 2f;
        foreach (OffenseCombatEffectModule effect in ability.Effects)
        {
            switch (effect)
            {
                case OffenseHealEffect:
                    utility += (1f - target.HealthRatio) * 8f;
                    break;
                case OffenseGuardEffect:
                case OffenseSummonEffect:
                    utility += protectWeight * 0.5f + (1f - target.HealthRatio) * 5f;
                    break;
                case OffenseSmokeEffect:
                    utility += session.Combatants.Count(value =>
                        value.Team != actor.Team
                        && !value.IsDead
                        && value.Weapon?.IsRanged == true) * 0.75f;
                    if (target.Statuses.Any(value =>
                            value.Type == OffenseBattleStatusType.SmokeObscured))
                    {
                        utility -= 8f;
                    }
                    break;
                case OffenseCleanseEffect:
                    utility += target.Statuses.Count * 0.5f;
                    break;
            }
        }

        if (ability.TargetRule == OffenseBattleTargetRule.Enemy)
        {
            utility -= TargetScore(target, profile) * 0.05f;
        }
        return utility;
    }

    private static float TargetScore(
        OffenseBattleCombatant target,
        EnemyTacticalProfile profile)
    {
        float score = target.HealthRatio * 10f - target.Stats.Attack * 0.05f;
        foreach (string tag in profile.preferredTargetTags ?? new List<string>())
        {
            if (MatchesTargetTag(target, tag))
            {
                score -= 4f;
            }
        }
        foreach (string tag in profile.avoidedTargetTags ?? new List<string>())
        {
            if (MatchesTargetTag(target, tag))
            {
                score += 6f;
            }
        }
        return score;
    }

    private static bool MatchesTargetTag(
        OffenseBattleCombatant target,
        string tag) => (tag ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "nearest" => target.Formation == OffenseFormationSlot.Front,
            "backline" => target.Formation == OffenseFormationSlot.Rear,
            "fast" => target.Stats.MoveSpeed >= 6f,
            "shielded" => target.Shield.IsValid,
            "armored" => target.Armor?.Count > 0,
            "guarded" => target.Statuses.Any(value =>
                value.Type == OffenseBattleStatusType.Guard
                || value.Type == OffenseBattleStatusType.SummonedGuard),
            "low-health" => target.HealthRatio <= 0.35f,
            "construct" => target.SpeciesTag.IndexOf(
                "golem",
                StringComparison.OrdinalIgnoreCase) >= 0
                || target.SpeciesTag.IndexOf(
                    "construct",
                    StringComparison.OrdinalIgnoreCase) >= 0,
            _ => false
        };

    private static bool IsPositionAllowed(
        OffenseFormationSlot formation,
        OffenseFormationMask mask)
    {
        OffenseFormationMask flag = formation switch
        {
            OffenseFormationSlot.Front => OffenseFormationMask.Front,
            OffenseFormationSlot.Middle => OffenseFormationMask.Middle,
            _ => OffenseFormationMask.Rear
        };
        return (mask & flag) != 0;
    }

    private static uint StableTie(
        OffenseBattleSession session,
        OffenseBattleCombatant actor,
        OffenseBattleCombatant target) =>
        PersistentEntityId.GetStableHash32(
            $"{session.BattleId}:{session.RoundNumber}:{actor.PersistentId}:"
            + $"{actor.TurnsStarted}:{target.PersistentId}");
}
