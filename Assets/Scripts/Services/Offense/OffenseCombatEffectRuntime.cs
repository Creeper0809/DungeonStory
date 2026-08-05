using System;
using System.Collections.Generic;
using System.Linq;

internal static class OffenseCombatEffectRuntime
{
    public static void Apply(
        OffenseCombatEffectModule effect,
        OffenseBattleEffectContext context)
    {
        if (effect == null)
        {
            return;
        }
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        switch (effect)
        {
            case OffenseDamageEffect damage:
                ApplyDamage(damage, context);
                return;
            case OffenseHealEffect heal:
                ApplyHeal(heal, context);
                return;
            case OffenseGuardEffect guard:
                AddStatus(
                    context,
                    OffenseBattleStatusType.Guard,
                    guard.DamageReduction,
                    guard.Turns,
                    $"guard:{context.Source.PersistentId}");
                return;
            case OffenseDamageOverTimeEffect damageOverTime:
                AddStatus(
                    context,
                    OffenseBattleStatusType.DamageOverTime,
                    damageOverTime.DamagePerTurn,
                    damageOverTime.Turns,
                    $"dot:{context.Source.PersistentId}");
                return;
            case OffenseVulnerabilityEffect vulnerability:
                AddStatus(
                    context,
                    OffenseBattleStatusType.Vulnerability,
                    vulnerability.IncreasedDamage,
                    vulnerability.Turns,
                    $"vulnerable:{context.Source.PersistentId}");
                return;
            case OffenseDelayEffect delay:
                context.Session.Delay(context.Target, delay.InitiativePenalty);
                return;
            case OffenseAttackModifierEffect attackModifier:
                AddStatus(
                    context,
                    OffenseBattleStatusType.AttackModifier,
                    attackModifier.MultiplierDelta,
                    attackModifier.Turns,
                    $"attack-modifier:{context.Source.PersistentId}:{context.Target.PersistentId}");
                return;
            case OffenseCleanseEffect cleanse:
                context.Session.Cleanse(context.Target, cleanse.MaximumStatuses);
                return;
            case OffenseRepositionEffect reposition:
                context.Session.Reposition(context.Target, reposition.Offset);
                return;
            case OffenseConditionalAmplifyEffect amplify:
                ApplyConditionalAmplify(amplify, context);
                return;
            case OffenseCooldownAdjustEffect cooldown:
                context.Session.AdjustCooldowns(context.Target, cooldown.TurnDelta);
                return;
            case OffenseMultiTargetEffect multiTarget:
                ApplyMultiTarget(multiTarget, context);
                return;
            default:
                throw new InvalidOperationException(
                    $"No offense runtime adapter is registered for {effect.GetType().FullName}.");
        }
    }

    private static void ApplyDamage(
        OffenseDamageEffect effect,
        OffenseBattleEffectContext context)
    {
        for (int i = 0; i < effect.HitCount && !context.Target.IsDead; i++)
        {
            float damage = context.Session.CalculateBasicDamage(
                    context.Source,
                    context.Target)
                * effect.BasicDamageMultiplier
                + effect.FlatDamage;
            context.DamageDealt += context.Session.ApplyDamage(
                context.Source,
                context.Target,
                damage);
        }
    }

    private static void ApplyHeal(
        OffenseHealEffect effect,
        OffenseBattleEffectContext context)
    {
        if (effect.FlatAmount > 0f)
        {
            context.Session.Heal(context.Target, effect.FlatAmount);
        }

        float drainHeal = context.DamageDealt * effect.DamageDealtRatio;
        if (drainHeal > 0f)
        {
            context.Session.Heal(context.Source, drainHeal);
        }
    }

    private static void ApplyConditionalAmplify(
        OffenseConditionalAmplifyEffect effect,
        OffenseBattleEffectContext context)
    {
        if (context.Target.HealthRatio > effect.HealthThreshold)
        {
            return;
        }

        float damage = context.Session.CalculateBasicDamage(
                context.Source,
                context.Target)
            * effect.ExtraDamageMultiplier;
        context.DamageDealt += context.Session.ApplyDamage(
            context.Source,
            context.Target,
            damage);
    }

    private static void ApplyMultiTarget(
        OffenseMultiTargetEffect effect,
        OffenseBattleEffectContext context)
    {
        IEnumerable<OffenseBattleCombatant> additional = context.Session
            .GetLivingTeam(context.Target.Team)
            .Where(target => target != context.Target)
            .Take(Math.Max(1, effect.TargetCount - 1));
        foreach (OffenseBattleCombatant target in additional)
        {
            float damage = context.Session.CalculateBasicDamage(
                    context.Source,
                    target)
                * effect.SplashMultiplier;
            context.DamageDealt += context.Session.ApplyDamage(
                context.Source,
                target,
                damage);
        }
    }

    private static void AddStatus(
        OffenseBattleEffectContext context,
        OffenseBattleStatusType statusType,
        float magnitude,
        int turns,
        string stackKey)
    {
        context.Session.AddStatus(
            context.Target,
            statusType,
            magnitude,
            turns,
            context.Source.PersistentId,
            stackKey);
    }
}
