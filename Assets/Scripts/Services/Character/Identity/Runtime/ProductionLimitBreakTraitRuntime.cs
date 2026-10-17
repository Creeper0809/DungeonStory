using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static ExtremeTraitRuleAccess;
using static ExtremeTraitRuleIds;

internal sealed class ProductionLimitBreakTraitRuntime
{
    private readonly CharacterIdentityStateStore states;

    internal ProductionLimitBreakTraitRuntime(CharacterIdentityStateStore states) =>
        this.states = states ?? throw new ArgumentNullException(nameof(states));

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryBeginProductionLimitBreak(
        CharacterActor actor,
        string batchId,
        float elapsedSeconds,
        out ProductionLimitBreakRule rule)
    {
        rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(states,
            actor, trait, ProductionLimitBreakRuleId);
        float now = Mathf.Max(0f, elapsedSeconds);
        if (state.active || state.aftermathUntilSeconds > now) return false;
        state.batchId = Required(batchId, nameof(batchId));
        state.active = true;
        state.activeLeaseUntilSeconds = now + 5f;
        Write(states, actor, trait, ProductionLimitBreakRuleId, state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool CanBeginProductionLimitBreak(
        CharacterActor actor,
        string batchId,
        float elapsedSeconds)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null)
            return false;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(states,
            actor, trait, ProductionLimitBreakRuleId);
        return !string.IsNullOrWhiteSpace(batchId)
            && !state.active
            && state.aftermathUntilSeconds <= Mathf.Max(0f, elapsedSeconds);
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool CanConfigureProductionLimitBreak(
        CharacterActor actor,
        float elapsedSeconds)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null)
            return false;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(states,
            actor, trait, ProductionLimitBreakRuleId);
        return !state.active
            && state.aftermathUntilSeconds <= Mathf.Max(0f, elapsedSeconds);
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool RefreshProductionLimitBreak(
        CharacterActor actor,
        string batchId,
        float elapsedSeconds)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null)
            return false;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(states,
            actor, trait, ProductionLimitBreakRuleId);
        if (!state.active
            || !string.Equals(
                state.batchId,
                batchId?.Trim(),
                StringComparison.Ordinal))
            return false;
        state.activeLeaseUntilSeconds = Mathf.Max(0f, elapsedSeconds) + 5f;
        Write(states, actor, trait, ProductionLimitBreakRuleId, state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool ExpireProductionLimitBreak(
        CharacterActor actor,
        float elapsedSeconds)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null)
            return false;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(states,
            actor, trait, ProductionLimitBreakRuleId);
        float now = Mathf.Max(0f, elapsedSeconds);
        if (!state.active || state.activeLeaseUntilSeconds >= now)
            return false;
        state.active = false;
        state.activeLeaseUntilSeconds = 0f;
        state.aftermathUntilSeconds = now + DaysToSeconds(rule.aftermathDays);
        Write(states, actor, trait, ProductionLimitBreakRuleId, state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal void EndProductionLimitBreak(CharacterActor actor, string batchId, float elapsedSeconds)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null) return;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(states,
            actor, trait, ProductionLimitBreakRuleId);
        if (!state.active || !string.Equals(state.batchId, batchId?.Trim(), StringComparison.Ordinal))
            return;
        state.active = false;
        state.activeLeaseUntilSeconds = 0f;
        state.aftermathUntilSeconds = Mathf.Max(0f, elapsedSeconds)
            + DaysToSeconds(rule.aftermathDays);
        Write(states, actor, trait, ProductionLimitBreakRuleId, state);
    }

    internal void AddConditions(CharacterActor actor, float now, ICollection<string> result)
    {
        ProductionLimitBreakRule rule = ResolveRule<ProductionLimitBreakRule>(
            actor, 305, ProductionLimitBreakRuleId, out CharacterTraitSO trait);
        if (rule == null) return;
        ProductionLimitBreakRuntimeState state = Read<ProductionLimitBreakRuntimeState>(states,
            actor, trait, ProductionLimitBreakRuleId);
        if (state.active && state.activeLeaseUntilSeconds >= now)
            result.Add("state:production-limit-break");
        if (state.aftermathUntilSeconds > now)
            result.Add("state:production-limit-break-aftermath");
    }
}
