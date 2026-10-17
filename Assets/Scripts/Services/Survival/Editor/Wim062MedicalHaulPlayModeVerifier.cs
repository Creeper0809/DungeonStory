#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEngine;
using VContainer;

// One bounded integration witness, run only in a protected, disposable main Play session.
// The patient is a validated current-format checkpoint; disease generation is not tested here.
public static class Wim062MedicalHaulPlayModeVerifier
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-062-main-haul-ui.txt";
    private static readonly List<string> report = new();
    private static readonly HashSet<Vector2Int> visited = new();
    private static ICharacterAiWorldRegistry world;
    private static ISurvivalFoodPersistence survival;
    private static ISurvivalTreatmentSupplyQuery supply;
    private static IWorldItemStackRuntime items;
    private static CharacterActor patient;
    private static CharacterActor carrier;
    private static AbilityHaul haul;
    private static BuildableObject facility;
    private static CharacterSummaryInfo view;
    private static GridSystemManager gridManager;
    private static Action<WorldItemHaulPlan> previousHook;
    private static string medicineId;
    private static string destinationId;
    private static int initialQuantity;
    private static bool blocked;
    private static bool carriedObserved;
    private static bool noPathObserved;
    private static Vector2Int blockedCell;
    private static GridCellAreaType originalArea;
    private static double startedWall;
    private static float startedGame;
    private static string outcome = "NOT_STARTED";

    public static string Status => outcome + "; phase="
        + (noPathObserved ? "recovery" : blocked ? "blocked-delivery" : "awaiting-haul")
        + "; wall=" + (EditorApplication.timeSinceStartup - startedWall).ToString("0.0")
        + "; game=" + (Time.time - startedGame).ToString("0.0")
        + "; carry=" + carriedObserved + "; visited=" + visited.Count
        + "; haul=" + (haul == null ? "none" : haul.CurrentExecutionStage)
        + "; failure=" + (haul == null ? "none" : haul.LastFailureReason);

    public static string Start(DungeonRuntimeLifetimeScope scope)
    {
        Require(outcome == "NOT_STARTED", "use a new disposable Play session; do not retry a mutated witness");
        Require(Application.isPlaying && Time.timeScale == 0f && scope?.Container != null,
            "requires paused game time in protected main Play after StartParty");
        report.Clear();
        visited.Clear();
        blocked = carriedObserved = noPathObserved = false;
        outcome = "RUNNING";
        startedWall = EditorApplication.timeSinceStartup;
        startedGame = Time.time;
        report.Add("WIM062 main physical delivery / AI / Health UI");
        report.Add("setup=actual StartParty, reachable existing medical facility and stock command; validated current-format patient checkpoint");
        report.Add("intervention=actual grid delivery cell blocked after haul reservation; no injected path result");
        report.Add("not-tested=natural disease generation, whole-world restore, long-run balance; prior core receipt/ACK evidence retained");
        try
        {
            world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
            survival = scope.Container.Resolve<ISurvivalFoodPersistence>();
            supply = scope.Container.Resolve<ISurvivalTreatmentSupplyQuery>();
            items = scope.Container.Resolve<IWorldItemStackRuntime>();
            gridManager = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
            var actors = world.Characters.Where(a => a != null && !a.IsDead)
                .OrderBy(a => CharacterPersistentIdentity.Require(a).Value, StringComparer.Ordinal).ToArray();
            Require(actors.Length >= 2 && gridManager?.grid != null, "main party/grid not ready");
            patient = actors[0];
            carrier = actors[1];
            haul = carrier.GetComponent<AbilityHaul>();
            Require(haul != null && carrier.Brain != null, "real carrier haul/brain missing");
            foreach (var actor in actors)
            {
                var work = actor.GetComponent<AbilityWork>();
                Require(work != null, "work priority command missing");
                actor.Brain.StopCurrentActionForReplan("WIM062 controlled work schedule");
                work.ClearPriorityWorkTarget();
                work.SetWorkPriority(BuiltInWorkTypeIds.Treat, WorkPriorityLevel.Off);
                work.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Off);
            }
            var carrierWork = carrier.GetComponent<AbilityWork>();
            foreach (var id in BuiltInWorkTypeIds.All)
                carrierWork.SetWorkPriority(id, id == BuiltInWorkTypeIds.Haul
                    ? WorkPriorityLevel.Priority1 : WorkPriorityLevel.Off);
            carrierWork.SetDutyState(AbilityWork.DutyState.OnDuty);

            var warehouse = world.Warehouses.FirstOrDefault(w => w != null && w.HasWarehouseInventory
                && w.Inventory.Accepts(StockCategory.Medicine) && w.Inventory.HasMassCapacityAuthority);
            Require(warehouse != null, "main initial warehouse absent; no synthetic warehouse substitution");
            // Do not place an unrelated test facility in the first empty cell:
            // an empty interior pocket can be separated from the live corridor.
            foreach (var candidate in world.Buildings.Where(b => b != null && !b.isDestroy
                    && b.Grid == gridManager.grid
                    && b.BuildingData?.GetAbility<BuildingMedicalAbility>()?.requiresMedicine == true)
                .OrderBy(b => (b.centerPos - carrier.GetNowXY()).sqrMagnitude)
                .ThenBy(b => b.RequirePersistentInstanceId().Value, StringComparer.Ordinal))
            {
                if (!candidate.TryGetNearestWorkAccessGridPosition(gridManager.grid,
                        carrier.GetNowXY(), out var access)
                    || !gridManager.grid.SearchPathTo(carrier.GetNowXY(), access).ContainsPosition(access)) continue;
                facility = candidate;
                break;
            }
            Require(facility != null && facility.Grid == gridManager.grid && !facility.isDestroy,
                "no reachable actual registered treatment facility is available in main setup");
            report.Add("facility-definition=" + facility.BuildingData.name);
            Require(facility.TryGetNearestWorkAccessGridPosition(gridManager.grid,
                    carrier.GetNowXY(), out Vector2Int workAccess),
                "prepared medical facility has no walkable work access");
            bool accessReachable = gridManager.grid.SearchPathTo(carrier.GetNowXY(), workAccess)
                .ContainsPosition(workAccess);
            report.Add("facility-center=" + facility.centerPos + "; work-access=" + workAccess
                + "; carrier=" + carrier.GetNowXY() + "; access-reachable=" + accessReachable);
            Require(accessReachable, "prepared facility work access is not reachable before intervention");
            var ownerSource = scope.Container.Resolve<ICharacterConsumablesInputOwnerDescriptorSource>();
            bool reconciled = scope.Container.Resolve<ICharacterConsumablesInputOwnerRuntime>()
                .TryReconcileLive(ownerSource.BuildLiveInputOwnerDescriptors(),
                    "wim062-main-placed-facility", out string ownerFailure);
            Require(reconciled, "actual medical input owner/capacity not ready: " + ownerFailure);

            var definition = scope.Container.Resolve<IItemDefinitionCatalog>().All
                // This witness tests transport, not medicine ranking. Ordinary Treat
                // accepts the Medicine stock category, not only the surgical feature.
                .Where(d => d != null && d.StockCategory == StockCategory.Medicine)
                .OrderBy(d => d.ItemId, StringComparer.Ordinal).First();
            medicineId = definition.ItemId;
            int before = PhysicalQuantity();
            report.Add("warehouse=" + warehouse.PersistentInstanceId.Value
                + "; category=" + warehouse.Inventory.AcceptedCategory + "; medicine=" + medicineId);
            bool spawned = scope.Container.Resolve<WorldItemWarehouseService>().SpawnItemStock(
                warehouse, medicineId, 1, "qa:wim062:main-supply", "generic:" + medicineId,
                out int quantity, out _, out DomainFailure spawnFailure);
            Require(spawned && quantity == 1, "actual warehouse admission failed: "
                + spawnFailure.Code + "; " + string.Join(";", spawnFailure.Parameters.ToArray()));
            initialQuantity = before + 1;
            Require(PhysicalQuantity() == initialQuantity, "warehouse spawn conservation failed");

            var checkpoint = survival.Capture();
            var patientId = CharacterPersistentIdentity.Require(patient);
            checkpoint.health.RemoveAll(h => h != null && h.persistentId == patientId.Value);
            checkpoint.health.Add(new SurvivalHealthSaveData { persistentId = patientId.Value,
                state = SurvivalHealthState.Sick, severity = 0.8f, remainingSeconds = 360f,
                source = "wim062-controlled-current-checkpoint" });
            survival.PublishRestoreCandidate(survival.BuildRestoreCandidate(checkpoint));
            var command = survival as ISurvivalTreatmentCompletionCommand;
            Require(command != null, "registered survival authority lacks treatment command");
            bool ready = command.TryEnsureTreatmentSupply(patient.BuildingVisitor, facility, 620062,
                out bool completionOnly, out DomainFailure requestFailure);
            Require(!ready && !completionOnly, "warehouse-only medicine bypassed delivery");
            bool foundSupply = supply.TryGetTreatmentSupply(patientId, out var initial);
            report.Add("request-query: found=" + foundSupply + "; expected-facility="
                + facility.RequirePersistentInstanceId().Value + "; observed-facility=" + initial.FacilityId.Value
                + "; item=" + initial.ItemId.Value + "; kind=" + initial.Kind + "; state=" + initial.State
                + "; destination=" + initial.DestinationId);
            Require(foundSupply
                && initial.FacilityId.Equals(facility.RequirePersistentInstanceId())
                && initial.ItemId.Value == medicineId && initial.Kind == SurvivalTreatmentKind.Standard
                && initial.State == SurvivalTreatmentSupplyState.Requested,
                "actual treatment request did not target prepared facility/medicine: " + requestFailure.Code
                + "; " + string.Join(";", requestFailure.Parameters.ToArray()));
            destinationId = initial.DestinationId;
            var requestedStacks = items.GetAllStacks().Where(s => s.DestinationId == destinationId
                && s.ItemId == medicineId && s.Quantity > 0).ToArray();
            Require(requestedStacks.Sum(s => s.Quantity) == 1,
                "requested medical destination does not own exactly one physical medicine");
            var reachability = scope.Container.Resolve<IWorldItemDeliveryReachabilityQuery>();
            foreach (var stack in requestedStacks)
            {
                var reachable = reachability.AssessExactStackDelivery((ItemStackId)stack.StackId,
                    stack.Quantity, stack.DestinationPosition, destinationId, out string reachFailure);
                report.Add("delivery-preflight=" + reachable + "; reason=" + reachFailure
                    + "; source=" + stack.Position + "; drop=" + stack.DestinationPosition);
                Require(reachable != WorldItemDeliveryReachabilityStatus.Unreachable
                    && reachable != WorldItemDeliveryReachabilityStatus.Invalid,
                    "prepared exact medicine route is unavailable before intervention: " + reachFailure);
            }
            view = UnityEngine.Object.FindFirstObjectByType<CharacterSummaryInfo>(FindObjectsInactive.Include);
            Require(view != null, "player Character Summary view missing");
            AssertUi("Requested");
            report.Add("[PASS] warehouse-only -> actual requested destination=" + destinationId + "; item=" + medicineId);
            previousHook = haul.DebugBeforeHaulRoutineStart;
            Require(previousHook == null, "another haul verifier owns the existing hook");
            haul.DebugBeforeHaulRoutineStart = BeforeHaul;
            EditorApplication.update += Tick;
            carrier.SetAiPaused(false);
            carrier.Brain.PreferActionOnNextDecision<AIHaul>();
            carrier.Brain.RequestImmediateReplan(clearFailures: true);
            Time.timeScale = 1f;
            return Status;
        }
        catch (Exception e) { Finish(e); return Status; }
    }

    private static void BeforeHaul(WorldItemHaulPlan plan)
    {
        report.Add("haul-plan=" + haul.RuntimeHaulStartCount + "; legs="
            + string.Join(" | ", plan.DeliveryLegs.Select(l => l.DestinationId
                + ":" + l.PickupStandPosition + "->" + l.DeliveryPosition)));
        if (blocked || noPathObserved || !plan.DeliveryLegs.Any(l => l.DestinationId == destinationId)) return;
        var leg = plan.DeliveryLegs.First(l => l.DestinationId == destinationId);
        var cell = gridManager.grid.GetGridCell(leg.DeliveryPosition);
        Require(cell != null && carrier.GetNowXY() != cell.Position
            && cell.GetOccupant(GridLayer.Character) == null
            && leg.PickupStandPosition != leg.DeliveryPosition,
            "delivery obstruction would overlap carrier/pickup; invalid witness setup");
        originalArea = cell.AreaType;
        blockedCell = cell.Position;
        gridManager.grid.SetAreaType(blockedCell, GridCellAreaType.BlockedExterior);
        gridManager.NotifyGridObjectChanged();
        blocked = true;
        report.Add("actual-haul-start=" + carrier.Identity.PersistentId + "; blocked=" + blockedCell);
    }

    private static void Tick()
    {
        if (outcome != "RUNNING") return;
        try
        {
            Require(Application.isPlaying, "Play stopped before witness completed");
            visited.Add(carrier.GetNowXY());
            carriedObserved |= carrier.CarryInventory.Items.Any(i => i != null && i.itemId == medicineId && i.quantity > 0);
            Require(PhysicalQuantity() == initialQuantity, "medicine lost/duplicated/consumed before delivered treatment");
            Require(survival.Capture().health.Any(h => h != null
                && h.persistentId == patient.Identity.PersistentId && Mathf.Approximately(h.severity, 0.8f)),
                "patient healed before any authorized treatment completion");
            Require(supply.TryGetTreatmentSupply(CharacterPersistentIdentity.Require(patient), out var state),
                "pending treatment disappeared from read model");
            if (!noPathObserved && state.State == SurvivalTreatmentSupplyState.NoPath)
            {
                Require(blocked && haul.TryGetLastDeliveryPathFailure(out var failure)
                    && failure.DestinationId == destinationId && failure.ItemId == medicineId,
                    "NoPath did not originate from real medical haul");
                AssertUi("NoPath");
                report.Add("[PASS] real movement failure -> typed NoPath -> player Health UI; quantity preserved");
                noPathObserved = true;
                RestoreCell();
                carrier.Brain.PreferActionOnNextDecision<AIHaul>();
                carrier.Brain.RequestImmediateReplan(clearFailures: true);
            }
            if (noPathObserved && state.State == SurvivalTreatmentSupplyState.Ready)
            {
                Require(carriedObserved && visited.Count >= 2 && haul.RuntimeHaulStartCount > 0,
                    "ready result has no observed real pickup/movement evidence");
                Require(items.GetAllStacks().Any(s => s.ItemId == medicineId && s.Quantity > 0
                    && s.State == WorldItemStackState.FacilityBuffer && s.DestinationId == destinationId),
                    "medicine not physically in exact treatment buffer");
                AssertUi("Ready");
                report.Add("[PASS] access restored -> real AI carry/movement/deposit -> Ready Health UI");
                Finish(null);
                return;
            }
            Require(EditorApplication.timeSinceStartup - startedWall < 240d && Time.time - startedGame < 120f,
                "bounded witness exceeded time; " + Status + "; supply=" + state.State);
        }
        catch (Exception e) { Finish(e); }
    }

    private static int PhysicalQuantity() => items.GetAllStacks()
        .Where(s => s.ItemId == medicineId && s.State != WorldItemStackState.Carried).Sum(s => s.Quantity)
        + world.Characters.Where(a => a != null && a.CarryInventory != null)
            .Sum(a => a.CarryInventory.Items.Where(i => i != null && i.itemId == medicineId).Sum(i => i.quantity));

    private static void AssertUi(string expectedState)
    {
        view.OnTriggerEvent(new InfoFeedEvent(patient));
        view.ShowHealthTab();
        var label = view.UI?.transform.Find(
            "CharacterSummaryGeneratedView/Content/HealthContent/HealthContentViewport/HealthSummaryText")?.GetComponent<TMP_Text>();
        // Explicit expected keys, not the enum formatter whose mapping is under test.
        string kind = CharacterSummaryUiTextQuery.Get("CharacterSummary.Health.Consumables.TreatmentSupply.Kind.Standard");
        string status = CharacterSummaryUiTextQuery.Get("CharacterSummary.Health.Consumables.TreatmentSupply.State." + expectedState);
        string expected = CharacterSummaryUiTextQuery.Get("CharacterSummary.Health.Consumables.TreatmentSupply.Row", kind, status);
        Require(label != null && label.gameObject.activeInHierarchy && view.UI.activeInHierarchy
            && !expected.Contains("CharacterSummary.") && label.text.Contains(expected, StringComparison.Ordinal),
            "player Health UI did not render exact " + expectedState + " row: " + expected);
        report.Add("ui=" + expected);
    }

    private static void RestoreCell()
    {
        if (!blocked || gridManager?.grid == null) return;
        gridManager.grid.SetAreaType(blockedCell, originalArea);
        gridManager.NotifyGridObjectChanged();
        blocked = false;
    }

    private static void Finish(Exception failure)
    {
        EditorApplication.update -= Tick;
        Time.timeScale = 0f;
        if (haul != null) haul.DebugBeforeHaulRoutineStart = previousHook;
        RestoreCell();
        outcome = failure == null ? "PASS" : "FAIL";
        report.Add(outcome + (failure == null ? string.Empty : ": " + failure));
        if (items != null && !string.IsNullOrEmpty(destinationId))
            foreach (var stack in items.GetAllStacks().Where(s => s.DestinationId == destinationId))
                report.Add("target-stock=" + stack.StackId + "; item=" + stack.ItemId
                    + "; qty=" + stack.Quantity + "; state=" + stack.State
                    + "; pos=" + stack.Position + "; drop=" + stack.DestinationPosition
                    + "; reserved=" + stack.ReservedQuantity);
        report.Add("observed-carry=" + carriedObserved + "; visited-cells=" + visited.Count);
        report.Add("wall-seconds=" + (EditorApplication.timeSinceStartup - startedWall).ToString("0.000")
            + "; game-seconds=" + (Time.time - startedGame).ToString("0.000"));
        report.Add("cleanup=hook/cell restored; time paused; operator MUST Stop disposable protected Play, no scene/save write");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllLines(ReportPath, report);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException("WIM062 " + reason);
    }
}
#endif
