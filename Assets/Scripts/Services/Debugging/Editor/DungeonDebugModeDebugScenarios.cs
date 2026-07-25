#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DungeonDebugModeDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Developer Mode/Run EditMode Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Developer mode EditMode scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> failures = new List<string>();
        Verify("settings v1 migration defaults developer mode off", VerifySettingsMigration, failures);
        Verify("run history is capped and save-safe", VerifyHistoryAndTransientReset, failures);
        Verify("target contracts reject approximate selections", VerifyExactTargetContracts, failures);
        Verify("legacy V12 JSON accepts optional debug metadata", VerifyLegacyV12Compatibility, failures);

        foreach (string failure in failures)
        {
            Debug.LogError(failure);
        }

        if (failures.Count == 0 && logSuccess)
        {
            Debug.Log("Developer mode EditMode scenarios passed.");
        }

        return failures.Count == 0;
    }

    private static bool VerifySettingsMigration()
    {
        DungeonUserSettingsData migrated = JsonUtility.FromJson<DungeonUserSettingsData>(
            "{\"version\":1,\"resolutionWidth\":1600,\"resolutionHeight\":900}");
        migrated.Normalize();
        return migrated.version == DungeonUserSettingsData.CurrentVersion
            && !migrated.developerMode
            && migrated.resolutionWidth == 1600
            && migrated.resolutionHeight == 900;
    }

    private static bool VerifyHistoryAndTransientReset()
    {
        ScenarioSettings settings = new ScenarioSettings();
        ScenarioGameData gameData = new ScenarioGameData();
        DungeonDebugModeService mode = new DungeonDebugModeService(settings, gameData);
        mode.Start();
        try
        {
            mode.SetCheat(DungeonDebugCheat.FreezeNeeds, true);
            mode.SetOverlay(DungeonDebugOverlayKind.Grid, true);
            for (int index = 0; index < 60; index++)
            {
                mode.MarkMutation(
                    "scenario:" + index,
                    "전체",
                    DungeonDebugCommandResult.Succeeded("완료"));
            }

            DungeonDebugRunSaveData captured = mode.Capture();
            bool capturedState = captured.debugModified
                && captured.recentCommands.Count == 50
                && captured.recentCommands[0].commandId == "scenario:10";
            mode.Restore(captured);
            bool transientCleared = !mode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds)
                && !mode.IsOverlayEnabled(DungeonDebugOverlayKind.Grid)
                && mode.IsDebugModified;
            settings.Current.developerMode = false;
            DungeonUserSettingsRuntime.Publish(settings.Current);
            return capturedState && transientCleared;
        }
        finally
        {
            mode.Dispose();
        }
    }

    private static bool VerifyExactTargetContracts()
    {
        DungeonDebugTargetSelection empty = new DungeonDebugTargetSelection
        {
            Kind = DungeonDebugTargetKind.Character,
            HasGridPosition = true,
            GridPosition = Vector2Int.zero
        };
        DungeonDebugTargetSelection cell = new DungeonDebugTargetSelection
        {
            Kind = DungeonDebugTargetKind.GridCell,
            HasGridPosition = true,
            GridPosition = Vector2Int.zero
        };

        return !empty.Matches(DungeonDebugTargetKind.Character)
            && !empty.Matches(DungeonDebugTargetKind.Building)
            && !empty.Matches(DungeonDebugTargetKind.ItemPile)
            && cell.Matches(DungeonDebugTargetKind.GridCell)
            && !cell.Matches(DungeonDebugTargetKind.Wildlife);
    }

    private static bool VerifyLegacyV12Compatibility()
    {
        DungeonGameSaveData legacy = JsonUtility.FromJson<DungeonGameSaveData>(
            "{\"version\":12,\"savedAtUtc\":\"2026-07-23T00:00:00Z\"}");
        DungeonDebugRunSaveData debug =
            DungeonSaveSectionPayload.ReadOrNew<DungeonDebugRunSaveData>(
                legacy,
                DungeonDebugSaveSection.Id);
        return legacy != null
            && legacy.version == DungeonGameSaveData.CurrentVersion
            && !debug.debugModified
            && debug.recentCommands.Count == 0;
    }

    private static void Verify(string label, Func<bool> scenario, ICollection<string> failures)
    {
        try
        {
            if (!scenario())
            {
                failures.Add(label);
            }
        }
        catch (Exception exception)
        {
            failures.Add($"{label}: {exception.GetType().Name} {exception.Message}");
        }
    }

    private sealed class ScenarioSettings : IDungeonUserSettingsService
    {
        public DungeonUserSettingsData Current { get; } = new DungeonUserSettingsData
        {
            developerMode = true
        };

        public string SettingsPath => string.Empty;
        public string LastError => string.Empty;
        public void Update(Action<DungeonUserSettingsData> change) => change?.Invoke(Current);
        public void ResetDefaults() => Current.developerMode = false;
        public void ApplyCurrent() => DungeonUserSettingsRuntime.Publish(Current);
    }

    private sealed class ScenarioGameData : IGameDataProvider
    {
        private readonly GameData data;

        public ScenarioGameData()
        {
            data = ScriptableObject.CreateInstance<GameData>();
            data.day = new Data<int>();
            data.hour = new Data<int>();
            data.day.Initialize(2);
            data.hour.Initialize(7);
        }

        public bool TryGetGameData(out GameData gameData)
        {
            gameData = data;
            return true;
        }
    }
}
#endif
