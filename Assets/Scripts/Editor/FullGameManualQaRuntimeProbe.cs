using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BehaviorDesigner.Runtime;
using DG.Tweening;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

public static class FullGameManualQaRuntimeProbe
{
    private static readonly List<string> capturedErrors = new List<string>();
    private static string lastIntruderReport = "Intruder probe has not run.";
    private static string lastFeatureFamilyReport = "Feature-family probe has not run.";
    private static string lastRemainingFeatureReport = "Remaining-feature probe has not run.";
    private static string lastCharacterAiFeatureReport = "Character AI feature probe has not run.";
    private static string lastClosingGapReport = "Closing-gap probe has not run.";
    private static string lastTimedGapReport = "Timed-gap probe has not run yet.";
    private static string lastCharacterAiLongObservationReport = "Character AI long observation probe has not run yet.";
    private static string lastCoreGridUiCompletionReport = "Core/Grid/UI completion probe has not run yet.";
    private static string lastCompletionAuditReport = "Completion-audit probe has not run yet.";
    private static bool capturingLogs;

    [MenuItem("DungeonStory/Debug/QA/Run SampleScene Intruder Probe")]
    public static void RunSampleSceneIntruderProbeFromMenu()
    {
        Debug.Log(RunSampleSceneIntruderProbe());
    }

    [MenuItem("DungeonStory/Debug/QA/Run SampleScene Feature Family Probe")]
    public static void RunSampleSceneFeatureFamilyProbeFromMenu()
    {
        Debug.Log(RunSampleSceneFeatureFamilyProbe());
    }

    [MenuItem("DungeonStory/Debug/QA/Run SampleScene Remaining Feature Probe")]
    public static void RunSampleSceneRemainingFeatureProbeFromMenu()
    {
        Debug.Log(RunSampleSceneRemainingFeatureProbe());
    }

    [MenuItem("DungeonStory/Debug/QA/Run Character AI Feature Probe")]
    public static void RunCharacterAiFeatureProbeFromMenu()
    {
        Debug.Log(RunCharacterAiFeatureProbe());
    }

    [MenuItem("DungeonStory/Debug/QA/Run SampleScene Closing Gap Probe")]
    public static void RunSampleSceneClosingGapProbeFromMenu()
    {
        Debug.Log(RunSampleSceneClosingGapProbe());
    }

    [MenuItem("DungeonStory/Debug/QA/Start SampleScene Timed Gap Probe")]
    public static void StartSampleSceneTimedGapProbeFromMenu()
    {
        Debug.Log(StartSampleSceneTimedGapProbe());
    }

    [MenuItem("DungeonStory/Debug/QA/Start Character AI Long Observation Probe")]
    public static void StartCharacterAiLongObservationProbeFromMenu()
    {
        Debug.Log(StartCharacterAiLongObservationProbe());
    }

    [MenuItem("DungeonStory/Debug/QA/Start Core Grid UI Completion Probe")]
    public static void StartCoreGridUiCompletionProbeFromMenu()
    {
        Debug.Log(StartCoreGridUiCompletionProbe());
    }

    [MenuItem("DungeonStory/Debug/QA/Start Completion Audit Probe")]
    public static void StartCompletionAuditProbeFromMenu()
    {
        Debug.Log(StartCompletionAuditProbe());
    }

    public static string RunSampleSceneIntruderProbe()
    {
        if (!Application.isPlaying)
        {
            lastIntruderReport = "FAIL: Probe requires PlayMode.";
            return lastIntruderReport;
        }

        StartLogCapture();
        Time.timeScale = 8f;
        DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery();

        InvasionDirectorRuntime director = sceneQuery.First<InvasionDirectorRuntime>(includeInactive: true);

        CharacterSO defaultIntruder = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset");
        CharacterSpawner spawner = sceneQuery.First<CharacterSpawner>(includeInactive: true);
        GridSystemManager gridManager = sceneQuery.First<GridSystemManager>(includeInactive: true);
        Grid grid = gridManager != null ? gridManager.grid : null;
        bool entryResolved = InvasionIntruderEntryResolver.TryResolve(spawner, grid, out InvasionIntruderEntry entry);

        int beforeIntruders = CountRuntimeInActiveScene<InvasionIntruderRuntime>();
        bool spawned = false;
        bool intruderAssigned = false;
        string exception = string.Empty;

        if (director != null)
        {
            try
            {
                InvasionThreatSnapshot snapshot = new InvasionThreatSnapshot(
                    125f,
                    InvasionThreatStage.Candidate,
                    new InvasionThreatFactors(6f, 4f, 3f, 1f),
                    0f,
                    0f);
                spawned = director.TrySpawnIntruder(snapshot, out CharacterActor intruder);
                intruderAssigned = intruder != null;
            }
            catch (Exception ex)
            {
                exception = $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        int afterIntruders = CountRuntimeInActiveScene<InvasionIntruderRuntime>();
        int activeIntruders = director != null && director.ActiveIntruders != null
            ? director.ActiveIntruders.Count
            : -1;

        lastIntruderReport =
            $"spawned={spawned}; intruderAssigned={intruderAssigned}; " +
            $"director={(director != null ? director.name : "<null>")}; " +
            $"defaultIntruder={(defaultIntruder != null ? defaultIntruder.name : "<null>")}; " +
            $"entryResolved={entryResolved}; entryGrid={(entryResolved ? entry.GridPosition.ToString() : "<none>")}; " +
            $"beforeIntruders={beforeIntruders}; afterIntruders={afterIntruders}; activeIntruders={activeIntruders}; " +
            $"capturedErrors={capturedErrors.Count}; exception={(string.IsNullOrEmpty(exception) ? "<none>" : exception)}";

        return lastIntruderReport;
    }

    public static string GetSampleSceneIntruderProbeReport()
    {
        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors);

        return $"{lastIntruderReport}; currentIntruders={CountRuntimeInActiveScene<InvasionIntruderRuntime>()}; " +
            $"currentActors={CountRuntimeInActiveScene<CharacterActor>()}; capturedErrors={capturedErrors.Count}; errors={errors}";
    }

    public static string RunSampleSceneFeatureFamilyProbe()
    {
        if (!Application.isPlaying)
        {
            lastFeatureFamilyReport = "FAIL: Probe requires PlayMode.";
            return lastFeatureFamilyReport;
        }

        StartLogCapture();

        float originalTimeScale = Time.timeScale;
        GameData gameData = null;
        int originalMoney = 0;
        bool shouldRestoreMoney = false;
        List<string> lines = new List<string>();

        try
        {
            Time.timeScale = 4f;
            Scene activeScene = SceneManager.GetActiveScene();
            DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery();

            GameManager gameManager = sceneQuery.First<GameManager>(includeInactive: true);
            gameData = gameManager != null ? gameManager.gameData : null;
            if (gameData != null && gameData.holdingMoney != null)
            {
                originalMoney = gameData.holdingMoney.Value;
                shouldRestoreMoney = true;
                gameData.holdingMoney.Value = Mathf.Max(originalMoney, 50000);
            }

            List<BuildableObject> buildings = FindActiveSceneComponents<BuildableObject>()
                .Where((building) => building != null && !building.isDestroy)
                .ToList();
            List<CharacterActor> actors = FindActiveSceneComponents<CharacterActor>()
                .Where((actor) => actor != null)
                .ToList();

            lines.Add($"activeScene={activeScene.path}; buildings={buildings.Count}; actors={actors.Count}");

            DailyFacilityShopRuntime shop = sceneQuery.First<DailyFacilityShopRuntime>(includeInactive: true);
            BlueprintResearchRuntime research = sceneQuery.First<BlueprintResearchRuntime>(includeInactive: true);
            FacilitySynthesisRuntime synthesis = sceneQuery.First<FacilitySynthesisRuntime>(includeInactive: true);
            FacilityEvolutionRuntime evolution = sceneQuery.First<FacilityEvolutionRuntime>(includeInactive: true);
            CodexRuntime codex = sceneQuery.First<CodexRuntime>(includeInactive: true);
            OperatingDaySettlementRuntime settlement = sceneQuery.First<OperatingDaySettlementRuntime>(includeInactive: true);
            EventAlertRuntime alerts = sceneQuery.First<EventAlertRuntime>(includeInactive: true);
            RunVariableRuntime runVariables = sceneQuery.First<RunVariableRuntime>(includeInactive: true);
            OffenseWorldMapRuntime worldMap = sceneQuery.First<OffenseWorldMapRuntime>(includeInactive: true);
            OffenseExpeditionRuntime expedition = sceneQuery.First<OffenseExpeditionRuntime>(includeInactive: true);
            OffenseRewardRuntime rewards = sceneQuery.First<OffenseRewardRuntime>(includeInactive: true);
            MetaProgressionRuntime meta = sceneQuery.First<MetaProgressionRuntime>(includeInactive: true);

            FacilityShopPurchaseResult purchaseResult = default;
            FacilityBlueprintSO blueprint = null;

            RunEventAlertProbe(
                alerts,
                ResolveFromLifetimeScope<IGameEventBus>(),
                lines);
            RunShopProbe(shop, gameData, lines, out purchaseResult);
            if (!purchaseResult.TryGetBlueprint(out blueprint))
            {
                blueprint = FindBlueprintCandidate(research);
            }
            RunResearchProbe(research, blueprint, buildings, lines);
            RunSynthesisProbe(synthesis, buildings, lines);
            RunEvolutionProbe(evolution, buildings, lines);
            RunCodexProbe(codex, lines);
            RunOperatingDayProbe(settlement, buildings, lines);
            RunRunVariableProbe(runVariables, lines);
            RunOffenseProbe(worldMap, expedition, rewards, lines);
            RunMetaProbe(meta, actors, lines);
            RunUiProbe(lines);

            string screenshotPath = "Temp/full-game-qa-samplescene-feature-family.png";
            ScreenCapture.CaptureScreenshot(screenshotPath);
            lines.Add($"screenCapture={screenshotPath}");
        }
        catch (Exception ex)
        {
            lines.Add($"EXCEPTION={ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (shouldRestoreMoney && gameData != null && gameData.holdingMoney != null)
            {
                gameData.holdingMoney.Value = originalMoney;
            }

            Time.timeScale = originalTimeScale;
        }

        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        lines.Add($"capturedErrors={capturedErrors.Count}; errors={errors}");
        lastFeatureFamilyReport = string.Join("\n", lines);
        return lastFeatureFamilyReport;
    }

    public static string GetSampleSceneFeatureFamilyProbeReport()
    {
        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        return $"{lastFeatureFamilyReport}\ncurrentCapturedErrors={capturedErrors.Count}; errors={errors}";
    }

    public static string RunSampleSceneRemainingFeatureProbe()
    {
        if (!Application.isPlaying)
        {
            lastRemainingFeatureReport = "FAIL: Probe requires PlayMode.";
            return lastRemainingFeatureReport;
        }

        StartLogCapture();

        float originalTimeScale = Time.timeScale;
        List<UnityEngine.Object> tempObjects = new List<UnityEngine.Object>();
        List<string> lines = new List<string>();

        try
        {
            Time.timeScale = 4f;
            Scene activeScene = SceneManager.GetActiveScene();
            DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery();
            List<BuildableObject> buildings = FindActiveSceneComponents<BuildableObject>()
                .Where((building) => building != null && !building.isDestroy)
                .ToList();
            List<CharacterActor> actors = FindActiveSceneComponents<CharacterActor>()
                .Where((actor) => actor != null)
                .ToList();

            lines.Add($"activeScene={activeScene.path}; buildings={buildings.Count}; actors={actors.Count}");

            RunAiSceneProbe(sceneQuery, actors, lines);
            RunInventoryLogisticsSceneProbe(buildings, lines);
            RunInvasionDefenseSceneProbe(sceneQuery, buildings, tempObjects, lines);
            RunRecruitmentSceneProbe(sceneQuery, buildings, tempObjects, lines);
            RunStaffDiscontentSceneProbe(sceneQuery, tempObjects, lines);
            RunOffenseCompletionSceneProbe(sceneQuery, tempObjects, lines);
            RunEvolutionSupplementProbe(sceneQuery, buildings, lines);
            RunDebugWorldSupplementProbe(lines);
            RunUiProbe(lines);

            string screenshotPath = "Temp/full-game-qa-samplescene-remaining-feature-families.png";
            ScreenCapture.CaptureScreenshot(screenshotPath);
            lines.Add($"screenCapture={screenshotPath}");
        }
        catch (Exception ex)
        {
            lines.Add($"EXCEPTION={ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            DestroyTempObjects(tempObjects);
            Time.timeScale = originalTimeScale;
        }

        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        lines.Add($"capturedErrors={capturedErrors.Count}; errors={errors}");
        lastRemainingFeatureReport = string.Join("\n", lines);
        return lastRemainingFeatureReport;
    }

    public static string GetSampleSceneRemainingFeatureProbeReport()
    {
        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        return $"{lastRemainingFeatureReport}\ncurrentCapturedErrors={capturedErrors.Count}; errors={errors}";
    }

    public static string RunCharacterAiFeatureProbe()
    {
        if (!Application.isPlaying)
        {
            lastCharacterAiFeatureReport = "FAIL: Probe requires PlayMode.";
            return lastCharacterAiFeatureReport;
        }

        StartLogCapture();

        float originalTimeScale = Time.timeScale;
        List<UnityEngine.Object> tempObjects = new List<UnityEngine.Object>();
        List<string> lines = new List<string>();

        try
        {
            Time.timeScale = 4f;
            Scene activeScene = SceneManager.GetActiveScene();
            DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery();
            List<BuildableObject> buildings = FindActiveSceneComponents<BuildableObject>()
                .Where((building) => building != null && !building.isDestroy)
                .ToList();
            List<CharacterActor> actors = FindActiveSceneComponents<CharacterActor>()
                .Where((actor) => actor != null)
                .ToList();

            lines.Add($"activeScene={activeScene.path}; buildings={buildings.Count}; actors={actors.Count}");

            RunInjectedCustomerAiProbe(sceneQuery, tempObjects, lines);
            RunInjectedStaffDutyPriorityProbe(sceneQuery, tempObjects, lines);
            RunInjectedOwnerPriorityProbe(sceneQuery, tempObjects, lines);
            RunInjectedLocalLlmProbe(sceneQuery, tempObjects, lines);
            RunInjectedCharacterVisualProbe(tempObjects, lines);
            RunUiProbe(lines);

            string screenshotPath = "Temp/full-game-qa-character-ai-feature-probe.png";
            ScreenCapture.CaptureScreenshot(screenshotPath);
            lines.Add($"screenCapture={screenshotPath}");
        }
        catch (Exception ex)
        {
            lines.Add($"EXCEPTION={ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            DestroyTempObjects(tempObjects);
        }

        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        lines.Add($"capturedErrors={capturedErrors.Count}; errors={errors}");
        lastCharacterAiFeatureReport = string.Join("\n", lines);
        return lastCharacterAiFeatureReport;
    }

    public static string GetCharacterAiFeatureProbeReport()
    {
        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        return $"{lastCharacterAiFeatureReport}\ncurrentCapturedErrors={capturedErrors.Count}; errors={errors}";
    }

    public static string RunSampleSceneClosingGapProbe()
    {
        if (!Application.isPlaying)
        {
            lastClosingGapReport = "FAIL: Probe requires PlayMode.";
            return lastClosingGapReport;
        }

        StartLogCapture();

        float originalTimeScale = Time.timeScale;
        List<UnityEngine.Object> tempObjects = new List<UnityEngine.Object>();
        List<string> lines = new List<string>();

        try
        {
            Time.timeScale = 8f;
            Scene activeScene = SceneManager.GetActiveScene();
            DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery();
            GridSystemManager gridManager = sceneQuery.First<GridSystemManager>(includeInactive: true);
            Grid grid = gridManager != null ? gridManager.grid : null;
            List<BuildableObject> buildings = FindActiveSceneComponents<BuildableObject>()
                .Where((building) => building != null && !building.isDestroy)
                .ToList();

            lines.Add($"activeScene={activeScene.path}; grid={(grid != null ? $"{grid.width}x{grid.height}" : "<none>")}; buildings={buildings.Count}");

            RunGridPlacementAndDefenseProbe(sceneQuery, grid, tempObjects, lines);
            RunInvasionFinalCombatProbe(sceneQuery, grid, tempObjects, lines);
            RunVisibleBubbleProbe(sceneQuery, grid, tempObjects, lines);
            RunUiProbe(lines);

            string screenshotPath = "Temp/full-game-qa-samplescene-closing-gap-probe.png";
            ScreenCapture.CaptureScreenshot(screenshotPath);
            lines.Add($"screenCapture={screenshotPath}");
        }
        catch (Exception ex)
        {
            lines.Add($"EXCEPTION={ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            DestroyTempObjects(tempObjects);
            Time.timeScale = originalTimeScale;
        }

        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        lines.Add($"capturedErrors={capturedErrors.Count}; errors={errors}");
        lastClosingGapReport = string.Join("\n", lines);
        return lastClosingGapReport;
    }

    public static string GetSampleSceneClosingGapProbeReport()
    {
        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        return $"{lastClosingGapReport}\ncurrentCapturedErrors={capturedErrors.Count}; errors={errors}";
    }

    public static string StartSampleSceneTimedGapProbe()
    {
        if (!Application.isPlaying)
        {
            lastTimedGapReport = "FAIL: Probe requires PlayMode.";
            return lastTimedGapReport;
        }

        StartLogCapture();
        TimedGapProbeRunner existing = UnityEngine.Object.FindFirstObjectByType<TimedGapProbeRunner>();
        if (existing != null)
        {
            lastTimedGapReport = "RUNNING: Timed-gap probe is already active.";
            return lastTimedGapReport;
        }

        GameObject runnerObject = new GameObject("QA Timed Gap Probe Runner");
        TimedGapProbeRunner runner = runnerObject.AddComponent<TimedGapProbeRunner>();
        lastTimedGapReport = "RUNNING: Timed-gap probe started.";
        runner.Begin();
        return lastTimedGapReport;
    }

    public static string GetSampleSceneTimedGapProbeReport()
    {
        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        return $"{lastTimedGapReport}\ncurrentCapturedErrors={capturedErrors.Count}; errors={errors}";
    }

    public static string StartCharacterAiLongObservationProbe()
    {
        if (!Application.isPlaying)
        {
            lastCharacterAiLongObservationReport = "FAIL: Probe requires PlayMode.";
            return lastCharacterAiLongObservationReport;
        }

        StartLogCapture();
        CharacterAiLongObservationRunner existing = UnityEngine.Object.FindFirstObjectByType<CharacterAiLongObservationRunner>();
        if (existing != null)
        {
            lastCharacterAiLongObservationReport = "RUNNING: Character AI long observation probe is already active.";
            return lastCharacterAiLongObservationReport;
        }

        GameObject runnerObject = new GameObject("QA Character AI Long Observation Runner");
        CharacterAiLongObservationRunner runner = runnerObject.AddComponent<CharacterAiLongObservationRunner>();
        lastCharacterAiLongObservationReport = "RUNNING: Character AI long observation probe started.";
        runner.Begin();
        return lastCharacterAiLongObservationReport;
    }

    public static string GetCharacterAiLongObservationProbeReport()
    {
        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        return $"{lastCharacterAiLongObservationReport}\ncurrentCapturedErrors={capturedErrors.Count}; errors={errors}";
    }

    public static string StartCoreGridUiCompletionProbe()
    {
        if (!Application.isPlaying)
        {
            lastCoreGridUiCompletionReport = "FAIL: Probe requires PlayMode.";
            return lastCoreGridUiCompletionReport;
        }

        StartLogCapture();
        CoreGridUiCompletionRunner existing = UnityEngine.Object.FindFirstObjectByType<CoreGridUiCompletionRunner>();
        if (existing != null)
        {
            lastCoreGridUiCompletionReport = "RUNNING: Core/Grid/UI completion probe is already active.";
            return lastCoreGridUiCompletionReport;
        }

        GameObject runnerObject = new GameObject("QA Core Grid UI Completion Runner");
        CoreGridUiCompletionRunner runner = runnerObject.AddComponent<CoreGridUiCompletionRunner>();
        lastCoreGridUiCompletionReport = "RUNNING: Core/Grid/UI completion probe started.";
        runner.Begin();
        return lastCoreGridUiCompletionReport;
    }

    public static string GetCoreGridUiCompletionProbeReport()
    {
        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        return $"{lastCoreGridUiCompletionReport}\ncurrentCapturedErrors={capturedErrors.Count}; errors={errors}";
    }

    public static string StartCompletionAuditProbe()
    {
        if (!Application.isPlaying)
        {
            lastCompletionAuditReport = "FAIL: Probe requires PlayMode.";
            return lastCompletionAuditReport;
        }

        StartLogCapture();
        CompletionAuditRunner existing = UnityEngine.Object.FindFirstObjectByType<CompletionAuditRunner>();
        if (existing != null)
        {
            lastCompletionAuditReport = "RUNNING: Completion-audit probe is already active.";
            return lastCompletionAuditReport;
        }

        GameObject runnerObject = new GameObject("QA Completion Audit Runner");
        CompletionAuditRunner runner = runnerObject.AddComponent<CompletionAuditRunner>();
        lastCompletionAuditReport = "RUNNING: Completion-audit probe started.";
        runner.Begin();
        return lastCompletionAuditReport;
    }

    public static string GetCompletionAuditProbeReport()
    {
        string errors = capturedErrors.Count == 0
            ? "<none>"
            : string.Join(" || ", capturedErrors.Select(CompactLog));

        return $"{lastCompletionAuditReport}\ncurrentCapturedErrors={capturedErrors.Count}; errors={errors}";
    }

    private sealed class CompletionAuditRunner : MonoBehaviour
    {
        private const string SampleSceneName = "SampleScene";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

        public void Begin()
        {
            DontDestroyOnLoad(gameObject);
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float originalTimeScale = Time.timeScale;
            Scene originalScene = SceneManager.GetActiveScene();
            string originalScenePath = originalScene.IsValid() ? originalScene.path : string.Empty;
            bool loadedSampleScene = false;
            bool sampleSceneUnloaded = false;
            List<UnityEngine.Object> tempObjects = new List<UnityEngine.Object> { gameObject };
            List<string> lines = new List<string>();
            bool startedInSampleScene = string.Equals(originalScenePath, SampleScenePath, StringComparison.OrdinalIgnoreCase);
            List<RootActiveState> originalRootStates = startedInSampleScene
                ? new List<RootActiveState>()
                : CaptureRootActiveStates(originalScene);

            Time.timeScale = 1f;
            Exception outerException = null;
            if (!startedInSampleScene)
            {
                SetRootActiveStates(originalRootStates, active: false);
            }

            if (!string.Equals(originalScenePath, SampleScenePath, StringComparison.OrdinalIgnoreCase))
            {
                if (!Application.CanStreamedLevelBeLoaded(SampleSceneName)
                    && !Application.CanStreamedLevelBeLoaded(SampleScenePath))
                {
                    lines.Add($"SAMPLE-SCENE loadable=False; name={SampleSceneName}");
                }
                else
                {
                    AsyncOperation load = SceneManager.LoadSceneAsync(SampleSceneName, LoadSceneMode.Additive);
                    while (load != null && !load.isDone)
                    {
                        yield return null;
                    }

                    loadedSampleScene = true;
                }
            }

            Scene sampleScene = SceneManager.GetSceneByPath(SampleScenePath);
            if (!sampleScene.IsValid())
            {
                sampleScene = SceneManager.GetSceneByName(SampleSceneName);
            }

            if (sampleScene.IsValid() && sampleScene.isLoaded)
            {
                SceneManager.SetActiveScene(sampleScene);
            }

            yield return null;
            yield return new WaitForSecondsRealtime(0.75f);

            try
            {
                Scene activeScene = SceneManager.GetActiveScene();
                DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery();
                Grid grid = ResolveGrid(sceneQuery);
                List<BuildableObject> buildings = FindActiveSceneComponents<BuildableObject>()
                    .Where((building) => building != null && !building.isDestroy)
                    .ToList();

                lines.Add(
                    $"activeScene={activeScene.path}; loadedSampleScene={loadedSampleScene}; " +
                    $"roots={activeScene.rootCount}; lifetimeScopes={CountRuntimeInActiveScene<LifetimeScope>()}; " +
                    $"grid={(grid != null ? $"{grid.width}x{grid.height}" : "<none>")}; buildings={buildings.Count}");

                RunOwnerModelRunEndProbe(sceneQuery, grid, tempObjects, lines);
                RunCombatReportContributionProbe(sceneQuery, grid, buildings, tempObjects, lines);
                RunRebellionCommandCompletionProbe(sceneQuery, grid, tempObjects, lines);
                RunUiProbe(lines);

                string screenshotPath = "Temp/full-game-qa-completion-audit-probe.png";
                ScreenCapture.CaptureScreenshot(screenshotPath);
                lines.Add($"screenCapture={screenshotPath}");
            }
            catch (Exception ex)
            {
                outerException = ex;
                lines.Add($"COMPLETION-AUDIT exception={ex.GetType().Name}: {CompactText(ex.Message)}");
            }

            DestroyTempObjects(tempObjects.Where((item) => item != gameObject));

            Time.timeScale = originalTimeScale;
            string errors = capturedErrors.Count == 0
                ? "<none>"
                : string.Join(" || ", capturedErrors.Select(CompactLog));
            lines.Add($"loadedSampleSceneAdditive={loadedSampleScene}; activeSceneBeforeRestore={(SceneManager.GetActiveScene().IsValid() ? SceneManager.GetActiveScene().path : "<none>")}; originalScene={originalScenePath}");
            lines.Add($"capturedErrors={capturedErrors.Count}; errors={errors}");
            if (outerException != null)
            {
                lines.Add($"outerException={outerException.GetType().Name}: {CompactText(outerException.Message)}");
            }

            lastCompletionAuditReport = string.Join("\n", lines);
            StopLogCapture();

            if (originalScene.IsValid() && originalScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalScene);
            }

            if (loadedSampleScene && sampleScene.IsValid() && sampleScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(sampleScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }

                sampleSceneUnloaded = true;
            }

            RestoreRootActiveStates(originalRootStates);
            if (sampleSceneUnloaded)
            {
                string report = lastCompletionAuditReport + $"\nsampleSceneUnloaded=True; restoredRoots={originalRootStates.Count}";
                lastCompletionAuditReport = report;
            }

            Destroy(gameObject);
        }
    }

    private readonly struct RootActiveState
    {
        public RootActiveState(GameObject root, bool activeSelf)
        {
            Root = root;
            ActiveSelf = activeSelf;
        }

        public GameObject Root { get; }
        public bool ActiveSelf { get; }
    }

    private static List<RootActiveState> CaptureRootActiveStates(Scene scene)
    {
        List<RootActiveState> states = new List<RootActiveState>();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return states;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root != null)
            {
                states.Add(new RootActiveState(root, root.activeSelf));
            }
        }

        return states;
    }

    private static void SetRootActiveStates(IEnumerable<RootActiveState> states, bool active)
    {
        foreach (RootActiveState state in states)
        {
            if (state.Root != null)
            {
                state.Root.SetActive(active);
            }
        }
    }

    private static void RestoreRootActiveStates(IEnumerable<RootActiveState> states)
    {
        foreach (RootActiveState state in states)
        {
            if (state.Root != null)
            {
                state.Root.SetActive(state.ActiveSelf);
            }
        }
    }

    private static void RunOwnerModelRunEndProbe(
        DungeonSceneComponentQuery sceneQuery,
        Grid grid,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        OwnerRunManager manager = sceneQuery.First<OwnerRunManager>(includeInactive: true);
        OwnerSelectionPanel selectionPanel = sceneQuery.First<OwnerSelectionPanel>(includeInactive: true);
        MetaProgressionRuntime meta = sceneQuery.First<MetaProgressionRuntime>(includeInactive: true);
        bool panelBuilt = false;
        int panelButtons = -1;
        bool selectedByIndex = false;
        bool ownerSpawned = false;
        bool profileConnected = false;
        bool speciesPresent = false;
        bool traitsPresent = false;
        bool statsFromProfile = false;
        bool ownerDeathApplied = false;
        bool runEnded = false;
        bool metaResult = false;
        int runResultPanels = -1;
        string speciesTag = string.Empty;
        int traitCount = -1;
        float healthBefore = -1f;
        float healthAfter = -1f;
        string exception = string.Empty;

        try
        {
            if (selectionPanel != null)
            {
                selectionPanel.gameObject.SetActive(true);
                selectionPanel.BuildOptions();
                panelBuilt = true;
                panelButtons = selectionPanel.GetComponentsInChildren<Button>(true).Length;
            }

            if (manager != null && manager.OwnerCandidates != null && manager.OwnerCandidates.Count > 0)
            {
                manager.SelectOwnerByIndex(0);
                selectedByIndex = manager.selectedOwnerData != null && manager.selectedOwnerData.Value != null;
            }

            CharacterActor owner = manager != null ? manager.CurrentOwnerActor : null;
            ownerSpawned = owner != null && owner.IsOwner;
            if (owner != null)
            {
                TryPlaceActorAtNearestWalkable(grid, owner, Vector2Int.zero, out _);
                CharacterIdentity identity = owner.Identity;
                CharacterRuntimeProfile profile = identity != null ? identity.Profile : null;
                speciesTag = profile != null ? profile.SpeciesTag : owner.SpeciesTag;
                traitCount = profile != null && profile.Traits != null ? profile.Traits.Count : -1;
                profileConnected = identity != null && identity.Data != null && profile != null;
                speciesPresent = !string.IsNullOrWhiteSpace(speciesTag);
                traitsPresent = traitCount > 0;
                statsFromProfile = owner.Stats != null
                    && owner.Stats.GetCharacterStat(CharacterStatType.Toughness) > 0
                    && owner.Stats.GetMoveSpeed() > 0f
                    && owner.Stats.GetCombatPowerMultiplier() > 0f;

                if (meta != null)
                {
                    meta.SetShowRunResultPanel(true);
                }

                healthBefore = owner.CurrentHealth;
                owner.ApplyDamage(owner.MaxHealth + 100f, "QA owner completion audit");
                healthAfter = owner.CurrentHealth;
                ownerDeathApplied = owner.IsDead && healthAfter <= 0f;
            }

            runEnded = manager != null && manager.IsRunEnded;
            metaResult = meta != null && meta.LatestResult != null;
            runResultPanels = CountRuntimeInActiveScene<RunResultPanel>();
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add(
            $"OWNER-MODEL-RUN panel={(selectionPanel != null)}; panelBuilt={panelBuilt}; panelButtons={panelButtons}; " +
            $"manager={(manager != null)}; candidates={(manager != null && manager.OwnerCandidates != null ? manager.OwnerCandidates.Count : -1)}; " +
            $"selectedByIndex={selectedByIndex}; ownerSpawned={ownerSpawned}; profileConnected={profileConnected}; " +
            $"species={CompactText(speciesTag)}; speciesPresent={speciesPresent}; traitCount={traitCount}; traitsPresent={traitsPresent}; " +
            $"statsFromProfile={statsFromProfile}; ownerDeathApplied={ownerDeathApplied}; health={healthBefore:0.###}->{healthAfter:0.###}; " +
            $"runEnded={runEnded}; meta={(meta != null)}; metaResult={metaResult}; runResultPanels={runResultPanels}; exception={CompactText(exception)}");
    }

    private static void RunCombatReportContributionProbe(
        DungeonSceneComponentQuery sceneQuery,
        Grid grid,
        IReadOnlyList<BuildableObject> buildings,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        InvasionCombatReportRuntime combatReport = sceneQuery.First<InvasionCombatReportRuntime>(includeInactive: true);
        IDefenseStatusRuntimeService defenseStatus = ResolveFromLifetimeScope<IDefenseStatusRuntimeService>();
        GridBuildingPlacementService placementService = CreateQaPlacementService(grid, tempObjects);
        BuildingSO defenseData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset");
        CharacterActor intruder = CreateQaCharacter(
            "QA Report Intruder",
            CharacterType.Intruder,
            CharacterRole.Regular,
            80,
            addWork: false,
            addShopping: false,
            tempObjects);
        CharacterActor owner = CreateQaCharacter(
            "QA Report Owner",
            CharacterType.NPC,
            CharacterRole.Owner,
            80,
            addWork: true,
            addShopping: false,
            tempObjects);

        bool foundPosition = false;
        bool placed = false;
        bool destroyed = false;
        Vector2Int position = Vector2Int.zero;
        DefenseFacility defense = null;
        List<DefenseActivationReport> reports = new List<DefenseActivationReport>();
        BuildableObject damagedFacility = null;
        InvasionCombatReportSnapshot report = null;
        string detail = string.Empty;
        string message = string.Empty;

        try
        {
            foundPosition = TryFindPlaceablePosition(placementService, defenseData, grid, out position, out message);
            if (foundPosition)
            {
                placed = placementService.TryPlaceBuilding(defenseData, position, out message);
                defense = placed && grid != null
                    ? grid.GetGridCell(position)?.GetBuilding() as DefenseFacility
                    : null;
            }

            TryPlaceActorAtNearestWalkable(grid, intruder, position, out Vector2Int intruderPos);
            TryPlaceActorAtNearestWalkable(grid, owner, new Vector2Int(1, 0), out _);

            InvasionThreatSnapshot snapshot = new InvasionThreatSnapshot(
                180f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(8f, 7f, 6f, 2f),
                0f,
                0f);
            ResolveFromLifetimeScope<IGameEventBus>()
                ?.Publish(new InvasionStartedEvent(snapshot));
            ResolveFromLifetimeScope<IGameEventBus>()
                ?.Publish(new InvasionSpawnedEvent(intruder, snapshot));

            DefenseTriggerTiming[] timings =
            {
                DefenseTriggerTiming.OnEnter,
                DefenseTriggerTiming.GuardResponse,
                DefenseTriggerTiming.Periodic,
                DefenseTriggerTiming.Cooldown
            };
            foreach (DefenseTriggerTiming timing in timings)
            {
                reports = DefenseFacilityResolver.TriggerAt(grid, intruder, position, timing, defenseStatus);
                if (reports.Count > 0)
                {
                    break;
                }
            }

            damagedFacility = buildings.FirstOrDefault((building) =>
                building != null && building.Facility != null && building != defense);
            if (damagedFacility != null)
            {
                damagedFacility.SetDamaged(true);
                ResolveFromLifetimeScope<IGameEventBus>()
                    ?.Publish(new InvasionFacilityDamagedEvent(intruder, damagedFacility));
            }

            ResolveFromLifetimeScope<IGameEventBus>()
                ?.Publish(new InvasionFinalCombatStartedEvent(intruder, owner));
            ResolveFromLifetimeScope<IGameEventBus>()
                ?.Publish(new InvasionResolvedEvent(true, 0.25f));
            report = combatReport != null ? combatReport.CurrentReport : null;
            detail = report != null ? report.ToDetailText() : string.Empty;
        }
        catch (Exception ex)
        {
            message = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            if (placementService != null && defense != null)
            {
                destroyed = placementService.TryDestroyBuilding(defense, out _, out string destroyMessage);
                if (!string.IsNullOrWhiteSpace(destroyMessage))
                {
                    message = destroyMessage;
                }
            }
        }

        DefenseContributionSnapshot topDamage = report != null ? report.TopDamageContribution : null;
        lines.Add(
            $"COMBAT-REPORT runtime={(combatReport != null)}; defenseStatus={defenseStatus != null}; " +
            $"defenseAsset={defenseData != null}; foundPosition={foundPosition}; placed={placed}; position={position}; " +
            $"triggeredReports={reports.Count}; report={(report != null)}; resolved={(report != null && report.IsResolved)}; " +
            $"defenseContributions={(report != null ? report.DefenseContributions.Count : -1)}; topDamage={(topDamage != null ? topDamage.TotalDamage : 0f):0.###}; " +
            $"damagedFacilities={(report != null ? report.DamagedFacilities.Count : -1)}; finalCombat={(report != null && report.FinalCombatStarted)}; " +
            $"detailLength={detail.Length}; destroyed={destroyed}; message={CompactText(message)}");
    }

    private static void RunRebellionCommandCompletionProbe(
        DungeonSceneComponentQuery sceneQuery,
        Grid grid,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        StaffDiscontentRuntime discontent = sceneQuery.First<StaffDiscontentRuntime>(includeInactive: true);
        OwnerCommandController controller = sceneQuery.First<OwnerCommandController>(includeInactive: true);
        CharacterActor guard = CreateQaCharacter(
            "QA Command Guard",
            CharacterType.NPC,
            CharacterRole.Regular,
            90,
            addWork: true,
            addShopping: false,
            tempObjects);
        CharacterActor rebel = CreateQaCharacter(
            "QA Command Rebel",
            CharacterType.NPC,
            CharacterRole.Regular,
            5,
            addWork: true,
            addShopping: false,
            tempObjects);

        bool placedGuard = TryPlaceActorAtNearestWalkable(grid, guard, new Vector2Int(0, 0), out Vector2Int guardPos);
        bool placedRebel = TryPlaceActorAtNearestWalkable(grid, rebel, new Vector2Int(2, 0), out Vector2Int rebelPos);
        bool outcomeLocalRebellion = false;
        bool selectedByController = false;
        bool commandInvoked = false;
        bool commandSet = false;
        bool destinationResolved = false;
        bool suppressed = false;
        StaffDiscontentStage stage = StaffDiscontentStage.Stable;
        StaffDiscontentOutcome outcome = StaffDiscontentOutcome.None;
        string message = string.Empty;

        try
        {
            rebel.stats[CharacterCondition.MOOD] = 5f;
            StaffDiscontentRecord record = discontent != null
                ? discontent.ProcessStaff(rebel, out outcome)
                : null;
            stage = record != null ? record.Stage : StaffDiscontentStage.Stable;
            outcomeLocalRebellion = outcome == StaffDiscontentOutcome.LocalRebellion
                || (record != null && record.IsInLocalRebellion);

            if (controller != null)
            {
                controller.OnTriggerEvent(new InfoFeedEvent(CharacterActor.From(guard)));
                selectedByController = controller.SelectedActor == guard;
                MethodInfo suppressMethod = typeof(OwnerCommandController).GetMethod(
                    "TryIssueSuppressCommand",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                suppressMethod?.Invoke(controller, new object[] { rebel });
                commandInvoked = suppressMethod != null;
            }

            if (guard.TryGetAbility(out AbilityWork work))
            {
                GridPathSearchResult search = grid != null ? grid.SearchPath(guard.GetNowXY()) : null;
                commandSet = work.HasPrioritySuppressTarget && work.PrioritySuppressActor == rebel;
                destinationResolved = work.TryGetPrioritySuppressDestination(search, out _);
            }

            suppressed = discontent != null && discontent.ResolveSuppressedRebel(rebel, guard);
        }
        catch (Exception ex)
        {
            message = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add(
            $"REBELLION-COMMAND runtime={(discontent != null)}; controller={(controller != null)}; " +
            $"placedGuard={placedGuard}; guardPos={guardPos}; placedRebel={placedRebel}; rebelPos={rebelPos}; " +
            $"outcome={outcome}; stage={stage}; localRebellion={outcomeLocalRebellion}; selectedByController={selectedByController}; " +
            $"commandInvoked={commandInvoked}; commandSet={commandSet}; destinationResolved={destinationResolved}; " +
            $"suppressed={suppressed}; message={CompactText(message)}");
    }

    private sealed class CoreGridUiCompletionRunner : MonoBehaviour
    {
        public void Begin()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float originalTimeScale = Time.timeScale;
            Vector3 originalCameraPosition = Vector3.zero;
            bool shouldRestoreCamera = false;
            List<UnityEngine.Object> tempObjects = new List<UnityEngine.Object> { gameObject };
            List<string> lines = new List<string>();

            try
            {
                Time.timeScale = 1f;
                yield return null;

                Scene activeScene = SceneManager.GetActiveScene();
                DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery();
                GridSystemManager gridManager = sceneQuery.First<GridSystemManager>(includeInactive: true);
                GridUIManager gridUiManager = sceneQuery.First<GridUIManager>(includeInactive: true);
                UIManager uiManager = sceneQuery.First<UIManager>(includeInactive: true);
                CameraManager cameraManager = sceneQuery.First<CameraManager>(includeInactive: true);
                Camera mainCamera = Camera.main ?? sceneQuery.First<Camera>(includeInactive: true);
                Grid grid = gridManager != null ? gridManager.grid : null;
                List<BuildableObject> buildings = FindActiveSceneComponents<BuildableObject>()
                    .Where((building) => building != null && !building.isDestroy)
                    .ToList();

                lines.Add(
                    $"activeScene={activeScene.path}; roots={activeScene.rootCount}; " +
                    $"lifetimeScopes={CountRuntimeInActiveScene<LifetimeScope>()}; " +
                    $"gameManagers={CountRuntimeInActiveScene<GameManager>()}; " +
                    $"gridManagers={CountRuntimeInActiveScene<GridSystemManager>()}; " +
                    $"gridUiManagers={CountRuntimeInActiveScene<GridUIManager>()}; " +
                    $"uiManagers={CountRuntimeInActiveScene<UIManager>()}; " +
                    $"cameraManagers={CountRuntimeInActiveScene<CameraManager>()}; " +
                    $"cameras={CountRuntimeInActiveScene<Camera>()}; " +
                    $"grid={(grid != null ? $"{grid.width}x{grid.height}" : "<none>")}; buildings={buildings.Count}");

                bool foundationPass = false;
                string foundationException = string.Empty;
                try
                {
                    foundationPass = GridFoundationDebugScenarios.RunAll(false);
                }
                catch (Exception ex)
                {
                    foundationException = $"{ex.GetType().Name}: {ex.Message}";
                }

                Vector2Int start = Vector2Int.zero;
                Vector2Int destination = Vector2Int.zero;
                bool reachablePairFound = grid != null && TryFindReachablePair(grid, out start, out destination);
                GridPathSearchResult search = reachablePairFound ? grid.SearchPath(start) : null;
                int reachableCount = search != null ? search.GetReachablePositions().Count : 0;
                bool destinationWalkable = reachablePairFound && grid.IsWalkable(destination);
                lines.Add(
                    $"GRID-COMPLETION foundationPass={foundationPass}; " +
                    $"foundationException={CompactText(foundationException)}; " +
                    $"reachablePairFound={reachablePairFound}; start={start}; destination={destination}; " +
                    $"reachableCount={reachableCount}; destinationWalkable={destinationWalkable}");

                bool cameraChecked = cameraManager != null && mainCamera != null;
                bool cameraClampChangedExtreme = false;
                Vector3 cameraBefore = Vector3.zero;
                Vector3 cameraAfterClamp = Vector3.zero;
                float cameraSize = mainCamera != null ? mainCamera.orthographicSize : 0f;
                if (cameraChecked)
                {
                    shouldRestoreCamera = true;
                    originalCameraPosition = cameraManager.transform.position;
                    cameraBefore = originalCameraPosition;
                    cameraManager.transform.position = new Vector3(9999f, -9999f, -10f);
                    cameraManager.ClampToCurrentBounds();
                    cameraAfterClamp = cameraManager.transform.position;
                    cameraClampChangedExtreme = Vector3.Distance(cameraAfterClamp, new Vector3(9999f, -9999f, -10f)) > 1f
                        && !float.IsNaN(cameraAfterClamp.x)
                        && !float.IsNaN(cameraAfterClamp.y)
                        && Mathf.Abs(cameraAfterClamp.z + 10f) < 0.01f;
                    cameraManager.transform.position = originalCameraPosition;
                    cameraManager.ClampToCurrentBounds();
                }

                lines.Add(
                    $"CORE-CAMERA checked={cameraChecked}; before={cameraBefore}; " +
                    $"afterClamp={cameraAfterClamp}; clampChangedExtreme={cameraClampChangedExtreme}; " +
                    $"orthographicSize={cameraSize:0.###}; screen={Screen.width}x{Screen.height}");

                CanvasGroup touchGuard = uiManager != null ? uiManager.touchGaurd : null;
                bool guardExists = touchGuard != null;
                bool guardBlocked = false;
                bool guardReleased = false;
                if (uiManager != null && touchGuard != null)
                {
                    uiManager.MakeTouchFalse();
                    yield return null;
                    guardBlocked = touchGuard.interactable && touchGuard.blocksRaycasts;
                    uiManager.MakeTouchTrue();
                    yield return null;
                    guardReleased = !touchGuard.interactable && !touchGuard.blocksRaycasts;
                }

                lines.Add($"CORE-UI-TOUCH-GUARD exists={guardExists}; blocked={guardBlocked}; released={guardReleased}");

                UIBuildingInfo info = gridUiManager != null && gridUiManager.buildingInfoUI != null
                    ? gridUiManager.buildingInfoUI
                    : sceneQuery.First<UIBuildingInfo>(includeInactive: true);
                BuildableObject selectedBuilding = buildings.FirstOrDefault((building) =>
                    building != null && building.BuildingData != null);
                CanvasGroup infoGroup = info != null ? info.GetComponent<CanvasGroup>() : null;
                bool infoOpened = false;
                bool infoBlockedTouch = false;
                bool infoDisplayedName = false;
                bool infoClosed = false;
                bool infoReleasedTouch = false;
                string selectedBuildingName = selectedBuilding != null && selectedBuilding.BuildingData != null
                    ? selectedBuilding.BuildingData.objectName
                    : "<none>";
                string displayedName = "<none>";

                if (info != null && selectedBuilding != null)
                {
                    info.CloseDispaly();
                    yield return new WaitForSecondsRealtime(0.2f);
                    info.DisplayBuildingInfo(selectedBuilding);
                    yield return new WaitForSecondsRealtime(0.25f);

                    displayedName = info.nameText != null ? info.nameText.text : "<none>";
                    infoOpened = info.gameObject.activeInHierarchy
                        && infoGroup != null
                        && infoGroup.alpha > 0.9f
                        && infoGroup.interactable
                        && infoGroup.blocksRaycasts;
                    infoDisplayedName = !string.IsNullOrWhiteSpace(displayedName)
                        && displayedName != "<none>";
                    infoBlockedTouch = touchGuard == null || touchGuard.blocksRaycasts;

                    RunUiProbe(lines);
                    string screenshotPath = "Temp/full-game-qa-core-grid-ui-completion.png";
                    ScreenCapture.CaptureScreenshot(screenshotPath);
                    lines.Add($"screenCapture={screenshotPath}");

                    info.CloseDispaly();
                    yield return new WaitForSecondsRealtime(0.25f);
                    infoClosed = infoGroup != null
                        && infoGroup.alpha < 0.1f
                        && !infoGroup.interactable
                        && !infoGroup.blocksRaycasts;
                    infoReleasedTouch = touchGuard == null || !touchGuard.blocksRaycasts;
                }

                lines.Add(
                    $"GRID-UI-BUILDING-INFO info={(info != null)}; selected={(selectedBuilding != null)}; " +
                    $"selectedName={CompactText(selectedBuildingName)}; opened={infoOpened}; " +
                    $"displayedName={CompactText(displayedName)}; nameVisible={infoDisplayedName}; " +
                    $"touchBlocked={infoBlockedTouch}; closed={infoClosed}; touchReleased={infoReleasedTouch}");
            }
            finally
            {
                if (shouldRestoreCamera)
                {
                    CameraManager cameraManager = UnityEngine.Object.FindFirstObjectByType<CameraManager>();
                    if (cameraManager != null)
                    {
                        cameraManager.transform.position = originalCameraPosition;
                    }
                }

                Time.timeScale = originalTimeScale;
                string errors = capturedErrors.Count == 0
                    ? "<none>"
                    : string.Join(" || ", capturedErrors.Select(CompactLog));
                lines.Add($"capturedErrors={capturedErrors.Count}; errors={errors}");
                lastCoreGridUiCompletionReport = string.Join("\n", lines);

                tempObjects.Remove(gameObject);
                DestroyTempObjects(tempObjects);
                Destroy(gameObject);
            }
        }
    }

    private sealed class TimedGapProbeRunner : MonoBehaviour
    {
        public void Begin()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float originalTimeScale = Time.timeScale;
            List<UnityEngine.Object> tempObjects = new List<UnityEngine.Object> { gameObject };
            List<string> lines = new List<string>();

            try
            {
                Time.timeScale = 24f;
                yield return null;

                Scene activeScene = SceneManager.GetActiveScene();
                DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery();
                GridSystemManager gridManager = sceneQuery.First<GridSystemManager>(includeInactive: true);
                Grid grid = gridManager != null ? gridManager.grid : null;
                List<BuildableObject> buildings = FindActiveSceneComponents<BuildableObject>()
                    .Where((building) => building != null && !building.isDestroy)
                    .ToList();

                lines.Add($"activeScene={activeScene.path}; grid={(grid != null ? $"{grid.width}x{grid.height}" : "<none>")}; buildings={buildings.Count}");
                RunEvolutionDetailedProbe(sceneQuery, buildings, lines);
                yield return RunTimedInvasionCoroutine(sceneQuery, grid, lines);
                yield return RunLocalLlmEndpointCoroutine(sceneQuery, lines);
                RunUiProbe(lines);

                string screenshotPath = "Temp/full-game-qa-samplescene-timed-gap-probe.png";
                ScreenCapture.CaptureScreenshot(screenshotPath);
                lines.Add($"screenCapture={screenshotPath}");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                string errors = capturedErrors.Count == 0
                    ? "<none>"
                    : string.Join(" || ", capturedErrors.Select(CompactLog));
                lines.Add($"capturedErrors={capturedErrors.Count}; errors={errors}");
                lastTimedGapReport = string.Join("\n", lines);

                tempObjects.Remove(gameObject);
                DestroyTempObjects(tempObjects);
                Destroy(gameObject);
            }
        }
    }

    private sealed class CharacterAiLongObservationRunner : MonoBehaviour
    {
        public void Begin()
        {
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float originalTimeScale = Time.timeScale;
            List<UnityEngine.Object> tempObjects = new List<UnityEngine.Object> { gameObject };
            List<string> lines = new List<string>();

            try
            {
                Time.timeScale = 12f;
                yield return null;

                Scene activeScene = SceneManager.GetActiveScene();
                DungeonSceneComponentQuery sceneQuery = new DungeonSceneComponentQuery();
                Grid grid = ResolveGrid(sceneQuery);
                List<BuildableObject> buildings = FindActiveSceneComponents<BuildableObject>()
                    .Where((building) => building != null && !building.isDestroy)
                    .ToList();
                lines.Add($"activeScene={activeScene.path}; grid={(grid != null ? $"{grid.width}x{grid.height}" : "<none>")}; buildings={buildings.Count}; timeScale={Time.timeScale:0.##}");

                yield return ObserveLongCustomerVisit(grid, tempObjects, lines);
                yield return ObserveLongStaffWanderAndFatigue(grid, tempObjects, lines);
                RunUiProbe(lines);

                string screenshotPath = "Temp/full-game-qa-character-ai-long-observation.png";
                ScreenCapture.CaptureScreenshot(screenshotPath);
                lines.Add($"screenCapture={screenshotPath}");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                string errors = capturedErrors.Count == 0
                    ? "<none>"
                    : string.Join(" || ", capturedErrors.Select(CompactLog));
                lines.Add($"capturedErrors={capturedErrors.Count}; errors={errors}");
                lastCharacterAiLongObservationReport = string.Join("\n", lines);

                tempObjects.Remove(gameObject);
                DestroyTempObjects(tempObjects);
                Destroy(gameObject);
            }
        }
    }

    private static IEnumerator ObserveLongCustomerVisit(
        Grid grid,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        CharacterActor customer = CreateQaCharacter(
            "QA Long Customer",
            CharacterType.Customer,
            CharacterRole.Regular,
            90,
            addWork: false,
            addShopping: true,
            tempObjects);
        bool placedAtGrid = TryPlaceActorAtNearestWalkable(grid, customer, new Vector2Int(4, 0), out Vector2Int startGrid);
        SetCondition(customer, CharacterCondition.HUNGER, 15f);
        SetCondition(customer, CharacterCondition.FUN, 20f);
        SetCondition(customer, CharacterCondition.MOOD, 85f);

        AbilityShopping shopping = customer != null ? customer.GetAbility<AbilityShopping>() : null;
        AIShopping shoppingAction = Resources.Load<AIShopping>("SO/AI/Action/Shopping");
        AIAction action = shoppingAction != null ? new AIAction { actionset = shoppingAction } : null;
        bool planned = false;
        string failure = string.Empty;
        string destination = string.Empty;
        int visitCountBefore = shopping != null ? shopping.visitCount : -1;
        int visitedBefore = shopping != null && shopping.visitedBuilding != null ? shopping.visitedBuilding.Count : -1;
        Vector3 startWorld = customer != null ? customer.transform.position : Vector3.zero;

        if (customer != null && action != null)
        {
            planned = action.SetDestinationWithFailure(customer, out AIActionFailure actionFailure);
            failure = actionFailure.ToString();
            destination = action.destination != null ? action.destination.name : string.Empty;
            if (planned)
            {
                customer.Brain.bestAction = action;
                action.MarkStarted(Time.time);
                shoppingAction.Execute(customer);
            }
        }

        bool moved = false;
        bool visited = false;
        float deadline = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (customer != null)
            {
                moved = moved || Vector3.Distance(startWorld, customer.transform.position) > 0.1f;
            }

            visited = shopping != null
                && ((shopping.visitedBuilding != null && shopping.visitedBuilding.Count > visitedBefore)
                    || shopping.visitCount < visitCountBefore);
            if (visited)
            {
                break;
            }

            yield return null;
        }

        action?.ReleaseReservation(customer);
        Vector2Int endGrid = grid != null && customer != null ? grid.GetXY(customer.transform.position) : Vector2Int.zero;
        int visitedAfter = shopping != null && shopping.visitedBuilding != null ? shopping.visitedBuilding.Count : -1;
        int visitCountAfter = shopping != null ? shopping.visitCount : -1;

        lines.Add($"LONG-CUSTOMER-VISIT placedAtGrid={placedAtGrid}; start={startGrid}; end={endGrid}; planned={planned}; destination={CompactText(destination)}; moved={moved}; visited={visited}; visitCount={visitCountBefore}->{visitCountAfter}; visitedBuildings={visitedBefore}->{visitedAfter}; failure={CompactText(failure)}");
    }

    private static IEnumerator ObserveLongStaffWanderAndFatigue(
        Grid grid,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        CharacterActor wanderStaff = CreateQaCharacter(
            "QA Long Wander Staff",
            CharacterType.NPC,
            CharacterRole.Regular,
            85,
            addWork: true,
            addShopping: true,
            tempObjects);
        bool wanderPlaced = TryPlaceActorAtNearestWalkable(grid, wanderStaff, new Vector2Int(4, 0), out Vector2Int wanderStart);
        AbilityMove wanderMove = wanderStaff != null ? wanderStaff.GetAbility<AbilityMove>() : null;
        Queue<GridMoveStep> previewPath = null;
        bool wanderPathFound = wanderMove != null
            && wanderMove.TryFindIdleWanderPath(1, 6, out previewPath);
        int wanderPathSteps = previewPath != null ? previewPath.Count : 0;
        Vector3 wanderStartWorld = wanderStaff != null ? wanderStaff.transform.position : Vector3.zero;
        bool wanderStarted = wanderMove != null && wanderMove.StartIdleWander(0.25f, 1, 6);
        bool wanderMoved = false;
        float wanderDeadline = Time.realtimeSinceStartup + 6f;
        while (Time.realtimeSinceStartup < wanderDeadline)
        {
            if (wanderStaff != null)
            {
                wanderMoved = wanderMoved || Vector3.Distance(wanderStartWorld, wanderStaff.transform.position) > 0.1f;
            }

            if (wanderMoved)
            {
                break;
            }

            yield return null;
        }

        Vector2Int wanderEnd = grid != null && wanderStaff != null ? grid.GetXY(wanderStaff.transform.position) : Vector2Int.zero;

        CharacterActor fatigueStaff = CreateQaCharacter(
            "QA Long Fatigue Staff",
            CharacterType.NPC,
            CharacterRole.Regular,
            90,
            addWork: true,
            addShopping: true,
            tempObjects);
        bool fatiguePlaced = TryPlaceActorAtNearestWalkable(grid, fatigueStaff, new Vector2Int(4, 0), out Vector2Int fatigueStart);
        AbilityWork work = fatigueStaff != null ? fatigueStaff.GetAbility<AbilityWork>() : null;
        float sleepBefore = GetCondition(fatigueStaff, CharacterCondition.SLEEP);
        float moodBefore = GetCondition(fatigueStaff, CharacterCondition.MOOD);
        bool workCandidateFound = false;
        bool workStarted = false;
        BuildableObject workTarget = null;
        WorkTypeId workType = default;
        string workMessage = string.Empty;

        if (work != null && grid != null && fatigueStaff != null)
        {
            GridPathSearchResult search = grid.SearchPath(fatigueStaff.GetNowXY());
            workCandidateFound = work.TryGetBestAnyWorkCandidate(search, out WorkTargetCandidate candidate)
                && candidate.IsValid;
            if (workCandidateFound)
            {
                workTarget = candidate.Building;
                workType = candidate.WorkTypeId;
                if (workTarget != null && workTarget.buildPoses != null && workTarget.buildPoses.Count > 0)
                {
                    fatigueStaff.transform.position = grid.GetWorldPos(workTarget.buildPoses[0]);
                }

                work.StartWorking(workType, workTarget);
                workStarted = work.isWorking;
            }
            else
            {
                workMessage = candidate.FailureReason;
            }
        }

        bool fatigueChanged = false;
        float fatigueDeadline = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < fatigueDeadline)
        {
            fatigueChanged = GetCondition(fatigueStaff, CharacterCondition.SLEEP) < sleepBefore
                || GetCondition(fatigueStaff, CharacterCondition.MOOD) < moodBefore;
            if (fatigueChanged)
            {
                break;
            }

            yield return null;
        }

        float sleepAfter = GetCondition(fatigueStaff, CharacterCondition.SLEEP);
        float moodAfter = GetCondition(fatigueStaff, CharacterCondition.MOOD);
        Vector2Int fatigueEnd = grid != null && fatigueStaff != null ? grid.GetXY(fatigueStaff.transform.position) : Vector2Int.zero;

        lines.Add($"LONG-STAFF-WANDER-FATIGUE wanderPlaced={wanderPlaced}; wanderStart={wanderStart}; wanderEnd={wanderEnd}; wanderPathFound={wanderPathFound}; wanderPathSteps={wanderPathSteps}; wanderStarted={wanderStarted}; wanderMoved={wanderMoved}; fatiguePlaced={fatiguePlaced}; fatigueStart={fatigueStart}; fatigueEnd={fatigueEnd}; workCandidate={workCandidateFound}; workStarted={workStarted}; workTarget={(workTarget != null ? workTarget.name : "<none>")}; workType={workType}; fatigueChanged={fatigueChanged}; sleep={sleepBefore:0.##}->{sleepAfter:0.##}; mood={moodBefore:0.##}->{moodAfter:0.##}; message={CompactText(workMessage)}");
    }

    public static void StopLogCapture()
    {
        if (!capturingLogs)
        {
            return;
        }

        Application.logMessageReceived -= OnLogMessageReceived;
        capturingLogs = false;
    }

    private static void StartLogCapture()
    {
        capturedErrors.Clear();
        if (capturingLogs)
        {
            return;
        }

        Application.logMessageReceived += OnLogMessageReceived;
        capturingLogs = true;
    }

    private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
        {
            return;
        }

        capturedErrors.Add(string.IsNullOrWhiteSpace(stackTrace)
            ? condition
            : $"{condition}\n{stackTrace}");
    }

    private static void RunEventAlertProbe(
        EventAlertRuntime alerts,
        IGameEventBus gameEventBus,
        List<string> lines)
    {
        int before = alerts != null ? alerts.EventLog.Count : -1;
        bool callbackFired = false;

        gameEventBus.RaiseAlert(
            "QA Feature Alert",
            "Feature-family probe choice execution.",
            EventAlertImportance.Medium,
            "QA",
            new[]
            {
                new EventAlertChoice("Apply", "QA callback", () => callbackFired = true)
            });

        EventAlertRecord choiceRecord = alerts != null ? alerts.EventLog.LastOrDefault() : null;
        bool opened = false;
        bool choiceExecuted = false;
        if (alerts != null && choiceRecord != null)
        {
            alerts.Open(choiceRecord);
            opened = alerts.IsDetailVisible;
            choiceExecuted = alerts.ExecuteChoice(0);
        }

        gameEventBus.RaiseAlert(
            "QA Visible Alert",
            "Feature-family probe leaves this detail open for visual capture.",
            EventAlertImportance.Low,
            "QA");

        EventAlertRecord visibleRecord = alerts != null ? alerts.EventLog.LastOrDefault() : null;
        if (alerts != null && visibleRecord != null)
        {
            alerts.Open(visibleRecord);
        }

        int after = alerts != null ? alerts.EventLog.Count : -1;
        lines.Add($"EVENT-ALERT runtime={alerts != null}; before={before}; after={after}; opened={opened}; choiceExecuted={choiceExecuted}; callback={callbackFired}; detailVisible={(alerts != null && alerts.IsDetailVisible)}");
    }

    private static void RunShopProbe(
        DailyFacilityShopRuntime shop,
        GameData gameData,
        List<string> lines,
        out FacilityShopPurchaseResult result)
    {
        result = default;
        if (shop == null)
        {
            lines.Add("SHOP runtime=False");
            return;
        }

        shop.Refresh(7, true);
        int dailyOffers = shop.CurrentDailyOffers.Count;
        int basicOffers = shop.CurrentBasicPurchaseOffers.Count;
        int beforeMoney = gameData != null && gameData.holdingMoney != null ? gameData.holdingMoney.Value : -1;
        bool purchased = false;

        if (dailyOffers > 0 && gameData != null)
        {
            purchased = shop.TryPurchaseDailyOffer(0, gameData, out result);
        }

        int afterMoney = gameData != null && gameData.holdingMoney != null ? gameData.holdingMoney.Value : -1;
        result.TryGetBlueprint(out FacilityBlueprintSO purchasedBlueprint);
        result.TryGetBuilding(out BuildingSO purchasedBuilding);
        lines.Add($"SHOP runtime=True; dailyOffers={dailyOffers}; basicOffers={basicOffers}; purchased={purchased}; type={result.offerTypeId}; blueprint={(purchasedBlueprint != null ? purchasedBlueprint.name : "<none>")}; building={(purchasedBuilding != null ? purchasedBuilding.name : "<none>")}; money={beforeMoney}->{afterMoney}; message={CompactText(result.message)}");
    }

    private static void RunResearchProbe(
        BlueprintResearchRuntime research,
        FacilityBlueprintSO blueprint,
        IReadOnlyList<BuildableObject> buildings,
        List<string> lines)
    {
        if (research == null)
        {
            lines.Add("RESEARCH runtime=False");
            return;
        }

        bool queued = false;
        if (blueprint != null && !research.State.IsCompleted(blueprint) && !research.HasActiveResearch)
        {
            queued = research.EnqueueBlueprint(blueprint);
        }

        BuildableObject researchFacility = buildings.FirstOrDefault((building) => building.SupportsWork(BuiltInWorkTypeIds.Research));
        BlueprintResearchWorkResult work = default;
        bool worked = false;
        if (researchFacility != null && research.HasActiveResearch)
        {
            work = research.ApplyResearchWork(null, researchFacility, 999f);
            worked = work.Success;
        }

        lines.Add($"RESEARCH runtime=True; blueprint={(blueprint != null ? blueprint.name : "<none>")}; queued={queued}; hasActive={research.HasActiveResearch}; researchFacility={(researchFacility != null ? researchFacility.name : "<none>")}; worked={worked}; completed={work.Completed}; progress={work.ProgressRatio:0.###}; message={CompactText(work.Message)}");
    }

    private static void RunSynthesisProbe(
        FacilitySynthesisRuntime synthesis,
        IReadOnlyList<BuildableObject> buildings,
        List<string> lines)
    {
        if (synthesis == null)
        {
            lines.Add("SYNTHESIS runtime=False");
            return;
        }

        IReadOnlyList<FacilitySynthesisRecipeSO> visibleRecipes = synthesis.VisibleRecipes;
        FacilitySynthesisRecipeSO recipe = FindMatchingSynthesisRecipe(visibleRecipes, buildings, out List<BuildableObject> materials)
            ?? visibleRecipes.FirstOrDefault((candidate) => candidate != null);

        synthesis.ClearSelection();
        foreach (BuildableObject material in materials)
        {
            synthesis.ToggleMaterialSelection(material);
        }

        FacilitySynthesisResult result = default;
        bool attempted = recipe != null;
        bool success = attempted && synthesis.TrySynthesizeSelected(recipe, out result);

        lines.Add($"SYNTHESIS runtime=True; visibleRecipes={visibleRecipes.Count}; selectedMaterials={synthesis.SelectedMaterials.Count}; matchedMaterials={materials.Count}; attempted={attempted}; success={success}; recipe={(recipe != null ? recipe.name : "<none>")}; result={(result.ResultBuilding != null ? result.ResultBuilding.name : "<none>")}; message={CompactText(result.Message)}");
    }

    private static void RunEvolutionProbe(
        FacilityEvolutionRuntime evolution,
        IReadOnlyList<BuildableObject> buildings,
        List<string> lines)
    {
        if (evolution == null)
        {
            lines.Add("EVOLUTION runtime=False");
            return;
        }

        BuildableObject selectedFacility = null;
        FacilityEvolutionCandidate selectedCandidate = null;
        int candidateCount = 0;

        foreach (BuildableObject building in buildings)
        {
            IReadOnlyList<FacilityEvolutionCandidate> candidates = evolution.GetCandidates(building, includeRejected: true);
            if (candidates.Count > 0 && selectedFacility == null)
            {
                selectedFacility = building;
                candidateCount = candidates.Count;
                selectedCandidate = candidates.FirstOrDefault((candidate) => candidate.Approved)
                    ?? candidates.FirstOrDefault();
            }

            if (selectedCandidate != null && selectedCandidate.Approved)
            {
                break;
            }
        }

        FacilityEvolutionResult result = default;
        bool success = selectedFacility != null
            && selectedCandidate != null
            && selectedCandidate.Approved
            && evolution.TryEvolve(selectedFacility, selectedCandidate.Recipe, out result);

        lines.Add($"EVOLUTION runtime=True; facility={(selectedFacility != null ? selectedFacility.name : "<none>")}; candidates={candidateCount}; approved={(selectedCandidate != null && selectedCandidate.Approved)}; success={success}; recipe={(selectedCandidate != null && selectedCandidate.Recipe != null ? selectedCandidate.Recipe.name : "<none>")}; result={(result.ResultBuilding != null ? result.ResultBuilding.name : "<none>")}; message={CompactText(success ? result.Message : selectedCandidate != null ? selectedCandidate.Reason : string.Empty)}");
    }

    private static void RunEvolutionDetailedProbe(
        DungeonSceneComponentQuery sceneQuery,
        IReadOnlyList<BuildableObject> buildings,
        List<string> lines)
    {
        FacilityEvolutionRuntime evolution = sceneQuery.First<FacilityEvolutionRuntime>(includeInactive: true);
        if (evolution == null)
        {
            lines.Add("EVOLUTION-DETAILED runtime=False");
            return;
        }

        int sceneCandidates = 0;
        int sceneApproved = 0;
        BuildableObject selectedFacility = null;
        FacilityEvolutionCandidate selectedCandidate = null;
        List<string> samples = new List<string>();

        foreach (BuildableObject building in buildings)
        {
            IReadOnlyList<FacilityEvolutionCandidate> candidates = evolution.GetCandidates(building, includeRejected: true);
            sceneCandidates += candidates.Count;
            sceneApproved += candidates.Count((candidate) => candidate.Approved);

            foreach (FacilityEvolutionCandidate candidate in candidates)
            {
                if (samples.Count < 4)
                {
                    samples.Add($"{building.name}:{(candidate.Recipe != null ? candidate.Recipe.name : "<none>")}:{candidate.Approved}:{CompactText(candidate.Validation != null ? candidate.Validation.ToMessage() : candidate.Reason, 100)}:{CompactText(FormatCandidateChecks(candidate), 180)}");
                }

                if (selectedCandidate == null
                    || !selectedCandidate.Approved && candidate.Approved)
                {
                    selectedFacility = building;
                    selectedCandidate = candidate;
                }
            }
        }

        bool qaPrepared = false;
        bool approvedAfterPrepare = selectedCandidate != null && selectedCandidate.Approved;
        bool evolved = false;
        FacilityEvolutionResult result = default;
        string afterPrepareReason = string.Empty;

        if (selectedFacility != null && selectedCandidate != null && !selectedCandidate.Approved)
        {
            qaPrepared = PrepareFacilityForEvolutionQa(selectedFacility, selectedCandidate);
            FacilityEvolutionCandidate refreshed = evolution
                .GetCandidates(selectedFacility, includeRejected: true)
                .FirstOrDefault((candidate) => candidate.Recipe == selectedCandidate.Recipe);
            approvedAfterPrepare = refreshed != null && refreshed.Approved;
            afterPrepareReason = refreshed != null
                ? refreshed.Validation != null ? refreshed.Validation.ToMessage() : refreshed.Reason
                : "candidate disappeared";
            selectedCandidate = refreshed ?? selectedCandidate;
        }

        if (selectedFacility != null
            && selectedCandidate != null
            && selectedCandidate.Approved
            && selectedCandidate.Recipe != null)
        {
            evolved = evolution.TryEvolve(selectedFacility, selectedCandidate.Recipe, out result);
        }

        string selectedFacilityName = selectedFacility != null ? selectedFacility.name : "<none>";
        string selectedRecipeName = selectedCandidate != null && selectedCandidate.Recipe != null
            ? selectedCandidate.Recipe.name
            : "<none>";
        string selectedMessage = evolved
            ? result.Message
            : !string.IsNullOrWhiteSpace(afterPrepareReason)
                ? afterPrepareReason
                : selectedCandidate != null
                    ? selectedCandidate.Reason
                    : string.Empty;
        string sampleSummary = string.Join(" || ", samples);

        lines.Add($"EVOLUTION-DETAILED runtime=True; sceneCandidates={sceneCandidates}; sceneApproved={sceneApproved}; selectedFacility={selectedFacilityName}; selectedRecipe={selectedRecipeName}; selectedApproved={(selectedCandidate != null && selectedCandidate.Approved)}; qaPrepared={qaPrepared}; approvedAfterPrepare={approvedAfterPrepare}; evolved={evolved}; result={(result.ResultBuilding != null ? result.ResultBuilding.name : "<none>")}; message={CompactText(selectedMessage, 180)}; samples={CompactText(sampleSummary, 360)}");
    }

    private static string FormatCandidateChecks(FacilityEvolutionCandidate candidate)
    {
        if (candidate == null || candidate.Validation == null)
        {
            return string.Empty;
        }

        return string.Join(
            "|",
            candidate.Validation.Checks
                .Take(6)
                .Select((check) => $"{check.Category}/{check.Label}/{(check.Passed ? "pass" : "fail")}/{check.Detail}"));
    }

    private static bool PrepareFacilityForEvolutionQa(
        BuildableObject facility,
        FacilityEvolutionCandidate candidate)
    {
        if (facility == null || candidate == null || candidate.Recipe == null)
        {
            return false;
        }

        FacilityEvolutionRecipeSO recipe = candidate.Recipe;
        FacilityEvolutionStateComponent state = facility.GetComponent<FacilityEvolutionStateComponent>()
            ?? facility.gameObject.AddComponent<FacilityEvolutionStateComponent>();
        state.InitializeIfNeeded(facility);
        FacilityEvolutionStateSnapshot snapshot = state.CreateSnapshot();
        snapshot.starGrade = Mathf.Max(snapshot.starGrade, recipe.requiredStarGrade);
        state.ApplySnapshot(snapshot);

        FacilityEvolutionRecordComponent record = facility.GetComponent<FacilityEvolutionRecordComponent>()
            ?? facility.gameObject.AddComponent<FacilityEvolutionRecordComponent>();
        foreach (FacilityEvolutionTokenRequirement requirement in recipe.requiredRecordTokens ?? Array.Empty<FacilityEvolutionTokenRequirement>())
        {
            if (!string.IsNullOrWhiteSpace(requirement.key))
            {
                record.AddToken(requirement.key, Mathf.Max(1, requirement.minCount));
            }
        }

        foreach (FacilityEvolutionMetricRequirement requirement in recipe.requiredRoomMetrics ?? Array.Empty<FacilityEvolutionMetricRequirement>())
        {
            if (string.IsNullOrWhiteSpace(requirement.key))
            {
                continue;
            }

            float value = requirement.requireMin ? requirement.minValue : 0f;
            if (requirement.requireMax)
            {
                value = requirement.requireMin
                    ? Mathf.Min(value, requirement.maxValue)
                    : requirement.maxValue;
            }

            record.SetMetric(requirement.key, value);
        }

        ApplyIdentityEvidenceForQa(record, recipe);
        TryAddEvolutionQaFixture(facility, recipe);
        TryAddEvolutionQaDoor(facility);
        ApplyRoomSizedDensityEvidenceForQa(record, facility, recipe);
        return true;
    }

    private static bool TryAddEvolutionQaDoor(BuildableObject facility)
    {
        if (facility == null || facility.Grid == null)
        {
            return false;
        }

        RoomLayoutCache roomCache = new RoomLayoutCache();
        if (!roomCache.TryGetRoom(facility, out RoomInstance room) || room == null)
        {
            return false;
        }

        if (room.HasDoor)
        {
            return true;
        }

        Vector2Int? position = room.Cells
            .OrderBy((cell) => cell.x)
            .ThenBy((cell) => cell.y)
            .Where((cell) => facility.Grid.GetGridCell(cell)?.CanOccupy(GridLayer.Building) == true)
            .Select((cell) => (Vector2Int?)cell)
            .FirstOrDefault();
        if (!position.HasValue)
        {
            return false;
        }

        BuildingSO doorData = ScriptableObject.CreateInstance<BuildingSO>();
        doorData.id = 980000 + UnityEngine.Random.Range(1, 9999);
        doorData.objectName = "Door";
        doorData.width = 1;
        doorData.height = 1;
        doorData.layer = GridLayer.Building;
        doorData.category = BuildingCategory.None;
        doorData.type = typeof(Door);
        doorData.unlocked = true;

        GameObject doorObject = new GameObject("QA Evolution Fixture");
        BuildableObject door = doorObject.AddComponent<BuildableObject>();
        door.SetGrid(facility.Grid);
        door.Initialization(doorData, position.Value);
        bool registered = facility.Grid.RegisterOccupant(
            door,
            doorData.Placement.Layer,
            doorData.GetGridPosList(position.Value),
            doorData.Placement.IsMovement);
        if (!registered)
        {
            UnityEngine.Object.Destroy(doorObject);
            UnityEngine.Object.Destroy(doorData);
            return false;
        }

        doorObject.transform.position = facility.Grid.GetWorldPos(position.Value);
        return true;
    }

    private static void ApplyRoomSizedDensityEvidenceForQa(
        FacilityEvolutionRecordComponent record,
        BuildableObject facility,
        FacilityEvolutionRecipeSO recipe)
    {
        if (record == null || facility == null || recipe == null)
        {
            return;
        }

        RoomLayoutCache roomCache = new RoomLayoutCache();
        if (!roomCache.TryGetRoom(facility, out RoomInstance room) || room == null)
        {
            return;
        }

        float area = Mathf.Max(1f, room.Cells.Count);
        FacilityEvolutionMetricRequirement[] requirements =
            recipe.requiredRoomMetrics ?? Array.Empty<FacilityEvolutionMetricRequirement>();
        ApplyDensityEvidenceForQa(
            record,
            requirements,
            FacilityEvolutionTerms.SeatDensity,
            FacilityEvolutionTerms.SeatCount,
            area);
        ApplyDensityEvidenceForQa(
            record,
            requirements,
            FacilityEvolutionTerms.TableDensity,
            FacilityEvolutionTerms.TableCount,
            area);
    }

    private static void ApplyDensityEvidenceForQa(
        FacilityEvolutionRecordComponent record,
        IReadOnlyList<FacilityEvolutionMetricRequirement> requirements,
        string densityMetric,
        string sourceMetric,
        float area)
    {
        FacilityEvolutionMetricRequirement[] matching = requirements
            .Where((requirement) => requirement.key == densityMetric)
            .ToArray();
        if (matching.Length == 0)
        {
            return;
        }

        bool requiresMin = matching.Any((requirement) => requirement.requireMin);
        bool requiresMax = matching.Any((requirement) => requirement.requireMax);
        float minDensity = requiresMin
            ? matching.Where((requirement) => requirement.requireMin).Max((requirement) => requirement.minValue)
            : 0f;
        float maxDensity = requiresMax
            ? matching.Where((requirement) => requirement.requireMax).Min((requirement) => requirement.maxValue)
            : float.PositiveInfinity;
        float currentDensity = record.GetRecord(null).GetMetric(sourceMetric) / area;
        float targetDensity = requiresMin ? Mathf.Max(currentDensity, minDensity) : currentDensity;
        if (requiresMin && (!requiresMax || minDensity + 0.01f <= maxDensity))
        {
            targetDensity = Mathf.Max(targetDensity, minDensity + 0.01f);
        }

        if (requiresMax)
        {
            targetDensity = Mathf.Min(targetDensity, maxDensity);
        }

        record.SetMetric(sourceMetric, Mathf.Max(0f, targetDensity * area));
    }

    private static void ApplyIdentityEvidenceForQa(
        FacilityEvolutionRecordComponent record,
        FacilityEvolutionRecipeSO recipe)
    {
        if (record == null || recipe == null)
        {
            return;
        }

        foreach (FacilityEvolutionValue weight in recipe.identityPressureWeights ?? Array.Empty<FacilityEvolutionValue>())
        {
            if (string.IsNullOrWhiteSpace(weight.key) || weight.value <= 0f)
            {
                continue;
            }

            switch (weight.key)
            {
                case FacilityEvolutionTerms.Combat:
                    record.AddToken(FacilityEvolutionTerms.MercenaryHangout, 4);
                    record.AddToken(FacilityEvolutionTerms.HighMeatConsumption, 5);
                    break;
                case FacilityEvolutionTerms.Crowd:
                    record.SetMetric(FacilityEvolutionTerms.SeatCount, 4f);
                    record.SetMetric(FacilityEvolutionTerms.TableCount, 1f);
                    record.SetMetric(FacilityEvolutionTerms.TurnoverRate, 1f);
                    record.SetMetric(FacilityEvolutionTerms.AverageWaitTime, 0f);
                    record.AddToken(FacilityEvolutionTerms.HighTurnoverService, 3);
                    break;
                case FacilityEvolutionTerms.Luxury:
                    record.SetMetric(FacilityEvolutionTerms.AverageSpend, 80f);
                    record.SetMetric(FacilityEvolutionTerms.NobleVisitorRatio, 1f);
                    record.AddToken(FacilityEvolutionTerms.NoblePatronage, 3);
                    break;
                case FacilityEvolutionTerms.Service:
                    record.SetMetric(FacilityEvolutionTerms.ServiceQuality, 100f);
                    record.SetMetric(FacilityEvolutionTerms.AverageSatisfaction, 95f);
                    record.SetMetric(FacilityEvolutionTerms.StockoutCount, 0f);
                    record.AddToken(FacilityEvolutionTerms.CleanServiceStreak, 3);
                    break;
                case FacilityEvolutionTerms.Rest:
                    record.SetMetric(FacilityEvolutionTerms.AverageStayDuration, 160f);
                    record.SetMetric(FacilityEvolutionTerms.AverageSatisfaction, 95f);
                    break;
                case FacilityEvolutionTerms.Security:
                    record.SetMetric(FacilityEvolutionTerms.ZoneSafetyScore, 100f);
                    record.SetMetric(FacilityEvolutionTerms.DistanceToGuardPost, 0f);
                    record.AddToken(FacilityEvolutionTerms.GuardRallyPoint, 3);
                    record.AddToken(FacilityEvolutionTerms.IntruderBloodied, 3);
                    break;
                case FacilityEvolutionTerms.Logistics:
                    record.SetMetric(FacilityEvolutionTerms.StockCostPerVisit, 1.5f);
                    record.SetMetric(FacilityEvolutionTerms.StockoutCount, 0f);
                    record.SetMetric(FacilityEvolutionTerms.DistanceToStorage, 0f);
                    break;
            }
        }
    }

    private static bool TryAddEvolutionQaFixture(
        BuildableObject facility,
        FacilityEvolutionRecipeSO recipe)
    {
        if (facility == null || facility.Grid == null || recipe == null)
        {
            return false;
        }

        RoomLayoutCache roomCache = new RoomLayoutCache();
        if (!roomCache.TryGetRoom(facility, out RoomInstance room) || room == null)
        {
            return false;
        }

        if (!TryFindQaFixturePosition(facility.Grid, room, out Vector2Int position, out GridLayer layer))
        {
            return false;
        }

        BuildingSO fixtureData = ScriptableObject.CreateInstance<BuildingSO>();
        fixtureData.id = 990000 + UnityEngine.Random.Range(1, 9999);
        fixtureData.objectName = "QA Evolution Fixture";
        fixtureData.width = 1;
        fixtureData.height = 1;
        fixtureData.layer = layer;
        fixtureData.category = BuildingCategory.None;
        fixtureData.type = typeof(BuildableObject);
        fixtureData.unlocked = true;
        fixtureData.Evolution = new FacilityEvolutionContributionData
        {
            contributesToRoomProfile = true,
            tags = BuildQaEvolutionTags(recipe),
            scores = BuildQaEvolutionScores(recipe),
            metrics = Array.Empty<FacilityEvolutionValue>()
        };

        GameObject fixtureObject = new GameObject("QA Evolution Fixture");
        BuildableObject fixture = fixtureObject.AddComponent<BuildableObject>();
        fixture.SetGrid(facility.Grid);
        fixture.Initialization(fixtureData, position);

        bool registered = facility.Grid.RegisterOccupant(
            fixture,
            fixtureData.Placement.Layer,
            fixtureData.GetGridPosList(position),
            fixtureData.Placement.IsMovement);
        if (!registered)
        {
            UnityEngine.Object.Destroy(fixtureObject);
            UnityEngine.Object.Destroy(fixtureData);
            return false;
        }

        fixtureObject.transform.position = facility.Grid.GetWorldPos(position);
        return true;
    }

    private static bool TryFindQaFixturePosition(
        Grid grid,
        RoomInstance room,
        out Vector2Int position,
        out GridLayer layer)
    {
        position = Vector2Int.zero;
        layer = GridLayer.Building;
        if (grid == null || room == null)
        {
            return false;
        }

        foreach (Vector2Int cellPosition in room.Cells)
        {
            GridCell cell = grid.GetGridCell(cellPosition);
            if (cell != null && cell.CanOccupy(GridLayer.Building))
            {
                position = cellPosition;
                layer = GridLayer.Building;
                return true;
            }
        }

        foreach (Vector2Int cellPosition in room.Cells)
        {
            GridCell cell = grid.GetGridCell(cellPosition);
            if (cell != null && cell.CanOccupy(GridLayer.Character))
            {
                position = cellPosition;
                layer = GridLayer.Character;
                return true;
            }
        }

        return false;
    }

    private static string[] BuildQaEvolutionTags(FacilityEvolutionRecipeSO recipe)
    {
        IEnumerable<string> scoreTags = BuildQaEvolutionScores(recipe)
            .Select((score) => score.key);
        IEnumerable<string> identityTags = recipe.identityPressureWeights?
            .Where((weight) => weight.value > 0f)
            .Select((weight) => weight.key)
            ?? Array.Empty<string>();
        IEnumerable<string> requiredTags = recipe.requiredRoomTags ?? Array.Empty<string>();

        return scoreTags
            .Concat(identityTags)
            .Concat(requiredTags)
            .Where((tag) => !string.IsNullOrWhiteSpace(tag))
            .Distinct()
            .ToArray();
    }

    private static FacilityEvolutionValue[] BuildQaEvolutionScores(FacilityEvolutionRecipeSO recipe)
    {
        if (recipe == null)
        {
            return Array.Empty<FacilityEvolutionValue>();
        }

        Dictionary<string, float> scores = new Dictionary<string, float>();
        foreach (FacilityEvolutionMetricRequirement requirement in recipe.requiredRoomScores ?? Array.Empty<FacilityEvolutionMetricRequirement>())
        {
            if (!string.IsNullOrWhiteSpace(requirement.key) && requirement.requireMin)
            {
                scores[requirement.key] = Mathf.Max(GetScore(scores, requirement.key), requirement.minValue + 5f);
            }
        }

        foreach (FacilityEvolutionValue weight in recipe.identityPressureWeights ?? Array.Empty<FacilityEvolutionValue>())
        {
            if (string.IsNullOrWhiteSpace(weight.key) || weight.value <= 0f)
            {
                continue;
            }

            scores[weight.key] = Mathf.Max(GetScore(scores, weight.key), 45f);
            if (weight.key == FacilityEvolutionTerms.Combat)
            {
                scores[FacilityEvolutionTerms.Training] = Mathf.Max(GetScore(scores, FacilityEvolutionTerms.Training), 45f);
            }
            else if (weight.key == FacilityEvolutionTerms.Rest)
            {
                scores[FacilityEvolutionTerms.Hygiene] = Mathf.Max(GetScore(scores, FacilityEvolutionTerms.Hygiene), 45f);
            }
            else if (weight.key == FacilityEvolutionTerms.Security)
            {
                scores[FacilityEvolutionTerms.Defense] = Mathf.Max(GetScore(scores, FacilityEvolutionTerms.Defense), 45f);
            }
            else if (weight.key == FacilityEvolutionTerms.Logistics)
            {
                scores[FacilityEvolutionTerms.Storage] = Mathf.Max(GetScore(scores, FacilityEvolutionTerms.Storage), 45f);
            }
        }

        return scores
            .Where((pair) => !string.IsNullOrWhiteSpace(pair.Key))
            .Select((pair) => new FacilityEvolutionValue(pair.Key, pair.Value))
            .ToArray();
    }

    private static float GetScore(Dictionary<string, float> scores, string key)
    {
        return !string.IsNullOrWhiteSpace(key) && scores.TryGetValue(key, out float value)
            ? value
            : 0f;
    }

    private static void RunCodexProbe(CodexRuntime codex, List<string> lines)
    {
        if (codex == null)
        {
            lines.Add("CODEX runtime=False");
            return;
        }

        codex.ImportReferenceData();
        int facilities = codex.GetEntries(CodexEntryCategory.Facility).Count;
        int monsters = codex.GetEntries(CodexEntryCategory.Monster).Count;
        int invasions = codex.GetEntries(CodexEntryCategory.Invasion).Count;
        lines.Add($"CODEX runtime=True; facilities={facilities}; monsters={monsters}; invasions={invasions}");
    }

    private static void RunOperatingDayProbe(
        OperatingDaySettlementRuntime settlement,
        IReadOnlyList<BuildableObject> buildings,
        List<string> lines)
    {
        if (settlement == null)
        {
            lines.Add("OPERATING-DAY runtime=False");
            return;
        }

        BuildableObject facility = buildings.FirstOrDefault((building) => building.Facility != null);
        IGameEventBus gameEventBus = ResolveFromLifetimeScope<IGameEventBus>();
        gameEventBus?.Publish(new OperatingDayStartedEvent(7));
        if (facility != null)
        {
            gameEventBus?.Publish(new FacilityRevenueEvent(null, facility, 123));
            gameEventBus?.Publish(new FacilityStockConsumedEvent(
                null,
                facility,
                StockCategory.Food,
                2));
        }

        gameEventBus?.Publish(new OperatingDayEndedEvent(7));
        OperatingDayReport report = settlement.LatestReport;
        lines.Add($"OPERATING-DAY runtime=True; report={report != null}; day={(report != null ? report.day : -1)}; revenue={(report != null ? report.totalRevenue : -1)}; facilities={(report != null ? report.facilityRevenues.Count : -1)}; events={(report != null ? report.eventLog.Count : -1)}");
    }

    private static void RunRunVariableProbe(RunVariableRuntime runVariables, List<string> lines)
    {
        if (runVariables == null)
        {
            lines.Add("RUN-VARIABLE runtime=False");
            return;
        }

        runVariables.StartRun(12345);
        RunVariableDefinition operation = RunVariableCatalog.GetByCategory(RunVariableCategory.Operation).FirstOrDefault();
        RunVariableDefinition invasion = RunVariableCatalog.GetByCategory(RunVariableCategory.Invasion).FirstOrDefault();
        ActiveRunVariable activeOperation = operation != null
            ? runVariables.ActivateOperationVariable(operation.id, 7, true)
            : null;
        RunVariableDefinition activeInvasion = invasion != null
            ? runVariables.SelectInvasionVariable(invasion.id, true)
            : null;

        lines.Add($"RUN-VARIABLE runtime=True; startVariables={runVariables.State.StartVariables != null}; operation={(activeOperation != null ? activeOperation.Definition.id.ToString() : "<none>")}; invasion={(activeInvasion != null ? activeInvasion.id.ToString() : "<none>")}; threatMultiplier={runVariables.GetThreatRiseMultiplier():0.###}");
    }

    private static void RunOffenseProbe(
        OffenseWorldMapRuntime worldMap,
        OffenseExpeditionRuntime expedition,
        OffenseRewardRuntime rewards,
        List<string> lines)
    {
        if (worldMap == null || expedition == null)
        {
            lines.Add($"OFFENSE worldMap={worldMap != null}; expedition={expedition != null}; rewards={rewards != null}");
            return;
        }

        worldMap.StartWorldMap(0);
        IReadOnlyList<OffenseTargetSnapshot> visibleTargets = worldMap.VisibleTargets;
        OffenseTargetSnapshot target = visibleTargets.FirstOrDefault();
        OffenseTargetSnapshot selectedTarget = null;
        string selectMessage = string.Empty;
        bool selected = target != null
            && worldMap.TrySelectTarget(target.id, out selectedTarget, out selectMessage);

        OffenseWorldMapPanel mapPanel = worldMap.ShowWorldMap();
        OffenseExpeditionPanel expeditionPanel = expedition.ShowExpeditionPanel();
        IReadOnlyList<CharacterActor> members = expedition.GetAvailableMemberActors();
        IEnumerable<CharacterActor> party = target != null ? members.Take(target.requiredMembers) : Array.Empty<CharacterActor>();
        OffenseExpeditionRun run = null;
        string startMessage = string.Empty;
        bool started = target != null && expedition.TryStartExpedition(
            target.id,
            party,
            out run,
            out startMessage);
        IOffenseBattleRuntime battle = ResolveFromLifetimeScope<IOffenseBattleRuntime>();
        bool commandIssued = false;
        if (started && battle?.Session?.CurrentActor?.Team == OffenseBattleTeam.Allies)
        {
            OffenseBattleCombatant enemy = battle.Session.Combatants
                .FirstOrDefault(combatant => combatant.Team == OffenseBattleTeam.Enemies && !combatant.IsDead);
            commandIssued = enemy != null && battle.TryIssuePlayerCommand(
                OffenseBattleActionType.BasicAttack,
                enemy.PersistentId,
                string.Empty,
                out _);
        }

        lines.Add($"OFFENSE worldMap=True; expedition=True; rewards={rewards != null}; visibleTargets={visibleTargets.Count}; selected={selected}; selectedTarget={(selected ? selectedTarget.title : "<none>")}; mapPanel={mapPanel != null}; expeditionPanel={expeditionPanel != null}; availableMembers={members.Count}; started={started}; battleActive={battle?.HasActiveBattle}; commandIssued={commandIssued}; rewardMoney={(rewards != null ? rewards.State.MoneyEarned : -1)}; message={CompactText(started ? startMessage : selectMessage)}");
    }

    private static void RunMetaProbe(MetaProgressionRuntime meta, IReadOnlyList<CharacterActor> actors, List<string> lines)
    {
        if (meta == null)
        {
            lines.Add("META runtime=False");
            return;
        }

        CharacterActor owner = actors.FirstOrDefault((actor) => actor.Identity != null && actor.Identity.IsOwner);
        meta.SetShowRunResultPanel(true);
        RunResultSnapshot result = meta.EndRun(owner, "QA feature-family probe");
        bool purchaseAttempted = MetaProgressionCatalog.All.Count > 0;
        bool upgradePurchased = false;
        string upgradeMessage = string.Empty;
        if (purchaseAttempted)
        {
            MetaUpgradeDefinition upgrade = MetaProgressionCatalog.All.First();
            upgradePurchased = meta.TryPurchaseUpgrade(upgrade.id, out upgradeMessage);
        }

        lines.Add($"META runtime=True; owner={(owner != null ? owner.name : "<none>")}; result={result != null}; currency={(meta.State != null ? meta.State.AvailableCurrency : -1)}; latestCurrency={(result != null ? result.legacyCurrency : -1)}; upgradeAttempted={purchaseAttempted}; upgradePurchased={upgradePurchased}; message={CompactText(upgradeMessage)}");
    }

    private static void RunUiProbe(List<string> lines)
    {
        RectTransform[] rects = UnityEngine.Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        int invalid = 0;
        int oversized = 0;
        foreach (RectTransform rect in rects)
        {
            if (rect == null) continue;

            Rect worldRect = GetWorldRect(rect);
            if (float.IsNaN(worldRect.width) || float.IsNaN(worldRect.height))
            {
                invalid++;
            }

            if (worldRect.width > Screen.width * 1.2f || worldRect.height > Screen.height * 1.2f)
            {
                oversized++;
            }
        }

        lines.Add($"UI activeRects={rects.Length}; invalid={invalid}; oversized={oversized}; activeTexts={texts.Length}; activeButtons={buttons.Length}");
    }

    private static void RunAiSceneProbe(
        DungeonSceneComponentQuery sceneQuery,
        IReadOnlyList<CharacterActor> actors,
        List<string> lines)
    {
        CharacterAiScheduler scheduler = sceneQuery.First<CharacterAiScheduler>(includeInactive: true);
        int actorCount = 0;
        int canRunCount = 0;
        int brainCount = 0;
        int directDecisionCount = 0;
        List<string> labels = new List<string>();

        foreach (CharacterActor actor in actors)
        {
            if (actor == null || !actor.gameObject.activeInHierarchy)
            {
                continue;
            }

            actorCount++;
            actor.EnsureRuntimeState();
            if (actor.Brain != null)
            {
                brainCount++;
            }

            if (!actor.CanRunAi || actor.Brain == null)
            {
                continue;
            }

            canRunCount++;
            try
            {
                actor.Brain.RequestImmediateReplan(clearFailures: true);
                bool decided = actor.Brain.DecideAction();
                if (decided)
                {
                    directDecisionCount++;
                    labels.Add($"{actor.name}:{CompactText(actor.Brain.CurrentActionDebugLabel, 40)}");
                }

                scheduler?.RequestImmediateDecisionFor(actor);
            }
            catch (Exception ex)
            {
                labels.Add($"{actor.name}:{ex.GetType().Name}");
            }
        }

        if (scheduler != null)
        {
            for (int i = 0; i < 5; i++)
            {
                scheduler.RunManualTick(0.5f);
            }
        }

        lines.Add($"AI sceneScheduler={scheduler != null}; actors={actorCount}; brains={brainCount}; canRun={canRunCount}; directDecisions={directDecisionCount}; schedulerRegistered={(scheduler != null ? scheduler.RegisteredCharacterCount : -1)}; schedulerProcessed={(scheduler != null ? scheduler.LastProcessedDecisionCount : -1)}; behaviorTicks={(scheduler != null ? scheduler.LastBehaviorTreeTickCount : -1)}; labels={CompactText(string.Join("|", labels), 180)}");
    }

    private static void RunInventoryLogisticsSceneProbe(
        IReadOnlyList<BuildableObject> buildings,
        List<string> lines)
    {
        Shop shop = buildings.OfType<Shop>().FirstOrDefault((candidate) => candidate != null)
            ?? buildings.OfType<Shop>().FirstOrDefault();
        IWarehouseFacility warehouse = buildings
            .OfType<IWarehouseFacility>()
            .FirstOrDefault((candidate) => candidate != null
                && candidate.HasWarehouseInventory
                && candidate.Inventory != null
                && candidate.Inventory.TotalStock > 0);

        if (shop == null || warehouse == null)
        {
            lines.Add($"INVENTORY sceneShop={shop != null}; sceneWarehouse={warehouse != null}");
            return;
        }

        int beforeShop = shop.CurrentStock;
        int beforeWarehouse = warehouse.Inventory.TotalStock;
        bool clearedShopStock = false;
        if (shop.MissingStock <= 0)
        {
            clearedShopStock = ClearShopStockForQa(shop);
        }

        int moved = shop.RestockFrom(new[] { warehouse }, 5, out string message);
        int afterShop = shop.CurrentStock;
        int afterWarehouse = warehouse.Inventory.TotalStock;
        lines.Add($"INVENTORY sceneShop=True; sceneWarehouse=True; clearedShopStock={clearedShopStock}; moved={moved}; shopStock={beforeShop}->{afterShop}; warehouseStock={beforeWarehouse}->{afterWarehouse}; message={CompactText(message)}");
    }

    private static void RunInvasionDefenseSceneProbe(
        DungeonSceneComponentQuery sceneQuery,
        IReadOnlyList<BuildableObject> buildings,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        InvasionThreatRuntime threat = sceneQuery.First<InvasionThreatRuntime>(includeInactive: true);
        InvasionDirectorRuntime director = sceneQuery.First<InvasionDirectorRuntime>(includeInactive: true);
        InvasionCombatReportRuntime combatReport = sceneQuery.First<InvasionCombatReportRuntime>(includeInactive: true);
        IDefenseStatusRuntimeService defenseStatus = ResolveFromLifetimeScope<IDefenseStatusRuntimeService>();
        EventAlertRuntime alerts = sceneQuery.First<EventAlertRuntime>(includeInactive: true);
        int alertBefore = alerts != null ? alerts.EventLog.Count : -1;
        int intruderBefore = director != null && director.ActiveIntruders != null ? director.ActiveIntruders.Count : -1;
        bool threatTicked = false;

        if (threat != null)
        {
            float candidateThreshold = threat.Settings != null ? threat.Settings.candidateThreshold : 100f;
            threat.AddThreat(candidateThreshold + 50f);
            threat.Tick(threat.Settings != null ? threat.Settings.maxCandidateDelaySeconds + 1f : 30f);
            threatTicked = true;
        }

        CharacterActor intruder = CreateQaCharacter(
            "QA Intruder",
            CharacterType.Intruder,
            CharacterRole.Regular,
            8,
            addWork: false,
            addShopping: false,
            tempObjects);
        CharacterActor owner = FindActiveSceneComponents<CharacterActor>()
            .FirstOrDefault((actor) => actor != null && actor.Identity != null && actor.Identity.IsOwner)
            ?? CreateQaCharacter(
                "QA Owner",
                CharacterType.NPC,
                CharacterRole.Owner,
                10,
                addWork: true,
                addShopping: false,
                tempObjects);

        InvasionThreatSnapshot snapshot = threat != null
            ? threat.LatestSnapshot
            : new InvasionThreatSnapshot(
                125f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(4f, 3f, 7f, 2f),
                0f,
                0f);

        ResolveFromLifetimeScope<IGameEventBus>()
            ?.Publish(new InvasionStartedEvent(snapshot));
        ResolveFromLifetimeScope<IGameEventBus>()
            ?.Publish(new InvasionSpawnedEvent(intruder, snapshot));

        DefenseFacility defense = FindTriggerableDefense(buildings, out DefenseTriggerTiming timing);
        DefenseActivationReport defenseReport = defense != null && defenseStatus != null
            ? defense.Trigger(intruder, timing, defenseStatus)
            : null;
        BuildableObject damagedFacility = buildings.FirstOrDefault((building) => building != null && building.Facility != null);
        if (damagedFacility != null)
        {
            ResolveFromLifetimeScope<IGameEventBus>()
                ?.Publish(new InvasionFacilityDamagedEvent(intruder, damagedFacility));
        }

        ResolveFromLifetimeScope<IGameEventBus>()
            ?.Publish(new InvasionFinalCombatStartedEvent(intruder, owner));
        ResolveFromLifetimeScope<IGameEventBus>()
            ?.Publish(new InvasionResolvedEvent(true, 1f));

        InvasionCombatReportSnapshot report = combatReport != null ? combatReport.CurrentReport : null;
        int alertAfter = alerts != null ? alerts.EventLog.Count : -1;
        int intruderAfter = director != null && director.ActiveIntruders != null ? director.ActiveIntruders.Count : -1;
        lines.Add($"INVASION-DEFENSE threat={threat != null}; threatTicked={threatTicked}; stage={(threat != null ? threat.CurrentStage.ToString() : "<none>")}; director={director != null}; intruders={intruderBefore}->{intruderAfter}; defenseStatusService={defenseStatus != null}; defense={(defense != null ? defense.name : "<none>")}; timing={timing}; defenseTriggered={defenseReport != null}; defenseDamage={(defenseReport != null ? defenseReport.TotalDamage : 0f):0.###}; combatReport={report != null}; defenseContributions={(report != null ? report.DefenseContributions.Count : -1)}; damagedFacilities={(report != null ? report.DamagedFacilities.Count : -1)}; alerts={alertBefore}->{alertAfter}");
    }

    private static void RunRecruitmentSceneProbe(
        DungeonSceneComponentQuery sceneQuery,
        IReadOnlyList<BuildableObject> buildings,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        RegularCustomerRuntime regularCustomer = sceneQuery.First<RegularCustomerRuntime>(includeInactive: true);
        BuildableObject facility = buildings.FirstOrDefault((building) => building != null && building.Facility != null);
        if (regularCustomer == null || facility == null)
        {
            lines.Add($"RECRUITMENT runtime={regularCustomer != null}; facility={facility != null}");
            return;
        }

        CharacterActor customer = CreateQaCharacter(
            "QA Recruitable Customer",
            CharacterType.Customer,
            CharacterRole.Regular,
            10,
            addWork: false,
            addShopping: true,
            tempObjects);
        customer.stats[CharacterCondition.MOOD] = 95f;

        int visits = Mathf.Max(regularCustomer.Rules.recruitCandidateVisitThreshold, regularCustomer.Rules.regularVisitThreshold);
        for (int i = 0; i < visits; i++)
        {
            regularCustomer.OnTriggerEvent(new FacilityVisitEvent(customer, facility));
        }

        string customerId = RegularCustomerService.GetCustomerId(customer);
        bool hasRecord = regularCustomer.State.TryGetRecord(customerId, out RegularCustomerRecord record);
        bool recruited = regularCustomer.TryRecruit(customerId, out RegularCustomerRecruitResult recruitResult);
        lines.Add($"RECRUITMENT runtime=True; facility={facility.name}; visits={visits}; hasRecord={hasRecord}; status={(record != null ? record.Status.ToString() : "<none>")}; candidate={(record != null && record.IsRecruitCandidate)}; recruited={recruited}; recruitedCount={regularCustomer.State.RecruitedCharacters.Count}; message={CompactText(recruitResult.Message)}");
    }

    private static void RunStaffDiscontentSceneProbe(
        DungeonSceneComponentQuery sceneQuery,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        StaffDiscontentRuntime discontent = sceneQuery.First<StaffDiscontentRuntime>(includeInactive: true);
        if (discontent == null)
        {
            lines.Add("DISCONTENT-REBELLION runtime=False");
            return;
        }

        CharacterActor guard = CreateQaCharacter(
            "QA Guard Staff",
            CharacterType.NPC,
            CharacterRole.Regular,
            12,
            addWork: true,
            addShopping: false,
            tempObjects);
        CharacterActor rebel = CreateQaCharacter(
            "QA Rebel Staff",
            CharacterType.NPC,
            CharacterRole.Regular,
            8,
            addWork: true,
            addShopping: false,
            tempObjects);
        guard.stats[CharacterCondition.MOOD] = 90f;
        rebel.stats[CharacterCondition.MOOD] = 5f;

        StaffDiscontentRecord record = discontent.ProcessStaff(rebel, out StaffDiscontentOutcome outcome);
        int autoSuppressAssigned = discontent.DispatchAutoSuppress(rebel);
        bool isolated = discontent.TryIsolateRebel(rebel, guard, out StaffRebellionResponseResult isolateResult);
        bool suppressed = discontent.ResolveSuppressedRebel(rebel, guard);

        lines.Add($"DISCONTENT-REBELLION runtime=True; outcome={outcome}; stage={(record != null ? record.Stage.ToString() : "<none>")}; rebellionTarget={discontent.IsRebellionTarget(rebel)}; autoSuppressAssigned={autoSuppressAssigned}; isolated={isolated}; suppressed={suppressed}; isolateMessage={CompactText(isolateResult.Message)}");
    }

    private static void RunOffenseCompletionSceneProbe(
        DungeonSceneComponentQuery sceneQuery,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        OffenseWorldMapRuntime worldMap = sceneQuery.First<OffenseWorldMapRuntime>(includeInactive: true);
        OffenseExpeditionRuntime expedition = sceneQuery.First<OffenseExpeditionRuntime>(includeInactive: true);
        OffenseRewardRuntime rewards = sceneQuery.First<OffenseRewardRuntime>(includeInactive: true);
        if (worldMap == null || expedition == null)
        {
            lines.Add($"OFFENSE-COMPLETE worldMap={worldMap != null}; expedition={expedition != null}; rewards={rewards != null}");
            return;
        }

        worldMap.StartWorldMap(0);
        OffenseTargetSnapshot target = worldMap.VisibleTargets.FirstOrDefault();
        if (target == null)
        {
            lines.Add($"OFFENSE-COMPLETE worldMap=True; expedition=True; rewards={rewards != null}; visibleTargets=0");
            return;
        }

        worldMap.TrySelectTarget(target.id, out _, out string selectMessage);
        int memberCount = Mathf.Max(1, target.requiredMembers);
        List<CharacterActor> members = new List<CharacterActor>();
        for (int i = 0; i < memberCount; i++)
        {
            members.Add(CreateQaCharacter(
                $"QA Expedition Staff {i + 1}",
                CharacterType.NPC,
                CharacterRole.Regular,
                30,
                addWork: true,
                addShopping: false,
                tempObjects));
        }

        int rewardBefore = rewards != null ? rewards.State.MoneyEarned : -1;
        bool started = expedition.TryStartExpedition(target.id, members, out OffenseExpeditionRun run, out string startMessage);
        IOffenseBattleRuntime battle = ResolveFromLifetimeScope<IOffenseBattleRuntime>();
        bool completed = started && DriveBattleToCompletion(battle, 100);
        OffenseExpeditionResult result = expedition.ResultHistory
            .FirstOrDefault(candidate => candidate.expeditionId == run?.ExpeditionId);
        int rewardAfter = rewards != null ? rewards.State.MoneyEarned : -1;

        lines.Add($"OFFENSE-COMPLETE target={target.title}; requiredMembers={target.requiredMembers}; tempMembers={members.Count}; started={started}; completed={completed}; success={(completed && result != null && result.success)}; rewardMoney={rewardBefore}->{rewardAfter}; rewardSummaries={(completed && result != null ? result.rewardSummaries.Count : -1)}; message={CompactText(started ? startMessage : selectMessage)}");
    }

    private static bool DriveBattleToCompletion(IOffenseBattleRuntime battle, int maximumCommands)
    {
        int commands = 0;
        while (battle != null && battle.HasActiveBattle && commands++ < maximumCommands)
        {
            OffenseBattleSession session = battle.Session;
            if (session?.CurrentActor?.Team != OffenseBattleTeam.Allies)
            {
                battle.AdvanceToPlayerDecision();
                continue;
            }

            OffenseBattleCombatant enemy = session.Combatants
                .FirstOrDefault(combatant => combatant.Team == OffenseBattleTeam.Enemies && !combatant.IsDead);
            if (enemy == null || !battle.TryIssuePlayerCommand(
                    OffenseBattleActionType.BasicAttack,
                    enemy.PersistentId,
                    string.Empty,
                    out _))
            {
                break;
            }
        }

        return battle?.Session?.IsComplete == true;
    }

    private static void RunEvolutionSupplementProbe(
        DungeonSceneComponentQuery sceneQuery,
        IReadOnlyList<BuildableObject> buildings,
        List<string> lines)
    {
        FacilityEvolutionRuntime evolution = sceneQuery.First<FacilityEvolutionRuntime>(includeInactive: true);
        int sceneCandidateCount = 0;
        int approvedCount = 0;
        foreach (BuildableObject building in buildings)
        {
            IReadOnlyList<FacilityEvolutionCandidate> candidates = evolution != null
                ? evolution.GetCandidates(building, includeRejected: true)
                : Array.Empty<FacilityEvolutionCandidate>();
            sceneCandidateCount += candidates.Count;
            approvedCount += candidates.Count((candidate) => candidate.Approved);
        }

        bool debugWorldPassed = false;
        string debugWorldError = string.Empty;
        try
        {
            debugWorldPassed = FacilityEvolutionDebugScenarios.RunAll(false);
        }
        catch (Exception ex)
        {
            debugWorldError = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add($"EVOLUTION-SUPPLEMENT runtime={evolution != null}; sceneCandidates={sceneCandidateCount}; sceneApproved={approvedCount}; debugWorldPassed={debugWorldPassed}; debugWorldError={CompactText(debugWorldError)}");
    }

    private static void RunDebugWorldSupplementProbe(List<string> lines)
    {
        lines.Add("DEBUG-WORLD supplements=skipped-in-this-probe; reason=legacy Editor fixtures create uninjected temporary objects in PlayMode and must be run/fixed separately from direct SampleScene evidence");
    }

    private static void RunInjectedCustomerAiProbe(
        DungeonSceneComponentQuery sceneQuery,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        CharacterAiScheduler scheduler = sceneQuery.First<CharacterAiScheduler>(includeInactive: true);
        CharacterActor customer = CreateQaCharacter(
            "QA Customer AI",
            CharacterType.Customer,
            CharacterRole.Regular,
            70,
            addWork: false,
            addShopping: true,
            tempObjects);
        SetCondition(customer, CharacterCondition.HUNGER, 20f);
        SetCondition(customer, CharacterCondition.FUN, 35f);
        SetCondition(customer, CharacterCondition.MOOD, 80f);

        bool decided = false;
        bool scheduledDecided = false;
        bool directFallback = false;
        int maxSchedulerProcessed = 0;
        int maxBehaviorTicks = 0;
        int treeTicksBefore = -1;
        int treeTicksAfter = -1;
        string treeBranch = string.Empty;
        string treeTask = string.Empty;
        string treeStatus = string.Empty;
        string exception = string.Empty;
        try
        {
            customer.Brain.RequestImmediateReplan(clearFailures: true);
            scheduler?.RequestImmediateDecisionFor(customer);
            BehaviorTree behaviorTree = customer.GetComponent<BehaviorTree>();
            treeTicksBefore = behaviorTree != null ? behaviorTree.DungeonStoryTickCount : -1;
            for (int i = 0; i < 5; i++)
            {
                scheduler?.RunManualTick(0.5f);
                maxSchedulerProcessed = Mathf.Max(
                    maxSchedulerProcessed,
                    scheduler != null ? scheduler.LastProcessedDecisionCount : 0);
                maxBehaviorTicks = Mathf.Max(
                    maxBehaviorTicks,
                    scheduler != null ? scheduler.LastBehaviorTreeTickCount : 0);
                scheduledDecided = scheduledDecided
                    || (customer.Brain != null && customer.Brain.bestAction != null);
            }

            behaviorTree = customer.GetComponent<BehaviorTree>();
            if (behaviorTree != null)
            {
                treeTicksAfter = behaviorTree.DungeonStoryTickCount;
                treeBranch = behaviorTree.DungeonStoryBranch;
                treeTask = behaviorTree.DungeonStoryTask;
                treeStatus = behaviorTree.DungeonStoryStatus;
            }

            if (scheduledDecided)
            {
                decided = true;
            }
            else
            {
                directFallback = true;
                decided = customer.Brain.DecideAction();
            }
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add($"CUSTOMER-AI injected=True; canRun={customer.CanRunAi}; scheduler={scheduler != null}; schedulerRegistered={(scheduler != null ? scheduler.RegisteredCharacterCount : -1)}; schedulerProcessedLast={(scheduler != null ? scheduler.LastProcessedDecisionCount : -1)}; schedulerProcessedMax={maxSchedulerProcessed}; behaviorTicksLast={(scheduler != null ? scheduler.LastBehaviorTreeTickCount : -1)}; behaviorTicksMax={maxBehaviorTicks}; treeTicks={treeTicksBefore}->{treeTicksAfter}; scheduledDecided={scheduledDecided}; directFallback={directFallback}; decided={decided}; action={CompactText(customer.Brain != null ? customer.Brain.CurrentActionDebugLabel : string.Empty)}; destination={CompactText(customer.Brain != null && customer.Brain.bestAction != null && customer.Brain.bestAction.ReservedDestination != null ? customer.Brain.bestAction.ReservedDestination.name : string.Empty)}; btBranch={CompactText(treeBranch)}; btTask={CompactText(treeTask)}; btStatus={CompactText(treeStatus)}; exception={CompactText(exception)}");
    }

    private static void RunInjectedStaffDutyPriorityProbe(
        DungeonSceneComponentQuery sceneQuery,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        Grid grid = ResolveGrid(sceneQuery);
        CharacterActor staff = CreateQaCharacter(
            "QA Duty Staff",
            CharacterType.NPC,
            CharacterRole.Regular,
            75,
            addWork: true,
            addShopping: true,
            tempObjects);

        bool hasWork = staff.TryGetAbility(out AbilityWork work);
        bool hasShopping = staff.TryGetAbility(out AbilityShopping shopping);
        bool shouldOffDuty = false;
        bool canStartLowCondition = false;
        bool offDutyAfterLowCondition = false;
        bool offDutyVisitorDuringOffDuty = false;
        bool agedOffDutyForQa = false;
        bool returnReadyAfterRecover = false;
        bool canStartAfterRecover = false;
        bool returnedToWorkAfterRecover = false;
        bool prioritySet = false;
        string priorityMessage = string.Empty;
        string exception = string.Empty;
        BuildableObject target = null;
        WorkTypeId targetWorkType = default;

        try
        {
            if (hasWork)
            {
                SetCondition(staff, CharacterCondition.SLEEP, 5f);
                SetCondition(staff, CharacterCondition.MOOD, 10f);
                SetCondition(staff, CharacterCondition.EXCRETION, 10f);
                SetCondition(staff, CharacterCondition.HYGIENE, 10f);
                shouldOffDuty = work.ShouldTakeOffDuty();
                canStartLowCondition = work.CanStartWorkAction();
                offDutyAfterLowCondition = work.IsOffDuty;
                offDutyVisitorDuringOffDuty = shopping != null && shopping.IsOffDutyStaffVisitor();

                agedOffDutyForQa = AgeOffDutyForQa(work);
                work.RecoverOffDuty(100f, 100f, 100f, 100f, 100f, 100f);
                returnReadyAfterRecover = work.ShouldReturnToWork();
                canStartAfterRecover = work.CanStartWorkAction();
                returnedToWorkAfterRecover = work.CurrentDutyState == AbilityWork.DutyState.OnDuty;

                GridPathSearchResult search = grid != null ? grid.SearchPath(staff.GetNowXY()) : null;
                if (work.TryGetBestAnyWorkCandidate(search, out WorkTargetCandidate candidate)
                    && candidate.IsValid)
                {
                    target = candidate.Building;
                    targetWorkType = candidate.WorkTypeId;
                    prioritySet = work.TrySetPriorityWorkTarget(target, targetWorkType, search, out priorityMessage);
                }
                else
                {
                    priorityMessage = candidate.FailureReason;
                }
            }
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add($"STAFF-DUTY-PRIORITY hasWork={hasWork}; hasShopping={hasShopping}; shouldOffDuty={shouldOffDuty}; canStartLowCondition={canStartLowCondition}; offDutyAfterLowCondition={offDutyAfterLowCondition}; offDutyVisitorDuringOffDuty={offDutyVisitorDuringOffDuty}; agedOffDutyForQa={agedOffDutyForQa}; returnReadyAfterRecover={returnReadyAfterRecover}; canStartAfterRecover={canStartAfterRecover}; returnedToWorkAfterRecover={returnedToWorkAfterRecover}; prioritySet={prioritySet}; priorityTarget={(target != null ? target.name : "<none>")}; priorityType={targetWorkType}; assigned={(work != null && work.assignedShop != null ? work.assignedShop.name : "<none>")}; message={CompactText(priorityMessage)}; exception={CompactText(exception)}");
    }

    private static void RunInjectedOwnerPriorityProbe(
        DungeonSceneComponentQuery sceneQuery,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        Grid grid = ResolveGrid(sceneQuery);
        CharacterActor owner = CreateQaCharacter(
            "QA Owner",
            CharacterType.NPC,
            CharacterRole.Owner,
            90,
            addWork: true,
            addShopping: false,
            tempObjects);

        bool hasWork = owner.TryGetAbility(out AbilityWork work);
        bool decided = false;
        bool prioritySet = false;
        string priorityMessage = string.Empty;
        string exception = string.Empty;
        BuildableObject target = null;
        WorkTypeId targetWorkType = default;

        try
        {
            owner.Brain.RequestImmediateReplan(clearFailures: true);
            decided = owner.Brain.DecideAction();

            if (hasWork)
            {
                GridPathSearchResult search = grid != null ? grid.SearchPath(owner.GetNowXY()) : null;
                if (work.TryGetBestAnyWorkCandidate(search, out WorkTargetCandidate candidate)
                    && candidate.IsValid)
                {
                    target = candidate.Building;
                    targetWorkType = candidate.WorkTypeId;
                    prioritySet = work.TrySetPriorityWorkTarget(target, targetWorkType, search, out priorityMessage);
                }
                else
                {
                    priorityMessage = candidate.FailureReason;
                }
            }
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add($"OWNER-PRIORITY isOwner={owner.IsOwner}; hasWork={hasWork}; decided={decided}; action={CompactText(owner.Brain != null ? owner.Brain.CurrentActionDebugLabel : string.Empty)}; prioritySet={prioritySet}; priorityTarget={(target != null ? target.name : "<none>")}; priorityType={targetWorkType}; message={CompactText(priorityMessage)}; exception={CompactText(exception)}");
    }

    private static void RunInjectedLocalLlmProbe(
        DungeonSceneComponentQuery sceneQuery,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        LocalLlmRequestQueue queue = sceneQuery.First<LocalLlmRequestQueue>(includeInactive: true);
        if (queue == null)
        {
            GameObject queueObject = new GameObject("QA Local LLM Queue");
            tempObjects.Add(queueObject);
            queue = queueObject.AddComponent<LocalLlmRequestQueue>();
            queue.Construct(new UnityUiClock());
        }

        queue.ClearForDebug();
        queue.SetWarningLogsSuppressedForDebug(true);

        AiDirectorRuntime director = sceneQuery.First<AiDirectorRuntime>(includeInactive: true);
        if (director == null)
        {
            GameObject directorObject = new GameObject("QA AI Director");
            tempObjects.Add(directorObject);
            director = directorObject.AddComponent<AiDirectorRuntime>();
            InjectGameObjectFromLifetimeScope(directorObject);
        }

        CharacterActor actor = CreateQaCharacter(
            "QA LLM Customer",
            CharacterType.Customer,
            CharacterRole.Regular,
            80,
            addWork: false,
            addShopping: true,
            tempObjects);
        SetCondition(actor, CharacterCondition.MOOD, 35f);
        SetCondition(actor, CharacterCondition.FUN, 25f);

        bool personaRequested = false;
        bool personaApplied = false;
        bool moodRequested = false;
        bool moodApplied = false;
        bool macroRequested = false;
        bool macroApplied = false;
        bool bubbleApplied = false;
        string exception = string.Empty;

        try
        {
            personaRequested = actor.PersonaRuntime != null
                && actor.PersonaRuntime.RequestPersonaIfNeeded(logIfMissingQueue: false);
            InvokePrivateLlmResult(
                actor.PersonaRuntime,
                "OnPersonaResult",
                "{\"traitName\":\"QA Curious\",\"flavorText\":\"Checks every corner.\",\"selfCareMultiplier\":1.1,\"curiosityMultiplier\":1.4,\"shoppingMultiplier\":1.2,\"patienceMultiplier\":0.9,\"hungerCurveMultiplier\":1.0,\"funCurveMultiplier\":1.1,\"moodCurveMultiplier\":1.0,\"preferredFacilityTags\":[\"Meal\",\"Rest\"]}");
            personaApplied = actor.PersonaRuntime != null && actor.PersonaRuntime.HasGeneratedPersona;

            moodRequested = director != null && director.RequestMoodImpulse(actor);
            InvokePrivateLlmResult(
                director,
                "OnMoodImpulseResult",
                actor,
                "{\"moodImpulse\":\"Wait\",\"strength\":0.65,\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"reason\":\"low mood wants a pause\",\"validSeconds\":30}");
            moodApplied = actor.Blackboard != null && actor.Blackboard.HasActiveMoodImpulse();

            macroRequested = director != null && director.RequestMacroGoal(actor);
            InvokePrivateLlmResult(
                director,
                "OnMacroGoalResult",
                actor,
                "{\"macroGoal\":\"SeekFun\",\"reason\":\"needs a better mood\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"validSeconds\":30}");
            macroApplied = actor.Blackboard != null && actor.Blackboard.HasActiveMacroGoal();

            if (actor.DialogueRuntime != null)
            {
                InvokePrivateLlmResult(
                    actor.DialogueRuntime,
                    "OnBubbleResult",
                    "{\"line\":\"I need a short break.\"}",
                    "original line");
                bubbleApplied = !string.IsNullOrWhiteSpace(actor.DialogueRuntime.LastGeneratedBubbleLine)
                    && !string.IsNullOrWhiteSpace(actor.DialogueRuntime.LastBubbleLine);
            }
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            queue.AbortAllForDebug();
            queue.SetWarningLogsSuppressedForDebug(false);
        }

        lines.Add($"LOCAL-LLM-FLOW queue=True; personaRequested={personaRequested}; personaApplied={personaApplied}; trait={CompactText(actor.PersonaRuntime != null ? actor.PersonaRuntime.Persona.traitName : string.Empty)}; moodRequested={moodRequested}; moodApplied={moodApplied}; moodType={(actor.Blackboard != null && actor.Blackboard.ActiveMoodImpulse != null ? actor.Blackboard.ActiveMoodImpulse.type.ToString() : "<none>")}; macroRequested={macroRequested}; macroApplied={macroApplied}; macroType={(actor.Blackboard != null && actor.Blackboard.ActiveMacroGoal != null ? actor.Blackboard.ActiveMacroGoal.type.ToString() : "<none>")}; bubbleApplied={bubbleApplied}; bubble={CompactText(actor.DialogueRuntime != null ? actor.DialogueRuntime.LastGeneratedBubbleLine : string.Empty)}; exception={CompactText(exception)}");
    }

    private static void RunInjectedCharacterVisualProbe(
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        CharacterActor actor = CreateQaCharacter(
            "QA Visual Feedback",
            CharacterType.NPC,
            CharacterRole.Regular,
            80,
            addWork: false,
            addShopping: false,
            tempObjects);

        bool hasVisual = actor.VisualRenderer != null || actor.VisualRoot != null;
        CharacterFeedbackBubble bubble = actor.GetComponent<CharacterFeedbackBubble>();
        bool bubblePresent = bubble != null;
        CharacterFeedbackState shownState = CharacterFeedbackState.None;
        CharacterFeedbackState persistentState = CharacterFeedbackState.None;
        string exception = string.Empty;

        try
        {
            if (bubble != null)
            {
                bubble.Show(CharacterFeedbackState.Joy);
                shownState = bubble.CurrentState;
                SetCondition(actor, CharacterCondition.SLEEP, 5f);
                persistentState = bubble.EvaluatePersistentState();
            }
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add($"CHARACTER-VISUAL-FEEDBACK hasVisual={hasVisual}; bubblePresent={bubblePresent}; shownState={shownState}; persistentState={persistentState}; renderer={(actor.VisualRenderer != null ? actor.VisualRenderer.name : "<none>")}; exception={CompactText(exception)}");
    }

    private static void RunGridPlacementAndDefenseProbe(
        DungeonSceneComponentQuery sceneQuery,
        Grid grid,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        BuildingSO defenseData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset");
        IDefenseStatusRuntimeService defenseStatus = ResolveFromLifetimeScope<IDefenseStatusRuntimeService>();
        GridBuildingPlacementService placementService = CreateQaPlacementService(grid, tempObjects);

        bool foundPosition = false;
        bool placed = false;
        bool destroyed = false;
        string placeMessage = string.Empty;
        string destroyMessage = string.Empty;
        Vector2Int position = Vector2Int.zero;
        DefenseFacility defense = null;
        DefenseActivationReport directReport = null;
        List<DefenseActivationReport> resolverReports = new List<DefenseActivationReport>();
        CharacterActor intruder = null;
        float healthBefore = -1f;
        float healthAfter = -1f;

        try
        {
            foundPosition = placementService != null
                && defenseData != null
                && TryFindPlaceablePosition(placementService, defenseData, grid, out position, out placeMessage);
            placed = foundPosition && placementService.TryPlaceBuilding(defenseData, position, out placeMessage);
            defense = placed ? FindPlacedBuilding<DefenseFacility>(grid, defenseData, position) : null;

            if (defense != null && defenseStatus != null)
            {
                intruder = CreateQaCharacter(
                    "QA Defense Intruder",
                    CharacterType.Intruder,
                    CharacterRole.Regular,
                    120,
                    addWork: false,
                    addShopping: false,
                    tempObjects);
                intruder.transform.position = grid.GetWorldPos(defense.buildPoses != null && defense.buildPoses.Count > 0
                    ? defense.buildPoses[0]
                    : position);

                healthBefore = intruder.CurrentHealth;
                if (defense.buildPoses != null && defense.buildPoses.Count > 0)
                {
                    resolverReports = DefenseFacilityResolver.TriggerAt(
                        grid,
                        intruder,
                        defense.buildPoses[0],
                        DefenseTriggerTiming.OnEnter,
                        defenseStatus);
                }

                directReport = resolverReports.Count > 0 ? resolverReports[0] : null;
                if (directReport == null)
                {
                    directReport = defense.Trigger(intruder, DefenseTriggerTiming.OnEnter, defenseStatus);
                }

                healthAfter = intruder.CurrentHealth;
            }
        }
        catch (Exception ex)
        {
            placeMessage = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            if (placementService != null && defense != null)
            {
                destroyed = placementService.TryDestroyBuilding(defense, out _, out destroyMessage);
            }
        }

        lines.Add($"GRID-BUILD-DEFENSE grid={grid != null}; defenseAsset={defenseData != null}; placementService={placementService != null}; foundPosition={foundPosition}; position={position}; placed={placed}; placedType={(defense != null ? defense.GetType().Name : "<none>")}; defenseTriggered={directReport != null}; resolverTriggers={resolverReports.Count}; damage={(directReport != null ? directReport.TotalDamage : 0f):0.###}; health={healthBefore:0.###}->{healthAfter:0.###}; destroyed={destroyed}; defenseStatus={defenseStatus != null}; message={CompactText(!string.IsNullOrWhiteSpace(destroyMessage) ? destroyMessage : placeMessage)}");
    }

    private static void RunInvasionFinalCombatProbe(
        DungeonSceneComponentQuery sceneQuery,
        Grid grid,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        IInvasionIntruderContext context = ResolveFromLifetimeScope<IInvasionIntruderContext>();
        IDefenseStatusRuntimeService defenseStatus = ResolveFromLifetimeScope<IDefenseStatusRuntimeService>();
        CharacterSO intruderData = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset");
        CharacterActor intruder = CreateQaCharacter(
            "QA Breakthrough Intruder",
            CharacterType.Intruder,
            CharacterRole.Regular,
            100,
            addWork: false,
            addShopping: false,
            tempObjects);
        CharacterActor owner = FindActiveSceneComponents<CharacterActor>()
            .FirstOrDefault((actor) => actor != null && actor.IsOwner)
            ?? CreateQaCharacter(
                "QA Final Combat Owner",
                CharacterType.NPC,
                CharacterRole.Owner,
                100,
                addWork: true,
                addShopping: false,
                tempObjects);

        bool directPath = false;
        int pathCount = -1;
        bool pathReachedOwner = false;
        bool runtimeInitialized = false;
        bool finalCombatApplied = false;
        float ownerHealthBefore = owner != null ? owner.CurrentHealth : -1f;
        float ownerHealthAfter = ownerHealthBefore;
        string exception = string.Empty;

        try
        {
            if (grid != null && TryFindReachablePair(grid, out Vector2Int start, out Vector2Int ownerPos))
            {
                intruder.transform.position = grid.GetWorldPos(start);
                owner.transform.position = grid.GetWorldPos(ownerPos);
                Queue<GridMoveStep> path = InvasionIntruderPlanner.GetNextPath(
                    grid,
                    start,
                    ownerPos,
                    1f,
                    intruder.PathSearchBroker,
                    new DungeonStory.Foundation.RandomStreamProvider(301)
                        .Get("qa-invasion-path"),
                    out directPath);
                pathCount = path.Count;
                while (path.Count > 0)
                {
                    GridMoveStep step = path.Dequeue();
                    intruder.transform.position = grid.GetWorldPos(step.To);
                }

                pathReachedOwner = InvasionIntruderPlanner.IsAtOwner(grid, intruder, owner);
            }

            InvasionIntruderRuntime runtime = intruder.gameObject.AddComponent<InvasionIntruderRuntime>();
            if (context != null && defenseStatus != null)
            {
                runtime.Initialize(
                    context,
                    defenseStatus,
                    new DungeonStory.Foundation.UnityGameClock(),
                    new DungeonStory.Foundation.RandomStreamProvider(307),
                    ResolveFromLifetimeScope<IGameEventBus>()
                        ?? CharacterAiEditorTestDependencies.GameEvents);
                runtimeInitialized = true;
                runtime.Begin(
                    intruderData != null ? intruderData : intruder.Identity.Data,
                    new InvasionThreatSnapshot(
                        125f,
                        InvasionThreatStage.Candidate,
                        new InvasionThreatFactors(4f, 3f, 7f, 2f),
                        0f,
                        0f),
                    new InvasionIntruderSettings
                    {
                        secondsToFullFocus = 0.1f,
                        repathIntervalSeconds = 0.1f,
                        facilityDamageIntervalSeconds = 999f,
                        finalCombatDamage = 10f,
                        finalCombatWindupSeconds = 0f
                    },
                    intruder.transform.position,
                    intruder.transform.position,
                    grid != null ? grid.GetXY(intruder.transform.position) : Vector2Int.zero);
            }

            ownerHealthBefore = owner != null ? owner.CurrentHealth : -1f;
            runtime.ApplyFinalCombat(owner);
            ownerHealthAfter = owner != null ? owner.CurrentHealth : -1f;
            finalCombatApplied = ownerHealthAfter < ownerHealthBefore;
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        lines.Add($"INVASION-FINAL-COMBAT grid={grid != null}; context={context != null}; defenseStatus={defenseStatus != null}; intruderData={intruderData != null}; directPath={directPath}; pathSteps={pathCount}; pathReachedOwner={pathReachedOwner}; runtimeInitialized={runtimeInitialized}; finalCombatApplied={finalCombatApplied}; ownerHealth={ownerHealthBefore:0.###}->{ownerHealthAfter:0.###}; exception={CompactText(exception)}");
    }

    private static IEnumerator RunTimedInvasionCoroutine(
        DungeonSceneComponentQuery sceneQuery,
        Grid grid,
        List<string> lines)
    {
        InvasionDirectorRuntime director = sceneQuery.First<InvasionDirectorRuntime>(includeInactive: true);
        CharacterActor owner = FindActiveSceneComponents<CharacterActor>()
            .FirstOrDefault((actor) => actor != null && actor.IsOwner);
        int activeBefore = director != null && director.ActiveIntruders != null
            ? director.ActiveIntruders.Count
            : -1;
        float ownerHealthBefore = owner != null ? owner.CurrentHealth : -1f;
        float ownerHealthAfter = ownerHealthBefore;
        bool settingsOverridden = OverrideInvasionDirectorSettingsForQa(director);
        bool spawned = false;
        bool assigned = false;
        bool finalCombatApplied = false;
        bool finishedOrDestroyed = false;
        string state = "<none>";
        string exception = string.Empty;

        try
        {
            if (director != null)
            {
                spawned = director.TrySpawnIntruder(
                    new InvasionThreatSnapshot(
                        180f,
                        InvasionThreatStage.Candidate,
                        new InvasionThreatFactors(8f, 8f, 4f, 2f),
                        0f,
                        0f),
                    out CharacterActor intruder);
                assigned = intruder != null;
            }
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        InvasionIntruderRuntime runtime = director != null && director.ActiveIntruders != null
            ? director.ActiveIntruders.LastOrDefault()
            : null;

        float deadline = Time.realtimeSinceStartup + 8f;
        while (string.IsNullOrEmpty(exception) && Time.realtimeSinceStartup < deadline)
        {
            ownerHealthAfter = owner != null ? owner.CurrentHealth : -1f;
            finalCombatApplied = ownerHealthBefore >= 0f && ownerHealthAfter < ownerHealthBefore;
            if (runtime == null || runtime.Equals(null))
            {
                finishedOrDestroyed = spawned;
                break;
            }

            state = runtime.State.ToString();
            if (runtime.State == InvasionIntruderState.Finished || finalCombatApplied)
            {
                finishedOrDestroyed = true;
                break;
            }

            yield return null;
        }

        ownerHealthAfter = owner != null ? owner.CurrentHealth : -1f;
        finalCombatApplied = ownerHealthBefore >= 0f && ownerHealthAfter < ownerHealthBefore;
        if (runtime == null || runtime.Equals(null))
        {
            state = "<destroyed>";
            finishedOrDestroyed = spawned;
        }
        else
        {
            state = runtime.State.ToString();
        }

        int activeAfter = director != null && director.ActiveIntruders != null
            ? director.ActiveIntruders.Count
            : -1;
        lines.Add($"INVASION-TIMED-COROUTINE grid={grid != null}; director={director != null}; owner={owner != null}; settingsOverridden={settingsOverridden}; spawned={spawned}; assigned={assigned}; active={activeBefore}->{activeAfter}; state={state}; finalCombatApplied={finalCombatApplied}; finishedOrDestroyed={finishedOrDestroyed}; ownerHealth={ownerHealthBefore:0.###}->{ownerHealthAfter:0.###}; exception={CompactText(exception)}");
    }

    private static bool OverrideInvasionDirectorSettingsForQa(InvasionDirectorRuntime director)
    {
        if (director == null)
        {
            return false;
        }

        FieldInfo settingsField = typeof(InvasionDirectorRuntime).GetField(
            "intruderSettings",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (settingsField == null)
        {
            return false;
        }

        settingsField.SetValue(
            director,
            new InvasionIntruderSettings
            {
                secondsToFullFocus = 0.1f,
                repathIntervalSeconds = 0.05f,
                facilityDamageIntervalSeconds = 999f,
                finalCombatDamage = 10f,
                finalCombatWindupSeconds = 0f
            });
        return true;
    }

    private static IEnumerator RunLocalLlmEndpointCoroutine(
        DungeonSceneComponentQuery sceneQuery,
        List<string> lines)
    {
        LocalLlmRequestQueue queue = sceneQuery.First<LocalLlmRequestQueue>(includeInactive: true);
        if (queue == null)
        {
            lines.Add("LOCAL-LLM-ENDPOINT queue=False");
            yield break;
        }

        queue.AbortAllForDebug();
        queue.SetWarningLogsSuppressedForDebug(true);
        queue.ConfigureTimeoutsForDebug(
            personaSeconds: 20f,
            macroGoalSeconds: 20f,
            moodImpulseSeconds: 20f,
            socialRumorSeconds: 20f,
            facilityEvolutionSeconds: 20f,
            bubbleSeconds: 20f);

        bool completed = false;
        LocalLlmResult callbackResult = default;
        bool accepted = queue.GeneratePersonaAsync(
            "Return exactly one compact JSON object and no markdown: {\"qa\":\"pong\"}.",
            (result) =>
            {
                callbackResult = result;
                completed = true;
            });

        float deadline = Time.realtimeSinceStartup + 30f;
        while (accepted && !completed && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        lines.Add($"LOCAL-LLM-ENDPOINT queue=True; configured={queue.HasConfiguredEndpoint}; accepted={accepted}; completed={completed}; status={(completed ? callbackResult.Status.ToString() : "<pending>")}; success={(completed && callbackResult.IsSuccess)}; content={CompactText(completed ? callbackResult.Content : string.Empty, 120)}; error={CompactText(completed ? callbackResult.Error : string.Empty, 120)}; queued={queue.QueuedCount}; running={queue.RunningCount}; timeouts={queue.TimeoutCount}; dropped={queue.DroppedEphemeralRequestCount}");
        queue.SetWarningLogsSuppressedForDebug(false);
    }

    private static void RunVisibleBubbleProbe(
        DungeonSceneComponentQuery sceneQuery,
        Grid grid,
        List<UnityEngine.Object> tempObjects,
        List<string> lines)
    {
        CharacterActor actor = CreateQaCharacter(
            "QA Visible Bubble Owner",
            CharacterType.NPC,
            CharacterRole.Owner,
            100,
            addWork: false,
            addShopping: false,
            tempObjects);

        if (grid != null && TryFindWalkablePosition(grid, out Vector2Int position))
        {
            actor.transform.position = grid.GetWorldPos(position);
        }
        else
        {
            Camera camera = sceneQuery.First<Camera>(includeInactive: true) ?? Camera.main;
            if (camera != null)
            {
                actor.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 0f);
            }
        }

        CharacterFeedbackBubble feedback = actor.GetComponent<CharacterFeedbackBubble>();
        CharacterDialogueRuntime dialogue = actor.DialogueRuntime;
        bool schedulerAllowsFeedback = false;
        bool feedbackShown = false;
        bool dialogueShown = false;
        int textCountBefore = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Count((text) => text != null && text.gameObject.activeInHierarchy);
        string exception = string.Empty;

        try
        {
            ICharacterAiSchedulingService schedulingService = ResolveFromLifetimeScope<ICharacterAiSchedulingService>();
            schedulerAllowsFeedback = schedulingService == null || schedulingService.ShouldShowCharacterFeedback(actor);
            feedback?.Show(CharacterFeedbackState.Joy);
            dialogue?.ShowLine("QA visible line");
            feedbackShown = feedback != null && feedback.CurrentState == CharacterFeedbackState.Joy;
            dialogueShown = dialogue != null && dialogue.LastBubbleLine == "QA visible line";
        }
        catch (Exception ex)
        {
            exception = $"{ex.GetType().Name}: {ex.Message}";
        }

        int textCountAfter = UnityEngine.Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Count((text) => text != null && text.gameObject.activeInHierarchy);

        lines.Add($"VISIBLE-BUBBLE schedulerAllows={schedulerAllowsFeedback}; feedbackPresent={feedback != null}; feedbackShown={feedbackShown}; dialoguePresent={dialogue != null}; dialogueShown={dialogueShown}; lastDialogue={CompactText(dialogue != null ? dialogue.LastBubbleLine : string.Empty)}; activeTexts={textCountBefore}->{textCountAfter}; exception={CompactText(exception)}");
    }

    private static CharacterActor CreateQaCharacter(
        string name,
        CharacterType type,
        CharacterRole role,
        int statValue,
        bool addWork,
        bool addShopping,
        List<UnityEngine.Object> tempObjects)
    {
        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        tempObjects.Add(data);
        data.id = 970000 + tempObjects.Count;
        data.characterName = name;
        data.characterType = type;
        data.role = role;
        data.speciesTag = type == CharacterType.Intruder ? "Intruder" : "QA";
        data.baseStats = CharacterStatBlock.CreateDefault(statValue);
        data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

        GameObject obj = new GameObject(name);
        tempObjects.Add(obj);
        obj.AddComponent<SpriteRenderer>();
        CharacterActor actor = obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        if (addShopping)
        {
            obj.AddComponent<AbilityShopping>();
        }

        if (addWork)
        {
            obj.AddComponent<AbilityWork>();
        }

        AIBrain brain = obj.AddComponent<AIBrain>();
        brain.availableActions = type == CharacterType.Customer
            ? AiDebugScenarioActionFactory.CreateCustomerActions()
            : AiDebugScenarioActionFactory.CreateStaffActions();

        InjectGameObjectFromLifetimeScope(obj);
        actor.RefreshAbilityCache();
        actor.Initialization(data);
        actor.EnsureRuntimeState();
        InjectGameObjectFromLifetimeScope(obj);
        actor.RefreshAbilityCache();
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        if (actor.stats != null)
        {
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.MOOD] = 100f;
        }

        return actor;
    }

    private static void InjectGameObjectFromLifetimeScope(GameObject target)
    {
        LifetimeScope scope = FindActiveSceneLifetimeScope();
        if (scope == null || scope.Container == null || target == null)
        {
            return;
        }

        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }
    }

    private static T ResolveFromLifetimeScope<T>() where T : class
    {
        LifetimeScope scope = FindActiveSceneLifetimeScope();
        if (scope == null || scope.Container == null)
        {
            return null;
        }

        try
        {
            return scope.Container.Resolve<T>();
        }
        catch
        {
            return null;
        }
    }

    private static LifetimeScope FindActiveSceneLifetimeScope()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        LifetimeScope[] scopes = UnityEngine.Object.FindObjectsByType<LifetimeScope>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        return scopes.FirstOrDefault((scope) =>
                scope != null
                && scope.Container != null
                && scope.gameObject.scene == activeScene)
            ?? scopes.FirstOrDefault((scope) => scope != null && scope.Container != null);
    }

    private static Grid ResolveGrid(DungeonSceneComponentQuery sceneQuery)
    {
        GridSystemManager gridManager = sceneQuery.First<GridSystemManager>(includeInactive: true);
        return gridManager != null ? gridManager.grid : null;
    }

    private static bool TryPlaceActorAtNearestWalkable(
        Grid grid,
        CharacterActor actor,
        Vector2Int preferred,
        out Vector2Int position)
    {
        position = default;
        if (grid == null || actor == null || !grid.TryFindNearestWalkablePosition(preferred, out position))
        {
            return false;
        }

        actor.transform.position = grid.GetWorldPos(position);
        return true;
    }

    private static bool AgeOffDutyForQa(AbilityWork work)
    {
        if (work == null || !work.IsOffDuty)
        {
            return false;
        }

        FieldInfo dutyControllerField = typeof(AbilityWork).GetField(
            "dutyController",
            BindingFlags.Instance | BindingFlags.NonPublic);
        object dutyController = dutyControllerField?.GetValue(work);
        if (dutyController == null)
        {
            return false;
        }

        FieldInfo startedAtField = dutyController.GetType().GetField(
            "offDutyStartedAt",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (startedAtField == null)
        {
            return false;
        }

        startedAtField.SetValue(dutyController, Time.time - 999f);
        return true;
    }

    private static void SetCondition(CharacterActor actor, CharacterCondition condition, float value)
    {
        if (actor == null)
        {
            return;
        }

        actor.EnsureRuntimeState();
        if (actor.stats != null)
        {
            actor.stats[condition] = value;
        }
    }

    private static float GetCondition(CharacterActor actor, CharacterCondition condition)
    {
        if (actor == null || actor.Stats == null)
        {
            return 0f;
        }

        return actor.Stats.Stats.TryGetValue(condition, out float value) ? value : 0f;
    }

    private static void InvokePrivateLlmResult(
        object target,
        string methodName,
        string json,
        string originalText = "")
    {
        if (target == null)
        {
            return;
        }

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(
            target,
            new object[]
            {
                new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    json,
                    string.Empty,
                    originalText)
            });
    }

    private static void InvokePrivateLlmResult(
        object target,
        string methodName,
        CharacterActor actor,
        string json)
    {
        if (target == null)
        {
            return;
        }

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(
            target,
            new object[]
            {
                actor,
                new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    json,
                    string.Empty,
                    string.Empty)
            });
    }

    private static DefenseFacility FindTriggerableDefense(
        IReadOnlyList<BuildableObject> buildings,
        out DefenseTriggerTiming timing)
    {
        DefenseTriggerTiming[] timings =
        {
            DefenseTriggerTiming.OnEnter,
            DefenseTriggerTiming.GuardResponse,
            DefenseTriggerTiming.Periodic,
            DefenseTriggerTiming.Cooldown
        };

        foreach (DefenseFacility defense in buildings.OfType<DefenseFacility>())
        {
            foreach (DefenseTriggerTiming candidateTiming in timings)
            {
                if (defense != null && defense.CanTrigger(candidateTiming, out _))
                {
                    timing = candidateTiming;
                    return defense;
                }
            }
        }

        timing = DefenseTriggerTiming.None;
        return null;
    }

    private static GridBuildingPlacementService CreateQaPlacementService(
        Grid grid,
        List<UnityEngine.Object> tempObjects)
    {
        if (grid == null)
        {
            return null;
        }

        IDataCatalog catalog = ResolveFromLifetimeScope<IDataCatalog>();
        IReadOnlyDictionary<int, BuildingSO> buildingData = catalog != null
            ? catalog.GetData<BuildingSO>()
            : null;
        BuildingSO hallway = buildingData != null && buildingData.TryGetValue(0, out BuildingSO resolvedHallway)
            ? resolvedHallway
            : null;
        IGridBuildingObjectFactory objectFactory = ResolveFromLifetimeScope<IGridBuildingObjectFactory>()
            ?? new GridBuildingObjectFactory();
        IGridBuildingVisual visual = null;
        try
        {
            visual = ResolveFromLifetimeScope<IGridTextureProvider>()?.Texture;
        }
        catch
        {
            visual = null;
        }

        GridBuildingFactory buildingFactory = new GridBuildingFactory(
            visual,
            (building) =>
            {
                if (building == null)
                {
                    return;
                }

                tempObjects?.Add(building.gameObject);
                InjectGameObjectFromLifetimeScope(building.gameObject);
            },
            objectFactory);

        return new GridBuildingPlacementService(
            grid,
            hallway,
            (id) => buildingData != null && buildingData.TryGetValue(id, out BuildingSO result) ? result : null,
            buildingFactory,
            new BuildingPlacementValidator(
                new GridPlacementValidator(),
                () =>
                {
                    IGameDataProvider gameDataProvider = ResolveFromLifetimeScope<IGameDataProvider>();
                    return gameDataProvider != null && gameDataProvider.TryGetGameData(out GameData gameData)
                        ? new BuildingConditionContext(gameData)
                        : BuildingConditionContext.Empty;
                }));
    }

    private static bool TryFindPlaceablePosition(
        GridBuildingPlacementService placementService,
        BuildingSO buildingData,
        Grid grid,
        out Vector2Int position,
        out string message)
    {
        position = Vector2Int.zero;
        message = string.Empty;
        if (placementService == null || buildingData == null || grid == null)
        {
            message = "missing placement prerequisites";
            return false;
        }

        foreach (GridCell cell in grid.GetCells())
        {
            if (cell == null)
            {
                continue;
            }

            if (placementService.CanPlaceBuilding(buildingData, cell.Position, out message))
            {
                position = cell.Position;
                return true;
            }
        }

        return false;
    }

    private static T FindPlacedBuilding<T>(
        Grid grid,
        BuildingSO buildingData,
        Vector2Int position) where T : BuildableObject
    {
        if (grid == null || buildingData == null)
        {
            return null;
        }

        foreach (Vector2Int gridPosition in buildingData.GetGridPosList(position))
        {
            GridCell cell = grid.GetGridCell(gridPosition);
            T match = cell?
                .GetAllOccupants()
                .OfType<T>()
                .FirstOrDefault((building) => building != null && building.BuildingData == buildingData);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool TryFindWalkablePosition(Grid grid, out Vector2Int position)
    {
        position = Vector2Int.zero;
        if (grid == null)
        {
            return false;
        }

        foreach (GridCell cell in grid.GetCells())
        {
            if (cell != null && grid.IsWalkable(cell.Position))
            {
                position = cell.Position;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindReachablePair(
        Grid grid,
        out Vector2Int start,
        out Vector2Int destination)
    {
        start = Vector2Int.zero;
        destination = Vector2Int.zero;
        if (!TryFindWalkablePosition(grid, out start))
        {
            return false;
        }

        Vector2Int startPosition = start;
        GridPathSearchResult search = grid.SearchPath(startPosition);
        destination = search.GetReachablePositions()
            .Where((position) => position != startPosition && grid.IsWalkable(position))
            .OrderByDescending((position) => Mathf.Abs(position.x - startPosition.x) + Mathf.Abs(position.y - startPosition.y))
            .FirstOrDefault();
        return destination != startPosition && grid.IsValidGridPos(destination);
    }

    private static bool ClearShopStockForQa(Shop shop)
    {
        if (shop == null)
        {
            return false;
        }

        FieldInfo field = typeof(Shop).GetField("stocks", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            return false;
        }

        field.SetValue(shop, new List<RemainStock>());
        return true;
    }

    private static void DestroyTempObjects(IEnumerable<UnityEngine.Object> tempObjects)
    {
        if (tempObjects == null)
        {
            return;
        }

        foreach (UnityEngine.Object obj in tempObjects.Where((item) => item != null).Reverse())
        {
            KillTweens(obj);
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }
    }

    private static void KillTweens(UnityEngine.Object obj)
    {
        DOTween.Kill(obj, complete: false);
        if (obj is GameObject gameObject)
        {
            foreach (Component component in gameObject.GetComponentsInChildren<Component>(true))
            {
                if (component != null)
                {
                    DOTween.Kill(component, complete: false);
                    DOTween.Kill(component.gameObject, complete: false);
                    DOTween.Kill(component.transform, complete: false);
                }
            }
        }
    }

    private static FacilityBlueprintSO FindBlueprintCandidate(BlueprintResearchRuntime research)
    {
        string[] guids = AssetDatabase.FindAssets("t:FacilityBlueprintSO", new[] { "Assets/Resources" });
        return guids
            .Select((guid) => AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>(AssetDatabase.GUIDToAssetPath(guid)))
            .FirstOrDefault((blueprint) => blueprint != null
                && (research == null || !research.State.IsCompleted(blueprint)));
    }

    private static FacilitySynthesisRecipeSO FindMatchingSynthesisRecipe(
        IEnumerable<FacilitySynthesisRecipeSO> recipes,
        IReadOnlyList<BuildableObject> buildings,
        out List<BuildableObject> materials)
    {
        materials = new List<BuildableObject>();
        foreach (FacilitySynthesisRecipeSO recipe in recipes ?? Array.Empty<FacilitySynthesisRecipeSO>())
        {
            if (recipe == null || !recipe.HasValidData)
            {
                continue;
            }

            List<BuildableObject> candidates = new List<BuildableObject>();
            foreach (BuildingSO required in recipe.materialBuildings)
            {
                BuildableObject match = buildings.FirstOrDefault((building) =>
                    building != null
                    && !candidates.Contains(building)
                    && building.BuildingData == required);
                if (match != null)
                {
                    candidates.Add(match);
                }
            }

            if (candidates.Count == recipe.materialBuildings.Length)
            {
                materials = candidates;
                return recipe;
            }
        }

        return null;
    }

    private static IReadOnlyList<T> FindActiveSceneComponents<T>() where T : Component
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where((candidate) => candidate != null && candidate.gameObject.scene == activeScene)
            .ToList();
    }

    private static Rect GetWorldRect(RectTransform rectTransform)
    {
        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);
        float minX = corners.Min((corner) => corner.x);
        float maxX = corners.Max((corner) => corner.x);
        float minY = corners.Min((corner) => corner.y);
        float maxY = corners.Max((corner) => corner.y);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static string CompactLog(string text)
    {
        return CompactText(text, 260);
    }

    private static string CompactText(string text, int maxLength = 120)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "<none>";
        }

        string singleLine = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= maxLength
            ? singleLine
            : singleLine.Substring(0, maxLength) + "...";
    }

    private static int CountRuntimeInActiveScene<T>() where T : Component
    {
        Scene activeScene = SceneManager.GetActiveScene();
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count((candidate) => candidate != null && candidate.gameObject.scene == activeScene);
    }
}
