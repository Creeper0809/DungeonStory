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

// Root-owned cross-domain witness. Authored placement, physical stock and the
// delivery request are controlled setup; pickup, travel and Refuel are real AI.
public sealed class WimFacilityFuelLiveRunner
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-facility-fuel-live.txt";
    private static bool running;
    private readonly List<string> lines = new();
    private DungeonRuntimeLifetimeScope scope;
    private ICharacterAiWorldRegistry world;
    private IWorldItemStackRuntime items;
    private IGameClock clock;
    private IGameTimeScaleController timeScale;
    private string workerId, firstId, secondId, fuelId, destination;
    private int completions;

    public static string StartFocused()
    {
        Require(Application.isPlaying && !running, "Start once in fresh disposable main Play.");
        var scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main runtime is not initialized.");
        var saves = scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(saves != null, "Cannot protect user save files.");
        saves.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        var game = FindFirstObjectByType<GameManager>();
        Require(game != null, "Missing actual main coroutine host.");
        game.isPause = true;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        try { game.StartCoroutine(new WimFacilityFuelLiveRunner().Observe()); }
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
            if (value is IEnumerator nested) stack.Push(nested);
            else yield return value;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        Pause();
        if (failure != null && workerId != null)
        {
            try { lines.Add(Diagnostic()); }
            catch (Exception error) { lines.Add("diagnostic=" + error.Message); }
        }
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add("scope=actual owner/party UI, authored E01, separated physical AI haul and priority Refuel, current full-world JSON; placement/healthy staff/initial stock/request/typed next action are controlled; not natural priority, full-day fuel balance, pending-ACK fault injection or six-adult proof");
        lines.Add("cleanup=operator stops disposable Play; no scene/user persistence writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        running = false;
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        clock = scope.Container.Resolve<IGameClock>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        var owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Missing owner preparation.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                .TryReconcileNewRunTierZero(out var expansion, out string failure)
                && expansion.CurrentInteriorColumns == 29, "TierZero: " + failure);
            var button = Resources.FindObjectsOfTypeAll<Button>().SingleOrDefault(x => x != null
                && x.gameObject.scene.isLoaded && x.gameObject.activeInHierarchy && x.name == "OwnerOption_1001");
            Require(button != null && button.IsInteractable()
                && PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, Vector2.zero), "Actual owner UI unavailable.");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null, "Party UI did not publish owner.");
        }
        Require(world.TryGetGrid(out Grid grid) && grid != null, "Missing main grid.");
        var worker = world.Characters.Where(x => x != null && !x.IsOwner && !x.IsDead
            && x.characterType == CharacterType.NPC && x.Brain != null
            && x.GetComponent<AbilityHaul>() != null && x.GetComponent<AbilityWork>() != null)
            .OrderBy(x => x.Identity.PersistentId, StringComparer.Ordinal).FirstOrDefault();
        Require(worker != null, "No actual staff with haul and work capabilities.");
        workerId = worker.Identity.PersistentId;
        foreach (var actor in world.Characters.Where(x => x != null)) actor.SetAiPaused(true);
        var definition = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/Modular/E01_벽횃불.asset");
        var authored = definition?.GetAbility<BuildingFuelConsumerAbility>();
        Require(authored != null && authored.fuelPerRefuel == 1
            && authored.fuelSecondsPerRefuel == 180 && authored.workSeconds == .6f,
            "Authored E01 is not one exact fuel/180 running seconds/.6 WU.");
        fuelId = authored.fuelItemId;
        firstId = Place(grid, worker, definition).RequirePersistentInstanceId().Value;
        secondId = Place(grid, worker, definition).RequirePersistentInstanceId().Value;
        var supply = scope.Container.Resolve<ISurvivalRefuelSupplyQuery>();
        var refuel = scope.Container.Resolve<ISurvivalRefuelCompletionCommand>();
        Require(supply.TryGetRefuelSupplyPlan(First(), out var plan) && plan.ItemId == fuelId
            && plan.RequiredQuantity == 1, "Actual fuel plan disagrees with authoring.");
        destination = plan.DestinationId;
        foreach (var stock in items.GetAllStacks().Where(x => x.ItemId == fuelId && x.Quantity > 0).ToArray())
            Require(items.SetForbidden(stock.StackId, true), "Cannot hold unrelated fuel during setup.");
        Vector2Int source = SelectSource(grid, worker, First().centerPos);
        int before = Quantity();
        Require(items.SpawnItemAt(fuelId, 1, source, WorldItemStackState.Loose, string.Empty, out int added) && added == 1,
            "Could not publish physical fuel source.");
        int total = Quantity();
        Require(total == before + 1 && !First().HasFacilityFuelSupply && !Second().HasFacilityFuelSupply,
            "Initial fuel/charge mismatch.");
        Require(!refuel.TryEnsureRefuelSupply(null, First(), out bool early, out _) && !early
            && Quantity() == total && !First().HasFacilityFuelSupply,
            "Request consumed remote fuel or powered a facility.");
        ConfigureWorker(false);
        Resume();
        float wall = Time.realtimeSinceStartup, game = clock.Time;
        bool carried = false;
        while (Delivered() < 1 && Time.realtimeSinceStartup - wall < 60 && clock.Time - game < 160)
        {
            carried |= items.GetAllStacks().Any(x => x.ItemId == fuelId && x.State == WorldItemStackState.Carried
                && x.DestinationId == workerId) && items.CaptureHaulDeliveryIntentsByDestination(destination).Count > 0;
            yield return null;
        }
        Pause();
        Require(carried && Delivered() == 1 && Quantity() == total && !First().HasFacilityFuelSupply
            && !Second().HasFacilityFuelSupply, "Physical AI haul/delivery/no-early-charge failed. " + Diagnostic());
        lines.Add("[PASS] actual AI pickup/carry/exact buffer1; source=" + source + "; target=" + First().centerPos
            + "; consumed=0; chargeA/B=0; gameSeconds=" + (clock.Time - game) + "; wallSeconds=" + (Time.realtimeSinceStartup - wall));
        Resume(); wall = Time.realtimeSinceStartup;
        while (Worker().GetComponent<AbilityHaul>().IsHauling && Time.realtimeSinceStartup - wall < 10) yield return null;
        Pause();
        Require(!Worker().GetComponent<AbilityHaul>().IsHauling, "Delivered haul did not close normally.");
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        var pending = saves.FromJson(saves.ToJson(saves.Capture()));
        Require(saves.TryRestore(pending, out var restored) && restored.Success,
            "Delivered pre-work full restore failed: " + string.Join(" | ", restored.Errors));
        Pause(); yield return null;
        Require(Delivered() == 1 && Quantity() == total && !First().HasFacilityFuelSupply
            && !Second().HasFacilityFuelSupply, "Delivered full restore lost buffer or credited unperformed work.");
        lines.Add("[PASS] delivered pre-work current full-world JSON restore preserves exact buffer/quantity and zero A/B charge");
        int sequence = First().FacilityState.nextFuelOperationSequence;
        using (scope.Container.Resolve<IGameEventBus>().Subscribe<WorkCompletedIdentityEvent>(e =>
        {
            if (e.Character.Value == workerId && e.WorkId == BuiltInWorkTypeIds.Refuel.Value) completions++;
        }))
        {
            ConfigureWorker(true);
            Resume(); wall = Time.realtimeSinceStartup; game = clock.Time;
            while ((First().FacilityState.nextFuelOperationSequence == sequence || completions == 0)
                && Time.realtimeSinceStartup - wall < 60 && clock.Time - game < 160) yield return null;
            Pause();
        }
        Require(completions == 1 && First().FacilityState.nextFuelOperationSequence == sequence + 1
            && First().FacilityState.pendingFuel.phase == (int)FacilityFuelCommitPhase.None
            && First().HasFacilityFuelSupply && First().FacilityState.remainingFuelGameSeconds > 179
            && First().FacilityState.remainingFuelGameSeconds <= 180 && !Second().HasFacilityFuelSupply
            && Quantity() == total - 1 && Delivered() == 0,
            "Actual Refuel work did not consume/publish/ACK once for A only. " + Diagnostic());
        lines.Add("[PASS] actual priority Refuel completion1; operation+1/ACK; fuel-1; A="
            + First().FacilityState.remainingFuelGameSeconds + "; B=0; gameSeconds=" + (clock.Time - game)
            + "; wallSeconds=" + (Time.realtimeSinceStartup - wall));
        float charge = First().FacilityState.remainingFuelGameSeconds;
        var terminal = saves.FromJson(saves.ToJson(saves.Capture()));
        Require(saves.TryRestore(terminal, out var terminalRestore) && terminalRestore.Success,
            "Charged terminal full restore failed: " + string.Join(" | ", terminalRestore.Errors));
        Pause(); yield return null;
        Require(First().FacilityState.remainingFuelGameSeconds == charge && !Second().HasFacilityFuelSupply
            && Quantity() == total - 1 && First().FacilityState.nextFuelOperationSequence == sequence + 1,
            "Terminal full restore duplicated consumption, charge or operation.");
        foreach (var actor in world.Characters.Where(x => x != null)) actor.SetAiPaused(true);
        Resume(); wall = Time.realtimeSinceStartup; game = clock.Time;
        while (clock.Time - game < 1 && Time.realtimeSinceStartup - wall < 15) yield return null;
        Pause();
        Require(clock.Time - game >= 1 && First().FacilityState.remainingFuelGameSeconds < charge
            && First().HasFacilityFuelSupply && !Second().HasFacilityFuelSupply && Quantity() == total - 1,
            "Actual running clock did not drain only local charge without another physical debit.");
        lines.Add("[PASS] terminal current full-world restore exact; actual clock local-charge drain; no second fuel debit or B activation");
    }

    private BuildableObject Place(Grid grid, CharacterActor worker, BuildingSO definition)
    {
        Require(scope.Container.Resolve<IWorldDropZoneQuery>().TryGetDeliveryDropoff(out var origin), "Missing dropoff.");
        var traversal = GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(worker));
        var access = scope.Container.Resolve<IGridTraversalAccessQuery>();
        var costs = scope.Container.Resolve<IGridTraversalCostPolicy>();
        Vector2Int? anchor = null;
        foreach (var room in scope.Container.Resolve<IRoomLayoutCache>().GetLayout(grid).Rooms
            .Where(x => x.IsUsable).OrderBy(x => x.Bounds.yMin).ThenBy(x => x.Bounds.xMin))
        {
            anchor = room.Cells.OrderBy(x => x.x).ThenBy(x => x.y).Where(p =>
                definition.GetGridPosList(p).All(c => room.ContainsCell(c) && grid.GetGridCell(c) is GridCell cell
                    && cell.CanBuildInArea(definition) && cell.CanOccupy(definition.Placement.Layer))
                && grid.IsWalkable(p) && grid.SearchPathTo(origin, p,
                    c => access.CanTraverse(grid, c, traversal, out _), costs, traversal).GetMoveCostTo(p) != int.MaxValue)
                .Select(p => (Vector2Int?)p).FirstOrDefault();
            if (anchor.HasValue) break;
        }
        Require(anchor.HasValue, "No reachable legal E01 footprint in an existing usable room.");
        var value = scope.Container.Resolve<IGridBuildingObjectFactory>().Create(grid, definition, anchor.Value);
        Require(value != null, "Actual building factory rejected E01.");
        foreach (var component in value.GetComponentsInChildren<MonoBehaviour>(true)) scope.Container.Inject(component);
        value.SetGrid(grid); value.Initialization(definition, anchor.Value);
        Require(grid.RegisterOccupant(value, definition.Placement.Layer, definition.GetGridPosList(anchor.Value),
            definition.Placement.IsMovement) && scope.Container.Resolve<IBuildingWorldQuery>().Buildings.Contains(value),
            "E01 is not registered in the actual grid/world.");
        return value;
    }

    private Vector2Int SelectSource(Grid grid, CharacterActor worker, Vector2Int target)
    {
        var traversal = GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(worker));
        var access = scope.Container.Resolve<IGridTraversalAccessQuery>();
        var costs = scope.Container.Resolve<IGridTraversalCostPolicy>();
        var source = grid.GetCells().Select(x => x.Position).Where(p => grid.IsWalkable(p)
            && Mathf.Abs(p.x - target.x) + Mathf.Abs(p.y - target.y) >= 4)
            .OrderBy(p => Mathf.Abs(p.x - target.x) + Mathf.Abs(p.y - target.y)).ThenBy(p => p.y).ThenBy(p => p.x)
            .Where(p => grid.SearchPathTo(target, p, c => access.CanTraverse(grid, c, traversal, out _), costs, traversal)
                .GetMoveCostTo(p) != int.MaxValue).Select(p => (Vector2Int?)p).FirstOrDefault();
        Require(source.HasValue, "No reachable separated source; same-cell substitution forbidden.");
        return source.Value;
    }

    private void ConfigureWorker(bool refuel)
    {
        var worker = Worker();
        foreach (var actor in world.Characters.Where(x => x != null)) actor.SetAiPaused(actor != worker);
        foreach (var need in new[] { CharacterCondition.HUNGER, CharacterCondition.THIRST, CharacterCondition.SLEEP,
            CharacterCondition.HYGIENE, CharacterCondition.EXCRETION, CharacterCondition.FUN })
            worker.Stats.ChangesStat(need, 100 - worker.Stats.GetConditionValue(need, 0));
        var work = worker.GetComponent<AbilityWork>();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
        work.SetWorkPriority(BuiltInWorkTypeIds.Refuel, refuel ? WorkPriorityLevel.Priority1 : WorkPriorityLevel.Off);
        if (refuel)
        {
            Require(work.TrySetPriorityWorkTarget(First(), BuiltInWorkTypeIds.Refuel, null, out string failure),
                "Actual priority Refuel command failed: " + failure);
            Require(worker.Brain.PreferActionOnNextDecision<AIWork>(), "Staff has no actual work action.");
        }
        else Require(worker.Brain.PreferActionOnNextDecision<AIHaul>(), "Staff has no actual haul action.");
        worker.Brain.RequestImmediateReplan(clearFailures: true);
        Require(worker.CanRunAi, "Staff cannot run after normal setup.");
    }

    private CharacterActor Worker() => world.Characters.Single(x => x != null && x.Identity.PersistentId == workerId);
    private BuildableObject First() => Building(firstId);
    private BuildableObject Second() => Building(secondId);
    private BuildableObject Building(string id) => scope.Container.Resolve<IBuildingWorldQuery>().Buildings
        .Single(x => x != null && x.RequirePersistentInstanceId().Value == id);
    private int Quantity() => items.GetAllStacks().Where(x => x.ItemId == fuelId).Sum(x => x.Quantity);
    private int Delivered() => items.GetAllStacks().Where(x => x.ItemId == fuelId
        && x.State == WorldItemStackState.FacilityBuffer && x.DestinationId == destination).Sum(x => x.Quantity);
    private string Diagnostic() => "worker=" + workerId + "; action=" + Worker().Brain.bestAction?.actionset?.Branch
        + "; phase=" + Worker().Brain.CurrentActionPhase + "; failure=" + Worker().Brain.LastActionFailure
        + "; hauling=" + Worker().GetComponent<AbilityHaul>().IsHauling + "; delivered=" + Delivered();
    private void Pause() { var game = FindFirstObjectByType<GameManager>(); if (game != null) game.isPause = true; if (timeScale != null) timeScale.Scale = 0; }
    private void Resume() { FindFirstObjectByType<GameManager>().isPause = false; timeScale.Scale = 2; }
    private static void Require(bool value, string failure) { if (!value) throw new InvalidOperationException(failure); }
}
#endif
