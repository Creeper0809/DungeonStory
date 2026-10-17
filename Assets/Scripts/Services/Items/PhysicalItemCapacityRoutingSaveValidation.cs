using System;
using System.Collections.Generic;
using System.Linq;
using static PhysicalItemSaveValidationRules;

internal static class PhysicalItemCapacityRoutingSaveValidation
{
    internal static void ValidatePendingCapacityRoutingDrains(
        DungeonPhysicalItemSaveData snapshot,
        DungeonGameRestoreReport report)
    {
        IReadOnlyList<ProductionCapacityRoutingDrainSaveData> values =
            snapshot.pendingCapacityRoutingDrains;
        HashSet<string> operations = new(StringComparer.Ordinal);
        HashSet<string> batches = new(StringComparer.Ordinal);
        HashSet<string> ownedRoutes = new(StringComparer.Ordinal);
        HashSet<string> ownedCarryStacks = new(StringComparer.Ordinal);
        string previousOperation = string.Empty;
        foreach (ProductionCapacityRoutingDrainSaveData value in values)
        {
            string operation = value?.stepOperationId ?? string.Empty;
            if (!ValidateCapacityRoutingDrainRecord(
                    value,
                    operations,
                    batches,
                    ownedRoutes,
                    ownedCarryStacks))
            {
                report.AddError(
                    "Invalid pending production capacity-routing drain contract '"
                    + operation + "'.");
                continue;
            }
            if (!ValidateStableCapacityRoutingExternalState(
                    value,
                    snapshot,
                    out string externalFailureReason))
            {
                report.AddError(
                    "Pending production capacity-routing drain has unstable "
                    + "physical state '" + operation + "': "
                    + externalFailureReason + ".");
                continue;
            }
            if (previousOperation.Length > 0
                && string.CompareOrdinal(previousOperation, operation) >= 0)
            {
                report.AddError(
                    "Pending production capacity-routing drains are not in canonical operation order.");
            }
            previousOperation = operation;
        }
    }

    private static bool ValidateStableCapacityRoutingExternalState(
        ProductionCapacityRoutingDrainSaveData drain,
        DungeonPhysicalItemSaveData snapshot,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (drain == null
            || drain.phase < ProductionCapacityRoutingDrainPhase
                .AwaitingStablePhysicalState)
        {
            return true;
        }
        if (drain.phase == ProductionCapacityRoutingDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
        {
            return ValidateTerminalCapacityRoutingPhysicalSuccessor(
                drain,
                snapshot,
                out failureReason);
        }

        string[] releasedOperations = (drain.actorAuthorityReleases
                ?? new List<
                    ProductionCapacityRoutingActorAuthorityReleaseSaveData>())
            .Where(value => value != null && value.effectsCommitted)
            .SelectMany(value => value.operationIds ?? new List<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if ((snapshot.reservationIntents
                ?? new List<ItemReservationIntentSaveData>())
            .Any(intent => intent != null
                && releasedOperations.Contains(
                    intent.ownerOperationId,
                    StringComparer.Ordinal)))
        {
            failureReason = "released-operation-reservation-remains";
            return false;
        }

        Dictionary<string, WorldItemStackSaveData[]> stacksById =
            (snapshot.stacks ?? new List<WorldItemStackSaveData>())
            .Where(value => value != null)
            .GroupBy(value => value.stackId ?? string.Empty,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);
        foreach (ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt in
                 drain.actorQuiesceReceipts)
        {
            ProductionCapacityRoutingDrainActorCarrySaveData[] carries =
                drain.sourceActorCarries
                    .Where(value => value != null
                        && string.Equals(
                            value.actorPersistentId,
                            receipt.actorPersistentId,
                            StringComparison.Ordinal))
                    .OrderBy(value => value.carriedStackId,
                        StringComparer.Ordinal)
                    .ThenBy(value => value.haulIntentOperationId,
                        StringComparer.Ordinal)
                    .ToArray();
            List<WorldItemStackSaveData> physicalRows = new(carries.Length);
            foreach (ProductionCapacityRoutingDrainActorCarrySaveData carry in
                     carries)
            {
                ProductionCapacityRoutingDrainSliceSaveData[] matchingSlices =
                    drain.sourceSlices.Where(slice => slice != null
                        && string.Equals(
                            slice.routeOperationId,
                            carry.routeOperationId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            slice.routedStackId,
                            carry.carriedStackId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            slice.sourceStackId,
                            carry.sourceStackId,
                            StringComparison.Ordinal)).ToArray();
                if (matchingSlices.Length != 1
                    || !stacksById.TryGetValue(
                        carry.carriedStackId,
                        out WorldItemStackSaveData[] matchingStacks)
                    || matchingStacks.Length != 1)
                {
                    failureReason =
                        "actor-carry-physical-stack-or-slice-cardinality";
                    return false;
                }
                ProductionCapacityRoutingDrainSliceSaveData slice =
                    matchingSlices[0];
                WorldItemStackSaveData stack = matchingStacks[0];
                if (stack.state != WorldItemStackState.Loose
                    || stack.gridX != receipt.physicalCellX
                    || stack.gridY != receipt.physicalCellY
                    || stack.quantity != carry.quantity
                    || !string.Equals(
                        stack.itemId,
                        slice.itemId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ProductionCapacityRoutingDrainFingerprint
                            .CreateActorCarryStackSignature(
                                stack.itemId,
                                stack.itemInstanceId,
                                stack.components),
                        carry.stackSignature,
                        StringComparison.Ordinal)
                    || string.IsNullOrEmpty(stack.destinationId)
                    || !stack.hasDestinationPosition
                    || stack.dropDisposition != WorldItemDropDisposition.None
                    || !string.IsNullOrEmpty(stack.recoveryOwnerOperationId)
                    || !string.IsNullOrEmpty(stack.recoverySourceStackId)
                    || !string.IsNullOrEmpty(
                        stack.recoveryCarrierPersistentId)
                    || stack.recoveryInterruptionKind !=
                        WorldItemCarryInterruptionKind.None
                    || stack.droppedAtGameTime != 0d
                    || stack.recoveryDeadlineGameTime != 0d
                    || !FacilityOutputExactRouteCustodyCodec.TryRead(
                        stack.components,
                        out FacilityOutputExactRouteCustodyMetadata custody)
                    || custody.Phase !=
                        FacilityOutputExactRouteCustodyPhase.Routable
                    || !string.Equals(
                        custody.BatchCommitId,
                        drain.batchCommitId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        custody.RouteOperationId,
                        carry.routeOperationId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        custody.CurrentSourceStackId,
                        carry.sourceStackId,
                        StringComparison.Ordinal)
                    || custody.Quantity != carry.quantity
                    || custody.MassGrams != carry.massGrams
                    || !string.Equals(
                        custody.ComponentFingerprint,
                        slice.componentFingerprint,
                        StringComparison.Ordinal))
                {
                    failureReason = "actor-carry-physical-state-mismatch";
                    return false;
                }
                string target = !string.IsNullOrEmpty(
                        custody.CurrentTargetDestinationId)
                    ? custody.CurrentTargetDestinationId
                    : custody.TargetDestinationId;
                if (!string.Equals(
                        stack.destinationId,
                        target,
                        StringComparison.Ordinal)
                    || !string.IsNullOrEmpty(
                        custody.CurrentTargetDestinationId)
                    && (stack.destinationGridX !=
                            custody.CurrentTargetPosition.x
                        || stack.destinationGridY !=
                            custody.CurrentTargetPosition.y))
                {
                    failureReason = "actor-carry-target-mismatch";
                    return false;
                }
                physicalRows.Add(stack);
            }
            if (!string.Equals(
                    ProductionCapacityRoutingActorPhysicalFingerprint.Create(
                        physicalRows),
                    receipt.postPhysicalFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason = "actor-carry-post-physical-fingerprint-mismatch";
                return false;
            }
        }
        return true;
    }

    private static bool ValidateTerminalCapacityRoutingPhysicalSuccessor(
        ProductionCapacityRoutingDrainSaveData drain,
        DungeonPhysicalItemSaveData snapshot,
        out string failureReason)
    {
        failureReason = string.Empty;
        ProductionPhysicalCustodyDrainSaveData[] successors =
            (snapshot.pendingProductionCustodyDrains
                    ?? new List<ProductionPhysicalCustodyDrainSaveData>())
                .Where(value => value != null
                    && string.Equals(
                        value.sourceDestinationId,
                        drain.sourceDestinationId,
                        StringComparison.Ordinal)
                    && value.phase is ProductionPhysicalCustodyDrainPhase
                            .EffectCommittedAwaitingOwnerAck
                        or ProductionPhysicalCustodyDrainPhase
                            .OwnerAcknowledgedAwaitingCheckpointGc)
                .ToArray();
        if (successors.Length != 1)
        {
            failureReason = "terminal-physical-successor-cardinality";
            return false;
        }

        ProductionPhysicalCustodyDrainSaveData successor = successors[0];
        HashSet<string> successorSourceStacks = (successor.sourceStackIds
                ?? new List<string>()).ToHashSet(StringComparer.Ordinal);
        HashSet<string> successorReleasedStacks = (successor.releasedStackIds
                ?? new List<string>()).ToHashSet(StringComparer.Ordinal);
        HashSet<string> successorSourceActors = (successor.sourceActorIds
                ?? new List<string>()).ToHashSet(StringComparer.Ordinal);
        HashSet<string> successorCompletedActors = (successor.completedActorIds
                ?? new List<string>()).ToHashSet(StringComparer.Ordinal);
        HashSet<string> successorSourceIntents =
            (successor.sourceHaulIntentOperationIds ?? new List<string>())
                .ToHashSet(StringComparer.Ordinal);
        HashSet<string> successorReleasedIntents =
            (successor.releasedHaulIntentOperationIds ?? new List<string>())
                .ToHashSet(StringComparer.Ordinal);
        string[] preserved = (drain.preservedStackIds ?? new List<string>())
            .ToArray();
        HashSet<string> preservedSet = preserved.ToHashSet(
            StringComparer.Ordinal);
        string[] actors = (drain.sourceActorCarries
                ?? new List<ProductionCapacityRoutingDrainActorCarrySaveData>())
            .Where(value => value != null)
            .Select(value => value.actorPersistentId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        HashSet<string> actorSet = actors.ToHashSet(StringComparer.Ordinal);
        string[] intents = (drain.sourceActorCarries
                ?? new List<ProductionCapacityRoutingDrainActorCarrySaveData>())
            .Where(value => value != null)
            .Select(value => value.haulIntentOperationId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        HashSet<string> intentSet = intents.ToHashSet(StringComparer.Ordinal);
        if (!successorSourceStacks.SetEquals(preservedSet)
            || !successorReleasedStacks.SetEquals(preservedSet)
            || !successorSourceActors.SetEquals(actorSet)
            || !successorCompletedActors.SetEquals(actorSet)
            || !successorSourceIntents.SetEquals(intentSet)
            || !successorReleasedIntents.SetEquals(intentSet)
            || successor.releasedQuantity != drain.preservedQuantity
            || successor.releasedMassGrams != drain.preservedMassGrams)
        {
            failureReason = "terminal-physical-successor-vector-mismatch";
            return false;
        }

        int currentQuantity = 0;
        foreach (string stackId in preserved)
        {
            WorldItemStackSaveData[] stacks = (snapshot.stacks
                    ?? new List<WorldItemStackSaveData>())
                .Where(value => value != null && string.Equals(
                    value.stackId,
                    stackId,
                    StringComparison.Ordinal))
                .ToArray();
            ProductionCapacityRoutingDrainSliceSaveData[] slices =
                (drain.sourceSlices
                        ?? new List<ProductionCapacityRoutingDrainSliceSaveData>())
                    .Where(value => value != null && string.Equals(
                        value.routedStackId,
                        stackId,
                        StringComparison.Ordinal))
                    .ToArray();
            if (stacks.Length != 1)
            {
                failureReason =
                    "terminal-physical-successor-stack-or-slice-cardinality";
                return false;
            }

            WorldItemStackSaveData stack = stacks[0];
            try
            {
                currentQuantity = checked(currentQuantity + stack.quantity);
            }
            catch (OverflowException)
            {
                failureReason = "terminal-physical-successor-quantity-overflow";
                return false;
            }

            if (slices.Length > 0)
            {
                int expectedQuantity;
                try
                {
                    expectedQuantity = slices.Sum(
                        value => value.routedQuantity);
                }
                catch (OverflowException)
                {
                    failureReason =
                        "terminal-physical-successor-slice-quantity-overflow";
                    return false;
                }
                if (slices.Select(value => value.itemId)
                        .Distinct(StringComparer.Ordinal).Count() != 1
                    || stack.quantity != expectedQuantity
                    || !string.Equals(
                        stack.itemId,
                        slices[0].itemId,
                        StringComparison.Ordinal))
                {
                    failureReason =
                        "terminal-physical-successor-slice-mismatch";
                    return false;
                }
            }

            bool retainsDestroyedOriginCustody =
                FacilityOutputExactRouteCustodyCodec.TryRead(
                    stack.components,
                    out FacilityOutputExactRouteCustodyMetadata custody)
                && string.Equals(
                    custody.OriginDestinationId,
                    drain.sourceDestinationId,
                    StringComparison.Ordinal);
            if (stack.quantity <= 0
                || stack.state is WorldItemStackState.Carried
                    or WorldItemStackState.InTransit
                || string.Equals(
                    stack.destinationId,
                    drain.sourceDestinationId,
                    StringComparison.Ordinal)
                || string.Equals(
                    stack.sourceStorageDestinationId,
                    drain.sourceDestinationId,
                    StringComparison.Ordinal)
                || retainsDestroyedOriginCustody)
            {
                failureReason = "terminal-physical-successor-stack-mismatch";
                return false;
            }

            ProductionCapacityRoutingDrainActorCarrySaveData[] carries =
                (drain.sourceActorCarries
                        ?? new List<
                            ProductionCapacityRoutingDrainActorCarrySaveData>())
                    .Where(value => value != null && string.Equals(
                        value.carriedStackId,
                        stackId,
                        StringComparison.Ordinal))
                    .ToArray();
            if (carries.Length > 1
                || carries.Length == 1
                && carries[0].quantity != stack.quantity)
            {
                failureReason =
                    "terminal-physical-successor-actor-carry-mismatch";
                return false;
            }
        }
        if (currentQuantity != drain.preservedQuantity)
        {
            failureReason =
                "terminal-physical-successor-current-quantity-mismatch";
            return false;
        }
        return true;
    }

    private static bool ValidateCapacityRoutingDrainRecord(
        ProductionCapacityRoutingDrainSaveData value,
        ISet<string> operations,
        ISet<string> batches,
        ISet<string> ownedRoutes,
        ISet<string> ownedCarryStacks)
    {
        if (value == null
            || !Enum.IsDefined(
                typeof(ProductionCapacityRoutingDrainPhase),
                value.phase)
            || !IsCanonicalNonEmpty(value.stepOperationId)
            || !operations.Add(value.stepOperationId)
            || !IsCanonicalNonEmpty(value.facilityId)
            || !IsCanonicalNonEmpty(value.sourceDestinationId)
            || !IsCanonicalNonEmpty(value.batchCommitId)
            || !batches.Add(value.batchCommitId)
            || !string.Equals(
                value.ownerStableId,
                "routing-batch:" + value.batchCommitId,
                StringComparison.Ordinal)
            || !IsLowerSha256(value.sourceOutcomeFingerprint)
            || !IsLowerSha256(value.sourceRoutingFingerprint)
            || !IsLowerSha256(value.sourceOwnershipFingerprint)
            || !IsLowerSha256(value.requestFingerprint)
            || value.inputQuantity <= 0
            || value.inputMassGrams <= 0L
            || value.sourceLines == null
            || value.sourceRoutes == null
            || value.sourceSlices == null
            || value.sourceActorCarries == null
            || value.actorQuiesceReceipts == null
            || value.actorAuthorityReleases == null
            || !AreCanonicalSortedUnique(value.sourceCustodyStackIds)
            || value.sourceCustodyStackIds.Count == 0
            || !AreCanonicalSortedUnique(value.completedLineCommitIds)
            || !AreCanonicalSortedUnique(value.finalRouteOperationIds)
            || !AreCanonicalSortedUnique(value.preservedStackIds)
            || !AreCanonicalSortedUnique(value.actorQuiesceReceipts
                .Select(ProductionCapacityRoutingDrainFingerprint
                    .ActorQuiesceReceiptKey).ToArray())
            || !AreCanonicalSortedUnique(value.actorAuthorityReleases
                .Select(release => release?.actorPersistentId ?? string.Empty)
                .ToArray())
            || !AreCanonicalSortedUnique(value.releasedHaulIntentOperationIds)
            || !AreCanonicalSortedUnique(value.stablePhysicalStackIds))
        {
            return false;
        }

        string[] lineIds = value.sourceLines
            .Select(line => line?.lineCommitId ?? string.Empty)
            .ToArray();
        string[] sourceRouteIds = value.sourceRoutes
            .Select(route => route?.routeOperationId ?? string.Empty)
            .ToArray();
        string[] sourceSliceKeys = value.sourceSlices
            .Select(ProductionCapacityRoutingDrainFingerprint.SliceKey)
            .ToArray();
        string[] sourceCarryKeys = value.sourceActorCarries
            .Select(ProductionCapacityRoutingDrainFingerprint.ActorCarryKey)
            .ToArray();
        if (!AreCanonicalSortedUnique(lineIds)
            || lineIds.Length == 0
            || !AreCanonicalSortedUnique(sourceRouteIds)
            || !AreCanonicalSortedUnique(sourceSliceKeys)
            || !AreCanonicalSortedUnique(sourceCarryKeys)
            || sourceRouteIds.Any(routeId => !ownedRoutes.Add(routeId)))
        {
            return false;
        }

        HashSet<string> routeIds = sourceRouteIds.ToHashSet(
            StringComparer.Ordinal);
        HashSet<string> lineIdSet = lineIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> custodyStackIds = value.sourceCustodyStackIds
            .ToHashSet(StringComparer.Ordinal);
        int inputQuantity = 0;
        long inputMass = 0L;
        try
        {
            foreach (ProductionCapacityRoutingDrainLineSaveData line in
                     value.sourceLines)
            {
                if (line == null
                    || !IsCanonicalNonEmpty(line.outputLineId)
                    || !IsCanonicalNonEmpty(line.itemId)
                    || !IsLowerSha256(line.componentFingerprint)
                    || !IsCanonicalNonEmpty(line.outputCapabilityId)
                    || line.outputCapabilityVersion <= 0
                    || !IsCanonicalNonEmpty(line.outputComponentCodecId)
                    || line.outputComponentCodecVersion <= 0
                    || !IsLowerSha256(line.outputCapabilityFingerprint)
                    || !string.Equals(
                        line.outputCapabilityFingerprint,
                        ProductionOutputCapabilityDescriptorFingerprint.Capture(
                            line.outputLineId,
                            line.itemId,
                            line.outputCapabilityId,
                            line.outputCapabilityVersion,
                            line.outputComponentCodecId,
                            line.outputComponentCodecVersion),
                        StringComparison.Ordinal)
                    || line.originalQuantity <= 0
                    || line.originalMassGrams <= 0L
                    || line.remainingQuantity < 0
                    || line.remainingMassGrams < 0L
                    || line.routedQuantity < 0
                    || line.routedMassGrams < 0L
                    || line.originalQuantity != checked(
                        line.remainingQuantity + line.routedQuantity)
                    || line.originalMassGrams != checked(
                        line.remainingMassGrams + line.routedMassGrams))
                {
                    return false;
                }
                inputQuantity = checked(inputQuantity + line.originalQuantity);
                inputMass = checked(inputMass + line.originalMassGrams);
            }
        }
        catch (OverflowException)
        {
            return false;
        }
        if (inputQuantity != value.inputQuantity
            || inputMass != value.inputMassGrams)
        {
            return false;
        }

        foreach (ProductionCapacityRoutingDrainRouteSaveData route in
                 value.sourceRoutes)
        {
            if (route == null
                || !IsLowerSha256(route.requestFingerprint)
                || route.phase is < 1 or > 3
                || (route.phase == 1
                    ? !string.IsNullOrEmpty(route.physicalReceiptFingerprint)
                    : !IsLowerSha256(route.physicalReceiptFingerprint))
                || route.currentDeliveryRevision < 0L
                || !IsLowerSha256(route.currentDeliveryRevisionFingerprint)
                || !IsCanonicalText(route.currentTargetDestinationId)
                || !IsCanonicalText(route.currentTargetAuthorityFingerprint))
            {
                return false;
            }
        }
        foreach (ProductionCapacityRoutingDrainSliceSaveData slice in
                 value.sourceSlices)
        {
            if (slice == null
                || !routeIds.Contains(slice.routeOperationId)
                || !custodyStackIds.Contains(slice.routedStackId)
                || !IsCanonicalNonEmpty(slice.sourceStackId)
                || !IsCanonicalNonEmpty(slice.routedStackId)
                || !IsCanonicalNonEmpty(slice.outputLineId)
                || !lineIdSet.Contains(slice.lineCommitId)
                || !IsCanonicalNonEmpty(slice.itemId)
                || slice.sourceOffsetQuantity < 0
                || slice.routedOffsetQuantity < 0
                || slice.routedQuantity <= 0
                || slice.routedMassGrams <= 0L
                || !IsLowerSha256(slice.componentFingerprint))
            {
                return false;
            }
        }
        foreach (ProductionCapacityRoutingDrainActorCarrySaveData carry in
                 value.sourceActorCarries)
        {
            if (carry == null
                || !IsCanonicalNonEmpty(carry.actorPersistentId)
                || !IsCanonicalNonEmpty(carry.haulIntentOperationId)
                || !routeIds.Contains(carry.routeOperationId)
                || !custodyStackIds.Contains(carry.carriedStackId)
                || !ownedCarryStacks.Add(carry.carriedStackId)
                || !IsCanonicalNonEmpty(carry.sourceStackId)
                || carry.quantity <= 0
                || carry.massGrams <= 0L
                || !IsLowerSha256(carry.stackSignature))
            {
                return false;
            }
        }

        string[] actorIds = value.sourceActorCarries
            .Select(carry => carry.actorPersistentId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();
        string[] intentIds = value.sourceActorCarries
            .Select(carry => carry.haulIntentOperationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();
        foreach (ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt in
                 value.actorQuiesceReceipts)
        {
            if (receipt == null
                || !actorIds.Contains(
                    receipt.actorPersistentId,
                    StringComparer.Ordinal)
                || !string.Equals(
                    receipt.batchCommitId,
                    value.batchCommitId,
                    StringComparison.Ordinal)
                || !AreCanonicalSortedUnique(receipt.carriedRowKeys)
                || receipt.carriedRowKeys.Count == 0
                || !AreCanonicalSortedUnique(receipt.quantityLeaseIds)
                || receipt.quantityLeaseIds.Count == 0
                || !AreCanonicalSortedUnique(
                    receipt.warehouseAdmissionTokenIds)
                || !IsLowerSha256(receipt.activePlanFingerprint)
                || !IsLowerSha256(receipt.prePhysicalFingerprint)
                || !IsLowerSha256(receipt.postPhysicalFingerprint)
                || !IsLowerSha256(receipt.receiptFingerprint)
                || !receipt.carriedRowKeys.SequenceEqual(
                    value.sourceActorCarries
                        .Where(carry => string.Equals(
                            carry.actorPersistentId,
                            receipt.actorPersistentId,
                            StringComparison.Ordinal))
                        .Select(ProductionCapacityRoutingDrainFingerprint
                            .ActorCarryKey)
                        .OrderBy(key => key, StringComparer.Ordinal),
                    StringComparer.Ordinal)
                || !string.Equals(
                    receipt.receiptFingerprint,
                    ProductionCapacityRoutingDrainFingerprint
                        .CreateActorQuiesceReceiptFingerprint(
                            value.stepOperationId,
                            value.requestFingerprint,
                            receipt),
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        string[] quiescedActorIds = value.actorQuiesceReceipts
            .Select(receipt => receipt.actorPersistentId)
            .ToArray();
        List<string> committedAuthorityOperations = new();
        bool encounteredPreparedAuthorityRelease = false;
        foreach (ProductionCapacityRoutingActorAuthorityReleaseSaveData release in
                 value.actorAuthorityReleases)
        {
            ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt =
                value.actorQuiesceReceipts.FirstOrDefault(candidate =>
                    candidate != null
                    && string.Equals(
                        candidate.actorPersistentId,
                        release?.actorPersistentId,
                        StringComparison.Ordinal));
            string[] expectedOperations = value.sourceActorCarries
                .Where(carry => carry != null
                    && string.Equals(
                        carry.actorPersistentId,
                        release?.actorPersistentId,
                        StringComparison.Ordinal))
                .Select(carry => carry.haulIntentOperationId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(operationId => operationId, StringComparer.Ordinal)
                .ToArray();
            if (!ValidateCapacityActorAuthorityRelease(
                    value,
                    release,
                    receipt,
                    expectedOperations))
            {
                return false;
            }
            if (encounteredPreparedAuthorityRelease)
                return false;
            if (release.effectsCommitted)
                committedAuthorityOperations.AddRange(release.operationIds);
            else
                encounteredPreparedAuthorityRelease = true;
        }
        string[] releaseActorIds = value.actorAuthorityReleases
            .Select(release => release.actorPersistentId)
            .ToArray();
        string[] expectedReleasedIntentIds = committedAuthorityOperations
            .Distinct(StringComparer.Ordinal)
            .OrderBy(operationId => operationId, StringComparer.Ordinal)
            .ToArray();
        bool authorityReleaseProgressValid = IsPrefix(releaseActorIds, actorIds)
            && value.releasedHaulIntentOperationIds.SequenceEqual(
                expectedReleasedIntentIds,
                StringComparer.Ordinal);
        bool sourceProgressValid = IsPrefix(
                value.completedLineCommitIds,
                lineIds)
            && IsPrefix(quiescedActorIds, actorIds)
            && authorityReleaseProgressValid
            && IsPrefix(
                value.stablePhysicalStackIds,
                value.preservedStackIds)
            && (value.finalRouteOperationIds.Count == 0
                || IsSubset(sourceRouteIds, value.finalRouteOperationIds));
        if (!sourceProgressValid)
            return false;

        bool linesComplete = value.completedLineCommitIds.SequenceEqual(
            lineIds,
            StringComparer.Ordinal);
        bool actorsComplete = quiescedActorIds.SequenceEqual(
            actorIds,
            StringComparer.Ordinal);
        bool intentsComplete = value.releasedHaulIntentOperationIds
            .SequenceEqual(intentIds, StringComparer.Ordinal);
        bool authorityReleasesComplete = releaseActorIds.SequenceEqual(
                actorIds,
                StringComparer.Ordinal)
            && value.actorAuthorityReleases.All(release =>
                release.effectsCommitted && release.actorPlanFinalized);
        bool stableComplete = value.stablePhysicalStackIds.SequenceEqual(
            value.preservedStackIds,
            StringComparer.Ordinal);
        bool finalVectorsPresent = value.finalRouteOperationIds.Count > 0
            && value.preservedStackIds.Count > 0;
        bool terminal = value.phase is ProductionCapacityRoutingDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionCapacityRoutingDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;
        bool phaseProgressValid = value.phase switch
        {
            ProductionCapacityRoutingDrainPhase.Prepared =>
                value.completedLineCommitIds.Count == 0
                && value.finalRouteOperationIds.Count == 0
                && value.preservedStackIds.Count == 0
                && value.actorQuiesceReceipts.Count == 0
                && value.actorAuthorityReleases.Count == 0
                && value.releasedHaulIntentOperationIds.Count == 0
                && value.stablePhysicalStackIds.Count == 0,
            ProductionCapacityRoutingDrainPhase.RoutingRemainder =>
                value.finalRouteOperationIds.Count == 0
                && value.preservedStackIds.Count == 0
                && value.actorQuiesceReceipts.Count == 0
                && value.actorAuthorityReleases.Count == 0
                && value.releasedHaulIntentOperationIds.Count == 0
                && value.stablePhysicalStackIds.Count == 0,
            // These phases are one synchronous runtime transaction. Saving
            // either would expose Loose physical cargo while lease/admission/
            // intent or frozen Ability authority is only partly retired.
            ProductionCapacityRoutingDrainPhase.QuiescingActors => false,
            ProductionCapacityRoutingDrainPhase.ReleasingOperationAuthority =>
                false,
            ProductionCapacityRoutingDrainPhase.AwaitingStablePhysicalState =>
                linesComplete && finalVectorsPresent && actorsComplete
                && intentsComplete && authorityReleasesComplete,
            ProductionCapacityRoutingDrainPhase.AwaitingDurableCheckpointGc
                or ProductionCapacityRoutingDrainPhase
                    .EffectCommittedAwaitingOwnerAck
                or ProductionCapacityRoutingDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc =>
                linesComplete && finalVectorsPresent && actorsComplete
                && intentsComplete && authorityReleasesComplete
                && stableComplete,
            _ => false
        };
        if (!phaseProgressValid)
            return false;

        bool terminalPayloadValid = terminal
            ? string.Equals(
                    value.observedRemovedBatchCommitId,
                    value.batchCommitId,
                    StringComparison.Ordinal)
                && value.preservedQuantity == value.inputQuantity
                && value.preservedMassGrams == value.inputMassGrams
                && IsLowerSha256(value.resultFingerprint)
                && string.Equals(
                    value.commitId,
                    ProductionCapacityRoutingDrainFingerprint.CreateCommitId(
                        value.stepOperationId,
                        value.requestFingerprint),
                    StringComparison.Ordinal)
                && IsLowerSha256(value.receiptFingerprint)
                && string.Equals(
                    value.receiptFingerprint,
                    ProductionCapacityRoutingDrainFingerprint.CreateReceipt(value),
                    StringComparison.Ordinal)
            : value.preservedQuantity == 0
                && value.preservedMassGrams == 0L
                && string.IsNullOrEmpty(value.observedRemovedBatchCommitId)
                && string.IsNullOrEmpty(value.resultFingerprint)
                && string.IsNullOrEmpty(value.commitId)
                && string.IsNullOrEmpty(value.receiptFingerprint);
        return terminalPayloadValid
            && string.Equals(
                value.requestFingerprint,
                ProductionCapacityRoutingDrainFingerprint.CreateRequest(
                    value.stepOperationId,
                    value.ownerStableId,
                    value.facilityId,
                    value.sourceDestinationId,
                    value.batchCommitId,
                    value.sourceOutcomeFingerprint,
                    value.sourceRoutingFingerprint,
                    value.sourceOwnershipFingerprint,
                    value.sourceLines,
                    value.sourceRoutes,
                    value.sourceSlices,
                    value.sourceActorCarries,
                    value.sourceCustodyStackIds,
                    value.inputQuantity,
                    value.inputMassGrams),
                StringComparison.Ordinal);
    }

    private static bool ValidateCapacityActorAuthorityRelease(
        ProductionCapacityRoutingDrainSaveData drain,
        ProductionCapacityRoutingActorAuthorityReleaseSaveData release,
        ProductionCapacityRoutingActorQuiesceReceiptSaveData receipt,
        IReadOnlyList<string> expectedOperations)
    {
        if (release == null
            || receipt == null
            || !string.Equals(
                release.actorQuiesceReceiptFingerprint,
                receipt.receiptFingerprint,
                StringComparison.Ordinal)
            || !AreCanonicalSortedUnique(release.operationIds)
            || !release.operationIds.SequenceEqual(
                expectedOperations,
                StringComparer.Ordinal)
            || release.operations == null
            || release.operations.Count != release.operationIds.Count
            || !release.operations.Select(row => row?.operationId ?? string.Empty)
                .SequenceEqual(release.operationIds, StringComparer.Ordinal)
            || !IsLowerSha256(release.activePlanFingerprint)
            || !string.Equals(
                release.activePlanFingerprint,
                receipt.activePlanFingerprint,
                StringComparison.Ordinal)
            || !IsLowerSha256(release.planFingerprint))
        {
            return false;
        }

        foreach (ProductionCapacityRoutingOperationAuthorityRowSaveData row in
                 release.operations)
        {
            if (row == null
                || !IsCanonicalNonEmpty(row.operationId)
                || !AreCanonicalSortedUnique(row.quantityLeaseIds)
                || row.quantityLeaseIds.Count == 0
                || !AreCanonicalSortedUnique(row.warehouseAdmissionTokenIds)
                || !IsLowerSha256(row.haulIntentFingerprint))
            {
                return false;
            }
        }
        string[] releaseLeaseIds = release.operations
            .SelectMany(row => row.quantityLeaseIds)
            .OrderBy(leaseId => leaseId, StringComparer.Ordinal)
            .ToArray();
        string[] releaseAdmissionIds = release.operations
            .SelectMany(row => row.warehouseAdmissionTokenIds)
            .OrderBy(tokenId => tokenId, StringComparer.Ordinal)
            .ToArray();
        if (!releaseLeaseIds.SequenceEqual(
                receipt.quantityLeaseIds,
                StringComparer.Ordinal)
            || !releaseAdmissionIds.SequenceEqual(
                receipt.warehouseAdmissionTokenIds,
                StringComparer.Ordinal)
            || !string.Equals(
                release.planFingerprint,
                ProductionCapacityRoutingDrainFingerprint
                    .CreateActorAuthorityReleasePlanFingerprint(
                        drain.stepOperationId,
                        drain.requestFingerprint,
                        release),
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!release.effectsCommitted)
        {
            return !release.actorPlanFinalized
                && string.IsNullOrEmpty(release.effectFingerprint)
                && string.IsNullOrEmpty(release.receiptFingerprint);
        }
        string expectedEffect = ProductionCapacityRoutingDrainFingerprint
            .CreateActorAuthorityReleaseEffectFingerprint(
                release.planFingerprint,
                actorPlanFinalized: true);
        return release.actorPlanFinalized
            && string.Equals(
                release.effectFingerprint,
                expectedEffect,
                StringComparison.Ordinal)
            && string.Equals(
                release.receiptFingerprint,
                ProductionCapacityRoutingDrainFingerprint
                    .CreateActorAuthorityReleaseReceiptFingerprint(
                        release.planFingerprint,
                        expectedEffect),
                StringComparison.Ordinal);
    }
}
