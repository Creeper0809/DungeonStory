using UnityEditor;
using UnityEngine;

public static class OffenseExpeditionDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Offense/Run Expedition Battle Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(true))
        {
            Debug.LogError("Offense expedition battle scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        bool battle = OffenseBattleDebugScenarios.RunAll(logSuccess);
        bool journey = OffenseJourneyDebugScenarios.RunAll(logSuccess);
        return battle && journey;
    }
}
