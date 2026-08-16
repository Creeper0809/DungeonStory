#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class WildlifeAiHuntPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/wildlife-ai-hunt-playmode.txt";
    private const string PendingFlagPath =
        "Temp/wildlife-ai-hunt-playmode.flag";

    [MenuItem("DungeonStory/Debug/QA/Run Wildlife AI Hunt PlayMode Verification")]
    public static void RunFromMenu() => RequestRun();

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
}

public sealed class WildlifeAiHuntPlayModeRunner : MonoBehaviour
{
    private const float VerificationTimeScale = 8f;
    private const float SetupTimeoutRealtime = 12f;
    private const float HuntTimeoutRealtime = 35f;

    private readonly List<string> checks = new();
    private readonly List<string> failures = new();
    private readonly List<MonoBehaviourState> pausedAi = new();
    private readonly Dictionary<CharacterCondition, float> originalStats = new();

    private CharacterActor hunter;
    private AbilityWork work;
    private AIBrain brain;
    private AbilityHunt huntAbility;
    private WildlifeRuntime wildlife;
    private IWorldItemStackRuntime items;
    private AIAction[] originalActions;
    private WorkPriorityLevel originalHuntPriority;
    private float originalTimeScale;

    private IEnumerator Start()
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
            Cleanup();
            Time.timeScale = originalTimeScale;
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
    }

    private IEnumerator ResolveWorld()
    {
        float deadline = Time.realtimeSinceStartup + SetupTimeoutRealtime;
        DungeonRuntimeLifetimeScope scope = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = UnityEngine.Object.FindFirstObjectByType<
                DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include);
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

        PauseOtherAi();
        Check(wildlife != null, "WILDLIFE_RUNTIME", "resolved");
        Check(items != null, "ITEM_RUNTIME", "resolved");
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
        wildlife.DesignateHunt(target.WildlifeId, true, priority: true);
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
        Check(minimumHealth < healthBefore, "HUNT_APPLIED_DAMAGE",
            $"health={healthBefore}->{minimumHealth}");
        Check(killed, "HUNT_TARGET_KILLED",
            $"alive={target?.IsAlive}; health={target?.CurrentHealth}; phase={brain.CurrentActionPhase}; failure={brain.LastActionFailure}");
        Check(carcassesAfter == carcassesBefore + 1,
            "HUNT_CARCASS_EXACTLY_ONCE",
            $"carcasses={carcassesBefore}->{carcassesAfter}");
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
    }

    private int CountCarcasses() => items?.GetAllStacks().Count(stack =>
        stack != null
        && WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
            stack.ItemId,
            out _)) ?? 0;

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
        foreach (MonoBehaviourState state in pausedAi)
        {
            if (state.Component != null)
            {
                state.Component.enabled = state.WasEnabled;
            }
        }
    }

    private void Check(bool passed, string id, string detail)
    {
        checks.Add($"{id}\t{(passed ? "PASS" : "FAIL")}\t{detail}");
        if (!passed)
        {
            failures.Add(id + ": " + detail);
        }
    }

    private void WriteReport()
    {
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
        File.WriteAllLines(WildlifeAiHuntPlayModeVerifier.ReportPath, lines);
        if (failures.Count == 0)
        {
            Debug.Log("WILDLIFE_AI_HUNT=PASS");
        }
        else
        {
            Debug.LogError("WILDLIFE_AI_HUNT=FAIL; " + string.Join(" | ", failures));
        }
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
