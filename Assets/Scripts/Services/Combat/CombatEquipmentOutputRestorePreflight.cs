using System;
using System.Collections.Generic;
using System.Linq;

internal static class CombatEquipmentOutputRestorePreflight
{
    internal static IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement>
        ValidateAndBuildAcknowledgements(
            IReadOnlyList<CombatEquipmentCraftOrderSaveData> orders,
            IProductionOutputMaximumMassRegistry maximumMass,
            IProductionOutputDetachedFacilityCapacityRestoreGuard
                detachedCapacity,
            IProductionDomainOutputRestoreJoin outputRestoreJoin)
    {
        if (maximumMass == null)
            throw new ArgumentNullException(nameof(maximumMass));
        if (detachedCapacity == null)
            throw new ArgumentNullException(nameof(detachedCapacity));
        if (outputRestoreJoin == null)
            throw new ArgumentNullException(nameof(outputRestoreJoin));

        CombatEquipmentCraftOrderSaveData[] ordered = (orders
                ?? Array.Empty<CombatEquipmentCraftOrderSaveData>())
            .Where(value => value != null)
            .OrderBy(
                CombatEquipmentCraftOutputTransaction.OwnerStableId,
                StringComparer.Ordinal)
            .ToArray();

        // Validate the full owner set before the first join call. A corrupt
        // late row must not expose a partially adopted candidate set.
        foreach (CombatEquipmentCraftOrderSaveData order in ordered)
            ValidateDetachedCapacity(order, maximumMass, detachedCapacity);

        List<ProductionDomainOutputRestoreAcknowledgement>
            acknowledgements = new();
        foreach (CombatEquipmentCraftOrderSaveData order in ordered)
        {
            switch (order.outputPhase)
            {
                case CombatEquipmentCraftOutputPhase
                        .PublishedAwaitingInputAcknowledgement:
                    acknowledgements.Add(outputRestoreJoin.AdoptPending(
                        order.outputPublication));
                    order.outputPublication.outputAcknowledged = true;
                    order.outputPublication.restoredInCurrentTransaction = true;
                    order.outputPhase = CombatEquipmentCraftOutputPhase
                        .RestoredOutputAwaitingInputAcknowledgement;
                    break;

                default:
                    outputRestoreJoin.RequireNoPending(order.outputPublication);
                    break;
            }
        }
        return acknowledgements
            .OrderBy(value => value.BatchCommitId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateDetachedCapacity(
        CombatEquipmentCraftOrderSaveData order,
        IProductionOutputMaximumMassRegistry maximumMass,
        IProductionOutputDetachedFacilityCapacityRestoreGuard detachedCapacity)
    {
        ProductionDomainOutputPublicationSaveData owner =
            order?.outputPublication;
        if (owner == null || owner.IsEmpty)
            return;
        if (!string.Equals(
                owner.ownerFacilityId,
                order.facilityPersistentId,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.outcomeFingerprint,
                CombatEquipmentCraftOutputTransaction
                    .CaptureOutcomeFingerprint(order),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Combat detached output owner provenance drifted: "
                + CombatEquipmentCraftOutputTransaction.OwnerStableId(order));
        }
        ProductionOutputMaximumMassProjection projection = maximumMass
            .CaptureDeclared(
                order.outputCapability.ToDescriptor(),
                order.outputQuantity);
        ProductionOutputBatchMaximumMassProof proof = new(new[] { projection });
        if (!string.Equals(
                proof.SourceDigest,
                owner.maximumMassProofDigest,
                StringComparison.Ordinal)
            || proof.MaximumBatchMassGrams != owner.maximumBatchMassGrams)
        {
            throw new InvalidOperationException(
                "Combat detached output capacity proof is stale: "
                + CombatEquipmentCraftOutputTransaction.OwnerStableId(order));
        }
        detachedCapacity.Validate(
            CombatEquipmentCraftOutputTransaction.OwnerStableId(order),
            order.facilityPersistentId,
            proof,
            owner.capacitySourceDigest,
            owner.requiredMinimumCapacityGrams);
    }
}
