using System;
using System.Collections.Generic;
using DungeonStory.Content.CoreSession;

public sealed class RunFlowSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonRunFlowSaveData,
        DungeonRunFlowAggregateState>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "run.flow";

    private readonly IDungeonRunFlowRuntime runtime;
    private readonly IDungeonRunFlowRestorePublisher restorePublisher;
    private readonly CoreSessionRulesDefinition rules;

    public RunFlowSaveSection(
        IDungeonRunFlowRuntime runtime,
        ICoreSessionRulesProvider rulesProvider)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        restorePublisher = runtime as IDungeonRunFlowRestorePublisher
            ?? throw new InvalidOperationException(
                $"{nameof(RunFlowSaveSection)} requires a detached-state restore publisher.");
        rules = (rulesProvider
                ?? throw new ArgumentNullException(nameof(rulesProvider)))
            .CoreSessionRules
            ?? throw new InvalidOperationException(
                "Core-session rules are not authored.");
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonRunFlowSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    // A missing pending fact must stay null, not Unity's inline default object.
    protected override string SerializePayload(DungeonRunFlowSaveData payload) =>
        Newtonsoft.Json.JsonConvert.SerializeObject(payload);
    public override IReadOnlyList<string> DependsOn => new[]
    {
        OffenseAggregateSaveSection.Id,
        InvasionSaveSection.Id
    };

    protected override DungeonRunFlowSaveData CapturePayload()
    {
        return new DungeonRunFlowSaveData
        {
            phase = runtime.Phase,
            outcome = runtime.Outcome,
            currentDay = runtime.CurrentDay,
            bossArmed = runtime.IsBossArmed,
            bossActive = runtime.IsBossActive,
            bossCycle = runtime.BossCycle,
            pendingCompletion = restorePublisher.CapturePendingCompletion()
        };
    }

    protected override void ValidateRawPayload(string payloadJson) => ValidatePendingCompletionJson(payloadJson);

    public static void ValidatePendingCompletionJson(string json)
    {
        var root = Newtonsoft.Json.Linq.JObject.Parse(json, new Newtonsoft.Json.Linq.JsonLoadSettings
        { DuplicatePropertyNameHandling = Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error });
        var pending = root["pendingCompletion"];
        if (pending == null) throw new InvalidOperationException("owner-run-pending-field-missing");
        if (pending.Type == Newtonsoft.Json.Linq.JTokenType.Null) return;
        if (pending is not Newtonsoft.Json.Linq.JObject
            || pending["publicationKey"]?.Type != Newtonsoft.Json.Linq.JTokenType.String
            || pending["fact"] is not Newtonsoft.Json.Linq.JObject fact)
            throw new InvalidOperationException("owner-run-pending-shape-invalid");
        foreach (string field in new[] { "runSeed", "outcome", "absoluteDay" })
            if (fact[field]?.Type != Newtonsoft.Json.Linq.JTokenType.Integer)
                throw new InvalidOperationException("owner-run-pending-field-invalid:" + field);
        foreach (string field in new[] { "ownerId", "displayName", "reason" })
            if (fact[field]?.Type != Newtonsoft.Json.Linq.JTokenType.String)
                throw new InvalidOperationException("owner-run-pending-field-invalid:" + field);
    }

    protected override DungeonRunFlowAggregateState BuildRestoreCandidate(
        DungeonRunFlowSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        ValidatePayload(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Run-flow restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        return new DungeonRunFlowAggregateState
        {
            Phase = payload.phase,
            Outcome = payload.outcome,
            CurrentDay = payload.currentDay,
            BossArmed = payload.bossArmed,
            BossActive = payload.bossActive,
            BossCycle = payload.bossCycle,
            PendingCompletion = payload.pendingCompletion?.Clone()
        };
    }

    protected override void PublishRestoreCandidate(
        DungeonRunFlowAggregateState candidate) =>
        restorePublisher.PublishRestoreState(candidate);

    private void ValidatePayload(
        DungeonRunFlowSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload == null)
        {
            report.AddError("Run-flow payload is null.");
            return;
        }
        if (payload.version != DungeonRunFlowSaveData.CurrentVersion)
        {
            report.AddError(
                $"Run-flow payload version {payload.version} is unsupported.");
        }
        if (!Enum.IsDefined(typeof(DungeonRunOutcome), payload.outcome)
            || payload.currentDay < 1)
        {
            report.AddError("Run-flow outcome or current day is invalid.");
            return;
        }

        DungeonRunPhase expectedPhase = payload.outcome == DungeonRunOutcome.None
            ? DungeonRunFlowRules.ResolvePhaseForDay(
                payload.currentDay,
                rules)
            : DungeonRunPhase.Finished;
        if (payload.phase != expectedPhase)
        {
            report.AddError(
                $"Run-flow phase {payload.phase} does not match day/outcome state.");
        }

        try { payload.pendingCompletion?.Validate(payload.outcome, payload.currentDay); }
        catch (InvalidOperationException error) { report.AddError(error.Message); }

        int maximumBossCycle =
            DungeonRunFlowRules.ResolveBossCycleForDay(
                payload.currentDay,
                rules);
        if (payload.bossCycle < 0
            || payload.bossCycle > maximumBossCycle
            || payload.bossArmed && payload.bossActive
            || (payload.bossArmed || payload.bossActive)
                && (payload.outcome != DungeonRunOutcome.None
                    || payload.bossCycle <= 0))
        {
            report.AddError("Run-flow boss-cycle hierarchy is invalid.");
        }
    }

}
