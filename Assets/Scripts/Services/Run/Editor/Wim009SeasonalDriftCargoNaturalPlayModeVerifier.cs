#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using static UnityEngine.Object;

// Root-owned focused integration witness. Seasonal eligibility and the two
// Spring days are controlled, while daily dispatch, physical publication,
// ordinary AI hauling, storage and expiry use the registered production path.
public sealed class Wim009SeasonalDriftCargoNaturalPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-009-seasonal-drift-cargo-natural.txt";

    private const string DefinitionId = "seasonal:spring-drift-cargo";
    private const string ItemId = "material:lumber";
    private static bool running;
    private readonly List<string> lines = new();
    private readonly List<V20ContentEffectsResolvedEvent> resolved = new();
    private DungeonRuntimeLifetimeScope scope;
    private IGameTimeScaleController timeScale;
    private IGameClock clock;
    private ICharacterAiWorldRegistry world;
    private IWorldItemStackRuntime items;
    private V20CampaignRuntime campaign;
    private V20StoryContentCatalog catalog;
    private IGameCalendar calendar;
    private IGameEventBus events;
    private IDisposable resolvedSubscription;

    public static string StartFocused()
    {
        Require(Application.isPlaying && !running,
            "Start once inside a fresh protected main Play session.");
        DungeonRuntimeLifetimeScope current =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(current?.Container != null,
            "Main runtime is not initialized.");
        IDisposable persistence =
            current.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "Cannot protect user save files.");
        persistence.Dispose();
        current.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        GameManager host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Missing main coroutine host.");
        host.isPause = true;
        current.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        try
        {
            host.StartCoroutine(
                new Wim009SeasonalDriftCargoNaturalPlayModeVerifier().Observe());
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
        Stack<IEnumerator> pending = new();
        pending.Push(Run());
        Exception failure = null;
        while (pending.Count > 0)
        {
            object current = null;
            bool moved;
            try
            {
                moved = pending.Peek().MoveNext();
                if (moved) current = pending.Peek().Current;
            }
            catch (Exception error)
            {
                failure = error;
                break;
            }
            if (!moved)
            {
                (pending.Pop() as IDisposable)?.Dispose();
                continue;
            }
            if (current is IEnumerator nested) pending.Push(nested);
            else yield return current;
        }

        while (pending.Count > 0)
            (pending.Pop() as IDisposable)?.Dispose();
        resolvedSubscription?.Dispose();
        resolvedSubscription = null;
        Pause();
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add(
            "scope=controlled Spring eligibility/date through production OperatingDayStartedEvent; actual V20 daily resolution, seasonal application adapter, physical Loose/Carried/Stored ownership, ordinary AI haul, secured preservation and unpicked expiry; not free-running calendar duration, all seasonal definitions, or six-adult balance");
        lines.Add(
            "cleanup=paused disposable protected Play; persistence disabled before owner preparation; operator stops without scene/profile/save writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        running = false;
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null,
            "Main runtime disappeared during the witness.");
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        clock = scope.Container.Resolve<IGameClock>();
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        campaign = scope.Container.Resolve<V20CampaignRuntime>();
        catalog = scope.Container.Resolve<V20StoryContentCatalog>();
        calendar = scope.Container.Resolve<IGameCalendar>();
        events = scope.Container.Resolve<IGameEventBus>();
        resolvedSubscription = events.Subscribe<V20ContentEffectsResolvedEvent>(
            value => resolved.Add(value));
        IDisposable runFlowSubscriptions =
            scope.Container.Resolve<IDungeonRunFlowRuntime>() as IDisposable;
        Require(runFlowSubscriptions != null,
            "Run-flow subscription isolation is unavailable.");
        runFlowSubscriptions.Dispose();
        InvasionThreatRuntime threat = FindFirstObjectByType<InvasionThreatRuntime>();
        Require(threat != null
            && !threat.IsCandidatePending
            && !threat.CapturePersistentState().CandidateRaisedThisCycle,
            "The witness requires a fresh invasion-threat cycle.");
        threat.enabled = false;
        lines.Add(
            "isolation=unrelated run-flow subscriptions and invasion threat producer disabled; V20 daily and seasonal cargo subscribers retained");

        OwnerRunManager owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Owner preparation is unavailable.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                    .TryReconcileNewRunTierZero(out _, out string expansionFailure),
                "Tier-zero preparation failed: " + expansionFailure);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null,
                "Normal party UI did not publish an owner.");
        }

        CharacterActor worker = world.Characters
            .Where(value => value != null
                && !value.IsOwner
                && !value.IsDead
                && value.CurrentLifecycleState == CharacterLifecycleState.Active
                && value.Brain != null
                && value.GetComponent<AbilityHaul>() != null)
            .OrderBy(value => value.Identity.PersistentId,
                StringComparer.Ordinal)
            .FirstOrDefault();
        Require(worker != null, "No active authored staff hauler is available.");
        Require(scope.Container.Resolve<IWarehouseWorldQuery>().Warehouses.Any(
                value => value != null
                    && value.HasWarehouseInventory),
            "The prepared run has no operational physical warehouse.");
        int firstDay = 10;
        resolved.Clear();
        PrepareOnlyTargetForDay(firstDay);
        calendar.SetDateTime(firstDay, 8);
        events.Publish(new OperatingDayStartedEvent(firstDay));
        IV20DailyEvaluationDiagnostic daily =
            scope.Container.Resolve<IV20DailyEvaluationDiagnostic>();
        Require(daily.LastDailyEvaluationAbsoluteDay == firstDay
            && daily.LastDailyEvaluationSucceeded,
            "Production daily dispatcher rejected the controlled Spring day: "
                + daily.LastDailyEvaluationFailure);
        V20ActiveEventSaveData first = campaign.ActiveSeasonalEvents.Single(
            value => string.Equals(value.definitionId, DefinitionId,
                StringComparison.Ordinal));
        Require(resolved.Count(value => string.Equals(
                    value.DefinitionId, DefinitionId, StringComparison.Ordinal)
                && string.Equals(value.ResolutionId, "started",
                    StringComparison.Ordinal)
                && string.Equals(value.OccurrenceInstanceId, first.instanceId,
                    StringComparison.Ordinal)) == 1,
            "The actual daily result did not publish the occurrence-bound Spring cargo event.");
        WorldItemStackSnapshot[] firstCargo = Cargo(first.instanceId);
        Require(first.seasonalDriftCargo.spawned
            && firstCargo.Sum(value => value.Quantity) == 6
            && firstCargo.All(value => value.ItemId == ItemId
                && value.State == WorldItemStackState.Loose),
            "Spring event did not publish exactly six physical Loose lumber units.");
        HashSet<string> firstCargoIds = firstCargo
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (WorldItemStackSnapshot unrelated in items.GetAllStacks().Where(
                     value => value != null
                         && value.State == WorldItemStackState.Loose
                         && !firstCargoIds.Contains(value.StackId)))
            Require(items.SetForbidden(unrelated.StackId, true),
                "Could not isolate an unrelated Loose stack from focused hauling: "
                    + unrelated.StackId);
        lines.Add("[PASS] actual daily dispatch -> Spring occurrence -> exact Loose lumber6");

        ConfigureHauler(worker);
        Resume(4f);
        float wallStart = Time.realtimeSinceStartup;
        float gameStart = clock.Time;
        bool observedCarried = false;
        while (Time.realtimeSinceStartup - wallStart < 60f
            && clock.Time - gameStart < 180f)
        {
            foreach (CharacterActor actor in world.Characters.Where(value =>
                         value != null && value != worker))
                actor.SetAiPaused(true);
            firstCargo = Cargo(first.instanceId);
            observedCarried |= firstCargo.Any(value =>
                value.State == WorldItemStackState.Carried
                && string.Equals(value.DestinationId,
                    worker.Identity.PersistentId, StringComparison.Ordinal));
            if (firstCargo.Sum(value => value.State == WorldItemStackState.Stored
                    ? value.Quantity : 0) == 6)
                break;
            yield return null;
        }
        Pause();
        firstCargo = Cargo(first.instanceId);
        Require(observedCarried
            && firstCargo.Sum(value => value.State == WorldItemStackState.Stored
                ? value.Quantity : 0) == 6,
            "Ordinary AI haul did not carry and store all six event lumber units.");

        events.Publish(new OperatingDayStartedEvent(firstDay));
        first = campaign.ActiveSeasonalEvents.Single(value =>
            value.instanceId == first.instanceId);
        firstCargo = Cargo(first.instanceId);
        Require(first.seasonalDriftCargo.securedQuantity == 6
            && firstCargo.All(value => SeasonalDriftCargoRules.TryReadComponent(
                value, out string occurrenceId, out bool secured)
                && occurrenceId == first.instanceId
                && secured),
            "Stored cargo did not retain its exact occurrence component and secured marker.");
        int firstExpiryDay = first.deadlineAbsoluteDay + 1;
        string firstOccurrenceId = first.instanceId;
        calendar.SetDateTime(firstExpiryDay, 8);
        events.Publish(new OperatingDayStartedEvent(firstExpiryDay));
        first = campaign.ActiveSeasonalEvents.FirstOrDefault(value =>
                value.instanceId == firstOccurrenceId)
            ?? first;
        Require(first.seasonalDriftCargo.expirationCompleted
            && first.seasonalDriftCargo.expiredQuantity == 0
            && Cargo(first.instanceId).Sum(value => value.Quantity) == 6,
            "Secured stored cargo was removed or counted as expired.");
        lines.Add(
            "[PASS] real AI Loose -> Carried -> Stored; secured component preserved; deadline removed zero secured units");

        int secondDay = GameCalendarRules.DaysPerYear + 10;
        PrepareOnlyTargetForDay(secondDay);
        calendar.SetDateTime(secondDay, 8);
        events.Publish(new OperatingDayStartedEvent(secondDay));
        V20ActiveEventSaveData second = campaign.ActiveSeasonalEvents.Single(
            value => string.Equals(value.definitionId, DefinitionId,
                StringComparison.Ordinal));
        WorldItemStackSnapshot[] secondCargo = Cargo(second.instanceId);
        Require(secondCargo.Sum(value => value.Quantity) == 6
            && secondCargo.All(value => value.State == WorldItemStackState.Loose
                && items.SetForbidden(value.StackId, true)),
            "Second Spring occurrence did not expose six holdable source units.");
        int secondExpiryDay = second.deadlineAbsoluteDay + 1;
        string secondOccurrenceId = second.instanceId;
        calendar.SetDateTime(secondExpiryDay, 8);
        events.Publish(new OperatingDayStartedEvent(secondExpiryDay));
        second = campaign.ActiveSeasonalEvents.FirstOrDefault(value =>
                value.instanceId == secondOccurrenceId)
            ?? second;
        Require(second.seasonalDriftCargo.expirationCompleted
            && second.seasonalDriftCargo.expiredQuantity == 6
            && Cargo(second.instanceId).Length == 0
            && Cargo(first.instanceId).Sum(value => value.Quantity) == 6,
            "Deadline did not sink only the unpicked source lot or damaged secured cargo.");
        lines.Add(
            "[PASS] separate unpicked Loose lumber6 expired through physical Sink; prior secured Stored lumber6 remained");
    }

    private void PrepareOnlyTargetForDay(int absoluteDay)
    {
        SeasonalEventWorldSaveData state = campaign.CaptureSeasonal();
        state.activeEvents.Clear();
        state.completedEventIds = catalog.SeasonalEvents
            .Where(value => value.season == Season.Spring
                && !string.Equals(value.StableId, DefinitionId,
                    StringComparison.Ordinal))
            .Select(value => value.StableId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        state.cycle = absoluteDay / GameCalendarRules.DaysPerYear;
        state.lastEvaluationAbsoluteDay = absoluteDay - 1;
        campaign.PublishSeasonal(campaign.PrepareSeasonal(state));
    }

    private WorldItemStackSnapshot[] Cargo(string occurrenceId) => items
        .GetAllStacks()
        .Where(value => value != null
            && SeasonalDriftCargoRules.TryReadComponent(
                value, out string owner, out _)
            && string.Equals(owner, occurrenceId, StringComparison.Ordinal))
        .OrderBy(value => value.StackId, StringComparer.Ordinal)
        .ToArray();

    private void ConfigureHauler(CharacterActor worker)
    {
        foreach (CharacterActor actor in world.Characters.Where(value => value != null))
            actor.SetAiPaused(actor != worker);
        if (worker.Brain.HasRunningAction)
        {
            Require(worker.Brain.StopCurrentActionForReplan(
                    "wim009-drift-cargo-ready"),
                "The selected worker's prior action could not be cancelled through its typed replan path.");
        }
        foreach (CharacterCondition need in new[]
                 {
                     CharacterCondition.HUNGER,
                     CharacterCondition.THIRST,
                     CharacterCondition.SLEEP,
                     CharacterCondition.HYGIENE,
                     CharacterCondition.EXCRETION,
                     CharacterCondition.FUN
                 })
            worker.Stats.ChangesStat(
                need,
                100f - worker.Stats.GetConditionValue(need, 0f));
        AbilityWork work = worker.GetComponent<AbilityWork>();
        Require(work != null, "The selected hauler has no work authority.");
        work.ClearPriorityWorkTarget();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
        Require(worker.Brain.PreferActionOnNextDecision<AIHaul>(),
            "The selected worker has no authored haul action.");
        worker.Brain.RequestImmediateReplan(clearFailures: true);
    }

    private void Pause()
    {
        GameManager host = FindFirstObjectByType<GameManager>();
        if (host != null) host.isPause = true;
        if (timeScale != null) timeScale.Scale = 0f;
    }

    private void Resume(float scale)
    {
        GameManager host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Missing main time host.");
        host.isPause = false;
        timeScale.Scale = scale;
    }

    private static void Click(string objectName)
    {
        UnityEngine.UI.Button button = Resources.FindObjectsOfTypeAll<
                UnityEngine.UI.Button>()
            .FirstOrDefault(value => value != null
                && value.gameObject.scene.IsValid()
                && value.gameObject.activeInHierarchy
                && string.Equals(value.name, objectName,
                    StringComparison.Ordinal));
        Require(button != null && button.interactable,
            "Missing UI button: " + objectName);
        button.onClick.Invoke();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
