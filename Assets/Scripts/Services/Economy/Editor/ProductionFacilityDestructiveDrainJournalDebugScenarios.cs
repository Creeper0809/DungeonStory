using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProductionFacilityDestructiveDrainJournalDebugScenarios
{
    [MenuItem(
        "DungeonStory/Debug/Economy/Run Production Facility Destructive Drain Journal Contracts")]
    public static void RunAll()
    {
        DungeonRuntimeAggregateRootStore sourceRoots = new();
        ProductionFacilityDestructiveDrainParticipantRegistry registry =
            ProductionFacilityDestructiveDrainParticipantRegistryDebugScenarios
                .CreateRegistry();
        ProductionFacilityDestructiveDrainJournal source = new(
            sourceRoots,
            registry);
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa-durable-destructive-drain";
        ProductionFacilityDestructiveDrainOperationId operationId =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(
                facilityId);
        string lifecycle = ProductionFacilityDestructiveDrainCanonical
            .ComputeFingerprint("qa:lifecycle:prepared");
        const string ownerId = "production-bill:qa-durable-drain";
        List<ProductionFacilityDestructiveDrainParticipantSaveData>
            participants =
                ProductionFacilityDestructiveDrainParticipantRegistryDebugScenarios
                    .CreateSaveParticipants(operationId, ownerId);
        string initiating = ProductionFacilityDestructiveDrainCanonical
            .BuildInitiatingMutationOperationId(
                ProductionFacilityDestructiveDrainCause.StructuralIntegrity,
                facilityId);

        Require(
            source.TryRequest(
                ProductionFacilityDestructiveDrainCause.StructuralIntegrity,
                facilityId,
                initiating,
                lifecycle,
                participants,
                out ProductionFacilityDestructiveDrainEntrySaveData prepared,
                out string requestFailure),
            "durable drain request failed: " + requestFailure);
        int requestedVersion = source.Version;
        Require(
            prepared.phase == ProductionFacilityDestructiveDrainPhase.Prepared
                && prepared.revision == 1L
                && source.TryRequest(
                    ProductionFacilityDestructiveDrainCause.StructuralIntegrity,
                    facilityId,
                    initiating,
                    lifecycle,
                    participants,
                    out ProductionFacilityDestructiveDrainEntrySaveData replay,
                    out _)
                && replay.revision == prepared.revision
                && source.Version == requestedVersion,
            "replayed durable drain request was not an exact no-op");
        Require(
            !source.TryRequest(
                ProductionFacilityDestructiveDrainCause.CombatCover,
                facilityId,
                ProductionFacilityDestructiveDrainCanonical
                    .BuildInitiatingMutationOperationId(
                        ProductionFacilityDestructiveDrainCause.CombatCover,
                        facilityId),
                lifecycle,
                participants,
                out _,
                out string conflictFailure)
                && conflictFailure.EndsWith(
                    "operation-conflict",
                    StringComparison.Ordinal),
            "a second cause replaced the canonical per-facility drain operation");

        List<ProductionFacilityDestructiveDrainParticipantSaveData>
            driftedPlan = CloneParticipants(participants);
        FindGeneric(driftedPlan).planFingerprint =
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:plan:drifted");
        Require(
            !source.TryRequest(
                ProductionFacilityDestructiveDrainCause.StructuralIntegrity,
                facilityId,
                initiating,
                lifecycle,
                driftedPlan,
                out _,
                out string replayPlanFailure)
                && replayPlanFailure.EndsWith(
                    "operation-conflict",
                    StringComparison.Ordinal),
            "replayed destructive-drain request silently replaced its immutable plan");

        List<ProductionFacilityDestructiveDrainParticipantSaveData>
            missingOwner = CloneParticipants(participants);
        FindGeneric(missingOwner).owners.Clear();
        Require(
            !source.TryAdvance(
                operationId,
                prepared.revision,
                ProductionFacilityDestructiveDrainPhase.DrainingParticipants,
                lifecycle,
                missingOwner,
                out _,
                out string missingOwnerFailure)
                && missingOwnerFailure.EndsWith(
                    "participant-transition-invalid",
                    StringComparison.Ordinal),
            "destructive-drain advance deleted an immutable owner");

        List<ProductionFacilityDestructiveDrainParticipantSaveData>
            jumpedOwner = CloneParticipants(participants);
        ProductionFacilityDestructiveDrainOwnerSaveData jumped =
            FindGeneric(jumpedOwner).owners[0];
        jumped.phase =
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged;
        jumped.commitId = "qa-commit:jump";
        jumped.receiptFingerprint =
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:receipt:jump");
        Require(
            !source.TryAdvance(
                operationId,
                prepared.revision,
                ProductionFacilityDestructiveDrainPhase.DrainingParticipants,
                lifecycle,
                jumpedOwner,
                out _,
                out string jumpFailure)
                && jumpFailure.EndsWith(
                    "participant-transition-invalid",
                    StringComparison.Ordinal),
            "destructive-drain owner skipped its durable effect-commit phase");

        string drainingFingerprint = ProductionFacilityDestructiveDrainCanonical
            .ComputeFingerprint("qa:lifecycle:draining");
        Require(
            source.TryAdvance(
                operationId,
                prepared.revision,
                ProductionFacilityDestructiveDrainPhase.DrainingParticipants,
                drainingFingerprint,
                participants,
                out ProductionFacilityDestructiveDrainEntrySaveData draining,
                out string advanceFailure)
                && draining.revision == 2L,
            "durable drain phase advance failed: " + advanceFailure);
        Require(
            !source.TryAdvance(
                operationId,
                prepared.revision,
                ProductionFacilityDestructiveDrainPhase.DrainingParticipants,
                drainingFingerprint,
                participants,
                out _,
                out string staleFailure)
                && staleFailure.EndsWith("revision-stale", StringComparison.Ordinal),
            "stale durable drain revision was accepted");
        Require(
            !source.TryRemoveCheckpointed(
                operationId,
                draining.revision,
                out _),
            "nonterminal durable drain was garbage-collected");

        ProductionFacilityDestructiveDrainSaveSection sourceSection = new(
            source,
            ProductionOutputLifecycleRestoreCandidatePublisher
                .IsolatedSectionFixtureOnly);
        string json = sourceSection.Capture();
        DungeonRuntimeAggregateRootStore restoredRoots = new();
        ProductionFacilityDestructiveDrainJournal restored = new(
            restoredRoots,
            registry);
        ProductionFacilityDestructiveDrainSaveSection restoredSection = new(
            restored,
            ProductionOutputLifecycleRestoreCandidatePublisher
                .IsolatedSectionFixtureOnly);
        DungeonGameRestoreReport report = new();
        IDungeonSaveRestoreStage stage = restoredSection.StageRestore(
            json,
            DungeonProductionFacilityDestructiveDrainSaveData.CurrentVersion,
            report);
        stage.Commit(report);
        Require(
            report.Success
                && string.Equals(
                    json,
                    restoredSection.Capture(),
                    StringComparison.Ordinal)
                && restored.TryGet(operationId, out var restoredEntry)
                && restoredEntry.revision == draining.revision
                && restoredEntry.phase == draining.phase,
            "durable drain current-format save/restore was not byte-stable");

        DungeonProductionFacilityDestructiveDrainSaveData invalid =
            restored.Capture();
        invalid.entries[0].operationId =
            "production-facility-destructive-drain:building:other";
        RequireThrows(
            () => restored.BuildRestore(invalid),
            "mismatched durable drain operation/facility join was accepted");
        Debug.Log("Production facility destructive-drain journal contracts passed.");
    }

    private static List<ProductionFacilityDestructiveDrainParticipantSaveData>
        CloneParticipants(
            IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
                source)
    {
        List<ProductionFacilityDestructiveDrainParticipantSaveData> result =
            new();
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData value in
                 source)
        {
            result.Add(value.Clone());
        }
        return result;
    }

    private static ProductionFacilityDestructiveDrainParticipantSaveData
        FindGeneric(
            IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
                participants)
    {
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData value in
                 participants)
        {
            if (string.Equals(
                    value.participantId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .GenericProductionBills,
                    StringComparison.Ordinal))
            {
                return value;
            }
        }
        throw new InvalidOperationException(
            "The generic destructive-drain fixture participant is missing.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }
}
