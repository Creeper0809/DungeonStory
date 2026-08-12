using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class GameplayPerformanceReport
{
    public string profileId;
    public string utcTimestamp;
    public string applicationVersion;
    public string unityVersion;
    public string operatingSystem;
    public string processor;
    public int processorCount;
    public int systemMemoryMb;
    public string graphicsDevice;
    public int graphicsMemoryMb;
    public int screenWidth;
    public int screenHeight;
    public bool aiSchedulerDisabled;
    public bool characterPresentationDisabled;
    public bool characterStatsUpdatesDisabled;
    public int requestedActorCount;
    public int requestedLivestockCount;
    public int requestedFacilityCount;
    public int requestedGridWidth;
    public int requestedGridHeight;
    public int requestedActiveFloors;
    public float requestedSimulationSpeed;
    public int normalOperationSupplyDays;
    public int normalOperationWarehouseCount;
    public int seededWarehouseFoodAmount;
    public int seededWarehouseWaterAmount;
    public int seededLooseFoodAmount;
    public int seededLooseWaterAmount;
    public int seededFoodAmount;
    public int seededWaterAmount;
    public int waterStockCandidateCount;
    public int storedWaterCandidateCount;
    public int looseWaterCandidateCount;
    public int storedWaterQuantity;
    public int looseWaterQuantity;
    public int availableWaterQuantity;
    public int[] waterCandidateCountByFloor;
    public int[] waterQuantityByFloor;
    public float minimumThirst;
    public float averageThirst;
    public float maximumThirst;
    public int actorsBelowSafeDrinkThreshold;
    public int actorsWithCriticalThirst;
    public int actorsWithThirstWarningBurden;
    public int actorsWithThirstBreakdownBurden;
    public int activeDeprivationBreakdowns;
    public int activeDesperateDrinkBreakdowns;
    public int safeReliefRequests;
    public int safeReliefPlanFailures;
    public int safeReliefActionsStarted;
    public int safeReliefStoredStackPlans;
    public int safeReliefMoveFailures;
    public int safeReliefBreakdownMoveFailures;
    public int safeReliefBlockedMoveFailures;
    public int safeReliefOtherMoveFailures;
    public int safeReliefStaleStartFailures;
    public int safeReliefWallBlockedFailures;
    public int safeReliefDoorDeniedFailures;
    public int safeReliefDefenseReservationFailures;
    public int safeReliefTraversalChangedFailures;
    public int safeReliefArrivals;
    public int safeReliefInteractionAttempts;
    public int safeReliefSuccesses;
    public int safeReliefRunningActions;
    public int safeReliefActionsFinished;
    public long safeReliefPlannedPathSteps;
    public float safeReliefAveragePlannedPathSteps;
    public int safeReliefMaximumPlannedPathSteps;
    public float safeReliefAverageDurationSeconds;
    public float safeReliefMaximumDurationSeconds;
    public int safeReliefCancelledMoveFailures;
    public int safeReliefMissingPathFailures;
    public int safeReliefMissingMovementHandlerFailures;
    public int safeReliefGridUnavailableFailures;
    public int safeReliefInvalidSpeedFailures;
    public int safeReliefNoFailureReasonFailures;
    public int safeReliefActorDeadMoveFailures;
    public int safeReliefActorMissingMoveFailures;
    public int safeReliefCrossFloorTargetPlans;
    public int safeReliefPathsWithVerticalTraversal;
    public long safeReliefVerticalTraversalSteps;
    public int desperateDrinkAttempts;
    public int desperateDrinkStackMoveFailures;
    public int desperateDrinkStackArrivals;
    public int desperateDrinkStackConsumptions;
    public int actualActorCount;
    public int deadActorCount;
    public bool ownerPresent;
    public bool ownerAlive;
    public int actualStressActorCount;
    public int preexistingSkillGenerationRequestsCancelled;
    public bool syntheticSkillGenerationRequestsCancelled;
    public int actualWildlifeCount;
    public int actualLivestockCount;
    public int actualStressLivestockCount;
    public int actualBuildingCount;
    public int actualDenseFacilityCount;
    public int actualDenseDoorCount;
    public int activeRendererCount;
    public int visibleRendererCount;
    public int activeCanvasCount;
    public int activeNameplateCount;
    public double dynamicWorkSmoothedFrameMilliseconds;
    public double dynamicWorkAvailableMilliseconds;
    public double dynamicWorkConsumedMilliseconds;
    public int dynamicWorkBacklog;
    public int gridWidth;
    public int gridHeight;
    public int schedulerRegisteredCharacters;
    public int presentationRegisteredCharacters;
    public int presentationVisibleCharacters;
    public double schedulerLastMilliseconds;
    public int schedulerLastDecisions;
    public int schedulerLastLegacyFallbacks;
    public int schedulerLastPathSearches;
    public double schedulerCurrentBudgetMilliseconds;
    public double schedulerEstimatedDecisionMilliseconds;
    public double schedulerEstimatedPathMilliseconds;
    public double schedulerSmoothedFrameMilliseconds;
    public long schedulerProcessedDecisions;
    public long schedulerStarvedDecisions;
    public long schedulerSkippedDecisions;
    public long schedulerLegacyFallbacks;
    public float schedulerOldestDeferralSeconds;
    public float schedulerMaximumDeferralSeconds;
    public bool schedulerBudgetExhausted;
    public bool facilityCandidateIndexPending;
    public int facilityCandidateIndexVersion;
    public int sampleCount;
    public float sampleDurationSeconds;
    public double setupMilliseconds;
    public double totalProfileMilliseconds;
    public float averageFps;
    public float onePercentLowFps;
    public int framesOver16_67Ms;
    public int framesOver33_33Ms;
    public long monoUsedBytesAtStart;
    public long monoUsedBytesAtEnd;
    public long monoUsedBytesAfterStartCollection;
    public long monoUsedBytesAfterEndCollection;
    public long totalAllocatedBytesAtStart;
    public long totalAllocatedBytesAtEnd;
    public long monoUsedFirstQuarterAverageBytes;
    public long monoUsedLastQuarterAverageBytes;
    public long sustainedMonoGrowthBytes;
    public long retainedMonoGrowthBytes;
    public int editorBaselineGcSampleCount;
    public double editorBaselineGcAverageBytes;
    public long editorBaselineGcP95Bytes;
    public long editorBaselineGcMaximumBytes;
    public double gameplayIncrementalGcAverageBytes;
    public double gameplayIncrementalGcP95Bytes;
    public int warningCount;
    public int errorCount;
    public bool valid;
    public bool meets60FpsP95;
    public bool meets60FpsP99;
    public bool meets60FpsEverySample;
    public bool meetsSchedulerP95Target;
    public bool meetsAverageGcTarget;
    public bool meetsGcDistributionTarget;
    public bool meetsMemoryGrowthTarget;
    public bool meetsMixedPopulationTarget;
    public bool usesEditorBaselineAdjustedGcTarget;
    public bool isFinalGcAuthority;
    public string gcAcceptanceAuthority;
    public bool vSyncDisabled;
    public int targetFrameRate;
    public bool measurementIncludesRendering;
    public bool measurementIncludesUi;
    public bool measurementIncludesPhysics;
    public bool measurementUsesNormalNewRun;
    public bool measurementUsesRealCharacterPrefab;
    public bool measurementUsesRealBuildingObjects;
    public bool measurementUsesRealWildlifeActors;
    public bool measurementUsesAnimalHusbandryRuntime;
    public string failureReason;
    public string[] logMessages;
    public FrameMetric frame;
    public FrameMetric mainThread;
    public FrameMetric renderThread;
    public FrameMetric gcCollect;
    public FrameMetric aiBudget;
    public FrameMetric characterStats;
    public FrameMetric aiDirector;
    public FrameMetric abilityMove;
    public FrameMetric abilityWork;
    public NamedFrameMetric[] runtimeTicks;
    public AllocationMetric gc;
    public GameplayCharacterAiPerformanceReport aiPerformance;
    public SlowFrameProfile[] slowFrames;
}

[Serializable]
public sealed class GameplayCharacterAiPerformanceReport
{
    public bool valid;
    public int actorCount;
    public int sampleFrames;
    public GameplayCharacterAiPerformanceMetric scheduler;
    public GameplayCharacterAiPerformanceMetric behaviorTree;
    public GameplayCharacterAiPerformanceMetric pathBroker;
    public GameplayCharacterAiPerformanceMetric garbageCollection;
    public List<GameplayCharacterAiPerformanceMetric> metrics;
    public int brokerSearches;
    public int brokerCacheHits;
    public int brokerBudgetDeferrals;
    public string summary;
}

[Serializable]
public sealed class GameplayCharacterAiPerformanceMetric
{
    public string name;
    public int sampleCount;
    public double average;
    public double p95;
    public double max;
    public long gcBytes;
}

[Serializable]
public sealed class SlowFrameProfile
{
    public float measuredFrameMilliseconds;
    public float profilerFrameMilliseconds;
    public int profilerFrameIndex;
    public SlowFrameSample[] samples;
    public SlowFrameAllocation[] allocations;
}

[Serializable]
public sealed class NamedFrameMetric
{
    public string name;
    public FrameMetric metric;
}

[Serializable]
public sealed class SlowFrameSample
{
    public string name;
    public float milliseconds;
}

[Serializable]
public sealed class SlowFrameAllocation
{
    public string path;
    public long bytes;
}

[Serializable]
public sealed class FrameMetric
{
    public float average;
    public float p50;
    public float p95;
    public float p99;
    public float maximum;

    public static FrameMetric From(float[] samples, int count)
    {
        return Calculate(samples, count, includeZero: true);
    }

    public static FrameMetric FromPositive(float[] samples, int count)
    {
        return Calculate(samples, count, includeZero: false);
    }

    private static FrameMetric Calculate(
        float[] samples,
        int count,
        bool includeZero)
    {
        if (samples == null || count <= 0)
        {
            return new FrameMetric();
        }

        float[] sorted = new float[count];
        int validCount = 0;
        double sum = 0d;
        for (int i = 0; i < count; i++)
        {
            float value = samples[i];
            if (!includeZero && value <= 0f)
            {
                continue;
            }

            sorted[validCount++] = value;
            sum += value;
        }

        if (validCount == 0)
        {
            return new FrameMetric();
        }

        Array.Sort(sorted, 0, validCount);
        return new FrameMetric
        {
            average = (float)(sum / validCount),
            p50 = Percentile(sorted, validCount, 0.50f),
            p95 = Percentile(sorted, validCount, 0.95f),
            p99 = Percentile(sorted, validCount, 0.99f),
            maximum = sorted[validCount - 1]
        };
    }

    private static float Percentile(float[] sorted, int count, float percentile)
    {
        int index = Mathf.Clamp(
            Mathf.CeilToInt(count * percentile) - 1,
            0,
            count - 1);
        return sorted[index];
    }
}

[Serializable]
public sealed class AllocationMetric
{
    public double averageBytes;
    public long p95Bytes;
    public long maximumBytes;

    public static AllocationMetric From(long[] samples, int count)
    {
        if (samples == null || count <= 0)
        {
            return new AllocationMetric();
        }

        long[] sorted = new long[count];
        long maximum = 0;
        double sum = 0d;
        for (int i = 0; i < count; i++)
        {
            long value = Math.Max(0, samples[i]);
            sorted[i] = value;
            sum += value;
            maximum = Math.Max(maximum, value);
        }

        Array.Sort(sorted);
        int p95Index = Mathf.Clamp(
            Mathf.CeilToInt(count * 0.95f) - 1,
            0,
            count - 1);
        return new AllocationMetric
        {
            averageBytes = sum / count,
            p95Bytes = sorted[p95Index],
            maximumBytes = maximum
        };
    }
}

public sealed class GameplayPerformanceOptions
{
    public string ProfileId { get; private set; } = "actual-gameplay";
    public int ActorCount { get; private set; }
    public int LivestockCount { get; private set; }
    public int FacilityCount { get; private set; }
    public int GridWidth { get; private set; } = 60;
    public int GridHeight { get; private set; } = 3;
    public int ActiveFloors { get; private set; } = 3;
    public int NormalOperationSupplyDays { get; private set; }
    public float SimulationSpeed { get; private set; } = 1f;
    public int RoomSpan { get; private set; } = 16;
    public int WarmupFrames { get; private set; } = 300;
    public float SampleSeconds { get; private set; } = 12f;
    public float HoldSeconds { get; private set; } = 4f;
    public bool DisableAiScheduler { get; private set; }
    public bool DisableCharacterPresentation { get; private set; }
    public bool DisableCharacterStatsUpdates { get; private set; }
    public bool CaptureRawProfiler { get; private set; }
    public bool HasDiagnosticIsolation =>
        DisableAiScheduler
        || DisableCharacterPresentation
        || DisableCharacterStatsUpdates;
    public string ReportPath { get; private set; }
    public string ScreenshotPath { get; private set; }
    public bool IsEditorProfile { get; private set; }

#if UNITY_EDITOR
    public static GameplayPerformanceOptions CreateEditor(
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
        float simulationSpeed,
        bool disableAiScheduler,
        bool disableCharacterPresentation,
        bool disableCharacterStatsUpdates,
        bool captureRawProfiler,
        int livestockCount,
        int normalOperationSupplyDays)
    {
        return new GameplayPerformanceOptions
        {
            ProfileId = string.IsNullOrWhiteSpace(profileId)
                ? "editor-gameplay"
                : profileId,
            ActorCount = Mathf.Clamp(actorCount, 0, 5000),
            LivestockCount = Mathf.Clamp(livestockCount, 0, 5000),
            FacilityCount = Mathf.Clamp(facilityCount, 0, 100000),
            GridWidth = Mathf.Clamp(gridWidth, 1, 1024),
            GridHeight = Mathf.Clamp(gridHeight, 1, 1024),
            ActiveFloors = Mathf.Clamp(activeFloors, 1, Mathf.Max(1, gridHeight)),
            NormalOperationSupplyDays = Mathf.Clamp(
                normalOperationSupplyDays,
                0,
                30),
            SimulationSpeed = Mathf.Clamp(simulationSpeed, 0.1f, 5f),
            RoomSpan = 16,
            WarmupFrames = Mathf.Clamp(warmupFrames, 1, 3600),
            SampleSeconds = Mathf.Clamp(sampleSeconds, 2f, 120f),
            HoldSeconds = 2f,
            DisableAiScheduler = disableAiScheduler,
            DisableCharacterPresentation = disableCharacterPresentation,
            DisableCharacterStatsUpdates = disableCharacterStatsUpdates,
            CaptureRawProfiler = captureRawProfiler,
            ReportPath = Path.GetFullPath(reportPath),
            ScreenshotPath = Path.GetFullPath(screenshotPath),
            IsEditorProfile = true
        };
    }
#endif

    public static GameplayPerformanceOptions Parse(string[] arguments)
    {
        GameplayPerformanceOptions options = new GameplayPerformanceOptions();
        options.ProfileId = ReadString(
            arguments,
            "-performance-profile-id",
            options.ProfileId);
        options.ActorCount = ReadInt(
            arguments,
            "-performance-actors",
            0,
            0,
            5000);
        options.LivestockCount = ReadInt(
            arguments,
            "-performance-livestock",
            0,
            0,
            5000);
        options.FacilityCount = ReadInt(
            arguments,
            "-performance-facilities",
            0,
            0,
            100000);
        options.GridWidth = ReadInt(
            arguments,
            "-performance-grid-width",
            options.GridWidth,
            1,
            1024);
        options.GridHeight = ReadInt(
            arguments,
            "-performance-grid-height",
            options.GridHeight,
            1,
            1024);
        options.ActiveFloors = ReadInt(
            arguments,
            "-performance-active-floors",
            options.ActiveFloors,
            1,
            options.GridHeight);
        options.NormalOperationSupplyDays = ReadInt(
            arguments,
            "-performance-supply-days",
            0,
            0,
            30);
        options.SimulationSpeed = ReadFloat(
            arguments,
            "-performance-simulation-speed",
            options.SimulationSpeed,
            0.1f,
            5f);
        options.RoomSpan = ReadInt(
            arguments,
            "-performance-room-span",
            options.RoomSpan,
            8,
            128);
        options.WarmupFrames = ReadInt(
            arguments,
            "-performance-warmup-frames",
            options.WarmupFrames,
            1,
            3600);
        options.SampleSeconds = ReadFloat(
            arguments,
            "-performance-sample-seconds",
            options.SampleSeconds,
            2f,
            120f);
        options.HoldSeconds = ReadFloat(
            arguments,
            "-performance-hold-seconds",
            options.HoldSeconds,
            2f,
            60f);
        options.DisableAiScheduler = HasFlag(
            arguments,
            "-performance-disable-ai");
        options.DisableCharacterPresentation = HasFlag(
            arguments,
            "-performance-disable-character-presentation");
        options.DisableCharacterStatsUpdates = HasFlag(
            arguments,
            "-performance-disable-character-stats");

        string defaultDirectory = Path.Combine(
            Application.persistentDataPath,
            "Performance");
        options.ReportPath = Path.GetFullPath(ReadString(
            arguments,
            "-performance-report",
            Path.Combine(defaultDirectory, $"{options.ProfileId}.json")));
        options.ScreenshotPath = Path.GetFullPath(ReadString(
            arguments,
            "-performance-screenshot",
            Path.Combine(defaultDirectory, $"{options.ProfileId}.png")));
        return options;
    }

    private static bool HasFlag(string[] arguments, string key)
    {
        return arguments != null
            && arguments.Any(argument =>
                string.Equals(argument, key, StringComparison.OrdinalIgnoreCase));
    }

    private static int ReadInt(
        string[] arguments,
        string key,
        int fallback,
        int minimum,
        int maximum)
    {
        string value = ReadValue(arguments, key);
        return int.TryParse(value, out int parsed)
            ? Mathf.Clamp(parsed, minimum, maximum)
            : Mathf.Clamp(fallback, minimum, maximum);
    }

    private static float ReadFloat(
        string[] arguments,
        string key,
        float fallback,
        float minimum,
        float maximum)
    {
        string value = ReadValue(arguments, key);
        return float.TryParse(value, out float parsed)
            ? Mathf.Clamp(parsed, minimum, maximum)
            : Mathf.Clamp(fallback, minimum, maximum);
    }

    private static string ReadString(
        string[] arguments,
        string key,
        string fallback)
    {
        string value = ReadValue(arguments, key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ReadValue(string[] arguments, string key)
    {
        if (arguments == null)
        {
            return null;
        }

        for (int i = 0; i + 1 < arguments.Length; i++)
        {
            if (string.Equals(arguments[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[i + 1];
            }
        }

        return null;
    }
}
