using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StaffRebellionResponseDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character/Run P2 Staff Rebellion Response Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P2 staff rebellion response scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("자동 제압 배정", VerifyAutoSuppressAssignment, errors);
        RunScenario("반란 직원 제압 명령 대상", VerifyRebelSuppressCommandTarget, errors);
        RunScenario("격리로 사장 위협 확산 차단", VerifyIsolationBlocksOwnerThreat, errors);
        RunScenario("반란 직전 진정", VerifyCalmBeforeRebellion, errors);
        RunScenario("제압 완료 후 대상 해제", VerifySuppressedRebelClearsThreat, errors);
        RunScenario("원장 커밋 거부 시 상태·기분 롤백", VerifyRejectedCommitRollsBack, errors);
        RunScenario("전달 실패 시 상태와 outbox 유지", VerifyPendingDeliveryRetainsState, errors);
        RunScenario("동일 결과 replay와 충돌 payload 구분", VerifyExactReplayAndConflict, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P2 staff rebellion response scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyAutoSuppressAssignment()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld();
        CharacterActor guard = world.CreateStaff(301, "Auto Guard", new Vector2Int(0, 0), 80f);
        CharacterActor rebel = world.CreateStaff(302, "Auto Rebel", new Vector2Int(3, 0), 5f);

        world.Runtime.ProcessStaff(CharacterActor.From(rebel), out StaffDiscontentOutcome outcome);
        AbilityWork guardWork = guard.GetAbility<AbilityWork>();

        return outcome == StaffDiscontentOutcome.LocalRebellion
            && guardWork.PrioritySuppressActor == CharacterActor.From(rebel)
            && guardWork.HasPrioritySuppressTarget
            && guardWork.TryGetPrioritySuppressDestination(world.Grid.SearchPath(new Vector2Int(0, 0)), out BuildableObject destination)
            && destination != null;
    }

    private static bool VerifyRebelSuppressCommandTarget()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld();
        CharacterActor guard = world.CreateStaff(303, "Manual Guard", new Vector2Int(0, 0), 80f);
        CharacterActor rebel = world.CreateStaff(304, "Manual Rebel", new Vector2Int(2, 0), 5f);

        world.Runtime.ProcessStaff(CharacterActor.From(rebel), out _);
        return WorkCommandResolver.IsSuppressTarget(CharacterActor.From(rebel), world.Runtime.IsRebellionTarget)
            && WorkCommandResolver.TryResolveSuppressCommand(
                CharacterActor.From(guard),
                CharacterActor.From(rebel),
                world.Runtime.IsRebellionTarget,
                out _);
    }

    private static bool VerifyIsolationBlocksOwnerThreat()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld();
        CharacterActor rebel = world.CreateStaff(305, "Isolated Rebel", new Vector2Int(2, 0), 5f);

        StaffDiscontentRecord record = world.Runtime.ProcessStaff(CharacterActor.From(rebel), out _);
        bool isolated = world.Runtime.TryIsolateRebel(CharacterActor.From(rebel), null, out StaffRebellionResponseResult isolationResult);
        world.Runtime.ProcessStaff(CharacterActor.From(rebel), out StaffDiscontentOutcome secondOutcome);
        world.Runtime.ProcessStaff(CharacterActor.From(rebel), out StaffDiscontentOutcome thirdOutcome);

        return record != null
            && isolated
            && isolationResult.Success
            && record.IsIsolated
            && !record.IsOwnerThreat
            && secondOutcome == StaffDiscontentOutcome.None
            && thirdOutcome == StaffDiscontentOutcome.None
            && world.HasPublishedResponse(
                rebel,
                StaffRebellionResponseType.Isolate,
                "격리");
    }

    private static bool VerifyCalmBeforeRebellion()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld();
        CharacterActor staff = world.CreateStaff(306, "Calm Target", new Vector2Int(1, 0), 20f);
        CharacterActor actor = world.CreateStaff(307, "Negotiator", new Vector2Int(0, 0), 80f);

        StaffDiscontentRecord record = world.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome beforeOutcome);
        bool calmed = world.Runtime.TryCalmStaff(CharacterActor.From(staff), CharacterActor.From(actor), out StaffRebellionResponseResult calmResult);

        return record != null
            && beforeOutcome == StaffDiscontentOutcome.WorkDisruption
            && calmed
            && calmResult.Success
            && record.Stage != StaffDiscontentStage.WorkDisruption
            && record.Stage != StaffDiscontentStage.LocalRebellion
            && staff.stats[CharacterCondition.MOOD] > 20f
            && world.HasPublishedResponse(
                staff,
                StaffRebellionResponseType.Calm,
                "진정");
    }

    private static bool VerifySuppressedRebelClearsThreat()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld();
        CharacterActor guard = world.CreateStaff(308, "Suppressing Guard", new Vector2Int(0, 0), 80f);
        CharacterActor rebel = world.CreateStaff(309, "Suppressed Rebel", new Vector2Int(2, 0), 5f);

        StaffDiscontentRecord record = world.Runtime.ProcessStaff(CharacterActor.From(rebel), out _);
        bool resolved = world.Runtime.ResolveSuppressedRebel(CharacterActor.From(rebel), CharacterActor.From(guard));

        return record != null
            && resolved
            && record.IsSuppressed
            && record.IsPermanentLoss
            && !record.IsInLocalRebellion
            && !WorkCommandResolver.IsSuppressTarget(CharacterActor.From(rebel), world.Runtime.IsRebellionTarget)
            && world.HasPublishedResponse(
                rebel,
                StaffRebellionResponseType.SuppressCommand,
                "제압");
    }

    private static bool VerifyRejectedCommitRollsBack()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld(
            rejectCommit: true);
        CharacterActor staff = world.CreateStaff(
            310,
            "Rollback Target",
            new Vector2Int(1, 0),
            20f);
        CharacterActor actor = world.CreateStaff(
            311,
            "Rollback Negotiator",
            new Vector2Int(0, 0),
            80f);
        StaffDiscontentRecord initial = world.Runtime.ProcessStaff(
            CharacterActor.From(staff),
            out _);
        StaffDiscontentSnapshot before = initial?.ToSnapshot();
        float moodBefore = staff.stats[CharacterCondition.MOOD];

        bool calmed = world.Runtime.TryCalmStaff(
            CharacterActor.From(staff),
            CharacterActor.From(actor),
            out StaffRebellionResponseResult result);
        bool restored = world.Runtime.State.TryGetRecord(
            CharacterActor.From(staff),
            out StaffDiscontentRecord current);
        return before != null
            && !calmed
            && !result.Success
            && restored
            && current.Stage == before.stage
            && current.LowMoodDays == before.lowMoodDays
            && Mathf.Approximately(
                staff.stats[CharacterCondition.MOOD],
                moodBefore)
            && world.Runtime.OutcomeRevision == 0L
            && world.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 0
            && world.Ledger.GetOutboxSnapshot().Count == 0;
    }

    private static bool VerifyPendingDeliveryRetainsState()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld(
            rejectDelivery: true);
        CharacterActor rebel = world.CreateStaff(
            312,
            "Pending Delivery Rebel",
            new Vector2Int(2, 0),
            5f);
        StaffDiscontentRecord record = world.Runtime.ProcessStaff(
            CharacterActor.From(rebel),
            out _);

        bool isolated = world.Runtime.TryIsolateRebel(
            CharacterActor.From(rebel),
            null,
            out StaffRebellionResponseResult result);
        IReadOnlyList<GameplayOutcomeOutboxSnapshot> outbox =
            world.Ledger.GetOutboxSnapshot();
        world.Runtime.State.TryGetRecord(
            CharacterActor.From(rebel),
            out StaffDiscontentRecord current);
        int globalCount = world.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All).Items.Count;
        bool valid = record != null
            && isolated
            && result.Success
            && record.IsIsolated
            && world.Runtime.OutcomeRevision == 1L
            && globalCount == 0
            && outbox.Count == 1
            && outbox[0].Lifecycle ==
                GameplayOutcomeRecordLifecycle.DeliveryFaultPending;
        if (!valid)
        {
            Debug.LogError(
                "Staff pending-delivery detail: "
                + $"record={(record != null)}, isolated={isolated}, "
                + $"result={result.Success}, originalIsolated={record?.IsIsolated}, "
                + $"currentIsolated={current?.IsIsolated}, message={result.Message}, "
                + $"revision={world.Runtime.OutcomeRevision}, "
                + $"global={globalCount}, outbox={outbox.Count}, "
                + $"lifecycle={(outbox.Count == 0 ? "none" : outbox[0].Lifecycle.ToString())}, "
                + $"fault={(outbox.Count == 0 ? "none" : outbox[0].LastFaultCode)}");
        }
        return valid;
    }

    private static bool VerifyExactReplayAndConflict()
    {
        using RebellionScenarioWorld world = new RebellionScenarioWorld();
        CharacterActor staff = world.CreateStaff(
            313,
            "Replay Target",
            new Vector2Int(1, 0),
            20f);
        CharacterActor actor = world.CreateStaff(
            314,
            "Replay Negotiator",
            new Vector2Int(0, 0),
            80f);
        StaffDiscontentRecord record = world.Runtime.ProcessStaff(
            CharacterActor.From(staff),
            out _);
        StaffDiscontentSnapshot before = record?.ToSnapshot();
        if (before == null
            || !world.Runtime.TryCalmStaff(
                CharacterActor.From(staff),
                CharacterActor.From(actor),
                out StaffRebellionResponseResult result)
            || !CharacterPersistentIdentity.TryGet(
                actor,
                out CharacterId responder))
        {
            return false;
        }

        GameplayOutcomeQueryPage page = world.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        if (page.Items.Count != 1 || page.Items[0].Exact == null)
            return false;
        GameplayOutcomeSnapshot exact = page.Items[0].Exact;
        KoreanNameSnapshot responderName =
            StaffDiscontentOutcomeReceipt.CaptureName(
                responder.Value,
                StaffDiscontentService.GetStaffDisplayName(
                    actor,
                    responder.Value));
        StaffDiscontentOutcomeReceipt exactReceipt = new(
            1L,
            exact.absoluteDay,
            StaffRebellionResponseType.Calm,
            before,
            result.Snapshot,
            responder,
            responderName);
        OutcomePrepareResult replay = world.Recorder.TryPrepare(
            exactReceipt,
            out PreparedOutcomeToken unexpected);
        if (unexpected.IsValid)
            world.Recorder.CancelPrepared(unexpected);

        StaffDiscontentSnapshot after = result.Snapshot;
        StaffDiscontentSnapshot conflictingAfter = new(
            after.staffId,
            after.displayName,
            after.stage,
            after.outcome,
            Math.Min(100f, after.mood + 1f),
            after.lowMoodDays,
            after.permanentLoss,
            after.departed,
            after.localRebellion,
            after.ownerThreat,
            after.isolated,
            after.suppressed);
        StaffDiscontentOutcomeReceipt conflictingReceipt = new(
            1L,
            exact.absoluteDay,
            StaffRebellionResponseType.Calm,
            before,
            conflictingAfter,
            responder,
            responderName);
        OutcomePrepareResult conflict = world.Recorder.TryPrepare(
            conflictingReceipt,
            out PreparedOutcomeToken conflictingToken);
        if (conflictingToken.IsValid)
            world.Recorder.CancelPrepared(conflictingToken);
        return replay.Code is OutcomePrepareCode.AlreadyCommitted
                or OutcomePrepareCode.AlreadyPublished
                or OutcomePrepareCode.AlreadyTerminal
            && conflict.Code == OutcomePrepareCode.ConflictingResult;
    }

    private sealed class RebellionScenarioWorld : IDisposable
    {
        private static readonly FieldInfo GridSystemInstanceField =
            typeof(GridSystemManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo GridField =
            typeof(GridSystemManager).GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CharacterAwakeMethod =
            typeof(CharacterActor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GridSystemManager previousGridSystem;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<ScriptableObject> scriptableObjects = new List<ScriptableObject>();

        public RebellionScenarioWorld(
            bool rejectCommit = false,
            bool rejectDelivery = false)
        {
            previousGridSystem = GridSystemInstanceField?.GetValue(null) as GridSystemManager;
            Grid = new Grid(8, 1);

            GameObject gridSystemObject = new GameObject("Rebellion Response GridSystemManager");
            objects.Add(gridSystemObject);
            GridSystemManager manager = gridSystemObject.AddComponent<GridSystemManager>();
            GridField?.SetValue(manager, Grid);
            GridSystemInstanceField?.SetValue(null, manager);

            GameObject runtimeObject = new GameObject("Rebellion Response Runtime");
            objects.Add(runtimeObject);
            Runtime = runtimeObject.AddComponent<StaffDiscontentRuntime>();
            StaffDiscontentOutcomeDescriptor descriptor = new(
                new KoreanJosaFormatter());
            StaffDiscontentOutcomeAdapter adapter = new();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    rejectDelivery
                        ? new RejectingDeliveryDescriptor(descriptor)
                        : descriptor
                },
                new IGameplayOutcomeAdapterRegistration[] { adapter });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 8,
                    largePageCount: 0,
                    knownResultKeyCapacity: 128),
                new GameplayOutcomeRunId("run:staff-response-test"),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                CharacterAiEditorTestDependencies.GameEvents);
            StaffDiscontentGameplayOutcomeBridge realCommitter = new(
                Recorder,
                Ledger,
                Ledger);
            OutcomeCommitter = rejectCommit
                ? new RejectingCommitter(realCommitter)
                : realCommitter;
            RootStore = new DungeonRuntimeAggregateRootStore();
            Runtime.Construct(
                CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.GameEvents,
                RootStore,
                CharacterAiEditorTestDependencies.SettlementStandings,
                OutcomeCommitter,
                CharacterAiEditorTestDependencies.GameClock);

            for (int x = 0; x < Grid.width; x++)
            {
                PlaceHallway(new Vector2Int(x, 0));
            }
        }

        public Grid Grid { get; }
        public StaffDiscontentRuntime Runtime { get; }
        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public IStaffDiscontentOutcomeCommitter OutcomeCommitter { get; }
        public DungeonRuntimeAggregateRootStore RootStore { get; }

        public bool HasPublishedResponse(
            CharacterActor staff,
            StaffRebellionResponseType responseType,
            string expectedText)
        {
            if (!CharacterPersistentIdentity.TryGet(staff, out CharacterId id))
                return false;
            GameplayEntityId entity = new(
                StaffDiscontentOutcomeIds.CharacterKind,
                id.Value);
            GameplayOutcomeQueryPage subject = Ledger.GetForEntity(
                entity,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All);
            GameplayOutcomeQueryPage global = Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All);
            if (subject.Items.Count != 1
                || global.Items.Count != 1
                || subject.Items[0].Exact == null
                || global.Items[0].Exact == null)
            {
                return false;
            }

            GameplayOutcomeSnapshot snapshot = subject.Items[0].Exact;
            GameplayOutcomeId outcomeId = new(
                new GameplayOutcomeRunId(snapshot.runId),
                snapshot.sequence);
            return string.Equals(
                    snapshot.producerId,
                    StaffDiscontentOutcomeIds.ProducerId,
                    StringComparison.Ordinal)
                && string.Equals(
                    snapshot.outcomeTypeId,
                    StaffDiscontentOutcomeIds.ResponseResolved.Value,
                    StringComparison.Ordinal)
                && snapshot.commitRevision == Runtime.OutcomeRevision
                && snapshot.ownerRevision == Runtime.OutcomeRevision
                && string.Equals(
                    snapshot.operationId,
                    "staff-response:" + Runtime.OutcomeRevision,
                    StringComparison.Ordinal)
                && global.Items[0].Exact.sequence == snapshot.sequence
                && Ledger.TryProject(
                    outcomeId,
                    new NarrativePerspectiveContext(
                        entity,
                        NarrativePerspectiveKind.Character,
                        "ko-KR"),
                    out NarrativeView characterView)
                && characterView.Text.Contains(
                    expectedText,
                    StringComparison.Ordinal);
        }

        public CharacterActor CreateStaff(int id, string name, Vector2Int position, float mood)
        {
            GameObject obj = new GameObject(name);
            objects.Add(obj);
            obj.AddComponent<SpriteRenderer>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityShopping>();
            obj.AddComponent<AbilityWork>();
            AIBrain brain = obj.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();
            CharacterActor character = obj.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(obj, Runtime);
            CharacterAwakeMethod?.Invoke(character, null);

            CharacterSO data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                name,
                "Orc");
            scriptableObjects.Add(data);
            data.id = id;
            data.characterType = CharacterType.NPC;
            data.role = CharacterRole.Regular;
            data.characterName = name;
            data.speciesTag = "Orc";
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            obj.transform.position = Grid.GetWorldPos(position);
            character.RefreshAbilityCache();
            character.Initialization(data);
            character.Identity.SetPersistentId($"character:staff-rebellion-test:{id}");
            character.SetLifecycleState(CharacterLifecycleState.Active);
            character.stats[CharacterCondition.SLEEP] = 50f;
            character.stats[CharacterCondition.HUNGER] = 50f;
            character.stats[CharacterCondition.FUN] = 50f;
            character.stats[CharacterCondition.EXCRETION] = 50f;
            character.stats[CharacterCondition.HYGIENE] = 50f;
            character.stats[CharacterCondition.MOOD] = mood;
            return character;
        }

        public void Dispose()
        {
            GridSystemInstanceField?.SetValue(null, previousGridSystem);
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }

            foreach (ScriptableObject obj in scriptableObjects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }
        }

        private void PlaceHallway(Vector2Int position)
        {
            GameObject obj = new GameObject($"Hallway {position.x}");
            objects.Add(obj);
            BuildableObject hallway = obj.AddComponent<BuildableObject>();
            BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
            scriptableObjects.Add(data);
            data.id = 7000 + position.x;
            data.objectName = obj.name;
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Hallway;
            data.category = BuildingCategory.Movement;
            data.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;

            obj.transform.position = Grid.GetWorldPos(position);
            hallway.SetGrid(Grid);
            CharacterAiEditorTestDependencies.Inject(hallway);
            hallway.Initialization(data, position);
            Grid.RegisterOccupant(
                hallway,
                GridLayer.Hallway,
                data.GetGridPosList(position),
                true);
        }
    }

    private sealed class RejectingCommitter : IStaffDiscontentOutcomeCommitter
    {
        private readonly IStaffDiscontentOutcomeCommitter inner;

        public RejectingCommitter(IStaffDiscontentOutcomeCommitter inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public bool TryReserve(
            long ownerRevision,
            int absoluteDay,
            bool hasResponder,
            out ReservedStaffDiscontentOutcome reserved,
            out string failureReason) => inner.TryReserve(
            ownerRevision,
            absoluteDay,
            hasResponder,
            out reserved,
            out failureReason);

        public bool TryWrite(
            in StaffDiscontentOutcomeReceipt receipt,
            in ReservedStaffDiscontentOutcome reserved,
            out PreparedOwnerOutcome prepared,
            out string failureReason) => inner.TryWrite(
            receipt,
            reserved,
            out prepared,
            out failureReason);

        public OwnerOutcomeCommitResult Commit(
            in PreparedOwnerOutcome prepared,
            long expectedOwnerRevision)
        {
            inner.Cancel(prepared);
            return new OwnerOutcomeCommitResult(
                OwnerOutcomeCommitPhase.Rejected,
                prepared.ResultKey,
                default,
                string.Empty,
                "injected-staff-outcome-commit-rejection");
        }

        public OwnerOutcomeCommitResult Reconcile(
            GameplayResultKey resultKey) => inner.Reconcile(resultKey);

        public void Cancel(in ReservedStaffDiscontentOutcome reserved) =>
            inner.Cancel(reserved);

        public void Cancel(in PreparedOwnerOutcome prepared) =>
            inner.Cancel(prepared);
    }

    private sealed class RejectingDeliveryDescriptor :
        IGameplayOutcomeDescriptor
    {
        private readonly IGameplayOutcomeDescriptor inner;
        private int validationCount;

        public RejectingDeliveryDescriptor(IGameplayOutcomeDescriptor inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public GameplayOutcomeTypeId OutcomeTypeId => inner.OutcomeTypeId;
        public INarrativePerspectiveProjector PerspectiveProjector =>
            inner.PerspectiveProjector;
        public IOutcomeMemoryPolicy MemoryPolicy => inner.MemoryPolicy;
        public IOutcomePerceptionPolicy PerceptionPolicy =>
            inner.PerceptionPolicy;
        public IOutcomeMemoryConsolidator MemoryConsolidator =>
            inner.MemoryConsolidator;
        public bool IsKnownRole(GameplayRoleId roleId) =>
            inner.IsKnownRole(roleId);
        public bool IsKnownMetric(
            GameplayMetricId metricId,
            GameplayMetricUnitId unitId) =>
            inner.IsKnownMetric(metricId, unitId);
        public OutcomeValidationResult Validate(
            in GameplayOutcomeReadView outcome)
        {
            OutcomeValidationResult actual = inner.Validate(outcome);
            if (!actual.Valid)
                return actual;
            validationCount++;
            return validationCount == 1
                ? actual
                : OutcomeValidationResult.Reject(
                    "injected-staff-outcome-delivery-rejection");
        }
    }
}
