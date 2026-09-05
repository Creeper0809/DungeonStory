using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class
    ProductionFacilityDestructiveDrainOpenOperationGateDebugScenarios
{
    [MenuItem(
        "DungeonStory/Debug/Economy/Run Destructive Drain Durable Open Gate Contracts")]
    public static void RunAll()
    {
        DungeonRuntimeAggregateRootStore roots = new();
        ProductionFacilityDestructiveDrainJournal journal = new(roots);
        ProductionFacilityDestructiveDrainOpenOperationQuery open = new(roots);
        ProductionFacilityMutationEpochRuntime transient = new();
        ProductionFacilityMutationAuthorityGate gate = new(transient, open);
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa-destructive-open-gate";
        string mutationId = ProductionFacilityDestructiveDrainCanonical
            .BuildInitiatingMutationOperationId(
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                facilityId);
        string fingerprint = new('a', 64);

        Require(!open.IsOpen(facilityId), "Fresh journal was unexpectedly open.");
        long reopenedEpoch = 0L;
        string reopenFailure = string.Empty;
        Require(
            gate.TryBegin(
                facilityId,
                mutationId,
                out long epoch,
                out string beginFailure)
            && string.IsNullOrEmpty(beginFailure)
            && gate.IsFrozen(facilityId),
            "Transient mutation epoch did not begin.");
        Require(
            gate.TryCaptureOpen(
                facilityId,
                out ProductionFacilityMutationFenceSnapshot transientSnapshot)
            && transientSnapshot.FacilityId.Equals(facilityId)
            && string.Equals(
                transientSnapshot.OperationId,
                mutationId,
                StringComparison.Ordinal)
            && transientSnapshot.OperationRevision == epoch
            && transientSnapshot.Kind ==
                ProductionFacilityMutationFenceKind.TransientTopology,
            "Combined mutation gate did not expose the exact transient operation.");
        Require(
            !ProductionFacilityMutationWorkPolicy.TryRequireMutable(
                gate,
                facilityId,
                out DomainFailure transientWorkFailure)
            && transientWorkFailure.Code
                == FailureCode.ProductionBillUnavailable
            && transientWorkFailure.Parameters.Length == 2
            && string.Equals(
                transientWorkFailure.Parameters[1],
                "production-facility-mutation-open:transient-topology:"
                + mutationId + ":" + epoch,
                StringComparison.Ordinal),
            "Shared production work policy did not expose the exact transient fence.");

        Require(
            journal.TryRequest(
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                facilityId,
                mutationId,
                fingerprint,
                Array.Empty<
                    ProductionFacilityDestructiveDrainParticipantSaveData>(),
                out ProductionFacilityDestructiveDrainEntrySaveData entry,
                out string requestFailure)
            && string.IsNullOrEmpty(requestFailure),
            "Durable destructive-drain journal request failed.");
        Require(
            open.TryCapture(
                facilityId,
                out ProductionFacilityDestructiveDrainOpenOperationSnapshot
                    snapshot)
            && snapshot.Phase ==
                ProductionFacilityDestructiveDrainPhase.Prepared
            && snapshot.Revision == entry.revision,
            "Root-store-only query did not expose the prepared operation.");
        Require(
            gate.TryCaptureOpen(
                facilityId,
                out ProductionFacilityMutationFenceSnapshot overlapSnapshot)
            && overlapSnapshot.Kind ==
                ProductionFacilityMutationFenceKind.DurableDestructiveDrain
            && string.Equals(
                overlapSnapshot.OperationId,
                snapshot.OperationId.Value,
                StringComparison.Ordinal)
            && overlapSnapshot.OperationRevision == snapshot.Revision,
            "Combined mutation gate did not prefer the durable operation while transient and durable fences overlapped.");

        Require(
            gate.TryEnd(
                facilityId,
                mutationId,
                epoch,
                out string endFailure)
            && string.IsNullOrEmpty(endFailure)
            && gate.IsFrozen(facilityId),
            "Durable gate opened when the transient epoch ended.");
        Require(
            gate.TryCaptureOpen(
                facilityId,
                out ProductionFacilityMutationFenceSnapshot durableSnapshot)
            && durableSnapshot.Kind ==
                ProductionFacilityMutationFenceKind.DurableDestructiveDrain
            && durableSnapshot.OperationRevision == entry.revision,
            "Combined mutation gate lost the exact durable operation snapshot.");
        Require(
            !ProductionFacilityMutationWorkPolicy.TryRequireMutable(
                gate,
                facilityId,
                out DomainFailure durableWorkFailure)
            && durableWorkFailure.Parameters.Length == 2
            && string.Equals(
                durableWorkFailure.Parameters[1],
                "production-facility-mutation-open:durable-destructive-drain:"
                + durableSnapshot.OperationId + ":"
                + durableSnapshot.OperationRevision,
                StringComparison.Ordinal),
            "Shared production work policy did not expose the exact durable fence.");
        Require(
            !gate.TryBegin(
                facilityId,
                mutationId,
                out _,
                out string blockedFailure)
            && blockedFailure.StartsWith(
                "production-facility-mutation-durable-drain-open:",
                StringComparison.Ordinal),
            "A second public mutation entered while the journal was open.");

        while (entry.phase < ProductionFacilityDestructiveDrainPhase
                   .WorldRemovedAwaitingCheckpointGc)
        {
            Require(
                journal.TryAdvance(
                    ProductionFacilityDestructiveDrainOperationId.FromFacility(
                        facilityId),
                    entry.revision,
                    (ProductionFacilityDestructiveDrainPhase)
                        ((int)entry.phase + 1),
                    fingerprint,
                    entry.participants
                        ?? new List<
                            ProductionFacilityDestructiveDrainParticipantSaveData>(),
                    out entry,
                    out string advanceFailure)
                && string.IsNullOrEmpty(advanceFailure)
                && open.IsOpen(facilityId),
                "Durable gate opened before journal-last checkpoint GC.");
        }

        Require(
            journal.TryRemoveCheckpointed(
                ProductionFacilityDestructiveDrainOperationId.FromFacility(
                    facilityId),
                entry.revision,
                out string removeFailure)
            && string.IsNullOrEmpty(removeFailure)
            && !open.IsOpen(facilityId)
            && gate.TryBegin(
                facilityId,
                mutationId,
                out reopenedEpoch,
                out reopenFailure)
            && reopenedEpoch > 0L
            && string.IsNullOrEmpty(reopenFailure),
            "Durable gate did not open after journal-last checkpoint GC.");
        Require(
            gate.TryEnd(
                facilityId,
                mutationId,
                reopenedEpoch,
                out string finalEndFailure)
            && string.IsNullOrEmpty(finalEndFailure)
            && !gate.TryCaptureOpen(facilityId, out _)
            && !gate.TryCaptureOpen(
                (BuildingInstanceId)"building:qa-unrelated-mutation-gate",
                out _)
            && ProductionFacilityMutationWorkPolicy.TryRequireMutable(
                gate,
                facilityId,
                out DomainFailure closedWorkFailure)
            && !closedWorkFailure.IsFailure,
            "Combined mutation snapshot remained visible after all fences ended or leaked to an unrelated facility.");

        Debug.Log("Destructive-drain durable open gate contracts passed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
