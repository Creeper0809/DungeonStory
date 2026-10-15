#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using PhysicalFixture = DurableFacilityEquipmentUseRuntimeDebugScenarios.FakePhysicalPort;

public static class CareerMentorshipFactDebugScenarios
{
    public static void RunBatch()
    {
        Require(Application.isBatchMode, "B115 requires isolated batch execution.");
        VerifyRejection();
        VerifyRecovery(false);
        VerifyRecovery(true);
        VerifyActivity();
        MigratedProducerRunOffenseEntryE2EDebugScenarios.RunCareerMentorshipAwardObserverFocused();
        CareerDurableEquipmentDebugScenarios.RunAll();
        Require(ObservedCareerArcCausalProductionEntryDebugScenarios.RunCausalOwnerFamily(true),
            "B115 career causal checkpoint failed.");
        Debug.Log("B115_CAREER_OBSERVERS=PASS");
    }

    private static void VerifyRejection()
    {
        using var fixture = new Fixture();
        string careerBefore = JsonUtility.ToJson(fixture.Careers.Capture());
        int blocked = 0;
        fixture.Transport.BeforePublish = () =>
        {
            Reject(() => fixture.Capture()); blocked++;
            Reject(() => fixture.RawAward()); blocked++;
        };
        fixture.Transport.Reject = true;
        Require(!fixture.RawAward() && blocked == 2 && fixture.Durability == 80d
            && fixture.Physical.ReplaceCalls == 1 && fixture.Physical.RestoreCalls == 1
            && JsonUtility.ToJson(fixture.Careers.Capture()) == careerBefore
            && fixture.NativeCount == 0 && fixture.Journal.Capture().pending.Count == 0,
            "B115 rejected award did not restore wear/career or leaked observation.");
        fixture.Transport.BeforePublish = null;
        fixture.Transport.Reject = false;
        Require(fixture.RawAward() && fixture.Durability == 79.5d
            && fixture.Careers.Mentorships.Single().LastAwardAbsoluteDay == Day
            && fixture.NativeCount == 1, "B115 rejected award could not retry.");
        Debug.Log("B115_CAREER_REJECTION_GUARD=PASS");
    }

    private static void VerifyRecovery(bool afterCommit)
    {
        Bundle saved;
        using (var source = new Fixture())
        {
            source.Transport.ThrowBeforeCommit = !afterCommit;
            source.Transport.HideAcknowledgement = afterCommit;
            Require(source.RawAward() && source.Durability == 79.5d && source.AwardCallbacks == 1
                && source.Careers.Mentorships.Single().LastAwardAbsoluteDay == Day
                && source.Journal.Capture().pending.Count == 1
                && source.Facts.Dispatcher.ActiveLeaseCount == 0,
                "B115 unknown ACK undid wear/career or lost its pending receipt.");
            var fact = (CareerMentorshipAwardFact)SocialRitualFactCodec.Decode(source.Journal.Capture().pending.Single());
            var immutable = fact.Snapshot(); immutable.studentId = "character:tampered";
            Require(fact.Snapshot().studentId == Student.Value, "B115 fact exposed mutable identity.");
            if (afterCommit) source.Careers.ClearMentorship(Student);
            saved = source.Capture();
            Validate(saved);
            var altered = RoundTrip(saved); altered.journal.pending[0].publicationKey += "-tampered";
            Reject(() => Validate(altered));
            altered = RoundTrip(saved);
            var invalid = fact.Snapshot(); invalid.afterContentRevision++;
            Reject(() => new CareerMentorshipAwardFact(invalid));
            if (afterCommit)
            {
                var changed = fact.Snapshot(); changed.proficiencyId = "proficiency:tampered";
                altered.journal.pending[0].payload = SocialRitualFactCodec.Encode(new CareerMentorshipAwardFact(changed)).payload;
                Reject(() => Validate(altered));
            }
        }
        using (var restored = new Fixture(saved))
        {
            string careerBefore = JsonUtility.ToJson(restored.Careers.Capture());
            long revisionBefore = restored.Physical.CaptureStack(StackId).ContentRevision;
            restored.Journal.OnRestoreCompleted();
            Require(restored.Journal.Capture().pending.Count == 0 && restored.NativeCount == 1
                && restored.Durability == 79.5d && restored.Physical.ReplaceCalls == 0
                && restored.Physical.CaptureStack(StackId).ContentRevision == revisionBefore
                && restored.AwardCallbacks == 0 && JsonUtility.ToJson(restored.Careers.Capture()) == careerBefore,
                "B115 recovery repeated equipment/career effects or native result.");
            string complete = JsonUtility.ToJson(restored.Capture());
            Require(restored.Journal.RecoverPending(out _) && JsonUtility.ToJson(restored.Capture()) == complete,
                "B115 second replay changed state.");
        }
        Debug.Log(afterCommit ? "B115_CAREER_ACK_AFTER=PASS" : "B115_CAREER_ACK_BEFORE=PASS");
    }

    private static void VerifyActivity()
    {
        var events = new GameEventBus();
        var host = new GameObject("B115 mentorship activity fixture");
        try
        {
            var actor = host.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.InjectWithDomainEvents(host, events);
            actor.EnsureRuntimeState();
            var teacher = new CareerMentorshipCompletedEvent(actor, "제자", "proficiency:cooking", true);
            var learner = new CareerMentorshipCompletedEvent(actor, "스승", "proficiency:cooking", false);
            Require(CharacterActionActivityObserver.Project(teacher).FactText == "제자에게 proficiency:cooking 숙련을 가르쳤다."
                && CharacterActionActivityObserver.Project(learner).FactText == "스승에게 proficiency:cooking 숙련을 배웠다."
                && !CharacterActionActivityObserver.Project(teacher).NarrativeEligible,
                "B115 mentorship display semantics changed.");
            int count = 0;
            void Capture(CharacterLogEntry _) => count++;
            actor.LogComponent.OnLogAdded += Capture;
            try
            {
                using var observer = new CharacterActionActivityObserver(events);
                observer.Initialize(); observer.Initialize();
                actor.PublishDomainEvent(teacher); Require(count == 1, "B115 duplicate activity subscription.");
                observer.Dispose(); actor.PublishDomainEvent(learner); Require(count == 1, "B115 detached activity still wrote.");
                observer.Initialize(); actor.PublishDomainEvent(learner); Require(count == 2, "B115 activity did not reattach once.");
            }
            finally { actor.LogComponent.OnLogAdded -= Capture; }
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
        Debug.Log("B115_CAREER_ACTIVITY=PASS");
    }

    private const int Day = 42;
    private const string StackId = "stack:b115:career-ledger";
    private static readonly CharacterId Mentor = new("character:b115:mentor"), Student = new("character:b115:student");
    private static readonly BuildingInstanceId Academy = new("building:b115:academy");
    private static readonly Vector2Int Position = new(7, 3);

    [Serializable]
    private sealed class Bundle
    {
        public CharacterCareerWorldSaveData career;
        public long contentRevision;
        public List<ItemInstanceComponentSaveData> components;
        public SocialRitualFactSaveData journal;
        public GameplayOutcomeLedgerSaveData ledger;
        public DungeonMigratedProducerOutcomeSaveData counters;
    }
    private static void Validate(Bundle value)
    {
        SocialRitualFactSaveSection.ValidateJson(JsonUtility.ToJson(value.journal));
        SocialRitualFactPreflight.ValidateOwners(value.journal, null, null, null);
        SocialRitualFactPreflight.ValidateJournal(value.journal, value.ledger, value.counters);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly MigratedProducerOutcomeEditorFixture Outcomes = new("run:b115:career-award");
        internal readonly SocialRitualFactEditorFixture Facts;
        internal readonly Transport Transport;
        internal readonly SocialRitualFactJournal Journal;
        internal readonly CareerRuntime Careers;
        internal readonly PhysicalFixture Physical;
        private readonly CareerDurableEquipmentAwardRuntime runtime;
        internal int AwardCallbacks;
        internal int NativeCount => Outcomes.Ledger.CaptureGameplayOutcomes().outbox.Count + Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count;
        internal double Durability => Physical.CaptureStack(StackId).Components.Single().values.Single(v => v.key == "current").decimalValue;
        internal Fixture(Bundle saved = null)
        {
            Facts = new SocialRitualFactEditorFixture(Outcomes);
            Transport = new Transport(Facts.Dispatcher);
            Journal = new SocialRitualFactJournal(Transport, Transport);
            Careers = new CareerRuntime(Outcomes.AggregateRootStore);
            var policies = new DurableFacilityEquipmentPolicyRegistry(new IDurableFacilityEquipmentPolicySource[] { new CareerDurableEquipmentPolicySource() });
            Require(policies.TryGetPolicy(CareerDurableEquipmentPolicySource.PolicyId, out var policy), "B115 policy missing.");
            var slots = new Slots(policy.CreateAssignment(Academy.Value, Academy, Position));
            Physical = new PhysicalFixture(new WorldItemStackSnapshot
            {
                StackId = StackId, ContentRevision = saved?.contentRevision ?? 7L, ItemId = DurableToolItemRules.CareerLedger,
                Quantity = 1, State = WorldItemStackState.FacilityBuffer, Position = Position,
                DestinationId = slots.Snapshot.DestinationId,
                Components = saved?.components ?? new List<ItemInstanceComponentSaveData> { new()
                {
                    componentTypeId = ItemInstanceComponentIds.Durability, schemaVersion = 1, affectsStacking = true,
                    values = new List<ItemStateValueSaveData>
                    {
                        new() { key = "current", kind = ItemStateValueKind.Decimal, decimalValue = 80d },
                        new() { key = "maximum", kind = ItemStateValueKind.Decimal, decimalValue = 100d }
                    }
                } }
            });
            var use = new DurableFacilityEquipmentUseRuntime(slots, slots, Physical, Physical,
                new DurableFacilityEquipmentUsabilityRegistry(new IDurableFacilityEquipmentUsabilityPolicy[] { new PositiveDurabilityComponentUsabilityPolicy() }),
                new DurableFacilityEquipmentWearRegistry(new IDurableFacilityEquipmentWearPolicy[] { new PositiveDurabilityComponentWearPolicy() }));
            runtime = new CareerDurableEquipmentAwardRuntime(policies, slots, use, Careers, Journal);
            if (saved == null)
            {
                Careers.AssignMentorship(Mentor, Student, Academy, new CharacterProficiencyId("proficiency:cooking"));
                Careers.RecordMentorshipWork(Student, Day, true, CareerRules.MentoringWorkAmountPerParticipant);
                Careers.RecordMentorshipWork(Student, Day, false, CareerRules.MentoringWorkAmountPerParticipant);
                return;
            }
            Validate(saved);
            Careers.PublishRestore(Careers.PrepareRestore(saved.career));
            var native = Outcomes.Ledger.PrepareGameplayOutcomeRestore(saved.ledger);
            Outcomes.Ledger.BeginRestoreCandidate(); Outcomes.Ledger.PublishGameplayOutcomeRestore(native);
            Outcomes.Ledger.PublishRestoreCandidate(); Outcomes.Ledger.CompleteRestoreCandidate();
            Outcomes.Transaction.Restore(Outcomes.Transaction.PrepareRestore(saved.counters));
            Journal.Restore(saved.journal);
        }
        internal bool RawAward() => runtime.TryCommitAward(Academy, Position, Careers.Mentorships.Single(), Day,
            () => { AwardCallbacks++; return Careers.TryMarkMentoringAwarded(Student, Day); }, out _);
        internal Bundle Capture()
        {
            var physical = Physical.CaptureStack(StackId);
            return RoundTrip(new Bundle
            {
                career = Careers.Capture(), contentRevision = physical.ContentRevision, components = physical.Components.Select(v => v.Clone()).ToList(),
                journal = Journal.Capture(), ledger = Outcomes.Ledger.CaptureGameplayOutcomes(), counters = Outcomes.Transaction.Capture()
            });
        }
        public void Dispose() { Journal.AssertIdle(); Facts.Dispose(); }
    }

    private sealed class Slots : IDurableFacilityEquipmentSlotCommand, IDurableFacilityEquipmentSlotQuery
    {
        internal readonly DurableFacilityEquipmentSlotSnapshot Snapshot;
        internal Slots(DurableFacilityEquipmentAssignment assignment)
        {
            Snapshot = new DurableFacilityEquipmentSlotSnapshot(assignment, 1L,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(assignment.Key, 1L),
                DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(assignment.Key, 1L),
                DurableFacilityEquipmentFingerprint.CreateAssignment(assignment),
                new DurableFacilityEquipmentCapacityProjection(assignment.CapacityPolicyKind, new PhysicalMassGrams(1000L), 1L, new string('a', 64)),
                new[] { new DurableFacilityEquipmentRequirementStatus(assignment.Requirements[0], 0, 1) });
        }
        public bool TryCapture(DurableFacilityEquipmentSlotKey key, out DurableFacilityEquipmentSlotSnapshot value)
        { value = key.Equals(Snapshot.Key) ? Snapshot : null; return value != null; }
        public IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> CaptureAll() => new[] { Snapshot };
        public DurableFacilityEquipmentSlotResult TryReconcile(DurableFacilityEquipmentAssignment desired)
        {
            Require(DurableFacilityEquipmentFingerprint.CreateAssignment(desired) == Snapshot.AssignmentFingerprint, "B115 slot drift.");
            return new(DurableFacilityEquipmentSlotStatus.Replay, Snapshot, string.Empty);
        }
        public DurableFacilityEquipmentSlotResult TryEnsureSupply(DurableFacilityEquipmentSlotKey key) =>
            new(DurableFacilityEquipmentSlotStatus.Replay, Snapshot, string.Empty);
        public DurableFacilityEquipmentSlotResult TryClose(DurableFacilityEquipmentSlotKey key, string reasonCode) => throw new InvalidOperationException("B115 unexpected exhaustion.");
        public IReadOnlyList<DurableFacilityEquipmentSlotResult> TryAdvancePending() => Array.Empty<DurableFacilityEquipmentSlotResult>();
    }

    private sealed class Transport : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher dispatcher;
        internal bool Reject, ThrowBeforeCommit, HideAcknowledgement;
        internal Action BeforePublish;
        internal Transport(DomainFactDispatcher dispatcher) => this.dispatcher = dispatcher;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) => dispatcher.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact) => dispatcher.Publish(lease, fact);
        public void Cancel(in DomainFactLease lease) => dispatcher.Cancel(lease);
        public DomainFactDelivery Replay<T>(in T fact) => dispatcher.Replay(fact);
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure)
        {
            BeforePublish?.Invoke();
            if (Reject)
            {
                failure = "B115-controlled-rejection";
                for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Reject(failure);
                return false;
            }
            if (ThrowBeforeCommit) throw new InvalidOperationException("B115-controlled-unknown-before");
            bool accepted = dispatcher.PublishBatch(facts, results, out failure);
            if (HideAcknowledgement && accepted)
                for (int i = 0; i < results.Length; i++) results[i] = DomainFactDelivery.Uncertain("B115-controlled-unknown-after");
            return accepted;
        }
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure) => dispatcher.ReplayBatch(facts, results, out failure);
    }
    private static T RoundTrip<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
    private static void Reject(Action action)
    { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("B115 expected explicit rejection."); }
    private static void Require(bool value, string failure) { if (!value) throw new InvalidOperationException(failure); }
}
#endif
