#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

/// <summary>
/// Focused production coverage for the five deprivation breakdowns. The
/// fixture authors a valid state through the V18 aggregate save boundary, then
/// lets the live BehaviorTree root discover HasDeprivationBreakdown and run the
/// normal command. It never invokes CharacterAiDecisionPipeline or the
/// breakdown action runner directly.
/// </summary>
[InitializeOnLoad]
public static class CharacterDeprivationProductionBtPlayModeVerifier
{
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string PendingPath = "Temp/character-deprivation-production-bt.flag";
    public const string ReportPath =
        "Artifacts/QA/character-deprivation-production-bt-playmode.txt";
    private const string ReportPathEnvironmentVariable =
        "DUNGEONSTORY_DEPRIVATION_BT_REPORT_PATH";
    private const string VandalismOnlyEnvironmentVariable =
        "DUNGEONSTORY_DEPRIVATION_VANDALISM_ONLY";
    private const string FullMatrixEnvironmentVariable =
        "DUNGEONSTORY_DEPRIVATION_A09_FULL";
    private const string CorpseOnlyEnvironmentVariable =
        "DUNGEONSTORY_DEPRIVATION_CORPSE_ONLY";
    private static bool runnerCreated;
    private static bool batchRunRequested;

    internal static bool VandalismOnlyRequested => string.Equals(
        Environment.GetEnvironmentVariable(
            VandalismOnlyEnvironmentVariable)?.Trim(),
        "1",
        StringComparison.Ordinal);

    internal static bool FullMatrixRequested => string.Equals(
        Environment.GetEnvironmentVariable(
            FullMatrixEnvironmentVariable)?.Trim(),
        "1",
        StringComparison.Ordinal);

    internal static bool CorpseEatingOnlyRequested => string.Equals(
        Environment.GetEnvironmentVariable(CorpseOnlyEnvironmentVariable)?.Trim(),
        "1", StringComparison.Ordinal);

    internal static string ActiveReportPath
    {
        get
        {
            string relative = Environment.GetEnvironmentVariable(
                    ReportPathEnvironmentVariable)?.Trim();
            if (string.IsNullOrWhiteSpace(relative))
                relative = ReportPath;
            string root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                ".."));
            string absolute = Path.GetFullPath(Path.Combine(root, relative));
            string qaRoot = Path.GetFullPath(Path.Combine(
                root,
                "Artifacts",
                "QA"));
            if (!absolute.StartsWith(
                    qaRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Deprivation BT report must remain under Artifacts/QA.");
            }
            return absolute;
        }
    }

    static CharacterDeprivationProductionBtPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Run Deprivation Production BT Matrix")]
    public static void RequestRun()
    {
        if (Application.isBatchMode)
        {
            batchRunRequested = true;
            EditorApplication.playModeStateChanged -= ExitBatchAfterRun;
            EditorApplication.playModeStateChanged += ExitBatchAfterRun;
        }
        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingPath, DateTime.UtcNow.ToString("O"));
        if (EditorApplication.isPlaying)
        {
            TryStartPendingRunner();
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

    internal static void StartIsolatedFromFacade(
        IEnumerable<string> initialWarnings,
        IEnumerable<string> initialErrors)
    {
        if (!Application.isBatchMode
            || !EditorApplication.isPlaying
            || !DungeonPersistencePaths.IsIsolatedVerification
            || (VandalismOnlyRequested ? 1 : 0) + (FullMatrixRequested ? 1 : 0)
                + (CorpseEatingOnlyRequested ? 1 : 0) != 1)
        {
            throw new InvalidOperationException(
                "Deprivation verification requires exactly one active isolated PlayMode scope.");
        }

        string reportPath = ActiveReportPath;
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "Existing deprivation evidence must be preserved.");
        }

        CharacterDeprivationProductionBtPlayModeRunner runner = StartRunner()
            ?? throw new InvalidOperationException(
                "Deprivation runner could not be created.");
        runner.ExitBatchProcessOnCompletion = true;
        runner.AddInitialConsoleIssues(initialWarnings, initialErrors);
    }

    private static void ExitBatchAfterRun(PlayModeStateChange change)
    {
        if (!batchRunRequested
            || change != PlayModeStateChange.EnteredEditMode)
        {
            return;
        }
        batchRunRequested = false;
        EditorApplication.playModeStateChanged -= ExitBatchAfterRun;
        string reportPath = ActiveReportPath;
        bool passed = File.Exists(reportPath)
            && File.ReadLines(reportPath).Any(line =>
                string.Equals(line, "STATUS=PASS", StringComparison.Ordinal));
        EditorApplication.Exit(passed ? 0 : 1);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() => TryStartPendingRunner();

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            return;
        }
        if (change == PlayModeStateChange.EnteredPlayMode)
            TryStartPendingRunner();
    }

    private static void TryStartPendingRunner()
    {
        if (!File.Exists(PendingPath)) return;
        if (StartRunner() != null) File.Delete(PendingPath);
    }

    private static CharacterDeprivationProductionBtPlayModeRunner StartRunner()
    {
        CharacterDeprivationProductionBtPlayModeRunner existing =
            UnityEngine.Object.FindFirstObjectByType<
                CharacterDeprivationProductionBtPlayModeRunner>();
        if (existing != null)
        {
            runnerCreated = true;
            return existing;
        }
        if (runnerCreated) return null;
        CharacterDeprivationProductionBtPlayModeRunner runner =
            new GameObject(nameof(CharacterDeprivationProductionBtPlayModeRunner))
                .AddComponent<CharacterDeprivationProductionBtPlayModeRunner>();
        runnerCreated = runner != null;
        return runner;
    }
}

public sealed class CharacterDeprivationProductionBtPlayModeRunner : MonoBehaviour
{
    private const float SetupTimeout = 15f;
    private const float RowTimeout = 18f;
    private const string WaterItemId = "resource:clean-water";
    private const string MealItemId = "food:preserved-ration";
    private const string BreakdownOwnerId = "survival:breakdown";

    private readonly List<string> rows = new();
    private readonly List<string> failures = new();
    private readonly List<string> consoleIssues = new();
    private readonly List<MonoBehaviourState> pausedAi = new();

    private DungeonRuntimeLifetimeScope scope;
    private IDungeonGameSaveService saves;
    private ICharacterDeprivationRuntime deprivation;
    private IWorldItemStackRuntime items;
    private WorldItemRepository repository;
    private IItemQuantityReservationService reservations;
    private IWorldFilthQuery filth;
    private IBuildingWorldQuery buildings;
    private IGridSystemProvider gridSystems;
    private IRandomStreamProvider randomStreams;
    private IGameplayOutcomeDiagnosticsQuery outcomeDiagnostics;
    private DungeonGameSaveData baseline;
    private string actorId;
    private float oldTimeScale;
    private bool finished;
    private bool reportWritten;
    public bool ExitBatchProcessOnCompletion { get; set; }

    public void AddInitialConsoleIssues(
        IEnumerable<string> warnings,
        IEnumerable<string> errors)
    {
        if (warnings != null)
        {
            consoleIssues.AddRange(warnings.Select(value =>
                "Warning: " + value));
        }
        if (errors != null)
        {
            consoleIssues.AddRange(errors.Select(value =>
                "Error: " + value));
        }
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        oldTimeScale = Time.timeScale;
        Time.timeScale = 8f;
        Application.logMessageReceived += CaptureIssue;
        yield return ExecuteGuarded(RunMatrix());
        FinishRun();
    }

    private IEnumerator RunMatrix()
    {
        yield return ResolveWorld();
        if (failures.Count == 0)
        {
            if (CharacterDeprivationProductionBtPlayModeVerifier.CorpseEatingOnlyRequested)
            {
                yield return VerifyRow("CORPSE_EAT_A04", DeprivationKind.Hunger,
                    CharacterBreakdownKind.DesperateEat, CharacterCondition.HUNGER,
                    DarkSurvivalItemDefinitions.HumanoidCorpseItemId);
                yield break;
            }
            if (CharacterDeprivationProductionBtPlayModeVerifier
                .VandalismOnlyRequested)
            {
                yield return VerifyRow(
                    "VIOLENT_BREAKDOWN_A04",
                    DeprivationKind.MentalInstability,
                    CharacterBreakdownKind.ViolentImpulse,
                    CharacterCondition.MOOD,
                    null);
                yield break;
            }
            yield return VerifyRow(
                "RELIEF",
                DeprivationKind.Bladder,
                CharacterBreakdownKind.DesperateRelief,
                CharacterCondition.EXCRETION,
                null);
            yield return VerifyRow(
                "DRINK",
                DeprivationKind.Thirst,
                CharacterBreakdownKind.DesperateDrink,
                CharacterCondition.THIRST,
                WaterItemId);
            yield return VerifyRow(
                "EAT",
                DeprivationKind.Hunger,
                CharacterBreakdownKind.DesperateEat,
                CharacterCondition.HUNGER,
                MealItemId);
            yield return VerifyRow(
                "VIOLENT_BREAKDOWN",
                DeprivationKind.MentalInstability,
                CharacterBreakdownKind.ViolentImpulse,
                CharacterCondition.MOOD,
                null);
            yield return VerifyRow(
                "COLLAPSE",
                DeprivationKind.Exhaustion,
                CharacterBreakdownKind.Collapse,
                CharacterCondition.SLEEP,
                null);
        }
    }

    private IEnumerator ExecuteGuarded(IEnumerator root)
    {
        Stack<IEnumerator> stack = new();
        stack.Push(root);
        while (stack.Count > 0)
        {
            IEnumerator current = stack.Peek();
            object yielded;
            try
            {
                if (!current.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                yielded = current.Current;
            }
            catch (Exception exception)
            {
                string detail = exception.GetType().Name + ": "
                    + exception.Message + "\n" + exception.StackTrace;
                consoleIssues.Add("Exception: " + detail);
                Check(false, "RUNNER_UNHANDLED_EXCEPTION", detail);
                yield break;
            }

            if (yielded is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }
            yield return yielded;
        }
    }

    private void FinishRun()
    {
        if (finished) return;
        finished = true;
        try
        {
            RestoreBaselineBestEffort();
            RestorePausedAi();
        }
        catch (Exception exception)
        {
            string detail = exception.GetType().Name + ": " + exception.Message;
            consoleIssues.Add("Exception during cleanup: " + detail);
            failures.Add("CLEANUP_EXCEPTION: " + detail);
        }
        Application.logMessageReceived -= CaptureIssue;
        Time.timeScale = oldTimeScale;
        if (!reportWritten)
        {
            WriteReport();
            reportWritten = true;
        }
        bool passed = failures.Count == 0;
        Destroy(gameObject);
        if (ExitBatchProcessOnCompletion)
        {
            DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
            EditorApplication.delayCall += () =>
                EditorApplication.Exit(passed ? 0 : 1);
            return;
        }
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        };
    }

    private void OnDisable()
    {
        if (!finished)
        {
            failures.Add("RUNNER_DISABLED_BEFORE_COMPLETION");
            FinishRun();
        }
    }

    private IEnumerator ResolveWorld()
    {
        float deadline = Time.realtimeSinceStartup + SetupTimeout;
        bool prepared = false;
        CharacterActor actor = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            if (scope?.Container != null
                && LiveActors().Length == 0
                && !prepared)
            {
                prepared = true;
                rows.Add("INFO\tSTART_PARTY\t"
                    + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            }
            actor = LiveActors().FirstOrDefault(candidate =>
                candidate.Brain != null
                && candidate.BehaviorTree != null
                && candidate.Stats != null);
            if (scope?.Container != null && actor != null) break;
            yield return null;
        }

        Check(scope?.Container != null, "LIVE_SCOPE", scope?.name ?? "missing");
        Check(actor != null, "LIVE_ACTOR", actor?.name ?? "missing");
        if (scope?.Container == null || actor == null) yield break;

        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        deprivation = scope.Container.Resolve<ICharacterDeprivationRuntime>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        repository = scope.Container.Resolve<WorldItemRepository>();
        reservations = scope.Container.Resolve<IItemQuantityReservationService>();
        filth = scope.Container.Resolve<IWorldFilthQuery>();
        buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        gridSystems = scope.Container.Resolve<IGridSystemProvider>();
        randomStreams = scope.Container.Resolve<IRandomStreamProvider>();
        outcomeDiagnostics =
            scope.Container.Resolve<IGameplayOutcomeDiagnosticsQuery>();
        IEnvironmentalFieldQuery environment =
            scope.Container.Resolve<IEnvironmentalFieldQuery>();
        float readinessDeadline = Time.realtimeSinceStartup + SetupTimeout;
        while (!environment.IsInitialized
               && Time.realtimeSinceStartup < readinessDeadline)
        {
            yield return null;
        }
        Check(environment.IsInitialized,
            "SAVE_RUNTIME_READY",
            $"environmentInitialized={environment.IsInitialized};version={environment.Version}");
        if (!environment.IsInitialized) yield break;
        actorId = CharacterPersistentIdentity.Require(actor).Value;
        PauseOtherAi(actorId);
        actor.SetAiPaused(false);
        actor.Brain.enabled = true;
        actor.BehaviorTree.enabled = true;
        baseline = saves.Capture();
        Check(baseline != null, "V18_BASELINE_CAPTURE", baseline != null ? actorId : "missing");
    }

    private IEnumerator VerifyRow(
        string prefix,
        DeprivationKind cause,
        CharacterBreakdownKind kind,
        CharacterCondition condition,
        string fixtureItemId)
    {
        if (!TryRestoreBaselineAndAuthorBreakdown(
                prefix,
                cause,
                kind,
                condition,
                out CharacterActor actor,
                out string producerDetail))
        {
            yield break;
        }

        WorldItemStackSnapshot fixture = null;
        bool corpseRow = fixtureItemId == DarkSurvivalItemDefinitions.HumanoidCorpseItemId;
        if (corpseRow)
        {
            actor.SetAiPaused(true);
            // Official restore creates fresh actors whose lifecycle/grid startup
            // may still be pending in this frame. Let that startup complete before
            // authoring the controlled death position; do not relocate the corpse.
            yield return null;
            CharacterActor victim = LiveActors().FirstOrDefault(candidate =>
                candidate != actor && !CharacterPersistentIdentity.Require(candidate).Equals(CharacterId.Owner));
            Check(victim != null, prefix + "_CORPSE_SOURCE_ACTOR", victim?.name ?? "missing");
            if (victim == null) yield break;
            string victimId = CharacterPersistentIdentity.Require(victim).Value;
            actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            victim.GetAbility<AbilityMove>()?.CancelActiveMovement();
            if (!gridSystems.TryGetGrid(out Grid corpseGrid))
            {
                Check(false, prefix + "_CORPSE_GRID_READY", "missing");
                yield break;
            }
            Vector2Int deathCell = actor.GetNowXY();
            victim.transform.position = corpseGrid.GetWorldPos(deathCell);
            bool positioned = victim.GetNowXY() == deathCell;
            Check(positioned, prefix + "_DEATH_POSITION_READY",
                $"actor={actor.GetNowXY()};victim={victim.GetNowXY()};expected={deathCell}");
            if (!positioned) yield break;
            scope.Container.Resolve<ICharacterBodyHealthCommand>().ApplyLegacyDamage(
                victim, victim.CurrentHealth + 100f, "qa:corpse-eating-prerequisite", allowDeath: true);
            float corpseDeadline = Time.realtimeSinceStartup + SetupTimeout;
            while (Time.realtimeSinceStartup < corpseDeadline)
            {
                fixture = items.GetAllStacks().FirstOrDefault(candidate =>
                    candidate != null && candidate.ItemId == fixtureItemId
                    && candidate.SourceCharacterId == victimId);
                if (fixture != null) break;
                yield return null;
            }
            Check(victim.IsDead && fixture?.SourceCharacterId == victimId
                    && fixture.Position == deathCell && fixture.TotalQuantity == 1,
                prefix + "_ACTUAL_DEATH_CORPSE",
                $"dead={victim.IsDead};victim={victimId};stack={fixture?.StackId};quantity={fixture?.TotalQuantity};"
                    + $"deathCell={deathCell};corpseCell={fixture?.Position};actorCell={actor.GetNowXY()};victimCell={victim.GetNowXY()}");
            if (fixture == null || !victim.IsDead || fixture.Position != deathCell) yield break;
            var actual = (CharacterDeprivationRuntime)deprivation;
            bool selected = actual.DebugTryFindEmergencyFood(actor, out WorldItemStackSnapshot planned)
                && planned.StackId == fixture.StackId;
            Check(selected, prefix + "_PRODUCTION_FOOD_SELECTION",
                $"expected={fixture.StackId};selected={planned?.StackId};item={planned?.ItemId}");
            if (!selected) yield break;
            VerifyCorpseCommitRejection(actor, fixture);
            if (failures.Count > 0) yield break;
            Check(scope.Container.Resolve<ISurgeryPersistence>().Capture().corpseFreshness
                    .Any(entry => entry.stackId == fixture.StackId),
                prefix + "_SURGERY_LIVE_CORPSE_TRACKED", fixture.StackId);
            if (failures.Count > 0) yield break;
            WriteCreateOnly("corpse-before-world.json", saves.ToJson(saves.Capture()));
            actor.SetAiPaused(false);
        }
        else if (!string.IsNullOrWhiteSpace(fixtureItemId))
        {
            string stackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                fixtureItemId,
                1,
                WorldItemStackState.Loose,
                position: actor.GetNowXY());
            fixture = items.GetAllStacks().FirstOrDefault(stack =>
                stack?.StackId == stackId);
            Check(fixture != null,
                prefix + "_PHYSICAL_FIXTURE",
                fixture != null
                    ? $"stack={fixture.StackId};item={fixture.ItemId};qty={fixture.TotalQuantity}"
                    : "seed failed");
            if (fixture == null) yield break;
        }

        AIBrain brain = actor.Brain;
        CharacterBlackboard blackboard = actor.Blackboard;
        long handledBefore = blackboard.GetHandledDecisionCount(
            CharacterAiBranch.DeprivationBreakdown);
        long typedHandledBefore =
            blackboard.GetHandledDeprivationBreakdownCount(kind);
        int terminalBefore = brain.ExternalIntentTerminalCount;
        int filthBefore = filth.GetAll().Count;
        int activityBefore = actor.LogComponent?.ActivityEntries.Count ?? 0;
        float needBefore = GetNeed(actor, condition);
        BuildableObject vandalismTarget = null;
        long vandalismSequenceBefore = 0L;
        if (cause == DeprivationKind.MentalInstability)
        {
            vandalismTarget = FindVandalismTarget(actor);
            Check(vandalismTarget != null,
                prefix + "_FACILITY_TARGET",
                vandalismTarget != null
                    ? vandalismTarget.RequirePersistentInstanceId().Value
                    : "no eligible facility");
            if (vandalismTarget == null)
                yield break;
            bool stagedNearFacility = TryStageNearFacility(
                actor,
                vandalismTarget,
                out string stagingDetail);
            Check(stagedNearFacility,
                prefix + "_FACILITY_STAGING",
                stagingDetail);
            if (!stagedNearFacility)
                yield break;

            // Resolve once more from the production ordering at the staged
            // position so the asserted owner is exactly the target the domain
            // runner will select. The live BT and movement/commit coroutine are
            // still responsible for the terminal transition.
            vandalismTarget = FindVandalismTarget(actor);
            Check(vandalismTarget != null,
                prefix + "_STAGED_FACILITY_TARGET",
                vandalismTarget != null
                    ? vandalismTarget.RequirePersistentInstanceId().Value
                    : "no eligible facility after staging");
            if (vandalismTarget == null)
                yield break;
            vandalismSequenceBefore =
                vandalismTarget.NextVandalismOperationSequence;
            randomStreams.Get("character-deprivation").Restore(1UL);
        }
        CharacterAiRuntimeDiagnosticsSnapshot diagnosticsBefore =
            brain.CaptureRuntimeDiagnostics();
        string observedRoute = string.Empty;
        long observedExternalEpoch = 0L;
        bool observedExternal = false;

        brain.RequestImmediateReplan(clearFailures: true);
        float deadline = Time.realtimeSinceStartup + RowTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            long handled = blackboard.GetHandledDecisionCount(
                CharacterAiBranch.DeprivationBreakdown);
            if (handled > handledBefore)
                observedRoute = blackboard.LastDecisionRouteSummary;
            if (brain.IsExternallyDrivenActionActive
                && string.Equals(
                    brain.ExternalIntentOwnerId,
                    BreakdownOwnerId,
                    StringComparison.Ordinal))
            {
                observedExternal = true;
                observedExternalEpoch = brain.ExternalIntentEpoch;
            }

            if (handled > handledBefore
                && brain.ExternalIntentTerminalCount > terminalBefore
                && !brain.IsExternallyDrivenActionActive
                && !deprivation.HasActiveBreakdown(actor))
            {
                break;
            }
            yield return null;
        }

        long handledAfter = blackboard.GetHandledDecisionCount(
            CharacterAiBranch.DeprivationBreakdown);
        long typedHandledAfter =
            blackboard.GetHandledDeprivationBreakdownCount(kind);
        CharacterAiRuntimeDiagnosticsSnapshot diagnosticsAfter =
            brain.CaptureRuntimeDiagnostics();
        float needAfter = GetNeed(actor, condition);
        bool activeAfter = deprivation.HasActiveBreakdown(actor);
        bool branchObserved = handledAfter == handledBefore + 1
            && typedHandledAfter == typedHandledBefore + 1
            && blackboard.LastHandledDeprivationBreakdownKind == kind;
        Check(branchObserved,
            prefix + "_PRODUCTION_BT_ROUTE",
            $"producer={producerDetail};handled={handledBefore}->{handledAfter};"
            + $"typed[{kind}]={typedHandledBefore}->{typedHandledAfter};"
            + $"lastTyped={blackboard.LastHandledDeprivationBreakdownKind};"
            + $"finalRoute={observedRoute}");
        Check(observedExternal,
            prefix + "_EXTERNAL_ACTION_STARTED",
            $"owner={BreakdownOwnerId};epoch={observedExternalEpoch};phase={brain.CurrentActionPhase}");
        Check(brain.ExternalIntentTerminalCount == terminalBefore + 1
              && brain.LastExternalIntentTerminalKind ==
                  CharacterAiActionTerminalKind.Completed,
            prefix + "_TYPED_TERMINAL",
            $"count={terminalBefore}->{brain.ExternalIntentTerminalCount};kind={brain.LastExternalIntentTerminalKind}");
        Check(!activeAfter && !brain.IsExternallyDrivenActionActive,
            prefix + "_DOMAIN_TERMINAL",
            $"breakdownActive={activeAfter};external={brain.IsExternallyDrivenActionActive}");

        VerifyDomainOutcome(
            prefix,
            cause,
            condition,
            actor,
            fixture,
            needBefore,
            needAfter,
            filthBefore,
            activityBefore);

        if (cause == DeprivationKind.MentalInstability)
        {
            FacilityVandalismCommitState committed =
                vandalismTarget.LastVandalism;
            GameplayResultKey expected = FacilityVandalismOutcomeIds.ResultKey(
                vandalismTarget.RequirePersistentInstanceId(),
                vandalismSequenceBefore);
            bool exactOutcome = outcomeDiagnostics.TryGetResultIdentity(
                expected,
                out GameplayOutcomeReplayIdentity identity)
                && (int)identity.State
                    >= (int)GameplayOutcomeReplayState.Committed;
            Check(vandalismTarget.IsDamaged
                    && vandalismTarget.NextVandalismOperationSequence
                        == vandalismSequenceBefore + 1L
                    && committed.operationSequence
                        == vandalismSequenceBefore
                    && committed.actorId == actorId
                    && committed.source == "deprivation"
                    && committed.reason == "mental-instability"
                    && committed.publicationKey == expected.ToString()
                    && exactOutcome,
                prefix + "_FACILITY_OUTCOME",
                $"facility={vandalismTarget.RequirePersistentInstanceId().Value};"
                    + $"sequence={vandalismSequenceBefore}->{vandalismTarget.NextVandalismOperationSequence};"
                    + $"actor={committed.actorId};source={committed.source};"
                    + $"reason={committed.reason};result={committed.publicationKey};"
                    + $"exactOutcome={exactOutcome}");
        }

        bool fixtureLeaseClean = fixture == null
            || reservations.GetLeasesForStack(
                    new ItemStackId(fixture.StackId)).Count == 0;
        Check(diagnosticsAfter.Gate.LivePathRequests == 0
              && diagnosticsAfter.Gate.LiveReservations == 0
              && fixtureLeaseClean,
            prefix + "_OWNERSHIP_CLEANUP",
            $"paths={diagnosticsAfter.Gate.LivePathRequests};brainReservations={diagnosticsAfter.Gate.LiveReservations};fixtureLeaseClean={fixtureLeaseClean}");
        Check(diagnosticsAfter.Gate.InvariantAnomalies ==
              diagnosticsBefore.Gate.InvariantAnomalies,
            prefix + "_NO_LIFECYCLE_ANOMALY",
            diagnosticsAfter.FormatDeltaFrom(in diagnosticsBefore));

        if (corpseRow)
            VerifyCorpseSaveAndReplay(actor, fixture);

        if (cause == DeprivationKind.MentalInstability
            && CharacterDeprivationProductionBtPlayModeVerifier
                .VandalismOnlyRequested)
        {
            VerifyVandalismSaveRoundTrip(
                prefix,
                vandalismTarget,
                vandalismSequenceBefore,
                actorId,
                FacilityVandalismSourceKind.Deprivation,
                "mental-instability");
        }
    }

    private void VerifyCorpseCommitRejection(CharacterActor actor, WorldItemStackSnapshot corpse)
    {
        var actual = (CharacterDeprivationRuntime)deprivation;
        var ledger = scope.Container.Resolve<IGameplayOutcomePersistence>();
        string statsBefore = CorpseStatsFingerprint(actor);
        string domainBefore = JsonUtility.ToJson(actual.Capture());
        string ledgerBefore = string.Join("\n", ledger.CaptureGameplayOutcomes().exactOutcomes
            .Select(entry => JsonUtility.ToJson(entry)));
        string physicalBefore = JsonUtility.ToJson(items.Capture());
        Func<GameplayResultKey, bool> previous = CharacterCorpseConsumptionFactObserver.DebugRejectCommit;
        string rejectedOperation = "qa-corpse-reject:" + actorId + ":" + corpse.StackId;
        bool rejected = false;
        string failure = string.Empty;
        try
        {
            CharacterCorpseConsumptionFactObserver.DebugRejectCommit = key =>
                key.OperationId.Value == rejectedOperation || previous?.Invoke(key) == true;
            try { actual.DebugTryConsumeHumanoidCorpse(actor, corpse, rejectedOperation); }
            catch (InvalidOperationException exception)
            {
                failure = exception.Message;
                rejected = failure.Contains("qa-corpse-consumption-atomic-commit-rejected");
            }
        }
        finally { CharacterCorpseConsumptionFactObserver.DebugRejectCommit = previous; }
        string statsAfter = CorpseStatsFingerprint(actor);
        string domainAfter = JsonUtility.ToJson(actual.Capture());
        string ledgerAfter = string.Join("\n", ledger.CaptureGameplayOutcomes().exactOutcomes
            .Select(entry => JsonUtility.ToJson(entry)));
        string physicalAfter = JsonUtility.ToJson(items.Capture());
        Check(rejected && statsBefore == statsAfter && domainBefore == domainAfter
                && ledgerBefore == ledgerAfter && physicalBefore == physicalAfter
                && outcomeDiagnostics.GetDiagnostics().ReservedCount == 0,
            "CORPSE_EAT_A04_REJECT_ROLLBACK",
            $"rejected={rejected};statsUnchanged={statsBefore == statsAfter};"
                + $"domainUnchanged={domainBefore == domainAfter};ledgerUnchanged={ledgerBefore == ledgerAfter};"
                + $"physicalUnchanged={physicalBefore == physicalAfter};reason={failure}");
    }

    private static string CorpseStatsFingerprint(CharacterActor actor)
    {
        CharacterMoodDeliveryTransactionSnapshot value = actor.Stats.CaptureMoodDeliveryTransactionState();
        return string.Join("|", value.Stats.OrderBy(pair => (int)pair.Key)
                .Select(pair => ((int)pair.Key) + "=" + pair.Value.ToString("R")))
            + ";health=" + value.CurrentHealth.ToString("R")
            + ";injury=" + value.InjurySeverity.ToString("R")
            + ";baseMood=" + value.BaseMood.ToString("R")
            + ";lastMood=" + value.LastCalculatedMood.ToString("R")
            + ";factors=" + string.Join("|", value.Factors.Select(factor => JsonUtility.ToJson(factor)));
    }

    private void VerifyCorpseSaveAndReplay(CharacterActor actor, WorldItemStackSnapshot corpse)
    {
        actor.SetAiPaused(true);
        var ledger = scope.Container.Resolve<IGameplayOutcomePersistence>();
        string operation = "breakdown-eat:" + actorId + ":" + corpse.StackId;
        GameplayOutcomeSnapshot[] rowsNow = ledger.CaptureGameplayOutcomes().exactOutcomes.ToArray();
        GameplayOutcomeSnapshot[] parents = rowsNow.Where(entry => entry.outcomeTypeId == "survival.taboo-incident"
            && entry.facts.Any(fact => fact.factId == MigratedProducerOutcomeIds.SourceStackFact.Value
                && fact.value == corpse.StackId)).ToArray();
        GameplayOutcomeSnapshot[] physicals = rowsNow.Where(entry =>
            entry.operationId == operation
            && entry.producerId == TradeInventoryOutcomeDefinitions.Get(
                TradeInventoryOutcomeKind.PhysicalItemBatchDisposition).ProducerId).ToArray();
        Check(parents.Length == 1 && physicals.Length == 1,
            "CORPSE_EAT_A04_EXACT_ONCE", $"taboo={parents.Length};physical={physicals.Length};operation={operation}");
        if (parents.Length != 1 || physicals.Length != 1) return;
        GameplayOutcomeSnapshot parent = parents[0];
        GameplayOutcomeSnapshot physical = physicals[0];
        var physicalKey = new GameplayResultKey(physical.producerId,
            new GameplayOperationId(physical.operationId), physical.commitRevision, physical.localResultIndex);
        bool actorLinked = parent.participants.Any(p => p.entityKindId == "character"
            && p.entityId == actorId && p.roleId == "actor");
        bool victimLinked = parent.participants.Any(p => p.entityKindId == "character"
            && p.entityId == corpse.SourceCharacterId && p.roleId == "target");
        bool stackLinked = parent.participants.Any(p => p.entityKindId == "item-stack"
            && p.entityId == corpse.StackId && p.roleId == "source");
        Check(actorLinked && victimLinked && stackLinked
                && parent.parentRunId == physical.runId && parent.parentSequence == physical.sequence
                && parent.rootOperationId == operation
                && parent.causationRelationId == "corpse-eat-from-physical-sink"
                && parent.facts.Any(f => f.factId == "physical-result-key" && f.value == physicalKey.ToString()),
            "CORPSE_EAT_A04_TYPED_CAUSAL_JOIN",
            $"actor={actorId};victim={corpse.SourceCharacterId};stack={corpse.StackId};"
                + $"physical={physical.runId}:{physical.sequence};taboo={parent.runId}:{parent.sequence};"
                + $"actorLinked={actorLinked};victimLinked={victimLinked};stackLinked={stackLinked}");
        double Metric(GameplayMetricId id) => parent.metrics.Single(m => m.metricId == id.Value).value;
        double hungerBefore = Metric(MigratedProducerOutcomeIds.HungerBeforeMetric);
        double hungerAfter = Metric(MigratedProducerOutcomeIds.HungerAfterMetric);
        float expectedRecovery = scope.Container.Resolve<CharacterNeedStateService>().ApplyRecoveryMultiplier(
            CharacterCondition.HUNGER, 75f, CharacterNeedRecoverySource.Emergency);
        bool sameSpecies = string.Equals(actor.SpeciesTag, corpse.SourceSpeciesTag, StringComparison.OrdinalIgnoreCase);
        Check(Metric(MigratedProducerOutcomeIds.QuantityMetric) == 1d
                && Metric(MigratedProducerOutcomeIds.InputMassMetric) > 0d
                && Math.Abs(hungerAfter - Math.Min(100d, hungerBefore + expectedRecovery)) < 0.001d
                && Math.Abs(Metric(MigratedProducerOutcomeIds.InfectionAfterMetric)
                    - Math.Min(100d, Metric(MigratedProducerOutcomeIds.InfectionBeforeMetric)
                        + (sameSpecies ? 20d : 12d))) < 0.001d
                && items.GetAllStacks().All(stack => stack.StackId != corpse.StackId),
            "CORPSE_EAT_A04_PHYSICAL_AND_RECOVERY",
            $"quantity=1;hunger={hungerBefore}->{hungerAfter};authoredRecovery={expectedRecovery};"
                + $"infection={Metric(MigratedProducerOutcomeIds.InfectionBeforeMetric)}"
                + $"->{Metric(MigratedProducerOutcomeIds.InfectionAfterMetric)}");
        var actual = (CharacterDeprivationRuntime)deprivation;
        Check(actual.Capture().characters.Single(c => c.characterId == actorId).tabooMemories
                .Contains($"극한의 굶주림 속에서 {corpse.SourceDisplayName}을 먹었다"),
            "CORPSE_EAT_A04_TABOO_MEMORY", corpse.SourceCharacterId);
        WriteCreateOnly("corpse-taboo-outcome.json", JsonUtility.ToJson(parent, true));
        WriteCreateOnly("corpse-physical-outcome.json", JsonUtility.ToJson(physical, true));
        Check(scope.Container.Resolve<ISurgeryPersistence>().Capture().corpseFreshness
                .All(entry => entry.stackId != corpse.StackId),
            "CORPSE_EAT_A04_SURGERY_CONSUMED_REFERENCE_REMOVED", corpse.StackId);
        string worldJson = saves.ToJson(saves.Capture());
        WriteCreateOnly("corpse-completed-world.json", worldJson);
        string parentJson = JsonUtility.ToJson(parent);
        string physicalJson = JsonUtility.ToJson(physical);
        bool restored = saves.TryRestore(saves.FromJson(worldJson), out DungeonGameRestoreReport report);
        string restoreDetail = report == null ? "no report"
            : $"success={report.Success};errors={string.Join(" | ", report.Errors)};"
                + $"warnings={string.Join(" | ", report.Warnings)};"
                + $"characters={report.RestoredCharacterCount};buildings={report.RestoredBuildingCount}";
        WriteCreateOnly("corpse-restore-diagnostics.txt", restoreDetail);
        Check(restored, "CORPSE_EAT_A04_OFFICIAL_JSON_RESTORE", restoreDetail);
        if (!restored) return;
        CharacterActor fresh = FindActor(actorId);
        if (fresh == null) { Check(false, "CORPSE_EAT_A04_FRESH_ACTOR", "missing"); return; }
        fresh.SetAiPaused(true);
        Check(!ReferenceEquals(fresh, actor), "CORPSE_EAT_A04_FRESH_ACTOR", actorId);
        GameplayOutcomeSnapshot[] restoredRows = ledger.CaptureGameplayOutcomes().exactOutcomes.ToArray();
        Check(restoredRows.Count(entry => JsonUtility.ToJson(entry) == parentJson) == 1
                && restoredRows.Count(entry => JsonUtility.ToJson(entry) == physicalJson) == 1
                && items.GetAllStacks().All(stack => stack.StackId != corpse.StackId),
            "CORPSE_EAT_A04_EXACT_RESTORE", "same typed taboo/physical identity;consumed corpse absent");
        string statsBefore = CorpseStatsFingerprint(fresh);
        string domainBefore = JsonUtility.ToJson(actual.Capture());
        string ledgerBefore = JsonUtility.ToJson(ledger.CaptureGameplayOutcomes());
        string itemsBefore = JsonUtility.ToJson(items.Capture());
        DungeonGameSaveData invalidWorld = saves.FromJson(worldJson);
        DungeonSaveSectionEnvelope surgerySection = invalidWorld.sections.Single(section =>
            section.sectionId == "medical.surgery");
        DungeonSurgerySaveData invalidSurgery = JsonUtility.FromJson<DungeonSurgerySaveData>(
            surgerySection.payloadJson);
        invalidSurgery.corpseFreshness.Add(new SurgicalCorpseFreshnessState
            { stackId = corpse.StackId, remainingFreshnessSeconds = 10f });
        surgerySection.payloadJson = JsonUtility.ToJson(invalidSurgery);
        invalidWorld.manifest = DungeonSaveManifest.Capture(invalidWorld.sections);
        bool invalidRestored = saves.TryRestore(invalidWorld, out DungeonGameRestoreReport invalidReport);
        Check(!invalidRestored && invalidReport.Errors.Any(error =>
                error.Contains("Corpse freshness references missing incoming stack"))
                && ReferenceEquals(fresh, FindActor(actorId))
                && statsBefore == CorpseStatsFingerprint(fresh)
                && domainBefore == JsonUtility.ToJson(actual.Capture())
                && ledgerBefore == JsonUtility.ToJson(ledger.CaptureGameplayOutcomes())
                && itemsBefore == JsonUtility.ToJson(items.Capture()),
            "CORPSE_EAT_A04_INVALID_STALE_REFERENCE_REJECTED",
            $"restored={invalidRestored};errors={string.Join(" | ", invalidReport.Errors)};live unchanged");
        bool replay = actual.DebugTryConsumeHumanoidCorpse(fresh, corpse, operation);
        bool command = deprivation.TryRunActiveBreakdown(fresh, out _);
        Check(!replay && !command && statsBefore == CorpseStatsFingerprint(fresh)
                && domainBefore == JsonUtility.ToJson(actual.Capture())
                && ledgerBefore == JsonUtility.ToJson(ledger.CaptureGameplayOutcomes())
                && itemsBefore == JsonUtility.ToJson(items.Capture()),
            "CORPSE_EAT_A04_RESTORE_DUPLICATE_INERT",
            $"sameConsumptionReplay={replay};publicBreakdownCommand={command};effects/physical/ledger unchanged");
        WriteCreateOnly("corpse-restored-world.json", saves.ToJson(saves.Capture()));
    }

    private static void WriteCreateOnly(string name, string content)
    {
        string path = Path.Combine(Path.GetDirectoryName(
            CharacterDeprivationProductionBtPlayModeVerifier.ActiveReportPath), name);
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(file, new UTF8Encoding(false));
        writer.Write(content);
    }

    private void VerifyVandalismSaveRoundTrip(
        string prefix,
        BuildableObject expectedFacility,
        long operationSequence,
        string expectedActorId,
        FacilityVandalismSourceKind source,
        string reason)
    {
        BuildingInstanceId facilityId =
            expectedFacility.RequirePersistentInstanceId();
        bool gridResolved = gridSystems.TryGetGrid(out Grid grid)
            && grid != null;
        BuildableObject[] gridFacilities = gridResolved
            ? grid.FindAllOccupants(null)
                .OfType<BuildableObject>()
                .Distinct()
                .ToArray()
            : Array.Empty<BuildableObject>();
        int gridIdMatches = gridFacilities.Count(candidate =>
            candidate != null
            && candidate.PersistentInstanceId.Equals(facilityId));
        int registryIdMatches = buildings.Buildings.Count(candidate =>
            candidate != null
            && candidate.PersistentInstanceId.Equals(facilityId));
        bool sameGridReference = gridFacilities.Any(candidate =>
            ReferenceEquals(candidate, expectedFacility));
        Check(gridResolved
                && sameGridReference
                && gridIdMatches == 1
                && registryIdMatches == 1,
            prefix + "_CURRENT_GRID_FACILITY",
            $"facility={facilityId.Value};gridResolved={gridResolved};"
                + $"sameReference={sameGridReference};"
                + $"gridIdMatches={gridIdMatches};"
                + $"registryIdMatches={registryIdMatches};"
                + $"gridFacilities={gridFacilities.Length};"
                + $"registryFacilities={buildings.Buildings.Count}");

        DungeonGameSaveData committedSave = saves.Capture();
        DungeonSaveSectionEnvelope[] facilitySections = committedSave?.sections
            ?.Where(entry => entry != null
                && string.Equals(
                    entry.sectionId,
                    ModularFacilityWorldSaveSection.Id,
                    StringComparison.Ordinal))
            .ToArray()
            ?? Array.Empty<DungeonSaveSectionEnvelope>();
        ModularFacilityWorldSaveData savedFacilities =
            facilitySections.Length == 1
                ? JsonUtility.FromJson<ModularFacilityWorldSaveData>(
                    facilitySections[0].payloadJson)
                : null;
        ModularFacilityBuildingSaveData[] savedRows = savedFacilities?.buildings
            ?.Where(entry => entry != null
                && string.Equals(
                    entry.persistentInstanceId,
                    facilityId.Value,
                    StringComparison.Ordinal))
            .ToArray()
            ?? Array.Empty<ModularFacilityBuildingSaveData>();
        Check(committedSave != null
                && facilitySections.Length == 1
                && savedRows.Length == 1
                && savedRows[0].isDamaged,
            prefix + "_FACILITY_SAVE_ROW",
            $"facility={facilityId.Value};"
                + $"sectionCount={facilitySections.Length};"
                + $"rowCount={savedRows.Length};"
                + $"savedDamaged={(savedRows.Length == 1 && savedRows[0].isDamaged)};"
                + $"stateModules={(savedRows.Length == 1 ? savedRows[0].stateModules?.Count ?? 0 : 0)};"
                + $"savedFacilities={savedFacilities?.buildings?.Count ?? 0}");
        bool restored = committedSave != null
            && saves.TryRestore(
                CloneSave(committedSave),
                out DungeonGameRestoreReport report);
        BuildableObject restoredFacility = restored
            ? buildings.Buildings.FirstOrDefault(candidate =>
                candidate != null
                && candidate.PersistentInstanceId.Equals(facilityId))
            : null;
        FacilityVandalismCommitState committed =
            restoredFacility?.LastVandalism;
        GameplayResultKey expected = FacilityVandalismOutcomeIds.ResultKey(
            facilityId,
            operationSequence);
        bool exactOutcome = restored
            && outcomeDiagnostics.TryGetResultIdentity(
                expected,
                out GameplayOutcomeReplayIdentity identity)
            && (int)identity.State
                >= (int)GameplayOutcomeReplayState.Committed;
        bool valid = restoredFacility != null
            && restoredFacility.IsDamaged
            && restoredFacility.NextVandalismOperationSequence
                == operationSequence + 1L
            && committed != null
            && committed.operationSequence == operationSequence
            && committed.actorId == expectedActorId
            && committed.source == FacilityVandalismOutcomeIds.Source(source)
            && committed.reason == reason
            && committed.publicationKey == expected.ToString()
            && exactOutcome;
        Check(valid,
            prefix + "_SAVE_ROUND_TRIP",
            $"restored={restored};facility={facilityId.Value};"
                + $"sequence={operationSequence}->{restoredFacility?.NextVandalismOperationSequence};"
                + $"actor={committed?.actorId};source={committed?.source};"
                + $"reason={committed?.reason};result={committed?.publicationKey};"
                + $"exactOutcome={exactOutcome}");
    }

    private bool TryRestoreBaselineAndAuthorBreakdown(
        string prefix,
        DeprivationKind cause,
        CharacterBreakdownKind kind,
        CharacterCondition condition,
        out CharacterActor actor,
        out string producerDetail)
    {
        actor = null;
        producerDetail =
            "V18 aggregate restore + explicit QA CharacterStats prerequisite; "
            + "deterministic active breakdown cannot be reached in a bounded QA row through 30 real-time held seconds";
        DungeonGameRestoreReport resetReport = null;
        if (baseline == null
            || !saves.TryRestore(CloneSave(baseline), out resetReport))
        {
            Check(false,
                prefix + "_BASELINE_RESTORE",
                resetReport?.ToString() ?? "restore failed");
            return false;
        }

        actor = FindActor(actorId);
        if (actor == null)
        {
            Check(false, prefix + "_RESTORED_ACTOR", actorId);
            return false;
        }
        PauseOtherAi(actorId);
        actor.SetAiPaused(true);
        SetNeedForFixture(actor, condition, 5f);

        DungeonGameSaveData authored = saves.Capture();
        DungeonSaveSectionEnvelope envelope = authored.sections.FirstOrDefault(entry =>
            entry != null
            && string.Equals(
                entry.sectionId,
                DarkSurvivalSaveSection.Id,
                StringComparison.Ordinal));
        if (envelope == null)
        {
            Check(false, prefix + "_DEPRIVATION_SECTION", "missing");
            return false;
        }

        DungeonDarkSurvivalSaveData payload =
            JsonUtility.FromJson<DungeonDarkSurvivalSaveData>(
                envelope.payloadJson)
            ?? new DungeonDarkSurvivalSaveData();
        CharacterDeprivationState state = payload.characters.FirstOrDefault(entry =>
            string.Equals(entry?.characterId, actorId, StringComparison.Ordinal));
        if (state == null)
        {
            state = new CharacterDeprivationState { characterId = actorId };
            payload.characters.Add(state);
        }
        state.burdens = Enum.GetValues(typeof(DeprivationKind))
            .Cast<DeprivationKind>()
            .Select(entry => new DeprivationBurdenSaveData
            {
                kind = entry,
                burden = entry == cause ? 100f : 0f,
                maximumHeldSeconds = entry == cause ? 30f : 0f,
                nextBreakdownCheckAt = Time.time + 30f,
                nextDamageAt = Time.time + 30f
            })
            .ToList();
        state.breakdown = new CharacterBreakdownState
        {
            active = true,
            kind = kind,
            cause = cause,
            startedAt = Time.time,
            suppressionResistance = 35f,
            lastReplanReason = "qa:v18-production-bt"
        };
        state.breakdownGeneration = Math.Max(1, state.breakdownGeneration + 1);
        state.dispatchedBreakdownGeneration = state.breakdownGeneration;
        envelope.payloadJson = JsonUtility.ToJson(payload);
        authored.manifest = DungeonSaveManifest.Capture(authored.sections);
        if (!saves.TryRestore(authored, out DungeonGameRestoreReport authoredReport))
        {
            Check(false,
                prefix + "_V18_BREAKDOWN_PRODUCER",
                authoredReport?.ToString() ?? "restore failed");
            actor = null;
            return false;
        }

        actor = FindActor(actorId);
        if (actor != null)
        {
            PauseOtherAi(actorId);
            actor.SetAiPaused(true);

            // CharacterStats is not owned by the V18 deprivation section. The
            // aggregate restore recreates the actor at its persisted baseline,
            // so the production stat command must be applied again after the
            // authored V18 payload is restored. Otherwise the active breakdown
            // is immediately relieved at the baseline need value before the
            // Behavior Designer branch can observe it.
            SetNeedForFixture(actor, condition, 5f);
        }

        float authoredNeed = actor != null
            ? GetNeed(actor, condition)
            : float.NaN;
        bool authoredBreakdownActive = actor != null
            && deprivation.HasActiveBreakdown(actor);
        bool authoredBreakdownKind = actor != null
            && deprivation.HasBreakdownKind(actor, kind);
        bool valid = actor != null
            // Mental instability uses the aggregate mood after authored need
            // factors. Assigning the base prerequisite to 5 can legitimately
            // recalculate to a value above 5; the production breakdown stays
            // active while that aggregate remains below the relief boundary.
            && authoredNeed < 30f
            && authoredBreakdownKind;
        Check(valid,
            prefix + "_V18_BREAKDOWN_PRODUCER",
            valid
                ? $"kind={kind};cause={cause};need={authoredNeed:0.##};reason={producerDetail}"
                : $"actor={(actor != null ? actor.name : "missing")};"
                    + $"need={authoredNeed:0.##};active={authoredBreakdownActive};"
                    + $"kind={authoredBreakdownKind};"
                    + $"restoreSuccess={authoredReport?.Success};"
                    + $"restoreErrors={string.Join("|", authoredReport?.Errors ?? Array.Empty<string>())}");
        if (!valid) return false;

        AIBrain restoredBrain = actor.Brain;
        string restoredAction = restoredBrain?.CurrentActionDebugLabel
            ?? string.Empty;
        bool hadRunningAction = restoredBrain?.HasRunningAction == true;
        if (restoredBrain != null)
        {
            if (hadRunningAction)
            {
                restoredBrain.StopCurrentActionForReplan(
                    "QA authored deprivation breakdown takes ownership");
            }
            else
            {
                restoredBrain.RequestImmediateReplan(clearFailures: true);
            }
        }
        bool survivedActionReset = deprivation.HasBreakdownKind(actor, kind);
        Check(survivedActionReset,
            prefix + "_V18_BREAKDOWN_ACTION_RESET",
            $"hadRunning={hadRunningAction};action={restoredAction};"
                + $"activeAfterReset={survivedActionReset}");
        if (!survivedActionReset) return false;
        actor.SetAiPaused(false);
        actor.Brain.enabled = true;
        actor.BehaviorTree.enabled = true;
        return true;
    }

    private void VerifyDomainOutcome(
        string prefix,
        DeprivationKind cause,
        CharacterCondition condition,
        CharacterActor actor,
        WorldItemStackSnapshot fixture,
        float needBefore,
        float needAfter,
        int filthBefore,
        int activityBefore)
    {
        if (cause == DeprivationKind.Bladder)
        {
            Check(needAfter >= 30f && filth.GetAll().Count > filthBefore,
                prefix + "_DOMAIN_OUTCOME",
                $"need={needBefore:0.##}->{needAfter:0.##};filth={filthBefore}->{filth.GetAll().Count}");
            return;
        }

        if (cause == DeprivationKind.MentalInstability)
        {
            deprivation.TryGetSnapshot(actor, out CharacterDeprivationSnapshot snapshot);
            float burden = snapshot.Burdens != null
                && snapshot.Burdens.TryGetValue(cause, out float value)
                    ? value
                    : float.NaN;
            int activityAfter = actor.LogComponent?.ActivityEntries.Count ?? 0;
            Check(burden <= 55.01f && activityAfter > activityBefore,
                prefix + "_DOMAIN_OUTCOME",
                $"burden={burden:0.##};activities={activityBefore}->{activityAfter}");
            return;
        }

        WorldItemStackSnapshot remaining = fixture == null
            ? null
            : items.GetAllStacks().FirstOrDefault(stack =>
                stack?.StackId == fixture.StackId);
        bool physicalConsumed = fixture == null
            || remaining == null
            || remaining.TotalQuantity < fixture.TotalQuantity;
        Check(needAfter >= 30f
              && (cause == DeprivationKind.Exhaustion || physicalConsumed),
            prefix + "_DOMAIN_OUTCOME",
            $"condition={condition};need={needBefore:0.##}->{needAfter:0.##};physicalConsumed={physicalConsumed}");
    }

    private void PauseOtherAi(string selectedActorId)
    {
        foreach (CharacterActor candidate in LiveActors())
        {
            string id = CharacterPersistentIdentity.TryGet(candidate, out CharacterId value)
                ? value.Value
                : string.Empty;
            if (string.Equals(id, selectedActorId, StringComparison.Ordinal))
                continue;
            RememberAndDisable(candidate.Brain);
            RememberAndDisable(candidate.BehaviorTree);
            candidate.SetAiPaused(true);
        }
    }

    private void RememberAndDisable(MonoBehaviour behaviour)
    {
        if (behaviour == null) return;
        if (!pausedAi.Any(entry => entry.Behaviour == behaviour))
            pausedAi.Add(new MonoBehaviourState(behaviour, behaviour.enabled));
        behaviour.enabled = false;
    }

    private void RestorePausedAi()
    {
        foreach (MonoBehaviourState state in pausedAi)
        {
            if (state.Behaviour != null)
                state.Behaviour.enabled = state.WasEnabled;
        }
        pausedAi.Clear();
    }

    private void RestoreBaselineBestEffort()
    {
        if (baseline == null || saves == null) return;
        try
        {
            if (!saves.TryRestore(CloneSave(baseline), out DungeonGameRestoreReport report))
                failures.Add("FINAL_BASELINE_RESTORE: " + report);
        }
        catch (Exception exception)
        {
            failures.Add("FINAL_BASELINE_RESTORE: " + exception.Message);
        }
    }

    private static DungeonGameSaveData CloneSave(DungeonGameSaveData source) =>
        JsonUtility.FromJson<DungeonGameSaveData>(JsonUtility.ToJson(source));

    private static CharacterActor FindActor(string persistentId) =>
        LiveActors().FirstOrDefault(candidate =>
            CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
            && string.Equals(id.Value, persistentId, StringComparison.Ordinal));

    private static CharacterActor[] LiveActors() =>
        UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(candidate => candidate != null
                && !candidate.IsDead
                && candidate.CurrentLifecycleState == CharacterLifecycleState.Active)
            .ToArray();

    private static float GetNeed(
        CharacterActor actor,
        CharacterCondition condition) =>
        actor?.Stats != null
        && actor.Stats.TryGetConditionValue(condition, out float value)
            ? value
            : float.NaN;

    private static void SetNeedForFixture(
        CharacterActor actor,
        CharacterCondition condition,
        float value)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        actor.EnsureRuntimeState();
        IDictionary<CharacterCondition, float> stats = actor.stats;
        if (stats == null)
            throw new InvalidOperationException(
                "CharacterStats prerequisite cannot be authored without runtime stats.");
        stats[condition] = Mathf.Clamp(value, 0f, 100f);
    }

    private BuildableObject FindVandalismTarget(CharacterActor actor)
    {
        if (actor == null || buildings == null)
            return null;
        Vector2Int origin = actor.GetNowXY();
        BuildableObject best = null;
        int bestDistance = int.MaxValue;
        foreach (BuildableObject candidate in buildings.Buildings)
        {
            if (candidate == null
                || candidate.IsGridDestroyed
                || !FacilityVandalismTargetRules
                    .IsPersistentAggregateTarget(candidate)
                || candidate.IsDamaged
                || candidate.IsGridMovement
                || candidate.Facility == null
                || !candidate.CanApplyDamageRules)
            {
                continue;
            }
            int distance = Mathf.Abs(origin.x - candidate.centerPos.x)
                + Mathf.Abs(origin.y - candidate.centerPos.y);
            if (distance >= bestDistance)
                continue;
            best = candidate;
            bestDistance = distance;
        }
        return best;
    }

    private bool TryStageNearFacility(
        CharacterActor actor,
        BuildableObject facility,
        out string detail)
    {
        detail = string.Empty;
        if (actor == null
            || facility == null
            || gridSystems == null
            || !gridSystems.TryGetGrid(out Grid grid)
            || grid == null)
        {
            detail = "actor/facility/grid unavailable";
            return false;
        }

        Vector2Int[] candidates =
        {
            facility.centerPos + Vector2Int.left,
            facility.centerPos + Vector2Int.right,
            facility.centerPos + Vector2Int.up,
            facility.centerPos + Vector2Int.down
        };
        Vector2Int? staging = candidates
            .Where(position => grid.IsValidGridPos(position)
                && grid.IsWalkable(position))
            .Cast<Vector2Int?>()
            .FirstOrDefault();
        if (!staging.HasValue)
        {
            detail = $"facility={facility.RequirePersistentInstanceId().Value};"
                + "no walkable adjacent cell";
            return false;
        }

        actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
        actor.transform.position = grid.GetWorldPos(staging.Value);
        bool adjacent = Mathf.Abs(actor.GetNowXY().x - facility.centerPos.x)
                + Mathf.Abs(actor.GetNowXY().y - facility.centerPos.y)
            <= 1;
        detail = $"facility={facility.RequirePersistentInstanceId().Value};"
            + $"cell={actor.GetNowXY()};adjacent={adjacent}";
        return adjacent;
    }

    private void Check(bool passed, string key, string detail)
    {
        rows.Add((passed ? "PASS" : "FAIL") + "\t" + key + "\t" + detail);
        if (!passed) failures.Add(key + ": " + detail);
    }

    private void CaptureIssue(string condition, string stack, LogType type)
    {
        if (type != LogType.Error
            && type != LogType.Exception
            && type != LogType.Assert
            && type != LogType.Warning)
            return;
        consoleIssues.Add(type + ": " + condition + "\n" + stack);
    }

    private void WriteReport()
    {
        Check(consoleIssues.Count == 0,
            "CONSOLE_CLEAN",
            consoleIssues.Count == 0
                ? "warnings=0;errors=0"
                : string.Join(" | ", consoleIssues.Take(8)));
        string status = failures.Count == 0 ? "PASS" : "FAIL";
        List<string> output = new()
        {
            "Character Deprivation Production BT PlayMode Matrix",
            "UTC=" + DateTime.UtcNow.ToString("O"),
            "STATUS=" + status,
            "SCOPE=" + (CharacterDeprivationProductionBtPlayModeVerifier
                .VandalismOnlyRequested
                ? "official-GameplayScene+live-BehaviorTree+violent-vandalism-A04"
                : CharacterDeprivationProductionBtPlayModeVerifier.CorpseEatingOnlyRequested
                    ? "official-GameplayScene+live-BehaviorTree+corpse-eating-A04;controlled-death/hunger-prerequisite"
                    : "five-deprivation-breakdown-matrix"),
            "fullAuditClosure=false",
            "humanApprovalClaimed=false",
            "trainingEligible=false",
            "AUTHORITY=V18 aggregate state producer -> live BehaviorTree root -> CharacterAiDecisionPipeline Has/RunDeprivationBreakdown -> domain runner",
            "PROHIBITED_DIRECT_CALLS=decisionPipeline.RunDeprivationBreakdown, TryRunActiveBreakdown, BeginBreakdownAction, DebugForceBreakdown, DebugClearBreakdown",
            string.Empty
        };
        output.AddRange(rows);
        if (failures.Count > 0)
        {
            output.Add(string.Empty);
            output.Add("FAILURES");
            output.AddRange(failures);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(
            CharacterDeprivationProductionBtPlayModeVerifier.ActiveReportPath)
            ?? "Artifacts/QA");
        File.WriteAllLines(
            CharacterDeprivationProductionBtPlayModeVerifier.ActiveReportPath,
            output);
    }

    private readonly struct MonoBehaviourState
    {
        public MonoBehaviourState(MonoBehaviour behaviour, bool wasEnabled)
        {
            Behaviour = behaviour;
            WasEnabled = wasEnabled;
        }

        public MonoBehaviour Behaviour { get; }
        public bool WasEnabled { get; }
    }
}
#endif
