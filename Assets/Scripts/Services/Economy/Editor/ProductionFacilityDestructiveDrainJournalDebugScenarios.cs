using System;
using System.Collections.Generic;
using System.Linq;
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

        ProductionFacilityDestructiveDrainCheckpointGcContext checkpoint = new(
            1L,
            new string('a', 64),
            "qa-journal-slot");
        Require(
            restored.PrepareCheckpointGarbageCollection(
                    checkpoint,
                    Array.Empty<string>(),
                    out IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                        checkpointCandidate).Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
            && restored.PublishCheckpointGarbageCollection(checkpointCandidate)
                .Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            "journal checkpoint marker did not publish for an empty selection");
        restored.CompleteCheckpointGarbageCollection(checkpointCandidate);
        DungeonProductionFacilityDestructiveDrainSaveData checkpointed =
            restored.Capture();
        Require(
            checkpointed.lastConfirmedCheckpointSequence == 1L
            && string.Equals(
                checkpointed.lastConfirmedSerializedByteDigest,
                new string('a', 64),
                StringComparison.Ordinal)
            && restored.PrepareCheckpointGarbageCollection(
                    checkpoint,
                    Array.Empty<string>(),
                    out _).Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus
                    .AlreadyApplied,
            "journal checkpoint marker replay was not exact and idempotent");

        ProductionFacilityDestructiveDrainCheckpointGcContext rollback = new(
            2L,
            new string('b', 64),
            "qa-journal-slot");
        Require(
            restored.PrepareCheckpointGarbageCollection(
                    rollback,
                    Array.Empty<string>(),
                    out IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                        rollbackCandidate).Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
            && restored.PublishCheckpointGarbageCollection(rollbackCandidate)
                .Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            "journal rollback fixture did not publish");
        restored.RollbackCheckpointGarbageCollection(rollbackCandidate);
        restored.CompleteCheckpointGarbageCollection(rollbackCandidate);
        Require(restored.LastConfirmedCheckpointSequence == 1L
                && string.Equals(
                    restored.LastConfirmedSerializedByteDigest,
                    new string('a', 64),
                    StringComparison.Ordinal),
            "journal rollback did not restore the exact prior marker");

        DungeonProductionFacilityDestructiveDrainSaveData invalid =
            restored.Capture();
        invalid.entries[0].operationId =
            "production-facility-destructive-drain:building:other";
        RequireThrows(
            () => restored.BuildRestore(invalid),
            "mismatched durable drain operation/facility join was accepted");
        VerifyCheckpointMarkerSaveSectionRoundTrip(registry);
        VerifyRowRemovalAndMarkerSaveSectionRoundTrip(restored, registry);
        VerifyCheckpointMarkerShapeFailsLoud(restored);
        Debug.Log("Production facility destructive-drain journal contracts passed.");
    }

    private static void VerifyCheckpointMarkerSaveSectionRoundTrip(
        ProductionFacilityDestructiveDrainParticipantRegistry registry)
    {
        ProductionFacilityDestructiveDrainJournal source = new(
            new DungeonRuntimeAggregateRootStore(),
            registry);
        string digest = new('c', 64);
        ProductionFacilityDestructiveDrainCheckpointGcContext context = new(
            1L,
            digest,
            "qa-journal-marker-roundtrip");
        Require(
            source.PrepareCheckpointGarbageCollection(
                    context,
                    Array.Empty<string>(),
                    out IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                        candidate).Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
            && source.PublishCheckpointGarbageCollection(candidate).Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            "V3 journal marker-only checkpoint could not publish.");
        source.CompleteCheckpointGarbageCollection(candidate);

        ProductionFacilityDestructiveDrainJournal restored = RoundTripSection(
            source,
            registry,
            out string captured);
        Require(
            restored.CaptureOpen().Count == 0
            && restored.LastConfirmedCheckpointSequence == 1L
            && string.Equals(
                restored.LastConfirmedSerializedByteDigest,
                digest,
                StringComparison.Ordinal)
            && string.Equals(
                captured,
                new ProductionFacilityDestructiveDrainSaveSection(
                    restored,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly).Capture(),
                StringComparison.Ordinal),
            "V3 journal marker sequence/digest did not round-trip byte-exactly.");
    }

    private static void VerifyRowRemovalAndMarkerSaveSectionRoundTrip(
        ProductionFacilityDestructiveDrainJournal source,
        ProductionFacilityDestructiveDrainParticipantRegistry registry)
    {
        DungeonProductionFacilityDestructiveDrainSaveData terminal =
            source.Capture();
        Require(terminal.entries.Count == 1,
            "Combined checkpoint fixture requires one durable row.");
        ProductionFacilityDestructiveDrainEntrySaveData entry =
            terminal.entries[0];
        entry.phase = ProductionFacilityDestructiveDrainPhase
            .WorldRemovedAwaitingCheckpointGc;
        int ownerIndex = 0;
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in
                 entry.participants.SelectMany(value => value.owners))
        {
            owner.phase = ProductionFacilityDestructiveDrainStepPhase
                .OwnerAcknowledged;
            owner.commitId = "qa-journal-terminal-commit:" + ownerIndex;
            owner.receiptFingerprint = new string(
                (char)('d' + ownerIndex % 3),
                64);
            ownerIndex++;
        }

        ProductionFacilityDestructiveDrainJournal combined = new(
            new DungeonRuntimeAggregateRootStore(),
            registry);
        combined.Restore(combined.BuildRestore(terminal));
        Require(
            ProductionFacilityDestructiveDrainOperationId.TryParse(
                entry.operationId,
                out ProductionFacilityDestructiveDrainOperationId operation),
            "Combined checkpoint fixture operation ID was invalid.");
        string digest = new('f', 64);
        ProductionFacilityDestructiveDrainCheckpointGcContext context = new(
            checked(terminal.lastConfirmedCheckpointSequence + 1L),
            digest,
            "qa-journal-row-marker-roundtrip");
        Require(
            combined.PrepareCheckpointGarbageCollection(
                    context,
                    new[] { operation.Value },
                    out IProductionFacilityDestructiveDrainCheckpointGcJournalCandidate
                        candidate).Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
            && combined.PublishCheckpointGarbageCollection(candidate).Status ==
                ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied,
            "V3 journal combined row-removal/marker checkpoint did not publish.");
        combined.CompleteCheckpointGarbageCollection(candidate);

        ProductionFacilityDestructiveDrainJournal restored = RoundTripSection(
            combined,
            registry,
            out _);
        Require(
            restored.CaptureOpen().Count == 0
            && !restored.TryGet(operation, out _)
            && restored.LastConfirmedCheckpointSequence ==
                context.CheckpointSequence
            && string.Equals(
                restored.LastConfirmedSerializedByteDigest,
                digest,
                StringComparison.Ordinal),
            "V3 journal row removal and marker were not restored atomically.");
    }

    private static void VerifyCheckpointMarkerShapeFailsLoud(
        ProductionFacilityDestructiveDrainJournal source)
    {
        ProductionFacilityDestructiveDrainSaveSection section = new(
            source,
            ProductionOutputLifecycleRestoreCandidatePublisher
                .IsolatedSectionFixtureOnly);
        DungeonProductionFacilityDestructiveDrainSaveData baseline =
            source.Capture();
        baseline.entries.Clear();

        foreach ((long sequence, string digest, string id) invalid in new[]
                 {
                     (-1L, string.Empty, "negative-sequence"),
                     (0L, new string('1', 64), "zero-with-digest"),
                     (1L, string.Empty, "positive-without-digest"),
                     (1L, "not-a-digest", "positive-with-invalid-digest")
                 })
        {
            baseline.lastConfirmedCheckpointSequence = invalid.sequence;
            baseline.lastConfirmedSerializedByteDigest = invalid.digest;
            string json = JsonUtility.ToJson(baseline);
            RequireThrows(
                () => section.ValidatePayload(
                    json,
                    DungeonProductionFacilityDestructiveDrainSaveData
                        .CurrentVersion,
                    new DungeonGameRestoreReport()),
                "V3 journal accepted illegal marker combination: "
                + invalid.id);
        }

        baseline.lastConfirmedCheckpointSequence = 1L;
        baseline.lastConfirmedSerializedByteDigest = new string('2', 64);
        string valid = JsonUtility.ToJson(baseline);
        string missingSequence = valid.Replace(
            "\"lastConfirmedCheckpointSequence\":1,",
            string.Empty);
        string missingDigest = valid.Replace(
            "\"lastConfirmedSerializedByteDigest\":\""
            + baseline.lastConfirmedSerializedByteDigest + "\",",
            string.Empty);
        Require(!string.Equals(valid, missingSequence, StringComparison.Ordinal)
                && !string.Equals(valid, missingDigest, StringComparison.Ordinal),
            "V3 journal missing-field fixtures did not remove their scalars.");
        RequireThrows(
            () => section.ValidatePayload(
                missingSequence,
                DungeonProductionFacilityDestructiveDrainSaveData.CurrentVersion,
                new DungeonGameRestoreReport()),
            "V3 journal accepted a missing checkpoint sequence field.");
        RequireThrows(
            () => section.ValidatePayload(
                missingDigest,
                DungeonProductionFacilityDestructiveDrainSaveData.CurrentVersion,
                new DungeonGameRestoreReport()),
            "V3 journal accepted a missing checkpoint digest field.");

        DungeonProductionFacilityDestructiveDrainSaveData v2 = source.Capture();
        v2.version = 2;
        RequireThrows(
            () => section.ValidatePayload(
                JsonUtility.ToJson(v2),
                DungeonProductionFacilityDestructiveDrainSaveData.CurrentVersion,
                new DungeonGameRestoreReport()),
            "Legacy V2 journal payload was accepted by the V3-only boundary.");
    }

    private static ProductionFacilityDestructiveDrainJournal RoundTripSection(
        ProductionFacilityDestructiveDrainJournal source,
        ProductionFacilityDestructiveDrainParticipantRegistry registry,
        out string captured)
    {
        ProductionFacilityDestructiveDrainSaveSection sourceSection = new(
            source,
            ProductionOutputLifecycleRestoreCandidatePublisher
                .IsolatedSectionFixtureOnly);
        captured = sourceSection.Capture();
        ProductionFacilityDestructiveDrainJournal restored = new(
            new DungeonRuntimeAggregateRootStore(),
            registry);
        ProductionFacilityDestructiveDrainSaveSection restoredSection = new(
            restored,
            ProductionOutputLifecycleRestoreCandidatePublisher
                .IsolatedSectionFixtureOnly);
        DungeonGameRestoreReport report = new();
        IDungeonSaveRestoreStage stage = restoredSection.StageRestore(
            captured,
            DungeonProductionFacilityDestructiveDrainSaveData.CurrentVersion,
            report);
        stage.Commit(report);
        Require(report.Success, "V3 journal save-section round-trip failed.");
        return restored;
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
