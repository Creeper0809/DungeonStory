using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using static GameplayPerformanceReportEvaluator;

public sealed class GameplayPerformanceMeasurementSession : IDisposable
{
    internal const int MaximumSamples = 7200;

    private readonly string[] runtimeTickMarkerNames =
    {
        "CaptivityRuntime.Tick",
        "CircusRuntime.Tick",
        "ExteriorActivityRuntime.Tick",
        "CharacterSkillGenerationService.Tick",
        "WorkOrderRuntime.Tick",
        "CharacterBodyHealthRuntime.Tick",
        "CharacterMedicalRuntime.Tick",
        "EquipmentMaintenancePolicyRuntime.Tick",
        "DefenseEngagementRuntime.Tick",
        "OffenseReturnArrivalRuntime.Tick",
        "CharacterDeprivationRuntime.Tick",
        "WorldWaterRuntime.Tick",
        "WildlifeCaptureRuntime.Tick",
        "WildlifeEcosystemRuntime.Tick",
        "WildlifeRuntime.Tick",
        "AnimalHusbandryRuntime.Tick",
        "FirstRunObjectiveRuntime.Tick",
        "RoomLayoutCache.Rebuild"
    };

    private readonly GameplayPerformanceReportAssembler reportAssembler;
    private readonly GameplayRawProfilerSnapshotCollector rawProfilerCollector;
    private ProfilerRecorder mainThreadRecorder;
    private ProfilerRecorder renderThreadRecorder;
    private ProfilerRecorder gcAllocationRecorder;
    private ProfilerRecorder gcCollectRecorder;
    private ProfilerRecorder aiBudgetRecorder;
    private ProfilerRecorder characterStatsRecorder;
    private ProfilerRecorder aiDirectorRecorder;
    private ProfilerRecorder abilityMoveRecorder;
    private ProfilerRecorder abilityWorkRecorder;
    private ProfilerRecorder[] runtimeTickRecorders;

    public GameplayPerformanceMeasurementSession(
        GameplayRawProfilerSnapshotCollector rawProfilerCollector)
        : this(new GameplayPerformanceReportAssembler(), rawProfilerCollector)
    {
    }

    internal GameplayPerformanceMeasurementSession(
        GameplayPerformanceReportAssembler reportAssembler,
        GameplayRawProfilerSnapshotCollector rawProfilerCollector)
    {
        this.reportAssembler = reportAssembler
            ?? throw new ArgumentNullException(nameof(reportAssembler));
        this.rawProfilerCollector = rawProfilerCollector
            ?? throw new ArgumentNullException(nameof(rawProfilerCollector));
    }

    public int SampleCount { get; private set; }

    public IEnumerator Capture(
        GameplayPerformanceOptions options,
        GameplayPerformanceReport report)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (report == null) throw new ArgumentNullException(nameof(report));

        GameplayPerformanceSampleSnapshot samples =
            new GameplayPerformanceSampleSnapshot(
                MaximumSamples,
                runtimeTickMarkerNames);
        SampleCount = 0;
        StartRecorders();

        ForceManagedCollection();
        report.monoUsedBytesAfterStartCollection = Profiler.GetMonoUsedSizeLong();
        report.monoUsedBytesAtStart = Profiler.GetMonoUsedSizeLong();
        report.totalAllocatedBytesAtStart = Profiler.GetTotalAllocatedMemoryLong();
        float startedAt = Time.realtimeSinceStartup;
        while (SampleCount < MaximumSamples
            && Time.realtimeSinceStartup - startedAt < options.SampleSeconds)
        {
            yield return new WaitForEndOfFrame();
            int index = SampleCount++;
            samples.Frame[index] = Time.unscaledDeltaTime * 1000f;
            samples.MainThread[index] = ReadRecorderMilliseconds(mainThreadRecorder);
            samples.RenderThread[index] = ReadRecorderMilliseconds(renderThreadRecorder);
            samples.GcCollect[index] = ReadRecorderMilliseconds(gcCollectRecorder);
            samples.AiBudget[index] = ReadRecorderMilliseconds(aiBudgetRecorder);
            samples.CharacterStats[index] = ReadRecorderMilliseconds(characterStatsRecorder);
            samples.AiDirector[index] = ReadRecorderMilliseconds(aiDirectorRecorder);
            samples.AbilityMove[index] = ReadRecorderMilliseconds(abilityMoveRecorder);
            samples.AbilityWork[index] = ReadRecorderMilliseconds(abilityWorkRecorder);
            for (int markerIndex = 0;
                markerIndex < runtimeTickRecorders.Length;
                markerIndex++)
            {
                samples.RuntimeTicks[markerIndex][index] =
                    ReadRecorderMilliseconds(runtimeTickRecorders[markerIndex]);
            }

            samples.Gc[index] = gcAllocationRecorder.Valid
                ? Math.Max(0, gcAllocationRecorder.LastValue)
                : 0;
            samples.MonoUsed[index] = Profiler.GetMonoUsedSizeLong();
            rawProfilerCollector.RecordFrame(index);
        }

        samples.Count = SampleCount;
        report.sampleDurationSeconds = Time.realtimeSinceStartup - startedAt;
        report.sampleCount = SampleCount;
        report.monoUsedBytesAtEnd = Profiler.GetMonoUsedSizeLong();
        report.totalAllocatedBytesAtEnd = Profiler.GetTotalAllocatedMemoryLong();
        ForceManagedCollection();
        report.monoUsedBytesAfterEndCollection = Profiler.GetMonoUsedSizeLong();
        rawProfilerCollector.CaptureRecentAllocationHotspots(samples);
        reportAssembler.Apply(
            report,
            options,
            samples,
            rawProfilerCollector.CaptureProfiles());
        Dispose();
    }

#if UNITY_EDITOR
    public IEnumerator CaptureEditorGcBaseline(GameplayPerformanceReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));

        const int BaselineWarmupFrames = 120;
        const int BaselineSampleFrames = 240;
        for (int frame = 0; frame < BaselineWarmupFrames; frame++)
        {
            yield return null;
        }

        ProfilerRecorder recorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Memory,
            "GC Allocated In Frame",
            1);
        long totalBytes = 0;
        int recordedFrames = 0;
        try
        {
            for (int frame = 0; frame < BaselineSampleFrames; frame++)
            {
                yield return new WaitForEndOfFrame();
                if (!recorder.Valid)
                {
                    continue;
                }

                totalBytes += Math.Max(0L, recorder.LastValue);
                recordedFrames++;
            }
        }
        finally
        {
            recorder.Dispose();
        }

        report.editorBaselineGcAverageBytes = recordedFrames > 0
            ? totalBytes / (double)recordedFrames
            : 0d;
    }
#endif

    public void Dispose()
    {
        DisposeRecorder(ref mainThreadRecorder);
        DisposeRecorder(ref renderThreadRecorder);
        DisposeRecorder(ref gcAllocationRecorder);
        DisposeRecorder(ref gcCollectRecorder);
        DisposeRecorder(ref aiBudgetRecorder);
        DisposeRecorder(ref characterStatsRecorder);
        DisposeRecorder(ref aiDirectorRecorder);
        DisposeRecorder(ref abilityMoveRecorder);
        DisposeRecorder(ref abilityWorkRecorder);
        if (runtimeTickRecorders == null)
        {
            return;
        }

        for (int index = 0; index < runtimeTickRecorders.Length; index++)
        {
            DisposeRecorder(ref runtimeTickRecorders[index]);
        }
    }

    private void StartRecorders()
    {
        runtimeTickRecorders = new ProfilerRecorder[runtimeTickMarkerNames.Length];
        for (int markerIndex = 0;
            markerIndex < runtimeTickMarkerNames.Length;
            markerIndex++)
        {
            runtimeTickRecorders[markerIndex] =
                StartRecorderByName(runtimeTickMarkerNames[markerIndex]);
        }

        mainThreadRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal,
            "Main Thread",
            1);
        renderThreadRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Internal,
            "Render Thread",
            1);
        gcAllocationRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Memory,
            "GC Allocated In Frame",
            1);
        gcCollectRecorder = StartRecorderByName("GC.Collect");
        aiBudgetRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts,
            "CharacterAiScheduler.ProcessAiBudget",
            1);
        characterStatsRecorder = StartRecorderByName(
            "CharacterStatMaintenanceRuntime.Tick");
        aiDirectorRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts,
            "Assembly-CSharp.dll!::AiDirectorRuntime.Update() [Invoke]",
            1);
        abilityMoveRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts,
            "Assembly-CSharp.dll!::AbilityMove.Move2PosBySpeedInternal() [Coroutine: MoveNext] [Invoke]",
            1);
        abilityWorkRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts,
            "Assembly-CSharp.dll!::AbilityWork.Work() [Coroutine: MoveNext] [Invoke]",
            1);
    }

    private static ProfilerRecorder StartRecorderByName(string markerName)
    {
        List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        for (int index = 0; index < handles.Count; index++)
        {
            ProfilerRecorderHandle handle = handles[index];
            if (!handle.Valid)
            {
                continue;
            }

            ProfilerRecorderDescription description =
                ProfilerRecorderHandle.GetDescription(handle);
            if (!string.Equals(
                    description.Name,
                    markerName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            return new ProfilerRecorder(
                handle,
                1,
                ProfilerRecorderOptions.Default
                    | ProfilerRecorderOptions.StartImmediately);
        }

        return default;
    }

    private static void ForceManagedCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static void DisposeRecorder(ref ProfilerRecorder recorder)
    {
        if (recorder.Valid)
        {
            recorder.Dispose();
        }
    }
}

internal sealed class GameplayPerformanceSampleSnapshot
{
    public GameplayPerformanceSampleSnapshot(
        int maximumSamples,
        IReadOnlyList<string> runtimeMarkerNames)
    {
        if (runtimeMarkerNames == null)
        {
            throw new ArgumentNullException(nameof(runtimeMarkerNames));
        }

        Frame = new float[maximumSamples];
        MainThread = new float[maximumSamples];
        RenderThread = new float[maximumSamples];
        GcCollect = new float[maximumSamples];
        AiBudget = new float[maximumSamples];
        CharacterStats = new float[maximumSamples];
        AiDirector = new float[maximumSamples];
        AbilityMove = new float[maximumSamples];
        AbilityWork = new float[maximumSamples];
        RuntimeTickNames = new string[runtimeMarkerNames.Count];
        RuntimeTicks = new float[runtimeMarkerNames.Count][];
        for (int index = 0; index < runtimeMarkerNames.Count; index++)
        {
            RuntimeTickNames[index] = runtimeMarkerNames[index];
            RuntimeTicks[index] = new float[maximumSamples];
        }

        Gc = new long[maximumSamples];
        MonoUsed = new long[maximumSamples];
    }

    public int Count { get; set; }
    public float[] Frame { get; }
    public float[] MainThread { get; }
    public float[] RenderThread { get; }
    public float[] GcCollect { get; }
    public float[] AiBudget { get; }
    public float[] CharacterStats { get; }
    public float[] AiDirector { get; }
    public float[] AbilityMove { get; }
    public float[] AbilityWork { get; }
    public string[] RuntimeTickNames { get; }
    public float[][] RuntimeTicks { get; }
    public long[] Gc { get; }
    public long[] MonoUsed { get; }
}
