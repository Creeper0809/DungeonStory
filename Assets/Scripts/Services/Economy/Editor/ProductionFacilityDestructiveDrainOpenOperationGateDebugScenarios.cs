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
            gate.TryEnd(
                facilityId,
                mutationId,
                epoch,
                out string endFailure)
            && string.IsNullOrEmpty(endFailure)
            && gate.IsFrozen(facilityId),
            "Durable gate opened when the transient epoch ended.");
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
                out long reopenedEpoch,
                out string reopenFailure)
            && reopenedEpoch > 0L
            && string.IsNullOrEmpty(reopenFailure),
            "Durable gate did not open after journal-last checkpoint GC.");

        Debug.Log("Destructive-drain durable open gate contracts passed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
