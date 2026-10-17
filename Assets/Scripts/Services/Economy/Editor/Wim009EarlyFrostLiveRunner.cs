#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

// Root-owned integration witness. Research, placement, climate, calendar and
// environmental cells are controlled setup. The observed event dispatch,
// temperature-gated growth, presenter and whole-save paths are production code.
public sealed class Wim009EarlyFrostLiveRunner
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-009-early-frost-live.txt";

    private const string FrostId = "seasonal:autumn-early-frost";
    private const string ClearFrontId = "weather:clear";
    private const string ColdFrontId = "weather:cold-snap";
    private const int ClearDay = 61;
    private const int FrostDay = 62;
    private const float ColdTemperatureC = -20f;
    private const float SuitableTemperatureC = 20f;

    private static bool running;
    private readonly List<string> lines = new();
    private readonly List<V20ContentEffectsResolvedEvent> frostResolutions =
        new();

    private DungeonRuntimeLifetimeScope scope;
    private CropPlotRuntime crops;
    private CropDefinitionSO crop;
    private IWorldItemStackRuntime items;
    private IGameEventBus events;
    private IClimatePersistence climate;
    private V20CampaignRuntime campaign;
    private ISeasonalEventQuery seasonal;
    private IDungeonGameSaveService saves;
    private IGameCalendar calendar;
    private IGameClock clock;
    private IGameTimeScaleController timeScale;
    private IEnvironmentalFieldPersistence environmentalField;
    private IDisposable frostResolutionSubscription;

    public static string StartFocused()
    {
        Require(
            Application.isPlaying && !running,
            "A fresh disposable main Play session is required; no duplicate run.");
        DungeonRuntimeLifetimeScope current =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(current?.Container != null, "Main scope has not initialized.");
        ((IDisposable)current.Container.Resolve<IDungeonSaveCommandService>())
            .Dispose();
        ((IDisposable)current.Container.Resolve<MetaProfilePersistenceService>())
            .Dispose();
        GameManager host = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        Require(host != null, "Missing actual main coroutine host.");
        host.isPause = true;
        current.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        try
        {
            host.StartCoroutine(new Wim009EarlyFrostLiveRunner().Observe());
        }
        catch
        {
            running = false;
            throw;
        }
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Observe()
    {
        Stack<IEnumerator> stack = new();
        stack.Push(Run());
        Exception failure = null;
        while (stack.Count > 0)
        {
            object value = null;
            bool moved;
            try
            {
                moved = stack.Peek().MoveNext();
                if (moved) value = stack.Peek().Current;
            }
            catch (Exception error)
            {
                failure = error;
                break;
            }
            if (!moved)
            {
                (stack.Pop() as IDisposable)?.Dispose();
                continue;
            }
            if (value is IEnumerator nested) stack.Push(nested);
            else yield return value;
        }
        while (stack.Count > 0)
            (stack.Pop() as IDisposable)?.Dispose();
        frostResolutionSubscription?.Dispose();
        Pause();
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add(
            "scope=actual main daily event subscriber/content transaction, real P23/P24 crop runtime, existing per-cell temperature authority, current crop presenter and current whole-world save; owner/party UI is real; research/placement/climate/calendar/environment cells are controlled setup; no natural weather selection/heating performance/AI farm/six-adult balance/whole-WIM009 claim");
        lines.Add(
            "cleanup=operator stops disposable protected Play; save/profile writers disposed; no scene or user persistence writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        running = false;
    }

    private IEnumerator Run()
    {
        scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        crops = scope.Container.Resolve<CropPlotRuntime>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        events = scope.Container.Resolve<IGameEventBus>();
        climate = scope.Container.Resolve<IClimatePersistence>();
        campaign = scope.Container.Resolve<V20CampaignRuntime>();
        seasonal = scope.Container.Resolve<ISeasonalEventQuery>();
        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        calendar = scope.Container.Resolve<IGameCalendar>();
        clock = scope.Container.Resolve<IGameClock>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        environmentalField = scope.Container.Resolve<IEnvironmentalFieldPersistence>();
        frostResolutionSubscription = events.Subscribe<V20ContentEffectsResolvedEvent>(
            OnContentEffectsResolved);

        IDisposable runFlowSubscriptions =
            scope.Container.Resolve<IDungeonRunFlowRuntime>() as IDisposable;
        Require(
            runFlowSubscriptions != null,
            "Run-flow subscription isolation is unavailable.");
        runFlowSubscriptions.Dispose();
        InvasionDirectorRuntime invasion =
            UnityEngine.Object.FindFirstObjectByType<InvasionDirectorRuntime>();
        Require(
            invasion != null && invasion.ActiveIntruders.Count == 0,
            "Frost witness requires a fresh world without an owned invasion.");
        InvasionThreatRuntime threat =
            UnityEngine.Object.FindFirstObjectByType<InvasionThreatRuntime>();
        Require(
            threat != null
            && !threat.IsCandidatePending
            && !threat.CapturePersistentState().CandidateRaisedThisCycle,
            "Frost witness requires a fresh threat cycle before isolation.");
        threat.enabled = false;
        lines.Add(
            "isolation=run-flow event subscriptions disposed and fresh invasion threat producer disabled; actual seasonal daily subscriber and save participants retained");

        OwnerRunManager owner =
            UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Missing main owner preparation.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(
                scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                    .TryReconcileNewRunTierZero(
                        out DungeonSpaceExpansionResult expansion,
                        out string expansionFailure)
                && expansion.CurrentInteriorColumns == 29,
                "Normal TierZero: " + expansionFailure);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(
                owner.CurrentOwnerActor != null,
                "Actual party UI did not publish the owner.");
        }
        foreach (CharacterActor actor in scope.Container
                     .Resolve<ICharacterAiWorldRegistry>().Characters
                     .Where(value => value != null))
            actor.SetAiPaused(true);
        Pause();
        yield return null;
        Require(
            clock.IsPaused && clock.DeltaTime == 0f,
            "Paused clock did not settle before the controlled crop witness.");

        BlueprintResearchRuntime research = scope.Container
            .Resolve<ProgressionSceneRuntimeReferences>().BlueprintResearch;
        foreach (string id in new[] { "gathering", "field", "indoor" })
        {
            Require(
                research.TryCompleteProjectImmediatelyForVerification(
                    new ResearchProjectId("research:agriculture:" + id),
                    out string researchFailure),
                "Research setup: " + researchFailure);
        }
        Require(
            scope.Container.Resolve<IResourceEconomyContentCatalog>()
                .TryGetCrop("crop:twilight-grain", out crop),
            "Missing authored twilight grain.");
        Require(
            crop.TemperatureRange == new Vector2(4f, 30f),
            "The independent twilight-grain temperature fixture changed.");

        string outdoorId = PlacePlot(
            "Assets/Resources/SO/Building/Modular/P23_야외경작지.asset",
            expectedIndoor: false);
        string indoorId = PlacePlot(
            "Assets/Resources/SO/Building/Modular/P24_실내재배조.asset",
            expectedIndoor: true);
        Sow(outdoorId);
        Sow(indoorId);
        lines.Add(
            "crops=actual P23 outdoor + P24 indoor; twilight-grain; every runtime RequiredMaterials lot physically supplied and sow work completed");

        PrepareSeasonalEligibility(ClearDay - 1);
        PrepareClimate(ClearDay, ClearFrontId, 2);
        calendar.SetDateTime(ClearDay, 8);
        events.Publish(new OperatingDayStartedEvent(ClearDay));
        IV20DailyEvaluationDiagnostic daily =
            scope.Container.Resolve<IV20DailyEvaluationDiagnostic>();
        Require(
            daily.LastDailyEvaluationAbsoluteDay == ClearDay
            && daily.LastDailyEvaluationSucceeded,
            "Clear-front daily dispatch failed: "
            + daily.LastDailyEvaluationFailure);
        Require(
            seasonal.ActiveSeasonalEvents.All(value =>
                value.definitionId != FrostId)
            && frostResolutions.Count == 0,
            "Early frost occurred without the required cold-snap front.");
        lines.Add(
            "[PASS] actual main day61 clear-front dispatch created no early-frost occurrence or resolution");

        PrepareClimate(FrostDay, ColdFrontId, 3);
        calendar.SetDateTime(FrostDay, 8);
        events.Publish(new OperatingDayStartedEvent(FrostDay));
        Require(
            daily.LastDailyEvaluationAbsoluteDay == FrostDay
            && daily.LastDailyEvaluationSucceeded,
            "Cold-front daily dispatch failed: "
            + daily.LastDailyEvaluationFailure);
        V20ActiveEventSaveData occurrence = seasonal.ActiveSeasonalEvents
            .Single(value => value.definitionId == FrostId);
        Require(
            occurrence.startedAbsoluteDay == FrostDay
            && occurrence.deadlineAbsoluteDay - occurrence.startedAbsoluteDay
                is >= 2 and <= 4,
            "Early-frost occurrence lost its authored 2-4 day lifetime.");
        Require(
            frostResolutions.Count == 1
            && frostResolutions[0].ResolutionId == "started"
            && frostResolutions[0].Effects.Count == 0
            && frostResolutions[0].PhysicalEffectsApplied,
            "Actual daily postcommit frost resolution is missing, duplicated, or carries a legacy effect.");
        lines.Add(
            "[PASS] actual main day62 cold-snap dispatch created exactly one early-frost occurrence; deadlineDelta="
            + (occurrence.deadlineAbsoluteDay - occurrence.startedAbsoluteDay)
            + "; startEffects=0");

        SetEnvironment(outdoorId, ColdTemperatureC);
        SetEnvironment(indoorId, SuitableTemperatureC);
        float outdoorBefore = Row(outdoorId).growthHours;
        float indoorBefore = Row(indoorId).growthHours;
        Resume();
        float wall = Time.realtimeSinceStartup;
        while (Row(indoorId).growthHours <= indoorBefore
               && Time.realtimeSinceStartup - wall < 5f)
            yield return null;
        Pause();
        yield return null;
        CropPlotSnapshot coldOutdoor = Snapshot(outdoorId);
        CropPlotSnapshot warmIndoor = Snapshot(indoorId);
        Require(
            Mathf.Approximately(Row(outdoorId).growthHours, outdoorBefore)
            && Row(indoorId).growthHours > indoorBefore
            && coldOutdoor.TemperatureStatus
                == CropGrowthTemperatureStatus.TooCold
            && warmIndoor.TemperatureStatus
                == CropGrowthTemperatureStatus.Suitable,
            "Existing temperature authority did not stop the cold outdoor crop while allowing the suitable indoor crop.");
        RenderTemperature(outdoorId, ColdTemperatureC, expectCold: true);
        RenderTemperature(indoorId, SuitableTemperatureC, expectCold: false);
        lines.Add(
            "[PASS] real crop ticks under active frost: outdoor -20C growth unchanged/TooCold; indoor 20C growth advanced/Suitable; actual presenter reports current temperature and adjusted range");

        float outdoorCold = Row(outdoorId).growthHours;
        SetEnvironment(outdoorId, SuitableTemperatureC);
        Resume();
        wall = Time.realtimeSinceStartup;
        while (Row(outdoorId).growthHours <= outdoorCold
               && Time.realtimeSinceStartup - wall < 5f)
            yield return null;
        Pause();
        yield return null;
        Require(
            Row(outdoorId).growthHours > outdoorCold
            && Snapshot(outdoorId).TemperatureStatus
                == CropGrowthTemperatureStatus.Suitable,
            "Warming the actual outdoor plot did not resume its existing growth path.");
        RenderTemperature(outdoorId, SuitableTemperatureC, expectCold: false);
        lines.Add(
            "[PASS] same outdoor crop resumed growth at 20C while the frost occurrence remained active; no fixed frost multiplier or crop reset");

        DungeonGameSaveData frozenWorld =
            saves.FromJson(saves.ToJson(saves.Capture()));
        string occurrenceId = occurrence.instanceId;
        int deadline = occurrence.deadlineAbsoluteDay;
        PrepareClimate(FrostDay, ClearFrontId, 1);
        SeasonalEventWorldSaveData changedSeason = campaign.CaptureSeasonal();
        changedSeason.activeEvents.RemoveAll(value =>
            value.instanceId == occurrenceId);
        campaign.PublishSeasonal(campaign.PrepareSeasonal(changedSeason));
        SetEnvironment(outdoorId, ColdTemperatureC);
        Require(
            !seasonal.ActiveSeasonalEvents.Any(value =>
                value.instanceId == occurrenceId),
            "Controlled pre-restore mutation did not remove the occurrence.");
        Require(
            saves.TryRestore(frozenWorld, out DungeonGameRestoreReport restored)
            && restored.Success,
            "Current whole-world frost restore failed: "
            + string.Join(" | ", restored.Errors));
        yield return null;
        Require(
            scope.Container.Resolve<IClimateQuery>().WeatherFrontId
                == ColdFrontId
            && seasonal.ActiveSeasonalEvents.Single(value =>
                value.instanceId == occurrenceId).deadlineAbsoluteDay == deadline
            && Snapshot(outdoorId).TemperatureStatus
                == CropGrowthTemperatureStatus.Suitable
            && Snapshot(indoorId).TemperatureStatus
                == CropGrowthTemperatureStatus.Suitable,
            "Whole-world restore lost the climate/event deadline or crop environment projection.");
        lines.Add(
            "[PASS] current whole-world JSON restored cold-snap, exact frost instance/deadline and both real plot environment projections; no frost-specific crop save field");

        string cropsBeforeExpiry = JsonUtility.ToJson(crops.Capture());
        PrepareClimate(deadline + 1, ClearFrontId, 2);
        calendar.SetDateTime(deadline + 1, 8);
        events.Publish(new OperatingDayStartedEvent(deadline + 1));
        Require(
            daily.LastDailyEvaluationAbsoluteDay == deadline + 1
            && daily.LastDailyEvaluationSucceeded,
            "Frost-expiry daily dispatch failed: "
            + daily.LastDailyEvaluationFailure);
        Require(
            !seasonal.ActiveSeasonalEvents.Any(value =>
                value.instanceId == occurrenceId)
            && JsonUtility.ToJson(crops.Capture()) == cropsBeforeExpiry,
            "Frost expiry retained its occurrence or mutated crop authority.");
        lines.Add(
            "[PASS] day after deadline removed the event occurrence without healing, damaging, rerolling or otherwise mutating either crop");
    }

    private void OnContentEffectsResolved(V20ContentEffectsResolvedEvent value)
    {
        if (value.DefinitionId == FrostId)
            frostResolutions.Add(value);
    }

    private void PrepareSeasonalEligibility(int lastEvaluationDay)
    {
        V20StoryContentCatalog catalog =
            scope.Container.Resolve<V20StoryContentCatalog>();
        SeasonalEventWorldSaveData data = campaign.CaptureSeasonal();
        data.activeEvents.Clear();
        data.completedEventIds = data.completedEventIds
            .Concat(catalog.SeasonalEvents.Where(value =>
                value.season == Season.Autumn && value.StableId != FrostId)
                .Select(value => value.StableId))
            .Where(value => !string.IsNullOrWhiteSpace(value)
                && value != FrostId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        data.lastEvaluationAbsoluteDay = lastEvaluationDay;
        campaign.PublishSeasonal(campaign.PrepareSeasonal(data));
    }

    private void PrepareClimate(int day, string frontId, int remainingDays)
    {
        ClimateWorldSaveData data = climate.Capture();
        Require(
            !string.IsNullOrWhiteSpace(data.climateZoneId),
            "Current climate zone is unavailable.");
        data.absoluteDay = day;
        data.weatherFrontId = frontId;
        data.frontRemainingDays = remainingDays;
        data.dailyNoiseC = 0f;
        climate.PublishRestore(climate.PrepareRestore(data));
        Require(
            scope.Container.Resolve<IClimateQuery>().AbsoluteDay == day
            && scope.Container.Resolve<IClimateQuery>().WeatherFrontId
                == frontId,
            "Typed climate preparation did not publish the requested current front.");
    }

    private string PlacePlot(string assetPath, bool expectedIndoor)
    {
        Require(
            scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(
                out Grid grid),
            "Missing actual grid.");
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            assetPath);
        Require(definition != null, "Missing authored crop plot: " + assetPath);
        BuildingCropPlotAbility ability =
            definition.GetAbility<BuildingCropPlotAbility>();
        Require(
            ability != null && ability.Indoor == expectedIndoor,
            "Crop plot indoor contract mismatch: " + assetPath);
        Vector2Int? anchor = null;
        foreach (RoomInstance room in scope.Container
                     .Resolve<IRoomLayoutCache>().GetLayout(grid).Rooms
                     .Where(value => value.IsUsable)
                     .OrderBy(value => value.Bounds.yMin)
                     .ThenBy(value => value.Bounds.xMin))
        {
            foreach (Vector2Int position in room.Cells
                         .OrderBy(value => value.y)
                         .ThenBy(value => value.x))
            {
                if (definition.GetGridPosList(position).All(cellPosition =>
                        room.ContainsCell(cellPosition)
                        && grid.GetGridCell(cellPosition) is GridCell cell
                        && cell.CanBuildInArea(definition)
                        && cell.CanOccupy(definition.Placement.Layer)))
                {
                    anchor = position;
                    break;
                }
            }
            if (anchor.HasValue) break;
        }
        Require(
            anchor.HasValue,
            "No legal crop footprint; do not expand or overwrite a facility: "
            + assetPath);
        BuildableObject plot = scope.Container
            .Resolve<IGridBuildingObjectFactory>()
            .Create(grid, definition, anchor.Value);
        Require(plot != null, "Actual crop factory failed: " + assetPath);
        foreach (MonoBehaviour component in plot
                     .GetComponentsInChildren<MonoBehaviour>(true))
            scope.Container.Inject(component);
        plot.SetGrid(grid);
        plot.Initialization(definition, anchor.Value);
        Require(
            grid.RegisterOccupant(
                plot,
                definition.Placement.Layer,
                definition.GetGridPosList(anchor.Value),
                definition.Placement.IsMovement),
            "Actual crop grid registration failed: " + assetPath);
        crops.Restore(crops.BuildRestore(crops.Capture()));
        string id = plot.RequirePersistentInstanceId().Value;
        Require(
            scope.Container.Resolve<IBuildingWorldQuery>().Buildings
                .Contains(plot),
            "Unpublished crop fixture: " + assetPath);
        SetEnvironment(id, SuitableTemperatureC);
        return id;
    }

    private void Sow(string plotId)
    {
        BuildableObject plot = Plot(plotId);
        Require(
            crops.TrySetCrop(plot, crop.CropId, out string setFailure),
            setFailure);
        crops.Tick();
        CropPlotSnapshot row = Snapshot(plotId);
        foreach (KeyValuePair<string, int> input in row.RequiredMaterials)
        {
            int spawned;
            bool succeeded = input.Key == crop.SeedItemId
                ? scope.Container.Resolve<IItemTransferService>()
                    .TrySpawnItemWithComponents(
                        input.Key,
                        input.Value,
                        plot.centerPos,
                        WorldItemStackState.FacilityBuffer,
                        row.MaterialDestinationId,
                        new[]
                        {
                            SeedLotItemStateCodec.Encode(new SeedLotState
                            {
                                cropId = crop.CropId,
                                cultivarGenomeId = crop.BaseGenome.GenomeId,
                                generation = 0,
                                pathogenLoad = 0
                            })
                        },
                        out spawned)
                : items.SpawnItemAt(
                    input.Key,
                    input.Value,
                    plot.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    row.MaterialDestinationId,
                    out spawned);
            Require(
                succeeded && spawned == input.Value,
                "Physical sow setup failed: " + input.Key);
        }
        crops.Tick();
        Require(
            crops.TryGetWork(
                plot,
                BuiltInWorkTypeIds.Sow,
                out CropPlotWorkSnapshot work)
            && work.Available,
            "Sow unavailable: " + work.UnavailableReason);
        Require(
            crops.ApplyWork(
                plot,
                BuiltInWorkTypeIds.Sow,
                work.RequiredWork,
                out bool completed)
            && completed,
            "Actual sow work failed.");
        Require(
            Row(plotId).phase == CropPlotPhase.Growing,
            "Completed sow did not start its actual cycle.");
    }

    private void SetEnvironment(string plotId, float temperatureC)
    {
        Vector2Int position = Plot(plotId).centerPos;
        DungeonEnvironmentalFieldSaveData data = environmentalField.Capture();
        data.cells.RemoveAll(value =>
            value.x == position.x && value.y == position.y);
        data.cells.Add(new EnvironmentalCellSaveData
        {
            x = position.x,
            y = position.y,
            temperatureC = temperatureC,
            airQuality = 100f,
            lightLevel = 100f
        });
        data.cells.Sort((left, right) =>
        {
            int leftIndex = checked(left.y * data.width + left.x);
            int rightIndex = checked(right.y * data.width + right.x);
            return leftIndex.CompareTo(rightIndex);
        });
        environmentalField.Restore(environmentalField.PrepareRestore(data));
    }

    private void RenderTemperature(
        string plotId,
        float expectedTemperatureC,
        bool expectCold)
    {
        string before = JsonUtility.ToJson(crops.Capture());
        GameObject panel = new(
            "Wim009_Early_Frost_Panel",
            typeof(RectTransform),
            typeof(Canvas));
        try
        {
            scope.Container.Resolve<ICropPlotBuildingPanelPresenter>().Render(
                panel.transform,
                Plot(plotId),
                TMPro.TMP_Settings.defaultFontAsset,
                _ => { },
                () => { });
            string text = string.Join(
                "\n",
                panel.GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .Select(value => value.text));
            CropPlotSnapshot snapshot = Snapshot(plotId);
            string current = $"현재 {expectedTemperatureC:0.#}℃";
            string range = $"{snapshot.MinimumTemperatureC:0.#}~"
                + $"{snapshot.MaximumTemperatureC:0.#}℃";
            Require(
                text.Contains(current, StringComparison.Ordinal)
                && text.Contains(range, StringComparison.Ordinal)
                && (expectCold
                    ? text.Contains("저온으로 성장 정지", StringComparison.Ordinal)
                    : !text.Contains("저온으로 성장 정지", StringComparison.Ordinal)),
                "Player crop presenter temperature mismatch: " + text);
            Require(
                JsonUtility.ToJson(crops.Capture()) == before,
                "Read-only crop presenter mutated crop state.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(panel);
        }
    }

    private BuildableObject Plot(string id) => scope.Container
        .Resolve<IBuildingWorldQuery>().Buildings.Single(value =>
            value != null && value.RequirePersistentInstanceId().Value == id);

    private CropPlotSaveData Row(string id) => crops.Capture().plots.Single(
        value => value.buildingInstanceId == id);

    private CropPlotSnapshot Snapshot(string id) => crops.Plots.Single(
        value => value.PlotId == id);

    private void Pause()
    {
        GameManager game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        if (game != null) game.isPause = true;
        if (timeScale != null) timeScale.Scale = 0f;
    }

    private void Resume()
    {
        UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = false;
        timeScale.Scale = 2f;
    }

    private static void Click(string name)
    {
        Button button = Resources.FindObjectsOfTypeAll<Button>()
            .SingleOrDefault(value => value != null
                && value.gameObject.scene.isLoaded
                && value.gameObject.activeInHierarchy
                && value.name == name);
        Require(
            button != null
            && button.IsInteractable()
            && PlayModeVerificationFrameWait.DispatchPointerClick(
                button.gameObject,
                Vector2.zero),
            "Actual UI button unavailable: " + name);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
