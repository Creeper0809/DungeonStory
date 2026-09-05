using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SurgeryMaterialTerminalCheckpointGcDebugScenarios
{
    private const string Digest =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Surgery Terminal Checkpoint GC")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log(
            "[SurgeryMaterialTerminalCheckpointGcDebugScenarios] PASS");
    }

    public static void RunAll()
    {
        VerifySuccessfulCollectionPreservesHistory();
        VerifyNoEligibleIsIdempotent();
        VerifyChildPublishFailurePreservesUpper();
        VerifyUpperDriftRollsBackChild();
        VerifyBidirectionalOrphansFailClosed();
    }

    private static void VerifySuccessfulCollectionPreservesHistory()
    {
        Fixture fixture = new(withClosedOwner: true, withChild: true);
        SurgeryOrder live = fixture.State.Orders.Single();
        SurgeryMaterialTerminalCheckpointGcResult result = fixture.Coordinator
            .OnDurableSaveCommitted("slot:test", Digest);

        Require(result.Status ==
                SurgeryMaterialTerminalCheckpointGcStatus.Applied,
            "Closed Surgery terminal receipt was not collected.");
        Require(live.state == SurgeryOrderState.Cancelled
            && live.materialTerminalDrainPhase ==
                SurgeryMaterialTerminalDrainPhase.None
            && live.materialTerminalTargetState ==
                SurgeryOrderState.PatientWaiting
            && string.IsNullOrEmpty(live.materialTerminalStepOperationId)
            && fixture.Children.Drains.Count == 0,
            "Checkpoint GC did not preserve Surgery history while clearing both join authorities.");
    }

    private static void VerifyNoEligibleIsIdempotent()
    {
        Fixture fixture = new(withClosedOwner: false, withChild: false);
        SurgeryMaterialTerminalCheckpointGcResult first = fixture.Coordinator
            .OnDurableSaveCommitted("slot:test", Digest);
        SurgeryMaterialTerminalCheckpointGcResult second = fixture.Coordinator
            .OnDurableSaveCommitted("slot:test", Digest);
        Require(first.Status ==
                SurgeryMaterialTerminalCheckpointGcStatus.AlreadyApplied
            && second.Status ==
                SurgeryMaterialTerminalCheckpointGcStatus.AlreadyApplied,
            "An empty Surgery terminal checkpoint was not idempotent.");
    }

    private static void VerifyChildPublishFailurePreservesUpper()
    {
        Fixture fixture = new(withClosedOwner: true, withChild: true);
        fixture.Children.FailPublish = true;
        SurgeryOrder before = SurgeryStateCloner.CloneOrder(
            fixture.State.Orders.Single());
        SurgeryMaterialTerminalCheckpointGcResult result = fixture.Coordinator
            .OnDurableSaveCommitted("slot:test", Digest);

        Require(result.Status ==
                SurgeryMaterialTerminalCheckpointGcStatus.Corruption
            && fixture.State.Orders.Single().materialTerminalDrainPhase ==
                before.materialTerminalDrainPhase
            && fixture.State.Orders.Single().materialTerminalStepOperationId ==
                before.materialTerminalStepOperationId
            && fixture.Children.Drains.Count == 1
            && !fixture.Children.HasActiveCandidate,
            "Child publish failure leaked a partial Surgery checkpoint collection.");
    }

    private static void VerifyUpperDriftRollsBackChild()
    {
        Fixture fixture = new(withClosedOwner: true, withChild: true);
        fixture.Children.AfterPublish = () =>
            fixture.State.Orders.Single().completedWork += 1f;
        SurgeryMaterialTerminalCheckpointGcResult result = fixture.Coordinator
            .OnDurableSaveCommitted("slot:test", Digest);

        Require(result.Status ==
                SurgeryMaterialTerminalCheckpointGcStatus.Corruption
            && fixture.Children.Drains.Count == 1
            && fixture.State.Orders.Single().materialTerminalDrainPhase ==
                SurgeryMaterialTerminalDrainPhase.ClosedAwaitingCheckpointGc
            && !fixture.Children.HasActiveCandidate,
            "Upper drift did not rollback the exact Items child tombstone.");
    }

    private static void VerifyBidirectionalOrphansFailClosed()
    {
        Fixture upperOnly = new(withClosedOwner: true, withChild: false);
        Require(upperOnly.Coordinator.OnDurableSaveCommitted(
                    "slot:test",
                    Digest).Status ==
                SurgeryMaterialTerminalCheckpointGcStatus.Corruption,
            "An upper-only Surgery terminal receipt was collected.");

        Fixture lowerOnly = new(withClosedOwner: false, withChild: true);
        Require(lowerOnly.Coordinator.OnDurableSaveCommitted(
                    "slot:test",
                    Digest).Status ==
                SurgeryMaterialTerminalCheckpointGcStatus.Corruption,
            "A lower-only Surgery terminal receipt was collected.");
    }

    private sealed class Fixture
    {
        internal Fixture(bool withClosedOwner, bool withChild)
        {
            DungeonRuntimeAggregateRootStore roots = new();
            SurgeryAggregateStateStore store = new(roots);
            State = store.State;
            SurgeryOrder owner = CreateOwner(withClosedOwner);
            State.Orders.Add(owner);
            Children = new FakeChildAuthority();
            if (withChild)
                Children.Add(CreateChild(owner));
            Coordinator = new SurgeryMaterialTerminalCheckpointGcCoordinator(
                store,
                Children,
                Children);
        }

        internal SurgeryAggregateState State { get; }
        internal FakeChildAuthority Children { get; }
        internal SurgeryMaterialTerminalCheckpointGcCoordinator Coordinator
        { get; }
    }

    private static SurgeryOrder CreateOwner(bool closed) => new()
    {
        orderId = "surgery:checkpoint-gc-fixture",
        facilityId = "facility:surgery-fixture",
        materialDestinationId =
            "surgery-materials:surgery:checkpoint-gc-fixture",
        materialCapacityFingerprint = Digest,
        materialTerminalDrainPhase = closed
            ? SurgeryMaterialTerminalDrainPhase.ClosedAwaitingCheckpointGc
            : SurgeryMaterialTerminalDrainPhase.None,
        materialTerminalTargetState = closed
            ? SurgeryOrderState.Cancelled
            : SurgeryOrderState.PatientWaiting,
        materialTerminalParentOperationId = closed
            ? SurgeryMaterialTerminalIdentity.FormatParentOperationId(
                "surgery:checkpoint-gc-fixture")
            : string.Empty,
        materialTerminalStepOperationId = closed
            ? SurgeryMaterialTerminalIdentity.FormatStepOperationId(
                "surgery:checkpoint-gc-fixture")
            : string.Empty,
        materialTerminalRequestFingerprint = closed ? Digest : string.Empty,
        materialTerminalCommitId = closed
            ? "commit:surgery-checkpoint-gc-fixture"
            : string.Empty,
        materialTerminalReceiptFingerprint = closed ? Digest : string.Empty,
        materialTerminalInputQuantity = closed ? 2 : 0,
        materialTerminalInputMassGrams = closed ? 800L : 0L,
        materialTerminalOwnerX = closed ? 4 : 0,
        materialTerminalOwnerY = closed ? 5 : 0,
        state = SurgeryOrderState.Cancelled,
        statusData = new SurgeryStatusData()
    };

    private static FacilityBufferDestinationCustodyDrainSnapshot CreateChild(
        SurgeryOrder owner)
    {
        bool closed = owner.materialTerminalDrainPhase ==
            SurgeryMaterialTerminalDrainPhase.ClosedAwaitingCheckpointGc;
        string orderId = owner.orderId;
        return new FacilityBufferDestinationCustodyDrainSnapshot(
            SurgeryMaterialTerminalIdentity.FormatParentOperationId(orderId),
            SurgeryMaterialTerminalIdentity.FormatStepOperationId(orderId),
            SurgeryMaterialTerminalIdentity.FormatOwnerStableId(orderId),
            orderId,
            owner.facilityId,
            owner.materialDestinationId,
            owner.materialCapacityFingerprint,
            closed ? owner.materialTerminalRequestFingerprint : Digest,
            closed ? owner.materialTerminalOwnerX : 4,
            closed ? owner.materialTerminalOwnerY : 5,
            FacilityBufferDestinationCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc,
            0,
            0,
            0,
            0,
            closed ? owner.materialTerminalInputQuantity : 2,
            closed ? owner.materialTerminalInputMassGrams : 800L,
            closed ? owner.materialTerminalInputQuantity : 2,
            closed ? owner.materialTerminalInputMassGrams : 800L,
            closed
                ? owner.materialTerminalCommitId
                : "commit:surgery-checkpoint-gc-fixture",
            closed ? owner.materialTerminalReceiptFingerprint : Digest);
    }

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
                ?? Array.Empty<
                    FacilityBufferDestinationCustodyDrainSnapshot>());
            candidate = active;
            return true;
        }

        public bool TryPublishCheckpointGarbageCollection(
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate
                candidate,
            out string failureReason)
        {
            Candidate exact = Require(candidate);
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
                live.RemoveAll(value => value.StepOperationId ==
                    expected.StepOperationId);
            exact.Published = true;
            AfterPublish?.Invoke();
            failureReason = string.Empty;
            return true;
        }

        public void RollbackCheckpointGarbageCollection(
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate)
        {
            Candidate exact = Require(candidate);
            if (!exact.Published)
                return;
            live.AddRange(exact.Expected);
            exact.Published = false;
        }

        public void CompleteCheckpointGarbageCollection(
            IFacilityBufferDestinationCustodyDrainCheckpointGcCandidate candidate)
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

            internal IReadOnlyList<
                FacilityBufferDestinationCustodyDrainSnapshot> Expected
            { get; }
            internal bool Published { get; set; }
            internal bool Completed { get; set; }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
