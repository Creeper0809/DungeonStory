using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static ExtremeTraitRuleAccess;
using static ExtremeTraitRuleIds;

internal sealed class MiracleSurgeryTraitRuntime
{
    private readonly CharacterIdentityStateStore states;

    internal MiracleSurgeryTraitRuntime(CharacterIdentityStateStore states) =>
        this.states = states ?? throw new ArgumentNullException(nameof(states));

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryResolveMiracleSurgery(
        CharacterActor actor,
        string surgeryId,
        bool isCritical,
        ulong runSeed,
        float elapsedSeconds,
        out ExtremeRiskResolution resolution)
    {
        resolution = default;
        MiracleSurgeryRule rule = ResolveRule<MiracleSurgeryRule>(
            actor, 303, MiracleSurgeryRuleId, out CharacterTraitSO trait);
        if (rule == null || !isCritical) return false;
        string surgery = Required(surgeryId, nameof(surgeryId));
        MiracleSurgeryRuntimeState state = Read<MiracleSurgeryRuntimeState>(states,
            actor, trait, MiracleSurgeryRuleId);
        state.usedSurgeryIds ??= new List<string>();
        if (state.usedSurgeryIds.Contains(surgery, StringComparer.Ordinal)) return false;
        state.usedSurgeryIds.Add(surgery);
        state.usedSurgeryIds.Sort(StringComparer.Ordinal);
        state.aftermathUntilSeconds = Mathf.Max(0f, elapsedSeconds)
            + DaysToSeconds(rule.aftermathDays);
        ulong hash = FixedHash(runSeed, "surgery", surgery, ActorId(actor), "303");
        float roll = Roll01(hash);
        resolution = roll < rule.miracleChance
            ? new ExtremeRiskResolution(ExtremeRiskOutcome.Miracle, 1f, 1f, 0f, hash)
            : roll < rule.miracleChance + rule.complicationChance
                ? new ExtremeRiskResolution(ExtremeRiskOutcome.Complication, 1f, 1f, 0f, hash)
                : new ExtremeRiskResolution(ExtremeRiskOutcome.Normal, 1f, 1f, 0f, hash);
        Write(states, actor, trait, MiracleSurgeryRuleId, state);
        return true;
    }

    internal void AddConditions(CharacterActor actor, float now, ICollection<string> result)
    {
        AddTimedAftermath<MiracleSurgeryRule, MiracleSurgeryRuntimeState>(
            states, actor, 303, MiracleSurgeryRuleId, "state:miracle-surgery-aftermath",
            value => value.aftermathUntilSeconds, now, result);
    }
}
