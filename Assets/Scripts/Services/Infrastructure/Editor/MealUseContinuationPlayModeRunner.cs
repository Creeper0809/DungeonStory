#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>Real AIEat/AIDrink consumption and official whole-world checkpoints.</summary>
public sealed class MealUseContinuationPlayModeRunner
{
    [Serializable] private sealed class Report
    {
        public string scope = "GameplayScene; authored dining facility; controlled hunger/thirst and food/water fixtures; public off-duty/AIEat/AIDrink request; real movement, consumption and service. Whole-world meal/failure restore/replay, malformed meal-owner rejection, and drinking lease retirement/replan/completed replay. Last-publication faults use all real sections/participants and verify original meal/drink continuation; meal rollback uses an earlier actual clock epoch. Non-owner AIEat postphysical departure/death uses controlled effect/parent/ACK rejection and official whole-world restore before parent, after parent and after ACK. Independent QA rewind branches are not one natural history. Not natural production, guest payment, cold-process restart or training evidence.";
        public bool passed, pausePreserved, completedOnce, replayExact, ordinaryDisableCancelled;
        public bool failedMealRestored, failedMealTerminatedOnce, failedMealReplayExact;
        public bool failedMealReservationReleased, failedMealReservationReplayExact;
        public bool mealLatePublishRollbackAtomic, mealPostRollbackStable, mealClockProgressExact;
        public bool mealRolledBackActionCompletedOnce;
        public float mealRollbackCandidateTimeline, mealRollbackLiveTimeline;
        public List<MealRestoreRejection> mealRejectedRestores = new();
        public bool drinkLeaseRetired, drinkRejectedRestoreAtomic, drinkConsumedOnce, drinkReplayExact;
        public bool drinkCheckpointResume, coldDrinkStateExact;
        public bool drinkLatePublishRollbackAtomic, drinkRolledBackActionCompletedOnce;
        public bool drinkPostRollbackStable;
        public string drinkOperationId, drinkSourceStackId;
        public int waterBefore, waterAfter;
        public float drinkThirstBefore;
        public bool networkSent, humanApprovalClaimed, trainingEligible, fullAuditClosure;
        public bool checkpointResume;
        public string sourceCheckpointPath, sourceCheckpointSha256;
        public float rawClockBeforeRestore, savedTimeline, remainingMealSeconds, consumedAfterGameSeconds;
        public string actorId, facilityId, operationId, mealOperationId, serviceSessionId, failure;
        public int foodBefore, foodAfter, usesBefore, usesAfter, rejectedAcks;
        public float hungerBefore, hungerAfter, elapsedSeconds;
        public List<Checkpoint> checkpoints = new();
        public List<GameplayOutcomeSnapshot> results = new();
        public List<GameplayOutcomeSnapshot> drinkResults = new();
        public List<string> freshEatingTrace = new();
        public List<PostphysicalLifecycleWitness> postphysicalLifecycle = new();
        public int expectedCon03EffectRejections, expectedCon03ParentRejections, con03RetiredObserverNotices;
        public List<string> warnings = new(), errors = new();
    }
    [Serializable] private sealed class PostphysicalLifecycleWitness
    {
        public string branch, actorId, facilityId, mealOperationId, facilityOperationId, serviceSessionId;
        public string physicalCommitId, sourceStackId, resultKey, mealStatus;
        public int sections, quantity, pendingAfter, activeAfter, completedUsesBefore, completedUsesAfter;
        public long massGrams;
        public float hungerAtRetirement;
        public bool passed, actorAbsent, facilityUseRetired, sessionRetired, preParentRestored, publishedRestored, terminalReplayExact;
        public List<GameplayOutcomeSnapshot> mealResults = new(), facilityResults = new();
    }
    [Serializable] private sealed class Checkpoint
    {
        public string stage;
        public int sections;
        public bool freshActor, freshFacility, executionExact, consumptionExact, stockExact, sessionExact;
    }
    [Serializable] private sealed class OutcomeSet { public List<GameplayOutcomeSnapshot> items; }
    [Serializable] private sealed class MealRestoreRejection
    {
        public string stage, caseId;
        public int sections;
        public bool rejectedForOwner, stateExact, referencesExact, rootExact, passed;
        public string[] errors;
    }
    private sealed class LateRestoreFault : IDungeonRestoreTransactionParticipant
    {
        private readonly string domain;
        public LateRestoreFault(string domain) => this.domain = domain;
        public string ParticipantId => "zzzz.qa." + domain + ".late-publish";
        public string FailureCode => "qa-" + domain + "-late-publication-fault";
        public bool PublishAttempted, RolledBack;
        public void BeginRestoreCandidate() { }
        public void PublishRestoreCandidate()
        {
            PublishAttempted = true;
            throw new InvalidOperationException(FailureCode);
        }
        public void RollbackPublishedRestoreCandidate() => RolledBack = true;
        public void DiscardRestoreCandidate() { }
    }
    [Serializable] private sealed class ConsumptionObservation
    {
        public bool actorCanRunAi, resultFound;
        public string resultCode;
        public string[] parameters;
        public FacilityUseExecutionState execution;
        public DungeonCharacterConsumablesSaveData consumables;
    }

    private const string FoodId = "food:preserved-ration";
    private readonly Report report = new();
    private DungeonRuntimeLifetimeScope scope;
    private ICharacterAiWorldRegistry world;
    private IDungeonGameSaveService saves;
    private ICharacterConsumablesPersistence consumables;
    private IWorldItemStackRuntime items;
    private IServiceSessionRuntime sessions;
    private IGameTimeScaleController speed;
    private PhysicalItemBatchDispositionService batch;
    private CharacterSpawner spawner;
    private bool spawnerWasPaused;
    private float originalScale;
    private string output;
    private string eatingWorld;
    private CharacterConsumablesApplicationPorts con03Ports;
    private CharacterConsumablesRuntime con03Outcomes;
    private bool con03FaultsActive;

    public MealUseContinuationPlayModeRunner(IEnumerable<string> warnings, IEnumerable<string> errors)
    {
        report.warnings.AddRange(warnings);
        report.errors.AddRange(errors);
    }

    public IEnumerator Run()
    {
        File.Delete(DungeonFullWorldRoundTripPlayModeFacade.RequestPath);
        output = Path.GetDirectoryName(DungeonPersistencePaths.Root);
        Application.logMessageReceived += CaptureLog;
        float started = Time.realtimeSinceStartup;
        var stack = new Stack<IEnumerator>();
        stack.Push(Verify());
        while (stack.Count > 0)
        {
            bool moved;
            object next = null;
            try
            {
                Require(Time.realtimeSinceStartup - started < 240f, "Meal witness exceeded 240 real seconds.");
                Require(report.errors.Count == 0, "Runtime error: " + report.errors.FirstOrDefault());
                moved = stack.Peek().MoveNext();
                if (moved) next = stack.Peek().Current;
            }
            catch (Exception exception) { report.failure = exception.ToString(); break; }
            if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator child) stack.Push(child);
            else yield return next;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        if (batch != null) batch.DebugRejectAcknowledgement = null;
        if (con03Ports != null) con03Ports.DebugRejectMealEffects = null;
        if (con03Outcomes != null) con03Outcomes.DebugRejectMealFactPublication = null;
        con03FaultsActive = false;
        if (!string.IsNullOrEmpty(report.failure) && saves != null)
        {
            try { WriteNew("failure-world.json", saves.ToJson(saves.Capture())); }
            catch (Exception exception) { report.failure += "\nFailure checkpoint unavailable: " + exception.Message; }
        }
        report.elapsedSeconds = Time.realtimeSinceStartup - started;
        bool mealVerified = report.pausePreserved && report.completedOnce && report.replayExact
            && report.ordinaryDisableCancelled
            && report.mealRejectedRestores.Count == 21 && report.mealRejectedRestores.All(value => value.passed)
            && report.mealLatePublishRollbackAtomic && report.mealPostRollbackStable
            && report.mealClockProgressExact && report.mealRolledBackActionCompletedOnce
            && report.failedMealRestored && report.failedMealTerminatedOnce && report.failedMealReplayExact
            && report.failedMealReservationReleased && report.failedMealReservationReplayExact
            && report.checkpoints.Count == 2 && report.checkpoints.All(value => value.freshActor
                && value.freshFacility && value.executionExact && value.consumptionExact
                && value.stockExact && value.sessionExact);
        report.passed = string.IsNullOrEmpty(report.failure) && report.errors.Count == 0 && !report.networkSent
            && report.drinkLeaseRetired && report.drinkConsumedOnce && report.drinkReplayExact
            && (report.drinkCheckpointResume ? report.coldDrinkStateExact
                : mealVerified && report.drinkRejectedRestoreAtomic
                    && report.drinkLatePublishRollbackAtomic && report.drinkPostRollbackStable
                    && report.drinkRolledBackActionCompletedOnce
                    && report.postphysicalLifecycle.Count == 3
                    && report.con03RetiredObserverNotices == 1
                    && report.postphysicalLifecycle.All(value => value.passed));
        WriteNew("report.json", JsonUtility.ToJson(report, true));
        if (speed != null) speed.Scale = originalScale;
        if (spawner != null) spawner.ConfigureDeterministicSimulationForDiagnostics(paused: spawnerWasPaused);
        Application.logMessageReceived -= CaptureLog;
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(report.passed ? 0 : 1);
    }

    private IEnumerator Verify()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Meal witness requires the owned isolated entry.");
        float ready = Time.realtimeSinceStartup + 45f;
        while (scope == null && Time.realtimeSinceStartup < ready)
        {
            scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).FirstOrDefault(value => value != null && value.Container != null);
            if (scope == null) yield return null;
        }
        Require(scope != null, "Official world scope missing.");
        if (scope.Container.Resolve<ICharacterSpawnerProvider>().TryGetSpawner(out spawner))
        {
            spawnerWasPaused = spawner.DeterministicSimulationPausedForDiagnostics;
            spawner.ConfigureDeterministicSimulationForDiagnostics(paused: true);
        }
        var queue = scope.Container.Resolve<ILocalLlmRuntimeProvider>().GetRequiredRuntime() as LocalLlmRequestQueue;
        Require(queue != null, "Capture-only queue missing.");
        queue.ConfigureIsolatedRequestCapture(request => report.networkSent |= request.NetworkSent || !request.CaptureOnly);
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        if (!scope.Container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out OwnerRunManager manager)
            || manager.CurrentOwnerActor == null)
        {
            StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            // Freeze the fixture at the public party commit, before startup
            // decisions can move the actor a timing-dependent extra cell.
            PauseSetupActors();
            for (int frame = 0; frame < 10; frame++) yield return null;
        }
        Require(StartPartyPreparationPlayModeVerifier.TryReconcileTierZeroForDirectGameplayFixture(scope, out string setup), setup);
        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        consumables = scope.Container.Resolve<ICharacterConsumablesPersistence>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        sessions = scope.Container.Resolve<IServiceSessionRuntime>();
        speed = scope.Container.Resolve<IGameTimeScaleController>();
        originalScale = speed.Scale;
        Require(originalScale > 0f, "Initial game speed is not positive.");
        speed.Scale = 0f;
        PauseSetupActors();
        batch = scope.Container.Resolve<IPhysicalItemBatchDispositionService>() as PhysicalItemBatchDispositionService;
        Require(batch != null && batch.DebugRejectAcknowledgement == null, "Physical authority missing or already under test.");
        string resumePath = Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD");
        if (Environment.GetEnvironmentVariable("DUNGEONSTORY_DRINK_CONTINUATION_VERIFY") == "1")
        {
            Require(!string.IsNullOrEmpty(resumePath), "Cold drink verification requires an explicit hashed checkpoint.");
            yield return VerifyColdDrinkCheckpoint(resumePath);
            yield break;
        }
        if (!string.IsNullOrEmpty(resumePath))
            RestoreColdEatingCheckpoint(resumePath);
        else
            yield return BeginFreshEating();

        yield return VerifyEatingContinuation();
        yield return VerifyDrinkReservationRestore();
        yield return VerifyPostphysicalLifecycle();
    }

    private IEnumerator VerifyPostphysicalLifecycle()
    {
        speed.Scale = 0f;
        PauseSetupActors();
        CharacterActor staff = world.Characters.FirstOrDefault(value => value != null
            && value.Role != CharacterRole.Owner && value.CurrentLifecycleState == CharacterLifecycleState.Active
            && !value.IsDead && value.gameObject.activeInHierarchy && value.TryGetAbility(out AbilityWork _));
        Require(staff != null, "CON03 requires a real prepared non-owner worker.");
        string actorId = staff.Identity.PersistentId;
        Facility facility = CurrentFacility();
        string facilityId = facility.RequirePersistentInstanceId().Value;
        int usesBefore = facility.FacilityState.completedUses;
        var source = scope.Container.Resolve<ICharacterConsumablesInputOwnerDescriptorSource>();
        Require(scope.Container.Resolve<ICharacterConsumablesInputOwnerRuntime>().TryReconcileLive(
            source.BuildLiveInputOwnerDescriptors(), "qa-con03-staff-meal", out string admission), admission);
        string destination = CharacterConsumablesRuntime.GetMealDestinationId(new BuildingInstanceId(facilityId),
            new ConsumableItemDefinitionId(FoodId));
        // The prephysical cancellation branch preserved the original delivered ration.
        // Reuse it: this exact input destination has capacity for one ration only.
        Require(items.GetAllStacks().Where(value => value.ItemId == FoodId
            && value.State == WorldItemStackState.FacilityBuffer && value.DestinationId == destination)
            .Sum(value => value.Quantity) == 1, "CON03 requires exactly one preserved delivered ration.");
        Require(world.TryGetGrid(out Grid grid), "CON03 grid unavailable.");
        staff.Brain.ClearPathSearchCache();
        GridPathSearchResult approach = staff.Brain.GetPathSearch(staff);
        Require(approach != null && approach.ContainsVisitableOccupant(facility),
            "CON03 fixture facility is not reachable by the actual worker.");
        GridMoveStep[] steps = approach.GetMovePathTo(facility).ToArray();
        Vector2Int origin = staff.GetNowXY();
        Vector2Int staging = steps.Length > 1 ? steps[steps.Length - 2].To : origin;
        Require(grid.IsValidGridPos(staging) && grid.IsWalkable(staging) && approach.ContainsPosition(staging),
            "CON03 staging is not on the worker's legal facility route.");
        // Controlled fixture placement, leaving the last real movement/visit step to AIEat.
        staff.transform.position = grid.GetWorldPos(staging);
        staff.Brain.ClearPathSearchCache();
        WriteNew("con03-staging.txt", "actor=" + actorId + "; origin=" + origin + "; staged=" + staging
            + "; facility=" + facilityId + "; legalRouteSteps=" + steps.Length);
        int quantityBefore = FoodQuantity();
        foreach (CharacterCondition condition in staff.Stats.StatSnapshot.Keys.ToArray()) staff.stats[condition] = 100f;
        staff.stats[CharacterCondition.HUNGER] = 0f;
        Require(staff.TryGetAbility(out AbilityWork work), "CON03 worker lacks duty command.");
        work.SetDutyState(AbilityWork.DutyState.OffDuty);
        staff.SetAiPaused(false);
        Require(staff.CanRunAi, "CON03 prepared worker cannot run AI after unpausing.");
        staff.Brain.RequestImmediateReplanForAction<AIEat>();
        speed.Scale = originalScale;
        float until = Time.realtimeSinceStartup + 45f;
        while (string.IsNullOrEmpty(facility.FindActiveUse(actorId)?.mealOperationId)
            && Time.realtimeSinceStartup < until) yield return null;
        speed.Scale = 0f;
        WriteNew("con03-meal-entry.txt", CaptureMealEntryTrace(staff));
        FacilityUseExecutionState use = facility.FindActiveUse(actorId);
        Require(use != null && !string.IsNullOrEmpty(use.mealOperationId)
            && !string.IsNullOrEmpty(use.serviceSessionId), "CON03 real staff AIEat did not start a meal/service.");
        string mealId = use.mealOperationId, facilityOperationId = use.operationId, serviceId = use.serviceSessionId;
        Require(consumables.Capture().activeMealPlans.Single(value => value.planId == mealId).phase
            == CharacterMealPlanPhase.Eating, "CON03 needs the real prephysical checkpoint.");
        string branchStart = saves.ToJson(saves.Capture());
        WriteNew("con03-eating-world.json", branchStart);
        con03Ports = scope.Container.Resolve<ICharacterConsumablesWorldPort>() as CharacterConsumablesApplicationPorts;
        con03Outcomes = scope.Container.Resolve<CharacterConsumablesRuntime>();
        Require(con03Ports != null && con03Outcomes != null
            && con03Ports.DebugRejectMealEffects == null && con03Outcomes.DebugRejectMealFactPublication == null,
            "CON03 production ports missing or already instrumented.");
        foreach (string branch in new[] { "departure-item-committed", "death-item-committed", "departure-effects-applied" })
        {
            Require(saves.TryRestore(saves.FromJson(branchStart), out var restored),
                "CON03 branch restore failed: " + string.Join(" | ", restored.Errors));
            speed.Scale = 0f;
            bool applied = branch == "departure-effects-applied";
            var witness = new PostphysicalLifecycleWitness
            {
                branch = branch, actorId = actorId, facilityId = facilityId, mealOperationId = mealId,
                facilityOperationId = facilityOperationId, serviceSessionId = serviceId, completedUsesBefore = usesBefore
            };
            report.postphysicalLifecycle.Add(witness);
            con03FaultsActive = true;
            con03Ports.DebugRejectMealEffects = command => command.OperationId.Value == mealId && !applied;
            con03Outcomes.DebugRejectMealFactPublication = id => id.Value == actorId;
            batch.DebugRejectAcknowledgement = id => batch.TryGetPending(mealId, out var pending) && pending.CommitId == id;
            speed.Scale = originalScale;
            until = Time.realtimeSinceStartup + 15f;
            CharacterMealPlanSaveData plan = null;
            while (Time.realtimeSinceStartup < until)
            {
                plan = consumables.Capture().activeMealPlans.SingleOrDefault(value => value.planId == mealId);
                if (plan?.phase == (applied ? CharacterMealPlanPhase.EffectsApplied : CharacterMealPlanPhase.ItemCommitted)) break;
                yield return null;
            }
            speed.Scale = 0f;
            Require(plan != null && plan.phase == (applied ? CharacterMealPlanPhase.EffectsApplied : CharacterMealPlanPhase.ItemCommitted)
                && FoodQuantity() == quantityBefore - 1 && batch.TryGetPending(mealId, out _),
                "CON03 actual path did not stop at its exact physical/effect boundary: " + branch);
            witness.physicalCommitId = plan.physicalCommitId;
            witness.sourceStackId = plan.physicalCommitSourceStackIds.Single();
            witness.quantity = plan.physicalCommitQuantity;
            witness.massGrams = plan.physicalCommitInputMassGrams;
            staff = world.Characters.Single(value => value != null && value.Identity.PersistentId == actorId);
            witness.hungerAtRetirement = staff.Stats.GetConditionValue(CharacterCondition.HUNGER);
            if (branch == "death-item-committed")
                scope.Container.Resolve<ICharacterBodyHealthCommand>().Kill(staff, "qa-con03-postphysical-death");
            else staff.SetLifecycleState(CharacterLifecycleState.Despawned);
            witness.actorAbsent = !scope.Container.Resolve<ICharacterConsumablesPersistentActorQuery>()
                .GetPersistentActorIds().Contains(new CharacterId(actorId));
            facility = world.Buildings.OfType<Facility>().Single(value => value.PersistentInstanceId.Value == facilityId);
            witness.facilityUseRetired = facility.FindActiveUse(actorId) == null;
            witness.sessionRetired = !sessions.ActiveSessions.Any(value => value.SessionId == serviceId);
            Require(witness.actorAbsent && witness.facilityUseRetired && witness.sessionRetired,
                "CON03 lifecycle did not retire its actor, actual facility use and service: " + branch);
            con03Ports.DebugRejectMealEffects = null;
            witness.preParentRestored = RestoreDetachedMealWorld(branch + "-pre-parent", witness,
                CharacterMealPlanPhase.EffectsApplied, quantityBefore - 1);
            Require(witness.preParentRestored, "CON03 pre-parent restore changed frozen evidence: " + branch);
            con03Outcomes.DebugRejectMealFactPublication = null;
            speed.Scale = originalScale;
            until = Time.realtimeSinceStartup + 5f;
            while (consumables.Capture().activeMealPlans.SingleOrDefault(value => value.planId == mealId)?.phase
                != CharacterMealPlanPhase.EffectsPublished && Time.realtimeSinceStartup < until) yield return null;
            speed.Scale = 0f;
            witness.publishedRestored = RestoreDetachedMealWorld(branch + "-published", witness,
                CharacterMealPlanPhase.EffectsPublished, quantityBefore - 1);
            Require(witness.publishedRestored, "CON03 published restore changed frozen evidence: " + branch);
            witness.resultKey = consumables.Capture().completedOperations.Single(value => value.operationId == mealId).outcomeResultKey;
            Require(!string.IsNullOrEmpty(witness.resultKey), "CON03 published decision has no exact result key: " + branch);
            batch.DebugRejectAcknowledgement = null;
            speed.Scale = originalScale;
            until = Time.realtimeSinceStartup + 5f;
            while (consumables.Capture().activeMealPlans.Any(value => value.planId == mealId)
                && Time.realtimeSinceStartup < until) yield return null;
            speed.Scale = 0f;
            witness.mealResults = QueryResults(value => value.facts.Any(fact =>
                fact.factId == MigratedProducerOutcomeIds.PhysicalCommitFact.Value && fact.value == witness.physicalCommitId));
            witness.facilityResults = QueryResults(value => value.facts.Any(fact =>
                fact.factId == MigratedProducerOutcomeIds.OperationFact.Value && fact.value == facilityOperationId));
            witness.activeAfter = consumables.Capture().activeMealPlans.Count(value => value.planId == mealId);
            witness.pendingAfter = batch.TryGetPending(mealId, out _) ? 1 : 0;
            witness.completedUsesAfter = CurrentFacility().FacilityState.completedUses;
            Require(witness.activeAfter == 0 && witness.pendingAfter == 0 && witness.quantity == 1
                && witness.mealResults.Count == 1 && witness.facilityResults.Count == 1
                && witness.mealResults[0].status == (applied ? GameplayOutcomeStatus.Succeeded : GameplayOutcomeStatus.PartiallySucceeded)
                && witness.facilityResults[0].status != GameplayOutcomeStatus.Succeeded
                && (!applied || witness.mealResults[0].facts.Any(fact => fact.factId == "summary"
                    && fact.value.Contains("liveObservers=recipient-retired")))
                && witness.completedUsesAfter == usesBefore && FoodQuantity() == quantityBefore - 1,
                "CON03 detached completion lost/duplicated a physical, facility or meal result: " + branch);
            witness.mealStatus = witness.mealResults[0].status.ToString();
            string mealResults = JsonUtility.ToJson(new OutcomeSet { items = witness.mealResults });
            string facilityResults = JsonUtility.ToJson(new OutcomeSet { items = witness.facilityResults });
            string terminal = saves.ToJson(saves.Capture());
            WriteNew("con03-" + branch + "-terminal-world.json", terminal);
            Require(saves.TryRestore(saves.FromJson(terminal), out restored),
                "CON03 terminal restore failed: " + string.Join(" | ", restored.Errors));
            speed.Scale = originalScale;
            yield return null;
            speed.Scale = 0f;
            witness.terminalReplayExact = FoodQuantity() == quantityBefore - 1 && !batch.TryGetPending(mealId, out _)
                && !consumables.Capture().activeMealPlans.Any(value => value.planId == mealId)
                && mealResults == JsonUtility.ToJson(new OutcomeSet { items = QueryResults(value => value.facts.Any(fact =>
                    fact.factId == MigratedProducerOutcomeIds.PhysicalCommitFact.Value && fact.value == witness.physicalCommitId)) })
                && facilityResults == JsonUtility.ToJson(new OutcomeSet { items = QueryResults(value => value.facts.Any(fact =>
                    fact.factId == MigratedProducerOutcomeIds.OperationFact.Value && fact.value == facilityOperationId)) });
            Require(witness.terminalReplayExact, "CON03 terminal whole-world replay changed results: " + branch);
            witness.passed = witness.preParentRestored && witness.publishedRestored && witness.terminalReplayExact;
            con03FaultsActive = false;
        }
    }

    private bool RestoreDetachedMealWorld(string stage, PostphysicalLifecycleWitness witness,
        CharacterMealPlanPhase phase, int expectedFood)
    {
        var captured = saves.Capture();
        witness.sections = captured.sections.Count;
        Require(witness.sections == scope.Container.Resolve<IDungeonSaveSectionRegistry>().OrderedSections.Count,
            "CON03 captured only a partial world.");
        var saved = DungeonSaveJson.Deserialize<DungeonCharacterConsumablesSaveData>(captured.sections
            .Single(value => value.sectionId == CharacterConsumablesSaveSection.Id).payloadJson);
        var plan = saved.activeMealPlans.Single(value => value.planId == witness.mealOperationId);
        Require(plan.phase == phase && plan.physicalCommitId == witness.physicalCommitId
            && plan.effectEvidenceFrozen && plan.physicalContextFrozen, "CON03 frozen decision differs: " + stage);
        string exact = JsonUtility.ToJson(plan);
        string json = saves.ToJson(captured);
        WriteNew("con03-" + stage + "-world.json", json);
        Require(saves.TryRestore(saves.FromJson(json), out var restored),
            "CON03 detached whole-world restore failed: " + stage + "; " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        return exact == JsonUtility.ToJson(consumables.Capture().activeMealPlans.Single(value => value.planId == witness.mealOperationId))
            && !scope.Container.Resolve<ICharacterConsumablesPersistentActorQuery>().GetPersistentActorIds()
                .Contains(new CharacterId(witness.actorId))
            && !sessions.ActiveSessions.Any(value => value.SessionId == witness.serviceSessionId)
            && CurrentFacility().FindActiveUse(witness.actorId) == null && FoodQuantity() == expectedFood
            && batch.TryGetPending(witness.mealOperationId, out var pending) && pending.CommitId == witness.physicalCommitId;
    }

    private void PauseSetupActors()
    {
        foreach (CharacterActor character in world.Characters.Where(value => value != null))
        {
            character.Brain?.StopCurrentActionForReplan("qa-controlled-meal-setup");
            character.SetAiPaused(true);
        }
    }

    private IEnumerator BeginFreshEating()
    {
        CharacterActor actor = world.Characters.Single(value => value != null && value.Role == CharacterRole.Owner);
        report.actorId = actor.Identity.PersistentId;
        Require(world.TryGetGrid(out Grid grid), "Grid unavailable.");
        Facility facility = PrepareDiningFacility(grid);
        report.facilityId = facility.RequirePersistentInstanceId().Value;
        var ownerSource = scope.Container.Resolve<ICharacterConsumablesInputOwnerDescriptorSource>();
        Require(scope.Container.Resolve<ICharacterConsumablesInputOwnerRuntime>().TryReconcileLive(
            ownerSource.BuildLiveInputOwnerDescriptors(), "qa-meal-continuation", out string ownerFailure), ownerFailure);
        string destination = CharacterConsumablesRuntime.GetMealDestinationId(facility.RequirePersistentInstanceId(),
            new ConsumableItemDefinitionId(FoodId));
        Require(items.SpawnItemAt(FoodId, 1, facility.centerPos, WorldItemStackState.FacilityBuffer,
            destination, out int admitted) && admitted == 1, "Controlled delivered-food fixture admission failed.");
        report.foodBefore = FoodQuantity();
        report.usesBefore = facility.FacilityState.completedUses;
        actor.stats[CharacterCondition.HUNGER] = 25f;
        report.hungerBefore = actor.stats[CharacterCondition.HUNGER];
        Require(actor.TryGetAbility(out AbilityWork duty), "Owner lacks work-duty command.");
        duty.SetDutyState(AbilityWork.DutyState.OffDuty);
        actor.SetAiPaused(false);
        actor.Brain.RequestImmediateReplanForAction<AIEat>();
        speed.Scale = originalScale;

        float until = Time.realtimeSinceStartup + 100f;
        float traceAt = Time.realtimeSinceStartup;
        FacilityUseExecutionState execution = null;
        while (Time.realtimeSinceStartup < until)
        {
            if (Time.realtimeSinceStartup >= traceAt)
            {
                report.freshEatingTrace.Add(CaptureMealEntryTrace(actor));
                traceAt = Time.realtimeSinceStartup + 5f;
            }
            Require(!actor.Brain.FirstPreferredActionHardFailure.HasFailure,
                "AIEat rejected: " + actor.Brain.FirstPreferredActionHardFailure);
            execution = facility.FindActiveUse(report.actorId);
            if (!string.IsNullOrEmpty(execution?.mealOperationId)) break;
            yield return null;
        }
        report.freshEatingTrace.Add(CaptureMealEntryTrace(actor));
        WriteNew("fresh-eating-observation.txt", "actor=" + report.actorId
            + "\ncanRunAi=" + actor.CanRunAi + "\nposition=" + actor.GetNowXY()
            + "\naction=" + actor.Brain.bestAction?.actionset?.GetType().Name
            + "\ndestination=" + actor.Brain.bestAction?.destination
            + "\nfacility=" + report.facilityId + "\nfacilityPosition=" + facility.centerPos
            + "\nhunger=" + actor.stats[CharacterCondition.HUNGER]
            + "\npreferredFailure=" + actor.Brain.FirstPreferredActionHardFailure
            + "\nexecution=" + JsonUtility.ToJson(execution));
        Require(execution != null && !string.IsNullOrEmpty(execution.mealOperationId),
            "Actual AIEat did not reach the authored dining meal operation.");
        speed.Scale = 0f;
        report.operationId = execution.operationId;
        report.mealOperationId = execution.mealOperationId;
        report.serviceSessionId = execution.serviceSessionId;
        Require(!string.IsNullOrEmpty(report.serviceSessionId), "Dining use has no actual service session.");
        CharacterMealPlanSaveData eating = MealPlan();
        Require(eating != null && eating.phase == CharacterMealPlanPhase.Eating
            && eating.characterId == report.actorId && eating.facilityInstanceId == report.facilityId,
            "Dining execution does not own its exact eating plan.");
        RestoreCheckpoint("eating");
    }

    private static string CaptureMealEntryTrace(CharacterActor actor)
    {
        AIBrain brain = actor.Brain;
        AIAction action = brain.bestAction;
        actor.TryGetAbility(out AbilityMove move);
        actor.TryGetAbility(out AbilityShopping shopping);
        string pathSearch = string.Empty;
        if (actor.PathSearchBroker is GridPathSearchBroker broker && brain.TryGetRuntimeGrid(out Grid grid))
        {
            BuildableObject target = brain.LastPreferredActionDeferredFailure.Target ?? brain.LastActionFailure.Target;
            pathSearch = "\nsearchBudget=" + broker.SearchBudgetForDiagnostics
                + "; timeBudgetMs=" + broker.SearchTimeBudgetMillisecondsForDiagnostics
                + "; searches=" + broker.SearchesThisFrame + "; searchMs=" + broker.SearchMillisecondsThisFrame
                + "; deferrals=" + broker.BudgetDeferralsThisFrame + "; cacheHits=" + broker.CacheHitsThisFrame
                + "; costVersion=" + broker.CostPolicyVersionForDiagnostics
                + "; target=" + (target != null ? target.RequirePersistentInstanceId().Value : "none");
            if (target != null)
                foreach (Vector2Int cell in target.buildPoses)
                    pathSearch += "\ncell=" + cell + "; " + broker.DescribeExactSearchForDiagnostics(grid,
                        actor.GetNowXY(), cell, GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor)));
        }
        return "time=" + Time.time + "; position=" + actor.GetNowXY() + "; canRun=" + actor.CanRunAi
            + "; action=" + action?.actionset?.GetType().Name + "; started=" + action?.HasStarted
            + "; destination=" + (action?.destination != null ? action.destination.RequirePersistentInstanceId().Value : "none")
            + "; plan=" + action?.planKind + "; steps=" + action?.pathSteps.Count
            + "; shopping=" + shopping?.HasActiveShoppingRoutineForDiagnostics
            + "; visit=" + shopping?.LastVisitOutcome
            + "; movement=" + move?.HasActiveMovementRoutineForDiagnostics
            + "; movementOwner=" + move?.ActiveMovementOperationOwnerForDiagnostics
            + "; blocked=" + move?.LastGridMoveWasBlocked + "/" + move?.LastGridMoveFailureReason
            + "; cancel=" + move?.LastMovementCancellationSourceForDiagnostics
            + "/" + move?.LastActionMovementCancellationReasonForDiagnostics
            + "; block=" + brain.CaptureCurrentOperationBlock().Failure
            + "\n" + brain.GetDebugSummary(1) + pathSearch;
    }

    private IEnumerator VerifyEatingContinuation()
    {
        // The user may pause longer than the old wall-clock timeout. This must
        // neither abandon a game-time meal nor advance its game-time progress.
        for (int frame = 0; frame < 2; frame++) yield return null;
        string pausedExecution = JsonUtility.ToJson(CurrentFacility().FindActiveUse(report.actorId));
        string pausedPlan = JsonUtility.ToJson(MealPlan());
        var labor = scope.Container.Resolve<ISettlementLaborAccountingService>();
        var laborSave = scope.Container.Resolve<ISettlementLaborPersistence>();
        string pausedLabor = JsonUtility.ToJson(laborSave.CaptureLaborSaveData());
        long pausedLaborRevision = labor.Capture().Revision;
        float pauseUntil = Time.realtimeSinceStartup + 16.2f;
        while (Time.realtimeSinceStartup < pauseUntil) yield return null;
        report.pausePreserved = pausedExecution == JsonUtility.ToJson(CurrentFacility().FindActiveUse(report.actorId))
            && pausedPlan == JsonUtility.ToJson(MealPlan()) && FoodQuantity() == report.foodBefore
            && pausedLabor == JsonUtility.ToJson(laborSave.CaptureLaborSaveData())
            && pausedLaborRevision == labor.Capture().Revision;
        WriteNew("paused-consumables.json", JsonUtility.ToJson(consumables.Capture(), true));
        WriteNew("paused-labor.json", JsonUtility.ToJson(laborSave.CaptureLaborSaveData(), true));
        Require(report.pausePreserved, "Paused meal/labor advanced or changed despite zero game time.");

        batch.DebugRejectAcknowledgement = commitId =>
        {
            if (!batch.TryGetPending(report.mealOperationId, out var pendingReceipt)
                || pendingReceipt.CommitId != commitId) return false;
            report.rejectedAcks++;
            return true;
        };
        speed.Scale = originalScale;
        float until = Time.realtimeSinceStartup + 25f;
        while (Time.realtimeSinceStartup < until)
        {
            CharacterMealPlanSaveData pending = MealPlan();
            if (pending == null || pending.phase == CharacterMealPlanPhase.EffectsPublished) break;
            yield return null;
        }
        speed.Scale = 0f;
        bool foundResult = scope.Container.Resolve<CharacterConsumablesRuntime>().TryGetMealOperationResult(
            new ConsumableOperationId(report.mealOperationId), out CharacterConsumablesMealResult mealResult);
        WriteNew("consumption-observed.json", JsonUtility.ToJson(new ConsumptionObservation
        {
            actorCanRunAi = CurrentActor().CanRunAi, resultFound = foundResult,
            resultCode = mealResult.FailureCode.ToString(), parameters = mealResult.Parameters.ToArray(),
            execution = CurrentFacility().FindActiveUse(report.actorId), consumables = consumables.Capture()
        }, true));
        Require(report.rejectedAcks > 0 && MealPlan()?.phase == CharacterMealPlanPhase.EffectsPublished,
            "Actual consumption did not reach the injected physical acknowledgement failure: "
                + mealResult.FailureCode + "; " + string.Join(" | ", mealResult.Parameters.ToArray()));
        if (report.checkpointResume)
        {
            report.consumedAfterGameSeconds = consumables.Capture().timelineTime - report.savedTimeline;
            Require(report.consumedAfterGameSeconds >= report.remainingMealSeconds
                && report.consumedAfterGameSeconds <= report.remainingMealSeconds + 1f,
                "Cold restore changed the remaining meal duration.");
        }
        report.foodAfter = FoodQuantity();
        report.hungerAfter = CurrentActor().stats[CharacterCondition.HUNGER];
        Require(report.foodAfter == report.foodBefore - 1 && report.hungerAfter > report.hungerBefore,
            "Pending ACK did not preserve one real food consumption and hunger recovery.");
        yield return VerifyMealLatePublishRollback();
        RestoreCheckpoint("effects-published");
        batch.DebugRejectAcknowledgement = null;
        speed.Scale = originalScale;
        until = Time.realtimeSinceStartup + 35f;
        while (Time.realtimeSinceStartup < until)
        {
            if (CurrentFacility().FindActiveUse(report.actorId) == null) break;
            yield return null;
        }
        speed.Scale = 0f;
        report.results = Results();
        report.usesAfter = CurrentFacility().FacilityState.completedUses;
        report.completedOnce = MealPlan() == null && CompletedMeals() == 1
            && CurrentFacility().FindActiveUse(report.actorId) == null
            && report.usesAfter == report.usesBefore + 1 && FoodQuantity() == report.foodAfter
            && report.results.Count == 1 && report.results[0].status == GameplayOutcomeStatus.Succeeded
            && !sessions.ActiveSessions.Any(value => value.SessionId == report.serviceSessionId);
        Require(report.completedOnce, "Meal/service did not finish once or its stock/result was lost/duplicated.");
        string resultJson = JsonUtility.ToJson(new OutcomeSet { items = report.results });
        string completed = saves.ToJson(saves.Capture());
        WriteNew("completed-world.json", completed);
        Require(saves.TryRestore(saves.FromJson(completed), out var restored),
            "Completed meal restore failed: " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        report.replayExact = resultJson == JsonUtility.ToJson(new OutcomeSet { items = Results() })
            && MealPlan() == null && CompletedMeals() == 1 && FoodQuantity() == report.foodAfter
            && CurrentFacility().FacilityState.completedUses == report.usesAfter
            && CurrentFacility().FindActiveUse(report.actorId) == null;
        Require(report.replayExact, "Completed-world restore revived/lost/duplicated the meal.");
        WriteNew("completed-restored-world.json", saves.ToJson(saves.Capture()));

        // The restore-retirement exception must not suppress normal lifecycle
        // cancellation of the replacement actor's still-unconsumed meal.
        Require(saves.TryRestore(saves.FromJson(eatingWorld), out restored),
            "Independent disable branch restore failed: " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        CurrentActor().gameObject.SetActive(false);
        report.ordinaryDisableCancelled = MealPlan() == null && CompletedMeals() == 0
            && FoodQuantity() == report.foodBefore && CurrentFacility().FindActiveUse(report.actorId) == null
            && !sessions.ActiveSessions.Any(value => value.SessionId == report.serviceSessionId);
        WriteNew("ordinary-disable-consumables.json", JsonUtility.ToJson(consumables.Capture(), true));
        Require(report.ordinaryDisableCancelled, "Ordinary actor disable retained its meal or consumed food.");
        yield return VerifyFailedMealContinuation();
    }

    private IEnumerator VerifyFailedMealContinuation()
    {
        Require(saves.TryRestore(saves.FromJson(eatingWorld), out var restored),
            "Failure branch setup restore failed: " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        var runtime = (CharacterConsumablesRuntime)consumables;
        const string reason = "qa-prephysical-meal-abort";
        Require(runtime.CancelActiveMealOperations(new CharacterId(report.actorId), reason) == 1,
            "Failure branch did not abort its one unconsumed meal.");
        var mealReservations = scope.Container.Resolve<IItemQuantityReservationService>();
        report.failedMealReservationReleased = !mealReservations.TryGetLeasesByOwner(report.mealOperationId, out _);
        Require(report.failedMealReservationReleased, "Restored meal cancellation retained its physical reservation before rebind.");
        var failure = consumables.Capture().failedMealOperations.Single(value => value.operationId == report.mealOperationId);
        string failureJson = JsonUtility.ToJson(failure);
        Require(!failure.recoveryPending && failure.detail == reason && MealPlan() == null
            && CompletedMeals() == 0 && FoodQuantity() == report.foodBefore
            && CurrentFacility().FindActiveUse(report.actorId)?.mealOperationId == report.mealOperationId,
            "Failure branch lost its terminal receipt or changed physical consumption.");
        string pendingFailure = saves.ToJson(saves.Capture());
        WriteNew("failed-meal-world.json", pendingFailure);
        VerifyMalformedMealOwners("failed", pendingFailure);
        var actor = CurrentActor();
        var facility = CurrentFacility();
        string useJson = JsonUtility.ToJson(facility.FindActiveUse(report.actorId));
        Require(saves.TryRestore(saves.FromJson(pendingFailure), out restored),
            "Failed meal whole-world restore rejected: " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        bool found = runtime.TryGetMealOperationResult(new ConsumableOperationId(report.mealOperationId), out var result);
        report.failedMealRestored = !ReferenceEquals(actor, CurrentActor())
            && !ReferenceEquals(facility, CurrentFacility()) && !found && !result.Success
            && result.OperationId.Value == report.mealOperationId && result.Parameters.Contains(reason)
            && result.FailureCode == CharacterConsumablesFailureCode.PhysicalConsumptionFailed
            && useJson == JsonUtility.ToJson(CurrentFacility().FindActiveUse(report.actorId))
            && failureJson == JsonUtility.ToJson(consumables.Capture().failedMealOperations
                .Single(value => value.operationId == report.mealOperationId));
        Require(report.failedMealRestored, "Failed meal owner/reason was not restored exactly.");
        WriteNew("failed-meal-restored-world.json", saves.ToJson(saves.Capture()));
        speed.Scale = originalScale;
        float until = Time.realtimeSinceStartup + 15f;
        while (CurrentFacility().FindActiveUse(report.actorId) != null && Time.realtimeSinceStartup < until)
            yield return null;
        speed.Scale = 0f;
        var results = Results();
        report.failedMealTerminatedOnce = CurrentFacility().FindActiveUse(report.actorId) == null
            && MealPlan() == null && CompletedMeals() == 0 && FoodQuantity() == report.foodBefore
            && CurrentFacility().FacilityState.completedUses == report.usesBefore
            && results.Count == 1 && results[0].status == GameplayOutcomeStatus.Failed
            && !sessions.ActiveSessions.Any(value => value.SessionId == report.serviceSessionId);
        Require(report.failedMealTerminatedOnce, "Restored failed meal did not terminate once without consumption.");
        string resultsJson = JsonUtility.ToJson(new OutcomeSet { items = results });
        string terminal = saves.ToJson(saves.Capture());
        WriteNew("failed-meal-terminal-world.json", terminal);
        Require(saves.TryRestore(saves.FromJson(terminal), out restored),
            "Terminal failed meal restore rejected: " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        report.failedMealReservationReplayExact = !mealReservations.TryGetLeasesByOwner(report.mealOperationId, out _);
        Require(report.failedMealReservationReplayExact, "Terminal failed meal restore revived its physical reservation.");
        report.failedMealReplayExact = resultsJson == JsonUtility.ToJson(new OutcomeSet { items = Results() })
            && failureJson == JsonUtility.ToJson(consumables.Capture().failedMealOperations
                .Single(value => value.operationId == report.mealOperationId))
            && MealPlan() == null && CompletedMeals() == 0 && FoodQuantity() == report.foodBefore
            && CurrentFacility().FindActiveUse(report.actorId) == null;
        Require(report.failedMealReplayExact, "Terminal failure replay lost its reason or duplicated consumption/outcome.");
    }

    private IEnumerator VerifyDrinkReservationRestore()
    {
        const string waterId = "resource:clean-water", foreignOwner = "qa:drink:foreign-reservation";
        var reservations = scope.Container.Resolve<IItemQuantityReservationService>();
        var reservationPersistence = scope.Container.Resolve<IItemQuantityReservationPersistence>();
        CharacterActor actor = CurrentActor();
        actor.Brain.StopCurrentActionForReplan("qa-drink-restore-setup");
        actor.SetAiPaused(true);
        foreach (CharacterCondition condition in actor.Stats.StatSnapshot.Keys.ToArray())
            actor.stats[condition] = 100f;
        actor.stats[CharacterCondition.THIRST] = 25f;
        Require(world.TryGetGrid(out Grid grid), "Drink grid unavailable.");
        Vector2Int origin = actor.GetNowXY();
        Vector2Int? destination = Enumerable.Range(4, 12)
            .SelectMany(distance => new[] { origin + Vector2Int.right * distance, origin + Vector2Int.left * distance })
            .Where(cell => grid.IsValidGridPos(cell) && grid.IsWalkable(cell))
            .Select(cell => (Vector2Int?)cell).FirstOrDefault();
        Require(destination.HasValue, "No reachable-cell candidate for controlled water fixture.");
        Require(items.SpawnItemAt(waterId, 3, destination.Value, WorldItemStackState.Loose,
            string.Empty, out int admitted) && admitted == 3, "Controlled water fixture admission failed.");
        actor.SetAiPaused(false);
        actor.Brain.RequestImmediateReplanForAction<AIDrink>();
        speed.Scale = originalScale;
        ItemQuantityLease drink = null;
        float until = Time.realtimeSinceStartup + 60f;
        while (Time.realtimeSinceStartup < until && drink == null)
        {
            drink = DrinkLease(reservations);
            if (drink == null) yield return null;
        }
        Require(drink != null && actor.Brain.bestAction?.actionset is AIDrink,
            "Actual AIDrink did not expose its in-flight water lease.");
        speed.Scale = 0f;
        report.drinkOperationId = drink.ownerOperationId;
        report.drinkSourceStackId = drink.slices.Single().stackId;
        var foreignStack = items.GetAllStacks().First(value => value.ItemId == waterId
            && reservations.GetAvailableQuantity(new ItemStackId(value.StackId)) > 0);
        Require(reservations.TryReserve(foreignOwner, report.actorId, ItemReservationPurpose.ProductionInput,
            "qa:drink:foreign-cohort", new ItemQuantityReservationRequest(new ItemStackId(foreignStack.StackId),
                1, foreignStack.ReservationSignature), out _, out var reservationFailure),
            "Foreign reservation fixture failed: " + reservationFailure);
        string foreignJson = JsonUtility.ToJson(reservationPersistence.CaptureReservationIntents()
            .Single(value => value.ownerOperationId == foreignOwner));
        report.waterBefore = WaterQuantity();
        report.drinkThirstBefore = actor.stats[CharacterCondition.THIRST];
        var previousResults = WaterResults();
        Require(previousResults.Count == 0, "Water witness requires no earlier owner drinking result.");
        DungeonGameSaveData saved = saves.Capture();
        string savedJson = saves.ToJson(saved);
        WriteNew("drink-inflight-world.json", savedJson);
        string StateSections(DungeonGameSaveData data) => string.Join("\n", data.sections.OrderBy(value => value.sectionId,
            StringComparer.Ordinal).Select(value => value.sectionId + ":" + value.sectionVersion + ":" + value.payloadJson));
        var invalid = saves.FromJson(savedJson);
        var invalidSection = invalid.sections.Single(value => value.sectionId == CharacterConsumablesSaveSection.Id);
        var invalidPayload = DungeonSaveJson.Deserialize<DungeonCharacterConsumablesSaveData>(invalidSection.payloadJson);
        invalidPayload.timelineTime = -1f;
        invalidSection.payloadJson = JsonUtility.ToJson(invalidPayload);
        string allBefore = StateSections(saves.Capture());
        string leaseBefore = JsonUtility.ToJson(drink);
        bool digestRejected = !saves.TryRestore(invalid, out var digestFailure)
            && digestFailure.Errors.Any(value => value.Contains("payload digest mismatch"))
            && ReferenceEquals(actor, CurrentActor()) && allBefore == StateSections(saves.Capture())
            && leaseBefore == JsonUtility.ToJson(DrinkLease(reservations));
        WriteNew("drink-digest-rejected-restore.txt", "unchangedRejection=" + digestRejected
            + "\nerrors=" + string.Join(" | ", digestFailure.Errors));
        Require(digestRejected, "Tampered drink checkpoint bypassed integrity validation or changed the world.");
        // Reseal only the intentionally invalid test copy so domain validation,
        // not the envelope checksum, must reject its negative timeline.
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        bool invalidRestored = saves.TryRestore(invalid, out var rejected);
        bool actorUnchanged = ReferenceEquals(actor, CurrentActor());
        bool sectionsUnchanged = allBefore == StateSections(saves.Capture());
        string rejectedLease = JsonUtility.ToJson(DrinkLease(reservations));
        WriteNew("drink-rejected-restore.txt", "restored=" + invalidRestored
            + "\nerrors=" + string.Join(" | ", rejected.Errors)
            + "\nactorUnchanged=" + actorUnchanged + "\nsectionsUnchanged=" + sectionsUnchanged
            + "\nleaseBefore=" + leaseBefore + "\nleaseAfter=" + rejectedLease);
        report.drinkRejectedRestoreAtomic = !invalidRestored
            && rejected.Errors.Any(value => value.Contains("timeline"))
            && actorUnchanged && sectionsUnchanged && leaseBefore == rejectedLease;
        Require(report.drinkRejectedRestoreAtomic, "Rejected drink checkpoint altered world/lease/actor or failed for a different reason.");
        yield return VerifyDrinkLatePublishRollback(savedJson);
        Require(saves.TryRestore(saves.FromJson(savedJson), out var restored),
            "In-flight drink restore failed: " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        CurrentActor().SetAiPaused(true);
        yield return null; // The production deprivation Tick reconciles even while game time is paused.
        report.drinkLeaseRetired = !ReferenceEquals(actor, CurrentActor())
            && !reservations.TryGetLeasesByOwner(report.drinkOperationId, out _)
            && WaterQuantity() == report.waterBefore && WaterResults().Count == 0
            && foreignJson == JsonUtility.ToJson(reservationPersistence.CaptureReservationIntents()
                .Single(value => value.ownerOperationId == foreignOwner))
            && saved.sections.Single(value => value.sectionId == DarkSurvivalSaveSection.Id).payloadJson
                == saves.Capture().sections.Single(value => value.sectionId == DarkSurvivalSaveSection.Id).payloadJson;
        Require(report.drinkLeaseRetired, "Restored transient drink lease/old coroutine corrupted reservation or need ownership.");
        WriteNew("drink-restored-world.json", saves.ToJson(saves.Capture()));
        yield return VerifyDrinkCompletion(foreignOwner);
    }

    private IEnumerator VerifyDrinkLatePublishRollback(string savedJson)
    {
        var controlBefore = saves.Capture();
        yield return null;
        var controlAfter = saves.Capture();
        WriteNew("drink-paused-control.txt", "scale=" + speed.Scale + "\nchangedSections="
            + string.Join(",", ChangedSectionIds(controlBefore, controlAfter)));
        Require(PausedFrameAuthority(controlBefore) == PausedFrameAuthority(controlAfter),
            "Paused control changed authority beyond frame RNG/derived facility cache stamps.");
        WriteNew("drink-before-late-publish-world.json", saves.ToJson(controlAfter));
        var root = scope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
        var official = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        var participants = scope.Container.Resolve<IEnumerable<IDungeonRestoreTransactionParticipant>>().ToArray();
        var fault = new LateRestoreFault("drink");
        Require(participants.Length > 0 && participants.All(value =>
            string.CompareOrdinal(value.ParticipantId, fault.ParticipantId) < 0),
            "Late drink fault must follow every production restore participant.");
        var registry = new DungeonSaveSectionRegistry(official.OrderedSections, root,
            participants.Concat(new[] { fault }),
            scope.Container.Resolve<IEnumerable<IDungeonSaveRegistryPreflightValidator>>());
        var reservations = scope.Container.Resolve<IItemQuantityReservationService>();
        CharacterActor actor = CurrentActor();
        var action = actor.Brain.bestAction;
        int revision = root.PublishedRestoreRevision;
        float scaleBefore = speed.Scale;
        var beforeSave = saves.Capture();
        string before = StateSectionPayloads(beforeSave);
        string lease = JsonUtility.ToJson(DrinkLease(reservations));
        var failure = new DungeonGameRestoreReport();
        bool accepted = registry.RestoreAll(saves.FromJson(savedJson).sections, failure);
        WriteNew("drink-late-publish-rollback.txt", "accepted=" + accepted
            + "\nparticipants=" + participants.Length + "\nsections=" + official.OrderedSections.Count
            + "\nfaultReached=" + fault.PublishAttempted + "\nfaultRolledBack=" + fault.RolledBack
            + "\nrootUnchanged=" + (!root.IsRestoreStaging && root.PublishedRestoreRevision == revision)
            + "\nscaleBefore=" + scaleBefore + "\nscaleAfter=" + speed.Scale
            + "\nactorUnchanged=" + ReferenceEquals(actor, CurrentActor())
            + "\nactionUnchanged=" + ReferenceEquals(action, actor.Brain.bestAction)
            + "\nsectionsUnchanged=" + (before == StateSectionPayloads(saves.Capture()))
            + "\nleaseUnchanged=" + (lease == JsonUtility.ToJson(DrinkLease(reservations)))
            + "\nerrors=" + string.Join(" | ", failure.Errors));
        report.drinkLatePublishRollbackAtomic = !accepted && fault.PublishAttempted && fault.RolledBack
            && failure.Errors.Count == 1 && failure.Errors[0].Contains("qa-drink-late-publication-fault")
            && !root.IsRestoreStaging && root.PublishedRestoreRevision == revision
            && speed.Scale == scaleBefore
            && ReferenceEquals(actor, CurrentActor()) && ReferenceEquals(action, actor.Brain.bestAction)
            && before == StateSectionPayloads(saves.Capture())
            && lease == JsonUtility.ToJson(DrinkLease(reservations));
        Require(report.drinkLatePublishRollbackAtomic,
            "Late publication failure changed world/reference/action/lease or did not reach the intended fault.");
        yield return null; // Reconciliation must not retire live claims after a failed restore.
        var afterTick = saves.Capture();
        WriteNew("drink-after-late-publish-tick-world.json", saves.ToJson(afterTick));
        report.drinkPostRollbackStable = PausedFrameAuthority(beforeSave) == PausedFrameAuthority(afterTick)
            && lease == JsonUtility.ToJson(DrinkLease(reservations)) && speed.Scale == scaleBefore
            && ReferenceEquals(actor, CurrentActor()) && ReferenceEquals(action, actor.Brain.bestAction)
            && root.PublishedRestoreRevision == revision;
        Require(report.drinkPostRollbackStable,
            "Deferred cleanup changed the live world after rejected publication.");
        // Do not request a new action here: the original coroutine must survive rollback.
        speed.Scale = originalScale;
        float until = Time.realtimeSinceStartup + 45f;
        while (Time.realtimeSinceStartup < until && WaterResults().Count == 0) yield return null;
        speed.Scale = 0f;
        actor.SetAiPaused(true);
        var outcomes = WaterResults();
        report.drinkRolledBackActionCompletedOnce = ReferenceEquals(actor, CurrentActor())
            && WaterQuantity() == report.waterBefore - 1 && outcomes.Count == 1
            && outcomes[0].status == GameplayOutcomeStatus.Succeeded && DrinkLease(reservations) == null
            && actor.stats[CharacterCondition.THIRST] > report.drinkThirstBefore;
        WriteNew("drink-rollback-completed-world.json", saves.ToJson(saves.Capture()));
        Require(report.drinkRolledBackActionCompletedOnce,
            "Original AIDrink did not finish exactly once after late restore rollback.");
    }

    private static string StateSectionPayloads(DungeonGameSaveData data) => string.Join("\n", data.sections
        .OrderBy(value => value.sectionId, StringComparer.Ordinal)
        .Select(value => value.sectionId + ":" + value.sectionVersion + ":" + value.payloadJson));

    private static IEnumerable<string> ChangedSectionIds(DungeonGameSaveData before, DungeonGameSaveData after)
        => before.sections.Where(value => value.payloadJson != after.sections.Single(next =>
            next.sectionId == value.sectionId).payloadJson).Select(value => value.sectionId);

    private static string PausedFrameAuthority(DungeonGameSaveData data)
    {
        // Applied only across a yielded frame, never to the immediate rollback
        // comparison. The no-restore control proves these existing Tick changes:
        // WildlifeRuntime draws an offscreen interval before its due-time check;
        // FacilityEvolutionActivationProjection refreshes an ephemeral cache stamp.
        // Every other field, including all outcomes, active nodes and item data,
        // must still match. Source checkpoints and live state are never edited.
        var parts = new List<string>();
        foreach (var section in data.sections.OrderBy(value => value.sectionId, StringComparer.Ordinal))
        {
            string payload = section.payloadJson;
            if (section.sectionId == "foundation.random-streams")
            {
                var json = JObject.Parse(payload);
                var stream = json["streams"].Children<JObject>().SingleOrDefault(value =>
                    (string)value["streamId"] == "wildlife.runtime");
                if (stream != null) stream["state"] = "<paused-frame-wildlife-rng>";
                payload = json.ToString(Newtonsoft.Json.Formatting.None);
            }
            else if (section.sectionId == "world.facilities")
            {
                var worldData = DungeonSaveJson.Deserialize<ModularFacilityWorldSaveData>(payload);
                foreach (var module in worldData.buildings.SelectMany(value => value.stateModules))
                {
                    var json = JObject.Parse(module.payload);
                    if (json.SelectToken("instanceEvolution.facilityStateVersion") == null) continue;
                    const string pattern = "\"facilityStateVersion\":-?[0-9]+";
                    Require(Regex.Matches(module.payload, pattern).Count == 1,
                        "Ambiguous facility cache stamp in paused-frame comparison.");
                    module.payload = Regex.Replace(module.payload, pattern, "\"facilityStateVersion\":0");
                }
                payload = JsonUtility.ToJson(worldData);
            }
            parts.Add(section.sectionId + ":" + section.sectionVersion + ":" + payload);
        }
        return string.Join("\n", parts);
    }

    private IEnumerator VerifyColdDrinkCheckpoint(string path)
    {
        report.checkpointResume = report.drinkCheckpointResume = true;
        report.scope = "Hashed actual AIDrink in-flight checkpoint restored in a fresh isolated process with a younger raw clock. Exact physical stock and unrelated reservation intents, transient drink lease retirement, actual replan/one water consumption/outcome and completed world replay. No meal assertions, natural production, guest payment, late publication rollback or training evidence.";
        var saved = saves.FromJson(ReadHashedCheckpoint(path));
        var physical = DungeonSaveJson.Deserialize<DungeonPhysicalItemSaveData>(saved.sections
            .Single(value => value.sectionId == "items.physical").payloadJson);
        var intent = physical.reservationIntents.Single(value => value.hadActiveItemReservation
            && value.ownerOperationId.StartsWith("safe-drink:" + value.ownerCharacterId + ":", StringComparison.Ordinal));
        var hint = intent.reservationHints.Single();
        Require(hint.purpose == ItemReservationPurpose.PersonalConsumption && hint.itemId == "resource:clean-water"
            && hint.quantity == 1, "Cold drink source must contain one actual personal water claim.");
        report.actorId = intent.ownerCharacterId;
        report.drinkOperationId = intent.ownerOperationId;
        report.drinkSourceStackId = hint.preferredPhysicalStackId;
        report.waterBefore = physical.stacks.Where(value => value.itemId == "resource:clean-water").Sum(value => value.quantity);
        const string foreignOwner = "qa:drink:foreign-reservation";
        Require(physical.reservationIntents.Any(value => value.ownerOperationId == foreignOwner),
            "Cold drink source must retain the unrelated reservation witness.");
        var savedConsumables = DungeonSaveJson.Deserialize<DungeonCharacterConsumablesSaveData>(saved.sections
            .Single(value => value.sectionId == CharacterConsumablesSaveSection.Id).payloadJson);
        report.rawClockBeforeRestore = scope.Container.Resolve<IGameClock>().Time;
        report.savedTimeline = savedConsumables.timelineTime;
        Require(report.rawClockBeforeRestore < report.savedTimeline,
            "Cold drink witness requires a younger raw clock than the saved timeline.");
        CharacterActor previous = CurrentActor();
        Require(saves.TryRestore(saved, out var restored),
            "Cold drink restore failed: " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        CurrentActor().SetAiPaused(true);
        report.drinkThirstBefore = CurrentActor().stats[CharacterCondition.THIRST];
        yield return null;
        var after = saves.Capture();
        var reservations = scope.Container.Resolve<IItemQuantityReservationService>();
        // The checkpoint file is never modified. Only the comparison model
        // removes the single transient claim which production must retire.
        physical.reservationIntents.Remove(intent);
        var actualPhysical = DungeonSaveJson.Deserialize<DungeonPhysicalItemSaveData>(after.sections
            .Single(value => value.sectionId == "items.physical").payloadJson);
        report.coldDrinkStateExact = !ReferenceEquals(previous, CurrentActor())
            && saved.sections.Count == scope.Container.Resolve<IDungeonSaveSectionRegistry>().OrderedSections.Count
            && JsonUtility.ToJson(physical) == JsonUtility.ToJson(actualPhysical)
            && saved.sections.Single(value => value.sectionId == DarkSurvivalSaveSection.Id).payloadJson
                == after.sections.Single(value => value.sectionId == DarkSurvivalSaveSection.Id).payloadJson;
        report.drinkLeaseRetired = !reservations.TryGetLeasesByOwner(report.drinkOperationId, out _)
            && WaterQuantity() == report.waterBefore && WaterResults().Count == 0;
        WriteNew("cold-drink-restored-world.json", saves.ToJson(after));
        Require(report.coldDrinkStateExact && report.drinkLeaseRetired,
            "Cold drink restore changed stock/unrelated reservations/needs or retained the transient claim.");
        yield return VerifyDrinkCompletion(foreignOwner);
    }

    private int WaterQuantity() => items.GetAllStacks().Where(value => value.ItemId == "resource:clean-water").Sum(value => value.Quantity);

    private List<GameplayOutcomeSnapshot> WaterResults() => QueryResults(value => value.outcomeTypeId == "survival.water-consumed"
        && value.participants.Any(participant => participant.roleId == "actor" && participant.entityId == report.actorId));

    private IEnumerator VerifyDrinkCompletion(string foreignOwner)
    {
        var reservations = scope.Container.Resolve<IItemQuantityReservationService>();
        CharacterActor actor = CurrentActor();
        actor.SetAiPaused(false);
        actor.Brain.RequestImmediateReplanForAction<AIDrink>();
        speed.Scale = originalScale;
        float until = Time.realtimeSinceStartup + 45f;
        while (Time.realtimeSinceStartup < until && WaterResults().Count == 0) yield return null;
        speed.Scale = 0f;
        actor.SetAiPaused(true);
        report.drinkResults = WaterResults();
        report.waterAfter = WaterQuantity();
        report.drinkConsumedOnce = report.waterAfter == report.waterBefore - 1 && report.drinkResults.Count == 1
            && report.drinkResults[0].status == GameplayOutcomeStatus.Succeeded
            && actor.stats[CharacterCondition.THIRST] > report.drinkThirstBefore && DrinkLease(reservations) == null;
        Require(report.drinkConsumedOnce, "Replanned AIDrink did not consume/recover/publish exactly once.");
        string completed = saves.ToJson(saves.Capture());
        WriteNew("drink-completed-world.json", completed);
        string outcomeJson = JsonUtility.ToJson(new OutcomeSet { items = report.drinkResults });
        Require(saves.TryRestore(saves.FromJson(completed), out var restored),
            "Completed drink restore failed: " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        CurrentActor().SetAiPaused(true);
        yield return null;
        report.drinkReplayExact = WaterQuantity() == report.waterAfter && DrinkLease(reservations) == null
            && outcomeJson == JsonUtility.ToJson(new OutcomeSet { items = WaterResults() });
        Require(report.drinkReplayExact, "Completed drinking replay duplicated consumption or changed outcome.");
        WriteNew("drink-completed-restored-world.json", saves.ToJson(saves.Capture()));
        reservations.ReleaseByOwner(foreignOwner, ItemReservationReleaseReason.Cancelled);
    }

    private ItemQuantityLease DrinkLease(IItemQuantityReservationService reservations) => items.GetAllStacks()
        .SelectMany(stack => reservations.GetLeasesForStack(new ItemStackId(stack.StackId)))
        .Where(lease => lease.ownerCharacterId == report.actorId && lease.purpose == ItemReservationPurpose.PersonalConsumption
            && lease.ownerOperationId.StartsWith("safe-drink:" + report.actorId + ":", StringComparison.Ordinal))
        .GroupBy(lease => lease.leaseId).Select(group => group.First()).SingleOrDefault();

    private void VerifyMalformedMealOwners(string stage, string sourceJson)
    {
        var source = saves.FromJson(sourceJson);
        var characterData = DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(source.sections
            .Single(value => value.sectionId == CharacterWorldSaveSection.Id).payloadJson);
        string otherActor = characterData.actors.First(value => value.persistentId != report.actorId).persistentId;
        var facilityData = DungeonSaveJson.Deserialize<ModularFacilityWorldSaveData>(source.sections
            .Single(value => value.sectionId == ModularFacilityWorldSaveSection.Id).payloadJson);
        string otherFacility = facilityData.buildings.First(value => value.persistentInstanceId != report.facilityId).persistentInstanceId;
        string operation = report.mealOperationId;
        var original = DungeonSaveJson.Deserialize<DungeonCharacterConsumablesSaveData>(source.sections
            .Single(value => value.sectionId == CharacterConsumablesSaveSection.Id).payloadJson);
        bool active = original.activeMealPlans.Any(value => value.planId == operation);
        bool completed = original.completedOperations.Any(value => value.operationId == operation);
        bool failed = original.failedMealOperations.Any(value => value.operationId == operation);
        Require(active || failed, "Malformed meal witness requires an actual unfinished execution owner.");

        Reject("missing-receipt", (data, use) =>
        {
            data.activeMealPlans.RemoveAll(value => value.planId == operation);
            data.completedOperations.RemoveAll(value => value.operationId == operation);
            data.failedMealOperations.RemoveAll(value => value.operationId == operation);
        });
        Reject("duplicate-receipt", (data, use) =>
        {
            if (active) data.activeMealPlans.Add(data.activeMealPlans.Single(value => value.planId == operation));
            else data.failedMealOperations.Add(data.failedMealOperations.Single(value => value.operationId == operation));
        });
        Reject("wrong-actor", (data, use) =>
        {
            if (active) data.activeMealPlans.Single(value => value.planId == operation).characterId = otherActor;
            else data.failedMealOperations.Single(value => value.operationId == operation).characterId = otherActor;
        });
        Reject("wrong-facility", (data, use) =>
        {
            if (active) data.activeMealPlans.Single(value => value.planId == operation).facilityInstanceId = otherFacility;
            else data.failedMealOperations.Single(value => value.operationId == operation).facilityInstanceId = otherFacility;
        });
        Reject("wrong-execution-operation", (data, use) => use.mealOperationId = operation + ":qa-other");
        if (active)
            Reject("premature-success", (data, use) => { use.mealSucceeded = true; use.mealDisplayName = "QA premature meal"; });
        if (completed)
        {
            Reject("completed-wrong-actor", (data, use) =>
                data.completedOperations.Single(value => value.operationId == operation).characterId = otherActor);
            Reject("completed-not-meal", (data, use) =>
                data.completedOperations.Single(value => value.operationId == operation).meal = false);
        }
        if (failed)
        {
            Reject("failure-without-recovery-plan", (data, use) =>
                data.failedMealOperations.Single(value => value.operationId == operation).recoveryPending = true);
            Reject("failure-without-reason", (data, use) =>
                data.failedMealOperations.Single(value => value.operationId == operation).detail = string.Empty);
        }

        void Reject(string caseId, Action<DungeonCharacterConsumablesSaveData, FacilityUseExecutionState> mutate)
        {
            var invalid = saves.FromJson(sourceJson);
            var consumableSection = invalid.sections.Single(value => value.sectionId == CharacterConsumablesSaveSection.Id);
            var data = DungeonSaveJson.Deserialize<DungeonCharacterConsumablesSaveData>(consumableSection.payloadJson);
            var facilitySection = invalid.sections.Single(value => value.sectionId == ModularFacilityWorldSaveSection.Id);
            var facilities = DungeonSaveJson.Deserialize<ModularFacilityWorldSaveData>(facilitySection.payloadJson);
            var module = facilities.buildings.Single(value => value.persistentInstanceId == report.facilityId).stateModules
                .Single(value => value.moduleId == BuildingStateModuleIds.FacilityOperation);
            var facilityState = DungeonSaveJson.Deserialize<FacilityRuntimeState>(module.payload);
            var use = facilityState.activeUses.Single(value => value.actorId == report.actorId);
            mutate(data, use);
            consumableSection.payloadJson = JsonUtility.ToJson(data);
            module.payload = JsonUtility.ToJson(facilityState);
            facilitySection.payloadJson = JsonUtility.ToJson(facilities);
            // Reseal only this test copy: the owner validator must reject it, not a checksum mismatch.
            invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
            string prefix = "meal-owner-" + stage + "-" + caseId;
            WriteNew(prefix + "-invalid-world.json", JsonUtility.ToJson(invalid, true));
            var root = scope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
            var actor = CurrentActor(); var facility = CurrentFacility(); var action = actor.Brain.bestAction;
            var execution = facility.FindActiveUse(report.actorId);
            int revision = root.PublishedRestoreRevision;
            float scale = speed.Scale;
            var before = saves.Capture();
            bool accepted = saves.TryRestore(invalid, out var failure);
            var after = saves.Capture();
            var witness = new MealRestoreRejection
            {
                stage = stage, caseId = caseId, sections = before.sections.Count,
                rejectedForOwner = !accepted && failure.Errors.Any(value => value.Contains("Facility meal operation owner")),
                stateExact = StateSectionPayloads(before) == StateSectionPayloads(after),
                referencesExact = ReferenceEquals(actor, CurrentActor()) && ReferenceEquals(facility, CurrentFacility())
                    && ReferenceEquals(action, CurrentActor().Brain.bestAction)
                    && ReferenceEquals(execution, CurrentFacility().FindActiveUse(report.actorId)) && actor.gameObject.activeInHierarchy,
                rootExact = !root.IsRestoreStaging && root.PublishedRestoreRevision == revision && speed.Scale == scale,
                errors = failure.Errors.ToArray()
            };
            witness.passed = witness.rejectedForOwner && witness.stateExact && witness.referencesExact && witness.rootExact
                && witness.sections == scope.Container.Resolve<IDungeonSaveSectionRegistry>().OrderedSections.Count;
            report.mealRejectedRestores.Add(witness);
            WriteNew(prefix + "-witness.json", JsonUtility.ToJson(witness, true));
            Require(witness.passed, "Malformed meal owner changed the world or failed at the wrong boundary: "
                + prefix + "; " + string.Join(" | ", failure.Errors));
        }
    }

    private IEnumerator VerifyMealLatePublishRollback()
    {
        Require(MealPlan()?.phase == CharacterMealPlanPhase.EffectsPublished,
            "Meal clock rollback requires the real post-consumption ACK fault.");
        var clock = scope.Container.Resolve<IGameClock>();
        // Scale was set to zero inside this frame's coroutine. Unity's deltaTime
        // still belongs to the already-started frame, so other scheduled work
        // coroutines may finish that frame before the paused-frame control begins.
        WriteNew("meal-clock-pause-request-world.json", saves.ToJson(saves.Capture()));
        yield return null;
        Require(clock.IsPaused && clock.DeltaTime == 0f, "Meal rollback control requires a fully paused frame.");
        var controlBefore = saves.Capture();
        yield return null;
        var beforeSave = saves.Capture();
        WriteNew("meal-clock-paused-control-before-world.json", saves.ToJson(controlBefore));
        WriteNew("meal-clock-paused-control-after-world.json", saves.ToJson(beforeSave));
        WriteNew("meal-clock-paused-control.txt", "changedSections=" + string.Join(",", ChangedSectionIds(controlBefore, beforeSave)));
        Require(PausedFrameAuthority(controlBefore) == PausedFrameAuthority(beforeSave),
            "Paused meal control changed authority beyond existing RNG/cache stamps.");
        string liveJson = saves.ToJson(beforeSave);
        WriteNew("meal-clock-before-late-publish-world.json", liveJson);
        var earlier = saves.FromJson(eatingWorld);
        report.mealRollbackCandidateTimeline = DungeonSaveJson.Deserialize<DungeonCharacterConsumablesSaveData>(earlier.sections
            .Single(value => value.sectionId == CharacterConsumablesSaveSection.Id).payloadJson).timelineTime;
        report.mealRollbackLiveTimeline = consumables.Capture().timelineTime;
        Require(report.mealRollbackLiveTimeline - report.mealRollbackCandidateTimeline >= 3f,
            "Clock rollback must use genuinely different saved/live meal epochs.");
        float rawBefore = clock.Time;
        var root = scope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
        var official = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        var participants = scope.Container.Resolve<IEnumerable<IDungeonRestoreTransactionParticipant>>().ToArray();
        var fault = new LateRestoreFault("meal-clock");
        Require(participants.Length > 0 && participants.All(value => string.CompareOrdinal(value.ParticipantId, fault.ParticipantId) < 0),
            "Meal clock fault must follow every production participant.");
        var registry = new DungeonSaveSectionRegistry(official.OrderedSections, root, participants.Concat(new[] { fault }),
            scope.Container.Resolve<IEnumerable<IDungeonSaveRegistryPreflightValidator>>());
        var actor = CurrentActor(); var facility = CurrentFacility(); var action = actor.Brain.bestAction;
        var execution = facility.FindActiveUse(report.actorId);
        int revision = root.PublishedRestoreRevision;
        float scale = speed.Scale;
        var failure = new DungeonGameRestoreReport();
        bool accepted = registry.RestoreAll(earlier.sections, failure);
        var immediateAfter = saves.Capture();
        report.mealLatePublishRollbackAtomic = !accepted && fault.PublishAttempted && fault.RolledBack
            && failure.Errors.Count == 1 && failure.Errors[0].Contains(fault.FailureCode)
            && !root.IsRestoreStaging && root.PublishedRestoreRevision == revision && speed.Scale == scale
            && ReferenceEquals(actor, CurrentActor()) && ReferenceEquals(facility, CurrentFacility())
            && ReferenceEquals(action, actor.Brain.bestAction) && ReferenceEquals(execution, facility.FindActiveUse(report.actorId))
            && StateSectionPayloads(beforeSave) == StateSectionPayloads(immediateAfter);
        WriteNew("meal-clock-immediate-rollback-world.json", saves.ToJson(immediateAfter));
        WriteNew("meal-clock-late-rollback.txt", "accepted=" + accepted + "\natomic=" + report.mealLatePublishRollbackAtomic
            + "\nparticipants=" + participants.Length + "\nsections=" + official.OrderedSections.Count
            + "\ncandidateTimeline=" + report.mealRollbackCandidateTimeline + "\nliveTimeline=" + report.mealRollbackLiveTimeline
            + "\nfaultReached=" + fault.PublishAttempted + "\nfaultRolledBack=" + fault.RolledBack
            + "\nchangedSections=" + string.Join(",", ChangedSectionIds(beforeSave, immediateAfter))
            + "\nerrors=" + string.Join(" | ", failure.Errors));
        Require(report.mealLatePublishRollbackAtomic, "Earlier meal publish failure changed the live world or clock anchor.");
        yield return null;
        var afterTick = saves.Capture();
        report.mealPostRollbackStable = PausedFrameAuthority(beforeSave) == PausedFrameAuthority(afterTick)
            && ReferenceEquals(actor, CurrentActor()) && ReferenceEquals(action, actor.Brain.bestAction)
            && root.PublishedRestoreRevision == revision && speed.Scale == scale;
        WriteNew("meal-clock-after-late-publish-world.json", saves.ToJson(afterTick));
        Require(report.mealPostRollbackStable, "Meal rollback changed the live action/authority on the next paused frame.");
        // This branch resumes the original coroutine without a replan or successful restore.
        var rejectAck = batch.DebugRejectAcknowledgement;
        batch.DebugRejectAcknowledgement = null;
        speed.Scale = originalScale;
        float until = Time.realtimeSinceStartup + 35f;
        while (facility.FindActiveUse(report.actorId) != null && Time.realtimeSinceStartup < until) yield return null;
        speed.Scale = 0f;
        report.mealClockProgressExact = Mathf.Abs((consumables.Capture().timelineTime - report.mealRollbackLiveTimeline)
            - (clock.Time - rawBefore)) < 0.01f;
        var results = Results();
        report.mealRolledBackActionCompletedOnce = ReferenceEquals(actor, CurrentActor()) && MealPlan() == null
            && CompletedMeals() == 1 && FoodQuantity() == report.foodAfter && facility.FindActiveUse(report.actorId) == null
            && facility.FacilityState.completedUses == report.usesBefore + 1
            && results.Count == 1 && results[0].status == GameplayOutcomeStatus.Succeeded;
        WriteNew("meal-clock-original-completed-world.json", saves.ToJson(saves.Capture()));
        Require(report.mealClockProgressExact && report.mealRolledBackActionCompletedOnce,
            "Original post-physical meal did not finish once on its unchanged logical clock.");
        // Return to the actual earlier effects-published snapshot for the independent
        // successful-restore branch below. This rewind is QA branching, not one history.
        batch.DebugRejectAcknowledgement = rejectAck;
        Require(saves.TryRestore(saves.FromJson(liveJson), out var restored),
            "Independent effects-published branch restore failed: " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        WriteNew("meal-clock-effects-branch-restored-world.json", saves.ToJson(saves.Capture()));
    }

    private void RestoreCheckpoint(string stage)
    {
        CharacterActor actor = CurrentActor();
        Facility facility = CurrentFacility();
        string execution = JsonUtility.ToJson(facility.FindActiveUse(report.actorId));
        string consumed = JsonUtility.ToJson(consumables.Capture());
        string service = JsonUtility.ToJson(sessions.Capture());
        int quantity = FoodQuantity();
        var save = saves.Capture();
        var witness = new Checkpoint { stage = stage, sections = save.sections.Count };
        report.checkpoints.Add(witness);
        Require(witness.sections == scope.Container.Resolve<IDungeonSaveSectionRegistry>().OrderedSections.Count,
            "Whole-world section coverage mismatch.");
        string json = saves.ToJson(save);
        if (stage == "eating") eatingWorld = json;
        WriteNew(stage + "-world.json", json);
        VerifyMalformedMealOwners(stage, json);
        Require(saves.TryRestore(saves.FromJson(json), out var restored),
            "Whole-world " + stage + " restore failed: " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        witness.freshActor = !ReferenceEquals(actor, CurrentActor());
        witness.freshFacility = !ReferenceEquals(facility, CurrentFacility());
        witness.executionExact = execution == JsonUtility.ToJson(CurrentFacility().FindActiveUse(report.actorId));
        witness.consumptionExact = consumed == JsonUtility.ToJson(consumables.Capture());
        witness.sessionExact = service == JsonUtility.ToJson(sessions.Capture());
        witness.stockExact = quantity == FoodQuantity();
        WriteNew(stage + "-restored-world.json", saves.ToJson(saves.Capture()));
        Require(witness.freshActor && witness.freshFacility && witness.executionExact && witness.consumptionExact
            && witness.stockExact && witness.sessionExact, "Whole-world meal identity/progress/stock mismatch: " + stage);
        Require(CurrentActor().Brain.bestAction?.destination == CurrentFacility(), "Meal action not rebound to its saved facility.");
    }

    private string ReadHashedCheckpoint(string path)
    {
        report.sourceCheckpointPath = Path.GetFullPath(path);
        string qaRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA")) + Path.DirectorySeparatorChar;
        Require(Path.IsPathRooted(path) && report.sourceCheckpointPath.StartsWith(qaRoot, StringComparison.OrdinalIgnoreCase),
            "Cold source must be explicit QA evidence in this project.");
        byte[] bytes = File.ReadAllBytes(report.sourceCheckpointPath);
        using (var sha = SHA256.Create())
            report.sourceCheckpointSha256 = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        Require(string.Equals(report.sourceCheckpointSha256,
            Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_SHA256"), StringComparison.OrdinalIgnoreCase),
            "Cold checkpoint hash mismatch.");
        return Encoding.UTF8.GetString(bytes);
    }

    private void RestoreColdEatingCheckpoint(string path)
    {
        report.checkpointResume = true;
        report.scope = "Hashed actual D04/AIEat eating checkpoint restored in a fresh process with a younger raw clock; exact owner/timeline, remaining meal duration, pause, ACK failure, completed replay, normal disable and pre-physical failed-meal restore/replay. Malformed owner rejection and earlier-epoch late-publication rollback preserve the live world and original meal coroutine. Also AIDrink lease retirement/replan/rejected restore/late rollback/completed replay (not cold restore of a drinking checkpoint). Independent QA rewind branches, controlled resources/needs; not natural production, guest payment or training evidence.";
        eatingWorld = ReadHashedCheckpoint(path);
        var save = saves.FromJson(eatingWorld);
        var savedConsumables = DungeonSaveJson.Deserialize<DungeonCharacterConsumablesSaveData>(save.sections
            .Single(value => value.sectionId == CharacterConsumablesSaveSection.Id).payloadJson);
        Require(savedConsumables.version == DungeonCharacterConsumablesSaveData.CurrentVersion
            && savedConsumables.activeMealPlans.Count == 1, "Expected one current-schema actual meal plan.");
        var plan = savedConsumables.activeMealPlans.Single();
        Require(plan.phase == CharacterMealPlanPhase.Eating, "Cold checkpoint must precede food consumption.");
        report.actorId = plan.characterId;
        report.facilityId = plan.facilityInstanceId;
        report.mealOperationId = plan.planId;
        var savedWorld = DungeonSaveJson.Deserialize<ModularFacilityWorldSaveData>(save.sections
            .Single(value => value.sectionId == "world.facilities").payloadJson);
        var savedFacility = DungeonSaveJson.Deserialize<FacilityRuntimeState>(savedWorld.buildings
            .Single(value => value.persistentInstanceId == report.facilityId).stateModules
            .Single(value => value.moduleId == BuildingStateModuleIds.FacilityOperation).payload);
        var execution = savedFacility.activeUses.Single(value => value.actorId == report.actorId
            && value.mealOperationId == report.mealOperationId);
        report.operationId = execution.operationId;
        report.serviceSessionId = execution.serviceSessionId;
        report.usesBefore = savedFacility.completedUses;
        report.rawClockBeforeRestore = scope.Container.Resolve<IGameClock>().Time;
        report.savedTimeline = savedConsumables.timelineTime;
        report.remainingMealSeconds = (float)(plan.createdAt + 4d - report.savedTimeline);
        Require(report.rawClockBeforeRestore < plan.createdAt && report.remainingMealSeconds > 0f,
            "Cold test must rewind the actual raw clock while retaining positive meal time.");
        CharacterActor previousActor = world.Characters.Single(value => value.Identity.PersistentId == report.actorId);
        var previousFacility = world.Buildings.OfType<Facility>().FirstOrDefault(value =>
            value.RequirePersistentInstanceId().Value == report.facilityId);
        Require(saves.TryRestore(save, out var restored), "Cold meal restore failed: " + string.Join(" | ", restored.Errors));
        speed.Scale = 0f;
        var witness = new Checkpoint
        {
            stage = "cold-eating", sections = save.sections.Count,
            freshActor = !ReferenceEquals(previousActor, CurrentActor()),
            freshFacility = !ReferenceEquals(previousFacility, CurrentFacility()),
            executionExact = JsonUtility.ToJson(execution) == JsonUtility.ToJson(CurrentFacility().FindActiveUse(report.actorId)),
            consumptionExact = JsonUtility.ToJson(savedConsumables) == JsonUtility.ToJson(consumables.Capture()),
            sessionExact = JsonUtility.ToJson(DungeonSaveJson.Deserialize<ServiceRoomsSaveData>(save.sections
                .Single(value => value.sectionId == ServiceRoomsSaveSection.Id).payloadJson)) == JsonUtility.ToJson(sessions.Capture())
        };
        var restoredSave = saves.Capture();
        // Compare the physical authority payload itself, not just a derived quantity.
        witness.stockExact = save.sections.Single(value => value.sectionId == "items.physical").payloadJson
            == restoredSave.sections.Single(value => value.sectionId == "items.physical").payloadJson;
        report.checkpoints.Add(witness);
        Require(witness.sections == scope.Container.Resolve<IDungeonSaveSectionRegistry>().OrderedSections.Count
            && witness.freshActor && witness.freshFacility && witness.executionExact
            && witness.consumptionExact && witness.sessionExact && witness.stockExact,
            "Cold meal restore changed the execution, timeline, session or physical state.");
        Require(CurrentActor().Brain.bestAction?.destination == CurrentFacility(), "Cold meal action not rebound.");
        report.foodBefore = FoodQuantity();
        report.hungerBefore = CurrentActor().stats[CharacterCondition.HUNGER];
        WriteNew("cold-eating-restored-world.json", saves.ToJson(restoredSave));
        VerifyMalformedMealOwners("eating", saves.ToJson(restoredSave));
    }

    private Facility PrepareDiningFacility(Grid grid)
    {
        BuildingSO definition = Resources.Load<BuildingSO>("SO/Building/Modular/D04_배식카운터");
        Require(definition != null, "Authored D04 dining facility missing.");
        Facility existing = world.Buildings.OfType<Facility>().FirstOrDefault(value => !value.isDestroy && value.BuildingData == definition);
        if (existing != null) return existing;
        RoomLayout layout = scope.Container.Resolve<IRoomLayoutCache>().GetLayout(grid);
        foreach (RoomInstance room in layout.Rooms.Where(value => value != null && value.IsUsable).OrderByDescending(value => value.Cells.Count))
        {
            var cells = new HashSet<Vector2Int>(room.Cells);
            foreach (Vector2Int cell in room.Cells.OrderBy(value => value.x).ThenBy(value => value.y))
            {
                if (definition.GetGridPosList(cell).Any(position => !cells.Contains(position)
                    || grid.GetGridCell(position) == null || !grid.GetGridCell(position).CanBuildInArea(definition)
                    || grid.GetGridCell(position).HasOccupantInLayer(definition.Placement.Layer))) continue;
                Require(scope.Container.Resolve<IDungeonGridBuildingControllerProvider>().Controller.TryPlaceInitialBuildings(
                    new[] { new InitialBuildInfo { Building = definition, Position = cell } }, out string failure), failure);
                scope.Container.Resolve<IRoomLayoutCache>().Clear();
                return world.Buildings.OfType<Facility>().Single(value => value.BuildingData == definition && value.buildPoses.Contains(cell));
            }
        }
        throw new InvalidOperationException("No real room permits authored D04 placement.");
    }

    private CharacterActor CurrentActor() => world.Characters.Single(value => value != null && value.Identity.PersistentId == report.actorId);
    private Facility CurrentFacility() => world.Buildings.OfType<Facility>().Single(value => value.PersistentInstanceId.Value == report.facilityId);
    private CharacterMealPlanSaveData MealPlan() => consumables.Capture().activeMealPlans.SingleOrDefault(value => value.planId == report.mealOperationId);
    private int CompletedMeals() => consumables.Capture().completedOperations.Count(value => value.operationId == report.mealOperationId
        && value.meal && value.characterId == report.actorId && value.facilityInstanceId == report.facilityId);
    private int FoodQuantity() => items.GetAllStacks().Where(value => value.ItemId == FoodId).Sum(value => value.Quantity);
    private List<GameplayOutcomeSnapshot> Results()
        => QueryResults(value => value.facts.Any(fact => fact.factId == MigratedProducerOutcomeIds.OperationFact.Value
            && fact.value == report.operationId));

    private List<GameplayOutcomeSnapshot> QueryResults(Func<GameplayOutcomeSnapshot, bool> predicate)
    {
        // Facility visit identity is a domain receipt key, not the migrated
        // ledger's hashed operation ID. Join the exact persisted source fact.
        var result = new List<GameplayOutcomeSnapshot>();
        var query = scope.Container.Resolve<IGameplayOutcomeQuery>();
        OutcomeCursor cursor = OutcomeCursor.FirstPage(200);
        while (true)
        {
            GameplayOutcomeQueryPage page = query.GetGlobal(cursor, OutcomeFilter.All);
            if (page.Items.Count == 0) break;
            result.AddRange(page.Items.Where(value => value.Exact != null && predicate(value.Exact)).Select(value => value.Exact));
            cursor = page.NextCursor;
        }
        return result;
    }
    private void WriteNew(string name, string contents)
    {
        using var stream = new FileStream(Path.Combine(output, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contents);
    }
    private void CaptureLog(string message, string trace, LogType type)
    {
        if (con03FaultsActive && type == LogType.Log
            && message.StartsWith("character-consumables-meal-live-observers-retired:recipient-retired:", StringComparison.Ordinal))
            report.con03RetiredObserverNotices++;
        if (con03FaultsActive && type == LogType.Error)
        {
            if (message == "character-consumables-meal-effects-failed:qa-con03-meal-effects-rejected")
            { report.expectedCon03EffectRejections++; return; }
            if (message == "character-consumables-fact-publication-rejected:debug-meal-publication-rejection")
            { report.expectedCon03ParentRejections++; return; }
        }
        if (type == LogType.Warning) report.warnings.Add(message);
        else if (type is LogType.Error or LogType.Exception or LogType.Assert) report.errors.Add(message + "\n" + trace);
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
}
#endif
