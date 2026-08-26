#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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
                ProductionCapacityRoutingDrainSaveData> { saved.Clone() }
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
                + JsonUtility.ToJson(saved) + "]",
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
            "V16 physical JSON without capacity-routing array was accepted.");

        DungeonPhysicalItemSaveData pastVersion =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(canonicalJson);
        pastVersion.version = 15;
        DungeonGameRestoreReport pastVersionReport =
            ProductionPhysicalCustodyDrainSaveValidationProbe.Validate(
                pastVersion,
                new EmptyCatalog());
        Require(!pastVersionReport.Success,
            "Physical V15 payload was accepted by the V16 validator.");

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
    }

    private static ProductionCapacityRoutingDrainRequest CreateRequest(
        string facilityId = FacilityId)
    {
        ProductionCapacityRoutingDrainLineSaveData[] lines =
        {
            new()
            {
                lineCommitId = "line:qa:a",
                outputLineId = "output:qa:a",
                itemId = "item:qa:a",
                componentFingerprint = new string('a', 64),
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
                originalQuantity = 2,
                originalMassGrams = 2_000L,
                remainingQuantity = 2,
                remainingMassGrams = 2_000L
            }
        };
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
                sourceStackId = "stack:qa:a",
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
                        "item:qa:a",
                        string.Empty,
                        Array.Empty<ItemInstanceComponentSaveData>())
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class EmptyCatalog : IDungeonItemCatalogProvider
    {
        private readonly DungeonItemDefinition definition = new(
            "item:qa:a",
            "QA Capacity Item",
            "Capacity-routing save validation fixture item.",
            StockCategory.General,
            1,
            null,
            1f,
            75);

        public IReadOnlyList<DungeonItemDefinition> All =>
            new[] { definition };

        public DungeonItemDefinition GetDefinition(string itemId) =>
            string.Equals(itemId, definition.ItemId, StringComparison.Ordinal)
                ? definition
                : throw new KeyNotFoundException(itemId);

        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition definition)
        {
            definition = string.Equals(
                    itemId,
                    this.definition.ItemId,
                    StringComparison.Ordinal)
                ? this.definition
                : null;
            return definition != null;
        }
    }
}
#endif
