#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using VContainer;

// Root-owned focused integration witness. Seasonal occurrence selection,
// fixture placement and cell temperatures are controlled setup. Power/fluid
// projection, allocation, hysteresis, current-format restore and presenters
// are the production paths under verification.
public static class Wim009TemporarySupplyLiveScenario
{
    private const string HeatGridId = "seasonal:summer-heat-grid";
    private const string FrozenPipesId = "seasonal:winter-frozen-pipes";
    private const float ReducedMultiplier = 0.8f;
    private const float InBandTemperatureC = 1f;
    private const float FreezeTemperatureC = 0f;
    private const float RecoveryTemperatureC = 2f;
    private const float PumpWindowSeconds = 4f;
    private const float TimeoutSeconds = 12f;

    public static IEnumerator Run(
        DungeonRuntimeLifetimeScope scope,
        BuildableObject generator,
        BuildableObject battery,
        BuildableObject pump,
        BuildableObject cleanTank,
        ICollection<string> report)
    {
        Require(scope?.Container != null, "WIM009 main scope is unavailable.");
        Require(generator != null && battery != null && pump != null
            && cleanTank != null, "WIM009 authored live fixtures are incomplete.");
        Require(report != null, "WIM009 report dependency is unavailable.");

        IObjectResolver container = scope.Container;
        IPowerInfrastructureQuery power =
            container.Resolve<IPowerInfrastructureQuery>();
        IPowerInfrastructurePersistence powerPersistence =
            container.Resolve<IPowerInfrastructurePersistence>();
        IFluidInfrastructureQuery fluid =
            container.Resolve<IFluidInfrastructureQuery>();
        IFluidInfrastructurePersistence fluidPersistence =
            container.Resolve<IFluidInfrastructurePersistence>();
        IEnvironmentalFieldPersistence environment =
            container.Resolve<IEnvironmentalFieldPersistence>();
        IDungeonGameSaveService gameSaves =
            container.Resolve<IDungeonGameSaveService>();
        V20CampaignRuntime campaign = container.Resolve<V20CampaignRuntime>();
        ISeasonalEventQuery seasonal = container.Resolve<ISeasonalEventQuery>();
        V20StoryContentCatalog story =
            container.Resolve<V20StoryContentCatalog>();
        IGameClock clock = container.Resolve<IGameClock>();
        IGameTimeScaleController timeScale =
            container.Resolve<IGameTimeScaleController>();
        GameManager game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        Require(game != null, "WIM009 main GameManager is unavailable.");

        string generatorId = generator.RequirePersistentInstanceId().Value;
        string batteryId = battery.RequirePersistentInstanceId().Value;
        string pumpId = pump.RequirePersistentInstanceId().Value;
        string tankId = cleanTank.RequirePersistentInstanceId().Value;
        Pause(game, timeScale);
        ClearSeasonal(campaign);

        SeedPower(powerPersistence, generatorId, batteryId,
            generatorFuelSeconds: 90f, batteryStoredPower: 0f);
        PowerNetworkSnapshot neutralGenerator = NetworkFor(power, generatorId);
        Require(neutralGenerator.ProductionPerSecond > 0.001f
            && Approximately(
                neutralGenerator.NominalAvailableSourcePerSecond,
                neutralGenerator.AvailableSourcePerSecond),
            "Neutral generator source projection is unavailable or reduced.");
        float generatorStorageCapacity = neutralGenerator.StorageCapacity;

        V20ActiveEventSaveData heat = StartOnlySeasonal(
            campaign,
            story,
            HeatGridId,
            Season.Summer,
            absoluteDay: 200);
        PowerNetworkSnapshot reducedGenerator = NetworkFor(power, generatorId);
        RequirePowerReduction(reducedGenerator, heat.instanceId);
        Require(Approximately(
                reducedGenerator.ProductionPerSecond,
                neutralGenerator.ProductionPerSecond * ReducedMultiplier)
            && Approximately(
                reducedGenerator.StorageCapacity,
                generatorStorageCapacity),
            "Heat-grid did not reduce the live generator offer exactly once or changed storage capacity.");
        VerifyPowerPresenter(container, heat.instanceId, reducedGenerator);

        SeedPower(powerPersistence, generatorId, batteryId,
            generatorFuelSeconds: 0f, batteryStoredPower: 200f);
        PowerNetworkSnapshot reducedBattery = NetworkFor(power, batteryId);
        Require(reducedBattery.ProductionPerSecond < 0.001f
            && reducedBattery.NominalAvailableSourcePerSecond > 0.001f,
            "Battery-only source fixture still contains generator production.");
        RequirePowerReduction(reducedBattery, heat.instanceId);
        Require(Approximately(reducedBattery.StoredPower, 200f)
            && Approximately(reducedBattery.StorageCapacity, 240f),
            "Paused heat-grid query mutated authored or stored battery energy.");

        SeedPower(powerPersistence, generatorId, batteryId,
            generatorFuelSeconds: 90f, batteryStoredPower: 200f);
        PowerNetworkSnapshot reducedCombined = NetworkFor(power, generatorId);
        RequirePowerReduction(reducedCombined, heat.instanceId);
        Require(!Approximately(
                reducedCombined.AvailableSourcePerSecond,
                reducedCombined.NominalAvailableSourcePerSecond
                    * ReducedMultiplier * ReducedMultiplier),
            "Heat-grid multiplier stacked once per source instead of once on each source offer.");

        Expire(campaign, Season.Summer, heat.deadlineAbsoluteDay);
        PowerNetworkSnapshot expiredPower = NetworkFor(power, generatorId);
        Require(expiredPower.CapacitySourceOccurrenceInstanceId.Length == 0
            && Approximately(expiredPower.AvailableSourceMultiplier, 1f)
            && Approximately(
                expiredPower.AvailableSourcePerSecond,
                expiredPower.NominalAvailableSourcePerSecond),
            "Expired heat-grid left reduced power capacity or stale UI provenance.");
        report.Add(
            "[PASS] actual generator, battery-only and combined offers were reduced once to 80%; stored energy, storage capacity and expiry remained intact");

        V20ActiveEventSaveData frozenPipes = StartOnlySeasonal(
            campaign,
            story,
            FrozenPipesId,
            Season.Winter,
            absoluteDay: 300);
        SeedPower(powerPersistence, generatorId, batteryId,
            generatorFuelSeconds: 90f, batteryStoredPower: 0f);

        SetTemperature(environment, pump.centerPos, InBandTemperatureC);
        ResetFluid(fluidPersistence);
        Require(power.IsPowered(pump),
            "The actual pump has no power for the normal throughput window.");
        float normalElapsed = 0f;
        yield return AdvanceGameTimeHoldingTemperature(
            game,
            timeScale,
            clock,
            environment,
            pump.centerPos,
            InBandTemperatureC,
            PumpWindowSeconds,
            value => normalElapsed = value);
        float normalWater = ReadCleanWater(fluidPersistence, tankId);
        Require(fluid.TryGetWaterCondition(
                ResolveBuilding(container, pumpId),
                out BuildingWaterConditionSnapshot normalCondition)
            && normalCondition.HasSeasonalSource
            && !normalCondition.Frozen
            && Approximately(normalCondition.ThroughputMultiplier, 1f),
            "A new frozen-pipes occurrence incorrectly latched in the 0-2C band.");

        SetTemperature(environment, pump.centerPos, FreezeTemperatureC);
        ResetFluid(fluidPersistence);
        float frozenElapsed = 0f;
        yield return AdvanceGameTimeHoldingTemperature(
            game,
            timeScale,
            clock,
            environment,
            pump.centerPos,
            FreezeTemperatureC,
            PumpWindowSeconds,
            value => frozenElapsed = value);
        float frozenWater = ReadCleanWater(fluidPersistence, tankId);
        BuildableObject currentPump = ResolveBuilding(container, pumpId);
        Require(fluid.TryGetWaterCondition(
                currentPump,
                out BuildingWaterConditionSnapshot frozenCondition)
            && frozenCondition.Frozen
            && frozenCondition.IsThroughputReduced
            && string.Equals(
                frozenCondition.SourceOccurrenceInstanceId,
                frozenPipes.instanceId,
                StringComparison.Ordinal),
            "The live pump did not latch frozen at the exact 0C boundary.");
        float normalRate = normalWater / Math.Max(normalElapsed, 0.001f);
        float frozenRate = frozenWater / Math.Max(frozenElapsed, 0.001f);
        float observedRatio = frozenRate / Math.Max(normalRate, 0.001f);
        Require(normalRate > 0.1f
            && observedRatio >= 0.68f
            && observedRatio <= 0.92f,
            $"Live pump throughput ratio is outside the 0.8 window: normal={normalRate:0.###}, frozen={frozenRate:0.###}, ratio={observedRatio:0.###}.");

        SeedPower(powerPersistence, generatorId, batteryId,
            generatorFuelSeconds: 0f, batteryStoredPower: 0f);
        SetTemperature(environment, currentPump.centerPos, InBandTemperatureC);
        float stockBeforeRecovery = ReadCleanWater(fluidPersistence, tankId);
        yield return AdvanceGameTime(
            game,
            timeScale,
            clock,
            0.5f,
            _ => { });
        currentPump = ResolveBuilding(container, pumpId);
        Require(fluid.TryGetWaterCondition(
                currentPump,
                out BuildingWaterConditionSnapshot recovering)
            && recovering.Frozen
            && recovering.Recovering
            && Approximately(recovering.ObservedTemperatureC, 1f)
            && Approximately(recovering.ThroughputMultiplier, ReducedMultiplier),
            "The 1C hysteresis band did not retain the frozen latch.");
        Require(Approximately(
                ReadCleanWater(fluidPersistence, tankId),
                stockBeforeRecovery),
            "A temperature-only latch transition changed stored water.");

        DungeonGameSaveData checkpoint = gameSaves.Capture();
        string checkpointJson = JsonUtility.ToJson(fluidPersistence.Capture());
        SetTemperature(environment, currentPump.centerPos, RecoveryTemperatureC);
        yield return AdvanceGameTime(
            game,
            timeScale,
            clock,
            0.5f,
            _ => { });
        Require(fluid.TryGetWaterCondition(
                ResolveBuilding(container, pumpId),
                out BuildingWaterConditionSnapshot clearedBeforeRestore)
            && !clearedBeforeRestore.Frozen,
            "The exact 2C recovery boundary did not clear the live latch.");

        Require(gameSaves.TryRestore(checkpoint, out DungeonGameRestoreReport restoreReport),
            "WIM009 current whole-world restore failed: "
            + string.Join(" | ", restoreReport.Errors));
        for (int frame = 0; frame < 4; frame++)
            yield return null;
        Pause(game, timeScale);
        currentPump = ResolveBuilding(container, pumpId);
        BuildableObject currentTank = ResolveBuilding(container, tankId);
        Require(fluid.TryGetWaterCondition(
                currentPump,
                out BuildingWaterConditionSnapshot restored)
            && restored.Frozen
            && restored.Recovering
            && string.Equals(
                restored.SourceOccurrenceInstanceId,
                frozenPipes.instanceId,
                StringComparison.Ordinal),
            "Current whole-world restore lost the frozen latch or occurrence identity.");
        Require(JsonUtility.ToJson(fluidPersistence.Capture()) == checkpointJson
            && Approximately(
                ReadCleanWater(fluidPersistence, tankId),
                stockBeforeRecovery),
            "Current whole-world restore changed fluid state or stored water.");
        VerifyWaterPresenter(container, currentPump, restored);

        SetTemperature(environment, currentPump.centerPos, RecoveryTemperatureC);
        yield return AdvanceGameTime(
            game,
            timeScale,
            clock,
            0.5f,
            _ => { });
        currentPump = ResolveBuilding(container, pumpId);
        Require(fluid.TryGetWaterCondition(
                currentPump,
                out BuildingWaterConditionSnapshot recovered)
            && !recovered.Frozen
            && Approximately(recovered.ThroughputMultiplier, 1f),
            "The restored frozen latch did not recover at 2C.");
        Require(Approximately(
                ReadCleanWater(fluidPersistence, currentTank
                    .RequirePersistentInstanceId().Value),
                stockBeforeRecovery),
            "Recovery changed stored water while the pump was unpowered.");

        Expire(campaign, Season.Winter, frozenPipes.deadlineAbsoluteDay);
        yield return AdvanceGameTime(
            game,
            timeScale,
            clock,
            0.5f,
            _ => { });
        DungeonFluidInfrastructureSaveData expiredFluid =
            fluidPersistence.Capture();
        Require(!seasonal.GetPipedWaterContribution().IsActive
            && expiredFluid.nodes.All(value => !value.frozenPipeLatched
                && value.frozenPipeOccurrenceInstanceId.Length == 0),
            "Expired frozen-pipes left a persisted latch or source identity.");
        report.Add(
            $"[PASS] actual pump normal/frozen rate ratio={observedRatio:0.###}; 0C latch, 1C recovery band, 2C clear, exact whole-save restore, stored-water preservation, UI and expiry passed");
        report.Add(
            "scope=controlled seasonal selection/cell temperature and authored fixture placement; actual main campaign projections, generator+battery allocator, pump production, fluid V7, current whole-save and presenters; no natural event selection, AI response, transfer/processor live throughput, or whole-WIM009 claim");
        Pause(game, timeScale);
    }

    private static void RequirePowerReduction(
        PowerNetworkSnapshot snapshot,
        string occurrenceInstanceId)
    {
        Require(string.Equals(
                snapshot.CapacitySourceOccurrenceInstanceId,
                occurrenceInstanceId,
                StringComparison.Ordinal)
            && Approximately(snapshot.AvailableSourceMultiplier, ReducedMultiplier)
            && snapshot.NominalAvailableSourcePerSecond > 0.001f
            && Approximately(
                snapshot.AvailableSourcePerSecond,
                snapshot.NominalAvailableSourcePerSecond * ReducedMultiplier),
            "Live power network did not expose one exact 0.8 available-source reduction.");
    }

    private static void SeedPower(
        IPowerInfrastructurePersistence persistence,
        string generatorId,
        string batteryId,
        float generatorFuelSeconds,
        float batteryStoredPower)
    {
        DungeonPowerInfrastructureSaveData data = persistence.Capture();
        PowerNodeSaveData generator = data.nodes.Single(value =>
            string.Equals(value.buildingInstanceId, generatorId,
                StringComparison.Ordinal));
        PowerNodeSaveData battery = data.nodes.Single(value =>
            string.Equals(value.buildingInstanceId, batteryId,
                StringComparison.Ordinal));
        generator.fuelSeconds = generatorFuelSeconds;
        battery.storedPower = batteryStoredPower;
        persistence.Restore(persistence.PrepareRestore(data));
    }

    private static void ResetFluid(IFluidInfrastructurePersistence persistence)
    {
        DungeonFluidInfrastructureSaveData data = persistence.Capture();
        foreach (FluidNodeSaveData node in data.nodes)
        {
            node.cleanWater = 0f;
            node.unsafeWater = 0f;
            node.foulWater = 0f;
            node.wastewater = 0f;
            node.processorWork = 0f;
            node.transferWork = 0f;
            node.frozenPipeLatched = false;
            node.frozenPipeOccurrenceInstanceId = string.Empty;
        }
        persistence.Restore(persistence.PrepareRestore(data));
    }

    private static float ReadCleanWater(
        IFluidInfrastructurePersistence persistence,
        string tankId) => persistence.Capture().nodes.Single(value =>
            string.Equals(value.buildingInstanceId, tankId,
                StringComparison.Ordinal)).cleanWater;

    private static PowerNetworkSnapshot NetworkFor(
        IPowerInfrastructureQuery power,
        string buildingId) => power.Networks.Single(network => network.Nodes.Any(
            node => string.Equals(
                node.BuildingId.Value,
                buildingId,
                StringComparison.Ordinal)));

    private static BuildableObject ResolveBuilding(
        IObjectResolver container,
        string buildingId) => container.Resolve<IBuildingWorldQuery>()
            .Buildings.Single(value => value != null
                && value.PersistentInstanceId.IsValid
                && string.Equals(
                    value.PersistentInstanceId.Value,
                    buildingId,
                    StringComparison.Ordinal));

    private static V20ActiveEventSaveData StartOnlySeasonal(
        V20CampaignRuntime campaign,
        V20StoryContentCatalog catalog,
        string targetId,
        Season season,
        int absoluteDay)
    {
        SeasonalEventWorldSaveData state = campaign.CaptureSeasonal();
        state.activeEvents.Clear();
        state.completedEventIds = catalog.SeasonalEvents
            .Where(value => value.season == season
                && !string.Equals(value.StableId, targetId,
                    StringComparison.Ordinal))
            .Select(value => value.StableId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        state.cycle = absoluteDay / GameCalendarRules.DaysPerYear;
        state.lastEvaluationAbsoluteDay = absoluteDay - 1;
        campaign.PublishSeasonal(campaign.PrepareSeasonal(state));
        IReadOnlyList<V20ResolvedEventResult> results = campaign.EvaluateDaily(
            new V20DailyEventContext
            {
                AbsoluteDay = absoluteDay,
                RunSeed = 157181,
                Season = season,
                WeatherFrontId = string.Empty
            });
        Require(results.Count(value => string.Equals(
                value.DefinitionId,
                targetId,
                StringComparison.Ordinal)
            && string.Equals(value.ResolutionId, "started",
                StringComparison.Ordinal)) == 1,
            "Controlled WIM009 seasonal setup did not start " + targetId);
        return campaign.ActiveSeasonalEvents.Single(value =>
            string.Equals(value.definitionId, targetId,
                StringComparison.Ordinal));
    }

    private static void ClearSeasonal(V20CampaignRuntime campaign)
    {
        SeasonalEventWorldSaveData state = campaign.CaptureSeasonal();
        state.activeEvents.Clear();
        campaign.PublishSeasonal(campaign.PrepareSeasonal(state));
    }

    private static void Expire(
        V20CampaignRuntime campaign,
        Season season,
        int deadlineAbsoluteDay)
    {
        campaign.EvaluateDaily(new V20DailyEventContext
        {
            AbsoluteDay = deadlineAbsoluteDay + 1,
            RunSeed = 157181,
            Season = season,
            WeatherFrontId = string.Empty
        });
    }

    private static void SetTemperature(
        IEnvironmentalFieldPersistence persistence,
        Vector2Int position,
        float temperatureC)
    {
        DungeonEnvironmentalFieldSaveData data = persistence.Capture();
        data.cells.RemoveAll(value => value.x == position.x
            && value.y == position.y);
        data.cells.Add(new EnvironmentalCellSaveData
        {
            x = position.x,
            y = position.y,
            temperatureC = temperatureC,
            airQuality = 100f,
            lightLevel = 100f
        });
        data.cells.Sort((left, right) => checked(left.y * data.width + left.x)
            .CompareTo(checked(right.y * data.width + right.x)));
        persistence.Restore(persistence.PrepareRestore(data));
    }

    private static IEnumerator AdvanceGameTime(
        GameManager game,
        IGameTimeScaleController timeScale,
        IGameClock clock,
        float seconds,
        Action<float> observeElapsed)
    {
        float startedAt = clock.Time;
        float wallStartedAt = Time.realtimeSinceStartup;
        game.isPause = false;
        timeScale.Scale = 5f;
        while (clock.Time - startedAt < seconds
            && Time.realtimeSinceStartup - wallStartedAt < TimeoutSeconds)
        {
            yield return null;
        }
        float elapsed = clock.Time - startedAt;
        Pause(game, timeScale);
        yield return null;
        Require(elapsed >= seconds,
            $"WIM009 game-time window timed out at {elapsed:0.###}/{seconds:0.###} seconds.");
        observeElapsed?.Invoke(elapsed);
    }

    private static IEnumerator AdvanceGameTimeHoldingTemperature(
        GameManager game,
        IGameTimeScaleController timeScale,
        IGameClock clock,
        IEnvironmentalFieldPersistence environment,
        Vector2Int position,
        float temperatureC,
        float seconds,
        Action<float> observeElapsed)
    {
        float startedAt = clock.Time;
        float wallStartedAt = Time.realtimeSinceStartup;
        game.isPause = false;
        timeScale.Scale = 5f;
        while (clock.Time - startedAt < seconds
            && Time.realtimeSinceStartup - wallStartedAt < TimeoutSeconds)
        {
            // The environment entry point runs before the fluid entry point.
            // Restore the controlled cell every frame so diffusion cannot warm
            // a valid 0C latch through the 2C recovery threshold during the
            // sustained throughput measurement.
            SetTemperature(environment, position, temperatureC);
            yield return null;
        }

        float elapsed = clock.Time - startedAt;
        Pause(game, timeScale);
        yield return null;
        Require(elapsed >= seconds,
            $"WIM009 fixed-temperature window timed out at {elapsed:0.###}/{seconds:0.###} seconds.");
        observeElapsed?.Invoke(elapsed);
    }

    private static void Pause(
        GameManager game,
        IGameTimeScaleController timeScale)
    {
        game.isPause = true;
        timeScale.Scale = 0f;
    }

    private static void VerifyPowerPresenter(
        IObjectResolver container,
        string occurrenceInstanceId,
        PowerNetworkSnapshot snapshot)
    {
        Require(container.Resolve<IFeatureSurfaceTabPresenterRegistry>()
                .TryGet(TabId.Industry, out IFeatureSurfaceTabPresenter presenter),
            "Main Industry presenter is unavailable.");
        RecordingFeatureSurfaceView view = new();
        presenter.Present(view);
        string text = view.Text;
        Require(text.Contains("가용 전원", StringComparison.Ordinal)
            && text.Contains("폭염 전력부하", StringComparison.Ordinal)
            && text.Contains("-20%", StringComparison.Ordinal)
            && snapshot.CapacitySourceOccurrenceInstanceId
                == occurrenceInstanceId,
            "Industry network card omitted temporary capacity provenance: "
            + text);
    }

    private static void VerifyWaterPresenter(
        IObjectResolver container,
        BuildableObject pump,
        BuildingWaterConditionSnapshot snapshot)
    {
        GameObject panel = new(
            "Wim009_Temporary_Supply_Panel",
            typeof(RectTransform),
            typeof(Canvas));
        try
        {
            container.Resolve<IEnvironmentalBuildingPanelPresenter>().Render(
                panel.transform,
                pump,
                TMP_Settings.defaultFontAsset,
                _ => { },
                () => { });
            string text = string.Join("\n", panel
                .GetComponentsInChildren<TMP_Text>(true)
                .Select(value => value.text));
            Require(text.Contains("배관 회복 대기", StringComparison.Ordinal)
                && text.Contains("유량 80%", StringComparison.Ordinal)
                && text.Contains("1°C", StringComparison.Ordinal)
                && text.Contains(snapshot.SourceDisplayName,
                    StringComparison.Ordinal),
                "Building water-condition presenter omitted live hysteresis/provenance: "
                + text);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(panel);
        }
    }

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.01f;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RecordingFeatureSurfaceView : IFeatureSurfaceView
    {
        private readonly List<string> text = new();
        public string Text => string.Join("\n", text);

        public void AddSection(string title, string summary)
        {
            text.Add(title ?? string.Empty);
            text.Add(summary ?? string.Empty);
        }

        public void AddLabel(string value, float fontSize, float height) =>
            text.Add(value ?? string.Empty);

        public void AddDataCard(
            string actionName,
            string title,
            string detail,
            string buttonText,
            Action onClick,
            float height)
        {
            text.Add(title ?? string.Empty);
            text.Add(detail ?? string.Empty);
        }

        public void AddControlCard(
            string actionName,
            string title,
            string detail,
            IReadOnlyList<FeatureSurfaceStepper> steppers,
            IReadOnlyList<FeatureSurfaceAction> actions,
            float height)
        {
            text.Add(title ?? string.Empty);
            text.Add(detail ?? string.Empty);
        }

        public void ShowFeedback(string message) =>
            text.Add(message ?? string.Empty);

        public void RequestRefresh()
        {
        }
    }
}
#endif
