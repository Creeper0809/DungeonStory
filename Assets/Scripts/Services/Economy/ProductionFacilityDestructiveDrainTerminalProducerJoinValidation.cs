using System;
using System.Collections.Generic;
using System.Linq;

internal static class ProductionFacilityDestructiveDrainTerminalProducerJoinValidation
{
    internal static void ValidateGenericTerminalProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<ProductionGenericBillTerminalDrainSaveData> producers,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            entry.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .GenericProductionBills,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
                ?? throw new InvalidOperationException(
                    "Generic destructive-drain participant has no owner collection.");
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in owners)
        {
            if (owner == null)
            {
                throw new InvalidOperationException(
                    "Generic destructive-drain owner is null.");
            }

            ProductionGenericBillTerminalDrainSaveData[] matches =
                (producers
                    ?? Array.Empty<
                        ProductionGenericBillTerminalDrainSaveData>())
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
                    "production-destructive-drain-generic-producer-missing: "
                    + owner.stepOperationId);
            }
            if (matches.Length != 1 || !joinedSteps.Add(owner.stepOperationId))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-generic-producer-duplicate: "
                    + owner.stepOperationId);
            }

            ProductionGenericBillTerminalDrainSaveData producer = matches[0];
            if (!string.Equals(
                    producer.parentOperationId,
                    entry.operationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.ownerStableId,
                    owner.ownerStableId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.facilityId,
                    entry.facilityId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    producer.requestFingerprint,
                    owner.requestFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-generic-producer-request-mismatch: "
                    + owner.stepOperationId);
            }

            bool phaseMatches = owner.phase switch
            {
                ProductionFacilityDestructiveDrainStepPhase.Planned =>
                    producer.phase !=
                        ProductionGenericBillTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck =>
                    producer.phase is
                        ProductionGenericBillTerminalDrainPhase
                            .BillTerminalCommittedAwaitingOwnerAcknowledgement
                        or ProductionGenericBillTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                    producer.phase ==
                        ProductionGenericBillTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                _ => false
            };
            if (!phaseMatches)
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-generic-producer-phase-mismatch: "
                    + owner.stepOperationId);
            }
            if (owner.phase !=
                    ProductionFacilityDestructiveDrainStepPhase.Planned
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
                    "production-destructive-drain-generic-producer-receipt-mismatch: "
                    + owner.stepOperationId);
            }
        }
    }


    internal static void ValidateCombatTerminalProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<CombatEquipmentTerminalDrainSaveData> producers,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            entry.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .CombatEquipmentCrafting,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
                ?? throw new InvalidOperationException(
                    "Combat destructive-drain participant has no owner collection.");
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in owners)
        {
            if (owner == null)
                throw new InvalidOperationException(
                    "Combat destructive-drain owner is null.");
            CombatEquipmentTerminalDrainSaveData[] matches = (producers
                    ?? Array.Empty<CombatEquipmentTerminalDrainSaveData>())
                .Where(value => value != null
                    && string.Equals(value.stepOperationId,
                        owner.stepOperationId, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                if (owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase.Planned)
                    continue;
                throw new InvalidOperationException(
                    "production-destructive-drain-combat-producer-missing: "
                    + owner.stepOperationId);
            }
            if (matches.Length != 1 || !joinedSteps.Add(owner.stepOperationId))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-combat-producer-duplicate: "
                    + owner.stepOperationId);
            }

            CombatEquipmentTerminalDrainSaveData producer = matches[0];
            if (!string.Equals(producer.parentOperationId,
                    entry.operationId, StringComparison.Ordinal)
                || !string.Equals(producer.source.ownerStableId,
                    owner.ownerStableId, StringComparison.Ordinal)
                || !string.Equals(producer.source.facilityId,
                    entry.facilityId, StringComparison.Ordinal)
                || !string.Equals(producer.requestFingerprint,
                    owner.requestFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-combat-producer-request-mismatch: "
                    + owner.stepOperationId);
            }

            bool phaseMatches = owner.phase switch
            {
                ProductionFacilityDestructiveDrainStepPhase.Planned =>
                    producer.phase != CombatEquipmentTerminalDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck =>
                    producer.phase is CombatEquipmentTerminalDrainPhase
                            .TerminalEffectsCommittedAwaitingOwnerAcknowledgement
                        or CombatEquipmentTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                    producer.phase == CombatEquipmentTerminalDrainPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc,
                _ => false
            };
            ValidateTerminalProducerPhaseAndReceipt(
                owner,
                producer.commitId,
                producer.receiptFingerprint,
                phaseMatches,
                "combat");
        }
    }


    internal static void ValidateApparelTerminalProducerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IReadOnlyList<ProductionApparelOrderTerminalDrainSaveData> producers,
        ISet<string> joinedSteps)
    {
        ProductionFacilityDestructiveDrainParticipantSaveData participant =
            entry.participants.Single(value => string.Equals(
                value.participantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .ApparelWorkOrders,
                StringComparison.Ordinal));
        IReadOnlyList<ProductionFacilityDestructiveDrainOwnerSaveData> owners =
            participant.owners
                ?? throw new InvalidOperationException(
                    "Apparel destructive-drain participant has no owner collection.");
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in owners)
        {
            if (owner == null)
                throw new InvalidOperationException(
                    "Apparel destructive-drain owner is null.");
            ProductionApparelOrderTerminalDrainSaveData[] matches = (producers
                    ?? Array.Empty<
                        ProductionApparelOrderTerminalDrainSaveData>())
                .Where(value => value != null
                    && string.Equals(value.stepOperationId,
                        owner.stepOperationId, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                if (owner.phase ==
                    ProductionFacilityDestructiveDrainStepPhase.Planned)
                    continue;
                throw new InvalidOperationException(
                    "production-destructive-drain-apparel-producer-missing: "
                    + owner.stepOperationId);
            }
            if (matches.Length != 1 || !joinedSteps.Add(owner.stepOperationId))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-apparel-producer-duplicate: "
                    + owner.stepOperationId);
            }

            ProductionApparelOrderTerminalDrainSaveData producer = matches[0];
            if (!string.Equals(producer.parentOperationId,
                    entry.operationId, StringComparison.Ordinal)
                || !string.Equals(producer.ownerStableId,
                    owner.ownerStableId, StringComparison.Ordinal)
                || !string.Equals(producer.facilityId,
                    entry.facilityId, StringComparison.Ordinal)
                || !string.Equals(producer.requestFingerprint,
                    owner.requestFingerprint, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "production-destructive-drain-apparel-producer-request-mismatch: "
                    + owner.stepOperationId);
            }

            bool phaseMatches = owner.phase switch
            {
                ProductionFacilityDestructiveDrainStepPhase.Planned =>
                    producer.phase !=
                        ProductionApparelOrderTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck =>
                    producer.phase is
                        ProductionApparelOrderTerminalDrainPhase
                            .SourceOrderTerminalCommittedAwaitingOwnerAcknowledgement
                        or ProductionApparelOrderTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged =>
                    producer.phase ==
                        ProductionApparelOrderTerminalDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc,
                _ => false
            };
            ValidateTerminalProducerPhaseAndReceipt(
                owner,
                producer.commitId,
                producer.receiptFingerprint,
                phaseMatches,
                "apparel");
        }
    }


    private static void ValidateTerminalProducerPhaseAndReceipt(
        ProductionFacilityDestructiveDrainOwnerSaveData owner,
        string producerCommitId,
        string producerReceiptFingerprint,
        bool phaseMatches,
        string producerKind)
    {
        if (!phaseMatches)
        {
            throw new InvalidOperationException(
                "production-destructive-drain-" + producerKind
                + "-producer-phase-mismatch: " + owner.stepOperationId);
        }
        if (owner.phase != ProductionFacilityDestructiveDrainStepPhase.Planned
            && (!string.Equals(owner.commitId,
                    producerCommitId, StringComparison.Ordinal)
                || !string.Equals(owner.receiptFingerprint,
                    producerReceiptFingerprint, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "production-destructive-drain-" + producerKind
                + "-producer-receipt-mismatch: " + owner.stepOperationId);
        }
    }
}
