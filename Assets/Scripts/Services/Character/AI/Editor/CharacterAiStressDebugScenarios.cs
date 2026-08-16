using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BehaviorDesigner.Runtime;
using DungeonStory.Foundation;
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
    private const int SynchronousWarmupFrames = 180;
    private const int DecisionBudget = 16;
    private const int PathBudget = 8;
    private const double TargetFrameP95Milliseconds = 1000.0 / 60.0;
    private const double TargetSchedulerP95Milliseconds = 4.0;
    private const double TargetAverageGcKilobytesPerFrame = 64.0;
    private const double TargetMaximumGcKilobytesPerFrame = 256.0;
    private const int MaximumSynchronousNpcCount = 100;
    private const int PlayModeCreationBatchSize = 8;
    private const string RuntimeDiagnosticsGateVersion =
        "ai-runtime-gate-v3";
    private const string VerifierRevision =
        "character-ai-stress-v4-20260814";
    private const string PlayModeProfileRequestedKey = "DungeonStory.CharacterAiStress.PlayModeProfile.Requested";
    private const string PlayModeProfileNpcCountKey = "DungeonStory.CharacterAiStress.PlayModeProfile.NpcCount";
    private const string PlayModeProfileWarmupFramesKey = "DungeonStory.CharacterAiStress.PlayModeProfile.WarmupFrames";
    private const string PlayModeProfileSampleFramesKey = "DungeonStory.CharacterAiStress.PlayModeProfile.SampleFrames";
    private const string PlayModeProfileExitWhenDoneKey = "DungeonStory.CharacterAiStress.PlayModeProfile.ExitWhenDone";
    private const string PlayModeProfileReportKey = "DungeonStory.CharacterAiStress.PlayModeProfile.Report";
    private const string ProfileReportPath = "docs/implementation-reports/ai-play-mode-profile-latest.json";
    private const string Synchronous100ReportPath =
        "docs/implementation-reports/ai-sync-100-stress-latest.json";
    private const string StressGridWidthEnvironmentKey = "DUNGEON_AI_STRESS_GRID_WIDTH";
    private const string StressGridHeightEnvironmentKey = "DUNGEON_AI_STRESS_GRID_HEIGHT";
    private const string StressActiveFloorCountEnvironmentKey = "DUNGEON_AI_STRESS_ACTIVE_FLOORS";
    private const string StressDetailedDecisionProfileEnvironmentKey =
        "DUNGEON_AI_STRESS_DETAILED_DECISIONS";
    private const string StressFacilityCountEnvironmentKey = "DUNGEON_AI_STRESS_FACILITY_COUNT";
    private const string StressRoomSpanEnvironmentKey = "DUNGEON_AI_STRESS_ROOM_SPAN";
    private const string DenseGridAiProfileReportEnvironmentKey = "DUNGEON_AI_DENSE_REPORT";
    private const string LargeGridAiProfileReportPath =
        "docs/implementation-reports/navigation-large-ai-profile-latest.json";
    private const string DenseGridAiProfileReportPath =
        "docs/implementation-reports/navigation-dense-dungeon-profile-latest.json";

    [Serializable]
    private sealed class SynchronousAiProfileResult
    {
        public bool valid;
        public string measurementScope;
        public string utc;
        public string verifierRevision;
        public string runtimeDiagnosticsGate;
        public int npcCount;
        public int registered;
        public int pendingAtEnd;
        public int charactersWithActions;
        public int tickedTrees;
        public int schedulerTouched;
        public int typedExemptions;
        public int lifecycleViolations;
        public int pathConservationViolations;
        public int reservationConservationViolations;
        public int branchConservationViolations;
        public int schedulerDelayViolations;
        public long minimumSchedulerProcessDelta;
        public long maximumSchedulerProcessDelta;
        public long minimumTreeTickDelta;
        public long maximumTreeTickDelta;
        public long starvedDecisionDelta;
        public float oldestDecisionDeferralSeconds;
        public float maximumDecisionDeferralSeconds;
        public int invariantViolations;
        public int orphanRecoveries;
        public int failureLoops;
        public int totalDecisions;
        public int maxDecisionsPerFrame;
        public int totalBehaviorTreeTicks;
        public int maxBehaviorTreeTicksPerFrame;
        public int totalPathSearches;
        public int maxPathSearchesPerFrame;
        public int totalBrokerPathSearches;
        public int maxBrokerPathSearchesPerFrame;
        public int brokerCacheHits;
        public int brokerPathBudgetDeferrals;
        public int schedulerAllocationSamples;
        public double schedulerAverageAllocatedKb;
        public double schedulerMaximumAllocatedKb;
        public double elapsedMs;
        public string gameplayEvidence;
        public string summary;
    }

    [Serializable]
    private sealed class LargeGridAiProfileResult
    {
        public bool valid;
        public string measurementScope;
        public string utc;
        public string verifierRevision;
        public string runtimeDiagnosticsGate;
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
        public int allocationSamples;
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
        public int actorsTickedDuringSample;
        public int schedulerTouched;
        public int healthyActivityTouched;
        public int pendingAtEnd;
        public long minimumSchedulerProcessDelta;
        public long maximumSchedulerProcessDelta;
        public long minimumTreeTickDelta;
        public long maximumTreeTickDelta;
        public int lifecycleViolations;
        public int pathConservationViolations;
        public int reservationConservationViolations;
        public int branchConservationViolations;
        public int schedulerDelayViolations;
        public long invariantAnomalyDelta;
        public long orphanRecoveryDelta;
        public long failureLoopDelta;
        public long starvedDecisionDelta;
        public float oldestDecisionDeferralSeconds;
        public float maximumDecisionDeferralSeconds;
        public float maximumInitialDecisionDeferralSeconds;
        public CharacterAiPerformanceReport performance;
        public string failure;
    }

    private sealed class FixedSchedulerService : ICharacterAiSchedulingService
    {
        public bool IsSchedulerAvailable => true;
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

    private sealed class FixedProfileClock : IGameClock, IUiClock
    {
        private float time;
        public float DeltaTime => 1f / 60f;
        public float Time => time;
        public int FrameCount { get; private set; }
        public bool IsPaused => false;

        public void Advance(float deltaTime)
        {
            time += Mathf.Max(0f, deltaTime);
            FrameCount++;
        }
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
        LastReport = "RunAll is not pass evidence: synchronous validation is capped at "
            + $"{MaximumSynchronousNpcCount}. Use StartPlayModeProfile(500, ...) instead.";
        if (logSuccess) UnityEngine.Debug.LogWarning(LastReport);
        return false;
    }

    public static bool RunScaleSuite(bool logSuccess)
    {
        LastScaleReport = "RunScaleSuite is not pass evidence because 300/500 exceed the "
            + $"{MaximumSynchronousNpcCount}-NPC synchronous cap. RunForCount(100) and "
            + "StartPlayModeProfile(300/500, ...) are separate required gates.";
        if (logSuccess) UnityEngine.Debug.LogWarning(LastScaleReport);
        return false;
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

        Dictionary<CharacterActor, CharacterAiRuntimeGateSnapshot>
            runtimeGateBaselines = new();
        Dictionary<CharacterActor, long> behaviorTreeTickBaselines = new();
        foreach (CharacterActor character in world.Characters)
        {
            if (character?.Brain != null)
            {
                character.Brain.ResetSchedulerDelayTelemetryForDiagnostics();
                runtimeGateBaselines[character] =
                    character.Brain.CaptureRuntimeGateSnapshot();
            }
            if (character?.BehaviorTree != null)
            {
                behaviorTreeTickBaselines[character] =
                    character.BehaviorTree.DungeonStoryTickCount;
            }
        }

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

        // Registration is intentionally spread over 1.5 seconds and off-screen
        // actors may wait another 1.5 seconds for their first decision. Keep the
        // fairness baseline before this horizon so every actor must receive its
        // initial scheduler/BT service, but exclude the horizon from timing/GC and
        // steady-state scheduler-delay measurements below. The synchronous fixture
        // has no player loop, so actors that start a multi-frame movement during
        // warmup correctly remain RUNNING and do not need another BT decision.
        for (int frame = 0; frame < SynchronousWarmupFrames; frame++)
        {
            world.RunSchedulerTick(1f / 60f);
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
        foreach (CharacterActor character in world.Characters)
        {
            character?.Brain?.ResetSchedulerDelayTelemetryForDiagnostics();
        }
        world.Scheduler.ResetDecisionDeferralTelemetryForDiagnostics();
        long starvedDecisionBaseline =
            world.Scheduler.CumulativeStarvedDecisionCount;

        CharacterAiEditorTestDependencies.ResetPerformanceRecorder();
        Stopwatch stopwatch = Stopwatch.StartNew();
        long totalSchedulerAllocatedBytes = 0L;
        long maximumSchedulerAllocatedBytes = 0L;
        int schedulerAllocationSamples = 0;

        for (int frame = 0; frame < SimulationFrames; frame++)
        {
            world.RunSchedulerTick(1f / 60f);
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
            if (world.Scheduler.LastAllocatedBytes >= 0L)
            {
                totalSchedulerAllocatedBytes += world.Scheduler.LastAllocatedBytes;
                maximumSchedulerAllocatedBytes = Math.Max(
                    maximumSchedulerAllocatedBytes,
                    world.Scheduler.LastAllocatedBytes);
                schedulerAllocationSamples++;
            }
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

        int typedExemptions = world.Characters.Count(IsTypedStressExemption);
        int schedulerTouched = world.Characters.Count(character =>
        {
            if (IsTypedStressExemption(character)) return true;
            if (character?.Brain == null
                || !runtimeGateBaselines.TryGetValue(character, out var start))
            {
                return false;
            }
            CharacterAiRuntimeGateSnapshot end =
                character.Brain.CaptureRuntimeGateSnapshot();
            return end.SchedulerProcesses > start.SchedulerProcesses;
        });
        int lifecycleViolations = world.Characters.Count(character =>
        {
            if (character?.Brain == null
                || !runtimeGateBaselines.TryGetValue(character, out var start))
            {
                return !IsTypedStressExemption(character);
            }
            CharacterAiRuntimeGateSnapshot end =
                character.Brain.CaptureRuntimeGateSnapshot();
            return !end.ConservesLifecycleFrom(in start);
        });
        int pathConservationViolations = world.Characters.Count(character =>
        {
            if (character?.Brain == null
                || !runtimeGateBaselines.TryGetValue(character, out var start))
                return !IsTypedStressExemption(character);
            CharacterAiRuntimeGateSnapshot end =
                character.Brain.CaptureRuntimeGateSnapshot();
            return !end.ConservesPathsFrom(in start);
        });
        int reservationConservationViolations = world.Characters.Count(character =>
        {
            if (character?.Brain == null
                || !runtimeGateBaselines.TryGetValue(character, out var start))
                return !IsTypedStressExemption(character);
            CharacterAiRuntimeGateSnapshot end =
                character.Brain.CaptureRuntimeGateSnapshot();
            return !end.ConservesReservationsFrom(in start);
        });
        int branchConservationViolations = world.Characters.Count(character =>
        {
            if (character?.Brain == null
                || !runtimeGateBaselines.TryGetValue(character, out var start))
                return !IsTypedStressExemption(character);
            CharacterAiRuntimeGateSnapshot end =
                character.Brain.CaptureRuntimeGateSnapshot();
            return !end.ConservesObservedBranchesFrom(in start);
        });
        int schedulerDelayViolations = world.Characters.Count(character =>
            !IsTypedStressExemption(character)
            && character?.Brain?.MaximumSchedulerDelaySeconds > 2f);
        long minimumSchedulerProcesses = world.Characters
            .Where(character => !IsTypedStressExemption(character))
            .Select(character =>
            {
                if (character?.Brain == null
                    || !runtimeGateBaselines.TryGetValue(
                        character,
                        out CharacterAiRuntimeGateSnapshot start))
                {
                    return 0L;
                }
                CharacterAiRuntimeGateSnapshot end =
                    character.Brain.CaptureRuntimeGateSnapshot();
                return end.SchedulerProcesses - start.SchedulerProcesses;
            })
            .DefaultIfEmpty(0L)
            .Min();
        long maximumSchedulerProcesses = world.Characters
            .Where(character => !IsTypedStressExemption(character))
            .Select(character =>
            {
                if (character?.Brain == null
                    || !runtimeGateBaselines.TryGetValue(
                        character,
                        out CharacterAiRuntimeGateSnapshot start))
                    return 0L;
                CharacterAiRuntimeGateSnapshot end =
                    character.Brain.CaptureRuntimeGateSnapshot();
                return end.SchedulerProcesses - start.SchedulerProcesses;
            })
            .DefaultIfEmpty(0L)
            .Max();
        long minimumTreeTickDelta = world.Characters
            .Where(character => !IsTypedStressExemption(character))
            .Select(character =>
            {
                if (character?.BehaviorTree == null
                    || !behaviorTreeTickBaselines.TryGetValue(
                        character,
                        out long startTicks))
                {
                    return 0L;
                }
                return character.BehaviorTree.DungeonStoryTickCount
                    - startTicks;
            })
            .DefaultIfEmpty(0L)
            .Min();
        long maximumTreeTickDelta = world.Characters
            .Where(character => !IsTypedStressExemption(character))
            .Select(character =>
            {
                if (character?.BehaviorTree == null
                    || !behaviorTreeTickBaselines.TryGetValue(
                        character,
                        out long startTicks))
                    return 0L;
                return character.BehaviorTree.DungeonStoryTickCount
                    - startTicks;
            })
            .DefaultIfEmpty(0L)
            .Max();
        double averageSchedulerAllocatedKb = schedulerAllocationSamples > 0
            ? totalSchedulerAllocatedBytes / 1024d / schedulerAllocationSamples
            : 0d;
        double maximumSchedulerAllocatedKb =
            maximumSchedulerAllocatedBytes / 1024d;
        int invariantViolations = world.Characters.Count(character =>
            character?.Brain?.RuntimeInvariantAnomalyCount > 0L);
        int orphanRecoveries = world.Characters.Count(character =>
            character?.Brain?.RuntimeOrphanWorkActionRecoveryCount > 0L);
        int failureLoops = world.Characters.Count(character =>
            character?.Brain?.RuntimeFailureLoopCount > 0L);
        long starvedDecisionDelta = Math.Max(
            0L,
            world.Scheduler.CumulativeStarvedDecisionCount
                - starvedDecisionBaseline);
        float oldestDecisionDeferralSeconds =
            world.Scheduler.LastOldestDecisionDeferralSeconds;
        float maximumDecisionDeferralSeconds =
            world.Scheduler.MaximumObservedDecisionDeferralSeconds;
        bool valid = world.Scheduler.RegisteredCharacterCount == npcCount
            && schedulerTouched == npcCount
            && tickedTrees == npcCount
            && withActions == npcCount
            && maxDecisions <= DecisionBudget
            && maxPathSearches <= PathBudget
            && maxBrokerPathSearches <= PathBudget
            && totalDecisions > 0
            && totalBehaviorTreeTicks > 0
            && totalPathSearches + totalBrokerPathSearches > 0
            && lifecycleViolations == 0
            && pathConservationViolations == 0
            && reservationConservationViolations == 0
            && branchConservationViolations == 0
            && schedulerDelayViolations == 0
            && minimumSchedulerProcesses > 0L
            && minimumTreeTickDelta > 0L
            && schedulerAllocationSamples == SimulationFrames
            && averageSchedulerAllocatedKb
                <= TargetAverageGcKilobytesPerFrame
            && maximumSchedulerAllocatedKb
                <= TargetMaximumGcKilobytesPerFrame
            && invariantViolations == 0
            && orphanRecoveries == 0
            && failureLoops == 0
            && starvedDecisionDelta == 0L
            && oldestDecisionDeferralSeconds <= 2f
            && maximumDecisionDeferralSeconds <= 2f;

        LastReport =
            $"valid={valid}, verifierRevision={VerifierRevision}, runtimeDiagnosticsGate={RuntimeDiagnosticsGateVersion}, registered={world.Scheduler.RegisteredCharacterCount}, " +
            $"pending={pendingCharacters}, withActions={withActions}, tickedTrees={tickedTrees}, gridReady={gridReady}, " +
            $"pathBudgetActive={world.Scheduler.IsPathBudgetActiveForDebug}, touched={touchedCharacters}, " +
            $"schedulerTouched={schedulerTouched}, typedExemptions={typedExemptions}, lifecycleViolations={lifecycleViolations}, pathConservationViolations={pathConservationViolations}, reservationConservationViolations={reservationConservationViolations}, branchConservationViolations={branchConservationViolations}, schedulerDelayViolations={schedulerDelayViolations}, schedulerProcessDeltaMinMax={minimumSchedulerProcesses}/{maximumSchedulerProcesses}, treeTickDeltaMinMax={minimumTreeTickDelta}/{maximumTreeTickDelta}, gameplayEvidence=N/A(sync-no-player-loop), invariantViolations={invariantViolations}, orphanRecoveries={orphanRecoveries}, failureLoops={failureLoops}, " +
            $"starvedDecisionDelta={starvedDecisionDelta}, oldestDecisionDeferralSeconds={oldestDecisionDeferralSeconds:0.###}, maximumDecisionDeferralSeconds={maximumDecisionDeferralSeconds:0.###}, " +
            $"totalDecisions={totalDecisions}, maxDecisions/frame={maxDecisions}, " +
            $"totalBtTicks={totalBehaviorTreeTicks}, maxBtTicks/frame={maxBehaviorTreeTicks}, " +
            $"totalPathSearches={totalPathSearches}, maxPathSearches/frame={maxPathSearches}, " +
            $"brokerPathSearches={totalBrokerPathSearches}, brokerCacheHits={totalBrokerPathCacheHits}, " +
            $"brokerBudgetDeferrals={totalBrokerPathBudgetDeferrals}, maxBrokerPathSearches/frame={maxBrokerPathSearches}, " +
            $"maxBrokerCacheHits/frame={maxBrokerPathCacheHits}, maxBrokerBudgetDeferrals/frame={maxBrokerPathBudgetDeferrals}, " +
            $"schedulerGcSamples={schedulerAllocationSamples}/{SimulationFrames}, schedulerGcAvgKb={averageSchedulerAllocatedKb:0.###}, schedulerGcMaxKb={maximumSchedulerAllocatedKb:0.###}, perf=[{performanceSummary}], branches={branches}, samples={samples}, " +
            $"elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.0}";

        if (npcCount == MaximumSynchronousNpcCount)
        {
            SynchronousAiProfileResult durableResult =
                new SynchronousAiProfileResult
                {
                    valid = valid,
                    measurementScope =
                        "synchronous scheduler/behavior-tree/path-budget and runtime ownership accounting; no Unity player loop, movement coroutine, rendering, or facility service completion",
                    utc = DateTime.UtcNow.ToString("O"),
                    verifierRevision = VerifierRevision,
                    runtimeDiagnosticsGate = RuntimeDiagnosticsGateVersion,
                    npcCount = npcCount,
                    registered = world.Scheduler.RegisteredCharacterCount,
                    pendingAtEnd = pendingCharacters,
                    charactersWithActions = withActions,
                    tickedTrees = tickedTrees,
                    schedulerTouched = schedulerTouched,
                    typedExemptions = typedExemptions,
                    lifecycleViolations = lifecycleViolations,
                    pathConservationViolations =
                        pathConservationViolations,
                    reservationConservationViolations =
                        reservationConservationViolations,
                    branchConservationViolations =
                        branchConservationViolations,
                    schedulerDelayViolations = schedulerDelayViolations,
                    minimumSchedulerProcessDelta = minimumSchedulerProcesses,
                    maximumSchedulerProcessDelta = maximumSchedulerProcesses,
                    minimumTreeTickDelta = minimumTreeTickDelta,
                    maximumTreeTickDelta = maximumTreeTickDelta,
                    starvedDecisionDelta = starvedDecisionDelta,
                    oldestDecisionDeferralSeconds =
                        oldestDecisionDeferralSeconds,
                    maximumDecisionDeferralSeconds =
                        maximumDecisionDeferralSeconds,
                    invariantViolations = invariantViolations,
                    orphanRecoveries = orphanRecoveries,
                    failureLoops = failureLoops,
                    totalDecisions = totalDecisions,
                    maxDecisionsPerFrame = maxDecisions,
                    totalBehaviorTreeTicks = totalBehaviorTreeTicks,
                    maxBehaviorTreeTicksPerFrame = maxBehaviorTreeTicks,
                    totalPathSearches = totalPathSearches,
                    maxPathSearchesPerFrame = maxPathSearches,
                    totalBrokerPathSearches = totalBrokerPathSearches,
                    maxBrokerPathSearchesPerFrame = maxBrokerPathSearches,
                    brokerCacheHits = totalBrokerPathCacheHits,
                    brokerPathBudgetDeferrals =
                        totalBrokerPathBudgetDeferrals,
                    schedulerAllocationSamples = schedulerAllocationSamples,
                    schedulerAverageAllocatedKb =
                        averageSchedulerAllocatedKb,
                    schedulerMaximumAllocatedKb =
                        maximumSchedulerAllocatedKb,
                    elapsedMs = stopwatch.Elapsed.TotalMilliseconds,
                    gameplayEvidence = "N/A(sync-no-player-loop)",
                    summary = LastReport
                };
            Directory.CreateDirectory(
                Path.GetDirectoryName(Synchronous100ReportPath)
                    ?? Directory.GetCurrentDirectory());
            File.WriteAllText(
                Synchronous100ReportPath,
                JsonUtility.ToJson(durableResult, true));
        }

        if (logSuccess || !valid)
        {
            UnityEngine.Debug.Log($"{npcCount} NPC AI stress: {LastReport}");
        }

        return valid;
    }

    private static bool IsTypedStressExemption(CharacterActor character) =>
        character == null || !character.CanRunAi;

    public static bool RunConfiguredLargeGrid500Profile(bool logSuccess)
    {
        return RunConfigured1024Profile(logSuccess, facilityDense: false);
    }

    public static bool RunConfiguredDenseDungeon500Profile(bool logSuccess)
    {
        return RunConfigured1024Profile(logSuccess, facilityDense: true);
    }

    private static bool RunConfigured1024Profile(
        bool logSuccess,
        bool facilityDense)
    {
        string previousWidth = Environment.GetEnvironmentVariable(
            StressGridWidthEnvironmentKey);
        string previousHeight = Environment.GetEnvironmentVariable(
            StressGridHeightEnvironmentKey);
        string previousFloors = Environment.GetEnvironmentVariable(
            StressActiveFloorCountEnvironmentKey);
        try
        {
            Environment.SetEnvironmentVariable(
                StressGridWidthEnvironmentKey,
                "1024");
            Environment.SetEnvironmentVariable(
                StressGridHeightEnvironmentKey,
                "1024");
            Environment.SetEnvironmentVariable(
                StressActiveFloorCountEnvironmentKey,
                "3");
            return RunConfiguredLargeGridProfile(
                NpcCount,
                logSuccess,
                facilityDense);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                StressGridWidthEnvironmentKey,
                previousWidth);
            Environment.SetEnvironmentVariable(
                StressGridHeightEnvironmentKey,
                previousHeight);
            Environment.SetEnvironmentVariable(
                StressActiveFloorCountEnvironmentKey,
                previousFloors);
        }
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
                ? "real building objects, room scan, facility churn, full-population invalidation fairness; no movement or rendering"
                : "scheduler-only; full-population invalidation fairness; no movement, rendering, or presentation",
            utc = DateTime.UtcNow.ToString("O"),
            verifierRevision = VerifierRevision,
            runtimeDiagnosticsGate = RuntimeDiagnosticsGateVersion,
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
                    // 1024 columns / 16 cells per room * 4 authored slots
                    // * 3 active floors. Asking for 8192 exceeded even the
                    // physical fixture capacity and made the profile abort
                    // before any AI work was measured.
                    768,
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
            // Actor registration may apply the production scheduling profile.
            // Reassert the diagnostic hard ceilings after the full population
            // exists so this profile measures the authored 4 ms service slice
            // instead of a stale bootstrap/default budget.
            world.ConfigureDiagnosticBudgets(DecisionBudget, PathBudget);
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
                world.RunSchedulerTick(1f / 60f);
            }

            for (int tick = 0; tick < warmupTicks; tick++)
            {
                world.RunSchedulerTick(1f / 60f);
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

            bool detailedDecisionProfile = string.Equals(
                Environment.GetEnvironmentVariable(
                    StressDetailedDecisionProfileEnvironmentKey),
                "1",
                StringComparison.Ordinal);
            CharacterAiEditorTestDependencies.ResetPerformanceRecorder(
                // Performance authority mirrors the release configuration.
                // Detailed per-stage Stopwatch/List tracing is a separate
                // diagnostic pass and must not contaminate the p95 gate.
                detailedCollectionEnabled: detailedDecisionProfile,
                slowTraceEnabled: detailedDecisionProfile);
            // Warmup may have run while an earlier diagnostic left detailed
            // tracing enabled. Do not carry those instrumented per-actor cost
            // estimates into the release-configuration sample window.
            world.ConfigureDiagnosticBudgets(DecisionBudget, PathBudget);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            List<double> tickTimes = new List<double>(sampleTicks);
            List<double> schedulerTimes = new List<double>(sampleTicks);
            long totalAllocatedBytes = 0L;
            long maxAllocatedBytes = 0L;
            int allocatedSamples = 0;
            int[] tickBaseline = world.Characters
                .Select(character => character?.BehaviorTree?.DungeonStoryTickCount ?? 0)
                .ToArray();
            ForceFullPopulationReplan(world);
            foreach (CharacterActor character in world.Characters)
            {
                character?.Brain?.ResetSchedulerDelayTelemetryForDiagnostics();
            }
            // Warmup requests can already be overdue. A full-population
            // invalidation sample must measure latency from the invalidation
            // boundary rather than inheriting an older due time that
            // Schedule() deliberately preserves for normal runtime fairness.
            world.Scheduler.ResetDecisionQueueForDiagnostics();
            world.Scheduler.ResetDecisionDeferralTelemetryForDiagnostics();
            Dictionary<CharacterActor, CharacterAiRuntimeGateSnapshot>
                runtimeGateBaselines = new();
            Dictionary<CharacterActor, long> orphanRecoveryBaselines = new();
            foreach (CharacterActor character in world.Characters)
            {
                if (character?.Brain == null)
                {
                    continue;
                }

                runtimeGateBaselines[character] =
                    character.Brain.CaptureRuntimeGateSnapshot();
                orphanRecoveryBaselines[character] =
                    character.Brain.RuntimeOrphanWorkActionRecoveryCount;
            }
            long starvedDecisionBaseline =
                world.Scheduler.CumulativeStarvedDecisionCount;

            for (int tick = 0; tick < sampleTicks; tick++)
            {
                long started = Stopwatch.GetTimestamp();
                world.RunSchedulerTick(1f / 60f);
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
            result.allocationSamples = allocatedSamples;
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
            if (detailedDecisionProfile)
            {
                CharacterAiEditorTestDependencies.FlushSlowPerformanceTrace();
            }
            result.actorsTickedDuringSample = world.Characters
                .Where((character, index) => character?.BehaviorTree != null
                    && character.BehaviorTree.DungeonStoryTickCount
                        > tickBaseline[index])
                .Count();
            result.pendingAtEnd = world.Characters.Count(character =>
                character != null && character.IsAiDecisionPending);
            result.minimumSchedulerProcessDelta = long.MaxValue;
            result.minimumTreeTickDelta = long.MaxValue;
            for (int index = 0; index < world.Characters.Count; index++)
            {
                CharacterActor character = world.Characters[index];
                if (IsTypedStressExemption(character))
                {
                    result.schedulerTouched++;
                    result.healthyActivityTouched++;
                    continue;
                }

                if (character?.Brain == null
                    || !runtimeGateBaselines.TryGetValue(
                        character,
                        out CharacterAiRuntimeGateSnapshot startGate))
                {
                    result.lifecycleViolations++;
                    continue;
                }

                CharacterAiRuntimeGateSnapshot endGate =
                    character.Brain.CaptureRuntimeGateSnapshot();
                long schedulerProcessDelta =
                    endGate.SchedulerProcesses - startGate.SchedulerProcesses;
                result.minimumSchedulerProcessDelta = Math.Min(
                    result.minimumSchedulerProcessDelta,
                    schedulerProcessDelta);
                result.maximumSchedulerProcessDelta = Math.Max(
                    result.maximumSchedulerProcessDelta,
                    schedulerProcessDelta);
                if (schedulerProcessDelta > 0L)
                {
                    result.schedulerTouched++;
                }
                if (endGate.HasHealthyActivityFrom(in startGate))
                {
                    result.healthyActivityTouched++;
                }

                long treeTickDelta = character.BehaviorTree != null
                    ? character.BehaviorTree.DungeonStoryTickCount
                        - tickBaseline[index]
                    : 0L;
                result.minimumTreeTickDelta = Math.Min(
                    result.minimumTreeTickDelta,
                    treeTickDelta);
                result.maximumTreeTickDelta = Math.Max(
                    result.maximumTreeTickDelta,
                    treeTickDelta);

                if (!endGate.ConservesLifecycleFrom(in startGate))
                    result.lifecycleViolations++;
                if (!endGate.ConservesPathsFrom(in startGate))
                    result.pathConservationViolations++;
                if (!endGate.ConservesReservationsFrom(in startGate))
                    result.reservationConservationViolations++;
                if (!endGate.ConservesObservedBranchesFrom(in startGate))
                    result.branchConservationViolations++;
                if (endGate.MaximumSchedulerDelayMilliseconds > 2000)
                    result.schedulerDelayViolations++;

                result.invariantAnomalyDelta += Math.Max(
                    0L,
                    endGate.InvariantAnomalies - startGate.InvariantAnomalies);
                result.failureLoopDelta += Math.Max(
                    0L,
                    endGate.FailureLoops - startGate.FailureLoops);
                if (orphanRecoveryBaselines.TryGetValue(
                    character,
                    out long orphanRecoveryBaseline))
                {
                    result.orphanRecoveryDelta += Math.Max(
                        0L,
                        character.Brain.RuntimeOrphanWorkActionRecoveryCount
                            - orphanRecoveryBaseline);
                }
            }
            if (result.minimumSchedulerProcessDelta == long.MaxValue)
                result.minimumSchedulerProcessDelta = 0L;
            if (result.minimumTreeTickDelta == long.MaxValue)
                result.minimumTreeTickDelta = 0L;
            result.starvedDecisionDelta = Math.Max(
                0L,
                world.Scheduler.CumulativeStarvedDecisionCount
                    - starvedDecisionBaseline);
            result.oldestDecisionDeferralSeconds =
                world.Scheduler.LastOldestDecisionDeferralSeconds;
            result.maximumDecisionDeferralSeconds =
                world.Scheduler.MaximumObservedDecisionDeferralSeconds;
            float sampleHorizonSeconds = sampleTicks / 60f;
            result.maximumInitialDecisionDeferralSeconds = 0f;
            for (int index = 0; index < world.Characters.Count; index++)
            {
                CharacterActor character = world.Characters[index];
                if (character?.BehaviorTree == null
                    || character.BehaviorTree.DungeonStoryTickCount
                        <= tickBaseline[index])
                {
                    result.maximumInitialDecisionDeferralSeconds =
                        sampleHorizonSeconds;
                    break;
                }

                result.maximumInitialDecisionDeferralSeconds = Mathf.Max(
                    result.maximumInitialDecisionDeferralSeconds,
                    character.Brain?.MaximumSchedulerDelaySeconds ?? 0f);
            }
            if (world.SchedulerDecisionLimit != DecisionBudget)
            {
                throw new InvalidOperationException(
                    $"Large-grid scheduler decision limit drifted: "
                    + $"{world.SchedulerDecisionLimit} != {DecisionBudget}.");
            }

            result.valid = result.gridWidth == 1024
                && result.gridHeight == 1024
                && result.registered == npcCount
                && result.tickedTrees == npcCount
                && result.charactersWithActions == npcCount
                && result.totalDecisions > 0
                && result.actorsTickedDuringSample == npcCount
                && result.schedulerTouched == npcCount
                && result.minimumSchedulerProcessDelta > 0L
                && result.minimumTreeTickDelta > 0L
                && result.maximumInitialDecisionDeferralSeconds <= 2f
                && result.maximumDecisionDeferralSeconds <= 2f
                && result.oldestDecisionDeferralSeconds <= 2f
                && result.starvedDecisionDelta == 0L
                && result.totalPathSearches + result.totalBrokerSearches > 0
                && result.totalUnboundedSearches == 0
                && result.maxDecisionsPerTick <= DecisionBudget
                && result.maxPathSearchesPerTick <= PathBudget
                && result.maxBrokerSearchesPerTick <= PathBudget
                && result.lifecycleViolations == 0
                && result.pathConservationViolations == 0
                && result.reservationConservationViolations == 0
                && result.branchConservationViolations == 0
                && result.schedulerDelayViolations == 0
                && result.invariantAnomalyDelta == 0L
                && result.orphanRecoveryDelta == 0L
                && result.failureLoopDelta == 0L
                && result.p95TickMs <= TargetFrameP95Milliseconds
                && result.p95SchedulerMs <= TargetSchedulerP95Milliseconds
                && result.allocationSamples == sampleTicks
                && result.averageAllocatedKb
                    <= TargetAverageGcKilobytesPerFrame
                && result.maxAllocatedKb
                    <= TargetMaximumGcKilobytesPerFrame;
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

        // A full action cancellation is much heavier than a production wake-up.
        // Rotate one actor per tick so all 500 actors are sampled without
        // manufacturing synchronized bursts that the production cadence
        // deliberately spreads across frames.
        const int forcedCount = 1;
        int baseIndex = tick * forcedCount;
        // At the observed ~0.55ms full decision cost, forcing all 16 authored
        // safety slots in one synthetic tick guarantees an 8-9ms burst and
        // measures the hard ceiling rather than the 4ms adaptive scheduler.
        // Force only the amount that can fit the production target; the
        // scheduler's own backlog/fairness logic remains responsible for the
        // rest.
        for (int offset = 0; offset < forcedCount; offset++)
        {
            CharacterActor actor =
                world.Characters[(baseIndex + offset) % world.Characters.Count];
            if (actor == null)
            {
                continue;
            }

            // This diagnostic intentionally measures forced replanning. A
            // regular wake-up preserves an in-flight action by contract, so it
            // cannot manufacture a decision sample once every actor owns a
            // running action. Terminate through the typed cancellation path
            // before scheduling the next decision.
            actor.Brain?.StopCurrentActionForReplan(
                "large-grid-stress-forced-replan");
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
            world.Scheduler.RequestImmediateDecisionFor(actor);
        }
    }

    private static void ForceFullPopulationReplan(StressWorld world)
    {
        if (world == null) return;
        foreach (CharacterActor actor in world.Characters)
        {
            if (actor == null) continue;
            actor.Brain?.StopCurrentActionForReplan(
                "large-grid-population-invalidation");
            actor.Brain?.RequestImmediateReplan(clearFailures: true);
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
        private readonly Dictionary<CharacterActor, CharacterAiRuntimeGateSnapshot>
            runtimeGateBaselines =
                new Dictionary<CharacterActor, CharacterAiRuntimeGateSnapshot>();
        private readonly Dictionary<CharacterActor, long>
            behaviorTreeTickBaselines = new Dictionary<CharacterActor, long>();

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
        private int maxFairnessDecisionFloor;
        private int maxPathSearches;
        private int maxBrokerPathSearches;
        private int maxBrokerPathCacheHits;
        private int maxBrokerPathBudgetDeferrals;
        private int budgetExhaustedFrames;
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
        private long previousMeasuredFrameTimestamp;
        private long starvedDecisionBaseline;

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
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    UnityEngine.Debug.LogWarning("500 NPC Play Mode profile is already running.");
                    return;
                }

                // Entering Play Mode can be rejected by Unity after the
                // request bit is set (for example, when a project compile
                // error is discovered). A later retry must not be blocked by
                // that orphaned SessionState flag.
                SessionState.SetBool(PlayModeProfileRequestedKey, false);
                SessionState.SetString(
                    PlayModeProfileReportKey,
                    "aborted=True, reason=stale-profile-request-recovered");
                current = null;
                UnityEngine.Debug.LogWarning(
                    "Recovered a stale 500 NPC Play Mode profile request left before Play Mode entry.");
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
            // Unity_RunCommand already executes inside the editor player loop.
            // Calling EditorApplication.Step repeatedly from here recursively
            // re-enters PlayerLoop and can leak TempJob allocations. Resume the
            // ordinary loop and request one future update instead; the profile
            // session samples through OnEditorUpdate across real frames.
            EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
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
                detailedCollectionEnabled: sampleFrames <= 120,
                slowTraceEnabled: sampleFrames <= 120);
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
            // StressWorld uses a deterministic clock so synchronous profiles
            // are reproducible. In PlayMode the scheduler is driven by Unity's
            // Update rather than RunSchedulerTick, so advance that same clock
            // once per real player frame; otherwise DynamicFrameWorkBudget
            // treats the entire 600-frame sample as one frame and accumulates
            // consumed time forever.
            world.AdvanceProfileClock(Time.unscaledDeltaTime);
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
                    maxFairnessDecisionFloor = Mathf.Max(
                        maxFairnessDecisionFloor,
                        warmupScheduler.LastFairnessDecisionFloor);
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
                    // World construction and the mandatory first tick for every
                    // behavior tree are warm-up work. Reset the diagnostic
                    // recorder here so category percentiles and slow-operation
                    // traces describe only the measured frames below.
                    CharacterAiEditorTestDependencies.ResetPerformanceRecorder(
                        detailedCollectionEnabled: sampleFrames <= 120,
                        slowTraceEnabled: sampleFrames <= 120);
                    world.ConfigureDiagnosticBudgets(
                        DecisionBudget,
                        PathBudget);
                    ForceFullPopulationReplan(world);
                    world.Scheduler.ResetDecisionQueueForDiagnostics();
                    world.Scheduler.ResetDecisionDeferralTelemetryForDiagnostics();
                    starvedDecisionBaseline =
                        world.Scheduler.CumulativeStarvedDecisionCount;
                    runtimeGateBaselines.Clear();
                    behaviorTreeTickBaselines.Clear();
                    foreach (CharacterActor character in world.Characters)
                    {
                        if (character?.Brain != null)
                        {
                            character.Brain
                                .ResetSchedulerDelayTelemetryForDiagnostics();
                            runtimeGateBaselines[character] =
                                character.Brain.CaptureRuntimeGateSnapshot();
                        }
                        if (character?.BehaviorTree != null)
                        {
                            behaviorTreeTickBaselines[character] =
                                character.BehaviorTree.DungeonStoryTickCount;
                        }
                    }
                    previousMeasuredFrameTimestamp = Stopwatch.GetTimestamp();
                    sampleStopwatch.Restart();
                }

                return;
            }

            CharacterAiScheduler scheduler = world.Scheduler;
            long measuredFrameTimestamp = Stopwatch.GetTimestamp();
            double deltaMs = previousMeasuredFrameTimestamp > 0L
                ? (measuredFrameTimestamp - previousMeasuredFrameTimestamp)
                    * 1000.0
                    / Stopwatch.Frequency
                : Mathf.Max(0f, Time.unscaledDeltaTime * 1000f);
            previousMeasuredFrameTimestamp = measuredFrameTimestamp;
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
                maxFairnessDecisionFloor = Mathf.Max(
                    maxFairnessDecisionFloor,
                    scheduler.LastFairnessDecisionFloor);
                maxPathSearches = Mathf.Max(maxPathSearches, scheduler.LastPathSearchCount);
                maxBrokerPathSearches = Mathf.Max(maxBrokerPathSearches, scheduler.LastBrokerPathSearchCount);
                maxBrokerPathCacheHits = Mathf.Max(maxBrokerPathCacheHits, scheduler.LastBrokerPathCacheHitCount);
                maxBrokerPathBudgetDeferrals = Mathf.Max(maxBrokerPathBudgetDeferrals, scheduler.LastBrokerPathBudgetDeferralCount);
                if (scheduler.LastBudgetExhausted)
                {
                    budgetExhaustedFrames++;
                }
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
            int minimumTreeTicks = world.Characters
                .Where(character => character?.BehaviorTree != null)
                .Select(character => character.BehaviorTree.DungeonStoryTickCount)
                .DefaultIfEmpty(0)
                .Min();
            int maximumTreeTicks = world.Characters
                .Where(character => character?.BehaviorTree != null)
                .Select(character => character.BehaviorTree.DungeonStoryTickCount)
                .DefaultIfEmpty(0)
                .Max();

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
            List<string> behaviorViolations = new List<string>();
            long measuredStarvedDecisions = Math.Max(
                0L,
                (scheduler?.CumulativeStarvedDecisionCount ?? 0L)
                    - starvedDecisionBaseline);
            if (scheduler == null)
                behaviorViolations.Add("scheduler-missing");
            if (scheduler != null && scheduler.RegisteredCharacterCount != npcCount)
                behaviorViolations.Add($"registered:{scheduler.RegisteredCharacterCount}!={npcCount}");
            int typedExemptions = world.Characters.Count(IsTypedStressExemption);
            int typedTouched = 0;
            int schedulerTouched = 0;
            int lifecycleViolations = 0;
            int schedulerDelayViolations = 0;
            int invariantViolations = 0;
            int orphanRecoveries = 0;
            int failureLoops = 0;
            long minimumMeasuredTreeTickDelta = long.MaxValue;
            long maximumMeasuredTreeTickDelta = 0L;
            long minimumSchedulerProcessDelta = long.MaxValue;
            long maximumSchedulerProcessDelta = 0L;
            const int maximumActorDiagnostics = 32;
            List<string> untouchedActorDiagnostics = new List<string>();
            List<string> failureLoopActorDiagnostics = new List<string>();
            foreach (CharacterActor character in world.Characters)
            {
                if (IsTypedStressExemption(character))
                {
                    typedTouched++;
                    schedulerTouched++;
                    continue;
                }
                if (character?.Brain == null)
                {
                    lifecycleViolations++;
                    if (untouchedActorDiagnostics.Count < maximumActorDiagnostics)
                    {
                        untouchedActorDiagnostics.Add(
                            $"actor={character?.name ?? "null"};brain=missing");
                    }
                    continue;
                }
                CharacterAiRuntimeGateSnapshot end =
                    character.Brain.CaptureRuntimeGateSnapshot();
                runtimeGateBaselines.TryGetValue(
                    character,
                    out CharacterAiRuntimeGateSnapshot start);
                long schedulerProcessDelta =
                    end.SchedulerProcesses - start.SchedulerProcesses;
                if (schedulerProcessDelta > 0L)
                {
                    schedulerTouched++;
                }
                minimumSchedulerProcessDelta = Math.Min(
                    minimumSchedulerProcessDelta,
                    schedulerProcessDelta);
                maximumSchedulerProcessDelta = Math.Max(
                    maximumSchedulerProcessDelta,
                    schedulerProcessDelta);
                long startTreeTicks = behaviorTreeTickBaselines.TryGetValue(
                    character,
                    out long treeBaseline)
                        ? treeBaseline
                        : character.BehaviorTree != null
                            ? character.BehaviorTree.DungeonStoryTickCount
                            : 0L;
                long treeTickDelta =
                    (character.BehaviorTree != null
                        ? character.BehaviorTree.DungeonStoryTickCount
                        : 0L)
                    - startTreeTicks;
                minimumMeasuredTreeTickDelta = Math.Min(
                    minimumMeasuredTreeTickDelta,
                    treeTickDelta);
                maximumMeasuredTreeTickDelta = Math.Max(
                    maximumMeasuredTreeTickDelta,
                    treeTickDelta);
                if (end.HasHealthyActivityFrom(in start))
                {
                    typedTouched++;
                }
                else if (untouchedActorDiagnostics.Count < maximumActorDiagnostics)
                {
                    untouchedActorDiagnostics.Add(FormatActorGateDiagnostic(
                        character,
                        in start,
                        in end));
                }
                if (!end.ConservesLifecycleFrom(in start)
                    || !end.ConservesPathsFrom(in start)
                    || !end.ConservesReservationsFrom(in start)
                    || !end.ConservesObservedBranchesFrom(in start))
                    lifecycleViolations++;
                if (end.MaximumSchedulerDelayMilliseconds > 2000)
                    schedulerDelayViolations++;
                if (end.InvariantAnomalies > start.InvariantAnomalies)
                    invariantViolations++;
                if (character.Brain.RuntimeOrphanWorkActionRecoveryCount > 0L)
                    orphanRecoveries++;
                if (end.FailureLoops > start.FailureLoops)
                {
                    failureLoops++;
                    if (failureLoopActorDiagnostics.Count < maximumActorDiagnostics)
                    {
                        failureLoopActorDiagnostics.Add(FormatActorGateDiagnostic(
                            character,
                            in start,
                            in end));
                    }
                }
            }
            if (typedTouched != npcCount)
                behaviorViolations.Add($"typed-touched:{typedTouched}!={npcCount}");
            if (schedulerTouched != npcCount)
                behaviorViolations.Add(
                    $"scheduler-touched:{schedulerTouched}!={npcCount}");
            if (minimumMeasuredTreeTickDelta == long.MaxValue)
                minimumMeasuredTreeTickDelta = 0L;
            if (minimumSchedulerProcessDelta == long.MaxValue)
                minimumSchedulerProcessDelta = 0L;
            if (minimumMeasuredTreeTickDelta <= 0L)
                behaviorViolations.Add(
                    $"measured-tree-tick-min:{minimumMeasuredTreeTickDelta}");
            if (minimumSchedulerProcessDelta <= 0L)
                behaviorViolations.Add(
                    $"scheduler-process-min:{minimumSchedulerProcessDelta}");
            if (lifecycleViolations > 0)
                behaviorViolations.Add($"lifecycle-conservation:{lifecycleViolations}");
            if (schedulerDelayViolations > 0)
                behaviorViolations.Add(
                    $"actor-scheduler-delay:{schedulerDelayViolations}");
            if (invariantViolations > 0)
                behaviorViolations.Add($"invariant-anomalies:{invariantViolations}");
            if (orphanRecoveries > 0)
                behaviorViolations.Add($"orphan-recoveries:{orphanRecoveries}");
            if (failureLoops > 0)
                behaviorViolations.Add($"failure-loops:{failureLoops}");
            if (tickedTrees != npcCount)
                behaviorViolations.Add($"ticked-trees:{tickedTrees}!={npcCount}");
            if (withActions != npcCount)
                behaviorViolations.Add($"with-actions:{withActions}!={npcCount}");
            if (maxDecisions > DecisionBudget)
                behaviorViolations.Add($"decision-budget:{maxDecisions}>{DecisionBudget}");
            if (maxPathSearches > PathBudget)
                behaviorViolations.Add($"path-budget:{maxPathSearches}>{PathBudget}");
            if (maxBrokerPathSearches > PathBudget)
                behaviorViolations.Add($"broker-path-budget:{maxBrokerPathSearches}>{PathBudget}");
            if (totalDecisions <= 0)
                behaviorViolations.Add("no-decisions");
            if (totalPathSearches + totalBrokerPathSearches <= 0)
                behaviorViolations.Add("no-path-searches");
            if (measuredStarvedDecisions > 0)
                behaviorViolations.Add(
                    $"starved-decisions:{measuredStarvedDecisions}");
            if (scheduler != null
                && scheduler.MaximumObservedDecisionDeferralSeconds > 2f)
            {
                behaviorViolations.Add(
                    $"decision-deferral:{scheduler.MaximumObservedDecisionDeferralSeconds:0.###}>2");
            }
            bool behaviorValid = behaviorViolations.Count == 0;
            string behaviorFailure = string.Join(",", behaviorViolations);
            // In an Editor Play Mode profile the frame-wide recorder also
            // includes editor tooling, profiler recorder plumbing and test
            // harness work. Prefer the scheduler's same-thread allocation
            // counter when available; keep frame-wide GC as a diagnostic and
            // retain the full-frame budget for a Player-build audit.
            bool aiOwnedGcValid = schedulerGcCounterSupported
                ? avgSchedulerGcAllocKb <= TargetAverageGcKilobytesPerFrame
                    && maxSchedulerGcAllocKb
                        <= TargetMaximumGcKilobytesPerFrame
                : avgGcAllocKb <= TargetAverageGcKilobytesPerFrame
                    && maxGcAllocKb <= TargetMaximumGcKilobytesPerFrame;
            bool performanceValid = p95FrameMs <= TargetFrameP95Milliseconds
                && p95SchedulerMs <= TargetSchedulerP95Milliseconds
                && aiOwnedGcValid;
            bool valid = behaviorValid && performanceValid;
            CharacterAiPerformanceReport detailedPerformance =
                CharacterAiEditorTestDependencies.CapturePerformanceReport(npcCount);
            CharacterAiEditorTestDependencies.FlushSlowPerformanceTrace();
            string detailedPerformanceSummary = string.Join(
                ",",
                detailedPerformance.metrics
                    .Where(metric => metric != null && metric.sampleCount > 0)
                    .Select(metric =>
                        $"{metric.name} n={metric.sampleCount} avg={metric.average:0.00} "
                        + $"p95={metric.p95:0.00} max={metric.max:0.00}ms"));

            string report =
                $"valid={valid}, behaviorValid={behaviorValid}, performanceValid={performanceValid}, "
                + $"runtimeDiagnosticsGate={RuntimeDiagnosticsGateVersion}, "
                + $"behaviorFailure={behaviorFailure}, "
                + $"grid={world.Grid.width}x{world.Grid.height}, "
                + $"npc={npcCount}, registered={(scheduler != null ? scheduler.RegisteredCharacterCount : 0)}, " +
                $"active={touchedCharacters}, pending={pendingCharacters}, withActions={withActions}, tickedTrees={tickedTrees}, " +
                $"typedTouched={typedTouched}, schedulerTouched={schedulerTouched}, typedExemptions={typedExemptions}, lifecycleViolations={lifecycleViolations}, schedulerDelayViolations={schedulerDelayViolations}, invariantViolations={invariantViolations}, orphanRecoveries={orphanRecoveries}, failureLoops={failureLoops}, " +
                $"treeTicksMinMax={minimumTreeTicks}/{maximumTreeTicks}, " +
                $"measuredTreeTickDeltaMinMax={minimumMeasuredTreeTickDelta}/{maximumMeasuredTreeTickDelta}, " +
                $"schedulerProcessDeltaMinMax={minimumSchedulerProcessDelta}/{maximumSchedulerProcessDelta}, " +
                $"warmupFrames={warmupSamples}, warmupWallMs={warmupStopwatch.Elapsed.TotalMilliseconds:0.0}, warmupCleanupMs={warmupCleanupMs:0.0}, " +
                $"samples={samples}, sampleWallMs={sampleStopwatch.Elapsed.TotalMilliseconds:0.0}, "
                + $"creationFrames={creationFrames}, creationMs={creationMs:0.0}, maxCreationFrameMs={maxCreationFrameMs:0.0}, " +
                $"avgFrameMs={avgDeltaMs:0.00}, p95FrameMs={p95FrameMs:0.00}, maxFrameMs={maxDeltaMs:0.00}, " +
                $"frames>16.7ms={framesOver16Ms}, frames>33.3ms={framesOver33Ms}, " +
                $"avgMainThreadMs={avgMainThreadMs:0.00}, maxMainThreadMs={maxMainThreadMs:0.00}, mainThreadSamples={mainThreadSamples}, " +
                $"avgSchedulerMs={avgSchedulerMs:0.000}, p95SchedulerMs={p95SchedulerMs:0.000}, maxSchedulerMs={maxSchedulerMs:0.000}, " +
                $"totalDecisions={totalDecisions}, maxDecisions/frame={maxDecisions}, " +
                $"maxFairnessFloor={maxFairnessDecisionFloor}, " +
                $"budgetExhaustedFrames={budgetExhaustedFrames}, "
                + $"starvedDecisions={measuredStarvedDecisions}, "
                + $"oldestDeferral={(scheduler?.LastOldestDecisionDeferralSeconds ?? 0f):0.###}s, "
                + $"maxDeferral={(scheduler?.MaximumObservedDecisionDeferralSeconds ?? 0f):0.###}s, "
                + $"totalPathSearches={totalPathSearches}, maxPathSearches/frame={maxPathSearches}, " +
                $"brokerPathSearches={totalBrokerPathSearches}, brokerCacheHits={totalBrokerPathCacheHits}, " +
                $"brokerBudgetDeferrals={totalBrokerPathBudgetDeferrals}, maxBrokerPathSearches/frame={maxBrokerPathSearches}, " +
                $"maxBrokerCacheHits/frame={maxBrokerPathCacheHits}, maxBrokerBudgetDeferrals/frame={maxBrokerPathBudgetDeferrals}, " +
                $"avgGcAllocKB/frame={avgGcAllocKb:0.0}, maxGcAllocKB/frame={maxGcAllocKb:0.0}, " +
                $"editorBaselineGcKB/frame={gcBaselineBytesPerFrame / 1024.0:0.0}, " +
                $"schedulerGcCounterSupported={schedulerGcCounterSupported}, " +
                $"aiOwnedGcValid={aiOwnedGcValid}, " +
                $"avgSchedulerGcAllocKB/frame={avgSchedulerGcAllocKb:0.0}, maxSchedulerGcAllocKB/frame={maxSchedulerGcAllocKb:0.0}, " +
                $"monoUsedDeltaMB={monoDeltaMb:0.00}, gen0Collections={GC.CollectionCount(0) - startGen0Collections}, "
                + $"perf=[{detailedPerformanceSummary}], "
                + $"untouchedActors=[{string.Join(" | ", untouchedActorDiagnostics)}], "
                + $"failureLoopActors=[{string.Join(" | ", failureLoopActorDiagnostics)}]";

            SessionState.SetString(PlayModeProfileReportKey, report);
            WriteProfileReport(
                valid,
                report,
                touchedCharacters,
                pendingCharacters,
                withActions,
                tickedTrees,
                minimumTreeTicks,
                maximumTreeTicks,
                behaviorValid,
                behaviorFailure,
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

        private static string FormatActorGateDiagnostic(
            CharacterActor character,
            in CharacterAiRuntimeGateSnapshot start,
            in CharacterAiRuntimeGateSnapshot end)
        {
            if (character == null) return "actor=null";
            AIBrain brain = character.Brain;
            string actorId = CharacterPersistentIdentity.TryGet(
                character,
                out CharacterId persistentId)
                    ? persistentId.Value
                    : character.name;
            AIActionFailure? failure = brain != null
                ? brain.LastActionFailure
                : (AIActionFailure?)null;
            return
                $"actor={actorId};pos={character.transform.position};"
                + $"action={brain?.CurrentActionDebugLabel};"
                + $"phase={brain?.CurrentActionPhase};runtimePhase={brain?.CurrentRuntimePhase};"
                + $"destination={brain?.CurrentDestinationDebugLabel};"
                + $"failure={failure?.Kind}:{failure?.Reason};"
                + $"gameplayProgress={end.GameplayProgressRevision - start.GameplayProgressRevision};"
                + $"queueHeartbeats={end.FacilityQueueHeartbeats - start.FacilityQueueHeartbeats};"
                + $"serviceHeartbeats={end.FacilityServiceHeartbeats - start.FacilityServiceHeartbeats};"
                + $"runtimeProgress={end.ProgressRevision - start.ProgressRevision};"
                + $"schedulerProcesses={end.SchedulerProcesses - start.SchedulerProcesses};"
                + $"schedulerDelayMs={end.MaximumSchedulerDelayMilliseconds};"
                + $"starts={end.ActionStarts - start.ActionStarts};"
                + $"terminals={end.ActionTerminals - start.ActionTerminals};"
                + $"live={end.LiveActions};paths={end.PathRequests - start.PathRequests}/"
                + $"{end.PathResults - start.PathResults}/{end.LivePathRequests};"
                + $"reservations={end.ReservationAcquires - start.ReservationAcquires}/"
                + $"{end.ReservationReleases - start.ReservationReleases}/{end.LiveReservations};"
                + $"retries={end.RetrySchedules - start.RetrySchedules}/"
                + $"{end.RetryAttempts - start.RetryAttempts};"
                + $"failureLoops={end.FailureLoops - start.FailureLoops};"
                + $"branches=[{end.FormatObservedBranchesFrom(in start)}]";
        }

        private void WriteProfileReport(
            bool valid,
            string report,
            int touchedCharacters,
            int pendingCharacters,
            int withActions,
            int tickedTrees,
            int minimumTreeTicks,
            int maximumTreeTicks,
            bool behaviorValid,
            string behaviorFailure,
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
                $"  \"behaviorFailure\": \"{EscapeJson(behaviorFailure)}\",\n" +
                $"  \"performanceValid\": {performanceValid.ToString().ToLowerInvariant()},\n" +
                $"  \"measurementScope\": \"isolated PlayMode stress world with live scheduler, behavior trees, action selection and movement coroutines; no production scene rendering or long-running economy/service simulation\",\n" +
                $"  \"utc\": \"{DateTime.UtcNow:O}\",\n" +
                $"  \"verifierRevision\": \"{VerifierRevision}\",\n" +
                $"  \"runtimeDiagnosticsGate\": \"{RuntimeDiagnosticsGateVersion}\",\n" +
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
                $"  \"minimumTreeTicks\": {minimumTreeTicks},\n" +
                $"  \"maximumTreeTicks\": {maximumTreeTicks},\n" +
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
                $"  \"maxFairnessDecisionFloor\": {maxFairnessDecisionFloor},\n" +
                $"  \"budgetExhaustedFrames\": {budgetExhaustedFrames},\n" +
                $"  \"starvedDecisions\": {Math.Max(0L, (world.Scheduler?.CumulativeStarvedDecisionCount ?? 0L) - starvedDecisionBaseline)},\n" +
                $"  \"oldestDecisionDeferralSeconds\": {(world.Scheduler?.LastOldestDecisionDeferralSeconds ?? 0f):0.###},\n" +
                $"  \"maximumDecisionDeferralSeconds\": {(world.Scheduler?.MaximumObservedDecisionDeferralSeconds ?? 0f):0.###},\n" +
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
                $"  \"gcPassAuthority\": \"{(schedulerGcCounterSupported ? "ai-scheduler-thread" : "editor-frame-fallback")}\",\n" +
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
        private readonly IDisposable gridSystemOverride;
        private readonly ExternalBehaviorTree externalBehavior;
        private readonly FixedProfileClock profileClock;
        private readonly IGridPathSearchBroker profilePathSearchBroker;
        private readonly IDynamicFrameWorkBudget profileFrameWorkBudget;
        private readonly IFacilityCandidateCache profileFacilityCandidateCache;
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
            gridSystemOverride =
                CharacterAiEditorTestDependencies.OverrideGridSystemForScenario(
                    manager);

            GameObject schedulerObject = new GameObject("500 NPC Stress CharacterAiScheduler");
            objects.Add(schedulerObject);
            externalBehavior = CharacterAiBehaviorDesignerGraphBuilder.EnsureCharacterAiExternalBehavior();
            Scheduler = schedulerObject.AddComponent<CharacterAiScheduler>();
            Scheduling = new FixedSchedulerService(Scheduler);
            SetPrivateField(Scheduler, "registerExistingSceneCharacters", false);
            profileClock = new FixedProfileClock();
            profilePathSearchBroker = new GridPathSearchBroker(
                profileClock,
                doorAccessQuery: null,
                performanceRecorder: null,
                costPolicy: null);
            profileFrameWorkBudget = new DynamicFrameWorkBudget(
                profileClock,
                profileClock);
            profileFacilityCandidateCache = new FacilityCandidateCacheStore(
                CharacterAiEditorTestDependencies.WorldRegistry,
                frameWorkBudget: profileFrameWorkBudget);
            Scheduler.Construct(
                CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.TestMainCameraProvider,
                CharacterAiEditorTestDependencies.TestBehaviorTreeConfigurator,
                profilePathSearchBroker,
                profileClock,
                profileFrameWorkBudget,
                CharacterAiEditorTestDependencies.TestPerformanceRecorder,
                profileClock,
                profileFacilityCandidateCache,
                playerStaffCommands: null,
                debugRules: DisabledDungeonDebugRuleQuery.Instance);
            Scheduler.ClearRegistrationsForDebug();
            SetPrivateField(Scheduler, "characterAiExternalBehavior", externalBehavior);
            // The 1024-grid profile targets the same 4ms AI slice as release.
            // A 16-decision hard ceiling allows the fairness floor to create an
            // artificial 8-10ms burst in this synchronous harness. Bound this
            // configuration to the measured large-grid service capacity.
            Scheduler.ConfigureDiagnosticBudgets(DecisionBudget, PathBudget);
            SetPrivateField(Scheduler, "visibleDecisionInterval", 0.35f);
            SetPrivateField(Scheduler, "offscreenDecisionInterval", 1.5f);
            SetPrivateField(Scheduler, "ownerDecisionInterval", 0.2f);
            SetPrivateField(Scheduler, "retryDelay", 0.05f);
            SetPrivateField(Scheduler, "registrationSpreadSeconds", 1.5f);
            SetPrivateField(Scheduler, "minDecisionsPerFrame", 1);
            SetPrivateField(Scheduler, "maximumDecisionDeferralSeconds", 2f);
        }

        public Grid Grid { get; }
        public CharacterAiScheduler Scheduler { get; }
        public ICharacterAiSchedulingService Scheduling { get; }
        public int SchedulerDecisionLimit =>
            Scheduler.MaximumDecisionsPerFrameForDiagnostics;
        public void ConfigureDiagnosticBudgets(
            int maximumDecisionsPerFrame,
            int maximumPathSearchesPerFrame)
        {
            Scheduler.ConfigureDiagnosticBudgets(
                maximumDecisionsPerFrame,
                maximumPathSearchesPerFrame);
        }
        public void RunSchedulerTick(float deltaTime)
        {
            profileClock.Advance(deltaTime);
            Scheduler.RunManualTick(deltaTime);
        }
        public void AdvanceProfileClock(float deltaTime)
        {
            profileClock.Advance(deltaTime);
        }
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
            gridSystemOverride?.Dispose();
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
            // The isolated scheduler profile has no authored dungeon entry or
            // CharacterSpawner. ExitDungeon is therefore outside its stated
            // scope and would only exercise a missing fixture dependency.
            brain.availableActions = AiDebugScenarioActionFactory
                .CreateCustomerActions()
                .Where(action => action?.actionset?.Branch
                    != CharacterAiBranch.ExitDungeon)
                .ToArray();
            BehaviorTree behaviorTree = obj.AddComponent<BehaviorTree>();
            behaviorTree.StartWhenEnabled = false;
            behaviorTree.ExternalBehavior = externalBehavior;
            CharacterActor character = obj.AddComponent<CharacterActor>();
            CharacterAwakeMethod?.Invoke(character, null);
            CharacterAiEditorTestDependencies.Inject(
                obj,
                Scheduling,
                profileClock,
                profilePathSearchBroker,
                profileFrameWorkBudget,
                profileFacilityCandidateCache);

            CharacterSO data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.Customer,
                $"Stress Customer {speciesTag}",
                speciesTag);
            scriptableObjects.Add(data);
            SetPrivateField(data, "frequencyVisitMin", 3);
            SetPrivateField(data, "frequencyVisitMax", 3);
            SetPrivateField(data, "minHoldingMoney", 500);
            SetPrivateField(data, "maxHoldingMoney", 600);
            ApplyStressPersona(obj.GetComponent<CustomerPersonaRuntime>(), speciesTag);
            obj.transform.position = Grid.GetWorldPos(position);
            character.Initialization(data);
            // Character initialization augments all visitor action sets with
            // ExitDungeon. The isolated scheduler profile deliberately has no
            // authored entry/spawner, so remove that out-of-scope action after
            // the production normalization pass.
            brain.availableActions = brain.availableActions
                .Where(action => action?.actionset?.Branch
                    != CharacterAiBranch.ExitDungeon)
                .ToArray();
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
            FieldInfo field = target?.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(
                    target?.GetType().FullName ?? "<null>",
                    fieldName);
            }
            field.SetValue(target, value);
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }

    private sealed class TestStairOccupant :
        IGridOccupant,
        IGridMovementOccupant,
        IGridMovementHandler
    {
        public int GridId => -1;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
        public GridMoveType GridMoveType => GridMoveType.Stair;

        public System.Collections.IEnumerator Traverse(
            IBuildingVisitorPort actor,
            GridMoveStep step)
        {
            if (actor != null)
            {
                yield return actor.MoveToGrid(step.To);
            }
        }
    }
}
