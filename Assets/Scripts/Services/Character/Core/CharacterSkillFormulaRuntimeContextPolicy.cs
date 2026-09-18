using System;
using System.Collections.Generic;

/// <summary>
/// Declares which formula capability axes are fully consumed by each existing
/// CharacterSkill execution path. This is a new-generation eligibility policy;
/// restored skills keep their versioned validation and runtime behavior.
/// </summary>
public static class CharacterSkillFormulaRuntimeContextPolicy
{
    [Flags]
    private enum ConsumingContext
    {
        None = 0,
        ManualCombat = 1 << 0,
        ManualWork = 1 << 1,
        PassiveOutsideCombat = 1 << 2,
        DefenseUltimate = 1 << 3,
        ManagementUltimate = 1 << 4
    }

    // Each registered capability must opt in to every execution path that consumes
    // all of its authored formula axes. A missing/future capability therefore fails closed.
    private static readonly IReadOnlyDictionary<string, ConsumingContext> ContextsByCapability =
        BuildContextsByCapability();

    public static bool ConsumesAllAppliedAxes(
        CharacterSkillKind kind,
        CharacterSkillCandidateRule rule,
        CharacterSkillModuleRule module)
    {
        if (rule == null)
        {
            return false;
        }

        return ConsumesAllAppliedAxes(
            kind,
            rule.trigger,
            rule.ultimateDomain,
            module);
    }

    public static bool ConsumesAllAppliedAxes(
        CharacterSkillKind kind,
        CharacterSkillTrigger trigger,
        CharacterUltimateDomain ultimateDomain,
        CharacterSkillModuleRule module)
    {
        if (module == null)
        {
            return false;
        }

        CharacterSkillModuleCapabilityDescriptor descriptor =
            CharacterSkillModuleCapabilityRegistry.Require(module);
        if (!ContextsByCapability.TryGetValue(
                descriptor.CapabilityId,
                out ConsumingContext declaredContexts))
        {
            return false;
        }

        ConsumingContext context = ResolveContext(kind, trigger, ultimateDomain);
        if (context == ConsumingContext.None
            || (declaredContexts & context) == ConsumingContext.None)
        {
            return false;
        }

        // Passive management consumption is additionally trigger-specific and remains
        // owned by the capability registry's authored reachability declaration.
        return context != ConsumingContext.PassiveOutsideCombat
            || descriptor.Domain != CharacterSkillCapabilityDomain.Management
            || descriptor.IsManagementModuleReachable(kind, trigger);
    }

    private static ConsumingContext ResolveContext(
        CharacterSkillKind kind,
        CharacterSkillTrigger trigger,
        CharacterUltimateDomain ultimateDomain)
    {
        if (kind == CharacterSkillKind.Active
            && ultimateDomain == CharacterUltimateDomain.None)
        {
            return trigger switch
            {
                CharacterSkillTrigger.ManualCombat => ConsumingContext.ManualCombat,
                CharacterSkillTrigger.ManualWork => ConsumingContext.ManualWork,
                _ => ConsumingContext.None
            };
        }

        if (kind == CharacterSkillKind.Passive
            && ultimateDomain == CharacterUltimateDomain.None)
        {
            return trigger switch
            {
                CharacterSkillTrigger.BattleStarted
                    or CharacterSkillTrigger.DamageTaken
                    or CharacterSkillTrigger.EnemyDefeated
                    or CharacterSkillTrigger.BattleCompleted
                    or CharacterSkillTrigger.InvasionStarted
                    or CharacterSkillTrigger.WorkStarted
                    or CharacterSkillTrigger.WorkCompleted
                    or CharacterSkillTrigger.NeedChanged
                    or CharacterSkillTrigger.MoodChanged
                    or CharacterSkillTrigger.RelationshipChanged
                    or CharacterSkillTrigger.OperatingDayStarted =>
                        ConsumingContext.PassiveOutsideCombat,
                _ => ConsumingContext.None
            };
        }

        if (kind != CharacterSkillKind.Ultimate)
        {
            return ConsumingContext.None;
        }

        return (ultimateDomain, trigger) switch
        {
            (CharacterUltimateDomain.Offense, CharacterSkillTrigger.ManualCombat) =>
                ConsumingContext.ManualCombat,
            (CharacterUltimateDomain.Defense, CharacterSkillTrigger.InvasionStarted) =>
                ConsumingContext.DefenseUltimate,
            (CharacterUltimateDomain.Management, CharacterSkillTrigger.OperatingDayStarted) =>
                ConsumingContext.ManagementUltimate,
            _ => ConsumingContext.None
        };
    }

    private static IReadOnlyDictionary<string, ConsumingContext> BuildContextsByCapability()
    {
        Dictionary<string, ConsumingContext> values =
            new Dictionary<string, ConsumingContext>(StringComparer.Ordinal);

        Add(values, ConsumingContext.ManualCombat,
            "damage", "guard", "buff", "cleanse", "protect", "vulnerability",
            "delay", "debuff", "multi_target", "reposition", "cooldown_adjust");
        Add(values,
            ConsumingContext.ManualCombat
            | ConsumingContext.PassiveOutsideCombat,
            "heal");
        Add(values,
            ConsumingContext.ManualCombat
            | ConsumingContext.DefenseUltimate,
            "dot", "conditional_amplify");

        Add(values,
            ConsumingContext.ManualWork
            | ConsumingContext.PassiveOutsideCombat
            | ConsumingContext.ManagementUltimate,
            "work_speed", "output", "repair", "stock", "research", "revenue",
            "cleaning", "needs", "mood", "relationship");

        return values;
    }

    private static void Add(
        IDictionary<string, ConsumingContext> values,
        ConsumingContext contexts,
        params string[] capabilityIds)
    {
        foreach (string capabilityId in capabilityIds)
        {
            if (!values.TryAdd(capabilityId, contexts))
            {
                throw new InvalidOperationException(
                    $"Duplicate formula runtime-context capability '{capabilityId}'.");
            }
        }
    }
}
