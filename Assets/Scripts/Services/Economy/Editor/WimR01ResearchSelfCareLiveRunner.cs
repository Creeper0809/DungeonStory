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

// Root-owned integration witness. One controlled thirst change; normal AI owns
// interruption, drinking and resumption. No direct research progress or cap edits.
public sealed class WimR01ResearchSelfCareLiveRunner
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-r01-research-self-care-live-participation.txt";
    public const string TimingReportPath = "Artifacts/QA/wim-implementation/wim-r01-research-live-time-attribution-transitions.txt";
    public const string CandidateProbeReportPath = "Artifacts/QA/wim-implementation/wim-r01-work-candidate-probe.txt";
    public const string CandidateClassificationReportPath = "Artifacts/QA/wim-implementation/wim-r01-work-candidate-classification.txt";
    private const string Project = "research:commerce:logistics";
    private const string Water = "resource:clean-water";
    private readonly List<string> lines = new();
    private readonly List<ResearchProgressEvent> events = new();
    private static bool running;
    private bool timingOnly;
    private bool candidateProbe;
    private bool candidateClassification;
    private string OutputPath => candidateClassification ? CandidateClassificationReportPath
        : candidateProbe ? CandidateProbeReportPath : timingOnly ? TimingReportPath : ReportPath;
    private DungeonRuntimeLifetimeScope scope;
    private IGameClock clock;
    private IGameTimeScaleController timeScale;
    private BlueprintResearchRuntime research;
    private IProjectWorkforceRuntime workforce;
    private IWorldItemStackRuntime items;
    private ICharacterDeprivationQuery deprivation;
    private CharacterActor worker;
    private AbilityWork work;
    private IDisposable subscription;
    private IEnvironmentalFieldPersistence environment;
    private DungeonEnvironmentalFieldSaveData originalEnvironment;
    private DungeonEnvironmentalFieldSaveData safeEnvironment;
    private float nextEnvironmentRefresh;

    public static string StartFocused() => Start(false);
    public static string StartTiming() => Start(true);
    public static string StartCandidateProbe() => Start(false, true);
    public static string StartCandidateClassification() => Start(false, false, true);

    private static string Start(bool timingOnly, bool candidateProbe = false, bool candidateClassification = false)
    {
        Require(Application.isPlaying && !running, "Fresh disposable main Play required.");
        var scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main runtime missing.");
        var persistence = scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "Cannot protect actual user saves.");
        persistence.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        var host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Main coroutine host missing.");
        host.isPause = true;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
        var runner = new WimR01ResearchSelfCareLiveRunner
            { timingOnly = timingOnly, candidateProbe = candidateProbe, candidateClassification = candidateClassification };
        Directory.CreateDirectory(Path.GetDirectoryName(runner.OutputPath));
        File.WriteAllText(runner.OutputPath, "result=RUNNING\n");
        running = true;
        try { host.StartCoroutine(runner.Observe()); }
        catch { running = false; throw; }
        return "RUNNING " + runner.OutputPath;
    }

    private IEnumerator Observe()
    {
        var pending = new Stack<IEnumerator>();
        pending.Push(Run());
        Exception failure = null;
        while (pending.Count > 0)
        {
            object value = null;
            bool moved;
            try { MaintainSafeEnvironment(); moved = pending.Peek().MoveNext(); if (moved) value = pending.Peek().Current; }
            catch (Exception error) { failure = error; break; }
            if (!moved) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
            if (value is IEnumerator nested) pending.Push(nested);
            else yield return value;
        }
        try
        {
            while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
            subscription?.Dispose();
        }
        finally
        {
            Pause();
            if (originalEnvironment != null)
            {
                try { environment.Restore(environment.PrepareRestore(originalEnvironment)); }
                catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
            }
        }
        if (worker != null) lines.Add(Diagnostic());
        lines.Add(failure == null ? (candidateProbe ? "result=DIAGNOSTIC_CAPTURED" : "result=PASS") : "result=FAIL\n" + failure);
        lines.Add(candidateClassification
            ? "scope=controlled incremental-scan boundary fixture in main runtime using actual staff/research/AIWork. Known internal scan/cache fields are temporarily staged and restored in one frame. Verifies incomplete defer, completed-empty hard failure, partial-valid candidate and explicit-path behavior. NOT a natural completion, prior12/17-stall fix, daily throughput or ETA witness."
            : candidateProbe
            ? "scope=bounded candidate-selection diagnosis, not research completion/ETA/AI gameplay PASS. Capture retained null-scan state before one same-frame null-versus-explicit-path candidate comparison. The comparison can modify candidate diagnostics/assignment; no later gameplay result is inferred from it. No production cache, budget or fallback modification."
            : timingOnly
            ? "scope=actual single-staff research completion and frame-observed work/travel/other time partition. Safe environmental field, initial needs and priorities are controlled; no thirst perturbation/repeated need reset or research/cap mutation. Work boundaries are reported separately, not attributed to a guessed cause. Not daily productivity, live ETA, alternate equipment bonus certification or collaborative research."
            : "scope=actual owner/party and research UI; single real staff AI, physical drink and resume; safe environmental field, one initial healthy-needs setup, priorities and one routine-thirst perturbation are controlled. Exposure and needs are NOT repeatedly reset. No action-list replacement, forced drink/stop-work/progress, actual lamp/build-power, daily productivity, paired-run timing or collaborative-research claim.");
        lines.Add("cleanup=paused disposable Play; operator stops; user persistence disabled before owner selection");
        File.WriteAllLines(OutputPath, lines);
        Debug.Log(failure == null ? (candidateProbe ? "R01 diagnostic captured: " : "R01 witness PASS: ") + OutputPath : "R01 witness FAIL: " + failure.Message);
        running = false;
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        clock = scope.Container.Resolve<IGameClock>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        research = scope.Container.Resolve<ProgressionSceneRuntimeReferences>().BlueprintResearch;
        workforce = scope.Container.Resolve<IProjectWorkforceRuntime>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        deprivation = scope.Container.Resolve<ICharacterDeprivationQuery>();
        var world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        var owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Owner preparation unavailable.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>().TryReconcileNewRunTierZero(
                out var expansion, out string reason) && expansion.CurrentInteriorColumns == 29, "TierZero: " + reason);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null, "Party did not publish owner.");
        }
        worker = world.Characters.Where(x => x != null && !x.IsOwner && !x.IsDead
            && x.characterType == CharacterType.NPC && x.CurrentLifecycleState == CharacterLifecycleState.Active
            // Current deprivation authority excludes Golem food/water needs.
            // Select a supported existing staff member; do not change species.
            && !string.Equals(x.SpeciesTag, "Golem", StringComparison.OrdinalIgnoreCase) && x.Brain != null
            && x.GetComponent<AbilityWork>() != null
            && x.Brain.availableActions.Any(a => a?.actionset is AIDrink))
            .OrderBy(x => x.Identity.PersistentId, StringComparer.Ordinal).FirstOrDefault();
        Require(worker != null, "No actual staff with routine drink and work.");
        foreach (var actor in world.Characters.Where(x => x != null)) actor.SetAiPaused(true);
        lines.Add("controlledPausedRoster=" + string.Join(";", world.Characters
            .Where(x => x != null).OrderBy(x => x.Identity.PersistentId, StringComparer.Ordinal)
            .Select(x => x.Identity.PersistentId + "@" + x.GetNowXY() + "/" + x.CurrentLifecycleState)));
        environment = scope.Container.Resolve<IEnvironmentalFieldPersistence>();
        originalEnvironment = environment.Capture();
        Require(originalEnvironment.width > 0 && originalEnvironment.height > 0
            && originalEnvironment.cells != null && originalEnvironment.thermostats != null,
            "Valid current environmental field required for controlled surroundings.");
        safeEnvironment = JsonUtility.FromJson<DungeonEnvironmentalFieldSaveData>(JsonUtility.ToJson(originalEnvironment));
        // Capture is sparse; preserve it for cleanup, but author every fixture
        // cell so omitted default-dark cells cannot retain unsafe surroundings.
        safeEnvironment.cells.Clear();
        for (int y = 0; y < safeEnvironment.height; y++)
            for (int x = 0; x < safeEnvironment.width; x++)
                safeEnvironment.cells.Add(new EnvironmentalCellSaveData
                    { x = x, y = y, temperatureC = 22, airQuality = 100, lightLevel = 100 });
        MaintainSafeEnvironment();
        lines.Add("controlledEnvironment=22C/air100/light100 every5 game seconds through existing field persistence; original restored during cleanup; character exposure/needs simulation remains active");
        foreach (var need in new[] { CharacterCondition.HUNGER, CharacterCondition.THIRST, CharacterCondition.SLEEP,
            CharacterCondition.HYGIENE, CharacterCondition.EXCRETION, CharacterCondition.FUN })
            worker.Stats.ChangesStat(need, 100 - worker.Stats.GetConditionValue(need, 0));
        work = worker.GetComponent<AbilityWork>();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        foreach (var type in WorkTypeCatalog.All)
            work.SetWorkPriority(type.WorkTypeId, type.WorkTypeId == BuiltInWorkTypeIds.Research
                ? WorkPriorityLevel.Priority1 : WorkPriorityLevel.Off);
        var project = research.ProjectCatalog.Projects.Single(x => x.ProjectId.Value == Project);
        Require(project.RequiredWork == 17 && project.MaximumResearchers == 1, "Authored 17WU/single researcher changed.");
        Require(!research.State.Projects.IsCompleted(project.ProjectId), "Project must be fresh, not reset by fixture.");
        subscription = scope.Container.Resolve<IGameEventBus>().Subscribe<ResearchProgressEvent>(OnProgress);
        // Capture actual preparation capabilities before the first unpause.
        // Do not query action scores/candidates here: observation must not
        // populate or change the decision state being diagnosed.
        lines.Add("preUnpauseWorker=" + worker.Identity.PersistentId
            + "; actionTypes=" + string.Join(",", worker.Brain.availableActions
                .Select(a => a?.actionset?.GetType().FullName ?? "<null>")
                .OrderBy(name => name, StringComparer.Ordinal))
            + "; hasAIWork=" + worker.Brain.availableActions.Any(a => a?.actionset is AIWork)
            + "; hasAIDrink=" + worker.Brain.availableActions.Any(a => a?.actionset is AIDrink)
            + "; duty=" + work.CurrentDutyState
            + "; priorities=" + JsonUtility.ToJson(work.WorkPriorities));
        File.WriteAllLines(OutputPath, lines);
        Resume();
        float warm = clock.Time, wall = Time.realtimeSinceStartup;
        while (clock.Time - warm < 1 && Time.realtimeSinceStartup - wall < 15) yield return null;
        Pause();
        Require(clock.Time - warm >= 1, "Preparation game clock did not run.");
        Click("TopTabButton_Research_연구");
        yield return null;
        Click("Node_" + Project);
        yield return null;
        var window = FindFirstObjectByType<ResearchTreeWindow>();
        Require(window != null, "Actual research UI missing.");
        string estimate = string.Join("\n", window.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(x => x.text));
        Require(estimate.Contains("45 WU/게임일") && estimate.Contains("0.4 게임일")
            && estimate.Contains("준비 기준 추정") && estimate.Contains("실측은 반영하지 않음"),
            "Preparation estimate/disclaimer missing: " + estimate);
        Click("ProjectAction");
        yield return null;
        var tab = window.GetComponent<UITab>();
        Require(tab != null, "Research tab close owner missing.");
        tab.CloseTab();
        lines.Add("[PASS] actual preparation estimate=adult1/45WU/day/0.4day; actual enqueue; cap1/17WU unchanged");
        if (candidateClassification)
        {
            yield return VerifyCandidateClassification();
            yield break;
        }
        worker.SetAiPaused(false);
        worker.Brain.RequestImmediateReplan(clearFailures: true);
        Resume();
        float started = clock.Time, startedWall = Time.realtimeSinceStartup;
        if (candidateProbe)
        {
            yield return ObserveCandidateProbe();
            yield break;
        }
        if (timingOnly)
        {
            yield return ObserveTiming(started, startedWall);
            yield break;
        }
        yield return WaitFor(() => events.Count > 0 && IsResearching()
            && work.GenericCompletedWorkForDiagnostics > .1f, 75, "Initial normal research/cycle witness");
        Pause();
        float before = Progress();
        Require(before > 0 && before < 17 && workforce.GetActiveWorkerCount(Project) == 1
            && workforce.GetContributionMultiplier(Project, worker.Identity.PersistentId) == 1,
            "Actual research participation/contribution not sole1.");
        Require(!workforce.CanJoin(Project, worker.Identity.PersistentId, 1),
            "Continuation must not reopen duplicate admission for its current researcher.");
        lines.Add("duplicateAdmissionWhileParticipating=REJECTED; simultaneousResearchers=1");
        float rawPartial = work.GenericCompletedWorkForDiagnostics;
        float rawRequired = work.GenericRequiredWorkForDiagnostics;
        Require(rawPartial < rawRequired, "Perturbation must occur during unfinished cycle.");
        foreach (var stock in items.GetAllStacks().Where(x => x.ItemId == Water && x.Quantity > 0).ToArray())
            Require(items.SetForbidden(stock.StackId, true), "Could not isolate disposable water source.");
        Vector2Int source = worker.GetNowXY();
        int total = WaterCount();
        Require(items.SpawnItemAt(Water, 1, source, WorldItemStackState.Loose, string.Empty, out int added)
            && added == 1 && WaterCount() == total + 1, "Exact physical drink source failed.");
        var response = scope.Container.Resolve<ICharacterNeedBalanceRuntime>().GetResponse(CharacterCondition.THIRST);
        float startThirst = response.routineStart;
        Require(startThirst > response.emergencyStart + 1
            && startThirst > 21 + worker.Stats.GetExpectedTimedNeedLoss(CharacterCondition.THIRST, 90),
            "Routine band cannot stay clear of emergency forecast.");
        worker.Stats.ChangesStat(CharacterCondition.THIRST, startThirst - Thirst());
        Require(deprivation.NeedsRoutineDrink(worker, out string drinkReason),
            "Routine thirst perturbation unavailable: " + drinkReason);
        lines.Add("perturbation=thirst:" + startThirst + "; source=" + source + "; progress=" + before
            + "; partialRaw=" + rawPartial + "/" + rawRequired + "; soleContribution=1");
        // No forced decision, stop-work, AIDrink call or action preference here.
        Resume();
        yield return WaitFor(() => deprivation.IsRoutineDrinkActionActive(worker), 45, "Natural routine drink selection");
        float drinkStart = clock.Time, drinkProgress = Progress();
        int drinkEvents = events.Count;
        Require(drinkProgress < 17 && !IsResearching() && workforce.GetActiveWorkerCount(Project) == 0,
            "Drink must leave unfinished research and its workforce lease.");
        Require(worker.Brain.bestAction?.actionset is AIDrink,
            "Routine runner must belong to the actual AIDrink action, not a direct fixture invocation.");
        wall = Time.realtimeSinceStartup;
        while (deprivation.IsRoutineDrinkActionActive(worker) && Time.realtimeSinceStartup - wall < 30)
        {
            Require(!IsResearching() && workforce.GetActiveWorkerCount(Project) == 0
                && Progress() == drinkProgress && events.Count == drinkEvents,
                "Research approved progress or participant leaked during drink.");
            Require(!CharacterNeedAiThresholds.IsEmergencyOrImminentPhysicalHarm(worker, CharacterCondition.THIRST),
                "Routine drink became emergency care; cannot count as the requested routine interruption witness.");
            yield return null;
        }
        Require(!deprivation.IsRoutineDrinkActionActive(worker) && WaterCount() == total
            && Thirst() > startThirst, "Drink did not consume exactly one and restore thirst.");
        float drinkEnd = clock.Time;
        lines.Add("[PASS] routine AI interruption/lease0; drinkGameSeconds=" + (drinkEnd - drinkStart)
            + "; physicalWaterDelta=-1; stableProgress=" + drinkProgress + "; thirstAfter=" + Thirst());
        yield return WaitFor(() => IsResearching()
            && workforce.GetActiveWorkerCount(Project) == 1
            && work.HasActiveGenericProgressForDiagnostics,
            45, "Autonomous same-project research participation after approach");
        Require(workforce.GetActiveWorkerCount(Project) == 1
            && workforce.GetContributionMultiplier(Project, worker.Identity.PersistentId) == 1,
            "Resumed researcher has duplicate or changed contribution.");
        yield return WaitFor(() => research.State.Projects.IsCompleted(project.ProjectId), 75, "Remaining actual research completion");
        Pause();
        Require(Progress() == 17 && events.Sum(x => x.ProgressDelta) == 17
            && events.All(x => x.Researcher.Value == worker.Identity.PersistentId
                && x.ApprovedWork > 0 && x.ProgressDelta > 0 && x.ProgressDelta <= x.ApprovedWork)
            && WaterCount() == total, "Progress/event/worker/physical conservation failed.");
        lines.Add("[PASS] autonomous resume/same worker; progress=17/17; approved=" + events.Sum(x => x.ApprovedWork)
            + "; events=" + string.Join(",", events.Select(x => x.ApprovedWork + "/" + x.ProgressDelta))
            + "; gameSeconds=" + (clock.Time - started) + "; wallSeconds=" + (Time.realtimeSinceStartup - startedWall));
        lines.Add("estimateInterpretation=0.4day is rounded preparation/reference-WU estimate, not live ETA; measured run includes routine travel/drink and any incomplete-cycle restart, no inferred 45WU/day performance");
    }

    private IEnumerator VerifyCandidateClassification()
    {
        var actions = worker.Brain.availableActions.Select(x => x?.actionset).OfType<AIWork>().ToArray();
        Require(actions.Length == 1 && !actions[0].WorkTypeId.IsValid,
            "Boundary fixture requires one actual general AIWork action.");
        var action = actions[0];
        // This fixture stages the general scan. A retained typed preference
        // would legitimately select a different dictionary key in AIWork.
        if (worker.Brain.TryGetPreferredWorkType(out var preferred))
        {
            worker.Brain.ConsumePreferredWorkType(preferred);
            lines.Add("controlledPreparation=consumed retained preferred work type " + preferred.Value);
        }
        Require(!worker.Brain.HasPreferredWorkType, "General work fixture still has a typed preference.");
        // Force scan *state*, not stopwatch speed: tiny time slices cannot be
        // tested deterministically by assuming how fast the host machine is.
        var cache = scope.Container.Resolve<IFacilityCandidateCache>();
        float deadline = Time.realtimeSinceStartup + 3f;
        do
        {
            work.TryGetBestAnyWorkCandidate(null, out _);
            if (!cache.HasPendingIndexBuild) break;
            yield return null;
        } while (Time.realtimeSinceStartup < deadline);
        Require(!cache.HasPendingIndexBuild, "Actual facility index not ready for boundary fixture.");
        var path = worker.Brain.GetPathSearch(worker);
        Require(path != null, "Actual explicit path query unavailable.");
        Require(work.TryGetBestAnyWorkCandidate(path, out var valid)
            && valid.IsValid && valid.WorkTypeId == BuiltInWorkTypeIds.Research,
            "Actual explicit-path research candidate unavailable.");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        object selector = typeof(AbilityWork).GetField("targetSelector", flags)?.GetValue(work);
        Require(selector != null, "Candidate selector fixture contract changed.");
        var scans = typeof(WorkTargetSelector).GetField("incrementalScans", flags)?.GetValue(selector) as IDictionary;
        var memo = typeof(WorkTargetSelector).GetField("candidateCache", flags)?.GetValue(selector) as IDictionary;
        Require(scans != null && memo != null && scans.Contains(FacilityWorkType.None), "General scan not captured.");
        object scan = scans[FacilityWorkType.None];
        var fields = scan.GetType().GetFields(flags);
        var original = fields.ToDictionary(x => x.Name, x => x.GetValue(scan), StringComparer.Ordinal);
        var originalMemo = new List<DictionaryEntry>();
        foreach (DictionaryEntry entry in memo) originalMemo.Add(entry);
        var source = original["Source"] as IReadOnlyList<BuildableObject>;
        Require(source?.Count > 1, "Boundary fixture needs actual multi-facility scan.");
        void Set(string name, object value)
        {
            var field = scan.GetType().GetField(name, flags);
            Require(field != null, "Scan field missing: " + name);
            field.SetValue(scan, value);
        }
        void Stage(bool complete, WorkTargetCandidate best)
        {
            memo.Clear();
            Set("EvaluatedCount", complete ? source.Count : 0);
            Set("Complete", complete);
            Set("LastAdvancedFrame", clock.FrameCount);
            Set("CompletedFrame", clock.FrameCount);
            Set("Best", best);
            Set("MostUrgent", best);
            Set("Rejected", default(WorkTargetCandidate));
        }
        try
        {
            Stage(false, default);
            bool found = action.TryResolveDestinationWithFailure(worker, null, out var destination, out var failure);
            Require(!found && destination == null && failure.Kind == AIActionFailureKind.FacilityCandidateDeferred
                && failure.IsDeferred && !string.IsNullOrEmpty(failure.Reason), "Incomplete scan was not typed deferred.");
            lines.Add("PASS incomplete/no-best => AIWork FacilityCandidateDeferred/IsDeferred, no destination");
            Stage(true, default);
            found = action.TryResolveDestinationWithFailure(worker, null, out destination, out failure);
            Require(!found && destination == null && failure.Kind == AIActionFailureKind.NoDestination
                && !failure.IsDeferred, "Completed empty scan incorrectly deferred.");
            lines.Add("PASS completed-empty => AIWork NoDestination/not deferred");
            Stage(false, valid);
            found = action.TryResolveDestinationWithFailure(worker, null, out destination, out failure);
            Require(found && destination == WorkTargetCandidateRuntimeAdapter.ResolveBuilding(valid)
                && !failure.HasFailure, "Valid partial candidate was suppressed.");
            lines.Add("PASS partial-valid => same actual research destination");
            Stage(false, default);
            found = action.TryResolveDestinationWithFailure(worker, path, out destination, out failure);
            Require(found && destination == WorkTargetCandidateRuntimeAdapter.ResolveBuilding(valid)
                && !failure.HasFailure, "Explicit path query inherited unrelated scan deferral.");
            lines.Add("PASS explicit path ignores incomplete cached scan; staged cases=4; sameFrame=" + clock.FrameCount);
        }
        finally
        {
            foreach (var field in fields) field.SetValue(scan, original[field.Name]);
            memo.Clear();
            foreach (var entry in originalMemo) memo.Add(entry.Key, entry.Value);
        }
    }

    private IEnumerator ObserveCandidateProbe()
    {
        float startedWall = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startedWall < 30f)
        {
            string rejection = worker.Brain.GetCurrentJobGiverEvaluationRejectionSummary(CharacterAiBranch.Work);
            if (!IsResearching() && rejection.StartsWith("Work/NoDestination", StringComparison.Ordinal))
            {
                // Single main-thread packet, before any explicit candidate query.
                // No scan mutation or evaluator invocation is hidden in capture.
                CaptureCandidateScans("retained-before-query");
                bool nullFound = work.TryGetBestAnyWorkCandidate(null, out var nullCandidate);
                CaptureCandidateScans("after-null-query");
                var path = worker.Brain.GetPathSearch(worker);
                bool pathFound = work.TryGetBestAnyWorkCandidate(path, out var pathCandidate);
                lines.Add("sameFrameCandidateComparison frame=" + clock.FrameCount
                    + "; game=" + clock.Time + "; retained=" + rejection
                    + "; nullFound=" + nullFound + "; null=" + DescribeProbeCandidate(nullCandidate)
                    + "; pathFound=" + pathFound + "; path=" + DescribeProbeCandidate(pathCandidate)
                    + "; nullFalsePathTrue=" + (!nullFound && pathFound));
                yield break;
            }
            yield return null;
        }
        CaptureCandidateScans("bounded-no-rejection-observed");
        lines.Add("probeOutcome=NO_MATCHING_REJECTION_WITHIN_30_WALL_SECONDS; not a pass or proof of absence");
    }

    private void CaptureCandidateScans(string stage)
    {
        const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        var selectorField = typeof(AbilityWork).GetField("targetSelector", fields);
        Require(selectorField != null, "Candidate probe selector contract changed.");
        object selector = selectorField.GetValue(work);
        var cache = scope.Container.Resolve<IFacilityCandidateCache>();
        Require(cache is FacilityCandidateCacheStore, "Candidate probe expects current concrete cache authority.");
        object CacheField(string name)
        {
            var field = typeof(FacilityCandidateCacheStore).GetField(name, fields);
            Require(field != null, "Candidate probe cache field missing: " + name);
            return field.GetValue(cache);
        }
        var orderField = typeof(AbilityWork).GetField("workOrderRuntime", fields);
        Require(orderField != null, "Candidate probe work-order authority changed.");
        var orders = orderField.GetValue(work) as IWorkOrderRuntime;
        var world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        // HasPendingIndexBuild performs EnsureIndexVersion, so do NOT call it
        // from a supposedly observational snapshot. Preserve raw stored state
        // and the current registry version before the ordinary query runs.
        lines.Add("probeStage=" + stage + "; frame=" + clock.FrameCount + "; game=" + clock.Time
            + "; storedIndexComplete=" + CacheField("indexBuildComplete")
            + "; storedIndexBuilding=" + CacheField("indexedBuildingVersion")
            + "; storedBuildingScanIndex=" + CacheField("buildingScanIndex")
            + "; currentIndex/dynamic/cachedGrid/building/order=" + cache.CandidateIndexVersion + "/"
            + cache.DynamicStateVersion + "/" + (work.CachedGrid?.StructuralVersion ?? -1)
            + "/" + world.BuildingVersion + "/" + (orders?.WorkOrderCandidateVersion ?? -1));
        if (selector == null) { lines.Add("incrementalScans=selector-not-initialized"); return; }
        var scanField = typeof(WorkTargetSelector).GetField("incrementalScans", fields);
        Require(scanField != null, "Candidate probe scan contract changed.");
        var scans = scanField.GetValue(selector) as IDictionary;
        Require(scans != null, "Candidate probe dictionary unavailable.");
        var rows = new List<string>();
        foreach (DictionaryEntry entry in scans)
        {
            object scan = entry.Value;
            object Read(string name)
            {
                var field = scan.GetType().GetField(name, fields);
                Require(field != null, "Candidate probe field missing: " + name);
                return field.GetValue(scan);
            }
            var source = Read("Source") as IReadOnlyList<BuildableObject>;
            rows.Add("scan=" + entry.Key + "; count=" + (source?.Count ?? -1)
                + "; evaluated=" + Read("EvaluatedCount") + "; complete=" + Read("Complete")
                + "; offset=" + Read("StartOffset") + "; advanced=" + Read("LastAdvancedFrame")
                + "; completedFrame=" + Read("CompletedFrame")
                + "; index/dynamic/grid/building/order=" + Read("CandidateIndexVersion") + "/"
                + Read("DynamicStateVersion") + "/" + Read("GridVersion") + "/"
                + Read("BuildingVersion") + "/" + Read("WorkOrderVersion")
                + "; best=" + DescribeProbeCandidate((WorkTargetCandidate)Read("Best"))
                + "; rejected=" + DescribeProbeCandidate((WorkTargetCandidate)Read("Rejected"))
                + "; firstSources=" + (source == null ? "none" : string.Join(",", source.Take(8)
                    .Select(x => x == null ? "<null>" : x.name + "@" + x.centerPos))));
        }
        rows.Sort(StringComparer.Ordinal);
        lines.AddRange(rows);
    }

    private static string DescribeProbeCandidate(WorkTargetCandidate candidate)
    {
        var building = WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate);
        return "valid:" + candidate.IsValid + "/" + candidate.WorkTypeId.Value + "/"
            + (building == null ? "<none>" : building.name + "@" + building.centerPos)
            + "/" + candidate.FailureKind + "/" + candidate.FailureReason;
    }

    private IEnumerator ObserveTiming(float started, float startedWall)
    {
        var movement = worker.GetComponent<AbilityMove>();
        var room = scope.Container.Resolve<IRoomEnvironmentQuery>();
        lines.Add("postCycleMetaMultiplier=" + scope.Container.Resolve<IMetaProgressionRuntimeReader>().GetArcaneResearchWorkMultiplier()
            + "; pipeline=raw work rate -> completed cycle -> current participant contribution -> supplied durable research equipment -> meta research multiplier -> remaining project clamp");
        long lastRevision = work.ApprovedWorkProgressRevisionForDiagnostics;
        int lastRun = work.ActiveWorkRunIdForDiagnostics;
        bool lastGeneric = work.HasActiveGenericProgressForDiagnostics;
        bool lastResearch = IsResearching();
        float lastRaw = work.GenericCompletedWorkForDiagnostics;
        float lastTime = clock.Time;
        Vector2Int lastCell = worker.GetNowXY();
        double rawIncrementSeconds = 0, boundarySeconds = 0, travelSeconds = 0, otherSeconds = 0;
        double observedSteadyWu = 0;
        float minObservedRate = float.PositiveInfinity, maxObservedRate = 0, maxFrameGameSeconds = 0;
        float nextSample = clock.Time;
        int steadyIntervals = 0, observedEvents = 0;
        string lastPhase = string.Empty;
        while (!research.State.Projects.IsCompleted(new ResearchProjectId(Project))
            && Time.realtimeSinceStartup - startedWall < 210)
        {
            yield return null;
            float now = clock.Time;
            float delta = now - lastTime;
            Require(delta >= 0 && !float.IsNaN(delta) && !float.IsInfinity(delta), "Research clock moved backwards or became nonfinite.");
            if (delta == 0) continue;
            long revision = work.ApprovedWorkProgressRevisionForDiagnostics;
            int run = work.ActiveWorkRunIdForDiagnostics;
            bool generic = work.HasActiveGenericProgressForDiagnostics;
            bool researching = IsResearching();
            float raw = work.GenericCompletedWorkForDiagnostics;
            if (lastResearch && lastGeneric && (run != lastRun || !researching || !generic))
            {
                lines.Add("researchRunExit previousRun=" + lastRun + "; previousRaw=" + lastRaw
                    + "; previousGame=" + lastTime + "; currentRun=" + run
                    + "; completionEvents=" + events.Count
                    + "; cancelledRun=" + work.LastCancelledWorkRunIdForDiagnostics
                    + "; cancellationCount=" + work.ActiveWorkCancellationCountForDiagnostics
                    + "; hunger=" + worker.Stats.GetConditionValue(CharacterCondition.HUNGER, 0)
                    + "; sleep=" + worker.Stats.GetConditionValue(CharacterCondition.SLEEP, 0)
                    + "; hygiene=" + worker.Stats.GetConditionValue(CharacterCondition.HYGIENE, 0)
                    + "; excretion=" + worker.Stats.GetConditionValue(CharacterCondition.EXCRETION, 0)
                    + "; fun=" + worker.Stats.GetConditionValue(CharacterCondition.FUN, 0)
                    + "; " + Diagnostic());
            }
            Vector2Int cell = worker.GetNowXY();
            string phase;
            maxFrameGameSeconds = Mathf.Max(maxFrameGameSeconds, delta);
            if (revision != lastRevision)
            {
                Require(revision > lastRevision && (lastResearch || researching),
                    "Approved-work revision belongs to an unexpected non-research action.");
                if (lastGeneric && generic && run == lastRun && raw > lastRaw)
                {
                    float observed = (raw - lastRaw) / delta;
                    Require(!float.IsNaN(observed) && !float.IsInfinity(observed) && observed > 0,
                        "Observed live work increment was invalid.");
                    minObservedRate = Mathf.Min(minObservedRate, observed);
                    maxObservedRate = Mathf.Max(maxObservedRate, observed);
                    observedSteadyWu += raw - lastRaw;
                    steadyIntervals++;
                    rawIncrementSeconds += delta;
                    phase = "observed-research-increment";
                }
                else
                {
                    boundarySeconds += delta;
                    phase = "work-cycle-boundary";
                }
            }
            else if (movement.IsSystemMoveInProgress || cell != lastCell)
            {
                travelSeconds += delta;
                phase = "movement-observed";
            }
            else
            {
                otherSeconds += delta;
                phase = "no-work-increment/no-movement";
            }
            if (phase != lastPhase)
                lines.Add("phase game=" + now + "; kind=" + phase + "; run=" + run
                    + "; generic=" + raw + "/" + work.GenericRequiredWorkForDiagnostics
                    + "; action=" + worker.Brain.bestAction?.actionset + "; cell=" + cell);
            lastPhase = phase;
            if (generic && researching && now >= nextSample)
            {
                // Read the existing production trace; do not invoke the work
                // calculator to manufacture its own expected observation.
                var trace = CharacterPerformanceExecutionTrace.Snapshot().SingleOrDefault(x =>
                    x.ConsumerId == "WorkAmountCalculator.CalculateWorkPerSecond"
                    && x.Detail == BuiltInWorkTypeIds.Research.Value);
                lines.Add("rateInputs game=" + now + "; performance=" + trace?.OutputValue
                    + "; cumulativeTraceInvocationCount=" + trace?.Count
                    + "; roomDuration=" + room.GetWorkDurationMultiplier(work.assignedShop, BuiltInWorkTypeIds.Research)
                    + "; craftsmanship=" + work.assignedShop?.Craftsmanship.Quality
                    + "; contribution=" + workforce.GetContributionMultiplier(Project, worker.Identity.PersistentId));
                nextSample = now + 5;
            }
            while (observedEvents < events.Count)
            {
                var e = events[observedEvents++];
                lines.Add("completion game=" + now + "; approved=" + e.ApprovedWork
                    + "; projectDelta=" + e.ProgressDelta + "; actor=" + e.Researcher.Value);
            }
            Require(workforce.GetActiveWorkerCount(Project) <= 1, "Authored single-researcher cap exceeded.");
            lastRevision = revision; lastRun = run; lastGeneric = generic;
            lastResearch = researching; lastRaw = raw; lastTime = now; lastCell = cell;
        }
        Pause();
        Require(Progress() == 17 && events.Sum(x => x.ProgressDelta) == 17
            && events.All(x => x.Researcher.Value == worker.Identity.PersistentId
                && x.ApprovedWork > 0 && x.ProgressDelta > 0 && x.ProgressDelta <= x.ApprovedWork)
            && steadyIntervals >= 20, "Timing witness lacks actual sole-worker completion or stable work observations.");
        double partition = rawIncrementSeconds + boundarySeconds + travelSeconds + otherSeconds;
        Require(Math.Abs(partition - (lastTime - started)) < 0.01, "Observed time partition is incomplete.");
        lines.Add("PASS actualResearchTime totalGame=" + (lastTime - started)
            + "; wall=" + (Time.realtimeSinceStartup - startedWall)
            + "; observedIncrementSeconds=" + rawIncrementSeconds + "; boundarySeconds=" + boundarySeconds
            + "; movementSeconds=" + travelSeconds + "; otherSeconds=" + otherSeconds
            + "; maxObservationStep=" + maxFrameGameSeconds);
        lines.Add("PASS observedSteadyWork wu=" + observedSteadyWu + "; intervals=" + steadyIntervals
            + "; observedRateMin=" + minObservedRate + "; observedRateMax=" + maxObservedRate
            + "; observedWeightedMeanRate=" + observedSteadyWu / rawIncrementSeconds
            + "; approvedTotal=" + events.Sum(x => x.ApprovedWork) + "; projectProgress=17");
        lines.Add("interpretation=45WU/day is a preparation reference, not runtime rate; live increment rate is measured from raw progress/time, boundaries remain explicit. Rate input snapshots are sampled observations, not a causal claim for unobserved intervals or old150.5375sec run.");
    }

    private IEnumerator WaitFor(Func<bool> predicate, float seconds, string stage)
    {
        float wall = Time.realtimeSinceStartup;
        float startedGame = clock.Time;
        Vector2Int startCell = worker.GetNowXY(), lastCell = startCell;
        int cellChanges = 0;
        while (!predicate() && Time.realtimeSinceStartup - wall < seconds)
        {
            yield return null;
            Vector2Int current = worker.GetNowXY();
            if (current != lastCell) cellChanges++;
            lastCell = current;
        }
        lines.Add("observedStage=" + stage + "; startCell=" + startCell + "; endCell=" + lastCell
            + "; cellChanges=" + cellChanges + "; gameSeconds=" + (clock.Time - startedGame)
            + "; wallSeconds=" + (Time.realtimeSinceStartup - wall));
        if (!predicate())
        {
            try { lines.Add(ApproachDiagnostic()); }
            catch (Exception diagnostic)
            {
                lines.Add("approachAtFailure=diagnostic-error:" + diagnostic.GetType().Name + "/" + diagnostic.Message);
            }
        }
        Require(predicate(), stage + " timed out: " + Diagnostic());
    }

    private string ApproachDiagnostic()
    {
        var movement = worker.GetComponent<AbilityMove>();
        var target = work.assignedShop;
        // One failure-time query, never a per-frame pathfinding loop or forced move.
        var search = worker.Brain.GetPathSearch(worker);
        bool reachable = WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(target, search, out var access);
        var actors = scope.Container.Resolve<ICharacterAiWorldRegistry>().Characters.Where(x => x != null);
        return "approachAtFailure=target:" + target?.name + "@" + target?.centerPos
            + "; accessReachable=" + reachable + "; access=" + access
            + "; accessCost=" + (reachable ? search.GetMoveCostTo(access) : -1)
            + "; systemMove=" + movement?.IsSystemMoveInProgress
            + "; systemDestination=" + movement?.ActiveSystemMoveDestinationForDiagnostics
            + "; replans=" + movement?.RuntimeActionPathReplanCount
            + "; pathFailures=" + movement?.RuntimeActionPathFailureCount
            + "; roster=" + string.Join(";", actors.OrderBy(x => x.Identity.PersistentId, StringComparer.Ordinal)
                .Select(x => x.Identity.PersistentId + "@" + x.GetNowXY()));
    }
    private void MaintainSafeEnvironment()
    {
        if (safeEnvironment == null || clock.Time < nextEnvironmentRefresh) return;
        environment.Restore(environment.PrepareRestore(safeEnvironment));
        nextEnvironmentRefresh = clock.Time + 5;
    }
    private void OnProgress(ResearchProgressEvent value)
    {
        if (value.ProjectId == Project) events.Add(value);
    }
    private bool IsResearching() => work.isWorking && work.IsAssignedWork(BuiltInWorkTypeIds.Research);
    private float Progress() => research.State.Projects.GetProgress(new ResearchProjectId(Project)).Progress;
    private int WaterCount() => items.GetAllStacks().Where(x => x.ItemId == Water).Sum(x => x.Quantity);
    private float Thirst() => worker.Stats.GetConditionValue(CharacterCondition.THIRST, 0);
    private string Diagnostic() => "worker=" + worker.Identity.PersistentId + "; action=" + worker.Brain.bestAction?.actionset
        + "; research=" + IsResearching() + "; participants=" + workforce.GetActiveWorkerCount(Project)
        + "; progress=" + Progress() + "; generic=" + work.GenericCompletedWorkForDiagnostics
        + "/" + work.GenericRequiredWorkForDiagnostics + "; thirst=" + Thirst() + "; cell=" + worker.GetNowXY()
        + "; gameTime=" + clock.Time + "; wall=" + Time.realtimeSinceStartup
        + "; cancellation=" + work.LastActiveWorkCancellationReasonForDiagnostics
        + "; preWuExit=" + work.LastPreWuExitKindForDiagnostics + "/" + work.LastPreWuExitDetailForDiagnostics
        + "; brain=" + worker.Brain.GetDebugSummary();
    private static void Click(string name)
    {
        var button = Resources.FindObjectsOfTypeAll<Button>().SingleOrDefault(x => x != null && x.name == name
            && x.gameObject.scene.isLoaded && x.gameObject.activeInHierarchy);
        Require(button != null && button.IsInteractable()
            && PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, Vector2.zero), "Actual UI click unavailable: " + name);
    }
    private void Pause() { var game = FindFirstObjectByType<GameManager>(); if (game != null) game.isPause = true; if (timeScale != null) timeScale.Scale = 0; }
    private void Resume() { FindFirstObjectByType<GameManager>().isPause = false; timeScale.Scale = 2; }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
}
#endif
