using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Profiling;
using UnityEditorInternal;
#endif

public sealed class GameplayRawProfilerSnapshotCollector : IDisposable
{
    private readonly List<SlowFrameProfile> slowFrameProfiles =
        new List<SlowFrameProfile>();
    private int[] profilerFrameIndices;
#if UNITY_EDITOR
    private bool originalProfilerEnabled;
    private bool captureActive;
#endif

    public void Begin(GameplayPerformanceOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        profilerFrameIndices = options.CaptureRawProfiler
            ? new int[GameplayPerformanceMeasurementSession.MaximumSamples]
            : null;
        slowFrameProfiles.Clear();
#if UNITY_EDITOR
        if (!options.CaptureRawProfiler || captureActive)
        {
            return;
        }

        originalProfilerEnabled = ProfilerDriver.enabled;
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.enabled = true;
        captureActive = true;
#endif
    }

    internal void RecordFrame(int sampleIndex)
    {
#if UNITY_EDITOR
        if (profilerFrameIndices != null
            && sampleIndex >= 0
            && sampleIndex < profilerFrameIndices.Length)
        {
            profilerFrameIndices[sampleIndex] = ProfilerDriver.lastFrameIndex;
        }
#endif
    }

    internal void CaptureRecentAllocationHotspots(
        GameplayPerformanceSampleSnapshot samples)
    {
        if (samples == null) throw new ArgumentNullException(nameof(samples));
#if UNITY_EDITOR
        if (profilerFrameIndices == null || samples.Count <= 0)
        {
            return;
        }

        const int RetainedProfilerFrameWindow = 180;
        int first = Mathf.Max(0, samples.Count - RetainedProfilerFrameWindow);
        List<int> rankedIndices = new List<int>(samples.Count - first);
        for (int index = first; index < samples.Count; index++)
        {
            rankedIndices.Add(index);
        }

        rankedIndices.Sort((left, right) =>
            samples.Gc[right].CompareTo(samples.Gc[left]));
        HashSet<int> capturedFrames = new HashSet<int>();
        const int MaximumCapturedAllocationFrames = 5;
        for (int rank = 0;
            rank < rankedIndices.Count
                && capturedFrames.Count < MaximumCapturedAllocationFrames;
            rank++)
        {
            int sampleIndex = rankedIndices[rank];
            int profilerFrameIndex = profilerFrameIndices[sampleIndex];
            if (profilerFrameIndex <= 0
                || !capturedFrames.Add(profilerFrameIndex))
            {
                continue;
            }

            CaptureSlowProfilerFrame(
                samples.Frame[sampleIndex],
                profilerFrameIndex);
        }
#endif
    }

    internal SlowFrameProfile[] CaptureProfiles()
    {
        return slowFrameProfiles.ToArray();
    }

    public void End()
    {
#if UNITY_EDITOR
        if (!captureActive)
        {
            return;
        }

        ProfilerDriver.enabled = originalProfilerEnabled;
        captureActive = false;
#endif
    }

    public void Dispose()
    {
        End();
    }

#if UNITY_EDITOR
    private void CaptureSlowProfilerFrame(
        float measuredFrameMilliseconds,
        int frameIndex)
    {
        using RawFrameDataView view =
            ProfilerDriver.GetRawFrameDataView(frameIndex, 0);
        if (!view.valid || view.sampleCount <= 0)
        {
            return;
        }

        List<SlowFrameSample> samples = new List<SlowFrameSample>(64);
        for (int sampleIndex = 0; sampleIndex < view.sampleCount; sampleIndex++)
        {
            float sampleMilliseconds = view.GetSampleTimeMs(sampleIndex);
            if (sampleMilliseconds < 0.2f)
            {
                continue;
            }

            string sampleName = view.GetSampleName(sampleIndex);
            if (string.IsNullOrWhiteSpace(sampleName)
                || string.Equals(sampleName, "PlayerLoop", StringComparison.Ordinal)
                || string.Equals(sampleName, "Main Thread", StringComparison.Ordinal))
            {
                continue;
            }

            samples.Add(new SlowFrameSample
            {
                name = sampleName,
                milliseconds = sampleMilliseconds
            });
        }

        samples.Sort((left, right) =>
            right.milliseconds.CompareTo(left.milliseconds));
        if (samples.Count > 24)
        {
            samples.RemoveRange(24, samples.Count - 24);
        }

        Dictionary<string, long> allocationBytesByPath =
            new Dictionary<string, long>(StringComparer.Ordinal);
        List<string> samplePath = new List<string>(32);
        int allocationSampleIndex = 0;
        while (allocationSampleIndex < view.sampleCount)
        {
            CollectAllocationSamples(
                view,
                ref allocationSampleIndex,
                samplePath,
                allocationBytesByPath);
        }

        SlowFrameAllocation[] allocations = allocationBytesByPath
            .Select(pair => new SlowFrameAllocation
            {
                path = pair.Key,
                bytes = pair.Value
            })
            .OrderByDescending(entry => entry.bytes)
            .Take(24)
            .ToArray();

        slowFrameProfiles.Add(new SlowFrameProfile
        {
            measuredFrameMilliseconds = measuredFrameMilliseconds,
            profilerFrameMilliseconds = view.frameTimeMs,
            profilerFrameIndex = frameIndex,
            samples = samples.ToArray(),
            allocations = allocations
        });
    }

    private static void CollectAllocationSamples(
        RawFrameDataView view,
        ref int sampleIndex,
        List<string> samplePath,
        Dictionary<string, long> allocationBytesByPath)
    {
        if (sampleIndex < 0 || sampleIndex >= view.sampleCount)
        {
            return;
        }

        int currentIndex = sampleIndex++;
        string sampleName = view.GetSampleName(currentIndex);
        int childCount = view.GetSampleChildrenCount(currentIndex);
        bool isAllocation = string.Equals(
            sampleName,
            "GC.Alloc",
            StringComparison.Ordinal);
        if (isAllocation && view.GetSampleMetadataCount(currentIndex) > 0)
        {
            long bytes = Math.Max(
                0L,
                view.GetSampleMetadataAsLong(currentIndex, 0));
            string path = BuildAllocationPath(samplePath);
            if (allocationBytesByPath.TryGetValue(path, out long existing))
            {
                allocationBytesByPath[path] = existing + bytes;
            }
            else
            {
                allocationBytesByPath[path] = bytes;
            }
        }

        bool includeInPath = !string.IsNullOrWhiteSpace(sampleName)
            && !isAllocation
            && !string.Equals(sampleName, "PlayerLoop", StringComparison.Ordinal)
            && !string.Equals(sampleName, "Main Thread", StringComparison.Ordinal);
        if (includeInPath)
        {
            samplePath.Add(sampleName);
        }

        for (int childIndex = 0; childIndex < childCount; childIndex++)
        {
            CollectAllocationSamples(
                view,
                ref sampleIndex,
                samplePath,
                allocationBytesByPath);
        }

        if (includeInPath)
        {
            samplePath.RemoveAt(samplePath.Count - 1);
        }
    }

    private static string BuildAllocationPath(List<string> samplePath)
    {
        if (samplePath == null || samplePath.Count == 0)
        {
            return "<root>";
        }

        const int MaximumPathSegments = 6;
        int first = Mathf.Max(0, samplePath.Count - MaximumPathSegments);
        return string.Join(" > ", samplePath.Skip(first));
    }
#endif
}
