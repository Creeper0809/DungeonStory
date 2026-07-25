using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BehaviorDesigner.Runtime;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CharacterAiStressDebugScenarios
{
    private const int NpcCount = 500;
    private static readonly int[] ScaleNpcCounts = { 100, 300, NpcCount };
    private const int SimulationFrames = 180;
    private const int DecisionBudget = 16;
    private const int PathBudget = 8;
    private const string PlayModeProfileRequestedKey = "DungeonStory.CharacterAiStress.PlayModeProfile.Requested";
    private const string PlayModeProfileNpcCountKey = "DungeonStory.CharacterAiStress.PlayModeProfile.NpcCount";
    private const string PlayModeProfileWarmupFramesKey = "DungeonStory.CharacterAiStress.PlayModeProfile.WarmupFrames";
    private const string PlayModeProfileSampleFramesKey = "DungeonStory.CharacterAiStress.PlayModeProfile.SampleFrames";
    private const string PlayModeProfileExitWhenDoneKey = "DungeonStory.CharacterAiStress.PlayModeProfile.ExitWhenDone";
    private const string PlayModeProfileReportKey = "DungeonStory.CharacterAiStress.PlayModeProfile.Report";
    private const string ProfileReportPath = "docs/implementation-reports/ai-play-mode-profile-latest.json";

    private sealed class FixedSchedulerService : ICharacterAiSchedulingService
    {
        private readonly CharacterAiScheduler scheduler;

        public FixedSchedulerService(CharacterAiScheduler scheduler)
        {
            this.scheduler = scheduler
                ?? throw new ArgumentNullException(nameof(scheduler));
        }

        public bool IsDrivingAi => scheduler.IsDrivingAi;
        public void Register(CharacterActor actor) => scheduler.RegisterActor(actor);
        public void Unregister(CharacterActor actor) => scheduler.UnregisterActor(actor);
        public void RequestImmediateDecision(CharacterActor actor) => scheduler.RequestImmediateDecisionFor(actor);
        public bool TryConsumePathSearchBudget() => scheduler.TryConsumePathSearchBudget();
        public bool ShouldShowCharacterFeedback(CharacterActor actor) => false;
        public int GetMovementFrameStride(CharacterActor actor) => scheduler.GetMovementFrameStrideFor(actor);
        public void ResetPathSearchBudgetForDebug() => scheduler.ResetPathSearchBudgetForDebugInstance();
    }

    public static string LastReport { get; private set; } = string.Empty;
    public static string LastScaleReport { get; private set; } = string.Empty;
    public static string LastPlayModeProfileReport => SessionState.GetString(PlayModeProfileReportKey, string.Empty);
    public static bool IsPlayModeProfileRunning => SessionState.GetBool(PlayModeProfileRequestedKey, false);

    [InitializeOnLoadMethod]
    private static void InitializePlayModeProfiler()
    {
        PlayModeProfileSession.Initialize();
    }

    [MenuItem("DungeonStory/Debug/AI/Run 500 NPC AI Stress Scenario")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            UnityEngine.Debug.LogError("500 NPC AI stress scenario failed.");
        }
    }

    [MenuItem("DungeonStory/Debug/AI/Run 100 300 500 NPC AI Stress Suite")]
    public static void RunScaleSuiteFromMenu()
    {
        bool success = RunScaleSuite(true);
        if (!success)
        {
            UnityEngine.Debug.LogError("100/300/500 NPC AI stress suite failed.");
        }
    }

    [MenuItem("DungeonStory/Debug/AI/Profile 500 NPC Play Mode")]
    public static void RunPlayModeProfileFromMenu()
    {
        StartPlayModeProfile(NpcCount, 0, 600, true);
    }

    public static void StartPlayModeProfile(
        int npcCount = NpcCount,
        int warmupFrames = 0,
        int sampleFrames = 600,
        bool exitWhenDone = true)
    {
        PlayModeProfileSession.Start(npcCount, warmupFrames, sampleFrames, exitWhenDone);
    }

    public static void PumpPlayModeProfileFrames(int maxFrames = 600)
    {
        PlayModeProfileSession.PumpFrames(maxFrames);
    }

    public static bool RunAll(bool logSuccess)
    {
        return RunForCount(NpcCount, logSuccess);
    }

    public static bool RunScaleSuite(bool logSuccess)
    {
        List<string> reports = new List<string>();
        bool valid = true;
        foreach (int npcCount in ScaleNpcCounts)
        {
            valid &= RunForCount(npcCount, logSuccess);
            reports.Add($"{npcCount}: {LastReport}");
        }

        LastScaleReport = string.Join("\n", reports);
        if (logSuccess || !valid)
        {
            UnityEngine.Debug.Log($"100/300/500 NPC AI stress suite valid={valid}\n{LastScaleReport}");
        }

        return valid;
    }

    public static bool RunForCount(int npcCount, bool logSuccess)
    {
        npcCount = Mathf.Max(1, npcCount);
        using StressWorld world = new StressWorld();
        world.PlaceFacilities();
        world.CreateCustomers(npcCount);

        Stopwatch stopwatch = Stopwatch.StartNew();
        int maxDecisions = 0;
        int maxPathSearches = 0;
        int maxBrokerPathSearches = 0;
        int maxBrokerPathCacheHits = 0;
        int maxBrokerPathBudgetDeferrals = 0;
        int maxBehaviorTreeTicks = 0;
        int totalDecisions = 0;
        int totalPathSearches = 0;
        int totalBrokerPathSearches = 0;
        int totalBrokerPathCacheHits = 0;
        int totalBrokerPathBudgetDeferrals = 0;
        int totalBehaviorTreeTicks = 0;

        for (int frame = 0; frame < SimulationFrames; frame++)
        {
            world.Scheduler.RunManualTick(1f / 60f);
            maxDecisions = Mathf.Max(maxDecisions, world.Scheduler.LastProcessedDecisionCount);
            maxPathSearches = Mathf.Max(maxPathSearches, world.Scheduler.LastPathSearchCount);
            maxBrokerPathSearches = Mathf.Max(maxBrokerPathSearches, world.Scheduler.LastBrokerPathSearchCount);
            maxBrokerPathCacheHits = Mathf.Max(maxBrokerPathCacheHits, world.Scheduler.LastBrokerPathCacheHitCount);
            maxBrokerPathBudgetDeferrals = Mathf.Max(maxBrokerPathBudgetDeferrals, world.Scheduler.LastBrokerPathBudgetDeferralCount);
            maxBehaviorTreeTicks = Mathf.Max(maxBehaviorTreeTicks, world.Scheduler.LastBehaviorTreeTickCount);
            totalDecisions += world.Scheduler.LastProcessedDecisionCount;
            totalPathSearches += world.Scheduler.LastPathSearchCount;
            totalBrokerPathSearches += world.Scheduler.LastBrokerPathSearchCount;
            totalBrokerPathCacheHits += world.Scheduler.LastBrokerPathCacheHitCount;
            totalBrokerPathBudgetDeferrals += world.Scheduler.LastBrokerPathBudgetDeferralCount;
            totalBehaviorTreeTicks += world.Scheduler.LastBehaviorTreeTickCount;
        }

        stopwatch.Stop();

        int touchedCharacters = world.Characters.Count((character) =>
            character != null
            && character.ai != null
            && (!character.ai.isBestActionEnd || character.ai.bestAction != null || character.Log.Count > 0));
        int pendingCharacters = world.Characters.Count((character) => character != null && character.IsAiDecisionPending);
        int withActions = world.Characters.Count((character) =>
            character != null
            && character.ai != null
            && character.ai.availableActions != null
            && character.ai.availableActions.Length > 0);
        int tickedTrees = world.Characters.Count((character) =>
            character != null
            && character.BehaviorTree != null
            && character.BehaviorTree.DungeonStoryTickCount > 0);
        string branches = string.Join(
            ",",
            world.Characters
                .Where((character) => character != null && character.Blackboard != null)
                .GroupBy((character) => character.Blackboard.CurrentBranch)
                .OrderByDescending((group) => group.Count())
                .Select((group) => $"{group.Key}:{group.Count()}"));
        string samples = string.Join(
            " | ",
            world.Characters
                .Where((character) => character != null && character.Blackboard != null)
                .Take(5)
                .Select((character) =>
                    $"{character.name}:{character.Blackboard.CurrentBranch}/{character.Blackboard.CurrentTask}"
                    + $"/route={character.Blackboard.LastDecisionRouteSummary}"
                    + $"/brain={character.ai?.GetDebugSummary(1)}"));
        GridSystemManager gridSystemManager = Object.FindFirstObjectByType<GridSystemManager>();
        bool gridReady = gridSystemManager != null && gridSystemManager.grid == world.Grid;

        bool valid = world.Scheduler.RegisteredCharacterCount == npcCount
            && touchedCharacters > 0
            && tickedTrees == npcCount
            && withActions == npcCount
            && maxDecisions <= DecisionBudget
            && maxPathSearches <= PathBudget
            && totalDecisions > 0
            && totalBehaviorTreeTicks > 0
            && totalPathSearches > 0;

        LastReport =
            $"valid={valid}, registered={world.Scheduler.RegisteredCharacterCount}, " +
            $"pending={pendingCharacters}, withActions={withActions}, tickedTrees={tickedTrees}, gridReady={gridReady}, " +
            $"pathBudgetActive={world.Scheduler.IsPathBudgetActiveForDebug}, touched={touchedCharacters}, " +
            $"totalDecisions={totalDecisions}, maxDecisions/frame={maxDecisions}, " +
            $"totalBtTicks={totalBehaviorTreeTicks}, maxBtTicks/frame={maxBehaviorTreeTicks}, " +
            $"totalPathSearches={totalPathSearches}, maxPathSearches/frame={maxPathSearches}, " +
            $"brokerPathSearches={totalBrokerPathSearches}, brokerCacheHits={totalBrokerPathCacheHits}, " +
            $"brokerBudgetDeferrals={totalBrokerPathBudgetDeferrals}, maxBrokerPathSearches/frame={maxBrokerPathSearches}, " +
            $"maxBrokerCacheHits/frame={maxBrokerPathCacheHits}, maxBrokerBudgetDeferrals/frame={maxBrokerPathBudgetDeferrals}, " +
            $"branches={branches}, samples={samples}, elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.0}";

        if (logSuccess || !valid)
        {
            UnityEngine.Debug.Log($"{npcCount} NPC AI stress: {LastReport}");
        }

        return valid;
    }

    private sealed class PlayModeProfileSession
    {
        private static PlayModeProfileSession current;

        private readonly int npcCount;
        private readonly int warmupFrames;
        private readonly int sampleFrames;
        private readonly bool exitWhenDone;
        private readonly List<double> frameTimesMs = new List<double>();
        private readonly List<double> schedulerTimesMs = new List<double>();
        private readonly Stopwatch sampleStopwatch = new Stopwatch();
        private readonly Stopwatch warmupStopwatch = new Stopwatch();

        private StressWorld world;
        private ProfilerRecorder mainThreadRecorder;
        private ProfilerRecorder gcAllocRecorder;
        private bool profilerStateCaptured;
        private bool completed;
        private bool samplingStarted;
        private float previousTimeScale;
        private int lastFrame = -1;
        private int warmupSamples;
        private int samples;
        private int totalDecisions;
        private int totalPathSearches;
        private int totalBrokerPathSearches;
        private int totalBrokerPathCacheHits;
        private int totalBrokerPathBudgetDeferrals;
        private int maxDecisions;
        private int maxPathSearches;
        private int maxBrokerPathSearches;
        private int maxBrokerPathCacheHits;
        private int maxBrokerPathBudgetDeferrals;
        private int framesOver16Ms;
        private int framesOver33Ms;
        private int mainThreadSamples;
        private long totalGcAllocBytes;
        private long maxGcAllocBytes;
        private long startMonoUsedBytes;
        private long endMonoUsedBytes;
        private int startGen0Collections;
        private double creationMs;
        private double warmupCleanupMs;
        private double totalDeltaMs;
        private double maxDeltaMs;
        private double totalMainThreadMs;
        private double maxMainThreadMs;
        private double totalSchedulerMs;
        private double maxSchedulerMs;

        private PlayModeProfileSession(
            int npcCount,
            int warmupFrames,
            int sampleFrames,
            bool exitWhenDone)
        {
            this.npcCount = Mathf.Max(1, npcCount);
            this.warmupFrames = Mathf.Max(0, warmupFrames);
            this.sampleFrames = Mathf.Max(1, sampleFrames);
            this.exitWhenDone = exitWhenDone;
        }

        public static void Initialize()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            if (SessionState.GetBool(PlayModeProfileRequestedKey, false)
                && EditorApplication.isPlaying)
            {
                EnsureCurrent().BeginIfNeeded();
            }
        }

        public static void Start(
            int npcCount,
            int warmupFrames,
            int sampleFrames,
            bool exitWhenDone)
        {
            if (SessionState.GetBool(PlayModeProfileRequestedKey, false))
            {
                UnityEngine.Debug.LogWarning("500 NPC Play Mode profile is already running.");
                return;
            }

            SessionState.SetBool(PlayModeProfileRequestedKey, true);
            SessionState.SetInt(PlayModeProfileNpcCountKey, Mathf.Max(1, npcCount));
            SessionState.SetInt(PlayModeProfileWarmupFramesKey, Mathf.Max(0, warmupFrames));
            SessionState.SetInt(PlayModeProfileSampleFramesKey, Mathf.Max(1, sampleFrames));
            SessionState.SetBool(PlayModeProfileExitWhenDoneKey, exitWhenDone);
            SessionState.SetString(PlayModeProfileReportKey, string.Empty);

            current = EnsureCurrent();
            if (EditorApplication.isPlaying)
            {
                current.BeginIfNeeded();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEngine.Debug.LogWarning("Unity is already changing Play Mode state; profile request is queued.");
                return;
            }

            EditorApplication.EnterPlaymode();
        }

        private static PlayModeProfileSession EnsureCurrent()
        {
            if (current != null)
            {
                return current;
            }

            current = new PlayModeProfileSession(
                SessionState.GetInt(PlayModeProfileNpcCountKey, NpcCount),
                SessionState.GetInt(PlayModeProfileWarmupFramesKey, 0),
                SessionState.GetInt(PlayModeProfileSampleFramesKey, 600),
                SessionState.GetBool(PlayModeProfileExitWhenDoneKey, true));
            return current;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PlayModeProfileRequestedKey, false))
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    current = null;
                }

                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EnsureCurrent().BeginIfNeeded();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                if (current != null && !current.completed)
                {
                    current.Abort("Play Mode exited before sampling completed.");
                }
            }
        }

        private static void OnEditorUpdate()
        {
            if (!SessionState.GetBool(PlayModeProfileRequestedKey, false)
                || !EditorApplication.isPlaying)
            {
                return;
            }

            EnsureCurrent().Tick();
        }

        public static void PumpFrames(int maxFrames)
        {
            if (!SessionState.GetBool(PlayModeProfileRequestedKey, false)
                || !EditorApplication.isPlaying)
            {
                UnityEngine.Debug.LogWarning("500 NPC Play Mode profile pump skipped because no profile is running in Play Mode.");
                return;
            }

            PlayModeProfileSession session = EnsureCurrent();
            session.BeginIfNeeded();
            bool wasPaused = EditorApplication.isPaused;
            EditorApplication.isPaused = true;

            int framesToPump = Mathf.Max(1, maxFrames);
            for (int i = 0;
                i < framesToPump
                && SessionState.GetBool(PlayModeProfileRequestedKey, false)
                && EditorApplication.isPlaying;
                i++)
            {
                EditorApplication.Step();
                session.SampleCurrentFrame();
            }

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPaused = wasPaused;
            }
        }

        private void BeginIfNeeded()
        {
            if (world != null)
            {
                return;
            }

            CaptureProfilerState();
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;

            Stopwatch creationStopwatch = Stopwatch.StartNew();
            world = new StressWorld();
            world.PlaceFacilities();
            world.CreateCustomers(npcCount);
            creationStopwatch.Stop();
            creationMs = creationStopwatch.Elapsed.TotalMilliseconds;

            StartRecorders();
            warmupStopwatch.Restart();

            UnityEngine.Debug.Log(
                $"500 NPC Play Mode profile started: npc={npcCount}, " +
                $"warmupFrames={warmupFrames}, sampleFrames={sampleFrames}, creationMs={creationMs:0.0}");
        }

        private void Tick()
        {
            BeginIfNeeded();
            if (Time.timeScale <= 0f)
            {
                Time.timeScale = 1f;
            }

            SampleCurrentFrame();
        }

        private void SampleCurrentFrame()
        {
            int frame = Time.frameCount;
            if (frame == lastFrame)
            {
                return;
            }

            lastFrame = frame;
            bool hasUntickedTree = world.Characters.Any(character =>
                character != null
                && character.BehaviorTree != null
                && character.BehaviorTree.DungeonStoryTickCount == 0);
            if (warmupSamples < warmupFrames || hasUntickedTree)
            {
                CharacterAiScheduler warmupScheduler = world.Scheduler;
                if (warmupScheduler != null)
                {
                    totalDecisions += warmupScheduler.LastProcessedDecisionCount;
                    totalPathSearches += warmupScheduler.LastPathSearchCount;
                    totalBrokerPathSearches += warmupScheduler.LastBrokerPathSearchCount;
                    totalBrokerPathCacheHits += warmupScheduler.LastBrokerPathCacheHitCount;
                    totalBrokerPathBudgetDeferrals += warmupScheduler.LastBrokerPathBudgetDeferralCount;
                    maxDecisions = Mathf.Max(maxDecisions, warmupScheduler.LastProcessedDecisionCount);
                    maxPathSearches = Mathf.Max(maxPathSearches, warmupScheduler.LastPathSearchCount);
                    maxBrokerPathSearches = Mathf.Max(maxBrokerPathSearches, warmupScheduler.LastBrokerPathSearchCount);
                    maxBrokerPathCacheHits = Mathf.Max(maxBrokerPathCacheHits, warmupScheduler.LastBrokerPathCacheHitCount);
                    maxBrokerPathBudgetDeferrals = Mathf.Max(maxBrokerPathBudgetDeferrals, warmupScheduler.LastBrokerPathBudgetDeferralCount);
                }

                warmupSamples++;
                return;
            }

            if (!samplingStarted)
            {
                samplingStarted = true;
                warmupStopwatch.Stop();
                Stopwatch cleanupStopwatch = Stopwatch.StartNew();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                cleanupStopwatch.Stop();
                warmupCleanupMs = cleanupStopwatch.Elapsed.TotalMilliseconds;
                startMonoUsedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                startGen0Collections = GC.CollectionCount(0);
                sampleStopwatch.Restart();
            }

            CharacterAiScheduler scheduler = world.Scheduler;
            double deltaMs = Mathf.Max(0f, Time.unscaledDeltaTime * 1000f);
            double schedulerMs = scheduler != null ? scheduler.LastProcessingMilliseconds : 0.0;
            long mainThreadNs = mainThreadRecorder.Valid ? mainThreadRecorder.LastValue : 0;
            long gcAllocBytes = gcAllocRecorder.Valid ? gcAllocRecorder.LastValue : 0;

            samples++;
            totalDeltaMs += deltaMs;
            maxDeltaMs = Math.Max(maxDeltaMs, deltaMs);
            frameTimesMs.Add(deltaMs);

            if (deltaMs > 16.7)
            {
                framesOver16Ms++;
            }

            if (deltaMs > 33.3)
            {
                framesOver33Ms++;
            }

            totalSchedulerMs += schedulerMs;
            maxSchedulerMs = Math.Max(maxSchedulerMs, schedulerMs);
            schedulerTimesMs.Add(schedulerMs);

            if (mainThreadNs > 0)
            {
                double mainThreadMs = mainThreadNs / 1000000.0;
                mainThreadSamples++;
                totalMainThreadMs += mainThreadMs;
                maxMainThreadMs = Math.Max(maxMainThreadMs, mainThreadMs);
            }

            totalGcAllocBytes += Math.Max(0, gcAllocBytes);
            maxGcAllocBytes = Math.Max(maxGcAllocBytes, gcAllocBytes);

            if (scheduler != null)
            {
                totalDecisions += scheduler.LastProcessedDecisionCount;
                totalPathSearches += scheduler.LastPathSearchCount;
                totalBrokerPathSearches += scheduler.LastBrokerPathSearchCount;
                totalBrokerPathCacheHits += scheduler.LastBrokerPathCacheHitCount;
                totalBrokerPathBudgetDeferrals += scheduler.LastBrokerPathBudgetDeferralCount;
                maxDecisions = Mathf.Max(maxDecisions, scheduler.LastProcessedDecisionCount);
                maxPathSearches = Mathf.Max(maxPathSearches, scheduler.LastPathSearchCount);
                maxBrokerPathSearches = Mathf.Max(maxBrokerPathSearches, scheduler.LastBrokerPathSearchCount);
                maxBrokerPathCacheHits = Mathf.Max(maxBrokerPathCacheHits, scheduler.LastBrokerPathCacheHitCount);
                maxBrokerPathBudgetDeferrals = Mathf.Max(maxBrokerPathBudgetDeferrals, scheduler.LastBrokerPathBudgetDeferralCount);
            }

            if (samples >= sampleFrames)
            {
                Complete();
            }
        }

        private void Complete()
        {
            completed = true;
            sampleStopwatch.Stop();
            endMonoUsedBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();

            CharacterAiScheduler scheduler = world.Scheduler;
            int touchedCharacters = world.Characters.Count((character) =>
                character != null
                && character.ai != null
                && (!character.ai.isBestActionEnd || character.ai.bestAction != null || character.Log.Count > 0));
            int pendingCharacters = world.Characters.Count((character) => character != null && character.IsAiDecisionPending);
            int withActions = world.Characters.Count((character) =>
                character != null
                && character.ai != null
                && character.ai.availableActions != null
                && character.ai.availableActions.Length > 0);
            int tickedTrees = world.Characters.Count((character) =>
                character != null
                && character.BehaviorTree != null
                && character.BehaviorTree.DungeonStoryTickCount > 0);

            double avgDeltaMs = samples > 0 ? totalDeltaMs / samples : 0.0;
            double avgSchedulerMs = samples > 0 ? totalSchedulerMs / samples : 0.0;
            double avgMainThreadMs = mainThreadSamples > 0 ? totalMainThreadMs / mainThreadSamples : 0.0;
            double avgGcAllocKb = samples > 0 ? totalGcAllocBytes / 1024.0 / samples : 0.0;
            double maxGcAllocKb = maxGcAllocBytes / 1024.0;
            double monoDeltaMb = (endMonoUsedBytes - startMonoUsedBytes) / 1024.0 / 1024.0;
            bool valid = scheduler != null
                && scheduler.RegisteredCharacterCount == npcCount
                && touchedCharacters > 0
                && tickedTrees == npcCount
                && withActions == npcCount
                && maxDecisions <= DecisionBudget
                && maxPathSearches <= PathBudget
                && totalDecisions > 0
                && totalPathSearches > 0;

            string report =
                $"valid={valid}, npc={npcCount}, registered={(scheduler != null ? scheduler.RegisteredCharacterCount : 0)}, " +
                $"active={touchedCharacters}, pending={pendingCharacters}, withActions={withActions}, tickedTrees={tickedTrees}, " +
                $"warmupFrames={warmupSamples}, warmupWallMs={warmupStopwatch.Elapsed.TotalMilliseconds:0.0}, warmupCleanupMs={warmupCleanupMs:0.0}, " +
                $"samples={samples}, sampleWallMs={sampleStopwatch.Elapsed.TotalMilliseconds:0.0}, creationMs={creationMs:0.0}, " +
                $"avgFrameMs={avgDeltaMs:0.00}, p95FrameMs={Percentile(frameTimesMs, 0.95):0.00}, maxFrameMs={maxDeltaMs:0.00}, " +
                $"frames>16.7ms={framesOver16Ms}, frames>33.3ms={framesOver33Ms}, " +
                $"avgMainThreadMs={avgMainThreadMs:0.00}, maxMainThreadMs={maxMainThreadMs:0.00}, mainThreadSamples={mainThreadSamples}, " +
                $"avgSchedulerMs={avgSchedulerMs:0.000}, p95SchedulerMs={Percentile(schedulerTimesMs, 0.95):0.000}, maxSchedulerMs={maxSchedulerMs:0.000}, " +
                $"totalDecisions={totalDecisions}, maxDecisions/frame={maxDecisions}, " +
                $"totalPathSearches={totalPathSearches}, maxPathSearches/frame={maxPathSearches}, " +
                $"brokerPathSearches={totalBrokerPathSearches}, brokerCacheHits={totalBrokerPathCacheHits}, " +
                $"brokerBudgetDeferrals={totalBrokerPathBudgetDeferrals}, maxBrokerPathSearches/frame={maxBrokerPathSearches}, " +
                $"maxBrokerCacheHits/frame={maxBrokerPathCacheHits}, maxBrokerBudgetDeferrals/frame={maxBrokerPathBudgetDeferrals}, " +
                $"avgGcAllocKB/frame={avgGcAllocKb:0.0}, maxGcAllocKB/frame={maxGcAllocKb:0.0}, " +
                $"monoUsedDeltaMB={monoDeltaMb:0.00}, gen0Collections={GC.CollectionCount(0) - startGen0Collections}";

            SessionState.SetString(PlayModeProfileReportKey, report);
            WriteProfileReport(
                valid,
                report,
                touchedCharacters,
                pendingCharacters,
                withActions,
                tickedTrees,
                avgDeltaMs,
                Percentile(frameTimesMs, 0.95),
                maxDeltaMs,
                avgSchedulerMs,
                Percentile(schedulerTimesMs, 0.95),
                maxSchedulerMs,
                avgGcAllocKb,
                maxGcAllocKb,
                monoDeltaMb);
            UnityEngine.Debug.Log($"500 NPC Play Mode profile: {report}");

            Cleanup();
            SessionState.SetBool(PlayModeProfileRequestedKey, false);
            if (exitWhenDone && EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
        }

        private void Abort(string reason)
        {
            completed = true;
            Cleanup();
            SessionState.SetBool(PlayModeProfileRequestedKey, false);
            SessionState.SetString(PlayModeProfileReportKey, $"aborted=True, reason={reason}");
            UnityEngine.Debug.LogWarning($"500 NPC Play Mode profile aborted: {reason}");
        }

        private void CaptureProfilerState()
        {
            if (profilerStateCaptured)
            {
                return;
            }

            profilerStateCaptured = true;
        }

        private void StartRecorders()
        {
            mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
        }

        private void Cleanup()
        {
            if (mainThreadRecorder.Valid)
            {
                mainThreadRecorder.Dispose();
            }

            if (gcAllocRecorder.Valid)
            {
                gcAllocRecorder.Dispose();
            }

            if (world != null)
            {
                world.Dispose();
                world = null;
            }

            if (profilerStateCaptured)
            {
                Time.timeScale = previousTimeScale;
            }

            current = null;
        }

        private static double Percentile(List<double> values, double percentile)
        {
            if (values.Count == 0)
            {
                return 0.0;
            }

            List<double> sortedValues = new List<double>(values);
            sortedValues.Sort();
            int index = Mathf.Clamp(
                Mathf.CeilToInt((float)(percentile * sortedValues.Count)) - 1,
                0,
                sortedValues.Count - 1);
            return sortedValues[index];
        }

        private void WriteProfileReport(
            bool valid,
            string report,
            int touchedCharacters,
            int pendingCharacters,
            int withActions,
            int tickedTrees,
            double avgFrameMs,
            double p95FrameMs,
            double maxFrameMs,
            double avgSchedulerMs,
            double p95SchedulerMs,
            double maxSchedulerMs,
            double avgGcAllocKb,
            double maxGcAllocKb,
            double monoDeltaMb)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProfileReportPath));
            string json =
                "{\n" +
                $"  \"valid\": {valid.ToString().ToLowerInvariant()},\n" +
                $"  \"npc\": {npcCount},\n" +
                $"  \"registered\": {(world.Scheduler != null ? world.Scheduler.RegisteredCharacterCount : 0)},\n" +
                $"  \"touched\": {touchedCharacters},\n" +
                $"  \"pending\": {pendingCharacters},\n" +
                $"  \"withActions\": {withActions},\n" +
                $"  \"tickedTrees\": {tickedTrees},\n" +
                $"  \"samples\": {samples},\n" +
                $"  \"warmupFrames\": {warmupSamples},\n" +
                $"  \"warmupWallMs\": {warmupStopwatch.Elapsed.TotalMilliseconds:0.###},\n" +
                $"  \"warmupCleanupMs\": {warmupCleanupMs:0.###},\n" +
                $"  \"creationMs\": {creationMs:0.###},\n" +
                $"  \"avgFrameMs\": {avgFrameMs:0.###},\n" +
                $"  \"p95FrameMs\": {p95FrameMs:0.###},\n" +
                $"  \"maxFrameMs\": {maxFrameMs:0.###},\n" +
                $"  \"avgSchedulerMs\": {avgSchedulerMs:0.###},\n" +
                $"  \"p95SchedulerMs\": {p95SchedulerMs:0.###},\n" +
                $"  \"maxSchedulerMs\": {maxSchedulerMs:0.###},\n" +
                $"  \"totalDecisions\": {totalDecisions},\n" +
                $"  \"maxDecisionsPerFrame\": {maxDecisions},\n" +
                $"  \"totalPathSearches\": {totalPathSearches},\n" +
                $"  \"maxPathSearchesPerFrame\": {maxPathSearches},\n" +
                $"  \"brokerPathSearches\": {totalBrokerPathSearches},\n" +
                $"  \"brokerCacheHits\": {totalBrokerPathCacheHits},\n" +
                $"  \"brokerBudgetDeferrals\": {totalBrokerPathBudgetDeferrals},\n" +
                $"  \"maxBrokerPathSearchesPerFrame\": {maxBrokerPathSearches},\n" +
                $"  \"maxBrokerCacheHitsPerFrame\": {maxBrokerPathCacheHits},\n" +
                $"  \"maxBrokerBudgetDeferralsPerFrame\": {maxBrokerPathBudgetDeferrals},\n" +
                $"  \"avgGcAllocKbPerFrame\": {avgGcAllocKb:0.###},\n" +
                $"  \"maxGcAllocKbPerFrame\": {maxGcAllocKb:0.###},\n" +
                $"  \"monoUsedDeltaMb\": {monoDeltaMb:0.###},\n" +
                $"  \"gen0Collections\": {GC.CollectionCount(0) - startGen0Collections},\n" +
                "  \"profilerLog\": \"\",\n" +
                $"  \"summary\": \"{EscapeJson(report)}\"\n" +
                "}\n";
            File.WriteAllText(ProfileReportPath, json);
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    private sealed class StressWorld : IDisposable
    {
        private static readonly FieldInfo GridSystemInstanceField =
            typeof(GridSystemManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo GridField =
            typeof(GridSystemManager).GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CharacterAwakeMethod =
            typeof(CharacterActor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GridSystemManager previousGridSystem;
        private readonly Grid previousGrid;
        private readonly ExternalBehaviorTree externalBehavior;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<ScriptableObject> scriptableObjects = new List<ScriptableObject>();

        public StressWorld()
        {
            previousGridSystem = GridSystemInstanceField?.GetValue(null) as GridSystemManager;
            previousGrid = previousGridSystem != null ? previousGridSystem.grid : null;
            Grid = new Grid(96, 3);
            for (int y = 0; y < Grid.height; y++)
            {
                for (int x = 0; x < Grid.width; x++)
                {
                    Grid.RegisterOccupant(
                        new TestHallwayOccupant(),
                        GridLayer.Hallway,
                        new List<Vector2Int> { new Vector2Int(x, y) },
                        false);
                }
            }

            RegisterStressStair(0);
            RegisterStressStair(Grid.width - 1);

            GridSystemManager manager = previousGridSystem;
            if (manager == null)
            {
                GameObject gridSystemObject = new GameObject("500 NPC Stress GridSystemManager");
                objects.Add(gridSystemObject);
                manager = gridSystemObject.AddComponent<GridSystemManager>();
            }

            GridField?.SetValue(manager, Grid);
            GridSystemInstanceField?.SetValue(null, manager);

            GameObject schedulerObject = new GameObject("500 NPC Stress CharacterAiScheduler");
            objects.Add(schedulerObject);
            externalBehavior = CharacterAiBehaviorDesignerGraphBuilder.EnsureCharacterAiExternalBehavior();
            Scheduler = schedulerObject.AddComponent<CharacterAiScheduler>();
            Scheduling = new FixedSchedulerService(Scheduler);
            SetPrivateField(Scheduler, "registerExistingSceneCharacters", false);
            CharacterAiEditorTestDependencies.Inject(Scheduler);
            Scheduler.ClearRegistrationsForDebug();
            SetPrivateField(Scheduler, "characterAiExternalBehavior", externalBehavior);
            SetPrivateField(Scheduler, "maxDecisionsPerFrame", DecisionBudget);
            SetPrivateField(Scheduler, "maxPathSearchesPerFrame", PathBudget);
            SetPrivateField(Scheduler, "visibleDecisionInterval", 0.01f);
            SetPrivateField(Scheduler, "offscreenDecisionInterval", 0.01f);
            SetPrivateField(Scheduler, "ownerDecisionInterval", 0.01f);
            SetPrivateField(Scheduler, "retryDelay", 0.01f);
        }

        public Grid Grid { get; }
        public CharacterAiScheduler Scheduler { get; }
        public ICharacterAiSchedulingService Scheduling { get; }
        public List<CharacterActor> Characters { get; } = new List<CharacterActor>();

        public void PlaceFacilities()
        {
            string[] assetNames =
            {
                "P1_LowFoodShop",
                "P1_MeatRestaurant",
                "P1_GeneralStore",
                "P1_RestRoom",
                "P1_ResearchLab",
                "P1_ManaStorage"
            };

            int[] nextPositionsByFloor = { 4, 8, 12 };
            for (int i = 0; i < 18; i++)
            {
                int floor = i % Grid.height;
                int x = nextPositionsByFloor[floor];
                Place(assetNames[i % assetNames.Length], new Vector2Int(x, floor));
                nextPositionsByFloor[floor] += 14;
            }
        }

        public void CreateCustomers(int count)
        {
            string[] species = { "Slime", "Orc", "Vampire" };
            for (int i = 0; i < count; i++)
            {
                CharacterActor character = CreateCustomer(
                    species[i % species.Length],
                    GetCustomerPosition(i),
                    20f + (i % 70),
                    20f + ((i * 3) % 70),
                    20f + ((i * 5) % 70),
                    20f + ((i * 7) % 70));
                Characters.Add(character);
                Scheduler.RegisterActor(character);
            }
        }

        private void RegisterStressStair(int x)
        {
            List<Vector2Int> positions = new List<Vector2Int>();
            for (int y = 0; y < Grid.height; y++)
            {
                positions.Add(new Vector2Int(x, y));
            }

            Grid.RegisterOccupant(new TestStairOccupant(), GridLayer.Building, positions, true);
        }

        private Vector2Int GetCustomerPosition(int index)
        {
            int floor = (index / Grid.width) % Grid.height;
            int x = index % Grid.width;
            if (x == 0)
            {
                x = 1;
            }
            else if (x == Grid.width - 1)
            {
                x = Grid.width - 2;
            }

            return new Vector2Int(x, floor);
        }

        public void Dispose()
        {
            if (previousGridSystem != null)
            {
                GridField?.SetValue(previousGridSystem, previousGrid);
            }

            GridSystemInstanceField?.SetValue(null, previousGridSystem);
            FacilityCandidateCache.Clear();

            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                DestroyRuntimeAware(obj);
            }

            foreach (ScriptableObject obj in scriptableObjects.Where((obj) => obj != null))
            {
                DestroyRuntimeAware(obj);
            }
        }

        private static void DestroyRuntimeAware(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }

        private BuildableObject Place(string assetName, Vector2Int position)
        {
            BuildingSO sourceData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                $"Assets/Resources/SO/Building/P1/{assetName}.asset");
            if (sourceData == null)
            {
                throw new InvalidOperationException($"{assetName} asset not found.");
            }

            BuildingSO buildingData = Object.Instantiate(sourceData);
            buildingData.hideFlags = HideFlags.HideAndDontSave;
            buildingData.AbilityModules.Remove<BuildingRoomRequirementAbility>();
            scriptableObjects.Add(buildingData);

            GridBuildingFactory factory = new GridBuildingFactory();
            BuildableObject building = factory.Create(Grid, buildingData, position);
            if (building == null)
            {
                throw new InvalidOperationException($"{assetName} could not be created.");
            }

            objects.Add(building.gameObject);
            CharacterAiEditorTestDependencies.Inject(building);
            building.SetGrid(Grid);
            building.Initialization(buildingData, position);
            bool registered = Grid.RegisterOccupant(
                building,
                buildingData.Placement.Layer,
                buildingData.GetGridPosList(position),
                buildingData.Placement.IsMovement);
            if (!registered)
            {
                throw new InvalidOperationException($"{assetName} could not be registered.");
            }

            return building;
        }

        private CharacterActor CreateCustomer(
            string speciesTag,
            Vector2Int position,
            float hunger,
            float sleep,
            float fun,
            float mood)
        {
            GameObject obj = new GameObject($"Stress Customer {speciesTag}");
            objects.Add(obj);
            obj.AddComponent<SpriteRenderer>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityShopping>();
            AIBrain brain = obj.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateCustomerActions();
            BehaviorTree behaviorTree = obj.AddComponent<BehaviorTree>();
            behaviorTree.StartWhenEnabled = false;
            behaviorTree.ExternalBehavior = externalBehavior;
            CharacterActor character = obj.AddComponent<CharacterActor>();
            CharacterAwakeMethod?.Invoke(character, null);
            CharacterAiEditorTestDependencies.Inject(obj, Scheduling);

            CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
            scriptableObjects.Add(data);
            SetPrivateField(data, "frequencyVisitMin", 3);
            SetPrivateField(data, "frequencyVisitMax", 3);
            SetPrivateField(data, "minHoldingMoney", 500);
            SetPrivateField(data, "maxHoldingMoney", 600);
            data.characterType = CharacterType.Customer;
            data.characterName = speciesTag;
            data.speciesTag = speciesTag;

            ApplyStressPersona(obj.GetComponent<CustomerPersonaRuntime>(), speciesTag);
            obj.transform.position = Grid.GetWorldPos(position);
            character.Initialization(data);
            character.SetLifecycleState(CharacterLifecycleState.Active);
            character.stats[CharacterCondition.HUNGER] = hunger;
            character.stats[CharacterCondition.SLEEP] = sleep;
            character.stats[CharacterCondition.FUN] = fun;
            character.stats[CharacterCondition.MOOD] = mood;
            return character;
        }

        private static void ApplyStressPersona(CustomerPersonaRuntime personaRuntime, string speciesTag)
        {
            if (personaRuntime == null)
            {
                return;
            }

            personaRuntime.ApplyGeneratedPersona(new CustomerPersonaData
            {
                traitName = $"Stress {speciesTag}",
                flavorText = "Deterministic stress-test persona.",
                selfCareMultiplier = 1f,
                curiosityMultiplier = 1f,
                shoppingMultiplier = 1f,
                patienceMultiplier = 1f,
                hungerCurveMultiplier = 1f,
                funCurveMultiplier = 1f,
                moodCurveMultiplier = 1f,
                preferredFacilityTags = Array.Empty<string>()
            });
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }

    private sealed class TestStairOccupant : IGridOccupant, IGridMovementOccupant
    {
        public int GridId => -1;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
        public GridMoveType GridMoveType => GridMoveType.Stair;
    }
}
