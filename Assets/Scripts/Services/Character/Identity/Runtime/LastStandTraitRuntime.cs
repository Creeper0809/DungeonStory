using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static ExtremeTraitRuleAccess;
using static ExtremeTraitRuleIds;

internal sealed class LastStandTraitRuntime
{
    private readonly CharacterIdentityStateStore states;

    internal LastStandTraitRuntime(CharacterIdentityStateStore states) =>
        this.states = states ?? throw new ArgumentNullException(nameof(states));

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryActivateLastStand(
        CharacterActor actor,
        string encounterId,
        float healthRatio,
        bool coreOrganCritical,
        float elapsedSeconds,
        out LastStandRule rule)
    {
        rule = ResolveRule<LastStandRule>(actor, 301, LastStandRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        string requiredEncounter = Required(encounterId, nameof(encounterId));
        LastStandRuntimeState state = Read<LastStandRuntimeState>(states, actor, trait, LastStandRuleId);
        if (!string.Equals(state.encounterId, requiredEncounter, StringComparison.Ordinal))
        {
            state.encounterId = requiredEncounter;
            state.used = false;
            state.active = false;
        }
        if (state.used
            || (!coreOrganCritical && Mathf.Clamp01(healthRatio) >= rule.healthThreshold))
        {
            return false;
        }
        state.used = true;
        state.active = true;
        Write(states, actor, trait, LastStandRuleId, state);
        return true;
    }

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal void EndLastStand(CharacterActor actor, string encounterId, float elapsedSeconds)
    {
        LastStandRule rule = ResolveRule<LastStandRule>(actor, 301, LastStandRuleId, out CharacterTraitSO trait);
        if (rule == null) return;
        LastStandRuntimeState state = Read<LastStandRuntimeState>(states, actor, trait, LastStandRuleId);
        if (!state.active || !string.Equals(state.encounterId, encounterId?.Trim(), StringComparison.Ordinal))
            return;
        state.active = false;
        state.aftermathUntilSeconds = Mathf.Max(0f, elapsedSeconds)
            + DaysToSeconds(rule.aftermathDays);
        Write(states, actor, trait, LastStandRuleId, state);
    }

    internal void AddConditions(CharacterActor actor, float now, ICollection<string> result)
    {
        LastStandRule rule = ResolveRule<LastStandRule>(actor, 301, LastStandRuleId, out CharacterTraitSO trait);
        if (rule == null) return;
        LastStandRuntimeState state = Read<LastStandRuntimeState>(states, actor, trait, LastStandRuleId);
        if (state.active) result.Add("state:last-stand");
        if (state.aftermathUntilSeconds > now) result.Add("state:last-stand-aftermath");
    }
}
