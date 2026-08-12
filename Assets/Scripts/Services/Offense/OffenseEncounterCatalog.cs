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
        return new OffenseBattleCombatant(
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
    }
}
