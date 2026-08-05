using System;
using System.Collections.Generic;

public readonly struct CharacterAiDecisionTickResult
{
    public CharacterAiDecisionTickResult(
        bool handled,
        CharacterAiBranch branch,
        string task,
        string status)
    {
        Handled = handled;
        Branch = branch;
        Task = task ?? string.Empty;
        Status = status ?? string.Empty;
    }

    public bool Handled { get; }
    public CharacterAiBranch Branch { get; }
    public string Task { get; }
    public string Status { get; }
}

public interface ICharacterAiDecisionPipeline
{
    CharacterAiDecisionTickResult RunRootDecision(CharacterActor actor);
    bool HasCriticalState(CharacterActor actor);
    CharacterAiDecisionTickResult RunCritical(
        CharacterActor actor,
        CharacterBlackboard blackboard);
    bool HasDeprivationBreakdown(CharacterActor actor);
    CharacterAiDecisionTickResult RunDeprivationBreakdown(CharacterActor actor);
    bool HasLockedAction(CharacterActor actor);
    bool CanInterruptCurrentAction(CharacterActor actor);
    CharacterAiDecisionTickResult RunLockedAction(CharacterActor actor);
    bool HasMacroGoal(CharacterActor actor);
    bool HasContinuableCurrentAction(CharacterActor actor);
    bool ShouldStopCurrentActionForReplan(CharacterActor actor);
    CharacterAiDecisionTickResult ContinueCurrentAction(CharacterActor actor);
    CharacterAiDecisionTickResult StopCurrentActionForReplan(CharacterActor actor);
    CharacterAiDecisionTickResult SelectJobGiverAction(
        CharacterActor actor,
        CharacterAiJobGiver jobGiver,
        string taskName);
    CharacterAiDecisionTickResult RunSelectedAction(
        CharacterActor actor,
        string taskName,
        CharacterAiBranch branchOverride = CharacterAiBranch.None);
    CharacterAiDecisionTickResult RunMacroGoalDecision(CharacterActor actor);
    CharacterAiDecisionTickResult RunEmergencyDecision(CharacterActor actor);
    CharacterAiDecisionTickResult RunRoutineUtilityDecision(CharacterActor actor);
    CharacterAiDecisionTickResult RunIdleBehavior(
        CharacterActor actor,
        CharacterBlackboard blackboard);
    CharacterAiDecisionTickResult RecordBtDecisionTrace(
        CharacterActor actor,
        CharacterAiBranch branch,
        string taskName,
        string status);
    bool HasMacroGoalType(CharacterActor actor, CharacterMacroGoalType goalType);
    CharacterAiDecisionTickResult ClearContinueMacro(CharacterActor actor);
    CharacterAiDecisionTickResult RunComplainMacro(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal);
    CharacterAiDecisionTickResult ApplyAvoidFacility(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal);
    CharacterAiDecisionTickResult RunExitDungeonMacro(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal);
    CharacterAiDecisionTickResult RunVandalizeMacro(
        CharacterActor actor,
        CharacterBlackboard blackboard,
        CharacterMacroGoal goal);
}

public interface ICharacterAiFacilityLookup
{
    BuildableObject FindFacility(int id, string tag);
}

public sealed class CharacterAiFacilityLookup : ICharacterAiFacilityLookup
{
    private readonly IBuildingWorldQuery buildingWorld;

    public CharacterAiFacilityLookup(IBuildingWorldQuery buildingWorld)
    {
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
    }

    public BuildableObject FindFacility(int id, string tag)
    {
        IReadOnlyList<BuildableObject> buildings = buildingWorld.Buildings;
        foreach (BuildableObject building in buildings)
        {
            if (CharacterAiDecisionPipeline.MatchesFacility(building, id, tag))
            {
                return building;
            }
        }

        return null;
    }
}
