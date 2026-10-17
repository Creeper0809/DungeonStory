#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class Wim009EarlyFrostRulesDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-009-early-frost-rules.txt";

    private const string EarlyFrostId = "seasonal:autumn-early-frost";
    private const string ColdSnapFrontId = "weather:cold-snap";

    public static bool Run(out string report)
    {
        List<string> lines = new()
        {
            "WIM009 early-frost rules/authoring",
            "scope=real content catalog plus isolated generic daily-event selection; no climate runtime, crop growth, save, AI, UI, or expedition claim"
        };
        string current = "not-started";
        int passed = 0;

        try
        {
            current = "actual-catalog-authoring";
            var source = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            var catalog = new V20StoryContentCatalog(source);
            SeasonalWorldEventDefinitionSO[] seasonal = catalog.SeasonalEvents
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .ToArray();
            Require(seasonal.Length == 28,
                $"Expected 28 real seasonal assets, found {seasonal.Length}.");
            Require(seasonal.Select(value => value.StableId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == seasonal.Length,
                "Real seasonal assets contain duplicate stable IDs.");

            SeasonalWorldEventDefinitionSO[] frosts = seasonal
                .Where(value => string.Equals(
                    value.StableId,
                    EarlyFrostId,
                    StringComparison.Ordinal))
                .ToArray();
            Require(frosts.Length == 1,
                "Expected exactly one authored autumn early-frost event.");
            SeasonalWorldEventDefinitionSO frost = frosts[0];
            Require(string.Equals(
                    frost.requiredWeatherFrontId,
                    ColdSnapFrontId,
                    StringComparison.Ordinal),
                "Early frost must require the canonical cold-snap front.");
            Require(seasonal.Where(value => !ReferenceEquals(value, frost))
                    .All(value => value.requiredWeatherFrontId == string.Empty),
                "A non-early-frost seasonal event requires a weather front.");
            Require(frost.season == Season.Autumn
                    && frost.minimumDurationDays == 2
                    && frost.maximumDurationDays == 4,
                "Early frost must remain an Autumn event with duration 2-4 days.");
            Require(HasExactAffectedDomains(frost, "agriculture", "environment"),
                "Early frost must affect exactly agriculture and environment.");
            Require(frost.startEffects != null && frost.startEffects.Count == 0,
                "Early frost must not author start effects.");
            Require(AllEffects(frost).All(effect => effect != null
                    && effect.kind != V20ContentEffectKind.Threat
                    && (effect.targetId ?? string.Empty).IndexOf(
                        "expedition",
                        StringComparison.OrdinalIgnoreCase) < 0),
                "Early frost must not author expedition or Threat effects.");
            Require(source.GetAll<WeatherFrontDefinitionSO>()
                    .Count(value => value != null && string.Equals(
                        value.stableId,
                        ColdSnapFrontId,
                        StringComparison.Ordinal)) == 1,
                "Early frost's required cold-snap front is absent or ambiguous.");
            passed++;

            current = "isolated-generic-weather-gate";
            var campaign = new V20CampaignRuntime(
                new DungeonRuntimeAggregateRootStore(),
                catalog);
            SeasonalEventWorldSaveData precondition = campaign.CaptureSeasonal();
            precondition.completedEventIds.AddRange(seasonal
                .Where(value => value.season == Season.Autumn
                    && !ReferenceEquals(value, frost))
                .Select(value => value.StableId));
            campaign.PublishSeasonal(campaign.PrepareSeasonal(precondition));

            IReadOnlyList<V20ResolvedEventResult> clear = campaign.EvaluateDaily(
                new V20DailyEventContext
                {
                    AbsoluteDay = 100,
                    RunSeed = 90210,
                    Season = Season.Autumn,
                    WeatherFrontId = string.Empty
                });
            Require(clear.All(value => !string.Equals(
                        value.DefinitionId,
                        EarlyFrostId,
                        StringComparison.Ordinal))
                    && campaign.ActiveSeasonalEvents.All(value => !string.Equals(
                        value.definitionId,
                        EarlyFrostId,
                        StringComparison.Ordinal)),
                "Empty weather context started early frost.");

            IReadOnlyList<V20ResolvedEventResult> coldSnap = campaign
                .EvaluateDaily(new V20DailyEventContext
                {
                    AbsoluteDay = 101,
                    RunSeed = 90210,
                    Season = Season.Autumn,
                    WeatherFrontId = ColdSnapFrontId
                });
            V20ResolvedEventResult[] starts = coldSnap.Where(value =>
                    string.Equals(
                        value.DefinitionId,
                        EarlyFrostId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.ResolutionId,
                        "started",
                        StringComparison.Ordinal))
                .ToArray();
            Require(starts.Length == 1 && starts[0].Effects.Count == 0,
                "Exact cold-snap context did not produce one effect-free early-frost start.");
            V20ActiveEventSaveData[] active = campaign.ActiveSeasonalEvents
                .Where(value => string.Equals(
                    value.definitionId,
                    EarlyFrostId,
                    StringComparison.Ordinal))
                .ToArray();
            Require(active.Length == 1
                    && active[0].deadlineAbsoluteDay - active[0].startedAbsoluteDay >= 2
                    && active[0].deadlineAbsoluteDay - active[0].startedAbsoluteDay <= 4,
                "Exact cold-snap start did not retain an early-frost duration in 2-4 days.");
            passed++;

            lines.Add("result=PASS");
            lines.Add($"passed={passed}; seasonal=28; earlyFrost=1; otherWeatherFrontRequirements=0; coldSnapFront=1; duration=2-4");
            report = string.Join("\n", lines);
            WriteReport(report);
            return true;
        }
        catch (Exception error)
        {
            lines.Add("result=FAIL");
            lines.Add("case=" + current + "; passed=" + passed
                + "; " + error.GetType().Name + ": " + error.Message);
            report = string.Join("\n", lines);
            WriteReport(report);
            return false;
        }
    }

    private static IEnumerable<V20ContentEffect> AllEffects(
        SeasonalWorldEventDefinitionSO definition) =>
        (definition.startEffects ?? new List<V20ContentEffect>())
        .Concat(definition.dailyEffects ?? new List<V20ContentEffect>())
        .Concat(definition.endEffects ?? new List<V20ContentEffect>());

    private static bool HasExactAffectedDomains(
        SeasonalWorldEventDefinitionSO definition,
        string first,
        string second)
    {
        string[] domains = (definition.affectedDomainIds ?? new List<string>())
            .ToArray();
        return domains.Length == 2
            && domains.Distinct(StringComparer.Ordinal).Count() == 2
            && domains.Contains(first, StringComparer.Ordinal)
            && domains.Contains(second, StringComparer.Ordinal);
    }

    private static void WriteReport(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report + "\n");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
