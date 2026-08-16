#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>
/// Live Brain -> behaviour tree -> action verification for self-care actions
/// that previously had only domain-level coverage.
/// </summary>
public static class CharacterAiSelfCarePlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/character-ai-self-care-playmode.txt";
    private const string PendingFlagPath =
        "Temp/character-ai-self-care-playmode.flag";

    [MenuItem("DungeonStory/Debug/QA/Run Character AI Self-Care PlayMode Verification")]
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
        if (!File.Exists(PendingFlagPath)) return;
        File.Delete(PendingFlagPath);
        StartRunner();
    }

    private static void StartRunner()
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                CharacterAiSelfCarePlayModeRunner>() != null)
        {
            return;
        }

        new GameObject("Character AI Self-Care PlayMode Runner")
            .AddComponent<CharacterAiSelfCarePlayModeRunner>();
    }
}

public sealed class CharacterAiSelfCarePlayModeRunner : MonoBehaviour
{
    private const float SetupTimeoutRealtime = 12f;
    private const float ScenarioTimeoutRealtime = 25f;
    private const string TonicItemId = "drug:vitality-tonic";
    private const string TonicSubstanceId = "substance:vitality-tonic";

    private readonly List<string> checks = new();
    private readonly List<string> failures = new();
    private readonly List<string> consoleIssues = new();
    private readonly List<MonoBehaviourState> pausedAi = new();
    private readonly Dictionary<CharacterCondition, float> originalStats = new();

    private CharacterActor actor;
    private AIBrain brain;
    private AIAction[] originalActions;
    private IWorldItemStackRuntime items;
    private ICharacterDeprivationCommand deprivationCommands;
    private ICharacterDeprivationQuery deprivationQuery;
    private ICharacterNeedBalanceRuntime needBalance;
    private ICharacterSubstanceRuntime substances;
    private float originalTimeScale;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        originalTimeScale = Time.timeScale;
        Time.timeScale = 6f;
        Application.logMessageReceived += CaptureIssue;

        try
        {
            yield return ResolveWorld();
            if (failures.Count == 0)
            {
                yield return VerifyRoutineDrink();
                yield return VerifyScheduledSubstanceUse();
            }
        }
        finally
        {
            Cleanup();
            Application.logMessageReceived -= CaptureIssue;
            Time.timeScale = originalTimeScale;
            WriteReport();
            Destroy(gameObject);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying)
                    EditorApplication.isPlaying = false;
            };
        }
    }

    private IEnumerator ResolveWorld()
    {
        DungeonRuntimeLifetimeScope scope = null;
        float deadline = Time.realtimeSinceStartup + SetupTimeoutRealtime;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = UnityEngine.Object.FindFirstObjectByType<
                DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include);
            CharacterActor[] live = LiveWorkers();
            if (scope != null && scope.Container != null && live.Length > 0)
                break;
            if (scope != null && live.Length == 0)
            {
                checks.Add("SETUP\tINFO\t"
                    + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            }
            yield return null;
        }

        actor = LiveWorkers().FirstOrDefault(candidate =>
            candidate.Brain?.availableActions?.Any(action =>
                action?.actionset is AIDrink) == true
            && candidate.Brain.availableActions.Any(action =>
                action?.actionset is AISubstanceUse));
        Check(scope?.Container != null, "LIVE_SCOPE", scope?.name ?? "missing");
        Check(actor != null, "LIVE_SELF_CARE_ACTOR", actor?.name ?? "missing");
        if (scope?.Container == null || actor == null) yield break;

        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        deprivationCommands = scope.Container.Resolve<ICharacterDeprivationCommand>();
        deprivationQuery = scope.Container.Resolve<ICharacterDeprivationQuery>();
        needBalance = scope.Container.Resolve<ICharacterNeedBalanceRuntime>();
        substances = scope.Container.Resolve<ICharacterSubstanceRuntime>();
        brain = actor.Brain;
        originalActions = brain.availableActions;
        foreach (KeyValuePair<CharacterCondition, float> pair in
                 actor.Stats.StatSnapshot)
        {
            originalStats[pair.Key] = pair.Value;
        }
        PauseOtherAi();
        NeutralizeNeeds();
        actor.SetAiPaused(false);
        brain.enabled = true;
        if (actor.BehaviorTree != null) actor.BehaviorTree.enabled = true;
        Check(items != null, "ITEM_RUNTIME", items != null ? "resolved" : "missing");
        Check(deprivationCommands != null, "DRINK_RUNTIME", "resolved");
        Check(needBalance != null, "NEED_BALANCE_RUNTIME", "resolved");
        Check(substances != null, "SUBSTANCE_RUNTIME", "resolved");
    }

    private IEnumerator VerifyRoutineDrink()
    {
        AIAction drink = originalActions.FirstOrDefault(action =>
            action?.actionset is AIDrink);
        Check(drink != null, "AUTHORED_DRINK_ACTION", drink?.actionset?.name ?? "missing");
        if (drink == null) yield break;

        int waterBefore = CountWorldItem("resource:clean-water");
        Check(waterBefore > 0, "DRINK_PHYSICAL_SUPPLY", $"water={waterBefore}");
        if (waterBefore <= 0) yield break;

        CharacterNeedResponseProfile thirstResponse =
            needBalance.GetResponse(CharacterCondition.THIRST);
        float forecastLoss = actor.Stats.GetExpectedTimedNeedLoss(
            CharacterCondition.THIRST,
            90f);
        float nonEmergencyFloor = 20f + forecastLoss + 1f;
        float routineUpper = thirstResponse.routineStart;
        bool validRoutineBand = routineUpper > Mathf.Max(
            thirstResponse.emergencyStart + 1f,
            nonEmergencyFloor);
        Check(validRoutineBand, "DRINK_ROUTINE_FIXTURE_BAND",
            $"emergency={thirstResponse.emergencyStart:0.##}; "
            + $"routine={thirstResponse.routineStart:0.##}; "
            + $"forecast90s={forecastLoss:0.##}; nonEmergencyFloor={nonEmergencyFloor:0.##}");
        if (!validRoutineBand) yield break;

        // Start at the top of the routine band. The old midpoint fixture was
        // projected below the physical-harm floor within the 90-second care
        // horizon, so production correctly promoted it to emergency relief.
        float routineThirst = routineUpper;
        actor.SetAiPaused(true);
        NeutralizeNeeds();
        bool deprivationReset = deprivationCommands
            .DebugResetForDeterministicScenario(actor);
        brain.StopCurrentActionForReplan("self-care-drink-fixture-reset");

        bool fixtureSettled = false;
        float settleDeadline = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < settleDeadline)
        {
            CharacterAiRuntimeGateSnapshot gate = brain.CaptureRuntimeGateSnapshot();
            fixtureSettled = brain.bestAction == null
                && !brain.IsExternallyDrivenActionActive
                && deprivationQuery?.IsRoutineDrinkActionActive(actor) != true
                && gate.LivePathRequests == 0
                && gate.LiveReservations == 0;
            if (fixtureSettled)
            {
                yield return null;
                break;
            }

            yield return null;
        }
        Check(deprivationReset && fixtureSettled, "DRINK_ROUTINE_FIXTURE_SETTLED",
            $"reset={deprivationReset}; settled={fixtureSettled}; "
            + $"best={brain.bestAction?.actionset}; external={brain.IsExternallyDrivenActionActive}; "
            + $"routineRunner={deprivationQuery?.IsRoutineDrinkActionActive(actor)}");
        if (!deprivationReset || !fixtureSettled)
        {
            actor.SetAiPaused(false);
            yield break;
        }

        actor.Stats.Stats[CharacterCondition.THIRST] = routineThirst;
        string routineReason = string.Empty;
        bool routineNeeded = deprivationQuery?.NeedsRoutineDrink(
            actor,
            out routineReason) == true;
        bool emergencyOrImminent = CharacterNeedAiThresholds
            .IsEmergencyOrImminentPhysicalHarm(
                actor,
                CharacterCondition.THIRST);
        Check(routineNeeded && !emergencyOrImminent,
            "DRINK_ROUTINE_NOT_EMERGENCY",
            $"thirst={routineThirst:0.##}; routineNeeded={routineNeeded}; "
            + $"emergencyOrImminent={emergencyOrImminent}; reason={routineReason}");
        if (!routineNeeded || emergencyOrImminent)
        {
            actor.SetAiPaused(false);
            yield break;
        }

        brain.availableActions = new[] { drink };
        brain.PreferActionOnNextDecision<AIDrink>(180f);
        CharacterAiRuntimeGateSnapshot before = brain.CaptureRuntimeGateSnapshot();
        int externalTransitionsBefore = brain.ExternalIntentTransitionCount;
        actor.SetAiPaused(false);
        brain.RequestImmediateReplan(clearFailures: true);

        bool selected = false;
        bool externalOwnerObserved = false;
        bool simultaneousOwnerOverlap = false;
        string firstExternalDetail = string.Empty;
        string firstOverlapDetail = string.Empty;
        float deadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
        while (Time.realtimeSinceStartup < deadline
            && (brain.CaptureRuntimeGateSnapshot().ActionTerminals
                    <= before.ActionTerminals
                || CountWorldItem("resource:clean-water") >= waterBefore))
        {
            CharacterAiRuntimeGateSnapshot gate = brain.CaptureRuntimeGateSnapshot();
            bool drinkSelected = brain.bestAction?.actionset is AIDrink;
            bool drinkEpochLive = drinkSelected
                && gate.GetBranch(CharacterAiBranch.Drink).LiveActions > 0;
            bool externalActive = brain.IsExternallyDrivenActionActive;
            selected |= drinkSelected;
            if (externalActive && !externalOwnerObserved)
            {
                firstExternalDetail = $"frame={Time.frameCount}; "
                    + $"owner={brain.ExternalIntentOwnerId}; kind={brain.ExternalIntentKind}; "
                    + $"externalEpoch={brain.ExternalIntentEpoch}; actionEpoch={brain.RuntimeActionEpoch}";
            }
            externalOwnerObserved |= externalActive;
            if (drinkEpochLive && externalActive && !simultaneousOwnerOverlap)
            {
                firstOverlapDetail = $"frame={Time.frameCount}; "
                    + $"owner={brain.ExternalIntentOwnerId}; kind={brain.ExternalIntentKind}; "
                    + $"externalEpoch={brain.ExternalIntentEpoch}; actionEpoch={brain.RuntimeActionEpoch}";
            }
            simultaneousOwnerOverlap |= drinkEpochLive && externalActive;
            yield return null;
        }
        yield return null;
        yield return null;

        int waterAfter = CountWorldItem("resource:clean-water");
        CharacterAiRuntimeGateSnapshot after = brain.CaptureRuntimeGateSnapshot();
        // Routine drinking remains under the selected AIDrink action epoch.
        // External intent ownership is reserved for emergency safe relief;
        // overlapping it here would create two execution authorities.
        int externalTransitionDelta = brain.ExternalIntentTransitionCount
            - externalTransitionsBefore;
        Check(selected && !simultaneousOwnerOverlap,
            "DRINK_SELECTED_ACTION_EPOCH_OWNED",
            $"selectedFrameObserved={selected}; overlap={simultaneousOwnerOverlap}; "
            + $"externalEver={externalOwnerObserved}; phase={brain.CurrentActionPhase}; "
            + $"firstOverlap={firstOverlapDetail}");
        Check(!simultaneousOwnerOverlap, "DRINK_NO_EXTERNAL_OWNER_OVERLAP",
            $"overlap={simultaneousOwnerOverlap}; externalEver={externalOwnerObserved}; "
            + $"firstExternal={firstExternalDetail}; firstOverlap={firstOverlapDetail}");
        Check(externalTransitionDelta == 0,
            "DRINK_ROUTINE_EXTERNAL_TRANSITION_ZERO",
            $"transitions={externalTransitionsBefore}->{brain.ExternalIntentTransitionCount}; "
            + $"firstExternal={firstExternalDetail}");
        Check(actor.Stats.Stats[CharacterCondition.THIRST] > routineThirst,
            "DRINK_RESTORED_THIRST",
            $"thirst={routineThirst:0.##}->{actor.Stats.Stats[CharacterCondition.THIRST]:0.##}");
        Check(waterAfter == waterBefore - 1,
            "DRINK_PHYSICAL_EXACTLY_ONCE", $"water={waterBefore}->{waterAfter}");
        Check(after.ActionTerminals >= before.ActionTerminals,
            "DRINK_LIFECYCLE_CONSERVED",
            $"starts={before.ActionStarts}->{after.ActionStarts}; terminals={before.ActionTerminals}->{after.ActionTerminals}");
        Check(after.InvariantAnomalies == before.InvariantAnomalies,
            "DRINK_NO_INVARIANT_ANOMALY",
            $"invariants={before.InvariantAnomalies}->{after.InvariantAnomalies}");

        brain.StopCurrentActionForReplan("self-care-drink-complete");
        actor.Stats.Stats[CharacterCondition.THIRST] = 100f;
        yield return null;
    }

    private IEnumerator VerifyScheduledSubstanceUse()
    {
        AIAction substance = originalActions.FirstOrDefault(action =>
            action?.actionset is AISubstanceUse);
        Check(substance != null, "AUTHORED_SUBSTANCE_ACTION",
            substance?.actionset?.name ?? "missing");
        if (substance == null) yield break;

        NeutralizeNeeds();
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        int beforeExisting = CountWorldItem(TonicItemId) + inventory.CountItem(TonicItemId);
        bool spawned = items.SpawnItemAt(
            TonicItemId,
            1,
            actor.GetNowXY(),
            WorldItemStackState.Loose,
            string.Empty,
            out int spawnedCount);
        Check(spawned && spawnedCount == 1, "SUBSTANCE_PHYSICAL_SUPPLY",
            $"spawned={spawnedCount}; before={beforeExisting}");
        if (!spawned) yield break;

        substances.SetPolicy(
            actor,
            TonicSubstanceId,
            SubstancePolicyMode.Scheduled,
            moodThreshold: 100f,
            scheduledHour: 0);
        Check(substances.TryGetAutomaticUseRequest(actor, out CharacterSubstanceUseRequest request)
                && request.ItemId == TonicItemId,
            "SUBSTANCE_AUTOMATIC_REQUEST",
            $"item={request.ItemId}; urgency={request.Urgency:0.##}; reason={request.Reason}");

        brain.StopCurrentActionForReplan("self-care-substance-setup");
        brain.availableActions = new[] { substance };
        brain.PreferActionOnNextDecision<AISubstanceUse>(180f);
        CharacterAiRuntimeGateSnapshot before = brain.CaptureRuntimeGateSnapshot();
        brain.RequestImmediateReplan(clearFailures: true);

        bool selected = false;
        bool abilityRunning = false;
        CharacterSubstanceState state = default;
        AIActionFailure observedFailure = AIActionFailure.None;
        string observedPhase = string.Empty;
        string observedPhaseDetail = string.Empty;
        float deadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
        while (Time.realtimeSinceStartup < deadline)
        {
            selected |= brain.bestAction?.actionset is AISubstanceUse;
            abilityRunning |= actor.GetComponent<AbilityUseSubstance>()?.IsUsingSubstance == true;
            if (brain.LastActionFailure.HasFailure)
            {
                observedFailure = brain.LastActionFailure;
                observedPhase = brain.CurrentActionPhase;
                observedPhaseDetail = brain.CurrentActionPhaseDetail;
            }
            state = substances.GetState(actor, TonicSubstanceId);
            if (state.activeSeconds > 0.01f) break;
            yield return null;
        }
        yield return null;
        yield return null;

        int afterCount = CountWorldItem(TonicItemId) + inventory.CountItem(TonicItemId);
        CharacterAiRuntimeGateSnapshot after = brain.CaptureRuntimeGateSnapshot();
        Check(selected, "SUBSTANCE_BT_SELECTED",
            $"selected={selected}; phase={brain.CurrentActionPhase}; observedPhase={observedPhase}; "
            + $"detail={observedPhaseDetail}; failure={observedFailure}");
        Check(abilityRunning, "SUBSTANCE_ABILITY_RAN", $"runningObserved={abilityRunning}");
        Check(state.activeSeconds > 0.01f, "SUBSTANCE_EFFECT_ACTIVE",
            $"activeSeconds={state.activeSeconds:0.##}; tolerance={state.tolerance:0.##}");
        Check(afterCount == beforeExisting, "SUBSTANCE_PHYSICAL_EXACTLY_ONCE",
            $"total={beforeExisting + 1}->{afterCount}");
        Check(after.ActionStarts >= before.ActionStarts + 1,
            "SUBSTANCE_ACTION_STARTED",
            $"starts={before.ActionStarts}->{after.ActionStarts}");
        Check(after.ActionTerminals >= before.ActionTerminals + 1
                && after.ActionCompleted >= before.ActionCompleted + 1,
            "SUBSTANCE_TYPED_COMPLETION",
            $"terminals={before.ActionTerminals}->{after.ActionTerminals}; "
            + $"completed={before.ActionCompleted}->{after.ActionCompleted}");
        Check(after.LivePathRequests == 0 && after.LiveReservations == 0,
            "SUBSTANCE_NO_OWNERSHIP_LEAK",
            $"paths={after.LivePathRequests}; reservations={after.LiveReservations}");
        Check(after.InvariantAnomalies == before.InvariantAnomalies,
            "SUBSTANCE_NO_INVARIANT_ANOMALY",
            $"invariants={before.InvariantAnomalies}->{after.InvariantAnomalies}");
    }

    private int CountWorldItem(string itemId) => items?.GetAllStacks()
        .Where(stack => stack != null
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
        .Sum(stack => stack.Quantity) ?? 0;

    private void NeutralizeNeeds()
    {
        foreach (CharacterCondition condition in actor.Stats.StatSnapshot.Keys.ToArray())
            actor.Stats.Stats[condition] = 100f;
    }

    private void PauseOtherAi()
    {
        foreach (CharacterActor candidate in LiveWorkers())
        {
            if (candidate == actor) continue;
            if (candidate.Brain != null)
            {
                pausedAi.Add(new MonoBehaviourState(candidate.Brain, candidate.Brain.enabled));
                candidate.Brain.enabled = false;
            }
            if (candidate.BehaviorTree != null)
            {
                pausedAi.Add(new MonoBehaviourState(
                    candidate.BehaviorTree,
                    candidate.BehaviorTree.enabled));
                candidate.BehaviorTree.enabled = false;
            }
        }
    }

    private void Cleanup()
    {
        actor?.GetComponent<AbilityUseSubstance>()?.StopUse("self-care-verifier-cleanup");
        if (brain != null)
        {
            brain.StopCurrentActionForReplan("self-care-verifier-cleanup");
            brain.availableActions = originalActions;
            brain.RequestImmediateReplan(clearFailures: true);
        }
        if (actor != null) actor.Stats.Stats = originalStats;
        foreach (MonoBehaviourState state in pausedAi)
            if (state.Component != null) state.Component.enabled = state.WasEnabled;
    }

    private void CaptureIssue(string condition, string stack, LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert
            || type == LogType.Warning)
        {
            consoleIssues.Add($"{type}:{condition}");
        }
    }

    private void Check(bool passed, string id, string detail)
    {
        checks.Add($"{id}\t{(passed ? "PASS" : "FAIL")}\t{detail}");
        if (!passed) failures.Add(id + ": " + detail);
    }

    private void WriteReport()
    {
        Check(consoleIssues.Count == 0, "CONSOLE_WARNING_ERROR_ZERO",
            consoleIssues.Count == 0 ? "0/0" : string.Join(" | ", consoleIssues));
        List<string> lines = new()
        {
            "CHARACTER_AI_SELF_CARE_PLAYMODE",
            $"checks={checks.Count}; failures={failures.Count}; consoleIssues={consoleIssues.Count}",
            "case\tresult\tdetail"
        };
        lines.AddRange(checks);
        lines.Add($"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}; failures={failures.Count}");
        if (failures.Count > 0)
        {
            lines.Add("FAILURES");
            lines.AddRange(failures);
        }
        File.WriteAllLines(CharacterAiSelfCarePlayModeVerifier.ReportPath, lines);
        if (failures.Count == 0) Debug.Log("CHARACTER_AI_SELF_CARE=PASS");
        else Debug.LogError("CHARACTER_AI_SELF_CARE=FAIL; " + string.Join(" | ", failures));
    }

    private static CharacterActor[] LiveWorkers() =>
        CharacterActorCollection.DistinctByGameObject(
            UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None))
        .Where(candidate => candidate != null
            && !candidate.IsDead
            && candidate.CurrentLifecycleState == CharacterLifecycleState.Active
            && CharacterWorkRoleUtility.TryGetWork(candidate, out _))
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
