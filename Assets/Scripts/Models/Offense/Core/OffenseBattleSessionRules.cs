using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class OffenseBattleSessionRules
{
    public static CombatStatSnapshot CreateCombatStats(
        OffenseBattleCombatant combatant)
    {
        float manipulation = combatant.Manipulation * combatant.Consciousness;
        float mobility = combatant.Mobility * combatant.Consciousness;
        return new CombatStatSnapshot(
            combatant.Stats.Attack * manipulation,
            combatant.Stats.Shooting * manipulation,
            combatant.Stats.Evasion * mobility,
            combatant.Stats.MoveSpeed * mobility,
            combatant.Stats.Strength * manipulation,
            combatant.Stats.Toughness,
            combatant.Stats.Dexterity * manipulation,
            Mathf.Min(combatant.HealthRatio, combatant.Consciousness));
    }

    public static CombatStatSnapshot CreateModifiedCombatStats(
        OffenseBattleEncounterRules encounterRules,
        OffenseBattleCombatant combatant,
        bool attacking)
    {
        CombatStatSnapshot value = OffenseBattleSessionRules.CreateCombatStats(combatant);
        float accuracy = attacking
            ? encounterRules.GetAccuracyMultiplier(combatant.Team)
            : 1f;
        float sedation = combatant.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.Sedated)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max();
        float activity = 1f - Mathf.Clamp(sedation, 0f, 0.8f);
        return new CombatStatSnapshot(
            value.Melee * accuracy * activity,
            value.Shooting * accuracy * activity,
            value.Evasion * encounterRules.GetMovementMultiplier(combatant.Team),
            value.MoveSpeed
                * encounterRules.GetMovementMultiplier(combatant.Team)
                * activity,
            value.Strength,
            value.Toughness,
            value.Dexterity * accuracy * activity,
            value.HealthMultiplier,
            combatant.ArcanePowerMultiplier,
            hasArcanePowerMultiplier: true);
    }

    public static CombatStatSnapshot CreateAttackStats(
        OffenseBattleEncounterRules encounterRules,
        OffenseBattleCombatant source,
        OffenseBattleCombatant target,
        CombatWeaponSnapshot weapon)
    {
        CombatStatSnapshot value = CreateModifiedCombatStats(
            encounterRules,
            source,
            attacking: true);
        bool resistedCaptureShot = encounterRules.Objective ==
                OffenseEncounterObjective.CaptureLeader
            && source?.Team == OffenseBattleTeam.Allies
            && string.Equals(
                target?.PersistentId,
                encounterRules.ObjectiveCombatantId,
                StringComparison.Ordinal)
            && string.Equals(
                weapon?.AmmunitionItemId,
                "ammo:tranquilizer-dart",
                StringComparison.Ordinal);
        if (!resistedCaptureShot)
        {
            return value;
        }

        float resistance = encounterRules.ObjectiveControlResistanceMultiplier;
        return new CombatStatSnapshot(
            value.Melee / resistance,
            value.Shooting / resistance,
            value.Evasion,
            value.MoveSpeed,
            value.Strength,
            value.Toughness,
            value.Dexterity / resistance,
            value.HealthMultiplier,
            value.ArcanePowerMultiplier,
            value.HasArcanePowerMultiplier);
    }

    public static CombatCoverSnapshot CreateEffectiveCover(
        OffenseBattleCombatant target)
    {
        if (target == null)
        {
            return default;
        }

        float smoke = GetSmokeObscuration(target);
        float blockChance = Mathf.Max(
            target.CoverBlockChance,
            smoke * 0.4f);
        return blockChance <= 0f
            ? default
            : new CombatCoverSnapshot(
                CombatCoverHeight.Low,
                blockChance,
                0f,
                smoke > 0f ? "offense-smoke" : "offense-cover");
    }

    public static float GetVisibilityMultiplier(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target,
        CombatWeaponSnapshot weapon)
    {
        if (weapon?.IsRanged != true)
        {
            return 1f;
        }

        float obscuration = Mathf.Max(
            GetSmokeObscuration(source),
            GetSmokeObscuration(target));
        return Mathf.Clamp(1f - obscuration * 0.65f, 0.35f, 1f);
    }

    private static float GetSmokeObscuration(OffenseBattleCombatant combatant) =>
        combatant?.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.SmokeObscured)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max() ?? 0f;

    public static float GetAttackMultiplier(OffenseBattleCombatant actor)
    {
        if (actor == null) return 1f;
        float support = actor.Statuses
            .Where(value => value.Type == OffenseBattleStatusType.AttackModifier)
            .Sum(value => value.Value);
        float sedation = actor.Statuses
            .Where(value => value.Type == OffenseBattleStatusType.Sedated)
            .Select(value => value.Value)
            .DefaultIfEmpty(0f)
            .Max();
        return Mathf.Max(0.1f, (1f + support) * (1f - Mathf.Clamp01(sedation)));
    }

    public static bool IsConstruct(OffenseBattleCombatant target)
    {
        string species = target?.SpeciesTag ?? string.Empty;
        return species.IndexOf("golem", StringComparison.OrdinalIgnoreCase) >= 0
            || species.IndexOf("construct", StringComparison.OrdinalIgnoreCase) >= 0
            || species.IndexOf("clockwork", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static int GetFormationDistance(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target)
    {
        return 1 + ((int)source.Formation + (int)target.Formation) * 4;
    }

    public static string GetBodyPartName(CombatBodyPart bodyPart)
    {
        return bodyPart switch
        {
            CombatBodyPart.Head => "머리",
            CombatBodyPart.Torso => "몸통",
            CombatBodyPart.LeftArm => "왼팔",
            CombatBodyPart.RightArm => "오른팔",
            CombatBodyPart.LeftLeg => "왼다리",
            CombatBodyPart.RightLeg => "오른다리",
            _ => "몸"
        };
    }

    public static float EstimateAbilityDamageMultiplier(
        CharacterCombatAbilityDefinition ability)
    {
        float estimate = 0f;
        foreach (OffenseCombatEffectModule effect in
                 ability?.Effects ?? Array.Empty<OffenseCombatEffectModule>())
        {
            if (effect is OffenseDamageEffect)
            {
                estimate += 1.1f;
            }
            else if (effect is OffenseVulnerabilityEffect
                     || effect is OffenseDamageOverTimeEffect)
            {
                estimate += 0.3f;
            }
        }

        return estimate;
    }
}
