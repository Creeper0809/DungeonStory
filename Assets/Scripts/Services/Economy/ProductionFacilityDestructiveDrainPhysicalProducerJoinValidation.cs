using System;
using System.Collections.Generic;
using System.Linq;

internal static class ProductionFacilityDestructiveDrainPhysicalProducerJoinValidation
{
    internal static void ValidateCapacityRoutingActorAuthorityDisjoint(
        IReadOnlyList<ProductionCapacityRoutingDrainSaveData> producers,
        DungeonCharacterWorldSaveData characters)
    {
        foreach (ProductionCapacityRoutingDrainSaveData producer in
                 producers ?? Array.Empty<ProductionCapacityRoutingDrainSaveData>())
        {
            if (producer == null)
                continue;
            if (producer.phase is ProductionCapacityRoutingDrainPhase
                    .QuiescingActors
                or ProductionCapacityRoutingDrainPhase
                    .ReleasingOperationAuthority)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-transient-save-phase: "
                    + producer.stepOperationId);
            }
            if (producer.phase < ProductionCapacityRoutingDrainPhase
                    .AwaitingStablePhysicalState)
            {
                continue;
            }
            HashSet<string> releasedOperations = producer
                .actorAuthorityReleases
                .Where(value => value != null && value.effectsCommitted)
                .SelectMany(value => value.operationIds ?? new List<string>())
                .ToHashSet(StringComparer.Ordinal);
            if (releasedOperations.Count == 0)
                continue;
            foreach (DungeonCharacterSaveData actor in characters?.actors
                         ?? new List<DungeonCharacterSaveData>())
            {
                string characterId = actor?.persistentId ?? string.Empty;
                if (actor?.haulDeliveryIntent != null
                    && releasedOperations.Contains(
                        actor.haulDeliveryIntent.operationId)
                    || actor?.carryInventory?.items?.Any(item => item != null
                        && item.quantity > 0
                        && releasedOperations.Contains(
                            item.ownerOperationId)) == true)
                {
                    throw new InvalidOperationException(
                        "production-destructive-drain-released-character-authority-live: "
                        + producer.stepOperationId + ":" + characterId);
                }
            }
        }
    }

    internal static void ValidatePhysicalCustodyProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<ProductionPhysicalCustodyDrainSaveData> producers,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            entry.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .PhysicalCustodyCarryRecovery,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
                ?? throw new InvalidOperationException(
                    "Physical destructive-drain participant has no owner collection.");
        if (owners.Count > 1)
        {
            throw new InvalidOperationException(
                "Physical destructive-drain participant must use one atomic destination owner.");
        }
        if (owners.Count == 0)
            return;

        ProductionFacilityDestructiveDrainOwnerSaveData owner = owners[0]
            ?? throw new InvalidOperationException(
                "Physical destructive-drain owner is null.");
        ProductionPhysicalCustodyDrainSaveData[] matches = producers
            .Where(value => value != null
                && string.Equals(
                    value.stepOperationId,
                    owner.stepOperationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length == 0)
        {
            if (owner.phase == ProductionFacilityDestructiveDrainStepPhase.Planned)
                return;
            throw new InvalidOperationException(
                "production-destructive-drain-physical-producer-missing: "
                + owner.stepOperationId);
        }
        if (matches.Length != 1 || !joinedSteps.Add(owner.stepOperationId))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-physical-producer-duplicate: "
                + owner.stepOperationId);
        }

        ProductionPhysicalCustodyDrainSaveData producer = matches[0];
        if (!string.Equals(
                producer.ownerStableId,
                owner.ownerStableId,
                StringComparison.Ordinal)
            || !string.Equals(
                producer.sourceDestinationId,
                entry.destinationId,
                StringComparison.Ordinal)
            || !string.Equals(
                producer.requestFingerprint,
                owner.requestFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                producer.sourceOwnershipFingerprint,
                participant.preparedContributionFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-physical-producer-request-mismatch: "
                + owner.stepOperationId);
        }

        bool phaseMatches = owner.phase switch
        {
            ProductionFacilityDestructiveDrainStepPhase.Planned =>
                producer.phase != ProductionPhysicalCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
            ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck =>
                producer.phase is ProductionPhysicalCustodyDrainPhase
                        .EffectCommittedAwaitingOwnerAck
                    or ProductionPhysicalCustodyDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc,
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                producer.phase == ProductionPhysicalCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
            _ => false
        };
        if (!phaseMatches)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-physical-producer-phase-mismatch: "
                + owner.stepOperationId);
        }
        if (owner.phase != ProductionFacilityDestructiveDrainStepPhase.Planned
            && (!string.Equals(
                    owner.commitId,
                    producer.commitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.receiptFingerprint,
                    producer.receiptFingerprint,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-physical-producer-receipt-mismatch: "
                + owner.stepOperationId);
        }
    }

    internal static void ValidateCapacityRoutingProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<ProductionCapacityRoutingDrainSaveData> producers,
        ProductionPreparedOutputRoutingSaveData routing,
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> physicalRoutes,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            entry.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
                ?? throw new InvalidOperationException(
                    "Capacity destructive-drain participant has no owner collection.");
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in owners)
        {
            if (owner == null)
                throw new InvalidOperationException(
                    "Capacity destructive-drain owner is null.");
            ProductionCapacityRoutingDrainSaveData[] matches = producers
                .Where(value => value != null
                    && string.Equals(
                        value.stepOperationId,
                        owner.stepOperationId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                if (owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase.Planned)
                {
                    continue;
                }
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-producer-missing: "
                    + owner.stepOperationId);
            }
            if (matches.Length != 1 || !joinedSteps.Add(owner.stepOperationId))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-producer-duplicate: "
                    + owner.stepOperationId);
            }

            ProductionCapacityRoutingDrainSaveData producer = matches[0];
            if (producer.phase is ProductionCapacityRoutingDrainPhase
                    .QuiescingActors
                or ProductionCapacityRoutingDrainPhase
                    .ReleasingOperationAuthority)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-transient-save-phase: "
                    + producer.stepOperationId);
            }
            string expectedOwner =
                ProductionFacilityDestructiveDrainOwnerStableIds.RoutingBatch(
                    producer.batchCommitId);
            if (!string.Equals(
                    producer.ownerStableId,
                    owner.ownerStableId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.ownerStableId,
                    expectedOwner,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.facilityId,
                    entry.facilityId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.sourceDestinationId,
                    entry.destinationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.requestFingerprint,
                    owner.requestFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.sourceOwnershipFingerprint,
                    participant.preparedContributionFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-producer-request-mismatch: "
                    + owner.stepOperationId);
            }

            bool phaseMatches = owner.phase switch
            {
                ProductionFacilityDestructiveDrainStepPhase.Planned =>
                    producer.phase != ProductionCapacityRoutingDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck =>
                    producer.phase is ProductionCapacityRoutingDrainPhase
                            .EffectCommittedAwaitingOwnerAck
                        or ProductionCapacityRoutingDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                    producer.phase == ProductionCapacityRoutingDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc,
                _ => false
            };
            if (!phaseMatches)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-producer-phase-mismatch: "
                    + owner.stepOperationId);
            }
            if (owner.phase != ProductionFacilityDestructiveDrainStepPhase.Planned
                && (!string.Equals(
                        owner.commitId,
                        producer.commitId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        owner.receiptFingerprint,
                        producer.receiptFingerprint,
                        StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-producer-receipt-mismatch: "
                    + owner.stepOperationId);
            }
            ValidateCapacityRoutingProducerSource(
                producer,
                routing,
                physicalRoutes);
        }
    }


    private static void ValidateCapacityRoutingProducerSource(
        ProductionCapacityRoutingDrainSaveData producer,
        ProductionPreparedOutputRoutingSaveData routing,
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> physicalRoutes)
    {
        ProductionPreparedOutputRoutingBatchSaveData[] batchMatches =
            (routing?.batches
                ?? new List<ProductionPreparedOutputRoutingBatchSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.batchCommitId,
                    producer.batchCommitId,
                    StringComparison.Ordinal))
            .ToArray();
        FacilityOutputExactRouteOutboxSaveData[] itemRoutes =
            (physicalRoutes ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.batchCommitId,
                    producer.batchCommitId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.routeOperationId, StringComparer.Ordinal)
            .ToArray();
        bool terminal = producer.phase is ProductionCapacityRoutingDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionCapacityRoutingDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;
        bool mayObserveCheckpointGap = producer.phase ==
            ProductionCapacityRoutingDrainPhase.AwaitingDurableCheckpointGc;
        if (batchMatches.Length == 0)
        {
            if ((!terminal && !mayObserveCheckpointGap)
                || itemRoutes.Length != 0)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-source-missing-one-sided: "
                    + producer.stepOperationId);
            }
            return;
        }
        if (batchMatches.Length != 1 || terminal)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-capacity-source-terminal-or-duplicate: "
                + producer.stepOperationId);
        }

        ProductionPreparedOutputRoutingBatchSaveData batch = batchMatches[0];
        if (!string.Equals(batch.ownerFacilityId, producer.facilityId,
                StringComparison.Ordinal)
            || !string.Equals(batch.destinationId, producer.sourceDestinationId,
                StringComparison.Ordinal)
            || !string.Equals(batch.outcomeFingerprint,
                producer.sourceOutcomeFingerprint, StringComparison.Ordinal)
            || !string.Equals(batch.routingFingerprint,
                producer.sourceRoutingFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-capacity-source-batch-mismatch: "
                + producer.stepOperationId);
        }

        Dictionary<string, ProductionPreparedOutputRoutingLineSaveData> lines =
            (batch.lines ?? new List<ProductionPreparedOutputRoutingLineSaveData>())
            .Where(value => value != null)
            .ToDictionary(value => value.lineCommitId, StringComparer.Ordinal);
        foreach (ProductionCapacityRoutingDrainLineSaveData frozen in
                 producer.sourceLines)
        {
            if (!lines.TryGetValue(
                    frozen.lineCommitId,
                    out ProductionPreparedOutputRoutingLineSaveData live)
                || !string.Equals(live.outputLineId, frozen.outputLineId,
                    StringComparison.Ordinal)
                || !string.Equals(live.itemId, frozen.itemId,
                    StringComparison.Ordinal)
                || !string.Equals(live.componentFingerprint,
                    frozen.componentFingerprint, StringComparison.Ordinal)
                || !string.Equals(live.outputCapabilityId,
                    frozen.outputCapabilityId, StringComparison.Ordinal)
                || live.outputCapabilityVersion !=
                    frozen.outputCapabilityVersion
                || !string.Equals(live.outputComponentCodecId,
                    frozen.outputComponentCodecId, StringComparison.Ordinal)
                || live.outputComponentCodecVersion !=
                    frozen.outputComponentCodecVersion
                || !string.Equals(live.outputCapabilityFingerprint,
                    frozen.outputCapabilityFingerprint,
                    StringComparison.Ordinal)
                || live.originalQuantity != frozen.originalQuantity
                || live.originalMassGrams != frozen.originalMassGrams
                || live.remainingQuantity > frozen.remainingQuantity
                || live.remainingMassGrams > frozen.remainingMassGrams
                || live.routedQuantity < frozen.routedQuantity
                || live.routedMassGrams < frozen.routedMassGrams)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-source-line-mismatch: "
                    + producer.stepOperationId + ":" + frozen.lineCommitId);
            }
        }

        Dictionary<string, ProductionPreparedOutputRouteOperationSaveData>
            operations = lines.Values
                .SelectMany(line => line.routeOperations
                    ?? new List<ProductionPreparedOutputRouteOperationSaveData>())
                .Where(value => value != null)
                .ToDictionary(value => value.routeOperationId,
                    StringComparer.Ordinal);
        foreach (ProductionCapacityRoutingDrainRouteSaveData frozen in
                 producer.sourceRoutes)
        {
            if (!operations.TryGetValue(
                    frozen.routeOperationId,
                    out ProductionPreparedOutputRouteOperationSaveData live)
                || !string.Equals(live.requestFingerprint,
                    frozen.requestFingerprint, StringComparison.Ordinal)
                || (int)live.phase < frozen.phase
                || !string.IsNullOrEmpty(frozen.physicalReceiptFingerprint)
                    && !string.Equals(live.physicalReceiptFingerprint,
                        frozen.physicalReceiptFingerprint,
                        StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-source-route-mismatch: "
                    + producer.stepOperationId + ":"
                    + frozen.routeOperationId);
            }
        }

        if (producer.finalRouteOperationIds.Count > 0)
        {
            string[] liveOperationIds = operations.Keys
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] physicalOperationIds = itemRoutes
                .Select(value => value.routeOperationId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!producer.finalRouteOperationIds.SequenceEqual(
                    liveOperationIds,
                    StringComparer.Ordinal)
                || !producer.finalRouteOperationIds.SequenceEqual(
                    physicalOperationIds,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-capacity-terminal-route-set-mismatch: "
                    + producer.stepOperationId);
            }
        }
    }
}
