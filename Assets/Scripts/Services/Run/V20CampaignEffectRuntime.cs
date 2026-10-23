using static V20CampaignIdentity;
using static V20CampaignSaveCodec;
using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class V20CampaignEffectRuntime : IContentWorkDelayQuery, IContentWorkDelayCommand
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly V20SocietyEventStateStore societyState;
    internal readonly V20MilestoneRuntime milestoneRuntime;
    private readonly V21ContentEffectExecutionRegistry handlers;
    private readonly Func<V20ContentEffectOwnerSnapshot> capture;
    internal Action<V20ContentEffect, V20ContentEffectOwnerSnapshot, V20ContentEffectOwnerSnapshot> Observer;
    private int evaluationAbsoluteDay = -1;

    internal V20CampaignEffectRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        V20SocietyEventStateStore societyState,
        V20MilestoneRuntime milestoneRuntime,
        V21ContentEffectExecutionRegistry handlers,
        Func<V20ContentEffectOwnerSnapshot> capture)
    {
        this.rootStore = rootStore;
        this.societyState = societyState;
        this.milestoneRuntime = milestoneRuntime;
        this.handlers = handlers;
        this.capture = capture;
    }

    private SocietyEventAggregateState Society => societyState.Current;
    private SocietyEventAggregateState WritableSociety => societyState.Writable;

    internal void BeginDailyEvaluation(int absoluteDay) => evaluationAbsoluteDay = absoluteDay;
    internal void EndDailyEvaluation() => evaluationAbsoluteDay = -1;
    private FactionCampaignAggregateState WritableFactions => rootStore.GetOrCreateWritable(
        () => new FactionCampaignAggregateState(),
        value => new FactionCampaignAggregateState { Data = Clone(value.Data) });

    private FactionCampaignStateSaveData RequireWritableFaction(string factionId)
    {
        string normalized = Normalize(factionId);
        FactionCampaignStateSaveData state = WritableFactions.Data.factions
            .FirstOrDefault(value => string.Equals(
                value.factionId,
                normalized,
                StringComparison.Ordinal));
        return state ?? throw new KeyNotFoundException(
            $"Unknown V20 faction campaign '{normalized}'.");
    }
    public int GetRemainingDays(string scopeId = "")
    {
        int currentDay = Math.Max(0, Society.Data.lastEvaluationAbsoluteDay);
        bool queryAll = string.IsNullOrWhiteSpace(scopeId);
        string scope = queryAll ? string.Empty : NormalizeWorkDelayScope(scopeId);
        return Society.Data.workDelays
            .Where(value => value != null
                && (queryAll || string.Equals(
                    value.scopeId,
                    scope,
                    StringComparison.Ordinal)))
            .Select(value => Math.Max(0, value.untilAbsoluteDayExclusive - currentDay))
            .DefaultIfEmpty(0)
            .Max();
    }

    public float GetWorkSpeedMultiplier(WorkTypeId workTypeId)
    {
        int currentDay = Math.Max(0, Society.Data.lastEvaluationAbsoluteDay);
        string workId = workTypeId.Value?.Trim() ?? string.Empty;
        int active = Society.Data.workDelays.Count(value => value != null
            && value.untilAbsoluteDayExclusive > currentDay
            && WorkDelayAffects(value.scopeId, workId));
        float result = ContentWorkDelaySpeedAuthority.Resolve(active);
        if (workTypeId == BuiltInWorkTypeIds.Haul)
        {
            result = Math.Max(
                ContentWorkDelaySpeedAuthority.MinimumMultiplier,
                result * milestoneRuntime.GetEndlessCrisisMultiplier(
                    EndlessCrisisAxis.Logistics));
        }
        return result;
    }

    public void ApplyWorkDelay(
        string scopeId,
        int days,
        int absoluteDay)
    =>
        ApplyWorkDelayAt(scopeId, days, Math.Max(0, absoluteDay));

    public void ApplyFactionChange(
        string factionId,
        int rapportDelta,
        int grievanceDelta,
        int obligationDelta)
    {
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (rapportDelta > 0)
        {
            rapportDelta = Math.Max(
                1,
                (int)Math.Round(
                    rapportDelta * milestoneRuntime.GetEndlessCrisisMultiplier(
                        EndlessCrisisAxis.Faction),
                    MidpointRounding.AwayFromZero));
        }
        state.rapport = Math.Clamp(state.rapport + rapportDelta, -100, 100);
        state.grievance = Math.Clamp(state.grievance + grievanceDelta, 0, 100);
        state.obligationTokens = Math.Clamp(
            state.obligationTokens + obligationDelta,
            0,
            5);
    }

    internal int EffectAbsoluteDay => evaluationAbsoluteDay >= 0 ? evaluationAbsoluteDay
        : Math.Max(0, Society.Data.lastEvaluationAbsoluteDay);

    internal void ApplyInternalEffects(
        IEnumerable<V20ContentEffect> source,
        string contextFactionId = "")
    {
        foreach (V20ContentEffect effect in source ?? Array.Empty<V20ContentEffect>())
        {
            if (effect == null || !effect.IsValid) continue;
            V20ContentEffectHandler handler = handlers.Require(effect.kind);
            if (handler.Stage == V20ContentEffectStage.Campaign)
            {
                V20ContentEffectOwnerSnapshot before = Observer == null ? null : capture();
                if (before != null) before.EffectAbsoluteDay = EffectAbsoluteDay;
                handler.ApplyCampaign(this, effect, contextFactionId);
                if (Observer != null)
                {
                    V20ContentEffectOwnerSnapshot after = capture();
                    after.EffectAbsoluteDay = EffectAbsoluteDay;
                    Observer(effect, before, after);
                }
            }
        }
    }

    private void ApplyWorkDelayAt(
        string authoredScopeId,
        int days,
        int currentDay)
    {
        if (days == 0) return;
        SocietyEventWorldSaveData state = WritableSociety.Data;
        state.workDelays.RemoveAll(value => value == null
            || value.untilAbsoluteDayExclusive <= currentDay);
        string scope = NormalizeWorkDelayScope(authoredScopeId);
        V20WorkDelaySaveData existing = state.workDelays.FirstOrDefault(value =>
            string.Equals(value.scopeId, scope, StringComparison.Ordinal));
        if (days > 0)
        {
            existing ??= new V20WorkDelaySaveData { scopeId = scope };
            if (!state.workDelays.Contains(existing)) state.workDelays.Add(existing);
            existing.untilAbsoluteDayExclusive = Math.Max(
                currentDay,
                existing.untilAbsoluteDayExclusive) + days;
            return;
        }

        if (existing != null)
            existing.untilAbsoluteDayExclusive = Math.Max(
                currentDay,
                existing.untilAbsoluteDayExclusive + days);
        else if (scope == "global")
        {
            foreach (V20WorkDelaySaveData delay in state.workDelays)
                delay.untilAbsoluteDayExclusive = Math.Max(
                    currentDay,
                    delay.untilAbsoluteDayExclusive + days);
        }
        state.workDelays.RemoveAll(value =>
            value.untilAbsoluteDayExclusive <= currentDay);
    }

    private static string NormalizeWorkDelayScope(string value)
    =>
        string.IsNullOrWhiteSpace(value) ? "global" : value.Trim();

    private static bool WorkDelayAffects(string scopeId, string workTypeId)
    {
        string scope = NormalizeWorkDelayScope(scopeId);
        if (scope == "global") return true;
        if (scope == "flood")
            return WorkTypeCatalog.TryGet(
                    workTypeId,
                    out WorkTypeDefinition definition)
                && definition.CapabilityId is "crop:sow"
                    or "crop:harvest"
                    or "item:haul"
                    or "building:stock";
        if (scope is "road" or "whiteout")
            return ContainsAny(workTypeId, "expedition", "haul", "logistic", "carry", "trade");
        if (workTypeId.IndexOf(scope, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // Authored service, life-event and faction-work delays describe the
        // operational cost of resolving the choice, not a WorkTypeId. They
        // must therefore slow the whole settlement unless the author used one
        // of the explicit environmental scopes handled above.
        return scope.StartsWith("service-incident:", StringComparison.Ordinal)
            || scope.StartsWith("life-event:", StringComparison.Ordinal)
            || scope.StartsWith("faction-work:", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string value, params string[] fragments)
    =>
        fragments.Any(fragment => value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);

    internal string ResolveFactionTarget(
        string authoredTargetId,
        string contextFactionId)
    {
        string target = Normalize(authoredTargetId);
        if (target is "requesting-faction" or "affected-faction")
            target = Normalize(contextFactionId);
        if (WritableFactions.Data.factions.Any(value => string.Equals(
                value.factionId,
                target,
                StringComparison.Ordinal)))
            return target;
        throw new InvalidOperationException(
            $"Faction effect target '{authoredTargetId}' has no valid campaign faction context.");
    }
}
