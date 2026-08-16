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
/// Production Brain -> behavior tree -> ability fault matrix for physical
/// consumables. Editor authority is used only to seed and invalidate physical
/// fixture stacks after the live action has acquired its real quantity lease.
/// </summary>
[InitializeOnLoad]
public static class CharacterConsumableActionFaultPlayModeVerifier
{
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string PendingPath = "Temp/character-consumable-action-fault.flag";
    public const string ReportPath =
        "Artifacts/QA/character-consumable-action-fault-playmode.txt";
    private static bool runnerCreated;

    static CharacterConsumableActionFaultPlayModeVerifier()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Run Consumable Action Fault Matrix")]
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
    private static void Bootstrap()
    {
        TryStartPendingRunner();
    }

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
                CharacterConsumableActionFaultPlayModeRunner>() != null)
        {
            runnerCreated = true;
            return true;
        }
        if (runnerCreated) return false;
        CharacterConsumableActionFaultPlayModeRunner runner =
            new GameObject(nameof(CharacterConsumableActionFaultPlayModeRunner))
                .AddComponent<CharacterConsumableActionFaultPlayModeRunner>();
        runnerCreated = runner != null;
        return runnerCreated;
    }
}

public sealed class CharacterConsumableActionFaultPlayModeRunner : MonoBehaviour
{
    private const float SetupTimeout = 15f;
    private const float RowTimeout = 20f;
    private const string WaterId = "resource:clean-water";
    private const string MealId = "food:preserved-ration";
    private const string SubstanceItemId = "drug:vitality-tonic";
    private const string SubstanceId = "substance:vitality-tonic";

    private readonly List<string> rows = new();
    private readonly List<string> failures = new();
    private readonly List<string> consoleIssues = new();
    private readonly List<string> temporaryStackIds = new();
    private readonly List<MonoBehaviourState> pausedAi = new();
    private readonly Dictionary<CharacterCondition, float> originalStats = new();

    private DungeonRuntimeLifetimeScope scope;
    private CharacterActor actor;
    private AIBrain brain;
    private AIAction[] originalActions;
    private IWorldItemStackRuntime items;
    private WorldItemRepository repository;
    private IItemQuantityReservationService reservations;
    private ICharacterSubstanceRuntime substances;
    private ICharacterDeprivationQuery deprivationQuery;
    private ICharacterConsumablesPersistence consumables;
    private ICharacterMealOperationCancellation mealCancellation;
    private IFacilityCandidateCache facilities;
    private Grid grid;
    private float oldTimeScale;
    private int fixtureSequence;
    private bool previousRowSettled;
    private bool lifecycleCancelled;
    private bool startCompleted;
    private bool reportWritten;
    private Vector3 originalActorWorldPosition;
    private bool originalActorPositionCaptured;

    private void Awake()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnDisable()
    {
        if (!startCompleted)
            lifecycleCancelled = true;
    }

    private void OnDestroy()
    {
        if (!startCompleted)
            lifecycleCancelled = true;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        if (!startCompleted && previous == gameObject.scene)
            lifecycleCancelled = true;
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (!startCompleted && scene == gameObject.scene)
            lifecycleCancelled = true;
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        oldTimeScale = Time.timeScale;
        Time.timeScale = 6f;
        Application.logMessageReceived += CaptureIssue;
        try
        {
            yield return ResolveWorld();
            if (failures.Count == 0)
            {
                yield return VerifyDrinkFault(
                    "DRINK_SOURCE_LOSS",
                    ConsumableFaultKind.SourceLoss);
                yield return VerifyDrinkFault(
                    "DRINK_LEASE_INVALID",
                    ConsumableFaultKind.LeaseInvalidation);
                yield return VerifyEatFault(
                    "EAT_SOURCE_LOSS",
                    ConsumableFaultKind.SourceLoss);
                yield return VerifyEatFault(
                    "EAT_SPOIL",
                    ConsumableFaultKind.Spoil);
                yield return VerifyEatFault(
                    "EAT_LEASE_INVALID",
                    ConsumableFaultKind.LeaseInvalidation);
                yield return VerifySubstanceFault(
                    "SUBSTANCE_SOURCE_LOSS",
                    ConsumableFaultKind.SourceLoss);
                yield return VerifySubstanceFault(
                    "SUBSTANCE_LEASE_INVALID",
                    ConsumableFaultKind.LeaseInvalidation);
            }
        }
        finally
        {
            if (lifecycleCancelled && !startCompleted)
                Check(false,
                    "RUNNER_LIFECYCLE_ABORTED",
                    "scene or runner lifecycle ended before the matrix completed");
            Cleanup();
            Application.logMessageReceived -= CaptureIssue;
            Time.timeScale = oldTimeScale;
            if (!reportWritten)
            {
                WriteReport();
                reportWritten = true;
            }
            startCompleted = true;
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
        float deadline = Time.realtimeSinceStartup + SetupTimeout;
        bool prepared = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            if (scope?.Container != null
                && LiveWorkers().Length == 0
                && !prepared)
            {
                prepared = true;
                rows.Add("INFO\tSTART_PARTY\t"
                    + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            }
            actor = LiveWorkers().FirstOrDefault(candidate =>
                candidate.Brain?.availableActions?.Any(action =>
                    action?.actionset is AIDrink) == true
                && candidate.Brain.availableActions.Any(action =>
                    action?.actionset is AIEat)
                && candidate.Brain.availableActions.Any(action =>
                    action?.actionset is AISubstanceUse));
            if (scope?.Container != null && actor != null) break;
            yield return null;
        }

        Check(scope?.Container != null, "LIVE_SCOPE", scope?.name ?? "missing");
        Check(actor != null, "LIVE_CONSUMABLE_ACTOR", actor?.name ?? "missing");
        if (scope?.Container == null || actor == null) yield break;

        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        repository = scope.Container.Resolve<WorldItemRepository>();
        reservations = scope.Container.Resolve<IItemQuantityReservationService>();
        substances = scope.Container.Resolve<ICharacterSubstanceRuntime>();
        deprivationQuery = scope.Container.Resolve<ICharacterDeprivationQuery>();
        consumables = scope.Container.Resolve<ICharacterConsumablesPersistence>();
        mealCancellation = scope.Container.Resolve<ICharacterMealOperationCancellation>();
        facilities = scope.Container.Resolve<IFacilityCandidateCache>();
        IGridSystemProvider grids = scope.Container.Resolve<IGridSystemProvider>();
        Check(grids.TryGetGrid(out grid) && grid != null,
            "LIVE_GRID", grid != null ? $"{grid.width}x{grid.height}" : "missing");
        brain = actor.Brain;
        originalActorWorldPosition = actor.transform.position;
        originalActorPositionCaptured = true;
        originalActions = brain.availableActions;
        foreach (KeyValuePair<CharacterCondition, float> pair in actor.Stats.StatSnapshot)
            originalStats[pair.Key] = pair.Value;
        PauseOtherAi();
        actor.SetAiPaused(false);
        brain.enabled = true;
        if (actor.BehaviorTree != null) actor.BehaviorTree.enabled = true;
    }

    private IEnumerator VerifyDrinkFault(string prefix, ConsumableFaultKind fault)
    {
        yield return SettlePreviousRow(prefix);
        if (!HasLiveRuntimeContext()) yield break;
        if (!previousRowSettled) yield break;

        AIAction action = FindAction<AIDrink>();
        Check(action != null, prefix + "_AUTHORED", action?.actionset?.name ?? "missing");
        if (action == null) yield break;

        WorldItemStackSnapshot target = SeedLoose(WaterId, actor.GetNowXY(), 1);
        LeaseFixture foreign = SeedForeignLease(WaterId, FindFixtureCell(17));
        Check(target != null && foreign.IsValid, prefix + "_FIXTURE",
            $"target={target?.StackId};foreign={foreign.LeaseId}");
        if (target == null || !foreign.IsValid) yield break;

        PrepareAction(action, CharacterCondition.THIRST);
        CharacterAiRuntimeDiagnosticsSnapshot before = brain.CaptureRuntimeDiagnostics();
        brain.PreferActionOnNextDecision<AIDrink>(180f);
        brain.RequestImmediateReplan(clearFailures: true);

        ItemQuantityLease targetLease = null;
        long epoch = 0;
        yield return WaitUntil(() =>
        {
            KeepForeignLeaseAlive(foreign);
            if (brain.bestAction?.actionset is AIDrink)
                epoch = brain.RuntimeActionEpoch;
            targetLease = FindActorLease(ItemReservationPurpose.PersonalConsumption, WaterId);
            return epoch > 0 && targetLease != null;
        });
        if (!HasLiveRuntimeContext()) yield break;
        Check(epoch > 0 && targetLease != null, prefix + "_RUNNING_LEASE",
            $"epoch={epoch};lease={targetLease?.leaseId};action={brain.bestAction?.actionset}");
        if (targetLease == null)
        {
            CleanupRow(target, foreign);
            yield break;
        }
        Check(targetLease.slices.Any(slice => slice.stackId == target.StackId),
            prefix + "_FIXTURE_SELECTED",
            string.Join(",", targetLease.slices.Select(slice => slice.stackId)));

        InjectFault(fault, target.StackId, targetLease.leaseId);
        actor.Stats.Stats[CharacterCondition.THIRST] = 100f;
        yield return AssertFailedTerminal(prefix, before, epoch, targetLease, foreign);
        CleanupRow(target, foreign);
    }

    private IEnumerator VerifySubstanceFault(string prefix, ConsumableFaultKind fault)
    {
        yield return SettlePreviousRow(prefix);
        if (!HasLiveRuntimeContext()) yield break;
        if (!previousRowSettled) yield break;

        AIAction action = FindAction<AISubstanceUse>();
        Check(action != null, prefix + "_AUTHORED", action?.actionset?.name ?? "missing");
        if (action == null) yield break;

        CharacterSubstancePolicyState previous = substances.GetPolicy(actor, SubstanceId);
        WorldItemStackSnapshot target = SeedLoose(SubstanceItemId, actor.GetNowXY(), 1);
        LeaseFixture foreign = SeedForeignLease(SubstanceItemId, FindFixtureCell(23));
        AbilityUseSubstance ability = AbilityUseSubstance.Ensure(actor);
        Check(target != null && foreign.IsValid && ability != null,
            prefix + "_FIXTURE", $"target={target?.StackId};foreign={foreign.LeaseId}");
        if (target == null || !foreign.IsValid || ability == null) yield break;

        long epoch = 0;
        WorldItemReservedStackQuantity liveReservation = default;
        ability.DebugAfterQuantityLeaseReserved = (reserved, _) =>
        {
            liveReservation = reserved;
            epoch = brain.RuntimeActionEpoch;
            InjectFault(fault, reserved.StackId, reserved.LeaseId);
            substances.SetPolicy(
                actor,
                SubstanceId,
                SubstancePolicyMode.Forbidden);
        };
        substances.SetPolicy(
            actor,
            SubstanceId,
            SubstancePolicyMode.Scheduled,
            moodThreshold: 100f,
            scheduledHour: 0);
        PrepareAction(action, null);
        CharacterAiRuntimeDiagnosticsSnapshot before = brain.CaptureRuntimeDiagnostics();
        brain.PreferActionOnNextDecision<AISubstanceUse>(180f);
        brain.RequestImmediateReplan(clearFailures: true);
        yield return WaitUntil(() =>
        {
            KeepForeignLeaseAlive(foreign);
            return liveReservation.IsValid || epoch > 0;
        });
        if (!HasLiveRuntimeContext()) yield break;
        Check(liveReservation.IsValid && epoch > 0,
            prefix + "_RUNNING_LEASE",
            $"epoch={epoch};stack={liveReservation.StackId};lease={liveReservation.LeaseId}");
        if (liveReservation.IsValid)
        {
            ItemQuantityLease targetLease = new()
            {
                leaseId = liveReservation.LeaseId,
                ownerCharacterId = CharacterPersistentIdentity.Require(actor).Value,
                purpose = ItemReservationPurpose.PersonalConsumption,
                remainingQuantity = 1,
                slices = new List<ItemLeaseSlice>
                {
                    new() { stackId = liveReservation.StackId, quantity = 1 }
                }
            };
            yield return AssertFailedTerminal(prefix, before, epoch, targetLease, foreign);
        }
        ability.DebugAfterQuantityLeaseReserved = null;
        substances.SetPolicy(
            actor,
            SubstanceId,
            previous.mode,
            previous.moodThreshold,
            previous.scheduledHour);
        CleanupRow(target, foreign);
    }

    private IEnumerator VerifyEatFault(string prefix, ConsumableFaultKind fault)
    {
        yield return SettlePreviousRow(prefix);
        if (!HasLiveRuntimeContext()) yield break;
        if (!previousRowSettled) yield break;

        AIAction action = FindAction<AIEat>();
        Check(action != null, prefix + "_AUTHORED", action?.actionset?.name ?? "missing");
        if (action == null || grid == null) yield break;
        bool fixtureReady = TrySeedAvailableMealFacility(
            out Facility facility,
            out WorldItemStackSnapshot target,
            out string facilityDetail);
        Check(fixtureReady, prefix + "_FACILITY", facilityDetail);
        if (!fixtureReady) yield break;

        LeaseFixture foreign = SeedForeignLease(MealId, FindFixtureCell(31));
        Check(target != null
              && foreign.IsValid
              && target.StackId != foreign.StackId,
            prefix + "_FIXTURE",
            $"target={target?.StackId};targetSignature={target?.ReservationSignature};"
            + $"foreignStack={foreign.StackId};foreignSignature={foreign.Signature};"
            + $"foreignLease={foreign.LeaseId}");
        if (target == null || !foreign.IsValid) yield break;

        PrepareAction(action, CharacterCondition.HUNGER);
        CharacterAiRuntimeDiagnosticsSnapshot before = brain.CaptureRuntimeDiagnostics();
        int immediateDecisionRequestsBefore = brain.ImmediateDecisionRequestCount;
        long committedPathDeferralsBefore =
            brain.RuntimeCommittedPathSearchDeferralCount;
        // Use the production scheduler wake-up as one atomic operation. A
        // verifier-side direct BT loop runs after that frame's shared path
        // budget and can keep a legitimate Deferred candidate starved forever.
        // The scheduler owns BeginFrame/budget rollover and retains a deferred
        // preferred action for its next legal slice.
        brain.RequestImmediateReplanForAction<AIEat>(clearFailures: true);
        bool preferredAtRequest =
            brain.IsActionPreferredForNextDecision<AIEat>();
        CharacterMealPlanSaveData plan = null;
        ItemQuantityLease targetLease = null;
        long epoch = 0;
        int schedulerObservationFrames = 0;
        yield return WaitUntil(() =>
        {
            KeepForeignLeaseAlive(foreign);
            schedulerObservationFrames++;
            if (brain.bestAction?.actionset is AIEat)
                epoch = brain.RuntimeActionEpoch;
            plan = consumables.Capture().activeMealPlans.FirstOrDefault(candidate =>
                candidate != null
                && candidate.characterId == CharacterPersistentIdentity.Require(actor).Value);
            if (plan != null
                && reservations.TryGetLeasesByOwner(plan.planId, out IReadOnlyList<ItemQuantityLease> leases))
                targetLease = leases.FirstOrDefault();
            return epoch > 0 && plan != null && targetLease != null;
        });
        if (!HasLiveRuntimeContext()) yield break;
        Check(epoch > 0 && plan != null && targetLease != null,
            prefix + "_RUNNING_MEAL_PLAN",
            BuildEatStartDiagnostic(
                before,
                immediateDecisionRequestsBefore,
                committedPathDeferralsBefore,
                preferredAtRequest,
                schedulerObservationFrames,
                epoch,
                plan,
                targetLease,
                action,
                facility));
        if (plan == null || targetLease == null)
        {
            CleanupRow(target, foreign);
            yield break;
        }
        Check(plan.sourceStackId == target.StackId, prefix + "_FIXTURE_SELECTED",
            $"expected={target.StackId};actual={plan.sourceStackId}");
        if (plan.sourceStackId != target.StackId)
        {
            mealCancellation.CancelActiveMealOperations(actor, "consumable-fault-fixture-mismatch");
            CleanupRow(target, foreign);
            yield break;
        }

        Check(TryDescribeLeaseIsolation(
                target,
                targetLease,
                foreign,
                out string isolationDetail),
            prefix + "_PREFAULT_LEASE_ISOLATION",
            isolationDetail);

        InjectFault(fault, target.StackId, targetLease.leaseId);
        actor.Stats.Stats[CharacterCondition.HUNGER] = 100f;
        yield return AssertFailedTerminal(prefix, before, epoch, targetLease, foreign);
        Check(consumables.Capture().activeMealPlans.All(candidate =>
                candidate == null
                || candidate.characterId != CharacterPersistentIdentity.Require(actor).Value)
              && facility.CurrentUserCount == 0
              && facility.ActiveVisitReservationCount == 0,
            prefix + "_MEAL_SEAT_PLAN_CLEAN",
            $"plans={consumables.Capture().activeMealPlans.Count};users={facility.CurrentUserCount};"
            + $"reservations={facility.ActiveVisitReservationCount}");
        CleanupRow(target, foreign);
    }

    private string BuildEatStartDiagnostic(
        CharacterAiRuntimeDiagnosticsSnapshot before,
        int immediateDecisionRequestsBefore,
        long committedPathDeferralsBefore,
        bool preferredAtRequest,
        int schedulerObservationFrames,
        long epoch,
        CharacterMealPlanSaveData plan,
        ItemQuantityLease targetLease,
        AIAction action,
        Facility facility)
    {
        CharacterAiRuntimeDiagnosticsSnapshot after =
            brain.CaptureRuntimeDiagnostics();
        CharacterAiRuntimeGateSnapshot beforeGate = before.Gate;
        CharacterAiRuntimeGateSnapshot afterGate = after.Gate;
        IGridPathSearchBroker broker = actor.PathSearchBroker;
        return $"epoch={epoch};plan={plan?.planId};source={plan?.sourceStackId};"
            + $"lease={targetLease?.leaseId};schedulerFrames={schedulerObservationFrames};"
            + $"wakeRequests={immediateDecisionRequestsBefore}->{brain.ImmediateDecisionRequestCount};"
            + $"schedulerProcesses={beforeGate.SchedulerProcesses}->{afterGate.SchedulerProcesses};"
            + $"retrySchedules={beforeGate.RetrySchedules}->{afterGate.RetrySchedules};"
            + $"retryAttempts={beforeGate.RetryAttempts}->{afterGate.RetryAttempts};"
            + $"pathRequests={beforeGate.PathRequests}->{afterGate.PathRequests};"
            + $"pathResults={beforeGate.PathResults}->{afterGate.PathResults};"
            + $"committedPathDeferrals={committedPathDeferralsBefore}"
            + $"->{brain.RuntimeCommittedPathSearchDeferralCount};"
            + $"preferredAtRequest={preferredAtRequest};"
            + $"preferredNow={brain.IsActionPreferredForNextDecision<AIEat>()};"
            + $"scoringPending={brain.IsActionScoringPending};"
            + $"pathDeferred={brain.IsPathSearchDeferred};"
            + $"committedPathDeferred={brain.IsCommittedPathSearchDeferred};"
            + $"brokerSearches={broker?.SearchesThisFrame ?? -1};"
            + $"brokerDeferrals={broker?.BudgetDeferralsThisFrame ?? -1};"
            + $"best={brain.bestAction?.actionset};phase={brain.CurrentActionPhase};"
            + $"phaseDetail={brain.CurrentActionPhaseDetail};failure={brain.LastActionFailure};"
            + $"hunger={actor.Stats.Stats[CharacterCondition.HUNGER]:0.##};"
            + $"utility={CharacterNeedAiThresholds.GetRoutineUtility(actor, CharacterCondition.HUNGER):0.###};"
            + $"canStart={action.actionset.CanStart(actor)};"
            + $"facilityUsers={facility.CurrentUserCount};"
            + $"facilityReservations={facility.ActiveVisitReservationCount}";
    }

    private IEnumerator AssertFailedTerminal(
        string prefix,
        CharacterAiRuntimeDiagnosticsSnapshot before,
        long epoch,
        ItemQuantityLease targetLease,
        LeaseFixture foreign)
    {
        CharacterAiActionTerminalKind terminal = CharacterAiActionTerminalKind.None;
        yield return WaitUntil(() =>
        {
            KeepForeignLeaseAlive(foreign);
            return brain.CaptureRuntimeDiagnostics()
                .TryGetActionTerminal(epoch, out terminal);
        });
        if (!HasLiveRuntimeContext()) yield break;
        CharacterAiRuntimeDiagnosticsSnapshot after = brain.CaptureRuntimeDiagnostics();
        Check(terminal == CharacterAiActionTerminalKind.Failed,
            prefix + "_TYPED_FAILED", $"epoch={epoch};terminal={terminal};failure={brain.LastActionFailure}");
        Check(after.ExecutionFailures == before.ExecutionFailures + 1,
            prefix + "_FAILURE_ONCE",
            $"failures={before.ExecutionFailures}->{after.ExecutionFailures}");
        Check(after.ImmediateReplans >= before.ImmediateReplans + 1,
            prefix + "_IMMEDIATE_REPLAN",
            $"replans={before.ImmediateReplans}->{after.ImmediateReplans}");
        Check(!reservations.Revalidate(targetLease.leaseId, out _, out _),
            prefix + "_TARGET_LEASE_CLEAN", targetLease.leaseId);
        bool foreignLeaseLive = reservations.Revalidate(
            foreign.LeaseId,
            out ItemQuantityLease kept,
            out DomainFailure foreignFailure);
        WorldItemStackSnapshot foreignStack = items.GetAllStacks()
            .FirstOrDefault(stack => stack?.StackId == foreign.StackId);
        bool physicallyPreserved = foreignLeaseLive
            && kept.remainingQuantity == 1
            && kept.slices?.Count == 1
            && kept.slices[0].stackId == foreign.StackId
            && targetLease.slices.All(slice => slice.stackId != foreign.StackId)
            && foreignStack != null
            && foreignStack.TotalQuantity == 1
            && foreignStack.ReservedQuantity == 1
            && foreignStack.AvailableQuantity == 0;
        Check(physicallyPreserved,
            prefix + "_FOREIGN_LEASE_PRESERVED",
            $"lease={foreign.LeaseId};stack={foreign.StackId};"
            + $"remaining={kept?.remainingQuantity};"
            + $"signature={foreign.Signature};"
            + $"slices={DescribeSlices(kept)};failure={foreignFailure.Code};"
            + $"total={foreignStack?.TotalQuantity};"
            + $"reserved={foreignStack?.ReservedQuantity};"
            + $"available={foreignStack?.AvailableQuantity}");
        Check(after.Gate.LivePathRequests == 0
              && after.Gate.LiveReservations == 0,
            prefix + "_AI_OWNERSHIP_CLEAN",
            $"paths={after.Gate.LivePathRequests};reservations={after.Gate.LiveReservations}");
    }

    private void InjectFault(
        ConsumableFaultKind fault,
        string stackId,
        string leaseId)
    {
        switch (fault)
        {
            case ConsumableFaultKind.SourceLoss:
                items.DeleteStack(stackId);
                break;
            case ConsumableFaultKind.LeaseInvalidation:
                reservations.Release(
                    leaseId,
                    ItemReservationReleaseReason.StackInvalidated);
                break;
            case ConsumableFaultKind.Spoil:
                items.TrySetInstanceComponent(
                    stackId,
                    new ItemInstanceComponentSaveData
                    {
                        componentTypeId = ItemInstanceComponentIds.Freshness,
                        schemaVersion = 2,
                        affectsStacking = true,
                        values = new List<ItemStateValueSaveData>
                        {
                            new()
                            {
                                key = "remaining-seconds",
                                kind = ItemStateValueKind.Decimal,
                                decimalValue = 0d
                            },
                            new()
                            {
                                key = "preserved",
                                kind = ItemStateValueKind.Boolean,
                                booleanValue = false
                            }
                        }
                    });
                break;
        }
    }

    private void PrepareAction(AIAction action, CharacterCondition? need)
    {
        brain.StopCurrentActionForReplan("consumable-fault-row-reset");
        actor.GetComponent<AbilityUseSubstance>()?.StopUse("consumable-fault-row-reset");
        mealCancellation.CancelActiveMealOperations(actor, "consumable-fault-row-reset");
        foreach (CharacterCondition condition in actor.Stats.StatSnapshot.Keys.ToArray())
            actor.Stats.Stats[condition] = 100f;
        if (need.HasValue && actor.Stats.Stats.ContainsKey(need.Value))
            actor.Stats.Stats[need.Value] =
                actor.Stats.GetNeedResponse(need.Value).routineStart;
        brain.availableActions = new[] { action };
    }

    private IEnumerator SettlePreviousRow(string prefix)
    {
        previousRowSettled = false;
        if (!HasLiveRuntimeContext()) yield break;
        brain.availableActions = Array.Empty<AIAction>();
        brain.StopCurrentActionForReplan("consumable-fault-row-settle");
        actor.GetComponent<AbilityShopping>()?
            .StopShopping("consumable-fault-row-settle");
        actor.GetComponent<AbilityUseSubstance>()?
            .StopUse("consumable-fault-row-settle");
        mealCancellation.CancelActiveMealOperations(
            actor,
            "consumable-fault-row-settle");
        foreach (CharacterCondition condition in actor.Stats.StatSnapshot.Keys.ToArray())
            actor.Stats.Stats[condition] = 100f;
        brain.RequestImmediateReplan(clearFailures: true);

        yield return WaitUntil(() =>
        {
            AbilityShopping shopping = actor.GetComponent<AbilityShopping>();
            AbilityUseSubstance substance = actor.GetComponent<AbilityUseSubstance>();
            CharacterAiRuntimeDiagnosticsSnapshot diagnostics =
                brain.CaptureRuntimeDiagnostics();
            bool hasMealPlan = consumables.Capture().activeMealPlans.Any(candidate =>
                candidate != null
                && candidate.characterId
                    == CharacterPersistentIdentity.Require(actor).Value);
            previousRowSettled = brain.bestAction == null
                && !brain.IsExternallyDrivenActionActive
                && shopping?.HasActiveShoppingRoutineForDiagnostics != true
                && substance?.IsUsingSubstance != true
                && deprivationQuery?.IsRoutineDrinkActionActive(actor) != true
                && !hasMealPlan
                && diagnostics.Gate.LivePathRequests == 0
                && diagnostics.Gate.LiveReservations == 0;
            return previousRowSettled;
        });
        if (!HasLiveRuntimeContext()) yield break;
        Check(previousRowSettled,
            prefix + "_ROW_SETTLED",
            previousRowSettled
                ? "brain/action/meal/lease ownership idle"
                : $"best={brain.bestAction?.actionset};"
                  + $"external={brain.IsExternallyDrivenActionActive};"
                  + $"paths={brain.CaptureRuntimeDiagnostics().Gate.LivePathRequests};"
                  + $"reservations={brain.CaptureRuntimeDiagnostics().Gate.LiveReservations}");
        if (previousRowSettled)
            yield return null;
    }

    private bool TrySeedAvailableMealFacility(
        out Facility facility,
        out WorldItemStackSnapshot target,
        out string detail)
    {
        facility = null;
        target = null;
        detail = "no authored meal facility accepted a live visitor";
        facilities.AdvanceIndex(1.0);
        Vector2Int origin = actor.GetNowXY();
        Facility[] candidates = facilities.GetCandidates(grid, FacilityRole.Meal)
            .OfType<Facility>()
            .Where(candidate => candidate != null && !candidate.isDestroy)
            .OrderBy(candidate => Mathf.Abs(candidate.centerPos.x - origin.x)
                                  + Mathf.Abs(candidate.centerPos.y - origin.y))
            .ThenBy(candidate => candidate.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal)
            .ToArray();
        foreach (Facility candidate in candidates)
        {
            string destinationId = "facility-input:meal:"
                + candidate.RequirePersistentInstanceId().Value;
            WorldItemStackSnapshot seeded = SeedBuffer(
                MealId,
                candidate.centerPos,
                destinationId);
            facilities.AdvanceIndex(0.1);
            string visitFailure = string.Empty;
            if (seeded != null
                && candidate.CanVisit(actor.BuildingVisitor, out visitFailure)
                && TryStageActorNearMealFacility(
                    candidate,
                    origin,
                    out string stagingDetail))
            {
                facility = candidate;
                target = seeded;
                detail = $"{candidate.name};distance="
                    + (Mathf.Abs(candidate.centerPos.x - origin.x)
                       + Mathf.Abs(candidate.centerPos.y - origin.y))
                    + $";{stagingDetail}";
                return true;
            }

            detail = seeded == null
                ? $"{candidate.name}:seed-failed"
                : !candidate.CanVisit(actor.BuildingVisitor, out visitFailure)
                    ? $"{candidate.name}:visit-rejected:{visitFailure}"
                    : $"{candidate.name}:reachable-staging-missing";
            if (seeded != null)
            {
                WorldItemRepositoryEditorAccess.TryRemoveStack(
                    repository,
                    seeded.StackId);
                temporaryStackIds.Remove(seeded.StackId);
            }
        }
        return false;
    }

    private bool TryStageActorNearMealFacility(
        Facility facility,
        Vector2Int origin,
        out string detail)
    {
        detail = "path-search-unavailable";
        if (facility == null || actor?.PathSearchBroker == null || grid == null)
            return false;

        brain.ClearPathSearchCache();
        GridPathSearchResult search = brain.GetPathSearch(actor);
        if (search == null || !search.ContainsVisitableOccupant(facility))
        {
            detail = "facility-not-production-reachable";
            return false;
        }

        GridMoveStep[] steps = search.GetMovePathTo(facility).ToArray();
        int originalDistance = search.GetMoveDistanceTo(facility);
        if (steps.Length == 0)
        {
            bool alreadyAtFacility = originalDistance == 0
                || Mathf.Abs(facility.centerPos.x - origin.x)
                   + Mathf.Abs(facility.centerPos.y - origin.y) <= 1;
            detail = alreadyAtFacility
                ? $"staged={origin};route=0"
                : $"empty-route;distance={originalDistance}";
            return alreadyAtFacility;
        }

        // Leave the final production path step for AbilityMove. This keeps
        // the row on the real movement/visit pipeline while bounding setup
        // time independently of the authored map's current layout.
        Vector2Int staging = steps.Length == 1
            ? origin
            : steps[steps.Length - 2].To;
        if (!grid.IsValidGridPos(staging)
            || !grid.IsWalkable(staging)
            || !search.ContainsPosition(staging))
        {
            detail = $"invalid-staging={staging};route={steps.Length}";
            return false;
        }

        actor.transform.position = grid.GetWorldPos(staging);
        brain.ClearPathSearchCache();
        detail = $"staged={staging};route={steps.Length};remaining<=1";
        return true;
    }

    private bool KeepForeignLeaseAlive(LeaseFixture foreign)
    {
        if (!foreign.IsValid
            || !reservations.Revalidate(
                foreign.LeaseId,
                out ItemQuantityLease lease,
                out _))
        {
            return false;
        }
        return reservations.Renew(
            foreign.LeaseId,
            lease.maximumExpiresAtGameSeconds + 45d,
            out _);
    }

    private AIAction FindAction<T>() where T : AIActionSet =>
        originalActions.FirstOrDefault(action => action?.actionset is T);

    private ItemQuantityLease FindActorLease(
        ItemReservationPurpose purpose,
        string itemId)
    {
        string actorId = CharacterPersistentIdentity.Require(actor).Value;
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks())
        {
            if (stack == null || stack.ItemId != itemId) continue;
            ItemQuantityLease lease = reservations
                .GetLeasesForStack(new ItemStackId(stack.StackId))
                .FirstOrDefault(candidate => candidate != null
                    && candidate.ownerCharacterId == actorId
                    && candidate.purpose == purpose);
            if (lease != null) return lease;
        }
        return null;
    }

    private WorldItemStackSnapshot SeedLoose(
        string itemId,
        Vector2Int position,
        int quantity)
    {
        string id = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            quantity,
            WorldItemStackState.Loose,
            position: position);
        temporaryStackIds.Add(id);
        return items.GetAllStacks().FirstOrDefault(stack => stack?.StackId == id);
    }

    private WorldItemStackSnapshot SeedBuffer(
        string itemId,
        Vector2Int position,
        string destinationId)
    {
        string id = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            1,
            WorldItemStackState.FacilityBuffer,
            destinationId,
            position: position);
        temporaryStackIds.Add(id);
        return items.GetAllStacks().FirstOrDefault(stack => stack?.StackId == id);
    }

    private LeaseFixture SeedForeignLease(string itemId, Vector2Int position)
    {
        WorldItemStackSnapshot stack = SeedLoose(itemId, position, 1);
        if (stack == null) return default;
        string operationId = $"qa:consumable-foreign:{++fixtureSequence:D4}";
        bool isolated = items.TrySetInstanceComponent(
            stack.StackId,
            new ItemInstanceComponentSaveData
            {
                componentTypeId = ItemInstanceComponentIds.Provenance,
                schemaVersion = 1,
                affectsStacking = true,
                values = new List<ItemStateValueSaveData>
                {
                    new()
                    {
                        key = "source-character-id",
                        kind = ItemStateValueKind.String,
                        stringValue = operationId
                    },
                    new()
                    {
                        key = "species",
                        kind = ItemStateValueKind.String,
                        stringValue = "qa:foreign-lease-isolation"
                    }
                }
            });
        stack = items.GetAllStacks().FirstOrDefault(candidate =>
            candidate?.StackId == stack.StackId);
        if (!isolated || stack == null) return default;
        if (!reservations.TryReserve(
                operationId,
                "character:qa:foreign-consumer",
                ItemReservationPurpose.PersonalConsumption,
                operationId,
                new ItemQuantityReservationRequest(
                    new ItemStackId(stack.StackId),
                    1,
                    stack.ReservationSignature),
                out ItemQuantityLease lease,
                out _))
            return default;
        return new LeaseFixture(
            stack.StackId,
            lease.leaseId,
            stack.ReservationSignature);
    }

    private bool TryDescribeLeaseIsolation(
        WorldItemStackSnapshot target,
        ItemQuantityLease targetLease,
        LeaseFixture foreign,
        out string detail)
    {
        ItemQuantityLease currentTarget = null;
        DomainFailure targetFailure = DomainFailure.None;
        bool targetLive = targetLease != null
            && reservations.Revalidate(
                targetLease.leaseId,
                out currentTarget,
                out targetFailure);
        ItemQuantityLease currentForeign = null;
        DomainFailure foreignFailure = DomainFailure.None;
        bool foreignLive = foreign.IsValid
            && reservations.Revalidate(
                foreign.LeaseId,
                out currentForeign,
                out foreignFailure);
        WorldItemStackSnapshot targetStack = items.GetAllStacks()
            .FirstOrDefault(stack => stack?.StackId == target?.StackId);
        WorldItemStackSnapshot foreignStack = items.GetAllStacks()
            .FirstOrDefault(stack => stack?.StackId == foreign.StackId);
        bool disjoint = targetLive
            && foreignLive
            && targetStack != null
            && foreignStack != null
            && !string.Equals(
                targetStack.StackId,
                foreignStack.StackId,
                StringComparison.Ordinal)
            && !string.Equals(
                targetStack.ReservationSignature,
                foreignStack.ReservationSignature,
                StringComparison.Ordinal)
            && currentTarget.slices.All(targetSlice =>
                currentForeign.slices.All(foreignSlice =>
                    !string.Equals(
                        targetSlice.stackId,
                        foreignSlice.stackId,
                        StringComparison.Ordinal)));
        detail = $"isolated={disjoint};targetLive={targetLive};"
            + $"targetStack={targetStack?.StackId};targetState={targetStack?.State};"
            + $"targetDestination={targetStack?.DestinationId};"
            + $"targetSignature={targetStack?.ReservationSignature};"
            + $"targetSlices={DescribeSlices(currentTarget)};targetFailure={targetFailure.Code};"
            + $"foreignLive={foreignLive};foreignStack={foreignStack?.StackId};"
            + $"foreignState={foreignStack?.State};foreignDestination={foreignStack?.DestinationId};"
            + $"foreignSignature={foreignStack?.ReservationSignature};"
            + $"foreignSlices={DescribeSlices(currentForeign)};foreignFailure={foreignFailure.Code}";
        return disjoint;
    }

    private static string DescribeSlices(ItemQuantityLease lease) =>
        lease?.slices == null
            ? "none"
            : string.Join(",", lease.slices.Select(slice =>
                slice == null
                    ? "null"
                    : $"{slice.stackId}:{slice.quantity}:{slice.expectedStackSignature}"));

    private Vector2Int FindFixtureCell(int offset)
    {
        if (grid == null) return actor.GetNowXY();
        Vector2Int origin = actor.GetNowXY();
        for (int step = 0; step < grid.width; step++)
        {
            int x = (origin.x + offset + step) % grid.width;
            Vector2Int candidate = new(x, origin.y);
            if (grid.IsValidGridPos(candidate) && grid.IsWalkable(candidate))
                return candidate;
        }
        return origin;
    }

    private IEnumerator WaitUntil(Func<bool> predicate)
    {
        float deadline = Time.realtimeSinceStartup + RowTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (!HasLiveRuntimeContext()) yield break;
            if (predicate()) yield break;
            yield return null;
        }
    }

    private bool HasLiveRuntimeContext() =>
        !lifecycleCancelled
        && this != null
        && gameObject != null
        && scope != null
        && actor != null
        && brain != null;

    private void CleanupRow(WorldItemStackSnapshot target, LeaseFixture foreign)
    {
        if (foreign.IsValid)
            reservations.Release(foreign.LeaseId, ItemReservationReleaseReason.Cancelled);
        if (target != null)
            WorldItemRepositoryEditorAccess.TryRemoveStack(repository, target.StackId);
        if (!string.IsNullOrWhiteSpace(foreign.StackId))
            WorldItemRepositoryEditorAccess.TryRemoveStack(repository, foreign.StackId);
        temporaryStackIds.Remove(target?.StackId ?? string.Empty);
        temporaryStackIds.Remove(foreign.StackId ?? string.Empty);
        brain.StopCurrentActionForReplan("consumable-fault-row-cleanup");
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
        if (lifecycleCancelled)
        {
            temporaryStackIds.Clear();
            pausedAi.Clear();
            return;
        }
        if (actor != null)
        {
            actor.GetComponent<AbilityUseSubstance>()?.StopUse("consumable-fault-cleanup");
            mealCancellation?.CancelActiveMealOperations(actor, "consumable-fault-cleanup");
            brain?.StopCurrentActionForReplan("consumable-fault-cleanup");
            if (brain != null) brain.availableActions = originalActions;
            actor.Stats.Stats = originalStats;
            if (originalActorPositionCaptured)
            {
                actor.transform.position = originalActorWorldPosition;
                brain?.ClearPathSearchCache();
            }
        }
        if (repository != null)
        {
            foreach (string stackId in temporaryStackIds.Distinct().ToArray())
                WorldItemRepositoryEditorAccess.TryRemoveStack(repository, stackId);
        }
        foreach (MonoBehaviourState state in pausedAi)
            if (state.Component != null) state.Component.enabled = state.WasEnabled;
    }

    private void CaptureIssue(string condition, string stack, LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert
            || type == LogType.Warning)
            consoleIssues.Add(type + ":" + condition);
    }

    private void Check(bool passed, string id, string detail)
    {
        rows.Add($"{(passed ? "PASS" : "FAIL")}\t{id}\t{detail}");
        if (!passed) failures.Add(id + ": " + detail);
    }

    private void WriteReport()
    {
        Check(consoleIssues.Count == 0, "CONSOLE_WARNING_ERROR_ZERO",
            consoleIssues.Count == 0 ? "0/0" : string.Join(" | ", consoleIssues));
        List<string> report = new()
        {
            "# Character consumable action production-live fault matrix",
            "authority=Brain/BT -> AIDrink/AIEat/AISubstanceUse -> production ability/runtime",
            "RESULT=" + (failures.Count == 0 ? "PASS" : "FAIL")
                + $"; failures={failures.Count}"
        };
        report.AddRange(rows);
        if (failures.Count > 0)
            report.AddRange(failures.Select(value => "FAILURE\t" + value));
        File.WriteAllLines(CharacterConsumableActionFaultPlayModeVerifier.ReportPath, report);
        Debug.Log(failures.Count == 0
            ? "CHARACTER_CONSUMABLE_ACTION_FAULT=PASS"
            : "CHARACTER_CONSUMABLE_ACTION_FAULT=FAIL; " + string.Join(" | ", failures));
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

    private enum ConsumableFaultKind
    {
        SourceLoss,
        Spoil,
        LeaseInvalidation
    }

    private readonly struct LeaseFixture
    {
        public LeaseFixture(string stackId, string leaseId, string signature)
        {
            StackId = stackId ?? string.Empty;
            LeaseId = leaseId ?? string.Empty;
            Signature = signature ?? string.Empty;
        }
        public string StackId { get; }
        public string LeaseId { get; }
        public string Signature { get; }
        public bool IsValid => StackId.Length > 0
            && LeaseId.Length > 0
            && Signature.Length > 0;
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
