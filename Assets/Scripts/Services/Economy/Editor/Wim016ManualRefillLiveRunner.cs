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
using static UnityEngine.Object;

// Root-owned important integration witness. The only direct crop work is the
// documented sow precondition; normal-mode refill work comes from real staff AI.
// Recovery-only mode explicitly prepares current-format interrupted checkpoints.
public sealed class Wim016ManualRefillLiveRunner
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-016-manual-refill-live.txt";
    public const string RecoveryReportPath = "Artifacts/QA/wim-implementation/wim-016-committed-refill-restore.txt";
    public const string CancellationReportPath = "Artifacts/QA/wim-implementation/wim-016-refill-cancellation.txt";
    private const string WaterId = "resource:clean-water";
    private readonly List<string> lines = new();
    private static bool running;
    private DungeonRuntimeLifetimeScope scope;
    private CropPlotRuntime crops;
    private IWorldItemStackRuntime items;
    private ICharacterAiWorldRegistry world;
    private IGameClock clock;
    private IGameTimeScaleController timeScale;
    private string plotId;
    private string workerId;
    private bool recoveryOnly;
    private bool cancellationOnly;
    private string OutputPath => cancellationOnly
        ? CancellationReportPath
        : recoveryOnly ? RecoveryReportPath : ReportPath;
    public static bool IsRunning => running;

    public static string StartFocused() => Start(false, false);
    public static string StartCommittedRecoveryFocused() => Start(true, false);
    public static string StartCancellationFocused() => Start(false, true);

    private static string Start(bool recoveryOnly, bool cancellationOnly)
    {
        Require(Application.isPlaying && !running, "Start once in fresh disposable main Play.");
        var scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main runtime is not initialized.");
        var commands = scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(commands != null, "Cannot protect user save files.");
        commands.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        var game = FindFirstObjectByType<GameManager>();
        Require(game != null, "Missing actual main coroutine host.");
        game.isPause = true;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
        var runner = new Wim016ManualRefillLiveRunner
        {
            recoveryOnly = recoveryOnly,
            cancellationOnly = cancellationOnly
        };
        Directory.CreateDirectory(Path.GetDirectoryName(runner.OutputPath));
        File.WriteAllText(runner.OutputPath, "result=RUNNING\n");
        running = true;
        try { game.StartCoroutine(runner.Observe()); }
        catch { running = false; throw; }
        return "RUNNING " + runner.OutputPath;
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
            if (value is IEnumerator nested) stack.Push(nested);
            else yield return value;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        Pause();
        if (failure != null && crops != null && plotId != null)
        {
            try { lines.Add("failure-state=" + Row().WaterRefillPhase + "; water=" + Row().CurrentWater
                + "; delivered=" + Row().WaterRefillDeliveredQuantity + "; work=" + Row().WaterRefillCompletedWork
                + "; reason=" + Row().WaterRefillFailureReason); lines.Add(WorkerDiagnostic()); }
            catch (Exception diagnostic) { lines.Add("diagnostic=" + diagnostic.Message); }
        }
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add(cancellationOnly
            ? "scope=actual owner/party UI, registered P23, physical FacilityBuffer water, partial Treat, production demolition and registered whole-world restore; research/building/sow/environment/dry checkpoint and delivered input are controlled setup; no natural AI delivery, full crop cycle or six-adult balance claim"
            : recoveryOnly
                ? "scope=current registered whole-world restore and crop recovery/physical ACK; actual typed WIP transfer backs controlled InputCommitted/OutcomePublished checkpoints; no naturally interrupted callback, AI delivery, full crop cycle or six-adult balance claim"
                : "scope=actual owner/party UI, registered P23, actual main clock/AI haul/Treat and current-world JSON restore; research/building/sow/healthy staff/typed action preference/environment/dry checkpoint are controlled setup; no natural priority, full crop cycle, committed-ACK fault injection or six-adult balance claim");
        lines.Add("cleanup=operator stops disposable Play; no scene or user persistence writes");
        File.WriteAllLines(OutputPath, lines);
        Debug.Log(string.Join("\n", lines));
        running = false;
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        clock = scope.Container.Resolve<IGameClock>();
        crops = scope.Container.Resolve<CropPlotRuntime>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        var owner = FindFirstObjectByType<OwnerRunManager>();
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
            Require(owner.CurrentOwnerActor != null, "Actual party UI failed to publish owner.");
        }
        Require(world.TryGetGrid(out Grid grid) && grid != null, "Missing main grid.");
        var worker = world.Characters.Where(x => x != null && !x.IsOwner && !x.IsDead
            && x.characterType == CharacterType.NPC && x.Brain != null
            && x.GetComponent<AbilityHaul>() != null && x.GetComponent<AbilityWork>() != null)
            .OrderBy(x => x.Identity.PersistentId, StringComparer.Ordinal).FirstOrDefault();
        Require(worker != null, "No actual staff with haul and work capabilities.");
        workerId = worker.Identity.PersistentId;
        foreach (var actor in world.Characters.Where(x => x != null)) actor.SetAiPaused(true);
        foreach (var water in items.GetAllStacks().Where(x => x.ItemId == WaterId && x.Quantity > 0).ToArray())
            Require(items.SetForbidden(water.StackId, true), "Cannot hold unrelated water during disposable setup.");
        var research = scope.Container.Resolve<ProgressionSceneRuntimeReferences>().BlueprintResearch;
        foreach (string id in new[] { "gathering", "field" })
            Require(research.TryCompleteProjectImmediatelyForVerification(new ResearchProjectId("research:agriculture:" + id),
                out string failure), "Authored research preparation: " + failure);
        var plot = PlacePlot(grid, worker);
        plotId = plot.RequirePersistentInstanceId().Value;
        var catalog = scope.Container.Resolve<IResourceEconomyContentCatalog>();
        Require(catalog.TryGetCrop("crop:twilight-grain", out CropDefinitionSO crop), "Missing authored grain.");
        crops.Restore(crops.BuildRestore(crops.Capture()));
        Require(crops.TrySetCrop(plot, crop.CropId, out string selected), selected);
        crops.Tick(); // Paused preparation; never manually tick while the main clock runs.
        Require(Row().RequiredMaterials.TryGetValue(WaterId, out int initialWater) && initialWater == 1,
            "Initial sow must request exactly one physical water, not full-cycle prepayment.");
        foreach (var input in Row().RequiredMaterials)
        {
            int spawned;
            bool success = input.Key == crop.SeedItemId
                ? scope.Container.Resolve<IItemTransferService>().TrySpawnItemWithComponents(input.Key, input.Value,
                    plot.centerPos, WorldItemStackState.FacilityBuffer, Row().MaterialDestinationId,
                    new[] { SeedLotItemStateCodec.Encode(new SeedLotState {
                        cropId = crop.CropId, cultivarGenomeId = crop.BaseGenome.GenomeId, generation = 0, pathogenLoad = 0 }) }, out spawned)
                : items.SpawnItemAt(input.Key, input.Value, plot.centerPos, WorldItemStackState.FacilityBuffer,
                    Row().MaterialDestinationId, out spawned);
            Require(success && spawned == input.Value, "Physical sow setup failed: " + input.Key);
        }
        crops.Tick();
        Require(crops.TryGetWork(plot, BuiltInWorkTypeIds.Sow, out var sow) && sow.Available,
            "Prepared sow unavailable: " + sow.UnavailableReason);
        Require(crops.ApplyWork(plot, BuiltInWorkTypeIds.Sow, sow.RequiredWork, out bool sown) && sown,
            "Controlled sow precondition did not complete.");
        Require(Row().Phase == CropPlotPhase.Growing && Row().CurrentWater == 1, "Initial water is not exactly one.");
        PrepareGrowingEnvironment(plot);
        if (cancellationOnly)
        {
            // Owner/start-party setup may restore the normal game speed after
            // Start() established the fixture boundary. Reassert the paused
            // authority immediately before the deterministic cancellation
            // transaction rather than relying on an earlier UI state.
            Pause();
            yield return null;
            yield return VerifyCancellation(plot);
            yield break;
        }
        if (recoveryOnly)
        {
            yield return null; // Observe the paused clock before deterministic recovery ticks.
            VerifyCommittedRecovery();
            yield break;
        }
        int waterBeforeRefill = WaterQuantity();
        float wetGrowth = SavedPlot().growthHours;
        yield return AdvanceGameSeconds(.6f);
        Require(Row().CurrentWater < 1 && Row().CurrentWater > 0 && SavedPlot().growthHours > wetGrowth,
            "Actual clock did not deplete water and advance a watered, lit crop.");
        lines.Add("[PASS] actual-clock wet growth/depletion; water=" + Row().CurrentWater + "; growthHours=" + SavedPlot().growthHours);

        var dry = crops.Capture();
        dry.plots.Single(x => x.buildingInstanceId == plotId).currentWater = 0;
        // Preserve any already-created manual owner; do not release or reset its lease.
        crops.Restore(crops.BuildRestore(dry));
        float dryGrowth = SavedPlot().growthHours;
        yield return AdvanceGameSeconds(.6f);
        Require(Row().CurrentWater == 0 && SavedPlot().growthHours == dryGrowth
            && Row().WaterRefillPhase == CropWaterRefillPhase.WaitingForDelivery
            && Row().WaterRefillRequiredWork == 1 && WaterQuantity() == waterBeforeRefill,
            "Dry crop must stop and own one 1-WU delivery request without virtual water.");
        string destination = Row().WaterRefillDestinationId;
        Require(!string.IsNullOrEmpty(destination), "Missing persistent refill destination.");
        string paused = JsonUtility.ToJson(crops.Capture());
        RenderWaterUi(plot, "물 운반 0/1");
        Require(JsonUtility.ToJson(crops.Capture()) == paused, "Actual presenter mutated crop ownership.");
        Vector2Int source = SelectSource(grid, worker, plot.centerPos);
        Require(items.SpawnItemAt(WaterId, 1, source, WorldItemStackState.Loose, string.Empty, out int added) && added == 1,
            "Could not publish the one physical refill source.");
        int totalWithSource = WaterQuantity();
        Require(totalWithSource == waterBeforeRefill + 1, "Source preparation changed unrelated water.");
        ConfigureWorker(false);
        Resume();
        float wall = Time.realtimeSinceStartup, game = clock.Time;
        bool carried = false;
        while (Row().WaterRefillDeliveredQuantity < 1 && Time.realtimeSinceStartup - wall < 60 && clock.Time - game < 160)
        {
            carried |= items.GetAllStacks().Any(x => x.ItemId == WaterId && x.State == WorldItemStackState.Carried
                && x.DestinationId == workerId) && items.CaptureHaulDeliveryIntentsByDestination(destination).Count > 0;
            yield return null;
        }
        Pause();
        Require(carried && Row().WaterRefillDeliveredQuantity == 1 && Row().CurrentWater == 0
            && Row().WaterRefillCompletedWork == 0 && WaterQuantity() == totalWithSource,
            "Actual separated pickup/carry/delivery or zero-work/zero-recovery boundary failed. " + WorkerDiagnostic());
        Require(items.GetAllStacks().Where(x => x.ItemId == WaterId && x.State == WorldItemStackState.FacilityBuffer
            && x.DestinationId == destination).Sum(x => x.Quantity) == 1, "Delivery is not the exact physical facility buffer.");
        lines.Add("[PASS] actual AI pickup/carry/refill intent/buffer1; source=" + source + "; destination=" + plot.centerPos
            + "; waterRecovery=0; WU=0; gameSeconds=" + (clock.Time - game) + "; wallSeconds=" + (Time.realtimeSinceStartup - wall));
        // Let the current haul close normally; never clear carried ownership to force work.
        Resume(); wall = Time.realtimeSinceStartup;
        // Physical arrival can be visible before the crop scheduler's next
        // publication. Wait for the actual work-ready owner, not just stock.
        while ((Worker().GetComponent<AbilityHaul>().IsHauling
            || Row().WaterRefillPhase != CropWaterRefillPhase.ReadyForWork)
            && Time.realtimeSinceStartup - wall < 10) yield return null;
        Pause();
        Require(!Worker().GetComponent<AbilityHaul>().IsHauling
            && Row().WaterRefillPhase == CropWaterRefillPhase.ReadyForWork,
            "Delivered haul or normal crop work-ready publication did not finish.");
        ConfigureWorker(true);
        Resume(); wall = Time.realtimeSinceStartup; game = clock.Time;
        while (Row().WaterRefillCompletedWork <= 0 && Time.realtimeSinceStartup - wall < 60 && clock.Time - game < 160)
            yield return null;
        Pause();
        var partial = Row();
        Require(partial.WaterRefillPhase == CropWaterRefillPhase.Working && partial.WaterRefillCompletedWork > 0
            && partial.WaterRefillCompletedWork < 1 && partial.CurrentWater == 0
            && WaterQuantity() == totalWithSource, "Actual Treat did not expose unconsumed partial work. " + WorkerDiagnostic());
        RenderWaterUi(Plot(), "직원 작업");
        float completedBeforeRestore = partial.WaterRefillCompletedWork;
        int sequence = SavedPlot().nextWaterRefillOperationSequence;
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        var pending = saves.FromJson(saves.ToJson(saves.Capture()));
        var invalid = saves.FromJson(saves.ToJson(pending));
        var incoming = DungeonSaveSectionPayload.ReadOrNew<DungeonCropPlotSaveData>(invalid, CropPlotSaveSection.Id);
        incoming.plots.Single(x => x.buildingInstanceId == plotId).currentWater = -1;
        DungeonSaveSectionPayload.Write(invalid, CropPlotSaveSection.Id, DungeonCropPlotSaveData.CurrentVersion,
            DungeonSaveRestorePhase.RuntimeState, incoming);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        string cropBefore = JsonUtility.ToJson(crops.Capture()), physicalBefore = JsonUtility.ToJson(items.Capture());
        Require(!saves.TryRestore(invalid, out var rejected) && !rejected.Success
            && rejected.Errors.Any(x => x.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0)
            && JsonUtility.ToJson(crops.Capture()) == cropBefore && JsonUtility.ToJson(items.Capture()) == physicalBefore,
            "Invalid incoming water was accepted or changed live crop/physical state: " + string.Join(" | ", rejected.Errors));
        Require(saves.TryRestore(pending, out var restored) && restored.Success,
            "Actual pending whole-world restore failed: " + string.Join(" | ", restored.Errors));
        Pause(); yield return null;
        Require(Row().WaterRefillPhase == CropWaterRefillPhase.Working
            && Row().WaterRefillCompletedWork == completedBeforeRestore && Row().CurrentWater == 0
            && Row().WaterRefillDestinationId == destination && Row().WaterRefillDeliveredQuantity == 1
            && WaterQuantity() == totalWithSource, "Pending restore changed work, owner, water or physical buffer.");
        lines.Add("[PASS] actual partial Treat=" + completedBeforeRestore + "/1WU; invalid water restore rejected atomically; current whole-world JSON restore preserves work/owner/buffer and zero recovery");
        ConfigureWorker(true);
        Resume(); wall = Time.realtimeSinceStartup; game = clock.Time;
        while (SavedPlot().nextWaterRefillOperationSequence == sequence && Time.realtimeSinceStartup - wall < 60
            && clock.Time - game < 160) yield return null;
        Pause();
        Require(SavedPlot().nextWaterRefillOperationSequence == sequence + 1
            && Row().WaterRefillPhase == CropWaterRefillPhase.None && Row().CurrentWater > .99f && Row().CurrentWater <= 1
            && WaterQuantity() == totalWithSource - 1, "Restored actual Treat did not consume/publish/ACK once. " + WorkerDiagnostic());
        float terminalWater = Row().CurrentWater;
        var terminal = saves.FromJson(saves.ToJson(saves.Capture()));
        Require(saves.TryRestore(terminal, out var terminalRestore) && terminalRestore.Success,
            "Terminal world restore failed: " + string.Join(" | ", terminalRestore.Errors));
        Pause(); yield return null;
        Require(Row().CurrentWater == terminalWater && Row().WaterRefillPhase == CropWaterRefillPhase.None
            && SavedPlot().nextWaterRefillOperationSequence == sequence + 1 && WaterQuantity() == totalWithSource - 1,
            "Terminal restore repeated water publication or consumption.");
        foreach (var actor in world.Characters.Where(x => x != null)) actor.SetAiPaused(true);
        float growthAfter = SavedPlot().growthHours;
        yield return AdvanceGameSeconds(.6f);
        Require(SavedPlot().growthHours > growthAfter && Row().CurrentWater < terminalWater
            && WaterQuantity() == totalWithSource - 1, "Refilled crop did not resume growth/depletion without duplicate consumption.");
        lines.Add("[PASS] actual remaining Treat completes/ACKs once; water+1, physical-1; terminal full restore exact; actual-clock growth resumes and water depletes");
    }

    private void VerifyCommittedRecovery()
    {
        Require(clock.IsPaused && clock.DeltaTime == 0f,
            "Recovery fixture needs a genuinely paused, zero-delta main clock.");
        var dry = crops.Capture();
        dry.plots.Single(x => x.buildingInstanceId == plotId).currentWater = 0;
        crops.Restore(crops.BuildRestore(dry));
        crops.Tick();
        string destination = Row().WaterRefillDestinationId;
        Require(Row().WaterRefillPhase == CropWaterRefillPhase.WaitingForDelivery,
            "Dry checkpoint did not create a real refill destination.");
        Require(items.SpawnItemAt(WaterId, 1, Plot().centerPos,
                WorldItemStackState.FacilityBuffer, destination, out int spawned) && spawned == 1,
            "Controlled physical refill input was not admitted to its real buffer.");
        crops.Tick();
        Require(crops.ApplyWork(Plot(), BuiltInWorkTypeIds.Treat, .5f, out bool completed) && !completed,
            "Controlled half-work setup did not retain the unconsumed refill owner.");
        var owner = SavedPlot().waterRefill;
        Require(owner.phase == CropWaterRefillPhase.Working && owner.requiredWork == 1f
            && Row().CurrentWater == 0, "Unexpected authored refill checkpoint contract.");
        int quantityBefore = WaterQuantity();
        var gateway = scope.Container.Resolve<IProductionItemGateway>();
        Require(gateway.ConsumeDeliveredToWip(destination,
                new Dictionary<string, int>(StringComparer.Ordinal) { [WaterId] = 1 },
                owner.operationId, out ProductionWipInputReceipt receipt, out string failure), failure);
        Require(receipt.IsCommitted && receipt.Quantity == 1 && receipt.InputMassGrams > 0
            && receipt.SourceStackIds != null && receipt.SourceStackIds.Count > 0
            && WaterQuantity() == quantityBefore - 1,
            "Recovery checkpoint has no exact real physical transfer receipt.");

        // Only the crop owner/phase is a controlled interrupted-checkpoint input.
        // The physical receipt, source IDs and mass all come from the real transfer above.
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        var committed = saves.FromJson(saves.ToJson(saves.Capture()));
        var payload = DungeonSaveSectionPayload.ReadOrNew<DungeonCropPlotSaveData>(committed, CropPlotSaveSection.Id);
        var saved = payload.plots.Single(x => x.buildingInstanceId == plotId);
        int sequence = saved.nextWaterRefillOperationSequence;
        owner = saved.waterRefill;
        owner.completedWork = owner.requiredWork;
        owner.phase = CropWaterRefillPhase.InputCommitted;
        owner.commitId = receipt.CommitId;
        owner.inputQuantity = receipt.Quantity;
        owner.inputMassGrams = receipt.InputMassGrams;
        owner.physicalRequestFingerprint = receipt.PhysicalRequestFingerprint;
        owner.sourceStackIds = receipt.SourceStackIds.OrderBy(x => x, StringComparer.Ordinal).ToList();
        owner.failureReason = string.Empty;
        saved.currentWater = 0;
        WriteRecoveryCrop(committed, payload);

        foreach (var phase in new[] { CropWaterRefillPhase.InputCommitted, CropWaterRefillPhase.OutcomePublished })
        {
            var checkpoint = saves.FromJson(saves.ToJson(committed));
            payload = DungeonSaveSectionPayload.ReadOrNew<DungeonCropPlotSaveData>(checkpoint, CropPlotSaveSection.Id);
            saved = payload.plots.Single(x => x.buildingInstanceId == plotId);
            saved.waterRefill.phase = phase;
            saved.currentWater = phase == CropWaterRefillPhase.OutcomePublished ? 1 : 0;
            WriteRecoveryCrop(checkpoint, payload);
            Require(saves.TryRestore(checkpoint, out var restored) && restored.Success,
                phase + " restore rejected valid physical ownership: " + string.Join(" | ", restored.Errors));
            Require(Row().WaterRefillPhase == phase && Row().CurrentWater == saved.currentWater
                && WaterQuantity() == quantityBefore - 1
                && items.TryGetPendingBatchPhysicalDisposition(owner.operationId, out var pendingReceipt)
                && pendingReceipt.CommitId == receipt.CommitId,
                phase + " changed frozen outcome or pending input before recovery.");

            var malformed = saves.FromJson(saves.ToJson(checkpoint));
            var badPayload = DungeonSaveSectionPayload.ReadOrNew<DungeonCropPlotSaveData>(malformed, CropPlotSaveSection.Id);
            badPayload.plots.Single(x => x.buildingInstanceId == plotId).waterRefill.inputMassGrams++;
            WriteRecoveryCrop(malformed, badPayload);
            string cropsBefore = JsonUtility.ToJson(crops.Capture());
            string physicalBefore = JsonUtility.ToJson(items.Capture());
            int revision = scope.Container.Resolve<DungeonRuntimeAggregateRootStore>().PublishedRestoreRevision;
            Require(!saves.TryRestore(malformed, out var rejected) && !rejected.Success
                && JsonUtility.ToJson(crops.Capture()) == cropsBefore
                && JsonUtility.ToJson(items.Capture()) == physicalBefore
                && scope.Container.Resolve<DungeonRuntimeAggregateRootStore>().PublishedRestoreRevision == revision,
                phase + " malformed receipt was accepted or partially published.");

            crops.Tick(); // Actual recovery consumer, not ApplyWork or manual ACK.
            Require(Row().CurrentWater == 1 && Row().WaterRefillPhase == CropWaterRefillPhase.None
                && SavedPlot().nextWaterRefillOperationSequence == sequence + 1
                && WaterQuantity() == quantityBefore - 1
                && !items.TryGetPendingBatchPhysicalDisposition(owner.operationId, out _),
                phase + " did not publish/retire/ACK exactly once.");
            var terminal = saves.FromJson(saves.ToJson(saves.Capture()));
            Require(saves.TryRestore(terminal, out var terminalRestore) && terminalRestore.Success,
                phase + " terminal restore failed: " + string.Join(" | ", terminalRestore.Errors));
            crops.Tick();
            Require(Row().CurrentWater == 1 && Row().WaterRefillPhase == CropWaterRefillPhase.None
                && SavedPlot().nextWaterRefillOperationSequence == sequence + 1
                && WaterQuantity() == quantityBefore - 1
                && !items.TryGetPendingBatchPhysicalDisposition(owner.operationId, out _),
                phase + " terminal retry duplicated water, work or physical consumption.");
            lines.Add("[PASS] " + phase + ": whole-world JSON join; altered mass atomic rejection; water=1; consumed=1; sequence+1; pending receipt ACK; terminal restore/retry unchanged");
        }
    }

    private IEnumerator VerifyCancellation(BuildableObject plot)
    {
        Require(clock.IsPaused && clock.DeltaTime == 0f,
            "Cancellation fixture needs a genuinely paused, zero-delta main clock.");
        Vector2Int plotPosition = plot.centerPos;
        var dry = crops.Capture();
        dry.plots.Single(x => x.buildingInstanceId == plotId).currentWater = 0;
        crops.Restore(crops.BuildRestore(dry));
        crops.Tick();
        CropPlotSnapshot waiting = Row();
        string destination = waiting.WaterRefillDestinationId;
        Require(waiting.WaterRefillPhase == CropWaterRefillPhase.WaitingForDelivery
            && waiting.CurrentWater == 0 && destination.Length > 0,
            "Dry checkpoint did not create the real uncommitted refill owner.");

        IFacilityBufferDestinationClaimQuery claims =
            scope.Container.Resolve<IFacilityBufferDestinationClaimQuery>();
        IFacilityBufferMassCapacityQuery capacities =
            scope.Container.Resolve<IFacilityBufferMassCapacityQuery>();
        Require(claims.TryGetClaim(destination, plotPosition, out var claim)
            && capacities.TryGetCapacity(destination, plotPosition, out var capacity)
            && string.Equals(claim.OwnerDomain,
                CropPlotInputOwnerAuthority.OwnerDomain,
                StringComparison.Ordinal)
            && capacity.Profile.MaxMassGrams > 0,
            "The refill owner did not publish its exact claim and gram capacity.");

        int quantityBefore = WaterQuantity();
        Require(items.SpawnItemAt(WaterId, 1, plotPosition,
                WorldItemStackState.FacilityBuffer, destination, out int spawned)
            && spawned == 1,
            "Controlled physical refill input was not admitted to its real buffer.");
        crops.Tick();
        Require(Row().WaterRefillPhase == CropWaterRefillPhase.ReadyForWork
            && Row().WaterRefillDeliveredQuantity == 1
            && crops.ApplyWork(plot, BuiltInWorkTypeIds.Treat, .5f,
                out bool completed) && !completed
            && Row().WaterRefillPhase == CropWaterRefillPhase.Working
            && Row().WaterRefillCompletedWork > 0
            && Row().WaterRefillCompletedWork < Row().WaterRefillRequiredWork
            && Row().CurrentWater == 0
            && WaterQuantity() == quantityBefore + 1,
            "The physical refill did not reach the uncommitted partial-work boundary.");
        lines.Add("[PASS] uncommitted refill owns exact claim/capacity and one physical buffer water at partial Treat");

        IDungeonGridBuildingControllerProvider controllers =
            scope.Container.Resolve<IDungeonGridBuildingControllerProvider>();
        Require(controllers.Controller.TryDestroyBuilding(plot, out string destroyMessage),
            "Production demolition rejected the uncommitted refill: " + destroyMessage);
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline
            && (scope.Container.Resolve<IBuildingWorldQuery>().Buildings.Contains(plot)
                || crops.Plots.Any(x => x.PlotId == plotId)))
        {
            crops.Tick();
            yield return null;
        }
        crops.Tick();

        Require(!scope.Container.Resolve<IBuildingWorldQuery>().Buildings.Contains(plot)
            && !crops.Plots.Any(x => x.PlotId == plotId),
            "Demolished crop plot or its runtime owner remained published.");
        Require(!claims.TryGetClaim(destination, plotPosition, out _)
            && !capacities.TryGetCapacity(destination, plotPosition, out _)
            && items.CaptureHaulDeliveryIntentsByDestination(destination).Count == 0,
            "Terminal refill destination retained a claim, capacity or haul intent.");
        WorldItemStackSnapshot[] released = items.GetAllStacks()
            .Where(x => x.ItemId == WaterId && x.Quantity > 0
                && x.Position == plotPosition)
            .ToArray();
        Require(WaterQuantity() == quantityBefore + 1
            && released.Sum(x => x.Quantity) == 1
            && released.All(x => x.State == WorldItemStackState.Loose
                && string.IsNullOrEmpty(x.DestinationId)),
            "Demolition deleted, duplicated or teleported the delivered water.");
        lines.Add("[PASS] production demolition retires claim/capacity/intent; deposited water becomes one Loose stack at the former plot cell");

        IDungeonGameSaveService saves = scope.Container.Resolve<IDungeonGameSaveService>();
        DungeonGameSaveData terminal = saves.FromJson(saves.ToJson(saves.Capture()));
        Require(saves.TryRestore(terminal, out var restored) && restored.Success,
            "Terminal cancellation whole-world restore failed: "
            + string.Join(" | ", restored.Errors));
        crops.Tick();
        Require(!crops.Plots.Any(x => x.PlotId == plotId)
            && !claims.TryGetClaim(destination, plotPosition, out _)
            && !capacities.TryGetCapacity(destination, plotPosition, out _)
            && items.CaptureHaulDeliveryIntentsByDestination(destination).Count == 0
            && WaterQuantity() == quantityBefore + 1
            && items.GetAllStacks().Where(x => x.ItemId == WaterId
                    && x.Position == plotPosition)
                .Sum(x => x.Quantity) == 1,
            "Terminal restore resurrected the refill owner or changed physical water.");
        lines.Add("[PASS] current registered whole-world restore keeps cancellation terminal and exact physical water without reroute or duplication");
    }

    private static void WriteRecoveryCrop(DungeonGameSaveData checkpoint, DungeonCropPlotSaveData payload)
    {
        DungeonSaveSectionPayload.Write(checkpoint, CropPlotSaveSection.Id, DungeonCropPlotSaveData.CurrentVersion,
            DungeonSaveRestorePhase.RuntimeState, payload);
        checkpoint.manifest = DungeonSaveManifest.Capture(checkpoint.sections);
    }

    private BuildableObject PlacePlot(Grid grid, CharacterActor worker)
    {
        var definition = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/Modular/P23_야외경작지.asset");
        Require(definition != null, "Missing actual P23 definition.");
        Require(scope.Container.Resolve<IWorldDropZoneQuery>().TryGetDeliveryDropoff(out var dropoff), "Missing normal dropoff.");
        var traversal = GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(worker));
        var access = scope.Container.Resolve<IGridTraversalAccessQuery>();
        var costs = scope.Container.Resolve<IGridTraversalCostPolicy>();
        Vector2Int? anchor = null;
        foreach (var room in scope.Container.Resolve<IRoomLayoutCache>().GetLayout(grid).Rooms
            .Where(x => x.IsUsable).OrderBy(x => x.Bounds.yMin).ThenBy(x => x.Bounds.xMin))
        {
            foreach (var p in room.Cells.OrderBy(x => x.y).ThenBy(x => x.x))
            {
                if (!definition.GetGridPosList(p).All(c => room.ContainsCell(c)
                    && grid.GetGridCell(c) is GridCell cell && cell.CanBuildInArea(definition)
                    && cell.CanOccupy(definition.Placement.Layer))) continue;
                if (!grid.IsWalkable(p) || grid.SearchPathTo(dropoff, p,
                    c => access.CanTraverse(grid, c, traversal, out _), costs, traversal).GetMoveCostTo(p) == int.MaxValue) continue;
                anchor = p; break;
            }
            if (anchor.HasValue) break;
        }
        Require(anchor.HasValue, "No reachable legal P23 footprint in an existing usable room.");
        var plot = scope.Container.Resolve<IGridBuildingObjectFactory>().Create(grid, definition, anchor.Value);
        Require(plot != null, "Actual building factory rejected P23.");
        foreach (var component in plot.GetComponentsInChildren<MonoBehaviour>(true)) scope.Container.Inject(component);
        plot.SetGrid(grid); plot.Initialization(definition, anchor.Value);
        Require(grid.RegisterOccupant(plot, definition.Placement.Layer, definition.GetGridPosList(anchor.Value),
            definition.Placement.IsMovement), "Actual grid rejected P23.");
        Require(scope.Container.Resolve<IBuildingWorldQuery>().Buildings.Contains(plot), "P23 is not in published main world.");
        lines.Add("setup=actual owner/party UI; authored P23 factory at " + anchor + "; no irrigation fixture or extra expansion");
        return plot;
    }

    private Vector2Int SelectSource(Grid grid, CharacterActor worker, Vector2Int destination)
    {
        var traversal = GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(worker));
        var access = scope.Container.Resolve<IGridTraversalAccessQuery>();
        var costs = scope.Container.Resolve<IGridTraversalCostPolicy>();
        var selected = grid.GetCells().Select(x => x.Position).Where(p => grid.IsWalkable(p) && Distance(p, destination) >= 4)
            .OrderBy(p => Distance(p, destination)).ThenBy(p => p.y).ThenBy(p => p.x)
            .Where(p => grid.SearchPathTo(destination, p, c => access.CanTraverse(grid, c, traversal, out _), costs, traversal)
                .GetMoveCostTo(p) != int.MaxValue).Select(p => (Vector2Int?)p).FirstOrDefault();
        Require(selected.HasValue, "No reachable separated source; do not substitute same-cell delivery.");
        return selected.Value;
    }

    private void PrepareGrowingEnvironment(BuildableObject plot)
    {
        var environment = scope.Container.Resolve<IEnvironmentalFieldPersistence>();
        var data = environment.Capture();
        data.cells.RemoveAll(x => x.x == plot.centerPos.x && x.y == plot.centerPos.y);
        data.cells.Add(new EnvironmentalCellSaveData { x = plot.centerPos.x, y = plot.centerPos.y,
            temperatureC = 20, airQuality = 100, lightLevel = 100 });
        environment.Restore(environment.PrepareRestore(data));
    }

    private void ConfigureWorker(bool treat)
    {
        var worker = Worker();
        foreach (var actor in world.Characters.Where(x => x != null)) actor.SetAiPaused(actor != worker);
        foreach (var need in new[] { CharacterCondition.HUNGER, CharacterCondition.THIRST, CharacterCondition.SLEEP,
            CharacterCondition.HYGIENE, CharacterCondition.EXCRETION, CharacterCondition.FUN })
            worker.Stats.ChangesStat(need, 100 - worker.Stats.GetConditionValue(need, 0));
        var work = worker.GetComponent<AbilityWork>();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
        // Keep Treat off during the delivery-only observation, then use the normal
        // player-priority command. No direct assignment, target warp or ApplyWork.
        work.SetWorkPriority(BuiltInWorkTypeIds.Treat, treat ? WorkPriorityLevel.Priority1 : WorkPriorityLevel.Off);
        if (treat)
        {
            Require(work.TrySetPriorityWorkTarget(Plot(), BuiltInWorkTypeIds.Treat, null, out string failure),
                "Actual priority Treat command failed: " + failure);
            Require(worker.Brain.PreferActionOnNextDecision<AIWork>(), "Staff has no real work action.");
        }
        else Require(worker.Brain.PreferActionOnNextDecision<AIHaul>(), "Staff has no real haul action.");
        worker.Brain.RequestImmediateReplan(clearFailures: true);
        Require(worker.CanRunAi, "Staff is not active after normal preparation.");
    }

    private void RenderWaterUi(BuildableObject plot, string stage)
    {
        var panel = new GameObject("Wim016WaterPanelWitness", typeof(RectTransform), typeof(Canvas));
        try
        {
            scope.Container.Resolve<ICropPlotBuildingPanelPresenter>().Render(panel.transform, plot,
                TMPro.TMP_Settings.defaultFontAsset, _ => { }, () => { });
            string text = string.Join("\n", panel.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(x => x.text));
            Require(text.Contains("급수 · 0/2") && text.Contains(stage), "Actual water presenter lost state: " + text);
        }
        finally { DestroyImmediate(panel); }
    }

    private IEnumerator AdvanceGameSeconds(float seconds)
    {
        float start = clock.Time, wall = Time.realtimeSinceStartup;
        Resume();
        while (clock.Time - start < seconds && Time.realtimeSinceStartup - wall < 15) yield return null;
        Pause();
        Require(clock.Time - start >= seconds, "Main clock did not advance; wall/game timing cannot be inferred.");
        lines.Add("clock=game " + (clock.Time - start) + " / wall " + (Time.realtimeSinceStartup - wall));
    }
    private CharacterActor Worker() => world.Characters.Single(x => x != null && x.Identity.PersistentId == workerId);
    private BuildableObject Plot() => scope.Container.Resolve<IBuildingWorldQuery>().Buildings
        .Single(x => x != null && x.RequirePersistentInstanceId().Value == plotId);
    private CropPlotSnapshot Row() => crops.Plots.Single(x => x.PlotId == plotId);
    private CropPlotSaveData SavedPlot() => crops.Capture().plots.Single(x => x.buildingInstanceId == plotId);
    private int WaterQuantity() => items.GetAllStacks().Where(x => x.ItemId == WaterId).Sum(x => x.Quantity);
    private string WorkerDiagnostic() => "worker=" + workerId + "; action=" + Worker().Brain.bestAction?.actionset?.Branch
        + "; phase=" + Worker().Brain.CurrentActionPhase + "; failure=" + Worker().Brain.LastActionFailure
        + "; hauling=" + Worker().GetComponent<AbilityHaul>().IsHauling;
    private static int Distance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    private void Pause() { var game = FindFirstObjectByType<GameManager>(); if (game != null) game.isPause = true; if (timeScale != null) timeScale.Scale = 0; }
    private void Resume() { FindFirstObjectByType<GameManager>().isPause = false; timeScale.Scale = 2; }
    private static void Require(bool value, string failure) { if (!value) throw new InvalidOperationException(failure); }
}
#endif
