#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BehaviorDesigner.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public static class WildlifeAiHuntPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/wildlife-ai-hunt-playmode.txt";
    private const string ReportEnvironmentVariable =
        "DUNGEONSTORY_U0_E11_WILDLIFE_REPORT_PATH";
    private const string VerifyEnvironmentVariable =
        "DUNGEONSTORY_U0_E11_WILDLIFE_VERIFY";
    private const string PendingFlagPath =
        "Temp/wildlife-ai-hunt-playmode.flag";
    private const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";
    private static bool batchExitArmed;

    internal static void PrepareIsolatedBackgroundNarrativeDeferral()
    {
        if (Environment.GetEnvironmentVariable(VerifyEnvironmentVariable) != "1")
            return;
        if (!Application.isBatchMode || !DungeonPersistencePaths.IsIsolatedVerification
            || !IsCreateOnlyReport || EditorApplication.isPlaying
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
                CaptivityWildlifeLifecyclePlayModeVerifier.LiveNarrativeCaptureVariable)))
            throw new InvalidOperationException(
                "E11 hunt background deferral requires a fresh explicit isolated batch before Play.");
        // Existing capture mode prevents native inference startup. The actual
        // Brain/AIHunt/AbilityHunt path is unchanged and receives no fake response.
        Environment.SetEnvironmentVariable(
            CaptivityWildlifeLifecyclePlayModeVerifier.LiveNarrativeCaptureVariable, "1");
    }

    [MenuItem("DungeonStory/Debug/QA/Run Wildlife AI Hunt PlayMode Verification")]
    public static void RunFromMenu() => RequestRun();

    public static void RunBatch()
    {
        Directory.CreateDirectory("Temp");
        string reportPath = ResolveReportPath();
        string rawReportPath = ResolveRawReportPath();
        string reportDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory))
            Directory.CreateDirectory(reportDirectory);
        if (IsCreateOnlyReport
            && (File.Exists(reportPath) || File.Exists(rawReportPath)))
        {
            throw new InvalidOperationException(
                "E11 wildlife report paths must be create-only: "
                + reportPath + "; " + rawReportPath);
        }
        if (File.Exists(reportPath))
        {
            File.Delete(reportPath);
        }
        File.WriteAllText(PendingFlagPath, DateTime.UtcNow.ToString("O"));
        if (!batchExitArmed)
        {
            batchExitArmed = true;
            EditorApplication.playModeStateChanged += ExitBatchAfterRun;
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

    public static void RequestRun()
    {
        if (EditorApplication.isPlaying)
        {
            StartRunner();
            return;
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingFlagPath, DateTime.UtcNow.ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    public static IEnumerator Verify(
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        Exception preparationFailure = null;
        try
        {
            if (!Application.isBatchMode
                || !DungeonPersistencePaths.IsIsolatedVerification
                || !string.Equals(
                    Environment.GetEnvironmentVariable(
                        VerifyEnvironmentVariable),
                    "1",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Focused E11 wildlife verification requires explicit isolated batch mode.");
            }
            string reportPath = ResolveReportPath();
            string rawReportPath = ResolveRawReportPath();
            if (File.Exists(reportPath) || File.Exists(rawReportPath))
            {
                throw new InvalidOperationException(
                    "Focused E11 wildlife report paths must be create-only: "
                    + reportPath + "; " + rawReportPath);
            }
        }
        catch (Exception exception)
        {
            preparationFailure = exception;
        }

        if (preparationFailure != null)
        {
            WriteFocusedFailure(preparationFailure, warnings, errors);
            yield break;
        }

        WildlifeAiHuntPlayModeRunner runner =
            new GameObject("E11 Wildlife AI Hunt PlayMode Runner")
                .AddComponent<WildlifeAiHuntPlayModeRunner>();
        runner.ManagedExternally = true;
        yield return runner.ExecuteGuarded();
        runner.AddConsoleResult(warnings, errors);
        runner.WriteReport();
        bool succeeded = runner.Passed;
        UnityEngine.Object.Destroy(runner.gameObject);
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () =>
            EditorApplication.Exit(succeeded ? 0 : 1);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapPendingRun()
    {
        if (!File.Exists(PendingFlagPath))
        {
            return;
        }

        File.Delete(PendingFlagPath);
        StartRunner();
    }

    private static void StartRunner()
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                WildlifeAiHuntPlayModeRunner>() != null)
        {
            return;
        }

        new GameObject("Wildlife AI Hunt PlayMode Runner")
            .AddComponent<WildlifeAiHuntPlayModeRunner>();
    }

    private static void ExitBatchAfterRun(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode || !batchExitArmed)
            return;
        EditorApplication.playModeStateChanged -= ExitBatchAfterRun;
        batchExitArmed = false;
        EditorApplication.delayCall += () =>
        {
            string reportPath = ResolveReportPath();
            bool passed = File.Exists(reportPath)
                && File.ReadLines(reportPath).Any(line =>
                    string.Equals(
                        line,
                        "RESULT=PASS; failures=0",
                        StringComparison.Ordinal));
            EditorApplication.Exit(passed ? 0 : 1);
        };
    }

    internal static string ResolveReportPath()
    {
        string overridePath = Environment.GetEnvironmentVariable(
            ReportEnvironmentVariable);
        return string.IsNullOrWhiteSpace(overridePath)
            ? ReportPath
            : Path.GetFullPath(overridePath.Trim());
    }

    internal static string ResolveRawReportPath() =>
        Path.ChangeExtension(ResolveReportPath(), ".raw.json");

    internal static bool IsCreateOnlyReport =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
            ReportEnvironmentVariable));

    private static void WriteFocusedFailure(
        Exception exception,
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        string reportPath = ResolveReportPath();
        string rawReportPath = ResolveRawReportPath();
        if (File.Exists(reportPath) || File.Exists(rawReportPath))
        {
            Debug.LogError(
                "Focused E11 wildlife evidence already exists; preserving it without writing a failure report. "
                + reportPath + "; " + rawReportPath + "\n" + exception);
            DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
            EditorApplication.ExitPlaymode();
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
            return;
        }
        string reportDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory))
            Directory.CreateDirectory(reportDirectory);
        string[] lines =
        {
            "WILDLIFE_AI_HUNT_PLAYMODE",
            "unityMcpUsed=false",
            "dungeonPlayerMcpUsed=false",
            "fullAuditClosure=false",
            "trainingEligible=false",
            "humanApprovalClaimed=false",
            $"warningCount={warnings?.Count ?? 0}",
            $"errorCount={errors?.Count ?? 0}",
            "RESULT=FAIL; failures=1",
            exception.ToString()
        };
        if (IsCreateOnlyReport)
        {
            using FileStream stream = new(
                reportPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            using StreamWriter writer = new(
                stream,
                new System.Text.UTF8Encoding(false));
            foreach (string line in lines)
                writer.WriteLine(line);
        }
        else
        {
            File.WriteAllLines(reportPath, lines);
        }
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(1);
    }
}

public sealed class WildlifeAiHuntPlayModeRunner : MonoBehaviour
{
    private const float VerificationTimeScale = 8f;
    private const float SetupTimeoutRealtime = 12f;
    private const float HuntTimeoutRealtime = 35f;
    private const string RawFixtureScope =
        "Test-only E11 fixture: authored repeating crossbow and bolt reserve, "
        + "worker vitals raised, other worker AI paused, and authored hunt action isolated; "
        + "non-target narrative requests retained without inference or supplied responses; "
        + "all observations are excluded from training and natural-gameplay claims.";
    private static readonly string[] RequiredRawStages =
    {
        "prepared",
        "designation-committed",
        "action-started",
        "terminal-before-cleanup"
    };

    private readonly List<string> checks = new();
    private readonly List<string> failures = new();
    private readonly List<MonoBehaviourState> pausedAi = new();
    private readonly Dictionary<CharacterCondition, float> originalStats = new();
    private readonly JArray rawObservations = new();
    private bool rawObservationsValidated;

    private CharacterActor hunter;
    private AbilityWork work;
    private AIBrain brain;
    private AbilityHunt huntAbility;
    private WildlifeRuntime wildlife;
    private IWorldItemStackRuntime items;
    private ICharacterCarryInventoryRegistry carryInventories;
    private CharacterCarryInventory carryInventory;
    private ICombatEquipmentRuntime equipment;
    private GameplayOutcomeLedger outcomeLedger;
    private LocalLlmRequestQueue deferredNarrativeQueue;
    private AIAction[] originalActions;
    private WorkPriorityLevel originalHuntPriority;
    private CharacterCombatLoadoutProfile originalEquipmentProfile;
    private CharacterCarryInventorySaveData originalCarryInventory;
    private string hunterId = string.Empty;
    private string testWeaponInstanceId = string.Empty;
    private int testWeaponAmmunitionBefore;
    private int testAmmunitionTotalBefore;
    private float originalTimeScale;

    internal bool ManagedExternally { get; set; }
    internal bool Passed => failures.Count == 0;

    private IEnumerator Start()
    {
        if (ManagedExternally)
            yield break;

        yield return ExecuteCore();
        WriteReport();
        Destroy(gameObject);
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        };
    }

    internal IEnumerator ExecuteCore()
    {
        Directory.CreateDirectory("Artifacts/QA");
        originalTimeScale = Time.timeScale;
        Time.timeScale = VerificationTimeScale;

        try
        {
            yield return ResolveWorld();
            if (failures.Count == 0)
            {
                yield return RunHunt();
            }
        }
        finally
        {
            if (Application.isBatchMode && WildlifeAiHuntPlayModeVerifier.IsCreateOnlyReport)
                Check(deferredNarrativeQueue != null
                        && deferredNarrativeQueue.RunningCount == 0
                        && !deferredNarrativeQueue.IsBundledModelStarting
                        && !deferredNarrativeQueue.IsBundledModelRunning
                        && !deferredNarrativeQueue.HasConfiguredEndpoint,
                    "BACKGROUND_LLM_DEFERRED_THROUGH_RUN",
                    $"queued={deferredNarrativeQueue?.QueuedCount};running={deferredNarrativeQueue?.RunningCount};"
                        + "requests-retained-until-normal-queue-teardown;no-response-provider;trainingEligible=false");
            Cleanup();
            Time.timeScale = originalTimeScale;
        }
    }

    internal IEnumerator ExecuteGuarded()
    {
        Stack<IEnumerator> pending = new();
        pending.Push(ExecuteCore());
        while (pending.Count > 0)
        {
            IEnumerator current = pending.Peek();
            bool moved = false;
            object yielded = null;
            Exception failure = null;
            try
            {
                moved = current.MoveNext();
                if (moved)
                    yielded = current.Current;
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (failure != null)
            {
                Check(false,
                    "UNHANDLED_RUNTIME_EXCEPTION",
                    failure.ToString());
                yield break;
            }
            if (!moved)
            {
                pending.Pop();
                continue;
            }
            if (yielded is IEnumerator nested)
            {
                pending.Push(nested);
                continue;
            }
            yield return yielded;
        }
    }

    private IEnumerator ResolveWorld()
    {
        float deadline = Time.realtimeSinceStartup + SetupTimeoutRealtime;
        DungeonRuntimeLifetimeScope scope = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = UnityEngine.Object.FindFirstObjectByType<
                DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include);
            if (scope?.Container != null && deferredNarrativeQueue == null
                && Application.isBatchMode && WildlifeAiHuntPlayModeVerifier.IsCreateOnlyReport)
            {
                deferredNarrativeQueue = scope.Container.Resolve<ILocalLlmRuntimeProvider>()
                    .GetRequiredRuntime() as LocalLlmRequestQueue;
                bool prepared = DungeonPersistencePaths.IsIsolatedVerification
                    && Environment.GetEnvironmentVariable(
                        CaptivityWildlifeLifecyclePlayModeVerifier.LiveNarrativeCaptureVariable) == "1"
                    && deferredNarrativeQueue != null && deferredNarrativeQueue.RunningCount == 0
                    && !deferredNarrativeQueue.IsBundledModelStarting
                    && !deferredNarrativeQueue.IsBundledModelRunning
                    && !deferredNarrativeQueue.HasConfiguredEndpoint
                    && deferredNarrativeQueue.CaptureReadiness().State == LocalLlmRuntimeReadinessState.Starting;
                Check(prepared, "BACKGROUND_LLM_DEFERRAL_PREPARATION",
                    "isolated-non-target-preparation;no-native-host-or-endpoint;trainingEligible=false");
                if (!prepared) yield break;
                int queuedBefore = deferredNarrativeQueue.QueuedCount;
                deferredNarrativeQueue.ConfigureIsolatedRequestCapture(request =>
                    Check(false, "BACKGROUND_LLM_UNEXPECTED_DISPATCH",
                        "Background requests must remain queued in this fixture."));
                deferredNarrativeQueue.ConfigureIsolatedAutomaticDispatch(false);
                Check(deferredNarrativeQueue.QueuedCount == queuedBefore
                        && deferredNarrativeQueue.CaptureReadiness().State == LocalLlmRuntimeReadinessState.Ready,
                    "BACKGROUND_LLM_REQUESTS_RETAINED",
                    $"queued={queuedBefore}->{deferredNarrativeQueue.QueuedCount};clear=false;abort=false;response-provider=false;trainingEligible=false");
                if (failures.Count != 0) yield break;
            }
            CharacterActor[] actors = GetLiveWorkers();
            if (scope != null && actors.Length > 0)
            {
                break;
            }

            if (scope != null && actors.Length == 0)
            {
                checks.Add("SETUP\tINFO\t"
                    + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            }
            yield return null;
        }

        hunter = GetLiveWorkers().FirstOrDefault(candidate =>
            candidate.Brain?.availableActions?.Any(action =>
                action?.actionset is AIHunt) == true);
        Check(scope != null, "LIVE_SCOPE", scope != null ? scope.name : "missing");
        Check(hunter != null, "LIVE_HUNTER", hunter?.name ?? "missing");
        if (scope == null || hunter == null)
        {
            yield break;
        }

        wildlife = scope.Container.Resolve<WildlifeRuntime>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        carryInventories = scope.Container.Resolve<ICharacterCarryInventoryRegistry>();
        equipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        outcomeLedger = scope.Container.Resolve<GameplayOutcomeLedger>();
        (scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable)
            ?.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        brain = hunter.Brain;
        hunter.TryGetAbility(out work);
        huntAbility = AbilityHunt.Ensure(hunter, wildlife);
        originalActions = brain.availableActions;
        originalHuntPriority = work.WorkPriorities.GetPriority(
            BuiltInWorkTypeIds.Hunt);
        foreach (KeyValuePair<CharacterCondition, float> entry in
                 hunter.Stats.StatSnapshot)
        {
            originalStats[entry.Key] = entry.Value;
        }

        PrepareRangedHuntWeapon();

        PauseOtherAi();
        Check(wildlife != null, "WILDLIFE_RUNTIME", "resolved");
        Check(items != null, "ITEM_RUNTIME", "resolved");
        Check(equipment != null, "COMBAT_EQUIPMENT_RUNTIME", "resolved");
        Check(work != null, "HUNT_WORK_ROLE", work != null ? "resolved" : "missing");
        Check(huntAbility != null, "HUNT_ABILITY", huntAbility != null ? "resolved" : "missing");
    }

    private IEnumerator RunHunt()
    {
        wildlife.Tick();
        WildlifeActor target = wildlife.Wildlife
            .Where(candidate => candidate != null && candidate.IsAlive)
            .OrderBy(candidate => candidate.IsDangerous)
            .ThenBy(candidate => candidate.MaxHealth)
            .ThenBy(candidate => candidate.WildlifeId, StringComparer.Ordinal)
            .FirstOrDefault();
        Check(target != null, "LIVE_HUNT_TARGET", target?.DisplayName ?? "missing");
        if (target == null)
        {
            yield break;
        }
        string targetId = target.WildlifeId;

        AIAction huntAction = originalActions.FirstOrDefault(action =>
            action?.actionset is AIHunt);
        Check(huntAction != null, "AUTHORED_HUNT_ACTION", huntAction?.actionset?.name ?? "missing");
        if (huntAction == null)
        {
            yield break;
        }

        foreach (CharacterCondition condition in originalStats.Keys.ToArray())
        {
            hunter.Stats.Stats[condition] = 100f;
        }
        work.SetWorkPriority(BuiltInWorkTypeIds.Hunt, WorkPriorityLevel.Priority1);
        int designationBefore = CountWildlifeOutcome(
            EnvironmentOutcomeIds.WildlifeCommandProducer,
            "hunt-designation",
            targetId);
        int attackBefore = CountWildlifeOutcome(
            EnvironmentOutcomeIds.WildlifeActionProducer,
            "hunt-attack",
            targetId);
        ObserveRawStage("prepared", target, targetId);
        wildlife.DesignateHunt(targetId, true, priority: true);
        int designationAfter = CountWildlifeOutcome(
            EnvironmentOutcomeIds.WildlifeCommandProducer,
            "hunt-designation",
            targetId);
        Check(designationAfter == designationBefore + 1
                && wildlife.Capture().gameplayOutcomes.Count == 0,
            "HUNT_DESIGNATION_PARENT_EXACTLY_ONCE",
            $"ledger={designationBefore}->{designationAfter}; pending={wildlife.Capture().gameplayOutcomes.Count}");
        ObserveRawStage("designation-committed", target, targetId);
        brain.StopCurrentActionForReplan("wildlife-ai-hunt-verifier");
        brain.availableActions = new[] { huntAction };
        brain.PreferActionOnNextDecision<AIHunt>(180f);

        int healthBefore = target.CurrentHealth;
        int carcassesBefore = CountCarcasses();
        CharacterAiRuntimeGateSnapshot gateBefore =
            brain.CaptureRuntimeGateSnapshot();
        brain.RequestImmediateReplan(clearFailures: true);

        float startDeadline = Time.realtimeSinceStartup + SetupTimeoutRealtime;
        while (Time.realtimeSinceStartup < startDeadline
            && (brain.bestAction?.actionset is not AIHunt
                || !brain.bestAction.HasStarted
                || !huntAbility.IsHunting))
        {
            yield return null;
        }

        Check(brain.bestAction?.actionset is AIHunt && huntAbility.IsHunting,
            "HUNT_ACTION_STARTED",
            $"action={brain.bestAction?.actionset?.GetType().Name}; phase={brain.CurrentActionPhase}");
        ObserveRawStage("action-started", target, targetId);

        float deadline = Time.realtimeSinceStartup + HuntTimeoutRealtime;
        int minimumHealth = target.CurrentHealth;
        while (Time.realtimeSinceStartup < deadline
            && target != null
            && target.IsAlive
            && huntAbility.IsHunting)
        {
            minimumHealth = Mathf.Min(minimumHealth, target.CurrentHealth);
            yield return null;
        }

        // The lethal hit can transition IsAlive to false in the same frame that
        // ends the observation loop. Capture that terminal health before judging
        // whether production combat actually applied damage.
        if (target != null)
        {
            minimumHealth = Mathf.Min(minimumHealth, target.CurrentHealth);
        }

        bool killed = target == null || !target.IsAlive;
        int carcassesAfter = CountCarcasses();
        yield return null;
        yield return null;
        CharacterAiRuntimeGateSnapshot gate = brain.CaptureRuntimeGateSnapshot();
        GameplayOutcomeSnapshot[] attackOutcomes = FindWildlifeOutcomes(
            EnvironmentOutcomeIds.WildlifeActionProducer,
            "hunt-attack",
            targetId)
            .Skip(attackBefore)
            .ToArray();
        Check(minimumHealth < healthBefore, "HUNT_APPLIED_DAMAGE",
            $"health={healthBefore}->{minimumHealth}");
        Check(killed, "HUNT_TARGET_KILLED",
            $"alive={target?.IsAlive}; health={target?.CurrentHealth}; phase={brain.CurrentActionPhase}; failure={brain.LastActionFailure}");
        Check(carcassesAfter == carcassesBefore + 1,
            "HUNT_CARCASS_EXACTLY_ONCE",
            $"carcasses={carcassesBefore}->{carcassesAfter}");
        Check(attackOutcomes.Length > 0
                && wildlife.Capture().gameplayOutcomes.Count == 0,
            "HUNT_ATTACK_PARENTS_COMMITTED",
            $"attackResults={attackOutcomes.Length}; pending={wildlife.Capture().gameplayOutcomes.Count}");
        GameplayOutcomeSnapshot lethal = attackOutcomes.LastOrDefault();
        int recordedAmmunitionConsumed = attackOutcomes.Sum(outcome =>
            (int)Metric(
                outcome,
                EnvironmentOutcomeIds.AmmunitionConsumedMetric.Value));
        bool activeWeaponResolved = equipment.TryGetActiveWeapon(
            hunterId,
            out CombatWeaponSnapshot weaponAfter);
        int ammunitionTotalAfter = activeWeaponResolved
            && weaponAfter != null
            && carryInventory != null
                ? weaponAfter.LoadedAmmo
                    + carryInventory.CountItem("ammo:bolt")
                : -1;
        Check(attackOutcomes.Length > 0
                && attackOutcomes.All(outcome =>
                    Fact(outcome, EnvironmentOutcomeIds.WeaponDefinitionFact.Value)
                        == "weapon:repeating-crossbow"
                    && Fact(outcome, EnvironmentOutcomeIds.WeaponInstanceFact.Value)
                        == testWeaponInstanceId
                    && Fact(outcome, EnvironmentOutcomeIds.AmmunitionItemFact.Value)
                        == "ammo:bolt"
                    && Metric(
                        outcome,
                        EnvironmentOutcomeIds.AmmunitionConsumedMetric.Value) == 1d
                    && Metric(
                        outcome,
                        EnvironmentOutcomeIds.AmmunitionBeforeMetric.Value)
                        - Metric(
                            outcome,
                            EnvironmentOutcomeIds.AmmunitionAfterMetric.Value) == 1d),
            "HUNT_ACTUAL_RANGED_AMMUNITION_RECORDED",
            $"attacks={attackOutcomes.Length}; consumed={recordedAmmunitionConsumed}; weapon={testWeaponInstanceId}");
        Check(activeWeaponResolved
                && weaponAfter != null
                && weaponAfter.InstanceId == testWeaponInstanceId
                && ammunitionTotalAfter
                    == testAmmunitionTotalBefore - recordedAmmunitionConsumed,
            "HUNT_ACTUAL_RANGED_AMMUNITION_DEBIT",
            $"total={testAmmunitionTotalBefore}->{ammunitionTotalAfter}; loaded={testWeaponAmmunitionBefore}->{weaponAfter?.LoadedAmmo}; recorded={recordedAmmunitionConsumed}");
        Check(lethal != null
                && Metric(lethal, EnvironmentOutcomeIds.KilledMetric.Value) == 1d
                && Metric(lethal, EnvironmentOutcomeIds.OutputQuantityMetric.Value) == 1d
                && Metric(
                    lethal,
                    EnvironmentOutcomeIds.AmmunitionConsumedMetric.Value) == 1d
                && Fact(lethal, EnvironmentOutcomeIds.CarcassStackFact.Value) != "none",
            "HUNT_LETHAL_PARENT_HAS_PHYSICAL_CARCASS",
            lethal == null
                ? "missing lethal parent"
                : $"killed={Metric(lethal, EnvironmentOutcomeIds.KilledMetric.Value)}; output={Metric(lethal, EnvironmentOutcomeIds.OutputQuantityMetric.Value)}; ammo={Metric(lethal, EnvironmentOutcomeIds.AmmunitionConsumedMetric.Value)}; carcass={Fact(lethal, EnvironmentOutcomeIds.CarcassStackFact.Value)}");
        Check(!huntAbility.IsHunting, "HUNT_COROUTINE_TERMINATED",
            $"isHunting={huntAbility.IsHunting}");
        Check(gate.ActionStarts == gateBefore.ActionStarts + 1,
            "HUNT_ACTION_STARTED_ONCE",
            $"starts={gateBefore.ActionStarts}->{gate.ActionStarts}");
        Check(gate.ActionTerminals == gateBefore.ActionTerminals + 1,
            "HUNT_TERMINAL_ONCE",
            $"terminal={gateBefore.ActionTerminals}->{gate.ActionTerminals}");
        Check(gate.ActionCompleted == gateBefore.ActionCompleted + 1,
            "HUNT_COMPLETED_TERMINAL",
            $"completed={gateBefore.ActionCompleted}->{gate.ActionCompleted}");
        Check(gate.ActionFailed == gateBefore.ActionFailed,
            "HUNT_NO_FAILED_TERMINAL",
            $"failed={gateBefore.ActionFailed}->{gate.ActionFailed}");
        Check(gate.LivePathRequests == 0 && gate.LiveReservations == 0,
            "HUNT_NO_RUNTIME_OWNERSHIP_LEAK",
            $"paths={gate.LivePathRequests}; reservations={gate.LiveReservations}");
        Check(gate.InvariantAnomalies == gateBefore.InvariantAnomalies,
            "HUNT_NO_INVARIANT_ANOMALY",
            $"invariants={gateBefore.InvariantAnomalies}->{gate.InvariantAnomalies}");
        ObserveRawStage(
            "terminal-before-cleanup",
            target,
            targetId);
    }

    private int CountCarcasses() => items?.GetAllStacks().Count(stack =>
        stack != null
        && WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
            stack.ItemId,
            out _)) ?? 0;

    private IEnumerable<GameplayOutcomeSnapshot> FindWildlifeOutcomes(
        string producerId,
        string eventKind,
        string wildlifeId)
    {
        string prefix = string.Equals(
                producerId,
                EnvironmentOutcomeIds.WildlifeCommandProducer,
                StringComparison.Ordinal)
            ? $"wildlife-command:{eventKind}:{wildlifeId}:"
            : $"wildlife-action:{eventKind}:{wildlifeId}:";
        return outcomeLedger.CaptureGameplayOutcomes().exactOutcomes
            .Where(value => value != null
                && string.Equals(value.producerId, producerId, StringComparison.Ordinal)
                && value.operationId.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(value => value.ownerRevision);
    }

    private int CountWildlifeOutcome(
        string producerId,
        string eventKind,
        string wildlifeId) => FindWildlifeOutcomes(
            producerId,
            eventKind,
            wildlifeId).Count();

    private static double Metric(GameplayOutcomeSnapshot outcome, string metricId) =>
        outcome?.metrics?.FirstOrDefault(value => value != null
            && string.Equals(value.metricId, metricId, StringComparison.Ordinal))?.value ?? double.NaN;

    private static string Fact(GameplayOutcomeSnapshot outcome, string factKindId) =>
        outcome?.facts?.FirstOrDefault(value => value != null
            && string.Equals(value.factId, factKindId, StringComparison.Ordinal))?.value ?? string.Empty;

    private void ObserveRawStage(
        string stage,
        WildlifeActor target,
        string targetId)
    {
        if (!WildlifeAiHuntPlayModeVerifier.IsCreateOnlyReport)
            return;

        try
        {
            rawObservations.Add(CaptureRawStage(
                stage,
                target,
                targetId));
        }
        catch (Exception exception)
        {
            Check(
                false,
                "RAW_OBSERVATION_"
                    + stage.Replace('-', '_').ToUpperInvariant(),
                exception.ToString());
        }
    }

    private JObject CaptureRawStage(
        string stage,
        WildlifeActor target,
        string targetId)
    {
        if (string.IsNullOrWhiteSpace(stage)
            || string.IsNullOrWhiteSpace(targetId))
        {
            throw new InvalidOperationException(
                "E11 raw observation requires initialized stage and target identities.");
        }
        if (rawObservations.Any(value => string.Equals(
                (string)value?["stage"],
                stage,
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "E11 raw observation stage was captured more than once: " + stage);
        }
        if (hunter == null
            || wildlife == null
            || brain == null
            || huntAbility == null
            || outcomeLedger == null
            || carryInventory == null
            || equipment == null)
        {
            throw new InvalidOperationException(
                "E11 raw observation authority is not fully initialized at " + stage + ".");
        }
        if (items is not WorldItemStackRuntime itemRuntime)
        {
            throw new InvalidOperationException(
                "E11 raw observation requires the real WorldItemStackRuntime at " + stage + ".");
        }
        if (object.ReferenceEquals(target, null))
        {
            throw new InvalidOperationException(
                "E11 raw managed target reference is unavailable at " + stage + ".");
        }
        // The lethal path removes the native Unity object before the settled
        // terminal frame. These getters read only the retained actor's managed
        // fields; never access a removed native component or invent zero health.
        int observedTargetHealth = target.CurrentHealth;
        bool observedTargetAlive = target.IsAlive;
        bool targetNativeObjectPresent = target != null;
        if (!targetNativeObjectPresent
            && (observedTargetAlive || observedTargetHealth != 0
                || target.State != WildlifeState.Dead))
        {
            throw new InvalidOperationException(
                "E11 raw removed target lacks an actual lethal managed state at " + stage + ".");
        }
        if (!string.Equals(
                target.WildlifeId,
                targetId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "E11 raw target identity changed at " + stage + ".");
        }

        string actualHunterId = CharacterPersistentIdentity.Require(hunter).Value;
        if (string.IsNullOrWhiteSpace(actualHunterId)
            || !string.Equals(actualHunterId, hunterId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "E11 raw hunter identity is unavailable or changed at " + stage + ".");
        }
        if (!equipment.TryGetActiveWeapon(
                actualHunterId,
                out CombatWeaponSnapshot activeWeapon)
            || activeWeapon == null
            || string.IsNullOrWhiteSpace(activeWeapon.AmmunitionItemId))
        {
            throw new InvalidOperationException(
                "E11 raw active ranged weapon is unavailable at " + stage + ".");
        }

        DungeonWildlifeSaveData wildlifeSnapshot = wildlife.Capture();
        DungeonPhysicalItemSaveData physicalItemsSnapshot = itemRuntime.Capture();
        GameplayOutcomeLedgerSaveData ledgerSnapshot =
            outcomeLedger.CaptureGameplayOutcomes();
        CharacterCarryInventorySaveData carrySnapshot = carryInventory.Capture();
        if (wildlifeSnapshot == null
            || physicalItemsSnapshot == null
            || ledgerSnapshot == null
            || carrySnapshot?.items == null)
        {
            throw new InvalidOperationException(
                "E11 raw canonical runtime capture is uninitialized at " + stage + ".");
        }

        int reserveAmmunition = carrySnapshot.items
            .Where(value => value != null
                && string.Equals(
                    value.itemId,
                    activeWeapon.AmmunitionItemId,
                    StringComparison.Ordinal))
            .Sum(value => value.quantity);
        CharacterAiRuntimeGateSnapshot gate =
            brain.CaptureRuntimeGateSnapshot();
        HuntRawObservationSnapshot summary = new()
        {
            stage = stage,
            fixtureScope = RawFixtureScope,
            testOnlyFixture = true,
            hunterStableId = actualHunterId,
            targetStableId = targetId,
            targetHealthObserved = observedTargetHealth,
            targetAliveObserved = observedTargetAlive,
            targetStateObserved = target.State.ToString(),
            targetNativeObjectPresent = targetNativeObjectPresent,
            targetHealthObservationSource = "retained-same-target-managed-fields",
            carcassCount = itemRuntime.GetAllStacks().Count(stack =>
                stack != null
                && WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                    stack.ItemId,
                    out _)),
            reserveAmmunition = reserveAmmunition,
            loadedAmmunition = activeWeapon.LoadedAmmo,
            ammunitionTotal = activeWeapon.LoadedAmmo + reserveAmmunition,
            huntAbilityRunning = huntAbility.IsHunting,
            activeActionType = brain.bestAction?.actionset?.GetType().FullName
                ?? string.Empty,
            activeActionStarted = brain.bestAction?.HasStarted ?? false,
            currentActionPhase = brain.CurrentActionPhase.ToString(),
            lastActionFailure = brain.LastActionFailure.ToString(),
            activeWeapon = ActiveWeaponRawSnapshot.Capture(activeWeapon),
            gate = CharacterAiGateRawSnapshot.Capture(gate)
        };
        JObject observation = CanonicalObject(summary, "E11 observation summary");
        observation["wildlife"] = CanonicalObject(
            wildlifeSnapshot,
            "WildlifeRuntime.Capture");
        observation["physicalItems"] = CanonicalObject(
            physicalItemsSnapshot,
            "WorldItemStackRuntime.Capture");
        observation["gameplayLedger"] = CanonicalObject(
            ledgerSnapshot,
            "GameplayOutcomeLedger.CaptureGameplayOutcomes");
        observation["carryInventory"] = CanonicalObject(
            carrySnapshot,
            "CharacterCarryInventory.Capture");
        return observation;
    }

    private static JObject CanonicalObject(object value, string label)
    {
        if (value == null)
            throw new InvalidOperationException(label + " returned null.");
        string canonicalJson = JsonUtility.ToJson(value, false);
        if (string.IsNullOrWhiteSpace(canonicalJson))
            throw new InvalidOperationException(label + " returned empty canonical JSON.");
        JObject parsed = JObject.Parse(canonicalJson);
        if (!parsed.HasValues)
            throw new InvalidOperationException(label + " returned an empty canonical object.");
        return parsed;
    }

    private void PrepareRangedHuntWeapon()
    {
        Check(equipment != null,
            "RANGED_HUNT_EQUIPMENT_AVAILABLE",
            equipment != null ? "resolved" : "missing");
        if (equipment == null || hunter == null)
            return;

        hunterId = CharacterPersistentIdentity.Require(hunter).Value;
        carryInventory = carryInventories?.Find(
            CharacterPersistentIdentity.Require(hunter));
        originalCarryInventory = carryInventory?.Capture();
        Check(carryInventory != null && originalCarryInventory != null,
            "RANGED_HUNT_CARRY_INVENTORY_CAPTURED",
            carryInventory != null ? "resolved" : "missing");
        originalEquipmentProfile = equipment
            .GetActiveProfileSnapshot(hunterId)
            ?.Clone();
        Check(originalEquipmentProfile != null,
            "RANGED_HUNT_LOADOUT_CAPTURED",
            originalEquipmentProfile?.profileId ?? "missing");
        if (originalEquipmentProfile == null
            || carryInventory == null
            || originalCarryInventory == null)
            return;

        if (originalEquipmentProfile.weaponInstanceIds.Count > 0)
        {
            Check(equipment.TryUnassignSlot(
                    hunterId,
                    CombatEquipmentLoadoutSlot.Weapon,
                    out string weaponFailure),
                "RANGED_HUNT_ORIGINAL_WEAPONS_STORED",
                weaponFailure);
        }
        if (!string.IsNullOrWhiteSpace(originalEquipmentProfile.shieldInstanceId))
        {
            Check(equipment.TryUnassignSlot(
                    hunterId,
                    CombatEquipmentLoadoutSlot.Armor,
                    out string armorFailure),
                "RANGED_HUNT_ORIGINAL_ARMOR_STORED",
                armorFailure);
        }
        if (failures.Count > 0)
            return;

        CombatEquipmentInstance testWeapon = equipment.CreateExternalInstance(
            "weapon:repeating-crossbow",
            CombatEquipmentQuality.Normal);
        testWeaponInstanceId = testWeapon?.instanceId ?? string.Empty;
        CharacterCarryInventorySaveData fixtureCarryInventory =
            carryInventory.Capture();
        fixtureCarryInventory.items.Add(new CharacterCarriedItemSaveData
        {
            carriedStackId = "qa:e11:ammo-reserve:" + testWeaponInstanceId,
            itemId = "ammo:bolt",
            quantity = 50
        });
        carryInventory.Restore(fixtureCarryInventory);
        Check(carryInventory.CountItem("ammo:bolt") >= 50,
            "RANGED_HUNT_AMMUNITION_RESERVE_PREPARED",
            $"reserve={carryInventory.CountItem("ammo:bolt")}");
        string assignFailure = string.Empty;
        string activeFailure = string.Empty;
        string fireModeFailure = string.Empty;
        CombatWeaponSnapshot activeWeapon = null;
        bool loaded = testWeapon != null
            && equipment.TryLoadExternalAmmunition(
                testWeaponInstanceId,
                "ammo:bolt",
                5);
        bool assigned = loaded
            && equipment.TryAssignToCharacter(
                hunterId,
                testWeaponInstanceId,
                out assignFailure);
        bool activated = assigned
            && equipment.TrySetActiveWeapon(
                hunterId,
                testWeaponInstanceId,
                out activeFailure);
        bool fireModeSet = activated
            && equipment.TrySetFireMode(
                hunterId,
                CombatFireMode.Aimed,
                out fireModeFailure);
        bool holdFireCleared = fireModeSet
            && equipment.TrySetHoldFire(hunterId, false);
        bool prepared = holdFireCleared
            && equipment.TryGetActiveWeapon(
                hunterId,
                out activeWeapon)
            && activeWeapon != null
            && activeWeapon.IsRanged
            && activeWeapon.InstanceId == testWeaponInstanceId
            && activeWeapon.AmmunitionItemId == "ammo:bolt"
            && activeWeapon.LoadedAmmo == 5;
        testWeaponAmmunitionBefore = prepared ? activeWeapon.LoadedAmmo : 0;
        testAmmunitionTotalBefore = prepared
            ? activeWeapon.LoadedAmmo
                + carryInventory.CountItem("ammo:bolt")
            : 0;
        Check(prepared,
            "RANGED_HUNT_WEAPON_PREPARED",
            prepared
                ? $"weapon={activeWeapon.DefinitionId}; instance={activeWeapon.InstanceId}; ammo={activeWeapon.AmmunitionItemId}:{activeWeapon.LoadedAmmo}"
                : $"loaded={loaded}; assigned={assigned}:{assignFailure}; active={activated}:{activeFailure}; fireMode={fireModeSet}:{fireModeFailure}; holdFire={holdFireCleared}");
    }

    private void PauseOtherAi()
    {
        foreach (CharacterActor actor in GetLiveWorkers())
        {
            if (actor == hunter)
            {
                continue;
            }

            if (actor.Brain != null)
            {
                pausedAi.Add(new MonoBehaviourState(actor.Brain, actor.Brain.enabled));
                actor.Brain.enabled = false;
            }
            if (actor.BehaviorTree != null)
            {
                pausedAi.Add(new MonoBehaviourState(
                    actor.BehaviorTree,
                    actor.BehaviorTree.enabled));
                actor.BehaviorTree.enabled = false;
            }
        }
    }

    private void Cleanup()
    {
        huntAbility?.StopHunting("wildlife-ai-hunt-verifier-cleanup");
        if (brain != null)
        {
            brain.StopCurrentActionForReplan("wildlife-ai-hunt-verifier-cleanup");
            brain.availableActions = originalActions;
            brain.RequestImmediateReplan(clearFailures: true);
        }
        if (work != null)
        {
            work.SetWorkPriority(BuiltInWorkTypeIds.Hunt, originalHuntPriority);
        }
        if (hunter != null)
        {
            hunter.Stats.Stats = originalStats;
        }
        RestoreOriginalEquipment();
        foreach (MonoBehaviourState state in pausedAi)
        {
            if (state.Component != null)
            {
                state.Component.enabled = state.WasEnabled;
            }
        }
    }

    private void RestoreOriginalEquipment()
    {
        if (equipment == null
            || originalEquipmentProfile == null
            || string.IsNullOrWhiteSpace(hunterId))
        {
            return;
        }

        CharacterCombatLoadoutProfile current =
            equipment.GetActiveProfileSnapshot(hunterId);
        if (current?.weaponInstanceIds?.Count > 0)
        {
            equipment.TryUnassignSlot(
                hunterId,
                CombatEquipmentLoadoutSlot.Weapon,
                out _);
        }

        foreach (string instanceId in originalEquipmentProfile.weaponInstanceIds)
        {
            equipment.TryAssignToCharacter(hunterId, instanceId, out _);
        }
        if (!string.IsNullOrWhiteSpace(
                originalEquipmentProfile.activeWeaponInstanceId))
        {
            equipment.TrySetActiveWeapon(
                hunterId,
                originalEquipmentProfile.activeWeaponInstanceId,
                out _);
        }

        foreach (string instanceId in originalEquipmentProfile.armorInstanceIds)
        {
            equipment.TryAssignToCharacter(hunterId, instanceId, out _);
        }
        if (!string.IsNullOrWhiteSpace(originalEquipmentProfile.shieldInstanceId))
        {
            equipment.TryAssignToCharacter(
                hunterId,
                originalEquipmentProfile.shieldInstanceId,
                out _);
        }
        if (!string.IsNullOrWhiteSpace(
                originalEquipmentProfile.activeWeaponInstanceId))
        {
            equipment.TrySetFireMode(
                hunterId,
                originalEquipmentProfile.fireMode,
                out _);
        }
        equipment.TrySetHoldFire(
            hunterId,
            originalEquipmentProfile.holdFire);
        carryInventory?.Restore(originalCarryInventory);
    }

    private void Check(bool passed, string id, string detail)
    {
        checks.Add($"{id}\t{(passed ? "PASS" : "FAIL")}\t{detail}");
        if (!passed)
        {
            failures.Add(id + ": " + detail);
        }
    }

    internal void AddConsoleResult(
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        int warningCount = warnings?.Count ?? 0;
        int errorCount = errors?.Count ?? 0;
        Check(warningCount == 0 && errorCount == 0,
            "RUNTIME_CONSOLE_CLEAN",
            $"warnings={warningCount}; errors={errorCount}");
    }

    internal void WriteReport()
    {
        ValidateRawObservations();
        List<string> lines = new()
        {
            "WILDLIFE_AI_HUNT_PLAYMODE",
            $"checks={checks.Count}; failures={failures.Count}",
            "case\tresult\tdetail"
        };
        lines.AddRange(checks);
        lines.Add($"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}; failures={failures.Count}");
        if (failures.Count > 0)
        {
            lines.Add("FAILURES");
            lines.AddRange(failures);
        }
        if (WildlifeAiHuntPlayModeVerifier.IsCreateOnlyReport)
        {
            lines.Insert(1, "unityMcpUsed=false");
            lines.Insert(2, "dungeonPlayerMcpUsed=false");
            lines.Insert(3, "fullAuditClosure=false");
            lines.Insert(4, "trainingEligible=false");
            lines.Insert(5, "humanApprovalClaimed=false");
        }
        string reportPath = WildlifeAiHuntPlayModeVerifier.ResolveReportPath();
        string reportDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(reportDirectory))
            Directory.CreateDirectory(reportDirectory);
        if (WildlifeAiHuntPlayModeVerifier.IsCreateOnlyReport)
        {
            using (FileStream stream = new(
                       reportPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            using (StreamWriter writer = new(
                       stream,
                       new System.Text.UTF8Encoding(false)))
            {
                foreach (string line in lines)
                    writer.WriteLine(line);
            }
            WriteRawReport();
        }
        else
        {
            File.WriteAllLines(reportPath, lines);
        }
        if (failures.Count == 0)
        {
            Debug.Log("WILDLIFE_AI_HUNT=PASS");
        }
        else
        {
            Debug.LogError("WILDLIFE_AI_HUNT=FAIL; " + string.Join(" | ", failures));
        }
    }

    private void ValidateRawObservations()
    {
        if (!WildlifeAiHuntPlayModeVerifier.IsCreateOnlyReport
            || rawObservationsValidated)
        {
            return;
        }

        rawObservationsValidated = true;
        string[] invalidStages = RequiredRawStages
            .Where(stage => rawObservations.Count(value => string.Equals(
                (string)value?["stage"],
                stage,
                StringComparison.Ordinal)) != 1)
            .ToArray();
        Check(
            invalidStages.Length == 0,
            "RAW_OBSERVATION_STAGES_EXACTLY_ONCE",
            invalidStages.Length == 0
                ? string.Join(",", RequiredRawStages)
                : "missing-or-duplicate=" + string.Join(",", invalidStages));
        JToken terminal = rawObservations.FirstOrDefault(value =>
            string.Equals((string)value?["stage"], "terminal-before-cleanup",
                StringComparison.Ordinal));
        Check(terminal != null
                && (int?)terminal["targetHealthObserved"] == 0
                && (bool?)terminal["targetAliveObserved"] == false
                && (string)terminal["targetStateObserved"] == "Dead",
            "RAW_TERMINAL_SAME_TARGET_LETHAL_STATE",
            terminal == null ? "terminal-observation-unavailable"
                : $"health={terminal["targetHealthObserved"]};alive={terminal["targetAliveObserved"]};"
                    + $"state={terminal["targetStateObserved"]};nativePresent={terminal["targetNativeObjectPresent"]};"
                    + $"source={terminal["targetHealthObservationSource"]}");
    }

    private void WriteRawReport()
    {
        JObject raw = CanonicalObject(
            new HuntRawReportSnapshot
            {
                passed = Passed,
                fixtureScope = RawFixtureScope
            },
            "E11 raw report metadata");
        raw["observations"] = new JArray(
            rawObservations.Select(value => value.DeepClone()));
        raw["checks"] = CanonicalStringArray(checks, "E11 checks");
        raw["failures"] = CanonicalStringArray(failures, "E11 failures");

        string rawPath = WildlifeAiHuntPlayModeVerifier.ResolveRawReportPath();
        using FileStream stream = new(
            rawPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(
            stream,
            new System.Text.UTF8Encoding(false));
        writer.Write(raw.ToString(Formatting.Indented));
        writer.WriteLine();
    }

    private static JArray CanonicalStringArray(
        IEnumerable<string> values,
        string label)
    {
        JObject wrapper = CanonicalObject(
            new RawStringArraySnapshot
            {
                values = values?.ToList() ?? new List<string>()
            },
            label);
        if (wrapper["values"] is not JArray array)
            throw new InvalidOperationException(label + " did not produce a canonical array.");
        return (JArray)array.DeepClone();
    }

    private static CharacterActor[] GetLiveWorkers() =>
        CharacterActorCollection.DistinctByGameObject(
            UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None))
        .Where(actor => actor != null
            && !actor.IsDead
            && actor.CurrentLifecycleState == CharacterLifecycleState.Active
            && CharacterWorkRoleUtility.TryGetWork(actor, out _))
        .ToArray();

    [Serializable]
    private sealed class HuntRawReportSnapshot
    {
        public string schema = "dungeonstory-e11-wildlife-hunt-current-raw@1";
        public bool passed;
        public bool trainingEligible;
        public bool naturalGameplay;
        public bool actualBrainPath = true;
        public bool wholeWorldColdRestore;
        public bool testOnlyFixture = true;
        public bool fullAuditClosure;
        public bool humanApprovalClaimed;
        public string fixtureScope = string.Empty;
    }

    [Serializable]
    private sealed class HuntRawObservationSnapshot
    {
        public string stage = string.Empty;
        public string fixtureScope = string.Empty;
        public bool testOnlyFixture;
        public string hunterStableId = string.Empty;
        public string targetStableId = string.Empty;
        public int targetHealthObserved;
        public bool targetAliveObserved;
        public string targetStateObserved = string.Empty;
        public bool targetNativeObjectPresent;
        public string targetHealthObservationSource = string.Empty;
        public int carcassCount;
        public int reserveAmmunition;
        public int loadedAmmunition;
        public int ammunitionTotal;
        public bool huntAbilityRunning;
        public string activeActionType = string.Empty;
        public bool activeActionStarted;
        public string currentActionPhase = string.Empty;
        public string lastActionFailure = string.Empty;
        public ActiveWeaponRawSnapshot activeWeapon;
        public CharacterAiGateRawSnapshot gate;
    }

    [Serializable]
    private sealed class ActiveWeaponRawSnapshot
    {
        public string definitionId = string.Empty;
        public string instanceId = string.Empty;
        public string kind = string.Empty;
        public string quality = string.Empty;
        public string ammunitionItemId = string.Empty;
        public int magazineCapacity;
        public int loadedAmmo;
        public int maximumRange;
        public float reloadSeconds;
        public bool supportsAimed;
        public bool supportsRapid;
        public bool supportsSuppressive;
        public float materialDamageMultiplier;
        public float materialPenetrationMultiplier;
        public float evolutionAccuracyMultiplier;
        public bool gunpowderWeapon;
        public float durabilityRatio;
        public float maximumMisfireChance;
        public float misfireChance;
        public float smokeExposure;
        public string roleFlags = string.Empty;
        public bool isRanged;
        public bool requiresAmmo;
        public string verbType = string.Empty;
        public string verbKind = string.Empty;
        public float attackTime;
        public float baseDamage;
        public float penetration;
        public string damageType = string.Empty;
        public float tracking;
        public bool verbConsumesAmmo;
        public bool verbDropsWeaponOnUse;
        public float projectileSpeed;
        public List<CombatRangeProfile> ranges = new();

        public static ActiveWeaponRawSnapshot Capture(
            CombatWeaponSnapshot snapshot)
        {
            if (snapshot?.Verb == null)
            {
                throw new InvalidOperationException(
                    "E11 raw active weapon verb is unavailable.");
            }

            float projectileSpeed = snapshot.Verb switch
            {
                ProjectileVerb projectile => projectile.projectileSpeed,
                RecoverableThrowVerb recoverable => recoverable.projectileSpeed,
                _ => throw new InvalidOperationException(
                    "E11 raw active ranged weapon has no projectile speed.")
            };
            return new ActiveWeaponRawSnapshot
            {
                definitionId = snapshot.DefinitionId,
                instanceId = snapshot.InstanceId,
                kind = snapshot.Kind.ToString(),
                quality = snapshot.Quality.ToString(),
                ammunitionItemId = snapshot.AmmunitionItemId,
                magazineCapacity = snapshot.MagazineCapacity,
                loadedAmmo = snapshot.LoadedAmmo,
                maximumRange = snapshot.MaximumRange,
                reloadSeconds = snapshot.ReloadSeconds,
                supportsAimed = snapshot.SupportsAimed,
                supportsRapid = snapshot.SupportsRapid,
                supportsSuppressive = snapshot.SupportsSuppressive,
                materialDamageMultiplier = snapshot.MaterialDamageMultiplier,
                materialPenetrationMultiplier =
                    snapshot.MaterialPenetrationMultiplier,
                evolutionAccuracyMultiplier = snapshot.EvolutionAccuracyMultiplier,
                gunpowderWeapon = snapshot.GunpowderWeapon,
                durabilityRatio = snapshot.DurabilityRatio,
                maximumMisfireChance = snapshot.MaximumMisfireChance,
                misfireChance = snapshot.MisfireChance,
                smokeExposure = snapshot.SmokeExposure,
                roleFlags = snapshot.RoleFlags.ToString(),
                isRanged = snapshot.IsRanged,
                requiresAmmo = snapshot.RequiresAmmo,
                verbType = snapshot.Verb.GetType().FullName ?? string.Empty,
                verbKind = snapshot.Verb.Kind.ToString(),
                attackTime = snapshot.Verb.attackTime,
                baseDamage = snapshot.Verb.baseDamage,
                penetration = snapshot.Verb.penetration,
                damageType = snapshot.Verb.damageType.ToString(),
                tracking = snapshot.Verb.tracking,
                verbConsumesAmmo = snapshot.Verb.ConsumesAmmo,
                verbDropsWeaponOnUse = snapshot.Verb.DropsWeaponOnUse,
                projectileSpeed = projectileSpeed,
                ranges = snapshot.Ranges
                    .Where(value => value != null)
                    .ToList()
            };
        }
    }

    [Serializable]
    private sealed class CharacterAiGateRawSnapshot
    {
        public long actionStarts;
        public long actionTerminals;
        public long actionCompleted;
        public long actionFailed;
        public long actionCancelled;
        public int liveActions;
        public long progressRevision;
        public long gameplayProgressRevision;
        public long facilityQueueHeartbeats;
        public long facilityServiceHeartbeats;
        public long pathRequests;
        public long pathResults;
        public int livePathRequests;
        public long reservationAcquires;
        public long reservationReleases;
        public int liveReservations;
        public long retrySchedules;
        public long retryAttempts;
        public long schedulerProcesses;
        public long schedulerOverdue;
        public int maximumSchedulerDelayMilliseconds;
        public long invariantAnomalies;
        public long failureLoops;

        public static CharacterAiGateRawSnapshot Capture(
            CharacterAiRuntimeGateSnapshot snapshot) => new()
        {
            actionStarts = snapshot.ActionStarts,
            actionTerminals = snapshot.ActionTerminals,
            actionCompleted = snapshot.ActionCompleted,
            actionFailed = snapshot.ActionFailed,
            actionCancelled = snapshot.ActionCancelled,
            liveActions = snapshot.LiveActions,
            progressRevision = snapshot.ProgressRevision,
            gameplayProgressRevision = snapshot.GameplayProgressRevision,
            facilityQueueHeartbeats = snapshot.FacilityQueueHeartbeats,
            facilityServiceHeartbeats = snapshot.FacilityServiceHeartbeats,
            pathRequests = snapshot.PathRequests,
            pathResults = snapshot.PathResults,
            livePathRequests = snapshot.LivePathRequests,
            reservationAcquires = snapshot.ReservationAcquires,
            reservationReleases = snapshot.ReservationReleases,
            liveReservations = snapshot.LiveReservations,
            retrySchedules = snapshot.RetrySchedules,
            retryAttempts = snapshot.RetryAttempts,
            schedulerProcesses = snapshot.SchedulerProcesses,
            schedulerOverdue = snapshot.SchedulerOverdue,
            maximumSchedulerDelayMilliseconds =
                snapshot.MaximumSchedulerDelayMilliseconds,
            invariantAnomalies = snapshot.InvariantAnomalies,
            failureLoops = snapshot.FailureLoops
        };
    }

    [Serializable]
    private sealed class RawStringArraySnapshot
    {
        public List<string> values = new();
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
}
#endif
