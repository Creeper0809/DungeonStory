using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterAiPerfSettings",
    menuName = "DungeonStory/AI/Performance Settings")]
public sealed class CharacterAiPerfSettingsSO : ScriptableObject
{
    [Header("Frame Targets")]
    [SerializeField, Min(0.1f)] private float targetSchedulerAverageMs = 2f;
    [SerializeField, Min(0.1f)] private float targetSchedulerP95Ms = 4f;
    [SerializeField, Min(1f)] private float targetGcKbPerFrame = 64f;
    [SerializeField, Range(64, 4096)] private int sampleCapacity = 512;

    [Header("Tick LOD")]
    [SerializeField, Min(0.01f)] private float selectedTickInterval = 0.15f;
    [SerializeField, Min(0.01f)] private float visibleTickInterval = 0.35f;
    [SerializeField, Min(0.1f)] private float offscreenIdleTickInterval = 1.5f;
    [SerializeField, Min(0.1f)] private float offscreenLongWorkTickInterval = 4f;

    public float TargetSchedulerAverageMs => targetSchedulerAverageMs;
    public float TargetSchedulerP95Ms => targetSchedulerP95Ms;
    public float TargetGcKbPerFrame => targetGcKbPerFrame;
    public int SampleCapacity => sampleCapacity;
    public float SelectedTickInterval => selectedTickInterval;
    public float VisibleTickInterval => visibleTickInterval;
    public float OffscreenIdleTickInterval => offscreenIdleTickInterval;
    public float OffscreenLongWorkTickInterval => offscreenLongWorkTickInterval;
}

[Serializable]
public sealed class CharacterAiPerformanceReport
{
    public bool valid;
    public int actorCount;
    public int sampleFrames;
    public CharacterAiPerformanceMetric scheduler = new CharacterAiPerformanceMetric("Scheduler");
    public CharacterAiPerformanceMetric behaviorTree = new CharacterAiPerformanceMetric("BT");
    public CharacterAiPerformanceMetric pathBroker = new CharacterAiPerformanceMetric("Grid.SearchPath");
    public CharacterAiPerformanceMetric garbageCollection = new CharacterAiPerformanceMetric("GC");
    public List<CharacterAiPerformanceMetric> metrics = new List<CharacterAiPerformanceMetric>();
    public int brokerSearches;
    public int brokerCacheHits;
    public int brokerBudgetDeferrals;
    public string summary;
}

public enum AiPerformanceCategory
{
    Scheduler,
    BehaviorTree,
    DecisionContext,
    DomainSelection,
    ActionScoring,
    WorldSignal,
    FacilityScoring,
    WorkTargetSelection,
    HaulPlanning,
    Wildlife,
    PathSearch,
    UiFeedback,
    WorldSignalSpatialIndex,
    WorldSignalProximity,
    WorldSignalEnvironment,
    ActionPrepare,
    ActionConsiderationScore,
    ActionCanStart,
    ActionResolveDestination,
    FacilityCandidateSource,
    FacilityCandidateLoop,
    DecisionContextNeeds,
    DecisionContextAbilities,
    DecisionContextWorldSignal,
    FacilityAvailability
}

public interface ICharacterAiPerfSettingsProvider
{
    CharacterAiPerfSettingsSO Settings { get; }
}

public interface ICharacterAiPerformanceRecorder
{
    bool DetailedCollectionEnabled { get; }
    void Record(AiPerformanceCategory category, double elapsedMilliseconds, long gcBytes = 0);
    void RecordPathCounters(int searches, int cacheHits, int budgetDeferrals);
    CharacterAiPerformanceReport CaptureReport(int actorCount);
    void Reset();
}

public static class CharacterAiPerformanceCaptureControl
{
    private static int detailedCaptureCount;
    private static int slowTraceCount;

    public static bool IsDetailedCaptureRequested =>
        Volatile.Read(ref detailedCaptureCount) > 0;
    public static bool IsSlowTraceRequested =>
        Volatile.Read(ref slowTraceCount) > 0;

    public static void BeginDetailedCapture()
    {
        if (Interlocked.Increment(ref detailedCaptureCount) == 1)
        {
            CharacterAiSlowOperationTrace.Reset();
        }
    }

    public static void EndDetailedCapture()
    {
        int current;
        do
        {
            current = Volatile.Read(ref detailedCaptureCount);
            if (current <= 0)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(
                   ref detailedCaptureCount,
                   current - 1,
                   current) != current);
    }

    public static void BeginSlowTrace()
    {
        if (Interlocked.Increment(ref slowTraceCount) == 1)
        {
            CharacterAiSlowOperationTrace.Reset();
        }
    }

    public static void EndSlowTrace()
    {
        int current;
        do
        {
            current = Volatile.Read(ref slowTraceCount);
            if (current <= 0)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(
                   ref slowTraceCount,
                   current - 1,
                   current) != current);
    }
}

internal static class CharacterAiSlowOperationTrace
{
    private const string DetailedProfileArgument = "-ai-detailed-performance";
    private const double SlowThresholdMilliseconds = 4d;
    private const int MaximumEntries = 64;

    private static readonly bool enabled = Array.Exists(
        Environment.GetCommandLineArgs(),
        argument => string.Equals(
            argument,
            DetailedProfileArgument,
            StringComparison.OrdinalIgnoreCase));
    private static int entryCount;

    public static bool Enabled => (enabled
            || CharacterAiPerformanceCaptureControl.IsSlowTraceRequested)
        && Volatile.Read(ref entryCount) < MaximumEntries;

    public static void Reset()
    {
        Interlocked.Exchange(ref entryCount, 0);
    }

    public static void Record(
        string stage,
        CharacterActor actor,
        AIActionSet actionSet,
        Consideration consideration,
        double elapsedMilliseconds)
    {
        if ((!enabled
                && !CharacterAiPerformanceCaptureControl.IsSlowTraceRequested)
            || elapsedMilliseconds < SlowThresholdMilliseconds)
        {
            return;
        }

        int sequence = Interlocked.Increment(ref entryCount);
        if (sequence > MaximumEntries)
        {
            return;
        }

        string actorId = actor?.Identity?.PersistentId;
        Debug.Log(
            $"AI_SLOW_OPERATION #{sequence} "
            + $"stage={stage ?? "unknown"} "
            + $"elapsedMs={elapsedMilliseconds:0.000} "
            + $"actor={actorId ?? actor?.name ?? "none"} "
            + $"action={actionSet?.GetType().Name ?? "none"} "
            + $"actionLabel={actionSet?.GetDisplayLabel() ?? "none"} "
            + $"consideration={consideration?.GetType().Name ?? "none"}");
    }
}

[Serializable]
public sealed class CharacterAiPerformanceMetric
{
    public string name;
    public int sampleCount;
    public double average;
    public double p95;
    public double max;
    public long gcBytes;

    public CharacterAiPerformanceMetric(string name)
    {
        this.name = name;
    }
}
