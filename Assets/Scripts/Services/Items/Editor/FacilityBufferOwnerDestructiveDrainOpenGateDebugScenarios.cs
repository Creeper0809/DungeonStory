#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class FacilityBufferOwnerDestructiveDrainOpenGateDebugScenarios
{
    private const string CapacitySourceDigest =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [MenuItem(
        "DungeonStory/Debug/Items/Run Facility Buffer Owner Destructive Drain Open Gate Contracts")]
    public static void RunAll()
    {
        VerifyAll();
        Debug.Log(
            "Facility-buffer owner destructive-drain open-gate contracts passed.");
    }

    internal static void VerifyAll()
    {
        DungeonRuntimeAggregateRootStore roots = new();
        ProductionFacilityDestructiveDrainJournal journal = new(roots);
        ProductionFacilityDestructiveDrainOpenOperationQuery openOperations =
            new(roots);
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa:facility-buffer-drain-gate";
        string destinationId = ProductionOutputDestinationId
            .FromFacility(facilityId)
            .Value;
        Vector2Int dropPosition = new(23, 17);

        FacilityBufferDestinationClaimRegistry claims = new();
        FakeOccupancyQuery occupancy = new();
        FakeMassQuery mass = new();
        FacilityBufferMassAdmissionService admission = new(
            claims,
            occupancy,
            mass,
            openOperations);
        FacilityBufferDestinationClaim claim = new(
            destinationId,
            dropPosition,
            ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
            destinationId,
            facilityId.Value,
            FacilityBufferDestinationAnchorKind.LiveFacility);
        FacilityBufferCapacityProfile profile = new(
            destinationId,
            dropPosition,
            claim.OwnerDomain,
            claim.OwnerOperationId,
            claim.OwnerFacilityId,
            new PhysicalMassGrams(12_000L),
            ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision);
        Require(
            claims.TryClaim(claim, out _, out _)
            && admission.TryReplaceOwnedProfiles(
                claim.OwnerDomain,
                new[] { profile },
                out _,
                out _),
            "Facility-buffer drain-gate fixture could not publish authority.");

        FacilityBufferMassAdmissionRequest existingExact = CreateExactRequest(
            "qa:facility-buffer-drain-gate:existing-exact",
            profile,
            "stack:qa:facility-buffer-drain-gate:existing-exact");
        FacilityBufferPlannedOutputRequest existingPlanned =
            CreatePlannedRequest(
                "qa:facility-buffer-drain-gate:existing-planned",
                "production-output-batch:qa:drain-gate:existing",
                profile,
                "output:existing");
        Require(
            admission.TryReserveExactLot(
                existingExact,
                out FacilityBufferMassAdmissionToken existingExactToken,
                out _,
                out _),
            "Fixture could not establish the pre-journal exact token.");
        Require(
            admission.TryReservePlannedOutput(
                existingPlanned,
                out FacilityBufferPlannedOutputToken existingPlannedToken,
                out _,
                out _),
            "Fixture could not establish the pre-journal planned token.");
        Require(
            string.Equals(
                existingExactToken.TokenId,
                "facility-buffer-admission:000000000001",
                StringComparison.Ordinal)
            && string.Equals(
                existingPlannedToken.TokenId,
                "facility-buffer-planned-output-admission:000000000002",
                StringComparison.Ordinal),
            "Fixture could not establish the pre-journal token state.");

        FacilityBufferMassAdmissionRequest blockedExact = CreateExactRequest(
            "qa:facility-buffer-drain-gate:blocked-exact",
            profile,
            "stack:qa:facility-buffer-drain-gate:blocked-exact");
        FacilityBufferPlannedOutputRequest blockedPlanned =
            CreatePlannedRequest(
                "qa:facility-buffer-drain-gate:blocked-planned",
                "production-output-batch:qa:drain-gate:blocked",
                profile,
                "output:blocked");
        long revisionBeforeOpen = admission.Revision;
        Require(
            admission.TryGetCapacity(
                destinationId,
                dropPosition,
                out FacilityBufferMassCapacitySnapshot beforeOpen)
            && beforeOpen.ReservedMassGrams == 2_000L,
            "Fixture pre-journal reserved mass was not exact.");

        ProductionFacilityDestructiveDrainOperationId operationId =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(
                facilityId);
        string lifecycleFingerprint =
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:facility-buffer-drain-gate:prepared");
        Require(
            journal.TryRequest(
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                facilityId,
                ProductionFacilityDestructiveDrainCanonical
                    .BuildInitiatingMutationOperationId(
                        ProductionFacilityDestructiveDrainCause
                            .ExplicitDemolition,
                        facilityId),
                lifecycleFingerprint,
                Array.Empty<ProductionFacilityDestructiveDrainParticipantSaveData>(),
                out ProductionFacilityDestructiveDrainEntrySaveData entry,
                out string requestFailure)
            && openOperations.TryCapture(facilityId, out var openSnapshot)
            && openSnapshot.OperationId.Equals(operationId),
            "Root-store destructive journal did not open: " + requestFailure);

        Require(
            !admission.TryReserveExactLot(
                blockedExact,
                out FacilityBufferMassAdmissionToken blockedExactToken,
                out FacilityBufferMassAdmissionFailureCode exactFailure,
                out string exactFailureReason)
            && exactFailure
                == FacilityBufferMassAdmissionFailureCode
                    .OwnerDestructiveDrainOpen
            && string.IsNullOrEmpty(blockedExactToken.TokenId)
            && exactFailureReason.Contains(
                operationId.Value,
                StringComparison.Ordinal),
            "An exact-lot reservation crossed the owner destructive-drain gate.");
        Require(
            !admission.TryReservePlannedOutput(
                blockedPlanned,
                out FacilityBufferPlannedOutputToken blockedPlannedToken,
                out FacilityBufferMassAdmissionFailureCode plannedFailure,
                out string plannedFailureReason)
            && plannedFailure
                == FacilityBufferMassAdmissionFailureCode
                    .OwnerDestructiveDrainOpen
            && string.IsNullOrEmpty(blockedPlannedToken.TokenId)
            && plannedFailureReason.Contains(
                operationId.Value,
                StringComparison.Ordinal),
            "A planned-output reservation crossed the owner destructive-drain gate.");
        Require(
            admission.Revision == revisionBeforeOpen
            && admission.TryGetCapacity(
                destinationId,
                dropPosition,
                out FacilityBufferMassCapacitySnapshot whileOpen)
            && whileOpen.ReservedMassGrams == beforeOpen.ReservedMassGrams
            && !admission.TryGetReceipt(existingExactToken.TokenId, out _)
            && admission.TryValidatePlannedOutputReservation(
                existingPlannedToken,
                out _,
                out _),
            "Rejected reservations mutated profile revision, reserved mass, or existing tokens.");

        entry = AdvanceToCheckpointGc(
            journal,
            operationId,
            entry,
            lifecycleFingerprint);
        Require(
            journal.TryRemoveCheckpointed(
                operationId,
                entry.revision,
                out string removeFailure)
            && journal.CaptureOpen().Count == 0
            && !openOperations.IsOpen(facilityId),
            "Journal-last removal did not release the owner gate: "
            + removeFailure);

        Require(
            admission.TryReserveExactLot(
                blockedExact,
                out FacilityBufferMassAdmissionToken admittedExact,
                out _,
                out _),
            "The exact-lot request remained fenced after journal-last removal.");
        Require(
            admission.TryReservePlannedOutput(
                blockedPlanned,
                out FacilityBufferPlannedOutputToken admittedPlanned,
                out _,
                out _),
            "The planned-output request remained fenced after journal-last removal.");
        Require(
            string.Equals(
                admittedExact.TokenId,
                "facility-buffer-admission:000000000003",
                StringComparison.Ordinal)
            && string.Equals(
                admittedPlanned.TokenId,
                "facility-buffer-planned-output-admission:000000000004",
                StringComparison.Ordinal)
            && admission.Revision == revisionBeforeOpen
            && admission.TryGetCapacity(
                destinationId,
                dropPosition,
                out FacilityBufferMassCapacitySnapshot afterRemoval)
            && afterRemoval.ReservedMassGrams == 4_000L,
            "The exact requests were not admitted unchanged after journal-last removal, or a rejected request consumed token sequence state.");

        Require(
            admission.TryRelease(
                existingExactToken,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _)
            && admission.TryReleasePlannedOutput(
                existingPlannedToken,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _)
            && admission.TryRelease(
                admittedExact,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _)
            && admission.TryReleasePlannedOutput(
                admittedPlanned,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _)
            && admission.TryGetCapacity(
                destinationId,
                dropPosition,
                out FacilityBufferMassCapacitySnapshot released)
            && released.ReservedMassGrams == 0L,
            "Fixture cleanup could not prove all four reservation tokens remained exact.");

    }

    private static ProductionFacilityDestructiveDrainEntrySaveData
        AdvanceToCheckpointGc(
            ProductionFacilityDestructiveDrainJournal journal,
            ProductionFacilityDestructiveDrainOperationId operationId,
            ProductionFacilityDestructiveDrainEntrySaveData current,
            string lifecycleFingerprint)
    {
        ProductionFacilityDestructiveDrainPhase[] phases =
        {
            ProductionFacilityDestructiveDrainPhase.DrainingParticipants,
            ProductionFacilityDestructiveDrainPhase.AwaitingEmptyVerification,
            ProductionFacilityDestructiveDrainPhase.AwaitingAuthorityRevoke,
            ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval,
            ProductionFacilityDestructiveDrainPhase
                .WorldRemovedAwaitingCheckpointGc
        };
        foreach (ProductionFacilityDestructiveDrainPhase phase in phases)
        {
            Require(
                journal.TryAdvance(
                    operationId,
                    current.revision,
                    phase,
                    lifecycleFingerprint,
                    Array.Empty<ProductionFacilityDestructiveDrainParticipantSaveData>(),
                    out ProductionFacilityDestructiveDrainEntrySaveData next,
                    out string failureReason),
                "Could not advance destructive journal to " + phase + ": "
                + failureReason);
            current = next;
        }
        return current;
    }

    private static FacilityBufferMassAdmissionRequest CreateExactRequest(
        string operationId,
        FacilityBufferCapacityProfile profile,
        string stackId) => new(
        operationId,
        profile.DestinationId,
        profile.DropPosition,
        profile.OwnerDomain,
        profile.OwnerOperationId,
        profile.OwnerFacilityId,
        profile.CapacityRevision,
        new[]
        {
            new FacilityBufferMassLotSlice(
                stackId,
                quantity: 1,
                expectedReservationRevision: 1L)
        });

    private static FacilityBufferPlannedOutputRequest CreatePlannedRequest(
        string operationId,
        string batchCommitId,
        FacilityBufferCapacityProfile profile,
        string outputLineId) => new(
        operationId,
        batchCommitId,
        "outcome:qa:facility-buffer-drain-gate",
        profile.DestinationId,
        profile.DropPosition,
        profile.OwnerDomain,
        profile.OwnerOperationId,
        profile.OwnerFacilityId,
        profile.CapacityRevision,
        new[]
        {
            new FacilityBufferPlannedOutputSlice(
                outputLineId,
                PhysicalItemMassSubject.ForDefinition(
                    (ItemDefinitionId)"qa:item:facility-buffer-drain-gate"),
                quantity: 1)
        },
        CapacitySourceDigest,
        expectedMinimumCapacityGrams: 1L);

    private sealed class FakeOccupancyQuery :
        IFacilityBufferPhysicalOccupancyQuery
    {
        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(
            nonCarriedMassGrams: 0L,
            committedCarriedMassGrams: 0L);

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            failureReason = string.Empty;
            int quantity = 0;
            foreach (FacilityBufferMassLotSlice slice in slices)
                quantity = checked(quantity + slice.Quantity);
            lot = new FacilityBufferExactLotSnapshot(
                "qa:facility-buffer-drain-gate:lot:" + quantity,
                new PhysicalMassGrams(checked(quantity * 1_000L)));
            return quantity > 0;
        }
    }

    private sealed class FakeMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 1L;

        public PhysicalMassGrams GetDefinitionUnitMass(
            ItemDefinitionId itemId)
        {
            if (!itemId.IsValid)
                throw new InvalidOperationException("qa-mass-item-missing");
            return new PhysicalMassGrams(1_000L);
        }

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject)
        {
            if (subject == null)
                throw new ArgumentNullException(nameof(subject));
            return subject.HasPreparedUnitMass
                ? subject.PreparedUnitMass
                : GetDefinitionUnitMass(subject.ItemId);
        }

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject)
        {
            if (subject == null || !itemId.Equals(subject.ItemId))
                throw new InvalidOperationException("qa-mass-subject-mismatch");
            return GetPreparedStackUnitMass(subject);
        }

        public PhysicalMassGrams GetStackTotalMass(
            PhysicalItemLotSnapshot lot) => GetQuantityMass(
            lot.Subject.ItemId,
            lot.Subject,
            lot.Quantity);

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => GetStackUnitMass(itemId, subject)
            .Multiply(quantity);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
