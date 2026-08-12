using System;

public static class GameplayGcAcceptancePolicy
{
    // Editor values are regression signals measured against a paused-world baseline.
    public const int EditorBaselineWarmupFrames = 30;
    public const int EditorBaselineSampleFrames = 120;

    public const long EditorIncrementalAverageBytesPerFrame = 512L * 1024L;
    public const long EditorIncrementalP95BytesPerFrame = 2L * 1024L * 1024L;
    public const long EditorAbsoluteAverageRunawayBytesPerFrame = 16L * 1024L * 1024L;
    public const long EditorAbsoluteMaximumRunawayBytesPerFrame = 256L * 1024L * 1024L;

    // Player values apply only to steady-state gameplay. Save/load and explicit
    // bulk operations use the retained-heap and domain conservation gates instead.
    public const long PlayerSteadyAverageBytesPerFrame = 32L * 1024L;
    public const long PlayerSteadyP95BytesPerFrame = 128L * 1024L;
    public const long PlayerMaximumBytesInSingleFrame = 2L * 1024L * 1024L;
    public const long RetainedMonoGrowthBytes = 64L * 1024L * 1024L;

    public static double IncrementalBytes(double activeBytes, double baselineBytes) =>
        Math.Max(0d, activeBytes - baselineBytes);

    public static bool PassesEditorIncremental(double averageBytes, double p95Bytes) =>
        averageBytes <= EditorIncrementalAverageBytesPerFrame
        && p95Bytes <= EditorIncrementalP95BytesPerFrame;

    public static bool PassesEditorRunawayGuard(double averageBytes, double maximumBytes) =>
        averageBytes <= EditorAbsoluteAverageRunawayBytesPerFrame
        && maximumBytes <= EditorAbsoluteMaximumRunawayBytesPerFrame;

    public static bool PassesPlayerSteadyState(
        double averageBytes,
        double p95Bytes,
        double maximumBytes) =>
        averageBytes <= PlayerSteadyAverageBytesPerFrame
        && p95Bytes <= PlayerSteadyP95BytesPerFrame
        && maximumBytes <= PlayerMaximumBytesInSingleFrame;
}
