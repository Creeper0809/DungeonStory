using System;
using System.Diagnostics;
using DungeonStory.Foundation;
using VContainer.Unity;

public readonly struct GameplayOutcomeConsolidationPumpDiagnostics
{
    public GameplayOutcomeConsolidationPumpDiagnostics(
        int lastObservedDay,
        long scheduledCutoff,
        long sliceCount,
        long examinedCount,
        long publishedCount,
        int schedulingFaultCount,
        string lastDetailCode)
    {
        LastObservedDay = lastObservedDay;
        ScheduledCutoff = scheduledCutoff;
        SliceCount = sliceCount;
        ExaminedCount = examinedCount;
        PublishedCount = publishedCount;
        SchedulingFaultCount = schedulingFaultCount;
        LastDetailCode = lastDetailCode ?? string.Empty;
    }
    public int LastObservedDay { get; }
    public long ScheduledCutoff { get; }
    public long SliceCount { get; }
    public long ExaminedCount { get; }
    public long PublishedCount { get; }
    public int SchedulingFaultCount { get; }
    public string LastDetailCode { get; }
}

public interface IGameplayOutcomeConsolidationPumpDiagnostics
{
    GameplayOutcomeConsolidationPumpDiagnostics GetConsolidationPumpDiagnostics();
}

/// <summary>
/// Main-thread, single-writer day-boundary scheduler and bounded PlayerLoop
/// pump. It invokes no Unity API and uses Stopwatch ticks as the slice budget.
/// </summary>
public sealed class GameplayOutcomeConsolidationRuntime :
    IInitializable,
    ITickable,
    IDisposable,
    IGameplayOutcomeConsolidationPumpDiagnostics
{
    private const int MaximumRecordsPerTick = 4;
    private static readonly long MaximumStopwatchTicksPerTick =
        Math.Max(1L, Stopwatch.Frequency / 500L);

    private readonly IGameEventBus events;
    private readonly IGameplayOutcomeConsolidationService consolidation;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly IGameplayOutcomeRegistry registry;
    private IDisposable dayStartedSubscription;
    private long lastObservedWorldEpoch;
    private int lastObservedDay = -1;
    private long scheduledCutoff;
    private long sliceCount;
    private long examinedCount;
    private long publishedCount;
    private int schedulingFaultCount;
    private string lastDetailCode = string.Empty;

    public GameplayOutcomeConsolidationRuntime(
        IGameEventBus events,
        IGameplayOutcomeConsolidationService consolidation,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameplayOutcomeRegistry registry)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.consolidation = consolidation ?? throw new ArgumentNullException(nameof(consolidation));
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public void Initialize()
    {
        dayStartedSubscription = events.Subscribe<OperatingDayStartedEvent>(OnDayStarted);
    }

    public void Tick()
    {
        if (consolidation.PendingJobCount <= 0) return;
        GameplayOutcomeConsolidationSliceResult result = consolidation.ProcessSlice(
            MaximumRecordsPerTick,
            MaximumStopwatchTicksPerTick);
        sliceCount++;
        examinedCount += result.Examined;
        publishedCount += result.Published;
        lastDetailCode = result.StatusCode;
    }

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        dayStartedSubscription = null;
    }

    public GameplayOutcomeConsolidationPumpDiagnostics GetConsolidationPumpDiagnostics() =>
        new GameplayOutcomeConsolidationPumpDiagnostics(
            lastObservedDay,
            scheduledCutoff,
            sliceCount,
            examinedCount,
            publishedCount,
            schedulingFaultCount,
            lastDetailCode);

    private void OnDayStarted(OperatingDayStartedEvent started)
    {
        GameplayOutcomeLedgerDiagnostics snapshot = diagnostics.GetDiagnostics();
        if (snapshot.WorldEpoch != lastObservedWorldEpoch)
        {
            lastObservedWorldEpoch = snapshot.WorldEpoch;
            lastObservedDay = -1;
            scheduledCutoff = 0L;
        }
        if (started.day < 0 || started.day <= lastObservedDay) return;
        long cutoff = snapshot.NextSequence - 1L;
        GameplayOutcomeConsolidationRequest request = new GameplayOutcomeConsolidationRequest(
            started.day,
            cutoff,
            registry.ConsolidationPolicyVersion,
            snapshot.WorldEpoch);
        if (!consolidation.TrySchedule(request, out string failureCode))
        {
            if (schedulingFaultCount < int.MaxValue) schedulingFaultCount++;
            lastDetailCode = failureCode;
            return;
        }
        lastObservedDay = started.day;
        scheduledCutoff = cutoff;
        lastDetailCode = string.Empty;
    }
}
