using System;
using System.Linq;
using UnityEngine;

public static class OffenseEncounterBalanceRules
{
    public static void ScaleEnemyHealth(
        OffenseBattleCombatant combatant,
        float multiplier)
    {
        if (combatant == null)
        {
            throw new ArgumentNullException(nameof(combatant));
        }
        if (!float.IsFinite(multiplier)
            || multiplier < 0.0001f
            || multiplier > 1_600f)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        }
        if (Mathf.Abs(multiplier - 1f) <= 0.000001f)
        {
            return;
        }

        OffenseBattleStats before = combatant.Stats;
        float healthRatio = combatant.CurrentHealth / Mathf.Max(1f, before.MaxHealth);
        combatant.RestoreStats(new OffenseBattleStats(
            before.MaxHealth * multiplier,
            before.Attack,
            before.Strength,
            before.Toughness,
            before.Dexterity,
            before.MoveSpeed,
            before.Shooting,
            before.Evasion));
        combatant.RestoreHealth(
            combatant.Stats.MaxHealth * healthRatio,
            combatant.TotalDamageTaken);

        CharacterBodyHealthSnapshot body = combatant.CaptureBodyHealth();
        CharacterBodyPartHealthState[] scaled = body.Parts
            .Select(part => new CharacterBodyPartHealthState
            {
                bodyPart = part.bodyPart,
                maxHealth = Mathf.Max(1f, part.maxHealth * multiplier),
                currentHealth = Mathf.Clamp(
                    part.currentHealth * multiplier,
                    0f,
                    Mathf.Max(1f, part.maxHealth * multiplier)),
                bleedingPerSecond = part.bleedingPerSecond
            })
            .ToArray();
        combatant.ApplyBodyHealth(new CharacterBodyHealthSnapshot(
            scaled,
            body.BloodLoss,
            body.Suppression,
            body.Consciousness,
            body.Manipulation,
            body.Mobility,
            body.Downed));
    }
}
