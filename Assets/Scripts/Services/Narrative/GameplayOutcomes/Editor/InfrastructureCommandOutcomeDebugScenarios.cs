#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class InfrastructureCommandOutcomeDebugScenarios
{
    private const string FacilityId = "building:qa-infrastructure-command";

    public static bool RunAll(bool logSuccess = false)
    {
        VerifyBridgeAndQuery();
        VerifyOwnerTransactionAndSave();
        if (logSuccess)
            Debug.Log("[Infrastructure Command Outcome] PASS");
        return true;
    }

    private static void VerifyBridgeAndQuery()
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new InfrastructureCommandOutcomeDescriptor(
                    new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new InfrastructureCommandOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = new(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 128),
            new GameplayOutcomeRunId("run:infrastructure-command-editor"),
            1L);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new GameEventBus());
        InfrastructureCommandGameplayOutcomeBridge bridge = new(
            recorder,
            ledger,
            new FixedDisplayNames(),
            new FakeCalendar());
        InfrastructureCommandOutcomeSource source = new(
            InfrastructureCommandOutcomeKind.ConveyorFilterChanged,
            FacilityId,
            FacilityId,
            11,
            6,
            "items=material:ore",
            "items=material:ingot");

        Require(
            bridge.TryPrepare(
                source,
                1L,
                out IPreparedInfrastructureCommandOutcome prepared,
                out string prepareFailure),
            "infrastructure command prepare failed: " + prepareFailure);
        Require(
            prepared.Source.Kind == source.Kind
            && prepared.Source.TargetId == source.TargetId,
            "prepared infrastructure command lost its source snapshot");
        InfrastructureCommandOutcomeCommitResult committed =
            bridge.Commit(prepared);
        Require(
            committed.DurablyCommitted,
            "infrastructure command commit failed: " + committed.DetailCode);

        GameplayEntityId facility = new(
            InfrastructureCommandOutcomeIds.FacilityKind,
            FacilityId);
        GameplayOutcomeQueryPage global = ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage facilityPage = ledger.GetForEntity(
            facility,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            global.Items.Count == 1
            && facilityPage.Items.Count == 1
            && global.Items[0].Exact != null,
            "infrastructure command queries did not expose one exact outcome");
        GameplayOutcomeSnapshot exact = global.Items[0].Exact;
        Require(
            exact.outcomeTypeId == InfrastructureCommandOutcomeIds.Applied.Value
            && exact.ownerRevision == 1L
            && exact.commitRevision == 1L
            && exact.absoluteDay == 17
            && exact.locationX == 11
            && exact.locationY == 6
            && exact.participants.Count == 1
            && exact.participants[0].displayText == "제련소"
            && exact.facts.Any(value =>
                value.factId
                    == InfrastructureCommandOutcomeIds.CommandKindFact.Value
                && value.value == "conveyor-filter-changed")
            && exact.facts.Any(value =>
                value.factId == InfrastructureCommandOutcomeIds.TargetIdFact.Value
                && value.value == FacilityId)
            && exact.facts.Any(value =>
                value.factId
                    == InfrastructureCommandOutcomeIds.BeforeValueFact.Value
                && value.value == "items=material:ore")
            && exact.facts.Any(value =>
                value.factId
                    == InfrastructureCommandOutcomeIds.AfterValueFact.Value
                && value.value == "items=material:ingot"),
            "infrastructure command outcome lost frozen receipt data");

        GameplayOutcomeId outcomeId = new(
            new GameplayOutcomeRunId(exact.runId),
            exact.sequence);
        Require(
            ledger.TryProject(
                outcomeId,
                new NarrativePerspectiveContext(
                    facility,
                    NarrativePerspectiveKind.Facility,
                    "ko-KR"),
                out NarrativeView view)
            && view.Text.Contains("제련소는", StringComparison.Ordinal)
            && view.Text.Contains("운송 필터 변경", StringComparison.Ordinal),
            "infrastructure command Korean projection was not stable");

        Require(
            bridge.TryPrepare(
                source,
                1L,
                out IPreparedInfrastructureCommandOutcome replay,
                out string replayFailure),
            "infrastructure command replay prepare failed: " + replayFailure);
        Require(
            bridge.Commit(replay).DurablyCommitted
            && ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1,
            "infrastructure command replay duplicated the canonical result");

        InfrastructureCommandOutcomeSource drifted = new(
            InfrastructureCommandOutcomeKind.ConveyorFilterChanged,
            FacilityId,
            FacilityId,
            11,
            6,
            "items=material:ore",
            "items=material:ore");
        Require(
            !bridge.TryPrepare(drifted, 1L, out _, out _),
            "same infrastructure command result key accepted drifted facts");

        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = new(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 128),
            new GameplayOutcomeRunId("run:infrastructure-command-restore"),
            1L);
        restored.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(
            restored.GetForEntity(
                facility,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Single().Exact.immutablePayloadHash
            == exact.immutablePayloadHash,
            "infrastructure command outcome changed across save round trip");
    }

    private static void VerifyOwnerTransactionAndSave()
    {
        FakeCommitter committer = new();
        InfrastructureCommandOutcomeRuntime runtime = new(
            new DungeonRuntimeAggregateRootStore(),
            committer);
        InfrastructureCommandOutcomeSource first = CreateOwnerSource(
            InfrastructureCommandOutcomeKind.PowerPriorityChanged,
            "Production",
            "Critical");

        Require(
            runtime.TryPrepare(first, out IPreparedInfrastructureCommandOutcome
                rejectedToken, out string rejectedPrepareFailure),
            "owner rejection prepare failed: " + rejectedPrepareFailure);
        committer.RejectNextCommit = true;
        InfrastructureCommandOutcomeCommitResult rejected =
            runtime.CommitReversible(rejectedToken);
        DungeonInfrastructureCommandOutcomeSaveData afterRejection =
            runtime.Capture();
        Require(
            !rejected.DurablyCommitted
            && afterRejection.nextOwnerRevision == 1L
            && afterRejection.pendingOutcomes.Count == 0,
            "rejected reversible command consumed the owner revision");

        Require(
            runtime.TryPrepare(first, out IPreparedInfrastructureCommandOutcome
                committedToken, out string committedPrepareFailure),
            "owner commit prepare failed: " + committedPrepareFailure);
        Require(
            runtime.CommitReversible(committedToken).DurablyCommitted
            && runtime.Capture().nextOwnerRevision == 2L,
            "reversible command did not advance exactly once");

        InfrastructureCommandOutcomeSource queued = CreateOwnerSource(
            InfrastructureCommandOutcomeKind.ConveyorOverflowApproved,
            "false",
            "true");
        Require(
            runtime.TryPrepare(queued, out IPreparedInfrastructureCommandOutcome
                queuedToken, out string queuedPrepareFailure),
            "queued command prepare failed: " + queuedPrepareFailure);
        runtime.QueueCommitted(queuedToken);
        committer.RejectNextCommit = true;
        runtime.DeliverQueued(queuedToken.OwnerRevision, queuedToken);
        DungeonInfrastructureCommandOutcomeSaveData pending = runtime.Capture();
        Require(
            pending.nextOwnerRevision == 3L
            && pending.pendingOutcomes.Count == 1
            && pending.pendingOutcomes[0].ownerRevision == 2L
            && pending.pendingOutcomes[0].kind
                == InfrastructureCommandOutcomeKind.ConveyorOverflowApproved
            && pending.pendingOutcomes[0].facilityDisplayText == "시험 설비",
            "queued infrastructure outcome was not frozen durably");

        string json = JsonUtility.ToJson(pending);
        DungeonInfrastructureCommandOutcomeSaveData roundTrip =
            JsonUtility.FromJson<DungeonInfrastructureCommandOutcomeSaveData>(
                json);
        FakeCommitter restoredCommitter = new();
        InfrastructureCommandOutcomeRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            restoredCommitter);
        restored.Restore(restored.PrepareRestore(roundTrip));
        restored.DeliverQueued(2L);
        DungeonInfrastructureCommandOutcomeSaveData delivered =
            restored.Capture();
        Require(
            delivered.nextOwnerRevision == 3L
            && delivered.pendingOutcomes.Count == 0
            && restoredCommitter.PendingPrepareCount == 1
            && restoredCommitter.CommitCount == 1,
            "restored infrastructure outbox did not deliver exactly once");

        DungeonInfrastructureCommandOutcomeSaveData malformed =
            JsonUtility.FromJson<DungeonInfrastructureCommandOutcomeSaveData>(
                json);
        malformed.pendingOutcomes[0].absoluteDay = 0;
        RequireThrows(
            () => restored.PrepareRestore(malformed),
            "invalid frozen infrastructure outbox row was accepted");

        DungeonInfrastructureCommandOutcomeSaveData duplicate =
            JsonUtility.FromJson<DungeonInfrastructureCommandOutcomeSaveData>(
                json);
        duplicate.pendingOutcomes.Add(duplicate.pendingOutcomes[0].Clone());
        RequireThrows(
            () => restored.PrepareRestore(duplicate),
            "duplicate infrastructure outbox revision was accepted");

        DungeonInfrastructureCommandOutcomeSaveData stale =
            JsonUtility.FromJson<DungeonInfrastructureCommandOutcomeSaveData>(
                json);
        stale.pendingOutcomes[0].ownerRevision = stale.nextOwnerRevision;
        RequireThrows(
            () => restored.PrepareRestore(stale),
            "stale infrastructure outbox revision was accepted");

        DungeonInfrastructureCommandOutcomeSaveData missingRows =
            JsonUtility.FromJson<DungeonInfrastructureCommandOutcomeSaveData>(
                json);
        missingRows.pendingOutcomes = null;
        RequireThrows(
            () => restored.PrepareRestore(missingRows),
            "null infrastructure outbox collection was accepted");

        InfrastructureCommandOutcomeSaveSection section = new(runtime);
        Require(
            section.SectionId == InfrastructureCommandOutcomeSaveSection.Id
            && section.SectionVersion == 1
            && section.DependsOn.Contains(GameplayOutcomeLedgerSaveSection.Id)
            && section.DependsOn.Contains(ModularFacilityWorldSaveSection.Id),
            "infrastructure command save section contract is incomplete");
    }

    private static InfrastructureCommandOutcomeSource CreateOwnerSource(
        InfrastructureCommandOutcomeKind kind,
        string before,
        string after) => new(
        kind,
        kind == InfrastructureCommandOutcomeKind.ConveyorOverflowApproved
            ? "conveyor-payload:00000001"
            : FacilityId,
        FacilityId,
        3,
        9,
        before,
        after);

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FakePrepared : IPreparedInfrastructureCommandOutcome
    {
        public FakePrepared(
            in InfrastructureCommandOutcomeSource source,
            long ownerRevision,
            in InfrastructureCommandOutcomeFrozenContext context)
        {
            Source = source;
            OwnerRevision = ownerRevision;
            FrozenContext = context;
        }

        public long OwnerRevision { get; }
        public InfrastructureCommandOutcomeSource Source { get; }
        public InfrastructureCommandOutcomeFrozenContext FrozenContext { get; }
    }

    private sealed class FakeCommitter : IInfrastructureCommandOutcomeCommitter
    {
        public bool RejectNextCommit { get; set; }
        public int CommitCount { get; private set; }
        public int PendingPrepareCount { get; private set; }

        public bool TryPrepare(
            in InfrastructureCommandOutcomeSource source,
            long ownerRevision,
            out IPreparedInfrastructureCommandOutcome prepared,
            out string failureReason)
        {
            prepared = new FakePrepared(
                source,
                ownerRevision,
                CreateContext());
            failureReason = string.Empty;
            return true;
        }

        public bool TryPreparePending(
            InfrastructureCommandOutcomeOutboxSaveData pending,
            out IPreparedInfrastructureCommandOutcome prepared,
            out string failureReason)
        {
            PendingPrepareCount++;
            prepared = new FakePrepared(
                pending.ToSource(),
                pending.ownerRevision,
                pending.ToFrozenContext());
            failureReason = string.Empty;
            return true;
        }

        public InfrastructureCommandOutcomeCommitResult Commit(
            IPreparedInfrastructureCommandOutcome prepared)
        {
            CommitCount++;
            if (RejectNextCommit)
            {
                RejectNextCommit = false;
                return new InfrastructureCommandOutcomeCommitResult(
                    false,
                    "injected-rejection");
            }
            return new InfrastructureCommandOutcomeCommitResult(true, "committed");
        }

        public void Cancel(IPreparedInfrastructureCommandOutcome prepared)
        {
        }

        private static InfrastructureCommandOutcomeFrozenContext CreateContext() =>
            new(
                "시험 설비",
                "infrastructure-command-test-v1",
                (int)KoreanPronunciationMode.AutoHangulDisplay,
                string.Empty,
                (int)KoreanFinalConsonantKind.None,
                "infrastructure-command-test-pronunciation-v1",
                "ko-KR",
                17);
    }

    private sealed class FixedDisplayNames : IGameplayOutcomeDisplayNameQuery
    {
        public bool TryGetCurrentName(
            GameplayEntityId entityId,
            out KoreanNameSnapshot name)
        {
            if (entityId.Kind.Equals(
                    InfrastructureCommandOutcomeIds.FacilityKind)
                && string.Equals(
                    entityId.Value,
                    FacilityId,
                    StringComparison.Ordinal))
            {
                name = new KoreanNameSnapshot(
                    "제련소",
                    "infrastructure-command-facility-v1",
                    KoreanPronunciationHint.AutoHangulDisplay(
                        "infrastructure-command-pronunciation-v1"),
                    "ko-KR");
                return true;
            }
            name = default;
            return false;
        }
    }

    private sealed class FakeCalendar : IGameCalendar
    {
        public int Day => 17;
        public int Hour => 8;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => TimeOfDay.Morning;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int day, int hour)
        {
        }
    }
}
#endif
