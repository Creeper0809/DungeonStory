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

// Root-owned integration witness. Research, placement, clock and growth checkpoints
// are controlled setup, not natural crop growth or an autonomous six-adult farm.
public sealed class Wim009CropPestLiveRunner
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-009-crop-pests-live.txt";
    private const string PestId = "seasonal:summer-vermin-bloom";
    private static bool running;
    private readonly List<string> lines = new();
    private DungeonRuntimeLifetimeScope scope;
    private CropPlotRuntime crops;
    private CropDefinitionSO crop;
    private IWorldItemStackRuntime items;
    private IGameEventBus events;
    private ISeasonalEventQuery seasonal;
    private IDungeonGameSaveService saves;
    private IFacilityBufferMassAdmissionService admission;
    private readonly List<FacilityBufferPlannedOutputToken> heldTokens = new();

    public static string StartFocused()
    {
        Require(Application.isPlaying && !running, "A fresh disposable main Play session is required; no duplicate run.");
        var current = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(current?.Container != null, "Main scope has not initialized.");
        ((IDisposable)current.Container.Resolve<IDungeonSaveCommandService>()).Dispose();
        ((IDisposable)current.Container.Resolve<MetaProfilePersistenceService>()).Dispose();
        var host = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        Require(host != null, "Missing actual main coroutine host.");
        host.isPause = true;
        current.Container.Resolve<IGameTimeScaleController>().Scale = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        try { host.StartCoroutine(new Wim009CropPestLiveRunner().Observe()); }
        catch { running = false; throw; }
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Observe()
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(Run());
        Exception failure = null;
        while (stack.Count > 0)
        {
            object value = null;
            bool moved;
            try { moved = stack.Peek().MoveNext(); if (moved) value = stack.Peek().Current; }
            catch (Exception error) { failure = error; break; }
            if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (value is IEnumerator nested) stack.Push(nested); else yield return value;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        if (admission != null)
        {
            foreach (var token in heldTokens.ToArray())
            {
                try { Release(token); }
                catch (Exception error) { failure ??= error; }
            }
        }
        Pause();
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add("scope=actual main content transaction/postcommit dispatcher, registered crop consumer, current save, harvest work/physical output and crop presenter; owner/party UI is real, research/placement/growth/full-buffer are controlled setup; no natural crop growth/AI/six-adult balance/frost/whole-WIM009 claim");
        lines.Add("cleanup=operator stops disposable Play; no scene or user persistence writes");
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
        seasonal = scope.Container.Resolve<ISeasonalEventQuery>();
        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        admission = scope.Container.Resolve<IFacilityBufferMassAdmissionService>();
        var owner = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Missing main owner preparation.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                .TryReconcileNewRunTierZero(out var expansion, out string failure)
                && expansion.CurrentInteriorColumns == 29, "Normal TierZero: " + failure);
            var button = Resources.FindObjectsOfTypeAll<Button>().SingleOrDefault(x => x != null
                && x.gameObject.scene.isLoaded && x.gameObject.activeInHierarchy && x.name == "OwnerOption_1001");
            Require(button != null && button.IsInteractable()
                && PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, Vector2.zero), "Actual owner UI unavailable.");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null, "Actual party UI did not publish the owner.");
        }
        foreach (var actor in scope.Container.Resolve<ICharacterAiWorldRegistry>().Characters.Where(x => x != null))
            actor.SetAiPaused(true);
        Pause();
        // The clock publishes DeltaTime once per frame. A pause issued after the
        // party UI must settle before repeated direct Tick calls are compared.
        yield return null;
        Require(scope.Container.Resolve<IGameClock>().IsPaused
            && scope.Container.Resolve<IGameClock>().DeltaTime == 0f,
            "Paused clock did not settle before the controlled crop witness.");
        var research = scope.Container.Resolve<ProgressionSceneRuntimeReferences>().BlueprintResearch;
        foreach (string id in new[] { "gathering", "field" })
            Require(research.TryCompleteProjectImmediatelyForVerification(new ResearchProjectId("research:agriculture:" + id),
                out string failure), "Research setup: " + failure);
        Require(scope.Container.Resolve<IResourceEconomyContentCatalog>().TryGetCrop("crop:twilight-grain", out crop),
            "Missing authored twilight grain.");
        RunScenario();
    }

    private void RunScenario()
    {
        string growing = PlacePlot(), later = PlacePlot(), mature = PlacePlot();
        Sow(growing); Sow(mature); Mature(mature);
        var started = StartPests(32);
        var occurrence = seasonal.ActiveSeasonalEvents.Single(x => x.definitionId == PestId);
        string firstSource = occurrence.instanceId;
        int expiry = occurrence.deadlineAbsoluteDay + 1;
        Require(Damage(growing) == 10 && Damage(mature) == 10 && Damage(later) == 0,
            "Started event must damage existing growing/mature cycles, not an empty plot.");
        Require(Row(growing).seasonalYieldDamage.sourceEventInstanceId == firstSource
            && Row(mature).seasonalYieldDamage.sourceEventInstanceId == firstSource
            && crops.Capture().handledSeasonalStartInstanceIds.Count(x => x == firstSource) == 1,
            "Start occurrence provenance or exact one handled record is missing.");
        Sow(later);
        string beforeReplay = JsonUtility.ToJson(crops.Capture());
        V20SocietyEventAlertProjection.PublishResolved(events, started, true);
        crops.Tick();
        Require(Damage(later) == 0 && JsonUtility.ToJson(crops.Capture()) == beforeReplay,
            "Repeated start or active-event polling damaged a later sow or changed the first damage.");
        RenderDamage(growing);
        lines.Add("[PASS] actual daily content transaction + postcommit event: growing/mature=10%, empty/later-sown=0%; preview/uncommitted event and duplicate/polling mutate nothing; actual crop presenter shows cost");

        var world = saves.FromJson(saves.ToJson(saves.Capture()));
        var invalid = saves.FromJson(saves.ToJson(world));
        var incoming = DungeonSaveSectionPayload.ReadOrNew<DungeonCropPlotSaveData>(invalid, CropPlotSaveSection.Id);
        incoming.plots.Single(x => x.buildingInstanceId == growing).seasonalYieldDamage.primaryBatchLossPercent = 11;
        DungeonSaveSectionPayload.Write(invalid, CropPlotSaveSection.Id, DungeonCropPlotSaveData.CurrentVersion,
            DungeonSaveRestorePhase.RuntimeState, incoming);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        string beforeCrop = JsonUtility.ToJson(crops.Capture()), beforeItems = JsonUtility.ToJson(items.Capture());
        Require(!saves.TryRestore(invalid, out var rejected) && !rejected.Success
            && rejected.Errors.Any(x => x.IndexOf("seasonal", StringComparison.OrdinalIgnoreCase) >= 0)
            && JsonUtility.ToJson(crops.Capture()) == beforeCrop && JsonUtility.ToJson(items.Capture()) == beforeItems,
            "Invalid seasonal loss was accepted, failed at an unrelated gate, or partially changed live state: " + string.Join(" | ", rejected.Errors));
        Require(saves.TryRestore(world, out var restored) && restored.Success,
            "Current whole-world restore failed: " + string.Join(" | ", restored.Errors));
        Pause();
        Require(Damage(growing) == 10 && Damage(mature) == 10 && Damage(later) == 0
            && Row(growing).seasonalYieldDamage.sourceEventInstanceId == firstSource
            && crops.Capture().handledSeasonalStartInstanceIds.Count(x => x == firstSource) == 1,
            "Whole restore lost cycle provenance or handled-start authority.");
        V20SocietyEventAlertProjection.PublishResolved(events, started, true);
        Require(Damage(later) == 0 && Damage(growing) == 10, "Restored replay hit a later sow.");
        ResolveDay(expiry, 91, Season.Autumn);
        Require(!seasonal.ActiveSeasonalEvents.Any(x => x.instanceId == firstSource)
            && Damage(growing) == 10 && Damage(mature) == 10,
            "Event expiry healed existing cycle damage or left the old occurrence active.");
        lines.Add("[PASS] invalid 11% current whole-world JSON rejected atomically; normal whole restore and exact replay preserve handled IDs/current-cycle provenance; real event expiry does not heal crop damage");

        Mature(growing);
        var held = HoldOutput(growing);
        var frozen = Freeze(growing);
        Require(frozen.seasonalPrimaryBatchLossPercent == 10
            && frozen.seasonalDamageSourceEventInstanceId == firstSource
            && frozen.seasonalDamageSourceDefinitionId == PestId
            && frozen.seasonalPrimaryLossQuantity == frozen.primaryQuantityBeforeSeasonalLoss / 10
            && frozen.harvestQuantity == frozen.primaryQuantityBeforeSeasonalLoss - frozen.primaryQuantityBeforeSeasonalLoss / 10,
            "Frozen primary outcome does not match the independent floor-loss equation/provenance.");
        string frozenJson = JsonUtility.ToJson(frozen);
        var frozenSave = crops.Capture();
        var invalidFrozen = JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(frozenSave));
        invalidFrozen.plots.Single(x => x.buildingInstanceId == growing).pendingHarvest.seasonalPrimaryLossQuantity++;
        string priorFrozenLive = JsonUtility.ToJson(crops.Capture());
        bool denied = false;
        try { crops.BuildRestore(invalidFrozen); }
        catch (InvalidOperationException error)
        {
            denied = error.Message.IndexOf("seasonal", StringComparison.OrdinalIgnoreCase) >= 0
                || error.Message == "Crop harvest frozen owner contradicts plot state or authored maximums.";
            if (!denied) throw;
        }
        Require(denied && JsonUtility.ToJson(crops.Capture()) == priorFrozenLive,
            "Contradictory frozen loss was accepted or changed live crop state.");
        crops.Restore(crops.BuildRestore(JsonUtility.FromJson<DungeonCropPlotSaveData>(JsonUtility.ToJson(frozenSave))));
        crops.Tick();
        Require(JsonUtility.ToJson(Row(growing).pendingHarvest) == frozenJson,
            "Frozen output/current crop restore/retry rerolled or reapplied seasonal loss.");
        RenderDamage(growing);
        Release(held); Finish(growing, frozen);
        Require(Damage(growing) == 0, "Completed harvest did not retire its cycle damage.");
        Sow(growing);
        Require(Damage(growing) == 0, "New paid sow inherited the previous cycle damage.");
        string beforeStaleStart = JsonUtility.ToJson(crops.Capture());
        bool staleRejected = false;
        try { V20SocietyEventAlertProjection.PublishResolved(events, started, true); }
        catch (InvalidOperationException error)
        {
            staleRejected = error.Message.IndexOf("current active occurrence", StringComparison.Ordinal) >= 0;
            if (!staleRejected) throw;
        }
        Require(staleRejected && Damage(growing) == 0
            && JsonUtility.ToJson(crops.Capture()) == beforeStaleStart,
            "Expired old start replay was not rejected without changing the new cycle.");
        lines.Add("[PASS] actual harvest work freezes primary " + frozen.primaryQuantityBeforeSeasonalLoss + "→"
            + frozen.harvestQuantity + "; loss=" + frozen.seasonalPrimaryLossQuantity
            + "; seed=" + frozen.seedQuantity + "; malformed loss rejected, frozen restore/retry exact, released physical primary+seed publish once, next paid cycle clears damage");

        Mature(later);
        var cleanHeld = HoldOutput(later);
        var alreadyFrozen = Freeze(later);
        Require(alreadyFrozen.seasonalPrimaryBatchLossPercent == 0 && alreadyFrozen.seasonalPrimaryLossQuantity == 0
            && alreadyFrozen.primaryQuantityBeforeSeasonalLoss == 0
            && string.IsNullOrEmpty(alreadyFrozen.seasonalDamageSourceEventInstanceId)
            && string.IsNullOrEmpty(alreadyFrozen.seasonalDamageSourceDefinitionId),
            "Unaffected crop froze with a seasonal loss.");
        string cleanJson = JsonUtility.ToJson(alreadyFrozen);
        StartPests(32 + GameCalendarRules.DaysPerYear);
        Require(JsonUtility.ToJson(Row(later).pendingHarvest) == cleanJson && Damage(later) == 0,
            "New event rewrote a previously frozen harvest.");
        Require(Damage(growing) == 10 && Damage(mature) == 10,
            "New event did not affect a new growing cycle or compounded existing damage above its cap.");
        Release(cleanHeld); Finish(later, alreadyFrozen);
        lines.Add("[PASS] second real seasonal occurrence leaves already-frozen output unchanged, applies to a new cycle, caps old unharvested cycle at10%; clean frozen output is physically published exactly once");
    }

    private string PlacePlot()
    {
        Require(scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid), "Missing actual grid.");
        var definition = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/Modular/P23_야외경작지.asset");
        Require(definition != null, "Missing authored P23.");
        Vector2Int? anchor = null;
        foreach (var room in scope.Container.Resolve<IRoomLayoutCache>().GetLayout(grid).Rooms
            .Where(x => x.IsUsable).OrderBy(x => x.Bounds.yMin).ThenBy(x => x.Bounds.xMin))
        {
            foreach (var p in room.Cells.OrderBy(x => x.y).ThenBy(x => x.x))
            {
                if (definition.GetGridPosList(p).All(c => room.ContainsCell(c)
                    && grid.GetGridCell(c) is GridCell cell && cell.CanBuildInArea(definition)
                    && cell.CanOccupy(definition.Placement.Layer))) { anchor = p; break; }
            }
            if (anchor.HasValue) break;
        }
        Require(anchor.HasValue, "No legal P23 footprint; do not expand or overwrite a facility.");
        var plot = scope.Container.Resolve<IGridBuildingObjectFactory>().Create(grid, definition, anchor.Value);
        Require(plot != null, "Actual P23 factory failed.");
        foreach (var component in plot.GetComponentsInChildren<MonoBehaviour>(true)) scope.Container.Inject(component);
        plot.SetGrid(grid); plot.Initialization(definition, anchor.Value);
        Require(grid.RegisterOccupant(plot, definition.Placement.Layer, definition.GetGridPosList(anchor.Value),
            definition.Placement.IsMovement), "Actual grid registration failed.");
        crops.Restore(crops.BuildRestore(crops.Capture()));
        string id = plot.RequirePersistentInstanceId().Value;
        Require(scope.Container.Resolve<IBuildingWorldQuery>().Buildings.Contains(plot), "Unpublished P23 fixture.");
        var environment = scope.Container.Resolve<IEnvironmentalFieldPersistence>();
        var data = environment.Capture();
        data.cells.RemoveAll(x => x.x == plot.centerPos.x && x.y == plot.centerPos.y);
        data.cells.Add(new EnvironmentalCellSaveData { x = plot.centerPos.x, y = plot.centerPos.y,
            temperatureC = 20, airQuality = 100, lightLevel = 100 });
        environment.Restore(environment.PrepareRestore(data));
        return id;
    }

    private void Sow(string id)
    {
        var plot = Plot(id);
        Require(crops.TrySetCrop(plot, crop.CropId, out string failure), failure);
        crops.Tick();
        var row = crops.Plots.Single(x => x.PlotId == id);
        foreach (var input in row.RequiredMaterials)
        {
            int spawned;
            bool success = input.Key == crop.SeedItemId
                ? scope.Container.Resolve<IItemTransferService>().TrySpawnItemWithComponents(input.Key, input.Value,
                    plot.centerPos, WorldItemStackState.FacilityBuffer, row.MaterialDestinationId,
                    new[] { SeedLotItemStateCodec.Encode(new SeedLotState { cropId = crop.CropId,
                        cultivarGenomeId = crop.BaseGenome.GenomeId, generation = 0, pathogenLoad = 0 }) }, out spawned)
                : items.SpawnItemAt(input.Key, input.Value, plot.centerPos, WorldItemStackState.FacilityBuffer,
                    row.MaterialDestinationId, out spawned);
            Require(success && spawned == input.Value, "Physical sow setup failed: " + input.Key);
        }
        crops.Tick();
        Require(crops.TryGetWork(plot, BuiltInWorkTypeIds.Sow, out var work) && work.Available,
            "Sow unavailable: " + work.UnavailableReason);
        Require(crops.ApplyWork(plot, BuiltInWorkTypeIds.Sow, work.RequiredWork, out bool done) && done,
            "Actual sow work failed.");
        Require(Row(id).phase == CropPlotPhase.Growing, "Completed sow did not start its actual cycle.");
    }

    private void Mature(string id)
    {
        var data = crops.Capture();
        data.plots.Single(x => x.buildingInstanceId == id).growthHours = crop.GrowthHours;
        crops.Restore(crops.BuildRestore(data));
        crops.Tick();
        Require(Row(id).phase == CropPlotPhase.ReadyToHarvest, "Controlled growth checkpoint did not yield harvest work.");
    }

    private V20ResolvedEventResult StartPests(int day)
    {
        int seed = -1;
        var catalog = scope.Container.Resolve<V20StoryContentCatalog>();
        for (int candidate = 0; candidate < 128; candidate++)
        {
            var isolated = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
            isolated.EvaluateDaily(new V20DailyEventContext { AbsoluteDay = day, RunSeed = candidate, Season = Season.Summer });
            if (isolated.ActiveSeasonalEvents.Any(x => x.definitionId == PestId)) { seed = candidate; break; }
        }
        Require(seed >= 0, "No deterministic pest event fixture seed found in bounded source search.");
        string before = JsonUtility.ToJson(crops.Capture());
        var results = ResolveDay(day, seed, Season.Summer, publish: false);
        var selected = results.SingleOrDefault(x => x.DefinitionId == PestId && x.ResolutionId == "started");
        Require(selected.DefinitionId == PestId && seasonal.ActiveSeasonalEvents.Count(x => x.definitionId == PestId) == 1,
            "Actual committed seasonal producer did not select the expected pest event.");
        Require(JsonUtility.ToJson(crops.Capture()) == before,
            "Campaign evaluation changed crops before its postcommit notification.");
        events.Publish(new V20ContentEffectsResolvedEvent(selected.DefinitionId, selected.ResolutionId, selected.Effects, false));
        Require(JsonUtility.ToJson(crops.Capture()) == before, "Uncommitted notification changed crop authority.");
        foreach (var resolution in results) V20SocietyEventAlertProjection.PublishResolved(events, resolution, true);
        return selected;
    }

    private IReadOnlyList<V20ResolvedEventResult> ResolveDay(int day, int seed, Season season, bool publish = true)
    {
        var context = new V20DailyEventContext { AbsoluteDay = day, RunSeed = seed, Season = season };
        Require(scope.Container.Resolve<IContentResolutionService>().TryExecute(new ContentResolutionRequest {
                ActionId = "content:daily:" + day, Kind = ContentResolutionRequestKind.DailyEvaluation,
                AbsoluteDay = day, DailyContext = context, Requirements = context.Requirements },
            out var result, out DomainFailure failure), "Actual daily transaction failed: " + failure);
        if (publish)
            foreach (var resolution in result.Resolutions)
                V20SocietyEventAlertProjection.PublishResolved(events, resolution, true);
        return result.Resolutions;
    }

    private void Release(FacilityBufferPlannedOutputToken token)
    {
        Require(admission.TryReleasePlannedOutput(token, FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
            out var code, out string reason), "Competing output reservation release: " + code + ":" + reason);
        heldTokens.Remove(token);
    }

    private FacilityBufferPlannedOutputToken HoldOutput(string id)
    {
        var plot = Plot(id);
        var services = scope.Container;
        var capabilities = services.Resolve<IProductionOutputCapabilityRegistry>();
        var maximumMass = services.Resolve<IProductionOutputMaximumMassRegistry>();
        var facility = services.Resolve<IProductionFacilityHandleQuery>().CaptureFacility(plot);
        var proof = new ProductionOutputBatchMaximumMassProof(new[] {
            maximumMass.CaptureDeclared(capabilities.CaptureDeclaredDescriptor(
                CropHarvestOutputMaximumAuthority.HarvestOutputLineId(crop.CropId), crop.HarvestItemId,
                ProductionOutputCapabilityIds.StandardDefinition),
                CropHarvestOutputMaximumAuthority.ResolveMaximumHarvestQuantity(crop, false,
                    services.Resolve<ICharacterPerformanceDefinitionMaximumQuery>())),
            maximumMass.CaptureDeclared(capabilities.CaptureDeclaredDescriptor(
                CropHarvestOutputMaximumAuthority.SeedOutputLineId(crop.CropId), crop.SeedItemId,
                ProductionOutputCapabilityIds.CropHarvestSeedLot),
                CropHarvestOutputMaximumAuthority.ResolveMaximumReturnedSeedQuantity(
                    services.Resolve<IGameplayEffectResultBoundsQuery>())) });
        var source = services.Resolve<IProductionOutputBufferCapacityProjector>().CaptureSource(facility, proof);
        Require(services.Resolve<IProductionOutputDestinationAuthorityRuntime>().TryEnsure(facility,
            source.RequiredMinimumCapacityGrams, out var profile, out string failure), "Output authority: " + failure);
        string destination = ProductionOutputDestinationId.FromFacility(plot.RequirePersistentInstanceId()).Value;
        Require(admission.TryGetCapacity(destination, plot.centerPos, out var capacity)
            && capacity.ReservedMassGrams == 0, "Output destination is missing or unexpectedly reserved.");
        Require(services.Resolve<IResourceEconomyContentCatalog>().TryGetItem(crop.HarvestItemId, out var item),
            "Missing physical harvest item.");
        long unitGrams = Mathf.RoundToInt(item.UnitWeight * 1000);
        Require(unitGrams > 0, "Nonpositive harvest mass.");
        int quantity = checked((int)(profile.MaxMassGrams / unitGrams));
        Require(quantity > 0, "Cannot saturate the output with an exact physical mass vector.");
        var request = new FacilityBufferPlannedOutputRequest("qa:wim009:hold:" + id,
            "production-output-batch:qa:wim009:" + id, new string('a', 64), destination, plot.centerPos,
            profile.OwnerDomain, profile.OwnerOperationId, profile.OwnerFacilityId, profile.CapacityRevision,
            new[] { new FacilityBufferPlannedOutputSlice("qa:wim009:fill:" + crop.CropId,
                PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)crop.HarvestItemId), quantity) },
            source.SourceDigest, source.RequiredMinimumCapacityGrams);
        Require(admission.TryReservePlannedOutput(request, out var token, out var code, out string reason),
            "Cannot prepare competing output capacity: " + code + ":" + reason);
        heldTokens.Add(token);
        return token;
    }

    private CropHarvestOutputSaveData Freeze(string id)
    {
        var plot = Plot(id);
        Require(crops.TryGetWork(plot, BuiltInWorkTypeIds.Harvest, out var work) && work.Available,
            "Harvest unavailable: " + work.UnavailableReason);
        int beforePrimary = Count(crop.HarvestItemId), beforeSeed = Count(crop.SeedItemId);
        Require(crops.ApplyWork(plot, BuiltInWorkTypeIds.Harvest, work.RequiredWork, out bool completed) && !completed,
            "Capacity-blocked actual harvest did not freeze its output for retry.");
        var pending = Row(id).pendingHarvest;
        Require(pending.phase == CropHarvestOutputPhase.Frozen && pending.outputPublication.IsEmpty
            && Count(crop.HarvestItemId) == beforePrimary && Count(crop.SeedItemId) == beforeSeed,
            "Freeze leaked partial physical output or failed to retain a frozen result.");
        return pending;
    }

    private void Finish(string id, CropHarvestOutputSaveData expected)
    {
        int beforePrimary = Count(crop.HarvestItemId), beforeSeed = Count(crop.SeedItemId);
        Require(crops.ApplyWork(Plot(id), BuiltInWorkTypeIds.Harvest, crop.HarvestWork, out bool completed) && completed,
            "Released frozen output did not complete.");
        Require(Count(crop.HarvestItemId) == beforePrimary + expected.harvestQuantity
            && Count(crop.SeedItemId) == beforeSeed + expected.seedQuantity,
            "Physical primary/seed quantities do not match the one frozen outcome.");
        Require(Row(id).pendingHarvest.phase == CropHarvestOutputPhase.None
            && Row(id).nextHarvestOperationSequence == expected.operationSequence + 1,
            "Output operation was not retired exactly once.");
    }

    private void RenderDamage(string id)
    {
        string before = JsonUtility.ToJson(crops.Capture());
        var panel = new GameObject("Wim009_Crop_Pest_Panel", typeof(RectTransform), typeof(Canvas));
        try
        {
            scope.Container.Resolve<ICropPlotBuildingPanelPresenter>().Render(panel.transform, Plot(id),
                TMPro.TMP_Settings.defaultFontAsset, _ => { }, () => { });
            string text = string.Join("\n", panel.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(x => x.text));
            Require(text.Contains("해충") && text.Contains("10%"), "Player crop presenter omits the current-cycle pest cost: " + text);
            Require(JsonUtility.ToJson(crops.Capture()) == before, "Read-only presenter mutated crop state.");
        }
        finally { UnityEngine.Object.DestroyImmediate(panel); }
    }

    private BuildableObject Plot(string id) => scope.Container.Resolve<IBuildingWorldQuery>().Buildings
        .Single(x => x != null && x.RequirePersistentInstanceId().Value == id);
    private CropPlotSaveData Row(string id) => crops.Capture().plots.Single(x => x.buildingInstanceId == id);
    private int Damage(string id) => Row(id).seasonalYieldDamage.primaryBatchLossPercent;
    private int Count(string itemId) => items.GetAllStacks().Where(x => x.ItemId == itemId).Sum(x => x.Quantity);
    private void Pause()
    {
        if (scope?.Container == null) return;
        var game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        if (game != null) game.isPause = true;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
