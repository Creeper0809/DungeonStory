#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class Wim009TemporarySupplyRulesDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-009-temporary-supply-rules.txt";

    private const string HeatGridId = "seasonal:summer-heat-grid";
    private const string FrozenPipesId = "seasonal:winter-frozen-pipes";
    private const float ReducedMultiplier = 0.8f;
    private const float NeutralMultiplier = 1f;
    private const float FreezeThresholdC = 0f;
    private const float RecoveryThresholdC = 2f;
    private const int ProjectionStartDay = 100;

    public static bool Run(out string report)
    {
        List<string> lines = new()
        {
            "WIM009 temporary supply rules/authoring",
            "scope=real seasonal and modular-building authoring plus isolated generic campaign projections and a static water-availability signature guard; no grid, power, fluid, temperature, save, AI work, UI, or asset mutation claim",
            "allocation=not asserted; the current projection query is not documented here as a hot read"
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

            SeasonalWorldEventDefinitionSO heatGrid = RequireOne(
                seasonal,
                HeatGridId);
            SeasonalWorldEventDefinitionSO frozenPipes = RequireOne(
                seasonal,
                FrozenPipesId);
            Require(heatGrid.minimumDurationDays == 2
                    && heatGrid.maximumDurationDays == 5,
                "Heat-grid must retain duration 2-5 days.");
            Require(Equal(
                    heatGrid.powerAvailableSupplyCapacityMultiplier,
                    ReducedMultiplier),
                "Heat-grid power available-supply multiplier must be 0.8.");
            Require(Equal(
                    heatGrid.pipedWaterThroughputMultiplier,
                    NeutralMultiplier),
                "Heat-grid must not author a piped-water throughput declaration.");
            Require(HasNoThreatStartEffect(heatGrid),
                "Heat-grid must not author a Threat start effect.");

            Require(frozenPipes.minimumDurationDays == 2
                    && frozenPipes.maximumDurationDays == 4,
                "Frozen-pipes must retain duration 2-4 days.");
            Require(Equal(
                    frozenPipes.pipedWaterThroughputMultiplier,
                    ReducedMultiplier)
                    && Equal(
                        frozenPipes.pipedWaterFreezeThresholdC,
                        FreezeThresholdC)
                    && Equal(
                        frozenPipes.pipedWaterRecoveryThresholdC,
                        RecoveryThresholdC),
                "Frozen-pipes must author 0.8 throughput with 0C/2C thresholds.");
            Require(Equal(
                    frozenPipes.powerAvailableSupplyCapacityMultiplier,
                    NeutralMultiplier),
                "Frozen-pipes must not author a power available-supply declaration.");
            Require(HasNoThreatStartEffect(frozenPipes),
                "Frozen-pipes must not author a Threat start effect.");

            SeasonalWorldEventDefinitionSO[] other = seasonal
                .Where(value => !ReferenceEquals(value, heatGrid)
                    && !ReferenceEquals(value, frozenPipes))
                .ToArray();
            Require(other.Length == 26
                    && other.All(value => Equal(
                        value.powerAvailableSupplyCapacityMultiplier,
                        NeutralMultiplier)
                        && Equal(
                            value.pipedWaterThroughputMultiplier,
                            NeutralMultiplier)),
                "The other 26 seasonal definitions must be neutral for both temporary supply declarations.");
            passed++;

            current = "isolated-generic-campaign-projections";
            VerifyPowerProjection(catalog, seasonal, heatGrid);
            VerifyPipedWaterProjection(catalog, seasonal, frozenPipes);
            passed++;

            current = "manual-water-authoring-and-availability-signature";
            VerifyManualWaterAuthoring();
            passed++;

            lines.Add("result=PASS");
            lines.Add(
                "passed=" + passed
                + "; seasonal=28; heatGrid=1; frozenPipes=1; neutralOther=26"
                + "; power=0.8; pipedWater=0.8; thresholds=0/2"
                + "; projection=neutral-active-expired"
                + "; manualWaterSources=H03:4/.9,H04:6/1.2,L03:3/1.1"
                + "; canDrawWater=BuildableObject-only");
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

    private static void VerifyPowerProjection(
        V20StoryContentCatalog catalog,
        IReadOnlyList<SeasonalWorldEventDefinitionSO> seasonal,
        SeasonalWorldEventDefinitionSO heatGrid)
    {
        var campaign = new V20CampaignRuntime(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        Require(!campaign.GetPowerCapacityContribution().IsActive
                && !campaign.GetPipedWaterContribution().IsActive,
            "A campaign without an active seasonal event must have neutral supply contributions.");

        V20ActiveEventSaveData active = StartOnlySeasonal(
            campaign,
            seasonal,
            heatGrid,
            ProjectionStartDay);
        SeasonalPowerCapacityContribution power =
            campaign.GetPowerCapacityContribution();
        Require(power.IsActive
                && string.Equals(
                    power.OccurrenceInstanceId,
                    active.instanceId,
                    StringComparison.Ordinal)
                && string.Equals(power.DefinitionId, HeatGridId, StringComparison.Ordinal)
                && string.Equals(
                    power.DisplayName,
                    heatGrid.DisplayName,
                    StringComparison.Ordinal)
                && power.RemainingDays
                    == active.deadlineAbsoluteDay - ProjectionStartDay
                && Equal(power.AvailableSupplyMultiplier, ReducedMultiplier),
            "Heat-grid projection drifted from its active occurrence or literal 0.8 contribution.");
        Require(!campaign.GetPipedWaterContribution().IsActive,
            "Heat-grid must not project a piped-water contribution.");

        Expire(campaign, heatGrid.season, active.deadlineAbsoluteDay);
        Require(!campaign.GetPowerCapacityContribution().IsActive
                && !campaign.GetPipedWaterContribution().IsActive,
            "Expired heat-grid must leave neutral supply contributions.");
    }

    private static void VerifyPipedWaterProjection(
        V20StoryContentCatalog catalog,
        IReadOnlyList<SeasonalWorldEventDefinitionSO> seasonal,
        SeasonalWorldEventDefinitionSO frozenPipes)
    {
        var campaign = new V20CampaignRuntime(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        Require(!campaign.GetPowerCapacityContribution().IsActive
                && !campaign.GetPipedWaterContribution().IsActive,
            "A campaign without an active seasonal event must have neutral supply contributions.");

        V20ActiveEventSaveData active = StartOnlySeasonal(
            campaign,
            seasonal,
            frozenPipes,
            ProjectionStartDay);
        SeasonalPipedWaterContribution pipedWater =
            campaign.GetPipedWaterContribution();
        Require(pipedWater.IsActive
                && string.Equals(
                    pipedWater.OccurrenceInstanceId,
                    active.instanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    pipedWater.DefinitionId,
                    FrozenPipesId,
                    StringComparison.Ordinal)
                && string.Equals(
                    pipedWater.DisplayName,
                    frozenPipes.DisplayName,
                    StringComparison.Ordinal)
                && pipedWater.RemainingDays
                    == active.deadlineAbsoluteDay - ProjectionStartDay
                && Equal(pipedWater.ThroughputMultiplier, ReducedMultiplier)
                && Equal(pipedWater.FreezeThresholdC, FreezeThresholdC)
                && Equal(pipedWater.RecoveryThresholdC, RecoveryThresholdC),
            "Frozen-pipes projection drifted from its active occurrence or literal contribution.");
        Require(!campaign.GetPowerCapacityContribution().IsActive,
            "Frozen-pipes must not project a power contribution.");

        Expire(campaign, frozenPipes.season, active.deadlineAbsoluteDay);
        Require(!campaign.GetPowerCapacityContribution().IsActive
                && !campaign.GetPipedWaterContribution().IsActive,
            "Expired frozen-pipes must leave neutral supply contributions.");
    }

    private static V20ActiveEventSaveData StartOnlySeasonal(
        V20CampaignRuntime campaign,
        IReadOnlyList<SeasonalWorldEventDefinitionSO> seasonal,
        SeasonalWorldEventDefinitionSO target,
        int day)
    {
        SeasonalEventWorldSaveData state = campaign.CaptureSeasonal();
        state.completedEventIds.AddRange(seasonal
            .Where(value => value.season == target.season
                && !ReferenceEquals(value, target))
            .Select(value => value.StableId));
        campaign.PublishSeasonal(campaign.PrepareSeasonal(state));

        IReadOnlyList<V20ResolvedEventResult> started = campaign.EvaluateDaily(
            new V20DailyEventContext
            {
                AbsoluteDay = day,
                RunSeed = 90210,
                Season = target.season,
                WeatherFrontId = string.Empty
            });
        Require(started.Count(value => string.Equals(
                    value.DefinitionId,
                    target.StableId,
                    StringComparison.Ordinal)
                && string.Equals(value.ResolutionId, "started", StringComparison.Ordinal))
                    == 1,
            "Controlled seasonal precondition did not start exactly the requested definition.");
        V20ActiveEventSaveData[] active = campaign.ActiveSeasonalEvents
            .Where(value => string.Equals(
                value.definitionId,
                target.StableId,
                StringComparison.Ordinal))
            .ToArray();
        Require(active.Length == 1
                && active[0].deadlineAbsoluteDay - active[0].startedAbsoluteDay
                    >= target.minimumDurationDays
                && active[0].deadlineAbsoluteDay - active[0].startedAbsoluteDay
                    <= target.maximumDurationDays,
            "Controlled seasonal start has an invalid active duration.");
        return active[0];
    }

    private static void Expire(
        V20CampaignRuntime campaign,
        Season season,
        int deadlineDay)
    {
        campaign.EvaluateDaily(new V20DailyEventContext
        {
            AbsoluteDay = deadlineDay + 1,
            RunSeed = 90210,
            Season = season,
            WeatherFrontId = string.Empty
        });
    }

    private static void VerifyManualWaterAuthoring()
    {
        BuildingSO[] modular = Resources.LoadAll<BuildingSO>(
                "SO/Building/Modular")
            .Where(value => value != null)
            .ToArray();
        VerifyWaterSource(modular, "H03", 4, 0.9f);
        VerifyWaterSource(modular, "H04", 6, 1.2f);
        VerifyWaterSource(modular, "L03", 3, 1.1f);

        Type rules = typeof(BuildableObject).Assembly.GetType(
            "SurvivalFacilityWorkRules",
            throwOnError: false);
        Require(rules != null,
            "SurvivalFacilityWorkRules is absent from the runtime assembly.");
        MethodInfo[] availability = rules
            .GetMethods(BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic)
            .Where(method => string.Equals(
                method.Name,
                "CanDrawWater",
                StringComparison.Ordinal))
            .ToArray();
        Require(availability.Length == 1
                && availability[0].IsPublic
                && availability[0].ReturnType == typeof(bool),
            "Water availability must expose exactly one public CanDrawWater rule.");
        ParameterInfo[] parameters = availability[0].GetParameters();
        Require(parameters.Length == 1
                && parameters[0].ParameterType == typeof(BuildableObject),
            "CanDrawWater must accept only BuildableObject; weather and seasonal-event inputs are forbidden.");
    }

    private static void VerifyWaterSource(
        IEnumerable<BuildingSO> buildings,
        string code,
        int expectedWaterPerWork,
        float expectedWorkSeconds)
    {
        BuildingSO[] matches = buildings.Where(value => string.Equals(
                value.GetAbility<BuildingFacilityPartAbility>()?.code,
                code,
                StringComparison.Ordinal))
            .ToArray();
        Require(matches.Length == 1,
            $"Expected exactly one real modular building '{code}', found {matches.Length}.");
        BuildingWaterSourceAbility[] water = matches[0].Abilities
            .OfType<BuildingWaterSourceAbility>()
            .ToArray();
        Require(water.Length == 1
                && water[0].waterPerWork == expectedWaterPerWork
                && Equal(water[0].workSeconds, expectedWorkSeconds),
            $"{code} water source must remain {expectedWaterPerWork}/{expectedWorkSeconds:0.0} work units.");
    }

    private static SeasonalWorldEventDefinitionSO RequireOne(
        IEnumerable<SeasonalWorldEventDefinitionSO> seasonal,
        string stableId)
    {
        SeasonalWorldEventDefinitionSO[] matches = seasonal.Where(value =>
            string.Equals(value.StableId, stableId, StringComparison.Ordinal))
            .ToArray();
        Require(matches.Length == 1,
            $"Expected exactly one seasonal definition '{stableId}', found {matches.Length}.");
        return matches[0];
    }

    private static bool HasNoThreatStartEffect(
        SeasonalWorldEventDefinitionSO definition) =>
        (definition.startEffects ?? new List<V20ContentEffect>())
        .All(effect => effect != null && effect.kind != V20ContentEffectKind.Threat);

    private static bool Equal(float left, float right) =>
        Math.Abs(left - right) < 0.0001f;

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
