#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

internal static class WimDoorOperationScenario
{
    internal static IEnumerator Run(DungeonRuntimeLifetimeScope scope, CharacterActor actor, Grid grid,
        Func<BuildingSO, Vector2Int, BuildableObject> place, List<string> report, bool returnRestoreOnly = false)
    {
        var query = scope.Container.Resolve<IDoorAccessQuery>();
        var command = scope.Container.Resolve<IDoorAccessCommandService>();
        AbilityMove move = actor.GetAbility<AbilityMove>();
        var origin = actor.GetNowXY();
        var reachable = grid.SearchPath(origin).GetReachablePositions();
        Vector2Int? chosen = null;
        foreach (var cell in reachable.OrderBy(p => Math.Abs(p.x - origin.x) + Math.Abs(p.y - origin.y)))
        {
            var a = cell + Vector2Int.left; var b = cell + Vector2Int.right;
            if (!grid.IsValidGridPos(a) || !grid.IsValidGridPos(b) || !grid.IsWalkable(a) || !grid.IsWalkable(b)) continue;
            if (grid.GetGridCell(cell)?.GetOccupant(GridLayer.Building) != null
                || grid.GetGridCell(a)?.GetOccupant(GridLayer.Building) != null
                || grid.GetGridCell(b)?.GetOccupant(GridLayer.Building) != null) continue;
            chosen = cell; break;
        }
        Require(chosen.HasValue, "No real free doorway corridor.");
        Vector2Int center = chosen.Value, left = center + Vector2Int.left, right = center + Vector2Int.right;
        Require(move.TryStartSystemMove(left, DoorAccessOverrideKind.None, out string failure), failure);
        yield return Wait(() => actor.GetNowXY() == left && !move.IsSystemMoveInProgress, "Actor did not reach doorway approach.");
        var definition = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/InteriorDoor.asset");
        Require(definition != null, "Authored interior door missing.");
        var door = place(definition, center) as Door;
        Require(door != null && !door.IsOpen, "Actual door must start closed.");
        if (returnRestoreOnly)
        {
            yield return VerifySavedReturn(scope, actor, grid, door, left, center, command, report);
            yield break;
        }
        int structure = grid.StructuralVersion;
        var context = GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor));
        string state = door.OperationStateModule.CaptureState();
        for (int i = 0; i < 10; i++) Require(query.CanTraverse(grid, center, context, out _), "Default actor permission denied.");
        Require(!door.IsOpen && door.OperationStateModule.CaptureState() == state, "Path queries opened the door.");
        string id = CharacterPersistentIdentity.Require(actor).Value;
        Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.Deny), "Could not deny actor.");
        Require(!query.CanTraverse(grid, center, context, out _) && !door.IsOpen, "Denied actor can traverse.");
        Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.GroupDefault), "Could not restore actor permission.");
        report.Add("readonly-path-query-and-individual-denial=PASS");

        Require(move.TryStartSystemMove(center, DoorAccessOverrideKind.None, out failure), failure);
        yield return Wait(() => door.IsOpen, "Actual movement did not open door before entry.");
        Require(command.SetHeldOpen(door, false) && door.IsOpen, "Incoming passage was closed on actor.");
        yield return Wait(() => actor.GetNowXY() == center && !move.IsSystemMoveInProgress, "Actor did not enter door cell.");
        Require(command.SetHeldOpen(door, false) && door.IsOpen, "Occupied door closed.");
        Require(move.TryStartSystemMove(right, DoorAccessOverrideKind.None, out failure), failure);
        yield return Wait(() => actor.GetNowXY() == right && !move.IsSystemMoveInProgress && !door.IsOpen,
            "Door did not close after actual actor exit.");
        Require(grid.StructuralVersion == structure, "Door operation changed room structural revision.");
        report.Add("actual-system-movement-open-incoming-occupied-exit-close=PASS");

        var info = UnityEngine.Object.FindFirstObjectByType<UIBuildingInfo>(FindObjectsInactive.Include);
        Require(info != null, "Main building info missing.");
        info.DisplayBuildingInfo(door); info.OpenDispaly();
        yield return null;
        ClickOperation();
        yield return null;
        Require(door.IsHeldOpen && door.IsOpen, "Actual UI hold command failed.");
        string held = door.OperationStateModule.CaptureState();
        Require(door.OperationStateModule.TryRestoreState(1, held, out failure)
            && door.OperationStateModule.CaptureState() == held, "Held door save roundtrip failed.");
        Require(!door.OperationStateModule.TryRestoreState(1, "{}", out _)
            && door.OperationStateModule.CaptureState() == held, "Invalid restore mutated held door.");
        ClickOperation();
        yield return Wait(() => !door.IsOpen && !door.IsHeldOpen, "UI automatic close did not release hold.");
        info.CloseDispaly();
        report.Add("main-building-UI-hold-close-module-roundtrip-invalid-atomic=PASS");

        Require(move.TryStartSystemMove(center, DoorAccessOverrideKind.None, out failure), failure);
        yield return Wait(() => door.IsOpen, "Cancel scenario did not begin passage.");
        Vector3 cancelledAt = actor.transform.position;
        move.CancelActiveMovement();
        Require(!move.OccupiesDoorPassage(door) && actor.transform.position == cancelledAt,
            "Cancel retained passage or teleported actor.");
        yield return null;
        if (door.ContainsCell(grid.GetXY(actor.transform.position))) Require(door.IsOpen, "Cancelled occupant was closed in.");
        Require(move.TryStartSystemMove(right, DoorAccessOverrideKind.None, out failure), failure);
        yield return Wait(() => actor.GetNowXY() == right && !move.IsSystemMoveInProgress && !door.IsOpen,
            "Cancelled passage did not recover.");
        report.Add("cancel-no-teleport-no-passage-leak-and-resume=PASS");
        yield return VerifyWildlife(scope, grid, door, left, center, right, query, command, report);
        yield return VerifyWorldMovement(actor, move, grid, door, left, center, right, command, report);
        yield return VerifyExit(scope, actor, move, grid, door, left, center, command, report);
        report.Add("scope=authored-InteriorDoor-main-UI-real-actor-movement;initial-building-is-fixture-placement");
    }

    private static IEnumerator VerifyExit(DungeonRuntimeLifetimeScope scope, CharacterActor actor, AbilityMove move,
        Grid grid, Door door, Vector2Int outside, Vector2Int threshold, IDoorAccessCommandService command, List<string> report)
    {
        Require(scope.Container.Resolve<ICharacterSpawnerProvider>().TryGetSpawner(out CharacterSpawner spawner)
            && spawner != null, "Real spawner missing for exit boundary.");
        Require(spawner.TryGetEntryGridPosition(out var entry) && entry != threshold,
            "Exit fixture must cross a destination door, not test source-cell egress.");
        var serialized = new SerializedObject(spawner);
        var originalDoor = serialized.FindProperty("entryDoorPoint").objectReferenceValue;
        var originalOutside = serialized.FindProperty("outsideSpawnPoint").objectReferenceValue;
        var doorPoint = new GameObject("WimExitThresholdFixture");
        var outsidePoint = new GameObject("WimExitOutsideFixture");
        doorPoint.transform.position = grid.GetWorldPos(threshold);
        outsidePoint.transform.position = grid.GetWorldPos(outside);
        string id = CharacterPersistentIdentity.Require(actor).Value;
        try
        {
            // Change only this live fixture's serialized waypoint references.
            // The production spawner, entry-grid authority and exit routine remain real.
            serialized.FindProperty("entryDoorPoint").objectReferenceValue = doorPoint.transform;
            serialized.FindProperty("outsideSpawnPoint").objectReferenceValue = outsidePoint.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(move.TryStartSystemMove(entry, DoorAccessOverrideKind.DirectCommand, out string failure), failure);
            yield return Wait(() => actor.GetNowXY() == entry && !move.IsSystemMoveInProgress, "Exit actor did not reach staging.");
            // The preceding real entry deliberately reactivates AI in SetLifecycleState(Active).
            // Pause decisions again so a new unrelated job cannot race explicit-cancel assertions.
            actor.SetAiPaused(true);
            actor.Brain.StopAllAiForLifecycleTransition("qa:wim:exit-after-entry");
            var work = actor.GetAbility<AbilityWork>();
            Require(work != null && !actor.IsOwner, "Exit fixture requires actual non-owner worker.");
            work.BeginOffDuty("qa:wim:door-exit");
            Require(work.IsOffDuty, "Actual off-duty command failed.");
            Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.Deny), "Cannot deny exit door.");
            long attempts = spawner.ExitInteractionAttemptCount;
            move.StartExitDungeon();
            actor.Brain.RequestImmediateReplan();
            Require(move.HasActiveMovementRoutineForDiagnostics,
                "Scheduler wake-up cancelled the domain-owned exit transit.");
            Vector3 cancelledAt = actor.transform.position;
            move.CancelActiveMovement();
            yield return null;
            Require(actor.transform.position == cancelledAt && !move.HasActiveMovementRoutineForDiagnostics
                && spawner.ExitInteractionAttemptCount == attempts, "Explicit exit cancellation moved or handed off the actor.");
            move.StartExitDungeon();
            yield return Wait(() => !move.HasActiveMovementRoutineForDiagnostics
                && move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied, "Exit did not terminate denied raw passage.");
            Require(spawner.ExitInteractionAttemptCount == attempts && actor.isActiveAndEnabled
                && actor.GetNowXY() != threshold, "Denied exit called the spawner or reached forbidden door.");
            Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.GroupDefault), "Cannot allow exit retry.");
            move.StartExitDungeon();
            yield return Wait(() => spawner.ExitInteractionAttemptCount > attempts && !move.HasActiveMovementRoutineForDiagnostics,
                "Allowed exit did not hand off after real traversal.");
            Require(spawner.ExitInteractionAttemptCount == attempts + 1
                && actor.transform.position == outsidePoint.transform.position, "Exit handoff was duplicated or happened before destination.");
            report.Add("actual-exit-command-denied-no-spawner-handoff-retry-physical-arrival-once=PASS");
            report.Add("exit-scheduler-wakeup-retained-explicit-cancel-no-arrival=PASS");
            report.Add("exit-scope=real-off-duty-worker-StartExitDungeon-and-spawner;live-waypoint-fixture-restored;not-visitor-population-release");
            yield return VerifyReturn(scope, actor, move, grid, door, command, report);
        }
        finally
        {
            report.Add("exit-terminal-trace=position:" + actor.transform.position
                + ";grid:" + actor.GetNowXY() + ";failure:" + move.LastGridMoveFailureReason
                + ";blocked:" + move.LastGridMoveWasBlocked + ";active:" + move.HasActiveMovementRoutineForDiagnostics
                + ";lifecycle:" + actor.CurrentLifecycleState + ";handoffs:" + spawner.ExitInteractionAttemptCount
                + ";offDuty:" + actor.GetAbility<AbilityWork>().IsOffDuty
                + ";entry:" + entry + ";threshold:" + threshold
                + ";cancel:" + move.LastMovementCancellationSourceForDiagnostics
                + ";actionCancel:" + move.LastActionMovementCancellationReasonForDiagnostics);
            move.CancelActiveMovement();
            command.SetIndividualRule(door, id, DoorAccessIndividualRule.GroupDefault);
            serialized.Update();
            serialized.FindProperty("entryDoorPoint").objectReferenceValue = originalDoor;
            serialized.FindProperty("outsideSpawnPoint").objectReferenceValue = originalOutside;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.Destroy(doorPoint);
            UnityEngine.Object.Destroy(outsidePoint);
        }
    }

    private static IEnumerator VerifyReturn(DungeonRuntimeLifetimeScope scope, CharacterActor actor, AbilityMove move,
        Grid grid, Door door, IDoorAccessCommandService command, List<string> report)
    {
        var returns = scope.Container.Resolve<IExpeditionReturnService>();
        Require(scope.Container.Resolve<IWorldDropZoneQuery>().TryGetVisitorEntryPoint(out var entry),
            "Actual return entry query missing.");
        string id = CharacterPersistentIdentity.Require(actor).Value;
        actor.SetAiPaused(true);
        actor.Brain.StopAllAiForLifecycleTransition("qa:wim:expedition-return");
        Require(actor.BeginExpedition(), "Actual expedition lifecycle did not begin.");
        Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.Deny), "Cannot deny return doorway.");
        int completed = 0, duplicateCompleted = 0;
        Require(!returns.TryBeginReturn(null, true, _ => duplicateCompleted++, out _)
            && duplicateCompleted == 0, "Rejected return invoked completion.");
        var port = scope.Container.Resolve<IOffenseExpeditionReturnPort>();
        var arrivals = scope.Container.Resolve<IOffenseReturnArrivalRuntime>();
        const string expeditionId = "qa:wim:door-return";
        port.Begin(expeditionId);
        Require(port.TryBeginMemberReturn(expeditionId, actor, outcome =>
            { Require(outcome == ExpeditionReturnOutcome.Arrived, "Non-arrival reported as arrival."); completed++; }),
            "Actual return port rejected member.");
        string targetId = scope.Container.Resolve<IOffenseCampaignCatalog>().Targets.First().id;
        Require(arrivals.QueueArrival(expeditionId, targetId, OffenseReturnArrivalKind.SpecialWildlife, 1) == 1,
            "Cannot observe actual unsealed arrival barrier.");
        OffenseReturnArrivalState Barrier() => arrivals.Arrivals.Single(value => value.expeditionId == expeditionId);
        Require(Barrier().returningMembers == 1 && Barrier().materializedIds.Count == 0, "Return barrier registration incorrect.");
        yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied,
            "Actual return did not encounter denied door.");
        Require(completed == 0 && actor.CurrentLifecycleState == CharacterLifecycleState.ReturningExpedition
            && actor.GetNowXY() != entry.GridPosition, "Denied return falsely arrived or released lifecycle.");
        Vector3 waitingAt = actor.transform.position;
        move.CancelActiveMovement();
        yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.Cancelled,
            "Return segment cancellation was not reported.");
        Require(actor.transform.position == waitingAt && completed == 0 && Barrier().returningMembers == 1,
            "Movement cancellation teleported or settled the retained return.");
        yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied,
            "Retained return did not retry the same denied segment.");
        waitingAt = actor.transform.position;
        Require(!port.TryBeginMemberReturn(expeditionId, actor, _ => duplicateCompleted++)
            && !port.TryBeginMemberReturn(expeditionId, null, _ => duplicateCompleted++)
            && Barrier().returningMembers == 1 && duplicateCompleted == 0,
            "Rejected duplicate/null request released the existing party barrier.");
        var exterior = returns as ExteriorActivityRuntime;
        Require(exterior != null, "Registered return service is not the production exterior runtime.");
        exterior.BeginRestoreCandidate();
        exterior.DiscardRestoreCandidate();
        Require(!returns.TryBeginReturn(actor, true, _ => duplicateCompleted++, out string failure)
            && failure == "return-already-in-progress" && actor.transform.position == waitingAt,
            "Duplicate return changed the active transit.");
        Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.GroupDefault), "Cannot restore return access.");
        yield return Wait(() => completed == 1, "Allowed return never completed.");
        actor.SetAiPaused(true);
        Require(actor.GetNowXY() == entry.GridPosition && actor.CurrentLifecycleState == CharacterLifecycleState.Active
            && duplicateCompleted == 0 && Barrier().returningMembers == 0 && Barrier().materializedIds.Count == 0,
            "Return completion occurred without actual arrival or party barrier settlement.");
        yield return null;
        Require(completed == 1, "Return callback repeated.");
        Require(actor.BeginExpedition(), "Cannot begin a second real expedition lifecycle.");
        Require(returns.TryBeginReturn(actor, true, outcome =>
            { Require(outcome == ExpeditionReturnOutcome.Arrived, "Second return was not an arrival."); completed++; }, out failure),
            "Completed return retained stale ownership: " + failure);
        yield return Wait(() => completed == 2, "Second return did not complete exactly once.");
        actor.SetAiPaused(true);
        Require(actor.GetNowXY() == entry.GridPosition && duplicateCompleted == 0,
            "Second return did not physically arrive.");
        report.Add("actual-expedition-return-denied-wait-release-arrival-once-duplicate-rejected=PASS");
        report.Add("return-discarded-restore-candidate-preserves-transit-completion-releases-owner=PASS");
        report.Add("actual-return-port-barrier-cancel-retains-one-duplicate-no-decrement-arrival-zero=PASS");
        yield return VerifyDepartureAndFatalReturn(scope, actor, move, door, command, report);
        report.Add("return-scope=registered-return-port-service-arrival-barrier-real-lifecycle-grid;unsealed-reward-fixture-no-materialization;full-restore-resume-pending");
    }

    private static IEnumerator VerifySavedReturn(DungeonRuntimeLifetimeScope scope, CharacterActor actor, Grid grid,
        Door door, Vector2Int outside, Vector2Int threshold, IDoorAccessCommandService command, List<string> report)
    {
        var runtime = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>();
        var saves = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        var target = scope.Container.Resolve<IOffenseCampaignCatalog>().Targets.First();
        Require(runtime != null && runtime.ActiveExpeditions.Count == 0, "Return restore fixture needs an idle expedition runtime.");
        Require(scope.Container.Resolve<ICharacterSpawnerProvider>().TryGetSpawner(out var spawner), "Spawner missing.");
        var serialized = new SerializedObject(spawner);
        var oldDoor = serialized.FindProperty("entryDoorPoint").objectReferenceValue;
        var oldOutside = serialized.FindProperty("outsideSpawnPoint").objectReferenceValue;
        var doorPoint = new GameObject("WimReturnRestoreDoor");
        var outsidePoint = new GameObject("WimReturnRestoreOutside");
        doorPoint.transform.position = grid.GetWorldPos(threshold);
        outsidePoint.transform.position = grid.GetWorldPos(outside);
        string id = CharacterPersistentIdentity.Require(actor).Value;
        const string runId = "qa:wim:return-restore";
        try
        {
            serialized.FindProperty("entryDoorPoint").objectReferenceValue = doorPoint.transform;
            serialized.FindProperty("outsideSpawnPoint").objectReferenceValue = outsidePoint.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.Deny), "Cannot deny return restore door.");
            actor.SetAiPaused(true);
            actor.Brain.StopAllAiForLifecycleTransition("qa:wim:return-restore");
            Require(actor.BeginExpedition(), "Cannot enter fixture expedition lifecycle.");
            var run = new OffenseExpeditionRun(runId, target, new[] { actor }, 1f);
            runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(new[] { run }, runtime.ResultHistory));
            Require(runtime.TryRetreat(runId, out string failure), failure);
            var move = actor.GetAbility<AbilityMove>();
            yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied, "Return did not wait.");
            Require(runtime.ActiveExpeditions.Contains(run) && run.ReturnPending, "Pending return removed before settlement.");
            Vector3 position = actor.transform.position;
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.SetAiPaused(true);
            actor.Brain.StopAllAiForLifecycleTransition("qa:wim:return-owner-transfer");
            yield return Wait(() => run.ReturnProgress.Single().LastFailure == "return-start-unavailable", "Ownership transfer not exposed.");
            Require(actor.transform.position == position && !run.ReturnFinalized
                && run.ReturnProgress.Single().Stage == ExpeditionReturnStage.ToDoor, "Rejected resume changed progress/position.");
            actor.SetLifecycleState(CharacterLifecycleState.ReturningExpedition);
            yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied
                && run.ReturnProgress.Single().LastFailure.Length == 0, "Restored ownership did not resume.");
            var checkpoint = saves.CaptureAll();
            Vector2Int savedCell = grid.GetXY(actor.transform.position);
            var saved = JsonUtility.FromJson<DungeonOffenseAggregateSaveData>(checkpoint.Single(value => value.sectionId == OffenseAggregateSaveSection.Id).payloadJson);
            var savedRun = saved.expedition.activeExpeditions.Single(value => value.expeditionId == runId);
            Require(savedRun.returnPending && savedRun.returnProgress.Single().stage == ExpeditionReturnStage.ToDoor,
                "Save omitted pending return stage.");
            savedRun.returnProgress[0].stage = 0;
            Vector3 beforeInvalidRestorePosition = actor.transform.position;
            ExpeditionReturnStage beforeInvalidRestoreStage = run.ReturnProgress.Single().Stage;
            bool rejected = false;
            try { OffenseAggregateSaveValidation.BuildRestorePlan(saved); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Invalid saved return stage was accepted.");
            Require(actor.transform.position == beforeInvalidRestorePosition,
                "Invalid saved return validation mutated the live actor position.");
            Require(run.ReturnProgress.Single().Stage == beforeInvalidRestoreStage,
                "Invalid saved return validation mutated the live return stage.");
            var restoreReport = new DungeonGameRestoreReport();
            Require(saves.RestoreAll(checkpoint, restoreReport) && restoreReport.Success,
                "Return checkpoint restore failed: " + string.Join(" | ", restoreReport.Errors));
            yield return null;
            Require(scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out grid), "Restored grid missing.");
            actor = UnityEngine.Object.FindObjectsByType<CharacterActor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(value => value.Identity?.PersistentId == id);
            move = actor.GetAbility<AbilityMove>();
            door = grid.GetGridCell(threshold).GetOccupant(GridLayer.Building) as Door;
            run = runtime.ActiveExpeditions.Single(value => value.ExpeditionId == runId);
            report.Add("return-restore-position=before:" + position + ";savedCell:" + savedCell
                + ";after:" + actor.transform.position + ";stage:" + run.ReturnProgress.Single().Stage
                + ";lifecycle:" + actor.CurrentLifecycleState);
            Require(actor.GetNowXY() == savedCell && run.ReturnPending
                && run.ReturnProgress.Single().Stage == ExpeditionReturnStage.ToDoor
                && actor.CurrentLifecycleState == CharacterLifecycleState.ReturningExpedition,
                "Restore lost the canonical saved cell, stage or return lifecycle.");
            yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied,
                "Saved return did not resume at denied door.");
            Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.GroupDefault), "Cannot release restored door.");
            yield return Wait(() => !runtime.ActiveExpeditions.Any(value => value.ExpeditionId == runId), "Restored return never settled.");
            actor.SetAiPaused(true);
            Require(scope.Container.Resolve<IWorldDropZoneQuery>().TryGetVisitorEntryPoint(out var entry)
                && actor.GetNowXY() == entry.GridPosition
                && runtime.ResultHistory.Count(value => value.expeditionId == runId) == 1,
                "Restored return settled without arrival or duplicated result.");
            report.Add("actual-retreat-keeps-run-until-return-finalization=PASS");
            report.Add("lifecycle-transfer-start-rejection-retains-position-stage-resumes=PASS");
            report.Add("current-format-invalid-stage-atomic-rejection=PASS");
            report.Add("whole-registry-return-checkpoint-current-position-resume-finalizer-once=PASS");
            report.Add("position-contract=existing-character-world-gridX/gridY;subcell-offset-not-persisted;no-expedition-specific-position-authority");
            report.Add("scope=real-retreat-return-save-registry-finalizer;initial-party-run-and-waypoints-fixtures;empty-supply-failed-expedition-no-success-reward");
        }
        finally
        {
            serialized.Update();
            serialized.FindProperty("entryDoorPoint").objectReferenceValue = oldDoor;
            serialized.FindProperty("outsideSpawnPoint").objectReferenceValue = oldOutside;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.Destroy(doorPoint); UnityEngine.Object.Destroy(outsidePoint);
        }
    }

    private static IEnumerator VerifyDepartureAndFatalReturn(DungeonRuntimeLifetimeScope scope, CharacterActor actor,
        AbilityMove move, Door door, IDoorAccessCommandService command, List<string> report)
    {
        var target = scope.Container.Resolve<IOffenseCampaignCatalog>().Targets.First();
        var run = new OffenseExpeditionRun("qa:wim:door-departure", target, new[] { actor }, 1f);
        var departure = scope.Container.Resolve<IExpeditionDepartureService>();
        string id = CharacterPersistentIdentity.Require(actor).Value;
        int completed = 0, duplicate = 0;
        Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.Deny), "Cannot deny departure.");
        Require(departure.TryBeginDeparture(run, run.MemberActors, () => true, () => completed++, out string failure), failure);
        Require(!departure.TryBeginDeparture(run, run.MemberActors, () => true, () => duplicate++, out failure)
            && failure == "expedition-departure-already-in-progress", "Duplicate departure accepted.");
        yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied,
            "Departure never reached denied door.");
        Vector3 before = actor.transform.position;
        move.CancelActiveMovement();
        yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.Cancelled,
            "Departure cancellation not observed.");
        Require(actor.transform.position == before && completed == 0 && !actor.IsOnExpedition,
            "Cancelled departure teleported/completed.");
        yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied,
            "Departure request was lost after segment cancellation.");
        Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.GroupDefault), "Cannot release departure.");
        yield return Wait(() => completed == 1, "Released departure did not finish.");
        Require(scope.Container.Resolve<IWorldDropZoneQuery>().TryGetVisitorEntryPoint(out var entry)
            && actor.transform.position == entry.OutsidePosition && actor.IsOnExpedition && duplicate == 0,
            "Departure completed before physical outside position.");
        report.Add("actual-departure-service-duplicate-reject-denied-cancel-retain-outside-complete-once=PASS");

        // The return transport/barrier are real. Only final economic side effects are
        // recorded so this fixture cannot grant a synthetic campaign victory/reward.
        var port = new RecordingReturnPort(scope.Container.Resolve<IOffenseExpeditionReturnPort>());
        var finalizer = new RecordingReturnFinalizer();
        var coordinator = new OffenseExpeditionReturnCoordinator(port, finalizer,
            scope.Container.Resolve<IGameEventBus>(), scope.Container.Resolve<ICharacterPerformanceQuery>());
        var results = new List<OffenseExpeditionResult>();
        Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.Deny), "Cannot deny fatal return.");
        int level = actor.Progression.Level, xp = actor.Progression.CurrentExperience;
        coordinator.Complete(run, true, string.Empty, results, () => { });
        yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied,
            "Coordinator return did not wait at door.");
        Require(finalizer.Count == 0 && port.ReleaseCount == 0
            && actor.Progression.Level == level && actor.Progression.CurrentExperience == xp,
            "Return finalized/awarded experience before arrival.");
        var arrivals = scope.Container.Resolve<IOffenseReturnArrivalRuntime>();
        Require(arrivals.QueueArrival(run.ExpeditionId, target.id, OffenseReturnArrivalKind.SpecialWildlife, 1) == 1,
            "Fatal return barrier fixture missing.");
        var barrier = arrivals.Arrivals.Single(value => value.expeditionId == run.ExpeditionId);
        Require(barrier.returningMembers == 1, "Fatal return was not pending.");
        before = actor.transform.position;
        actor.Die(CharacterDeathCauseCode.Combat, "qa:wim:return-death");
        yield return Wait(() => finalizer.Count == 1, "Dead return member left finalizer pending.");
        Require(actor.Stats.IsDead && actor.transform.position == before && port.DeadCount == 1
            && port.ReleaseCount == 1 && !port.HasSurvivor && barrier.returningMembers == 0
            && barrier.materializedIds.Count == 0 && !results.Single().members.Single().survived
            && actor.Progression.Level == level && actor.Progression.CurrentExperience == xp,
            "Death was treated as arrival/survival, awarded experience or duplicated settlement.");
        yield return null;
        Require(finalizer.Count == 1 && port.DeadCount == 1, "Fatal settlement repeated.");
        report.Add("actual-return-death-terminal-barrier-zero-no-teleport-no-success-experience=PASS");
        report.Add("return-final-snapshot-dead-last-survivor-false-finalizer-once=PASS");
        report.Add("return-finalizer-scope=real-coordinator-and-transport;economic-release-and-finalizer-recording-ports;no-campaign-rewards-issued");
    }

    private sealed class RecordingReturnPort : IOffenseExpeditionReturnPort
    {
        private readonly IOffenseExpeditionReturnPort inner;
        internal int ReleaseCount, DeadCount;
        internal bool HasSurvivor;
        internal RecordingReturnPort(IOffenseExpeditionReturnPort inner) => this.inner = inner;
        public void Begin(string id) => inner.Begin(id);
        public bool TryBeginMemberReturn(string id, CharacterActor actor, Action<ExpeditionReturnOutcome> completed,
            ExpeditionReturnProgress progress = null)
            => inner.TryBeginMemberReturn(id, actor, outcome =>
            { if (outcome == ExpeditionReturnOutcome.Dead) DeadCount++; completed(outcome); }, progress);
        public void EndMemberImmediately(CharacterActor actor, bool survived) => inner.EndMemberImmediately(actor, survived);
        public void HandleMemberDeath(CharacterActor actor) => inner.HandleMemberDeath(actor);
        public void ReleaseResources(OffenseExpeditionRun run, bool hasSurvivor)
            { ReleaseCount++; HasSurvivor = hasSurvivor; }
        public void Seal(string id) { } // Intentionally leave synthetic reward fixture unsealed.
    }

    private sealed class RecordingReturnFinalizer : IOffenseExpeditionResultFinalizer
    {
        internal int Count;
        public OffenseExpeditionResult Finalize(OffenseExpeditionRun run, OffenseExpeditionResult result,
            List<OffenseExpeditionResult> history)
            { Count++; history.Add(result); return result; }
    }

    private static IEnumerator VerifyWorldMovement(CharacterActor actor, AbilityMove move, Grid grid, Door door,
        Vector2Int left, Vector2Int center, Vector2Int right, IDoorAccessCommandService command, List<string> report)
    {
        string id = CharacterPersistentIdentity.Require(actor).Value;
        Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.Deny), "Cannot deny raw transit.");
        Vector3 before = actor.transform.position;
        // The door is intermediate, not the target. Force the final-position
        // commit to cross it in a single stride and check exact non-movement.
        yield return move.Move2PosBySpeed(grid.GetWorldPos(left), 1000f);
        Require(move.LastGridMoveWasBlocked && move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied
            && actor.transform.position == before && !door.IsOpen, "Raw final commit skipped denied intermediate door.");
        Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.GroupDefault), "Cannot allow raw transit.");
        yield return move.Move2PosBySpeed(grid.GetWorldPos(center));
        Require(move.LastGridMoveFailureReason == GridMoveFailureReason.None && actor.GetNowXY() == center
            && door.IsOpen, "Raw movement failed to open/occupy door.");
        Require(command.SetHeldOpen(door, false) && door.IsOpen, "Raw occupant was closed in.");
        yield return move.Move2PosBySpeed(grid.GetWorldPos(right));
        yield return Wait(() => !door.IsOpen, "Raw departure did not auto-close.");
        report.Add("raw-world-intermediate-door-final-stride-denial-open-occupancy-exit=PASS");

        Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.Deny), "Cannot deny lifecycle entry.");
        move.StartEnterDungeon(grid.GetWorldPos(center), left);
        yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied,
            "Entry did not expose denied door wait.");
        Require(actor.CurrentLifecycleState == CharacterLifecycleState.EnteringDungeon
            && actor.GetNowXY() != left && move.HasActiveMovementRoutineForDiagnostics,
            "Denied entry falsely completed or lost its transit owner.");
        before = actor.transform.position;
        move.CancelActiveMovement();
        yield return null;
        Require(actor.transform.position == before && !move.HasActiveMovementRoutineForDiagnostics
            && actor.CurrentLifecycleState != CharacterLifecycleState.Active,
            "Cancelled denied entry moved or published arrival.");
        move.StartEnterDungeon(grid.GetWorldPos(center), left);
        yield return Wait(() => move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied,
            "Entry retry did not retain denial.");
        Require(command.SetIndividualRule(door, id, DoorAccessIndividualRule.GroupDefault), "Cannot release entry wait.");
        yield return Wait(() => actor.CurrentLifecycleState == CharacterLifecycleState.Active
            && actor.GetNowXY() == left && !move.HasActiveMovementRoutineForDiagnostics, "Allowed entry did not actually arrive.");
        yield return Wait(() => !door.IsOpen, "Entry passage leaked open state.");
        report.Add("entry-lifecycle-denied-wait-cancel-no-false-arrival-retry-real-arrival=PASS");
        report.Add("raw-scope=actual-AbilityMove-world-motion-and-entry;exit-invasion-expedition-fault-consumers-not-yet-run");
    }

    private static IEnumerator VerifyWildlife(DungeonRuntimeLifetimeScope scope, Grid grid, Door door,
        Vector2Int left, Vector2Int center, Vector2Int right, IDoorAccessQuery query,
        IDoorAccessCommandService command, List<string> report)
    {
        var species = scope.Container.Resolve<IWildlifeSpeciesCatalogProvider>().All
            .Where(s => s.CanEnterDungeon).OrderBy(s => s.SpeciesId, StringComparer.Ordinal).FirstOrDefault();
        Require(species != null, "No authored dungeon-capable wildlife species.");
        Require(grid.GetGridCell(left).CanOccupy(GridLayer.Wildlife)
            && grid.GetGridCell(center).CanOccupy(GridLayer.Wildlife)
            && grid.GetGridCell(right).CanOccupy(GridLayer.Wildlife), "Wildlife fixture corridor occupied.");
        var fixture = new GameObject("WimDoorWildlifeFixture");
        WildlifeActor animal = fixture.AddComponent<WildlifeActor>();
        var clock = scope.Container.Resolve<IGameClock>();
        try
        {
            animal.ConfigureRuntimeServices(scope.Container.Resolve<IGridPathSearchBroker>(),
                scope.Container.Resolve<ICharacterAiWorldRegistry>(), clock,
                scope.Container.Resolve<IRandomStreamProvider>(), query);
            animal.Initialize(grid, species, "qa:wim:door-wildlife", left);
            var context = GridTraversalContext.ForWildlife(animal.WildlifeId);
            Require(command.SetGroupAllowed(door, DoorAccessGroup.Wildlife, false), "Cannot deny wildlife group.");
            Vector3 before = animal.transform.position;
            Require(!query.CanTraverse(grid, center, context, out _) && !animal.TrySetPath(center, clock.Time)
                && animal.transform.position == before && !door.IsOpen, "Wildlife denied-path mutation/bypass.");
            Require(command.SetGroupAllowed(door, DoorAccessGroup.Wildlife, true), "Cannot allow wildlife group.");
            Require(query.CanTraverse(grid, center, context, out _) && !door.IsOpen,
                "Wildlife path query opened door.");
            Require(animal.TrySetPath(center, clock.Time) && animal.IsMoving && door.IsOpen
                && animal.OccupiesDoorPassage(door), "Actual wildlife step failed to open/protect door.");
            Require(command.SetHeldOpen(door, false) && door.IsOpen, "Door closed on incoming wildlife.");
            yield return TickWildlifeToRest(animal, clock);
            Require(animal.GridPosition == center && door.IsOpen, "Wildlife did not occupy open door.");
            Require(command.SetHeldOpen(door, false) && door.IsOpen, "Door closed on stationary wildlife.");
            Require(animal.TrySetPath(right, clock.Time) && animal.OccupiesDoorPassage(door),
                "Wildlife exit did not protect source door.");
            Require(command.SetHeldOpen(door, false) && door.IsOpen, "Door closed before wildlife exited.");
            yield return TickWildlifeToRest(animal, clock);
            yield return Wait(() => !door.IsOpen, "Door did not close after wildlife exit.");
            Require(animal.GridPosition == right && !animal.OccupiesDoorPassage(door), "Wildlife passage leaked.");
            Require(command.SetHeldOpen(door, true)
                && command.SetGroupAllowed(door, DoorAccessGroup.Wildlife, false), "Cannot set held/denied case.");
            before = animal.transform.position;
            Require(!animal.TrySetPath(center, clock.Time) && animal.transform.position == before,
                "Held-open door bypassed wildlife permission.");
            Require(command.SetHeldOpen(door, false), "Cannot release fixture hold.");
            report.Add("wildlife-real-step-deny-open-incoming-occupied-exit-close-held-deny=PASS");
            report.Add("wildlife-scope=" + species.SpeciesId
                + ";authored-species-real-grid-broker-access-adapter;fixture-spawn-and-explicit-Actor.Tick;not-natural-behavior");
        }
        finally
        {
            animal.PrepareForDespawn();
            UnityEngine.Object.Destroy(fixture);
        }
    }

    private static IEnumerator TickWildlifeToRest(WildlifeActor animal, IGameClock clock)
    {
        // This actor is registered with the real grid/subject query, not the population
        // scheduler. Tick its production movement once per frame, without a second owner.
        float start = Time.realtimeSinceStartup;
        while (animal.IsMoving && Time.realtimeSinceStartup - start < 10f)
        {
            animal.Tick(clock.DeltaTime);
            yield return null;
        }
        Require(!animal.IsMoving, "Wildlife step timed out.");
    }

    private static void ClickOperation()
    {
        var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .SingleOrDefault(b => b.gameObject.activeInHierarchy && b.transform.parent?.name == "DoorOperation");
        Require(button != null && button.IsInteractable() && EventSystem.current != null, "Door operation UI not available.");
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }
    private static IEnumerator Wait(Func<bool> condition, string failure)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - start < 30f) yield return null;
        Require(condition(), failure);
    }
    private static void Require(bool condition, string failure)
    { if (!condition) throw new InvalidOperationException(failure); }
}
#endif
