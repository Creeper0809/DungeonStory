#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class CropPlotDebugScenarios
{
    private const string ReportPath =
        "docs/implementation-reports/crop-plot-runtime-latest.txt";
    public const string RequestPath =
        "Temp/v27-crop-plot-runtime.request";

    // Actual main services and actual player presenter, with an explicitly
    // controlled environmental checkpoint. No natural farm/AI claim.
    public static bool RunWim016LightObservationFocused(out string report)
    {
        var lines = new List<string> {
            "WIM016 main field -> crop snapshot -> player presenter",
            "scope=protected paused Play; real sow/WIP; controlled field checkpoints",
            "not-tested=natural growth/depletion, staff AI, lamp construction, irrigation" };
        GameObject fixture = null;
        GameObject panel = null;
        IEnvironmentalFieldPersistence fieldPersistence = null;
        DungeonEnvironmentalFieldSaveData originalField = null;
        try
        {
            Require(Application.isPlaying && Time.timeScale == 0,
                "Requires protected paused disposable main Play.");
            var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
            Require(scope?.Container != null, "Missing main scope.");
            var runtime = scope.Container.Resolve<CropPlotRuntime>();
            var items = scope.Container.Resolve<IWorldItemStackRuntime>();
            var transfers = scope.Container.Resolve<IItemTransferService>();
            var catalog = scope.Container.Resolve<IResourceEconomyContentCatalog>();
            var research = scope.Container.Resolve<ProgressionSceneRuntimeReferences>().BlueprintResearch;
            foreach (string id in new[] { "research:agriculture:gathering", "research:agriculture:field" })
                Require(research.TryCompleteProjectImmediatelyForVerification(new ResearchProjectId(id), out string failure), failure);
            Require(scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid), "Missing grid.");
            fieldPersistence = scope.Container.Resolve<IEnvironmentalFieldPersistence>();
            originalField = fieldPersistence.Capture();
            Require(catalog.TryGetCrop("crop:twilight-grain", out CropDefinitionSO crop), "Missing authored grain.");
            fixture = new GameObject("Wim016_Light_Observation_Witness");
            var plot = fixture.AddComponent<Facility>();
            scope.Container.Inject(plot);
            plot.SetGrid(grid);
            plot.Initialization(LoadBuilding("P23"), new Vector2Int(4, 0));
            runtime.Restore(runtime.BuildRestore(runtime.Capture()));
            Require(runtime.TrySetCrop(plot, crop.CropId, out string cropFailure), cropFailure);
            runtime.Tick();
            string plotId = plot.RequirePersistentInstanceId().Value;
            var waiting = runtime.Plots.Single(p => p.PlotId == plotId);
            foreach (var input in waiting.RequiredMaterials)
                Require(SpawnCropMaterial(items, transfers, crop, input.Key, input.Value,
                    plot.centerPos, waiting.MaterialDestinationId, out int spawned) && spawned == input.Value,
                    "Physical sow input preparation failed: " + input.Key);
            runtime.Tick();
            Require(runtime.TryGetWork(plot, BuiltInWorkTypeIds.Sow, out var sow) && sow.Available,
                "Sow unavailable: " + sow.UnavailableReason);
            Require(runtime.ApplyWork(plot, BuiltInWorkTypeIds.Sow, sow.RequiredWork, out bool done) && done,
                "Actual sow failed.");
            var presenter = scope.Container.Resolve<ICropPlotBuildingPanelPresenter>();
            string before = JsonUtility.ToJson(runtime.Capture());
            foreach (var row in new (float Light, float Multiplier, CropGrowthLightStatus Status, string Ui)[] {
                (0, 0, CropGrowthLightStatus.Stopped, "광량·현재0·정지5·충분50·성장0%"),
                (20, 1f / 3f, CropGrowthLightStatus.Slowed, "광량·현재20·정지5·충분50·성장33%"),
                (50, 1, CropGrowthLightStatus.Normal, "광량·현재50·정지5·충분50·성장100%"),
                (100, 1, CropGrowthLightStatus.Normal, "광량·현재100·정지5·충분50·성장100%"),
                (0, 0, CropGrowthLightStatus.Stopped, "광량·현재0·정지5·충분50·성장0%") })
            {
                var field = JsonUtility.FromJson<DungeonEnvironmentalFieldSaveData>(JsonUtility.ToJson(originalField));
                field.cells.RemoveAll(c => c.x == plot.centerPos.x && c.y == plot.centerPos.y);
                field.cells.Add(new EnvironmentalCellSaveData { x = plot.centerPos.x, y = plot.centerPos.y,
                    temperatureC = 20, airQuality = 100, lightLevel = row.Light });
                fieldPersistence.Restore(fieldPersistence.PrepareRestore(field));
                // Deliberately no crop Tick: environment revision alone must
                // refresh a stopped plot's derived observation without mutation.
                var observed = runtime.Plots.Single(p => p.PlotId == plotId);
                Require(observed.CurrentLight == row.Light && observed.LightStatus == row.Status
                    && Mathf.Approximately(observed.LightGrowthMultiplier, row.Multiplier),
                    "Actual field change did not reach crop projection: " + row.Light);
                Require(JsonUtility.ToJson(runtime.Capture()) == before,
                    "Readonly light observation changed crop/water ownership.");
                panel = new GameObject("Wim016_Crop_Panel_Witness", typeof(RectTransform), typeof(Canvas));
                presenter.Render(panel.transform, plot, TMPro.TMP_Settings.defaultFontAsset, _ => { }, () => { });
                string text = string.Join("\n", panel.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.text));
                string lightLine = text.Split('\n').SingleOrDefault(line => line.StartsWith("광량 ·", StringComparison.Ordinal));
                Require(lightLine != null && lightLine.Replace(" ", "").Replace("\u00a0", "") == row.Ui,
                    "Actual player crop panel light values stale/wrong: " + lightLine);
                Require(text.Contains("급수") || text.Contains("수분"), "Crop panel lost separate water observation.");
                lines.Add($"[PASS] actual field {row.Light} -> {observed.LightStatus}/{observed.LightGrowthMultiplier:R}; presenter contains light and water; readonly crop capture exact");
                UnityEngine.Object.DestroyImmediate(panel);
                panel = null;
            }
            lines.Add("result=PASS; controlled main-service/presenter witness, not natural UI selection or growth");
            report = string.Join("\n", lines);
            return true;
        }
        catch (Exception error)
        {
            lines.Add("result=FAIL\n" + error);
            report = string.Join("\n", lines);
            return false;
        }
        finally
        {
            if (panel != null) UnityEngine.Object.DestroyImmediate(panel);
            if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            if (fieldPersistence != null && originalField != null)
                fieldPersistence.Restore(fieldPersistence.PrepareRestore(originalField));
        }
    }

    // Root-owned scoped service witness. Run only in disposable protected Play,
    // after the medical witness is terminal and before Stop. It deliberately
    // prepares a dry current-format checkpoint; it is not a natural growth/AI test.
    public static string StartWim016FiniteNetworkFocused()
    {
        Require(Application.isPlaying,
            "Operator must enter a new disposable main Play session before this witness.");
        Require(UnityEngine.Object.FindFirstObjectByType<Wim016FiniteNetworkPlayModeRunner>() == null,
            "Finite-network witness is already running; do not start a duplicate.");
        var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Missing main scope; do not run before initialization.");
        // Reuse the verified WIM048 public persistence protection, before any fixture,
        // party action or clock advancement. No user save/settings file is written.
        var saveCommands = scope.Container.Resolve<IDungeonSaveCommandService>();
        var metaPersistence = scope.Container.Resolve<MetaProfilePersistenceService>();
        Require(saveCommands is IDisposable && metaPersistence is IDisposable,
            "Cannot protect the disposable session's persistence services.");
        ((IDisposable)saveCommands).Dispose();
        ((IDisposable)metaPersistence).Dispose();
        UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = true;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
        new GameObject("Wim016FiniteNetworkWitness")
            .AddComponent<Wim016FiniteNetworkPlayModeRunner>();
        return "RUNNING: " + Wim016FiniteNetworkPlayModeRunner.ReportPath;
    }

    public static IEnumerator PrepareWim016FiniteNetworkFocused(IList<string> lines)
    {
        Require(Application.isPlaying && Time.timeScale == 0f,
            "Main Play must be paused during witness preparation.");
        var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Missing main scope.");
        var owner = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Main owner manager is unavailable.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                    .TryReconcileNewRunTierZero(out var expansion, out string expansionFailure),
                "Normal TierZero preparation failed: " + expansionFailure);
            Require(expansion.CurrentInteriorColumns == 29,
                "Unexpected authored TierZero; do not change capacity just for this test.");
            var ownerButton = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Button>()
                .SingleOrDefault(value => value != null && value.gameObject.scene.isLoaded
                    && value.gameObject.activeInHierarchy && value.name == "OwnerOption_1001");
            Require(ownerButton != null && ownerButton.IsInteractable()
                    && PlayModeVerificationFrameWait.DispatchPointerClick(ownerButton.gameObject, Vector2.zero),
                "Actual owner preparation UI is unavailable.");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = true;
            scope.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
            Require(owner.CurrentOwnerActor != null, "Actual party UI did not create the main owner.");
            lines.Add("setup=persistence disposed before actual owner/party UI; normal TierZero; re-paused; no save writes");
        }
        Require(scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid),
            "Missing real main Grid.");
        var research = scope.Container.Resolve<ProgressionSceneRuntimeReferences>().BlueprintResearch;
        foreach (string id in new[] { "gathering", "field", "compost", "irrigation" })
            Require(research.TryCompleteProjectImmediatelyForVerification(
                    new ResearchProjectId("research:agriculture:" + id), out string failure),
                "Research fixture preparation failed: " + failure);
        var definitions = new[]
        {
            LoadBuilding("RF02"),
            AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Industrial/I08_상수_탱크.asset"),
            LoadBuilding("P23")
        };
        Require(definitions.All(value => value != null), "Missing authored RF02/I08/P23.");
        var offsets = new[] { Vector2Int.zero, new Vector2Int(2, 0), new Vector2Int(4, 0) };
        var layouts = scope.Container.Resolve<IRoomLayoutCache>();
        Vector2Int? selected = null;
        foreach (var room in layouts.GetLayout(grid).Rooms.Where(value => value != null && value.IsUsable)
                     .OrderBy(value => value.Bounds.yMin).ThenBy(value => value.Bounds.xMin))
        {
            foreach (Vector2Int cell in room.Cells.OrderBy(value => value.y).ThenBy(value => value.x))
            {
                bool legal = true;
                var claimed = new HashSet<(GridLayer, Vector2Int)>();
                for (int index = 0; index < definitions.Length && legal; index++)
                {
                    BuildingSO definition = definitions[index];
                    legal = definition.GetGridPosList(cell + offsets[index]).All(footprint =>
                        room.ContainsCell(footprint)
                        && grid.GetGridCell(footprint) is GridCell gridCell
                        && gridCell.CanBuildInArea(definition)
                        && gridCell.CanOccupy(definition.Placement.Layer)
                        && claimed.Add((definition.Placement.Layer, footprint)));
                }
                if (!legal) continue;
                selected = cell;
                break;
            }
            if (selected.HasValue) break;
        }
        Require(selected.HasValue, "No existing usable room has a legal six-cell witness footprint.");
        var factory = scope.Container.Resolve<IGridBuildingObjectFactory>();
        var placed = new BuildableObject[3];
        for (int index = 0; index < placed.Length; index++)
        {
            Vector2Int anchor = selected.Value + offsets[index];
            BuildingSO definition = definitions[index];
            BuildableObject building = factory.Create(grid, definition, anchor);
            Require(building != null, "Main building factory rejected witness.");
            placed[index] = building;
            foreach (MonoBehaviour component in building.GetComponentsInChildren<MonoBehaviour>(true))
                scope.Container.Inject(component);
            building.SetGrid(grid);
            building.Initialization(definition, anchor);
            Require(grid.RegisterOccupant(building, definition.Placement.Layer,
                    definition.GetGridPosList(anchor), definition.Placement.IsMovement),
                "Main Grid rejected preflighted witness footprint.");
        }
        lines.Add($"setup=existing usable room; RF02={placed[0].centerPos}; I08={placed[1].centerPos}; P23={placed[2].centerPos}; factory placement/research/input are controlled preparation");
        IEnumerator witness = RunWim016FiniteNetworkFocused(placed[2], placed[1], placed[0], lines);
        try
        {
            while (witness.MoveNext()) yield return witness.Current;
        }
        finally
        {
            (witness as IDisposable)?.Dispose();
            // Do not destroy registered owners in isolation: operator stops disposable
            // Play after observing the result, reverting the complete prepared world.
        }
    }

    // Root-owned integration witness. The caller supplies real, registered main-world
    // buildings in a legal room, and must protect persistence before disposable Play.
    // This does not substitute direct irrigation calls for the normal crop scheduler.
    public static IEnumerator RunWim016FiniteNetworkFocused(
        BuildableObject plot,
        BuildableObject tank,
        BuildableObject irrigator,
        IList<string> lines)
    {
        Require(Application.isPlaying && Time.timeScale == 0f,
            "Requires paused, persistence-protected disposable main Play.");
        var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Missing main runtime scope.");
        var services = scope.Container;
        var runtime = services.Resolve<CropPlotRuntime>();
        var fluid = services.Resolve<IFluidInfrastructureTransaction>();
        var water = services.Resolve<IFluidInfrastructureQuery>();
        var irrigation = services.Resolve<ICropIrrigationRuntime>();
        var fluidPersistence = services.Resolve<IFluidInfrastructurePersistence>();
        var saves = services.Resolve<IDungeonGameSaveService>();
        var clock = services.Resolve<IGameClock>();
        var game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        var timeScale = services.Resolve<IGameTimeScaleController>();
        var world = services.Resolve<IBuildingWorldQuery>();
        var items = services.Resolve<IWorldItemStackRuntime>();
        var transfers = services.Resolve<IItemTransferService>();
        var catalog = services.Resolve<IResourceEconomyContentCatalog>();
        Require(game != null && new[] { plot, tank, irrigator }.All(value =>
                value != null && world.Buildings.Contains(value)),
            "Witness buildings must belong to the real published main world.");
        Require(plot.BuildingData?.id == 1095 && tank.BuildingData?.id == 9817
                && irrigator.BuildingData?.id == 8802,
            "Witness must use authored P23, I08 and RF02 definitions.");
        Require(services.Resolve<IFacilityCapabilityQuery>()
                .FindOperational(FacilityCapabilityKind.None).Contains(irrigator),
            "RF02 must have an actual usable room, not a detached eligibility stub.");
        Require(water.TryGetNetwork(tank, out var tankNetwork)
                && water.TryGetNetwork(irrigator, out var irrigationNetwork)
                && tankNetwork.NetworkId == irrigationNetwork.NetworkId
                && tankNetwork.Channel == UtilityChannel.CleanWater
                && tankNetwork.CleanWater == 0f,
            "Requires an empty, isolated real I08/RF02 clean-water network.");
        string isolatedNetworkId = tankNetwork.NetworkId;
        string plotInstanceId = plot.RequirePersistentInstanceId().Value;
        string tankInstanceId = tank.RequirePersistentInstanceId().Value;
        string irrigatorInstanceId = irrigator.RequirePersistentInstanceId().Value;
        Require(world.Buildings.Count(value => value != null
                && water.TryGetNetwork(value, out var network)
                && network.NetworkId == isolatedNetworkId) == 2,
            "Finite source has another connected producer or consumer.");
        Require(catalog.TryGetCrop("crop:twilight-grain", out CropDefinitionSO crop),
            "Missing authored crop:twilight-grain.");
        runtime.Restore(runtime.BuildRestore(runtime.Capture()));
        Require(runtime.TrySetCrop(plot, crop.CropId, out string cropFailure), cropFailure);
        runtime.Tick(); // Paused preparation only; never drive Tick during live frames.
        string plotId = plotInstanceId;
        CropPlotSnapshot row = runtime.Plots.Single(value => value.PlotId == plotId);
        foreach (var input in row.RequiredMaterials)
            Require(SpawnCropMaterial(items, transfers, crop, input.Key, input.Value,
                    plot.centerPos, row.MaterialDestinationId, out int spawned)
                && spawned == input.Value, "Physical sow input failed: " + input.Key);
        runtime.Tick();
        Require(runtime.TryGetWork(plot, BuiltInWorkTypeIds.Sow, out var sow)
                && sow.Available, "Authored sow work unavailable.");
        Require(runtime.ApplyWork(plot, BuiltInWorkTypeIds.Sow, sow.RequiredWork,
                out bool sowed) && sowed, "Physical sow did not commit.");
        row = runtime.Plots.Single(value => value.PlotId == plotId);
        Require(row.Phase == CropPlotPhase.Growing && row.CurrentWater == 1f,
            "Sow must publish exactly one initial water.");

        // Explicit counterfactual checkpoint: no natural time-to-dry claim is made.
        var dry = runtime.Capture();
        var dryRow = dry.plots.Single(value => value.buildingInstanceId == plotId);
        Require(dryRow.waterRefill.phase == CropWaterRefillPhase.None,
            "Cannot overwrite a manual refill owner to prepare irrigation.");
        dryRow.currentWater = 0f;
        runtime.Restore(runtime.BuildRestore(dry));
        var request = new CropIrrigationRequest(plot, true, 0f, 2f, false);
        var assessment = irrigation.Assess(request);
        string emptyCrop = JsonUtility.ToJson(runtime.Capture());
        string emptyFluid = JsonUtility.ToJson(fluidPersistence.Capture());
        Require(assessment.Status == CropIrrigationStatus.WaterUnavailable
                && water.TryGetNetwork(tank, out tankNetwork)
                && tankNetwork.CleanWater == 0f,
            "Empty actual clean-water network did not expose an unavailable irrigation state.");
        for (int query = 0; query < 5; query++)
        {
            irrigation.Assess(request);
            _ = runtime.Plots;
            _ = runtime.Capture();
        }
        Require(JsonUtility.ToJson(runtime.Capture()) == emptyCrop
                && JsonUtility.ToJson(fluidPersistence.Capture()) == emptyFluid,
            "Unavailable irrigation observation mutated crop or physical fluid authority.");
        Require(RenderCropPanelText(services, plot).Contains(
                "배관망에 깨끗한 물이 부족",
                StringComparison.Ordinal),
            "Actual crop panel did not expose the empty-network reason.");
        lines.Add("[PASS] empty actual network -> unavailable UI; repeated assess/capture leaves crop and fluid byte-equivalent");

        DungeonGameSaveData dryWhole = saves.FromJson(saves.ToJson(saves.Capture()));
        runtime.Tick();
        row = runtime.Plots.Single(value => value.PlotId == plotId);
        Require(row.WaterRefillPhase == CropWaterRefillPhase.WaitingForDelivery,
            "Empty network did not publish the real manual refill owner.");
        Require(fluid.TryAdd(tank, WorldWaterQuality.Clean, 1f, out float manualBudget)
                && manualBudget == 1f,
            "Manual-owner suppression budget must be exactly one water.");
        assessment = irrigation.Assess(new CropIrrigationRequest(
            plot, true, 0f, 2f, true));
        float manualGameStart = clock.Time;
        float manualWallStart = Time.realtimeSinceStartup;
        game.isPause = false;
        timeScale.Scale = 1f;
        while (clock.Time - manualGameStart < 0.333333f
            && Time.realtimeSinceStartup - manualWallStart < 15f)
            yield return null;
        game.isPause = true;
        timeScale.Scale = 0f;
        row = runtime.Plots.Single(value => value.PlotId == plotId);
        Require(assessment.Status == CropIrrigationStatus.ManualRefillActive
                && clock.Time - manualGameStart >= 0.333333f
                && row.CurrentWater == 0f
                && row.WaterRefillPhase != CropWaterRefillPhase.None
                && water.TryGetNetwork(tank, out tankNetwork)
                && tankNetwork.CleanWater == 1f,
            "A live positive-time scheduler did not preserve manual-owner suppression without debit.");
        Require(RenderCropPanelText(services, plot).Contains(
                "관개 · 직원 급수 진행 중",
                StringComparison.Ordinal),
            "Actual crop panel did not distinguish manual-owner suppression.");
        lines.Add("[PASS] positive-time real scheduler with manual refill owner + recovered tank1 -> auto debit0/crop credit0; presenter=manual active");

        Require(saves.TryRestore(dryWhole, out DungeonGameRestoreReport dryRestore)
                && dryRestore.Success,
            "Current whole-world dry checkpoint restore failed: "
            + string.Join(" | ", dryRestore.Errors));
        plot = ResolveLiveBuilding(services, plotInstanceId);
        tank = ResolveLiveBuilding(services, tankInstanceId);
        irrigator = ResolveLiveBuilding(services, irrigatorInstanceId);
        Require(runtime.Plots.Single(value => value.PlotId == plotId)
                    .WaterRefillPhase == CropWaterRefillPhase.None
                && water.TryGetNetwork(tank, out tankNetwork)
                && tankNetwork.CleanWater == 0f,
            "Whole restore did not clear the controlled manual owner and recover exact empty fluid state.");
        Require(fluid.TryAdd(tank, WorldWaterQuality.Clean, 1f, out float accepted)
                && accepted == 1f,
            "Finite preparation budget must be exactly one water.");
        request = new CropIrrigationRequest(plot, true, 0f, 2f, false);
        assessment = irrigation.Assess(request);
        Require(assessment.Status == CropIrrigationStatus.Available
                && assessment.IrrigatorId.Equals(irrigator.RequirePersistentInstanceId()),
            "Actual research/room/network/access route did not recover.");
        Require(RenderCropPanelText(services, plot).Contains(
                "관개 · 공급 가능",
                StringComparison.Ordinal),
            "Actual crop panel did not expose recovered irrigation availability.");

        DungeonGameSaveData availableWhole = saves.FromJson(
            saves.ToJson(saves.Capture()));
        Require(saves.TryRestore(availableWhole, out DungeonGameRestoreReport availableRestore)
                && availableRestore.Success,
            "Current whole-world finite-water restore failed: "
            + string.Join(" | ", availableRestore.Errors));
        plot = ResolveLiveBuilding(services, plotInstanceId);
        tank = ResolveLiveBuilding(services, tankInstanceId);
        irrigator = ResolveLiveBuilding(services, irrigatorInstanceId);
        row = runtime.Plots.Single(value => value.PlotId == plotId);
        Require(row.CurrentWater == 0f
                && row.WaterRefillPhase == CropWaterRefillPhase.None
                && water.TryGetNetwork(tank, out tankNetwork)
                && tankNetwork.CleanWater == 1f,
            "Finite-water whole restore filled the crop, duplicated water or created an owner.");
        lines.Add("[PASS] current whole-save low-water restore: crop0/owner none/tank1 exact; presenter unavailable->manual->available");

        string pausedBefore = JsonUtility.ToJson(runtime.Capture());
        for (int query = 0; query < 5; query++)
        {
            irrigation.Assess(request);
            _ = runtime.Plots;
            _ = runtime.Capture();
        }
        Require(JsonUtility.ToJson(runtime.Capture()) == pausedBefore
                && water.TryGetNetwork(tank, out tankNetwork) && tankNetwork.CleanWater == 1f,
            "Paused observation consumed finite water or changed crop ownership/state.");
        lines.Add("[PASS] real registered room/network/access; paused query/capture leaves crop and tank1 unchanged");

        float gameStart = clock.Time;
        float realStart = Time.realtimeSinceStartup;
        try
        {
            game.isPause = false;
            timeScale.Scale = 1f;
            bool supplied = false;
            while (Time.realtimeSinceStartup - realStart < 15f)
            {
                yield return null; // Observe the normal main-world scheduler once per frame.
                Require(water.TryGetNetwork(tank, out tankNetwork), "Live tank lost its network.");
                if (tankNetwork.CleanWater != 0f) continue;
                supplied = true;
                break;
            }
            game.isPause = true;
            timeScale.Scale = 0f;
            float elapsed = clock.Time - gameStart;
            row = runtime.Plots.Single(value => value.PlotId == plotId);
            Require(supplied && elapsed > 0f,
                "No real-clock supply; elapsed game seconds=" + elapsed);
            // Maximum authored dry-weather depletion bounds any observation lag.
            // The expected credit is independent (1), not computed by the supplier.
            float maximumDepletion = crop.DailyWater
                * plot.BuildingData.GetAbility<BuildingCropPlotAbility>().WaterMultiplier
                * elapsed / 180f;
            Require(row.CurrentWater > 0f && row.CurrentWater <= 1f
                    && row.CurrentWater >= 1f - maximumDepletion - 0.0001f,
                "Tank debit did not yield exactly one crop credit minus bounded live depletion.");
            Require(row.WaterRefillPhase == CropWaterRefillPhase.None,
                "Same supply also created a competing manual refill owner.");
            string suppliedSnapshot = JsonUtility.ToJson(runtime.Capture());
            for (int query = 0; query < 5; query++)
            {
                _ = runtime.Plots;
                _ = runtime.Capture();
                irrigation.Assess(new CropIrrigationRequest(plot, true,
                    row.CurrentWater, row.WaterCapacity, false));
            }
            Require(JsonUtility.ToJson(runtime.Capture()) == suppliedSnapshot
                    && water.TryGetNetwork(tank, out tankNetwork) && tankNetwork.CleanWater == 0f,
                "Paused post-supply observation replayed or regenerated water.");
            float terminalWater = row.CurrentWater;
            DungeonGameSaveData terminalWhole = saves.FromJson(
                saves.ToJson(saves.Capture()));
            Require(saves.TryRestore(terminalWhole, out DungeonGameRestoreReport terminalRestore)
                    && terminalRestore.Success,
                "Terminal irrigated whole-world restore failed: "
                + string.Join(" | ", terminalRestore.Errors));
            plot = ResolveLiveBuilding(services, plotInstanceId);
            tank = ResolveLiveBuilding(services, tankInstanceId);
            row = runtime.Plots.Single(value => value.PlotId == plotId);
            Require(Mathf.Approximately(row.CurrentWater, terminalWater)
                    && row.WaterRefillPhase == CropWaterRefillPhase.None
                    && water.TryGetNetwork(tank, out tankNetwork)
                    && tankNetwork.CleanWater == 0f,
                "Terminal restore replayed irrigation debit/credit or changed the owner.");
            lines.Add($"[PASS] actual clock {elapsed:0.######}s / wall {Time.realtimeSinceStartup - realStart:0.###}s: tank1->0, crop={row.CurrentWater:0.######}, no competing owner/replay");
            lines.Add("[PASS] terminal current whole-save restore preserves tank0/crop credit and does not replay supply");
            lines.Add("scope=controlled dry checkpoint + real main clock/crop scheduler/fluid network/current whole-save/presenter; not natural staff haul, full cycle or six-adult proof");
        }
        finally
        {
            game.isPause = true;
            timeScale.Scale = 0f;
        }
    }

    private static string RenderCropPanelText(
        IObjectResolver services,
        BuildableObject plot)
    {
        GameObject panel = new(
            "Wim016IrrigationPanelWitness",
            typeof(RectTransform),
            typeof(Canvas));
        try
        {
            services.Resolve<ICropPlotBuildingPanelPresenter>().Render(
                panel.transform,
                plot,
                TMPro.TMP_Settings.defaultFontAsset,
                _ => { },
                () => { });
            return string.Join(
                "\n",
                panel.GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .Select(value => value.text));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(panel);
        }
    }

    private static BuildableObject ResolveLiveBuilding(
        IObjectResolver services,
        string instanceId) => services.Resolve<IBuildingWorldQuery>()
        .Buildings
        .Single(value => value != null
            && string.Equals(
                value.PersistentInstanceId.Value,
                instanceId,
                StringComparison.Ordinal));

    public static bool RunWim016WaterRefillFocused(out string report)
    {
        var lines = new List<string> { "WIM016 actual crop refill service witness",
            "scope=authored detached facility + real input/WIP gateway, controlled dry checkpoint",
            "not-tested=natural depletion, staff AI/haul, irrigation, save-section staging, whole-world restore" };
        GameObject fixture = null;
        try
        {
            Require(Application.isPlaying && Time.timeScale == 0f,
                "Requires paused disposable protected main Play.");
            var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
            Require(scope?.Container != null, "Missing main runtime scope.");
            var runtime = scope.Container.Resolve<CropPlotRuntime>();
            var items = scope.Container.Resolve<IWorldItemStackRuntime>();
            var transfers = scope.Container.Resolve<IItemTransferService>();
            var catalog = scope.Container.Resolve<IResourceEconomyContentCatalog>();
            var research = scope.Container.Resolve<ProgressionSceneRuntimeReferences>().BlueprintResearch;
            foreach (string id in new[] { "research:agriculture:gathering", "research:agriculture:field" })
                Require(research.TryCompleteProjectImmediatelyForVerification(new ResearchProjectId(id), out string failure),
                    "Research fixture preparation failed: " + failure);
            Require(scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid), "Grid missing.");
            BuildingSO authored = LoadBuilding("P23");
            Require(authored != null && catalog.TryGetCrop("crop:twilight-grain", out _), "Authored plot/crop missing.");
            catalog.TryGetCrop("crop:twilight-grain", out CropDefinitionSO crop);
            fixture = new GameObject("Wim016_Refill_Service_Witness");
            var plot = fixture.AddComponent<Facility>();
            scope.Container.Inject(plot);
            plot.SetGrid(grid);
            plot.Initialization(authored, new Vector2Int(4, 0));
            runtime.Restore(runtime.BuildRestore(runtime.Capture()));
            Require(runtime.TrySetCrop(plot, crop.CropId, out string cropFailure), cropFailure);
            runtime.Tick();
            string plotId = plot.RequirePersistentInstanceId().Value;
            CropPlotSnapshot current = runtime.Plots.Single(p => p.PlotId == plotId);
            const string water = "resource:clean-water";
            Require(current.RequiredMaterials.TryGetValue(water, out int initialWater) && initialWater == 1,
                "Sow must consume one initial water, not full cycle prepayment.");
            foreach (var input in current.RequiredMaterials)
                Require(SpawnCropMaterial(items, transfers, crop, input.Key, input.Value, plot.centerPos,
                        current.MaterialDestinationId, out int spawned) && spawned == input.Value,
                    "Physical fixture input failed: " + input.Key);
            runtime.Tick();
            Require(runtime.TryGetWork(plot, BuiltInWorkTypeIds.Sow, out var sow) && sow.Available,
                "Sow work unavailable: " + sow.UnavailableReason);
            Require(runtime.ApplyWork(plot, BuiltInWorkTypeIds.Sow, sow.RequiredWork, out bool sowed) && sowed,
                "Sow did not commit.");
            current = runtime.Plots.Single(p => p.PlotId == plotId);
            Require(current.Phase == CropPlotPhase.Growing && Mathf.Approximately(current.CurrentWater, 1f),
                "Actual sow receipt did not initialize exactly one water.");
            lines.Add("[PASS] physical initial water1 -> sow -> growing/currentWater1");

            var dry = runtime.Capture();
            dry.plots.Single(p => p.buildingInstanceId == plotId).currentWater = 0f;
            runtime.Restore(runtime.BuildRestore(dry));
            runtime.Tick();
            current = runtime.Plots.Single(p => p.PlotId == plotId);
            Require(current.WaterGrowthMultiplier == 0f && current.WaterRefillPhase == CropWaterRefillPhase.WaitingForDelivery,
                "Dry checkpoint did not stop growth and request a refill.");
            string destination = current.WaterRefillDestinationId;
            int beforeSpawn = CountItem(items, water);
            Require(items.SpawnItemAt(water, 1, plot.centerPos, WorldItemStackState.FacilityBuffer,
                    destination, out int delivered) && delivered == 1, "Exact refill buffer admission failed.");
            runtime.Tick();
            current = runtime.Plots.Single(p => p.PlotId == plotId);
            Require(current.CurrentWater == 0f && CountItem(items, water) == beforeSpawn + 1,
                "Delivery alone consumed water or restored moisture.");
            Require(runtime.TryGetWork(plot, BuiltInWorkTypeIds.Treat, out var work)
                && work.Available && Mathf.Approximately(work.RequiredWork, 1f),
                "Authored P23 refill work must be1WU and available after delivery.");
            Require(runtime.ApplyWork(plot, BuiltInWorkTypeIds.Treat, 0.5f, out bool completed) && !completed,
                "Partial refill unexpectedly completed.");
            var partial = runtime.Capture();
            var partialRow = partial.plots.Single(p => p.buildingInstanceId == plotId);
            Require(partialRow.currentWater == 0f && partialRow.waterRefill.completedWork == 0.5f
                && CountItem(items, water) == beforeSpawn + 1, "Partial work changed physical water/moisture.");
            Require(!runtime.TrySetCrop(plot, crop.CropId, out _), "Active refill allowed crop replacement.");
            var restored = JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(partial));
            runtime.Restore(runtime.BuildRestore(restored));
            runtime.Tick();
            var resumed = runtime.Capture().plots.Single(p => p.buildingInstanceId == plotId);
            Require(resumed.waterRefill.destinationId == destination && resumed.waterRefill.completedWork == 0.5f
                && resumed.currentWater == 0f && CountItem(items, water) == beforeSpawn + 1,
                "Partial current-format restore reset progress, moisture or physical quantity.");
            lines.Add("[PASS] delivery/0.5WU no consumption or moisture; active owner retained; partial DTO restore exact");
            Require(runtime.ApplyWork(plot, BuiltInWorkTypeIds.Treat, 0.5f, out completed) && completed,
                "Resumed refill did not complete exact remaining work.");
            current = runtime.Plots.Single(p => p.PlotId == plotId);
            Require(Mathf.Approximately(current.CurrentWater, 1f)
                && current.WaterRefillPhase == CropWaterRefillPhase.None
                && CountItem(items, water) == beforeSpawn, "Refill WIP/ACK did not consume1 and publish1 once.");
            Require(!runtime.ApplyWork(plot, BuiltInWorkTypeIds.Treat, 1f, out _),
                "Completed refill accepted duplicate work without a new owner.");
            runtime.Restore(runtime.BuildRestore(runtime.Capture()));
            current = runtime.Plots.Single(p => p.PlotId == plotId);
            Require(Mathf.Approximately(current.CurrentWater, 1f) && CountItem(items, water) == beforeSpawn,
                "Terminal runtime DTO round-trip replayed water consumption/moisture.");
            lines.Add("[PASS] remaining0.5WU -> consume1/publish1/ACK; duplicate work rejected; terminal runtime DTO round-trip exact");
            lines.Add("PASS; controlled service/runtime DTO evidence only; operator MUST Stop disposable Play");
            report = string.Join("\n", lines);
            return true;
        }
        catch (Exception error)
        {
            lines.Add("FAIL: " + error);
            report = string.Join("\n", lines);
            return false;
        }
        finally
        {
            if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
        }
    }

    [MenuItem("Tools/DungeonStory/Economy/Request Crop Plot Runtime Verification")]
    public static void RequestRuntimeVerification()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, "requested");
        if (EditorApplication.isPlaying)
        {
            CropPlotDebugPlayModeRunner.StartPending();
            return;
        }
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/DungeonStory/Economy/Verify Crop Plot Runtime")]
    public static void VerifyRuntimeFromMenu()
    {
        List<string> lines = new List<string>
        {
            "# Crop Plot Runtime Verification",
            $"utc={DateTime.UtcNow:O}",
            $"playMode={Application.isPlaying}"
        };
        GameObject plotObject = null;
        GameObject correlationConflictPlotObject = null;
        GameObject indoorPlotObject = null;
        GameObject fungalShelfObject = null;
        Facility detachedRoundTripPlot = null;
        Facility detachedRoundTripConflictPlot = null;
        try
        {
            Require(
                CropPhysicalTransactionFixture.Run(),
                "crop physical transaction fixture failed");
            Require(Application.isPlaying, "Play Mode is required.");
            DungeonRuntimeLifetimeScope scope =
                UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
            Require(scope != null && scope.Container != null, "Runtime scope is missing.");

            CropPlotRuntime runtime = scope.Container.Resolve<CropPlotRuntime>();
            IWorldItemStackRuntime items =
                scope.Container.Resolve<IWorldItemStackRuntime>();
            IItemTransferService transfers =
                scope.Container.Resolve<IItemTransferService>();
            IResourceEconomyContentCatalog catalog =
                scope.Container.Resolve<IResourceEconomyContentCatalog>();
            BlueprintResearchRuntime research = scope.Container
                .Resolve<ProgressionSceneRuntimeReferences>()
                .BlueprintResearch;
            IGridSystemProvider gridProvider =
                scope.Container.Resolve<IGridSystemProvider>();
            Require(research != null, "Research runtime is missing.");
            research.State.Projects.Complete(
                new ResearchProjectId("research:agriculture:field"));
            research.State.Projects.Complete(
                new ResearchProjectId("research:agriculture:gathering"));
            research.State.Projects.Complete(
                new ResearchProjectId("research:agriculture:indoor"));
            research.State.Projects.Complete(
                new ResearchProjectId("research:forestry:fungal"));
            Require(gridProvider.TryGetGrid(out Grid grid), "Grid is missing.");

            BuildingSO outdoorPlot = LoadBuilding("P23");
            BuildingSO indoorPlot = LoadBuilding("P24");
            BuildingSO fungalShelf = LoadBuilding("RF13");
            Require(outdoorPlot != null, "P23 outdoor crop plot is missing.");
            Require(indoorPlot != null, "P24 indoor grow bed is missing.");
            Require(fungalShelf != null, "RF13 fungal shelf is missing.");
            Require(
                outdoorPlot.GetAbility<BuildingCropPlotAbility>() is { Indoor: false },
                "P23 crop plot ability is invalid.");
            Require(
                indoorPlot.GetAbility<BuildingCropPlotAbility>() is
                {
                    Indoor: true,
                    CompostPerCycle: 1,
                    FuelPerCycle: 1
                },
                "P24 crop plot ability is invalid.");
            BuildingCropPlotAbility fungalAbility =
                fungalShelf.GetAbility<BuildingCropPlotAbility>();
            Require(
                fungalAbility != null
                && fungalAbility.Indoor
                && fungalAbility.CompostPerCycle == 1
                && fungalAbility.CycleSupplyInputs.Count == 1
                && fungalAbility.CycleSupplyInputs[0].ItemId
                    == "supply:inoculated-log"
                && fungalAbility.CycleSupplyInputs[0].Amount == 1,
                "RF13 must consume one inoculated-log section per cycle.");
            Require(
                outdoorPlot.Facility.SupportsWork(BuiltInWorkTypeIds.Sow)
                && outdoorPlot.Facility.SupportsWork(BuiltInWorkTypeIds.Harvest),
                "P23 does not expose sow and harvest work.");
            Require(
                catalog.TryGetCrop(
                    "crop:twilight-grain",
                    out CropDefinitionSO crop),
                "twilight grain definition is missing.");
            const string WaterItemId = "resource:clean-water";
            lines.Add(VerifyTemperatureGate(
                crop,
                outdoorPlot.GetAbility<BuildingCropPlotAbility>(),
                indoorPlot.GetAbility<BuildingCropPlotAbility>()));

            plotObject = new GameObject("CropPlot_Runtime_Verifier");
            Facility plot = plotObject.AddComponent<Facility>();
            scope.Container.Inject(plot);
            plot.SetGrid(grid);
            plot.Initialization(outdoorPlot, new Vector2Int(4, 0));
            correlationConflictPlotObject = new GameObject(
                "CropPlot_CorrelationConflict_Verifier");
            Facility correlationConflictPlot =
                correlationConflictPlotObject.AddComponent<Facility>();
            scope.Container.Inject(correlationConflictPlot);
            correlationConflictPlot.SetGrid(grid);
            correlationConflictPlot.Initialization(
                outdoorPlot,
                new Vector2Int(6, 0));
            runtime.Restore(runtime.BuildRestore(runtime.Capture()));
            Require(
                runtime.TrySetCrop(
                    plot,
                    "crop:twilight-grain",
                    out string cropMessage),
                cropMessage);
            Require(
                runtime.TrySetCrop(
                    correlationConflictPlot,
                    "crop:twilight-grain",
                    out string correlationCropMessage),
                correlationCropMessage);
            const string CropExecutionActionId =
                "qa:crop-plan-execution:outdoor-primary";
            Require(
                runtime.TryBindNextCycle(
                    CropExecutionActionId,
                    plot.RequirePersistentInstanceId().Value,
                    crop.CropId,
                    out string bindFailure),
                "Could not bind Crop execution action: " + bindFailure);
            Require(
                !runtime.TryBindNextCycle(
                    CropExecutionActionId,
                    correlationConflictPlot.RequirePersistentInstanceId().Value,
                    crop.CropId,
                    out string duplicateBindFailure)
                && string.Equals(
                    duplicateBindFailure,
                    "crop-cycle-correlation-global-conflict",
                    StringComparison.Ordinal),
                "Duplicate Crop action correlation was not rejected globally: "
                + duplicateBindFailure);
            runtime.Tick();

            CropPlotSnapshot waiting = runtime.Plots.Single(entry =>
                entry.PlotId == plot.RequirePersistentInstanceId().Value);
            Require(
                waiting.Phase == CropPlotPhase.WaitingForMaterials,
                $"unexpected initial phase={waiting.Phase}");
            Require(
                waiting.RequiredMaterials.TryGetValue(
                    WaterItemId,
                    out int outdoorWaterRequired)
                && waiting.CycleWaterSupplyStatus
                    == CropCycleWaterSupplyStatus.AwaitingCycleSupply
                && waiting.CycleWaterQuantity == outdoorWaterRequired,
                "outdoor crop plot requested no physical water.");
            foreach (KeyValuePair<string, int> material in waiting.RequiredMaterials)
            {
                Require(
                    SpawnCropMaterial(
                        items,
                        transfers,
                        crop,
                        material.Key,
                        material.Value,
                        plot.centerPos,
                        waiting.MaterialDestinationId,
                        out int spawned)
                    && spawned == material.Value,
                    $"could not deliver {material.Key}");
            }

            runtime.Tick();
            Require(
                runtime.TryGetWork(
                    plot,
                    BuiltInWorkTypeIds.Sow,
                    out CropPlotWorkSnapshot sow)
                && sow.Available,
                $"sow work unavailable: {sow.UnavailableReason}");
            Require(
                runtime.ApplyWork(
                    plot,
                    BuiltInWorkTypeIds.Sow,
                    sow.RequiredWork,
                    out bool sowed)
                && sowed,
                "sowing did not complete.");

            DungeonCropPlotSaveData growingSave = runtime.Capture();
            CropPlotSaveData growing = growingSave.plots.Single(entry =>
                entry.buildingInstanceId == waiting.PlotId);
            Require(
                growing.phase == CropPlotPhase.Growing,
                $"crop did not enter growing phase: {growing.phase}");
            CropPlotSnapshot outdoorGrowing = runtime.Plots.Single(entry =>
                entry.PlotId == waiting.PlotId);
            Require(
                growing.cycleExecutionReceipt.status
                    == CropCycleExecutionReceiptStatus.Active
                && growing.cycleExecutionReceipt.explicitCorrelation
                && string.Equals(
                    growing.cycleExecutionReceipt.correlationId,
                    CropExecutionActionId,
                    StringComparison.Ordinal)
                && !runtime.TryCaptureExecutionReceipt(
                    CropExecutionActionId,
                    out _),
                "Active Crop execution receipt was not durable or became observable before terminal completion.");
            Require(
                outdoorGrowing.CycleWaterSupplyStatus
                    == CropCycleWaterSupplyStatus.SuppliedForCurrentCycle
                && outdoorGrowing.CycleWaterQuantity == outdoorWaterRequired,
                "Outdoor cycle water supply was not projected from its active sow receipt.");
            int outdoorWaterAfterSow = CountItem(items, WaterItemId);
            runtime.Restore(runtime.BuildRestore(growingSave));
            CropPlotSnapshot restoredOutdoorGrowing = runtime.Plots.Single(entry =>
                entry.PlotId == waiting.PlotId);
            Require(
                restoredOutdoorGrowing.CycleWaterSupplyStatus
                    == CropCycleWaterSupplyStatus.SuppliedForCurrentCycle
                && restoredOutdoorGrowing.CycleWaterQuantity
                    == outdoorWaterRequired,
                "Outdoor cycle water supply drifted across crop-plot restore.");
            runtime.Tick();
            Require(
                CountItem(items, WaterItemId) == outdoorWaterAfterSow,
                "Outdoor growth tick consumed the sow-paid cycle water again.");
            growing.growthHours = crop.GrowthHours;
            runtime.Restore(runtime.BuildRestore(growingSave));
            runtime.Tick();

            Require(
                runtime.TryGetWork(
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    out CropPlotWorkSnapshot harvest)
                && harvest.Available,
                $"harvest work unavailable: {harvest.UnavailableReason}");
            lines.Add(VerifyHarvestOutputFacilityBufferWaitRestoreRetry(
                runtime,
                plot,
                items,
                scope.Container,
                catalog,
                crop,
                harvest,
                CropExecutionActionId,
                out int stockBefore,
                out int stockAfter));

            CropPlotSaveSection saveSection =
                scope.Container.Resolve<CropPlotSaveSection>();
            string sectionPayload = saveSection.Capture();
            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            IRestoreWorldCandidateQuery candidateQuery =
                scope.Container.Resolve<IRestoreWorldCandidateQuery>();
            IRestoreWorldCandidatePublisher candidatePublisher =
                scope.Container.Resolve<IRestoreWorldCandidatePublisher>();
            Require(
                !candidateQuery.TryGetBuildings(out _),
                "Crop-plot focused restore started with an occupied detached-world slot.");
            GameObject detachedRoundTripObject =
                new GameObject("CropPlot_Detached_RoundTrip_Verifier");
            detachedRoundTripPlot =
                detachedRoundTripObject.AddComponent<Facility>();
            detachedRoundTripPlot.PrepareForDetachedRestore();
            scope.Container.Inject(detachedRoundTripPlot);
            detachedRoundTripPlot.RestorePersistentIdentity(
                plot.RequirePersistentInstanceId());
            detachedRoundTripPlot.SetGrid(grid);
            detachedRoundTripPlot.Initialization(
                outdoorPlot,
                plot.centerPos);
            GameObject detachedConflictObject =
                new GameObject("CropPlot_Detached_Conflict_RoundTrip_Verifier");
            detachedRoundTripConflictPlot =
                detachedConflictObject.AddComponent<Facility>();
            detachedRoundTripConflictPlot.PrepareForDetachedRestore();
            scope.Container.Inject(detachedRoundTripConflictPlot);
            detachedRoundTripConflictPlot.RestorePersistentIdentity(
                correlationConflictPlot.RequirePersistentInstanceId());
            detachedRoundTripConflictPlot.SetGrid(grid);
            detachedRoundTripConflictPlot.Initialization(
                outdoorPlot,
                correlationConflictPlot.centerPos);
            bool candidatePublished = false;
            try
            {
                candidatePublisher.SetFacilityCandidate(
                    grid,
                    new BuildableObject[]
                    {
                        detachedRoundTripPlot,
                        detachedRoundTripConflictPlot
                    });
                candidatePublished = true;
                saveSection.Restore(
                    sectionPayload,
                    saveSection.SectionVersion,
                    report);
            }
            finally
            {
                if (candidatePublished)
                    candidatePublisher.ClearFacilityCandidate();
                if (detachedRoundTripPlot != null)
                {
                    detachedRoundTripPlot.DiscardDetachedRestore();
                    detachedRoundTripPlot = null;
                }
                if (detachedRoundTripConflictPlot != null)
                {
                    detachedRoundTripConflictPlot.DiscardDetachedRestore();
                    detachedRoundTripConflictPlot = null;
                }
            }
            Require(
                report.Success,
                string.Join(" / ", report.Errors));
            VerifyStrictSaveIsolation(saveSection);

            indoorPlotObject = new GameObject("IndoorCropPlot_Runtime_Verifier");
            Facility indoor = indoorPlotObject.AddComponent<Facility>();
            scope.Container.Inject(indoor);
            indoor.SetGrid(grid);
            indoor.Initialization(indoorPlot, new Vector2Int(8, 0));
            runtime.Restore(runtime.BuildRestore(runtime.Capture()));
            Require(
                runtime.TrySetCrop(
                    indoor,
                    "crop:cave-mushroom",
                    out string indoorCropMessage),
                indoorCropMessage);
            runtime.Tick();

            CropPlotSnapshot indoorWaiting = runtime.Plots.Single(entry =>
                entry.PlotId == indoor.RequirePersistentInstanceId().Value);
            string fuelItemId = indoorWaiting.RequiredMaterials.Keys
                .SingleOrDefault(itemId => catalog.TryGetItem(
                        itemId,
                        out ResourceItemDefinitionSO definition)
                    && (definition.IngredientTags & ResourceIngredientTag.Fuel) != 0);
            Require(
                indoorWaiting.RequiredMaterials.ContainsKey(WaterItemId)
                && indoorWaiting.RequiredMaterials.ContainsKey("material:compost")
                && !string.IsNullOrWhiteSpace(fuelItemId)
                && indoorWaiting.RequiredMaterials.ContainsKey(fuelItemId),
                "Indoor crop cycle must require water, compost, and fuel.");
            Require(
                catalog.TryGetCrop(
                    "crop:cave-mushroom",
                    out CropDefinitionSO indoorCrop),
                "cave mushroom definition is missing.");

            int waterRequired = indoorWaiting.RequiredMaterials[WaterItemId];
            Require(
                indoorWaiting.CycleWaterSupplyStatus
                    == CropCycleWaterSupplyStatus.AwaitingCycleSupply
                && indoorWaiting.CycleWaterQuantity == waterRequired,
                "Indoor pending cycle water quantity was not projected.");
            Require(
                items.SpawnItemAt(
                    WaterItemId,
                    waterRequired,
                    indoor.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    indoorWaiting.MaterialDestinationId,
                    out int indoorWaterSpawned)
                && indoorWaterSpawned == waterRequired,
                "Indoor water delivery failed.");
            runtime.Tick();
            Require(
                runtime.TryGetWork(
                    indoor,
                    BuiltInWorkTypeIds.Sow,
                    out CropPlotWorkSnapshot blockedIndoorSow)
                && !blockedIndoorSow.Available,
                "Indoor sowing opened before compost and fuel arrived.");

            foreach (KeyValuePair<string, int> material in
                     indoorWaiting.RequiredMaterials.Where(entry =>
                         !string.Equals(
                             entry.Key,
                             WaterItemId,
                             StringComparison.Ordinal)))
            {
                Require(
                    SpawnCropMaterial(
                        items,
                        transfers,
                        indoorCrop,
                        material.Key,
                        material.Value,
                        indoor.centerPos,
                        indoorWaiting.MaterialDestinationId,
                        out int indoorMaterialSpawned)
                    && indoorMaterialSpawned == material.Value,
                    $"Indoor material delivery failed: {material.Key}");
            }

            runtime.Tick();
            Require(
                runtime.TryGetWork(
                    indoor,
                    BuiltInWorkTypeIds.Sow,
                    out CropPlotWorkSnapshot indoorSow)
                && indoorSow.Available,
                $"Indoor sowing remained blocked: {indoorSow.UnavailableReason}");
            Require(
                runtime.ApplyWork(
                    indoor,
                    BuiltInWorkTypeIds.Sow,
                    indoorSow.RequiredWork,
                    out bool indoorSowed)
                && indoorSowed,
                "Indoor sowing did not complete.");
            CropPlotSnapshot indoorGrowing = runtime.Plots.Single(entry =>
                entry.PlotId == indoorWaiting.PlotId);
            Require(
                indoorGrowing.Phase == CropPlotPhase.Growing,
                $"Indoor crop did not start growing: {indoorGrowing.Phase}");
            Require(
                indoorGrowing.CycleWaterSupplyStatus
                    == CropCycleWaterSupplyStatus.SuppliedForCurrentCycle
                && indoorGrowing.CycleWaterQuantity == waterRequired,
                "Indoor cycle water supply was not projected from its active sow receipt.");
            int indoorWaterAfterSow = CountItem(items, WaterItemId);
            runtime.Tick();
            Require(
                CountItem(items, WaterItemId) == indoorWaterAfterSow,
                "Indoor growth tick consumed the sow-paid cycle water again.");

            Require(
                catalog.TryGetItem(
                    "supply:inoculated-log",
                    out ResourceItemDefinitionSO inoculatedLog)
                && Mathf.RoundToInt(inoculatedLog.UnitWeight * 1000f) == 700,
                "Inoculated-log authority must be exactly 700 g.");
            fungalShelfObject = new GameObject("FungalShelf_Runtime_Verifier");
            Facility fungal = fungalShelfObject.AddComponent<Facility>();
            scope.Container.Inject(fungal);
            fungal.SetGrid(grid);
            fungal.Initialization(fungalShelf, new Vector2Int(12, 0));
            runtime.Restore(runtime.BuildRestore(runtime.Capture()));
            Require(
                runtime.TrySetCrop(
                    fungal,
                    "crop:cave-mushroom",
                    out string fungalCropMessage),
                fungalCropMessage);
            runtime.Tick();

            CropPlotSnapshot fungalWaiting = runtime.Plots.Single(entry =>
                entry.PlotId == fungal.RequirePersistentInstanceId().Value);
            Require(
                fungalWaiting.RequiredMaterials.TryGetValue(
                    "supply:inoculated-log",
                    out int requiredLogs)
                && requiredLogs == 1,
                "RF13 did not request exactly one inoculated-log section.");
            int inoculatedBefore = CountItem(items, "supply:inoculated-log");
            foreach (KeyValuePair<string, int> material in
                     fungalWaiting.RequiredMaterials)
            {
                Require(
                    SpawnCropMaterial(
                        items,
                        transfers,
                        indoorCrop,
                        material.Key,
                        material.Value,
                        fungal.centerPos,
                        fungalWaiting.MaterialDestinationId,
                        out int fungalMaterialSpawned)
                    && fungalMaterialSpawned == material.Value,
                    $"RF13 material delivery failed: {material.Key}");
            }

            runtime.Tick();
            Require(
                runtime.TryGetWork(
                    fungal,
                    BuiltInWorkTypeIds.Sow,
                    out CropPlotWorkSnapshot fungalSow)
                && fungalSow.Available,
                $"RF13 sowing remained blocked: {fungalSow.UnavailableReason}");
            Require(
                runtime.ApplyWork(
                    fungal,
                    BuiltInWorkTypeIds.Sow,
                    fungalSow.RequiredWork,
                    out bool fungalSowed)
                && fungalSowed,
                "RF13 sowing did not complete.");
            CropPlotSnapshot fungalGrowing = runtime.Plots.Single(entry =>
                entry.PlotId == fungalWaiting.PlotId);
            Require(
                fungalGrowing.Phase == CropPlotPhase.Growing,
                $"RF13 crop did not start growing: {fungalGrowing.Phase}");
            Require(
                CountItem(items, "supply:inoculated-log")
                    == inoculatedBefore,
                "RF13 did not consume exactly the one spawned inoculated-log section.");

            lines.Add($"plot={waiting.PlotId}");
            lines.Add($"crop={crop.CropId}");
            lines.Add("materials=" + string.Join(
                ",",
                waiting.RequiredMaterials.Select(entry =>
                    $"{entry.Key}x{entry.Value}")));
            lines.Add($"harvest={stockBefore}->{stockAfter}");
            lines.Add(
                $"outdoorCycleWater=supplied:{outdoorWaterRequired};tickDelta=0");
            lines.Add($"indoorPlot={indoorWaiting.PlotId}");
            lines.Add("indoorMaterials=" + string.Join(
                ",",
                indoorWaiting.RequiredMaterials.Select(entry =>
                    $"{entry.Key}x{entry.Value}")));
            lines.Add($"indoorPhase={indoorGrowing.Phase}");
            lines.Add(
                $"indoorCycleWater=supplied:{waterRequired};tickDelta=0");
            lines.Add($"fungalPlot={fungalWaiting.PlotId}");
            lines.Add("fungalMaterials=" + string.Join(
                ",",
                fungalWaiting.RequiredMaterials.Select(entry =>
                    $"{entry.Key}x{entry.Value}")));
            lines.Add($"inoculatedLog=700g;consumed={requiredLogs}");
            lines.Add($"fungalPhase={fungalGrowing.Phase}");
            lines.Add("valid=true");
            WriteReport(lines);
            Debug.Log(string.Join(Environment.NewLine, lines));
        }
        catch (Exception exception)
        {
            lines.Add("valid=false");
            lines.Add("error=" + exception);
            WriteReport(lines);
            Debug.LogException(exception);
        }
        finally
        {
            if (plotObject != null)
            {
                UnityEngine.Object.Destroy(plotObject);
            }

            if (correlationConflictPlotObject != null)
            {
                UnityEngine.Object.Destroy(correlationConflictPlotObject);
            }

            if (indoorPlotObject != null)
            {
                UnityEngine.Object.Destroy(indoorPlotObject);
            }

            if (fungalShelfObject != null)
            {
                UnityEngine.Object.Destroy(fungalShelfObject);
            }

            if (detachedRoundTripPlot != null)
            {
                detachedRoundTripPlot.DiscardDetachedRestore();
            }

            if (detachedRoundTripConflictPlot != null)
            {
                detachedRoundTripConflictPlot.DiscardDetachedRestore();
            }
        }
    }

    private static BuildingSO LoadBuilding(string code)
    {
        return AssetDatabase
            .FindAssets(
                "t:BuildingSO",
                new[]
                {
                    "Assets/Resources/SO/Building/Modular",
                    "Assets/Resources/SO/Building/ResearchOverhaul"
                })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(building =>
                building?.GetAbility<BuildingFacilityPartAbility>()?.code
                == code);
    }

    private static string VerifyTemperatureGate(
        CropDefinitionSO crop,
        BuildingCropPlotAbility outdoorAbility,
        BuildingCropPlotAbility indoorAbility)
    {
        CropGenomePhenotype phenotype = new(
            coldToleranceDegrees: 2f,
            heatToleranceDegrees: 3f,
            growthMultiplier: 1.16f,
            yieldMultiplier: 1f,
            diseaseRiskMultiplier: 1f,
            seedYieldBonus: 0);
        Vector2 authored = crop.TemperatureRange;
        float minimum = authored.x - phenotype.ColdToleranceDegrees;
        float maximum = authored.y + phenotype.HeatToleranceDegrees;
        CropGrowthTemperatureEvaluation waiting =
            CropGrowthCycleAuthority.EvaluateTemperature(
                crop,
                phenotype,
                hasEnvironmentObservation: false,
                observedTemperatureC: 0f);
        CropGrowthTemperatureEvaluation cold =
            CropGrowthCycleAuthority.EvaluateTemperature(
                crop,
                phenotype,
                hasEnvironmentObservation: true,
                observedTemperatureC: minimum - 0.1f);
        CropGrowthTemperatureEvaluation suitable =
            CropGrowthCycleAuthority.EvaluateTemperature(
                crop,
                phenotype,
                hasEnvironmentObservation: true,
                observedTemperatureC: minimum);
        CropGrowthTemperatureEvaluation hot =
            CropGrowthCycleAuthority.EvaluateTemperature(
                crop,
                phenotype,
                hasEnvironmentObservation: true,
                observedTemperatureC: maximum + 0.1f);
        Require(
            waiting.Status
                == CropGrowthTemperatureStatus.WaitingForEnvironmentObservation
            && cold.Status == CropGrowthTemperatureStatus.TooCold
            && suitable.Status == CropGrowthTemperatureStatus.Suitable
            && hot.Status == CropGrowthTemperatureStatus.TooHot
            && suitable.MinimumTemperatureC == minimum
            && suitable.MaximumTemperatureC == maximum,
            "Actual crop-cell temperature gate or genome tolerance drifted.");

        SurvivalEnvironmentSnapshot rain = new(
            SurvivalWeatherType.Rain,
            outdoorTemperature: -999f,
            exteriorNightDanger: 0f,
            sanitationRisk: 0f,
            diseaseRisk: 0f);
        float outdoor = CropGrowthCycleAuthority.ResolveOutdoorRuntimeMultiplier(
            outdoorAbility,
            phenotype,
            rain,
            cropCalendarOperational: true);
        float outdoorExpected = outdoorAbility.GrowthMultiplier
            * CropGrowthCycleAuthority.OutdoorRainMultiplier
            * CropGrowthCycleAuthority.CropCalendarMultiplier
            * phenotype.GrowthMultiplier;
        float indoor = CropGrowthCycleAuthority.ResolveIndoorRuntimeMultiplier(
            indoorAbility,
            climateControlOperational: true,
            cropCalendarOperational: true,
            phenotype);
        float indoorExpected = indoorAbility.GrowthMultiplier
            * CropGrowthCycleAuthority.ClimateControlMultiplier
            * CropGrowthCycleAuthority.CropCalendarMultiplier
            * phenotype.GrowthMultiplier;
        Require(
            Mathf.Approximately(outdoor, outdoorExpected)
            && Mathf.Approximately(indoor, indoorExpected),
            "Temperature eligibility duplicated existing indoor or outdoor growth multipliers.");
        return "PASS CROP_ACTUAL_CELL_TEMPERATURE_GATE "
            + $"range={minimum:0.#}..{maximum:0.#};"
            + "unobserved=waiting;outdoorFactorsOnce=true;indoorFactorsOnce=true";
    }

    private static int CountItem(
        IWorldItemStackRuntime items,
        string itemId)
    {
        return items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    itemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
    }

    private static bool SpawnCropMaterial(
        IWorldItemStackRuntime items,
        IItemTransferService transfers,
        CropDefinitionSO crop,
        string itemId,
        int amount,
        Vector2Int position,
        string destinationId,
        out int spawned)
    {
        if (!string.Equals(itemId, crop.SeedItemId, StringComparison.Ordinal))
        {
            return items.SpawnItemAt(
                itemId,
                amount,
                position,
                WorldItemStackState.FacilityBuffer,
                destinationId,
                out spawned);
        }

        spawned = 0;
        return crop.BaseGenome != null
            && transfers.TrySpawnItemWithComponents(
                itemId,
                amount,
                position,
                WorldItemStackState.FacilityBuffer,
                destinationId,
                new[]
                {
                    SeedLotItemStateCodec.Encode(new SeedLotState
                    {
                        cropId = crop.CropId,
                        cultivarGenomeId = crop.BaseGenome.GenomeId,
                        generation = 0,
                        pathogenLoad = 0f
                    })
                },
                out spawned);
    }

    private static string VerifyHarvestOutputFacilityBufferWaitRestoreRetry(
        CropPlotRuntime runtime,
        Facility plot,
        IWorldItemStackRuntime items,
        IObjectResolver services,
        IResourceEconomyContentCatalog catalog,
        CropDefinitionSO crop,
        CropPlotWorkSnapshot harvest,
        string executionActionId,
        out int stockBefore,
        out int stockAfter)
    {
        Require(services != null, "Runtime service resolver is missing.");
        IFacilityBufferMassAdmissionService admission =
            services.Resolve<IFacilityBufferMassAdmissionService>();
        IFacilityBufferPlannedOutputPublicationService publication =
            services.Resolve<IFacilityBufferPlannedOutputPublicationService>();
        Require(admission != null, "Facility-buffer admission service is missing.");
        Require(publication != null,
            "Facility-buffer publication service is missing.");
        BuildingInstanceId plotId = plot.RequirePersistentInstanceId();
        string destinationId = ProductionOutputDestinationId
            .FromFacility(plotId)
            .Value;
        Require(catalog.TryGetItem(
                crop.HarvestItemId,
                out ResourceItemDefinitionSO harvestDefinition),
            "Crop harvest item definition is missing.");
        long harvestUnitMassGrams = Mathf.RoundToInt(
            harvestDefinition.UnitWeight * 1000f);
        Require(harvestUnitMassGrams > 0L,
            "Crop harvest item has no positive physical mass.");

        IProductionFacilityHandleQuery facilityHandles =
            services.Resolve<IProductionFacilityHandleQuery>();
        IProductionOutputCapabilityRegistry capabilities =
            services.Resolve<IProductionOutputCapabilityRegistry>();
        IProductionOutputMaximumMassRegistry maximumMass =
            services.Resolve<IProductionOutputMaximumMassRegistry>();
        IProductionOutputBufferCapacityProjector capacityProjector =
            services.Resolve<IProductionOutputBufferCapacityProjector>();
        IProductionOutputDestinationAuthorityRuntime destinations =
            services.Resolve<IProductionOutputDestinationAuthorityRuntime>();
        ICharacterPerformanceDefinitionMaximumQuery performanceMaximum =
            services.Resolve<ICharacterPerformanceDefinitionMaximumQuery>();
        IGameplayEffectResultBoundsQuery effectBounds =
            services.Resolve<IGameplayEffectResultBoundsQuery>();
        ProductionFacilityHandle facility = facilityHandles.CaptureFacility(plot);
        ProductionOutputCapabilityDescriptor harvestCapability =
            capabilities.CaptureDeclaredDescriptor(
                CropHarvestOutputMaximumAuthority.HarvestOutputLineId(crop.CropId),
                crop.HarvestItemId,
                ProductionOutputCapabilityIds.StandardDefinition);
        ProductionOutputCapabilityDescriptor seedCapability =
            capabilities.CaptureDeclaredDescriptor(
                CropHarvestOutputMaximumAuthority.SeedOutputLineId(crop.CropId),
                crop.SeedItemId,
                ProductionOutputCapabilityIds.CropHarvestSeedLot);
        ProductionOutputBatchMaximumMassProof maximumProof = new(new[]
        {
            maximumMass.CaptureDeclared(
                harvestCapability,
                CropHarvestOutputMaximumAuthority.ResolveMaximumHarvestQuantity(
                    crop,
                    indoor: false,
                    performanceMaximum)),
            maximumMass.CaptureDeclared(
                seedCapability,
                CropHarvestOutputMaximumAuthority
                    .ResolveMaximumReturnedSeedQuantity(effectBounds))
        });
        ProductionOutputBufferCapacitySourceSnapshot capacitySource =
            capacityProjector.CaptureSource(facility, maximumProof);
        Require(destinations.TryEnsure(
                facility,
                capacitySource.RequiredMinimumCapacityGrams,
                out FacilityBufferCapacityProfile authoredProfile,
                out string capacityFailure),
            "Could not publish crop output capacity authority: " + capacityFailure);
        Require(admission.TryGetCapacity(
                destinationId,
                plot.centerPos,
                out FacilityBufferMassCapacitySnapshot initialCapacity),
            "Crop output FacilityBuffer capacity authority is missing.");
        Require(initialCapacity.Profile.MaxMassGrams == authoredProfile.MaxMassGrams
            && initialCapacity.Profile.MaxMassGrams
                >= capacitySource.RequiredMinimumCapacityGrams,
            "Crop output FacilityBuffer capacity drifted from its maximum proof.");
        Require(initialCapacity.ReservedMassGrams == 0L,
            "Crop output FacilityBuffer already has reserved mass before the fixture.");
        Require(!items.GetAllStacks().Any(stack => stack != null
                && stack.State is WorldItemStackState.FacilityBuffer
                    or WorldItemStackState.FacilityOutputBuffer
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)),
            "Crop output FacilityBuffer already has physical occupancy before the fixture.");

        long fillerQuantityLong = initialCapacity.Profile.MaxMassGrams
            / harvestUnitMassGrams;
        Require(fillerQuantityLong is > 0L and <= int.MaxValue,
            "Crop output capacity cannot be saturated by the harvest item fixture.");
        int fillerQuantity = checked((int)fillerQuantityLong);
        FacilityBufferPlannedOutputRequest capacityFixture = new(
            "qa:crop-output-capacity-wait:" + plotId.Value,
            "production-output-batch:qa:crop-output-capacity-wait:"
                + plotId.Value,
            new string('a', 64),
            destinationId,
            plot.centerPos,
            initialCapacity.Profile.OwnerDomain,
            initialCapacity.Profile.OwnerOperationId,
            initialCapacity.Profile.OwnerFacilityId,
            initialCapacity.Profile.CapacityRevision,
            new[]
            {
                new FacilityBufferPlannedOutputSlice(
                    "qa:crop-output-capacity-fill:" + crop.CropId,
                    PhysicalItemMassSubject.ForDefinition(
                        (ItemDefinitionId)crop.HarvestItemId),
                    fillerQuantity)
            },
            capacitySource.SourceDigest,
            capacitySource.RequiredMinimumCapacityGrams);
        FacilityBufferPlannedOutputToken capacityToken = default;
        bool capacityReserved = false;
        stockBefore = CountItem(items, crop.HarvestItemId);
        stockAfter = stockBefore;
        int seedStockBefore = CountItem(items, crop.SeedItemId);
        try
        {
            Require(
                admission.TryReservePlannedOutput(
                    capacityFixture,
                    out capacityToken,
                    out FacilityBufferMassAdmissionFailureCode reserveFailure,
                    out string reserveReason),
                "Could not reserve the competing crop-output capacity fixture: "
                    + reserveFailure + ":" + reserveReason);
            capacityReserved = true;
            Require(admission.TryGetCapacity(
                    destinationId,
                    plot.centerPos,
                    out FacilityBufferMassCapacitySnapshot saturated)
                && saturated.ReservedMassGrams
                    + harvestUnitMassGrams > saturated.Profile.MaxMassGrams,
                "Competing reservation did not leave less than one harvest item of capacity.");

            Require(runtime.ApplyWork(
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    harvest.RequiredWork,
                    out bool completedWhileFull)
                && !completedWhileFull,
                "Capacity-blocked crop harvest did not retain its completed work for retry.");
            DungeonCropPlotSaveData waitingCapture = runtime.Capture();
            CropPlotSaveData waiting = waitingCapture.plots.Single(entry =>
                entry.buildingInstanceId == plotId.Value);
            Require(waiting.pendingHarvest.phase == CropHarvestOutputPhase.Frozen
                && waiting.pendingHarvest.outputPublication.IsEmpty
                && waiting.pendingHarvest.harvestQuantity > 0
                && waiting.pendingHarvest.seedQuantity > 0
                && Mathf.Approximately(waiting.harvestWork, harvest.RequiredWork),
                "Capacity wait did not preserve one frozen crop-output vector "
                + "before admission ownership was available.");
            Require(CountItem(items, crop.HarvestItemId) == stockBefore
                && CountItem(items, crop.SeedItemId) == seedStockBefore,
                "Capacity wait published a partial crop-output vector.");

            string frozenOperationId = waiting.pendingHarvest.operationId;
            string frozenOutcomeFingerprint =
                waiting.pendingHarvest.ecologyOutcomeFingerprint;
            int expectedHarvestQuantity = waiting.pendingHarvest.harvestQuantity;
            int expectedSeedQuantity = waiting.pendingHarvest.seedQuantity;
            string expectedSeedCanonical = SeedLotItemStateCodec
                .Encode(waiting.pendingHarvest.returnedSeedLot)
                .ToCanonicalString();

            runtime.Restore(runtime.BuildRestore(waitingCapture));
            runtime.Tick();
            CropPlotSaveData restoredWaiting = runtime.Capture().plots.Single(entry =>
                entry.buildingInstanceId == plotId.Value);
            Require(restoredWaiting.pendingHarvest.phase
                    == CropHarvestOutputPhase.Frozen
                && string.Equals(
                    restoredWaiting.pendingHarvest.operationId,
                    frozenOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    restoredWaiting.pendingHarvest.ecologyOutcomeFingerprint,
                    frozenOutcomeFingerprint,
                    StringComparison.Ordinal)
                && restoredWaiting.pendingHarvest.harvestQuantity
                    == expectedHarvestQuantity
                && restoredWaiting.pendingHarvest.seedQuantity
                    == expectedSeedQuantity,
                "Frozen crop-output vector changed across capture and restore.");

            Require(admission.TryReleasePlannedOutput(
                    capacityToken,
                    FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                    out FacilityBufferMassAdmissionFailureCode releaseFailure,
                    out string releaseReason),
                "Could not release competing crop-output capacity: "
                    + releaseFailure + ":" + releaseReason);
            capacityReserved = false;

            Require(runtime.ApplyWork(
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    harvest.RequiredWork,
                    out bool completedOnRetry)
                && completedOnRetry,
                "Frozen crop output did not complete after capacity was released.");
            stockAfter = CountItem(items, crop.HarvestItemId);
            int seedStockAfter = CountItem(items, crop.SeedItemId);
            Require(stockAfter == stockBefore + expectedHarvestQuantity
                && seedStockAfter == seedStockBefore + expectedSeedQuantity,
                "Crop retry did not publish the exact frozen harvest and seed quantities. "
                + $"harvest={stockBefore}->{stockAfter} expectedDelta={expectedHarvestQuantity}, "
                + $"seed={seedStockBefore}->{seedStockAfter} expectedDelta={expectedSeedQuantity}.");

            WorldItemStackSnapshot[] published = items.GetAllStacks()
                .Where(stack => stack != null
                    && stack.State == WorldItemStackState.Loose
                    && stack.Position == plot.centerPos
                    && string.IsNullOrEmpty(stack.DestinationId))
                .ToArray();
            Require(published.Sum(stack => string.Equals(
                        stack.ItemId,
                        crop.HarvestItemId,
                        StringComparison.Ordinal)
                    ? stack.Quantity
                    : 0) == expectedHarvestQuantity
                && published.Sum(stack => string.Equals(
                        stack.ItemId,
                        crop.SeedItemId,
                        StringComparison.Ordinal)
                    ? stack.Quantity
                    : 0) == expectedSeedQuantity
                && published.Any(stack => string.Equals(
                        stack.ItemId,
                        crop.SeedItemId,
                        StringComparison.Ordinal)
                    && stack.Components.Count(component => component != null
                        && string.Equals(
                            component.componentTypeId,
                            SeedLotItemStateCodec.ComponentTypeId,
                            StringComparison.Ordinal)) == 1
                    && string.Equals(
                        stack.Components.Single(component => component != null
                            && string.Equals(
                                component.componentTypeId,
                                SeedLotItemStateCodec.ComponentTypeId,
                                StringComparison.Ordinal)).ToCanonicalString(),
                        expectedSeedCanonical,
                        StringComparison.Ordinal)),
                "Released crop output lost its exact two-line quantity or seed-lot state.");

            CropPlotSaveData completed = runtime.Capture().plots.Single(entry =>
                entry.buildingInstanceId == plotId.Value);
            Require(completed.pendingHarvest.phase == CropHarvestOutputPhase.None
                && completed.nextHarvestOperationSequence
                    == waiting.nextHarvestOperationSequence + 1,
                "Completed crop retry did not retire exactly one frozen operation.");
            Require(runtime.TryCaptureExecutionReceipt(
                    executionActionId,
                    out CropPlanExecutionReceipt executionReceipt)
                && executionReceipt.Succeeded
                && executionReceipt.ExplicitCorrelation
                && string.Equals(
                    executionReceipt.PlotId,
                    plotId.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    executionReceipt.HarvestOperationId,
                    frozenOperationId,
                    StringComparison.Ordinal)
                && executionReceipt.Outputs.Count == 2
                && executionReceipt.Outputs.Sum(output => output.Quantity)
                    == expectedHarvestQuantity + expectedSeedQuantity
                && executionReceipt.Outputs.Sum(output => output.MassGrams)
                    == executionReceipt.OutputMassGrams,
                "Completed Crop action did not expose its exact terminal receipt.");
            Require(publication.TryCaptureBatch(
                    executionReceipt.OutputBatchCommitId,
                    allowAcknowledged: true,
                    out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
                    out bool batchAcknowledged,
                    out FacilityBufferPlannedOutputPublicationFailureCode
                        captureFailure,
                    out string captureReason)
                && batchAcknowledged
                && batch != null
                && batch.TotalMassGrams == executionReceipt.OutputMassGrams
                && string.Equals(
                    batch.OutcomeFingerprint,
                    executionReceipt.OutputOutcomeFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    batch.PlannedOutputFingerprint,
                    executionReceipt.PlannedOutputFingerprint,
                    StringComparison.Ordinal)
                && executionReceipt.Outputs.All(output => batch.Stacks.Any(stack =>
                    string.Equals(
                        stack.OutputLineId,
                        output.OutputLineId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemId,
                        output.ItemId,
                        StringComparison.Ordinal)
                    && stack.Quantity == output.Quantity
                    && stack.MassGrams == output.MassGrams)),
                "Crop receipt did not exact-join its acknowledged physical batch: "
                + captureFailure + ":" + captureReason);

            string terminalDigest = executionReceipt.RuntimeReceiptDigest;
            runtime.Tick();
            CropPlotSnapshot awaitingAck = runtime.Plots.Single(entry =>
                entry.PlotId == plotId.Value);
            Require(string.Equals(
                    awaitingAck.BlockedReason,
                    "crop-cycle-execution-receipt-awaiting-acknowledgement",
                    StringComparison.Ordinal),
                "Explicit terminal receipt did not block automatic overwrite.");
            DungeonCropPlotSaveData terminalSave = runtime.Capture();
            runtime.Restore(runtime.BuildRestore(terminalSave));
            Require(runtime.TryCaptureExecutionReceipt(
                    executionActionId,
                    out CropPlanExecutionReceipt restoredReceipt)
                && string.Equals(
                    restoredReceipt.RuntimeReceiptDigest,
                    terminalDigest,
                    StringComparison.Ordinal),
                "Crop terminal receipt drifted across save/restore.");
            Require(!runtime.TryAcknowledgeExecutionReceipt(
                    executionActionId,
                    new string('0', 64),
                    out string staleAcknowledgementFailure)
                && string.Equals(
                    staleAcknowledgementFailure,
                    "crop-cycle-execution-receipt-digest-mismatch",
                    StringComparison.Ordinal)
                && runtime.TryCaptureExecutionReceipt(
                    executionActionId,
                    out CropPlanExecutionReceipt retainedAfterStaleAck)
                && string.Equals(
                    retainedAfterStaleAck.RuntimeReceiptDigest,
                    terminalDigest,
                    StringComparison.Ordinal),
                "Stale Crop receipt acknowledgement removed or changed the live owner.");
            Require(runtime.TryAcknowledgeExecutionReceipt(
                    executionActionId,
                    executionReceipt.RuntimeReceiptDigest,
                    out string acknowledgementFailure),
                "Crop terminal receipt acknowledgement failed: "
                + acknowledgementFailure);
            Require(!runtime.TryCaptureExecutionReceipt(
                    executionActionId,
                    out _),
                "Acknowledged Crop terminal receipt remained observable.");
            Require(!runtime.TryAcknowledgeExecutionReceipt(
                    executionActionId,
                    executionReceipt.RuntimeReceiptDigest,
                    out string duplicateAcknowledgementFailure)
                && string.Equals(
                    duplicateAcknowledgementFailure,
                    "crop-cycle-execution-receipt-not-found",
                    StringComparison.Ordinal),
                "Duplicate Crop receipt acknowledgement was not rejected.");
            runtime.Tick();
            CropPlotSaveData acknowledgedSave = runtime.Capture().plots.Single(entry =>
                entry.buildingInstanceId == plotId.Value);
            Require(acknowledgedSave.cycleExecutionReceipt.IsEmpty,
                "Acknowledged Crop terminal receipt was not retired.");
            Require(!runtime.ApplyWork(
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    harvest.RequiredWork,
                    out bool replayCompleted)
                && !replayCompleted
                && CountItem(items, crop.HarvestItemId) == stockAfter
                && CountItem(items, crop.SeedItemId) == seedStockAfter,
                "Completed crop operation replayed its physical output.");
        }
        finally
        {
            if (capacityReserved)
            {
                admission.TryReleasePlannedOutput(
                    capacityToken,
                    FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                    out _,
                    out _);
            }
        }

        return "PASS CROP_OUTPUT_FACILITY_BUFFER_WAIT_RESTORE_RETRY_EXACT_ONCE "
            + $"plot={plotId.Value};harvest={stockBefore}->{stockAfter}"
            + ";capacityWait=true;frozenRestore=true;replayDelta=0"
            + ";executionReceipt=true;physicalBatchJoin=true"
            + ";terminalRestore=true;ackRetention=true";
    }

    private static void VerifyStrictSaveIsolation(
        CropPlotSaveSection saveSection)
    {
        Require(
            saveSection is IDungeonSaveSectionPreflight
            && saveSection is IDungeonRollbackFreeSaveSection,
            "Crop-plot save section is not strict and rollback-free.");
        string before = saveSection.Capture();
        DungeonCropPlotSaveData invalid =
            JsonUtility.FromJson<DungeonCropPlotSaveData>(before);
        Require(invalid?.plots?.Count > 0, "Crop-plot isolation fixture is empty.");
        invalid.plots[0].sowWork = -1f;
        bool rejected = false;
        try
        {
            ((IDungeonSaveSectionPreflight)saveSection).ValidatePayload(
                JsonUtility.ToJson(invalid),
                saveSection.SectionVersion,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, "Negative crop-plot progress was accepted.");
        Require(
            string.Equals(before, saveSection.Capture(), StringComparison.Ordinal),
            "Failed crop-plot preflight mutated live state.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void WriteReport(IEnumerable<string> lines)
    {
        string absolutePath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ".");
        File.WriteAllLines(absolutePath, lines);
    }
}

public sealed class Wim016FiniteNetworkPlayModeRunner : MonoBehaviour
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-016-finite-network-runtime.txt";

    private IEnumerator Start()
    {
        var lines = new List<string> { "WIM016 finite real-network / actual-clock witness", "status=RUNNING" };
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllLines(ReportPath, lines);
        IEnumerator test = CropPlotDebugScenarios.PrepareWim016FiniteNetworkFocused(lines);
        Exception failure = null;
        while (true)
        {
            bool moved;
            object current;
            try
            {
                moved = test.MoveNext();
                current = moved ? test.Current : null;
            }
            catch (Exception error)
            {
                failure = error;
                break;
            }
            if (!moved) break;
            yield return current;
        }
        try { (test as IDisposable)?.Dispose(); }
        catch (Exception error) { failure ??= error; }
        lines[1] = failure == null ? "status=PASS" : "status=FAIL";
        if (failure != null) lines.Add(failure.ToString());
        lines.Add("Operator must stop protected disposable Play; no save-section/full-cycle/AI completion claimed.");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log("WIM016_FINITE_NETWORK_" + (failure == null ? "PASS" : "FAIL") + ": " + ReportPath);
        Destroy(gameObject);
    }
}

public sealed class CropPlotDebugPlayModeRunner : MonoBehaviour
{
    private const float ResolveTimeoutSeconds = 30f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => StartPending();

    internal static void StartPending()
    {
        if (!File.Exists(CropPlotDebugScenarios.RequestPath))
            return;
        File.Delete(CropPlotDebugScenarios.RequestPath);
        if (FindFirstObjectByType<CropPlotDebugPlayModeRunner>() != null)
            return;
        new GameObject(nameof(CropPlotDebugPlayModeRunner))
            .AddComponent<CropPlotDebugPlayModeRunner>();
    }

    private IEnumerator Start()
    {
        float deadline = Time.realtimeSinceStartup + ResolveTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            DungeonRuntimeLifetimeScope scope = FindFirstObjectByType<
                DungeonRuntimeLifetimeScope>();
            if (scope?.Container != null)
                break;
            yield return null;
        }

        try
        {
            CropPlotDebugScenarios.VerifyRuntimeFromMenu();
            string reportPath = Path.GetFullPath(
                "docs/implementation-reports/crop-plot-runtime-latest.txt");
            string report = File.Exists(reportPath)
                ? File.ReadAllText(reportPath)
                : string.Empty;
            if (!report.Contains("valid=true", StringComparison.Ordinal))
            {
                Debug.LogError(
                    "CROP_PLOT_REQUESTED_PLAYMODE_VERIFICATION_FAILED");
            }
            else
            {
                Debug.Log("CROP_PLOT_REQUESTED_PLAYMODE_VERIFICATION_PASS");
            }
        }
        finally
        {
            Destroy(gameObject);
            EditorApplication.ExitPlaymode();
        }
    }
}
#endif
