#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class DurableFacilityEquipmentSlotCheckpointGcDebugScenarios
{
    private const string Digest0 =
        "0000000000000000000000000000000000000000000000000000000000000000";
    private const string Digest1 =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private const string FacilityId = "building:qa-durable-equipment-gc";
    private const string OwnerDomain = "qa.durable-equipment-gc";
    private const string PolicyId = "policy:qa.durable-equipment-gc";
    private const string ItemId = "record:qa-durable-equipment-gc";
    private static readonly Vector2Int DropPosition = new(17, 11);

    [MenuItem(
        "DungeonStory/Debug/V27/Run Durable Facility Equipment Slot Checkpoint GC")]
    public static void RunAll()
    {
        VerifyEmptyIsAlreadyApplied();
        VerifyClosedChildFirstUpperLastAndSecondCall();
        VerifyUpperPublishFailureRollsChildBack();
        VerifyParticipantIdentityOrderAndStatusMapping();

        Debug.Log(
            "[V27][PASS] Durable facility-equipment slot checkpoint GC preserves "
            + "empty replay, child-first/upper-last collection, rollback, active "
            + "and draining rows, and durable participant status mapping.");
    }

    private static void VerifyEmptyIsAlreadyApplied()
    {
        Fixture fixture = Fixture.Create(includeClosed: false);
        DurableFacilityEquipmentSlotCheckpointGcResult result = fixture
            .Coordinator.OnDurableSaveCommitted("qa-slot", Digest0);

        Require(
            result.Status ==
                DurableFacilityEquipmentSlotCheckpointGcStatus.AlreadyApplied,
            "An empty durable-equipment checkpoint collection was not replay-safe.");
        Require(
            fixture.Events.SequenceEqual(new[]
            {
                "upper.prepare",
                "upper.publish",
                "upper.complete"
            }),
            "An empty checkpoint collection touched the child GC or used the wrong order.");
        fixture.AssertActiveAndDrainingPreserved();
    }

    private static void VerifyClosedChildFirstUpperLastAndSecondCall()
    {
        Fixture fixture = Fixture.Create(includeClosed: true);
        DurableFacilityEquipmentSlotCheckpointGcResult first = fixture
            .Coordinator.OnDurableSaveCommitted("qa-slot", Digest0);

        Require(
            first.Status == DurableFacilityEquipmentSlotCheckpointGcStatus.Applied,
            "A terminal durable-equipment upper/child pair was not collected.");
        Require(
            fixture.Events.SequenceEqual(new[]
            {
                "upper.prepare",
                "child.prepare",
                "child.publish",
                "upper.publish",
                "upper.complete",
                "child.complete"
            }),
            "Durable-equipment checkpoint GC was not child-first and upper-last.");
        Require(
            fixture.State.Slots.All(value => value.AssignmentSequence != 3L)
            && fixture.State.Children.All(value =>
                !string.Equals(
                    value.StepOperationId,
                    fixture.ClosedChild.StepOperationId,
                    StringComparison.Ordinal)),
            "The collected terminal upper/child pair remained live.");
        fixture.AssertActiveAndDrainingPreserved();

        fixture.Events.Clear();
        DurableFacilityEquipmentSlotCheckpointGcResult second = fixture
            .Coordinator.OnDurableSaveCommitted("qa-slot", Digest0);
        Require(
            second.Status ==
                DurableFacilityEquipmentSlotCheckpointGcStatus.AlreadyApplied,
            "A second durable-equipment checkpoint collection was not idempotent.");
        Require(
            fixture.Events.SequenceEqual(new[]
            {
                "upper.prepare",
                "upper.publish",
                "upper.complete"
            }),
            "A second checkpoint collection revisited the already-collected child.");
    }

    private static void VerifyUpperPublishFailureRollsChildBack()
    {
        Fixture fixture = Fixture.Create(includeClosed: true);
        fixture.Upper.FailPublish = true;

        DurableFacilityEquipmentSlotCheckpointGcResult result = fixture
            .Coordinator.OnDurableSaveCommitted("qa-slot", Digest0);

        Require(
            result.Status ==
                DurableFacilityEquipmentSlotCheckpointGcStatus.Corruption,
            "An upper publication failure did not fail the durable checkpoint.");
        Require(
            fixture.Events.SequenceEqual(new[]
            {
                "upper.prepare",
                "child.prepare",
                "child.publish",
                "upper.publish",
                "child.rollback",
                "child.complete",
                "upper.rollback"
            }),
            "Upper publication failure did not roll the published child back before completion.");
        Require(
            fixture.State.Slots.Any(value => value.AssignmentSequence == 3L)
            && fixture.State.Children.Any(value =>
                FacilityBufferDestinationCustodyDrainProjection.AreExactEqual(
                    value,
                    fixture.ClosedChild)),
            "Upper publication failure lost the terminal upper or child.");
        fixture.AssertActiveAndDrainingPreserved();
    }

    private static void VerifyParticipantIdentityOrderAndStatusMapping()
    {
        foreach ((DurableFacilityEquipmentSlotCheckpointGcStatus domain,
                     DungeonDurableSaveCommitStatus expected) in new[]
                 {
                     (DurableFacilityEquipmentSlotCheckpointGcStatus.Applied,
                         DungeonDurableSaveCommitStatus.Applied),
                     (DurableFacilityEquipmentSlotCheckpointGcStatus
                             .AlreadyApplied,
                         DungeonDurableSaveCommitStatus.AlreadyApplied),
                     (DurableFacilityEquipmentSlotCheckpointGcStatus.Deferred,
                         DungeonDurableSaveCommitStatus.Deferred),
                     (DurableFacilityEquipmentSlotCheckpointGcStatus.Corruption,
                         DungeonDurableSaveCommitStatus.Corruption)
                 })
        {
            DurableFacilityEquipmentSlotCheckpointGcDurableSaveParticipant
                participant = new(new FixedCoordinator(domain));
            DungeonDurableSaveCommitResult result = participant
                .OnDurableSaveCommitted(new DungeonDurableSaveCommitContext(
                    "qa-slot",
                    Digest0));
            Require(
                participant.ParticipantId ==
                    DurableFacilityEquipmentSlotCheckpointGcDurableSaveParticipant
                        .Id
                && participant.Order == 320
                && result.ParticipantId == participant.ParticipantId
                && result.Status == expected,
                "Durable-equipment checkpoint participant identity, order, or status mapping drifted.");
        }
    }

    private static DurableFacilityEquipmentSlotSnapshot CreateSlot(
        string ownerSubjectId,
        long sequence,
        DurableFacilityEquipmentSlotLifecyclePhase phase,
        FacilityBufferDestinationCustodyDrainSnapshot drain = null)
    {
        DurableFacilityEquipmentRequirement requirement = new(
            "durable-index",
            (ItemDefinitionId)ItemId,
            1);
        DurableFacilityEquipmentAssignment assignment = new(
            new DurableFacilityEquipmentSlotKey(OwnerDomain, ownerSubjectId),
            PolicyId,
            policyRevision: 1L,
            DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
            DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent,
            (BuildingInstanceId)FacilityId,
            DropPosition,
            new[] { requirement });
        DurableFacilityEquipmentCapacityProjection projection = new(
            DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
            new PhysicalMassGrams(1300L),
            sourceAuthorityRevision: 1L,
            Digest0);
        bool terminal = phase ==
            DurableFacilityEquipmentSlotLifecyclePhase
                .ClosedAwaitingCheckpointGc;
        return new DurableFacilityEquipmentSlotSnapshot(
            assignment,
            sequence,
            DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                assignment.Key,
                sequence),
            DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                assignment.Key,
                sequence),
            DurableFacilityEquipmentFingerprint.CreateAssignment(assignment),
            projection,
            new[]
            {
                new DurableFacilityEquipmentRequirementStatus(
                    requirement,
                    pendingQuantity: 0,
                    bufferedUsableQuantity: 0)
            },
            phase,
            phase == DurableFacilityEquipmentSlotLifecyclePhase.Active
                ? string.Empty
                : "qa-close",
            drain,
            authoritiesRevoked: terminal);
    }

    private static FacilityBufferDestinationCustodyDrainSnapshot CreateDrain(
        string ownerSubjectId,
        long sequence,
        FacilityBufferDestinationCustodyDrainPhase phase)
    {
        DurableFacilityEquipmentSlotKey key = new(OwnerDomain, ownerSubjectId);
        return new FacilityBufferDestinationCustodyDrainSnapshot(
            DurableFacilityEquipmentSlotIdentity.BuildDrainParentOperationId(
                key,
                sequence),
            DurableFacilityEquipmentSlotIdentity.BuildDrainStepOperationId(
                key,
                sequence),
            DurableFacilityEquipmentSlotIdentity.BuildOwnerStableId(key, sequence),
            ownerSubjectId,
            FacilityId,
            DurableFacilityEquipmentSlotIdentity.BuildDestinationId(key, sequence),
            Digest0,
            Digest1,
            DropPosition.x,
            DropPosition.y,
            phase,
            sourceActorCount: 1,
            completedActorCount: PhaseAtLeast(
                phase,
                FacilityBufferDestinationCustodyDrainPhase
                    .ReleasingOperationAuthority) ? 1 : 0,
            sourceOperationCount: 1,
            releasedOperationCount: PhaseAtLeast(
                phase,
                FacilityBufferDestinationCustodyDrainPhase
                    .ReleasingDestination) ? 1 : 0,
            inputQuantity: 1,
            inputMassGrams: 1300L,
            releasedQuantity: PhaseAtLeast(
                phase,
                FacilityBufferDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck) ? 1 : 0,
            releasedMassGrams: PhaseAtLeast(
                phase,
                FacilityBufferDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck) ? 1300L : 0L,
            commitId: PhaseAtLeast(
                phase,
                FacilityBufferDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck)
                ? "commit:qa-durable-equipment-gc:" + sequence
                : string.Empty,
            receiptFingerprint: PhaseAtLeast(
                phase,
                FacilityBufferDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck)
                ? Digest1
                : string.Empty);
    }

    private static bool PhaseAtLeast(
        FacilityBufferDestinationCustodyDrainPhase value,
        FacilityBufferDestinationCustodyDrainPhase threshold) =>
        (int)value >= (int)threshold;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        private Fixture(SharedState state)
        {
            State = state;
            Events = new List<string>();
            Persistence = new FakePersistence(state);
            Upper = new FakeUpper(state, Events);
            Child = new FakeChild(state, Events);
            Coordinator = new DurableFacilityEquipmentSlotCheckpointGcCoordinator(
                Persistence,
                Upper,
                Child,
                Child);
            ClosedChild = state.Children.FirstOrDefault(value =>
                value.Phase == FacilityBufferDestinationCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc);
        }

        internal SharedState State { get; }
        internal List<string> Events { get; }
        internal FakePersistence Persistence { get; }
        internal FakeUpper Upper { get; }
        internal FakeChild Child { get; }
        internal DurableFacilityEquipmentSlotCheckpointGcCoordinator Coordinator
            { get; }
        internal FacilityBufferDestinationCustodyDrainSnapshot ClosedChild
            { get; }

        internal static Fixture Create(bool includeClosed)
        {
            FacilityBufferDestinationCustodyDrainSnapshot drainingChild =
                CreateDrain(
                    "slot:draining",
                    2L,
                    FacilityBufferDestinationCustodyDrainPhase.ReleasingActors);
            SharedState state = new();
            state.Slots.Add(CreateSlot(
                "slot:active",
                1L,
                DurableFacilityEquipmentSlotLifecyclePhase.Active));
            state.Slots.Add(CreateSlot(
                "slot:draining",
                2L,
                DurableFacilityEquipmentSlotLifecyclePhase.Draining,
                drainingChild));
            state.Children.Add(drainingChild);
            if (includeClosed)
            {
                FacilityBufferDestinationCustodyDrainSnapshot closedChild =
                    CreateDrain(
                        "slot:closed",
                        3L,
                        FacilityBufferDestinationCustodyDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc);
                state.Slots.Add(CreateSlot(
                    "slot:closed",
                    3L,
                    DurableFacilityEquipmentSlotLifecyclePhase
                        .ClosedAwaitingCheckpointGc,
                    closedChild));
                state.Children.Add(closedChild);
            }
            return new Fixture(state);
        }

        internal void AssertActiveAndDrainingPreserved()
        {
            Require(
                State.Slots.Any(value =>
                    value.AssignmentSequence == 1L
                    && value.LifecyclePhase ==
                    DurableFacilityEquipmentSlotLifecyclePhase.Active)
                && State.Slots.Any(value =>
                    value.AssignmentSequence == 2L
                    && value.LifecyclePhase ==
                    DurableFacilityEquipmentSlotLifecyclePhase.Draining)
                && State.Children.Any(value => string.Equals(
                    value.StepOperationId,
                    DurableFacilityEquipmentSlotIdentity
                        .BuildDrainStepOperationId(
                            new DurableFacilityEquipmentSlotKey(
                                OwnerDomain,
                                "slot:draining"),
                            2L),
                    StringComparison.Ordinal)),
                "Checkpoint GC removed an active or draining durable-equipment row.");
        }
    }

    private sealed class SharedState
    {
        internal List<DurableFacilityEquipmentSlotSnapshot> Slots { get; } = new();
        internal List<FacilityBufferDestinationCustodyDrainSnapshot> Children
            { get; } = new();
    }

    private sealed class FakePersistence : IDurableFacilityEquipmentSlotPersistence
    {
        private readonly SharedState state;

        internal FakePersistence(SharedState state)
        {
            this.state = state;
        }

        public DungeonDurableFacilityEquipmentSaveData CaptureSaveData()
        {
            long next = state.Slots.Count == 0
                ? 1L
                : state.Slots.Max(value => value.AssignmentSequence) + 1L;
            return new DungeonDurableFacilityEquipmentSaveData
            {
                nextAssignmentSequence = next,
                revision = 1L,
                slots = state.Slots
                    .OrderBy(value => value.AssignmentSequence)
                    .Select(DurableFacilityEquipmentRestoreProjection.Capture)
                    .ToList()
            };
        }

        public void PublishRestoreCandidate(
            DurableFacilityEquipmentRestoreCandidate candidate) =>
            throw new InvalidOperationException(
                "The checkpoint fixture must not publish a restore candidate.");
    }

    private sealed class FakeUpper : IDurableFacilityEquipmentSlotCheckpointGcPort
    {
        private readonly SharedState state;
        private readonly List<string> events;
        private Candidate active;

        internal FakeUpper(SharedState state, List<string> events)
        {
            this.state = state;
            this.events = events;
        }

        internal bool FailPublish { get; set; }

        public bool TryPrepareCheckpointGarbageCollection(
            out IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate,
            out string failureReason)
        {
            events.Add("upper.prepare");
            if (active != null)
            {
                candidate = null;
                failureReason = "qa-upper-already-active";
                return false;
            }
            active = new Candidate(state.Slots.Where(value =>
                    value.LifecyclePhase ==
                    DurableFacilityEquipmentSlotLifecyclePhase
                        .ClosedAwaitingCheckpointGc)
                .OrderBy(value => value.AssignmentSequence)
                .ToArray());
            candidate = active;
            failureReason = string.Empty;
            return true;
        }

        public bool TryPublishCheckpointGarbageCollection(
            IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate,
            out string failureReason)
        {
            events.Add("upper.publish");
            if (!ReferenceEquals(candidate, active))
            {
                failureReason = "qa-upper-candidate-invalid";
                return false;
            }
            if (FailPublish)
            {
                failureReason = "qa-upper-publish-failed";
                return false;
            }
            foreach (DurableFacilityEquipmentSlotSnapshot value in active.ClosedSlots)
                state.Slots.Remove(value);
            active.Published = true;
            failureReason = string.Empty;
            return true;
        }

        public void RollbackCheckpointGarbageCollection(
            IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate)
        {
            events.Add("upper.rollback");
            Require(ReferenceEquals(candidate, active),
                "The upper rollback received a foreign candidate.");
            if (active.Published)
            {
                state.Slots.AddRange(active.ClosedSlots);
                state.Slots.Sort((left, right) =>
                    left.AssignmentSequence.CompareTo(right.AssignmentSequence));
            }
            active = null;
        }

        public void CompleteCheckpointGarbageCollection(
            IDurableFacilityEquipmentSlotCheckpointGcCandidate candidate)
        {
            events.Add("upper.complete");
            Require(ReferenceEquals(candidate, active) && active.Published,
                "The upper completion received an unpublished candidate.");
            active = null;
        }

        private sealed class Candidate :
            IDurableFacilityEquipmentSlotCheckpointGcCandidate
        {
            internal Candidate(
                IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> closedSlots)
            {
                ClosedSlots = closedSlots;
            }

            public IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> ClosedSlots
                { get; }
            internal bool Published { get; set; }
        }
    }

    private sealed class FakeChild :
        IFacilityBufferDestinationCustodyDrainLiveQuery,
        IFacilityBufferDestinationCustodyDrainCheckpointGcPort
    {
        private readonly SharedState state;
        private readonly List<string> events;
        private Candidate active;

        internal FakeChild(SharedState state, List<string> events)
        {
            this.state = state;
            this.events = events;
        }

        public IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot> Drains =>
            state.Children.OrderBy(value => value.StepOperationId,
                StringComparer.Ordinal).ToArray();

        public bool TryPrepareCheckpointGarbageCollection(
            IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot> snapshots,
            out IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate,
            out string failureReason)
        {
            events.Add("child.prepare");
            FacilityBufferDestinationCustodyDrainSnapshot[] requested =
                (snapshots ?? Array.Empty<
                    FacilityBufferDestinationCustodyDrainSnapshot>()).ToArray();
            bool exact = active == null
                && requested.All(expected => state.Children.Any(value =>
                    FacilityBufferDestinationCustodyDrainProjection.AreExactEqual(
                        expected,
                        value)));
            if (!exact)
            {
                candidate = null;
                failureReason = "qa-child-prepare-invalid";
                return false;
            }
            active = new Candidate(requested);
            candidate = active;
            failureReason = string.Empty;
            return true;
        }

        public bool TryPublishCheckpointGarbageCollection(
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate,
            out string failureReason)
        {
            events.Add("child.publish");
            if (!ReferenceEquals(candidate, active))
            {
                failureReason = "qa-child-candidate-invalid";
                return false;
            }
            foreach (FacilityBufferDestinationCustodyDrainSnapshot value in
                     active.Snapshots)
            {
                state.Children.Remove(value);
            }
            active.Published = true;
            failureReason = string.Empty;
            return true;
        }

        public void RollbackCheckpointGarbageCollection(
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate)
        {
            events.Add("child.rollback");
            Require(ReferenceEquals(candidate, active),
                "The child rollback received a foreign candidate.");
            if (active.Published)
            {
                state.Children.AddRange(active.Snapshots);
                state.Children.Sort((left, right) => string.CompareOrdinal(
                    left.StepOperationId,
                    right.StepOperationId));
                active.Published = false;
            }
        }

        public void CompleteCheckpointGarbageCollection(
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate)
        {
            events.Add("child.complete");
            Require(ReferenceEquals(candidate, active),
                "The child completion received a foreign candidate.");
            active = null;
        }

        private sealed class Candidate :
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
        {
            internal Candidate(
                IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot>
                    snapshots)
            {
                Snapshots = snapshots;
            }

            internal IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot>
                Snapshots { get; }
            internal bool Published { get; set; }
        }
    }

    private sealed class FixedCoordinator :
        IDurableFacilityEquipmentSlotCheckpointGcCoordinator
    {
        private readonly DurableFacilityEquipmentSlotCheckpointGcStatus status;

        internal FixedCoordinator(
            DurableFacilityEquipmentSlotCheckpointGcStatus status)
        {
            this.status = status;
        }

        public DurableFacilityEquipmentSlotCheckpointGcResult
            OnDurableSaveCommitted(
                string slotId,
                string serializedByteDigest) => new(status, "qa-status");
    }
}
#endif
