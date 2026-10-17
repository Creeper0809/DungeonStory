using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;

internal static class ProductionFacilityDestructiveDrainStockSensorProducerJoinValidation
{
    internal static void ValidateStockSensorCompositeProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        DungeonProductionBillSaveData production,
        IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> children,
        ISet<string> joinedChildSteps)
    {
        if (entry == null
            || production == null
            || children == null
            || joinedChildSteps == null
            || !ProductionFacilityDestructiveDrainOperationId.TryParse(
                entry.operationId,
                out ProductionFacilityDestructiveDrainOperationId operationId))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-join-input-invalid");
        }

        BuildingInstanceId facilityId =
            (BuildingInstanceId)(entry.facilityId ?? string.Empty);
        if (!facilityId.IsValid)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-facility-invalid");
        }

        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            (entry.participants
                ?? new List<ProductionFacilityDestructiveDrainParticipantSaveData>())
            .Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .StockSensorEmbeddedSalvage,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
            ?? throw new InvalidOperationException(
                "Stock-sensor destructive-drain participant has no owner collection.");
        if (owners.Count > 1 || owners.Any(value => value == null))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-owner-cardinality");
        }

        ProductionStockSensorPhysicalCommitSaveData pending =
            SingleStockSensorRow(
                production.pendingStockSensorInstalls,
                facilityId.Value,
                value => value?.facilityId,
                "pending-install");
        ProductionInstalledStockSensorSaveData installed =
            SingleStockSensorRow(
                production.installedStockSensors,
                facilityId.Value,
                value => value?.facilityId,
                "installed");
        ProductionStockSensorRemovalSaveData removal =
            SingleStockSensorRow(
                production.pendingStockSensorRemovals,
                facilityId.Value,
                value => value?.facilityId,
                "removal");
        if (!ProductionStockSensorDestructiveDrainCanonical.Provenance.TryCreate(
                facilityId,
                pending,
                installed,
                removal,
                out ProductionStockSensorDestructiveDrainCanonical.Provenance
                    provenance))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-provenance-conflict");
        }

        string expectedOwner =
            ProductionFacilityDestructiveDrainOwnerStableIds.StockSensor(
                facilityId.Value);
        string expectedUpperStep = ProductionFacilityDestructiveDrainCanonical
            .BuildStepOperationId(
                operationId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .StockSensorEmbeddedSalvage,
                expectedOwner);
        string expectedChildStep =
            ProductionStockSensorDestructiveDrainCanonical
                .BuildChildStepOperationId(expectedUpperStep);
        string expectedDestination =
            ProductionStockSensorRuntime.BuildDestinationId(facilityId.Value);
        ProductionInputDestinationCustodyDrainSaveData[] childMatches = children
            .Where(value => value != null
                && string.Equals(value.stepOperationId,
                    expectedChildStep, StringComparison.Ordinal))
            .ToArray();

        if (owners.Count == 0)
        {
            if (provenance.Present || childMatches.Length != 0)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-stock-sensor-owner-missing");
            }
            return;
        }

        ProductionFacilityDestructiveDrainOwnerSaveData owner = owners[0];
        if (!string.Equals(owner.ownerStableId,
                expectedOwner, StringComparison.Ordinal)
            || !string.Equals(owner.stepOperationId,
                expectedUpperStep, StringComparison.Ordinal)
            || owner.disposition !=
                ProductionFacilityDestructiveDrainDisposition.Terminalize
            || !string.IsNullOrEmpty(owner.targetDestinationId))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-owner-identity-mismatch");
        }

        if (childMatches.Length == 0)
        {
            if (owner.phase ==
                ProductionFacilityDestructiveDrainStepPhase.Planned)
            {
                return;
            }
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-child-missing: "
                + expectedChildStep);
        }
        if (childMatches.Length != 1
            || !joinedChildSteps.Add(expectedChildStep))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-child-duplicate: "
                + expectedChildStep);
        }

        ProductionInputDestinationCustodyDrainSaveData child = childMatches[0];
        if (!ProductionInputDestinationCustodyDrainContract.IsValidSave(child)
            || !string.Equals(child.parentOperationId,
                entry.operationId, StringComparison.Ordinal)
            || !string.Equals(child.ownerStableId,
                expectedOwner, StringComparison.Ordinal)
            || !string.Equals(child.billId,
                expectedDestination, StringComparison.Ordinal)
            || !string.Equals(child.facilityId,
                facilityId.Value, StringComparison.Ordinal)
            || !string.Equals(child.sourceDestinationId,
                expectedDestination, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-child-identity-mismatch: "
                + expectedChildStep);
        }

        string expectedRequest =
            ProductionStockSensorDestructiveDrainCanonical
                .BuildRequestFingerprint(child.requestFingerprint, provenance);
        if (!string.Equals(owner.requestFingerprint,
                expectedRequest, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-request-mismatch: "
                + expectedUpperStep);
        }

        bool childPhaseMatches = owner.phase switch
        {
            ProductionFacilityDestructiveDrainStepPhase.Planned =>
                !ProductionStockSensorDestructiveDrainCanonical
                    .IsChildAcknowledged(child.phase),
            ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck =>
                ProductionStockSensorDestructiveDrainCanonical
                    .IsChildEffectCommitted(child.phase),
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                ProductionStockSensorDestructiveDrainCanonical
                    .IsChildAcknowledged(child.phase),
            _ => false
        };
        bool removalPhaseMatches = owner.phase switch
        {
            ProductionFacilityDestructiveDrainStepPhase.Planned =>
                removal == null
                || !ProductionStockSensorDestructiveDrainCanonical
                    .IsSensorAcknowledged(removal.phase),
            ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck =>
                provenance.Present
                    ? removal != null
                        && ProductionStockSensorDestructiveDrainCanonical
                            .IsSensorEffectCommitted(removal.phase)
                    : removal == null,
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                provenance.Present
                    ? removal != null
                        && ProductionStockSensorDestructiveDrainCanonical
                            .IsSensorAcknowledged(removal.phase)
                    : removal == null,
            _ => false
        };
        if (!childPhaseMatches || !removalPhaseMatches)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-phase-mismatch: "
                + expectedUpperStep);
        }

        if (owner.phase == ProductionFacilityDestructiveDrainStepPhase.Planned)
            return;
        if (!ProductionStockSensorDestructiveDrainCanonical
                .TryBuildCompositeTerminal(
                    owner.requestFingerprint,
                    child,
                    removal,
                    out string expectedCommit,
                    out string expectedReceipt)
            || !string.Equals(owner.commitId,
                expectedCommit, StringComparison.Ordinal)
            || !string.Equals(owner.receiptFingerprint,
                expectedReceipt, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-terminal-mismatch: "
                + expectedUpperStep);
        }
    }


    private static T SingleStockSensorRow<T>(
        IEnumerable<T> rows,
        string facilityId,
        Func<T, string> facilitySelector,
        string sourceKind)
        where T : class
    {
        T[] matches = (rows ?? Array.Empty<T>())
            .Where(value => value != null
                && string.Equals(facilitySelector(value),
                    facilityId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-stock-sensor-source-duplicate: "
                + sourceKind + ":" + facilityId);
        }
        return matches.SingleOrDefault();
    }

    internal static bool IsStockSensorChild(
        ProductionInputDestinationCustodyDrainSaveData child) =>
        child != null
        && ((child.ownerStableId ?? string.Empty).StartsWith(
                "stock-sensor:", StringComparison.Ordinal)
            || (child.billId ?? string.Empty).StartsWith(
                "production-sensor:", StringComparison.Ordinal)
            || (child.sourceDestinationId ?? string.Empty).StartsWith(
                "production-sensor:", StringComparison.Ordinal));
}
