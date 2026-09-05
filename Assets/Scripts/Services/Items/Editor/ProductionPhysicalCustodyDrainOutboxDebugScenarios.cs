#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ProductionPhysicalCustodyDrainOutboxDebugScenarios
{
    private const string StepOperationId =
        "production-facility-destructive-drain-step:qa:physical";
    private const string DestinationId =
        "production-output:building:qa:physical-drain";
    private static readonly string OwnerStableId =
        "physical-destination:" + DestinationId;

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Production Custody Drain Outbox")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_PRODUCTION_CUSTODY_DRAIN_OUTBOX=PASS");
    }

    public static void RunAll()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        ProductionPhysicalCustodyDrainOutbox outbox = new(repository);
        ProductionPhysicalCustodyDrainRequest request = Request();

        Require(outbox.TryPrepare(request).Status ==
                ProductionPhysicalCustodyDrainStatus.Applied,
            "Initial physical custody drain prepare failed.");
        Require(outbox.TryPrepare(request).Status ==
                ProductionPhysicalCustodyDrainStatus.Replay,
            "Exact physical custody drain prepare did not replay.");
        ProductionPhysicalCustodyDrainRequest conflict = CreateRequest(
            request.StepOperationId,
            request.OwnerStableId,
            request.SourceDestinationId,
            request.OwnerGridX + 1,
            request.OwnerGridY,
            request.SourceOwnershipFingerprint,
            request.SourceStackIds,
            request.SourceActorIds,
            request.SourceHaulIntentOperationIds,
            request.InputQuantity,
            request.InputMassGrams);
        Require(outbox.TryPrepare(conflict).Status ==
                ProductionPhysicalCustodyDrainStatus.Conflict,
            "Conflicting physical custody drain prepare was accepted.");

        Require(outbox.TryBeginDraining(
                    StepOperationId,
                    request.RequestFingerprint).Status ==
                ProductionPhysicalCustodyDrainStatus.Applied,
            "Physical custody drain did not enter actor release.");
        Require(outbox.TryRecordActorCompleted(
                    StepOperationId,
                    "character:qa:b").Status ==
                ProductionPhysicalCustodyDrainStatus.Conflict,
            "Out-of-order actor progress was accepted.");
        Require(outbox.TryRecordActorCompleted(
                    StepOperationId,
                    "character:qa:a").Status ==
                ProductionPhysicalCustodyDrainStatus.Applied
            && outbox.TryRecordActorCompleted(
                    StepOperationId,
                    "character:qa:a").Status ==
                ProductionPhysicalCustodyDrainStatus.Replay
            && outbox.TryRecordActorCompleted(
                    StepOperationId,
                    "character:qa:b").Status ==
                ProductionPhysicalCustodyDrainStatus.Applied,
            "Actor progress was not monotonic and idempotent.");
        Require(outbox.TryBeginReleasingIntents(StepOperationId).Status ==
                ProductionPhysicalCustodyDrainStatus.Applied,
            "Physical custody drain did not enter intent release.");
        Require(outbox.TryRecordHaulIntentReleased(
                    StepOperationId,
                    "haul:qa:b").Status ==
                ProductionPhysicalCustodyDrainStatus.Conflict,
            "Out-of-order haul-intent progress was accepted.");
        Require(outbox.TryRecordHaulIntentReleased(
                    StepOperationId,
                    "haul:qa:a").Status ==
                ProductionPhysicalCustodyDrainStatus.Applied
            && outbox.TryRecordHaulIntentReleased(
                    StepOperationId,
                    "haul:qa:b").Status ==
                ProductionPhysicalCustodyDrainStatus.Applied,
            "Haul-intent progress did not complete.");
        Require(outbox.TryBeginReleasingDestination(StepOperationId).Status ==
                ProductionPhysicalCustodyDrainStatus.Applied,
            "Physical custody drain did not enter destination release.");

        ProductionPhysicalCustodyDrainResult committed = outbox.TryCommitEffect(
            StepOperationId,
            new[] { "stack:qa:b", "stack:qa:a" },
            3,
            3_000L,
            new string('c', 64));
        Require(committed.Status == ProductionPhysicalCustodyDrainStatus.Applied
                && committed.CommitId.Length > 0
                && committed.ReceiptFingerprint.Length == 64,
            "Physical custody drain effect receipt was not committed.");
        Require(outbox.TryCommitEffect(
                    StepOperationId,
                    new[] { "stack:qa:a", "stack:qa:b" },
                    3,
                    3_000L,
                    new string('c', 64)).Status ==
                ProductionPhysicalCustodyDrainStatus.Replay,
            "Physical custody drain effect did not replay exactly.");
        Require(outbox.TryCapture(
                    StepOperationId,
                    out ProductionPhysicalCustodyDrainSaveData saved)
                && saved.phase == ProductionPhysicalCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck,
            "Committed physical custody drain receipt was not durable.");

        DungeonPhysicalItemSaveData physical = new()
        {
            haulingSettings = new ItemHaulingSettingsSnapshot
            {
                maxCarryMultiplier = 1.5f
            },
            pendingProductionCustodyDrains = new List<
                ProductionPhysicalCustodyDrainSaveData> { saved.Clone() }
        };
        DungeonGameRestoreReport valid =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                physical,
                new EmptyCatalog());
        Require(valid.Success,
            "Canonical physical custody drain save failed validation: "
            + string.Join(" | ", valid.Errors));
        DungeonPhysicalItemSaveData tampered =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                JsonUtility.ToJson(physical));
        tampered.pendingProductionCustodyDrains[0].releasedMassGrams--;
        DungeonGameRestoreReport invalid =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                tampered,
                new EmptyCatalog());
        Require(!invalid.Success,
            "A one-gram physical custody drain receipt mutation passed validation.");

        Require(outbox.TryAcknowledge(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionPhysicalCustodyDrainStatus.Applied
            && outbox.TryAcknowledge(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionPhysicalCustodyDrainStatus.Replay,
            "Physical custody drain acknowledgement was not idempotent.");
        Require(outbox.TryGarbageCollect(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionPhysicalCustodyDrainStatus.Applied
            && !outbox.TryCapture(StepOperationId, out _),
            "Acknowledged physical custody drain was not garbage-collected.");

        VerifyCheckpointGcTransaction();
    }

    private static void VerifyCheckpointGcTransaction()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        ProductionPhysicalCustodyDrainOutbox outbox = new(repository);
        IProductionPhysicalCustodyDrainCheckpointGcPort gc = outbox;
        ProductionPhysicalCustodyDrainSaveData rowA =
            CreateTerminalCheckpointRow(outbox, "a");
        ProductionPhysicalCustodyDrainSaveData rowB =
            CreateTerminalCheckpointRow(outbox, "b");
        ProductionPhysicalCustodyDrainSaveData rowC =
            CreateTerminalCheckpointRow(outbox, "c");
        string rowAJson = JsonUtility.ToJson(rowA);
        string rowCJson = JsonUtility.ToJson(rowC);
        int preparedItemRevision = repository.ItemStackVersion;
        int preparedHaulRevision = repository.HaulJobVersion;

        Require(gc.TryPrepareCheckpointGarbageCollection(
                    new[] { rowB, rowA },
                    out IProductionPhysicalCustodyDrainCheckpointGcCandidate
                        partialCandidate,
                    out string prepareFailure)
                && outbox.TryCapture(rowA.stepOperationId, out var preparedA)
                && string.Equals(
                    JsonUtility.ToJson(preparedA),
                    rowAJson,
                    StringComparison.Ordinal)
                && repository.ItemStackVersion == preparedItemRevision
                && repository.HaulJobVersion == preparedHaulRevision,
            "V27_PHYSICAL_CHECKPOINT_GC_PREPARE_MUTATED:" + prepareFailure);
        Require(!gc.TryPrepareCheckpointGarbageCollection(
                new[] { rowC }, out _, out _),
            "V27_PHYSICAL_CHECKPOINT_GC_ACCEPTED_SECOND_ACTIVE_CANDIDATE");
        Require(outbox.TryGarbageCollect(
                    rowA.stepOperationId,
                    rowA.receiptFingerprint).Status ==
                ProductionPhysicalCustodyDrainStatus.Deferred,
            "V27_PHYSICAL_CHECKPOINT_GC_DID_NOT_FENCE_LEGACY_GC");

        Require(RemovePendingForFault(
                repository,
                "RemovePendingProductionCustodyDrain",
                rowB.stepOperationId),
            "V27_PHYSICAL_CHECKPOINT_GC_FAULT_INJECTION_FAILED");
        int faultItemRevision = repository.ItemStackVersion;
        int faultHaulRevision = repository.HaulJobVersion;
        Require(!gc.TryPublishCheckpointGarbageCollection(
                    partialCandidate,
                    out _)
                && outbox.TryCapture(
                    rowA.stepOperationId,
                    out ProductionPhysicalCustodyDrainSaveData autoRestoredA)
                && string.Equals(
                    JsonUtility.ToJson(autoRestoredA),
                    rowAJson,
                    StringComparison.Ordinal),
            "V27_PHYSICAL_CHECKPOINT_GC_MIDDLE_FAILURE_NOT_OBSERVED");
        gc.RollbackCheckpointGarbageCollection(partialCandidate);
        bool hasRestoredA = outbox.TryCapture(
            rowA.stepOperationId,
            out ProductionPhysicalCustodyDrainSaveData restoredA);
        bool hasPreservedC = outbox.TryCapture(
            rowC.stepOperationId,
            out ProductionPhysicalCustodyDrainSaveData preservedC);
        Require(hasRestoredA
                && string.Equals(
                    JsonUtility.ToJson(restoredA),
                    rowAJson,
                    StringComparison.Ordinal)
                && !outbox.TryCapture(rowB.stepOperationId, out _)
                && hasPreservedC
                && string.Equals(
                    JsonUtility.ToJson(preservedC),
                    rowCJson,
                    StringComparison.Ordinal)
                && repository.ItemStackVersion == faultItemRevision
                && repository.HaulJobVersion == faultHaulRevision,
            "V27_PHYSICAL_CHECKPOINT_GC_PARTIAL_ROLLBACK_NOT_EXACT");
        gc.CompleteCheckpointGarbageCollection(partialCandidate);
        RequireThrows(() => gc.TryPublishCheckpointGarbageCollection(
                partialCandidate,
                out _),
            "V27_PHYSICAL_CHECKPOINT_GC_COMPLETED_CANDIDATE_REUSED");

        int exactItemRevision = repository.ItemStackVersion;
        int exactHaulRevision = repository.HaulJobVersion;
        Require(gc.TryPrepareCheckpointGarbageCollection(
                    new[] { restoredA },
                    out IProductionPhysicalCustodyDrainCheckpointGcCandidate
                        exactCandidate,
                    out _)
                && gc.TryPublishCheckpointGarbageCollection(
                    exactCandidate,
                    out _)
                && !outbox.TryCapture(rowA.stepOperationId, out _)
                && repository.ItemStackVersion == exactItemRevision
                && repository.HaulJobVersion == exactHaulRevision,
            "V27_PHYSICAL_CHECKPOINT_GC_PUBLISH_NOT_EXACT");
        gc.RollbackCheckpointGarbageCollection(exactCandidate);
        Require(outbox.TryCapture(rowA.stepOperationId, out var exactRestored)
                && string.Equals(
                    JsonUtility.ToJson(exactRestored),
                    rowAJson,
                    StringComparison.Ordinal)
                && repository.ItemStackVersion == exactItemRevision
                && repository.HaulJobVersion == exactHaulRevision,
            "V27_PHYSICAL_CHECKPOINT_GC_ROLLBACK_CHANGED_REVISIONS");
        gc.CompleteCheckpointGarbageCollection(exactCandidate);

        Require(gc.TryPrepareCheckpointGarbageCollection(
                    new[] { preservedC },
                    out IProductionPhysicalCustodyDrainCheckpointGcCandidate
                        completedCandidate,
                    out _)
                && gc.TryPublishCheckpointGarbageCollection(
                    completedCandidate,
                    out _),
            "V27_PHYSICAL_CHECKPOINT_GC_FINAL_PUBLISH_FAILED");
        gc.CompleteCheckpointGarbageCollection(completedCandidate);
        Require(!outbox.TryCapture(rowC.stepOperationId, out _),
            "V27_PHYSICAL_CHECKPOINT_GC_COMPLETE_RETAINED_ROW");
    }

    private static ProductionPhysicalCustodyDrainSaveData
        CreateTerminalCheckpointRow(
            ProductionPhysicalCustodyDrainOutbox outbox,
            string suffix)
    {
        string step = StepOperationId + ":checkpoint:" + suffix;
        string destination = DestinationId + ":checkpoint:" + suffix;
        string stack = "stack:qa:checkpoint:" + suffix;
        ProductionPhysicalCustodyDrainRequest request = CreateRequest(
            step,
            "physical-destination:" + destination,
            destination,
            7,
            9,
            new string('8', 64),
            new[] { stack },
            Array.Empty<string>(),
            Array.Empty<string>(),
            1,
            1_000L);
        Require(outbox.TryPrepare(request).Status ==
                ProductionPhysicalCustodyDrainStatus.Applied
            && outbox.TryBeginDraining(step, request.RequestFingerprint).Status ==
                ProductionPhysicalCustodyDrainStatus.Applied
            && outbox.TryBeginReleasingIntents(step).Status ==
                ProductionPhysicalCustodyDrainStatus.Applied
            && outbox.TryBeginReleasingDestination(step).Status ==
                ProductionPhysicalCustodyDrainStatus.Applied,
            "V27_PHYSICAL_CHECKPOINT_GC_TERMINAL_SETUP_FAILED:" + suffix);
        ProductionPhysicalCustodyDrainResult committed = outbox.TryCommitEffect(
            step,
            new[] { stack },
            1,
            1_000L,
            new string('9', 64));
        ProductionPhysicalCustodyDrainResult acknowledged =
            outbox.TryAcknowledge(step, committed.ReceiptFingerprint);
        bool hasTerminal = outbox.TryCapture(
            step,
            out ProductionPhysicalCustodyDrainSaveData terminal);
        Require(committed.Status == ProductionPhysicalCustodyDrainStatus.Applied
            && acknowledged.Status ==
                ProductionPhysicalCustodyDrainStatus.Applied
            && hasTerminal,
            "V27_PHYSICAL_CHECKPOINT_GC_ACK_SETUP_FAILED:" + suffix);
        return terminal;
    }

    private static bool RemovePendingForFault(
        WorldItemRepository repository,
        string methodName,
        string stepOperationId)
    {
        MethodInfo method = typeof(WorldItemRepository).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                typeof(WorldItemRepository).FullName,
                methodName);
        return (bool)method.Invoke(repository, new object[] { stepOperationId });
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

    private static ProductionPhysicalCustodyDrainRequest Request() =>
        CreateRequest(
            StepOperationId,
            OwnerStableId,
            DestinationId,
            7,
            9,
            new string('b', 64),
            new[] { "stack:qa:b", "stack:qa:a" },
            new[] { "character:qa:b", "character:qa:a" },
            new[] { "haul:qa:b", "haul:qa:a" },
            3,
            3_000L);

    private static ProductionPhysicalCustodyDrainRequest CreateRequest(
        string stepOperationId,
        string ownerStableId,
        string destinationId,
        int ownerGridX,
        int ownerGridY,
        string sourceOwnershipFingerprint,
        IEnumerable<string> sourceStackIds,
        IEnumerable<string> sourceActorIds,
        IEnumerable<string> sourceIntentIds,
        int inputQuantity,
        long inputMassGrams)
    {
        string[] stacks = sourceStackIds.OrderBy(
            value => value,
            StringComparer.Ordinal).ToArray();
        string[] actors = sourceActorIds.OrderBy(
            value => value,
            StringComparer.Ordinal).ToArray();
        string[] intents = sourceIntentIds.OrderBy(
            value => value,
            StringComparer.Ordinal).ToArray();
        string requestFingerprint =
            ProductionPhysicalCustodyDrainFingerprint.CreateRequest(
                stepOperationId,
                ownerStableId,
                destinationId,
                ownerGridX,
                ownerGridY,
                sourceOwnershipFingerprint,
                stacks,
                actors,
                intents,
                inputQuantity,
                inputMassGrams);
        return new ProductionPhysicalCustodyDrainRequest(
            stepOperationId,
            ownerStableId,
            destinationId,
            ownerGridX,
            ownerGridY,
            requestFingerprint,
            sourceOwnershipFingerprint,
            stacks,
            actors,
            intents,
            inputQuantity,
            inputMassGrams);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class EmptyCatalog : IDungeonItemCatalogProvider
    {
        public IReadOnlyList<DungeonItemDefinition> All =>
            Array.Empty<DungeonItemDefinition>();

        public DungeonItemDefinition GetDefinition(string itemId) =>
            throw new KeyNotFoundException(itemId);

        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition definition)
        {
            definition = null;
            return false;
        }
    }
}
#endif
