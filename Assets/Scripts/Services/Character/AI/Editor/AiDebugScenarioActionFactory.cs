using System;
using System.Collections.Generic;
using UnityEngine;

public static class AiDebugScenarioActionFactory
{
    public static AIAction[] CreateCustomerActions()
    {
        return CreateActions(
            "SO/AI/Action/Eat",
            "SO/AI/Action/Rest",
            "SO/AI/Action/Shopping",
            "SO/AI/Action/LookAround",
            "SO/AI/Action/ExitDungeon");
    }

    public static AIAction[] CreateStaffActions()
    {
        return CreateActions(
            "SO/AI/Action/Work",
            "SO/AI/Action/Wait",
            "SO/AI/Action/Eat",
            "SO/AI/Action/Rest",
            "SO/AI/Action/Toilet",
            "SO/AI/Action/Hygiene",
            "SO/AI/Action/Shopping");
    }

    private static AIAction[] CreateActions(params string[] resourcePaths)
    {
        List<AIAction> actions = new List<AIAction>(resourcePaths.Length);
        foreach (string resourcePath in resourcePaths)
        {
            AIActionSet actionSet = Resources.Load<AIActionSet>(resourcePath);
            if (actionSet == null)
            {
                throw new InvalidOperationException($"Required debug AI action asset is missing: Resources/{resourcePath}");
            }

            actions.Add(new AIAction { actionset = actionSet });
        }

        return actions.ToArray();
    }
}
