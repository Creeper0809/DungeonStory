#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class DoorHeldOpenOutcomeDebugScenarios
{
    // Controlled commands on the official world; no synthetic occupancy or LateUpdate invocation.
    public static IEnumerator VerifyTraversalPlayMode(
        ICollection<string> warnings, ICollection<string> errors)
    {
        var report = new DoorTraversalReport();
        var pending = new Stack<IEnumerator>();
        pending.Push(RunTraversalChecks(report));
        try
        {
            while (pending.Count > 0)
            {
                object next = null;
                bool failed = false;
                try
                {
                    IEnumerator current = pending.Peek();
                    if (!current.MoveNext())
                    {
                        (pending.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    next = current.Current;
                    if (next is IEnumerator nested)
                    {
                        pending.Push(nested);
                        continue;
                    }
                    report.ObservePassageFrame();
                }
                catch (Exception exception)
                {
                    report.failure = exception.ToString();
                    failed = true;
                }
                if (failed) break;
                yield return next;
            }
        }
        finally
        {
            while (pending.Count > 0)
                (pending.Pop() as IDisposable)?.Dispose();
        }
        report.warnings = warnings.ToList();
        report.errors = errors.ToList();
        report.passed = report.checksCompleted && report.failure.Length == 0
            && warnings.Count == 0 && errors.Count == 0;
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output), "A door traversal output directory is required.");
        using (var writer = new StreamWriter(new FileStream(
            Path.Combine(output, "door-traversal-bdcd01-report.json"),
            FileMode.CreateNew, FileAccess.Write, FileShare.Read)))
            writer.Write(JsonUtility.ToJson(report, true));
        Debug.Log("U0_BDC_D01_DOOR_TRAVERSAL=" + (report.passed ? "PASS" : "FAIL"));
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(report.passed ? 0 : 1);
    }

    private static IEnumerator RunTraversalChecks(DoorTraversalReport report)
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
            && Environment.GetEnvironmentVariable("DUNGEONSTORY_U0_BDCD01_DOOR_TRAVERSAL_VERIFY") == "1",
            "Door traversal verification requires its explicit isolated PlayMode selector.");
        DungeonRuntimeLifetimeScope scope = null;
        float readyDeadline = Time.realtimeSinceStartup + 45f;
        while (scope == null && Time.realtimeSinceStartup < readyDeadline)
        {
            scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(value => value?.Container != null);
            if (scope == null) yield return null;
        }
        Require(scope != null, "Official gameplay container did not become ready.");
        IOwnerRunManagerProvider owners = scope.Container.Resolve<IOwnerRunManagerProvider>();
        Require(owners.TryGetManager(out OwnerRunManager manager), "Official owner manager is missing.");
        if (manager.CurrentOwnerActor == null)
        {
            report.preparation.Add("controlled-start-party-bootstrap="
                + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            for (int frame = 0; frame < 5; frame++) yield return null;
        }
        Require(owners.TryGetManager(out manager) && manager.CurrentOwnerActor != null,
            "Official start-party command did not publish an owner.");
        ICharacterAiWorldRegistry world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        Require(world.TryGetGrid(out Grid grid) && grid != null, "Official grid is missing.");
        IGameSpeedController speed = scope.Container.Resolve<IGameSpeedController>();
        var pausedActors = world.Characters.Where(value => value?.Lifecycle != null)
            .Select(value => (actor: value, paused: value.Lifecycle.IsAiPaused)).ToArray();
        bool previousPause = speed.IsPaused;
        int previousSpeed = speed.Speed;
        CharacterActor actor = manager.CurrentOwnerActor;
        Vector3 previousPosition = actor.transform.position;
        AbilityMove move = actor.GetAbility<AbilityMove>();
        Require(move != null && !actor.IsDead && !actor.IsOnExpedition,
            "Official owner has no available movement ability.");
        try
        {
            speed.SetPaused(true);
            foreach (var entry in pausedActors)
            {
                entry.actor.Lifecycle.SetAiPaused(true);
                entry.actor.GetAbility<AbilityMove>()?.CancelActiveMovement("bdcd01-controlled-preparation");
            }
            report.preparation.Add("AI pause and initial position are test arrangement, not narrative actions.");
            IBuildingDoorTraversalSubjectPort passage = scope.Container.Resolve<IBuildingDoorTraversalSubjectPort>();
            Require(TryFindTraversalDoor(world, grid, passage, out Door door,
                    out Vector2Int start, out Vector2Int end),
                "No unoccupied official door with two walkable approach cells was available.");
            report.actorId = CharacterPersistentIdentity.Require(actor).Value;
            report.doorId = door.PersistentInstanceId.Value;
            report.startX = start.x; report.startY = start.y;
            report.doorX = door.centerPos.x; report.doorY = door.centerPos.y;
            report.endX = end.x; report.endY = end.y;
            report.Door = door; report.Move = move;
            GameplayOutcomeLedger ledger = scope.Container.Resolve<GameplayOutcomeLedger>();
            GameplayEntityId subject = new(new GameplayEntityKindId("facility"), report.doorId);
            IDoorAccessCommandService commands = scope.Container.Resolve<IDoorAccessCommandService>();
            IDoorAccessQuery access = scope.Container.Resolve<IDoorAccessQuery>();
            Require(access.ResolveSubject(GridTraversalContext.ForCharacter(
                    CharacterPersistentIdentity.Require(actor))).Character == actor,
                "Door permission query did not resolve the exact official actor.");
            actor.transform.position = grid.GetWorldPos(start);
            Require(DoorPolicyResults(ledger, subject).Count == 0 && !door.IsOpen && !door.IsHeldOpen
                && door.ManualOperationRevision == 0L, "Door did not have a fresh manual-policy baseline.");
            Require(commands.SetHeldOpen(door, false) && DoorPolicyResults(ledger, subject).Count == 0
                && door.ManualOperationRevision == 0L, "Initial no-op emitted a manual outcome.");
            Require(commands.SetHeldOpen(door, true) && door.IsOpen && door.IsHeldOpen
                && DoorPolicyResults(ledger, subject).Count == 1 && door.ManualOperationRevision == 1L,
                "Manual hold did not commit exactly one result.");
            yield return null; yield return null;
            Require(door.IsOpen && door.IsHeldOpen && !passage.IsDoorPassageOccupied(door),
                "Unoccupied held-open door did not survive actual frame maintenance.");
            Require(commands.SetHeldOpen(door, true) && DoorPolicyResults(ledger, subject).Count == 1,
                "Repeated hold emitted a duplicate result.");
            Require(commands.SetHeldOpen(door, false) && !door.IsOpen && !door.IsHeldOpen
                && DoorPolicyResults(ledger, subject).Count == 2 && door.ManualOperationRevision == 2L,
                "Unoccupied release did not close with one manual result.");
            report.policyCountBeforeTraversal = DoorPolicyResults(ledger, subject).Count;
            report.Stage = "enter";
            speed.SetSpeed(1); speed.SetPaused(false);
            yield return move.Move2GridPosition(door.centerPos);
            Require(move.LastGridMoveFailureReason == GridMoveFailureReason.None
                && grid.GetXY(actor.transform.position) == door.centerPos
                && report.enterPassageObserved && door.IsOpen && !door.IsHeldOpen
                && passage.IsDoorPassageOccupied(door),
                "Actual AbilityMove admission/arrival did not open and occupy the door.");
            report.policyCountAfterTraversal = DoorPolicyResults(ledger, subject).Count;
            Require(report.policyCountAfterTraversal == report.policyCountBeforeTraversal
                && door.ManualOperationRevision == 2L,
                "Movement admission created a manual result or revision.");
            report.Stage = "occupied";
            speed.SetPaused(true);
            Require(commands.SetHeldOpen(door, true) && DoorPolicyResults(ledger, subject).Count == 3,
                "Occupied hold did not create one new manual result.");
            Require(commands.SetHeldOpen(door, false) && door.IsOpen && !door.IsHeldOpen
                && DoorPolicyResults(ledger, subject).Count == 4 && door.ManualOperationRevision == 4L,
                "Occupied release closed unsafely or lost its exact manual result.");
            int occupiedFrame = Time.frameCount;
            yield return null; yield return null;
            report.occupiedFrames = Time.frameCount - occupiedFrame;
            report.occupiedReleaseStayedOpen = door.IsOpen && passage.IsDoorPassageOccupied(door);
            Require(report.occupiedFrames >= 2 && report.occupiedReleaseStayedOpen,
                "Actual LateUpdate closed an occupied, unheld door.");
            Require(commands.SetHeldOpen(door, false) && DoorPolicyResults(ledger, subject).Count == 4
                && door.ManualOperationRevision == 4L, "Occupied release replay was not a no-op.");
            report.operationPayload = door.OperationStateModule.CaptureState();
            var restored = new DoorOperationStateModule(() => { });
            Require(restored.TryRestoreState(restored.CurrentVersion, report.operationPayload, out string failure)
                && restored.IsOpen && !restored.IsHeldOpen && restored.ManualRevision == 4L
                && restored.PendingManualOutcome == null, "Fresh operation owner restore failed: " + failure);
            report.freshOperationOwnerRestored = true;
            report.policyCountBeforeAutoClose = DoorPolicyResults(ledger, subject).Count;
            report.physicalRevisionBeforeExit = door.OperationRevision;
            report.Stage = "exit";
            speed.SetPaused(false);
            yield return move.Move2GridPosition(end);
            Require(move.LastGridMoveFailureReason == GridMoveFailureReason.None
                && grid.GetXY(actor.transform.position) == end && report.exitPassageObserved
                && !move.OccupiesDoorPassage(door) && !passage.IsDoorPassageOccupied(door),
                "Actual exit did not release its passage lease and physical occupancy.");
            speed.SetPaused(true);
            int exitFrame = Time.frameCount;
            report.Stage = "closed";
            yield return null; yield return null;
            report.autoCloseFrames = Time.frameCount - exitFrame;
            report.autoClosed = !door.IsOpen && !door.IsHeldOpen;
            report.physicalRevisionAfterExit = door.OperationRevision;
            report.policyCountAfterAutoClose = DoorPolicyResults(ledger, subject).Count;
            Require(report.autoCloseFrames >= 2 && report.autoClosed
                && report.physicalRevisionAfterExit > report.physicalRevisionBeforeExit
                && report.policyCountAfterAutoClose == report.policyCountBeforeAutoClose
                && door.ManualOperationRevision == 4L,
                "Actual frame auto-close changed manual policy identity or failed to close.");
            report.outcomes = DoorPolicyResults(ledger, subject).OrderBy(value => value.ownerRevision).ToList();
            Require(report.outcomes.Count == 4
                && report.outcomes.Select(value => value.runId + ":" + value.sequence).Distinct().Count() == 4,
                "Manual results did not have four distinct completion identities.");
            for (int index = 0; index < report.outcomes.Count; index++)
            {
                GameplayOutcomeSnapshot result = report.outcomes[index];
                Require(result.ownerRevision == index + 1L && result.operationId
                    == DoorHeldOpenPendingSaveData.BuildOperationId(report.doorId, index + 1L)
                    && result.facts.Count == 3 && result.facts[0].value == DoorHeldOpenPendingSaveData.PolicyStatusKey
                    && result.facts[1].value == DoorHeldOpenPendingSaveData.PlayerPanelOrigin
                    && result.facts[2].value == result.operationId
                    && result.metrics.Count == 3 && result.metrics[0].value == (index % 2 == 0 ? 0d : 1d)
                    && result.metrics[1].value == (index % 2 == 0 ? 1d : 0d)
                    && result.metrics[2].value == index + 1L && result.status == GameplayOutcomeStatus.Succeeded,
                    "Manual result lost its exact revision, operation or player command origin.");
            }
            report.checksCompleted = true;
        }
        finally
        {
            move.CancelActiveMovement("bdcd01-verification-cleanup");
            actor.transform.position = previousPosition;
            foreach (var entry in pausedActors)
                if (entry.actor != null) entry.actor.Lifecycle.SetAiPaused(entry.paused);
            speed.SetSpeed(previousSpeed); speed.SetPaused(previousPause);
            report.Door = null; report.Move = null;
        }
    }

    private static bool TryFindTraversalDoor(ICharacterAiWorldRegistry world, Grid grid,
        IBuildingDoorTraversalSubjectPort passage, out Door door, out Vector2Int start, out Vector2Int end)
    {
        foreach (Door candidate in world.Buildings.OfType<Door>())
        {
            if (!candidate.CanAcceptManualOperation || candidate.Grid != grid || candidate.buildPoses.Count != 1
                || !ReferenceEquals(grid.GetGridCell(candidate.centerPos)?.GetOccupant(GridLayer.Building), candidate)
                || candidate.IsOpen || candidate.IsHeldOpen || candidate.ManualOperationRevision != 0L
                || candidate.OperationStateModule.PendingManualOutcome != null || passage.IsDoorPassageOccupied(candidate)) continue;
            foreach (Vector2Int direction in new[] { Vector2Int.right, Vector2Int.up })
            {
                Vector2Int from = candidate.centerPos - direction, to = candidate.centerPos + direction;
                if (!grid.IsWalkable(from) || !grid.IsWalkable(to) || !grid.IsWalkable(candidate.centerPos)
                    || grid.GetGridCell(from)?.GetOccupant(GridLayer.Building) is Door
                    || grid.GetGridCell(to)?.GetOccupant(GridLayer.Building) is Door) continue;
                door = candidate; start = from; end = to; return true;
            }
        }
        door = null; start = default; end = default; return false;
    }

    private static List<GameplayOutcomeSnapshot> DoorPolicyResults(GameplayOutcomeLedger ledger,
        GameplayEntityId subject) => ledger.GetForEntity(subject, OutcomeCursor.FirstPage(100), OutcomeFilter.All)
        .Items.Where(value => value.Exact != null && value.Exact.outcomeTypeId == PolicyStatusChangeOutcomeIds.Changed.Value
            && value.Exact.producerId == PolicyStatusChangeOutcomeIds.ProducerId)
        .Select(value => value.Exact).ToList();

    [Serializable]
    private sealed class DoorTraversalReport
    {
        public string scope = "controlled-official-world/AbilityMove/real-frame-door-maintenance;operation-section-restore";
        public bool trainingEligible = false, humanApprovalClaimed = false;
        public bool fullAuditClosure = false, fullWorldRestoreClaimed = false;
        public bool passed, checksCompleted, enterPassageObserved, exitPassageObserved;
        public bool occupiedReleaseStayedOpen, freshOperationOwnerRestored, autoClosed;
        public string failure = string.Empty, actorId = string.Empty, doorId = string.Empty, operationPayload = string.Empty;
        public int startX, startY, doorX, doorY, endX, endY, occupiedFrames, autoCloseFrames;
        public int policyCountBeforeTraversal, policyCountAfterTraversal, policyCountBeforeAutoClose, policyCountAfterAutoClose;
        public int physicalRevisionBeforeExit, physicalRevisionAfterExit;
        public List<string> preparation = new(), warnings = new(), errors = new();
        public List<GameplayOutcomeSnapshot> outcomes = new();
        [NonSerialized] public Door Door;
        [NonSerialized] public AbilityMove Move;
        [NonSerialized] public string Stage;
        private string observedStage;
        private float passageStartedAt, lastProgressGameTime;
        private int lastProgressFrame;
        private Vector3 lastPosition;
        internal void ObservePassageFrame()
        {
            if (Door == null || Move == null) return;
            if (Stage is "enter" or "exit")
            {
                float gameTime = Move.GameClockTimeForDiagnostics;
                if (observedStage != Stage)
                {
                    observedStage = Stage;
                    passageStartedAt = Time.realtimeSinceStartup;
                    lastPosition = Move.transform.position;
                    lastProgressGameTime = gameTime;
                    lastProgressFrame = Time.frameCount;
                }
                if ((Move.transform.position - lastPosition).sqrMagnitude > 0.000001f)
                {
                    lastPosition = Move.transform.position;
                    lastProgressGameTime = gameTime;
                    lastProgressFrame = Time.frameCount;
                }
                Require(gameTime - lastProgressGameTime < 10f || Time.frameCount - lastProgressFrame < 120,
                    "Door movement stalled despite simulation/frame progress; gameTime=" + gameTime);
                Require(Time.realtimeSinceStartup - passageStartedAt < 180f,
                    "Door verification exceeded its infrastructure limit; gameTime=" + gameTime
                    + ";framesSinceMotion=" + (Time.frameCount - lastProgressFrame));
            }
            else observedStage = null;
            if (!Move.OccupiesDoorPassage(Door)) return;
            Require(Door.IsOpen, "Actual frame maintenance closed an active movement passage.");
            if (Stage == "enter") enterPassageObserved = true;
            if (Stage == "exit") exitPassageObserved = true;
        }
    }

    public static void RunBDCD01FromBatch()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_BDCD01_DOOR_POLICY_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "BDC-D01 door-policy verification was not explicitly selected.");
        }

        string outputRoot = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            throw new InvalidOperationException(
                "A BDC-D01 audit remediation output directory is required.");
        }

        string reportPath = Path.Combine(
            outputRoot,
            "door-policy-bdcd01-report.txt");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "The BDC-D01 door-policy report already exists.");
        }

        try
        {
            RunOrThrow();
            WriteReport(reportPath, new[]
            {
                "U0_BDC_D01_DOOR_POLICY=PASS",
                "manual-g03-exact-outcome=true",
                "bulk-actual-production-path=true",
                "bulk-changed-count=1",
                "changed-target-revision=1",
                "source-revision-not-imported=true",
                "same-policy-revision-unchanged=true",
                "outside-room-revision-unchanged=true",
                "bulk-replay-changed-count=0",
                "save-restore-revision=1",
                "human-approval-claimed=false",
                "training-claimed=false",
                "full-audit-closure-claimed=false"
            });
            Debug.Log("U0_BDC_D01_DOOR_POLICY=PASS");
        }
        catch (Exception exception)
        {
            if (!File.Exists(reportPath))
            {
                WriteReport(reportPath, new[]
                {
                    "U0_BDC_D01_DOOR_POLICY=FAIL",
                    "exception=" + exception.GetType().FullName,
                    "message=" + exception.Message,
                    "human-approval-claimed=false",
                    "training-claimed=false",
                    "full-audit-closure-claimed=false"
                });
            }
            throw;
        }
    }

    public static void RunG03FromBatch()
    {
        string reportPath = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_G03_POLICY_REVISION_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(reportPath) || File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "A new G03 policy-revision report path is required.");
        }

        try
        {
            RunOrThrow();
            WriteReport(reportPath, new[]
            {
                "U0_G03_POLICY_REVISION=PASS",
                "same-value-no-op=true",
                "before-after-revision-source=true",
                "prepare-rejection-no-mutation=true",
                "delete-then-reassign=true",
                "pending-save-retry=true",
                "physical-auto-close-policy-result=0",
                "growth-outcome-count=0",
                "human-approval-claimed=false",
                "training-claimed=false",
                "full-audit-closure-claimed=false"
            });
            Debug.Log("U0_G03_POLICY_REVISION=PASS");
        }
        catch (Exception exception)
        {
            if (!File.Exists(reportPath))
            {
                WriteReport(reportPath, new[]
                {
                    "U0_G03_POLICY_REVISION=FAIL",
                    "exception=" + exception.GetType().FullName,
                    "message=" + exception.Message,
                    "human-approval-claimed=false",
                    "training-claimed=false",
                    "full-audit-closure-claimed=false"
                });
            }
            throw;
        }
    }

    public static void RunOrThrow()
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new PolicyStatusChangeOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new PolicyStatusChangeOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = new(
            registry,
            new GameplayOutcomeBufferLimits(),
            new GameplayOutcomeRunId("run:door-held-open-editor"),
            1L);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new DungeonStory.Foundation.GameEventBus());
        using var domainFacts = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
        domainFacts.Subscribe(
            "fixture:door-held-open",
            new GameplayDomainFactObserver<
                DoorHeldOpenFact,
                PolicyStatusChangeReceipt>(
                DoorHeldOpenFactMapping.Capture,
                registry,
                ledger,
                recorder,
                commits));
        var committer = new FaultingCommitter(domainFacts);
        var ports = new UnusedDoorPorts();
        DoorAccessService domain = new(
            ports,
            ports,
            ports,
            new DungeonRuntimeAggregateRootStore());
        ICharacterAiWorldRegistry world = DispatchProxy.Create<
            ICharacterAiWorldRegistry,
            UnusedWorldProxy>();
        DoorAccessUnityAdapter commands = new(
            domain,
            world,
            committer,
            EditorFixedGameCalendar.Instance);

        Door door = CreateDoor("building:door-held-open-qa");
        Door restored = null;
        try
        {
            GameplayEntityId subject = new(
                new GameplayEntityKindId("facility"),
                door.PersistentInstanceId.Value);
            Require(!commands.SetHeldOpen(null, true),
                "Invalid door command succeeded.");
            Require(commands.SetHeldOpen(door, false)
                    && Count(ledger, subject) == 0
                    && door.OperationStateModule.ManualRevision == 0L,
                "Initial no-op created an outcome or revision.");

            Require(commands.SetHeldOpen(door, true)
                    && door.IsHeldOpen
                    && door.OperationStateModule.ManualRevision == 1L
                    && door.OperationStateModule.PendingManualOutcome == null
                    && Count(ledger, subject) == 1,
                "Manual hold did not commit one terminal outcome.");
            GameplayOutcomeSnapshot first = ledger.GetForEntity(
                subject,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items[0].Exact;
            Require(first.outcomeTypeId
                        == PolicyStatusChangeOutcomeIds.Changed.Value
                    && first.producerId
                        == PolicyStatusChangeOutcomeIds.ProducerId
                    && first.ownerRevision == 1L
                    && first.operationId
                        == DoorHeldOpenPendingSaveData.BuildOperationId(
                            door.PersistentInstanceId.Value, 1L)
                    && first.metrics.Count == 3
                    && first.metrics[0].value == 0d
                    && first.metrics[1].value == 1d
                    && first.metrics[2].value == 1d
                    && first.facts.Count == 3
                    && first.facts[0].value
                        == DoorHeldOpenPendingSaveData.PolicyStatusKey
                    && first.facts[1].value
                        == DoorHeldOpenPendingSaveData.PlayerPanelOrigin
                    && first.facts[2].value
                        == DoorHeldOpenPendingSaveData.BuildOperationId(
                            door.PersistentInstanceId.Value, 1L),
                "G03 receipt lost policy key, transition, revision, source, or operation.");
            Require(commands.SetHeldOpen(door, true)
                    && Count(ledger, subject) == 1
                    && door.OperationStateModule.ManualRevision == 1L,
                "Repeated hold was not a no-op.");

            committer.RejectNextPrepare = true;
            Require(!commands.SetHeldOpen(door, false)
                    && door.IsHeldOpen
                    && door.OperationStateModule.ManualRevision == 1L
                    && Count(ledger, subject) == 1,
                "Prepare rejection changed door authority or ledger.");

            committer.DeferNextDelivery = true;
            Require(!commands.SetHeldOpen(door, false)
                    && !door.IsHeldOpen
                    && door.IsOpen
                    && door.OperationStateModule.ManualRevision == 2L
                    && door.OperationStateModule.PendingManualOutcome != null
                    && Count(ledger, subject) == 2,
                "Deferred delivery lost its committed state or pending receipt.");
            string saved = door.OperationStateModule.CaptureState();
            restored = CreateDoor(door.PersistentInstanceId.Value);
            Require(restored.OperationStateModule.TryRestoreState(
                        restored.OperationStateModule.CurrentVersion,
                        saved,
                        out string restoreFailure),
                "Door operation restore failed: " + restoreFailure);
            Require(restored.OperationStateModule.ManualRevision == 2L
                    && restored.IsOpen
                    && !restored.IsHeldOpen
                    && restored.OperationStateModule.PendingManualOutcome != null
                    && Count(ledger, subject) == 2,
                "Restore created an outcome or lost pending delivery.");
            Require(commands.SetHeldOpen(restored, false)
                    && restored.OperationStateModule.PendingManualOutcome == null
                    && restored.OperationStateModule.ManualRevision == 2L
                    && Count(ledger, subject) == 2,
                "Retry did not acknowledge exactly the saved operation.");
            Require(commands.SetHeldOpen(restored, false)
                    && Count(ledger, subject) == 2,
                "Post-retry no-op duplicated the outcome.");
            restored.OperationStateModule.Set(false, false);
            Require(!restored.IsOpen
                    && restored.OperationStateModule.ManualRevision == 2L
                    && Count(ledger, subject) == 2,
                "Physical auto-close created a manual policy result.");
            Require(commands.SetHeldOpen(restored, true)
                    && restored.IsHeldOpen
                    && restored.OperationStateModule.ManualRevision == 3L
                    && restored.OperationStateModule.PendingManualOutcome == null
                    && Count(ledger, subject) == 3,
                "Deleted held-open policy was not reassigned with one new revision.");
            IReadOnlyList<GameplayOutcomeQueryItem> finalOutcomes = ledger
                .GetForEntity(
                    subject,
                    OutcomeCursor.FirstPage(10),
                    OutcomeFilter.All)
                .Items;
            foreach (GameplayOutcomeQueryItem outcome in finalOutcomes)
            {
                Require(outcome.Exact.outcomeTypeId
                            == PolicyStatusChangeOutcomeIds.Changed.Value
                        && outcome.Exact.producerId
                            == PolicyStatusChangeOutcomeIds.ProducerId,
                    "Policy setting was recorded as a non-policy growth outcome.");
            }

            DoorOperationStateModule legacy = new(() => { });
            Require(legacy.TryRestoreState(1, "{\"state\":3}", out _)
                    && legacy.IsHeldOpen
                    && legacy.ManualRevision == 0L
                    && legacy.PendingManualOutcome == null,
                "Legacy door operation state did not restore without a new result.");
            DoorOperationStateModule fresh = new(() => { });
            string freshState = fresh.CaptureState();
            DoorOperationStateModule freshRestored = new(() => { });
            Require(freshRestored.TryRestoreState(
                        freshRestored.CurrentVersion,
                        freshState,
                        out string freshFailure)
                    && !freshRestored.IsOpen
                    && freshRestored.ManualRevision == 0L
                    && freshRestored.PendingManualOutcome == null,
                "Fresh v3 door operation state did not roundtrip: "
                + freshFailure + ":" + freshState);
            DoorOperationStateModule v2Empty = new(() => { });
            Require(v2Empty.TryRestoreState(
                        2,
                        "{\"state\":1,\"manualRevision\":0,\"pending\":{\"doorId\":\"\",\"operationId\":\"\",\"commandOrigin\":\"\",\"revision\":0,\"absoluteDay\":0,\"positionX\":0,\"positionY\":0,\"beforeOpen\":false,\"beforeHeld\":false,\"afterOpen\":false,\"afterHeld\":false}}",
                        out string v2Failure)
                    && v2Empty.PendingManualOutcome == null,
                "JsonUtility v2 empty pending was not normalized: "
                + v2Failure);
            VerifyBulkAccessPolicyRevisionAndRestore(committer);
            Debug.Log("BDC-D01 door held-open outcome scenarios PASS");
        }
        finally
        {
            if (restored != null)
                UnityEngine.Object.DestroyImmediate(restored.gameObject);
            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }
    }

    private static Door CreateDoor(string id)
    {
        GameObject holder = new("DoorHeldOpenOutcomeDebug");
        Door door = holder.AddComponent<Door>();
        door.RestorePersistentIdentity((BuildingInstanceId)id);
        typeof(Door).GetField(
                "<AccessStateModule>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(door, new DoorAccessStateModule(() => { }));
        typeof(Door).GetField(
                "<OperationStateModule>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(door, new DoorOperationStateModule(() => { }));
        Require(door.AccessStateModule != null
                && door.OperationStateModule != null,
            "Door state modules could not be installed in the fixture.");
        return door;
    }

    private static Door CreateDoor(
        string id,
        Grid grid,
        Vector2Int position)
    {
        Door door = CreateDoor(id);
        door.SetGrid(grid);
        FieldInfo centerField = typeof(BuildableObject).GetField(
            "<centerPos>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(centerField != null,
            "Door fixture could not access its runtime center.");
        centerField.SetValue(door, position);
        FieldInfo posesField = typeof(BuildableObject).GetField(
            "mutableBuildPoses",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var poses = posesField?.GetValue(door) as List<Vector2Int>;
        Require(poses != null,
            "Door fixture could not access its runtime footprint.");
        poses.Add(position);
        return door;
    }

    private static void VerifyBulkAccessPolicyRevisionAndRestore(
        IDomainFactPublisher committer)
    {
        Grid grid = new(10, 3);
        Door source = null;
        Door changed = null;
        Door samePolicy = null;
        Door outsideRoom = null;
        try
        {
            source = CreateDoor(
                "building:door-policy-source",
                grid,
                new Vector2Int(2, 1));
            changed = CreateDoor(
                "building:door-policy-changed",
                grid,
                new Vector2Int(4, 1));
            samePolicy = CreateDoor(
                "building:door-policy-same",
                grid,
                new Vector2Int(4, 1));
            outsideRoom = CreateDoor(
                "building:door-policy-outside",
                grid,
                new Vector2Int(6, 1));

            Require(source.AccessStateModule.SetGroupAllowed(
                        DoorAccessGroup.Customer,
                        false)
                    && source.AccessStateModule.SetGroupAllowed(
                        DoorAccessGroup.Captive,
                        false)
                    && source.AccessStateModule.Revision == 2L,
                "Source policy did not create two local revisions.");
            Require(samePolicy.AccessStateModule.CopyFrom(source.AccessPolicy)
                    && samePolicy.AccessStateModule.Revision == 1L
                    && EquivalentPolicy(
                        samePolicy.AccessPolicy,
                        source.AccessPolicy),
                "Same-policy fixture did not establish its independent revision.");

            RoomInstance room = new(
                1,
                new[] { new Vector2Int(3, 1) },
                Array.Empty<BuildableObject>(),
                new BuildableObject[] { source, changed, samePolicy },
                Array.Empty<BuildableObject>(),
                3,
                0,
                selfContained: true);
            var roomCache = new ControlledRoomCache(grid, room);
            ICharacterAiWorldRegistry world = DispatchProxy.Create<
                ICharacterAiWorldRegistry,
                BulkWorldProxy>();
            ((BulkWorldProxy)(object)world).SetBuildings(new BuildableObject[]
            {
                source,
                changed,
                samePolicy,
                outsideRoom
            });
            var unused = new UnusedDoorPorts();
            var roomPort = new BuildingDoorRoomPolicyAdapter(world, roomCache);
            DoorAccessService domain = new(
                unused,
                unused,
                roomPort,
                new DungeonRuntimeAggregateRootStore());
            DoorAccessUnityAdapter commands = new(
                domain,
                world,
                committer,
                EditorFixedGameCalendar.Instance);

            Require(commands.ApplyPolicyToRoomDoors(source) == 1,
                "Bulk policy did not report exactly one actual door change.");
            Require(changed.AccessStateModule.Revision == 1L
                    && changed.AccessStateModule.Revision
                        != source.AccessStateModule.Revision
                    && EquivalentPolicy(changed.AccessPolicy, source.AccessPolicy),
                "Changed door lost its local revision or copied the wrong snapshot.");
            Require(samePolicy.AccessStateModule.Revision == 1L
                    && EquivalentPolicy(
                        samePolicy.AccessPolicy,
                        source.AccessPolicy),
                "Same-policy door was counted or revised again.");
            Require(outsideRoom.AccessStateModule.Revision == 0L
                    && outsideRoom.AccessPolicy.AllowedGroups
                        == DoorAccessGroup.All,
                "Outside-room door was changed by the bulk command.");
            Require(commands.ApplyPolicyToRoomDoors(source) == 0
                    && changed.AccessStateModule.Revision == 1L
                    && samePolicy.AccessStateModule.Revision == 1L,
                "Bulk replay was not an exact no-op.");

            string saved = changed.AccessStateModule.CaptureState();
            DoorAccessStateModule restored = new();
            Require(restored.TryRestoreState(
                        restored.CurrentVersion,
                        saved,
                        out string failure)
                    && restored.Revision == 1L
                    && EquivalentPolicy(restored.State, source.AccessPolicy),
                "Changed door policy did not roundtrip its snapshot/revision: "
                + failure);
        }
        finally
        {
            DestroyDoor(outsideRoom);
            DestroyDoor(samePolicy);
            DestroyDoor(changed);
            DestroyDoor(source);
        }
    }

    private static bool EquivalentPolicy(
        DoorAccessPolicyState left,
        DoorAccessPolicyState right) => left != null
        && right != null
        && left.AllowedGroups == right.AllowedGroups
        && left.IndividuallyAllowedIds.SequenceEqual(
            right.IndividuallyAllowedIds,
            StringComparer.Ordinal)
        && left.IndividuallyDeniedIds.SequenceEqual(
            right.IndividuallyDeniedIds,
            StringComparer.Ordinal);

    private static void DestroyDoor(Door door)
    {
        if (door != null)
        {
            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }
    }

    private static int Count(
        GameplayOutcomeLedger ledger,
        GameplayEntityId subject) => ledger.GetForEntity(
            subject,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All).Items.Count;

    private static void WriteReport(string path, IEnumerable<string> lines)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("G03 report directory is invalid.");
        Directory.CreateDirectory(directory);
        File.WriteAllLines(fullPath, lines);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FaultingCommitter : IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;
        internal bool RejectNextPrepare;
        internal bool DeferNextDelivery;

        internal FaultingCommitter(IDomainFactPublisher inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failureReason)
        {
            if (RejectNextPrepare)
            {
                RejectNextPrepare = false;
                lease = default;
                failureReason = "injected-door-prepare-rejection";
                return false;
            }
            return inner.TryReserve(proposed, out lease, out failureReason);
        }

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            DomainFactDelivery result = inner.Publish(lease, actual);
            if (!DeferNextDelivery || !result.Completed)
                return result;
            DeferNextDelivery = false;
            return new DomainFactDelivery(
                DomainFactDeliveryPhase.AcceptedPendingDelivery,
                result.OperationKey,
                result.PublicationId,
                result.Fingerprint,
                "injected-door-delivery-deferred");
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(facts, results, out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class UnusedDoorPorts :
        IBuildingDoorAccessSubjectPort,
        IBuildingDoorPolicyInvalidationPort,
        IBuildingDoorRoomPolicyPort
    {
        public bool TryResolveDoorAccessSubject(
            UnityEngine.Object subject,
            out BuildingDoorAccessSubjectSnapshot snapshot)
        {
            snapshot = default;
            throw new InvalidOperationException(
                "Door subject resolution is outside the command fixture.");
        }

        public void InvalidateDoorPolicyPaths() =>
            throw new InvalidOperationException(
                "Door policy invalidation is outside the command fixture.");

        public int ApplyToRoomDoors(
            IBuildingDoorAccessPolicyPort source,
            object policy) => throw new InvalidOperationException(
            "Room policy is outside the command fixture.");
    }

    public class UnusedWorldProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            throw new InvalidOperationException(
                "Character world query is outside the door command fixture.");
    }

    public class BulkWorldProxy : DispatchProxy
    {
        private IReadOnlyList<BuildableObject> buildings =
            Array.Empty<BuildableObject>();

        internal void SetBuildings(IReadOnlyList<BuildableObject> value) =>
            buildings = value ?? Array.Empty<BuildableObject>();

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            if (string.Equals(
                    targetMethod?.Name,
                    "get_Buildings",
                    StringComparison.Ordinal))
            {
                return buildings;
            }

            throw new InvalidOperationException(
                "Character world query is outside the bulk door fixture: "
                + targetMethod?.Name);
        }
    }

    private sealed class ControlledRoomCache : IRoomLayoutCache
    {
        private readonly Grid grid;
        private readonly RoomInstance room;

        internal ControlledRoomCache(Grid grid, RoomInstance room)
        {
            this.grid = grid;
            this.room = room;
        }

        public RoomLayout GetLayout(Grid candidate) => new(
            candidate == grid
                ? new[] { room }
                : Array.Empty<RoomInstance>());

        public bool TryGetRoom(
            Grid candidate,
            Vector2Int cell,
            out RoomInstance found)
        {
            if (candidate == grid && room.ContainsCell(cell))
            {
                found = room;
                return true;
            }

            found = null;
            return false;
        }

        public bool TryGetRoom(BuildableObject part, out RoomInstance found)
        {
            found = null;
            return false;
        }

        public void Clear()
        {
        }
    }
}
#endif
