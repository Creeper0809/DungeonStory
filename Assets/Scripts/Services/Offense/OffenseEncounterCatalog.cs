using System;
using UnityEngine;

// Ally projection remains a pure adapter. Enemy definitions and compositions
// are owned exclusively by the V20 SO catalogs and EnemyEncounterFactory.
public static class OffenseEncounterCatalog
{
    public static string GetEnemySummary(int campaignOrder) =>
        $"SO 전술 조우군 {Mathf.Clamp(campaignOrder, 1, 6)}";

    public static OffenseBattleCombatant CreateAlly(
        CharacterActor actor,
        string persistentId,
        ICharacterPerformanceQuery performance,
        OffenseFormationSlot formation = OffenseFormationSlot.Front,
        float stress = 0f)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (performance == null) throw new ArgumentNullException(nameof(performance));
        actor.EnsureRuntimeState();
        CharacterIdentity identity = actor.Identity;
        float stressMultiplier = Mathf.Lerp(
            1f,
            0.65f,
            Mathf.Clamp01(stress / 100f));
        float maxHealth = Mathf.Max(1f, actor.MaxHealth);
        var combatant = new OffenseBattleCombatant(
            persistentId,
            identity != null ? identity.DisplayName : actor.name,
            identity != null ? identity.SpeciesTag : string.Empty,
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(
                maxHealth,
                5f * performance.Evaluate(actor, "performance:combat:melee-hit").Value
                    * stressMultiplier,
                5f * performance.Evaluate(actor, "performance:combat:melee-power").Value
                    * stressMultiplier,
                5f * performance.Evaluate(actor, "performance:combat:defense-reaction").Value
                    * stressMultiplier,
                5f * performance.Evaluate(actor, CharacterCompositePerformanceIds.PrecisionExecution).Value
                    * stressMultiplier,
                5f * performance.Evaluate(actor, "performance:combat:movement").Value
                    * stressMultiplier,
                5f * performance.Evaluate(actor, "performance:combat:ranged-hit").Value
                    * stressMultiplier,
                5f * performance.Evaluate(actor, "performance:combat:evasion").Value
                    * stressMultiplier),
            Mathf.Clamp(actor.CurrentHealth, 0f, maxHealth),
            CharacterCombatAbilityCatalog.GetAbilities(actor),
            identity?.Data != null ? identity.Data.id : -1,
            formation);
        combatant.SetBluntDamageMultiplier(actor.Stats.ProjectDetailedStat(
            GameplayEffectTargetIds.BluntDamageReceived, 1f).Value);
        return combatant;
    }

    public static OffenseBattleCombatant RestoreAlly(
        CharacterActor actor,
        string persistentId,
        OffenseBattleCombatantPersistenceState saved)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (saved == null) throw new ArgumentNullException(nameof(saved));
        if (string.IsNullOrWhiteSpace(persistentId)
            || !string.Equals(persistentId, saved.persistentId, StringComparison.Ordinal)
            || !IsFiniteNonNegative(saved.maxHealth) || saved.maxHealth < 1f
            || !IsFiniteNonNegative(saved.attack)
            || !IsFiniteNonNegative(saved.strength)
            || !IsFiniteNonNegative(saved.toughness)
            || !IsFiniteNonNegative(saved.dexterity)
            || !IsFiniteNonNegative(saved.moveSpeed)
            || !IsFiniteNonNegative(saved.shooting)
            || !IsFiniteNonNegative(saved.evasion)
            || !IsFiniteNonNegative(saved.currentHealth)
            || saved.currentHealth > saved.maxHealth
            || !saved.hasArcanePowerMultiplier
            || !IsFiniteNonNegative(saved.arcanePowerMultiplier)
            || (saved.hasBluntDamageMultiplier && (!float.IsFinite(saved.bluntDamageMultiplier)
                || saved.bluntDamageMultiplier <= 0f)))
        {
            throw new InvalidOperationException(
                $"Battle ally '{persistentId}' has no exact valid persisted projection.");
        }

        // Battle stats are frozen by the battle owner and already captured.
        // Re-evaluating actor proficiency here reads an unrelated pre-commit
        // narrative aggregate during detached full-world restore.
        actor.EnsureRuntimeState();
        CharacterIdentity identity = actor.Identity;
        OffenseBattleCombatant combatant = new(
            persistentId,
            identity != null ? identity.DisplayName : actor.name,
            identity != null ? identity.SpeciesTag : string.Empty,
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(saved.maxHealth, saved.attack, saved.strength,
                saved.toughness, saved.dexterity, saved.moveSpeed,
                saved.shooting, saved.evasion),
            saved.currentHealth,
            CharacterCombatAbilityCatalog.GetAbilities(actor),
            identity?.Data != null ? identity.Data.id : -1,
            saved.formation,
            saved.participatesInInitiative);
        combatant.SetArcanePowerMultiplier(saved.arcanePowerMultiplier);
        combatant.SetBluntDamageMultiplier(saved.hasBluntDamageMultiplier ? saved.bluntDamageMultiplier : 1f);
        return combatant;
    }

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
}
