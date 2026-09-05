#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BehaviorDesigner.Runtime;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

/// <summary>
/// Live fault-injection verification for the authored Brain -> behaviour tree ->
/// action -> AbilityMove/AbilityShopping pipeline.  The verifier intentionally
/// does not invoke action enumerators or decision methods directly.
/// </summary>
public static class CharacterAiFaultRecoveryPlayModeVerifier
{
    public const string VerifierRevision =
        "fault-recovery-selector-artifacts-v1";
    public const string ReportPath =
        "Artifacts/QA/character-ai-fault-recovery-playmode.txt";
    private const string PendingFlagPath =
        "Temp/character-ai-fault-recovery-playmode.flag";
    private static readonly string[] DurableGroupSelectors =
    {
        "core",
        "facility-shared",
        "facility-action",
        "destinationless",
        "deprivation",
        "primitive",
        "subscriber"
    };
    private static readonly IReadOnlyDictionary<string, string[]> ExpectedRowsBySelector =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["core"] = new[]
            {
                "core:repath",
                "core:no-path"
            },
            ["facility-shared"] = new[]
            {
                "facility-shared:approach",
                "facility-shared:queue",
                "facility-shared:interaction"
            },
            ["facility-action"] = new[]
            {
                "facility-action:eat:approach",
                "facility-action:eat:queue",
                "facility-action:eat:interaction",
                "facility-action:toilet:approach",
                "facility-action:toilet:queue",
                "facility-action:toilet:interaction",
                "facility-action:hygiene:approach",
                "facility-action:hygiene:queue",
                "facility-action:hygiene:interaction",
                "facility-action:recreation:approach",
                "facility-action:recreation:queue",
                "facility-action:recreation:interaction",
                "facility-action:shopping:approach",
                "facility-action:shopping:queue",
                "facility-action:shopping:interaction"
            },
            ["destinationless"] = new[]
            {
                "destinationless:look_around:recovery",
                "destinationless:look_around:starvation",
                "destinationless:wait:recovery",
                "destinationless:wait:starvation",
                "destinationless:exit_dungeon:recovery",
                "destinationless:exit_dungeon:starvation"
            },
            ["deprivation"] = new[]
            {
                "deprivation:relief",
                "deprivation:drink",
                "deprivation:eat",
                "deprivation:collapse",
                "deprivation:violent"
            },
            ["primitive"] = new[]
            {
                "primitive:field-meal:commit-loss",
                "primitive:bucket-wash:path-invalidated",
                "primitive:bucket-wash:target-lost",
                "primitive:latrine:path-invalidated",
                "primitive:latrine:target-invalidated",
                "primitive:floor-rest:interrupted"
            },
            ["subscriber"] = new[]
            {
                "subscriber:throwing-destruction-handler"
            }
        };

    [MenuItem("DungeonStory/Debug/QA/Run Character AI Fault Recovery PlayMode Verification")]
    public static void RunFromMenu() => RequestRun();

    /// <summary>
    /// Public Unity-MCP entry point. It may be called in EditMode or PlayMode.
    /// </summary>
    public static void RequestRun()
    {
        RequestRun("all");
    }

    public static void RequestRun(string selector)
    {
        string normalized = NormalizeSelector(selector);
        if (EditorApplication.isPlaying)
        {
            StartRunner(normalized, exitPlayModeOnCompletion: false);
            return;
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingFlagPath, normalized);
        EditorApplication.EnterPlaymode();
    }

    public static void RequestCoreMovementGroup() => RequestRun("core");
    public static void RequestSharedFacilityGroup() => RequestRun("facility-shared");
    public static void RequestActionFacilityGroup() => RequestRun("facility-action");
    public static void RequestDestinationlessGroup() => RequestRun("destinationless");
    public static void RequestDeprivationGroup() => RequestRun("deprivation");
    public static void RequestPrimitiveSurvivalGroup() => RequestRun("primitive");
    public static void RequestSubscriberGroup() => RequestRun("subscriber");

    public static IReadOnlyList<string> GetExpectedRowsForSelector(
        string selector)
    {
        string normalized = NormalizeSelector(selector);
        if (normalized == "all")
        {
            return DurableGroupSelectors
                .SelectMany(group => ExpectedRowsBySelector[group])
                .ToArray();
        }

        return ExpectedRowsBySelector.TryGetValue(
            normalized,
            out string[] rows)
            ? rows
            : Array.Empty<string>();
    }

    public static string GetReportPathForSelector(string selector)
    {
        string normalized = NormalizeSelector(selector);
        if (normalized == "all")
            return ReportPath;

        if (ExpectedRowsBySelector.ContainsKey(normalized))
        {
            return "Artifacts/QA/character-ai-fault-recovery-"
                + normalized + "-playmode.txt";
        }

        string safeSelector = new string(normalized
            .Select(character => char.IsLetterOrDigit(character)
                || character == '-'
                ? character
                : '-')
            .ToArray());
        return "Artifacts/QA/character-ai-fault-recovery-diagnostic-"
            + safeSelector + "-playmode.txt";
    }

    internal static IReadOnlyList<string> GetDurableGroupSelectors() =>
        DurableGroupSelectors;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapPendingRun()
    {
        if (!File.Exists(PendingFlagPath))
        {
            return;
        }

        string selector = NormalizeSelector(File.ReadAllText(PendingFlagPath));
        File.Delete(PendingFlagPath);
        StartRunner(selector, exitPlayModeOnCompletion: true);
    }

    private static void StartRunner(string selector, bool exitPlayModeOnCompletion)
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                CharacterAiFaultRecoveryPlayModeRunner>() != null)
        {
            Debug.LogWarning("Character AI fault recovery verification is already running.");
            return;
        }

        CharacterAiFaultRecoveryPlayModeRunner runner =
            new GameObject("Character AI Fault Recovery PlayMode Runner")
                .AddComponent<CharacterAiFaultRecoveryPlayModeRunner>();
        runner.ConfigureSelector(selector, exitPlayModeOnCompletion);
    }

    private static string NormalizeSelector(string selector) =>
        string.IsNullOrWhiteSpace(selector)
            ? "all"
            : selector.Trim().ToLowerInvariant();
}

public sealed class CharacterAiFaultRecoveryPlayModeRunner : MonoBehaviour
{
    private const float VerificationTimeScale = 4f;
    private const float ActionStartTimeoutRealtime = 8f;
    // The official grid uses authored world-space cell spacing and live
    // character kinematics. A valid two-stair detour can exceed the former
    // eight-real-second fixture timeout for a slow healthy actor even though
    // the action is progressing normally.
    private const float ScenarioTimeoutRealtime = 20f;
    private const float StarvationScenarioTimeoutRealtime = 75f;
    private const int StarvationScenarioMaximumFrames = 256;
    private const int StarvationFixtureDeferralLimit = 12;
    private const int ProductionPathSearchDeferralLimit = 64;
    private const float OverallTimeoutRealtime = 270f;
    private readonly List<string> checks = new();
    private readonly List<string> failures = new();
    private readonly List<string> consoleIssues = new();
    private readonly List<MonoBehaviourState> pausedAi = new();
    private readonly List<BuildingSO> runtimeDefinitions = new();
    private readonly List<BuildableObject> runtimeFacilities = new();
    private readonly List<Vector2Int> addedHallwayCells = new();
    private readonly List<FaultWallOccupant> walls = new();
    private readonly List<FaultStairOccupant> stairs = new();
    private readonly Dictionary<Vector2Int, GridTraversalLink[]>
        originalTraversalLinksByCell = new();
    private readonly Dictionary<Vector2Int, IGridOccupant>
        originalMovementBuildingByCell = new();
    private readonly Dictionary<Vector2Int, GridCellAreaType>
        originalAreaTypesByCell = new();
    private readonly List<string> temporaryStackIds = new();
    private readonly Dictionary<string, int> primitiveStartedCounts = new();
    private readonly Dictionary<string, int> primitiveCompletedCounts = new();
    private readonly List<string> startedRowIds = new();
    private readonly List<string> completedRowIds = new();
    private readonly Dictionary<string, bool> rowPassedById =
        new(StringComparer.Ordinal);

    private LifetimeScope scope;
    private GridSystemManager gridSystem;
    private Grid grid;
    private CharacterActor subject;
    private CharacterActor queueHolder;
    private AIBrain brain;
    private AbilityMove move;
    private IFacilityCandidateCache facilityCandidates;
    private ICharacterAiActionAssetCatalog actionAssetCatalog;
    private IGameClock gameClock;
    private ICharacterDeprivationCommand deprivationCommands;
    private IWorldItemStackRuntime itemStacks;
    private WorldItemRepository itemRepository;
    private IItemQuantityReservationService itemReservations;
    private IDisposable primitiveStartedSubscription;
    private IDisposable primitiveCompletedSubscription;
    private AIAction[] originalActions;
    private Dictionary<CharacterCondition, float> originalStats;
    private Vector3 originalPosition;
    private bool originalSubjectOffDuty;
    private float originalTimeScale;
    private CorridorFixture corridor;
    private bool capturingLogs;
    private string selector = "all";
    private string currentScenario = "bootstrap";
    private float runStartedRealtime;
    private float scenarioStartedRealtime;
    private float overallDeadlineRealtime;
    private DateTime runStartedUtc;
    private bool runActive;
    private bool finalized;
    private bool exitPlayModeOnCompletion;
    private int failuresAtScenarioStart;
    private int selectedScenarioCount;

    public void ConfigureSelector(string value, bool exitPlayModeWhenDone = false)
    {
        selector = string.IsNullOrWhiteSpace(value)
            ? "all"
            : value.Trim().ToLowerInvariant();
        exitPlayModeOnCompletion = exitPlayModeWhenDone;
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        runStartedUtc = DateTime.UtcNow;
        runStartedRealtime = Time.realtimeSinceStartup;
        overallDeadlineRealtime = runStartedRealtime + OverallTimeoutRealtime;
        runActive = true;
        originalTimeScale = Time.timeScale;
        Time.timeScale = VerificationTimeScale;
        StartLogCapture();

        try
        {
            BeginScenario("setup:resolve-live-world");
            yield return ResolveLiveWorld();
            CompleteScenario("setup:resolve-live-world");
            if (failures.Count == 0)
            {
                if (ShouldRun("core"))
                {
                    if (ShouldRun("core:repath"))
                    {
                        BeginScenario("core:repath");
                        yield return RunRepathScenario();
                        CompleteScenario("core:repath");
                    }
                    if (ShouldRun("core:no-path"))
                    {
                        BeginScenario("core:no-path");
                        yield return RunNoPathScenario();
                        CompleteScenario("core:no-path");
                    }
                }
                if (ShouldRun("facility-shared"))
                    yield return RunFacilityDestructionMatrix();
                if (ShouldRun("facility-action"))
                    yield return RunActionSpecificFacilityDestructionMatrix();
                if (ShouldRun("destinationless"))
                    yield return RunDestinationlessDeferredRecoveryMatrix();
                if (ShouldRun("deprivation"))
                    yield return RunDeprivationBreakdownTerminalMatrix();
                if (ShouldRun("primitive"))
                    yield return RunPrimitiveSurvivalFaultMatrix();
                if (ShouldRun("subscriber"))
                {
                    BeginScenario("subscriber:throwing-destruction-handler");
                    yield return RunThrowingDestructionSubscriberScenario();
                    CompleteScenario("subscriber:throwing-destruction-handler");
                }
            }
        }
        finally
        {
            FinalizeRun();
        }
    }

    private void Update()
    {
        if (!runActive || finalized
            || Time.realtimeSinceStartup < overallDeadlineRealtime)
        {
            return;
        }

        string failure = $"OVERALL_TIMEOUT: selector={selector}; scenario={currentScenario}; "
            + $"elapsed={Time.realtimeSinceStartup - runStartedRealtime:0.###}s";
        failures.Add(failure);
        checks.Add("FAIL " + failure);
        StopAllCoroutines();
        FinalizeRun();
    }

    private IEnumerator ResolveLiveWorld()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindScope();
            gridSystem = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
            grid = gridSystem?.grid;
            CharacterActor[] liveActors = GetLiveActors();
            if (scope != null && grid != null && liveActors.Length >= 2)
            {
                break;
            }

            if (scope != null && liveActors.Length == 0)
            {
                string result = StartPartyPreparationPlayModeVerifier
                    .RunFastCommitForDebug();
                checks.Add("SETUP start-party=" + Compact(result));
            }
            yield return null;
        }

        CharacterActor[] actors = GetLiveActors();
        subject = actors.FirstOrDefault(IsPipelineActor);
        queueHolder = actors.FirstOrDefault(candidate =>
            candidate != subject && candidate.BuildingVisitor != null);
        Check(scope != null, "LIVE_SCOPE", "active LifetimeScope");
        Check(grid != null, "LIVE_GRID", grid != null
            ? $"{grid.width}x{grid.height}" : "missing");
        Check(subject != null, "LIVE_PIPELINE_ACTOR", subject?.name ?? "missing");
        Check(queueHolder != null, "LIVE_QUEUE_HOLDER", queueHolder?.name ?? "missing");
        if (subject == null || queueHolder == null || grid == null || scope == null)
        {
            yield break;
        }

        brain = subject.Brain;
        subject.TryGetAbility(out move);
        facilityCandidates = scope.Container.Resolve<IFacilityCandidateCache>();
        actionAssetCatalog = scope.Container.Resolve<ICharacterAiActionAssetCatalog>();
        gameClock = scope.Container.Resolve<IGameClock>();
        deprivationCommands = scope.Container.Resolve<ICharacterDeprivationCommand>();
        itemStacks = scope.Container.Resolve<IWorldItemStackRuntime>();
        itemRepository = scope.Container.Resolve<WorldItemRepository>();
        itemReservations = scope.Container.Resolve<IItemQuantityReservationService>();
        IGameEventBus events = scope.Container.Resolve<IGameEventBus>();
        CharacterId subjectId = CharacterPersistentIdentity.Require(subject);
        primitiveStartedSubscription = events.Subscribe<CharacterPrimitiveSurvivalStartedEvent>(
            started =>
            {
                if (started.CharacterId.Equals(subjectId))
                    IncrementPrimitiveCount(primitiveStartedCounts, started.ActionId);
            });
        primitiveCompletedSubscription = events.Subscribe<CharacterPrimitiveSurvivalCompletedEvent>(
            completed =>
            {
                if (completed.CharacterId.Equals(subjectId))
                    IncrementPrimitiveCount(primitiveCompletedCounts, completed.ActionId);
            });
        originalActions = brain.availableActions;
        originalStats = new Dictionary<CharacterCondition, float>(subject.Stats.StatSnapshot);
        originalPosition = subject.transform.position;
        originalSubjectOffDuty = subject.TryGetAbility(out AbilityWork originalWork)
            && originalWork.IsOffDuty;
        AIAction restAction = originalActions?.FirstOrDefault(
            candidate => candidate?.actionset is AIRest);
        Check(restAction != null, "LIVE_REST_ACTION", restAction?.actionset?.name ?? "missing");
        if (restAction == null)
        {
            yield break;
        }

        NeutralizeSubjectAndEndPrimitiveFallback();
        brain.availableActions = new[] { restAction };
        PauseOtherAi();
        if (!TryCreateCorridor(out corridor, out string corridorFailure))
        {
            Check(false, "CORRIDOR_FIXTURE", corridorFailure);
            yield break;
        }
        Check(true, "CORRIDOR_FIXTURE", corridor.ToString());
        gridSystem.NotifyGridObjectChanged();
        yield return null;
    }

    private IEnumerator RunRepathScenario()
    {
        BuildableObject primary = CreateRestFacility(
            corridor.Primary,
            "repath-primary",
            useDuration: 0.45f);
        Check(primary != null, "REPATH_PRIMARY_CREATED", Describe(primary));
        if (primary == null)
        {
            yield break;
        }

        long actionStartsBefore = brain.RuntimeActionStartCount;
        yield return PrepareAction(primary, "repath");
        AIAction committed = brain.bestAction;
        long repathsBefore = move.RuntimeActionPathReplanCount;
        long pathFailuresBefore = move.RuntimeActionPathFailureCount;
        Vector2Int start = subject.GetNowXY();

        FaultWallOccupant blocker = AddWall(corridor.LowerBlock, "repath-blocker");
        gridSystem.NotifyGridObjectChanged();
        bool switched = false;
        int abaOscillations = 0;
        Vector2Int previous = start;
        Vector2Int previousPrevious = start;
        bool reachedTarget = false;
        float deadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
        while (!reachedTarget && Time.realtimeSinceStartup < deadline)
        {
            if (brain.bestAction != null && !ReferenceEquals(brain.bestAction, committed))
            {
                switched = true;
            }
            Vector2Int current = subject.GetNowXY();
            reachedTarget = current == corridor.Primary;
            if (current == previousPrevious && current != previous)
            {
                abaOscillations++;
            }
            previousPrevious = previous;
            previous = current;
            yield return null;
        }

        Check(reachedTarget, "REPATH_TARGET_REACHED",
            $"position={subject.GetNowXY()}; target={corridor.Primary}; "
            + $"facilityUses={primary?.FacilityState.completedUses}; phase={brain.CurrentActionPhase}; "
            + $"detail={brain.CurrentActionPhaseDetail}; blocked={move.LastGridMoveFailureReason}");
        Check(!switched, "REPATH_SAME_ACTION",
            $"same={!switched}; action={Describe(committed)}");
        Check(move.RuntimeActionPathReplanCount == repathsBefore + 1,
            "REPATH_EXACTLY_ONCE",
            $"repaths={repathsBefore}->{move.RuntimeActionPathReplanCount}");
        Check(move.RuntimeActionPathFailureCount == pathFailuresBefore,
            "REPATH_NO_TERMINAL_FAILURE",
            $"failures={pathFailuresBefore}->{move.RuntimeActionPathFailureCount}");
        Check(abaOscillations == 0, "REPATH_NO_ABA_OSCILLATION",
            $"oscillations={abaOscillations}");
        Check(brain.RuntimeActionStartCount == actionStartsBefore + 1,
            "REPATH_NO_DUPLICATE_ACTION_START",
            $"starts={actionStartsBefore}->{brain.RuntimeActionStartCount}");
        RemoveWall(blocker);
        DestroyFacility(primary);
        yield return ResetBetweenScenarios();
    }

    private IEnumerator RunNoPathScenario()
    {
        BuildableObject primary = CreateRestFacility(
            corridor.Primary,
            "no-path-primary",
            useDuration: 0.45f);
        Check(primary != null, "NOPATH_PRIMARY_CREATED", Describe(primary));
        if (primary == null)
        {
            yield break;
        }

        yield return PrepareAction(primary, "no-path");
        AIAction committed = brain.bestAction;
        long failuresBefore = brain.RuntimeExecutionFailureCount;
        long replansBefore = brain.RuntimeImmediateReplanCount;
        long protectedReplansBefore =
            brain.RuntimeProtectedRunningActionReplanCount;
        int immediateDecisionsBefore = brain.ImmediateDecisionRequestCount;
        long pathFailuresBefore = move.RuntimeActionPathFailureCount;
        FaultWallOccupant lower = AddWall(corridor.LowerBlock, "seal-lower");
        FaultWallOccupant upper = AddWall(corridor.UpperBlock, "seal-upper");
        gridSystem.NotifyGridObjectChanged();

        AIActionFailure capturedFailure = AIActionFailure.None;
        float deadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
        while (brain.RuntimeExecutionFailureCount == failuresBefore
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        if (brain.RuntimeExecutionFailureCount > failuresBefore)
        {
            capturedFailure = brain.LastActionFailure;
        }

        Check(brain.RuntimeExecutionFailureCount == failuresBefore + 1,
            "NOPATH_TYPED_FAILURE_ONCE",
            $"count={failuresBefore}->{brain.RuntimeExecutionFailureCount}; failure={capturedFailure}");
        Check(capturedFailure.Kind == AIActionFailureKind.NoPath,
            "NOPATH_KIND", capturedFailure.ToString());
        Check(move.RuntimeActionPathFailureCount == pathFailuresBefore + 1,
            "NOPATH_MOVE_TERMINAL_ONCE",
            $"moveFailures={pathFailuresBefore}->{move.RuntimeActionPathFailureCount}");
        Check(committed == null || !committed.HasReservation,
            "NOPATH_RESERVATION_RELEASED",
            $"actionReservation={committed?.HasReservation}; facilityReservations={primary.ActiveVisitReservationCount}");
        Check(primary.ActiveVisitReservationCount == 0,
            "NOPATH_FACILITY_RESERVATION_RELEASED",
            $"reservations={primary.ActiveVisitReservationCount}");
        bool directReplan =
            brain.RuntimeImmediateReplanCount >= replansBefore + 1;
        bool protectedReplanWake =
            brain.RuntimeProtectedRunningActionReplanCount
                >= protectedReplansBefore + 1
            && brain.ImmediateDecisionRequestCount
                >= immediateDecisionsBefore + 1;
        Check(directReplan || protectedReplanWake,
            "NOPATH_IMMEDIATE_REPLAN",
            $"route={(directReplan ? "direct" : protectedReplanWake ? "protected-wake" : "none")}; "
            + $"replans={replansBefore}->{brain.RuntimeImmediateReplanCount}; "
            + $"protected={protectedReplansBefore}->{brain.RuntimeProtectedRunningActionReplanCount}; "
            + $"decisions={immediateDecisionsBefore}->{brain.ImmediateDecisionRequestCount}");

        long hotStartsBefore = brain.RuntimeActionStartCount;
        int hotRetries = 0;
        float observeUntil = Time.realtimeSinceStartup + 1.25f;
        while (Time.realtimeSinceStartup < observeUntil)
        {
            if (brain.bestAction != null
                && brain.bestAction.HasStarted
                && ReferenceEquals(brain.bestAction.destination, primary))
            {
                hotRetries++;
            }
            yield return null;
        }
        Check(hotRetries == 0, "NOPATH_NO_HOT_RETRY_SAME_TOPOLOGY",
            $"samples={hotRetries}; actionStarts={hotStartsBefore}->{brain.RuntimeActionStartCount}");

        RemoveWall(lower);
        RemoveWall(upper);
        DestroyFacility(primary);
        yield return ResetBetweenScenarios();
    }

    private IEnumerator RunFacilityDestructionMatrix()
    {
        foreach (FacilityFaultPhase phase in Enum.GetValues(typeof(FacilityFaultPhase)))
        {
            string row = "facility-shared:" + phase.ToString().ToLowerInvariant();
            if (!ShouldRun(row))
                continue;
            BeginScenario(row);
            yield return RunFacilityDestructionScenario(
                phase,
                "FACILITY_DESTROY_" + phase.ToString().ToUpperInvariant());
            CompleteScenario(row);
        }
    }

    private IEnumerator RunActionSpecificFacilityDestructionMatrix()
    {
        FacilityActionScenario[] scenarios =
        {
            new FacilityActionScenario("EAT", FacilityRole.Meal, CharacterCondition.HUNGER,
                action => action?.actionset is AIEat),
            new FacilityActionScenario("TOILET", FacilityRole.Toilet, CharacterCondition.EXCRETION,
                action => action?.actionset is AIFacilityRoleAction roleAction
                    && roleAction.Role == FacilityRole.Toilet),
            new FacilityActionScenario("HYGIENE", FacilityRole.Hygiene, CharacterCondition.HYGIENE,
                action => action?.actionset is AIFacilityRoleAction roleAction
                    && roleAction.Role == FacilityRole.Hygiene),
            new FacilityActionScenario("RECREATION", FacilityRole.Entertainment, CharacterCondition.FUN,
                action => action?.actionset is AIFacilityRoleAction roleAction
                    && roleAction.Role == FacilityRole.Entertainment),
            new FacilityActionScenario("SHOPPING", FacilityRole.Training, null,
                action => action?.actionset is AIShopping)
        };

        foreach (FacilityActionScenario scenario in scenarios)
        {
            AIAction authoredAction = originalActions?.FirstOrDefault(scenario.Match);
            if (authoredAction == null && scenario.Label == "SHOPPING")
            {
                authoredAction = CreateProductionAction<AIShopping>(
                    "SO/AI/Action/Shopping",
                    CharacterAiBranch.Shopping);
            }
            Check(authoredAction != null,
                scenario.Label + "_AUTHORED_ACTION",
                Describe(authoredAction));
            if (authoredAction == null)
            {
                continue;
            }

            foreach (FacilityFaultPhase phase in Enum.GetValues(typeof(FacilityFaultPhase)))
            {
                string row = "facility-action:"
                    + scenario.Label.ToLowerInvariant() + ":"
                    + phase.ToString().ToLowerInvariant();
                if (!ShouldRun(row))
                    continue;
                BeginScenario(row);
                yield return RunActionSpecificFacilityDestructionScenario(
                    scenario,
                    authoredAction,
                    phase);
                CompleteScenario(row);
            }
        }
    }

    private AIAction CreateProductionAction<T>(
        string resourcePath,
        CharacterAiBranch branch)
        where T : AIActionSet
    {
        AIActionSet actionSet = actionAssetCatalog.GetRequiredAction(
            resourcePath,
            branch);
        if (!(actionSet is T))
        {
            throw new InvalidOperationException(
                $"Production action {resourcePath} is {actionSet?.GetType().Name}; "
                + $"expected {typeof(T).Name}.");
        }
        AIAction action = new AIAction { actionset = actionSet };
        action.BindClock(gameClock);
        return action;
    }

    private IEnumerator RunActionSpecificFacilityDestructionScenario(
        FacilityActionScenario scenario,
        AIAction authoredAction,
        FacilityFaultPhase phase)
    {
        string prefix = scenario.Label + "_DESTROY_" + phase.ToString().ToUpperInvariant();
        BuildableObject primary = CreateFacility(
            corridor.Primary,
            prefix + "_primary",
            scenario.Role,
            phase == FacilityFaultPhase.Interaction ? 2f : 0.6f);
        Check(primary != null, prefix + "_PRIMARY_CREATED", Describe(primary));
        if (primary == null)
        {
            yield break;
        }

        WorldItemStackSnapshot mealSeed = null;
        try
        {
            if (scenario.Role == FacilityRole.Meal)
            {
                mealSeed = SpawnTemporaryMealBufferStack(
                    primary,
                    "food:preserved-ration",
                    1);
                Check(mealSeed != null,
                    prefix + "_MEAL_BUFFER_SEEDED",
                    mealSeed?.StackId ?? "missing");
                if (mealSeed == null)
                    yield break;
            }

            if (phase == FacilityFaultPhase.Queue)
            {
                bool held = primary.TryBeginUse(queueHolder.BuildingVisitor, out string holdFailure);
                Check(held, prefix + "_HOLDER_ADMITTED", Compact(holdFailure));
                if (authoredAction.actionset is AIShopping)
                {
                    bool immediateAdmission = primary.CanVisit(
                        subject.BuildingVisitor,
                        out string admissionFailure);
                    bool queueEligible = CharacterVisitPolicy.CanVisitBuilding(
                        subject,
                        primary,
                        alreadyVisited: false,
                        out string queueFailure);
                    Check(!immediateAdmission && queueEligible,
                        prefix + "_QUEUE_CAPACITY_DISTINGUISHED",
                        $"immediate={immediateAdmission}:{Compact(admissionFailure)}; "
                        + $"queue={queueEligible}:{Compact(queueFailure)}");
                }
            }

            bool synchronousEatApproach = phase == FacilityFaultPhase.Approach
                && authoredAction.actionset is AIEat;
            bool phaseReachedDuringStart = false;
            bool faultInjectedDuringStart = false;
            long failuresBefore = -1;
            long replansBefore = -1;
            long protectedReplansBefore = -1;
            int immediateDecisionsBefore = -1;
            Action onStarted = null;
            if (synchronousEatApproach)
            {
                onStarted = () =>
                {
                    // Eat may collapse this two-cell movement synchronously.
                    // Its observable fault boundary is therefore arrival with
                    // the action/MealPlan active but before Facility.TryBeginUse
                    // admits the actor, not a guaranteed intermediate position.
                    phaseReachedDuringStart = IsRunningAction(
                            authoredAction,
                            primary)
                        && primary.CurrentUserCount == 0;
                    if (!phaseReachedDuringStart)
                        return;

                    failuresBefore = brain.RuntimeExecutionFailureCount;
                    replansBefore = brain.RuntimeImmediateReplanCount;
                    protectedReplansBefore =
                        brain.RuntimeProtectedRunningActionReplanCount;
                    immediateDecisionsBefore =
                        brain.ImmediateDecisionRequestCount;
                    primary.DestroySelf();
                    faultInjectedDuringStart = true;
                };
            }

            yield return PrepareFacilityAction(
                scenario,
                authoredAction,
                primary,
                prefix,
                onStarted);
            if (synchronousEatApproach && !phaseReachedDuringStart)
            {
                Check(false, prefix + "_PHASE_REACHED",
                    $"eat pre-admission/arrival boundary missed; action={Describe(brain.bestAction)}; "
                    + $"position={subject.GetNowXY()}; target={primary.centerPos}");
                DestroyFacility(primary);
                yield return ResetBetweenScenarios();
                yield break;
            }
            if (!faultInjectedDuringStart
                && !IsRunningAction(authoredAction, primary))
            {
                DestroyFacility(primary);
                yield return ResetBetweenScenarios();
                yield break;
            }
            bool phaseReached = faultInjectedDuringStart
                ? phaseReachedDuringStart
                : HasReachedFaultPhase(primary, phase);
            float phaseDeadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
            while (!faultInjectedDuringStart
                && !phaseReached
                && Time.realtimeSinceStartup < phaseDeadline)
            {
                yield return null;
                phaseReached = HasReachedFaultPhase(primary, phase);
            }
            Check(phaseReached, prefix + "_PHASE_REACHED",
                $"authority={(synchronousEatApproach ? "eat-pre-admission/arrival" : "facility-approach/queue/use")}; "
                + $"position={subject.GetNowXY()}; target={primary.centerPos}; "
                + $"branch={brain.bestAction?.actionset?.Branch}; phase={brain.CurrentActionPhase}; "
                + $"users={primary.CurrentUserCount}; reservations={primary.ActiveVisitReservationCount}; "
                + $"waiting={primary.WaitingVisitReservationCount}");

            if (!faultInjectedDuringStart)
            {
                failuresBefore = brain.RuntimeExecutionFailureCount;
                replansBefore = brain.RuntimeImmediateReplanCount;
                protectedReplansBefore =
                    brain.RuntimeProtectedRunningActionReplanCount;
                immediateDecisionsBefore =
                    brain.ImmediateDecisionRequestCount;
                primary.DestroySelf();
            }
            Check(primary.CurrentUserCount == 0
                    && primary.ActiveVisitReservationCount == 0
                    && primary.WaitingVisitReservationCount == 0
                    && primary.WorkerReservation == null,
                prefix + "_CLEANUP",
                $"users={primary.CurrentUserCount}; reservations={primary.ActiveVisitReservationCount}; "
                + $"waiting={primary.WaitingVisitReservationCount}; worker={primary.WorkerReservation}");

            float failureDeadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
            while (brain.RuntimeExecutionFailureCount == failuresBefore
                && Time.realtimeSinceStartup < failureDeadline)
            {
                yield return null;
            }
            Check(brain.RuntimeExecutionFailureCount == failuresBefore + 1,
                prefix + "_TYPED_FAILURE_ONCE",
                $"failures={failuresBefore}->{brain.RuntimeExecutionFailureCount}; {brain.LastActionFailure}");
            Check(brain.LastActionFailure.Kind == AIActionFailureKind.Destroyed,
                prefix + "_DESTROYED_KIND",
                brain.LastActionFailure.ToString());
            bool directReplan =
                brain.RuntimeImmediateReplanCount >= replansBefore + 1;
            bool protectedReplanWake =
                brain.RuntimeProtectedRunningActionReplanCount
                    >= protectedReplansBefore + 1
                && brain.ImmediateDecisionRequestCount
                    >= immediateDecisionsBefore + 1;
            Check(directReplan || protectedReplanWake,
                prefix + "_IMMEDIATE_REPLAN",
                $"route={(directReplan ? "direct" : protectedReplanWake ? "protected-wake" : "none")}; "
                + $"replans={replansBefore}->{brain.RuntimeImmediateReplanCount}; "
                + $"protected={protectedReplansBefore}->{brain.RuntimeProtectedRunningActionReplanCount}; "
                + $"decisions={immediateDecisionsBefore}->{brain.ImmediateDecisionRequestCount}");
            if (mealSeed != null)
            {
                Check(itemReservations.GetReservedQuantity(
                            (ItemStackId)mealSeed.StackId) == 0,
                    prefix + "_MEAL_LEASE_CLEAN",
                    $"reserved={itemReservations.GetReservedQuantity((ItemStackId)mealSeed.StackId)}");
            }

            DestroyFacility(primary);
            yield return ResetBetweenScenarios();
        }
        finally
        {
            if (mealSeed != null)
            {
                scope.Container.Resolve<ICharacterMealOperationCancellation>()
                    .CancelActiveMealOperations(
                        subject,
                        "fault-verifier-meal-fixture-cleanup");
                RemoveTemporaryStack(mealSeed.StackId);
            }
            DestroyFacility(primary);
        }
    }

    private IEnumerator PrepareFacilityAction(
        FacilityActionScenario scenario,
        AIAction authoredAction,
        BuildableObject destination,
        string prefix,
        Action onStarted = null)
    {
        NeutralizeSubjectAndEndPrimitiveFallback();
        brain.StopCurrentActionForReplan("fault-verifier-reset-" + prefix);
        move.CancelActiveMovement();
        subject.transform.position = grid.GetWorldPos(corridor.Start);
        brain.availableActions = new[] { authoredAction };

        float publicationDeadline = Time.realtimeSinceStartup + ActionStartTimeoutRealtime;
        while (Time.realtimeSinceStartup < publicationDeadline
            && !facilityCandidates.GetCandidates(grid, scenario.Role)
                .Any(candidate => ReferenceEquals(candidate, destination)))
        {
            facilityCandidates.AdvanceIndex(1.0);
            yield return null;
        }
        Check(facilityCandidates.GetCandidates(grid, scenario.Role)
                .Any(candidate => ReferenceEquals(candidate, destination)),
            prefix + "_PUBLISHED",
            Describe(destination));

        SetAllNeedsNeutral();
        if (authoredAction.actionset is AIShopping)
        {
            if (subject.TryGetAbility(out AbilityWork work))
            {
                work.SetDutyState(AbilityWork.DutyState.OffDuty);
            }
            if (subject.Stats.Stats.ContainsKey(CharacterCondition.FUN))
            {
                subject.Stats.Stats[CharacterCondition.FUN] = 0f;
            }
            subject.GetAbility<AbilityShopping>()?.BeginOffDutyVisitCycle();
        }
        if (scenario.NeedCondition.HasValue
            && subject.Stats.Stats.ContainsKey(scenario.NeedCondition.Value))
        {
            subject.Stats.Stats[scenario.NeedCondition.Value] = 0f;
        }
        PreferFacilityAction(authoredAction.actionset);
        brain.RequestImmediateReplan(clearFailures: true);
        bool shoppingQueue = authoredAction.actionset is AIShopping
            && destination.CurrentUserCount >= destination.EffectiveCapacity;
        float deadline = Time.realtimeSinceStartup
            + (shoppingQueue ? ScenarioTimeoutRealtime : ActionStartTimeoutRealtime);
        string productionDetail = string.Empty;
        int deferredRetries = 0;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (IsRunningAction(authoredAction, destination))
            {
                productionDetail = "scheduler action=" + Describe(brain.bestAction)
                    + "; phase=" + brain.CurrentActionPhase;
                Check(true,
                    prefix + "_PRODUCTION_PIPELINE_SELECTED",
                    productionDetail + $"; deferredRetries={deferredRetries}");
                Check(true, prefix + "_ACTION_STARTED", Describe(brain.bestAction));
                onStarted?.Invoke();
                yield break;
            }
            if (brain.LastActionFailure.Kind
                == AIActionFailureKind.PathSearchDeferred)
            {
                deferredRetries++;
            }
            yield return null;
        }
        Check(false,
            prefix + "_PRODUCTION_PIPELINE_SELECTED",
            productionDetail + $"; deferredRetries={deferredRetries}; "
            + $"lastFailure={brain.LastActionFailure}");
        Check(false, prefix + "_ACTION_STARTED",
            $"expected={authoredAction.actionset?.name}@{Describe(destination)}; "
            + $"actual={Describe(brain.bestAction)}; phase={brain.CurrentActionPhase}; "
            + $"failure={brain.LastActionFailure}");
    }

    private bool IsRunningAction(AIAction authoredAction, BuildableObject destination)
    {
        AIAction running = brain != null ? brain.bestAction : null;
        return running != null
            && authoredAction != null
            && ReferenceEquals(running.actionset, authoredAction.actionset)
            && running.HasStarted
            && ReferenceEquals(running.destination, destination);
    }

    private void PreferFacilityAction(AIActionSet actionSet)
    {
        switch (actionSet)
        {
            case AIEat:
                brain.PreferActionOnNextDecision<AIEat>(180f);
                break;
            case AIFacilityRoleAction:
                brain.PreferActionOnNextDecision<AIFacilityRoleAction>(180f);
                break;
            case AIShopping:
                brain.PreferActionOnNextDecision<AIShopping>(180f);
                break;
        }
    }

    private IEnumerator RunDeprivationBreakdownTerminalMatrix()
    {
        ICharacterDeprivationCommand deprivation = deprivationCommands;
        BreakdownScenario[] scenarios =
        {
            new BreakdownScenario("RELIEF", CharacterBreakdownKind.DesperateRelief),
            new BreakdownScenario("DRINK", CharacterBreakdownKind.DesperateDrink),
            new BreakdownScenario("EAT", CharacterBreakdownKind.DesperateEat),
            new BreakdownScenario("COLLAPSE", CharacterBreakdownKind.Collapse),
            new BreakdownScenario("VIOLENT", CharacterBreakdownKind.ViolentImpulse)
        };

        foreach (BreakdownScenario scenario in scenarios)
        {
            string row = "deprivation:" + scenario.Label.ToLowerInvariant();
            if (!ShouldRun(row))
                continue;
            BeginScenario(row);
            string prefix = "DEPRIVATION_" + scenario.Label;
            Check(deprivation != null,
                prefix + "_PRODUCTION_COMMAND",
                deprivation != null ? deprivation.GetType().Name : "missing");
            if (deprivation == null)
            {
                CompleteScenario(row);
                continue;
            }

            NeutralizeSubjectAndEndPrimitiveFallback();
            deprivation.DebugResetForDeterministicScenario(subject);
            brain.availableActions = Array.Empty<AIAction>();
            bool forced = deprivation.DebugForceBreakdown(subject, scenario.Kind);
            Check(forced, prefix + "_FORCED", scenario.Kind.ToString());

            long externalEpochBefore = brain.ExternalIntentEpoch;
            int externalTransitionsBefore = brain.ExternalIntentTransitionCount;
            CharacterAiDecisionTickResult pipelineResult = brain
                .RequireDecisionPipeline()
                .RunDeprivationBreakdown(subject);
            Check(pipelineResult.Handled,
                prefix + "_PRODUCTION_PIPELINE_SELECTED",
                pipelineResult.Status);
            float startDeadline = Time.realtimeSinceStartup + ActionStartTimeoutRealtime;
            while (Time.realtimeSinceStartup < startDeadline
                && !brain.IsExternallyDrivenActionActive)
            {
                yield return null;
            }
            Check(brain.IsExternallyDrivenActionActive,
                prefix + "_PIPELINE_STARTED",
                $"branch={pipelineResult.Branch}; external={brain.IsExternallyDrivenActionActive}; "
                + $"owner={brain.ExternalIntentOwnerId}; epoch={brain.ExternalIntentEpoch}");

            deprivation.DebugClearBreakdown(subject);
            float terminalDeadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
            while (Time.realtimeSinceStartup < terminalDeadline
                && brain.IsExternallyDrivenActionActive)
            {
                yield return null;
            }
            Check(!brain.IsExternallyDrivenActionActive,
                prefix + "_LEASE_RELEASED",
                $"external={brain.IsExternallyDrivenActionActive}; owner={brain.ExternalIntentOwnerId}");
            Check(brain.ExternalIntentEpoch == externalEpochBefore + 1
                    && brain.ExternalIntentTransitionCount == externalTransitionsBefore + 1,
                prefix + "_TERMINAL_ONCE",
                $"externalEpoch={externalEpochBefore}->{brain.ExternalIntentEpoch}; "
                + $"transitions={externalTransitionsBefore}->{brain.ExternalIntentTransitionCount}");
            deprivation.DebugResetForDeterministicScenario(subject);
            yield return ResetBetweenScenarios();
            CompleteScenario(row);
        }
    }

    private IEnumerator RunDestinationlessDeferredRecoveryMatrix()
    {
        AIAction[] destinationlessActions = BuildVisitorDestinationlessActions(
            originalActions);

        DestinationlessScenario[] scenarios =
        {
            new DestinationlessScenario("LOOK_AROUND",
                action => action?.actionset is AILookAround,
                PrepareLookAroundState,
                recoveryTerminatesNoPath: false,
                requiresVisitorProjection: false),
            new DestinationlessScenario("WAIT",
                action => action?.actionset is AIWait,
                null,
                recoveryTerminatesNoPath: false,
                requiresVisitorProjection: false),
            new DestinationlessScenario("EXIT_DUNGEON",
                action => action?.actionset is AIExitDungeon,
                PrepareExitDungeonState,
                recoveryTerminatesNoPath: true,
                requiresVisitorProjection: true)
        };

        foreach (DestinationlessScenario scenario in scenarios)
        {
            AIAction authoredAction = destinationlessActions.FirstOrDefault(scenario.Match);
            Check(authoredAction != null,
                scenario.Label + "_AUTHORED_ACTION",
                Describe(authoredAction));
            if (authoredAction == null)
            {
                continue;
            }
            string recoveryRow = "destinationless:"
                + scenario.Label.ToLowerInvariant() + ":recovery";
            if (ShouldRun(recoveryRow))
            {
                BeginScenario(recoveryRow);
                yield return RunDestinationlessDeferredScenario(
                    scenario,
                    authoredAction,
                    deferCount: 3,
                    expectStarvation: false);
                CompleteScenario(recoveryRow);
            }
            string starvationRow = "destinationless:"
                + scenario.Label.ToLowerInvariant() + ":starvation";
            if (ShouldRun(starvationRow))
            {
                BeginScenario(starvationRow);
                yield return RunDestinationlessDeferredScenario(
                    scenario,
                    authoredAction,
                    deferCount: int.MaxValue,
                    expectStarvation: true);
                CompleteScenario(starvationRow);
            }
        }
    }

    private void PrepareLookAroundState()
    {
        AbilityShopping shopping = subject.GetAbility<AbilityShopping>();
        shopping?.RestorePersistentState(1, 0, 0);
        shopping?.RecordVisitableFacilitySearchResult(false);
    }

    private void PrepareExitDungeonState()
    {
        AbilityShopping shopping = subject.GetAbility<AbilityShopping>();
        shopping?.RestorePersistentState(0, 1, 0);
        shopping?.RecordVisitableFacilitySearchResult(false);
    }

    private AIAction[] BuildVisitorDestinationlessActions(AIAction[] configured)
    {
        List<AIAction> actions = configured != null
            ? configured.Where(action => action?.actionset != null).ToList()
            : new List<AIAction>();
        AddRequiredDestinationlessAction<AILookAround>(
            actions,
            "SO/AI/Action/LookAround",
            CharacterAiBranch.LookAround);
        AddRequiredDestinationlessAction<AIWait>(
            actions,
            "SO/AI/Action/Wait",
            CharacterAiBranch.Wait);
        AddRequiredDestinationlessAction<AIExitDungeon>(
            actions,
            "SO/AI/Action/ExitDungeon",
            CharacterAiBranch.ExitDungeon);
        foreach (AIAction action in actions)
            action.BindClock(gameClock);
        return actions.ToArray();
    }

    private void AddRequiredDestinationlessAction<T>(
        ICollection<AIAction> actions,
        string resourcePath,
        CharacterAiBranch branch)
        where T : AIActionSet
    {
        if (actions.Any(action => action?.actionset is T))
            return;
        AIActionSet actionSet = actionAssetCatalog.GetRequiredAction(
            resourcePath,
            branch);
        if (!(actionSet is T))
        {
            throw new InvalidOperationException(
                $"Production action {resourcePath} is {actionSet?.GetType().Name}; "
                + $"expected {typeof(T).Name}.");
        }
        actions.Add(new AIAction { actionset = actionSet });
    }

    private IEnumerator RunPrimitiveSurvivalFaultMatrix()
    {
        if (ShouldRun("primitive:field-meal:commit-loss"))
        {
            const string row = "primitive:field-meal:commit-loss";
            BeginScenario(row);
            yield return RunPrimitiveItemLossScenario(
                CharacterCondition.HUNGER,
                "survival:field-meal",
                "food:preserved-ration",
                action => action?.actionset is AIPrimitiveFieldMeal,
                sourceDistance: 1,
                prefix: "PRIMITIVE_FIELD_MEAL_COMMIT_LOSS");
            CompleteScenario(row);
        }
        if (ShouldRun("primitive:bucket-wash:path-invalidated"))
        {
            const string row = "primitive:bucket-wash:path-invalidated";
            BeginScenario(row);
            yield return RunPrimitiveBucketWashPathInvalidation();
            CompleteScenario(row);
        }
        if (ShouldRun("primitive:bucket-wash:target-lost"))
        {
            const string row = "primitive:bucket-wash:target-lost";
            BeginScenario(row);
            yield return RunPrimitiveItemLossScenario(
                CharacterCondition.HYGIENE,
                "survival:bucket-wash",
                "resource:clean-water",
                action => action?.actionset is AIPrimitiveBucketWash,
                sourceDistance: 1,
                seedAtActorStart: true,
                prefix: "PRIMITIVE_BUCKET_WASH_TARGET_LOST");
            CompleteScenario(row);
        }
        if (ShouldRun("primitive:latrine:path-invalidated"))
        {
            const string row = "primitive:latrine:path-invalidated";
            BeginScenario(row);
            yield return RunPrimitiveLatrineInvalidation(targetInvalidation: false);
            CompleteScenario(row);
        }
        if (ShouldRun("primitive:latrine:target-invalidated"))
        {
            const string row = "primitive:latrine:target-invalidated";
            BeginScenario(row);
            yield return RunPrimitiveLatrineInvalidation(targetInvalidation: true);
            CompleteScenario(row);
        }
        if (ShouldRun("primitive:floor-rest:interrupted"))
        {
            const string row = "primitive:floor-rest:interrupted";
            BeginScenario(row);
            yield return RunPrimitiveFloorRestInterruption();
            CompleteScenario(row);
        }
    }

    private IEnumerator RunPrimitiveItemLossScenario(
        CharacterCondition condition,
        string actionId,
        string itemId,
        Func<AIAction, bool> actionMatch,
        int sourceDistance,
        string prefix,
        bool seedAtActorStart = false)
    {
        Vector2Int sourcePosition = seedAtActorStart
            ? corridor.Start
            : corridor.Primary;
        WorldItemStackSnapshot seeded = SpawnTemporaryStack(
            itemId,
            sourcePosition,
            1);
        Check(seeded != null, prefix + "_SOURCE_SEEDED", seeded?.StackId ?? "missing");
        if (seeded == null)
            yield break;

        int completedBefore = GetPrimitiveCount(primitiveCompletedCounts, actionId);
        yield return StartPrimitivePipeline(condition, actionId, actionMatch, prefix);
        float needBefore = GetNeedValue(condition);
        if (!brain.IsExternallyDrivenActionActive)
        {
            RemoveTemporaryStack(seeded.StackId);
            yield return ResetBetweenScenarios();
            yield break;
        }

        if (seedAtActorStart)
        {
            // The external runner is acquired synchronously; allow its
            // production coroutine one frame to capture the unique same-cell
            // source before invalidating that exact stack.
            yield return null;
        }

        float approachDeadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
        while (Time.realtimeSinceStartup < approachDeadline
            && Manhattan(subject.GetNowXY(), seeded.Position) > sourceDistance
            && brain.IsExternallyDrivenActionActive)
        {
            yield return null;
        }
        Check(Manhattan(subject.GetNowXY(), seeded.Position) <= sourceDistance,
            prefix + "_APPROACHED_SOURCE",
            $"actor={subject.GetNowXY()}; source={seeded.Position}");
        RemoveTemporaryStack(seeded.StackId);

        yield return WaitForPrimitiveTerminal(prefix);
        Check(GetPrimitiveCount(primitiveCompletedCounts, actionId) == completedBefore,
            prefix + "_NO_LATE_COMPLETION",
            $"completed={completedBefore}->{GetPrimitiveCount(primitiveCompletedCounts, actionId)}");
        Check(GetNeedValue(condition) <= needBefore + 0.01f,
            prefix + "_NO_RECOVERY_AFTER_LOSS",
            $"need={needBefore:0.###}->{GetNeedValue(condition):0.###}");
        Check(itemReservations.GetReservedQuantity((ItemStackId)seeded.StackId) == 0,
            prefix + "_RESERVATION_CLEAN",
            $"reserved={itemReservations.GetReservedQuantity((ItemStackId)seeded.StackId)}");
        yield return ResetBetweenScenarios();
    }

    private IEnumerator RunPrimitiveBucketWashPathInvalidation()
    {
        const string prefix = "PRIMITIVE_BUCKET_WASH_PATH_INVALIDATED";
        const string actionId = "survival:bucket-wash";
        WorldItemStackSnapshot seeded = SpawnTemporaryStack(
            "resource:clean-water",
            corridor.Primary,
            1);
        Check(seeded != null, prefix + "_SOURCE_SEEDED", seeded?.StackId ?? "missing");
        if (seeded == null)
            yield break;
        int completedBefore = GetPrimitiveCount(primitiveCompletedCounts, actionId);
        yield return StartPrimitivePipeline(
            CharacterCondition.HYGIENE,
            actionId,
            action => action?.actionset is AIPrimitiveBucketWash,
            prefix);
        FaultWallOccupant lower = AddWall(corridor.LowerBlock, prefix + "-lower");
        FaultWallOccupant upper = AddWall(corridor.UpperBlock, prefix + "-upper");
        gridSystem.NotifyGridObjectChanged();
        yield return WaitForPrimitiveTerminal(prefix);
        Check(GetPrimitiveCount(primitiveCompletedCounts, actionId) == completedBefore,
            prefix + "_NO_COMPLETION",
            $"completed={completedBefore}->{GetPrimitiveCount(primitiveCompletedCounts, actionId)}");
        Check(move.LastGridMoveFailureReason != GridMoveFailureReason.None,
            prefix + "_MOVEMENT_TERMINAL",
            move.LastGridMoveFailureReason.ToString());
        Check(itemReservations.GetReservedQuantity((ItemStackId)seeded.StackId) == 0,
            prefix + "_RESERVATION_CLEAN",
            $"reserved={itemReservations.GetReservedQuantity((ItemStackId)seeded.StackId)}");
        RemoveWall(lower);
        RemoveWall(upper);
        RemoveTemporaryStack(seeded.StackId);
        yield return ResetBetweenScenarios();
    }

    private IEnumerator RunPrimitiveLatrineInvalidation(bool targetInvalidation)
    {
        string prefix = targetInvalidation
            ? "PRIMITIVE_LATRINE_TARGET_INVALIDATED"
            : "PRIMITIVE_LATRINE_PATH_INVALIDATED";
        const string actionId = "survival:primitive-latrine";
        SetTemporaryAreaType(corridor.Primary, GridCellAreaType.ExteriorPath);
        int completedBefore = GetPrimitiveCount(primitiveCompletedCounts, actionId);
        yield return StartPrimitivePipeline(
            CharacterCondition.EXCRETION,
            actionId,
            action => action?.actionset is AIPrimitiveLatrine,
            prefix);
        float needBefore = GetNeedValue(CharacterCondition.EXCRETION);

        FaultWallOccupant lower = null;
        FaultWallOccupant upper = null;
        if (targetInvalidation)
        {
            float deadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
            while (Time.realtimeSinceStartup < deadline
                && subject.GetNowXY() != corridor.Primary
                && brain.IsExternallyDrivenActionActive)
                yield return null;
            Check(subject.GetNowXY() == corridor.Primary,
                prefix + "_TARGET_REACHED",
                $"actor={subject.GetNowXY()}; target={corridor.Primary}");
            SetTemporaryAreaType(corridor.Primary, GridCellAreaType.BlockedExterior);
        }
        else
        {
            lower = AddWall(corridor.LowerBlock, prefix + "-lower");
            upper = AddWall(corridor.UpperBlock, prefix + "-upper");
            gridSystem.NotifyGridObjectChanged();
        }

        yield return WaitForPrimitiveTerminal(prefix);
        Check(GetPrimitiveCount(primitiveCompletedCounts, actionId) == completedBefore,
            prefix + "_NO_COMPLETION",
            $"completed={completedBefore}->{GetPrimitiveCount(primitiveCompletedCounts, actionId)}");
        Check(GetNeedValue(CharacterCondition.EXCRETION) <= needBefore + 0.01f,
            prefix + "_NO_RECOVERY",
            $"need={needBefore:0.###}->{GetNeedValue(CharacterCondition.EXCRETION):0.###}");
        RemoveWall(lower);
        RemoveWall(upper);
        RestoreTemporaryAreaType(corridor.Primary);
        yield return ResetBetweenScenarios();
    }

    private IEnumerator RunPrimitiveFloorRestInterruption()
    {
        const string prefix = "PRIMITIVE_FLOOR_REST_INTERRUPTED";
        const string actionId = "survival:floor-rest";
        int completedBefore = GetPrimitiveCount(primitiveCompletedCounts, actionId);
        yield return StartPrimitivePipeline(
            CharacterCondition.SLEEP,
            actionId,
            action => action?.actionset is AIPrimitiveFloorRest,
            prefix);
        float sleepBefore = GetNeedValue(CharacterCondition.SLEEP);
        string owner = brain.ExternalIntentOwnerId;
        bool interrupted = brain.EndExternallyDrivenAction(owner, clearFailures: false);
        Check(interrupted, prefix + "_INTERRUPT_ACCEPTED", $"owner={owner}");
        yield return WaitForPrimitiveTerminal(prefix);
        Check(GetPrimitiveCount(primitiveCompletedCounts, actionId) == completedBefore,
            prefix + "_NO_STALE_COMPLETION",
            $"completed={completedBefore}->{GetPrimitiveCount(primitiveCompletedCounts, actionId)}");
        Check(GetNeedValue(CharacterCondition.SLEEP) <= sleepBefore + 0.01f,
            prefix + "_NO_STALE_RECOVERY",
            $"sleep={sleepBefore:0.###}->{GetNeedValue(CharacterCondition.SLEEP):0.###}");
        yield return ResetBetweenScenarios();
    }

    private IEnumerator StartPrimitivePipeline(
        CharacterCondition condition,
        string actionId,
        Func<AIAction, bool> actionMatch,
        string prefix)
    {
        NeutralizeSubjectAndEndPrimitiveFallback();
        brain.StopCurrentActionForReplan("fault-verifier-" + prefix);
        move.CancelActiveMovement();
        subject.transform.position = grid.GetWorldPos(corridor.Start);
        SetNeedValue(condition, 0f);
        AIAction authored = originalActions?.FirstOrDefault(actionMatch);
        Check(authored != null, prefix + "_AUTHORED_ACTION", Describe(authored));
        if (authored == null)
            yield break;
        brain.availableActions = new[] { authored };
        PreferPrimitiveAction(authored.actionset);
        int startedBefore = GetPrimitiveCount(primitiveStartedCounts, actionId);
        brain.RequestImmediateReplan(clearFailures: true);
        float deadline = Time.realtimeSinceStartup + ActionStartTimeoutRealtime;
        while (Time.realtimeSinceStartup < deadline
            && (GetPrimitiveCount(primitiveStartedCounts, actionId) == startedBefore
                || !brain.IsExternallyDrivenActionActive))
            yield return null;
        Check(GetPrimitiveCount(primitiveStartedCounts, actionId) == startedBefore + 1
                && brain.IsExternallyDrivenActionActive,
            prefix + "_PRODUCTION_RUNNER_STARTED",
            $"started={startedBefore}->{GetPrimitiveCount(primitiveStartedCounts, actionId)}; "
            + $"external={brain.IsExternallyDrivenActionActive}; owner={brain.ExternalIntentOwnerId}");
    }

    private IEnumerator WaitForPrimitiveTerminal(string prefix)
    {
        float deadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
        while (brain.IsExternallyDrivenActionActive
            && Time.realtimeSinceStartup < deadline)
            yield return null;
        Check(!brain.IsExternallyDrivenActionActive,
            prefix + "_TERMINAL_BOUNDED",
            $"external={brain.IsExternallyDrivenActionActive}; owner={brain.ExternalIntentOwnerId}");
    }

    private IEnumerator RunDestinationlessDeferredScenario(
        DestinationlessScenario scenario,
        AIAction authoredAction,
        int deferCount,
        bool expectStarvation)
    {
        string prefix = scenario.Label
            + (expectStarvation ? "_STARVATION" : "_DEFERRED_RECOVERY");
        NeutralizeSubjectAndEndPrimitiveFallback();
        brain.StopCurrentActionForReplan("fault-verifier-" + prefix);
        move.CancelActiveMovement();
        subject.transform.position = grid.GetWorldPos(corridor.Start);
        brain.availableActions = new[] { authoredAction };
        IDisposable visitorProjection = scenario.RequiresVisitorProjection
            ? CharacterWorkRoleUtility.DebugProjectAsVisitor(subject)
            : null;
        if (subject.TryGetAbility(out AbilityWork work))
        {
            work.SetDutyState(AbilityWork.DutyState.OffDuty);
        }
        scenario.Prepare?.Invoke();
        if (scenario.RequiresVisitorProjection)
        {
            bool projectedWorker = CharacterWorkRoleUtility.TryGetWork(
                subject,
                out _);
            AbilityShopping shopping = subject.GetAbility<AbilityShopping>();
            bool shouldExit = shopping?.ShouldExitDungeon() == true;
            bool canStart = authoredAction.actionset.CanStart(subject);
            Check(!projectedWorker && shouldExit && canStart,
                prefix + "_EXIT_PREREQUISITES",
                $"worker={projectedWorker}; shouldExit={shouldExit}; "
                + $"canStart={canStart}; visitCount={shopping?.visitCount}; "
                + $"lookAroundCount={shopping?.lookAroundCount}");
        }

        IGridPathSearchBroker productionBroker = subject.PathSearchBroker;
        DeferredPathSearchBroker faultBroker = new(
            productionBroker,
            deferCount,
            scenario.RecoveryTerminatesNoPath && !expectStarvation);
        IGridPathSearchBroker previousBroker = move.DebugReplacePathSearchBroker(faultBroker);
        int previousDeferralLimit = move.PathSearchDeferralLimitForDiagnostics;
        if (expectStarvation)
        {
            Check(previousDeferralLimit == ProductionPathSearchDeferralLimit,
                prefix + "_PRODUCTION_DEFERRAL_LIMIT",
                previousDeferralLimit.ToString());
            move.DebugReplacePathSearchDeferralLimit(
                StarvationFixtureDeferralLimit);
        }
        long startsBefore = brain.RuntimeActionStartCount;
        CharacterAiRuntimeGateSnapshot gateBefore = brain.CaptureRuntimeGateSnapshot();
        try
        {
            PreferDestinationlessAction(authoredAction.actionset);
            brain.RequestImmediateReplan(clearFailures: true);
            float startDeadline = Time.realtimeSinceStartup + ActionStartTimeoutRealtime;
            bool productionStarted = false;
            string productionDetail = string.Empty;
            while (Time.realtimeSinceStartup < startDeadline)
            {
                if (ReferenceEquals(
                        brain.bestAction?.actionset,
                        authoredAction.actionset)
                    && brain.bestAction.HasStarted)
                {
                    productionStarted = true;
                    productionDetail = "scheduler action="
                        + Describe(brain.bestAction)
                        + "; phase=" + brain.CurrentActionPhase;
                    break;
                }
                yield return null;
            }
            Check(productionStarted,
                prefix + "_PRODUCTION_PIPELINE_SELECTED",
                productionDetail);
            Check(ReferenceEquals(brain.bestAction?.actionset, authoredAction.actionset)
                    && brain.bestAction.HasStarted,
                prefix + "_PIPELINE_STARTED",
                $"action={Describe(brain.bestAction)}; phase={brain.CurrentActionPhase}");
            if (!productionStarted
                || !ReferenceEquals(brain.bestAction?.actionset, authoredAction.actionset)
                || !brain.bestAction.HasStarted)
            {
                yield break;
            }

            AIAction committedAction = brain.bestAction;
            // This row owns one action epoch.  Removing future candidates after
            // the production pipeline committed the action prevents the live BT
            // from starting a second identical Wait/LookAround while the row is
            // observing the first terminal.
            brain.availableActions = Array.Empty<AIAction>();
            bool switchedBeforeTerminal = false;
            float deadline = Time.realtimeSinceStartup
                + (expectStarvation
                    ? StarvationScenarioTimeoutRealtime
                    : ScenarioTimeoutRealtime);
            int frameDeadline = expectStarvation
                ? Time.frameCount + StarvationScenarioMaximumFrames
                : int.MaxValue;
            while (Time.realtimeSinceStartup < deadline
                   && Time.frameCount < frameDeadline)
            {
                CharacterAiRuntimeGateSnapshot current = brain.CaptureRuntimeGateSnapshot();
                if (current.ActionTerminals == gateBefore.ActionTerminals
                    && brain.bestAction != null
                    && !ReferenceEquals(brain.bestAction, committedAction))
                {
                    switchedBeforeTerminal = true;
                }
                if (expectStarvation)
                {
                    if (brain.LastActionFailure.Kind == AIActionFailureKind.PathSearchStarved
                        && current.ActionTerminals > gateBefore.ActionTerminals)
                    {
                        break;
                    }
                }
                else if (faultBroker.DeferredCalls >= deferCount
                    && current.ActionTerminals > gateBefore.ActionTerminals)
                {
                    break;
                }
                yield return null;
            }

            CharacterAiRuntimeGateSnapshot gateAfter = brain.CaptureRuntimeGateSnapshot();
            Check(brain.RuntimeActionStartCount == startsBefore + 1
                    && !switchedBeforeTerminal,
                prefix + "_SAME_ACTION_ONCE",
                $"starts={startsBefore}->{brain.RuntimeActionStartCount}; "
                + $"switchedBeforeTerminal={switchedBeforeTerminal}");
            Check(faultBroker.DeferredCalls >= (expectStarvation
                    ? StarvationFixtureDeferralLimit
                    : deferCount),
                prefix + "_DEFERRED_HEARTBEATS",
                $"deferredCalls={faultBroker.DeferredCalls}; retrySchedules="
                + $"{gateBefore.RetrySchedules}->{gateAfter.RetrySchedules}");
            if (expectStarvation)
            {
                Check(brain.LastActionFailure.Kind == AIActionFailureKind.PathSearchStarved,
                    prefix + "_TYPED_TERMINAL",
                    brain.LastActionFailure.ToString());
                Check(gateAfter.ActionFailed == gateBefore.ActionFailed + 1
                        && gateAfter.ActionTerminals == gateBefore.ActionTerminals + 1,
                    prefix + "_FAILED_ONCE",
                    $"failed={gateBefore.ActionFailed}->{gateAfter.ActionFailed}; "
                    + $"terminal={gateBefore.ActionTerminals}->{gateAfter.ActionTerminals}");
            }
            else
            {
                Check(brain.LastActionFailure.Kind != AIActionFailureKind.PathSearchStarved,
                    prefix + "_NO_TERMINAL_STARVATION",
                    brain.LastActionFailure.ToString());
                if (scenario.RecoveryTerminatesNoPath)
                {
                    Check(brain.LastActionFailure.Kind == AIActionFailureKind.NoPath
                            && gateAfter.ActionFailed == gateBefore.ActionFailed + 1,
                        prefix + "_TOPOLOGY_VERDICT_AFTER_RETRY",
                        $"failure={brain.LastActionFailure}; failed="
                        + $"{gateBefore.ActionFailed}->{gateAfter.ActionFailed}");
                }
                else
                {
                    Check(gateAfter.ActionCompleted == gateBefore.ActionCompleted + 1,
                        prefix + "_COMPLETED_AFTER_RETRY",
                        $"completed={gateBefore.ActionCompleted}->{gateAfter.ActionCompleted}");
                }
            }
        }
        finally
        {
            move.DebugReplacePathSearchDeferralLimit(previousDeferralLimit);
            move.DebugReplacePathSearchBroker(previousBroker);
            visitorProjection?.Dispose();
            if (subject.CurrentLifecycleState != CharacterLifecycleState.Active)
            {
                subject.SetLifecycleState(CharacterLifecycleState.Active);
            }
        }
        yield return ResetBetweenScenarios();
    }

    private void PreferDestinationlessAction(AIActionSet actionSet)
    {
        switch (actionSet)
        {
            case AILookAround:
                brain.PreferActionOnNextDecision<AILookAround>(180f);
                break;
            case AIWait:
                brain.PreferActionOnNextDecision<AIWait>(180f);
                break;
            case AIExitDungeon:
                brain.PreferActionOnNextDecision<AIExitDungeon>(180f);
                break;
        }
    }

    private IEnumerator RunThrowingDestructionSubscriberScenario()
    {
        const string prefix = "FACILITY_DESTROY_THROWING_SUBSCRIBER";
        BuildableObject facility = CreateRestFacility(
            corridor.Primary,
            "throwing-subscriber",
            useDuration: 0.5f);
        Check(facility != null, prefix + "_CREATED", Describe(facility));
        if (facility == null)
        {
            yield break;
        }

        bool admitted = facility.TryBeginUse(
            queueHolder.BuildingVisitor,
            out string admissionFailure);
        Check(admitted, prefix + "_OCCUPIED", Compact(admissionFailure));
        int laterSubscriberCalls = 0;
        facility.OnBuildingDestroyed += () =>
            throw new InvalidOperationException("qa-throwing-destruction-subscriber");
        facility.OnBuildingDestroyed += () => laterSubscriberCalls++;

        AggregateException captured = null;
        try
        {
            facility.DestroySelf();
        }
        catch (AggregateException exception)
        {
            captured = exception;
        }

        Check(captured?.InnerExceptions.Count == 1,
            prefix + "_FAILURE_REPORTED",
            captured?.ToString() ?? "missing AggregateException");
        Check(laterSubscriberCalls == 1,
            prefix + "_LATER_SUBSCRIBER_NOTIFIED",
            $"calls={laterSubscriberCalls}");
        Check(facility.isDestroy
                && facility.CurrentUserCount == 0
                && facility.ActiveVisitReservationCount == 0
                && facility.WaitingVisitReservationCount == 0
                && facility.WorkerReservation == null,
            prefix + "_CORE_TEARDOWN_COMPLETED",
            $"destroy={facility.isDestroy}; users={facility.CurrentUserCount}; "
            + $"reservations={facility.ActiveVisitReservationCount}; "
            + $"waiting={facility.WaitingVisitReservationCount}; "
            + $"worker={facility.WorkerReservation?.BuildingDisplayName}");

        // A repeated call is deliberately a no-op and must not republish the
        // event or throw the same subscriber failure again.
        Exception repeatedFailure = null;
        try
        {
            facility.DestroySelf();
        }
        catch (Exception exception)
        {
            repeatedFailure = exception;
        }
        Check(repeatedFailure == null && laterSubscriberCalls == 1,
            prefix + "_IDEMPOTENT",
            $"repeat={repeatedFailure}; calls={laterSubscriberCalls}");

        runtimeFacilities.Remove(facility);
        yield return ResetBetweenScenarios();
    }

    private IEnumerator RunFacilityDestructionScenario(
        FacilityFaultPhase phase,
        string checkPrefix)
    {
        BuildableObject primary = CreateRestFacility(
            corridor.Primary,
            phase + "-primary",
            useDuration: phase == FacilityFaultPhase.Interaction ? 2f : 0.6f);
        Check(primary != null,
            checkPrefix + "_PRIMARY_CREATED",
            $"primary={Describe(primary)}");
        if (primary == null)
        {
            yield break;
        }

        if (phase == FacilityFaultPhase.Queue)
        {
            bool held = primary.TryBeginUse(queueHolder.BuildingVisitor, out string holdFailure);
            Check(held, checkPrefix + "_HOLDER_ADMITTED", Compact(holdFailure));
        }

        yield return PrepareAction(primary, phase.ToString());
        float phaseDeadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
        while (!HasReachedFaultPhase(primary, phase)
            && Time.realtimeSinceStartup < phaseDeadline)
        {
            yield return null;
        }
        Check(HasReachedFaultPhase(primary, phase), checkPrefix + "_PHASE_REACHED",
            $"phase={brain.CurrentActionPhase}; users={primary.CurrentUserCount}; "
            + $"reservations={primary.ActiveVisitReservationCount}; waiting={primary.WaitingVisitReservationCount}");

        long failuresBefore = brain.RuntimeExecutionFailureCount;
        long replansBefore = brain.RuntimeImmediateReplanCount;
        long protectedReplansBefore =
            brain.RuntimeProtectedRunningActionReplanCount;
        int immediateDecisionsBefore = brain.ImmediateDecisionRequestCount;
        primary.DestroySelf();
        Check(primary.CurrentUserCount == 0
                && primary.ActiveVisitReservationCount == 0
                && primary.WaitingVisitReservationCount == 0
                && primary.WorkerReservation == null,
            checkPrefix + "_IMMEDIATE_OCCUPANCY_CLEANUP",
            $"users={primary.CurrentUserCount}; reservations={primary.ActiveVisitReservationCount}; "
            + $"waiting={primary.WaitingVisitReservationCount}; worker={primary.WorkerReservation?.BuildingDisplayName}");

        // The alternate is published after the target is destroyed, but before
        // the next scheduler decision. This prevents the scorer from avoiding
        // a deliberately full queue fixture while still proving live replanning.
        BuildableObject alternate = CreateRestFacility(
            corridor.Alternate,
            phase + "-alternate",
            useDuration: 0.4f);
        Check(alternate != null,
            checkPrefix + "_ALTERNATE_CREATED",
            Describe(alternate));

        AIActionFailure capturedFailure = AIActionFailure.None;
        float failureDeadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
        while (brain.RuntimeExecutionFailureCount == failuresBefore
            && Time.realtimeSinceStartup < failureDeadline)
        {
            yield return null;
        }
        if (brain.RuntimeExecutionFailureCount > failuresBefore)
        {
            capturedFailure = brain.LastActionFailure;
        }
        Check(brain.RuntimeExecutionFailureCount == failuresBefore + 1,
            checkPrefix + "_TYPED_FAILURE_ONCE",
            $"failures={failuresBefore}->{brain.RuntimeExecutionFailureCount}; {capturedFailure}");
        Check(capturedFailure.Kind == AIActionFailureKind.Destroyed,
            checkPrefix + "_DESTROYED_KIND", capturedFailure.ToString());
        bool directReplan =
            brain.RuntimeImmediateReplanCount >= replansBefore + 1;
        bool protectedReplanWake =
            brain.RuntimeProtectedRunningActionReplanCount
                >= protectedReplansBefore + 1
            && brain.ImmediateDecisionRequestCount
                >= immediateDecisionsBefore + 1;
        Check(directReplan || protectedReplanWake,
            checkPrefix + "_IMMEDIATE_REPLAN",
            $"route={(directReplan ? "direct" : protectedReplanWake ? "protected-wake" : "none")}; "
            + $"replans={replansBefore}->{brain.RuntimeImmediateReplanCount}; "
            + $"protected={protectedReplansBefore}->{brain.RuntimeProtectedRunningActionReplanCount}; "
            + $"decisions={immediateDecisionsBefore}->{brain.ImmediateDecisionRequestCount}");

        yield return AwaitAlternatePublication(alternate, checkPrefix);

        bool alternateSelected = false;
        float alternateDeadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
        while (Time.realtimeSinceStartup < alternateDeadline)
        {
            if (alternate != null
                && (alternate.FacilityState.completedUses > 0
                    || brain.bestAction?.actionset is AIRest
                && ReferenceEquals(brain.bestAction.destination, alternate)
                && brain.bestAction.HasStarted))
            {
                alternateSelected = true;
                break;
            }
            yield return null;
        }
        Check(alternateSelected, checkPrefix + "_ALTERNATE_REPLAN",
            $"action={Describe(brain.bestAction)}; phase={brain.CurrentActionPhase}");

        DestroyFacility(primary);
        DestroyFacility(alternate);
        yield return ResetBetweenScenarios();
    }

    private IEnumerator PrepareAction(BuildableObject expectedDestination, string label)
    {
        NeutralizeSubjectAndEndPrimitiveFallback();
        brain.StopCurrentActionForReplan("fault-verifier-reset-" + label);
        move.CancelActiveMovement();
        subject.transform.position = grid.GetWorldPos(corridor.Start);
        // Facility publication and the incremental candidate cache are frame
        // driven. Keep all primitive needs neutral until the authored Rest
        // facility is observable by the real decision pipeline.
        float publicationDeadline = Time.realtimeSinceStartup +
            ActionStartTimeoutRealtime;
        while (Time.realtimeSinceStartup < publicationDeadline)
        {
            IReadOnlyList<BuildableObject> candidates =
                facilityCandidates.GetCandidates(grid, FacilityRole.Rest);
            if (candidates.Any(candidate =>
                    ReferenceEquals(candidate, expectedDestination)))
            {
                break;
            }

            facilityCandidates.AdvanceIndex(1.0);
            yield return null;
        }
        Check(
            facilityCandidates.GetCandidates(grid, FacilityRole.Rest)
                .Any(candidate => ReferenceEquals(candidate, expectedDestination)),
            "FACILITY_PUBLISHED_" + label.ToUpperInvariant(),
            Describe(expectedDestination));
        SetNeutralNeedsWithUrgentSleep();
        brain.PreferActionOnNextDecision<AIRest>(180f);
        brain.RequestImmediateReplan(clearFailures: true);

        float deadline = Time.realtimeSinceStartup + ActionStartTimeoutRealtime;
        while (Time.realtimeSinceStartup < deadline)
        {
            AIAction action = brain.bestAction;
            if (action?.actionset is AIRest
                && action.HasStarted
                && ReferenceEquals(action.destination, expectedDestination)
                && (string.Equals(brain.CurrentActionPhase, "이동", StringComparison.Ordinal)
                    || subject.GetNowXY() != corridor.Start))
            {
                yield break;
            }
            yield return null;
        }
        Check(false, "ACTION_START_" + label.ToUpperInvariant(),
            $"expected={Describe(expectedDestination)}; actual={Describe(brain.bestAction)}; "
            + $"phase={brain.CurrentActionPhase}; failure={brain.LastActionFailure}");
    }

    private IEnumerator ResetBetweenScenarios()
    {
        NeutralizeSubjectAndEndPrimitiveFallback();
        brain.StopCurrentActionForReplan("fault-verifier-between-scenarios");
        move.CancelActiveMovement();
        subject.transform.position = grid.GetWorldPos(corridor.Start);
        yield return null;
        yield return null;
    }

    private IEnumerator AwaitAlternatePublication(
        BuildableObject alternate,
        string checkPrefix)
    {
        // Do not terminate or reprioritize a primitive action from the test.
        // The production facility index publishes the alternate; if primitive
        // floor-rest claimed the short destruction gap, its own periodic
        // authored-facility revalidation must yield the external intent and let
        // the normal BT choose this facility.
        float deadline = Time.realtimeSinceStartup + ActionStartTimeoutRealtime;
        bool published = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            published = alternate != null
                && facilityCandidates.GetCandidates(grid, FacilityRole.Rest)
                    .Any(candidate => ReferenceEquals(candidate, alternate));
            if (published)
            {
                break;
            }

            yield return null;
        }

        Check(
            published,
            checkPrefix + "_ALTERNATE_PUBLISHED_ORGANICALLY",
            $"alternate={Describe(alternate)}; external={brain.IsExternallyDrivenActionActive}; "
            + $"owner={brain.ExternalIntentOwnerId}");
    }

    private bool HasReachedFaultPhase(BuildableObject facility, FacilityFaultPhase phase)
    {
        if (facility == null || brain.bestAction == null
            || !ReferenceEquals(brain.bestAction.destination, facility))
        {
            return false;
        }
        return phase switch
        {
            FacilityFaultPhase.Approach =>
                facility.ActiveVisitReservationCount == 1
                && facility.CurrentUserCount == 0
                && subject.GetNowXY() != facility.centerPos,
            FacilityFaultPhase.Queue =>
                facility.WaitingVisitReservationCount >= 1
                && facility.CurrentUserCount == 1,
            FacilityFaultPhase.Interaction => facility.CurrentUserCount == 1,
            _ => false
        };
    }

    private void SetNeutralNeedsWithUrgentSleep()
    {
        SetAllNeedsNeutral();
        if (subject.Stats.Stats.ContainsKey(CharacterCondition.SLEEP))
        {
            subject.Stats.Stats[CharacterCondition.SLEEP] = 0f;
        }
    }

    private void SetAllNeedsNeutral()
    {
        foreach (CharacterCondition condition in Enum.GetValues(typeof(CharacterCondition)))
        {
            if (subject.Stats.Stats.ContainsKey(condition))
            {
                subject.Stats.Stats[condition] = 100f;
            }
        }
    }

    private void PreferPrimitiveAction(AIActionSet actionSet)
    {
        switch (actionSet)
        {
            case AIPrimitiveFieldMeal:
                brain.PreferActionOnNextDecision<AIPrimitiveFieldMeal>(180f);
                break;
            case AIPrimitiveFloorRest:
                brain.PreferActionOnNextDecision<AIPrimitiveFloorRest>(180f);
                break;
            case AIPrimitiveLatrine:
                brain.PreferActionOnNextDecision<AIPrimitiveLatrine>(180f);
                break;
            case AIPrimitiveBucketWash:
                brain.PreferActionOnNextDecision<AIPrimitiveBucketWash>(180f);
                break;
        }
    }

    private float GetNeedValue(CharacterCondition condition) =>
        subject?.Stats != null
        && subject.Stats.TryGetConditionValue(condition, out float value)
            ? value
            : 0f;

    private void SetNeedValue(CharacterCondition condition, float value)
    {
        if (subject?.Stats?.Stats?.ContainsKey(condition) == true)
            subject.Stats.Stats[condition] = value;
    }

    private WorldItemStackSnapshot SpawnTemporaryStack(
        string itemId,
        Vector2Int position,
        int quantity)
    {
        // A fixture stack must remain independently removable. Never seed into
        // an occupied cell where the production stacker could merge it with
        // authored stock and make cleanup destructive or lossy.
        if (itemStacks.GetStacksAt(position, includeStored: true).Count > 0)
            return null;
        HashSet<string> before = itemStacks.GetAllStacks()
            .Where(stack => stack != null)
            .Select(stack => stack.StackId)
            .ToHashSet();
        if (!itemStacks.SpawnItemAt(
                itemId,
                quantity,
                position,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            || spawned != quantity)
            return null;
        WorldItemStackSnapshot created = itemStacks.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && !before.Contains(stack.StackId)
                && stack.Position == position
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal));
        if (created != null)
            temporaryStackIds.Add(created.StackId);
        return created;
    }

    private WorldItemStackSnapshot SpawnTemporaryMealBufferStack(
        BuildableObject facility,
        string itemId,
        int quantity)
    {
        if (facility == null || quantity <= 0)
            return null;
        string destinationId = CharacterConsumablesRuntime.GetMealDestinationId(
            facility.RequirePersistentInstanceId(),
            new ConsumableItemDefinitionId(itemId));
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            itemRepository,
            itemId,
            quantity,
            WorldItemStackState.FacilityBuffer,
            destinationId,
            position: facility.centerPos);
        temporaryStackIds.Add(stackId);
        WorldItemStackSnapshot created = itemStacks.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && string.Equals(stack.StackId, stackId, StringComparison.Ordinal)
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, destinationId,
                    StringComparison.Ordinal)
                && stack.Position == facility.centerPos
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal));
        if (created == null)
            RemoveTemporaryStack(stackId);
        return created;
    }

    private void RemoveTemporaryStack(string stackId)
    {
        if (string.IsNullOrWhiteSpace(stackId)
            || !temporaryStackIds.Remove(stackId))
            return;
        WorldItemRepositoryEditorAccess.TryRemoveStack(itemRepository, stackId);
    }

    private void SetTemporaryAreaType(
        Vector2Int position,
        GridCellAreaType areaType)
    {
        GridCell cell = grid.GetGridCell(position);
        if (cell == null)
            return;
        if (!originalAreaTypesByCell.ContainsKey(position))
            originalAreaTypesByCell[position] = cell.AreaType;
        grid.SetAreaType(position, areaType);
        gridSystem.NotifyGridObjectChanged();
    }

    private void RestoreTemporaryAreaType(Vector2Int position)
    {
        if (!originalAreaTypesByCell.TryGetValue(position, out GridCellAreaType areaType))
            return;
        grid.SetAreaType(position, areaType);
        originalAreaTypesByCell.Remove(position);
        gridSystem.NotifyGridObjectChanged();
    }

    private static int GetPrimitiveCount(
        IReadOnlyDictionary<string, int> counts,
        string actionId) =>
        counts != null && counts.TryGetValue(actionId, out int count) ? count : 0;

    private static void IncrementPrimitiveCount(
        IDictionary<string, int> counts,
        string actionId)
    {
        counts.TryGetValue(actionId, out int count);
        counts[actionId] = count + 1;
    }

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);

    private void NeutralizeSubjectAndEndPrimitiveFallback(
        bool clearFailures = true)
    {
        if (subject == null || brain == null)
        {
            return;
        }
        SetAllNeedsNeutral();
        if (brain.IsExternallyDrivenActionActive)
        {
            brain.EndExternallyDrivenAction(
                brain.ExternalIntentOwnerId,
                clearFailures);
        }
    }

    private bool TryCreateCorridor(out CorridorFixture fixture, out string failure)
    {
        fixture = default;
        failure = string.Empty;
        if (grid.width < 7 || grid.height < 2)
        {
            failure = $"grid too small: {grid.width}x{grid.height}";
            return false;
        }

        HashSet<Vector2Int> actorCells = GetLiveActors()
            .Select(actor => actor.GetNowXY())
            .ToHashSet();
        int[] candidateWidths = { 9, 7, 5 };
        for (int y = 0; y < grid.height - 1; y++)
        {
            foreach (int corridorWidth in candidateWidths)
            {
                if (corridorWidth > grid.width - 2)
                {
                    continue;
                }
                for (int x = 1; x <= grid.width - corridorWidth - 1; x++)
                {
                    List<Vector2Int> cells = new(corridorWidth * 2);
                    for (int row = 0; row < 2; row++)
                    {
                        for (int offset = 0; offset < corridorWidth; offset++)
                        {
                            cells.Add(new Vector2Int(x + offset, y + row));
                        }
                    }
                    if (cells.Any(position => !CanOwnCorridorCell(
                        position,
                        actorCells)))
                    {
                        continue;
                    }

                    // Official floors may be authored as one connected movement
                    // surface, which gives every cell links outside a small QA
                    // window. Snapshot those links and isolate only this window;
                    // the real Grid, Brain and AbilityMove still execute, and
                    // cleanup restores the exact authored link arrays.
                    foreach (Vector2Int position in cells)
                    {
                        GridCell cell = grid.GetGridCell(position);
                        IGridOccupant building = cell.GetOccupant(GridLayer.Building);
                        if (building != null)
                        {
                            originalMovementBuildingByCell[position] = building;
                            grid.RemoveOccupant(
                                building,
                                GridLayer.Building,
                                new[] { position },
                                disconnectPositions: false);
                        }
                        originalTraversalLinksByCell[position] =
                            cell.TraversalLinks.ToArray();
                        cell.SetTraversalLinks(null);
                    }

                    FaultHallwayOccupant hallway = new();
                    List<Vector2Int> missingHallways = cells
                        .Where(position => !grid.GetGridCell(position)
                            .HasOccupantInLayer(GridLayer.Hallway))
                        .ToList();
                    if (missingHallways.Count > 0
                        && !grid.RegisterOccupant(
                            hallway,
                            GridLayer.Hallway,
                            missingHallways,
                            connectPositions: false))
                    {
                        foreach (Vector2Int position in cells)
                        {
                            if (originalTraversalLinksByCell.TryGetValue(
                                position,
                                out GridTraversalLink[] originalLinks))
                            {
                                grid.GetGridCell(position)?.SetTraversalLinks(originalLinks);
                                originalTraversalLinksByCell.Remove(position);
                            }
                            if (originalMovementBuildingByCell.TryGetValue(
                                position,
                                out IGridOccupant originalBuilding))
                            {
                                grid.RegisterOccupant(
                                    originalBuilding,
                                    GridLayer.Building,
                                    new[] { position },
                                    connectPositions: false);
                                originalMovementBuildingByCell.Remove(position);
                            }
                        }
                        continue;
                    }
                    addedHallwayCells.AddRange(missingHallways);

                    int lastOffset = corridorWidth - 1;
                    int targetOffset = corridorWidth - 2;
                    int blockOffset = corridorWidth / 2;
                    FaultWallOccupant boundary = AddWall(
                        new[]
                        {
                            new Vector2Int(x, y), new Vector2Int(x + lastOffset, y),
                            new Vector2Int(x, y + 1), new Vector2Int(x + lastOffset, y + 1)
                        },
                        "corridor-boundary");
                    if (boundary == null)
                    {
                        failure = "failed to register corridor boundary";
                        return false;
                    }

                    FaultStairOccupant leftStair = AddStair(
                        new Vector2Int(x + 1, y),
                        new Vector2Int(x + 1, y + 1),
                        "corridor-left-stair");
                    FaultStairOccupant rightStair = AddStair(
                        new Vector2Int(x + targetOffset, y),
                        new Vector2Int(x + targetOffset, y + 1),
                        "corridor-right-stair");
                    if (leftStair == null || rightStair == null)
                    {
                        failure = "failed to register corridor stairs";
                        return false;
                    }

                    fixture = new CorridorFixture(
                        new Vector2Int(x + 1, y),
                        new Vector2Int(x + blockOffset, y),
                        new Vector2Int(x + blockOffset, y + 1),
                        new Vector2Int(x + targetOffset, y),
                        new Vector2Int(x + targetOffset, y + 1));
                    return true;
                }
            }
        }

        failure = "no 9/7/5x2 window with empty building cells for an isolated traversal fixture";
        return false;
    }

    private bool CanOwnCorridorCell(
        Vector2Int position,
        HashSet<Vector2Int> actorCells)
    {
        GridCell cell = grid.GetGridCell(position);
        IGridOccupant building = cell?.GetOccupant(GridLayer.Building);
        bool replaceableMovementSurface = building == null
            || building.IsGridMovement
            && (!(building is IGridBuildingOccupantCapability capability)
                || !capability.BlocksGridMovement);
        return cell != null
            && !actorCells.Contains(position)
            && replaceableMovementSurface
            && cell.IsBuildableArea
            && (cell.HasOccupantInLayer(GridLayer.Hallway)
                || cell.CanOccupy(GridLayer.Hallway));
    }

    private BuildableObject CreateRestFacility(
        Vector2Int position,
        string suffix,
        float useDuration)
    {
        return CreateFacility(
            position,
            suffix,
            FacilityRole.Rest,
            useDuration);
    }

    private BuildableObject CreateFacility(
        Vector2Int position,
        string suffix,
        FacilityRole role,
        float useDuration)
    {
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        runtimeDefinitions.Add(data);
        data.name = "QA_Fault_" + suffix;
        data.id = 982000 + runtimeDefinitions.Count;
        data.objectName = "QA Fault Rest " + suffix;
        data.width = 1;
        data.height = 1;
        data.layer = GridLayer.Building;
        data.category = BuildingCategory.Special;
        data.runtimeArchetype = BuildingRuntimeArchetypeKind.Facility;
        data.unlocked = true;
        BuildingSO visualTemplate = Resources.LoadAll<BuildingSO>("SO/Building")
            .FirstOrDefault(candidate => candidate != null && candidate.sprite != null);
        data.sprite = visualTemplate?.sprite;
        data.icon = visualTemplate?.icon ?? visualTemplate?.sprite;
        data.Facility = new FacilityData
        {
            roles = role,
            capacity = 1,
            useDuration = useDuration,
            requiredWorkers = 0,
            disabledWhenDamaged = false
        };
        data.AbilityModules.Add(new BuildingNeedRecoveryAbility
        {
            recovery = CreateRecovery(role)
        });
        data.ValidateAbilitiesOrThrow();

        GridBuildingFactory factory = new(building => InjectGameObject(building.gameObject));
        BuildableObject facility = factory.Create(grid, data, position);
        if (facility == null)
        {
            return null;
        }
        facility.SetGrid(grid);
        facility.Initialization(data, position);
        if (!grid.RegisterOccupant(
            facility,
            data.layer,
            data.GetGridPosList(position),
            connectPositions: false))
        {
            Destroy(facility.gameObject);
            return null;
        }
        runtimeFacilities.Add(facility);
        gridSystem.NotifyGridObjectChanged();
        return facility;
    }

    private static FacilityNeedRecoveryData CreateRecovery(FacilityRole role)
    {
        FacilityNeedRecoveryData recovery = default;
        if ((role & FacilityRole.Rest) != 0)
            recovery.sleep = 12f;
        if ((role & FacilityRole.Meal) != 0)
            recovery.hunger = 35f;
        if ((role & FacilityRole.Toilet) != 0)
            recovery.excretion = 35f;
        if ((role & FacilityRole.Hygiene) != 0)
            recovery.hygiene = 35f;
        if ((role & FacilityRole.Entertainment) != 0)
        {
            recovery.fun = 35f;
            recovery.mood = 4f;
        }
        if ((role & FacilityRole.Purchase) != 0)
            recovery.mood = 4f;
        return recovery;
    }

    private FaultWallOccupant AddWall(Vector2Int position, string label) =>
        AddWall(new[] { position }, label);

    private FaultWallOccupant AddWall(
        IReadOnlyList<Vector2Int> positions,
        string label)
    {
        FaultWallOccupant wall = new(label, positions);
        if (!grid.RegisterOccupant(
            wall,
            GridLayer.Building,
            positions,
            connectPositions: false))
        {
            return null;
        }
        walls.Add(wall);
        return wall;
    }

    private void RemoveWall(FaultWallOccupant wall)
    {
        if (wall == null)
        {
            return;
        }
        grid.RemoveOccupant(
            wall,
            GridLayer.Building,
            wall.Positions,
            disconnectPositions: false);
        walls.Remove(wall);
        gridSystem.NotifyGridObjectChanged();
    }

    private FaultStairOccupant AddStair(
        Vector2Int lower,
        Vector2Int upper,
        string label)
    {
        FaultStairOccupant stair = new(grid, lower, upper, label);
        Vector2Int[] positions = { lower, upper };
        if (!grid.RegisterOccupant(
            stair,
            GridLayer.Utility,
            positions,
            connectPositions: true))
        {
            return null;
        }
        stairs.Add(stair);
        return stair;
    }

    private void DestroyFacility(BuildableObject facility)
    {
        if (facility == null)
        {
            return;
        }
        if (!facility.isDestroy)
        {
            facility.DestroySelf();
        }
        runtimeFacilities.Remove(facility);
    }

    private void PauseOtherAi()
    {
        foreach (CharacterActor actor in GetLiveActors())
        {
            if (actor == subject)
            {
                continue;
            }
            AIBrain otherBrain = actor.Brain;
            BehaviorTree tree = actor.BehaviorTree;
            if (otherBrain != null)
            {
                pausedAi.Add(new MonoBehaviourState(otherBrain, otherBrain.enabled));
                otherBrain.enabled = false;
            }
            if (tree != null)
            {
                pausedAi.Add(new MonoBehaviourState(tree, tree.enabled));
                tree.enabled = false;
            }
        }
    }

    private void CleanupWorld()
    {
        primitiveStartedSubscription?.Dispose();
        primitiveStartedSubscription = null;
        primitiveCompletedSubscription?.Dispose();
        primitiveCompletedSubscription = null;
        if (brain != null)
        {
            brain.StopCurrentActionForReplan("fault-verifier-cleanup");
            brain.availableActions = originalActions;
            brain.RequestImmediateReplan(clearFailures: true);
        }
        move?.CancelActiveMovement();
        if (subject != null)
        {
            subject.transform.position = originalPosition;
            if (subject.TryGetAbility(out AbilityWork work))
            {
                work.SetDutyState(originalSubjectOffDuty
                    ? AbilityWork.DutyState.OffDuty
                    : AbilityWork.DutyState.OnDuty);
            }
            if (originalStats != null)
            {
                subject.Stats.Stats = originalStats;
            }
        }
        foreach (MonoBehaviourState state in pausedAi)
        {
            if (state.Component != null)
            {
                state.Component.enabled = state.WasEnabled;
            }
        }
        pausedAi.Clear();

        foreach (BuildableObject facility in runtimeFacilities.ToArray())
        {
            DestroyFacility(facility);
        }
        if (grid != null)
        {
            foreach (KeyValuePair<Vector2Int, GridCellAreaType> entry
                in originalAreaTypesByCell.ToArray())
            {
                grid.SetAreaType(entry.Key, entry.Value);
            }
            originalAreaTypesByCell.Clear();
            foreach (FaultWallOccupant wall in walls.ToArray())
            {
                grid.RemoveOccupant(
                    wall,
                    GridLayer.Building,
                    wall.Positions,
                    disconnectPositions: false);
            }
            foreach (FaultStairOccupant stair in stairs)
            {
                grid.RemoveOccupant(
                    stair,
                    GridLayer.Utility,
                    stair.Positions,
                    disconnectPositions: true);
            }
            foreach (KeyValuePair<Vector2Int, GridTraversalLink[]> entry
                in originalTraversalLinksByCell)
            {
                grid.GetGridCell(entry.Key)?.SetTraversalLinks(entry.Value);
            }
            if (addedHallwayCells.Count > 0)
            {
                foreach (Vector2Int position in addedHallwayCells)
                {
                    IGridOccupant occupant = grid.GetGridCell(position)?
                        .GetOccupant(GridLayer.Hallway);
                    if (occupant is FaultHallwayOccupant)
                    {
                        grid.RemoveOccupant(
                            occupant,
                            GridLayer.Hallway,
                            new[] { position },
                            disconnectPositions: false);
                    }
                }
            }
            foreach (KeyValuePair<Vector2Int, IGridOccupant> entry
                in originalMovementBuildingByCell)
            {
                if (entry.Value != null && !entry.Value.IsGridDestroyed)
                {
                    grid.RegisterOccupant(
                        entry.Value,
                        GridLayer.Building,
                        new[] { entry.Key },
                        connectPositions: false);
                }
            }
        }
        walls.Clear();
        stairs.Clear();
        originalTraversalLinksByCell.Clear();
        originalMovementBuildingByCell.Clear();
        addedHallwayCells.Clear();
        foreach (BuildingSO definition in runtimeDefinitions)
        {
            if (definition != null)
            {
                Destroy(definition);
            }
        }
        runtimeDefinitions.Clear();
        if (itemRepository != null)
        {
            foreach (string stackId in temporaryStackIds.ToArray())
                WorldItemRepositoryEditorAccess.TryRemoveStack(itemRepository, stackId);
        }
        temporaryStackIds.Clear();
        gridSystem?.NotifyGridObjectChanged();
    }

    private void InjectGameObject(GameObject target)
    {
        if (scope?.Container == null || target == null)
        {
            return;
        }
        foreach (MonoBehaviour component in target
            .GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
        {
            scope.Container.Inject(component);
        }
    }

    private static LifetimeScope FindScope()
    {
        Scene scene = SceneManager.GetActiveScene();
        LifetimeScope[] scopes = UnityEngine.Object.FindObjectsByType<LifetimeScope>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        return scopes.FirstOrDefault(candidate => candidate != null
                && candidate.Container != null
                && candidate.gameObject.scene == scene)
            ?? scopes.FirstOrDefault(candidate => candidate?.Container != null);
    }

    private static CharacterActor[] GetLiveActors() =>
        CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null
                && actor.gameObject.activeInHierarchy
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                && !actor.IsDead)
            .ToArray();

    private static bool IsPipelineActor(CharacterActor actor) =>
        actor != null
        && actor.Brain != null
        && actor.BehaviorTree != null
        && actor.BehaviorTree.enabled
        && actor.TryGetAbility(out AbilityMove _)
        && actor.TryGetAbility(out AbilityShopping _)
        && actor.Brain.availableActions?.Any(
            action => action?.actionset is AIRest) == true;

    private void Check(bool condition, string id, string detail)
    {
        string line = $"{(condition ? "PASS" : "FAIL")} {id}: {Compact(detail)}";
        checks.Add(line);
        if (!condition)
        {
            failures.Add(line);
        }
    }

    private bool ShouldRun(string candidate)
    {
        string normalized = candidate?.Trim().ToLowerInvariant() ?? string.Empty;
        return selector == "all"
            || selector == normalized
            || selector.StartsWith(normalized + ":", StringComparison.Ordinal)
            || normalized.StartsWith(selector + ":", StringComparison.Ordinal);
    }

    private void BeginScenario(string scenario)
    {
        currentScenario = scenario;
        scenarioStartedRealtime = Time.realtimeSinceStartup;
        failuresAtScenarioStart = failures.Count;
        if (!string.Equals(
                scenario,
                "setup:resolve-live-world",
                StringComparison.Ordinal))
        {
            if (startedRowIds.Contains(scenario, StringComparer.Ordinal))
            {
                failures.Add("DUPLICATE_SCENARIO_START: " + scenario);
            }
            else
            {
                startedRowIds.Add(scenario);
            }
            selectedScenarioCount++;
        }
        WriteReport(final: false);
    }

    private void CompleteScenario(string scenario)
    {
        float elapsed = Mathf.Max(
            0f,
            Time.realtimeSinceStartup - scenarioStartedRealtime);
        bool passed = failures.Count == failuresAtScenarioStart;
        if (!string.Equals(
                scenario,
                "setup:resolve-live-world",
                StringComparison.Ordinal))
        {
            if (!startedRowIds.Contains(scenario, StringComparer.Ordinal))
            {
                failures.Add("SCENARIO_COMPLETED_WITHOUT_START: " + scenario);
                passed = false;
            }
            if (completedRowIds.Contains(scenario, StringComparer.Ordinal))
            {
                failures.Add("DUPLICATE_SCENARIO_COMPLETION: " + scenario);
                passed = false;
            }
            else
            {
                completedRowIds.Add(scenario);
            }
            rowPassedById[scenario] = passed;
        }
        checks.Add($"{(passed ? "PASS" : "FAIL")} SCENARIO {scenario}: "
            + $"elapsed={elapsed:0.###}s");
        currentScenario = "between-scenarios";
        WriteReport(final: false);
    }

    private void FinalizeRun()
    {
        if (finalized)
            return;
        finalized = true;
        runActive = false;
        if (failures.Count == 0 && selectedScenarioCount == 0)
        {
            failures.Add($"SELECTOR_MATCHED_NO_SCENARIOS: selector={selector}");
        }
        try
        {
            CleanupWorld();
        }
        catch (Exception exception)
        {
            failures.Add("CLEANUP_FAILED: " + Compact(exception));
        }
        finally
        {
            StopLogCapture();
            Time.timeScale = originalTimeScale;
            currentScenario = "complete";
            try
            {
                WriteReport(final: true);
            }
            finally
            {
                Destroy(gameObject);
                if (exitPlayModeOnCompletion && EditorApplication.isPlaying)
                {
                    EditorApplication.ExitPlaymode();
                }
            }
        }
    }

    private void StartLogCapture()
    {
        if (capturingLogs)
        {
            return;
        }
        Application.logMessageReceived += OnLog;
        capturingLogs = true;
    }

    private void StopLogCapture()
    {
        if (!capturingLogs)
        {
            return;
        }
        Application.logMessageReceived -= OnLog;
        capturingLogs = false;
    }

    private void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            consoleIssues.Add($"{type}: {Compact(condition)}");
        }
    }

    private void WriteReport(bool final)
    {
        if (selector == "all")
        {
            foreach (string group in CharacterAiFaultRecoveryPlayModeVerifier
                         .GetDurableGroupSelectors())
            {
                WriteScopedReport(
                    group,
                    CharacterAiFaultRecoveryPlayModeVerifier
                        .GetReportPathForSelector(group),
                    final);
            }
            WriteScopedReport(
                "all",
                CharacterAiFaultRecoveryPlayModeVerifier.ReportPath,
                final);
        }
        else
        {
            WriteScopedReport(
                selector,
                CharacterAiFaultRecoveryPlayModeVerifier
                    .GetReportPathForSelector(selector),
                final);
        }

        if (final)
        {
            string primaryPath = CharacterAiFaultRecoveryPlayModeVerifier
                .GetReportPathForSelector(selector);
            bool passed = ScopePassed(selector);
            Debug.Log($"Character AI fault recovery verification "
                + $"{(passed ? "PASS" : "FAIL")}: {primaryPath}");
        }
    }

    private void WriteScopedReport(
        string reportSelector,
        string reportPath,
        bool final)
    {
        IReadOnlyList<string> expectedRows =
            CharacterAiFaultRecoveryPlayModeVerifier
                .GetExpectedRowsForSelector(reportSelector);
        string[] startedRows = RowsForScope(startedRowIds, reportSelector);
        string[] completedRows = RowsForScope(completedRowIds, reportSelector);
        bool exactRows = expectedRows.Count > 0
            && expectedRows.SequenceEqual(startedRows, StringComparer.Ordinal)
            && expectedRows.SequenceEqual(completedRows, StringComparer.Ordinal);
        bool passed = final && ScopePassed(reportSelector);
        DateTime utc = DateTime.UtcNow;

        StringBuilder builder = new(16 * 1024);
        builder.AppendLine("# Character AI Fault Recovery PlayMode Verification");
        builder.AppendLine("pipeline=live Brain -> BehaviorTree -> AIAction -> AbilityMove/AbilityShopping");
        builder.AppendLine("repathAuthority=target cell reached + same action + one repath + zero terminal failure; facility interaction lifecycle is verified by the approach/queue/interaction destruction matrix");
        builder.AppendLine($"scene={SceneManager.GetActiveScene().path}");
        builder.AppendLine($"status={(final ? "FINAL" : "RUNNING")}");
        builder.AppendLine($"selector={reportSelector}");
        builder.AppendLine($"verifierRevision={CharacterAiFaultRecoveryPlayModeVerifier.VerifierRevision}");
        builder.AppendLine($"utc={utc:O}");
        builder.AppendLine($"startedUtc={runStartedUtc:O}");
        builder.AppendLine($"completedUtc={(final ? utc.ToString("O") : string.Empty)}");
        builder.AppendLine($"currentScenario={currentScenario}");
        builder.AppendLine($"scenarioElapsedRealtime={Mathf.Max(0f, Time.realtimeSinceStartup - scenarioStartedRealtime):0.###}");
        builder.AppendLine($"totalElapsedRealtime={Mathf.Max(0f, Time.realtimeSinceStartup - runStartedRealtime):0.###}");
        builder.AppendLine($"overallCeilingRealtime={OverallTimeoutRealtime:0.###}");
        builder.AppendLine($"selectedScenariosStarted={startedRows.Length}");
        builder.AppendLine($"expectedRows={string.Join(",", expectedRows)}");
        builder.AppendLine($"startedRows={string.Join(",", startedRows)}");
        builder.AppendLine($"completedRows={string.Join(",", completedRows)}");
        builder.AppendLine($"startedRowIds={string.Join(",", startedRows)}");
        builder.AppendLine($"completedRowIds={string.Join(",", completedRows)}");
        builder.AppendLine($"exactRows={exactRows}");
        builder.AppendLine($"checks={checks.Count}");
        builder.AppendLine($"failures={failures.Count}");
        builder.AppendLine($"consoleErrors={consoleIssues.Count}");
        builder.AppendLine();
        builder.AppendLine("## Row authority");
        foreach (string row in expectedRows)
        {
            bool started = startedRowIds.Contains(row, StringComparer.Ordinal);
            bool completed = completedRowIds.Contains(row, StringComparer.Ordinal);
            bool rowPassed = completed
                && rowPassedById.TryGetValue(row, out bool value)
                && value;
            string rowResult = !started
                ? "NOT_STARTED"
                : !completed
                    ? "RUNNING"
                    : rowPassed ? "PASS" : "FAIL";
            builder.AppendLine($"row={row};started={started};completed={completed};"
                + $"result={rowResult}");
        }
        builder.AppendLine();
        builder.AppendLine("## Checks");
        foreach (string line in checks)
        {
            builder.AppendLine(line);
        }
        builder.AppendLine();
        builder.AppendLine("## Failures");
        if (failures.Count == 0)
        {
            builder.AppendLine("PASS none");
        }
        else
        {
            foreach (string failure in failures.Distinct(StringComparer.Ordinal))
            {
                builder.AppendLine("FAIL " + failure);
            }
        }
        builder.AppendLine();
        builder.AppendLine("## Console errors");
        if (consoleIssues.Count == 0)
        {
            builder.AppendLine("PASS none");
        }
        else
        {
            foreach (string issue in consoleIssues)
            {
                builder.AppendLine("FAIL " + issue);
            }
        }
        builder.AppendLine();
        string result = !final ? "RUNNING" : passed ? "PASS" : "FAIL";
        builder.AppendLine("result=" + result);
        builder.AppendLine("RESULT=" + result);
        string directory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
    }

    private bool ScopePassed(string reportSelector)
    {
        IReadOnlyList<string> expectedRows =
            CharacterAiFaultRecoveryPlayModeVerifier
                .GetExpectedRowsForSelector(reportSelector);
        if (expectedRows.Count == 0
            || consoleIssues.Count != 0)
        {
            return false;
        }

        string[] startedRows = RowsForScope(startedRowIds, reportSelector);
        string[] completedRows = RowsForScope(completedRowIds, reportSelector);
        return expectedRows.SequenceEqual(startedRows, StringComparer.Ordinal)
            && expectedRows.SequenceEqual(completedRows, StringComparer.Ordinal)
            && expectedRows.All(row => rowPassedById.TryGetValue(
                row,
                out bool passed) && passed);
    }

    private static string[] RowsForScope(
        IEnumerable<string> rows,
        string reportSelector)
    {
        if (string.Equals(reportSelector, "all", StringComparison.Ordinal))
            return rows.ToArray();

        string prefix = reportSelector + ":";
        return rows.Where(row => string.Equals(
                    row,
                    reportSelector,
                    StringComparison.Ordinal)
                || row.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
    }

    private static string Describe(AIAction action) => action == null
        ? "null"
        : $"{action.actionset?.GetType().Name}/{Describe(action.destination)}/started={action.HasStarted}";

    private static string Describe(BuildableObject building) => building == null
        ? "null"
        : $"{building.name}@{building.centerPos}/destroy={building.isDestroy}";

    private static string Compact(object value) =>
        (value?.ToString() ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

    private enum FacilityFaultPhase
    {
        Approach,
        Queue,
        Interaction
    }

    private readonly struct FacilityActionScenario
    {
        public FacilityActionScenario(
            string label,
            FacilityRole role,
            CharacterCondition? needCondition,
            Func<AIAction, bool> match)
        {
            Label = label;
            Role = role;
            NeedCondition = needCondition;
            Match = match;
        }

        public string Label { get; }
        public FacilityRole Role { get; }
        public CharacterCondition? NeedCondition { get; }
        public Func<AIAction, bool> Match { get; }
    }

    private readonly struct BreakdownScenario
    {
        public BreakdownScenario(
            string label,
            CharacterBreakdownKind kind)
        {
            Label = label;
            Kind = kind;
        }

        public string Label { get; }
        public CharacterBreakdownKind Kind { get; }
    }

    private readonly struct DestinationlessScenario
    {
        public DestinationlessScenario(
            string label,
            Func<AIAction, bool> match,
            Action prepare,
            bool recoveryTerminatesNoPath,
            bool requiresVisitorProjection)
        {
            Label = label;
            Match = match;
            Prepare = prepare;
            RecoveryTerminatesNoPath = recoveryTerminatesNoPath;
            RequiresVisitorProjection = requiresVisitorProjection;
        }

        public string Label { get; }
        public Func<AIAction, bool> Match { get; }
        public Action Prepare { get; }
        public bool RecoveryTerminatesNoPath { get; }
        public bool RequiresVisitorProjection { get; }
    }

    private sealed class DeferredPathSearchBroker : IGridPathSearchBroker
    {
        private readonly IGridPathSearchBroker inner;
        private readonly bool forceUnreachableAfterDeferrals;
        private int remainingDeferrals;

        public DeferredPathSearchBroker(
            IGridPathSearchBroker inner,
            int deferCount,
            bool forceUnreachableAfterDeferrals)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            remainingDeferrals = deferCount;
            this.forceUnreachableAfterDeferrals = forceUnreachableAfterDeferrals;
        }

        public int DeferredCalls { get; private set; }
        public int SearchesThisFrame => inner.SearchesThisFrame;
        public int UrgentOverdraftSearchesThisFrame =>
            inner.UrgentOverdraftSearchesThisFrame;
        public int UnboundedSearchesThisFrame => inner.UnboundedSearchesThisFrame;
        public int CacheHitsThisFrame => inner.CacheHitsThisFrame;
        public int BudgetDeferralsThisFrame => inner.BudgetDeferralsThisFrame + DeferredCalls;
        public double SearchMillisecondsThisFrame => inner.SearchMillisecondsThisFrame;

        public void BeginFrame(
            int searchBudget,
            bool enforceBudget,
            double searchTimeBudgetMilliseconds = double.PositiveInfinity) =>
            inner.BeginFrame(
                searchBudget,
                enforceBudget,
                searchTimeBudgetMilliseconds);

        public bool TryGetSearch(
            Grid grid,
            Vector2Int start,
            out GridPathSearchResult result,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default)
        {
            if (ShouldDefer())
            {
                result = null;
                return false;
            }
            return inner.TryGetSearch(
                grid,
                start,
                out result,
                priority,
                traversalContext);
        }

        public Queue<GridMoveStep> GetMovePath(
            Grid grid,
            Vector2Int start,
            Func<Vector2Int, bool> terminateEndCondition,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default)
        {
            return ShouldDefer()
                ? null
                : inner.GetMovePath(
                    grid,
                    start,
                    terminateEndCondition,
                    priority,
                    traversalContext);
        }

        public Queue<GridMoveStep> GetMovePathTo(
            Grid grid,
            Vector2Int start,
            Vector2Int destination,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default)
        {
            return ShouldDefer()
                ? null
                : forceUnreachableAfterDeferrals
                    ? new Queue<GridMoveStep>()
                    : inner.GetMovePathTo(
                    grid,
                    start,
                    destination,
                    priority,
                    traversalContext);
        }

        public GridPathRequestStatus RequestMovePathTo(
            Grid grid,
            Vector2Int start,
            Vector2Int destination,
            out Queue<GridMoveStep> path,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default)
        {
            if (ShouldDefer())
            {
                path = null;
                return GridPathRequestStatus.Pending;
            }
            if (forceUnreachableAfterDeferrals)
            {
                path = new Queue<GridMoveStep>();
                return GridPathRequestStatus.Unreachable;
            }
            return inner.RequestMovePathTo(
                grid,
                start,
                destination,
                out path,
                priority,
                traversalContext);
        }

        public void Clear() => inner.Clear();

        private bool ShouldDefer()
        {
            if (remainingDeferrals <= 0)
            {
                return false;
            }
            DeferredCalls++;
            if (remainingDeferrals != int.MaxValue)
            {
                remainingDeferrals--;
            }
            return true;
        }
    }

    private readonly struct CorridorFixture
    {
        public CorridorFixture(
            Vector2Int start,
            Vector2Int lowerBlock,
            Vector2Int upperBlock,
            Vector2Int primary,
            Vector2Int alternate)
        {
            Start = start;
            LowerBlock = lowerBlock;
            UpperBlock = upperBlock;
            Primary = primary;
            Alternate = alternate;
        }

        public Vector2Int Start { get; }
        public Vector2Int LowerBlock { get; }
        public Vector2Int UpperBlock { get; }
        public Vector2Int Primary { get; }
        public Vector2Int Alternate { get; }
        public override string ToString() =>
            $"start={Start}; block={LowerBlock}/{UpperBlock}; targets={Primary}/{Alternate}";
    }

    private readonly struct MonoBehaviourState
    {
        public MonoBehaviourState(MonoBehaviour component, bool wasEnabled)
        {
            Component = component;
            WasEnabled = wasEnabled;
        }
        public MonoBehaviour Component { get; }
        public bool WasEnabled { get; }
    }

    private sealed class FaultHallwayOccupant : IGridOccupant
    {
        public int GridId => 982001;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }

    private sealed class FaultWallOccupant : IGridBuildingOccupantCapability
    {
        public FaultWallOccupant(string label, IReadOnlyList<Vector2Int> positions)
        {
            Label = label;
            Positions = positions?.ToArray() ?? Array.Empty<Vector2Int>();
        }
        public string Label { get; }
        public IReadOnlyList<Vector2Int> Positions { get; }
        public int GridId => Label.GetHashCode();
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => false;
        public bool BlocksGridMovement => true;
        public bool AllowsInteriorWalkability => false;
    }

    private sealed class FaultStairOccupant :
        IGridOccupant,
        IGridMovementOccupant,
        IGridMovementHandler
    {
        private readonly Grid grid;
        public FaultStairOccupant(
            Grid grid,
            Vector2Int lower,
            Vector2Int upper,
            string label)
        {
            this.grid = grid;
            Label = label;
            Positions = new[] { lower, upper };
        }
        public string Label { get; }
        public IReadOnlyList<Vector2Int> Positions { get; }
        public int GridId => Label.GetHashCode();
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
        public GridMoveType GridMoveType => GridMoveType.Stair;

        public IEnumerator Traverse(IBuildingVisitorPort actor, GridMoveStep step)
        {
            actor?.SetWorldPosition(grid.GetWorldPos(step.To));
            yield return null;
        }
    }
}
#endif
