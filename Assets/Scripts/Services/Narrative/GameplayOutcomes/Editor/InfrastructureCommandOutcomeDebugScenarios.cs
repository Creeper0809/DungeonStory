#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class InfrastructureCommandOutcomeDebugScenarios
{
    private const string FacilityId = "building:qa-infrastructure-command";
    private const string FacilityName = "제련소";

    public static bool RunAll(bool logSuccess = false)
    {
        VerifyBridgeAndQuery();
        VerifyAutonomousBridgeAndQuery();
        VerifyOwnerTransactionAndSave();
        if (logSuccess)
            Debug.Log("[Infrastructure Command Outcome] PASS");
        return true;
    }

    private static void VerifyAutonomousBridgeAndQuery()
    {
        using Fixture fixture = new("run:infrastructure-autonomous-editor");
        IndustrialTransitionKind[] kinds =
        {
            IndustrialTransitionKind.PowerFuelConsumed,
            IndustrialTransitionKind.PowerBreakerTripped,
            IndustrialTransitionKind.WastewaterBatchCompleted,
            IndustrialTransitionKind.FluidLeakEpisode,
            IndustrialTransitionKind.WastewaterBackflow,
            IndustrialTransitionKind.PipeFreezeLatched,
            IndustrialTransitionKind.PipeFreezeRecovered
        };
        for (int index = 0; index < kinds.Length; index++)
        {
            IndustrialTransitionFactData draft = CreateDraft(
                fixture.Runtime,
                kinds[index],
                NeutralState(),
                NeutralState());
            Require(
                fixture.Runtime.TryPrepare(
                    draft,
                    out IDomainFactStage prepared,
                    out string failure),
                "infrastructure autonomous prepare failed: " + failure);
            Require(
                fixture.Runtime.CommitReversible(prepared).Completed,
                "infrastructure autonomous commit failed for " + kinds[index]);
        }

        GameplayOutcomeQueryPage page = fixture.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(20),
            OutcomeFilter.All);
        Require(
            page.Items.Count == kinds.Length
            && page.Items.All(value => value.Exact?.outcomeTypeId
                == InfrastructureAutonomousOutcomeIds.Transitioned.Value)
            && page.Items.Select(value => value.Exact.facts.Single(fact =>
                    fact.factId == InfrastructureAutonomousOutcomeIds
                        .TransitionKindFact.Value).value)
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(kinds
                    .Select(IndustrialTransitionFactObservation.MapKind)
                    .Select(InfrastructureAutonomousOutcomeIds.StableKind)
                    .OrderBy(value => value, StringComparer.Ordinal)),
            "the seven autonomous infrastructure transitions were not recorded exactly once");
        Require(
            page.Items.All(value => value.Exact.outcomeTypeId
                != InfrastructureCommandOutcomeIds.Applied.Value),
            "autonomous infrastructure transitions were mislabeled as manual commands");

        GameplayOutcomeSnapshot first = page.Items
            .Select(value => value.Exact)
            .Single(value => value.ownerRevision == 1L);
        GameplayOutcomeId firstId = new(
            new GameplayOutcomeRunId(first.runId),
            first.sequence);
        GameplayEntityId facility = new(
            InfrastructureAutonomousOutcomeIds.FacilityKind,
            FacilityId);
        Require(
            fixture.Ledger.TryProject(
                firstId,
                new NarrativePerspectiveContext(
                    facility,
                    NarrativePerspectiveKind.Facility,
                    "ko-KR"),
                out NarrativeView view)
            && view.Text.Contains("연료", StringComparison.Ordinal),
            "autonomous infrastructure narrative projection was not stable");
    }

    private static void VerifyBridgeAndQuery()
    {
        using Fixture fixture = new("run:infrastructure-command-editor");
        IndustrialStateData before = NeutralState();
        before.filter.itemIds.Add("material:ore");
        IndustrialStateData after = NeutralState();
        after.filter.itemIds.Add("material:ingot");
        IndustrialTransitionFactData draft = CreateDraft(
            fixture.Runtime,
            IndustrialTransitionKind.ConveyorFilterChanged,
            before,
            after);
        Require(
            fixture.Runtime.TryPrepare(
                draft,
                out IDomainFactStage prepared,
                out string prepareFailure),
            "infrastructure command prepare failed: " + prepareFailure);
        IndustrialTransitionFactData factData = draft.Clone();
        factData.ownerRevision = prepared.OwnerRevision;
        Require(
            prepared.OwnerRevision == 1L
            && prepared.OperationKey
                == InfrastructureCommandOutcomeIds.ResultKey(1L).ToString(),
            "prepared infrastructure command lost its typed identity");
        DomainFactDelivery committed = fixture.Runtime.CommitReversible(prepared);
        Require(
            committed.Completed,
            "infrastructure command commit failed: " + committed.Detail);

        const string expectedBefore =
            "items=material:ore;categories=;materials=;forbidden=false;quality=false:Awful:Mythic;freshness=false:0:1;contaminated=true";
        const string expectedAfter =
            "items=material:ingot;categories=;materials=;forbidden=false;quality=false:Awful:Mythic;freshness=false:0:1;contaminated=true";
        GameplayEntityId facility = new(
            InfrastructureCommandOutcomeIds.FacilityKind,
            FacilityId);
        GameplayOutcomeQueryPage global = fixture.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage facilityPage = fixture.Ledger.GetForEntity(
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
            && exact.participants[0].displayText == FacilityName
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
                && value.value == expectedBefore)
            && exact.facts.Any(value =>
                value.factId
                    == InfrastructureCommandOutcomeIds.AfterValueFact.Value
                && value.value == expectedAfter),
            "infrastructure command outcome lost deterministic typed fact data");

        GameplayOutcomeId outcomeId = new(
            new GameplayOutcomeRunId(exact.runId),
            exact.sequence);
        Require(
            fixture.Ledger.TryProject(
                outcomeId,
                new NarrativePerspectiveContext(
                    facility,
                    NarrativePerspectiveKind.Facility,
                    "ko-KR"),
                out NarrativeView view)
            && view.Text.Contains("제련소는", StringComparison.Ordinal)
            && view.Text.Contains("운송 필터 변경", StringComparison.Ordinal),
            "infrastructure command Korean projection was not stable");

        DomainFactDelivery replay = fixture.Dispatcher.Replay(
            new IndustrialTransitionFact(factData));
        Require(
            replay.Completed
            && replay.OperationKey == committed.OperationKey
            && replay.PublicationId == committed.PublicationId
            && replay.Fingerprint == committed.Fingerprint
            && fixture.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1,
            "infrastructure command replay changed identity/hash or duplicated the canonical result");

        IndustrialTransitionFactData drifted = factData.Clone();
        drifted.after = before.Clone();
        Require(
            fixture.Dispatcher.Replay(new IndustrialTransitionFact(drifted)).Phase
                == DomainFactDeliveryPhase.Rejected,
            "same infrastructure command result key accepted drifted facts");

        GameplayOutcomeLedgerSaveData saved = fixture.Ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = new(
            fixture.Registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 128),
            new GameplayOutcomeRunId("run:infrastructure-command-restore"),
            1L,
            fixture.Calendar);
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
        using Fixture fixture = new("run:infrastructure-owner-editor");
        IndustrialTransitionFactData first = CreateDraft(
            fixture.Runtime,
            IndustrialTransitionKind.PowerPriorityChanged,
            StateWithPriority(PowerPriority.Production),
            StateWithPriority(PowerPriority.Critical));

        Require(
            fixture.Runtime.TryPrepare(
                first,
                out IDomainFactStage rejectedToken,
                out string rejectedPrepareFailure),
            "owner rejection prepare failed: " + rejectedPrepareFailure);
        fixture.Faults.RejectNextCommit = true;
        DomainFactDelivery rejected =
            fixture.Runtime.CommitReversible(rejectedToken);
        DungeonInfrastructureObservationSaveData afterRejection =
            fixture.Runtime.Capture();
        Require(
            rejected.Phase == DomainFactDeliveryPhase.Rejected
            && afterRejection.nextOwnerRevision == 1L
            && afterRejection.pending.Count == 0,
            "definitively rejected reversible command consumed the owner revision");

        Require(
            fixture.Runtime.TryPrepare(
                first,
                out IDomainFactStage committedToken,
                out string committedPrepareFailure),
            "owner commit prepare failed: " + committedPrepareFailure);
        Require(
            fixture.Runtime.CommitReversible(committedToken).Completed
            && fixture.Runtime.Capture().nextOwnerRevision == 2L,
            "reversible command did not advance exactly once");

        IndustrialStateData overflowBefore = NeutralState();
        IndustrialStateData overflowAfter = NeutralState();
        overflowAfter.overflowApproved = true;
        IndustrialTransitionFactData queued = CreateDraft(
            fixture.Runtime,
            IndustrialTransitionKind.ConveyorOverflowApproved,
            overflowBefore,
            overflowAfter,
            "conveyor-payload:00000001");
        Require(
            fixture.Runtime.TryPrepare(
                queued,
                out IDomainFactStage queuedToken,
                out string queuedPrepareFailure),
            "queued command prepare failed: " + queuedPrepareFailure);
        fixture.Faults.InDoubtNextCommit = true;
        DomainFactDelivery uncertain =
            fixture.Runtime.CommitReversible(queuedToken);
        DungeonInfrastructureObservationSaveData pending =
            fixture.Runtime.Capture();
        Require(
            uncertain.Phase == DomainFactDeliveryPhase.InDoubt
            && pending.nextOwnerRevision == 3L
            && pending.pending.Count == 1
            && pending.pending[0].fact.ownerRevision == 2L
            && pending.pending[0].fact.kind
                == IndustrialTransitionKind.ConveyorOverflowApproved
            && pending.pending[0].fact.facilityDisplayName == FacilityName,
            "unknown infrastructure acknowledgement did not retain the typed pending fact");

        string json = JsonUtility.ToJson(pending);
        DungeonInfrastructureObservationSaveData roundTrip =
            JsonUtility.FromJson<DungeonInfrastructureObservationSaveData>(json);
        InfrastructureObservationRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            fixture.Faults,
            fixture.Faults,
            fixture.Calendar);
        restored.Restore(restored.PrepareRestore(roundTrip));
        restored.DeliverQueued(2L);
        DungeonInfrastructureObservationSaveData delivered = restored.Capture();
        Require(
            delivered.nextOwnerRevision == 3L
            && delivered.pending.Count == 0
            && fixture.Faults.ReplayCount == 1,
            "restored infrastructure fact did not reconcile exactly once");

        DungeonInfrastructureObservationSaveData malformed =
            JsonUtility.FromJson<DungeonInfrastructureObservationSaveData>(json);
        malformed.pending[0].fact.absoluteDay = 0;
        RequireThrows(
            () => restored.PrepareRestore(malformed),
            "invalid frozen infrastructure fact was accepted");

        DungeonInfrastructureObservationSaveData duplicate =
            JsonUtility.FromJson<DungeonInfrastructureObservationSaveData>(json);
        duplicate.pending.Add(duplicate.pending[0].Clone());
        RequireThrows(
            () => restored.PrepareRestore(duplicate),
            "duplicate infrastructure fact revision was accepted");

        DungeonInfrastructureObservationSaveData stale =
            JsonUtility.FromJson<DungeonInfrastructureObservationSaveData>(json);
        stale.pending[0].fact.ownerRevision = stale.nextOwnerRevision;
        RequireThrows(
            () => restored.PrepareRestore(stale),
            "stale infrastructure fact revision was accepted");

        DungeonInfrastructureObservationSaveData missingRows =
            JsonUtility.FromJson<DungeonInfrastructureObservationSaveData>(json);
        missingRows.pending = null;
        RequireThrows(
            () => restored.PrepareRestore(missingRows),
            "null infrastructure fact collection was accepted");
    }

    private static IndustrialTransitionFactData CreateDraft(
        IInfrastructureFactJournal journal,
        IndustrialTransitionKind kind,
        IndustrialStateData before,
        IndustrialStateData after,
        string targetId = FacilityId) =>
        journal.CaptureDraft(
            kind,
            targetId,
            FacilityId,
            FacilityName,
            11,
            6,
            before,
            after,
            new IndustrialTransitionDetails());

    private static IndustrialStateData NeutralState() => new();

    private static IndustrialStateData StateWithPriority(PowerPriority value)
    {
        IndustrialStateData state = NeutralState();
        state.powerPriority = value;
        return state;
    }

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

    private sealed class Fixture : IDisposable
    {
        private readonly IDisposable binding;

        internal Fixture(string runId)
        {
            Calendar = new FakeCalendar();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new InfrastructureCommandOutcomeDescriptor(
                        new KoreanJosaFormatter()),
                    new InfrastructureAutonomousOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new InfrastructureCommandOutcomeAdapter(),
                    new InfrastructureAutonomousOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 32,
                    largePageCount: 0,
                    knownResultKeyCapacity: 128),
                new GameplayOutcomeRunId(runId),
                1L,
                Calendar);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                Registry,
                new GameEventBus());
            Dispatcher = new DomainFactDispatcher();
            binding = new IndustrialTransitionFactObserverBinding(
                Registry,
                Ledger,
                recorder,
                new GameplayDomainFactCommitGroup(recorder, Ledger))
                .Connect(Dispatcher);
            Faults = new FaultingFactPorts(Dispatcher);
            Runtime = new InfrastructureObservationRuntime(
                new DungeonRuntimeAggregateRootStore(),
                Faults,
                Faults,
                Calendar);
        }

        internal FakeCalendar Calendar { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal DomainFactDispatcher Dispatcher { get; }
        internal FaultingFactPorts Faults { get; }
        internal InfrastructureObservationRuntime Runtime { get; }

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class FaultingFactPorts :
        IDomainFactStagingPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;

        internal FaultingFactPorts(DomainFactDispatcher inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool RejectNextCommit { get; set; }
        internal bool InDoubtNextCommit { get; set; }
        internal int ReplayCount { get; private set; }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failure)
        {
            if (!inner.TryStage(fact, out IDomainFactStage prepared, out failure))
            {
                stage = null;
                return false;
            }
            stage = new FaultingStage(this, prepared);
            return true;
        }

        public bool ReplayBatch(
            System.Collections.Generic.IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            ReplayCount++;
            return inner.ReplayBatch(facts, results, out failure);
        }

        private sealed class FaultingStage : IDomainFactStage
        {
            private readonly FaultingFactPorts owner;
            private readonly IDomainFactStage inner;

            internal FaultingStage(
                FaultingFactPorts owner,
                IDomainFactStage inner)
            {
                this.owner = owner;
                this.inner = inner;
            }

            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;

            public DomainFactDelivery Commit()
            {
                if (owner.RejectNextCommit)
                {
                    owner.RejectNextCommit = false;
                    inner.Cancel();
                    return DomainFactDelivery.Reject(
                        "injected-definitive-rejection");
                }
                DomainFactDelivery result = inner.Commit();
                if (!owner.InDoubtNextCommit)
                    return result;
                owner.InDoubtNextCommit = false;
                return new DomainFactDelivery(
                    DomainFactDeliveryPhase.InDoubt,
                    result.OperationKey,
                    result.PublicationId,
                    result.Fingerprint,
                    "injected-unknown-acknowledgement");
            }

            public void Cancel() => inner.Cancel();
        }
    }

    internal sealed class FakeCalendar : IGameCalendar
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
