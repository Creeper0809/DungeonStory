using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

internal sealed class MetaRunProgressAggregateState
{
    internal HashSet<int> DiscoveredFacilityIds { get; } = new HashSet<int>();
    internal HashSet<string> UnlockedRecipeIds { get; } = new HashSet<string>(StringComparer.Ordinal);
    internal float RunStartTime { get; set; }
    internal int CurrentDay { get; set; } = 1;
    internal int SettlementCount { get; set; }
    internal int DefendedInvasionCount { get; set; }
    internal InvasionThreatStage MaxThreatStage { get; set; } = InvasionThreatStage.Peaceful;
    internal float FinalInvasionThreat { get; set; }
    internal int OffenseSuccessCount { get; set; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MetaRunProgressTracker
{
    private readonly IGameClock gameClock;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private MetaRunProgressAggregateState State => aggregateRootStore.GetOrCreate(() => new MetaRunProgressAggregateState());
    private MetaRunProgressAggregateState Writable => aggregateRootStore.GetOrCreateWritable(() => new MetaRunProgressAggregateState(), CloneState);

    public MetaRunProgressTracker(IGameClock gameClock, DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.aggregateRootStore = aggregateRootStore ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public IReadOnlyCollection<string> UnlockedRecipeIds => State.UnlockedRecipeIds;
    public IReadOnlyCollection<int> DiscoveredFacilityIds => State.DiscoveredFacilityIds;
    public float ElapsedSeconds => Mathf.Max(0f, gameClock.Time - State.RunStartTime);
    public int CurrentDay => State.CurrentDay;
    public int SettlementCount => State.SettlementCount;
    public int DefendedInvasionCount => State.DefendedInvasionCount;
    public InvasionThreatStage MaxThreatStage => State.MaxThreatStage;
    public float FinalInvasionThreat => State.FinalInvasionThreat;
    public int OffenseSuccessCount => State.OffenseSuccessCount;

    public void StartNewRun(float startTime) => aggregateRootStore.Replace(new MetaRunProgressAggregateState { RunStartTime = startTime });
    public void RecordOperatingDayStarted(int day) => Writable.CurrentDay = Mathf.Max(Writable.CurrentDay, day);
    public void RecordOperatingDayReport(int day) { Writable.SettlementCount++; Writable.CurrentDay = Mathf.Max(Writable.CurrentDay, day); }
    public void RecordThreat(InvasionThreatStage stage, float threat)
    {
        MetaRunProgressAggregateState current = Writable;
        if (GetThreatStageScore(stage) > GetThreatStageScore(current.MaxThreatStage)) current.MaxThreatStage = stage;
        current.FinalInvasionThreat = Mathf.Max(current.FinalInvasionThreat, threat);
    }
    public void RecordInvasionResolved(bool defended) { if (defended) Writable.DefendedInvasionCount++; }
    public void RecordFacilityDiscovery(int definitionId) { if (definitionId >= 0) Writable.DiscoveredFacilityIds.Add(definitionId); }
    public void RecordRecipes(IEnumerable<string> recipeIds) { foreach (string id in recipeIds ?? Array.Empty<string>()) RecordRecipe(id); }
    public void RecordSynthesis(string recipeId, int resultBuildingId) { RecordRecipe(recipeId); RecordFacilityDiscovery(resultBuildingId); }
    public void RecordOffenseSuccess() => Writable.OffenseSuccessCount++;

    public MetaRunResultBuildContext CreateResultContext(string ownerName, string reason, MetaRunEnvironmentSnapshot environment, DungeonRunOutcome outcome)
    {
        MetaRunProgressAggregateState current = State;
        return new MetaRunResultBuildContext(ownerName, reason, ElapsedSeconds, current.CurrentDay, current.SettlementCount,
            current.DefendedInvasionCount, current.MaxThreatStage, current.FinalInvasionThreat, current.DiscoveredFacilityIds.Count,
            current.UnlockedRecipeIds.Count, current.OffenseSuccessCount, environment.DifficultyMultiplier, environment.Difficulty,
            environment.SurvivalPressure, outcome);
    }

    public void Restore(float elapsedSeconds, int day, int settlements, int defended, InvasionThreatStage stage, float threat,
        int offense, IEnumerable<int> facilityIds, IEnumerable<string> recipeIds)
    {
        MetaRunProgressAggregateState restored = new MetaRunProgressAggregateState { RunStartTime = gameClock.Time - Mathf.Max(0f, elapsedSeconds),
            CurrentDay = Mathf.Max(1, day), SettlementCount = Mathf.Max(0, settlements), DefendedInvasionCount = Mathf.Max(0, defended),
            MaxThreatStage = stage, FinalInvasionThreat = Mathf.Max(0f, threat), OffenseSuccessCount = Mathf.Max(0, offense) };
        foreach (int id in facilityIds ?? Array.Empty<int>()) if (id >= 0) restored.DiscoveredFacilityIds.Add(id);
        foreach (string id in recipeIds ?? Array.Empty<string>()) if (!string.IsNullOrWhiteSpace(id)) restored.UnlockedRecipeIds.Add(id.Trim());
        aggregateRootStore.Replace(restored);
    }

    private void RecordRecipe(string id) { if (!string.IsNullOrWhiteSpace(id)) Writable.UnlockedRecipeIds.Add(id.Trim()); }
    private static MetaRunProgressAggregateState CloneState(MetaRunProgressAggregateState source)
    {
        MetaRunProgressAggregateState clone = new MetaRunProgressAggregateState { RunStartTime = source?.RunStartTime ?? 0f,
            CurrentDay = source?.CurrentDay ?? 1, SettlementCount = source?.SettlementCount ?? 0, DefendedInvasionCount = source?.DefendedInvasionCount ?? 0,
            MaxThreatStage = source?.MaxThreatStage ?? InvasionThreatStage.Peaceful, FinalInvasionThreat = source?.FinalInvasionThreat ?? 0f,
            OffenseSuccessCount = source?.OffenseSuccessCount ?? 0 };
        if (source != null) { foreach (int id in source.DiscoveredFacilityIds) clone.DiscoveredFacilityIds.Add(id); foreach (string id in source.UnlockedRecipeIds) clone.UnlockedRecipeIds.Add(id); }
        return clone;
    }
    private static int GetThreatStageScore(InvasionThreatStage stage) => stage switch { InvasionThreatStage.Warning => 1, InvasionThreatStage.Candidate => 2, InvasionThreatStage.Safety => 2, _ => 0 };
}
