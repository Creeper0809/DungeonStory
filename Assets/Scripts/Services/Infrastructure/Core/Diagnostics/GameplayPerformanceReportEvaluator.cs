using System;
using Unity.Profiling;
using UnityEngine;

public static class GameplayPerformanceReportEvaluator
{
    public static void Initialize(
        GameplayPerformanceReport report,
        GameplayPerformanceOptions options)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (options == null) throw new ArgumentNullException(nameof(options));

        report.profileId = options.ProfileId;
        report.utcTimestamp = DateTime.UtcNow.ToString("O");
        report.applicationVersion = Application.version;
        report.unityVersion = Application.unityVersion;
        report.operatingSystem = SystemInfo.operatingSystem;
        report.processor = SystemInfo.processorType;
        report.processorCount = SystemInfo.processorCount;
        report.systemMemoryMb = SystemInfo.systemMemorySize;
        report.graphicsDevice = SystemInfo.graphicsDeviceName;
        report.graphicsMemoryMb = SystemInfo.graphicsMemorySize;
        report.screenWidth = Screen.width;
        report.screenHeight = Screen.height;
        report.requestedActorCount = options.ActorCount;
        report.requestedLivestockCount = options.LivestockCount;
        report.requestedFacilityCount = options.FacilityCount;
        report.requestedGridWidth = options.GridWidth;
        report.requestedGridHeight = options.GridHeight;
        report.requestedActiveFloors = options.ActiveFloors;
        report.requestedSimulationSpeed = options.SimulationSpeed;
        report.vSyncDisabled = true;
        report.targetFrameRate = -1;
        report.measurementIncludesRendering = true;
        report.measurementIncludesUi = true;
        report.measurementIncludesPhysics = true;
        report.measurementUsesNormalNewRun = true;
        report.measurementUsesRealCharacterPrefab = true;
        report.measurementUsesRealBuildingObjects = true;
        report.measurementUsesRealWildlifeActors = true;
        report.measurementUsesAnimalHusbandryRuntime = true;
    }

    public static bool Validate(
        GameplayPerformanceReport report,
        GameplayPerformanceOptions options,
        int sampleCount)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (sampleCount < 120
            || report.errorCount > 0
            || report.gridWidth < options.GridWidth
            || report.gridHeight < options.GridHeight)
        {
            return false;
        }

        if (options.ActorCount > 0
            && report.actualActorCount < options.ActorCount)
        {
            return false;
        }

        if (options.LivestockCount > 0
            && (report.actualLivestockCount < options.LivestockCount
                || report.actualStressLivestockCount < options.LivestockCount
                || !report.meetsMixedPopulationTarget))
        {
            return false;
        }

        return options.FacilityCount <= 0
            || report.actualDenseFacilityCount >= options.FacilityCount;
    }

    public static string BuildFailureReason(
        GameplayPerformanceReport report,
        GameplayPerformanceOptions options,
        int sampleCount)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (options == null) throw new ArgumentNullException(nameof(options));
        return $"samples={sampleCount}; errors={report.errorCount}; "
            + $"actors={report.actualActorCount}/{options.ActorCount}; "
            + $"livestock={report.actualLivestockCount}/{options.LivestockCount}; "
            + $"facilities={report.actualDenseFacilityCount}/{options.FacilityCount}; "
            + $"grid={report.gridWidth}x{report.gridHeight}; "
            + $"frameP95={report.frame?.p95 ?? 0f:0.###}; "
            + $"schedulerP95={report.aiBudget?.p95 ?? 0f:0.###}; "
            + $"avgGcBytes={report.gc?.averageBytes ?? 0d:0}; "
            + $"baselineGcBytes={report.editorBaselineGcAverageBytes:0}; "
            + $"incrementalGcBytes={report.gameplayIncrementalGcAverageBytes:0}; "
            + $"sustainedMonoGrowthBytes={report.sustainedMonoGrowthBytes}; "
            + $"retainedMonoGrowthBytes={report.retainedMonoGrowthBytes}";
    }

    public static float ReadRecorderMilliseconds(ProfilerRecorder recorder)
    {
        return recorder.Valid && recorder.LastValue > 0
            ? recorder.LastValue / 1_000_000f
            : 0f;
    }

    public static int CountOver(float[] samples, int count, float threshold)
    {
        int result = 0;
        for (int index = 0; index < count; index++)
        {
            if (samples[index] > threshold)
            {
                result++;
            }
        }

        return result;
    }

    public static long AverageWindow(
        long[] values,
        int count,
        int start,
        int length)
    {
        if (values == null || count <= 0 || length <= 0)
        {
            return 0L;
        }

        int from = Mathf.Clamp(start, 0, count - 1);
        int to = Mathf.Clamp(from + length, from + 1, count);
        decimal sum = 0m;
        for (int index = from; index < to; index++)
        {
            sum += Math.Max(0L, values[index]);
        }

        return (long)(sum / Mathf.Max(1, to - from));
    }
}
