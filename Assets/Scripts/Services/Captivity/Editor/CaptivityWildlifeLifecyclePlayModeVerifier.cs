#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

/// <summary>
/// Production-live lifecycle coverage for captivity escape, wildlife capture
/// transport, and animal-care AI work.  Fixture state is authored through the
/// registered V18 save sections; terminal state is reached only by the real
/// Brain/Ability/runtime pipeline.
/// </summary>
public static class CaptivityWildlifeLifecyclePlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/captivity-wildlife-lifecycle-playmode.txt";
    private const string PendingPath =
        "Temp/captivity-wildlife-lifecycle-playmode.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private static readonly List<string> teardownConsoleIssues = new();
    private static bool teardownConsoleCaptureArmed;

    [MenuItem("DungeonStory/Debug/QA/Run Captivity Wildlife Lifecycle Matrix")]
    public static void RequestRun()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingPath, DateTime.UtcNow.ToString("O"));
        if (EditorApplication.isPlaying)
        {
            StartRunner(exitPlayMode: false);
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!File.Exists(PendingPath)) return;
        File.Delete(PendingPath);
        StartRunner(exitPlayMode: true);
    }

    private static void StartRunner(bool exitPlayMode)
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                CaptivityWildlifeLifecyclePlayModeRunner>() != null)
        {
            return;
        }

        CaptivityWildlifeLifecyclePlayModeRunner runner =
            new GameObject(nameof(CaptivityWildlifeLifecyclePlayModeRunner))
                .AddComponent<CaptivityWildlifeLifecyclePlayModeRunner>();
        runner.Configure(exitPlayMode);
    }

    internal static void ArmTeardownConsoleCapture()
    {
        if (teardownConsoleCaptureArmed)
        {
            return;
        }
        teardownConsoleIssues.Clear();
        teardownConsoleCaptureArmed = true;
        Application.logMessageReceived += CaptureTeardownConsoleIssue;
        EditorApplication.playModeStateChanged += CompleteTeardownConsoleCapture;
    }

    private static void CaptureTeardownConsoleIssue(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is LogType.Warning
            or LogType.Error
            or LogType.Exception
            or LogType.Assert)
        {
            teardownConsoleIssues.Add($"{type}:{condition}");
        }
    }

    private static void CompleteTeardownConsoleCapture(
        PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }
        Application.logMessageReceived -= CaptureTeardownConsoleIssue;
        EditorApplication.playModeStateChanged -= CompleteTeardownConsoleCapture;
        teardownConsoleCaptureArmed = false;
        if (teardownConsoleIssues.Count == 0 || !File.Exists(ReportPath))
        {
            teardownConsoleIssues.Clear();
            return;
        }

        List<string> report = File.ReadAllLines(ReportPath).ToList();
        if (report.Any(line => line.StartsWith(
                "FAIL\tTEARDOWN_CONSOLE_WARNING_ERROR_ZERO",
                StringComparison.Ordinal)))
        {
            teardownConsoleIssues.Clear();
            return;
        }
        int existingFailures = 0;
        if (report.Count > 1)
        {
            string marker = "failures=";
            int markerIndex = report[1].IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                int.TryParse(report[1].Substring(markerIndex + marker.Length), out existingFailures);
            }
            report[1] = $"RESULT=FAIL; failures={existingFailures + 1}";
        }
        string detail = string.Join(" | ", teardownConsoleIssues);
        report.Add($"FAIL\tTEARDOWN_CONSOLE_WARNING_ERROR_ZERO\t{detail}");
        report.Add($"FAILURE\tTEARDOWN_CONSOLE_WARNING_ERROR_ZERO: {detail}");
        File.WriteAllLines(ReportPath, report);
        teardownConsoleIssues.Clear();
    }
}

public sealed class CaptivityWildlifeLifecyclePlayModeRunner : MonoBehaviour
{
    private readonly List<string> rows = new();
    private readonly List<string> failures = new();
    private readonly List<string> consoleIssues = new();
    private readonly List<BuildableObject> fixtureBuildings = new();
    private readonly List<FixtureBuildingSnapshot> fixtureBuildingSnapshots =
        new();
    private readonly List<AreaSnapshot> areaSnapshots = new();
    private readonly List<DisplacedMovementSnapshot> displacedMovementBuildings =
        new();
    private readonly List<DisplacedWildlifeSnapshot> displacedWildlife = new();
    private readonly List<MonoBehaviourState> pausedAi = new();
    private readonly List<IGridOccupant> faultWalls = new();

    private bool exitPlayMode;
    private float oldTimeScale;
    private DungeonRuntimeLifetimeScope scope;
    private ICharacterAiWorldRegistry world;
    private IDungeonSaveSectionRegistry saveRegistry;
    private DungeonRuntimeAggregateRootStore aggregateStore;
    private Grid grid;
    private IRoomLayoutCache rooms;
    private IGridPathSearchBroker pathSearchBroker;
    private IGameEventBus gameEvents;
    private ICaptivityRuntime captivity;
    private ICaptivityCommandService captivityCommands;
    private ICaptivityEscapeRuntime escape;
    private ICaptivityPersistence captivityPersistence;
    private ICircusPersistence circusPersistence;
    private IWildlifeCaptureRuntime capture;
    private IWorldItemStackRuntime itemRuntime;
    private IPhysicalItemBatchDispositionService batchDispositions;
    private IResourceEconomyContentCatalog resourceCatalog;
    private WildlifeRuntime wildlife;
    private IWildlifeSpeciesCatalogProvider wildlifeSpecies;
    private IAnimalHusbandryPersistence husbandryPersistence;
    private IAnimalHusbandryQuery husbandryQuery;
    private IWorkPolicyRegistry workPolicyRegistry;
    private IFacilityCandidateCache facilityCandidateCache;
    private IEmergencyWorkAccountingService emergencyAccounting;
    private ISettlementAlertService settlementAlerts;
    private ISettlementAlertPersistence settlementAlertPersistence;
    private CharacterAlarmResponseRuntime alarmResponses;
    private IExperiencePacingRuntime experiencePacing;
    private InvasionThreatRuntime invasionThreat;
    private InvasionDirectorRuntime invasionDirector;
    private IInvasionSaveService invasionSaveService;
    private IDisposable invasionStartedProbeSubscription;
    private IDisposable invasionResolvedProbeSubscription;
    private IDisposable invasionCandidateProbeSubscription;
    private IDisposable activeIncidentsProbeSubscription;
    private int invasionStartedEventCount;
    private int invasionResolvedEventCount;
    private int invasionCandidateEventCount;
    private int activeIncidentsChangedEventCount;
    private readonly List<string> invasionEventTrace = new();
    private ICharacterProficiencyQuery proficiencies;
    private ICharacterBodyHealthQuery bodyHealthQuery;
    private ICharacterBodyHealthCommand bodyHealthCommands;
    private ICharacterMedicalQuery medicalQuery;
    private ICharacterMedicalCommand medicalCommands;
    private ICharacterDeprivationRuntime deprivationRuntime;
    private IGameCalendar calendar;
    private CharacterActor worker;
    private CharacterActor captiveActor;
    private AbilityWork workerWork;
    private WorkPriorityLevel oldAnimalCarePriority;
    private CharacterType captiveOldType;
    private CharacterLifecycleState captiveOldLifecycle;
    private bool captiveOldPaused;
    private List<DungeonSaveSectionEnvelope> baseline;
    private CaptivitySaveData confinedPayload;
    private BuildableObject housing;
    private BuildableObject pen;
    private BuildableObject faultPen;
    private RoomInstance fixtureRoom;
    private Vector2Int workerCell;
    private Vector2Int captiveCell;
    private Vector2Int escapeConnectorCell;
    private Vector2Int escapeExteriorAnchorCell;
    private string fixtureStage = string.Empty;
    private string placementFailure = string.Empty;

    public void Configure(bool shouldExitPlayMode) =>
        exitPlayMode = shouldExitPlayMode;

    private IEnumerator Start()
    {
        oldTimeScale = Time.timeScale;
        Time.timeScale = 8f;
        Application.logMessageReceived += CaptureConsoleIssue;
        try
        {
            yield return ResolveAndBuildFixture();
            if (failures.Count == 0) yield return EstablishConfinedCaptive();
            if (failures.Count == 0) yield return VerifyEscapeRows();
            if (failures.Count == 0) yield return VerifyTransportRows();
            if (failures.Count == 0) yield return VerifyAnimalCareRow();
        }
        finally
        {
            Cleanup();
            Application.logMessageReceived -= CaptureConsoleIssue;
            Check(consoleIssues.Count == 0,
                "CONSOLE_WARNING_ERROR_ZERO",
                consoleIssues.Count == 0
                    ? "0/0"
                    : string.Join(" | ", consoleIssues));
            Time.timeScale = oldTimeScale;
            WriteReport();
            if (exitPlayMode)
            {
                CaptivityWildlifeLifecyclePlayModeVerifier
                    .ArmTeardownConsoleCapture();
            }
            Destroy(gameObject);
            if (exitPlayMode)
            {
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlaying)
                        EditorApplication.isPlaying = false;
                };
            }
        }
    }

    private IEnumerator ResolveAndBuildFixture()
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            if (scope?.Container != null) break;
            yield return null;
        }
        Check(scope?.Container != null, "LIVE_SCOPE", scope?.name ?? "missing");
        if (scope?.Container == null) yield break;

        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        proficiencies = scope.Container.Resolve<ICharacterProficiencyQuery>();
        bodyHealthQuery = scope.Container.Resolve<ICharacterBodyHealthQuery>();
        bodyHealthCommands = scope.Container.Resolve<ICharacterBodyHealthCommand>();
        medicalQuery = scope.Container.Resolve<ICharacterMedicalQuery>();
        medicalCommands = scope.Container.Resolve<ICharacterMedicalCommand>();
        deprivationRuntime = scope.Container.Resolve<ICharacterDeprivationRuntime>();
        calendar = scope.Container.Resolve<IGameCalendar>();
        if (world.Characters.Count(actor =>
                actor != null
                && !actor.IsDead
                && HasCanonicalProficiencyProfile(actor)) < 2)
        {
            rows.Add("INFO\tSTART_PARTY\t"
                + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            for (int frame = 0; frame < 8; frame++) yield return null;
        }
        // Start-party setup normalizes the global simulation speed. Restore
        // this verifier's bounded acceleration after that authority has
        // finished so four alert hours do not become thirty seconds of
        // unrelated world simulation before the transport row.
        Time.timeScale = 8f;
        Check(Mathf.Approximately(Time.timeScale, 8f),
            "FIXTURE_SIMULATION_SPEED",
            $"timeScale={Time.timeScale:0.###}");

        CharacterActor[] actors = world.Characters
            .Where(actor => actor != null && !actor.IsDead)
            .Where(HasCanonicalProficiencyProfile)
            .ToArray();
        worker = actors.FirstOrDefault(actor =>
            actor.characterType == CharacterType.NPC);
        captiveActor = actors.FirstOrDefault(actor =>
            actor != worker && actor.characterType == CharacterType.NPC);
        Check(worker != null && captiveActor != null,
            "LIVE_ACTORS", $"actors={actors.Length};"
            + $"worker={DescribeActor(worker)};captive={DescribeActor(captiveActor)}");
        if (worker == null || captiveActor == null) yield break;

        saveRegistry = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        aggregateStore = scope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
        captivityPersistence = scope.Container.Resolve<ICaptivityPersistence>();
        captivity = scope.Container.Resolve<ICaptivityRuntime>();
        captivityCommands = scope.Container.Resolve<ICaptivityCommandService>();
        escape = scope.Container.Resolve<ICaptivityEscapeRuntime>();
        circusPersistence = scope.Container.Resolve<ICircusPersistence>();
        capture = scope.Container.Resolve<IWildlifeCaptureRuntime>();
        itemRuntime = scope.Container.Resolve<IWorldItemStackRuntime>();
        batchDispositions =
            scope.Container.Resolve<IPhysicalItemBatchDispositionService>();
        resourceCatalog =
            scope.Container.Resolve<IResourceEconomyContentCatalog>();
        wildlife = scope.Container.Resolve<WildlifeRuntime>();
        wildlifeSpecies = scope.Container.Resolve<IWildlifeSpeciesCatalogProvider>();
        husbandryPersistence = scope.Container.Resolve<IAnimalHusbandryPersistence>();
        husbandryQuery = scope.Container.Resolve<IAnimalHusbandryQuery>();
        workPolicyRegistry = scope.Container.Resolve<IWorkPolicyRegistry>();
        facilityCandidateCache = scope.Container.Resolve<IFacilityCandidateCache>();
        emergencyAccounting = scope.Container.Resolve<IEmergencyWorkAccountingService>();
        settlementAlerts = scope.Container.Resolve<ISettlementAlertService>();
        settlementAlertPersistence =
            scope.Container.Resolve<ISettlementAlertPersistence>();
        alarmResponses = scope.Container.Resolve<CharacterAlarmResponseRuntime>();
        experiencePacing = scope.Container.Resolve<IExperiencePacingRuntime>();
        rooms = scope.Container.Resolve<IRoomLayoutCache>();
        pathSearchBroker = scope.Container.Resolve<IGridPathSearchBroker>();
        gameEvents = scope.Container.Resolve<IGameEventBus>();
        invasionThreat = FindFirstObjectByType<InvasionThreatRuntime>(
            FindObjectsInactive.Include);
        invasionDirector = FindFirstObjectByType<InvasionDirectorRuntime>(
            FindObjectsInactive.Include);
        invasionSaveService = scope.Container.Resolve<IInvasionSaveService>();
        BeginInvasionEventDiagnostics();
        bool isolatedInvasionFixture = experiencePacing != null
            && !experiencePacing.AllowsRandomInvasion
            && invasionDirector != null
            && invasionDirector.ActiveIntruders.Count == 0;
        Check(isolatedInvasionFixture,
            "ESCAPE_INVASION_FIXTURE_ISOLATED",
            $"day={experiencePacing?.CurrentDay};"
            + $"randomAllowed={experiencePacing?.AllowsRandomInvasion};"
            + $"activeIntruders={invasionDirector?.ActiveIntruders.Count};"
            + $"threat={invasionThreat?.CurrentThreat:0.###};"
            + $"safety={invasionThreat?.SafetyRemaining:0.###};"
            + $"candidatePending={invasionThreat?.IsCandidatePending}");
        Check(world.TryGetGrid(out grid) && grid != null,
            "LIVE_GRID", grid != null ? $"{grid.width}x{grid.height}" : "missing");
        if (grid == null) yield break;

        baseline = saveRegistry.CaptureAll();
        PauseOtherAi();
        worker.TryGetAbility(out workerWork);
        oldAnimalCarePriority = workerWork != null
            ? workerWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.AnimalCare)
            : WorkPriorityLevel.Off;
        captiveOldType = captiveActor.characterType;
        captiveOldLifecycle = captiveActor.CurrentLifecycleState;
        captiveOldPaused = captiveActor.IsAiPaused();

        Check(CreateAuthoredRoom(), "AUTHORED_ROOM",
            $"housing={housing != null};pen={pen != null};faultPen={faultPen != null};room={fixtureRoom?.Id};stage={fixtureStage}");
    }

    private IEnumerator EstablishConfinedCaptive()
    {
        worker.transform.position = grid.GetWorldPos(workerCell);
        captiveActor.transform.position = grid.GetWorldPos(captiveCell);
        worker.SetLifecycleState(CharacterLifecycleState.Active);
        captiveActor.characterType = CharacterType.Intruder;
        captiveActor.SetLifecycleState(CharacterLifecycleState.Downed);
        captiveActor.SetAiPaused(true);

        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(worker);
        IDungeonItemCatalogProvider catalog =
            scope.Container.Resolve<IDungeonItemCatalogProvider>();
        IItemHaulingSettingsProvider hauling =
            scope.Container.Resolve<IItemHaulingSettingsProvider>();
        bool supplied = inventory.TryAdd(
            "qa:captivity-wildlife:restraint",
            CaptivityItemDefinitions.RestraintsItemId,
            1,
            catalog,
            hauling,
            out string reason);
        bool ordered = supplied
            && captivityCommands.TryOrderCapture(captiveActor, worker, out reason);
        Check(ordered, "CAPTIVE_CAPTURE_STARTED", reason);
        if (!ordered) yield break;

        string captiveId = CharacterPersistentIdentity.Require(captiveActor).Value;
        float deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (captivity.TryGetCaptive(captiveId, out CaptiveState state)
                && state?.status == CaptivityStatus.Confined)
            {
                break;
            }
            yield return null;
        }
        bool confined = captivity.TryGetCaptive(captiveId, out CaptiveState captured)
            && captured?.status == CaptivityStatus.Confined;
        Check(confined, "CAPTIVE_CAPTURE_TERMINAL",
            $"status={captured?.status};result={captured?.lastResult}");
        if (!confined) yield break;
        confinedPayload = Clone(captivityPersistence.Capture());
    }

    private IEnumerator VerifyEscapeRows()
    {
        string captiveId = CharacterPersistentIdentity.Require(captiveActor).Value;

        // Capture deliberately leaves a physically incapacitated captive in
        // Downed.  A real invasion must not turn that state into a free heal or
        // an escape.  Keep this as a distinct pre-start gate from the later
        // running-action lifecycle interruption row.
        Check(RestoreEscapeReadyCaptivity(captiveId),
            "ESCAPE_DOWNED_PRESTART_RESTORE", captiveId);
        captivity.TryGetCaptive(captiveId, out CaptiveState state);
        EscapeStartProbe probe = new();
        int downedAttemptsBefore = state?.failedEscapeAttempts ?? -1;
        yield return TryBeginEscapeThroughInvasion(
            state,
            "qa-downed-prestart",
            probe);
        bool started = probe.Started;
        string reason = probe.Reason;
        captivity.TryGetCaptive(captiveId, out CaptiveState downedPrestart);
        AbilityCaptiveEscape ability = captiveActor.GetComponent<AbilityCaptiveEscape>();
        Check(!started
              && captiveActor.CurrentLifecycleState == CharacterLifecycleState.Downed
              && downedPrestart?.status == CaptivityStatus.Confined
              && downedPrestart.failedEscapeAttempts == downedAttemptsBefore
              && ability?.IsEscaping != true,
            "ESCAPE_DOWNED_PRESTART_DENIED",
            $"started={started};lifecycle={captiveActor.CurrentLifecycleState};"
            + $"status={downedPrestart?.status};attempts={downedPrestart?.failedEscapeAttempts};"
            + $"active={ability?.IsEscaping};reason={reason}");

        Check(RestoreEscapeReadyCaptivity(captiveId), "ESCAPE_NOPATH_RESTORE", captiveId);
        Check(RecoverCaptiveThroughMedicalAuthority(out string recoveryDetail),
            "ESCAPE_NOPATH_MEDICAL_RECOVERY", recoveryDetail);
        EscapeRouteProbe routeProbe = new();
        yield return WaitForProductionEscapeRoute(routeProbe);
        Check(routeProbe.Ready,
            "ESCAPE_NOPATH_ROUTE_PREFLIGHT", routeProbe.Detail);
        captivity.TryGetCaptive(captiveId, out state);
        probe = new EscapeStartProbe();
        AbilityMove escapeMove = captiveActor.GetComponent<AbilityMove>();
        IGridPathSearchBroker originalMoveBroker = escapeMove?
            .DebugReplacePathSearchBroker(new NoPathSearchBroker());
        yield return TryBeginEscapeThroughInvasion(state, "qa-no-path", probe);
        started = probe.Started;
        reason = probe.Reason;
        if (escapeMove != null && originalMoveBroker != null)
            escapeMove.DebugReplacePathSearchBroker(originalMoveBroker);
        ability = captiveActor.GetComponent<AbilityCaptiveEscape>();
        yield return WaitUntil(() =>
            captivity.TryGetCaptive(captiveId, out CaptiveState current)
            && current?.status == CaptivityStatus.Confined, 12f);
        captivity.TryGetCaptive(captiveId, out CaptiveState noPath);
        Check(started && noPath?.status == CaptivityStatus.Confined
              && noPath.failedEscapeAttempts == 1
              && ability?.IsEscaping != true,
            "ESCAPE_NOPATH_TERMINAL",
            $"started={started};status={noPath?.status};attempts={noPath?.failedEscapeAttempts}");

        Check(RestoreEscapeReadyCaptivity(captiveId),
            "ESCAPE_RUNNING_DOWNED_RESTORE", captiveId);
        Check(RecoverCaptiveThroughMedicalAuthority(out recoveryDetail),
            "ESCAPE_RUNNING_DOWNED_MEDICAL_RECOVERY", recoveryDetail);
        routeProbe = new EscapeRouteProbe();
        yield return WaitForProductionEscapeRoute(routeProbe);
        Check(routeProbe.Ready,
            "ESCAPE_RUNNING_DOWNED_ROUTE_PREFLIGHT", routeProbe.Detail);
        captivity.TryGetCaptive(captiveId, out state);
        int runningDownedAttemptsBefore = state?.failedEscapeAttempts ?? -1;
        probe = new EscapeStartProbe();
        yield return TryBeginEscapeThroughInvasion(state, "qa-running-downed", probe);
        started = probe.Started;
        reason = probe.Reason;
        ability = captiveActor.GetComponent<AbilityCaptiveEscape>();
        if (started) captiveActor.SetLifecycleState(CharacterLifecycleState.Downed);
        yield return null;
        yield return null;
        captivity.TryGetCaptive(captiveId, out CaptiveState downed);
        AbilityMove downedMove = captiveActor.GetComponent<AbilityMove>();
        CharacterAiRuntimeGateSnapshot downedGate =
            captiveActor.Brain.CaptureRuntimeGateSnapshot();
        int attemptsAfterFirstCleanup = downed?.failedEscapeAttempts ?? -1;
        captiveActor.SetLifecycleState(CharacterLifecycleState.Downed);
        yield return null;
        captivity.TryGetCaptive(captiveId, out CaptiveState downedAfterDuplicateCleanup);
        Check(started && downed?.status == CaptivityStatus.Confined
            && downed.failedEscapeAttempts == runningDownedAttemptsBefore + 1
            && downedAfterDuplicateCleanup?.failedEscapeAttempts
                == attemptsAfterFirstCleanup
            && ability?.IsEscaping != true
            && ability?.HasEscapePassForDiagnostics != true
            && downedMove?.HasActiveMovementRoutineForDiagnostics != true
            && downedGate.LivePathRequests == 0,
            "ESCAPE_RUNNING_DOWNED_TERMINAL",
            $"status={downed?.status};active={ability?.IsEscaping};"
            + $"pass={ability?.HasEscapePassForDiagnostics};"
            + $"attempts={runningDownedAttemptsBefore}->{attemptsAfterFirstCleanup}"
            + $"->{downedAfterDuplicateCleanup?.failedEscapeAttempts};"
            + $"movement={downedMove?.HasActiveMovementRoutineForDiagnostics};"
            + $"paths={downedGate.LivePathRequests}");

        Check(RestoreEscapeReadyCaptivity(captiveId), "ESCAPE_DISABLE_RESTORE", captiveId);
        Check(RecoverCaptiveThroughMedicalAuthority(out recoveryDetail),
            "ESCAPE_DISABLE_MEDICAL_RECOVERY", recoveryDetail);
        routeProbe = new EscapeRouteProbe();
        yield return WaitForProductionEscapeRoute(routeProbe);
        Check(routeProbe.Ready,
            "ESCAPE_DISABLE_ROUTE_PREFLIGHT", routeProbe.Detail);
        captivity.TryGetCaptive(captiveId, out state);
        probe = new EscapeStartProbe();
        yield return TryBeginEscapeThroughInvasion(state, "qa-disable", probe);
        started = probe.Started;
        reason = probe.Reason;
        ability = captiveActor.GetComponent<AbilityCaptiveEscape>();
        if (started && ability != null) ability.enabled = false;
        yield return null;
        captivity.TryGetCaptive(captiveId, out CaptiveState disabled);
        Check(started && disabled?.status == CaptivityStatus.Confined
              && ability?.IsEscaping != true,
            "ESCAPE_DISABLE_TERMINAL",
            $"status={disabled?.status};active={ability?.IsEscaping}");
        if (ability != null) ability.enabled = true;

        // Success is last because the production terminal owns an exterior
        // departure.  Preparing it through medical authority avoids reviving a
        // Downed captive with a test-only lifecycle setter.
        Check(RestoreEscapeReadyCaptivity(captiveId),
            "ESCAPE_SUCCESS_RESTORE", captiveId);
        Check(RecoverCaptiveThroughMedicalAuthority(out recoveryDetail),
            "ESCAPE_SUCCESS_MEDICAL_RECOVERY", recoveryDetail);
        routeProbe = new EscapeRouteProbe();
        yield return WaitForProductionEscapeRoute(routeProbe);
        Check(routeProbe.Ready,
            "ESCAPE_SUCCESS_ROUTE_PREFLIGHT", routeProbe.Detail);
        captivity.TryGetCaptive(captiveId, out state);
        probe = new EscapeStartProbe();
        yield return TryBeginEscapeThroughInvasion(state, "qa-success", probe);
        started = probe.Started;
        reason = probe.Reason;
        Check(started, "ESCAPE_SUCCESS_STARTED", reason);
        yield return WaitUntil(() =>
            captivity.TryGetCaptive(captiveId, out CaptiveState current)
            && current?.status == CaptivityStatus.Escaped, 30f);
        AbilityMove successExitMove =
            captiveActor.GetComponent<AbilityMove>();
        yield return WaitUntil(() =>
        {
            bool escapedState = captivity.TryGetCaptive(
                    captiveId,
                    out CaptiveState current)
                && current?.status == CaptivityStatus.Escaped;
            bool movementTerminal = successExitMove == null
                || (!successExitMove.IsSystemMoveInProgress
                    && !successExitMove
                        .HasActiveMovementRoutineForDiagnostics);
            bool lifecycleHandoffTerminal = captiveActor == null
                || !captiveActor.gameObject.activeInHierarchy
                || captiveActor.CurrentLifecycleState
                    != CharacterLifecycleState.ExitingDungeon;
            return escapedState
                && movementTerminal
                && lifecycleHandoffTerminal
                && captiveActor?.Brain?.IsExternallyDrivenActionActive
                    != true;
        }, 30f);
        captivity.TryGetCaptive(captiveId, out CaptiveState escaped);
        ability = captiveActor.GetComponent<AbilityCaptiveEscape>();
        CharacterAiRuntimeGateSnapshot exitGate =
            captiveActor.Brain.CaptureRuntimeGateSnapshot();
        bool exitHandoffTerminal =
            successExitMove?.IsSystemMoveInProgress != true
            && successExitMove?.HasActiveMovementRoutineForDiagnostics
                != true
            && captiveActor.CurrentLifecycleState
                != CharacterLifecycleState.ExitingDungeon
            && captiveActor.Brain.IsExternallyDrivenActionActive != true
            && exitGate.LivePathRequests == 0;
        Check(escaped?.status == CaptivityStatus.Escaped
              && ability?.IsEscaping != true,
            "ESCAPE_SUCCESS_TERMINAL",
            $"status={escaped?.status};active={ability?.IsEscaping}");
        Check(exitHandoffTerminal,
            "ESCAPE_SUCCESS_EXIT_HANDOFF_TERMINAL",
            $"lifecycle={captiveActor.CurrentLifecycleState};"
            + $"active={captiveActor.gameObject.activeInHierarchy};"
            + $"systemMove={successExitMove?.IsSystemMoveInProgress};"
            + "moveRoutine="
            + $"{successExitMove?.HasActiveMovementRoutineForDiagnostics};"
            + $"external={captiveActor.Brain.IsExternallyDrivenActionActive};"
            + $"paths={exitGate.LivePathRequests}");

        yield return ResolveEscapeInvasionAndWaitForGreen();
    }

    private IEnumerator ResolveEscapeInvasionAndWaitForGreen()
    {
        int activeIntrudersBeforeRestore =
            invasionDirector?.ActiveIntruders.Count ?? -1;
        bool invasionRestored = RestoreBaselineSection(
            InvasionSaveSection.Id,
            invasionSaveService as IDungeonRestoreTransactionParticipant,
            out string invasionRestoreDetail);
        bool invasionAuthorityClean = invasionRestored
            && invasionDirector != null
            && invasionDirector.ActiveIntruders.Count == 0;
        Check(invasionAuthorityClean,
            "ESCAPE_RETALIATION_INVASION_RESTORED",
            $"activeIntruders={activeIntrudersBeforeRestore}"
            + $"->{invasionDirector?.ActiveIntruders.Count};"
            + $"candidateEvents={invasionCandidateEventCount};"
            + $"startedEvents={invasionStartedEventCount};"
            + $"detail={invasionRestoreDetail}");
        if (!invasionAuthorityClean)
        {
            yield break;
        }

        int startedBefore = invasionStartedEventCount;
        int resolvedBefore = invasionResolvedEventCount;
        int candidatesBefore = invasionCandidateEventCount;
        int incidentChangesBefore = activeIncidentsChangedEventCount;
        long revisionBefore = settlementAlerts.GetNextIncidentRevision(
            "incident:invasion:active");
        string incidentBefore = DescribeInvasionIncident();
        gameEvents.Publish(new InvasionResolvedEvent(true, 0f));
        SettlementAlertSnapshot immediate = settlementAlerts.Capture();
        long revisionAfter = settlementAlerts.GetNextIncidentRevision(
            "incident:invasion:active");
        string incidentAfter = DescribeInvasionIncident();
        bool immediateResolution = invasionResolvedEventCount
                == resolvedBefore + 1
            && invasionStartedEventCount == startedBefore
            && invasionCandidateEventCount == candidatesBefore
            && activeIncidentsChangedEventCount > incidentChangesBefore
            && !immediate.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal)
            && immediate.DesiredLevel == SettlementThreatAlertLevel.Green;
        Check(immediateResolution,
            "ESCAPE_INVASION_RESOLVE_COMMITTED",
            $"events=start:{startedBefore}->{invasionStartedEventCount},"
            + $"resolved:{resolvedBefore}->{invasionResolvedEventCount},"
            + $"candidate:{candidatesBefore}->{invasionCandidateEventCount},"
            + $"incidentChanged:{incidentChangesBefore}"
            + $"->{activeIncidentsChangedEventCount};"
            + $"revision={revisionBefore}->{revisionAfter};"
            + $"before={incidentBefore};after={incidentAfter};"
            + $"desired={immediate.DesiredLevel};"
            + $"committed={immediate.CommittedLevel};"
            + $"trace=[{string.Join(" | ", invasionEventTrace.TakeLast(12))}]");
        if (!immediateResolution)
        {
            yield break;
        }

        long startedHour = calendar.AbsoluteHour;
        float realtimeDeadline = Time.realtimeSinceStartup + 45f;
        SettlementAlertSnapshot snapshot = settlementAlerts.Capture();
        while (Time.realtimeSinceStartup < realtimeDeadline
               && calendar.AbsoluteHour - startedHour <= 6L)
        {
            snapshot = settlementAlerts.Capture();
            bool invasionResolved = !snapshot.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal);
            bool green = snapshot.DesiredLevel
                    == SettlementThreatAlertLevel.Green
                && snapshot.CommittedLevel
                    == SettlementThreatAlertLevel.Green;
            bool responseReleased = workerWork?.HasEmergencyResponseWorkGateForDiagnostics
                    != true
                && alarmResponses.PendingResponderCountForDiagnostics == 0
                && alarmResponses.ReturningResponderCountForDiagnostics == 0
                && alarmResponses.AssignedResponderCountForDiagnostics == 0;
            if (invasionResolved && green && responseReleased)
            {
                break;
            }
            yield return null;
        }

        snapshot = settlementAlerts.Capture();
        bool clean = !snapshot.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal)
            && snapshot.DesiredLevel == SettlementThreatAlertLevel.Green
            && snapshot.CommittedLevel == SettlementThreatAlertLevel.Green
            && workerWork?.HasEmergencyResponseWorkGateForDiagnostics != true
            && alarmResponses.PendingResponderCountForDiagnostics == 0
            && alarmResponses.ReturningResponderCountForDiagnostics == 0
            && alarmResponses.AssignedResponderCountForDiagnostics == 0
            && invasionStartedEventCount == startedBefore
            && invasionResolvedEventCount == resolvedBefore + 1
            && invasionCandidateEventCount == candidatesBefore;
        Check(clean,
            "ESCAPE_INVASION_RESPONSE_RELEASED",
            $"hours={calendar.AbsoluteHour - startedHour};"
            + $"desired={snapshot.DesiredLevel};committed={snapshot.CommittedLevel};"
            + $"incidents=[{string.Join(",", snapshot.ActiveIncidentIds)}];"
            + $"gate={workerWork?.HasEmergencyResponseWorkGateForDiagnostics}:"
            + $"{workerWork?.EmergencyResponseWorkEpochForDiagnostics}:"
            + $"{workerWork?.EmergencyResponseOnlyWorkTypeForDiagnostics};"
            + $"responses={alarmResponses.PendingResponderCountForDiagnostics}/"
            + $"{alarmResponses.ReturningResponderCountForDiagnostics}/"
            + $"{alarmResponses.AssignedResponderCountForDiagnostics};"
            + $"events=start:{startedBefore}->{invasionStartedEventCount},"
            + $"resolved:{resolvedBefore}->{invasionResolvedEventCount},"
            + $"candidate:{candidatesBefore}->{invasionCandidateEventCount};"
            + $"timeScale={Time.timeScale:0.###};"
            + $"incident={DescribeInvasionIncident()};"
            + $"trace=[{string.Join(" | ", invasionEventTrace.TakeLast(12))}]");
    }

    private bool RecoverCaptiveThroughMedicalAuthority(out string detail)
    {
        detail = string.Empty;
        if (captiveActor == null
            || bodyHealthQuery == null
            || bodyHealthCommands == null
            || medicalCommands == null)
        {
            detail = "medical recovery authority missing";
            return false;
        }

        CharacterBodyHealthSnapshot before = bodyHealthQuery.GetSnapshot(captiveActor);
        CharacterVitalsSnapshot vitalsBefore = bodyHealthQuery.GetVitals(captiveActor);

        // Heal is the production anatomy/vitals mutation boundary.  The
        // explicit medical recovery notification is the production lifecycle
        // completion boundary and verifies the health query before publishing
        // Active; neither operation writes CharacterLifecycle directly.
        bodyHealthCommands.Heal(captiveActor, 10000f, stopBleeding: true);
        medicalCommands.NotifyCharacterRecovered(captiveActor);

        CharacterBodyHealthSnapshot after = bodyHealthQuery.GetSnapshot(captiveActor);
        CharacterVitalsSnapshot vitalsAfter = bodyHealthQuery.GetVitals(captiveActor);
        bool recovered = !after.Downed
            && captiveActor.CurrentLifecycleState == CharacterLifecycleState.Active;
        detail = $"beforeDowned={before.Downed};beforeHealth="
            + $"{vitalsBefore.CurrentHealth:0.##}/{vitalsBefore.MaximumHealth:0.##};"
            + $"afterDowned={after.Downed};afterHealth="
            + $"{vitalsAfter.CurrentHealth:0.##}/{vitalsAfter.MaximumHealth:0.##};"
            + $"lifecycle={captiveActor.CurrentLifecycleState}";
        return recovered;
    }

    private IEnumerator WaitForProductionEscapeRoute(EscapeRouteProbe probe)
    {
        if (probe == null
            || captiveActor == null
            || grid == null
            || pathSearchBroker == null)
        {
            if (probe != null) probe.Detail = "escape route authority missing";
            yield break;
        }

        Vector2Int start = captiveActor.GetNowXY();
        GridTraversalContext context = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(captiveActor),
            DoorAccessOverrideKind.CaptiveEscape,
            GridMovementIntent.EscapeHazard);
        float deadline = Time.realtimeSinceStartup + 5f;
        int deferred = 0;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (!pathSearchBroker.TryGetSearch(
                    grid,
                    start,
                    out GridPathSearchResult search,
                    GridPathSearchPriority.Urgent,
                    context))
            {
                deferred++;
                yield return null;
                continue;
            }

            Vector2Int[] exits = grid.GetCells()
                .Where(cell => cell != null
                    && cell.AreaType == GridCellAreaType.ExteriorPath
                    && cell.IsWalkableArea
                    && grid.IsWalkable(cell.Position)
                    && search.ContainsPosition(cell.Position))
                .Select(cell => cell.Position)
                .ToArray();
            probe.Ready = exits.Length > 0;
            string nearest = exits.Length > 0
                ? exits.OrderBy(position =>
                        Mathf.Abs(position.x - start.x)
                        + Mathf.Abs(position.y - start.y))
                    .First()
                    .ToString()
                : "none";
            probe.Detail = $"start={start};deferred={deferred};"
                + $"reachableExits={exits.Length};"
                + $"nearest={nearest};"
                + $"connector={escapeConnectorCell}:"
                + $"{search.ContainsPosition(escapeConnectorCell)}:"
                + $"{DescribeCell(escapeConnectorCell)};"
                + $"anchor={escapeExteriorAnchorCell}:"
                + $"{search.ContainsPosition(escapeExteriorAnchorCell)}:"
                + $"{DescribeCell(escapeExteriorAnchorCell)};"
                + $"lifecycle={captiveActor.CurrentLifecycleState};"
                + $"gridRevision={grid.TraversalVersion}";
            yield break;
        }

        probe.Detail = $"start={start};deferred={deferred};search-timeout;"
            + $"lifecycle={captiveActor.CurrentLifecycleState};"
            + $"gridRevision={grid.TraversalVersion}";
    }

    private IEnumerator VerifyTransportRows()
    {
        worker.SetLifecycleState(CharacterLifecycleState.Active);
        // Freeze autonomous arbitration before cancelling the previous action.
        // Planning the lawful source/pickup/delivery triple yields across path
        // broker frames. Keep the actor quiescent while warming the exact
        // production pickup and delivery searches, then let TryOrderCapture
        // acquire the external lease before any movement is allowed to start.
        worker.SetAiPaused(true);
        worker.transform.position = grid.GetWorldPos(workerCell);
        worker.Brain?.StopCurrentActionForReplan("qa-wildlife-transport");
        AbilityHaul transportHaul = worker.GetComponent<AbilityHaul>();
        AbilityMove preparationMove = worker.GetComponent<AbilityMove>();
        transportHaul?.StopHauling("qa-wildlife-transport");
        preparationMove?.CancelActiveMovement(
            "qa-wildlife-transport-preparation");
        int settledFrames = 0;
        float settleDeadline = Time.realtimeSinceStartup + 3f;
        while (settledFrames < 2
               && Time.realtimeSinceStartup < settleDeadline)
        {
            yield return null;
            bool settled = worker.Brain?.bestAction == null
                && worker.Brain?.IsExternallyDrivenActionActive != true
                && transportHaul?.IsHauling != true
                && workerWork?.isWorking != true
                && workerWork?.HasActiveWorkRoutineForDiagnostics != true
                && preparationMove?.HasActiveMovementRoutineForDiagnostics
                    != true;
            settledFrames = settled ? settledFrames + 1 : 0;
        }
        Check(settledFrames >= 2,
            "TRANSPORT_SUCCESS_ATOMIC_PREPARATION",
            $"settled={settledFrames}/2;paused={worker.IsAiPaused()};"
            + $"action={worker.Brain?.bestAction?.actionset?.GetType().Name};"
            + $"external={worker.Brain?.IsExternallyDrivenActionActive};"
            + $"haul={transportHaul?.IsHauling};"
            + $"work={workerWork?.isWorking}:"
            + $"{workerWork?.HasActiveWorkRoutineForDiagnostics};"
            + $"movement={preparationMove?.HasActiveMovementRoutineForDiagnostics}");
        if (settledFrames < 2)
        {
            yield break;
        }

        TransportFixturePlanProbe transportPlan = new();
        yield return PlanLawfulTransportFixture(transportPlan);
        Check(transportPlan.Ready,
            "TRANSPORT_SUCCESS_LAWFUL_SOURCE_PLAN",
            transportPlan.Detail);
        if (!transportPlan.Ready) yield break;
        WildlifeActor successAnimal = SpawnCaptureAnimal(
            transportPlan.SourceCell);
        Check(successAnimal != null
              && successAnimal.GridPosition == transportPlan.SourceCell,
            "TRANSPORT_SUCCESS_SOURCE",
            successAnimal != null
                ? $"{successAnimal.WildlifeId}:"
                    + $"planned={transportPlan.SourceCell}:"
                    + $"actual={successAnimal.GridPosition}"
                : "missing");
        if (successAnimal == null
            || successAnimal.GridPosition != transportPlan.SourceCell)
        {
            yield break;
        }
        Vector2Int successAnimalSourceCell = successAnimal.GridPosition;
        Transform successAnimalParentBeforeTransport =
            successAnimal.transform.parent;
        // Capture eligibility must be established before the incremental
        // path preflight yields. A live wildlife brain may otherwise leave the
        // authored source cell while the broker is still building its search,
        // invalidating both the warmed pickup path and the ownership handoff.
        DamageForCapture(successAnimal);
        PickupDeliveryPreflightProbe pickupProbe = new();
        yield return PlaceWorkerAtReachablePickupAndDeliveryStand(
            successAnimal,
            transportPlan,
            pickupProbe);
        bool pickupReady = pickupProbe.Ready;
        Check(pickupReady,
            "TRANSPORT_SUCCESS_PICKUP_TO_DELIVERY_PREFLIGHT",
            pickupProbe.Detail);
        if (!pickupReady) yield break;
        int transportTerminalBefore =
            worker.Brain?.ExternalIntentTerminalCount ?? 0;
        bool ordered = capture.TryOrderCapture(successAnimal, worker, pen, out string reason);
        Check(ordered, "TRANSPORT_SUCCESS_STARTED", reason);
        AbilityWildlifeCaptureTransport transport =
            worker.GetComponent<AbilityWildlifeCaptureTransport>();
        capture.TryGetCaptured(
            successAnimal.WildlifeId,
            out CapturedWildlifeState ownershipState);
        string expectedTransportOwner =
            $"captivity:wildlife-transport:{successAnimal.WildlifeId}";
        bool atomicPickupOwnership = ordered
            && worker.IsAiPaused()
            && worker.GetNowXY() == pickupProbe.ApproachStart
            && Mathf.Abs(worker.GetNowXY().x - successAnimal.GridPosition.x)
                + Mathf.Abs(worker.GetNowXY().y - successAnimal.GridPosition.y)
                > 1
            && worker.Brain?.bestAction == null
            && worker.Brain?.IsExternallyDrivenActionActive == true
            && worker.Brain?.ExternalIntentOwnerId == expectedTransportOwner
            && transport?.IsTransporting == true
            && ownershipState?.transportState
                == CapturedWildlifeTransportState.AwaitingTransport
            && ownershipState.reservedCarrierId
                == CharacterPersistentIdentity.Require(worker).Value;
        Check(atomicPickupOwnership,
            "TRANSPORT_SUCCESS_ATOMIC_PICKUP_OWNERSHIP",
            $"ordered={ordered};reason={reason};paused={worker.IsAiPaused()};"
            + $"carrier={worker.GetNowXY()};approach={pickupProbe.ApproachStart};"
            + $"pickup={pickupProbe.PickupStand};animal={successAnimal.GridPosition};"
            + $"action={worker.Brain?.bestAction?.actionset?.GetType().Name};"
            + $"external={worker.Brain?.IsExternallyDrivenActionActive};"
            + $"owner={worker.Brain?.ExternalIntentOwnerId};"
            + $"expectedOwner={expectedTransportOwner};"
            + $"transport={transport?.IsTransporting};"
            + $"state={ownershipState?.transportState};"
            + $"reservedCarrier={ownershipState?.reservedCarrierId};"
            + $"terminalFailure={transport?.LastTerminalFailureReasonForDiagnostics};"
            + $"movement={preparationMove?.HasActiveMovementRoutineForDiagnostics};"
            + $"movementOwner={preparationMove?.ActiveMovementOperationOwnerForDiagnostics};"
            + "preemption="
            + $"{preparationMove?.LastMovementOperationPreemptionForDiagnostics}");
        if (!atomicPickupOwnership)
        {
            yield break;
        }
        TransportTerminalObservation transportObservation = new();
        yield return ObserveTransportTerminal(
            successAnimal.WildlifeId,
            pickupProbe,
            transportObservation);
        capture.TryGetCaptured(successAnimal.WildlifeId, out CapturedWildlifeState penned);
        AbilityMove transportMove = worker.GetComponent<AbilityMove>();
        Vector2Int deliveryPosition = penned?.penPosition ?? default;
        GridCell deliveryCell = penned != null
            ? grid.GetGridCell(deliveryPosition)
            : null;
        IGridOccupant deliveryBuilding =
            deliveryCell?.GetOccupant(GridLayer.Building);
        IGridOccupant deliveryConstruction =
            deliveryCell?.GetOccupant(GridLayer.Construction);
        IGridOccupant deliveryConveyor =
            deliveryCell?.GetOccupant(GridLayer.Conveyor);
        IGridOccupant deliveryCharacter =
            deliveryCell?.GetOccupant(GridLayer.Character);
        IGridOccupant deliveryDownedCharacter =
            deliveryCell?.GetOccupant(GridLayer.DownedCharacter);
        IGridOccupant deliveryWildlife =
            deliveryCell?.GetOccupant(GridLayer.Wildlife);
        bool deliveryCellValid = deliveryCell != null
            && grid.IsWalkable(deliveryPosition)
            && !(pen.buildPoses ?? Array.Empty<Vector2Int>())
                .Contains(deliveryPosition);
        bool deliveryBlockingLayersClear = deliveryBuilding == null
            && deliveryConstruction == null
            && deliveryConveyor == null
            && deliveryCharacter == null
            && deliveryDownedCharacter == null;
        Vector2Int[] successAnimalGridRegistrations =
            FindGridOccupantRegistrations(
                grid,
                GridLayer.Wildlife,
                successAnimal);
        IGridOccupant[] successAnimalGridIndexMatches = grid
            .FindAllOccupants(value => ReferenceEquals(value, successAnimal))
            .ToArray();
        bool successAnimalGridIndexExact =
            successAnimalGridIndexMatches.Length == 1
            && ReferenceEquals(
                successAnimalGridIndexMatches[0],
                successAnimal);
        bool deliveryWildlifeExact = ReferenceEquals(
                deliveryWildlife,
                successAnimal)
            && successAnimalGridRegistrations.Length == 1
            && successAnimalGridRegistrations[0] == deliveryPosition
            && successAnimalGridIndexExact
            && successAnimal.GridPosition == deliveryPosition
            && successAnimal.State == WildlifeState.Captured
            && successAnimal.IsAlive;
        IGridOccupant sourceWildlife = grid.GetGridCell(successAnimalSourceCell)
            ?.GetOccupant(GridLayer.Wildlife);
        bool sourceResidueClear = successAnimalSourceCell == deliveryPosition
            || !ReferenceEquals(sourceWildlife, successAnimal);
        bool parentReleased = ReferenceEquals(
                successAnimal.transform.parent,
                successAnimalParentBeforeTransport)
            && !successAnimal.transform.IsChildOf(worker.transform);
        CapturedWildlifeState[] sameCapturedStates = capture.CapturedAnimals
            .Where(value => value != null
                && string.Equals(
                    value.wildlifeId,
                    successAnimal.WildlifeId,
                    StringComparison.Ordinal))
            .ToArray();
        bool capturedStateExact = penned != null
            && sameCapturedStates.Length == 1
            && string.Equals(
                penned.wildlifeId,
                successAnimal.WildlifeId,
                StringComparison.Ordinal)
            && string.Equals(
                sameCapturedStates[0].wildlifeId,
                successAnimal.WildlifeId,
                StringComparison.Ordinal)
            && sameCapturedStates[0].penPosition == deliveryPosition
            && sameCapturedStates[0].transportState
                == CapturedWildlifeTransportState.Penned
            && string.IsNullOrWhiteSpace(
                sameCapturedStates[0].reservedCarrierId)
            && penned.transportState == CapturedWildlifeTransportState.Penned
            && string.IsNullOrWhiteSpace(penned.reservedCarrierId);
        WildlifeActor[] sameWorldWildlife = world.Wildlife
            .Where(value => value != null
                && string.Equals(
                    value.WildlifeId,
                    successAnimal.WildlifeId,
                    StringComparison.Ordinal))
            .ToArray();
        bool worldWildlifeExact = sameWorldWildlife.Length == 1
            && ReferenceEquals(sameWorldWildlife[0], successAnimal);
        CharacterActor[] liveActorsAtDelivery = world.Characters
            .Where(value => value != null
                && value.gameObject.activeInHierarchy
                && !value.IsDead
                && value.GetNowXY() == deliveryPosition)
            .ToArray();
        int deliveryWorkerCount = liveActorsAtDelivery.Count(value =>
            ReferenceEquals(value, worker));
        int deliveryOtherActorCount = liveActorsAtDelivery.Length
            - deliveryWorkerCount;
        bool liveActorColocationExact = deliveryWorkerCount == 1
            && deliveryOtherActorCount == 0;
        bool deliveryStandValid = deliveryCellValid
            && deliveryBlockingLayersClear
            && deliveryWildlifeExact
            && sourceResidueClear
            && parentReleased
            && capturedStateExact
            && worldWildlifeExact
            && liveActorColocationExact;
        string deliveryLayerDetail = DescribeGridCellLayers(deliveryCell);
        string deliveryRegistrationDetail =
            successAnimalGridRegistrations.Length == 0
                ? "none"
                : string.Join(",", successAnimalGridRegistrations);
        string deliveryLiveActorDetail =
            liveActorsAtDelivery.Length == 0
                ? "none"
                : string.Join("|", liveActorsAtDelivery.Select(DescribeLiveActor));
        Check(
            deliveryCellValid,
            "TRANSPORT_SUCCESS_DELIVERY_CELL",
            $"exists={deliveryCell != null};position={deliveryPosition};"
            + $"area={deliveryCell?.AreaType};terrain={deliveryCell?.TerrainType};"
            + $"walkable={(deliveryCell != null && grid.IsWalkable(deliveryPosition))};"
            + "insidePenFootprint="
            + $"{(pen.buildPoses ?? Array.Empty<Vector2Int>()).Contains(deliveryPosition)};"
            + $"layers={deliveryLayerDetail}");
        Check(
            deliveryBlockingLayersClear,
            "TRANSPORT_SUCCESS_DELIVERY_BLOCKING_LAYERS",
            $"building={DescribeGridOccupant(deliveryBuilding)};"
            + $"construction={DescribeGridOccupant(deliveryConstruction)};"
            + $"conveyor={DescribeGridOccupant(deliveryConveyor)};"
            + $"character={DescribeGridOccupant(deliveryCharacter)};"
            + $"downed={DescribeGridOccupant(deliveryDownedCharacter)}");
        Check(
            deliveryWildlifeExact && worldWildlifeExact,
            "TRANSPORT_SUCCESS_DELIVERY_WILDLIFE_REGISTRATION",
            $"cell={DescribeGridOccupant(deliveryWildlife)};"
            + $"expected={DescribeGridOccupant(successAnimal)};"
            + $"registrations={successAnimalGridRegistrations.Length}:"
            + $"{deliveryRegistrationDetail};"
            + $"gridIndexMatches={successAnimalGridIndexMatches.Length};"
            + $"gridIndexExact={successAnimalGridIndexExact};"
            + $"gridPosition={successAnimal.GridPosition};"
            + $"state={successAnimal.State};alive={successAnimal.IsAlive};"
            + $"worldMatches={sameWorldWildlife.Length};"
            + $"worldExact={worldWildlifeExact}");
        Check(
            sourceResidueClear,
            "TRANSPORT_SUCCESS_SOURCE_RELEASE",
            $"source={successAnimalSourceCell};delivery={deliveryPosition};"
            + $"occupant={DescribeGridOccupant(sourceWildlife)};"
            + $"sameReference={ReferenceEquals(sourceWildlife, successAnimal)}");
        Check(
            parentReleased,
            "TRANSPORT_SUCCESS_PARENT_RELEASE",
            $"before={DescribeTransform(successAnimalParentBeforeTransport)};"
            + $"after={DescribeTransform(successAnimal.transform.parent)};"
            + $"carrier={DescribeTransform(worker.transform)};"
            + $"childOfCarrier={successAnimal.transform.IsChildOf(worker.transform)}");
        Check(
            capturedStateExact,
            "TRANSPORT_SUCCESS_CAPTURE_STATE",
            $"matches={sameCapturedStates.Length};state={penned?.transportState};"
            + $"reservedCarrier={penned?.reservedCarrierId};"
            + $"wildlifeId={penned?.wildlifeId};"
            + $"expectedId={successAnimal.WildlifeId}");
        Check(
            liveActorColocationExact,
            "TRANSPORT_SUCCESS_LIVE_ACTOR_COLOCATION",
            $"position={deliveryPosition};count={liveActorsAtDelivery.Length};"
            + $"workerCount={deliveryWorkerCount};"
            + $"otherCount={deliveryOtherActorCount};"
            + $"actors={deliveryLiveActorDetail};"
            + $"worker={DescribeLiveActor(worker)}");
        Check(penned?.transportState == CapturedWildlifeTransportState.Penned
              && deliveryStandValid
              && transportMove?.LastGridMoveFailureReason
                  == GridMoveFailureReason.None
              && transportObservation.AwaitingTransportObserved
              && transportObservation.PickupApproachProgressObserved
              && transportObservation.PickupStandReached
              && transportObservation.TransportingObserved
              && !transportObservation.Stalled
              && transport?.IsTransporting != true
              && worker.Brain?.IsExternallyDrivenActionActive != true
              && worker.Brain?.ExternalIntentTerminalCount
                  == transportTerminalBefore + 1
              && worker.Brain?.LastExternalIntentTerminalKind
                  == CharacterAiActionTerminalKind.Completed,
            "TRANSPORT_SUCCESS_TERMINAL",
            $"state={penned?.transportState};active={transport?.IsTransporting};"
            + $"delivery={penned?.penPosition};stand={deliveryStandValid};"
            + $"cell={deliveryCellValid};blocking={deliveryBlockingLayersClear};"
            + $"wildlife={deliveryWildlifeExact};source={sourceResidueClear};"
            + $"parent={parentReleased};capture={capturedStateExact};"
            + $"worldWildlife={worldWildlifeExact};"
            + $"liveActors={liveActorColocationExact};"
            + $"carrier={worker.GetNowXY()};"
            + $"world={worker.transform.position};"
            + $"movement={transportMove?.LastGridMoveFailureReason};"
            + $"systemMove={transportMove?.IsSystemMoveInProgress};"
            + "systemDestination="
            + $"{transportMove?.ActiveSystemMoveDestinationForDiagnostics};"
            + "systemDestinationMatches="
            + $"{transportMove?.IsSystemMoveInProgressTo(pickupProbe.DeliveryStand)};"
            + "movementOwner="
            + $"{transportMove?.ActiveMovementOperationOwnerForDiagnostics};"
            + "movementVersion="
            + $"{transportMove?.MovementOperationVersionForDiagnostics};"
            + $"observation={transportObservation.ObservationSeconds:0.###}/"
            + $"{transportObservation.AllowedSeconds:0.###};"
            + $"stalled={transportObservation.Stalled};"
            + "secondsSinceProgress="
            + $"{transportObservation.SecondsSinceProgress:0.###};"
            + $"progressSamples={transportObservation.ProgressSamples};"
            + "pickupApproach="
            + $"{transportObservation.AwaitingTransportObserved}:"
            + $"{transportObservation.PickupApproachProgressObserved}:"
            + $"{transportObservation.PickupStandReached}:"
            + $"{transportObservation.TransportingObserved};"
            + "lastProgress="
            + $"{transportObservation.LastProgressGridPosition}:"
            + $"{transportObservation.LastProgressWorldPosition};"
            + "cancellationSource="
            + $"{transportMove?.LastMovementCancellationSourceForDiagnostics};"
            + "operationPreemption="
            + $"{transportMove?.LastMovementOperationPreemptionForDiagnostics};"
            + "rejectedOperation="
            + $"{transportMove?.LastRejectedMovementOperationOwnerForDiagnostics};"
            + $"external={worker.Brain?.IsExternallyDrivenActionActive};"
            + $"externalOwner={worker.Brain?.ExternalIntentOwnerId};"
            + $"externalKind={worker.Brain?.ExternalIntentKind};"
            + $"externalEpoch={worker.Brain?.ExternalIntentEpoch};"
            + "deliverySearchDeferrals="
            + $"{transport?.DeliveryStandSearchDeferralCountForDiagnostics};"
            + $"terminal={transportTerminalBefore}"
            + $"->{worker.Brain?.ExternalIntentTerminalCount}:"
            + $"{worker.Brain?.LastExternalIntentTerminalKind};"
            + $"failure={transport?.LastTerminalFailureReasonForDiagnostics}");
        capture.TryRelease(successAnimal.WildlifeId, out _);
        Destroy(successAnimal.gameObject);
        yield return null;

        WildlifeActor lostAnimal = SpawnCaptureAnimal(FindFarWalkable(worker.GetNowXY()));
        Check(lostAnimal != null, "TRANSPORT_SOURCE_LOSS_SOURCE", lostAnimal?.WildlifeId ?? "missing");
        if (lostAnimal != null)
        {
            DamageForCapture(lostAnimal);
            ordered = capture.TryOrderCapture(lostAnimal, worker, pen, out reason);
            string lostId = lostAnimal.WildlifeId;
            if (ordered) Destroy(lostAnimal.gameObject);
            yield return null;
            yield return null;
            yield return WaitUntil(() =>
                transport?.IsTransporting != true
                && worker.Brain?.IsExternallyDrivenActionActive != true,
                8f);
            Check(ordered && !capture.TryGetCaptured(lostId, out _)
                  && transport?.IsTransporting != true,
                "TRANSPORT_SOURCE_LOSS_TERMINAL",
                $"ordered={ordered};active={transport?.IsTransporting}");
        }

        WildlifeActor blockedAnimal = SpawnCaptureAnimal(FindFarWalkable(worker.GetNowXY()));
        Check(blockedAnimal != null, "TRANSPORT_NOPATH_SOURCE", blockedAnimal?.WildlifeId ?? "missing");
        if (blockedAnimal != null)
        {
            DamageForCapture(blockedAnimal);
            Vector2Int isolatedFaultCell = grid.GetCells()
                .Where(cell => cell != null
                    && cell.AreaType == GridCellAreaType.ExteriorPath
                    && grid.IsWalkable(cell.Position))
                .OrderByDescending(cell => Mathf.Abs(
                        cell.Position.x - blockedAnimal.GridPosition.x)
                    + Mathf.Abs(
                        cell.Position.y - blockedAnimal.GridPosition.y))
                .Select(cell => cell.Position)
                .FirstOrDefault();
            worker.transform.position = grid.GetWorldPos(isolatedFaultCell);
            RegisterFaultRing(isolatedFaultCell);
            int failedTerminalBefore =
                worker.Brain?.ExternalIntentTerminalCount ?? 0;
            ordered = capture.TryOrderCapture(blockedAnimal, worker, pen, out reason);
            yield return null;
            RemoveFaultWalls();
            yield return WaitUntil(() =>
                transport?.IsTransporting != true
                && worker.Brain?.IsExternallyDrivenActionActive != true,
                8f);
            Check(ordered && !capture.TryGetCaptured(blockedAnimal.WildlifeId, out _)
                  && transport?.IsTransporting != true
                  && worker.Brain?.ExternalIntentTerminalCount
                      == failedTerminalBefore + 1
                  && worker.Brain?.LastExternalIntentTerminalKind
                      == CharacterAiActionTerminalKind.Failed,
                "TRANSPORT_NOPATH_TERMINAL",
                $"ordered={ordered};reason={reason};active={transport?.IsTransporting};"
                + $"terminal={failedTerminalBefore}"
                + $"->{worker.Brain?.ExternalIntentTerminalCount}:"
                + $"{worker.Brain?.LastExternalIntentTerminalKind}");
            worker.transform.position = grid.GetWorldPos(workerCell);
            Destroy(blockedAnimal.gameObject);
            yield return null;
        }

        TransportChaosStartProbe downedStart = new();
        yield return StartTransportChaos("TRANSPORT_DOWNED", downedStart);
        WildlifeActor downedAnimal = downedStart.Animal;
        Check(downedStart.Live && downedAnimal != null,
            "TRANSPORT_DOWNED_SOURCE",
            $"live={downedStart.Live};animal={downedAnimal?.WildlifeId ?? "missing"}");
        if (downedStart.Live && downedAnimal != null)
        {
            CharacterBodyHealthSnapshot originalTransportBody =
                CloneBodyHealthSnapshot(bodyHealthQuery.GetSnapshot(worker));
            bool downedFixtureReady = TryCreateLawfulDownedSnapshot(
                originalTransportBody,
                out CharacterBodyHealthSnapshot downedTransportBody,
                out string downedTransportDetail);
            int downedTerminalBefore = downedStart.TerminalBefore;
            int downedReleaseAttemptsBefore =
                worker.TransientAiOwnershipReleaseAttemptCountForDiagnostics;
            bool downedOwnershipReady =
                !worker.TransientAiOwnershipReleasedForDiagnostics;
            if (downedFixtureReady)
            {
                bodyHealthCommands.ApplySnapshot(
                    worker,
                    downedTransportBody,
                    "qa-wildlife-transport-downed");
            }
            yield return null;
            yield return null;
            CharacterBodyHealthSnapshot downedTransportObserved =
                bodyHealthQuery.GetSnapshot(worker);
            Check(downedFixtureReady
                  && downedOwnershipReady
                  && downedTransportObserved.Downed
                  && worker.CurrentLifecycleState
                      == CharacterLifecycleState.Downed
                  && !capture.TryGetCaptured(downedAnimal.WildlifeId, out _)
                  && downedStart.Transport?.IsTransporting != true
                  && worker.Brain?.IsExternallyDrivenActionActive != true
                  && worker.Brain?.ExternalIntentTerminalCount
                      == downedTerminalBefore + 1
                  && worker.Brain?.LastExternalIntentTerminalKind
                      == CharacterAiActionTerminalKind.Cancelled,
                "TRANSPORT_DOWNED_TERMINAL",
                $"live={downedStart.Live};fixture={downedFixtureReady}:"
                + $"{downedTransportDetail};"
                + $"body={downedTransportObserved.Downed}/"
                + $"{downedTransportObserved.Mobility:0.###};"
                + $"lifecycle={worker.CurrentLifecycleState};"
                + $"active={downedStart.Transport?.IsTransporting};"
                + $"external={worker.Brain?.IsExternallyDrivenActionActive};"
                + $"externalOwner={worker.Brain?.ExternalIntentOwnerId};"
                + $"externalKind={worker.Brain?.ExternalIntentKind};"
                + $"externalEpoch={worker.Brain?.ExternalIntentEpoch};"
                + "ownership="
                + $"{downedOwnershipReady}:"
                + $"{downedReleaseAttemptsBefore}"
                + $"->{worker.TransientAiOwnershipReleaseAttemptCountForDiagnostics}:"
                + $"{worker.TransientAiOwnershipReleasedForDiagnostics}:"
                + $"{worker.LastTransientAiOwnershipReleaseReasonForDiagnostics};"
                + "failure="
                + $"{downedStart.Transport?.LastTerminalFailureReasonForDiagnostics};"
                + $"terminal={downedTerminalBefore}"
                + $"->{worker.Brain?.ExternalIntentTerminalCount}:"
                + $"{worker.Brain?.LastExternalIntentTerminalKind}");
            worker.SetAiPaused(true);
            bodyHealthCommands.ApplySnapshot(
                worker,
                originalTransportBody,
                "qa-wildlife-transport-downed-recovered");
            yield return null;
            yield return null;
            Destroy(downedAnimal.gameObject);
            yield return null;
        }

        WildlifeActor penLossAnimal = SpawnCaptureAnimal(worker.GetNowXY());
        Check(penLossAnimal != null, "TRANSPORT_PEN_LOSS_SOURCE", penLossAnimal?.WildlifeId ?? "missing");
        if (penLossAnimal != null)
        {
            DamageForCapture(penLossAnimal);
            ordered = capture.TryOrderCapture(penLossAnimal, worker, faultPen, out reason);
            if (ordered) faultPen.DestroySelf();
            yield return null;
            yield return null;
            Check(ordered && !capture.TryGetCaptured(penLossAnimal.WildlifeId, out _)
                  && transport?.IsTransporting != true,
                "TRANSPORT_PEN_DESTROY_TERMINAL",
                $"ordered={ordered};active={transport?.IsTransporting}");
            Destroy(penLossAnimal.gameObject);
            yield return null;
        }

        if (failures.Count == 0)
        {
            yield return VerifyTransportAlertCarrierDownedChaos();
        }
        if (failures.Count == 0)
        {
            yield return VerifyTransportDestinationContenderChaos();
        }
    }

    private IEnumerator VerifyTransportAlertCarrierDownedChaos()
    {
        TransportChaosStartProbe start = new();
        yield return StartTransportChaos(
            "TRANSPORT_ALERT",
            start);
        if (!start.Live)
        {
            yield break;
        }

        List<ActorAiPauseState> isolatedActors = PauseAllActorsExceptWorker();
        CharacterBodyHealthSnapshot originalBody = CloneBodyHealthSnapshot(
            bodyHealthQuery.GetSnapshot(worker));
        if (!TryCreateLawfulDownedSnapshot(
                originalBody,
                out CharacterBodyHealthSnapshot downedBody,
                out string downedFixtureDetail))
        {
            Check(false,
                "TRANSPORT_ALERT_CARRIER_DOWNED_BODY_FIXTURE",
                downedFixtureDetail);
            RestoreActorAiPauseStates(isolatedActors);
            yield break;
        }

        worker.SetAiPaused(false);
        int startedBefore = invasionStartedEventCount;
        int resolvedBefore = invasionResolvedEventCount;
        int candidateBefore = invasionCandidateEventCount;
        gameEvents.Publish(new InvasionStartedEvent(default));
        SettlementAlertSnapshot red = settlementAlerts.Capture();
        capture.TryGetCaptured(
            start.Animal.WildlifeId,
            out CapturedWildlifeState redState);
        bool redBoundWhileTransportLive =
            invasionStartedEventCount == startedBefore + 1
            && invasionResolvedEventCount == resolvedBefore
            && invasionCandidateEventCount == candidateBefore
            && red.DesiredLevel == SettlementThreatAlertLevel.Red
            && red.CommittedLevel == SettlementThreatAlertLevel.Red
            && red.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal)
            && workerWork.HasEmergencyResponseWorkGateForDiagnostics
            && workerWork.EmergencyResponseWorkEpochForDiagnostics
                == red.AlertEpochId
            && workerWork.EmergencyResponseOnlyWorkTypeForDiagnostics
                == BuiltInWorkTypeIds.Guard
            && start.Transport.IsTransporting
            && worker.Brain.IsExternallyDrivenActionActive
            && string.Equals(
                worker.Brain.ExternalIntentOwnerId,
                start.ExpectedOwner,
                StringComparison.Ordinal)
            && redState?.transportState
                == CapturedWildlifeTransportState.Transporting;
        Check(redBoundWhileTransportLive,
            "TRANSPORT_ALERT_EXTERNAL_LEASE_RED_BOUND",
            $"alert={red.DesiredLevel}/{red.CommittedLevel}/{red.AlertEpochId};"
            + $"events={startedBefore}->{invasionStartedEventCount}:"
            + $"{resolvedBefore}->{invasionResolvedEventCount}:"
            + $"{candidateBefore}->{invasionCandidateEventCount};"
            + $"gate={workerWork.HasEmergencyResponseWorkGateForDiagnostics}:"
            + $"{workerWork.EmergencyResponseWorkEpochForDiagnostics}:"
            + $"{workerWork.EmergencyResponseOnlyWorkTypeForDiagnostics};"
            + $"transport={start.Transport.IsTransporting};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive}:"
            + $"{worker.Brain.ExternalIntentOwnerId};"
            + $"state={redState?.transportState};"
            + $"phase={worker.Brain.CurrentActionPhase};"
            + $"movement={start.Move.IsSystemMoveInProgress}:"
            + $"{start.Move.ActiveSystemMoveDestinationForDiagnostics}");

        bool noGuardOverlap = redBoundWhileTransportLive
            && !workerWork.isWorking
            && !workerWork.HasActiveWorkRoutineForDiagnostics
            && !worker.Brain.HasRunningWorkAction
            && worker.Brain.bestAction == null;
        Check(noGuardOverlap,
            "TRANSPORT_ALERT_NO_GUARD_OVERLAP",
            $"work={workerWork.isWorking}/"
            + $"{workerWork.HasActiveWorkRoutineForDiagnostics};"
            + $"brainWork={worker.Brain.HasRunningWorkAction};"
            + $"action={worker.Brain.bestAction?.actionset?.GetType().Name};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive};"
            + $"gate={workerWork.HasEmergencyResponseWorkGateForDiagnostics}");
        if (!redBoundWhileTransportLive || !noGuardOverlap)
        {
            gameEvents.Publish(new InvasionResolvedEvent(true, 0f));
            RestoreActorAiPauseStates(isolatedActors);
            yield break;
        }

        int terminalBefore = worker.Brain.ExternalIntentTerminalCount;
        bodyHealthCommands.ApplySnapshot(
            worker,
            downedBody,
            "qa-wildlife-transport-alert-carrier-downed");
        CharacterBodyHealthSnapshot immediateBody = bodyHealthQuery
            .GetSnapshot(worker);
        bool stateRemoved = !capture.TryGetCaptured(
            start.Animal.WildlifeId,
            out CapturedWildlifeState afterDownedState);
        Vector2Int[] immediateRegistrations = FindGridOccupantRegistrations(
            grid,
            GridLayer.Wildlife,
            start.Animal);
        bool synchronousTerminal = immediateBody.Downed
            && worker.CurrentLifecycleState == CharacterLifecycleState.Downed
            && !worker.CanRunAi
            && !start.Transport.IsTransporting
            && !worker.Brain.IsExternallyDrivenActionActive
            && stateRemoved
            && worker.Brain.ExternalIntentTerminalCount == terminalBefore + 1
            && worker.Brain.LastExternalIntentTerminalKind
                == CharacterAiActionTerminalKind.Cancelled;
        Check(synchronousTerminal,
            "TRANSPORT_ALERT_CARRIER_DOWNED_EXACT_TERMINAL",
            $"body={immediateBody.Downed}/{immediateBody.Mobility:0.###};"
            + $"lifecycle={worker.CurrentLifecycleState};"
            + $"canRun={worker.CanRunAi};"
            + $"transport={start.Transport.IsTransporting};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive};"
            + $"captureRemoved={stateRemoved};"
            + $"state={afterDownedState?.transportState};"
            + $"terminal={terminalBefore}"
            + $"->{worker.Brain.ExternalIntentTerminalCount}:"
            + $"{worker.Brain.LastExternalIntentTerminalKind};"
            + $"failure={start.Transport.LastTerminalFailureReasonForDiagnostics};"
            + $"registrations={string.Join(",", immediateRegistrations)}");

        bool physicalRollback = immediateRegistrations.Length == 1
            && ReferenceEquals(
                grid.GetGridCell(immediateRegistrations[0])
                    ?.GetOccupant(GridLayer.Wildlife),
                start.Animal)
            && ReferenceEquals(start.Animal.transform.parent, start.OriginalParent)
            && !start.Animal.transform.IsChildOf(worker.transform)
            && start.Animal.State != WildlifeState.Captured
            && start.Animal.IsAlive;
        Check(physicalRollback,
            "TRANSPORT_ALERT_ROLLBACK_PHYSICAL_CONVERGED",
            $"registrations={immediateRegistrations.Length}:"
            + $"{string.Join(",", immediateRegistrations)};"
            + $"gridPosition={start.Animal.GridPosition};"
            + $"state={start.Animal.State};"
            + $"parent={DescribeTransform(start.Animal.transform.parent)};"
            + $"expectedParent={DescribeTransform(start.OriginalParent)};"
            + $"childOfCarrier={start.Animal.transform.IsChildOf(worker.transform)};"
            + $"alive={start.Animal.IsAlive}");

        yield return null;
        yield return null;
        bool gateRetired = !workerWork.HasEmergencyResponseWorkGateForDiagnostics
            && !worker.Brain.IsExternallyDrivenActionActive
            && !start.Transport.IsTransporting;
        Check(gateRetired,
            "TRANSPORT_ALERT_DOWNED_GATE_RETIRED",
            $"gate={workerWork.HasEmergencyResponseWorkGateForDiagnostics}:"
            + $"{workerWork.EmergencyResponseWorkEpochForDiagnostics};"
            + $"responses={alarmResponses.PendingResponderCountForDiagnostics}/"
            + $"{alarmResponses.ReturningResponderCountForDiagnostics}/"
            + $"{alarmResponses.AssignedResponderCountForDiagnostics};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive};"
            + $"transport={start.Transport.IsTransporting}");

        worker.SetAiPaused(true);
        bodyHealthCommands.ApplySnapshot(
            worker,
            originalBody,
            "qa-wildlife-transport-alert-carrier-recovered");
        yield return null;
        yield return null;

        yield return ResolveTransportChaosInvasionAndWaitForGreen(
            "TRANSPORT_ALERT");

        CharacterBodyHealthSnapshot recovered = bodyHealthQuery
            .GetSnapshot(worker);
        bool recoveredClean = !recovered.Downed
            && worker.CurrentLifecycleState == CharacterLifecycleState.Active
            && !medicalQuery.ActiveOrders.Any(order => order != null
                && order.IsActive
                && string.Equals(
                    order.patientId,
                    worker.Identity?.PersistentId,
                    StringComparison.Ordinal))
            && !workerWork.HasEmergencyResponseWorkGateForDiagnostics
            && !worker.Brain.IsExternallyDrivenActionActive
            && !start.Transport.IsTransporting;
        Check(recoveredClean,
            "TRANSPORT_ALERT_RESPONSE_GREEN_CONVERGED",
            $"body={recovered.Downed}/{recovered.Mobility:0.###};"
            + $"lifecycle={worker.CurrentLifecycleState};"
            + $"medical={medicalQuery.ActiveOrders.Count(order => order != null && order.IsActive && string.Equals(order.patientId, worker.Identity?.PersistentId, StringComparison.Ordinal))};"
            + $"gate={workerWork.HasEmergencyResponseWorkGateForDiagnostics};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive};"
            + $"transport={start.Transport.IsTransporting};"
            + $"alert={settlementAlerts.Capture().CommittedLevel}");
        RestoreActorAiPauseStates(isolatedActors);
        Destroy(start.Animal.gameObject);
        yield return null;
    }

    private IEnumerator VerifyTransportDestinationContenderChaos()
    {
        TransportChaosStartProbe start = new();
        yield return StartTransportChaos(
            "TRANSPORT_CONTENDER",
            start);
        if (!start.Live)
        {
            yield break;
        }

        Vector2Int destination = start.State.penPosition;
        WildlifeActor blocker = SpawnCaptureAnimal(destination);
        if (blocker != null)
        {
            blocker.SetCaptured(true);
            blocker.WarpTo(destination);
        }
        bool blockerExact = blocker != null
            && ReferenceEquals(
                grid.GetGridCell(destination)?.GetOccupant(GridLayer.Wildlife),
                blocker)
            && blocker.GridPosition == destination
            && blocker.State == WildlifeState.Captured;
        Check(blockerExact,
            "TRANSPORT_CONTENDER_INJECTED_AFTER_DESTINATION_RESOLVED",
            $"destination={destination};"
            + $"movement={start.Move.IsSystemMoveInProgress}:"
            + $"{start.Move.ActiveSystemMoveDestinationForDiagnostics};"
            + $"blocker={DescribeGridOccupant(blocker)};"
            + $"cell={DescribeGridOccupant(grid.GetGridCell(destination)?.GetOccupant(GridLayer.Wildlife))};"
            + $"state={start.State.transportState};"
            + $"carrier={worker.GetNowXY()}");
        if (!blockerExact)
        {
            yield break;
        }

        float expectedRealSeconds = start.PathWorldDistance
            / Mathf.Max(0.1f, worker.GetMoveSpeed());
        float allowedSeconds = Mathf.Clamp(
            8f + expectedRealSeconds * 2f + start.PathStepCount * 0.15f,
            20f,
            90f);
        float deadline = Time.realtimeSinceStartup + allowedSeconds;
        while (Time.realtimeSinceStartup < deadline
               && start.Transport.IsTransporting)
        {
            yield return null;
        }
        capture.TryGetCaptured(
            start.Animal.WildlifeId,
            out CapturedWildlifeState terminalState);
        Vector2Int[] targetRegistrations = FindGridOccupantRegistrations(
            grid,
            GridLayer.Wildlife,
            start.Animal);
        Vector2Int[] blockerRegistrations = FindGridOccupantRegistrations(
            grid,
            GridLayer.Wildlife,
            blocker);
        bool typedRollback = !start.Transport.IsTransporting
            && !worker.Brain.IsExternallyDrivenActionActive
            && terminalState == null
            && worker.Brain.ExternalIntentTerminalCount
                == start.TerminalBefore + 1
            && worker.Brain.LastExternalIntentTerminalKind
                == CharacterAiActionTerminalKind.Failed
            && start.Transport.LastTerminalFailureReasonForDiagnostics
                .IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0;
        Check(typedRollback,
            "TRANSPORT_CONTENDER_TYPED_ROLLBACK",
            $"transport={start.Transport.IsTransporting};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive};"
            + $"capture={terminalState?.transportState};"
            + $"terminal={start.TerminalBefore}"
            + $"->{worker.Brain.ExternalIntentTerminalCount}:"
            + $"{worker.Brain.LastExternalIntentTerminalKind};"
            + $"failure={start.Transport.LastTerminalFailureReasonForDiagnostics};"
            + $"carrier={worker.GetNowXY()};destination={destination};"
            + $"movement={start.Move.IsSystemMoveInProgress}:"
            + $"{start.Move.ActiveSystemMoveDestinationForDiagnostics};"
            + $"allowed={allowedSeconds:0.###}");

        bool physicalConservation = targetRegistrations.Length == 1
            && blockerRegistrations.Length == 1
            && blockerRegistrations[0] == destination
            && ReferenceEquals(
                grid.GetGridCell(targetRegistrations[0])
                    ?.GetOccupant(GridLayer.Wildlife),
                start.Animal)
            && ReferenceEquals(
                grid.GetGridCell(destination)?.GetOccupant(GridLayer.Wildlife),
                blocker)
            && targetRegistrations[0] != destination
            && ReferenceEquals(start.Animal.transform.parent, start.OriginalParent)
            && !start.Animal.transform.IsChildOf(worker.transform)
            && start.Animal.State != WildlifeState.Captured
            && world.Wildlife.Count(value => ReferenceEquals(value, start.Animal)) == 1
            && world.Wildlife.Count(value => ReferenceEquals(value, blocker)) == 1;
        Check(physicalConservation,
            "TRANSPORT_CONTENDER_PHYSICAL_CONSERVATION",
            $"target={targetRegistrations.Length}:"
            + $"{string.Join(",", targetRegistrations)}:"
            + $"{start.Animal.GridPosition}:{start.Animal.State};"
            + $"blocker={blockerRegistrations.Length}:"
            + $"{string.Join(",", blockerRegistrations)}:"
            + $"{blocker.GridPosition}:{blocker.State};"
            + $"parent={DescribeTransform(start.Animal.transform.parent)};"
            + $"expectedParent={DescribeTransform(start.OriginalParent)};"
            + $"world={world.Wildlife.Count(value => ReferenceEquals(value, start.Animal))}/"
            + $"{world.Wildlife.Count(value => ReferenceEquals(value, blocker))}");
        Check(terminalState == null
              && !capture.CapturedAnimals.Any(value => value != null
                  && string.Equals(
                      value.wildlifeId,
                      start.Animal.WildlifeId,
                      StringComparison.Ordinal))
              && !ReferenceEquals(
                  grid.GetGridCell(destination)?.GetOccupant(GridLayer.Wildlife),
                  start.Animal),
            "TRANSPORT_CONTENDER_NO_PENNED_DIVERGENCE",
            $"capture={terminalState?.transportState};"
            + $"aggregateMatches={capture.CapturedAnimals.Count(value => value != null && string.Equals(value.wildlifeId, start.Animal.WildlifeId, StringComparison.Ordinal))};"
            + $"destination={DescribeGridOccupant(grid.GetGridCell(destination)?.GetOccupant(GridLayer.Wildlife))};"
            + $"targetRegistrations={targetRegistrations.Length}");

        if (blocker != null)
        {
            Destroy(blocker.gameObject);
            yield return null;
        }
    }

    private IEnumerator StartTransportChaos(
        string markerPrefix,
        TransportChaosStartProbe probe)
    {
        worker.SetAiPaused(true);
        worker.SetLifecycleState(CharacterLifecycleState.Active);
        worker.transform.position = grid.GetWorldPos(workerCell);
        AbilityWildlifeCaptureTransport existingTransport =
            worker.GetComponent<AbilityWildlifeCaptureTransport>();
        existingTransport?.StopForLifecycleTransition(
            markerPrefix.ToLowerInvariant() + "-prepare");
        worker.Brain?.StopCurrentActionForReplan(
            markerPrefix.ToLowerInvariant() + "-prepare");
        worker.GetComponent<AbilityHaul>()?.StopHauling(
            markerPrefix.ToLowerInvariant() + "-prepare");
        worker.GetComponent<AbilityMove>()?.CancelActiveMovement(
            markerPrefix.ToLowerInvariant() + "-prepare");
        workerWork?.ClearPriorityWorkTarget();
        yield return null;
        yield return null;

        TransportFixturePlanProbe plan = new();
        yield return PlanLawfulTransportFixture(plan);
        WildlifeActor animal = plan.Ready
            ? SpawnCaptureAnimal(plan.SourceCell)
            : null;
        if (animal != null)
        {
            DamageForCapture(animal);
        }
        PickupDeliveryPreflightProbe preflight = new();
        if (animal != null)
        {
            yield return PlaceWorkerAtReachablePickupAndDeliveryStand(
                animal,
                plan,
                preflight);
        }
        bool fixtureReady = plan.Ready
            && animal != null
            && preflight.Ready;
        Check(fixtureReady,
            markerPrefix + "_LAWFUL_FIXTURE",
            $"plan={plan.Ready}:{plan.Detail};"
            + $"animal={animal?.WildlifeId}:{animal?.GridPosition};"
            + $"preflight={preflight.Ready}:{preflight.Detail}");
        if (!fixtureReady)
        {
            yield break;
        }

        int terminalBefore = worker.Brain.ExternalIntentTerminalCount;
        Transform originalParent = animal.transform.parent;
        Vector2Int source = animal.GridPosition;
        bool ordered = capture.TryOrderCapture(
            animal,
            worker,
            pen,
            out string orderFailure);
        AbilityWildlifeCaptureTransport transport =
            worker.GetComponent<AbilityWildlifeCaptureTransport>();
        AbilityMove move = worker.GetComponent<AbilityMove>();
        float expectedRealSeconds = preflight.PathWorldDistance
            / Mathf.Max(0.1f, worker.GetMoveSpeed());
        float allowedSeconds = Mathf.Clamp(
            8f + expectedRealSeconds * 2f + preflight.PathStepCount * 0.15f,
            20f,
            90f);
        float deadline = Time.realtimeSinceStartup + allowedSeconds;
        CapturedWildlifeState state = null;
        bool live = false;
        while (ordered
               && Time.realtimeSinceStartup < deadline
               && transport?.IsTransporting == true)
        {
            capture.TryGetCaptured(animal.WildlifeId, out state);
            live = state?.transportState
                    == CapturedWildlifeTransportState.Transporting
                && animal.transform.IsChildOf(worker.transform)
                && worker.Brain.IsExternallyDrivenActionActive
                && move?.IsSystemMoveInProgress == true
                && move.IsSystemMoveInProgressTo(state.penPosition);
            if (live)
            {
                break;
            }
            yield return null;
        }

        string expectedOwner =
            $"captivity:wildlife-transport:{animal.WildlifeId}";
        live &= string.Equals(
            worker.Brain.ExternalIntentOwnerId,
            expectedOwner,
            StringComparison.Ordinal);
        Check(ordered && live,
            markerPrefix + "_PRODUCTION_CARRY_LIVE",
            $"ordered={ordered}:{orderFailure};"
            + $"state={state?.transportState};"
            + $"carrier={worker.GetNowXY()};"
            + $"animal={animal.GridPosition};"
            + $"parented={animal.transform.IsChildOf(worker.transform)};"
            + $"transport={transport?.IsTransporting};"
            + $"external={worker.Brain.IsExternallyDrivenActionActive}:"
            + $"{worker.Brain.ExternalIntentOwnerId};"
            + $"expected={expectedOwner};"
            + $"movement={move?.IsSystemMoveInProgress}:"
            + $"{move?.ActiveSystemMoveDestinationForDiagnostics};"
            + $"planned={state?.penPosition};"
            + $"terminalFailure={transport?.LastTerminalFailureReasonForDiagnostics};"
            + $"allowed={allowedSeconds:0.###}");
        if (!ordered || !live)
        {
            yield break;
        }

        probe.Live = true;
        probe.Animal = animal;
        probe.OriginalParent = originalParent;
        probe.Source = source;
        probe.State = state;
        probe.Transport = transport;
        probe.Move = move;
        probe.ExpectedOwner = expectedOwner;
        probe.TerminalBefore = terminalBefore;
        probe.PathStepCount = preflight.PathStepCount;
        probe.PathWorldDistance = preflight.PathWorldDistance;
    }

    private IEnumerator ResolveTransportChaosInvasionAndWaitForGreen(
        string markerPrefix)
    {
        int resolvedBefore = invasionResolvedEventCount;
        int startedBefore = invasionStartedEventCount;
        int candidatesBefore = invasionCandidateEventCount;
        gameEvents.Publish(new InvasionResolvedEvent(true, 0f));
        SettlementAlertSnapshot immediate = settlementAlerts.Capture();
        bool resolved = invasionResolvedEventCount == resolvedBefore + 1
            && invasionStartedEventCount == startedBefore
            && invasionCandidateEventCount == candidatesBefore
            && immediate.DesiredLevel == SettlementThreatAlertLevel.Green
            && !immediate.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal);
        Check(resolved,
            markerPrefix + "_INVASION_RESOLVED",
            $"events={startedBefore}->{invasionStartedEventCount}:"
            + $"{resolvedBefore}->{invasionResolvedEventCount}:"
            + $"{candidatesBefore}->{invasionCandidateEventCount};"
            + $"alert={immediate.DesiredLevel}/{immediate.CommittedLevel};"
            + $"incidents=[{string.Join(",", immediate.ActiveIncidentIds)}]");

        long startedHour = calendar.AbsoluteHour;
        float deadline = Time.realtimeSinceStartup + 45f;
        SettlementAlertSnapshot current = immediate;
        while (resolved
               && Time.realtimeSinceStartup < deadline
               && calendar.AbsoluteHour - startedHour <= 6L)
        {
            current = settlementAlerts.Capture();
            if (current.DesiredLevel == SettlementThreatAlertLevel.Green
                && current.CommittedLevel == SettlementThreatAlertLevel.Green
                && !current.ActiveIncidentIds.Contains(
                    "incident:invasion:active",
                    StringComparer.Ordinal)
                && alarmResponses.PendingResponderCountForDiagnostics == 0
                && alarmResponses.ReturningResponderCountForDiagnostics == 0
                && alarmResponses.AssignedResponderCountForDiagnostics == 0
                && !world.Characters.Where(value => value != null)
                    .Select(value => value.GetComponent<AbilityWork>())
                    .Any(value => value != null
                        && value.HasEmergencyResponseWorkGateForDiagnostics))
            {
                break;
            }
            yield return null;
        }

        current = settlementAlerts.Capture();
        bool clean = resolved
            && current.DesiredLevel == SettlementThreatAlertLevel.Green
            && current.CommittedLevel == SettlementThreatAlertLevel.Green
            && !current.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal)
            && alarmResponses.PendingResponderCountForDiagnostics == 0
            && alarmResponses.ReturningResponderCountForDiagnostics == 0
            && alarmResponses.AssignedResponderCountForDiagnostics == 0
            && !world.Characters.Where(value => value != null)
                .Select(value => value.GetComponent<AbilityWork>())
                .Any(value => value != null
                    && value.HasEmergencyResponseWorkGateForDiagnostics);
        Check(clean,
            markerPrefix + "_FINAL_GREEN",
            $"hours={calendar.AbsoluteHour - startedHour};"
            + $"alert={current.DesiredLevel}/{current.CommittedLevel};"
            + $"incidents=[{string.Join(",", current.ActiveIncidentIds)}];"
            + $"responses={alarmResponses.PendingResponderCountForDiagnostics}/"
            + $"{alarmResponses.ReturningResponderCountForDiagnostics}/"
            + $"{alarmResponses.AssignedResponderCountForDiagnostics}");
    }

    private List<ActorAiPauseState> PauseAllActorsExceptWorker()
    {
        List<ActorAiPauseState> states = new();
        foreach (CharacterActor actor in world.Characters.Where(value =>
                     value != null && value != worker))
        {
            states.Add(new ActorAiPauseState(actor, actor.IsAiPaused()));
            actor.SetAiPaused(true);
        }
        return states;
    }

    private static void RestoreActorAiPauseStates(
        IEnumerable<ActorAiPauseState> states)
    {
        foreach (ActorAiPauseState state in states ??
                     Array.Empty<ActorAiPauseState>())
        {
            if (state.Actor != null)
            {
                state.Actor.SetAiPaused(state.WasPaused);
            }
        }
    }

    private static bool TryCreateLawfulDownedSnapshot(
        CharacterBodyHealthSnapshot original,
        out CharacterBodyHealthSnapshot downed,
        out string detail)
    {
        downed = default;
        detail = "missing body parts";
        if (original.Parts == null || original.Parts.Count == 0)
        {
            return false;
        }

        List<CharacterBodyPartHealthState> parts = original.Parts
            .Select(CloneBodyPart)
            .ToList();
        int legs = 0;
        foreach (CharacterBodyPartHealthState part in parts)
        {
            if (part?.bodyPart is not CombatBodyPart.LeftLeg
                and not CombatBodyPart.RightLeg)
            {
                continue;
            }
            part.currentHealth = Mathf.Max(0.5f, part.maxHealth * 0.18f);
            legs++;
        }
        detail = $"legs={legs};parts={parts.Count};"
            + $"before={original.Downed}/{original.Mobility:0.###}";
        if (legs != 2)
        {
            return false;
        }

        downed = new CharacterBodyHealthSnapshot(
            parts,
            original.BloodLoss,
            original.Suppression,
            consciousness: 1f,
            manipulation: 1f,
            mobility: 0.08f,
            downed: true);
        return true;
    }

    private IEnumerator ObserveTransportTerminal(
        string wildlifeId,
        PickupDeliveryPreflightProbe preflight,
        TransportTerminalObservation observation)
    {
        if (observation == null)
        {
            yield break;
        }

        AbilityWildlifeCaptureTransport transport =
            worker.GetComponent<AbilityWildlifeCaptureTransport>();
        AbilityMove move = worker.GetComponent<AbilityMove>();
        float speed = Mathf.Max(0.1f, worker.GetMoveSpeed());
        float pathDistance = Mathf.Max(
            0f,
            preflight?.PathWorldDistance ?? 0f);
        int pathSteps = Mathf.Max(0, preflight?.PathStepCount ?? 0);
        // Ability movement may cross scaled and unscaled waits while entering
        // doors or rebuilding an incremental path. Use the slower unscaled
        // distance bound and let the no-progress watchdog catch real stalls.
        float expectedRealSeconds = pathDistance / speed;
        observation.AllowedSeconds = Mathf.Clamp(
            8f + expectedRealSeconds * 2f + pathSteps * 0.15f,
            20f,
            90f);

        float startedAt = Time.realtimeSinceStartup;
        float lastProgressAt = startedAt;
        Vector3 lastWorld = worker.transform.position;
        Vector2Int lastGrid = worker.GetNowXY();
        observation.LastProgressWorldPosition = lastWorld;
        observation.LastProgressGridPosition = lastGrid;
        const float progressEpsilon = 0.005f;
        const float movementStallSeconds = 5f;
        bool observedSystemMovement = false;

        while (Time.realtimeSinceStartup - startedAt
               < observation.AllowedSeconds)
        {
            bool hasState = capture.TryGetCaptured(
                wildlifeId,
                out CapturedWildlifeState state);
            if (hasState && state != null)
            {
                if (state.transportState
                    == CapturedWildlifeTransportState.AwaitingTransport)
                {
                    observation.AwaitingTransportObserved = true;
                    observation.PickupStandReached |=
                        worker.GetNowXY() == preflight.PickupStand;
                }
                else if (state.transportState
                    == CapturedWildlifeTransportState.Transporting)
                {
                    observation.TransportingObserved = true;
                    observation.PickupStandReached |=
                        worker.GetNowXY() == preflight.PickupStand;
                }
            }
            if (state?.transportState
                == CapturedWildlifeTransportState.Penned)
            {
                observation.Penned = true;
                break;
            }
            if (transport?.IsTransporting != true
                || worker.Brain?.IsExternallyDrivenActionActive != true)
            {
                break;
            }

            Vector3 currentWorld = worker.transform.position;
            Vector2Int currentGrid = worker.GetNowXY();
            if (Vector3.Distance(currentWorld, lastWorld) > progressEpsilon
                || currentGrid != lastGrid)
            {
                lastWorld = currentWorld;
                lastGrid = currentGrid;
                lastProgressAt = Time.realtimeSinceStartup;
                observation.LastProgressWorldPosition = currentWorld;
                observation.LastProgressGridPosition = currentGrid;
                observation.ProgressSamples++;
                if (state?.transportState
                    == CapturedWildlifeTransportState.AwaitingTransport)
                {
                    observation.PickupApproachProgressObserved = true;
                }
            }

            if (move?.IsSystemMoveInProgress == true)
            {
                observedSystemMovement = true;
            }
            if (observedSystemMovement
                && move?.IsSystemMoveInProgress == true
                && Time.realtimeSinceStartup - lastProgressAt
                    >= movementStallSeconds)
            {
                observation.Stalled = true;
                break;
            }
            yield return null;
        }

        observation.ObservationSeconds =
            Time.realtimeSinceStartup - startedAt;
        observation.SecondsSinceProgress =
            Time.realtimeSinceStartup - lastProgressAt;
    }

    private IEnumerator TryBeginEscapeThroughInvasion(
        CaptiveState state,
        string scenario,
        EscapeStartProbe probe)
    {
        if (probe == null)
        {
            yield break;
        }
        if (state == null || gameEvents == null)
        {
            probe.Reason = $"{scenario}: missing captive state or event bus";
            yield break;
        }

        // InvasionStartedEvent is the registered production command boundary.
        // The escape-ready authority was installed through the V18 restore
        // transaction; TryGetCaptive deliberately returns a read-only clone.
        int startedBefore = invasionStartedEventCount;
        int resolvedBefore = invasionResolvedEventCount;
        int candidatesBefore = invasionCandidateEventCount;
        gameEvents.Publish(new InvasionStartedEvent(default));
        SettlementAlertSnapshot immediateAlert = settlementAlerts.Capture();
        bool isolatedStart = invasionStartedEventCount == startedBefore + 1
            && invasionResolvedEventCount == resolvedBefore
            && invasionCandidateEventCount == candidatesBefore
            && immediateAlert.ActiveIncidentIds.Contains(
                "incident:invasion:active",
                StringComparer.Ordinal)
            && immediateAlert.DesiredLevel == SettlementThreatAlertLevel.Red
            && immediateAlert.CommittedLevel == SettlementThreatAlertLevel.Red;
        Check(isolatedStart,
            "ESCAPE_INVASION_START_COMMITTED",
            $"scenario={scenario};"
            + $"events=start:{startedBefore}->{invasionStartedEventCount},"
            + $"resolved:{resolvedBefore}->{invasionResolvedEventCount},"
            + $"candidate:{candidatesBefore}->{invasionCandidateEventCount};"
            + $"desired={immediateAlert.DesiredLevel};"
            + $"committed={immediateAlert.CommittedLevel};"
            + $"incident={DescribeInvasionIncident()}");
        if (!isolatedStart)
        {
            probe.Reason = scenario + ": invasion event isolation failed";
            yield break;
        }
        int initialAttempts = state.failedEscapeAttempts;
        yield return WaitUntil(() =>
        {
            if (!captivity.TryGetCaptive(
                    state.captiveId,
                    out CaptiveState observed))
            {
                return false;
            }
            probe.Started = observed.status is CaptivityStatus.EscapeAttempt
                or CaptivityStatus.Escaped
                || observed.failedEscapeAttempts > initialAttempts;
            return probe.Started;
        }, 5f);
        if (probe.Started)
        {
            probe.Reason = string.Empty;
            yield break;
        }

        captivity.TryGetCaptive(state.captiveId, out CaptiveState current);
        probe.Reason = $"{scenario}: invasion trigger did not start escape; "
            + $"status={current?.status};inCustody={current?.IsInCustody};"
            + $"falseCompliance={current?.falseCompliance};"
            + $"risk={current?.escapeRisk};result={current?.lastResult};"
            + $"actor={DescribeActor(captiveActor)};"
            + $"lifecycle={captiveActor?.CurrentLifecycleState}";
    }

    private IEnumerator VerifyAnimalCareRow()
    {
        WildlifeActor animal = SpawnCaptureAnimal(pen.centerPos);
        Check(animal != null, "ANIMAL_CARE_SOURCE", animal?.WildlifeId ?? "missing");
        if (animal == null || workerWork == null) yield break;
        animal.SetCaptured(true);
        animal.ChangeHunger(0.9f - animal.Hunger);
        animal.ChangeThirst(-animal.Thirst);
        string penId = pen.RequirePersistentInstanceId().Value;
        string feedItemId = ResolveAuthoredFeedItemId(animal);
        long expectedFeedMass = string.IsNullOrEmpty(feedItemId)
            ? 0L
            : itemRuntime.MassQuery
                .GetDefinitionUnitMass((ItemDefinitionId)feedItemId)
                .Value;
        int feedQuantityBefore = CountFacilityItem(penId, feedItemId);
        bool feedSpawned = !string.IsNullOrEmpty(feedItemId)
            && itemRuntime.SpawnItemAt(
                feedItemId,
                1,
                pen.centerPos,
                WorldItemStackState.FacilityBuffer,
                penId,
                out int spawnedFeed)
            && spawnedFeed == 1;
        WorldItemStackSnapshot feedSource = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, penId, StringComparison.Ordinal)
                && string.Equals(stack.ItemId, feedItemId, StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        Check(feedSpawned
              && feedSource != null
              && expectedFeedMass > 0L,
            "ANIMAL_CARE_FEED_SOURCE_PHYSICAL",
            $"item={feedItemId};spawned={feedSpawned};"
            + $"stack={feedSource?.StackId};mass={expectedFeedMass};"
            + $"before={feedQuantityBefore};after={CountFacilityItem(penId, feedItemId)}");
        if (!feedSpawned || feedSource == null || expectedFeedMass <= 0L)
        {
            yield break;
        }
        float feedHungerBefore = animal.Hunger;
        CircusSaveData circus = Clone(circusPersistence.Capture());
        circus.capturedWildlife.RemoveAll(item =>
            item != null && item.wildlifeId == animal.WildlifeId);
        circus.capturedWildlife.Add(new CapturedWildlifeState
        {
            wildlifeId = animal.WildlifeId,
            speciesId = animal.SpeciesId,
            penId = penId,
            penPosition = pen.centerPos,
            capturePosition = animal.GridPosition,
            transportState = CapturedWildlifeTransportState.Penned,
            isTamed = false,
            nextCareAt = 0f,
            lastCareStatus = "qa-penned"
        });
        Check(RestoreCircus(circus), "ANIMAL_CARE_CIRCUS_V18_RESTORE", animal.WildlifeId);

        string feedOperationId = CapturedWildlifeFeedOutbox.FormatOperationId(
            animal.WildlifeId,
            1);
        string expectedFeedCommitId =
            $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Sink}:"
            + $"{feedOperationId}:1:{expectedFeedMass}";
        bool feedCommitted = false;
        CapturedWildlifeState fedState = null;
        yield return WaitUntil(() =>
        {
            feedCommitted = capture.TryGetCaptured(animal.WildlifeId, out fedState)
                && fedState != null
                && fedState.nextFeedOperationSequence == 1
                && string.Equals(
                    animal.LastCaptiveFeedCommitId,
                    expectedFeedCommitId,
                    StringComparison.Ordinal)
                && !CapturedWildlifeFeedOutbox.HasPending(fedState);
            return feedCommitted;
        }, 5f);
        int feedQuantityAfter = CountFacilityItem(penId, feedItemId);
        float expectedHungerAfter = Mathf.Clamp01(feedHungerBefore - 0.72f);
        bool physicalSinkExact = feedCommitted
            && feedQuantityAfter == feedQuantityBefore
            && !batchDispositions.TryGetPending(feedOperationId, out _)
            && Mathf.Abs(animal.Hunger - expectedHungerAfter) <= 0.03f;
        Check(physicalSinkExact,
            "ANIMAL_CARE_FEED_SINK_EXACT",
            $"item={feedItemId};quantity={feedQuantityBefore + 1}->{feedQuantityAfter};"
            + $"hunger={feedHungerBefore:0.####}->{animal.Hunger:0.####};"
            + $"expected={expectedHungerAfter:0.####};commit={animal.LastCaptiveFeedCommitId};"
            + $"pending={batchDispositions.TryGetPending(feedOperationId, out _)}");
        bool outboxClean = feedCommitted
            && fedState.nextFeedOperationSequence == 1
            && CapturedWildlifeFeedOutbox.HasEmptyProvenance(fedState)
            && string.Equals(fedState.lastFeedItemId, feedItemId, StringComparison.Ordinal)
            && Mathf.Approximately(fedState.lastFeedDiseaseChance, 0f);
        Check(outboxClean,
            "ANIMAL_CARE_FEED_OUTBOX_CLEAN",
            $"sequence={fedState?.nextFeedOperationSequence};"
            + $"phase={fedState?.pendingFeedPhase};item={fedState?.lastFeedItemId};"
            + $"disease={fedState?.lastFeedDiseaseChance:0.####}");
        CapturedWildlifeState savedFeedState = circusPersistence.Capture()
            .capturedWildlife
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.wildlifeId,
                    animal.WildlifeId,
                    StringComparison.Ordinal));
        WildlifeSaveData savedFeedActor = animal.Capture();
        Check(savedFeedState != null
              && savedFeedState.nextFeedOperationSequence == 1
              && CapturedWildlifeFeedOutbox.HasEmptyProvenance(savedFeedState)
              && string.Equals(
                  savedFeedActor.lastCaptiveFeedCommitId,
                  expectedFeedCommitId,
                  StringComparison.Ordinal),
            "ANIMAL_CARE_FEED_SAVE_EXACT",
            $"state={savedFeedState != null};"
            + $"sequence={savedFeedState?.nextFeedOperationSequence};"
            + $"phase={savedFeedState?.pendingFeedPhase};"
            + $"actorCommit={savedFeedActor.lastCaptiveFeedCommitId}");
        if (!physicalSinkExact || !outboxClean || savedFeedState == null)
        {
            yield break;
        }

        DungeonAnimalHusbandrySaveData husbandry = new();
        husbandry.penPolicies.Add(new AnimalPenPolicySaveData
        {
            penBuildingInstanceId = penId,
            allowedSpeciesDefinitionIds = new List<string>
            {
                animal.SpeciesId
            }
        });
        husbandry.animals.Add(new HusbandryAnimalSaveData
        {
            animalInstanceId = animal.WildlifeId,
            speciesDefinitionId = animal.SpeciesId,
            penBuildingInstanceId = penId,
            sex = AnimalSex.Female,
            ageDays = 20f,
            tamed = false,
            tamingProgress = 0f,
            pendingWorkKind = AnimalHusbandryWorkKind.Tame,
            pendingWorkCompleted = 0f,
            statusCode = AnimalHusbandryStatusCode.AwaitingTaming
        });
        int candidateRevisionBeforeRestore =
            facilityCandidateCache.DynamicStateVersion;
        Check(RestoreHusbandry(husbandry), "ANIMAL_CARE_V18_RESTORE", animal.WildlifeId);
        Check(facilityCandidateCache.DynamicStateVersion
              > candidateRevisionBeforeRestore,
            "ANIMAL_CARE_CANDIDATE_CACHE_INVALIDATED",
            $"dynamicRevision={candidateRevisionBeforeRestore}"
            + $"->{facilityCandidateCache.DynamicStateVersion}");

        worker.SetLifecycleState(CharacterLifecycleState.Active);
        worker.transform.position = grid.GetWorldPos(workerCell);
        workerWork.SetWorkPriority(BuiltInWorkTypeIds.AnimalCare, WorkPriorityLevel.Priority1);
        FacilityWorkType animalCareLegacyType =
            FacilityWorkTypeMap.GetRequired(BuiltInWorkTypeIds.AnimalCare);
        int candidateIndexStableFrames = 0;
        int candidateIndexWaitFrames = 0;
        bool candidateIndexPending = true;
        bool candidateIndexContainsPen = false;
        float candidateIndexDeadline = Time.realtimeSinceStartup + 8f;
        while (candidateIndexStableFrames < 2
               && Time.realtimeSinceStartup < candidateIndexDeadline)
        {
            candidateIndexWaitFrames++;
            candidateIndexPending = facilityCandidateCache.HasPendingIndexBuild;
            candidateIndexContainsPen = facilityCandidateCache
                .GetWorkCandidates(grid, animalCareLegacyType)
                .Contains(pen);
            candidateIndexStableFrames = !candidateIndexPending
                && candidateIndexContainsPen
                    ? candidateIndexStableFrames + 1
                    : 0;
            if (candidateIndexStableFrames < 2)
            {
                yield return null;
            }
        }
        Check(candidateIndexStableFrames >= 2,
            "ANIMAL_CARE_CANDIDATE_INDEX_READY",
            $"stable={candidateIndexStableFrames}/2;frames={candidateIndexWaitFrames};"
            + $"pending={candidateIndexPending};containsPen={candidateIndexContainsPen};"
            + $"revision={facilityCandidateCache.CandidateIndexVersion};"
            + $"dynamic={facilityCandidateCache.DynamicStateVersion}");
        if (candidateIndexStableFrames < 2)
        {
            yield break;
        }
        AnimalCareAiPreflightSnapshot preflight =
            AnimalCareAiPreflight.Capture(
                worker,
                workerWork,
                pen,
                husbandryQuery,
                workPolicyRegistry,
                facilityCandidateCache);
        string preflightDetail = preflight.Format();
        Check(preflight.AuthoredWorkType,
            "ANIMAL_CARE_PREFLIGHT_AUTHORED_WORK_TYPE",
            preflightDetail);
        Check(preflight.HusbandryAvailable,
            "ANIMAL_CARE_PREFLIGHT_HUSBANDRY_QUERY",
            preflightDetail);
        Check(preflight.SearchAvailable && preflight.WorkAccessAvailable,
            "ANIMAL_CARE_PREFLIGHT_WORK_ACCESS_PATH",
            preflightDetail);
        Check(preflight.PolicyAvailable,
            "ANIMAL_CARE_PREFLIGHT_WORK_POLICY",
            preflightDetail);
        Check(preflight.CandidateAvailable && preflight.CandidateTargetsPen,
            "ANIMAL_CARE_PREFLIGHT_TARGET_SELECTOR",
            preflightDetail);
        Check(preflight.AiWorkCatalogAvailable,
            "ANIMAL_CARE_PREFLIGHT_AIWORK_CATALOG",
            preflightDetail);
        Check(preflight.Passed,
            "ANIMAL_CARE_PREFLIGHT_EARLIEST_AUTHORITY",
            preflightDetail);
        if (!preflight.Passed)
        {
            yield break;
        }
        EmergencyReserveSnapshot ledgerBefore = emergencyAccounting.CaptureSnapshot();
        AnimalCareStartObservation initialStart = new();
        yield return ObserveAnimalCareStart(initialStart, "qa-animal-care");
        Check(initialStart.Neutralized,
            "ANIMAL_CARE_SUBJECT_NEUTRALIZED",
            initialStart.NeutralizationDetail);
        Check(initialStart.PriorityAccepted
              && initialStart.PriorityTargetMatches
              && initialStart.PriorityWorkTypeMatches
              && !initialStart.AutonomousHaulCanStart,
            "ANIMAL_CARE_PRIORITY_SUPPRESSES_AUTONOMOUS_HAUL",
            initialStart.PriorityArbitrationDetail);
        Check(initialStart.PreferredDeferredCount == 0
              || (initialStart.PreferredDeferredFallbackSuppressions
                      == initialStart.PreferredDeferredCount
                  && initialStart.Started),
            "ANIMAL_CARE_PREFERRED_DEFERRED_OWNERSHIP",
            initialStart.PriorityArbitrationDetail);
        Check(initialStart.PreferredCommitCount == 1
              && initialStart.PreferredDisposition
                  == CharacterAiPreferredActionDisposition.Selected
              && initialStart.PreferredDispositionBranch
                  == CharacterAiBranch.Work
              && initialStart.AiWorkEvaluationObserved
              && initialStart.Started,
            "ANIMAL_CARE_PREFERRED_BT_ARBITRATION",
            initialStart.PriorityArbitrationDetail);
        Check(initialStart.UnexpectedIntermediateEpochCount == 0,
            "ANIMAL_CARE_NO_INTERMEDIATE_FALLBACK_COMMIT",
            initialStart.EpochTransitions);
        Check(initialStart.Started,
            "ANIMAL_CARE_BRAIN_AIWORK_STARTED",
            initialStart.Detail);
        if (!initialStart.Started)
        {
            yield break;
        }

        AbilityMove animalCareMove = worker.GetComponent<AbilityMove>();
        AIAction animalCareAction = worker.Brain.bestAction;
        Vector2Int progressStartGrid = worker.GetNowXY();
        Vector3 progressStartWorld = worker.transform.position;
        float observationSeconds = CalculateBoundedAnimalCareObservationSeconds(
            animalCareAction,
            grid.GetWorldPos(animal.GridPosition));
        float progressDeadline = Time.realtimeSinceStartup + observationSeconds;
        float lastObservedProgressAt = Time.realtimeSinceStartup;
        Vector3 lastObservedWorld = progressStartWorld;
        long lastGameplayProgress = worker.Brain
            .CaptureRuntimeGateSnapshot()
            .GameplayProgressRevision;
        bool movementOrGameplayAdvanced = false;
        bool stalled = false;
        float progress = 0f;
        while (Time.realtimeSinceStartup < progressDeadline)
        {
            husbandryQuery.TryGetAnimal(
                new WildlifeInstanceId(animal.WildlifeId),
                out HusbandryAnimalState state);
            progress = state?.TamingProgress ?? 0f;
            if (progress > 0f) break;

            CharacterAiRuntimeGateSnapshot progressGate = worker.Brain
                .CaptureRuntimeGateSnapshot();
            Vector3 currentWorld = worker.transform.position;
            bool positionAdvanced =
                (currentWorld - lastObservedWorld).sqrMagnitude > 0.0001f;
            bool gameplayAdvanced =
                progressGate.GameplayProgressRevision > lastGameplayProgress;
            if (positionAdvanced || gameplayAdvanced)
            {
                movementOrGameplayAdvanced = true;
                lastObservedProgressAt = Time.realtimeSinceStartup;
                lastObservedWorld = currentWorld;
                lastGameplayProgress = progressGate.GameplayProgressRevision;
            }
            else if (Time.realtimeSinceStartup - lastObservedProgressAt >= 5f)
            {
                stalled = true;
                break;
            }
            yield return null;
        }
        Vector2Int progressEndGrid = worker.GetNowXY();
        Vector3 progressEndWorld = worker.transform.position;
        Check(progress > 0f && !stalled,
            "ANIMAL_CARE_PROGRESS",
            $"progress={progress:0.####};eta={observationSeconds:0.##}s;"
            + $"startGrid={progressStartGrid};endGrid={progressEndGrid};"
            + $"startWorld={progressStartWorld};endWorld={progressEndWorld};"
            + $"advanced={movementOrGameplayAdvanced};stalled={stalled};"
            + $"phase={worker.Brain.CurrentActionPhase};"
            + $"movement={animalCareMove?.LastGridMoveFailureReason}");

        int releaseAttemptsBefore = worker
            .TransientAiOwnershipReleaseAttemptCountForDiagnostics;
        int repeatedWorkCleanupBefore = worker
            .RepeatedWorkOwnershipCleanupCountForDiagnostics;
        long lifecycleTransitionBefore = worker.Lifecycle
            .LifecycleTransitionRevisionForDiagnostics;
        CharacterAiRuntimeGateSnapshot lifecycleGateBefore = worker.Brain
            .CaptureRuntimeGateSnapshot();
        CharacterBodyHealthSnapshot originalBody = CloneBodyHealthSnapshot(
            bodyHealthQuery.GetSnapshot(worker));
        List<CharacterBodyPartHealthState> downedParts = originalBody.Parts
            .Select(CloneBodyPart)
            .ToList();
        int injuredLegCount = 0;
        foreach (CharacterBodyPartHealthState part in downedParts)
        {
            if (part.bodyPart is not CombatBodyPart.LeftLeg
                and not CombatBodyPart.RightLeg)
            {
                continue;
            }

            part.currentHealth = Mathf.Max(0.5f, part.maxHealth * 0.18f);
            injuredLegCount++;
        }
        Check(injuredLegCount == 2,
            "ANIMAL_CARE_DOWNED_BODY_FIXTURE",
            $"legs={injuredLegCount};parts={downedParts.Count};"
            + $"beforeDowned={originalBody.Downed};"
            + $"beforeMobility={originalBody.Mobility:0.###}");
        if (injuredLegCount != 2)
        {
            yield break;
        }

        bodyHealthCommands.ApplySnapshot(
            worker,
            new CharacterBodyHealthSnapshot(
                downedParts,
                originalBody.BloodLoss,
                originalBody.Suppression,
                consciousness: 1f,
                manipulation: 1f,
                mobility: 0.08f,
                downed: true),
            "qa-animal-care-downed");
        CharacterBodyHealthSnapshot synchronousBody = bodyHealthQuery
            .GetSnapshot(worker);
        CharacterMedicalOrder synchronousOrder = medicalQuery.ActiveOrders
            .FirstOrDefault(order => order != null
                && order.IsActive
                && string.Equals(
                    order.patientId,
                    worker.Identity?.PersistentId,
                    StringComparison.Ordinal));
        bool synchronousCleanup = synchronousBody.Downed
            && worker.CurrentLifecycleState == CharacterLifecycleState.Downed
            && synchronousOrder != null
            && !workerWork.isWorking
            && !workerWork.HasActiveWorkRoutineForDiagnostics
            && pen.WorkerReservation == null
            && pen.CurrentUserCount == 0;
        yield return null;
        yield return null;
        husbandryQuery.TryGetAnimal(
            new WildlifeInstanceId(animal.WildlifeId),
            out HusbandryAnimalState afterDowned);
        float frozen = afterDowned?.TamingProgress ?? 0f;
        for (int frame = 0; frame < 4; frame++) yield return null;
        husbandryQuery.TryGetAnimal(
            new WildlifeInstanceId(animal.WildlifeId),
            out HusbandryAnimalState afterDelay);
        float delayed = afterDelay?.TamingProgress ?? 0f;
        CharacterAiRuntimeGateSnapshot gate = worker.Brain.CaptureRuntimeGateSnapshot();
        CharacterBodyHealthSnapshot stableDownedBody = bodyHealthQuery
            .GetSnapshot(worker);
        CharacterMedicalOrder stableDownedOrder = medicalQuery.ActiveOrders
            .FirstOrDefault(order => order != null
                && order.IsActive
                && string.Equals(
                    order.patientId,
                    worker.Identity?.PersistentId,
                    StringComparison.Ordinal));
        bool typedCancellation = worker.Brain.CaptureRuntimeDiagnostics()
            .TryGetActionTerminal(
                initialStart.Epoch,
                out CharacterAiActionTerminalKind downedTerminal)
            && downedTerminal == CharacterAiActionTerminalKind.Cancelled;
        EmergencyReserveSnapshot ledgerAfter = emergencyAccounting.CaptureSnapshot();
        Check(!workerWork.isWorking
              && !workerWork.HasActiveWorkRoutineForDiagnostics
              && synchronousCleanup
              && stableDownedBody.Downed
              && worker.CurrentLifecycleState == CharacterLifecycleState.Downed
              && stableDownedOrder != null
              && Mathf.Approximately(frozen, delayed)
              && pen.WorkerReservation == null
              && pen.CurrentUserCount == 0
              && gate.LivePathRequests == 0
              && gate.LiveReservations == 0
              && gate.ActionTerminals == lifecycleGateBefore.ActionTerminals + 1
              && typedCancellation
              && worker.TransientAiOwnershipReleaseAttemptCountForDiagnostics
                  == releaseAttemptsBefore + 1
              && worker.TransientAiOwnershipReleasedForDiagnostics
              && worker.Lifecycle.LifecycleTransitionRevisionForDiagnostics
                  == lifecycleTransitionBefore + 1
              && ledgerAfter.ActiveOperationCount <= ledgerBefore.ActiveOperationCount,
            "ANIMAL_CARE_DOWNED_EXACT_CLEANUP",
            $"synchronous={synchronousCleanup};"
            + $"bodyDowned={synchronousBody.Downed}->{stableDownedBody.Downed};"
            + $"mobility={synchronousBody.Mobility:0.###}"
            + $"->{stableDownedBody.Mobility:0.###};"
            + $"medicalOrder={synchronousOrder?.orderId}"
            + $"->{stableDownedOrder?.orderId};"
            + $"working={workerWork.isWorking};"
            + $"routine={workerWork.HasActiveWorkRoutineForDiagnostics};"
            + $"progress={frozen:0.####}->{delayed:0.####};"
            + $"worker={pen.WorkerReservation};users={pen.CurrentUserCount};"
            + $"paths={gate.LivePathRequests};reservations={gate.LiveReservations};"
            + $"actionTerminals={lifecycleGateBefore.ActionTerminals}"
            + $"->{gate.ActionTerminals};typedTerminal={downedTerminal};"
            + $"ledger={ledgerBefore.ActiveOperationCount}"
            + $"->{ledgerAfter.ActiveOperationCount};"
            + "releaseAttempts="
            + $"{releaseAttemptsBefore}"
            + $"->{worker.TransientAiOwnershipReleaseAttemptCountForDiagnostics};"
            + "repeatedWorkCleanup="
            + $"{repeatedWorkCleanupBefore}"
            + $"->{worker.RepeatedWorkOwnershipCleanupCountForDiagnostics};"
            + "releaseReason="
            + $"{worker.LastTransientAiOwnershipReleaseReasonForDiagnostics};"
            + $"released={worker.TransientAiOwnershipReleasedForDiagnostics};"
            + $"lifecycle={worker.CurrentLifecycleState};"
            + "transitionRevision="
            + $"{lifecycleTransitionBefore}"
            + $"->{worker.Lifecycle.LifecycleTransitionRevisionForDiagnostics};"
            + "transition="
            + $"{worker.Lifecycle.LastTransitionPreviousStateForDiagnostics}"
            + $"->{worker.Lifecycle.LastTransitionNextStateForDiagnostics};"
            + "transitionInProgress="
            + $"{worker.Lifecycle.LifecycleTransitionInProgressForDiagnostics}");

        worker.SetAiPaused(true);
        workerWork.ClearPriorityWorkTarget();
        bodyHealthCommands.ApplySnapshot(
            worker,
            originalBody,
            "qa-animal-care-recovered");
        yield return null;
        yield return null;
        CharacterBodyHealthSnapshot recoveredBody = bodyHealthQuery
            .GetSnapshot(worker);
        bool medicalOrderReleased = !medicalQuery.ActiveOrders.Any(order =>
            order != null
            && order.IsActive
            && string.Equals(
                order.patientId,
                worker.Identity?.PersistentId,
                StringComparison.Ordinal));
        Check(!recoveredBody.Downed
              && worker.CurrentLifecycleState == CharacterLifecycleState.Active
              && medicalOrderReleased
              && !workerWork.isWorking
              && !workerWork.HasActiveWorkRoutineForDiagnostics,
            "ANIMAL_CARE_DOWNED_BODY_RECOVERED",
            $"bodyDowned={recoveredBody.Downed};"
            + $"mobility={recoveredBody.Mobility:0.###};"
            + $"lifecycle={worker.CurrentLifecycleState};"
            + $"medicalReleased={medicalOrderReleased};"
            + $"paused={worker.IsAiPaused()};"
            + $"working={workerWork.isWorking};"
            + $"routine={workerWork.HasActiveWorkRoutineForDiagnostics}");
        Check(RestoreHusbandry(husbandry),
            "ANIMAL_CARE_TARGET_LOSS_RESTORE", animal.WildlifeId);
        workerWork.SetWorkPriority(BuiltInWorkTypeIds.AnimalCare, WorkPriorityLevel.Priority1);
        AnimalCareStartObservation targetLossStart = new();
        yield return ObserveAnimalCareStart(
            targetLossStart,
            "qa-animal-care-target-loss");
        Check(targetLossStart.Neutralized,
            "ANIMAL_CARE_TARGET_LOSS_SUBJECT_NEUTRALIZED",
            targetLossStart.NeutralizationDetail);
        bool targetRunStarted = targetLossStart.Started;
        pen.DestroySelf();
        yield return null;
        yield return null;
        gate = worker.Brain.CaptureRuntimeGateSnapshot();
        Check(targetRunStarted
              && !workerWork.isWorking
              && !workerWork.HasActiveWorkRoutineForDiagnostics
              && gate.LivePathRequests == 0
              && gate.LiveReservations == 0,
            "ANIMAL_CARE_PEN_DESTROY_EXACT_CLEANUP",
            $"started={targetRunStarted};working={workerWork.isWorking};routine={workerWork.HasActiveWorkRoutineForDiagnostics};paths={gate.LivePathRequests};reservations={gate.LiveReservations}");
    }

    private IEnumerator ObserveAnimalCareStart(
        AnimalCareStartObservation observation,
        string resetReason)
    {
        AIBrain brain = worker.Brain;
        AbilityHaul haul = worker.GetComponent<AbilityHaul>();
        AbilityMove move = worker.GetComponent<AbilityMove>();

        // Preparation must be atomic with respect to the production scheduler.
        // Pausing first prevents a just-cancelled haul from being selected again
        // between StopCurrentActionForReplan and the AnimalCare preference.
        worker.SetAiPaused(true);
        brain.StopCurrentActionForReplan(resetReason);
        haul?.StopHauling(resetReason);
        move?.CancelActiveMovement(resetReason);
        workerWork.ClearPriorityWorkTarget();

        observation.Neutralized = TryNeutralizeAnimalCareSubject(
            out string neutralizationDetail);
        observation.NeutralizationDetail = neutralizationDetail;
        if (!observation.Neutralized)
        {
            observation.Detail = "neutralized=false;" + neutralizationDetail;
            yield break;
        }

        float settleDeadline = Time.realtimeSinceStartup + 3f;
        int consecutiveSettledFrames = 0;
        do
        {
            observation.SettleFrames++;
            yield return null;
            CharacterAiRuntimeGateSnapshot settleGate =
                brain.CaptureRuntimeGateSnapshot();
            bool cleanFrame = brain.bestAction == null
                && !brain.IsExternallyDrivenActionActive
                && haul?.IsHauling != true
                && !workerWork.isWorking
                && !workerWork.HasActiveWorkRoutineForDiagnostics
                && move?.HasActiveMovementRoutineForDiagnostics != true
                && settleGate.LivePathRequests == 0
                && settleGate.LiveReservations == 0;
            consecutiveSettledFrames = cleanFrame
                ? consecutiveSettledFrames + 1
                : 0;
            observation.SettleConsecutiveFrames =
                consecutiveSettledFrames;
            observation.Settled = consecutiveSettledFrames >= 2;
        }
        while (!observation.Settled
               && Time.realtimeSinceStartup < settleDeadline);

        if (!observation.Settled)
        {
            CharacterAiRuntimeGateSnapshot unsettledGate =
                brain.CaptureRuntimeGateSnapshot();
            observation.Detail =
                $"settled=false;settleFrames={observation.SettleFrames};"
                + $"consecutive={observation.SettleConsecutiveFrames}/2;"
                + $"action={brain.bestAction?.actionset?.GetType().Name};"
                + $"external={brain.IsExternallyDrivenActionActive};"
                + $"hauling={haul?.IsHauling};working={workerWork.isWorking};"
                + $"workRoutine={workerWork.HasActiveWorkRoutineForDiagnostics};"
                + $"moveRoutine={move?.HasActiveMovementRoutineForDiagnostics};"
                + $"paths={unsettledGate.LivePathRequests};"
                + $"reservations={unsettledGate.LiveReservations};"
                + $"phase={brain.CurrentActionPhase};failure={brain.LastActionFailure}";
            yield break;
        }

        GridPathSearchResult prioritySearch = grid.SearchPath(worker.GetNowXY());
        observation.PriorityAccepted = workerWork.TrySetPriorityWorkTarget(
            pen,
            BuiltInWorkTypeIds.AnimalCare,
            prioritySearch,
            out string priorityFailure);
        if (!observation.PriorityAccepted)
        {
            WorkTargetCandidate firstRejected =
                workerWork.LastRejectedWorkCandidate;
            // Exterior/environment warnings use the same two-step public
            // confirmation handshake as the player priority-work command UI.
            observation.PriorityAccepted = workerWork.TrySetPriorityWorkTarget(
                pen,
                BuiltInWorkTypeIds.AnimalCare,
                prioritySearch,
                out string confirmedFailure);
            observation.PriorityDetail = observation.PriorityAccepted
                ? "confirmed-after-warning:" + priorityFailure
                    + ";firstRejected=" + firstRejected.FailureReason
                : "rejected:first=" + priorityFailure
                    + ";firstRejected=" + firstRejected.FailureReason
                    + ";confirmed=" + confirmedFailure
                    + ";confirmedRejected="
                    + workerWork.LastRejectedWorkCandidate.FailureReason;
        }
        else
        {
            observation.PriorityDetail = "accepted";
        }

        if (!observation.PriorityAccepted)
        {
            observation.Detail =
                $"settled=true;priorityAccepted=false;"
                + $"priority={observation.PriorityDetail};"
                + $"pen={pen?.RequirePersistentInstanceId().Value};"
                + $"actor={worker.GetNowXY()};phase={brain.CurrentActionPhase};"
                + $"failure={brain.LastActionFailure}";
            yield break;
        }

        observation.PriorityTargetMatches =
            workerWork.PriorityWorkTarget == pen;
        observation.PriorityWorkTypeMatches =
            workerWork.PriorityWorkTypeId == BuiltInWorkTypeIds.AnimalCare;
        observation.UrgentPriority = workerWork.HasUrgentPriorityWork;
        // Register the same production preference that the upcoming scheduler
        // decision will consume before sampling AIWork.  This keeps the
        // diagnostic on the exact AnimalCare path rather than the generic
        // fallback action.
        observation.Preferred = brain.PreferWorkActionOnNextDecision(
            BuiltInWorkTypeIds.AnimalCare,
            120f);
        AIAction haulCandidate = brain.availableActions?.FirstOrDefault(
            candidate => candidate?.actionset is AIHaul);
        observation.AutonomousHaulCanStart =
            haulCandidate?.actionset?.CanStart(worker) == true;
        AIAction aiWorkCandidate = brain.availableActions?.FirstOrDefault(
            candidate => candidate?.actionset is AIWork);
        WorkDutyStartDiagnostics workStartGates =
            workerWork.CaptureWorkStartDiagnostics();
        observation.AbilityCanStartAny =
            workStartGates.CanStartByDutyGate;
        observation.AbilityCanStartAnimalCare =
            workStartGates.CanStartByDutyGate
            && observation.PriorityTargetMatches
            && observation.PriorityWorkTypeMatches;
        observation.DutyState = workStartGates.DutyState;
        observation.RoutineNeedBlocked = workStartGates.RoutineNeedBlocked;
        observation.RestProtection = workStartGates.RestProtectionBlocked;
        observation.DiscontentBlocked = workStartGates.DiscontentBlocked;
        observation.DiscontentReason = workStartGates.DiscontentReason;
        observation.ConditionWouldTakeOffDuty =
            workStartGates.ConditionWouldTakeOffDuty;
        observation.DutyGateCanStart = workStartGates.CanStartByDutyGate;
        observation.PriorityArbitrationDetail =
            $"targetMatch={observation.PriorityTargetMatches};"
            + $"workTypeMatch={observation.PriorityWorkTypeMatches};"
            + $"priorityType={workerWork.PriorityWorkTypeId.Value};"
            + $"urgent={observation.UrgentPriority};"
            + $"haulCanStart={observation.AutonomousHaulCanStart};"
            + $"abilityAny={observation.AbilityCanStartAny};"
            + $"abilityAnimalCare={observation.AbilityCanStartAnimalCare};"
            + $"duty={observation.DutyState};"
            + $"routineNeedBlock={observation.RoutineNeedBlocked};"
            + $"restProtection={observation.RestProtection};"
            + $"discontent={observation.DiscontentBlocked}:"
            + $"{observation.DiscontentReason};"
            + $"conditionOffDuty={observation.ConditionWouldTakeOffDuty};"
            + $"dutyGateCanStart={observation.DutyGateCanStart};"
            + $"haulPriority={workerWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul)};"
            + $"target={workerWork.PriorityWorkTarget?.name};"
            + $"pen={pen?.name}";

        CharacterAiRuntimeDiagnosticsSnapshot before =
            brain.CaptureRuntimeDiagnostics();
        long startEpoch = brain.RuntimeActionEpoch;
        int wakeRequestsBefore = brain.ImmediateDecisionRequestCount;
        long committedDeferralsBefore =
            brain.RuntimeCommittedPathSearchDeferralCount;
        long preferredDeferralsBefore =
            brain.RuntimePreferredActionDeferredCount;
        long preferredFallbackSuppressionsBefore =
            brain.RuntimePreferredActionDeferredFallbackSuppressionCount;
        long preferredCommitsBefore =
            brain.RuntimePreferredActionCommitCount;
        long preferredHardFailuresBefore =
            brain.RuntimePreferredActionHardFailureCount;
        long preferredDispositionRevisionBefore =
            brain.RuntimePreferredActionDispositionRevision;

        // Let the production scheduler own path-budget rollover. Calling the
        // decision tree directly every frame runs outside that ownership and
        // can indefinitely starve an otherwise valid Deferred AIWork action.
        // Register while paused, then publish the actor and wake exactly one
        // official scheduler decision. No other action can occupy this gap.
        worker.SetAiPaused(false);
        brain.RequestImmediateReplan(clearFailures: true);

        float deadline = Time.realtimeSinceStartup + 12f;
        long lastObservedEpoch = startEpoch;
        while (Time.realtimeSinceStartup < deadline)
        {
            observation.SchedulerFrames++;
            AIAction current = brain.bestAction;
            long currentEpoch = brain.RuntimeActionEpoch;
            if (currentEpoch != lastObservedEpoch)
            {
                string actionType = current?.actionset?.GetType().Name
                    ?? "<none>";
                bool targetEpoch = current?.actionset is AIWork
                    && workerWork.AssignedWorkTypeId
                        == BuiltInWorkTypeIds.AnimalCare;
                observation.EpochTransitionCount++;
                if (!targetEpoch)
                {
                    observation.UnexpectedIntermediateEpochCount++;
                }
                observation.EpochTransitions +=
                    $"[{lastObservedEpoch}->{currentEpoch}:"
                    + $"{actionType}:assigned="
                    + $"{workerWork.AssignedWorkTypeId.Value}]";
                lastObservedEpoch = currentEpoch;
            }
            // The catalog intentionally contains one generic AIWork action.
            // Its serialized WorkTypeId is empty; production resolves the
            // preferred type through AIBrain, so exactness lives on the
            // assigned AbilityWork state rather than that asset field.
            bool exactAnimalCare = current?.actionset is AIWork
                && workerWork.AssignedWorkTypeId
                    == BuiltInWorkTypeIds.AnimalCare;
            if (observation.Preferred
                && exactAnimalCare
                && workerWork.IsAssignedWork(BuiltInWorkTypeIds.AnimalCare)
                && brain.HasRunningWorkAction)
            {
                observation.Started = true;
                observation.Epoch = brain.RuntimeActionEpoch;
                break;
            }
            yield return null;
        }

        // Read the production evaluator's published result.  This is a
        // non-mutating observation of AIWork.CanStart/AdjustScore; invoking
        // those methods again from a verifier would alter duty/candidate
        // caches and could change the decision being measured.
        observation.AiWorkAdjustedScore = aiWorkCandidate?.score ?? 0f;
        observation.PreferredDeferredCount =
            brain.RuntimePreferredActionDeferredCount
            - preferredDeferralsBefore;
        observation.PreferredDeferredFallbackSuppressions =
            brain.RuntimePreferredActionDeferredFallbackSuppressionCount
            - preferredFallbackSuppressionsBefore;
        observation.PreferredDeferredFailure =
            brain.LastPreferredActionDeferredFailure.ToString();
        observation.PreferredCommitCount =
            brain.RuntimePreferredActionCommitCount
            - preferredCommitsBefore;
        observation.PreferredHardFailureCount =
            brain.RuntimePreferredActionHardFailureCount
            - preferredHardFailuresBefore;
        observation.FirstPreferredHardFailureKind =
            brain.FirstPreferredActionHardFailure.Kind;
        observation.FirstPreferredHardFailure =
            brain.FirstPreferredActionHardFailure.ToString();
        observation.FirstPreferredHardFailureSource =
            brain.FirstPreferredActionHardFailureSource;
        observation.PreferredDisposition =
            brain.RuntimePreferredActionDisposition;
        observation.PreferredDispositionBranch =
            brain.RuntimePreferredActionDispositionBranch;
        observation.PreferredDispositionTransitions =
            brain.RuntimePreferredActionDispositionRevision
            - preferredDispositionRevisionBefore;
        string aiWorkLabel = aiWorkCandidate?.actionset?.GetDisplayLabel()
            ?? string.Empty;
        IReadOnlyList<AIActionDebugCandidate> evaluated =
            brain.LastCandidateScores;
        for (int index = evaluated.Count - 1; index >= 0; index--)
        {
            AIActionDebugCandidate candidate = evaluated[index];
            if (!string.Equals(
                    candidate.ActionLabel,
                    aiWorkLabel,
                    StringComparison.Ordinal))
            {
                continue;
            }

            observation.AiWorkEvaluationObserved = true;
            observation.AiWorkCanStart = !candidate.Failure.HasFailure;
            observation.AiWorkFailure = candidate.Failure.ToString();
            break;
        }
        if (observation.PreferredCommitCount > 0
            && observation.Started)
        {
            observation.AiWorkEvaluationObserved = true;
            observation.AiWorkCanStart = true;
        }
        observation.PriorityArbitrationDetail +=
            $";aiWorkEvaluated={observation.AiWorkEvaluationObserved}"
            + $";aiWorkCanStart={observation.AiWorkCanStart}"
            + $";aiWorkAdjusted={observation.AiWorkAdjustedScore:0.###}"
            + $";aiWorkFailure={observation.AiWorkFailure}"
            + $";preferredDeferred={observation.PreferredDeferredCount}"
            + $";preferredFallbackSuppressed="
            + $"{observation.PreferredDeferredFallbackSuppressions}"
            + $";preferredCommits={observation.PreferredCommitCount}"
            + $";preferredHardFailures="
            + $"{observation.PreferredHardFailureCount}"
            + $";firstPreferredHardFailure="
            + $"{observation.FirstPreferredHardFailureSource}:"
            + $"{observation.FirstPreferredHardFailureKind}:"
            + $"{observation.FirstPreferredHardFailure}"
            + $";preferredDisposition={observation.PreferredDisposition}:"
            + $"{observation.PreferredDispositionBranch}"
            + $";preferredDispositionTransitions="
            + $"{observation.PreferredDispositionTransitions}"
            + $";preferredDeferredFailure="
            + $"{observation.PreferredDeferredFailure}";

        CharacterAiRuntimeDiagnosticsSnapshot after =
            brain.CaptureRuntimeDiagnostics();
        CharacterAiRuntimeGateSnapshot beforeGate = before.Gate;
        CharacterAiRuntimeGateSnapshot afterGate = after.Gate;
        observation.Detail =
            $"settled={observation.Settled};settleFrames={observation.SettleFrames};"
            + $"settleConsecutive={observation.SettleConsecutiveFrames}/2;"
            + $"priorityAccepted={observation.PriorityAccepted};"
            + $"priority={observation.PriorityDetail};"
            + $"priorityArbitration={observation.PriorityArbitrationDetail};"
            + $"preferred={observation.Preferred};started={observation.Started};"
            + $"epoch={startEpoch}->{brain.RuntimeActionEpoch};"
            + $"startedEpoch={observation.Epoch};"
            + $"epochTransitions={observation.EpochTransitionCount}:"
            + $"{observation.EpochTransitions};"
            + $"schedulerFrames={observation.SchedulerFrames};"
            + $"wakeRequests={wakeRequestsBefore}->{brain.ImmediateDecisionRequestCount};"
            + $"schedulerProcesses={beforeGate.SchedulerProcesses}->{afterGate.SchedulerProcesses};"
            + $"retrySchedules={beforeGate.RetrySchedules}->{afterGate.RetrySchedules};"
            + $"retryAttempts={beforeGate.RetryAttempts}->{afterGate.RetryAttempts};"
            + $"pathRequests={beforeGate.PathRequests}->{afterGate.PathRequests};"
            + $"pathResults={beforeGate.PathResults}->{afterGate.PathResults};"
            + $"committedDeferrals={committedDeferralsBefore}"
            + $"->{brain.RuntimeCommittedPathSearchDeferralCount};"
            + $"pathDeferred={brain.IsPathSearchDeferred};"
            + $"committedPathDeferred={brain.IsCommittedPathSearchDeferred};"
            + $"brokerSearches={pathSearchBroker?.SearchesThisFrame ?? -1};"
            + $"brokerDeferrals={pathSearchBroker?.BudgetDeferralsThisFrame ?? -1};"
            + $"preferredNow={brain.IsActionPreferredForNextDecision<AIWork>()};"
            + $"action={brain.bestAction?.actionset?.GetType().Name};"
            + $"assigned={workerWork.AssignedWorkTypeId.Value};"
            + $"phase={brain.CurrentActionPhase};"
            + $"phaseDetail={brain.CurrentActionPhaseDetail};"
            + $"failure={brain.LastActionFailure}";
    }

    private bool TryNeutralizeAnimalCareSubject(out string detail)
    {
        CharacterStats stats = worker?.Stats;
        if (stats == null || deprivationRuntime == null)
        {
            detail = $"stats={stats != null};deprivation={deprivationRuntime != null}";
            return false;
        }

        Dictionary<CharacterCondition, float> restoredStats =
            stats.StatSnapshot.ToDictionary(
                pair => pair.Key,
                pair => pair.Value);
        restoredStats[CharacterCondition.HUNGER] = 100f;
        restoredStats[CharacterCondition.THIRST] = 100f;
        restoredStats[CharacterCondition.SLEEP] = 100f;
        restoredStats[CharacterCondition.FUN] = 100f;
        restoredStats[CharacterCondition.MOOD] = 100f;
        restoredStats[CharacterCondition.EXCRETION] = 100f;
        restoredStats[CharacterCondition.HYGIENE] = 100f;
        stats.RestorePersistentState(
            restoredStats,
            worker.CurrentHealth,
            worker.InjurySeverity,
            100f,
            Array.Empty<CharacterMoodFactorSnapshot>());

        bool deprivationReset = deprivationRuntime
            .DebugResetForDeterministicScenario(worker);
        bool routineDrink = deprivationRuntime.NeedsRoutineDrink(
            worker,
            out string routineDrinkReason);
        bool drinkRunnerActive = deprivationRuntime
            .IsRoutineDrinkActionActive(worker);
        bool hungerEmergency = CharacterNeedAiThresholds
            .IsEmergencyOrImminentPhysicalHarm(
                worker,
                CharacterCondition.HUNGER);
        bool thirstEmergency = CharacterNeedAiThresholds
            .IsEmergencyOrImminentPhysicalHarm(
                worker,
                CharacterCondition.THIRST);
        float effectiveMood = CharacterMoodImpulseUtility.GetMood01(worker);
        bool neutral = deprivationReset
            && !routineDrink
            && !drinkRunnerActive
            && !hungerEmergency
            && !thirstEmergency
            && effectiveMood >= 0.9f;

        detail = $"reset={deprivationReset};"
            + $"needs={DescribeAnimalCareNeeds(stats)};"
            + $"routineDrink={routineDrink}:{routineDrinkReason};"
            + $"drinkRunner={drinkRunnerActive};"
            + $"hungerEmergency={hungerEmergency};"
            + $"thirstEmergency={thirstEmergency};"
            + $"effectiveMood={effectiveMood:0.###};"
            + $"paused={worker.IsAiPaused()};"
            + $"action={worker.Brain?.bestAction?.actionset?.GetType().Name ?? "<none>"}";
        return neutral;
    }

    private static string DescribeAnimalCareNeeds(CharacterStats stats)
    {
        if (stats == null)
        {
            return "missing";
        }

        CharacterCondition[] conditions =
        {
            CharacterCondition.HUNGER,
            CharacterCondition.THIRST,
            CharacterCondition.SLEEP,
            CharacterCondition.FUN,
            CharacterCondition.MOOD,
            CharacterCondition.EXCRETION,
            CharacterCondition.HYGIENE
        };
        return string.Join(",", conditions.Select(condition =>
            stats.TryGetConditionValue(condition, out float value)
                ? $"{condition}={value:0.###}"
                : $"{condition}=missing"));
    }

    private static CharacterBodyHealthSnapshot CloneBodyHealthSnapshot(
        CharacterBodyHealthSnapshot source) =>
        new(
            source.Parts.Select(CloneBodyPart).ToArray(),
            source.BloodLoss,
            source.Suppression,
            source.Consciousness,
            source.Manipulation,
            source.Mobility,
            source.Downed);

    private static CharacterBodyPartHealthState CloneBodyPart(
        CharacterBodyPartHealthState source) =>
        source == null
            ? null
            : new CharacterBodyPartHealthState
            {
                bodyPart = source.bodyPart,
                maxHealth = source.maxHealth,
                currentHealth = source.currentHealth,
                bleedingPerSecond = source.bleedingPerSecond
            };

    private float CalculateBoundedAnimalCareObservationSeconds(
        AIAction action,
        Vector3 fallbackTargetWorld)
    {
        float worldDistance = 0f;
        int pathStepCount = action?.pathSteps?.Count ?? 0;
        Vector3 previous = worker.transform.position;
        if (pathStepCount > 0)
        {
            for (int index = 0; index < pathStepCount; index++)
            {
                Vector3 next = grid.GetWorldPos(action.pathSteps[index].To);
                worldDistance += Vector3.Distance(previous, next);
                previous = next;
            }
        }
        else
        {
            worldDistance = Vector3.Distance(previous, fallbackTargetWorld);
        }

        float gameSeconds = worldDistance
            / Mathf.Max(0.1f, worker.GetMoveSpeed());
        float realSeconds = gameSeconds / Mathf.Max(0.01f, Time.timeScale);
        float observationSeconds = 4f + realSeconds * 2f
            + pathStepCount * 0.1f;
        return Mathf.Clamp(observationSeconds, 8f, 30f);
    }

    private sealed class AnimalCareStartObservation
    {
        public bool Preferred;
        public bool Started;
        public bool Settled;
        public bool Neutralized;
        public bool PriorityAccepted;
        public bool PriorityTargetMatches;
        public bool PriorityWorkTypeMatches;
        public bool UrgentPriority;
        public bool AutonomousHaulCanStart;
        public bool AiWorkCanStart;
        public bool AbilityCanStartAny;
        public bool AbilityCanStartAnimalCare;
        public bool AiWorkEvaluationObserved;
        public bool RoutineNeedBlocked;
        public bool RestProtection;
        public bool DiscontentBlocked;
        public bool ConditionWouldTakeOffDuty;
        public bool DutyGateCanStart;
        public float AiWorkAdjustedScore;
        public AbilityWork.DutyState DutyState;
        public string DiscontentReason = string.Empty;
        public string AiWorkFailure = string.Empty;
        public string PreferredDeferredFailure = string.Empty;
        public string FirstPreferredHardFailure = string.Empty;
        public int SettleFrames;
        public int SettleConsecutiveFrames;
        public int SchedulerFrames;
        public int EpochTransitionCount;
        public int UnexpectedIntermediateEpochCount;
        public long Epoch;
        public long PreferredDeferredCount;
        public long PreferredDeferredFallbackSuppressions;
        public long PreferredCommitCount;
        public long PreferredHardFailureCount;
        public long PreferredDispositionTransitions;
        public CharacterAiPreferredActionDisposition PreferredDisposition;
        public CharacterAiPreferredActionFailureSource
            FirstPreferredHardFailureSource;
        public AIActionFailureKind FirstPreferredHardFailureKind;
        public CharacterAiBranch PreferredDispositionBranch;
        public string EpochTransitions = string.Empty;
        public string PriorityDetail = string.Empty;
        public string PriorityArbitrationDetail = string.Empty;
        public string Detail = string.Empty;
        public string NeutralizationDetail = string.Empty;
    }

    private sealed class PickupDeliveryPreflightProbe
    {
        public bool Ready;
        public Vector2Int ApproachStart;
        public Vector2Int PickupStand;
        public Vector2Int DeliveryStand;
        public int PathStepCount;
        public float PathWorldDistance;
        public string Detail = string.Empty;
    }

    private sealed class TransportTerminalObservation
    {
        public bool Penned;
        public bool Stalled;
        public bool AwaitingTransportObserved;
        public bool PickupApproachProgressObserved;
        public bool PickupStandReached;
        public bool TransportingObserved;
        public float ObservationSeconds;
        public float AllowedSeconds;
        public float SecondsSinceProgress;
        public Vector3 LastProgressWorldPosition;
        public Vector2Int LastProgressGridPosition;
        public int ProgressSamples;
    }

    private sealed class TransportFixturePlanProbe
    {
        public bool Ready;
        public Vector2Int SourceCell;
        public Vector2Int PickupStand;
        public Vector2Int DeliveryStand;
        public string Detail = string.Empty;
    }

    private sealed class TransportChaosStartProbe
    {
        public bool Live;
        public WildlifeActor Animal;
        public Transform OriginalParent;
        public Vector2Int Source;
        public CapturedWildlifeState State;
        public AbilityWildlifeCaptureTransport Transport;
        public AbilityMove Move;
        public string ExpectedOwner = string.Empty;
        public int TerminalBefore;
        public int PathStepCount;
        public float PathWorldDistance;
    }

    private readonly struct ActorAiPauseState
    {
        public ActorAiPauseState(CharacterActor actor, bool wasPaused)
        {
            Actor = actor;
            WasPaused = wasPaused;
        }

        public CharacterActor Actor { get; }
        public bool WasPaused { get; }
    }

    private bool CreateAuthoredRoom()
    {
        const int roomWidth = 10;
        const int roomHeight = 3;
        fixtureStage = "load-authored-assets";
        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        BuildingSO stair = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Stair.asset");
        BuildingSO housingAsset = FindBuildingAsset(data =>
            data.GetCaptiveHousingAbility()?.IsValid == true);
        BuildingSO penAsset = FindBuildingAsset(data =>
            data.GetBeastPenAbility()?.IsValid == true);
        if (hallway == null || wall == null || door == null || stair == null
            || housingAsset == null || penAsset == null)
        {
            fixtureStage = $"authored-assets-missing:hallway={hallway != null};wall={wall != null};door={door != null};stair={stair != null};housing={housingAsset != null};pen={penAsset != null}";
            return false;
        }
        if (grid.width < roomWidth + 4 || grid.height < roomHeight)
        {
            fixtureStage =
                $"grid-too-small:grid={grid.width}x{grid.height};required={roomWidth + 4}x{roomHeight}";
            return false;
        }

        int start = -1;
        const int row = 0;
        HashSet<BuildableObject> selectedDisplacements = null;
        HashSet<WildlifeActor> selectedWildlifeDisplacements = null;
        int selectedWildlifeCount = int.MaxValue;
        int selectedMovementCount = int.MaxValue;
        string lastRejectedCell = "none";
        fixtureStage = "find-clear-footprint";
        for (int x = 1; x <= grid.width - roomWidth - 4; x++)
        {
            bool clear = true;
            HashSet<BuildableObject> candidates = new();
            HashSet<WildlifeActor> wildlifeCandidates = new();
            for (int dy = 0; dy < roomHeight && clear; dy++)
            for (int dx = 0; dx < roomWidth && clear; dx++)
            {
                Vector2Int position = new(x + dx, row + dy);
                GridCell cell = grid.GetGridCell(position);
                if (cell == null)
                {
                    clear = false;
                    lastRejectedCell = $"missing:{position}";
                    break;
                }
                foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                {
                    IGridOccupant occupant = cell.GetOccupant(layer);
                    if (occupant == null)
                        continue;
                    // Wildlife owns a real production grid layer and can cover
                    // every legal 10x3 span on the authored 60x3 map.  It is
                    // safe to remove only the animals inside the chosen bounded
                    // fixture span because the full V18 baseline was captured
                    // before this scan and is restored atomically in Cleanup.
                    // Characters, facilities, doors and all other hard
                    // occupants remain non-displaceable.
                    if (layer == GridLayer.Wildlife
                        && occupant is WildlifeActor wildlifeActor
                        && wildlifeActor.IsAlive)
                    {
                        wildlifeCandidates.Add(wildlifeActor);
                        continue;
                    }
                    if ((layer != GridLayer.Building
                            && layer != GridLayer.Hallway)
                        || occupant is not BuildableObject movement
                        || movement is Facility
                        || movement is Door
                        || movement.Facility != null
                        || !movement.IsGridMovement
                        || movement.BlocksGridMovement)
                    {
                        clear = false;
                        lastRejectedCell =
                            $"hard-occupant:{position}:layer={layer}:type={occupant.GetType().Name}";
                        break;
                    }
                    candidates.Add(movement);
                }
            }
            if (clear)
            {
                Vector2Int connector = new(x + roomWidth, row + 1);
                Vector2Int exteriorAnchor = new(x + roomWidth + 3, row);
                GridCell anchorCell = grid.GetGridCell(exteriorAnchor);
                if (anchorCell == null
                    || anchorCell.AreaType != GridCellAreaType.ExteriorPath
                    || !anchorCell.IsWalkableArea
                    || !grid.IsWalkable(exteriorAnchor))
                {
                    clear = false;
                    lastRejectedCell =
                        $"escape-anchor-invalid:{exteriorAnchor}:area={anchorCell?.AreaType}";
                }

                Vector2Int[] escapeCells =
                {
                    connector,
                    new Vector2Int(x + roomWidth + 1, row + 1),
                    new Vector2Int(x + roomWidth + 2, row + 1),
                    new Vector2Int(x + roomWidth, row),
                    new Vector2Int(x + roomWidth + 1, row),
                    new Vector2Int(x + roomWidth + 2, row),
                    exteriorAnchor
                };
                foreach (Vector2Int position in escapeCells)
                {
                    if (!clear) break;
                    GridCell cell = grid.GetGridCell(position);
                    if (cell == null)
                    {
                        clear = false;
                        lastRejectedCell = $"escape-cell-missing:{position}";
                        break;
                    }
                    foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                    {
                        IGridOccupant occupant = cell.GetOccupant(layer);
                        if (occupant == null) continue;
                        if (layer == GridLayer.Wildlife
                            && occupant is WildlifeActor wildlifeActor
                            && wildlifeActor.IsAlive)
                        {
                            wildlifeCandidates.Add(wildlifeActor);
                            continue;
                        }
                        if ((layer != GridLayer.Building
                                && layer != GridLayer.Hallway)
                            || occupant is not BuildableObject movement
                            || movement is Facility
                            || movement is Door
                            || movement.Facility != null
                            || !movement.IsGridMovement
                            || movement.BlocksGridMovement)
                        {
                            clear = false;
                            lastRejectedCell =
                                $"escape-hard-occupant:{position}:layer={layer}:type={occupant.GetType().Name}";
                            break;
                        }
                        candidates.Add(movement);
                    }
                }
            }
            if (clear
                && (wildlifeCandidates.Count < selectedWildlifeCount
                    || wildlifeCandidates.Count == selectedWildlifeCount
                    && candidates.Count < selectedMovementCount))
            {
                start = x;
                selectedDisplacements = candidates;
                selectedWildlifeDisplacements = wildlifeCandidates;
                selectedWildlifeCount = wildlifeCandidates.Count;
                selectedMovementCount = candidates.Count;
            }
        }
        if (start < 0)
        {
            fixtureStage =
                $"clear-footprint-missing:grid={grid.width}x{grid.height};required={roomWidth}x{roomHeight};last={lastRejectedCell}";
            return false;
        }

        escapeConnectorCell = new Vector2Int(start + roomWidth, row + 1);
        escapeExteriorAnchorCell = new Vector2Int(start + roomWidth + 3, row);

        fixtureStage = "displace-bounded-wildlife";
        foreach (WildlifeActor wildlifeActor in selectedWildlifeDisplacements
                     .OrderBy(value => value.GridPosition.y)
                     .ThenBy(value => value.GridPosition.x)
                     .ThenBy(value => value.WildlifeId, StringComparer.Ordinal))
        {
            DisplacedWildlifeSnapshot snapshot = new(
                wildlifeActor.WildlifeId,
                wildlifeActor.SpeciesId,
                wildlifeActor.GridPosition);
            if (!wildlife.TryRemoveArrival(snapshot.WildlifeId))
            {
                fixtureStage =
                    $"wildlife-displacement-failed:{snapshot.WildlifeId}:{snapshot.Position}";
                return false;
            }
            displacedWildlife.Add(snapshot);
            if (wildlife.Wildlife.Any(value => value != null
                    && string.Equals(
                        value.WildlifeId,
                        snapshot.WildlifeId,
                        StringComparison.Ordinal))
                || ReferenceEquals(
                    grid.GetGridCell(snapshot.Position)?.GetOccupant(
                        GridLayer.Wildlife),
                    wildlifeActor))
            {
                fixtureStage =
                    $"wildlife-displacement-verification-failed:{snapshot.WildlifeId}:{snapshot.Position}";
                return false;
            }
        }

        for (int dy = 0; dy < roomHeight; dy++)
        for (int dx = 0; dx < roomWidth; dx++)
        {
            Vector2Int position = new(start + dx, row + dy);
            GridCell cell = grid.GetGridCell(position);
            areaSnapshots.Add(new AreaSnapshot(position, cell.AreaType));
            grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
            if (cell.AreaType != GridCellAreaType.DungeonInterior)
            {
                fixtureStage =
                    $"area-mutation-failed:{position}:actual={cell.AreaType}";
                return false;
            }
        }

        Vector2Int stairAnchor = new(start + roomWidth + 1, row);
        foreach (Vector2Int position in stair.GetGridPosList(stairAnchor))
        {
            GridCell cell = grid.GetGridCell(position);
            if (cell == null)
            {
                fixtureStage = $"escape-stair-cell-missing:{position}";
                return false;
            }
            if (!areaSnapshots.Any(snapshot => snapshot.Position == position))
                areaSnapshots.Add(new AreaSnapshot(position, cell.AreaType));
            grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
            if (cell.AreaType != GridCellAreaType.DungeonInterior)
            {
                fixtureStage =
                    $"escape-stair-area-mutation-failed:{position}:actual={cell.AreaType}";
                return false;
            }
        }

        foreach (BuildableObject movement in selectedDisplacements
                     .OrderBy(value => value.centerPos.y)
                     .ThenBy(value => value.centerPos.x)
                     .ThenBy(value => value.GridId))
        {
            GridLayer layer = movement.BuildingData.Placement.Layer;
            Vector2Int[] positions = movement.buildPoses.ToArray();
            bool connectPositions = movement.BuildingData.Placement.IsMovement;
            if (!grid.RemoveOccupant(
                    movement,
                    layer,
                    positions,
                    connectPositions))
            {
                fixtureStage =
                    $"movement-displacement-failed:{movement.GridId}:layer={layer}";
                return false;
            }
            displacedMovementBuildings.Add(new DisplacedMovementSnapshot(
                movement,
                layer,
                positions,
                connectPositions));
            if (!positions.All(position =>
                    grid.GetGridCell(position)?.ContainsOccupant(
                        layer,
                        movement) == false))
            {
                fixtureStage =
                    $"movement-displacement-verification-failed:{movement.GridId}:layer={layer}";
                return false;
            }
        }

        // Match DungeonStoryGridBuildingController.ConfigurePlacedBuilding:
        // production placement injects the concrete building component.
        GridBuildingFactory factory = new(created =>
            scope.Container.Inject(created));
        fixtureStage = "place-perimeter";
        for (int dx = 0; dx < roomWidth; dx++)
        {
            if (Place(factory, wall, new Vector2Int(start + dx, row)) == null
                || Place(factory, wall, new Vector2Int(start + dx, row + roomHeight - 1)) == null)
            {
                fixtureStage = $"perimeter-placement-failed:offset={dx};{placementFailure}";
                return false;
            }
        }
        Door cellDoor = Place(
            factory,
            door,
            new Vector2Int(start + roomWidth - 1, row + 1)) as Door;
        if (cellDoor == null
            || Place(factory, wall, new Vector2Int(start, row + 1)) == null)
        {
            fixtureStage = $"endpoint-placement-failed:{placementFailure}";
            return false;
        }
        IDoorAccessCommandService doorCommands =
            scope.Container.Resolve<IDoorAccessCommandService>();
        if (!doorCommands.ApplyPreset(cellDoor, DoorAccessPreset.Cell))
        {
            fixtureStage = "cell-door-preset-failed";
            return false;
        }
        Stair escapeStair = Place(factory, stair, stairAnchor) as Stair;
        if (escapeStair == null)
        {
            fixtureStage = "escape-stair-placement-failed:" + placementFailure;
            return false;
        }
        // The lower stair landing and the authored exterior anchor must form
        // one contiguous exterior lane.  Captivity escape uses the production
        // path broker to discover an ExteriorPath destination; leaving the
        // landing as DungeonInterior creates an authored-looking stair that
        // cannot actually reach an exterior destination.
        for (int x = start + roomWidth; x <= start + roomWidth + 3; x++)
        {
            Vector2Int position = new(x, row);
            GridCell cell = grid.GetGridCell(position);
            if (cell == null)
            {
                fixtureStage = $"escape-exterior-cell-missing:{position}";
                return false;
            }
            if (!areaSnapshots.Any(snapshot => snapshot.Position == position))
                areaSnapshots.Add(new AreaSnapshot(position, cell.AreaType));
            grid.SetAreaType(position, GridCellAreaType.ExteriorPath);
            if (cell.AreaType != GridCellAreaType.ExteriorPath)
            {
                fixtureStage =
                    $"escape-exterior-area-mutation-failed:{position}:actual={cell.AreaType}";
                return false;
            }
        }
        fixtureStage = "place-interior-hallway";
        for (int dx = 1; dx < roomWidth - 1; dx++)
        {
            if (Place(factory, hallway, new Vector2Int(start + dx, row + 1)) == null)
            {
                fixtureStage = $"hallway-placement-failed:offset={dx};{placementFailure}";
                return false;
            }
        }

        fixtureStage = "place-captivity-facilities";
        // CP01 is authored as 2x1 and even-width placement treats the anchor
        // as the right-hand cell (anchor - 1, anchor). CB01 is 1x1; keep all
        // three facilities on distinct cells and preserve free approach cells.
        // The captive housing is deliberately closest to the exit.  The
        // one-dimensional floor topology cannot route around facilities; with
        // pens between housing and the door a perfectly walkable connector is
        // still unreachable from a confined captive.
        housing = Place(factory, housingAsset, new Vector2Int(start + 7, row + 1));
        if (housing == null)
        {
            fixtureStage = "housing-placement-failed:" + placementFailure;
            return false;
        }
        pen = Place(factory, penAsset, new Vector2Int(start + 2, row + 1));
        if (pen == null)
        {
            fixtureStage = "pen-placement-failed:" + placementFailure;
            return false;
        }
        faultPen = Place(factory, penAsset, new Vector2Int(start + 4, row + 1));
        if (faultPen == null)
        {
            fixtureStage = "fault-pen-placement-failed:" + placementFailure;
            return false;
        }
        rooms.Clear();
        workerCell = new Vector2Int(start + 5, row + 1);
        captiveCell = new Vector2Int(start + 8, row + 1);
        RoomInstance housingRoom = null;
        RoomInstance penRoom = null;
        RoomInstance faultPenRoom = null;
        bool housingRoomResolved = housing != null
            && rooms.TryGetRoom(housing, out housingRoom);
        bool penRoomResolved = pen != null
            && rooms.TryGetRoom(pen, out penRoom);
        bool faultPenRoomResolved = faultPen != null
            && rooms.TryGetRoom(faultPen, out faultPenRoom);
        fixtureRoom = penRoom;
        if (housing == null || pen == null || faultPen == null
            || !penRoomResolved
            || fixtureRoom == null || !fixtureRoom.IsUsable
            || !housingRoomResolved
            || !faultPenRoomResolved
            || housingRoom?.Id != fixtureRoom.Id
            || faultPenRoom?.Id != fixtureRoom.Id
            || !fixtureRoom.Cells.Contains(workerCell)
            || !fixtureRoom.Cells.Contains(captiveCell)
            || !grid.IsWalkable(workerCell)
            || !grid.IsWalkable(captiveCell))
        {
            fixtureStage =
                $"facility-or-room-invalid:housing={housing != null};pen={pen != null};faultPen={faultPen != null};room={fixtureRoom?.Id};usable={fixtureRoom?.IsUsable};housingRoom={housingRoom?.Id};penRoom={penRoom?.Id};faultPenRoom={faultPenRoom?.Id};workerWalkable={grid.IsWalkable(workerCell)};captiveWalkable={grid.IsWalkable(captiveCell)};{placementFailure}";
            return false;
        }
        if (!TryValidateEscapeConnection(cellDoor, escapeStair, out string escapeFailure))
        {
            fixtureStage = "escape-connection-invalid:" + escapeFailure;
            return false;
        }
        fixtureStage =
            $"ready:bounds={start},{row}..{start + roomWidth - 1},{row + roomHeight - 1};room={fixtureRoom.Id};escape={escapeConnectorCell}->{escapeExteriorAnchorCell};displacedMovement={displacedMovementBuildings.Count};displacedWildlife={displacedWildlife.Count};areas={areaSnapshots.Count}";
        return true;
    }

    private bool TryValidateEscapeConnection(
        Door cellDoor,
        Stair escapeStair,
        out string failure)
    {
        GridCell connector = grid.GetGridCell(escapeConnectorCell);
        GridCell anchor = grid.GetGridCell(escapeExteriorAnchorCell);
        IDoorAccessQuery doorAccess = scope.Container.Resolve<IDoorAccessQuery>();
        GridTraversalContext context = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(captiveActor),
            DoorAccessOverrideKind.CaptiveEscape,
            GridMovementIntent.EscapeHazard);
        bool connectorWalkable = connector?.IsWalkableArea == true
            && grid.IsWalkable(escapeConnectorCell);
        bool anchorWalkable = anchor?.AreaType == GridCellAreaType.ExteriorPath
            && anchor.IsWalkableArea
            && grid.IsWalkable(escapeExteriorAnchorCell);
        bool stairLink = connector?.TraversalLinks.Any(link =>
            link != null
            && link.To == new Vector2Int(
                escapeConnectorCell.x,
                escapeConnectorCell.y - 1)
            && link.MoveType == GridMoveType.Stair
            && ReferenceEquals(link.Through, escapeStair)) == true;
        bool doorAllowed = doorAccess.CanTraverse(
            grid,
            cellDoor.centerPos,
            context,
            out string doorDenial);
        Vector2Int lowerLanding = new(
            escapeConnectorCell.x,
            escapeConnectorCell.y - 1);
        Vector2Int[] exteriorChain = Enumerable.Range(
                lowerLanding.x,
                escapeExteriorAnchorCell.x - lowerLanding.x + 1)
            .Select(x => new Vector2Int(x, lowerLanding.y))
            .ToArray();
        bool exteriorChainReady = exteriorChain.All(position =>
        {
            GridCell cell = grid.GetGridCell(position);
            return cell?.AreaType == GridCellAreaType.ExteriorPath
                && cell.IsWalkableArea
                && grid.IsWalkable(position);
        });
        GridPathSearchResult search = grid.SearchPathWithTraversalFilter(
            captiveCell,
            position => doorAccess.CanTraverse(
                grid,
                position,
                context,
                out _));
        bool routeReady = search != null
            && search.ContainsPosition(escapeConnectorCell)
            && search.ContainsPosition(escapeExteriorAnchorCell);
        if (connectorWalkable
            && anchorWalkable
            && stairLink
            && doorAllowed
            && exteriorChainReady
            && routeReady)
        {
            failure = string.Empty;
            return true;
        }

        failure = $"connectorWalkable={connectorWalkable};"
            + $"anchorWalkable={anchorWalkable};stairLink={stairLink};"
            + $"doorAllowed={doorAllowed};doorDenial={doorDenial};"
            + $"exteriorChainReady={exteriorChainReady};routeReady={routeReady};"
            + $"connector={DescribeCell(escapeConnectorCell)};"
            + $"anchor={DescribeCell(escapeExteriorAnchorCell)};"
            + $"door={DescribeCell(cellDoor.centerPos)};"
            + "exteriorChain=["
            + string.Join("|", exteriorChain.Select(DescribeCell))
            + "]";
        return false;
    }

    private string DescribeCell(Vector2Int position)
    {
        GridCell cell = grid.GetGridCell(position);
        if (cell == null) return $"{position}:missing";
        string occupants = string.Join(",",
            Enum.GetValues(typeof(GridLayer))
                .Cast<GridLayer>()
                .Select(layer => new
                {
                    Layer = layer,
                    Occupant = cell.GetOccupant(layer)
                })
                .Where(entry => entry.Occupant != null)
                .Select(entry =>
                    $"{entry.Layer}:{entry.Occupant.GetType().Name}#{entry.Occupant.GridId}"));
        string links = string.Join(",",
            cell.TraversalLinks.Select(link =>
                $"{link.To}/{link.MoveType}/{link.Through?.GetType().Name ?? "none"}"));
        return $"{position}:area={cell.AreaType}:walkableArea={cell.IsWalkableArea}:"
            + $"walkable={grid.IsWalkable(position)}:occupants=[{occupants}]:links=[{links}]";
    }

    private BuildableObject Place(
        GridBuildingFactory factory,
        BuildingSO data,
        Vector2Int position)
    {
        placementFailure = string.Empty;
        IReadOnlyList<Vector2Int> positions = data.GetGridPosList(position);
        Vector2Int blocked = positions.FirstOrDefault(cell =>
            grid.GetGridCell(cell)?.CanOccupy(data.Placement.Layer) != true);
        if (positions.Any(cell =>
                grid.GetGridCell(cell)?.CanOccupy(data.Placement.Layer) != true))
        {
            GridCell cell = grid.GetGridCell(blocked);
            placementFailure = $"precheck:data={data.name};anchor={position};cell={blocked};layer={data.Placement.Layer};exists={cell != null};area={cell?.AreaType};occupant={cell?.GetOccupant(data.Placement.Layer)?.GetType().Name ?? "none"}";
            return null;
        }
        BuildableObject building = factory.Create(grid, data, position);
        if (building == null)
        {
            placementFailure = $"factory-null:data={data.name};anchor={position}";
            return null;
        }
        if (!building.CanApplyDamageRules)
        {
            placementFailure =
                $"factory-injection-missing:data={data.name};anchor={position};"
                + $"type={building.GetType().Name};"
                + $"instance={building.GetInstanceID()}";
            Destroy(building.gameObject);
            return null;
        }
        building.SetGrid(grid);
        building.Initialization(data, position);
        if (!grid.RegisterOccupant(
                building,
                data.Placement.Layer,
                positions,
                data.Placement.IsMovement))
        {
            placementFailure = $"register-failed:data={data.name};anchor={position};cells={string.Join(",", positions)}";
            Destroy(building.gameObject);
            return null;
        }
        world.RegisterBuilding(building);
        fixtureBuildings.Add(building);
        fixtureBuildingSnapshots.Add(new FixtureBuildingSnapshot(
            building,
            data.Placement.Layer,
            positions.ToArray(),
            data.Placement.IsMovement));
        return building;
    }

    private static BuildingSO FindBuildingAsset(Func<BuildingSO, bool> predicate) =>
        AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building/Captivity" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(data => data != null && predicate(data));

    private string ResolveAuthoredFeedItemId(WildlifeActor animal)
    {
        WildlifeDietType diet = wildlifeSpecies.TryGetSpecies(
                animal.SpeciesId,
                out WildlifeSpeciesDefinition species)
            ? species.Diet
            : WildlifeDietType.Omnivore;
        string preferred = diet == WildlifeDietType.Herbivore
            ? "feed:hay"
            : "feed:dog-food";
        if (resourceCatalog.TryGetItem(
                preferred,
                out ResourceItemDefinitionSO authored)
            && IsFeedAllowed(diet, authored.IngredientTags))
        {
            return authored.ItemId;
        }

        return resourceCatalog.Items
            .Where(item => item != null
                && item.StockCategory == StockCategory.Food
                && IsFeedAllowed(diet, item.IngredientTags))
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .Select(item => item.ItemId)
            .FirstOrDefault() ?? string.Empty;
    }

    private static bool IsFeedAllowed(
        WildlifeDietType diet,
        ResourceIngredientTag tags)
    {
        bool plant = (tags & (ResourceIngredientTag.Plant
            | ResourceIngredientTag.Fungus)) != 0;
        bool animal = (tags & (ResourceIngredientTag.Meat
            | ResourceIngredientTag.Blood
            | ResourceIngredientTag.Fat
            | ResourceIngredientTag.Egg
            | ResourceIngredientTag.Milk)) != 0;
        bool spoiled = (tags & ResourceIngredientTag.Spoiled) != 0;
        return diet switch
        {
            WildlifeDietType.Herbivore => plant && !animal,
            WildlifeDietType.Carnivore => animal,
            WildlifeDietType.Scavenger => animal || spoiled,
            _ => plant || animal
        };
    }

    private int CountFacilityItem(string destinationId, string itemId)
    {
        if (string.IsNullOrEmpty(destinationId)
            || string.IsNullOrEmpty(itemId))
        {
            return 0;
        }
        return itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
    }

    private WildlifeActor SpawnCaptureAnimal(Vector2Int near)
    {
        wildlife.Tick();
        WildlifeActor source = wildlife.Wildlife.FirstOrDefault(value =>
            value != null && value.IsAlive);
        string speciesId = source?.SpeciesId
            ?? wildlifeSpecies?.All?.FirstOrDefault()?.SpeciesId;
        if (string.IsNullOrWhiteSpace(speciesId)) return null;
        HashSet<string> existing = wildlife.Wildlife
            .Where(value => value != null)
            .Select(value => value.WildlifeId)
            .ToHashSet(StringComparer.Ordinal);
        Vector2Int position = default;
        bool didSpawn = false;
        foreach (Vector2Int candidate in grid.GetCells()
                     .Where(cell => cell != null && grid.IsWalkable(cell.Position))
                     .OrderBy(cell => Mathf.Abs(cell.Position.x - near.x)
                         + Mathf.Abs(cell.Position.y - near.y))
                     .Select(cell => cell.Position))
        {
            if (!wildlife.DebugSpawn(
                    speciesId,
                    1,
                    candidate,
                    out int spawned,
                    out _)
                || spawned != 1)
            {
                continue;
            }
            position = candidate;
            didSpawn = true;
            break;
        }
        if (!didSpawn)
            return null;
        WildlifeActor created = wildlife.Wildlife.FirstOrDefault(value =>
            value != null && !existing.Contains(value.WildlifeId));
        created?.WarpTo(position);
        return created;
    }

    private Vector2Int FindFarWalkable(Vector2Int from) =>
        grid.GetCells()
            .Where(cell => cell != null && grid.IsWalkable(cell.Position))
            .OrderByDescending(cell => Mathf.Abs(cell.Position.x - from.x)
                + Mathf.Abs(cell.Position.y - from.y))
            .Select(cell => cell.Position)
            .FirstOrDefault();

    private IEnumerator PlanLawfulTransportFixture(
        TransportFixturePlanProbe probe)
    {
        if (probe == null)
        {
            yield break;
        }
        probe.Detail =
            "missing worker, grid, room, pen, or path broker";
        if (worker == null
            || grid == null
            || fixtureRoom == null
            || pen == null
            || pathSearchBroker == null)
        {
            yield break;
        }

        HashSet<Vector2Int> penFootprint = new(
            pen.buildPoses ?? Array.Empty<Vector2Int>());
        Vector2Int[] deliveryStands = fixtureRoom.Cells
            .Where(cell => grid.IsWalkable(cell)
                && !penFootprint.Contains(cell)
                && IsUnoccupiedTransportStand(grid.GetGridCell(cell)))
            .OrderBy(cell => Mathf.Abs(cell.x - pen.centerPos.x)
                + Mathf.Abs(cell.y - pen.centerPos.y))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .ToArray();
        Vector2Int[] sourceCells = grid.GetCells()
            .Where(cell => cell != null
                && WildlifeRuntime.IsInitialWildlifeSpawnCell(grid, cell)
                && cell.AreaType == GridCellAreaType.ExteriorPath
                && grid.IsWalkable(cell.Position)
                && IsUnoccupiedTransportStand(cell))
            .OrderBy(cell => Mathf.Abs(cell.Position.x - pen.centerPos.x)
                + Mathf.Abs(cell.Position.y - pen.centerPos.y))
            .ThenBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .Select(cell => cell.Position)
            .ToArray();
        GridTraversalContext traversal = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(worker),
            DoorAccessOverrideKind.EscortPass);
        // Mirror CaptivityAbilityUnityPort.TryCreateAdjacentMovement exactly.
        // A wildlife-layer occupant does not block grid movement, so the
        // target cell itself is a lawful final pickup stand.  This matters on
        // a one-cell exterior stair landing where none of the four adjacent
        // cells owns the traversal link back into the pen room.
        Vector2Int[] pickupOffsets =
        {
            Vector2Int.left,
            Vector2Int.right,
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.zero
        };
        float deadline = Time.realtimeSinceStartup + 8f;
        int deferred = 0;
        int pickupCandidates = 0;
        foreach (Vector2Int source in sourceCells)
        {
            foreach (Vector2Int offset in pickupOffsets)
            {
                Vector2Int pickup = source + offset;
                if (!grid.IsValidGridPos(pickup)
                    || !grid.IsWalkable(pickup))
                {
                    continue;
                }

                pickupCandidates++;
                GridPathSearchResult search = null;
                while (Time.realtimeSinceStartup < deadline
                       && !pathSearchBroker.TryGetSearch(
                           grid,
                           pickup,
                           out search,
                           GridPathSearchPriority.Urgent,
                           traversal))
                {
                    deferred++;
                    yield return null;
                }
                if (search == null)
                {
                    probe.Detail = $"sourceCandidates={sourceCells.Length};"
                        + $"pickupCandidates={pickupCandidates};"
                        + $"deliveryCandidates={deliveryStands.Length};"
                        + $"deferred={deferred};search-timeout;"
                        + $"doors={DescribeFixtureDoors()}";
                    yield break;
                }

                foreach (Vector2Int delivery in deliveryStands)
                {
                    Queue<GridMoveStep> path =
                        search.GetMovePathTo(delivery);
                    if (path == null
                        || !GridMovePathRules.TryGetPathDestination(
                            path,
                            out Vector2Int pathEnd)
                        || pathEnd != delivery)
                    {
                        continue;
                    }

                    probe.Ready = true;
                    probe.SourceCell = source;
                    probe.PickupStand = pickup;
                    probe.DeliveryStand = delivery;
                    probe.Detail = $"source={source};pickup={pickup};"
                        + $"delivery={delivery};pathEnd={pathEnd};"
                        + $"pathSteps={path.Count};deferred={deferred};"
                        + $"sourceCell={DescribeCell(source)};"
                        + $"pickupCell={DescribeCell(pickup)};"
                        + $"deliveryCell={DescribeCell(delivery)};"
                        + $"doors={DescribeFixtureDoors()}";
                    yield break;
                }
            }
        }

        probe.Detail = $"sourceCandidates={sourceCells.Length};"
            + $"pickupCandidates={pickupCandidates};"
            + $"deliveryCandidates={deliveryStands.Length};"
            + $"deferred={deferred};doors={DescribeFixtureDoors()};"
            + "no lawful source/pickup/delivery EscortPass triple";
    }

    private IEnumerator PlaceWorkerAtReachablePickupAndDeliveryStand(
        WildlifeActor animal,
        TransportFixturePlanProbe expectedPlan,
        PickupDeliveryPreflightProbe probe)
    {
        if (probe == null)
        {
            yield break;
        }
        probe.Detail =
            "missing actor, animal, grid, room, pen, or path broker";
        if (worker == null
            || animal == null
            || grid == null
            || fixtureRoom == null
            || pen == null
            || pathSearchBroker == null)
        {
            yield break;
        }
        HashSet<Vector2Int> penFootprint = new(
            pen.buildPoses ?? Array.Empty<Vector2Int>());
        Vector2Int[] deliveryStands = fixtureRoom.Cells
            .Where(cell => grid.IsWalkable(cell)
                && !penFootprint.Contains(cell)
                && IsUnoccupiedTransportStand(grid.GetGridCell(cell)))
            .OrderBy(cell => expectedPlan != null
                    && cell == expectedPlan.DeliveryStand
                ? 0
                : 1)
            .ThenBy(cell => Mathf.Abs(cell.x - pen.centerPos.x)
                + Mathf.Abs(cell.y - pen.centerPos.y))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .ToArray();
        Vector2Int source = animal.GridPosition;
        Vector2Int[] pickupCandidates =
        {
            source + Vector2Int.left,
            source + Vector2Int.right,
            source + Vector2Int.up,
            source + Vector2Int.down,
            source
        };
        GridTraversalContext pickupTraversal =
            GridTraversalContext.ForCharacter(
                CharacterPersistentIdentity.Require(worker),
                DoorAccessOverrideKind.None);
        GridTraversalContext deliveryTraversal = GridTraversalContext.ForCharacter(
            CharacterPersistentIdentity.Require(worker),
            DoorAccessOverrideKind.EscortPass);
        float deadline = Time.realtimeSinceStartup + 8f;
        int deferred = 0;
        int searchedPickupStands = 0;
        Vector2Int carrierStart = worker.GetNowXY();
        GridPathSearchResult carrierSearch = null;
        while (Time.realtimeSinceStartup < deadline
               && !pathSearchBroker.TryGetSearch(
                   grid,
                   carrierStart,
                   out carrierSearch,
                   GridPathSearchPriority.Urgent,
                   pickupTraversal))
        {
            deferred++;
            yield return null;
        }
        if (carrierSearch == null)
        {
            probe.Detail = $"carrier={carrierStart};deferred={deferred};"
                + "carrier-to-pickup production search unavailable";
            yield break;
        }

        Vector2Int selectedPickup = default;
        Queue<GridMoveStep> selectedPickupPath = null;
        foreach (Vector2Int pickup in pickupCandidates)
        {
            if (!grid.IsValidGridPos(pickup)
                || !grid.IsWalkable(pickup))
            {
                continue;
            }

            searchedPickupStands++;
            Queue<GridMoveStep> pickupPath =
                carrierSearch.GetMovePathTo(pickup);
            if (pickupPath == null
                || pickupPath.Count == 0
                || !GridMovePathRules.TryGetPathDestination(
                    pickupPath,
                    out Vector2Int pickupPathEnd)
                || pickupPathEnd != pickup)
            {
                continue;
            }
            if (selectedPickupPath == null
                || pickupPath.Count < selectedPickupPath.Count)
            {
                selectedPickup = pickup;
                selectedPickupPath = pickupPath;
            }
        }
        if (selectedPickupPath == null)
        {
            probe.Detail = $"animal={source};carrier={carrierStart};"
                + $"pickupCandidates={searchedPickupStands};"
                + $"deferred={deferred};"
                + "no production pickup approach path";
            yield break;
        }

        GridPathSearchResult search = null;
        while (Time.realtimeSinceStartup < deadline
               && !pathSearchBroker.TryGetSearch(
                   grid,
                   selectedPickup,
                   out search,
                   GridPathSearchPriority.Urgent,
                   deliveryTraversal))
        {
            deferred++;
            yield return null;
        }
        if (search == null)
        {
            probe.Detail = $"animal={source};carrier={carrierStart};"
                + $"pickup={selectedPickup};deferred={deferred};"
                + "pickup-to-delivery EscortPass search unavailable";
            yield break;
        }

        foreach (Vector2Int delivery in deliveryStands)
        {
            Queue<GridMoveStep> path = search.GetMovePathTo(delivery);
            if (path == null
                || !GridMovePathRules.TryGetPathDestination(
                    path,
                    out Vector2Int pathEnd)
                || pathEnd != delivery)
            {
                continue;
            }

            probe.Ready = true;
            probe.ApproachStart = carrierStart;
            probe.PickupStand = selectedPickup;
            probe.DeliveryStand = delivery;
            probe.PathStepCount = selectedPickupPath.Count + path.Count;
            probe.PathWorldDistance = CalculatePathWorldDistance(
                    carrierStart,
                    selectedPickupPath)
                + CalculatePathWorldDistance(selectedPickup, path);
            probe.Detail = $"animal={source};carrier={carrierStart};"
                + $"pickup={selectedPickup};delivery={delivery};"
                + $"pathEnd={pathEnd};"
                + $"pickupSteps={selectedPickupPath.Count};"
                + $"deliverySteps={path.Count};"
                + $"pathWorldDistance={probe.PathWorldDistance:0.###};"
                + $"deferred={deferred};"
                + $"pickupCell={DescribeCell(selectedPickup)};"
                + $"deliveryCell={DescribeCell(delivery)};"
                + $"doors={DescribeFixtureDoors()}";
            yield break;
        }

        probe.Detail = $"animal={source};pickupCandidates="
            + $"{searchedPickupStands};deliveryCandidates="
            + $"{deliveryStands.Length};deferred={deferred};"
            + $"doors={DescribeFixtureDoors()};"
            + "no exact production pickup and EscortPass delivery path";
    }

    private float CalculatePathWorldDistance(
        Vector2Int start,
        IEnumerable<GridMoveStep> path)
    {
        Vector3 previous = grid.GetWorldPos(start);
        float distance = 0f;
        if (path == null)
        {
            return distance;
        }

        foreach (GridMoveStep step in path)
        {
            if (!step.IsValid)
            {
                continue;
            }

            Vector3 next = grid.GetWorldPos(step.To);
            distance += Vector3.Distance(previous, next);
            previous = next;
        }
        return distance;
    }

    private static bool IsUnoccupiedTransportStand(GridCell cell)
    {
        return cell != null
            && cell.GetOccupant(GridLayer.Building) == null
            && cell.GetOccupant(GridLayer.Construction) == null
            && cell.GetOccupant(GridLayer.Conveyor) == null
            && cell.GetOccupant(GridLayer.Character) == null
            && cell.GetOccupant(GridLayer.DownedCharacter) == null
            && cell.GetOccupant(GridLayer.Wildlife) == null;
    }

    private static Vector2Int[] FindGridOccupantRegistrations(
        Grid targetGrid,
        GridLayer layer,
        IGridOccupant expectedOccupant)
    {
        if (targetGrid == null || expectedOccupant == null)
        {
            return Array.Empty<Vector2Int>();
        }

        List<Vector2Int> positions = new();
        for (int y = 0; y < targetGrid.height; y++)
        {
            for (int x = 0; x < targetGrid.width; x++)
            {
                Vector2Int position = new(x, y);
                if (ReferenceEquals(
                        targetGrid.GetGridCell(position)?.GetOccupant(layer),
                        expectedOccupant))
                {
                    positions.Add(position);
                }
            }
        }
        return positions.ToArray();
    }

    private static string DescribeGridCellLayers(GridCell cell)
    {
        if (cell == null)
        {
            return "missing-cell";
        }

        List<string> layers = new();
        List<IGridOccupant> occupants = new();
        foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
        {
            occupants.Clear();
            cell.FillOccupantsInLayer(layer, occupants);
            string detail = occupants.Count == 0
                ? "none"
                : string.Join("&", occupants.Select(DescribeGridOccupant));
            layers.Add($"{layer}=[{detail}]");
        }
        return string.Join("|", layers);
    }

    private static string DescribeGridOccupant(IGridOccupant occupant)
    {
        if (occupant == null)
        {
            return "none";
        }

        string runtimeType = occupant.GetType().FullName;
        if (occupant is UnityEngine.Object destroyedUnityObject
            && destroyedUnityObject == null)
        {
            return $"{runtimeType}:unity-destroyed:clrReference=True";
        }

        string authority = occupant switch
        {
            WildlifeActor animal when animal != null =>
                $"wildlife:{animal.WildlifeId}:{animal.SpeciesId}:"
                + $"{animal.State}@{animal.GridPosition}",
            DownedCharacterGridOccupant downed when downed.Actor != null =>
                $"downed:{downed.Actor.Identity?.PersistentId}:"
                + $"{downed.Actor.CurrentLifecycleState}@{downed.Actor.GetNowXY()}",
            BuildableObject building when building != null =>
                $"building:{(building.PersistentInstanceId.IsValid ? building.PersistentInstanceId.Value : "missing-id")}:"
                + $"{building.BuildingData?.name}@{building.centerPos}",
            _ => "authority-unavailable"
        };
        string unityState = occupant is UnityEngine.Object unityObject
            ? unityObject == null
                ? "unity-destroyed"
                : $"unity:{unityObject.name}:{unityObject.GetInstanceID()}"
            : "non-unity";
        return $"{runtimeType}:gridId={occupant.GridId}:"
            + $"destroyed={occupant.IsGridDestroyed}:"
            + $"visitable={occupant.IsGridVisitable}:"
            + $"movement={occupant.IsGridMovement}:"
            + $"{authority}:{unityState}";
    }

    private static string DescribeLiveActor(CharacterActor actor)
    {
        if (actor == null)
        {
            return "none";
        }

        return $"{actor.Identity?.PersistentId}:{actor.name}:"
            + $"{actor.CurrentLifecycleState}:active={actor.gameObject.activeInHierarchy}:"
            + $"paused={actor.IsAiPaused()}@{actor.GetNowXY()}";
    }

    private static string DescribeTransform(Transform value)
    {
        if (value == null)
        {
            return "none";
        }
        return $"{value.name}:{value.GetInstanceID()}";
    }

    private string DescribeFixtureDoors()
    {
        string[] details = fixtureBuildings
            .Where(building => building?.BuildingData != null
                && building.BuildingData.name.IndexOf(
                    "Door",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(building => DescribeCell(building.centerPos))
            .ToArray();
        return details.Length > 0
            ? string.Join("|", details)
            : "none";
    }

    private static void DamageForCapture(WildlifeActor actor)
    {
        if (actor != null)
            actor.ApplyDamage(Mathf.CeilToInt(actor.MaxHealth * 0.7f), null);
    }

    private void RegisterFaultRing(Vector2Int center)
    {
        RemoveFaultWalls();
        Vector2Int[] positions =
        {
            center + Vector2Int.left,
            center + Vector2Int.right,
            center + Vector2Int.up,
            center + Vector2Int.down
        };
        foreach (Vector2Int position in positions.Where(grid.IsValidGridPos))
        {
            FaultWall wall = new($"qa-wall:{position}", position);
            if (grid.RegisterOccupant(
                    wall, GridLayer.Building, new[] { position }, false))
                faultWalls.Add(wall);
        }
    }

    private void RemoveFaultWalls()
    {
        foreach (FaultWall wall in faultWalls.OfType<FaultWall>())
            grid?.RemoveOccupant(
                wall, GridLayer.Building, new[] { wall.Position }, false);
        faultWalls.Clear();
    }

    private bool RestoreCaptivity(CaptivitySaveData payload) =>
        RestoreSection(
            CaptivitySaveSection.Id,
            JsonUtility.ToJson(Clone(payload)),
            captivityPersistence as IDungeonRestoreTransactionParticipant);

    private bool RestoreEscapeReadyCaptivity(string captiveId)
    {
        CaptivitySaveData payload = Clone(confinedPayload);
        CaptiveState state = payload?.captives?.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.captiveId,
                captiveId,
                StringComparison.Ordinal));
        if (state == null)
        {
            fixtureStage = "escape-ready-captive-missing:" + captiveId;
            return false;
        }

        // These values satisfy the production false-compliance formula.  The
        // persisted flag is also explicit so the invasion event can fire in
        // the same tick as publication, before the next periodic recalculation.
        state.compliance = 100f;
        state.will = 0f;
        state.fear = 100f;
        state.grudge = 100f;
        state.trust = 0f;
        state.escapeRisk = 100f;
        state.falseCompliance = true;
        return RestoreCaptivity(payload);
    }

    private bool RestoreCircus(CircusSaveData payload)
    {
        IDungeonSaveSection captivitySection = saveRegistry.OrderedSections
            .Single(section => section.SectionId == CaptivitySaveSection.Id);
        IDungeonSaveSection circusSection = saveRegistry.OrderedSections
            .Single(section => section.SectionId == CircusSaveSection.Id);
        HashSet<string> actual = new(StringComparer.Ordinal)
        {
            CaptivitySaveSection.Id,
            CircusSaveSection.Id
        };
        List<IDungeonSaveSection> sections = captivitySection.DependsOn
            .Concat(circusSection.DependsOn)
            .Where(id => !actual.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .Select(id => (IDungeonSaveSection)new DependencyMarkerSection(id))
            .Concat(new[] { captivitySection, circusSection })
            .ToList();
        DungeonSaveSectionRegistry registry = new(
            sections,
            aggregateStore,
            new[]
            {
                captivityPersistence as IDungeonRestoreTransactionParticipant,
                circusPersistence as IDungeonRestoreTransactionParticipant
            }.Where(value => value != null).ToArray());
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        envelopes.Single(value => value.sectionId == CircusSaveSection.Id)
            .payloadJson = JsonUtility.ToJson(Clone(payload));
        DungeonGameRestoreReport report = new();
        bool restored = registry.RestoreAll(envelopes, report);
        if (!restored) rows.Add("INFO\tCIRCUS_RESTORE_ERROR\t" + string.Join(" | ", report.Errors));
        return restored;
    }

    private bool RestoreHusbandry(DungeonAnimalHusbandrySaveData payload) =>
        RestoreSection(
            AnimalHusbandrySaveSection.Id,
            JsonUtility.ToJson(payload),
            participant: null);

    private void BeginInvasionEventDiagnostics()
    {
        invasionStartedProbeSubscription ??=
            gameEvents.Subscribe<InvasionStartedEvent>(_ =>
            {
                invasionStartedEventCount++;
                RecordInvasionEvent("started");
            });
        invasionResolvedProbeSubscription ??=
            gameEvents.Subscribe<InvasionResolvedEvent>(resolved =>
            {
                invasionResolvedEventCount++;
                RecordInvasionEvent(
                    $"resolved:defended={resolved.defended}:"
                    + $"risk={resolved.residualRisk:0.###}");
            });
        invasionCandidateProbeSubscription ??=
            gameEvents.Subscribe<InvasionCandidateEvent>(_ =>
            {
                invasionCandidateEventCount++;
                RecordInvasionEvent("candidate");
            });
        activeIncidentsProbeSubscription ??=
            gameEvents.Subscribe<SettlementActiveIncidentsChangedEvent>(change =>
            {
                activeIncidentsChangedEventCount++;
                RecordInvasionEvent(
                    $"incidents:epoch={change.EpochId}:"
                    + $"desired={change.DesiredLevel}:"
                    + $"committed={change.CommittedLevel}:"
                    + $"count={change.ActiveIncidentCount}");
            });
    }

    private void EndInvasionEventDiagnostics()
    {
        invasionStartedProbeSubscription?.Dispose();
        invasionResolvedProbeSubscription?.Dispose();
        invasionCandidateProbeSubscription?.Dispose();
        activeIncidentsProbeSubscription?.Dispose();
        invasionStartedProbeSubscription = null;
        invasionResolvedProbeSubscription = null;
        invasionCandidateProbeSubscription = null;
        activeIncidentsProbeSubscription = null;
    }

    private void RecordInvasionEvent(string detail)
    {
        invasionEventTrace.Add(
            $"frame={Time.frameCount};hour={calendar?.AbsoluteHour};{detail}");
        const int maximumTraceEntries = 48;
        if (invasionEventTrace.Count > maximumTraceEntries)
        {
            invasionEventTrace.RemoveRange(
                0,
                invasionEventTrace.Count - maximumTraceEntries);
        }
    }

    private string DescribeInvasionIncident()
    {
        DungeonStory.Infrastructure.SettlementThreatAlertSaveData save =
            settlementAlertPersistence?.CaptureAlertSaveData();
        DungeonStory.Infrastructure.SettlementIncidentSaveData incident =
            save?.incidents?.FirstOrDefault(value =>
                value != null
                && string.Equals(
                    value.incidentId,
                    "incident:invasion:active",
                    StringComparison.Ordinal));
        return incident == null
            ? "missing"
            : $"active={incident.active},revision={incident.revision},"
              + $"level={incident.requiredLevel},source={incident.sourceId},"
              + $"detail={incident.diagnostic}";
    }

    private bool RestoreSection(
        string sectionId,
        string payloadJson,
        IDungeonRestoreTransactionParticipant participant)
    {
        IDungeonSaveSection section = saveRegistry.OrderedSections
            .Single(candidate => candidate.SectionId == sectionId);
        List<IDungeonSaveSection> sections = section.DependsOn
            .Distinct(StringComparer.Ordinal)
            .Select(dependency =>
                (IDungeonSaveSection)new DependencyMarkerSection(dependency))
            .Append(section)
            .ToList();
        DungeonSaveSectionRegistry registry = new(
            sections,
            aggregateStore,
            participant != null
                ? new[] { participant }
                : Array.Empty<IDungeonRestoreTransactionParticipant>());
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        envelopes.Single(value => value.sectionId == sectionId).payloadJson = payloadJson;
        DungeonGameRestoreReport report = new();
        bool restored = registry.RestoreAll(envelopes, report);
        if (!restored) rows.Add($"INFO\t{sectionId}_RESTORE_ERROR\t{string.Join(" | ", report.Errors)}");
        return restored;
    }

    private bool RestoreBaselineSection(
        string sectionId,
        IDungeonRestoreTransactionParticipant participant,
        out string detail)
    {
        detail = string.Empty;
        DungeonSaveSectionEnvelope baselineEnvelope = baseline?
            .FirstOrDefault(value => string.Equals(
                value.sectionId,
                sectionId,
                StringComparison.Ordinal));
        if (baselineEnvelope == null)
        {
            detail = "baseline-section-missing:" + sectionId;
            return false;
        }

        IDungeonSaveSection section = saveRegistry.OrderedSections
            .Single(candidate => candidate.SectionId == sectionId);
        HashSet<string> actual = new(StringComparer.Ordinal)
        {
            sectionId
        };
        List<IDungeonSaveSection> sections = section.DependsOn
            .Where(id => !actual.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .Select(id => (IDungeonSaveSection)new DependencyMarkerSection(id))
            .Append(section)
            .ToList();
        DungeonSaveSectionRegistry registry = new(
            sections,
            aggregateStore,
            participant != null
                ? new[] { participant }
                : Array.Empty<IDungeonRestoreTransactionParticipant>());
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        DungeonSaveSectionEnvelope target = envelopes.Single(value =>
            string.Equals(
                value.sectionId,
                sectionId,
                StringComparison.Ordinal));
        target.payloadJson = baselineEnvelope.payloadJson;
        DungeonGameRestoreReport report = new();
        bool restored = registry.RestoreAll(envelopes, report);
        detail = restored
            ? "restored:" + sectionId
            : string.Join(" | ", report.Errors);
        return restored;
    }

    private static T Clone<T>(T value) =>
        JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

    private static IEnumerator WaitUntil(Func<bool> condition, float timeout)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (Time.realtimeSinceStartup < deadline && !condition())
            yield return null;
    }

    private void PauseOtherAi()
    {
        foreach (CharacterActor actor in world.Characters.Where(value => value != null))
        {
            AIBrain brain = actor.Brain;
            if (brain == null) continue;
            pausedAi.Add(new MonoBehaviourState(brain, brain.enabled));
            if (actor != worker) brain.enabled = false;
        }
    }

    private void Cleanup()
    {
        EndInvasionEventDiagnostics();
        RemoveFaultWalls();
        if (workerWork != null)
            workerWork.SetWorkPriority(BuiltInWorkTypeIds.AnimalCare, oldAnimalCarePriority);
        foreach (MonoBehaviourState state in pausedAi)
            if (state.Behaviour != null) state.Behaviour.enabled = state.Enabled;

        bool fixtureOccupantsRemoved = true;
        for (int index = fixtureBuildingSnapshots.Count - 1; index >= 0; index--)
        {
            FixtureBuildingSnapshot snapshot = fixtureBuildingSnapshots[index];
            BuildableObject building = snapshot.Building;
            if (building != null)
            {
                world?.UnregisterBuilding(building);
                building.Grid?.RemoveOccupant(
                    building,
                    snapshot.Layer,
                    snapshot.Positions,
                    snapshot.ConnectPositions);
            }
            fixtureOccupantsRemoved &= snapshot.Positions.All(position =>
                !ReferenceEquals(
                    grid?.GetGridCell(position)?.GetOccupant(snapshot.Layer),
                    snapshot.Building));
            if (building != null)
                Destroy(building.gameObject);
        }
        Check(
            fixtureOccupantsRemoved,
            "FIXTURE_OCCUPANTS_REMOVED",
            $"count={fixtureBuildings.Count};exact={fixtureOccupantsRemoved}");
        fixtureBuildings.Clear();
        fixtureBuildingSnapshots.Clear();

        bool displacedRestored = true;
        foreach (DisplacedMovementSnapshot displaced in displacedMovementBuildings)
        {
            bool registered = displaced.Building != null
                && grid != null
                && grid.RegisterOccupant(
                    displaced.Building,
                    displaced.Layer,
                    displaced.Positions,
                    displaced.ConnectPositions);
            bool exact = registered
                && displaced.Positions.All(position =>
                    grid.GetGridCell(position)?.ContainsOccupant(
                        displaced.Layer,
                        displaced.Building) == true);
            displacedRestored &= exact;
        }
        Check(
            displacedRestored,
            "FIXTURE_MOVEMENT_RESTORE",
            $"count={displacedMovementBuildings.Count};exact={displacedRestored}");
        displacedMovementBuildings.Clear();

        bool areasRestored = true;
        foreach (AreaSnapshot snapshot in areaSnapshots)
        {
            GridCell cell = grid?.GetGridCell(snapshot.Position);
            if (cell == null)
            {
                areasRestored = false;
                continue;
            }
            grid.SetAreaType(snapshot.Position, snapshot.AreaType);
            areasRestored &= cell.AreaType == snapshot.AreaType;
        }
        Check(
            areasRestored,
            "FIXTURE_AREA_RESTORE",
            $"count={areaSnapshots.Count};exact={areasRestored}");
        areaSnapshots.Clear();
        rooms?.Clear();

        try
        {
            if (saveRegistry != null && baseline != null)
            {
                DungeonGameRestoreReport report = new();
                if (!saveRegistry.RestoreAll(baseline, report))
                    Check(false, "BASELINE_RESTORE", string.Join(" | ", report.Errors));
                else
                {
                    Grid restoredGrid = null;
                    bool restoredGridResolved =
                        world?.TryGetGrid(out restoredGrid) == true
                        && restoredGrid != null;
                    List<string> wildlifeRestoreMismatches = new();
                    bool wildlifeRestored = restoredGridResolved
                        && displacedWildlife.All(snapshot =>
                    {
                        WildlifeActor[] matching = wildlife?.Wildlife.Where(value =>
                            value != null
                            && string.Equals(
                                value.WildlifeId,
                                snapshot.WildlifeId,
                                StringComparison.Ordinal)).ToArray()
                            ?? Array.Empty<WildlifeActor>();
                        WildlifeActor restored = matching.FirstOrDefault();
                        WildlifeActor occupant =
                            restoredGrid?.GetGridCell(snapshot.Position)?.GetOccupant(
                                GridLayer.Wildlife) as WildlifeActor;
                        bool exact = restoredGridResolved
                            && matching.Length == 1
                            && restored != null
                            && restored.GridPosition == snapshot.Position
                            && string.Equals(
                                restored.SpeciesId,
                                snapshot.SpeciesId,
                                StringComparison.Ordinal)
                            && occupant != null
                            && occupant.GridPosition == snapshot.Position
                            && string.Equals(
                                occupant.WildlifeId,
                                snapshot.WildlifeId,
                                StringComparison.Ordinal)
                            && string.Equals(
                                occupant.SpeciesId,
                                snapshot.SpeciesId,
                                StringComparison.Ordinal);
                        if (!exact)
                        {
                            wildlifeRestoreMismatches.Add(
                                $"{snapshot.WildlifeId}:{snapshot.SpeciesId}@{snapshot.Position}"
                                + $"=>population={matching.Length};restored={restored?.SpeciesId}@{restored?.GridPosition};"
                                + $"occupant={occupant?.WildlifeId}:{occupant?.SpeciesId}@{occupant?.GridPosition}");
                        }
                        return exact;
                    });
                    Check(
                        wildlifeRestored,
                        "FIXTURE_WILDLIFE_V18_RESTORE",
                        $"count={displacedWildlife.Count};exact={wildlifeRestored};"
                        + (wildlifeRestoreMismatches.Count == 0
                            ? $"grid={restoredGridResolved};identity=id+species+position+grid-occupancy"
                            : string.Join(" | ", wildlifeRestoreMismatches)));
                }
            }
        }
        catch (Exception exception)
        {
            Check(false, "BASELINE_RESTORE_EXCEPTION", exception.Message);
        }
        displacedWildlife.Clear();
    }

    private void Check(bool success, string id, string detail)
    {
        rows.Add($"{(success ? "PASS" : "FAIL")}\t{id}\t{detail}");
        if (!success) failures.Add(id + ": " + detail);
        WriteReport();
    }

    private bool HasCanonicalProficiencyProfile(CharacterActor candidate)
    {
        if (candidate == null
            || proficiencies == null
            || calendar == null
            || !CharacterPersistentIdentity.TryGet(
                candidate,
                out CharacterId characterId))
        {
            return false;
        }

        IReadOnlyList<CharacterProficiencySnapshot> values =
            proficiencies.GetAllProficiencies(
                characterId,
                calendar.AbsoluteHour);
        return values != null
            && values.Count == BuiltInCharacterProficiencyIds.All.Count;
    }

    private string DescribeActor(CharacterActor candidate)
    {
        if (candidate == null) return "missing";
        CharacterPersistentIdentity.TryGet(candidate, out CharacterId id);
        int count = id.IsValid && proficiencies != null && calendar != null
            ? proficiencies.GetAllProficiencies(id, calendar.AbsoluteHour).Count
            : 0;
        return $"{candidate.name}:{id.Value}:proficiencies={count}";
    }

    private void CaptureConsoleIssue(string condition, string stackTrace, LogType type)
    {
        if (type is LogType.Warning
            or LogType.Error
            or LogType.Exception
            or LogType.Assert)
        {
            consoleIssues.Add($"{type}:{condition}");
        }
    }

    private void WriteReport()
    {
        Directory.CreateDirectory("Artifacts/QA");
        List<string> report = new()
        {
            "# Captivity / Wildlife production-live lifecycle matrix",
            $"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}; failures={failures.Count}",
            "fixture=GameplayScene+official-start-party+authored-room+V18-save-boundary",
            "terminal-policy=no-direct-runtime-terminal-calls"
        };
        report.AddRange(rows);
        report.AddRange(failures.Select(value => "FAILURE\t" + value));
        File.WriteAllLines(CaptivityWildlifeLifecyclePlayModeVerifier.ReportPath, report);
    }

    private readonly struct AreaSnapshot
    {
        public AreaSnapshot(Vector2Int position, GridCellAreaType areaType)
        {
            Position = position;
            AreaType = areaType;
        }
        public Vector2Int Position { get; }
        public GridCellAreaType AreaType { get; }
    }

    private sealed class EscapeStartProbe
    {
        public bool Started;
        public string Reason = string.Empty;
    }

    private sealed class EscapeRouteProbe
    {
        public bool Ready;
        public string Detail = string.Empty;
    }

    private readonly struct DisplacedMovementSnapshot
    {
        public DisplacedMovementSnapshot(
            BuildableObject building,
            GridLayer layer,
            IReadOnlyList<Vector2Int> positions,
            bool connectPositions)
        {
            Building = building;
            Layer = layer;
            Positions = positions;
            ConnectPositions = connectPositions;
        }

        public BuildableObject Building { get; }
        public GridLayer Layer { get; }
        public IReadOnlyList<Vector2Int> Positions { get; }
        public bool ConnectPositions { get; }
    }

    private readonly struct FixtureBuildingSnapshot
    {
        public FixtureBuildingSnapshot(
            BuildableObject building,
            GridLayer layer,
            IReadOnlyList<Vector2Int> positions,
            bool connectPositions)
        {
            Building = building;
            Layer = layer;
            Positions = positions;
            ConnectPositions = connectPositions;
        }

        public BuildableObject Building { get; }
        public GridLayer Layer { get; }
        public IReadOnlyList<Vector2Int> Positions { get; }
        public bool ConnectPositions { get; }
    }

    private readonly struct DisplacedWildlifeSnapshot
    {
        public DisplacedWildlifeSnapshot(
            string wildlifeId,
            string speciesId,
            Vector2Int position)
        {
            WildlifeId = wildlifeId ?? string.Empty;
            SpeciesId = speciesId ?? string.Empty;
            Position = position;
        }

        public string WildlifeId { get; }
        public string SpeciesId { get; }
        public Vector2Int Position { get; }
    }

    private readonly struct MonoBehaviourState
    {
        public MonoBehaviourState(MonoBehaviour behaviour, bool enabled)
        {
            Behaviour = behaviour;
            Enabled = enabled;
        }
        public MonoBehaviour Behaviour { get; }
        public bool Enabled { get; }
    }

    private sealed class NoPathSearchBroker : IGridPathSearchBroker
    {
        public int SearchesThisFrame => 0;
        public int UrgentOverdraftSearchesThisFrame => 0;
        public int UnboundedSearchesThisFrame => 0;
        public int CacheHitsThisFrame => 0;
        public int BudgetDeferralsThisFrame => 0;
        public double SearchMillisecondsThisFrame => 0d;

        public void BeginFrame(
            int searchBudget,
            bool enforceBudget,
            double searchTimeBudgetMilliseconds = double.PositiveInfinity)
        {
        }

        public bool TryGetSearch(
            Grid grid,
            Vector2Int start,
            out GridPathSearchResult result,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default)
        {
            result = null;
            return false;
        }

        public Queue<GridMoveStep> GetMovePath(
            Grid grid,
            Vector2Int start,
            Func<Vector2Int, bool> terminateEndCondition,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default) => new();

        public Queue<GridMoveStep> GetMovePathTo(
            Grid grid,
            Vector2Int start,
            Vector2Int destination,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default) => new();

        public GridPathRequestStatus RequestMovePathTo(
            Grid grid,
            Vector2Int start,
            Vector2Int destination,
            out Queue<GridMoveStep> path,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default)
        {
            path = new Queue<GridMoveStep>();
            return GridPathRequestStatus.Unreachable;
        }

        public void Clear()
        {
        }
    }

    private sealed class FaultWall : IGridBuildingOccupantCapability
    {
        public FaultWall(string id, Vector2Int position)
        {
            Id = id;
            Position = position;
        }
        public string Id { get; }
        public Vector2Int Position { get; }
        public int GridId => Id.GetHashCode();
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => false;
        public bool BlocksGridMovement => true;
        public bool AllowsInteriorWalkability => false;
    }

    private sealed class DependencyMarkerSection :
        DungeonDebugStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly string id;
        public DependencyMarkerSection(string id) =>
            this.id = id ?? throw new ArgumentNullException(nameof(id));
        public override string SectionId => id;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;
        protected override void CommitMarker(DungeonGameRestoreReport report) { }
    }
}
#endif
