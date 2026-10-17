using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static ExtremeTraitRuleAccess;
using static ExtremeTraitRuleIds;

internal sealed class ForbiddenResearchLeapTraitRuntime
{
    private readonly CharacterIdentityStateStore states;

    internal ForbiddenResearchLeapTraitRuntime(CharacterIdentityStateStore states) =>
        this.states = states ?? throw new ArgumentNullException(nameof(states));

    [GameplayInternalOnly("Only the extreme-trait facade dispatches this capability command/query.",
        "ExtremeTraitRuntime")]
    internal bool TryPrepareForbiddenResearchLeap(
        CharacterActor actor,
        string projectId,
        ulong runSeed,
        float elapsedSeconds,
        out ForbiddenResearchLeapPreparation preparation)
    {
        preparation = null;
        if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        ForbiddenResearchLeapRule rule = ResolveRule<ForbiddenResearchLeapRule>(
            actor, 302, ForbiddenLeapRuleId, out CharacterTraitSO trait);
        if (rule == null) return false;
        string project = Required(projectId, nameof(projectId));
        ForbiddenResearchLeapRuntimeState state = Read<ForbiddenResearchLeapRuntimeState>(states,
            actor, trait, ForbiddenLeapRuleId);
        state.usedProjectIds ??= new List<string>();
        if (state.usedProjectIds.Contains(project, StringComparer.Ordinal)) return false;
        state.usedProjectIds.Add(project);
        state.usedProjectIds.Sort(StringComparer.Ordinal);
        state.aftermathUntilSeconds = Mathf.Max(0f, elapsedSeconds)
            + DaysToSeconds(rule.aftermathDays);
        ulong hash = FixedHash(runSeed, "research", project, ActorId(actor), "302");
        float roll = Roll01(hash);
        ExtremeRiskResolution resolution = roll < rule.breakthroughChance
            ? new ExtremeRiskResolution(
                ExtremeRiskOutcome.Breakthrough, 1f, 1f,
                Mathf.Abs(rule.breakthroughProgress), hash)
            : roll < rule.breakthroughChance + rule.setbackChance
                ? new ExtremeRiskResolution(
                    ExtremeRiskOutcome.Setback, 1f, 1f,
                    -Mathf.Abs(rule.setbackProgress), hash)
                : new ExtremeRiskResolution(ExtremeRiskOutcome.Normal, 1f, 1f, 0f, hash);
        string characterId = ActorId(actor);
        string traitId = trait.DefinitionId.Value;
        states.TryGet(characterId, traitId, ForbiddenLeapRuleId, out var before);
        preparation = new ForbiddenResearchLeapPreparation(
            states, characterId, traitId, project, before,
            JsonUtility.ToJson(state), state.aftermathUntilSeconds, resolution);
        return true;
    }

    internal void AddConditions(CharacterActor actor, float now, ICollection<string> result)
    {
        AddTimedAftermath<ForbiddenResearchLeapRule, ForbiddenResearchLeapRuntimeState>(
            states, actor, 302, ForbiddenLeapRuleId, "state:forbidden-leap-aftermath",
            value => value.aftermathUntilSeconds, now, result);
    }
}
