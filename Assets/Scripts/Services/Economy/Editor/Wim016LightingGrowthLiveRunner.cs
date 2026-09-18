#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using static UnityEngine.Object;

// Root-owned important integration witness. Research completion and material
// arrival are controlled setup; construction, power, field light, crop growth,
// player presenters and whole-save restore use the production paths.
public sealed class Wim016LightingGrowthLiveRunner
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-016-lighting-growth-live.txt";

    private readonly List<string> lines = new();
    private static bool running;
    private DungeonRuntimeLifetimeScope scope;
    private IObjectResolver services;
    private IGameClock clock;
    private IGameTimeScaleController timeScale;
    private GameManager game;
    private CropPlotRuntime crops;
    private IPowerInfrastructureQuery power;
    private IPowerInfrastructureCommand powerCommands;
    private IEnvironmentalFieldQuery field;
    private string plotId;
    private string generatorId;
    private string sunLampId;
    private string artificialSunId;

    public static string StartFocused()
    {
        Require(Application.isPlaying && !running,
            "Start once in a fresh disposable main Play session.");
        DungeonRuntimeLifetimeScope scope =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main runtime is not initialized.");
        IDisposable saveCommands = scope.Container
            .Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(saveCommands != null, "Cannot protect user save files.");
        saveCommands.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        GameManager game = FindFirstObjectByType<GameManager>();
        Require(game != null, "Missing actual main coroutine host.");
        game.isPause = true;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        try
        {
            game.StartCoroutine(new Wim016LightingGrowthLiveRunner().Observe());
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
            object current = null;
            bool moved;
            try
            {
                moved = stack.Peek().MoveNext();
                if (moved)
                    current = stack.Peek().Current;
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
            if (current is IEnumerator nested)
                stack.Push(nested);
            else
                yield return current;
        }
        while (stack.Count > 0)
            (stack.Pop() as IDisposable)?.Dispose();
        Pause();
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add("scope=actual authored research unlock publication, construction site/order/exact physical BOM/full work, I03/U04 power, field light, indoor crop growth, power and crop presenters, current whole-save outage restore; research labor/AI material haul/natural priority/full crop cycle/six-adult balance are not claimed");
        lines.Add("cleanup=operator stops persistence-protected disposable Play; no scene or user persistence writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        running = false;
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        services = scope.Container;
        game = FindFirstObjectByType<GameManager>();
        clock = services.Resolve<IGameClock>();
        timeScale = services.Resolve<IGameTimeScaleController>();
        crops = services.Resolve<CropPlotRuntime>();
        power = services.Resolve<IPowerInfrastructureQuery>();
        powerCommands = services.Resolve<IPowerInfrastructureCommand>();
        field = services.Resolve<IEnvironmentalFieldQuery>();

        OwnerRunManager owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Missing main owner preparation.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(services.Resolve<IDungeonSpaceExpansionCommand>()
                    .TryReconcileNewRunTierZero(out DungeonSpaceExpansionResult expansion,
                        out string expansionFailure)
                    && expansion.CurrentInteriorColumns == 29,
                "Normal TierZero preparation failed: " + expansionFailure);
            Button button = Resources.FindObjectsOfTypeAll<Button>()
                .SingleOrDefault(value => value != null
                    && value.gameObject.scene.isLoaded
                    && value.gameObject.activeInHierarchy
                    && value.name == "OwnerOption_1001");
            Require(button != null && button.IsInteractable()
                    && PlayModeVerificationFrameWait.DispatchPointerClick(
                        button.gameObject, Vector2.zero),
                "Actual owner UI is unavailable.");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null,
                "Actual party UI failed to publish an owner.");
        }

        ICharacterAiWorldRegistry world =
            services.Resolve<ICharacterAiWorldRegistry>();
        CharacterActor worker = world.Characters
            .Where(value => value != null && !value.IsOwner && !value.IsDead
                && value.GetComponent<AbilityWork>() != null)
            .OrderBy(value => value.Identity.PersistentId,
                StringComparer.Ordinal)
            .FirstOrDefault();
        Require(worker != null, "No actual staff can complete construction.");
        PauseAllCharacterAi();

        Require(services.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid)
                && grid != null,
            "Missing actual main Grid.");
        BuildingSO generatorDefinition = LoadBuilding(
            "Assets/Resources/SO/Building/Industrial/I03_마나_발전기.asset");
        BuildingSO ductDefinition = LoadBuilding(
            "Assets/Resources/SO/Building/Industrial/U04_통합_기반_덕트.asset");
        BuildingSO sunLampDefinition = LoadBuilding(
            "Assets/Resources/SO/Building/Industrial/I19_태양등.asset");
        BuildingSO artificialSunDefinition = LoadBuilding(
            "Assets/Resources/SO/Building/Industrial/I20_인공태양.asset");
        BuildingSO plotDefinition = LoadBuilding(
            "Assets/Resources/SO/Building/Modular/P24_실내재배조.asset");
        LightingFixtureLayout layout = PrepareFixtureLayout(
            grid, world, generatorDefinition, sunLampDefinition,
            artificialSunDefinition, plotDefinition, ductDefinition);
        Vector2Int generatorAnchor = layout.GeneratorAnchor;
        Vector2Int sunLampAnchor = layout.SunLampAnchor;
        Vector2Int plotAnchor = layout.PlotAnchor;
        Vector2Int artificialSunAnchor = layout.ArtificialSunAnchor;
        lines.Add("setup=disposable Play cleared Facility-only initial showcase rooms; "
            + "cropRoom=" + layout.CropRoomBounds
            + "; artificialSunRoom=" + layout.ArtificialSunRoomBounds
            + "; generator=" + generatorAnchor
            + "; ducts=" + layout.DuctCells.Count);

        IDungeonGridBuildingControllerProvider controllerProvider =
            services.Resolve<IDungeonGridBuildingControllerProvider>();
        int constructionCount = world.Buildings.OfType<ConstructionSite>().Count();
        Require(!controllerProvider.Controller.TryPlaceConstructionSite(
                    sunLampDefinition, sunLampAnchor, out string lockedSunReason)
                && world.Buildings.OfType<ConstructionSite>().Count()
                    == constructionCount,
            "I19 was buildable before its authored research unlock: "
            + lockedSunReason);
        Require(!controllerProvider.Controller.TryPlaceConstructionSite(
                    artificialSunDefinition, artificialSunAnchor,
                    out string lockedArtificialReason)
                && world.Buildings.OfType<ConstructionSite>().Count()
                    == constructionCount,
            "I20 was buildable before its authored research unlock: "
            + lockedArtificialReason);
        lines.Add("[PASS] pre-unlock I19/I20 production placement rejected without a construction owner");

        BlueprintResearchRuntime research = services
            .Resolve<ProgressionSceneRuntimeReferences>().BlueprintResearch;
        CompleteResearchTree(research, "research:industry:electric-lighting");
        CompleteResearchTree(research, "research:industry:mana-power");
        CompleteResearchTree(research, "research:agriculture:indoor");
        Require(research.State.IsBuildingUnlocked(sunLampDefinition.id)
                && research.State.IsBuildingUnlocked(artificialSunDefinition.id)
                && research.State.IsBuildingUnlocked(plotDefinition.id),
            "Authored research completion did not publish all three building unlocks.");

        BuildableObject generator = PlaceControlled(grid, generatorDefinition,
            generatorAnchor);
        foreach (Vector2Int ductCell in layout.DuctCells)
            PlaceControlled(grid, ductDefinition, ductCell);
        BuildableObject plot = PlaceControlled(grid, plotDefinition, plotAnchor);
        BuildableObject sunLamp = ConstructActual(worker, world,
            sunLampDefinition, sunLampAnchor, controllerProvider.Controller);
        BuildableObject artificialSun = ConstructActual(worker, world,
            artificialSunDefinition, artificialSunAnchor,
            controllerProvider.Controller);
        plotId = plot.RequirePersistentInstanceId().Value;
        generatorId = generator.RequirePersistentInstanceId().Value;
        sunLampId = sunLamp.RequirePersistentInstanceId().Value;
        artificialSunId = artificialSun.RequirePersistentInstanceId().Value;
        lines.Add("[PASS] I19/I20 actual construction sites consumed exact physical authored BOM and full construction WU; completed buildings published");

        PrepareGrowingCrop(plot);
        Require(powerCommands.SetConnectionEnabled(sunLamp, false).Succeeded
                && powerCommands.SetConnectionEnabled(artificialSun, false).Succeeded,
            "Cannot isolate authored grow lights before power startup.");

        yield return AdvanceUntil(
            () => power.TryGetNode(Generator(), out _)
                && power.TryGetNode(SunLamp(), out _)
                && power.TryGetNode(ArtificialSun(), out _),
            "Actual I03/U04/I19/I20 power topology did not publish.",
            1f);
        Pause();
        SupplyGeneratorFuel("startup");
        yield return AdvanceUntil(
            () => power.TryGetNode(Generator(), out PowerNodeSnapshot node)
                && node.ProductionPerSecond > 0f,
            "Actual I03 did not consume the arrived physical fuel and produce power.",
            2f);
        Pause();

        yield return VerifyLamp(sunLampId, artificialSunId,
            "I19", verifyOutageRestore: true);
        yield return VerifyLamp(artificialSunId, sunLampId,
            "I20", verifyOutageRestore: false);
    }

    private IEnumerator VerifyLamp(
        string lampId,
        string otherLampId,
        string code,
        bool verifyOutageRestore)
    {
        BuildableObject lamp = Resolve(lampId);
        BuildableObject other = Resolve(otherLampId);
        Require(powerCommands.SetConnectionEnabled(other, false).Succeeded,
            code + " could not isolate the other grow light.");
        if (power.TryGetNode(lamp, out PowerNodeSnapshot before)
            && before.ConnectionEnabled)
        {
            Require(powerCommands.SetConnectionEnabled(lamp, false).Succeeded,
                code + " could not establish a disconnected baseline.");
        }
        yield return AdvanceUntil(
            () => !power.IsPowered(Resolve(lampId))
                && CellLight() < Plot().LightSufficientThreshold
                && Plot().LightGrowthMultiplier < .999f,
            code + " disconnected baseline did not reach sub-sufficient light.",
            1f);
        Pause();

        EnsureIndustryPanelOpen();
        yield return null;
        SupplyGeneratorFuel(code + " activation");
        Click("IndustryPowerConnection_" + lampId);
        yield return AdvanceUntil(
            () => power.IsPowered(Resolve(lampId))
                && CellLight() >= Plot().LightSufficientThreshold
                && Plot().LightGrowthMultiplier >= 0.999f,
            code + " powered field did not reach the crop's sufficient-light threshold.",
            2f);
        Pause();
        float growthBefore = SavedPlot().growthHours;
        float poweredWindowStart = clock.Time;
        yield return AdvanceGameFramesAndSeconds(8, 1f);
        float growthPowered = SavedPlot().growthHours;
        float poweredGain = growthPowered - growthBefore;
        float poweredElapsedHours =
            (clock.Time - poweredWindowStart)
            / GameSimulationTimeRules.SecondsPerGameHour;
        float poweredGrowthRate = poweredGain / poweredElapsedHours;
        Require(poweredGrowthRate > 0f,
            code + " sufficient actual light did not advance crop growth.");
        string poweredUi = RenderCropPanelText();
        string poweredLightLine = poweredUi.Split('\n')
            .SingleOrDefault(value => value.StartsWith(
                "광량 · 현재", StringComparison.Ordinal));
        Require(poweredLightLine != null
                && poweredLightLine.Contains(
                    "성장 100", StringComparison.Ordinal),
            code + " actual crop panel did not show sufficient light/100%: "
            + poweredUi);

        EnsureIndustryPanelOpen();
        yield return null;
        SupplyGeneratorFuel(code + " recovery");
        Click("IndustryPowerConnection_" + lampId);
        yield return AdvanceUntil(
            () => !power.IsPowered(Resolve(lampId))
                && (verifyOutageRestore
                    ? IsIndoorAmbientLightSettled()
                    : IsReducedLightEstablished(.75f)),
            code + (verifyOutageRestore
                ? " outage did not settle back to indoor ambient light."
                : " outage did not establish a materially reduced-light window."),
            2f);
        Pause();
        float unpoweredMultiplier = Plot().LightGrowthMultiplier;
        float unpoweredGrowthBefore = SavedPlot().growthHours;
        float unpoweredWindowStart = clock.Time;
        yield return AdvanceGameFramesAndSeconds(8, 1f);
        float unpoweredGrowth = SavedPlot().growthHours;
        float unpoweredGain = unpoweredGrowth - unpoweredGrowthBefore;
        float unpoweredElapsedHours =
            (clock.Time - unpoweredWindowStart)
            / GameSimulationTimeRules.SecondsPerGameHour;
        float unpoweredGrowthRate = unpoweredGain / unpoweredElapsedHours;
        float maximumReducedRateRatio = Mathf.Min(
            .9f,
            unpoweredMultiplier + .1f);
        Require(unpoweredGrowthRate >= 0f
                && unpoweredGrowthRate
                    < poweredGrowthRate * maximumReducedRateRatio,
            code + " indoor ambient growth was not slower than powered growth: "
            + "poweredRate=" + poweredGrowthRate
            + "; unpoweredRate=" + unpoweredGrowthRate
            + "; poweredFrames>=8; unpoweredFrames>=8");
        float observedUnpoweredMultiplier = Plot().LightGrowthMultiplier;
        float observedUnpoweredLight = CellLight();
        string unpoweredUi = RenderCropPanelText();
        int expectedGrowthPercent = Mathf.RoundToInt(
            observedUnpoweredMultiplier * 100f);
        string unpoweredLightLine = unpoweredUi.Split('\n')
            .SingleOrDefault(value => value.StartsWith(
                "광량 · 현재", StringComparison.Ordinal));
        Require(unpoweredLightLine != null
                && unpoweredLightLine.Contains(
                    "성장 " + expectedGrowthPercent,
                    StringComparison.Ordinal)
                && !unpoweredLightLine.Contains(
                    "성장 100", StringComparison.Ordinal),
            code + " actual crop panel did not show reduced indoor-ambient growth: "
            + unpoweredUi);

        if (verifyOutageRestore)
        {
            IDungeonGameSaveService saves =
                services.Resolve<IDungeonGameSaveService>();
            DungeonGameSaveData outage = saves.FromJson(
                saves.ToJson(saves.Capture()));
            Require(saves.TryRestore(outage,
                    out DungeonGameRestoreReport restore)
                    && restore.Success,
                code + " current whole-save outage restore failed: "
                + string.Join(" | ", restore.Errors));
            Pause();
            PauseAllCharacterAi();
            lamp = Resolve(lampId);
            Require(power.TryGetNode(lamp, out PowerNodeSnapshot restoredNode)
                    && !restoredNode.ConnectionEnabled
                    && !power.IsPowered(lamp)
                    && Mathf.Approximately(
                        SavedPlot().growthHours, unpoweredGrowth)
                    && Mathf.Abs(Plot().LightGrowthMultiplier
                        - observedUnpoweredMultiplier) <= 0.01f,
                code + " outage restore changed connection, growth or reduced-light state.");
            lines.Add("[PASS] I19 current whole-save outage restores connection-off, exact crop progress and reduced indoor-ambient growth");
            RebuildIndustryPanel();
            yield return null;
        }

        EnsureIndustryPanelOpen();
        yield return null;
        Click("IndustryPowerConnection_" + lampId);
        yield return AdvanceUntil(
            () => power.IsPowered(Resolve(lampId))
                && CellLight() >= Plot().LightSufficientThreshold
                && Plot().LightGrowthMultiplier >= 0.999f,
            code + " did not recover actual field light after reconnect.",
            2f);
        Pause();
        float recoveredBefore = SavedPlot().growthHours;
        yield return AdvanceGameFramesAndSeconds(4, .5f);
        Require(SavedPlot().growthHours > recoveredBefore,
            code + " crop growth did not resume after reconnect.");
        lines.Add("[PASS] " + code
            + " independent actual power->field light "
            + CellLight().ToString("0.###")
            + "->crop100%/positive growth; disconnect returns to indoor light "
            + observedUnpoweredLight.ToString("0.###")
            + "/growth " + observedUnpoweredMultiplier.ToString("P0")
            + "; reconnect restores full growth");
        Require(powerCommands.SetConnectionEnabled(Resolve(lampId), false).Succeeded,
            code + " cleanup isolation failed.");
        yield return AdvanceUntil(
            () => !power.IsPowered(Resolve(lampId)),
            code + " did not disconnect before the next independent lamp.",
            1f);
        Pause();
    }

    private BuildableObject ConstructActual(
        CharacterActor worker,
        ICharacterAiWorldRegistry world,
        BuildingSO definition,
        Vector2Int anchor,
        DungeonStoryGridBuildingController controller)
    {
        Require(controller.TryPlaceConstructionSite(
                definition, anchor, out string placementFailure),
            definition.objectName + " actual placement failed: "
            + placementFailure);
        ConstructionSite site = world.Buildings.OfType<ConstructionSite>()
            .Single(value => value != null && value.id == definition.id
                && value.centerPos == anchor);
        IWorkOrderRuntime orders = services.Resolve<IWorkOrderRuntime>();
        Require(orders.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct,
                out WorkOrderProgressState order),
            definition.objectName + " construction order was not published.");
        IWorldItemStackRuntime items = services.Resolve<IWorldItemStackRuntime>();
        foreach (KeyValuePair<string, int> material in
                 order.ItemMaterialRequirements.OrderBy(
                     value => value.Key, StringComparer.Ordinal))
        {
            Require(items.SpawnItemAt(material.Key, material.Value, anchor,
                        WorldItemStackState.FacilityBuffer,
                        order.MaterialDestinationId, out int spawned)
                    && spawned == material.Value,
                definition.objectName + " physical construction input failed: "
                + material.Key);
        }
        Require(orders.RefreshMaterialsReady(site),
            definition.objectName + " exact physical BOM did not become ready.");
        Require(orders.ApplyWork(worker, site, BuiltInWorkTypeIds.Construct,
                    order.RequiredWork, out bool completed,
                    out bool effects, out string workFailure)
                && completed && effects,
            definition.objectName + " construction work failed: " + workFailure);
        return world.Buildings.Single(value => value != null
            && value is not ConstructionSite
            && value.id == definition.id
            && value.centerPos == anchor);
    }

    private void PrepareGrowingCrop(BuildableObject plot)
    {
        IResourceEconomyContentCatalog catalog =
            services.Resolve<IResourceEconomyContentCatalog>();
        Require(catalog.TryGetCrop("crop:twilight-grain",
                out CropDefinitionSO crop),
            "Missing authored crop:twilight-grain.");
        crops.Restore(crops.BuildRestore(crops.Capture()));
        Require(crops.TrySetCrop(plot, crop.CropId,
                out string selectFailure), selectFailure);
        crops.Tick();
        CropPlotSnapshot row = crops.Plots.Single(
            value => value.PlotId == plotId);
        IWorldItemStackRuntime items = services.Resolve<IWorldItemStackRuntime>();
        IItemTransferService transfers = services.Resolve<IItemTransferService>();
        foreach (KeyValuePair<string, int> input in row.RequiredMaterials)
        {
            int spawned;
            bool success = input.Key == crop.SeedItemId
                ? transfers.TrySpawnItemWithComponents(input.Key, input.Value,
                    plot.centerPos, WorldItemStackState.FacilityBuffer,
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
                    }, out spawned)
                : items.SpawnItemAt(input.Key, input.Value, plot.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    row.MaterialDestinationId, out spawned);
            Require(success && spawned == input.Value,
                "Physical indoor sow input failed: " + input.Key);
        }
        crops.Tick();
        Require(crops.TryGetWork(plot, BuiltInWorkTypeIds.Sow,
                    out CropPlotWorkSnapshot sow) && sow.Available,
            "Indoor sow unavailable: " + sow.UnavailableReason);
        Require(crops.ApplyWork(plot, BuiltInWorkTypeIds.Sow,
                    sow.RequiredWork, out bool sown) && sown,
            "Physical indoor sow did not commit.");
        DungeonCropPlotSaveData prepared = crops.Capture();
        CropPlotSaveData saved = prepared.plots.Single(
            value => value.buildingInstanceId == plotId);
        saved.currentWater = 2f;
        crops.Restore(crops.BuildRestore(prepared));

        IEnvironmentalFieldPersistence environment =
            services.Resolve<IEnvironmentalFieldPersistence>();
        DungeonEnvironmentalFieldSaveData fieldState = environment.Capture();
        fieldState.cells.RemoveAll(value => value.x == plot.centerPos.x
            && value.y == plot.centerPos.y);
        fieldState.cells.Add(new EnvironmentalCellSaveData
        {
            x = plot.centerPos.x,
            y = plot.centerPos.y,
            temperatureC = 20f,
            airQuality = 100f,
            lightLevel = DungeonStory.Environment
                .EnvironmentalFieldSimulationRules.IndoorAmbientLight
        });
        fieldState.cells = fieldState.cells
            .OrderBy(value => value.y)
            .ThenBy(value => value.x)
            .ToList();
        environment.Restore(environment.PrepareRestore(fieldState));
        Require(Plot().Phase == CropPlotPhase.Growing
                && Mathf.Approximately(Plot().CurrentWater, 2f),
            "Controlled indoor crop is not in a stable growing state.");
    }

    private void CompleteResearchTree(
        BlueprintResearchRuntime runtime,
        string projectId)
    {
        Require(runtime.ProjectCatalog.TryGet(
                new ResearchProjectId(projectId),
                out ResearchProjectSO project)
                && project != null,
            "Missing authored research project: " + projectId);
        CompleteResearchTree(runtime, project, new HashSet<string>(
            StringComparer.Ordinal));
    }

    private void CompleteResearchTree(
        BlueprintResearchRuntime runtime,
        ResearchProjectSO project,
        HashSet<string> active)
    {
        if (runtime.State.Projects.IsCompleted(project.ProjectId))
            return;
        Require(active.Add(project.ProjectId.Value),
            "Research prerequisite cycle at " + project.ProjectId.Value);
        foreach (ResearchProjectSO prerequisite in project.Prerequisites
                     .OrderBy(value => value.ProjectId.Value,
                         StringComparer.Ordinal))
            CompleteResearchTree(runtime, prerequisite, active);
        active.Remove(project.ProjectId.Value);
        Require(runtime.TryCompleteProjectImmediatelyForVerification(
                project.ProjectId, out string failure),
            "Authored research completion failed: " + failure);
    }

    private BuildableObject PlaceControlled(
        Grid grid,
        BuildingSO definition,
        Vector2Int anchor)
    {
        BuildableObject building = services.Resolve<IGridBuildingObjectFactory>()
            .Create(grid, definition, anchor);
        Require(building != null,
            "Controlled fixture factory rejected " + definition.objectName);
        foreach (MonoBehaviour component in
                 building.GetComponentsInChildren<MonoBehaviour>(true))
            services.Inject(component);
        building.SetGrid(grid);
        building.Initialization(definition, anchor);
        Require(grid.RegisterOccupant(building, definition.layer,
                definition.GetGridPosList(anchor),
                definition.Placement.IsMovement),
            "Controlled fixture grid registration failed: "
            + definition.objectName);
        return building;
    }

    private LightingFixtureLayout PrepareFixtureLayout(
        Grid grid,
        ICharacterAiWorldRegistry world,
        BuildingSO generator,
        BuildingSO sunLamp,
        BuildingSO artificialSun,
        BuildingSO plot,
        BuildingSO duct)
    {
        IRoomLayoutCache roomCache = services.Resolve<IRoomLayoutCache>();
        RoomInstance[] rooms = roomCache.GetLayout(grid).Rooms
            .Where(value => value != null && value.IsUsable)
            .OrderBy(value => value.Bounds.yMin)
            .ThenBy(value => value.Bounds.xMin)
            .ToArray();
        RoomInstance cropRoom = rooms.FirstOrDefault(value =>
            value.Cells.Count >= 4 && IsFacilityOnlyShowcaseRoom(value, world));
        Require(cropRoom != null,
            "No Facility-only existing usable room can host P24 and I19.");
        ClearShowcaseRoom(cropRoom, world);
        RoomInstance artificialRoom = rooms.FirstOrDefault(value =>
            value.Id != cropRoom.Id
            && value.Cells.Count >= 3
            && IsFacilityOnlyShowcaseRoom(value, world));
        Require(artificialRoom != null,
            "No second Facility-only existing usable room can host I20.");
        ClearShowcaseRoom(artificialRoom, world);

        Vector2Int cropLeft = FindContiguousRun(cropRoom, 4);
        Vector2Int plotAnchor = cropLeft + Vector2Int.right;
        Vector2Int sunLampAnchor = cropLeft + Vector2Int.right * 3;
        Vector2Int artificialLeft = FindContiguousRun(artificialRoom, 3);
        Vector2Int artificialSunAnchor = artificialLeft + Vector2Int.right;

        HashSet<Vector2Int> reserved = new(
            plot.GetGridPosList(plotAnchor)
                .Concat(sunLamp.GetGridPosList(sunLampAnchor))
                .Concat(artificialSun.GetGridPosList(artificialSunAnchor)));
        Vector2Int generatorAnchor = rooms
            .Where(value => value.Id != cropRoom.Id
                && value.Id != artificialRoom.Id)
            .SelectMany(value => value.Cells
                .OrderBy(position => position.y)
                .ThenBy(position => position.x)
                .Select(position => (Room: value, Anchor: position)))
            .Where(value => generator.GetGridPosList(value.Anchor).All(position =>
                value.Room.ContainsCell(position)
                && !reserved.Contains(position)
                && grid.GetGridCell(position) is GridCell cell
                && cell.CanBuildInArea(generator)
                && cell.CanOccupy(generator.layer)))
            .Select(value => (Vector2Int?)value.Anchor)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No existing usable-room two-cell run can host controlled I03.");

        HashSet<Vector2Int> ductCells = new();
        AddManhattanPath(ductCells, generatorAnchor, sunLampAnchor);
        AddManhattanPath(ductCells, generatorAnchor, artificialSunAnchor);
        foreach (Vector2Int position in ductCells)
        {
            Require(grid.GetGridCell(position) is GridCell cell
                    && cell.CanBuildInArea(duct)
                    && cell.CanOccupy(duct.layer),
                "Actual duct path is not legal at " + position + ".");
        }

        Require(plot.GetGridPosList(plotAnchor).All(position =>
                    cropRoom.ContainsCell(position)
                    && grid.GetGridCell(position)?.CanOccupy(plot.layer) == true)
                && sunLamp.GetGridPosList(sunLampAnchor).All(position =>
                    cropRoom.ContainsCell(position)
                    && grid.GetGridCell(position)?.CanOccupy(sunLamp.layer) == true
                    && grid.GetGridCell(position)?.CanOccupy(
                        GridLayer.Construction) == true)
                && artificialSun.GetGridPosList(artificialSunAnchor).All(position =>
                    artificialRoom.ContainsCell(position)
                    && grid.GetGridCell(position)?.CanOccupy(
                        artificialSun.layer) == true
                    && grid.GetGridCell(position)?.CanOccupy(
                        GridLayer.Construction) == true),
            "Prepared existing-room fixture is not legal after showcase teardown.");
        return new LightingFixtureLayout(
            generatorAnchor,
            sunLampAnchor,
            plotAnchor,
            artificialSunAnchor,
            ductCells.OrderBy(value => value.y).ThenBy(value => value.x).ToArray(),
            cropRoom.Bounds,
            artificialRoom.Bounds);
    }

    private static bool IsFacilityOnlyShowcaseRoom(
        RoomInstance room,
        ICharacterAiWorldRegistry world)
    {
        BuildableObject[] occupants = world.Buildings
            .Where(value => value != null
                && value.BuildingData?.layer == GridLayer.Building
                && value.buildPoses.Any(room.ContainsCell))
            .ToArray();
        return occupants.Length > 0
            && occupants.All(value => value is Facility
                && value.BuildingData.GetStorageMassCapacityGrams() <= 0L
                && value.BuildingData.GetAbility<BuildingInternalStockAbility>() == null
                && value.BuildingData.GetAbility<BuildingProductionAbility>() == null
                && value.BuildingData.GetAbility<BuildingProductionWorkstationAbility>() == null
                && value.BuildingData.GetAbility<BuildingResearchCapacityAbility>() == null);
    }

    private static void ClearShowcaseRoom(
        RoomInstance room,
        ICharacterAiWorldRegistry world)
    {
        BuildableObject[] occupants = world.Buildings
            .Where(value => value is Facility
                && value.BuildingData?.layer == GridLayer.Building
                && value.buildPoses.Any(room.ContainsCell))
            .Distinct()
            .ToArray();
        Require(occupants.Length > 0,
            "Facility-only showcase room unexpectedly has no fixtures.");
        foreach (BuildableObject occupant in occupants)
            DestroyImmediate(occupant.gameObject);
        Require(room.Cells.All(position =>
                !world.Buildings.Any(value => value != null
                    && value.BuildingData?.layer == GridLayer.Building
                    && value.buildPoses.Contains(position))),
            "Showcase teardown left a Building-layer runtime owner.");
    }

    private static Vector2Int FindContiguousRun(RoomInstance room, int width)
    {
        foreach (Vector2Int left in room.Cells.OrderBy(value => value.y)
                     .ThenBy(value => value.x))
        {
            if (Enumerable.Range(0, width).All(offset =>
                    room.ContainsCell(left + Vector2Int.right * offset)))
                return left;
        }
        throw new InvalidOperationException(
            "Usable room has no contiguous " + width + "-cell run: "
            + room.Bounds);
    }

    private static void AddManhattanPath(
        ISet<Vector2Int> cells,
        Vector2Int from,
        Vector2Int to)
    {
        Vector2Int cursor = from;
        cells.Add(cursor);
        while (cursor.x != to.x)
        {
            cursor.x += Math.Sign(to.x - cursor.x);
            cells.Add(cursor);
        }
        while (cursor.y != to.y)
        {
            cursor.y += Math.Sign(to.y - cursor.y);
            cells.Add(cursor);
        }
    }

    private IEnumerator AdvanceUntil(
        Func<bool> condition,
        string failure,
        float minimumGameSeconds)
    {
        float gameStart = clock.Time;
        float wallStart = Time.realtimeSinceStartup;
        Resume();
        while ((!condition() || clock.Time - gameStart < minimumGameSeconds)
            && Time.realtimeSinceStartup - wallStart < 30f)
            yield return null;
        Pause();
        Require(condition() && clock.Time - gameStart >= minimumGameSeconds,
            failure + "; game=" + (clock.Time - gameStart)
            + "; wall=" + (Time.realtimeSinceStartup - wallStart));
    }

    private IEnumerator AdvanceGameSeconds(float seconds)
    {
        float gameStart = clock.Time;
        float wallStart = Time.realtimeSinceStartup;
        Resume();
        while (clock.Time - gameStart < seconds
            && Time.realtimeSinceStartup - wallStart < 20f)
            yield return null;
        Pause();
        Require(clock.Time - gameStart >= seconds,
            "Main clock did not advance by " + seconds + " game seconds.");
    }

    private IEnumerator AdvanceGameFramesAndSeconds(
        int minimumFrames,
        float minimumGameSeconds)
    {
        Require(minimumFrames > 0, "Measured growth window requires frames.");
        Require(minimumGameSeconds > 0f,
            "Measured growth window requires positive game time.");
        int frameStart = clock.FrameCount;
        float gameStart = clock.Time;
        float wallStart = Time.realtimeSinceStartup;
        Resume();
        while ((clock.FrameCount - frameStart < minimumFrames
                || clock.Time - gameStart < minimumGameSeconds)
            && Time.realtimeSinceStartup - wallStart < 30f)
        {
            yield return null;
        }
        Pause();
        Require(clock.FrameCount - frameStart >= minimumFrames
                && clock.Time - gameStart >= minimumGameSeconds,
            "Measured growth window did not advance by "
            + minimumFrames + " frames and " + minimumGameSeconds
            + " game seconds; frames=" + (clock.FrameCount - frameStart)
            + "; game=" + (clock.Time - gameStart)
            + "; wall=" + (Time.realtimeSinceStartup - wallStart));
    }

    private void EnsureIndustryPanelOpen()
    {
        if (FindObjectsByType<Button>(FindObjectsSortMode.None).Any(
                value => value != null
                    && value.gameObject.activeInHierarchy
                    && value.name.StartsWith(
                        "IndustryPowerConnection_",
                        StringComparison.Ordinal)))
        {
            return;
        }
        UITabManager tabs = FindFirstObjectByType<UITabManager>();
        Require(tabs != null, "Main tab manager is missing.");
        tabs.ToggleSelectButton(10);
    }

    private void RebuildIndustryPanel()
    {
        UITabManager tabs = FindFirstObjectByType<UITabManager>();
        Require(tabs != null, "Main tab manager is missing after restore.");
        if (FindObjectsByType<Button>(FindObjectsSortMode.None).Any(
                value => value != null
                    && value.gameObject.activeInHierarchy
                    && value.name.StartsWith(
                        "IndustryPowerConnection_",
                        StringComparison.Ordinal)))
        {
            tabs.ToggleSelectButton(10);
        }
        tabs.ToggleSelectButton(10);
    }

    private static void Click(string name)
    {
        Button button = FindObjectsByType<Button>(FindObjectsSortMode.None)
            .SingleOrDefault(value => value != null
                && value.gameObject.activeInHierarchy
                && value.name == name);
        Require(button != null && button.IsInteractable()
                && EventSystem.current != null,
            "Actual main UI action missing: " + name);
        ExecuteEvents.Execute(button.gameObject,
            new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            }, ExecuteEvents.pointerClickHandler);
    }

    private string RenderCropPanelText()
    {
        GameObject panel = new("Wim016LightCropPanelWitness",
            typeof(RectTransform), typeof(Canvas));
        try
        {
            services.Resolve<ICropPlotBuildingPanelPresenter>().Render(
                panel.transform, PlotBuilding(),
                TMPro.TMP_Settings.defaultFontAsset,
                _ => { }, () => { });
            return string.Join("\n",
                panel.GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .Select(value => value.text));
        }
        finally
        {
            DestroyImmediate(panel);
        }
    }

    private float CellLight()
    {
        Require(field.TryGetCell(PlotBuilding().centerPos, out var cell),
            "Actual plot environmental cell is missing.");
        return cell.LightLevel;
    }

    private bool IsIndoorAmbientLightSettled()
    {
        CropPlotSnapshot plot = Plot();
        float ambient = DungeonStory.Environment
            .EnvironmentalFieldSimulationRules.IndoorAmbientLight;
        float expectedMultiplier = Mathf.Clamp01(
            (ambient - plot.LightStopThreshold)
            / (plot.LightSufficientThreshold - plot.LightStopThreshold));
        return CellLight() <= ambient + .5f
            && Mathf.Abs(plot.LightGrowthMultiplier - expectedMultiplier) <= .02f;
    }

    private bool IsReducedLightEstablished(float maximumGrowthMultiplier)
    {
        CropPlotSnapshot plot = Plot();
        return CellLight() < plot.LightSufficientThreshold
            && plot.LightGrowthMultiplier <= maximumGrowthMultiplier;
    }

    private BuildableObject Resolve(string id) => services
        .Resolve<IBuildingWorldQuery>().Buildings.Single(value => value != null
            && string.Equals(value.PersistentInstanceId.Value, id,
                StringComparison.Ordinal));

    private BuildableObject Generator() => Resolve(generatorId);

    private BuildableObject SunLamp() => Resolve(sunLampId);
    private BuildableObject ArtificialSun() => Resolve(artificialSunId);
    private BuildableObject PlotBuilding() => Resolve(plotId);

    private void SupplyGeneratorFuel(string context)
    {
        BuildableObject generator = Generator();
        string destination = "power:"
            + generator.RequirePersistentInstanceId().Value;
        IWorldItemStackRuntime items = services.Resolve<IWorldItemStackRuntime>();
        Require(items.SpawnItemAt("resource:mana-crystal", 1,
                    generator.centerPos, WorldItemStackState.FacilityBuffer,
                    destination, out int spawned)
                && spawned == 1,
            "Controlled exact physical I03 fuel arrival failed: " + context);
    }

    private void PauseAllCharacterAi()
    {
        ICharacterAiWorldRegistry world =
            services.Resolve<ICharacterAiWorldRegistry>();
        foreach (CharacterActor actor in world.Characters
                     .Where(value => value != null))
        {
            actor.SetAiPaused(true);
        }
    }
    private CropPlotSnapshot Plot() => crops.Plots.Single(
        value => value.PlotId == plotId);
    private CropPlotSaveData SavedPlot() => crops.Capture().plots.Single(
        value => value.buildingInstanceId == plotId);

    private static BuildingSO LoadBuilding(string path)
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
        Require(definition != null, "Missing authored building: " + path);
        return definition;
    }

    private void Pause()
    {
        if (game != null)
            game.isPause = true;
        if (timeScale != null)
            timeScale.Scale = 0f;
    }

    private void Resume()
    {
        game.isPause = false;
        timeScale.Scale = 2f;
    }

    private sealed class LightingFixtureLayout
    {
        public LightingFixtureLayout(
            Vector2Int generatorAnchor,
            Vector2Int sunLampAnchor,
            Vector2Int plotAnchor,
            Vector2Int artificialSunAnchor,
            IReadOnlyList<Vector2Int> ductCells,
            RectInt cropRoomBounds,
            RectInt artificialSunRoomBounds)
        {
            GeneratorAnchor = generatorAnchor;
            SunLampAnchor = sunLampAnchor;
            PlotAnchor = plotAnchor;
            ArtificialSunAnchor = artificialSunAnchor;
            DuctCells = ductCells;
            CropRoomBounds = cropRoomBounds;
            ArtificialSunRoomBounds = artificialSunRoomBounds;
        }

        public Vector2Int GeneratorAnchor { get; }
        public Vector2Int SunLampAnchor { get; }
        public Vector2Int PlotAnchor { get; }
        public Vector2Int ArtificialSunAnchor { get; }
        public IReadOnlyList<Vector2Int> DuctCells { get; }
        public RectInt CropRoomBounds { get; }
        public RectInt ArtificialSunRoomBounds { get; }
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
            throw new InvalidOperationException(failure);
    }
}
#endif
