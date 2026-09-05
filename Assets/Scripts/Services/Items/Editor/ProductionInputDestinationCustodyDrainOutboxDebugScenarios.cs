#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ProductionInputDestinationCustodyDrainOutboxDebugScenarios
{
    private const string ParentOperationId =
        "production-facility-destructive-drain:qa:input-destination";
    private const string StepOperationId =
        "production-facility-destructive-drain-step:qa:input-destination";
    private const string OwnerStableId =
        "production-input-destination:qa:input-destination";
    private const string BillId = "production-bill:qa:input-destination";
    private const string FacilityId = "building:qa:input-destination";
    private const string DestinationId =
        "production-input:building:qa:input-destination";

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Input Destination Custody Drain Outbox")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_INPUT_DESTINATION_CUSTODY_DRAIN_OUTBOX=PASS");
    }

    public static void RunAll()
    {
        VerifyClaimOnlyZeroInputFlow();
        VerifyPhysicalInputFlowAndPersistence();
        VerifyCheckpointGcTransaction();
    }

    private static void VerifyCheckpointGcTransaction()
    {
        WorldItemRepository repository = CreateRepository();
        ProductionInputDestinationCustodyDrainOutbox outbox = new(repository);
        IProductionInputDestinationCustodyDrainCheckpointGcPort gc = outbox;
        ProductionInputDestinationCustodyDrainSaveData rowA =
            CreateTerminalCheckpointRow(outbox, "a");
        ProductionInputDestinationCustodyDrainSaveData rowB =
            CreateTerminalCheckpointRow(outbox, "b");
        ProductionInputDestinationCustodyDrainSaveData rowC =
            CreateTerminalCheckpointRow(outbox, "c");
        string rowAJson = JsonUtility.ToJson(rowA);
        string rowCJson = JsonUtility.ToJson(rowC);
        int preparedItemRevision = repository.ItemStackVersion;
        int preparedHaulRevision = repository.HaulJobVersion;

        string overlapFailure = string.Empty;
        Require(gc.TryPrepareCheckpointGarbageCollection(
                    new[] { rowB, rowA },
                    out IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                        partialCandidate,
                    out string prepareFailure)
                && outbox.TryCapture(rowA.stepOperationId, out var preparedA)
                && string.Equals(
                    JsonUtility.ToJson(preparedA),
                    rowAJson,
                    StringComparison.Ordinal)
                && repository.ItemStackVersion == preparedItemRevision
                && repository.HaulJobVersion == preparedHaulRevision,
            "V27_INPUT_CHECKPOINT_GC_PREPARE_MUTATED:" + prepareFailure);
        Require(gc.TryPrepareCheckpointGarbageCollection(
                    new[] { rowC },
                    out IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                        disjointCandidate,
                    out string disjointFailure)
                && !gc.TryPrepareCheckpointGarbageCollection(
                    new[] { rowA }, out _, out overlapFailure)
                && string.Equals(
                    overlapFailure,
                    "production-input-destination-checkpoint-gc-overlap",
                    StringComparison.Ordinal),
            "V27_INPUT_CHECKPOINT_GC_DISJOINT_OR_OVERLAP_CONTRACT_FAILED:"
            + disjointFailure + ":" + overlapFailure);
        Require(outbox.TryGarbageCollect(
                    rowA.stepOperationId,
                    rowA.receiptFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Deferred,
            "V27_INPUT_CHECKPOINT_GC_DID_NOT_FENCE_LEGACY_GC");
        Require(gc.TryPublishCheckpointGarbageCollection(
                    disjointCandidate,
                    out string disjointPublishFailure)
                && !outbox.TryCapture(rowC.stepOperationId, out _),
            "V27_INPUT_CHECKPOINT_GC_DISJOINT_PUBLISH_FAILED:"
            + disjointPublishFailure);
        gc.RollbackCheckpointGarbageCollection(disjointCandidate);
        Require(outbox.TryCapture(rowC.stepOperationId, out var restoredDisjoint)
                && string.Equals(
                    JsonUtility.ToJson(restoredDisjoint),
                    rowCJson,
                    StringComparison.Ordinal),
            "V27_INPUT_CHECKPOINT_GC_DISJOINT_ROLLBACK_NOT_EXACT");
        gc.CompleteCheckpointGarbageCollection(disjointCandidate);

        Require(RemovePendingForFault(
                repository,
                "RemovePendingProductionInputDestinationDrain",
                rowB.stepOperationId),
            "V27_INPUT_CHECKPOINT_GC_FAULT_INJECTION_FAILED");
        int faultItemRevision = repository.ItemStackVersion;
        int faultHaulRevision = repository.HaulJobVersion;
        Require(!gc.TryPublishCheckpointGarbageCollection(
                    partialCandidate,
                    out _)
                && outbox.TryCapture(
                    rowA.stepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData
                        autoRestoredA)
                && string.Equals(
                    JsonUtility.ToJson(autoRestoredA),
                    rowAJson,
                    StringComparison.Ordinal),
            "V27_INPUT_CHECKPOINT_GC_MIDDLE_FAILURE_NOT_OBSERVED");
        gc.RollbackCheckpointGarbageCollection(partialCandidate);
        bool hasRestoredA = outbox.TryCapture(
            rowA.stepOperationId,
            out ProductionInputDestinationCustodyDrainSaveData restoredA);
        bool hasPreservedC = outbox.TryCapture(
            rowC.stepOperationId,
            out ProductionInputDestinationCustodyDrainSaveData preservedC);
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
            "V27_INPUT_CHECKPOINT_GC_PARTIAL_ROLLBACK_NOT_EXACT");
        gc.CompleteCheckpointGarbageCollection(partialCandidate);
        RequireThrows(() => gc.TryPublishCheckpointGarbageCollection(
                partialCandidate,
                out _),
            "V27_INPUT_CHECKPOINT_GC_COMPLETED_CANDIDATE_REUSED");

        Require(gc.TryPrepareCheckpointGarbageCollection(
                    new[] { restoredA, preservedC },
                    out IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                        conflictCandidate,
                    out _)
                && gc.TryPublishCheckpointGarbageCollection(
                    conflictCandidate,
                    out _),
            "V27_INPUT_CHECKPOINT_GC_CONFLICT_FIXTURE_PUBLISH_FAILED");
        ProductionInputDestinationCustodyDrainSaveData driftedA =
            JsonUtility.FromJson<ProductionInputDestinationCustodyDrainSaveData>(
                rowAJson);
        driftedA.billId += ":rollback-conflict";
        SetPendingForFault(
            repository,
            "SetPendingProductionInputDestinationDrain",
            driftedA);
        RequireThrows(
            () => gc.RollbackCheckpointGarbageCollection(conflictCandidate),
            "V27_INPUT_CHECKPOINT_GC_ROLLBACK_CONFLICT_NOT_REJECTED");
        Require(outbox.TryCapture(rowA.stepOperationId, out var preservedDrift)
                && string.Equals(
                    JsonUtility.ToJson(preservedDrift),
                    JsonUtility.ToJson(driftedA),
                    StringComparison.Ordinal)
                && !outbox.TryCapture(rowC.stepOperationId, out _),
            "V27_INPUT_CHECKPOINT_GC_ROLLBACK_CONFLICT_PARTIALLY_RESTORED");
        Require(RemovePendingForFault(
                repository,
                "RemovePendingProductionInputDestinationDrain",
                rowA.stepOperationId),
            "V27_INPUT_CHECKPOINT_GC_ROLLBACK_CONFLICT_CLEANUP_FAILED");
        gc.RollbackCheckpointGarbageCollection(conflictCandidate);
        Require(outbox.TryCapture(rowA.stepOperationId, out restoredA)
                && outbox.TryCapture(rowC.stepOperationId, out preservedC)
                && string.Equals(
                    JsonUtility.ToJson(restoredA),
                    rowAJson,
                    StringComparison.Ordinal)
                && string.Equals(
                    JsonUtility.ToJson(preservedC),
                    rowCJson,
                    StringComparison.Ordinal),
            "V27_INPUT_CHECKPOINT_GC_CONFLICT_RECOVERY_NOT_EXACT");
        gc.CompleteCheckpointGarbageCollection(conflictCandidate);

        int exactItemRevision = repository.ItemStackVersion;
        int exactHaulRevision = repository.HaulJobVersion;
        Require(gc.TryPrepareCheckpointGarbageCollection(
                    new[] { restoredA },
                    out IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                        exactCandidate,
                    out _)
                && gc.TryPublishCheckpointGarbageCollection(
                    exactCandidate,
                    out _)
                && !outbox.TryCapture(rowA.stepOperationId, out _)
                && repository.ItemStackVersion == exactItemRevision
                && repository.HaulJobVersion == exactHaulRevision,
            "V27_INPUT_CHECKPOINT_GC_PUBLISH_NOT_EXACT");
        gc.RollbackCheckpointGarbageCollection(exactCandidate);
        Require(outbox.TryCapture(rowA.stepOperationId, out var exactRestored)
                && string.Equals(
                    JsonUtility.ToJson(exactRestored),
                    rowAJson,
                    StringComparison.Ordinal)
                && repository.ItemStackVersion == exactItemRevision
                && repository.HaulJobVersion == exactHaulRevision,
            "V27_INPUT_CHECKPOINT_GC_ROLLBACK_CHANGED_REVISIONS");
        gc.CompleteCheckpointGarbageCollection(exactCandidate);

        Require(gc.TryPrepareCheckpointGarbageCollection(
                    new[] { preservedC },
                    out IProductionInputDestinationCustodyDrainCheckpointGcCandidate
                        completedCandidate,
                    out _)
                && gc.TryPublishCheckpointGarbageCollection(
                    completedCandidate,
                    out _),
            "V27_INPUT_CHECKPOINT_GC_FINAL_PUBLISH_FAILED");
        gc.CompleteCheckpointGarbageCollection(completedCandidate);
        Require(!outbox.TryCapture(rowC.stepOperationId, out _),
            "V27_INPUT_CHECKPOINT_GC_COMPLETE_RETAINED_ROW");
    }

    private static ProductionInputDestinationCustodyDrainSaveData
        CreateTerminalCheckpointRow(
            ProductionInputDestinationCustodyDrainOutbox outbox,
            string suffix)
    {
        string step = StepOperationId + ":checkpoint:" + suffix;
        ProductionInputDestinationCustodyDrainRequest request = CreateRequest(
            step,
            BillId + ":checkpoint:" + suffix,
            DestinationId + ":checkpoint:" + suffix,
            Array.Empty<ProductionInputDestinationDrainStackSaveData>(),
            Array.Empty<ProductionInputDestinationDrainOperationSaveData>(),
            Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
            0,
            0L);
        Require(outbox.TryPrepare(request).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
            && outbox.TryBeginDraining(step, request.RequestFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
            && outbox.TryBeginReleasingOperationAuthority(step).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
            && outbox.TryBeginReleasingDestination(step).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "V27_INPUT_CHECKPOINT_GC_TERMINAL_SETUP_FAILED:" + suffix);
        ProductionInputDestinationCustodyDrainResult committed =
            outbox.TryCommitEffect(
                step,
                Array.Empty<string>(),
                0,
                0L,
                new string('7', 64));
        ProductionInputDestinationCustodyDrainResult acknowledged =
            outbox.TryAcknowledge(step, committed.ReceiptFingerprint);
        bool hasTerminal = outbox.TryCapture(
            step,
            out ProductionInputDestinationCustodyDrainSaveData terminal);
        Require(committed.Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
            && acknowledged.Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
            && hasTerminal,
            "V27_INPUT_CHECKPOINT_GC_ACK_SETUP_FAILED:" + suffix);
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

    private static void SetPendingForFault(
        WorldItemRepository repository,
        string methodName,
        ProductionInputDestinationCustodyDrainSaveData row)
    {
        MethodInfo method = typeof(WorldItemRepository).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                typeof(WorldItemRepository).FullName,
                methodName);
        method.Invoke(repository, new object[] { row });
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

    private static void VerifyClaimOnlyZeroInputFlow()
    {
        WorldItemRepository repository = CreateRepository();
        ProductionInputDestinationCustodyDrainOutbox outbox = new(repository);
        ProductionInputDestinationCustodyDrainRequest request = CreateRequest(
            StepOperationId + ":claim-only",
            BillId + ":claim-only",
            DestinationId + ":claim-only",
            Array.Empty<ProductionInputDestinationDrainStackSaveData>(),
            Array.Empty<ProductionInputDestinationDrainOperationSaveData>(),
            Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
            inputQuantity: 0,
            inputMassGrams: 0L);

        Require(outbox.TryPrepare(request).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Claim-only input destination drain prepare failed.");
        Require(outbox.TryBeginDraining(
                    request.StepOperationId,
                    request.RequestFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Claim-only drain did not enter actor release.");
        Require(outbox.TryBeginReleasingOperationAuthority(
                    request.StepOperationId).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Claim-only drain did not pass its empty actor vector.");
        Require(outbox.TryBeginReleasingDestination(
                    request.StepOperationId).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Claim-only drain did not pass its empty operation vector.");

        ProductionInputDestinationCustodyDrainResult committed =
            outbox.TryCommitEffect(
                request.StepOperationId,
                Array.Empty<string>(),
                releasedQuantity: 0,
                releasedMassGrams: 0L,
                resultFingerprint: new string('1', 64));
        Require(committed.Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
                && committed.CommitId.Length > 0
                && committed.ReceiptFingerprint.Length == 64,
            "Claim-only input destination drain did not commit exactly.");
        Require(outbox.TryAcknowledge(
                    request.StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Claim-only input destination drain was not acknowledged.");
        Require(outbox.TryGarbageCollect(
                    request.StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
                && !outbox.TryCapture(request.StepOperationId, out _),
            "Claim-only input destination drain was not garbage-collected.");
    }

    private static void VerifyPhysicalInputFlowAndPersistence()
    {
        WorldItemRepository repository = CreateRepository();
        ProductionInputDestinationCustodyDrainOutbox outbox = new(repository);
        ProductionInputDestinationDrainStackSaveData[] stacks = CreateStacks();
        ProductionInputDestinationDrainOperationSaveData[] operations =
            CreateOperations();
        ProductionInputDestinationDrainActorSaveData[] actors = CreateActors();
        ProductionInputDestinationCustodyDrainRequest request = CreateRequest(
            StepOperationId,
            BillId,
            DestinationId,
            stacks.Reverse(),
            operations.Reverse(),
            actors.Reverse(),
            inputQuantity: 3,
            inputMassGrams: 3_000L);
        ProductionInputDestinationCustodyDrainRequest canonicalReplay =
            CreateRequest(
                StepOperationId,
                BillId,
                DestinationId,
                stacks,
                operations,
                actors,
                inputQuantity: 3,
                inputMassGrams: 3_000L);

        Require(outbox.TryPrepare(request).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Initial input destination custody drain prepare failed.");
        Require(string.Equals(
                request.RequestFingerprint,
                canonicalReplay.RequestFingerprint,
                StringComparison.Ordinal)
            && outbox.TryPrepare(canonicalReplay).Status ==
                ProductionInputDestinationCustodyDrainStatus.Replay,
            "Equivalent input ordering was not canonical or did not replay.");
        ProductionInputDestinationCustodyDrainRequest conflict = CreateRequest(
            StepOperationId,
            BillId,
            DestinationId,
            stacks,
            operations,
            actors,
            inputQuantity: 3,
            inputMassGrams: 3_000L,
            ownerGridX: 8);
        Require(outbox.TryPrepare(conflict).Status ==
                ProductionInputDestinationCustodyDrainStatus.Conflict,
            "Conflicting input destination custody request was accepted.");

        Require(outbox.TryBeginDraining(
                    StepOperationId,
                    request.RequestFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Input destination custody drain did not enter actor release.");
        Require(outbox.TryRecordActorCompleted(
                    StepOperationId,
                    "character:qa:b").Status ==
                ProductionInputDestinationCustodyDrainStatus.Conflict,
            "Out-of-order input destination actor progress was accepted.");
        Require(outbox.TryRecordActorCompleted(
                    StepOperationId,
                    "character:qa:a").Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
            && outbox.TryRecordActorCompleted(
                    StepOperationId,
                    "character:qa:a").Status ==
                ProductionInputDestinationCustodyDrainStatus.Replay
            && outbox.TryRecordActorCompleted(
                    StepOperationId,
                    "character:qa:b").Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Input destination actor progress was not prefix ordered and idempotent.");
        Require(outbox.TryBeginReleasingOperationAuthority(
                    StepOperationId).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Input destination custody drain did not enter operation release.");
        Require(outbox.TryRecordOperationReleased(
                    StepOperationId,
                    "haul:qa:b").Status ==
                ProductionInputDestinationCustodyDrainStatus.Conflict,
            "Out-of-order input destination operation progress was accepted.");
        Require(outbox.TryRecordOperationReleased(
                    StepOperationId,
                    "haul:qa:a").Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
            && outbox.TryRecordOperationReleased(
                    StepOperationId,
                    "haul:qa:a").Status ==
                ProductionInputDestinationCustodyDrainStatus.Replay
            && outbox.TryRecordOperationReleased(
                    StepOperationId,
                    "haul:qa:b").Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Input destination operation progress was not prefix ordered and idempotent.");
        Require(outbox.TryBeginReleasingDestination(StepOperationId).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied,
            "Input destination custody drain did not enter destination release.");

        Require(outbox.TryCommitEffect(
                    StepOperationId,
                    new[] { "stack:qa:b", "stack:qa:a" },
                    releasedQuantity: 3,
                    releasedMassGrams: 2_999L,
                    resultFingerprint: new string('2', 64)).Status ==
                ProductionInputDestinationCustodyDrainStatus.Deferred,
            "A one-gram input destination result mismatch was accepted.");
        ProductionInputDestinationCustodyDrainResult committed =
            outbox.TryCommitEffect(
                StepOperationId,
                new[] { "stack:qa:b", "stack:qa:a" },
                releasedQuantity: 3,
                releasedMassGrams: 3_000L,
                resultFingerprint: new string('2', 64));
        Require(committed.Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
                && committed.CommitId.Length > 0
                && committed.ReceiptFingerprint.Length == 64,
            "Exact input destination quantity, gram, and stack vector did not commit.");
        Require(outbox.TryCommitEffect(
                    StepOperationId,
                    new[] { "stack:qa:a", "stack:qa:b" },
                    releasedQuantity: 3,
                    releasedMassGrams: 3_000L,
                    resultFingerprint: new string('2', 64)).Status ==
                ProductionInputDestinationCustodyDrainStatus.Replay,
            "Exact input destination result did not replay.");

        Require(outbox.TryCapture(
                    StepOperationId,
                    out ProductionInputDestinationCustodyDrainSaveData saved)
                && saved.phase == ProductionInputDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingBillAck
                && saved.releasedQuantity == 3
                && saved.releasedMassGrams == 3_000L
                && saved.releasedStackIds.SequenceEqual(
                    new[] { "stack:qa:a", "stack:qa:b" },
                    StringComparer.Ordinal)
                && ProductionInputDestinationCustodyDrainContract
                    .IsValidSave(saved),
            "Committed input destination custody receipt was not durably valid.");
        DungeonPhysicalItemSaveData physical = new()
        {
            haulingSettings = new ItemHaulingSettingsSnapshot
            {
                maxCarryMultiplier = 1.5f
            },
            pendingProductionInputDestinationDrains = new List<
                ProductionInputDestinationCustodyDrainSaveData>
            {
                saved.Clone()
            }
        };
        DungeonGameRestoreReport valid =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                physical,
                new EmptyCatalog());
        Require(valid.Success,
            "Canonical input destination custody save failed validation: "
            + string.Join(" | ", valid.Errors));

        ProductionInputDestinationCustodyDrainSaveData tampered = saved.Clone();
        tampered.sourceOwnershipFingerprint = new string('f', 64);
        Require(!ProductionInputDestinationCustodyDrainContract
                .IsValidSave(tampered),
            "A source fingerprint mutation passed the input destination contract.");
        DungeonPhysicalItemSaveData tamperedPhysical = new()
        {
            haulingSettings = new ItemHaulingSettingsSnapshot
            {
                maxCarryMultiplier = 1.5f
            },
            pendingProductionInputDestinationDrains = new List<
                ProductionInputDestinationCustodyDrainSaveData> { tampered }
        };
        DungeonGameRestoreReport invalid =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                tamperedPhysical,
                new EmptyCatalog());
        Require(!invalid.Success,
            "A tampered input destination custody fingerprint passed save validation.");

        Require(outbox.TryAcknowledge(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
            && outbox.TryAcknowledge(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Replay,
            "Input destination custody acknowledgement was not idempotent.");
        Require(outbox.TryGarbageCollect(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionInputDestinationCustodyDrainStatus.Applied
            && !outbox.TryCapture(StepOperationId, out _),
            "Acknowledged input destination custody drain was not garbage-collected.");
    }

    private static WorldItemRepository CreateRepository() => new(
        new GuidPersistentIdGenerator(),
        new DungeonRuntimeAggregateRootStore());

    private static ProductionInputDestinationCustodyDrainRequest CreateRequest(
        string stepOperationId,
        string billId,
        string destinationId,
        IEnumerable<ProductionInputDestinationDrainStackSaveData> stacks,
        IEnumerable<ProductionInputDestinationDrainOperationSaveData> operations,
        IEnumerable<ProductionInputDestinationDrainActorSaveData> actors,
        int inputQuantity,
        long inputMassGrams,
        int ownerGridX = 7)
    {
        ProductionInputDestinationDrainStackSaveData[] stackRows =
            (stacks ?? Array.Empty<
                ProductionInputDestinationDrainStackSaveData>()).ToArray();
        ProductionInputDestinationDrainOperationSaveData[] operationRows =
            (operations ?? Array.Empty<
                ProductionInputDestinationDrainOperationSaveData>()).ToArray();
        ProductionInputDestinationDrainActorSaveData[] actorRows =
            (actors ?? Array.Empty<
                ProductionInputDestinationDrainActorSaveData>()).ToArray();
        string requestFingerprint =
            ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                ParentOperationId,
                stepOperationId,
                OwnerStableId,
                billId,
                FacilityId,
                destinationId,
                ownerGridX,
                9,
                new string('a', 64),
                new string('b', 64),
                stackRows,
                operationRows,
                actorRows,
                inputQuantity,
                inputMassGrams);
        return new ProductionInputDestinationCustodyDrainRequest(
            ParentOperationId,
            stepOperationId,
            OwnerStableId,
            billId,
            FacilityId,
            destinationId,
            ownerGridX,
            9,
            new string('a', 64),
            new string('b', 64),
            stackRows,
            operationRows,
            actorRows,
            inputQuantity,
            inputMassGrams,
            requestFingerprint);
    }

    private static ProductionInputDestinationDrainStackSaveData[] CreateStacks() =>
        new[]
        {
            new ProductionInputDestinationDrainStackSaveData
            {
                stackId = "stack:qa:a",
                itemId = "item:qa:a",
                componentFingerprint = new string('c', 64),
                quantity = 1,
                massGrams = 1_000L,
                state = WorldItemStackState.Carried,
                positionX = 3,
                positionY = 4,
                sourceStorageDestinationId = "warehouse:qa:a",
                destinationPositionX = 7,
                destinationPositionY = 9,
                reservationRevision = 4L
            },
            new ProductionInputDestinationDrainStackSaveData
            {
                stackId = "stack:qa:b",
                itemId = "item:qa:b",
                componentFingerprint = new string('d', 64),
                quantity = 2,
                massGrams = 2_000L,
                state = WorldItemStackState.Stored,
                positionX = 1,
                positionY = 2,
                sourceStorageDestinationId = "warehouse:qa:b",
                destinationPositionX = 7,
                destinationPositionY = 9,
                reservationRevision = 5L
            }
        };

    private static ProductionInputDestinationDrainOperationSaveData[]
        CreateOperations() => new[]
        {
            new ProductionInputDestinationDrainOperationSaveData
            {
                operationId = "haul:qa:a",
                actorId = "character:qa:a",
                hadCommittedPickup = true,
                operationFingerprint = new string('e', 64),
                leaseAuthorityFingerprints = new List<string> { new string('1', 64) },
                carriedStackIds = new List<string> { "stack:qa:a" }
            },
            new ProductionInputDestinationDrainOperationSaveData
            {
                operationId = "haul:qa:b",
                actorId = "character:qa:b",
                hadCommittedPickup = true,
                operationFingerprint = new string('f', 64),
                leaseAuthorityFingerprints = new List<string> { new string('2', 64) },
                carriedStackIds = new List<string> { "stack:qa:b" }
            }
        };

    private static ProductionInputDestinationDrainActorSaveData[] CreateActors() =>
        new[]
        {
            new ProductionInputDestinationDrainActorSaveData
            {
                actorId = "character:qa:a",
                sourcePhysicalFingerprint = new string('3', 64),
                allowedOperationIds = new List<string> { "haul:qa:a" }
            },
            new ProductionInputDestinationDrainActorSaveData
            {
                actorId = "character:qa:b",
                sourcePhysicalFingerprint = new string('4', 64),
                allowedOperationIds = new List<string> { "haul:qa:b" }
            }
        };

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
