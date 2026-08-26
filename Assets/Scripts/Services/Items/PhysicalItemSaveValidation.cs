using System;
using System.Collections.Generic;
using System.Linq;

internal static class PhysicalItemSaveValidation
{
    internal const int MaxSavedStacks = 262_144;
    internal const int MaxSavedUniqueItems = 65_536;
    internal const int MaxPendingBatchDispositions = 16_384;
    internal const int MaxPendingExactOutputRoutes = 16_384;
    internal const int MaxPendingProductionCustodyDrains = 4_096;
    internal const int MaxPendingProductionInputDestinationDrains = 4_096;
    internal const int MaxPendingCapacityRoutingDrains = 16_384;
    private const int MaxComponentsPerItem = 64;
    private const int MaxValuesPerComponent = 256;

    internal static void Validate(
        DungeonPhysicalItemSaveData snapshot,
        DungeonGameRestoreReport report,
        IDungeonItemCatalogProvider catalog)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }
        if (snapshot == null)
        {
            report.AddError("Physical item payload is null.");
            return;
        }
        if (snapshot.version != DungeonPhysicalItemSaveData.CurrentVersion)
        {
            report.AddError(
                $"Unsupported physical item payload version {snapshot.version}; expected {DungeonPhysicalItemSaveData.CurrentVersion}.");
        }
        if (snapshot.nextHaulOperationSequence <= 0)
        {
            report.AddError("Physical item haul-operation sequence must be positive.");
        }
        if (snapshot.lastConfirmedExactRouteCheckpointSequence < 0L
            || (snapshot.lastConfirmedExactRouteCheckpointSequence == 0L
                && !string.IsNullOrEmpty(
                    snapshot.lastConfirmedExactRouteCheckpointDigest))
            || (snapshot.lastConfirmedExactRouteCheckpointSequence > 0L
                && !IsLowerSha256(
                    snapshot.lastConfirmedExactRouteCheckpointDigest)))
        {
            report.AddError(
                "Physical item exact-route checkpoint sequence/digest is invalid.");
        }

        ValidateHaulingSettings(snapshot.haulingSettings, report);
        if (snapshot.stacks == null)
        {
            report.AddError("Physical item payload has no stack list.");
            return;
        }
        if (snapshot.uniqueItems == null)
        {
            report.AddError("Physical item payload has no unique-item list.");
            return;
        }
        if (snapshot.reservationIntents == null)
        {
            report.AddError("Physical item payload has no reservation-intent list.");
            return;
        }
        if (snapshot.pendingBatchDispositions == null)
        {
            report.AddError("Physical item payload has no pending batch-disposition list.");
            return;
        }
        if (snapshot.pendingExactOutputRoutes == null)
        {
            report.AddError("Physical item payload has no pending exact-output-route list.");
            return;
        }
        if (snapshot.pendingProductionCustodyDrains == null)
        {
            report.AddError(
                "Physical item payload has no pending production custody-drain list.");
            return;
        }
        if (snapshot.pendingProductionInputDestinationDrains == null)
        {
            report.AddError(
                "Physical item payload has no pending production input-destination drain list.");
            return;
        }
        if (snapshot.pendingCapacityRoutingDrains == null)
        {
            report.AddError(
                "Physical item payload has no pending capacity-routing drain list.");
            return;
        }
        if (snapshot.stacks.Count > MaxSavedStacks)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxSavedStacks}-stack limit.");
        }
        if (snapshot.uniqueItems.Count > MaxSavedUniqueItems)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxSavedUniqueItems}-unique-item limit.");
        }
        if (snapshot.pendingBatchDispositions.Count > MaxPendingBatchDispositions)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxPendingBatchDispositions}-pending-disposition limit.");
        }
        if (snapshot.pendingExactOutputRoutes.Count > MaxPendingExactOutputRoutes)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxPendingExactOutputRoutes}-pending-route limit.");
        }
        if (snapshot.pendingProductionCustodyDrains.Count
            > MaxPendingProductionCustodyDrains)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxPendingProductionCustodyDrains}-pending-production-custody-drain limit.");
        }
        if (snapshot.pendingProductionInputDestinationDrains.Count
            > MaxPendingProductionInputDestinationDrains)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxPendingProductionInputDestinationDrains}-pending-production-input-destination-drain limit.");
        }
        if (snapshot.pendingCapacityRoutingDrains.Count
            > MaxPendingCapacityRoutingDrains)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxPendingCapacityRoutingDrains}-pending-capacity-routing-drain limit.");
        }

        Dictionary<string, UniqueItemInstanceSaveData> uniqueById =
            ValidateUniqueItems(snapshot.uniqueItems, report);
        ValidateStacks(snapshot.stacks, uniqueById, report, catalog);
        ValidateReservationIntents(snapshot, report);
        ValidatePendingBatchDispositions(snapshot.pendingBatchDispositions, report);
        ValidatePendingExactOutputRoutes(snapshot.pendingExactOutputRoutes, report);
        ValidatePendingProductionCustodyDrains(
            snapshot.pendingProductionCustodyDrains,
            report);
        ValidatePendingProductionInputDestinationDrains(
            snapshot.pendingProductionInputDestinationDrains,
            report);
        ValidatePendingCapacityRoutingDrains(snapshot, report);
        ValidateEquipmentModuleAppraisalJoins(
            snapshot.uniqueItems,
            snapshot.pendingBatchDispositions,
            report);
    }

    private static void ValidatePendingProductionInputDestinationDrains(
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

    private static void ValidatePendingProductionCustodyDrains(
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

    private static void ValidatePendingCapacityRoutingDrains(
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
                    ownedCarryStacks)
                || !ValidateStableCapacityRoutingExternalState(
                    value,
                    snapshot))
            {
                report.AddError(
                    "Invalid pending production capacity-routing drain '"
                    + operation + "'.");
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
        DungeonPhysicalItemSaveData snapshot)
    {
        if (drain == null
            || drain.phase < ProductionCapacityRoutingDrainPhase
                .AwaitingStablePhysicalState)
        {
            return true;
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
                return false;
            }
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

    private static bool AreCanonicalSortedUnique(
        IReadOnlyList<string> values)
    {
        if (values == null)
            return false;
        string previous = string.Empty;
        for (int index = 0; index < values.Count; index++)
        {
            string current = values[index];
            if (!IsCanonicalNonEmpty(current)
                || index > 0
                    && string.CompareOrdinal(previous, current) >= 0)
            {
                return false;
            }
            previous = current;
        }
        return true;
    }

    private static bool IsSubset(
        IReadOnlyList<string> subset,
        IReadOnlyList<string> superset)
    {
        HashSet<string> allowed = new(
            superset ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        return (subset ?? Array.Empty<string>()).All(allowed.Contains);
    }

    private static bool IsPrefix(
        IReadOnlyList<string> prefix,
        IReadOnlyList<string> full)
    {
        if (prefix == null || full == null || prefix.Count > full.Count)
            return false;
        for (int index = 0; index < prefix.Count; index++)
        {
            if (!string.Equals(
                    prefix[index],
                    full[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static void ValidatePendingExactOutputRoutes(
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

    private static void ValidateEquipmentModuleAppraisalJoins(
        IReadOnlyList<UniqueItemInstanceSaveData> uniqueItems,
        IReadOnlyList<PhysicalItemBatchDispositionSaveData> dispositions,
        DungeonGameRestoreReport report)
    {
        const string operationPrefix = "equipment-module-appraisal:";
        Dictionary<string, PhysicalItemBatchDispositionSaveData> byOperation =
            dispositions
                .Where(value => value != null
                    && !string.IsNullOrEmpty(value.operationId))
                .GroupBy(value => value.operationId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
        HashSet<string> ownedOperations = new(StringComparer.Ordinal);

        foreach (UniqueItemInstanceSaveData unique in
                 uniqueItems ?? Array.Empty<UniqueItemInstanceSaveData>())
        {
            if (unique == null
                || !PhysicalItemIds.IsEquipmentModule(unique.definitionId))
            {
                continue;
            }
            ItemInstanceComponentSaveData component = unique.components?
                .FirstOrDefault(value => value != null
                    && string.Equals(
                        value.componentTypeId,
                        ItemInstanceComponentIds.EquipmentModule,
                        StringComparison.Ordinal));
            if (!EquipmentModuleItemStateCodec.TryDecode(
                    component,
                    out EquipmentModuleInstance module,
                    out _))
            {
                continue;
            }

            EquipmentModuleAppraisalCommitSaveData pending =
                module.pendingAppraisal;
            EquipmentModuleAppraisalCommitPhase phase =
                (EquipmentModuleAppraisalCommitPhase)pending.phase;
            if (phase == EquipmentModuleAppraisalCommitPhase.None)
            {
                continue;
            }
            if (!ownedOperations.Add(pending.operationId))
            {
                report.AddError(
                    $"Equipment-module appraisal operation '{pending.operationId}' has multiple owners.");
                continue;
            }

            bool hasReceipt = byOperation.TryGetValue(
                pending.operationId,
                out PhysicalItemBatchDispositionSaveData disposition);
            if (!hasReceipt)
            {
                // Intent may have been captured before the item transaction. An
                // outcome may have been captured after terminal acknowledgement
                // but before the module component was cleared.
                continue;
            }

            bool common = disposition.kind
                    == (int)PhysicalItemDispositionKind.Sink
                && string.Equals(
                    disposition.reasonCode,
                    pending.reasonCode,
                    StringComparison.Ordinal)
                && disposition.quantity == pending.quantity
                && disposition.sourceStackIds != null
                && disposition.sourceStackIds.Count == 1
                && string.Equals(
                    disposition.sourceStackIds[0],
                    pending.couponStackId,
                    StringComparison.Ordinal);
            if (phase == EquipmentModuleAppraisalCommitPhase.OutcomePublished)
            {
                common = common
                    && disposition.inputMassGrams == pending.inputMassGrams
                    && string.Equals(
                        disposition.commitId,
                        pending.commitId,
                        StringComparison.Ordinal)
                    && pending.sourceStackIds.SequenceEqual(
                        disposition.sourceStackIds,
                        StringComparer.Ordinal);
            }
            if (!common)
            {
                report.AddError(
                    $"Equipment-module appraisal '{pending.operationId}' does not match its physical Sink receipt.");
            }
        }

        foreach (PhysicalItemBatchDispositionSaveData disposition in dispositions
                     .Where(value => value?.operationId?.StartsWith(
                         operationPrefix,
                         StringComparison.Ordinal) == true))
        {
            if (!ownedOperations.Contains(disposition.operationId))
            {
                report.AddError(
                    $"Physical appraisal Sink '{disposition.operationId}' has no equipment-module owner.");
            }
        }
    }

    private static void ValidatePendingBatchDispositions(
        IReadOnlyList<PhysicalItemBatchDispositionSaveData> values,
        DungeonGameRestoreReport report)
    {
        HashSet<string> operations = new(StringComparer.Ordinal);
        HashSet<string> commits = new(StringComparer.Ordinal);
        string previousOperation = string.Empty;
        foreach (PhysicalItemBatchDispositionSaveData value in values)
        {
            string operation = value?.operationId ?? string.Empty;
            if (value == null
                || (value.kind != (int)PhysicalItemDispositionKind.Transfer
                    && value.kind != (int)PhysicalItemDispositionKind.Sink)
                || !IsCanonicalNonEmpty(operation)
                || !IsCanonicalNonEmpty(value.reasonCode)
                || !IsCanonicalNonEmpty(value.requestFingerprint)
                || !IsCanonicalNonEmpty(value.commitId)
                || value.sourceStackIds == null
                || value.sourceStackIds.Count == 0
                || value.sourceStackIds.Any(id => !IsCanonicalNonEmpty(id))
                || value.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                    != value.sourceStackIds.Count
                || value.quantity <= 0
                || value.inputMassGrams <= 0L
                || !operations.Add(operation)
                || !commits.Add(value.commitId))
            {
                report.AddError(
                    $"Invalid pending physical batch disposition '{operation}'.");
                continue;
            }
            for (int index = 1; index < value.sourceStackIds.Count; index++)
            {
                if (string.CompareOrdinal(
                        value.sourceStackIds[index - 1],
                        value.sourceStackIds[index]) >= 0)
                {
                    report.AddError(
                        $"Pending physical batch disposition '{operation}' source IDs are not canonical.");
                    break;
                }
            }
            if (previousOperation.Length > 0
                && string.CompareOrdinal(previousOperation, operation) >= 0)
            {
                report.AddError(
                    "Pending physical batch dispositions are not in canonical operation order.");
            }
            previousOperation = operation;
            PhysicalItemBatchDispositionReceipt reconstructed = new(
                (PhysicalItemDispositionKind)value.kind,
                value.operationId,
                value.reasonCode,
                value.requestFingerprint,
                value.sourceStackIds,
                value.quantity,
                value.inputMassGrams);
            if (!reconstructed.IsCommitted
                || !string.Equals(
                    reconstructed.CommitId,
                    value.commitId,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Pending physical batch disposition '{operation}' has a mismatched receipt identity.");
            }
        }
    }

    private static void ValidateReservationIntents(
        DungeonPhysicalItemSaveData snapshot,
        DungeonGameRestoreReport report)
    {
        Dictionary<string, WorldItemStackSaveData> stacks = snapshot.stacks
            .Where(stack => stack != null && !string.IsNullOrWhiteSpace(stack.stackId))
            .ToDictionary(stack => stack.stackId, StringComparer.Ordinal);
        Dictionary<string, int> totalsByStack = new(StringComparer.Ordinal);
        HashSet<string> owners = new(StringComparer.Ordinal);
        HashSet<string> claimIds = new(StringComparer.Ordinal);
        string previousOwner = string.Empty;
        foreach (ItemReservationIntentSaveData intent in snapshot.reservationIntents)
        {
            string owner = intent?.ownerOperationId ?? string.Empty;
            if (intent == null
                || !intent.hadActiveItemReservation
                || !IsCanonicalNonEmpty(owner)
                || !owners.Add(owner)
                || intent.reservationHints == null
                || intent.reservationHints.Count == 0)
            {
                report.AddError($"Invalid reservation intent '{owner}'.");
                continue;
            }
            if (previousOwner.Length > 0
                && string.CompareOrdinal(previousOwner, owner) >= 0)
            {
                report.AddError("Reservation intents are not in canonical owner order.");
            }
            previousOwner = owner;
            int expectedOrdinal = 0;
            foreach (ItemReservationClaimHintSaveData hint in intent.reservationHints)
            {
                string stackId = hint?.preferredPhysicalStackId ?? string.Empty;
                int ordinal = expectedOrdinal++;
                string invalidReason = GetInvalidClaimReason(
                    hint,
                    ordinal,
                    stackId,
                    stacks,
                    claimIds,
                    out WorldItemStackSaveData stack);
                if (invalidReason.Length > 0)
                {
                    report.AddError(
                        $"Invalid reservation claim '{hint?.claimHintId}' for owner '{owner}': {invalidReason}.");
                    continue;
                }
                totalsByStack[stackId] = totalsByStack.TryGetValue(
                    stackId,
                    out int total)
                    ? checked(total + hint.quantity)
                    : hint.quantity;
            }
        }
        foreach (KeyValuePair<string, int> total in totalsByStack)
        {
            if (stacks[total.Key].quantity < total.Value)
            {
                report.AddError(
                    $"Reservation hints exceed physical stack '{total.Key}': {total.Value}/{stacks[total.Key].quantity}.");
            }
        }
    }

    private static string GetInvalidClaimReason(
        ItemReservationClaimHintSaveData hint,
        int expectedOrdinal,
        string stackId,
        IReadOnlyDictionary<string, WorldItemStackSaveData> stacks,
        ISet<string> claimIds,
        out WorldItemStackSaveData stack)
    {
        stack = null;
        if (hint == null) return "hint-null";
        if (hint.claimOrdinal != expectedOrdinal)
            return $"ordinal={hint.claimOrdinal}, expected={expectedOrdinal}";
        if (hint.quantity <= 0) return $"quantity={hint.quantity}";
        if (!IsCanonicalNonEmpty(hint.claimHintId)) return "claim-id-noncanonical";
        // originStackId is provenance, not a current physical ownership target.
        // Partial extraction may retire it while preferredPhysicalStackId stays live.
        if (!IsCanonicalNonEmpty(hint.originStackId)) return "origin-stack-id-noncanonical";
        if (!claimIds.Add(hint.claimHintId)) return "duplicate-claim-id";
        if (!stacks.TryGetValue(stackId, out stack))
            return $"preferred-stack-missing:{stackId}";
        if (!string.Equals(stack.itemId, hint.itemId, StringComparison.Ordinal))
            return $"item-mismatch:{hint.itemId}->{stack.itemId}";
        // Reservation ownership deliberately ignores freshness. Freshness is a
        // stacking concern and keeps aging while an owner walks, waits, or eats;
        // treating that normal mutation as an ownership change makes every
        // in-flight food lease eventually unsaveable. Material state such as
        // quality, contamination, durability, and provenance remains part of
        // this signature and still fails closed when it changes.
        string actualSignature = ItemReservationSignature.Create(
            stack.itemId,
            stack.components);
        if (!string.Equals(actualSignature, hint.expectedStackSignature, StringComparison.Ordinal))
            return $"signature-mismatch:{hint.expectedStackSignature}->{actualSignature}";
        if (!Enum.IsDefined(typeof(ItemReservationPurpose), hint.purpose))
            return $"purpose-invalid:{(int)hint.purpose}";
        return string.Empty;
    }

    private static void ValidateHaulingSettings(
        ItemHaulingSettingsSnapshot settings,
        DungeonGameRestoreReport report)
    {
        if (settings == null)
        {
            report.AddError("Physical item payload has no hauling settings.");
            return;
        }

        float value = settings.maxCarryMultiplier;
        float steps = value / 0.05f;
        if (float.IsNaN(value)
            || float.IsInfinity(value)
            || value < 1f
            || value > 2.5f
            || Math.Abs(steps - Math.Round(steps)) > 0.0001d)
        {
            report.AddError(
                $"Physical item hauling multiplier {value} is not canonical.");
        }
    }

    private static Dictionary<string, UniqueItemInstanceSaveData> ValidateUniqueItems(
        IReadOnlyList<UniqueItemInstanceSaveData> savedItems,
        DungeonGameRestoreReport report)
    {
        Dictionary<string, UniqueItemInstanceSaveData> result =
            new(StringComparer.Ordinal);
        HashSet<string> moduleIds = new(StringComparer.Ordinal);
        string previousId = string.Empty;
        for (int index = 0; index < savedItems.Count; index++)
        {
            UniqueItemInstanceSaveData unique = savedItems[index];
            string instanceId = unique?.itemInstanceId ?? string.Empty;
            ItemInstanceId typedId = (ItemInstanceId)instanceId;
            if (unique == null
                || !typedId.IsValid
                || !string.Equals(instanceId, typedId.Value, StringComparison.Ordinal)
                || !result.TryAdd(instanceId, unique))
            {
                report.AddError(
                    $"Physical unique item {index} has invalid or duplicate ID '{instanceId}'.");
                continue;
            }
            if (previousId.Length > 0
                && string.CompareOrdinal(previousId, instanceId) >= 0)
            {
                report.AddError(
                    "Physical unique items must use canonical ascending instance-ID order.");
            }
            previousId = instanceId;

            string definitionId = unique.definitionId ?? string.Empty;
            if (!IsCanonicalNonEmpty(definitionId))
            {
                report.AddError(
                    $"Physical unique item '{instanceId}' has a non-canonical definition ID.");
            }
            ValidateComponents(unique.components, $"unique item '{instanceId}'", report);

            ItemInstanceComponentSaveData equipmentComponent = null;
            ItemInstanceComponentSaveData moduleComponent = null;
            if (unique.components != null)
            {
                foreach (ItemInstanceComponentSaveData component in unique.components)
                {
                    if (component != null
                        && string.Equals(
                            component.componentTypeId,
                            ItemInstanceComponentIds.Equipment,
                            StringComparison.Ordinal))
                    {
                        if (equipmentComponent != null)
                        {
                            report.AddError(
                                $"Physical unique item '{instanceId}' has duplicate equipment state.");
                        }
                        equipmentComponent = component;
                    }
                    if (component != null
                        && string.Equals(
                            component.componentTypeId,
                            ItemInstanceComponentIds.EquipmentModule,
                            StringComparison.Ordinal))
                    {
                        if (moduleComponent != null)
                        {
                            report.AddError(
                                $"Physical unique item '{instanceId}' has duplicate equipment-module state.");
                        }
                        moduleComponent = component;
                    }
                }
            }

            if (PhysicalItemIds.IsEquipmentModule(definitionId))
            {
                string moduleDecodeError = "missing equipment-module state";
                if (equipmentComponent != null
                    || moduleComponent == null
                    || !EquipmentModuleItemStateCodec.TryDecode(
                        moduleComponent,
                        out EquipmentModuleInstance module,
                        out moduleDecodeError))
                {
                    report.AddError(
                        $"Physical unique item '{instanceId}' has invalid equipment-module state: {moduleDecodeError}.");
                    continue;
                }
                if (!string.Equals(
                        module.instanceId,
                        instanceId,
                        StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(module.sourceStackId)
                    || !string.IsNullOrWhiteSpace(
                        module.attachedEquipmentInstanceId)
                    || !moduleIds.Add(module.instanceId))
                {
                    report.AddError(
                        $"Physical unique item '{instanceId}' does not match its independent equipment-module identity.");
                }
                continue;
            }

            if (moduleComponent != null)
            {
                report.AddError(
                    $"Physical unique item '{instanceId}' mixes equipment and independent module state.");
            }

            EquipmentPhysicalStatePayload payload = null;
            string decodeError = "missing equipment state";
            if (equipmentComponent == null
                || !EquipmentItemStateCodec.TryDecodeFull(
                    equipmentComponent,
                    out payload,
                    out decodeError))
            {
                report.AddError(
                    $"Physical unique item '{instanceId}' has invalid equipment state: {decodeError ?? "missing equipment state"}.");
                continue;
            }

            string expectedDefinition =
                PhysicalItemIds.ForEquipment(payload.equipment.definitionId);
            if (!string.Equals(
                    payload.equipment.instanceId,
                    instanceId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    definitionId,
                    expectedDefinition,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Physical unique item '{instanceId}' does not match its equipment identity or definition.");
            }

            foreach (EquipmentModuleInstance module in
                     payload.attachedModules ?? new List<EquipmentModuleInstance>())
            {
                string moduleId = module?.instanceId ?? string.Empty;
                if (module == null
                    || !IsCanonicalNonEmpty(moduleId)
                    || !string.IsNullOrWhiteSpace(module.sourceStackId)
                    || !string.Equals(
                        module.attachedEquipmentInstanceId,
                        payload.equipment.instanceId,
                        StringComparison.Ordinal)
                    || !moduleIds.Add(moduleId))
                {
                    report.AddError(
                        $"Physical unique item '{instanceId}' has an invalid or duplicate module '{moduleId}'.");
                }
            }
        }

        return result;
    }

    private static void ValidateStacks(
        IReadOnlyList<WorldItemStackSaveData> stacks,
        IReadOnlyDictionary<string, UniqueItemInstanceSaveData> uniqueById,
        DungeonGameRestoreReport report,
        IDungeonItemCatalogProvider catalog)
    {
        HashSet<string> stackIds = new(StringComparer.Ordinal);
        HashSet<string> stackedInstanceIds = new(StringComparer.Ordinal);
        WorldItemStackSaveData previous = null;
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSaveData stack = stacks[index];
            string stackId = stack?.stackId ?? string.Empty;
            ItemStackId typedStackId = (ItemStackId)stackId;
            if (stack == null
                || !typedStackId.IsValid
                || !string.Equals(stackId, typedStackId.Value, StringComparison.Ordinal)
                || !stackIds.Add(stackId))
            {
                report.AddError(
                    $"Physical stack {index} has invalid or duplicate ID '{stackId}'.");
                continue;
            }
            if (previous != null && CompareStackOrder(previous, stack) >= 0)
            {
                report.AddError(
                    "Physical stacks must use canonical y/x/item/stack-ID order.");
            }
            previous = stack;

            string itemId = stack.itemId ?? string.Empty;
            if (!IsCanonicalNonEmpty(itemId)
                || !catalog.TryGetDefinition(itemId, out DungeonItemDefinition definition))
            {
                report.AddError(
                    $"Physical stack '{stackId}' references unknown or non-canonical item '{itemId}'.");
                continue;
            }
            if (stack.quantity <= 0 || stack.quantity > definition.MaxStack)
            {
                report.AddError(
                    $"Physical stack '{stackId}' has invalid quantity {stack.quantity}/{definition.MaxStack}.");
            }
            if (!Enum.IsDefined(typeof(WorldItemStackState), stack.state))
            {
                report.AddError(
                    $"Physical stack '{stackId}' has invalid state {stack.state}.");
            }
            if (stack.reservedByPersistentId == null
                || stack.reservedByPersistentId.Length != 0)
            {
                report.AddError(
                    $"Physical stack '{stackId}' contains transient reservation state.");
            }
            if (!IsCanonicalText(stack.destinationId)
                || !IsCanonicalText(stack.aggregationCohortId)
                || !IsCanonicalText(stack.sourceStorageDestinationId)
                || !IsCanonicalText(stack.sourceCharacterId)
                || !IsCanonicalText(stack.sourceDisplayName)
                || !IsCanonicalText(stack.sourceSpeciesTag)
                || !IsCanonicalText(stack.sourceDeathReason)
                || !IsCanonicalText(stack.recoveryOwnerOperationId)
                || !IsCanonicalText(stack.recoverySourceStackId)
                || !IsCanonicalText(stack.recoveryCarrierPersistentId))
            {
                report.AddError(
                    $"Physical stack '{stackId}' contains non-canonical text fields.");
            }
            if (!stack.hasDestinationPosition
                && (stack.destinationGridX != 0 || stack.destinationGridY != 0))
            {
                report.AddError(
                    $"Physical stack '{stackId}' has stale destination coordinates.");
            }
            if (!string.IsNullOrEmpty(stack.destinationId)
                && stack.destinationId.StartsWith(
                    WorldItemStackRuntime.CombatLoadoutDestinationPrefix,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Physical stack '{stackId}' contains transient combat-loadout routing.");
            }
            if (!Enum.IsDefined(typeof(WasteOriginKind), stack.wasteOrigin)
                || float.IsNaN(stack.contamination)
                || float.IsInfinity(stack.contamination)
                || stack.contamination < 0f
                || stack.contamination > 100f)
            {
                report.AddError(
                    $"Physical stack '{stackId}' has invalid waste or contamination state.");
            }
            ValidateRecoveryDrop(stack, stackId, report);
            ValidateComponents(stack.components, $"stack '{stackId}'", report);

            string instanceId = stack.itemInstanceId ?? string.Empty;
            ItemInstanceId typedInstanceId = (ItemInstanceId)instanceId;
            if (definition.MaxStack == 1 && !typedInstanceId.IsValid)
            {
                report.AddError(
                    $"Unique physical stack '{stackId}' has no item-instance ID.");
            }
            if (instanceId.Length == 0)
            {
                continue;
            }
            bool equipmentBacked = PhysicalItemIds.TryGetEquipmentDefinitionId(
                    itemId,
                    out _)
                || PhysicalItemIds.IsEquipmentModule(itemId);
            UniqueItemInstanceSaveData unique = null;
            if (!typedInstanceId.IsValid
                || !string.Equals(instanceId, typedInstanceId.Value, StringComparison.Ordinal)
                || !stackedInstanceIds.Add(instanceId))
            {
                report.AddError(
                    $"Physical stack '{stackId}' has an invalid or duplicate item-instance ID '{instanceId}'.");
                continue;
            }
            if (equipmentBacked
                && (!uniqueById.TryGetValue(instanceId, out unique)
                    || !string.Equals(unique.definitionId, itemId, StringComparison.Ordinal)))
            {
                report.AddError(
                    $"Physical equipment stack '{stackId}' has no matching authoritative item instance '{instanceId}'.");
            }
            else if (!equipmentBacked && uniqueById.ContainsKey(instanceId))
            {
                report.AddError(
                    $"Inline-authority unique stack '{stackId}' must not duplicate item instance '{instanceId}' in the equipment registry.");
            }
            if (PhysicalItemIds.IsEquipmentModule(itemId)
                && unique != null)
            {
                ItemInstanceComponentSaveData moduleComponent =
                    unique.components?.FirstOrDefault(component =>
                        component != null
                        && string.Equals(
                            component.componentTypeId,
                            ItemInstanceComponentIds.EquipmentModule,
                            StringComparison.Ordinal));
                if (!EquipmentModuleItemStateCodec.TryDecode(
                        moduleComponent,
                        out EquipmentModuleInstance module,
                        out _)
                    || !string.Equals(
                        module.sourceStackId,
                        stackId,
                        StringComparison.Ordinal))
                {
                    report.AddError(
                        $"Physical equipment-module stack '{stackId}' does not match its module source stack.");
                }
            }
        }

        foreach (UniqueItemInstanceSaveData unique in uniqueById.Values)
        {
            if (unique != null
                && PhysicalItemIds.IsEquipmentModule(unique.definitionId)
                && !stackedInstanceIds.Contains(unique.itemInstanceId))
            {
                report.AddError(
                    $"Physical equipment module '{unique.itemInstanceId}' has no authoritative stack.");
            }
        }
    }

    internal static void ValidateRecoveryDrop(
        WorldItemStackSaveData stack,
        string stackId,
        DungeonGameRestoreReport report)
    {
        bool hasMetadata = stack.dropDisposition != WorldItemDropDisposition.None
            || !string.IsNullOrEmpty(stack.recoveryOwnerOperationId)
            || !string.IsNullOrEmpty(stack.recoverySourceStackId)
            || !string.IsNullOrEmpty(stack.recoveryCarrierPersistentId)
            || stack.recoveryInterruptionKind != WorldItemCarryInterruptionKind.None
            || stack.droppedAtGameTime != 0d
            || stack.recoveryDeadlineGameTime != 0d;
        if (!hasMetadata)
            return;

        bool finiteTimes = !double.IsNaN(stack.droppedAtGameTime)
            && !double.IsInfinity(stack.droppedAtGameTime)
            && !double.IsNaN(stack.recoveryDeadlineGameTime)
            && !double.IsInfinity(stack.recoveryDeadlineGameTime);
        bool routedRecovery = FacilityOutputExactRouteCustodyCodec.TryRead(
                stack.components,
                out FacilityOutputExactRouteCustodyMetadata custody)
            && custody.Phase == FacilityOutputExactRouteCustodyPhase.Routable;
        bool destinationShapeValid = routedRecovery
            ? IsCanonicalNonEmpty(stack.destinationId)
                && stack.hasDestinationPosition
            : string.IsNullOrEmpty(stack.destinationId)
                && !stack.hasDestinationPosition;
        if (stack.dropDisposition !=
                WorldItemDropDisposition.TransientCarryRecoveryDrop
            || stack.state != WorldItemStackState.Loose
            || !destinationShapeValid
            || !IsCanonicalNonEmpty(stack.recoveryOwnerOperationId)
            || !IsCanonicalNonEmpty(stack.recoverySourceStackId)
            || !IsCanonicalNonEmpty(stack.recoveryCarrierPersistentId)
            || !HaulDeliveryOperationIdentity.TryParse(
                stack.recoveryOwnerOperationId,
                stack.recoveryCarrierPersistentId,
                out _)
            || stack.recoveryInterruptionKind is not (
                WorldItemCarryInterruptionKind.Downed
                or WorldItemCarryInterruptionKind.Dead)
            || !finiteTimes
            || stack.droppedAtGameTime < 0d
            || stack.recoveryDeadlineGameTime <= stack.droppedAtGameTime)
        {
            report.AddError(
                $"Physical stack '{stackId}' has invalid transient carry-recovery metadata.");
        }
    }

    private static void ValidateComponents(
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        string owner,
        DungeonGameRestoreReport report)
    {
        if (components == null)
        {
            report.AddError($"Physical {owner} has no component list.");
            return;
        }
        if (components.Count > MaxComponentsPerItem)
        {
            report.AddError(
                $"Physical {owner} exceeds the {MaxComponentsPerItem}-component limit.");
        }

        HashSet<string> componentIds = new(StringComparer.Ordinal);
        foreach (ItemInstanceComponentSaveData component in components)
        {
            string componentId = component?.componentTypeId ?? string.Empty;
            if (component == null
                || !IsCanonicalNonEmpty(componentId)
                || component.schemaVersion < 1
                || !componentIds.Add(componentId))
            {
                report.AddError(
                    $"Physical {owner} has an invalid or duplicate component '{componentId}'.");
                continue;
            }
            if (component.values == null)
            {
                report.AddError(
                    $"Physical {owner} component '{componentId}' has no value list.");
                continue;
            }
            if (component.values.Count > MaxValuesPerComponent)
            {
                report.AddError(
                    $"Physical {owner} component '{componentId}' exceeds the {MaxValuesPerComponent}-value limit.");
            }

            HashSet<string> keys = new(StringComparer.Ordinal);
            foreach (ItemStateValueSaveData value in component.values)
            {
                string key = value?.key ?? string.Empty;
                if (value == null
                    || !IsCanonicalNonEmpty(key)
                    || !Enum.IsDefined(typeof(ItemStateValueKind), value.kind)
                    || !keys.Add(key)
                    || (value.kind == ItemStateValueKind.Decimal
                        && (double.IsNaN(value.decimalValue)
                            || double.IsInfinity(value.decimalValue))))
                {
                    report.AddError(
                        $"Physical {owner} component '{componentId}' has invalid or duplicate value '{key}'.");
                }
            }
        }
    }

    private static int CompareStackOrder(
        WorldItemStackSaveData left,
        WorldItemStackSaveData right)
    {
        int comparison = left.gridY.CompareTo(right.gridY);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = left.gridX.CompareTo(right.gridX);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = string.CompareOrdinal(left.itemId, right.itemId);
        return comparison != 0
            ? comparison
            : string.CompareOrdinal(left.stackId, right.stackId);
    }

    private static bool IsCanonicalNonEmpty(string value)
    {
        return !string.IsNullOrEmpty(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool IsCanonicalText(string value)
    {
        return value != null
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool IsLowerSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if ((current < '0' || current > '9')
                && (current < 'a' || current > 'f'))
            {
                return false;
            }
        }
        return true;
    }
}
