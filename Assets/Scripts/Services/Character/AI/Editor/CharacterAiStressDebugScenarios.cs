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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CharacterAiStressDebugScenarios
{
    private const int NpcCount = 500;
    private static readonly int[] ScaleNpcCounts = { 100, 300, NpcCount };
    private const int SimulationFrames = 180;
    private const int DecisionBudget = 16;
    private const int PathBudget = 8;
    private const double TargetFrameP95Milliseconds = 1000.0 / 60.0;
    private const double TargetSchedulerP95Milliseconds = 4.0;
    private const double TargetAverageGcKilobytesPerFrame = 64.0;
    private const int MaximumSynchronousNpcCount = 100;
    private const int PlayModeCreationBatchSize = 8;
    private const string PlayModeProfileRequestedKey = "DungeonStory.CharacterAiStress.PlayModeProfile.Requested";
    private const string PlayModeProfileNpcCountKey = "DungeonStory.CharacterAiStress.PlayModeProfile.NpcCount";
    private const string PlayModeProfileWarmupFramesKey = "DungeonStory.CharacterAiStress.PlayModeProfile.WarmupFrames";
    private const string PlayModeProfileSampleFramesKey = "DungeonStory.CharacterAiStress.PlayModeProfile.SampleFrames";
    private const string PlayModeProfileExitWhenDoneKey = "DungeonStory.CharacterAiStress.PlayModeProfile.ExitWhenDone";
    private const string PlayModeProfileReportKey = "DungeonStory.CharacterAiStress.PlayModeProfile.Report";
    private const string ProfileReportPath = "docs/implementation-reports/ai-play-mode-profile-latest.json";
    private const string StressGridWidthEnvironmentKey = "DUNGEON_AI_STRESS_GRID_WIDTH";
    private const string StressGridHeightEnvironmentKey = "DUNGEON_AI_STRESS_GRID_HEIGHT";
    private const string StressActiveFloorCountEnvironmentKey = "DUNGEON_AI_STRESS_ACTIVE_FLOORS";
    private const string StressFacilityCountEnvironmentKey = "DUNGEON_AI_STRESS_FACILITY_COUNT";
    private const string StressRoomSpanEnvironmentKey = "DUNGEON_AI_STRESS_ROOM_SPAN";
    private const string DenseGridAiProfileReportEnvironmentKey = "DUNGEON_AI_DENSE_REPORT";
    private const string LargeGridAiProfileReportPath =
        "docs/implementation-reports/navigation-large-ai-profile-latest.json";
    private const string DenseGridAiProfileReportPath =
        "docs/implementation-reports/navigation-dense-dungeon-profile-latest.json";

    [Serializable]
    private sealed class LargeGridAiProfileResult
    {
        public bool valid;
        public string measurementScope;
        public string utc;
        public string processor;
        public int processorCount;
        public int systemMemoryMb;
        public int gridWidth;
        public int gridHeight;
        public int cellCount;
        public int activeFloorCount;
        public int npcCount;
        public int requestedFacilityCount;
        public int initialFacilityCount;
        public int activeFacilityCount;
        public int doorCount;
        public int expectedRoomCount;
        public int roomCount;
        public int usableRoomCount;
        public int roomFurnitureCount;
        public int churnedFacilityCount;
        public int postChurnRoomCount;
        public int postChurnFurnitureCount;
        public int corridorPathLength;
        public int warmupTicks;
        public int sampleTicks;
        public double setupMs;
        public double facilityPlacementMs;
        public double roomScanMs;
        public double facilityChurnMs;
        public double postChurnRoomScanMs;
        public double corridorPathMs;
        public double averageTickMs;
        public double p95TickMs;
        public double maxTickMs;
        public double averageSchedulerMs;
        public double p95SchedulerMs;
        public double maxSchedulerMs;
        public double averageAllocatedKb;
        public double maxAllocatedKb;
        public int registered;
        public int tickedTrees;
        public int charactersWithActions;
        public int totalDecisions;
        public int maxDecisionsPerTick;
        public int totalPathSearches;
        public int maxPathSearchesPerTick;
        public int totalBrokerSearches;
        public int maxBrokerSearchesPerTick;
        public int totalUnboundedSearches;
        public int maxUnboundedSearchesPerTick;
        public int brokerCacheHits;
        public int brokerBudgetDeferrals;
        public CharacterAiPerformanceReport performance;
        public string failure;
    }

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
        public bool ShouldCollectDetailedDiagnostics(CharacterActor actor) => false;
        public int GetMovementFrameStride(CharacterActor actor) => scheduler.GetMovementFrameStrideFor(actor);
        public double GetDecisionWorkSliceMilliseconds(CharacterActor actor) =>
            scheduler.GetDecisionWorkSliceMillisecondsFor(actor);
        public void ResetPathSearchBudgetForDebug() => scheduler.ResetPathSearchBudgetForDebugInstance();
    }

    public static string LastReport { get; private set; } = string.Empty;
    public static string LastScaleReport { get; private set; } = string.Empty;
    public static string LastLargeGridReport { get; private set; } = string.Empty;
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
        if (npcCount > MaximumSynchronousNpcCount)
        {
            LastReport =
                $"synchronous stress is limited to {MaximumSynchronousNpcCount} NPCs; "
                + "use StartPlayModeProfile for frame-distributed 300/500 NPC validation.";
            UnityEngine.Debug.LogWarning(LastReport);
            return false;
        }

        using StressWorld world = new StressWorld();
        world.PlaceFacilities();
        world.CreateCustomers(npcCount);

        CharacterAiEditorTestDependencies.ResetPerformanceRecorder();
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
        CharacterAiPerformanceReport performanceReport =
            CharacterAiEditorTestDependencies.CapturePerformanceReport(npcCount);
        string performanceSummary = string.Join(
            ",",
            performanceReport.metrics
                .Where(metric => metric != null && metric.max > 0d)
                .Select(metric =>
                    $"{metric.name} n={metric.sampleCount} avg={metric.average:0.00} "
                    + $"p95={metric.p95:0.00} max={metric.max:0.00}ms"));

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
            && maxBrokerPathSearches <= PathBudget
            && totalDecisions > 0
            && totalBehaviorTreeTicks > 0
            && totalPathSearches + totalBrokerPathSearches > 0;

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
            $"perf=[{performanceSummary}], branches={branches}, samples={samples}, " +
            $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.0}";

        if (logSuccess || !valid)
        {
            UnityEngine.Debug.Log($"{npcCount} NPC AI stress: {LastReport}");
        }

        return valid;
    }

    public static bool RunConfiguredLargeGrid500Profile(bool logSuccess)
    {
        return RunConfiguredLargeGridProfile(
            NpcCount,
            logSuccess,
            facilityDense: false);
    }

    public static bool RunConfiguredDenseDungeon500Profile(bool logSuccess)
    {
        return RunConfiguredLargeGridProfile(
            NpcCount,
            logSuccess,
            facilityDense: true);
    }

    public static bool RunConfiguredLargeGridProfile(
        int npcCount,
        bool logSuccess,
        bool facilityDense)
    {
        npcCount = Mathf.Max(1, npcCount);
        const int warmupTicks = 180;
        const int sampleTicks = 600;
        LargeGridAiProfileResult result = new LargeGridAiProfileResult
        {
            measurementScope = facilityDense
                ? "real building objects, room scan, facility churn, scheduler-only forced 16 replans/tick; no movement or rendering"
                : "scheduler-only; forced 16 replans/tick; no movement, rendering, or presentation",
            utc = DateTime.UtcNow.ToString("O"),
            processor = SystemInfo.processorType,
            processorCount = SystemInfo.processorCount,
            systemMemoryMb = SystemInfo.systemMemorySize,
            npcCount = npcCount,
            warmupTicks = warmupTicks,
            sampleTicks = sampleTicks
        };

        try
        {
            Stopwatch setup = Stopwatch.StartNew();
            using StressWorld world = new StressWorld();
            if (facilityDense)
            {
                result.requestedFacilityCount = ReadEnvironmentInt(
                    StressFacilityCountEnvironmentKey,
                    8192,
                    1,
                    32768);
                int roomSpan = ReadEnvironmentInt(
                    StressRoomSpanEnvironmentKey,
                    16,
                    8,
                    64);
                result.expectedRoomCount =
                    world.ActiveFloorCount * Mathf.CeilToInt(world.Grid.width / (float)roomSpan);

                Stopwatch placement = Stopwatch.StartNew();
                world.PlaceDenseDungeon(result.requestedFacilityCount, roomSpan);
                placement.Stop();
                result.facilityPlacementMs = placement.Elapsed.TotalMilliseconds;
                result.initialFacilityCount = world.DenseFacilityCount;
                result.activeFacilityCount = world.DenseFacilityCount;
                result.doorCount = world.DenseDoorCount;

                Stopwatch roomScan = Stopwatch.StartNew();
                RoomLayout initialLayout = RoomDetector.Build(world.Grid);
                roomScan.Stop();
                result.roomScanMs = roomScan.Elapsed.TotalMilliseconds;
                result.roomCount = initialLayout.Rooms.Count;
                result.usableRoomCount = initialLayout.Rooms.Count(room => room.IsUsable);
                result.roomFurnitureCount = initialLayout.Rooms.Sum(room => room.Furniture.Count);

                Stopwatch corridor = Stopwatch.StartNew();
                Queue<GridMoveStep> corridorPath = world.Grid.GetMovePathTo(
                    new Vector2Int(1, 0),
                    new Vector2Int(world.Grid.width - 2, 0));
                corridor.Stop();
                result.corridorPathMs = corridor.Elapsed.TotalMilliseconds;
                result.corridorPathLength = corridorPath?.Count ?? 0;

                int churnTarget = Mathf.Min(512, Mathf.Max(1, result.initialFacilityCount / 8));
                Stopwatch churn = Stopwatch.StartNew();
                result.churnedFacilityCount = world.DestroyDenseFacilities(churnTarget);
                churn.Stop();
                result.facilityChurnMs = churn.Elapsed.TotalMilliseconds;
                result.activeFacilityCount =
                    result.initialFacilityCount - result.churnedFacilityCount;

                Stopwatch postChurnRoomScan = Stopwatch.StartNew();
                RoomLayout postChurnLayout = RoomDetector.Build(world.Grid);
                postChurnRoomScan.Stop();
                result.postChurnRoomScanMs = postChurnRoomScan.Elapsed.TotalMilliseconds;
                result.postChurnRoomCount = postChurnLayout.Rooms.Count;
                result.postChurnFurnitureCount =
                    postChurnLayout.Rooms.Sum(room => room.Furniture.Count);
            }
            else
            {
                world.PlaceFacilities();
            }

            world.CreateCustomers(npcCount);
            setup.Stop();

            result.setupMs = setup.Elapsed.TotalMilliseconds;
            result.gridWidth = world.Grid.width;
            result.gridHeight = world.Grid.height;
            result.cellCount = checked(world.Grid.width * world.Grid.height);
            result.activeFloorCount = world.ActiveFloorCount;

            const int maximumBootstrapTicks = 2000;
            for (int bootstrapTick = 0;
                 bootstrapTick < maximumBootstrapTicks
                 && world.Characters.Any(character =>
                     character != null
                     && character.BehaviorTree != null
                     && character.BehaviorTree.DungeonStoryTickCount == 0);
                 bootstrapTick++)
            {
                world.Scheduler.RunManualTick(1f / 60f);
            }

            for (int tick = 0; tick < warmupTicks; tick++)
            {
                world.Scheduler.RunManualTick(1f / 60f);
                result.totalUnboundedSearches +=
                    world.Scheduler.LastBrokerUnboundedPathSearchCount;
                result.maxUnboundedSearchesPerTick = Mathf.Max(
                    result.maxUnboundedSearchesPerTick,
                    world.Scheduler.LastBrokerUnboundedPathSearchCount);
            }
            UnityEngine.Debug.Log(
                $"Large-grid warmup completed: unboundedSearches="
                + $"{result.totalUnboundedSearches}, "
                + $"maxUnbounded/tick={result.maxUnboundedSearchesPerTick}");

            CharacterAiEditorTestDependencies.ResetPerformanceRecorder(
                detailedCollectionEnabled: true);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            List<double> tickTimes = new List<double>(sampleTicks);
            List<double> schedulerTimes = new List<double>(sampleTicks);
            long totalAllocatedBytes = 0L;
            long maxAllocatedBytes = 0L;
            int allocatedSamples = 0;

            for (int tick = 0; tick < sampleTicks; tick++)
            {
                ForceDecisionBatch(world, tick);
                long started = Stopwatch.GetTimestamp();
                world.Scheduler.RunManualTick(1f / 60f);
                tickTimes.Add(
                    (Stopwatch.GetTimestamp() - started)
                    * 1000.0
                    / Stopwatch.Frequency);
                schedulerTimes.Add(world.Scheduler.LastProcessingMilliseconds);

                result.totalDecisions += world.Scheduler.LastProcessedDecisionCount;
                result.maxDecisionsPerTick = Mathf.Max(
                    result.maxDecisionsPerTick,
                    world.Scheduler.LastProcessedDecisionCount);
                result.totalPathSearches += world.Scheduler.LastPathSearchCount;
                result.maxPathSearchesPerTick = Mathf.Max(
                    result.maxPathSearchesPerTick,
                    world.Scheduler.LastPathSearchCount);
                result.totalBrokerSearches +=
                    world.Scheduler.LastBrokerPathSearchCount;
                result.maxBrokerSearchesPerTick = Mathf.Max(
                    result.maxBrokerSearchesPerTick,
                    world.Scheduler.LastBrokerPathSearchCount);
                result.totalUnboundedSearches +=
                    world.Scheduler.LastBrokerUnboundedPathSearchCount;
                result.maxUnboundedSearchesPerTick = Mathf.Max(
                    result.maxUnboundedSearchesPerTick,
                    world.Scheduler.LastBrokerUnboundedPathSearchCount);
                result.brokerCacheHits +=
                    world.Scheduler.LastBrokerPathCacheHitCount;
                result.brokerBudgetDeferrals +=
                    world.Scheduler.LastBrokerPathBudgetDeferralCount;

                if (world.Scheduler.LastAllocatedBytes >= 0L)
                {
                    allocatedSamples++;
                    totalAllocatedBytes += world.Scheduler.LastAllocatedBytes;
                    maxAllocatedBytes = Math.Max(
                        maxAllocatedBytes,
                        world.Scheduler.LastAllocatedBytes);
                }
            }

            tickTimes.Sort();
            schedulerTimes.Sort();
            result.averageTickMs = Average(tickTimes);
            result.p95TickMs = Percentile(tickTimes, 0.95);
            result.maxTickMs = tickTimes[tickTimes.Count - 1];
            result.averageSchedulerMs = Average(schedulerTimes);
            result.p95SchedulerMs = Percentile(schedulerTimes, 0.95);
            result.maxSchedulerMs = schedulerTimes[schedulerTimes.Count - 1];
            result.averageAllocatedKb = allocatedSamples > 0
                ? totalAllocatedBytes / 1024.0 / allocatedSamples
                : -1.0;
            result.maxAllocatedKb = allocatedSamples > 0
                ? maxAllocatedBytes / 1024.0
                : -1.0;
            result.registered = world.Scheduler.RegisteredCharacterCount;
            result.tickedTrees = world.Characters.Count(character =>
                character != null
                && character.BehaviorTree != null
                && character.BehaviorTree.DungeonStoryTickCount > 0);
            result.charactersWithActions = world.Characters.Count(character =>
                character != null
                && character.ai != null
                && character.ai.availableActions != null
                && character.ai.availableActions.Length > 0);
            result.performance =
                CharacterAiEditorTestDependencies.CapturePerformanceReport(npcCount);

            result.valid = result.gridWidth == 1024
                && result.gridHeight == 1024
                && result.registered == npcCount
                && result.tickedTrees == npcCount
                && result.charactersWithActions == npcCount
                && result.totalDecisions > 0
                && result.totalPathSearches + result.totalBrokerSearches > 0
                && result.totalUnboundedSearches == 0
                && result.maxDecisionsPerTick <= DecisionBudget
                && result.maxPathSearchesPerTick <= PathBudget
                && result.maxBrokerSearchesPerTick <= PathBudget
                && result.p95SchedulerMs <= TargetSchedulerP95Milliseconds
                && (result.averageAllocatedKb < 0
                    || result.averageAllocatedKb
                        <= TargetAverageGcKilobytesPerFrame);
            if (facilityDense)
            {
                result.valid = result.valid
                    && result.initialFacilityCount == result.requestedFacilityCount
                    && result.activeFacilityCount
                        == result.initialFacilityCount - result.churnedFacilityCount
                    && result.doorCount > 0
                    && result.roomCount == result.expectedRoomCount
                    && result.usableRoomCount == result.expectedRoomCount
                    && result.roomFurnitureCount == result.initialFacilityCount
                    && result.postChurnRoomCount == result.roomCount
                    && result.postChurnFurnitureCount == result.activeFacilityCount
                    && result.corridorPathLength == result.gridWidth - 3;
            }
        }
        catch (Exception exception)
        {
            result.valid = false;
            result.failure = exception.ToString();
            UnityEngine.Debug.LogException(exception);
        }
        finally
        {
            Grid.ReleaseRetainedSearchMemoryForDiagnostics();
            GC.Collect();
        }

        LastLargeGridReport = JsonUtility.ToJson(result, true);
        string reportPath = facilityDense
            ? Environment.GetEnvironmentVariable(DenseGridAiProfileReportEnvironmentKey)
            : LargeGridAiProfileReportPath;
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            reportPath = DenseGridAiProfileReportPath;
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(reportPath)
            ?? Directory.GetCurrentDirectory());
        File.WriteAllText(reportPath, LastLargeGridReport);
        if (logSuccess || !result.valid)
        {
            UnityEngine.Debug.Log(
                $"1024x1024 {npcCount} NPC "
                + $"{(facilityDense ? "dense dungeon" : "scheduler")} profile "
                + $"valid={result.valid}\n"
                + LastLargeGridReport);
        }

        return result.valid;
    }

    private static void ForceDecisionBatch(StressWorld world, int tick)
    {
        if (world == null || world.Characters.Count == 0)
        {
            return;
        }

        int baseIndex = tick * DecisionBudget;
        for (int offset = 0; offset < DecisionBudget; offset++)
        {
            CharacterActor actor =
                world.Characters[(baseIndex + offset) % world.Characters.Count];
            if (actor == null)
            {
                continue;
            }

            actor.Brain?.RequestImmediateReplan(clearFailures: true);
            world.Scheduler.RequestImmediateDecisionFor(actor);
        }
    }

    private static int ReadEnvironmentInt(
        string key,
        int fallback,
        int minimum,
        int maximum)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(key), out int parsed)
            ? Mathf.Clamp(parsed, minimum, maximum)
            : Mathf.Clamp(fallback, minimum, maximum);
    }

    private static double Average(IReadOnlyList<double> values)
    {
        if (values == null || values.Count == 0)
        {
            return 0.0;
        }

        double total = 0.0;
        for (int index = 0; index < values.Count; index++)
        {
            total += values[index];
        }

        return total / values.Count;
    }

    private static double Percentile(
        IReadOnlyList<double> sortedValues,
        double percentile)
    {
        if (sortedValues == null || sortedValues.Count == 0)
        {
            return 0.0;
        }

        int index = Mathf.Clamp(
            Mathf.CeilToInt((float)(sortedValues.Count * percentile)) - 1,
            0,
            sortedValues.Count - 1);
        return sortedValues[index];
    }

    private sealed class PlayModeProfileSession
    {
        private const int GcBaselineFrameCount = 30;
        private static PlayModeProfileSession current;

        private readonly int npcCount;
        private readonly int warmupFrames;
        private readonly int sampleFrames;
        private readonly bool exitWhenDone;
        private readonly List<double> frameTimesMs;
        private readonly List<double> schedulerTimesMs;
        private readonly List<GameObject> disabledSceneRoots = new List<GameObject>();
        private readonly Stopwatch sampleStopwatch = new Stopwatch();
        private readonly Stopwatch warmupStopwatch = new Stopwatch();

        private StressWorld world;
        private Scene previousScene;
        private Scene profileScene;
        private ProfilerRecorder mainThreadRecorder;
        private ProfilerRecorder gcAllocRecorder;
        private bool profilerStateCaptured;
        private bool completed;
        private bool samplingStarted;
        private bool previousEditorPaused;
        private bool previousRunInBackground;
        private float previousTimeScale;
        private int lastFrame = -1;
        private int creationFrames;
        private int warmupSamples;
        private int samples;
        private int gcBaselineFramesRemaining;
        private int gcBaselineSamples;
        private int stabilizationFramesRemaining;
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
        private long totalSchedulerAllocatedBytes;
        private long maxSchedulerAllocatedBytes;
        private int schedulerGcSamples;
        private long startMonoUsedBytes;
        private long endMonoUsedBytes;
        private long totalGcBaselineBytes;
        private double gcBaselineBytesPerFrame;
        private int startGen0Collections;
        private double creationMs;
        private double maxCreationFrameMs;
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
            frameTimesMs = new List<double>(this.sampleFrames);
            schedulerTimesMs = new List<double>(this.sampleFrames);
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

            EditorApplication.QueuePlayerLoopUpdate();
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
            previousEditorPaused = EditorApplication.isPaused;
            EditorApplication.isPaused = false;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            IsolateProfileScene();

            CharacterAiEditorTestDependencies.ResetPerformanceRecorder(
                detailedCollectionEnabled: sampleFrames <= 120);
            world = new StressWorld();
            world.PlaceFacilities();
            world.SetSchedulerEnabled(false);

            UnityEngine.Debug.Log(
                $"500 NPC Play Mode profile staging started: npc={npcCount}, "
                + $"batch={PlayModeCreationBatchSize}, warmupFrames={warmupFrames}, "
                + $"sampleFrames={sampleFrames}");
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
            if (world.Characters.Count < npcCount)
            {
                Stopwatch batchStopwatch = Stopwatch.StartNew();
                world.CreateCustomersBatch(npcCount, PlayModeCreationBatchSize);
                batchStopwatch.Stop();
                double batchMilliseconds = batchStopwatch.Elapsed.TotalMilliseconds;
                creationMs += batchMilliseconds;
                maxCreationFrameMs = Math.Max(maxCreationFrameMs, batchMilliseconds);
                creationFrames++;
                if (world.Characters.Count < npcCount
                    && world.Characters.Count % 64 == 0)
                {
                    UnityEngine.Debug.Log(
                        $"500 NPC Play Mode profile staging progress: "
                        + $"{world.Characters.Count}/{npcCount}");
                }

                if (world.Characters.Count >= npcCount)
                {
                    world.SetSchedulerEnabled(true);
                    StartRecorders();
                    warmupStopwatch.Restart();
                    UnityEngine.Debug.Log(
                        $"500 NPC Play Mode profile staging completed: npc={npcCount}, "
                        + $"frames={creationFrames}, totalMs={creationMs:0.0}, "
                        + $"maxBatchMs={maxCreationFrameMs:0.0}");
                }

                return;
            }

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
                world.SetSchedulerEnabled(false);
                gcBaselineFramesRemaining = GcBaselineFrameCount;
                return;
            }

            if (gcBaselineFramesRemaining > 0)
            {
                if (gcAllocRecorder.Valid)
                {
                    totalGcBaselineBytes += Math.Max(
                        0L,
                        gcAllocRecorder.LastValue);
                    gcBaselineSamples++;
                }

                gcBaselineFramesRemaining--;
                if (gcBaselineFramesRemaining == 0)
                {
                    gcBaselineBytesPerFrame = gcBaselineSamples > 0
                        ? (double)totalGcBaselineBytes / gcBaselineSamples
                        : 0d;
                    world.SetSchedulerEnabled(true);
                    stabilizationFramesRemaining = 2;
                }

                return;
            }

            if (stabilizationFramesRemaining > 0)
            {
                stabilizationFramesRemaining--;
                if (stabilizationFramesRemaining == 0)
                {
                    sampleStopwatch.Restart();
                }

                return;
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
            long schedulerAllocatedBytes = scheduler?.LastAllocatedBytes ?? -1L;
            if (schedulerAllocatedBytes >= 0L)
            {
                schedulerGcSamples++;
                totalSchedulerAllocatedBytes += schedulerAllocatedBytes;
                maxSchedulerAllocatedBytes = Math.Max(
                    maxSchedulerAllocatedBytes,
                    schedulerAllocatedBytes);
            }

            if (mainThreadNs > 0)
            {
                double mainThreadMs = mainThreadNs / 1000000.0;
                mainThreadSamples++;
                totalMainThreadMs += mainThreadMs;
                maxMainThreadMs = Math.Max(maxMainThreadMs, mainThreadMs);
            }

            long gameplayGcAllocBytes = Math.Max(
                0L,
                gcAllocBytes - (long)gcBaselineBytesPerFrame);
            totalGcAllocBytes += gameplayGcAllocBytes;
            maxGcAllocBytes = Math.Max(
                maxGcAllocBytes,
                gameplayGcAllocBytes);

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
            bool schedulerGcCounterSupported = schedulerGcSamples > 0;
            double avgSchedulerGcAllocKb = schedulerGcCounterSupported
                ? totalSchedulerAllocatedBytes / 1024.0 / schedulerGcSamples
                : -1.0;
            double maxSchedulerGcAllocKb = schedulerGcCounterSupported
                ? maxSchedulerAllocatedBytes / 1024.0
                : -1.0;
            double monoDeltaMb = (endMonoUsedBytes - startMonoUsedBytes) / 1024.0 / 1024.0;
            double p95FrameMs = Percentile(frameTimesMs, 0.95);
            double p95SchedulerMs = Percentile(schedulerTimesMs, 0.95);
            bool behaviorValid = scheduler != null
                && scheduler.RegisteredCharacterCount == npcCount
                && touchedCharacters > 0
                && tickedTrees == npcCount
                && withActions == npcCount
                && maxDecisions <= DecisionBudget
                && maxPathSearches <= PathBudget
                && maxBrokerPathSearches <= PathBudget
                && totalDecisions > 0
                && totalPathSearches + totalBrokerPathSearches > 0;
            bool performanceValid = p95FrameMs <= TargetFrameP95Milliseconds
                && p95SchedulerMs <= TargetSchedulerP95Milliseconds
                && avgGcAllocKb <= TargetAverageGcKilobytesPerFrame;
            bool valid = behaviorValid && performanceValid;
            CharacterAiPerformanceReport detailedPerformance =
                CharacterAiEditorTestDependencies.CapturePerformanceReport(npcCount);
            string detailedPerformanceSummary = string.Join(
                ",",
                detailedPerformance.metrics
                    .Where(metric => metric != null && metric.sampleCount > 0)
                    .Select(metric =>
                        $"{metric.name} n={metric.sampleCount} avg={metric.average:0.00} "
                        + $"p95={metric.p95:0.00} max={metric.max:0.00}ms"));

            string report =
                $"valid={valid}, behaviorValid={behaviorValid}, performanceValid={performanceValid}, "
                + $"grid={world.Grid.width}x{world.Grid.height}, "
                + $"npc={npcCount}, registered={(scheduler != null ? scheduler.RegisteredCharacterCount : 0)}, " +
                $"active={touchedCharacters}, pending={pendingCharacters}, withActions={withActions}, tickedTrees={tickedTrees}, " +
                $"warmupFrames={warmupSamples}, warmupWallMs={warmupStopwatch.Elapsed.TotalMilliseconds:0.0}, warmupCleanupMs={warmupCleanupMs:0.0}, " +
                $"samples={samples}, sampleWallMs={sampleStopwatch.Elapsed.TotalMilliseconds:0.0}, "
                + $"creationFrames={creationFrames}, creationMs={creationMs:0.0}, maxCreationFrameMs={maxCreationFrameMs:0.0}, " +
                $"avgFrameMs={avgDeltaMs:0.00}, p95FrameMs={p95FrameMs:0.00}, maxFrameMs={maxDeltaMs:0.00}, " +
                $"frames>16.7ms={framesOver16Ms}, frames>33.3ms={framesOver33Ms}, " +
                $"avgMainThreadMs={avgMainThreadMs:0.00}, maxMainThreadMs={maxMainThreadMs:0.00}, mainThreadSamples={mainThreadSamples}, " +
                $"avgSchedulerMs={avgSchedulerMs:0.000}, p95SchedulerMs={p95SchedulerMs:0.000}, maxSchedulerMs={maxSchedulerMs:0.000}, " +
                $"totalDecisions={totalDecisions}, maxDecisions/frame={maxDecisions}, " +
                $"totalPathSearches={totalPathSearches}, maxPathSearches/frame={maxPathSearches}, " +
                $"brokerPathSearches={totalBrokerPathSearches}, brokerCacheHits={totalBrokerPathCacheHits}, " +
                $"brokerBudgetDeferrals={totalBrokerPathBudgetDeferrals}, maxBrokerPathSearches/frame={maxBrokerPathSearches}, " +
                $"maxBrokerCacheHits/frame={maxBrokerPathCacheHits}, maxBrokerBudgetDeferrals/frame={maxBrokerPathBudgetDeferrals}, " +
                $"avgGcAllocKB/frame={avgGcAllocKb:0.0}, maxGcAllocKB/frame={maxGcAllocKb:0.0}, " +
                $"editorBaselineGcKB/frame={gcBaselineBytesPerFrame / 1024.0:0.0}, " +
                $"schedulerGcCounterSupported={schedulerGcCounterSupported}, " +
                $"avgSchedulerGcAllocKB/frame={avgSchedulerGcAllocKb:0.0}, maxSchedulerGcAllocKB/frame={maxSchedulerGcAllocKb:0.0}, " +
                $"monoUsedDeltaMB={monoDeltaMb:0.00}, gen0Collections={GC.CollectionCount(0) - startGen0Collections}, "
                + $"perf=[{detailedPerformanceSummary}]";

            SessionState.SetString(PlayModeProfileReportKey, report);
            WriteProfileReport(
                valid,
                report,
                touchedCharacters,
                pendingCharacters,
                withActions,
                tickedTrees,
                behaviorValid,
                performanceValid,
                avgDeltaMs,
                p95FrameMs,
                maxDeltaMs,
                avgSchedulerMs,
                p95SchedulerMs,
                maxSchedulerMs,
                avgGcAllocKb,
                maxGcAllocKb,
                schedulerGcCounterSupported,
                avgSchedulerGcAllocKb,
                maxSchedulerGcAllocKb,
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

            RestoreProfileScene();

            if (profilerStateCaptured)
            {
                EditorApplication.isPaused = previousEditorPaused;
                Application.runInBackground = previousRunInBackground;
                Time.timeScale = previousTimeScale;
            }

            current = null;
        }

        private void IsolateProfileScene()
        {
            previousScene = SceneManager.GetActiveScene();
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                foreach (GameObject root in previousScene.GetRootGameObjects())
                {
                    if (root != null && root.activeSelf)
                    {
                        root.SetActive(false);
                        disabledSceneRoots.Add(root);
                    }
                }
            }

            profileScene = SceneManager.CreateScene("CharacterAiStressProfileRuntime");
            SceneManager.SetActiveScene(profileScene);
        }

        private void RestoreProfileScene()
        {
            if (previousScene.IsValid() && previousScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousScene);
            }

            foreach (GameObject root in disabledSceneRoots)
            {
                if (root != null)
                {
                    root.SetActive(true);
                }
            }

            disabledSceneRoots.Clear();
            if (profileScene.IsValid() && profileScene.isLoaded)
            {
                SceneManager.UnloadSceneAsync(profileScene);
            }

            profileScene = default;
            previousScene = default;
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
            bool behaviorValid,
            bool performanceValid,
            double avgFrameMs,
            double p95FrameMs,
            double maxFrameMs,
            double avgSchedulerMs,
            double p95SchedulerMs,
            double maxSchedulerMs,
            double avgGcAllocKb,
            double maxGcAllocKb,
            bool schedulerGcCounterSupported,
            double avgSchedulerGcAllocKb,
            double maxSchedulerGcAllocKb,
            double monoDeltaMb)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProfileReportPath));
            string json =
                "{\n" +
                $"  \"valid\": {valid.ToString().ToLowerInvariant()},\n" +
                $"  \"behaviorValid\": {behaviorValid.ToString().ToLowerInvariant()},\n" +
                $"  \"performanceValid\": {performanceValid.ToString().ToLowerInvariant()},\n" +
                $"  \"npc\": {npcCount},\n" +
                $"  \"gridWidth\": {world.Grid.width},\n" +
                $"  \"gridHeight\": {world.Grid.height},\n" +
                $"  \"targetFrameP95Ms\": {TargetFrameP95Milliseconds:0.###},\n" +
                $"  \"targetSchedulerP95Ms\": {TargetSchedulerP95Milliseconds:0.###},\n" +
                $"  \"targetAverageGcKbPerFrame\": {TargetAverageGcKilobytesPerFrame:0.###},\n" +
                $"  \"processor\": \"{EscapeJson(SystemInfo.processorType)}\",\n" +
                $"  \"processorCount\": {SystemInfo.processorCount},\n" +
                $"  \"systemMemoryMb\": {SystemInfo.systemMemorySize},\n" +
                $"  \"graphicsDevice\": \"{EscapeJson(SystemInfo.graphicsDeviceName)}\",\n" +
                $"  \"registered\": {(world.Scheduler != null ? world.Scheduler.RegisteredCharacterCount : 0)},\n" +
                $"  \"touched\": {touchedCharacters},\n" +
                $"  \"pending\": {pendingCharacters},\n" +
                $"  \"withActions\": {withActions},\n" +
                $"  \"tickedTrees\": {tickedTrees},\n" +
                $"  \"samples\": {samples},\n" +
                $"  \"warmupFrames\": {warmupSamples},\n" +
                $"  \"warmupWallMs\": {warmupStopwatch.Elapsed.TotalMilliseconds:0.###},\n" +
                $"  \"warmupCleanupMs\": {warmupCleanupMs:0.###},\n" +
                $"  \"creationFrames\": {creationFrames},\n" +
                $"  \"creationMs\": {creationMs:0.###},\n" +
                $"  \"maxCreationFrameMs\": {maxCreationFrameMs:0.###},\n" +
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
                $"  \"schedulerGcCounterSupported\": {schedulerGcCounterSupported.ToString().ToLowerInvariant()},\n" +
                $"  \"avgSchedulerGcAllocKbPerFrame\": {avgSchedulerGcAllocKb:0.###},\n" +
                $"  \"maxSchedulerGcAllocKbPerFrame\": {maxSchedulerGcAllocKb:0.###},\n" +
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
        private readonly Dictionary<string, BuildingSO> buildingDataByAssetPath =
            new Dictionary<string, BuildingSO>(StringComparer.Ordinal);
        private readonly List<BuildableObject> denseFacilities = new List<BuildableObject>();
        private readonly GridBuildingFactory buildingFactory = new GridBuildingFactory();
        private readonly int activeFloorCount;
        private readonly bool usesLargeGridLayout;

        public StressWorld()
        {
            previousGridSystem = GridSystemInstanceField?.GetValue(null) as GridSystemManager;
            previousGrid = previousGridSystem != null ? previousGridSystem.grid : null;
            int gridWidth = ReadEnvironmentInt(
                StressGridWidthEnvironmentKey,
                96,
                8,
                1024);
            int gridHeight = ReadEnvironmentInt(
                StressGridHeightEnvironmentKey,
                3,
                1,
                1024);
            activeFloorCount = ReadEnvironmentInt(
                StressActiveFloorCountEnvironmentKey,
                Mathf.Min(3, gridHeight),
                1,
                gridHeight);
            usesLargeGridLayout = gridWidth > 96 || gridHeight > 3;
            Grid = new Grid(gridWidth, gridHeight);
            for (int y = 0; y < Grid.height; y++)
            {
                for (int x = 0; x < Grid.width; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    if (y < activeFloorCount)
                    {
                        Grid.RegisterOccupant(
                            new TestHallwayOccupant(),
                            GridLayer.Hallway,
                            new List<Vector2Int> { position },
                            false);
                    }
                    else
                    {
                        Grid.GetGridCell(position)
                            .SetAreaType(GridCellAreaType.ExteriorPath);
                    }
                }
            }

            if (usesLargeGridLayout)
            {
                RegisterStressTraversalColumn(0);
                RegisterStressTraversalColumn(Grid.width - 1);
                Grid.RefreshTraversalHeuristicMetadata();
            }
            else
            {
                RegisterStressStair(0);
                RegisterStressStair(Grid.width - 1);
            }

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
        public int ActiveFloorCount => activeFloorCount;
        public int DenseFacilityCount => denseFacilities.Count(building =>
            building != null && !building.isDestroy);
        public int DenseDoorCount { get; private set; }

        public void SetSchedulerEnabled(bool enabled)
        {
            Scheduler.enabled = enabled;
        }

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

            int[] nextPositionsByFloor = new int[activeFloorCount];
            int firstX = usesLargeGridLayout
                ? Mathf.Max(4, Grid.width / 16)
                : 4;
            int spacing = usesLargeGridLayout
                ? Mathf.Max(14, Grid.width / 8)
                : 14;
            for (int floor = 0; floor < nextPositionsByFloor.Length; floor++)
            {
                nextPositionsByFloor[floor] = firstX + floor * 4;
            }

            for (int i = 0; i < 18; i++)
            {
                int floor = i % activeFloorCount;
                int x = nextPositionsByFloor[floor];
                Place(assetNames[i % assetNames.Length], new Vector2Int(x, floor));
                nextPositionsByFloor[floor] += spacing;
            }
        }

        public void PlaceDenseDungeon(int targetFacilityCount, int roomSpan)
        {
            string[] facilityAssetPaths =
            {
                "Assets/Resources/SO/Building/Modular/Q01_연구책상.asset",
                "Assets/Resources/SO/Building/Modular/Q02_연금술작업대.asset",
                "Assets/Resources/SO/Building/Modular/D01_간이화덕.asset",
                "Assets/Resources/SO/Building/Modular/D02_고기그릴.asset",
                "Assets/Resources/SO/Building/Modular/R01_간이침대.asset",
                "Assets/Resources/SO/Building/Modular/S01_판매카운터.asset",
                "Assets/Resources/SO/Building/Modular/L01_대형보관선반.asset",
                "Assets/Resources/SO/Building/Modular/G01_경비초소책상.asset"
            };
            const string doorAssetPath =
                "Assets/Resources/SO/Building/InteriorDoor.asset";
            int[] slotOffsets = { 2, 6, 10, 13 };

            for (int floor = 0; floor < activeFloorCount; floor++)
            {
                for (int boundaryX = roomSpan;
                     boundaryX < Grid.width;
                     boundaryX += roomSpan)
                {
                    PlaceAsset(
                        doorAssetPath,
                        new Vector2Int(boundaryX, floor),
                        removeRoomRequirement: false,
                        trackDenseFacility: false);
                    DenseDoorCount++;
                }
            }

            List<Vector2Int> facilitySlots = new List<Vector2Int>();
            for (int floor = 0; floor < activeFloorCount; floor++)
            {
                for (int roomStart = 0;
                     roomStart < Grid.width;
                     roomStart += roomSpan)
                {
                    foreach (int slotOffset in slotOffsets)
                    {
                        int x = roomStart + slotOffset;
                        if (x + 1 >= Mathf.Min(Grid.width, roomStart + roomSpan))
                        {
                            continue;
                        }

                        facilitySlots.Add(new Vector2Int(x, floor));
                    }
                }
            }

            if (facilitySlots.Count < targetFacilityCount)
            {
                throw new InvalidOperationException(
                    $"Dense dungeon capacity exhausted: requested={targetFacilityCount}, "
                    + $"capacity={facilitySlots.Count}, floors={activeFloorCount}, "
                    + $"width={Grid.width}, roomSpan={roomSpan}.");
            }

            for (int placedFacilities = 0;
                 placedFacilities < targetFacilityCount;
                 placedFacilities++)
            {
                int slotIndex = (int)(
                    (long)placedFacilities
                    * facilitySlots.Count
                    / targetFacilityCount);
                string assetPath =
                    facilityAssetPaths[placedFacilities % facilityAssetPaths.Length];
                PlaceAsset(
                    assetPath,
                    facilitySlots[slotIndex],
                    removeRoomRequirement: true,
                    trackDenseFacility: true);
            }
        }

        public int DestroyDenseFacilities(int count)
        {
            int destroyed = 0;
            for (int i = 0; i < denseFacilities.Count && destroyed < count; i++)
            {
                BuildableObject building = denseFacilities[i];
                if (building == null || building.isDestroy)
                {
                    continue;
                }

                building.DestroySelf();
                destroyed++;
            }

            FacilityCandidateCache.Clear();
            return destroyed;
        }

        public void CreateCustomers(int count)
        {
            while (Characters.Count < count)
            {
                CreateCustomersBatch(count, count);
            }
        }

        public int CreateCustomersBatch(int totalCount, int maximumBatchSize)
        {
            string[] species = { "Slime", "Orc", "Vampire" };
            int start = Characters.Count;
            int end = Mathf.Min(
                Mathf.Max(start, totalCount),
                start + Mathf.Max(1, maximumBatchSize));
            for (int i = start; i < end; i++)
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

            return end - start;
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

        private void RegisterStressTraversalColumn(int x)
        {
            for (int y = 0; y < Grid.height; y++)
            {
                List<GridTraversalLink> links = new List<GridTraversalLink>(2);
                if (y > 0)
                {
                    links.Add(new GridTraversalLink(
                        new Vector2Int(x, y - 1),
                        null,
                        GridMoveType.Stair));
                }

                if (y + 1 < Grid.height)
                {
                    links.Add(new GridTraversalLink(
                        new Vector2Int(x, y + 1),
                        null,
                        GridMoveType.Stair));
                }

                Grid.GetGridCell(new Vector2Int(x, y)).SetTraversalLinks(links);
            }
        }

        private Vector2Int GetCustomerPosition(int index)
        {
            if (usesLargeGridLayout)
            {
                int availableWidth = Mathf.Max(1, Grid.width - 2);
                int distributedX = 1 + ((index * 37) % availableWidth);
                int distributedFloor = index % activeFloorCount;
                return new Vector2Int(distributedX, distributedFloor);
            }

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

        private static int ReadEnvironmentInt(
            string key,
            int fallback,
            int minimum,
            int maximum)
        {
            return int.TryParse(Environment.GetEnvironmentVariable(key), out int parsed)
                ? Mathf.Clamp(parsed, minimum, maximum)
                : Mathf.Clamp(fallback, minimum, maximum);
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
            return PlaceAsset(
                $"Assets/Resources/SO/Building/P1/{assetName}.asset",
                position,
                removeRoomRequirement: true,
                trackDenseFacility: false);
        }

        private BuildableObject PlaceAsset(
            string assetPath,
            Vector2Int position,
            bool removeRoomRequirement,
            bool trackDenseFacility)
        {
            BuildingSO buildingData = GetStressBuildingData(
                assetPath,
                removeRoomRequirement);
            BuildableObject building = buildingFactory.Create(Grid, buildingData, position);
            if (building == null)
            {
                throw new InvalidOperationException(
                    $"{assetPath} could not be created at {position}.");
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
                throw new InvalidOperationException(
                    $"{assetPath} could not be registered at {position}.");
            }

            if (trackDenseFacility)
            {
                denseFacilities.Add(building);
            }

            return building;
        }

        private BuildingSO GetStressBuildingData(
            string assetPath,
            bool removeRoomRequirement)
        {
            string cacheKey = $"{assetPath}|roomRequirement={!removeRoomRequirement}";
            if (buildingDataByAssetPath.TryGetValue(
                    cacheKey,
                    out BuildingSO cachedData))
            {
                return cachedData;
            }

            BuildingSO sourceData = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
            if (sourceData == null)
            {
                throw new InvalidOperationException($"{assetPath} asset not found.");
            }

            BuildingSO buildingData = Object.Instantiate(sourceData);
            buildingData.hideFlags = HideFlags.HideAndDontSave;
            if (removeRoomRequirement)
            {
                buildingData.AbilityModules.Remove<BuildingRoomRequirementAbility>();
            }

            scriptableObjects.Add(buildingData);
            buildingDataByAssetPath[cacheKey] = buildingData;
            return buildingData;
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
