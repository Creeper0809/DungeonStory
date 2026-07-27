using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NavigationPerformanceBatchVerifier
{
    private const string BatchProfileRequestedKey =
        "DungeonStory.NavigationPerformanceBatchVerifier.Requested";

    static NavigationPerformanceBatchVerifier()
    {
        EditorApplication.update -= ObservePendingProfile;
        EditorApplication.update += ObservePendingProfile;
    }

    public static void RunGridAnd100CharacterRegression()
    {
        bool gridValid = GridFoundationDebugScenarios.RunAll(true);
        bool aiValid = CharacterAiStressDebugScenarios.RunForCount(100, true);
        bool valid = gridValid && aiValid;
        Debug.Log(
            $"Navigation batch regression valid={valid}, "
            + $"grid={gridValid}, ai100={aiValid}, "
            + $"aiReport={CharacterAiStressDebugScenarios.LastReport}");
        EditorApplication.Exit(valid ? 0 : 1);
    }

    public static void Run500CharacterProfile()
    {
        SessionState.SetBool(BatchProfileRequestedKey, true);
        CharacterAiStressDebugScenarios.StartPlayModeProfile(
            npcCount: 500,
            warmupFrames: 0,
            sampleFrames: 600,
            exitWhenDone: true);
    }

    public static void Run500CharacterDiagnostic()
    {
        SessionState.SetBool(BatchProfileRequestedKey, true);
        CharacterAiStressDebugScenarios.StartPlayModeProfile(
            npcCount: 500,
            warmupFrames: 0,
            sampleFrames: 120,
            exitWhenDone: true);
    }

    public static void Run1024Grid500CharacterSchedulerProfile()
    {
        bool valid =
            CharacterAiStressDebugScenarios.RunConfiguredLargeGrid500Profile(true);
        Debug.Log(
            $"Navigation 1024-grid 500-character scheduler profile valid={valid}");
        EditorApplication.Exit(valid ? 0 : 1);
    }

    public static void Run1024DenseDungeon500CharacterProfile()
    {
        Environment.SetEnvironmentVariable(
            "DUNGEON_AI_STRESS_GRID_WIDTH",
            "1024");
        Environment.SetEnvironmentVariable(
            "DUNGEON_AI_STRESS_GRID_HEIGHT",
            "1024");
        Environment.SetEnvironmentVariable(
            "DUNGEON_AI_STRESS_ACTIVE_FLOORS",
            "64");

        bool valid =
            CharacterAiStressDebugScenarios.RunConfiguredDenseDungeon500Profile(true);
        Debug.Log(
            $"Navigation 1024-grid facility-dense 500-character profile valid={valid}");
        EditorApplication.Exit(valid ? 0 : 1);
    }

    private static void ObservePendingProfile()
    {
        if (!SessionState.GetBool(BatchProfileRequestedKey, false)
            || CharacterAiStressDebugScenarios.IsPlayModeProfileRunning
            || EditorApplication.isPlaying
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string report = CharacterAiStressDebugScenarios.LastPlayModeProfileReport;
        if (string.IsNullOrWhiteSpace(report))
        {
            return;
        }

        SessionState.SetBool(BatchProfileRequestedKey, false);
        bool valid = report.IndexOf("valid=True", StringComparison.Ordinal) >= 0
            && report.IndexOf("behaviorValid=True", StringComparison.Ordinal) >= 0
            && report.IndexOf("performanceValid=True", StringComparison.Ordinal) >= 0;
        Debug.Log($"Navigation 500-character batch profile valid={valid}: {report}");
        EditorApplication.Exit(valid ? 0 : 1);
    }
}
