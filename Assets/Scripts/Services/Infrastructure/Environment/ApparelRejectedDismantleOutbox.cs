using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Durable two-part physical Transform for a quality-rejected apparel item.
/// The unique source first enters Items-owned Transfer WIP. The deterministic
/// recovery output is then published exactly once, and only then is the input
/// receipt acknowledged. The saved apparel order owns the cross-aggregate
/// join and the explicit input/output mass loss envelope.
/// </summary>
public static class ApparelRejectedDismantleOutbox
{
    public const string ReasonCode =
        "apparel-rejected-output-to-dismantle-wip";
    public const string RecoveryReasonCode =
        "apparel-rejected-dismantle-recovery";
    public const string OperationPrefix = "apparel-rejected-dismantle:";
    public const string RecoveryOperationPrefix =
        "apparel-rejected-recovery:";

    public static string FormatOperationId(string orderId, int attemptIndex) =>
        $"{OperationPrefix}{orderId}:{Math.Max(0, attemptIndex):D4}";

    public static string FormatRecoveryOperationId(
        string orderId,
        int attemptIndex) =>
        $"{RecoveryOperationPrefix}{orderId}:{Math.Max(0, attemptIndex):D4}:0000";

    public static string FormatRecoveryCommitId(
        string operationId,
        string itemId,
        int quantity) =>
        $"physical-source:{operationId}:{itemId}:{quantity}";

    public static string CreateRequestFingerprint(string sourceStackId) =>
        $"{(int)PhysicalItemDispositionKind.Transfer}:{ReasonCode}:"
        + $"{sourceStackId}=1";

    [GameplayInternalOnly(
        "Apparel work-order completion is the sole rejected-output Transform owner.",
        "ApparelWorkOrderRuntime rejected-output resolver only")]
    public static bool TryCommitOrResume(
        ApparelWorkOrderSaveData order,
        IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService dispositions,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || items == null
            || dispositions == null
            || !order.dismantlingRejectedOutput
            || !Canonical(order.orderId)
            || !Canonical(order.rejectedOutputStackId)
            || !Canonical(order.rejectedOutputInstanceId))
        {
            failureReason = "apparel-rejected-dismantle-invalid";
            return false;
        }

        string operationId = FormatOperationId(
            order.orderId,
            order.qualityAttemptIndex);
        bool ownerStarted = !string.IsNullOrEmpty(
            order.rejectedDismantleOperationId);
        bool hasPending = dispositions.TryGetPending(
            operationId,
            out PhysicalItemBatchDispositionReceipt pending);

        if (order.rejectedDismantleAcknowledged)
        {
            return ValidateOwnerShape(order, out failureReason);
        }

        if (!ownerStarted && hasPending)
        {
            if (!ValidateReceipt(order, pending, operationId, out failureReason))
                return false;
            AdoptInputReceipt(order, pending);
            return true;
        }

        if (ownerStarted)
        {
            if (!ValidateOwnerShape(order, out failureReason))
                return false;
            if (!hasPending)
            {
                // Acknowledge removes the pending row before the owner update can
                // be durably observed. Continue only into the exact recovery
                // validation below; do not infer acknowledgement here and do not
                // recreate an owner-declared published batch if it is missing.
                // TryEnsureRecovery validates the expected facility destination,
                // then the idempotent acknowledgement command closes the owner.
                if (order.rejectedRecoveryPublished)
                    return true;
                failureReason =
                    "apparel-rejected-dismantle-pending-receipt-missing";
                return false;
            }
            return ValidateReceipt(
                order,
                pending,
                operationId,
                out failureReason);
        }

        WorldItemStackSnapshot source = items.GetAllStacks()
            .SingleOrDefault(stack => stack != null
                && string.Equals(
                    stack.StackId,
                    order.rejectedOutputStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemInstanceId,
                    order.rejectedOutputInstanceId,
                    StringComparison.Ordinal)
                && stack.State == WorldItemStackState.FacilityOutputBuffer
                && stack.Quantity == 1
                && stack.ReservedQuantity == 0
                && string.IsNullOrEmpty(stack.ReservedByPersistentId));
        if (source == null)
        {
            failureReason = "apparel-rejected-dismantle-source-missing";
            return false;
        }

        if (!dispositions.TryCommitPending(
                new[]
                {
                    new PhysicalItemTransformInput(
                        order.rejectedOutputStackId,
                        1)
                },
                PhysicalItemDispositionKind.Transfer,
                operationId,
                ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason)
            || !ValidateReceipt(
                order,
                receipt,
                operationId,
                out failureReason))
        {
            return false;
        }

        AdoptInputReceipt(order, receipt);
        return true;
    }

    [GameplayInternalOnly(
        "Apparel work-order completion publishes deterministic recovery only after input WIP exists.",
        "ApparelWorkOrderRuntime rejected-output resolver only")]
    public static bool TryEnsureRecovery(
        ApparelWorkOrderSaveData order,
        IWorldItemStackRuntime items,
        Vector2Int position,
        string destinationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || items == null
            || !Canonical(destinationId)
            || !ValidateOwnerShape(order, out failureReason)
            || order.rejectedDismantleAcknowledged)
        {
            if (string.IsNullOrEmpty(failureReason))
                failureReason = "apparel-rejected-recovery-invalid";
            return false;
        }

        string operationId = FormatRecoveryOperationId(
            order.orderId,
            order.qualityAttemptIndex);
        int quantity = order.rejectedMaterialAmount;
        if (quantity == 0)
        {
            if (!string.IsNullOrEmpty(order.rejectedRecoveryCommitId)
                || order.rejectedRecoveryOutputMassGrams != 0L)
            {
                failureReason = "apparel-rejected-zero-recovery-conflict";
                return false;
            }
            order.rejectedRecoveryOperationId = operationId;
            order.rejectedMaterialSpawned = 0;
            order.rejectedRecoveryPublished = true;
            return true;
        }
        if (!Canonical(order.rejectedRecoveryItemId))
        {
            failureReason = "apparel-rejected-recovery-item-invalid";
            return false;
        }

        long outputMass;
        try
        {
            outputMass = items.MassQuery.GetDefinitionUnitMass(
                    (ItemDefinitionId)order.rejectedRecoveryItemId)
                .Multiply(quantity).Value;
        }
        catch (Exception exception)
        {
            failureReason = "apparel-rejected-recovery-mass-invalid:"
                + exception.GetType().Name;
            return false;
        }
        if (outputMass <= 0L
            || outputMass > order.rejectedDismantleInputMassGrams)
        {
            failureReason = "apparel-rejected-recovery-mass-exceeds-input";
            return false;
        }

        string commitId = FormatRecoveryCommitId(
            operationId,
            order.rejectedRecoveryItemId,
            quantity);
        if ((!string.IsNullOrEmpty(order.rejectedRecoveryOperationId)
                && !string.Equals(
                    order.rejectedRecoveryOperationId,
                    operationId,
                    StringComparison.Ordinal))
            || (!string.IsNullOrEmpty(order.rejectedRecoveryCommitId)
                && !string.Equals(
                    order.rejectedRecoveryCommitId,
                    commitId,
                    StringComparison.Ordinal))
            || (order.rejectedRecoveryOutputMassGrams != 0L
                && order.rejectedRecoveryOutputMassGrams != outputMass))
        {
            failureReason = "apparel-rejected-recovery-owner-conflict";
            return false;
        }

        WorldItemStackSnapshot[] existing = CaptureRecoveryStacks(
            items,
            commitId);
        if (existing.Length == 0)
        {
            if (order.rejectedRecoveryPublished)
            {
                failureReason =
                    "apparel-rejected-recovery-published-output-missing";
                return false;
            }
            if (!items.SpawnItemAtWithComponents(
                    order.rejectedRecoveryItemId,
                    quantity,
                    position,
                    WorldItemStackState.FacilityOutputBuffer,
                    destinationId,
                    new[]
                    {
                        ProductionOutputCommitComponentCodec.Create(commitId)
                    },
                    out int spawned)
                || spawned != quantity)
            {
                failureReason =
                    "apparel-rejected-recovery-output-space-unavailable";
                return false;
            }
            existing = CaptureRecoveryStacks(items, commitId);
        }

        if (!ValidateRecoveryStacks(
                existing,
                order.rejectedRecoveryItemId,
                quantity,
                outputMass,
                position,
                destinationId,
                items,
                out failureReason))
        {
            return false;
        }

        order.rejectedRecoveryOperationId = operationId;
        order.rejectedRecoveryCommitId = commitId;
        order.rejectedRecoveryOutputMassGrams = outputMass;
        order.rejectedMaterialSpawned = quantity;
        order.rejectedRecoveryPublished = true;
        return true;
    }

    [GameplayInternalOnly(
        "Apparel work-order completion acknowledges input only after exact recovery publication.",
        "ApparelWorkOrderRuntime rejected-output resolver only")]
    public static bool TryAcknowledge(
        ApparelWorkOrderSaveData order,
        IPhysicalItemBatchDispositionService dispositions,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || dispositions == null
            || !ValidateOwnerShape(order, out failureReason)
            || !order.rejectedRecoveryPublished)
        {
            if (string.IsNullOrEmpty(failureReason))
                failureReason = "apparel-rejected-recovery-not-published";
            return false;
        }
        if (order.rejectedDismantleAcknowledged)
            return true;
        if (!dispositions.Acknowledge(
                order.rejectedDismantleCommitId,
                out failureReason))
        {
            return false;
        }
        order.rejectedDismantleAcknowledged = true;
        return true;
    }

    public static bool ValidateOwnerShape(
        ApparelWorkOrderSaveData order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null)
        {
            failureReason = "apparel-rejected-dismantle-owner-null";
            return false;
        }
        bool hasOperation = !string.IsNullOrEmpty(
            order.rejectedDismantleOperationId);
        if (!order.dismantlingRejectedOutput)
        {
            bool empty = !order.rejectedOutputConsumed
                && string.IsNullOrEmpty(order.rejectedOutputStackId)
                && string.IsNullOrEmpty(order.rejectedOutputInstanceId)
                && order.rejectedMaterialAmount == 0
                && order.rejectedMaterialSpawned == 0
                && string.IsNullOrEmpty(order.rejectedRecoveryItemId)
                && !hasOperation
                && string.IsNullOrEmpty(order.rejectedDismantleCommitId)
                && string.IsNullOrEmpty(
                    order.rejectedDismantleRequestFingerprint)
                && order.rejectedDismantleInputMassGrams == 0L
                && string.IsNullOrEmpty(order.rejectedRecoveryOperationId)
                && string.IsNullOrEmpty(order.rejectedRecoveryCommitId)
                && order.rejectedRecoveryOutputMassGrams == 0L
                && PlannedRecoveryFieldsEmpty(order)
                && string.IsNullOrEmpty(order.rejectedOutputLeaseId)
                && !order.rejectedRecoveryPublished
                && !order.rejectedDismantleAcknowledged;
            if (!empty)
                failureReason = "apparel-rejected-dismantle-owner-partial";
            return empty;
        }

        bool hasPlannedRecovery = !PlannedRecoveryFieldsEmpty(order);
        string baseRecoveryOperationId = FormatRecoveryOperationId(
            order.orderId,
            order.qualityAttemptIndex);
        string expectedRecoveryCommitId = order.rejectedMaterialAmount == 0
            ? string.Empty
            : FormatRecoveryCommitId(
                baseRecoveryOperationId,
                order.rejectedRecoveryItemId,
                order.rejectedMaterialAmount);
        bool activeTokenShape = Canonical(order.rejectedRecoveryOperationId)
            && Canonical(order.rejectedRecoveryAdmissionTokenId)
            && Canonical(order.rejectedRecoveryPlannedOutputFingerprint)
            && string.Equals(
                order.rejectedRecoveryOperationId,
                FormatPublicationOperationId(
                    order.orderId,
                    order.qualityAttemptIndex,
                    order.rejectedRecoveryPublicationAttempt),
                StringComparison.Ordinal);
        bool releasedTokenShape = string.IsNullOrEmpty(
                order.rejectedRecoveryOperationId)
            && string.IsNullOrEmpty(order.rejectedRecoveryAdmissionTokenId)
            && string.IsNullOrEmpty(
                order.rejectedRecoveryPlannedOutputFingerprint);
        bool plannedFrozenShape = order.rejectedMaterialAmount > 0
            && string.Equals(
                order.rejectedRecoveryCommitId,
                expectedRecoveryCommitId,
                StringComparison.Ordinal)
            && Canonical(order.rejectedRecoveryOutcomeFingerprint)
            && order.rejectedRecoveryOutputCapability is { IsEmpty: false }
            && string.Equals(
                order.rejectedRecoveryOutputCapability.outputLineId,
                ApparelPhysicalTransaction.RejectedRecoveryOutputLineId,
                StringComparison.Ordinal)
            && string.Equals(
                order.rejectedRecoveryOutputCapability.itemId,
                order.rejectedRecoveryItemId,
                StringComparison.Ordinal)
            && Canonical(order.rejectedRecoveryOutputCapability.fingerprint)
            && IsSha256(order.rejectedRecoveryMaximumMassProofDigest)
            && order.rejectedRecoveryMaximumBatchMassGrams > 0L
            && Canonical(order.rejectedRecoveryCapacitySourceDigest)
            && order.rejectedRecoveryRequiredMinimumCapacityGrams > 0L
            && order.rejectedRecoveryOutputMassGrams > 0L
            && order.rejectedRecoveryOutputMassGrams
                <= order.rejectedRecoveryMaximumBatchMassGrams
            && order.rejectedRecoveryOutputMassGrams
                <= order.rejectedDismantleInputMassGrams
            && (activeTokenShape || releasedTokenShape)
            && CanonicalSortedUnique(order.rejectedRecoveryStackIds)
            && (!order.rejectedRecoveryAdmissionCommitted
                || order.rejectedRecoveryPublished)
            && (!order.rejectedRecoveryOutputAcknowledged
                || order.rejectedRecoveryAdmissionCommitted);
        bool legacyRecoveryShape = !hasPlannedRecovery
            && (!order.rejectedRecoveryPublished
                ? string.IsNullOrEmpty(order.rejectedRecoveryOperationId)
                    && string.IsNullOrEmpty(order.rejectedRecoveryCommitId)
                    && order.rejectedRecoveryOutputMassGrams == 0L
                    && order.rejectedMaterialSpawned == 0
                : string.Equals(
                        order.rejectedRecoveryOperationId,
                        baseRecoveryOperationId,
                        StringComparison.Ordinal)
                    && order.rejectedMaterialSpawned
                        == order.rejectedMaterialAmount
                    && (order.rejectedMaterialAmount == 0
                        ? string.IsNullOrEmpty(
                                order.rejectedRecoveryCommitId)
                            && order.rejectedRecoveryOutputMassGrams == 0L
                        : string.Equals(
                                order.rejectedRecoveryCommitId,
                                expectedRecoveryCommitId,
                                StringComparison.Ordinal)
                            && order.rejectedRecoveryOutputMassGrams > 0L
                            && order.rejectedRecoveryOutputMassGrams
                                <= order.rejectedDismantleInputMassGrams));
        bool plannedZeroShape = hasPlannedRecovery
            && order.rejectedMaterialAmount == 0
            && string.Equals(
                order.rejectedRecoveryOperationId,
                baseRecoveryOperationId,
                StringComparison.Ordinal)
            && string.IsNullOrEmpty(order.rejectedRecoveryCommitId)
            && Canonical(order.rejectedRecoveryOutcomeFingerprint)
            && order.rejectedRecoveryOutputMassGrams == 0L
            && order.rejectedMaterialSpawned == 0
            && order.rejectedRecoveryPublished
            && order.rejectedRecoveryAdmissionCommitted
            && order.rejectedRecoveryOutputAcknowledged
            && (order.rejectedRecoveryStackIds?.Count ?? 0) == 0;
        bool recoveryShape = legacyRecoveryShape
            || plannedZeroShape
            || (hasPlannedRecovery && plannedFrozenShape
                && (!order.rejectedRecoveryPublished
                    ? order.rejectedMaterialSpawned == 0
                        && !order.rejectedRecoveryAdmissionCommitted
                        && !order.rejectedRecoveryOutputAcknowledged
                        && (order.rejectedRecoveryStackIds?.Count ?? 0) == 0
                    : order.rejectedMaterialSpawned
                            == order.rejectedMaterialAmount
                        && order.rejectedRecoveryStackIds.Count > 0));

        bool valid = Canonical(order.orderId)
            && Canonical(order.rejectedOutputStackId)
            && Canonical(order.rejectedOutputInstanceId)
            && Canonical(order.rejectedRecoveryItemId)
            && order.rejectedMaterialAmount >= 0
            && order.rejectedMaterialSpawned >= 0
            && order.rejectedMaterialSpawned
                <= order.rejectedMaterialAmount
            && (!hasOperation
                ? !order.rejectedOutputConsumed
                    && string.IsNullOrEmpty(order.rejectedDismantleCommitId)
                    && string.IsNullOrEmpty(
                        order.rejectedDismantleRequestFingerprint)
                    && order.rejectedDismantleInputMassGrams == 0L
                    && !order.rejectedRecoveryPublished
                    && !order.rejectedDismantleAcknowledged
                    && (string.IsNullOrEmpty(order.rejectedOutputLeaseId)
                        || Canonical(order.rejectedOutputLeaseId))
                : order.rejectedOutputConsumed
                    && string.Equals(
                        order.rejectedDismantleOperationId,
                        FormatOperationId(
                            order.orderId,
                            order.qualityAttemptIndex),
                        StringComparison.Ordinal)
                    && Canonical(order.rejectedDismantleCommitId)
                    && string.Equals(
                        order.rejectedDismantleRequestFingerprint,
                        CreateRequestFingerprint(
                            order.rejectedOutputStackId),
                        StringComparison.Ordinal)
                    && order.rejectedDismantleInputMassGrams > 0L)
            && recoveryShape
            && (!order.rejectedDismantleAcknowledged
                || order.rejectedRecoveryPublished)
            && (!order.rejectedDismantleAcknowledged
                || !hasPlannedRecovery
                || order.rejectedRecoveryOutputAcknowledged);
        if (!valid)
            failureReason = "apparel-rejected-dismantle-owner-invalid";
        return valid;
    }

    public static bool TryValidateRecoveryOutput(
        ApparelWorkOrderSaveData order,
        IWorldItemStackRuntime items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null
            || items == null
            || !order.rejectedRecoveryPublished
            || !ValidateOwnerShape(order, out failureReason))
        {
            return false;
        }
        if (order.rejectedMaterialAmount == 0)
            return true;
        WorldItemStackSnapshot[] stacks = CaptureRecoveryStacks(
            items,
            order.rejectedRecoveryCommitId);
        if (stacks.Length == 0)
        {
            failureReason = "apparel-rejected-recovery-output-missing";
            return false;
        }
        string destinationId = stacks[0].DestinationId;
        Vector2Int position = stacks[0].Position;
        return ValidateRecoveryStacks(
            stacks,
            order.rejectedRecoveryItemId,
            order.rejectedMaterialAmount,
            order.rejectedRecoveryOutputMassGrams,
            position,
            destinationId,
            items,
            out failureReason);
    }

    public static void Clear(ApparelWorkOrderSaveData order)
    {
        if (order == null)
            return;
        order.rejectedOutputConsumed = false;
        order.rejectedDismantleOperationId = string.Empty;
        order.rejectedDismantleCommitId = string.Empty;
        order.rejectedDismantleRequestFingerprint = string.Empty;
        order.rejectedDismantleInputMassGrams = 0L;
        order.rejectedRecoveryOperationId = string.Empty;
        order.rejectedRecoveryCommitId = string.Empty;
        order.rejectedRecoveryOutputMassGrams = 0L;
        order.rejectedRecoveryPublicationAttempt = 0;
        order.rejectedRecoveryOutcomeFingerprint = string.Empty;
        order.rejectedRecoveryAdmissionTokenId = string.Empty;
        order.rejectedRecoveryOutputCapability =
            new ProductionOutputCapabilitySaveData();
        order.rejectedRecoveryMaximumMassProofDigest = string.Empty;
        order.rejectedRecoveryMaximumBatchMassGrams = 0L;
        order.rejectedRecoveryCapacitySourceDigest = string.Empty;
        order.rejectedRecoveryRequiredMinimumCapacityGrams = 0L;
        order.rejectedRecoveryPlannedOutputFingerprint = string.Empty;
        order.rejectedRecoveryStackIds?.Clear();
        order.rejectedRecoveryPublished = false;
        order.rejectedRecoveryAdmissionCommitted = false;
        order.rejectedRecoveryOutputAcknowledged = false;
        order.rejectedDismantleAcknowledged = false;
        order.rejectedOutputLeaseId = string.Empty;
    }

    private static void AdoptInputReceipt(
        ApparelWorkOrderSaveData order,
        PhysicalItemBatchDispositionReceipt receipt)
    {
        order.rejectedDismantleOperationId = receipt.OperationId;
        order.rejectedDismantleCommitId = receipt.CommitId;
        order.rejectedDismantleRequestFingerprint = receipt.RequestFingerprint;
        order.rejectedDismantleInputMassGrams = receipt.InputMassGrams;
        order.rejectedOutputConsumed = true;
    }

    private static bool ValidateReceipt(
        ApparelWorkOrderSaveData order,
        PhysicalItemBatchDispositionReceipt receipt,
        string operationId,
        out string failureReason)
    {
        bool valid = receipt.IsCommitted
            && receipt.Kind == PhysicalItemDispositionKind.Transfer
            && string.Equals(
                receipt.OperationId,
                operationId,
                StringComparison.Ordinal)
            && string.Equals(
                receipt.ReasonCode,
                ReasonCode,
                StringComparison.Ordinal)
            && string.Equals(
                receipt.RequestFingerprint,
                CreateRequestFingerprint(order.rejectedOutputStackId),
                StringComparison.Ordinal)
            && receipt.SourceStackIds.Count == 1
            && string.Equals(
                receipt.SourceStackIds[0],
                order.rejectedOutputStackId,
                StringComparison.Ordinal)
            && receipt.Quantity == 1
            && receipt.InputMassGrams > 0L
            && (string.IsNullOrEmpty(order.rejectedDismantleCommitId)
                || string.Equals(
                    receipt.CommitId,
                    order.rejectedDismantleCommitId,
                    StringComparison.Ordinal));
        failureReason = valid
            ? string.Empty
            : "apparel-rejected-dismantle-receipt-conflict";
        return valid;
    }

    private static WorldItemStackSnapshot[] CaptureRecoveryStacks(
        IWorldItemStackRuntime items,
        string commitId) => items.GetAllStacks()
        .Where(stack => stack != null
            && ProductionOutputCommitComponentCodec.Matches(
                stack.Components,
                commitId))
        .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
        .ToArray();

    private static bool ValidateRecoveryStacks(
        WorldItemStackSnapshot[] stacks,
        string itemId,
        int quantity,
        long expectedMass,
        Vector2Int position,
        string destinationId,
        IWorldItemStackRuntime items,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (stacks == null
            || stacks.Length == 0
            || stacks.Any(stack => !string.Equals(
                    stack.ItemId,
                    itemId,
                    StringComparison.Ordinal)
                || stack.State != WorldItemStackState.FacilityOutputBuffer
                || stack.Position != position
                || !string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            || stacks.Sum(stack => (long)stack.Quantity) != quantity)
        {
            failureReason = "apparel-rejected-recovery-existing-conflict";
            return false;
        }
        long actualMass = stacks.Sum(stack =>
            items.MassQuery.GetQuantityMass(
                    (ItemDefinitionId)stack.ItemId,
                    PhysicalItemMassSubjectAdapter.Create(
                        items.MassQuery,
                        (ItemDefinitionId)stack.ItemId,
                        stack.ItemInstanceId,
                        stack.Components),
                    stack.Quantity)
                .Value);
        if (actualMass != expectedMass)
        {
            failureReason = "apparel-rejected-recovery-mass-conflict";
            return false;
        }
        return true;
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool PlannedRecoveryFieldsEmpty(
        ApparelWorkOrderSaveData order) =>
        order.rejectedRecoveryPublicationAttempt == 0
        && string.IsNullOrEmpty(order.rejectedRecoveryOutcomeFingerprint)
        && string.IsNullOrEmpty(order.rejectedRecoveryAdmissionTokenId)
        && (order.rejectedRecoveryOutputCapability == null
            || order.rejectedRecoveryOutputCapability.IsEmpty)
        && string.IsNullOrEmpty(
            order.rejectedRecoveryMaximumMassProofDigest)
        && order.rejectedRecoveryMaximumBatchMassGrams == 0L
        && string.IsNullOrEmpty(order.rejectedRecoveryCapacitySourceDigest)
        && order.rejectedRecoveryRequiredMinimumCapacityGrams == 0L
        && string.IsNullOrEmpty(
            order.rejectedRecoveryPlannedOutputFingerprint)
        && (order.rejectedRecoveryStackIds?.Count ?? 0) == 0
        && !order.rejectedRecoveryAdmissionCommitted
        && !order.rejectedRecoveryOutputAcknowledged;

    private static bool CanonicalSortedUnique(IReadOnlyList<string> values)
    {
        if (values == null)
            return false;
        string previous = null;
        foreach (string value in values)
        {
            if (!Canonical(value)
                || previous != null
                && string.CompareOrdinal(previous, value) >= 0)
            {
                return false;
            }
            previous = value;
        }
        return true;
    }

    private static bool IsSha256(string value) => value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');

    private static string FormatPublicationOperationId(
        string orderId,
        int qualityAttempt,
        int publicationAttempt) =>
        $"{RecoveryOperationPrefix}{orderId}:"
        + $"{Math.Max(0, qualityAttempt):D4}:"
        + $"{Math.Max(0, publicationAttempt):D4}";
}
