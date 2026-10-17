#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>Observe normal AI; restore the official whole world at an actual service yield.</summary>
public sealed class FacilityUseContinuationPlayModeRunner
{
    [Serializable]
    private sealed class Report
    {
        public string scope = "GameplayScene/start-party fixture; natural AI facility use; official whole-world restore";
        public string selectionOrigin = "natural-ai";
        public bool passed, freshActor, freshFacility, remainingExact, sessionExact, completedOnce, replayPreserved, exactParentBound;
        public bool fullAuditClosure, humanApprovalClaimed, trainingEligible, networkSent;
        public bool checkpointResume;
        public float rawClockBeforeRestore, savedServiceTimeline;
        public string sourceCheckpointPath, sourceCheckpointSha256;
        public int sections, dispatches, usesBefore, usesAfter;
        public string actorId, facilityId, operationId, serviceSessionId, failure;
        public float elapsedSeconds;
        public FacilityUseExecutionState before, afterRestore;
        public List<RejectionWitness> rejectedRestores = new();
        public List<TerminationWitness> terminatedUses = new();
        public List<GameplayOutcomeSnapshot> results = new();
        public List<string> warnings = new(), errors = new();
    }

    [Serializable] private sealed class RejectionWitness
    {
        public string caseId, reason, beforeSeal, afterSeal;
        public bool rejected, reasonMatched, worldUnchanged, referencesUnchanged, executionUnchanged;
        public List<string> changedSections = new();
    }

    [Serializable] private sealed class TerminationWitness
    {
        public string caseId;
        public bool stopped, noRecovery, cancelledParent, repeatedCleanupExact, restoredExact;
        public List<GameplayOutcomeSnapshot> results = new();
    }

    [Serializable] private sealed class Observation
    {
        public float gameTime, scale;
        public ServiceRoomsSaveData services;
        public List<ActorObservation> actors = new();
    }
    [Serializable] private sealed class ActorObservation
    {
        public FacilityUseVisitorState state;
        public string action, destination;
        public bool shopping;
    }

    private readonly Report report = new();
    private DungeonRuntimeLifetimeScope scope;
    private IDungeonGameSaveService saves;
    private IGameTimeScaleController timeScale;
    private IBuildingWorldQuery buildings;
    private IServiceSessionRuntime sessions;
    private IGameplayOutcomeQuery outcomes;
    private CharacterSpawner spawner;
    private bool spawnerWasPaused;
    private float originalScale;
    private string output;
    private CharacterActor requestedActor;

    public FacilityUseContinuationPlayModeRunner(IEnumerable<string> warnings, IEnumerable<string> errors)
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
            object next = null;
            bool moved;
            try
            {
                Require(Time.realtimeSinceStartup - started < 240f, "Bounded live witness did not finish within 240 real seconds.");
                Require(report.errors.Count == 0, "Runtime error invalidated the live witness: " + report.errors.FirstOrDefault());
                moved = stack.Peek().MoveNext();
                if (moved) next = stack.Peek().Current;
            }
            catch (Exception exception)
            {
                report.failure = exception.ToString();
                break;
            }
            if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator child) stack.Push(child);
            else yield return next;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        report.elapsedSeconds = Time.realtimeSinceStartup - started;
        bool continuationPassed = report.freshActor && report.freshFacility && report.remainingExact && report.sessionExact
            && report.completedOnce && report.replayPreserved && !report.networkSent;
        bool normalContinuationPassed = continuationPassed
            && report.rejectedRestores.Count == 7 && report.rejectedRestores.All(value => value.rejected
                && value.reasonMatched && value.worldUnchanged && value.referencesUnchanged && value.executionUnchanged);
        report.passed = string.IsNullOrEmpty(report.failure) && report.errors.Count == 0 && !report.networkSent
            && (report.checkpointResume ? continuationPassed : normalContinuationPassed)
            && report.terminatedUses.Count == 3 && report.terminatedUses.All(value => value.stopped
                && value.noRecovery && value.cancelledParent && value.repeatedCleanupExact && value.restoredExact);
        if (timeScale != null) timeScale.Scale = originalScale;
        if (spawner != null) spawner.ConfigureDeterministicSimulationForDiagnostics(paused: spawnerWasPaused);
        WriteNew("report.json", JsonUtility.ToJson(report, true));
        Application.logMessageReceived -= CaptureLog;
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(report.passed ? 0 : 1);
    }

    private IEnumerator Verify()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Only the isolated owned PlayMode entry is permitted.");
        RetailOperationProficiencyDebugScenarios.RunAll();
        float readyUntil = Time.realtimeSinceStartup + 45f;
        while (scope == null && Time.realtimeSinceStartup < readyUntil)
        {
            scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(value => value != null && value.Container != null);
            if (scope == null) yield return null;
        }
        Require(scope != null, "Official world scope is unavailable.");
        if (scope.Container.Resolve<ICharacterSpawnerProvider>().TryGetSpawner(out spawner))
        {
            spawnerWasPaused = spawner.DeterministicSimulationPausedForDiagnostics;
            spawner.ConfigureDeterministicSimulationForDiagnostics(paused: true);
        }
        var queue = scope.Container.Resolve<ILocalLlmRuntimeProvider>().GetRequiredRuntime() as LocalLlmRequestQueue;
        Require(queue != null, "Capture-only LLM queue unavailable.");
        queue.ConfigureIsolatedRequestCapture(request =>
        {
            report.dispatches++;
            report.networkSent |= request.NetworkSent || !request.CaptureOnly;
        });
        if (!scope.Container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out OwnerRunManager manager)
            || manager.CurrentOwnerActor == null)
        {
            StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            for (int frame = 0; frame < 10; frame++) yield return null;
        }
        Require(StartPartyPreparationPlayModeVerifier.TryReconcileTierZeroForDirectGameplayFixture(scope, out string setup), setup);
        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        sessions = scope.Container.Resolve<IServiceSessionRuntime>();
        outcomes = scope.Container.Resolve<IGameplayOutcomeQuery>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        originalScale = timeScale.Scale;
        Require(originalScale > 0, "Initial game is paused; do not override user/game speed.");
        string resumePath = Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD");
        if (!string.IsNullOrWhiteSpace(resumePath))
        {
            Require(Environment.GetEnvironmentVariable("DUNGEONSTORY_FACILITY_CONTINUATION_REST_REQUEST") != "1",
                "Saved termination branches must not also request a new rest action.");
            timeScale.Scale = 0;
            string resumedCheckpoint = ReadTerminationCheckpoint(resumePath);
            yield return VerifyCheckpointCompletion(resumedCheckpoint);
            yield return VerifyTerminationBranches(resumedCheckpoint);
            yield break;
        }
        if (Environment.GetEnvironmentVariable("DUNGEONSTORY_FACILITY_CONTINUATION_REST_REQUEST") == "1")
        {
            report.selectionOrigin = "controlled-public-off-duty-and-AI-rest-request";
            report.scope = "GameplayScene/start-party fixture; public off-duty command and one AI rest preference request; real action/movement/admission and official whole-world restore; not natural selection proof";
            Require(scope.Container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out manager)
                && manager.CurrentOwnerActor?.Brain != null, "Rest request has no live owner recipient.");
            requestedActor = manager.CurrentOwnerActor;
            Require(requestedActor.TryGetAbility(out AbilityWork duty), "Rest request needs the ordinary work-duty command.");
            duty.SetDutyState(AbilityWork.DutyState.OffDuty);
            requestedActor.Brain.RequestImmediateReplanForAction<AIRest>();
        }

        Facility facility = null;
        FacilityUseExecutionState progress = null;
        float observeUntil = Time.realtimeSinceStartup + 120f;
        float nextObservation = Time.realtimeSinceStartup;
        int observation = 0;
        while (Time.realtimeSinceStartup < observeUntil)
        {
            Require(requestedActor == null || !requestedActor.Brain.FirstPreferredActionHardFailure.HasFailure,
                "Rest request rejected: " + requestedActor?.Brain.FirstPreferredActionHardFailure.ToString());
            foreach (Facility candidate in buildings.Buildings.OfType<Facility>())
            {
                progress = candidate.FacilityState.activeUses.FirstOrDefault(value =>
                    value.phase == FacilityUseExecutionPhase.Service && value.timerInitialized
                    && value.remainingSeconds > 1f && value.remainingSeconds < value.serviceSeconds
                    && !string.IsNullOrEmpty(value.actionType) && !string.IsNullOrEmpty(value.serviceSessionId));
                if (progress != null) { facility = candidate; break; }
            }
            if (facility != null) break;
            if (Time.realtimeSinceStartup >= nextObservation)
            {
                var sample = new Observation
                {
                    gameTime = scope.Container.Resolve<IGameClock>().Time,
                    scale = timeScale.Scale,
                    services = sessions.Capture(),
                    actors = scope.Container.Resolve<ICharacterWorldQuery>().Characters
                        .Where(value => value != null && value.BuildingVisitor != null)
                        .Select(value => new ActorObservation
                        {
                            state = FacilityUseVisitorState.Capture(value.BuildingVisitor.VisitorSnapshot),
                            action = value.Brain?.bestAction?.actionset?.GetType().FullName ?? string.Empty,
                            destination = value.Brain?.bestAction?.destination?.BuildingData?.objectName ?? string.Empty,
                            shopping = value.TryGetAbility(out AbilityShopping shopping) && shopping.HasActiveShoppingRoutineForDiagnostics
                        }).ToList()
                };
                WriteNew($"observation-{++observation:D3}.json", JsonUtility.ToJson(sample, true));
                nextObservation = Time.realtimeSinceStartup + 20f;
            }
            yield return null;
        }
        Require(facility != null && progress != null, "No facility service reached a saveable yield within observation bound: " + report.selectionOrigin);
        timeScale.Scale = 0;
        report.before = progress.Clone();
        report.actorId = progress.actorId;
        report.facilityId = facility.RequirePersistentInstanceId().Value;
        report.operationId = progress.operationId;
        report.serviceSessionId = progress.serviceSessionId;
        report.usesBefore = facility.FacilityState.completedUses;
        CharacterActor oldActor = scope.Container.Resolve<ICharacterWorldQuery>().Characters
            .Single(value => value.Identity?.PersistentId == report.actorId);
        Require(oldActor.Brain.bestAction?.actionset?.GetType().FullName == progress.actionType,
            "Natural action identity differs from the captured execution.");
        Require(sessions.ActiveSessions.Any(value => value.SessionId == progress.serviceSessionId), "Execution lost its active service.");
        string sessionBefore = JsonUtility.ToJson(sessions.Capture());
        DungeonGameSaveData checkpoint = saves.Capture();
        report.sections = checkpoint.sections.Count;
        Require(report.sections == scope.Container.Resolve<IDungeonSaveSectionRegistry>().OrderedSections.Count,
            "Whole-world section coverage mismatch.");
        string json = saves.ToJson(checkpoint);
        WriteNew("active-world.json", json);
        WriteNew("active-execution.json", JsonUtility.ToJson(report.before, true));
        VerifyRejectedRestores(json, oldActor, facility);
        Require(saves.TryRestore(saves.FromJson(json), out DungeonGameRestoreReport restored),
            "Official active-world restore failed: " + string.Join(" | ", restored.Errors));
        var characterSaves = scope.Container.Resolve<ICharacterWorldSaveService>();
        Require(characterSaves.TryGetRestoredActor(report.actorId, out CharacterActor actor), "Restored actor not indexed.");
        Facility replacement = buildings.Buildings.OfType<Facility>()
            .Single(value => value.RequirePersistentInstanceId().Value == report.facilityId);
        report.freshActor = !ReferenceEquals(oldActor, actor);
        report.freshFacility = !ReferenceEquals(facility, replacement);
        FacilityUseExecutionState resumed = replacement.FacilityState.activeUses.Single(value => value.operationId == report.operationId);
        report.afterRestore = resumed.Clone();
        report.remainingExact = JsonUtility.ToJson(report.before) == JsonUtility.ToJson(report.afterRestore);
        report.sessionExact = sessionBefore == JsonUtility.ToJson(sessions.Capture());
        Require(report.freshActor && report.freshFacility && report.remainingExact && report.sessionExact,
            "Restore replaced execution identity/timer/session or retained old scene owners.");
        Require(actor.Brain.bestAction?.destination == replacement,
            "Restored actor was not automatically rebound to its saved facility.");
        WriteNew("restored-world.json", saves.ToJson(saves.Capture()));
        yield return VerifyCompletion(replacement);
        yield return VerifyTerminationBranches(json);
    }

    private IEnumerator VerifyCheckpointCompletion(string json)
    {
        DungeonGameSaveData checkpoint = saves.FromJson(json);
        ServiceRoomsSaveData savedSessions = DungeonSaveJson.Deserialize<ServiceRoomsSaveData>(checkpoint.sections
            .Single(value => value.sectionId == ServiceRoomsSaveSection.Id).payloadJson);
        report.rawClockBeforeRestore = scope.Container.Resolve<IGameClock>().Time;
        report.savedServiceTimeline = savedSessions.timelineTime;
        Require(report.rawClockBeforeRestore < savedSessions.sessions
                .Single(value => value.sessionId == report.serviceSessionId).startedAt,
            "Cold-clock regression witness requires the new process clock to precede the original session start.");
        CharacterActor oldActor = scope.Container.Resolve<ICharacterWorldQuery>().Characters
            .SingleOrDefault(value => value.Identity?.PersistentId == report.actorId);
        Facility oldFacility = buildings.Buildings.OfType<Facility>()
            .SingleOrDefault(value => value.RequirePersistentInstanceId().Value == report.facilityId);
        Require(saves.TryRestore(checkpoint, out DungeonGameRestoreReport restored),
            "Cold active-world restore failed: " + string.Join(" | ", restored.Errors));
        Require(scope.Container.Resolve<ICharacterWorldSaveService>().TryGetRestoredActor(report.actorId, out CharacterActor actor),
            "Cold restored actor not indexed.");
        Facility replacement = buildings.Buildings.OfType<Facility>()
            .Single(value => value.RequirePersistentInstanceId().Value == report.facilityId);
        report.freshActor = !ReferenceEquals(oldActor, actor);
        report.freshFacility = !ReferenceEquals(oldFacility, replacement);
        report.afterRestore = replacement.FacilityState.activeUses.Single(value => value.operationId == report.operationId).Clone();
        report.remainingExact = JsonUtility.ToJson(report.before) == JsonUtility.ToJson(report.afterRestore);
        report.sessionExact = JsonUtility.ToJson(savedSessions) == JsonUtility.ToJson(sessions.Capture());
        report.usesBefore = replacement.FacilityState.completedUses;
        Require(report.freshActor && report.freshFacility && report.remainingExact && report.sessionExact
            && actor.Brain.bestAction?.destination == replacement
            && actor.GetAbility<AbilityShopping>().HasActiveShoppingRoutineForDiagnostics,
            "Cold restore changed execution/session or did not rebind its actual action.");
        WriteNew("restored-world.json", saves.ToJson(saves.Capture()));
        yield return VerifyCompletion(replacement);
    }

    private IEnumerator VerifyCompletion(Facility replacement)
    {
        timeScale.Scale = originalScale;
        float completionUntil = Time.realtimeSinceStartup + 120f;
        while (Time.realtimeSinceStartup < completionUntil
            && replacement.FacilityState.activeUses.Any(value => value.operationId == report.operationId))
            yield return null;
        timeScale.Scale = 0;
        report.usesAfter = replacement.FacilityState.completedUses;
        report.results = ReadOperationResults();
        string completed = saves.ToJson(saves.Capture());
        WriteNew("completion-observation-world.json", completed);
        WriteNew("completion-operation-results.json", JsonUtility.ToJson(new ResultSet { items = report.results }, true));
        GameplayOutcomeSnapshot parent = report.results.Count == 1 ? report.results[0] : null;
        report.exactParentBound = parent != null && parent.status == GameplayOutcomeStatus.Succeeded
            && parent.subjects.Any(value => value.entityKindId == "character" && value.entityId == report.actorId)
            && parent.subjects.Any(value => value.entityKindId == "facility" && value.entityId == report.facilityId)
            && parent.facts.Any(value => value.factId == MigratedProducerOutcomeIds.ServiceSessionFact.Value
                && value.value == report.serviceSessionId)
            && parent.facts.Any(value => value.factId == MigratedProducerOutcomeIds.OutcomeStatusFact.Value
                && value.value == CharacterFacilityUseOutcomeStatus.Completed.ToString())
            && parent.metrics.Any(value => value.metricId == MigratedProducerOutcomeIds.SleepBeforeMetric.Value
                && value.referenceId == report.actorId && value.value == report.before.before.sleep);
        report.completedOnce = report.usesAfter == report.usesBefore + 1
            && !replacement.FacilityState.activeUses.Any(value => value.operationId == report.operationId)
            && !sessions.ActiveSessions.Any(value => value.SessionId == report.serviceSessionId)
            && report.exactParentBound;
        Require(report.completedOnce, "Restored AI did not finish exactly one original facility use with its exact parent.");
        WriteNew("completed-world.json", completed);
        string resultJson = JsonUtility.ToJson(new ResultSet { items = report.results });
        Require(saves.TryRestore(saves.FromJson(completed), out DungeonGameRestoreReport restored),
            "Completed-world restore failed: " + string.Join(" | ", restored.Errors));
        yield return null;
        yield return null;
        report.replayPreserved = resultJson == JsonUtility.ToJson(new ResultSet { items = ReadOperationResults() })
            && !sessions.ActiveSessions.Any(value => value.SessionId == report.serviceSessionId);
        Require(report.replayPreserved, "Completed-world restore duplicated/lost the original result or revived its session.");
        WriteNew("completed-restored-world.json", saves.ToJson(saves.Capture()));
    }

    private string ReadTerminationCheckpoint(string path)
    {
        report.checkpointResume = true;
        report.scope = "Explicitly hashed actual GameplayScene checkpoint restored in a fresh process: exact execution/session, actual completion/replay and three termination branches; prior rejected-restore tests are NOT repeated or inherited";
        report.selectionOrigin = "saved-actual-facility-execution";
        report.sourceCheckpointPath = Path.GetFullPath(path);
        string qaRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA")) + Path.DirectorySeparatorChar;
        Require(Path.IsPathRooted(path) && report.sourceCheckpointPath.StartsWith(qaRoot, StringComparison.OrdinalIgnoreCase),
            "Only an explicitly selected checkpoint in this project's QA evidence is permitted.");
        byte[] bytes = File.ReadAllBytes(report.sourceCheckpointPath);
        using (var sha = SHA256.Create())
            report.sourceCheckpointSha256 = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        Require(string.Equals(report.sourceCheckpointSha256,
                Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_SHA256"), StringComparison.OrdinalIgnoreCase),
            "Selected termination checkpoint hash mismatch.");
        string json = Encoding.UTF8.GetString(bytes);
        DungeonGameSaveData checkpoint = saves.FromJson(json);
        report.sections = checkpoint.sections.Count;
        var world = DungeonSaveJson.Deserialize<ModularFacilityWorldSaveData>(checkpoint.sections
            .Single(value => value.sectionId == "world.facilities").payloadJson);
        var targets = world.buildings.SelectMany(building => building.stateModules
            .Where(module => module.moduleId == BuildingStateModuleIds.FacilityOperation)
            .SelectMany(module => DungeonSaveJson.Deserialize<FacilityRuntimeState>(module.payload).activeUses
                .Where(use => use.phase == FacilityUseExecutionPhase.Service && use.timerInitialized
                    && !use.effectsCommitted && !string.IsNullOrEmpty(use.serviceSessionId))
                .Select(use => (building, use)))).ToList();
        Require(targets.Count == 1, "Termination checkpoint must identify exactly one active pre-effect service.");
        report.before = targets[0].use.Clone();
        report.actorId = report.before.actorId;
        report.operationId = report.before.operationId;
        report.serviceSessionId = report.before.serviceSessionId;
        report.facilityId = targets[0].building.persistentInstanceId;
        return json;
    }

    private IEnumerator VerifyTerminationBranches(string activeWorld)
    {
        // Independent branches of the same real, pre-effect execution. Restoring
        // this checkpoint is explicit test control, never new gameplay history.
        foreach (string caseId in new[] { "cancel-action", "disable-shopping", "destroy-facility" })
        {
            Require(saves.TryRestore(saves.FromJson(activeWorld), out DungeonGameRestoreReport restored),
                "Termination branch restore failed: " + string.Join(" | ", restored.Errors));
            Require(scope.Container.Resolve<ICharacterWorldSaveService>().TryGetRestoredActor(report.actorId, out CharacterActor actor),
                "Termination branch actor missing.");
            Facility facility = buildings.Buildings.OfType<Facility>()
                .Single(value => value.RequirePersistentInstanceId().Value == report.facilityId);
            AbilityShopping shopping = actor.GetAbility<AbilityShopping>();
            Require(facility.FindActiveUse(report.actorId)?.effectsCommitted == false
                && shopping.HasActiveShoppingRoutineForDiagnostics, "Termination branch is not an active pre-effect use.");
            BuildingVisitorSnapshot before = actor.BuildingVisitor.VisitorSnapshot;
            int uses = facility.FacilityState.completedUses;
            if (caseId == "cancel-action")
                Require(actor.Brain.StopCurrentActionForReplan("qa-facility-use-cancel"),
                    "Public action cancellation rejected the current action.");
            else if (caseId == "disable-shopping") shopping.enabled = false;
            else facility.DestroySelf();

            BuildingVisitorSnapshot after = actor.BuildingVisitor.VisitorSnapshot;
            var witness = new TerminationWitness
            {
                caseId = caseId,
                stopped = !shopping.HasActiveShoppingRoutineForDiagnostics
                    && facility.FacilityState.activeUses.Count == 0
                    && !sessions.ActiveSessions.Any(value => value.SessionId == report.serviceSessionId),
                noRecovery = facility.FacilityState.completedUses == uses && before.Sleep == after.Sleep
                    && before.CurrentHealth == after.CurrentHealth && before.InjurySeverity == after.InjurySeverity,
                results = ReadOperationResults()
            };
            GameplayOutcomeSnapshot parent = witness.results.Count == 1 ? witness.results[0] : null;
            witness.cancelledParent = parent != null && parent.status == GameplayOutcomeStatus.Cancelled
                && parent.subjects.Any(value => value.entityKindId == "character" && value.entityId == report.actorId)
                && parent.subjects.Any(value => value.entityKindId == "facility" && value.entityId == report.facilityId)
                && parent.facts.Any(value => value.factId == MigratedProducerOutcomeIds.ServiceSessionFact.Value
                    && value.value == report.serviceSessionId)
                && parent.facts.Any(value => value.factId == MigratedProducerOutcomeIds.OutcomeStatusFact.Value
                    && value.value == CharacterFacilityUseOutcomeStatus.Cancelled.ToString());
            string resultJson = JsonUtility.ToJson(new ResultSet { items = witness.results });
            shopping.StopShopping("qa-repeat-cleanup");
            if (caseId == "destroy-facility") facility.DestroySelf();
            witness.repeatedCleanupExact = resultJson == JsonUtility.ToJson(new ResultSet { items = ReadOperationResults() });
            report.terminatedUses.Add(witness);
            WriteNew("termination-" + caseId + "-observed.json", JsonUtility.ToJson(witness, true));
            Require(witness.stopped && witness.noRecovery && witness.cancelledParent && witness.repeatedCleanupExact,
                "Termination lost/doubled the original result, recovered needs, or retained execution: " + caseId);
            string terminalWorld = saves.ToJson(saves.Capture());
            WriteNew("termination-" + caseId + "-world.json", terminalWorld);
            Require(saves.TryRestore(saves.FromJson(terminalWorld), out restored),
                "Terminated-world restore failed: " + caseId + "; " + string.Join(" | ", restored.Errors));
            yield return null;
            yield return null;
            witness.restoredExact = resultJson == JsonUtility.ToJson(new ResultSet { items = ReadOperationResults() })
                && !sessions.ActiveSessions.Any(value => value.SessionId == report.serviceSessionId)
                && !buildings.Buildings.OfType<Facility>().Any(value => value.FacilityState.activeUses
                    .Any(use => use.operationId == report.operationId));
            WriteNew("termination-" + caseId + "-restored.json", JsonUtility.ToJson(witness, true));
            Require(witness.restoredExact, "Terminated-world restore revived or duplicated its use: " + caseId);
        }
    }

    private void VerifyRejectedRestores(string originalJson, CharacterActor liveActor, Facility liveFacility)
    {
        // Reseal only test copies: semantic validation must reject them, not just
        // checksum validation. No frame/time advance is permitted between captures.
        Verify("missing-actor", state =>
        {
            state.activeUses[0].actorId = "qa:missing-facility-actor";
            state.activeUses[0].before.actorId = "qa:missing-facility-actor";
        }, "Service execution owner mismatch");
        Verify("unknown-action", state => state.activeUses[0].actionType = "QaUnregisteredFacilityAction",
            "Invalid facility execution actor/action/operation");
        Verify("wrong-operation-owner", state => state.activeUses[0].operationId += ":wrong-owner",
            "Invalid facility execution actor/action/operation");
        Verify("missing-service", state => state.activeUses[0].serviceSessionId = "service:qa-missing",
            "Service execution owner mismatch");
        Verify("orphan-service", state => state.activeUses.Clear(), "no executable owner");
        Verify("duplicate-execution", state => state.activeUses.Add(state.activeUses[0].Clone()),
            "facility active use");
        Verify("negative-time", state => state.activeUses[0].remainingSeconds = -1f,
            "facility active use");

        void Verify(string caseId, Action<FacilityRuntimeState> corrupt, string expectedReason)
        {
            DungeonGameSaveData baseline = saves.Capture();
            AIAction action = liveActor.Brain.bestAction;
            var shopping = liveActor.GetAbility<AbilityShopping>();
            bool hadRoutine = shopping.HasActiveShoppingRoutineForDiagnostics;
            string execution = JsonUtility.ToJson(liveFacility.FindActiveUse(report.actorId));
            DungeonGameSaveData candidate = saves.FromJson(originalJson);
            DungeonSaveSectionEnvelope section = candidate.sections.Single(value => value.sectionId == "world.facilities");
            var world = DungeonSaveJson.Deserialize<ModularFacilityWorldSaveData>(section.payloadJson);
            BuildingStateModuleSaveData module = world.buildings.Single(value => value.persistentInstanceId == report.facilityId)
                .stateModules.Single(value => value.moduleId == BuildingStateModuleIds.FacilityOperation);
            var state = DungeonSaveJson.Deserialize<FacilityRuntimeState>(module.payload);
            Require(state.activeUses.Count == 1 && state.activeUses[0].operationId == report.operationId,
                "Rejection fixture must target exactly the observed active execution.");
            corrupt(state);
            module.payload = JsonUtility.ToJson(state);
            section.payloadJson = JsonUtility.ToJson(world);
            candidate.manifest = DungeonSaveManifest.Capture(candidate.sections);
            WriteNew("rejection-" + caseId + "-input.json", saves.ToJson(candidate));
            bool accepted = saves.TryRestore(candidate, out DungeonGameRestoreReport rejected);
            DungeonGameSaveData after = saves.Capture();
            string reason = string.Join(" | ", rejected.Errors);
            var witness = new RejectionWitness
            {
                caseId = caseId, rejected = !accepted, reason = reason,
                reasonMatched = reason.Contains(expectedReason, StringComparison.OrdinalIgnoreCase),
                beforeSeal = baseline.manifest.snapshotSha256, afterSeal = after.manifest.snapshotSha256,
                referencesUnchanged = scope.Container.Resolve<ICharacterWorldQuery>().Characters
                    .Any(value => ReferenceEquals(value, liveActor))
                    && buildings.Buildings.Any(value => ReferenceEquals(value, liveFacility)),
                executionUnchanged = ReferenceEquals(action, liveActor.Brain.bestAction)
                    && liveActor.Brain.bestAction?.destination == liveFacility && hadRoutine
                    && shopping.HasActiveShoppingRoutineForDiagnostics
                    && execution == JsonUtility.ToJson(liveFacility.FindActiveUse(report.actorId)),
                changedSections = baseline.sections.Where(before => !after.sections.Any(value =>
                    value.sectionId == before.sectionId && value.sectionVersion == before.sectionVersion
                    && value.optional == before.optional && value.payloadJson == before.payloadJson))
                    .Select(value => value.sectionId).ToList()
            };
            witness.worldUnchanged = baseline.sections.Count == after.sections.Count && witness.changedSections.Count == 0;
            report.rejectedRestores.Add(witness);
            WriteNew("rejection-" + caseId + "-witness.json", JsonUtility.ToJson(witness, true));
            if (!witness.worldUnchanged) WriteNew("rejection-" + caseId + "-changed-world.json", saves.ToJson(after));
            Require(witness.rejected && witness.reasonMatched && witness.worldUnchanged
                && witness.referencesUnchanged && witness.executionUnchanged,
                "Rejected restore changed the live world/action or failed for an unrelated reason: " + caseId + "; " + reason);
        }
    }

    [Serializable] private sealed class ResultSet { public List<GameplayOutcomeSnapshot> items; }
    private List<GameplayOutcomeSnapshot> ReadOperationResults()
    {
        var result = new List<GameplayOutcomeSnapshot>();
        OutcomeCursor cursor = OutcomeCursor.FirstPage(200);
        while (true)
        {
            GameplayOutcomeQueryPage page = outcomes.GetGlobal(cursor, OutcomeFilter.All);
            if (page.Items.Count == 0) break;
            result.AddRange(page.Items.Where(value => value.Exact?.outcomeTypeId == "service.facility-visited"
                && value.Exact.facts.Any(fact => fact.factId == MigratedProducerOutcomeIds.OperationFact.Value
                    && fact.value == report.operationId)).Select(value => value.Exact));
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
    private void CaptureLog(string text, string stack, LogType type)
    {
        if (type == LogType.Warning) report.warnings.Add(text);
        else if (type is LogType.Error or LogType.Exception or LogType.Assert) report.errors.Add(text + "\n" + stack);
    }
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
#endif
