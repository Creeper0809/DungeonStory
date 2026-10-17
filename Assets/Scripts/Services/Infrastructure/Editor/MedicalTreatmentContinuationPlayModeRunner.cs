#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>Real scene authorities; controlled disease, delivery and treatment command.</summary>
public sealed class MedicalTreatmentContinuationPlayModeRunner
{
    [Serializable] private sealed class Report
    {
        public string scope = "GameplayScene/start-party; independent standard/detox branches from the same official baseline; authored medical facility; controlled illness/toxicity and physical haul; real typed treatment command; official whole-world pending/completed restore. Natural disease, movement and AI scheduling are NOT tested.";
        public bool passed, identityChecks, freshActor, freshFacility, pendingExact, completedOnce, replayExact;
        public bool networkSent, humanApprovalClaimed, trainingEligible, fullAuditClosure;
        public int sections, rejectedAcks, medicineBefore, medicineAfter, terminalCount;
        public float severityBefore, severityAfter, elapsedSeconds;
        public string actorId, workerId, facilityId, medicineId, operationId, serviceSessionId, failure;
        public SurvivalTreatmentPlanSaveData pending;
        public DetoxReport detox = new();
        public FuelReport fuel = new();
        public List<GameplayOutcomeSnapshot> terminals = new();
        public List<string> warnings = new(), errors = new();
    }
    [Serializable] private sealed class DetoxReport
    {
        public bool freshActor, freshFacility, pendingExact, completedOnce, replayExact;
        public int sections, rejectedAcks, medicineBefore, medicineAfter, terminalCount, outcomeCount;
        public float toxicityBefore, toxicityAfter;
        public string medicineId = "medicine:antidote", operationId, serviceSessionId;
        public CharacterDetoxTreatmentPlanSaveData pending;
        public List<GameplayOutcomeSnapshot> terminals = new(), outcomes = new();
        public List<RejectionReport> rejectedRestores = new();
    }
    [Serializable] private sealed class RejectionReport
    {
        public string caseId, reason;
        public bool rejected, expectedReason, referencesUnchanged, worldUnchanged;
        public List<string> changedSections = new();
    }
    [Serializable] private sealed class FuelReport
    {
        public string scope = "Independent baseline; authored E01; controlled physical haul and actual refuel command/default physical outcome bridge; pending ACK whole-world recovery and completed restore. No natural AI scheduling or elapsed burn-time claim.";
        public bool freshFacility, recoveredOnce, replayExact;
        public int sections, rejectedAcks, quantityBefore, quantityAfter, sequenceBefore, sequenceAfter;
        public float charge;
        public string facilityId, otherFacilityId, itemId, operationId, commitId;
        public List<GameplayOutcomeSnapshot> outcomes = new();
    }
    private readonly Report report = new();
    private DungeonRuntimeLifetimeScope scope;
    private IDungeonGameSaveService saves;
    private IGameTimeScaleController timeScale;
    private ICharacterAiWorldRegistry world;
    private ISurvivalFoodPersistence food;
    private ISurvivalTreatmentCompletionCommand treatment;
    private IWorldItemStackRuntime items;
    private IServiceSessionRuntime sessions;
    private PhysicalItemBatchDispositionService batch;
    private CharacterSpawner spawner;
    private bool spawnerWasPaused;
    private float originalScale;
    private string output;
    private const int WorkRunId = 270627;

    public MedicalTreatmentContinuationPlayModeRunner(IEnumerable<string> warnings, IEnumerable<string> errors)
    {
        report.warnings.AddRange(warnings);
        report.errors.AddRange(errors);
    }

    public IEnumerator Run()
    {
        File.Delete(DungeonFullWorldRoundTripPlayModeFacade.RequestPath);
        output = Path.GetDirectoryName(DungeonPersistencePaths.Root);
        Application.logMessageReceived += CaptureLog;
        float started = Time.realtimeSinceStartup;
        var stack = new Stack<IEnumerator>();
        stack.Push(Verify());
        while (stack.Count > 0)
        {
            bool moved;
            object next = null;
            try
            {
                Require(Time.realtimeSinceStartup - started < 240f, "Bounded medical witness exceeded 240 real seconds.");
                Require(report.errors.Count == 0, "Runtime error: " + report.errors.FirstOrDefault());
                moved = stack.Peek().MoveNext();
                if (moved) next = stack.Peek().Current;
            }
            catch (Exception exception) { report.failure = exception.ToString(); break; }
            if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator child) stack.Push(child);
            else yield return next;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        report.elapsedSeconds = Time.realtimeSinceStartup - started;
        report.passed = string.IsNullOrEmpty(report.failure) && report.errors.Count == 0
            && !report.networkSent && report.freshActor && report.freshFacility
            && report.pendingExact && report.completedOnce && report.replayExact
            && report.detox.freshActor && report.detox.freshFacility
            && report.detox.pendingExact && report.detox.completedOnce && report.detox.replayExact
            && report.detox.rejectedRestores.Count == 3
            && report.detox.rejectedRestores.All(value => value.rejected && value.expectedReason
                && value.referencesUnchanged && value.worldUnchanged)
            && report.fuel.freshFacility && report.fuel.recoveredOnce && report.fuel.replayExact;
        WriteNew("report.json", JsonUtility.ToJson(report, true));
        if (timeScale != null) timeScale.Scale = originalScale;
        if (spawner != null) spawner.ConfigureDeterministicSimulationForDiagnostics(paused: spawnerWasPaused);
        Application.logMessageReceived -= CaptureLog;
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(report.passed ? 0 : 1);
    }

    private IEnumerator Verify()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Medical witness requires the owned isolated PlayMode entry.");
        report.identityChecks = ServiceExecutionOwnerSaveValidationDebugScenarios.RunAll(log: false);
        Require(report.identityChecks, "Service execution owner/physical identity regressions failed.");
        float deadline = Time.realtimeSinceStartup + 45f;
        while (scope == null && Time.realtimeSinceStartup < deadline)
        {
            scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(value => value != null && value.Container != null);
            if (scope == null) yield return null;
        }
        Require(scope != null, "Official world scope missing.");
        if (scope.Container.Resolve<ICharacterSpawnerProvider>().TryGetSpawner(out spawner))
        {
            spawnerWasPaused = spawner.DeterministicSimulationPausedForDiagnostics;
            spawner.ConfigureDeterministicSimulationForDiagnostics(paused: true);
        }
        var queue = scope.Container.Resolve<ILocalLlmRuntimeProvider>().GetRequiredRuntime() as LocalLlmRequestQueue;
        Require(queue != null, "Capture-only queue missing.");
        queue.ConfigureIsolatedRequestCapture(request => report.networkSent |= request.NetworkSent || !request.CaptureOnly);
        if (!scope.Container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out OwnerRunManager manager)
            || manager.CurrentOwnerActor == null)
        {
            StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            for (int frame = 0; frame < 10; frame++) yield return null;
        }
        Require(StartPartyPreparationPlayModeVerifier.TryReconcileTierZeroForDirectGameplayFixture(scope, out string setup), setup);
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        originalScale = timeScale.Scale;
        timeScale.Scale = 0;
        PauseActors();
        food = scope.Container.Resolve<ISurvivalFoodPersistence>();
        treatment = scope.Container.Resolve<ISurvivalFoodQuery>() as ISurvivalTreatmentCompletionCommand;
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        sessions = scope.Container.Resolve<IServiceSessionRuntime>();
        batch = scope.Container.Resolve<IPhysicalItemBatchDispositionService>() as PhysicalItemBatchDispositionService;
        Require(treatment != null && batch != null && batch.DebugRejectAcknowledgement == null,
            "Actual medical/physical authorities missing or already owned by another verifier.");
        Require(world.TryGetGrid(out Grid grid), "Actual grid missing.");
        CharacterActor patient = world.Characters.Single(value => value != null && value.Role == CharacterRole.Owner);
        CharacterActor worker = world.Characters.Where(value => value != null && value != patient
                && CharacterWorkRoleUtility.IsWorker(value))
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal).First();
        report.actorId = patient.Identity.PersistentId;
        report.workerId = worker.Identity.PersistentId;
        BuildableObject facility = PrepareMedicalFacility(grid);
        report.facilityId = facility.RequirePersistentInstanceId().Value;
        var ownerSource = scope.Container.Resolve<ICharacterConsumablesInputOwnerDescriptorSource>();
        Require(scope.Container.Resolve<ICharacterConsumablesInputOwnerRuntime>().TryReconcileLive(
            ownerSource.BuildLiveInputOwnerDescriptors(), "qa-medical-continuation", out string ownerFailure), ownerFailure);
        string baselineWorld = saves.ToJson(saves.Capture());
        WriteNew("baseline-world.json", baselineWorld);

        // Explicit test setup, never exported as an observed disease or training fact.
        DungeonSurvivalSaveData illness = food.Capture();
        Require(illness.health.All(value => value == null || value.severity <= 0f),
            "Fresh medical witness unexpectedly contains other patients.");
        illness.health.Add(new SurvivalHealthSaveData { persistentId = report.actorId,
            state = SurvivalHealthState.Sick, severity = 0.8f, remainingSeconds = 360f,
            source = "qa-controlled-medical-checkpoint" });
        food.PublishRestoreCandidate(food.BuildRestoreCandidate(illness));
        report.severityBefore = Severity();
        var medicine = scope.Container.Resolve<IItemDefinitionCatalog>().All
            .Where(value => value != null && value.StockCategory == StockCategory.Medicine)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal).First();
        report.medicineId = medicine.ItemId;
        var warehouse = world.Warehouses.FirstOrDefault(value => value != null && value.HasWarehouseInventory
            && value.Inventory.Accepts(StockCategory.Medicine) && value.Inventory.HasMassCapacityAuthority);
        Require(warehouse != null, "Authored initial medical stock warehouse unavailable.");
        int existing = MedicineQuantity();
        Require(scope.Container.Resolve<WorldItemWarehouseService>().SpawnItemStock(
                warehouse, medicine.ItemId, 1, "qa:medical-continuation", "generic:" + medicine.ItemId,
                out int spawned, out _, out DomainFailure spawnFailure) && spawned == 1,
            "Physical stock admission failed: " + spawnFailure);
        report.medicineBefore = existing + 1;
        Require(!treatment.TryEnsureTreatmentSupply(worker.BuildingVisitor, facility, WorkRunId,
                out bool earlyCompletion, out _) && !earlyCompletion,
            "Warehouse-only medicine bypassed delivery.");
        DeliverMedicine(worker, facility, report.medicineId, report.medicineBefore);
        Require(treatment.TryEnsureTreatmentSupply(worker.BuildingVisitor, facility, WorkRunId,
            out bool completionOnly, out DomainFailure supplyFailure) && !completionOnly,
            "Delivered medicine not available: " + supplyFailure);

        batch.DebugRejectAcknowledgement = _ => { report.rejectedAcks++; return true; };
        bool completed;
        DomainFailure interrupted;
        try { completed = treatment.TryApplyTreatmentWork(worker.BuildingVisitor, facility, WorkRunId, out _, out interrupted); }
        finally { batch.DebugRejectAcknowledgement = null; }
        WriteNew("treatment-attempt-survival.json", JsonUtility.ToJson(food.Capture(), true));
        WriteNew("treatment-attempt-services.json", JsonUtility.ToJson(sessions.Capture(), true));
        Require(!completed && report.rejectedAcks == 1, "Real terminal ACK failure not observed: completed="
            + completed + "; rejected=" + report.rejectedAcks + "; " + interrupted.Code
            + "; " + string.Join(" | ", interrupted.Parameters.ToArray()));
        report.pending = food.Capture().activeTreatmentPlans.Single();
        report.operationId = report.pending.operationId;
        report.serviceSessionId = report.pending.serviceSessionId;
        report.severityAfter = Severity();
        report.medicineAfter = MedicineQuantity();
        Require(report.pending.phase == SurvivalTreatmentPlanPhase.ServiceCompleted
            && report.pending.patientId == report.actorId && report.pending.facilityInstanceId == report.facilityId
            && report.severityAfter < report.severityBefore && report.medicineAfter == report.medicineBefore - 1
            && batch.TryGetPending(report.pending.physicalCommitOperationId, out var receipt)
            && receipt.CommitId == report.pending.physicalCommitId,
            "Interrupted treatment did not retain its exact completed-effect and physical receipt owner.");
        string foodBefore = JsonUtility.ToJson(food.Capture());
        string sessionBefore = JsonUtility.ToJson(sessions.Capture());
        string terminalBefore = TerminalJson();
        Require(report.terminals.Count == 1 && report.terminals[0].status == GameplayOutcomeStatus.Succeeded,
            "Treatment did not publish exactly one service terminal.");
        DungeonGameSaveData pendingWorld = saves.Capture();
        report.sections = pendingWorld.sections.Count;
        Require(report.sections == scope.Container.Resolve<IDungeonSaveSectionRegistry>().OrderedSections.Count,
            "Official whole-world section coverage mismatch.");
        string pendingJson = saves.ToJson(pendingWorld);
        WriteNew("pending-world.json", pendingJson);
        Require(saves.TryRestore(saves.FromJson(pendingJson), out DungeonGameRestoreReport restored),
            "Pending medical world restore failed: " + string.Join(" | ", restored.Errors));
        CharacterActor replacementPatient = Actor(report.actorId);
        CharacterActor replacementWorker = Actor(report.workerId);
        BuildableObject replacement = world.Buildings.Single(value => value != null
            && value.PersistentInstanceId.Value == report.facilityId);
        report.freshActor = !ReferenceEquals(patient, replacementPatient) && !ReferenceEquals(worker, replacementWorker);
        report.freshFacility = !ReferenceEquals(facility, replacement);
        report.pendingExact = foodBefore == JsonUtility.ToJson(food.Capture())
            && sessionBefore == JsonUtility.ToJson(sessions.Capture()) && terminalBefore == TerminalJson()
            && MedicineQuantity() == report.medicineAfter;
        Require(report.freshActor && report.freshFacility && report.pendingExact,
            "Whole-world restore changed treatment progress or retained old scene owners.");
        PauseActors();
        WriteNew("pending-restored-world.json", saves.ToJson(saves.Capture()));
        Require(treatment.TryApplyTreatmentWork(replacementWorker.BuildingVisitor, replacement, WorkRunId,
            out int amount, out DomainFailure retryFailure) && amount == 1,
            "Restored treatment did not finish: " + retryFailure);
        report.completedOnce = food.Capture().activeTreatmentPlans.Count == 0
            && food.Capture().completedTreatmentOperationIds.Count(value => value == report.operationId) == 1
            && !batch.TryGetPending(report.pending.physicalCommitOperationId, out _)
            && MedicineQuantity() == report.medicineAfter && Severity() == report.severityAfter
            && TerminalJson() == terminalBefore;
        Require(report.completedOnce, "Restored treatment repeated effects/medicine/terminal or lost exact completion.");
        string completedFood = JsonUtility.ToJson(food.Capture());
        Require(treatment.TryApplyTreatmentWork(replacementWorker.BuildingVisitor, replacement, WorkRunId,
                out _, out _) && JsonUtility.ToJson(food.Capture()) == completedFood
            && MedicineQuantity() == report.medicineAfter && TerminalJson() == terminalBefore,
            "Same work replay was not idempotent.");
        string completedJson = saves.ToJson(saves.Capture());
        WriteNew("completed-world.json", completedJson);
        Require(saves.TryRestore(saves.FromJson(completedJson), out DungeonGameRestoreReport finalRestore),
            "Completed medical world restore failed: " + string.Join(" | ", finalRestore.Errors));
        report.replayExact = JsonUtility.ToJson(food.Capture()) == completedFood
            && MedicineQuantity() == report.medicineAfter && TerminalJson() == terminalBefore;
        report.terminalCount = report.terminals.Count;
        Require(report.replayExact, "Completed medical restore revived or lost treatment state.");
        WriteNew("completed-restored-world.json", saves.ToJson(saves.Capture()));
        VerifyDetox(baselineWorld);
        VerifyFuel(baselineWorld);
    }

    private void VerifyFuel(string baselineWorld)
    {
        FuelReport fuel = report.fuel;
        Require(saves.TryRestore(saves.FromJson(baselineWorld), out DungeonGameRestoreReport baselineRestore),
            "Fuel baseline restore failed: " + string.Join(" | ", baselineRestore.Errors));
        PauseActors();
        Require(world.TryGetGrid(out Grid grid), "Fuel grid unavailable.");
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/Modular/E01_벽횃불.asset");
        var authored = definition?.GetAbility<BuildingFuelConsumerAbility>();
        Require(authored != null && authored.fuelPerRefuel == 1 && authored.fuelSecondsPerRefuel == 180f,
            "Authored fuel contract missing.");
        BuildableObject first = PlaceFacility(grid, definition), second = PlaceFacility(grid, definition);
        fuel.facilityId = first.RequirePersistentInstanceId().Value;
        fuel.otherFacilityId = second.RequirePersistentInstanceId().Value;
        fuel.itemId = authored.fuelItemId;
        var supply = scope.Container.Resolve<ISurvivalRefuelSupplyQuery>();
        var refuel = scope.Container.Resolve<ISurvivalRefuelCompletionCommand>();
        Require(supply.TryGetRefuelSupplyPlan(first, out SurvivalFacilityFuelSupplyPlan plan)
            && plan.ItemId == fuel.itemId && plan.RequiredQuantity == 1, "Actual fuel supply contract mismatch.");
        var catalog = scope.Container.Resolve<IItemDefinitionCatalog>();
        Require(catalog.TryGet((ItemDefinitionId)fuel.itemId, out ItemDefinitionSO item), "Fuel item unavailable.");
        var warehouse = world.Warehouses.First(value => value != null && value.HasWarehouseInventory
            && value.Inventory.Accepts(item.StockCategory) && value.Inventory.HasMassCapacityAuthority);
        fuel.quantityBefore = MedicineQuantity(fuel.itemId) + 1;
        Require(scope.Container.Resolve<WorldItemWarehouseService>().SpawnItemStock(warehouse, fuel.itemId,
            1, "qa:fuel-continuation", "generic:" + fuel.itemId, out int added, out _, out DomainFailure admission)
            && added == 1, "Fuel stock admission failed: " + admission.Code);
        Require(!refuel.TryEnsureRefuelSupply(null, first, out bool early, out _) && !early
            && !first.HasFacilityFuelSupply && !second.HasFacilityFuelSupply, "Remote fuel charged a facility.");
        DeliverInput(Actor(report.workerId), first, fuel.itemId, plan.DestinationId, fuel.quantityBefore);
        Require(refuel.TryEnsureRefuelSupply(null, first, out bool completionOnly, out DomainFailure supplyFailure)
            && !completionOnly, "Delivered fuel unavailable: " + supplyFailure.Code);
        fuel.sequenceBefore = first.FacilityState.nextFuelOperationSequence;
        fuel.operationId = SurvivalFoodStockRuntime.FormatFacilityFuelOperationId(first, fuel.sequenceBefore);
        Require(batch.DebugRejectAcknowledgement == null, "Another verifier owns physical ACK hook.");
        batch.DebugRejectAcknowledgement = _ => { fuel.rejectedAcks++; return true; };
        bool applied;
        DomainFailure interrupted;
        try { applied = refuel.TryApplyRefuelWork(null, first, out _, out interrupted); }
        finally { batch.DebugRejectAcknowledgement = null; }
        WriteNew("fuel-attempt-state.json", JsonUtility.ToJson(first.FacilityState, true));
        Require(!applied && fuel.rejectedAcks == 1
            && first.FacilityState.pendingFuel.phase == (int)FacilityFuelCommitPhase.OutcomePublished
            && batch.TryGetPending(fuel.operationId, out var receipt),
            "Actual fuel physical ACK boundary not reached: " + interrupted.Code
            + "; " + string.Join(" | ", interrupted.Parameters.ToArray()));
        fuel.commitId = first.FacilityState.pendingFuel.physicalCommitId;
        fuel.quantityAfter = MedicineQuantity(fuel.itemId);
        fuel.charge = first.FacilityState.remainingFuelGameSeconds;
        fuel.outcomes = FuelOutcomes();
        Require(fuel.quantityAfter == fuel.quantityBefore - 1 && fuel.charge == authored.fuelSecondsPerRefuel
            && !second.HasFacilityFuelSupply && fuel.outcomes.Count == 1
            && fuel.outcomes[0].status == GameplayOutcomeStatus.Succeeded,
            "Fuel debit/charge/default physical outcome not exactly once.");
        string outcomeBefore = JsonUtility.ToJson(new OutcomeSet { items = fuel.outcomes });
        DungeonGameSaveData checkpoint = saves.Capture();
        fuel.sections = checkpoint.sections.Count;
        Require(fuel.sections == scope.Container.Resolve<IDungeonSaveSectionRegistry>().OrderedSections.Count,
            "Fuel whole-world section coverage mismatch.");
        string pendingJson = saves.ToJson(checkpoint);
        WriteNew("fuel-pending-world.json", pendingJson);
        Require(saves.TryRestore(saves.FromJson(pendingJson), out DungeonGameRestoreReport restore),
            "Fuel pending restore failed: " + string.Join(" | ", restore.Errors));
        PauseActors();
        BuildableObject restoredFirst = Facility(fuel.facilityId), restoredSecond = Facility(fuel.otherFacilityId);
        fuel.freshFacility = !ReferenceEquals(first, restoredFirst) && !ReferenceEquals(second, restoredSecond);
        // Survival's official restore publisher recovers this already-published receipt.
        fuel.sequenceAfter = restoredFirst.FacilityState.nextFuelOperationSequence;
        fuel.recoveredOnce = fuel.freshFacility && fuel.sequenceAfter == fuel.sequenceBefore + 1
            && restoredFirst.FacilityState.pendingFuel.phase == (int)FacilityFuelCommitPhase.None
            && restoredFirst.FacilityState.remainingFuelGameSeconds == fuel.charge
            && !restoredSecond.HasFacilityFuelSupply && !batch.TryGetPending(fuel.operationId, out _)
            && MedicineQuantity(fuel.itemId) == fuel.quantityAfter && FuelOutcomeJson() == outcomeBefore;
        Require(fuel.recoveredOnce, "Fuel whole-world recovery duplicated/lost charge, item, physical outcome or ACK.");
        string completedState = JsonUtility.ToJson(restoredFirst.FacilityState);
        Require(refuel.TryApplyRefuelWork(null, restoredFirst, out int replayAmount, out _) && replayAmount == 0
            && completedState == JsonUtility.ToJson(restoredFirst.FacilityState)
            && MedicineQuantity(fuel.itemId) == fuel.quantityAfter && FuelOutcomeJson() == outcomeBefore,
            "Already-charged fuel command repeated consumption or publication.");
        string completed = saves.ToJson(saves.Capture());
        WriteNew("fuel-completed-world.json", completed);
        Require(saves.TryRestore(saves.FromJson(completed), out DungeonGameRestoreReport finalRestore),
            "Completed fuel restore failed: " + string.Join(" | ", finalRestore.Errors));
        fuel.replayExact = completedState == JsonUtility.ToJson(Facility(fuel.facilityId).FacilityState)
            && !Facility(fuel.otherFacilityId).HasFacilityFuelSupply
            && MedicineQuantity(fuel.itemId) == fuel.quantityAfter && FuelOutcomeJson() == outcomeBefore;
        Require(fuel.replayExact, "Completed fuel restore changed exact effect/consumption/outcome.");
        WriteNew("fuel-completed-restored-world.json", saves.ToJson(saves.Capture()));

        List<GameplayOutcomeSnapshot> FuelOutcomes() => ReadOutcomes(value => value.provenance.Any(
            item => item.kindId == "domain-commit" && item.value == fuel.commitId));
        string FuelOutcomeJson() => JsonUtility.ToJson(new OutcomeSet { items = FuelOutcomes() });
        BuildableObject Facility(string id) => world.Buildings.Single(value => value != null && value.PersistentInstanceId.Value == id);
    }

    private void VerifyDetox(string baselineWorld)
    {
        DetoxReport detox = report.detox;
        Require(saves.TryRestore(saves.FromJson(baselineWorld), out DungeonGameRestoreReport baselineRestore),
            "Detox baseline restore failed: " + string.Join(" | ", baselineRestore.Errors));
        PauseActors();
        CharacterActor patient = Actor(report.actorId), worker = Actor(report.workerId);
        BuildableObject facility = world.Buildings.Single(value => value != null && value.PersistentInstanceId.Value == report.facilityId);
        var persistence = scope.Container.Resolve<ICharacterConsumablesPersistence>();
        var consumables = scope.Container.Resolve<ICharacterConsumablesApplication>();
        CharacterId patientId = new(report.actorId);
        DungeonCharacterConsumablesSaveData toxic = persistence.Capture();
        Require(toxic.toxicityStates.All(value => value.characterId != report.actorId),
            "Independent baseline already has toxicity for this patient.");
        toxic.toxicityStates.Add(new CharacterToxicityState { characterId = report.actorId,
            toxicity = 60f, lastNaturalRecoveryDay = scope.Container.Resolve<IGameCalendar>().Day });
        persistence.PublishRestoreCandidate(persistence.BuildRestoreCandidate(toxic));
        detox.toxicityBefore = consumables.GetToxicityStatus(patientId).Toxicity;
        Require(scope.Container.Resolve<IItemDefinitionCatalog>().TryGet((ItemDefinitionId)detox.medicineId,
            out ItemDefinitionSO medicine) && medicine != null, "Authored detox medicine unavailable.");
        Require(medicine.TryGetFeature(out MedicineItemFeature feature) && feature.detoxReduction > 0f,
            "Authored detox effect unavailable.");
        // Keep the authored reduction; the 60-point starting toxicity is explicit fixture input.
        float expectedReduction = feature.detoxReduction;
        var warehouse = world.Warehouses.First(value => value != null && value.HasWarehouseInventory
            && value.Inventory.Accepts(StockCategory.Medicine) && value.Inventory.HasMassCapacityAuthority);
        detox.medicineBefore = MedicineQuantity(detox.medicineId) + 1;
        Require(scope.Container.Resolve<WorldItemWarehouseService>().SpawnItemStock(warehouse, detox.medicineId,
            1, "qa:detox-continuation", "generic:" + detox.medicineId, out int spawned, out _, out DomainFailure spawnFailure)
            && spawned == 1, "Detox stock admission failed: " + spawnFailure.Code);
        const int detoxRunId = WorkRunId + 1;
        Require(!treatment.TryEnsureTreatmentSupply(worker.BuildingVisitor, facility, detoxRunId, out bool early, out _)
            && !early, "Warehouse-only detox bypassed delivery.");
        DeliverMedicine(worker, facility, detox.medicineId, detox.medicineBefore);
        Require(treatment.TryEnsureTreatmentSupply(worker.BuildingVisitor, facility, detoxRunId, out bool completionOnly,
            out DomainFailure supplyFailure) && !completionOnly, "Detox supply unavailable: " + supplyFailure.Code);
        Require(batch.DebugRejectAcknowledgement == null, "Another verifier owns physical ACK hook.");
        batch.DebugRejectAcknowledgement = _ => { detox.rejectedAcks++; return true; };
        bool completed;
        DomainFailure interrupted;
        try { completed = ApplyDetox(patient, worker, facility, detoxRunId, out _, out interrupted); }
        finally { batch.DebugRejectAcknowledgement = null; }
        WriteNew("detox-attempt-consumables.json", JsonUtility.ToJson(persistence.Capture(), true));
        WriteNew("detox-attempt-services.json", JsonUtility.ToJson(sessions.Capture(), true));
        Require(!completed && detox.rejectedAcks == 1, "Detox ACK boundary not reached: " + interrupted.Code
            + "; " + string.Join(" | ", interrupted.Parameters.ToArray()));
        detox.pending = persistence.Capture().activeDetoxTreatmentPlans.Single();
        detox.operationId = detox.pending.operationId;
        Require(detox.operationId.StartsWith(SurvivalFoodRuntime.DetoxOperationPrefix, StringComparison.Ordinal),
            "Detox service identity missing.");
        detox.serviceSessionId = detox.operationId.Substring(SurvivalFoodRuntime.DetoxOperationPrefix.Length);
        detox.toxicityAfter = consumables.GetToxicityStatus(patientId).Toxicity;
        detox.medicineAfter = MedicineQuantity(detox.medicineId);
        detox.outcomes = ReadOutcomes(IsDetoxOutcome);
        WriteNew("detox-attempt-results.json", JsonUtility.ToJson(new OutcomeSet {
            items = ReadOutcomes(value => value.outcomeTypeId == "survival.detox-treated") }, true));
        Require(detox.pending.phase == CharacterDetoxTreatmentPlanPhase.EffectsPublished
            && detox.pending.characterId == report.actorId && detox.pending.facilityInstanceId == report.facilityId
            && detox.medicineAfter == detox.medicineBefore - 1
            && detox.toxicityAfter == Mathf.Max(0, detox.toxicityBefore - expectedReduction)
            && detox.outcomes.Count == 1 && detox.outcomes[0].status == GameplayOutcomeStatus.Succeeded
            && sessions.ActiveSessions.Count(value => value.SessionId == detox.serviceSessionId) == 1
            && batch.TryGetPending(detox.pending.physicalCommitOperationId, out var receipt)
            && receipt.CommitId == detox.pending.physicalCommitId,
            "Detox pending did not preserve exactly one effect, outcome, active service and physical receipt.");
        string consumablesBefore = JsonUtility.ToJson(persistence.Capture());
        string sessionBefore = JsonUtility.ToJson(sessions.Capture());
        string outcomeBefore = JsonUtility.ToJson(new OutcomeSet { items = detox.outcomes });
        DungeonGameSaveData checkpoint = saves.Capture();
        detox.sections = checkpoint.sections.Count;
        Require(detox.sections == scope.Container.Resolve<IDungeonSaveSectionRegistry>().OrderedSections.Count,
            "Detox whole-world section coverage mismatch.");
        string pendingJson = saves.ToJson(checkpoint);
        WriteNew("detox-pending-world.json", pendingJson);
        VerifyRejectedDetoxRestores(pendingJson, patient, worker, facility);
        Require(saves.TryRestore(saves.FromJson(pendingJson), out DungeonGameRestoreReport restore),
            "Detox pending world restore failed: " + string.Join(" | ", restore.Errors));
        CharacterActor freshPatient = Actor(report.actorId), freshWorker = Actor(report.workerId);
        BuildableObject freshFacility = world.Buildings.Single(value => value != null && value.PersistentInstanceId.Value == report.facilityId);
        detox.freshActor = !ReferenceEquals(patient, freshPatient) && !ReferenceEquals(worker, freshWorker);
        detox.freshFacility = !ReferenceEquals(facility, freshFacility);
        detox.pendingExact = consumablesBefore == JsonUtility.ToJson(persistence.Capture())
            && sessionBefore == JsonUtility.ToJson(sessions.Capture()) && outcomeBefore == DetoxOutcomeJson()
            && MedicineQuantity(detox.medicineId) == detox.medicineAfter;
        Require(detox.freshActor && detox.freshFacility && detox.pendingExact,
            "Detox restore changed the pending state or retained old scene owners.");
        PauseActors();
        WriteNew("detox-pending-restored-world.json", saves.ToJson(saves.Capture()));
        Require(ApplyDetox(freshPatient, freshWorker, freshFacility, detoxRunId,
            out int amount, out DomainFailure retryFailure) && amount == 1,
            "Detox resume failed: " + retryFailure.Code + "; " + string.Join(" | ", retryFailure.Parameters.ToArray()));
        detox.terminals = ReadTerminals(detox.serviceSessionId);
        detox.completedOnce = persistence.Capture().activeDetoxTreatmentPlans.Count == 0
            && persistence.Capture().completedOperations.Count(value => value.operationId == detox.operationId && value.detox) == 1
            && !batch.TryGetPending(detox.pending.physicalCommitOperationId, out _)
            && sessions.ActiveSessions.All(value => value.SessionId != detox.serviceSessionId)
            && detox.terminals.Count == 1 && detox.terminals[0].status == GameplayOutcomeStatus.Succeeded
            && MedicineQuantity(detox.medicineId) == detox.medicineAfter
            && consumables.GetToxicityStatus(patientId).Toxicity == detox.toxicityAfter
            && DetoxOutcomeJson() == outcomeBefore;
        Require(detox.completedOnce, "Detox resume duplicated/lost effects, medicine, service or outcome.");
        string completedConsumables = JsonUtility.ToJson(persistence.Capture());
        string completedSessions = JsonUtility.ToJson(sessions.Capture());
        string completedTerminals = JsonUtility.ToJson(new OutcomeSet { items = detox.terminals });
        Require(ApplyDetox(freshPatient, freshWorker, freshFacility, detoxRunId, out _, out _)
            && JsonUtility.ToJson(persistence.Capture()) == completedConsumables
            && MedicineQuantity(detox.medicineId) == detox.medicineAfter && DetoxOutcomeJson() == outcomeBefore,
            "Immediate same-work detox replay duplicated a completed effect.");
        string completedWorld = saves.ToJson(saves.Capture());
        WriteNew("detox-completed-world.json", completedWorld);
        Require(saves.TryRestore(saves.FromJson(completedWorld), out DungeonGameRestoreReport completedRestore),
            "Completed detox world restore failed: " + string.Join(" | ", completedRestore.Errors));
        detox.replayExact = JsonUtility.ToJson(persistence.Capture()) == completedConsumables
            && JsonUtility.ToJson(sessions.Capture()) == completedSessions && DetoxOutcomeJson() == outcomeBefore
            && JsonUtility.ToJson(new OutcomeSet { items = ReadTerminals(detox.serviceSessionId) }) == completedTerminals
            && MedicineQuantity(detox.medicineId) == detox.medicineAfter;
        Require(detox.replayExact, "Completed detox restore changed effects or revived pending execution.");
        detox.terminalCount = detox.terminals.Count;
        detox.outcomeCount = detox.outcomes.Count;
        WriteNew("detox-completed-restored-world.json", saves.ToJson(saves.Capture()));

        // This migrated producer binds its source command in the stable result
        // key, not an operation fact. Require that exact key and patient, never
        // accept an arbitrary result merely because its type matches.
        bool IsDetoxOutcome(GameplayOutcomeSnapshot value)
        {
            const string producer = "ledger.survival.detox-treated";
            string ownerKey = producer + "|character-consumables-detox@1|" + detox.operationId;
            string exactOperation = "migrated:32:" + NarrativeInferenceHash.ComputeSha256Utf8(ownerKey).Substring(0, 32) + ":1";
            return value.producerId == producer && value.outcomeTypeId == "survival.detox-treated"
                && value.operationId == exactOperation && value.commitRevision == 1 && value.localResultIndex == 0
                && value.participants.Any(actor => actor.entityKindId == "character" && actor.entityId == report.actorId);
        }
        string DetoxOutcomeJson() => JsonUtility.ToJson(new OutcomeSet { items = ReadOutcomes(IsDetoxOutcome) });
    }

    private void VerifyRejectedDetoxRestores(string pendingJson, CharacterActor patient,
        CharacterActor worker, BuildableObject facility)
    {
        Verify("wrong-patient", (services, consumables) =>
            consumables.activeDetoxTreatmentPlans.Single().characterId = report.workerId,
            "Service execution owner mismatch (detox)");
        Verify("missing-session", (services, consumables) => services.sessions.Clear(),
            "Service execution owner mismatch (detox)");
        Verify("missing-plan", (services, consumables) => consumables.activeDetoxTreatmentPlans.Clear(),
            "Active service session has no executable owner");

        void Verify(string caseId, Action<ServiceRoomsSaveData, DungeonCharacterConsumablesSaveData> corrupt,
            string expectedReason)
        {
            DungeonGameSaveData before = saves.Capture();
            DungeonGameSaveData candidate = saves.FromJson(pendingJson);
            DungeonSaveSectionEnvelope serviceSection = candidate.sections.Single(value => value.sectionId == ServiceRoomsSaveSection.Id);
            DungeonSaveSectionEnvelope consumableSection = candidate.sections.Single(value => value.sectionId == CharacterConsumablesSaveSection.Id);
            ServiceRoomsSaveData serviceData = DungeonSaveJson.Deserialize<ServiceRoomsSaveData>(serviceSection.payloadJson);
            DungeonCharacterConsumablesSaveData consumableData = DungeonSaveJson.Deserialize<DungeonCharacterConsumablesSaveData>(consumableSection.payloadJson);
            Require(serviceData.sessions.Count == 1 && consumableData.activeDetoxTreatmentPlans.Count == 1,
                "Rejection fixture did not target exactly the real pending detox execution.");
            corrupt(serviceData, consumableData);
            serviceSection.payloadJson = JsonUtility.ToJson(serviceData);
            consumableSection.payloadJson = JsonUtility.ToJson(consumableData);
            // Reseal a new test copy: reject its semantic owner mismatch, not its checksum.
            candidate.manifest = DungeonSaveManifest.Capture(candidate.sections);
            WriteNew("detox-rejection-" + caseId + "-input.json", saves.ToJson(candidate));
            bool accepted = saves.TryRestore(candidate, out DungeonGameRestoreReport rejected);
            DungeonGameSaveData after = saves.Capture();
            string reason = string.Join(" | ", rejected.Errors);
            var witness = new RejectionReport { caseId = caseId, rejected = !accepted, reason = reason,
                expectedReason = reason.Contains(expectedReason, StringComparison.Ordinal),
                referencesUnchanged = world.Characters.Any(value => ReferenceEquals(value, patient))
                    && world.Characters.Any(value => ReferenceEquals(value, worker))
                    && world.Buildings.Any(value => ReferenceEquals(value, facility)),
                changedSections = before.sections.Where(section => !after.sections.Any(value =>
                    value.sectionId == section.sectionId && value.sectionVersion == section.sectionVersion
                    && value.optional == section.optional && value.payloadJson == section.payloadJson))
                    .Select(value => value.sectionId).ToList() };
            witness.worldUnchanged = before.sections.Count == after.sections.Count && witness.changedSections.Count == 0;
            report.detox.rejectedRestores.Add(witness);
            WriteNew("detox-rejection-" + caseId + "-witness.json", JsonUtility.ToJson(witness, true));
            if (!witness.worldUnchanged) WriteNew("detox-rejection-" + caseId + "-changed-world.json", saves.ToJson(after));
            Require(witness.rejected && witness.expectedReason && witness.referencesUnchanged && witness.worldUnchanged,
                "Rejected medical restore changed live authorities or failed for another reason: " + caseId + "; " + reason);
        }
    }

    private bool ApplyDetox(CharacterActor patient, CharacterActor worker, BuildableObject facility,
        int workRunId, out int amount, out DomainFailure failure)
    {
        // The production consumables port deliberately requires CanRunAi. Only
        // unpause this patient around a synchronous command; never yield an AI frame.
        patient.SetAiPaused(false);
        try { return treatment.TryApplyTreatmentWork(worker.BuildingVisitor, facility, workRunId, out amount, out failure); }
        finally { patient.SetAiPaused(true); }
    }

    private BuildableObject PrepareMedicalFacility(Grid grid)
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/Medical/M01_응급처치대.asset");
        Require(definition != null, "Authored M01 missing.");
        BuildableObject existing = world.Buildings.FirstOrDefault(value => value != null && !value.isDestroy
            && value.BuildingData == definition);
        if (existing != null) return existing;
        return PlaceFacility(grid, definition);
    }

    private BuildableObject PlaceFacility(Grid grid, BuildingSO definition)
    {
        RoomLayout layout = scope.Container.Resolve<IRoomLayoutCache>().GetLayout(grid);
        foreach (RoomInstance room in layout.Rooms.Where(value => value != null && value.IsUsable)
            .OrderByDescending(value => value.Cells.Count))
        {
            var cells = new HashSet<Vector2Int>(room.Cells);
            foreach (Vector2Int cell in room.Cells.OrderBy(value => value.x).ThenBy(value => value.y))
            {
                if (definition.GetGridPosList(cell).Any(position => !cells.Contains(position)
                    || grid.GetGridCell(position) == null || !grid.GetGridCell(position).CanBuildInArea(definition)
                    || grid.GetGridCell(position).HasOccupantInLayer(definition.Placement.Layer))) continue;
                var controller = scope.Container.Resolve<IDungeonGridBuildingControllerProvider>().Controller;
                Require(controller.TryPlaceInitialBuildings(new[] { new InitialBuildInfo { Building = definition, Position = cell } }, out string failure), failure);
                scope.Container.Resolve<IRoomLayoutCache>().Clear();
                return world.Buildings.Single(value => value != null && value.BuildingData == definition && value.buildPoses.Contains(cell));
            }
        }
        throw new InvalidOperationException("No authored room permits controlled placement of " + definition.name);
    }

    private void DeliverMedicine(CharacterActor carrier, BuildableObject facility, string medicineId, int expectedQuantity)
    {
        string destination = CharacterConsumablesInputDestinationIdentity.Build(CharacterConsumablesInputKind.MedicalTreatment,
            facility.RequirePersistentInstanceId(), new ConsumableItemDefinitionId(medicineId));
        DeliverInput(carrier, facility, medicineId, destination, expectedQuantity);
    }

    private void DeliverInput(CharacterActor carrier, BuildableObject facility, string medicineId, string destination, int expectedQuantity)
    {
        CharacterCarryInventory carry = carrier.CarryInventory;
        Require(carry != null && carry.Items.Count == 0, "Controlled carrier has no empty real inventory.");
        Require(items.TryReserveBestHaulPlan(carrier, out WorldItemHaulPlan plan, out string planFailure), planFailure);
        Require(plan.PrimaryDestinationId == destination && plan.ReservedStackQuantities.Count == 1
            && plan.ReservedStackQuantities[0].Quantity == 1, "Actual haul planner selected another route; do not replace its result.");
        WorldItemReservedStackQuantity leg = plan.ReservedStackQuantities[0];
        Require(items.TryPickupReservedStackQuantity(carrier, carry, leg, out int picked, out string pickupFailure)
            && picked == 1, pickupFailure);
        Require(items.TryCommitHaulPickup(leg.OwnerOperationId, carry, out string commitFailure), commitFailure);
        Require(items.TryDepositCarriedItemsToFacility(carrier, carry, facility.centerPos, destination,
            new[] { leg.OwnerOperationId }, out string depositFailure), depositFailure);
        Require(scope.Container.Resolve<ItemQuantityReservationService>().ReleaseByOwner(leg.OwnerOperationId,
                ItemReservationReleaseReason.Completed) > 0 && items.ReleaseHaulDeliveryIntent(leg.OwnerOperationId),
            "Completed medical haul retained a reservation or delivery intent.");
        Require(carry.Items.Count == 0 && MedicineQuantity(medicineId) == expectedQuantity,
            "Controlled physical delivery changed the medicine quantity.");
    }

    private void PauseActors()
    {
        foreach (CharacterActor actor in world.Characters.Where(value => value != null))
        {
            actor.Brain?.StopCurrentActionForReplan("qa-medical-controlled-command");
            actor.SetAiPaused(true);
        }
    }
    private CharacterActor Actor(string id) => world.Characters.Single(value => value != null && value.Identity.PersistentId == id);
    private int MedicineQuantity(string itemId = null) => items.GetAllStacks().Where(value => value.ItemId == (itemId ?? report.medicineId)).Sum(value => value.Quantity);
    private float Severity() => food.Capture().health.Single(value => value.persistentId == report.actorId).severity;
    [Serializable] private sealed class OutcomeSet { public List<GameplayOutcomeSnapshot> items; }
    private string TerminalJson()
    {
        report.terminals = ReadTerminals(report.serviceSessionId);
        return JsonUtility.ToJson(new OutcomeSet { items = report.terminals });
    }
    private List<GameplayOutcomeSnapshot> ReadTerminals(string sessionId) => ReadOutcomes(value =>
        value.outcomeTypeId == ServiceSessionOutcomeIds.Terminal.Value && value.facts.Any(fact =>
            fact.factId == ServiceSessionOutcomeIds.SessionIdFact.Value && fact.value == sessionId));
    private List<GameplayOutcomeSnapshot> ReadOutcomes(Func<GameplayOutcomeSnapshot, bool> predicate)
    {
        var result = new List<GameplayOutcomeSnapshot>();
        var query = scope.Container.Resolve<IGameplayOutcomeQuery>();
        OutcomeCursor cursor = OutcomeCursor.FirstPage(200);
        while (true)
        {
            GameplayOutcomeQueryPage page = query.GetGlobal(cursor, OutcomeFilter.All);
            if (page.Items.Count == 0) break;
            result.AddRange(page.Items.Where(value => value.Exact != null && predicate(value.Exact)).Select(value => value.Exact));
            cursor = page.NextCursor;
        }
        return result;
    }
    private void WriteNew(string name, string contents)
    {
        using var stream = new FileStream(Path.Combine(output, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contents);
    }
    private void CaptureLog(string text, string stack, LogType type)
    {
        if (type == LogType.Warning) report.warnings.Add(text);
        else if (type is LogType.Error or LogType.Exception or LogType.Assert) report.errors.Add(text + "\n" + stack);
    }
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
#endif
