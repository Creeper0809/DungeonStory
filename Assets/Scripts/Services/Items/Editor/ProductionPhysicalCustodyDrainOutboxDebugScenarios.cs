#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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
