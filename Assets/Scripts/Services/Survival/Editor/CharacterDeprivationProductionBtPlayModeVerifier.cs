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
    private static bool runnerCreated;

    static CharacterDeprivationProductionBtPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Run Deprivation Production BT Matrix")]
    public static void RequestRun()
    {
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
        if (StartRunner()) File.Delete(PendingPath);
    }

    private static bool StartRunner()
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                CharacterDeprivationProductionBtPlayModeRunner>() != null)
        {
            runnerCreated = true;
            return true;
        }
        if (runnerCreated) return false;
        CharacterDeprivationProductionBtPlayModeRunner runner =
            new GameObject(nameof(CharacterDeprivationProductionBtPlayModeRunner))
                .AddComponent<CharacterDeprivationProductionBtPlayModeRunner>();
        runnerCreated = runner != null;
        return runnerCreated;
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
    private DungeonGameSaveData baseline;
    private string actorId;
    private float oldTimeScale;
    private bool finished;
    private bool reportWritten;

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
        Destroy(gameObject);
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
        if (!string.IsNullOrWhiteSpace(fixtureItemId))
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
            "V18 aggregate restore + production CharacterStats mutation; "
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
        if (condition != CharacterCondition.MOOD)
        {
            float current = GetNeed(actor, condition);
            actor.ChangesStat(condition, 5f - current);
        }
        else
        {
            float current = GetNeed(actor, CharacterCondition.MOOD);
            actor.ChangesStat(CharacterCondition.MOOD, 5f - current);
        }

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
        bool valid = actor != null
            && deprivation.HasBreakdownKind(actor, kind);
        Check(valid,
            prefix + "_V18_BREAKDOWN_PRODUCER",
            valid
                ? $"kind={kind};cause={cause};need={GetNeed(actor, condition):0.##};reason={producerDetail}"
                : authoredReport?.ToString() ?? "actor/state missing");
        if (!valid) return false;
        PauseOtherAi(actorId);
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
            CharacterDeprivationProductionBtPlayModeVerifier.ReportPath)
            ?? "Artifacts/QA");
        File.WriteAllLines(
            CharacterDeprivationProductionBtPlayModeVerifier.ReportPath,
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
