using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    public enum CharacterAiBehaviorOperation
    {
        None,
        RunDeprivationBreakdown,
        RunLockedAction,
        RunEmergencyDecision,
        RunRoutineUtilityDecision,
        ClearMacroGoal,
        RunMacroGoalDecision,
        RunCriticalState,
        ContinueCurrentAction,
        StopCurrentActionForReplan,
        SelectJobGiverAction,
        RunSelectedAction,
        RunIdleBehavior
    }

    public readonly struct CharacterAiBehaviorTaskRequest
    {
        public CharacterAiBehaviorTaskRequest(
            CharacterId characterId,
            CharacterAiBehaviorOperation operation,
            CharacterAiBranch branch = CharacterAiBranch.None,
            string label = "")
        {
            CharacterId = characterId;
            Operation = operation;
            Branch = branch;
            Label = label ?? string.Empty;
        }

        public CharacterId CharacterId { get; }
        public CharacterAiBehaviorOperation Operation { get; }
        public CharacterAiBranch Branch { get; }
        public string Label { get; }
        public bool IsValid => CharacterId.IsValid
            && Operation != CharacterAiBehaviorOperation.None;
    }

    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public static class CharacterAiBehaviorTasks
    {
        public const float AmbientIdleUtility = 0.001f;

        public static bool IsConditionSuccess(
            bool actorMissing,
            bool serviceAvailable,
            bool condition,
            bool missingActorSucceeds = false) =>
            actorMissing && missingActorSucceeds
            || serviceAvailable && condition;

        public static bool IsActionSuccess(
            bool serviceAvailable,
            bool handled) =>
            serviceAvailable && handled;

        public static float GetAmbientIdleUtility(bool canRunAi) =>
            canRunAi ? AmbientIdleUtility : 0f;

        public static CharacterAiBehaviorTaskRequest CreateRequest(
            CharacterId characterId,
            CharacterAiBehaviorOperation operation,
            CharacterAiBranch branch = CharacterAiBranch.None,
            string label = "") =>
            new(characterId, operation, branch, label);
    }
}
