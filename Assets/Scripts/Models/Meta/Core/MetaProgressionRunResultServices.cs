using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct MetaRunResultBuildContext
{
    public MetaRunResultBuildContext(
        string ownerName,
        string reason,
        float survivalSeconds,
        int currentDay,
        int settlementCount,
        int defendedInvasionCount,
        InvasionThreatStage maxThreatStage,
        float finalInvasionThreat,
        int discoveredFacilityCount,
        int unlockedRecipeCount,
        int offenseSuccessCount,
        float difficultyMultiplier,
        DungeonDifficulty difficulty,
        DungeonSurvivalPressure survivalPressure,
        DungeonRunOutcome outcome = DungeonRunOutcome.Defeat)
    {
        OwnerName = string.IsNullOrWhiteSpace(ownerName) ? "사장" : ownerName.Trim();
        Reason = string.IsNullOrWhiteSpace(reason) ? "사장 쓰러짐" : reason.Trim();
        SurvivalSeconds = Mathf.Max(0f, survivalSeconds);
        CurrentDay = Mathf.Max(1, currentDay);
        SettlementCount = Mathf.Max(0, settlementCount);
        DefendedInvasionCount = Mathf.Max(0, defendedInvasionCount);
        MaxThreatStage = maxThreatStage;
        FinalInvasionThreat = Mathf.Max(0f, finalInvasionThreat);
        DiscoveredFacilityCount = Mathf.Max(0, discoveredFacilityCount);
        UnlockedRecipeCount = Mathf.Max(0, unlockedRecipeCount);
        OffenseSuccessCount = Mathf.Max(0, offenseSuccessCount);
        DifficultyMultiplier = Mathf.Max(0.01f, difficultyMultiplier);
        Difficulty = difficulty;
        SurvivalPressure = survivalPressure;
        Outcome = outcome == DungeonRunOutcome.None ? DungeonRunOutcome.Defeat : outcome;
    }

    public string OwnerName { get; }
    public string Reason { get; }
    public float SurvivalSeconds { get; }
    public int CurrentDay { get; }
    public int SettlementCount { get; }
    public int DefendedInvasionCount { get; }
    public InvasionThreatStage MaxThreatStage { get; }
    public float FinalInvasionThreat { get; }
    public int DiscoveredFacilityCount { get; }
    public int UnlockedRecipeCount { get; }
    public int OffenseSuccessCount { get; }
    public float DifficultyMultiplier { get; }
    public DungeonDifficulty Difficulty { get; }
    public DungeonSurvivalPressure SurvivalPressure { get; }
    public DungeonRunOutcome Outcome { get; }
}

public interface IMetaRunResultBuilder
{
    RunResultSnapshot Build(MetaRunResultBuildContext context);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MetaRunResultBuilder : IMetaRunResultBuilder
{
    public RunResultSnapshot Build(MetaRunResultBuildContext context)
    {
        return new RunResultSnapshot(
            ownerName: context.OwnerName,
            endReason: context.Reason,
            survivalSeconds: context.SurvivalSeconds,
            survivedOperatingDays: context.CurrentDay,
            settlementCount: context.SettlementCount,
            defendedInvasionCount: context.DefendedInvasionCount,
            maxThreatStage: context.MaxThreatStage,
            finalInvasionThreat: context.FinalInvasionThreat,
            firstDiscoveredFacilityCount: context.DiscoveredFacilityCount,
            firstUnlockedRecipeCount: context.UnlockedRecipeCount,
            offenseSuccessCount: context.OffenseSuccessCount,
            difficultyMultiplier: context.DifficultyMultiplier,
            outcome: context.Outcome,
            difficulty: context.Difficulty,
            survivalPressure: context.SurvivalPressure);
    }
}

public readonly struct MetaRunEnvironmentSnapshot
{
    public MetaRunEnvironmentSnapshot(
        float difficultyMultiplier,
        DungeonDifficulty difficulty,
        DungeonSurvivalPressure survivalPressure)
    {
        DifficultyMultiplier = Mathf.Max(0.01f, difficultyMultiplier);
        Difficulty = difficulty;
        SurvivalPressure = survivalPressure;
    }

    public float DifficultyMultiplier { get; }
    public DungeonDifficulty Difficulty { get; }
    public DungeonSurvivalPressure SurvivalPressure { get; }
}

public readonly struct MetaFacilityCandidateSnapshot
{
    public MetaFacilityCandidateSnapshot(int definitionId, bool eligible)
    {
        DefinitionId = definitionId;
        Eligible = eligible;
    }

    public int DefinitionId { get; }
    public bool Eligible { get; }
}

public interface IMetaRuntimeEventSink
{
    void RecordOffenseSuccess();
    void RecordOperatingDayStarted(int day);
    void RecordOperatingDayReport(int day);
    void RecordThreat(InvasionThreatStage stage, float threat);
    void RecordInvasionResolved(bool defended);
    void RecordFacilityDiscovery(int id);
    void RecordResearchRecipes(IEnumerable<string> ids);
    void RecordSynthesis(string recipeId, int resultBuildingId);
    RunResultSnapshot EndRun(
        string ownerName,
        string reason,
        DungeonRunOutcome outcome = DungeonRunOutcome.Defeat);
}

public interface IMetaRuntimeApplicationPort
{
    void Bind(IMetaRuntimeEventSink runtime);
    void Unbind(IMetaRuntimeEventSink runtime);
    MetaRunEnvironmentSnapshot CaptureRunEnvironment();
    void PublishUpgradePurchased(MetaUpgradePurchasedEvent purchasedEvent, string message);
    void PublishRunResult(RunResultReadyEvent readyEvent);
    void ShowRunResult(RunResultSnapshot result);
}
