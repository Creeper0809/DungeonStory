#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Content.CoreSession;
using UnityEditor;
using UnityEngine;

public static class DungeonDebugModeDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Debug Mode/Run EditMode Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Debug Mode EditMode scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> failures = new List<string>();
        Verify("settings v1 migration defaults developer mode off", VerifySettingsMigration, failures);
        Verify("player copy hides stable ids until debug mode", VerifyPlayerCopyProjection, failures);
        Verify("quality and worker policies use player labels", VerifyGameplayPolicyLabels, failures);
        Verify("run history is capped and save-safe", VerifyHistoryAndTransientReset, failures);
        Verify("debug rules are isolated per runtime scope", VerifyScopedRuleIsolation, failures);
        Verify("target contracts reject approximate selections", VerifyExactTargetContracts, failures);
        Verify("pre-V18 saves are rejected without partial debug restore", VerifyLegacyV12Rejection, failures);

        foreach (string failure in failures)
        {
            Debug.LogError(failure);
        }

        if (failures.Count == 0 && logSuccess)
        {
            Debug.Log("Debug Mode EditMode scenarios passed.");
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
        DungeonDebugModeService mode = new DungeonDebugModeService(
            settings,
            gameData,
            new DungeonRuntimeAggregateRootStore(),
            LoadRulesProvider());
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
            mode.PublishRestoreCandidate(
                mode.PrepareRestoreCandidate(captured));
            bool transientCleared = !mode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds)
                && !mode.IsOverlayEnabled(DungeonDebugOverlayKind.Grid)
                && mode.IsDebugModified;
            settings.Update(current => current.developerMode = false);
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

    private static bool VerifyPlayerCopyProjection()
    {
        const string orderId = "apparel-order:scenario:17";
        string normal = GameplayUiPresentationText.OrderCreated(orderId, false);
        string debug = GameplayUiPresentationText.OrderCreated(orderId, true);
        return !normal.Contains(orderId, StringComparison.Ordinal)
            && !normal.Contains("DEBUG", StringComparison.Ordinal)
            && debug.Contains(orderId, StringComparison.Ordinal)
            && debug.Contains("DEBUG", StringComparison.Ordinal);
    }

    private static bool VerifyGameplayPolicyLabels()
    {
        WorkerSelectionPolicySaveData policy = new()
        {
            mode = WorkerSelectionMode.RuleSet,
            matchMode = WorkerRequirementMatchMode.All,
            sortMode = WorkerCandidateSortMode.BestExpectedQuality,
            statRequirements = new List<WorkerStatRequirementSaveData>
            {
                new()
                {
                    statType = (int)CharacterStatType.Dexterity,
                    minimumValue = 7
                }
            }
        };
        string worker = GameplayUiPresentationText.WorkerPolicy(policy);
        return GameplayUiPresentationText.Quality(CraftsmanshipQualityTier.Masterwork)
                == "명품"
            && GameplayUiPresentationText.RejectedOutput(
                RejectedOutputDisposition.AutoDismantle) == "불합격품 자동 분해"
            && worker.Contains("민첩 7+", StringComparison.Ordinal)
            && !worker.Contains(nameof(WorkerSelectionMode.RuleSet), StringComparison.Ordinal);
    }

    private static bool VerifyScopedRuleIsolation()
    {
        DungeonDebugModeService firstMode = new DungeonDebugModeService(
            new ScenarioSettings(),
            new ScenarioGameData(),
            new DungeonRuntimeAggregateRootStore(),
            LoadRulesProvider());
        DungeonDebugModeService secondMode = new DungeonDebugModeService(
            new ScenarioSettings(),
            new ScenarioGameData(),
            new DungeonRuntimeAggregateRootStore(),
            LoadRulesProvider());
        firstMode.Start();
        secondMode.Start();
        try
        {
            DungeonDebugRuleRuntime firstRules = new DungeonDebugRuleRuntime(firstMode);
            DungeonDebugRuleRuntime secondRules = new DungeonDebugRuleRuntime(secondMode);
            firstMode.SetCheat(DungeonDebugCheat.FreezeNeeds, true);
            firstRules.BeginCommandExecution();
            bool isolatedCommandDepth = firstRules.IsExecutingCommand
                && !secondRules.IsExecutingCommand;
            firstRules.EndCommandExecution();
            return isolatedCommandDepth
                && firstRules.IsEnabled(DungeonDebugCheat.FreezeNeeds)
                && !secondRules.IsEnabled(DungeonDebugCheat.FreezeNeeds)
                && !firstRules.IsExecutingCommand;
        }
        finally
        {
            firstMode.Dispose();
            secondMode.Dispose();
        }
    }

    private static bool VerifyLegacyV12Rejection()
    {
        DungeonGameSaveData legacy = JsonUtility.FromJson<DungeonGameSaveData>(
            "{\"version\":12,\"savedAtUtc\":\"2026-07-23T00:00:00Z\"}");
        return legacy != null
            && legacy.version == 12
            && DungeonSaveCompatibility.TryGetIncompatibilityReason(
                legacy.version,
                out string reason)
            && !string.IsNullOrWhiteSpace(reason);
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

    private static ICoreSessionRulesProvider LoadRulesProvider()
    {
        CoreSessionRulesSO rules = AssetDatabase.LoadAssetAtPath<CoreSessionRulesSO>(
            "Assets/Resources/SO/Content/CoreSessionRules.asset");
        if (rules == null)
        {
            throw new InvalidOperationException("Core session rules asset is missing.");
        }

        rules.ValidateDefinition();
        return new FixedCoreSessionRulesProvider(
            rules.CreateRuntimeDefinition());
    }

    private sealed class FixedCoreSessionRulesProvider : ICoreSessionRulesProvider
    {
        public FixedCoreSessionRulesProvider(CoreSessionRulesDefinition rules)
        {
            CoreSessionRules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public CoreSessionRulesDefinition CoreSessionRules { get; }
    }

    private sealed class ScenarioSettings : IDungeonUserSettingsService
    {
        public DungeonUserSettingsData Current { get; } = new DungeonUserSettingsData
        {
            developerMode = true
        };

        public string SettingsPath => string.Empty;
        public string LastError => string.Empty;
        public event Action Changed;
        public void Update(Action<DungeonUserSettingsData> change)
        {
            change?.Invoke(Current);
            Changed?.Invoke();
        }
        public void ResetDefaults() => Update(current => current.developerMode = false);
        public void ApplyCurrent() => Changed?.Invoke();
    }

    private sealed class ScenarioGameData : IGameSessionStateProvider
    {
        private readonly GameSessionState data;

        public ScenarioGameData()
        {
            data = new GameSessionState();
            data.day.Initialize(2);
            data.hour.Initialize(7);
        }

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = data;
            return true;
        }
    }
}
#endif
