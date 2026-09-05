#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ProductionCapacityRoutingDrainOutboxDebugScenarios
{
    private const string StepOperationId =
        "production-facility-destructive-drain-step:qa:capacity";
    private const string FacilityId = "building:qa:capacity-drain";
    private const string DestinationId =
        "production-output:building:qa:capacity-drain";
    private const string BatchCommitId = "batch:qa:capacity-drain";
    private static readonly string OwnerStableId =
        "routing-batch:" + BatchCommitId;

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Capacity Routing Drain Outbox")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_CAPACITY_ROUTING_DRAIN_OUTBOX=PASS");
    }

    public static void RunAll()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        ProductionCapacityRoutingDrainOutbox outbox = new(repository);
        ProductionCapacityRoutingDrainRequest invalidProvenance = CreateRequest(
            tamperCapabilityFingerprint: true);
        Require(outbox.TryPrepare(invalidProvenance).Status ==
                ProductionCapacityRoutingDrainStatus.Conflict,
            "Capacity-routing drain accepted a drifted capability fingerprint.");
        ProductionCapacityRoutingDrainRequest request = CreateRequest();

        Require(outbox.TryPrepare(request).Status ==
                ProductionCapacityRoutingDrainStatus.Applied,
            "Initial capacity-routing drain prepare failed.");
        Require(outbox.TryPrepare(request).Status ==
                ProductionCapacityRoutingDrainStatus.Replay,
            "Exact capacity-routing drain prepare did not replay.");
        ProductionCapacityRoutingDrainRequest conflict = CreateRequest(
            facilityId: "building:qa:capacity-drain-conflict");
        Require(outbox.TryPrepare(conflict).Status ==
                ProductionCapacityRoutingDrainStatus.Conflict,
            "Conflicting capacity-routing drain prepare was accepted.");

        Require(outbox.TryBeginRouting(
                    StepOperationId,
                    request.RequestFingerprint).Status ==
                ProductionCapacityRoutingDrainStatus.Applied,
            "Capacity-routing drain did not enter remainder routing.");
        Require(outbox.TryRecordLineRouted(
                    StepOperationId,
                    "line:qa:b").Status ==
                ProductionCapacityRoutingDrainStatus.Conflict,
            "Out-of-order routing-line progress was accepted.");
        Require(outbox.TryRecordLineRouted(
                    StepOperationId,
                    "line:qa:a").Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && outbox.TryRecordLineRouted(
                    StepOperationId,
                    "line:qa:a").Status ==
                ProductionCapacityRoutingDrainStatus.Replay
            && outbox.TryRecordLineRouted(
                    StepOperationId,
                    "line:qa:b").Status ==
                ProductionCapacityRoutingDrainStatus.Applied,
            "Routing-line progress was not monotonic and idempotent.");

        Require(outbox.TryBeginQuiescingActors(
                    StepOperationId,
                    new[] { "route:qa:b", "route:qa:a" },
                    new[] { "stack:qa:b", "stack:qa:a" }).Status ==
                ProductionCapacityRoutingDrainStatus.Applied,
            "Capacity-routing drain did not freeze the terminal route vector.");
        ProductionCapacityRoutingActorQuiesceReceiptSaveData unexpectedReceipt =
            CreateActorReceipt(
                request,
                "character:qa:b",
                new[]
                {
                    "character:qa:b|haul:qa:b|route:qa:b|stack:qa:b|source:qa:b"
                });
        Require(outbox.TryConfirmActorQuiesced(
                    StepOperationId,
                    unexpectedReceipt).Status ==
                ProductionCapacityRoutingDrainStatus.Conflict,
            "An unplanned actor quiesce was accepted.");
        ProductionCapacityRoutingActorQuiesceReceiptSaveData actorReceipt =
            CreateActorReceipt(
                request,
                "character:qa:a",
                request.SourceActorCarries.Select(
                    ProductionCapacityRoutingDrainFingerprint.ActorCarryKey));
        Require(outbox.TryConfirmActorQuiesced(
                    StepOperationId,
                    actorReceipt).Status ==
                ProductionCapacityRoutingDrainStatus.Deferred,
            "Outbox accepted an actor receipt before atomic physical publication.");
        outbox.PublishEditorTestActorQuiesceReceipt(
            StepOperationId,
            actorReceipt);
        Require(outbox.TryConfirmActorQuiesced(
                    StepOperationId,
                    actorReceipt).Status ==
                ProductionCapacityRoutingDrainStatus.Replay,
            "Atomically published actor quiesce receipt did not replay.");
        RequireCurrentDrainRejectedAsUnstable(
            outbox,
            "QuiescingActors was accepted as a stable current-format save.");
        ProductionCapacityRoutingActorQuiesceReceiptSaveData alteredReceipt =
            actorReceipt.Clone();
        alteredReceipt.physicalCellX++;
        alteredReceipt.receiptFingerprint =
            ProductionCapacityRoutingDrainFingerprint
                .CreateActorQuiesceReceiptFingerprint(
                    request.StepOperationId,
                    request.RequestFingerprint,
                    alteredReceipt);
        Require(outbox.TryConfirmActorQuiesced(
                    StepOperationId,
                    alteredReceipt).Status ==
                ProductionCapacityRoutingDrainStatus.Conflict,
            "Altered actor quiesce receipt replay was accepted.");
        Require(outbox.TryBeginReleasingOperationAuthority(
                    StepOperationId).Status ==
                ProductionCapacityRoutingDrainStatus.Applied,
            "Capacity-routing drain did not enter authority release.");
        ProductionCapacityRoutingActorAuthorityReleaseSaveData releasePlan =
            CreateAuthorityReleasePlan(request, actorReceipt);
        Require(outbox.TryPrepareActorAuthorityRelease(
                    StepOperationId,
                    request.RequestFingerprint,
                    releasePlan).Status ==
                ProductionCapacityRoutingDrainStatus.Applied,
            "Actor authority-release plan was not prepared.");
        Require(outbox.TryPrepareActorAuthorityRelease(
                    StepOperationId,
                    request.RequestFingerprint,
                    releasePlan).Status ==
                ProductionCapacityRoutingDrainStatus.Replay,
            "Actor authority-release plan did not replay exactly.");
        RequireCurrentDrainRejectedAsUnstable(
            outbox,
            "Prepared authority release was accepted as a stable current-format save.");
        ProductionCapacityRoutingActorAuthorityReleaseSaveData
            conflictingRelease = releasePlan.Clone();
        conflictingRelease.operations[0].haulIntentFingerprint =
            new string('a', 64);
        conflictingRelease.planFingerprint =
            ProductionCapacityRoutingDrainFingerprint
                .CreateActorAuthorityReleasePlanFingerprint(
                    request.StepOperationId,
                    request.RequestFingerprint,
                    conflictingRelease);
        Require(outbox.TryPrepareActorAuthorityRelease(
                    StepOperationId,
                    request.RequestFingerprint,
                    conflictingRelease).Status ==
                ProductionCapacityRoutingDrainStatus.Conflict,
            "Conflicting actor authority-release plan was accepted.");
        string authorityEffect = ProductionCapacityRoutingDrainFingerprint
            .CreateActorAuthorityReleaseEffectFingerprint(
                releasePlan.planFingerprint,
                actorPlanFinalized: true);
        Require(outbox.TryCommitActorAuthorityRelease(
                    StepOperationId,
                    releasePlan.planFingerprint,
                    ProductionCapacityRoutingDrainFingerprint
                        .CreateActorAuthorityReleaseEffectFingerprint(
                            releasePlan.planFingerprint,
                            actorPlanFinalized: false),
                    actorPlanFinalized: false).Status ==
                ProductionCapacityRoutingDrainStatus.Conflict,
            "Non-final actor authority effect was accepted.");
        Require(outbox.TryCommitActorAuthorityRelease(
                    StepOperationId,
                    releasePlan.planFingerprint,
                    authorityEffect,
                    actorPlanFinalized: true).Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && outbox.TryCommitActorAuthorityRelease(
                    StepOperationId,
                    releasePlan.planFingerprint,
                    authorityEffect,
                    actorPlanFinalized: true).Status ==
                ProductionCapacityRoutingDrainStatus.Replay,
            "Actor-wide authority release was not committed idempotently.");
        RequireCurrentDrainRejectedAsUnstable(
            outbox,
            "Committed release still in the runtime transition was accepted as a stable save.");
        Require(outbox.TryBeginAwaitingStablePhysicalState(
                    StepOperationId).Status ==
                ProductionCapacityRoutingDrainStatus.Applied,
            "Capacity-routing drain did not enter physical stabilization.");
        Require(outbox.TryRecordStablePhysicalStack(
                    StepOperationId,
                    "stack:qa:b").Status ==
                ProductionCapacityRoutingDrainStatus.Conflict,
            "Out-of-order physical stabilization was accepted.");
        Require(outbox.TryRecordStablePhysicalStack(
                    StepOperationId,
                    "stack:qa:a").Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && outbox.TryRecordStablePhysicalStack(
                    StepOperationId,
                    "stack:qa:b").Status ==
                ProductionCapacityRoutingDrainStatus.Applied,
            "Stable physical stack progress did not complete.");
        Require(outbox.TryBeginAwaitingDurableCheckpointGc(
                    StepOperationId).Status ==
                ProductionCapacityRoutingDrainStatus.Applied,
            "Capacity-routing drain did not enter durable checkpoint wait.");
        Require(outbox.TryCapture(
                    StepOperationId,
                    out ProductionCapacityRoutingDrainSaveData stableSaved)
                && stableSaved.phase == ProductionCapacityRoutingDrainPhase
                    .AwaitingDurableCheckpointGc,
            "Stable capacity-routing checkpoint row was not durable.");
        Require(outbox.TryCommitEffect(
                    StepOperationId,
                    BatchCommitId,
                    3,
                    2_999L,
                    new string('f', 64)).Status ==
                ProductionCapacityRoutingDrainStatus.Conflict,
            "A one-gram capacity-routing result mutation was accepted.");

        ProductionCapacityRoutingDrainResult committed =
            outbox.TryCommitEffect(
                StepOperationId,
                BatchCommitId,
                3,
                3_000L,
                new string('f', 64));
        Require(committed.Status == ProductionCapacityRoutingDrainStatus.Applied
                && committed.CommitId.Length > 0
                && committed.ReceiptFingerprint.Length == 64,
            "Capacity-routing effect receipt was not committed.");
        Require(outbox.TryCommitEffect(
                    StepOperationId,
                    BatchCommitId,
                    3,
                    3_000L,
                    new string('f', 64)).Status ==
                ProductionCapacityRoutingDrainStatus.Replay,
            "Capacity-routing effect did not replay exactly.");
        Require(outbox.TryCapture(
                    StepOperationId,
                    out ProductionCapacityRoutingDrainSaveData saved)
                && saved.phase == ProductionCapacityRoutingDrainPhase
                    .EffectCommittedAwaitingOwnerAck,
            "Committed capacity-routing receipt was not durable.");
        Require(saved.sourceLines.All(line => line != null
                && string.Equals(
                    line.outputCapabilityFingerprint,
                    ProductionOutputCapabilityDescriptorFingerprint.Capture(
                        line.outputLineId,
                        line.itemId,
                        line.outputCapabilityId,
                        line.outputCapabilityVersion,
                        line.outputComponentCodecId,
                        line.outputComponentCodecVersion),
                    StringComparison.Ordinal)),
            "Committed capacity-routing drain lost exact output capability provenance.");
        Require(string.Equals(
                saved.requestFingerprint,
                ProductionCapacityRoutingDrainFingerprint.CreateRequest(
                    saved.stepOperationId,
                    saved.ownerStableId,
                    saved.facilityId,
                    saved.sourceDestinationId,
                    saved.batchCommitId,
                    saved.sourceOutcomeFingerprint,
                    saved.sourceRoutingFingerprint,
                    saved.sourceOwnershipFingerprint,
                    saved.sourceLines,
                    saved.sourceRoutes,
                    saved.sourceSlices,
                    saved.sourceActorCarries,
                    saved.sourceCustodyStackIds,
                    saved.inputQuantity,
                    saved.inputMassGrams),
                StringComparison.Ordinal),
            "Committed capacity-routing drain request fingerprint drifted in transit.");

        DungeonPhysicalItemSaveData physical = new()
        {
            haulingSettings = new ItemHaulingSettingsSnapshot
            {
                maxCarryMultiplier = 1.5f
            },
            stacks = new List<WorldItemStackSaveData>
            {
                CreateQuiescedActorStack()
            },
            pendingCapacityRoutingDrains = new List<
                ProductionCapacityRoutingDrainSaveData> { stableSaved.Clone() }
        };
        DungeonGameRestoreReport valid =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                physical,
                new EmptyCatalog());
        Require(valid.Success,
            "Canonical capacity-routing save failed validation: "
            + string.Join(" | ", valid.Errors));

        DungeonPhysicalItemSaveData lingeringReservation =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                JsonUtility.ToJson(physical));
        WorldItemStackSaveData lingeringStack =
            lingeringReservation.stacks.Single();
        lingeringReservation.reservationIntents.Add(
            new ItemReservationIntentSaveData
            {
                ownerOperationId = "haul:qa:a",
                ownerCharacterId = "character:qa:a",
                hadActiveItemReservation = true,
                reservationHints = new List<ItemReservationClaimHintSaveData>
                {
                    new()
                    {
                        claimHintId = "claim:qa:capacity:a",
                        originStackId = "source:qa:a",
                        preferredPhysicalStackId = lingeringStack.stackId,
                        itemId = lingeringStack.itemId,
                        expectedStackSignature = ItemReservationSignature.Create(
                            lingeringStack.itemId,
                            lingeringStack.components),
                        quantity = 1,
                        purpose = ItemReservationPurpose.Hauling,
                        aggregationCohortId =
                            "haul:Warehouse:warehouse:qa:a"
                    }
                }
            });
        DungeonGameRestoreReport lingeringReservationReport =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                lingeringReservation,
                new EmptyCatalog());
        Require(!lingeringReservationReport.Success,
            "A released capacity operation retained a reservation intent.");

        DungeonPhysicalItemSaveData physicalTamper =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                JsonUtility.ToJson(physical));
        physicalTamper.stacks[0].gridX++;
        DungeonGameRestoreReport physicalTamperReport =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                physicalTamper,
                new EmptyCatalog());
        Require(!physicalTamperReport.Success,
            "Actor receipt accepted a mismatched physical post-state.");

        DungeonPhysicalItemSaveData missingPhysical =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                JsonUtility.ToJson(physical));
        missingPhysical.stacks.Clear();
        DungeonGameRestoreReport missingPhysicalReport =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                missingPhysical,
                new EmptyCatalog());
        Require(!missingPhysicalReport.Success,
            "Actor receipt accepted a missing physical stack.");

        DungeonPhysicalItemSaveData terminalPhysicalDivergence =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                JsonUtility.ToJson(physical));
        ProductionCapacityRoutingDrainSaveData acknowledgedCapacity =
            saved.Clone();
        acknowledgedCapacity.phase = ProductionCapacityRoutingDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;
        terminalPhysicalDivergence.pendingCapacityRoutingDrains[0] =
            acknowledgedCapacity;
        terminalPhysicalDivergence.pendingProductionCustodyDrains.Add(
            CreateTerminalPhysicalSuccessor(saved));
        terminalPhysicalDivergence.stacks[0].gridX++;
        terminalPhysicalDivergence.stacks[0].destinationId = string.Empty;
        terminalPhysicalDivergence.stacks[0].hasDestinationPosition = false;
        terminalPhysicalDivergence.stacks[0].destinationGridX = 0;
        terminalPhysicalDivergence.stacks[0].destinationGridY = 0;
        terminalPhysicalDivergence.stacks[0].components = new List<
            ItemInstanceComponentSaveData>();
        terminalPhysicalDivergence.stacks.Add(
            CreateReleasedTerminalStackB());
        terminalPhysicalDivergence.stacks = terminalPhysicalDivergence.stacks
            .OrderBy(value => value.gridY)
            .ThenBy(value => value.gridX)
            .ThenBy(value => value.itemId, StringComparer.Ordinal)
            .ThenBy(value => value.stackId, StringComparer.Ordinal)
            .ToList();
        DungeonGameRestoreReport terminalPhysicalReport =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                terminalPhysicalDivergence,
                new EmptyCatalog());
        Require(terminalPhysicalReport.Success,
            "Terminal capacity tombstone incorrectly retained external physical authority: "
            + string.Join(" | ", terminalPhysicalReport.Errors));

        DungeonPhysicalItemSaveData tampered =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                JsonUtility.ToJson(physical));
        tampered.pendingCapacityRoutingDrains[0].preservedMassGrams--;
        DungeonGameRestoreReport invalid =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                tampered,
                new EmptyCatalog());
        Require(!invalid.Success,
            "A one-gram capacity-routing receipt mutation passed validation.");

        string canonicalJson = JsonUtility.ToJson(physical);
        PhysicalItemsSaveSection.ValidateRequiredCurrentSchemaShape(
            canonicalJson);
        string missingCapacityArray = canonicalJson.Replace(
            ",\"pendingCapacityRoutingDrains\":["
                + JsonUtility.ToJson(stableSaved) + "]",
            string.Empty,
            StringComparison.Ordinal);
        bool rejectedMissingArray = false;
        try
        {
            PhysicalItemsSaveSection.ValidateRequiredCurrentSchemaShape(
                missingCapacityArray);
        }
        catch (InvalidOperationException)
        {
            rejectedMissingArray = true;
        }
        Require(rejectedMissingArray,
            "Current physical JSON without the capacity-routing array was accepted.");

        DungeonPhysicalItemSaveData pastVersion =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(canonicalJson);
        pastVersion.version = DungeonPhysicalItemSaveData.CurrentVersion - 1;
        DungeonGameRestoreReport pastVersionReport =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                pastVersion,
                new EmptyCatalog());
        Require(!pastVersionReport.Success,
            "A previous physical payload version was accepted by the current validator.");

        Require(outbox.TryAcknowledge(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && outbox.TryAcknowledge(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionCapacityRoutingDrainStatus.Replay,
            "Capacity-routing acknowledgement was not idempotent.");
        Require(outbox.TryGarbageCollect(
                    StepOperationId,
                    committed.ReceiptFingerprint).Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && !outbox.TryCapture(StepOperationId, out _),
            "Acknowledged capacity-routing producer was not garbage-collected.");

        VerifyCheckpointGcTransaction();
    }

    private static void VerifyCheckpointGcTransaction()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        ProductionCapacityRoutingDrainOutbox outbox = new(repository);
        IProductionCapacityRoutingDrainCheckpointGcOutbox gc = outbox;
        ProductionCapacityRoutingDrainSaveData rowA =
            CreateTerminalCheckpointRow(outbox, "a");
        ProductionCapacityRoutingDrainSaveData rowB =
            CreateTerminalCheckpointRow(outbox, "b");
        ProductionCapacityRoutingDrainSaveData rowC =
            CreateTerminalCheckpointRow(outbox, "c");
        string rowAJson = JsonUtility.ToJson(rowA);
        string rowCJson = JsonUtility.ToJson(rowC);
        int preparedItemRevision = repository.ItemStackVersion;
        int preparedHaulRevision = repository.HaulJobVersion;

        Require(gc.TryPrepareCheckpointGarbageCollection(
                    new[] { rowB, rowA },
                    out IProductionCapacityRoutingDrainCheckpointGcCandidate
                        partialCandidate,
                    out string prepareFailure)
                && outbox.TryCapture(rowA.stepOperationId, out var preparedA)
                && string.Equals(
                    JsonUtility.ToJson(preparedA),
                    rowAJson,
                    StringComparison.Ordinal)
                && repository.ItemStackVersion == preparedItemRevision
                && repository.HaulJobVersion == preparedHaulRevision,
            "V27_CAPACITY_CHECKPOINT_GC_PREPARE_MUTATED:" + prepareFailure);
        Require(!gc.TryPrepareCheckpointGarbageCollection(
                new[] { rowC }, out _, out _),
            "V27_CAPACITY_CHECKPOINT_GC_ACCEPTED_SECOND_ACTIVE_CANDIDATE");
        Require(outbox.TryGarbageCollect(
                    rowA.stepOperationId,
                    rowA.receiptFingerprint).Status ==
                ProductionCapacityRoutingDrainStatus.Deferred,
            "V27_CAPACITY_CHECKPOINT_GC_DID_NOT_FENCE_LEGACY_GC");

        Require(RemovePendingForFault(
                repository,
                "RemovePendingCapacityRoutingDrain",
                rowB.stepOperationId),
            "V27_CAPACITY_CHECKPOINT_GC_FAULT_INJECTION_FAILED");
        int faultItemRevision = repository.ItemStackVersion;
        int faultHaulRevision = repository.HaulJobVersion;
        Require(!gc.TryPublishCheckpointGarbageCollection(
                    partialCandidate,
                    out _)
                && outbox.TryCapture(
                    rowA.stepOperationId,
                    out ProductionCapacityRoutingDrainSaveData autoRestoredA)
                && string.Equals(
                    JsonUtility.ToJson(autoRestoredA),
                    rowAJson,
                    StringComparison.Ordinal),
            "V27_CAPACITY_CHECKPOINT_GC_MIDDLE_FAILURE_NOT_OBSERVED");
        gc.RollbackCheckpointGarbageCollection(partialCandidate);
        bool hasRestoredA = outbox.TryCapture(
            rowA.stepOperationId,
            out ProductionCapacityRoutingDrainSaveData restoredA);
        bool hasPreservedC = outbox.TryCapture(
            rowC.stepOperationId,
            out ProductionCapacityRoutingDrainSaveData preservedC);
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
            "V27_CAPACITY_CHECKPOINT_GC_PARTIAL_ROLLBACK_NOT_EXACT");
        gc.CompleteCheckpointGarbageCollection(partialCandidate);
        RequireThrows(() => gc.TryPublishCheckpointGarbageCollection(
                partialCandidate,
                out _),
            "V27_CAPACITY_CHECKPOINT_GC_COMPLETED_CANDIDATE_REUSED");

        int exactItemRevision = repository.ItemStackVersion;
        int exactHaulRevision = repository.HaulJobVersion;
        Require(gc.TryPrepareCheckpointGarbageCollection(
                    new[] { restoredA },
                    out IProductionCapacityRoutingDrainCheckpointGcCandidate
                        exactCandidate,
                    out _)
                && gc.TryPublishCheckpointGarbageCollection(
                    exactCandidate,
                    out _)
                && !outbox.TryCapture(rowA.stepOperationId, out _)
                && repository.ItemStackVersion == exactItemRevision
                && repository.HaulJobVersion == exactHaulRevision,
            "V27_CAPACITY_CHECKPOINT_GC_PUBLISH_NOT_EXACT");
        gc.RollbackCheckpointGarbageCollection(exactCandidate);
        Require(outbox.TryCapture(rowA.stepOperationId, out var exactRestored)
                && string.Equals(
                    JsonUtility.ToJson(exactRestored),
                    rowAJson,
                    StringComparison.Ordinal)
                && repository.ItemStackVersion == exactItemRevision
                && repository.HaulJobVersion == exactHaulRevision,
            "V27_CAPACITY_CHECKPOINT_GC_ROLLBACK_CHANGED_REVISIONS");
        gc.CompleteCheckpointGarbageCollection(exactCandidate);

        Require(gc.TryPrepareCheckpointGarbageCollection(
                    new[] { preservedC },
                    out IProductionCapacityRoutingDrainCheckpointGcCandidate
                        completedCandidate,
                    out _)
                && gc.TryPublishCheckpointGarbageCollection(
                    completedCandidate,
                    out _),
            "V27_CAPACITY_CHECKPOINT_GC_FINAL_PUBLISH_FAILED");
        gc.CompleteCheckpointGarbageCollection(completedCandidate);
        Require(!outbox.TryCapture(rowC.stepOperationId, out _),
            "V27_CAPACITY_CHECKPOINT_GC_COMPLETE_RETAINED_ROW");
    }

    private static ProductionCapacityRoutingDrainSaveData
        CreateTerminalCheckpointRow(
            ProductionCapacityRoutingDrainOutbox outbox,
            string suffix)
    {
        ProductionCapacityRoutingDrainRequest request =
            CreateCheckpointRequest(suffix);
        string step = request.StepOperationId;
        string route = "route:qa:checkpoint:" + suffix;
        string stack = "stack:qa:checkpoint:" + suffix;
        Require(outbox.TryPrepare(request).Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && outbox.TryBeginRouting(step, request.RequestFingerprint).Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && outbox.TryRecordLineRouted(
                step,
                "line:qa:checkpoint:" + suffix).Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && outbox.TryBeginQuiescingActors(
                step,
                new[] { route },
                new[] { stack }).Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && outbox.TryBeginReleasingOperationAuthority(step).Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && outbox.TryBeginAwaitingStablePhysicalState(step).Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && outbox.TryRecordStablePhysicalStack(step, stack).Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && outbox.TryBeginAwaitingDurableCheckpointGc(step).Status ==
                ProductionCapacityRoutingDrainStatus.Applied,
            "V27_CAPACITY_CHECKPOINT_GC_TERMINAL_SETUP_FAILED:" + suffix);
        Require(outbox.TryCapture(
                step,
                out ProductionCapacityRoutingDrainSaveData awaiting),
            "V27_CAPACITY_CHECKPOINT_GC_AWAITING_ROW_MISSING:" + suffix);
        ProductionCapacityRoutingDrainResult committed = outbox.TryCommitEffect(
            step,
            request.BatchCommitId,
            1,
            1_000L,
            ProductionCapacityRoutingDrainFingerprint.CreateResultFingerprint(
                awaiting));
        ProductionCapacityRoutingDrainResult acknowledged =
            outbox.TryAcknowledge(step, committed.ReceiptFingerprint);
        bool hasTerminal = outbox.TryCapture(
            step,
            out ProductionCapacityRoutingDrainSaveData terminal);
        Require(committed.Status == ProductionCapacityRoutingDrainStatus.Applied
            && acknowledged.Status ==
                ProductionCapacityRoutingDrainStatus.Applied
            && hasTerminal,
            "V27_CAPACITY_CHECKPOINT_GC_ACK_SETUP_FAILED:" + suffix);
        return terminal;
    }

    private static ProductionCapacityRoutingDrainRequest CreateCheckpointRequest(
        string suffix)
    {
        string step = StepOperationId + ":checkpoint:" + suffix;
        string batch = BatchCommitId + ":checkpoint:" + suffix;
        string facility = FacilityId + ":checkpoint:" + suffix;
        string destination = DestinationId + ":checkpoint:" + suffix;
        string lineId = "line:qa:checkpoint:" + suffix;
        string outputId = "output:qa:checkpoint:" + suffix;
        string itemId = "item:qa:checkpoint:" + suffix;
        ProductionCapacityRoutingDrainLineSaveData[] lines =
        {
            new()
            {
                lineCommitId = lineId,
                outputLineId = outputId,
                itemId = itemId,
                componentFingerprint = new string('a', 64),
                outputCapabilityId =
                    ProductionOutputCapabilityIds.StandardDefinition,
                outputCapabilityVersion =
                    ProductionOutputCapabilityIds.StandardDefinitionVersion,
                outputComponentCodecId =
                    ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                outputComponentCodecVersion =
                    ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
                outputCapabilityFingerprint =
                    ProductionOutputCapabilityDescriptorFingerprint.Capture(
                        outputId,
                        itemId,
                        ProductionOutputCapabilityIds.StandardDefinition,
                        ProductionOutputCapabilityIds.StandardDefinitionVersion,
                        ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                        ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
                originalQuantity = 1,
                originalMassGrams = 1_000L,
                routedQuantity = 1,
                routedMassGrams = 1_000L
            }
        };
        string[] custodyStacks = { "stack:qa:checkpoint:" + suffix };
        string owner = "routing-batch:" + batch;
        string requestFingerprint =
            ProductionCapacityRoutingDrainFingerprint.CreateRequest(
                step,
                owner,
                facility,
                destination,
                batch,
                new string('1', 64),
                new string('2', 64),
                new string('3', 64),
                lines,
                Array.Empty<ProductionCapacityRoutingDrainRouteSaveData>(),
                Array.Empty<ProductionCapacityRoutingDrainSliceSaveData>(),
                Array.Empty<ProductionCapacityRoutingDrainActorCarrySaveData>(),
                custodyStacks,
                1,
                1_000L);
        return new ProductionCapacityRoutingDrainRequest(
            step,
            owner,
            facility,
            destination,
            batch,
            new string('1', 64),
            new string('2', 64),
            new string('3', 64),
            lines,
            Array.Empty<ProductionCapacityRoutingDrainRouteSaveData>(),
            Array.Empty<ProductionCapacityRoutingDrainSliceSaveData>(),
            Array.Empty<ProductionCapacityRoutingDrainActorCarrySaveData>(),
            custodyStacks,
            1,
            1_000L,
            requestFingerprint);
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

    private static ProductionCapacityRoutingDrainRequest CreateRequest(
        string facilityId = FacilityId,
        bool tamperCapabilityFingerprint = false)
    {
        WorldItemStackSaveData stableActorStack = CreateQuiescedActorStack();
        ProductionCapacityRoutingDrainLineSaveData[] lines =
        {
            new()
            {
                lineCommitId = "line:qa:a",
                outputLineId = "output:qa:a",
                itemId = "item:qa:a",
                componentFingerprint = new string('a', 64),
                outputCapabilityId = ProductionOutputCapabilityIds.StandardDefinition,
                outputCapabilityVersion =
                    ProductionOutputCapabilityIds.StandardDefinitionVersion,
                outputComponentCodecId =
                    ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                outputComponentCodecVersion =
                    ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
                outputCapabilityFingerprint =
                    ProductionOutputCapabilityDescriptorFingerprint.Capture(
                        "output:qa:a",
                        "item:qa:a",
                        ProductionOutputCapabilityIds.StandardDefinition,
                        ProductionOutputCapabilityIds.StandardDefinitionVersion,
                        ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                        ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
                originalQuantity = 1,
                originalMassGrams = 1_000L,
                routedQuantity = 1,
                routedMassGrams = 1_000L
            },
            new()
            {
                lineCommitId = "line:qa:b",
                outputLineId = "output:qa:b",
                itemId = "item:qa:b",
                componentFingerprint = new string('b', 64),
                outputCapabilityId = ProductionOutputCapabilityIds.StandardDefinition,
                outputCapabilityVersion =
                    ProductionOutputCapabilityIds.StandardDefinitionVersion,
                outputComponentCodecId =
                    ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                outputComponentCodecVersion =
                    ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
                outputCapabilityFingerprint =
                    ProductionOutputCapabilityDescriptorFingerprint.Capture(
                        "output:qa:b",
                        "item:qa:b",
                        ProductionOutputCapabilityIds.StandardDefinition,
                        ProductionOutputCapabilityIds.StandardDefinitionVersion,
                        ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                        ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
                originalQuantity = 2,
                originalMassGrams = 2_000L,
                remainingQuantity = 2,
                remainingMassGrams = 2_000L
            }
        };
        if (tamperCapabilityFingerprint)
            lines[0].outputCapabilityFingerprint = new string('9', 64);
        ProductionCapacityRoutingDrainRouteSaveData[] routes =
        {
            new()
            {
                routeOperationId = "route:qa:a",
                requestFingerprint = new string('c', 64),
                physicalReceiptFingerprint = new string('d', 64),
                phase = 3,
                currentDeliveryRevision = 0,
                currentDeliveryRevisionFingerprint = new string('e', 64),
                currentTargetDestinationId = "warehouse:qa:a"
            }
        };
        ProductionCapacityRoutingDrainSliceSaveData[] slices =
        {
            new()
            {
                routeOperationId = "route:qa:a",
                sourceStackId = "source:qa:a",
                routedStackId = "stack:qa:a",
                outputLineId = "output:qa:a",
                lineCommitId = "line:qa:a",
                itemId = "item:qa:a",
                routedQuantity = 1,
                routedMassGrams = 1_000L,
                componentFingerprint = new string('a', 64)
            }
        };
        ProductionCapacityRoutingDrainActorCarrySaveData[] carries =
        {
            new()
            {
                actorPersistentId = "character:qa:a",
                haulIntentOperationId = "haul:qa:a",
                routeOperationId = "route:qa:a",
                carriedStackId = "stack:qa:a",
                sourceStackId = "source:qa:a",
                quantity = 1,
                massGrams = 1_000L,
                stackSignature = ProductionCapacityRoutingDrainFingerprint
                    .CreateActorCarryStackSignature(
                        stableActorStack.itemId,
                        stableActorStack.itemInstanceId,
                        stableActorStack.components)
            }
        };
        string[] custodyStacks = { "stack:qa:a", "stack:qa:b" };
        string requestFingerprint =
            ProductionCapacityRoutingDrainFingerprint.CreateRequest(
                StepOperationId,
                OwnerStableId,
                facilityId,
                DestinationId,
                BatchCommitId,
                new string('1', 64),
                new string('2', 64),
                new string('3', 64),
                lines,
                routes,
                slices,
                carries,
                custodyStacks,
                3,
                3_000L);
        return new ProductionCapacityRoutingDrainRequest(
            StepOperationId,
            OwnerStableId,
            facilityId,
            DestinationId,
            BatchCommitId,
            new string('1', 64),
            new string('2', 64),
            new string('3', 64),
            lines,
            routes,
            slices,
            carries,
            custodyStacks,
            3,
            3_000L,
            requestFingerprint);
    }

    private static void RequireCurrentDrainRejectedAsUnstable(
        ProductionCapacityRoutingDrainOutbox outbox,
        string message)
    {
        Require(outbox.TryCapture(
                StepOperationId,
                out ProductionCapacityRoutingDrainSaveData current),
            "Capacity-routing transition fixture could not capture its current drain.");
        DungeonPhysicalItemSaveData snapshot = new()
        {
            haulingSettings = new ItemHaulingSettingsSnapshot
            {
                maxCarryMultiplier = 1.5f
            },
            pendingCapacityRoutingDrains = new List<
                ProductionCapacityRoutingDrainSaveData> { current }
        };
        DungeonGameRestoreReport validation =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                snapshot,
                new EmptyCatalog());
        Require(!validation.Success, message);
    }

    private static ProductionCapacityRoutingActorQuiesceReceiptSaveData
        CreateActorReceipt(
            ProductionCapacityRoutingDrainRequest request,
            string actorPersistentId,
            IEnumerable<string> rowKeys)
    {
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt = new()
        {
            actorPersistentId = actorPersistentId,
            batchCommitId = request.BatchCommitId,
            physicalCellX = 4,
            physicalCellY = 5,
            carriedRowKeys = (rowKeys ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            quantityLeaseIds = new List<string> { "item-lease:qa:a" },
            warehouseAdmissionTokenIds = new List<string>(),
            activePlanFingerprint = new string('6', 64),
            prePhysicalFingerprint = new string('7', 64),
            postPhysicalFingerprint =
                ProductionCapacityRoutingActorPhysicalFingerprint
                    .CreateEditorTest(new[] { CreateQuiescedActorStack() })
        };
        receipt.receiptFingerprint =
            ProductionCapacityRoutingDrainFingerprint
                .CreateActorQuiesceReceiptFingerprint(
                    request.StepOperationId,
                    request.RequestFingerprint,
                    receipt);
        return receipt;
    }

    private static ProductionCapacityRoutingActorAuthorityReleaseSaveData
        CreateAuthorityReleasePlan(
            ProductionCapacityRoutingDrainRequest request,
            ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt)
    {
        ProductionCapacityRoutingActorAuthorityReleaseSaveData plan = new()
        {
            actorPersistentId = receipt.actorPersistentId,
            actorQuiesceReceiptFingerprint = receipt.receiptFingerprint,
            operationIds = new List<string> { "haul:qa:a" },
            operations = new List<
                ProductionCapacityRoutingOperationAuthorityRowSaveData>
            {
                new()
                {
                    operationId = "haul:qa:a",
                    quantityLeaseIds = receipt.quantityLeaseIds.ToList(),
                    warehouseAdmissionTokenIds =
                        receipt.warehouseAdmissionTokenIds.ToList(),
                    haulIntentFingerprint = new string('9', 64)
                }
            },
            activePlanFingerprint = receipt.activePlanFingerprint
        };
        plan.planFingerprint = ProductionCapacityRoutingDrainFingerprint
            .CreateActorAuthorityReleasePlanFingerprint(
                request.StepOperationId,
                request.RequestFingerprint,
                plan);
        return plan;
    }

    private static WorldItemStackSaveData CreateQuiescedActorStack()
    {
        string itemId = "item:qa:a";
        string componentSignature = ItemStackSignature.Create(
            itemId,
            Array.Empty<ItemInstanceComponentSaveData>());
        ItemInstanceComponentSaveData custody =
            FacilityOutputExactRouteEditorTestFactory.CreateRoutableCustody(
                BatchCommitId,
                new string('1', 64),
                new string('2', 64),
                "output:qa:a",
                "line:qa:a",
                originalStackOrdinal: 0,
                originalBatchStackCount: 1,
                originalBatchQuantity: 1,
                originalBatchMassGrams: 1_000L,
                itemId,
                componentSignature,
                new string('a', 64),
                DestinationId,
                "warehouse:qa:a",
                "source:qa:a",
                "source:qa:a",
                new Vector2Int(0, 0),
                sourceOffsetQuantity: 0,
                quantity: 1,
                massGrams: 1_000L,
                routeOperationId: "route:qa:a",
                requestFingerprint: new string('c', 64),
                physicalReceiptFingerprint: new string('d', 64),
                currentDeliveryRevision: 0L,
                currentDeliveryRevisionFingerprint: new string('e', 64),
                currentTargetDestinationId: "warehouse:qa:a",
                currentTargetPosition: new Vector2Int(9, 9));
        return new WorldItemStackSaveData
        {
            stackId = "stack:qa:a",
            itemId = itemId,
            quantity = 1,
            state = WorldItemStackState.Loose,
            gridX = 4,
            gridY = 5,
            destinationId = "warehouse:qa:a",
            hasDestinationPosition = true,
            destinationGridX = 9,
            destinationGridY = 9,
            components = new List<ItemInstanceComponentSaveData> { custody }
        };
    }

    private static ProductionPhysicalCustodyDrainSaveData
        CreateTerminalPhysicalSuccessor(
            ProductionCapacityRoutingDrainSaveData capacity)
    {
        string ownerStableId = "physical-destination:"
            + capacity.sourceDestinationId;
        string stepOperationId =
            "production-physical-successor:qa:capacity-routing";
        string[] sourceStacks = (capacity.preservedStackIds
                ?? new List<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] sourceActors = (capacity.sourceActorCarries
                ?? new List<ProductionCapacityRoutingDrainActorCarrySaveData>())
            .Where(value => value != null)
            .Select(value => value.actorPersistentId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] sourceIntents = (capacity.sourceActorCarries
                ?? new List<ProductionCapacityRoutingDrainActorCarrySaveData>())
            .Where(value => value != null)
            .Select(value => value.haulIntentOperationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string sourceOwnershipFingerprint = new string('4', 64);
        string requestFingerprint =
            ProductionPhysicalCustodyDrainFingerprint.CreateRequest(
                stepOperationId,
                ownerStableId,
                capacity.sourceDestinationId,
                4,
                5,
                sourceOwnershipFingerprint,
                sourceStacks,
                sourceActors,
                sourceIntents,
                capacity.preservedQuantity,
                capacity.preservedMassGrams);
        return new ProductionPhysicalCustodyDrainSaveData
        {
            stepOperationId = stepOperationId,
            ownerStableId = ownerStableId,
            sourceDestinationId = capacity.sourceDestinationId,
            ownerGridX = 4,
            ownerGridY = 5,
            requestFingerprint = requestFingerprint,
            sourceOwnershipFingerprint = sourceOwnershipFingerprint,
            phase = ProductionPhysicalCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc,
            sourceStackIds = sourceStacks.ToList(),
            sourceActorIds = sourceActors.ToList(),
            sourceHaulIntentOperationIds = sourceIntents.ToList(),
            completedActorIds = sourceActors.ToList(),
            releasedHaulIntentOperationIds = sourceIntents.ToList(),
            releasedStackIds = sourceStacks.ToList(),
            inputQuantity = capacity.preservedQuantity,
            inputMassGrams = capacity.preservedMassGrams,
            releasedQuantity = capacity.preservedQuantity,
            releasedMassGrams = capacity.preservedMassGrams,
            resultFingerprint = new string('5', 64),
            commitId = "commit:qa:physical-successor",
            receiptFingerprint = new string('6', 64)
        };
    }

    private static WorldItemStackSaveData CreateReleasedTerminalStackB() =>
        new()
        {
            stackId = "stack:qa:b",
            itemId = "item:qa:b",
            quantity = 2,
            state = WorldItemStackState.Loose,
            gridX = 4,
            gridY = 5,
            destinationId = string.Empty,
            sourceStorageDestinationId = string.Empty,
            hasDestinationPosition = false,
            components = new List<ItemInstanceComponentSaveData>()
        };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class EmptyCatalog : IDungeonItemCatalogProvider
    {
        private readonly DungeonItemDefinition[] definitions =
        {
            new(
                "item:qa:a",
                "QA Capacity Item A",
                "Capacity-routing save validation fixture item A.",
                StockCategory.General,
                1,
                null,
                1f,
                75),
            new(
                "item:qa:b",
                "QA Capacity Item B",
                "Capacity-routing save validation fixture item B.",
                StockCategory.General,
                1,
                null,
                1f,
                75)
        };

        public IReadOnlyList<DungeonItemDefinition> All =>
            definitions;

        public DungeonItemDefinition GetDefinition(string itemId) =>
            definitions.FirstOrDefault(value => string.Equals(
                itemId,
                value.ItemId,
                StringComparison.Ordinal))
            ?? throw new KeyNotFoundException(itemId);

        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition definition)
        {
            definition = definitions.FirstOrDefault(value => string.Equals(
                itemId,
                value.ItemId,
                StringComparison.Ordinal));
            return definition != null;
        }
    }
}
#endif
