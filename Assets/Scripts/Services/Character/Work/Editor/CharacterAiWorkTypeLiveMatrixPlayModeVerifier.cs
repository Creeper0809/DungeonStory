#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

/// <summary>
/// Production-live closure for work types that do not yet have a dedicated
/// verifier. A row can pass only after the real Brain -> AIWork -> AbilityWork
/// -> WorkTaskExecutor path changes approved work progress and closes its
/// typed lifecycle. Missing authored targets remain explicit blockers.
/// </summary>
public static class CharacterAiWorkTypeLiveMatrixPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/character-ai-worktype-live-matrix.txt";
    public const string P15AutomationModesReportPath =
        "Artifacts/QA/v27-p15-production-execution-modes-playmode.txt";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string PendingPath =
        "Temp/character-ai-worktype-live-matrix.flag";
    private const string P15AutomationModesPendingPath =
        "Temp/v27-p15-production-execution-modes.flag";

    [MenuItem("DungeonStory/Debug/QA/Run Character AI WorkType Live Matrix")]
    public static void RequestRun()
    {
        if (EditorApplication.isPlaying)
        {
            StartRunner(p15AutomationModesOnly: false);
            return;
        }

        if (!File.Exists(GameplayScenePath))
        {
            throw new FileNotFoundException(
                "Character AI WorkType live matrix requires the official gameplay scene.",
                GameplayScenePath);
        }

        EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingPath, DateTime.UtcNow.ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("DungeonStory/Debug/QA/Run P15 Production Execution Modes")]
    public static void RequestP15AutomationModesRun()
    {
        if (EditorApplication.isPlaying)
        {
            StartRunner(p15AutomationModesOnly: true);
            return;
        }

        if (!File.Exists(GameplayScenePath))
        {
            throw new FileNotFoundException(
                "P15 execution-mode verification requires the official gameplay scene.",
                GameplayScenePath);
        }

        EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory("Temp");
        File.WriteAllText(P15AutomationModesPendingPath, DateTime.UtcNow.ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        bool p15AutomationModesOnly = File.Exists(P15AutomationModesPendingPath);
        if (!p15AutomationModesOnly && !File.Exists(PendingPath))
            return;
        StartRunner(p15AutomationModesOnly);
    }

    internal static void MarkRunCompleted()
    {
        if (File.Exists(PendingPath))
            File.Delete(PendingPath);
        if (File.Exists(P15AutomationModesPendingPath))
            File.Delete(P15AutomationModesPendingPath);
    }

    private static void StartRunner(bool p15AutomationModesOnly)
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                CharacterAiWorkTypeLiveMatrixPlayModeRunner>() != null)
        {
            return;
        }

        CharacterAiWorkTypeLiveMatrixPlayModeRunner runner =
            new GameObject("Character AI WorkType Live Matrix Runner")
                .AddComponent<CharacterAiWorkTypeLiveMatrixPlayModeRunner>();
        runner.P15AutomationModesOnly = p15AutomationModesOnly;
    }
}

public sealed class CharacterAiWorkTypeLiveMatrixPlayModeRunner : MonoBehaviour
{
    public bool P15AutomationModesOnly { get; set; }

    private const float MinimumProgressObservationSeconds = 8f;
    private const float MaximumProgressObservationSeconds = 120f;
    private const float MaximumCompletionObservationSeconds = 240f;
    private const float CompletionNoProgressSeconds = 30f;
    private const float MovementStallSeconds = 5f;
    private const float MovementPositionEpsilonSquared = 0.0001f;

    private static readonly WorkTypeId[] Rows =
    {
        BuiltInWorkTypeIds.Operate,
        BuiltInWorkTypeIds.Restock,
        BuiltInWorkTypeIds.Repair,
        BuiltInWorkTypeIds.Reception,
        BuiltInWorkTypeIds.Craft,
        BuiltInWorkTypeIds.Butcher,
        BuiltInWorkTypeIds.DrawWater,
        BuiltInWorkTypeIds.Cook,
        BuiltInWorkTypeIds.Refuel,
        BuiltInWorkTypeIds.Perform,
        BuiltInWorkTypeIds.Gather,
        BuiltInWorkTypeIds.Sow,
        BuiltInWorkTypeIds.Harvest,
        BuiltInWorkTypeIds.Logging,
        BuiltInWorkTypeIds.Quarry,
        BuiltInWorkTypeIds.AnimalCare,
        BuiltInWorkTypeIds.GrandProject,
        BuiltInWorkTypeIds.ThreatMitigation,
        BuiltInWorkTypeIds.Plumbing,
        BuiltInWorkTypeIds.Dismantle
    };

    private static readonly string[] RequiredFixtureResearchIds =
    {
        "research:agriculture:gathering",
        "research:agriculture:field",
        "research:forestry:logging",
        "research:mining:surface",
        "research:mining:quarry",
        "research:mining:stonecutting",
        "research:cuisine:livestock",
        "research:plumbing:sewer"
    };

    private readonly List<WorkTypeLiveRow> results =
        new List<WorkTypeLiveRow>(Rows.Length);
    private readonly List<string> consoleIssues = new List<string>(8);
    private readonly List<ActorPauseState> actorPauseStates =
        new List<ActorPauseState>(8);
    private readonly List<BuildableObject> roomFixtureBuildings =
        new List<BuildableObject>(32);
    private readonly List<BuildableObject> rowScopedFixtureBuildings =
        new List<BuildableObject>(16);
    private readonly List<string> rowScopedWildlifeIds = new List<string>(4);
    private readonly List<string> rowScopedItemStackIds = new List<string>(16);
    private readonly List<DisplacedMovementSnapshot> displacedRoomMovements =
        new List<DisplacedMovementSnapshot>(16);
    private readonly List<DisplacedWildlifeSnapshot> displacedRoomWildlife =
        new List<DisplacedWildlifeSnapshot>(4);
    private readonly List<FixtureAreaSnapshot> roomAreaSnapshots =
        new List<FixtureAreaSnapshot>(24);
    private DungeonRuntimeLifetimeScope runtimeScope;
    private IDungeonSaveSectionRegistry saveRegistry;
    private ICharacterAiWorldRegistry worldRegistry;
    private List<DungeonSaveSectionEnvelope> baseline;
    private CharacterActor actor;
    private AbilityWork work;
    private AIBrain brain;
    private Grid grid;
    private IProductionBillQuery productionBills;
    private IProductionBillOrderCommand productionOrders;
    private IProductionBillWorkExecution productionWork;
    private IAutomationInfrastructureQuery automation;
    private IAutomationInfrastructureCommand automationCommands;
    private IAutomationInfrastructurePersistence automationPersistence;
    private IPowerInfrastructureQuery power;
    private ISettlementLaborAccountingService settlementLabor;
    private IResourceEconomyContentCatalog economyContent;
    private IWorldItemStackRuntime physicalItems;
    private WorldItemRepository itemRepository;
    private IWildlifeRuntime wildlifeRuntime;
    private IWildlifeCarcassService carcassService;
    private IShopStockCatalog shopStockCatalog;
    private IWorldResourceRuntime worldResources;
    private CropPlotRuntime cropPlots;
    private IWorkOrderRuntime workOrders;
    private StaffDiscontentRuntime staffDiscontent;
    private ICharacterEnvironmentPersistence characterEnvironmentPersistence;
    private ICharacterEnvironmentStatusQuery characterEnvironmentStatus;
    private ICharacterDeprivationRuntime deprivationRuntime;
    private BuildableObject domainStage;
    private BuildableObject domainPen;
    private RoomInstance performanceRoom;
    private Vector2Int? roomExternalExitStand;
    private Vector2Int? roomBaselineReachableSentinel;
    private float previousTimeScale;
    private bool consoleGatePassed;
    private bool abortRemainingRows;

    private IEnumerator Start()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 8f;
        Application.logMessageReceived += CaptureConsoleIssue;
        yield return RunGuarded();
        Cleanup();
        Application.logMessageReceived -= CaptureConsoleIssue;
        consoleGatePassed = consoleIssues.Count == 0;
        results.Add(consoleGatePassed
            ? WorkTypeLiveRow.Info("console-warning-error-zero", "0/0")
            : new WorkTypeLiveRow(
                "global:console-warning-error-zero",
                "FAIL",
                "-",
                string.Join(" | ", consoleIssues)));
        WriteReport();
        CharacterAiWorkTypeLiveMatrixPlayModeVerifier.MarkRunCompleted();
        Time.timeScale = previousTimeScale;
        Destroy(gameObject);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        };
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= CaptureConsoleIssue;
    }

    private void CaptureConsoleIssue(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is LogType.Warning
            or LogType.Error
            or LogType.Exception
            or LogType.Assert)
        {
            consoleIssues.Add(type + ":" + condition);
        }
    }

    private IEnumerator RunGuarded()
    {
        IEnumerator scenario = null;
        try
        {
            scenario = RunScenario();
        }
        catch (Exception exception)
        {
            AddGlobalFailure("setup-exception", exception.ToString());
        }

        if (scenario == null)
            yield break;

        while (true)
        {
            bool moved;
            object current = null;
            try
            {
                moved = scenario.MoveNext();
                if (moved)
                    current = scenario.Current;
            }
            catch (Exception exception)
            {
                AddGlobalFailure("runtime-exception", exception.ToString());
                yield break;
            }

            if (!moved)
                yield break;
            yield return current;
        }
    }

    private IEnumerator RunScenario()
    {
        DungeonRuntimeLifetimeScope scope = null;
        float setupDeadline = Time.realtimeSinceStartup + 45f;
        while (Time.realtimeSinceStartup < setupDeadline)
        {
            DungeonRuntimeLifetimeScope[] candidates =
                FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < candidates.Length; index++)
            {
                DungeonRuntimeLifetimeScope candidate = candidates[index];
                if (candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.scene.isLoaded
                    && candidate.Container != null)
                {
                    scope = candidate;
                    break;
                }
            }
            if (scope != null)
                break;
            yield return null;
        }

        if (scope?.Container == null)
        {
            AddGlobalFailure(
                "runtime-scope-missing",
                $"DungeonRuntimeLifetimeScope was not published after 45 realtime seconds; "
                + $"activeScene={SceneManager.GetActiveScene().path}.");
            yield break;
        }
        runtimeScope = scope;

        worldRegistry = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        CharacterActor[] actors = LiveActors(worldRegistry);
        if (actors.Length < 3)
        {
            string promotion = StartPartyPreparationPlayModeVerifier
                .RunFastCommitForDebug();
            results.Add(WorkTypeLiveRow.Info("baseline-promotion", promotion));
            for (int frame = 0; frame < 8; frame++)
                yield return null;
            actors = LiveActors(worldRegistry);
        }

        if (actors.Length < 3 || actors.Count(value => value.Role == CharacterRole.Owner) != 1)
        {
            AddGlobalFailure(
                "started-party-invalid",
                "actors=" + actors.Length + "; owners="
                + actors.Count(value => value.Role == CharacterRole.Owner));
            yield break;
        }

        saveRegistry = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        actor = SelectStableWorkSubject(actors);
        if (grid == null || actor == null)
        {
            AddGlobalFailure(
                "fixture-world-authority-missing",
                "grid=" + (grid != null) + "; actor=" + (actor != null));
            yield break;
        }
        baseline = saveRegistry.CaptureAll();
        if (!EnsureGrandProjectResearchPrerequisite(
                scope.Container,
                out string researchFixtureDetail))
        {
            AddGlobalFailure(
                "grand-project-research-fixture",
                researchFixtureDetail);
            yield break;
        }
        results.Add(WorkTypeLiveRow.Info(
            "grand-project-research-fixture",
            researchFixtureDetail));
        for (int frame = 0; frame < 4; frame++)
            yield return null;
        actors = LiveActors(worldRegistry);

        productionBills = scope.Container.Resolve<IProductionBillQuery>();
        productionOrders = scope.Container.Resolve<IProductionBillOrderCommand>();
        productionWork = scope.Container.Resolve<IProductionBillWorkExecution>();
        automation = scope.Container.Resolve<IAutomationInfrastructureQuery>();
        automationCommands = scope.Container.Resolve<IAutomationInfrastructureCommand>();
        automationPersistence = scope.Container.Resolve<
            IAutomationInfrastructurePersistence>();
        power = scope.Container.Resolve<IPowerInfrastructureQuery>();
        settlementLabor = scope.Container.Resolve<ISettlementLaborAccountingService>();
        economyContent = scope.Container.Resolve<IResourceEconomyContentCatalog>();
        physicalItems = scope.Container.Resolve<IWorldItemStackRuntime>();
        itemRepository = scope.Container.Resolve<WorldItemRepository>();
        wildlifeRuntime = scope.Container.Resolve<IWildlifeRuntime>();
        carcassService = scope.Container.Resolve<IWildlifeCarcassService>();
        shopStockCatalog = scope.Container.Resolve<IShopStockCatalog>();
        worldResources = scope.Container.Resolve<IWorldResourceRuntime>();
        cropPlots = scope.Container.Resolve<CropPlotRuntime>();
        workOrders = scope.Container.Resolve<IWorkOrderRuntime>();
        characterEnvironmentPersistence = scope.Container
            .Resolve<ICharacterEnvironmentPersistence>();
        characterEnvironmentStatus = scope.Container
            .Resolve<ICharacterEnvironmentStatusQuery>();
        deprivationRuntime = scope.Container
            .Resolve<ICharacterDeprivationRuntime>();
        staffDiscontent = scope.Container
            .Resolve<CharacterSceneRuntimeReferences>()
            .StaffDiscontent;
        actor = SelectStableWorkSubject(actors);
        work = actor != null ? actor.GetComponent<AbilityWork>() : null;
        brain = actor != null ? actor.Brain : null;
        grid = FindFirstObjectByType<GridSystemManager>()?.grid;
        if (actor == null || work == null || brain == null || grid == null)
        {
            AddGlobalFailure(
                "worker-fixture-missing",
                "actor=" + (actor != null) + "; work=" + (work != null)
                + "; brain=" + (brain != null) + "; grid=" + (grid != null));
            yield break;
        }
        results.Add(WorkTypeLiveRow.Info(
            "subject",
            "id=" + (actor.Identity?.PersistentId ?? "-")
                + "; role=" + actor.Role
                + "; lifecycle=" + actor.CurrentLifecycleState));

        if (worldResources is WorldResourceRuntime worldResourceRuntime)
        {
            float resourceDeadline = Time.realtimeSinceStartup + 8f;
            bool published = false;
            while (Time.realtimeSinceStartup < resourceDeadline)
            {
                worldResourceRuntime.Tick();
                published = HasAvailableWorldResource(BuiltInWorkTypeIds.Gather)
                    && HasAvailableWorldResource(BuiltInWorkTypeIds.Logging);
                if (published)
                    break;
                yield return null;
            }
            results.Add(WorkTypeLiveRow.Info(
                "world-resource-fixture",
                "gather=" + HasAvailableWorldResource(BuiltInWorkTypeIds.Gather)
                + "; logging=" + HasAvailableWorldResource(BuiltInWorkTypeIds.Logging)
                + "; nodes=" + worldResources.NodeCount));
        }

        // Remove live-fixture competition without mutating authored targets.
        // The selected subject remains active; every actor's exact prior pause
        // state is restored after the authoritative save baseline is restored.
        actorPauseStates.Clear();
        foreach (CharacterActor liveActor in actors)
        {
            string persistentId = liveActor.Identity?.PersistentId ?? string.Empty;
            actorPauseStates.Add(new ActorPauseState(
                persistentId,
                liveActor.IsAiPaused()));
            if (ReferenceEquals(liveActor, actor))
                continue;

            liveActor.Brain?.StopCurrentActionForReplan(
                "worktype live matrix isolate subject");
            liveActor.SetAiPaused(true);
        }
        results.Add(WorkTypeLiveRow.Info(
            "subject-isolation",
            "subject=" + (actor.Identity?.PersistentId ?? actor.name)
            + "; competitorsPaused=" + (actors.Length - 1)
            + "; priorPauseStatesCaptured=" + actorPauseStates.Count));
        yield return null;

        if (P15AutomationModesOnly)
        {
            IEnumerator focused = RunP15AutomationModesFocused();
            while (focused.MoveNext())
                yield return focused.Current;
            yield break;
        }

        foreach (WorkTypeId workTypeId in Rows)
        {
            if (abortRemainingRows)
                break;
            IEnumerator row = RunRow(workTypeId);
            while (row.MoveNext())
                yield return row.Current;
        }
    }

    private IEnumerator RunP15AutomationModesFocused()
    {
        IEnumerator manual = RunP15NaturalExecutionModeArm(
            AutomationMode.Manual,
            "p15:manual");
        while (manual.MoveNext())
            yield return manual.Current;

        IEnumerator assisted = RunP15NaturalExecutionModeArm(
            AutomationMode.PoweredAssist,
            "p15:powered-assist");
        while (assisted.MoveNext())
            yield return assisted.Current;

        IEnumerator occupied = RunP15AllocatedWorkerTransitionArm();
        while (occupied.MoveNext())
            yield return occupied.Current;

        IEnumerator automatic = RunP15AutomaticExecutionArm();
        while (automatic.MoveNext())
            yield return automatic.Current;

        IEnumerator utilityFailure = RunP15UtilityFailureAtomicArm();
        while (utilityFailure.MoveNext())
            yield return utilityFailure.Current;
    }

    private IEnumerator RunP15NaturalExecutionModeArm(
        AutomationMode mode,
        string rowId)
    {
        P15FocusedFixture focused = null;
        string setupFailure = string.Empty;
        IEnumerator setup = PrepareP15FocusedFixture(
            value => focused = value,
            value => setupFailure = value);
        while (setup.MoveNext())
            yield return setup.Current;
        if (focused == null)
        {
            results.Add(new WorkTypeLiveRow(
                rowId,
                "FAIL",
                "P15",
                "fixture=" + setupFailure));
            yield break;
        }

        InfrastructureCommandResult modeCommand = automationCommands.SetMode(
            focused.Target,
            mode);
        float modeDeadline = Time.realtimeSinceStartup + 5f;
        AutomationFacilitySnapshot modeSnapshot = null;
        while (modeCommand.Succeeded
               && Time.realtimeSinceStartup < modeDeadline)
        {
            if (automation.TryGetFacility(focused.Target, out modeSnapshot)
                && modeSnapshot.Mode == mode
                && modeSnapshot.Powered
                && modeSnapshot.Operational)
            {
                break;
            }
            yield return null;
        }

        float speedMultiplier = automation.GetWorkSpeedMultiplier(focused.Target);
        ProductionBillSnapshot billBefore = FindFocusedBill(focused);
        SettlementLaborAccountingSnapshot laborBefore = settlementLabor.Capture();
        PrepareActor(BuiltInWorkTypeIds.Cook);
        WorkPhaseResult phase = null;
        IEnumerator phaseRun = RunLivePhase(
            BuiltInWorkTypeIds.Cook,
            focused.Target,
            WorkProbeFault.CancelAfterApprovedProgress,
            fixture: null,
            value => phase = value);
        while (phaseRun.MoveNext())
            yield return phaseRun.Current;
        ProductionBillSnapshot billAfter = FindFocusedBill(focused);
        SettlementLaborAccountingSnapshot laborAfter = settlementLabor.Capture();

        long actualDelta = DeltaCounterAcrossReset(
            laborBefore.ActualLaborMilliWu,
            laborAfter.ActualLaborMilliWu);
        long automaticDelta = DeltaCounterAcrossReset(
            laborBefore.DomainAutomationMilliWu,
            laborAfter.DomainAutomationMilliWu);
        float completedDelta = billBefore != null && billAfter != null
            ? billAfter.CompletedWork - billBefore.CompletedWork
            : 0f;
        bool modeReady = modeSnapshot != null
            && modeSnapshot.Mode == mode
            && modeSnapshot.Powered
            && modeSnapshot.Operational;
        bool multiplierValid = mode == AutomationMode.Manual
            ? Mathf.Approximately(speedMultiplier, 1f)
            : speedMultiplier > 1f;
        bool passed = modeCommand.Succeeded
            && modeReady
            && multiplierValid
            && phase?.Passed == true
            && completedDelta > 0f
            && actualDelta > 0L
            && automaticDelta == 0L;

        string detail = "modeCommand=" + modeCommand.Succeeded
            + "; modeReady=" + modeReady
            + "; multiplier=" + speedMultiplier.ToString("0.###")
            + "; naturalPhasePass=" + (phase?.Passed == true)
            + "; billProgress=" + (completedDelta > 0f)
            + "; actualLaborPositive=" + (actualDelta > 0L)
            + "; automaticLaborZero=" + (automaticDelta == 0L);
        if (!CloseP15FocusedFixture(focused, out string cleanupFailure))
        {
            passed = false;
            detail += "; cleanup=" + cleanupFailure;
        }
        results.Add(new WorkTypeLiveRow(
            rowId,
            passed ? "PASS" : "FAIL",
            "P15",
            detail));
    }

    private IEnumerator RunP15AllocatedWorkerTransitionArm()
    {
        const string RowId = "p15:allocated-worker-transition";
        P15FocusedFixture focused = null;
        string setupFailure = string.Empty;
        IEnumerator setup = PrepareP15FocusedFixture(
            value => focused = value,
            value => setupFailure = value);
        while (setup.MoveNext())
            yield return setup.Current;
        if (focused == null)
        {
            results.Add(new WorkTypeLiveRow(
                RowId,
                "FAIL",
                "P15",
                "fixture=" + setupFailure));
            yield break;
        }

        InfrastructureCommandResult manual = automationCommands.SetMode(
            focused.Target,
            AutomationMode.Manual);
        PrepareActor(BuiltInWorkTypeIds.Cook);
        WorkPhaseResult phase = null;
        bool occupancyOnlyObserved = false;
        bool transitionAttempted = false;
        InfrastructureCommandResult transition = default;
        IEnumerator phaseRun = RunLivePhase(
            BuiltInWorkTypeIds.Cook,
            focused.Target,
            WorkProbeFault.CancelAfterApprovedProgress,
            fixture: null,
            value => phase = value);
        while (phaseRun.MoveNext())
        {
            if (!transitionAttempted
                && focused.Target is IAllocatedWorkerOccupancyQuery occupancy
                && occupancy.HasAllocatedWorker
                && focused.Target.WorkerReservation == null
                && productionBills.GetBills(focused.Target).All(value =>
                    string.IsNullOrWhiteSpace(value.ReservedWorkerId)))
            {
                occupancyOnlyObserved = true;
                transitionAttempted = true;
                transition = automationCommands.SetMode(
                    focused.Target,
                    AutomationMode.Automatic);
            }
            yield return phaseRun.Current;
        }

        bool typedRejection = transitionAttempted
            && !transition.Succeeded
            && transition.Failure.Code == FailureCode.AutomationModeUnsupported
            && DomainFailureContains(
                transition.Failure,
                "automatic-mode-manual-worker-active");
        bool passed = manual.Succeeded
            && occupancyOnlyObserved
            && typedRejection
            && phase?.Passed == true;
        string detail = "manual=" + manual.Succeeded
            + "; occupancyOnly=" + occupancyOnlyObserved
            + "; transitionAttempted=" + transitionAttempted
            + "; typedRejection=" + typedRejection
            + "; naturalPhasePass=" + (phase?.Passed == true);
        if (!CloseP15FocusedFixture(focused, out string cleanupFailure))
        {
            passed = false;
            detail += "; cleanup=" + cleanupFailure;
        }
        results.Add(new WorkTypeLiveRow(
            RowId,
            passed ? "PASS" : "FAIL",
            "P15",
            detail));
    }

    private IEnumerator RunP15AutomaticExecutionArm()
    {
        const string RowId = "p15:automatic";
        P15FocusedFixture focused = null;
        string setupFailure = string.Empty;
        IEnumerator setup = PrepareP15FocusedFixture(
            value => focused = value,
            value => setupFailure = value);
        while (setup.MoveNext())
            yield return setup.Current;
        if (focused == null)
        {
            results.Add(new WorkTypeLiveRow(
                RowId,
                "FAIL",
                "P15",
                "fixture=" + setupFailure));
            yield break;
        }

        InfrastructureCommandResult automatic = automationCommands.SetMode(
            focused.Target,
            AutomationMode.Automatic);
        float modeDeadline = Time.realtimeSinceStartup + 5f;
        AutomationFacilitySnapshot modeSnapshot = null;
        while (automatic.Succeeded
               && Time.realtimeSinceStartup < modeDeadline)
        {
            if (automation.TryGetFacility(focused.Target, out modeSnapshot)
                && modeSnapshot.Mode == AutomationMode.Automatic
                && modeSnapshot.Powered
                && modeSnapshot.Operational)
            {
                break;
            }
            yield return null;
        }

        PrepareActor(BuiltInWorkTypeIds.Cook);
        runtimeScope.Container.Resolve<IFacilityCandidateCache>()
            .MarkDynamicStateDirty();
        yield return null;
        ProductionWorkAvailabilityResult availability =
            productionWork.CheckWorkAvailability(
                focused.Target,
                BuiltInWorkTypeIds.Cook);
        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        bool foundCandidate = work.TryGetBestWorkCandidate(
            BuiltInWorkTypeIds.Cook,
            search,
            out WorkTargetCandidate candidate);
        BuildableObject candidateTarget = foundCandidate
            ? WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate)
            : null;
        bool priorityAccepted = work.TrySetPriorityWorkTarget(
            focused.Target,
            BuiltInWorkTypeIds.Cook,
            search,
            out string priorityFailure);
        ProductionWorkBeginResult begin = productionWork.BeginWork(
            actor,
            focused.Target,
            BuiltInWorkTypeIds.Cook);
        ProductionWorkExecutionResult execute = productionWork.ExecuteWork(
            actor,
            focused.Target,
            focused.BillId,
            0.25f);
        bool typedManualRejection = !availability.Available
            && DomainFailureContains(
                availability.Failure,
                ProductionWorkstationExecutionModeRules
                    .ManualDisabledByAutomaticMode)
            && !begin.Succeeded
            && DomainFailureContains(
                begin.Failure,
                ProductionWorkstationExecutionModeRules
                    .ManualDisabledByAutomaticMode)
            && !execute.Succeeded
            && DomainFailureContains(
                execute.Failure,
                ProductionWorkstationExecutionModeRules
                    .ManualDisabledByAutomaticMode)
            && !priorityAccepted;
        bool naturalTargetExcluded = !foundCandidate
            || !ReferenceEquals(candidateTarget, focused.Target);

        ProductionBillSnapshot billBefore = FindFocusedBill(focused);
        SettlementLaborAccountingSnapshot laborBefore = settlementLabor.Capture();
        long approvedBefore = work.ApprovedWorkProgressRevisionForDiagnostics;
        actor.SetAiPaused(false);
        brain.PreferWorkActionOnNextDecision(BuiltInWorkTypeIds.Cook, 2f);
        brain.RequestImmediateReplan(clearFailures: true);
        float progressDeadline = Time.realtimeSinceStartup + 12f;
        bool automaticProgressed = false;
        while (Time.realtimeSinceStartup < progressDeadline)
        {
            ProductionBillSnapshot current = FindFocusedBill(focused);
            SettlementLaborAccountingSnapshot currentLabor = settlementLabor.Capture();
            automaticProgressed = BillTokenChanged(billBefore, current)
                || DeltaCounterAcrossReset(
                    laborBefore.DomainAutomationMilliWu,
                    currentLabor.DomainAutomationMilliWu) > 0L;
            if (automaticProgressed)
                break;
            yield return null;
        }
        brain.StopCurrentActionForReplan(
            "p15 automatic execution-mode exclusion observation complete");
        actor.SetAiPaused(true);
        for (int frame = 0; frame < 2; frame++)
            yield return null;

        SettlementLaborAccountingSnapshot laborAfter = settlementLabor.Capture();
        long actualDelta = DeltaCounterAcrossReset(
            laborBefore.ActualLaborMilliWu,
            laborAfter.ActualLaborMilliWu);
        long automaticDelta = DeltaCounterAcrossReset(
            laborBefore.DomainAutomationMilliWu,
            laborAfter.DomainAutomationMilliWu);
        bool noNaturalProgress = work.ApprovedWorkProgressRevisionForDiagnostics
            == approvedBefore
            && !ReferenceEquals(work.assignedShop, focused.Target);
        bool noManualOwnership = focused.Target.WorkerReservation == null
            && (!(focused.Target is IAllocatedWorkerOccupancyQuery occupancy)
                || !occupancy.HasAllocatedWorker)
            && productionBills.GetBills(focused.Target).All(value =>
                string.IsNullOrWhiteSpace(value.ReservedWorkerId));

        DungeonAutomationSaveData savedMode = automationPersistence.Capture();
        InfrastructureCommandResult switchedManual = automationCommands.SetMode(
            focused.Target,
            AutomationMode.Manual);
        AutomationRestoreCandidate restoredMode = automationPersistence
            .PrepareRestore(savedMode);
        automationPersistence.Restore(restoredMode);
        yield return null;
        bool restoreExact = automation.TryGetFacility(
                focused.Target,
                out AutomationFacilitySnapshot restoredSnapshot)
            && restoredSnapshot.Mode == AutomationMode.Automatic
            && !productionWork.CheckWorkAvailability(
                focused.Target,
                BuiltInWorkTypeIds.Cook).Available;

        bool modeReady = modeSnapshot != null
            && modeSnapshot.Mode == AutomationMode.Automatic
            && modeSnapshot.Powered
            && modeSnapshot.Operational;
        bool passed = automatic.Succeeded
            && modeReady
            && typedManualRejection
            && naturalTargetExcluded
            && noNaturalProgress
            && automaticProgressed
            && automaticDelta > 0L
            && actualDelta == 0L
            && noManualOwnership
            && switchedManual.Succeeded
            && restoreExact;
        string detail = "modeCommand=" + automatic.Succeeded
            + "; modeReady=" + modeReady
            + "; typedManualRejection=" + typedManualRejection
            + "; priorityAccepted=" + priorityAccepted
            + "; naturalTargetExcluded=" + naturalTargetExcluded
            + "; noNaturalProgress=" + noNaturalProgress
            + "; automaticProgress=" + automaticProgressed
            + "; actualLaborZero=" + (actualDelta == 0L)
            + "; automaticLaborPositive=" + (automaticDelta > 0L)
            + "; noManualOwnership=" + noManualOwnership
            + "; restoreExact=" + restoreExact;
        if (!CloseP15FocusedFixture(focused, out string cleanupFailure))
        {
            passed = false;
            detail += "; cleanup=" + cleanupFailure;
        }
        results.Add(new WorkTypeLiveRow(
            RowId,
            passed ? "PASS" : "FAIL",
            "P15",
            detail));
    }

    private IEnumerator PrepareP15FocusedFixture(
        Action<P15FocusedFixture> completed,
        Action<string> failed)
    {
        if (!CleanupRowScopedFixtures(out string priorCleanupFailure))
        {
            failed("prior-cleanup=" + priorCleanupFailure);
            yield break;
        }
        if (!CleanupRoomFixture(out string priorRoomCleanupFailure))
        {
            failed("prior-room-cleanup=" + priorRoomCleanupFailure);
            yield break;
        }
        MaintainStableWorkSubject();
        actor.SetAiPaused(true);
        brain.StopCurrentActionForReplan(
            "P15 focused fixture deterministic setup");
        actor.GetAbility<AbilityMove>()?.CancelActiveMovement(
            "P15 focused fixture deterministic setup");
        yield return null;
        yield return null;
        if (!TryPlacePoweredP15Pair(
                out BuildableObject target,
                out string placementFailure))
        {
            failed("placement=" + placementFailure);
            yield break;
        }

        float powerDeadline = Time.realtimeSinceStartup + 8f;
        while (!power.IsPowered(target)
               && Time.realtimeSinceStartup < powerDeadline)
        {
            yield return null;
        }
        if (!power.IsPowered(target))
        {
            failed("P15 did not join the adjacent fuel-free I02 power network");
            yield break;
        }

        if (!TryPrepareProductionFixture(
                BuiltInWorkTypeIds.Cook,
                out MaterialWorkFixture fixture,
                out string fixtureFailure,
                target))
        {
            failed("bill=" + fixtureFailure);
            yield break;
        }
        ProductionBillSnapshot bill = productionBills.GetBills(target)
            .SingleOrDefault(value => value.RecipeId == "recipe:tallow");
        if (bill == null)
        {
            failed("prepared P15 tallow bill is missing");
            yield break;
        }

        runtimeScope.Container.Resolve<IFacilityCandidateCache>()
            .MarkDynamicStateDirty();
        yield return null;
        completed(new P15FocusedFixture(target, fixture, bill.BillId));
    }

    private IEnumerator RunP15UtilityFailureAtomicArm()
    {
        const string RowId = "p15:utility-failure-atomic";
        P15FocusedFixture focused = null;
        string setupFailure = string.Empty;
        IEnumerator setup = PrepareP15FocusedFixture(
            value => focused = value,
            value => setupFailure = value);
        while (setup.MoveNext())
            yield return setup.Current;
        if (focused == null)
        {
            results.Add(new WorkTypeLiveRow(
                RowId,
                "FAIL",
                "P15",
                "fixture=" + setupFailure));
            yield break;
        }

        InfrastructureCommandResult manual = automationCommands.SetMode(
            focused.Target,
            AutomationMode.Manual);
        IFluidWastewaterTransaction wastewater =
            runtimeScope.Container.Resolve<IFluidWastewaterTransaction>();
        bool fullyAccepted = wastewater.TryAddWastewater(
            focused.Target,
            100000f,
            out float acceptedWastewater,
            out DomainFailure overflowFailure);
        ProductionWorkAvailabilityResult availability =
            productionWork.CheckWorkAvailability(
                focused.Target,
                BuiltInWorkTypeIds.Cook);
        ProductionBillSnapshot billBefore = FindFocusedBill(focused);
        string physicalBefore = CaptureP15PhysicalStackFingerprint();
        int sludgeBefore = CaptureP15ItemQuantity(
            IndustrialItemDefinitions.SludgeId);
        int waterBefore = CaptureP15ItemQuantity("resource:clean-water");
        bool delegatedWork = false;
        IWorkExecutionHandlerRegistry handlers =
            runtimeScope.Container.Resolve<IWorkExecutionHandlerRegistry>();
        bool handlerResolved = handlers.TryGet(
            BuiltInWorkTypeIds.Cook,
            out IWorkExecutionHandler handler);
        WorkExecutionResult executionResult = new();
        if (handlerResolved)
        {
            WorkExecutionContext context = new(
                0,
                work,
                actor,
                focused.Target,
                BuiltInWorkTypeIds.Cook,
                (required, label, multiplier) =>
                {
                    delegatedWork = true;
                    return EmptyP15FocusedCoroutine();
                },
                () => true,
                (required, completed, label, multiplier, apply) =>
                {
                    delegatedWork = true;
                    return EmptyP15FocusedCoroutine();
                });
            IEnumerator execution = handler.Execute(context, executionResult);
            while (execution.MoveNext())
                yield return execution.Current;
        }

        ProductionBillSnapshot billAfter = FindFocusedBill(focused);
        string physicalAfter = CaptureP15PhysicalStackFingerprint();
        int sludgeAfter = CaptureP15ItemQuantity(
            IndustrialItemDefinitions.SludgeId);
        int waterAfter = CaptureP15ItemQuantity("resource:clean-water");
        bool utilityRejected = !availability.Available
            && availability.Failure.Code
                == FailureCode.ProductionUtilitiesUnavailable
            && DomainFailureContains(
                availability.Failure,
                FailureCode.FluidWastewaterUnavailable.ToString());
        bool billUnchanged = billBefore != null
            && billAfter != null
            && billAfter.BillId == billBefore.BillId
            && billAfter.MaterialsConsumed == billBefore.MaterialsConsumed
            && billAfter.ProcessFluidConsumed == billBefore.ProcessFluidConsumed
            && Mathf.Approximately(
                billAfter.CompletedWork,
                billBefore.CompletedWork)
            && billAfter.RemainingCycles == billBefore.RemainingCycles;
        bool passed = manual.Succeeded
            && !fullyAccepted
            && acceptedWastewater > 0f
            && overflowFailure.IsFailure
            && utilityRejected
            && handlerResolved
            && !executionResult.CompletedSuccessfully
            && !delegatedWork
            && billUnchanged
            && string.Equals(
                physicalAfter,
                physicalBefore,
                StringComparison.Ordinal)
            && sludgeAfter == sludgeBefore
            && waterAfter == waterBefore;
        string detail = "manual=" + manual.Succeeded
            + "; tankFilled=" + (!fullyAccepted && acceptedWastewater > 0f)
            + "; overflowTyped=" + overflowFailure.IsFailure
            + "; utilityRejected=" + utilityRejected
            + "; handlerResolved=" + handlerResolved
            + "; handlerRejected=" + !executionResult.CompletedSuccessfully
            + "; delegatedWork=" + delegatedWork
            + "; billUnchanged=" + billUnchanged
            + "; physicalUnchanged=" + string.Equals(
                physicalAfter,
                physicalBefore,
                StringComparison.Ordinal)
            + "; waterUnchanged=" + (waterAfter == waterBefore)
            + "; sludgeUnchanged=" + (sludgeAfter == sludgeBefore);
        if (!CloseP15FocusedFixture(focused, out string cleanupFailure))
        {
            passed = false;
            detail += "; cleanup=" + cleanupFailure;
        }
        results.Add(new WorkTypeLiveRow(
            RowId,
            passed ? "PASS" : "FAIL",
            "P15",
            detail));
    }

    private static IEnumerator EmptyP15FocusedCoroutine()
    {
        yield break;
    }

    private string CaptureP15PhysicalStackFingerprint() => string.Join(
        "|",
        physicalItems.GetAllStacks()
            .Where(value => value != null)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => value.StackId + ":" + value.ItemId + ":"
                + value.Quantity + ":" + value.AvailableQuantity + ":"
                + (int)value.State + ":" + value.DestinationId));

    private int CaptureP15ItemQuantity(string itemId) => physicalItems
        .GetAllStacks()
        .Where(value => value != null
            && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
        .Sum(value => value.Quantity);

    private bool TryPlacePoweredP15Pair(
        out BuildableObject target,
        out string failureReason)
    {
        target = null;
        failureReason = string.Empty;
        BuildingSO generatorData = LoadAuthoredBuilding(value =>
            string.Equals(
                value.GetAbility<BuildingFacilityPartAbility>()?.code,
                "I02",
                StringComparison.Ordinal));
        BuildingSO p15Data = LoadAuthoredBuilding(value =>
            string.Equals(
                value.GetAbility<BuildingFacilityPartAbility>()?.code,
                "P15",
                StringComparison.Ordinal));
        BuildingSO wastewaterTankData = LoadAuthoredBuilding(value =>
            string.Equals(
                value.GetAbility<BuildingFacilityPartAbility>()?.code,
                "I09",
                StringComparison.Ordinal)
            && value.GetAbility<BuildingWaterStorageAbility>() is
            {
                wastewaterCapacity: > 0f
            } storage
            && (storage.channels & UtilityChannel.Wastewater) != 0);
        BuildingSO wastewaterDuctData = LoadAuthoredBuilding(value =>
            string.Equals(
                value.GetAbility<BuildingFacilityPartAbility>()?.code,
                "U04",
                StringComparison.Ordinal)
            && (value.GetAbility<BuildingUtilityConnectionAbility>()?.channels
                & UtilityChannel.Wastewater) != 0);
        if (generatorData == null
            || p15Data == null
            || wastewaterTankData == null
            || wastewaterDuctData == null)
        {
            failureReason = "I02, P15, I09, or U04 authored building is missing";
            return false;
        }

        Vector2Int[] anchors = grid.GetCells()
            .Where(cell => cell != null)
            .Select(cell => cell.Position)
            .OrderBy(position => position.y)
            .ThenBy(position => position.x)
            .ToArray();
        foreach (Vector2Int generatorAnchor in anchors)
        {
            IReadOnlyList<Vector2Int> generatorCells =
                generatorData.GetGridPosList(generatorAnchor);
            if (generatorCells.Count == 0)
                continue;
            Vector2Int p15Anchor = new(
                generatorCells.Max(position => position.x) + 1,
                generatorCells.Min(position => position.y));
            IReadOnlyList<Vector2Int> p15Cells = p15Data.GetGridPosList(p15Anchor);
            Vector2Int wastewaterTankAnchor = new(
                p15Cells.Max(position => position.x) + 1,
                p15Cells.Min(position => position.y));
            IReadOnlyList<Vector2Int> wastewaterTankCells =
                wastewaterTankData.GetGridPosList(wastewaterTankAnchor);
            HashSet<Vector2Int> infrastructureCells = generatorCells
                .Concat(p15Cells)
                .Concat(wastewaterTankCells)
                .ToHashSet();
            if (infrastructureCells.Count
                    != generatorCells.Count + p15Cells.Count
                        + wastewaterTankCells.Count
                || generatorCells.Any(position =>
                    grid.GetGridCell(position) is not GridCell cell
                    || !cell.CanBuildInArea(generatorData)
                    || !cell.CanOccupy(generatorData.Placement.Layer))
                || p15Cells.Any(position =>
                    grid.GetGridCell(position) is not GridCell cell
                    || !cell.CanBuildInArea(p15Data)
                    || !cell.CanOccupy(p15Data.Placement.Layer))
                || wastewaterTankCells.Any(position =>
                    grid.GetGridCell(position) is not GridCell cell
                    || !cell.CanBuildInArea(wastewaterTankData)
                    || !cell.CanOccupy(wastewaterTankData.Placement.Layer)))
            {
                continue;
            }

            BuildableObject generator = PlaceAuthoredBuildingAt(
                generatorData,
                generatorAnchor,
                out string generatorFailure);
            if (generator == null)
                continue;
            rowScopedFixtureBuildings.Add(generator);
            BuildableObject p15 = PlaceAuthoredBuildingAt(
                p15Data,
                p15Anchor,
                out string p15Failure);
            if (p15 == null)
            {
                CleanupRowScopedFixtures(out _);
                failureReason = "P15 exact placement failed:" + p15Failure;
                continue;
            }
            rowScopedFixtureBuildings.Add(p15);
            BuildableObject wastewaterTank = PlaceAuthoredBuildingAt(
                wastewaterTankData,
                wastewaterTankAnchor,
                out string wastewaterTankFailure);
            if (wastewaterTank == null)
            {
                CleanupRowScopedFixtures(out _);
                failureReason = "I09 exact placement failed:"
                    + wastewaterTankFailure;
                continue;
            }
            rowScopedFixtureBuildings.Add(wastewaterTank);
            if (!TryOverlayP15WastewaterDucts(
                    p15,
                    wastewaterDuctData,
                    infrastructureCells,
                    out string wastewaterFailure))
            {
                CleanupRowScopedFixtures(out _);
                failureReason = "P15 wastewater overlay failed:"
                    + wastewaterFailure;
                continue;
            }
            GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
            if (!WorkTargetSelectionRules.IsReachable(p15, search))
            {
                CleanupRowScopedFixtures(out _);
                failureReason = "adjacent powered P15 has no reachable work access";
                continue;
            }

            target = p15;
            return true;
        }

        if (TryPlaceP15PairInVerifierSpan(
                generatorData,
                p15Data,
                wastewaterTankData,
                wastewaterDuctData,
                out target,
                out string spanFailure))
        {
            return true;
        }

        failureReason = "no reachable adjacent I02+P15 authored placement; span="
            + spanFailure;
        return false;
    }

    private bool TryPlaceP15PairInVerifierSpan(
        BuildingSO generatorData,
        BuildingSO p15Data,
        BuildingSO wastewaterTankData,
        BuildingSO wastewaterDuctData,
        out BuildableObject target,
        out string failureReason)
    {
        target = null;
        failureReason = "no safe six-cell verifier span";
        GridPathSearchResult preMutationSearch = grid.SearchPath(actor.GetNowXY());
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x <= grid.width - 6; x++)
            {
                // Dungeon y is a floor rather than north/south space. Reserve
                // the reachable stand on the actor-facing left edge, then put
                // P15 before I02 so the five-cell powered pair cannot cut the
                // actor off from the workstation stand.
                Vector2Int access = new(x, y);
                if (!grid.IsValidGridPos(access)
                    || !grid.IsWalkable(access)
                    || !preMutationSearch.ContainsPosition(access))
                {
                    continue;
                }

                HashSet<BuildableObject> displacements = new();
                bool safe = true;
                for (int offset = 1; offset <= 5 && safe; offset++)
                {
                    Vector2Int position = new(x + offset, y);
                    GridCell cell = grid.GetGridCell(position);
                    if (cell == null)
                    {
                        safe = false;
                        break;
                    }
                    foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                    {
                        IGridOccupant occupant = cell.GetOccupant(layer);
                        if (occupant == null)
                            continue;
                        if ((layer == GridLayer.Building
                                || layer == GridLayer.Hallway)
                            && occupant is BuildableObject movement
                            && movement is not Facility
                            && movement is not Door
                            && movement.Facility == null
                            && movement.IsGridMovement
                            && !movement.BlocksGridMovement)
                        {
                            displacements.Add(movement);
                            continue;
                        }
                        if (layer == generatorData.Placement.Layer
                            || layer == p15Data.Placement.Layer
                            || layer == wastewaterDuctData.Placement.Layer
                            || layer == GridLayer.Character
                            || layer == GridLayer.Wildlife)
                        {
                            safe = false;
                            break;
                        }
                    }
                }
                if (!safe)
                    continue;

                for (int offset = 1; offset <= 5; offset++)
                {
                    Vector2Int position = new(x + offset, y);
                    GridCell cell = grid.GetGridCell(position);
                    roomAreaSnapshots.Add(new FixtureAreaSnapshot(
                        position,
                        cell.AreaType));
                    grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
                }
                bool displaced = true;
                foreach (BuildableObject movement in displacements
                             .OrderBy(value => value.centerPos.y)
                             .ThenBy(value => value.centerPos.x)
                             .ThenBy(value => value.GridId))
                {
                    GridLayer layer = movement.BuildingData.Placement.Layer;
                    Vector2Int[] positions = movement.buildPoses.ToArray();
                    if (!grid.RemoveOccupant(
                            movement,
                            layer,
                            positions,
                            movement.BuildingData.Placement.IsMovement))
                    {
                        displaced = false;
                        failureReason = "movement displacement failed:"
                            + movement.GridId;
                        break;
                    }
                    displacedRoomMovements.Add(new DisplacedMovementSnapshot(
                        movement,
                        layer,
                        positions,
                        movement.BuildingData.Placement.IsMovement));
                }
                if (!displaced)
                {
                    CleanupRoomFixture(out _);
                    continue;
                }

                // This focused verifier owns execution-mode admission, not a
                // long-distance routing benchmark. Move the isolated subject
                // onto the already-proven reachable stand before the five-cell
                // pair closes the one-dimensional floor corridor; the whole
                // save baseline restores the exact original actor position.
                actor.transform.position = grid.GetWorldPos(access);
                brain.ClearPathSearchCache();

                Vector2Int p15Anchor = new(x + 1, y);
                Vector2Int generatorAnchor = new(x + 3, y);
                BuildableObject generator = PlaceAuthoredBuildingAt(
                    generatorData,
                    generatorAnchor,
                    out string generatorFailure);
                if (generator == null)
                {
                    failureReason = "span I02 placement failed:" + generatorFailure;
                    CleanupRoomFixture(out _);
                    continue;
                }
                rowScopedFixtureBuildings.Add(generator);
                BuildableObject p15 = PlaceAuthoredBuildingAt(
                    p15Data,
                    p15Anchor,
                    out string p15Failure);
                if (p15 == null)
                {
                    failureReason = "span P15 placement failed:" + p15Failure;
                    CleanupRowScopedFixtures(out _);
                    CleanupRoomFixture(out _);
                    continue;
                }
                rowScopedFixtureBuildings.Add(p15);
                if (!TryPlaceP15WastewaterTankAndRoute(
                        p15,
                        wastewaterTankData,
                        wastewaterDuctData,
                        out string wastewaterFailure))
                {
                    failureReason = "span P15 wastewater route failed:"
                        + wastewaterFailure;
                    CleanupRowScopedFixtures(out _);
                    CleanupRoomFixture(out _);
                    continue;
                }
                GridPathSearchResult postSearch = grid.SearchPath(actor.GetNowXY());
                if (!WorkTargetSelectionRules.IsReachable(p15, postSearch))
                {
                    failureReason = "span P15 work access is unreachable";
                    CleanupRowScopedFixtures(out _);
                    CleanupRoomFixture(out _);
                    continue;
                }

                target = p15;
                return true;
            }
        }
        return false;
    }

    private bool TryPlaceP15WastewaterTankAndRoute(
        BuildableObject p15,
        BuildingSO wastewaterTankData,
        BuildingSO wastewaterDuctData,
        out string failureReason)
    {
        failureReason = "no same-floor I09 route";
        IReadOnlyList<Vector2Int> p15Cells = p15.BuildingData.GetGridPosList(
            p15.centerPos);
        int p15MinX = p15Cells.Min(value => value.x);
        int p15MaxX = p15Cells.Max(value => value.x);
        int floor = p15Cells.Min(value => value.y);
        Vector2Int[] anchors = Enumerable.Range(0, grid.width)
            .Select(x => new Vector2Int(x, floor))
            .OrderBy(value => Mathf.Min(
                Mathf.Abs(value.x - p15MinX),
                Mathf.Abs(value.x - p15MaxX)))
            .ThenBy(value => value.x)
            .ToArray();

        foreach (Vector2Int tankAnchor in anchors)
        {
            IReadOnlyList<Vector2Int> tankCells =
                wastewaterTankData.GetGridPosList(tankAnchor);
            if (tankCells.Count == 0
                || tankCells.Any(value => value.y != floor
                    || grid.GetGridCell(value) == null))
            {
                continue;
            }

            int tankMinX = tankCells.Min(value => value.x);
            int tankMaxX = tankCells.Max(value => value.x);
            int routeMinX = Mathf.Min(p15MinX, tankMinX);
            int routeMaxX = Mathf.Max(p15MaxX, tankMaxX);
            Vector2Int[] routeCells = Enumerable
                .Range(routeMinX, routeMaxX - routeMinX + 1)
                .Select(x => new Vector2Int(x, floor))
                .ToArray();
            bool routeAvailable = routeCells.All(position =>
            {
                GridCell cell = grid.GetGridCell(position);
                IGridOccupant utility = cell?.GetOccupant(GridLayer.Utility);
                if (utility == null)
                    return cell != null;
                return utility is BuildableObject existing
                    && (existing.BuildingData?.GetAbility<
                            BuildingUtilityConnectionAbility>()?.channels
                        & UtilityChannel.Wastewater) != 0;
            });
            if (!routeAvailable)
                continue;

            HashSet<BuildableObject> displacements = new();
            bool tankAreaAvailable = true;
            foreach (Vector2Int position in tankCells)
            {
                GridCell cell = grid.GetGridCell(position);
                foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                {
                    IGridOccupant occupant = cell.GetOccupant(layer);
                    if (occupant == null)
                        continue;
                    if ((layer == GridLayer.Building
                            || layer == GridLayer.Hallway)
                        && occupant is BuildableObject movement
                        && movement is not Facility
                        && movement is not Door
                        && movement.Facility == null
                        && movement.IsGridMovement
                        && !movement.BlocksGridMovement)
                    {
                        displacements.Add(movement);
                        continue;
                    }
                    if (layer == wastewaterTankData.Placement.Layer
                        || layer == GridLayer.Character
                        || layer == GridLayer.Wildlife)
                    {
                        tankAreaAvailable = false;
                        break;
                    }
                }
                if (!tankAreaAvailable)
                    break;
            }
            if (!tankAreaAvailable)
                continue;

            foreach (Vector2Int position in tankCells)
            {
                GridCell cell = grid.GetGridCell(position);
                roomAreaSnapshots.Add(new FixtureAreaSnapshot(
                    position,
                    cell.AreaType));
                grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
            }
            bool displaced = true;
            foreach (BuildableObject movement in displacements
                         .OrderBy(value => value.centerPos.y)
                         .ThenBy(value => value.centerPos.x)
                         .ThenBy(value => value.GridId))
            {
                GridLayer layer = movement.BuildingData.Placement.Layer;
                Vector2Int[] positions = movement.buildPoses.ToArray();
                if (!grid.RemoveOccupant(
                        movement,
                        layer,
                        positions,
                        movement.BuildingData.Placement.IsMovement))
                {
                    displaced = false;
                    failureReason = "I09 movement displacement failed:"
                        + movement.GridId;
                    break;
                }
                displacedRoomMovements.Add(new DisplacedMovementSnapshot(
                    movement,
                    layer,
                    positions,
                    movement.BuildingData.Placement.IsMovement));
            }
            if (!displaced)
                return false;

            BuildableObject tank = PlaceAuthoredBuildingAt(
                wastewaterTankData,
                tankAnchor,
                out string tankFailure);
            if (tank == null)
            {
                failureReason = "I09 placement failed:" + tankFailure;
                return false;
            }
            rowScopedFixtureBuildings.Add(tank);

            foreach (Vector2Int position in routeCells)
            {
                IGridOccupant existingUtility = grid.GetGridCell(position)
                    ?.GetOccupant(GridLayer.Utility);
                if (existingUtility != null)
                    continue;
                BuildableObject duct = PlaceAuthoredBuildingAt(
                    wastewaterDuctData,
                    position,
                    out string ductFailure);
                if (duct == null)
                {
                    failureReason = "U04 route placement failed at "
                        + position + ":" + ductFailure;
                    return false;
                }
                rowScopedFixtureBuildings.Add(duct);
            }

            IFluidWastewaterTransaction wastewater =
                runtimeScope.Container.Resolve<IFluidWastewaterTransaction>();
            if (wastewater.CanAcceptWastewater(
                    p15,
                    0.25f,
                    out DomainFailure wastewaterFailure))
            {
                return true;
            }
            failureReason = wastewaterFailure.ToString();
            return false;
        }
        return false;
    }

    private bool TryOverlayP15WastewaterDucts(
        BuildableObject p15,
        BuildingSO wastewaterDuctData,
        IEnumerable<Vector2Int> infrastructureCells,
        out string failureReason)
    {
        failureReason = string.Empty;
        foreach (Vector2Int position in infrastructureCells
                     .Distinct()
                     .OrderBy(value => value.y)
                     .ThenBy(value => value.x))
        {
            BuildableObject duct = PlaceAuthoredBuildingAt(
                wastewaterDuctData,
                position,
                out string ductFailure);
            if (duct == null)
            {
                failureReason = "U04 placement failed at " + position
                    + ":" + ductFailure;
                return false;
            }
            rowScopedFixtureBuildings.Add(duct);
        }

        IFluidWastewaterTransaction wastewater =
            runtimeScope.Container.Resolve<IFluidWastewaterTransaction>();
        if (!wastewater.CanAcceptWastewater(
                p15,
                0.25f,
                out DomainFailure wastewaterFailure))
        {
            failureReason = wastewaterFailure.IsFailure
                ? wastewaterFailure.ToString()
                : FailureCode.FluidWastewaterUnavailable.ToString();
            return false;
        }
        return true;
    }

    private ProductionBillSnapshot FindFocusedBill(P15FocusedFixture focused) =>
        focused == null
            ? null
            : productionBills.GetBills(focused.Target)
                .FirstOrDefault(value => value.BillId == focused.BillId);

    private bool CloseP15FocusedFixture(
        P15FocusedFixture focused,
        out string failureReason)
    {
        actor?.SetAiPaused(true);
        brain?.StopCurrentActionForReplan("P15 focused fixture cleanup");
        work?.ClearPriorityWorkTarget();
        string billFailure = string.Empty;
        bool billClosed = focused?.Fixture == null
            || focused.Fixture.TryInvalidate(out billFailure);
        bool fixturesClosed = CleanupRowScopedFixtures(out string fixtureFailure);
        bool roomClosed = CleanupRoomFixture(out string roomFailure);
        failureReason = string.Join(
            ";",
            new[]
            {
                billClosed ? string.Empty : "bill=" + billFailure,
                fixturesClosed ? string.Empty : "fixtures=" + fixtureFailure,
                roomClosed ? string.Empty : "room=" + roomFailure
            }.Where(value => value.Length > 0));
        return billClosed && fixturesClosed && roomClosed;
    }

    private static bool BillTokenChanged(
        ProductionBillSnapshot before,
        ProductionBillSnapshot after) =>
        before != null
        && (after == null
            || after.CompletedWork > before.CompletedWork
            || after.RemainingCycles < before.RemainingCycles);

    private static long DeltaCounterAcrossReset(long before, long after) =>
        after >= before ? after - before : Math.Max(0L, after);

    private static bool DomainFailureContains(
        DomainFailure failure,
        string expected)
    {
        if (!failure.IsFailure || string.IsNullOrEmpty(expected))
            return false;
        ReadOnlySpan<string> parameters = failure.Parameters;
        for (int index = 0; index < parameters.Length; index++)
        {
            if (string.Equals(parameters[index], expected, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private IEnumerator RunRow(WorkTypeId workTypeId)
    {
        if (!CleanupRowScopedFixtures(out string rowCleanupFailure))
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "previous-row-fixture-cleanup-failed",
                rowCleanupFailure));
            yield break;
        }
        if (!CleanupRoomFixture(out string roomCleanupFailure))
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "previous-room-fixture-cleanup-failed",
                roomCleanupFailure));
            yield break;
        }
        MaintainStableWorkSubject();
        MaterialWorkFixture fixture = null;
        string fixtureFailure = string.Empty;
        if (IsDomainFixtureWork(workTypeId))
        {
            IEnumerator domainSetup = PrepareDomainFixture(
                workTypeId,
                value => fixture = value,
                value => fixtureFailure = value);
            while (domainSetup.MoveNext())
                yield return domainSetup.Current;
        }
        else if (!TryPrepareMaterialFixture(
                     workTypeId,
                     out fixture,
                     out fixtureFailure))
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "production-live-fixture-unavailable",
                fixtureFailure));
            yield break;
        }
        if (IsDomainFixtureWork(workTypeId) && fixture == null)
        {
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "production-live-domain-fixture-unavailable",
                fixtureFailure));
            IEnumerator cleanup = CleanupSingleDomainRoomAfterRow(workTypeId);
            while (cleanup.MoveNext())
                yield return cleanup.Current;
            yield break;
        }

        // Domain transactions change candidate availability without changing
        // building topology. Publish that production boundary before the
        // selector reads its incremental cache.
        if (fixture != null)
        {
            runtimeScope.Container.Resolve<IFacilityCandidateCache>()
                .MarkDynamicStateDirty();
            yield return null;
        }

        PrepareActor(workTypeId);
        WorkTargetCandidate candidate = default;
        BuildableObject target = null;
        float candidateDeadline = Time.realtimeSinceStartup + 2.5f;
        while (Time.realtimeSinceStartup < candidateDeadline)
        {
            MaintainStableWorkSubject();
            GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
            if (work.TryGetBestWorkCandidate(workTypeId, search, out candidate))
            {
                target = WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate);
                if (target != null)
                    break;
            }
            yield return null;
        }

        if (target == null)
        {
            WorkTargetCandidate rejected = work.LastRejectedWorkCandidate;
            results.Add(WorkTypeLiveRow.Blocked(
                workTypeId,
                "production-target-unavailable",
                "candidate=" + candidate.FailureKind + ":" + candidate.FailureReason
                + "; rejected=" + rejected.FailureKind + ":" + rejected.FailureReason
                + "; fixtureAuthority=" + DescribeFixtureAuthority(workTypeId, fixture)
                + "; prerequisite=" + GetMissingPrerequisite(workTypeId)));
            IEnumerator cleanup = CleanupSingleDomainRoomAfterRow(workTypeId);
            while (cleanup.MoveNext())
                yield return cleanup.Current;
            yield break;
        }
        if (fixture != null && !fixture.AcceptsTarget(target))
        {
            // The live world may contain another equally valid target with a
            // higher autonomous urgency (for example an exterior drop zone).
            // This matrix already issues the public priority-work command in
            // RunLivePhase, so use the fixture target and let that production
            // command perform the real path/execution validation.  No handler
            // is called directly here.
            target = fixture.Target;
        }
        // Unity fake-null objects cannot be dereferenced after the invalidation
        // phase. Capture durable report identity before either phase mutates
        // the production target.
        string targetLabel = target != null
            ? target.name
            : "<missing-target>";

        WorkPhaseResult cancellation = null;
        IEnumerator cancellationRun = RunLivePhase(
            workTypeId,
            target,
            WorkProbeFault.CancelAfterApprovedProgress,
            null,
            value => cancellation = value);
        while (cancellationRun.MoveNext())
            yield return cancellationRun.Current;

        if (workTypeId == BuiltInWorkTypeIds.GrandProject)
        {
            IGrandProjectRuntime grand =
                runtimeScope.Container.Resolve<IGrandProjectRuntime>();
            IProjectWorkforceRuntime workforce =
                runtimeScope.Container.Resolve<IProjectWorkforceRuntime>();
            string projectId = grand.State.activeProjectId;
            float releaseDeadline = Time.realtimeSinceStartup + 2f;
            while (workforce.GetActiveWorkerCount(projectId) > 0
                   && Time.realtimeSinceStartup < releaseDeadline)
            {
                yield return null;
            }
            if (workforce.GetActiveWorkerCount(projectId) > 0)
            {
                results.Add(new WorkTypeLiveRow(
                    workTypeId.Value,
                    "FAIL",
                    targetLabel,
                    (cancellation?.Format() ?? "cancel-phase-missing")
                    + " | grand-project-worker-lease-not-released:project="
                    + projectId + "; activeWorkers="
                    + workforce.GetActiveWorkerCount(projectId)));
                yield break;
            }
        }

        PrepareActor(workTypeId);
        if (fixture != null
            && !fixture.TryPrepareInvalidationPhase(out string phaseFailure))
        {
            results.Add(new WorkTypeLiveRow(
                workTypeId.Value,
                "FAIL",
                targetLabel,
                (cancellation?.Format() ?? "cancel-phase-missing")
                + " | invalidation-fixture-failed:" + phaseFailure));
            IEnumerator cleanup = CleanupSingleDomainRoomAfterRow(workTypeId);
            while (cleanup.MoveNext())
                yield return cleanup.Current;
            yield break;
        }
        WorkPhaseResult invalidation = null;
        IEnumerator invalidationRun = RunLivePhase(
            workTypeId,
            target,
            WorkProbeFault.InvalidateTargetAfterStart,
            fixture,
            value => invalidation = value);
        while (invalidationRun.MoveNext())
            yield return invalidationRun.Current;

        WorkPhaseResult completion = null;
        string completionSetupFailure = string.Empty;
        string completionTargetLabel = string.Empty;
        if (workTypeId == BuiltInWorkTypeIds.Dismantle)
        {
            // The invalidation row intentionally cancels the order while
            // leaving its rejected facility in the world.  Remove that
            // verifier-owned fixture before creating the success fixture so it
            // can reuse the same safe authored anchor; otherwise the second
            // facility is pushed to the exposed top row and the environment
            // policy (correctly) interrupts it before any dismantle WU.
            if (target != null && !target.isDestroy)
            {
                target.DestroySelf();
                yield return null;
            }
            PrepareActor(workTypeId);
            if (TryPrepareDismantleFixture(
                    out MaterialWorkFixture completionFixture,
                    out completionSetupFailure,
                    requireFaultObservationWindow: false))
            {
                runtimeScope.Container.Resolve<IFacilityCandidateCache>()
                    .MarkDynamicStateDirty();
                yield return null;
                // The real construction and rejected-quality setup above can
                // publish actor mood/activity effects. Re-establish the clean
                // phase boundary after materialization and immediately before
                // the live AI command; never neutralize the actor after work
                // has started.
                PrepareActor(workTypeId);
                BuildableObject completionTarget = completionFixture.Target;
                completionTargetLabel = completionTarget != null
                    ? completionTarget.name
                    : "<missing-completion-target>";
                IEnumerator completionRun = RunLivePhase(
                    workTypeId,
                    completionTarget,
                    WorkProbeFault.CompleteNormally,
                    completionFixture,
                    value => completion = value);
                while (completionRun.MoveNext())
                    yield return completionRun.Current;
            }
        }

        bool passed = cancellation != null && cancellation.Passed
            && invalidation != null && invalidation.Passed
            && (workTypeId != BuiltInWorkTypeIds.Dismantle
                || completion != null && completion.Passed);
        results.Add(new WorkTypeLiveRow(
            workTypeId.Value,
            passed ? "PASS" : "FAIL",
            targetLabel,
            (cancellation?.Format() ?? "cancel-phase-missing")
            + " | " + (invalidation?.Format() ?? "invalidation-phase-missing")
            + (workTypeId != BuiltInWorkTypeIds.Dismantle
                ? string.Empty
                : " | completionTarget=" + completionTargetLabel
                    + "; " + (completion?.Format()
                        ?? "completion-phase-missing:"
                        + completionSetupFailure))));
        IEnumerator rowRoomCleanup = CleanupSingleDomainRoomAfterRow(workTypeId);
        while (rowRoomCleanup.MoveNext())
            yield return rowRoomCleanup.Current;
    }

    private IEnumerator RunLivePhase(
        WorkTypeId workTypeId,
        BuildableObject target,
        WorkProbeFault fault,
        MaterialWorkFixture fixture,
        Action<WorkPhaseResult> complete)
    {
        WorkPhaseResult result = new WorkPhaseResult(fault);
        if (workTypeId == BuiltInWorkTypeIds.Plumbing)
        {
            result.DomainTrace = "before-phase=" + CapturePlumbingState(target);
        }
        else if (workTypeId == BuiltInWorkTypeIds.Dismantle)
        {
            result.DomainTrace = "before-phase="
                + DescribeFixtureAuthority(workTypeId, fixture);
        }
        MaintainStableWorkSubject();
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            result.Blocker = "target-not-active-before-phase";
            complete(result);
            yield break;
        }

        // A need action which was already committed before this row owns a
        // higher-priority external intent.  Do not terminate it from the
        // verifier and do not mistake its primitive phase for a failed work
        // start.  Keep the authored needs neutral and wait for the production
        // action to reach its own terminal before asking the scheduler to
        // select this work row.
        float intentDeadline = Time.realtimeSinceStartup + 12f;
        while (brain.IsExternallyDrivenActionActive
               && Time.realtimeSinceStartup < intentDeadline)
        {
            MaintainStableWorkSubject();
            yield return null;
        }
        if (brain.IsExternallyDrivenActionActive)
        {
            result.Blocker = "higher-priority-intent-did-not-settle:owner="
                + brain.ExternalIntentOwnerId + "; kind=" + brain.ExternalIntentKind
                + "; phase=" + brain.CurrentActionPhase;
            complete(result);
            yield break;
        }
        brain.StopCurrentActionForReplan(
            "worktype live matrix higher-priority intent settled");
        float previousActionDeadline = Time.realtimeSinceStartup + 8f;
        while ((brain.HasRunningAction
                || brain.IsExternallyDrivenActionActive
                || work.HasActiveWorkRoutineForDiagnostics
                || work.isWorking)
               && Time.realtimeSinceStartup < previousActionDeadline)
        {
            MaintainStableWorkSubject();
            yield return null;
        }
        if (brain.HasRunningAction
            || brain.IsExternallyDrivenActionActive
            || work.HasActiveWorkRoutineForDiagnostics
            || work.isWorking)
        {
            result.Blocker = "previous-action-did-not-settle:running="
                + brain.HasRunningAction + "; external="
                + brain.IsExternallyDrivenActionActive + "; working="
                + work.isWorking + "; routine="
                + work.HasActiveWorkRoutineForDiagnostics + "; run="
                + work.ActiveWorkRunIdForDiagnostics + "; phase="
                + brain.CurrentActionPhase;
            complete(result);
            yield break;
        }
        for (int frame = 0; frame < 2; frame++)
        {
            MaintainStableWorkSubject();
            yield return null;
        }

        // Capture the lifecycle before the public priority command. Some work
        // commands can synchronously publish an action epoch when their target
        // is already reachable; capturing afterwards would lose that start and
        // misclassify a live DrawWater action as "did not start".
        CharacterAiRuntimeDiagnosticsSnapshot start =
            brain.CaptureRuntimeDiagnostics();
        CharacterAiRuntimeGateSnapshot startGate = start.Gate;
        long startEpoch = brain.RuntimeActionEpoch;
        long startWorkRevision =
            work.ApprovedWorkProgressRevisionForDiagnostics;
        result.StartEpoch = startEpoch;
        result.StartGate = startGate;

        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        if (!work.TrySetPriorityWorkTarget(
                target,
                workTypeId,
                search,
                out string priorityFailure))
        {
            WorkTargetCandidate firstRejected = work.LastRejectedWorkCandidate;
            // Forced work in an unsafe exterior/environment is a two-step
            // production command: first call publishes the warning, the exact
            // same call confirms it. This is the same public handshake used by
            // the player command UI; a second rejection remains a real red.
            if (!work.TrySetPriorityWorkTarget(
                    target,
                    workTypeId,
                    search,
                    out string confirmedFailure))
            {
                WorkTargetCandidate confirmedRejected =
                    work.LastRejectedWorkCandidate;
                result.Blocker = "priority-target-rejected:first="
                    + priorityFailure + "; firstTyped="
                    + FormatRejectedCandidate(firstRejected)
                    + "; confirmed=" + confirmedFailure
                    + "; confirmedTyped="
                    + FormatRejectedCandidate(confirmedRejected);
                complete(result);
                yield break;
            }

            result.PriorityCommandDetail = "confirmed-after-warning:"
                + priorityFailure + "; typed="
                + FormatRejectedCandidate(firstRejected);
        }

        // Publish the actor lifecycle before selecting the preferred action.
        // SetAiPaused(false) only queues a decision; no scheduler turn can race
        // this synchronous preference call on the Unity main thread.
        actor.SetAiPaused(false);
        if (!actor.CanRunAi)
        {
            result.Blocker = "actor-cannot-run-ai-after-unpause:lifecycle="
                + actor.CurrentLifecycleState + "; enabled=" + brain.enabled;
            actor.SetAiPaused(true);
            complete(result);
            yield break;
        }
        if (!brain.PreferWorkActionOnNextDecision(workTypeId, 45f))
        {
            result.Blocker = "brain-rejected-work-preference; actions="
                + FormatAvailableActions(brain.availableActions);
            actor.SetAiPaused(true);
            complete(result);
            yield break;
        }

        brain.RequestImmediateReplan(clearFailures: true);
        // A deferred path-search is not a rejected work action. The production
        // scheduler is allowed to run a short AIWait while its preferred
        // AIWork remains pending, then retry it on a later decision. Preserve
        // that real scheduling path, but bound and account every intervening
        // epoch so an unrelated action or an immortal wait cannot become a
        // false green.
        const int maxDeferredWaitEpochs = 6;
        HashSet<long> deferredWaitEpochs = new HashSet<long>();
        HashSet<long> observedAiWorkEpochs = new HashSet<long>();
        long lastRetriedWaitEpoch = 0L;
        float startDeadline = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < startDeadline)
        {
            MaintainStableWorkSubject();
            if (work.isWorking
                && work.AssignedWorkTypeId == workTypeId
                && brain.RuntimeActionEpoch > startEpoch)
            {
                result.Started = true;
                result.ObservedWorkType = work.AssignedWorkTypeId.Value;
                result.ObservedEpoch = brain.RuntimeActionEpoch;
                break;
            }

            long decidingEpoch = brain.RuntimeActionEpoch;
            AIActionSet decidingActionSet = brain.bestAction?.actionset;
            bool workStillPreferred =
                brain.IsActionPreferredForNextDecision<AIWork>();
            if (decidingEpoch > startEpoch
                && decidingActionSet is AIWork)
            {
                observedAiWorkEpochs.Add(decidingEpoch);
            }
            else if (decidingEpoch > startEpoch
                     && decidingActionSet is AIWait
                     && workStillPreferred)
            {
                deferredWaitEpochs.Add(decidingEpoch);
                result.PreStartDeferredWaits = deferredWaitEpochs.Count;
                if (deferredWaitEpochs.Count > maxDeferredWaitEpochs)
                {
                    result.Blocker = "preferred-aiwork-deferred-wait-limit:count="
                        + deferredWaitEpochs.Count + "; phase="
                        + brain.CurrentActionPhase + "; failure="
                        + brain.LastActionFailure;
                    break;
                }
            }
            else if (decidingEpoch > startEpoch
                     && decidingActionSet != null
                     && !workStillPreferred)
            {
                result.Blocker = "preferred-aiwork-lost-before-start:action="
                    + decidingActionSet.GetType().Name + "; phase="
                    + brain.CurrentActionPhase + "; failure="
                    + brain.LastActionFailure;
                break;
            }

            CharacterAiRuntimeDiagnosticsSnapshot deciding =
                brain.CaptureRuntimeDiagnostics();
            if (decidingEpoch > startEpoch
                && deciding.TryGetActionTerminal(
                    decidingEpoch,
                    out CharacterAiActionTerminalKind decidingTerminal))
            {
                if (deferredWaitEpochs.Contains(decidingEpoch)
                    && (decidingTerminal == CharacterAiActionTerminalKind.Completed
                        || decidingTerminal == CharacterAiActionTerminalKind.Cancelled))
                {
                    if (lastRetriedWaitEpoch != decidingEpoch)
                    {
                        lastRetriedWaitEpoch = decidingEpoch;
                        if (!brain.PreferWorkActionOnNextDecision(workTypeId, 45f))
                        {
                            result.Blocker =
                                "preferred-aiwork-retry-registration-failed";
                            break;
                        }
                        brain.RequestImmediateReplan(clearFailures: false);
                    }
                }
                else if (observedAiWorkEpochs.Contains(decidingEpoch))
                {
                    result.ObservedEpoch = decidingEpoch;
                    result.ObservedTerminalKind = decidingTerminal;
                    result.TerminalObserved = true;
                    result.Blocker =
                        "preferred-aiwork-terminal-before-running-observation:"
                        + decidingTerminal + "; failure=" + brain.LastActionFailure
                        + "; currentAction=" + brain.CurrentActionDebugLabel
                        + "; currentPhase=" + brain.CurrentActionPhase
                        + "; assigned=" + work.AssignedWorkTypeId.Value;
                    break;
                }
                else if (!deferredWaitEpochs.Contains(decidingEpoch))
                {
                    result.Blocker = "unaccounted-prestart-terminal:epoch="
                        + decidingEpoch + "; terminal=" + decidingTerminal
                        + "; action="
                        + (decidingActionSet?.GetType().Name ?? "null");
                    break;
                }
            }
            yield return null;
        }

        if (!result.Started)
        {
            if (string.IsNullOrEmpty(result.Blocker))
            {
                result.Blocker = "aiwork-did-not-start:action="
                    + brain.CurrentActionDebugLabel
                    + "; phase=" + brain.CurrentActionPhase
                    + "; failure=" + brain.LastActionFailure
                    + "; assigned=" + work.AssignedWorkTypeId.Value
                    + "; deferredWaits=" + deferredWaitEpochs.Count;
            }
            complete(result);
            yield break;
        }

        for (long skippedEpoch = startEpoch + 1L;
             skippedEpoch < result.ObservedEpoch;
             skippedEpoch++)
        {
            CharacterAiRuntimeDiagnosticsSnapshot now =
                brain.CaptureRuntimeDiagnostics();
            bool isAccountedDeferredWait =
                deferredWaitEpochs.Contains(skippedEpoch);
            bool hasTerminal = now.TryGetActionTerminal(
                skippedEpoch,
                out CharacterAiActionTerminalKind currentTerminal);
            if (!isAccountedDeferredWait
                || !hasTerminal
                || (currentTerminal != CharacterAiActionTerminalKind.Completed
                    && currentTerminal
                        != CharacterAiActionTerminalKind.Cancelled))
            {
                result.Blocker =
                    "unaccounted-action-epoch-before-aiwork:epoch="
                    + skippedEpoch + "; deferredWait="
                    + isAccountedDeferredWait + "; terminal="
                    + (hasTerminal ? currentTerminal.ToString() : "missing");
                break;
            }
        }
        if (!string.IsNullOrEmpty(result.Blocker))
        {
            brain.StopCurrentActionForReplan(
                "worktype live matrix unaccounted pre-start epoch");
            actor.SetAiPaused(true);
            complete(result);
            yield break;
        }

        AIAction startedAction = brain.bestAction;
        if (startedAction?.actionset is not AIWork startedWorkAction)
        {
            result.Blocker = "started-action-is-not-aiwork:type="
                + (startedAction?.actionset?.GetType().Name ?? "null")
                + "; assigned=" + work.AssignedWorkTypeId.Value;
            brain.StopCurrentActionForReplan(
                "worktype live matrix rejected non-AIWork start");
            actor.SetAiPaused(true);
            complete(result);
            yield break;
        }
        if (work.AssignedWorkTypeId != workTypeId
            || (startedWorkAction.WorkTypeId.IsValid
                && startedWorkAction.WorkTypeId != workTypeId))
        {
            result.Blocker = "started-aiwork-type-mismatch:requested="
                + workTypeId.Value + "; action="
                + (startedWorkAction.WorkTypeId.IsValid
                    ? startedWorkAction.WorkTypeId.Value
                    : "generic") + "; assigned="
                + work.AssignedWorkTypeId.Value;
            brain.StopCurrentActionForReplan(
                "worktype live matrix rejected mismatched AIWork start");
            actor.SetAiPaused(true);
            complete(result);
            yield break;
        }
        result.ObservedActionType = startedWorkAction.GetType().Name + "["
            + (startedWorkAction.WorkTypeId.IsValid
                 ? startedWorkAction.WorkTypeId.Value
                 : "generic->" + workTypeId.Value) + "]";
        if (workTypeId == BuiltInWorkTypeIds.Plumbing)
        {
            result.DomainTrace += "; after-start="
                + CapturePlumbingState(work.assignedShop)
                + "; expectedTarget=" + FormatBuildingIdentity(target)
                + "; assignedTarget=" + FormatBuildingIdentity(work.assignedShop);
        }
        else if (workTypeId == BuiltInWorkTypeIds.Dismantle)
        {
            result.DomainTrace += "; after-start="
                + DescribeFixtureAuthority(workTypeId, fixture)
                + "; assignedTarget=" + FormatBuildingIdentity(work.assignedShop)
                + "; execution="
                + work.LastWorkOrderExecutionDetailForDiagnostics;
        }

        // The official scheduler selected and started this action. Freeze only
        // subsequent decisions while its already-running production coroutine
        // travels, works and reaches a typed terminal. Repeated direct decision
        // calls here used to race the scheduler and manufacture extra epochs.
        actor.SetAiPaused(true);

        AbilityMove move = null;
        actor.TryGetAbility<AbilityMove>(out move);
        AIAction observedAction = startedAction;
        result.EstimatedTravelSeconds =
            CalculateBoundedTravelObservationSeconds(observedAction);
        result.MovementStartPosition = actor.GetNowXY();
        bool targetWasActive = target.gameObject.activeSelf;
        if (fault == WorkProbeFault.CancelAfterApprovedProgress)
        {
            float progressSoftDeadline = Time.realtimeSinceStartup
                + result.EstimatedTravelSeconds;
            float progressHardDeadline = Time.realtimeSinceStartup
                + MaximumProgressObservationSeconds;
            float lastMovementProgressAt = Time.realtimeSinceStartup;
            Vector3 lastWorldPosition = actor.transform.position;
            long lastGameplayProgress = startGate.GameplayProgressRevision;
            while (Time.realtimeSinceStartup < progressHardDeadline
                   && work.ApprovedWorkProgressRevisionForDiagnostics <= startWorkRevision)
            {
                MaintainStableWorkSubject();
                CharacterAiRuntimeDiagnosticsSnapshot currentDiagnostics =
                    brain.CaptureRuntimeDiagnostics();
                CharacterAiRuntimeGateSnapshot currentGate =
                    currentDiagnostics.Gate;
                Vector3 currentWorldPosition = actor.transform.position;
                bool positionAdvanced =
                    (currentWorldPosition - lastWorldPosition).sqrMagnitude
                    > MovementPositionEpsilonSquared;
                bool waypointAdvanced =
                    currentGate.GameplayProgressRevision > lastGameplayProgress;
                if (positionAdvanced || waypointAdvanced)
                {
                    result.MovementObserved = true;
                    lastMovementProgressAt = Time.realtimeSinceStartup;
                    lastWorldPosition = currentWorldPosition;
                    lastGameplayProgress = currentGate.GameplayProgressRevision;
                }

                if (currentDiagnostics.TryGetActionTerminal(
                        result.ObservedEpoch,
                        out CharacterAiActionTerminalKind earlyTerminal))
                {
                    result.ObservedTerminalKind = earlyTerminal;
                    result.TerminalObserved = true;
                    result.Blocker =
                        "action-terminal-before-approved-wu:" + earlyTerminal;
                    break;
                }

                bool moving = IsMovementOrAdmissionPhase(
                    brain.CurrentRuntimePhase);
                // Restock and other multi-leg executors can publish an initial
                // AIAction path only to the work target, then resolve a hidden
                // warehouse/facility leg inside WorkTaskExecutor.  The initial
                // path estimate is therefore a soft bound: keep observing only
                // while the production runtime still owns a movement/admission
                // phase and its position/waypoint heartbeat is advancing.  A
                // non-moving work phase remains bounded by the original ETA,
                // and every movement phase still has the independent 5-second
                // stall watchdog plus the absolute 120-second ceiling.
                if (Time.realtimeSinceStartup >= progressSoftDeadline
                    && !moving)
                {
                    break;
                }
                if (moving
                    && Time.realtimeSinceStartup - lastMovementProgressAt
                    >= MovementStallSeconds)
                {
                    result.MovementStalled = true;
                    result.Blocker = "movement-waypoint-stalled; "
                        + CaptureStageDetail(target, move);
                    break;
                }
                yield return null;
            }

            result.Progressed =
                work.ApprovedWorkProgressRevisionForDiagnostics > startWorkRevision;
            result.ProgressDelta =
                work.ApprovedWorkProgressRevisionForDiagnostics - startWorkRevision;
            result.StageBeforeTerminalRequest = CaptureStageDetail(target, move);
            if (workTypeId == BuiltInWorkTypeIds.Plumbing)
            {
                result.DomainTrace += "; before-terminal="
                    + CapturePlumbingState(work.assignedShop)
                    + "; expected-before-terminal="
                    + CapturePlumbingState(target);
            }
            result.TerminalRequested = brain.StopCurrentActionForReplan(
                result.Progressed
                    ? "worktype live matrix cancel after approved progress"
                    : "worktype live matrix abort after progress timeout");
            if (!result.Progressed && string.IsNullOrEmpty(result.Blocker))
                result.Blocker = "approved-wu-progress-timeout; "
                    + CaptureStageDetail(target, move);
            else if (result.Progressed && !result.TerminalRequested)
                result.Blocker = "cancel-request-rejected";
        }
        else if (fault == WorkProbeFault.CompleteNormally)
        {
            // No verifier mutation: the production WorkOrder/AbilityWork path
            // must remove the dismantle target, leave any recovery continuation
            // authoritative, and publish its own Completed terminal.
            result.TerminalRequested = true;
        }
        else
        {
            if (fixture != null)
            {
                result.TerminalRequested = fixture.TryInvalidate(
                    out string invalidationFailure);
                if (!result.TerminalRequested)
                    result.Blocker = "fixture-invalidation-failed:" + invalidationFailure;
            }
            else
            {
                target.gameObject.SetActive(false);
                result.TerminalRequested = true;
            }
        }

        float terminalObservationSeconds = fault switch
        {
            WorkProbeFault.InvalidateTargetAfterStart =>
                result.EstimatedTravelSeconds,
            WorkProbeFault.CompleteNormally =>
                MaximumCompletionObservationSeconds,
            _ => 5f
        };
        float terminalDeadline = Time.realtimeSinceStartup
            + terminalObservationSeconds;
        float terminalLastMovementAt = Time.realtimeSinceStartup;
        Vector3 terminalLastWorldPosition = actor.transform.position;
        long terminalLastGameplayProgress =
            brain.CaptureRuntimeDiagnostics().Gate.GameplayProgressRevision;
        long terminalLastApprovedWorkProgress =
            work.ApprovedWorkProgressRevisionForDiagnostics;
        float terminalLastAuthoritativeProgressAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup < terminalDeadline)
        {
            MaintainStableWorkSubject();
            CharacterAiRuntimeDiagnosticsSnapshot current =
                brain.CaptureRuntimeDiagnostics();
            if (current.TryGetActionTerminal(
                    result.ObservedEpoch,
                    out CharacterAiActionTerminalKind terminalKind))
            {
                result.TerminalObserved = true;
                result.ObservedTerminalKind = terminalKind;
                if (fault == WorkProbeFault.CompleteNormally)
                {
                    result.StageBeforeTerminalRequest =
                        CaptureStageDetail(target, move);
                }
                break;
            }

            Vector3 currentWorldPosition = actor.transform.position;
            bool positionAdvanced =
                (currentWorldPosition - terminalLastWorldPosition).sqrMagnitude
                > MovementPositionEpsilonSquared;
            bool waypointAdvanced = current.Gate.GameplayProgressRevision
                > terminalLastGameplayProgress;
            long currentApprovedWorkProgress =
                work.ApprovedWorkProgressRevisionForDiagnostics;
            bool approvedWorkAdvanced = currentApprovedWorkProgress
                > terminalLastApprovedWorkProgress;
            if (positionAdvanced || waypointAdvanced)
            {
                result.MovementObserved = true;
                terminalLastMovementAt = Time.realtimeSinceStartup;
                terminalLastAuthoritativeProgressAt = Time.realtimeSinceStartup;
                terminalLastWorldPosition = currentWorldPosition;
                terminalLastGameplayProgress =
                    current.Gate.GameplayProgressRevision;
            }
            if (approvedWorkAdvanced)
            {
                terminalLastAuthoritativeProgressAt = Time.realtimeSinceStartup;
                terminalLastApprovedWorkProgress = currentApprovedWorkProgress;
            }

            bool moving = IsMovementOrAdmissionPhase(
                brain.CurrentRuntimePhase);
            if (moving
                && Time.realtimeSinceStartup - terminalLastMovementAt
                >= MovementStallSeconds)
            {
                result.MovementStalled = true;
                if (string.IsNullOrEmpty(result.Blocker))
                {
                    result.Blocker = "movement-waypoint-stalled; "
                        + CaptureStageDetail(target, move);
                }
                break;
            }
            if (fault == WorkProbeFault.CompleteNormally
                && !moving
                && Time.realtimeSinceStartup - terminalLastAuthoritativeProgressAt
                    >= CompletionNoProgressSeconds)
            {
                if (string.IsNullOrEmpty(result.Blocker))
                {
                    result.Blocker = "completion-authority-no-progress; "
                        + CaptureStageDetail(target, move);
                }
                break;
            }
            yield return null;
        }

        result.MovementEndPosition = actor.GetNowXY();
        result.GameplayProgressDelta =
            brain.CaptureRuntimeDiagnostics().Gate.GameplayProgressRevision
            - startGate.GameplayProgressRevision;
        result.ProgressDelta =
            work.ApprovedWorkProgressRevisionForDiagnostics - startWorkRevision;
        result.Progressed = result.ProgressDelta > 0;
        result.CompletionTargetRemoved = fault != WorkProbeFault.CompleteNormally
            || target == null
            || target.isDestroy
            || !target.gameObject.activeInHierarchy;

        if (fault == WorkProbeFault.InvalidateTargetAfterStart
            && fixture == null)
            target.gameObject.SetActive(targetWasActive);

        for (int frame = 0; frame < 2; frame++)
        {
            MaintainStableWorkSubject();
            yield return null;
        }

        CharacterAiRuntimeDiagnosticsSnapshot end = brain.CaptureRuntimeDiagnostics();
        result.EndGate = end.Gate;
        result.ActionStarts = end.Gate.ActionStarts - startGate.ActionStarts;
        result.ActionTerminals = end.Gate.ActionTerminals - startGate.ActionTerminals;
        result.Cancelled = end.Gate.ActionCancelled - startGate.ActionCancelled;
        result.Failed = end.Gate.ActionFailed - startGate.ActionFailed;
        result.PathRequests = end.Gate.PathRequests - startGate.PathRequests;
        result.PathResults = end.Gate.PathResults - startGate.PathResults;
        result.ReservationAcquires =
            end.Gate.ReservationAcquires - startGate.ReservationAcquires;
        result.ReservationReleases =
            end.Gate.ReservationReleases - startGate.ReservationReleases;
        CharacterAiRuntimeGateSnapshot endGate = end.Gate;
        result.LifecycleConserved = endGate.ConservesLifecycleFrom(in startGate);
        result.PathsConserved = endGate.ConservesPathsFrom(in startGate);
        result.ReservationsConserved = endGate.ConservesReservationsFrom(in startGate);
        result.NoInvariantAnomaly =
            endGate.InvariantAnomalies == startGate.InvariantAnomalies;
        result.TypedTerminal = fault switch
        {
            WorkProbeFault.CancelAfterApprovedProgress =>
                result.ObservedTerminalKind
                    == CharacterAiActionTerminalKind.Cancelled,
            WorkProbeFault.CompleteNormally =>
                result.ObservedTerminalKind
                    == CharacterAiActionTerminalKind.Completed,
            _ => result.ObservedTerminalKind
                    == CharacterAiActionTerminalKind.Failed
                || result.ObservedTerminalKind
                    == CharacterAiActionTerminalKind.Cancelled
        };
        if (!result.TerminalObserved && string.IsNullOrEmpty(result.Blocker))
            result.Blocker = "typed-terminal-timeout; "
                + CaptureStageDetail(target);
        if (!result.TypedTerminal && string.IsNullOrEmpty(result.Blocker))
            result.Blocker = "expected-terminal-kind-not-observed";
        if ((!result.LifecycleConserved
             || !result.PathsConserved
             || !result.ReservationsConserved)
            && string.IsNullOrEmpty(result.Blocker))
        {
            result.Blocker = "lifecycle-path-reservation-conservation-failed";
        }
        complete(result);
    }

    private float CalculateBoundedTravelObservationSeconds(AIAction action)
    {
        float worldDistance = 0f;
        Vector3 previous = actor.transform.position;
        int pathStepCount = action?.pathSteps?.Count ?? 0;
        for (int index = 0; index < pathStepCount; index++)
        {
            Vector3 next = grid.GetWorldPos(action.pathSteps[index].To);
            worldDistance += Vector3.Distance(previous, next);
            previous = next;
        }

        // Facility admission can transfer the route from AIAction to the
        // facility worker-port routine before this verifier snapshots it. Use
        // the production work-access stand as the bounded fallback instead of
        // collapsing an actively moving long route to the old fixed 8 seconds.
        if (worldDistance <= 0.001f
            && work?.assignedShop != null
            && WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(
                work.assignedShop,
                grid.SearchPath(actor.GetNowXY()),
                out Vector2Int workAccess))
        {
            worldDistance = Vector3.Distance(
                actor.transform.position,
                grid.GetWorldPos(workAccess));
        }

        float gameSeconds = worldDistance / Mathf.Max(0.1f, actor.GetMoveSpeed());
        float realSeconds = gameSeconds / Mathf.Max(0.01f, Time.timeScale);
        // Door traversal, scheduling stride and one path rebuild are bounded
        // overheads. This is deliberately conservative, but never converts a
        // waypoint stall into an unbounded wait.
        float observationSeconds = 4f + realSeconds * 2f
            + pathStepCount * 0.1f;
        return Mathf.Clamp(
            observationSeconds,
            MinimumProgressObservationSeconds,
            MaximumProgressObservationSeconds);
    }

    private string CaptureStageDetail(
        BuildableObject target,
        AbilityMove move = null)
    {
        CharacterAiRuntimeDiagnosticsSnapshot aiDiagnostics =
            brain?.CaptureRuntimeDiagnostics() ?? default;
        int activeWorkRunId = work?.ActiveWorkRunIdForDiagnostics ?? 0;
        int cancelledWorkRunId =
            work?.LastCancelledWorkRunIdForDiagnostics ?? 0;
        string cancellationReason =
            work?.LastActiveWorkCancellationReasonForDiagnostics
            ?? string.Empty;
        string cancellationLatchState = string.IsNullOrWhiteSpace(
                cancellationReason)
            ? "none"
            : cancelledWorkRunId == activeWorkRunId
                ? "current"
                : "stale";
        Vector2Int actorPosition = actor != null
            ? actor.GetNowXY()
            : default;
        return "phase=" + (brain?.CurrentActionPhase ?? "-")
            + "; failure=" + (brain?.LastActionFailure.ToString() ?? "-")
            + "; actor=" + actorPosition
            + "; target=" + (target != null ? target.centerPos.ToString() : "null")
            + "; active=" + (target != null && target.gameObject.activeInHierarchy)
            + "; assigned=" + (work?.AssignedWorkTypeId.Value ?? "-")
            + "; working=" + (work?.isWorking ?? false)
            + "; movementActive="
            + (move?.HasActiveMovementRoutineForDiagnostics ?? false)
            + "; movementFailure="
            + (move?.LastGridMoveFailureReason.ToString() ?? "-")
            + "; runtimePhase=" + (brain?.CurrentRuntimePhase.ToString() ?? "-")
            + "; lifecycle=" + (actor?.CurrentLifecycleState.ToString() ?? "-")
            + "; preWuExit="
            + (work?.LastPreWuExitKindForDiagnostics.ToString() ?? "-")
            + ":"
            + (work?.LastPreWuExitDetailForDiagnostics ?? string.Empty)
            + "; workOrder="
            + (work?.LastWorkOrderExecutionDetailForDiagnostics ?? string.Empty)
            + "; workCancellation="
            + (work?.ActiveWorkCancellationCountForDiagnostics ?? 0L)
            + "/run=" + cancelledWorkRunId
            + "/activeRun=" + activeWorkRunId
            + "/latch=" + cancellationLatchState
            + "/reason=" + cancellationReason
            + "; interruptedReplan="
            + aiDiagnostics.LastInterruptedReplanDetail
            + "; environment=" + FormatSubjectEnvironment()
            + "; discontent=" + FormatSubjectDiscontent()
            + "; workAccess=" + FormatWorkAccess(target);
    }

    private string CapturePlumbingState(BuildableObject target)
    {
        if (target == null || runtimeScope?.Container == null)
            return FormatBuildingIdentity(target) + ":query-unavailable";

        IFluidInfrastructureQuery query =
            runtimeScope.Container.Resolve<IFluidInfrastructureQuery>();
        return query.TryGetMaintenance(
                target,
                out float blockage,
                out float leak)
            ? FormatBuildingIdentity(target) + ":blockage="
                + blockage.ToString("0.###") + ",leak="
                + leak.ToString("0.###")
            : FormatBuildingIdentity(target) + ":not-a-fluid-node";
    }

    private static string FormatBuildingIdentity(BuildableObject target)
    {
        if (target == null)
            return "null";

        return target.PersistentInstanceId.IsValid
            ? target.PersistentInstanceId.Value
            : target.name + "@" + target.GetInstanceID();
    }

    private static CharacterActor SelectStableWorkSubject(
        IEnumerable<CharacterActor> actors)
    {
        return actors?
            .Where(value => value != null
                && value.characterType == CharacterType.NPC
                && value.GetComponent<AbilityWork>() != null
                && value.Brain != null)
            // The matrix accelerates several game days while exercising all
            // rows. A regular employee is legitimately eligible for permanent
            // departure during that time, which contaminates every later row
            // with Despawned. The founder uses the identical production
            // Brain/AIWork/AbilityWork path but is not a staff-discontent
            // subject, so it is the stable authority for this executor matrix.
            .OrderByDescending(value => value.Role == CharacterRole.Owner)
            .ThenBy(value => value.Identity?.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsMovementOrAdmissionPhase(
        CharacterAiRuntimePhase phase) =>
        phase == CharacterAiRuntimePhase.Moving
        || phase == CharacterAiRuntimePhase.Repathing
        || phase == CharacterAiRuntimePhase.FacilityAdmission;

    private string FormatWorkAccess(BuildableObject target)
    {
        if (target == null || grid == null || actor == null)
            return "unavailable";
        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        return WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(
                target,
                search,
                out Vector2Int access)
            ? access.ToString()
            : "none;footprint=" + string.Join(",", target.buildPoses);
    }

    private void PrepareActor(WorkTypeId selected)
    {
        // The reset is an atomic verifier boundary. Pause first so neither the
        // scheduler nor a deprivation runner can observe the half-reset state
        // between raw needs, persistent mood factors and deprivation burden.
        actor.SetAiPaused(true);
        brain.StopCurrentActionForReplan("worktype live matrix row reset");
        work.ClearPriorityWorkTarget();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        foreach (WorkTypeId id in BuiltInWorkTypeIds.All)
            work.SetWorkPriority(id, id == selected
                ? WorkPriorityLevel.Priority1
                : WorkPriorityLevel.Off);

        ResetNeutralPersistentState();
        ResetSubjectEnvironmentExposure();
        // Keep the scheduler quiescent until the next phase has explicitly
        // installed its priority target.  The running coroutine (if any) is
        // allowed to unwind while paused and RunLivePhase verifies that it did.
        brain.enabled = true;
    }

    private void ResetNeutralPersistentState()
    {
        CharacterStats stats = actor?.Stats;
        if (stats == null)
        {
            throw new InvalidOperationException(
                "WorkType live matrix cannot neutralize a subject without CharacterStats.");
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
            actor.CurrentHealth,
            actor.InjurySeverity,
            100f,
            Array.Empty<CharacterMoodFactorSnapshot>());

        if (deprivationRuntime == null
            || !deprivationRuntime.DebugResetForDeterministicScenario(actor))
        {
            throw new InvalidOperationException(
                "WorkType live matrix deprivation reset rejected the selected subject.");
        }

        float effectiveMood = CharacterMoodImpulseUtility.GetMood01(actor);
        if (effectiveMood < 0.9f)
        {
            throw new InvalidOperationException(
                "WorkType live matrix neutral mood precondition failed: effectiveMood="
                + effectiveMood.ToString("0.###") + "; rawMood="
                + (stats.StatSnapshot.TryGetValue(
                        CharacterCondition.MOOD,
                        out float rawMood)
                    ? rawMood.ToString("0.###")
                    : "missing"));
        }
    }

    private void ResetSubjectEnvironmentExposure()
    {
        if (actor?.Identity == null
            || characterEnvironmentPersistence == null)
        {
            return;
        }

        string actorId = actor.Identity.PersistentId;
        DungeonCharacterEnvironmentSaveData state =
            characterEnvironmentPersistence.Capture();
        List<CharacterEnvironmentExposure> exposures =
            (state.exposures ?? Array.Empty<CharacterEnvironmentExposure>())
            .Where(value => value != null
                && !string.Equals(
                    value.characterId,
                    actorId,
                    StringComparison.Ordinal))
            .ToList();
        exposures.Add(new CharacterEnvironmentExposure
        {
            characterId = actorId,
            coldExposure = 0f,
            heatExposure = 0f,
            airborneExposure = 0f,
            visualStrain = 0f,
            physiologicalBand = EnvironmentalExposureBand.Stable,
            visualBand = EnvironmentalExposureBand.Stable,
            criticalDamageTimer = 0f,
            coldWorkCooldownActive = false
        });
        state.exposures = exposures
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .ToArray();
        characterEnvironmentPersistence.PublishRestoreCandidate(
            characterEnvironmentPersistence.BuildRestoreCandidate(state));
        runtimeScope?.Container.Resolve<ICharacterEnvironmentWorkContext>()
            .ClearWorkContext(new CharacterId(actorId));
    }

    private string FormatSubjectEnvironment()
    {
        if (actor?.Identity == null || characterEnvironmentStatus == null)
            return "unavailable";

        CharacterId characterId = new CharacterId(
            actor.Identity.PersistentId);
        CharacterEnvironmentExposure exposure =
            characterEnvironmentStatus.GetExposure(characterId);
        if (exposure == null)
            return "phys=Stable,visual=Stable,exposure=unpublished";
        return "phys="
            + characterEnvironmentStatus.GetPhysiologicalBand(characterId)
            + ",visual="
            + characterEnvironmentStatus.GetVisualBand(characterId)
            + ",cold=" + exposure.coldExposure.ToString("0.###")
            + ",heat=" + exposure.heatExposure.ToString("0.###")
            + ",air=" + exposure.airborneExposure.ToString("0.###")
            + ",strain=" + exposure.visualStrain.ToString("0.###");
    }

    private static string FormatAvailableActions(IReadOnlyList<AIAction> actions)
    {
        if (actions == null)
            return "null";
        if (actions.Count == 0)
            return "empty";
        return string.Join(",", actions.Select(value =>
        {
            AIActionSet actionSet = value?.actionset;
            if (actionSet == null)
                return "<null>";
            if (actionSet is AIWork workAction)
            {
                return actionSet.GetType().Name + "["
                    + (workAction.WorkTypeId.IsValid
                        ? workAction.WorkTypeId.Value
                        : "generic") + "]";
            }
            return actionSet.GetType().Name;
        }));
    }

    private static string FormatRejectedCandidate(WorkTargetCandidate candidate)
    {
        return "kind=" + candidate.FailureKind
            + ",workType=" + candidate.WorkTypeId.Value
            + ",reason=" + (candidate.FailureReason ?? string.Empty)
            + ",breakdown=" + (candidate.BreakdownSummary ?? string.Empty);
    }

    private static void SetNeutralNeeds(CharacterActor targetActor)
    {
        IDictionary<CharacterCondition, float> needs = targetActor?.stats;
        if (needs != null)
        {
            needs[CharacterCondition.HUNGER] = 100f;
            needs[CharacterCondition.THIRST] = 100f;
            needs[CharacterCondition.SLEEP] = 100f;
            needs[CharacterCondition.FUN] = 100f;
            needs[CharacterCondition.MOOD] = 100f;
            needs[CharacterCondition.EXCRETION] = 100f;
            needs[CharacterCondition.HYGIENE] = 100f;
        }
    }

    private void MaintainStableWorkSubject()
    {
        SetNeutralNeeds(actor);
        // This verifier accelerates multiple authored days to exercise twenty
        // executor rows. Staff-discontent is a separate production system and
        // must not make a later row inherit PermanentDeparture from earlier
        // rows. Reset it through its public restore authority, never by
        // mutating records or lifecycle state directly.
        staffDiscontent?.RestoreSnapshots(
            Array.Empty<StaffDiscontentSnapshot>());
    }

    private string FormatSubjectDiscontent()
    {
        if (staffDiscontent == null || actor == null)
            return "unavailable";
        string actorId = actor.Identity?.PersistentId ?? string.Empty;
        StaffDiscontentSnapshot snapshot = staffDiscontent.CaptureSnapshots()
            .FirstOrDefault(value => value != null
                && string.Equals(value.staffId, actorId, StringComparison.Ordinal));
        return snapshot == null
            ? "none"
            : snapshot.stage + "/" + snapshot.outcome
                + "/days=" + snapshot.lowMoodDays
                + "/departed=" + snapshot.departed;
    }

    private static CharacterActor[] LiveActors(ICharacterAiWorldRegistry world) =>
        world?.Characters
            .Where(value => value != null && !value.IsDead)
            .OrderBy(value => value.Identity?.PersistentId ?? string.Empty, StringComparer.Ordinal)
            .ToArray()
        ?? Array.Empty<CharacterActor>();

    private static bool IsDomainFixtureWork(WorkTypeId workTypeId) =>
        workTypeId == BuiltInWorkTypeIds.Repair
        || workTypeId == BuiltInWorkTypeIds.ThreatMitigation
        || workTypeId == BuiltInWorkTypeIds.Plumbing
        || workTypeId == BuiltInWorkTypeIds.Perform
        || workTypeId == BuiltInWorkTypeIds.AnimalCare
        || workTypeId == BuiltInWorkTypeIds.GrandProject;

    private IEnumerator PrepareDomainFixture(
        WorkTypeId workTypeId,
        Action<MaterialWorkFixture> publish,
        Action<string> fail)
    {
        if (workTypeId == BuiltInWorkTypeIds.Repair)
        {
            publish(PrepareRepairFixture(out string reason));
            if (!string.IsNullOrEmpty(reason)) fail(reason);
            yield break;
        }
        if (workTypeId == BuiltInWorkTypeIds.ThreatMitigation)
        {
            publish(PrepareThreatFixture(out string reason));
            if (!string.IsNullOrEmpty(reason)) fail(reason);
            yield return null;
            yield break;
        }
        if (workTypeId == BuiltInWorkTypeIds.Plumbing)
        {
            BuildableObject target = PreparePlumbingTarget(out string reason);
            if (target == null)
            {
                fail(reason);
                yield break;
            }

            IFluidInfrastructureQuery query =
                runtimeScope.Container.Resolve<IFluidInfrastructureQuery>();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline
                   && (!query.TryGetMaintenance(target, out float blockage, out float leak)
                       || blockage <= 0.01f && leak <= 0.01f))
            {
                yield return null;
            }
            if (!query.TryGetMaintenance(target, out float readyBlockage, out float readyLeak)
                || readyBlockage <= 0.01f && readyLeak <= 0.01f)
            {
                fail("actual wastewater backflow did not publish plumbing maintenance");
                yield break;
            }

            publish(new MaterialWorkFixture(
                target,
                $"plumbing:blockage={readyBlockage:0.###};leak={readyLeak:0.###}",
                prepareInvalidation: null,
                invalidate: (out string invalidationReason) =>
                {
                    if (target == null || target.isDestroy)
                    {
                        invalidationReason = "plumbing facility already destroyed";
                        return false;
                    }
                    target.DestroySelf();
                    invalidationReason = string.Empty;
                    return true;
                }));
            yield break;
        }

        if (workTypeId == BuiltInWorkTypeIds.Perform)
        {
            publish(PreparePerformFixture(out string reason));
            if (!string.IsNullOrEmpty(reason)) fail(reason);
            yield break;
        }

        if (workTypeId == BuiltInWorkTypeIds.AnimalCare)
        {
            IEnumerator setup = PrepareAnimalCareFixture(publish, fail);
            while (setup.MoveNext())
                yield return setup.Current;
            yield break;
        }

        if (workTypeId == BuiltInWorkTypeIds.GrandProject)
        {
            publish(PrepareGrandProjectFixture(out string reason));
            if (!string.IsNullOrEmpty(reason)) fail(reason);
            yield break;
        }

        fail("production-live domain fixture pending for " + workTypeId.Value);
    }

    private MaterialWorkFixture PreparePerformFixture(out string failureReason)
    {
        failureReason = string.Empty;
        if (!EnsurePerformanceRoom(out failureReason))
            return null;

        ICircusRuntime circus = runtimeScope.Container.Resolve<ICircusRuntime>();
        CircusProgramModule program = circus.Programs
            .Where(value => value != null
                && !value.requiresCaptive
                && !value.requiresWildlife)
            .OrderBy(value => value.programId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (program == null)
        {
            failureReason = "no authored participant-free circus program";
            return null;
        }
        if (!circus.TrySchedule(
                domainStage,
                program.programId,
                CircusLethalityPolicy.StopWhenDowned,
                Array.Empty<string>(),
                Array.Empty<string>(),
                out CircusShowOrder order,
                out failureReason))
        {
            failureReason = "circus schedule authority rejected fixture: "
                + failureReason;
            return null;
        }

        return new MaterialWorkFixture(
            domainStage,
            $"perform:order={order.orderId};program={order.programId}",
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                if (domainStage == null || domainStage.isDestroy)
                {
                    reason = "circus stage already destroyed";
                    return false;
                }
                domainStage.DestroySelf();
                reason = string.Empty;
                return true;
            });
    }

    private IEnumerator PrepareAnimalCareFixture(
        Action<MaterialWorkFixture> publish,
        Action<string> fail)
    {
        if (!EnsureAnimalRoom(out string roomFailure))
        {
            fail(roomFailure);
            yield break;
        }

        IWildlifeSpeciesCatalogProvider speciesCatalog =
            runtimeScope.Container.Resolve<IWildlifeSpeciesCatalogProvider>();
        IWildlifeCaptureRuntime capture =
            runtimeScope.Container.Resolve<IWildlifeCaptureRuntime>();
        IAnimalHusbandryQuery husbandry =
            runtimeScope.Container.Resolve<IAnimalHusbandryQuery>();
        IAnimalHusbandryCommand husbandryCommand =
            runtimeScope.Container.Resolve<IAnimalHusbandryCommand>();
        string speciesId = speciesCatalog.All
            .Where(value => value != null)
            .Select(value => value.SpeciesId)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (string.IsNullOrWhiteSpace(speciesId))
        {
            fail("wildlife species catalog has no authored species");
            yield break;
        }

        HashSet<Vector2Int> penFootprint = domainPen.BuildingData
            .GetGridPosList(domainPen.centerPos)
            .ToHashSet();
        GridCell domesticBirthCell = performanceRoom.Cells
            .Select(grid.GetGridCell)
            .Where(cell => cell != null
                && !penFootprint.Contains(cell.Position)
                && cell.AreaType != GridCellAreaType.BlockedExterior
                && grid.IsWalkable(cell.Position)
                && !cell.HasOccupantInLayer(GridLayer.Wildlife)
                && !cell.HasOccupantInLayer(GridLayer.Character)
                && !cell.HasOccupantInLayer(GridLayer.DownedCharacter)
                && !cell.HasOccupantInLayer(GridLayer.Building)
                && !cell.HasOccupantInLayer(GridLayer.Construction)
                && !cell.HasOccupantInLayer(GridLayer.Conveyor))
            .OrderBy(cell => Mathf.Abs(
                    cell.Position.x - domainPen.centerPos.x)
                + Mathf.Abs(cell.Position.y - domainPen.centerPos.y))
            .ThenBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .FirstOrDefault();
        if (domesticBirthCell == null)
        {
            fail("usable pen room has no exact lawful domestic-birth cell");
            yield break;
        }
        int wildlifeCountBefore = wildlifeRuntime.Wildlife.Count;
        int capturedCountBefore = capture.CapturedAnimals.Count;
        if (!wildlifeRuntime.TrySpawnDomesticBirth(
                speciesId,
                domesticBirthCell.Position,
                out WildlifeActor animal,
                out string spawnMessage)
            || animal == null)
        {
            fail("domestic-birth authority rejected fixture: " + spawnMessage);
            yield break;
        }
        rowScopedWildlifeIds.Add(animal.WildlifeId);
        WildlifeActor publishedBirth = grid.GetGridCell(
                animal.GridPosition)?.GetOccupant(GridLayer.Wildlife)
            as WildlifeActor;
        if (wildlifeRuntime.Wildlife.Count != wildlifeCountBefore + 1
            || !string.Equals(animal.SpeciesId, speciesId, StringComparison.Ordinal)
            || animal.GridPosition != domesticBirthCell.Position
            || publishedBirth == null
            || !string.Equals(
                publishedBirth.WildlifeId,
                animal.WildlifeId,
                StringComparison.Ordinal)
            || !string.Equals(
                publishedBirth.SpeciesId,
                speciesId,
                StringComparison.Ordinal))
        {
            fail("domestic-birth publication identity/state/layer mismatch");
            yield break;
        }
        string penId = domainPen.RequirePersistentInstanceId().Value;
        if (!capture.TryRegisterPenBorn(
                animal,
                penId,
                domesticBirthCell.Position,
                out string registerFailure))
        {
            fail("pen-born registration authority rejected fixture: "
                + registerFailure);
            yield break;
        }
        CapturedWildlifeState[] pennedMatches = capture.CapturedAnimals
            .Where(value => value != null
                && string.Equals(
                    value.wildlifeId,
                    animal.WildlifeId,
                    StringComparison.Ordinal))
            .ToArray();
        CapturedWildlifeState penned = pennedMatches.Length == 1
            ? pennedMatches[0]
            : null;
        WildlifeActor publishedPenned = grid.GetGridCell(
                domesticBirthCell.Position)?.GetOccupant(GridLayer.Wildlife)
            as WildlifeActor;
        if (capture.CapturedAnimals.Count != capturedCountBefore + 1
            || penned == null
            || !string.Equals(penned.speciesId, speciesId, StringComparison.Ordinal)
            || !string.Equals(penned.penId, penId, StringComparison.Ordinal)
            || penned.penPosition != domesticBirthCell.Position
            || penned.transportState != CapturedWildlifeTransportState.Penned
            || penned.escaped
            || !penned.isTamed
            || animal.State != WildlifeState.Captured
            || animal.GridPosition != domesticBirthCell.Position
            || publishedPenned == null
            || !string.Equals(
                publishedPenned.WildlifeId,
                animal.WildlifeId,
                StringComparison.Ordinal)
            || !string.Equals(
                publishedPenned.SpeciesId,
                speciesId,
                StringComparison.Ordinal)
            || publishedPenned.State != WildlifeState.Captured)
        {
            fail("pen-born registration identity/species/position/state/layer mismatch");
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + 8f;
        WildlifeInstanceId husbandryAnimalId = new(animal.WildlifeId);
        while (Time.realtimeSinceStartup < deadline
               && !husbandry.TryGetAnimal(husbandryAnimalId, out _))
        {
            yield return null;
        }
        if (!husbandry.TryGetAnimal(
                husbandryAnimalId,
                out HusbandryAnimalState projectedAnimal)
            || !projectedAnimal.Tamed)
        {
            fail("pen-born animal did not publish the tamed husbandry projection");
            yield break;
        }
        if (!husbandryCommand.DesignateSlaughter(
                husbandryAnimalId,
                true,
                out AnimalHusbandryFailure designationFailure))
        {
            fail("husbandry slaughter designation authority rejected fixture: "
                + designationFailure.Code);
            yield break;
        }
        runtimeScope.Container.Resolve<IFacilityCandidateCache>()
            .MarkDynamicStateDirty();
        deadline = Time.realtimeSinceStartup + 8f;
        while (Time.realtimeSinceStartup < deadline
               && !husbandry.TryGetWork(domainPen, actor, out _))
        {
            yield return null;
        }
        if (!husbandry.TryGetWork(
                domainPen,
                actor,
                out AnimalHusbandryWorkSnapshot workSnapshot))
        {
            fail("captured animal did not publish husbandry work");
            yield break;
        }
        if (workSnapshot.Kind != AnimalHusbandryWorkKind.Slaughter
            || !workSnapshot.AnimalId.Equals(husbandryAnimalId))
        {
            fail("husbandry authority published the wrong work identity: kind="
                + workSnapshot.Kind + "; animal="
                + workSnapshot.AnimalId.Value);
            yield break;
        }

        // The row explicitly tests AnimalCare at the highest work priority.
        // Publish that production policy before the preflight so the selector
        // observes the same authority that Brain will use when the row starts.
        work.SetWorkPriority(
            BuiltInWorkTypeIds.AnimalCare,
            WorkPriorityLevel.Priority1);
        AnimalCareAiPreflightSnapshot preflight =
            AnimalCareAiPreflight.Capture(
                actor,
                work,
                domainPen,
                husbandry,
                runtimeScope.Container.Resolve<IWorkPolicyRegistry>(),
                runtimeScope.Container.Resolve<IFacilityCandidateCache>());
        results.Add(WorkTypeLiveRow.Info(
            "animal-care-preflight",
            "birth=" + domesticBirthCell.Position + "; penned="
            + animal.GridPosition + "; "
            + preflight.Format()));
        if (!preflight.Passed)
        {
            fail("animal-care production preflight failed: "
                + preflight.Format());
            yield break;
        }

        publish(new MaterialWorkFixture(
            domainPen,
            $"animal-care:animal={animal.WildlifeId};kind={workSnapshot.Kind}",
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                if (domainPen == null || domainPen.isDestroy)
                {
                    reason = "animal pen already destroyed";
                    return false;
                }
                domainPen.DestroySelf();
                reason = string.Empty;
                return true;
            }));
    }

    private MaterialWorkFixture PrepareGrandProjectFixture(
        out string failureReason)
    {
        failureReason = string.Empty;
        IGrandProjectRuntime grand =
            runtimeScope.Container.Resolve<IGrandProjectRuntime>();
        BuildableObject office = worldRegistry.Buildings
            .Where(value => value != null
                && !value.isDestroy
                && value.SupportsWork(BuiltInWorkTypeIds.GrandProject)
                && value.HasSemanticTag("grand-project-office"))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (office == null)
        {
            BuildingSO authored = LoadAuthoredBuilding(data =>
                data.Facility?.SupportsWork(BuiltInWorkTypeIds.GrandProject) == true
                && data.HasSemanticTag("grand-project-office"));
            office = PlaceAuthoredBuilding(authored, out failureReason);
        }
        if (office == null)
            return null;

        GrandProjectDefinition definition = grand.Definitions
            .Where(value => value != null
                && grand.GetStatus(value.ProjectId, out _)
                    == GrandProjectStatus.Available)
            .OrderBy(value => value.ProjectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (definition == null)
        {
            failureReason = "no research-unlocked grand project is available";
            return null;
        }
        if (!grand.Start(definition.ProjectId, out string startMessage))
        {
            failureReason = "grand-project authority rejected start: "
                + startMessage;
            return null;
        }

        string destinationId = grand.State.destinationId;
        foreach (IGrouping<string, ItemAmountDefinition> group in
                 definition.Requirements.GroupBy(
                     value => value.ItemId,
                     StringComparer.Ordinal))
        {
            int amount = group.Sum(value => value.Amount);
            if (!physicalItems.SpawnItemAt(
                    group.Key,
                    amount,
                    office.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    destinationId,
                    out int spawned)
                || spawned != amount)
            {
                grand.CancelActive(out _);
                failureReason = "grand-project material delivery failed for "
                    + group.Key;
                return null;
            }
        }
        if (!grand.TryGetWork(
                office.RequirePersistentInstanceId(),
                out GrandProjectWorkSnapshot workSnapshot)
            || !workSnapshot.Available)
        {
            grand.CancelActive(out _);
            failureReason = "grand-project did not publish executable work: "
                + workSnapshot.UnavailableReason;
            return null;
        }

        return new MaterialWorkFixture(
            office,
            $"grand-project:id={definition.ProjectId};destination={destinationId}",
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                if (office == null || office.isDestroy)
                {
                    reason = "grand-project office already destroyed";
                    return false;
                }
                office.DestroySelf();
                reason = string.Empty;
                return true;
            });
    }

    private bool EnsurePerformanceRoom(out string failureReason)
    {
        return EnsureSingleDomainRoom(
            createPerformanceStage: true,
            out failureReason);
    }

    private bool EnsureAnimalRoom(out string failureReason)
    {
        if (domainPen != null && !domainPen.isDestroy
            && performanceRoom != null && performanceRoom.IsUsable)
        {
            failureReason = string.Empty;
            return true;
        }
        return EnsureSingleDomainRoom(
            createPerformanceStage: false,
            out failureReason);
    }

    private bool EnsureSingleDomainRoom(
        bool createPerformanceStage,
        out string failureReason,
        HashSet<string> rejectedCandidates = null,
        List<string> rejectionDetails = null)
    {
        rejectedCandidates ??= new HashSet<string>(StringComparer.Ordinal);
        rejectionDetails ??= new List<string>();
        failureReason = string.Empty;
        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO content = createPerformanceStage
            ? LoadAuthoredBuilding(data =>
                string.Equals(
                    data.GetAbility<BuildingFacilityPartAbility>()?.code,
                    "CS01",
                    StringComparison.Ordinal)
                && data.GetAbility<BuildingCircusStageAbility>() != null)
            : LoadAuthoredBuilding(data =>
                string.Equals(
                    data.GetAbility<BuildingFacilityPartAbility>()?.code,
                    "CB01",
                    StringComparison.Ordinal)
                && data.GetAbility<BuildingBeastPenAbility>() != null);
        BuildingSO audienceSeating = createPerformanceStage
            ? LoadAuthoredBuilding(data =>
                string.Equals(
                    data.GetAbility<BuildingFacilityPartAbility>()?.code,
                    "CS02",
                    StringComparison.Ordinal)
                && data.GetAudienceSeatingAbility()?.IsValid == true)
            : null;
        if (hallway == null || door == null || !door.IsInteriorDoor
            || door.Placement.Width != 1 || wall == null || content == null
            || createPerformanceStage && audienceSeating == null)
        {
            failureReason = "authored single-room assets invalid: hallway="
                + (hallway != null) + "; door=" + (door != null)
                + "; wall=" + (wall != null) + "; content="
                + (content != null) + "; kind="
                + (createPerformanceStage ? "CS01" : "CB01")
                + "; CS02=" + (audienceSeating != null);
            return false;
        }

        int contentWidth = Mathf.Max(1, content.Placement.Width);
        int seatingWidth = audienceSeating != null
            ? Mathf.Max(1, audienceSeating.Placement.Width)
            : 0;
        // Keep one real hallway/work-access cell between the door and the
        // domain facility.  A two-cell CB01 in a four-cell shell otherwise
        // consumes the complete room interior and publishes a usable room
        // that no worker can actually enter.
        int spanWidth = Mathf.Max(
            4,
            contentWidth + seatingWidth + 3);
        const int roomRow = 1;
        if (grid == null || grid.height <= roomRow
            || grid.width < spanWidth + 2)
        {
            failureReason = "official grid cannot host a " + spanWidth
                + "-cell horizontal "
                + (createPerformanceStage ? "CS01" : "CB01")
                + " room";
            return false;
        }

        int start = -1;
        bool selectedDoorOnLeft = true;
        Vector2Int selectedContentAnchor = default;
        Vector2Int selectedSeatingAnchor = default;
        List<Vector2Int> selectedConnector = null;
        HashSet<BuildableObject> selectedDisplacements = null;
        HashSet<WildlifeActor> selectedWildlife = null;
        string selectedCandidateKey = string.Empty;
        string lastRejected = "none";
        GridPathSearchResult preMutationSearch = grid.SearchPath(actor.GetNowXY());
        for (int x = 1; x <= grid.width - spanWidth - 1 && start < 0; x++)
        {
            bool free = true;
            bool hasExterior = false;
            HashSet<BuildableObject> candidates = new();
            HashSet<WildlifeActor> wildlifeCandidates = new();
            // RoomDetector and grid movement both use left/right adjacency.
            // Reserving y=0 and y=2 made this one-dimensional room fixture
            // depend on unrelated outdoor resource hosts in those rows.
            // Materialize only the authored horizontal room domain at y=1.
            for (int y = roomRow; y <= roomRow && free; y++)
            {
                for (int offset = 0; offset < spanWidth; offset++)
                {
                    Vector2Int position = new(x + offset, y);
                    GridCell cell = grid.GetGridCell(position);
                    if (cell == null)
                    {
                        free = false;
                        lastRejected = "missing:" + position;
                        break;
                    }
                    hasExterior |= cell.AreaType
                        != GridCellAreaType.DungeonInterior;
                    foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                    {
                        IGridOccupant occupant = cell.GetOccupant(layer);
                        if (occupant == null)
                            continue;
                        if (layer == GridLayer.Wildlife
                            && occupant is WildlifeActor wildlife
                            && wildlife.IsAlive
                            && wildlife.State != WildlifeState.Captured)
                        {
                            wildlifeCandidates.Add(wildlife);
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
                            free = false;
                            lastRejected = "hard-occupant:" + position
                                + ":" + layer + ":"
                                + occupant.GetType().Name;
                            break;
                        }
                        candidates.Add(movement);
                    }
                    if (!free)
                        break;
                }
            }
            if (free && hasExterior)
            {
                Vector2Int leftOutside = new(x - 1, 1);
                Vector2Int rightOutside = new(x + spanWidth, 1);
                bool leftConnected = TryPlanRoomConnector(
                    x,
                    spanWidth,
                    leftOutside,
                    preMutationSearch,
                    out List<Vector2Int> leftConnector,
                    out HashSet<BuildableObject> leftDisplacements,
                    out Vector2Int leftExternalStand,
                    out string leftFailure);
                bool rightConnected = TryPlanRoomConnector(
                    x,
                    spanWidth,
                    rightOutside,
                    preMutationSearch,
                    out List<Vector2Int> rightConnector,
                    out HashSet<BuildableObject> rightDisplacements,
                    out Vector2Int rightExternalStand,
                    out string rightFailure);
                if (!leftConnected && !rightConnected)
                {
                    lastRejected = "isolated-door:" + leftOutside + "="
                        + leftFailure + "/" + rightOutside + "="
                        + rightFailure;
                    continue;
                }
                bool preferLeft = leftConnected && (!rightConnected
                    || leftConnector.Count <= rightConnector.Count);
                bool[] doorOptions = preferLeft
                    ? new[] { true, false }
                    : new[] { false, true };
                foreach (bool doorOnLeft in doorOptions)
                {
                    if (doorOnLeft ? !leftConnected : !rightConnected)
                        continue;
                    if (!TryResolveRoomContentAnchor(
                            content,
                            audienceSeating,
                            x,
                            spanWidth,
                            doorOnLeft,
                            out Vector2Int contentAnchor,
                            out Vector2Int workStand,
                            out Vector2Int seatingAnchor))
                    {
                        lastRejected = "no-horizontal-work-stand:"
                            + x + ":" + (doorOnLeft ? "L" : "R");
                        continue;
                    }
                    string candidateKey = x + ":"
                        + (doorOnLeft ? "L" : "R") + ":"
                        + contentAnchor.x + ":stand=" + workStand.x
                        + ":seat=" + (audienceSeating != null
                            ? seatingAnchor.x.ToString()
                            : "none");
                    if (rejectedCandidates.Contains(candidateKey))
                        continue;
                    start = x;
                    selectedDoorOnLeft = doorOnLeft;
                    selectedContentAnchor = contentAnchor;
                    selectedSeatingAnchor = seatingAnchor;
                    selectedConnector = doorOnLeft
                        ? leftConnector
                        : rightConnector;
                    roomExternalExitStand = doorOnLeft
                        ? leftExternalStand
                        : rightExternalStand;
                    roomBaselineReachableSentinel = actor.GetNowXY();
                    selectedDisplacements = new HashSet<BuildableObject>(
                        candidates);
                    selectedDisplacements.UnionWith(
                        doorOnLeft
                            ? leftDisplacements
                            : rightDisplacements);
                    selectedWildlife = wildlifeCandidates;
                    WildlifeActor exitWildlife = grid.GetGridCell(
                            roomExternalExitStand.Value)
                        ?.GetOccupant(GridLayer.Wildlife) as WildlifeActor;
                    if (exitWildlife != null
                        && exitWildlife.IsAlive
                        && exitWildlife.State != WildlifeState.Captured)
                    {
                        selectedWildlife.Add(exitWildlife);
                    }
                    selectedCandidateKey = candidateKey;
                    break;
                }
            }
        }
        if (start < 0)
        {
            failureReason = "no safe " + spanWidth
                + "-cell horizontal exterior "
                + (createPerformanceStage ? "CS01" : "CB01")
                + " room span; last=" + lastRejected
                + (rejectionDetails.Count == 0
                    ? string.Empty
                    : "; rejected=" + string.Join(
                        " || ",
                        rejectionDetails.TakeLast(6)));
            return false;
        }

        HashSet<Vector2Int> reservedRoomCells = new(
            selectedConnector ?? Enumerable.Empty<Vector2Int>());
        if (roomExternalExitStand.HasValue)
            reservedRoomCells.Add(roomExternalExitStand.Value);
        for (int offset = 0; offset < spanWidth; offset++)
            reservedRoomCells.Add(new Vector2Int(start + offset, roomRow));
        if (!TryDisplaceRoomWildlife(
                selectedWildlife,
                reservedRoomCells,
                out failureReason))
        {
            return false;
        }

        foreach (Vector2Int position in selectedConnector)
        {
            GridCell cell = grid.GetGridCell(position);
            roomAreaSnapshots.Add(
                new FixtureAreaSnapshot(position, cell.AreaType));
            grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
            if (cell.AreaType != GridCellAreaType.DungeonInterior)
            {
                failureReason = "single-room connector area mutation failed at "
                    + position;
                return false;
            }
        }

        for (int y = roomRow; y <= roomRow; y++)
        {
            for (int offset = 0; offset < spanWidth; offset++)
            {
                Vector2Int position = new(start + offset, y);
                GridCell cell = grid.GetGridCell(position);
                roomAreaSnapshots.Add(
                    new FixtureAreaSnapshot(position, cell.AreaType));
                grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
                if (cell.AreaType != GridCellAreaType.DungeonInterior)
                {
                    failureReason = "single-room area mutation failed at "
                        + position;
                    return false;
                }
            }
        }
        foreach (BuildableObject movement in selectedDisplacements
                     .OrderBy(value => value.centerPos.y)
                     .ThenBy(value => value.centerPos.x)
                     .ThenBy(value => value.GridId))
        {
            GridLayer layer = movement.BuildingData.Placement.Layer;
            Vector2Int[] positions = movement.buildPoses.ToArray();
            if (!grid.RemoveOccupant(
                    movement,
                    layer,
                    positions,
                    movement.BuildingData.Placement.IsMovement))
            {
                failureReason = "single-room movement displacement failed: "
                    + movement.GridId;
                return false;
            }
            displacedRoomMovements.Add(new DisplacedMovementSnapshot(
                movement,
                layer,
                positions,
                movement.BuildingData.Placement.IsMovement));
        }

        foreach (Vector2Int position in selectedConnector)
        {
            if (grid.GetGridCell(position)?.GetOccupant(GridLayer.Hallway) == null
                && PlaceRoomFixture(
                    hallway,
                    position,
                    out failureReason) == null)
            {
                return false;
            }
        }

        if (PlaceRoomFixture(
                door,
                new Vector2Int(
                    selectedDoorOnLeft ? start : start + spanWidth - 1,
                    1),
                out failureReason) == null
            || PlaceRoomFixture(
                wall,
                new Vector2Int(
                    selectedDoorOnLeft ? start + spanWidth - 1 : start,
                    1),
                out failureReason) == null)
            return false;
        for (int offset = 1; offset < spanWidth - 1; offset++)
        {
            Vector2Int position = new(start + offset, 1);
            if (grid.GetGridCell(position)?.GetOccupant(GridLayer.Hallway) == null
                && PlaceRoomFixture(
                    hallway,
                    position,
                    out failureReason) == null)
                return false;
        }

        BuildableObject domainContent = PlaceRoomFixture(
            content,
            selectedContentAnchor,
            out failureReason);
        if (domainContent == null)
            return false;
        BuildableObject domainSeating = null;
        if (audienceSeating != null)
        {
            domainSeating = PlaceRoomFixture(
                audienceSeating,
                selectedSeatingAnchor,
                out failureReason);
            if (domainSeating == null)
                return false;
        }
        if (createPerformanceStage)
            domainStage = domainContent;
        else
            domainPen = domainContent;

        IRoomLayoutCache rooms =
            runtimeScope.Container.Resolve<IRoomLayoutCache>();
        rooms.Clear();
        if (!rooms.TryGetRoom(domainContent, out RoomInstance room)
            || !room.IsUsable)
        {
            string candidateFailure = room == null
                ? "single domain room was not published"
                : "single domain room unusable: cells=" + room.Cells.Count
                  + "; doors=" + room.Doors.Count
                  + "; walls=" + room.Walls.Count
                  + "; open=" + room.OpenBoundaryCount
                  + "; solid=" + room.SolidBoundaryCount
                  + "; closed=" + room.IsClosed
                  + "; hasDoor=" + room.HasDoor;
            return RetrySingleDomainRoomCandidate(
                createPerformanceStage,
                selectedCandidateKey,
                candidateFailure,
                rejectedCandidates,
                rejectionDetails,
                out failureReason);
        }
        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        if (!WorkTargetSelectionRules.IsReachable(domainContent, search))
        {
            string candidateFailure =
                "usable single domain room lacks production work access; "
                + "roomCells=" + room.Cells.Count
                + "; roomDoors=" + room.Doors.Count
                + "; closed=" + room.IsClosed
                + "; hasDoor=" + room.HasDoor + "; "
                + FormatRoomAccessDiagnostics(
                    domainContent,
                    new Vector2Int(
                        selectedDoorOnLeft
                            ? start
                            : start + spanWidth - 1,
                        1),
                    search);
            return RetrySingleDomainRoomCandidate(
                createPerformanceStage,
                selectedCandidateKey,
                candidateFailure,
                rejectedCandidates,
                rejectionDetails,
                out failureReason);
        }
        performanceRoom = room;
        results.Add(WorkTypeLiveRow.Info(
            createPerformanceStage
                ? "performance-room-fixture"
                : "animal-room-fixture",
            "bounds=" + start + "," + roomRow + ".."
                + (start + spanWidth - 1) + "," + roomRow
                + "; cells=" + room.Cells.Count
                + "; doors=" + room.Doors.Count
                + "; doorSide=" + (selectedDoorOnLeft ? "left" : "right")
                + "; connectorCells=" + selectedConnector.Count
                + "; displacedWildlife=" + displacedRoomWildlife.Count
                + "; content="
                + domainContent.RequirePersistentInstanceId().Value
                + "; seating=" + (domainSeating != null
                    ? domainSeating.RequirePersistentInstanceId().Value
                    : "none")));
        return true;
    }

    private static bool TryResolveRoomContentAnchor(
        BuildingSO content,
        BuildingSO seating,
        int roomStart,
        int roomWidth,
        bool doorOnLeft,
        out Vector2Int anchor,
        out Vector2Int workStand,
        out Vector2Int seatingAnchor)
    {
        anchor = default;
        workStand = default;
        seatingAnchor = default;
        int interiorMinimum = roomStart + 1;
        int interiorMaximum = roomStart + roomWidth - 2;
        var contentCandidates = Enumerable.Range(
                interiorMinimum,
                interiorMaximum - interiorMinimum + 1)
            .Select(x => new Vector2Int(x, 1))
            .Select(candidate => new
            {
                Anchor = candidate,
                Footprint = content.GetGridPosList(candidate).ToArray()
            })
            .Where(candidate => candidate.Footprint.Length > 0
                && candidate.Footprint.All(position =>
                    position.y == 1
                    && position.x >= interiorMinimum
                    && position.x <= interiorMaximum))
            .Select(candidate => new
            {
                candidate.Anchor,
                candidate.Footprint,
                MinimumX = candidate.Footprint.Min(position => position.x),
                MaximumX = candidate.Footprint.Max(position => position.x)
            })
            .Select(candidate => new
            {
                candidate.Anchor,
                candidate.MinimumX,
                candidate.MaximumX,
                Stand = new Vector2Int(
                    doorOnLeft
                        ? candidate.MinimumX - 1
                        : candidate.MaximumX + 1,
                    1)
            })
            .Where(candidate => candidate.Stand.x >= interiorMinimum
                && candidate.Stand.x <= interiorMaximum)
            .ToArray();
        if (contentCandidates.Length == 0)
            return false;

        if (seating == null)
        {
            var selected = contentCandidates
                .OrderBy(candidate => doorOnLeft
                    ? -candidate.MaximumX
                    : candidate.MinimumX)
                .ThenBy(candidate => candidate.Anchor.x)
                .First();
            anchor = selected.Anchor;
            workStand = selected.Stand;
            return true;
        }

        var seatingCandidates = Enumerable.Range(
                interiorMinimum,
                interiorMaximum - interiorMinimum + 1)
            .Select(x => new Vector2Int(x, 1))
            .Select(candidate => new
            {
                Anchor = candidate,
                Footprint = seating.GetGridPosList(candidate).ToArray()
            })
            .Where(candidate => candidate.Footprint.Length > 0
                && candidate.Footprint.All(position =>
                    position.y == 1
                    && position.x >= interiorMinimum
                    && position.x <= interiorMaximum))
            .Select(candidate => new
            {
                candidate.Anchor,
                MinimumX = candidate.Footprint.Min(position => position.x),
                MaximumX = candidate.Footprint.Max(position => position.x)
            })
            .ToArray();
        var layouts = contentCandidates
            .SelectMany(contentCandidate => seatingCandidates
                .Where(seatingCandidate => doorOnLeft
                    ? seatingCandidate.MinimumX > contentCandidate.MaximumX
                    : seatingCandidate.MaximumX < contentCandidate.MinimumX)
                .Select(seatingCandidate => new
                {
                    Content = contentCandidate,
                    Seating = seatingCandidate
                }))
            .OrderBy(layout => doorOnLeft
                ? layout.Content.MinimumX
                : -layout.Content.MaximumX)
            .ThenBy(layout => doorOnLeft
                ? -layout.Seating.MaximumX
                : layout.Seating.MinimumX)
            .ThenBy(layout => layout.Content.Anchor.x)
            .ToArray();
        if (layouts.Length == 0)
            return false;
        anchor = layouts[0].Content.Anchor;
        workStand = layouts[0].Content.Stand;
        seatingAnchor = layouts[0].Seating.Anchor;
        return true;
    }

    private bool RetrySingleDomainRoomCandidate(
        bool createPerformanceStage,
        string candidateKey,
        string candidateFailure,
        HashSet<string> rejectedCandidates,
        List<string> rejectionDetails,
        out string failureReason)
    {
        rejectedCandidates.Add(candidateKey);
        rejectionDetails.Add(candidateKey + "=" + candidateFailure);
        if (!CleanupRoomFixture(out string cleanupFailure))
        {
            failureReason = candidateFailure
                + "; rollback=" + cleanupFailure;
            return false;
        }
        return EnsureSingleDomainRoom(
            createPerformanceStage,
            out failureReason,
            rejectedCandidates,
            rejectionDetails);
    }

    private string FormatRoomAccessDiagnostics(
        BuildableObject content,
        Vector2Int doorPosition,
        GridPathSearchResult search)
    {
        IReadOnlyList<Vector2Int> footprint =
            content.BuildingData.GetGridPosList(content.centerPos);
        HashSet<Vector2Int> footprintSet = footprint.ToHashSet();
        Vector2Int[] directions =
        {
            Vector2Int.left,
            Vector2Int.right
        };
        string[] stands = footprint
            .SelectMany(position => directions.Select(direction =>
                position + direction))
            .Where(position => !footprintSet.Contains(position))
            .Distinct()
            .OrderBy(position => position.y)
            .ThenBy(position => position.x)
            .Select(position => FormatRoomCell(position, search))
            .ToArray();
        return "actor=" + actor.GetNowXY()
            + "; door=" + FormatRoomCell(doorPosition, search)
            + "; content=" + string.Join(",", footprint)
            + "; stands=" + string.Join(" | ", stands);
    }

    private string FormatRoomCell(
        Vector2Int position,
        GridPathSearchResult search)
    {
        GridCell cell = grid.GetGridCell(position);
        if (cell == null)
            return position + "[missing]";
        string occupants = string.Join(",", cell.GetAllOccupants()
            .Where(value => value != null)
            .Select(value => value.GetType().Name));
        return position + "[area=" + cell.AreaType
            + ",walk=" + grid.IsWalkable(position)
            + ",reach=" + search.ContainsPosition(position)
            + ",occupants=" + occupants + "]";
    }

    private bool TryDisplaceRoomWildlife(
        IEnumerable<WildlifeActor> wildlife,
        ISet<Vector2Int> reservedCells,
        out string failureReason)
    {
        failureReason = string.Empty;
        foreach (WildlifeActor animal in (wildlife
                     ?? Enumerable.Empty<WildlifeActor>())
                 .Where(value => value != null)
                 .OrderBy(value => value.WildlifeId, StringComparer.Ordinal))
        {
            Vector2Int origin = animal.GridPosition;
            GridCell destination = grid.GetCells()
                .Where(cell => cell != null
                    && !reservedCells.Contains(cell.Position)
                    && Mathf.Abs(cell.Position.x - origin.x)
                        + Mathf.Abs(cell.Position.y - origin.y) <= 12
                    && CanPlaceWildlifeAt(
                        grid,
                        cell.Position,
                        animal.CanEnterDungeon))
                .OrderBy(cell => Mathf.Abs(cell.Position.x - origin.x)
                    + Mathf.Abs(cell.Position.y - origin.y))
                .ThenBy(cell => cell.Position.y)
                .ThenBy(cell => cell.Position.x)
                .FirstOrDefault();
            if (destination == null)
            {
                failureReason = "no bounded lawful wildlife displacement for "
                    + animal.WildlifeId + " at " + origin;
                return false;
            }

            displacedRoomWildlife.Add(new DisplacedWildlifeSnapshot(
                animal,
                origin,
                destination.Position));
            animal.WarpTo(destination.Position);
            if (animal.GridPosition != destination.Position
                || grid.GetGridCell(destination.Position)?.ContainsOccupant(
                    GridLayer.Wildlife,
                    animal) != true)
            {
                failureReason = "wildlife displacement authority failed for "
                    + animal.WildlifeId + ":" + origin + "->"
                    + destination.Position;
                return false;
            }
        }
        return true;
    }

    private static bool CanPlaceWildlifeAt(
        Grid targetGrid,
        Vector2Int position,
        bool canEnterDungeon)
    {
        GridCell cell = targetGrid?.GetGridCell(position);
        if (cell == null
            || !targetGrid.IsWalkable(position)
            || cell.HasOccupantInLayer(GridLayer.Wildlife)
            || cell.AreaType == GridCellAreaType.BlockedExterior)
        {
            return false;
        }

        if (cell.AreaType == GridCellAreaType.ExteriorPath
            && !WildlifeRuntime.IsOutdoorSurfaceCell(targetGrid, cell))
        {
            return false;
        }

        return canEnterDungeon
            || cell.AreaType != GridCellAreaType.DungeonInterior;
    }

    private bool TryPlanRoomConnector(
        int roomStart,
        int roomWidth,
        Vector2Int outsideDoor,
        GridPathSearchResult existingReachability,
        out List<Vector2Int> connector,
        out HashSet<BuildableObject> displacements,
        out Vector2Int externalStand,
        out string failureReason)
    {
        connector = new List<Vector2Int>();
        displacements = new HashSet<BuildableObject>();
        externalStand = default;
        failureReason = string.Empty;
        if (existingReachability.ContainsPosition(outsideDoor))
        {
            externalStand = outsideDoor;
            return true;
        }

        bool IsRoomCell(Vector2Int position) =>
            position.x >= roomStart
            && position.x < roomStart + roomWidth
            && position.y == outsideDoor.y;

        bool TryGetMutableCell(
            Vector2Int position,
            out HashSet<BuildableObject> movements)
        {
            movements = new HashSet<BuildableObject>();
            if (position.x < 0 || position.x >= grid.width
                || position.y < 0 || position.y >= grid.height
                || position.y != outsideDoor.y
                || IsRoomCell(position))
            {
                return false;
            }
            GridCell cell = grid.GetGridCell(position);
            if (cell == null)
                return false;
            foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
            {
                IGridOccupant occupant = cell.GetOccupant(layer);
                if (occupant == null)
                    continue;
                if ((layer != GridLayer.Building
                        && layer != GridLayer.Hallway)
                    || occupant is not BuildableObject movement
                    || movement is Facility
                    || movement is Door
                    || movement.Facility != null
                    || !movement.IsGridMovement
                    || movement.BlocksGridMovement)
                {
                    return false;
                }
                movements.Add(movement);
            }
            return true;
        }

        if (!TryGetMutableCell(outsideDoor, out _))
        {
            failureReason = "door-outside-not-mutable";
            return false;
        }

        Queue<Vector2Int> open = new Queue<Vector2Int>();
        Dictionary<Vector2Int, Vector2Int> previous =
            new Dictionary<Vector2Int, Vector2Int>();
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>
        {
            outsideDoor
        };
        open.Enqueue(outsideDoor);
        Vector2Int[] directions =
        {
            Vector2Int.left,
            Vector2Int.right
        };
        Vector2Int lastMutable = default;
        bool connected = false;
        while (open.Count > 0 && !connected)
        {
            Vector2Int current = open.Dequeue();
            foreach (Vector2Int direction in directions)
            {
                Vector2Int next = current + direction;
                if (next.x < 0 || next.x >= grid.width
                    || next.y < 0 || next.y >= grid.height
                    || IsRoomCell(next)
                    || visited.Contains(next))
                    continue;
                if (existingReachability.ContainsPosition(next))
                {
                    lastMutable = current;
                    externalStand = next;
                    connected = true;
                    break;
                }
                if (!TryGetMutableCell(next, out _))
                    continue;
                visited.Add(next);
                previous[next] = current;
                open.Enqueue(next);
            }
        }
        if (!connected)
        {
            failureReason = "no-safe-corridor-to-reachable-cell";
            return false;
        }

        Vector2Int cursor = lastMutable;
        connector.Add(cursor);
        while (cursor != outsideDoor)
        {
            cursor = previous[cursor];
            connector.Add(cursor);
        }
        connector.Reverse();
        foreach (Vector2Int position in connector)
        {
            if (!TryGetMutableCell(position, out HashSet<BuildableObject> movements))
            {
                failureReason = "connector-became-invalid:" + position;
                return false;
            }
            displacements.UnionWith(movements);
        }
        return true;
    }

    private bool EnsurePerformanceAndAnimalRoomFixture(out string failureReason)
    {
        failureReason = string.Empty;
        if (performanceRoom != null && performanceRoom.IsUsable
            && domainStage != null && !domainStage.isDestroy
            && domainPen != null && !domainPen.isDestroy)
        {
            return true;
        }
        if (roomFixtureBuildings.Count > 0)
        {
            failureReason = "performance room fixture was partially invalidated";
            return false;
        }

        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Hallway.asset");
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        BuildingSO stage = LoadAuthoredBuilding(data =>
            string.Equals(
                data.GetAbility<BuildingFacilityPartAbility>()?.code,
                "CS01",
                StringComparison.Ordinal)
            && data.GetAbility<BuildingCircusStageAbility>() != null);
        BuildingSO pen = LoadAuthoredBuilding(data =>
            data.GetAbility<BuildingBeastPenAbility>() != null);
        if (hallway == null || door == null || !door.IsInteriorDoor
            || door.Placement.Width != 1 || wall == null || stage == null
            || pen == null || stage.Placement.Width > 2)
        {
            failureReason = "authored performance room assets invalid: hallway="
                + (hallway != null) + "; door=" + (door != null)
                + "; wall=" + (wall != null) + "; CS01=" + (stage != null)
                + "; pen=" + (pen != null);
            return false;
        }
        if (grid == null || grid.height < 3 || grid.width < 10)
        {
            failureReason = "official grid cannot host an 8x3 performance room";
            return false;
        }

        int start = -1;
        HashSet<BuildableObject> selectedDisplacements = null;
        string lastRejected = "none";
        for (int x = 1; x <= grid.width - 9 && start < 0; x++)
        {
            bool free = true;
            bool hasExterior = false;
            HashSet<BuildableObject> candidates = new();
            for (int y = 0; y < 3 && free; y++)
            {
                for (int offset = 0; offset < 8; offset++)
                {
                    Vector2Int position = new(x + offset, y);
                    GridCell cell = grid.GetGridCell(position);
                    if (cell == null)
                    {
                        free = false;
                        lastRejected = "missing:" + position;
                        break;
                    }
                    hasExterior |= cell.AreaType != GridCellAreaType.DungeonInterior;
                    foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                    {
                        IGridOccupant occupant = cell.GetOccupant(layer);
                        if (occupant == null)
                            continue;
                        if ((layer != GridLayer.Building && layer != GridLayer.Hallway)
                            || occupant is not BuildableObject movement
                            || movement is Facility
                            || movement is Door
                            || movement.Facility != null
                            || !movement.IsGridMovement
                            || movement.BlocksGridMovement)
                        {
                            free = false;
                            lastRejected = "hard-occupant:" + position
                                + ":" + layer + ":" + occupant.GetType().Name;
                            break;
                        }
                        candidates.Add(movement);
                    }
                    if (!free)
                        break;
                }
            }
            if (free && hasExterior)
            {
                start = x;
                selectedDisplacements = candidates;
            }
        }
        if (start < 0)
        {
            failureReason = "no safe 8x3 exterior performance-room span; last="
                + lastRejected;
            return false;
        }

        for (int y = 0; y < 3; y++)
        {
            for (int offset = 0; offset < 8; offset++)
            {
                Vector2Int position = new(start + offset, y);
                GridCell cell = grid.GetGridCell(position);
                roomAreaSnapshots.Add(new FixtureAreaSnapshot(position, cell.AreaType));
                grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
                if (cell.AreaType != GridCellAreaType.DungeonInterior)
                {
                    failureReason = "performance room area mutation failed at " + position;
                    return false;
                }
            }
        }
        foreach (BuildableObject movement in selectedDisplacements
                     .OrderBy(value => value.centerPos.y)
                     .ThenBy(value => value.centerPos.x)
                     .ThenBy(value => value.GridId))
        {
            GridLayer layer = movement.BuildingData.Placement.Layer;
            Vector2Int[] positions = movement.buildPoses.ToArray();
            if (!grid.RemoveOccupant(
                    movement,
                    layer,
                    positions,
                    movement.BuildingData.Placement.IsMovement))
            {
                failureReason = "performance room movement displacement failed: "
                    + movement.GridId;
                return false;
            }
            displacedRoomMovements.Add(new DisplacedMovementSnapshot(
                movement,
                layer,
                positions,
                movement.BuildingData.Placement.IsMovement));
        }

        for (int offset = 0; offset < 8; offset++)
        {
            if (PlaceRoomFixture(wall, new Vector2Int(start + offset, 0), out failureReason) == null
                || PlaceRoomFixture(wall, new Vector2Int(start + offset, 2), out failureReason) == null)
                return false;
        }
        if (PlaceRoomFixture(door, new Vector2Int(start, 1), out failureReason) == null
            || PlaceRoomFixture(wall, new Vector2Int(start + 7, 1), out failureReason) == null)
            return false;
        for (int offset = 1; offset < 7; offset++)
        {
            Vector2Int position = new(start + offset, 1);
            if (grid.GetGridCell(position)?.GetOccupant(GridLayer.Hallway) == null
                && PlaceRoomFixture(hallway, position, out failureReason) == null)
                return false;
        }

        domainPen = PlaceRoomFixture(
            pen,
            new Vector2Int(start + 1, 1),
            out failureReason);
        domainStage = PlaceRoomFixture(
            stage,
            new Vector2Int(start + 3, 1),
            out failureReason);
        if (domainPen == null || domainStage == null)
            return false;

        IRoomLayoutCache rooms = runtimeScope.Container.Resolve<IRoomLayoutCache>();
        rooms.Clear();
        if (!rooms.TryGetRoom(domainStage, out RoomInstance room)
            || !room.IsUsable
            || !rooms.TryGetRoom(domainPen, out RoomInstance penRoom)
            || !ReferenceEquals(room, penRoom))
        {
            failureReason = room == null
                ? "CS01 room was not published"
                : "CS01 room unusable: cells=" + room.Cells.Count
                  + "; doors=" + room.Doors.Count
                  + "; walls=" + room.Walls.Count
                  + "; open=" + room.OpenBoundaryCount
                  + "; solid=" + room.SolidBoundaryCount
                  + "; closed=" + room.IsClosed
                  + "; hasDoor=" + room.HasDoor;
            return false;
        }
        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        if (!WorkTargetSelectionRules.IsReachable(domainStage, search)
            || !WorkTargetSelectionRules.IsReachable(domainPen, search))
        {
            failureReason = "usable performance room lacks production work access";
            return false;
        }
        performanceRoom = room;
        results.Add(WorkTypeLiveRow.Info(
            "performance-room-fixture",
            "bounds=" + start + ",0.." + (start + 7) + ",2"
            + "; cells=" + room.Cells.Count + "; doors=" + room.Doors.Count
            + "; stage=" + domainStage.RequirePersistentInstanceId().Value
            + "; pen=" + domainPen.RequirePersistentInstanceId().Value));
        return true;
    }

    private BuildableObject PlaceRoomFixture(
        BuildingSO data,
        Vector2Int position,
        out string failureReason)
    {
        BuildableObject building = PlaceAuthoredBuildingAt(
            data,
            position,
            out failureReason);
        if (building != null)
            roomFixtureBuildings.Add(building);
        return building;
    }

    private bool EnsureGrandProjectResearchPrerequisite(
        IObjectResolver resolver,
        out string detail)
    {
        detail = string.Empty;
        if (baseline == null || baseline.Count == 0)
        {
            detail = "save baseline was not captured before fixture research promotion";
            return false;
        }

        IResearchProjectCatalog catalog =
            resolver.Resolve<IResearchProjectCatalog>();
        HashSet<string> closure = new(StringComparer.Ordinal);
        foreach (string projectId in RequiredFixtureResearchIds)
        {
            if (!catalog.TryGet(
                    new ResearchProjectId(projectId),
                    out ResearchProjectSO project))
            {
                detail = "authored research prerequisite missing: " + projectId;
                return false;
            }
            AddResearchClosure(project, closure);
        }

        IGrandProjectRuntime grand = resolver.Resolve<IGrandProjectRuntime>();
        GrandProjectDefinition grandDefinition = grand.Definitions
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.RequiredResearchId))
            .OrderBy(value => value.ProjectId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (grandDefinition != null
            && catalog.TryGet(
                new ResearchProjectId(grandDefinition.RequiredResearchId),
                out ResearchProjectSO grandProject))
        {
            AddResearchClosure(grandProject, closure);
        }

        List<DungeonSaveSectionEnvelope> fixtureSave = baseline
            .Select(CloneEnvelope)
            .ToList();
        DungeonSaveSectionEnvelope researchEnvelope = fixtureSave.FirstOrDefault(
            value => string.Equals(
                value.sectionId,
                BlueprintResearchSaveSection.Id,
                StringComparison.Ordinal));
        if (researchEnvelope == null)
        {
            detail = "official save registry has no research.blueprints section";
            return false;
        }

        DungeonResearchSaveData research = JsonUtility.FromJson<
            DungeonResearchSaveData>(researchEnvelope.payloadJson);
        if (research == null)
        {
            detail = "research.blueprints baseline payload is invalid";
            return false;
        }
        research.completedProjectIds ??= new List<string>();
        research.projectProgress ??=
            new List<DungeonResearchProjectProgressSaveData>();
        research.projectQueue ??= new List<DungeonResearchQueueEntrySaveData>();
        foreach (string projectId in closure.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            research.completedProjectIds.RemoveAll(value => string.Equals(
                value,
                projectId,
                StringComparison.Ordinal));
            research.completedProjectIds.Add(projectId);
            research.projectProgress.RemoveAll(value => value != null
                && string.Equals(
                    value.projectId,
                    projectId,
                    StringComparison.Ordinal));
            research.projectQueue.RemoveAll(value => value != null
                && string.Equals(
                    value.projectId,
                    projectId,
                    StringComparison.Ordinal));
            if (string.Equals(
                    research.activeProjectId,
                    projectId,
                    StringComparison.Ordinal))
            {
                research.activeProjectId = string.Empty;
            }
        }
        research.completedProjectIds = research.completedProjectIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        researchEnvelope.payloadJson = JsonUtility.ToJson(research);

        DungeonGameRestoreReport report = new();
        if (!saveRegistry.RestoreAll(fixtureSave, report) || !report.Success)
        {
            detail = "legal fixture research restore failed: "
                + string.Join(" | ", report.Errors);
            return false;
        }

        BlueprintResearchRuntime runtime = resolver
            .Resolve<ProgressionSceneRuntimeReferences>()
            .BlueprintResearch;
        string[] missing = closure
            .Where(value => !runtime.State.Projects.IsCompleted(
                new ResearchProjectId(value)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
        {
            detail = "fixture research restore did not publish: "
                + string.Join(",", missing);
            return false;
        }

        detail = "save-registry research closure="
            + string.Join(",", closure.OrderBy(
                value => value,
                StringComparer.Ordinal));
        return true;
    }

    private bool TryAddDeterministicWorldResourceFixture(
        List<DungeonSaveSectionEnvelope> fixtureSave,
        out string detail)
    {
        const string PatchId = "wildlife-habitat:qa-worktype-brush";
        const string NodeId = "building:qa-worktype-brush";
        detail = string.Empty;
        if (grid == null || actor == null)
        {
            detail = "world-resource fixture requires the official grid and subject";
            return false;
        }

        DungeonSaveSectionEnvelope wildlifeEnvelope = fixtureSave.FirstOrDefault(
            value => string.Equals(value.sectionId, WildlifeSaveSection.Id, StringComparison.Ordinal));
        DungeonSaveSectionEnvelope resourceEnvelope = fixtureSave.FirstOrDefault(
            value => string.Equals(value.sectionId, WorldResourceSaveSection.Id, StringComparison.Ordinal));
        if (wildlifeEnvelope == null || resourceEnvelope == null)
        {
            detail = "official save registry lacks wildlife/world-resource sections";
            return false;
        }

        DungeonWildlifeSaveData wildlife =
            JsonUtility.FromJson<DungeonWildlifeSaveData>(wildlifeEnvelope.payloadJson);
        DungeonWorldResourceSaveData resources =
            JsonUtility.FromJson<DungeonWorldResourceSaveData>(resourceEnvelope.payloadJson);
        if (wildlife?.ecosystem == null || resources == null)
        {
            detail = "wildlife/world-resource baseline payload is invalid";
            return false;
        }
        wildlife.ecosystem.patches ??= new List<WildlifeHabitatPatchSaveData>();
        resources.nodes ??= new List<WorldResourceNodeSaveData>();

        // A previous interrupted verifier run can leave this deterministic
        // fixture in the captured baseline. Remove only the verifier-owned
        // rows before choosing a cell so they cannot exclude themselves.
        wildlife.ecosystem.patches.RemoveAll(value => value != null
            && string.Equals(value.patchId, PatchId, StringComparison.Ordinal));
        resources.nodes.RemoveAll(value => value != null
            && string.Equals(
                value.buildingInstanceId,
                NodeId,
                StringComparison.Ordinal));

        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        HashSet<Vector2Int> occupiedResourceCells = resources.nodes
            .Where(value => value != null)
            .Select(value => new Vector2Int(value.gridX, value.gridY))
            .ToHashSet();
        GridCell selected = grid.GetCells()
            .Where(value => value != null
                && value.AreaType == GridCellAreaType.ExteriorPath
                && value.TerrainType == GridCellTerrainType.Dry
                && value.IsWalkableArea
                && WildlifeRuntime.IsOutdoorSurfaceCell(grid, value)
                && search.ContainsPosition(value.Position)
                && !occupiedResourceCells.Contains(value.Position))
            .OrderBy(value => Mathf.Abs(value.Position.x - actor.GetNowXY().x)
                + Mathf.Abs(value.Position.y - actor.GetNowXY().y))
            .ThenBy(value => value.Position.y)
            .ThenBy(value => value.Position.x)
            .FirstOrDefault();
        if (selected == null)
        {
            detail = "no reachable dry outdoor cell can host deterministic brush resources";
            return false;
        }

        wildlife.ecosystem.patches.Add(new WildlifeHabitatPatchSaveData
        {
            patchId = PatchId,
            habitatType = WildlifeHabitatType.Brush,
            gridX = selected.Position.x,
            gridY = selected.Position.y,
            radius = 2,
            resourceCapacity = 10f,
            currentResource = 10f,
            regenPerSecond = 0.02f,
            danger = 0f,
            preferredSpeciesTags = new List<string>()
        });
        resources.nodes.Add(new WorldResourceNodeSaveData
        {
            buildingInstanceId = NodeId,
            gridX = selected.Position.x,
            gridY = selected.Position.y,
            sources = new List<WorldResourceSourceSaveData>
            {
                new WorldResourceSourceSaveData
                {
                    workTypeId = BuiltInWorkTypeIds.Gather.Value,
                    recipeId = "source:grass",
                    completedWork = 0f,
                    remainingCycles = -1
                },
                new WorldResourceSourceSaveData
                {
                    workTypeId = BuiltInWorkTypeIds.Logging.Value,
                    recipeId = "source:logging",
                    completedWork = 0f,
                    remainingCycles = 1
                }
            }
        });
        wildlifeEnvelope.payloadJson = JsonUtility.ToJson(wildlife);
        resourceEnvelope.payloadJson = JsonUtility.ToJson(resources);
        detail = "deterministic-brush=" + selected.Position
            + "; patch=" + PatchId + "; node=" + NodeId;
        return true;
    }

    private bool HasAvailableWorldResource(WorkTypeId workTypeId) =>
        worldResources?.Nodes.Any(value => value != null
            && worldResources.TryGetWork(
                value,
                workTypeId,
                out WorldResourceWorkSnapshot snapshot)
            && snapshot.Available) == true;

    private static void AddResearchClosure(
        ResearchProjectSO project,
        ISet<string> closure)
    {
        if (project == null || !closure.Add(project.ProjectId.Value))
            return;
        foreach (ResearchProjectSO prerequisite in project.Prerequisites
                     .Where(value => value != null)
                     .OrderBy(
                         value => value.ProjectId.Value,
                         StringComparer.Ordinal))
        {
            AddResearchClosure(prerequisite, closure);
        }
    }

    private static DungeonSaveSectionEnvelope CloneEnvelope(
        DungeonSaveSectionEnvelope source) => new()
    {
        sectionId = source?.sectionId ?? string.Empty,
        sectionVersion = source?.sectionVersion ?? 1,
        restorePhase = source?.restorePhase
            ?? DungeonSaveRestorePhase.RuntimeState,
        optional = source?.optional ?? false,
        payloadJson = source?.payloadJson ?? string.Empty
    };

    private MaterialWorkFixture PrepareRepairFixture(out string failureReason)
    {
        failureReason = string.Empty;
        IBuildingStructuralIntegrityRuntime integrity =
            runtimeScope.Container.Resolve<IBuildingStructuralIntegrityRuntime>();
        BuildableObject target = worldRegistry.Buildings
            .Where(value => value != null
                && !value.isDestroy
                && value.SupportsWork(BuiltInWorkTypeIds.Repair)
                && integrity.TryGet(value, out _)
                && IsReachableFromSubject(value))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target == null)
        {
            BuildingSO authored = LoadAuthoredBuilding(data =>
                data.Facility?.SupportsWork(BuiltInWorkTypeIds.Repair) == true
                && data.GetAbility<BuildingStructuralIntegrityAbility>() != null);
            target = PlaceAuthoredBuilding(authored, out failureReason);
        }
        if (target == null)
        {
            if (string.IsNullOrEmpty(failureReason))
                failureReason = "no authored structural repair facility";
            return null;
        }

        BuildingStructuralDamageResult damaged = integrity.ApplyDamage(target, 24f);
        if (!damaged.Applied || damaged.Destroyed)
        {
            failureReason = "structural damage authority rejected nonlethal repair damage";
            return null;
        }

        bool Redamage(out string reason)
        {
            if (target == null || target.isDestroy)
            {
                reason = "repair target disappeared before invalidation phase";
                return false;
            }
            BuildingStructuralDamageResult result = integrity.ApplyDamage(target, 12f);
            reason = result.Applied && !result.Destroyed
                ? string.Empty
                : "repair target could not be re-damaged";
            return result.Applied && !result.Destroyed;
        }
        return new MaterialWorkFixture(
            target,
            $"repair:hp={damaged.Snapshot.CurrentHitPoints:0.###}/{damaged.Snapshot.MaxHitPoints:0.###}",
            prepareInvalidation: Redamage,
            invalidate: (out string reason) =>
            {
                if (!integrity.TryGet(target, out BuildingStructuralIntegritySnapshot before))
                {
                    reason = "repair target structural state missing";
                    return false;
                }
                BuildingStructuralDamageResult destroyed =
                    integrity.ApplyDamage(target, before.MaxHitPoints * 2f);
                reason = destroyed.Destroyed
                    ? string.Empty
                    : "structural damage did not destroy repair target";
                return destroyed.Destroyed;
            });
    }

    private MaterialWorkFixture PrepareThreatFixture(out string failureReason)
    {
        failureReason = string.Empty;
        IOffenseWorldSimulation offenseWorld =
            runtimeScope.Container.Resolve<IOffenseWorldSimulation>();
        IOffenseContentCatalog content =
            runtimeScope.Container.Resolve<IOffenseContentCatalog>();
        IOffenseUrgentMitigationRuntime mitigation =
            runtimeScope.Container.Resolve<IOffenseUrgentMitigationRuntime>();
        OffenseUrgentSiteDefinitionSO definition = content.UrgentSites
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.mitigationWorkTypeId)
                && !string.IsNullOrWhiteSpace(value.mitigationItemId)
                && value.mitigationItemAmount > 0)
            .OrderBy(value => value.urgentSiteId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (definition == null)
        {
            failureReason = "no authored urgent mitigation definition";
            return null;
        }
        WorkTypeId semanticWork = new(definition.mitigationWorkTypeId);
        BuildableObject facility = worldRegistry.Buildings
            .Where(value => value != null && !value.isDestroy
                && value.SupportsWork(semanticWork))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (facility == null)
        {
            BuildingSO authored = LoadAuthoredBuilding(data =>
                data.Facility?.SupportsWork(semanticWork) == true);
            facility = PlaceAuthoredBuilding(authored, out failureReason);
        }
        if (facility == null) return null;

        OffenseHexTileState tile = offenseWorld.Tiles
            .Where(value => value != null && !value.blocked
                && offenseWorld.GetMinimumStepDistance(
                    offenseWorld.DungeonCoord, value.Coord) is >= 1 and <= 12)
            .OrderBy(value => value.q)
            .ThenBy(value => value.r)
            .FirstOrDefault();
        if (tile == null)
        {
            failureReason = "urgent incident/order authority rejected fixture: tile-missing";
            return null;
        }
        if (!offenseWorld.TrySpawnUrgentSite(
                definition.urgentSiteId, tile.Coord, out string siteId))
        {
            failureReason = "urgent incident authority rejected fixture spawn";
            return null;
        }
        if (!mitigation.TryStart(siteId, out string startMessage))
        {
            failureReason = "urgent mitigation authority rejected fixture start: "
                + startMessage;
            return null;
        }
        if (!mitigation.TryGetOrder(
                siteId,
                out OffenseUrgentMitigationOrderStateData order))
        {
            failureReason = "urgent mitigation authority did not publish order";
            return null;
        }
        if (!physicalItems.SpawnItemAt(
                definition.mitigationItemId,
                definition.mitigationItemAmount,
                facility.centerPos,
                WorldItemStackState.FacilityBuffer,
                order.destinationId,
                out int spawned)
            || spawned != definition.mitigationItemAmount)
        {
            failureReason = "urgent mitigation physical material delivery failed";
            return null;
        }

        return new MaterialWorkFixture(
            facility,
            $"threat:site={siteId};order={order.orderId};materials={spawned}",
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                bool destroyed = offenseWorld.TryDestroyUrgentSite(siteId);
                reason = destroyed ? string.Empty : "urgent incident already terminal";
                return destroyed;
            });
    }

    private BuildableObject PreparePlumbingTarget(out string failureReason)
    {
        failureReason = string.Empty;
        IFluidInfrastructureQuery query =
            runtimeScope.Container.Resolve<IFluidInfrastructureQuery>();
        IFluidWastewaterTransaction wastewater =
            runtimeScope.Container.Resolve<IFluidWastewaterTransaction>();
        BuildingSO tankDefinition = LoadAuthoredBuilding(data =>
            string.Equals(
                data.GetAbility<BuildingFacilityPartAbility>()?.code,
                "I09",
                StringComparison.Ordinal)
            && data.GetAbility<BuildingWaterStorageAbility>() is
            {
                wastewaterCapacity: > 0f
            } storage
            && (storage.channels & UtilityChannel.Wastewater) != 0);
        BuildingSO ductDefinition = LoadAuthoredBuilding(data =>
            string.Equals(
                data.GetAbility<BuildingFacilityPartAbility>()?.code,
                "U04",
                StringComparison.Ordinal)
            && data.Facility?.SupportsWork(BuiltInWorkTypeIds.Plumbing) == true
            && (data.GetAbility<BuildingUtilityConnectionAbility>()?.channels
                & UtilityChannel.Wastewater) != 0);
        BuildableObject tank = PlaceAuthoredBuilding(
            tankDefinition,
            out string tankFailure);
        if (tank == null || ductDefinition == null)
        {
            failureReason = "authored I09/U04 wastewater fixture unavailable: tank="
                + tankFailure + "; duct=" + (ductDefinition != null);
            return null;
        }

        BuildableObject target = null;
        foreach (Vector2Int position in tank.BuildingData.GetGridPosList(tank.centerPos))
        {
            BuildableObject duct = PlaceAuthoredBuildingAt(
                ductDefinition,
                position,
                out string ductFailure);
            if (duct == null)
            {
                failureReason = "U04 wastewater duct placement failed at "
                    + position + ": " + ductFailure;
                return null;
            }
            rowScopedFixtureBuildings.Add(duct);
            target ??= duct;
        }
        if (target is not IWorkableFacility
            || target.BuildingData?.Facility?.SupportsWork(
                BuiltInWorkTypeIds.Plumbing) != true)
        {
            failureReason = "authored U04 is not published as a plumbing "
                + "IWorkableFacility; rebuild Industrial Infrastructure assets";
            return null;
        }
        if (!query.TryGetMaintenance(target, out _, out _))
        {
            failureReason = "I09/U04 network did not publish maintenance authority";
            return null;
        }

        bool fullyAccepted = wastewater.TryAddWastewater(
            target,
            100000f,
            out float accepted,
            out DomainFailure domainFailure);
        if (accepted <= 0f)
        {
            failureReason = "wastewater transaction could not fill network: "
                + domainFailure;
            return null;
        }
        results.Add(WorkTypeLiveRow.Info(
            "plumbing-fixture",
            "tank=" + tank.RequirePersistentInstanceId().Value
            + "; target=" + target.RequirePersistentInstanceId().Value
            + "; accepted=" + accepted.ToString("0.###")
            + "; fullyAccepted=" + fullyAccepted
            + "; overflow=" + domainFailure));
        return target;
    }

    private BuildingSO LoadAuthoredBuilding(Func<BuildingSO, bool> predicate) =>
        AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value != null)
            .OrderBy(value => value.id)
            .FirstOrDefault(predicate);

    private BuildableObject PlaceAuthoredBuilding(
        BuildingSO data,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (data == null)
        {
            failureReason = "authored building asset missing";
            return null;
        }
        GridPathSearchResult subjectSearch = actor != null
            ? grid.SearchPath(actor.GetNowXY())
            : null;
        Vector2Int[] anchorCandidates = grid.GetCells()
            .Where(cell => cell != null)
            .Select(cell => cell.Position)
            .OrderBy(position => position.y)
            .ThenBy(position => position.x)
            .Where(position => data.GetGridPosList(position)
                .All(cell => grid.GetGridCell(cell) is GridCell gridCell
                    && gridCell.CanBuildInArea(data)
                    && gridCell.CanOccupy(data.Placement.Layer)))
            .Where(position => HasReachableAuthoredAccess(
                data,
                data.GetGridPosList(position),
                subjectSearch))
            .ToArray();
        if (anchorCandidates.Length == 0)
        {
            failureReason = "no reachable authored placement for " + data.name;
            return null;
        }

        GridBuildingFactory factory = new(created =>
            runtimeScope.Container.InjectGameObject(created.gameObject));
        for (int anchorIndex = 0; anchorIndex < anchorCandidates.Length; anchorIndex++)
        {
            Vector2Int anchor = anchorCandidates[anchorIndex];
            IReadOnlyList<Vector2Int> positions = data.GetGridPosList(anchor);
            if (positions.Count == 0
                || positions.Any(cell =>
                    grid.GetGridCell(cell) is not GridCell gridCell
                    || !gridCell.CanBuildInArea(data)
                    || !gridCell.CanOccupy(data.Placement.Layer)))
            {
                continue;
            }

            BuildableObject building = factory.Create(grid, data, anchor);
            if (building == null)
                continue;

            building.SetGrid(grid);
            building.Initialization(data, anchor);
            if (!grid.RegisterOccupant(
                    building,
                    data.Placement.Layer,
                    positions,
                    data.Placement.IsMovement))
            {
                Destroy(building.gameObject);
                continue;
            }

            // Work-only facilities intentionally have no visitor role. Prove
            // post-placement access through the production work stand
            // authority, never through CanQueueVisit/visitor admission.
            GridPathSearchResult postPlacementSearch = actor != null
                ? grid.SearchPath(actor.GetNowXY())
                : null;
            if (actor != null
                && !WorkTargetSelectionRules.IsReachable(
                    building,
                    postPlacementSearch))
            {
                building.DestroySelf();
                continue;
            }

            if (data.Facility?.roles == FacilityRole.None
                && postPlacementSearch != null
                && WorkTargetSelectionRules.TryGetReachableWorkAccessPosition(
                    building,
                    postPlacementSearch,
                    out Vector2Int workAccess))
            {
                results.Add(new WorkTypeLiveRow(
                    "info:work-only-access:" + data.name,
                    "INFO",
                    building.name,
                    "roles=None; workAccess=" + workAccess
                    + "; visitorAccess="
                    + postPlacementSearch.ContainsVisitableOccupant(building)
                    + "; live execution remains owned by its WorkType row"));
            }

            worldRegistry.RegisterBuilding(building);
            rowScopedFixtureBuildings.Add(building);
            return building;
        }

        failureReason = "no post-placement work access for " + data.name;
        return null;
    }

    private BuildableObject PlaceAuthoredBuildingAt(
        BuildingSO data,
        Vector2Int anchor,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (data == null || grid == null)
        {
            failureReason = "authored building or grid missing";
            return null;
        }
        IReadOnlyList<Vector2Int> positions = data.GetGridPosList(anchor);
        if (positions.Count == 0 || positions.Any(position =>
                grid.GetGridCell(position) is not GridCell cell
                || !cell.CanBuildInArea(data)
                || !cell.CanOccupy(data.Placement.Layer)))
        {
            failureReason = "exact authored footprint is unavailable";
            return null;
        }

        GridBuildingFactory factory = new(created =>
            runtimeScope.Container.InjectGameObject(created.gameObject));
        BuildableObject building = factory.Create(grid, data, anchor);
        if (building == null)
        {
            failureReason = "grid building factory returned null";
            return null;
        }
        building.SetGrid(grid);
        building.Initialization(data, anchor);
        if (!grid.RegisterOccupant(
                building,
                data.Placement.Layer,
                positions,
                data.Placement.IsMovement))
        {
            Destroy(building.gameObject);
            failureReason = "grid rejected exact authored occupant";
            return null;
        }
        worldRegistry.RegisterBuilding(building);
        return building;
    }

    private bool HasReachableAuthoredAccess(
        BuildingSO data,
        IReadOnlyList<Vector2Int> positions,
        GridPathSearchResult subjectSearch)
    {
        if (subjectSearch == null)
            return true;
        if (data == null || positions == null || positions.Count == 0)
            return false;
        if (data.IsGridMovement)
            return positions.Any(subjectSearch.ContainsPosition);

        for (int index = 0; index < positions.Count; index++)
        {
            Vector2Int occupied = positions[index];
            Vector2Int left = occupied + Vector2Int.left;
            Vector2Int right = occupied + Vector2Int.right;
            if (grid.IsValidGridPos(left)
                && grid.IsWalkable(left)
                && subjectSearch.ContainsPosition(left)
                || grid.IsValidGridPos(right)
                && grid.IsWalkable(right)
                && subjectSearch.ContainsPosition(right))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsReachableFromSubject(BuildableObject target)
    {
        if (actor == null || grid == null || target == null || target.isDestroy)
            return false;

        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        return WorkTargetSelectionRules.IsReachable(target, search);
    }

    private bool TryPrepareMaterialFixture(
        WorkTypeId workTypeId,
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        if (workTypeId == BuiltInWorkTypeIds.Craft
            || workTypeId == BuiltInWorkTypeIds.Cook
            || workTypeId == BuiltInWorkTypeIds.Quarry)
        {
            return TryPrepareProductionFixture(workTypeId, out fixture, out failureReason);
        }
        if (workTypeId == BuiltInWorkTypeIds.Restock)
            return TryPrepareRestockFixture(out fixture, out failureReason);
        if (workTypeId == BuiltInWorkTypeIds.Butcher)
            return TryPrepareButcherFixture(out fixture, out failureReason);
        if (workTypeId == BuiltInWorkTypeIds.Refuel)
            return TryPrepareRefuelFixture(out fixture, out failureReason);
        if (workTypeId == BuiltInWorkTypeIds.Gather
            || workTypeId == BuiltInWorkTypeIds.Logging)
        {
            return TryPrepareWorldResourceFixture(
                workTypeId,
                out fixture,
                out failureReason);
        }
        if (workTypeId == BuiltInWorkTypeIds.Sow
            || workTypeId == BuiltInWorkTypeIds.Harvest)
        {
            return TryPrepareCropPlotFixture(
                workTypeId,
                out fixture,
                out failureReason);
        }
        if (workTypeId == BuiltInWorkTypeIds.Dismantle)
            return TryPrepareDismantleFixture(out fixture, out failureReason);
        return true;
    }

    private bool TryPrepareProductionFixture(
        WorkTypeId workTypeId,
        out MaterialWorkFixture fixture,
        out string failureReason,
        BuildableObject requiredTarget = null)
    {
        fixture = null;
        failureReason = string.Empty;
        string requiredRecipeId;
        string requiredFacilityCode;
        string requiredResearchId;
        if (workTypeId == BuiltInWorkTypeIds.Craft)
        {
            requiredRecipeId = "recipe:stone-block";
            requiredFacilityCode = "P05";
            requiredResearchId = "research:mining:stonecutting";
        }
        else if (workTypeId == BuiltInWorkTypeIds.Cook)
        {
            requiredRecipeId = "recipe:tallow";
            requiredFacilityCode = "P15";
            requiredResearchId = "research:cuisine:livestock";
        }
        else if (workTypeId == BuiltInWorkTypeIds.Quarry)
        {
            requiredRecipeId = "source:quarry";
            requiredFacilityCode = "P22";
            requiredResearchId = "research:mining:quarry";
        }
        else
        {
            failureReason = "no authored production fixture contract for "
                + workTypeId.Value;
            return false;
        }

        ProductionRecipeSO[] recipes = economyContent.Recipes
            .Where(value => value != null
                && value.WorkTypeId == workTypeId
                && value.ProcessKind == ProductionProcessKind.WorkOnly
                && string.Equals(
                    value.RecipeId,
                    requiredRecipeId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.RequiredResearchId,
                    requiredResearchId,
                    StringComparison.Ordinal)
                && value.RequiredSupportTags.Count == 0
                && (workTypeId == BuiltInWorkTypeIds.Quarry
                    || value.Inputs.Count > 0))
            .OrderByDescending(value => value.RequiredWork)
            .ThenBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        if (recipes.Length == 0)
        {
            failureReason = "authored production recipe missing or mismatched: "
                + requiredRecipeId + "@" + requiredResearchId;
            return false;
        }

        ProductionRecipeSO recipe = null;
        BuildableObject target = null;
        List<string> placementFailures = new List<string>();
        foreach (ProductionRecipeSO candidate in recipes)
        {
            target = requiredTarget != null
                && !requiredTarget.isDestroy
                && requiredTarget.gameObject.activeInHierarchy
                && string.Equals(
                    requiredTarget.BuildingData?.GetAbility<
                        BuildingFacilityPartAbility>()?.code,
                    requiredFacilityCode,
                    StringComparison.Ordinal)
                && IsReachableFromSubject(requiredTarget)
                && requiredTarget.MatchesProductionWorkstation(candidate)
                    ? requiredTarget
                    : requiredTarget == null
                        ? worldRegistry.Buildings
                            .Where(building => building != null
                                && !building.isDestroy
                                && building.gameObject.activeInHierarchy
                                && string.Equals(
                                    building.BuildingData?.GetAbility<
                                        BuildingFacilityPartAbility>()?.code,
                                    requiredFacilityCode,
                                    StringComparison.Ordinal)
                                && IsReachableFromSubject(building)
                                && building.MatchesProductionWorkstation(candidate))
                            .OrderBy(
                                building => building.PersistentInstanceId.Value,
                                StringComparer.Ordinal)
                            .FirstOrDefault()
                        : null;
            if (target == null)
            {
                if (requiredTarget != null)
                {
                    string actualCode = requiredTarget.BuildingData?.GetAbility<
                        BuildingFacilityPartAbility>()?.code ?? "missing";
                    bool active = !requiredTarget.isDestroy
                        && requiredTarget.gameObject.activeInHierarchy;
                    bool reachable = IsReachableFromSubject(requiredTarget);
                    bool matches = requiredTarget.MatchesProductionWorkstation(
                        candidate);
                    placementFailures.Add(
                        candidate.RecipeId + "=required target mismatch: active="
                        + active + "; code=" + actualCode + "; expectedCode="
                        + requiredFacilityCode + "; reachable=" + reachable
                        + "; matches=" + matches + "; actor="
                        + actor.GetNowXY() + "; target=" + requiredTarget.centerPos);
                    continue;
                }
                BuildingSO authored = LoadAuthoredBuilding(data =>
                    data.Facility?.SupportsWork(workTypeId) == true
                    && string.Equals(
                        data.GetAbility<BuildingFacilityPartAbility>()?.code,
                        requiredFacilityCode,
                        StringComparison.Ordinal)
                    && string.Equals(
                        data.GetProductionWorkstationAbility()?.WorkstationTag,
                        candidate.WorkstationTag,
                        StringComparison.Ordinal));
                target = PlaceAuthoredBuilding(authored, out string placementFailure);
                if (target == null)
                {
                    placementFailures.Add(candidate.RecipeId + "=" + placementFailure);
                    continue;
                }
            }
            recipe = candidate;
            break;
        }
        if (recipe == null || target == null)
        {
            failureReason = "no placeable authored matching workstation: "
                + string.Join(" | ", placementFailures);
            return false;
        }

        foreach (ProductionBillSnapshot existing in productionBills.GetBills(target).ToArray())
        {
            ProductionBillCommandResult removed = productionOrders.RemoveBill(
                existing.BillId,
                returnMaterials: true);
            if (!removed.Succeeded)
            {
                failureReason = "could not clear pre-existing bill:" + removed.Failure;
                return false;
            }
        }

        ProductionBillCommandResult added = productionOrders.AddBill(
            target,
            recipe.RecipeId,
            ProductionOrderMode.RepeatCount,
            2);
        if (!added.Succeeded)
        {
            failureReason = "AddBill rejected:" + added.Failure;
            return false;
        }
        ProductionBillSnapshot bill = productionBills.GetBills(target)
            .FirstOrDefault(value => value.BillId == added.BillId);
        if (bill == null
            || (bill.Inputs.Count > 0
                && string.IsNullOrWhiteSpace(bill.MaterialDestinationId)))
        {
            failureReason = "added bill did not publish required material authority";
            return false;
        }

        List<string> materialStackIds = new List<string>();
        foreach (ItemAmountDefinition input in bill.Inputs)
        {
            HashSet<string> before = physicalItems.GetAllStacks()
                .Where(value => value != null)
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            int amount = Mathf.Max(1, input.Amount) * 2;
            if (!physicalItems.SpawnItemAt(
                    input.ItemId,
                    amount,
                    target.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    bill.MaterialDestinationId,
                    out int spawned)
                || spawned != amount)
            {
                failureReason = "physical recipe input spawn failed:" + input.ItemId;
                return false;
            }
            materialStackIds.AddRange(physicalItems.GetAllStacks()
                .Where(value => value != null
                    && !before.Contains(value.StackId)
                    && string.Equals(value.ItemId, input.ItemId, StringComparison.Ordinal)
                    && string.Equals(
                        value.DestinationId,
                        bill.MaterialDestinationId,
                        StringComparison.Ordinal))
                .Select(value => value.StackId));
        }

        BuildingProcessFluidAbility processFluid =
            target.BuildingData?.GetAbility<BuildingProcessFluidAbility>();
        if (processFluid != null
            && processFluid.Supports(workTypeId)
            && processFluid.cleanWaterPerCycle > 0f)
        {
            string processWaterDestination =
                "plumbing:process-water:"
                + target.RequirePersistentInstanceId().Value
                + ":" + workTypeId.Value;
            HashSet<string> before = physicalItems.GetAllStacks()
                .Where(value => value != null)
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            int containers = Mathf.Max(
                1,
                Mathf.CeilToInt(processFluid.cleanWaterPerCycle)) * 2;
            if (!physicalItems.SpawnItemAt(
                    "resource:clean-water",
                    containers,
                    target.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    processWaterDestination,
                    out int spawnedWater)
                || spawnedWater != containers)
            {
                failureReason = "physical process-water spawn failed:"
                    + processWaterDestination;
                return false;
            }
            string[] waterStackIds = physicalItems.GetAllStacks()
                .Where(value => value != null
                    && !before.Contains(value.StackId)
                    && string.Equals(
                        value.ItemId,
                        "resource:clean-water",
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.DestinationId,
                        processWaterDestination,
                        StringComparison.Ordinal))
                .Select(value => value.StackId)
                .ToArray();
            materialStackIds.AddRange(waterStackIds);
        }

        foreach (string stackId in materialStackIds)
        {
            if (!rowScopedItemStackIds.Contains(stackId))
                rowScopedItemStackIds.Add(stackId);
        }

        IProcessFluidUseRuntime processFluids =
            runtimeScope.Container.Resolve<IProcessFluidUseRuntime>();
        if (!processFluids.EnsureCycleSupply(
                target,
                workTypeId,
                out DomainFailure fluidFailure))
        {
            failureReason = "physical process-fluid supply not live-ready:"
                + fluidFailure;
            return false;
        }

        ProductionWorkAvailabilityResult availability =
            productionBills is IProductionBillWorkExecution workExecution
                ? workExecution.CheckWorkAvailability(target, workTypeId)
                : default;
        if (!availability.Available || availability.Bill?.BillId != bill.BillId)
        {
            failureReason = "physical bill not live-ready:"
                + (availability.Failure.IsFailure
                    ? availability.Failure.ToString()
                    : "wrong bill selected");
            return false;
        }

        fixture = new MaterialWorkFixture(
            target,
            workTypeId.Value + ":bill=" + bill.BillId.Value
                + "; recipe=" + recipe.RecipeId
                + "; facility=" + requiredFacilityCode
                + "; research=" + requiredResearchId
                + "; inputStacks=" + materialStackIds.Count,
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                ProductionBillCommandResult removed = productionOrders.RemoveBill(
                    bill.BillId,
                    returnMaterials: false);
                reason = removed.Succeeded ? string.Empty : removed.Failure.ToString();
                return removed.Succeeded;
            });
        return true;
    }

    private bool TryPrepareRestockFixture(
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        Shop[] liveShops = worldRegistry.Buildings
            .OfType<Shop>()
            .Where(value => value != null
                && !value.isDestroy
                && IsReachableFromSubject(value))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .ToArray();
        Shop shop = null;
        SaleItem saleItem = null;
        foreach (Shop candidate in liveShops)
        {
            saleItem = ResolvePhysicalSaleItem(candidate);
            if (saleItem == null)
                continue;
            shop = candidate;
            break;
        }
        if (shop == null)
        {
            BuildingSO[] authoredShops = AssetDatabase.FindAssets(
                    "t:BuildingSO",
                    new[] { "Assets/Resources/SO/Building" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
                .Where(data => data != null
                    && data.runtimeArchetype == BuildingRuntimeArchetypeKind.Shop)
                .OrderBy(data => data.id)
                .ToArray();
            List<string> authoredFailures = new List<string>();
            foreach (BuildingSO authoredShop in authoredShops)
            {
                Shop placed = PlaceAuthoredBuilding(
                    authoredShop,
                    out string placementFailure) as Shop;
                if (placed == null)
                {
                    authoredFailures.Add(authoredShop.name + "=" + placementFailure);
                    continue;
                }
                SaleItem physical = ResolvePhysicalSaleItem(placed);
                if (physical == null)
                {
                    authoredFailures.Add(authoredShop.name
                        + "=no physical sale definition");
                    placed.DestroySelf();
                    continue;
                }
                shop = placed;
                saleItem = physical;
                break;
            }
            if (shop == null)
            {
                failureReason = "no live/placeable shop exposes a physical sale definition: "
                    + string.Join(" | ", authoredFailures);
                return false;
            }
        }
        shop.DebugClearStock();
        if (saleItem == null)
        {
            failureReason = "shop sale item lacks a physical item definition";
            return false;
        }
        IWarehouseFacility warehouse = worldRegistry.Warehouses
            .Where(value => value?.Inventory != null
                && value.HasWarehouseInventory
                && !ReferenceEquals(value, shop)
                && value.Inventory.Accepts(saleItem.category)
                && value.Inventory.CanStoreItem(
                    saleItem.AuthoredItemDefinitionId, 2)
                && value is BuildableObject building
                && IsReachableFromSubject(building))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (warehouse == null)
            warehouse = PlaceAuthoredWarehouse(saleItem.category, 2, out failureReason);
        if (warehouse is not BuildableObject warehouseBuilding)
        {
            failureReason = "no compatible live warehouse for " + saleItem.category;
            return false;
        }

        string destination = WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + warehouse.PersistentInstanceId.Value;
        List<string> stackIds = new List<string>();
        bool Seed(out string reason)
        {
            // A cancelled first phase leaves its source stack physically live.
            // Rearm from one exact source set so the second phase cannot select
            // stale stock with an obsolete pickup stand.
            HashSet<string> liveIds = physicalItems.GetAllStacks()
                .Where(value => value != null)
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (string stackId in stackIds)
            {
                if (liveIds.Contains(stackId))
                    physicalItems.DeleteStack(stackId);
                rowScopedItemStackIds.Remove(stackId);
            }
            stackIds.Clear();
            HashSet<string> before = physicalItems.GetAllStacks()
                .Where(value => value != null)
                .Select(value => value.StackId)
                .ToHashSet(StringComparer.Ordinal);
            int amount = Mathf.Clamp(shop.MissingStock, 2, 12);
            bool spawned = physicalItems.SpawnItemAt(
                saleItem.ItemDefinitionId.Value,
                amount,
                warehouseBuilding.centerPos,
                WorldItemStackState.Stored,
                destination,
                out int created) && created == amount;
            stackIds.AddRange(physicalItems.GetAllStacks()
                .Where(value => value != null
                    && !before.Contains(value.StackId)
                    && string.Equals(
                        value.ItemId,
                        saleItem.ItemDefinitionId.Value,
                        StringComparison.Ordinal)
                    && string.Equals(value.DestinationId, destination, StringComparison.Ordinal))
                .Select(value => value.StackId));
            rowScopedItemStackIds.AddRange(stackIds);
            reason = spawned ? string.Empty : "exact physical shop stock spawn failed";
            return spawned;
        }
        if (!Seed(out failureReason))
            return false;

        fixture = new MaterialWorkFixture(
            shop,
            "restock:item=" + saleItem.ItemDefinitionId.Value
                + "; warehouse=" + warehouse.PersistentInstanceId.Value,
            prepareInvalidation: (out string reason) =>
            {
                shop.DebugClearStock();
                return Seed(out reason);
            },
            invalidate: (out string reason) =>
            {
                if (shop == null || shop.isDestroy)
                {
                    reason = "restock target already absent";
                    return false;
                }

                shop.DestroySelf();
                reason = string.Empty;
                return true;
            });
        return true;
    }

    private SaleItem ResolvePhysicalSaleItem(Shop shop)
    {
        if (shop == null)
            return null;
        return shop.ProductSnapshots
            .Select(product => shopStockCatalog.TryGetSaleItem(
                    product.Id,
                    out SaleItem item)
                ? item
                : null)
            .FirstOrDefault(item => item != null
                && item.ItemDefinitionId.IsValid
                && !PhysicalItemIds.TryGetEquipmentDefinitionId(
                    item.ItemDefinitionId.Value,
                    out _)
                && !PhysicalItemIds.IsEquipmentModule(
                    item.ItemDefinitionId.Value));
    }

    private bool TryPrepareButcherFixture(
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        BuildableObject target = worldRegistry.Buildings
            .Where(value => value != null
                && !value.isDestroy
                && value.BuildingData?.GetAbility<BuildingButcherAbility>() != null)
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target == null)
        {
            BuildingSO authored = LoadAuthoredBuilding(data =>
                data.GetAbility<BuildingButcherAbility>() != null);
            target = PlaceAuthoredBuilding(authored, out failureReason);
        }
        WildlifeActor source = wildlifeRuntime.Wildlife
            .Where(value => value != null && value.IsAlive && value.Species != null)
            .OrderBy(value => value.WildlifeId, StringComparer.Ordinal)
            .FirstOrDefault();
        string wildlifeFailure = string.Empty;
        if (source == null)
        {
            IWildlifeSpeciesCatalogProvider speciesCatalog =
                runtimeScope.Container.Resolve<IWildlifeSpeciesCatalogProvider>();
            string speciesId = speciesCatalog.All
                .Where(value => value != null)
                .Select(value => value.SpeciesId)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (string.IsNullOrWhiteSpace(speciesId))
            {
                wildlifeFailure = "authored wildlife species catalog is empty";
            }
            else if (!wildlifeRuntime.TrySpawnArrival(
                         speciesId,
                         actor.GetNowXY(),
                         out source,
                         out wildlifeFailure))
            {
                source = null;
            }
        }
        if (target == null || source == null)
        {
            failureReason = "butcher fixture missing: facility=" + (target != null)
                + "; wildlife=" + (source != null)
                + "; facilityReason=" + failureReason
                + "; wildlifeReason=" + wildlifeFailure;
            return false;
        }
        HashSet<string> before = physicalItems.GetAllStacks()
            .Where(value => value != null)
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);
        string sourceWildlifeId = source.WildlifeId;
        carcassService.SpawnCarcass(source);
        // The carcass is the production work input. Keeping the dead source
        // WildlifeActor on the grid adds no authority and polluted later
        // room fixtures with a hard Wildlife occupant. Remove it through the
        // public wildlife lifecycle command after the carcass was committed.
        if (!wildlifeRuntime.TryRemoveArrival(sourceWildlifeId))
        {
            failureReason = "production wildlife lifecycle did not retire "
                + "butcher source " + sourceWildlifeId;
            return false;
        }
        WorldItemStackSnapshot carcass = physicalItems.GetAllStacks()
            .FirstOrDefault(value => value != null
                && !before.Contains(value.StackId)
                && WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                    value.ItemId,
                    out _));
        if (carcass == null || !carcassService.HasButcherWorkAvailable(target))
        {
            failureReason = "production carcass service did not publish physical butcher work";
            return false;
        }
        fixture = new MaterialWorkFixture(
            target,
            "butcher:carcass=" + carcass.StackId,
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                bool removed = WorldItemRepositoryEditorAccess.TryRemoveStack(
                    itemRepository,
                    carcass.StackId);
                reason = removed ? string.Empty : "carcass already absent";
                return removed;
            });
        return true;
    }

    private bool TryPrepareRefuelFixture(
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        BuildableObject target = worldRegistry.Buildings
            .Where(value => value != null
                && !value.isDestroy
                && value.BuildingData?.GetAbility<BuildingFuelConsumerAbility>() != null
                && value.BuildingData?.GetAbility<BuildingGolemRechargeAbility>() == null
                && IsReachableFromSubject(value))
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target == null)
        {
            BuildingSO authored = LoadAuthoredBuilding(data =>
                data.GetAbility<BuildingFuelConsumerAbility>() != null
                && data.GetAbility<BuildingGolemRechargeAbility>() == null);
            target = PlaceAuthoredBuilding(authored, out failureReason);
        }
        IWarehouseFacility warehouse = worldRegistry.Warehouses
            .Where(value => value?.Inventory != null
                && value.HasWarehouseInventory
                && value.Inventory.Accepts(StockCategory.Fuel)
                && value.Inventory.RemainingMassGrams > 0L)
            .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (warehouse == null)
            warehouse = PlaceAuthoredWarehouse(StockCategory.Fuel, 4, out failureReason);
        if (target == null || warehouse == null)
        {
            failureReason = "fuel consumer or compatible physical warehouse missing";
            return false;
        }
        HashSet<string> before = physicalItems.GetAllStacks()
            .Where(value => value != null)
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);
        if (!physicalItems.SpawnStockInWarehouse(
                warehouse,
                StockCategory.Fuel,
                4,
                out int spawned)
            || spawned != 4)
        {
            failureReason = "physical fuel spawn failed";
            return false;
        }
        List<string> fuelStackIds = physicalItems.GetAllStacks()
            .Where(value => value != null
                && !before.Contains(value.StackId)
                && value.StockCategory == StockCategory.Fuel)
            .Select(value => value.StackId)
            .ToList();
        fixture = new MaterialWorkFixture(
            target,
            "refuel:physicalStacks=" + fuelStackIds.Count,
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                int removed = 0;
                foreach (WorldItemStackSnapshot stack in physicalItems.GetAllStacks()
                             .Where(value => value != null
                                 && value.StockCategory == StockCategory.Fuel)
                             .ToArray())
                {
                    if (WorldItemRepositoryEditorAccess.TryRemoveStack(
                            itemRepository,
                            stack.StackId))
                        removed++;
                }
                reason = removed > 0 ? string.Empty : "fuel stack already absent";
                return removed > 0;
            });
        return true;
    }

    private bool TryPrepareWorldResourceFixture(
        WorkTypeId workTypeId,
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        if (worldResources is not WorldResourceRuntime concrete)
        {
            failureReason = "official world-resource runtime is not available";
            return false;
        }
        IWorldResourcePersistence persistence =
            runtimeScope.Container.Resolve<IWorldResourcePersistence>();
        concrete.Tick();
        DungeonWorldResourceSaveData rowBaseline = persistence.Capture();

        WorldResourceNode node = worldResources.Nodes
            .Where(value => value != null)
            .OrderBy(value => value.NodeId, StringComparer.Ordinal)
            .FirstOrDefault(value => worldResources.TryGetWork(
                value,
                workTypeId,
                out WorldResourceWorkSnapshot workSnapshot)
                && workSnapshot.Available
                && IsReachableFromSubject(value.GetComponent<BuildableObject>()));
        if (node == null)
        {
            string[] observed = worldResources.Nodes
                .Where(value => value != null)
                .Select(value => worldResources.TryGetWork(
                        value,
                        workTypeId,
                        out WorldResourceWorkSnapshot snapshot)
                    ? value.NodeId + ":" + snapshot.UnavailableReason
                    : value.NodeId + ":no-" + workTypeId.Value + "-source")
                .ToArray();
            failureReason = "official world-resource topology has no reachable available "
                + workTypeId.Value + " source; nodes=" + worldResources.NodeCount
                + "; observed=" + string.Join(" | ", observed);
            return false;
        }
        BuildableObject target = node.GetComponent<BuildableObject>();
        if (target == null)
        {
            failureReason = "world-resource node host has no production BuildableObject";
            return false;
        }
        if (!worldResources.TryGetWork(
                node,
                workTypeId,
                out WorldResourceWorkSnapshot ready)
            || !ready.Available)
        {
            failureReason = "world-resource readiness changed during fixture selection";
            return false;
        }

        fixture = new MaterialWorkFixture(
            target,
            "world-resource:node=" + ready.NodeId
                + ";recipe=" + ready.RecipeId
                + ";remaining=" + (ready.RequiredWork - ready.CompletedWork).ToString("0.###"),
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                if (target == null || target.isDestroy)
                {
                    reason = "world-resource host already destroyed";
                    return false;
                }
                target.DestroySelf();
                // Destroying the live host is the fault under test. Re-publish
                // the exact authoritative resource aggregate immediately via
                // its production persistence boundary so this row cannot
                // remove a shared grass/tree/rock host needed by later rows.
                // The running action still owns the destroyed target instance
                // and must reach its typed terminal independently.
                persistence.Restore(persistence.BuildRestore(rowBaseline));
                concrete.Tick();
                bool republished = worldResources.Nodes.Any(value =>
                    value != null
                    && worldResources.TryGetWork(
                        value,
                        workTypeId,
                        out WorldResourceWorkSnapshot restored)
                    && restored.Available);
                if (!republished)
                {
                    reason = "world-resource authoritative row baseline did not republish";
                    return false;
                }
                reason = string.Empty;
                return true;
            });
        return true;
    }

    private bool TryPrepareCropPlotFixture(
        WorkTypeId workTypeId,
        out MaterialWorkFixture fixture,
        out string failureReason)
    {
        fixture = null;
        failureReason = string.Empty;
        BuildingSO plotDefinition = LoadAuthoredBuilding(data =>
            data.GetAbility<BuildingCropPlotAbility>() is { Indoor: false }
            && data.Facility?.SupportsWork(BuiltInWorkTypeIds.Sow) == true
            && data.Facility.SupportsWork(BuiltInWorkTypeIds.Harvest));
        BuildableObject plot = PlaceAuthoredBuilding(
            plotDefinition,
            out failureReason);
        if (plot == null)
            return false;

        cropPlots.Tick();
        CropDefinitionSO crop = economyContent.Crops
            .Where(value => value != null
                && string.Equals(
                    value.RequiredResearchId,
                    "research:agriculture:field",
                    StringComparison.Ordinal))
            .OrderBy(value => value.CropId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (crop == null)
        {
            failureReason = "no authored field-research crop for P23";
            return false;
        }
        if (!cropPlots.TrySetCrop(plot, crop.CropId, out string setMessage))
        {
            failureReason = "crop assignment authority rejected "
                + crop.CropId + ": " + setMessage;
            return false;
        }
        cropPlots.Tick();
        CropPlotSnapshot waiting = cropPlots.Plots.FirstOrDefault(value =>
            string.Equals(
                value.PlotId,
                plot.RequirePersistentInstanceId().Value,
                StringComparison.Ordinal));
        if (waiting == null || waiting.RequiredMaterials.Count == 0)
        {
            failureReason = "P23 did not publish versioned physical sowing materials";
            return false;
        }
        foreach (KeyValuePair<string, int> material in waiting.RequiredMaterials
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            IItemTransferService transfers =
                runtimeScope.Container.Resolve<IItemTransferService>();
            bool spawnedSuccessfully = string.Equals(
                    material.Key,
                    crop.SeedItemId,
                    StringComparison.Ordinal)
                ? crop.BaseGenome != null
                  && transfers.TrySpawnItemWithComponents(
                      material.Key,
                      material.Value,
                      plot.centerPos,
                      WorldItemStackState.FacilityBuffer,
                      waiting.MaterialDestinationId,
                      new[]
                      {
                          SeedLotItemStateCodec.Encode(new SeedLotState
                          {
                              cropId = crop.CropId,
                              cultivarGenomeId = crop.BaseGenome.GenomeId,
                              generation = 0,
                              pathogenLoad = 0f
                          })
                      },
                      out int spawnedSeed)
                  && spawnedSeed == material.Value
                : physicalItems.SpawnItemAt(
                    material.Key,
                    material.Value,
                    plot.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    waiting.MaterialDestinationId,
                    out int spawnedMaterial)
                  && spawnedMaterial == material.Value;
            if (!spawnedSuccessfully)
            {
                failureReason = "P23 physical sowing material spawn failed: "
                    + material.Key
                    + (string.Equals(material.Key, crop.SeedItemId, StringComparison.Ordinal)
                        && crop.BaseGenome == null
                        ? "; crop base genome missing"
                        : string.Empty);
                return false;
            }
        }
        cropPlots.Tick();

        if (workTypeId == BuiltInWorkTypeIds.Harvest)
        {
            DungeonCropPlotSaveData harvestState = cropPlots.Capture();
            CropPlotSaveData saved = harvestState.plots.FirstOrDefault(value =>
                string.Equals(
                    value.buildingInstanceId,
                    waiting.PlotId,
                    StringComparison.Ordinal));
            if (saved == null)
            {
                failureReason = "P23 versioned state is missing after material delivery";
                return false;
            }
            saved.phase = CropPlotPhase.ReadyToHarvest;
            saved.sowWork = crop.SowWork;
            saved.growthHours = crop.GrowthHours;
            saved.harvestWork = 0f;
            saved.materialsConsumed = true;
            cropPlots.Restore(cropPlots.BuildRestore(harvestState));
            cropPlots.Tick();
        }

        if (!cropPlots.TryGetWork(
                plot,
                workTypeId,
                out CropPlotWorkSnapshot ready)
            || !ready.Available)
        {
            failureReason = "P23 did not publish " + workTypeId.Value
                + " after authoritative setup: " + ready.UnavailableReason;
            return false;
        }
        fixture = new MaterialWorkFixture(
            plot,
            "crop-plot:plot=" + ready.PlotId
                + ";crop=" + crop.CropId
                + ";phase=" + cropPlots.Plots.First(value =>
                    string.Equals(value.PlotId, ready.PlotId, StringComparison.Ordinal)).Phase
                + ";materials=" + string.Join(",", waiting.RequiredMaterials
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => value.Key + "x" + value.Value)),
            prepareInvalidation: null,
            invalidate: (out string reason) =>
            {
                if (plot == null || plot.isDestroy)
                {
                    reason = "crop plot already destroyed";
                    return false;
                }
                plot.DestroySelf();
                reason = string.Empty;
                return true;
            });
        return true;
    }

    private bool TryPrepareDismantleFixture(
        out MaterialWorkFixture fixture,
        out string failureReason,
        bool requireFaultObservationWindow = true)
    {
        fixture = null;
        failureReason = string.Empty;
        IQualityTargetPipelineCommand quality =
            runtimeScope.Container.Resolve<IQualityTargetPipelineCommand>();
        IQualityTargetPipelineQuery qualityQuery =
            runtimeScope.Container.Resolve<IQualityTargetPipelineQuery>();
        BuildingSO[] authored = AssetDatabase.FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value != null
                && value.runtimeArchetype
                    == BuildingRuntimeArchetypeKind.Facility
                && value.Facility != null
                && !value.IsStructuralWall
                && !value.IsInteriorDoor
                && value.GetConstructionMaterials().Count > 0)
            // This row must observe accepted dismantle WU before requesting the
            // cancellation terminal.  Tiny props (the 32-WU candle produces an
            // 8-WU dismantle order) can cross the completion boundary inside
            // the first accelerated verifier tick, which tests rebuild/output
            // completion instead of the intended mid-work lifecycle fault.
            // Fault rows prefer substantial authored facilities. The separate
            // normal-completion row starts from the smallest authored facility;
            // its actor is reset after fixture materialization so construction
            // and quality setup cannot contaminate the live AI phase.
            .OrderBy(value => (requireFaultObservationWindow ? -1f : 1f)
                * value.GetRequiredWork(BuiltInWorkTypeIds.Construct))
            .ThenBy(value => (requireFaultObservationWindow ? -1 : 1)
                * value.GetConstructionMaterials().Sum(material => material.Amount))
            .ThenBy(value => value.id)
            .ToArray();
        Dictionary<int, BuildingSO> byId = authored
            .GroupBy(value => value.id)
            .ToDictionary(value => value.Key, value => value.First());
        void Configure(BuildableObject created)
        {
            if (created == null)
                return;
            runtimeScope.Container.InjectGameObject(created.gameObject);
            worldRegistry.RegisterBuilding(created);
        }
        GridBuildingFactory factory = new(Configure);
        BuildingPlacementValidator placementValidator =
            new BuildingPlacementValidator();
        GridBuildingPlacementService placement = new(
            grid,
            hallwayBuilding: null,
            findBuildingData: id => byId.TryGetValue(id, out BuildingSO data)
                ? data
                : null,
            buildingFactory: factory,
            placementValidator: placementValidator,
            workOrderRuntime: workOrders,
            onConstructionSiteCreated: Configure);
        GridPathSearchResult subjectSearch = grid.SearchPath(actor.GetNowXY());

        List<string> attempts = new();
        foreach (BuildingSO definition in authored)
        {
            Vector2Int[] anchors = grid.GetCells()
                .Where(value => value != null)
                .Select(value => value.Position)
                .Where(position => definition.GetGridPosList(position).All(cell =>
                    grid.GetGridCell(cell) is GridCell gridCell
                    && gridCell.CanBuildInArea(definition)
                    && gridCell.CanOccupy(definition.Placement.Layer)
                    && gridCell.CanOccupy(GridLayer.Construction)))
                .Where(position => HasReachableAuthoredAccess(
                    definition,
                    definition.GetGridPosList(position),
                    subjectSearch))
                .OrderBy(position => position.y)
                .ThenBy(position => position.x)
                .Take(3)
                .ToArray();
            foreach (Vector2Int anchor in anchors)
            {
                if (!placement.TryPlaceConstructionSite(
                        definition,
                        anchor,
                        out string placementFailure))
                {
                    attempts.Add(definition.name + "@" + anchor + "=" + placementFailure);
                    continue;
                }
                ConstructionSite site = worldRegistry.Buildings
                    .OfType<ConstructionSite>()
                    .FirstOrDefault(value => value != null
                        && !value.isDestroy
                        && value.centerPos == anchor
                        && value.id == definition.id);
                if (site == null
                    || !workOrders.TryGetOrderFor(
                        site,
                        BuiltInWorkTypeIds.Construct,
                        out WorkOrderProgressState construction))
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=construction order was not published");
                    continue;
                }

                QualityTargetPipelineSaveData request = new()
                {
                    definitionId = definition.ContentDefinitionId,
                    minimumQuality = CraftsmanshipQualityTier.Good,
                    requiredAcceptedCount = 1,
                    rejectedDisposition =
                        RejectedOutputDisposition.DismantleFacilityAndRetry,
                    limitMode = QualityRepeatLimitMode.SafeLimits,
                    maximumAttempts = 2,
                    workBudget = Mathf.Max(1f, construction.RequiredWork * 3f),
                    workerPolicy = WorkerSelectionPolicySaveData.Anyone(
                        WorkerCandidateSortMode.BestExpectedQuality)
                };
                if (!quality.CreateForWorkOrder(
                        construction.WorkOrderId,
                        request,
                        out string pipelineId,
                        out DomainFailure qualityFailure))
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=quality pipeline rejected:" + qualityFailure);
                    workOrders.CancelOrder(
                        construction.WorkOrderId,
                        refundDeliveredMaterials: true);
                    continue;
                }
                if (!workOrders.TryGetOrderFor(
                        site,
                        BuiltInWorkTypeIds.Construct,
                        out construction)
                    || construction.Status
                        == WorkOrderStatus.TargetCurrentlyUnreachable)
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=good quality target is unexpectedly unreachable");
                    quality.CancelQualityPipeline(pipelineId, out _);
                    workOrders.CancelOrder(
                        construction?.WorkOrderId ?? string.Empty,
                        refundDeliveredMaterials: true);
                    continue;
                }

                foreach (KeyValuePair<string, int> material in
                         construction.ItemMaterialRequirements)
                {
                    if (!physicalItems.SpawnItemAt(
                            material.Key,
                            material.Value,
                            anchor,
                            WorldItemStackState.FacilityBuffer,
                            construction.MaterialDestinationId,
                            out int spawned)
                        || spawned != material.Value)
                    {
                        failureReason = "dismantle prerequisite construction material failed: "
                            + material.Key;
                        return false;
                    }
                }
                string completionMessage = string.Empty;
                if (!workOrders.RefreshMaterialsReady(site)
                    || !workOrders.ApplyWork(
                        actor,
                        site,
                        BuiltInWorkTypeIds.Construct,
                        construction.RequiredWork,
                        out bool completed,
                        out _,
                        out completionMessage)
                    || !completed)
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=production construction completion rejected:"
                        + completionMessage);
                    continue;
                }

                BuildableObject completedBuilding = worldRegistry.Buildings
                    .Where(value => value != null
                        && !value.isDestroy
                        && value is not ConstructionSite
                        && value.centerPos == anchor
                        && value.id == definition.id)
                    .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (completedBuilding == null
                    || !workOrders.TryGetOrderFor(
                        completedBuilding,
                        BuiltInWorkTypeIds.Dismantle,
                        out WorkOrderProgressState dismantle))
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=quality roll met target; no dismantle order");
                    if (completedBuilding != null && !completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                if (!qualityQuery.TryGetQualityPipeline(
                        pipelineId,
                        out QualityTargetPipelineSaveData rejectedPipeline)
                    || rejectedPipeline.stage
                        != QualityTargetPipelineStage.Dismantling
                    || (int)completedBuilding.Craftsmanship.Quality
                        >= (int)rejectedPipeline.minimumQuality)
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=dismantle order lacked an actual terminal quality rejection");
                    workOrders.CancelOrder(
                        dismantle.WorkOrderId,
                        refundDeliveredMaterials: true);
                    quality.CancelQualityPipeline(pipelineId, out _);
                    if (!completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                if (!placementValidator.CanDestroy(
                        grid,
                        definition,
                        completedBuilding,
                        out string destroyPreflightFailure))
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=production destroy preflight rejected:"
                        + destroyPreflightFailure);
                    workOrders.CancelOrder(
                        dismantle.WorkOrderId,
                        refundDeliveredMaterials: true);
                    quality.CancelQualityPipeline(pipelineId, out _);
                    if (!completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                // WorkTaskExecutor clamps the authored worker rate to 8 WU/s.
                // Require at least two game-seconds of dismantle work so the
                // accelerated PlayMode row cannot legitimately complete before
                // its first approved-work observation/cancellation boundary.
                const float minimumObservableDismantleWork = 16f;
                if (requireFaultObservationWindow
                    && dismantle.RequiredWork < minimumObservableDismantleWork)
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=dismantle order too short for mid-work fault:"
                        + dismantle.RequiredWork.ToString("0.###"));
                    quality.CancelQualityPipeline(pipelineId, out _);
                    if (!completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                if (!requireFaultObservationWindow
                    && dismantle.RequiredWork <= 0f)
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=dismantle order had no positive work");
                    workOrders.CancelOrder(
                        dismantle.WorkOrderId,
                        refundDeliveredMaterials: true);
                    quality.CancelQualityPipeline(pipelineId, out _);
                    if (!completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                GridPathSearchResult completedSearch =
                    grid.SearchPath(actor.GetNowXY());
                if (!WorkTargetSelectionRules.IsReachable(
                        completedBuilding,
                        completedSearch))
                {
                    attempts.Add(definition.name + "@" + anchor
                        + "=production dismantle target has no work access");
                    workOrders.CancelOrder(
                        dismantle.WorkOrderId,
                        refundDeliveredMaterials: true);
                    if (!completedBuilding.isDestroy)
                        completedBuilding.DestroySelf();
                    continue;
                }
                rowScopedFixtureBuildings.Add(completedBuilding);

                fixture = new MaterialWorkFixture(
                    completedBuilding,
                    "dismantle:order=" + dismantle.WorkOrderId
                        + ";pipeline=" + pipelineId
                        + ";actualQuality="
                        + completedBuilding.Craftsmanship.Quality
                        + ";minimumQuality="
                        + rejectedPipeline.minimumQuality
                        + ";destroyPreflight=passed"
                        + ";fixturePurpose="
                        + (requireFaultObservationWindow
                            ? "mid-work-fault"
                            : "normal-completion")
                        + ";requiredWork="
                        + dismantle.RequiredWork.ToString("0.###")
                        + ";building=" + definition.ContentDefinitionId,
                    prepareInvalidation: null,
                    invalidate: (out string reason) =>
                    {
                        bool cancelled = workOrders.CancelOrder(
                            dismantle.WorkOrderId,
                            refundDeliveredMaterials: true);
                        reason = cancelled
                            ? string.Empty
                            : "dismantle order already terminal";
                        return cancelled;
                    });
                return true;
            }
        }

        failureReason = "production quality pipeline could not publish a real "
            + "facility dismantle order: " + string.Join(" | ", attempts.Take(12));
        return false;
    }

    private IWarehouseFacility PlaceAuthoredWarehouse(
        StockCategory category,
        int minimumCapacity,
        out string failureReason)
    {
        BuildingSO authored = LoadAuthoredBuilding(data =>
            data.runtimeArchetype == BuildingRuntimeArchetypeKind.Facility
            && data.GetStorageCapacity() >= minimumCapacity
            && (data.StoresAllCategories()
                || data.GetStorageCategory() == category));
        BuildableObject building = PlaceAuthoredBuilding(authored, out failureReason);
        if (building is not IWarehouseFacility warehouse
            || !warehouse.HasWarehouseInventory
            || warehouse.Inventory == null
            || !warehouse.Inventory.Accepts(category)
            || warehouse.Inventory.MaxMassGrams <= 0L)
        {
            if (string.IsNullOrEmpty(failureReason))
                failureReason = "placed authored warehouse is incompatible with " + category;
            return null;
        }
        return warehouse;
    }

    private string DescribeFixtureAuthority(
        WorkTypeId workTypeId,
        MaterialWorkFixture fixture)
    {
        BuildableObject target = fixture?.Target;
        if (target == null)
            return "fixture-target-missing";
        if (workTypeId == BuiltInWorkTypeIds.Plumbing)
        {
            IFluidInfrastructureQuery query =
                runtimeScope.Container.Resolve<IFluidInfrastructureQuery>();
            return query.TryGetMaintenance(target, out float blockage, out float leak)
                ? "maintenance:blockage=" + blockage.ToString("0.###")
                    + ",leak=" + leak.ToString("0.###")
                : "maintenance-state-missing";
        }
        if (workTypeId == BuiltInWorkTypeIds.Dismantle)
        {
            return workOrders.TryGetOrderFor(
                    target,
                    BuiltInWorkTypeIds.Dismantle,
                    out WorkOrderProgressState order)
                ? "order=" + order.WorkOrderId + ",status=" + order.Status
                    + ",remaining="
                    + Mathf.Max(0f, order.RequiredWork - order.CompletedWork)
                        .ToString("0.###")
                : "dismantle-order-missing";
        }
        return fixture.Detail;
    }

    private static string GetMissingPrerequisite(WorkTypeId workTypeId)
    {
        if (workTypeId == BuiltInWorkTypeIds.Restock)
            return "warehouse with a physical restock deficit and available source stock";
        if (workTypeId == BuiltInWorkTypeIds.Repair)
            return "damaged repairable building with repair materials";
        if (workTypeId == BuiltInWorkTypeIds.Craft)
            return "active production bill, compatible facility and physical recipe inputs";
        if (workTypeId == BuiltInWorkTypeIds.Butcher)
            return "butchery bill, compatible facility and eligible carcass";
        if (workTypeId == BuiltInWorkTypeIds.Cook)
            return "active cooking bill, compatible facility and physical ingredients";
        if (workTypeId == BuiltInWorkTypeIds.Refuel)
            return "fuel-deficient facility and compatible physical fuel";
        if (workTypeId == BuiltInWorkTypeIds.Perform)
            return "active performance assignment and usable venue";
        if (workTypeId == BuiltInWorkTypeIds.Gather)
            return "authored harvestable resource with an active gather order";
        if (workTypeId == BuiltInWorkTypeIds.Sow)
            return "prepared plot, sow order and compatible seed";
        if (workTypeId == BuiltInWorkTypeIds.Harvest)
            return "mature crop with an active harvest order";
        if (workTypeId == BuiltInWorkTypeIds.Logging)
            return "eligible tree with an active logging order";
        if (workTypeId == BuiltInWorkTypeIds.Quarry)
            return "eligible quarry or vein with an active quarry order";
        if (workTypeId == BuiltInWorkTypeIds.AnimalCare)
            return "registered animal with a live husbandry care need";
        if (workTypeId == BuiltInWorkTypeIds.GrandProject)
            return "active grand-project stage with delivered BOM";
        if (workTypeId == BuiltInWorkTypeIds.ThreatMitigation)
            return "active mitigatable hazard or threat incident";
        if (workTypeId == BuiltInWorkTypeIds.Plumbing)
            return "active plumbing construction or repair demand";
        if (workTypeId == BuiltInWorkTypeIds.Dismantle)
            return "eligible building with an active dismantle order";
        return "authored production target and its domain prerequisites";
    }

    private bool CleanupRowScopedFixtures(out string failureReason)
    {
        failureReason = string.Empty;
        bool succeeded = true;
        HashSet<string> liveStackIds = physicalItems?.GetAllStacks()
            .Where(value => value != null)
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        for (int index = rowScopedItemStackIds.Count - 1; index >= 0; index--)
        {
            string stackId = rowScopedItemStackIds[index];
            if (liveStackIds.Contains(stackId))
                succeeded &= physicalItems.DeleteStack(stackId);
        }
        rowScopedItemStackIds.Clear();
        for (int index = rowScopedWildlifeIds.Count - 1; index >= 0; index--)
        {
            string wildlifeId = rowScopedWildlifeIds[index];
            if (runtimeScope?.Container != null)
            {
                IAnimalHusbandryQuery husbandryQuery =
                    runtimeScope.Container.Resolve<IAnimalHusbandryQuery>();
                IAnimalHusbandryCommand husbandryCommand =
                    runtimeScope.Container.Resolve<IAnimalHusbandryCommand>();
                WildlifeInstanceId animalId = new(wildlifeId);
                if (husbandryQuery.TryGetAnimal(
                        animalId,
                        out HusbandryAnimalState husbandryState)
                    && husbandryState.SlaughterDesignated)
                {
                    succeeded &= husbandryCommand.DesignateSlaughter(
                        animalId,
                        false,
                        out _);
                }
                IWildlifeCaptureRuntime captureRuntime =
                    runtimeScope.Container.Resolve<IWildlifeCaptureRuntime>();
                if (captureRuntime.TryGetCaptured(wildlifeId, out _))
                {
                    succeeded &= captureRuntime.TryRelease(
                        wildlifeId,
                        out _);
                }
            }
            bool stillLive = wildlifeRuntime?.Wildlife.Any(value =>
                value != null
                && string.Equals(
                    value.WildlifeId,
                    wildlifeId,
                    StringComparison.Ordinal)) == true;
            if (stillLive)
            {
                bool removed = wildlifeRuntime.TryRemoveArrival(wildlifeId);
                succeeded &= removed
                    && !wildlifeRuntime.Wildlife.Any(value =>
                        value != null
                        && string.Equals(
                            value.WildlifeId,
                            wildlifeId,
                            StringComparison.Ordinal));
            }
        }
        rowScopedWildlifeIds.Clear();
        for (int index = rowScopedFixtureBuildings.Count - 1; index >= 0; index--)
        {
            BuildableObject building = rowScopedFixtureBuildings[index];
            if (building == null)
                continue;
            worldRegistry?.UnregisterBuilding(building);
            Grid buildingGrid = building.Grid;
            BuildingSO data = building.BuildingData;
            if (buildingGrid != null && data != null)
            {
                IReadOnlyList<Vector2Int> positions =
                    data.GetGridPosList(building.centerPos);
                bool removed = buildingGrid.RemoveOccupant(
                    building,
                    data.Placement.Layer,
                    positions,
                    data.Placement.IsMovement);
                succeeded &= removed || positions.All(position =>
                    buildingGrid.GetGridCell(position)?.ContainsOccupant(
                        data.Placement.Layer,
                        building) != true);
            }
            Destroy(building.gameObject);
        }
        rowScopedFixtureBuildings.Clear();
        if (!succeeded)
            failureReason = "one or more authored row fixtures retained grid occupancy";
        return succeeded;
    }

    private IEnumerator CleanupSingleDomainRoomAfterRow(
        WorkTypeId workTypeId)
    {
        bool ownsRoom = workTypeId == BuiltInWorkTypeIds.Perform
            || workTypeId == BuiltInWorkTypeIds.AnimalCare;
        if (!ownsRoom)
            yield break;

        actor?.SetAiPaused(true);
        AbilityMove cleanupMove = actor?.GetAbility<AbilityMove>();
        brain?.StopCurrentActionForReplan(
            "worktype live matrix single-domain row cleanup");
        work?.ClearPriorityWorkTarget();
        // Stop the action/work owners first, then cancel their movement as the
        // final synchronous ownership fence.  Cancelling movement before
        // OnStop/StopAssignedWork lets their later teardown cancel a newly
        // started protected cleanup move.
        cleanupMove?.CancelActiveMovement(
            "worktype live matrix single-domain cleanup settle");
        Vector2Int? baselineReachableSentinel =
            roomBaselineReachableSentinel;
        int settledFrames = 0;
        float settleDeadline = Time.realtimeSinceStartup + 5f;
        while (settledFrames < 2
               && Time.realtimeSinceStartup < settleDeadline)
        {
            CharacterAiRuntimeGateSnapshot gate =
                brain?.CaptureRuntimeGateSnapshot() ?? default;
            bool settled = brain?.HasRunningAction != true
                && brain?.IsExternallyDrivenActionActive != true
                && work?.isWorking != true
                && work?.HasActiveWorkRoutineForDiagnostics != true
                && cleanupMove?.HasActiveMovementRoutineForDiagnostics != true
                && gate.LivePathRequests == 0
                && gate.LiveReservations == 0;
            settledFrames = settled ? settledFrames + 1 : 0;
            if (settledFrames < 2)
                yield return null;
        }
        if (settledFrames < 2)
        {
            CharacterAiRuntimeGateSnapshot gate =
                brain?.CaptureRuntimeGateSnapshot() ?? default;
            AddGlobalFailure(
                "single-domain-room-cleanup-did-not-settle",
                workTypeId.Value + ":running="
                + (brain?.HasRunningAction == true) + "; external="
                + (brain?.IsExternallyDrivenActionActive == true)
                + "; working=" + (work?.isWorking == true)
                + "; routine="
                + (work?.HasActiveWorkRoutineForDiagnostics == true)
                + "; movement="
                + (cleanupMove?.HasActiveMovementRoutineForDiagnostics == true)
                + "; paths=" + gate.LivePathRequests
                + "; reservations=" + gate.LiveReservations);
            abortRemainingRows = true;
            yield break;
        }
        if (roomExternalExitStand.HasValue && actor != null)
        {
            Vector2Int requestedExit = roomExternalExitStand.Value;
            Vector2Int exitStand = requestedExit;
            AbilityMove move = cleanupMove;
            if (move == null)
            {
                AddGlobalFailure(
                    "single-domain-room-exit-move-missing",
                    workTypeId.Value);
            }
            else if (actor.GetNowXY() != requestedExit)
            {
                IGridPathSearchBroker pathBroker =
                    runtimeScope.Container.Resolve<IGridPathSearchBroker>();
                GridTraversalContext traversal = GridTraversalContext.ForCharacter(
                    CharacterPersistentIdentity.Require(actor),
                    DoorAccessOverrideKind.None);
                Queue<GridMoveStep> preflightPath = null;
                float pathDeadline = Time.realtimeSinceStartup + 3f;
                while (preflightPath == null
                       && Time.realtimeSinceStartup < pathDeadline)
                {
                    preflightPath = pathBroker.GetMovePathTo(
                        grid,
                        actor.GetNowXY(),
                        requestedExit,
                        GridPathSearchPriority.Urgent,
                        traversal);
                    if (preflightPath == null)
                        yield return null;
                }
                bool exitPathVerified = false;
                if (preflightPath == null || preflightPath.Count == 0)
                {
                    AddGlobalFailure(
                        "single-domain-room-exit-preflight-unreachable",
                        workTypeId.Value + ":actor=" + actor.GetNowXY()
                        + "; requested=" + requestedExit);
                }
                else
                {
                    Vector2Int pathEnd = preflightPath.Last().To;
                    if (pathEnd == requestedExit)
                    {
                        exitPathVerified = true;
                    }
                    else
                    {
                        GridPathSearchResult outsideNetwork =
                            baselineReachableSentinel.HasValue
                                ? grid.SearchPath(
                                    baselineReachableSentinel.Value)
                                : null;
                        GridCell pathEndCell = grid.GetGridCell(pathEnd);
                        bool lawfulPathEnd = pathEndCell != null
                            && grid.IsWalkable(pathEnd)
                            && performanceRoom?.ContainsCell(pathEnd) != true
                            && outsideNetwork?.ContainsPosition(pathEnd) == true;
                        if (lawfulPathEnd)
                        {
                            exitStand = pathEnd;
                            exitPathVerified = true;
                        }
                        else
                        {
                            AddGlobalFailure(
                                "single-domain-room-exit-preflight-end-mismatch",
                                workTypeId.Value + ":requested="
                                + requestedExit + "; pathEnd=" + pathEnd
                                + "; lawful=" + lawfulPathEnd);
                        }
                    }
                }
                if (!exitPathVerified)
                {
                    abortRemainingRows = true;
                    yield break;
                }
                int movementVersionBeforeStart =
                    move.MovementOperationVersionForDiagnostics;
                string cancellationBeforeStart =
                    move.LastMovementCancellationSourceForDiagnostics;
                string preemptionBeforeStart =
                    move.LastMovementOperationPreemptionForDiagnostics;
                string rejectedOwnerBeforeStart =
                    move.LastRejectedMovementOperationOwnerForDiagnostics;
                if (!move.TryStartProtectedSystemMove(
                        exitStand,
                        DoorAccessOverrideKind.None,
                        out string moveFailure))
                {
                    AddGlobalFailure(
                        "single-domain-room-exit-path-rejected",
                        workTypeId.Value + ":" + moveFailure
                        + "; actor=" + actor.GetNowXY()
                        + "; exit=" + exitStand);
                    abortRemainingRows = true;
                    yield break;
                }
                else
                {
                    float moveStartedAt = Time.realtimeSinceStartup;
                    int movementVersionAfterStart =
                        move.MovementOperationVersionForDiagnostics;
                    string movementOwnerAfterStart =
                        move.ActiveMovementOperationOwnerForDiagnostics;
                    float worldDistance = 0f;
                    Vector3 previousWorld = actor.transform.position;
                    foreach (GridMoveStep step in preflightPath)
                    {
                        Vector3 nextWorld = grid.GetWorldPos(step.To);
                        worldDistance += Vector3.Distance(
                            previousWorld,
                            nextWorld);
                        previousWorld = nextWorld;
                    }
                    float gameSeconds = worldDistance
                        / Mathf.Max(0.1f, actor.GetMoveSpeed());
                    float realSeconds = gameSeconds
                        / Mathf.Max(0.01f, Time.timeScale);
                    float observationSeconds = Mathf.Clamp(
                        5f + realSeconds * 2f + preflightPath.Count * 0.1f,
                        MinimumProgressObservationSeconds,
                        MaximumProgressObservationSeconds);
                    float softObservationDeadline = Time.realtimeSinceStartup
                        + observationSeconds;
                    float hardExitDeadline = Time.realtimeSinceStartup
                        + MaximumProgressObservationSeconds;
                    float lastProgressAt = Time.realtimeSinceStartup;
                    Vector3 lastWorldPosition = actor.transform.position;
                    Vector2Int lastGridPosition = actor.GetNowXY();
                    int worldProgressSamples = 0;
                    int gridProgressSamples = 0;
                    bool stalled = false;
                    bool movementOwnershipLost = false;
                    while (move.HasActiveMovementRoutineForDiagnostics
                           && Time.realtimeSinceStartup < hardExitDeadline)
                    {
                        if (actor.GetNowXY() != exitStand
                            && (move.MovementOperationVersionForDiagnostics
                                != movementVersionAfterStart
                                || !move.IsSystemMoveInProgressTo(exitStand)))
                        {
                            movementOwnershipLost = true;
                            break;
                        }
                        Vector3 currentWorldPosition = actor.transform.position;
                        if ((currentWorldPosition - lastWorldPosition).sqrMagnitude
                            > MovementPositionEpsilonSquared)
                        {
                            lastProgressAt = Time.realtimeSinceStartup;
                            lastWorldPosition = currentWorldPosition;
                            worldProgressSamples++;
                            Vector2Int currentGridPosition = actor.GetNowXY();
                            if (currentGridPosition != lastGridPosition)
                            {
                                lastGridPosition = currentGridPosition;
                                gridProgressSamples++;
                            }
                        }
                        else if (Time.realtimeSinceStartup - lastProgressAt
                                 >= MovementStallSeconds)
                        {
                            stalled = true;
                            break;
                        }
                        yield return null;
                    }
                    bool activeBeforeCancel =
                        move.HasActiveMovementRoutineForDiagnostics;
                    bool blockedBeforeCancel = move.LastGridMoveWasBlocked;
                    GridMoveFailureReason failureBeforeCancel =
                        move.LastGridMoveFailureReason;
                    int movementVersionBeforeCancel =
                        move.MovementOperationVersionForDiagnostics;
                    string movementOwnerBeforeCancel =
                        move.ActiveMovementOperationOwnerForDiagnostics;
                    string cancellationAtTerminal =
                        move.LastMovementCancellationSourceForDiagnostics;
                    string preemptionAtTerminal =
                        move.LastMovementOperationPreemptionForDiagnostics;
                    string rejectedOwnerAtTerminal =
                        move.LastRejectedMovementOperationOwnerForDiagnostics;
                    bool exited = actor.GetNowXY() == exitStand
                        && !blockedBeforeCancel
                        && !activeBeforeCancel;
                    move.CancelActiveMovement(
                        "worktype live matrix room exit completed");
                    if (!exited)
                    {
                        AddGlobalFailure(
                            "single-domain-room-exit-terminal-failed",
                            workTypeId.Value + ":actor=" + actor.GetNowXY()
                            + "; requested=" + requestedExit
                            + "; exit=" + exitStand + "; active="
                            + activeBeforeCancel + "; blocked="
                            + blockedBeforeCancel + "; failure="
                            + failureBeforeCancel + "; stalled=" + stalled
                            + "; ownershipLost=" + movementOwnershipLost
                            + "; softEtaExpired="
                            + (Time.realtimeSinceStartup
                               >= softObservationDeadline)
                            + "; softObservation="
                            + observationSeconds.ToString("0.###")
                            + "; hardObservation="
                            + MaximumProgressObservationSeconds.ToString("0.###")
                            + "; elapsed="
                            + (Time.realtimeSinceStartup - moveStartedAt)
                                .ToString("0.###")
                            + "; pathSteps=" + preflightPath.Count
                            + "; progressSamples=" + worldProgressSamples
                            + "/" + gridProgressSamples
                            + "; lastProgressAge="
                            + (Time.realtimeSinceStartup - lastProgressAt)
                                .ToString("0.###")
                            + "; operationVersion="
                            + movementVersionBeforeStart + "->"
                            + movementVersionAfterStart + "->"
                            + movementVersionBeforeCancel
                            + "; ownerAfterStart=" + movementOwnerAfterStart
                            + "; ownerBeforeCancel="
                            + movementOwnerBeforeCancel
                            + "; cancellation="
                            + cancellationBeforeStart + "->"
                            + cancellationAtTerminal
                            + "; preemption=" + preemptionBeforeStart + "->"
                            + preemptionAtTerminal
                            + "; rejectedOwner=" + rejectedOwnerBeforeStart
                            + "->"
                            + rejectedOwnerAtTerminal);
                        // Keep the still-valid connector and room intact. The
                        // final baseline cleanup owns recovery after the matrix
                        // stops; rolling back under an in-room actor would
                        // contaminate every later row with an unreachable worker.
                        abortRemainingRows = true;
                        yield break;
                    }
                }
            }
        }
        string[] animalIds = rowScopedWildlifeIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (!CleanupRowScopedFixtures(out string rowFailure))
        {
            AddGlobalFailure(
                "single-domain-row-fixture-cleanup-failed",
                workTypeId.Value + ":" + rowFailure);
        }

        if (animalIds.Length > 0 && runtimeScope?.Container != null)
        {
            IAnimalHusbandryQuery husbandry =
                runtimeScope.Container.Resolve<IAnimalHusbandryQuery>();
            float deadline = Time.realtimeSinceStartup + 8f;
            bool HasProjectedAnimal() => animalIds.Any(id =>
                husbandry.TryGetAnimal(new WildlifeInstanceId(id), out _));
            while (HasProjectedAnimal()
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            if (HasProjectedAnimal())
            {
                AddGlobalFailure(
                    "single-domain-husbandry-projection-cleanup-timeout",
                    workTypeId.Value + ":" + string.Join(",", animalIds));
            }
        }

        if (!CleanupRoomFixture(out string roomFailure))
        {
            AddGlobalFailure(
                "single-domain-room-cleanup-failed",
                workTypeId.Value + ":" + roomFailure);
        }
        if (worldResources is WorldResourceRuntime worldResourceRuntime)
            worldResourceRuntime.Tick();
        if (runtimeScope?.Container != null)
        {
            runtimeScope.Container.Resolve<IFacilityCandidateCache>()
                .MarkDynamicStateDirty();
        }
        yield return null;
        GridPathSearchResult restoredSearch = actor != null
            ? grid.SearchPath(actor.GetNowXY())
            : null;
        bool actorRestored = actor != null
            && grid.IsWalkable(actor.GetNowXY())
            && (!baselineReachableSentinel.HasValue
                || restoredSearch?.ContainsPosition(
                    baselineReachableSentinel.Value) == true);
        if (!actorRestored)
        {
            AddGlobalFailure(
                "single-domain-room-actor-reachability-not-restored",
                workTypeId.Value + ":actor="
                + (actor != null ? actor.GetNowXY().ToString() : "missing")
                + "; sentinel="
                + (baselineReachableSentinel?.ToString() ?? "missing"));
        }
        bool exact = rowScopedWildlifeIds.Count == 0
            && roomFixtureBuildings.Count == 0
            && displacedRoomMovements.Count == 0
            && displacedRoomWildlife.Count == 0
            && roomAreaSnapshots.Count == 0
            && domainStage == null
            && domainPen == null
            && performanceRoom == null
            && !roomExternalExitStand.HasValue
            && !roomBaselineReachableSentinel.HasValue;
        if (!exact)
        {
            AddGlobalFailure(
                "single-domain-room-cleanup-incomplete",
                workTypeId.Value + ":rowWildlife="
                + rowScopedWildlifeIds.Count + "; buildings="
                + roomFixtureBuildings.Count + "; movements="
                + displacedRoomMovements.Count + "; displacedWildlife="
                + displacedRoomWildlife.Count + "; areas="
                + roomAreaSnapshots.Count + "; stage=" + (domainStage != null)
                + "; pen=" + (domainPen != null) + "; room="
                + (performanceRoom != null));
        }
        else
        {
            results.Add(WorkTypeLiveRow.Info(
                "single-domain-room-cleanup",
                workTypeId.Value
                + ": exact area/building/wildlife projection restored"));
        }
    }

    private bool CleanupRoomFixture(out string failureReason)
    {
        failureReason = string.Empty;
        bool hadFixtureState = roomFixtureBuildings.Count > 0
            || displacedRoomMovements.Count > 0
            || displacedRoomWildlife.Count > 0
            || roomAreaSnapshots.Count > 0
            || domainStage != null
            || domainPen != null
            || performanceRoom != null
            || roomExternalExitStand.HasValue
            || roomBaselineReachableSentinel.HasValue;
        if (!hadFixtureState)
            return true;
        bool buildingsRemoved = true;
        for (int index = roomFixtureBuildings.Count - 1; index >= 0; index--)
        {
            BuildableObject building = roomFixtureBuildings[index];
            if (building == null)
                continue;
            worldRegistry?.UnregisterBuilding(building);
            Grid buildingGrid = building.Grid;
            BuildingSO data = building.BuildingData;
            if (buildingGrid != null && data != null)
            {
                IReadOnlyList<Vector2Int> positions =
                    data.GetGridPosList(building.centerPos);
                bool removed = buildingGrid.RemoveOccupant(
                    building,
                    data.Placement.Layer,
                    positions,
                    data.Placement.IsMovement);
                buildingsRemoved &= removed || positions.All(position =>
                    buildingGrid.GetGridCell(position)?.ContainsOccupant(
                        data.Placement.Layer,
                        building) != true);
            }
            Destroy(building.gameObject);
        }
        roomFixtureBuildings.Clear();

        bool movementsRestored = true;
        foreach (DisplacedMovementSnapshot displaced in displacedRoomMovements)
        {
            bool registered = displaced.Building != null
                && grid != null
                && grid.RegisterOccupant(
                    displaced.Building,
                    displaced.Layer,
                    displaced.Positions,
                    displaced.ConnectPositions);
            movementsRestored &= registered
                && displaced.Positions.All(position =>
                    grid.GetGridCell(position)?.ContainsOccupant(
                        displaced.Layer,
                        displaced.Building) == true);
        }
        displacedRoomMovements.Clear();

        bool areasRestored = true;
        foreach (FixtureAreaSnapshot snapshot in roomAreaSnapshots)
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
        roomAreaSnapshots.Clear();

        bool wildlifeRestored = true;
        for (int index = displacedRoomWildlife.Count - 1;
             index >= 0;
             index--)
        {
            DisplacedWildlifeSnapshot displaced =
                displacedRoomWildlife[index];
            WildlifeActor animal = displaced.Actor;
            if (animal == null || !animal.IsAlive)
            {
                wildlifeRestored = false;
                continue;
            }
            if (animal.GridPosition != displaced.Origin)
            {
                if (!CanPlaceWildlifeAt(
                        grid,
                        displaced.Origin,
                        animal.CanEnterDungeon))
                {
                    wildlifeRestored = false;
                    continue;
                }
                animal.WarpTo(displaced.Origin);
            }
            wildlifeRestored &= animal.GridPosition == displaced.Origin
                && grid.GetGridCell(displaced.Origin)?.ContainsOccupant(
                    GridLayer.Wildlife,
                    animal) == true;
        }
        displacedRoomWildlife.Clear();
        domainStage = null;
        domainPen = null;
        performanceRoom = null;
        roomExternalExitStand = null;
        roomBaselineReachableSentinel = null;
        if (runtimeScope?.Container != null)
            runtimeScope.Container.Resolve<IRoomLayoutCache>().Clear();

        if (!buildingsRemoved || !movementsRestored || !areasRestored
            || !wildlifeRestored)
        {
            failureReason = "buildings=" + buildingsRemoved
                + "; movements=" + movementsRestored
                + "; areas=" + areasRestored
                + "; wildlife=" + wildlifeRestored;
            return false;
        }
        return true;
    }

    private void Cleanup()
    {
        try
        {
            brain?.StopCurrentActionForReplan("worktype live matrix cleanup");
            work?.ClearPriorityWorkTarget();
            if (!CleanupRowScopedFixtures(out string rowCleanupFailure))
                AddGlobalFailure("row-fixture-cleanup-failed", rowCleanupFailure);
            if (!CleanupRoomFixture(out string roomCleanupFailure))
                AddGlobalFailure("room-fixture-cleanup-failed", roomCleanupFailure);
            if (saveRegistry != null && baseline != null)
            {
                DungeonGameRestoreReport report = new DungeonGameRestoreReport();
                if (!saveRegistry.RestoreAll(baseline, report))
                {
                    AddGlobalFailure(
                        "baseline-restore-failed",
                        string.Join(" | ", report.Errors));
                }
            }
            RestoreActorPauseStates();
        }
        catch (Exception exception)
        {
            AddGlobalFailure("cleanup-exception", exception.ToString());
        }
    }

    private void AddGlobalFailure(string id, string detail)
    {
        results.Add(new WorkTypeLiveRow("global:" + id, "FAIL", "-", detail));
    }

    private void RestoreActorPauseStates()
    {
        if (worldRegistry == null || actorPauseStates.Count == 0)
            return;

        CharacterActor[] liveActors = LiveActors(worldRegistry);
        foreach (ActorPauseState state in actorPauseStates)
        {
            CharacterActor restored = liveActors.FirstOrDefault(value =>
                string.Equals(
                    value.Identity?.PersistentId,
                    state.PersistentId,
                    StringComparison.Ordinal));
            if (restored == null)
            {
                AddGlobalFailure(
                    "actor-pause-restore-target-missing",
                    state.PersistentId);
                continue;
            }
            restored.SetAiPaused(state.WasPaused);
        }
    }

    private void WriteReport()
    {
        int passed = results.Count(row => row.Status == "PASS");
        int blocked = results.Count(row => row.Status == "BLOCKED");
        int failed = results.Count(row => row.Status == "FAIL");
        string[] focusedRows =
        {
            "p15:manual",
            "p15:powered-assist",
            "p15:allocated-worker-transition",
            "p15:automatic",
            "p15:utility-failure-atomic"
        };
        bool complete = consoleGatePassed
            && (P15AutomationModesOnly
                ? focusedRows.All(id => results.Any(row =>
                    row.WorkTypeId == id && row.Status == "PASS"))
                : Rows.All(id => results.Any(row =>
                    row.WorkTypeId == id.Value && row.Status == "PASS")));
        string reportPath = P15AutomationModesOnly
            ? CharacterAiWorkTypeLiveMatrixPlayModeVerifier
                .P15AutomationModesReportPath
            : CharacterAiWorkTypeLiveMatrixPlayModeVerifier.ReportPath;
        List<string> lines = new List<string>(results.Count + 8)
        {
            P15AutomationModesOnly
                ? "# P15 production execution-mode live matrix"
                : "# Character AI WorkType production-live matrix",
            P15AutomationModesOnly
                ? "authority=Manual/PoweredAssist:Brain -> AIWork -> AbilityWork -> WorkTaskExecutor; Automatic:AutomationRuntime -> ProductionBillWorkExecution"
                : "authority=Brain -> AIWork -> AbilityWork -> WorkTaskExecutor",
            "contract/direct-handler evidence is never accepted as PASS",
            "RESULT=" + (complete && failed == 0 && blocked == 0 ? "PASS" : "FAIL")
                + "; rows=" + (P15AutomationModesOnly
                    ? focusedRows.Length
                    : Rows.Length) + "; passed=" + passed
                + "; blocked=" + blocked + "; failed=" + failed,
            "status\tworkType\ttarget\tdetail"
        };
        IEnumerable<WorkTypeLiveRow> reportRows = P15AutomationModesOnly
            ? results.Where(row => focusedRows.Contains(
                    row.WorkTypeId,
                    StringComparer.Ordinal)
                || row.Status == "FAIL"
                || row.WorkTypeId == "info:console-warning-error-zero")
            : results;
        lines.AddRange(reportRows.Select(row =>
            row.Status + "\t" + row.WorkTypeId + "\t" + row.Target + "\t" + row.Detail));
        Directory.CreateDirectory(Path.GetDirectoryName(
            reportPath)
            ?? "Artifacts/QA");
        WriteUtf8LfIfChanged(reportPath, lines);
        string resultPrefix = P15AutomationModesOnly
            ? "P15_PRODUCTION_EXECUTION_MODES"
            : "CHARACTER_AI_WORKTYPE_LIVE_MATRIX";
        Debug.Log(resultPrefix + "="
            + (complete && failed == 0 && blocked == 0 ? "PASS" : "FAIL")
            + "; passed=" + passed
            + "; blocked=" + blocked + "; failed=" + failed);
    }

    private static void WriteUtf8LfIfChanged(
        string path,
        IReadOnlyList<string> lines)
    {
        string contents = string.Join("\n", lines) + "\n";
        if (File.Exists(path)
            && string.Equals(
                File.ReadAllText(path, Encoding.UTF8),
                contents,
                StringComparison.Ordinal))
        {
            return;
        }
        File.WriteAllText(
            path,
            contents,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private enum WorkProbeFault
    {
        CancelAfterApprovedProgress,
        InvalidateTargetAfterStart,
        CompleteNormally
    }

    private delegate bool FixtureMutation(out string failureReason);

    private sealed class MaterialWorkFixture
    {
        private readonly FixtureMutation prepareInvalidation;
        private readonly FixtureMutation invalidate;

        public MaterialWorkFixture(
            BuildableObject target,
            string detail,
            FixtureMutation prepareInvalidation,
            FixtureMutation invalidate,
            bool allowEquivalentTarget = false)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Detail = detail ?? string.Empty;
            this.prepareInvalidation = prepareInvalidation;
            this.invalidate = invalidate
                ?? throw new ArgumentNullException(nameof(invalidate));
            AllowEquivalentTarget = allowEquivalentTarget;
        }

        public BuildableObject Target { get; }
        public string Detail { get; }
        public bool AllowEquivalentTarget { get; }

        public bool AcceptsTarget(BuildableObject target) =>
            ReferenceEquals(target, Target)
            || AllowEquivalentTarget && target != null && !target.isDestroy;

        public bool TryPrepareInvalidationPhase(out string failureReason)
        {
            if (prepareInvalidation == null)
            {
                failureReason = string.Empty;
                return true;
            }
            return prepareInvalidation(out failureReason);
        }

        public bool TryInvalidate(out string failureReason) =>
            invalidate(out failureReason);
    }

    private sealed class P15FocusedFixture
    {
        public P15FocusedFixture(
            BuildableObject target,
            MaterialWorkFixture fixture,
            ProductionBillId billId)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            BillId = billId;
        }

        public BuildableObject Target { get; }
        public MaterialWorkFixture Fixture { get; }
        public ProductionBillId BillId { get; }
    }

    private sealed class WorkPhaseResult
    {
        public WorkPhaseResult(WorkProbeFault fault) { Fault = fault; }
        public WorkProbeFault Fault { get; }
        public bool Started;
        public bool Progressed;
        public bool CompletionTargetRemoved;
        public bool TerminalRequested;
        public bool TerminalObserved;
        public bool TypedTerminal;
        public bool LifecycleConserved;
        public bool PathsConserved;
        public bool ReservationsConserved;
        public bool NoInvariantAnomaly;
        public long StartEpoch;
        public long ObservedEpoch;
        public long ProgressDelta;
        public long ActionStarts;
        public long ActionTerminals;
        public long Cancelled;
        public long Failed;
        public long PathRequests;
        public long PathResults;
        public long ReservationAcquires;
        public long ReservationReleases;
        public long GameplayProgressDelta;
        public int PreStartDeferredWaits;
        public float EstimatedTravelSeconds;
        public bool MovementObserved;
        public bool MovementStalled;
        public Vector2Int MovementStartPosition;
        public Vector2Int MovementEndPosition;
        public CharacterAiActionTerminalKind ObservedTerminalKind;
        public string ObservedWorkType = string.Empty;
        public string ObservedActionType = string.Empty;
        public string PriorityCommandDetail = "direct";
        public string StageBeforeTerminalRequest = string.Empty;
        public string DomainTrace = string.Empty;
        public string Blocker = string.Empty;
        public CharacterAiRuntimeGateSnapshot StartGate;
        public CharacterAiRuntimeGateSnapshot EndGate;

        public bool Passed => Started
            && (Fault == WorkProbeFault.InvalidateTargetAfterStart || Progressed)
            && (Fault != WorkProbeFault.CompleteNormally
                || CompletionTargetRemoved)
            && TerminalRequested
            && TerminalObserved
            && TypedTerminal
            && ObservedEpoch > StartEpoch
            && LifecycleConserved
            && PathsConserved
            && ReservationsConserved
            && NoInvariantAnomaly
            && string.IsNullOrEmpty(Blocker);

        public string Format() =>
            Fault + ":pass=" + Passed
            + "; epoch=" + StartEpoch + "->" + ObservedEpoch
            + "; workType=" + ObservedWorkType
            + "; actionType=" + ObservedActionType
            + "; priorityCommand=" + PriorityCommandDetail
            + "; progress=" + ProgressDelta
            + "; completionTargetRemoved=" + CompletionTargetRemoved
            + "; action=" + ActionStarts + "/" + ActionTerminals
            + "; terminal=" + Cancelled + "C/" + Failed + "F"
            + "; terminalEpochKind=" + ObservedTerminalKind
            + "; path=" + PathRequests + "/" + PathResults
            + "; reservation=" + ReservationAcquires + "/" + ReservationReleases
            + "; movement=" + MovementObserved + "/" + MovementStalled
            + "@" + MovementStartPosition + "->" + MovementEndPosition
            + "; gameplayProgress=" + GameplayProgressDelta
            + "; preStartDeferredWaits=" + PreStartDeferredWaits
            + "; etaCap=" + EstimatedTravelSeconds.ToString("0.###")
            + "; conserve=" + LifecycleConserved + "/" + PathsConserved
            + "/" + ReservationsConserved
            + "; invariant=" + NoInvariantAnomaly
            + (string.IsNullOrEmpty(StageBeforeTerminalRequest)
                ? string.Empty
                : "; preTerminal=" + StageBeforeTerminalRequest)
            + (string.IsNullOrEmpty(DomainTrace)
                ? string.Empty
                : "; domainTrace=" + DomainTrace)
            + "; blocker=" + (string.IsNullOrEmpty(Blocker) ? "none" : Blocker);
    }

    private readonly struct WorkTypeLiveRow
    {
        public WorkTypeLiveRow(
            string workTypeId,
            string status,
            string target,
            string detail)
        {
            WorkTypeId = workTypeId ?? string.Empty;
            Status = status ?? string.Empty;
            Target = target ?? string.Empty;
            Detail = (detail ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
        }

        public string WorkTypeId { get; }
        public string Status { get; }
        public string Target { get; }
        public string Detail { get; }

        public static WorkTypeLiveRow Blocked(
            WorkTypeId id,
            string blocker,
            string detail) =>
            new WorkTypeLiveRow(id.Value, "BLOCKED", "-", blocker + ":" + detail);

        public static WorkTypeLiveRow Info(string id, string detail) =>
            new WorkTypeLiveRow("info:" + id, "INFO", "-", detail);
    }

    private readonly struct ActorPauseState
    {
        public ActorPauseState(string persistentId, bool wasPaused)
        {
            PersistentId = persistentId ?? string.Empty;
            WasPaused = wasPaused;
        }

        public string PersistentId { get; }
        public bool WasPaused { get; }
    }

    private readonly struct DisplacedMovementSnapshot
    {
        public DisplacedMovementSnapshot(
            BuildableObject building,
            GridLayer layer,
            Vector2Int[] positions,
            bool connectPositions)
        {
            Building = building;
            Layer = layer;
            Positions = positions ?? Array.Empty<Vector2Int>();
            ConnectPositions = connectPositions;
        }

        public BuildableObject Building { get; }
        public GridLayer Layer { get; }
        public Vector2Int[] Positions { get; }
        public bool ConnectPositions { get; }
    }

    private readonly struct FixtureAreaSnapshot
    {
        public FixtureAreaSnapshot(
            Vector2Int position,
            GridCellAreaType areaType)
        {
            Position = position;
            AreaType = areaType;
        }

        public Vector2Int Position { get; }
        public GridCellAreaType AreaType { get; }
    }

    private readonly struct DisplacedWildlifeSnapshot
    {
        public DisplacedWildlifeSnapshot(
            WildlifeActor actor,
            Vector2Int origin,
            Vector2Int destination)
        {
            Actor = actor;
            Origin = origin;
            Destination = destination;
        }

        public WildlifeActor Actor { get; }
        public Vector2Int Origin { get; }
        public Vector2Int Destination { get; }
    }
}
#endif
