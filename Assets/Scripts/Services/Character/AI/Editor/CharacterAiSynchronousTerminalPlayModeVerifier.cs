using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Focused production-component regression for synchronous action terminals.
/// The cross-domain fault verifier owns resource-loss fixtures; this runner
/// protects the common invariant that an immediately completed coroutine may
/// not be stored as a live executor handle and repeated cleanup is idempotent.
/// </summary>
public static class CharacterAiSynchronousTerminalPlayModeVerifier
{
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    public const string ReportPath =
        "Artifacts/QA/character-ai-synchronous-terminal-playmode.txt";
    private const string PendingPath =
        "Temp/character-ai-synchronous-terminal-playmode.flag";

    [MenuItem("DungeonStory/Debug/QA/Run AI Synchronous Terminal PlayMode Matrix")]
    public static void RequestRun()
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingPath, "run");
        if (EditorApplication.isPlaying)
        {
            StartRunner();
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
        StartRunner();
    }

    private static void StartRunner()
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                CharacterAiSynchronousTerminalPlayModeRunner>() != null)
        {
            return;
        }

        new GameObject(nameof(CharacterAiSynchronousTerminalPlayModeRunner))
            .AddComponent<CharacterAiSynchronousTerminalPlayModeRunner>();
    }
}

public sealed class CharacterAiSynchronousTerminalPlayModeRunner : MonoBehaviour
{
    private readonly List<string> rows = new();
    private readonly List<string> failures = new();

    private IEnumerator Start()
    {
        yield return Run();
        Directory.CreateDirectory(Path.GetDirectoryName(
            CharacterAiSynchronousTerminalPlayModeVerifier.ReportPath) ?? "Artifacts/QA");
        List<string> report = new()
        {
            "# Character AI synchronous terminal PlayMode matrix",
            "authority=production AIBrain/action-set/ability components",
            "result=" + (failures.Count == 0 ? "PASS" : "FAIL")
        };
        report.AddRange(rows);
        File.WriteAllLines(
            CharacterAiSynchronousTerminalPlayModeVerifier.ReportPath,
            report);
        Debug.Log(failures.Count == 0
            ? "AI_SYNCHRONOUS_TERMINAL_MATRIX=PASS"
            : "AI_SYNCHRONOUS_TERMINAL_MATRIX=FAIL; " + string.Join(" | ", failures));
        yield return null;
        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private IEnumerator Run()
    {
        CharacterActor actor = null;
        float deadline = Time.realtimeSinceStartup + 10f;
        bool attemptedStartParty = false;
        while (actor == null && Time.realtimeSinceStartup < deadline)
        {
            if (!attemptedStartParty
                && FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include)?.Container != null)
            {
                attemptedStartParty = true;
                StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            }
            actor = CharacterActorCollection.DistinctByGameObject(
                    FindObjectsByType<CharacterActor>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None))
                .FirstOrDefault(value => value != null
                    && !value.IsDead
                    && value.CurrentLifecycleState == CharacterLifecycleState.Active);
            yield return null;
        }

        Check(actor != null && actor.Brain != null, "live actor/brain resolved");
        if (actor == null || actor.Brain == null) yield break;

        actor.Brain.StopAllAiForLifecycleTransition("sync-terminal-verifier-setup");

        AIShopping shoppingActionSet = ScriptableObject.CreateInstance<AIShopping>();
        try
        {
            AIAction action = new(shoppingActionSet, AIActionPlan.WithoutDestination);
            actor.Brain.availableActions = new AIAction[] { action };
            actor.Brain.bestAction = action;
            actor.Brain.isBestActionEnd = false;
            actor.Brain.isExecuted = false;
            CharacterAiDecisionTickResult executed = new CharacterAiDecisionPipeline(
                    NoCharacterDeprivationBoundary.Instance,
                    NoCharacterDeprivationBoundary.Instance)
                .RunSelectedAction(actor, "sync-terminal:shopping:no-destination");
            AbilityShopping shopping = actor.GetAbility<AbilityShopping>();
            Check(executed.Handled, "shopping selected through production pipeline");
            Check(shopping != null && !shopping.HasActiveShoppingRoutineForDiagnostics,
                "shopping immediate terminal did not retain coroutine handle");
            Check(actor.Brain.bestAction == null && !actor.Brain.isExecuted,
                "shopping immediate terminal cleared AIBrain action");
            shopping?.StopShopping("sync-terminal-idempotent-1");
            shopping?.StopShopping("sync-terminal-idempotent-2");
            Check(shopping == null || !shopping.HasActiveShoppingRoutineForDiagnostics,
                "shopping repeated cleanup remained idempotent");
        }
        finally
        {
            Destroy(shoppingActionSet);
        }

        AbilityUseSubstance substance = AbilityUseSubstance.Ensure(actor);
        substance?.StopUse("sync-terminal-idempotent-1");
        substance?.StopUse("sync-terminal-idempotent-2");
        Check(substance == null || !substance.IsUsingSubstance,
            "substance repeated terminal cleanup retained no executor");

        AbilityHaul haul = AbilityHaul.Ensure(actor);
        haul?.StopHauling("sync-terminal-idempotent-1");
        haul?.StopHauling("sync-terminal-idempotent-2");
        Check(haul == null || !haul.IsHauling,
            "haul repeated terminal cleanup retained no executor");

        AbilityRescue rescue = AbilityRescue.Ensure(actor);
        rescue?.StopRescue(CharacterMedicalStatusCode.RescueInterrupted);
        rescue?.StopRescue(CharacterMedicalStatusCode.RescueInterrupted);
        Check(rescue == null || !rescue.IsRescuing,
            "rescue repeated terminal cleanup retained no executor");

        AbilityCaptiveEscort escort = actor.GetComponent<AbilityCaptiveEscort>();
        if (escort != null)
        {
            escort.StopEscort("sync-terminal-idempotent-1");
            escort.StopEscort("sync-terminal-idempotent-2");
        }
        Check(escort == null || !escort.IsEscorting,
            "captive escort repeated terminal cleanup retained no executor");

        actor.Brain.RequestImmediateReplan(clearFailures: true);
    }

    private void Check(bool passed, string detail)
    {
        string row = (passed ? "PASS\t" : "FAIL\t") + detail;
        rows.Add(row);
        if (!passed) failures.Add(detail);
    }
}
