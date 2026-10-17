#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static UnityEngine.Object;

// A short main-scene witness. Only setup is controlled; runtime selects and walks.
public sealed class Wim019WildlifeMigrationLiveRunner
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-019-migration-live.txt";
    private readonly List<string> lines = new();
    private IGameTimeScaleController timeScale;
    private static bool isRunning;

    public static string StartFocused()
    {
        Require(Application.isPlaying && !isRunning,
            "Start once in fresh protected main Play.");
        var scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null && FindFirstObjectByType<OwnerRunManager>() != null, "Main preparation unavailable.");
        var saves = scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(saves != null, "Cannot isolate user save writes.");
        saves.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        FindFirstObjectByType<GameManager>().isPause = true;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        // Editor-only types cannot be attached as MonoBehaviours. Use the
        // already-live main-scene host for this test-only iterator.
        isRunning = true;
        try
        {
            FindFirstObjectByType<GameManager>().StartCoroutine(new Wim019WildlifeMigrationLiveRunner().Observe());
        }
        catch
        {
            isRunning = false;
            File.WriteAllText(ReportPath, "result=FAIL; coroutine launch failed\n");
            throw;
        }
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Observe()
    {
        var iterators = new Stack<IEnumerator>();
        iterators.Push(Run());
        Exception failure = null;
        while (iterators.Count > 0)
        {
            object yielded = null;
            bool moved;
            try { moved = iterators.Peek().MoveNext(); if (moved) yielded = iterators.Peek().Current; }
            catch (Exception error) { failure = error; break; }
            if (!moved) { (iterators.Pop() as IDisposable)?.Dispose(); continue; }
            if (yielded is IEnumerator nested) iterators.Push(nested);
            else yield return yielded;
        }
        while (iterators.Count > 0) (iterators.Pop() as IDisposable)?.Dispose();
        Pause();
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add("scope=controlled autumn calendar and physical source/arrival; other staff AI paused; main wildlife cadence/decision/path/motion unchanged; no natural spawn frequency, winter progression, population balance or six-adult claim");
        lines.Add("cleanup=stop disposable Play; no scene/profile/save file writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        isRunning = false;
    }

    private IEnumerator Run()
    {
        var scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        var owner = FindFirstObjectByType<OwnerRunManager>();
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                .TryReconcileNewRunTierZero(out _, out string expansionFailure), expansionFailure);
            var button = Resources.FindObjectsOfTypeAll<Button>().SingleOrDefault(value => value != null
                && value.gameObject.scene.isLoaded && value.gameObject.activeInHierarchy && value.name == "OwnerOption_1001");
            Require(button != null && button.IsInteractable()
                && PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, Vector2.zero), "Actual owner UI unavailable.");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null, "Normal party UI did not commit.");
        }
        var world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        Require(world.TryGetGrid(out Grid grid) && grid != null, "Main grid unavailable.");
        foreach (var staff in world.Characters.Where(value => value != null)) staff.SetAiPaused(true);
        var calendar = scope.Container.Resolve<IGameCalendar>();
        calendar.SetDateTime(61, 8); // Existing typed command; no fabricated daily event/boss cycles.
        Require(calendar.Season == Season.Autumn, "Authored scavenger active season was not prepared.");
        var wildlife = scope.Container.Resolve<WildlifeRuntime>();
        var items = scope.Container.Resolve<IWorldItemStackRuntime>();
        var clock = scope.Container.Resolve<IGameClock>();
        var start = grid.GetCells().Where(cell => WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, cell))
            .Select(cell => cell.Position).OrderByDescending(p => p.x).ThenBy(p => p.y)
            .Where(p => Enumerable.Range(0, 5).All(i =>
                WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, grid.GetGridCell(p + new Vector2Int(i, 0)))))
            .Where(p => wildlife.Wildlife.All(a => a == null || !a.IsAlive
                || (Distance(a.GridPosition, p) > 4 && Distance(a.GridPosition, p + new Vector2Int(4, 0)) > 4)))
            .Select(p => (Vector2Int?)p).FirstOrDefault();
        Require(start.HasValue, "No unoccupied outdoor five-cell approach for this focused witness.");
        Vector2Int destination = start.Value + new Vector2Int(4, 0);
        var environment = scope.Container.Resolve<IEnvironmentalFieldQuery>();
        var ports = scope.Container.Resolve<WildlifeEcosystemApplicationPorts>();
        Require(environment.TryGetCell(destination, out var environmentCell)
            && ports.TryGetTemperatureC(destination, out float temperature)
            && temperature == environmentCell.TemperatureC,
            "Main wildlife temperature port is not reading the current environmental authority.");
        lines.Add("main-temperature-port=PASS; cell=" + destination + "; temperatureC=" + environmentCell.TemperatureC);
        Require(items.SpawnItemAt("wild:carcass:shadow_hare", 1, destination, WorldItemStackState.Loose,
            string.Empty, out int spawned) && spawned == 1, "Authored physical carcass spawn failed.");
        var source = items.GetAllStacks().SingleOrDefault(x => x.ItemId == "wild:carcass:shadow_hare"
            && x.Position == destination && x.State == WorldItemStackState.Loose);
        Require(source != null && source.Quantity == 1 && !source.Forbidden, "Physical source is not exact and observable.");
        Require(wildlife.TrySpawnArrival("carrion_drake", start.Value, out var animal, out string arrivalFailure)
            && animal != null && animal.GridPosition == start.Value, arrivalFailure);
        animal.ChangeHunger(-1f);
        animal.ChangeThirst(-1f);
        Require(animal.Hunger == 0 && animal.Thirst == 0 && animal.Species.IsActiveIn(calendar.Season), "Preference witness must be sated and in season.");
        lines.Add("prepared=actual authored carrion_drake; physical carcass=" + source.StackId
            + "; from=" + start.Value + "; to=" + destination + "; initial needs=0/0");
        double wallStart = Time.realtimeSinceStartupAsDouble;
        float gameStart = clock.Time;
        bool observedPath = false;
        FindFirstObjectByType<GameManager>().isPause = false;
        timeScale.Scale = 2;
        while (Time.realtimeSinceStartupAsDouble - wallStart < 25)
        {
            Require(animal != null && animal.IsAlive, "Witness animal lost its live ownership before arrival.");
            if (animal.IsMoving && animal.LastMoveTarget == destination && animal.Intent == WildlifeIntent.Wander
                && animal.IntentReason.Contains("사체", StringComparison.Ordinal))
                observedPath = true;
            if (animal.GridPosition == destination && observedPath) break;
            yield return null;
        }
        Pause();
        Require(observedPath && animal.GridPosition == destination,
            "Actual migration path did not arrive: pos=" + animal.GridPosition + "; target=" + animal.LastMoveTarget
            + "; intent=" + animal.Intent + "; reason=" + animal.IntentReason);
        Require(items.GetAllStacks().Any(x => x.StackId == source.StackId && x.Quantity == 1 && x.Position == destination),
            "Sated preference consumed or relocated the physical source.");
        lines.Add("actual-AI-path=PASS; intent=" + animal.Intent + "; reason=" + animal.IntentReason
            + "; gameSeconds=" + (clock.Time - gameStart) + "; wallSeconds=" + (Time.realtimeSinceStartupAsDouble - wallStart));
        lines.Add("quantity=1 before/1 after; no direct target command, warp, actor Tick or synthetic movement");
    }
    private static int Distance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    private void Pause()
    {
        var game = FindFirstObjectByType<GameManager>();
        if (game != null) game.isPause = true;
        if (timeScale != null) timeScale.Scale = 0;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
