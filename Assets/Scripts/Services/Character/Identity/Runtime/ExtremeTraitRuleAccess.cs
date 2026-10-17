using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>Stateless access to the existing identity-rule authority and fixed rule calculations.</summary>
internal static class ExtremeTraitRuleAccess
{
    internal static void AddTimedAftermath<TRule, TState>(CharacterIdentityStateStore states,
        CharacterActor actor,
        int traitId,
        string ruleId,
        string conditionId,
        Func<TState, float> deadline,
        float now,
        ICollection<string> result)
        where TRule : CharacterIdentityRule
        where TState : class, new()
    {
        TRule rule = ResolveRule<TRule>(actor, traitId, ruleId, out CharacterTraitSO trait);
        if (rule == null) return;
        if (deadline(Read<TState>(states, actor, trait, ruleId)) > now) result.Add(conditionId);
    }

    internal static bool FinitePositive(float value)
    =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    internal static TState Read<TState>(CharacterIdentityStateStore states, CharacterActor actor, CharacterTraitSO trait, string ruleId)
        where TState : class, new()
    {
        if (states.TryGet(
                ActorId(actor),
                trait.DefinitionId.Value,
                ruleId,
                out CharacterIdentityRuleStateSaveData saved)
            && !string.IsNullOrWhiteSpace(saved.statePayload))
            return JsonUtility.FromJson<TState>(saved.statePayload) ?? new TState();
        return new TState();
    }

    [GameplayInternalOnly("Capability owners write only their current identity rule payload.",
        "LastStandTraitRuntime|ForbiddenResearchLeapTraitRuntime|MiracleSurgeryTraitRuntime|GoldenHarvestTraitRuntime|ProductionLimitBreakTraitRuntime|ArcaneOverchargeTraitRuntime")]
    internal static void Write<TState>(CharacterIdentityStateStore states,
        CharacterActor actor,
        CharacterTraitSO trait,
        string ruleId,
        TState state)
        where TState : class
    =>
        states.Set(
            ActorId(actor),
            trait.DefinitionId.Value,
            ruleId,
            1,
            JsonUtility.ToJson(state));

    internal static TRule ResolveRule<TRule>(
        CharacterActor actor,
        int traitId,
        string ruleId,
        out CharacterTraitSO trait)
        where TRule : CharacterIdentityRule
    {
        trait = actor?.Progression?.ResolveSelectedTraits()
            .FirstOrDefault(value => value != null && value.id == traitId);
        return trait?.identityRules?
            .OfType<TRule>()
            .FirstOrDefault(value => string.Equals(value.ruleId, ruleId, StringComparison.Ordinal));
    }

    internal static string ActorId(CharacterActor actor)
    =>
        Required(actor?.Identity?.PersistentId, "actor");

    internal static float DaysToSeconds(int days)
    =>
        Mathf.Max(1, days) * GameCalendarRules.SecondsPerDay;

    internal static float HoursToSeconds(int hours)
    =>
        Mathf.Max(1, hours) * (GameCalendarRules.SecondsPerDay / 24f);

    internal static float Roll01(ulong hash)
    =>
        (hash % 1_000_000UL) / 1_000_000f;

    internal static ulong FixedHash(ulong runSeed, params string[] parts)
    {
        ulong hash = 14695981039346656037UL;
        Append(ref hash, runSeed.ToString(CultureInfo.InvariantCulture));
        foreach (string part in parts) Append(ref hash, part);
        return hash;
    }

    internal static void Append(ref ulong hash, string value)
    {
        foreach (char character in value?.Trim() ?? string.Empty)
        {
            unchecked
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }
        }
        unchecked
        {
            hash ^= 0x1FUL;
            hash *= 1099511628211UL;
        }
    }

    internal static string Required(string value, string label)
    =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Extreme trait {label} is required.")
            : value.Trim();
}
