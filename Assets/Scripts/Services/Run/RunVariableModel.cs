using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum RunVariableCategory
{
    Start,
    Operation,
    Invasion
}

public sealed class RunStartVariableSnapshot
{
    public RunStartVariableSnapshot(
        int seed,
        string ownerSpeciesTag,
        DungeonDifficulty difficulty,
        IReadOnlyList<int> startingFacilityCandidateIds,
        IReadOnlyList<string> startingGuestSpeciesCandidates,
        IReadOnlyList<int> startingBlueprintCandidateIds,
        int initialShopSeed,
        string initialDungeonLayoutId,
        float threatRiseMultiplier,
        string ownerDoctrineId = "")
    {
        this.seed = seed;
        this.ownerSpeciesTag = ownerSpeciesTag?.Trim() ?? string.Empty;
        runDifficulty = DungeonDifficultyRules.Normalize((int)difficulty);
        this.startingFacilityCandidateIds = EventPayloadSnapshot.Copy(startingFacilityCandidateIds);
        this.startingGuestSpeciesCandidates = EventPayloadSnapshot.Copy(startingGuestSpeciesCandidates);
        this.startingBlueprintCandidateIds = EventPayloadSnapshot.Copy(startingBlueprintCandidateIds);
        this.initialShopSeed = initialShopSeed;
        this.initialDungeonLayoutId = initialDungeonLayoutId?.Trim() ?? string.Empty;
        this.threatRiseMultiplier = Mathf.Max(0.05f, threatRiseMultiplier);
        this.ownerDoctrineId = ownerDoctrineId?.Trim() ?? string.Empty;
    }

    public RunStartVariableSnapshot(
        int seed,
        string ownerSpeciesTag,
        InvasionThreatDifficulty difficulty,
        IReadOnlyList<int> startingFacilityCandidateIds,
        IReadOnlyList<string> startingGuestSpeciesCandidates,
        IReadOnlyList<int> startingBlueprintCandidateIds,
        int initialShopSeed,
        string initialDungeonLayoutId,
        float threatRiseMultiplier,
        string ownerDoctrineId = "")
        : this(
            seed,
            ownerSpeciesTag,
            DungeonDifficultyRules.FromLegacy(difficulty),
            startingFacilityCandidateIds,
            startingGuestSpeciesCandidates,
            startingBlueprintCandidateIds,
            initialShopSeed,
            initialDungeonLayoutId,
            threatRiseMultiplier,
            ownerDoctrineId)
    {
    }

    public int seed { get; }
    public string ownerSpeciesTag { get; }
    public DungeonDifficulty runDifficulty { get; }
    public InvasionThreatDifficulty difficulty => DungeonDifficultyRules.ToLegacy(runDifficulty);
    public IReadOnlyList<int> startingFacilityCandidateIds { get; }
    public IReadOnlyList<string> startingGuestSpeciesCandidates { get; }
    public IReadOnlyList<int> startingBlueprintCandidateIds { get; }
    public int initialShopSeed { get; }
    public string initialDungeonLayoutId { get; }
    public float threatRiseMultiplier { get; }
    public string ownerDoctrineId { get; }

    public string ToSummaryText()
    {
        return string.Join("\n", new[]
        {
            $"사장 종족: {TextOrDefault(ownerSpeciesTag, "미정")}",
            $"시작 시설 후보: {FormatIds(startingFacilityCandidateIds)}",
            $"시작 손님층 후보: {FormatStrings(startingGuestSpeciesCandidates)}",
            $"시작 설계도 후보: {FormatIds(startingBlueprintCandidateIds)}",
            $"초기 상점 시드: {initialShopSeed}",
            $"초기 구조: {TextOrDefault(initialDungeonLayoutId, "기본")}",
            $"난이도: {runDifficulty} / 위협 계수 {threatRiseMultiplier:0.##}"
        });
    }

    private static string FormatIds(IEnumerable<int> values)
    {
        string text = values != null ? string.Join(", ", values) : string.Empty;
        return string.IsNullOrWhiteSpace(text) ? "없음" : text;
    }

    private static string FormatStrings(IEnumerable<string> values)
    {
        string text = values != null
            ? string.Join(", ", values.Where((value) => !string.IsNullOrWhiteSpace(value)))
            : string.Empty;
        return string.IsNullOrWhiteSpace(text) ? "없음" : text;
    }

    private static string TextOrDefault(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }
}

public sealed class RunVariableDefinition
{
    public RunVariableDefinition(
        string id,
        RunVariableCategory category,
        string title,
        string detail,
        EventAlertImportance importance,
        int activeDays,
        IReadOnlyList<IRunVariableEffect> effects)
    {
        this.id = id?.Trim() ?? string.Empty;
        this.category = category;
        this.title = title?.Trim() ?? string.Empty;
        this.detail = detail?.Trim() ?? string.Empty;
        this.importance = importance;
        this.activeDays = Mathf.Max(1, activeDays);
        this.effects = EventPayloadSnapshot.Copy(effects);
    }

    public string id { get; }
    public RunVariableCategory category { get; }
    public string title { get; }
    public string detail { get; }
    public EventAlertImportance importance { get; }
    public int activeDays { get; }
    public IReadOnlyList<IRunVariableEffect> effects { get; }

    public string ToDetailText()
    {
        return string.IsNullOrWhiteSpace(detail) ? title : detail;
    }
}

public sealed class ActiveRunVariable
{
    public ActiveRunVariable(RunVariableDefinition definition, int startDay)
    {
        Definition = definition;
        StartDay = Mathf.Max(1, startDay);
        RemainingDays = Mathf.Max(1, definition != null ? definition.activeDays : 1);
    }

    public RunVariableDefinition Definition { get; }
    public int StartDay { get; }
    public int RemainingDays { get; private set; }
    public bool IsExpired => RemainingDays <= 0;

    public ActiveRunVariable(RunVariableDefinition definition, int startDay, int remainingDays)
    {
        Definition = definition;
        StartDay = Mathf.Max(1, startDay);
        RemainingDays = Mathf.Max(0, remainingDays);
    }

    internal void AdvanceDay()
    {
        RemainingDays = Mathf.Max(0, RemainingDays - 1);
    }
}

public interface IRunVariableStateView
{
    RunStartVariableSnapshot StartVariables { get; }
    IReadOnlyList<ActiveRunVariable> ActiveOperationVariables { get; }
    RunVariableDefinition CurrentInvasionVariable { get; }
    bool HasStarted { get; }
}

public sealed class RunVariableState : IRunVariableStateView
{
    private readonly List<ActiveRunVariable> activeOperationVariables = new List<ActiveRunVariable>();
    private readonly IReadOnlyList<ActiveRunVariable> activeOperationVariablesView;

    public RunVariableState()
    {
        activeOperationVariablesView = activeOperationVariables.AsReadOnly();
    }

    public RunStartVariableSnapshot StartVariables { get; private set; }
    public IReadOnlyList<ActiveRunVariable> ActiveOperationVariables => activeOperationVariablesView;
    public RunVariableDefinition CurrentInvasionVariable { get; private set; }
    public bool HasStarted => StartVariables != null;

    internal void SetStartVariables(RunStartVariableSnapshot snapshot)
    {
        StartVariables = snapshot;
    }

    internal ActiveRunVariable ActivateOperationVariable(RunVariableDefinition definition, int day)
    {
        if (definition == null || definition.category != RunVariableCategory.Operation)
        {
            return null;
        }

        activeOperationVariables.RemoveAll((active) => active == null
            || active.Definition == null
            || string.Equals(active.Definition.id, definition.id, StringComparison.Ordinal));
        ActiveRunVariable instance = new ActiveRunVariable(definition, day);
        activeOperationVariables.Add(instance);
        return instance;
    }

    internal IReadOnlyList<ActiveRunVariable> AdvanceOperationVariables()
    {
        List<ActiveRunVariable> expired = new List<ActiveRunVariable>();
        foreach (ActiveRunVariable active in activeOperationVariables)
        {
            active.AdvanceDay();
            if (active.IsExpired)
            {
                expired.Add(active);
            }
        }

        activeOperationVariables.RemoveAll((active) => active == null || active.IsExpired);
        return expired;
    }

    internal void SetInvasionVariable(RunVariableDefinition definition)
    {
        CurrentInvasionVariable = definition != null && definition.category == RunVariableCategory.Invasion
            ? definition
            : null;
    }

    internal void ClearInvasionVariable()
    {
        CurrentInvasionVariable = null;
    }

    internal void Restore(
        RunStartVariableSnapshot startVariables,
        IEnumerable<ActiveRunVariable> operationVariables,
        RunVariableDefinition invasionVariable)
    {
        StartVariables = startVariables;
        activeOperationVariables.Clear();
        activeOperationVariables.AddRange((operationVariables ?? Array.Empty<ActiveRunVariable>())
            .Where(active => active != null
                && active.Definition != null
                && active.Definition.category == RunVariableCategory.Operation
                && !active.IsExpired));
        SetInvasionVariable(invasionVariable);
    }
}
