using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;
using static GameplayPerformanceSceneQuery;
using static GameplayPerformanceReportEvaluator;
using VContainer;
public sealed class DungeonGameplayPerformanceProbe : MonoBehaviour
{
    private const string EnableArgument = "-gameplay-performance-profile";
    private const string GameplaySceneName = "GameplayScene";
    private readonly List<string> capturedMessages = new List<string>();
    private readonly GameplayPerformanceReport report = new GameplayPerformanceReport();

    private GameplayPerformanceOptions options;
    private int warningCount;
    private int errorCount;
    private int originalVSyncCount;
    private int originalTargetFrameRate;
    private float originalTimeScale;
    private float originalFixedDeltaTime;
    private bool finished;
    private bool editorSlowTraceEnabled;
    private bool aiCaptureActive;
    private bool playableRunSetupAttempted;
    private string profileException;
    private ICharacterAiPerformanceCaptureScope aiCaptureScope;
    private GameplayPerformanceWorldConfigurator worldConfigurator;
    private GameplayPerformanceWorldSummaryCollector worldSummaryCollector;
    private GameplayRawProfilerSnapshotCollector rawProfilerCollector;
    private GameplayPerformanceMeasurementSession measurementSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!HasCommandLineArgument(EnableArgument)
            || FindAnyObjectByType<DungeonGameplayPerformanceProbe>() != null)
        {
            return;
        }

        CreateProbe(
            GameplayPerformanceOptions.Parse(Environment.GetCommandLineArgs()),
            enableSlowTrace: false);
    }

#if UNITY_EDITOR
    public static string GetEditorReadinessDiagnostics()
    {
        Scene scene = SceneManager.GetActiveScene();
        DungeonRuntimeLifetimeScope scope =
            FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
        GridSystemManager gridSystem =
            FindSceneComponent<GridSystemManager>(scene);
        CharacterSpawner spawner =
            FindSceneComponent<CharacterSpawner>(scene);
        CharacterActor[] actors =
            FindSceneComponents<CharacterActor>(scene);
        int activeActors = actors.Count(actor =>
            actor != null
            && actor.CurrentLifecycleState == CharacterLifecycleState.Active);
        return $"scene={scene.name}; "
            + $"scope={scope != null}; "
            + $"container={scope?.Container != null}; "
            + $"gridSystem={gridSystem != null}; "
            + $"grid={gridSystem?.grid != null}; "
            + $"spawner={spawner != null}; "
            + $"pool={spawner?.characterPool != null}; "
            + $"actors={actors.Length}; "
            + $"activeActors={activeActors}";
    }

    public static void StartEditorProfile(
        string profileId,
        int actorCount,
        int facilityCount,
        int gridWidth,
        int gridHeight,
        int activeFloors,
        int warmupFrames,
        float sampleSeconds,
        string reportPath,
        string screenshotPath,
        float simulationSpeed = 1f,
        bool disableAiScheduler = false,
        bool disableCharacterPresentation = false,
        bool disableCharacterStatsUpdates = false,
        bool captureRawProfiler = false,
        int livestockCount = 0,
        int normalOperationSupplyDays = 0)
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "The gameplay performance profile requires PlayMode.");
        }

        if (FindAnyObjectByType<DungeonGameplayPerformanceProbe>() != null)
        {
            throw new InvalidOperationException(
                "A gameplay performance profile is already running.");
        }

        GameplayPerformanceOptions editorOptions = GameplayPerformanceOptions.CreateEditor(
            profileId,
            actorCount,
            facilityCount,
            gridWidth,
            gridHeight,
            activeFloors,
            warmupFrames,
            sampleSeconds,
            reportPath,
            screenshotPath,
            simulationSpeed,
            disableAiScheduler,
            disableCharacterPresentation,
            disableCharacterStatsUpdates,
            captureRawProfiler,
            livestockCount,
            normalOperationSupplyDays);
        bool enableSlowTrace = profileId?.IndexOf(
                "trace",
                StringComparison.OrdinalIgnoreCase) >= 0;
        CreateProbe(editorOptions, enableSlowTrace);
    }

    public static void StartEditorEconomyMixedPopulationProfile()
    {
        StartEditorProfile(
            profileId: "economy-100-staff-100-livestock-x5",
            actorCount: 100,
            facilityCount: 128,
            gridWidth: 256,
            gridHeight: 8,
            activeFloors: 4,
            warmupFrames: 900,
            sampleSeconds: 20f,
            reportPath:
                "docs/implementation-reports/economy-100-staff-100-livestock-x5-latest.json",
            screenshotPath:
                "docs/implementation-reports/economy-100-staff-100-livestock-x5-latest.png",
            simulationSpeed: 5f,
            livestockCount: 100,
            normalOperationSupplyDays: 5);
    }
#endif

    private void Awake()
    {
        rawProfilerCollector = new GameplayRawProfilerSnapshotCollector();
        measurementSession = new GameplayPerformanceMeasurementSession(
            rawProfilerCollector);
    }

    private IEnumerator Start()
    {
        options ??= GameplayPerformanceOptions.Parse(Environment.GetCommandLineArgs());
        GameplayPerformanceReportEvaluator.Initialize(report, options);
        worldConfigurator = new GameplayPerformanceWorldConfigurator(
            options,
            report);
        worldSummaryCollector = new GameplayPerformanceWorldSummaryCollector(
            options,
            report);
        Application.logMessageReceived += CaptureLog;
        originalVSyncCount = QualitySettings.vSyncCount;
        originalTargetFrameRate = Application.targetFrameRate;
        originalTimeScale = Time.timeScale;
        originalFixedDeltaTime = Time.fixedDeltaTime;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;

        Stopwatch totalStopwatch = Stopwatch.StartNew();
        yield return RunSafely(RunProfile());
        report.totalProfileMilliseconds = totalStopwatch.Elapsed.TotalMilliseconds;
        report.valid = string.IsNullOrWhiteSpace(profileException)
            && GameplayPerformanceReportEvaluator.Validate(
                report,
                options,
                measurementSession.SampleCount);
        report.failureReason = report.valid
            ? string.Empty
            : !string.IsNullOrWhiteSpace(profileException)
                ? profileException
                : GameplayPerformanceReportEvaluator.BuildFailureReason(
                    report,
                    options,
                    measurementSession.SampleCount);

        yield return FinishProfile();
    }

    private IEnumerator RunSafely(IEnumerator root)
    {
        Stack<IEnumerator> stack = new Stack<IEnumerator>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            IEnumerator currentEnumerator = stack.Peek();
            bool movedNext = false;
            object current = null;
            Exception failure = null;
            try
            {
                movedNext = currentEnumerator.MoveNext();
                if (movedNext)
                {
                    current = currentEnumerator.Current;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (failure != null)
            {
                profileException = failure.ToString();
                errorCount++;
                UnityEngine.Debug.LogException(failure, this);
                yield break;
            }

            if (!movedNext)
            {
                (currentEnumerator as IDisposable)?.Dispose();
                stack.Pop();
                continue;
            }

            if (current is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }

            yield return current;
        }
    }

    private IEnumerator RunProfile()
    {
        LogProfileStage("ensure-gameplay-run");
        yield return EnsureGameplayRun();
        LogProfileStage("wait-gameplay-ready");
        yield return WaitForGameplayReady();
        BeginAiPerformanceCapture();
#if UNITY_EDITOR
        if (options.IsEditorProfile)
        {
            UnpauseGameplay();
            LogProfileStage("editor-gc-baseline");
            yield return measurementSession.CaptureEditorGcBaseline(report);
        }
#endif

        Stopwatch setupStopwatch = Stopwatch.StartNew();
        LogProfileStage("configure-world");
        yield return worldConfigurator.ConfigureMeasuredWorld();
        worldConfigurator.ApplyDiagnosticIsolation();
        setupStopwatch.Stop();
        report.setupMilliseconds = setupStopwatch.Elapsed.TotalMilliseconds;

        ResetAiPerformanceRecorder();
        UnpauseGameplay();
#if UNITY_EDITOR
        rawProfilerCollector.Begin(options);
#endif
        LogProfileStage("warmup");
        yield return WarmUp();
        LogProfileStage("capture");
        yield return measurementSession.Capture(options, report);
#if UNITY_EDITOR
        rawProfilerCollector.End();
#endif
        worldSummaryCollector.Capture(warningCount, errorCount, capturedMessages);
        LogProfileStage("capture-complete");
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= CaptureLog;
        worldConfigurator?.Dispose();
        worldConfigurator = null;
        measurementSession?.Dispose();
        rawProfilerCollector?.Dispose();
        RestoreFrameSettings();
        if (aiCaptureActive && aiCaptureScope != null)
        {
            aiCaptureScope.EndDetailedCapture();
            if (editorSlowTraceEnabled)
            {
                aiCaptureScope.EndSlowTrace();
            }

            aiCaptureActive = false;
            aiCaptureScope = null;
            editorSlowTraceEnabled = false;
        }
    }

    private static DungeonGameplayPerformanceProbe CreateProbe(
        GameplayPerformanceOptions requestedOptions,
        bool enableSlowTrace)
    {
        GameObject host = new GameObject(nameof(DungeonGameplayPerformanceProbe));
        DontDestroyOnLoad(host);
        DungeonGameplayPerformanceProbe probe =
            host.AddComponent<DungeonGameplayPerformanceProbe>();
        probe.options = requestedOptions
            ?? throw new ArgumentNullException(nameof(requestedOptions));
        probe.editorSlowTraceEnabled = enableSlowTrace;
        return probe;
    }

    private void BeginAiPerformanceCapture()
    {
        if (aiCaptureActive || options?.IsEditorProfile != true)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        DungeonRuntimeLifetimeScope scope =
            FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
        aiCaptureScope = scope?.Container
            ?.Resolve<ICharacterAiPerformanceCaptureScope>()
            ?? throw new InvalidOperationException(
                "The gameplay performance profile could not resolve its AI capture scope.");
        aiCaptureScope.BeginDetailedCapture();
        if (editorSlowTraceEnabled)
        {
            aiCaptureScope.BeginSlowTrace();
        }

        aiCaptureActive = true;
    }

    private IEnumerator EnsureGameplayRun()
    {
        if (SceneManager.GetActiveScene().name == GameplaySceneName)
        {
            yield break;
        }

        DungeonSceneNavigator navigator = ResolveSceneNavigator();
        if (!navigator.StartNewGameDirectForDebug(DungeonDifficulty.Normal))
        {
            throw new InvalidOperationException(
                "The normal new-run transition could not be started.");
        }

        float deadline = Time.realtimeSinceStartup + 90f;
        while (SceneManager.GetActiveScene().name != GameplaySceneName)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                throw new TimeoutException("GameplayScene did not load within 90 seconds.");
            }

            yield return null;
        }
    }

    private static DungeonSceneNavigator ResolveSceneNavigator()
    {
        DungeonTitleLifetimeScope title =
            FindAnyObjectByType<DungeonTitleLifetimeScope>();
        if (title?.Container != null)
        {
            return title.Container.Resolve<DungeonSceneNavigator>();
        }

        DungeonPreparationLifetimeScope preparation =
            FindAnyObjectByType<DungeonPreparationLifetimeScope>();
        if (preparation?.Container != null)
        {
            return preparation.Container.Resolve<DungeonSceneNavigator>();
        }

        DungeonRuntimeLifetimeScope gameplay =
            FindAnyObjectByType<DungeonRuntimeLifetimeScope>();
        if (gameplay?.Container != null)
        {
            return gameplay.Container.Resolve<DungeonSceneNavigator>();
        }

        throw new InvalidOperationException(
            "The performance probe could not resolve a scene-scoped navigator.");
    }

    private IEnumerator WaitForGameplayReady()
    {
        float deadline = Time.realtimeSinceStartup + 90f;
        while (Time.realtimeSinceStartup < deadline)
        {
            Scene scene = SceneManager.GetActiveScene();
            DungeonRuntimeLifetimeScope scope = FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
            GridSystemManager gridSystem = FindSceneComponent<GridSystemManager>(scene);
            CharacterSpawner spawner = FindSceneComponent<CharacterSpawner>(scene);
            CharacterActor[] actors = FindSceneComponents<CharacterActor>(scene);
            bool infrastructureReady = scope != null
                && scope.Container != null
                && gridSystem != null
                && gridSystem.grid != null
                && spawner != null
                && spawner.characterPool != null;
            if (infrastructureReady && !playableRunSetupAttempted)
            {
                playableRunSetupAttempted = true;
                EnsurePlayableRun(scope);
                yield return null;
                actors = FindSceneComponents<CharacterActor>(scene);
            }

            bool hasReadyActor = options.IsEditorProfile
                ? actors.Any(actor =>
                    actor != null
                    && actor.gameObject.activeInHierarchy)
                : actors.Any(actor =>
                    actor != null
                    && actor.CurrentLifecycleState
                        == CharacterLifecycleState.Active);
            if (infrastructureReady && hasReadyActor)
            {
                yield return new WaitForSecondsRealtime(2f);
                yield break;
            }

            yield return null;
        }

        throw new TimeoutException(
            "The normal gameplay run did not finish initializing within 90 seconds.");
    }

    private void EnsurePlayableRun(DungeonRuntimeLifetimeScope scope)
    {
        IStartPartyPreparationService preparation =
            scope.Container.Resolve<IStartPartyPreparationService>();
        IOwnerRunManagerProvider ownerProvider =
            scope.Container.Resolve<IOwnerRunManagerProvider>();
        IPreparedStartPartyGameplayApplier applier =
            scope.Container.Resolve<IPreparedStartPartyGameplayApplier>();
        if (!ownerProvider.TryGetManager(out OwnerRunManager manager)
            || manager == null)
        {
            throw new InvalidOperationException(
                "The gameplay profile could not resolve the owner run manager.");
        }

        if (manager.CurrentOwnerActor != null)
        {
            return;
        }

        List<string> failures = new List<string>();
        int runSeed = 17012026;
        foreach (CharacterSO owner in manager.OwnerCandidates.Where(candidate => candidate != null))
        {
            if (!preparation.Begin(owner, out string beginMessage))
            {
                failures.Add($"{owner.characterName}: begin={beginMessage}");
                continue;
            }

            try
            {
                if (!preparation.TryCreatePreparedSnapshot(
                        DungeonDifficulty.Normal,
                        runSeed,
                        out PreparedStartPartySnapshot snapshot,
                        out string snapshotMessage))
                {
                    failures.Add($"{owner.characterName}: snapshot={snapshotMessage}");
                    continue;
                }

                if (applier.TryApply(snapshot, out string applyMessage))
                {
                    LogProfileStage(
                        $"prepared-run-applied:{owner.characterName}:{applyMessage}");
                    return;
                }

                failures.Add($"{owner.characterName}: apply={applyMessage}");
            }
            finally
            {
                preparation.Cancel();
            }
        }

        throw new InvalidOperationException(
            "The gameplay profile could not create a playable start party. "
            + string.Join(" | ", failures));
    }

    private void LogProfileStage(string stage)
    {
        UnityEngine.Debug.Log(
            $"GAMEPLAY_PERFORMANCE_PROFILE_STAGE {options?.ProfileId ?? "unconfigured"} "
            + $"{stage}",
            this);
    }

    private IEnumerator WarmUp()
    {
        int frames = Mathf.Max(1, options.WarmupFrames);
        for (int i = 0; i < frames; i++)
        {
            yield return null;
        }
    }

    private void ResetAiPerformanceRecorder()
    {
        Scene scene = SceneManager.GetActiveScene();
        DungeonRuntimeLifetimeScope scope =
            FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
        if (scope != null && scope.Container != null)
        {
            scope.Container.Resolve<ICharacterAiPerformanceRecorder>().Reset();
            scope.Container.Resolve<ICharacterDeprivationCommand>()
                .ResetDiagnostics();
        }
    }

    private IEnumerator FinishProfile()
    {
        if (finished)
        {
            yield break;
        }

        finished = true;
        worldSummaryCollector.Capture(warningCount, errorCount, capturedMessages);
        Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)
            ?? Application.persistentDataPath);
        Directory.CreateDirectory(Path.GetDirectoryName(options.ScreenshotPath)
            ?? Application.persistentDataPath);
        File.WriteAllText(options.ReportPath, JsonUtility.ToJson(report, true));
        ScreenCapture.CaptureScreenshot(options.ScreenshotPath);
        UnityEngine.Debug.Log(
            $"GAMEPLAY_PERFORMANCE_PROFILE_COMPLETE {options.ProfileId} "
            + $"valid={report.valid} p95={report.frame.p95:F3}ms "
            + $"p99={report.frame.p99:F3}ms actors={report.actualActorCount} "
            + $"livestock={report.actualLivestockCount} "
            + $"buildings={report.actualBuildingCount} report={options.ReportPath}");

        yield return new WaitForSecondsRealtime(Mathf.Max(2f, options.HoldSeconds));
        RestoreFrameSettings();
#if UNITY_EDITOR
        if (options.IsEditorProfile)
        {
            UnityEditor.EditorApplication.isPlaying = false;
            yield break;
        }
#endif
        Application.Quit(report.valid ? 0 : 2);
    }

    private void UnpauseGameplay()
    {
        Scene scene = SceneManager.GetActiveScene();
        DungeonRuntimeLifetimeScope scope = FindSceneComponent<DungeonRuntimeLifetimeScope>(scene);
        if (scope?.Container != null)
        {
            IGameSpeedController speedController =
                scope.Container.Resolve<IGameSpeedController>();
            speedController.SetSpeed(Mathf.Clamp(
                Mathf.RoundToInt(options.SimulationSpeed),
                1,
                5));
            speedController.SetPaused(false);
        }

    }

    private static bool HasCommandLineArgument(string argument)
    {
        return Environment.GetCommandLineArgs().Any(value =>
            string.Equals(value, argument, StringComparison.OrdinalIgnoreCase));
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error
            || type == LogType.Assert
            || type == LogType.Exception)
        {
            errorCount++;
        }
        else if (type == LogType.Warning)
        {
            warningCount++;
        }

        if ((type == LogType.Warning
                || type == LogType.Error
                || type == LogType.Assert
                || type == LogType.Exception)
            && capturedMessages.Count < 20)
        {
            capturedMessages.Add($"{type}: {condition}");
        }
    }

    private void RestoreFrameSettings()
    {
        QualitySettings.vSyncCount = originalVSyncCount;
        Application.targetFrameRate = originalTargetFrameRate;
        Time.timeScale = originalTimeScale;
        Time.fixedDeltaTime = originalFixedDeltaTime;
    }


}
