using System;
using UnityEngine;
using static GameplayPerformanceReportEvaluator;

internal sealed class GameplayPerformanceReportAssembler
{
    private const float MixedPopulationSchedulerP95TargetMilliseconds = 4f;
    private const long MixedPopulationAverageGcTargetBytes = 64L * 1024L;
    private const long MixedPopulationMemoryGrowthTargetBytes = 16L * 1024L * 1024L;

    internal void Apply(
        GameplayPerformanceReport report,
        GameplayPerformanceOptions options,
        GameplayPerformanceSampleSnapshot samples,
        SlowFrameProfile[] slowFrameProfiles)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (samples == null) throw new ArgumentNullException(nameof(samples));

        int sampleCount = samples.Count;
        report.frame = FrameMetric.From(samples.Frame, sampleCount);
        report.mainThread = FrameMetric.FromPositive(samples.MainThread, sampleCount);
        report.renderThread = FrameMetric.FromPositive(samples.RenderThread, sampleCount);
        report.gcCollect = FrameMetric.FromPositive(samples.GcCollect, sampleCount);
        report.aiBudget = FrameMetric.FromPositive(samples.AiBudget, sampleCount);
        report.characterStats = FrameMetric.FromPositive(samples.CharacterStats, sampleCount);
        report.aiDirector = FrameMetric.FromPositive(samples.AiDirector, sampleCount);
        report.abilityMove = FrameMetric.FromPositive(samples.AbilityMove, sampleCount);
        report.abilityWork = FrameMetric.FromPositive(samples.AbilityWork, sampleCount);
        report.runtimeTicks = new NamedFrameMetric[samples.RuntimeTickNames.Length];
        for (int markerIndex = 0;
            markerIndex < report.runtimeTicks.Length;
            markerIndex++)
        {
            report.runtimeTicks[markerIndex] = new NamedFrameMetric
            {
                name = samples.RuntimeTickNames[markerIndex],
                metric = FrameMetric.FromPositive(
                    samples.RuntimeTicks[markerIndex],
                    sampleCount)
            };
        }

        report.gc = AllocationMetric.From(samples.Gc, sampleCount);
        int quarterLength = Mathf.Max(1, sampleCount / 4);
        report.monoUsedFirstQuarterAverageBytes = AverageWindow(
            samples.MonoUsed,
            sampleCount,
            0,
            quarterLength);
        report.monoUsedLastQuarterAverageBytes = AverageWindow(
            samples.MonoUsed,
            sampleCount,
            Mathf.Max(0, sampleCount - quarterLength),
            quarterLength);
        report.sustainedMonoGrowthBytes =
            report.monoUsedLastQuarterAverageBytes
            - report.monoUsedFirstQuarterAverageBytes;
        report.retainedMonoGrowthBytes = Math.Max(
            0L,
            report.monoUsedBytesAfterEndCollection
                - report.monoUsedBytesAfterStartCollection);
        report.meetsSchedulerP95Target =
            report.aiBudget.p95 > 0f
            && report.aiBudget.p95
                <= MixedPopulationSchedulerP95TargetMilliseconds;
        report.gameplayIncrementalGcAverageBytes = Math.Max(
            0d,
            report.gc.averageBytes - report.editorBaselineGcAverageBytes);
#if UNITY_EDITOR
        report.usesEditorBaselineAdjustedGcTarget = options.IsEditorProfile;
#endif
        double evaluatedGcAverage = report.usesEditorBaselineAdjustedGcTarget
            ? report.gameplayIncrementalGcAverageBytes
            : report.gc.averageBytes;
        report.meetsAverageGcTarget =
            evaluatedGcAverage <= MixedPopulationAverageGcTargetBytes;
        report.meetsMemoryGrowthTarget =
            report.retainedMonoGrowthBytes
                <= MixedPopulationMemoryGrowthTargetBytes;
#if UNITY_EDITOR
        report.slowFrames = slowFrameProfiles ?? Array.Empty<SlowFrameProfile>();
#endif
        report.averageFps = report.frame.average > 0f
            ? 1000f / report.frame.average
            : 0f;
        report.onePercentLowFps = report.frame.p99 > 0f
            ? 1000f / report.frame.p99
            : 0f;
        report.framesOver16_67Ms = CountOver(
            samples.Frame,
            sampleCount,
            16.6667f);
        report.framesOver33_33Ms = CountOver(
            samples.Frame,
            sampleCount,
            33.3333f);
        report.meets60FpsP95 = report.frame.p95 <= 16.6667f;
        report.meets60FpsP99 = report.frame.p99 <= 16.6667f;
        report.meets60FpsEverySample = report.frame.maximum <= 16.6667f;
        report.meetsMixedPopulationTarget =
            report.meets60FpsP95
            && report.meetsSchedulerP95Target
            && report.meetsAverageGcTarget
            && report.meetsMemoryGrowthTarget;
    }
}
