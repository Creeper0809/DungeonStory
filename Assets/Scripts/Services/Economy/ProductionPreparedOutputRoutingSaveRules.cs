using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static class ProductionPreparedOutputRoutingSaveRules
{
    private const string RequestV1 = "facility-output-exact-route-request-v1";
    private const string ReceiptV1 = "facility-output-exact-route-receipt-v1";
    private const string RoutingV9 = "prepared-output-routing-v9";
    private const string DeliveryRevisionV1 =
        "prepared-output-delivery-revision-v1";
    private const string DeliveryRerouteOperationV1 =
        "prepared-output-delivery-reroute-operation-v1";

    internal static void ValidateSave(ProductionPreparedOutputRoutingSaveData source)
    {
        if (source == null
            || source.version != ProductionPreparedOutputRoutingSaveData.CurrentVersion
            || source.lastConfirmedCheckpointSequence < 0L
            || source.lastConfirmedCheckpointSequence == 0L
                && !string.Equals(source.lastConfirmedCheckpointDigest,
                    string.Empty,
                    StringComparison.Ordinal)
            || source.lastConfirmedCheckpointSequence > 0L
                && !IsDigest(source.lastConfirmedCheckpointDigest)
            || source.batches == null)
            throw new InvalidOperationException("Routing payload schema is invalid.");
        HashSet<string> batchIds = new(StringComparer.Ordinal);
        HashSet<string> operationIds = new(StringComparer.Ordinal);
        string previous = string.Empty;
        foreach (var batch in source.batches)
        {
            ValidateBatch(batch, operationIds);
            if (!batchIds.Add(batch.batchCommitId)
                || previous.Length > 0
                    && string.CompareOrdinal(previous, batch.batchCommitId) >= 0)
                throw new InvalidOperationException("Routing batches are unordered.");
            previous = batch.batchCommitId;
        }
    }

    internal static void ValidateBatch(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ISet<string> operationIds)
    {
        if (batch == null || batch.lines == null || batch.lines.Count == 0
            || batch.nonPhysicalDispositions == null
            || batch.totalDeclaredLossMassGrams < 0L
            || batch.totalDeclaredExternalInputMassGrams < 0L)
            throw new InvalidOperationException("Routing batch is empty.");
        RequireCanonical(batch.batchCommitId, "batch.id");
        RequireCanonical(batch.ownerBillId, "batch.bill");
        RequireCanonical(batch.ownerRecipeId, "batch.recipe");
        RequireCanonical(batch.ownerFacilityId, "batch.facility");
        RequireCanonical(batch.destinationId, "batch.destination");
        RequireDigest(batch.outcomeFingerprint, "batch.outcome");
        RequireDigest(batch.routingFingerprint, "batch.fingerprint");
        ProductionBillId billId = (ProductionBillId)batch.ownerBillId;
        if (!billId.IsValid || !((BuildingInstanceId)batch.ownerFacilityId).IsValid
            || batch.cycleSequence <= 0
            || !string.Equals(batch.batchCommitId,
                ProductionPreparedOutputIdentity.BuildBatchCommitId(billId,
                    batch.cycleSequence, batch.outcomeFingerprint),
                StringComparison.Ordinal)
            || !string.Equals(batch.destinationId,
                ProductionBillIdentity.OutputDestinationPrefix + batch.ownerFacilityId,
                StringComparison.Ordinal)
            || !string.Equals(batch.routingFingerprint, RoutingFingerprint(batch),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Routing batch identity conflicts.");

        string previous = string.Empty;
        foreach (var line in batch.lines)
        {
            ValidateLine(batch, line, operationIds);
            if (previous.Length > 0
                && string.CompareOrdinal(previous, line.outputLineId) >= 0)
                throw new InvalidOperationException("Routing lines are unordered.");
            previous = line.outputLineId;
        }

        long declaredLossMassGrams = 0L;
        long declaredExternalInputMassGrams = 0L;
        previous = string.Empty;
        foreach (ProductionPreparedOutputNonPhysicalDispositionSaveData
                 disposition in batch.nonPhysicalDispositions)
        {
            ValidateNonPhysicalDisposition(batch, disposition);
            if (previous.Length > 0
                && string.CompareOrdinal(
                    previous,
                    disposition.outputLineId) >= 0)
            {
                throw new InvalidOperationException(
                    "Routing non-physical dispositions are unordered.");
            }
            previous = disposition.outputLineId;
            if (disposition.role == ProductionOutputRole.DeclaredLoss)
            {
                declaredLossMassGrams = checked(
                    declaredLossMassGrams + disposition.exactMassGrams);
            }
            else
            {
                declaredExternalInputMassGrams = checked(
                    declaredExternalInputMassGrams
                    + disposition.exactMassGrams);
            }
        }
        if (declaredLossMassGrams != batch.totalDeclaredLossMassGrams
            || declaredExternalInputMassGrams !=
                batch.totalDeclaredExternalInputMassGrams)
        {
            throw new InvalidOperationException(
                "Routing non-physical disposition total conflicts.");
        }

        long originalPhysicalMassGrams = batch.lines.Aggregate(
            0L,
            (total, line) => checked(total + line.originalMassGrams));
        bool hasProof = IsDigest(batch.maximumMassProofDigest)
            && batch.maximumBatchMassGrams > 0L
            && IsDigest(batch.capacityClaimDigest);
        if (!IsDigest(batch.capacitySourceDigest)
            || batch.outputBufferCycleCapacity is < 2 or > 4
            || batch.projectedPortfolioCapacityGrams <= 0L
            || !hasProof
            || originalPhysicalMassGrams > batch.maximumBatchMassGrams
            || batch.requiredMinimumCapacityGrams != Math.Max(
                batch.projectedPortfolioCapacityGrams,
                checked(
                    batch.maximumBatchMassGrams
                    * batch.outputBufferCycleCapacity)))
        {
            throw new InvalidOperationException(
                "Routing batch capacity authority is invalid.");
        }
    }

    internal static void ValidateLine(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line,
        ISet<string> operationIds)
    {
        if (line == null || !IsPhysicalRole(line.role)
            || line.originalQuantity <= 0 || line.remainingQuantity < 0
            || line.routedQuantity < 0
            || line.originalQuantity != line.remainingQuantity + line.routedQuantity
            || line.originalMassGrams <= 0L || line.remainingMassGrams < 0L
            || line.routedMassGrams < 0L
            || line.originalMassGrams != line.remainingMassGrams + line.routedMassGrams
            || line.routeOperations == null)
            throw new InvalidOperationException("Routing line totals are invalid.");
        RequireCanonical(line.outputLineId, "line.output");
        RequireCanonical(line.itemId, "line.item");
        RequireCanonical(line.destinationId, "line.destination");
        RequireDigest(line.componentFingerprint, "line.component");
        RequireCanonical(line.outputCapabilityId, "line.capability");
        RequireCanonical(line.outputComponentCodecId, "line.componentCodec");
        RequireDigest(
            line.outputCapabilityFingerprint,
            "line.capabilityFingerprint");
        if (!string.Equals(line.batchCommitId, batch.batchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(line.destinationId, batch.destinationId,
                StringComparison.Ordinal)
            || !string.Equals(line.lineCommitId,
                ProductionPreparedOutputIdentity.BuildLineCommitId(
                    batch.batchCommitId, line.outputLineId),
                StringComparison.Ordinal)
            || line.outputCapabilityVersion <= 0
            || line.outputComponentCodecVersion <= 0
            || !string.Equals(
                line.outputCapabilityFingerprint,
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    line.outputLineId,
                    line.itemId,
                    line.outputCapabilityId,
                    line.outputCapabilityVersion,
                    line.outputComponentCodecId,
                    line.outputComponentCodecVersion),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Routing line identity conflicts.");

        int coveredQuantity = 0;
        long coveredMass = 0L;
        bool pendingSeen = false;
        foreach (var operation in line.routeOperations
                     .OrderBy(value => value.sourceOffsetQuantity))
        {
            ValidateOperation(batch, line, operation);
            if (!operationIds.Add(operation.routeOperationId)
                || operation.sourceOffsetQuantity != coveredQuantity
                || operation.sourceOffsetMassGrams != coveredMass || pendingSeen)
                throw new InvalidOperationException("Routing operations overlap.");
            if (operation.phase == ProductionPreparedOutputRoutePhase.PhysicalPending)
            {
                pendingSeen = true;
                continue;
            }
            coveredQuantity = checked(coveredQuantity + operation.routedQuantity);
            coveredMass = checked(coveredMass + operation.routedMassGrams);
        }
        if (coveredQuantity != line.routedQuantity
            || coveredMass != line.routedMassGrams)
            throw new InvalidOperationException("Routing operation coverage conflicts.");
    }

    internal static void ValidateNonPhysicalDisposition(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputNonPhysicalDispositionSaveData disposition)
    {
        if (disposition == null
            || !ProductionOutputRoleRules.IsNonPhysical(disposition.role)
            || disposition.exactMassGrams <= 0L
            || disposition.canonicalPayload == null
            || disposition.canonicalPayload.Length == 0
            || !string.Equals(
                disposition.canonicalPayload,
                disposition.canonicalPayload.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Routing non-physical disposition is invalid.");
        }
        RequireCanonical(disposition.outputLineId, "disposition.output");
        RequireDigest(
            disposition.dispositionFingerprint,
            "disposition.fingerprint");
        if (!string.Equals(
                disposition.batchCommitId,
                batch.batchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                disposition.lineCommitId,
                ProductionPreparedOutputIdentity.BuildLineCommitId(
                    batch.batchCommitId,
                    disposition.outputLineId),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Routing non-physical disposition identity conflicts.");
        }
    }

    internal static void ValidateOperation(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line,
        ProductionPreparedOutputRouteOperationSaveData operation)
    {
        if (operation == null
            || !Enum.IsDefined(typeof(ProductionPreparedOutputRoutePhase),
                operation.phase)
            || operation.sourceOffsetQuantity < 0
            || operation.sourceOffsetMassGrams < 0L
            || operation.routedQuantity <= 0 || operation.routedMassGrams <= 0L
            || operation.physicalSlices == null)
            throw new InvalidOperationException("Routing operation fields are invalid.");
        RequireCanonical(operation.routeOperationId, "operation.id");
        RequireCanonicalOptional(
            operation.targetDestinationId,
            "operation.target");
        RequireDigest(operation.requestFingerprint, "operation.request");
        if (!string.Equals(operation.routeOperationId,
                BuildOperationId(batch.batchCommitId, line.lineCommitId,
                    operation.sourceOffsetQuantity), StringComparison.Ordinal)
            || !string.Equals(operation.requestFingerprint,
                RequestFingerprint(batch, line, operation),
                StringComparison.Ordinal))
            throw new InvalidOperationException("Routing operation identity conflicts.");
        bool pending = operation.phase ==
            ProductionPreparedOutputRoutePhase.PhysicalPending;
        ValidateDeliveryRevisions(operation);
        if (pending)
        {
            if (!string.IsNullOrEmpty(operation.physicalReceiptFingerprint)
                || operation.physicalSlices.Count != 0)
                throw new InvalidOperationException("Pending route has a receipt.");
            return;
        }
        RequireDigest(operation.physicalReceiptFingerprint, "operation.receipt");
        ValidateSavedSlices(line, operation);
        if (!string.Equals(
                operation.physicalReceiptFingerprint,
                SavedReceiptFingerprint(batch, line, operation),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Saved physical receipt fingerprint conflicts with its exact slices.");
        }
    }

    internal static void ValidateSavedSlices(
        ProductionPreparedOutputRoutingLineSaveData line,
        ProductionPreparedOutputRouteOperationSaveData operation)
    {
        if (operation.physicalSlices.Count == 0)
            throw new InvalidOperationException("Applied route has no slices.");
        int sourceOffset = operation.sourceOffsetQuantity;
        int quantity = 0;
        long mass = 0L;
        foreach (var slice in operation.physicalSlices
                     .OrderBy(value => value.sourceOffsetQuantity)
                     .ThenBy(value => value.sourceStackId, StringComparer.Ordinal)
                     .ThenBy(value => value.routedStackId, StringComparer.Ordinal)
                     .ThenBy(value => value.routedOffsetQuantity))
        {
            if (slice == null || slice.sourceOffsetQuantity != sourceOffset
                || slice.routedOffsetQuantity < 0 || slice.routedQuantity <= 0
                || slice.routedMassGrams <= 0L
                || checked(line.originalMassGrams * slice.routedQuantity)
                    != checked(slice.routedMassGrams * line.originalQuantity))
                throw new InvalidOperationException("Saved physical slice is invalid.");
            RequireCanonical(slice.sourceStackId, "slice.source");
            RequireCanonical(slice.routedStackId, "slice.routed");
            sourceOffset = checked(sourceOffset + slice.routedQuantity);
            quantity = checked(quantity + slice.routedQuantity);
            mass = checked(mass + slice.routedMassGrams);
        }
        if (quantity != operation.routedQuantity || mass != operation.routedMassGrams)
            throw new InvalidOperationException("Saved slice totals conflict.");
        foreach (var routed in operation.physicalSlices.GroupBy(
                     value => value.routedStackId, StringComparer.Ordinal))
        {
            int expected = 0;
            foreach (var slice in routed.OrderBy(value => value.routedOffsetQuantity)
                         .ThenBy(value => value.sourceStackId,
                             StringComparer.Ordinal))
            {
                if (slice.routedOffsetQuantity != expected)
                    throw new InvalidOperationException("Saved routed ranges conflict.");
                expected = checked(expected + slice.routedQuantity);
            }
        }
    }

    internal static void ValidateDeliveryRevisions(
        ProductionPreparedOutputRouteOperationSaveData operation)
    {
        if (operation.deliveryRevisions == null
            || operation.deliveryRevisions.Count == 0)
        {
            throw new InvalidOperationException(
                "Routing operation has no delivery revision authority.");
        }

        bool pendingPhysical = operation.phase ==
            ProductionPreparedOutputRoutePhase.PhysicalPending;
        ProductionPreparedOutputDeliveryRevisionSaveData previous = null;
        for (int index = 0; index < operation.deliveryRevisions.Count; index++)
        {
            ProductionPreparedOutputDeliveryRevisionSaveData revision =
                operation.deliveryRevisions[index];
            if (revision == null
                || revision.revision != index
                || !Enum.IsDefined(
                    typeof(ProductionPreparedOutputDeliveryTargetKind),
                    revision.targetKind)
                || !Enum.IsDefined(
                    typeof(ProductionPreparedOutputDeliveryRerouteReason),
                    revision.reason))
            {
                throw new InvalidOperationException(
                    "Delivery revision sequence is invalid.");
            }
            RequireCanonicalOptional(
                revision.targetDestinationId,
                "delivery.target");
            RequireCanonicalOptional(
                revision.rerouteOperationId,
                "delivery.rerouteOperation");
            RequireCanonicalOptional(
                revision.previousRevisionFingerprint,
                "delivery.previousFingerprint");
            RequireCanonicalOptional(
                revision.originalPhysicalReceiptFingerprint,
                "delivery.originalReceipt");
            RequireCanonicalOptional(
                revision.targetAuthorityFingerprint,
                "delivery.targetAuthority");
            RequireDigest(revision.revisionFingerprint,
                "delivery.revisionFingerprint");

            if (index == 0)
            {
                ProductionPreparedOutputDeliveryTargetKind initialKind =
                    string.IsNullOrEmpty(operation.targetDestinationId)
                        ? ProductionPreparedOutputDeliveryTargetKind
                            .WarehouseSelectionPending
                        : ProductionPreparedOutputDeliveryTargetKind
                            .InitialExactTarget;
                if (revision.targetKind != initialKind
                    || revision.reason !=
                        ProductionPreparedOutputDeliveryRerouteReason.InitialRoute
                    || !string.IsNullOrEmpty(revision.rerouteOperationId)
                    || !string.IsNullOrEmpty(revision.previousRevisionFingerprint)
                    || !string.Equals(revision.targetDestinationId,
                        operation.targetDestinationId, StringComparison.Ordinal)
                    || revision.targetPositionX != operation.targetPositionX
                    || revision.targetPositionY != operation.targetPositionY
                    || !string.IsNullOrEmpty(revision.targetAuthorityFingerprint))
                {
                    throw new InvalidOperationException(
                        "Initial delivery revision conflicts with original request.");
                }
            }
            else
            {
                RequireCanonical(revision.targetDestinationId,
                    "delivery.rerouteTarget");
                RequireDigest(revision.targetAuthorityFingerprint,
                    "delivery.targetAuthority");
                RequireDigest(revision.previousRevisionFingerprint,
                    "delivery.previousFingerprint");
                RequireCanonical(revision.rerouteOperationId,
                    "delivery.rerouteOperation");
                if (revision.targetKind !=
                        ProductionPreparedOutputDeliveryTargetKind.ExactRerouteTarget
                    || revision.reason ==
                        ProductionPreparedOutputDeliveryRerouteReason.InitialRoute
                    || previous == null
                    || !string.Equals(revision.previousRevisionFingerprint,
                        previous.revisionFingerprint, StringComparison.Ordinal)
                    || !string.Equals(revision.rerouteOperationId,
                        BuildDeliveryRerouteOperationId(
                            operation.routeOperationId,
                            revision.revision,
                            revision.previousRevisionFingerprint,
                            revision.originalPhysicalReceiptFingerprint,
                            revision.reason,
                            revision.targetDestinationId,
                            revision.targetPositionX,
                            revision.targetPositionY,
                            revision.targetAuthorityFingerprint),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Delivery reroute revision conflicts with its predecessor.");
                }
            }

            string expectedReceipt = pendingPhysical
                ? string.Empty
                : operation.physicalReceiptFingerprint;
            if (!string.Equals(revision.originalPhysicalReceiptFingerprint,
                    expectedReceipt, StringComparison.Ordinal)
                || !string.Equals(revision.revisionFingerprint,
                    DeliveryRevisionFingerprint(
                        operation.routeOperationId,
                        operation.requestFingerprint,
                        revision),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Delivery revision changed its original physical receipt or fingerprint.");
            }
            if (pendingPhysical && index > 0)
            {
                throw new InvalidOperationException(
                    "A physically pending route cannot have a reroute revision.");
            }
            previous = revision;
        }
    }

    internal static string DeliveryRevisionFingerprint(
        string routeOperationId,
        string requestFingerprint,
        ProductionPreparedOutputDeliveryRevisionSaveData revision) => Digest(
        DeliveryRevisionV1,
        routeOperationId,
        requestFingerprint,
        revision.revision.ToString(CultureInfo.InvariantCulture),
        ((int)revision.targetKind).ToString(CultureInfo.InvariantCulture),
        ((int)revision.reason).ToString(CultureInfo.InvariantCulture),
        revision.rerouteOperationId,
        revision.previousRevisionFingerprint,
        revision.originalPhysicalReceiptFingerprint,
        revision.targetDestinationId,
        revision.targetPositionX.ToString(CultureInfo.InvariantCulture),
        revision.targetPositionY.ToString(CultureInfo.InvariantCulture),
        revision.targetAuthorityFingerprint);

    internal static string BuildDeliveryRerouteOperationId(
        string routeOperationId,
        long revision,
        string previousRevisionFingerprint,
        string originalPhysicalReceiptFingerprint,
        ProductionPreparedOutputDeliveryRerouteReason reason,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        string targetAuthorityFingerprint) =>
        "production-output-delivery-reroute:" + Digest(
            DeliveryRerouteOperationV1,
            routeOperationId,
            revision.ToString(CultureInfo.InvariantCulture),
            previousRevisionFingerprint,
            originalPhysicalReceiptFingerprint,
            ((int)reason).ToString(CultureInfo.InvariantCulture),
            targetDestinationId,
            targetPositionX.ToString(CultureInfo.InvariantCulture),
            targetPositionY.ToString(CultureInfo.InvariantCulture),
            targetAuthorityFingerprint);

    internal static string BuildOperationId(
        string batchId,
        string lineId,
        int sourceOffset) => "production-output-route:" + Digest(
        "production-output-route-operation-v1", batchId, lineId,
        sourceOffset.ToString(CultureInfo.InvariantCulture));

    internal static string RoutingFingerprint(
        ProductionPreparedOutputRoutingBatchSaveData batch)
    {
        List<string> values = new()
        {
            RoutingV9, batch.batchCommitId, batch.ownerBillId,
            batch.ownerRecipeId, batch.ownerFacilityId,
            batch.cycleSequence.ToString(CultureInfo.InvariantCulture),
            batch.outcomeFingerprint, batch.destinationId,
            batch.capacitySourceDigest,
            batch.outputBufferCycleCapacity.ToString(
                CultureInfo.InvariantCulture),
            batch.projectedPortfolioCapacityGrams.ToString(
                CultureInfo.InvariantCulture),
            batch.requiredMinimumCapacityGrams.ToString(
                CultureInfo.InvariantCulture),
            batch.maximumMassProofDigest,
            batch.maximumBatchMassGrams.ToString(
                CultureInfo.InvariantCulture),
            batch.capacityClaimDigest,
            batch.totalDeclaredLossMassGrams.ToString(
                CultureInfo.InvariantCulture),
            batch.totalDeclaredExternalInputMassGrams.ToString(
                CultureInfo.InvariantCulture)
        };
        foreach (ProductionPreparedOutputNonPhysicalDispositionSaveData
                 disposition in batch.nonPhysicalDispositions.OrderBy(
                     value => value.outputLineId,
                     StringComparer.Ordinal))
        {
            values.Add(disposition.lineCommitId);
            values.Add(disposition.outputLineId);
            values.Add(((int)disposition.role).ToString(
                CultureInfo.InvariantCulture));
            values.Add(disposition.canonicalPayload);
            values.Add(disposition.dispositionFingerprint);
            values.Add(disposition.exactMassGrams.ToString(
                CultureInfo.InvariantCulture));
        }
        foreach (var line in batch.lines.OrderBy(value => value.outputLineId,
                     StringComparer.Ordinal))
        {
            values.Add(line.lineCommitId);
            values.Add(line.outputLineId);
            values.Add(((int)line.role).ToString(CultureInfo.InvariantCulture));
            values.Add(line.itemId);
            values.Add(line.componentFingerprint);
            values.Add(line.outputCapabilityId);
            values.Add(line.outputCapabilityVersion.ToString(
                CultureInfo.InvariantCulture));
            values.Add(line.outputComponentCodecId);
            values.Add(line.outputComponentCodecVersion.ToString(
                CultureInfo.InvariantCulture));
            values.Add(line.outputCapabilityFingerprint);
            values.Add(line.originalQuantity.ToString(CultureInfo.InvariantCulture));
            values.Add(line.originalMassGrams.ToString(CultureInfo.InvariantCulture));
        }
        return Digest(values.ToArray());
    }

    internal static string RequestFingerprint(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line,
        ProductionPreparedOutputRouteOperationSaveData operation) => Digest(
        RequestV1, operation.routeOperationId, batch.batchCommitId,
        line.destinationId, operation.targetDestinationId,
        operation.targetPositionX.ToString(CultureInfo.InvariantCulture),
        operation.targetPositionY.ToString(CultureInfo.InvariantCulture),
        line.outputLineId, line.lineCommitId,
        operation.sourceOffsetQuantity.ToString(CultureInfo.InvariantCulture),
        operation.routedQuantity.ToString(CultureInfo.InvariantCulture),
        line.itemId, line.componentFingerprint,
        operation.routedMassGrams.ToString(CultureInfo.InvariantCulture));

    internal static string ComputePhysicalReceiptFingerprint(
        ProductionPreparedOutputPhysicalRouteReceipt receipt)
    {
        List<string> values = new()
        {
            ReceiptV1, receipt.RequestFingerprint, receipt.RouteOperationId,
            receipt.BatchCommitId, receipt.SourceDestinationId,
            receipt.TargetDestinationId,
            receipt.TargetPositionX.ToString(CultureInfo.InvariantCulture),
            receipt.TargetPositionY.ToString(CultureInfo.InvariantCulture),
            receipt.TotalQuantity.ToString(CultureInfo.InvariantCulture),
            receipt.TotalMassGrams.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var slice in (receipt.Slices ?? Array.Empty<
                     ProductionPreparedOutputPhysicalRouteSliceReceipt>())
                 .OrderBy(value => value.SourceStackId, StringComparer.Ordinal)
                 .ThenBy(value => value.RoutedStackId, StringComparer.Ordinal))
        {
            values.Add(slice.SourceStackId);
            values.Add(slice.RoutedStackId);
            values.Add(slice.OutputLineId);
            values.Add(slice.LineCommitId);
            values.Add(slice.SourceOffsetQuantity.ToString(CultureInfo.InvariantCulture));
            values.Add(slice.RoutedOffsetQuantity.ToString(CultureInfo.InvariantCulture));
            values.Add(slice.RoutedQuantity.ToString(CultureInfo.InvariantCulture));
            values.Add(slice.ItemId);
            values.Add(slice.ComponentFingerprint);
            values.Add(slice.RoutedMassGrams.ToString(CultureInfo.InvariantCulture));
        }
        return Digest(values.ToArray());
    }

    internal static string SavedReceiptFingerprint(
        ProductionPreparedOutputRoutingBatchSaveData batch,
        ProductionPreparedOutputRoutingLineSaveData line,
        ProductionPreparedOutputRouteOperationSaveData operation)
    {
        ProductionPreparedOutputPhysicalRouteSliceReceipt[] slices =
            operation.physicalSlices.Select(value => new
                ProductionPreparedOutputPhysicalRouteSliceReceipt(
                    value.sourceStackId,
                    value.routedStackId,
                    line.outputLineId,
                    line.lineCommitId,
                    line.itemId,
                    value.sourceOffsetQuantity,
                    value.routedOffsetQuantity,
                    value.routedQuantity,
                    value.routedMassGrams,
                    line.componentFingerprint))
                .ToArray();
        return ComputePhysicalReceiptFingerprint(new
            ProductionPreparedOutputPhysicalRouteReceipt(
                operation.routeOperationId,
                operation.requestFingerprint,
                operation.physicalReceiptFingerprint,
                batch.batchCommitId,
                line.destinationId,
                operation.targetDestinationId,
                operation.targetPositionX,
                operation.targetPositionY,
                operation.routedQuantity,
                operation.routedMassGrams,
                slices));
    }

    internal static string Digest(params string[] values)
    {
        StringBuilder text = new();
        foreach (string value in values)
        {
            string exact = value ?? string.Empty;
            text.Append(Encoding.UTF8.GetByteCount(exact).ToString(
                CultureInfo.InvariantCulture));
            text.Append(':').Append(exact).Append('|');
        }
        using SHA256 sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))
            .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    internal static bool IsPhysicalRole(ProductionOutputRole role) =>
        ProductionOutputRoleRules.IsPhysical(role);

    internal static void RequireCanonical(string value, string label)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException($"Routing {label} is not canonical.");
    }

    internal static void RequireCanonicalOptional(string value, string label)
    {
        if (value == null
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Routing {label} is not canonical optional text.");
        }
    }

    internal static void RequireDigest(string value, string label)
    {
        if (!IsDigest(value))
            throw new InvalidOperationException($"Routing {label} is not SHA-256.");
    }

    internal static bool IsDigest(string value) =>
        value != null && value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
}
