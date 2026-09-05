using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class
    CharacterMedicalSupplyDestinationDrainCheckpointGcDebugScenarios
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Character Medical Supply Destination Drain Checkpoint GC")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log(
            "[CharacterMedicalSupplyDestinationDrainCheckpointGcDebugScenarios] PASS");
    }

    public static void RunAll()
    {
        VerifyEmptyAlreadyApplied();
        VerifySuccessfulCollectionIsChildFirstUpperLast();
        VerifyChildPublishFailureLeavesUpperIntact();
        VerifyUpperDriftRollsChildBack();
        VerifyActiveJoinSurvivesClosedCollection();
        VerifyOrder310ParticipantContract();
    }

    private static void VerifyEmptyAlreadyApplied()
    {
        Fixture fixture = new(closedCount: 0, withActiveJoin: false);
        CharacterMedicalSupplyDestinationDrainCheckpointGcResult first =
            fixture.Coordinator.OnDurableSaveCommitted("slot:test", Digest);
        CharacterMedicalSupplyDestinationDrainCheckpointGcResult replay =
            fixture.Coordinator.OnDurableSaveCommitted("slot:test", Digest);

        Require(first.Status ==
                CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
                    .AlreadyApplied
            && replay.Status ==
                CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
                    .AlreadyApplied
            && fixture.Children.Drains.Count == 0
            && !fixture.Children.HasActiveCandidate,
            "An empty Character Medical destination-drain checkpoint was not idempotent.");
    }

    private static void VerifySuccessfulCollectionIsChildFirstUpperLast()
    {
        Fixture fixture = new(closedCount: 1, withActiveJoin: false);
        CharacterMedicalOrder live = fixture.Order;
        bool childObservedUpper = false;
        fixture.Children.BeforePublish = () =>
        {
            childObservedUpper = live.treatmentDestinationDrainJoins.Count == 1
                && live.treatmentDestinationDrainJoins[0].phase ==
                    CharacterMedicalSupplyDestinationDrainPhase
                        .ClosedAwaitingCheckpointGc;
        };

        CharacterMedicalSupplyDestinationDrainCheckpointGcResult result =
            fixture.Coordinator.OnDurableSaveCommitted("slot:test", Digest);

        Require(result.Status ==
                CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
                    .Applied
            && childObservedUpper
            && live.treatmentDestinationDrainJoins.Count == 0
            && fixture.Children.Drains.Count == 0
            && !fixture.Children.HasActiveCandidate,
            "Character Medical checkpoint GC was not child-first/upper-last.");
    }

    private static void VerifyChildPublishFailureLeavesUpperIntact()
    {
        Fixture fixture = new(closedCount: 1, withActiveJoin: false);
        fixture.Children.FailPublish = true;
        string upperBefore = JsonUtility.ToJson(fixture.Order);

        CharacterMedicalSupplyDestinationDrainCheckpointGcResult result =
            fixture.Coordinator.OnDurableSaveCommitted("slot:test", Digest);

        Require(result.Status ==
                CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
                    .Corruption
            && string.Equals(
                JsonUtility.ToJson(fixture.Order),
                upperBefore,
                StringComparison.Ordinal)
            && fixture.Children.Drains.Count == 1
            && !fixture.Children.HasActiveCandidate,
            "Child publish failure leaked a partial Character Medical collection.");
    }

    private static void VerifyUpperDriftRollsChildBack()
    {
        Fixture fixture = new(closedCount: 1, withActiveJoin: false);
        fixture.Children.AfterPublish = () =>
            fixture.Order.completedTreatmentWork += 1f;

        CharacterMedicalSupplyDestinationDrainCheckpointGcResult result =
            fixture.Coordinator.OnDurableSaveCommitted("slot:test", Digest);

        Require(result.Status ==
                CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
                    .Corruption
            && fixture.Order.treatmentDestinationDrainJoins.Count == 1
            && fixture.Order.treatmentDestinationDrainJoins[0].phase ==
                CharacterMedicalSupplyDestinationDrainPhase
                    .ClosedAwaitingCheckpointGc
            && fixture.Children.Drains.Count == 1
            && !fixture.Children.HasActiveCandidate,
            "Upper drift did not rollback the exact Character Medical child tombstone.");
    }

    private static void VerifyActiveJoinSurvivesClosedCollection()
    {
        Fixture fixture = new(closedCount: 2, withActiveJoin: true);
        CharacterMedicalOrder live = fixture.Order;
        int activeSequence = live.treatmentDestinationSequence;
        string activeDestination = live.treatmentMaterialDestinationId;
        string activeFacility = live.treatmentFacilityId;
        string activeFingerprint = live.treatmentCapacityFingerprint;

        CharacterMedicalSupplyDestinationDrainCheckpointGcResult result =
            fixture.Coordinator.OnDurableSaveCommitted("slot:test", Digest);

        CharacterMedicalSupplyDestinationDrainJoinData remaining = live
            .treatmentDestinationDrainJoins.Single();
        FacilityBufferDestinationCustodyDrainSnapshot remainingChild = fixture
            .Children.Drains.Single();
        Require(result.Status ==
                CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
                    .Applied
            && remaining.destinationSequence == activeSequence
            && remaining.phase ==
                CharacterMedicalSupplyDestinationDrainPhase.Prepared
            && remainingChild.StepOperationId == remaining.stepOperationId
            && remainingChild.Phase ==
                FacilityBufferDestinationCustodyDrainPhase.Prepared
            && live.state ==
                CharacterMedicalOrderState.MaterialDestinationDraining
            && live.statusCode ==
                CharacterMedicalStatusCode.MaterialDestinationDraining
            && live.treatmentDestinationSequence == activeSequence
            && live.treatmentMaterialDestinationId == activeDestination
            && live.treatmentFacilityId == activeFacility
            && live.treatmentCapacityFingerprint == activeFingerprint
            && !fixture.Children.HasActiveCandidate,
            "Closed-N collection changed the active Character Medical destination lifetime.");
    }

    private static void VerifyOrder310ParticipantContract()
    {
        (CharacterMedicalSupplyDestinationDrainCheckpointGcStatus domain,
            DungeonDurableSaveCommitStatus durable)[] cases =
        {
            (CharacterMedicalSupplyDestinationDrainCheckpointGcStatus.Applied,
                DungeonDurableSaveCommitStatus.Applied),
            (CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
                    .AlreadyApplied,
                DungeonDurableSaveCommitStatus.AlreadyApplied),
            (CharacterMedicalSupplyDestinationDrainCheckpointGcStatus.Deferred,
                DungeonDurableSaveCommitStatus.Deferred),
            (CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
                    .Corruption,
                DungeonDurableSaveCommitStatus.Corruption)
        };

        foreach ((CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
                     domain,
                 DungeonDurableSaveCommitStatus durable) in cases)
        {
            CharacterMedicalSupplyDestinationDrainCheckpointGcDurableSaveParticipant
                participant = new(new StubCoordinator(domain));
            DungeonDurableSaveCommitResult result = participant
                .OnDurableSaveCommitted(
                    new DungeonDurableSaveCommitContext("slot:test", Digest));
            Require(participant.ParticipantId ==
                    CharacterMedicalSupplyDestinationDrainCheckpointGcDurableSaveParticipant
                        .Id
                && participant.ParticipantId ==
                    "310.character-medical-supply-destination-drain-checkpoint-gc"
                && participant.Order == 310
                && result.ParticipantId == participant.ParticipantId
                && result.Status == durable,
                $"Order-310 participant mapping failed for {domain}.");
        }
    }

    private sealed class Fixture
    {
        internal Fixture(int closedCount, bool withActiveJoin)
        {
            DungeonRuntimeAggregateRootStore roots = new();
            Order = CreateOrder(closedCount, withActiveJoin);
            SeedAggregate(roots, Order);
            Children = new FakeChildAuthority();
            foreach (CharacterMedicalSupplyDestinationDrainJoinData join in
                     Order.treatmentDestinationDrainJoins)
            {
                Children.Add(CreateChild(Order, join));
            }
            Coordinator = new
                CharacterMedicalSupplyDestinationDrainCheckpointGcCoordinator(
                    roots,
                    Children,
                    Children);
        }

        internal CharacterMedicalOrder Order { get; }
        internal FakeChildAuthority Children { get; }
        internal CharacterMedicalSupplyDestinationDrainCheckpointGcCoordinator
            Coordinator { get; }
    }

    private static void SeedAggregate(
        DungeonRuntimeAggregateRootStore roots,
        CharacterMedicalOrder order)
    {
        Type stateType = typeof(CharacterMedicalRuntime).Assembly.GetType(
            "CharacterMedicalAggregateState",
            throwOnError: true);
        object state = Activator.CreateInstance(stateType, nonPublic: true);
        System.Reflection.PropertyInfo ordersProperty = stateType.GetProperty(
            "Orders",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        if (ordersProperty?.GetValue(state) is not
            List<CharacterMedicalOrder> orders)
        {
            throw new InvalidOperationException(
                "Character Medical aggregate Orders authority is unavailable.");
        }
        orders.Add(order);
        System.Reflection.MethodInfo replace =
            typeof(DungeonRuntimeAggregateRootStore)
                .GetMethods()
                .Single(value => value.Name == "Replace"
                    && value.IsGenericMethodDefinition)
                .MakeGenericMethod(stateType);
        replace.Invoke(roots, new[] { state });
    }

    private static CharacterMedicalOrder CreateOrder(
        int closedCount,
        bool withActiveJoin)
    {
        const string orderId = "medical-order:checkpoint-gc-fixture";
        int activeSequence = withActiveJoin ? closedCount + 1 : 0;
        CharacterMedicalOrder order = new()
        {
            orderId = orderId,
            patientId = "character:checkpoint-gc-patient",
            treatmentFacilityId = withActiveJoin
                ? FacilityId(activeSequence)
                : string.Empty,
            state = withActiveJoin
                ? CharacterMedicalOrderState.MaterialDestinationDraining
                : CharacterMedicalOrderState.Cancelled,
            treatmentMaterialDestinationId = withActiveJoin
                ? FormatDestinationId(orderId, activeSequence)
                : string.Empty,
            nextTreatmentMaterialDestinationSequence = closedCount + 2,
            treatmentDestinationSequence = activeSequence,
            treatmentBufferCapacityGrams = withActiveJoin ? 900L : 0L,
            treatmentMassAuthorityRevision = withActiveJoin ? 1L : 0L,
            treatmentCapacityFingerprint = withActiveJoin ? Digest : string.Empty,
            treatmentSupplyOperationSequence = 1,
            statusCode = withActiveJoin
                ? CharacterMedicalStatusCode.MaterialDestinationDraining
                : CharacterMedicalStatusCode.Cancelled,
            statusParameters = new List<string>(),
            treatmentDestinationDrainJoins = new List<
                CharacterMedicalSupplyDestinationDrainJoinData>()
        };

        for (int sequence = 1; sequence <= closedCount; sequence++)
        {
            order.treatmentDestinationDrainJoins.Add(CreateJoin(
                orderId,
                sequence,
                CharacterMedicalSupplyDestinationDrainPhase
                    .ClosedAwaitingCheckpointGc));
        }
        if (withActiveJoin)
        {
            order.treatmentDestinationDrainJoins.Add(CreateJoin(
                orderId,
                activeSequence,
                CharacterMedicalSupplyDestinationDrainPhase.Prepared));
        }
        return order;
    }

    private static CharacterMedicalSupplyDestinationDrainJoinData CreateJoin(
        string orderId,
        int sequence,
        CharacterMedicalSupplyDestinationDrainPhase phase)
    {
        bool closed = phase == CharacterMedicalSupplyDestinationDrainPhase
            .ClosedAwaitingCheckpointGc;
        return new CharacterMedicalSupplyDestinationDrainJoinData
        {
            destinationSequence = sequence,
            phase = phase,
            targetState = closed
                ? CharacterMedicalOrderState.Cancelled
                : CharacterMedicalOrderState.AwaitingBed,
            targetStatusCode = closed
                ? CharacterMedicalStatusCode.Cancelled
                : CharacterMedicalStatusCode.AwaitingBed,
            targetStatusParameters = new List<string>(),
            parentOperationId = FormatParentOperationId(orderId, sequence),
            stepOperationId = FormatStepOperationId(orderId, sequence),
            ownerFacilityId = FacilityId(sequence),
            sourceDestinationId = FormatDestinationId(orderId, sequence),
            sourceBufferCapacityGrams = 900L,
            sourceMassAuthorityRevision = 1L,
            sourceCapacityFingerprint = Digest,
            requestFingerprint = Digest,
            commitId = closed
                ? $"commit:character-medical-checkpoint:{sequence}"
                : string.Empty,
            receiptFingerprint = closed ? Digest : string.Empty,
            inputQuantity = closed ? 1 : 0,
            inputMassGrams = closed ? 500L : 0L,
            ownerX = 3 + sequence,
            ownerY = 7
        };
    }

    private static FacilityBufferDestinationCustodyDrainSnapshot CreateChild(
        CharacterMedicalOrder order,
        CharacterMedicalSupplyDestinationDrainJoinData join)
    {
        bool closed = join.phase == CharacterMedicalSupplyDestinationDrainPhase
            .ClosedAwaitingCheckpointGc;
        return new FacilityBufferDestinationCustodyDrainSnapshot(
            join.parentOperationId,
            join.stepOperationId,
            FormatOwnerStableId(order.orderId),
            order.orderId,
            join.ownerFacilityId,
            join.sourceDestinationId,
            join.sourceCapacityFingerprint,
            join.requestFingerprint,
            join.ownerX,
            join.ownerY,
            closed
                ? FacilityBufferDestinationCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc
                : FacilityBufferDestinationCustodyDrainPhase.Prepared,
            0,
            0,
            0,
            0,
            join.inputQuantity,
            join.inputMassGrams,
            closed ? join.inputQuantity : 0,
            closed ? join.inputMassGrams : 0L,
            join.commitId,
            join.receiptFingerprint);
    }

    private static string FacilityId(int sequence) =>
        $"facility:character-medical-checkpoint:{sequence}";

    private static string FormatOwnerStableId(string orderId) =>
        "character-medical-order:" + (orderId ?? string.Empty);

    private static string FormatParentOperationId(
        string orderId,
        int destinationSequence) =>
        "character-medical-supply-drain:" + (orderId ?? string.Empty)
        + $":{destinationSequence:D8}";

    private static string FormatStepOperationId(
        string orderId,
        int destinationSequence) =>
        FormatParentOperationId(orderId, destinationSequence) + ":custody";

    private static string FormatDestinationId(
        string orderId,
        int destinationSequence) =>
        "facility-input:exact:medical.character-supply:"
        + (orderId ?? string.Empty) + $":{destinationSequence:D8}";

    private sealed class FakeChildAuthority :
        IFacilityBufferDestinationCustodyDrainLiveQuery,
        IFacilityBufferDestinationCustodyDrainCheckpointGcPort
    {
        private readonly List<FacilityBufferDestinationCustodyDrainSnapshot>
            live = new();
        private Candidate active;

        public IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot>
            Drains => live.ToArray();
        internal bool FailPublish { get; set; }
        internal Action BeforePublish { get; set; }
        internal Action AfterPublish { get; set; }
        internal bool HasActiveCandidate => active != null;

        internal void Add(
            FacilityBufferDestinationCustodyDrainSnapshot snapshot) =>
            live.Add(snapshot);

        public bool TryPrepareCheckpointGarbageCollection(
            IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot>
                snapshots,
            out IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
                candidate,
            out string failureReason)
        {
            candidate = null;
            failureReason = string.Empty;
            if (active != null)
            {
                failureReason = "fixture-already-active";
                return false;
            }
            active = new Candidate(snapshots?.ToArray()
                ?? Array.Empty<FacilityBufferDestinationCustodyDrainSnapshot>());
            candidate = active;
            return true;
        }

        public bool TryPublishCheckpointGarbageCollection(
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
                candidate,
            out string failureReason)
        {
            Candidate exact = Require(candidate);
            BeforePublish?.Invoke();
            if (FailPublish)
            {
                failureReason = "fixture-publish-failed";
                return false;
            }
            foreach (FacilityBufferDestinationCustodyDrainSnapshot expected in
                     exact.Expected)
            {
                FacilityBufferDestinationCustodyDrainSnapshot current = live
                    .SingleOrDefault(value => value.StepOperationId ==
                        expected.StepOperationId);
                if (!FacilityBufferDestinationCustodyDrainProjection
                    .AreExactEqual(current, expected))
                {
                    failureReason = "fixture-child-drift";
                    return false;
                }
            }
            foreach (FacilityBufferDestinationCustodyDrainSnapshot expected in
                     exact.Expected)
            {
                live.RemoveAll(value => value.StepOperationId ==
                    expected.StepOperationId);
            }
            exact.Published = true;
            AfterPublish?.Invoke();
            failureReason = string.Empty;
            return true;
        }

        public void RollbackCheckpointGarbageCollection(
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
                candidate)
        {
            Candidate exact = Require(candidate);
            if (!exact.Published)
                return;
            live.AddRange(exact.Expected);
            live.Sort((left, right) => string.CompareOrdinal(
                left.StepOperationId,
                right.StepOperationId));
            exact.Published = false;
        }

        public void CompleteCheckpointGarbageCollection(
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
                candidate)
        {
            Candidate exact = Require(candidate);
            exact.Completed = true;
            active = null;
        }

        private Candidate Require(
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
                candidate)
        {
            if (candidate is not Candidate exact
                || !ReferenceEquals(exact, active)
                || exact.Completed)
            {
                throw new InvalidOperationException(
                    "fixture-checkpoint-candidate-invalid");
            }
            return exact;
        }

        private sealed class Candidate :
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
        {
            internal Candidate(
                IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot>
                    expected)
            {
                Expected = expected;
            }

            internal IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot>
                Expected { get; }
            internal bool Published { get; set; }
            internal bool Completed { get; set; }
        }
    }

    private sealed class StubCoordinator :
        ICharacterMedicalSupplyDestinationDrainCheckpointGcCoordinator
    {
        private readonly CharacterMedicalSupplyDestinationDrainCheckpointGcStatus
            status;

        internal StubCoordinator(
            CharacterMedicalSupplyDestinationDrainCheckpointGcStatus status)
        {
            this.status = status;
        }

        public CharacterMedicalSupplyDestinationDrainCheckpointGcResult
            OnDurableSaveCommitted(
                string slotId,
                string serializedByteDigest) => new(status, "fixture");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
