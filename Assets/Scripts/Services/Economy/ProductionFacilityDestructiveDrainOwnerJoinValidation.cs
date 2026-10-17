using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Environment;

internal static class ProductionFacilityDestructiveDrainOwnerJoinValidation
{
    internal static IReadOnlyList<string> ProjectPreparedDemolitionOwners(
        ProductionFacilityDestructiveDrainEntrySaveData entry)
    {
        if (!ProductionFacilityDestructiveDrainJournal.IsValidOutcomeSnapshot(entry))
            throw new InvalidOperationException("production-destructive-drain-demolition-snapshot-invalid");
        // V4 migration deliberately has no frozen demolition observation. Its
        // empty owner is preserved; new observed demolitions carry a snapshot.
        return entry.OutcomeSnapshot.IsPresent
            ? new[] { ProductionFacilityDestructiveDrainOwnerStableIds.BuildingDemolition(entry.operationId) }
            : Array.Empty<string>();
    }

    internal static void ValidateBuildingDemolitionOutcomeJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry)
    {
        var row = entry.participants.Single(value => value != null
            && string.Equals(value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds.BuildingDemolitionOutcome,
                StringComparison.Ordinal));
        IReadOnlyList<string> expected = ProjectPreparedDemolitionOwners(entry);
        if (row.owners == null || row.owners.Count != expected.Count)
            throw new InvalidOperationException("production-destructive-drain-demolition-owner-cardinality");
        if (expected.Count == 0) return;
        BuildingInstanceId facilityId = (BuildingInstanceId)entry.facilityId;
        var operationId = ProductionFacilityDestructiveDrainOperationId.FromFacility(facilityId);
        var owner = row.owners[0];
        if (owner == null || !string.Equals(operationId.Value, entry.operationId, StringComparison.Ordinal)
            || owner.disposition != ProductionFacilityDestructiveDrainDisposition.Terminalize
            || !string.IsNullOrEmpty(owner.targetDestinationId))
            throw new InvalidOperationException("production-destructive-drain-demolition-owner-identity");
        var context = new ProductionFacilityDestructiveDrainStepContext(
            operationId, facilityId, row.participantId, owner,
            row.expectedCurrentContributionFingerprint, entry.OutcomeSnapshot);
        if (!BuildingDemolitionOutcomeLifecycle.IsCanonicalOwner(context))
            throw new InvalidOperationException("production-destructive-drain-demolition-owner-conflict");
        bool validReceipt = owner.phase switch
        {
            ProductionFacilityDestructiveDrainStepPhase.Planned =>
                string.IsNullOrEmpty(owner.commitId) && string.IsNullOrEmpty(owner.receiptFingerprint),
            ProductionFacilityDestructiveDrainStepPhase.EffectCommittedAwaitingOwnerAck
                or ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                BuildingDemolitionOutcomeLifecycle.MatchesJournalReceipt(context),
            _ => false
        };
        if (!validReceipt)
            throw new InvalidOperationException("production-destructive-drain-demolition-receipt-conflict");
    }

    internal static void ValidateFireDamageOutcomeJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<EnvironmentalFireDamageRecord> operations,
        ISet<string> joined)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData row = entry
            .participants.Single(value => value != null
                && string.Equals(
                    value.participantId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .EnvironmentalFireDamageOutcome,
                    StringComparison.Ordinal));
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in
                 row.owners)
        {
            const string Prefix = "fire-damage:";
            if (owner == null
                || !owner.ownerStableId.StartsWith(Prefix,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-fire-damage-owner-invalid");
            }
            string operationId = owner.ownerStableId.Substring(Prefix.Length);
            EnvironmentalFireDamageRecord operation = (operations
                    ?? Array.Empty<EnvironmentalFireDamageRecord>())
                .SingleOrDefault(value => value != null
                    && string.Equals(value.operationId, operationId,
                        StringComparison.Ordinal));
            if (operation == null
                || operation.targetKind !=
                    (int)EnvironmentalFireTargetKind.Building
                || !string.Equals(operation.targetId, entry.facilityId,
                    StringComparison.Ordinal)
                || !string.Equals(operation.requestFingerprint,
                    owner.requestFingerprint, StringComparison.Ordinal)
                || operation.phase ==
                    EnvironmentalFireDamagePhase.Prepared
                || operation.phase is
                        EnvironmentalFireDamagePhase.Delivered or EnvironmentalFireDamagePhase.Applied
                    && (owner.phase !=
                            ProductionFacilityDestructiveDrainStepPhase
                                .OwnerAcknowledged
                        || entry.phase is not
                            ProductionFacilityDestructiveDrainPhase
                                .AwaitingWorldRemoval
                            and not ProductionFacilityDestructiveDrainPhase
                                .WorldRemovedAwaitingCheckpointGc)
                || !joined.Add(operationId))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-fire-damage-owner-mismatch: "
                    + operationId);
            }
        }
    }


    internal static void ValidateWorkOrderDestructiveDrainJoins(
        DungeonWorkOrderSaveData workOrders,
        ModularFacilityWorldSaveData world,
        DungeonProductionFacilityDestructiveDrainSaveData drain,
        IBuildingDefinitionLookup buildingDefinitions)
    {
        if (workOrders == null
            || workOrders.version != DungeonWorkOrderSaveData.CurrentVersion
            || workOrders.orders == null
            || workOrders.qualityPipelines == null
            || world?.buildings == null)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-work-order-payload-invalid");
        }

        HashSet<string> joinedOperations = new(StringComparer.Ordinal);
        foreach (WorkOrderSaveData order in workOrders.orders
                     .Where(value => value != null)
                     .OrderBy(value => value.workOrderId, StringComparer.Ordinal))
        {
            string operationValue = order.destructiveDrainOperationId
                ?? string.Empty;
            bool hasOwnerState = operationValue.Length > 0
                || order.facilityRemovedForRetry
                || order.cancelRebuildAfterDestructiveDrain;
            if (!hasOwnerState)
                continue;
            if (!string.Equals(
                    order.workTypeId,
                    BuiltInWorkTypeIds.Dismantle.Value,
                    StringComparison.Ordinal)
                || !ProductionFacilityDestructiveDrainOperationId.TryParse(
                    operationValue,
                    out ProductionFacilityDestructiveDrainOperationId operation))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-owner-invalid: "
                    + (order.workOrderId ?? string.Empty));
            }
            if (!joinedOperations.Add(operation.Value))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-owner-duplicate: "
                    + operation.Value);
            }

            QualityTargetPipelineSaveData[] pipelineMatches =
                workOrders.qualityPipelines
                    .Where(pipeline => pipeline != null
                        && string.Equals(
                            pipeline.pipelineId,
                            order.qualityPipelineId,
                            StringComparison.Ordinal))
                    .Take(2)
                    .ToArray();
            if (string.IsNullOrEmpty(order.qualityPipelineId)
                || pipelineMatches.Length != 1)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-pipeline-cardinality: "
                    + (order.workOrderId ?? string.Empty));
            }
            QualityTargetPipelineSaveData pipeline = pipelineMatches[0];
            BuildingSO definition =
                buildingDefinitions.GetBuilding(order.targetBuildingId);
            string numericDefinitionId = order.targetBuildingId.ToString(
                CultureInfo.InvariantCulture);
            bool definitionMatches = definition != null
                && (string.Equals(
                        pipeline.definitionId,
                        definition.ContentDefinitionId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        pipeline.definitionId,
                        numericDefinitionId,
                        StringComparison.Ordinal));
            if (!definitionMatches
                || !pipeline.facilityPipeline
                || pipeline.footprintX != order.gridX
                || pipeline.footprintY != order.gridY
                || pipeline.footprintWidth !=
                    Math.Max(1, definition.Placement.Width)
                || pipeline.footprintHeight !=
                    Math.Max(1, definition.Placement.Height))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-pipeline-identity-mismatch: "
                    + (order.workOrderId ?? string.Empty));
            }
            if (order.cancelRebuildAfterDestructiveDrain
                && (pipeline.stage != QualityTargetPipelineStage.Cancelled
                    || order.status != (order.facilityRemovedForRetry
                        ? WorkOrderStatus.WaitingForOutputSpace
                        : WorkOrderStatus.Blocked)))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-cancel-state-mismatch: "
                    + (order.workOrderId ?? string.Empty));
            }

            ProductionFacilityDestructiveDrainEntrySaveData[] matches =
                (drain?.entries
                    ?? new List<
                        ProductionFacilityDestructiveDrainEntrySaveData>())
                .Where(entry => entry != null
                    && string.Equals(
                        entry.operationId,
                        operation.Value,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-journal-cardinality: "
                    + operation.Value);
            }

            ProductionFacilityDestructiveDrainEntrySaveData entry = matches[0];
            BuildingInstanceId facilityId =
                (BuildingInstanceId)(entry.facilityId ?? string.Empty);
            if (!facilityId.IsValid
                || !string.Equals(
                    ProductionFacilityDestructiveDrainOperationId
                        .FromFacility(facilityId).Value,
                    operation.Value,
                    StringComparison.Ordinal)
                || entry.cause !=
                    ProductionFacilityDestructiveDrainCause.ExplicitDemolition)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-journal-identity-mismatch: "
                    + operation.Value);
            }
            if (order.facilityRemovedForRetry
                && entry.phase != ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-terminal-phase-mismatch: "
                    + operation.Value);
            }
            if (entry.phase == ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc)
            {
                continue;
            }

            ModularFacilityBuildingSaveData[] worldMatches = world.buildings
                .Where(building => building != null
                    && string.Equals(
                        building.persistentInstanceId,
                        facilityId.Value,
                        StringComparison.Ordinal))
                .Take(2)
                .ToArray();
            if (worldMatches.Length != 1
                || worldMatches[0].buildingId != order.targetBuildingId
                || worldMatches[0].centerX != order.gridX
                || worldMatches[0].centerY != order.gridY)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-work-order-world-target-mismatch: "
                    + operation.Value);
            }
        }
    }
}
