using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public static class DungeonReleaseSoakPlayModeVerifier
{
    public const string ReportPath = "Temp/release-soak-report.txt";

    [MenuItem("DungeonStory/Debug/QA/Run Release Soak Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Release soak verification requires PlayMode.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<DungeonReleaseSoakVerificationRunner>() != null)
        {
            Debug.LogWarning("Release soak verification is already running.");
            return;
        }

        new GameObject("Release Soak Verification Runner")
            .AddComponent<DungeonReleaseSoakVerificationRunner>();
    }
}

public sealed class DungeonReleaseSoakVerificationRunner : MonoBehaviour
{
    private const string SoakSlot = "qa_release_soak";
    private const float WarmupSeconds = 2f;
    private const float MinimumSoakRealSeconds = 45f;
    private const float MaximumSoakRealSeconds = 150f;
    private const int RequiredOperatingDayAdvances = 2;
    private const int SoakGameSpeed = 5;
    private const float ObservationInterval = 0.5f;
    private const float LogisticsObservationInterval = 5f;
    private const int PerformanceWarmupFrames = 60;
    private const int PerformanceMinimumFrames = 300;
    private const float PerformanceMinimumSeconds = 30f;

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<float> frameTimesMs = new List<float>(16384);
    private readonly List<double> mainThreadTimesMs = new List<double>(16384);
    private readonly List<double> aiBudgetMarkerTimesMs = new List<double>(16384);
    private readonly List<double> schedulerTimesMs = new List<double>(16384);
    private readonly List<long> editorBaselineGcAllocations = new List<long>(
        GameplayGcAcceptancePolicy.EditorBaselineSampleFrames);
    private readonly List<long> editorSteadyGcAllocations = new List<long>(
        GameplayGcAcceptancePolicy.EditorBaselineSampleFrames);
    private readonly List<long> gcAllocations = new List<long>(16384);
    private readonly List<long> saveSizes = new List<long>();
    private readonly Dictionary<int, ActorObservation> actorObservations = new Dictionary<int, ActorObservation>();
    private readonly HashSet<string> observedFlowSummaries = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> observedFlowItems = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<WorkOrderStatus> observedWorkOrderStates = new HashSet<WorkOrderStatus>();
    private readonly HashSet<WorldItemStackState> observedItemStackStates = new HashSet<WorldItemStackState>();

    private ProfilerRecorder mainThreadRecorder;
    private ProfilerRecorder aiBudgetRecorder;
    private ProfilerRecorder gcAllocationRecorder;
    private float originalTimeScale = 1f;
    private int originalGameSpeed = 1;
    private bool originalPause;
    private GameSessionState gameData;
    private GameManager gameManager;
    private IGameSpeedController gameSpeedController;
    private CharacterAiScheduler scheduler;
    private Grid observedGrid;
    private IDungeonGameSaveSlotService slotService;
    private IGameplayFlowDiagnosticsQuery flowDiagnostics;
    private IWorkOrderRuntime workOrders;
    private IWorldItemStackRuntime itemStacks;
    private int invalidReservationSamples;
    private int overCapacitySamples;
    private int invalidQueueAccountingSamples;
    private int invalidActorPositionSamples;
    private int pausedSamples;
    private int observationSamples;
    private int flowObservationSamples;
    private int maxActiveWorkOrders;
    private int maxBlockedWorkOrders;
    private int maxLooseStackCount;
    private int totalDecisions;
    private int totalPathSearches;
    private int totalBrokerPathSearches;
    private int totalBrokerUrgentContinuationPathSearches;
    private int totalBrokerPathCacheHits;
    private int totalBrokerPathBudgetDeferrals;
    private int maxDecisions;
    private int maxPathSearches;
    private int maxBrokerPathSearches;
    private int maxBrokerNormalPathSearches;
    private int maxBrokerUrgentContinuationPathSearches;
    private int pathBudgetContractViolations;
    private int maxRegisteredCharacters;
    private long startMonoBytes;
    private long endMonoBytes;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Temp");
        PlayModeVerificationPersistenceSnapshot.CaptureCurrent("release-soak");
        Application.logMessageReceived += CaptureLog;
        originalTimeScale = Time.timeScale;

        yield return EnsurePlayableRun();
        yield return new WaitForSecondsRealtime(WarmupSeconds);

        DungeonRuntimeLifetimeScope scope = FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null && candidate.Container != null);
        Check(scope != null, "DI_SCOPE", "runtime container is available");

        if (scope != null)
        {
            slotService = scope.Container.Resolve<IDungeonGameSaveSlotService>();
            IGameSessionStateProvider dataProvider = scope.Container.Resolve<IGameSessionStateProvider>();
            dataProvider.TryGetSessionState(out gameData);
            flowDiagnostics = scope.Container.Resolve<IGameplayFlowDiagnosticsQuery>();
            workOrders = scope.Container.Resolve<IWorkOrderRuntime>();
            itemStacks = scope.Container.Resolve<IWorldItemStackRuntime>();
            gameSpeedController = scope.Container.Resolve<IGameSpeedController>();
        }

        gameManager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        scheduler = FindFirstObjectByType<CharacterAiScheduler>();
        GridSystemManager gridManager = FindFirstObjectByType<GridSystemManager>();
        observedGrid = gridManager != null ? gridManager.grid : null;
        Check(slotService != null
                && gameData != null
                && gameManager != null
                && gameSpeedController != null
                && scheduler != null,
            "RUNTIME_SERVICES",
            $"slots={slotService != null}; gameData={gameData != null}; "
            + $"gameManager={gameManager != null}; speed={gameSpeedController != null}; "
            + $"scheduler={scheduler != null}; grid={observedGrid != null}");

        if (slotService == null
            || gameData == null
            || gameManager == null
            || gameSpeedController == null
            || scheduler == null)
        {
            FinishAndExit();
            yield break;
        }

        originalGameSpeed = gameSpeedController.Speed;
        originalPause = gameSpeedController.IsPaused;

        gcAllocationRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Memory,
            "GC Allocated In Frame",
            1);
        yield return CaptureEditorGcBaseline();
        yield return CaptureEditorSteadyStateGc();
        mainThreadRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
        aiBudgetRecorder = ProfilerRecorder.StartNew(
            ProfilerCategory.Scripts,
            "CharacterAiScheduler.ProcessAiBudget",
            1);
        gameSpeedController.SetSpeed(SoakGameSpeed);
        gameSpeedController.SetPaused(false);
        yield return CaptureCleanPerformancePhase();

        int startDay = gameData.day.Value;
        int targetDay = startDay + RequiredOperatingDayAdvances;
        float startGameTime = gameData.curTime.Value;
        startMonoBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();

        SaveSnapshot("START_SAVE");
        float startedAt = Time.realtimeSinceStartup;
        float nextObservationAt = startedAt;
        float nextLogisticsObservationAt = startedAt;
        float nextSaveAt = startedAt + 30f;
        int timedSaveIndex = 1;

        while (ShouldContinueSoak(startedAt, targetDay))
        {
            if (scheduler != null)
            {
                totalDecisions += scheduler.LastProcessedDecisionCount;
                totalPathSearches += scheduler.LastPathSearchCount;
                totalBrokerPathSearches += scheduler.LastBrokerPathSearchCount;
                totalBrokerUrgentContinuationPathSearches +=
                    scheduler.LastBrokerUrgentOverdraftPathSearchCount;
                totalBrokerPathCacheHits += scheduler.LastBrokerPathCacheHitCount;
                totalBrokerPathBudgetDeferrals += scheduler.LastBrokerPathBudgetDeferralCount;
                maxDecisions = Mathf.Max(maxDecisions, scheduler.LastProcessedDecisionCount);
                maxPathSearches = Mathf.Max(maxPathSearches, scheduler.LastPathSearchCount);
                maxBrokerPathSearches = Mathf.Max(
                    maxBrokerPathSearches,
                    scheduler.LastBrokerPathSearchCount);
                int normalBrokerSearches = Mathf.Max(
                    0,
                    scheduler.LastBrokerPathSearchCount
                    - scheduler.LastBrokerUrgentOverdraftPathSearchCount);
                maxBrokerNormalPathSearches = Mathf.Max(
                    maxBrokerNormalPathSearches,
                    normalBrokerSearches);
                maxBrokerUrgentContinuationPathSearches = Mathf.Max(
                    maxBrokerUrgentContinuationPathSearches,
                    scheduler.LastBrokerUrgentOverdraftPathSearchCount);
                int pathBudget = scheduler.CurrentPathSearchBudget;
                if (normalBrokerSearches > pathBudget
                    || scheduler.LastBrokerUrgentOverdraftPathSearchCount
                        > GridPathSearchBroker.MaximumUrgentContinuationOverdraft
                    || scheduler.LastBrokerPathSearchCount
                        > pathBudget
                            + GridPathSearchBroker.MaximumUrgentContinuationOverdraft)
                {
                    pathBudgetContractViolations++;
                }
                maxRegisteredCharacters = Mathf.Max(maxRegisteredCharacters, scheduler.RegisteredCharacterCount);
            }

            if (Time.timeScale <= 0f)
            {
                pausedSamples++;
            }

            float now = Time.realtimeSinceStartup;
            if (now >= nextObservationAt)
            {
                ObserveWorld(now);
                nextObservationAt = now + ObservationInterval;
            }

            if (now >= nextLogisticsObservationAt)
            {
                ObserveWorkAndLogistics();
                nextLogisticsObservationAt = now + LogisticsObservationInterval;
            }

            if (now >= nextSaveAt && timedSaveIndex <= 2)
            {
                SaveSnapshot("TIMED_SAVE_" + timedSaveIndex);
                timedSaveIndex++;
                nextSaveAt += 30f;
            }

            yield return null;
        }

        ObserveWorld(Time.realtimeSinceStartup);
        ObserveWorkAndLogistics();
        SaveSnapshot("END_SAVE");
        endMonoBytes = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();

        int aiActorCount = actorObservations.Values.Count;
        int changedActorCount = actorObservations.Values.Count(item => item.ChangeCount > 0);
        float maxPendingSeconds = actorObservations.Count > 0
            ? actorObservations.Values.Max(item => item.MaxPendingSeconds)
            : 0f;
        float maxUnexplainedStationarySeconds = actorObservations.Count > 0
            ? actorObservations.Values.Max(item => item.MaxUnexplainedStationarySeconds)
            : 0f;
        int maxTwoCellOscillationReversals = actorObservations.Count > 0
            ? actorObservations.Values.Max(item => item.MaxTwoCellOscillationReversals)
            : 0;
        int maxReservationTargetChanges = actorObservations.Count > 0
            ? actorObservations.Values.Max(item => item.ReservationTargetChanges)
            : 0;
        int maxSameEpochReservationReacquires = actorObservations.Count > 0
            ? actorObservations.Values.Max(item => item.SameEpochReservationReacquires)
            : 0;
        int maxSameEpochDestinationSwitches = actorObservations.Count > 0
            ? actorObservations.Values.Max(item => item.SameEpochDestinationSwitches)
            : 0;
        bool reservationsConserved = actorObservations.Values.All(
            item => item.ReservationsConserved);
        bool reservationInvariantsClean = actorObservations.Values.All(
            item => item.ReservationInvariantAnomalyDelta == 0);
        int requiredChangedActors = aiActorCount > 0 ? Mathf.Max(1, Mathf.CeilToInt(aiActorCount * 0.5f)) : 0;

        float elapsedRealSeconds = Time.realtimeSinceStartup - startedAt;
        Check(gameData.day.Value >= targetDay,
            "FIRST_THREE_DAYS_ADVANCED",
            $"day={startDay}->{gameData.day.Value}; target={targetDay}; "
            + $"gameTime={startGameTime:0.0}->{gameData.curTime.Value:0.0}; "
            + $"realtime={elapsedRealSeconds:0.0}s");
        Check(observationSamples >= 60,
            "OBSERVATION_COVERAGE",
            $"samples={observationSamples}; realtime={elapsedRealSeconds:0.0}s");
        Check(aiActorCount == 0 || changedActorCount >= requiredChangedActors,
            "AI_STATE_PROGRESS",
            $"observed={aiActorCount}; changed={changedActorCount}; required={requiredChangedActors}; {DescribeActors()}");
        Check(maxPendingSeconds <= 15f,
            "AI_PENDING_BOUND",
            $"maxPendingRealtime={maxPendingSeconds:0.00}s");
        Check(maxUnexplainedStationarySeconds <= 10f,
            "AI_UNEXPLAINED_STATIONARY_BOUND",
            $"maxRealtime={maxUnexplainedStationarySeconds:0.00}s; {DescribeActors()}");
        Check(maxTwoCellOscillationReversals < 6,
            "AI_TWO_CELL_OSCILLATION",
            $"maxConsecutiveReversals={maxTwoCellOscillationReversals}; {DescribeActors()}");
        Check(reservationsConserved
                && reservationInvariantsClean
                && maxSameEpochReservationReacquires < 3
                && maxSameEpochDestinationSwitches == 0,
            "AI_RESERVATION_CHURN",
            $"rawTargetChanges={maxReservationTargetChanges}; "
            + $"sameEpochReacquires={maxSameEpochReservationReacquires}; "
            + $"sameEpochSwitches={maxSameEpochDestinationSwitches}; "
            + $"conserved={reservationsConserved}; "
            + $"invariantsClean={reservationInvariantsClean}; {DescribeActors()}");
        Check(totalDecisions > 0 && maxDecisions <= 16,
            "AI_DECISION_BUDGET",
            $"total={totalDecisions}; maxPerFrame={maxDecisions}; registeredMax={maxRegisteredCharacters}");
        Check(totalPathSearches + totalBrokerPathSearches + totalBrokerPathCacheHits > 0
                && maxPathSearches <= 8
                && pathBudgetContractViolations == 0,
            "AI_PATH_BUDGET",
            $"scheduler={totalPathSearches}; broker={totalBrokerPathSearches}; "
            + $"urgentContinuations={totalBrokerUrgentContinuationPathSearches}; "
            + $"cacheHits={totalBrokerPathCacheHits}; deferrals={totalBrokerPathBudgetDeferrals}; "
            + $"maxScheduler={maxPathSearches}; maxBroker={maxBrokerPathSearches}; "
            + $"maxNormal={maxBrokerNormalPathSearches}; "
            + $"maxUrgent={maxBrokerUrgentContinuationPathSearches}; "
            + $"urgentLimit={GridPathSearchBroker.MaximumUrgentContinuationOverdraft}; "
            + $"contractViolations={pathBudgetContractViolations}");
        Check(invalidReservationSamples == 0,
            "RESERVATION_OWNERSHIP",
            $"invalidSamples={invalidReservationSamples}");
        Check(overCapacitySamples == 0,
            "FACILITY_CAPACITY",
            $"activeUserOverCapacitySamples={overCapacitySamples}");
        Check(invalidQueueAccountingSamples == 0,
            "FACILITY_FIFO_QUEUE_ACCOUNTING",
            $"invalidSamples={invalidQueueAccountingSamples}");
        Check(invalidActorPositionSamples == 0,
            "ACTOR_POSITIONS",
            $"invalidSamples={invalidActorPositionSamples}");
        Check(pausedSamples == 0,
            "UNEXPECTED_PAUSE",
            $"pausedFrames={pausedSamples}");
        Check(flowObservationSamples > 0,
            "WORK_LOGISTICS_DIAGNOSTICS",
            $"samples={flowObservationSamples}; maxOrders={maxActiveWorkOrders}; "
            + $"maxBlocked={maxBlockedWorkOrders}; maxLoose={maxLooseStackCount}; "
            + $"summaries={string.Join(" | ", observedFlowSummaries)}; "
            + $"details={string.Join(" | ", observedFlowItems)}");
        Check(workOrders == null || observedWorkOrderStates.All(IsKnownWorkOrderState),
            "WORK_ORDER_STATE_VALIDITY",
            $"states={string.Join(",", observedWorkOrderStates.OrderBy(state => state))}");
        Check(itemStacks == null || observedItemStackStates.All(IsKnownItemStackState),
            "ITEM_STACK_STATE_VALIDITY",
            $"states={string.Join(",", observedItemStackStates.OrderBy(state => state))}");
        Check(itemStacks == null
                || (maxLooseStackCount > 0
                    && observedItemStackStates.Contains(WorldItemStackState.Stored)),
            "STARTER_SUPPLY_HAUL_FLOW",
            $"maxLoose={maxLooseStackCount}; "
            + $"states={string.Join(",", observedItemStackStates.OrderBy(state => state))}");

        VerifySaveGrowth();
        VerifyPerformance();
        yield return CaptureScreen();

        bool loaded = slotService.TryLoad(SoakSlot, out DungeonGameRestoreReport restoreReport);
        yield return null;
        yield return null;
        Check(loaded && restoreReport != null && restoreReport.Success && restoreReport.Warnings.Count == 0,
            "SOAK_SAVE_RELOAD",
            restoreReport == null
                ? "missing restore report"
                : $"loaded={loaded}; buildings={restoreReport.RestoredBuildingCount}; characters={restoreReport.RestoredCharacterCount}; warnings={restoreReport.Warnings.Count}; errors={restoreReport.Errors.Count}; "
                    + $"details={string.Join(" | ", restoreReport.Errors)}");

        FinishAndExit();
    }

    private IEnumerator EnsurePlayableRun()
    {
        yield return null;
        Button continueButton = FindSceneButton("ContinueLatestButton");
        if (continueButton != null && continueButton.gameObject.activeInHierarchy && continueButton.interactable)
        {
            PressButton(continueButton);
            yield return new WaitForSecondsRealtime(0.5f);
        }

        GameObject saveModal = FindSceneObject("SaveModal");
        if (saveModal != null && saveModal.activeInHierarchy)
        {
            Button startNewButton = FindSceneButton("StartNewRunButton");
            if (startNewButton != null && startNewButton.interactable)
            {
                PressButton(startNewButton);
                yield return null;
                if (saveModal.activeInHierarchy)
                {
                    PressButton(startNewButton);
                    yield return null;
                }
            }
        }

        Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.interactable
                && button.name.StartsWith("OwnerOption_", StringComparison.Ordinal));
        if (ownerButton != null)
        {
            PressButton(ownerButton);
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible();
            yield return new WaitForSecondsRealtime(0.5f);
        }

        bool ownerSelectionVisible = Resources.FindObjectsOfTypeAll<OwnerSelectionPanel>()
            .Any(panel => panel != null
                && panel.gameObject.scene.IsValid()
                && panel.gameObject.activeInHierarchy);
        Check((saveModal == null || !saveModal.activeInHierarchy) && !ownerSelectionVisible,
            "GAME_READY",
            "startup and owner selection panels are closed");
    }

    private void ObserveWorld(float now)
    {
        observationSamples++;
        CharacterActor[] actors = FindObjectsByType<CharacterActor>(FindObjectsSortMode.None);
        foreach (CharacterActor actor in actors)
        {
            if (actor == null || actor.IsDead || !actor.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 position = actor.transform.position;
            bool finite = float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z);
            if (!finite)
            {
                invalidActorPositionSamples++;
                continue;
            }

            if (observedGrid != null && actor.CurrentLifecycleState == CharacterLifecycleState.Active)
            {
                Vector2Int cell = observedGrid.GetXY(position);
                if (cell.x < 0
                    || cell.x >= observedGrid.width
                    || cell.y < 0
                    || cell.y >= observedGrid.height)
                {
                    invalidActorPositionSamples++;
                }
            }

            if (!actor.CanRunAi || actor.IsOwner)
            {
                continue;
            }

            int id = actor.GetInstanceID();
            string signature = GetActorSignature(actor, observedGrid);
            if (!actorObservations.TryGetValue(id, out ActorObservation observation))
            {
                observation = new ActorObservation(actor, signature, now);
                actorObservations.Add(id, observation);
            }

            observation.Observe(
                actor,
                signature,
                actor.IsAiDecisionPending,
                now,
                scheduler != null ? scheduler.GetNextDecisionDelayForDebug(actor) : -1f);
        }

        BuildableObject[] buildings = FindObjectsByType<BuildableObject>(FindObjectsSortMode.None);
        foreach (BuildableObject building in buildings)
        {
            if (building == null || building.isDestroy || !building.gameObject.activeInHierarchy)
            {
                continue;
            }

            int reservations = building.ActiveVisitReservationCount;
            int capacity = Mathf.Max(0, building.EffectiveCapacity);
            int users = building.CurrentUserCount;
            if (capacity < int.MaxValue && users > capacity)
            {
                overCapacitySamples++;
            }

            int availableSlots = capacity == int.MaxValue
                ? int.MaxValue
                : Mathf.Max(0, capacity - users);
            int expectedWaiting = availableSlots == int.MaxValue
                ? 0
                : Mathf.Max(0, reservations - availableSlots);
            if (building.WaitingVisitReservationCount != expectedWaiting)
            {
                invalidQueueAccountingSamples++;
            }

            foreach (CharacterId reservationId in
                     building.CaptureVisitReservationIdsForDiagnostics())
            {
                CharacterActor actor = actors.FirstOrDefault(candidate =>
                    candidate != null
                    && candidate.BuildingCharacterId.Equals(reservationId));
                AIAction action = actor != null && actor.Brain != null
                    ? actor.Brain.bestAction
                    : null;
                if (actor == null
                    || actor.IsDead
                    || !actor.gameObject.activeInHierarchy
                    || action == null
                    || !action.HasReservation
                    || action.ReservedDestination != building)
                {
                    invalidReservationSamples++;
                }
            }

            IBuildingCharacterPort workerReservation = building.WorkerReservation;
            CharacterActor worker = workerReservation as CharacterActor;
            if (workerReservation != null && worker == null)
            {
                invalidReservationSamples++;
            }
            else if (worker != null)
            {
                AIAction action = worker.Brain != null ? worker.Brain.bestAction : null;
                if (worker.IsDead
                    || !worker.gameObject.activeInHierarchy
                    || action == null
                    || !action.HasReservation
                    || action.ReservedDestination != building)
                {
                    invalidReservationSamples++;
                }
            }
        }
    }

    private void ObserveWorkAndLogistics()
    {
        if (flowDiagnostics != null)
        {
            GameplayFlowDiagnosticsSnapshot snapshot = flowDiagnostics.Capture();
            if (snapshot != null)
            {
                flowObservationSamples++;
                maxActiveWorkOrders = Mathf.Max(maxActiveWorkOrders, snapshot.ActiveOrderCount);
                maxBlockedWorkOrders = Mathf.Max(maxBlockedWorkOrders, snapshot.BlockedOrderCount);
                maxLooseStackCount = Mathf.Max(maxLooseStackCount, snapshot.LooseStackCount);
                if (!string.IsNullOrWhiteSpace(snapshot.Summary))
                {
                    observedFlowSummaries.Add(snapshot.Summary);
                }
                foreach (GameplayFlowDiagnosticItem item in snapshot.Items
                             ?? Array.Empty<GameplayFlowDiagnosticItem>())
                {
                    if (item != null)
                    {
                        observedFlowItems.Add($"{item.Title}: {item.Detail}");
                    }
                }
            }
        }

        DungeonWorkOrderSaveData workSnapshot = workOrders?.Capture();
        foreach (WorkOrderSaveData order in workSnapshot?.orders ?? Enumerable.Empty<WorkOrderSaveData>())
        {
            if (order != null)
            {
                observedWorkOrderStates.Add(order.status);
            }
        }

        foreach (WorldItemStackSnapshot stack in itemStacks?.GetAllStacks()
                     ?? Array.Empty<WorldItemStackSnapshot>())
        {
            if (stack != null && stack.Quantity > 0)
            {
                observedItemStackStates.Add(stack.State);
            }
        }
    }

    private bool ShouldContinueSoak(float startedAt, int targetDay)
    {
        float elapsed = Time.realtimeSinceStartup - startedAt;
        if (elapsed < MinimumSoakRealSeconds)
        {
            return true;
        }

        return elapsed < MaximumSoakRealSeconds && gameData.day.Value < targetDay;
    }

    private static bool IsKnownWorkOrderState(WorkOrderStatus state)
    {
        return Enum.IsDefined(typeof(WorkOrderStatus), state);
    }

    private static bool IsKnownItemStackState(WorldItemStackState state)
    {
        return Enum.IsDefined(typeof(WorldItemStackState), state);
    }

    private void SaveSnapshot(string label)
    {
        string path = slotService.Save(SoakSlot);
        long size = File.Exists(path) ? new FileInfo(path).Length : -1L;
        saveSizes.Add(size);
        report.Add($"{label} path={path}; bytes={size}");
    }

    private void VerifySaveGrowth()
    {
        bool allValid = saveSizes.Count >= 4 && saveSizes.All(size => size > 0);
        long first = allValid ? saveSizes[0] : 0L;
        long largest = allValid ? saveSizes.Max() : 0L;
        long growth = largest - first;
        long allowed = Math.Max(196608L, first);
        Check(allValid && growth <= allowed,
            "SAVE_GROWTH_BOUND",
            $"sizes={string.Join(",", saveSizes)}; growth={growth}; allowed={allowed}");
    }

    private void VerifyPerformance()
    {
        double p95FrameMs = Percentile(frameTimesMs.Select(value => (double)value), 0.95);
        double p95MainThreadMs = Percentile(mainThreadTimesMs, 0.95);
        double p95AiBudgetMarkerMs = Percentile(aiBudgetMarkerTimesMs, 0.95);
        double p95SchedulerMs = Percentile(schedulerTimesMs, 0.95);
        double baselineAverageBytes = editorBaselineGcAllocations.Count > 0
            ? editorBaselineGcAllocations.Average()
            : 0d;
        double baselineP95Bytes = Percentile(
            editorBaselineGcAllocations.Select(value => (double)value),
            0.95);
        double steadyAverageBytes = editorSteadyGcAllocations.Count > 0
            ? editorSteadyGcAllocations.Average()
            : 0d;
        double steadyP95Bytes = Percentile(
            editorSteadyGcAllocations.Select(value => (double)value),
            0.95);
        double burstAverageBytes = gcAllocations.Count > 0
            ? gcAllocations.Average()
            : 0d;
        double burstMaximumBytes = gcAllocations.Count > 0
            ? gcAllocations.Max()
            : 0d;
        double incrementalAverageBytes = GameplayGcAcceptancePolicy.IncrementalBytes(
            steadyAverageBytes,
            baselineAverageBytes);
        double incrementalP95Bytes = GameplayGcAcceptancePolicy.IncrementalBytes(
            steadyP95Bytes,
            baselineP95Bytes);
        double monoGrowthMb = (endMonoBytes - startMonoBytes) / 1024.0 / 1024.0;

        Check(frameTimesMs.Count > 100 && p95FrameMs <= 100.0,
            "FRAME_TIME",
            $"samples={frameTimesMs.Count}; p95={p95FrameMs:0.00}ms");
        Check(mainThreadRecorder.Valid
                && mainThreadTimesMs.Count > 100
                && mainThreadTimesMs.Any(value => value > 0d)
                && p95MainThreadMs <= 100.0,
            "MAIN_THREAD_TIME",
            $"samples={mainThreadTimesMs.Count}; p95={p95MainThreadMs:0.00}ms");
        Check(aiBudgetRecorder.Valid
                && aiBudgetMarkerTimesMs.Count > 100
                && aiBudgetMarkerTimesMs.Any(value => value > 0d)
                && p95AiBudgetMarkerMs <= 8.0,
            "AI_BUDGET_MARKER_TIME",
            $"samples={aiBudgetMarkerTimesMs.Count}; p95={p95AiBudgetMarkerMs:0.000}ms");
        Check(schedulerTimesMs.Count > 100 && p95SchedulerMs <= 8.0,
            "AI_PROCESSING_TIME",
            $"samples={schedulerTimesMs.Count}; p95={p95SchedulerMs:0.000}ms");
        Check(endMonoBytes - startMonoBytes <= GameplayGcAcceptancePolicy.RetainedMonoGrowthBytes,
            "MONO_MEMORY_GROWTH",
            $"start={startMonoBytes}; end={endMonoBytes}; delta={monoGrowthMb:0.00}MB");
        Check(editorBaselineGcAllocations.Count
                == GameplayGcAcceptancePolicy.EditorBaselineSampleFrames,
            "EDITOR_GC_BASELINE_COVERAGE",
            $"samples={editorBaselineGcAllocations.Count}; "
            + $"average={baselineAverageBytes / 1024d:0.0}KB; "
            + $"p95={baselineP95Bytes / 1024d:0.0}KB");
        Check(editorSteadyGcAllocations.Count
                == GameplayGcAcceptancePolicy.EditorBaselineSampleFrames,
            "EDITOR_GC_STEADY_COVERAGE",
            $"samples={editorSteadyGcAllocations.Count}; "
            + $"average={steadyAverageBytes / 1024d:0.0}KB; "
            + $"p95={steadyP95Bytes / 1024d:0.0}KB");
        Check(GameplayGcAcceptancePolicy.PassesEditorIncremental(
                incrementalAverageBytes,
                incrementalP95Bytes),
            "EDITOR_GC_INCREMENT",
            $"steadySamples={editorSteadyGcAllocations.Count}; "
            + $"baselineAverage={baselineAverageBytes / 1024d:0.0}KB; "
            + $"steadyAverage={steadyAverageBytes / 1024d:0.0}KB; "
            + $"incrementAverage={incrementalAverageBytes / 1024d:0.0}KB; "
            + $"baselineP95={baselineP95Bytes / 1024d:0.0}KB; "
            + $"steadyP95={steadyP95Bytes / 1024d:0.0}KB; "
            + $"incrementP95={incrementalP95Bytes / 1024d:0.0}KB; "
            + $"budgets={GameplayGcAcceptancePolicy.EditorIncrementalAverageBytesPerFrame / 1024L}KB/"
            + $"{GameplayGcAcceptancePolicy.EditorIncrementalP95BytesPerFrame / 1024L}KB");
        Check(GameplayGcAcceptancePolicy.PassesEditorRunawayGuard(
                burstAverageBytes,
                burstMaximumBytes),
            "EDITOR_GC_RUNAWAY_GUARD",
            $"burstSamples={gcAllocations.Count}; "
            + $"average={burstAverageBytes / 1024d:0.0}KB; "
            + $"maximum={burstMaximumBytes / 1024d:0.0}KB; "
            + $"guards={GameplayGcAcceptancePolicy.EditorAbsoluteAverageRunawayBytesPerFrame / 1024L}KB/"
            + $"{GameplayGcAcceptancePolicy.EditorAbsoluteMaximumRunawayBytesPerFrame / 1024L}KB");
    }

    private IEnumerator CaptureEditorGcBaseline()
    {
        gameSpeedController.SetPaused(true);
        for (int frame = 0;
            frame < GameplayGcAcceptancePolicy.EditorBaselineWarmupFrames;
            frame++)
        {
            yield return new WaitForEndOfFrame();
        }

        editorBaselineGcAllocations.Clear();
        for (int frame = 0;
            frame < GameplayGcAcceptancePolicy.EditorBaselineSampleFrames;
            frame++)
        {
            yield return new WaitForEndOfFrame();
            if (gcAllocationRecorder.Valid)
            {
                editorBaselineGcAllocations.Add(
                    Math.Max(0L, gcAllocationRecorder.LastValue));
            }
        }
    }

    private IEnumerator CaptureEditorSteadyStateGc()
    {
        gameSpeedController.SetSpeed(1);
        gameSpeedController.SetPaused(false);
        for (int frame = 0;
            frame < GameplayGcAcceptancePolicy.EditorBaselineWarmupFrames;
            frame++)
        {
            yield return new WaitForEndOfFrame();
        }

        editorSteadyGcAllocations.Clear();
        for (int frame = 0;
            frame < GameplayGcAcceptancePolicy.EditorBaselineSampleFrames;
            frame++)
        {
            yield return new WaitForEndOfFrame();
            if (gcAllocationRecorder.Valid)
            {
                editorSteadyGcAllocations.Add(
                    Math.Max(0L, gcAllocationRecorder.LastValue));
            }
        }

        report.Add(
            $"EDITOR_GC_STEADY_PHASE samples={editorSteadyGcAllocations.Count}; "
            + "speed=1; paused=false; observation=off; save=off; screenshot=off");
    }

    private IEnumerator CaptureCleanPerformancePhase()
    {
        gameSpeedController.SetSpeed(SoakGameSpeed);
        gameSpeedController.SetPaused(false);
        for (int frame = 0; frame < PerformanceWarmupFrames; frame++)
        {
            yield return new WaitForEndOfFrame();
        }

        int actorCount = FindObjectsByType<CharacterActor>(FindObjectsSortMode.None)
            .Count(actor => actor != null && actor.gameObject.activeInHierarchy);
        int buildingCount = FindObjectsByType<BuildableObject>(FindObjectsSortMode.None)
            .Count(building => building != null
                && !building.isDestroy
                && building.gameObject.activeInHierarchy);
        int itemStackCount = itemStacks?.GetAllStacks()?.Count ?? 0;
        report.Add(
            $"PERFORMANCE_WORKLOAD scene={gameObject.scene.name}; "
            + $"actors={actorCount}; buildings={buildingCount}; "
            + $"itemStacks={itemStackCount}; day={gameData.day.Value}; "
            + $"gameTime={gameData.curTime.Value:0.0}; speed={gameSpeedController.Speed}; "
            + $"sourceVersion={Application.version}");

        frameTimesMs.Clear();
        mainThreadTimesMs.Clear();
        aiBudgetMarkerTimesMs.Clear();
        schedulerTimesMs.Clear();
        gcAllocations.Clear();
        float startedAt = Time.realtimeSinceStartup;
        while (frameTimesMs.Count < PerformanceMinimumFrames
            || Time.realtimeSinceStartup - startedAt < PerformanceMinimumSeconds)
        {
            yield return new WaitForEndOfFrame();
            frameTimesMs.Add(Time.unscaledDeltaTime * 1000f);
            mainThreadTimesMs.Add(
                mainThreadRecorder.Valid
                    ? Math.Max(0d, mainThreadRecorder.LastValue / 1000000d)
                    : 0d);
            aiBudgetMarkerTimesMs.Add(
                aiBudgetRecorder.Valid
                    ? Math.Max(0d, aiBudgetRecorder.LastValue / 1000000d)
                    : 0d);
            schedulerTimesMs.Add(
                scheduler != null
                    ? Math.Max(0d, scheduler.LastProcessingMilliseconds)
                    : 0d);
            if (gcAllocationRecorder.Valid)
            {
                long allocated = Math.Max(0L, gcAllocationRecorder.LastValue);
                gcAllocations.Add(allocated);
            }
        }

        report.Add(
            $"PERFORMANCE_CLEAN_PHASE samples={frameTimesMs.Count}; "
            + $"realtime={Time.realtimeSinceStartup - startedAt:0.0}s; "
            + "observation=off; save=off; screenshot=off");
    }

    private IEnumerator CaptureScreen()
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels = capture.GetPixels32();
        int visible = pixels.Count(pixel => pixel.a > 0 && (pixel.r > 5 || pixel.g > 5 || pixel.b > 5));
        string path = "Temp/release-soak.png";
        File.WriteAllBytes(path, capture.EncodeToPNG());
        Check(capture.width > 0 && capture.height > 0 && visible > pixels.Length / 20,
            "SOAK_CAPTURE",
            $"path={path}; size={capture.width}x{capture.height}; visible={visible}");
        Destroy(capture);
    }

    private void FinishAndExit()
    {
        if (mainThreadRecorder.Valid)
        {
            mainThreadRecorder.Dispose();
        }

        if (aiBudgetRecorder.Valid)
        {
            aiBudgetRecorder.Dispose();
        }

        if (gcAllocationRecorder.Valid)
        {
            gcAllocationRecorder.Dispose();
        }

        if (gameSpeedController != null)
        {
            gameSpeedController.SetSpeed(originalGameSpeed);
            gameSpeedController.SetPaused(originalPause);
        }
        else
        {
            Time.timeScale = originalTimeScale;
        }
        Application.logMessageReceived -= CaptureLog;
        report.Add($"capturedErrors={errors.Count}; capturedWarnings={warnings.Count}");
        foreach (string error in errors)
        {
            report.Add("[CONSOLE ERROR] " + Compact(error));
        }

        foreach (string warning in warnings)
        {
            report.Add("[CONSOLE WARNING] " + Compact(warning));
        }

        bool passed = failures.Count == 0 && errors.Count == 0 && warnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {string.Join(" || ", failures)}");
        File.WriteAllText(DungeonReleaseSoakPlayModeVerifier.ReportPath, string.Join("\n", report));
        if (passed)
        {
            Debug.Log("Release soak verification passed. " + DungeonReleaseSoakPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Release soak verification failed. " + DungeonReleaseSoakPlayModeVerifier.ReportPath);
        }

        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private void Check(bool passed, string key, string detail)
    {
        report.Add($"{key}={(passed ? "PASS" : "FAIL")}; {detail}");
        if (!passed)
        {
            failures.Add(key + ": " + detail);
        }
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(condition + "\n" + stackTrace);
        }
    }

    private string DescribeActors()
    {
        return string.Join(
            " | ",
            actorObservations.Values
                .OrderBy(item => item.Name, StringComparer.Ordinal)
                .Select(item => $"{item.Name}:changes={item.ChangeCount},"
                    + $"pending={item.MaxPendingSeconds:0.0}s,"
                    + $"pendingProgressResets={item.PendingProgressResets},"
                    + $"pendingContext={item.MaxPendingContext},"
                    + $"stationary={item.MaxUnexplainedStationarySeconds:0.0}s,"
                    + $"oscillation={item.MaxTwoCellOscillationReversals},"
                    + $"reservationChanges={item.ReservationTargetChanges},"
                    + $"sameEpochReacquires={item.SameEpochReservationReacquires},"
                    + $"sameEpochSwitches={item.SameEpochDestinationSwitches},"
                    + $"reservationConserved={item.ReservationsConserved},"
                    + $"invariantDelta={item.ReservationInvariantAnomalyDelta}"));
    }

    private static string GetActorSignature(CharacterActor actor, Grid grid)
    {
        Vector2Int cell = grid != null ? grid.GetXY(actor.transform.position) : Vector2Int.zero;
        string branch = actor.Blackboard != null ? actor.Blackboard.CurrentBranch.ToString() : "None";
        string task = actor.Blackboard != null ? actor.Blackboard.CurrentTask : string.Empty;
        string action = actor.Brain?.bestAction?.actionset != null
            ? actor.Brain.bestAction.actionset.name
            : "None";
        return $"{cell.x},{cell.y}|{branch}|{task}|{action}|logs={actor.Log.Count}";
    }

    private static double Percentile(IEnumerable<double> source, double percentile)
    {
        double[] values = source.OrderBy(value => value).ToArray();
        if (values.Length == 0)
        {
            return 0.0;
        }

        int index = Mathf.Clamp(
            Mathf.CeilToInt((float)(percentile * values.Length)) - 1,
            0,
            values.Length - 1);
        return values[index];
    }

    private static Button FindSceneButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.name == name);
    }

    private static GameObject FindSceneObject(string name)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Where(transform => transform != null && transform.gameObject.scene.IsValid())
            .Select(transform => transform.gameObject)
            .FirstOrDefault(gameObject => gameObject.name == name);
    }

    private static void PressButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.OnPointerClick(new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        });
    }

    private static string Compact(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private sealed class ActorObservation
    {
        private string lastSignature;
        private Vector2Int lastCell;
        private Vector2Int previousCell;
        private string lastReservationTarget = string.Empty;
        private float pendingSince = -1f;
        private float unexplainedStationarySince = -1f;
        private long lastSchedulerProcesses = -1L;
        private long lastRuntimeProgressRevision = -1L;
        private long reservationEpoch = -1L;
        private long reservationReleasedAtProgressRevision = -1L;
        private CharacterAiRuntimeGateSnapshot reservationGateStart;
        private CharacterAiRuntimeGateSnapshot reservationGateEnd;
        private bool reservationSeenInEpoch;
        private bool reservationReleasedInEpoch;
        private bool hasCell;
        private int currentTwoCellOscillationReversals;

        public ActorObservation(CharacterActor actor, string signature, float now)
        {
            Name = actor == null || string.IsNullOrWhiteSpace(actor.name)
                ? "Character"
                : actor.name;
            lastSignature = signature;
            LastObservedAt = now;
            AIBrain brain = actor?.Brain;
            reservationEpoch = brain?.RuntimeActionEpoch ?? 0L;
            lastRuntimeProgressRevision = brain?.RuntimeProgressRevision ?? 0L;
            reservationGateStart = brain?.CaptureRuntimeGateSnapshot() ?? default;
            reservationGateEnd = reservationGateStart;
        }

        public string Name { get; }
        public int ChangeCount { get; private set; }
        public float MaxPendingSeconds { get; private set; }
        public string MaxPendingContext { get; private set; } = string.Empty;
        public float MaxUnexplainedStationarySeconds { get; private set; }
        public int MaxTwoCellOscillationReversals { get; private set; }
        public int ReservationTargetChanges { get; private set; }
        public int SameEpochReservationReacquires { get; private set; }
        public int SameEpochDestinationSwitches { get; private set; }
        public int PendingProgressResets { get; private set; }
        public float LastObservedAt { get; private set; }
        public bool ReservationsConserved =>
            reservationGateEnd.ConservesReservationsFrom(in reservationGateStart);
        public long ReservationInvariantAnomalyDelta =>
            reservationGateEnd.InvariantAnomalies
            - reservationGateStart.InvariantAnomalies;

        public void Observe(
            CharacterActor actor,
            string signature,
            bool pending,
            float now,
            float nextDecisionDelay)
        {
            if (!string.Equals(lastSignature, signature, StringComparison.Ordinal))
            {
                ChangeCount++;
                lastSignature = signature;
            }

            AIBrain brain = actor?.Brain;
            long schedulerProcesses = brain?.RuntimeSchedulerProcessCount ?? 0L;
            long runtimeProgressRevision = brain?.RuntimeProgressRevision ?? 0L;
            long actionEpoch = brain?.RuntimeActionEpoch ?? 0L;
            reservationGateEnd = brain?.CaptureRuntimeGateSnapshot() ?? default;
            if (actionEpoch != reservationEpoch)
            {
                reservationEpoch = actionEpoch;
                lastReservationTarget = GetReservationTarget(actor);
                reservationSeenInEpoch =
                    !string.IsNullOrWhiteSpace(lastReservationTarget);
                reservationReleasedInEpoch = false;
                reservationReleasedAtProgressRevision = -1L;
            }
            bool authoritativeProgress = lastSchedulerProcesses >= 0L
                && (schedulerProcesses > lastSchedulerProcesses
                    || runtimeProgressRevision > lastRuntimeProgressRevision);
            lastSchedulerProcesses = schedulerProcesses;
            lastRuntimeProgressRevision = runtimeProgressRevision;

            if (pending)
            {
                if (pendingSince < 0f || authoritativeProgress)
                {
                    pendingSince = now;
                    if (authoritativeProgress)
                    {
                        PendingProgressResets++;
                    }
                }

                float pendingSeconds = now - pendingSince;
                if (pendingSeconds >= MaxPendingSeconds)
                {
                    MaxPendingSeconds = pendingSeconds;
                    MaxPendingContext = DescribePendingContext(
                        actor,
                        nextDecisionDelay,
                        schedulerProcesses,
                        runtimeProgressRevision);
                }
            }
            else
            {
                pendingSince = -1f;
            }

            Vector2Int currentCell = actor != null ? actor.GetNowXY() : Vector2Int.zero;
            bool cellChanged = hasCell && currentCell != lastCell;
            if (!hasCell || cellChanged)
            {
                unexplainedStationarySince = -1f;
                if (hasCell && currentCell == previousCell && currentCell != lastCell)
                {
                    currentTwoCellOscillationReversals++;
                    MaxTwoCellOscillationReversals = Mathf.Max(
                        MaxTwoCellOscillationReversals,
                        currentTwoCellOscillationReversals);
                }
                else
                {
                    currentTwoCellOscillationReversals = 0;
                }

                previousCell = lastCell;
                lastCell = currentCell;
                hasCell = true;
            }
            else if (IsUnexplainedStationary(actor, pending))
            {
                if (unexplainedStationarySince < 0f)
                {
                    unexplainedStationarySince = now;
                }

                MaxUnexplainedStationarySeconds = Mathf.Max(
                    MaxUnexplainedStationarySeconds,
                    now - unexplainedStationarySince);
            }
            else
            {
                unexplainedStationarySince = -1f;
            }

            string reservationTarget = GetReservationTarget(actor);
            if (!string.Equals(
                    reservationTarget,
                    lastReservationTarget,
                    StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(lastReservationTarget)
                    || !string.IsNullOrWhiteSpace(reservationTarget))
                {
                    ReservationTargetChanges++;
                }

                bool hadTarget = !string.IsNullOrWhiteSpace(lastReservationTarget);
                bool hasTarget = !string.IsNullOrWhiteSpace(reservationTarget);
                if (hadTarget && hasTarget)
                {
                    SameEpochDestinationSwitches++;
                    reservationSeenInEpoch = true;
                    reservationReleasedInEpoch = false;
                }
                else if (hadTarget)
                {
                    reservationReleasedInEpoch = true;
                    reservationReleasedAtProgressRevision = runtimeProgressRevision;
                }
                else if (hasTarget)
                {
                    if (reservationSeenInEpoch
                        && reservationReleasedInEpoch
                        && runtimeProgressRevision
                            <= reservationReleasedAtProgressRevision)
                    {
                        SameEpochReservationReacquires++;
                    }

                    reservationSeenInEpoch = true;
                    reservationReleasedInEpoch = false;
                }

                lastReservationTarget = reservationTarget;
            }

            LastObservedAt = now;
        }

        private static string DescribePendingContext(
            CharacterActor actor,
            float nextDecisionDelay,
            long schedulerProcesses,
            long runtimeProgressRevision)
        {
            if (actor == null)
            {
                return "actor=null";
            }

            AIBrain brain = actor.Brain;
            CharacterBlackboard blackboard = actor.Blackboard;
            string failure = brain != null && brain.LastActionFailure.HasFailure
                ? Compact(brain.LastActionFailure.ToString())
                : "none";
            return Compact(
                $"cell={actor.GetNowXY()}; "
                + $"branch={blackboard?.CurrentBranch}; task={blackboard?.CurrentTask}; "
                + $"status={blackboard?.CurrentStatus}; "
                + $"action={brain?.CurrentActionDebugLabel}; phase={brain?.CurrentActionPhase}; "
                + $"detail={brain?.CurrentActionPhaseDetail}; failure={failure}; "
                + $"next={nextDecisionDelay:0.00}s; "
                + $"schedulerProcesses={schedulerProcesses}; "
                + $"runtimeProgress={runtimeProgressRevision}");
        }

        private static bool IsUnexplainedStationary(CharacterActor actor, bool pending)
        {
            if (actor == null
                || pending
                || actor.CurrentLifecycleState != CharacterLifecycleState.Active
                || !actor.CanRunAi)
            {
                return false;
            }

            AIBrain brain = actor.Brain;
            CharacterBlackboard blackboard = actor.Blackboard;
            if (brain == null || blackboard == null)
            {
                return true;
            }

            string phase = brain.CurrentActionPhase ?? string.Empty;
            CharacterAiBranch branch = blackboard.CurrentBranch;
            bool idleBranch = branch == CharacterAiBranch.Wait
                || branch == CharacterAiBranch.Idle
                || branch == CharacterAiBranch.RoutineUtility;
            bool idlePhase = string.IsNullOrWhiteSpace(phase)
                || phase.Contains("대기", StringComparison.Ordinal)
                || phase.Contains("갈 곳 찾는 중", StringComparison.Ordinal)
                || phase.Contains("판단", StringComparison.Ordinal);
            return idleBranch && idlePhase;
        }

        private static string GetReservationTarget(CharacterActor actor)
        {
            AIAction action = actor?.Brain?.bestAction;
            if (action == null || !action.HasReservation)
            {
                return string.Empty;
            }

            BuildableObject destination = action.ReservedDestination;
            if (destination == null)
            {
                return "<missing>";
            }

            return destination.GetInstanceID().ToString();
        }
    }
}
