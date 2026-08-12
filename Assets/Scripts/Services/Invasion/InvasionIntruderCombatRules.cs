using System;
using UnityEngine;

internal static class InvasionIntruderCombatRules
{
    public static float EstimateStructureDamage(
        CharacterActor intruder,
        InvasionIntruderSettings settings,
        float meleeDamageMultiplier,
        ICharacterPerformanceQuery performance)
    {
        if (intruder == null)
        {
            return 1f;
        }

        if (performance == null) throw new ArgumentNullException(nameof(performance));
        float attack = 5f * performance.Evaluate(
            intruder,
            "performance:combat:melee-hit").Value;
        float strength = 5f * performance.Evaluate(
            intruder,
            "performance:combat:melee-power").Value;
        return Mathf.Max(
            1f,
            (attack * 0.75f + strength * 0.45f)
            * intruder.GetCombatPowerMultiplier()
            * meleeDamageMultiplier
            * Mathf.Max(0.01f, settings.structureDamageMultiplier));
    }

    public static float CalculateStructuralDamage(
        CharacterActor intruder,
        InvasionIntruderSettings settings,
        float meleeDamageMultiplier,
        float toughness,
        bool enraged,
        ICharacterPerformanceQuery performance)
    {
        float damage = Mathf.Max(
            1f,
            EstimateStructureDamage(intruder, settings, meleeDamageMultiplier, performance)
            - Mathf.Max(0f, toughness) * 0.5f);
        return enraged ? damage * 1.25f : damage;
    }

    public static void TickDefenseStatuses(
        CharacterActor intruder,
        float deltaSeconds,
        IDefenseStatusRuntimeService statusRuntime)
    {
        if (intruder == null || intruder.IsDead)
        {
            return;
        }

        DefenseEffectResolver.TickStatuses(
            intruder,
            deltaSeconds,
            statusRuntime);
    }

    public static string ResolveRaidId(
        InvasionIntruderSettings settings,
        string runtimeId)
    {
        if (settings != null
            && !string.IsNullOrWhiteSpace(settings.raidId))
        {
            return settings.raidId.Trim();
        }

        return !string.IsNullOrWhiteSpace(runtimeId)
            ? runtimeId
            : "invasion:unassigned";
    }

    public static bool IsPostBreachState(InvasionIntruderState state)
    {
        return state != InvasionIntruderState.None
            && state != InvasionIntruderState.Rallying
            && state != InvasionIntruderState.Entering
            && state != InvasionIntruderState.Finished;
    }
}
