#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VContainer;

public static class MovementOwnershipDebugScenarios
{
    private const string Pending = "DungeonStory.MovementOwnership.Pending";
    private const string Started = "DungeonStory.MovementOwnership.Started";
    private const string Prepared = "DungeonStory.MovementOwnership.Prepared";
    private const string FaultRecoverySelector = "DungeonStory.MovementOwnership.FaultRecoverySelector";
    private static IEnumerator verification;
    private static readonly List<string> rows = new();

    public static void RequestActualContainerForBatch()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Movement ownership verification requires isolated batch execution.");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "PlayMode already active.");
        CharacterOperationObservationDebugScenarios.Run(WriteNew);
        CharacterAiOwnershipDebugScenarios.Run(WriteNew);
        CharacterAiExecutionLedgerDebugScenarios.Run(WriteNew);
        VerifyPureOwners();
        RequestPlayContainer(string.Empty);
    }

    public static void RequestDestinationlessForBatch() => RequestStandaloneGroup("destinationless");
    public static void RequestDeprivationForBatch() => RequestStandaloneGroup("deprivation");
    public static void RequestPrimitiveForBatch() => RequestStandaloneGroup("primitive");
    public static void RequestRestockForBatch() => RequestStandaloneGroup("restock");
    public static void RequestMedicalTransferForBatch() => RequestStandaloneGroup("medical-transfer");
    public static void RequestWorkAccidentForBatch() => RequestStandaloneGroup("work-accident");
    public static void RequestFactionReinforcementForBatch() => RequestStandaloneGroup("faction-reinforcement");
    public static void RequestSkillTargetingForBatch() => RequestStandaloneGroup("skill-targeting");

    private static void RequestStandaloneGroup(string selector)
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "AI fault-group verification requires isolated batch execution.");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "PlayMode already active.");
        Require(selector is "destinationless" or "deprivation" or "primitive" or "restock" or "medical-transfer" or "work-accident" or "faction-reinforcement" or "skill-targeting", "Unsupported isolated verification group.");
        if (selector == "restock")
            WriteNew("work-failure-contracts.txt", WorkExecutionFailureMatrixDebugScenarios.RunForArchitectureBatch());
        RequestPlayContainer(selector);
    }

    private static void RequestPlayContainer(string selector)
    {
        rows.Clear();
        SessionState.SetString(FaultRecoverySelector, selector);
        SessionState.SetBool(Pending, true);
        SessionState.SetBool(Prepared, false);
        SessionState.SetString(Started, EditorApplication.timeSinceStartup.ToString(CultureInfo.InvariantCulture));
        verification = null;
        EditorSceneManager.OpenScene("Assets/Scenes/GameplayScene.unity", OpenSceneMode.Single);
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
    }

    private static void Pump()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        try
        {
            if (!EditorApplication.isPlaying) return;
            var scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(value => value?.Container != null);
            if (scope == null)
            {
                Require(EditorApplication.timeSinceStartup - double.Parse(SessionState.GetString(Started, string.Empty),
                    CultureInfo.InvariantCulture) < 60, "Actual movement container was not ready within 60 seconds.");
                return;
            }
            if (!SessionState.GetBool(Prepared, false))
            {
                var game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
                Require(game != null, "Actual game manager missing.");
                var managers = scope.Container.Resolve<IOwnerRunManagerProvider>();
                Require(managers.TryGetManager(out var manager), "Actual owner manager missing.");
                string preparation = manager.CurrentOwnerActor == null
                    ? StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug() : "owner-already-present";
                Require(manager.CurrentOwnerActor != null && (preparation == "owner-already-present"
                    || preparation.StartsWith("committed=True;", StringComparison.Ordinal)), "Official party preparation failed: " + preparation);
                // The official fast-preparation helper freezes Time.timeScale before committing.
                game.isPause = false;
                scope.Container.Resolve<IGameTimeScaleController>().Scale = 1;
                Time.timeScale = 1;
                WriteNew("movement-preparation.txt", "Controlled official starting-party command; not a training event.\n" + preparation);
                SessionState.SetBool(Prepared, true);
                return;
            }
            string selector = SessionState.GetString(FaultRecoverySelector, string.Empty);
            verification ??= selector.Length == 0
                ? VerifyActual(scope.Container)
                : selector == "restock"
                    ? VerifyRestock(scope)
                    : selector == "medical-transfer"
                        ? VerifyMedicalTransfer(scope)
                        : selector == "work-accident"
                            ? VerifyWorkAccident(scope)
                            : selector == "faction-reinforcement"
                                ? VerifyFactionReinforcement(scope)
                                : selector == "skill-targeting"
                                    ? VerifySkillTargeting(scope)
                                : VerifyStandaloneFaultRecovery(selector);
            if (!verification.MoveNext()) Finish(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            try { WriteNew("movement-ownership-failure.txt", exception.ToString()); }
            finally { Finish(1); }
        }
    }

    private static void VerifyPureOwners()
    {
        var checks = new List<string> { "case\tresult" };
        try
        {
            Check(checks, "operation-version-stale-completion-and-normalized-owner", () =>
            {
                var state = new CharacterMovementOperationRuntime();
                Require(state.Version == 0 && state.Owner == string.Empty && state.SystemDestination == null, "New operation state is not empty.");
                Require(state.TryBeginMovementOperation("first", out int first) && first == 1, "Initial movement did not begin.");
                Require(state.TryBeginMovementOperation("second", out int second) && second == 2, "Movement preemption did not advance version.");
                Require(state.LastMovementOperationPreemptionForDiagnostics == "first->second", "Preemption identity changed.");
                state.CompleteMovementOperation(first, "first");
                state.CompleteMovementOperation(second, "foreign");
                Require(state.Owner == "second", "Stale/foreign completion stole movement.");
                state.CompleteMovementOperation(second, "second");
                Require(state.Owner == string.Empty && state.TryBeginMovementOperation(null, out _)
                    && state.Owner == "unknown", "Exact completion or legacy owner normalization changed.");
            });
            Check(checks, "protected-rejection-and-retained-ownership-after-arrival", () =>
            {
                var state = new CharacterMovementOperationRuntime();
                state.BeginSystemMove(Vector2Int.right, DoorAccessOverrideKind.DirectCommand, true);
                Require(!state.TryBeginMovementOperation("raw-path", out int rejected) && rejected == 0
                    && state.Version == 0 && state.LastRejectedMovementOperationOwnerForDiagnostics == "raw-path",
                    "Rejected movement changed the protected operation.");
                Require(state.TryBeginMovementOperation("protected-system-command", out int accepted), "Protected owner was rejected.");
                state.CompleteMovementOperation(accepted, "protected-system-command");
                state.CompleteSystemMove();
                Require(state.SystemDestination == null && state.SystemOverride == DoorAccessOverrideKind.None
                    && state.HasRetainedProtection, "Arrival released retained protected ownership.");
            });
            Check(checks, "cancellation-phases-preserve-manual-and-system-callback-boundaries", () =>
            {
                var state = new CharacterMovementOperationRuntime();
                state.BeginManualMove(Vector2Int.left);
                state.BeginSystemMove(Vector2Int.right, DoorAccessOverrideKind.DirectCommand, true);
                state.BeginTrackedMovement(true);
                state.TryBeginMovementOperation("protected-system-command", out int before);
                state.NoteCancellation(true, "qa-cancel");
                state.InvalidateMovementOperation();
                state.AbandonOperation();
                Require(state.Version == before + 1 && !state.HasRetainedProtection && !state.ExitTransitOwnsMovement
                    && state.Owner == string.Empty && state.ManualDestination == Vector2Int.left
                    && state.SystemDestination == Vector2Int.right, "Cancellation changed pre-callback destinations.");
                Require(state.LastMovementCancellationSourceForDiagnostics == "qa-cancel"
                    && state.LastMovementOperationPreemptionForDiagnostics == "protected-system-command->cancel:qa-cancel", "Cancellation diagnostics changed.");
                state.CompleteManualMove();
                state.ClearSystemDestination();
                Require(state.ManualDestination == null && state.SystemDestination == null, "Cancellation destinations survived cleanup.");
            });
            Check(checks, "unretained-system-arrival-and-empty-cancel", () =>
            {
                var state = new CharacterMovementOperationRuntime();
                state.NoteCancellation(false, "empty");
                Require(state.LastMovementCancellationSourceForDiagnostics == string.Empty, "Empty cancellation overwrote diagnostics.");
                state.BeginSystemMove(Vector2Int.up, DoorAccessOverrideKind.None, false);
                state.CompleteSystemMove();
                Require(!state.HasRetainedProtection && state.SystemDestination == null
                    && state.TryBeginMovementOperation("raw-path", out _), "Ordinary system arrival retained ownership.");
            });
            Check(checks, "response-start-identity-and-conflicting-owner", () =>
            {
                var state = new SocietyResponseMovementProvenanceRuntime();
                Require(!state.CanStart(" bad ", "external", new BuildingInstanceId("building:qa-facility"), Vector2Int.right, out _), "Noncanonical response was admitted.");
                Require(state.CanStart("response", "external", new BuildingInstanceId("building:qa-facility"), Vector2Int.right, out _), "Valid response was rejected.");
                state.RecordStarted("response", "external", new BuildingInstanceId("building:qa-facility"), Vector2Int.right);
                Require(state.CanStart("response", "external", new BuildingInstanceId("building:qa-facility"), Vector2Int.right, out _)
                    && !state.CanStart("foreign", "external", new BuildingInstanceId("building:qa-facility"), Vector2Int.right, out _)
                    && !state.CanStart("response", "external", new BuildingInstanceId("building:qa-facility"), Vector2Int.left, out _), "Pending response retry/conflict behavior changed.");
            });
            Check(checks, "response-receipt-location-idempotence-and-conflict", () =>
            {
                var state = NewProvenance();
                Require(!state.TryRecordSocietyResponseMovementReceipt("response", "external", "receipt", Vector2Int.zero, out _)
                    && !state.TryRecordSocietyResponseMovementReceipt("response", "external", "receipt", null, out _)
                    && state.SocietyResponseMovementReceiptId == string.Empty, "Receipt recorded before arrival.");
                Require(state.TryRecordSocietyResponseMovementReceipt("response", "external", "receipt", Vector2Int.right, out _)
                    && state.TryRecordSocietyResponseMovementReceipt("response", "external", "receipt", Vector2Int.right, out _)
                    && !state.TryRecordSocietyResponseMovementReceipt("response", "external", "other", Vector2Int.right, out _)
                    && !state.CanStart("response", "external", new BuildingInstanceId("building:qa-facility"), Vector2Int.right, out _), "Receipt replay changed identity or restarted completed ownership.");
            });
            Check(checks, "malformed-response-restore-is-atomic", () =>
            {
                var state = NewProvenance();
                foreach (Action malformed in new Action[]
                {
                    () => state.RestoreSocietyResponseMovementProvenance("", "external", "", 0, 0, ""),
                    () => state.RestoreSocietyResponseMovementProvenance("", "", "", 1, 0, ""),
                    () => state.RestoreSocietyResponseMovementProvenance(" response ", "external", "building:qa-facility", 1, 0, ""),
                    () => state.RestoreSocietyResponseMovementProvenance("response", "external", "", 1, 0, ""),
                    () => state.RestoreSocietyResponseMovementProvenance("response", "external", "building:qa-facility", 1, 0, " receipt ")
                })
                {
                    bool rejected = false;
                    try { malformed(); } catch (InvalidOperationException) { rejected = true; }
                    Require(rejected && state.SocietyResponseMovementOperationId == "response"
                        && state.SocietyResponseMovementDestination == Vector2Int.right && state.SocietyResponseMovementReceiptId == string.Empty,
                        "Malformed restore changed the live response owner.");
                }
            });
            Check(checks, "existing-character-dto-json-fresh-owner-retry-and-release", () =>
            {
                var saved = new DungeonCharacterSaveData
                {
                    societyResponseMovementOperationId = "response", societyResponseMovementExternalOperationId = "external",
                    societyResponseMovementFacilityId = "building:qa-facility", societyResponseMovementDestinationX = 1,
                    societyResponseMovementReceiptId = "receipt"
                };
                DungeonCharacterSaveData restored = JsonUtility.FromJson<DungeonCharacterSaveData>(JsonUtility.ToJson(saved));
                var fresh = RestoreFresh(restored);
                Require(fresh.TryRecordSocietyResponseMovementReceipt("response", "external", "receipt", Vector2Int.right, out _)
                    && !fresh.TryReleaseSocietyResponseMovementProvenance("foreign", "external")
                    && fresh.SocietyResponseMovementReceiptId == "receipt", "Fresh owner lost exact receipt identity.");
                Require(fresh.TryReleaseSocietyResponseMovementProvenance("response", "external")
                    && fresh.TryReleaseSocietyResponseMovementProvenance("response", "external")
                    && fresh.SocietyResponseMovementDestination == null, "Release was not exact and idempotent.");
            });
        }
        finally { WriteNew("movement-ownership-pure.tsv", string.Join("\n", checks)); }
    }

    private static IEnumerator VerifyActual(IObjectResolver container)
    {
        rows.Clear(); rows.Add("case\tresult");
        var managers = container.Resolve<IOwnerRunManagerProvider>();
        Require(managers.TryGetManager(out var manager), "Actual owner manager missing after preparation.");
        CharacterActor subject = manager.CurrentOwnerActor;
        Require(subject != null, "Actual owner missing.");
        Require(subject.TryGetAbility(out AbilityMove move), "Actual owner movement missing.");
        Require(container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid), "Actual grid missing.");
        float readyDeadline = Time.realtimeSinceStartup + 20;
        while (subject.CurrentLifecycleState == CharacterLifecycleState.EnteringDungeon && Time.realtimeSinceStartup < readyDeadline) yield return null;
        Require(subject.CurrentLifecycleState == CharacterLifecycleState.Active, "Prepared owner did not finish authored entry transit.");
        var saves = container.Resolve<IDungeonGameSaveService>();
        string baseline = saves.ToJson(saves.Capture(), true);
        var paused = new List<(MonoBehaviour behavior, bool enabled)>();
        // The service drives decisions directly even when Brain/BT components
        // are disabled. Pause its owned scheduling window as well, then stop
        // the selected action through its real terminal cleanup before issuing
        // a controlled domain movement command.
        foreach (CharacterAiScheduler scheduler in UnityEngine.Object.FindObjectsByType<CharacterAiScheduler>(FindObjectsSortMode.None))
        {
            paused.Add((scheduler, scheduler.enabled));
            scheduler.enabled = false;
            Require(!scheduler.IsDrivingAi, "Movement fixture scheduler is still driving decisions.");
        }
        subject.Brain?.StopCurrentActionForReplan("movement-boundary-owned-command");
        foreach (CharacterActor actor in UnityEngine.Object.FindObjectsByType<CharacterActor>(FindObjectsSortMode.None))
        {
            if (actor.Brain != null) { paused.Add((actor.Brain, actor.Brain.enabled)); actor.Brain.enabled = false; }
            if (actor.BehaviorTree != null) { paused.Add((actor.BehaviorTree, actor.BehaviorTree.enabled)); actor.BehaviorTree.enabled = false; }
        }
        Require(Time.timeScale > 0 && move.GameClockDeltaTimeForDiagnostics > 0,
            "Movement fixture clock is paused after official preparation: unity=" + Time.timeScale
                + "; delta=" + move.GameClockDeltaTimeForDiagnostics);
        move.CancelActiveMovement("movement-boundary-setup");
        Vector2Int origin = subject.GetNowXY();
        Queue<GridMoveStep> route = null;
        Vector2Int destination = default;
        foreach (Vector2Int offset in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
        {
            Vector2Int candidate = origin + offset;
            if (!grid.IsValidGridPos(candidate) || !grid.IsWalkable(candidate)) continue;
            Queue<GridMoveStep> path = grid.GetMovePathTo(origin, candidate);
            if (path == null || path.Count == 0 || path.Count > 2 || path.Any(step => step.MoveType != GridMoveType.Walk)) continue;
            route = path; destination = candidate; break;
        }
        Require(route != null, "No short authored walk route exists for the controlled owner movement.");
        Require(move.TryStartProtectedSystemMoveWithResolvedPath(destination, DoorAccessOverrideKind.DirectCommand, route, out string start), start);
        int version = move.MovementOperationVersionForDiagnostics;
        Require(!move.TryCancelForImmediateAiReplan("controlled-replan") && move.MovementOperationVersionForDiagnostics == version,
            "AI replan stole active protected movement.");
        Require(move.HasActiveMovementRoutineForDiagnostics && move.HasProtectedSystemMovementOwnership, "Protected route did not acquire movement.");
        Mark("actual-resolved-route-protection-and-replan-rejection");
        DungeonCharacterSaveData mid = container.Resolve<ICharacterWorldSaveService>().Capture(grid).actors
            .Single(actor => actor.persistentId == subject.Identity.PersistentId);
        string midJson = JsonUtility.ToJson(mid);
        Require(!midJson.Contains("movementOperationVersion") && !midJson.Contains("activeActionMovementRoutine"), "Transient movement leaked into saved actor data.");
        Mark("official-character-capture-during-active-movement");
        float movementDeadline = Time.realtimeSinceStartup + 20;
        while (move.IsSystemMoveInProgress && Time.realtimeSinceStartup < movementDeadline) yield return null;
        Require(!move.IsSystemMoveInProgress && subject.GetNowXY() == destination
            && move.LastGridMoveFailureReason == GridMoveFailureReason.None && move.HasProtectedSystemMovementOwnership,
            "Protected actor failed arrival or lost retained ownership; actual=" + subject.GetNowXY()
                + "; target=" + destination + "; running=" + move.IsSystemMoveInProgress
                + "; protected=" + move.HasProtectedSystemMovementOwnership
                + "; failure=" + move.LastGridMoveFailureReason + "; delta=" + move.GameClockDeltaTimeForDiagnostics
                + "; unity=" + Time.timeScale + "; version=" + version + "->" + move.MovementOperationVersionForDiagnostics
                + "; cancel=" + move.LastMovementCancellationSourceForDiagnostics
                + "; preemption=" + move.LastMovementOperationPreemptionForDiagnostics
                + "; rejected=" + move.LastRejectedMovementOperationOwnerForDiagnostics
                + "; brain=" + subject.Brain?.GetDebugSummary(1));
        Require(!move.TryCancelForImmediateAiReplan("controlled-arrival-replan"), "Arrival released retained ownership.");
        Mark("actual-arrival-retains-protection-until-terminal-cleanup");
        move.CancelActiveMovement("controlled-terminal");
        Require(!move.HasProtectedSystemMovementOwnership && !move.HasActiveMovementRoutineForDiagnostics
            && move.ActiveSystemMoveDestinationForDiagnostics == null && move.ActiveMovementOperationOwnerForDiagnostics == string.Empty,
            "Explicit terminal cleanup retained movement state.");
        Mark("actual-terminal-cancel-releases-movement");
        Facility facility = UnityEngine.Object.FindObjectsByType<Facility>(FindObjectsSortMode.None)
            .FirstOrDefault(value => value.PersistentInstanceId.IsValid);
        Require(facility != null, "Authored facility identity missing.");
        Require(move.TryStartSocietyResponseSystemMove("qa:response", "qa:external", facility.PersistentInstanceId,
            destination, DoorAccessOverrideKind.None, out string response), response);
        Require(move.TryRecordSocietyResponseMovementReceipt("qa:response", "qa:external", "qa:receipt", out _)
            && move.TryRecordSocietyResponseMovementReceipt("qa:response", "qa:external", "qa:receipt", out _)
            && !move.TryRecordSocietyResponseMovementReceipt("qa:response", "qa:external", "qa:foreign", out _), "Actual movement receipt replay/conflict failed.");
        DungeonCharacterSaveData captured = container.Resolve<ICharacterWorldSaveService>().Capture(grid).actors
            .Single(actor => actor.persistentId == subject.Identity.PersistentId);
        string actorJson = JsonUtility.ToJson(captured);
        WriteNew("movement-response-actor.json", actorJson);
        DungeonCharacterSaveData restored = JsonUtility.FromJson<DungeonCharacterSaveData>(actorJson);
        var fresh = RestoreFresh(restored);
        Require(fresh.TryRecordSocietyResponseMovementReceipt("qa:response", "qa:external", "qa:receipt", destination, out _), "Actual captured receipt did not restore in a fresh owner.");
        Mark("actual-response-receipt-capture-json-fresh-owner-exact-retry");
        move.CancelActiveMovement("controlled-response-cancel");
        Require(move.SocietyResponseMovementOperationId == string.Empty && move.SocietyResponseMovementDestination == null, "Cancel retained response provenance.");
        move.RestoreSocietyResponseMovementProvenance(restored.societyResponseMovementOperationId,
            restored.societyResponseMovementExternalOperationId, restored.societyResponseMovementFacilityId,
            restored.societyResponseMovementDestinationX, restored.societyResponseMovementDestinationY,
            restored.societyResponseMovementReceiptId);
        Require(move.TryRecordSocietyResponseMovementReceipt("qa:response", "qa:external", "qa:receipt", out _)
            && !move.TryReleaseSocietyResponseMovementProvenance("qa:foreign", "qa:external")
            && move.TryReleaseSocietyResponseMovementProvenance("qa:response", "qa:external"), "Actual restored facade retry/release failed.");
        Mark("actual-response-cancel-restore-facade-retry-and-release");
        // QA response IDs exercise the movement producer, not a campaign response.
        // Release them before the official whole-world transaction validates cross-domain joins.
        string persistentId = subject.Identity.PersistentId;
        Require(subject.TryGetAbility(out AbilityWork previousWork), "Actual source work ability missing.");
        object[] previousWorkModules = CaptureActualWorkModuleGraph(previousWork);
        Require(saves.TryRestore(saves.FromJson(baseline), out var restore), "Official movement baseline restore failed: " + string.Join(" | ", restore.Errors));
        Require(restore.Warnings.Count == 0, "Official movement baseline restore warned.");
        for (int frame = Time.frameCount + 2; Time.frameCount < frame;) yield return null;
        Require(container.Resolve<ICharacterWorldSaveService>().TryGetRestoredActor(persistentId, out CharacterActor replacement)
            && replacement != null && !ReferenceEquals(subject, replacement) && replacement.TryGetAbility(out AbilityMove replacementMove)
            && !replacementMove.HasActiveMovementRoutineForDiagnostics && !replacementMove.HasProtectedSystemMovementOwnership
            && replacementMove.ActiveMovementOperationOwnerForDiagnostics == string.Empty
            && replacementMove.SocietyResponseMovementOperationId == string.Empty, "Official restore reused transient movement ownership.");
        Require(replacement.TryGetAbility(out AbilityWork restoredWork)
                && !restoredWork.HasActiveWorkRoutineForDiagnostics,
            "Official restore retained a transient work routine.");
        object[] restoredWorkModules = CaptureActualWorkModuleGraph(restoredWork);
        Require(!ReferenceEquals(previousWork, restoredWork)
                && previousWorkModules.Where((module, index) =>
                    ReferenceEquals(module, restoredWorkModules[index])).Count() == 0,
            "Official restore reused the previous actor's work module graph.");
        Mark("official-world-restore-rebuilds-work-module-graph-without-transient-run");
        VerifyActualExternalIntent(replacement.Brain, replacement.GetNowXY());
        Mark("actual-external-intent-renew-preempt-stale-completion-and-manual-cleanup");
        foreach (var state in paused) if (state.behavior != null) state.behavior.enabled = state.enabled;
        Mark("official-world-restore-replaces-actor-and-drops-transient-movement");
        GC.Collect();
        IEnumerator group = VerifyFaultRecoveryGroup("core");
        try { while (group.MoveNext()) yield return group.Current; }
        finally { (group as IDisposable)?.Dispose(); }
        WriteNew("movement-ownership-actual.tsv", string.Join("\n", rows));
        Debug.Log("ARCHITECTURE_MOVEMENT_OWNERSHIP_BATCH=PASS; actual route/protected ownership; response receipt JSON/fresh-owner/retry; official actor/work graph restore; core2; actual external lease arbitration; destinationless/deprivation/primitive use dedicated processes");
    }

    private static IEnumerator VerifyStandaloneFaultRecovery(string selector)
    {
        GC.Collect();
        IEnumerator group = VerifyFaultRecoveryGroup(selector);
        try { while (group.MoveNext()) yield return group.Current; }
        finally { (group as IDisposable)?.Dispose(); }
        WriteNew("movement-fault-group-actual.tsv", string.Join("\n", rows));
        Debug.Log("ARCHITECTURE_AI_FAULT_GROUP_BATCH=PASS; selector=" + selector);
    }

    private static IEnumerator VerifyFaultRecoveryGroup(string selector)
    {
        string directory = Path.Combine(Path.GetDirectoryName(EvidencePath("unused")), "ai-" + selector);
        Directory.CreateDirectory(directory);
        var runner = new GameObject("Movement ownership " + selector + " regression")
            .AddComponent<CharacterAiFaultRecoveryPlayModeRunner>();
        runner.ConfigureSelector(selector);
        runner.ConfigureIsolatedEvidence(directory);
        string report = Path.Combine(directory, Path.GetFileName(CharacterAiFaultRecoveryPlayModeVerifier.GetReportPathForSelector(selector)));
        float deadline = Time.realtimeSinceStartup + 300; // Existing runner owns its unchanged 270-second failure bound.
        while (runner != null && Time.realtimeSinceStartup < deadline) yield return null;
        Require(runner == null && File.Exists(report), "Existing AI regression did not finish: " + selector);
        string evidence = File.ReadAllText(report);
        string[] reportLines = evidence.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Require(reportLines.Contains("status=FINAL") && reportLines.Contains("result=PASS")
            && reportLines.Contains("RESULT=PASS") && reportLines.Contains("consoleErrors=0")
            && reportLines.Contains("failures=0"), "Existing AI regression failed: " + selector + "; " + report);
        foreach (string row in CharacterAiFaultRecoveryPlayModeVerifier.GetExpectedRowsForSelector(selector))
            Require(evidence.Contains("PASS SCENARIO " + row + ":"), "Existing AI regression omitted row: " + row);
        Mark("actual-brain-behavior-tree-" + selector + "-fault-recovery");
    }

    private static IEnumerator VerifyRestock(DungeonRuntimeLifetimeScope scope)
    {
        Require(scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid), "Restock grid missing.");
        CharacterActor actor = null;
        float readyDeadline = Time.realtimeSinceStartup + 20;
        while (actor == null && Time.realtimeSinceStartup < readyDeadline)
        {
            actor = CharacterActorCollection.DistinctByGameObject(
                    UnityEngine.Object.FindObjectsByType<CharacterActor>(FindObjectsSortMode.None))
                .Where(value => value != null && !value.IsDead && value.Brain != null
                    && value.CurrentLifecycleState == CharacterLifecycleState.Active
                    && value.TryGetAbility(out AbilityWork work) && work.WorkPriorities != null
                    && work.PriorityWorkTarget == null
                    && work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul) != WorkPriorityLevel.Off
                    && value.TryGetAbility(out AbilityMove _))
                .OrderBy(value => value.Identity?.IsOwner == true ? 1 : 0)
                .ThenBy(value => value.BuildingCharacterId.Value, StringComparer.Ordinal).FirstOrDefault();
            if (actor == null) yield return null;
        }
        Require(actor != null, "Restock fixture requires an active production worker.");
        IDungeonGameSaveService saves = scope.Container.Resolve<IDungeonGameSaveService>();
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        IItemQuantityReservationService reservations = scope.Container.Resolve<IItemQuantityReservationService>();
        string baseline = saves.ToJson(saves.Capture(), false);
        WriteNew("work-restock-baseline-world.json", baseline);
        string actorId = actor.Identity.PersistentId;
        string checkpointShopId = null;
        string checkpointItemId = null;
        var checkpoints = new List<(string Phase, string Json, string Operation, int Stock, int Carry, long PhysicalQuantity)>();
        IGridBuildingObjectFactory factory = scope.Container.Resolve<IGridBuildingObjectFactory>();
        var created = new List<BuildableObject>();
        var report = new List<string>();
        BuildableObject Place(BuildingSO definition, Vector2Int position)
        {
            BuildableObject building = factory.Create(grid, definition, position);
            Require(building != null, "Restock authored building factory failed: " + definition.name);
            created.Add(building);
            foreach (MonoBehaviour component in building.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null) scope.Container.Inject(component);
            building.SetGrid(grid);
            building.Initialization(definition, position);
            Require(grid.RegisterOccupant(building, definition.layer, definition.GetGridPosList(position), definition.Placement.IsMovement),
                "Restock authored building registration failed: " + definition.name);
            return building;
        }

        long PhysicalQuantity() => items.GetAllStacks()
            .Where(value => value != null && value.ItemId == checkpointItemId)
            .Sum(value => (long)value.Quantity);

        void CaptureCheckpoint(string phase, Shop shop, WorldItemStackSnapshot source)
        {
            Require((phase == "pending" || phase == "carried")
                    && checkpoints.All(value => value.Phase != phase),
                "Restock world checkpoint is invalid or was captured twice: " + phase);
            string shopId = shop.RequirePersistentInstanceId().Value;
            checkpointShopId ??= shopId;
            checkpointItemId ??= source.ItemId;
            Require(checkpointShopId == shopId && checkpointItemId == source.ItemId,
                "Restock save checkpoints changed their physical facility/item authority.");
            string[] operations = shop.CreateStockSnapshot().activeRestockOperationIds.ToArray();
            Require(operations.Length == 1
                    && reservations.TryGetLeasesByOwner(operations[0], out var leases)
                    && leases.Count == 1 && leases[0].ownerCharacterId == actorId
                    && leases[0].purpose == ItemReservationPurpose.FacilityBuffer
                    && leases[0].remainingQuantity > 0,
                "Restock checkpoint has no exact live shop/quantity-lease ownership.");
            int carried = CharacterCarryInventory.Ensure(actor).CountItem(checkpointItemId);
            Require(phase == "pending" ? carried == 0 : carried > 0,
                "Restock checkpoint does not match its actual physical pickup phase: " + phase);
            string json = saves.ToJson(saves.Capture(), false);
            checkpoints.Add((phase, json, operations[0], shop.CurrentStock, carried, PhysicalQuantity()));
            WriteNew("work-restock-" + phase + "-world.json", json);
            report.Add("restock-world-captured=" + phase + ";official=true;carried=" + carried
                + ";operation=" + operations[0]);
        }

        IEnumerator Exercise()
        {
            yield return WimDoorConsumerScenario.RunRestockForArchitectureBatch(
                scope, actor, grid, Place, report, CaptureCheckpoint);
            Require(checkpoints.Count == 2 && checkpoints[0].Phase == "pending" && checkpoints[1].Phase == "carried",
                "Restock did not produce both real pending and physically carried world checkpoints.");
            CharacterActor previousActor = actor;
            foreach (var checkpoint in checkpoints)
            {
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    if (attempt == 2)
                    {
                        Require(saves.TryRestore(saves.FromJson(checkpoint.Json), out var liveRestore),
                            "Official live-owner restock restore failed: " + string.Join(" | ", liveRestore.Errors));
                        Require(liveRestore.Warnings.Count == 0, "Live-owner restock restore warned.");
                        foreach (CharacterActor live in CharacterActorCollection.DistinctByGameObject(
                                     UnityEngine.Object.FindObjectsByType<CharacterActor>(FindObjectsSortMode.None)))
                            live.SetAiPaused(true);
                        for (int frame = Time.frameCount + 2; Time.frameCount < frame;) yield return null;
                        Require(scope.Container.Resolve<ICharacterWorldSaveService>().TryGetRestoredActor(actorId, out previousActor)
                                && previousActor != null
                                && CharacterCarryInventory.Ensure(previousActor).CountItem(checkpointItemId) == checkpoint.Carry,
                            "Live-owner restock restore lost the worker or physical cargo.");
                        // Keep its durable ownership live. The next restore must
                        // retire this actor without releasing the replacement's claim.
                    }
                    Require(saves.TryRestore(saves.FromJson(checkpoint.Json), out var restore),
                        "Official " + checkpoint.Phase + " restock restore failed: " + string.Join(" | ", restore.Errors));
                    Require(restore.Warnings.Count == 0, "Official restock restore warned: " + checkpoint.Phase);
                    foreach (CharacterActor live in CharacterActorCollection.DistinctByGameObject(
                                 UnityEngine.Object.FindObjectsByType<CharacterActor>(FindObjectsSortMode.None)))
                        live.SetAiPaused(true);
                    for (int frame = Time.frameCount + 2; Time.frameCount < frame;) yield return null;
                    AbilityWork restoredWork = null;
                    Require(scope.Container.Resolve<ICharacterWorldSaveService>().TryGetRestoredActor(actorId, out var restoredActor)
                            && restoredActor != null && !ReferenceEquals(previousActor, restoredActor)
                            && restoredActor.TryGetAbility(out restoredWork)
                            && !restoredWork.HasActiveWorkRoutineForDiagnostics,
                        "Restock restore reused a transient actor/work coroutine.");
                    Shop restoredShop = UnityEngine.Object.FindObjectsByType<Shop>(FindObjectsSortMode.None)
                        .SingleOrDefault(value => value != null && !value.isDestroy
                            && value.PersistentInstanceId.Value == checkpointShopId);
                    CharacterCarryInventory carry = CharacterCarryInventory.Ensure(restoredActor);
                    Require(restoredShop != null && restoredShop.CurrentStock == checkpoint.Stock
                            && restoredShop.CreateStockSnapshot().activeRestockOperationIds.SequenceEqual(new[] { checkpoint.Operation })
                            && reservations.TryGetLeasesByOwner(checkpoint.Operation, out var leases)
                            && leases.Count == 1 && leases[0].ownerCharacterId == actorId
                            && leases[0].purpose == ItemReservationPurpose.FacilityBuffer
                            && PhysicalQuantity() == checkpoint.PhysicalQuantity
                            && carry.CountItem(checkpointItemId) == checkpoint.Carry,
                        "Official restock restore lost or duplicated durable cargo/lease/shop ownership: " + checkpoint.Phase);
                    if (attempt == 2)
                        report.Add("restock-official-" + checkpoint.Phase
                            + "-replace-live-owner=PASS;freshActor=true;lease/shop/cargo=preserved");
                    Vector2Int dropCell = restoredActor.GetNowXY();
                    Require(scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid restoredGrid)
                            && restoredGrid.IsValidGridPos(dropCell),
                        "Restored restock worker has no valid physical drop cell.");
                    long looseBefore = items.GetAllStacks().Where(value => value != null
                            && value.ItemId == checkpointItemId && value.State == WorldItemStackState.Loose
                            && value.Position == dropCell).Sum(value => (long)value.Quantity);
                    for (int cancel = 1; cancel <= 2; cancel++)
                    {
                        restoredWork.StopAssignedWorkFromAi("qa:restock-restored-cancel");
                        bool hasLeases = reservations.TryGetLeasesByOwner(checkpoint.Operation, out var remaining)
                            && remaining.Count > 0;
                        long looseAfter = items.GetAllStacks().Where(value => value != null
                                && value.ItemId == checkpointItemId && value.State == WorldItemStackState.Loose
                                && value.Position == dropCell).Sum(value => (long)value.Quantity);
                        Require(!hasLeases && restoredShop.ActiveRestockOperationCount == 0
                                && carry.CountItem(checkpointItemId) == 0
                                && restoredShop.CurrentStock == checkpoint.Stock
                                && PhysicalQuantity() == checkpoint.PhysicalQuantity
                                && looseAfter - looseBefore == checkpoint.Carry,
                            "Restored restock cancellation leaked ownership or changed physical quantity: "
                            + checkpoint.Phase + ";restore=" + attempt + ";cancel=" + cancel
                            + ";leases=" + (remaining?.Count ?? 0)
                            + ";shopOperations=" + restoredShop.ActiveRestockOperationCount
                            + ";carry=" + carry.CountItem(checkpointItemId)
                            + ";stock=" + restoredShop.CurrentStock + "/" + checkpoint.Stock
                            + ";physical=" + PhysicalQuantity() + "/" + checkpoint.PhysicalQuantity
                            + ";looseDelta=" + (looseAfter - looseBefore) + "/" + checkpoint.Carry);
                    }
                    previousActor = restoredActor;
                    report.Add("restock-official-" + checkpoint.Phase + "-restore-cancel-retry=PASS;restore=" + attempt
                        + ";freshActor=true;cancel=2;lease=0;shopOperation=0;physicalQuantity=conserved;drop=currentCell");
                }
            }
        }

        Exception failure = null;
        void RecordFailure(Exception exception) => failure = failure == null
            ? exception : new AggregateException(failure, exception);
        IEnumerator observed = PlayModeVerificationCoroutine.Observe(Exercise, RecordFailure);
        try
        {
            while (observed.MoveNext())
            {
                if (observed.Current != null)
                {
                    RecordFailure(new InvalidOperationException(
                        "Restock batch iterator requires frame waits; unsupported yield instruction."));
                    break;
                }
                yield return null;
            }
        }
        finally
        {
            try { (observed as IDisposable)?.Dispose(); }
            catch (Exception exception) { RecordFailure(exception); }
            foreach (BuildableObject building in created.AsEnumerable().Reverse())
            {
                try { if (building != null && !building.isDestroy) building.DestroySelf(); }
                catch (Exception exception) { RecordFailure(exception); }
            }
            try
            {
                Require(saves.TryRestore(saves.FromJson(baseline), out var restore),
                    "Restock verification baseline restore failed: " + string.Join(" | ", restore.Errors));
                Require(restore.Warnings.Count == 0, "Restock verification baseline restore warned.");
                report.Add("restock-official-baseline-restored=PASS");
            }
            catch (Exception exception) { RecordFailure(exception); }
            report.Add(failure == null ? "result=PASS" : "result=FAIL; " + failure);
            try { WriteNew("work-restock-actual.txt", string.Join("\n", report)); }
            catch (Exception exception)
            {
                if (failure != null)
                    throw new AggregateException("Restock verification and evidence writing failed.", failure, exception);
                throw;
            }
        }
        if (failure != null) throw new InvalidOperationException("Actual restock verification failed.", failure);
        Debug.Log("ARCHITECTURE_WORK_RESTOCK_BATCH=PASS; actual pending/no-path; selector reinjection; natural physical retry; official pending/carried world restore3 each including live owner replacement; exact cancellation2 each; current-cell recovery; baseline restored");
    }

    private static IEnumerator VerifyWorkAccident(DungeonRuntimeLifetimeScope scope)
    {
        var report = new List<string>
        {
            "scope=controlled actual actor/facility and approved production WU; pending work-orders JSON candidate restore; not natural accident probability or UI training"
        };
        var container = scope.Container;
        Require(container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out var manager),
            "Work accident owner manager missing.");
        float deadline = Time.realtimeSinceStartup + 20f;
        while (manager.CurrentOwnerActor != null
            && manager.CurrentOwnerActor.CurrentLifecycleState == CharacterLifecycleState.EnteringDungeon
            && Time.realtimeSinceStartup < deadline) yield return null;
        Require(manager.CurrentOwnerActor?.CurrentLifecycleState == CharacterLifecycleState.Active,
            "Work accident baseline owner did not finish entry.");
        IDungeonGameSaveService saves = container.Resolve<IDungeonGameSaveService>();
        string baseline = saves.ToJson(saves.Capture(), false);
        WriteNew("work-accident-baseline-world.json", baseline);
        UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = true;
        container.Resolve<IGameTimeScaleController>().Scale = 0;
        Time.timeScale = 0;
        Exception failure = null;
        BuildingSO controlledDefinition = null;
        try
        {
            Require(container.Resolve<IGridSystemProvider>().TryGetGrid(out Grid grid),
                "Work accident main grid unavailable.");
            BuildingSO authored = Resources.Load<BuildingSO>("SO/Building/Modular/D04_배식카운터");
            Require(authored != null && authored.OperationProficiency.IsValid
                && authored.Facility?.SupportsWork(BuiltInWorkTypeIds.Operate) == true,
                "Controlled work accident requires a genuine authored Operate facility/profile.");
            controlledDefinition = UnityEngine.Object.Instantiate(authored);
            controlledDefinition.hideFlags = HideFlags.HideAndDontSave;
            BuildingSO definition = controlledDefinition;
            Require(definition.GetAbility<BuildingEnvironmentalFireAbility>() == null
                && definition.GetAbility<BuildingProcessAccidentFireSourceAbility>() == null,
                "Controlled fire-plan setup must not replace an existing authored fire plan.");
            definition.AbilityModules.Add(new BuildingEnvironmentalFireAbility
            {
                acceptedSources = EnvironmentalFireIgnitionSources.ProcessAccident
            });
            definition.AbilityModules.Add(new BuildingProcessAccidentFireSourceAbility
            {
                workTypeId = BuiltInWorkTypeIds.Operate.Value,
                ignitionIntensity = 0.25f
            });
            report.Add("CONTROLLED_FIRE_PLAN: runtime-only clone of genuine D04 Operate profile; explicit process-fire input; authored asset unchanged");
            GridPlacementValidator geometry = new();
            Vector2Int? anchor = null;
            for (int y = 0; y < grid.height && !anchor.HasValue; y++)
            for (int x = 0; x < grid.width && !anchor.HasValue; x++)
            {
                Vector2Int position = new(x, y);
                var cells = definition.GetGridPosList(position);
                if (geometry.AreInsideHorizontalBounds(grid, cells, 1)
                    && geometry.CanBuildInArea(grid, definition, cells)
                    && geometry.CanOccupy(grid, definition.Placement.Layer, cells)
                    && geometry.HasSupportBelow(grid, cells)) anchor = position;
            }
            Require(anchor.HasValue, "No legal footprint for controlled accident facility.");
            var builder = UnityEngine.Object.FindFirstObjectByType<DungeonStoryGridBuildingController>();
            string placementFailure = "building-controller-missing";
            Require(builder != null && builder.TryPlaceInitialBuildings(new[]
                { new InitialBuildInfo { Building = definition, Position = anchor.Value } },
                out placementFailure), "Controlled accident facility placement failed: " + placementFailure);
            BuildableObject facility = container.Resolve<ICharacterAiWorldRegistry>().Buildings
                .Single(value => value != null && !value.isDestroy
                    && value.BuildingData == definition && value.centerPos == anchor.Value);
            report.Add("CONTROLLED_FACILITY: production initial-placement command; definition="
                + definition.name + ";id=" + facility.PersistentInstanceId.Value);
            V27CharacterPerformanceDebugScenarios.RunWorkAccidentForArchitectureBatch(scope, facility, report);
        }
        catch (Exception exception) { failure = exception; }
        // Audit actors use Unity deferred destruction; retire their bridges before
        // replacing the official world with the captured baseline.
        int frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        try
        {
            Require(saves.TryRestore(saves.FromJson(baseline), out var restored),
                "Work accident baseline restore failed: " + string.Join(" | ", restored.Errors));
            Require(restored.Warnings.Count == 0, "Work accident baseline restore warned.");
            report.Add("PASS official whole-world baseline restored after controlled accident fixtures");
        }
        catch (Exception exception)
        { failure = failure == null ? exception : new AggregateException(failure, exception); }
        if (controlledDefinition != null) UnityEngine.Object.Destroy(controlledDefinition);
        report.Add(failure == null ? "result=PASS" : "result=FAIL; " + failure);
        WriteNew("work-accident-actual.txt", string.Join("\n", report));
        if (failure != null) throw new InvalidOperationException("Actual work accident verification failed.", failure);
        Debug.Log("ARCHITECTURE_WORK_ACCIDENT_BATCH=PASS; production approved labor/injury/abort; pending JSON candidate restore; completion/retry no replay; official baseline restored");
    }

    private static IEnumerator VerifySkillTargeting(DungeonRuntimeLifetimeScope scope)
    {
        var report = new List<string>
        {
            "scope=actual GameplayScene prepared workers, production staff UI Button event, camera/collider/raycast/UI ports and automatic Play Update/OnDisable; controlled input and local real transaction/observer; not hardware input or official pending skill world restore"
        };
        var container = scope.Container;
        float deadline = Time.realtimeSinceStartup + 20;
        IReadOnlyList<StaffWorkPriorityRowModel> workers;
        do
        {
            workers = container.Resolve<IStaffWorkPriorityPanelModelBuilder>().BuildRows();
            if (workers.Count >= 2 && workers.All(value => value.Character.CurrentLifecycleState == CharacterLifecycleState.Active)) break;
            Require(Time.realtimeSinceStartup < deadline, "C1 actual starting workers did not finish entry.");
            yield return null;
        } while (true);
        var saves = container.Resolve<IDungeonGameSaveService>();
        string baseline = saves.ToJson(saves.Capture(), false);
        WriteNew("skill-targeting-baseline-world.json", baseline);
        var errors = new List<string>();
        void ObserveLog(string condition, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(condition);
        }
        UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = true;
        container.Resolve<IGameTimeScaleController>().Scale = 0;
        Time.timeScale = 0;
        Exception failure = null;
        Application.logMessageReceived += ObserveLog;
        IEnumerator observed = PlayModeVerificationCoroutine.Observe(
            () => CharacterSkillActivationOutcomeDebugScenarios.VerifyOwnerTargetingPlay(scope, report),
            exception => failure = exception);
        try
        {
            while (observed.MoveNext())
            {
                Require(observed.Current == null, "C1 fixture supports frame waits only.");
                yield return null;
            }
        }
        finally { (observed as IDisposable)?.Dispose(); }
        int frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        try
        {
            Require(saves.TryRestore(saves.FromJson(baseline), out var restored),
                "C1 baseline restore failed: " + string.Join(" | ", restored.Errors));
            Require(restored.Warnings.Count == 0, "C1 baseline restore warned.");
            report.Add("PASS official whole-world baseline restored after actual UI/pointer fixture");
            Require(errors.Count == 0, "C1 engine errors: " + string.Join(" | ", errors));
        }
        catch (Exception exception)
        { failure = failure == null ? exception : new AggregateException(failure, exception); }
        finally { Application.logMessageReceived -= ObserveLog; }
        report.Add("consoleErrors=" + errors.Count);
        report.Add(failure == null ? "result=PASS" : "result=FAIL; " + failure);
        WriteNew("skill-targeting-actual.txt", string.Join("\n", report));
        if (failure != null) throw new InvalidOperationException("Actual C1 targeting verification failed.", failure);
        Debug.Log("ARCHITECTURE_SKILL_TARGETING_BATCH=PASS; actual UI/Play callbacks/camera/physics; invalid retry; one cooldown/buff/outcome; official baseline restored");
    }

    private static IEnumerator VerifyFactionReinforcement(DungeonRuntimeLifetimeScope scope)
    {
        var report = new List<string>
        {
            "scope=official whole-world pending/completed faction route JSON; fresh current domain root and actual GameplayScene actors; partial and post-publication response-loss prefixes; production Tick retry/normal entry; controlled route/fault, not natural strategic route creation"
        };
        var container = scope.Container;
        Require(container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out var manager),
            "Reinforcement baseline owner manager missing.");
        float deadline = Time.realtimeSinceStartup + 20f;
        while (manager.CurrentOwnerActor != null
            && manager.CurrentOwnerActor.CurrentLifecycleState == CharacterLifecycleState.EnteringDungeon
            && Time.realtimeSinceStartup < deadline) yield return null;
        Require(manager.CurrentOwnerActor?.CurrentLifecycleState == CharacterLifecycleState.Active,
            "Reinforcement baseline owner did not finish entry.");
        var saves = container.Resolve<IDungeonGameSaveService>();
        string baseline = saves.ToJson(saves.Capture(), false);
        WriteNew("faction-reinforcement-baseline-world.json", baseline);
        var errors = new List<string>();
        void ObserveLog(string condition, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(condition);
        }
        UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = true;
        container.Resolve<IGameTimeScaleController>().Scale = 0;
        Time.timeScale = 0;
        Exception failure = null;
        Application.logMessageReceived += ObserveLog;
        IEnumerator observed = PlayModeVerificationCoroutine.Observe(
            () => FactionCargoPublicationDebugScenarios.RunReinforcementWorldRestoreForArchitectureBatch(scope, report, WriteNew, baseline),
            exception => failure = exception);
        try
        {
            while (observed.MoveNext())
            {
                Require(observed.Current == null, "Reinforcement fixture supports frame waits only.");
                yield return null;
            }
        }
        finally
        {
            (observed as IDisposable)?.Dispose();
        }
        int frame = Time.frameCount;
        while (Time.frameCount <= frame + 1) yield return null;
        try
        {
            Require(saves.TryRestore(saves.FromJson(baseline), out var restored),
                "Reinforcement baseline restore failed: " + string.Join(" | ", restored.Errors));
            Require(restored.Warnings.Count == 0, "Reinforcement baseline restore warned.");
            report.Add("PASS official whole-world baseline restored after pending/current-root integration");
            Require(errors.Count == 0, "Reinforcement engine errors: " + string.Join(" | ", errors));
        }
        catch (Exception exception)
        { failure = failure == null ? exception : new AggregateException(failure, exception); }
        finally { Application.logMessageReceived -= ObserveLog; }
        report.Add("consoleErrors=" + errors.Count);
        report.Add(failure == null ? "result=PASS" : "result=FAIL; " + failure);
        WriteNew("faction-reinforcement-actual.txt", string.Join("\n", report));
        if (failure != null) throw new InvalidOperationException("Actual reinforcement verification failed.", failure);
        Debug.Log("ARCHITECTURE_FACTION_REINFORCEMENT_BATCH=PASS; official pending/completed whole-world JSON; fresh actual root/actors; partial and lost-response retry; no duplicates; actual entry; official baseline restored");
    }

    private static IEnumerator VerifyMedicalTransfer(DungeonRuntimeLifetimeScope scope)
    {
        var report = new List<string>();
        Exception failure = null;
        IEnumerator observed = PlayModeVerificationCoroutine.Observe(
            () => Wim040ObservedIncidentLiveRunner.RunForArchitectureBatch(scope, report),
            exception => failure = exception);
        try
        {
            while (observed.MoveNext())
            {
                Require(observed.Current == null,
                    "Medical transfer batch supports the existing frame-wait scenario only.");
                yield return null;
            }
        }
        finally
        {
            try { (observed as IDisposable)?.Dispose(); }
            catch (Exception exception)
            { failure = failure == null ? exception : new AggregateException(failure, exception); }
            report.Add(failure == null ? "result=PASS" : "result=FAIL; " + failure);
            WriteNew("society-medical-transfer-actual.txt", string.Join("\n", report));
        }
        if (failure != null) throw new InvalidOperationException("Actual medical transfer verification failed.", failure);
        Debug.Log("ARCHITECTURE_SOCIETY_MEDICAL_TRANSFER_BATCH=PASS; actual physical meal/body collapse/Society response/AIRescue; official in-memory restore and exact receipt; not disk-slot or completed-treatment proof");
    }

    internal static object[] CaptureActualWorkModuleGraph(AbilityWork work)
    {
        Require(work != null, "Actual work module graph requires a native ability.");
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic;
        object[] modules = new[] { "WorkModules", "TargetSelector", "TaskExecutor", "DutyController", "CommandHandler" }
            .Select(name => typeof(AbilityWork).GetProperty(name, flags)?.GetValue(work)
                ?? throw new InvalidOperationException("Actual work module is missing: " + name))
            .ToArray();
        object restock = modules[2].GetType().GetField("restock", flags)?.GetValue(modules[2])
            ?? throw new InvalidOperationException("Actual work executor has no restock owner.");
        Require(ReferenceEquals(modules[4].GetType().GetField("targetSelector", flags)?.GetValue(modules[4]), modules[1])
                && ReferenceEquals(restock.GetType().GetField("targetSelector", flags)?.GetValue(restock), modules[1]),
            "Actual command/restock owners use different target selectors.");
        return modules;
    }

    private static void VerifyActualExternalIntent(AIBrain brain, Vector2Int position)
    {
        Require(brain != null, "Restored actor has no Brain for external intent verification.");
        long epoch = brain.ExternalIntentEpoch;
        int transitions = brain.ExternalIntentTransitionCount;
        int terminals = brain.ExternalIntentTerminalCount;
        int preemptions = brain.ExternalIntentPreemptionCount;
        int stale = brain.ExternalIntentStaleCompletionCount;
        int rejected = brain.ExternalIntentRejectedCount;
        Require(brain.TryBeginExternallyDrivenAction("qa:low", CharacterActionIntentKind.RoutineNeed,
            "controlled low", "Waiting", null, out var low), "Actual lower-priority intent did not start.");
        Require(brain.TryBeginExternallyDrivenAction("qa:low", CharacterActionIntentKind.Breakdown,
            "controlled renewal", "Renewed", null, out var renewal) && renewal.Epoch == low.Epoch
            && renewal.Kind == CharacterActionIntentKind.RoutineNeed, "Actual same-owner renewal changed lease.");
        Require(brain.TryBeginExternallyDrivenAction("qa:high", CharacterActionIntentKind.Breakdown,
            "controlled high", "Waiting", null, out var high) && !brain.EndExternallyDrivenAction(low),
            "Actual higher-priority preemption or stale completion rejection failed.");
        brain.BeginManualMoveCommand(position);
        Require(brain.IsManualCommandActive && !brain.IsExternallyDrivenActionActive
            && !brain.EndExternallyDrivenAction(high)
            && !brain.TryBeginExternallyDrivenAction("qa:blocked", CharacterActionIntentKind.Breakdown,
                "blocked", "Waiting", null, out _), "Actual manual command failed to retain ownership.");
        brain.CompleteManualMoveCommand(position, succeeded: true);
        Require(!brain.IsManualCommandActive && !brain.IsExternallyDrivenActionActive
            && brain.ExternalIntentEpoch == epoch + 2 && brain.ExternalIntentTransitionCount == transitions + 2
            && brain.ExternalIntentTerminalCount == terminals + 2 && brain.ExternalIntentPreemptionCount == preemptions + 1
            && brain.ExternalIntentStaleCompletionCount == stale + 2 && brain.ExternalIntentRejectedCount == rejected + 1,
            "Actual intent/manual cleanup did not conserve epochs/terminals/rejections.");
    }

    private static SocietyResponseMovementProvenanceRuntime NewProvenance()
    {
        var state = new SocietyResponseMovementProvenanceRuntime();
        state.RecordStarted("response", "external", new BuildingInstanceId("building:qa-facility"), Vector2Int.right);
        return state;
    }
    private static SocietyResponseMovementProvenanceRuntime RestoreFresh(DungeonCharacterSaveData actor)
    {
        var state = new SocietyResponseMovementProvenanceRuntime();
        state.RestoreSocietyResponseMovementProvenance(actor.societyResponseMovementOperationId,
            actor.societyResponseMovementExternalOperationId, actor.societyResponseMovementFacilityId,
            actor.societyResponseMovementDestinationX, actor.societyResponseMovementDestinationY, actor.societyResponseMovementReceiptId);
        return state;
    }
    private static void Check(List<string> checks, string name, Action action)
    {
        try { action(); checks.Add(name + "\tPASS"); }
        catch { checks.Add(name + "\tFAIL"); throw; }
    }
    private static void Mark(string name) { rows.Add(name + "\tPASS"); Debug.Log("MOVEMENT_BOUNDARY=PASS; " + name); }
    private static void Require(bool valid, string failure) { if (!valid) throw new InvalidOperationException(failure); }
    private static void Finish(int code)
    {
        IEnumerator owned = verification;
        verification = null;
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Pump;
        try { (owned as IDisposable)?.Dispose(); }
        catch (Exception exception) { code = 1; Debug.LogException(exception); }
        finally { EditorApplication.Exit(code); }
    }
    private static string EvidencePath(string name)
    {
        string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-logFile");
        Require(index >= 0 && index + 1 < args.Length && Path.IsPathRooted(args[index + 1]), "Isolated Unity log missing.");
        string directory = Path.GetDirectoryName(Path.GetFullPath(args[index + 1]));
        Require(directory.StartsWith(Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Evidence outside QA root.");
        return Path.Combine(directory, name);
    }
    private static void WriteNew(string name, string text)
    {
        using var stream = new FileStream(EvidencePath(name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream); writer.Write(text);
    }
}
#endif
