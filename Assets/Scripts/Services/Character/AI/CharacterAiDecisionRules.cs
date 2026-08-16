using UnityEngine;

internal static class CharacterAiDecisionRules
{
    public static string GetBuildingLabel(BuildableObject building)
    {
        if (building == null)
        {
            return "None";
        }

        return building.BuildingData != null
            && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
                ? building.BuildingData.objectName
                : building.name;
    }

    public static bool TryPrepare(
        CharacterActor actor,
        out CharacterBlackboard blackboard,
        out string error,
        bool requireCanRunAi = true)
    {
        blackboard = actor != null ? actor.Blackboard : null;
        error = string.Empty;
        if (actor == null)
        {
            error = "Actor is missing.";
            return false;
        }

        if (blackboard == null)
        {
            error = "CharacterBlackboard is missing.";
            Debug.LogError($"{actor.name}: {error}", actor);
            return false;
        }

        if (requireCanRunAi && !actor.CanRunAi)
        {
            error = $"AI cannot run in state {actor.CurrentLifecycleState}.";
            return false;
        }

        return true;
    }

    public static CharacterAiDecisionTickResult Result(
        bool handled,
        CharacterAiBranch branch,
        string task,
        string status,
        CharacterBlackboard blackboard)
    {
        blackboard?.RecordBtStatus(branch, task, status);
        blackboard?.RecordBtOutcome(branch, handled);
        return new CharacterAiDecisionTickResult(handled, branch, task, status);
    }
}
