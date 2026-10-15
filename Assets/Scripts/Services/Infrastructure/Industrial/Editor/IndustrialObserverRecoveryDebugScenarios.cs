#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Short, controlled Editor integration. No natural-play or training-data claim.
public static class IndustrialObserverRecoveryDebugScenarios
{
    public static void RunForBatch()
    {
        Require(InfrastructureCommandOutcomeDebugScenarios.RunAll(), "Industrial mapping/command regression failed.");
        Debug.Log("B126_INDUSTRIAL_21_MAPPING=PASS");
        Require(IndustrialInfrastructureDebugScenarios.RunE12AutonomousOutcomeFocused().StartsWith("PASS"),
            "Industrial autonomous owner regression failed.");
        Debug.Log("B126_INDUSTRIAL_AUTONOMOUS_OWNERS=PASS");
        string log = Application.consoleLogPath;
        Require(!string.IsNullOrWhiteSpace(log) && Path.IsPathRooted(log), "An absolute isolated log path is required.");
        E12CanonicalInfrastructureVerifier.Run(Path.Combine(Path.GetDirectoryName(log), "b126-canonical.json"));
        Debug.Log("B126_INDUSTRIAL_CANONICAL_OWNER_LEDGER=PASS");
        VerifyRejection();
        foreach (bool committed in new[] { false, true })
        {
            VerifyAckRecovery(committed, false);
            VerifyAckRecovery(committed, true);
            Debug.Log("B126_INDUSTRIAL_ACK_" + (committed ? "AFTER" : "BEFORE") + "_JSON_FRESH_JOURNAL=PASS");
        }
        Debug.Log("B126_INDUSTRIAL_OBSERVERS=PASS");
    }

    private static void VerifyRejection()
    {
        using var probe = new Probe(null);
        probe.Transport.Reject = true;
        var before = probe.Journal.Capture();
        Require(probe.Journal.TryPrepare(Draft(probe.Journal), out var stage, out string failure), failure);
        Require(probe.Journal.CommitReversible(stage).Phase == DomainFactDeliveryPhase.Rejected, "Definite rejection lost.");
        Require(JsonUtility.ToJson(before) == JsonUtility.ToJson(probe.Journal.Capture()) && Rows(probe.Ledger.CaptureGameplayOutcomes()).Length == 0,
            "Definite rejection advanced owner or ledger.");
        probe.Transport.Reject = false;
        Require(probe.Journal.TryPrepare(Draft(probe.Journal), out stage, out failure), failure);
        Require(probe.Journal.CommitReversible(stage).Completed, "Rejected command could not retry.");
    }

    private static void VerifyAckRecovery(bool committed, bool irreversible)
    {
        string json;
        using (var source = new Probe(committed))
        {
            var prior = source.Journal.Capture();
            bool fenced = false;
            source.Transport.BeforeCommit = () =>
            {
                MustThrow(() => source.Journal.Capture());
                MustThrow(() => source.Journal.PrepareRestore(prior));
                MustThrow(() => source.Journal.Restore(source.EmptyCandidate));
                MustThrow(() => source.Journal.TryPrepare(Draft(source.Journal), out _, out _));
                fenced = true;
            };
            var draft = Draft(source.Journal);
            Require(source.Journal.TryPrepare(draft, out var stage, out string failure), failure);
            draft.after.connected = false; draft.facilityDisplayName = "Changed after staging";
            if (irreversible)
            {
                source.Journal.QueueCommitted(stage);
                MustThrow(() => source.Journal.Capture());
                source.Journal.DeliverQueued(stage.OwnerRevision, stage);
            }
            else Require(!source.Journal.CommitReversible(stage).Completed, "Lost ACK reported success.");
            Require(fenced, "Publication re-entry boundaries were not exercised.");
            var owner = source.Journal.Capture();
            var ledger = source.Ledger.CaptureGameplayOutcomes();
            Require(owner.nextOwnerRevision == 2 && owner.pending.Count == 1 && owner.pending[0].fact.after.connected
                && owner.pending[0].fact.facilityDisplayName == "검증 설비" && owner.pending[0].fact.absoluteDay == 137,
                "Pending immutable fact/day/name differs.");
            Join(owner, ledger);
            var missing = JObject.Parse(JsonUtility.ToJson(owner));
            ((JObject)missing["pending"][0]["fact"]["after"]).Remove("connected");
            MustThrow(() => IndustrialSaveSectionParsing.ReadObservations(missing.ToString()));
            if (committed)
            {
                var wrong = JsonUtility.FromJson<DungeonInfrastructureObservationSaveData>(JsonUtility.ToJson(owner));
                wrong.pending[0].fact.after.connected = false;
                MustThrow(() => Join(wrong, ledger));
            }
            source.Transport.AllowRecovery = false;
            Require(!source.Journal.TryPrepare(Draft(source.Journal), out _, out _)
                && JsonUtility.ToJson(owner) == JsonUtility.ToJson(source.Journal.Capture()), "Pending fact was bypassed.");
            json = JsonUtility.ToJson(new Checkpoint { owner = owner, ledger = ledger });
        }
        using (var target = new Probe(null))
        {
            var checkpoint = JsonUtility.FromJson<Checkpoint>(json);
            Join(checkpoint.owner, checkpoint.ledger);
            var candidate = target.Ledger.PrepareGameplayOutcomeRestore(checkpoint.ledger);
            target.Ledger.BeginRestoreCandidate(); target.Ledger.PublishGameplayOutcomeRestore(candidate);
            target.Ledger.PublishRestoreCandidate(); target.Ledger.CompleteRestoreCandidate();
            target.Journal.Restore(target.Journal.PrepareRestore(checkpoint.owner));
            target.Calendar.SetDateTime(180, 9);
            target.Journal.Tick();
            var owner = target.Journal.Capture(); var ledger = target.Ledger.CaptureGameplayOutcomes();
            Join(owner, ledger);
            var row = Rows(ledger).Single();
            Require(owner.nextOwnerRevision == 2 && owner.pending.Count == 0 && row.ownerRevision == 1
                && row.absoluteDay == 137 && row.participants.Single().displayText == "검증 설비",
                "Fresh journal recovery changed original identity/name/day.");
            if (committed)
            {
                var original = Rows(checkpoint.ledger).Single();
                Require(row.sequence == original.sequence && row.immutablePayloadHash == original.immutablePayloadHash
                    && ledger.nextSequence == checkpoint.ledger.nextSequence, "Replay duplicated its original row.");
            }
            owner.nextOwnerRevision = 1;
            MustThrow(() => Join(owner, ledger));
            Require(target.Journal.TryPrepare(Draft(target.Journal), out var stage, out string failure), failure);
            Require(target.Journal.CommitReversible(stage).Completed && target.Journal.Capture().nextOwnerRevision == 3
                && Rows(target.Ledger.CaptureGameplayOutcomes()).Length == 2, "Following publication failed.");
        }
    }

    private static IndustrialTransitionFactData Draft(InfrastructureObservationRuntime journal) =>
        journal.CaptureDraft(IndustrialTransitionKind.PowerConnectionChanged, "building:b126-probe", "building:b126-probe",
            "검증 설비", 1, 2, new IndustrialStateData { connected = false }, new IndustrialStateData { connected = true });
    private static GameplayOutcomeSnapshot[] Rows(GameplayOutcomeLedgerSaveData ledger) => ledger.exactOutcomes.Concat(ledger.outbox).ToArray();
    private static void Join(DungeonInfrastructureObservationSaveData owner, GameplayOutcomeLedgerSaveData ledger)
    {
        var report = new DungeonGameRestoreReport();
        IndustrialTransitionFactObservation.ValidateSaveJoin(owner, ledger, report);
        Require(report.Success, string.Join(" | ", report.Errors));
    }
    private static void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }
    private static void MustThrow(Action action)
    { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Expected explicit refusal."); }
    [Serializable] private sealed class Checkpoint
    { public DungeonInfrastructureObservationSaveData owner; public GameplayOutcomeLedgerSaveData ledger; }

    private sealed class Probe : IDisposable
    {
        private readonly DomainFactDispatcher facts = new();
        internal readonly GameplayOutcomeLedger Ledger;
        internal readonly Transport Transport;
        internal readonly ProbeCalendar Calendar = new();
        internal readonly InfrastructureObservationRuntime Journal;
        internal readonly InfrastructureObservationRestoreCandidate EmptyCandidate;
        internal Probe(bool? committed)
        {
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new InfrastructureCommandOutcomeDescriptor(new KoreanJosaFormatter()),
                    new InfrastructureAutonomousOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new InfrastructureCommandOutcomeAdapter(), new InfrastructureAutonomousOutcomeAdapter() });
            Ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(), new GameplayOutcomeRunId("run:b126-industrial"), 1);
            var recorder = new GameplayOutcomeRecorder(Ledger, registry, new GameEventBus());
            facts.Subscribe("infrastructure.transition-observer", new IndustrialTransitionDomainFactObserver(registry, Ledger, recorder,
                new GameplayDomainFactCommitGroup(recorder, Ledger)));
            Transport = new Transport(facts, committed);
            Journal = new InfrastructureObservationRuntime(new DungeonRuntimeAggregateRootStore(), Transport, Transport, Calendar);
            EmptyCandidate = Journal.PrepareRestore(Journal.Capture());
        }
        public void Dispose() => facts.Dispose();
    }
    private sealed class Transport : IDomainFactStagingPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner; private readonly bool? committed;
        internal Action BeforeCommit; internal bool Reject, AllowRecovery = true;
        internal Transport(DomainFactDispatcher inner, bool? committed) { this.inner = inner; this.committed = committed; }
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            if (!inner.TryStage(fact, out var prepared, out failure)) { stage = null; return false; }
            stage = new Stage(this, prepared); return true;
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> requests, DomainFactDelivery[] results, out string failure)
        {
            if (!AllowRecovery) { failure = "b126:recovery-unavailable"; return false; }
            return inner.ReplayBatch(requests, results, out failure);
        }
        private sealed class Stage : IDomainFactStage
        {
            private readonly Transport owner; private readonly IDomainFactStage inner;
            internal Stage(Transport owner, IDomainFactStage inner) { this.owner = owner; this.inner = inner; }
            public string OperationKey => inner.OperationKey; public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay; public bool IsActive => inner.IsActive;
            public DomainFactDelivery Commit()
            {
                owner.BeforeCommit?.Invoke();
                if (owner.Reject) return DomainFactDelivery.Reject("b126:definite-rejection");
                if (!owner.committed.HasValue) return inner.Commit();
                if (owner.committed.Value) Require(inner.Commit().Completed, "Real commit failed before ACK loss.");
                return DomainFactDelivery.Uncertain("b126:lost-ack");
            }
            public void Cancel() => inner.Cancel();
        }
    }
    private sealed class ProbeCalendar : IGameCalendar
    {
        public int Day { get; private set; } = 137; public int Hour { get; private set; } = 8;
        public int Year => Current.Year; public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season; public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour; public float ElapsedSeconds => 0;
        public TimeOfDay TimeOfDay => TimeOfDay.Morning; public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int offset) => GameCalendarRules.ProjectRegional(Day, Hour, offset);
        public void Start() => IsRunning = true; public void SetDateTime(int day, int hour) { Day = day; Hour = hour; }
    }
}
#endif
