#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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
