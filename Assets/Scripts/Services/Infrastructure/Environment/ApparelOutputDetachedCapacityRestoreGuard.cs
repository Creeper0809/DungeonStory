using System;
using System.Collections.Generic;

public interface IApparelOutputDetachedCapacityRestoreGuard
{
    void Validate(
        IReadOnlyList<ApparelWorkOrderSaveData> liveOrders,
        IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> terminalStates);
}

/// <summary>
/// Reprojects every still-live Apparel output-capacity claim from the detached
/// facility-world candidate. Closed historical terminal rows are receipts, not
/// live capacity owners, and therefore are deliberately excluded.
/// </summary>
public sealed class ApparelOutputDetachedCapacityRestoreGuard :
    IApparelOutputDetachedCapacityRestoreGuard
{
    private readonly IProductionOutputMaximumMassRegistry maximumMass;
    private readonly IProductionOutputDetachedFacilityCapacityRestoreGuard
        detachedCapacity;

    public ApparelOutputDetachedCapacityRestoreGuard(
        IProductionOutputMaximumMassRegistry maximumMass,
        IProductionOutputDetachedFacilityCapacityRestoreGuard detachedCapacity)
    {
        this.maximumMass = maximumMass
            ?? throw new ArgumentNullException(nameof(maximumMass));
        this.detachedCapacity = detachedCapacity
            ?? throw new ArgumentNullException(nameof(detachedCapacity));
    }

    public void Validate(
        IReadOnlyList<ApparelWorkOrderSaveData> liveOrders,
        IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> terminalStates)
    {
        foreach (ApparelWorkOrderSaveData order in
                 liveOrders ?? Array.Empty<ApparelWorkOrderSaveData>())
        {
            ValidateOrder(order);
        }

        foreach (ApparelWorkOrderTerminalStateSaveData terminal in
                 terminalStates ?? Array.Empty<ApparelWorkOrderTerminalStateSaveData>())
        {
            if (terminal?.sourceTerminalReceipt == null)
                ValidateOrder(terminal?.sourceOrder);
        }
    }

    private void ValidateOrder(ApparelWorkOrderSaveData order)
    {
        if (order == null)
            throw new InvalidOperationException(
                "Apparel detached-capacity owner is null.");

        bool craftPresent = order.craftOutputCapability is { IsEmpty: false }
            || !string.IsNullOrEmpty(order.craftMaximumMassProofDigest)
            || order.craftMaximumBatchMassGrams != 0L
            || !string.IsNullOrEmpty(order.craftCapacitySourceDigest)
            || order.craftRequiredMinimumCapacityGrams != 0L;
        if (craftPresent)
        {
            ProductionOutputBatchMaximumMassProof proof = ReprojectProof(
                order.craftOutputCapability,
                maximumQuantity: 1,
                order.craftMaximumMassProofDigest,
                order.craftMaximumBatchMassGrams,
                "apparel-craft:" + order.orderId);
            detachedCapacity.Validate(
                "apparel-craft:" + order.orderId,
                order.facilityInstanceId,
                proof,
                order.craftCapacitySourceDigest,
                order.craftRequiredMinimumCapacityGrams);
        }

        bool rejectedPresent = order.rejectedRecoveryOutputCapability is
                { IsEmpty: false }
            || !string.IsNullOrEmpty(
                order.rejectedRecoveryMaximumMassProofDigest)
            || order.rejectedRecoveryMaximumBatchMassGrams != 0L
            || !string.IsNullOrEmpty(order.rejectedRecoveryCapacitySourceDigest)
            || order.rejectedRecoveryRequiredMinimumCapacityGrams != 0L;
        if (!rejectedPresent)
            return;

        ProductionOutputBatchMaximumMassProof rejectedProof = ReprojectProof(
            order.rejectedRecoveryOutputCapability,
            order.rejectedMaterialAmount,
            order.rejectedRecoveryMaximumMassProofDigest,
            order.rejectedRecoveryMaximumBatchMassGrams,
            "apparel-rejected:" + order.orderId);
        detachedCapacity.Validate(
            "apparel-rejected:" + order.orderId,
            order.facilityInstanceId,
            rejectedProof,
            order.rejectedRecoveryCapacitySourceDigest,
            order.rejectedRecoveryRequiredMinimumCapacityGrams);
    }

    private ProductionOutputBatchMaximumMassProof ReprojectProof(
        ProductionOutputCapabilitySaveData capability,
        int maximumQuantity,
        string savedDigest,
        long savedMaximumBatchMassGrams,
        string ownerStableId)
    {
        if (capability == null
            || capability.IsEmpty
            || maximumQuantity <= 0)
        {
            throw new InvalidOperationException(
                "Apparel detached-capacity maximum owner is incomplete: "
                + ownerStableId);
        }

        ProductionOutputMaximumMassProjection projection =
            maximumMass.CaptureDeclared(
                capability.ToDescriptor(),
                maximumQuantity);
        ProductionOutputBatchMaximumMassProof proof = new(
            new[] { projection });
        if (!string.Equals(
                proof.SourceDigest,
                savedDigest,
                StringComparison.Ordinal)
            || proof.MaximumBatchMassGrams != savedMaximumBatchMassGrams)
        {
            throw new InvalidOperationException(
                "Apparel detached-capacity maximum proof drifted: "
                + ownerStableId);
        }
        return proof;
    }
}
