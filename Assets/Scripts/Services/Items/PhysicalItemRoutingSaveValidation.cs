using System;
using System.Collections.Generic;
using System.Linq;
using static PhysicalItemSaveValidationRules;

internal static class PhysicalItemRoutingSaveValidation
{
    internal static void ValidatePendingProductionInputDestinationDrains(
        IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> values,
        DungeonGameRestoreReport report)
    {
        HashSet<string> stepOperations = new(StringComparer.Ordinal);
        HashSet<string> destinations = new(StringComparer.Ordinal);
        HashSet<string> bills = new(StringComparer.Ordinal);
        string previousOperation = string.Empty;
        foreach (ProductionInputDestinationCustodyDrainSaveData value in values)
        {
            string operation = value?.stepOperationId ?? string.Empty;
            bool valid = ProductionInputDestinationCustodyDrainContract
                .IsValidSave(value)
                && stepOperations.Add(operation)
                && destinations.Add(value.sourceDestinationId)
                && bills.Add(value.billId);
            if (!valid)
            {
                report.AddError(
                    "Invalid pending production input-destination drain '"
                    + operation + "'.");
                continue;
            }
            if (previousOperation.Length > 0
                && string.CompareOrdinal(previousOperation, operation) >= 0)
            {
                report.AddError(
                    "Pending production input-destination drains are not in canonical operation order.");
            }
            previousOperation = operation;
        }
    }

    internal static void ValidatePendingProductionCustodyDrains(
        IReadOnlyList<ProductionPhysicalCustodyDrainSaveData> values,
        DungeonGameRestoreReport report)
    {
        HashSet<string> operations = new(StringComparer.Ordinal);
        HashSet<string> destinations = new(StringComparer.Ordinal);
        string previousOperation = string.Empty;
        foreach (ProductionPhysicalCustodyDrainSaveData value in values)
        {
            string operation = value?.stepOperationId ?? string.Empty;
            bool terminal = value?.phase is
                ProductionPhysicalCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck
                or ProductionPhysicalCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc;
            bool prepared = value?.phase ==
                ProductionPhysicalCustodyDrainPhase.Prepared;
            bool progressValid = value != null
                && AreCanonicalSortedUnique(value.sourceStackIds)
                && value.sourceStackIds.Count > 0
                && AreCanonicalSortedUnique(value.sourceActorIds)
                && AreCanonicalSortedUnique(
                    value.sourceHaulIntentOperationIds)
                && AreCanonicalSortedUnique(value.completedActorIds)
                && AreCanonicalSortedUnique(
                    value.releasedHaulIntentOperationIds)
                && AreCanonicalSortedUnique(value.releasedStackIds)
                && IsSubset(
                    value.releasedHaulIntentOperationIds,
                    value.sourceHaulIntentOperationIds)
                && IsSubset(value.releasedStackIds, value.sourceStackIds);
            bool phaseProgressValid = value != null && value.phase switch
            {
                ProductionPhysicalCustodyDrainPhase.Prepared =>
                    value.completedActorIds.Count == 0
                    && value.releasedHaulIntentOperationIds.Count == 0
                    && value.releasedStackIds.Count == 0,
                ProductionPhysicalCustodyDrainPhase.ReleasingActors =>
                    IsPrefix(value.completedActorIds, value.sourceActorIds)
                    && value.releasedHaulIntentOperationIds.Count == 0
                    && value.releasedStackIds.Count == 0,
                ProductionPhysicalCustodyDrainPhase.ReleasingIntents =>
                    value.completedActorIds.SequenceEqual(
                        value.sourceActorIds,
                        StringComparer.Ordinal)
                    && IsPrefix(
                        value.releasedHaulIntentOperationIds,
                        value.sourceHaulIntentOperationIds)
                    && value.releasedStackIds.Count == 0,
                ProductionPhysicalCustodyDrainPhase.ReleasingDestination =>
                    value.completedActorIds.SequenceEqual(
                        value.sourceActorIds,
                        StringComparer.Ordinal)
                    && value.releasedHaulIntentOperationIds.SequenceEqual(
                        value.sourceHaulIntentOperationIds,
                        StringComparer.Ordinal)
                    && value.releasedStackIds.Count == 0,
                ProductionPhysicalCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck or
                ProductionPhysicalCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc =>
                    value.completedActorIds.SequenceEqual(
                        value.sourceActorIds,
                        StringComparer.Ordinal)
                    && value.releasedHaulIntentOperationIds.SequenceEqual(
                        value.sourceHaulIntentOperationIds,
                        StringComparer.Ordinal)
                    && value.releasedStackIds.SequenceEqual(
                        value.sourceStackIds,
                        StringComparer.Ordinal),
                _ => false
            };
            bool phasePayloadValid = value != null && (terminal
                ? value.releasedQuantity == value.inputQuantity
                    && value.releasedMassGrams == value.inputMassGrams
                    && value.releasedStackIds.Count ==
                        value.sourceStackIds.Count
                    && value.releasedHaulIntentOperationIds.Count ==
                        value.sourceHaulIntentOperationIds.Count
                    && IsLowerSha256(value.resultFingerprint)
                    && IsCanonicalNonEmpty(value.commitId)
                    && IsLowerSha256(value.receiptFingerprint)
                : value.releasedQuantity == 0
                    && value.releasedMassGrams == 0L
                    && string.IsNullOrEmpty(value.resultFingerprint)
                    && string.IsNullOrEmpty(value.commitId)
                    && string.IsNullOrEmpty(value.receiptFingerprint)
                    && (!prepared || phaseProgressValid));
            if (value == null
                || !Enum.IsDefined(
                    typeof(ProductionPhysicalCustodyDrainPhase),
                    value.phase)
                || !IsCanonicalNonEmpty(operation)
                || !IsCanonicalNonEmpty(value.sourceDestinationId)
                || !string.Equals(
                    value.ownerStableId,
                    "physical-destination:" + value.sourceDestinationId,
                    StringComparison.Ordinal)
                || !IsLowerSha256(value.requestFingerprint)
                || !IsLowerSha256(value.sourceOwnershipFingerprint)
                || value.inputQuantity <= 0
                || value.inputMassGrams <= 0L
                || !progressValid
                || !phaseProgressValid
                || !phasePayloadValid
                || !operations.Add(operation)
                || !destinations.Add(value.sourceDestinationId))
            {
                report.AddError(
                    "Invalid pending production physical custody drain '"
                    + operation + "'.");
                continue;
            }
            if (previousOperation.Length > 0
                && string.CompareOrdinal(previousOperation, operation) >= 0)
            {
                report.AddError(
                    "Pending production physical custody drains are not in canonical operation order.");
            }
            previousOperation = operation;
        }
    }

    internal static void ValidatePendingExactOutputRoutes(
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> values,
        DungeonGameRestoreReport report)
    {
        HashSet<string> operations = new(StringComparer.Ordinal);
        HashSet<string> physicalReceipts = new(StringComparer.Ordinal);
        Dictionary<string, string> routedStackOwners = new(StringComparer.Ordinal);
        string previousOperation = string.Empty;
        foreach (FacilityOutputExactRouteOutboxSaveData value in values)
        {
            string operation = value?.routeOperationId ?? string.Empty;
            if (value == null
                || !Enum.IsDefined(typeof(FacilityOutputExactRoutePhase), value.phase)
                || value.phase is not (FacilityOutputExactRoutePhase.PhysicalPending
                    or FacilityOutputExactRoutePhase.Routable)
                || !IsCanonicalNonEmpty(operation)
                || !IsLowerSha256(value.requestFingerprint)
                || !IsLowerSha256(value.physicalReceiptFingerprint)
                || !IsCanonicalNonEmpty(value.batchCommitId)
                || !IsCanonicalNonEmpty(value.sourceDestinationId)
                || !IsCanonicalText(value.targetDestinationId)
                || value.currentDeliveryRevision < 0L
                || !IsLowerSha256(value.currentDeliveryRevisionFingerprint)
                || !IsCanonicalText(value.currentDeliveryRerouteOperationId)
                || !IsCanonicalText(value.currentTargetDestinationId)
                || !IsCanonicalText(value.currentTargetAuthorityFingerprint)
                || (value.currentDeliveryRevision == 0L
                    ? !string.IsNullOrEmpty(
                            value.currentDeliveryRerouteOperationId)
                        || !string.IsNullOrEmpty(
                            value.currentTargetAuthorityFingerprint)
                        || !string.Equals(value.currentTargetDestinationId,
                            value.targetDestinationId, StringComparison.Ordinal)
                        || value.currentTargetPositionX != value.targetPositionX
                        || value.currentTargetPositionY != value.targetPositionY
                        || !string.Equals(
                            value.currentDeliveryRevisionFingerprint,
                            FacilityOutputExactRouteDeliveryRevisionFingerprint
                                .CreateInitial(
                                    value.routeOperationId,
                                    value.requestFingerprint,
                                    value.physicalReceiptFingerprint,
                                    value.targetDestinationId,
                                    value.targetPositionX,
                                    value.targetPositionY),
                            StringComparison.Ordinal)
                    : !IsCanonicalNonEmpty(
                            value.currentDeliveryRerouteOperationId)
                        || !IsCanonicalNonEmpty(
                            value.currentTargetDestinationId)
                        || !IsLowerSha256(
                            value.currentTargetAuthorityFingerprint))
                || value.totalQuantity <= 0
                || value.totalMassGrams <= 0L
                || value.slices == null
                || value.slices.Count == 0
                || !operations.Add(operation)
                || !physicalReceipts.Add(value.physicalReceiptFingerprint))
            {
                report.AddError($"Invalid pending exact output route '{operation}'.");
                continue;
            }
            if (previousOperation.Length > 0
                && string.CompareOrdinal(previousOperation, operation) >= 0)
            {
                report.AddError(
                    "Pending exact output routes are not in canonical operation order.");
            }
            previousOperation = operation;

            int quantity = 0;
            long massGrams = 0L;
            int nextSourceOffset = -1;
            string previousSliceKey = string.Empty;
            foreach (FacilityOutputExactRouteSliceSaveData slice in value.slices)
            {
                string sliceKey = slice == null
                    ? string.Empty
                    : slice.sourceOffsetQuantity.ToString("D10",
                        System.Globalization.CultureInfo.InvariantCulture)
                        + ":" + slice.sourceStackId + ":" + slice.routedStackId;
                if (slice == null
                    || !((ItemStackId)slice.sourceStackId).IsValid
                    || !((ItemStackId)slice.routedStackId).IsValid
                    || !IsCanonicalNonEmpty(slice.outputLineId)
                    || !IsCanonicalNonEmpty(slice.lineCommitId)
                    || !IsCanonicalNonEmpty(slice.itemId)
                    || slice.sourceOffsetQuantity < 0
                    || slice.routedOffsetQuantity < 0
                    || slice.routedQuantity <= 0
                    || slice.routedMassGrams <= 0L
                    || !IsLowerSha256(slice.componentFingerprint)
                    || routedStackOwners.TryGetValue(
                        slice.routedStackId,
                        out string routedOwner)
                        && !string.Equals(
                            routedOwner,
                            operation,
                            StringComparison.Ordinal)
                    || previousSliceKey.Length > 0
                        && string.CompareOrdinal(previousSliceKey, sliceKey) >= 0
                    || nextSourceOffset >= 0
                        && slice.sourceOffsetQuantity != nextSourceOffset)
                {
                    report.AddError(
                        $"Pending exact output route '{operation}' has an invalid, duplicate, overlapping, gapped, or unordered slice.");
                    continue;
                }
                routedStackOwners[slice.routedStackId] = operation;
                previousSliceKey = sliceKey;
                nextSourceOffset = checked(
                    slice.sourceOffsetQuantity + slice.routedQuantity);
                quantity = checked(quantity + slice.routedQuantity);
                massGrams = checked(massGrams + slice.routedMassGrams);
            }
            if (quantity != value.totalQuantity
                || massGrams != value.totalMassGrams)
            {
                report.AddError(
                    $"Pending exact output route '{operation}' totals conflict with its slices.");
            }
            foreach (IGrouping<string, FacilityOutputExactRouteSliceSaveData>
                     routed in value.slices
                         .Where(slice => slice != null)
                         .GroupBy(slice => slice.routedStackId,
                             StringComparer.Ordinal))
            {
                int nextRoutedOffset = 0;
                foreach (FacilityOutputExactRouteSliceSaveData slice in routed
                             .OrderBy(slice => slice.routedOffsetQuantity)
                             .ThenBy(slice => slice.sourceOffsetQuantity)
                             .ThenBy(slice => slice.sourceStackId,
                                 StringComparer.Ordinal))
                {
                    if (slice.routedOffsetQuantity != nextRoutedOffset)
                    {
                        report.AddError(
                            $"Pending exact output route '{operation}' has an overlapping or gapped routed-stack range '{routed.Key}'.");
                        break;
                    }
                    nextRoutedOffset = checked(
                        nextRoutedOffset + slice.routedQuantity);
                }
            }
        }
    }
}
