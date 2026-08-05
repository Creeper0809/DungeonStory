using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ResourceCharacterAiPerfSettingsProvider : ICharacterAiPerfSettingsProvider
{
    public const string ResourcePath = "Config/CharacterAiPerfSettings";

    private readonly CharacterAiPerfSettingsSO settings;

    public ResourceCharacterAiPerfSettingsProvider(IGameContentCatalog content)
    {
        settings = (content ?? throw new ArgumentNullException(nameof(content)))
            .RequireSingle<CharacterAiPerfSettingsSO>();
    }

    public CharacterAiPerfSettingsSO Settings => settings;
}

public sealed class CharacterAiPerformanceRecorder : ICharacterAiPerformanceRecorder
{
    private const string DetailedProfileArgument = "-ai-detailed-performance";

    private sealed class SampleRing
    {
        private readonly double[] elapsedMilliseconds;
        private readonly long[] allocatedBytes;
        private int nextIndex;

        public SampleRing(int capacity)
        {
            elapsedMilliseconds = new double[Math.Max(64, capacity)];
            allocatedBytes = new long[elapsedMilliseconds.Length];
        }

        public int Count { get; private set; }

        public void Add(double elapsed, long gcBytes)
        {
            elapsedMilliseconds[nextIndex] = Math.Max(0d, elapsed);
            allocatedBytes[nextIndex] = Math.Max(0L, gcBytes);
            nextIndex = (nextIndex + 1) % elapsedMilliseconds.Length;
            Count = Math.Min(Count + 1, elapsedMilliseconds.Length);
        }

        public CharacterAiPerformanceMetric Capture(string name)
        {
            CharacterAiPerformanceMetric metric = new CharacterAiPerformanceMetric(name);
            if (Count <= 0)
            {
                return metric;
            }

            double[] sorted = new double[Count];
            double total = 0d;
            long totalGc = 0L;
            double maximum = 0d;
            for (int i = 0; i < Count; i++)
            {
                double elapsed = elapsedMilliseconds[i];
                sorted[i] = elapsed;
                total += elapsed;
                totalGc += allocatedBytes[i];
                maximum = Math.Max(maximum, elapsed);
            }

            Array.Sort(sorted);
            int p95Index = Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * 0.95d) - 1);
            metric.average = total / Count;
            metric.p95 = sorted[Math.Max(0, p95Index)];
            metric.max = maximum;
            metric.gcBytes = totalGc / Count;
            metric.sampleCount = Count;
            return metric;
        }

        public void Clear()
        {
            Array.Clear(elapsedMilliseconds, 0, elapsedMilliseconds.Length);
            Array.Clear(allocatedBytes, 0, allocatedBytes.Length);
            nextIndex = 0;
            Count = 0;
        }
    }

    private static readonly string[] CategoryNames =
    {
        "Scheduler",
        "BT",
        "DecisionContext",
        "DomainSelection",
        "ActionScoring",
        "WorldSignal",
        "FacilityScoring",
        "WorkTargetSelector",
        "Haul",
        "Wildlife",
        "Grid.SearchPath",
        "UI Feedback",
        "WorldSignal.SpatialIndex",
        "WorldSignal.Proximity",
        "WorldSignal.Environment",
        "Action.Prepare",
        "Action.Considerations",
        "Action.CanStart",
        "Action.ResolveDestination",
        "Facility.CandidateSource",
        "Facility.CandidateLoop",
        "DecisionContext.Needs",
        "DecisionContext.Abilities",
        "DecisionContext.WorldSignal",
        "Facility.Availability"
    };

    private readonly IDungeonUserSettingsService userSettings;
    private readonly ICharacterAiPerformanceCaptureScope captureScope;
    private readonly CharacterAiPerfSettingsSO settings;
    private readonly SampleRing[] samples;
    private readonly bool commandLineDetailedCollection;
    private int pathSearches;
    private int pathCacheHits;
    private int pathBudgetDeferrals;

    public CharacterAiPerformanceRecorder(
        IDungeonUserSettingsService userSettings,
        ICharacterAiPerfSettingsProvider settingsProvider,
        ICharacterAiPerformanceCaptureScope captureScope)
    {
        this.userSettings = userSettings
            ?? throw new ArgumentNullException(nameof(userSettings));
        this.captureScope = captureScope
            ?? throw new ArgumentNullException(nameof(captureScope));
        settings = settingsProvider?.Settings
            ?? throw new ArgumentNullException(nameof(settingsProvider));
        commandLineDetailedCollection = Array.Exists(
            Environment.GetCommandLineArgs(),
            argument => string.Equals(
                argument,
                DetailedProfileArgument,
                StringComparison.OrdinalIgnoreCase));
        int categoryCount = Enum.GetValues(typeof(AiPerformanceCategory)).Length;
        samples = new SampleRing[categoryCount];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = new SampleRing(settings.SampleCapacity);
        }
    }

    public bool DetailedCollectionEnabled =>
        commandLineDetailedCollection
        || captureScope.IsDetailedCaptureRequested
        || userSettings.Current.developerMode;
    public bool SlowTraceEnabled => captureScope.SlowTraceEnabled;

    public void RecordSlowOperation(
        string stage,
        CharacterActor actor,
        AIActionSet actionSet,
        Consideration consideration,
        double elapsedMilliseconds)
    {
        captureScope.RecordSlowOperation(
            stage,
            actor,
            actionSet,
            consideration,
            elapsedMilliseconds);
    }

    public void Record(
        AiPerformanceCategory category,
        double elapsedMilliseconds,
        long gcBytes = 0)
    {
        if (!DetailedCollectionEnabled && category != AiPerformanceCategory.Scheduler)
        {
            return;
        }

        int index = (int)category;
        if (index < 0 || index >= samples.Length)
        {
            return;
        }

        samples[index].Add(elapsedMilliseconds, gcBytes);
    }

    public void RecordGridPathSearch(double elapsedMilliseconds)
    {
        Record(AiPerformanceCategory.PathSearch, elapsedMilliseconds);
    }

    public void RecordPathCounters(int searches, int cacheHits, int budgetDeferrals)
    {
        pathSearches += Math.Max(0, searches);
        pathCacheHits += Math.Max(0, cacheHits);
        pathBudgetDeferrals += Math.Max(0, budgetDeferrals);
    }

    public CharacterAiPerformanceReport CaptureReport(int actorCount)
    {
        CharacterAiPerformanceReport report = new CharacterAiPerformanceReport
        {
            actorCount = Math.Max(0, actorCount),
            brokerSearches = pathSearches,
            brokerCacheHits = pathCacheHits,
            brokerBudgetDeferrals = pathBudgetDeferrals
        };

        int sampleFrames = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            CharacterAiPerformanceMetric metric = samples[i].Capture(CategoryNames[i]);
            report.metrics.Add(metric);
            sampleFrames = Math.Max(sampleFrames, samples[i].Count);
        }

        report.sampleFrames = sampleFrames;
        report.scheduler = report.metrics[(int)AiPerformanceCategory.Scheduler];
        report.behaviorTree = report.metrics[(int)AiPerformanceCategory.BehaviorTree];
        report.pathBroker = report.metrics[(int)AiPerformanceCategory.PathSearch];
        report.garbageCollection = new CharacterAiPerformanceMetric("GC")
        {
            average = report.metrics.Sum(metric => metric.gcBytes) / 1024d,
            p95 = report.metrics.Max(metric => metric.gcBytes) / 1024d,
            max = report.metrics.Max(metric => metric.gcBytes) / 1024d,
            gcBytes = report.metrics.Sum(metric => metric.gcBytes)
        };

        bool hasSamples = sampleFrames > 0;
        bool schedulerWithinTarget =
            report.scheduler.average <= settings.TargetSchedulerAverageMs
            && report.scheduler.p95 <= settings.TargetSchedulerP95Ms;
        report.valid = hasSamples && schedulerWithinTarget;
        double cacheHitRate = pathSearches + pathCacheHits > 0
            ? pathCacheHits * 100d / (pathSearches + pathCacheHits)
            : 0d;
        report.summary =
            $"AI {actorCount}명 · 평균 {report.scheduler.average:0.00}ms · "
            + $"p95 {report.scheduler.p95:0.00}ms · 경로 캐시 {cacheHitRate:0.0}%";
        return report;
    }

    public void Reset()
    {
        foreach (SampleRing sample in samples)
        {
            sample.Clear();
        }

        pathSearches = 0;
        pathCacheHits = 0;
        pathBudgetDeferrals = 0;
    }
}
