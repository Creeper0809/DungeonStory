using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class MetaRunProgressTracker
{
    private readonly IGameClock gameClock;
    private readonly HashSet<int> discoveredFacilityIds = new HashSet<int>();
    private readonly HashSet<string> unlockedRecipeIds = new HashSet<string>();
    private readonly IReadOnlyCollection<string> unlockedRecipeIdsView;

    private float runStartTime;
    private int currentDay = 1;
    private int settlementCount;
    private int defendedInvasionCount;
    private InvasionThreatStage maxThreatStage = InvasionThreatStage.Peaceful;
    private float finalInvasionThreat;
    private int offenseSuccessCount;

    public MetaRunProgressTracker(IGameClock gameClock)
    {
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        unlockedRecipeIdsView = ReadOnlyView.Collection(unlockedRecipeIds);
    }

    public IReadOnlyCollection<string> UnlockedRecipeIds => unlockedRecipeIdsView;
    public IReadOnlyCollection<int> DiscoveredFacilityIds => discoveredFacilityIds;
    public float ElapsedSeconds => Mathf.Max(0f, gameClock.Time - runStartTime);
    public int CurrentDay => currentDay;
    public int SettlementCount => settlementCount;
    public int DefendedInvasionCount => defendedInvasionCount;
    public InvasionThreatStage MaxThreatStage => maxThreatStage;
    public float FinalInvasionThreat => finalInvasionThreat;
    public int OffenseSuccessCount => offenseSuccessCount;

    public void StartNewRun(float startTime)
    {
        runStartTime = startTime;
        currentDay = 1;
        settlementCount = 0;
        defendedInvasionCount = 0;
        maxThreatStage = InvasionThreatStage.Peaceful;
        finalInvasionThreat = 0f;
        offenseSuccessCount = 0;
        discoveredFacilityIds.Clear();
        unlockedRecipeIds.Clear();
    }

    public void RecordOperatingDayStarted(int day)
    {
        currentDay = Mathf.Max(currentDay, day);
    }

    public void RecordOperatingDayReport(OperatingDayReport report)
    {
        if (report == null)
        {
            return;
        }

        settlementCount++;
        currentDay = Mathf.Max(currentDay, report.day);
    }

    public void RecordThreat(InvasionThreatSnapshot snapshot)
    {
        if (GetThreatStageScore(snapshot.stage) > GetThreatStageScore(maxThreatStage))
        {
            maxThreatStage = snapshot.stage;
        }

        finalInvasionThreat = Mathf.Max(finalInvasionThreat, snapshot.threat);
    }

    public void RecordInvasionStarted(InvasionThreatSnapshot snapshot)
    {
        RecordThreat(snapshot);
        finalInvasionThreat = Mathf.Max(finalInvasionThreat, snapshot.threat);
    }

    public void RecordInvasionResolved(bool defended)
    {
        if (defended)
        {
            defendedInvasionCount++;
        }
    }

    public void RecordFacilityVisit(BuildableObject facility)
    {
        BuildingSO buildingData = facility != null ? facility.BuildingData : null;
        if (buildingData != null && buildingData.id >= 0)
        {
            discoveredFacilityIds.Add(buildingData.id);
        }
    }

    public void RecordBlueprintResearchCompleted(BlueprintResearchUnlockResult unlockResult)
    {
        foreach (string recipeId in unlockResult.UnlockedRecipes ?? Array.Empty<string>())
        {
            RecordRecipe(recipeId);
        }
    }

    public void RecordFacilitySynthesisCompleted(FacilitySynthesisResult result)
    {
        RecordRecipe(result.Recipe != null ? result.Recipe.recipeId : string.Empty);

        BuildingSO resultBuilding = result.ResultBuilding != null
            ? result.ResultBuilding.BuildingData
            : result.Recipe != null
                ? result.Recipe.resultBuilding
                : null;
        if (resultBuilding != null && resultBuilding.id >= 0)
        {
            discoveredFacilityIds.Add(resultBuilding.id);
        }
    }

    public void RecordOffenseSuccess()
    {
        offenseSuccessCount++;
    }

    public MetaRunResultBuildContext CreateResultContext(
        CharacterActor owner,
        string reason,
        DungeonRunOutcome outcome = DungeonRunOutcome.Defeat)
    {
        return new MetaRunResultBuildContext(
            owner,
            reason,
            runStartTime,
            currentDay,
            settlementCount,
            defendedInvasionCount,
            maxThreatStage,
            finalInvasionThreat,
            discoveredFacilityIds.Count,
            unlockedRecipeIds.Count,
            offenseSuccessCount,
            outcome);
    }

    public void Restore(
        float elapsedSeconds,
        int savedCurrentDay,
        int savedSettlementCount,
        int savedDefendedInvasionCount,
        InvasionThreatStage savedMaxThreatStage,
        float savedFinalInvasionThreat,
        int savedOffenseSuccessCount,
        IEnumerable<int> savedDiscoveredFacilityIds,
        IEnumerable<string> savedUnlockedRecipeIds)
    {
        runStartTime = gameClock.Time - Mathf.Max(0f, elapsedSeconds);
        currentDay = Mathf.Max(1, savedCurrentDay);
        settlementCount = Mathf.Max(0, savedSettlementCount);
        defendedInvasionCount = Mathf.Max(0, savedDefendedInvasionCount);
        maxThreatStage = savedMaxThreatStage;
        finalInvasionThreat = Mathf.Max(0f, savedFinalInvasionThreat);
        offenseSuccessCount = Mathf.Max(0, savedOffenseSuccessCount);
        discoveredFacilityIds.Clear();
        unlockedRecipeIds.Clear();

        foreach (int id in savedDiscoveredFacilityIds ?? Array.Empty<int>())
        {
            if (id >= 0)
            {
                discoveredFacilityIds.Add(id);
            }
        }

        foreach (string recipeId in savedUnlockedRecipeIds ?? Array.Empty<string>())
        {
            RecordRecipe(recipeId);
        }
    }

    private void RecordRecipe(string recipeId)
    {
        if (!string.IsNullOrWhiteSpace(recipeId))
        {
            unlockedRecipeIds.Add(recipeId);
        }
    }

    private static int GetThreatStageScore(InvasionThreatStage stage)
    {
        return stage switch
        {
            InvasionThreatStage.Warning => 1,
            InvasionThreatStage.Candidate => 2,
            InvasionThreatStage.Safety => 2,
            _ => 0
        };
    }
}
