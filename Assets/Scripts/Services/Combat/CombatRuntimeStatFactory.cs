using System.Linq;
using UnityEngine;

public static class CombatRuntimeStatFactory
{
    public static CombatStatSnapshot Create(
        CharacterActor actor,
        CharacterBodyHealthSnapshot body,
        ICharacterPerformanceQuery performance)
        => Create(actor, body, performance, null, string.Empty, 0f);

    public static CombatStatSnapshot Create(
        CharacterActor actor,
        CharacterBodyHealthSnapshot body,
        ICharacterPerformanceQuery performance,
        ExtremeTraitRuntime extremeTraits,
        string encounterId,
        float elapsedSeconds)
    {
        if (actor == null)
        {
            return default;
        }
        if (performance == null)
            throw new System.ArgumentNullException(nameof(performance));

        float health = Mathf.Clamp01(actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
        float bodyEfficiency = Mathf.Min(
            body.Consciousness,
            Mathf.Lerp(0.5f, 1f, body.Manipulation));
        bool activated = extremeTraits?.TryActivateLastStand(
            actor,
            string.IsNullOrWhiteSpace(encounterId) ? "combat:ambient" : encounterId,
            health,
            body.Consciousness <= 0.20f,
            elapsedSeconds,
            out _) == true;
        bool active = activated
            || extremeTraits?.GetActiveConditionIds(actor, elapsedSeconds)
                .Contains("state:last-stand") == true;
        // Last stand explicitly suppresses pain/critical-health performance
        // penalties. Remaining health still determines death, but must not
        // silently cancel the authored +50% combat clutch multiplier.
        float healthEfficiency = active ? 1f : health * bodyEfficiency;
        float meleeHit = performance.Evaluate(actor, "performance:combat:melee-hit").Value;
        float meleePower = performance.Evaluate(actor, "performance:combat:melee-power").Value;
        float rangedHit = performance.Evaluate(actor, "performance:combat:ranged-hit").Value;
        float evasion = performance.Evaluate(actor, "performance:combat:evasion").Value;
        float movement = performance.Evaluate(actor, "performance:combat:movement").Value;
        float defense = performance.Evaluate(actor, "performance:combat:defense-reaction").Value;
        return new CombatStatSnapshot(
            5f * meleeHit,
            5f * rangedHit,
            5f * evasion,
            5f * movement,
            5f * meleePower,
            5f * defense,
            5f * Mathf.Max(meleeHit, rangedHit),
            healthEfficiency);
    }

    public static CombatStatSnapshot Create(WildlifeActor actor)
    {
        if (actor == null)
        {
            return default;
        }

        float health = Mathf.Clamp01(actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
        float danger = actor.IsDangerous ? 1f : 0.65f;
        float aggression = Mathf.Clamp01(actor.Aggression);
        return new CombatStatSnapshot(
            3f + actor.RetaliationDamage * 0.65f,
            0f,
            3f + actor.FearSensitivity * 2f,
            5f * actor.CombatMobility,
            3f + actor.RetaliationDamage * 0.4f,
            4f + actor.MaxHealth * 0.08f,
            4f + aggression * 5f,
            health * danger);
    }
}
